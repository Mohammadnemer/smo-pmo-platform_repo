using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmoPmo.Api.Middleware;
using SmoPmo.Shared.Multitenancy;
using Xunit;

namespace Platform.Tests;

public sealed class TenantResolutionTests
{
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
        await middleware.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000123"), tenantContext.TenantId);
    }
}
