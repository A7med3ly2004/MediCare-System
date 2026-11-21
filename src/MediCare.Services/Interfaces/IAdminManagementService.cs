using MediCare.Data.Entities;
using MediCare.Services.Admin;
using MediCare.Services.Appointments;

namespace MediCare.Services.Interfaces;

public interface IAdminManagementService
{
    // Doctors
    Task<PagedResult<DoctorAdminListItemDto>> GetDoctorsAsync(string? search, int? specializationId, bool? isSuspended, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<DoctorAdminListItemDto?> GetDoctorByIdAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateDoctorAsync(UpdateDoctorAdminDto dto, CancellationToken ct = default);
    Task<bool> ToggleDoctorSuspensionAsync(int id, CancellationToken ct = default);
    Task<(bool Succeeded, string? Error)> DeleteDoctorAsync(int id, CancellationToken ct = default);

    // Patients
    Task<PagedResult<PatientAdminListItemDto>> GetPatientsAsync(string? search, bool? isSuspended, int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<PatientAdminListItemDto?> GetPatientByIdAsync(int id, CancellationToken ct = default);
    Task<bool> UpdatePatientAsync(UpdatePatientAdminDto dto, CancellationToken ct = default);
    Task<bool> TogglePatientSuspensionAsync(int id, CancellationToken ct = default);
    Task<(bool Succeeded, string? Error)> DeletePatientAsync(int id, CancellationToken ct = default);

    // Specializations
    Task<List<SpecializationAdminDto>> GetSpecializationsAsync(CancellationToken ct = default);
    Task<bool> CreateSpecializationAsync(string name, CancellationToken ct = default);
    Task<bool> UpdateSpecializationAsync(int id, string name, CancellationToken ct = default);
    Task<(bool Succeeded, string? Error)> DeleteSpecializationAsync(int id, CancellationToken ct = default);

    // Appointments (Admin filter & control)
    Task<PagedResult<AppointmentListItemDto>> GetAppointmentsAsync(DateOnly? date, int? doctorId, int? patientId, AppointmentStatus? status, int page = 1, int pageSize = 10, CancellationToken ct = default);

    // Notifications log
    Task<PagedResult<NotificationAdminListItemDto>> GetNotificationsAsync(string? status, string? type, int page = 1, int pageSize = 15, CancellationToken ct = default);
    Task<bool> ResendNotificationAsync(int id, CancellationToken ct = default);

    // Volume generator (Dev only: ~10,000 sample appointments for NFR-02)
    Task<int> SeedSampleVolumeAsync(int count = 10000, CancellationToken ct = default);
}
