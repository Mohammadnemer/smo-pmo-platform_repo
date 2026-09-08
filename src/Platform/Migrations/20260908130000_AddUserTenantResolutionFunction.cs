using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoPmo.Platform.Migrations;

/// <summary>
/// Closes the tenant-resolution gap TenantResolutionMiddleware's Users-table fallback
/// exposed: "Users" has ENABLE ROW LEVEL SECURITY but no FORCE (AddRowLevelSecurity), which
/// only skips the policy for the table's *owner*. The API connects as smopmo_app, a
/// deliberately non-owner role (deploy-api.yml — it cannot alter schema), so RLS applies to it
/// same as any tenant-scoped table. A plain "SELECT ... WHERE ExternalId = @oid" run before the
/// tenant is known therefore gets filtered by "TenantId" = app.current_tenant_id() against
/// whatever app.tenant_id still holds from the last request — the row exists but is invisible.
///
/// SECURITY DEFINER makes this one narrow, single-column read run with the function owner's
/// privileges (smopmo_migrator, who owns "Users" and so bypasses RLS on it) regardless of the
/// caller's session state — the standard pattern for a login-time "which tenant is this user"
/// bootstrap query under RLS. It returns only a uuid, never a Users row, so smopmo_app gains no
/// broader read access than it has today.
/// </summary>
[DbContext(typeof(PlatformDbContext))]
[Migration("20260908130000_AddUserTenantResolutionFunction")]
public partial class AddUserTenantResolutionFunction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS app;");

        migrationBuilder.Sql(@"CREATE OR REPLACE FUNCTION app.resolve_tenant_id(p_external_id text)
RETURNS uuid
LANGUAGE sql
SECURITY DEFINER
STABLE
SET search_path = public, app
AS $$
    SELECT ""TenantId"" FROM ""Users"" WHERE ""ExternalId"" = p_external_id LIMIT 1
$$;");

        // Runtime callers only ever need EXECUTE, never table-level access — least privilege,
        // and consistent with every other smopmo_app grant this codebase adds by hand. Skipped
        // silently when the role does not exist (a local postgres-superuser run).
        migrationBuilder.Sql(@"DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smopmo_app') THEN
        GRANT EXECUTE ON FUNCTION app.resolve_tenant_id(text) TO smopmo_app;
    END IF;
END $$;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS app.resolve_tenant_id(text);");
    }
}
