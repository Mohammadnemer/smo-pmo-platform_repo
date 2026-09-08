namespace SmoPmo.Smo;

/// <summary>
/// Turns a KPI's own target/actual/direction/thresholds into an achievement percentage
/// and a RAG band.
///
/// This is deliberately *not* part of the roll-up engine: it touches a single row and
/// walks no graph, so computing it on read costs nothing and breaks no rule. Objective,
/// initiative and strategy health are different — those are graph roll-ups, are STORED
/// by the X2 worker, and are only ever read here.
/// </summary>
public static class KpiEvaluation
{
    public static KpiEvaluationResult Evaluate(
        decimal? target,
        decimal? actual,
        KpiDirection direction,
        decimal greenThresholdPercent,
        decimal amberThresholdPercent)
    {
        if (target is null || actual is null)
        {
            return new KpiEvaluationResult(RagStatus.NotSet, null);
        }

        var achievement = AchievementPercent(target.Value, actual.Value, direction);
        var status = achievement >= greenThresholdPercent
            ? RagStatus.Green
            : achievement >= amberThresholdPercent
                ? RagStatus.Amber
                : RagStatus.Red;

        return new KpiEvaluationResult(status, decimal.Round(achievement, 2));
    }

    public static KpiEvaluationResult Evaluate(Kpi kpi) => Evaluate(
        kpi.Target,
        kpi.Actual,
        kpi.Direction,
        kpi.GreenThresholdPercent,
        kpi.AmberThresholdPercent);

    private const decimal FullyAchieved = 100m;

    private static decimal AchievementPercent(decimal target, decimal actual, KpiDirection direction)
    {
        if (direction == KpiDirection.HigherIsBetter)
        {
            // A zero target cannot be divided into; anything non-negative has met it.
            return target == 0m
                ? (actual >= 0m ? FullyAchieved : 0m)
                : actual / target * FullyAchieved;
        }

        // Lower is better: beating the target upwards of 100% means coming in under it.
        // Zero actual is the best possible outcome, so short-circuit the division.
        if (actual == 0m)
        {
            return FullyAchieved;
        }

        return target == 0m ? 0m : target / actual * FullyAchieved;
    }
}

public sealed record KpiEvaluationResult(RagStatus Status, decimal? AchievementPercent);
