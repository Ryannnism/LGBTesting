using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class ClientPortalSummaryBuilder
{
    public static async Task<List<Models.DTOs.TaskCategoryProgressDto>> BuildCategoryProgressAsync(
        AppDbContext context,
        IEnumerable<JobRequest> jobs,
        bool clientServiceView = true)
    {
        if (clientServiceView)
            return await BuildServiceLineCategoryProgressAsync(context, jobs);

        return await BuildTaskTypeCategoryProgressAsync(context, jobs);
    }

    /// <summary>Client portal: per-session counts grouped by service category (Board meetings, etc.).</summary>
    private static async Task<List<Models.DTOs.TaskCategoryProgressDto>> BuildServiceLineCategoryProgressAsync(
        AppDbContext context,
        IEnumerable<JobRequest> jobs)
    {
        var jobList = jobs
            .Where(j => string.Equals(j.TaskType, "Service", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (jobList.Count == 0)
            return [];

        var jobIds = jobList.Select(j => j.JobRequestId).ToList();
        var moiForms = await context.MOIForms
            .Where(f => f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
            .ToListAsync();

        var moisByJobId = moiForms
            .Where(f => f.JobRequestId.HasValue)
            .GroupBy(f => f.JobRequestId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var statusKeys = new List<(string Category, string StatusKey)>();

        foreach (var job in jobList)
        {
            var mois = moisByJobId.GetValueOrDefault(job.JobRequestId) ?? [];
            var units = job.Units.Count > 0
                ? job.Units.OrderBy(u => u.UnitNumber).ToList()
                : [new JobRequestUnit { JobRequestUnitId = 0, UnitNumber = 1, JobRequestId = job.JobRequestId }];

            foreach (var unit in units)
            {
                var moi = FindMoiForUnit(mois, job, unit);
                var status = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);
                var category = PackageServiceCategoryResolver.Resolve(job.Service);
                statusKeys.Add((category, status.Key));
                statusKeys.Add((PackageServiceCategoryResolver.AllServices, status.Key));
            }
        }

        return PackageServiceCategoryResolver.DisplayOrder
            .Select(cat => BuildProgressDto(cat, statusKeys.Where(u => u.Category == cat).ToList()))
            .Where(c => c.Total > 0)
            .ToList();
    }

    /// <summary>Legacy/internal: MOI, MOI Approval, MOA, Services task-type buckets.</summary>
    private static async Task<List<Models.DTOs.TaskCategoryProgressDto>> BuildTaskTypeCategoryProgressAsync(
        AppDbContext context,
        IEnumerable<JobRequest> jobs)
    {
        var jobList = jobs.ToList();
        var jobIds = jobList.Select(j => j.JobRequestId).ToList();
        var moiForms = await context.MOIForms
            .Where(f => f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
            .ToListAsync();

        var moiByJobId = moiForms
            .Where(f => f.JobRequestId.HasValue)
            .GroupBy(f => f.JobRequestId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var categories = new[] { "MOI", "MOI Approval", "MOA", "Services" };
        var statusKeys = new List<(string Category, string StatusKey)>();

        foreach (var job in jobList)
        {
            var moiForm = job.TaskType is "MOI" or "Service"
                ? moiByJobId.GetValueOrDefault(job.JobRequestId)
                : null;
            var pairedMoi = job.TaskType == "MOI Approval"
                ? jobList.FirstOrDefault(j =>
                    j.CustomerId == job.CustomerId
                    && j.TaskType == "MOI"
                    && string.Equals(j.AccountHolder, job.AccountHolder, StringComparison.OrdinalIgnoreCase))
                : jobList.FirstOrDefault(j =>
                    j.CustomerId == job.CustomerId
                    && string.Equals(j.AccountHolder, job.AccountHolder, StringComparison.OrdinalIgnoreCase)
                    && j.TaskType == "MOI");
            var pairedMoiForm = pairedMoi != null ? moiByJobId.GetValueOrDefault(pairedMoi.JobRequestId) : null;

            var status = PackageItemStatusResolver.Resolve(job, moiForm, pairedMoiForm);
            var category = ResolveTaskTypeCategory(job);

            if (job.Units.Count > 0)
            {
                foreach (var unit in job.Units)
                {
                    var unitMoi = FindMoiForUnit(
                        moiForms.Where(f => f.JobRequestId == job.JobRequestId).ToList(),
                        job,
                        unit);
                    var unitStatus = PackageItemStatusResolver.ResolveForUnit(job, unit, unitMoi);
                    statusKeys.Add((category, unitStatus.Key));
                }
            }
            else
            {
                statusKeys.Add((category, status.Key));
            }
        }

        return categories
            .Select(cat => BuildProgressDto(cat, statusKeys.Where(u => u.Category == cat).ToList()))
            .Where(c => c.Total > 0)
            .ToList();
    }

    private static Models.DTOs.TaskCategoryProgressDto BuildProgressDto(
        string category,
        List<(string Category, string StatusKey)> group) => new()
    {
        Category = category,
        Pending = group.Count(u => PackageItemStatuses.IsPendingBucket(u.StatusKey)),
        InProgress = group.Count(u => PackageItemStatuses.IsInProgressBucket(u.StatusKey)),
        Completed = group.Count(u => PackageItemStatuses.IsCompletedBucket(u.StatusKey)),
        Total = group.Count,
    };

    private static MOIForm? FindMoiForUnit(List<MOIForm> mois, JobRequest job, JobRequestUnit unit)
    {
        if (unit.JobRequestUnitId > 0)
        {
            var byUnit = mois.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
            if (byUnit != null)
                return byUnit;
        }

        return job.TotalQty <= 1
            ? mois.FirstOrDefault(f => f.JobRequestUnitId == null)
            : null;
    }

    private static string ResolveTaskTypeCategory(JobRequest job)
    {
        if (job.TaskType is "MOI" or "MOI Approval" or "MOA")
            return job.TaskType;
        return "Services";
    }
}
