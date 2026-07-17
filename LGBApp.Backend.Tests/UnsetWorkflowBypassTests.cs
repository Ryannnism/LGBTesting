using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>Review #7 W3 / §6.2 — Unset workflow must not allow mark-complete.</summary>
public class UnsetWorkflowBypassTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unset_BlocksCompleteUntilWorkflowChosen(string? mode)
    {
        Assert.True(JobWorkflowModes.IsUnset(mode));
        Assert.True(JobWorkflowModes.BlocksCompleteUntilWorkflowChosen(mode));
    }

    [Theory]
    [InlineData(JobWorkflowModes.MoiMoa)]
    [InlineData(JobWorkflowModes.AdminBypass)]
    [InlineData("moimoa")]
    [InlineData("adminbypass")]
    public void ChosenModes_DoNotBlockAsUnset(string mode)
    {
        Assert.False(JobWorkflowModes.IsUnset(mode));
        Assert.False(JobWorkflowModes.BlocksCompleteUntilWorkflowChosen(mode));
    }
}
