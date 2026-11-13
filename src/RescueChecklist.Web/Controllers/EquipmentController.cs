using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Services;

namespace RescueChecklist.Web.Controllers;

[Authorize]
public class EquipmentController(AccessScope access) : Controller
{
    public async Task<IActionResult> Index(string? q, int? buildingId)
    {
        var query = access.Equipment(User).Include(x => x.Building).Where(x => x.IsActive && x.Building.IsActive);
        if (buildingId.HasValue) query = query.Where(x => x.BuildingId == buildingId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q));
        ViewBag.Query = q;
        ViewBag.BuildingId = buildingId;
        ViewBag.Buildings = await access.Buildings(User).Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        return View(await query.OrderBy(x => x.Building.Name).ThenBy(x => x.Code).ToListAsync());
    }
}
