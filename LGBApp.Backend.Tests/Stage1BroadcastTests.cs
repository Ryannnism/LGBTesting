using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using LGBApp.Backend.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGBApp.Backend.Tests;

/// <summary>Flowchart M1 — stage-1 broadcast to legal + secretarial, for listed groups only.</summary>
public class Stage1BroadcastTests
{
    private sealed class RecordingEmail : IEmailSender
    {
        public List<string> Sent { get; } = [];

        public Task SendAsync(
            string to,
            string subject,
            string textBody,
            string? htmlBody = null,
            CancellationToken cancellationToken = default)
        {
            Sent.Add(to);
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

    private static void SeedInternalTeam(AppDbContext context)
    {
        context.Users.AddRange(
            new User { Email = "sharon@lgb.com.my", Name = "Sharon", Role = UserRoles.Admin, JobTitle = "Senior Manager, Company Secretarial" },
            new User { Email = "raj@taliworks.com.my", Name = "Datin Raj", Role = UserRoles.User, JobTitle = "Group Legal", CanApproveMoa = true },
            new User { Email = "nita@taliworks.com.my", Name = "Nita", Role = UserRoles.User, JobTitle = "Resolution preparation" });
        context.SaveChanges();
    }

    private static async Task<(MOAForm Form, WorkflowStepInstance Step, Customer Customer)> StartChainAsync(
        TestDbFactory db, string groupCode)
    {
        var customer = db.SeedCustomer();
        customer.DivisionGroupCode = groupCode;
        db.Context.SaveChanges();

        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);
        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");
        return (moa, step, customer);
    }

    [Theory]
    [InlineData("LGB")]
    [InlineData("BELLWORTH")]
    [InlineData("SWM")]
    public async Task ListedGroups_BroadcastToWholeInternalTeam(string groupCode)
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedInternalTeam(db.Context);

        var email = new RecordingEmail();
        var (form, step, customer) = await StartChainAsync(db, groupCode);
        await BuildNotifier(db, email).NotifyStage1BroadcastAsync(form, step, customer);

        Assert.Equal(
            ["nita@taliworks.com.my", "raj@taliworks.com.my", "sharon@lgb.com.my"],
            email.Sent.OrderBy(e => e).ToList());

        var notifications = await db.Context.AppNotifications
            .Where(n => n.EventType == "moa_stage1_broadcast")
            .ToListAsync();
        Assert.Equal(3, notifications.Count);
    }

    [Fact]
    public async Task UnlistedGroup_DoesNotBroadcast()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedInternalTeam(db.Context);

        var email = new RecordingEmail();
        var (form, step, customer) = await StartChainAsync(db, "TALIWORKS");
        await BuildNotifier(db, email).NotifyStage1BroadcastAsync(form, step, customer);

        Assert.Empty(email.Sent);
        Assert.Empty(db.Context.AppNotifications.Where(n => n.EventType == "moa_stage1_broadcast"));
    }

    [Fact]
    public async Task ClientUsers_AreNeverIncludedInTheBroadcast()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        SeedInternalTeam(db.Context);

        var email = new RecordingEmail();
        var (form, step, customer) = await StartChainAsync(db, "LGB");

        db.Context.Users.AddRange(
            new User { Email = "client.admin@acme.test", Name = "Client Admin", Role = UserRoles.ClientAdmin, CustomerId = customer.CustomerId },
            // A client-scoped account left on the internal "User" role must still be excluded.
            new User { Email = "stale.client@acme.test", Name = "Stale Client", Role = UserRoles.User, CustomerId = customer.CustomerId });
        db.Context.SaveChanges();

        await BuildNotifier(db, email).NotifyStage1BroadcastAsync(form, step, customer);

        Assert.DoesNotContain("client.admin@acme.test", email.Sent);
        Assert.DoesNotContain("stale.client@acme.test", email.Sent);
        Assert.Equal(3, email.Sent.Count);
    }
}
