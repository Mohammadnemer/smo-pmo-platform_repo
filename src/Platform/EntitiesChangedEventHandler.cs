using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Platform;

/// <summary>
/// Platform owns the audit log, so Platform is the only writer to it. Other modules report
/// what changed via <see cref="EntitiesChangedEvent"/> on the in-process mediator and this
/// handler persists the rows — no module reaches across into another module's table.
/// </summary>
public sealed class EntitiesChangedEventHandler : IDomainEventHandler<EntitiesChangedEvent>
{
    private readonly PlatformDbContext _dbContext;

    public EntitiesChangedEventHandler(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(EntitiesChangedEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent.Records.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var record in domainEvent.Records)
        {
            _dbContext.AuditEntries.Add(new AuditEntry
            {
                TenantId = domainEvent.TenantId,
                Module = record.Module,
                EntityName = record.EntityName,
                EntityId = record.EntityId,
                Action = record.Action,
                Details = record.Details,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
