using System.ComponentModel.DataAnnotations;

namespace MediCare.Web.ViewModels.Account;

public class VerifyOtpViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the 6-digit verification code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The verification code must be exactly 6 digits.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "The verification code must be exactly 6 digits.")]
    [Display(Name = "6-digit code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Seconds remaining before a resend is allowed (if cooldown is currently active).
    /// </summary>
    public int? CooldownSecondsRemaining { get; set; }
}
