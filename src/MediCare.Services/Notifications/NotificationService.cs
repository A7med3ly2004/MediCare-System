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

    // ── STUBS for TASK 5 ─────────────────────────────────────────────────────

    public Task<bool> SendBookingReceivedAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendBookingReceivedAsync is a stub for TASK 5.");
    }

    public Task<bool> SendAppointmentConfirmedAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendAppointmentConfirmedAsync is a stub for TASK 5.");
    }

    public Task<bool> SendAppointmentRejectedAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendAppointmentRejectedAsync is a stub for TASK 5.");
    }

    public Task<bool> SendAppointmentCancelledAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendAppointmentCancelledAsync is a stub for TASK 5.");
    }

    public Task<bool> SendAppointmentRescheduledAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendAppointmentRescheduledAsync is a stub for TASK 5.");
    }

    public Task<bool> SendReminderAsync(string userId, string toEmail, string toName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("SendReminderAsync is a stub for TASK 5.");
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
}
