using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Buildings = await db.Buildings.OrderBy(x => x.Name).ToListAsync();
        ViewBag.Equipment = await db.Equipment.Include(x => x.Building).OrderBy(x => x.Code).ToListAsync();
        ViewBag.Templates = await db.ChecklistTemplates.Include(x => x.Items).OrderByDescending(x => x.Version).ToListAsync();
        ViewBag.Users = await db.Users.Include(x => x.BuildingAccesses).ThenInclude(x => x.Building).OrderBy(x => x.UserName).ToListAsync();
        var userRoles = await db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name }).ToListAsync();
        ViewBag.UserRoles = userRoles.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name)));
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Building(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 30 || string.IsNullOrWhiteSpace(name) || name.Length > 120 || await db.Buildings.AnyAsync(x => x.Code == code.Trim()))
            return Fail("Kode gedung harus unik, dan nama wajib diisi.");
        db.Buildings.Add(new Building { Code = code.Trim(), Name = name.Trim() });
        await db.SaveChangesAsync(); return Done("Gedung dibuat.");
    }
    [HttpPost]
    public async Task<IActionResult> Equipment(int buildingId, string code, string name)
    {
        if (!await db.Buildings.AnyAsync(x => x.Id == buildingId && x.IsActive) || string.IsNullOrWhiteSpace(code) || code.Length > 30 || string.IsNullOrWhiteSpace(name) || name.Length > 120 || await db.Equipment.AnyAsync(x => x.Code == code.Trim()))
            return Fail("Gedung aktif, kode peralatan unik, dan nama wajib diisi.");
        db.Equipment.Add(new Equipment { BuildingId = buildingId, Code = code.Trim(), Name = name.Trim() });
        await db.SaveChangesAsync(); return Done("Peralatan dibuat.");
    }
    [HttpPost]
    public async Task<IActionResult> Template(string name, string questions)
    {
        var items = (questions ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || items.Length < 3 || items.Any(x => x.Length > 300))
            return Fail("Nama dan sedikitnya tiga butir checklist wajib diisi.");
        var version = (await db.ChecklistTemplates.Where(x => x.Name == name.Trim()).MaxAsync(x => (int?)x.Version) ?? 0) + 1;
        var old = await db.ChecklistTemplates.Where(x => x.IsActive).ToListAsync();
        foreach (var t in old) t.IsActive = false;
        db.ChecklistTemplates.Add(new ChecklistTemplate { Name = name.Trim(), Version = version,
            Items = items.Select((q, n) => new ChecklistItem { Order = n + 1, Question = q }).ToList() });
        await db.SaveChangesAsync(); return Done("Versi checklist baru dibuat dan diaktifkan.");
    }
    [HttpPost]
    public async Task<IActionResult> UserAccount(string userName, string displayName, string password, string role, int? buildingId)
    {
        if (!new[] { Roles.Admin, Roles.PICBuilding, Roles.Rescue, Roles.Management }.Contains(role) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100 || (role != Roles.Admin && (!buildingId.HasValue || !await db.Buildings.AnyAsync(x => x.Id == buildingId && x.IsActive))))
            return Fail("Data pengguna, peran, dan gedung harus valid.");
        var user = new ApplicationUser { UserName = userName.Trim(), DisplayName = displayName.Trim(), IsActive = true };
        var result = await users.CreateAsync(user, password ?? "");
        if (!result.Succeeded) return Fail(string.Join("; ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, role);
        if (buildingId.HasValue && role != Roles.Admin) db.BuildingAccesses.Add(new BuildingAccess { UserId = user.Id, BuildingId = buildingId.Value });
        await db.SaveChangesAsync(); return Done("Pengguna dibuat.");
    }
    [HttpPost]
    public async Task<IActionResult> ToggleBuilding(int id)
    {
        var b = await db.Buildings.FindAsync(id); if (b == null) return NotFound();
        b.IsActive = !b.IsActive; await db.SaveChangesAsync(); return Done("Status gedung diubah.");
    }
    [HttpPost]
    public async Task<IActionResult> ToggleEquipment(int id)
    {
        var e = await db.Equipment.FindAsync(id); if (e == null) return NotFound();
        e.IsActive = !e.IsActive; await db.SaveChangesAsync(); return Done("Status peralatan diubah.");
    }
    [HttpPost]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var u = await users.FindByIdAsync(id); if (u == null) return NotFound();
        if (u.Id == users.GetUserId(User)) return Fail("Akun sendiri tidak dapat dinonaktifkan.");
        u.IsActive = !u.IsActive; await users.UpdateAsync(u); return Done("Status pengguna diubah.");
    }
    [HttpPost]
    public async Task<IActionResult> AssignBuilding(string userId, int buildingId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || !await db.Buildings.AnyAsync(x => x.Id == buildingId) || await users.IsInRoleAsync(user, Roles.Admin)) return Fail("Penugasan tidak valid.");
        if (!await db.BuildingAccesses.AnyAsync(x => x.UserId == userId && x.BuildingId == buildingId))
        { db.BuildingAccesses.Add(new BuildingAccess { UserId = userId, BuildingId = buildingId }); await db.SaveChangesAsync(); }
        return Done("Akses gedung ditambahkan.");
    }
    [HttpPost]
    public async Task<IActionResult> RevokeBuilding(string userId, int buildingId)
    {
        var access = await db.BuildingAccesses.FindAsync(userId, buildingId);
        if (access == null) return NotFound();
        db.BuildingAccesses.Remove(access);
        await db.SaveChangesAsync();
        return Done("Akses gedung dicabut.");
    }
    [HttpPost]
    public async Task<IActionResult> ChangeRole(string userId, string role)
    {
        if (!new[] { Roles.Admin, Roles.PICBuilding, Roles.Rescue, Roles.Management }.Contains(role)) return Fail("Peran tidak valid.");
        var user = await users.FindByIdAsync(userId);
        if (user == null) return NotFound();
        if (user.Id == users.GetUserId(User)) return Fail("Peran akun sendiri tidak dapat diubah.");
        var old = await users.GetRolesAsync(user);
        if (old.Count > 0) await users.RemoveFromRolesAsync(user, old);
        await users.AddToRoleAsync(user, role);
        return Done("Peran pengguna diubah.");
    }
    private IActionResult Done(string message) { TempData["Success"] = message; return RedirectToAction(nameof(Index)); }
    private IActionResult Fail(string message) { TempData["Error"] = message; return RedirectToAction(nameof(Index)); }
}
