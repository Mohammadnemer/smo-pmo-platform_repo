using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        var middleware = new TenantResolutionMiddleware(next);
        await using var platformDb = NewPlatformDb();
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.True(nextCalled);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000123"), tenantContext.TenantId);
    }

    [Fact]
    public async Task MiddlewareResolvesTenantFromUsersTableWhenTokenHasNoTenantClaim()
    {
        // Mirrors a real Entra External ID token: no tenant_id claim, only the stable "oid"
        // object id — the shape TenantResolutionMiddleware falls back to once B2's
        // custom-claim mapping turns out not to exist.
        var expectedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000456");

        await using var platformDb = NewPlatformDb();
        platformDb.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = expectedTenantId,
            ExternalId = "entra-oid-1",
            Email = "user@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        });
        await platformDb.SaveChangesAsync();

        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("oid", "entra-oid-1")
        }, "Test"));

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.Equal(expectedTenantId, tenantContext.TenantId);
    }

    [Fact]
    public async Task MiddlewareLeavesTenantUnsetWhenNoClaimAndNoMatchingUser()
    {
        await using var platformDb = NewPlatformDb();
        var tenantContext = new TenantContext();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("oid", "unknown-oid")
        }, "Test"));

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, tenantContext, platformDb);

        Assert.Equal(Guid.Empty, tenantContext.TenantId);
    }
}
