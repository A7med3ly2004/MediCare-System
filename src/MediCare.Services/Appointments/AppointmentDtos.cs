using MediCare.Data.Entities;

namespace MediCare.Services.Appointments;

public class BookAppointmentDto
{
    public string PatientUserId { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Notes { get; set; }
}

public class CancelAppointmentDto
{
    public int AppointmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class RescheduleAppointmentDto
{
    public int AppointmentId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public DateOnly NewDate { get; set; }
    public TimeSpan NewStartTime { get; set; }
    public TimeSpan NewEndTime { get; set; }
}

public class ConfirmRejectDto
{
    public int AppointmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public int? DoctorId { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class AppointmentDetailDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientEmail { get; set; }

    public int DoctorId { get; set; }
    public string DoctorUserId { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public decimal ConsultationFee { get; set; }

    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; }

    public bool CanCancelOrReschedule { get; set; }
    public string FormattedTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
}

public class AppointmentListItemDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }

    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public decimal ConsultationFee { get; set; }

    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool CanCancelOrReschedule { get; set; }
    public string FormattedTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
}

public class DoctorDashboardDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }

    // KPIs
    public int TodayPatientsCount { get; set; }
    public int CompletedVisitsCount { get; set; }
    public int PendingRequestsCount { get; set; }
    public AppointmentListItemDto? NextConsultation { get; set; }

    // Appointment Lists
    public List<AppointmentListItemDto> TodayAppointments { get; set; } = new();
    public List<AppointmentListItemDto> PendingAppointments { get; set; } = new();
    public List<AppointmentListItemDto> UpcomingWeekAppointments { get; set; } = new();
}

public class PatientDashboardDto
{
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }

    public AppointmentListItemDto? NextAppointment { get; set; }
    public List<AppointmentListItemDto> RecentAppointments { get; set; } = new();
}
