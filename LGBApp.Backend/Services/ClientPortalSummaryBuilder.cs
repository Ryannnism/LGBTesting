using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class ClientPortalSummaryBuilder
{
    private static readonly string[] FormCategories = ["MOI", "MOI Approval", "MOA"];

    public static string ResolveCategory(JobRequest job)
    {
        if (FormCategories.Contains(job.TaskType, StringComparer.OrdinalIgnoreCase))
            return job.TaskType;
        return "Services";
    }

    public static List<Models.DTOs.TaskCategoryProgressDto> BuildCategoryProgress(IEnumerable<JobRequest> jobs)
    {
        var categories = new[] { "MOI", "MOI Approval", "MOA", "Services" };
        var units = jobs.SelectMany(j =>
        {
            if (j.Units.Count > 0)
                return j.Units.Select(u => (Category: ResolveCategory(j), Status: u.Status));
            return [(ResolveCategory(j), j.Status)];
        });

        return categories.Select(cat =>
        {
            var group = units.Where(u => u.Category == cat).ToList();
            return new Models.DTOs.TaskCategoryProgressDto
            {
                Category = cat,
                Pending = group.Count(u => u.Status == "Pending"),
                InProgress = group.Count(u => u.Status == "In Progress"),
                Completed = group.Count(u => u.Status == "Completed"),
                Total = group.Count,
            };
        }).Where(c => c.Total > 0).ToList();
    }
}
