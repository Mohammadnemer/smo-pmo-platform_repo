using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Smo;

/// <summary>
/// SMO's side of the roll-up seam (X2): sets one initiative's stored health to the
/// already-computed value Roll-up hands it — the weighted average across that initiative's
/// delivery links lives in Roll-up, since only Roll-up's link table holds
/// <c>ContributionWeight</c>/<c>IsPrimary</c> — then cascades the recompute up through the
/// initiative's objective and that objective's strategy. All three tables belong to SMO, so
/// the cascade is a local aggregation (<see cref="HealthAggregation"/>) rather than another
/// round trip through the mediator (architecture-mvp.md §9).
/// </summary>
public sealed class RecomputeInitiativeHealthCommandHandler : ICommandHandler<RecomputeInitiativeHealthCommand, bool>
{
    private readonly SmoDbContext _dbContext;

    public RecomputeInitiativeHealthCommandHandler(SmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(RecomputeInitiativeHealthCommand command, CancellationToken ct = default)
    {
        var initiative = await _dbContext.Initiatives.FirstOrDefaultAsync(i => i.Id == command.InitiativeId, ct);
        if (initiative is null)
        {
            return false;
        }

        Apply(initiative, command.Health);
        await _dbContext.SaveChangesAsync(ct);

        var objective = await _dbContext.Objectives.FirstAsync(o => o.Id == initiative.ObjectiveId, ct);
        var initiativeScores = await _dbContext.Initiatives.AsNoTracking()
            .Where(i => i.ObjectiveId == objective.Id)
            .Select(i => i.HealthScore)
            .ToListAsync(ct);
        Apply(objective, HealthAggregation.Average(initiativeScores));
        await _dbContext.SaveChangesAsync(ct);

        var perspective = await _dbContext.Perspectives.FirstAsync(p => p.Id == objective.PerspectiveId, ct);
        var strategy = await _dbContext.Strategies.FirstAsync(s => s.Id == perspective.StrategyId, ct);
        var siblingPerspectiveIds = await _dbContext.Perspectives.AsNoTracking()
            .Where(p => p.StrategyId == strategy.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var objectiveScores = await _dbContext.Objectives.AsNoTracking()
            .Where(o => siblingPerspectiveIds.Contains(o.PerspectiveId))
            .Select(o => o.HealthScore)
            .ToListAsync(ct);
        Apply(strategy, HealthAggregation.Average(objectiveScores));
        await _dbContext.SaveChangesAsync(ct);

        return true;
    }

    private static void Apply(Initiative initiative, HealthResult health)
    {
        initiative.Health = ToRagStatus(health.Status);
        initiative.HealthScore = health.Score;
        initiative.HealthComputedAt = DateTimeOffset.UtcNow;
    }

    private static void Apply(Objective objective, HealthResult health)
    {
        objective.Health = ToRagStatus(health.Status);
        objective.HealthScore = health.Score;
        objective.HealthComputedAt = DateTimeOffset.UtcNow;
    }

    private static void Apply(Strategy strategy, HealthResult health)
    {
        strategy.Health = ToRagStatus(health.Status);
        strategy.HealthScore = health.Score;
        strategy.HealthComputedAt = DateTimeOffset.UtcNow;
    }

    private static RagStatus ToRagStatus(HealthStatus status) => status switch
    {
        HealthStatus.Green => RagStatus.Green,
        HealthStatus.Amber => RagStatus.Amber,
        HealthStatus.Red => RagStatus.Red,
        _ => RagStatus.NotSet
    };
}
