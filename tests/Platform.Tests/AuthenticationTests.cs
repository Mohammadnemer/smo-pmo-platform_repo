using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Platform.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task ProtectedEndpointRequiresAuthentication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://example.test",
            ["Authentication:Audience"] = "api://smo-pmo-platform",
            ["Authentication:RequireHttpsMetadata"] = "false"
        });

        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
        {
            options.Authority = "https://example.test";
            options.Audience = "api://smo-pmo-platform";
            options.RequireHttpsMetadata = false;
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/me", () => Results.Ok(new { authenticated = true })).RequireAuthorization();

        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();
            var response = await client.GetAsync("/me");
            Assert.Equal(StatusCodes.Status401Unauthorized, (int)response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
