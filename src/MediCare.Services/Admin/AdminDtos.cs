using MediCare.Data.Entities;
using MediCare.Services.Appointments;

namespace MediCare.Services.Admin;

public class DailyReportDto
{
    public DateOnly Date { get; set; }
    public int NewPatientRegistrations { get; set; }
    public int NewDoctorRegistrations { get; set; }
    public int AppointmentsBooked { get; set; }
    public int AppointmentsConfirmed { get; set; }
    public int AppointmentsCancelled { get; set; }
    public int CompletedConsultations { get; set; }
    public decimal EstimatedRevenue { get; set; }
}

public class MonthlyTrendDto
{
    public string Month { get; set; } = string.Empty; // e.g. "Oct 2026"
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public int TotalBooked { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public decimal Revenue { get; set; }
}

public class DoctorPerformanceDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public int CompletedVisits { get; set; }
    public int TotalAssigned { get; set; }
    public decimal CompletionRate => TotalAssigned > 0 ? Math.Round((decimal)CompletedVisits / TotalAssigned * 100m, 1) : 0m;
    public decimal Revenue { get; set; }
}

public class SpecializationPopularityDto
{
    public int SpecializationId { get; set; }
    public string SpecializationName { get; set; } = string.Empty;
    public int AppointmentCount { get; set; }
    public int DoctorCount { get; set; }
}

public class PatientGrowthDto
{
    public string Month { get; set; } = string.Empty;
    public int NewPatients { get; set; }
}

public class AdminDashboardDto
{
    public int TotalPatients { get; set; }
    public int ActivePatients { get; set; }
    public int NewPatientsThisMonth { get; set; }

    public int TodayAppointmentsCount { get; set; }
    public int TodayCompletedCount { get; set; }
    public int TodayRemainingCount { get; set; }

    public int AvailableDoctorsCount { get; set; }
    public int TotalDoctorsCount { get; set; }

    public decimal MonthlyEstimatedRevenue { get; set; }
    public int MonthlyCompletedVisits { get; set; }

    public int PendingConfirmationsCount { get; set; }

    // Chart.js data series
    public List<string> ActivityDays { get; set; } = new();
    public List<int> ActivityCounts { get; set; } = new();

    public List<string> WeeklyDays { get; set; } = new();
    public List<int> WeeklyCounts { get; set; } = new();

    public List<AppointmentListItemDto> UpcomingAppointments { get; set; } = new();
    public List<DoctorAvailabilityItemDto> DoctorAvailabilities { get; set; } = new();
}

public class DoctorAvailabilityItemDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public bool IsAvailableToday { get; set; }
    public string WorkingHoursText { get; set; } = string.Empty;
}

public class AnalyticsReportDto
{
    public DateOnly SelectedDate { get; set; }
    public DailyReportDto DailySummary { get; set; } = new();
    public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
    public List<DoctorPerformanceDto> DoctorPerformances { get; set; } = new();
    public List<SpecializationPopularityDto> PopularSpecializations { get; set; } = new();
    public List<PatientGrowthDto> PatientGrowth { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class DoctorAdminListItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public int? SpecializationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsDeleted { get; set; }
    public int ActiveAppointmentsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PatientAdminListItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? BloodGroup { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsDeleted { get; set; }
    public int ActiveAppointmentsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly? LatestVisitDate { get; set; }
    public string? LatestDoctorName { get; set; }
}

public class SpecializationAdminDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DoctorCount { get; set; }
    public bool IsDeleted { get; set; }
}

public class NotificationAdminListItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class UpdateDoctorAdminDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public decimal ConsultationFee { get; set; }
}

public class UpdatePatientAdminDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }
}
