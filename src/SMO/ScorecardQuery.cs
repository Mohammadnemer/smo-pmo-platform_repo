using Microsoft.EntityFrameworkCore;

namespace SmoPmo.Smo;

/// <summary>
/// Builds the scorecard aggregate — strategy → perspectives → objectives → KPIs (with
/// trend) + initiatives — in a fixed number of queries regardless of tree size. Six
/// set-based reads, then assembly in memory; deliberately never one query per node.
/// </summary>
internal static class ScorecardQuery
{
    public const int DefaultTrendPoints = 6;
    public const int MaxTrendPoints = 60;

    public static async Task<ScorecardResponse?> BuildAsync(
        SmoDbContext db,
        Guid strategyId,
        bool includeHidden,
        int trendPoints,
        CancellationToken ct)
    {
        var strategy = await db.Strategies
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == strategyId, ct);

        if (strategy is null)
        {
            return null;
        }

        trendPoints = Math.Clamp(trendPoints, 0, MaxTrendPoints);

        var perspectives = await db.Perspectives
            .AsNoTracking()
            .Where(e => e.StrategyId == strategyId && (includeHidden || !e.IsHidden))
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        var perspectiveIds = perspectives.Select(e => e.Id).ToList();

        var objectives = await db.Objectives
            .AsNoTracking()
            .Where(e => perspectiveIds.Contains(e.PerspectiveId))
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        var objectiveIds = objectives.Select(e => e.Id).ToList();

        var kpis = await db.Kpis
            .AsNoTracking()
            .Where(e => objectiveIds.Contains(e.ObjectiveId))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        var initiatives = await db.Initiatives
            .AsNoTracking()
            .Where(e => objectiveIds.Contains(e.ObjectiveId))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        // Both ends must be in view: a link into a hidden perspective's objective would be
        // an arrow the response never sent a node for.
        var objectiveLinks = await db.ObjectiveLinks
            .AsNoTracking()
            .Where(e => objectiveIds.Contains(e.SourceObjectiveId) && objectiveIds.Contains(e.TargetObjectiveId))
            .ToListAsync(ct);

        var kpiIds = kpis.Select(e => e.Id).ToList();

        var measurements = trendPoints == 0 || kpiIds.Count == 0
            ? new List<KpiMeasurement>()
            : await db.KpiMeasurements
                .AsNoTracking()
                .Where(e => kpiIds.Contains(e.KpiId))
                .OrderBy(e => e.PeriodStart)
                .ThenBy(e => e.PeriodEnd)
                .ToListAsync(ct);

        var trendByKpi = measurements
            .GroupBy(e => e.KpiId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<KpiMeasurementResponse>)group
                    .TakeLast(trendPoints)
                    .Select(SmoMapping.ToResponse)
                    .ToList());

        var kpisByObjective = kpis.GroupBy(e => e.ObjectiveId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var initiativesByObjective = initiatives.GroupBy(e => e.ObjectiveId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var objectivesByPerspective = objectives.GroupBy(e => e.PerspectiveId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var scorecardPerspectives = new List<ScorecardPerspective>(perspectives.Count);
        var allStatuses = new List<RagStatus>(kpis.Count);

        foreach (var perspective in perspectives)
        {
            var perspectiveObjectives = objectivesByPerspective.GetValueOrDefault(perspective.Id) ?? new List<Objective>();
            var scorecardObjectives = new List<ScorecardObjective>(perspectiveObjectives.Count);
            var perspectiveStatuses = new List<RagStatus>();

            foreach (var objective in perspectiveObjectives)
            {
                var objectiveKpis = (kpisByObjective.GetValueOrDefault(objective.Id) ?? new List<Kpi>())
                    .Select(kpi => new ScorecardKpi(
                        kpi.ToResponse(),
                        trendByKpi.GetValueOrDefault(kpi.Id) ?? Array.Empty<KpiMeasurementResponse>()))
                    .ToList();

                var objectiveStatuses = objectiveKpis.Select(e => e.Kpi.Rag).ToList();
                perspectiveStatuses.AddRange(objectiveStatuses);

                scorecardObjectives.Add(new ScorecardObjective(
                    objective.ToResponse(),
                    RagCounts.From(objectiveStatuses),
                    objectiveKpis,
                    (initiativesByObjective.GetValueOrDefault(objective.Id) ?? new List<Initiative>())
                        .Select(SmoMapping.ToResponse)
                        .ToList()));
            }

            allStatuses.AddRange(perspectiveStatuses);

            scorecardPerspectives.Add(new ScorecardPerspective(
                perspective.ToResponse(),
                RagCounts.From(perspectiveStatuses),
                scorecardObjectives));
        }

        return new ScorecardResponse(
            strategy.ToResponse(),
            RagCounts.From(allStatuses),
            scorecardPerspectives,
            objectiveLinks.Select(SmoMapping.ToResponse).ToList());
    }
}
