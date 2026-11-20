using System.Security.Claims;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient")]
public class PatientController : Controller
{
    private readonly IAppointmentService _appointmentService;
    private readonly IDoctorService _doctorService;
    private readonly ILogger<PatientController> _logger;

    public PatientController(
        IAppointmentService appointmentService,
        IDoctorService doctorService,
        ILogger<PatientController> logger)
    {
        _appointmentService = appointmentService;
        _doctorService = doctorService;
        _logger = logger;
    }

    // ── GET: /Patient ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Patient Portal";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "Overview";
        ViewData["ActiveNav"] = "overview";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var dashboard = await _appointmentService.GetPatientDashboardDataAsync(userId);
        return View(dashboard);
    }

    // ── GET: /Patient/Appointments ───────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Appointments(string filter = "all")
    {
        ViewData["Title"] = "My Appointments";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "My appointments";
        ViewData["ActiveNav"] = "appointments";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        bool? upcomingFilter = filter.ToLowerInvariant() switch
        {
            "upcoming" => true,
            "past" => false,
            _ => null
        };

        var appointments = await _appointmentService.GetPatientAppointmentsAsync(userId, upcomingFilter);

        var viewModel = new PatientAppointmentsPageViewModel
        {
            Filter = filter.ToLowerInvariant(),
            Appointments = appointments
        };

        return View(viewModel);
    }

    // ── POST: /Patient/Book ───────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookAppointmentFormModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        if (!TimeSpan.TryParse(model.StartTime, out var startTime) ||
            !TimeSpan.TryParse(model.EndTime, out var endTime))
        {
            TempData["Error"] = "Invalid appointment slot time selected.";
            return RedirectToAction("Details", "Doctors", new { id = model.DoctorId });
        }

        var dto = new BookAppointmentDto
        {
            PatientUserId = userId,
            DoctorId = model.DoctorId,
            AppointmentDate = model.AppointmentDate,
            StartTime = startTime,
            EndTime = endTime,
            Notes = model.Notes
        };

        try
        {
            var result = await _appointmentService.CreateAsync(dto);
            TempData["Success"] = $"Your appointment request with Dr. {result.DoctorName} on {result.AppointmentDate:ddd, dd MMM} at {result.StartTime:hh\\:mm} was received successfully and is Pending confirmation.";
            return RedirectToAction(nameof(Index));
        }
        catch (SlotUnavailableException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Details", "Doctors", new { id = model.DoctorId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to book appointment for user {UserId}", userId);
            TempData["Error"] = "Unable to complete booking request. Please try again.";
            return RedirectToAction("Details", "Doctors", new { id = model.DoctorId });
        }
    }

    // ── POST: /Patient/Cancel ─────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelAppointmentFormModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var dto = new CancelAppointmentDto
        {
            AppointmentId = model.AppointmentId,
            UserId = userId,
            UserRole = "Patient",
            Reason = model.Reason
        };

        try
        {
            await _appointmentService.CancelAsync(dto);
            TempData["Success"] = "Your appointment has been cancelled successfully.";
        }
        catch (LeadTimeViolationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidStateTransitionException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel appointment {Id}", model.AppointmentId);
            TempData["Error"] = "An error occurred while cancelling the appointment.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── POST: /Patient/Reschedule ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(RescheduleAppointmentFormModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        if (!TimeSpan.TryParse(model.NewStartTime, out var startTime) ||
            !TimeSpan.TryParse(model.NewEndTime, out var endTime))
        {
            TempData["Error"] = "Invalid new time slot selected.";
            return RedirectToAction(nameof(Index));
        }

        var dto = new RescheduleAppointmentDto
        {
            AppointmentId = model.AppointmentId,
            PatientUserId = userId,
            NewDate = model.NewDate,
            NewStartTime = startTime,
            NewEndTime = endTime
        };

        try
        {
            var result = await _appointmentService.RescheduleAsync(dto);
            TempData["Success"] = $"Your appointment has been rescheduled to {result.AppointmentDate:ddd, dd MMM} at {result.StartTime:hh\\:mm}. Status returned to Pending.";
        }
        catch (LeadTimeViolationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (SlotUnavailableException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidStateTransitionException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reschedule appointment {Id}", model.AppointmentId);
            TempData["Error"] = "An error occurred while rescheduling the appointment.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Doctors() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Health() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Profile() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Notifications() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Settings() => RedirectToAction(nameof(Index));
}
