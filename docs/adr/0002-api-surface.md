# ADR 0002: API surface — REST resources plus aggregate read endpoints

## Context

Architecture §12.1 left the API surface open: REST with a few aggregate endpoints for
dashboards and traceability, versus GraphQL for graph-shaped reads. B5 (SMO API) is the first
session that has to commit, because the scorecard is exactly the graph-shaped read that
motivated the question.

Forces considered:

- The SMO write model is ordinary CRUD over a tree (strategy → perspective → objective → KPI,
  plus initiatives). REST fits it with no ceremony.
- The scorecard and strategy map (F4/F5) need a whole subtree at once. Pure REST would make the
  SPA fan out one request per node, which is the N+1 problem moved onto the network.
- GraphQL would serve those reads well but adds a dependency, a second place to enforce
  authorization and tenancy, and a resolver-level N+1 problem of its own. No other session in
  the manifest assumes it.
- MVP is meant to stay lean (in-process mediator, in-memory cache, single Container App).

## Decision

REST resource endpoints for all writes and single-resource reads, plus a small number of
purpose-built **aggregate read endpoints** where a view needs a whole subtree.

For SMO this is:

```
GET|POST         /api/smo/strategies
GET|PUT|DELETE   /api/smo/strategies/{id}
GET|POST         /api/smo/perspectives            ?strategyId=
GET|PUT|DELETE   /api/smo/perspectives/{id}
GET|POST         /api/smo/objectives              ?perspectiveId= ?strategyId=
GET|PUT|DELETE   /api/smo/objectives/{id}
GET|POST         /api/smo/kpis                    ?objectiveId=
GET|PUT|DELETE   /api/smo/kpis/{id}
GET|POST         /api/smo/kpis/{id}/measurements
GET|POST         /api/smo/initiatives             ?objectiveId=
GET|PUT|DELETE   /api/smo/initiatives/{id}

GET              /api/smo/strategies/{id}/scorecard   ?includeHidden= ?trendPoints=
```

Rules that come with the decision:

1. An aggregate endpoint is a **read**. Writes always go to the resource endpoints.
2. An aggregate endpoint must answer in a **fixed number of queries** regardless of tree size —
   never one query per node.
3. Aggregates return stored roll-up values; they never recompute roll-up health on read.
4. Each module maps its own routes under `/api/<module>`; `/Api` only calls the module's
   `Map…Endpoints` method and decides who satisfies the named authorization policies.
5. Validation failures return RFC 9457 problem details with per-field errors, so the SPA can
   bind them to AR/EN labels.

GraphQL stays available as a later, additive option if traceability reads across SMO + PMO +
roll-up turn out to need arbitrary client-shaped queries. Adopting it would not require
unpicking this decision.

## Status

Accepted (B5, 2026-07-30)

## Consequences

- The SPA gets one call per view for the scorecard and strategy map, and predictable CRUD
  everywhere else.
- Every new aggregate endpoint is a deliberate, reviewable addition rather than a client-driven
  query shape — the cost is that a genuinely new view may need a new endpoint.
- Rule 2 has to be honoured by hand; `ScorecardQuery` is the reference implementation (six
  set-based reads, then in-memory assembly).
- Open decision §12.1 is now closed and removed from `docs/decisions-open.md`.
