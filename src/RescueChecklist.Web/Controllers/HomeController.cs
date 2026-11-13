using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Services;

namespace RescueChecklist.Web.Controllers;

[Authorize]
public class HomeController(ApplicationDbContext db, AccessScope access) : Controller
{
    public async Task<IActionResult> Index()
    {
        var inspections = access.Inspections(User);
        ViewBag.EquipmentCount = await access.Equipment(User).CountAsync();
        ViewBag.InspectionCount = await inspections.CountAsync();
        ViewBag.OpenFindings = await db.InspectionAnswers.CountAsync(a => a.Result == "PerluTindakan" &&
            inspections.Any(i => i.Id == a.InspectionId) &&
            a.FollowUps.OrderByDescending(f => f.CreatedAt).Select(f => f.Status).FirstOrDefault() != "Selesai");
        return View(await inspections.Include(x => x.Equipment).ThenInclude(x => x.Building)
            .Include(x => x.Inspector).OrderByDescending(x => x.SubmittedAt).Take(6).ToListAsync());
    }
    [AllowAnonymous] public IActionResult Error() => View();
}
