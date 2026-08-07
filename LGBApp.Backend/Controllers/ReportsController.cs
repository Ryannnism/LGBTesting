using LGBApp.Backend.Data;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LGBApp.Backend.Controllers;

/// <summary>
/// B5 quarterly billing report. Admin-only: there is no separate Finance role, so the Finance
/// Head uses an Admin account.
/// </summary>
[Route("api/reports")]
[ApiController]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context) => _context = context;

    [HttpGet("billing/quarterly")]
    public async Task<IActionResult> QuarterlyBilling(
        [FromQuery] int? year,
        [FromQuery] int? quarter,
        [FromQuery] string format = "pdf")
    {
        var now = DateTime.UtcNow;
        var reportYear = year ?? now.Year;
        var reportQuarter = quarter ?? ((now.Month - 1) / 3 + 1);

        if (reportQuarter is < 1 or > 4)
            return BadRequest(new { message = "Quarter must be between 1 and 4." });
        if (reportYear is < 2000 or > 2999)
            return BadRequest(new { message = "Year is out of range." });

        var report = await BillingReportService.BuildAsync(_context, reportYear, reportQuarter);
        var fileName = $"LGB-billing-Q{reportQuarter}-{reportYear}";

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return File(BillingReportService.ToCsv(report), "text/csv", $"{fileName}.csv");

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            return Ok(report);

        return File(BillingReportPdfService.Build(report), "application/pdf", $"{fileName}.pdf");
    }
}
