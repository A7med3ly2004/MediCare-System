using System.Text.RegularExpressions;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.MedicalRecords;

public class MedicalRecordService : IMedicalRecordService
{
    private static readonly Regex BloodPressureRegex = new(@"^\d{2,3}\/\d{2,3}(\s*(mmHg|mm Hg))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> ValidBloodGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"
    };

    private readonly IUnitOfWork _uow;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MedicalRecordService> _logger;

    public MedicalRecordService(
        IUnitOfWork uow,
        ApplicationDbContext dbContext,
        ILogger<MedicalRecordService> logger)
    {
        _uow = uow;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MedicalRecordDetailDto> SaveVisitAsync(SaveMedicalRecordDto dto, CancellationToken ct = default)
    {
        ValidateRecordInput(dto);

        var appointment = await _dbContext.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.MedicalRecord).ThenInclude(m => m!.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId, ct);

        if (appointment == null)
        {
            throw new InvalidOperationException("Appointment not found.");
        }

        // Ownership: Doctor who owns this appointment (BR-11, NFR-06)
        if (!string.Equals(appointment.Doctor.UserId, dto.DoctorUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("You are not authorized to record a visit for another doctor's appointment.");
        }

        // BR-05: Allowed only for a Confirmed appointment (or editing a Completed appointment that already has a record)
        if (appointment.Status != AppointmentStatus.Confirmed && appointment.Status != AppointmentStatus.Completed)
        {
            throw new InvalidVisitStateException(
                $"Cannot record a visit for an appointment with status '{appointment.Status}'. Visits can only be recorded for Confirmed appointments.");
        }

        MedicalRecord record;

        if (appointment.MedicalRecord != null)
        {
            // Edit existing record
            record = appointment.MedicalRecord;
            ApplyRecordUpdates(record, dto);
            record.UpdatedAt = DateTime.UtcNow;

            UpdatePrescription(record, dto.Prescriptions);
        }
        else
        {
            // Create new record
            record = new MedicalRecord
            {
                AppointmentId = appointment.Id,
                BloodPressure = CleanBp(dto.BloodPressure),
                HeartRate = dto.HeartRate,
                Symptoms = dto.Symptoms?.Trim(),
                Diagnosis = dto.Diagnosis?.Trim(),
                TreatmentNotes = dto.TreatmentNotes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var validPrescriptions = dto.Prescriptions?
                .Where(p => !string.IsNullOrWhiteSpace(p.Medication))
                .ToList();

            if (validPrescriptions != null && validPrescriptions.Any())
            {
                var prescription = new Prescription
                {
                    MedicalRecord = record,
                    IssuedAt = DateTime.UtcNow
                };

                foreach (var item in validPrescriptions)
                {
                    prescription.Items.Add(new PrescriptionItem
                    {
                        Medication = item.Medication.Trim(),
                        Dosage = item.Dosage.Trim(),
                        Frequency = item.Frequency.Trim()
                    });
                }

                record.Prescription = prescription;
            }

            _dbContext.MedicalRecords.Add(record);

            // BR-05: Saving a medical record marks the appointment Completed in the same transaction
            appointment.Status = AppointmentStatus.Completed;
        }

        // Save all in ONE Unit of Work transaction (NFR-15)
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Medical record saved successfully for Appointment {AppointmentId} by Doctor {UserId}.", appointment.Id, dto.DoctorUserId);

        return MapToDetailDto(record, appointment);
    }

    public async Task<MedicalRecordDetailDto> UpdateVisitAsync(int medicalRecordId, SaveMedicalRecordDto dto, CancellationToken ct = default)
    {
        ValidateRecordInput(dto);

        var record = await _dbContext.MedicalRecords
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(m => m.Id == medicalRecordId, ct);

        if (record == null)
        {
            throw new MedicalRecordNotFoundException();
        }

        if (!string.Equals(record.Appointment.Doctor.UserId, dto.DoctorUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("You are not authorized to edit this medical record.");
        }

        ApplyRecordUpdates(record, dto);
        record.UpdatedAt = DateTime.UtcNow;

        UpdatePrescription(record, dto.Prescriptions);

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Medical record {RecordId} updated by Doctor {UserId}.", record.Id, dto.DoctorUserId);

        return MapToDetailDto(record, record.Appointment);
    }

    public async Task<MedicalRecordDetailDto?> GetRecordByIdAsync(int recordId, string userId, string role, CancellationToken ct = default)
    {
        // Admin has no access to clinical content pages (BR-11, NFR-06)
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Administrators do not have access to clinical medical records.");
        }

        var record = await _dbContext.MedicalRecords
            .AsNoTracking()
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(m => m.Id == recordId, ct);

        if (record == null) return null;

        // Verify ownership
        if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(record.Appointment.Patient.UserId, userId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("You are not authorized to view this medical record.");
            }
        }
        else if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(record.Appointment.Doctor.UserId, userId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("You are not authorized to view this medical record.");
            }
        }

        return MapToDetailDto(record, record.Appointment);
    }

    public async Task<MedicalRecordDetailDto?> GetRecordByAppointmentIdAsync(int appointmentId, string userId, string role, CancellationToken ct = default)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Administrators do not have access to clinical medical records.");
        }

        var record = await _dbContext.MedicalRecords
            .AsNoTracking()
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(m => m.AppointmentId == appointmentId, ct);

        if (record == null) return null;

        if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(record.Appointment.Patient.UserId, userId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("You are not authorized to view this medical record.");
            }
        }
        else if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(record.Appointment.Doctor.UserId, userId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("You are not authorized to view this medical record.");
            }
        }

        return MapToDetailDto(record, record.Appointment);
    }

    public async Task<List<MedicalRecordDetailDto>> GetPatientMedicalHistoryAsync(string patientUserId, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientUserId && !p.IsDeleted, ct);

        if (patient == null) return new List<MedicalRecordDetailDto>();

        var records = await _dbContext.MedicalRecords
            .AsNoTracking()
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .Where(m => m.Appointment.PatientId == patient.Id)
            .OrderByDescending(m => m.Appointment.AppointmentDate)
            .ThenByDescending(m => m.Appointment.StartTime)
            .ToListAsync(ct);

        return records.Select(r => MapToDetailDto(r, r.Appointment)).ToList();
    }

    public async Task<PatientMedicalProfileDto?> GetPatientMedicalProfileForDoctorAsync(int patientId, string doctorUserId, CancellationToken ct = default)
    {
        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == doctorUserId && !d.IsDeleted, ct);

        if (doctor == null) return null;

        // FR-25, BR-11: Doctor can only access data of patients who have an appointment with them
        bool hasRelationship = await _dbContext.Appointments
            .AnyAsync(a => a.DoctorId == doctor.Id && a.PatientId == patientId, ct);

        if (!hasRelationship)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this patient's medical details because they have no appointments with you.");
        }

        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted, ct);

        if (patient == null) return null;

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var nowTime = clinicNow.TimeOfDay;

        // Next upcoming appointment with this doctor
        var nextApptEntity = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Where(a => a.PatientId == patient.Id && a.DoctorId == doctor.Id
                     && (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime))
                     && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected)
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .FirstOrDefaultAsync(ct);

        AppointmentListItemDto? nextAppt = null;
        if (nextApptEntity != null)
        {
            nextAppt = new AppointmentListItemDto
            {
                Id = nextApptEntity.Id,
                PatientId = nextApptEntity.PatientId,
                PatientName = patient.User.FullName,
                PatientCode = patient.PatientCode,
                DoctorId = doctor.Id,
                DoctorName = doctor.User?.FullName ?? "Doctor",
                SpecializationName = doctor.Specialization?.Name ?? "General Practice",
                RoomNumber = doctor.RoomNumber,
                ConsultationFee = doctor.ConsultationFee,
                AppointmentDate = nextApptEntity.AppointmentDate,
                StartTime = nextApptEntity.StartTime,
                EndTime = nextApptEntity.EndTime,
                Status = nextApptEntity.Status,
                Notes = nextApptEntity.Notes
            };
        }

        // Records written for this patient
        var records = await _dbContext.MedicalRecords
            .AsNoTracking()
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .Where(m => m.Appointment.PatientId == patient.Id)
            .OrderByDescending(m => m.Appointment.AppointmentDate)
            .ThenByDescending(m => m.Appointment.StartTime)
            .ToListAsync(ct);

        return new PatientMedicalProfileDto
        {
            PatientId = patient.Id,
            PatientUserId = patient.UserId,
            PatientName = patient.User.FullName,
            PatientCode = patient.PatientCode,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.User.Email,
            DateOfBirth = patient.DateOfBirth,
            BloodGroup = patient.BloodGroup,
            Allergies = patient.Allergies,
            MedicalHistory = patient.MedicalHistory,
            NextAppointment = nextAppt,
            MedicalRecords = records.Select(r => MapToDetailDto(r, r.Appointment)).ToList()
        };
    }

    public async Task<PatientMedicalProfileDto?> GetPatientProfileAsync(string patientUserId, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == patientUserId && !p.IsDeleted, ct);

        if (patient == null) return null;

        return new PatientMedicalProfileDto
        {
            PatientId = patient.Id,
            PatientUserId = patient.UserId,
            PatientName = patient.User.FullName,
            PatientCode = patient.PatientCode,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.User.Email,
            DateOfBirth = patient.DateOfBirth,
            BloodGroup = patient.BloodGroup,
            Allergies = patient.Allergies,
            MedicalHistory = patient.MedicalHistory
        };
    }

    public async Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileDto dto, CancellationToken ct = default)
    {
        var patient = await _dbContext.Patients
            .FirstOrDefaultAsync(p => p.UserId == dto.PatientUserId && !p.IsDeleted, ct);

        if (patient == null) return false;

        if (!string.IsNullOrWhiteSpace(dto.BloodGroup))
        {
            var bg = dto.BloodGroup.Trim().ToUpperInvariant();
            if (!ValidBloodGroups.Contains(bg))
            {
                throw new ArgumentException($"Invalid blood group '{dto.BloodGroup}'. Must be one of: A+, A-, B+, B-, AB+, AB-, O+, O-.");
            }
            patient.BloodGroup = bg;
        }
        else
        {
            patient.BloodGroup = null;
        }

        patient.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        patient.Allergies = string.IsNullOrWhiteSpace(dto.Allergies) ? null : dto.Allergies.Trim();
        patient.MedicalHistory = string.IsNullOrWhiteSpace(dto.MedicalHistory) ? null : dto.MedicalHistory.Trim();

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Patient profile updated for User {UserId}.", dto.PatientUserId);

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void ValidateRecordInput(SaveMedicalRecordDto dto)
    {
        if (dto.HeartRate.HasValue && (dto.HeartRate.Value < 20 || dto.HeartRate.Value > 300))
        {
            throw new ArgumentException("Heart rate must be between 20 and 300 bpm.");
        }

        if (!string.IsNullOrWhiteSpace(dto.BloodPressure))
        {
            var bp = dto.BloodPressure.Trim();
            if (!BloodPressureRegex.IsMatch(bp))
            {
                throw new ArgumentException("Blood pressure must follow format like '120/80' or '120/80 mmHg'.");
            }
            if (bp.Length > 20)
            {
                throw new ArgumentException("Blood pressure text cannot exceed 20 characters.");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Symptoms) && dto.Symptoms.Length > 2000)
        {
            throw new ArgumentException("Symptoms cannot exceed 2000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Diagnosis) && dto.Diagnosis.Length > 2000)
        {
            throw new ArgumentException("Diagnosis cannot exceed 2000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(dto.TreatmentNotes) && dto.TreatmentNotes.Length > 4000)
        {
            throw new ArgumentException("Treatment notes cannot exceed 4000 characters.");
        }

        if (dto.Prescriptions != null)
        {
            foreach (var item in dto.Prescriptions)
            {
                if (string.IsNullOrWhiteSpace(item.Medication) && string.IsNullOrWhiteSpace(item.Dosage) && string.IsNullOrWhiteSpace(item.Frequency))
                {
                    continue; // empty row skipped
                }

                if (string.IsNullOrWhiteSpace(item.Medication))
                    throw new ArgumentException("Medication name is required for all prescription items.");
                if (string.IsNullOrWhiteSpace(item.Dosage))
                    throw new ArgumentException($"Dosage is required for medication '{item.Medication}'.");
                if (string.IsNullOrWhiteSpace(item.Frequency))
                    throw new ArgumentException($"Frequency is required for medication '{item.Medication}'.");

                if (item.Medication.Length > 200)
                    throw new ArgumentException("Medication name cannot exceed 200 characters.");
                if (item.Dosage.Length > 100)
                    throw new ArgumentException("Dosage cannot exceed 100 characters.");
                if (item.Frequency.Length > 100)
                    throw new ArgumentException("Frequency cannot exceed 100 characters.");
            }
        }
    }

    private static void ApplyRecordUpdates(MedicalRecord record, SaveMedicalRecordDto dto)
    {
        record.BloodPressure = CleanBp(dto.BloodPressure);
        record.HeartRate = dto.HeartRate;
        record.Symptoms = string.IsNullOrWhiteSpace(dto.Symptoms) ? null : dto.Symptoms.Trim();
        record.Diagnosis = string.IsNullOrWhiteSpace(dto.Diagnosis) ? null : dto.Diagnosis.Trim();
        record.TreatmentNotes = string.IsNullOrWhiteSpace(dto.TreatmentNotes) ? null : dto.TreatmentNotes.Trim();
    }

    private void UpdatePrescription(MedicalRecord record, List<PrescriptionItemDto>? items)
    {
        var validItems = items?
            .Where(p => !string.IsNullOrWhiteSpace(p.Medication))
            .ToList() ?? new List<PrescriptionItemDto>();

        if (record.Prescription == null)
        {
            if (validItems.Any())
            {
                var prescription = new Prescription
                {
                    MedicalRecordId = record.Id,
                    IssuedAt = DateTime.UtcNow
                };

                foreach (var item in validItems)
                {
                    prescription.Items.Add(new PrescriptionItem
                    {
                        Medication = item.Medication.Trim(),
                        Dosage = item.Dosage.Trim(),
                        Frequency = item.Frequency.Trim()
                    });
                }

                record.Prescription = prescription;
                _dbContext.Prescriptions.Add(prescription);
            }
        }
        else
        {
            // Update items
            _dbContext.PrescriptionItems.RemoveRange(record.Prescription.Items);
            record.Prescription.Items.Clear();

            foreach (var item in validItems)
            {
                record.Prescription.Items.Add(new PrescriptionItem
                {
                    PrescriptionId = record.Prescription.Id,
                    Medication = item.Medication.Trim(),
                    Dosage = item.Dosage.Trim(),
                    Frequency = item.Frequency.Trim()
                });
            }
        }
    }

    private static string? CleanBp(string? bp)
    {
        if (string.IsNullOrWhiteSpace(bp)) return null;
        var trimmed = bp.Trim();
        if (!trimmed.EndsWith("mmHg", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += " mmHg";
        }
        return trimmed;
    }

    private static MedicalRecordDetailDto MapToDetailDto(MedicalRecord m, Appointment a)
    {
        var p = a.Patient;
        var d = a.Doctor;

        return new MedicalRecordDetailDto
        {
            Id = m.Id,
            AppointmentId = a.Id,
            AppointmentDate = a.AppointmentDate,
            StartTime = a.StartTime,
            EndTime = a.EndTime,

            DoctorId = a.DoctorId,
            DoctorUserId = d.UserId,
            DoctorName = d.User?.FullName ?? "Doctor",
            DoctorSpecialization = d.Specialization?.Name ?? "General Practice",
            RoomNumber = d.RoomNumber,

            PatientId = a.PatientId,
            PatientUserId = p.UserId,
            PatientName = p.User?.FullName ?? "Patient",
            PatientCode = p.PatientCode,
            PatientPhone = p.PhoneNumber,
            PatientEmail = p.User?.Email,
            DateOfBirth = p.DateOfBirth,
            BloodGroup = p.BloodGroup,
            Allergies = p.Allergies,
            MedicalHistory = p.MedicalHistory,

            BloodPressure = m.BloodPressure,
            HeartRate = m.HeartRate,
            Symptoms = m.Symptoms,
            Diagnosis = m.Diagnosis,
            TreatmentNotes = m.TreatmentNotes,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,

            PrescriptionId = m.Prescription?.Id,
            PrescriptionIssuedAt = m.Prescription?.IssuedAt,
            Prescriptions = m.Prescription?.Items.Select(i => new PrescriptionItemDto
            {
                Id = i.Id,
                Medication = i.Medication,
                Dosage = i.Dosage,
                Frequency = i.Frequency
            }).ToList() ?? new List<PrescriptionItemDto>()
        };
    }
}
