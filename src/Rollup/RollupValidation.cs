namespace SmoPmo.Rollup;

/// <summary>
/// Request validation for the Roll-up write model. Hand-rolled to match SMO/PMO (see
/// SmoValidation/PmoValidation) — every failure comes back as an RFC 9457 validation
/// problem so the SPA can bind errors to fields.
/// </summary>
internal static class RollupValidation
{
    public static Dictionary<string, string[]>? Validate(InitiativeDeliveryLinkWriteModel model)
    {
        var errors = new ErrorBag();

        if (model.InitiativeId == Guid.Empty)
        {
            errors.Add(nameof(model.InitiativeId), "A value is required.");
        }

        var targets = new[] { model.ProgramId, model.ProjectId }.Count(id => id is not null);
        if (targets != 1)
        {
            errors.Add(nameof(model.ProjectId), "Exactly one of programId or projectId must be set.");
        }

        var weight = model.ContributionWeight ?? 100m;
        if (weight is <= 0m or > 100m)
        {
            errors.Add(nameof(model.ContributionWeight), "Contribution weight must be greater than 0 and no more than 100 percent.");
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

        public Dictionary<string, string[]>? Result => _errors.Count == 0
            ? null
            : _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
