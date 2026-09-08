// Dev-only synthetic data for verifying F7's "Done when" line — a 10k-task project scrolls
// and renders smoothly — without seeding 10k real rows through PostgreSQL on every dev boot
// (PmoDemoSeed's tree stays small and hand-curated, per F6's decision). Gated behind
// import.meta.env.DEV in ProjectWorkspacePage; never reachable in a production build.
//
// Shape-compatible with ScheduleResponse: many parallel task chains sharing one anchor date,
// each independently scheduled by simple forward chaining (no need to reimplement CPM
// client-side — the real engine is CriticalPathEngine.cs, this only has to *look* like its
// output). One chain is deliberately made the longest so the critical-path highlight has
// something real to show alongside the merely-long ones.

import type { DependencyResponse, ScheduleResponse, TaskResponse } from '../api/pmoTypes';
import { toIsoDate } from './ganttMath';

const MS_PER_DAY = 86_400_000;

export function generateStressSchedule(taskCount: number, projectId: string): ScheduleResponse {
  const anchor = new Date();
  const chainCount = Math.max(1, Math.round(Math.sqrt(taskCount)));
  const baseChainLength = Math.floor(taskCount / chainCount);

  const tasks: TaskResponse[] = [];
  const dependencies: DependencyResponse[] = [];
  let remaining = taskCount;
  let maxChainDurationDays = 0;
  const chains: { taskIds: string[]; durationDays: number }[] = [];

  for (let chainIndex = 0; chainIndex < chainCount && remaining > 0; chainIndex++) {
    const isLastChain = chainIndex === chainCount - 1;
    const length = isLastChain ? remaining : Math.min(remaining, baseChainLength);
    remaining -= length;

    let cursor = new Date(anchor);
    const taskIds: string[] = [];
    let previousId: string | null = null;

    for (let i = 0; i < length; i++) {
      const id = crypto.randomUUID();
      const durationDays = 1 + ((chainIndex + i) % 4);
      const start = new Date(cursor);
      const finish = new Date(cursor.getTime() + (durationDays - 1) * MS_PER_DAY);

      tasks.push({
        id,
        projectId,
        name: `Stress task ${chainIndex + 1}.${i + 1}`,
        description: null,
        wbsCode: `${chainIndex + 1}.${i + 1}`,
        durationDays,
        isMilestone: false,
        percentComplete: i % 5 === 0 ? 100 : (i * 13) % 100,
        assigneeUserId: null,
        constraintStart: null,
        scheduleStart: toIsoDate(start),
        scheduleFinish: toIsoDate(finish),
        totalFloatDays: null, // patched once the chain's total duration (and the max) is known
        isCritical: false, // patched below
      });
      taskIds.push(id);

      if (previousId) {
        dependencies.push({
          id: crypto.randomUUID(),
          projectId,
          predecessorTaskId: previousId,
          successorTaskId: id,
          type: 'FS',
          lagDays: 0,
        });
      }
      previousId = id;
      cursor = new Date(finish.getTime() + MS_PER_DAY);
    }

    const chainDurationDays = Math.round((cursor.getTime() - anchor.getTime()) / MS_PER_DAY);
    maxChainDurationDays = Math.max(maxChainDurationDays, chainDurationDays);
    chains.push({ taskIds, durationDays: chainDurationDays });
  }

  const taskById = new Map(tasks.map((task) => [task.id, task]));
  for (const chain of chains) {
    const isCriticalChain = chain.durationDays === maxChainDurationDays;
    const floatDays = maxChainDurationDays - chain.durationDays;
    for (const id of chain.taskIds) {
      const task = taskById.get(id);
      if (!task) continue;
      task.isCritical = isCriticalChain;
      task.totalFloatDays = floatDays;
    }
  }

  const projectFinish = toIsoDate(new Date(anchor.getTime() + (maxChainDurationDays - 1) * MS_PER_DAY));

  return {
    projectId,
    calendarAnchor: toIsoDate(anchor),
    projectFinish,
    tasks,
    dependencies,
  };
}

/** `?stress=10000` on a project workspace URL — dev-only escape hatch, never wired to any
 * production code path. Returns null unless both the flag is present and parses to a positive
 * task count. */
export function readStressTaskCount(search: string): number | null {
  const params = new URLSearchParams(search);
  const raw = params.get('stress');
  if (!raw) return null;
  const count = Number.parseInt(raw, 10);
  return Number.isFinite(count) && count > 0 ? count : null;
}
