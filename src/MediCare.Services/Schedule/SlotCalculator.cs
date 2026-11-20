using MediCare.Data.Entities;

namespace MediCare.Services.Schedule;

/// <summary>
/// Pure calculation component for generating available appointment slots.
/// Free of database and framework dependencies; easily testable.
/// </summary>
public static class SlotCalculator
{
    /// <summary>
    /// Calculates all unbooked, future time slots within a doctor's working windows.
    /// Excludes vacation days, overlapping active appointments, and past times.
    /// </summary>
    public static List<FreeSlotDto> CalculateFreeSlots(
        IEnumerable<DoctorSchedule> schedules,
        IEnumerable<VacationDay> vacationDays,
        IEnumerable<Appointment> activeAppointments,
        DateOnly fromDate,
        DateOnly toDate,
        DateTime clinicLocalNow)
    {
        var freeSlots = new List<FreeSlotDto>();

        if (fromDate > toDate) return freeSlots;

        var vacationDateSet = vacationDays
            .Select(v => v.VacationDate)
            .ToHashSet();

        // Group appointments by date for O(1) daily lookup
        var appointmentsByDate = activeAppointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected)
            .GroupBy(a => a.AppointmentDate)
            .ToDictionary(g => g.Key, g => g.ToList());

        var today = DateOnly.FromDateTime(clinicLocalNow);
        var nowTime = clinicLocalNow.TimeOfDay;

        // Group schedules by DayOfWeek (a day may have multiple non-overlapping windows)
        var schedulesByDay = schedules
            .Where(s => s.EndTime > s.StartTime && s.SlotMinutes > 0)
            .GroupBy(s => s.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());

        for (var currentDate = fromDate; currentDate <= toDate; currentDate = currentDate.AddDays(1))
        {
            // If the date is entirely in the past, skip
            if (currentDate < today) continue;

            // Exclude vacation days
            if (vacationDateSet.Contains(currentDate)) continue;

            var dayOfWeek = (int)currentDate.DayOfWeek;
            if (!schedulesByDay.TryGetValue(dayOfWeek, out var dayWindows) || !dayWindows.Any())
            {
                continue;
            }

            appointmentsByDate.TryGetValue(currentDate, out var dayAppointments);
            dayAppointments ??= new List<Appointment>();

            foreach (var window in dayWindows)
            {
                var slotDuration = TimeSpan.FromMinutes(window.SlotMinutes);
                var slotStart = window.StartTime;

                while (slotStart + slotDuration <= window.EndTime)
                {
                    var slotEnd = slotStart + slotDuration;

                    // If slot is today but already in the past, exclude
                    if (currentDate == today && slotStart <= nowTime)
                    {
                        slotStart = slotEnd;
                        continue;
                    }

                    // Check if any active appointment overlaps with [slotStart, slotEnd)
                    var isBooked = dayAppointments.Any(a =>
                        a.StartTime < slotEnd && a.EndTime > slotStart);

                    if (!isBooked)
                    {
                        freeSlots.Add(new FreeSlotDto
                        {
                            SlotId = $"{currentDate:yyyyMMdd}-{slotStart:hhmm}-{slotEnd:hhmm}",
                            Date = currentDate,
                            StartTime = slotStart,
                            EndTime = slotEnd,
                            SlotMinutes = window.SlotMinutes
                        });
                    }

                    slotStart = slotEnd;
                }
            }
        }

        return freeSlots;
    }

    /// <summary>
    /// Fast check to determine if a doctor has at least one free slot on a specific date.
    /// Exits early on the first available slot.
    /// </summary>
    public static bool HasAnyFreeSlot(
        IEnumerable<DoctorSchedule> schedules,
        IEnumerable<VacationDay> vacationDays,
        IEnumerable<Appointment> activeAppointments,
        DateOnly date,
        DateTime clinicLocalNow)
    {
        var today = DateOnly.FromDateTime(clinicLocalNow);
        if (date < today) return false;

        // Check vacation
        if (vacationDays.Any(v => v.VacationDate == date)) return false;

        var dayOfWeek = (int)date.DayOfWeek;
        var dayWindows = schedules
            .Where(s => s.DayOfWeek == dayOfWeek && s.EndTime > s.StartTime && s.SlotMinutes > 0)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (!dayWindows.Any()) return false;

        var dayAppointments = activeAppointments
            .Where(a => a.AppointmentDate == date &&
                        a.Status != AppointmentStatus.Cancelled &&
                        a.Status != AppointmentStatus.Rejected)
            .ToList();

        var nowTime = clinicLocalNow.TimeOfDay;

        foreach (var window in dayWindows)
        {
            var slotDuration = TimeSpan.FromMinutes(window.SlotMinutes);
            var slotStart = window.StartTime;

            while (slotStart + slotDuration <= window.EndTime)
            {
                var slotEnd = slotStart + slotDuration;

                if (date == today && slotStart <= nowTime)
                {
                    slotStart = slotEnd;
                    continue;
                }

                var isBooked = dayAppointments.Any(a =>
                    a.StartTime < slotEnd && a.EndTime > slotStart);

                if (!isBooked)
                {
                    return true;
                }

                slotStart = slotEnd;
            }
        }

        return false;
    }
}
