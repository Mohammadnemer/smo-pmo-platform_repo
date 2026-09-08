using System.Net;
using System.Net.Http.Json;
using SmoPmo.Pmo;
using Xunit;

namespace Pmo.Tests;

/// <summary>
/// Isolation and access control on the PMO surface. RLS is the database-side guarantee
/// (see PostgresRlsTests); what these tests pin down is that the API layer never hands one
/// tenant another tenant's rows, and that the RBAC matrix gates writes. Mirrors
/// Smo.Tests/SmoAccessTests.cs (B5).
/// </summary>
public sealed class PmoAccessTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task OneTenantCannotSeeOrFetchAnotherTenantsPortfolio()
    {
        await using var host = await PmoTestHost.StartAsync();

        var tenantA = host.CreateClient(TenantA, "PM");
        var tenantB = host.CreateClient(TenantB, "PM");

        var portfolio = await tenantA.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios",
            new PortfolioWriteModel("Tenant A portfolio", null));

        var listedForB = await tenantB.GetFromJsonAsync<List<PortfolioResponse>>("/api/pmo/portfolios");
        Assert.Empty(listedForB!);

        var fetchedByB = await tenantB.GetAsync($"/api/pmo/portfolios/{portfolio.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetchedByB.StatusCode);

        // Tenant B cannot hang its own program off tenant A's portfolio either.
        var hijack = await tenantB.PostAsJsonAsync("/api/pmo/programs",
            new ProgramWriteModel(portfolio.Id, "Hijacked program", null));
        Assert.Equal(HttpStatusCode.BadRequest, hijack.StatusCode);

        var listedForA = await tenantA.GetFromJsonAsync<List<PortfolioResponse>>("/api/pmo/portfolios");
        Assert.Single(listedForA!);
    }

    [Fact]
    public async Task AContributorCanReadTheScheduleButCannotWriteIt()
    {
        await using var host = await PmoTestHost.StartAsync();

        var pm = host.CreateClient(TenantA, "PM");
        var reader = host.CreateClient(TenantA, "Contributor");

        var portfolio = await pm.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios", new PortfolioWriteModel("Readable", null));

        var read = await reader.GetAsync("/api/pmo/portfolios");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await reader.PostAsJsonAsync("/api/pmo/portfolios", new PortfolioWriteModel("Contributor portfolio", null));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        var delete = await reader.DeleteAsync($"/api/pmo/portfolios/{portfolio.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerGetsNothing()
    {
        await using var host = await PmoTestHost.StartAsync();
        var anonymous = host.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/api/pmo/portfolios");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
