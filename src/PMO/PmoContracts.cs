namespace SmoPmo.Pmo;

// ───────────────────────────── Write models ─────────────────────────────
// Deliberately separate from the entities: a client must not be able to POST a TenantId, an
// Id, or a computed schedule field — ScheduleStart/Finish, TotalFloatDays and IsCritical are
// written only by ScheduleQuery after a CPM recompute.

public sealed record PortfolioWriteModel(string Name, string? Description);

public sealed record ProgramWriteModel(Guid PortfolioId, string Name, string? Description);

public sealed record ProjectWriteModel(
    Guid ProgramId,
    string Name,
    string? Description,
    DateOnly? StartDate,
    string? Status);

public sealed record TaskWriteModel(
    Guid ProjectId,
    string Name,
    string? Description,
    string? WbsCode,
    int DurationDays,
    bool IsMilestone,
    decimal PercentComplete,
    Guid? AssigneeUserId,
    DateOnly? ConstraintStart = null);

public sealed record DependencyWriteModel(
    Guid ProjectId,
    Guid PredecessorTaskId,
    Guid SuccessorTaskId,
    string Type,
    int LagDays);

public sealed record RaidItemWriteModel(
    string Title,
    string? Description,
    string Category,
    string Severity,
    string? Status,
    Guid? OwnerUserId,
    DateOnly? DueDate,
    Guid? PortfolioId,
    Guid? ProgramId,
    Guid? ProjectId);

public sealed record StatusReportWriteModel(
    Guid? ProjectId,
    Guid? ProgramId,
    DateOnly ReportDate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PmoRagStatus OverallStatus,
    string? Accomplishments,
    string? NextPeriodPlan,
    string? KeyRaidNotes,
    string? MilestoneStatus);

// ───────────────────────────── Read models ─────────────────────────────

public sealed record PortfolioResponse(Guid Id, string Name, string? Description);

public sealed record ProgramResponse(
    Guid Id,
    Guid PortfolioId,
    string Name,
    string? Description,
    PmoRagStatus Health,
    decimal? HealthScore,
    DateTimeOffset? HealthComputedAt);

public sealed record ProjectResponse(
    Guid Id,
    Guid ProgramId,
    string Name,
    string? Description,
    DateOnly? StartDate,
    string Status,
    PmoRagStatus Health,
    decimal? HealthScore,
    DateTimeOffset? HealthComputedAt);

public sealed record TaskResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    string? WbsCode,
    int DurationDays,
    bool IsMilestone,
    decimal PercentComplete,
    Guid? AssigneeUserId,
    DateOnly? ConstraintStart,
    DateOnly? ScheduleStart,
    DateOnly? ScheduleFinish,
    int? TotalFloatDays,
    bool IsCritical);

public sealed record DependencyResponse(
    Guid Id,
    Guid ProjectId,
    Guid PredecessorTaskId,
    Guid SuccessorTaskId,
    string Type,
    int LagDays);

public sealed record RaidItemResponse(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    string Severity,
    string Status,
    Guid? OwnerUserId,
    DateOnly? DueDate,
    Guid? PortfolioId,
    Guid? ProgramId,
    Guid? ProjectId);

public sealed record StatusReportResponse(
    Guid Id,
    Guid? ProjectId,
    Guid? ProgramId,
    DateOnly ReportDate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PmoRagStatus OverallStatus,
    string? Accomplishments,
    string? NextPeriodPlan,
    string? KeyRaidNotes,
    string? MilestoneStatus);

// ─────────────────────────── Schedule aggregate (read) ───────────────────────────
// The CPM engine wired server-side (B6's own line): the whole computed schedule in one
// round trip, plus the one-line answer "what is the critical path" a Gantt UI needs.

public sealed record ScheduleResponse(
    Guid ProjectId,
    DateOnly CalendarAnchor,
    DateOnly? ProjectFinish,
    IReadOnlyList<TaskResponse> Tasks,
    IReadOnlyList<DependencyResponse> Dependencies);

public sealed record CriticalPathResponse(
    Guid ProjectId,
    DateOnly? ProjectFinish,
    IReadOnlyList<TaskResponse> Tasks);
