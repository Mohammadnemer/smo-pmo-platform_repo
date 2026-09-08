using System.Net;
using System.Net.Http.Json;
using SmoPmo.Smo;
using Xunit;

namespace Smo.Tests;

/// <summary>
/// Isolation and access control on the SMO surface. RLS is the database-side guarantee and is
/// verified against PostgreSQL (see docs/sessions/B5.md); what these tests pin down is that the
/// API layer never hands one tenant another tenant's rows, and that the RBAC matrix gates writes.
/// </summary>
public sealed class SmoAccessTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task OneTenantCannotSeeOrFetchAnotherTenantsStrategy()
    {
        await using var host = await SmoTestHost.StartAsync();

        var tenantA = host.CreateClient(TenantA, "StrategyOwner");
        var tenantB = host.CreateClient(TenantB, "StrategyOwner");

        var strategy = await tenantA.PostAndReadAsync<StrategyResponse>("/api/smo/strategies",
            new StrategyWriteModel("Tenant A strategy", null, null, null, null, null, null, null));

        var listedForB = await tenantB.GetFromJsonAsync<List<StrategyResponse>>("/api/smo/strategies");
        Assert.Empty(listedForB!);

        var fetchedByB = await tenantB.GetAsync($"/api/smo/strategies/{strategy.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetchedByB.StatusCode);

        var scorecardForB = await tenantB.GetAsync($"/api/smo/strategies/{strategy.Id}/scorecard");
        Assert.Equal(HttpStatusCode.NotFound, scorecardForB.StatusCode);

        // Tenant B cannot hang its own perspective off tenant A's strategy either.
        var hijack = await tenantB.PostAsJsonAsync("/api/smo/perspectives",
            new PerspectiveWriteModel(strategy.Id, "Financial", null, null, null, 1, false));
        Assert.Equal(HttpStatusCode.BadRequest, hijack.StatusCode);

        var listedForA = await tenantA.GetFromJsonAsync<List<StrategyResponse>>("/api/smo/strategies");
        Assert.Single(listedForA!);
    }

    [Fact]
    public async Task AReaderCanReadTheScorecardButCannotWriteIt()
    {
        await using var host = await SmoTestHost.StartAsync();

        var owner = host.CreateClient(TenantA, "StrategyOwner");
        var reader = host.CreateClient(TenantA, "Contributor");

        var strategy = await owner.PostAndReadAsync<StrategyResponse>("/api/smo/strategies",
            new StrategyWriteModel("Readable strategy", null, null, null, null, null, null, null));

        var read = await reader.GetAsync($"/api/smo/strategies/{strategy.Id}/scorecard");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await reader.PostAsJsonAsync("/api/smo/strategies",
            new StrategyWriteModel("Contributor strategy", null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        var delete = await reader.DeleteAsync($"/api/smo/strategies/{strategy.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerGetsNothing()
    {
        await using var host = await SmoTestHost.StartAsync();
        var anonymous = host.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/api/smo/strategies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ARoleWithNoSmoEntitlementIsRefusedEvenForReads()
    {
        await using var host = await SmoTestHost.StartAsync();
        var stranger = host.CreateClient(TenantA, "Vendor");

        var response = await stranger.GetAsync("/api/smo/strategies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
