using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class MoiFormPairingService
{
    public static async Task<MOIForm?> FindMoiFormForCustomerAsync(AppDbContext context, int customerId)
    {
        var moiJobIds = await context.JobRequests
            .Where(j => j.CustomerId == customerId && j.TaskType == "MOI")
            .Select(j => j.JobRequestId)
            .ToListAsync();

        if (moiJobIds.Count == 0)
            return null;

        return await context.MOIForms
            .Where(f => f.JobRequestId != null && moiJobIds.Contains(f.JobRequestId.Value))
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public static async Task<MOIForm?> FindMoiFormForApprovalJobAsync(
        AppDbContext context,
        JobRequest approvalJob)
    {
        if (!approvalJob.CustomerId.HasValue)
            return null;

        var customerId = approvalJob.CustomerId.Value;

        var pending = await context.MOIForms
            .Where(f => f.WorkflowState == MoiWorkflowStates.PendingClientMoiApproval)
            .Join(
                context.JobRequests.Where(j => j.CustomerId == customerId),
                f => f.JobRequestId,
                j => j.JobRequestId,
                (f, _) => f)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();

        if (pending != null)
            return pending;

        return await FindMoiFormForCustomerAsync(context, customerId);
    }

    public static async Task<JobRequest?> FindMoiJobForFormAsync(AppDbContext context, MOIForm form)
    {
        if (!form.JobRequestId.HasValue)
            return null;

        return await context.JobRequests.FindAsync(form.JobRequestId.Value);
    }
}
