using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Account;
using MediCare.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Schedule;

public class ScheduleService : IScheduleService
{
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        IUnitOfWork uow,
        ApplicationDbContext dbContext,
        ILogger<ScheduleService> logger)
    {
        _uow = uow;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<DoctorScheduleItemDto>> GetDoctorSchedulesAsync(int doctorId, CancellationToken ct = default)
    {
        var schedules = await _dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(s => s.DoctorId == doctorId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return schedules.Select(s => new DoctorScheduleItemDto
        {
            Id = s.Id,
            DayOfWeek = s.DayOfWeek,
            DayName = ((DayOfWeek)s.DayOfWeek).ToString(),
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            SlotMinutes = s.SlotMinutes
        }).ToList();
    }

    public async Task<List<VacationDayDto>> GetDoctorVacationsAsync(int doctorId, CancellationToken ct = default)
    {
        var vacations = await _dbContext.VacationDays
            .AsNoTracking()
            .Where(v => v.DoctorId == doctorId)
            .OrderBy(v => v.VacationDate)
            .ToListAsync(ct);

        return vacations.Select(v => new VacationDayDto
        {
            Id = v.Id,
            DoctorId = v.DoctorId,
            VacationDate = v.VacationDate,
            Reason = v.Reason
        }).ToList();
    }

    public async Task<ScheduleOperationResult> UpdateDoctorSchedulesAsync(UpdateWorkingHoursDto dto, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == dto.DoctorId && !d.IsDeleted, ct);

        if (doctor == null)
            return ScheduleOperationResult.Fail("Doctor not found.");

        if (!string.Equals(doctor.UserId, dto.UserId, StringComparison.Ordinal))
            return ScheduleOperationResult.Fail("Forbidden: You are not authorized to update this doctor's schedule.");

        // Validation 1: Times and slot duration
        foreach (var item in dto.Schedules)
        {
            if (item.EndTime <= item.StartTime)
            {
                return ScheduleOperationResult.Fail($"End time must be after start time for {((DayOfWeek)item.DayOfWeek)}.");
            }

            if (item.SlotMinutes <= 0 || item.SlotMinutes > 240)
            {
                return ScheduleOperationResult.Fail($"Slot duration must be between 1 and 240 minutes.");
            }

            var totalMinutes = (int)(item.EndTime - item.StartTime).TotalMinutes;
            if (totalMinutes % item.SlotMinutes != 0)
            {
                return ScheduleOperationResult.Fail(
                    $"Slot length ({item.SlotMinutes}m) must divide evenly into the working hours window ({item.StartTime:hh\\:mm}–{item.EndTime:hh\\:mm}) on {((DayOfWeek)item.DayOfWeek)}.");
            }
        }

        // Validation 2: No overlapping windows on the same day
        var grouped = dto.Schedules.GroupBy(s => s.DayOfWeek);
        foreach (var group in grouped)
        {
            var windows = group.OrderBy(s => s.StartTime).ToList();
            for (int i = 0; i < windows.Count - 1; i++)
            {
                if (windows[i].EndTime > windows[i + 1].StartTime)
                {
                    return ScheduleOperationResult.Fail(
                        $"Overlapping working hours windows detected on {((DayOfWeek)group.Key)}: ({windows[i].StartTime:hh\\:mm}–{windows[i].EndTime:hh\\:mm}) and ({windows[i + 1].StartTime:hh\\:mm}–{windows[i + 1].EndTime:hh\\:mm}).");
                }
            }
        }

        // Validation 3 (BR-06): Check conflicts with future Pending or Confirmed appointments
        var clinicNow = GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);

        var futureAppointments = await _dbContext.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doctor.Id
                     && a.AppointmentDate >= today
                     && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync(ct);

        var conflicts = new List<ConflictingAppointmentDto>();

        foreach (var appt in futureAppointments)
        {
            var apptDayOfWeek = (int)appt.AppointmentDate.DayOfWeek;
            var dayWindows = dto.Schedules
                .Where(s => s.DayOfWeek == apptDayOfWeek)
                .ToList();

            // Find if appointment falls completely inside one of the proposed windows
            var matchingWindow = dayWindows.FirstOrDefault(w =>
                w.StartTime <= appt.StartTime && w.EndTime >= appt.EndTime);

            if (matchingWindow == null)
            {
                conflicts.Add(new ConflictingAppointmentDto
                {
                    AppointmentId = appt.Id,
                    AppointmentDate = appt.AppointmentDate,
                    StartTime = appt.StartTime,
                    EndTime = appt.EndTime,
                    PatientName = appt.Patient?.User?.FullName ?? "Patient",
                    PatientCode = appt.Patient?.PatientCode ?? string.Empty,
                    Status = appt.Status,
                    ConflictReason = "Falls outside proposed working hours."
                });
            }
            else
            {
                // Verify alignment with proposed slot duration
                var offsetMinutes = (int)(appt.StartTime - matchingWindow.StartTime).TotalMinutes;
                var apptDuration = (int)(appt.EndTime - appt.StartTime).TotalMinutes;

                if (offsetMinutes % matchingWindow.SlotMinutes != 0 || apptDuration != matchingWindow.SlotMinutes)
                {
                    conflicts.Add(new ConflictingAppointmentDto
                    {
                        AppointmentId = appt.Id,
                        AppointmentDate = appt.AppointmentDate,
                        StartTime = appt.StartTime,
                        EndTime = appt.EndTime,
                        PatientName = appt.Patient?.User?.FullName ?? "Patient",
                        PatientCode = appt.Patient?.PatientCode ?? string.Empty,
                        Status = appt.Status,
                        ConflictReason = $"Does not align with proposed slot length ({matchingWindow.SlotMinutes} mins)."
                    });
                }
            }
        }

        if (conflicts.Any())
        {
            _logger.LogWarning("BR-06 Conflict: Doctor {DoctorId} attempted to change schedule, conflicting with {Count} appointments.",
                doctor.Id, conflicts.Count);

            return ScheduleOperationResult.Conflict(
                "Cannot change working hours because the new schedule conflicts with existing Pending or Confirmed appointments.",
                conflicts);
        }

        // Apply schedule change inside one transaction
        var existingSchedules = await _dbContext.DoctorSchedules
            .Where(s => s.DoctorId == doctor.Id)
            .ToListAsync(ct);

        _dbContext.DoctorSchedules.RemoveRange(existingSchedules);

        foreach (var item in dto.Schedules)
        {
            _dbContext.DoctorSchedules.Add(new DoctorSchedule
            {
                DoctorId = doctor.Id,
                DayOfWeek = item.DayOfWeek,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                SlotMinutes = item.SlotMinutes
            });
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Doctor {DoctorId} updated working hours successfully.", doctor.Id);

        return ScheduleOperationResult.Ok();
    }

    public async Task<ScheduleOperationResult> AddVacationDayAsync(AddVacationDayDto dto, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == dto.DoctorId && !d.IsDeleted, ct);

        if (doctor == null)
            return ScheduleOperationResult.Fail("Doctor not found.");

        if (!string.Equals(doctor.UserId, dto.UserId, StringComparison.Ordinal))
            return ScheduleOperationResult.Fail("Forbidden: You are not authorized to add vacation for this doctor.");

        var clinicNow = GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);

        if (dto.VacationDate < today)
        {
            return ScheduleOperationResult.Fail("Vacation date cannot be in the past.");
        }

        var exists = await _dbContext.VacationDays
            .AnyAsync(v => v.DoctorId == doctor.Id && v.VacationDate == dto.VacationDate, ct);

        if (exists)
        {
            return ScheduleOperationResult.Fail($"A vacation day is already recorded for {dto.VacationDate:yyyy-MM-dd}.");
        }

        // BR-06: Check for conflicting future appointments on this date
        var conflictingAppts = await _dbContext.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doctor.Id
                     && a.AppointmentDate == dto.VacationDate
                     && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartTime)
            .ToListAsync(ct);

        if (conflictingAppts.Any())
        {
            var conflicts = conflictingAppts.Select(a => new ConflictingAppointmentDto
            {
                AppointmentId = a.Id,
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                PatientName = a.Patient?.User?.FullName ?? "Patient",
                PatientCode = a.Patient?.PatientCode ?? string.Empty,
                Status = a.Status,
                ConflictReason = $"Appointment scheduled on proposed vacation day ({dto.VacationDate:yyyy-MM-dd})."
            }).ToList();

            _logger.LogWarning("BR-06 Conflict: Doctor {DoctorId} attempted to set vacation on {Date}, conflicting with {Count} appointments.",
                doctor.Id, dto.VacationDate, conflicts.Count);

            return ScheduleOperationResult.Conflict(
                $"Cannot add vacation on {dto.VacationDate:yyyy-MM-dd} because there are existing appointments booked on this date.",
                conflicts);
        }

        _dbContext.VacationDays.Add(new VacationDay
        {
            DoctorId = doctor.Id,
            VacationDate = dto.VacationDate,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim()
        });

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Doctor {DoctorId} added vacation day on {Date}.", doctor.Id, dto.VacationDate);

        return ScheduleOperationResult.Ok();
    }

    public async Task<ServiceResult> RemoveVacationDayAsync(int vacationId, string userId, CancellationToken ct = default)
    {
        var vacation = await _dbContext.VacationDays
            .Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == vacationId, ct);

        if (vacation == null)
            return ServiceResult.Fail("Vacation day not found.");

        if (!string.Equals(vacation.Doctor.UserId, userId, StringComparison.Ordinal))
            return ServiceResult.Fail("Forbidden: You are not authorized to remove this vacation day.");

        _dbContext.VacationDays.Remove(vacation);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Doctor {DoctorId} removed vacation day {Date}.", vacation.DoctorId, vacation.VacationDate);
        return ServiceResult.Ok();
    }

    public async Task<List<FreeSlotDto>> GetFreeSlotsAsync(int doctorId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var schedules = await _dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(s => s.DoctorId == doctorId)
            .ToListAsync(ct);

        if (!schedules.Any()) return new List<FreeSlotDto>();

        var vacationDays = await _dbContext.VacationDays
            .AsNoTracking()
            .Where(v => v.DoctorId == doctorId && v.VacationDate >= fromDate && v.VacationDate <= toDate)
            .ToListAsync(ct);

        var activeAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                     && a.AppointmentDate >= fromDate
                     && a.AppointmentDate <= toDate
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Rejected)
            .ToListAsync(ct);

        var clinicNow = GetClinicLocalNow();

        return SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays,
            activeAppointments,
            fromDate,
            toDate,
            clinicNow);
    }

    public async Task<bool> HasFreeSlotsOnDateAsync(int doctorId, DateOnly date, CancellationToken ct = default)
    {
        var clinicNow = GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        if (date < today) return false;

        var dayOfWeek = (int)date.DayOfWeek;

        var schedules = await _dbContext.DoctorSchedules
            .AsNoTracking()
            .Where(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);

        if (!schedules.Any()) return false;

        var isVacation = await _dbContext.VacationDays
            .AsNoTracking()
            .AnyAsync(v => v.DoctorId == doctorId && v.VacationDate == date, ct);

        if (isVacation) return false;

        var activeAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                     && a.AppointmentDate == date
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.Rejected)
            .ToListAsync(ct);

        return SlotCalculator.HasAnyFreeSlot(
            schedules,
            Enumerable.Empty<VacationDay>(),
            activeAppointments,
            date,
            clinicNow);
    }

    public static DateTime GetClinicLocalNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            // Fallback: Cairo is GMT+2
            return DateTime.UtcNow.AddHours(2);
        }
    }
}
