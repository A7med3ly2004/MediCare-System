using MediCare.Services.Account;
using MediCare.Services.Admin;
using MediCare.Services.Appointments;
using MediCare.Services.Doctors;
using MediCare.Services.Email;
using MediCare.Services.Interfaces;
using MediCare.Services.MedicalRecords;
using MediCare.Services.Notifications;
using MediCare.Services.Otp;
using MediCare.Services.Reports;
using MediCare.Services.Schedule;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediCare.Services.Extensions;

public static class ServicesLayerExtensions
{
    public static IServiceCollection AddServicesLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IMedicalRecordService, MedicalRecordService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAdminManagementService, AdminManagementService>();
        services.AddHostedService<AppointmentReminderHostedService>();

        var sendGridKey = configuration["SendGrid:ApiKey"];
        if (!string.IsNullOrWhiteSpace(sendGridKey))
        {
            services.AddScoped<IEmailSender, SendGridEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, DevConsoleEmailSender>();
        }

        return services;
    }
}
