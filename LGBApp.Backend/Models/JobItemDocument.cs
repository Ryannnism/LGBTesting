namespace LGBApp.Backend.Models;

/// <summary>File in a package-item folder (MOI, MOA, or supporting).</summary>
public class JobItemDocument
{
    public int JobItemDocumentId { get; set; }
    public int JobRequestId { get; set; }
    public JobRequest JobRequest { get; set; } = null!;
    public int? JobRequestUnitId { get; set; }
    public JobRequestUnit? JobRequestUnit { get; set; }
    /// <summary>moi | moa | supporting</summary>
    public string Folder { get; set; } = "supporting";
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public int UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    /// <summary>False while MOI is still in client-only draft (hidden from internal staff).</summary>
    public bool VisibleToInternal { get; set; }
}
