using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WorkflowInstancesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly WorkflowNotifier _notifier;

    public WorkflowInstancesController(AppDbContext context, WorkflowNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    [HttpGet("moa/{moaFormId}")]
    public async Task<ActionResult<WorkflowInstanceDto>> GetForMoa(int moaFormId)
    {
        var dto = await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId);
        if (dto == null) return NotFound();
        return dto;
    }

    [HttpPost("moa/{moaFormId}/approve-step")]
    public async Task<ActionResult<WorkflowInstanceDto>> ApproveMoaStep(int moaFormId, ApproveWorkflowStepRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound("No active workflow.");

        var step = await WorkflowService.GetCurrentStepAsync(_context, instance);
        if (step == null) return BadRequest(new { message = "No active step." });

        var form = await _context.MOAForms.FindAsync(moaFormId);
        var customer = form != null
            ? await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company)
            : null;
        var isAdmin = AuthHelper.IsAdmin(User);

        if (!await WorkflowService.CanUserApproveStepAsync(_context, user, step, customer, isAdmin))
            return Forbid();

        // M5: a comment on approval is a bounce, not a sign-off — the chain holds here.
        var comment = (request.Comments ?? "").Trim();
        if (comment.Length > 0)
        {
            step.Comments = $"Returned by {user.Name}: {comment}";
            await _context.SaveChangesAsync();
            await NotifyBounceAsync(form, step, comment, MoaBounceKind.ApprovalComment);

            return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
                ?? throw new InvalidOperationException("Workflow missing after bounce.");
        }

        step.ApprovedByUserId = user.UserId;
        step.Comments = $"Approved by {user.Name}";
        await WorkflowService.AdvanceWorkflowAsync(_context, instance, step);

        if (instance.Status == "Completed" && form != null)
        {
            form.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await JobHandoffService.OnMoaWorkflowCompletedAsync(_context, moaFormId);
        }
        else if (form != null)
        {
            var next = instance.Steps.FirstOrDefault(s => s.Status == "Active");
            if (next != null)
                await _notifier.NotifyMoaStepActivatedAsync(form, next, customer);
        }

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after approve.");
    }

    [HttpPost("moa/{moaFormId}/add-approver")]
    public async Task<ActionResult<WorkflowInstanceDto>> AddCosecApprover(int moaFormId, InsertCosecApproverRequest request)
    {
        if (!AuthHelper.IsInternalStaff(User))
            return Forbid();

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound("No active workflow.");

        WorkflowStepInstance inserted;
        try
        {
            inserted = await WorkflowService.InsertCosecStepAsync(
                _context, instance, request.ApproverNames, request.AfterStepOrder);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Only email when the new step is the one now waiting — otherwise it waits its turn.
        if (inserted.Status == "Active")
        {
            var form = await _context.MOAForms.FindAsync(moaFormId);
            if (form != null)
            {
                var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
                await _notifier.NotifyMoaStepActivatedAsync(form, inserted, customer);
            }
        }

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after insert.");
    }

    [HttpPost("moa/{moaFormId}/reject-step")]
    public async Task<ActionResult<WorkflowInstanceDto>> RejectMoaStep(int moaFormId, ApproveWorkflowStepRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var reason = (request.Comments ?? "").Trim();
        if (reason.Length < 3)
            return BadRequest(new { message = "Enter a reason for rejecting this step." });

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound("No active workflow.");

        var step = await WorkflowService.GetCurrentStepAsync(_context, instance);
        if (step == null) return BadRequest(new { message = "No active step." });

        var form = await _context.MOAForms.FindAsync(moaFormId);
        var customer = form != null
            ? await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company)
            : null;

        if (!await WorkflowService.CanUserApproveStepAsync(_context, user, step, customer, AuthHelper.IsAdmin(User)))
            return Forbid();

        step.Comments = $"Rejected by {user.Name}: {reason}";
        await _context.SaveChangesAsync();
        await NotifyBounceAsync(form, step, reason, MoaBounceKind.Rejected);

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after reject.");
    }

    private async Task NotifyBounceAsync(MOAForm? form, WorkflowStepInstance step, string reason, MoaBounceKind kind)
    {
        if (form == null)
            return;

        JobRequest? job = null;
        if (form.JobRequestId is int jobId)
            job = await _context.JobRequests.FindAsync(jobId);

        await _notifier.NotifyMoaBounceAsync(job, form, step, reason, kind);
    }

    [HttpPost("moa/{moaFormId}/admin-override")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<WorkflowInstanceDto>> AdminOverrideMoaStep(int moaFormId, AdminOverrideStepRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound();

        var step = instance.Steps.FirstOrDefault(s => s.WorkflowStepInstanceId == request.StepInstanceId);
        if (step == null) return NotFound("Step not found.");

        step.AdminOverridden = true;
        step.OverriddenByUserId = user.UserId;
        step.Comments = request.Comments;
        step.ApprovedByUserId = user.UserId;
        await WorkflowService.AdvanceWorkflowAsync(_context, instance, step);

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after override.");
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var id)) return null;
        return await _context.Users.FindAsync(id);
    }
}
