using MediCare.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MediCare.Services.Email;

/// <summary>
/// Production email sender using the SendGrid API.
/// Settings read from SendGrid:ApiKey, SendGrid:FromEmail, SendGrid:FromName.
/// </summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IConfiguration configuration, ILogger<SendGridEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"];
            var fromName = _configuration["SendGrid:FromName"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SendGrid:ApiKey is not configured. Email to {To} was not sent.", to);
                return false;
            }

            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = "noreply@medicare.eg";
            if (string.IsNullOrWhiteSpace(fromName))
                fromName = "MediCare Clinic";

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var toEmail = new EmailAddress(to);
            var msg = MailHelper.CreateSingleEmail(from, toEmail, subject, plainTextContent: null, htmlContent: htmlBody);

            var response = await client.SendEmailAsync(msg);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent via SendGrid to {To} with subject '{Subject}'", to, subject);
                return true;
            }

            var body = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid failed to send email to {To}. Status: {StatusCode}. Response: {Response}",
                to, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while sending email to {To} via SendGrid", to);
            return false;
        }
    }
}
