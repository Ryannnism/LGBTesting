using LGBApp.Backend.Data;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LGBApp.Backend.Controllers;

/// <summary>
/// Removes HANDOVER §11 walkthrough data. Exists because acceptance testing runs against the live
/// database — there is no staging server.
/// </summary>
[Route("api/admin/test-data")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminTestDataController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminTestDataController> _logger;

    public AdminTestDataController(
        AppDbContext context,
        IWebHostEnvironment env,
        ILogger<AdminTestDataController> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    /// <summary>Dry run by default; pass apply=true to delete.</summary>
    [HttpPost("purge")]
    public async Task<ActionResult<TestDataPurgeService.PurgeReport>> Purge([FromQuery] bool apply = false)
    {
        var report = await TestDataPurgeService.RunAsync(_context, apply);

        if (report.Applied)
        {
            foreach (var key in report.StorageKeys)
            {
                try
                {
                    JobItemDocumentStorage.DeleteFile(_env, key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete uploaded test file {StorageKey}.", key);
                }
            }

            _logger.LogWarning(
                "Purged test data for {Companies} — {Jobs} jobs, {Moi} MOI, {Moa} MOA, {Invoices} invoices.",
                string.Join(", ", report.Companies), report.JobRequests, report.MoiForms,
                report.MoaForms, report.Invoices);
        }

        return Ok(report);
    }
}
