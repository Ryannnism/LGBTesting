using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

/// <summary>Flowchart MS6 / C3 — cosec inserts approvers into a chain that is already running.</summary>
public class CosecInsertStepTests
{
    private static async Task<WorkflowInstance> StartChainAsync(TestDbFactory db)
    {
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));
        return await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);
    }

    private static List<(int Order, string Key)> Chain(WorkflowInstance instance) =>
        instance.Steps.OrderBy(s => s.StepOrder).Select(s => (s.StepOrder, s.StepKey)).ToList();

    [Fact]
    public async Task Insert_LandsAfterActiveStep_AndRenumbersTheRest()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);
        var before = Chain(instance);

        var inserted = await WorkflowService.InsertCosecStepAsync(
            db.Context, instance, ["Datin Raj", "Seet Mei"]);

        Assert.Equal(2, inserted.StepOrder);
        Assert.Equal("Pending", inserted.Status);
        Assert.Equal("Datin Raj, Seet Mei", inserted.AssigneeName);

        var after = Chain(instance);
        Assert.Equal(before.Count + 1, after.Count);
        Assert.Equal(Enumerable.Range(1, after.Count), after.Select(s => s.Order));
        // Everything that followed the active step keeps its relative order, one place later.
        Assert.Equal(
            before.Skip(1).Select(s => s.Key),
            after.Skip(2).Select(s => s.Key));
        Assert.Equal(1, instance.CurrentStepOrder);
    }

    [Fact]
    public async Task AdvanceReachesTheInsertedStep()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);
        await WorkflowService.InsertCosecStepAsync(db.Context, instance, ["Datin Raj"]);

        var first = instance.Steps.Single(s => s.StepOrder == 1);
        await WorkflowService.AdvanceWorkflowAsync(db.Context, instance, first);

        var active = instance.Steps.Single(s => s.Status == "Active");
        Assert.Equal(WorkflowService.CosecAddedStepKey, active.StepKey);
        Assert.Equal(2, instance.CurrentStepOrder);

        // And the chain carries on past it.
        await WorkflowService.AdvanceWorkflowAsync(db.Context, instance, active);
        var next = instance.Steps.Single(s => s.Status == "Active");
        Assert.Equal(3, next.StepOrder);
        Assert.NotEqual(WorkflowService.CosecAddedStepKey, next.StepKey);
    }

    [Fact]
    public async Task Insert_AtAnExplicitPosition_ShiftsOnlyLaterSteps()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);
        var lastOrder = instance.Steps.Max(s => s.StepOrder);

        var inserted = await WorkflowService.InsertCosecStepAsync(
            db.Context, instance, ["Dee Nee"], afterStepOrder: lastOrder - 1);

        Assert.Equal(lastOrder, inserted.StepOrder);
        Assert.Equal(lastOrder + 1, instance.Steps.Max(s => s.StepOrder));
        Assert.Equal(
            Enumerable.Range(1, instance.Steps.Count),
            Chain(instance).Select(s => s.Order));
    }

    [Fact]
    public async Task Insert_BeforeAnApprovedStep_IsRefused()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);

        var first = instance.Steps.Single(s => s.StepOrder == 1);
        await WorkflowService.AdvanceWorkflowAsync(db.Context, instance, first);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WorkflowService.InsertCosecStepAsync(db.Context, instance, ["Too Late"], afterStepOrder: 0));
    }

    [Fact]
    public async Task Insert_RequiresNames_AndAnActiveWorkflow()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WorkflowService.InsertCosecStepAsync(db.Context, instance, ["   ", ""]));

        instance.Status = "Completed";
        await db.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WorkflowService.InsertCosecStepAsync(db.Context, instance, ["Datin Raj"]));
    }

    [Fact]
    public async Task InsertedStep_SurvivesReload_WithUniqueOrders()
    {
        using var db = new TestDbFactory();
        var instance = await StartChainAsync(db);
        await WorkflowService.InsertCosecStepAsync(db.Context, instance, ["Datin Raj"]);

        var reloaded = await db.Context.WorkflowInstances
            .Include(i => i.Steps)
            .AsNoTracking()
            .FirstAsync(i => i.WorkflowInstanceId == instance.WorkflowInstanceId);

        var orders = reloaded.Steps.Select(s => s.StepOrder).ToList();
        Assert.Equal(orders.Count, orders.Distinct().Count());
        Assert.Contains(reloaded.Steps, s => s.StepKey == WorkflowService.CosecAddedStepKey);
    }
}
