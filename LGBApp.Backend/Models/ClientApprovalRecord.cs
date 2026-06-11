namespace LGBApp.Backend.Models;

public class ClientApprovalRecord
{
    public int UserId { get; set; }
    public string AccountHolderName { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string? SignatureFileName { get; set; }
    public string? SignatureDataUrl { get; set; }
    public DateTime SignedAt { get; set; }
}
