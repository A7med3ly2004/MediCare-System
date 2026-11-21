using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Admin;
using MediCare.Services.Interfaces;
using MediCare.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediCare.UnitTests;

public class AdminServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private class TestEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

        public Task<bool> SendAsync(string to, string subject, string htmlBody)
        {
            SentEmails.Add((to, subject, htmlBody));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task DailyReport_ReturnsZeros_WhenNoActivityForDate()
    {
        using var db = CreateInMemoryDbContext();
        var reportService = new ReportService(db, NullLogger<ReportService>.Instance);

        var report = await reportService.GetDailyReportAsync(new DateOnly(2026, 12, 1));

        Assert.NotNull(report);
        Assert.Equal(new DateOnly(2026, 12, 1), report.Date);
        Assert.Equal(0, report.NewPatientRegistrations);
        Assert.Equal(0, report.NewDoctorRegistrations);
        Assert.Equal(0, report.AppointmentsBooked);
        Assert.Equal(0, report.AppointmentsConfirmed);
        Assert.Equal(0, report.AppointmentsCancelled);
        Assert.Equal(0, report.CompletedConsultations);
        Assert.Equal(0m, report.EstimatedRevenue);
    }

    [Fact]
    public async Task DailyReport_AccuratelyAggregatesMetrics_ForGivenDate()
    {
        using var db = CreateInMemoryDbContext();
        var targetDate = new DateOnly(2026, 11, 20);
        var targetDateTimeUtc = targetDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);

        // Add 1 patient registered on target date
        var patientUser = new ApplicationUser
        {
            Id = "p-user-1",
            FullName = "Patient One",
            Email = "p1@test.com",
            CreatedAt = targetDateTimeUtc
        };
        db.Users.Add(patientUser);
        var patient = new Patient { Id = 1, UserId = patientUser.Id, User = patientUser };
        db.Patients.Add(patient);

        // Add 1 doctor registered on target date
        var doctorUser = new ApplicationUser
        {
            Id = "d-user-1",
            FullName = "Doctor One",
            Email = "d1@test.com",
            CreatedAt = targetDateTimeUtc
        };
        db.Users.Add(doctorUser);
        var spec = new Specialization { Id = 1, Name = "Cardiology" };
        db.Specializations.Add(spec);
        var doctor = new Doctor { Id = 1, UserId = doctorUser.Id, SpecializationId = spec.Id, ConsultationFee = 350m, User = doctorUser };
        db.Doctors.Add(doctor);

        // Add appointments on target date
        // 1 Confirmed
        db.Appointments.Add(new Appointment
        {
            Id = 1,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = targetDate,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(9, 30, 0),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = targetDateTimeUtc
        });

        // 1 Cancelled
        db.Appointments.Add(new Appointment
        {
            Id = 2,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = targetDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Status = AppointmentStatus.Cancelled,
            CreatedAt = targetDateTimeUtc
        });

        // 1 Completed with consultation fee
        db.Appointments.Add(new Appointment
        {
            Id = 3,
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            AppointmentDate = targetDate,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Status = AppointmentStatus.Completed,
            CreatedAt = targetDateTimeUtc,
            Doctor = doctor
        });

        await db.SaveChangesAsync();

        var reportService = new ReportService(db, NullLogger<ReportService>.Instance);
        var report = await reportService.GetDailyReportAsync(targetDate);

        Assert.Equal(1, report.NewPatientRegistrations);
        Assert.Equal(1, report.NewDoctorRegistrations);
        Assert.Equal(3, report.AppointmentsBooked);
        Assert.Equal(1, report.AppointmentsConfirmed);
        Assert.Equal(1, report.AppointmentsCancelled);
        Assert.Equal(1, report.CompletedConsultations);
        Assert.Equal(350m, report.EstimatedRevenue);
    }

    [Fact]
    public async Task DeleteDoctor_WithFuturePendingOrConfirmedAppointments_RefusesDeletion()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var user = new ApplicationUser { Id = "doc-u1", FullName = "Dr. Test", Email = "doc@test.com" };
        var spec = new Specialization { Id = 1, Name = "Dermatology" };
        var doc = new Doctor { Id = 10, UserId = user.Id, SpecializationId = spec.Id, User = user };
        var pat = new Patient { Id = 20, UserId = "pat-u1" };

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var appt = new Appointment
        {
            Id = 100,
            DoctorId = doc.Id,
            PatientId = pat.Id,
            AppointmentDate = futureDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Status = AppointmentStatus.Confirmed
        };

        db.Users.Add(user);
        db.Specializations.Add(spec);
        db.Doctors.Add(doc);
        db.Patients.Add(pat);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        var (success, error) = await service.DeleteDoctorAsync(doc.Id);

        Assert.False(success);
        Assert.Contains("future", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("suspend", error, StringComparison.OrdinalIgnoreCase);

        // Verify doctor was NOT deleted
        var freshDoc = await db.Doctors.FindAsync(doc.Id);
        Assert.False(freshDoc!.IsDeleted);
    }

    [Fact]
    public async Task DeleteDoctor_WithNoFutureAppointments_SucceedsSoftDelete()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var user = new ApplicationUser { Id = "doc-u2", FullName = "Dr. Free", Email = "doc2@test.com" };
        var spec = new Specialization { Id = 1, Name = "Dermatology" };
        var doc = new Doctor { Id = 11, UserId = user.Id, SpecializationId = spec.Id, User = user };

        db.Users.Add(user);
        db.Specializations.Add(spec);
        db.Doctors.Add(doc);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        var (success, error) = await service.DeleteDoctorAsync(doc.Id);

        Assert.True(success);
        Assert.Null(error);

        var freshDoc = await db.Doctors.FindAsync(doc.Id);
        var freshUser = await db.Users.FindAsync(user.Id);
        Assert.True(freshDoc!.IsDeleted);
        Assert.True(freshUser!.IsDeleted);
    }

    [Fact]
    public async Task DeletePatient_WithFutureAppointments_RefusesDeletion()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var user = new ApplicationUser { Id = "pat-u2", FullName = "Pat With Appt", Email = "pat2@test.com" };
        var patient = new Patient { Id = 30, UserId = user.Id, User = user };
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var appt = new Appointment
        {
            Id = 101,
            DoctorId = 1,
            PatientId = patient.Id,
            AppointmentDate = futureDate,
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Status = AppointmentStatus.Pending
        };

        db.Users.Add(user);
        db.Patients.Add(patient);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        var (success, error) = await service.DeletePatientAsync(patient.Id);

        Assert.False(success);
        Assert.Contains("future", error, StringComparison.OrdinalIgnoreCase);

        var freshPatient = await db.Patients.FindAsync(patient.Id);
        Assert.False(freshPatient!.IsDeleted);
    }

    [Fact]
    public async Task DeleteSpecialization_InUseByActiveDoctor_RefusesDeletion()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var spec = new Specialization { Id = 5, Name = "Neurology", IsDeleted = false };
        var doc = new Doctor { Id = 40, UserId = "u-doc-neu", SpecializationId = spec.Id, IsDeleted = false };

        db.Specializations.Add(spec);
        db.Doctors.Add(doc);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        var (success, error) = await service.DeleteSpecializationAsync(spec.Id);

        Assert.False(success);
        Assert.Contains("in use", error, StringComparison.OrdinalIgnoreCase);

        var freshSpec = await db.Specializations.FindAsync(spec.Id);
        Assert.False(freshSpec!.IsDeleted);
    }

    [Fact]
    public async Task DeleteSpecialization_NotInUse_SucceedsSoftDelete()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var spec = new Specialization { Id = 6, Name = "Unused Spec", IsDeleted = false };
        db.Specializations.Add(spec);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        var (success, error) = await service.DeleteSpecializationAsync(spec.Id);

        Assert.True(success);
        Assert.Null(error);

        var freshSpec = await db.Specializations.FindAsync(spec.Id);
        Assert.True(freshSpec!.IsDeleted);
    }

    [Fact]
    public async Task ToggleDoctorSuspend_TogglesStateCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var emailSender = new TestEmailSender();

        var user = new ApplicationUser { Id = "doc-sus", FullName = "Dr. Suspendable", Email = "sus@test.com", IsSuspended = false };
        var doc = new Doctor { Id = 50, UserId = user.Id, SpecializationId = 1, User = user };

        db.Users.Add(user);
        db.Doctors.Add(doc);
        await db.SaveChangesAsync();

        var service = new AdminManagementService(uow, db, emailSender, NullLogger<AdminManagementService>.Instance);

        // Toggle to true
        var res1 = await service.ToggleDoctorSuspensionAsync(doc.Id);
        Assert.True(res1);
        var u1 = await db.Users.FindAsync(user.Id);
        Assert.True(u1!.IsSuspended);

        // Toggle back to false
        var res2 = await service.ToggleDoctorSuspensionAsync(doc.Id);
        Assert.True(res2);
        var u2 = await db.Users.FindAsync(user.Id);
        Assert.False(u2!.IsSuspended);
    }

    [Fact]
    public async Task ExportDailyReportToExcel_GeneratesValidExcelBytes()
    {
        var exportService = new ReportExportService(NullLogger<ReportExportService>.Instance);
        var report = new DailyReportDto
        {
            Date = new DateOnly(2026, 11, 21),
            NewPatientRegistrations = 5,
            NewDoctorRegistrations = 2,
            AppointmentsBooked = 14,
            AppointmentsConfirmed = 12,
            AppointmentsCancelled = 2,
            CompletedConsultations = 10,
            EstimatedRevenue = 3500m
        };

        var bytes = await exportService.ExportDailyReportToExcelAsync(report);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
        // Verify ZIP / XLSX magic bytes (PK..)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task ExportDailyReportToPdf_GeneratesValidPdfBytes()
    {
        var exportService = new ReportExportService(NullLogger<ReportExportService>.Instance);
        var report = new DailyReportDto
        {
            Date = new DateOnly(2026, 11, 21),
            NewPatientRegistrations = 5,
            NewDoctorRegistrations = 2,
            AppointmentsBooked = 14,
            AppointmentsConfirmed = 12,
            AppointmentsCancelled = 2,
            CompletedConsultations = 10,
            EstimatedRevenue = 3500m
        };

        var bytes = await exportService.ExportDailyReportToPdfAsync(report);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
        // Verify PDF magic bytes (%PDF)
        var header = System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray());
        Assert.Equal("%PDF", header);
    }
}
