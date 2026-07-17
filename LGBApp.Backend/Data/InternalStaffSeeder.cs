using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

/// <summary>
/// Seeds LGB internal team from CubeV Cosec Email + Group Legal Email tabs.
/// Sharon + Poh Li are Admins. SEED_STAFF_PASSWORD required in non-Development.
/// </summary>
public static class InternalStaffSeeder
{
    private static readonly (string Email, string Name, string Role, string JobTitle,
        bool CanApproveMoiIntake, bool CanRecommendMoi, bool CanApproveMoi, bool CanApproveMoa,
        bool IsInternalSignatory)[] Staff =
    [
        ("sharon@lgb.com.my", "Sharon", UserRoles.Admin, "Senior Manager, Company Secretarial", true, true, true, true, true),
        ("pohli.ng@taliworks.com.my", "Ng Poh Li", UserRoles.Admin, "Senior Manager, Company Secretarial", true, true, true, true, true),
        ("dzatin.zaharuddin@taliworks.com.my", "Nita", UserRoles.User, "Resolution preparation", false, false, false, false, false),
        ("zalila.zainal@lgb.com.my", "Siti", UserRoles.User, "Resolution preparation", false, false, false, false, false),
        ("nadia.rahman@taliworks.com.my", "Nadia", UserRoles.User, "Resolution preparation", false, false, false, false, false),
        ("raj@taliworks.com.my", "Datin Raj", UserRoles.User, "Group Legal", false, false, false, true, true),
        ("seetmei.lee@taliworks.com.my", "Seet Mei", UserRoles.User, "Group Legal", false, false, false, true, true),
        ("deenee.ooi@taliworks.com.my", "Dee Nee", UserRoles.User, "Group Legal", false, false, false, true, true),
        ("sutina.sujeno@taliworks.com.my", "Sutina", UserRoles.User, "Group Legal", false, false, false, true, true),
        // Live email-chain testing
        ("ryannnism@gmail.com", "Ryan Admin", UserRoles.Admin, "Senior Manager, Company Secretarial", true, true, true, true, true),
    ];

    /// <summary>Old seed emails → real CubeV addresses (update in place on existing DBs).</summary>
    private static readonly (string From, string To)[] EmailAliases =
    [
        ("sharon@lgb.test", "sharon@lgb.com.my"),
        ("ngpohli@lgb.test", "pohli.ng@taliworks.com.my"),
        ("nita@lgb.test", "dzatin.zaharuddin@taliworks.com.my"),
        ("siti@lgb.test", "zalila.zainal@lgb.com.my"),
        ("nadia@lgb.test", "nadia.rahman@taliworks.com.my"),
    ];

    private static readonly string[] IntakeApproverEmails =
    [
        "sharon@lgb.com.my",
        "pohli.ng@taliworks.com.my",
        "ryannnism@gmail.com",
        "danra69@gmail.com",
    ];

    public const string LiveTestAdminEmail = "ryannnism@gmail.com";
    public const string LiveTestClientEmail = "ryannnism@berkeley.edu";

    public static void Seed(
        AppDbContext context,
        bool resetPasswordsInDevelopment = false,
        string initialPassword = "password123")
    {
        if (string.IsNullOrWhiteSpace(initialPassword) || initialPassword.Length < 6)
            throw new InvalidOperationException(
                $"Initial staff password must be at least {PasswordPolicy.MinLength} characters.");

        foreach (var (from, to) in EmailAliases)
        {
            var old = context.Users.FirstOrDefault(u => u.Email == from);
            if (old == null) continue;
            if (context.Users.Any(u => u.Email == to))
                continue; // real account already exists — leave alias row alone
            old.Email = to;
        }

        foreach (var (email, name, role, jobTitle, canApproveIntake, canRecommend, canApproveMoi, canApproveMoa, isInternalSignatory) in Staff)
        {
            var existing = context.Users.FirstOrDefault(u => u.Email == email);
            if (existing != null)
            {
                existing.Name = name;
                existing.Role = role;
                existing.JobTitle = jobTitle;
                existing.CanApproveMoiIntake = canApproveIntake;
                existing.CanRecommendMoi = canRecommend;
                existing.CanApproveMoi = canApproveMoi;
                existing.CanApproveMoa = canApproveMoa;
                existing.IsInternalSignatory = isInternalSignatory;
                if (resetPasswordsInDevelopment)
                {
                    existing.PasswordHash = PasswordHasher.Hash(initialPassword);
                    existing.MustChangePassword = true;
                }
                continue;
            }

            context.Users.Add(new User
            {
                Email = email,
                PasswordHash = PasswordHasher.Hash(initialPassword),
                Name = name,
                Mobile = "",
                Role = role,
                JobTitle = jobTitle,
                CanApproveMoiIntake = canApproveIntake,
                CanRecommendMoi = canRecommend,
                CanApproveMoi = canApproveMoi,
                CanApproveMoa = canApproveMoa,
                IsInternalSignatory = isInternalSignatory,
                IsVerified = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        foreach (var email in IntakeApproverEmails)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
                user.CanApproveMoiIntake = true;
        }

        EnsureLiveTestClientAdmin(context, initialPassword, resetPasswordsInDevelopment);
        context.SaveChanges();
    }

    private static void EnsureLiveTestClientAdmin(
        AppDbContext context,
        string initialPassword,
        bool resetPassword)
    {
        var customer = context.Customers.FirstOrDefault(c => c.Status == "Active")
            ?? context.Customers.FirstOrDefault();

        var existing = context.Users.FirstOrDefault(u => u.Email == LiveTestClientEmail);
        if (existing != null)
        {
            existing.Role = UserRoles.ClientAdmin;
            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? "Ryan Client" : existing.Name;
            existing.IsVerified = true;
            if (customer != null)
                existing.CustomerId = customer.CustomerId;
            if (resetPassword)
            {
                existing.PasswordHash = PasswordHasher.Hash(initialPassword);
                existing.MustChangePassword = true;
            }
            return;
        }

        if (customer == null)
            return;

        context.Users.Add(new User
        {
            Email = LiveTestClientEmail,
            PasswordHash = PasswordHasher.Hash(initialPassword),
            Name = "Ryan Client",
            Mobile = "",
            Role = UserRoles.ClientAdmin,
            CustomerId = customer.CustomerId,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
