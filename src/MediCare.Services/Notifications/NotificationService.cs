using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        IEmailSender emailSender,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(string userId, string toEmail, string toName, string plainOtp)
    {
        var email = NotificationFactory.CreateOtpEmail(toName, plainOtp);
        return await SendAndLogNotificationAsync(userId, toEmail, "Otp", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendBookingReceivedPatientAsync(
        string userId, string toEmail, string toName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room, decimal fee)
    {
        var email = NotificationFactory.CreateBookingReceivedPatientEmail(toName, doctorName, date, startTime, endTime, room, fee);
        return await SendAndLogNotificationAsync(userId, toEmail, "BookingReceivedPatient", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendBookingReceivedDoctorAsync(
        string userId, string toEmail, string doctorName, string patientName, string patientCode, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? notes)
    {
        var email = NotificationFactory.CreateBookingReceivedDoctorEmail(doctorName, patientName, patientCode, date, startTime, endTime, notes);
        return await SendAndLogNotificationAsync(userId, toEmail, "BookingReceivedDoctor", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendAppointmentConfirmedAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
    {
        var email = NotificationFactory.CreateAppointmentConfirmedEmail(recipientName, doctorName, date, startTime, endTime, room);
        return await SendAndLogNotificationAsync(userId, toEmail, "AppointmentConfirmed", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendAppointmentRejectedAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? reason)
    {
        var email = NotificationFactory.CreateAppointmentRejectedEmail(recipientName, doctorName, date, startTime, endTime, reason);
        return await SendAndLogNotificationAsync(userId, toEmail, "AppointmentRejected", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendAppointmentCancelledAsync(
        string userId, string toEmail, string recipientName, string otherPartyName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string cancelledBy)
    {
        var email = NotificationFactory.CreateAppointmentCancelledEmail(recipientName, otherPartyName, date, startTime, endTime, cancelledBy);
        return await SendAndLogNotificationAsync(userId, toEmail, "AppointmentCancelled", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendAppointmentRescheduledAsync(
        string userId, string toEmail, string recipientName, string otherPartyName, DateOnly newDate, TimeSpan newStartTime, TimeSpan newEndTime)
    {
        var email = NotificationFactory.CreateAppointmentRescheduledEmail(recipientName, otherPartyName, newDate, newStartTime, newEndTime);
        return await SendAndLogNotificationAsync(userId, toEmail, "AppointmentRescheduled", email.Subject, email.HtmlBody);
    }

    public async Task<bool> SendReminderAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
    {
        var email = NotificationFactory.CreateReminderEmail(recipientName, doctorName, date, startTime, endTime, room);
        return await SendAndLogNotificationAsync(userId, toEmail, "AppointmentReminder", email.Subject, email.HtmlBody);
    }

    private async Task<bool> SendAndLogNotificationAsync(
        string userId,
        string toEmail,
        string type,
        string subject,
        string htmlBody)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Subject = subject,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        bool sent = false;
        try
        {
            sent = await _emailSender.SendAsync(toEmail, subject, htmlBody);
            if (sent)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTime.UtcNow;
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Type} email to {ToEmail}", type, toEmail);
            notification.Status = NotificationStatus.Failed;
            sent = false;
        }

        try
        {
            await _uow.Notifications.AddAsync(notification);
            await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification record for user {UserId}", userId);
            // Must never throw to caller
        }

        return sent;
    }

    public async Task<IEnumerable<Notification>> GetNotificationsByUserAsync(string userId, CancellationToken ct = default)
    {
        return await _uow.Notifications.GetByUserAsync(userId, ct);
    }
}
