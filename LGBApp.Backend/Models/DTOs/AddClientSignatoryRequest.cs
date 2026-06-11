using System.ComponentModel.DataAnnotations;

namespace LGBApp.Backend.Models.DTOs;

public class AddClientSignatoryRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public bool Moi { get; set; }
    public bool MoiApproval { get; set; }
    public bool Moa { get; set; }
}
