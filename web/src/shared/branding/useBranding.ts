import { useEffect, useMemo } from 'react';
import { applyBranding, getBranding, resolveTenantId, type TenantBranding } from './branding';

export function useBranding(): TenantBranding {
  const branding = useMemo(() => getBranding(resolveTenantId()), []);

  useEffect(() => {
    applyBranding(branding);
  }, [branding]);

  return branding;
}
