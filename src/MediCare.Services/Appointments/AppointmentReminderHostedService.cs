using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Appointments;

/// <summary>
/// Background service that sends 24-hour reminder emails for Confirmed appointments (FR-27).
/// Switchable via configuration: "Reminders:Enabled".
/// </summary>
public class AppointmentReminderHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentReminderHostedService> _logger;

    public AppointmentReminderHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AppointmentReminderHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = !bool.TryParse(_configuration["Reminders:Enabled"], out var confEnabled) || confEnabled;
        if (!enabled)
        {
            _logger.LogInformation("AppointmentReminderHostedService is disabled via configuration.");
            return;
        }

        _logger.LogInformation("AppointmentReminderHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing appointment reminder service.");
            }

            // Check once every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public async Task<int> ProcessRemindersAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var tomorrow = DateOnly.FromDateTime(clinicNow.AddDays(1));

        // Find confirmed appointments tomorrow
        var upcomingAppointments = await db.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.AppointmentDate == tomorrow)
            .ToListAsync(ct);

        int sentCount = 0;

        foreach (var appt in upcomingAppointments)
        {
            if (appt.Patient?.User == null || appt.Doctor?.User == null) continue;

            // Check if reminder was already sent for this appointment
            var reminderSubject = $"Tomorrow at {appt.StartTime:hh\\:mm}";
            var alreadyNotified = await db.Notifications
                .AnyAsync(n => n.UserId == appt.Patient.UserId
                            && n.Type == "AppointmentReminder"
                            && n.Subject.Contains(reminderSubject), ct);

            if (!alreadyNotified)
            {
                var sent = await notificationService.SendReminderAsync(
                    appt.Patient.UserId,
                    appt.Patient.User.Email ?? string.Empty,
                    appt.Patient.User.FullName,
                    appt.Doctor.User.FullName,
                    appt.AppointmentDate,
                    appt.StartTime,
                    appt.EndTime,
                    appt.Doctor.RoomNumber);

                if (sent) sentCount++;
            }
        }

        if (sentCount > 0)
        {
            _logger.LogInformation("Sent {Count} appointment reminders for {TomorrowDate}.", sentCount, tomorrow);
        }

        return sentCount;
    }
}
