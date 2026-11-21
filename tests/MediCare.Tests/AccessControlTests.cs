using System.Net;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Account;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.MedicalRecords;
using Microsoft.Extensions.DependencyInjection;

namespace MediCare.Tests;

public class AccessControlTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccessControlTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseSeeded();
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Doctors")]
    [InlineData("/Admin/Patients")]
    [InlineData("/Admin/Appointments")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/Notifications")]
    [InlineData("/Admin/Specializations")]
    [InlineData("/Doctor")]
    [InlineData("/Doctor/Schedule")]
    [InlineData("/Doctor/MedicalRecords")]
    [InlineData("/Patient")]
    [InlineData("/Patient/Appointments")]
    public async Task AnonymousUser_AccessingProtectedEndpoints_IsRedirectedToLogin(string url)
    {
        var client = _factory.CreateClientForRole("Anonymous");
        var response = await client.GetAsync(url);

        // Anonymous access is rejected: 401 Unauthorized or 302 Redirect to Login
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("/Account/Login") == true),
            $"Expected 401 Unauthorized or 302 Redirect to Login for anonymous accessing {url}, got {response.StatusCode}");
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Doctors")]
    [InlineData("/Admin/Patients")]
    [InlineData("/Admin/Appointments")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Doctor")]
    [InlineData("/Doctor/Schedule")]
    public async Task PatientRole_AccessingAdminOrDoctorEndpoints_IsRefused(string url)
    {
        var client = _factory.CreateClientForRole("Patient", "test-pat-1");
        var response = await client.GetAsync(url);

        // Either Forbidden (403) or redirect (302) to AccessDenied
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("AccessDenied") == true),
            $"Expected Forbidden or Redirect to AccessDenied for Patient accessing {url}, got {response.StatusCode} -> {response.Headers.Location}");
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Patient")]
    [InlineData("/Patient/Appointments")]
    public async Task DoctorRole_AccessingAdminOrPatientEndpoints_IsRefused(string url)
    {
        var client = _factory.CreateClientForRole("Doctor", "test-doc-1");
        var response = await client.GetAsync(url);

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("AccessDenied") == true),
            $"Expected Forbidden or Redirect to AccessDenied for Doctor accessing {url}, got {response.StatusCode} -> {response.Headers.Location}");
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/Doctors")]
    [InlineData("/Admin/Patients")]
    [InlineData("/Admin/Appointments")]
    [InlineData("/Admin/Specializations")]
    [InlineData("/Admin/Notifications")]
    public async Task AdminRole_AccessingAdminEndpoints_SucceedsWithOk(string url)
    {
        var client = _factory.CreateClientForRole("Admin", "test-admin-1");
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DataOwnership_PatientAttemptingToCancelAnotherPatientsAppointment_IsRefused()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();

        // Create appointment belonging to Patient 2 ("test-pat-2")
        var appt = new Appointment
        {
            DoctorId = 1,
            PatientId = 2,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Patient 1 ("test-pat-1") attempts to cancel it
        var cancelDto = new CancelAppointmentDto
        {
            AppointmentId = appt.Id,
            UserId = "test-pat-1", // Not the owner
            UserRole = "Patient"
        };

        // NFR-06: Must be refused with UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => appointmentService.CancelAsync(cancelDto));
    }

    [Fact]
    public async Task DataOwnership_DoctorAttemptingToRecordVisitForAnotherDoctorsAppointment_IsRefused()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recordService = scope.ServiceProvider.GetRequiredService<IMedicalRecordService>();

        // Create confirmed appointment belonging to Doctor 1 ("test-doc-1")
        var appt = new Appointment
        {
            DoctorId = 1,
            PatientId = 1,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeSpan(12, 0, 0),
            EndTime = new TimeSpan(12, 30, 0),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Doctor 2 ("test-doc-2") attempts to save visit
        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appt.Id,
            DoctorUserId = "test-doc-2", // Different doctor
            Diagnosis = "Cardiac checkup",
            BloodPressure = "120/80"
        };

        // NFR-06 / BR-11: Must be refused
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => recordService.SaveVisitAsync(dto));
    }

    [Fact]
    public async Task AccountStatus_UnverifiedSuspendedOrDeletedUser_CannotLogIn()
    {
        using var scope = _factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();

        // 1. Unverified user
        var unverifiedLogin = await accountService.LoginAsync("unverified@medicare.test", "Password#12345");
        Assert.False(unverifiedLogin.Succeeded);
        Assert.True(unverifiedLogin.IsUnverified);

        // 2. Suspended user
        var suspendedLogin = await accountService.LoginAsync("suspended@medicare.test", "Password#12345");
        Assert.False(suspendedLogin.Succeeded);
        Assert.True(suspendedLogin.IsSuspended);

        // 3. Deleted user
        var deletedLogin = await accountService.LoginAsync("deleted@medicare.test", "Password#12345");
        Assert.False(deletedLogin.Succeeded);
        Assert.True(deletedLogin.IsDeleted);

        // 4. Valid active verified user
        var validLogin = await accountService.LoginAsync("patient@medicare.test", "Password#12345");
        Assert.True(validLogin.Succeeded);
    }
}
