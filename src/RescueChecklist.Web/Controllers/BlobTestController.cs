using System.Text.RegularExpressions;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Controllers;

public sealed record BlobTestFile(string BlobName, string DisplayName, long SizeBytes, DateTimeOffset? LastModified, bool CanPreview);
public sealed record BlobTestPageModel(bool IsConfigured, string ContainerName, IReadOnlyList<BlobTestFile> Files);

/// <summary>
/// Isolated admin-only Blob Storage connectivity test. No database changes.
/// Uploaded objects live under poc-test/ inside a private container.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class BlobTestController(IConfiguration configuration, ILogger<BlobTestController> logger) : Controller
{
    private const string Prefix = "poc-test/";
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

    private BlobContainerClient? Container()
    {
        var uriText = configuration["BlobStorage:ServiceUri"];
        var containerName = configuration["BlobStorage:ContainerName"];
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(containerName))
            return null;

        return new BlobServiceClient(uri, new DefaultAzureCredential())
            .GetBlobContainerClient(containerName);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var container = Container();
        var containerName = configuration["BlobStorage:ContainerName"] ?? "(belum disetel)";
        if (container is null)
            return View(new BlobTestPageModel(false, containerName, Array.Empty<BlobTestFile>()));

        var files = new List<BlobTestFile>();
        try
        {
            await foreach (var item in container.GetBlobsAsync(prefix: Prefix, cancellationToken: HttpContext.RequestAborted))
            {
                var displayName = item.Name.Split('/').LastOrDefault() ?? "(file)";
                files.Add(new BlobTestFile(item.Name, displayName, item.Properties.ContentLength ?? 0,
                    item.Properties.LastModified, CanPreview(displayName)));
                if (files.Count >= 200) break;
            }
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(ex, "Cannot list Blob Storage POC test files");
            TempData["Error"] = "Tidak dapat membaca Blob Storage. Periksa IAM Storage Blob Data Contributor, endpoint, dan networking.";
        }

        return View(new BlobTestPageModel(true, containerName, files.OrderByDescending(f => f.LastModified).ToList()));
    }

    [HttpPost, RequestSizeLimit(MaxFileBytes + 65536)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0 || file.Length > MaxFileBytes)
        {
            TempData["Error"] = "Pilih file dengan ukuran 1 byte sampai 10 MB.";
            return RedirectToAction(nameof(Index));
        }

        var originalName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(originalName);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
        {
            TempData["Error"] = "Format yang diizinkan: PDF, PNG, JPG, GIF, WEBP, TXT, CSV, DOCX, XLSX.";
            return RedirectToAction(nameof(Index));
        }

        var container = Container();
        if (container is null)
        {
            TempData["Error"] = "Konfigurasi BlobStorage__ServiceUri dan BlobStorage__ContainerName belum lengkap.";
            return RedirectToAction(nameof(Index));
        }

        var safeName = Regex.Replace(originalName, "[^A-Za-z0-9._-]", "_");
        if (safeName.Length > 100) safeName = safeName[^100..];
        if (string.IsNullOrWhiteSpace(safeName.Trim('.','_'))) safeName = "file" + extension;
        var blobName = Prefix + Guid.NewGuid().ToString("N") + "/" + safeName;

        try
        {
            await using var stream = file.OpenReadStream();
            await container.GetBlobClient(blobName).UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, HttpContext.RequestAborted);

            TempData["Success"] = "File berhasil diunggah ke Azure Blob Storage.";
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(ex, "Blob Storage test upload failed");
            TempData["Error"] = "Upload gagal. Periksa izin Managed Identity, jaringan, dan konfigurasi container.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ViewFile(string? name, bool download = false)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith(Prefix, StringComparison.Ordinal) ||
            name.Split('/').Length != 3 || !Guid.TryParseExact(name.Split('/')[1], "N", out _))
            return BadRequest();

        var fileName = name.Split('/')[2];
        if (!AllowedTypes.TryGetValue(Path.GetExtension(fileName), out var contentType)) return BadRequest();

        var container = Container();
        if (container is null) return NotFound();

        try
        {
            var response = await container.GetBlobClient(name).DownloadStreamingAsync(cancellationToken: HttpContext.RequestAborted);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (download || !CanPreview(fileName))
                return File(response.Value.Content, contentType, fileName);

            return File(response.Value.Content, contentType, enableRangeProcessing: true);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return NotFound();
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(ex, "Blob Storage test view failed");
            return StatusCode(502, "Tidak dapat membaca file dari Blob Storage.");
        }
    }

    private static bool CanPreview(string fileName) =>
        new[] { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp" }
            .Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
