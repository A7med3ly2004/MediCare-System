using System.Security.Claims;
using MediCare.Data;
using MediCare.Services.Doctors;
using MediCare.Services.Interfaces;
using MediCare.Web.ViewModels.Doctors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize]
public class DoctorsController : Controller
{
    private readonly IDoctorService _doctorService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DoctorsController> _logger;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 2MB

    public DoctorsController(
        IDoctorService doctorService,
        ApplicationDbContext dbContext,
        IWebHostEnvironment env,
        ILogger<DoctorsController> logger)
    {
        _doctorService = doctorService;
        _dbContext = dbContext;
        _env = env;
        _logger = logger;
    }

    // ── GET: /Doctors ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(
        int? specializationId,
        string? locationKeyword,
        string? searchTerm,
        DateOnly? availableOn,
        int page = 1)
    {
        ViewData["Title"] = "Our Doctors";
        ViewData["PortalLabel"] = User.IsInRole("Admin") ? "Admin portal" : (User.IsInRole("Doctor") ? "Doctor portal" : "Patient portal");
        ViewData["PageLabel"] = "Doctors directory";
        ViewData["ActiveNav"] = "doctors";

        var filter = new DoctorFilterDto
        {
            SpecializationId = specializationId,
            LocationKeyword = locationKeyword,
            SearchTerm = searchTerm,
            AvailableOn = availableOn,
            Page = page,
            PageSize = 6
        };

        var pagedDoctors = await _doctorService.SearchDoctorsAsync(filter);
        var stats = await _doctorService.GetDirectoryStatsAsync();
        var specializations = await GetSpecializationsListAsync();

        var viewModel = new DoctorIndexViewModel
        {
            SpecializationId = specializationId,
            LocationKeyword = locationKeyword,
            SearchTerm = searchTerm,
            AvailableOn = availableOn,
            Page = page,
            Doctors = pagedDoctors,
            Stats = stats,
            Specializations = specializations
        };

        return View(viewModel);
    }

    // ── GET: /Doctors/Details/5 ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Details(int id, DateOnly? date)
    {
        var doctor = await _doctorService.GetDoctorDetailsAsync(id);
        if (doctor == null)
        {
            return NotFound();
        }

        ViewData["Title"] = $"{doctor.FullName} — Profile";
        ViewData["PortalLabel"] = User.IsInRole("Admin") ? "Admin portal" : (User.IsInRole("Doctor") ? "Doctor portal" : "Patient portal");
        ViewData["PageLabel"] = doctor.FullName;
        ViewData["ActiveNav"] = "doctors";

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var canEdit = User.IsInRole("Admin") || string.Equals(currentUserId, doctor.UserId, StringComparison.Ordinal);

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var isAvail = await _doctorService.CheckDoctorAvailabilityAsync(doctor.Id, targetDate);

        // Consulting hours today string
        var todaySched = doctor.Schedules.FirstOrDefault(s => s.DayOfWeek == (int)targetDate.DayOfWeek);
        var hoursStr = todaySched != null
            ? $"{todaySched.StartTime:hh\\:mm}–{todaySched.EndTime:hh\\:mm}"
            : "Off duty";

        var viewModel = new DoctorDetailsViewModel
        {
            Doctor = doctor,
            CanEditProfile = canEdit,
            SelectedDate = targetDate,
            IsAvailableOnSelectedDate = isAvail,
            FormattedConsultingHours = hoursStr
        };

        return View(viewModel);
    }

    // ── GET: /Doctors/Edit ────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Edit(int? id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        DoctorDetailsDto? doctor;
        if (id.HasValue && id.Value > 0)
        {
            doctor = await _doctorService.GetDoctorDetailsAsync(id.Value);
            if (doctor == null) return NotFound();

            // Strict ownership verification: A doctor cannot edit another doctor's profile
            if (!string.Equals(doctor.UserId, currentUserId, StringComparison.Ordinal))
            {
                _logger.LogWarning("Forbidden profile edit attempt: User {UserId} tried to edit Doctor {DoctorId}",
                    currentUserId, id.Value);
                return Forbid();
            }
        }
        else
        {
            doctor = await _doctorService.GetDoctorByUserIdAsync(currentUserId);
            if (doctor == null) return NotFound();
        }

        ViewData["Title"] = "Edit Doctor Profile";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "Edit profile";
        ViewData["ActiveNav"] = "settings";

        var viewModel = new DoctorEditProfileViewModel
        {
            DoctorId = doctor.Id,
            UserId = doctor.UserId,
            FullName = doctor.FullName,
            PhoneNumber = doctor.PhoneNumber,
            SpecializationId = doctor.SpecializationId,
            Location = doctor.Location,
            ConsultationFee = doctor.ConsultationFee,
            RoomNumber = doctor.RoomNumber,
            CurrentProfileImage = doctor.ProfileImage,
            Specializations = await GetSpecializationsListAsync()
        };

        return View(viewModel);
    }

    // ── POST: /Doctors/Edit ───────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DoctorEditProfileViewModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        // Strict server-side ownership check
        if (!string.Equals(model.UserId, currentUserId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Forbidden profile edit POST: User {UserId} does not match model UserId {ModelUserId}",
                currentUserId, model.UserId);
            return Forbid();
        }

        string? profileImagePath = null;

        // Image upload validation (matching registration rules)
        if (model.ProfileImage is not null && model.ProfileImage.Length > 0)
        {
            if (model.ProfileImage.Length > MaxImageSizeBytes)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Profile image must not exceed 2MB.");
            }

            var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext) || !AllowedImageMimeTypes.Contains(model.ProfileImage.ContentType.ToLowerInvariant()))
            {
                ModelState.AddModelError(nameof(model.ProfileImage), "Allowed image formats are JPG, PNG, and WebP.");
            }

            if (ModelState.IsValid)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "doctors");
                Directory.CreateDirectory(uploadsDir);

                var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfileImage.CopyToAsync(stream);
                }

                profileImagePath = $"/uploads/doctors/{uniqueFileName}";
            }
        }

        if (!ModelState.IsValid)
        {
            model.Specializations = await GetSpecializationsListAsync();
            return View(model);
        }

        var dto = new UpdateDoctorProfileDto
        {
            DoctorId = model.DoctorId,
            UserId = model.UserId,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            SpecializationId = model.SpecializationId,
            Location = model.Location,
            ConsultationFee = model.ConsultationFee,
            RoomNumber = model.RoomNumber,
            ProfileImagePath = profileImagePath
        };

        var result = await _doctorService.UpdateDoctorProfileAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Failed to update profile.");
            model.Specializations = await GetSpecializationsListAsync();
            return View(model);
        }

        TempData["Success"] = "Your profile has been updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.DoctorId });
    }

    private async Task<IEnumerable<SelectListItem>> GetSpecializationsListAsync()
    {
        return await _dbContext.Specializations
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name
            })
            .ToListAsync();
    }
}
