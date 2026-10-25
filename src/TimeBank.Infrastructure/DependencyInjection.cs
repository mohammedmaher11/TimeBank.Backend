using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Security;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Infrastructure.Events;
using TimeBank.Infrastructure.Persistence;
using TimeBank.Infrastructure.Persistence.Seed;
using TimeBank.Infrastructure.Security;

namespace TimeBank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required. Set it in appsettings.Development.json, user secrets, or the ConnectionStrings__DefaultConnection environment variable.");

        services.AddDbContext<TimeBankDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(TimeBankDbContext).Assembly.FullName);
                // Retry-on-failure is intentionally disabled: the exchange workflow uses
                // explicit transactions plus sp_getapplock, which the retrying execution
                // strategy cannot wrap safely (and a blind retry could duplicate a transfer).
            }));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}
