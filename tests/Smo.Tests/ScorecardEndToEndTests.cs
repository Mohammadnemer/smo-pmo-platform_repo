using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SmoPmo.Platform;
using SmoPmo.Smo;
using Xunit;

namespace Smo.Tests;

/// <summary>
/// B5's acceptance line: the scorecard's data can be built end to end through the API.
/// Nothing here touches a DbContext to set data up — every row is created over HTTP.
/// </summary>
public sealed class ScorecardEndToEndTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ScorecardIsBuiltEndToEndThroughTheApi()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        // 1. Strategy
        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies", new StrategyWriteModel(
            Name: "Vision 2030",
            NameAr: "رؤية 2030",
            Description: "Group strategy",
            DescriptionAr: "استراتيجية المجموعة",
            Vision: "Best-run portfolio in the region",
            Mission: "Deliver strategy through disciplined execution",
            HorizonStart: new DateOnly(2026, 1, 1),
            HorizonEnd: new DateOnly(2030, 12, 31)));

        // 2. Two BSC perspectives, one of them ordered second
        var financial = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives", new PerspectiveWriteModel(
            strategy.Id, "Financial", "مالي", null, null, DisplayOrder: 1, IsHidden: false));
        var customer = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives", new PerspectiveWriteModel(
            strategy.Id, "Customer", "العميل", null, null, DisplayOrder: 2, IsHidden: false));

        // 3. An objective under each
        var growRevenue = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives", new ObjectiveWriteModel(
            financial.Id, "Grow revenue", "زيادة الإيرادات", null, null, "AED 500m by 2030", null, null, DisplayOrder: 1));
        var delight = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives", new ObjectiveWriteModel(
            customer.Id, "Delight customers", "إسعاد العملاء", null, null, "CSAT 90", null, null, DisplayOrder: 1));

        // 4. KPIs: one that will land green, one amber, one red, one with no actual at all
        var revenue = await client.PostAndReadAsync<KpiResponse>("/api/smo/kpis", KpiModel(growRevenue.Id, "Revenue", target: 100m));
        var churn = await client.PostAndReadAsync<KpiResponse>("/api/smo/kpis", KpiModel(
            growRevenue.Id, "Churn", target: 5m, direction: KpiDirection.LowerIsBetter));
        var csat = await client.PostAndReadAsync<KpiResponse>("/api/smo/kpis", KpiModel(delight.Id, "CSAT", target: 90m));
        var nps = await client.PostAndReadAsync<KpiResponse>("/api/smo/kpis", KpiModel(delight.Id, "NPS", target: 50m));

        Assert.Equal(RagStatus.NotSet, revenue.Rag);

        // 5. Actuals, including a three-point series for the trend line
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{revenue.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 80m, "January"));
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{revenue.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 90m, "February"));
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{revenue.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 96m, "March"));

        // Lower-is-better: coming in at 4 against a target of 5 is over-achievement.
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{churn.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 4m, null));

        // 85 of 90 = 94.4% — amber, just under the green band.
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{csat.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 85m, null));

        // 20 of 50 = 40% — red.
        await client.PostAndReadAsync<KpiMeasurementResponse>($"/api/smo/kpis/{nps.Id}/measurements",
            new KpiMeasurementWriteModel(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 20m, null));

        // 6. A funded initiative against the objective that is behind
        var initiative = await client.PostAndReadAsync<InitiativeResponse>("/api/smo/initiatives", new InitiativeWriteModel(
            delight.Id, "Customer experience programme", "برنامج تجربة العميل", null, null,
            Sponsor: "COO", Budget: 2_500_000m, BudgetCurrency: "aed", ExpectedBenefit: "+10 CSAT",
            StartDate: new DateOnly(2026, 4, 1), EndDate: new DateOnly(2027, 3, 31), Status: "Draft"));

        // 7. The whole scorecard in one call
        var scorecard = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard");

        Assert.NotNull(scorecard);
        Assert.Equal("Vision 2030", scorecard.Strategy.Name);
        Assert.Equal("رؤية 2030", scorecard.Strategy.NameAr);

        // Perspectives come back in the tenant's configured order.
        Assert.Equal(new[] { "Financial", "Customer" }, scorecard.Perspectives.Select(p => p.Perspective.Name));

        var financialScorecard = scorecard.Perspectives[0];
        var revenueOnScorecard = financialScorecard.Objectives
            .SelectMany(o => o.Kpis)
            .Single(k => k.Kpi.Id == revenue.Id);

        // Latest actual won, RAG was evaluated, and the trend series is in period order.
        Assert.Equal(96m, revenueOnScorecard.Kpi.Actual);
        Assert.Equal(RagStatus.Green, revenueOnScorecard.Kpi.Rag);
        Assert.Equal(96m, revenueOnScorecard.Kpi.AchievementPercent);
        Assert.Equal(new[] { 80m, 90m, 96m }, revenueOnScorecard.Trend.Select(t => t.Value));

        var churnOnScorecard = financialScorecard.Objectives
            .SelectMany(o => o.Kpis)
            .Single(k => k.Kpi.Id == churn.Id);
        Assert.Equal(RagStatus.Green, churnOnScorecard.Kpi.Rag);
        Assert.Equal(125m, churnOnScorecard.Kpi.AchievementPercent);

        var customerScorecard = scorecard.Perspectives[1];
        var customerObjective = Assert.Single(customerScorecard.Objectives);
        Assert.Equal(new RagCounts(Green: 0, Amber: 1, Red: 1, NotSet: 0), customerObjective.KpiRagCounts);

        var initiativeOnScorecard = Assert.Single(customerObjective.Initiatives);
        Assert.Equal(initiative.Id, initiativeOnScorecard.Id);
        Assert.Equal("AED", initiativeOnScorecard.BudgetCurrency);
        Assert.Equal(2_500_000m, initiativeOnScorecard.Budget);

        // Whole-strategy tally: revenue + churn green, CSAT amber, NPS red.
        Assert.Equal(new RagCounts(Green: 2, Amber: 1, Red: 1, NotSet: 0), scorecard.KpiRagCounts);

        // Roll-up health stays untouched by this session: X2 stores it, nothing recomputes it.
        Assert.Equal(RagStatus.NotSet, scorecard.Strategy.Health);
        Assert.All(scorecard.Perspectives.SelectMany(p => p.Objectives),
            objective => Assert.Equal(RagStatus.NotSet, objective.Objective.Health));
    }

    [Fact]
    public async Task HiddenPerspectivesAreExcludedUnlessAskedFor()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies", NewStrategy("Ops strategy"));
        await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives", new PerspectiveWriteModel(
            strategy.Id, "Learning & Growth", null, null, null, DisplayOrder: 4, IsHidden: true));

        var visible = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard");
        var all = await client.GetFromJsonAsync<ScorecardResponse>($"/api/smo/strategies/{strategy.Id}/scorecard?includeHidden=true");

        Assert.Empty(visible!.Perspectives);
        Assert.Single(all!.Perspectives);
    }

    [Fact]
    public async Task EveryWriteThroughTheApiIsAudited()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies", NewStrategy("Audited strategy"));

        var updated = await client.PutAsJsonAsync($"/api/smo/strategies/{strategy.Id}", NewStrategy("Audited strategy v2"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await client.DeleteAsync($"/api/smo/strategies/{strategy.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var scope = host.CreateScope();
        var auditEntries = await host.GetRequiredService<PlatformDbContext>(scope).AuditEntries
            .Where(e => e.EntityId == strategy.Id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        Assert.Equal(new[] { "Create", "Update", "Delete" }, auditEntries.Select(e => e.Action));
        Assert.All(auditEntries, entry =>
        {
            Assert.Equal("SMO", entry.Module);
            Assert.Equal("Strategy", entry.EntityName);
            Assert.Equal(Tenant, entry.TenantId);
        });
    }

    [Fact]
    public async Task MissingRowsAndUnknownParentsAreRejected()
    {
        await using var host = await SmoTestHost.StartAsync();
        var client = host.CreateClient(Tenant, "StrategyOwner");

        var missing = await client.GetAsync($"/api/smo/strategies/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var orphan = await client.PostAsJsonAsync("/api/smo/perspectives", new PerspectiveWriteModel(
            Guid.NewGuid(), "Financial", null, null, null, 1, false));
        Assert.Equal(HttpStatusCode.BadRequest, orphan.StatusCode);

        var nameless = await client.PostAsJsonAsync("/api/smo/strategies", NewStrategy("   "));
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);

        var strategy = await client.PostAndReadAsync<StrategyResponse>("/api/smo/strategies", NewStrategy("Threshold checks"));
        var perspective = await client.PostAndReadAsync<PerspectiveResponse>("/api/smo/perspectives", new PerspectiveWriteModel(
            strategy.Id, "Financial", null, null, null, 1, false));
        var objective = await client.PostAndReadAsync<ObjectiveResponse>("/api/smo/objectives", new ObjectiveWriteModel(
            perspective.Id, "Grow revenue", null, null, null, null, null, null, 1));

        var invertedBands = await client.PostAsJsonAsync("/api/smo/kpis", KpiModel(
            objective.Id, "Broken KPI", target: 10m, green: 50m, amber: 90m));
        Assert.Equal(HttpStatusCode.BadRequest, invertedBands.StatusCode);
    }

    private static StrategyWriteModel NewStrategy(string name) =>
        new(name, null, null, null, null, null, null, null);

    private static KpiWriteModel KpiModel(
        Guid objectiveId,
        string name,
        decimal target,
        KpiDirection direction = KpiDirection.HigherIsBetter,
        decimal? green = null,
        decimal? amber = null) =>
        new(objectiveId, name, null, null, null, Unit: "count", Direction: direction,
            Frequency: KpiFrequency.Monthly, Baseline: 0m, Target: target,
            GreenThresholdPercent: green, AmberThresholdPercent: amber,
            Formula: null, DataSource: "manual", OwnerUserId: null);
}

internal static class HttpClientJsonExtensions
{
    /// <summary>POSTs, insists on 201, and returns the created resource.</summary>
    public static async Task<T> PostAndReadAsync<T>(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(payload), $"POST {url} returned an empty body.");

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
