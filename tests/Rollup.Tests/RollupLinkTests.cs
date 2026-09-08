using System.Net;
using System.Net.Http.Json;
using SmoPmo.Rollup;
using Xunit;

namespace Rollup.Tests;

/// <summary>
/// X1's "Done when" line, end to end: an initiative lists the programs and projects
/// delivering it, with the primary flag and contribution weight the PRD's contribution
/// model calls for (architecture-mvp.md §5, PRD §5.3) — plus the validation, cross-module
/// existence checks, RBAC and tenant isolation around it.
/// </summary>
public sealed class RollupLinkTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task AnInitiativeListsTheProgramAndProjectDeliveringIt()
    {
        await using var host = await RollupTestHost.StartAsync();

        var initiative = await host.SeedInitiativeAsync(TenantA, "Unified Platform Rollout");
        var program = await host.SeedProgramAsync(TenantA, "Core Platform Program");
        var project = await host.SeedProjectAsync(TenantA, "Tenant Onboarding Revamp");

        var client = host.CreateClient(TenantA, "StrategyOwner");

        await client.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, null, true, 70m));
        await client.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, null, project.Id, false, 30m));

        var delivery = await client.GetFromJsonAsync<InitiativeDeliveryResponse>(
            $"/api/rollup/initiatives/{initiative.Id}/delivery");

        Assert.NotNull(delivery);
        Assert.Equal("Unified Platform Rollout", delivery!.InitiativeName);

        var deliveredProgram = Assert.Single(delivery.Programs);
        Assert.Equal("Core Platform Program", deliveredProgram.Name);
        Assert.True(deliveredProgram.IsPrimary);
        Assert.Equal(70m, deliveredProgram.ContributionWeight);

        var deliveredProject = Assert.Single(delivery.Projects);
        Assert.Equal("Tenant Onboarding Revamp", deliveredProject.Name);
        Assert.False(deliveredProject.IsPrimary);
        Assert.Equal(30m, deliveredProject.ContributionWeight);
    }

    [Fact]
    public async Task ExactlyOneOfProgramOrProjectMustBeSet()
    {
        await using var host = await RollupTestHost.StartAsync();
        var initiative = await host.SeedInitiativeAsync(TenantA, "Neither vehicle set");
        var client = host.CreateClient(TenantA, "StrategyOwner");

        var neitherSet = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, null, null, false, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, neitherSet.StatusCode);

        var program = await host.SeedProgramAsync(TenantA, "Some Program");
        var project = await host.SeedProjectAsync(TenantA, "Some Project");
        var bothSet = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, project.Id, false, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, bothSet.StatusCode);
    }

    [Fact]
    public async Task LinkingToAnUnknownInitiativeProgramOrProjectIsRejected()
    {
        await using var host = await RollupTestHost.StartAsync();
        var client = host.CreateClient(TenantA, "StrategyOwner");

        var unknownInitiative = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(Guid.NewGuid(), Guid.NewGuid(), null, false, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, unknownInitiative.StatusCode);

        var initiative = await host.SeedInitiativeAsync(TenantA, "Real initiative");
        var unknownProgram = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, Guid.NewGuid(), null, false, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, unknownProgram.StatusCode);

        var unknownProject = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, null, Guid.NewGuid(), false, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, unknownProject.StatusCode);
    }

    [Fact]
    public async Task OnlyOnePrimaryLinkPerInitiativeIsAllowed()
    {
        await using var host = await RollupTestHost.StartAsync();
        var initiative = await host.SeedInitiativeAsync(TenantA, "Two primaries");
        var programA = await host.SeedProgramAsync(TenantA, "Program A");
        var programB = await host.SeedProgramAsync(TenantA, "Program B");
        var client = host.CreateClient(TenantA, "StrategyOwner");

        await client.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, programA.Id, null, true, 100m));

        var secondPrimary = await client.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, programB.Id, null, true, 100m));

        Assert.Equal(HttpStatusCode.BadRequest, secondPrimary.StatusCode);
    }

    [Fact]
    public async Task AReaderCanListLinksButCannotWriteThem()
    {
        await using var host = await RollupTestHost.StartAsync();
        var initiative = await host.SeedInitiativeAsync(TenantA, "Readable initiative");
        var program = await host.SeedProgramAsync(TenantA, "Readable program");

        var owner = host.CreateClient(TenantA, "StrategyOwner");
        var link = await owner.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, null, true, 100m));

        var reader = host.CreateClient(TenantA, "Contributor");
        var read = await reader.GetAsync("/api/rollup/links");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await reader.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, null, false, 50m));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        var delete = await reader.DeleteAsync($"/api/rollup/links/{link.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerGetsNothing()
    {
        await using var host = await RollupTestHost.StartAsync();
        var anonymous = host.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/api/rollup/links");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OneTenantCannotSeeOrLinkAnotherTenantsInitiative()
    {
        await using var host = await RollupTestHost.StartAsync();

        var initiative = await host.SeedInitiativeAsync(TenantA, "Tenant A initiative");
        var program = await host.SeedProgramAsync(TenantA, "Tenant A program");

        var tenantA = host.CreateClient(TenantA, "StrategyOwner");
        var link = await tenantA.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, null, true, 100m));

        var tenantB = host.CreateClient(TenantB, "StrategyOwner");

        // Tenant A's initiative doesn't exist as far as SMO's tenant filter is concerned
        // for tenant B, so the cross-module lookup treats it as unknown.
        var hijack = await tenantB.PostAsJsonAsync("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiative.Id, program.Id, null, false, 50m));
        Assert.Equal(HttpStatusCode.BadRequest, hijack.StatusCode);

        var listedForB = await tenantB.GetFromJsonAsync<List<InitiativeDeliveryLinkResponse>>("/api/rollup/links");
        Assert.Empty(listedForB!);

        var deliveryForB = await tenantB.GetAsync($"/api/rollup/initiatives/{initiative.Id}/delivery");
        Assert.Equal(HttpStatusCode.NotFound, deliveryForB.StatusCode);

        var listedForA = await tenantA.GetFromJsonAsync<List<InitiativeDeliveryLinkResponse>>("/api/rollup/links");
        Assert.Single(listedForA!);
        Assert.Equal(link.Id, listedForA![0].Id);
    }
}
