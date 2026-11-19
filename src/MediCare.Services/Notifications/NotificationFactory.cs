using System.Net;

namespace MediCare.Services.Notifications;

public class NotificationEmail
{
    public string Subject { get; init; } = string.Empty;
    public string HtmlBody { get; init; } = string.Empty;
}

/// <summary>
/// Factory that creates notification emails with HTML templates based on design tokens.
/// </summary>
public static class NotificationFactory
{
    public static NotificationEmail CreateOtpEmail(string recipientName, string plainOtp)
    {
        var subject = "MediCare — Verify your email address";
        var safeName = WebUtility.HtmlEncode(recipientName);
        var body = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>{subject}</title>
            </head>
            <body style="font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F7F9FA; margin: 0; padding: 24px; color: #253747;">
                <table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 520px; background-color: #FFFFFF; border-radius: 8px; border: 1px solid #E2EBEC; overflow: hidden; padding: 32px;">
                    <tr>
                        <td align="center" style="padding-bottom: 24px;">
                            <span style="font-size: 24px; font-weight: bold; color: #253747;">
                                <span style="display: inline-block; width: 28px; height: 28px; line-height: 28px; background-color: #90D0F2; color: #253747; border-radius: 6px; text-align: center; font-size: 18px; margin-right: 8px;">+</span>
                                MediCare
                            </span>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding-bottom: 16px;">
                            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Verify your email address</h2>
                            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0;">
                                Hello {safeName},<br/>
                                Thank you for registering with MediCare. Please use the verification code below to confirm your email and activate your account.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td align="center" style="padding: 24px 0;">
                            <div style="background-color: #EAF4FA; border: 1px dashed #90D0F2; border-radius: 8px; padding: 18px 24px; display: inline-block;">
                                <span style="font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #253747;">{plainOtp}</span>
                            </div>
                            <p style="font-size: 12px; color: #607280; margin: 12px 0 0 0;">This code will expire in <strong>10 minutes</strong>.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style="border-top: 1px solid #E2EBEC; padding-top: 20px;">
                            <p style="font-size: 12px; color: #607280; line-height: 1.4; margin: 0;">
                                If you did not create an account with MediCare, please disregard this email.
                            </p>
                            <p style="font-size: 11px; color: #B4919B; margin: 12px 0 0 0;">
                                MediCare Clinic · Al Nour Clinic · All times Cairo local time (GMT+2)
                            </p>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = body };
    }

    public static NotificationEmail CreateBookingReceivedEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("BookingReceived notification email is a stub for TASK 5.");
    }

    public static NotificationEmail CreateAppointmentConfirmedEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("Confirmed notification email is a stub for TASK 5.");
    }

    public static NotificationEmail CreateAppointmentRejectedEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("Rejected notification email is a stub for TASK 5.");
    }

    public static NotificationEmail CreateAppointmentCancelledEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("Cancelled notification email is a stub for TASK 5.");
    }

    public static NotificationEmail CreateAppointmentRescheduledEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("Rescheduled notification email is a stub for TASK 5.");
    }

    public static NotificationEmail CreateReminderEmail(string recipientName, int appointmentId)
    {
        // TASK 5: Full implementation
        throw new NotImplementedException("Reminder notification email is a stub for TASK 5.");
    }
}
