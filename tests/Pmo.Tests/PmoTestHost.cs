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
using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;

namespace Pmo.Tests;

/// <summary>
/// Spins up the real PMO endpoints, tenant middleware, RBAC matrix and audit seam over an
/// in-memory store. Only the token issuer is faked — a test scheme reads the tenant and roles
/// from request headers so a single host can act as different callers and different tenants.
/// Mirrors Smo.Tests/SmoTestHost.cs (B5).
/// </summary>
internal sealed class PmoTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private PmoTestHost(WebApplication app) => _app = app;

    /// <param name="configurePmoStore">
    /// Overrides the PMO store; defaults to a private in-memory database. The PostgreSQL tests
    /// pass a real connection so the same endpoints are exercised against real SQL and RLS.
    /// </param>
    public static async Task<PmoTestHost> StartAsync(Action<DbContextOptionsBuilder>? configurePmoStore = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<IMessageBus, InProcessMessageBus>();

        // The module under test, wired through its own registration entry point.
        builder.Services.AddPmoModule(configurePmoStore ?? (options => options.UseInMemoryDatabase(databaseName)));

        // Platform stands in only as the audit log's owner, so the cross-module audit path
        // is exercised end to end instead of being stubbed out.
        builder.Services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IDomainEventHandler<EntitiesChangedEvent>, EntitiesChangedEventHandler>();

        builder.Services.AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(PmoPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Read)));
            options.AddPolicy(PmoPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Write)));
        });
        builder.Services.AddScoped<IAuthorizationHandler, AuthorizationHandler>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.MapPmoEndpoints();

        await app.StartAsync();
        return new PmoTestHost(app);
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

    /// <summary>
    /// Drops and re-applies the PMO schema, so a relational run starts from the migration
    /// itself rather than from whatever a previous run left behind.
    /// </summary>
    public async Task ResetPmoDatabaseAsync()
    {
        using var scope = CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PmoDbContext>().Database;
        await database.EnsureDeletedAsync();
        await database.MigrateAsync();
    }

    public T GetRequiredService<T>(IServiceScope scope) where T : notnull =>
        scope.ServiceProvider.GetRequiredService<T>();

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

        // Same claim shape the Entra External ID token carries (B2), so the tenant middleware
        // and RBAC handler see exactly what they see in production.
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

        Xunit.Assert.True(response.IsSuccessStatusCode, $"POST {url} returned {response.StatusCode}: {payload}");
        Xunit.Assert.False(string.IsNullOrWhiteSpace(payload), $"POST {url} returned an empty body.");

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
