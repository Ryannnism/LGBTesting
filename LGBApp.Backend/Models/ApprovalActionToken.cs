namespace LGBApp.Backend.Models;

/// <summary>
/// Single-use bearer token for no-login MOA step approval (SR7 W4).
/// Never grants a session — scoped to one WorkflowStepInstance.
/// </summary>
public class ApprovalActionToken
{
    public int ApprovalActionTokenId { get; set; }
    /// <summary>SHA-256 hex of the raw token sent in email.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public int WorkflowStepInstanceId { get; set; }
    public WorkflowStepInstance? WorkflowStepInstance { get; set; }
    public int MoaFormId { get; set; }
    public int? AssigneeUserId { get; set; }
    public string AssigneeEmail { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
