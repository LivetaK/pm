using Microsoft.Extensions.DependencyInjection;
using pm.Application.Interfaces;
using pm.Infrastructure.Repositories;
using pm.Infrastructure.Services;

namespace pm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<DapperContext>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<DemoDataSeeder>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        return services;
    }
}
