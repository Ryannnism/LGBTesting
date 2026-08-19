using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

/// <summary>
/// Item 10 — staff passwords must be rotatable without working email delivery.
/// </summary>
public class AdminPasswordResetTests
{
    private static User SeedUser(TestDbFactory db, string email = "staff@test.local")
    {
        var user = new User
        {
            Email = email,
            Name = "Staff",
            Role = UserRoles.User,
            PasswordHash = PasswordHasher.Hash("OldPassword1"),
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Reset_ReplacesTheHash_AndForcesAChange()
    {
        using var db = new TestDbFactory();
        var user = SeedUser(db);
        var before = user.PasswordHash;

        var temp = await AdminPasswordResetService.ResetAsync(db.Context, user);

        Assert.NotEqual(before, user.PasswordHash);
        Assert.True(PasswordHasher.Verify(temp, user.PasswordHash));
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public async Task Reset_VoidsOutstandingResetCodes()
    {
        using var db = new TestDbFactory();
        var user = SeedUser(db);
        db.Context.PasswordResetOtps.Add(new PasswordResetOtp
        {
            Email = user.Email,
            CodeHash = "not-a-real-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
        });
        db.Context.SaveChanges();

        await AdminPasswordResetService.ResetAsync(db.Context, user);

        Assert.All(db.Context.PasswordResetOtps.ToList(), o => Assert.NotNull(o.ConsumedAt));
    }

    [Fact]
    public async Task Reset_RejectsAnExplicitPasswordBelowPolicy()
    {
        using var db = new TestDbFactory();
        var user = SeedUser(db);

        await Assert.ThrowsAsync<DomainException>(
            () => AdminPasswordResetService.ResetAsync(db.Context, user, "abc"));
    }

    [Fact]
    public void GeneratedPassword_MeetsPolicy()
    {
        var password = AdminPasswordResetService.GenerateTemporaryPassword();

        Assert.Equal(12, password.Length);
        Assert.True(PasswordPolicy.MeetsMinLength(password));
    }
}
