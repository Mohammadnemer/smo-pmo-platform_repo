using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Rollup.Migrations;

/// <summary>
/// The Roll-up module's own schema (X1): the single M:N link table between SMO initiatives
/// and PMO programs/projects. RLS is applied here from the start, the same as SMO's and
/// PMO's initial migrations — "the database refuses cross-tenant rows" has to be true for
/// every table this session exposes over HTTP, not just the older ones.
/// </summary>
[DbContext(typeof(RollupDbContext))]
[Migration("20260731000000_InitialRollupSchema")]
public partial class InitialRollupSchema : Migration
{
    private static readonly string[] RollupTables =
    {
        "RollupInitiativeLinks"
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RollupInitiativeLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                InitiativeId = table.Column<Guid>(type: "uuid", nullable: false),
                ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                ContributionWeight = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RollupInitiativeLinks", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_RollupInitiativeLinks_TenantId_InitiativeId", table: "RollupInitiativeLinks", columns: new[] { "TenantId", "InitiativeId" });
        migrationBuilder.CreateIndex(name: "IX_RollupInitiativeLinks_TenantId_ProgramId", table: "RollupInitiativeLinks", columns: new[] { "TenantId", "ProgramId" });
        migrationBuilder.CreateIndex(name: "IX_RollupInitiativeLinks_TenantId_ProjectId", table: "RollupInitiativeLinks", columns: new[] { "TenantId", "ProjectId" });

        // Self-contained rather than relying on another module's RLS migration having run
        // first: the schema and the function are created idempotently with the same
        // definition SMO's and PMO's migrations use.
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS app;");
        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;");

        foreach (var table in RollupTables)
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
        foreach (var table in RollupTables)
        {
            migrationBuilder.Sql(string.Format(
                @"DROP POLICY IF EXISTS ""{0}_tenant_isolation"" ON ""{0}"";
ALTER TABLE ""{0}"" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE ""{0}"" DISABLE ROW LEVEL SECURITY;",
                table));
        }

        migrationBuilder.DropTable(name: "RollupInitiativeLinks");
    }
}
