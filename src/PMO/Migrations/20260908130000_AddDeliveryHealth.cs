using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Pmo.Migrations
{
    /// <summary>
    /// Stored roll-up health for Program and Project (X2: architecture-mvp.md §5 — the
    /// project → program leg of the roll-up graph, the same "stored, not recomputed on read"
    /// columns SMO's Initiative/Objective/Strategy already carry). Plain AddColumn: both
    /// tables already have RLS enabled from their initial migration, so there is nothing more
    /// to secure here. Attributes declared directly on this partial (no separate .Designer.cs)
    /// — same shortcut AddObjectiveLinks took, since <c>PendingModelChangesWarning</c> is
    /// suppressed for every hand-written migration in this codebase.
    /// </summary>
    [DbContext(typeof(PmoDbContext))]
    [Migration("20260908130000_AddDeliveryHealth")]
    public partial class AddDeliveryHealth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Health",
                table: "PmoPrograms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSet");

            migrationBuilder.AddColumn<decimal>(
                name: "HealthScore",
                table: "PmoPrograms",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HealthComputedAt",
                table: "PmoPrograms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Health",
                table: "PmoProjects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSet");

            migrationBuilder.AddColumn<decimal>(
                name: "HealthScore",
                table: "PmoProjects",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HealthComputedAt",
                table: "PmoProjects",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Health", table: "PmoPrograms");
            migrationBuilder.DropColumn(name: "HealthScore", table: "PmoPrograms");
            migrationBuilder.DropColumn(name: "HealthComputedAt", table: "PmoPrograms");

            migrationBuilder.DropColumn(name: "Health", table: "PmoProjects");
            migrationBuilder.DropColumn(name: "HealthScore", table: "PmoProjects");
            migrationBuilder.DropColumn(name: "HealthComputedAt", table: "PmoProjects");
        }
    }
}
