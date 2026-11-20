using System.Security.Claims;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.MedicalRecords;
using MediCare.Web.ViewModels.Appointments;
using MediCare.Web.ViewModels.MedicalRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient")]
public class PatientController : Controller
{
    private readonly IAppointmentService _appointmentService;
    private readonly IDoctorService _doctorService;
    private readonly IMedicalRecordService _medicalRecordService;
    private readonly ILogger<PatientController> _logger;

    public PatientController(
        IAppointmentService appointmentService,
        IDoctorService doctorService,
        IMedicalRecordService medicalRecordService,
        ILogger<PatientController> logger)
    {
        _appointmentService = appointmentService;
        _doctorService = doctorService;
        _medicalRecordService = medicalRecordService;
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

    // ── GET: /Patient/History ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> History()
    {
        ViewData["Title"] = "Medical History";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "Medical history";
        ViewData["ActiveNav"] = "health";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var records = await _medicalRecordService.GetPatientMedicalHistoryAsync(userId);
        return View("History", new PatientHistoryPageViewModel { Records = records });
    }

    [HttpGet]
    public IActionResult Health() => RedirectToAction(nameof(History));

    // ── GET: /Patient/VisitDetails/{id} ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> VisitDetails(int id)
    {
        ViewData["Title"] = "Visit Details";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "Visit details";
        ViewData["ActiveNav"] = "health";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        try
        {
            var record = await _medicalRecordService.GetRecordByIdAsync(id, userId, "Patient");
            if (record == null) return NotFound();
            return View("VisitDetails", record);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ── GET: /Patient/Profile ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        ViewData["Title"] = "My Profile";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "My profile";
        ViewData["ActiveNav"] = "profile";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var profile = await _medicalRecordService.GetPatientProfileAsync(userId);
        if (profile == null) return NotFound();

        var model = new PatientProfileViewModel
        {
            PatientCode = profile.PatientCode,
            FullName = profile.PatientName,
            Email = profile.Email ?? string.Empty,
            DateOfBirth = profile.DateOfBirth,
            PhoneNumber = profile.PhoneNumber,
            BloodGroup = profile.BloodGroup,
            Allergies = profile.Allergies,
            MedicalHistory = profile.MedicalHistory
        };

        return View("Profile", model);
    }

    // ── POST: /Patient/Profile ────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(PatientProfileViewModel model)
    {
        ViewData["Title"] = "My Profile";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "My profile";
        ViewData["ActiveNav"] = "profile";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        var dto = new UpdatePatientProfileDto
        {
            PatientUserId = userId,
            PhoneNumber = model.PhoneNumber,
            BloodGroup = model.BloodGroup,
            Allergies = model.Allergies,
            MedicalHistory = model.MedicalHistory
        };

        try
        {
            var updated = await _medicalRecordService.UpdatePatientProfileAsync(dto);
            if (!updated) return NotFound();

            TempData["Success"] = "Your care information and profile details have been updated successfully.";
            return RedirectToAction(nameof(Profile));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Profile", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile for patient user {UserId}", userId);
            ModelState.AddModelError(string.Empty, "An error occurred while saving your profile. Please try again.");
            return View("Profile", model);
        }
    }

    [HttpGet]
    public IActionResult Notifications() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Settings() => RedirectToAction(nameof(Profile));
}
