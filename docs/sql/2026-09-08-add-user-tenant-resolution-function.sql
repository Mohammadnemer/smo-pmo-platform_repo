-- =============================================================================
--  app.resolve_tenant_id — closes the tenant-resolution gap under real Entra
--  External ID logins.
--
--  Applies migration 20260908130000_AddUserTenantResolutionFunction by hand,
--  the way .github/workflows/deploy-api.yml says Platform migrations reach
--  Azure: run as smopmo_migrator, from your laptop, because the API's
--  smopmo_app role has no permission to change schema.
--
--    psql "<smopmo_migrator connection string>" -v ON_ERROR_STOP=1 \
--         -f docs/sql/2026-09-08-add-user-tenant-resolution-function.sql
--
--  Why this exists: TenantResolutionMiddleware falls back to looking a signed-in
--  user's oid up in "Users" when the token carries no tenant_id claim (true of
--  every real Entra External ID token today — that custom-claim mapping was
--  never configured). "Users" has ENABLE ROW LEVEL SECURITY but no FORCE
--  (Platform's AddRowLevelSecurity migration), which only skips the policy for
--  the table's *owner*. The API connects as smopmo_app, a deliberately
--  non-owner role, so a plain SELECT there gets filtered by
--  "TenantId" = app.current_tenant_id() before the tenant is even known — the
--  row exists but stays invisible. SECURITY DEFINER makes this one narrow,
--  single-column read run with the function owner's privileges instead,
--  bypassing that chicken-and-egg RLS check. It returns only a uuid, never a
--  Users row, so smopmo_app gains no broader read access than it has today.
--
--  Safe to run more than once: CREATE OR REPLACE, and the grant is guarded.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

CREATE SCHEMA IF NOT EXISTS app;

CREATE OR REPLACE FUNCTION app.resolve_tenant_id(p_external_id text)
RETURNS uuid
LANGUAGE sql
SECURITY DEFINER
STABLE
SET search_path = public, app
AS $$
    SELECT "TenantId" FROM "Users" WHERE "ExternalId" = p_external_id LIMIT 1
$$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smopmo_app') THEN
        GRANT EXECUTE ON FUNCTION app.resolve_tenant_id(text) TO smopmo_app;
    END IF;
END $$;

-- Record the migration so a later `dotnet ef database update` does not try to
-- apply it a second time on top of the schema this script just created.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260908130000_AddUserTenantResolutionFunction', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Prove it: your own Users row from the earlier manual INSERT should resolve.
SELECT app.resolve_tenant_id('5d2970e7-dbc5-4582-8dc2-034601a95748') AS resolved_tenant_id;

COMMIT;
