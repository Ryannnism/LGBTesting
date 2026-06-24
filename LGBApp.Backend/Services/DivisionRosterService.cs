using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Division-group client signatory roster — Sharon sets up once in Admin; reused per customer company.
/// </summary>
public static class DivisionRosterService
{
    public static async Task ProvisionRecommenderLoginsAsync(AppDbContext context, DivisionGroup group)
    {
        var access = new SignatoryAccessService();

        foreach (var recommender in group.Recommenders)
        {
            if (string.IsNullOrWhiteSpace(recommender.Email))
            {
                recommender.UserId = null;
                continue;
            }

            var user = await FindOrCreateClientSignatoryAsync(context, recommender);
            recommender.UserId = user.UserId;

            var holderLinks = await context.AccountHolders
                .Where(h => h.UserId == user.UserId
                    || h.Email == recommender.Email.Trim()
                    || h.Name == recommender.DisplayName)
                .ToListAsync();

            foreach (var holder in holderLinks)
            {
                holder.UserId = user.UserId;
                await access.EnsureAccessAsync(context, user.UserId, holder.CustomerId);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task ApplyRosterToCustomerAsync(AppDbContext context, Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            return;

        var group = await context.DivisionGroups
            .Include(g => g.Recommenders)
            .FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);
        if (group == null)
            return;

        var access = new SignatoryAccessService();

        foreach (var recommender in group.Recommenders)
        {
            if (string.IsNullOrWhiteSpace(recommender.DisplayName))
                continue;

            var holder = customer.AccountHolders.FirstOrDefault(h =>
                (!string.IsNullOrWhiteSpace(recommender.Email)
                    && string.Equals(h.Email, recommender.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                || string.Equals(h.Name, recommender.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (holder == null)
            {
                holder = new AccountHolder { CustomerId = customer.CustomerId };
                customer.AccountHolders.Add(holder);
            }

            holder.Name = recommender.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(recommender.Email))
                holder.Email = recommender.Email.Trim();
            if (!string.IsNullOrWhiteSpace(recommender.Phone))
                holder.Phone = recommender.Phone.Trim();
            holder.NeedsMoi |= recommender.NeedsMoi;
            holder.NeedsMoiApproval |= recommender.NeedsMoiApproval;
            holder.NeedsMoa |= recommender.NeedsMoa;

            if (recommender.UserId.HasValue)
                holder.UserId = recommender.UserId;
        }

        CustomerSignatoryProvisioner.SyncCustomerSignerLists(customer);

        foreach (var holder in customer.AccountHolders.Where(h =>
                     !string.IsNullOrWhiteSpace(h.Email)
                     && (h.NeedsMoi || h.NeedsMoiApproval || h.NeedsMoa)))
        {
            var user = await CustomerSignatoryProvisioner.EnsureSignatoryUserAsync(
                context, customer, holder, provisionedByUserId: null, clientAdded: false);
            if (user != null)
                await access.EnsureAccessAsync(context, user.UserId, customer.CustomerId);
        }
    }

    private static async Task<User> FindOrCreateClientSignatoryAsync(
        AppDbContext context,
        DivisionGroupRecommender recommender)
    {
        if (recommender.UserId.HasValue)
        {
            var linked = await context.Users.FindAsync(recommender.UserId.Value);
            if (linked != null)
                return linked;
        }

        var email = recommender.Email.Trim();
        var emailLower = email.ToLowerInvariant();
        var byEmail = await context.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == emailLower);
        if (byEmail != null)
        {
            if (!string.Equals(byEmail.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Email {email} is already used by a non-signatory user.");

            byEmail.Name = recommender.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(recommender.Phone))
                byEmail.Mobile = recommender.Phone.Trim();
            return byEmail;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(CustomerSignatoryProvisioner.DefaultPassword),
            Name = recommender.DisplayName.Trim(),
            Mobile = recommender.Phone?.Trim() ?? string.Empty,
            Role = UserRoles.ClientSignatory,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
