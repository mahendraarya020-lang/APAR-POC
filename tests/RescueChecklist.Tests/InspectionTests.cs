using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;
using RescueChecklist.Web.Services;

namespace RescueChecklist.Tests;

public class InspectionTests
{
    private static ClaimsPrincipal Principal(string id, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "Test"));

    private static async Task<(ApplicationDbContext Db, SqliteConnection Connection, int EquipmentId, int TemplateId, int[] ItemIds)> Database()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "rescue-1", UserName = "rescue-1", DisplayName = "Rescue Test" });
        var assigned = new Building { Code = "A", Name = "Assigned" };
        var other = new Building { Code = "B", Name = "Other" };
        db.Buildings.AddRange(assigned, other);
        var equipment = new Equipment { Code = "E-A", Name = "Tandu", Building = assigned };
        db.Equipment.AddRange(equipment, new Equipment { Code = "E-B", Name = "P3K", Building = other });
        var template = new ChecklistTemplate { Name = "Sample", Version = 1,
            Items = [new ChecklistItem { Order = 1, Question = "Satu" }, new ChecklistItem { Order = 2, Question = "Dua" }, new ChecklistItem { Order = 3, Question = "Tiga" }] };
        db.ChecklistTemplates.Add(template);
        await db.SaveChangesAsync();
        db.BuildingAccesses.Add(new BuildingAccess { UserId = "rescue-1", BuildingId = assigned.Id });
        await db.SaveChangesAsync();
        return (db, connection, equipment.Id, template.Id, template.Items.OrderBy(x => x.Order).Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task BuildingScopeExcludesUnassignedEquipment()
    {
        var (db, connection, _, _, _) = await Database();
        await using var ownedDb = db; await using var ownedConnection = connection;
        var result = await new AccessScope(db).Equipment(Principal("rescue-1", Roles.Rescue)).Select(x => x.Code).ToListAsync();
        Assert.Equal(["E-A"], result);
    }

    [Fact]
    public async Task SubmissionRequiresFindingNoteAndIsIdempotent()
    {
        var (db, connection, equipmentId, templateId, ids) = await Database();
        await using var ownedDb = db; await using var ownedConnection = connection;
        var service = new InspectionService(db, new AccessScope(db));
        var principal = Principal("rescue-1", Roles.Rescue);
        var input = new InspectionInput { ClientSubmissionId = Guid.NewGuid(), EquipmentId = equipmentId, ChecklistTemplateId = templateId, TemplateVersion = 1, StartedAt = DateTime.UtcNow,
            Answers = [new AnswerInput { ChecklistItemId = ids[0], Result = "PerluTindakan" }, new AnswerInput { ChecklistItemId = ids[1], Result = "Baik" }, new AnswerInput { ChecklistItemId = ids[2], Result = "Baik" }] };
        var failure = await Assert.ThrowsAsync<InspectionFailure>(() => service.SubmitAsync(principal, input));
        Assert.Equal(400, failure.StatusCode);
        input.Answers[0].Note = "Perlu diganti";
        var first = await service.SubmitAsync(principal, input);
        var second = await service.SubmitAsync(principal, input);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.Inspections.CountAsync());
    }

    [Fact]
    public async Task PicCannotSubmitAndTemplateConflictKeepsNoInspection()
    {
        var (db, connection, equipmentId, templateId, ids) = await Database();
        await using var ownedDb = db; await using var ownedConnection = connection;
        var service = new InspectionService(db, new AccessScope(db));
        var input = new InspectionInput { ClientSubmissionId = Guid.NewGuid(), EquipmentId = equipmentId, ChecklistTemplateId = templateId, TemplateVersion = 2, StartedAt = DateTime.UtcNow,
            Answers = ids.Select(id => new AnswerInput { ChecklistItemId = id, Result = "Baik" }).ToList() };
        Assert.Equal(403, (await Assert.ThrowsAsync<InspectionFailure>(() => service.SubmitAsync(Principal("rescue-1", Roles.PICBuilding), input))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<InspectionFailure>(() => service.SubmitAsync(Principal("rescue-1", Roles.Rescue), input))).StatusCode);
        Assert.Empty(await db.Inspections.ToListAsync());
    }
}
