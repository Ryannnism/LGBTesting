using System.Text.Json;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class MoiFormMetadataHelper
{
    public static string? ReadRequiredExecutionDate(MOIForm? form)
    {
        var raw = ReadFormField(form, "requiredExecutionDate")
            ?? ReadFormField(form, "requiredDateOfExecution");
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    public static string? ReadDocumentTitle(MOIForm? form)
    {
        var raw = ReadFormField(form, "documentTitle")
            ?? ReadFormField(form, "DocumentTitle");
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static string? ReadFormField(MOIForm? form, string propertyName)
    {
        if (form == null || string.IsNullOrWhiteSpace(form.FormDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(form.FormDataJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var prop))
                return null;
            return prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : prop.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
