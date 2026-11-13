using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;
using RescueChecklist.Web.Services;

namespace RescueChecklist.Web.Controllers;

[Authorize]
public class InspectionsController(ApplicationDbContext db, AccessScope access, InspectionService service) : Controller
{
    public async Task<IActionResult> Index(int? buildingId, DateTime? from, DateTime? to)
    {
        var query = access.Inspections(User).Include(x => x.Equipment).ThenInclude(x => x.Building).Include(x => x.Inspector).AsQueryable();
        if (buildingId.HasValue) query = query.Where(x => x.Equipment.BuildingId == buildingId);
        if (from.HasValue) query = query.Where(x => x.SubmittedAt >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));
        if (to.HasValue) query = query.Where(x => x.SubmittedAt < DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
        ViewBag.Buildings = await access.Buildings(User).OrderBy(x => x.Name).ToListAsync();
        ViewBag.BuildingId = buildingId;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(await query.OrderByDescending(x => x.SubmittedAt).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var inspection = await access.Inspections(User).Include(x => x.Equipment).ThenInclude(x => x.Building)
            .Include(x => x.Inspector).Include(x => x.Answers).ThenInclude(x => x.Item)
            .Include(x => x.Answers).ThenInclude(x => x.FollowUps).ThenInclude(x => x.Author).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        return inspection == null ? NotFound() : View(inspection);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Rescue), HttpGet]
    public async Task<IActionResult> Create(int equipmentId)
    {
        var equipment = await access.Equipment(User).Include(x => x.Building).FirstOrDefaultAsync(x => x.Id == equipmentId && x.IsActive && x.Building.IsActive);
        if (equipment == null) return NotFound();
        var template = await db.ChecklistTemplates.Include(x => x.Items).Where(x => x.IsActive).OrderByDescending(x => x.Version).FirstOrDefaultAsync();
        if (template == null) return Problem("Belum ada checklist aktif.");
        ViewBag.Template = template;
        return View(equipment);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Rescue), HttpPost, ActionName("Index")]
    public async Task<IActionResult> Submit(InspectionInput input)
    {
        try
        {
            var inspection = await service.SubmitAsync(User, input);
            TempData["Success"] = "Inspeksi berhasil dikirim.";
            return RedirectToAction(nameof(Details), new { id = inspection.Id });
        }
        catch (InspectionFailure ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create), new { equipmentId = input.EquipmentId });
        }
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Rescue), HttpPost("/api/inspections/sync")]
    public async Task<IActionResult> Sync([FromBody] InspectionInput input)
    {
        try
        {
            var inspection = await service.SubmitAsync(User, input);
            return Ok(new { id = inspection.Id, message = "Tersinkron" });
        }
        catch (InspectionFailure ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("/api/offline/tasks")]
    public async Task<IActionResult> Tasks()
    {
        var equipment = await access.Equipment(User).Include(x => x.Building).Where(x => x.IsActive && x.Building.IsActive)
            .Select(x => new { x.Id, x.Code, x.Name, x.BuildingId, BuildingName = x.Building.Name }).ToListAsync();
        var template = await db.ChecklistTemplates.Where(x => x.IsActive).OrderByDescending(x => x.Version)
            .Select(x => new { x.Id, x.Name, x.Version, Items = x.Items.OrderBy(i => i.Order).Select(i => new { i.Id, i.Question, i.IsRequired }).ToList() }).FirstOrDefaultAsync();
        return Ok(new { user = User.Identity?.Name, equipment, template });
    }
}
