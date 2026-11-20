using System.Security.Claims;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using MediCare.Web.ViewModels.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Doctor")]
public class DoctorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IScheduleService _scheduleService;
    private readonly IAppointmentService _appointmentService;
    private readonly ILogger<DoctorController> _logger;

    public DoctorController(
        ApplicationDbContext context,
        IScheduleService scheduleService,
        IAppointmentService appointmentService,
        ILogger<DoctorController> logger)
    {
        _context = context;
        _scheduleService = scheduleService;
        _appointmentService = appointmentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Doctor Dashboard";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "Overview";
        ViewData["ActiveNav"] = "overview";

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Challenge();

        var dashboard = await _appointmentService.GetDoctorDashboardDataAsync(currentUserId);
        return View(dashboard);
    }

    // ── POST: /Doctor/Confirm ─────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int appointmentId, string? rowVersionBase64)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Challenge();

        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return NotFound();

        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var dto = new ConfirmRejectDto
        {
            AppointmentId = appointmentId,
            UserId = currentUserId,
            UserRole = "Doctor",
            DoctorId = doctor.Id,
            RowVersion = rowVersion
        };

        try
        {
            await _appointmentService.ConfirmAsync(dto);
            TempData["Success"] = "Appointment has been confirmed successfully.";
        }
        catch (InvalidStateTransitionException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm appointment {Id}", appointmentId);
            TempData["Error"] = "An error occurred while confirming the appointment.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── POST: /Doctor/Reject ──────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int appointmentId, string? rowVersionBase64, string? reason)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Challenge();

        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return NotFound();

        byte[]? rowVersion = !string.IsNullOrEmpty(rowVersionBase64)
            ? Convert.FromBase64String(rowVersionBase64)
            : null;

        var dto = new ConfirmRejectDto
        {
            AppointmentId = appointmentId,
            UserId = currentUserId,
            UserRole = "Doctor",
            DoctorId = doctor.Id,
            RowVersion = rowVersion
        };

        try
        {
            await _appointmentService.RejectAsync(dto, reason);
            TempData["Success"] = "Appointment has been declined.";
        }
        catch (InvalidStateTransitionException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject appointment {Id}", appointmentId);
            TempData["Error"] = "An error occurred while declining the appointment.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── GET: /Doctor/Schedule ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Schedule()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return NotFound();

        ViewData["Title"] = "My Schedule & Availability";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "My schedule";
        ViewData["ActiveNav"] = "schedule";

        var viewModel = await BuildScheduleViewModelAsync(doctor.Id, doctor.User.FullName);
        return View(viewModel);
    }

    // ── POST: /Doctor/UpdateWorkingHours ───────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWorkingHours(DoctorSchedulePageViewModel model)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return NotFound();

        if (model.DoctorId != doctor.Id)
        {
            _logger.LogWarning("Forbidden schedule update: Doctor {CurrentId} tried to update Doctor {ModelId}",
                doctor.Id, model.DoctorId);
            return Forbid();
        }

        ViewData["Title"] = "My Schedule & Availability";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "My schedule";
        ViewData["ActiveNav"] = "schedule";

        var scheduleDtos = new List<DoctorScheduleItemDto>();

        foreach (var day in model.Days)
        {
            if (!day.IsEnabled || day.Windows == null) continue;

            foreach (var win in day.Windows)
            {
                if (!TimeSpan.TryParse(win.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(win.EndTime, out var endTime))
                {
                    model.ErrorMessage = $"Invalid time format on {day.DayName}.";
                    model.Vacations = await _scheduleService.GetDoctorVacationsAsync(doctor.Id);
                    return View(nameof(Schedule), model);
                }

                scheduleDtos.Add(new DoctorScheduleItemDto
                {
                    DayOfWeek = day.DayOfWeek,
                    DayName = day.DayName,
                    StartTime = startTime,
                    EndTime = endTime,
                    SlotMinutes = win.SlotMinutes
                });
            }
        }

        var updateDto = new UpdateWorkingHoursDto
        {
            DoctorId = doctor.Id,
            UserId = doctor.UserId,
            Schedules = scheduleDtos
        };

        var result = await _scheduleService.UpdateDoctorSchedulesAsync(updateDto);

        if (!result.Succeeded)
        {
            model.ErrorMessage = result.Error;
            model.Conflicts = result.Conflicts;
            model.Vacations = await _scheduleService.GetDoctorVacationsAsync(doctor.Id);
            return View(nameof(Schedule), model);
        }

        TempData["Success"] = "Your weekly working hours have been saved successfully.";
        return RedirectToAction(nameof(Schedule));
    }

    // ── POST: /Doctor/AddVacation ──────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVacation(DoctorSchedulePageViewModel model)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return NotFound();

        ViewData["Title"] = "My Schedule & Availability";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "My schedule";
        ViewData["ActiveNav"] = "schedule";

        if (!model.NewVacation.VacationDate.HasValue)
        {
            model.ErrorMessage = "Please select a valid vacation date.";
            var fullModel = await BuildScheduleViewModelAsync(doctor.Id, doctor.User.FullName);
            fullModel.ErrorMessage = model.ErrorMessage;
            return View(nameof(Schedule), fullModel);
        }

        var addDto = new AddVacationDayDto
        {
            DoctorId = doctor.Id,
            UserId = doctor.UserId,
            VacationDate = model.NewVacation.VacationDate.Value,
            Reason = model.NewVacation.Reason
        };

        var result = await _scheduleService.AddVacationDayAsync(addDto);

        if (!result.Succeeded)
        {
            var fullModel = await BuildScheduleViewModelAsync(doctor.Id, doctor.User.FullName);
            fullModel.ErrorMessage = result.Error;
            fullModel.Conflicts = result.Conflicts;
            return View(nameof(Schedule), fullModel);
        }

        TempData["Success"] = $"Vacation day on {model.NewVacation.VacationDate.Value:yyyy-MM-dd} added successfully.";
        return RedirectToAction(nameof(Schedule));
    }

    // ── POST: /Doctor/RemoveVacation ───────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveVacation(int vacationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var result = await _scheduleService.RemoveVacationDayAsync(vacationId, userId);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Failed to remove vacation day.";
        }
        else
        {
            TempData["Success"] = "Vacation day removed successfully.";
        }

        return RedirectToAction(nameof(Schedule));
    }

    [HttpGet]
    public IActionResult Patients() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult MedicalRecords() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Messages() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Profile() => RedirectToAction("Edit", "Doctors");

    [HttpGet]
    public IActionResult Settings() => RedirectToAction("Edit", "Doctors");

    // ── Helper Methods ────────────────────────────────────────────────────────

    private async Task<Doctor?> GetCurrentDoctorAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return null;

        return await _context.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == currentUserId && !d.IsDeleted);
    }

    private async Task<DoctorSchedulePageViewModel> BuildScheduleViewModelAsync(int doctorId, string doctorName)
    {
        var dbSchedules = await _scheduleService.GetDoctorSchedulesAsync(doctorId);
        var vacations = await _scheduleService.GetDoctorVacationsAsync(doctorId);

        var days = new List<DayScheduleFormModel>();

        // Days of week: Sunday (0) through Saturday (6)
        for (int dayOfWeek = 0; dayOfWeek <= 6; dayOfWeek++)
        {
            var dayName = ((DayOfWeek)dayOfWeek).ToString();
            var daySchedules = dbSchedules.Where(s => s.DayOfWeek == dayOfWeek).ToList();

            var formDay = new DayScheduleFormModel
            {
                DayOfWeek = dayOfWeek,
                DayName = dayName,
                IsEnabled = daySchedules.Any()
            };

            if (daySchedules.Any())
            {
                formDay.Windows = daySchedules.Select(s => new TimeWindowFormModel
                {
                    Id = s.Id,
                    StartTime = $"{s.StartTime.Hours:D2}:{s.StartTime.Minutes:D2}",
                    EndTime = $"{s.EndTime.Hours:D2}:{s.EndTime.Minutes:D2}",
                    SlotMinutes = s.SlotMinutes
                }).ToList();
            }
            else
            {
                // Default window when enabling a day
                formDay.Windows.Add(new TimeWindowFormModel
                {
                    StartTime = "09:00",
                    EndTime = "17:00",
                    SlotMinutes = 30
                });
            }

            days.Add(formDay);
        }

        return new DoctorSchedulePageViewModel
        {
            DoctorId = doctorId,
            DoctorName = doctorName,
            Days = days,
            Vacations = vacations,
            NewVacation = new AddVacationFormModel()
        };
    }
}
