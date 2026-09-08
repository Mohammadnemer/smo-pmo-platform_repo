namespace SmoPmo.Smo;

/// <summary>
/// Request validation for the SMO write models. Hand-rolled to keep the MVP dependency
/// list short; every failure comes back as an RFC 9457 validation problem so the SPA can
/// bind errors to fields (and to the AR/EN label of that field).
/// </summary>
internal static class SmoValidation
{
    private const int MaxName = 200;
    private const int MaxCode = 20;

    public static Dictionary<string, string[]>? Validate(StrategyWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);

        if (model.HorizonStart is { } start && model.HorizonEnd is { } end && end < start)
        {
            errors.Add(nameof(model.HorizonEnd), "The horizon must end on or after it starts.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(PerspectiveWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);
        errors.Required(nameof(model.StrategyId), model.StrategyId);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(StrategicThemeWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);
        errors.MaxLength(nameof(model.Code), model.Code, MaxCode);
        errors.Required(nameof(model.StrategyId), model.StrategyId);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(ObjectiveWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);
        errors.Required(nameof(model.PerspectiveId), model.PerspectiveId);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(KpiWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);
        errors.Required(nameof(model.ObjectiveId), model.ObjectiveId);

        var green = model.GreenThresholdPercent ?? 95m;
        var amber = model.AmberThresholdPercent ?? 80m;

        if (green is < 0m or > 1000m)
        {
            errors.Add(nameof(model.GreenThresholdPercent), "The green threshold must be between 0 and 1000 percent.");
        }

        if (amber is < 0m or > 1000m)
        {
            errors.Add(nameof(model.AmberThresholdPercent), "The amber threshold must be between 0 and 1000 percent.");
        }

        if (amber > green)
        {
            errors.Add(nameof(model.AmberThresholdPercent), "The amber threshold cannot be higher than the green threshold.");
        }

        if (!Enum.IsDefined(model.Direction))
        {
            errors.Add(nameof(model.Direction), "Direction must be HigherIsBetter or LowerIsBetter.");
        }

        if (!Enum.IsDefined(model.Frequency))
        {
            errors.Add(nameof(model.Frequency), "Frequency must be Monthly, Quarterly, SemiAnnual or Annual.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(KpiMeasurementWriteModel model)
    {
        var errors = new ErrorBag();

        if (model.PeriodEnd < model.PeriodStart)
        {
            errors.Add(nameof(model.PeriodEnd), "The period must end on or after it starts.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(ObjectiveLinkWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Required(nameof(model.SourceObjectiveId), model.SourceObjectiveId);
        errors.Required(nameof(model.TargetObjectiveId), model.TargetObjectiveId);

        if (model.SourceObjectiveId != Guid.Empty && model.SourceObjectiveId == model.TargetObjectiveId)
        {
            errors.Add(nameof(model.TargetObjectiveId), "An objective cannot link to itself.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(InitiativeWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.MaxLength(nameof(model.NameAr), model.NameAr, MaxName);
        errors.Required(nameof(model.ObjectiveId), model.ObjectiveId);

        if (model.Budget is < 0m)
        {
            errors.Add(nameof(model.Budget), "Budget cannot be negative.");
        }

        if (!string.IsNullOrWhiteSpace(model.BudgetCurrency) && model.BudgetCurrency.Trim().Length != 3)
        {
            errors.Add(nameof(model.BudgetCurrency), "Currency must be a three-letter ISO code.");
        }

        if (model.StartDate is { } start && model.EndDate is { } end && end < start)
        {
            errors.Add(nameof(model.EndDate), "The initiative must end on or after it starts.");
        }

        return errors.Result;
    }

    private sealed class ErrorBag
    {
        private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

        public void Add(string field, string message)
        {
            if (!_errors.TryGetValue(field, out var messages))
            {
                messages = new List<string>();
                _errors[field] = messages;
            }

            messages.Add(message);
        }

        public void Name(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(nameof(Name), "Name is required.");
            }
            else
            {
                MaxLength(nameof(Name), value, MaxName);
            }
        }

        public void MaxLength(string field, string? value, int max)
        {
            if (value is not null && value.Trim().Length > max)
            {
                Add(field, $"Must be {max} characters or fewer.");
            }
        }

        public void Required(string field, Guid value)
        {
            if (value == Guid.Empty)
            {
                Add(field, "A value is required.");
            }
        }

        public Dictionary<string, string[]>? Result => _errors.Count == 0
            ? null
            : _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
