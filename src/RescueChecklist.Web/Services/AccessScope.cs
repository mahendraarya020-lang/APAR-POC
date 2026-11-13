using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Services;

public class AccessScope(ApplicationDbContext db)
{
    public IQueryable<Building> Buildings(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(Roles.Admin)) return db.Buildings;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.Buildings.Where(b => db.BuildingAccesses.Any(a => a.UserId == id && a.BuildingId == b.Id));
    }
    public IQueryable<Equipment> Equipment(ClaimsPrincipal principal)
        => db.Equipment.Where(e => Buildings(principal).Any(b => b.Id == e.BuildingId));
    public IQueryable<Inspection> Inspections(ClaimsPrincipal principal)
        => db.Inspections.Where(i => Buildings(principal).Any(b => b.Id == i.Equipment.BuildingId));
    public Task<bool> CanAccessBuildingAsync(ClaimsPrincipal principal, int id)
        => Buildings(principal).AnyAsync(b => b.Id == id);
}
