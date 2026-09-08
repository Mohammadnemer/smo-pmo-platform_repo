using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;
using SmoPmo.Shared.Persistence;

namespace SmoPmo.Rollup;

/// <summary>
/// Roll-up module composition root. Owns: initiative &lt;-&gt; program/project links (M:N,
/// primary + weight) and their table (<see cref="RollupDbContext"/>).
///
/// Wired only from /Api (architecture-mvp.md §9) — each module exposes exactly one
/// registration entry point and /Api calls them in order. Filled in: X1 (links). Health
/// computation (X2) and traceability (X3) build on top of this.
/// </summary>
public static class RollupModule
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=smo_pmo_platform;Username=postgres;Password=postgres";

    public static IServiceCollection AddRollupModule(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("Rollup")
            ?? configuration?.GetConnectionString("Platform")
            ?? DefaultConnectionString;

        return services.AddRollupModule(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(RollupDbContext.MigrationsHistoryTable));

            // Hand-written migrations mean the model snapshot isn't regenerated the way
            // `dotnet ef migrations add` would; see SmoModule/PmoModule for the same
            // suppression.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
    }

    /// <summary>
    /// Provider-agnostic overload: the module wires its own interceptors and endpoints
    /// either way, so a test can swap the store without bypassing module behaviour.
    /// </summary>
    public static IServiceCollection AddRollupModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped(sp => new AuditingSaveChangesInterceptor(
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<IMessageBus>(),
            module: "Rollup"));

        services.AddDbContext<RollupDbContext>((sp, options) =>
        {
            configureDbContext(options);
            options.AddInterceptors(
                sp.GetRequiredService<TenantConnectionInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        // The roll-up worker's entry point into Roll-up (X2) — see RollupHealthContracts in
        // /Shared for why this command lives there rather than in RollupContracts.
        services.AddScoped<ICommandHandler<RecomputeDeliveryHealthCommand>, RecomputeDeliveryHealthCommandHandler>();

        return services;
    }
}
