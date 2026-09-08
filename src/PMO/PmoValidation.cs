namespace SmoPmo.Pmo;

/// <summary>
/// Request validation for the PMO write models. Hand-rolled to keep the MVP dependency list
/// short (same call SMO made in B5); every failure comes back as an RFC 9457 validation
/// problem so the SPA can bind errors to fields.
/// </summary>
internal static class PmoValidation
{
    private const int MaxName = 200;
    private static readonly string[] DependencyTypes = { "FS", "SS", "FF", "SF" };
    private static readonly string[] RaidCategories = { "Risk", "Action", "Issue", "Decision" };
    private static readonly string[] RaidSeverities = { "Low", "Medium", "High", "Critical" };

    public static Dictionary<string, string[]>? Validate(PortfolioWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(ProgramWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.Required(nameof(model.PortfolioId), model.PortfolioId);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(ProjectWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.Required(nameof(model.ProgramId), model.ProgramId);
        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(TaskWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Name(model.Name);
        errors.Required(nameof(model.ProjectId), model.ProjectId);

        if (model.IsMilestone && model.DurationDays != 0)
        {
            errors.Add(nameof(model.DurationDays), "Milestones must have zero duration.");
        }
        else if (!model.IsMilestone && model.DurationDays < 1)
        {
            errors.Add(nameof(model.DurationDays), "Duration must be at least 1 working day for a non-milestone task.");
        }

        if (model.PercentComplete is < 0m or > 100m)
        {
            errors.Add(nameof(model.PercentComplete), "Percent complete must be between 0 and 100.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(DependencyWriteModel model)
    {
        var errors = new ErrorBag();
        errors.Required(nameof(model.ProjectId), model.ProjectId);
        errors.Required(nameof(model.PredecessorTaskId), model.PredecessorTaskId);
        errors.Required(nameof(model.SuccessorTaskId), model.SuccessorTaskId);

        if (model.PredecessorTaskId != Guid.Empty && model.PredecessorTaskId == model.SuccessorTaskId)
        {
            errors.Add(nameof(model.SuccessorTaskId), "A task cannot depend on itself.");
        }

        if (!DependencyTypes.Contains(model.Type?.Trim().ToUpperInvariant()))
        {
            errors.Add(nameof(model.Type), "Type must be one of FS, SS, FF, SF.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(RaidItemWriteModel model)
    {
        var errors = new ErrorBag();
        errors.MaxLength(nameof(model.Title), model.Title, MaxName);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            errors.Add(nameof(model.Title), "Title is required.");
        }

        if (!RaidCategories.Contains(model.Category?.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(nameof(model.Category), "Category must be one of Risk, Action, Issue, Decision.");
        }

        if (!RaidSeverities.Contains(model.Severity?.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(nameof(model.Severity), "Severity must be one of Low, Medium, High, Critical.");
        }

        var parents = new[] { model.PortfolioId, model.ProgramId, model.ProjectId }.Count(id => id is not null);
        if (parents != 1)
        {
            errors.Add(nameof(model.ProjectId), "Exactly one of portfolioId, programId or projectId must be set.");
        }

        return errors.Result;
    }

    public static Dictionary<string, string[]>? Validate(StatusReportWriteModel model)
    {
        var errors = new ErrorBag();

        var parents = new[] { model.ProjectId, model.ProgramId }.Count(id => id is not null);
        if (parents != 1)
        {
            errors.Add(nameof(model.ProjectId), "Exactly one of projectId or programId must be set.");
        }

        if (model.PeriodEnd < model.PeriodStart)
        {
            errors.Add(nameof(model.PeriodEnd), "The period must end on or after it starts.");
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
