using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

/// <summary>
/// AddSmoPmoSchema (D3) never created the LookupItems or AuditEntries tables even though
/// PlatformDbContext has always had DbSets for both, and it named the task/RAID tables
/// "Tasks"/"RAID" instead of the "TaskItems"/"RaidItems" EF's DbSet-name convention expects.
/// Filling that in here rather than editing the already-applied D3 migration.
/// </summary>
[DbContext(typeof(PlatformDbContext))]
[Migration("20260730035000_AddLookupAndAuditEntriesTables")]
public partial class AddLookupAndAuditEntriesTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "Tasks", newName: "TaskItems");
        migrationBuilder.RenameTable(name: "RAID", newName: "RaidItems");

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
        migrationBuilder.RenameTable(name: "RaidItems", newName: "RAID");
        migrationBuilder.RenameTable(name: "TaskItems", newName: "Tasks");
    }
}
