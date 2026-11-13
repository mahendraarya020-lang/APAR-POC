using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;
using RescueChecklist.Web.Services;
using System.Security.Claims;

namespace RescueChecklist.Web.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.PICBuilding)]
public class FollowUpsController(ApplicationDbContext db, AccessScope access) : Controller
{
    public async Task<IActionResult> Index()
    {
        var inspections = access.Inspections(User);
        var findings = await db.InspectionAnswers.Include(x => x.Item).Include(x => x.Inspection).ThenInclude(x => x.Equipment).ThenInclude(x => x.Building)
            .Include(x => x.FollowUps).Where(x => x.Result == "PerluTindakan" && inspections.Any(i => i.Id == x.InspectionId))
            .OrderByDescending(x => x.Inspection.SubmittedAt).Take(200).ToListAsync();
        return View(findings);
    }
    [HttpPost]
    public async Task<IActionResult> Create(int inspectionAnswerId, string note, string status)
    {
        var inspections = access.Inspections(User);
        var answer = await db.InspectionAnswers.FirstOrDefaultAsync(x => x.Id == inspectionAnswerId && x.Result == "PerluTindakan" && inspections.Any(i => i.Id == x.InspectionId));
        if (answer == null) return NotFound();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000 || !new[] { "Terbuka", "DalamProses", "Selesai" }.Contains(status))
        {
            TempData["Error"] = "Catatan dan status tindak lanjut wajib diisi dengan benar.";
            return RedirectToAction(nameof(Index));
        }
        db.FollowUps.Add(new FollowUp { InspectionAnswerId = answer.Id, AuthorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, Note = note.Trim(), Status = status, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        TempData["Success"] = "Tindak lanjut tersimpan.";
        return RedirectToAction(nameof(Index));
    }
}
