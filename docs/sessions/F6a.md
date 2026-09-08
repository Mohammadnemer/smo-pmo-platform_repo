# Session F6a

## Session ID

F6a — PMO issues tab

## Scope

An **Issues** tab in the PMO workspace: one tenant-wide log of everything raised as an issue,
across every portfolio, program and project, with severity/project/open-closed filters, a
three-number summary and a "log an issue" form.

Frontend only, and PMO only — the SMO and Executive workspaces are untouched. There is no new
entity, table, endpoint or module wiring.

Requested directly (not previously on the manifest); a row was added to
`docs/build-session-index.html` so the manifest and this file agree.

## Done when

`/pmo/issues` lists every RAID item of category `Issue` in the tenant — worst severity first,
open by default, filterable, and loggable — in the PMO nav only, in AR and EN, light and dark.

## Prerequisites

- F6 (PMO project screens) for the RAID read/write hooks and the `.pmo-table`/`.pmo-form`
  conventions this page reuses.
- B6's `/api/pmo/raid` endpoints. Nothing new was needed from the backend.

## Decisions made

**An issue is a RAID item, not a new entity.** `PmoValidation.cs` already fixes the RAID
category vocabulary as Risk/Action/Issue/Decision, and `PmoDemoSeed.cs` seeds issues today. A
parallel `Issue` entity would have split the same concept across two tables and two write
paths, so the tab is a view over `GET /api/pmo/raid` narrowed to `category === 'Issue'`, and
its form posts to the same `POST /api/pmo/raid` the project workspace's board uses. That also
keeps this a UI-only session — no module seam is crossed, nothing new to roll up.

**The category filter is applied client-side.** `GET /api/pmo/raid` filters by owner
(portfolio/program/project) only. Adding a `category` query parameter would have been a
backend change for a list that is already fetched whole and cached by TanStack Query under the
same key the workspace board uses — a request the SPA mostly answers from cache. If the RAID
list ever outgrows one fetch, the parameter is the fix, and it is a one-line change here.

**Sorted by severity, not by title.** The API orders RAID items by title, which is the wrong
first read for an issue log; the worst open issue belongs at the top. Ties fall back to title
so the order is stable.

**"Closed" is matched by value, and everything else counts as open.** `RaidItem.Status` has no
server-side vocabulary (`PmoValidation.cs` validates category and severity only), so the page
cannot enumerate statuses. Treating only a literal `Closed` as closed means an unrecognised
status shows up in the log rather than silently vanishing from it — the safe direction for an
issue register. The same reasoning applies to the status labels: Open/Mitigating/Closed get
translations, anything else falls through to the raw string instead of a missing-key placeholder.

**The scope column resolves whichever owner is set.** A RAID item carries nullable
portfolio/program/project ids, so the tab does not assume every issue is a project issue: it
links to the project workspace, the program's project list or the portfolio's program list,
whichever applies, and says "Not linked" when none is.

**Overdue is a string comparison.** `DueDate` is a `DateOnly` serialised `yyyy-MM-dd`, so it is
compared against a locally-formatted today string — parsing it into a `Date` first would shift
the date by the viewer's timezone.

**The form logs against a project only.** Portfolio- and program-level issues render in the
log, but authoring one needs a scope picker the other PMO pages do not have either; the project
select matches the workspace RAID form and keeps the page's write path identical to it.

## Files touched

Frontend:

- `web/src/modules/pmo/pages/IssuesPage.tsx` / `.css` — the page (new).
- `web/src/app/routes.tsx` — `/pmo/issues`.
- `web/src/shared/layout/AppShell.tsx` — the PMO workspace nav entry, and only that one.
- `web/src/shared/layout/icons.tsx` — `AlertCircleIcon` (Risk keeps `AlertTriangleIcon`).
- `web/src/shared/i18n/resources/{en,ar}.json` — `nav.issues`, `pages.issues`.

Docs:

- `docs/build-session-index.html` — F6a row.
- `docs/sessions/F6a.md` — this file.

## How it was verified

- `npm run build` (`tsc -b && vite build`) and `npm run lint` (oxlint) — both clean.
- Rendered in Chromium (Playwright) at 1440×1000 against a mocked `/api/pmo/*`, with the
  Gantt-session convention of checking both directions and both themes: EN/LTR light and
  AR/RTL dark, **no console errors**, no horizontal page scroll. The AR pass mirrors fully —
  nav, breadcrumb, summary cards, table and form — which is what the logical-properties-only
  CSS is there for.
- Behaviour, against a fixture of 4 issues (one closed, one program-scoped, one
  portfolio-scoped) plus 1 risk:
  - default view lists the 2 open project issues, Critical above High, and excludes both the
    closed issue and the risk;
  - unchecking "Open only" brings the count to 3 (the risk still excluded);
  - severity `Critical` narrows to 1 row; project `Customer Portal Rebuild` narrows to 1 row;
  - the summary reads open 3 / high-or-critical 2 / past due 1, and the overdue due date
    renders in the risk colour with an "(overdue)" suffix;
  - submitting the form POSTs
    `{"category":"Issue","severity":"High","status":"Open","projectId":"p1",…}` to
    `/api/pmo/raid` — the same contract the workspace board writes.
- Workspace scoping confirmed in the same run: the SMO nav reads Strategy | Initiatives |
  Strategy map | Scorecard | Alignment grid and the Executive nav reads Dashboard — neither
  gains an Issues entry.

## Follow-ups

- Status and severity are read-only here. Closing or re-prioritising an issue still means
  going to the project's RAID board; an inline edit would want `PUT /api/pmo/raid/{id}`,
  which already exists.
- `OwnerUserId` is a bare GUID in the contract with no user lookup in the SPA, so there is no
  owner column. It wants the P1 user directory.
- The same page would serve Actions and Decisions with the category as a parameter; left for
  when they are asked for rather than built ahead of the roadmap.
- `/pmo/risk` is still the F6-era placeholder. It is the obvious candidate to become the
  `Risk` view of this same component, but it was deliberately left alone — this session was
  scoped to issues.
