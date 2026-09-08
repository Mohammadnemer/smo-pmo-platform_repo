using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Pmo;

/// <summary>
/// PMO's side of the roll-up seam (X2, mirroring X1's <c>DeliverySummaryQueryHandlers</c>):
/// computes and stores a project's health from its own tasks/RAID items
/// (<see cref="ProjectHealthCalculator"/>), cascades to its parent program (an average of
/// that program's projects — <see cref="HealthAggregation"/>, same "stored, not recomputed
/// on read" rule everywhere else on the graph), and answers read-only lookups of
/// already-stored values for the delivery links a given recompute didn't just touch.
/// </summary>
public sealed class RecomputeProjectHealthCommandHandler
    : ICommandHandler<RecomputeProjectHealthCommand, ProjectHealthRecomputeResult?>
{
    private readonly PmoDbContext _dbContext;

    public RecomputeProjectHealthCommandHandler(PmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectHealthRecomputeResult?> HandleAsync(
        RecomputeProjectHealthCommand command, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct);
        if (project is null)
        {
            return null;
        }

        var tasks = await _dbContext.Tasks.AsNoTracking()
            .Where(t => t.ProjectId == project.Id)
            .ToListAsync(ct);
        var openRaidItems = await _dbContext.RaidItems.AsNoTracking()
            .Where(r => r.ProjectId == project.Id && r.Status == "Open")
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var projectHealth = ProjectHealthCalculator.Compute(tasks, openRaidItems, today);
        Apply(project, projectHealth);

        // Saved before the program aggregate is read back so the program's average sees
        // this project's brand-new score rather than its stale one (a plain LINQ query
        // always reads the store, never a same-context entity's pending change).
        await _dbContext.SaveChangesAsync(ct);

        var program = await _dbContext.Programs.FirstAsync(p => p.Id == project.ProgramId, ct);
        var siblingScores = await _dbContext.Projects.AsNoTracking()
            .Where(p => p.ProgramId == program.Id)
            .Select(p => p.HealthScore)
            .ToListAsync(ct);
        var programHealth = HealthAggregation.Average(siblingScores);
        Apply(program, programHealth);
        await _dbContext.SaveChangesAsync(ct);

        return new ProjectHealthRecomputeResult(project.Id, program.Id, projectHealth, programHealth);
    }

    private static void Apply(Project project, HealthResult health)
    {
        project.Health = PmoHealthStatusMapping.ToPmoRagStatus(health.Status);
        project.HealthScore = health.Score;
        project.HealthComputedAt = DateTimeOffset.UtcNow;
    }

    private static void Apply(Program program, HealthResult health)
    {
        program.Health = PmoHealthStatusMapping.ToPmoRagStatus(health.Status);
        program.HealthScore = health.Score;
        program.HealthComputedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class GetProjectHealthQueryHandler : ICommandHandler<GetProjectHealthQuery, HealthResult?>
{
    private readonly PmoDbContext _dbContext;

    public GetProjectHealthQueryHandler(PmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthResult?> HandleAsync(GetProjectHealthQuery command, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct);
        return project is null
            ? null
            : new HealthResult(PmoHealthStatusMapping.ToHealthStatus(project.Health), project.HealthScore);
    }
}

public sealed class GetProgramHealthQueryHandler : ICommandHandler<GetProgramHealthQuery, HealthResult?>
{
    private readonly PmoDbContext _dbContext;

    public GetProgramHealthQueryHandler(PmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthResult?> HandleAsync(GetProgramHealthQuery command, CancellationToken ct = default)
    {
        var program = await _dbContext.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.ProgramId, ct);
        return program is null
            ? null
            : new HealthResult(PmoHealthStatusMapping.ToHealthStatus(program.Health), program.HealthScore);
    }
}

/// <summary>Translates between PMO's own persisted RAG enum and the generic transport enum
/// the mediator carries (see HealthStatus in Shared/Integration) — the same "own the local
/// representation, translate at the boundary" rule as CrossModuleQueries' summary records.</summary>
internal static class PmoHealthStatusMapping
{
    public static PmoRagStatus ToPmoRagStatus(HealthStatus status) => status switch
    {
        HealthStatus.Green => PmoRagStatus.Green,
        HealthStatus.Amber => PmoRagStatus.Amber,
        HealthStatus.Red => PmoRagStatus.Red,
        _ => PmoRagStatus.NotSet
    };

    public static HealthStatus ToHealthStatus(PmoRagStatus status) => status switch
    {
        PmoRagStatus.Green => HealthStatus.Green,
        PmoRagStatus.Amber => HealthStatus.Amber,
        PmoRagStatus.Red => HealthStatus.Red,
        _ => HealthStatus.NotSet
    };
}
