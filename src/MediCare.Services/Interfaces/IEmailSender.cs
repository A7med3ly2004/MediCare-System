namespace MediCare.Services.Interfaces;

/// <summary>
/// Abstraction over email sending (SendGrid in production, console logger in development).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Send an email (to, subject, html body). Never throws: returns false on failure so
    /// a failed email attempt never crashes the user's request.
    /// </summary>
    Task<bool> SendAsync(string to, string subject, string htmlBody);
}
