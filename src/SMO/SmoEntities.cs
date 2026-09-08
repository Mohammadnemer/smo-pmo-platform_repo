using SmoPmo.Platform;

namespace SmoPmo.Smo;

/// <summary>
/// SMO domain entities. The SMO module owns these types *and* their tables — nothing
/// outside /SMO reads or writes them (architecture-mvp.md §3, §10).
///
/// Every user-facing label carries an <c>…Ar</c> twin: AR/EN is a line-one non-negotiable,
/// so the API is bilingual at the data layer rather than being retrofitted for the SPA.
/// </summary>
public sealed class Strategy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public string? Vision { get; set; }
    public string? Mission { get; set; }
    public DateOnly? HorizonStart { get; set; }
    public DateOnly? HorizonEnd { get; set; }

    /// <summary>Stored roll-up health, written by the X2 worker. Never recomputed on read.</summary>
    public RagStatus Health { get; set; } = RagStatus.NotSet;
    public decimal? HealthScore { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }

    public ICollection<Perspective> Perspectives { get; } = new List<Perspective>();
}

public sealed class Perspective : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid StrategyId { get; set; }
    public Strategy? Strategy { get; set; }

    /// <summary>Tenants may reorder and hide the four BSC perspectives (PRD §6.1.2).</summary>
    public int DisplayOrder { get; set; }
    public bool IsHidden { get; set; }

    public ICollection<Objective> Objectives { get; } = new List<Objective>();
}

public sealed class Objective : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid PerspectiveId { get; set; }
    public Perspective? Perspective { get; set; }

    public string? TargetState { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Stored roll-up health, written by the X2 worker. Never recomputed on read.</summary>
    public RagStatus Health { get; set; } = RagStatus.NotSet;
    public decimal? HealthScore { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }

    public ICollection<Kpi> Kpis { get; } = new List<Kpi>();
    public ICollection<Initiative> Initiatives { get; } = new List<Initiative>();
}

public sealed class Kpi : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid ObjectiveId { get; set; }
    public Objective? Objective { get; set; }

    public string? Unit { get; set; }
    public KpiDirection Direction { get; set; } = KpiDirection.HigherIsBetter;
    public KpiFrequency Frequency { get; set; } = KpiFrequency.Monthly;
    public decimal? Baseline { get; set; }
    public decimal? Target { get; set; }

    /// <summary>Latest measurement, denormalised from <see cref="KpiMeasurement"/> on append.</summary>
    public decimal? Actual { get; set; }
    public DateTimeOffset? ActualAsOf { get; set; }

    /// <summary>
    /// RAG bands as a percentage of target achievement: green at or above
    /// <see cref="GreenThresholdPercent"/>, amber at or above <see cref="AmberThresholdPercent"/>,
    /// red below. Evaluated per row from this KPI's own fields — this is measure
    /// evaluation, not the roll-up graph, so it is not a stored-health value.
    /// </summary>
    public decimal GreenThresholdPercent { get; set; } = 95m;
    public decimal AmberThresholdPercent { get; set; } = 80m;

    public string? Formula { get; set; }
    public string? DataSource { get; set; }
    public Guid? OwnerUserId { get; set; }

    public ICollection<KpiMeasurement> Measurements { get; } = new List<KpiMeasurement>();
}

/// <summary>One point in a KPI's historical series — what the scorecard sparkline reads.</summary>
public sealed class KpiMeasurement : BaseEntity
{
    public Guid KpiId { get; set; }
    public Kpi? Kpi { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Value { get; set; }
    public string? Note { get; set; }
}

public sealed class Initiative : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public Guid ObjectiveId { get; set; }
    public Objective? Objective { get; set; }

    public string? Sponsor { get; set; }
    public decimal? Budget { get; set; }
    public string? BudgetCurrency { get; set; }
    public string? ExpectedBenefit { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Lifecycle state. Kept as a plain string so B7's fixed flow (and B9's configurable
    /// engine) owns the transitions rather than the SMO schema.
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Stored roll-up health from linked delivery work (X1/X2). Never recomputed on read.</summary>
    public RagStatus Health { get; set; } = RagStatus.NotSet;
    public decimal? HealthScore { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }
}

/// <summary>
/// A cause-effect edge between two objectives — the arrows the strategy map draws
/// (PRD §6.1.6, §6.1.1 domain model). Directional: <see cref="SourceObjectiveId"/> is the
/// cause, <see cref="TargetObjectiveId"/> the effect it drives (typically the perspective
/// above it on the map — e.g. a Learning &amp; Growth objective feeding an Internal Process
/// one). No navigation collection back on <see cref="Objective"/>: a self-referencing FK
/// used both ways from the same principal needs two independent relationships, which is
/// simpler to configure from this side alone (see <see cref="SmoDbContext"/>).
/// </summary>
public sealed class ObjectiveLink : BaseEntity
{
    public Guid SourceObjectiveId { get; set; }
    public Objective? SourceObjective { get; set; }
    public Guid TargetObjectiveId { get; set; }
    public Objective? TargetObjective { get; set; }
    public string? Note { get; set; }
}

public enum RagStatus
{
    NotSet = 0,
    Red = 1,
    Amber = 2,
    Green = 3
}

public enum KpiDirection
{
    HigherIsBetter = 0,
    LowerIsBetter = 1
}

public enum KpiFrequency
{
    Monthly = 0,
    Quarterly = 1,
    SemiAnnual = 2,
    Annual = 3
}
