using Microsoft.Extensions.DependencyInjection;
using pm.Application.Interfaces;
using pm.Application.Services;

namespace pm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientService, ClientService>();
        return services;
    }
}
