namespace LGBApp.Backend.Models;

public class AccountHolder
{
    public int AccountHolderId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool NeedsMoi { get; set; }
    public bool NeedsMoiApproval { get; set; }
    public bool NeedsMoa { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>True when added by a client admin (internal admin should review).</summary>
    public bool ClientAdded { get; set; }
    public int? AddedByUserId { get; set; }
}
