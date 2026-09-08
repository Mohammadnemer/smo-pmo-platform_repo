using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmoPmo.Pmo;
using SmoPmo.Rollup;
using SmoPmo.Smo;

namespace SmoPmo.Api.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authority = configuration["Authentication:Authority"] ?? "https://login.microsoftonline.com/tenant-id/v2.0";
        var audience = configuration["Authentication:Audience"] ?? "api://smo-pmo-platform";
        var clientId = configuration["Authentication:ClientId"];
        var requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);

        // v1.0 access tokens carry `aud` as the API's App ID URI; v2.0 tokens (set via
        // accessTokenAcceptedVersion: 2 on the app registration) carry `aud` as the app's
        // plain client ID GUID instead. Accept either so a manifest/token-version change
        // on the Entra side doesn't silently 401 every request again.
        var validAudiences = new[] { audience, clientId }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToArray();

        // The real Entra External ID tenant/app registration is a deferred follow-up
        // (F1/F2) — until it exists, JwtBearer alone 401s every local call, which is what
        // blocked F4 from rendering anything live. In Development only, DevAuthentication
        // Handler stands in as the default scheme so the SPA (and manual testing) can
        // exercise real tenant-resolution/RBAC logic without a live IdP. ASPNETCORE_
        // ENVIRONMENT defaults to Production when unset, so this never activates outside
        // an explicit local `dotnet run`.
        var useDevAuth = environment.IsDevelopment();

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = useDevAuth ? DevAuthenticationHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = useDevAuth ? DevAuthenticationHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme;
        });

        authBuilder.AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(authority),
                    ValidateAudience = validAudiences.Length > 0,
                    ValidAudiences = validAudiences,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = false
                };
            });

        if (useDevAuth)
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
                DevAuthenticationHandler.SchemeName, _ => { });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminPolicy", policy => policy.Requirements.Add(new AuthorizationRequirement("Admin", PermissionAction.Read)));

            // The SMO and PMO modules ask for these by name (SmoPolicies/PmoPolicies); who
            // satisfies them is decided here, in the composition root, against B3's entity ×
            // action matrix. PMO reuses B3's "Project" resource — it already covers the whole
            // PMO surface the same way "Strategy" covers the whole SMO surface.
            options.AddPolicy(SmoPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Strategy", PermissionAction.Read)));
            options.AddPolicy(SmoPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Strategy", PermissionAction.Write)));
            options.AddPolicy(PmoPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Read)));
            options.AddPolicy(PmoPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Write)));

            // Roll-up (X1) spans both domains — a "Rollup" resource of its own rather than
            // reusing Strategy or Project, since either a strategy owner or a PM may need to
            // link an initiative to its delivery vehicle.
            options.AddPolicy(RollupPolicies.Read, policy => policy.Requirements.Add(new AuthorizationRequirement("Rollup", PermissionAction.Read)));
            options.AddPolicy(RollupPolicies.Write, policy => policy.Requirements.Add(new AuthorizationRequirement("Rollup", PermissionAction.Write)));
        });

        return services;
    }
}
