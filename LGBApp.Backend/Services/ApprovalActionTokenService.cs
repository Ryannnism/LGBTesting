using System.Security.Cryptography;
using System.Text;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>Issues and consumes single-use MOA email-action tokens (SR7 W4).</summary>
public class ApprovalActionTokenService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(72);

    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IAppClock _clock;
    private readonly ILogger<ApprovalActionTokenService> _logger;

    public ApprovalActionTokenService(
        AppDbContext context,
        IConfiguration config,
        IAppClock clock,
        ILogger<ApprovalActionTokenService> logger)
    {
        _context = context;
        _config = config;
        _clock = clock;
        _logger = logger;
    }

    public string? PublicApiBaseUrl =>
        (_config["App:PublicApiUrl"] ?? "").Trim().TrimEnd('/');

    /// <summary>
    /// Invalidate prior unused tokens for the step, issue one new raw token for the assignee.
    /// Returns null if PublicApiUrl is unset (cannot build email links).
    /// </summary>
    public async Task<(string RawToken, string ApproveUrl, string RejectUrl)?> IssueForStepAsync(
        WorkflowStepInstance step,
        int moaFormId,
        int? assigneeUserId,
        string assigneeEmail,
        string assigneeName)
    {
        var baseUrl = PublicApiBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("App:PublicApiUrl unset — cannot issue email approval links.");
            return null;
        }

        var prior = await _context.ApprovalActionTokens
            .Where(t => t.WorkflowStepInstanceId == step.WorkflowStepInstanceId && t.ConsumedAt == null)
            .ToListAsync();
        foreach (var row in prior)
            row.ConsumedAt = _clock.UtcNow;

        var raw = GenerateRawToken();
        _context.ApprovalActionTokens.Add(new ApprovalActionToken
        {
            TokenHash = HashToken(raw),
            WorkflowStepInstanceId = step.WorkflowStepInstanceId,
            MoaFormId = moaFormId,
            AssigneeUserId = assigneeUserId,
            AssigneeEmail = (assigneeEmail ?? "").Trim().ToLowerInvariant(),
            AssigneeName = (assigneeName ?? "").Trim(),
            ExpiresAt = _clock.UtcNow.Add(DefaultLifetime),
            CreatedAt = _clock.UtcNow,
        });
        await _context.SaveChangesAsync();

        var approveUrl = $"{baseUrl}/api/email-actions/{raw}";
        var rejectUrl = $"{baseUrl}/api/email-actions/{raw}?intent=reject";
        return (raw, approveUrl, rejectUrl);
    }

    public async Task<ApprovalActionToken?> FindValidAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length < 20)
            return null;

        var hash = HashToken(rawToken.Trim());
        var row = await _context.ApprovalActionTokens
            .Include(t => t.WorkflowStepInstance)!
                .ThenInclude(s => s!.WorkflowInstance)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (row == null)
            return null;
        if (row.ConsumedAt.HasValue)
            return null;
        if (row.ExpiresAt < _clock.UtcNow)
            return null;

        return row;
    }

    public async Task ConsumeAsync(ApprovalActionToken token)
    {
        token.ConsumedAt = _clock.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>After a step becomes Active, issue tokens for each resolvable assignee and return email payloads.</summary>
    public async Task<List<EmailActionLink>> IssueLinksForActiveStepAsync(
        WorkflowStepInstance step,
        MOAForm form,
        Customer? customer)
    {
        var results = new List<EmailActionLink>();
        var recipients = await ResolveRecipientsAsync(step, customer);
        if (recipients.Count == 0)
        {
            // Still issue one generic link for Admin forwarding
            var issued = await IssueForStepAsync(step, form.MOAFormId, null, "", step.AssigneeName);
            if (issued != null)
                results.Add(new EmailActionLink("", step.AssigneeName, issued.Value.ApproveUrl, issued.Value.RejectUrl));
            return results;
        }

        foreach (var r in recipients)
        {
            var issued = await IssueForStepAsync(step, form.MOAFormId, r.UserId, r.Email, r.Name);
            if (issued == null) continue;
            results.Add(new EmailActionLink(r.Email, r.Name, issued.Value.ApproveUrl, issued.Value.RejectUrl));
        }

        return results;
    }

    private async Task<List<(int? UserId, string Email, string Name)>> ResolveRecipientsAsync(
        WorkflowStepInstance step,
        Customer? customer)
    {
        var list = new List<(int? UserId, string Email, string Name)>();
        if (step.AssigneeUserId is int uid)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == uid);
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                list.Add((user.UserId, user.Email, user.Name));
            return list;
        }

        var names = (step.AssigneeName ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (names.Count == 0)
            return list;

        foreach (var name in names)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Name == name);
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                list.Add((user.UserId, user.Email, user.Name));
                continue;
            }

            var holder = customer?.AccountHolders.FirstOrDefault(h =>
                h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(h.Email));
            if (holder != null)
                list.Add((holder.UserId, holder.Email!, holder.Name));
        }

        return list
            .GroupBy(r => r.Email.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashToken(string raw)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public sealed record EmailActionLink(string Email, string Name, string ApproveUrl, string RejectUrl);
}
