# ADR 0001: Locked stack decisions

## Context

The platform needs a stable baseline for implementation so that contributors can build against the same assumptions and avoid re-opening foundational decisions during later sessions.

## Decision

The following stack decisions are accepted as the locked baseline for the MVP:

1. Backend: ASP.NET Core on the latest LTS release.
2. Frontend: React with TypeScript using Vite.
3. Cloud: Azure in the GCC region, using UAE North or Qatar Central as the deployment target.
4. Shape: modular monolith, with internal module boundaries and no microservices for MVP.
5. Database: Azure PostgreSQL Flexible Server with Row-Level Security.
6. Identity: Microsoft Entra External ID with OIDC, per-tenant SSO, and 2FA.
7. Gantt: built from scratch rather than using a commercial library.
8. .mpp binary import: deferred; JSON or CSV round-trip is used for now.

## Status

Accepted

## Consequences

These choices establish the implementation baseline for all later sessions, limit the number of open platform-level decisions, and provide a clear reference for future contributors.
