using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Pmo;

/// <summary>
/// PMO's side of the cross-module read seam (see <c>CrossModuleQueries</c> in /Shared) —
/// the mirror of SMO's <c>InitiativeSummaryQueryHandler</c>. Roll-up (X1) sends these
/// through <see cref="IMessageBus"/> to validate a program/project id and resolve its
/// display name without referencing <see cref="PmoDbContext"/> directly.
/// </summary>
public sealed class ProgramSummaryQueryHandler : ICommandHandler<GetProgramSummaryQuery, ProgramSummary?>
{
    private readonly PmoDbContext _dbContext;

    public ProgramSummaryQueryHandler(PmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProgramSummary?> HandleAsync(GetProgramSummaryQuery command, CancellationToken ct = default)
    {
        return _dbContext.Programs
            .AsNoTracking()
            .Where(e => e.Id == command.ProgramId)
            .Select(e => new ProgramSummary(e.Id, e.Name))
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class ProjectSummaryQueryHandler : ICommandHandler<GetProjectSummaryQuery, ProjectSummary?>
{
    private readonly PmoDbContext _dbContext;

    public ProjectSummaryQueryHandler(PmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectSummary?> HandleAsync(GetProjectSummaryQuery command, CancellationToken ct = default)
    {
        return _dbContext.Projects
            .AsNoTracking()
            .Where(e => e.Id == command.ProjectId)
            .Select(e => new ProjectSummary(e.Id, e.Name))
            .FirstOrDefaultAsync(ct);
    }
}
