using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;
using RescueChecklist.Web.Services;

namespace RescueChecklist.Web.Controllers;

public record ReportRow(int BuildingId, string Building, int Inspections, int Findings);

[Authorize(Roles = Roles.Admin + "," + Roles.PICBuilding + "," + Roles.Management)]
public class ReportsController(AccessScope access) : Controller
{
    private async Task<List<ReportRow>> Rows(int? buildingId, DateTime? from, DateTime? to)
    {
        var query = access.Inspections(User).Include(x => x.Equipment).ThenInclude(x => x.Building).Include(x => x.Answers).AsQueryable();
        if (buildingId.HasValue) query = query.Where(x => x.Equipment.BuildingId == buildingId);
        if (from.HasValue) query = query.Where(x => x.SubmittedAt >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));
        if (to.HasValue) query = query.Where(x => x.SubmittedAt < DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
        var data = await query.ToListAsync();
        return data.GroupBy(x => new { x.Equipment.BuildingId, x.Equipment.Building.Name })
            .Select(g => new ReportRow(g.Key.BuildingId, g.Key.Name, g.Count(), g.Sum(x => x.Answers.Count(a => a.Result == "PerluTindakan"))))
            .OrderBy(x => x.Building).ToList();
    }
    public async Task<IActionResult> Index(int? buildingId, DateTime? from, DateTime? to)
    {
        ViewBag.Buildings = await access.Buildings(User).OrderBy(x => x.Name).ToListAsync();
        ViewBag.BuildingId = buildingId;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(await Rows(buildingId, from, to));
    }
    [HttpGet("/Reports/Export.csv")]
    public async Task<IActionResult> Export(int? buildingId, DateTime? from, DateTime? to)
    {
        static string Cell(string value)
        {
            if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var rows = await Rows(buildingId, from, to);
        var csv = new StringBuilder("\uFEFFGedung,Jumlah inspeksi,Jumlah temuan\r\n");
        foreach (var row in rows) csv.Append(Cell(row.Building)).Append(',').Append(row.Inspections).Append(',').Append(row.Findings).Append("\r\n");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "rescue-report.csv");
    }
}
