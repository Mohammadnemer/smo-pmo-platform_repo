# /web — Frontend SPA

Vite + React + TypeScript SPA, scaffolded in build session **F1 · SPA
scaffold**. See `docs/sessions/F1.md` for what was built and how it was
verified.

## Layout

```
src/
  app/        bootstrap: providers (MSAL + TanStack Query), route table
  auth/       MSAL config/instance, ProtectedRoute (Entra redirect guard)
  shared/     query client, API fetch helper
  modules/    one folder per backend module (platform, smo, pmo, workflow,
              rollup, notifications), mirroring /src on the API side —
              each currently holds a placeholder pages/ stub
```

The top bar/nav shell (`shared/layout/AppShell.tsx`) is a functional
placeholder only. The branded, RTL-mirrored shell and real module screens
land in later sessions (F2 i18n & RTL shell, F3 design system, F4–F9).

## Getting started

```
cp .env.example .env.local   # fill in real Entra values once they exist
npm install
npm run dev                  # http://localhost:5173, proxies /api to the API on :5080
```

`npm run build` type-checks and builds; `npm run lint` runs oxlint.
