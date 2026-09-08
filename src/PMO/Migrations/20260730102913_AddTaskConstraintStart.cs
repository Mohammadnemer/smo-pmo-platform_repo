using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Pmo.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskConstraintStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ConstraintStart",
                table: "PmoTasks",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConstraintStart",
                table: "PmoTasks");
        }
    }
}
