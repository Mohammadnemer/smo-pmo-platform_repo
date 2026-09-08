-- =============================================================================
--  Strategic themes — schema + demo data for the SMO alignment grid.
--
--  Applies migration 20260909100000_AddStrategicThemes by hand, the way
--  .github/workflows/deploy-api.yml says SMO migrations reach Azure: run as
--  smopmo_migrator, from your laptop, because the API's smopmo_app role has no
--  permission to change schema.
--
--    psql "<smopmo_migrator connection string>" -v ON_ERROR_STOP=1 \
--         -f docs/sql/2026-09-09-add-strategic-themes.sql
--
--  Safe to run more than once: every DDL step is guarded, every INSERT is
--  conditional, and the seed section only touches rows it does not already
--  find. Section 4 is demo data — see the note there before running it against
--  a tenant that is not the demo tenant.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

-- The tenant this script seeds, and the tenant whose rows RLS will let us write.
-- Section 4 is the only part that reads it; sections 1-3 are tenant-agnostic DDL.
\set tenant_id '00000000-0000-0000-0000-000000000001'


-- ─────────────────────────────────────────────────────────────────────────────
--  1. The tenant-isolation function
--     Same definition InitialSmoSchema created. CREATE OR REPLACE so this script
--     does not assume it already exists.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS app;

CREATE OR REPLACE FUNCTION app.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(current_setting('app.tenant_id', true)::uuid, '00000000-0000-0000-0000-000000000000'::uuid)
$$;


-- ─────────────────────────────────────────────────────────────────────────────
--  2. SmoStrategicThemes — the alignment grid's columns
--     RLS from creation. A table the API exposes over HTTP does not get to exist
--     without a tenant-isolation policy, even for the length of this transaction.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "SmoStrategicThemes" (
    "Id"             uuid                     NOT NULL,
    "TenantId"       uuid                     NOT NULL,
    "CreatedAt"      timestamp with time zone NOT NULL,
    "UpdatedAt"      timestamp with time zone NULL,
    "CreatedBy"      text                     NOT NULL,
    "UpdatedBy"      text                     NULL,
    "StrategyId"     uuid                     NOT NULL,
    "Name"           character varying(200)   NOT NULL,
    "NameAr"         character varying(200)   NULL,
    "Description"    text                     NULL,
    "DescriptionAr"  text                     NULL,
    "Code"           character varying(20)    NULL,
    "DisplayOrder"   integer                  NOT NULL,
    CONSTRAINT "PK_SmoStrategicThemes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SmoStrategicThemes_SmoStrategies_StrategyId"
        FOREIGN KEY ("StrategyId") REFERENCES "SmoStrategies" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_SmoStrategicThemes_TenantId_StrategyId"
    ON "SmoStrategicThemes" ("TenantId", "StrategyId");
CREATE INDEX IF NOT EXISTS "IX_SmoStrategicThemes_StrategyId"
    ON "SmoStrategicThemes" ("StrategyId");

ALTER TABLE "SmoStrategicThemes" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SmoStrategicThemes" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "SmoStrategicThemes_tenant_isolation" ON "SmoStrategicThemes";
CREATE POLICY "SmoStrategicThemes_tenant_isolation" ON "SmoStrategicThemes"
FOR ALL
USING ("TenantId" = app.current_tenant_id())
WITH CHECK ("TenantId" = app.current_tenant_id());

-- The API connects as smopmo_app, which cannot create tables and so does not own
-- this one. Without this grant the new table is invisible to the running API.
-- Skipped silently when the role does not exist (a local postgres-superuser run).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smopmo_app') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON "SmoStrategicThemes" TO smopmo_app;
    END IF;
END $$;


-- ─────────────────────────────────────────────────────────────────────────────
--  3. SmoObjectives.StrategicThemeId — optional theme membership
--     Nullable and un-backfilled by the DDL: an existing objective simply has no
--     theme until section 4 (or a user) assigns one.
--     ON DELETE SET NULL — retiring a theme un-themes its objectives, never
--     deletes them.
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE "SmoObjectives" ADD COLUMN IF NOT EXISTS "StrategicThemeId" uuid NULL;

CREATE INDEX IF NOT EXISTS "IX_SmoObjectives_TenantId_StrategicThemeId"
    ON "SmoObjectives" ("TenantId", "StrategicThemeId");
CREATE INDEX IF NOT EXISTS "IX_SmoObjectives_StrategicThemeId"
    ON "SmoObjectives" ("StrategicThemeId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_SmoObjectives_SmoStrategicThemes_StrategicThemeId'
    ) THEN
        ALTER TABLE "SmoObjectives"
            ADD CONSTRAINT "FK_SmoObjectives_SmoStrategicThemes_StrategicThemeId"
            FOREIGN KEY ("StrategicThemeId") REFERENCES "SmoStrategicThemes" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

-- Record the migration so a later `dotnet ef database update` does not try to
-- apply it a second time on top of the schema this script just created.
INSERT INTO "__EFMigrationsHistory_Smo" ("MigrationId", "ProductVersion")
VALUES ('20260909100000_AddStrategicThemes', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;


-- ─────────────────────────────────────────────────────────────────────────────
--  4. Demo data for tenant :tenant_id
--
--     DEMO DATA. This section names the objectives seeded by SmoDemoSeed and
--     assigns each to a theme. Against a tenant whose objectives are real, it
--     matches nothing and is a no-op — but review it before running it there
--     anyway, and stop after section 3 if you only want the schema.
--
--     RLS is FORCEd on these tables, and FORCE applies to the table owner too,
--     so the session variable has to match the rows being written before any
--     INSERT or UPDATE below runs.
-- ─────────────────────────────────────────────────────────────────────────────

SET LOCAL app.tenant_id = :'tenant_id';

-- Four value-creation storylines cutting across the four BSC perspectives. Each
-- one spans at least two perspectives — a theme confined to a single perspective
-- would just be a sub-perspective, which is not what the axis is for.
--
-- Ids are deterministic (not gen_random_uuid()) so re-running this script cannot
-- produce a second set of themes, and so the assignments below can name them.
INSERT INTO "SmoStrategicThemes" (
    "Id", "TenantId", "CreatedAt", "CreatedBy",
    "StrategyId", "Code", "Name", "NameAr", "DisplayOrder")
SELECT
    theme."Id",
    :'tenant_id'::uuid,
    now(),
    'sql-seed',
    s."Id",
    theme."Code",
    theme."Name",
    theme."NameAr",
    theme."DisplayOrder"
FROM (
    VALUES
        ('a1000000-0000-0000-0000-000000000001'::uuid, 'TH-01', 'Revenue growth',      'نمو الإيرادات',        0),
        ('a1000000-0000-0000-0000-000000000002'::uuid, 'TH-02', 'Customer trust',      'ثقة العملاء',          1),
        ('a1000000-0000-0000-0000-000000000003'::uuid, 'TH-03', 'Delivery excellence', 'تميز التنفيذ',         2),
        ('a1000000-0000-0000-0000-000000000004'::uuid, 'TH-04', 'Data & talent',       'البيانات والكفاءات',   3)
) AS theme("Id", "Code", "Name", "NameAr", "DisplayOrder")
CROSS JOIN LATERAL (
    -- The demo tenant has exactly one strategy; ordering keeps this deterministic
    -- if that ever stops being true.
    SELECT "Id" FROM "SmoStrategies" ORDER BY "CreatedAt", "Id" LIMIT 1
) AS s
WHERE NOT EXISTS (
    SELECT 1 FROM "SmoStrategicThemes" existing WHERE existing."Id" = theme."Id"
);

-- Assign the seeded objectives to their themes, by name. Only ever fills a NULL:
-- a theme someone has already set by hand is left alone.
UPDATE "SmoObjectives" o
SET "StrategicThemeId" = assignment."ThemeId",
    "UpdatedAt"        = now(),
    "UpdatedBy"        = 'sql-seed'
FROM (
    VALUES
        ('Increase recurring revenue',      'a1000000-0000-0000-0000-000000000001'::uuid),
        ('Expand into new markets',         'a1000000-0000-0000-0000-000000000001'::uuid),
        ('Improve customer satisfaction',   'a1000000-0000-0000-0000-000000000002'::uuid),
        ('Improve quality / defect rate',   'a1000000-0000-0000-0000-000000000002'::uuid),
        ('Improve gross margin',            'a1000000-0000-0000-0000-000000000003'::uuid),
        ('Reduce project delivery time',    'a1000000-0000-0000-0000-000000000003'::uuid),
        ('Increase employee training hours','a1000000-0000-0000-0000-000000000003'::uuid),
        ('Improve digital tool adoption',   'a1000000-0000-0000-0000-000000000004'::uuid)
) AS assignment("Name", "ThemeId")
WHERE o."Name" = assignment."Name"
  AND o."StrategicThemeId" IS NULL;


-- ─────────────────────────────────────────────────────────────────────────────
--  5. What the grid will render
-- ─────────────────────────────────────────────────────────────────────────────

SELECT
    t."Code"                                   AS theme,
    t."Name"                                   AS theme_name,
    count(o."Id")                              AS objectives,
    count(DISTINCT o."PerspectiveId")          AS perspectives_spanned
FROM "SmoStrategicThemes" t
LEFT JOIN "SmoObjectives" o ON o."StrategicThemeId" = t."Id"
GROUP BY t."Code", t."Name", t."DisplayOrder"
ORDER BY t."DisplayOrder";

SELECT count(*) AS objectives_without_a_theme
FROM "SmoObjectives"
WHERE "StrategicThemeId" IS NULL;

COMMIT;
