using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Smo.Migrations;

/// <summary>
/// Cause-effect edges between objectives (PRD §6.1.6) — what the F5 strategy map draws as
/// arrows. Hand-written, RLS included from creation, same reasoning as
/// <see cref="InitialSmoSchema"/>: a table this session exposes over HTTP does not get to
/// ship without a tenant-isolation policy, even briefly.
/// </summary>
[DbContext(typeof(SmoDbContext))]
[Migration("20260908120000_AddObjectiveLinks")]
public partial class AddObjectiveLinks : Migration
{
    private const string Table = "SmoObjectiveLinks";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                SourceObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                Note = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoObjectiveLinks", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoObjectiveLinks_SmoObjectives_SourceObjectiveId",
                    column: x => x.SourceObjectiveId,
                    principalTable: "SmoObjectives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SmoObjectiveLinks_SmoObjectives_TargetObjectiveId",
                    column: x => x.TargetObjectiveId,
                    principalTable: "SmoObjectives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_SmoObjectiveLinks_TenantId_SourceObjectiveId", table: Table, columns: new[] { "TenantId", "SourceObjectiveId" });
        migrationBuilder.CreateIndex(name: "IX_SmoObjectiveLinks_TenantId_TargetObjectiveId", table: Table, columns: new[] { "TenantId", "TargetObjectiveId" });

        // Same tenant-isolation function InitialSmoSchema created — CREATE OR REPLACE with
        // an identical definition, so this migration doesn't assume it runs after that one
        // against a specific history, only that the function exists by the time this runs.
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS app;");
        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;");

        migrationBuilder.Sql($@"ALTER TABLE ""{Table}"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""{Table}"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""{Table}_tenant_isolation"" ON ""{Table}""
FOR ALL
USING (""TenantId"" = app.current_tenant_id())
WITH CHECK (""TenantId"" = app.current_tenant_id());");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"DROP POLICY IF EXISTS ""{Table}_tenant_isolation"" ON ""{Table}"";
ALTER TABLE ""{Table}"" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE ""{Table}"" DISABLE ROW LEVEL SECURITY;");

        migrationBuilder.DropTable(name: Table);
    }
}
