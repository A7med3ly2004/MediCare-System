namespace MediCare.Services.Interfaces;

/// <summary>
/// Notification email builder and dispatcher using the Factory pattern.
/// Logs every attempt to the Notifications table (FR-28, BR-10).
/// </summary>
public interface INotificationService
{
    /// <summary>Builds and sends the 6-digit OTP verification email.</summary>
    Task<bool> SendOtpEmailAsync(string userId, string toEmail, string toName, string plainOtp);

    // ── STUBS — to be implemented in TASK 5 ──────────────────────────────────

    /// <summary>STUB: booking received confirmation to patient (TASK 5).</summary>
    Task<bool> SendBookingReceivedAsync(string userId, string toEmail, string toName, int appointmentId);

    /// <summary>STUB: appointment confirmed by doctor/admin (TASK 5).</summary>
    Task<bool> SendAppointmentConfirmedAsync(string userId, string toEmail, string toName, int appointmentId);

    /// <summary>STUB: appointment rejected by doctor/admin (TASK 5).</summary>
    Task<bool> SendAppointmentRejectedAsync(string userId, string toEmail, string toName, int appointmentId);

    /// <summary>STUB: appointment cancelled (TASK 5).</summary>
    Task<bool> SendAppointmentCancelledAsync(string userId, string toEmail, string toName, int appointmentId);

    /// <summary>STUB: appointment rescheduled (TASK 5).</summary>
    Task<bool> SendAppointmentRescheduledAsync(string userId, string toEmail, string toName, int appointmentId);

    /// <summary>STUB: 24-hour reminder (TASK 5).</summary>
    Task<bool> SendReminderAsync(string userId, string toEmail, string toName, int appointmentId);
}
