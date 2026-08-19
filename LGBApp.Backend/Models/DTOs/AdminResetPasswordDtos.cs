namespace LGBApp.Backend.Models.DTOs;

public class AdminResetPasswordRequest
{
    /// <summary>Optional. When omitted the server generates a temporary password.</summary>
    public string? NewPassword { get; set; }
}

public class AdminResetPasswordResponse
{
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
