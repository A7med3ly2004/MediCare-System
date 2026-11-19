using MediCare.Services.Account;
using MediCare.Services.Email;
using MediCare.Services.Interfaces;
using MediCare.Services.Notifications;
using MediCare.Services.Otp;
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
