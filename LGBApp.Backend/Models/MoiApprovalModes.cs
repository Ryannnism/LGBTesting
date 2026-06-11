namespace LGBApp.Backend.Models;

public static class MoiApprovalModes
{
    /// <summary>Every listed MOI approver must sign before release to LGB.</summary>
    public const string AllRequired = "AllRequired";

    /// <summary>Any one listed MOI approver signing is enough to release to LGB.</summary>
    public const string AnyOne = "AnyOne";
}
