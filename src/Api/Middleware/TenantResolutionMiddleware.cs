using Microsoft.AspNetCore.Http;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // ITenantContext is resolved per invocation, not in the constructor: middleware itself is
    // effectively a singleton, and a captured TenantContext would serve one request's tenant
    // to every later request.
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var tenantId))
            {
                tenantContext.TenantId = tenantId;
            }
        }

        await _next(context);
    }
}
