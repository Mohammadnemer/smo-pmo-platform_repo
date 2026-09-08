using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Smo.Migrations;

/// <summary>
/// The SMO module's own schema. These tables were created by the Platform D3 migration
/// before B5; that migration no longer creates them and SMO owns them from here on, with
/// its own <c>__EFMigrationsHistory_Smo</c> ledger.
///
/// RLS is applied here rather than being left for later: D3 shipped the SMO tables with no
/// policy at all, which made "the database refuses cross-tenant rows" untrue for exactly
/// the tables this session exposes over HTTP.
/// </summary>
[DbContext(typeof(SmoDbContext))]
[Migration("20260730030000_InitialSmoSchema")]
public partial class InitialSmoSchema : Migration
{
    private static readonly string[] SmoTables =
    {
        "SmoStrategies",
        "SmoPerspectives",
        "SmoObjectives",
        "SmoKpis",
        "SmoKpiMeasurements",
        "SmoInitiatives"
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SmoStrategies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                Vision = table.Column<string>(type: "text", nullable: true),
                Mission = table.Column<string>(type: "text", nullable: true),
                HorizonStart = table.Column<DateOnly>(type: "date", nullable: true),
                HorizonEnd = table.Column<DateOnly>(type: "date", nullable: true),
                Health = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                HealthScore = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                HealthComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoStrategies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SmoPerspectives",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                StrategyId = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoPerspectives", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoPerspectives_SmoStrategies_StrategyId",
                    column: x => x.StrategyId,
                    principalTable: "SmoStrategies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SmoObjectives",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                PerspectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetState = table.Column<string>(type: "text", nullable: true),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                Health = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                HealthScore = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                HealthComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoObjectives", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoObjectives_SmoPerspectives_PerspectiveId",
                    column: x => x.PerspectiveId,
                    principalTable: "SmoPerspectives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SmoKpis",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                ObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Baseline = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Target = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Actual = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                ActualAsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                GreenThresholdPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                AmberThresholdPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                Formula = table.Column<string>(type: "text", nullable: true),
                DataSource = table.Column<string>(type: "text", nullable: true),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoKpis", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoKpis_SmoObjectives_ObjectiveId",
                    column: x => x.ObjectiveId,
                    principalTable: "SmoObjectives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SmoKpiMeasurements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                KpiId = table.Column<Guid>(type: "uuid", nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Note = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoKpiMeasurements", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoKpiMeasurements_SmoKpis_KpiId",
                    column: x => x.KpiId,
                    principalTable: "SmoKpis",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SmoInitiatives",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                ObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                Sponsor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                BudgetCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                ExpectedBenefit = table.Column<string>(type: "text", nullable: true),
                StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Health = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                HealthScore = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                HealthComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoInitiatives", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoInitiatives_SmoObjectives_ObjectiveId",
                    column: x => x.ObjectiveId,
                    principalTable: "SmoObjectives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_SmoStrategies_TenantId", table: "SmoStrategies", column: "TenantId");
        migrationBuilder.CreateIndex(name: "IX_SmoPerspectives_TenantId_StrategyId", table: "SmoPerspectives", columns: new[] { "TenantId", "StrategyId" });
        migrationBuilder.CreateIndex(name: "IX_SmoObjectives_TenantId_PerspectiveId", table: "SmoObjectives", columns: new[] { "TenantId", "PerspectiveId" });
        migrationBuilder.CreateIndex(name: "IX_SmoKpis_TenantId_ObjectiveId", table: "SmoKpis", columns: new[] { "TenantId", "ObjectiveId" });
        migrationBuilder.CreateIndex(name: "IX_SmoKpiMeasurements_TenantId_KpiId_PeriodStart", table: "SmoKpiMeasurements", columns: new[] { "TenantId", "KpiId", "PeriodStart" });
        migrationBuilder.CreateIndex(name: "IX_SmoInitiatives_TenantId_ObjectiveId", table: "SmoInitiatives", columns: new[] { "TenantId", "ObjectiveId" });

        // Self-contained rather than relying on the Platform RLS migration having run first:
        // the schema and the function are created idempotently with the same definition.
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS app;");
        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;");

        foreach (var table in SmoTables)
        {
            // FORCE is what makes this real: without it the policy is skipped for the table
            // owner, which is the very role the API connects as.
            migrationBuilder.Sql(string.Format(
                @"ALTER TABLE ""{0}"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""{0}"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""{0}_tenant_isolation"" ON ""{0}""
FOR ALL
USING (""TenantId"" = app.current_tenant_id())
WITH CHECK (""TenantId"" = app.current_tenant_id());",
                table));
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in SmoTables)
        {
            migrationBuilder.Sql(string.Format(
                @"DROP POLICY IF EXISTS ""{0}_tenant_isolation"" ON ""{0}"";
ALTER TABLE ""{0}"" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE ""{0}"" DISABLE ROW LEVEL SECURITY;",
                table));
        }

        migrationBuilder.DropTable(name: "SmoKpiMeasurements");
        migrationBuilder.DropTable(name: "SmoKpis");
        migrationBuilder.DropTable(name: "SmoInitiatives");
        migrationBuilder.DropTable(name: "SmoObjectives");
        migrationBuilder.DropTable(name: "SmoPerspectives");
        migrationBuilder.DropTable(name: "SmoStrategies");
    }
}
