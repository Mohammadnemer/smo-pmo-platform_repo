using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmoPmo.Platform;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
                    try
                    {
                        var resolved = await platformDb.Database
                            .SqlQueryRaw<Guid?>("SELECT app.resolve_tenant_id({0}) AS \"Value\"", externalId)
                            .FirstOrDefaultAsync(context.RequestAborted);
                        if (resolved is { } resolvedTenantId)
                        {
                            tenantContext.TenantId = resolvedTenantId;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Best-effort enrichment, not a required step: app.resolve_tenant_id
                        // might not exist yet (deploy-api.yml never runs migrations — see
                        // docs/sql/2026-09-08-add-user-tenant-resolution-function.sql), the
                        // connection might be down, or Postgres might reject the call for a
                        // reason narrower catches keep missing. Whatever the cause, degrade to
                        // the same "tenant unresolved" state the rest of the app already
                        // handles rather than 500ing every authenticated request over it.
                        _logger.LogWarning(ex,
                            "Failed to resolve tenant via app.resolve_tenant_id for external id {ExternalId}",
                            externalId);
                    }
                }
            }
        }

        await _next(context);
    }
}
