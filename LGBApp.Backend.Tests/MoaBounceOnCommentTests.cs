using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using LGBApp.Backend.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGBApp.Backend.Tests;

/// <summary>Flowchart M5 — an approver's comment bounces the ticket back to all cosec.</summary>
public class MoaBounceOnCommentTests
{
    private sealed class RecordingEmail : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = [];

        public Task SendAsync(
            string to,
            string subject,
            string textBody,
            string? htmlBody = null,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }

    private static WorkflowNotifier BuildNotifier(TestDbFactory db, IEmailSender email) =>
        new(db.Context,
            email,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:PublicFrontendUrl"] = "https://app.test",
                })
                .Build(),
            NullLogger<WorkflowNotifier>.Instance);

    private static void SeedCosec(AppDbContext context)
    {
        context.Users.AddRange(
            new User { Email = "sharon@lgb.com.my", Name = "Sharon", Role = UserRoles.Admin },
            new User { Email = "nita@taliworks.com.my", Name = "Nita", Role = UserRoles.User });
        context.SaveChanges();
    }

    private static async Task<(MOAForm Form, WorkflowInstance Instance)> StartChainAsync(TestDbFactory db)
    {
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        var instance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);
        return (moa, instance);
    }

    [Fact]
    public async Task Bounce_NotifiesEveryCosecMember()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedCosec(db.Context);

        var email = new RecordingEmail();
        var (form, instance) = await StartChainAsync(db);
        var step = instance.Steps.First(s => s.Status == "Active");

        await BuildNotifier(db, email).NotifyMoaBounceAsync(
            null, form, step, "Clause 4 is wrong", MoaBounceKind.ApprovalComment);

        Assert.Equal(2, email.Sent.Count);
        Assert.All(email.Sent, s => Assert.Contains("returned with comments", s.Subject));

        var notifications = await db.Context.AppNotifications
            .Where(n => n.EventType == "moa_bounce")
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
    }

    [Fact]
    public async Task Rejection_UsesRejectedWording()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedCosec(db.Context);

        var email = new RecordingEmail();
        var (form, instance) = await StartChainAsync(db);
        var step = instance.Steps.First(s => s.Status == "Active");

        await BuildNotifier(db, email).NotifyMoaBounceAsync(
            null, form, step, "Not approved", MoaBounceKind.Rejected);

        Assert.All(email.Sent, s => Assert.Contains("rejected", s.Subject));
    }

    [Fact]
    public async Task PlainApproval_StillAdvancesTheChain()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var (_, instance) = await StartChainAsync(db);

        var first = instance.Steps.OrderBy(s => s.StepOrder).First();
        await WorkflowService.AdvanceWorkflowAsync(db.Context, instance, first);

        Assert.Equal("Approved", first.Status);
        var active = instance.Steps.Single(s => s.Status == "Active");
        Assert.Equal(2, active.StepOrder);
        Assert.Equal(2, instance.CurrentStepOrder);
    }

    [Fact]
    public async Task BouncedStep_StaysActive_SoTheChainHolds()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedCosec(db.Context);

        var email = new RecordingEmail();
        var (form, instance) = await StartChainAsync(db);
        var step = instance.Steps.First(s => s.Status == "Active");
        var orderBefore = instance.CurrentStepOrder;

        step.Comments = "Returned by Sharon: fix the recitals";
        await db.Context.SaveChangesAsync();
        await BuildNotifier(db, email).NotifyMoaBounceAsync(
            null, form, step, "fix the recitals", MoaBounceKind.ApprovalComment);

        Assert.Equal("Active", step.Status);
        Assert.Equal(orderBefore, instance.CurrentStepOrder);
        Assert.Equal("Active", instance.Status);
        Assert.Null(step.ApprovedByUserId);
    }
}
