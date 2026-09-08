using System.Net;
using System.Net.Http.Json;
using SmoPmo.Pmo;
using SmoPmo.Rollup;
using SmoPmo.Smo;
using Xunit;

namespace Rollup.Tests;

/// <summary>
/// X2's "Done when" line, end to end: moving a task's % complete changes its objective's
/// stored health — plus the roll-up worker's debounce, RAID severity as a second input, the
/// contribution-weighted average across an initiative's delivery vehicles (PRD §5.3), and
/// tenant isolation of the recompute. Every assertion reads a *stored* value (never a
/// recomputed-on-read one) through the real SMO/PMO/Rollup HTTP surface, and flushes the
/// worker's debounce queue via <see cref="RollupTestHost.FlushRollupAsync"/> instead of
/// waiting on the real timer (architecture-mvp.md §5: "debounced... so editing 50 tasks ≠ 50
/// recomputes" — the queue itself is exercised, just not real wall-clock time).
/// </summary>
public sealed class RollupHealthTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task MovingATasksPercentCompleteChangesItsObjectivesStoredHealth()
    {
        await using var host = await RollupTestHost.StartAsync();
        var client = host.CreateClient(TenantA, "Admin");

        var (objectiveId, initiativeId) = await CreateStrategyTreeAsync(client);
        var projectId = await CreateProjectAsync(client);
        await LinkAsync(client, initiativeId, projectId, weight: 100m);

        var task = await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(projectId, "Build the thing", null, null, 10, false, 20m, null));

        await host.FlushRollupAsync();

        var afterCreate = await client.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveId}");
        Assert.Equal(RagStatus.Red, afterCreate!.Health);
        Assert.Equal(20m, afterCreate.HealthScore);

        // The move: only the task's % complete changes, nothing else.
        var putResponse = await client.PutAsJsonAsync($"/api/pmo/tasks/{task.Id}",
            new TaskWriteModel(projectId, "Build the thing", null, null, 10, false, 100m, null));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        await host.FlushRollupAsync();

        var afterUpdate = await client.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveId}");
        Assert.Equal(RagStatus.Green, afterUpdate!.Health);
        Assert.Equal(100m, afterUpdate.HealthScore);
        Assert.True(afterUpdate.HealthComputedAt > afterCreate.HealthComputedAt);

        // The graph walks all the way to the top (architecture-mvp.md §5): the strategy
        // owning this objective (through its perspective) is stored-healthy too.
        var perspective = await client.GetFromJsonAsync<PerspectiveResponse>(
            $"/api/smo/perspectives/{afterUpdate.PerspectiveId}");
        var strategy = await client.GetFromJsonAsync<StrategyResponse>($"/api/smo/strategies/{perspective!.StrategyId}");
        Assert.Equal(RagStatus.Green, strategy!.Health);
        Assert.Equal(100m, strategy.HealthScore);

        // ...and down through the project → program leg PMO owns.
        var project = await client.GetFromJsonAsync<ProjectResponse>($"/api/pmo/projects/{projectId}");
        Assert.Equal(PmoRagStatus.Green, project!.Health);
        var program = await client.GetFromJsonAsync<ProgramResponse>($"/api/pmo/programs/{project.ProgramId}");
        Assert.Equal(PmoRagStatus.Green, program!.Health);
    }

    [Fact]
    public async Task AnOpenCriticalRaidItemPullsAFullyCompleteProjectsHealthDown()
    {
        await using var host = await RollupTestHost.StartAsync();
        var client = host.CreateClient(TenantA, "Admin");

        var (objectiveId, initiativeId) = await CreateStrategyTreeAsync(client);
        var projectId = await CreateProjectAsync(client);
        await LinkAsync(client, initiativeId, projectId, weight: 100m);

        await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(projectId, "Finished work", null, null, 5, false, 100m, null));
        await host.FlushRollupAsync();

        var beforeRaid = await client.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveId}");
        Assert.Equal(RagStatus.Green, beforeRaid!.Health);

        await client.PostAndReadAsync<RaidItemResponse>("/api/pmo/raid",
            new RaidItemWriteModel("Vendor delay", null, "Risk", "Critical", null, null, null, null, null, projectId));
        await host.FlushRollupAsync();

        // 100 (progress) - 25 (one open Critical RAID item) = 75 -> Amber, not Green.
        var afterRaid = await client.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveId}");
        Assert.Equal(RagStatus.Amber, afterRaid!.Health);
        Assert.Equal(75m, afterRaid.HealthScore);
    }

    [Fact]
    public async Task InitiativeHealthIsAContributionWeightedAverageOfItsDeliveryVehicles()
    {
        await using var host = await RollupTestHost.StartAsync();
        var client = host.CreateClient(TenantA, "Admin");

        var (objectiveId, initiativeId) = await CreateStrategyTreeAsync(client);
        var healthyProjectId = await CreateProjectAsync(client);
        var unhealthyProjectId = await CreateProjectAsync(client);

        // 70% weight on a fully-complete project, 30% on one that hasn't started:
        // 100 * 0.7 + 0 * 0.3 = 70 -> Amber, not Green and not Red.
        await LinkAsync(client, initiativeId, healthyProjectId, weight: 70m);
        await LinkAsync(client, initiativeId, unhealthyProjectId, weight: 30m);

        await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(healthyProjectId, "Done", null, null, 5, false, 100m, null));
        await host.FlushRollupAsync();

        await client.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(unhealthyProjectId, "Not started", null, null, 5, false, 0m, null));
        await host.FlushRollupAsync();

        var initiative = await client.GetFromJsonAsync<InitiativeResponse>($"/api/smo/initiatives/{initiativeId}");
        Assert.Equal(RagStatus.Amber, initiative!.Health);
        Assert.Equal(70m, initiative.HealthScore);

        var objective = await client.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveId}");
        Assert.Equal(RagStatus.Amber, objective!.Health);
        Assert.Equal(70m, objective.HealthScore);
    }

    [Fact]
    public async Task RecomputingOneTenantsProjectNeverTouchesAnotherTenantsObjective()
    {
        await using var host = await RollupTestHost.StartAsync();
        var clientA = host.CreateClient(TenantA, "Admin");
        var clientB = host.CreateClient(TenantB, "Admin");

        var (objectiveA, initiativeA) = await CreateStrategyTreeAsync(clientA);
        var projectA = await CreateProjectAsync(clientA);
        await LinkAsync(clientA, initiativeA, projectA, weight: 100m);

        var (objectiveB, initiativeB) = await CreateStrategyTreeAsync(clientB);
        var projectB = await CreateProjectAsync(clientB);
        await LinkAsync(clientB, initiativeB, projectB, weight: 100m);

        await clientA.PostAndReadAsync<TaskResponse>("/api/pmo/tasks",
            new TaskWriteModel(projectA, "Tenant A work", null, null, 5, false, 100m, null));
        await host.FlushRollupAsync();

        var objectiveAAfter = await clientA.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveA}");
        Assert.Equal(RagStatus.Green, objectiveAAfter!.Health);

        // Tenant B's identically-shaped graph never got a task write, so it never signalled
        // the worker and stays exactly as fresh as it started.
        var objectiveBAfter = await clientB.GetFromJsonAsync<ObjectiveResponse>($"/api/smo/objectives/{objectiveB}");
        Assert.Equal(RagStatus.NotSet, objectiveBAfter!.Health);
        Assert.Null(objectiveBAfter.HealthScore);
    }

    private static async Task<(Guid ObjectiveId, Guid InitiativeId)> CreateStrategyTreeAsync(HttpClient client)
    {
        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies",
            new StrategyWriteModel("Digital Transformation", null, null, null, null, null, null, null));
        var perspective = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives",
            new PerspectiveWriteModel(strategy.Id, "Customer", null, null, null, 0, false));
        var objective = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives",
            new ObjectiveWriteModel(perspective.Id, "Improve onboarding", null, null, null, null, null, null, 0));
        var initiative = await client.PostAndReadAsync<InitiativeResponse>("/api/smo/initiatives",
            new InitiativeWriteModel(objective.Id, "Unified Platform Rollout", null, null, null, null, null, null, null, null, null, null));

        return (objective.Id, initiative.Id);
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var portfolio = await client.PostAndReadAsync<PortfolioResponse>("/api/pmo/portfolios",
            new PortfolioWriteModel("Core Portfolio", null));
        var program = await client.PostAndReadAsync<ProgramResponse>("/api/pmo/programs",
            new ProgramWriteModel(portfolio.Id, "Core Platform Program", null));
        var project = await client.PostAndReadAsync<ProjectResponse>("/api/pmo/projects",
            new ProjectWriteModel(program.Id, "Tenant Onboarding Revamp", null, null, null));

        return project.Id;
    }

    private static Task LinkAsync(HttpClient client, Guid initiativeId, Guid projectId, decimal weight) =>
        client.PostAndReadAsync<InitiativeDeliveryLinkResponse>("/api/rollup/links",
            new InitiativeDeliveryLinkWriteModel(initiativeId, null, projectId, weight >= 100m, weight));
}
