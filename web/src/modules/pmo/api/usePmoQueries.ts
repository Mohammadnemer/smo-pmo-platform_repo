import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '../../../shared/api/apiClient';
import type {
  DependencyResponse,
  DependencyWriteModel,
  PortfolioResponse,
  PortfolioWriteModel,
  ProgramResponse,
  ProgramWriteModel,
  ProjectResponse,
  ProjectWriteModel,
  RaidItemResponse,
  RaidItemWriteModel,
  ScheduleResponse,
  TaskResponse,
  TaskWriteModel,
} from './pmoTypes';

function postJson<T>(path: string, body: unknown): Promise<T> {
  return apiFetch<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

function putJson<T>(path: string, body: unknown): Promise<T> {
  return apiFetch<T>(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export function usePortfoliosQuery() {
  return useQuery({
    queryKey: ['pmo', 'portfolios'],
    queryFn: () => apiFetch<PortfolioResponse[]>('/pmo/portfolios'),
  });
}

export function usePortfolioQuery(portfolioId: string | undefined) {
  return useQuery({
    queryKey: ['pmo', 'portfolio', portfolioId],
    queryFn: () => apiFetch<PortfolioResponse>(`/pmo/portfolios/${portfolioId}`),
    enabled: portfolioId !== undefined,
  });
}

export function useCreatePortfolio() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (model: PortfolioWriteModel) => postJson<PortfolioResponse>('/pmo/portfolios', model),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pmo', 'portfolios'] }),
  });
}

/** `portfolioId` omitted (not just falsy) fetches every program in the tenant — used for the
 * per-portfolio program counts on the portfolio list. */
export function useProgramsQuery(portfolioId?: string) {
  return useQuery({
    queryKey: ['pmo', 'programs', portfolioId ?? null],
    queryFn: () =>
      apiFetch<ProgramResponse[]>(`/pmo/programs${portfolioId ? `?portfolioId=${portfolioId}` : ''}`),
  });
}

export function useProgramQuery(programId: string | undefined) {
  return useQuery({
    queryKey: ['pmo', 'program', programId],
    queryFn: () => apiFetch<ProgramResponse>(`/pmo/programs/${programId}`),
    enabled: programId !== undefined,
  });
}

export function useCreateProgram() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (model: ProgramWriteModel) => postJson<ProgramResponse>('/pmo/programs', model),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pmo', 'programs'] }),
  });
}

/** `programId` omitted fetches every project in the tenant — used for the per-program
 * project counts on the program list. */
export function useProjectsQuery(programId?: string) {
  return useQuery({
    queryKey: ['pmo', 'projects', programId ?? null],
    queryFn: () => apiFetch<ProjectResponse[]>(`/pmo/projects${programId ? `?programId=${programId}` : ''}`),
  });
}

export function useProjectQuery(projectId: string | undefined) {
  return useQuery({
    queryKey: ['pmo', 'project', projectId],
    queryFn: () => apiFetch<ProjectResponse>(`/pmo/projects/${projectId}`),
    enabled: projectId !== undefined,
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (model: ProjectWriteModel) => postJson<ProjectResponse>('/pmo/projects', model),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pmo', 'projects'] }),
  });
}

/** The CPM-computed schedule aggregate (B6): tasks with their stored start/finish/float/
 * critical flag, plus dependencies — read-only here, same "stored, not recomputed on read"
 * rule the roll-up engine follows (CLAUDE.md). Authoring tasks/dependencies is F7/F8's Gantt. */
export function useScheduleQuery(projectId: string | undefined) {
  return useQuery({
    queryKey: ['pmo', 'schedule', projectId],
    queryFn: () => apiFetch<ScheduleResponse>(`/pmo/projects/${projectId}/schedule`),
    enabled: projectId !== undefined,
  });
}

/** F8: drag-to-move (sets/clears `constraintStart`) and drag-to-resize (changes
 * `durationDays`) both go through this one endpoint — the server always recomputes CPM on a
 * task write (ScheduleQuery.RecomputeAsync), so a schedule refetch after success is what
 * actually reschedules the dragged task's dependents; the mutation's own response is just the
 * one task, not the whole recomputed graph. */
export function useUpdateTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, model }: { id: string; model: TaskWriteModel }) =>
      putJson<TaskResponse>(`/pmo/tasks/${id}`, model),
    onSuccess: (_data, variables) =>
      queryClient.invalidateQueries({ queryKey: ['pmo', 'schedule', variables.model.projectId] }),
  });
}

/** F8: drag-to-link. The server rejects a dependency that would create a cycle (409/400 —
 * see PmoEndpoints.cs's `CyclicDependency`), surfaced to the caller as a rejected mutation. */
export function useCreateDependency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (model: DependencyWriteModel) => postJson<DependencyResponse>('/pmo/dependencies', model),
    onSuccess: (_data, variables) =>
      queryClient.invalidateQueries({ queryKey: ['pmo', 'schedule', variables.projectId] }),
  });
}

interface RaidFilters {
  portfolioId?: string;
  programId?: string;
  projectId?: string;
}

export function useRaidQuery(filters: RaidFilters) {
  const params = new URLSearchParams();
  if (filters.portfolioId) params.set('portfolioId', filters.portfolioId);
  if (filters.programId) params.set('programId', filters.programId);
  if (filters.projectId) params.set('projectId', filters.projectId);
  const query = params.toString();

  return useQuery({
    queryKey: ['pmo', 'raid', filters.portfolioId ?? null, filters.programId ?? null, filters.projectId ?? null],
    queryFn: () => apiFetch<RaidItemResponse[]>(`/pmo/raid${query ? `?${query}` : ''}`),
  });
}

export function useCreateRaidItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (model: RaidItemWriteModel) => postJson<RaidItemResponse>('/pmo/raid', model),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pmo', 'raid'] }),
  });
}
