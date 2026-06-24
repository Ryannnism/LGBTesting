namespace LGBApp.Backend.Models;

public class DivisionGroupRecommender
{
    public int DivisionGroupRecommenderId { get; set; }
    public int DivisionGroupId { get; set; }
    public DivisionGroup DivisionGroup { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool NeedsMoi { get; set; }
    public bool NeedsMoiApproval { get; set; }
    public bool NeedsMoa { get; set; }
}
