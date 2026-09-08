using System.Net;
using System.Net.Http.Json;
using SmoPmo.Smo;
using Xunit;

namespace Smo.Tests;

/// <summary>
/// F5's acceptance line needs edges to draw, and this is where they come from: cause-effect
/// links between objectives (PRD §6.1.6), created through the API and surfaced on the
/// scorecard aggregate so the strategy map does not need a second round trip.
/// </summary>
public sealed class ObjectiveLinkTests
{
    private static readonly Guid Tenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ALinkCreatedThroughTheApiAppearsOnTheScorecardAggregate()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        var (strategy, cause, effect) = await SeedTwoObjectivesAsync(client);

        var link = await client.PostAndReadAsync<ObjectiveLinkResponse>("/api/smo/objective-links",
            new ObjectiveLinkWriteModel(cause.Id, effect.Id, "Faster delivery lifts satisfaction"));

        Assert.Equal(cause.Id, link.SourceObjectiveId);
        Assert.Equal(effect.Id, link.TargetObjectiveId);

        var scorecard = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard");
        var edge = Assert.Single(scorecard!.ObjectiveLinks);
        Assert.Equal(link.Id, edge.Id);
        Assert.Equal(cause.Id, edge.SourceObjectiveId);
        Assert.Equal(effect.Id, edge.TargetObjectiveId);

        // GET filters both by the objective at either end and by the owning strategy.
        var byObjective = await client.GetFromJsonAsync<List<ObjectiveLinkResponse>>($"/api/smo/objective-links?objectiveId={cause.Id}");
        Assert.Single(byObjective!);
        var byStrategy = await client.GetFromJsonAsync<List<ObjectiveLinkResponse>>($"/api/smo/objective-links?strategyId={strategy.Id}");
        Assert.Single(byStrategy!);

        var deleted = await client.DeleteAsync($"/api/smo/objective-links/{link.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard");
        Assert.Empty(afterDelete!.ObjectiveLinks);
    }

    [Fact]
    public async Task AnObjectiveCannotLinkToItselfOrToAnUnknownObjective()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        var (_, cause, _) = await SeedTwoObjectivesAsync(client);

        var selfLink = await client.PostAsJsonAsync("/api/smo/objective-links",
            new ObjectiveLinkWriteModel(cause.Id, cause.Id, null));
        Assert.Equal(HttpStatusCode.BadRequest, selfLink.StatusCode);

        var unknownTarget = await client.PostAsJsonAsync("/api/smo/objective-links",
            new ObjectiveLinkWriteModel(cause.Id, Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.BadRequest, unknownTarget.StatusCode);
    }

    [Fact]
    public async Task ALinkOnlyEverJoinsObjectivesInTheCallersOwnTenant()
    {
        await using var host = await SmoTestHost.StartAsync();
        var tenantA = host.CreateClient(Tenant, "StrategyOwner");
        var otherTenant = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tenantB = host.CreateClient(otherTenant, "StrategyOwner");

        var (_, causeA, effectA) = await SeedTwoObjectivesAsync(tenantA);

        // Tenant B cannot see tenant A's objectives, so they read as unknown parents, not a
        // cross-tenant link — same shape as MissingRowsAndUnknownParentsAreRejected (B5).
        var crossTenant = await tenantB.PostAsJsonAsync("/api/smo/objective-links",
            new ObjectiveLinkWriteModel(causeA.Id, effectA.Id, null));
        Assert.Equal(HttpStatusCode.BadRequest, crossTenant.StatusCode);
    }

    private static async Task<(StrategyResponse Strategy, ObjectiveResponse Cause, ObjectiveResponse Effect)> SeedTwoObjectivesAsync(HttpClient client)
    {
        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies",
            new StrategyWriteModel("Cause-effect strategy", null, null, null, null, null, null, null));

        var learningGrowth = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives",
            new PerspectiveWriteModel(strategy.Id, "Learning & Growth", null, null, null, 4, false));
        var internalProcess = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives",
            new PerspectiveWriteModel(strategy.Id, "Internal Process", null, null, null, 3, false));

        var cause = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives",
            new ObjectiveWriteModel(learningGrowth.Id, "Upskill delivery teams", null, null, null, null, null, null, 1));
        var effect = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives",
            new ObjectiveWriteModel(internalProcess.Id, "Reduce cycle time", null, null, null, null, null, null, 1));

        return (strategy, cause, effect);
    }
}
