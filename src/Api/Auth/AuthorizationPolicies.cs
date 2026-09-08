using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Api.Auth;

public enum PermissionAction
{
    Read,
    Write,
    Delete
}

public sealed class AuthorizationRequirement : IAuthorizationRequirement
{
    public AuthorizationRequirement(string resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    public string Resource { get; }
    public PermissionAction Action { get; }
}

public sealed class AuthorizationHandler : AuthorizationHandler<AuthorizationRequirement>
{
    private readonly ITenantContext _tenantContext;

    public AuthorizationHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AuthorizationRequirement requirement)
    {
        // Azure AD/Entra emits app roles under the "roles" claim, but ASP.NET Core's
        // default JWT inbound claim mapping renames the singular "role" (and, in
        // practice, Entra's own role claim) to the long ClaimTypes.Role URI before this
        // handler ever sees it — check all three shapes rather than relying on one.
        var roles = context.User.Claims.Where(x => x.Type == "role" || x.Type == "roles" || x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orgUnitId = context.User.FindFirst("org_unit_id")?.Value;
        var hasOrgUnit = string.IsNullOrWhiteSpace(orgUnitId) == false;

        var allowed = requirement.Resource switch
        {
            "Project" when requirement.Action == PermissionAction.Write => roles.Contains("PM") || roles.Contains("Admin"),
            "Project" when requirement.Action == PermissionAction.Read => roles.Contains("PM") || roles.Contains("Contributor") || roles.Contains("Admin") || hasOrgUnit,

            // Strategy covers the whole SMO surface (perspectives, objectives, KPIs,
            // initiatives). Writing it is the SMO lead's job; KPI owners update actuals
            // (PRD §7 matrix), while everyone in the tenant may read the scorecard.
            "Strategy" when requirement.Action == PermissionAction.Write =>
                roles.Contains("Admin") || roles.Contains("StrategyOwner") || roles.Contains("KpiOwner"),
            "Strategy" when requirement.Action == PermissionAction.Read =>
                roles.Contains("Admin") || roles.Contains("StrategyOwner") || roles.Contains("KpiOwner")
                || roles.Contains("PM") || roles.Contains("Contributor") || roles.Contains("Executive") || hasOrgUnit,

            // Linking an initiative to its delivery vehicle (X1) is a strategy-owner or PM
            // action; everyone who can read either side of the link can read it too.
            "Rollup" when requirement.Action == PermissionAction.Write =>
                roles.Contains("Admin") || roles.Contains("StrategyOwner") || roles.Contains("PM"),
            "Rollup" when requirement.Action == PermissionAction.Read =>
                roles.Contains("Admin") || roles.Contains("StrategyOwner") || roles.Contains("PM")
                || roles.Contains("Contributor") || roles.Contains("KpiOwner") || roles.Contains("Executive") || hasOrgUnit,

            "Admin" => roles.Contains("Admin"),
            _ => false
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
