namespace SmoPmo.Smo;

// ───────────────────────────── Write models ─────────────────────────────
// Deliberately separate from the entities: a client must not be able to POST a
// TenantId, an Id, or a rolled-up Health value. Tenancy is stamped by the
// interceptor; Health is written only by the roll-up worker (X2).

public sealed record StrategyWriteModel(
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Vision,
    string? Mission,
    DateOnly? HorizonStart,
    DateOnly? HorizonEnd);

public sealed record PerspectiveWriteModel(
    Guid StrategyId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    int DisplayOrder,
    bool IsHidden);

public sealed record ObjectiveWriteModel(
    Guid PerspectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? TargetState,
    Guid? OwnerUserId,
    Guid? OrgUnitId,
    int DisplayOrder);

public sealed record KpiWriteModel(
    Guid ObjectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Unit,
    KpiDirection Direction,
    KpiFrequency Frequency,
    decimal? Baseline,
    decimal? Target,
    decimal? GreenThresholdPercent,
    decimal? AmberThresholdPercent,
    string? Formula,
    string? DataSource,
    Guid? OwnerUserId);

public sealed record KpiMeasurementWriteModel(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Value,
    string? Note);

public sealed record ObjectiveLinkWriteModel(
    Guid SourceObjectiveId,
    Guid TargetObjectiveId,
    string? Note);

public sealed record InitiativeWriteModel(
    Guid ObjectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Sponsor,
    decimal? Budget,
    string? BudgetCurrency,
    string? ExpectedBenefit,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Status);

// ───────────────────────────── Read models ─────────────────────────────

public sealed record StrategyResponse(
    Guid Id,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Vision,
    string? Mission,
    DateOnly? HorizonStart,
    DateOnly? HorizonEnd,
    RagStatus Health,
    decimal? HealthScore,
    DateTimeOffset? HealthComputedAt);

public sealed record PerspectiveResponse(
    Guid Id,
    Guid StrategyId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    int DisplayOrder,
    bool IsHidden);

public sealed record ObjectiveResponse(
    Guid Id,
    Guid PerspectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? TargetState,
    Guid? OwnerUserId,
    Guid? OrgUnitId,
    int DisplayOrder,
    RagStatus Health,
    decimal? HealthScore,
    DateTimeOffset? HealthComputedAt);

public sealed record KpiResponse(
    Guid Id,
    Guid ObjectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Unit,
    KpiDirection Direction,
    KpiFrequency Frequency,
    decimal? Baseline,
    decimal? Target,
    decimal? Actual,
    DateTimeOffset? ActualAsOf,
    decimal GreenThresholdPercent,
    decimal AmberThresholdPercent,
    string? Formula,
    string? DataSource,
    Guid? OwnerUserId,
    RagStatus Rag,
    decimal? AchievementPercent);

public sealed record KpiMeasurementResponse(
    Guid Id,
    Guid KpiId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Value,
    string? Note);

public sealed record InitiativeResponse(
    Guid Id,
    Guid ObjectiveId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Sponsor,
    decimal? Budget,
    string? BudgetCurrency,
    string? ExpectedBenefit,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    RagStatus Health,
    decimal? HealthScore,
    DateTimeOffset? HealthComputedAt);

public sealed record ObjectiveLinkResponse(
    Guid Id,
    Guid SourceObjectiveId,
    Guid TargetObjectiveId,
    string? Note);

// ─────────────────────── Scorecard aggregate (read) ───────────────────────
// The one non-CRUD endpoint: the whole scorecard in a single round trip, so F4/F5
// do not N+1 across perspectives → objectives → KPIs. See ADR 0002.

public sealed record ScorecardResponse(
    StrategyResponse Strategy,
    RagCounts KpiRagCounts,
    IReadOnlyList<ScorecardPerspective> Perspectives,
    // The strategy map's cause-effect edges (F5) — only edges between two objectives that
    // are both in view, so hiding a perspective (includeHidden=false) can't leave a
    // dangling arrow pointing at an objective the response never sent.
    IReadOnlyList<ObjectiveLinkResponse> ObjectiveLinks);

public sealed record ScorecardPerspective(
    PerspectiveResponse Perspective,
    RagCounts KpiRagCounts,
    IReadOnlyList<ScorecardObjective> Objectives);

public sealed record ScorecardObjective(
    ObjectiveResponse Objective,
    RagCounts KpiRagCounts,
    IReadOnlyList<ScorecardKpi> Kpis,
    IReadOnlyList<InitiativeResponse> Initiatives);

public sealed record ScorecardKpi(
    KpiResponse Kpi,
    IReadOnlyList<KpiMeasurementResponse> Trend);

/// <summary>
/// Tally of KPI RAG bands over rows already loaded for this response. Counting bands
/// in a projection is not the same thing as recomputing roll-up health — objective,
/// initiative and strategy <c>Health</c> above are always the stored values.
/// </summary>
public sealed record RagCounts(int Green, int Amber, int Red, int NotSet)
{
    public static RagCounts From(IEnumerable<RagStatus> statuses)
    {
        var green = 0;
        var amber = 0;
        var red = 0;
        var notSet = 0;

        foreach (var status in statuses)
        {
            switch (status)
            {
                case RagStatus.Green:
                    green++;
                    break;
                case RagStatus.Amber:
                    amber++;
                    break;
                case RagStatus.Red:
                    red++;
                    break;
                default:
                    notSet++;
                    break;
            }
        }

        return new RagCounts(green, amber, red, notSet);
    }
}
