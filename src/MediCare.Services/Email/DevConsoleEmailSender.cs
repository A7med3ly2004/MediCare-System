using MediCare.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Email;

/// <summary>
/// Development email sender that logs the email, including OTP, to the console.
/// Used only when SendGrid:ApiKey is empty.
/// </summary>
public class DevConsoleEmailSender : IEmailSender
{
    private readonly ILogger<DevConsoleEmailSender> _logger;

    public DevConsoleEmailSender(ILogger<DevConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string to, string subject, string htmlBody)
    {
        _logger.LogInformation(
            "\n==================== [DEV CONSOLE EMAIL SENDER] ====================\n" +
            "TO:      {To}\n" +
            "SUBJECT: {Subject}\n" +
            "BODY:\n{Body}\n" +
            "====================================================================",
            to, subject, htmlBody);

        return Task.FromResult(true);
    }
}
