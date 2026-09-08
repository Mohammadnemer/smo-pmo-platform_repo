using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmoPmo.Api.Auth;
using SmoPmo.Api.Middleware;
using SmoPmo.Platform;
using SmoPmo.Pmo;
using SmoPmo.Rollup;
using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;
using SmoPmo.Smo;
using SmoPmo.Worker;
using Xunit;
// The test SDK generates its own empty `Program` entry-point class in the global namespace,
// which otherwise shadows SmoPmo.Pmo.Program (the PMO domain entity) wherever it's used
// unqualified — alias it instead of fully qualifying every reference.
using PmoProgram = SmoPmo.Pmo.Program;

namespace Rollup.Tests;

/// <summary>
/// Spins up the real Roll-up endpoints, tenant middleware, RBAC matrix and audit seam over
/// in-memory stores — plus SMO and PMO, wired in but with no HTTP surface mapped, because
/// Roll-up's cross-module queries (see CrossModuleQueries in /Shared) only resolve when
/// their handlers are registered in the same service provider the mediator dispatches
/// through. Test data for the "other side" of a link is seeded directly through the SMO/PMO
/// DbContexts rather than their endpoints, since only the module under test needs to be
/// exercised over HTTP.
/// </summary>
internal sealed class RollupTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RollupTestHost(WebApplication app) => _app = app;

    public static async Task<RollupTestHost> StartAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<IMessageBus, InProcessMessageBus>();

        // The module under test, plus the two modules it queries cross-module — all three
        // registered through their own entry points, same as production composition.
        builder.Services.AddSmoModule(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddPmoModule(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddRollupModule(options => options.UseInMemoryDatabase(databaseName));

        // X2: the roll-up worker's debounce queue/processor. Left on the default (real)
        // debounce/poll interval — tests never wait on the timer, they drive an immediate
        // flush through IRollupHealthProcessor.ProcessDueAsync(TimeSpan.Zero) instead, so the
        // "Done when" line doesn't depend on wall-clock time.
        builder.Services.AddBackgroundWorker();

        // Platform stands in only as the audit log's owner, so the cross-module audit path
        // is exercised end to end instead of being stubbed out.
        builder.Services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IDomainEventHandler<EntitiesChangedEvent>, EntitiesChangedEventHandler>();

        builder.Services.AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(RollupPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Rollup", PermissionAction.Read)));
            options.AddPolicy(RollupPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Rollup", PermissionAction.Write)));

            // X2's "Done when" line drives real PMO/SMO endpoints (a task PUT, an objective
            // GET) rather than seeding health values directly, so both modules' real policies
            // need to be registered too — copied verbatim from AuthenticationExtensions.cs.
            options.AddPolicy(SmoPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Strategy", PermissionAction.Read)));
            options.AddPolicy(SmoPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Strategy", PermissionAction.Write)));
            options.AddPolicy(PmoPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Read)));
            options.AddPolicy(PmoPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Write)));
        });
        builder.Services.AddScoped<IAuthorizationHandler, AuthorizationHandler>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.MapRollupEndpoints();
        app.MapSmoEndpoints();
        app.MapPmoEndpoints();

        await app.StartAsync();
        return new RollupTestHost(app);
    }

    /// <summary>
    /// Forces an immediate drain of whatever the roll-up worker's debounce queue currently
    /// holds, bypassing the real timer entirely — see <see cref="RollupWorkerOptions"/>. This
    /// is what makes "moving a task's % complete changes its objective's stored health"
    /// (X2's "Done when" line) assertable right after the PUT instead of racing a background
    /// poll loop.
    /// </summary>
    public async Task FlushRollupAsync()
    {
        using var scope = CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IRollupHealthProcessor>();
        await processor.ProcessDueAsync(TimeSpan.Zero);
    }

    /// <summary>A caller in the given tenant holding the given roles.</summary>
    public HttpClient CreateClient(Guid tenantId, params string[] roles)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.TenantHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>A caller with no token at all.</summary>
    public HttpClient CreateAnonymousClient() => _app.GetTestClient();

    /// <summary>Seeds a bare SMO initiative directly through its DbContext, tagged for the
    /// given tenant, so Roll-up's cross-module query has something real to resolve.</summary>
    public async Task<Initiative> SeedInitiativeAsync(Guid tenantId, string name)
    {
        using var scope = CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var db = scope.ServiceProvider.GetRequiredService<SmoDbContext>();
        var initiative = new Initiative { Id = Guid.NewGuid(), ObjectiveId = Guid.NewGuid(), Name = name };
        db.Initiatives.Add(initiative);
        await db.SaveChangesAsync();
        return initiative;
    }

    /// <summary>Seeds a bare PMO program directly through its DbContext.</summary>
    public async Task<PmoProgram> SeedProgramAsync(Guid tenantId, string name)
    {
        using var scope = CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var db = scope.ServiceProvider.GetRequiredService<PmoDbContext>();
        var program = new PmoProgram { Id = Guid.NewGuid(), PortfolioId = Guid.NewGuid(), Name = name };
        db.Programs.Add(program);
        await db.SaveChangesAsync();
        return program;
    }

    /// <summary>Seeds a bare PMO project directly through its DbContext.</summary>
    public async Task<Project> SeedProjectAsync(Guid tenantId, string name)
    {
        using var scope = CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var db = scope.ServiceProvider.GetRequiredService<PmoDbContext>();
        var project = new Project { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Name = name };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public IServiceScope CreateScope() => _app.Services.CreateScope();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string TenantHeader = "X-Test-Tenant";
    public const string RolesHeader = "X-Test-Roles";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TenantHeader, out var tenant))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("tenant_id", tenant.ToString()) };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim("roles", role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, "roles");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal static class HttpClientJsonExtensions
{
    /// <summary>POSTs, insists on 201, and returns the created resource.</summary>
    public static async Task<T> PostAndReadAsync<T>(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(payload), $"POST {url} returned an empty body.");

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
