using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260730020000_AddSmoPmoSchema")]
public partial class AddSmoPmoSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Strategies / Perspectives / Objectives / KPIs / Initiatives used to be created
        // here. B5 gave those tables to the SMO module, which creates them in its own
        // migration under src/SMO/Migrations — see docs/sessions/B5.md.
        //
        // Portfolios / Programs / Projects / Tasks / Dependencies / RAID used to be created
        // here too. B6 gave those tables to the PMO module, which creates them (as
        // Pmo-prefixed tables, to avoid colliding with a pre-B6 dev database that already
        // has these) in its own migration under src/PMO/Migrations — see docs/sessions/B6.md.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
