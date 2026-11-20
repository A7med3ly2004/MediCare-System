using System.ComponentModel.DataAnnotations;
using MediCare.Web.Attributes;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MediCare.Web.ViewModels.Account;

public class RegisterDoctorViewModel
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

    [Required(ErrorMessage = "Specialization is required.")]
    [Display(Name = "Specialization")]
    public int SpecializationId { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(150, ErrorMessage = "Location cannot exceed 150 characters.")]
    [Display(Name = "Clinic location / Address")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Consultation fee is required.")]
    [Range(0, 100000, ErrorMessage = "Consultation fee must be non-negative.")]
    [Display(Name = "Consultation fee (EGP)")]
    public decimal ConsultationFee { get; set; }

    [StringLength(20, ErrorMessage = "Room number cannot exceed 20 characters.")]
    [Display(Name = "Room number")]
    public string? RoomNumber { get; set; }

    [Display(Name = "Profile photo (JPG, PNG, WebP — max 2MB)")]
    public IFormFile? ProfileImage { get; set; }

    [MustBeTrue(ErrorMessage = "You must accept the privacy notice to register.")]
    [Display(Name = "I accept the privacy policy and terms of service")]
    public bool AcceptedPrivacyNotice { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Specializations { get; set; } = Enumerable.Empty<SelectListItem>();
}
