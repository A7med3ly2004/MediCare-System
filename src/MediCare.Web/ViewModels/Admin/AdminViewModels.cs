using System.ComponentModel.DataAnnotations;
using MediCare.Data.Entities;
using MediCare.Services.Admin;
using MediCare.Services.Appointments;

namespace MediCare.Web.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public AdminDashboardDto DashboardData { get; set; } = new();
    public DailyReportDto DailyReport { get; set; } = new();
    public DateOnly SelectedDate { get; set; }
}

public class AdminReportsViewModel
{
    public DateOnly SelectedDate { get; set; }
    public DailyReportDto DailyReport { get; set; } = new();
    public AnalyticsReportDto Analytics { get; set; } = new();
}

public class AdminDoctorsListViewModel
{
    public PagedResult<DoctorAdminListItemDto> Paging { get; set; } = new();
    public string? Search { get; set; }
    public int? SpecializationId { get; set; }
    public bool? IsSuspended { get; set; }
    public List<SpecializationAdminDto> Specializations { get; set; } = new();
}

public class AdminEditDoctorViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    public int SpecializationId { get; set; }

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [StringLength(20)]
    public string? RoomNumber { get; set; }

    [Range(0, 100000)]
    public decimal ConsultationFee { get; set; }

    public List<SpecializationAdminDto> AvailableSpecializations { get; set; } = new();
}

public class AdminPatientsListViewModel
{
    public PagedResult<PatientAdminListItemDto> Paging { get; set; } = new();
    public string? Search { get; set; }
    public bool? IsSuspended { get; set; }
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
}

public class AdminEditPatientViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(10)]
    public string? BloodGroup { get; set; }

    [StringLength(500)]
    public string? Allergies { get; set; }

    [StringLength(1000)]
    public string? MedicalHistory { get; set; }
}

public class AdminSpecializationsViewModel
{
    public List<SpecializationAdminDto> Specializations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class AdminAppointmentsListViewModel
{
    public PagedResult<AppointmentListItemDto> Paging { get; set; } = new();
    public DateOnly? SelectedDate { get; set; }
    public int? DoctorId { get; set; }
    public AppointmentStatus? Status { get; set; }
    public string? Search { get; set; }
    public List<DoctorAdminListItemDto> Doctors { get; set; } = new();

    // Summary counts for cards
    public int TotalAppointments { get; set; }
    public int ConfirmedCount { get; set; }
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
}

public class AdminNotificationsListViewModel
{
    public PagedResult<NotificationAdminListItemDto> Paging { get; set; } = new();
    public string? Status { get; set; }
    public string? Type { get; set; }
}
