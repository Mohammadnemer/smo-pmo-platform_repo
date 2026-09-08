using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Persistence;

namespace SmoPmo.Platform;

/// <summary>
/// Platform module composition root. Owns: tenancy, org structure, RBAC, users/roles, lookups, i18n, themes, audit.
///
/// Wired only from /Api (architecture-mvp.md §9) — each module exposes exactly one
/// registration entry point and /Api calls them in order. Filled in for D1 with the
/// initial EF Core DbContext and schema services.
/// </summary>
public static class PlatformModule
{
    /// <summary>Fallback for a stock local install; real environments set ConnectionStrings:Platform.</summary>
    public const string DefaultConnectionString =
        "Host=localhost;Database=smo_pmo_platform;Username=postgres;Password=postgres";

    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var connectionString = configuration?.GetConnectionString("Platform") ?? DefaultConnectionString;

        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);

            // Sets app.tenant_id on the connection, which is what the D2 RLS policies read.
            // Without it the policies compare against the all-zero guid (B1's "tenant_id
            // flows all the way to the Postgres connection").
            options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());

            // These migrations are hand-written rather than scaffolded, so the model
            // snapshot doesn't get regenerated the way `dotnet ef migrations add` would;
            // EF's snapshot-vs-model diff otherwise turns this into a startup exception.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<SaveChangesInterceptor>(sp => sp.GetRequiredService<AuditSaveChangesInterceptor>());

        // Platform is the audit log's only writer; other modules report changes to it
        // through the mediator (B5).
        services.AddScoped<IDomainEventHandler<EntitiesChangedEvent>, EntitiesChangedEventHandler>();

        return services;
    }
}
