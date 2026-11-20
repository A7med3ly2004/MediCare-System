using MediCare.Services.MedicalRecords;

namespace MediCare.Services.Interfaces;

public interface IMedicalRecordService
{
    /// <summary>
    /// Records visit details, vitals, diagnosis, notes and optional prescriptions for a Confirmed appointment.
    /// Marks the appointment Completed in the same Unit of Work transaction (BR-05, NFR-15).
    /// </summary>
    Task<MedicalRecordDetailDto> SaveVisitAsync(SaveMedicalRecordDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing medical record. Allowed for the treating doctor.
    /// </summary>
    Task<MedicalRecordDetailDto> UpdateVisitAsync(int medicalRecordId, SaveMedicalRecordDto dto, CancellationToken ct = default);

    /// <summary>
    /// Gets a single medical record by Id with strict server-side ownership access control (BR-11, NFR-06).
    /// </summary>
    Task<MedicalRecordDetailDto?> GetRecordByIdAsync(int recordId, string userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Gets a medical record by AppointmentId with ownership check.
    /// </summary>
    Task<MedicalRecordDetailDto?> GetRecordByAppointmentIdAsync(int appointmentId, string userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Gets a patient's own history of completed visits with prescriptions (FR-23, UC-09).
    /// </summary>
    Task<List<MedicalRecordDetailDto>> GetPatientMedicalHistoryAsync(string patientUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets a patient's medical summary and records for a doctor who has an appointment with that patient (FR-25).
    /// </summary>
    Task<PatientMedicalProfileDto?> GetPatientMedicalProfileForDoctorAsync(int patientId, string doctorUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets patient profile for self-view/editing (FR-24).
    /// </summary>
    Task<PatientMedicalProfileDto?> GetPatientProfileAsync(string patientUserId, CancellationToken ct = default);

    /// <summary>
    /// Updates patient profile data (Phone, BloodGroup, Allergies, MedicalHistory) (FR-24).
    /// </summary>
    Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileDto dto, CancellationToken ct = default);
}
