using MediCare.Services.Appointments;

namespace MediCare.Services.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentDetailDto> CreateAsync(BookAppointmentDto dto, CancellationToken ct = default);

    Task<AppointmentDetailDto> CancelAsync(CancelAppointmentDto dto, CancellationToken ct = default);

    Task<AppointmentDetailDto> RescheduleAsync(RescheduleAppointmentDto dto, CancellationToken ct = default);

    Task<AppointmentDetailDto> ConfirmAsync(ConfirmRejectDto dto, CancellationToken ct = default);

    Task<AppointmentDetailDto> RejectAsync(ConfirmRejectDto dto, string? reason = null, CancellationToken ct = default);

    Task<AppointmentDetailDto?> GetAppointmentDetailsAsync(int appointmentId, CancellationToken ct = default);

    Task<List<AppointmentListItemDto>> GetPatientAppointmentsAsync(string patientUserId, bool? upcoming = null, CancellationToken ct = default);

    Task<DoctorDashboardDto> GetDoctorDashboardDataAsync(string doctorUserId, CancellationToken ct = default);

    Task<PatientDashboardDto> GetPatientDashboardDataAsync(string patientUserId, CancellationToken ct = default);
}
