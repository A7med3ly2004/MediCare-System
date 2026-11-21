using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Appointments;

public class AppointmentService : IAppointmentService
{
    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _dbContext;
    private readonly IScheduleService _scheduleService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentService> _logger;

    private readonly int _cancelLeadHours;

    public AppointmentService(
        IUnitOfWork uow,
        ApplicationDbContext dbContext,
        IScheduleService scheduleService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<AppointmentService> logger)
    {
        _uow = uow;
        _dbContext = dbContext;
        _scheduleService = scheduleService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;

        _cancelLeadHours = int.TryParse(_configuration["Booking:CancelLeadHours"], out var leadHours) ? leadHours : 2;
    }

    public async Task<AppointmentDetailDto> CreateAsync(BookAppointmentDto dto, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == dto.PatientUserId && !p.IsDeleted, ct);

        if (patient == null)
        {
            throw new InvalidOperationException("Patient record not found.");
        }

        var doctor = await _dbContext.Doctors
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .FirstOrDefaultAsync(d => d.Id == dto.DoctorId && !d.IsDeleted, ct);

        if (doctor == null)
        {
            throw new InvalidOperationException("Doctor record not found.");
        }

        // 1. Verify slot availability via pure ScheduleService inside transaction
        var freeSlots = await _scheduleService.GetFreeSlotsAsync(dto.DoctorId, dto.AppointmentDate, dto.AppointmentDate, ct);
        var slotAvailable = freeSlots.Any(s => s.StartTime == dto.StartTime && s.EndTime == dto.EndTime);

        if (!slotAvailable)
        {
            throw new SlotUnavailableException("The selected time slot is no longer available.");
        }

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = dto.DoctorId,
            AppointmentDate = dto.AppointmentDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Status = AppointmentStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Appointments.Add(appointment);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent booking race detected for Doctor {DoctorId} on {Date} at {Time}",
                dto.DoctorId, dto.AppointmentDate, dto.StartTime);

            // Filtered unique index constraint caught
            throw new SlotUnavailableException("This time slot was just booked by another patient. Please select another slot.", ex);
        }

        // Send notifications (errors logged internally, never rolls back appointment)
        await _notificationService.SendBookingReceivedPatientAsync(
            patient.UserId,
            patient.User.Email ?? string.Empty,
            patient.User.FullName,
            doctor.User.FullName,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            doctor.RoomNumber,
            doctor.ConsultationFee);

        await _notificationService.SendBookingReceivedDoctorAsync(
            doctor.UserId,
            doctor.User.Email ?? string.Empty,
            doctor.User.FullName,
            patient.User.FullName,
            patient.PatientCode,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Notes);

        _logger.LogInformation("Appointment {AppointmentId} booked for Patient {PatientId} with Doctor {DoctorId}.",
            appointment.Id, patient.Id, doctor.Id);

        return MapToDetailDto(appointment, patient, doctor);
    }

    public async Task<AppointmentDetailDto> CancelAsync(CancelAppointmentDto dto, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId, ct);

        if (appointment == null)
        {
            throw new InvalidOperationException("Appointment not found.");
        }

        // Authorization check: Patient owns appointment OR user is Admin
        bool isAdmin = string.Equals(dto.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        bool isOwner = string.Equals(appointment.Patient?.UserId, dto.UserId, StringComparison.Ordinal);

        if (!isAdmin && !isOwner)
        {
            _logger.LogWarning("Unauthorized cancellation attempt on Appointment {Id} by User {UserId}", appointment.Id, dto.UserId);
            throw new UnauthorizedAccessException("You are not authorized to cancel this appointment.");
        }

        // Valid status: Pending or Confirmed
        if (appointment.Status != AppointmentStatus.Pending && appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Cancelled,
                $"Cannot cancel appointment with status {appointment.Status}. Only Pending or Confirmed appointments can be cancelled.");
        }

        // Lead time rule (BR-04): At least Booking:CancelLeadHours before start time
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var apptStart = appointment.AppointmentDate.ToDateTime(TimeOnly.MinValue).Add(appointment.StartTime);

        if (apptStart - clinicNow < TimeSpan.FromHours(_cancelLeadHours))
        {
            throw new LeadTimeViolationException(_cancelLeadHours,
                $"Appointments can only be cancelled at least {_cancelLeadHours} hours prior to the scheduled start time.");
        }

        appointment.Status = AppointmentStatus.Cancelled;

        // Record AuditLog (BR-08)
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = dto.UserId,
            Action = "Cancel",
            EntityName = "Appointment",
            EntityId = appointment.Id.ToString(),
            OccurredAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Appointment {AppointmentId} cancelled by User {UserId}.", appointment.Id, dto.UserId);

        // Notify both parties
        string cancelledBy = isAdmin ? "clinic administration" : "the patient";

        if (appointment.Patient?.User != null)
        {
            await _notificationService.SendAppointmentCancelledAsync(
                appointment.Patient.UserId,
                appointment.Patient.User.Email ?? string.Empty,
                appointment.Patient.User.FullName,
                $"Dr. {appointment.Doctor.User.FullName}",
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                cancelledBy);
        }

        if (appointment.Doctor?.User != null)
        {
            await _notificationService.SendAppointmentCancelledAsync(
                appointment.Doctor.UserId,
                appointment.Doctor.User.Email ?? string.Empty,
                $"Dr. {appointment.Doctor.User.FullName}",
                appointment.Patient?.User?.FullName ?? "Patient",
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                cancelledBy);
        }

        return MapToDetailDto(appointment, appointment.Patient!, appointment.Doctor!);
    }

    public async Task<AppointmentDetailDto> RescheduleAsync(RescheduleAppointmentDto dto, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId, ct);

        if (appointment == null)
        {
            throw new InvalidOperationException("Appointment not found.");
        }

        // Ownership check
        if (!string.Equals(appointment.Patient?.UserId, dto.PatientUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("You can only reschedule your own appointments.");
        }

        // Valid status: Pending or Confirmed
        if (appointment.Status != AppointmentStatus.Pending && appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Pending,
                $"Cannot reschedule appointment with status {appointment.Status}. Only Pending or Confirmed appointments can be rescheduled.");
        }

        // Lead time rule on current appointment: At least Booking:CancelLeadHours before old start time
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var oldApptStart = appointment.AppointmentDate.ToDateTime(TimeOnly.MinValue).Add(appointment.StartTime);

        if (oldApptStart - clinicNow < TimeSpan.FromHours(_cancelLeadHours))
        {
            throw new LeadTimeViolationException(_cancelLeadHours,
                $"Appointments can only be rescheduled at least {_cancelLeadHours} hours prior to the scheduled start time.");
        }

        // Check availability of NEW slot
        var freeSlots = await _scheduleService.GetFreeSlotsAsync(appointment.DoctorId, dto.NewDate, dto.NewDate, ct);
        var slotAvailable = freeSlots.Any(s => s.StartTime == dto.NewStartTime && s.EndTime == dto.NewEndTime);

        if (!slotAvailable)
        {
            throw new SlotUnavailableException("The proposed new appointment slot is not available.");
        }

        // Move appointment and return to Pending (BR-04)
        appointment.AppointmentDate = dto.NewDate;
        appointment.StartTime = dto.NewStartTime;
        appointment.EndTime = dto.NewEndTime;
        appointment.Status = AppointmentStatus.Pending;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent reschedule conflict for Doctor {DoctorId} on {Date} at {Time}",
                appointment.DoctorId, dto.NewDate, dto.NewStartTime);

            throw new SlotUnavailableException("The selected slot was just taken. Please choose another slot.", ex);
        }

        _logger.LogInformation("Appointment {AppointmentId} rescheduled to {Date} {StartTime}.",
            appointment.Id, dto.NewDate, dto.NewStartTime);

        // Notify patient and doctor
        if (appointment.Patient?.User != null)
        {
            await _notificationService.SendAppointmentRescheduledAsync(
                appointment.Patient.UserId,
                appointment.Patient.User.Email ?? string.Empty,
                appointment.Patient.User.FullName,
                $"Dr. {appointment.Doctor.User.FullName}",
                dto.NewDate,
                dto.NewStartTime,
                dto.NewEndTime);
        }

        if (appointment.Doctor?.User != null)
        {
            await _notificationService.SendAppointmentRescheduledAsync(
                appointment.Doctor.UserId,
                appointment.Doctor.User.Email ?? string.Empty,
                $"Dr. {appointment.Doctor.User.FullName}",
                appointment.Patient?.User?.FullName ?? "Patient",
                dto.NewDate,
                dto.NewStartTime,
                dto.NewEndTime);
        }

        return MapToDetailDto(appointment, appointment.Patient!, appointment.Doctor!);
    }

    public async Task<AppointmentDetailDto> ConfirmAsync(ConfirmRejectDto dto, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId, ct);

        if (appointment == null)
        {
            throw new InvalidOperationException("Appointment not found.");
        }

        // Authorization: Admin or Doctor who owns the appointment
        bool isAdmin = string.Equals(dto.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        bool isDoctorOwner = dto.DoctorId.HasValue && appointment.DoctorId == dto.DoctorId.Value;

        if (!isAdmin && !isDoctorOwner)
        {
            throw new UnauthorizedAccessException("You are not authorized to confirm this appointment.");
        }

        // Must be in Pending status (BR-03 / UC-07)
        if (appointment.Status != AppointmentStatus.Pending)
        {
            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Confirmed,
                $"Cannot confirm appointment because its status is already {appointment.Status}.");
        }

        // RowVersion concurrency token check
        if (dto.RowVersion != null && dto.RowVersion.Length > 0)
        {
            _dbContext.Entry(appointment).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;
        }

        appointment.Status = AppointmentStatus.Confirmed;

        // Record AuditLog (BR-08)
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = dto.UserId,
            Action = "Confirm",
            EntityName = "Appointment",
            EntityId = appointment.Id.ToString(),
            OccurredAt = DateTime.UtcNow
        });

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on confirming Appointment {AppointmentId}", appointment.Id);
            await _dbContext.Entry(appointment).ReloadAsync(ct);

            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Confirmed,
                $"The appointment has already been updated to {appointment.Status} by another user.");
        }

        _logger.LogInformation("Appointment {AppointmentId} Confirmed by User {UserId}.", appointment.Id, dto.UserId);

        // Notify patient
        if (appointment.Patient?.User != null)
        {
            await _notificationService.SendAppointmentConfirmedAsync(
                appointment.Patient.UserId,
                appointment.Patient.User.Email ?? string.Empty,
                appointment.Patient.User.FullName,
                appointment.Doctor.User.FullName,
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                appointment.Doctor.RoomNumber);
        }

        return MapToDetailDto(appointment, appointment.Patient!, appointment.Doctor!);
    }

    public async Task<AppointmentDetailDto> RejectAsync(ConfirmRejectDto dto, string? reason = null, CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId, ct);

        if (appointment == null)
        {
            throw new InvalidOperationException("Appointment not found.");
        }

        bool isAdmin = string.Equals(dto.UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        bool isDoctorOwner = dto.DoctorId.HasValue && appointment.DoctorId == dto.DoctorId.Value;

        if (!isAdmin && !isDoctorOwner)
        {
            throw new UnauthorizedAccessException("You are not authorized to reject this appointment.");
        }

        if (appointment.Status != AppointmentStatus.Pending)
        {
            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Rejected,
                $"Cannot reject appointment because its status is already {appointment.Status}.");
        }

        if (dto.RowVersion != null && dto.RowVersion.Length > 0)
        {
            _dbContext.Entry(appointment).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;
        }

        appointment.Status = AppointmentStatus.Rejected;

        // Record AuditLog (BR-08)
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = dto.UserId,
            Action = "Reject",
            EntityName = "Appointment",
            EntityId = appointment.Id.ToString(),
            OccurredAt = DateTime.UtcNow
        });

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on rejecting Appointment {AppointmentId}", appointment.Id);
            await _dbContext.Entry(appointment).ReloadAsync(ct);

            throw new InvalidStateTransitionException(
                appointment.Status,
                AppointmentStatus.Rejected,
                $"The appointment has already been updated to {appointment.Status} by another user.");
        }

        _logger.LogInformation("Appointment {AppointmentId} Rejected by User {UserId}.", appointment.Id, dto.UserId);

        // Notify patient
        if (appointment.Patient?.User != null)
        {
            await _notificationService.SendAppointmentRejectedAsync(
                appointment.Patient.UserId,
                appointment.Patient.User.Email ?? string.Empty,
                appointment.Patient.User.FullName,
                appointment.Doctor.User.FullName,
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                reason);
        }

        return MapToDetailDto(appointment, appointment.Patient!, appointment.Doctor!);
    }

    public async Task<AppointmentDetailDto?> GetAppointmentDetailsAsync(int appointmentId, CancellationToken ct = default)
    {
        var a = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

        if (a == null || a.Patient == null || a.Doctor == null) return null;

        return MapToDetailDto(a, a.Patient, a.Doctor);
    }

    public async Task<List<AppointmentListItemDto>> GetPatientAppointmentsAsync(string patientUserId, bool? upcoming = null, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientUserId && !p.IsDeleted, ct);

        if (patient == null) return new List<AppointmentListItemDto>();

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Where(a => a.PatientId == patient.Id);

        if (upcoming.HasValue)
        {
            if (upcoming.Value)
            {
                query = query.Where(a => a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime));
            }
            else
            {
                query = query.Where(a => a.AppointmentDate < today || (a.AppointmentDate == today && a.StartTime < nowTime));
            }
        }

        var appointments = await query
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync(ct);

        return appointments.Select(a => MapToListItemDto(a, clinicNow)).ToList();
    }

    public async Task<DoctorDashboardDto> GetDoctorDashboardDataAsync(string doctorUserId, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .FirstOrDefaultAsync(d => d.UserId == doctorUserId && !d.IsDeleted, ct);

        if (doctor == null)
        {
            return new DoctorDashboardDto();
        }

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;
        var weekEnd = today.AddDays(7);

        // Fetch appointments for this doctor in relevant window
        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doctor.Id
                     && (a.AppointmentDate >= today.AddDays(-1) && a.AppointmentDate <= weekEnd || a.Status == AppointmentStatus.Pending))
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync(ct);

        var todayAppts = appointments.Where(a => a.AppointmentDate == today).ToList();
        var pendingAppts = appointments.Where(a => a.Status == AppointmentStatus.Pending).ToList();

        var todayDtos = todayAppts.Select(a => MapToListItemDto(a, clinicNow)).ToList();
        var pendingDtos = pendingAppts.Select(a => MapToListItemDto(a, clinicNow)).ToList();
        var upcomingDtos = appointments
            .Where(a => a.AppointmentDate >= today && a.AppointmentDate <= weekEnd)
            .Select(a => MapToListItemDto(a, clinicNow))
            .ToList();

        // Next upcoming consultation today
        var nextConsultation = todayDtos
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.StartTime >= nowTime)
            .OrderBy(a => a.StartTime)
            .FirstOrDefault();

        return new DoctorDashboardDto
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.User.FullName,
            SpecializationName = doctor.Specialization?.Name ?? "General Practice",
            RoomNumber = doctor.RoomNumber,
            TodayPatientsCount = todayAppts.Count(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected),
            CompletedVisitsCount = todayAppts.Count(a => a.Status == AppointmentStatus.Completed),
            PendingRequestsCount = pendingAppts.Count,
            NextConsultation = nextConsultation,
            TodayAppointments = todayDtos,
            PendingAppointments = pendingDtos,
            UpcomingWeekAppointments = upcomingDtos
        };
    }

    public async Task<PatientDashboardDto> GetPatientDashboardDataAsync(string patientUserId, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == patientUserId && !p.IsDeleted, ct);

        if (patient == null)
        {
            return new PatientDashboardDto();
        }

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        var allAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Where(a => a.PatientId == patient.Id)
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.StartTime)
            .ToListAsync(ct);

        var allDtos = allAppts.Select(a => MapToListItemDto(a, clinicNow)).ToList();

        // Next upcoming appointment
        var nextAppt = allDtos
            .Where(a => (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending)
                     && (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)))
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .FirstOrDefault();

        return new PatientDashboardDto
        {
            PatientId = patient.Id,
            PatientName = patient.User.FullName,
            PatientCode = patient.PatientCode,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.User.Email,
            BloodGroup = patient.BloodGroup,
            Allergies = patient.Allergies,
            NextAppointment = nextAppt,
            RecentAppointments = allDtos.Take(10).ToList()
        };
    }

    private AppointmentDetailDto MapToDetailDto(Appointment a, Patient p, Doctor d)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var apptStart = a.AppointmentDate.ToDateTime(TimeOnly.MinValue).Add(a.StartTime);
        bool canCancelOrReschedule = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                                     && (apptStart - clinicNow >= TimeSpan.FromHours(_cancelLeadHours));

        return new AppointmentDetailDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientUserId = p.UserId,
            PatientName = p.User?.FullName ?? "Patient",
            PatientCode = p.PatientCode,
            PatientPhone = p.PhoneNumber,
            PatientEmail = p.User?.Email,
            DoctorId = a.DoctorId,
            DoctorUserId = d.UserId,
            DoctorName = d.User?.FullName ?? "Doctor",
            SpecializationName = d.Specialization?.Name ?? "General Practice",
            Location = d.Location,
            RoomNumber = d.RoomNumber,
            ConsultationFee = d.ConsultationFee,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = a.Status,
            Notes = a.Notes,
            RowVersion = a.RowVersion,
            CreatedAt = a.CreatedAt,
            CanCancelOrReschedule = canCancelOrReschedule
        };
    }

    private AppointmentListItemDto MapToListItemDto(Appointment a, DateTime clinicNow)
    {
        var apptStart = a.AppointmentDate.ToDateTime(TimeOnly.MinValue).Add(a.StartTime);
        bool canCancelOrReschedule = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                                     && (apptStart - clinicNow >= TimeSpan.FromHours(_cancelLeadHours));

        return new AppointmentListItemDto
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
            ConsultationFee = a.Doctor?.ConsultationFee ?? 0,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = a.Status,
            Notes = a.Notes,
            RowVersion = a.RowVersion,
            CanCancelOrReschedule = canCancelOrReschedule
        };
    }
}
