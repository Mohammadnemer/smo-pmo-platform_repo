using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Smo;

/// <summary>
/// The SMO module's own EF Core context. It maps only SMO-owned tables and keeps its own
/// migration history (<see cref="MigrationsHistoryTable"/>) so SMO schema changes ship
/// independently of Platform's.
///
/// Isolation is belt *and* braces: RLS in the database (SMO's initial migration) plus a
/// tenant query filter here. The filter is convenience; RLS is the guarantee.
/// </summary>
public sealed class SmoDbContext : DbContext
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Smo";

    private readonly ITenantContext _tenantContext;

    public SmoDbContext(DbContextOptions<SmoDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Read by the query filters below. EF parameterises it per query.</summary>
    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<Perspective> Perspectives => Set<Perspective>();
    public DbSet<Objective> Objectives => Set<Objective>();
    public DbSet<Kpi> Kpis => Set<Kpi>();
    public DbSet<KpiMeasurement> KpiMeasurements => Set<KpiMeasurement>();
    public DbSet<Initiative> Initiatives => Set<Initiative>();
    public DbSet<ObjectiveLink> ObjectiveLinks => Set<ObjectiveLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Strategy>(entity =>
        {
            entity.ToTable("SmoStrategies");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameAr).HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Health).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.HealthScore).HasPrecision(9, 4);
            entity.HasIndex(e => e.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Perspective>(entity =>
        {
            entity.ToTable("SmoPerspectives");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameAr).HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.StrategyId });
            entity.HasOne(e => e.Strategy)
                .WithMany(e => e.Perspectives)
                .HasForeignKey(e => e.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Objective>(entity =>
        {
            entity.ToTable("SmoObjectives");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameAr).HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Health).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.HealthScore).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.PerspectiveId });
            entity.HasOne(e => e.Perspective)
                .WithMany(e => e.Objectives)
                .HasForeignKey(e => e.PerspectiveId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Kpi>(entity =>
        {
            entity.ToTable("SmoKpis");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameAr).HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.Direction).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Baseline).HasPrecision(18, 4);
            entity.Property(e => e.Target).HasPrecision(18, 4);
            entity.Property(e => e.Actual).HasPrecision(18, 4);
            entity.Property(e => e.GreenThresholdPercent).HasPrecision(9, 4);
            entity.Property(e => e.AmberThresholdPercent).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.ObjectiveId });
            entity.HasOne(e => e.Objective)
                .WithMany(e => e.Kpis)
                .HasForeignKey(e => e.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<KpiMeasurement>(entity =>
        {
            entity.ToTable("SmoKpiMeasurements");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Value).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.KpiId, e.PeriodStart });
            entity.HasOne(e => e.Kpi)
                .WithMany(e => e.Measurements)
                .HasForeignKey(e => e.KpiId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Initiative>(entity =>
        {
            entity.ToTable("SmoInitiatives");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameAr).HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Sponsor).HasMaxLength(200);
            entity.Property(e => e.BudgetCurrency).HasMaxLength(3);
            entity.Property(e => e.Budget).HasPrecision(18, 2);
            entity.Property(e => e.Health).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.HealthScore).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.ObjectiveId });
            entity.HasOne(e => e.Objective)
                .WithMany(e => e.Initiatives)
                .HasForeignKey(e => e.ObjectiveId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ObjectiveLink>(entity =>
        {
            entity.ToTable("SmoObjectiveLinks");
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.SourceObjectiveId });
            entity.HasIndex(e => new { e.TenantId, e.TargetObjectiveId });
            // Restrict, not Cascade: both FKs point at Objective, and Postgres/SQL Server
            // both reject two cascade paths into the same table. An objective with links
            // must have them deleted first — acceptable for MVP; nothing here does bulk
            // objective deletion yet.
            entity.HasOne(e => e.SourceObjective)
                .WithMany()
                .HasForeignKey(e => e.SourceObjectiveId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TargetObjective)
                .WithMany()
                .HasForeignKey(e => e.TargetObjectiveId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });
    }
}
