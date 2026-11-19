using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Account;
using MediCare.Services.Interfaces;
using MediCare.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly IOtpService _otpService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 2MB

    public AccountController(
        IAccountService accountService,
        IOtpService otpService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IWebHostEnvironment env,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _otpService = otpService;
        _signInManager = signInManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _env = env;
        _logger = logger;
    }

    // ── Role Choice / Register entry ──────────────────────────────────────────

    [HttpGet]
    public IActionResult Register() => RedirectToAction(nameof(RoleChoice));

    [HttpGet]
    public IActionResult RoleChoice()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        return View();
    }

    // ── Register Patient ──────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult RegisterPatient()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        return View(new RegisterPatientViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPatient(RegisterPatientViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        if (!ModelState.IsValid)
            return View(model);

        var dto = new RegisterPatientDto
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            Password = model.Password,
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            DateOfBirth = model.DateOfBirth,
            BloodGroup = string.IsNullOrWhiteSpace(model.BloodGroup) ? null : model.BloodGroup.Trim(),
            Allergies = string.IsNullOrWhiteSpace(model.Allergies) ? null : model.Allergies.Trim(),
            MedicalHistory = string.IsNullOrWhiteSpace(model.MedicalHistory) ? null : model.MedicalHistory.Trim(),
            AcceptedPrivacyNotice = model.AcceptedPrivacyNotice
        };

        var result = await _accountService.RegisterPatientAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed. Please check your information.");
            return View(model);
        }

        return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
    }

    // ── Register Doctor ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> RegisterDoctor()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        var model = new RegisterDoctorViewModel
        {
            Specializations = await GetSpecializationsListAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterDoctor(RegisterDoctorViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        string? profileImagePath = null;

        // Validate and save profile image if provided
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

        var dto = new RegisterDoctorDto
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            Password = model.Password,
            SpecializationId = model.SpecializationId,
            Location = model.Location.Trim(),
            ConsultationFee = model.ConsultationFee,
            RoomNumber = string.IsNullOrWhiteSpace(model.RoomNumber) ? null : model.RoomNumber.Trim(),
            ProfileImagePath = profileImagePath,
            AcceptedPrivacyNotice = model.AcceptedPrivacyNotice
        };

        var result = await _accountService.RegisterDoctorAsync(dto);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed. Please check your information.");
            model.Specializations = await GetSpecializationsListAsync();
            return View(model);
        }

        return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
    }

    // ── Registration Pending (Email OTP verification placeholder) ─────────────

    [HttpGet]
    public IActionResult RegisterPending(string? email)
    {
        ViewBag.Email = email ?? "your email address";
        return View();
    }

    [HttpGet]
    public IActionResult VerifyEmail(string? email) => RedirectToAction(nameof(VerifyOtp), new { email });

    // ── Email OTP Verification ────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> VerifyOtp(string? email)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        var model = new VerifyOtpViewModel
        {
            Email = email?.Trim() ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is not null)
            {
                if (user.IsEmailVerified)
                {
                    TempData["InfoMessage"] = "Your email address is already verified. Please sign in.";
                    return RedirectToAction(nameof(Login));
                }

                var latestCode = await _dbContext.OtpCodes
                    .Where(o => o.UserId == user.Id)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestCode is not null)
                {
                    var cooldownSec = 60;
                    var elapsed = DateTime.UtcNow - latestCode.CreatedAt;
                    if (elapsed.TotalSeconds < cooldownSec)
                    {
                        model.CooldownSecondsRemaining = (int)Math.Ceiling(cooldownSec - elapsed.TotalSeconds);
                    }
                }
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        if (!ModelState.IsValid)
            return View(model);

        var result = await _otpService.VerifyOtpByEmailAsync(model.Email.Trim().ToLowerInvariant(), model.Code.Trim());

        switch (result.Status)
        {
            case OtpStatus.Success:
                TempData["SuccessMessage"] = "Your email has been verified successfully. Please sign in to continue.";
                return RedirectToAction(nameof(Login));

            case OtpStatus.WrongCode:
                ModelState.AddModelError(nameof(model.Code),
                    $"Invalid verification code. You have {result.AttemptsLeft} attempt(s) remaining.");
                return View(model);

            case OtpStatus.Expired:
                ModelState.AddModelError(string.Empty,
                    "This verification code has expired. Please request a new code below.");
                return View(model);

            case OtpStatus.Locked:
                ModelState.AddModelError(string.Empty,
                    "Too many wrong attempts. This code has been locked. Please request a new code below.");
                return View(model);

            case OtpStatus.AlreadyVerified:
                TempData["InfoMessage"] = "Your email is already verified. Please sign in.";
                return RedirectToAction(nameof(Login));

            case OtpStatus.UserNotFound:
            default:
                ModelState.AddModelError(string.Empty, "Unable to verify. Please check the email address or request a new code.");
                return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Please provide your email address to resend the code.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var result = await _otpService.SendOtpByEmailAsync(normalizedEmail);

        switch (result.Status)
        {
            case OtpStatus.Success:
                TempData["SuccessMessage"] = "A new 6-digit verification code has been sent to your email.";
                break;

            case OtpStatus.CooldownActive:
                TempData["ErrorMessage"] = $"Please wait {result.SecondsRemaining} seconds before requesting a new code.";
                break;

            case OtpStatus.AlreadyVerified:
                TempData["InfoMessage"] = "Your email address is already verified. Please sign in.";
                return RedirectToAction(nameof(Login));

            case OtpStatus.UserNotFound:
            default:
                TempData["ErrorMessage"] = "If an account exists with this email address, a verification code has been sent.";
                break;
        }

        return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleDashboard();

        if (!ModelState.IsValid)
            return View(model);

        // Run business pre-checks (unverified, suspended, deleted, locked out)
        var preCheck = await _accountService.LoginAsync(model.Email, model.Password);

        if (!preCheck.Succeeded)
        {
            if (preCheck.IsLockedOut)
                ModelState.AddModelError(string.Empty, "Account temporarily locked. Please try again later.");
            else if (preCheck.IsUnverified)
            {
                ViewBag.ShowVerifyLink = true;
                ViewBag.UnverifiedEmail = model.Email;
                ModelState.AddModelError(string.Empty, "Please verify your email address before logging in.");
            }
            else if (preCheck.IsSuspended)
                ModelState.AddModelError(string.Empty, "Your account has been suspended. Please contact the clinic.");
            else
                ModelState.AddModelError(string.Empty, "Invalid email or password.");

            return View(model);
        }

        // Perform actual sign-in with lockoutOnFailure set to true
        var signInResult = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account temporarily locked. Please try again later.");
            return View(model);
        }

        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        _logger.LogInformation("User logged in: {Email} ({Role})", model.Email, preCheck.Role);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToRoleAction(preCheck.Role);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }

    // ── Access Denied ─────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private IActionResult RedirectToRoleDashboard()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Admin");
        if (User.IsInRole("Doctor"))
            return RedirectToAction("Index", "Doctor");

        return RedirectToAction("Index", "Doctors");
    }

    private IActionResult RedirectToRoleAction(string? role)
    {
        return role switch
        {
            "Admin" => RedirectToAction("Index", "Admin"),
            "Doctor" => RedirectToAction("Index", "Doctor"),
            _ => RedirectToAction("Index", "Doctors")
        };
    }

    private async Task<IEnumerable<SelectListItem>> GetSpecializationsListAsync()
    {
        return await _dbContext.Specializations
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name
            })
            .ToListAsync();
    }
}
