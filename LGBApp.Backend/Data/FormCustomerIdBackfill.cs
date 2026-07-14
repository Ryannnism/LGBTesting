using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

/// <summary>§6.2: one-off backfill of MOI/MOA.CustomerId from company name match.</summary>
public static class FormCustomerIdBackfill
{
    public static void Apply(AppDbContext context)
    {
        var companies = context.Customers
            .AsNoTracking()
            .Select(c => new { c.CustomerId, c.Company })
            .ToList();
        if (companies.Count == 0)
            return;

        var byCompany = companies
            .GroupBy(c => c.Company, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CustomerId, StringComparer.OrdinalIgnoreCase);

        var mois = context.MOIForms.Where(f => f.CustomerId == null && f.Company != "").ToList();
        foreach (var form in mois)
        {
            if (byCompany.TryGetValue(form.Company, out var customerId))
                form.CustomerId = customerId;
        }

        var moas = context.MOAForms.Where(f => f.CustomerId == null && f.Company != "").ToList();
        foreach (var form in moas)
        {
            if (byCompany.TryGetValue(form.Company, out var customerId))
                form.CustomerId = customerId;
        }

        if (mois.Count > 0 || moas.Count > 0)
            context.SaveChanges();
    }
}
