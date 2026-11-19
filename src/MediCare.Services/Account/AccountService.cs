using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Account;

/// <summary>
/// Handles registration and login business logic.
/// SignInManager is intentionally NOT injected here: it carries ASP.NET Core hosting
/// dependencies.  The controller calls SignInManager.PasswordSignInAsync directly after
/// this service returns a successful LoginResult.
/// </summary>
public class AccountService : IAccountService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IUnitOfWork uow,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountService> logger)
    {
        _uow = uow;
        _userManager = userManager;
        _logger = logger;
    }

    // ── Register Patient ──────────────────────────────────────────────────────

    public async Task<ServiceResult> RegisterPatientAsync(RegisterPatientDto dto)
    {
        if (!dto.AcceptedPrivacyNotice)
            return ServiceResult.Fail("You must accept the privacy notice to register.");

        // Check email uniqueness before starting a transaction
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            return ServiceResult.Fail("An account with this email already exists.");

        await _uow.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                IsEmailVerified = false,   // must verify via OTP (Task 2B)
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                await _uow.RollbackAsync();
                return ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, "Patient");

            // Generate a unique patient code
            var code = await _uow.Patients.GeneratePatientCodeAsync();

            var patient = new Patient
            {
                UserId = user.Id,
                PatientCode = code,
                PhoneNumber = dto.PhoneNumber,
                DateOfBirth = dto.DateOfBirth,
                BloodGroup = dto.BloodGroup,
                Allergies = dto.Allergies,
                MedicalHistory = dto.MedicalHistory
            };

            await _uow.Patients.AddAsync(patient);
            await _uow.SaveChangesAsync();
            await _uow.CommitAsync();

            _logger.LogInformation("Patient registered: {Email} ({Code})", dto.Email, code);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            _logger.LogError(ex, "Error registering patient {Email}", dto.Email);
            return ServiceResult.Fail("An unexpected error occurred. Please try again.");
        }
    }

    // ── Register Doctor ───────────────────────────────────────────────────────

    public async Task<ServiceResult> RegisterDoctorAsync(RegisterDoctorDto dto)
    {
        if (!dto.AcceptedPrivacyNotice)
            return ServiceResult.Fail("You must accept the privacy notice to register.");

        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            return ServiceResult.Fail("An account with this email already exists.");

        await _uow.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                await _uow.RollbackAsync();
                return ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, "Doctor");

            var doctor = new Doctor
            {
                UserId = user.Id,
                SpecializationId = dto.SpecializationId,
                Location = dto.Location,
                ConsultationFee = dto.ConsultationFee,
                RoomNumber = dto.RoomNumber,
                ProfileImage = dto.ProfileImagePath
            };

            await _uow.Doctors.AddAsync(doctor);
            await _uow.SaveChangesAsync();
            await _uow.CommitAsync();

            _logger.LogInformation("Doctor registered: {Email}", dto.Email);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            _logger.LogError(ex, "Error registering doctor {Email}", dto.Email);
            return ServiceResult.Fail("An unexpected error occurred. Please try again.");
        }
    }

    // ── Login (pre-checks only — actual sign-in is done by SignInManager in the controller) ──

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        // Find user; UserManager respects global query filters by default for IdentityUser
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
            return LoginResult.Fail("Invalid email or password.");

        // Refuse soft-deleted accounts
        if (user.IsDeleted)
            return LoginResult.Deleted();

        // Refuse suspended accounts
        if (user.IsSuspended)
            return LoginResult.Suspended();

        // Refuse unverified accounts
        if (!user.IsEmailVerified)
            return LoginResult.Unverified();

        // Check lockout
        if (await _userManager.IsLockedOutAsync(user))
            return LoginResult.Locked();

        // Check password manually (SignInManager.PasswordSignInAsync is done in controller)
        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            // Increment failed access count for lockout
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
                return LoginResult.Locked();
            return LoginResult.Fail("Invalid email or password.");
        }

        // Reset failed count on success
        await _userManager.ResetAccessFailedCountAsync(user);

        // Get role for redirect
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Patient";

        return LoginResult.Ok(role, user.Id);
    }
}
