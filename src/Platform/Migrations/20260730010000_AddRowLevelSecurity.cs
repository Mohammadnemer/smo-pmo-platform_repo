using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20260730010000_AddRowLevelSecurity")]
public partial class AddRowLevelSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"CREATE SCHEMA IF NOT EXISTS app;");

        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;");

        // UserRole/OrgUnitRole are pure join tables with no TenantId column of their own;
        // they're isolated transitively through RLS on the tenant-owned tables they FK into.
        foreach (var tableName in new[] { "Tenants", "OrgUnits", "Users", "Roles" })
        {
            var policySql = string.Format(
                "ALTER TABLE \"{0}\" ENABLE ROW LEVEL SECURITY;\nCREATE POLICY \"{0}_tenant_isolation\" ON \"{0}\"\nFOR ALL\nUSING (\"TenantId\" = app.current_tenant_id());",
                tableName);
            migrationBuilder.Sql(policySql);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // UserRole/OrgUnitRole are pure join tables with no TenantId column of their own;
        // they're isolated transitively through RLS on the tenant-owned tables they FK into.
        foreach (var tableName in new[] { "Tenants", "OrgUnits", "Users", "Roles" })
        {
            var dropPolicySql = string.Format(
                "DROP POLICY IF EXISTS \"{0}_tenant_isolation\" ON \"{0}\";",
                tableName);
            migrationBuilder.Sql(dropPolicySql);
        }

        migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS app.current_tenant_id();");
    }
}
