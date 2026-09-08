export interface TenantBranding {
  tenantId: string;
  displayName: string;
  /** Single/double-letter mark shown in the brand badge until real logo assets exist. */
  logoMark: string;
  /** When set, the shell renders this instead of `logoMark`. */
  logoUrl?: string;
  brand: string;
  brandSoft: string;
  brandFg: string;
}

// Matches Api's DefaultTenantId (appsettings.Development.json) — the tenant B1's seed
// creates today.
const DEFAULT_TENANT_ID = '00000000-0000-0000-0000-000000000001';

// Stubbed per-tenant config until P4 (Themes & branding admin) exists to manage this for
// real through the API. Keyed by tenant id so re-skinning is a data change here, not a
// code change anywhere else — that's what F3's "Done when" line is asking for. The second
// entry exists purely to prove that: switch tenants (see resolveTenantId) and the whole
// shell re-skins with zero component edits.
const tenantBrandings: Record<string, TenantBranding> = {
  [DEFAULT_TENANT_ID]: {
    tenantId: DEFAULT_TENANT_ID,
    displayName: 'SMO + PMO Platform',
    logoMark: 'S',
    brand: '#2a6b8f',
    brandSoft: '#e8f0f5',
    brandFg: '#ffffff',
  },
  '11111111-1111-1111-1111-111111111111': {
    tenantId: '11111111-1111-1111-1111-111111111111',
    displayName: 'Acme Construction',
    logoMark: 'A',
    brand: '#b5622a',
    brandSoft: '#faece0',
    brandFg: '#ffffff',
  },
};

const TENANT_QUERY_PARAM = 'tenant';

/**
 * Real tenant resolution belongs to the authenticated session (Entra `tid` claim,
 * resolved server-side by B1's TenantResolutionMiddleware) once a real Entra tenant/app
 * registration exists — see F1's follow-ups. Until then, a `?tenant=` query param lets
 * branding be exercised locally without one.
 */
export function resolveTenantId(): string {
  const fromQuery = new URLSearchParams(window.location.search).get(TENANT_QUERY_PARAM);
  return fromQuery && fromQuery in tenantBrandings ? fromQuery : DEFAULT_TENANT_ID;
}

export function getBranding(tenantId: string): TenantBranding {
  return tenantBrandings[tenantId] ?? tenantBrandings[DEFAULT_TENANT_ID];
}

export function applyBranding(branding: TenantBranding) {
  const root = document.documentElement.style;
  root.setProperty('--brand', branding.brand);
  root.setProperty('--brand-soft', branding.brandSoft);
  root.setProperty('--brand-fg', branding.brandFg);
}
