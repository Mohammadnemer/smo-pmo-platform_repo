using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SmoPmo.Smo.Migrations;

[DbContext(typeof(SmoDbContext))]
partial class SmoDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SmoPmo.Smo.Strategy", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("DescriptionAr").HasColumnType("text");
            b.Property<string>("Health").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<DateTimeOffset?>("HealthComputedAt").HasColumnType("timestamp with time zone");
            b.Property<decimal?>("HealthScore").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<DateOnly?>("HorizonEnd").HasColumnType("date");
            b.Property<DateOnly?>("HorizonStart").HasColumnType("date");
            b.Property<string>("Mission").HasColumnType("text");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NameAr").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.Property<string>("Vision").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId");
            b.ToTable("SmoStrategies");
        });

        modelBuilder.Entity("SmoPmo.Smo.Perspective", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("DescriptionAr").HasColumnType("text");
            b.Property<int>("DisplayOrder").HasColumnType("integer");
            b.Property<bool>("IsHidden").HasColumnType("boolean");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NameAr").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("StrategyId").HasColumnType("uuid");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "StrategyId");
            b.HasIndex("StrategyId");
            b.ToTable("SmoPerspectives");
            b.HasOne("SmoPmo.Smo.Strategy", "Strategy")
                .WithMany("Perspectives")
                .HasForeignKey("StrategyId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("SmoPmo.Smo.Objective", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("DescriptionAr").HasColumnType("text");
            b.Property<int>("DisplayOrder").HasColumnType("integer");
            b.Property<string>("Health").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<DateTimeOffset?>("HealthComputedAt").HasColumnType("timestamp with time zone");
            b.Property<decimal?>("HealthScore").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NameAr").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid?>("OrgUnitId").HasColumnType("uuid");
            b.Property<Guid?>("OwnerUserId").HasColumnType("uuid");
            b.Property<Guid>("PerspectiveId").HasColumnType("uuid");
            b.Property<string>("TargetState").HasColumnType("text");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "PerspectiveId");
            b.HasIndex("PerspectiveId");
            b.ToTable("SmoObjectives");
            b.HasOne("SmoPmo.Smo.Perspective", "Perspective")
                .WithMany("Objectives")
                .HasForeignKey("PerspectiveId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("SmoPmo.Smo.Kpi", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<decimal?>("Actual").HasPrecision(18, 4).HasColumnType("numeric(18,4)");
            b.Property<DateTimeOffset?>("ActualAsOf").HasColumnType("timestamp with time zone");
            b.Property<decimal>("AmberThresholdPercent").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<decimal?>("Baseline").HasPrecision(18, 4).HasColumnType("numeric(18,4)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("DataSource").HasColumnType("text");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("DescriptionAr").HasColumnType("text");
            b.Property<string>("Direction").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<string>("Formula").HasColumnType("text");
            b.Property<string>("Frequency").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<decimal>("GreenThresholdPercent").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NameAr").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("ObjectiveId").HasColumnType("uuid");
            b.Property<Guid?>("OwnerUserId").HasColumnType("uuid");
            b.Property<decimal?>("Target").HasPrecision(18, 4).HasColumnType("numeric(18,4)");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<string>("Unit").HasMaxLength(50).HasColumnType("character varying(50)");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "ObjectiveId");
            b.HasIndex("ObjectiveId");
            b.ToTable("SmoKpis");
            b.HasOne("SmoPmo.Smo.Objective", "Objective")
                .WithMany("Kpis")
                .HasForeignKey("ObjectiveId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("SmoPmo.Smo.KpiMeasurement", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<Guid>("KpiId").HasColumnType("uuid");
            b.Property<string>("Note").HasColumnType("text");
            b.Property<DateOnly>("PeriodEnd").HasColumnType("date");
            b.Property<DateOnly>("PeriodStart").HasColumnType("date");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.Property<decimal>("Value").HasPrecision(18, 4).HasColumnType("numeric(18,4)");
            b.HasKey("Id");
            b.HasIndex("TenantId", "KpiId", "PeriodStart");
            b.HasIndex("KpiId");
            b.ToTable("SmoKpiMeasurements");
            b.HasOne("SmoPmo.Smo.Kpi", "Kpi")
                .WithMany("Measurements")
                .HasForeignKey("KpiId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("SmoPmo.Smo.Initiative", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<decimal?>("Budget").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
            b.Property<string>("BudgetCurrency").HasMaxLength(3).HasColumnType("character varying(3)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("Description").HasColumnType("text");
            b.Property<string>("DescriptionAr").HasColumnType("text");
            b.Property<DateOnly?>("EndDate").HasColumnType("date");
            b.Property<string>("ExpectedBenefit").HasColumnType("text");
            b.Property<string>("Health").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<DateTimeOffset?>("HealthComputedAt").HasColumnType("timestamp with time zone");
            b.Property<decimal?>("HealthScore").HasPrecision(9, 4).HasColumnType("numeric(9,4)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NameAr").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("ObjectiveId").HasColumnType("uuid");
            b.Property<string>("Sponsor").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<DateOnly?>("StartDate").HasColumnType("date");
            b.Property<string>("Status").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "ObjectiveId");
            b.HasIndex("ObjectiveId");
            b.ToTable("SmoInitiatives");
            b.HasOne("SmoPmo.Smo.Objective", "Objective")
                .WithMany("Initiatives")
                .HasForeignKey("ObjectiveId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("SmoPmo.Smo.ObjectiveLink", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("CreatedBy").IsRequired().HasColumnType("text");
            b.Property<string>("Note").HasColumnType("text");
            b.Property<Guid>("SourceObjectiveId").HasColumnType("uuid");
            b.Property<Guid>("TargetObjectiveId").HasColumnType("uuid");
            b.Property<Guid>("TenantId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("UpdatedBy").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("TenantId", "SourceObjectiveId");
            b.HasIndex("TenantId", "TargetObjectiveId");
            b.HasIndex("SourceObjectiveId");
            b.HasIndex("TargetObjectiveId");
            b.ToTable("SmoObjectiveLinks");
            b.HasOne("SmoPmo.Smo.Objective", "SourceObjective")
                .WithMany()
                .HasForeignKey("SourceObjectiveId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("SmoPmo.Smo.Objective", "TargetObjective")
                .WithMany()
                .HasForeignKey("TargetObjectiveId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });
    }
}
