import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '../../../shared/api/apiClient';
import type { ScorecardResponse, StrategyResponse } from './scorecardTypes';

export function useStrategiesQuery() {
  return useQuery({
    queryKey: ['smo', 'strategies'],
    queryFn: () => apiFetch<StrategyResponse[]>('/smo/strategies'),
  });
}

/**
 * The whole scorecard aggregate in one round trip (ADR 0002) — perspectives, objectives,
 * KPIs with trend, and initiatives. `strategyId` is optional so a page can chain this
 * after `useStrategiesQuery` without an extra loading branch: the query simply stays
 * disabled until a strategy id is known.
 */
export function useScorecardQuery(strategyId: string | undefined) {
  return useQuery({
    queryKey: ['smo', 'scorecard', strategyId],
    queryFn: () => apiFetch<ScorecardResponse>(`/smo/strategies/${strategyId}/scorecard`),
    enabled: strategyId !== undefined,
  });
}
