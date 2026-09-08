using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;
using SmoPmo.Shared.Persistence;

namespace SmoPmo.Smo;

/// <summary>
/// SMO module composition root. Owns: strategy, perspectives, objectives, KPIs/measures,
/// initiatives, scorecard — including their tables (<see cref="SmoDbContext"/>).
///
/// Wired only from /Api (architecture-mvp.md §9). Filled in: D3 (schema), B5 (SMO API).
/// </summary>
public static class SmoModule
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=smo_pmo_platform;Username=postgres;Password=postgres";

    public static IServiceCollection AddSmoModule(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("Smo")
            ?? configuration?.GetConnectionString("Platform")
            ?? DefaultConnectionString;

        return services.AddSmoModule(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(SmoDbContext.MigrationsHistoryTable));

            // Hand-written migrations mean the model snapshot isn't regenerated the way
            // `dotnet ef migrations add` would; see PlatformModule for the same suppression.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
    }

    /// <summary>
    /// Provider-agnostic overload: the module wires its own interceptors and endpoints
    /// either way, so a test can swap the store without bypassing module behaviour.
    /// </summary>
    public static IServiceCollection AddSmoModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped(sp => new AuditingSaveChangesInterceptor(
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<IMessageBus>(),
            module: "SMO"));

        services.AddDbContext<SmoDbContext>((sp, options) =>
        {
            configureDbContext(options);
            options.AddInterceptors(
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<ICommandHandler<CreateStrategyCommand, Guid>, CreateStrategyCommandHandler>();

        // Roll-up's side of the cross-module read seam (X1) — see CrossModuleQueries in
        // /Shared for why the contract lives there while the handler stays here.
        services.AddScoped<ICommandHandler<GetInitiativeSummaryQuery, InitiativeSummary?>, InitiativeSummaryQueryHandler>();

        // The roll-up worker's seam into SMO (X2) — see RollupHealthContracts in /Shared.
        services.AddScoped<ICommandHandler<RecomputeInitiativeHealthCommand, bool>, RecomputeInitiativeHealthCommandHandler>();

        return services;
    }
}
