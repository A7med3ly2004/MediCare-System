using System.Security.Claims;
using MediCare.Data;
using MediCare.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Services;

public class CurrentUserProfile
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Initial { get; set; } = "?";
}

public interface ICurrentUserProfileService
{
    Task<CurrentUserProfile> GetCurrentUserAsync();
}

public class CurrentUserProfileService : ICurrentUserProfileService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public CurrentUserProfileService(
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<CurrentUserProfile> GetCurrentUserAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null || httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return new CurrentUserProfile
            {
                FullName = "Guest",
                DisplayName = "Guest",
                RoleLabel = "Guest",
                Initial = "?"
            };
        }

        // Cache per request in HttpContext.Items
        if (httpContext.Items.TryGetValue("CurrentUserProfile", out var cached) && cached is CurrentUserProfile profile)
        {
            return profile;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = httpContext.User.Identity?.Name;

        if (string.IsNullOrEmpty(userId))
        {
            var fallback = new CurrentUserProfile
            {
                FullName = email ?? "User",
                DisplayName = email ?? "User",
                RoleLabel = "User",
                Initial = !string.IsNullOrEmpty(email) ? email.Substring(0, 1).ToUpper() : "?"
            };
            httpContext.Items["CurrentUserProfile"] = fallback;
            return fallback;
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var userFullName = user?.FullName;

        CurrentUserProfile result;

        if (httpContext.User.IsInRole("Admin"))
        {
            var name = !string.IsNullOrWhiteSpace(userFullName) ? userFullName : "Clinic Administrator";
            result = new CurrentUserProfile
            {
                FullName = name,
                DisplayName = name,
                RoleLabel = "Clinic administrator",
                Initial = GetInitial(name)
            };
        }
        else if (httpContext.User.IsInRole("Doctor"))
        {
            var doctor = await _context.Doctors.AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

            var rawName = !string.IsNullOrWhiteSpace(userFullName) ? userFullName : "Doctor";
            var formattedName = DoctorNameFormatter.Format(rawName);

            result = new CurrentUserProfile
            {
                FullName = rawName,
                DisplayName = formattedName,
                RoleLabel = "Doctor",
                ProfileImageUrl = !string.IsNullOrWhiteSpace(doctor?.ProfileImage) ? doctor.ProfileImage : null,
                Initial = GetInitial(rawName)
            };
        }
        else if (httpContext.User.IsInRole("Patient"))
        {
            var name = !string.IsNullOrWhiteSpace(userFullName) ? userFullName : (email ?? "Patient");
            result = new CurrentUserProfile
            {
                FullName = name,
                DisplayName = name,
                RoleLabel = "Patient",
                Initial = GetInitial(name)
            };
        }
        else
        {
            var fallbackName = !string.IsNullOrWhiteSpace(userFullName) ? userFullName : (email ?? "User");
            result = new CurrentUserProfile
            {
                FullName = fallbackName,
                DisplayName = fallbackName,
                RoleLabel = "User",
                Initial = GetInitial(fallbackName)
            };
        }

        httpContext.Items["CurrentUserProfile"] = result;
        return result;
    }

    public static string GetInitial(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var trimmed = name.Trim();
        while (trimmed.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(7).Trim();
            else if (trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(3).Trim();
            else if (trimmed.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(3).Trim();
        }
        return !string.IsNullOrWhiteSpace(trimmed) ? trimmed.Substring(0, 1).ToUpper() : "?";
    }
}
