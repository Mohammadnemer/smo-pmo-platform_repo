using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmoPmo.Api.Auth;

/// <summary>
/// Dev-only stand-in for the real Entra External ID bearer token. Only ever registered
/// when <c>AddApiAuthentication</c> sees <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsDevelopment"/>
/// — the real JwtBearer scheme, which 401s every call until F1's deferred Entra tenant/app
/// registration exists, is untouched everywhere else.
///
/// Always authenticates: there is no real credential to check, so a caller either gets the
/// fixed dev identity (tenant = <c>DefaultTenantId</c>, role Admin — enough to satisfy every
/// policy in <see cref="AuthenticationExtensions"/>) or, for tests of a different tenant/role
/// locally, one overridden via the optional <see cref="TenantHeader"/>/<see cref="RolesHeader"/>
/// headers. Mirrors the claim shape (and the header-override pattern) of
/// tests/Smo.Tests/SmoTestHost.cs's TestAuthenticationHandler so both paths exercise the same
/// tenant-resolution and RBAC code.
/// </summary>
internal sealed class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";
    public const string TenantHeader = "X-Dev-Tenant";
    public const string RolesHeader = "X-Dev-Roles";

    private readonly IConfiguration _configuration;

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var defaultTenantId = _configuration["DefaultTenantId"] ?? "00000000-0000-0000-0000-000000000001";
        var tenantId = Request.Headers.TryGetValue(TenantHeader, out var tenantHeader) && !string.IsNullOrWhiteSpace(tenantHeader)
            ? tenantHeader.ToString()
            : defaultTenantId;

        var roles = Request.Headers.TryGetValue(RolesHeader, out var rolesHeader) && !string.IsNullOrWhiteSpace(rolesHeader)
            ? rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { "Admin" };

        var claims = new List<Claim>
        {
            new("tenant_id", tenantId),
            new(ClaimTypes.Name, "dev-user")
        };
        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, "roles");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
