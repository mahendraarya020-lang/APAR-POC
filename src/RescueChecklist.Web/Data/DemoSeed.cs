using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Data;

public static class DemoSeed
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.PICBuilding, Roles.Rescue, Roles.Management })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

        if (!await db.Buildings.AnyAsync())
        {
            var building = new Building { Code = "BLD-A", Name = "Gedung Contoh A" };
            db.Buildings.Add(building);
            db.Equipment.AddRange(
                new Equipment { Building = building, Code = "EQ-001", Name = "Tandu lipat" },
                new Equipment { Building = building, Code = "EQ-002", Name = "Tas P3K" },
                new Equipment { Building = building, Code = "EQ-003", Name = "Alat bantu napas" });
            var template = new ChecklistTemplate { Name = "Pemeriksaan peralatan rescue", Version = 1 };
            template.Items.Add(new ChecklistItem { Order = 1, Question = "Kondisi fisik peralatan baik?" });
            template.Items.Add(new ChecklistItem { Order = 2, Question = "Peralatan bersih dan siap digunakan?" });
            template.Items.Add(new ChecklistItem { Order = 3, Question = "Komponen dan aksesori lengkap?" });
            db.ChecklistTemplates.Add(template);
            await db.SaveChangesAsync();
        }

        var password = Environment.GetEnvironmentVariable("RESCUE_DEMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(password)) return;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var buildingId = await db.Buildings.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        foreach (var (name, role, display) in new[] {
            ("admin", Roles.Admin, "Admin Demo"),
            ("pic", Roles.PICBuilding, "PIC Gedung Demo"),
            ("rescue", Roles.Rescue, "Petugas Rescue Demo"),
            ("management", Roles.Management, "Management Demo") })
        {
            var user = await users.FindByNameAsync(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name, DisplayName = display, IsActive = true };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded) throw new InvalidOperationException("Demo password rejected: " + string.Join("; ", result.Errors.Select(x => x.Description)));
            }
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
            if (role != Roles.Admin && !await db.BuildingAccesses.AnyAsync(x => x.UserId == user.Id && x.BuildingId == buildingId))
                db.BuildingAccesses.Add(new BuildingAccess { UserId = user.Id, BuildingId = buildingId });
        }
        await db.SaveChangesAsync();
    }
}
