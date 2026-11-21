using System.Security.Claims;
using MediCare.Data.Entities;
using MediCare.Services.Admin;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IReportService _reportService;
    private readonly IReportExportService _reportExportService;
    private readonly IAdminManagementService _adminManagementService;
    private readonly IAppointmentService _appointmentService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IReportService reportService,
        IReportExportService reportExportService,
        IAdminManagementService adminManagementService,
        IAppointmentService appointmentService,
        ILogger<AdminController> logger)
    {
        _reportService = reportService;
        _reportExportService = reportExportService;
        _adminManagementService = adminManagementService;
        _appointmentService = appointmentService;
        _logger = logger;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Dashboard Overview (UI/Admin overview.png)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? date, CancellationToken ct)
    {
        ViewData["Title"] = "Admin Overview";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Executive Overview";
        ViewData["ActiveNav"] = "overview";

        var targetDate = ParseDateOrDefault(date);

        var dashboardData = await _reportService.GetAdminDashboardDataAsync(ct);
        var dailyReport = await _reportService.GetDailyReportAsync(targetDate, ct);

        var vm = new AdminDashboardViewModel
        {
            DashboardData = dashboardData,
            DailyReport = dailyReport,
            SelectedDate = targetDate
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Reports & Analytics (UI/Reports and analytics.png)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Reports(string? date, CancellationToken ct)
    {
        ViewData["Title"] = "Reports & Analytics";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Reports & Trends";
        ViewData["ActiveNav"] = "reports";

        var targetDate = ParseDateOrDefault(date);

        var dailyReport = await _reportService.GetDailyReportAsync(targetDate, ct);
        var analytics = await _reportService.GetAnalyticsReportAsync(targetDate, ct);

        var vm = new AdminReportsViewModel
        {
            SelectedDate = targetDate,
            DailyReport = dailyReport,
            Analytics = analytics
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportReport(string? date, string format = "excel", CancellationToken ct = default)
    {
        var targetDate = ParseDateOrDefault(date);
        var report = await _reportService.GetDailyReportAsync(targetDate, ct);

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdfBytes = await _reportExportService.ExportDailyReportToPdfAsync(report, ct);
            return File(pdfBytes, "application/pdf", $"MediCare-DailyReport-{targetDate:yyyyMMdd}.pdf");
        }
        else
        {
            var excelBytes = await _reportExportService.ExportDailyReportToExcelAsync(report, ct);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"MediCare-DailyReport-{targetDate:yyyyMMdd}.xlsx");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Doctors Management
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Doctors(string? search, int? specializationId, bool? isSuspended, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Doctor Directory";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Doctor Management";
        ViewData["ActiveNav"] = "doctors";

        var paging = await _adminManagementService.GetDoctorsAsync(search, specializationId, isSuspended, page, 10, ct);
        var specializations = await _adminManagementService.GetSpecializationsAsync(ct);

        var vm = new AdminDoctorsListViewModel
        {
            Paging = paging,
            Search = search,
            SpecializationId = specializationId,
            IsSuspended = isSuspended,
            Specializations = specializations
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EditDoctor(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Edit Doctor";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Edit Doctor Profile";
        ViewData["ActiveNav"] = "doctors";

        var doctor = await _adminManagementService.GetDoctorByIdAsync(id, ct);
        if (doctor == null) return NotFound();

        var specializations = await _adminManagementService.GetSpecializationsAsync(ct);

        var vm = new AdminEditDoctorViewModel
        {
            Id = doctor.Id,
            FullName = doctor.FullName,
            Email = doctor.Email,
            SpecializationId = doctor.SpecializationId ?? 0,
            Location = doctor.Location,
            RoomNumber = doctor.RoomNumber,
            ConsultationFee = doctor.ConsultationFee,
            AvailableSpecializations = specializations
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDoctor(AdminEditDoctorViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableSpecializations = await _adminManagementService.GetSpecializationsAsync(ct);
            return View(model);
        }

        var dto = new UpdateDoctorAdminDto
        {
            Id = model.Id,
            FullName = model.FullName,
            SpecializationId = model.SpecializationId,
            Location = model.Location,
            RoomNumber = model.RoomNumber,
            ConsultationFee = model.ConsultationFee
        };

        var success = await _adminManagementService.UpdateDoctorAsync(dto, ct);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Failed to update doctor profile. Please verify input data.");
            model.AvailableSpecializations = await _adminManagementService.GetSpecializationsAsync(ct);
            return View(model);
        }

        TempData["Success"] = $"Doctor '{model.FullName}' updated successfully.";
        return RedirectToAction(nameof(Doctors));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDoctorSuspend(int id, CancellationToken ct)
    {
        var success = await _adminManagementService.ToggleDoctorSuspensionAsync(id, ct);
        if (success)
        {
            TempData["Success"] = "Doctor account status toggled successfully.";
        }
        else
        {
            TempData["Error"] = "Unable to find doctor or modify suspension status.";
        }

        return RedirectToAction(nameof(Doctors));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDoctor(int id, CancellationToken ct)
    {
        var (succeeded, error) = await _adminManagementService.DeleteDoctorAsync(id, ct);
        if (succeeded)
        {
            TempData["Success"] = "Doctor account deleted successfully.";
        }
        else
        {
            TempData["Error"] = error ?? "Unable to delete doctor.";
        }

        return RedirectToAction(nameof(Doctors));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Patients Management (UI/Patient management.png)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Patients(string? search, bool? isSuspended, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Patient Registry";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Patient Directory";
        ViewData["ActiveNav"] = "patients";

        var paging = await _adminManagementService.GetPatientsAsync(search, isSuspended, page, 10, ct);

        // Compute summary counts
        var allPatients = await _adminManagementService.GetPatientsAsync(null, null, 1, 10000, ct);
        int total = allPatients.TotalCount;
        int suspended = allPatients.Items.Count(p => p.IsSuspended);
        int active = total - suspended;

        var vm = new AdminPatientsListViewModel
        {
            Paging = paging,
            Search = search,
            IsSuspended = isSuspended,
            TotalCount = total,
            ActiveCount = active,
            SuspendedCount = suspended
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EditPatient(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Edit Patient";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Edit Patient Details";
        ViewData["ActiveNav"] = "patients";

        var patient = await _adminManagementService.GetPatientByIdAsync(id, ct);
        if (patient == null) return NotFound();

        var vm = new AdminEditPatientViewModel
        {
            Id = patient.Id,
            FullName = patient.FullName,
            Email = patient.Email,
            PhoneNumber = patient.PhoneNumber,
            BloodGroup = patient.BloodGroup,
            Allergies = patient.Allergies,
            MedicalHistory = patient.MedicalHistory
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPatient(AdminEditPatientViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var dto = new UpdatePatientAdminDto
        {
            Id = model.Id,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            BloodGroup = model.BloodGroup,
            Allergies = model.Allergies,
            MedicalHistory = model.MedicalHistory
        };

        var success = await _adminManagementService.UpdatePatientAsync(dto, ct);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, "Failed to update patient profile.");
            return View(model);
        }

        TempData["Success"] = $"Patient '{model.FullName}' profile updated.";
        return RedirectToAction(nameof(Patients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePatientSuspend(int id, CancellationToken ct)
    {
        var success = await _adminManagementService.TogglePatientSuspensionAsync(id, ct);
        if (success)
        {
            TempData["Success"] = "Patient account status toggled successfully.";
        }
        else
        {
            TempData["Error"] = "Unable to modify patient status.";
        }

        return RedirectToAction(nameof(Patients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePatient(int id, CancellationToken ct)
    {
        var (succeeded, error) = await _adminManagementService.DeletePatientAsync(id, ct);
        if (succeeded)
        {
            TempData["Success"] = "Patient profile deleted successfully.";
        }
        else
        {
            TempData["Error"] = error ?? "Unable to delete patient.";
        }

        return RedirectToAction(nameof(Patients));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Specializations Management
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Specializations(CancellationToken ct)
    {
        ViewData["Title"] = "Specializations";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Specializations Setup";
        ViewData["ActiveNav"] = "doctors";

        var specs = await _adminManagementService.GetSpecializationsAsync(ct);
        var vm = new AdminSpecializationsViewModel
        {
            Specializations = specs
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSpecialization(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Specialization name is required.";
            return RedirectToAction(nameof(Specializations));
        }

        var success = await _adminManagementService.CreateSpecializationAsync(name, ct);
        if (success)
        {
            TempData["Success"] = $"Specialization '{name}' added successfully.";
        }
        else
        {
            TempData["Error"] = $"Specialization '{name}' already exists or could not be created.";
        }

        return RedirectToAction(nameof(Specializations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSpecialization(int id, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Specialization name is required.";
            return RedirectToAction(nameof(Specializations));
        }

        var success = await _adminManagementService.UpdateSpecializationAsync(id, name, ct);
        if (success)
        {
            TempData["Success"] = "Specialization updated.";
        }
        else
        {
            TempData["Error"] = "Name already in use or specialization not found.";
        }

        return RedirectToAction(nameof(Specializations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSpecialization(int id, CancellationToken ct)
    {
        var (succeeded, error) = await _adminManagementService.DeleteSpecializationAsync(id, ct);
        if (succeeded)
        {
            TempData["Success"] = "Specialization removed.";
        }
        else
        {
            TempData["Error"] = error ?? "Unable to delete specialization.";
        }

        return RedirectToAction(nameof(Specializations));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Appointments Management (UI/Appointment management — day.png)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Appointments(
        string? date, int? doctorId, AppointmentStatus? status, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Appointments";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Appointment Schedule";
        ViewData["ActiveNav"] = "appointments";

        DateOnly? selectedDate = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate)
            ? parsedDate
            : null;

        var paging = await _adminManagementService.GetAppointmentsAsync(selectedDate, doctorId, null, status, page, 10, ct);
        var doctorsPaging = await _adminManagementService.GetDoctorsAsync(null, null, false, 1, 100, ct);

        // Overall summary counts for cards
        var allAppts = await _adminManagementService.GetAppointmentsAsync(selectedDate, null, null, null, 1, 1000, ct);
        int total = allAppts.TotalCount;
        int confirmed = allAppts.Items.Count(a => a.Status == AppointmentStatus.Confirmed);
        int pending = allAppts.Items.Count(a => a.Status == AppointmentStatus.Pending);
        int completed = allAppts.Items.Count(a => a.Status == AppointmentStatus.Completed);

        var vm = new AdminAppointmentsListViewModel
        {
            Paging = paging,
            SelectedDate = selectedDate,
            DoctorId = doctorId,
            Status = status,
            Doctors = doctorsPaging.Items,
            TotalAppointments = total,
            ConfirmedCount = confirmed,
            PendingCount = pending,
            CompletedCount = completed
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmAppointment(int appointmentId, string? rowVersionBase64, CancellationToken ct)
    {
        try
        {
            var dto = new ConfirmRejectDto
            {
                AppointmentId = appointmentId,
                UserId = CurrentUserId,
                UserRole = "Admin",
                RowVersion = !string.IsNullOrEmpty(rowVersionBase64) ? Convert.FromBase64String(rowVersionBase64) : null
            };

            await _appointmentService.ConfirmAsync(dto, ct);
            TempData["Success"] = $"Appointment #{appointmentId} confirmed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin failed to confirm appointment #{Id}.", appointmentId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAppointment(int appointmentId, string? rowVersionBase64, string? reason, CancellationToken ct)
    {
        try
        {
            var dto = new ConfirmRejectDto
            {
                AppointmentId = appointmentId,
                UserId = CurrentUserId,
                UserRole = "Admin",
                RowVersion = !string.IsNullOrEmpty(rowVersionBase64) ? Convert.FromBase64String(rowVersionBase64) : null
            };

            await _appointmentService.RejectAsync(dto, reason, ct);
            TempData["Success"] = $"Appointment #{appointmentId} has been rejected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin failed to reject appointment #{Id}.", appointmentId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(int appointmentId, string? reason, CancellationToken ct)
    {
        try
        {
            var dto = new CancelAppointmentDto
            {
                AppointmentId = appointmentId,
                UserId = CurrentUserId,
                UserRole = "Admin",
                Reason = reason
            };

            await _appointmentService.CancelAsync(dto, ct);
            TempData["Success"] = $"Appointment #{appointmentId} was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin failed to cancel appointment #{Id}.", appointmentId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Appointments));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Notifications Log (FR-28)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Notifications(string? status, string? type, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Notification History";
        ViewData["PortalLabel"] = "Clinic Administration";
        ViewData["PageLabel"] = "Email Logs & Outbox";
        ViewData["ActiveNav"] = "notifications";

        var paging = await _adminManagementService.GetNotificationsAsync(status, type, page, 15, ct);

        var vm = new AdminNotificationsListViewModel
        {
            Paging = paging,
            Status = status,
            Type = type
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendNotification(int id, CancellationToken ct)
    {
        var success = await _adminManagementService.ResendNotificationAsync(id, ct);
        if (success)
        {
            TempData["Success"] = $"Notification #{id} was re-sent successfully.";
        }
        else
        {
            TempData["Error"] = $"Failed to re-send notification #{id}.";
        }

        return RedirectToAction(nameof(Notifications));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Performance Benchmark Seeder (NFR-02)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedVolume(int count = 10000, CancellationToken ct = default)
    {
        var seeded = await _adminManagementService.SeedSampleVolumeAsync(count, ct);
        TempData["Success"] = $"Generated {seeded:N0} sample appointments for performance evaluation.";
        return RedirectToAction(nameof(Reports));
    }

    [HttpGet]
    public IActionResult Settings() => RedirectToAction(nameof(Specializations));

    [HttpGet]
    public IActionResult Schedule() => RedirectToAction(nameof(Appointments));

    private static DateOnly ParseDateOrDefault(string? dateStr)
    {
        if (!string.IsNullOrWhiteSpace(dateStr) && DateOnly.TryParse(dateStr, out var d))
        {
            return d;
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
