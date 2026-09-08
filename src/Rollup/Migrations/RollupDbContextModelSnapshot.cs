using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SmoPmo.Rollup.Migrations;

[DbContext(typeof(RollupDbContext))]
partial class RollupDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SmoPmo.Rollup.InitiativeDeliveryLink", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<decimal>("ContributionWeight").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<Guid>("InitiativeId").HasColumnType("uuid");
            b.Property<bool>("IsPrimary").HasColumnType("boolean");
            b.Property<Guid?>("ProgramId").HasColumnType("uuid");
            b.Property<Guid?>("ProjectId").HasColumnType("uuid");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "InitiativeId");
            b.HasIndex("TenantId", "ProgramId");
            b.HasIndex("TenantId", "ProjectId");
            b.ToTable("RollupInitiativeLinks");
        });
    }
}
