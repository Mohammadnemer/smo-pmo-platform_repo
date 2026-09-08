using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Rollup;

/// <summary>
/// The one place the PMO-side (project/program) and SMO-side (initiative) halves of the
/// roll-up graph meet (architecture-mvp.md §5, §9): Roll-up owns the
/// <see cref="InitiativeDeliveryLink"/> table connecting them, so it is the only module that
/// can weight and combine a delivery vehicle's health across modules without either module
/// project-referencing the other. Sent by the roll-up worker (X2) after its debounce window
/// elapses for a given project.
/// </summary>
public sealed class RecomputeDeliveryHealthCommandHandler : ICommandHandler<RecomputeDeliveryHealthCommand>
{
    private readonly RollupDbContext _dbContext;
    private readonly IMessageBus _bus;

    public RecomputeDeliveryHealthCommandHandler(RollupDbContext dbContext, IMessageBus bus)
    {
        _dbContext = dbContext;
        _bus = bus;
    }

    public async Task HandleAsync(RecomputeDeliveryHealthCommand command, CancellationToken ct = default)
    {
        var projectResult = await _bus.SendAsync(new RecomputeProjectHealthCommand(command.ProjectId), ct);
        if (projectResult is null)
        {
            // Deleted between the signal firing and the debounce window elapsing — nothing
            // left to roll up for it. Its old contribution to any initiative fades out
            // naturally the next time that initiative recomputes from a different trigger.
            return;
        }

        var affectedInitiativeIds = await _dbContext.Links.AsNoTracking()
            .Where(l => l.ProjectId == projectResult.ProjectId || l.ProgramId == projectResult.ProgramId)
            .Select(l => l.InitiativeId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var initiativeId in affectedInitiativeIds)
        {
            var health = await ComputeInitiativeHealthAsync(initiativeId, projectResult, ct);
            await _bus.SendAsync(new RecomputeInitiativeHealthCommand(initiativeId, health), ct);
        }
    }

    /// <summary>
    /// An initiative's health is the <see cref="InitiativeDeliveryLink.ContributionWeight"/>-
    /// weighted average across *all* of its delivery links (PRD §5.3: "a primary flag marks
    /// the main delivery vehicle; contribution_weight lets roll-up math attribute execution
    /// health proportionally"), not just the one link that triggered this recompute.
    /// </summary>
    private async Task<HealthResult> ComputeInitiativeHealthAsync(
        Guid initiativeId, ProjectHealthRecomputeResult freshProjectResult, CancellationToken ct)
    {
        var links = await _dbContext.Links.AsNoTracking()
            .Where(l => l.InitiativeId == initiativeId)
            .ToListAsync(ct);

        var weighted = new List<(decimal? Score, decimal Weight)>();
        foreach (var link in links)
        {
            var health = await ResolveDeliveryHealthAsync(link, freshProjectResult, ct);
            weighted.Add((health?.Score, link.ContributionWeight));
        }

        return HealthAggregation.WeightedAverage(weighted);
    }

    /// <summary>
    /// The link that triggered this recompute already has a fresh score in hand (no need to
    /// ask PMO again); every other link on the same initiative reads PMO's *already-stored*
    /// value — never recomputed here, matching "stored, not recomputed on read" everywhere
    /// else on the graph.
    /// </summary>
    private async Task<HealthResult?> ResolveDeliveryHealthAsync(
        InitiativeDeliveryLink link, ProjectHealthRecomputeResult freshProjectResult, CancellationToken ct)
    {
        if (link.ProjectId is { } projectId)
        {
            return projectId == freshProjectResult.ProjectId
                ? freshProjectResult.ProjectHealth
                : await _bus.SendAsync(new GetProjectHealthQuery(projectId), ct);
        }

        if (link.ProgramId is { } programId)
        {
            return programId == freshProjectResult.ProgramId
                ? freshProjectResult.ProgramHealth
                : await _bus.SendAsync(new GetProgramHealthQuery(programId), ct);
        }

        return null;
    }
}
