namespace LGBApp.Backend.Models.DTOs;

public class AccountHolderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool Moi { get; set; }
    public bool MoiApproval { get; set; }
    public bool Moa { get; set; }
    public int? UserId { get; set; }
    public bool ClientAdded { get; set; }
    public int? AddedByUserId { get; set; }
}
