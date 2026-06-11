namespace LGBApp.Backend.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;

        // This maps to 'password' from the frontend
        public string PasswordHash { get; set; } = string.Empty;

        // Brand new additions to match your Figma elements:
        public string Name { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public string JobTitle { get; set; } = string.Empty;
        public bool CanRecommendMoi { get; set; }
        /// <summary>Approve client-submitted MOI before resolution staff can see/assign.</summary>
        public bool CanApproveMoiIntake { get; set; }
        /// <summary>Sign off MOI after internal preparation (oversight — sees MOI before assignment).</summary>
        public bool CanApproveMoi { get; set; }
        /// <summary>Sign off MOA workflow steps (oversight — sees MOA before assignment).</summary>
        public bool CanApproveMoa { get; set; }
        /// <summary>Internal MOA workflow approver (named step assignee — not a client signatory).</summary>
        public bool IsInternalSignatory { get; set; }
        /// <summary>Set for ClientAdmin and Client — scopes access to one customer company.</summary>
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public bool IsVerified { get; set; } = false;
        /// <summary>When true, user must set a new password before using the app.</summary>
        public bool MustChangePassword { get; set; } = true;
        /// <summary>User id of the admin who invited this account (invitation-to-config).</summary>
        public int? InvitedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}