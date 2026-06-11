namespace LGBApp.Backend.Services;

/// <summary>Groups package service lines into client-friendly categories (portal counters).</summary>
public static class PackageServiceCategoryResolver
{
    public const string AllServices = "All services";

    public static readonly string[] DisplayOrder =
    [
        AllServices,
        "Board meetings",
        "Resolutions",
        "Annual compliance",
        "Secretarial & audit",
        "Support services",
        "Lodgement fees",
        "Other services",
    ];

    public static string Resolve(string serviceName)
    {
        var s = (serviceName ?? string.Empty).Trim();
        if (s.Length == 0)
            return "Other services";

        if (s.Contains("board meeting", StringComparison.OrdinalIgnoreCase))
            return "Board meetings";

        if (s.Contains("resolution", StringComparison.OrdinalIgnoreCase)
            || s.Contains("reso", StringComparison.OrdinalIgnoreCase))
            return "Resolutions";

        if (s.Contains("annual", StringComparison.OrdinalIgnoreCase)
            || s.Contains("MBRS", StringComparison.OrdinalIgnoreCase)
            || s.Contains("BO Declaration", StringComparison.OrdinalIgnoreCase)
            || s.Contains("audited", StringComparison.OrdinalIgnoreCase))
            return "Annual compliance";

        if (s.Contains("support", StringComparison.OrdinalIgnoreCase))
            return "Support services";

        if (s.Contains("secretarial", StringComparison.OrdinalIgnoreCase)
            || s.Contains("register office", StringComparison.OrdinalIgnoreCase)
            || s.Contains("auditor", StringComparison.OrdinalIgnoreCase))
            return "Secretarial & audit";

        if (s.Contains("lodgement", StringComparison.OrdinalIgnoreCase))
            return "Lodgement fees";

        return "Other services";
    }
}
