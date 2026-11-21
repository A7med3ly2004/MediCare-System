using MediCare.Data.Entities;

namespace MediCare.Services.Interfaces;

/// <summary>
/// Notification email builder and dispatcher using the Factory pattern.
/// Logs every attempt to the Notifications table (FR-28, BR-10).
/// </summary>
public interface INotificationService
{
    /// <summary>Builds and sends the 6-digit OTP verification email.</summary>
    Task<bool> SendOtpEmailAsync(string userId, string toEmail, string toName, string plainOtp);

    Task<bool> SendBookingReceivedPatientAsync(
        string userId, string toEmail, string toName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room, decimal fee);

    Task<bool> SendBookingReceivedDoctorAsync(
        string userId, string toEmail, string doctorName, string patientName, string patientCode, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? notes);

    Task<bool> SendAppointmentConfirmedAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room);

    Task<bool> SendAppointmentRejectedAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? reason);

    Task<bool> SendAppointmentCancelledAsync(
        string userId, string toEmail, string recipientName, string otherPartyName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string cancelledBy);

    Task<bool> SendAppointmentRescheduledAsync(
        string userId, string toEmail, string recipientName, string otherPartyName, DateOnly newDate, TimeSpan newStartTime, TimeSpan newEndTime);

    Task<bool> SendReminderAsync(
        string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room);

    /// <summary>Retrieves sent/pending notifications for a specific user.</summary>
    Task<IEnumerable<Notification>> GetNotificationsByUserAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<Notification>());
}
