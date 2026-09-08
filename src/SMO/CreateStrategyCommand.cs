using SmoPmo.Shared.Messaging;

namespace SmoPmo.Smo;

/// <summary>
/// Moved here from /Platform in B5, when SMO took ownership of the strategy tables.
/// It is the mediator path onto the same write the REST endpoint performs, and it is what
/// B4's acceptance test drives: command → handler → change → audit log.
///
/// Tenant stamping and audit reporting are the interceptor's job (see
/// <c>AuditingSaveChangesInterceptor</c>), so the handler only expresses intent.
/// </summary>
public sealed record CreateStrategyCommand(string Name, string? Description) : ICommand<Guid>;

public sealed class CreateStrategyCommandHandler : ICommandHandler<CreateStrategyCommand, Guid>
{
    private readonly SmoDbContext _dbContext;

    public CreateStrategyCommandHandler(SmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> HandleAsync(CreateStrategyCommand command, CancellationToken ct = default)
    {
        var strategy = new Strategy
        {
            Name = command.Name,
            Description = command.Description
        };

        _dbContext.Strategies.Add(strategy);
        await _dbContext.SaveChangesAsync(ct);

        return strategy.Id;
    }
}
