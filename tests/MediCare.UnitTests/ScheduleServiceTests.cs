using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediCare.UnitTests;

public class ScheduleServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UpdateDoctorSchedulesAsync_RejectsInvalidEndTime()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var dto = new UpdateWorkingHoursDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            Schedules = new List<DoctorScheduleItemDto>
            {
                new() { DayOfWeek = 1, StartTime = new TimeSpan(17, 0, 0), EndTime = new TimeSpan(9, 0, 0), SlotMinutes = 30 }
            }
        };

        var result = await service.UpdateDoctorSchedulesAsync(dto);

        Assert.False(result.Succeeded);
        Assert.Contains("End time must be after start time", result.Error);
    }

    [Fact]
    public async Task UpdateDoctorSchedulesAsync_RejectsNonDivisibleSlotLength()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        // 09:00 to 10:00 is 60 minutes. Slot length of 45 mins does not divide 60 evenly (60 % 45 != 0).
        var dto = new UpdateWorkingHoursDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            Schedules = new List<DoctorScheduleItemDto>
            {
                new() { DayOfWeek = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), SlotMinutes = 45 }
            }
        };

        var result = await service.UpdateDoctorSchedulesAsync(dto);

        Assert.False(result.Succeeded);
        Assert.Contains("must divide evenly", result.Error);
    }

    [Fact]
    public async Task UpdateDoctorSchedulesAsync_RejectsOverlappingWindowsOnSameDay()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        // Window 1: 09:00-13:00, Window 2: 12:00-16:00 (overlaps from 12:00 to 13:00)
        var dto = new UpdateWorkingHoursDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            Schedules = new List<DoctorScheduleItemDto>
            {
                new() { DayOfWeek = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(13, 0, 0), SlotMinutes = 30 },
                new() { DayOfWeek = 1, StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(16, 0, 0), SlotMinutes = 30 }
            }
        };

        var result = await service.UpdateDoctorSchedulesAsync(dto);

        Assert.False(result.Succeeded);
        Assert.Contains("Overlapping working hours", result.Error);
    }

    [Fact]
    public async Task UpdateDoctorSchedulesAsync_BR06_RejectsChangeWhenFutureAppointmentConflicts()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        var patient = new Patient
        {
            Id = 1,
            UserId = "pat-user-1",
            PatientCode = "P-1001",
            DateOfBirth = new DateOnly(1990, 1, 1),
            User = new ApplicationUser { Id = "pat-user-1", FullName = "Sara Patient", Email = "pat@test.com" }
        };

        // Next Monday date in the future
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var daysUntilNextMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilNextMonday == 0) daysUntilNextMonday = 7;
        var nextMonday = today.AddDays(daysUntilNextMonday);

        // Doctor currently has a Confirmed appointment next Monday at 14:00-14:30
        var appointment = new Appointment
        {
            Id = 10,
            Doctor = doctor,
            Patient = patient,
            AppointmentDate = nextMonday,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(14, 30, 0),
            Status = AppointmentStatus.Confirmed
        };

        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        // Doctor attempts to change Monday hours to only morning: 09:00 to 12:00
        var dto = new UpdateWorkingHoursDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            Schedules = new List<DoctorScheduleItemDto>
            {
                new() { DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(12, 0, 0), SlotMinutes = 30 }
            }
        };

        var result = await service.UpdateDoctorSchedulesAsync(dto);

        // Must fail with BR-06 conflict details and not save!
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Conflicts);
        Assert.Equal(10, result.Conflicts[0].AppointmentId);
        Assert.Equal("Sara Patient", result.Conflicts[0].PatientName);
        Assert.Equal("P-1001", result.Conflicts[0].PatientCode);
        Assert.Contains("Falls outside proposed working hours", result.Conflicts[0].ConflictReason);

        // Verify database schedules were NOT modified
        var savedSchedules = await db.DoctorSchedules.Where(s => s.DoctorId == 1).ToListAsync();
        Assert.Empty(savedSchedules);
    }

    [Fact]
    public async Task AddVacationDayAsync_BR06_RejectsVacationWhenAppointmentExistsOnDate()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        var patient = new Patient
        {
            Id = 1,
            UserId = "pat-user-1",
            PatientCode = "P-1002",
            DateOfBirth = new DateOnly(1992, 2, 2),
            User = new ApplicationUser { Id = "pat-user-1", FullName = "Mona Patient", Email = "mona@test.com" }
        };

        var futureDate = DateOnly.FromDateTime(ScheduleService.GetClinicLocalNow()).AddDays(5);

        var appointment = new Appointment
        {
            Id = 20,
            Doctor = doctor,
            Patient = patient,
            AppointmentDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Status = AppointmentStatus.Pending
        };

        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var addDto = new AddVacationDayDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            VacationDate = futureDate,
            Reason = "Emergency Leave"
        };

        var result = await service.AddVacationDayAsync(addDto);

        // Must reject with BR-06 conflict details
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Conflicts);
        Assert.Equal(20, result.Conflicts[0].AppointmentId);
        Assert.Equal("Mona Patient", result.Conflicts[0].PatientName);

        // Verify vacation was NOT added
        var vacations = await db.VacationDays.Where(v => v.DoctorId == 1).ToListAsync();
        Assert.Empty(vacations);
    }

    [Fact]
    public async Task AddVacationDayAsync_Succeeds_WhenNoAppointmentsExistOnDate()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user-1",
            Location = "Cairo",
            ConsultationFee = 500,
            User = new ApplicationUser { Id = "doc-user-1", FullName = "Dr. Test", Email = "doc@test.com" }
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var futureDate = DateOnly.FromDateTime(ScheduleService.GetClinicLocalNow()).AddDays(10);

        var addDto = new AddVacationDayDto
        {
            DoctorId = 1,
            UserId = "doc-user-1",
            VacationDate = futureDate,
            Reason = "Personal Holiday"
        };

        var result = await service.AddVacationDayAsync(addDto);

        Assert.True(result.Succeeded);
        var vacations = await db.VacationDays.Where(v => v.DoctorId == 1).ToListAsync();
        Assert.Single(vacations);
        Assert.Equal(futureDate, vacations[0].VacationDate);
        Assert.Equal("Personal Holiday", vacations[0].Reason);
    }
}
