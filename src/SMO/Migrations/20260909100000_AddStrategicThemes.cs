using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Smo.Migrations;

/// <summary>
/// Strategic themes (PRD §6.1.1, §6.1.2) — the cross-perspective storylines the alignment
/// grid uses as its columns, plus the optional membership column on objectives.
/// Hand-written, RLS included from creation, same reasoning as <see cref="AddObjectiveLinks"/>:
/// a table this session exposes over HTTP does not get to ship without a tenant-isolation
/// policy, even briefly.
///
/// <c>StrategicThemeId</c> is nullable and un-backfilled by this migration: an existing
/// objective simply has no theme until someone assigns one, so this is additive and safe to
/// run against a tenant that already has a populated scorecard tree.
/// </summary>
[DbContext(typeof(SmoDbContext))]
[Migration("20260909100000_AddStrategicThemes")]
public partial class AddStrategicThemes : Migration
{
    private const string Table = "SmoStrategicThemes";

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
                StrategyId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                DescriptionAr = table.Column<string>(type: "text", nullable: true),
                Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmoStrategicThemes", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmoStrategicThemes_SmoStrategies_StrategyId",
                    column: x => x.StrategyId,
                    principalTable: "SmoStrategies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_SmoStrategicThemes_TenantId_StrategyId", table: Table, columns: new[] { "TenantId", "StrategyId" });
        migrationBuilder.CreateIndex(name: "IX_SmoStrategicThemes_StrategyId", table: Table, column: "StrategyId");

        migrationBuilder.AddColumn<Guid>(
            name: "StrategicThemeId",
            table: "SmoObjectives",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SmoObjectives_TenantId_StrategicThemeId",
            table: "SmoObjectives",
            columns: new[] { "TenantId", "StrategicThemeId" });

        migrationBuilder.CreateIndex(
            name: "IX_SmoObjectives_StrategicThemeId",
            table: "SmoObjectives",
            column: "StrategicThemeId");

        // SetNull: retiring a theme un-themes its objectives, it never deletes them.
        migrationBuilder.AddForeignKey(
            name: "FK_SmoObjectives_SmoStrategicThemes_StrategicThemeId",
            table: "SmoObjectives",
            column: "StrategicThemeId",
            principalTable: Table,
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

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
        migrationBuilder.DropForeignKey(
            name: "FK_SmoObjectives_SmoStrategicThemes_StrategicThemeId",
            table: "SmoObjectives");

        migrationBuilder.DropIndex(name: "IX_SmoObjectives_StrategicThemeId", table: "SmoObjectives");
        migrationBuilder.DropIndex(name: "IX_SmoObjectives_TenantId_StrategicThemeId", table: "SmoObjectives");
        migrationBuilder.DropColumn(name: "StrategicThemeId", table: "SmoObjectives");

        migrationBuilder.Sql($@"DROP POLICY IF EXISTS ""{Table}_tenant_isolation"" ON ""{Table}"";
ALTER TABLE ""{Table}"" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE ""{Table}"" DISABLE ROW LEVEL SECURITY;");

        migrationBuilder.DropTable(name: Table);
    }
}
