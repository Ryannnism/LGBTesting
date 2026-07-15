using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class FormConcurrencyHelper
{
    /// <summary>
    /// Cheap pre-check against <c>updatedAt</c>. N2: no 1-second tolerance — exact match only.
    /// The real guard is <c>ConcurrencyStamp</c> + <see cref="SaveWithConcurrencyAsync"/>.
    /// </summary>
    public static ActionResult? CheckExpectedUpdatedAt(DateTime storedUtc, string? expectedUpdatedAt)
    {
        if (string.IsNullOrWhiteSpace(expectedUpdatedAt))
            return null;

        if (!DateTime.TryParse(
                expectedUpdatedAt,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var expected))
        {
            return null;
        }

        var expectedUtc = expected.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expected, DateTimeKind.Utc)
            : expected.ToUniversalTime();

        // form.UpdatedAt is written as UTC, but SQLite/EF hands it back with Kind=Unspecified.
        // Calling ToUniversalTime() on an Unspecified value shifts it by the server's local
        // offset, so on any non-UTC host every save falsely conflicts. Treat Unspecified as UTC
        // to mirror the expected-side normalization above.
        var storedNormalized = storedUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(storedUtc, DateTimeKind.Utc)
            : storedUtc.ToUniversalTime();

        // Compare to the millisecond so ISO round-trips match; no multi-second grace window.
        var storedMs = storedNormalized.Ticks / TimeSpan.TicksPerMillisecond;
        var expectedMs = expectedUtc.Ticks / TimeSpan.TicksPerMillisecond;
        if (storedMs == expectedMs)
            return null;

        return ConflictResult(storedUtc);
    }

    public static async Task<ActionResult?> SaveWithConcurrencyAsync(
        DbContext context,
        DateTime previousUpdatedAt,
        Func<Task>? beforeConflictReload = null)
    {
        try
        {
            await context.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (beforeConflictReload != null)
                await beforeConflictReload();
            return ConflictResult(previousUpdatedAt);
        }
    }

    public static ConflictObjectResult ConflictResult(DateTime updatedAt) =>
        new(new
        {
            message = "This form was updated by someone else. Refresh and try again.",
            updatedAt = updatedAt.ToString("O"),
        });
}
