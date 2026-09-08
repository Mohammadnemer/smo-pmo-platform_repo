using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmoPmo.Platform;
using SmoPmo.Shared.Auditing;
using SmoPmo.Shared.Messaging;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Shared.Persistence;

/// <summary>
/// Stamps tenant + audit metadata on every tracked <see cref="BaseEntity"/> and reports
/// what changed once the save succeeded (non-negotiable: an EF Core SaveChanges
/// interceptor records every change).
///
/// A module cannot write the Platform-owned audit table itself, so the changes are
/// published as an <see cref="EntitiesChangedEvent"/> through the in-process mediator
/// and Platform persists them. Reporting happens *after* the save so a rolled-back
/// transaction leaves no audit trail behind.
/// </summary>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IMessageBus _messageBus;
    private readonly string _module;
    private readonly List<AuditRecord> _pending = new();

    public AuditingSaveChangesInterceptor(ITenantContext tenantContext, IMessageBus messageBus, string module)
    {
        _tenantContext = tenantContext;
        _messageBus = messageBus;
        _module = module;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Fire-and-wait on the sync path: the async path is what the API actually uses.
        PublishAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        _pending.Clear();
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = "system";
            }
            else
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = "system";
            }

            _pending.Add(new AuditRecord(
                _module,
                entry.Metadata.ClrType.Name,
                entry.Entity.Id,
                entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    _ => "Delete"
                },
                Describe(entry)));
        }
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var records = _pending.ToArray();
        _pending.Clear();

        await _messageBus.PublishAsync(new EntitiesChangedEvent(_tenantContext.TenantId, records), cancellationToken);
    }

    private static string? Describe(EntityEntry<BaseEntity> entry)
    {
        var name = entry.Metadata.FindProperty("Name");
        return name is null ? null : entry.Property(name.Name).CurrentValue?.ToString();
    }
}
