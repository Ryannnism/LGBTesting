namespace LGBApp.Backend.Models.DTOs;

public class ClientApproveRequest
{
    public string Comments { get; set; } = string.Empty;
    public string? SignatureFileName { get; set; }
    public string? SignatureDataUrl { get; set; }
}
