import { useQuery } from '@tanstack/react-query';
import { apiFetch } from './apiClient';

interface HealthResponse {
  status: string;
  service: string;
  modules: string[];
}

// Exercises the TanStack Query wiring end-to-end against the API's anonymous
// /health endpoint, without reaching into any module's real screens (F4+).
export function useHealthQuery() {
  return useQuery({
    queryKey: ['health'],
    queryFn: () => apiFetch<HealthResponse>('/health'),
  });
}
