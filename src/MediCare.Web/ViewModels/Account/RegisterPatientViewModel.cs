using System.ComponentModel.DataAnnotations;
using MediCare.Web.Attributes;

namespace MediCare.Web.ViewModels.Account;

public class RegisterPatientViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Date of birth is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(5, ErrorMessage = "Blood group cannot exceed 5 characters.")]
    [Display(Name = "Blood group")]
    public string? BloodGroup { get; set; }

    [StringLength(500, ErrorMessage = "Allergies description cannot exceed 500 characters.")]
    [Display(Name = "Known allergies")]
    public string? Allergies { get; set; }

    [StringLength(2000, ErrorMessage = "Medical history cannot exceed 2000 characters.")]
    [Display(Name = "Medical history")]
    public string? MedicalHistory { get; set; }

    [MustBeTrue(ErrorMessage = "You must accept the privacy notice to register.")]
    [Display(Name = "I accept the privacy policy and terms of service")]
    public bool AcceptedPrivacyNotice { get; set; }
}
