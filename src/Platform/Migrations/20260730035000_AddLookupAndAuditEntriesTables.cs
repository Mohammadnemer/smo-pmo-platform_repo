using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

/// <summary>
/// AddSmoPmoSchema (D3) never created the LookupItems or AuditEntries tables even though
/// PlatformDbContext has always had DbSets for both, and it named the task/RAID tables
/// "Tasks"/"RAID" instead of the "TaskItems"/"RaidItems" EF's DbSet-name convention expects.
/// Filling that in here rather than editing the already-applied D3 migration.
///
/// D3 itself became a no-op once B6 moved Tasks/RAID ownership to the PMO module, so on any
/// database that never ran the original (pre-B6) D3, "Tasks"/"RAID" never exist — the renames
/// below are guarded with an existence check so this migration is a clean no-op for those
/// (e.g. a fresh database) while still fixing up old pre-B6 databases that do have them.
/// </summary>
[DbContext(typeof(PlatformDbContext))]
[Migration("20260730035000_AddLookupAndAuditEntriesTables")]
public partial class AddLookupAndAuditEntriesTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Tasks') THEN
                    ALTER TABLE ""Tasks"" RENAME TO ""TaskItems"";
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'RAID') THEN
                    ALTER TABLE ""RAID"" RENAME TO ""RaidItems"";
                END IF;
            END $$;
        ");

        migrationBuilder.CreateTable(
            name: "LookupItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LookupItems", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LookupItems_TenantId_Category_Code",
            table: "LookupItems",
            columns: new[] { "TenantId", "Category", "Code" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                UpdatedBy = table.Column<string>(type: "text", nullable: true),
                EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Details = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditEntries");
        migrationBuilder.DropTable(name: "LookupItems");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'TaskItems') THEN
                    ALTER TABLE ""TaskItems"" RENAME TO ""Tasks"";
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'RaidItems') THEN
                    ALTER TABLE ""RaidItems"" RENAME TO ""RAID"";
                END IF;
            END $$;
        ");
    }
}
