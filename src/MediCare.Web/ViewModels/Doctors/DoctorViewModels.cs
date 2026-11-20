using System.ComponentModel.DataAnnotations;
using MediCare.Services.Doctors;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MediCare.Web.ViewModels.Doctors;

public class DoctorIndexViewModel
{
    public int? SpecializationId { get; set; }
    public string? LocationKeyword { get; set; }
    public DateOnly? AvailableOn { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;

    public IEnumerable<SelectListItem> Specializations { get; set; } = Enumerable.Empty<SelectListItem>();
    public PagedResult<DoctorListItemDto> Doctors { get; set; } = new();
    public DoctorDirectoryStatsDto Stats { get; set; } = new();

    public bool HasActiveFilters =>
        SpecializationId.HasValue ||
        !string.IsNullOrWhiteSpace(LocationKeyword) ||
        AvailableOn.HasValue ||
        !string.IsNullOrWhiteSpace(SearchTerm);
}

public class DoctorDetailsViewModel
{
    public DoctorDetailsDto Doctor { get; set; } = null!;
    public bool CanEditProfile { get; set; }
    public DateOnly SelectedDate { get; set; }
    public bool IsAvailableOnSelectedDate { get; set; }
    public string FormattedConsultingHours { get; set; } = string.Empty;
}

public class DoctorEditProfileViewModel
{
    public int DoctorId { get; set; }
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Specialization is required.")]
    [Display(Name = "Specialization")]
    public int SpecializationId { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(150, ErrorMessage = "Location cannot exceed 150 characters.")]
    [Display(Name = "Clinic location")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Consultation fee is required.")]
    [Range(0, 100000, ErrorMessage = "Consultation fee must be non-negative.")]
    [Display(Name = "Consultation fee (EGP)")]
    public decimal ConsultationFee { get; set; }

    [StringLength(20, ErrorMessage = "Room number cannot exceed 20 characters.")]
    [Display(Name = "Room number")]
    public string? RoomNumber { get; set; }

    public string? CurrentProfileImage { get; set; }

    [Display(Name = "New profile photo (JPG, PNG, WebP — max 2MB)")]
    public IFormFile? ProfileImage { get; set; }

    public IEnumerable<SelectListItem> Specializations { get; set; } = Enumerable.Empty<SelectListItem>();
}
