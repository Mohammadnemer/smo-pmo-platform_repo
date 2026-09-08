// Pure date/layout math for the Gantt renderer (F7). No dependency on React or the DOM so it
// stays trivially testable and reusable by the dev-only stress generator (stressData.ts).
// Dates on the wire are `DateOnly` (yyyy-MM-dd) — parsed as UTC midnight throughout so day-diff
// arithmetic never trips over a local-timezone DST shift.

import type { DependencyResponse, TaskResponse } from '../api/pmoTypes';

export const ROW_HEIGHT = 32;
export const HEADER_HEIGHT = 40;
export const DAY_WIDTH = 28;
export const OVERSCAN_ROWS = 8;
export const MILESTONE_SIZE = 14;

const MS_PER_DAY = 86_400_000;

export function parseIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function diffDays(from: Date, to: Date): number {
  return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}

export function addDaysUtc(date: Date, days: number): Date {
  return new Date(date.getTime() + days * MS_PER_DAY);
}

export function todayUtc(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
}

export function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export interface DateRange {
  start: Date;
  totalDays: number;
}

/** Pads a couple of days on each side so the first/last bar isn't flush against the edge, and
 * always includes today so the "today" line is reachable even on a schedule entirely in the
 * past or future. */
export function computeDateRange(tasks: TaskResponse[]): DateRange {
  const today = todayUtc();
  let min = today;
  let max = addDaysUtc(today, 1);

  for (const task of tasks) {
    if (task.scheduleStart) {
      const start = parseIsoDate(task.scheduleStart);
      if (start < min) min = start;
      if (start > max) max = start;
    }
    if (task.scheduleFinish) {
      const finish = parseIsoDate(task.scheduleFinish);
      if (finish < min) min = finish;
      if (finish > max) max = finish;
    }
  }

  const start = addDaysUtc(min, -2);
  const end = addDaysUtc(max, 3);
  return { start, totalDays: Math.max(1, diffDays(start, end)) };
}

export interface TaskBar {
  /** Pixel offset from the range start, in LTR terms — callers render it via a logical
   * inset-inline-start so RTL mirroring is automatic. */
  x: number;
  width: number;
  isMilestone: boolean;
}

export function layoutTaskBar(task: TaskResponse, range: DateRange): TaskBar | null {
  if (!task.scheduleStart || !task.scheduleFinish) return null;

  const start = parseIsoDate(task.scheduleStart);
  const x = diffDays(range.start, start) * DAY_WIDTH;

  if (task.isMilestone) {
    return { x: x - MILESTONE_SIZE / 2, width: MILESTONE_SIZE, isMilestone: true };
  }

  const finish = parseIsoDate(task.scheduleFinish);
  const spanDays = Math.max(1, diffDays(start, finish) + 1);
  return { x, width: spanDays * DAY_WIDTH, isMilestone: false };
}

/** The edge of a task's bar a dependency connects from/to, per CPM semantics
 * (CriticalPathEngine.cs): FS/SS read the predecessor's start, FS/FF the successor's finish. */
export function dependencyAnchorX(
  bar: TaskBar,
  edge: 'start' | 'finish',
): number {
  if (bar.isMilestone) return bar.x + MILESTONE_SIZE / 2;
  return edge === 'start' ? bar.x : bar.x + bar.width;
}

export function predecessorEdge(type: string): 'start' | 'finish' {
  return type === 'SS' || type === 'SF' ? 'start' : 'finish';
}

export function successorEdge(type: string): 'start' | 'finish' {
  return type === 'FF' || type === 'SF' ? 'finish' : 'start';
}

/** F8: expand/collapse. A WBS code is a "parent" when some other task's code is exactly one
 * dot-segment deeper and starts with it (e.g. "1" is a parent of "1.1", but not of "1.10" —
 * the trailing dot in the prefix check rules that out). Ancestor lookups are a Set membership
 * test per dot-segment (O(depth)), not a scan over every other task, so this stays cheap even
 * at F7's 10k-task stress scale. */
export function computeParentWbsCodes(tasks: TaskResponse[]): Set<string> {
  const codes = new Set<string>();
  for (const task of tasks) {
    if (task.wbsCode) codes.add(task.wbsCode);
  }

  const parents = new Set<string>();
  for (const code of codes) {
    const segments = code.split('.');
    for (let i = segments.length - 1; i >= 1; i--) {
      const ancestor = segments.slice(0, i).join('.');
      if (codes.has(ancestor)) {
        parents.add(ancestor);
      }
    }
  }

  return parents;
}

/** Hides every task whose WBS code has a collapsed ancestor. Returns `tasks` unchanged
 * (same reference) when nothing is collapsed, so callers can memoize on identity. */
export function computeVisibleTasks(tasks: TaskResponse[], collapsed: ReadonlySet<string>): TaskResponse[] {
  if (collapsed.size === 0) return tasks;

  return tasks.filter((task) => {
    if (!task.wbsCode) return true;
    const segments = task.wbsCode.split('.');
    for (let i = 1; i < segments.length; i++) {
      if (collapsed.has(segments.slice(0, i).join('.'))) return false;
    }
    return true;
  });
}

export function wbsDepth(wbsCode: string | null): number {
  return wbsCode ? wbsCode.split('.').length - 1 : 0;
}

/** Groups dependencies by predecessor row index so the virtualized renderer only has to scan
 * dependencies whose predecessor is currently on screen, instead of all of them (matters at
 * 10k tasks / a comparable dependency count). */
export function groupDependenciesByPredecessorRow(
  dependencies: DependencyResponse[],
  rowIndexById: Map<string, number>,
): Map<number, DependencyResponse[]> {
  const grouped = new Map<number, DependencyResponse[]>();
  for (const dependency of dependencies) {
    const row = rowIndexById.get(dependency.predecessorTaskId);
    if (row === undefined) continue;
    const bucket = grouped.get(row);
    if (bucket) bucket.push(dependency);
    else grouped.set(row, [dependency]);
  }
  return grouped;
}
