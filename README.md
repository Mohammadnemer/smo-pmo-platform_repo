# SMO + PMO Platform

Multi-tenant B2B SaaS linking **strategy (SMO)** to **execution (PMO)** with live,
bidirectional roll-up. Built as a **modular monolith** on ASP.NET Core (.NET 10 LTS)
+ React/TS, deployed to Azure. See `PMO-SMO-Platform-PRD.md` and `architecture-mvp.md`.

## This is session T1 — repo & solution skeleton

**Acceptance (Done when):** `dotnet build` passes, and no module project-references
another module directly.

### Layout (architecture-mvp.md §9)

```
/src
  /Platform      module — tenancy, org, RBAC, users/roles, lookups, i18n, themes, audit
  /SMO           module — strategy, objectives, KPIs, initiatives, scorecard
  /PMO           module — portfolios, programs, projects, schedule, RAID, status
  /Workflow      module — state-machine engine (fixed flow in MVP)
  /Rollup        module — initiative↔delivery links + health computation
  /Notifications module — in-app + email
  /Shared        mediator abstractions (IMessageBus), ITenantContext — the only thing modules share
  /Api           composition root: references every module, wires them together
  /Worker        in-process BackgroundService host (roll-up/digests) — same process in MVP
/web             React + TS SPA            (scaffolded in F1)
/infra           Azure Bicep              (authored in T2)
```

### Module boundaries are build-enforced

Each module sets `<IsModule>true</IsModule>`. `Directory.Build.targets` runs during
`dotnet build` and **fails the build** if a module project-references anything other
than `/Shared`. Modules communicate only through `IMessageBus` (in `/Shared`), and are
wired together only in `/Api`. This makes the §9/§10 seam a compiler-level guarantee,
not a convention.

### Build & run

```bash
dotnet build                      # builds everything; enforces module boundaries
dotnet run --project src/Api      # then GET http://localhost:5080/health
```

### Notes / flagged decisions

- **No NuGet packages yet.** Every project uses the shared framework only
  (`FrameworkReference Microsoft.AspNetCore.App`). The mediator is hand-rolled as
  interfaces in `/Shared` rather than pulling in MediatR — architecture-mvp.md §12.2
  flags MediatR's move to a commercial licence as an open decision. The in-process
  dispatcher implementation lands in **B4**.
- **API surface** (REST vs GraphQL, §12.1) is still open. `/Api` uses the ASP.NET Core
  Web SDK, so REST/minimal-APIs is the current default; nothing here forecloses GraphQL.
- Everything under a module is an empty seam in T1 — schemas (D-track) and APIs
  (B-track) fill them in later, in dependency order.
