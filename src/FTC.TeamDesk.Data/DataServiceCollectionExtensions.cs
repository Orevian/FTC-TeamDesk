using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FTC.TeamDesk.Data;

public static class DataServiceCollectionExtensions
{
    public static string BuildConnectionString(string path) => $"Data Source={path};Foreign Keys=True;Pooling=True";

    public static IServiceCollection AddTeamDeskData(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<TeamDeskDbContext>(o => o.UseSqlite(BuildConnectionString(databasePath)));

        services.AddSingleton(sp => new DatabaseInitializer(databasePath, sp.GetRequiredService<IDbContextFactory<TeamDeskDbContext>>()));
        services.AddSingleton<IMemberRepository, MemberRepository>();
        services.AddSingleton<IApplicationRepository, ApplicationRepository>();
        services.AddSingleton<IBudgetRepository, BudgetRepository>();
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IActivityLogRepository, ActivityLogRepository>();
        services.AddSingleton<IAiConfigRepository, AiConfigRepository>();
        services.AddSingleton<IVaultRepository, VaultRepository>();
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        services.AddSingleton<IDatabaseMaintenance>(sp =>
            new DatabaseMaintenance(databasePath, sp.GetRequiredService<IDbContextFactory<TeamDeskDbContext>>()));
        return services;
    }
}
