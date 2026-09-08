using Microsoft.EntityFrameworkCore;
using SmoPmo.Shared.Multitenancy;

namespace SmoPmo.Pmo;

/// <summary>
/// The PMO module's own EF Core context. It maps only PMO-owned tables and keeps its own
/// migration history (<see cref="MigrationsHistoryTable"/>) so PMO schema changes ship
/// independently of Platform's and SMO's — the same move B5 made for SMO.
///
/// Isolation is belt *and* braces: RLS in the database (PMO's initial migration) plus a
/// tenant query filter here. The filter is convenience; RLS is the guarantee.
/// </summary>
public sealed class PmoDbContext : DbContext
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Pmo";

    private readonly ITenantContext _tenantContext;

    public PmoDbContext(DbContextOptions<PmoDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Read by the query filters below. EF parameterises it per query.</summary>
    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<RaidItem> RaidItems => Set<RaidItem>();
    public DbSet<StatusReport> StatusReports => Set<StatusReport>();
    public DbSet<PmoCalendar> Calendars => Set<PmoCalendar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Portfolio>(entity =>
        {
            entity.ToTable("PmoPortfolios");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => e.TenantId);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Program>(entity =>
        {
            entity.ToTable("PmoPrograms");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Health).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.HealthScore).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.PortfolioId });
            entity.HasOne(e => e.Portfolio)
                .WithMany(e => e.Programs)
                .HasForeignKey(e => e.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("PmoProjects");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Health).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.HealthScore).HasPrecision(9, 4);
            entity.HasIndex(e => new { e.TenantId, e.ProgramId });
            entity.HasOne(e => e.Program)
                .WithMany(e => e.Projects)
                .HasForeignKey(e => e.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("PmoTasks");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WbsCode).HasMaxLength(50);
            entity.Property(e => e.PercentComplete).HasPrecision(5, 2);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProjectId });
            entity.HasOne(e => e.Project)
                .WithMany(e => e.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Dependency>(entity =>
        {
            entity.ToTable("PmoDependencies");
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProjectId });
            entity.HasOne(e => e.Project)
                .WithMany(e => e.Dependencies)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<RaidItem>(entity =>
        {
            entity.ToTable("PmoRaidItems");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProjectId });
            entity.HasIndex(e => new { e.TenantId, e.ProgramId });
            entity.HasIndex(e => new { e.TenantId, e.PortfolioId });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<StatusReport>(entity =>
        {
            entity.ToTable("PmoStatusReports");
            entity.Property(e => e.OverallStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.ReportDate });
            entity.HasIndex(e => new { e.TenantId, e.ProgramId, e.ReportDate });
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PmoCalendar>(entity =>
        {
            entity.ToTable("PmoCalendars");
            entity.Property(e => e.WeekendDay1).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.WeekendDay2).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        });
    }
}
