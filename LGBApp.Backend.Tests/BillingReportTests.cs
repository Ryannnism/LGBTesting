using System.Globalization;
using System.Text;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>B5 — quarterly billing report boundaries, totals and output formats.</summary>
public class BillingReportTests
{
    private static Customer SeedCustomer(TestDbFactory db, string company)
    {
        var customer = new Customer
        {
            Name = "Alice",
            Email = $"{company.Replace(" ", "").ToLowerInvariant()}@test.local",
            Company = company,
            Status = "Active",
        };
        db.Context.Customers.Add(customer);
        db.Context.SaveChanges();
        return customer;
    }

    private static void SeedInvoice(TestDbFactory db, Customer customer, decimal amount, DateTime issuedAt)
    {
        db.Context.Invoices.Add(new Invoice
        {
            CustomerId = customer.CustomerId,
            InvoiceNumber = $"INV-{issuedAt:yyyyMMdd}-{amount:0000}",
            Amount = amount,
            Currency = "MYR",
            Status = "Issued",
            CreatedAt = issuedAt,
            IssuedAt = issuedAt,
        });
        db.Context.SaveChanges();
    }

    [Fact]
    public void QuarterWindow_IsInclusiveAtStart_ExclusiveAtEnd()
    {
        var (start, end) = BillingReportService.QuarterWindow(2026, 1);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), end);

        var (q4Start, q4End) = BillingReportService.QuarterWindow(2026, 4);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), q4Start);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), q4End);

        Assert.Throws<ArgumentOutOfRangeException>(() => BillingReportService.QuarterWindow(2026, 5));
    }

    [Fact]
    public async Task OnlyInvoicesInsideTheQuarterAreCounted()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomer(db, "Boundary Co");

        SeedInvoice(db, customer, 100m, new DateTime(2026, 3, 31, 23, 59, 0, DateTimeKind.Utc));
        SeedInvoice(db, customer, 200m, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedInvoice(db, customer, 300m, new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));
        SeedInvoice(db, customer, 400m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var q2 = await BillingReportService.BuildAsync(db.Context, 2026, 2);

        Assert.Equal(500m, q2.InvoicedTotal);
        Assert.Equal(2, q2.Invoices.Count);
        Assert.DoesNotContain(q2.Invoices, i => i.Amount is 100m or 400m);
    }

    [Fact]
    public async Task PackageValueAndQuotaAppearPerCustomer()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomer(db, "Package Co");

        db.Context.CustomerPackages.Add(new CustomerPackage
        {
            CustomerId = customer.CustomerId,
            PackageName = "Cosec retainer",
            PackageValue = 12000m,
            Validity = "1 Year",
            PurchasedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Status = "Active",
        });
        db.Context.CompletedServices.AddRange(
            new CompletedService
            {
                Customer = "Package Co",
                Service = "Secretarial record Checks",
                UsedQty = 2,
                TotalQty = 4,
                DateRequested = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
                DateCompleted = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            },
            // Outside the quarter — must not be counted.
            new CompletedService
            {
                Customer = "Package Co",
                Service = "Secretarial record Checks",
                UsedQty = 5,
                TotalQty = 5,
                DateRequested = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                DateCompleted = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            });
        db.Context.SaveChanges();

        var report = await BillingReportService.BuildAsync(db.Context, 2026, 2);

        var row = Assert.Single(report.Customers);
        Assert.Equal("Package Co", row.Company);
        Assert.Equal(12000m, row.ContractValue);
        Assert.Equal(2, row.QuotaUsed);
        Assert.Equal(1, row.ServicesCompleted);
        Assert.True(row.RemainingValue > 0 && row.RemainingValue < row.ContractValue);
    }

    [Fact]
    public async Task CustomersWithNoActivityAreOmitted()
    {
        using var db = new TestDbFactory();
        SeedCustomer(db, "Quiet Co");
        var active = SeedCustomer(db, "Busy Co");
        SeedInvoice(db, active, 750m, new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc));

        var report = await BillingReportService.BuildAsync(db.Context, 2026, 2);

        Assert.Equal(["Busy Co"], report.Customers.Select(c => c.Company));
    }

    [Fact]
    public async Task PdfStartsWithMagicBytes_AndCsvTotalsMatch()
    {
        using var db = new TestDbFactory();
        var a = SeedCustomer(db, "Alpha Co");
        var b = SeedCustomer(db, "Beta Co");
        SeedInvoice(db, a, 1200.50m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedInvoice(db, b, 300.25m, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = await BillingReportService.BuildAsync(db.Context, 2026, 2);

        var pdf = BillingReportPdfService.Build(report);
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        var csv = Encoding.UTF8.GetString(BillingReportService.ToCsv(report));
        var totalLine = csv.Split('\n').Single(l => l.StartsWith("Total,"));
        Assert.Contains(
            report.InvoicedTotal.ToString("0.00", CultureInfo.InvariantCulture),
            totalLine);
        Assert.Equal(1500.75m, report.InvoicedTotal);
        Assert.Contains("Alpha Co", csv);
        Assert.Contains("Beta Co", csv);
    }

    [Fact]
    public async Task CsvEscapesCommasInCompanyNames()
    {
        using var db = new TestDbFactory();
        var customer = SeedCustomer(db, "Lim, Goh & Bros Sdn Bhd");
        SeedInvoice(db, customer, 500m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = await BillingReportService.BuildAsync(db.Context, 2026, 2);
        var csv = Encoding.UTF8.GetString(BillingReportService.ToCsv(report));

        Assert.Contains("\"Lim, Goh & Bros Sdn Bhd\"", csv);
    }
}
