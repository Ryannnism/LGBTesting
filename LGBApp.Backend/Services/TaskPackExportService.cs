using System.Net;
using System.Text;
using System.Text.Json;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

/// <summary>
/// Renders the whole pack for a SINGLE task (job line) — MOI + MOA + checklist + sign-off trail
/// + document index — as one self-contained, printable HTML document. Multi-session jobs render
/// one block per session. Distinct from the per-form JSON export in <see cref="FormPackExportService"/>.
/// </summary>
public static class TaskPackExportService
{
    public sealed record Session(int? UnitNumber, string? Status, MOIForm? Moi, MOAForm? Moa);

    public static byte[] BuildHtml(
        JobRequest job,
        Customer? customer,
        IReadOnlyList<Session> sessions,
        IReadOnlyList<JobItemDocument> documents,
        string generatedByName)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append($"<title>Task Pack — {E(job.Service)} ({E(job.Customer)})</title>");
        sb.Append(Style());
        sb.Append("</head><body>");

        sb.Append("<div class=\"actions no-print\"><button onclick=\"window.print()\">Print / Save as PDF</button></div>");

        // Header
        sb.Append("<header><h1>Task Pack</h1>");
        sb.Append("<table class=\"meta\">");
        Meta(sb, "Company", job.Customer);
        Meta(sb, "Service", string.IsNullOrWhiteSpace(job.TaskType) || job.TaskType == "Service" ? job.Service : $"{job.TaskType} — {job.Service}");
        Meta(sb, "Account holder", job.AccountHolder);
        Meta(sb, "Sessions", job.TotalQty > 1 ? $"{job.UsedQty} of {job.TotalQty} completed" : "Single");
        Meta(sb, "Job status", job.Status);
        Meta(sb, "Handoff", string.IsNullOrWhiteSpace(job.InternalHandoffStatus) ? "—" : job.InternalHandoffStatus);
        Meta(sb, "Requested", job.DateRequested.ToString("yyyy-MM-dd"));
        if (job.DateCompleted.HasValue) Meta(sb, "Completed", job.DateCompleted.Value.ToString("yyyy-MM-dd"));
        Meta(sb, "Generated", $"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by {generatedByName}");
        sb.Append("</table></header>");

        if (sessions.Count == 0)
            sb.Append("<p class=\"empty\">No MOI/MOA forms exist for this task yet.</p>");

        foreach (var s in sessions)
        {
            sb.Append("<section class=\"session\">");
            if (s.UnitNumber.HasValue && job.TotalQty > 1)
                sb.Append($"<h2>Session #{s.UnitNumber}{(string.IsNullOrWhiteSpace(s.Status) ? "" : $" — {E(s.Status)}")}</h2>");

            RenderMoi(sb, s.Moi);
            RenderMoa(sb, s.Moa);
            sb.Append("</section>");
        }

        RenderDocuments(sb, documents);

        sb.Append("</body></html>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void RenderMoi(StringBuilder sb, MOIForm? moi)
    {
        if (moi == null) return;
        sb.Append("<div class=\"form\"><h3>MOI — Memorandum of Instruction</h3>");
        sb.Append("<table class=\"meta\">");
        Meta(sb, "Workflow state", moi.WorkflowState);
        Meta(sb, "Template", moi.FormTemplateCode);
        if (moi.FinanceRelated) Meta(sb, "Finance related", "Yes");
        if (moi.BankSignatoryMatter) Meta(sb, "Bank signatory matter", "Yes");
        if (!string.IsNullOrWhiteSpace(moi.RecommendationComments)) Meta(sb, "Recommendation", moi.RecommendationComments);
        Meta(sb, "Last updated", moi.UpdatedAt.ToString("yyyy-MM-dd HH:mm") + " UTC");
        sb.Append("</table>");
        RenderFormBody(sb, moi.FormDataJson);
        RenderApprovals(sb, "Client sign-off", moi.ClientApprovalsJson);
        RenderRejections(sb, moi.RejectionsJson);
        sb.Append("</div>");
    }

    private static void RenderMoa(StringBuilder sb, MOAForm? moa)
    {
        if (moa == null) return;
        sb.Append("<div class=\"form\"><h3>MOA — Minutes of Agreement / Resolution</h3>");
        sb.Append("<table class=\"meta\">");
        Meta(sb, "Template", moa.FormTemplateCode);
        if (moa.FinanceRelated) Meta(sb, "Finance related", "Yes");
        if (moa.BankSignatoryMatter) Meta(sb, "Bank signatory matter", "Yes");
        if (moa.ShareMovement) Meta(sb, "Share movement", "Yes");
        if (moa.SubmittedForAdminReviewAt.HasValue) Meta(sb, "Submitted for review", moa.SubmittedForAdminReviewAt.Value.ToString("yyyy-MM-dd HH:mm") + " UTC");
        if (moa.SharonApprovedAt.HasValue) Meta(sb, "Head-secretary approved", moa.SharonApprovedAt.Value.ToString("yyyy-MM-dd HH:mm") + " UTC");
        Meta(sb, "Last updated", moa.UpdatedAt.ToString("yyyy-MM-dd HH:mm") + " UTC");
        sb.Append("</table>");
        RenderChecklist(sb, moa.PackChecklistJson);
        RenderFormBody(sb, moa.FormDataJson);
        RenderApprovals(sb, "Client sign-off", moa.ClientApprovalsJson);
        RenderRejections(sb, moa.RejectionsJson);
        sb.Append("</div>");
    }

    private static void RenderFormBody(StringBuilder sb, string formDataJson)
    {
        Dictionary<string, object?> data;
        try { data = JsonHelper.Deserialize<Dictionary<string, object?>>(formDataJson) ?? new(); }
        catch { data = new(); }

        var rows = data
            .Where(kv => !HiddenKeys.Contains(kv.Key) && !IsBlank(kv.Value))
            .ToList();
        if (rows.Count == 0) return;

        sb.Append("<table class=\"body\"><tbody>");
        foreach (var (key, value) in rows)
            sb.Append($"<tr><th>{E(Humanize(key))}</th><td>{E(Stringify(value))}</td></tr>");
        sb.Append("</tbody></table>");
    }

    private static void RenderChecklist(StringBuilder sb, string packJson)
    {
        MoaPackChecklist? pack;
        try { pack = JsonSerializer.Deserialize<MoaPackChecklist>(packJson, JsonOpts); }
        catch { pack = null; }
        if (pack == null) return;

        sb.Append("<h4>Pack checklist</h4><table class=\"body\"><tbody>");
        Check(sb, "Internal checklist A", pack.InternalChecklistA);
        Check(sb, "Internal checklist B", pack.InternalChecklistB);
        Check(sb, "Clean agreement attached", pack.CleanAgreementAttached);
        Check(sb, "Shareholding table attached", pack.ShareholdingTableAttached);
        if (!string.IsNullOrWhiteSpace(pack.SsmRegistrationNo)) sb.Append($"<tr><th>SSM registration no.</th><td>{E(pack.SsmRegistrationNo)}</td></tr>");
        if (!string.IsNullOrWhiteSpace(pack.SsmEntityType)) sb.Append($"<tr><th>SSM entity type</th><td>{E(pack.SsmEntityType)}</td></tr>");
        if (!string.IsNullOrWhiteSpace(pack.SsmStatus)) sb.Append($"<tr><th>SSM status</th><td>{E(pack.SsmStatus)}</td></tr>");
        if (!string.IsNullOrWhiteSpace(pack.SsmAsAtDate)) sb.Append($"<tr><th>SSM as-at date</th><td>{E(pack.SsmAsAtDate)}</td></tr>");
        sb.Append("</tbody></table>");
    }

    private static void RenderApprovals(StringBuilder sb, string title, string approvalsJson)
    {
        List<ClientApprovalRecord> records;
        try { records = JsonHelper.Deserialize<List<ClientApprovalRecord>>(approvalsJson) ?? new(); }
        catch { records = new(); }
        if (records.Count == 0) return;

        sb.Append($"<h4>{E(title)}</h4><table class=\"signoff\"><thead><tr><th>Signatory</th><th>Signed at (UTC)</th><th>Comments</th><th>Signature</th></tr></thead><tbody>");
        foreach (var r in records)
        {
            var sig = !string.IsNullOrWhiteSpace(r.SignatureDataUrl)
                ? $"<img class=\"sig\" src=\"{EAttr(r.SignatureDataUrl!)}\" alt=\"signature\">"
                : (string.IsNullOrWhiteSpace(r.SignatureFileName) ? "—" : E(r.SignatureFileName!));
            sb.Append($"<tr><td>{E(r.AccountHolderName)}</td><td>{r.SignedAt:yyyy-MM-dd HH:mm}</td><td>{E(r.Comments)}</td><td>{sig}</td></tr>");
        }
        sb.Append("</tbody></table>");
    }

    private static void RenderRejections(StringBuilder sb, string rejectionsJson)
    {
        List<FormRejectionRecord> records;
        try { records = JsonHelper.Deserialize<List<FormRejectionRecord>>(rejectionsJson) ?? new(); }
        catch { records = new(); }
        if (records.Count == 0) return;

        sb.Append("<h4>Rejection history</h4><table class=\"signoff\"><thead><tr><th>Stage</th><th>By</th><th>At (UTC)</th><th>Reason</th></tr></thead><tbody>");
        foreach (var r in records)
            sb.Append($"<tr><td>{E(r.Stage)}</td><td>{E(r.UserName)}</td><td>{r.RejectedAt:yyyy-MM-dd HH:mm}</td><td>{E(r.Reason)}</td></tr>");
        sb.Append("</tbody></table>");
    }

    private static void RenderDocuments(StringBuilder sb, IReadOnlyList<JobItemDocument> documents)
    {
        sb.Append("<section class=\"documents\"><h3>Attached documents</h3>");
        if (documents.Count == 0)
        {
            sb.Append("<p class=\"empty\">No documents attached.</p></section>");
            return;
        }
        sb.Append("<table class=\"signoff\"><thead><tr><th>Folder</th><th>File</th><th>Uploaded by</th><th>Uploaded (UTC)</th></tr></thead><tbody>");
        foreach (var d in documents.OrderBy(d => d.Folder).ThenBy(d => d.UploadedAt))
            sb.Append($"<tr><td>{E(d.Folder.ToUpperInvariant())}</td><td>{E(d.FileName)}</td><td>{E(d.UploadedByName)}</td><td>{d.UploadedAt:yyyy-MM-dd HH:mm}</td></tr>");
        sb.Append("</tbody></table>");
        sb.Append("<p class=\"note\">Document files are stored separately and are not embedded in this printout.</p></section>");
    }

    // ---- helpers ----
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<string> HiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "jobId", "unitNumber", "sessionLabel",
    };

    private static void Meta(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.Append($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
    }

    private static void Check(StringBuilder sb, string label, bool done) =>
        sb.Append($"<tr><th>{E(label)}</th><td>{(done ? "☑ Yes" : "☐ No")}</td></tr>");

    private static bool IsBlank(object? v) =>
        v is null || (v is string s && string.IsNullOrWhiteSpace(s)) || (v is JsonElement je && je.ValueKind is JsonValueKind.Null);

    private static string Stringify(object? v)
    {
        if (v is null) return string.Empty;
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.Number => je.ToString(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Null => string.Empty,
                _ => je.GetRawText(),
            };
        }
        return v.ToString() ?? string.Empty;
    }

    private static string Humanize(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        var sb = new StringBuilder(key.Length + 6);
        sb.Append(char.ToUpperInvariant(key[0]));
        for (var i = 1; i < key.Length; i++)
        {
            var c = key[i];
            if (char.IsUpper(c) && !char.IsUpper(key[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
    private static string EAttr(string s) => WebUtility.HtmlEncode(s).Replace("\"", "&quot;");

    private static string Style() => """
        <style>
          :root { color-scheme: light; }
          * { box-sizing: border-box; }
          body { font: 13px/1.5 -apple-system, "Segoe UI", Roboto, Arial, sans-serif; color: #1a1a1a; max-width: 900px; margin: 24px auto; padding: 0 20px; }
          h1 { font-size: 22px; margin: 0 0 4px; }
          h2 { font-size: 16px; margin: 20px 0 8px; border-bottom: 2px solid #333; padding-bottom: 4px; }
          h3 { font-size: 15px; margin: 16px 0 6px; color: #0b5; color: #106b3f; }
          h4 { font-size: 13px; margin: 12px 0 4px; color: #555; text-transform: uppercase; letter-spacing: .04em; }
          table { border-collapse: collapse; width: 100%; margin: 4px 0 12px; }
          .meta th, .meta td, .body th, .body td { text-align: left; vertical-align: top; padding: 4px 8px; border-bottom: 1px solid #eee; }
          .meta th, .body th { width: 34%; color: #555; font-weight: 600; }
          .signoff th, .signoff td { text-align: left; padding: 5px 8px; border: 1px solid #ddd; font-size: 12px; }
          .signoff thead th { background: #f4f4f4; }
          .session { margin: 8px 0 20px; }
          .form { border: 1px solid #e2e2e2; border-radius: 6px; padding: 10px 14px; margin: 10px 0; }
          .documents { margin-top: 24px; }
          .sig { max-height: 44px; max-width: 160px; }
          .empty { color: #888; font-style: italic; }
          .note { color: #888; font-size: 11px; }
          .actions { margin-bottom: 16px; }
          button { font-size: 13px; padding: 8px 16px; border: 1px solid #106b3f; background: #106b3f; color: #fff; border-radius: 6px; cursor: pointer; }
          @media print { .no-print { display: none; } body { margin: 0; } .form { break-inside: avoid; } }
        </style>
        """;
}
