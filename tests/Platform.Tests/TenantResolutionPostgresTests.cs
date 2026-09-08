using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmoPmo.Api.Middleware;
using SmoPmo.Platform;
using SmoPmo.Shared.Multitenancy;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Tests;

/// <summary>
/// The part of the Users-table tenant-resolution fallback only a real PostgreSQL server can
/// prove: that app.resolve_tenant_id (AddUserTenantResolutionFunction) actually bypasses RLS
/// via SECURITY DEFINER. The in-memory provider used by TenantResolutionTests can't run raw SQL
/// against a Postgres-only function, and would falsely pass even if the function were missing.
///
/// Skip-attributed for the same reason as Smo.Tests/PostgresRlsTests.cs: point
/// PLATFORM_TEST_POSTGRES at a server (or fix the local credentials) and delete the Skip to run.
/// </summary>
public sealed class TenantResolutionPostgresTests
{
    private readonly ITestOutputHelper _output;

    public TenantResolutionPostgresTests(ITestOutputHelper output) => _output = output;

    private const string SkipReason =
        "Requires a reachable PostgreSQL server. Set PLATFORM_TEST_POSTGRES and delete this Skip to run.";

    private const string TestDatabase = "smo_pmo_platform_platformtest";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PLATFORM_TEST_POSTGRES")
        ?? $"Host=localhost;Database={TestDatabase};Username=postgres;Password=postgres";

    [Fact(Skip = SkipReason)]
    public async Task MiddlewareResolvesTenantViaSecurityDefinerFunctionEvenWithoutTenantContext()
    {
        var connectionString = await TryConnectAsync();
        if (connectionString is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setup = new PlatformDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        var tenantId = Guid.NewGuid();
        await using (var seed = new PlatformDbContext(options))
        {
            seed.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ExternalId = "entra-oid-1",
                Email = "user@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test"
            });
            await seed.SaveChangesAsync();
        }

        // A fresh context/connection with app.tenant_id never set — the exact pre-tenant state
        // the real middleware runs the lookup in. A plain LINQ query here would be filtered to
        // nothing by RLS; only the SECURITY DEFINER function can see across that boundary.
        await using var platformDb = new PlatformDbContext(options);
        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("oid", "entra-oid-1")
        }, "Test"));

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, NullLogger<TenantResolutionMiddleware>.Instance);
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.Equal(tenantId, tenantContext.TenantId);
    }

    private async Task<string?> TryConnectAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Timeout = 3 };
        var serverProbe = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres" };

        try
        {
            await using var connection = new NpgsqlConnection(serverProbe.ConnectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine(
                $"SKIPPED — no PostgreSQL server at {builder.Host}:{builder.Port} ({ex.GetType().Name}: {ex.Message}). " +
                "Set PLATFORM_TEST_POSTGRES to run this test.");
            return null;
        }

        _output.WriteLine($"Running against PostgreSQL at {builder.Host}:{builder.Port}, database {builder.Database}.");
        return builder.ConnectionString;
    }
}
