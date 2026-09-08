using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmoPmo.Api.Auth;
using SmoPmo.Shared.Multitenancy;
using Xunit;

namespace Platform.Tests;

public sealed class AuthorizationTests
{
    [Fact]
    public async Task ContributorIsDeniedAdminActionAndAllowedProjectRead()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ITenantContext, TenantContext>();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminPolicy", policy => policy.Requirements.Add(new AuthorizationRequirement("Admin", PermissionAction.Read)));
            options.AddPolicy("ProjectReadPolicy", policy => policy.Requirements.Add(new AuthorizationRequirement("Project", PermissionAction.Read)));
        });
        builder.Services.AddSingleton<IAuthorizationHandler, AuthorizationHandler>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/admin", [Authorize(Policy = "AdminPolicy")] () => Results.Ok(new { authorized = true })).RequireAuthorization();
        app.MapGet("/projects", [Authorize(Policy = "ProjectReadPolicy")] () => Results.Ok(new { authorized = true })).RequireAuthorization();

        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();
            var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
            adminRequest.Headers.Add("X-Test-User", "Contributor");

            var projectRequest = new HttpRequestMessage(HttpMethod.Get, "/projects");
            projectRequest.Headers.Add("X-Test-User", "Contributor");

            var adminResponse = await client.SendAsync(adminRequest);
            var projectResponse = await client.SendAsync(projectRequest);

            Assert.Equal(StatusCodes.Status403Forbidden, (int)adminResponse.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, (int)projectResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Context.Request.Headers.TryGetValue("X-Test-User", out var value) || value != "Contributor")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("role", "Contributor"),
                new Claim("org_unit_id", Guid.NewGuid().ToString())
            }, Scheme.Name);

            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
