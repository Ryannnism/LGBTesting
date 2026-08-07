using System.Text.Json;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

/// <summary>
/// Flowchart T3 — the MOI "Last Point of Approval" block. The client captures it as
/// formData.approvalPersons; this promotes it to a column so MS7 and reporting can read it
/// without parsing form JSON.
/// </summary>
public static class LastPointOfApprovalService
{
    public sealed record Entry(string Name, string Position);

    public static List<Entry> Read(MOIForm? form) =>
        Parse(form?.LastPointOfApprovalJson);

    public static List<Entry> ReadFromFormData(IDictionary<string, object?> formData)
    {
        if (!formData.TryGetValue("approvalPersons", out var raw) || raw == null)
            return [];
        return Parse(raw as string ?? JsonHelper.Serialize(raw));
    }

    public static bool IsWithLoa(IDictionary<string, object?> formData) =>
        formData.TryGetValue("withLOA", out var raw) && FormDataHelper.IsTruthy(raw);

    /// <summary>Keeps the column in step with the submitted form JSON. Clears it when LOA is off.</summary>
    public static void Sync(MOIForm form, IDictionary<string, object?> formData)
    {
        var entries = IsWithLoa(formData) ? ReadFromFormData(formData) : [];
        form.LastPointOfApprovalJson = JsonHelper.Serialize(entries);
    }

    /// <summary>Names for MS7, in the order the client entered them.</summary>
    public static string? ResolveFinalApproverName(MOIForm? form)
    {
        var names = Read(form)
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static List<Entry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var entries = new List<Entry>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;
                var name = ReadString(element, "name") ?? ReadString(element, "Name");
                var position = ReadString(element, "position") ?? ReadString(element, "Position");
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(position))
                    continue;
                entries.Add(new Entry((name ?? "").Trim(), (position ?? "").Trim()));
            }
            return entries;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
