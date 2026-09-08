using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SmoPmo.Platform.Migrations;

[DbContext(typeof(PlatformDbContext))]
partial class PlatformDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SmoPmo.Platform.Tenant", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("ExternalId").HasColumnType("text");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.ToTable("Tenants");
        });

        modelBuilder.Entity("SmoPmo.Platform.OrgUnit", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid?>("ParentId").HasColumnType("uuid");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("ParentId");
            b.ToTable("OrgUnits");
            b.HasOne("SmoPmo.Platform.Tenant", "Tenant")
                .WithMany("OrgUnits")
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne("SmoPmo.Platform.OrgUnit", "Parent")
                .WithMany("Children")
                .HasForeignKey("ParentId")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity("SmoPmo.Platform.Role", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.ToTable("Roles");
        });

        modelBuilder.Entity("SmoPmo.Platform.User", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("character varying(320)");
            b.Property<string>("ExternalId").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<Guid?>("OrgUnitId").HasColumnType("uuid");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("OrgUnitId");
            b.ToTable("Users");
            b.HasOne("SmoPmo.Platform.Tenant", "Tenant")
                .WithMany("Users")
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne("SmoPmo.Platform.OrgUnit", "OrgUnit")
                .WithMany("Users")
                .HasForeignKey("OrgUnitId")
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity("SmoPmo.Platform.Role")
            .HasMany("SmoPmo.Platform.OrgUnit", "OrgUnits")
            .WithMany("Roles")
            .UsingEntity("OrgUnitRole", b =>
            {
                b.HasOne("SmoPmo.Platform.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne("SmoPmo.Platform.OrgUnit", null)
                    .WithMany()
                    .HasForeignKey("OrgUnitId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

        modelBuilder.Entity("SmoPmo.Platform.Role")
            .HasMany("SmoPmo.Platform.User", "Users")
            .WithMany("Roles")
            .UsingEntity("UserRole", b =>
            {
                b.HasOne("SmoPmo.Platform.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne("SmoPmo.Platform.User", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade);
            });
    }
}
