namespace SmoPmo.Smo;

/// <summary>
/// Entity ↔ contract translation. Kept in one place so no endpoint can accidentally
/// project a tenant id, or let a client write a rolled-up health value.
/// </summary>
internal static class SmoMapping
{
    public static void Apply(this Strategy entity, StrategyWriteModel model)
    {
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.Vision = Clean(model.Vision);
        entity.Mission = Clean(model.Mission);
        entity.HorizonStart = model.HorizonStart;
        entity.HorizonEnd = model.HorizonEnd;
    }

    public static void Apply(this Perspective entity, PerspectiveWriteModel model)
    {
        entity.StrategyId = model.StrategyId;
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsHidden = model.IsHidden;
    }

    public static void Apply(this StrategicTheme entity, StrategicThemeWriteModel model)
    {
        entity.StrategyId = model.StrategyId;
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.Code = Clean(model.Code);
        entity.DisplayOrder = model.DisplayOrder;
    }

    public static void Apply(this Objective entity, ObjectiveWriteModel model)
    {
        entity.PerspectiveId = model.PerspectiveId;
        entity.StrategicThemeId = model.StrategicThemeId;
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.TargetState = Clean(model.TargetState);
        entity.OwnerUserId = model.OwnerUserId;
        entity.OrgUnitId = model.OrgUnitId;
        entity.DisplayOrder = model.DisplayOrder;
    }

    public static void Apply(this Kpi entity, KpiWriteModel model)
    {
        entity.ObjectiveId = model.ObjectiveId;
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.Unit = Clean(model.Unit);
        entity.Direction = model.Direction;
        entity.Frequency = model.Frequency;
        entity.Baseline = model.Baseline;
        entity.Target = model.Target;
        entity.GreenThresholdPercent = model.GreenThresholdPercent ?? 95m;
        entity.AmberThresholdPercent = model.AmberThresholdPercent ?? 80m;
        entity.Formula = Clean(model.Formula);
        entity.DataSource = Clean(model.DataSource);
        entity.OwnerUserId = model.OwnerUserId;
    }

    public static void Apply(this ObjectiveLink entity, ObjectiveLinkWriteModel model)
    {
        entity.SourceObjectiveId = model.SourceObjectiveId;
        entity.TargetObjectiveId = model.TargetObjectiveId;
        entity.Note = Clean(model.Note);
    }

    public static void Apply(this Initiative entity, InitiativeWriteModel model)
    {
        entity.ObjectiveId = model.ObjectiveId;
        entity.Name = model.Name.Trim();
        entity.NameAr = Clean(model.NameAr);
        entity.Description = Clean(model.Description);
        entity.DescriptionAr = Clean(model.DescriptionAr);
        entity.Sponsor = Clean(model.Sponsor);
        entity.Budget = model.Budget;
        entity.BudgetCurrency = Clean(model.BudgetCurrency)?.ToUpperInvariant();
        entity.ExpectedBenefit = Clean(model.ExpectedBenefit);
        entity.StartDate = model.StartDate;
        entity.EndDate = model.EndDate;
        entity.Status = Clean(model.Status) ?? entity.Status;
    }

    public static StrategyResponse ToResponse(this Strategy e) => new(
        e.Id, e.Name, e.NameAr, e.Description, e.DescriptionAr, e.Vision, e.Mission,
        e.HorizonStart, e.HorizonEnd, e.Health, e.HealthScore, e.HealthComputedAt);

    public static PerspectiveResponse ToResponse(this Perspective e) => new(
        e.Id, e.StrategyId, e.Name, e.NameAr, e.Description, e.DescriptionAr,
        e.DisplayOrder, e.IsHidden);

    public static StrategicThemeResponse ToResponse(this StrategicTheme e) => new(
        e.Id, e.StrategyId, e.Name, e.NameAr, e.Description, e.DescriptionAr,
        e.Code, e.DisplayOrder);

    public static ObjectiveResponse ToResponse(this Objective e) => new(
        e.Id, e.PerspectiveId, e.Name, e.NameAr, e.Description, e.DescriptionAr,
        e.TargetState, e.OwnerUserId, e.OrgUnitId, e.DisplayOrder,
        e.Health, e.HealthScore, e.HealthComputedAt, e.StrategicThemeId);

    public static KpiResponse ToResponse(this Kpi e)
    {
        var evaluation = KpiEvaluation.Evaluate(e);
        return new KpiResponse(
            e.Id, e.ObjectiveId, e.Name, e.NameAr, e.Description, e.DescriptionAr,
            e.Unit, e.Direction, e.Frequency, e.Baseline, e.Target, e.Actual, e.ActualAsOf,
            e.GreenThresholdPercent, e.AmberThresholdPercent, e.Formula, e.DataSource,
            e.OwnerUserId, evaluation.Status, evaluation.AchievementPercent);
    }

    public static KpiMeasurementResponse ToResponse(this KpiMeasurement e) => new(
        e.Id, e.KpiId, e.PeriodStart, e.PeriodEnd, e.Value, e.Note);

    public static ObjectiveLinkResponse ToResponse(this ObjectiveLink e) => new(
        e.Id, e.SourceObjectiveId, e.TargetObjectiveId, e.Note);

    public static InitiativeResponse ToResponse(this Initiative e) => new(
        e.Id, e.ObjectiveId, e.Name, e.NameAr, e.Description, e.DescriptionAr,
        e.Sponsor, e.Budget, e.BudgetCurrency, e.ExpectedBenefit, e.StartDate, e.EndDate,
        e.Status, e.Health, e.HealthScore, e.HealthComputedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
