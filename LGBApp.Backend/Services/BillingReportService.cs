using System.Globalization;
using System.Text;
using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// B5 quarterly billing report. Aggregates invoices raised in the quarter, the value of each
/// customer's packages, and the package quota they consumed.
/// </summary>
public static class BillingReportService
{
    public sealed record CustomerRow(
        int CustomerId,
        string Company,
        string Currency,
        decimal InvoicedTotal,
        int InvoiceCount,
        decimal ContractValue,
        decimal RemainingValue,
        int QuotaUsed,
        int ServicesCompleted);

    public sealed record InvoiceRow(
        string InvoiceNumber,
        string Company,
        string Status,
        string Currency,
        decimal Amount,
        DateTime CreatedAt,
        DateTime? IssuedAt);

    public sealed record Report(
        int Year,
        int Quarter,
        DateTime StartUtc,
        DateTime EndUtc,
        List<CustomerRow> Customers,
        List<InvoiceRow> Invoices)
    {
        public decimal InvoicedTotal => Customers.Sum(c => c.InvoicedTotal);
        public decimal ContractValueTotal => Customers.Sum(c => c.ContractValue);
        public decimal RemainingValueTotal => Customers.Sum(c => c.RemainingValue);
        public int QuotaUsedTotal => Customers.Sum(c => c.QuotaUsed);
        public string Label => $"Q{Quarter} {Year}";
    }

    /// <summary>Quarter window in UTC: inclusive at the start, exclusive at the end.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) QuarterWindow(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be 1-4.");

        var start = new DateTime(year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(3));
    }

    public static async Task<Report> BuildAsync(AppDbContext context, int year, int quarter)
    {
        var (start, end) = QuarterWindow(year, quarter);

        var customers = await context.Customers.AsNoTracking()
            .Select(c => new { c.CustomerId, c.Company })
            .ToListAsync();
        var companyById = customers.ToDictionary(c => c.CustomerId, c => c.Company);

        // IssuedAt is only populated from the day the issue action shipped, so fall back to
        // CreatedAt to keep historical quarters comparable.
        var invoices = await context.Invoices.AsNoTracking()
            .Where(i => (i.IssuedAt ?? i.CreatedAt) >= start && (i.IssuedAt ?? i.CreatedAt) < end)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();

        var packages = await context.CustomerPackages.AsNoTracking().ToListAsync();
        var completed = await context.CompletedServices.AsNoTracking()
            .Where(s => s.DateCompleted >= start && s.DateCompleted < end)
            .ToListAsync();

        var rows = new List<CustomerRow>();
        foreach (var customer in customers.OrderBy(c => c.Company, StringComparer.OrdinalIgnoreCase))
        {
            var customerInvoices = invoices.Where(i => i.CustomerId == customer.CustomerId).ToList();
            var customerPackages = packages
                .Where(p => p.CustomerId == customer.CustomerId
                    && p.PurchasedDate < end
                    && p.ExpiryDate >= start)
                .ToList();
            var customerServices = completed
                .Where(s => s.Customer.Equals(customer.Company, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (customerInvoices.Count == 0 && customerPackages.Count == 0 && customerServices.Count == 0)
                continue;

            rows.Add(new CustomerRow(
                customer.CustomerId,
                customer.Company,
                customerInvoices.FirstOrDefault()?.Currency ?? "MYR",
                customerInvoices.Sum(i => i.Amount),
                customerInvoices.Count,
                customerPackages.Sum(PackageProration.GetContractValue),
                // Valued at the close of the quarter, so a report re-run later gives the same number.
                customerPackages.Sum(p => PackageProration.GetActiveValue(p, end.AddDays(-1))),
                customerServices.Sum(s => s.UsedQty),
                customerServices.Count));
        }

        var invoiceRows = invoices
            .Select(i => new InvoiceRow(
                i.InvoiceNumber,
                companyById.TryGetValue(i.CustomerId, out var company) ? company : $"#{i.CustomerId}",
                i.Status,
                i.Currency,
                i.Amount,
                i.CreatedAt,
                i.IssuedAt))
            .ToList();

        return new Report(year, quarter, start, end, rows, invoiceRows);
    }

    public static byte[] ToCsv(Report report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"LGB Services quarterly billing report,{report.Label}");
        sb.AppendLine($"Period (UTC),{report.StartUtc:yyyy-MM-dd} to {report.EndUtc.AddDays(-1):yyyy-MM-dd}");
        sb.AppendLine();

        sb.AppendLine("Company,Currency,Invoices,Invoiced total,Package contract value,Remaining package value,Quota used,Services completed");
        foreach (var row in report.Customers)
        {
            sb.AppendLine(string.Join(',', [
                Escape(row.Company),
                Escape(row.Currency),
                row.InvoiceCount.ToString(CultureInfo.InvariantCulture),
                Money(row.InvoicedTotal),
                Money(row.ContractValue),
                Money(row.RemainingValue),
                row.QuotaUsed.ToString(CultureInfo.InvariantCulture),
                row.ServicesCompleted.ToString(CultureInfo.InvariantCulture),
            ]));
        }

        sb.AppendLine(string.Join(',', [
            "Total", "", report.Customers.Sum(c => c.InvoiceCount).ToString(CultureInfo.InvariantCulture),
            Money(report.InvoicedTotal), Money(report.ContractValueTotal), Money(report.RemainingValueTotal),
            report.QuotaUsedTotal.ToString(CultureInfo.InvariantCulture),
            report.Customers.Sum(c => c.ServicesCompleted).ToString(CultureInfo.InvariantCulture),
        ]));

        sb.AppendLine();
        sb.AppendLine("Invoice number,Company,Status,Currency,Amount,Created (UTC),Issued (UTC)");
        foreach (var invoice in report.Invoices)
        {
            sb.AppendLine(string.Join(',', [
                Escape(invoice.InvoiceNumber),
                Escape(invoice.Company),
                Escape(invoice.Status),
                Escape(invoice.Currency),
                Money(invoice.Amount),
                invoice.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                invoice.IssuedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            ]));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
            return '"' + text.Replace("\"", "\"\"") + '"';
        return text;
    }
}
