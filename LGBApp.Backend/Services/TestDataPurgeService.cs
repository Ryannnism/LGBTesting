using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Removes acceptance-walkthrough data (HANDOVER §11) from a live database. The prefix is fixed here
/// rather than taken from the caller so no request can widen the blast radius.
/// </summary>
public static class TestDataPurgeService
{
    public const string Prefix = "ZZ TEST";

    /// <summary>More companies than a walkthrough ever creates means something is wrong — stop.</summary>
    public const int MaxCustomers = 5;

    public sealed class PurgeReport
    {
        public bool Applied { get; set; }
        public List<string> Companies { get; set; } = [];
        public int Customers { get; set; }
        public int Users { get; set; }
        public int Products { get; set; }
        public int JobRequests { get; set; }
        public int MoiForms { get; set; }
        public int MoaForms { get; set; }
        public int WorkflowInstances { get; set; }
        public int Invoices { get; set; }
        public int Notifications { get; set; }
        public int ReminderLogs { get; set; }
        public int Documents { get; set; }
        /// <summary>Storage keys the caller should delete from disk after a successful apply.</summary>
        public List<string> StorageKeys { get; set; } = [];
    }

    public static async Task<PurgeReport> RunAsync(AppDbContext context, bool apply)
    {
        var customers = await context.Customers
            .Where(c => c.Company.StartsWith(Prefix) || c.Name.StartsWith(Prefix))
            .ToListAsync();

        if (customers.Count > MaxCustomers)
            throw new DomainException(
                $"{customers.Count} companies match '{Prefix}' — refusing to purge more than {MaxCustomers}.");

        var customerIds = customers.Select(c => c.CustomerId).ToList();

        var jobs = await context.JobRequests
            .Where(j => (j.CustomerId != null && customerIds.Contains(j.CustomerId.Value))
                || j.Customer.StartsWith(Prefix))
            .ToListAsync();
        var jobIds = jobs.Select(j => j.JobRequestId).ToList();

        var moiForms = await context.MOIForms
            .Where(f => (f.CustomerId != null && customerIds.Contains(f.CustomerId.Value))
                || (f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
                || f.Company.StartsWith(Prefix))
            .ToListAsync();
        var moiIds = moiForms.Select(f => f.MOIFormId).ToList();

        var moaForms = await context.MOAForms
            .Where(f => (f.CustomerId != null && customerIds.Contains(f.CustomerId.Value))
                || (f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
                || (f.MOIFormId != null && moiIds.Contains(f.MOIFormId.Value)))
            .ToListAsync();
        var moaIds = moaForms.Select(f => f.MOAFormId).ToList();

        var instances = await context.WorkflowInstances
            .Include(i => i.Steps)
            .Where(i => (i.MoiFormId != null && moiIds.Contains(i.MoiFormId.Value))
                || (i.MoaFormId != null && moaIds.Contains(i.MoaFormId.Value)))
            .ToListAsync();
        var stepIds = instances.SelectMany(i => i.Steps).Select(s => s.WorkflowStepInstanceId).ToList();

        var invoices = await context.Invoices
            .Where(i => customerIds.Contains(i.CustomerId)
                || (i.JobRequestId != null && jobIds.Contains(i.JobRequestId.Value)))
            .ToListAsync();

        var notifications = await context.AppNotifications
            .Where(n => (n.CustomerId != null && customerIds.Contains(n.CustomerId.Value))
                || (n.JobRequestId != null && jobIds.Contains(n.JobRequestId.Value)))
            .ToListAsync();

        var reminders = await context.ReminderLogs
            .Where(r => (r.TargetEntityType == ReminderKinds.TargetMoiForm && moiIds.Contains(r.TargetEntityId))
                || (r.TargetEntityType == ReminderKinds.TargetWorkflowStep && stepIds.Contains(r.TargetEntityId)))
            .ToListAsync();

        var documents = await context.JobItemDocuments
            .Where(d => jobIds.Contains(d.JobRequestId))
            .ToListAsync();

        // Client accounts of the test company, plus anything explicitly named for the test. Internal
        // staff are never matched by company, and the two seeded live-test logins are protected.
        var protectedEmails = new[]
        {
            InternalStaffSeeder.LiveTestAdminEmail,
            InternalStaffSeeder.LiveTestClientEmail,
        };
        var users = (await context.Users
            .Where(u => (u.CustomerId != null && customerIds.Contains(u.CustomerId.Value))
                || u.Name.StartsWith(Prefix))
            .ToListAsync())
            .Where(u => !protectedEmails.Contains(u.Email, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var products = await context.Products
            .Where(p => p.PackageName.StartsWith(Prefix))
            .ToListAsync();

        var report = new PurgeReport
        {
            Companies = customers.Select(c => c.Company).OrderBy(c => c).ToList(),
            Customers = customers.Count,
            Users = users.Count,
            Products = products.Count,
            JobRequests = jobs.Count,
            MoiForms = moiForms.Count,
            MoaForms = moaForms.Count,
            WorkflowInstances = instances.Count,
            Invoices = invoices.Count,
            Notifications = notifications.Count,
            ReminderLogs = reminders.Count,
            Documents = documents.Count,
            StorageKeys = documents.Select(d => d.StorageKey).ToList(),
        };

        if (!apply)
            return report;

        // Invoices first: their customer FK is Restrict, so they would block the customer delete.
        context.Invoices.RemoveRange(invoices);
        context.AppNotifications.RemoveRange(notifications);
        context.ReminderLogs.RemoveRange(reminders);
        await context.SaveChangesAsync();

        // Forms cascade to workflow instance → step instances → approval action tokens.
        context.MOAForms.RemoveRange(moaForms);
        context.MOIForms.RemoveRange(moiForms);
        await context.SaveChangesAsync();

        // Jobs cascade to units, unit assignees and service job forms.
        context.JobItemDocuments.RemoveRange(documents);
        context.JobRequests.RemoveRange(jobs);
        await context.SaveChangesAsync();

        context.Users.RemoveRange(users);
        context.Products.RemoveRange(products);
        await context.SaveChangesAsync();

        // Customers cascade to account holders, packages, completed services and signatory access.
        context.Customers.RemoveRange(customers);
        await context.SaveChangesAsync();

        report.Applied = true;
        return report;
    }
}
