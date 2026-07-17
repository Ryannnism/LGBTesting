namespace LGBApp.Backend.Services;

/// <summary>D1: per-job (or per-unit) choice — run MOI/MOA or send a note to Sharon.</summary>
public static class JobWorkflowModes
{
    public const string Unset = "";
    public const string MoiMoa = "MoiMoa";
    public const string AdminBypass = "AdminBypass";

    public static bool IsUnset(string? mode) =>
        string.IsNullOrWhiteSpace(mode);

    public static bool IsMoiMoa(string? mode) =>
        string.Equals(mode, MoiMoa, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdminBypass(string? mode) =>
        string.Equals(mode, AdminBypass, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Review #7 W3 / §6.2: Unset must not fall through to Completed — client must choose
    /// MoiMoa or AdminBypass first (flowchart is approval-gated end-to-end).
    /// </summary>
    public static bool BlocksCompleteUntilWorkflowChosen(string? mode) =>
        IsUnset(mode);
}
