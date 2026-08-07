using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LGBApp.Backend.Services;

/// <summary>B5 — one document: a per-customer summary followed by the invoice detail.</summary>
public static class BillingReportPdfService
{
    static BillingReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(BillingReportService.Report report)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("LGB Services").Bold().FontSize(16);
                    col.Item().Text($"Quarterly billing report — {report.Label}").FontSize(12)
                        .FontColor(Colors.Grey.Darken2);
                    col.Item().Text(
                        $"Period (UTC): {report.StartUtc:yyyy-MM-dd} to {report.EndUtc.AddDays(-1):yyyy-MM-dd}")
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text("Summary by customer").Bold().FontSize(11);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h, "Company");
                            HeaderCell(h, "Inv", right: true);
                            HeaderCell(h, "Invoiced", right: true);
                            HeaderCell(h, "Contract value", right: true);
                            HeaderCell(h, "Remaining value", right: true);
                            HeaderCell(h, "Quota", right: true);
                            HeaderCell(h, "Jobs", right: true);
                        });

                        foreach (var row in report.Customers)
                        {
                            Cell(table, row.Company);
                            Cell(table, row.InvoiceCount.ToString(), right: true);
                            Cell(table, $"{row.Currency} {row.InvoicedTotal:N2}", right: true);
                            Cell(table, $"{row.ContractValue:N2}", right: true);
                            Cell(table, $"{row.RemainingValue:N2}", right: true);
                            Cell(table, row.QuotaUsed.ToString(), right: true);
                            Cell(table, row.ServicesCompleted.ToString(), right: true);
                        }

                        TotalCell(table, "Total");
                        TotalCell(table, report.Customers.Sum(c => c.InvoiceCount).ToString(), right: true);
                        TotalCell(table, $"{report.InvoicedTotal:N2}", right: true);
                        TotalCell(table, $"{report.ContractValueTotal:N2}", right: true);
                        TotalCell(table, $"{report.RemainingValueTotal:N2}", right: true);
                        TotalCell(table, report.QuotaUsedTotal.ToString(), right: true);
                        TotalCell(table, report.Customers.Sum(c => c.ServicesCompleted).ToString(), right: true);
                    });

                    col.Item().PaddingTop(8).Text("Invoices raised in the quarter").Bold().FontSize(11);
                    if (report.Invoices.Count == 0)
                    {
                        col.Item().Text("No invoices in this period.").FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                HeaderCell(h, "Invoice no.");
                                HeaderCell(h, "Company");
                                HeaderCell(h, "Status");
                                HeaderCell(h, "Amount", right: true);
                                HeaderCell(h, "Created");
                                HeaderCell(h, "Issued");
                            });

                            foreach (var invoice in report.Invoices)
                            {
                                Cell(table, invoice.InvoiceNumber);
                                Cell(table, invoice.Company);
                                Cell(table, invoice.Status);
                                Cell(table, $"{invoice.Currency} {invoice.Amount:N2}", right: true);
                                Cell(table, $"{invoice.CreatedAt:yyyy-MM-dd}");
                                Cell(table, invoice.IssuedAt?.ToString("yyyy-MM-dd") ?? "—");
                            }
                        });
                    }

                    col.Item().PaddingTop(6).Text(
                        "Package values are the contract value of packages overlapping the quarter, and the "
                        + "prorated value remaining at the close of the quarter. Quota is package sessions consumed.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated by LGB Services · ");
                    t.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void HeaderCell(TableCellDescriptor header, string text, bool right = false)
    {
        var cell = header.Cell().BorderBottom(1).Padding(3);
        (right ? cell.AlignRight() : cell).Text(text).Bold();
    }

    private static void Cell(TableDescriptor table, string text, bool right = false)
    {
        var cell = table.Cell().BorderBottom((float)0.25).Padding(3);
        (right ? cell.AlignRight() : cell).Text(text);
    }

    private static void TotalCell(TableDescriptor table, string text, bool right = false)
    {
        var cell = table.Cell().BorderTop(1).Padding(3);
        (right ? cell.AlignRight() : cell).Text(text).Bold();
    }
}
