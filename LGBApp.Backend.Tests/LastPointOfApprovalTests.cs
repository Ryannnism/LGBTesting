using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>Flowchart T3 — last point of approval is captured with LOA and feeds MS7.</summary>
public class LastPointOfApprovalTests
{
    private static Dictionary<string, object?> FormData(bool withLoa, params (string Name, string Position)[] people) =>
        new()
        {
            ["withLOA"] = withLoa,
            ["approvalPersons"] = people
                .Select(p => new Dictionary<string, string> { ["name"] = p.Name, ["position"] = p.Position })
                .ToList(),
        };

    [Fact]
    public void Sync_CapturesEntries_WhenLoaApplies()
    {
        var form = new MOIForm();
        LastPointOfApprovalService.Sync(form, FormData(true, ("Datin Irene", "Director")));

        var entries = LastPointOfApprovalService.Read(form);
        Assert.Single(entries);
        Assert.Equal("Datin Irene", entries[0].Name);
        Assert.Equal("Director", entries[0].Position);
    }

    [Fact]
    public void Sync_ClearsEntries_WhenLoaTurnedOff()
    {
        var form = new MOIForm();
        LastPointOfApprovalService.Sync(form, FormData(true, ("Datin Irene", "Director")));
        LastPointOfApprovalService.Sync(form, FormData(false, ("Datin Irene", "Director")));

        Assert.Empty(LastPointOfApprovalService.Read(form));
        Assert.Null(LastPointOfApprovalService.ResolveFinalApproverName(form));
    }

    [Fact]
    public void Read_IgnoresMalformedJson_AndBlankRows()
    {
        Assert.Empty(LastPointOfApprovalService.Read(new MOIForm { LastPointOfApprovalJson = "not json" }));
        Assert.Empty(LastPointOfApprovalService.Read(new MOIForm { LastPointOfApprovalJson = "{}" }));
        Assert.Empty(LastPointOfApprovalService.Read(new MOIForm
        {
            LastPointOfApprovalJson = """[{"name":"","position":""}]""",
        }));
    }

    [Fact]
    public async Task Ms7_PrefersMoiLastPoint_OverSeededFinalApprover()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);

        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        LastPointOfApprovalService.Sync(moi, FormData(true, ("Datin Irene", "Director"), ("Sean Lim", "CFO")));
        db.Context.SaveChanges();

        var moa = db.SeedMoa(job, moi);
        var instance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var ms7 = instance.Steps.Single(s => s.StepKey == "FinalApprover");
        Assert.Equal("Datin Irene, Sean Lim", ms7.AssigneeName);
    }

    [Fact]
    public async Task Ms7_FallsBackToDatoLim_WhenNoLastPointCaptured()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moa = db.SeedMoa(job, db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved));

        var instance = await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var ms7 = instance.Steps.Single(s => s.StepKey == "FinalApprover");
        Assert.Equal("Dato' Lim", ms7.AssigneeName);
    }
}
