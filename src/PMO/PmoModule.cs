using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;
using SmoPmo.Shared.Persistence;

namespace SmoPmo.Pmo;

/// <summary>
/// PMO module composition root. Owns: portfolios, programs, projects, schedule/tasks, RAID,
/// status reports — including their tables (<see cref="PmoDbContext"/>) and the CPM engine.
///
/// Wired only from /Api (architecture-mvp.md §9). Filled in: D3 (schema, shared pending B6),
/// B6 (PMO API & scheduler — the module now owns its own tables, mirroring B5's SMO move).
/// </summary>
public static class PmoModule
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=smo_pmo_platform;Username=postgres;Password=postgres";

    public static IServiceCollection AddPmoModule(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("Pmo")
            ?? configuration?.GetConnectionString("Platform")
            ?? DefaultConnectionString;

        return services.AddPmoModule(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(PmoDbContext.MigrationsHistoryTable));

            // Hand-written migrations mean the model snapshot isn't regenerated the way
            // `dotnet ef migrations add` would; see PlatformModule/SmoModule for the same
            // suppression.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
    }

    /// <summary>
    /// Provider-agnostic overload: the module wires its own interceptors and endpoints
    /// either way, so a test can swap the store without bypassing module behaviour.
    /// </summary>
    public static IServiceCollection AddPmoModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped(sp => new AuditingSaveChangesInterceptor(
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<IMessageBus>(),
            module: "PMO"));

        services.AddDbContext<PmoDbContext>((sp, options) =>
        {
            configureDbContext(options);
            options.AddInterceptors(
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        // Roll-up's side of the cross-module read seam (X1) — see CrossModuleQueries in
        // /Shared for why the contract lives there while the handlers stay here.
        services.AddScoped<ICommandHandler<GetProgramSummaryQuery, ProgramSummary?>, ProgramSummaryQueryHandler>();
        services.AddScoped<ICommandHandler<GetProjectSummaryQuery, ProjectSummary?>, ProjectSummaryQueryHandler>();

        // The roll-up worker's seam into PMO (X2) — see RollupHealthContracts in /Shared.
        services.AddScoped<ICommandHandler<RecomputeProjectHealthCommand, ProjectHealthRecomputeResult?>, RecomputeProjectHealthCommandHandler>();
        services.AddScoped<ICommandHandler<GetProjectHealthQuery, HealthResult?>, GetProjectHealthQueryHandler>();
        services.AddScoped<ICommandHandler<GetProgramHealthQuery, HealthResult?>, GetProgramHealthQueryHandler>();

        return services;
    }
}
