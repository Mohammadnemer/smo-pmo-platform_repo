using SmoPmo.Shared.Messaging;

namespace SmoPmo.Shared.Integration;

/// <summary>
/// Generic RAG representation used only for cross-module transport on the roll-up graph.
/// Every module still owns and persists its own local enum (SMO's <c>RagStatus</c>, PMO's
/// <c>PmoRagStatus</c>) — this is the shape that crosses the mediator, translated back at
/// each boundary, the same reasoning as <c>InitiativeSummary</c> et al. in CrossModuleQueries.
/// </summary>
public enum HealthStatus
{
    NotSet = 0,
    Red = 1,
    Amber = 2,
    Green = 3
}

public sealed record HealthResult(HealthStatus Status, decimal? Score);

/// <summary>
/// Pure math shared by every level of the roll-up graph above Project
/// (architecture-mvp.md §5): a node's health is the (weighted) average of its children's
/// already-stored scores, banded into RAG. Kept here rather than duplicated in PMO
/// (Program averages its Projects) and SMO (Objective averages its Initiatives, Strategy
/// averages its Objectives) — the aggregation itself has no module-specific meaning. Project
/// health is the one exception: it comes from PMO's own task/RAID/schedule data, not from
/// averaging children, so it has its own formula (<c>ProjectHealthCalculator</c> in PMO).
/// </summary>
public static class HealthAggregation
{
    public static HealthResult Average(IEnumerable<decimal?> scores)
    {
        var values = scores.Where(s => s is not null).Select(s => s!.Value).ToList();
        return values.Count == 0 ? new HealthResult(HealthStatus.NotSet, null) : Band(values.Average());
    }

    public static HealthResult WeightedAverage(IEnumerable<(decimal? Score, decimal Weight)> items)
    {
        var usable = items.Where(i => i.Score is not null && i.Weight > 0).ToList();
        if (usable.Count == 0)
        {
            return new HealthResult(HealthStatus.NotSet, null);
        }

        var totalWeight = usable.Sum(i => i.Weight);
        var weighted = usable.Sum(i => i.Score!.Value * i.Weight) / totalWeight;
        return Band(weighted);
    }

    public static HealthResult Band(decimal score) => new(
        score switch
        {
            >= 80m => HealthStatus.Green,
            >= 50m => HealthStatus.Amber,
            _ => HealthStatus.Red
        },
        score);
}

/// <summary>
/// Raised by PMO after any write that can move a project's stored health
/// (architecture-mvp.md §5: "task % complete, RAID severity, schedule variance") — task and
/// dependency create/update/delete (each already recomputes the project's CPM schedule, so
/// one event covers all three: a % complete edit, a duration edit that shifts the critical
/// path, or a dependency edit that does the same) and a project-scoped RAID item
/// create/update/delete. The roll-up worker (X2) debounces these before recomputing.
/// </summary>
public sealed record PmoDeliveryHealthInputsChanged(Guid TenantId, Guid ProjectId) : IDomainEvent;

/// <summary>
/// Sent by the roll-up worker once its debounce window elapses for a given project. Handled
/// in the Roll-up module: the one place that holds both the PMO project/program link and the
/// SMO initiative it feeds (architecture-mvp.md §9) — see
/// <c>RecomputeDeliveryHealthCommandHandler</c>. Lives here (not in RollupContracts, which is
/// Roll-up's own CRUD surface) because the Worker — which only ever references /Shared — has
/// to be able to send it.
/// </summary>
public sealed record RecomputeDeliveryHealthCommand(Guid ProjectId) : ICommand;

// ─────────────────── PMO side of the seam (recompute + read stored values) ───────────────────

/// <summary>Recomputes and stores one project's health, then cascades to its parent program
/// (an average of that program's projects — see <see cref="HealthAggregation"/>). Returns
/// <see langword="null"/> if the project no longer exists (deleted between the signal firing
/// and the debounce window elapsing).</summary>
public sealed record RecomputeProjectHealthCommand(Guid ProjectId) : ICommand<ProjectHealthRecomputeResult?>;

public sealed record ProjectHealthRecomputeResult(
    Guid ProjectId,
    Guid ProgramId,
    HealthResult ProjectHealth,
    HealthResult ProgramHealth);

/// <summary>Read-only: the project's *already-stored* health, for a delivery link the current
/// recompute didn't touch. Never triggers a recompute — that would violate "stored, not
/// recomputed on read".</summary>
public sealed record GetProjectHealthQuery(Guid ProjectId) : ICommand<HealthResult?>;

public sealed record GetProgramHealthQuery(Guid ProgramId) : ICommand<HealthResult?>;

// ─────────────────────────── SMO side of the seam (set + cascade) ───────────────────────────

/// <summary>
/// Sets one initiative's stored health to an already-computed value — the weighted average
/// across that initiative's delivery links lives in Roll-up, not here, since only Roll-up
/// holds the link table's <c>ContributionWeight</c>/<c>IsPrimary</c> — then cascades the
/// recompute up through the initiative's objective and that objective's strategy. All three
/// tables belong to SMO, so the cascade is a local aggregation rather than another round trip
/// through the mediator. Returns <see langword="false"/> if the initiative no longer exists.
/// </summary>
public sealed record RecomputeInitiativeHealthCommand(Guid InitiativeId, HealthResult Health) : ICommand<bool>;
