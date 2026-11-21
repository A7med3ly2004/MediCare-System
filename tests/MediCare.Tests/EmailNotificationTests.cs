using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MediCare.Tests;

public class EmailNotificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailNotificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseSeeded();
    }

    [Fact]
    public async Task AppointmentLifecycle_SendsCorrectEmailTypeForEveryEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
        var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var fakeEmailSender = _factory.FakeEmailSender;

        fakeEmailSender.SentEmails.Clear();

        // 1. Send OTP
        await otpService.SendOtpAsync("test-unverified-1");
        Assert.Contains(fakeEmailSender.SentEmails, e => e.Subject.Contains("Verify") || e.Subject.Contains("Code") || e.Subject.Contains("OTP"));

        fakeEmailSender.SentEmails.Clear();

        // 2. Book appointment
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var startTime = new TimeSpan(14, 0, 0);
        var endTime = new TimeSpan(14, 30, 0);

        var created = await appointmentService.CreateAsync(new BookAppointmentDto
        {
            DoctorId = 1,
            PatientUserId = "test-pat-1",
            AppointmentDate = targetDate,
            StartTime = startTime,
            EndTime = endTime,
            Notes = "Checkup"
        });

        // Booking notification must be sent to patient and doctor
        Assert.True(fakeEmailSender.SentEmails.Count >= 2);
        Assert.Contains(fakeEmailSender.SentEmails, e => e.To == "patient@medicare.test");
        Assert.Contains(fakeEmailSender.SentEmails, e => e.To == "doctor@medicare.test");

        fakeEmailSender.SentEmails.Clear();

        // 3. Confirm appointment
        await appointmentService.ConfirmAsync(new ConfirmRejectDto
        {
            AppointmentId = created.Id,
            UserId = "test-doc-1",
            UserRole = "Doctor",
            DoctorId = 1
        });

        // Confirmation email must be sent
        Assert.Contains(fakeEmailSender.SentEmails, e => e.To == "patient@medicare.test" && (e.Subject.Contains("Confirmed") || e.Body.Contains("Confirmed")));

        fakeEmailSender.SentEmails.Clear();

        // 4. Reschedule appointment
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        await appointmentService.RescheduleAsync(new RescheduleAppointmentDto
        {
            AppointmentId = created.Id,
            PatientUserId = "test-pat-1",
            NewDate = newDate,
            NewStartTime = new TimeSpan(15, 0, 0),
            NewEndTime = new TimeSpan(15, 30, 0)
        });

        // Reschedule email must be sent
        Assert.True(fakeEmailSender.SentEmails.Count >= 1);
        Assert.Contains(fakeEmailSender.SentEmails, e => e.Subject.Contains("Rescheduled") || e.Body.Contains("Rescheduled"));

        fakeEmailSender.SentEmails.Clear();

        // 5. Cancel appointment
        await appointmentService.CancelAsync(new CancelAppointmentDto
        {
            AppointmentId = created.Id,
            UserId = "test-pat-1",
            UserRole = "Patient",
            Reason = "Patient unavailable"
        });

        // Cancellation email must be sent
        Assert.True(fakeEmailSender.SentEmails.Count >= 1);
        Assert.Contains(fakeEmailSender.SentEmails, e => e.Subject.Contains("Cancelled") || e.Body.Contains("Cancelled"));
    }
}
