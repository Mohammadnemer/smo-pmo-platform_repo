using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Platform;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public AuditSaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not PlatformDbContext dbContext)
        {
            return base.SavingChanges(eventData, result);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
        {
            if (entry.Entity is BaseEntity entity)
            {
                entity.TenantId = _tenantContext.TenantId;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.UpdatedBy = "system";

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = DateTimeOffset.UtcNow;
                    entity.CreatedBy = "system";
                }
            }

            if (entry.Entity is not AuditEntry && entry.Entity is not BaseEntity)
            {
                continue;
            }

            if (entry.Entity is AuditEntry)
            {
                continue;
            }

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                dbContext.AuditEntries.Add(new AuditEntry
                {
                    TenantId = _tenantContext.TenantId,
                    Module = "Platform",
                    EntityId = entry.Entity is BaseEntity audited ? audited.Id : Guid.Empty,
                    EntityName = entry.Metadata.ClrType.Name,
                    Action = entry.State switch
                    {
                        EntityState.Added => "Create",
                        EntityState.Modified => "Update",
                        EntityState.Deleted => "Delete",
                        _ => "Change"
                    },
                    Details = entry.Metadata.ClrType.Name,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "system"
                });
            }
        }

        return base.SavingChanges(eventData, result);
    }
}
