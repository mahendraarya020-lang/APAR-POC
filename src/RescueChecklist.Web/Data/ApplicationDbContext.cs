using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Models;

namespace RescueChecklist.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingAccess> BuildingAccesses => Set<BuildingAccess>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionAnswer> InspectionAnswers => Set<InspectionAnswer>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Building>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Equipment>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ChecklistTemplate>().HasIndex(x => new { x.Name, x.Version }).IsUnique();
        b.Entity<Inspection>().HasIndex(x => x.ClientSubmissionId).IsUnique();
        b.Entity<BuildingAccess>().HasKey(x => new { x.UserId, x.BuildingId });
        b.Entity<BuildingAccess>().HasOne(x => x.User).WithMany(x => x.BuildingAccesses).HasForeignKey(x => x.UserId);
        b.Entity<Inspection>().HasOne(x => x.Inspector).WithMany().HasForeignKey(x => x.InspectorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FollowUp>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InspectionAnswer>().HasIndex(x => new { x.InspectionId, x.ChecklistItemId }).IsUnique();
    }
}
