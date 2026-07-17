using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class MoiApprovalMatrixTests
{
    [Fact]
    public async Task BindMatrix_SetsRequiredApprover_FromSubmitterEmail()
    {
        using var db = new TestDbFactory();
        db.Context.MoiApprovalMatrixEntries.Add(new MoiApprovalMatrixEntry
        {
            GroupCode = "LGB",
            RequesterName = "Lenny",
            RequesterEmail = "lenny@test.local",
            ApproverName = "Datin Irene",
            ApproverEmail = "irene@test.local",
        });
        await db.Context.SaveChangesAsync();

        var form = new MOIForm { FormDataJson = "{}", Company = "Test Co" };
        var submitter = new User
        {
            Email = "lenny@test.local",
            Name = "Lenny",
            PasswordHash = "x",
            Role = UserRoles.ClientSignatory,
        };

        var ok = await ClientApprovalService.TryBindMatrixApproverAsync(db.Context, form, submitter, "Lenny");
        Assert.True(ok);
        Assert.Equal("Datin Irene", form.RequiredApproverName);
        Assert.Equal("irene@test.local", form.RequiredApproverEmail);
    }

    [Fact]
    public void PhaseComplete_RequiresMatrixApproverOnly()
    {
        var customer = new Customer
        {
            Company = "Test",
            AccountHolders =
            [
                new AccountHolder { Name = "A", NeedsMoiApproval = true },
                new AccountHolder { Name = "B", NeedsMoiApproval = true },
            ],
        };
        var form = new MOIForm
        {
            RequiredApproverName = "Datin Irene",
            RequiredApproverEmail = "irene@test.local",
        };
        var records = new List<ClientApprovalRecord>
        {
            new() { AccountHolderName = "Datin Irene", UserId = 1, SignedAt = DateTime.UtcNow },
        };

        Assert.True(ClientApprovalService.MoiClientPhaseComplete(customer, form, records));
        Assert.False(ClientApprovalService.MoiClientPhaseComplete(
            customer,
            form,
            [new ClientApprovalRecord { AccountHolderName = "A", UserId = 2, SignedAt = DateTime.UtcNow }]));
    }

    [Fact]
    public void FindHolder_RejectsNonMatrixApprover()
    {
        var customer = new Customer { Company = "Test", AccountHolders = [] };
        var form = new MOIForm
        {
            RequiredApproverName = "Datin Irene",
            RequiredApproverEmail = "irene@test.local",
        };
        var wrong = new User { Email = "other@test.local", Name = "Other", PasswordHash = "x", Role = "ClientSignatory" };
        var right = new User { Email = "irene@test.local", Name = "Datin Irene", PasswordHash = "x", Role = "ClientSignatory" };

        Assert.Null(ClientApprovalService.FindMoiApprovalHolderForUser(customer, wrong, form));
        Assert.NotNull(ClientApprovalService.FindMoiApprovalHolderForUser(customer, right, form));
    }
}
