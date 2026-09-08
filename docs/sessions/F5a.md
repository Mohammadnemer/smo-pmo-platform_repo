# Session F5a

## Session ID

F5a — Alignment grid

## Scope

A third view onto the SMO scorecard aggregate, alongside the scorecard (F4) and the
strategy map (F5): objectives laid out on a **perspective × strategic theme** matrix, with
cause-effect chain tracing and a detail panel.

Perspective already existed as an axis. Strategic theme did not, so this session also adds
it to the domain — the entity PRD §6.1.1 lists as an optional cross-perspective grouping
and §6.1.2 calls for alongside the four BSC perspectives.

Requested directly (not previously on the manifest); a row was added to
`docs/build-session-index.html` so the manifest and this file agree.

## Done when

Selecting an objective dims everything off its cause-effect chain and draws the links
between what is left — against real data, in AR and EN, in light and dark.

## Prerequisites

- B5 (SMO API + scorecard aggregate), F4/F5 (the aggregate's existing consumers).
- The `AddObjectiveLinks` migration — the chain tracing reads `SmoObjectiveLinks`.

## Decisions made

**Strategic theme is a real entity, not a client-side grouping.** The alternative — bucketing
objectives into pseudo-columns in the SPA — would have made the grid a picture rather than a
view of the data, and PRD §6.1.1 already specifies the entity. New table `SmoStrategicThemes`
plus a nullable `SmoObjectives.StrategicThemeId`.

**Theme membership is optional, and unthemed objectives get their own column.** A tenant may
never create a theme; an objective may not have been assigned one yet. Neither case may make
an objective disappear from a grid that claims to show the scorecard, so `buildAlignmentGrid`
appends an "unthemed" column — but only when something actually lands in it, so a fully
themed tenant does not get a permanently empty trailing column.

**`ON DELETE SET NULL` on the theme FK.** A theme is a grouping label. Retiring one must
un-theme its objectives, never cascade-delete them.

**Themes ship inside the scorecard aggregate**, not as a separate fetch — ADR 0002's one
round trip, same as the objective links F5 uses. Full CRUD is exposed at
`/api/smo/strategic-themes` as well, because without POST there would be no way to author a
theme at all; assignment then rides on the existing objective PUT.

**The card's colour is the stored `Objective.Health`,** never a value recomputed on read
(CLAUDE.md). That field is written by the X2 worker from *delivery* roll-up, so it is
`NotSet` until the PMO side has rolled up — which would leave every card grey today. The
card therefore also carries `kpiRagCounts` via the existing shared `RagSummary` component.
That is a count over rows already in the projection, which `SmoContracts.cs` is explicit is
not the same thing as recomputing roll-up health.

**Arrows are drawn into one absolutely-positioned SVG over the whole grid,** not per cell: a
link routes between arbitrary cells and would be clipped by the cell it started in.
Coordinates come from `offsetLeft`/`offsetTop`, which are measured from the wrapper's left
edge regardless of writing direction — so RTL mirrors with no second code path and no
mirroring maths of its own (contrast F5, which has to negate every node's x by hand because
React Flow lays out by raw coordinates).

**Chain tracing is iterative and guarded by a visited set.** Objective links are
user-authored and nothing in the API forbids a cycle; a naive recursive walk would not
terminate on one.

**The grid/panel split is a container query, not a media query.** What decides whether both
fit side by side is the width of the content column — the viewport minus a left nav the user
can collapse. The threshold (1330px) is the two widths added up rather than guessed: 890px of
grid min-content + 40px card padding + 396px of panel and gap.

## Files touched

Backend (SMO module):

- `src/SMO/SmoEntities.cs` — new `StrategicTheme`; `Objective.StrategicThemeId`.
- `src/SMO/SmoDbContext.cs` — table mapping, indexes, tenant query filter, SetNull FK.
- `src/SMO/SmoContracts.cs` — `StrategicThemeWriteModel`/`StrategicThemeResponse`;
  `StrategicThemeId` on the objective write and read models; `StrategicThemes` on
  `ScorecardResponse`.
- `src/SMO/SmoMapping.cs`, `src/SMO/SmoValidation.cs` — theme mapping and validation.
- `src/SMO/ScorecardQuery.cs` — themes loaded into the aggregate (a seventh set-based read).
- `src/SMO/SmoEndpoints.cs` — `/api/smo/strategic-themes` CRUD; objective POST/PUT now
  reject a theme id that is not in this tenant.
- `src/SMO/SmoDemoSeed.cs` — four themes, and every seeded objective claims one.
- `src/SMO/Migrations/20260909100000_AddStrategicThemes.cs` — hand-written, RLS from
  creation, matching `AddObjectiveLinks`.
- `src/SMO/Migrations/SmoDbContextModelSnapshot.cs` — updated to match.

Frontend:

- `web/src/modules/smo/alignment.ts` — grid shape + graph maths, free of React and the DOM.
- `web/src/modules/smo/pages/AlignmentGridPage.tsx` / `.css` — the page.
- `web/src/modules/smo/api/scorecardTypes.ts` — TS mirror of the contract changes.
- `web/src/app/routes.tsx` — `/smo/alignment`.
- `web/src/shared/layout/AppShell.tsx` — SMO nav entry (reuses the existing `GridIcon`).
- `web/src/shared/i18n/resources/{en,ar}.json` — `nav.alignmentGrid`, `pages.alignmentGrid`.

Ops / docs:

- `docs/sql/2026-09-09-add-strategic-themes.sql` — the migration plus demo themes for tenant
  `…0001`, hand-applied as `smopmo_migrator` (deploy-api.yml deliberately runs no migrations).
- `docs/build-session-index.html` — F5a row.

## How it was verified

- `dotnet build` — clean, 0 warnings, 0 errors.
- Full test suite — 74 tests, 70 passed, 0 failed, 4 skipped (Smo 27/29, Pmo 27/29,
  Rollup 11/11, Platform 5/5). The 4 skips are the pre-existing `PostgresRlsTests` in Smo and
  Pmo, `Skip`-attributed at compile time as B5/B6 left them.
- `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model
  since the last migration" — the hand-edited snapshot genuinely matches the model.
- `dotnet ef database update` applied `AddStrategicThemes` cleanly to local Postgres.
- The SQL script was run against local Postgres **twice**. First run: 4 themes inserted, 8
  objectives assigned, 0 unthemed. Second run: `INSERT 0`, `UPDATE 0`, identical final state —
  so every guard in it is real, including the `__EFMigrationsHistory_Smo` insert.
- `GET /api/smo/strategies/{id}/scorecard` against the running API returns
  `strategicThemes` with AR names and every objective carrying its `strategicThemeId`;
  0 unthemed, 7 objective links.
- `npm run build` and `npm run lint` — clean.
- Rendered in Chromium (Playwright) at 1680×1050 and inspected: 4×4 grid, 8 objective cards,
  4 row heads, 4 column heads, no horizontal overflow, **no console errors**. Selecting
  "Improve digital tool adoption" drew 4 arrows, dimmed 3 off-chain cards and listed a
  5-step chain bottom-up (Learning → Internal → Customer → Financial ×2). Confirmed in
  EN/LTR, AR/RTL (grid fully mirrored, arrows included) and AR/RTL dark.

## Follow-ups

- **Pre-existing, app-wide:** `web/src/shared/branding/branding.ts:61` writes `--brand-soft`
  as an inline style on the root element, which beats the `:root[data-theme='dark']` rule at
  `web/src/index.css:55` that exists to darken it. So `--brand-soft` keeps its light value in
  dark theme everywhere it is used — the Gantt (`GanttChart.css:191`), the strategy map's
  selection ring (`StrategyMapPage.css:68`), the shell nav (`AppShell.css:186,305`), `Badge`,
  and this page's selected chain row. Not introduced here and not fixed here; it belongs to
  F3/branding because the fix has to decide what a tenant's configured `brandSoft` means in
  dark theme.
- `Objective.TargetState` has no `…Ar` twin in the contract, so it shows English prose under
  AR. Worked around with `dir="auto"` so bidi at least renders it correctly; a real fix is a
  contract change.
- Themes have no admin UI yet — they are authored through the API or the seed. A
  theme editor belongs with the P-track admin screens.
- The grid scrolls horizontally past roughly five themes on a typical screen. Intended, and
  the page body itself never scrolls sideways; revisit if tenants routinely define more.
