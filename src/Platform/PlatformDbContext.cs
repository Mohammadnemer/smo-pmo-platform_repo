using Microsoft.EntityFrameworkCore;

namespace SmoPmo.Platform;

public sealed class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    // The SMO sets moved to SmoDbContext in B5, and the PMO sets to PmoDbContext in B6:
    // each module owns its own tables.
    public DbSet<LookupItem> LookupItems => Set<LookupItem>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
        });

        modelBuilder.Entity<OrgUnit>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.OrgUnits)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OrgUnit)
                .WithMany(e => e.Users)
                .HasForeignKey(e => e.OrgUnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
        });

        modelBuilder.Entity<User>()
            .HasMany(e => e.Roles)
            .WithMany(e => e.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserRole",
                left => left.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
                right => right.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade));

        modelBuilder.Entity<OrgUnit>()
            .HasMany(e => e.Roles)
            .WithMany(e => e.OrgUnits)
            .UsingEntity<Dictionary<string, object>>(
                "OrgUnitRole",
                left => left.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
                right => right.HasOne<OrgUnit>().WithMany().HasForeignKey("OrgUnitId").OnDelete(DeleteBehavior.Cascade));

        modelBuilder.Entity<LookupItem>(entity =>
        {
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Category, e.Code }).IsUnique();
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.Property(e => e.EntityName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Module).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TenantId).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.EntityId });
        });
    }

    public async Task SeedTenantAsync(Guid tenantId, string tenantName, CancellationToken cancellationToken = default)
    {
        if (await Tenants.AnyAsync(e => e.Id == tenantId, cancellationToken))
        {
            return;
        }

        var tenant = new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "system",
            Name = tenantName
        };

        await Tenants.AddAsync(tenant, cancellationToken);

        var defaults = new[]
        {
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "BSC Perspective", Code = "Customer", Value = "Customer" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "BSC Perspective", Code = "Finance", Value = "Finance" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "BSC Perspective", Code = "Internal", Value = "Internal" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "BSC Perspective", Code = "Learning", Value = "Learning" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "RAID Category", Code = "Risk", Value = "Risk" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "RAID Category", Code = "Issue", Value = "Issue" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Status", Code = "Draft", Value = "Draft" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Status", Code = "Active", Value = "Active" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Mon", Value = "Monday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Tue", Value = "Tuesday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Wed", Value = "Wednesday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Thu", Value = "Thursday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Fri", Value = "Friday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Sat", Value = "Saturday" },
            new LookupItem { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "system", Category = "Workday", Code = "Sun", Value = "Sunday" }
        };

        await LookupItems.AddRangeAsync(defaults, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }
}
