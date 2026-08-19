using System.Security.Cryptography;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Admin-side password reset. The self-service OTP path needs a working mail sender, so it is
/// unusable while delivery is limited to the sandbox address; this path needs no mail at all.
/// </summary>
public static class AdminPasswordResetService
{
    // Omits look-alike characters so a password read off a screen is retyped correctly.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string GenerateTemporaryPassword(int length = 12)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <returns>The password the user must sign in with next.</returns>
    public static async Task<string> ResetAsync(AppDbContext context, User user, string? newPassword = null)
    {
        var password = string.IsNullOrWhiteSpace(newPassword)
            ? GenerateTemporaryPassword()
            : newPassword.Trim();

        if (!PasswordPolicy.MeetsMinLength(password))
            throw new DomainException($"New password must be at least {PasswordPolicy.MinLength} characters.");

        user.PasswordHash = PasswordHasher.Hash(password);
        user.MustChangePassword = true;

        // A code requested just before the reset must not still work after it.
        var email = PasswordPolicy.NormalizeEmail(user.Email);
        var now = DateTime.UtcNow;
        var live = await context.PasswordResetOtps
            .Where(o => o.Email.ToLower() == email && o.ConsumedAt == null)
            .ToListAsync();
        foreach (var otp in live)
            otp.ConsumedAt = now;

        await context.SaveChangesAsync();
        return password;
    }
}
