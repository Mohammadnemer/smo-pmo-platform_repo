using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmoPmo.Api;
using SmoPmo.Api.Auth;
using SmoPmo.Api.Middleware;
using SmoPmo.Platform;
using SmoPmo.Smo;
using SmoPmo.Pmo;
using SmoPmo.Workflow;
using SmoPmo.Rollup;
using SmoPmo.Notifications;
using SmoPmo.Worker;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;

var builder = WebApplication.CreateBuilder(args);

// ────────────────────────── Composition root ──────────────────────────
// The ONLY place the modules meet. They never reference each other; they are
// registered side by side here and communicate at runtime through IMessageBus
// (defined in /Shared). See architecture-mvp.md §9.
builder.Services
    .AddPlatformModule(builder.Configuration)
    .AddSmoModule(builder.Configuration)
    .AddPmoModule(builder.Configuration)
    .AddWorkflowModule()
    .AddRollupModule(builder.Configuration)
    .AddNotificationsModule();

// The roll-up/notification worker runs in-process during MVP; split out at scale (§8).
builder.Services.AddBackgroundWorker(builder.Configuration);

// Per-request lifetimes, not singletons: TenantContext carries one request's tenant (a
// singleton would leak it across tenants), and the mediator resolves scoped handlers, so it
// must hold the request's scope rather than the root provider.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IMessageBus, InProcessMessageBus>();
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IAuthorizationHandler, AuthorizationHandler>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await dbContext.Database.MigrateAsync();

    // Each module migrates its own tables; SMO, PMO and Rollup each keep a separate
    // migration ledger (B5, B6, X1).
    await scope.ServiceProvider.GetRequiredService<SmoDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PmoDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<RollupDbContext>().Database.MigrateAsync();

    var tenantId = Guid.Parse(app.Configuration["DefaultTenantId"] ?? "00000000-0000-0000-0000-000000000001");
    var tenantName = app.Configuration["DefaultTenantName"] ?? "Default Tenant";
    await dbContext.SeedTenantAsync(tenantId, tenantName);

    // Dev-only demo scorecard tree (F4) so the SPA has real RAG-varied data to render
    // against, without a seeded fixture ever reaching a real tenant's database. SMO's
    // tables FORCE row-level security, so the tenant context has to be set before the
    // seeder's SaveChanges runs, same as the request pipeline does per-call.
    if (app.Environment.IsDevelopment())
    {
        var devTenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        devTenantContext.TenantId = tenantId;
        var smoDbContext = scope.ServiceProvider.GetRequiredService<SmoDbContext>();
        await SmoDemoSeed.SeedAsync(smoDbContext);

        // Dev-only demo portfolio/program/project tree (F6) so the PMO lists and project
        // workspace have real data to render, same reasoning as the SMO seed above.
        var pmoDbContext = scope.ServiceProvider.GetRequiredService<PmoDbContext>();
        await PmoDemoSeed.SeedAsync(pmoDbContext);

        // Dev-only demo initiative <-> delivery links (X1), tying the SMO and PMO seeds
        // above together by name so the aggregate read has something real to return. Lives
        // here rather than in a RollupDemoSeed helper because linking across modules by
        // name needs both DbContexts in scope at once, and only /Api is allowed to hold
        // both (architecture-mvp.md §9).
        var rollupDbContext = scope.ServiceProvider.GetRequiredService<RollupDbContext>();
        await RollupDemoSeed.SeedAsync(rollupDbContext, smoDbContext, pmoDbContext);
    }
}

// Liveness probe — used by the Container App (T2) and CI smoke test (T3).
var health = () => Results.Ok(new
{
    status = "ok",
    service = "smo-pmo-api",
    modules = new[] { "Platform", "SMO", "PMO", "Workflow", "Rollup", "Notifications" }
});
app.MapGet("/health", health);

// Same probe, reachable through the SPA's /api proxy (vite.config.ts forwards /api/*
// unchanged so it lines up with module routes like /api/smo/*; the root /health above
// stays for infra that curls the container directly).
app.MapGet("/api/health", health);

app.MapGet("/me", [Authorize]() => Results.Ok(new { authenticated = true })).RequireAuthorization();
app.MapGet("/admin", [Authorize(Policy = "AdminPolicy")] () => Results.Ok(new { authorized = true })).RequireAuthorization();

// Each module maps its own routes; /Api only calls them (B5 replaced the throwaway
// POST /strategies demo endpoint with the real SMO surface under /api/smo; B6 does the
// same for the throwaway GET /projects demo endpoint, now the real PMO surface).
app.MapSmoEndpoints();
app.MapPmoEndpoints();
app.MapRollupEndpoints();

app.Run();
