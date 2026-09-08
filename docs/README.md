# Documentation

This repository uses documentation as the long-lived record of intent, implementation, and decisions.

## How this documentation is organized

- The build manifest, [build-session-index.html](../build-session-index.html), is the source of truth for what gets built and in what order.
- Every build session gets a session file in [docs/sessions](sessions), named as [docs/sessions/<ID>.md](sessions), and that file is filled in when the session runs.
- Any non-trivial technical choice gets an Architecture Decision Record (ADR) in [docs/adr](adr), numbered in sequence.
- Open questions stay in [docs/decisions-open.md](decisions-open.md) until they are resolved and promoted to an ADR.

## Document inventory

- [docs/PMO-SMO-Platform-PRD.md](PMO-SMO-Platform-PRD.md) — product scope, domain model, roadmap, and requirements context.
- [docs/architecture-mvp.md](architecture-mvp.md) — technical baseline and implementation direction for the MVP.
- [docs/adr](adr) — accepted and proposed architecture decisions.
- [docs/sessions](sessions) — build-session spec and execution log files.
- [docs/decisions-open.md](decisions-open.md) — living list of unresolved decisions.

## Documentation rules

1. Treat the build manifest as the authoritative sequencing source for implementation work.
2. Keep each session file up to date with the scope, decisions, files touched, verification, and follow-ups for that session.
3. Record substantial technical choices as ADRs rather than leaving them implicit in code or chat history.
4. If an open question is resolved, move it from [docs/decisions-open.md](decisions-open.md) into a new ADR and mark the old entry as resolved.
5. Preserve the chain of reasoning across sessions so that future contributors can see why a component, workflow, or integration exists.
