using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

/// <summary>
/// Prints the whole pack for one task (job line): MOI + MOA + checklist + sign-off trail + document
/// index, as a single printable HTML page. Scoped to a task (and optionally one session), unlike the
/// per-form JSON export on the MOI/MOA controllers.
/// </summary>
[Route("api/jobs/{jobId:int}/pack")]
[ApiController]
[Authorize]
public class JobPackController : ControllerBase
{
    private readonly AppDbContext _context;

    public JobPackController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetPack(int jobId, [FromQuery] int? unitNumber)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);
        if (job == null) return NotFound();

        if (!CanAccessJob(job))
            return Forbid();

        var mois = await _context.MOIForms.Where(f => f.JobRequestId == jobId).ToListAsync();
        var moas = await _context.MOAForms.Where(f => f.JobRequestId == jobId).ToListAsync();

        // Which sessions to render.
        var units = job.Units.OrderBy(u => u.UnitNumber).ToList();
        List<JobRequestUnit?> targetUnits;
        if (unitNumber.HasValue)
        {
            var u = units.FirstOrDefault(x => x.UnitNumber == unitNumber.Value);
            if (u == null) return BadRequest(new { message = "Session not found for this task." });
            targetUnits = new() { u };
        }
        else if (job.TotalQty > 1)
        {
            targetUnits = units.Cast<JobRequestUnit?>().ToList();
        }
        else
        {
            targetUnits = new() { units.FirstOrDefault() };
        }

        var sessions = new List<TaskPackExportService.Session>();
        foreach (var unit in targetUnits)
        {
            var moi = ResolveMoi(mois, unit, job.TotalQty);
            var moa = ResolveMoa(moas, unit, job.TotalQty);

            // Only include a form the caller is actually allowed to see.
            if (moi != null && !await FormAccessHelper.CanAccessMoiFormAsync(_context, User, moi)) moi = null;
            if (moa != null && !await FormAccessHelper.CanAccessMoaFormAsync(_context, User, moa)) moa = null;

            // A session only belongs in the pack once it has a visible form. This keeps empty
            // tasks (and forms the caller cannot see yet) out, so the "no forms" state shows.
            if (moi == null && moa == null) continue;

            sessions.Add(new TaskPackExportService.Session(unit?.UnitNumber, unit?.Status, moi, moa));
        }

        var documents = await LoadVisibleDocumentsAsync(jobId, unitNumber, job.TotalQty);
        var customer = job.CustomerId.HasValue
            ? await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == job.CustomerId.Value)
            : await WorkflowService.ResolveCustomerForCompanyAsync(_context, job.Customer);

        var html = TaskPackExportService.BuildHtml(
            job, customer, sessions, documents,
            AuthHelper.CurrentUserName(User) ?? "LGB Services");

        var sessionSuffix = unitNumber.HasValue ? $"-s{unitNumber.Value}" : string.Empty;
        Response.Headers["Content-Disposition"] = $"inline; filename=\"task-{jobId}{sessionSuffix}-pack.html\"";
        return File(html, "text/html; charset=utf-8");
    }

    private bool CanAccessJob(JobRequest job)
    {
        if (AuthHelper.IsAdmin(User))
            return true;
        if (AuthHelper.IsExternalUser(User))
            return AuthHelper.CanAccessCustomer(User, job.CustomerId);
        return AuthHelper.CanAccessJob(User, job);
    }

    private static MOIForm? ResolveMoi(List<MOIForm> mois, JobRequestUnit? unit, int totalQty)
    {
        if (unit != null)
        {
            var byUnit = mois.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
            if (byUnit != null) return byUnit;
        }
        return totalQty <= 1 ? mois.FirstOrDefault(f => f.JobRequestUnitId == null) ?? mois.FirstOrDefault() : null;
    }

    private static MOAForm? ResolveMoa(List<MOAForm> moas, JobRequestUnit? unit, int totalQty)
    {
        if (unit != null)
        {
            var byUnit = moas.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
            if (byUnit != null) return byUnit;
        }
        return totalQty <= 1 ? moas.FirstOrDefault(f => f.JobRequestUnitId == null) ?? moas.FirstOrDefault() : null;
    }

    private async Task<List<JobItemDocument>> LoadVisibleDocumentsAsync(int jobId, int? unitNumber, int totalQty)
    {
        var query = _context.JobItemDocuments.Where(d => d.JobRequestId == jobId);

        if (unitNumber.HasValue && totalQty > 1)
        {
            var unitId = await _context.JobRequestUnits
                .Where(u => u.JobRequestId == jobId && u.UnitNumber == unitNumber.Value)
                .Select(u => (int?)u.JobRequestUnitId)
                .FirstOrDefaultAsync();
            query = query.Where(d => d.JobRequestUnitId == unitId);
        }

        // External users see their own company's docs; internal staff see released ones only.
        if (!AuthHelper.IsExternalUser(User) && !AuthHelper.IsAdmin(User))
            query = query.Where(d => d.VisibleToInternal);

        return await query.ToListAsync();
    }
}
