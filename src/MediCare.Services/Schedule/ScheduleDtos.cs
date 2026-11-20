using MediCare.Data.Entities;
using MediCare.Services.Account;

namespace MediCare.Services.Schedule;

public class DoctorScheduleItemDto
{
    public int Id { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SlotMinutes { get; set; } = 30;
}

public class UpdateWorkingHoursDto
{
    public int DoctorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<DoctorScheduleItemDto> Schedules { get; set; } = new();
}

public class VacationDayDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public DateOnly VacationDate { get; set; }
    public string? Reason { get; set; }
}

public class AddVacationDayDto
{
    public int DoctorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly VacationDate { get; set; }
    public string? Reason { get; set; }
}

public class FreeSlotDto
{
    public string SlotId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SlotMinutes { get; set; }
    public string FormattedTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
    public DateTime StartDateTime => Date.ToDateTime(TimeOnly.FromTimeSpan(StartTime));
    public DateTime EndDateTime => Date.ToDateTime(TimeOnly.FromTimeSpan(EndTime));
}

public class ConflictingAppointmentDto
{
    public int AppointmentId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string ConflictReason { get; set; } = string.Empty;
}

public class ScheduleOperationResult : ServiceResult
{
    public List<ConflictingAppointmentDto> Conflicts { get; set; } = new();

    public static ScheduleOperationResult Conflict(string message, List<ConflictingAppointmentDto> conflicts)
    {
        return new ScheduleOperationResult
        {
            Succeeded = false,
            Error = message,
            Conflicts = conflicts
        };
    }

    public static new ScheduleOperationResult Ok()
    {
        return new ScheduleOperationResult { Succeeded = true };
    }

    public static new ScheduleOperationResult Fail(string error)
    {
        return new ScheduleOperationResult { Succeeded = false, Error = error };
    }
}
