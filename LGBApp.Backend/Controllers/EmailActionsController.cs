using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

/// <summary>
/// No-login MOA step actions via single-use email tokens (SR7 W4).
/// Never issues a JWT/session.
/// </summary>
[Route("api/email-actions")]
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class EmailActionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApprovalActionTokenService _tokens;
    private readonly WorkflowNotifier _notifier;

    public EmailActionsController(
        AppDbContext context,
        ApprovalActionTokenService tokens,
        WorkflowNotifier notifier)
    {
        _context = context;
        _tokens = tokens;
        _notifier = notifier;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Show(string token, [FromQuery] string? intent)
    {
        var row = await _tokens.FindValidAsync(token);
        if (row == null)
            return Content(HtmlPage("Link expired", "This approval link is invalid, expired, or already used."), "text/html");

        var step = row.WorkflowStepInstance;
        if (step == null || step.Status != "Active" || step.WorkflowInstance?.Status != "Active")
            return Content(HtmlPage("No longer active", "This MOA step is no longer awaiting approval."), "text/html");

        var form = await _context.MOAForms.FindAsync(row.MoaFormId);
        var company = form?.Company ?? "";
        var reject = string.Equals(intent, "reject", StringComparison.OrdinalIgnoreCase);

        if (reject)
        {
            return Content(RejectFormHtml(token, company, step.DisplayName, step.AssigneeName), "text/html");
        }

        return Content(ApproveFormHtml(token, company, step.DisplayName, step.AssigneeName), "text/html");
    }

    public class EmailActionRequest
    {
        public string? Comments { get; set; }
        public string? Reason { get; set; }
    }

    [HttpPost("{token}/approve")]
    public async Task<IActionResult> Approve(string token, [FromForm] EmailActionRequest request)
    {
        var row = await _tokens.FindValidAsync(token);
        if (row == null)
            return Content(HtmlPage("Link expired", "This approval link is invalid, expired, or already used."), "text/html");

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == row.MoaFormId && i.Status == "Active");
        if (instance == null)
            return Content(HtmlPage("No longer active", "No active MOA workflow found."), "text/html");

        var step = await WorkflowService.GetCurrentStepAsync(_context, instance);
        if (step == null || step.WorkflowStepInstanceId != row.WorkflowStepInstanceId)
            return Content(HtmlPage("Step changed", "This step is no longer the current approval step."), "text/html");

        await _tokens.ConsumeAsync(row);

        step.ApprovedByUserId = row.AssigneeUserId;
        step.Comments = request.Comments ?? $"Approved via email by {row.AssigneeName}".Trim();
        await WorkflowService.AdvanceWorkflowAsync(_context, instance, step);

        var form = await _context.MOAForms.FindAsync(row.MoaFormId);
        if (instance.Status == "Completed" && form != null)
        {
            form.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await JobHandoffService.OnMoaWorkflowCompletedAsync(_context, row.MoaFormId);
        }
        else if (form != null)
        {
            var next = instance.Steps.FirstOrDefault(s => s.Status == "Active");
            if (next != null)
            {
                var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
                await _notifier.NotifyMoaStepActivatedAsync(form, next, customer);
            }
        }

        return Content(HtmlPage("Approved", $"Thank you. “{step.DisplayName}” has been recorded as approved. You can close this window."), "text/html");
    }

    [HttpPost("{token}/reject")]
    public async Task<IActionResult> Reject(string token, [FromForm] EmailActionRequest request)
    {
        var row = await _tokens.FindValidAsync(token);
        if (row == null)
            return Content(HtmlPage("Link expired", "This approval link is invalid, expired, or already used."), "text/html");

        var reason = (request.Reason ?? request.Comments ?? "").Trim();
        if (reason.Length < 3)
            return Content(HtmlPage("Reason required", "Please go back and enter a short reason for rejecting."), "text/html");

        var step = await _context.WorkflowStepInstances.FindAsync(row.WorkflowStepInstanceId);
        if (step == null || step.Status != "Active")
            return Content(HtmlPage("No longer active", "This MOA step is no longer awaiting approval."), "text/html");

        await _tokens.ConsumeAsync(row);

        step.Comments = $"Rejected via email by {row.AssigneeName}: {reason}";
        await _context.SaveChangesAsync();

        var form = await _context.MOAForms.FindAsync(row.MoaFormId);
        if (form?.JobRequestId is int jobId)
        {
            var job = await _context.JobRequests.FindAsync(jobId);
            if (job != null)
                await _notifier.NotifyMoaEmailRejectionAsync(job, form, step, reason);
        }

        return Content(HtmlPage("Rejection recorded", "Cosec has been notified. The approval chain was not advanced. You can close this window."), "text/html");
    }

    private const string PageCss =
        "body{font-family:system-ui,sans-serif;max-width:32rem;margin:2rem auto;padding:0 1rem;line-height:1.5}" +
        "button{border:0;padding:.6rem 1.2rem;border-radius:6px;font-size:1rem;cursor:pointer}" +
        "textarea{width:100%;min-height:4rem;margin:.5rem 0 1rem}";

    private static string ApproveFormHtml(string token, string company, string stepName, string assignee)
    {
        var t = System.Net.WebUtility.HtmlEncode(token);
        var c = System.Net.WebUtility.HtmlEncode(company);
        var s = System.Net.WebUtility.HtmlEncode(stepName);
        var a = System.Net.WebUtility.HtmlEncode(assignee);
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>"
            + "<title>Approve MOA step</title><style>" + PageCss
            + "button{background:#0f766e;color:#fff}</style></head><body>"
            + "<h1>Approve MOA step</h1>"
            + $"<p><strong>{c}</strong><br/>Step: {s}<br/>Assignee: {a}</p>"
            + $"<form method=\"post\" action=\"/api/email-actions/{t}/approve\">"
            + "<label>Optional comments</label>"
            + "<textarea name=\"Comments\" placeholder=\"Comments (optional)\"></textarea>"
            + "<button type=\"submit\">Approve</button></form>"
            + $"<p style=\"margin-top:1.5rem;font-size:.9rem\"><a href=\"/api/email-actions/{t}?intent=reject\">Reject instead</a></p>"
            + "</body></html>";
    }

    private static string RejectFormHtml(string token, string company, string stepName, string assignee)
    {
        var t = System.Net.WebUtility.HtmlEncode(token);
        var c = System.Net.WebUtility.HtmlEncode(company);
        var s = System.Net.WebUtility.HtmlEncode(stepName);
        var a = System.Net.WebUtility.HtmlEncode(assignee);
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>"
            + "<title>Reject MOA step</title><style>" + PageCss
            + "button{background:#b91c1c;color:#fff}</style></head><body>"
            + "<h1>Reject MOA step</h1>"
            + $"<p><strong>{c}</strong><br/>Step: {s}<br/>Assignee: {a}</p>"
            + "<p>Rejecting does <strong>not</strong> advance the chain — Cosec is notified to follow up.</p>"
            + $"<form method=\"post\" action=\"/api/email-actions/{t}/reject\">"
            + "<label>Reason (required)</label>"
            + "<textarea name=\"Reason\" required placeholder=\"Why are you rejecting?\"></textarea>"
            + "<button type=\"submit\">Submit rejection</button></form>"
            + "</body></html>";
    }

    private static string HtmlPage(string title, string body)
    {
        var t = System.Net.WebUtility.HtmlEncode(title);
        var b = System.Net.WebUtility.HtmlEncode(body);
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>"
            + $"<title>{t}</title><style>body{{font-family:system-ui,sans-serif;max-width:32rem;margin:2rem auto;padding:0 1rem;line-height:1.5}}</style></head>"
            + $"<body><h1>{t}</h1><p>{b}</p></body></html>";
    }
}
