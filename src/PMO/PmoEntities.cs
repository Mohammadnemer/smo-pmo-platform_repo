using SmoPmo.Platform;

namespace SmoPmo.Pmo;

/// <summary>
/// PMO domain entities. As of B6 the PMO module owns these types *and* their tables —
/// nothing outside /PMO reads or writes them (architecture-mvp.md §3, §10), mirroring the
/// move SMO made in B5. See docs/sessions/B6.md.
/// </summary>
public sealed class Portfolio : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Program> Programs { get; } = new List<Program>();
}

public sealed class Program : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid PortfolioId { get; set; }
    public Portfolio? Portfolio { get; set; }
    public ICollection<Project> Projects { get; } = new List<Project>();

    /// <summary>Stored roll-up health, written by the X2 worker: an average of this
    /// program's projects' own stored health. Never recomputed on read.</summary>
    public PmoRagStatus Health { get; set; } = PmoRagStatus.NotSet;
    public decimal? HealthScore { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }
}

public sealed class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ProgramId { get; set; }
    public Program? Program { get; set; }

    /// <summary>Anchor date for schedule math — CPM's working-day index 0 (D4/architecture
    /// §6: schedule math always goes through the tenant's working calendar).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Lifecycle state. Plain string so B7's fixed flow owns the transitions, same
    /// as SMO's Initiative.Status (B5).</summary>
    public string Status { get; set; } = "Draft";

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();
    public ICollection<Dependency> Dependencies { get; } = new List<Dependency>();

    /// <summary>Stored roll-up health, written by the X2 worker from this project's own
    /// tasks (% complete, schedule variance) and RAID items (severity) — the one genuinely
    /// PMO-specific formula in the roll-up graph (architecture-mvp.md §5). Never recomputed
    /// on read.</summary>
    public PmoRagStatus Health { get; set; } = PmoRagStatus.NotSet;
    public decimal? HealthScore { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }
}

/// <summary>Gantt row: name, WBS code, duration, % complete, assignee (PRD §6.2.2) plus the
/// stored CPM output. The schedule fields are only ever written by <c>ScheduleQuery</c> after
/// a recompute — never hand-set by an endpoint — the same "stored, not recomputed on read"
/// rule the roll-up engine follows elsewhere in the platform.</summary>
public sealed class TaskItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public string? WbsCode { get; set; }
    public int DurationDays { get; set; } = 1;
    public bool IsMilestone { get; set; }
    public decimal PercentComplete { get; set; }
    public Guid? AssigneeUserId { get; set; }

    /// <summary>"Start no earlier than" constraint (F8: dragging a task in the Gantt sets this).
    /// <see langword="null"/> means the task is purely dependency-driven, the CPM engine's
    /// original behaviour. Set, it becomes a floor under the engine's forward pass so the task
    /// (and anything downstream of it) never schedules earlier — the standard CPM constraint,
    /// not a hand-set schedule override.</summary>
    public DateOnly? ConstraintStart { get; set; }

    /// <summary>Stored CPM output. <see langword="null"/> until the project's schedule has
    /// been computed at least once.</summary>
    public DateOnly? ScheduleStart { get; set; }
    public DateOnly? ScheduleFinish { get; set; }
    public int? TotalFloatDays { get; set; }
    public bool IsCritical { get; set; }
}

/// <summary>FS / SS / FF / SF with lead/lag (PRD §6.2.2). Lag may be negative (lead time).</summary>
public sealed class Dependency : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid PredecessorTaskId { get; set; }
    public Guid SuccessorTaskId { get; set; }
    public string Type { get; set; } = "FS";
    public int LagDays { get; set; }
}

/// <summary>Risks, Actions, Issues, Decisions — present at portfolio, program and project
/// level (PRD §6.2.6): exactly one of the three parent ids is set.</summary>
public sealed class RaidItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Risk";
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public Guid? OwnerUserId { get; set; }
    public DateOnly? DueDate { get; set; }

    public Guid? PortfolioId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }
}

/// <summary>Structured periodic status report per project/program (PRD §6.2.7): snapshot +
/// history, so creating one is always an append, never an update.</summary>
public sealed class StatusReport : BaseEntity
{
    public Guid? ProjectId { get; set; }
    public Guid? ProgramId { get; set; }

    public DateOnly ReportDate { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public PmoRagStatus OverallStatus { get; set; } = PmoRagStatus.NotSet;
    public string? Accomplishments { get; set; }
    public string? NextPeriodPlan { get; set; }
    public string? KeyRaidNotes { get; set; }
    public string? MilestoneStatus { get; set; }
}

/// <summary>
/// One row per tenant: the per-tenant weekend that drives all schedule date math
/// (architecture-mvp.md non-negotiables — Jordan/GCC defaults to Fri–Sat). Auto-created
/// with that default the first time a project's schedule is computed; P3 owns letting a
/// tenant admin edit it.
/// </summary>
public sealed class PmoCalendar : BaseEntity
{
    public DayOfWeek WeekendDay1 { get; set; } = DayOfWeek.Friday;
    public DayOfWeek? WeekendDay2 { get; set; } = DayOfWeek.Saturday;
}

public enum PmoRagStatus
{
    NotSet = 0,
    Red = 1,
    Amber = 2,
    Green = 3
}
