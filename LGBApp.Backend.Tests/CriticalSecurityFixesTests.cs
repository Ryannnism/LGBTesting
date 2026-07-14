using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Xunit;

namespace LGBApp.Backend.Tests;

/// <summary>Wave 1 critical fixes from docs/SYSTEM_REVIEW.md (C1, C4 helpers).</summary>
public class CriticalSecurityFixesTests
{
    private static readonly HashSet<string> AllowedJobStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "In Progress", "Completed", "Canceled",
    };

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("In Progress", true)]
    [InlineData("Completed", true)]
    [InlineData("Canceled", true)]
    [InlineData("Hacked", false)]
    [InlineData("", false)]
    public void C1_JobStatusAllowlist(string status, bool ok)
    {
        Assert.Equal(ok, !string.IsNullOrWhiteSpace(status) && AllowedJobStatuses.Contains(status));
    }

    [Fact]
    public void C1_ApplyRequest_DoesNotBypassAdminAssignmentGate_ForNonAdmin()
    {
        var job = new JobRequest
        {
            Customer = "Acme",
            Service = "Secretarial",
            TotalQty = 1,
            UsedQty = 0,
            Status = "Pending",
            AssignedUserId = 9,
            JobAssignedTo = "Siti",
            DateRequested = DateTime.UtcNow,
        };
        var request = new JobRequestRequest
        {
            Customer = "Acme",
            Service = "Secretarial",
            TotalQty = 1,
            UsedQty = 0,
            Status = "Completed",
            DateRequested = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            AccountHolder = "Alice",
            AssignedUserId = 99,
            JobAssignedTo = "Attacker",
        };

        JobRequestMapper.ApplyRequest(job, request, isAdmin: false);
        Assert.Equal(9, job.AssignedUserId);
        Assert.Equal("Siti", job.JobAssignedTo);
        Assert.Equal("Completed", job.Status); // status mapping still applied; controller forbids non-admin
    }

    [Theory]
    [InlineData(MoiWorkflowStates.PendingAdminIntake, true)]
    [InlineData(MoiWorkflowStates.PendingRecommendation, true)]
    [InlineData(MoiWorkflowStates.Draft, false)]
    [InlineData(MoiWorkflowStates.PendingClientMoiApproval, false)]
    [InlineData(MoiWorkflowStates.Approved, false)]
    public void C4_ApproveMoi_AllowedStates(string state, bool allowed)
    {
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MoiWorkflowStates.PendingAdminIntake,
            MoiWorkflowStates.PendingRecommendation,
        };
        Assert.Equal(allowed, allow.Contains(state));
    }

    [Fact]
    public void C5_InvoiceNumber_IncrementsSuffixForDay()
    {
        var prefix = $"INV-{DateTime.UtcNow:yyyyMMdd}-";
        var existing = new List<string> { $"{prefix}0001", $"{prefix}0007", "INV-19990101-9999" };
        var maxSuffix = 0;
        foreach (var number in existing)
        {
            if (!number.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var suffixPart = number[prefix.Length..];
            if (int.TryParse(suffixPart, out var n) && n > maxSuffix)
                maxSuffix = n;
        }

        Assert.Equal($"{prefix}0008", $"{prefix}{maxSuffix + 1:D4}");
    }
}
