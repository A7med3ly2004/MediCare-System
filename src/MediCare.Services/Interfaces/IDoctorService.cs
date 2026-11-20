using MediCare.Data.Entities;
using MediCare.Services.Account;
using MediCare.Services.Doctors;

namespace MediCare.Services.Interfaces;

public interface IDoctorService
{
    /// <summary>
    /// Searches and filters verified, active doctors by specialization, location, date, and keyword with paging.
    /// </summary>
    Task<PagedResult<DoctorListItemDto>> SearchDoctorsAsync(DoctorFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// Gets overall directory statistics (total active doctors, available today, specialties count).
    /// </summary>
    Task<DoctorDirectoryStatsDto> GetDirectoryStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets full doctor details including user info, specialization, weekly schedules and vacation days.
    /// </summary>
    Task<DoctorDetailsDto?> GetDoctorDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets doctor profile for a specific UserId (for the logged-in doctor).
    /// </summary>
    Task<DoctorDetailsDto?> GetDoctorByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Updates the doctor's own profile. Enforces server-side ownership (dto.UserId must match doctor.UserId).
    /// </summary>
    Task<ServiceResult> UpdateDoctorProfileAsync(UpdateDoctorProfileDto dto, CancellationToken ct = default);

    /// <summary>
    /// Availability rule for a specific date: checks weekday schedule and vacation days.
    /// Extracted behind this single method so Task 4 can enhance it with real free slot checking.
    /// </summary>
    bool IsDoctorAvailableOnDate(Doctor doctor, DateOnly date);

    /// <summary>
    /// Async overload to check availability by doctor ID and date.
    /// </summary>
    Task<bool> CheckDoctorAvailabilityAsync(int doctorId, DateOnly date, CancellationToken ct = default);
}
