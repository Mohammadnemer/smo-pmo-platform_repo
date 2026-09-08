using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Smo;

/// <summary>
/// SMO's side of the cross-module read seam (see <c>CrossModuleQueries</c> in /Shared).
/// Roll-up (X1) sends this through <see cref="IMessageBus"/> to validate an initiative id
/// and to get its display name for the initiative ↔ delivery aggregate read, instead of
/// referencing <see cref="SmoDbContext"/> directly — which the module boundary forbids.
/// </summary>
public sealed class InitiativeSummaryQueryHandler : ICommandHandler<GetInitiativeSummaryQuery, InitiativeSummary?>
{
    private readonly SmoDbContext _dbContext;

    public InitiativeSummaryQueryHandler(SmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<InitiativeSummary?> HandleAsync(GetInitiativeSummaryQuery command, CancellationToken ct = default)
    {
        return _dbContext.Initiatives
            .AsNoTracking()
            .Where(e => e.Id == command.InitiativeId)
            .Select(e => new InitiativeSummary(e.Id, e.Name, e.NameAr))
            .FirstOrDefaultAsync(ct);
    }
}
