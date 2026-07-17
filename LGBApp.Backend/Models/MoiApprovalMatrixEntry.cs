namespace LGBApp.Backend.Models;

/// <summary>CubeV Approval Matrix: one MOI requester → one MOI approver.</summary>
public class MoiApprovalMatrixEntry
{
    public int MoiApprovalMatrixEntryId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
}
