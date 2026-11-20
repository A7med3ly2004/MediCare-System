using System.Net;

namespace MediCare.Services.Notifications;

public class NotificationEmail
{
    public string Subject { get; init; } = string.Empty;
    public string HtmlBody { get; init; } = string.Empty;
}

/// <summary>
/// Factory that creates notification emails with HTML templates based on MediCare design tokens.
/// </summary>
public static class NotificationFactory
{
    private static string WrapTemplate(string title, string contentHtml)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>{title}</title>
            </head>
            <body style="font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F7F9FA; margin: 0; padding: 24px; color: #253747;">
                <table align="center" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 540px; background-color: #FFFFFF; border-radius: 8px; border: 1px solid #E2EBEC; overflow: hidden; padding: 32px;">
                    <tr>
                        <td align="center" style="padding-bottom: 24px;">
                            <span style="font-size: 24px; font-weight: bold; color: #253747;">
                                <span style="display: inline-block; width: 28px; height: 28px; line-height: 28px; background-color: #90D0F2; color: #253747; border-radius: 6px; text-align: center; font-size: 18px; margin-right: 8px;">+</span>
                                MediCare
                            </span>
                        </td>
                    </tr>
                    <tr>
                        <td>
                            {contentHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style="border-top: 1px solid #E2EBEC; padding-top: 20px; margin-top: 24px;">
                            <p style="font-size: 11px; color: #607280; line-height: 1.4; margin: 0;">
                                MediCare Clinic · Al Nour Clinic, New Cairo · All times Cairo local time (GMT+2)
                            </p>
                            <p style="font-size: 11px; color: #B4919B; margin: 8px 0 0 0;">
                                For inquiries, contact reception at +20 2 2561 0340.
                            </p>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    public static NotificationEmail CreateOtpEmail(string recipientName, string plainOtp)
    {
        var subject = "MediCare — Verify your email address";
        var safeName = WebUtility.HtmlEncode(recipientName);
        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Verify your email address</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeName},<br/>
                Thank you for registering with MediCare. Please use the verification code below to confirm your email and activate your account.
            </p>
            <div style="text-align: center; margin: 24px 0;">
                <div style="background-color: #EAF4FA; border: 1px dashed #90D0F2; border-radius: 8px; padding: 18px 24px; display: inline-block;">
                    <span style="font-family: 'Courier New', Courier, monospace; font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #253747;">{plainOtp}</span>
                </div>
                <p style="font-size: 12px; color: #607280; margin: 12px 0 0 0;">This code will expire in <strong>10 minutes</strong>.</p>
            </div>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateBookingReceivedPatientEmail(
        string patientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room, decimal fee)
    {
        var subject = "MediCare — Appointment request received";
        var safePatient = WebUtility.HtmlEncode(patientName);
        var safeDoctor = WebUtility.HtmlEncode(doctorName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Appointment Request Received</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safePatient},<br/>
                Your appointment request has been received and is currently <strong style="color: #E6A817;">Pending</strong> doctor confirmation.
            </p>
            <table width="100%" style="background-color: #F7F9FA; border: 1px solid #E2EBEC; border-radius: 8px; padding: 16px; margin-bottom: 20px; font-size: 13px;">
                <tr><td style="color: #607280; padding: 4px 0;">Doctor:</td><td style="font-weight: 600; color: #253747; text-align: right;">{safeDoctor}</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Date:</td><td style="font-weight: 600; color: #253747; text-align: right;">{dateStr}</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Time:</td><td style="font-weight: 600; color: #253747; text-align: right;">{timeStr}</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Clinic Room:</td><td style="font-weight: 600; color: #253747; text-align: right;">{(string.IsNullOrEmpty(room) ? "Main clinic" : room)}</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Consultation Fee:</td><td style="font-weight: 600; color: #253747; text-align: right;">{fee:N0} EGP</td></tr>
            </table>
            <p style="font-size: 13px; color: #607280; margin: 0 0 20px 0;">
                You will receive another notification once Dr. {safeDoctor} reviews and confirms your appointment.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateBookingReceivedDoctorEmail(
        string doctorName, string patientName, string patientCode, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? notes)
    {
        var subject = "MediCare — New appointment request awaiting confirmation";
        var safeDoctor = WebUtility.HtmlEncode(doctorName);
        var safePatient = WebUtility.HtmlEncode(patientName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">New Appointment Request</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello Dr. {safeDoctor},<br/>
                A patient has submitted a new appointment booking for your review.
            </p>
            <table width="100%" style="background-color: #F7F9FA; border: 1px solid #E2EBEC; border-radius: 8px; padding: 16px; margin-bottom: 20px; font-size: 13px;">
                <tr><td style="color: #607280; padding: 4px 0;">Patient:</td><td style="font-weight: 600; color: #253747; text-align: right;">{safePatient} ({patientCode})</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Date:</td><td style="font-weight: 600; color: #253747; text-align: right;">{dateStr}</td></tr>
                <tr><td style="color: #607280; padding: 4px 0;">Time:</td><td style="font-weight: 600; color: #253747; text-align: right;">{timeStr}</td></tr>
                {(!string.IsNullOrEmpty(notes) ? $"<tr><td style=\"color: #607280; padding: 4px 0;\">Notes:</td><td style=\"font-weight: 600; color: #253747; text-align: right;\">{WebUtility.HtmlEncode(notes)}</td></tr>" : "")}
            </table>
            <p style="font-size: 13px; color: #607280; margin: 0 0 20px 0;">
                Please log into your Doctor Portal to confirm or reject this booking request.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateAppointmentConfirmedEmail(
        string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
    {
        var subject = "MediCare — Appointment Confirmed";
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeDoctor = WebUtility.HtmlEncode(doctorName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Your Appointment is Confirmed!</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeRecipient},<br/>
                We are pleased to inform you that your appointment with <strong>Dr. {safeDoctor}</strong> has been confirmed.
            </p>
            <table width="100%" style="background-color: #E8F5E9; border: 1px solid #C8E6C9; border-radius: 8px; padding: 16px; margin-bottom: 20px; font-size: 13px;">
                <tr><td style="color: #253747; padding: 4px 0;">Doctor:</td><td style="font-weight: 600; color: #253747; text-align: right;">Dr. {safeDoctor}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Date:</td><td style="font-weight: 600; color: #253747; text-align: right;">{dateStr}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Time:</td><td style="font-weight: 600; color: #253747; text-align: right;">{timeStr}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Clinic Location:</td><td style="font-weight: 600; color: #253747; text-align: right;">Al Nour Clinic · Room {(string.IsNullOrEmpty(room) ? "02" : room)}</td></tr>
            </table>
            <p style="font-size: 12px; color: #607280; margin: 0;">
                Please arrive 15 minutes before your consultation. If you need to reschedule or cancel, you may do so up to 2 hours before the start time.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateAppointmentRejectedEmail(
        string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? reason)
    {
        var subject = "MediCare — Appointment Request Declined";
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeDoctor = WebUtility.HtmlEncode(doctorName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #C62828; margin: 0 0 8px 0;">Appointment Request Declined</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeRecipient},<br/>
                We regret to inform you that your appointment request with <strong>Dr. {safeDoctor}</strong> on <strong>{dateStr}</strong> at <strong>{timeStr}</strong> could not be accommodated.
            </p>
            {(!string.IsNullOrEmpty(reason) ? $"<p style=\"font-size: 13px; color: #253747; background-color: #FFF3E0; padding: 12px; border-radius: 6px; border: 1px solid #FFE082;\"><strong>Reason:</strong> {WebUtility.HtmlEncode(reason)}</p>" : "")}
            <p style="font-size: 13px; color: #607280; margin: 0;">
                You are welcome to log in and select an alternative time slot or another doctor from our directory.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateAppointmentCancelledEmail(
        string recipientName, string otherPartyName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string cancelledBy)
    {
        var subject = "MediCare — Appointment Cancelled";
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeOther = WebUtility.HtmlEncode(otherPartyName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #C62828; margin: 0 0 8px 0;">Appointment Cancelled</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeRecipient},<br/>
                The appointment with <strong>{safeOther}</strong> scheduled for <strong>{dateStr}</strong> at <strong>{timeStr}</strong> has been cancelled by {cancelledBy}.
            </p>
            <p style="font-size: 13px; color: #607280; margin: 0;">
                The time slot has been released back into the clinic schedule.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateAppointmentRescheduledEmail(
        string recipientName, string otherPartyName, DateOnly newDate, TimeSpan newStartTime, TimeSpan newEndTime)
    {
        var subject = "MediCare — Appointment Rescheduled";
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeOther = WebUtility.HtmlEncode(otherPartyName);
        var timeStr = $"{newStartTime:hh\\:mm} – {newEndTime:hh\\:mm}";
        var dateStr = newDate.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Appointment Rescheduled</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeRecipient},<br/>
                Your appointment with <strong>{safeOther}</strong> has been rescheduled to <strong>{dateStr}</strong> at <strong>{timeStr}</strong>.
            </p>
            <p style="font-size: 13px; color: #607280; margin: 0;">
                The appointment has returned to <strong style="color: #E6A817;">Pending</strong> status awaiting confirmation of the new time.
            </p>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }

    public static NotificationEmail CreateReminderEmail(
        string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room)
    {
        var subject = "MediCare — Appointment Reminder: Tomorrow at " + $"{startTime:hh\\:mm}";
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeDoctor = WebUtility.HtmlEncode(doctorName);
        var timeStr = $"{startTime:hh\\:mm} – {endTime:hh\\:mm}";
        var dateStr = date.ToString("dddd, dd MMMM yyyy");

        var content = $"""
            <h2 style="font-size: 18px; font-weight: 600; color: #253747; margin: 0 0 8px 0;">Appointment Reminder</h2>
            <p style="font-size: 14px; color: #607280; line-height: 1.5; margin: 0 0 20px 0;">
                Hello {safeRecipient},<br/>
                This is a friendly reminder that your confirmed consultation with <strong>Dr. {safeDoctor}</strong> is scheduled for tomorrow.
            </p>
            <table width="100%" style="background-color: #EAF4FA; border: 1px solid #90D0F2; border-radius: 8px; padding: 16px; margin-bottom: 20px; font-size: 13px;">
                <tr><td style="color: #253747; padding: 4px 0;">Doctor:</td><td style="font-weight: 600; color: #253747; text-align: right;">Dr. {safeDoctor}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Date:</td><td style="font-weight: 600; color: #253747; text-align: right;">{dateStr}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Time:</td><td style="font-weight: 600; color: #253747; text-align: right;">{timeStr}</td></tr>
                <tr><td style="color: #253747; padding: 4px 0;">Room:</td><td style="font-weight: 600; color: #253747; text-align: right;">{(string.IsNullOrEmpty(room) ? "02" : room)}</td></tr>
            </table>
            """;

        return new NotificationEmail { Subject = subject, HtmlBody = WrapTemplate(subject, content) };
    }
}
