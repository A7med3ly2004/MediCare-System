using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Account;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Doctors;

public class DoctorService : IDoctorService
{
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DoctorService> _logger;

    public DoctorService(
        IUnitOfWork uow,
        ApplicationDbContext dbContext,
        ILogger<DoctorService> logger)
    {
        _uow = uow;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResult<DoctorListItemDto>> SearchDoctorsAsync(DoctorFilterDto filter, CancellationToken ct = default)
    {
        // Base query: Only active, verified, non-deleted doctors with complete profiles
        var query = _dbContext.Doctors
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays)
            .Where(d => !d.IsDeleted
                     && d.User != null
                     && !d.User.IsDeleted
                     && d.User.IsEmailVerified
                     && !d.User.IsSuspended
                     && d.SpecializationId > 0
                     && !string.IsNullOrWhiteSpace(d.Location)
                     && d.ConsultationFee >= 0);

        // Filter: Specialization
        if (filter.SpecializationId.HasValue && filter.SpecializationId.Value > 0)
        {
            query = query.Where(d => d.SpecializationId == filter.SpecializationId.Value);
        }

        // Filter: Location keyword
        if (!string.IsNullOrWhiteSpace(filter.LocationKeyword))
        {
            var loc = filter.LocationKeyword.Trim().ToLower();
            query = query.Where(d => d.Location.ToLower().Contains(loc));
        }

        // Filter: General search term (name, specialization name, location)
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(d =>
                d.User.FullName.ToLower().Contains(term) ||
                (d.Specialization != null && d.Specialization.Name.ToLower().Contains(term)) ||
                d.Location.ToLower().Contains(term));
        }

        var doctorsList = await query.ToListAsync(ct);

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var targetDate = filter.AvailableOn ?? today;

        // Efficient single-query retrieval of active appointments for all candidate doctors on relevant dates
        var candidateDoctorIds = doctorsList.Select(d => d.Id).ToList();
        var relevantDates = new List<DateOnly> { today };
        if (filter.AvailableOn.HasValue && filter.AvailableOn.Value != today)
        {
            relevantDates.Add(filter.AvailableOn.Value);
        }

        var activeAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => candidateDoctorIds.Contains(a.DoctorId)
                     && relevantDates.Contains(a.AppointmentDate)
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Rejected)
            .ToListAsync(ct);

        var apptsByDoctorAndDate = activeAppointments
            .GroupBy(a => (a.DoctorId, a.AppointmentDate))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Compute real free-slot availability for today and the filter date (Task 4)
        var doctorAvailability = doctorsList.ToDictionary(
            d => d.Id,
            d =>
            {
                apptsByDoctorAndDate.TryGetValue((d.Id, today), out var todayAppts);
                todayAppts ??= new List<Appointment>();
                var isAvailToday = SlotCalculator.HasAnyFreeSlot(d.Schedules, d.VacationDays, todayAppts, today, clinicNow);

                bool isAvailTarget;
                if (!filter.AvailableOn.HasValue || filter.AvailableOn.Value == today)
                {
                    isAvailTarget = isAvailToday;
                }
                else
                {
                    apptsByDoctorAndDate.TryGetValue((d.Id, filter.AvailableOn.Value), out var targetAppts);
                    targetAppts ??= new List<Appointment>();
                    isAvailTarget = SlotCalculator.HasAnyFreeSlot(d.Schedules, d.VacationDays, targetAppts, filter.AvailableOn.Value, clinicNow);
                }

                return (AvailableToday: isAvailToday, AvailableTarget: isAvailTarget);
            });

        // Filter: Availability on chosen date (matches only if at least one free slot exists)
        if (filter.AvailableOn.HasValue)
        {
            doctorsList = doctorsList
                .Where(d => doctorAvailability[d.Id].AvailableTarget)
                .ToList();
        }

        var totalCount = doctorsList.Count;
        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 6;

        var pagedDoctors = doctorsList
            .OrderBy(d => d.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d =>
            {
                var avail = doctorAvailability[d.Id];

                // Build consulting hours summary string (e.g. "09:00–15:00" or first schedule)
                var hoursSummary = "By appointment";
                var daySched = d.Schedules.FirstOrDefault(s => s.DayOfWeek == (int)targetDate.DayOfWeek);
                if (daySched != null)
                {
                    hoursSummary = $"{daySched.StartTime:hh\\:mm}–{daySched.EndTime:hh\\:mm}";
                }
                else if (d.Schedules.Any())
                {
                    var first = d.Schedules.OrderBy(s => s.DayOfWeek).First();
                    hoursSummary = $"{first.StartTime:hh\\:mm}–{first.EndTime:hh\\:mm}";
                }

                return new DoctorListItemDto
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    FullName = d.User.FullName,
                    SpecializationName = d.Specialization?.Name ?? "General Practice",
                    Location = d.Location,
                    ConsultationFee = d.ConsultationFee,
                    RoomNumber = d.RoomNumber,
                    ProfileImage = d.ProfileImage,
                    Email = d.User.Email ?? string.Empty,
                    PhoneNumber = d.User.PhoneNumber,
                    ConsultingHoursSummary = hoursSummary,
                    IsAvailableToday = avail.AvailableToday,
                    IsAvailableOnFilterDate = avail.AvailableTarget
                };
            })
            .ToList();

        return new PagedResult<DoctorListItemDto>
        {
            Items = pagedDoctors,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DoctorDirectoryStatsDto> GetDirectoryStatsAsync(CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);

        var activeDoctors = await _dbContext.Doctors
            .Include(d => d.User)
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays)
            .Where(d => !d.IsDeleted
                     && d.User != null
                     && !d.User.IsDeleted
                     && d.User.IsEmailVerified
                     && !d.User.IsSuspended
                     && d.SpecializationId > 0
                     && !string.IsNullOrWhiteSpace(d.Location)
                     && d.ConsultationFee >= 0)
            .ToListAsync(ct);

        var total = activeDoctors.Count;
        var doctorIds = activeDoctors.Select(d => d.Id).ToList();

        var todayAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => doctorIds.Contains(a.DoctorId)
                     && a.AppointmentDate == today
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Rejected)
            .ToListAsync(ct);

        var apptsByDoctor = todayAppointments
            .GroupBy(a => a.DoctorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var availableToday = activeDoctors.Count(d =>
        {
            apptsByDoctor.TryGetValue(d.Id, out var docAppts);
            docAppts ??= new List<Appointment>();
            return SlotCalculator.HasAnyFreeSlot(d.Schedules, d.VacationDays, docAppts, today, clinicNow);
        });

        var specialties = await _dbContext.Specializations
            .Where(s => !s.IsDeleted)
            .CountAsync(ct);

        return new DoctorDirectoryStatsDto
        {
            TotalDoctors = total,
            AvailableTodayCount = availableToday,
            SpecialtiesCount = specialties
        };
    }

    public async Task<DoctorDetailsDto?> GetDoctorDetailsAsync(int id, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

        if (doctor == null || doctor.User == null || doctor.User.IsDeleted || doctor.User.IsSuspended || !doctor.User.IsEmailVerified)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new DoctorDetailsDto
        {
            Id = doctor.Id,
            UserId = doctor.UserId,
            FullName = doctor.User.FullName,
            SpecializationId = doctor.SpecializationId,
            SpecializationName = doctor.Specialization?.Name ?? "General Practice",
            Location = doctor.Location,
            ConsultationFee = doctor.ConsultationFee,
            RoomNumber = doctor.RoomNumber,
            ProfileImage = doctor.ProfileImage,
            Email = doctor.User.Email ?? string.Empty,
            PhoneNumber = doctor.User.PhoneNumber,
            IsAvailableToday = IsDoctorAvailableOnDate(doctor, today),
            Schedules = doctor.Schedules
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new DoctorScheduleDto
                {
                    DayOfWeek = s.DayOfWeek,
                    DayName = ((DayOfWeek)s.DayOfWeek).ToString(),
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    SlotMinutes = s.SlotMinutes
                })
                .ToList(),
            VacationDates = doctor.VacationDays
                .OrderBy(v => v.VacationDate)
                .Select(v => v.VacationDate)
                .ToList()
        };
    }

    public async Task<DoctorDetailsDto?> GetDoctorByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays)
            .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted, ct);

        if (doctor == null)
            return null;

        return await GetDoctorDetailsAsync(doctor.Id, ct);
    }

    public async Task<ServiceResult> UpdateDoctorProfileAsync(UpdateDoctorProfileDto dto, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == dto.DoctorId && !d.IsDeleted, ct);

        if (doctor == null)
            return ServiceResult.Fail("Doctor profile not found.");

        // Strict server-side ownership enforcement
        if (!string.Equals(doctor.UserId, dto.UserId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Security alert: User {UserId} attempted to edit doctor {DoctorId} owned by {OwnerId}",
                dto.UserId, dto.DoctorId, doctor.UserId);
            return ServiceResult.Fail("Forbidden: You are not authorized to edit this doctor profile.");
        }

        // Validation
        if (dto.ConsultationFee < 0)
            return ServiceResult.Fail("Consultation fee must be non-negative.");

        if (string.IsNullOrWhiteSpace(dto.Location))
            return ServiceResult.Fail("Location is required.");

        var specExists = await _dbContext.Specializations
            .AnyAsync(s => s.Id == dto.SpecializationId && !s.IsDeleted, ct);

        if (!specExists)
            return ServiceResult.Fail("Selected specialization is invalid or no longer active.");

        // Apply profile updates
        doctor.SpecializationId = dto.SpecializationId;
        doctor.Location = dto.Location.Trim();
        doctor.ConsultationFee = dto.ConsultationFee;
        doctor.RoomNumber = string.IsNullOrWhiteSpace(dto.RoomNumber) ? null : dto.RoomNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.ProfileImagePath))
        {
            doctor.ProfileImage = dto.ProfileImagePath;
        }

        if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            doctor.User.FullName = dto.FullName.Trim();
        }

        doctor.User.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Doctor {DoctorId} ({FullName}) updated profile successfully.", doctor.Id, doctor.User.FullName);

        return ServiceResult.Ok();
    }

    /// <summary>
    /// Checks if doctor is available on a specific date based on working hours and vacations.
    /// Dedicated method as required by Step 3; Task 4 will refine this with appointment slot checks.
    /// </summary>
    public bool IsDoctorAvailableOnDate(Doctor doctor, DateOnly date)
    {
        if (doctor == null) return false;

        var dayOfWeek = (int)date.DayOfWeek;

        // Check if doctor has a schedule on this day of week
        var hasSchedule = doctor.Schedules.Any(s => s.DayOfWeek == dayOfWeek);
        if (!hasSchedule) return false;

        // Check if doctor is on vacation on this date
        var onVacation = doctor.VacationDays.Any(v => v.VacationDate == date);
        if (onVacation) return false;

        return true;
    }

    public async Task<bool> CheckDoctorAvailabilityAsync(int doctorId, DateOnly date, CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        if (date < today) return false;

        var doctor = await _dbContext.Doctors
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays)
            .FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct);

        if (doctor == null) return false;

        var dayAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                     && a.AppointmentDate == date
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Rejected)
            .ToListAsync(ct);

        return SlotCalculator.HasAnyFreeSlot(doctor.Schedules, doctor.VacationDays, dayAppts, date, clinicNow);
    }
}
