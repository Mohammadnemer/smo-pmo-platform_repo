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
                // looking the signed-in user's stable object id up in the Users table instead.
                // Users has no FORCE ROW LEVEL SECURITY (Platform's AddRowLevelSecurity
                // migration), so the table owner — the role the API connects as — can read it
                // before the tenant is known, same as SeedTenantAsync's pre-tenant Tenants
                // read at startup.
                var externalId = context.User.FindFirst("oid")?.Value
                    ?? context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrWhiteSpace(externalId))
                {
                    var user = await platformDb.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.ExternalId == externalId, context.RequestAborted);
                    if (user is not null)
                    {
                        tenantContext.TenantId = user.TenantId;
                    }
                }
            }
        }

        await _next(context);
    }
}
