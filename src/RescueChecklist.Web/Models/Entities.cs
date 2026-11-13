using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace RescueChecklist.Web.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string PICBuilding = "PICBuilding";
    public const string Rescue = "Rescue";
    public const string Management = "Management";
}

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)] public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<BuildingAccess> BuildingAccesses { get; set; } = [];
}
public class Building
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<Equipment> Equipment { get; set; } = [];
}
public class BuildingAccess
{
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
}
public class Equipment
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public class ChecklistTemplate
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public ICollection<ChecklistItem> Items { get; set; } = [];
}
public class ChecklistItem
{
    public int Id { get; set; }
    public int ChecklistTemplateId { get; set; }
    public ChecklistTemplate Template { get; set; } = null!;
    public int Order { get; set; }
    [Required, StringLength(300)] public string Question { get; set; } = "";
    public bool IsRequired { get; set; } = true;
}
public class Inspection
{
    public int Id { get; set; }
    public Guid ClientSubmissionId { get; set; }
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public int ChecklistTemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public string InspectorUserId { get; set; } = "";
    public ApplicationUser Inspector { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = "Terkirim";
    public ICollection<InspectionAnswer> Answers { get; set; } = [];
}
public class InspectionAnswer
{
    public int Id { get; set; }
    public int InspectionId { get; set; }
    public Inspection Inspection { get; set; } = null!;
    public int ChecklistItemId { get; set; }
    public ChecklistItem Item { get; set; } = null!;
    [Required] public string Result { get; set; } = "";
    [StringLength(1000)] public string? Note { get; set; }
    public ICollection<FollowUp> FollowUps { get; set; } = [];
}
public class FollowUp
{
    public int Id { get; set; }
    public int InspectionAnswerId { get; set; }
    public InspectionAnswer Answer { get; set; } = null!;
    public string AuthorUserId { get; set; } = "";
    public ApplicationUser Author { get; set; } = null!;
    [Required, StringLength(1000)] public string Note { get; set; } = "";
    public string Status { get; set; } = "Terbuka";
    public DateTime CreatedAt { get; set; }
}
