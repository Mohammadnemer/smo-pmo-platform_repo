using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmoPmo.Platform;
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
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, PlatformDbContext platformDb)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var tenantId))
            {
                tenantContext.TenantId = tenantId;
            }
            else
            {
                // Real Entra External ID tokens carry no tenant_id claim — that mapping was
                // never configured on the Entra side (B2 follow-up). Resolve the tenant by
                // looking the signed-in user's stable object id up against Users instead, via
                // the app.resolve_tenant_id SECURITY DEFINER function (AddUserTenantResolution
                // Function). A plain LINQ query here would run as smopmo_app — a deliberately
                // non-owner role RLS still applies to — and get filtered by "TenantId" =
                // app.current_tenant_id() before the tenant is even known; the function runs
                // with its owner's privileges instead, and returns only the one uuid needed.
                var externalId = context.User.FindFirst("oid")?.Value
                    ?? context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrWhiteSpace(externalId))
                {
                    var resolved = await platformDb.Database
                        .SqlQueryRaw<Guid?>("SELECT app.resolve_tenant_id({0}) AS \"Value\"", externalId)
                        .FirstOrDefaultAsync(context.RequestAborted);
                    if (resolved is { } resolvedTenantId)
                    {
                        tenantContext.TenantId = resolvedTenantId;
                    }
                }
            }
        }

        await _next(context);
    }
}
