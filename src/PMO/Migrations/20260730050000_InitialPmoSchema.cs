using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Pmo.Migrations;

/// <summary>
/// The PMO module's own schema. Portfolios/Programs/Projects/Tasks/Dependencies/RAID were
/// created by the Platform D3 migration before B6; that migration no longer creates them and
/// PMO owns them from here on, with its own <c>__EFMigrationsHistory_Pmo</c> ledger — the
/// same move B5 made for SMO.
///
/// Table names are Pmo-prefixed (not reused from D3) so a dev database that already ran the
/// old migration does not collide; see docs/sessions/B6.md → Follow-ups for the legacy tables.
///
/// RLS is applied here rather than being left for later: D3 shipped these tables with no
/// policy at all (B5's follow-up #3 flagged this gap explicitly for B6 to close).
/// </summary>
[DbContext(typeof(PmoDbContext))]
[Migration("20260730050000_InitialPmoSchema")]
public partial class InitialPmoSchema : Migration
{
    private static readonly string[] PmoTables =
    {
        "PmoPortfolios",
        "PmoPrograms",
        "PmoProjects",
        "PmoTasks",
        "PmoDependencies",
        "PmoRaidItems",
        "PmoStatusReports",
        "PmoCalendars"
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PmoPortfolios",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoPortfolios", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PmoPrograms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                PortfolioId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoPrograms", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoPrograms_PmoPortfolios_PortfolioId",
                    column: x => x.PortfolioId,
                    principalTable: "PmoPortfolios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoProjects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoProjects", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoProjects_PmoPrograms_ProgramId",
                    column: x => x.ProgramId,
                    principalTable: "PmoPrograms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                WbsCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                DurationDays = table.Column<int>(type: "integer", nullable: false),
                IsMilestone = table.Column<bool>(type: "boolean", nullable: false),
                PercentComplete = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ScheduleStart = table.Column<DateOnly>(type: "date", nullable: true),
                ScheduleFinish = table.Column<DateOnly>(type: "date", nullable: true),
                TotalFloatDays = table.Column<int>(type: "integer", nullable: true),
                IsCritical = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoTasks_PmoProjects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "PmoProjects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoDependencies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                PredecessorTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                SuccessorTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                LagDays = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoDependencies", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoDependencies_PmoProjects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "PmoProjects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoRaidItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                PortfolioId = table.Column<Guid>(type: "uuid", nullable: true),
                ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoRaidItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoRaidItems_PmoPortfolios_PortfolioId",
                    column: x => x.PortfolioId,
                    principalTable: "PmoPortfolios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PmoRaidItems_PmoPrograms_ProgramId",
                    column: x => x.ProgramId,
                    principalTable: "PmoPrograms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PmoRaidItems_PmoProjects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "PmoProjects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoStatusReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                OverallStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Accomplishments = table.Column<string>(type: "text", nullable: true),
                NextPeriodPlan = table.Column<string>(type: "text", nullable: true),
                KeyRaidNotes = table.Column<string>(type: "text", nullable: true),
                MilestoneStatus = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoStatusReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_PmoStatusReports_PmoProjects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "PmoProjects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PmoStatusReports_PmoPrograms_ProgramId",
                    column: x => x.ProgramId,
                    principalTable: "PmoPrograms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PmoCalendars",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                WeekendDay1 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                WeekendDay2 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PmoCalendars", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_PmoPortfolios_TenantId", table: "PmoPortfolios", column: "TenantId");
        migrationBuilder.CreateIndex(name: "IX_PmoPrograms_TenantId_PortfolioId", table: "PmoPrograms", columns: new[] { "TenantId", "PortfolioId" });
        migrationBuilder.CreateIndex(name: "IX_PmoPrograms_PortfolioId", table: "PmoPrograms", column: "PortfolioId");
        migrationBuilder.CreateIndex(name: "IX_PmoProjects_TenantId_ProgramId", table: "PmoProjects", columns: new[] { "TenantId", "ProgramId" });
        migrationBuilder.CreateIndex(name: "IX_PmoProjects_ProgramId", table: "PmoProjects", column: "ProgramId");
        migrationBuilder.CreateIndex(name: "IX_PmoTasks_TenantId_ProjectId", table: "PmoTasks", columns: new[] { "TenantId", "ProjectId" });
        migrationBuilder.CreateIndex(name: "IX_PmoTasks_ProjectId", table: "PmoTasks", column: "ProjectId");
        migrationBuilder.CreateIndex(name: "IX_PmoDependencies_TenantId_ProjectId", table: "PmoDependencies", columns: new[] { "TenantId", "ProjectId" });
        migrationBuilder.CreateIndex(name: "IX_PmoDependencies_ProjectId", table: "PmoDependencies", column: "ProjectId");
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_TenantId_ProjectId", table: "PmoRaidItems", columns: new[] { "TenantId", "ProjectId" });
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_TenantId_ProgramId", table: "PmoRaidItems", columns: new[] { "TenantId", "ProgramId" });
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_TenantId_PortfolioId", table: "PmoRaidItems", columns: new[] { "TenantId", "PortfolioId" });
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_PortfolioId", table: "PmoRaidItems", column: "PortfolioId");
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_ProgramId", table: "PmoRaidItems", column: "ProgramId");
        migrationBuilder.CreateIndex(name: "IX_PmoRaidItems_ProjectId", table: "PmoRaidItems", column: "ProjectId");
        migrationBuilder.CreateIndex(name: "IX_PmoStatusReports_TenantId_ProjectId_ReportDate", table: "PmoStatusReports", columns: new[] { "TenantId", "ProjectId", "ReportDate" });
        migrationBuilder.CreateIndex(name: "IX_PmoStatusReports_TenantId_ProgramId_ReportDate", table: "PmoStatusReports", columns: new[] { "TenantId", "ProgramId", "ReportDate" });
        migrationBuilder.CreateIndex(name: "IX_PmoStatusReports_ProgramId", table: "PmoStatusReports", column: "ProgramId");
        migrationBuilder.CreateIndex(name: "IX_PmoCalendars_TenantId", table: "PmoCalendars", column: "TenantId", unique: true);

        // Self-contained rather than relying on the Platform or SMO RLS migrations having run
        // first: the schema and the function are created idempotently with the same definition.
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS app;");
        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;");

        foreach (var table in PmoTables)
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
        foreach (var table in PmoTables)
        {
            migrationBuilder.Sql(string.Format(
                @"DROP POLICY IF EXISTS ""{0}_tenant_isolation"" ON ""{0}"";
ALTER TABLE ""{0}"" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE ""{0}"" DISABLE ROW LEVEL SECURITY;",
                table));
        }

        migrationBuilder.DropTable(name: "PmoCalendars");
        migrationBuilder.DropTable(name: "PmoStatusReports");
        migrationBuilder.DropTable(name: "PmoRaidItems");
        migrationBuilder.DropTable(name: "PmoDependencies");
        migrationBuilder.DropTable(name: "PmoTasks");
        migrationBuilder.DropTable(name: "PmoProjects");
        migrationBuilder.DropTable(name: "PmoPrograms");
        migrationBuilder.DropTable(name: "PmoPortfolios");
    }
}
