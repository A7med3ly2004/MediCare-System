using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace MediCare.UnitTests;

public class AppointmentServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _data;
        public TestConfiguration(Dictionary<string, string?>? data = null) => _data = data ?? new();
        public string? this[string key] { get => _data.TryGetValue(key, out var val) ? val : null; set => _data[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private class TestNotificationService : INotificationService
    {
        public List<string> SentCalls { get; } = new();

        public Task<bool> SendOtpEmailAsync(string userId, string toEmail, string toName, string plainOtp)
        {
            SentCalls.Add($"Otp:{toEmail}:{plainOtp}");
            return Task.FromResult(true);
        }

        public Task<bool> SendBookingReceivedPatientAsync(
            string userId, string toEmail, string toName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room, decimal fee)
        {
            SentCalls.Add($"BookingReceivedPatient:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendBookingReceivedDoctorAsync(
            string userId, string toEmail, string doctorName, string patientName, string patientCode, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? notes)
        {
            SentCalls.Add($"BookingReceivedDoctor:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendAppointmentConfirmedAsync(
            string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
        {
            SentCalls.Add($"AppointmentConfirmed:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendAppointmentRejectedAsync(
            string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? reason)
        {
            SentCalls.Add($"AppointmentRejected:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendAppointmentCancelledAsync(
            string userId, string toEmail, string recipientName, string otherPartyName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string cancelledBy)
        {
            SentCalls.Add($"AppointmentCancelled:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendAppointmentRescheduledAsync(
            string userId, string toEmail, string recipientName, string otherPartyName, DateOnly newDate, TimeSpan newStartTime, TimeSpan newEndTime)
        {
            SentCalls.Add($"AppointmentRescheduled:{toEmail}");
            return Task.FromResult(true);
        }

        public Task<bool> SendReminderAsync(
            string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
        {
            SentCalls.Add($"AppointmentReminder:{toEmail}");
            return Task.FromResult(true);
        }
    }

    private static (ApplicationDbContext db, UnitOfWork uow, AppointmentService service, TestNotificationService notif, Doctor doctor, Patient patient) SetupEnvironment()
    {
        var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var notif = new TestNotificationService();
        var config = new TestConfiguration(new Dictionary<string, string?> { { "Booking:CancelLeadHours", "2" } });
        var scheduleService = new ScheduleService(uow, db, NullLogger<ScheduleService>.Instance);
        var appointmentService = new AppointmentService(uow, db, scheduleService, notif, config, NullLogger<AppointmentService>.Instance);

        var doctorUser = new ApplicationUser { Id = "doc-user", FullName = "Dr. Hazem", Email = "doc@clinic.com" };
        var patientUser = new ApplicationUser { Id = "patient-user", FullName = "Ahmed Aly", Email = "patient@test.com" };

        var specialization = new Specialization { Id = 1, Name = "Cardiology" };

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user",
            User = doctorUser,
            SpecializationId = 1,
            Specialization = specialization,
            Location = "Cairo Clinic",
            RoomNumber = "101",
            ConsultationFee = 400
        };

        var patient = new Patient
        {
            Id = 1,
            UserId = "patient-user",
            User = patientUser,
            PatientCode = "P-1001",
            DateOfBirth = new DateOnly(1990, 1, 1),
            BloodGroup = "O+"
        };

        db.Users.AddRange(doctorUser, patientUser);
        db.Specializations.Add(specialization);
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.SaveChanges();

        return (db, uow, appointmentService, notif, doctor, patient);
    }

    [Fact]
    public async Task CreateAsync_ValidSlot_CreatesPendingAppointmentAndSendsNotifications()
    {
        var (db, _, service, notif, doctor, patient) = SetupEnvironment();

        // Target date tomorrow
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var schedule = new DoctorSchedule
        {
            DoctorId = doctor.Id,
            DayOfWeek = (int)tomorrow.DayOfWeek,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            SlotMinutes = 30
        };
        db.DoctorSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var dto = new BookAppointmentDto
        {
            DoctorId = doctor.Id,
            PatientUserId = patient.UserId,
            AppointmentDate = tomorrow,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Notes = "Chest pain checkup"
        };

        var result = await service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Pending, result.Status);
        Assert.Equal(doctor.Id, result.DoctorId);
        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal("Chest pain checkup", result.Notes);

        // Verify saved in DB
        var apptInDb = await db.Appointments.FirstOrDefaultAsync(a => a.Id == result.Id);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Pending, apptInDb.Status);

        // Verify emails were triggered for both patient and doctor
        Assert.Contains(notif.SentCalls, c => c.StartsWith("BookingReceivedPatient:"));
        Assert.Contains(notif.SentCalls, c => c.StartsWith("BookingReceivedDoctor:"));
    }

    [Fact]
    public async Task CreateAsync_UnavailableSlot_ThrowsSlotUnavailableException()
    {
        var (db, _, service, _, doctor, patient) = SetupEnvironment();

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        // Doctor only works 14:00 to 16:00
        var schedule = new DoctorSchedule
        {
            DoctorId = doctor.Id,
            DayOfWeek = (int)tomorrow.DayOfWeek,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(16, 0, 0),
            SlotMinutes = 30
        };
        db.DoctorSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Requesting 09:00 AM (not in schedule)
        var dto = new BookAppointmentDto
        {
            DoctorId = doctor.Id,
            PatientUserId = patient.UserId,
            AppointmentDate = tomorrow,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(9, 30, 0)
        };

        await Assert.ThrowsAsync<SlotUnavailableException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CancelAsync_Within2Hours_ThrowsLeadTimeViolationException()
    {
        var (db, _, service, _, doctor, patient) = SetupEnvironment();

        // Appointment starting in 30 minutes
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var targetDateTime = clinicNow.AddMinutes(30);
        var apptDate = DateOnly.FromDateTime(targetDateTime);
        var apptStart = targetDateTime.TimeOfDay;

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = apptDate,
            StartTime = apptStart,
            EndTime = apptStart.Add(TimeSpan.FromMinutes(30)),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var cancelDto = new CancelAppointmentDto
        {
            AppointmentId = appointment.Id,
            UserId = patient.UserId,
            UserRole = "Patient"
        };

        var ex = await Assert.ThrowsAsync<LeadTimeViolationException>(() => service.CancelAsync(cancelDto));
        Assert.Equal(2, ex.RequiredLeadHours);
    }

    [Fact]
    public async Task CancelAsync_ValidLeadTime_CancelsAndWritesAuditLog()
    {
        var (db, _, service, notif, doctor, patient) = SetupEnvironment();

        // Appointment 2 days from now
        var apptDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var apptStart = new TimeSpan(11, 0, 0);

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = apptDate,
            StartTime = apptStart,
            EndTime = apptStart.Add(TimeSpan.FromMinutes(30)),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var cancelDto = new CancelAppointmentDto
        {
            AppointmentId = appointment.Id,
            UserId = patient.UserId,
            UserRole = "Patient",
            Reason = "Personal emergency"
        };

        var result = await service.CancelAsync(cancelDto);

        Assert.Equal(AppointmentStatus.Cancelled, result.Status);

        // Verify status in DB
        var apptInDb = await db.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Cancelled, apptInDb.Status);

        // Verify AuditLog written (BR-08)
        var audit = await db.AuditLogs.FirstOrDefaultAsync(l => l.EntityId == appointment.Id.ToString() && l.Action == "Cancel");
        Assert.NotNull(audit);
        Assert.Equal(patient.UserId, audit.UserId);

        // Verify notification sent
        Assert.Contains(notif.SentCalls, c => c.StartsWith("AppointmentCancelled:"));
    }

    [Fact]
    public async Task CancelAsync_UnauthorizedPatient_ThrowsUnauthorizedAccessException()
    {
        var (db, _, service, _, doctor, patient) = SetupEnvironment();

        var apptDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = apptDate,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var cancelDto = new CancelAppointmentDto
        {
            AppointmentId = appointment.Id,
            UserId = "other-patient-user",
            UserRole = "Patient"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CancelAsync(cancelDto));
    }

    [Fact]
    public async Task ConfirmAsync_PendingAppointment_SetsConfirmedAndWritesAuditLog()
    {
        var (db, _, service, notif, doctor, patient) = SetupEnvironment();

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(14, 30, 0),
            Status = AppointmentStatus.Pending
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var confirmDto = new ConfirmRejectDto
        {
            AppointmentId = appointment.Id,
            UserId = doctor.UserId,
            UserRole = "Doctor",
            DoctorId = doctor.Id
        };

        var result = await service.ConfirmAsync(confirmDto);

        Assert.Equal(AppointmentStatus.Confirmed, result.Status);

        var apptInDb = await db.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Confirmed, apptInDb.Status);

        // Verify AuditLog
        var audit = await db.AuditLogs.FirstOrDefaultAsync(l => l.EntityId == appointment.Id.ToString() && l.Action == "Confirm");
        Assert.NotNull(audit);
        Assert.Equal(doctor.UserId, audit.UserId);

        // Verify email notification
        Assert.Contains(notif.SentCalls, c => c.StartsWith("AppointmentConfirmed:"));
    }

    [Fact]
    public async Task ConfirmAsync_NonPendingAppointment_ThrowsInvalidStateTransitionException()
    {
        var (db, _, service, _, doctor, patient) = SetupEnvironment();

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(14, 30, 0),
            Status = AppointmentStatus.Cancelled // Already cancelled
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var confirmDto = new ConfirmRejectDto
        {
            AppointmentId = appointment.Id,
            UserId = doctor.UserId,
            UserRole = "Doctor",
            DoctorId = doctor.Id
        };

        await Assert.ThrowsAsync<InvalidStateTransitionException>(() => service.ConfirmAsync(confirmDto));
    }

    [Fact]
    public async Task RejectAsync_PendingAppointment_SetsRejectedAndWritesAuditLog()
    {
        var (db, _, service, notif, doctor, patient) = SetupEnvironment();

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            StartTime = new TimeSpan(15, 0, 0),
            EndTime = new TimeSpan(15, 30, 0),
            Status = AppointmentStatus.Pending
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var rejectDto = new ConfirmRejectDto
        {
            AppointmentId = appointment.Id,
            UserId = doctor.UserId,
            UserRole = "Doctor",
            DoctorId = doctor.Id
        };

        var result = await service.RejectAsync(rejectDto, "Doctor in surgery");

        Assert.Equal(AppointmentStatus.Rejected, result.Status);

        var apptInDb = await db.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Rejected, apptInDb.Status);

        // Verify AuditLog
        var audit = await db.AuditLogs.FirstOrDefaultAsync(l => l.EntityId == appointment.Id.ToString() && l.Action == "Reject");
        Assert.NotNull(audit);
        Assert.Equal(doctor.UserId, audit.UserId);

        // Verify email notification
        Assert.Contains(notif.SentCalls, c => c.StartsWith("AppointmentRejected:"));
    }
}
