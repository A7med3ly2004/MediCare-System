using System.ComponentModel.DataAnnotations;
using MediCare.Services.Schedule;

namespace MediCare.Web.ViewModels.Schedule;

public class TimeWindowFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Start time is required.")]
    public string StartTime { get; set; } = "09:00";

    [Required(ErrorMessage = "End time is required.")]
    public string EndTime { get; set; } = "17:00";

    [Range(5, 240, ErrorMessage = "Slot duration must be between 5 and 240 minutes.")]
    public int SlotMinutes { get; set; } = 30;
}

public class DayScheduleFormModel
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<TimeWindowFormModel> Windows { get; set; } = new();
}

public class AddVacationFormModel
{
    [Required(ErrorMessage = "Vacation date is required.")]
    [Display(Name = "Vacation Date")]
    public DateOnly? VacationDate { get; set; }

    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    [Display(Name = "Reason (optional)")]
    public string? Reason { get; set; }
}

public class DoctorSchedulePageViewModel
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public List<DayScheduleFormModel> Days { get; set; } = new();
    public List<VacationDayDto> Vacations { get; set; } = new();
    public AddVacationFormModel NewVacation { get; set; } = new();
    public List<ConflictingAppointmentDto> Conflicts { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}
