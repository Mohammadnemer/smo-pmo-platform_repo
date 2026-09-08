using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

/// <summary>
/// B5: the audit log now records which module reported a change and which row it was, so a
/// trail can be read per record once modules audit through the mediator.
/// </summary>
[DbContext(typeof(PlatformDbContext))]
[Migration("20260730040000_AddAuditModuleAndEntityId")]
public partial class AddAuditModuleAndEntityId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Module",
            table: "AuditEntries",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Platform");

        migrationBuilder.AddColumn<Guid>(
            name: "EntityId",
            table: "AuditEntries",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_TenantId_EntityId",
            table: "AuditEntries",
            columns: new[] { "TenantId", "EntityId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AuditEntries_TenantId_EntityId", table: "AuditEntries");
        migrationBuilder.DropColumn(name: "EntityId", table: "AuditEntries");
        migrationBuilder.DropColumn(name: "Module", table: "AuditEntries");
    }
}
