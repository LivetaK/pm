using Microsoft.Extensions.DependencyInjection;
using pm.Application.Interfaces;
using pm.Infrastructure.Repositories;
using pm.Infrastructure.Services;

namespace pm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.AddSingleton<DapperContext>();
services.AddSingleton<DemoDataSeeder>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        return services;
    }
}
