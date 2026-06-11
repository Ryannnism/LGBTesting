namespace LGBApp.Backend.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string ClientAdmin = "ClientAdmin";
    /// <summary>External signatory — can complete MOI/MOA assigned to them; no portal admin access.</summary>
    public const string ClientSignatory = "ClientSignatory";
    /// <summary>Legacy role — migrated to ClientAdmin on startup.</summary>
    public const string Client = "Client";

    public static readonly string[] All = [Admin, User, ClientAdmin, ClientSignatory];
    public static readonly string[] Internal = [Admin, User];
    public static readonly string[] External = [ClientAdmin, ClientSignatory];

    public static bool IsValid(string? role) =>
        !string.IsNullOrWhiteSpace(role) && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool IsInternalRole(string? role) =>
        Internal.Contains(role ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static bool IsExternalRole(string? role) =>
        External.Contains(role ?? string.Empty, StringComparer.OrdinalIgnoreCase);
}
