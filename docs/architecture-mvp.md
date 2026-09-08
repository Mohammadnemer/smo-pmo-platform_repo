# Architecture — Integrated SMO + PMO Platform (MVP)

**Status:** MVP baseline · living document
**Scope of this doc:** the Phase-1 (MVP) architecture only. Later phases are noted as *deferred* so the MVP stays small, but the seams for them are built in from day one.

---

## 1. Locked decisions

| Area | Decision | Why |
|---|---|---|
| Backend | ASP.NET Core (.NET, latest LTS) | Team's strongest ecosystem |
| Frontend | React + TypeScript (Vite), SPA | Richest ecosystem for the hard UI (custom Gantt, strategy map, RTL) |
| Cloud | Azure | Team choice; strong GCC region presence |
| Shape | **Modular monolith** (not microservices) | Solo/very small team — one deploy, hard internal boundaries, split later if ever needed |
| Database | Azure Database for PostgreSQL + Row-Level Security | PRD's default; best-in-class RLS ergonomics; cheap at small scale |
| Identity | Microsoft Entra External ID | OIDC + per-tenant SAML/OIDC SSO + 2FA without owning identity (SOC-2 surface) |
| Gantt | **Built from scratch** | No license cost, full control, RTL done exactly right |
| `.mpp` | Deferred — JSON/CSV round-trip for now | Binary `.mpp` needs a commercial library; not MVP |

---

## 2. Architecture at a glance

One deployable ASP.NET Core application. Internally split into **modules** with strict boundaries: each module owns its own domain and EF Core entities and never touches another module's tables. Cross-module communication goes through an **in-process mediator** (commands + domain events), never direct calls into internals.

```
React SPA (TS)  ──HTTPS / JWT──►  Modular monolith (ASP.NET Core)
                                    ├─ Request pipeline: auth · tenant · RBAC · audit
                                    ├─ Modules: Platform · SMO · PMO · Workflow · Roll-up · Notifications
                                    └─ Background worker: roll-up recompute + digests
                                          │
                                          ▼
                            Azure managed services
                            PostgreSQL (RLS) · Blob · Entra ID
                            (Redis + Service Bus added at scale)
```

**Design rule for a solo team:** *build so you can split later, but don't split now.* The in-process event bus can be swapped for Azure Service Bus and a module moved to its own service without touching the rest of the code.

---

## 3. Modules & boundaries

| Module | Owns |
|---|---|
| **Platform / Identity** | Tenancy, org structure, RBAC, users/roles, lookups, localization, themes, audit log |
| **SMO** | Strategy, perspectives, objectives, KPIs/measures, initiatives, scorecard |
| **PMO** | Portfolios, programs, projects, schedule/tasks, RAID logs, status reports |
| **Workflow** | Configurable state-machine engine (states, transitions, approvals) — *fixed flow in MVP, configurable later* |
| **Roll-up / Integration** | Initiative ↔ program/project links (M:N, primary + weight); health computation up the graph |
| **Notifications** | In-app + email; digests later |

Each module = its own folder/project. Boundaries are compiler-enforced (see §9), not just conventional.

---

## 4. Multi-tenancy & the request pipeline

Every request passes the same gate before reaching a module:

1. **Auth** — validate JWT from Entra External ID; extract `tenant_id` + roles from claims.
2. **Tenant resolve** — set a scoped `TenantContext`; the DB connection issues `SET app.tenant_id = …`.
3. **RBAC** — policy-based authorization against the per-entity × per-action matrix, scoped to org unit.
4. **Audit** — an EF Core `SaveChanges` interceptor records every change automatically.

**Isolation model:** `tenant_id` on every row **plus** PostgreSQL Row-Level Security. RLS means the database itself refuses to return another tenant's rows even if a `WHERE` clause is forgotten in code — defense in depth that matters most for a solo reviewer.

---

## 5. The roll-up engine (the product's thesis)

This is the one part that isn't plain CRUD — it's what lets a user answer *"is our work moving our strategy?"*

- A PMO write (task % complete, RAID severity, schedule variance) raises a **domain event**.
- The background worker **debounces** (so editing 50 tasks ≠ 50 recomputes), walks the graph `project → initiative → objective → strategy`, and **stores** the computed health.
- Dashboards read the **stored** value — never recompute on read (per NFRs).
- Contribution model: one initiative fulfilled by many programs/projects (and vice versa), with a `primary` flag + `contribution_weight` for proportional attribution.

**MVP simplification:** in-process domain events + an in-process `BackgroundService`. At scale, promote to Azure Service Bus + a separately-scaled worker.

---

## 6. Data & persistence

- **PostgreSQL (Azure Flexible Server)** — shared schema, `tenant_id` on every row, RLS policies. EF Core with a global query filter on `tenant_id`.
- **Blob Storage** — attachments, exported PDFs, schedule baselines-as-blobs if needed.
- **Redis** — *deferred to scale*; MVP uses in-memory caching to save cost.
- **Working calendars** — per-tenant weekend config (Jordan/GCC = Fri–Sat) + holidays; this drives all schedule date math.

---

## 7. Gantt engine (already built — framework-agnostic core)

Built in layers, correctness-first. Layers 1–3 are done and verified:

1. **Domain model** — tasks (WBS hierarchy), dependencies, calendars, baselines.
2. **Working-calendar engine** — index↔date conversion that skips weekends/holidays.
3. **Scheduling engine (CPM)** — forward/backward pass, all four dependency types (FS/SS/FF/SF) with lag, total float, critical path, summary roll-up, milestones. Works in integer working-day index space so the algorithm stays clean.
4. **Rendering** *(next)* — virtualized grid (left) + SVG timeline (right); RTL-aware.
5. **Interaction** *(next)* — drag-move, drag-resize, drag-to-link, expand/collapse.
6. **Baselines** *(later)* — snapshot + variance overlay.

---

## 8. Azure topology — MVP (lean)

| Concern | MVP | At scale |
|---|---|---|
| SPA hosting | Azure Static Web Apps | + Front Door / WAF |
| API + worker | One Azure Container App (worker in-process) | Split worker into its own Container App |
| Database | PostgreSQL Flexible Server | + read replicas |
| Files | Blob Storage | — |
| Cache | in-memory | Azure Cache for Redis |
| Events | in-process mediator | Azure Service Bus |
| Identity | Entra External ID | — |
| Secrets / obs | Key Vault · Application Insights | — |
| IaC / CI | Bicep · GitHub Actions | — |
| Region | UAE North or Qatar Central (GCC residency) | — |

---

## 9. Proposed repo / solution layout

```
/src
  /Platform      (Identity, tenancy, RBAC, lookups, i18n, themes, audit)
  /SMO           (strategy, objectives, KPIs, initiatives)
  /PMO           (portfolios, programs, projects, schedule, RAID)
  /Workflow      (state-machine engine)
  /Rollup        (links + health computation)
  /Notifications
  /Shared        (mediator, TenantContext, common abstractions)
  /Api           (composition root: pipeline, DI, controllers/minimal APIs)
  /Worker        (BackgroundService host — same process in MVP)
/web             (React + TS SPA, including the custom Gantt engine)
/infra           (Bicep)
```

Modules reference `/Shared` and are wired together only in `/Api`. A module must not project-reference another module — that keeps boundaries honest.

---

## 10. Non-negotiables (do NOT skip, even at MVP)

Retrofitting these later is brutal:

- RLS + tenant isolation
- Module seams (in-process events, no cross-module table access)
- Audit interceptor
- i18n + full RTL in the SPA (logical CSS properties from line one)

## 11. Explicitly deferred (post-MVP)

EVM, resource capacity depth, configurable workflow builder, `.mpp` binary import, connector marketplace, Redis, Service Bus, Hijri calendar, automated KPI feeds, mobile app, AI insights.

---

## 12. Open decisions

1. ~~**API surface** — REST + a few aggregate endpoints for dashboards/traceability, vs GraphQL for graph-shaped reads.~~ **Decided in B5:** REST resources + purpose-built aggregate read endpoints — see [ADR 0002](adr/0002-api-surface.md).
2. **MediatR licensing** — recent versions moved commercial; confirm terms or use a lightweight hand-rolled dispatcher.
3. **Terminology configurability** — do tenants need to rename core objects (e.g. "Initiative")?
4. **Postgres vs Azure SQL** — Postgres chosen; revisit only if EF Core/Azure-native integration becomes a pain point.
