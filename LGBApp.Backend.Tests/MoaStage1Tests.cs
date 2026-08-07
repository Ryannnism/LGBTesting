using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGBApp.Backend.Tests;

/// <summary>MS1 reaches real people: job-title steps resolve to internal staff, not a name lookup.</summary>
public class MoaStage1Tests
{
    private sealed class FakeClock : IAppClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private static ApprovalActionTokenService BuildTokens(AppDbContext context) =>
        new(context,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:PublicApiUrl"] = "https://api.test",
                })
                .Build(),
            new FakeClock(),
            NullLogger<ApprovalActionTokenService>.Instance);

    private static void SeedCosecHeads(AppDbContext context)
    {
        context.Users.AddRange(
            new User
            {
                Email = "sharon@lgb.com.my",
                Name = "Sharon",
                Role = UserRoles.Admin,
                JobTitle = "Senior Manager, Company Secretarial",
            },
            new User
            {
                Email = "pohli.ng@taliworks.com.my",
                Name = "Ng Poh Li",
                Role = UserRoles.Admin,
                JobTitle = "Senior Manager, Company Secretarial",
            },
            new User
            {
                Email = "nita@taliworks.com.my",
                Name = "Nita",
                Role = UserRoles.User,
                JobTitle = "Resolution preparation",
            });
        context.SaveChanges();
    }

    [Fact]
    public async Task Stage1_JobTitleStep_IssuesLinksToMatchingStaff()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedCosecHeads(db.Context);

        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        var moa = db.SeedMoa(job, moi);
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");
        Assert.Equal("JobTitle", step.AssigneeType);
        Assert.Equal("Senior Manager, Company Secretarial", step.AssigneeName);

        var links = await BuildTokens(db.Context).IssueLinksForActiveStepAsync(step, moa, customer);

        var emails = links.Select(l => l.Email).OrderBy(e => e).ToList();
        Assert.Equal(["pohli.ng@taliworks.com.my", "sharon@lgb.com.my"], emails);
        Assert.All(links, l => Assert.Contains("/api/email-actions/", l.ApproveUrl));
    }

    [Fact]
    public async Task Stage1_CommaInJobTitle_IsNotSplitIntoTwoAssignees()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        db.Context.Users.Add(new User
        {
            Email = "senior.manager@lgb.com.my",
            // A person literally named after half the title must not be picked up by a name split.
            Name = "Senior Manager",
            Role = UserRoles.User,
            JobTitle = "Resolution preparation",
        });
        db.Context.SaveChanges();
        SeedCosecHeads(db.Context);

        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");
        var links = await BuildTokens(db.Context).IssueLinksForActiveStepAsync(step, moa, customer);

        Assert.DoesNotContain(links, l => l.Email == "senior.manager@lgb.com.my");
        Assert.Equal(2, links.Count);
    }

    [Fact]
    public async Task BankSignatory_AddsTehStep_ToTheChain()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);

        var plain = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        var plainInstance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, plain, customer);

        var banking = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        banking.BankSignatoryMatter = true;
        db.Context.SaveChanges();
        var bankingInstance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, banking, customer);

        Assert.DoesNotContain(plainInstance.Steps, s => s.StepKey == "TehSW");
        Assert.Contains(bankingInstance.Steps, s => s.StepKey == "TehSW");
        Assert.Equal(plainInstance.Steps.Count + 1, bankingInstance.Steps.Count);

        // CosecAdded is never seeded at init — it only exists via the runtime insert path (MS6/C3).
        Assert.DoesNotContain(bankingInstance.Steps, s => s.StepKey == "CosecAdded");

        // Dense, contiguous ordering is what AdvanceWorkflowAsync relies on.
        Assert.Equal(
            Enumerable.Range(1, bankingInstance.Steps.Count),
            bankingInstance.Steps.OrderBy(s => s.StepOrder).Select(s => s.StepOrder));
    }
}
