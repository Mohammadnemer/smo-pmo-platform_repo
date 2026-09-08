import type {
  ObjectiveLinkResponse,
  ScorecardObjective,
  ScorecardPerspective,
  ScorecardResponse,
  StrategicThemeResponse,
} from './api/scorecardTypes';

/**
 * The alignment grid's shape and graph maths, kept free of React and the DOM so the two
 * things that are actually easy to get wrong — which cell an objective lands in, and how
 * far a cause-effect chain reaches — can be reasoned about (and tested) on their own.
 *
 * The grid is the same objective set the scorecard (F4) and strategy map (F5) render,
 * crossed on a second axis: perspective down, strategic theme across.
 */

/** Sentinel column id for objectives with no theme. Not a real theme id, and never sent. */
export const UNTHEMED_COLUMN = '__unthemed__';

export interface AlignmentColumn {
  id: string;
  /** Null for the unthemed column, whose label comes from i18n rather than a row. */
  theme: StrategicThemeResponse | null;
}

export interface AlignmentRow {
  perspective: ScorecardPerspective;
  /** One entry per column, in column order. A cell may legitimately hold 0..n objectives. */
  cells: ScorecardObjective[][];
}

export interface AlignmentGrid {
  columns: AlignmentColumn[];
  rows: AlignmentRow[];
  /** Every objective in the grid, flattened — the panel's summary counts read this. */
  objectives: ScorecardObjective[];
}

/**
 * Rows follow the perspectives exactly as the aggregate ordered them (display order, so
 * Financial on top). Columns follow theme display order, with the unthemed column appended
 * only when something actually needs it — an untheme objective is never silently dropped,
 * but a tenant that themes everything doesn't get a permanently empty trailing column.
 */
export function buildAlignmentGrid(data: ScorecardResponse): AlignmentGrid {
  const objectives = data.perspectives.flatMap((perspective) => perspective.objectives);

  const themeIds = new Set(data.strategicThemes.map((theme) => theme.id));
  // An objective whose theme is missing from the aggregate (deleted mid-session, or a
  // perspective-scoped read) is treated as unthemed rather than vanishing into a column
  // that was never rendered.
  const hasUnthemed = objectives.some(
    (entry) => entry.objective.strategicThemeId === null || !themeIds.has(entry.objective.strategicThemeId),
  );

  const columns: AlignmentColumn[] = data.strategicThemes.map((theme) => ({ id: theme.id, theme }));
  if (hasUnthemed) {
    columns.push({ id: UNTHEMED_COLUMN, theme: null });
  }

  const rows: AlignmentRow[] = data.perspectives.map((perspective) => ({
    perspective,
    cells: columns.map((column) =>
      perspective.objectives.filter((entry) => columnIdFor(entry, themeIds) === column.id),
    ),
  }));

  return { columns, rows, objectives };
}

function columnIdFor(entry: ScorecardObjective, themeIds: Set<string>): string {
  const themeId = entry.objective.strategicThemeId;
  return themeId !== null && themeIds.has(themeId) ? themeId : UNTHEMED_COLUMN;
}

/** Adjacency in both directions, built once per link set. */
export interface LinkGraph {
  /** cause -> effects it drives. */
  outgoing: Map<string, string[]>;
  /** effect -> causes that drive it. */
  incoming: Map<string, string[]>;
}

export function buildLinkGraph(links: ObjectiveLinkResponse[]): LinkGraph {
  const outgoing = new Map<string, string[]>();
  const incoming = new Map<string, string[]>();

  for (const link of links) {
    push(outgoing, link.sourceObjectiveId, link.targetObjectiveId);
    push(incoming, link.targetObjectiveId, link.sourceObjectiveId);
  }

  return { outgoing, incoming };
}

function push(map: Map<string, string[]>, key: string, value: string) {
  const existing = map.get(key);
  if (existing) existing.push(value);
  else map.set(key, [value]);
}

/**
 * Every objective reachable from `objectiveId` in either direction, plus itself — the full
 * cause-effect chain the grid highlights and dims around.
 *
 * Iterative rather than recursive, and guarded by the visited set on the way in: objective
 * links are user-authored and nothing in the API forbids a cycle, so a naive walk would not
 * terminate on one.
 */
export function traceChain(graph: LinkGraph, objectiveId: string): Set<string> {
  const chain = new Set<string>([objectiveId]);

  for (const direction of [graph.incoming, graph.outgoing]) {
    const queue = [objectiveId];
    const seen = new Set<string>([objectiveId]);

    while (queue.length > 0) {
      const current = queue.pop() as string;
      for (const next of direction.get(current) ?? []) {
        if (seen.has(next)) continue;
        seen.add(next);
        chain.add(next);
        queue.push(next);
      }
    }
  }

  return chain;
}

export interface ChainStep {
  objective: ScorecardObjective;
  perspectiveName: string;
  perspectiveNameAr: string | null;
  isSelected: boolean;
}

/**
 * The chain as a bottom-up reading order: Learning &amp; Growth first, Financial last —
 * the direction causality actually runs in a balanced scorecard, and the reverse of the
 * top-down order the grid's rows are drawn in.
 */
export function chainSteps(
  data: ScorecardResponse,
  chain: Set<string>,
  selectedId: string,
): ChainStep[] {
  const steps: ChainStep[] = [];

  // Perspectives walked bottom-up, but objectives within one still left to right:
  // reversing the flattened list instead would also reverse the order inside a perspective.
  for (const perspective of [...data.perspectives].reverse()) {
    for (const entry of perspective.objectives) {
      if (!chain.has(entry.objective.id)) continue;
      steps.push({
        objective: entry,
        perspectiveName: perspective.perspective.name,
        perspectiveNameAr: perspective.perspective.nameAr,
        isSelected: entry.objective.id === selectedId,
      });
    }
  }

  return steps;
}

/** Looks an objective up across every cell — what the detail panel renders from. */
export function findObjective(grid: AlignmentGrid, objectiveId: string | null): ScorecardObjective | undefined {
  if (objectiveId === null) return undefined;
  return grid.objectives.find((entry) => entry.objective.id === objectiveId);
}
