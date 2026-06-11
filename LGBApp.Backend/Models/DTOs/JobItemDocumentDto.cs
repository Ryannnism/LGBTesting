namespace LGBApp.Backend.Models.DTOs;

public class JobItemDocumentDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string Folder { get; set; } = "supporting";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedAt { get; set; } = string.Empty;
    public bool VisibleToInternal { get; set; }
}

public class JobItemFolderDto
{
    public string Folder { get; set; } = string.Empty;
    public List<JobItemDocumentDto> Documents { get; set; } = [];
}

public class JobItemFoldersResponse
{
    public int JobId { get; set; }
    public string Service { get; set; } = string.Empty;
    public int? MoiFormId { get; set; }
    public int? MoaFormId { get; set; }
    public string? MoiWorkflowState { get; set; }
    public List<JobItemFolderDto> Folders { get; set; } = [];
}

public class UpdateMoiApprovalModeRequest
{
    public string MoiApprovalMode { get; set; } = MoiApprovalModes.AllRequired;
}
