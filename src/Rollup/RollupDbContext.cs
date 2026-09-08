using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Rollup;

/// <summary>
/// The Roll-up module's own EF Core context. It maps only Roll-up-owned tables and keeps
/// its own migration history (<see cref="MigrationsHistoryTable"/>) so its schema ships
/// independently of Platform's, SMO's and PMO's — the same move B5/B6 made for their
/// modules.
///
/// Isolation is belt *and* braces: RLS in the database (this module's initial migration)
/// plus a tenant query filter here. The filter is convenience; RLS is the guarantee.
/// </summary>
public sealed class RollupDbContext : DbContext
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Rollup";

    private readonly ITenantContext _tenantContext;

    public RollupDbContext(DbContextOptions<RollupDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Read by the query filter below. EF parameterises it per query.</summary>
    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<InitiativeDeliveryLink> Links => Set<InitiativeDeliveryLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InitiativeDeliveryLink>(entity =>
        {
            entity.ToTable("RollupInitiativeLinks");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ContributionWeight).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.InitiativeId });
            entity.HasIndex(e => new { e.TenantId, e.ProgramId });
            entity.HasIndex(e => new { e.TenantId, e.ProjectId });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });
    }
}
