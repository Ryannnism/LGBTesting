using System.Text.Json;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

public static class MoiApprovalMatrixSeeder
{
    private sealed class MatrixRow
    {
        public string GroupCode { get; set; } = "";
        public string RequesterName { get; set; } = "";
        public string RequesterEmail { get; set; } = "";
        public string ApproverName { get; set; } = "";
        public string ApproverEmail { get; set; } = "";
    }

    public static void Seed(AppDbContext context)
    {
        var path = ResolveSeedPath("moi-approval-matrix.json");
        if (path == null || !File.Exists(path))
        {
            Console.WriteLine("[Startup] moi-approval-matrix.json not found — skip matrix seed.");
            return;
        }

        var rows = JsonSerializer.Deserialize<List<MatrixRow>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var existing = context.MoiApprovalMatrixEntries.ToList()
            .GroupBy(e => e.RequesterEmail.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var email = (row.RequesterEmail ?? "").Trim().ToLowerInvariant();
            var approverEmail = (row.ApproverEmail ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(approverEmail))
                continue;
            if (!seenInFile.Add(email))
                continue; // JSON may list same requester twice — first wins

            if (existing.TryGetValue(email, out var cur))
            {
                cur.GroupCode = row.GroupCode ?? "";
                cur.RequesterName = row.RequesterName?.Trim() ?? "";
                cur.ApproverName = row.ApproverName?.Trim() ?? "";
                cur.ApproverEmail = approverEmail;
                continue;
            }

            context.MoiApprovalMatrixEntries.Add(new MoiApprovalMatrixEntry
            {
                GroupCode = row.GroupCode ?? "",
                RequesterName = row.RequesterName?.Trim() ?? "",
                RequesterEmail = email,
                ApproverName = row.ApproverName?.Trim() ?? "",
                ApproverEmail = approverEmail,
            });
            added++;
        }

        context.SaveChanges();

        // Ensure approvers can log in as ClientSignatory (no customer link yet — email login for MOA/MOI).
        EnsureMatrixUsers(context, rows);
        Console.WriteLine($"[Startup] MOI approval matrix: {rows.Count} rows ({added} new).");
    }

    private static void EnsureMatrixUsers(AppDbContext context, List<MatrixRow> rows)
    {
        var password = Environment.GetEnvironmentVariable("SEED_STAFF_PASSWORD");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            password = "password123";

        var emails = rows
            .SelectMany(r => new[] { r.RequesterEmail, r.ApproverEmail })
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var email in emails)
        {
            if (context.Users.Any(u => u.Email == email))
                continue;

            var row = rows.FirstOrDefault(r =>
                string.Equals(r.RequesterEmail, email, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.ApproverEmail, email, StringComparison.OrdinalIgnoreCase));
            var name = row != null && string.Equals(row.ApproverEmail, email, StringComparison.OrdinalIgnoreCase)
                ? row.ApproverName
                : row?.RequesterName ?? email;

            context.Users.Add(new User
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(name) ? email : name.Trim(),
                PasswordHash = PasswordHasher.Hash(password),
                Role = UserRoles.ClientSignatory,
                IsVerified = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.SaveChanges();
    }

    private static string? ResolveSeedPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "LGBApp.Backend", "Data", "Seed", fileName),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
