namespace SmoPmo.Pmo;

/// <summary>
/// Entity ↔ contract translation. Kept in one place so no endpoint can accidentally project
/// a tenant id, or let a client write a computed schedule field.
/// </summary>
internal static class PmoMapping
{
    public static void Apply(this Portfolio entity, PortfolioWriteModel model)
    {
        entity.Name = model.Name.Trim();
        entity.Description = Clean(model.Description);
    }

    public static void Apply(this Program entity, ProgramWriteModel model)
    {
        entity.PortfolioId = model.PortfolioId;
        entity.Name = model.Name.Trim();
        entity.Description = Clean(model.Description);
    }

    public static void Apply(this Project entity, ProjectWriteModel model)
    {
        entity.ProgramId = model.ProgramId;
        entity.Name = model.Name.Trim();
        entity.Description = Clean(model.Description);
        entity.StartDate = model.StartDate;
        entity.Status = Clean(model.Status) ?? entity.Status;
    }

    public static void Apply(this TaskItem entity, TaskWriteModel model)
    {
        entity.ProjectId = model.ProjectId;
        entity.Name = model.Name.Trim();
        entity.Description = Clean(model.Description);
        entity.WbsCode = Clean(model.WbsCode);
        entity.IsMilestone = model.IsMilestone;
        entity.DurationDays = model.IsMilestone ? 0 : model.DurationDays;
        entity.PercentComplete = model.PercentComplete;
        entity.AssigneeUserId = model.AssigneeUserId;
        entity.ConstraintStart = model.ConstraintStart;
    }

    public static void Apply(this Dependency entity, DependencyWriteModel model)
    {
        entity.ProjectId = model.ProjectId;
        entity.PredecessorTaskId = model.PredecessorTaskId;
        entity.SuccessorTaskId = model.SuccessorTaskId;
        entity.Type = model.Type.Trim().ToUpperInvariant();
        entity.LagDays = model.LagDays;
    }

    public static void Apply(this RaidItem entity, RaidItemWriteModel model)
    {
        entity.Title = model.Title.Trim();
        entity.Description = Clean(model.Description);
        entity.Category = model.Category.Trim();
        entity.Severity = model.Severity.Trim();
        entity.Status = Clean(model.Status) ?? entity.Status;
        entity.OwnerUserId = model.OwnerUserId;
        entity.DueDate = model.DueDate;
        entity.PortfolioId = model.PortfolioId;
        entity.ProgramId = model.ProgramId;
        entity.ProjectId = model.ProjectId;
    }

    public static void Apply(this StatusReport entity, StatusReportWriteModel model)
    {
        entity.ProjectId = model.ProjectId;
        entity.ProgramId = model.ProgramId;
        entity.ReportDate = model.ReportDate;
        entity.PeriodStart = model.PeriodStart;
        entity.PeriodEnd = model.PeriodEnd;
        entity.OverallStatus = model.OverallStatus;
        entity.Accomplishments = Clean(model.Accomplishments);
        entity.NextPeriodPlan = Clean(model.NextPeriodPlan);
        entity.KeyRaidNotes = Clean(model.KeyRaidNotes);
        entity.MilestoneStatus = Clean(model.MilestoneStatus);
    }

    public static PortfolioResponse ToResponse(this Portfolio e) => new(e.Id, e.Name, e.Description);

    public static ProgramResponse ToResponse(this Program e) => new(
        e.Id, e.PortfolioId, e.Name, e.Description, e.Health, e.HealthScore, e.HealthComputedAt);

    public static ProjectResponse ToResponse(this Project e) => new(
        e.Id, e.ProgramId, e.Name, e.Description, e.StartDate, e.Status,
        e.Health, e.HealthScore, e.HealthComputedAt);

    public static TaskResponse ToResponse(this TaskItem e) => new(
        e.Id, e.ProjectId, e.Name, e.Description, e.WbsCode, e.DurationDays, e.IsMilestone,
        e.PercentComplete, e.AssigneeUserId, e.ConstraintStart, e.ScheduleStart, e.ScheduleFinish,
        e.TotalFloatDays, e.IsCritical);

    public static DependencyResponse ToResponse(this Dependency e) => new(
        e.Id, e.ProjectId, e.PredecessorTaskId, e.SuccessorTaskId, e.Type, e.LagDays);

    public static RaidItemResponse ToResponse(this RaidItem e) => new(
        e.Id, e.Title, e.Description, e.Category, e.Severity, e.Status, e.OwnerUserId, e.DueDate,
        e.PortfolioId, e.ProgramId, e.ProjectId);

    public static StatusReportResponse ToResponse(this StatusReport e) => new(
        e.Id, e.ProjectId, e.ProgramId, e.ReportDate, e.PeriodStart, e.PeriodEnd, e.OverallStatus,
        e.Accomplishments, e.NextPeriodPlan, e.KeyRaidNotes, e.MilestoneStatus);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
