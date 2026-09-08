import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { apiRequest } from '../../auth/msalConfig';
import { msalInstance } from '../../auth/msalInstance';

// Dev server proxies this prefix to the API (see vite.config.ts); a real base
// URL is supplied at build time once F/T deploy sessions wire up hosting.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

async function getAccessToken(): Promise<string | null> {
  const account = msalInstance.getActiveAccount();
  if (!account) {
    return null;
  }

  try {
    const result = await msalInstance.acquireTokenSilent({ ...apiRequest, account });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      // Silent renewal needs an interactive prompt (e.g. expired session) —
      // redirect through login again rather than surfacing a raw 401.
      await msalInstance.acquireTokenRedirect({ ...apiRequest, account });
      return null;
    }
    throw error;
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getAccessToken();
  const headers = new Headers(init?.headers);
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  if (!response.ok) {
    throw new Error(`API request failed: ${response.status} ${response.statusText}`);
  }
  return (await response.json()) as T;
}
