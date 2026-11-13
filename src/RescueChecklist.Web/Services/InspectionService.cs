using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Services;

public class InspectionInput
{
    public Guid ClientSubmissionId { get; set; }
    public int EquipmentId { get; set; }
    public int ChecklistTemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public DateTime StartedAt { get; set; }
    public List<AnswerInput> Answers { get; set; } = [];
}
public class AnswerInput
{
    public int ChecklistItemId { get; set; }
    public string Result { get; set; } = "";
    public string? Note { get; set; }
}
public class InspectionFailure(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
public class InspectionService(ApplicationDbContext db, AccessScope access)
{
    public async Task<Inspection> SubmitAsync(ClaimsPrincipal principal, InspectionInput input)
    {
        if (!principal.IsInRole(Roles.Admin) && !principal.IsInRole(Roles.Rescue))
            throw new InspectionFailure(403, "Peran ini tidak dapat mengirim inspeksi.");
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InspectionFailure(401, "Silakan login kembali.");
        if (input.ClientSubmissionId == Guid.Empty) throw new InspectionFailure(400, "ID kiriman tidak valid.");
        var existing = await db.Inspections.FirstOrDefaultAsync(x => x.ClientSubmissionId == input.ClientSubmissionId);
        if (existing != null)
        {
            if (existing.InspectorUserId != userId) throw new InspectionFailure(409, "ID kiriman sudah dipakai pengguna lain.");
            return existing;
        }
        var equipment = await access.Equipment(principal).FirstOrDefaultAsync(x => x.Id == input.EquipmentId && x.IsActive && x.Building.IsActive);
        if (equipment == null) throw new InspectionFailure(403, "Peralatan tidak tersedia untuk akun ini.");
        var template = await db.ChecklistTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == input.ChecklistTemplateId && x.IsActive);
        if (template == null || template.Version != input.TemplateVersion)
            throw new InspectionFailure(409, "Versi checklist berubah. Buka ulang formulir dan periksa jawabannya.");
        if (input.Answers.GroupBy(x => x.ChecklistItemId).Any(x => x.Count() > 1) || input.Answers.Any(x => template.Items.All(i => i.Id != x.ChecklistItemId)))
            throw new InspectionFailure(400, "Butir jawaban tidak sesuai checklist.");
        foreach (var item in template.Items.Where(x => x.IsRequired))
            if (input.Answers.All(x => x.ChecklistItemId != item.Id)) throw new InspectionFailure(400, $"Jawaban wajib belum diisi: {item.Question}");
        foreach (var answer in input.Answers)
        {
            if (!new[] { "Baik", "PerluTindakan", "TidakBerlaku" }.Contains(answer.Result))
                throw new InspectionFailure(400, "Pilihan kondisi tidak valid.");
            if (answer.Result == "PerluTindakan" && string.IsNullOrWhiteSpace(answer.Note))
                throw new InspectionFailure(400, "Catatan wajib untuk kondisi Perlu tindakan.");
            if (answer.Note?.Length > 1000) throw new InspectionFailure(400, "Catatan terlalu panjang.");
        }
        var inspection = new Inspection
        {
            ClientSubmissionId = input.ClientSubmissionId,
            EquipmentId = equipment.Id,
            ChecklistTemplateId = template.Id,
            TemplateVersion = template.Version,
            InspectorUserId = userId,
            StartedAt = input.StartedAt.Kind == DateTimeKind.Utc && input.StartedAt <= DateTime.UtcNow ? input.StartedAt : DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            Answers = input.Answers.Select(x => new InspectionAnswer { ChecklistItemId = x.ChecklistItemId, Result = x.Result, Note = x.Note?.Trim() }).ToList()
        };
        db.Inspections.Add(inspection);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(inspection).State = EntityState.Detached;
            existing = await db.Inspections.FirstOrDefaultAsync(x => x.ClientSubmissionId == input.ClientSubmissionId);
            if (existing?.InspectorUserId == userId) return existing;
            throw;
        }
        return inspection;
    }
}
