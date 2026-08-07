using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>
/// Item 7 — an MOI with nobody to route to must park for an Admin, not skip client approval.
/// </summary>
public class MoiApproverFailClosedTests
{
    private static Customer SeedCustomerWithoutApprovers(TestDbFactory db)
    {
        var customer = new Customer
        {
            Name = "Alice",
            Email = "alice@test.local",
            Company = "Unrouted Co",
            Status = "Active",
            Cosec = true,
            MoiJson = JsonHelper.Serialize(new[] { "Alice" }),
            MoiApprovalJson = "[]",
            MoaJson = "[]",
            AccountHolders =
            [
                new AccountHolder { Name = "Alice", Email = "alice@test.local", NeedsMoi = true },
            ],
        };
        db.Context.Customers.Add(customer);
        db.Context.SaveChanges();
        return customer;
    }

    [Fact]
    public async Task NoMatrixRow_AndNoCompanyApprover_ParksTheForm()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomerWithoutApprovers(db);
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job);

        var outcome = await JobHandoffService.OnMoiSubmittedForApprovalAsync(
            db.Context, job, form, customer);

        Assert.Equal(MoiSubmitOutcome.NeedsApprover, outcome);
        Assert.NotNull(form.ApproverAssignmentRequestedAt);
        // Critically, it must not have slid past client approval into intake.
        Assert.Equal(MoiWorkflowStates.Draft, form.WorkflowState);
        Assert.NotEqual(JobHandoffStatuses.ClientSubmitted, job.InternalHandoffStatus);
    }

    [Fact]
    public async Task CompanyApprover_StillRoutesNormally()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job);

        var outcome = await JobHandoffService.OnMoiSubmittedForApprovalAsync(
            db.Context, job, form, customer);

        Assert.Equal(MoiSubmitOutcome.Routed, outcome);
        Assert.Null(form.ApproverAssignmentRequestedAt);
        Assert.Equal(MoiWorkflowStates.PendingClientMoiApproval, form.WorkflowState);
    }

    [Fact]
    public async Task MatrixRow_RoutesEvenWithoutCompanyApprovers()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomerWithoutApprovers(db);
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job);

        var submitter = new User
        {
            Email = "khtai@lgb.com.my",
            Name = "Tai Kok Hong",
            Role = UserRoles.ClientAdmin,
            CustomerId = customer.CustomerId,
        };
        db.Context.Users.Add(submitter);
        db.Context.MoiApprovalMatrixEntries.Add(new MoiApprovalMatrixEntry
        {
            GroupCode = "LGB",
            RequesterName = "Tai Kok Hong",
            RequesterEmail = "khtai@lgb.com.my",
            ApproverName = "Datin Irene",
            ApproverEmail = "irene@lgb.com.my",
        });
        db.Context.SaveChanges();

        var outcome = await JobHandoffService.OnMoiSubmittedForApprovalAsync(
            db.Context, job, form, customer, submitter: submitter);

        Assert.Equal(MoiSubmitOutcome.Routed, outcome);
        Assert.Equal("Datin Irene", form.RequiredApproverName);
        Assert.Null(form.ApproverAssignmentRequestedAt);
    }

    [Fact]
    public async Task ParkedForm_ClearsOnceAnApproverIsBound()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomerWithoutApprovers(db);
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job);

        await JobHandoffService.OnMoiSubmittedForApprovalAsync(db.Context, job, form, customer);
        Assert.NotNull(form.ApproverAssignmentRequestedAt);

        // Mirrors what the Admin assign endpoint does.
        form.RequiredApproverName = "Datin Irene";
        form.RequiredApproverEmail = "irene@lgb.com.my";
        form.ApproverAssignmentRequestedAt = null;
        form.WorkflowState = MoiWorkflowStates.PendingClientMoiApproval;
        await db.Context.SaveChangesAsync();

        var stillParked = db.Context.MOIForms.Count(f => f.ApproverAssignmentRequestedAt != null);
        Assert.Equal(0, stillParked);
    }
}
