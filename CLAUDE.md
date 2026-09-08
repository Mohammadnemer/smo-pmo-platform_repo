# Integrated SMO + PMO Platform

Multi-tenant B2B SaaS that links **strategy (SMO)** to **execution (PMO)** with
live bidirectional roll-up. The differentiator is the integration layer:
initiatives are *fulfilled by* programs/projects, execution health rolls **up**
into strategy, and strategic priority flows **down** into funded work.

---

## How we work — one session at a time

- **One chat = one build session** from `build-session-index.html` (e.g. "B2 ·
  Auth with Entra External ID"). Build the named session against its **"Done
  when"** acceptance line, then **stop** — do not sprawl into other sessions.
- A session is **done when its "Done when" line is literally true (it runs)** —
  not when we've discussed it.
- **Dependency order:** Foundation → DB → Backend → Frontend (T → D → B → F),
  with Integration (X) and Platform (P) alongside. Respect prerequisites.
- **MVP first.** Rows marked p2/p3 in the manifest are LATER phases and are out
  of scope unless I explicitly say we're pulling one in early. Do not build
  ahead of the roadmap.

## Reference docs (read the relevant one before building)

These live in the repo — read them, don't re-derive them:

- `docs/PMO-SMO-Platform-PRD.md` — product scope, domain model, features, roadmap.
- `docs/architecture-mvp.md` — **authoritative** technical baseline (Phase 1).
- `build-session-index.html` — the build manifest; each row is one session with
  its scope and "Done when" line. **This is our unit of work.**
- `docs/sessions/<ID>.md` — the per-session spec/log for the session being built
  (create it if it isn't there yet — see docs/README.md).

## Locked decisions — do NOT relitigate

- Backend: **ASP.NET Core** (.NET latest LTS)
- Frontend: **React + TypeScript (Vite)**, SPA
- Cloud: **Azure**, GCC region (UAE North / Qatar Central)
- Shape: **modular monolith** (NOT microservices) — one deploy, hard internal
  boundaries, split later only if it hurts
- DB: **Azure PostgreSQL Flexible Server + Row-Level Security**
- Identity: **Microsoft Entra External ID** (OIDC + per-tenant SSO + 2FA)
- Gantt: **built from scratch** (no commercial library)
- `.mpp` binary import: **deferred** (JSON/CSV round-trip for now)

If a request would break one of these, say so instead of complying.

## Non-negotiables — never skip, even in MVP

- **RLS + tenant isolation:** every tenant row carries `tenant_id`; the DB
  connection sets `app.tenant_id`. The database must refuse cross-tenant rows
  even if a `WHERE` clause is forgotten in code.
- **Module seams:** no module project-references another module. All
  cross-module communication goes through the in-process mediator (commands +
  domain events) in `/Shared`. Modules are wired together **only in `/Api`**.
- **Audit interceptor:** an EF Core `SaveChanges` interceptor records every change.
- **i18n + full RTL from line one:** AR/EN, logical CSS properties (never
  hard-coded left/right), mirrored nav/Gantt/charts. The **Fri–Sat weekend is
  per-tenant config** and drives all schedule date math.

## Architecture conventions

- Modules: **Platform · SMO · PMO · Workflow · Roll-up · Notifications.** Each
  module owns its own EF Core entities and tables.
- **Roll-up** is computed via **debounced domain events** in a background worker
  and **STORED**. Dashboards read the stored value — **never recompute on read.**
- Contribution model: one initiative fulfilled by many programs/projects (M:N),
  with a `primary` flag + `contribution_weight` for proportional attribution.
- Follow the repo layout in `docs/architecture-mvp.md §9`.

## Keep MVP lean

Use the in-process mediator, in-memory cache, and a single Container App.
Do NOT reach for Redis / Service Bus / read replicas — those are p2/p3 seams
that are already accounted for; adding them now is out of scope.

## When you hit an open decision — ask, don't guess

Flag open items instead of silently choosing (see PRD §12 / architecture §12):
terminology configurability (renaming core objects like "Initiative"), API
surface (REST + aggregate endpoints vs GraphQL), MediatR licensing. Stop and ask.

## Definition of done for any session

1. The session's "Done when" line runs and is demonstrably true.
2. No locked decision or module boundary was broken.
3. `docs/sessions/<ID>.md` is updated with what was built and how it was verified.
