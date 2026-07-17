using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGBApp.Backend.Tests;

public class EmailActionTokenTests
{
    private sealed class FakeClock : IAppClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    [Fact]
    public async Task Token_Approve_AdvancesStep_AndConsumes()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        var moa = db.SeedMoa(job, moi);
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");
        var clock = new FakeClock();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicApiUrl"] = "https://api.test",
            })
            .Build();
        var tokens = new ApprovalActionTokenService(
            db.Context, config, clock, NullLogger<ApprovalActionTokenService>.Instance);

        var issued = await tokens.IssueForStepAsync(step, moa.MOAFormId, null, "approver@test.local", "Approver");
        Assert.NotNull(issued);

        var row = await tokens.FindValidAsync(issued.Value.RawToken);
        Assert.NotNull(row);

        // Simulate approve
        await tokens.ConsumeAsync(row!);
        step.Comments = "ok";
        var instance = await db.Context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstAsync(i => i.MoaFormId == moa.MOAFormId);
        await WorkflowService.AdvanceWorkflowAsync(db.Context, instance, step);

        Assert.NotEqual("Active", step.Status);
        Assert.NotNull(await db.Context.WorkflowStepInstances.FirstOrDefaultAsync(s => s.Status == "Active"));
        Assert.Null(await tokens.FindValidAsync(issued.Value.RawToken));
    }

    [Fact]
    public async Task ExpiredOrConsumed_Token_IsInvalid()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        var moa = db.SeedMoa(job, moi);
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);
        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");

        var clock = new FakeClock { UtcNow = DateTime.UtcNow };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicApiUrl"] = "https://api.test",
            })
            .Build();
        var tokens = new ApprovalActionTokenService(
            db.Context, config, clock, NullLogger<ApprovalActionTokenService>.Instance);

        var issued = await tokens.IssueForStepAsync(step, moa.MOAFormId, 1, "a@test.local", "A");
        Assert.NotNull(issued);

        clock.UtcNow = clock.UtcNow.AddHours(80);
        Assert.Null(await tokens.FindValidAsync(issued.Value.RawToken));
    }

    [Fact]
    public void HashToken_IsDeterministic_AndNotPlaintext()
    {
        var a = ApprovalActionTokenService.HashToken("abc");
        var b = ApprovalActionTokenService.HashToken("abc");
        Assert.Equal(a, b);
        Assert.DoesNotContain("abc", a);
    }
}
