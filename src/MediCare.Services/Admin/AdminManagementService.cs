using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Appointments;
using MediCare.Services.Email;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Admin;

public class AdminManagementService : IAdminManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AdminManagementService> _logger;

    public AdminManagementService(
        IUnitOfWork uow,
        ApplicationDbContext dbContext,
        IEmailSender emailSender,
        ILogger<AdminManagementService> logger)
    {
        _uow = uow;
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Doctors Management
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<DoctorAdminListItemDto>> GetDoctorsAsync(
        string? search, int? specializationId, bool? isSuspended, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var query = _dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(d => d.User.FullName.Contains(s) || (d.User.Email != null && d.User.Email.Contains(s)) || d.Location.Contains(s));
        }

        if (specializationId.HasValue && specializationId.Value > 0)
        {
            query = query.Where(d => d.SpecializationId == specializationId.Value);
        }

        if (isSuspended.HasValue)
        {
            query = query.Where(d => d.User.IsSuspended == isSuspended.Value);
        }

        var total = await query.CountAsync(ct);

        var doctors = await query
            .OrderBy(d => d.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                Doctor = d,
                ActiveCount = _dbContext.Appointments.Count(a =>
                    a.DoctorId == d.Id &&
                    (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
                    (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)))
            })
            .ToListAsync(ct);

        var items = doctors.Select(x => new DoctorAdminListItemDto
        {
            Id = x.Doctor.Id,
            UserId = x.Doctor.UserId,
            FullName = x.Doctor.User.FullName,
            Email = x.Doctor.User.Email ?? string.Empty,
            SpecializationName = x.Doctor.Specialization?.Name ?? "General Practice",
            SpecializationId = x.Doctor.SpecializationId,
            Location = x.Doctor.Location,
            RoomNumber = x.Doctor.RoomNumber,
            ConsultationFee = x.Doctor.ConsultationFee,
            IsSuspended = x.Doctor.User.IsSuspended,
            IsDeleted = x.Doctor.IsDeleted,
            ActiveAppointmentsCount = x.ActiveCount,
            CreatedAt = x.Doctor.User.CreatedAt
        }).ToList();

        return new PagedResult<DoctorAdminListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<DoctorAdminListItemDto?> GetDoctorByIdAsync(int id, CancellationToken ct = default)
    {
        var d = await _dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

        if (d == null) return null;

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var activeCount = await _dbContext.Appointments.CountAsync(a =>
            a.DoctorId == d.Id &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
            (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)), ct);

        return new DoctorAdminListItemDto
        {
            Id = d.Id,
            UserId = d.UserId,
            FullName = d.User.FullName,
            Email = d.User.Email ?? string.Empty,
            SpecializationName = d.Specialization?.Name ?? "General Practice",
            SpecializationId = d.SpecializationId,
            Location = d.Location,
            RoomNumber = d.RoomNumber,
            ConsultationFee = d.ConsultationFee,
            IsSuspended = d.User.IsSuspended,
            IsDeleted = d.IsDeleted,
            ActiveAppointmentsCount = activeCount,
            CreatedAt = d.User.CreatedAt
        };
    }

    public async Task<bool> UpdateDoctorAsync(UpdateDoctorAdminDto dto, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == dto.Id && !d.IsDeleted, ct);

        if (doctor == null) return false;

        doctor.User.FullName = dto.FullName.Trim();
        doctor.SpecializationId = dto.SpecializationId > 0 ? dto.SpecializationId : doctor.SpecializationId;
        doctor.Location = dto.Location.Trim();
        doctor.RoomNumber = string.IsNullOrWhiteSpace(dto.RoomNumber) ? null : dto.RoomNumber.Trim();
        doctor.ConsultationFee = dto.ConsultationFee >= 0 ? dto.ConsultationFee : doctor.ConsultationFee;

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Admin updated Doctor profile {Id}.", dto.Id);
        return true;
    }

    public async Task<bool> ToggleDoctorSuspensionAsync(int id, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

        if (doctor == null) return false;

        doctor.User.IsSuspended = !doctor.User.IsSuspended;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin changed suspension state of Doctor {Id} to {IsSuspended}.", id, doctor.User.IsSuspended);
        return true;
    }

    public async Task<(bool Succeeded, string? Error)> DeleteDoctorAsync(int id, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

        if (doctor == null)
        {
            return (false, "Doctor not found.");
        }

        // BR-07: Check future Pending or Confirmed appointments
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        bool hasFutureAppts = await _dbContext.Appointments.AnyAsync(a =>
            a.DoctorId == id &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
            (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)), ct);

        if (hasFutureAppts)
        {
            return (false, "Cannot delete doctor because they have future pending or confirmed appointments. Please suspend the doctor or reschedule/cancel their appointments first.");
        }

        // Soft delete both Doctor and User profile so they cannot log in
        doctor.IsDeleted = true;
        doctor.User.IsDeleted = true;

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Admin soft-deleted Doctor {Id}.", id);

        return (true, null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patients Management
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<PatientAdminListItemDto>> GetPatientsAsync(
        string? search, bool? isSuspended, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var query = _dbContext.Patients
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.User.FullName.Contains(s) ||
                                     p.PatientCode.Contains(s) ||
                                     (p.PhoneNumber != null && p.PhoneNumber.Contains(s)) ||
                                     (p.User.Email != null && p.User.Email.Contains(s)));
        }

        if (isSuspended.HasValue)
        {
            query = query.Where(p => p.User.IsSuspended == isSuspended.Value);
        }

        var total = await query.CountAsync(ct);

        var patients = await query
            .OrderBy(p => p.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                Patient = p,
                ActiveCount = _dbContext.Appointments.Count(a =>
                    a.PatientId == p.Id &&
                    (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
                    (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime))),
                LatestVisit = _dbContext.Appointments
                    .Where(a => a.PatientId == p.Id && a.Status == AppointmentStatus.Completed)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Select(a => new { a.AppointmentDate, DoctorName = a.Doctor.User.FullName })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = patients.Select(x => new PatientAdminListItemDto
        {
            Id = x.Patient.Id,
            UserId = x.Patient.UserId,
            PatientCode = x.Patient.PatientCode,
            FullName = x.Patient.User.FullName,
            Email = x.Patient.User.Email ?? string.Empty,
            PhoneNumber = x.Patient.PhoneNumber,
            DateOfBirth = x.Patient.DateOfBirth,
            BloodGroup = x.Patient.BloodGroup,
            IsSuspended = x.Patient.User.IsSuspended,
            IsDeleted = x.Patient.IsDeleted,
            ActiveAppointmentsCount = x.ActiveCount,
            CreatedAt = x.Patient.User.CreatedAt,
            LatestVisitDate = x.LatestVisit?.AppointmentDate,
            LatestDoctorName = x.LatestVisit?.DoctorName
        }).ToList();

        return new PagedResult<PatientAdminListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<PatientAdminListItemDto?> GetPatientByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _dbContext.Patients
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (p == null) return null;

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var activeCount = await _dbContext.Appointments.CountAsync(a =>
            a.PatientId == p.Id &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
            (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)), ct);

        return new PatientAdminListItemDto
        {
            Id = p.Id,
            UserId = p.UserId,
            PatientCode = p.PatientCode,
            FullName = p.User.FullName,
            Email = p.User.Email ?? string.Empty,
            PhoneNumber = p.PhoneNumber,
            DateOfBirth = p.DateOfBirth,
            BloodGroup = p.BloodGroup,
            IsSuspended = p.User.IsSuspended,
            IsDeleted = p.IsDeleted,
            ActiveAppointmentsCount = activeCount,
            CreatedAt = p.User.CreatedAt
        };
    }

    public async Task<bool> UpdatePatientAsync(UpdatePatientAdminDto dto, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == dto.Id && !p.IsDeleted, ct);

        if (patient == null) return false;

        patient.User.FullName = dto.FullName.Trim();
        patient.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        patient.DateOfBirth = dto.DateOfBirth;
        patient.BloodGroup = string.IsNullOrWhiteSpace(dto.BloodGroup) ? null : dto.BloodGroup.Trim().ToUpperInvariant();
        patient.Allergies = string.IsNullOrWhiteSpace(dto.Allergies) ? null : dto.Allergies.Trim();
        patient.MedicalHistory = string.IsNullOrWhiteSpace(dto.MedicalHistory) ? null : dto.MedicalHistory.Trim();

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Admin updated Patient profile {Id}.", dto.Id);
        return true;
    }

    public async Task<bool> TogglePatientSuspensionAsync(int id, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (patient == null) return false;

        patient.User.IsSuspended = !patient.User.IsSuspended;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin changed suspension state of Patient {Id} to {IsSuspended}.", id, patient.User.IsSuspended);
        return true;
    }

    public async Task<(bool Succeeded, string? Error)> DeletePatientAsync(int id, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (patient == null)
        {
            return (false, "Patient not found.");
        }

        // BR-07: Check future Pending or Confirmed appointments
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        bool hasFutureAppts = await _dbContext.Appointments.AnyAsync(a =>
            a.PatientId == id &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
            (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)), ct);

        if (hasFutureAppts)
        {
            return (false, "Cannot delete patient because they have future pending or confirmed appointments. Please suspend the patient or cancel their appointments first.");
        }

        // Soft delete both Patient and User profile
        patient.IsDeleted = true;
        patient.User.IsDeleted = true;

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Admin soft-deleted Patient {Id}.", id);

        return (true, null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Specializations Management
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SpecializationAdminDto>> GetSpecializationsAsync(CancellationToken ct = default)
    {
        var specs = await _dbContext.Specializations
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SpecializationAdminDto
            {
                Id = s.Id,
                Name = s.Name,
                DoctorCount = _dbContext.Doctors.Count(d => d.SpecializationId == s.Id && !d.IsDeleted),
                IsDeleted = s.IsDeleted
            })
            .ToListAsync(ct);

        return specs;
    }

    public async Task<bool> CreateSpecializationAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var exists = await _dbContext.Specializations
            .AnyAsync(s => s.Name == trimmed && !s.IsDeleted, ct);

        if (exists) return false;

        _dbContext.Specializations.Add(new Specialization { Name = trimmed });
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateSpecializationAsync(int id, string name, CancellationToken ct = default)
    {
        var spec = await _dbContext.Specializations.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (spec == null) return false;

        var trimmed = name.Trim();
        var exists = await _dbContext.Specializations
            .AnyAsync(s => s.Id != id && s.Name == trimmed && !s.IsDeleted, ct);

        if (exists) return false;

        spec.Name = trimmed;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Succeeded, string? Error)> DeleteSpecializationAsync(int id, CancellationToken ct = default)
    {
        var spec = await _dbContext.Specializations.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (spec == null) return (false, "Specialization not found.");

        // BR-07: Specialization in use by any active doctor cannot be deleted
        bool inUse = await _dbContext.Doctors.AnyAsync(d => d.SpecializationId == id && !d.IsDeleted, ct);
        if (inUse)
        {
            return (false, "Cannot delete specialization because it is currently in use by one or more active doctors. Please reassign the doctors first.");
        }

        spec.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
        return (true, null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Appointments (Admin Filterable View)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<AppointmentListItemDto>> GetAppointmentsAsync(
        DateOnly? date, int? doctorId, int? patientId, AppointmentStatus? status, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .AsQueryable();

        if (date.HasValue)
        {
            query = query.Where(a => a.AppointmentDate == date.Value);
        }

        if (doctorId.HasValue && doctorId.Value > 0)
        {
            query = query.Where(a => a.DoctorId == doctorId.Value);
        }

        if (patientId.HasValue && patientId.Value > 0)
        {
            query = query.Where(a => a.PatientId == patientId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        var appointments = await query
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var clinicNow = ScheduleService.GetClinicLocalNow();

        var items = appointments.Select(a => new AppointmentListItemDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientName = a.Patient?.User?.FullName ?? "Patient",
            PatientCode = a.Patient?.PatientCode ?? string.Empty,
            PatientPhone = a.Patient?.PhoneNumber,
            DoctorId = a.DoctorId,
            DoctorName = a.Doctor?.User?.FullName ?? "Doctor",
            SpecializationName = a.Doctor?.Specialization?.Name ?? "General Practice",
            RoomNumber = a.Doctor?.RoomNumber,
            ConsultationFee = a.Doctor?.ConsultationFee ?? 0m,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = a.Status,
            Notes = a.Notes,
            RowVersion = a.RowVersion,
            CanCancelOrReschedule = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
        }).ToList();

        return new PagedResult<AppointmentListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Email Logs & Resend (FR-28)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<NotificationAdminListItemDto>> GetNotificationsAsync(
        string? status, string? type, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Include(n => n.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotificationStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(n => n.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(n => n.Type == type);
        }

        var total = await query.CountAsync(ct);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = notifications.Select(n => new NotificationAdminListItemDto
        {
            Id = n.Id,
            UserId = n.UserId,
            RecipientName = n.User?.FullName ?? "User",
            RecipientEmail = n.User?.Email ?? string.Empty,
            Type = n.Type,
            Subject = n.Subject,
            Status = n.Status.ToString(),
            CreatedAt = n.CreatedAt,
            SentAt = n.SentAt
        }).ToList();

        return new PagedResult<NotificationAdminListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> ResendNotificationAsync(int id, CancellationToken ct = default)
    {
        var notification = await _dbContext.Notifications
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        if (notification == null || notification.User == null || string.IsNullOrEmpty(notification.User.Email))
        {
            return false;
        }

        try
        {
            var success = await _emailSender.SendAsync(
                notification.User.Email,
                $"[Resent] {notification.Subject}",
                $"<p>This is a re-sent notification regarding your appointment / clinic request: <strong>{notification.Subject}</strong>.</p>");

            if (success)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTime.UtcNow;
                await _uow.SaveChangesAsync(ct);
                _logger.LogInformation("Admin re-sent notification {Id} successfully.", id);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-send notification {Id}.", id);
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10,000 Sample Appointments Seeder (Dev Only for NFR-02)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<int> SeedSampleVolumeAsync(int count = 10000, CancellationToken ct = default)
    {
        var existingCount = await _dbContext.Appointments.CountAsync(ct);
        if (existingCount >= count)
        {
            _logger.LogInformation("Database already has {Count} appointments, skipping volume seeding.", existingCount);
            return existingCount;
        }

        var doctors = await _dbContext.Doctors.Where(d => !d.IsDeleted).ToListAsync(ct);
        var patients = await _dbContext.Patients.Where(p => !p.IsDeleted).ToListAsync(ct);

        if (!doctors.Any() || !patients.Any())
        {
            throw new InvalidOperationException("Doctors and Patients must be seeded first before generating sample appointment volume.");
        }

        var random = new Random(42); // deterministic seed
        var needed = count - existingCount;

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);

        var statuses = new[]
        {
            AppointmentStatus.Completed,
            AppointmentStatus.Completed,
            AppointmentStatus.Completed,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Cancelled,
            AppointmentStatus.Rejected,
            AppointmentStatus.Pending
        };

        var batchSize = 1000;
        int inserted = 0;

        for (int i = 0; i < needed; i += batchSize)
        {
            var batch = new List<Appointment>();
            int currentBatchCount = Math.Min(batchSize, needed - i);

            for (int j = 0; j < currentBatchCount; j++)
            {
                var doc = doctors[random.Next(doctors.Count)];
                var pat = patients[random.Next(patients.Count)];

                // Distribute between -365 days and +60 days
                int dayOffset = random.Next(-365, 60);
                var apptDate = today.AddDays(dayOffset);

                // Slot times between 09:00 and 16:30
                int hour = random.Next(9, 17);
                int minute = random.Next(0, 2) * 30;
                var startTime = new TimeSpan(hour, minute, 0);
                var endTime = startTime.Add(TimeSpan.FromMinutes(30));

                var status = dayOffset < 0 ? AppointmentStatus.Completed : statuses[random.Next(statuses.Length)];

                batch.Add(new Appointment
                {
                    DoctorId = doc.Id,
                    PatientId = pat.Id,
                    AppointmentDate = apptDate,
                    StartTime = startTime,
                    EndTime = endTime,
                    Status = status,
                    Notes = "Sample clinical volume visit",
                    CreatedAt = DateTime.UtcNow.AddDays(dayOffset - 2)
                });
            }

            _dbContext.Appointments.AddRange(batch);
            await _dbContext.SaveChangesAsync(ct);
            inserted += currentBatchCount;
        }

        _logger.LogInformation("Successfully seeded {Count} sample appointments for performance evaluation.", inserted);
        return inserted;
    }
}
