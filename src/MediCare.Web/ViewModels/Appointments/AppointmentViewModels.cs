using System.ComponentModel.DataAnnotations;
using MediCare.Services.Appointments;

namespace MediCare.Web.ViewModels.Appointments;

public class BookAppointmentFormModel
{
    [Required]
    public int DoctorId { get; set; }

    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }

    [Required(ErrorMessage = "Appointment date is required.")]
    public DateOnly AppointmentDate { get; set; }

    [Required(ErrorMessage = "Start time is required.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "End time is required.")]
    public string EndTime { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    [Display(Name = "Reason for visit or clinical notes (optional)")]
    public string? Notes { get; set; }
}

public class CancelAppointmentFormModel
{
    [Required]
    public int AppointmentId { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class RescheduleAppointmentFormModel
{
    [Required]
    public int AppointmentId { get; set; }

    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "New appointment date is required.")]
    public DateOnly NewDate { get; set; }

    [Required(ErrorMessage = "New start time is required.")]
    public string NewStartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "New end time is required.")]
    public string NewEndTime { get; set; } = string.Empty;
}

public class PatientAppointmentsPageViewModel
{
    public string Filter { get; set; } = "all"; // all, upcoming, past
    public List<AppointmentListItemDto> Appointments { get; set; } = new();
}
