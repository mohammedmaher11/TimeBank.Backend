using Microsoft.Extensions.DependencyInjection;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.Services;

namespace TimeBank.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExchangeService, ExchangeService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IMemberSkillService, MemberSkillService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IMemberQueryService, MemberQueryService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IModerationService, ModerationService>();
        return services;
    }
}
