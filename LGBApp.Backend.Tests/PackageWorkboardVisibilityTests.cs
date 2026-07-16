using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>
/// Package workboard must list all catalog lines; the release gate is for the ops queue only.
/// </summary>
public class PackageWorkboardVisibilityTests
{
    [Fact]
    public void UnreleasedSeededJob_IsHiddenFromInternalQueue_ButExistsInDb()
    {
        var job = new JobRequest
        {
            JobRequestId = 42,
            CustomerPackageId = 2,
            TaskType = "Service",
            Service = "Annual Return",
            TotalQty = 1,
            UsedQty = 0,
            InternalHandoffStatus = string.Empty,
            Units = [new JobRequestUnit { UnitNumber = 1 }],
        };

        var filtered = InternalWorkVisibilityHelper.FilterJobsForInternal(
            [job],
            new Dictionary<int, List<MOIForm>>());

        Assert.Empty(filtered);
        Assert.Equal(1, job.TotalQty);
        Assert.Equal(0, job.UsedQty);
    }
}
