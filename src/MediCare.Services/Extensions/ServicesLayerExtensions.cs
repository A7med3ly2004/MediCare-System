using MediCare.Services.Account;
using MediCare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MediCare.Services.Extensions;

public static class ServicesLayerExtensions
{
    public static IServiceCollection AddServicesLayer(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        return services;
    }
}
