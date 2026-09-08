// Dev server proxies this prefix to the API (see vite.config.ts); a real base
// URL is supplied at build time once F/T deploy sessions wire up hosting.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init);
  if (!response.ok) {
    throw new Error(`API request failed: ${response.status} ${response.statusText}`);
  }
  return (await response.json()) as T;
}
