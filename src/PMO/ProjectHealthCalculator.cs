using SmoPmo.Shared.Integration;

namespace SmoPmo.Pmo;

/// <summary>
/// The MVP formula behind "project % complete + RAID severity + schedule variance"
/// (PRD §5.3/§6.3, architecture-mvp.md §5) — the one genuinely PMO-specific piece of the
/// roll-up graph. Everywhere else on the graph (Program, Initiative, Objective, Strategy)
/// just averages its children's already-computed scores via <see cref="HealthAggregation"/>.
/// A pure function deliberately: the roll-up worker (X2) needs this testable without a
/// database.
/// </summary>
internal static class ProjectHealthCalculator
{
    private const decimal MaxSchedulePenalty = 40m;
    private const decimal MaxRaidPenalty = 40m;
    private const decimal SchedulePenaltyPerOverdueCriticalTask = 15m;

    /// <summary>
    /// <paramref name="openRaidItems"/> must already be filtered to this project and
    /// <c>Status == "Open"</c> — resolved/closed risk doesn't weigh on current health.
    /// A project with no tasks and no open RAID items has nothing to score yet
    /// (<see cref="HealthStatus.NotSet"/>), same as a fresh Initiative/Objective/Strategy.
    /// </summary>
    public static HealthResult Compute(
        IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<RaidItem> openRaidItems,
        DateOnly today)
    {
        if (tasks.Count == 0 && openRaidItems.Count == 0)
        {
            return new HealthResult(HealthStatus.NotSet, null);
        }

        var progress = tasks.Count == 0 ? 100m : WeightedPercentComplete(tasks);

        // Schedule variance: a task on the critical path, not yet done, whose stored
        // finish date has already slipped past today. IsCritical/ScheduleFinish are the CPM
        // engine's own stored output (ScheduleQuery), never hand-set — see TaskItem.
        var overdueCriticalTasks = tasks.Count(t =>
            t.IsCritical && t.PercentComplete < 100m && t.ScheduleFinish is { } finish && finish < today);
        var schedulePenalty = Math.Min(overdueCriticalTasks * SchedulePenaltyPerOverdueCriticalTask, MaxSchedulePenalty);

        var raidPenalty = Math.Min(openRaidItems.Sum(SeverityPenalty), MaxRaidPenalty);

        var score = Math.Clamp(progress - schedulePenalty - raidPenalty, 0m, 100m);
        return HealthAggregation.Band(score);
    }

    private static decimal WeightedPercentComplete(IReadOnlyList<TaskItem> tasks)
    {
        var totalWeight = tasks.Sum(t => (decimal)Math.Max(t.DurationDays, 0));
        return totalWeight == 0
            ? tasks.Average(t => t.PercentComplete)
            : tasks.Sum(t => t.PercentComplete * Math.Max(t.DurationDays, 0)) / totalWeight;
    }

    private static decimal SeverityPenalty(RaidItem item) => item.Severity switch
    {
        "Critical" => 25m,
        "High" => 15m,
        "Medium" => 5m,
        "Low" => 2m,
        _ => 5m
    };
}
