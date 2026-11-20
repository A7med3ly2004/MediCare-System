namespace MediCare.Services.Doctors;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class DoctorFilterDto
{
    public int? SpecializationId { get; set; }
    public string? LocationKeyword { get; set; }
    public DateOnly? AvailableOn { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;
}

public class DoctorListItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public string? RoomNumber { get; set; }
    public string? ProfileImage { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string ConsultingHoursSummary { get; set; } = string.Empty;
    public bool IsAvailableToday { get; set; }
    public bool IsAvailableOnFilterDate { get; set; }
}

public class DoctorScheduleDto
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SlotMinutes { get; set; }
}

public class DoctorDetailsDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string SpecializationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public string? RoomNumber { get; set; }
    public string? ProfileImage { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsAvailableToday { get; set; }
    public List<DoctorScheduleDto> Schedules { get; set; } = new();
    public List<DateOnly> VacationDates { get; set; } = new();
}

public class UpdateDoctorProfileDto
{
    public int DoctorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int SpecializationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public string? RoomNumber { get; set; }
    public string? ProfileImagePath { get; set; }
}

public class DoctorDirectoryStatsDto
{
    public int TotalDoctors { get; set; }
    public int AvailableTodayCount { get; set; }
    public int SpecialtiesCount { get; set; }
}
