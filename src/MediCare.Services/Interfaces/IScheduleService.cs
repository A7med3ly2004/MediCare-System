using MediCare.Services.Account;
using MediCare.Services.Schedule;

namespace MediCare.Services.Interfaces;

public interface IScheduleService
{
    Task<List<DoctorScheduleItemDto>> GetDoctorSchedulesAsync(int doctorId, CancellationToken ct = default);

    Task<List<VacationDayDto>> GetDoctorVacationsAsync(int doctorId, CancellationToken ct = default);

    Task<ScheduleOperationResult> UpdateDoctorSchedulesAsync(UpdateWorkingHoursDto dto, CancellationToken ct = default);

    Task<ScheduleOperationResult> AddVacationDayAsync(AddVacationDayDto dto, CancellationToken ct = default);

    Task<ServiceResult> RemoveVacationDayAsync(int vacationId, string userId, CancellationToken ct = default);

    Task<List<FreeSlotDto>> GetFreeSlotsAsync(int doctorId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    Task<bool> HasFreeSlotsOnDateAsync(int doctorId, DateOnly date, CancellationToken ct = default);
}
