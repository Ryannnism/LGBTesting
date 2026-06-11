using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class UserMapper
{
    public static UserResponse ToResponse(User user, Customer? customer = null)
    {
        var response = new UserResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            Name = user.Name,
            Mobile = user.Mobile,
            Role = user.Role,
            JobTitle = user.JobTitle,
            CanRecommendMoi = user.CanRecommendMoi,
            CanApproveMoiIntake = user.CanApproveMoiIntake,
            CanApproveMoi = user.CanApproveMoi,
            CanApproveMoa = user.CanApproveMoa,
            IsInternalSignatory = user.IsInternalSignatory,
            CustomerId = user.CustomerId,
            CustomerName = user.Customer?.Company ?? customer?.Company,
            IsVerified = user.IsVerified,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
        };

        ApplySignatoryFlags(response, user, customer ?? user.Customer);
        return response;
    }

    private static void ApplySignatoryFlags(UserResponse response, User user, Customer? customer)
    {
        if (!string.Equals(user.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase)
            || customer?.AccountHolders == null)
            return;

        var holder = customer.AccountHolders.FirstOrDefault(h =>
            h.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(h.Email)
                && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)));

        if (holder == null)
            return;

        response.NeedsMoi = holder.NeedsMoi;
        response.NeedsMoiApproval = holder.NeedsMoiApproval;
        response.NeedsMoa = holder.NeedsMoa;
    }
}
