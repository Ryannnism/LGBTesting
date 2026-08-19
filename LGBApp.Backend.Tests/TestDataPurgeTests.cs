using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>
/// Acceptance walkthrough data must come off the live database in one pass.
/// </summary>
public class TestDataPurgeTests
{
    private static Customer SeedTestCompany(TestDbFactory db, string company = "ZZ TEST HOLDINGS")
        => db.SeedCustomer(company);

    private static Invoice SeedInvoice(TestDbFactory db, Customer customer, JobRequest? job, string number)
    {
        var invoice = new Invoice
        {
            CustomerId = customer.CustomerId,
            JobRequestId = job?.JobRequestId,
            InvoiceNumber = number,
            Amount = 100m,
            Currency = "MYR",
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        };
        db.Context.Invoices.Add(invoice);
        db.Context.SaveChanges();
        return invoice;
    }

    private static void SeedNotification(TestDbFactory db, Customer customer, JobRequest? job)
    {
        db.Context.AppNotifications.Add(new AppNotification
        {
            UserId = 1,
            EventType = "test",
            Title = "ZZ TEST notice",
            Message = "walkthrough",
            CustomerId = customer.CustomerId,
            JobRequestId = job?.JobRequestId,
            CreatedAt = DateTime.UtcNow,
        });
        db.Context.SaveChanges();
    }

    [Fact]
    public async Task DryRun_CountsButDeletesNothing()
    {
        using var db = new TestDbFactory();
        var customer = SeedTestCompany(db);
        var job = db.SeedServiceJob(customer);
        db.SeedMoi(job);
        db.SeedMoa(job);
        SeedInvoice(db, customer, job, "ZZ-INV-DRY");
        SeedNotification(db, customer, job);

        var report = await TestDataPurgeService.RunAsync(db.Context, apply: false);

        Assert.False(report.Applied);
        Assert.Equal(1, report.Customers);
        Assert.True(report.JobRequests > 0);
        Assert.True(report.MoiForms > 0);
        Assert.True(report.MoaForms > 0);
        Assert.True(report.Invoices > 0);
        Assert.True(report.Notifications > 0);
        Assert.Equal(1, db.Context.Customers.Count());
        Assert.Equal(1, db.Context.Invoices.Count());
    }

    [Fact]
    public async Task Apply_RemovesTheWholeGraphIncludingTheInvoice()
    {
        using var db = new TestDbFactory();
        var customer = SeedTestCompany(db);
        var job = db.SeedServiceJob(customer);
        db.SeedMoi(job);
        db.SeedMoa(job);
        SeedInvoice(db, customer, job, "ZZ-INV-APPLY");
        SeedNotification(db, customer, job);

        var report = await TestDataPurgeService.RunAsync(db.Context, apply: true);

        Assert.True(report.Applied);
        Assert.Empty(db.Context.Customers);
        Assert.Empty(db.Context.JobRequests);
        Assert.Empty(db.Context.MOIForms);
        Assert.Empty(db.Context.MOAForms);
        Assert.Empty(db.Context.Invoices);
        Assert.Empty(db.Context.AppNotifications);
    }

    [Fact]
    public async Task Apply_LeavesRealDataAlone()
    {
        using var db = new TestDbFactory();
        var test = SeedTestCompany(db);
        var real = db.SeedCustomer("Real Co");
        var testJob = db.SeedServiceJob(test);
        var realJob = db.SeedServiceJob(real);
        SeedInvoice(db, test, testJob, "ZZ-INV-TEST");
        SeedInvoice(db, real, realJob, "REAL-INV");

        await TestDataPurgeService.RunAsync(db.Context, apply: true);

        Assert.Single(db.Context.Customers);
        Assert.Equal("Real Co", db.Context.Customers.Single().Company);
        Assert.Single(db.Context.JobRequests);
        Assert.Single(db.Context.Invoices);
        Assert.Equal("REAL-INV", db.Context.Invoices.Single().InvoiceNumber);
    }

    [Fact]
    public async Task RefusesARunThatIsTooWide()
    {
        using var db = new TestDbFactory();
        for (var i = 1; i <= 6; i++)
            db.SeedCustomer($"ZZ TEST {i}");

        await Assert.ThrowsAsync<DomainException>(
            () => TestDataPurgeService.RunAsync(db.Context, apply: false));
    }

    [Fact]
    public async Task ProtectsTheSeededLiveTestLogins()
    {
        using var db = new TestDbFactory();
        var customer = SeedTestCompany(db);
        db.Context.Users.Add(new User
        {
            Email = InternalStaffSeeder.LiveTestClientEmail,
            Name = "ZZ TEST Client",
            Role = UserRoles.ClientAdmin,
            PasswordHash = PasswordHasher.Hash("OldPassword1"),
            CustomerId = customer.CustomerId,
            CreatedAt = DateTime.UtcNow,
        });
        db.Context.SaveChanges();

        await TestDataPurgeService.RunAsync(db.Context, apply: true);

        Assert.True(db.Context.Users.Any(u => u.Email == InternalStaffSeeder.LiveTestClientEmail));
    }
}
