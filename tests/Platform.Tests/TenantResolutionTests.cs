using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmoPmo.Api.Middleware;
using SmoPmo.Platform;
using SmoPmo.Shared.Multitenancy;
using Xunit;

namespace Platform.Tests;

public sealed class TenantResolutionTests
{
    private static PlatformDbContext NewPlatformDb() => new(
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TenantResolutionMiddleware NewMiddleware(RequestDelegate next) =>
        new(next, NullLogger<TenantResolutionMiddleware>.Instance);

    [Fact]
    public async Task MiddlewareSetsTenantFromJwtClaim()
    {
        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", "00000000-0000-0000-0000-000000000123"),
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        }, "Test"));

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // The tenant context is passed per invocation, not captured in the constructor
        // (B5): middleware is effectively a singleton and must not hold one request's tenant.
        var middleware = NewMiddleware(next);
        await using var platformDb = NewPlatformDb();
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.True(nextCalled);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000123"), tenantContext.TenantId);
    }

    [Fact]
    public async Task MiddlewareLeavesTenantUnsetWhenNoTenantClaimAndNoExternalIdClaimEither()
    {
        // No tenant_id, no oid/sub/NameIdentifier either — the fallback has nothing to look
        // up, so it must short-circuit before touching the database. Verified by using an
        // in-memory PlatformDbContext that would throw on the raw SQL the real fallback issues
        // (app.resolve_tenant_id is Postgres-only — see TenantResolutionPostgresTests for the
        // path that actually calls it).
        await using var platformDb = NewPlatformDb();
        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "Test"));

        var middleware = NewMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.Equal(Guid.Empty, tenantContext.TenantId);
    }
}
