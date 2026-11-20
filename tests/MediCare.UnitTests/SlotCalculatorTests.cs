using MediCare.Data.Entities;
using MediCare.Services.Schedule;

namespace MediCare.UnitTests;

public class SlotCalculatorTests
{
    private static readonly DateTime FixedClinicNow = new(2026, 11, 23, 8, 0, 0); // Monday 08:00 AM
    private static readonly DateOnly TargetMonday = new(2026, 11, 23);

    [Fact]
    public void CalculateFreeSlots_SingleWindow_ReturnsExpectedSlots()
    {
        // 09:00 to 11:00 with 30-minute slots -> exactly 4 slots
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), SlotMinutes = 30 }
        };

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: Enumerable.Empty<Appointment>(),
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.Equal(4, slots.Count);
        Assert.Equal(new TimeSpan(9, 0, 0), slots[0].StartTime);
        Assert.Equal(new TimeSpan(9, 30, 0), slots[0].EndTime);
        Assert.Equal(new TimeSpan(9, 30, 0), slots[1].StartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), slots[2].StartTime);
        Assert.Equal(new TimeSpan(10, 30, 0), slots[3].StartTime);
    }

    [Fact]
    public void CalculateFreeSlots_ExcludesVacationDays()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), SlotMinutes = 30 }
        };

        var vacations = new List<VacationDay>
        {
            new() { DoctorId = 1, VacationDate = TargetMonday, Reason = "National Holiday" }
        };

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacations,
            activeAppointments: Enumerable.Empty<Appointment>(),
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.Empty(slots);
    }

    [Fact]
    public void CalculateFreeSlots_ExcludesBookedAppointments()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), SlotMinutes = 30 }
        };

        // Book slot 09:30-10:00 (Pending) and 10:30-11:00 (Confirmed)
        var appointments = new List<Appointment>
        {
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 0, 0), Status = AppointmentStatus.Pending },
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 0, 0), Status = AppointmentStatus.Confirmed }
        };

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: appointments,
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new TimeSpan(9, 0, 0), slots[0].StartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), slots[1].StartTime);
    }

    [Fact]
    public void CalculateFreeSlots_CancelledAndRejectedAppointments_DoNotBlockSlots()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), SlotMinutes = 30 }
        };

        // Two appointments on 09:00 and 09:30, but one is Cancelled and one is Rejected
        var appointments = new List<Appointment>
        {
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = AppointmentStatus.Cancelled },
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 0, 0), Status = AppointmentStatus.Rejected }
        };

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: appointments,
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        // Both slots should be free because cancelled and rejected don't block
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void CalculateFreeSlots_ExcludesPastSlotsOnCurrentDay()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(12, 0, 0), SlotMinutes = 30 }
        };

        // Current time is Monday 10:15 AM
        var currentLocalTime = new DateTime(2026, 11, 23, 10, 15, 0);

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: Enumerable.Empty<Appointment>(),
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: currentLocalTime);

        // 09:00, 09:30, and 10:00 started before or at 10:15 -> excluded
        // Only 10:30, 11:00, 11:30 should be available
        Assert.Equal(3, slots.Count);
        Assert.Equal(new TimeSpan(10, 30, 0), slots[0].StartTime);
        Assert.Equal(new TimeSpan(11, 0, 0), slots[1].StartTime);
        Assert.Equal(new TimeSpan(11, 30, 0), slots[2].StartTime);
    }

    [Fact]
    public void CalculateFreeSlots_MultipleWindowsPerDay_CalculatesAllShifts()
    {
        var schedules = new List<DoctorSchedule>
        {
            // Morning shift: 09:00-11:00 (30m) -> 4 slots
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), SlotMinutes = 30 },
            // Evening shift: 17:00-19:00 (30m) -> 4 slots
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(17, 0, 0), EndTime = new TimeSpan(19, 0, 0), SlotMinutes = 30 }
        };

        var slots = SlotCalculator.CalculateFreeSlots(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: Enumerable.Empty<Appointment>(),
            fromDate: TargetMonday,
            toDate: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.Equal(8, slots.Count);
        Assert.Equal(new TimeSpan(9, 0, 0), slots[0].StartTime);
        Assert.Equal(new TimeSpan(17, 0, 0), slots[4].StartTime);
    }

    [Fact]
    public void HasAnyFreeSlot_ReturnsFalse_WhenAllSlotsBooked()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), SlotMinutes = 30 }
        };

        var appointments = new List<Appointment>
        {
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = AppointmentStatus.Confirmed },
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 0, 0), Status = AppointmentStatus.Pending }
        };

        var hasSlots = SlotCalculator.HasAnyFreeSlot(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: appointments,
            date: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.False(hasSlots);
    }

    [Fact]
    public void HasAnyFreeSlot_ReturnsTrue_WhenAtLeastOneSlotAvailable()
    {
        var schedules = new List<DoctorSchedule>
        {
            new() { DoctorId = 1, DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), SlotMinutes = 30 }
        };

        var appointments = new List<Appointment>
        {
            new() { DoctorId = 1, AppointmentDate = TargetMonday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = AppointmentStatus.Confirmed }
        };

        var hasSlots = SlotCalculator.HasAnyFreeSlot(
            schedules,
            vacationDays: Enumerable.Empty<VacationDay>(),
            activeAppointments: appointments,
            date: TargetMonday,
            clinicLocalNow: FixedClinicNow);

        Assert.True(hasSlots);
    }
}
