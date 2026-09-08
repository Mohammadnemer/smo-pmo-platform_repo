import { useEffect, useMemo, useRef, useState, type PointerEvent, type UIEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { DependencyResponse, TaskResponse } from '../api/pmoTypes';
import {
  DAY_WIDTH,
  OVERSCAN_ROWS,
  ROW_HEIGHT,
  addDaysUtc,
  computeDateRange,
  computeParentWbsCodes,
  computeVisibleTasks,
  dependencyAnchorX,
  diffDays,
  groupDependenciesByPredecessorRow,
  layoutTaskBar,
  parseIsoDate,
  predecessorEdge,
  successorEdge,
  toIsoDate,
  todayUtc,
  wbsDepth,
  type TaskBar,
} from './ganttMath';
import './GanttChart.css';

interface GanttChartProps {
  tasks: TaskResponse[];
  dependencies: DependencyResponse[];
  /** F8: drag-to-move. Omit to render read-only (e.g. the dev-only stress harness, which has
   * no real project behind it to write to). */
  onMoveTask?: (task: TaskResponse, constraintStart: string) => void;
  /** F8: drag-to-resize (changes duration). */
  onResizeTask?: (task: TaskResponse, durationDays: number) => void;
  /** F8: drag-to-link (draws a new FS dependency; the server rejects a cycle). */
  onLinkTasks?: (predecessor: TaskResponse, successor: TaskResponse) => void;
}

type DragState =
  | { kind: 'move'; task: TaskResponse; startClientX: number; currentClientX: number }
  | { kind: 'resize'; task: TaskResponse; startClientX: number; currentClientX: number }
  | { kind: 'link'; task: TaskResponse; startClientX: number; startClientY: number; currentClientX: number; currentClientY: number };

/**
 * Virtualised grid + SVG timeline (F7), plus F8's interaction layer: drag to move (sets a
 * "start no earlier than" constraint — see CriticalPathEngine.cs), drag to resize (changes
 * duration), drag to link (draws a dependency), and expand/collapse over the WBS-code
 * hierarchy. Only the rows currently on screen (plus overscan) are rendered on either side —
 * this is what lets a 10k-task project scroll smoothly instead of mounting 10k DOM rows and
 * 10k bars up front. Two independently scrollable panes (grid, timeline) share one
 * `scrollTop`, kept in sync imperatively (see `syncScroll`) rather than nested in a single
 * scroll container, so the timeline can scroll horizontally on its own.
 *
 * Dragging never recomputes CPM client-side — there is no client copy of the engine (F7's own
 * decision). A drag only moves its own bar/ghost live; the *committed* effect (dependents
 * rescheduling) appears once the caller's mutation resolves and the schedule query refetches
 * with the server's recomputed values. That refetch is what F8's "Done when" line is actually
 * asserting, not a client-side preview of it.
 *
 * RTL (CLAUDE.md's i18n non-negotiable): bars/gridlines/today-line are positioned with
 * `insetInlineStart`, so the browser mirrors them automatically once `<html dir="rtl">` is set
 * (F2) — no coordinate flipping needed for layout. Drag deltas are physical pixels (however the
 * pointer actually moved), so converting a delta into a day count has to flip sign in RTL —
 * later dates sit further from the timeline's start edge in both directions, but "further from
 * start" is a smaller physical X in RTL (the start edge is on the right) — see `signedDays`.
 * The live drag *preview* needs no such flip: a CSS transform translateX is always physical, so
 * the ghost bar tracks the pointer 1:1 in either direction for free.
 */
export function GanttChart({ tasks, dependencies, onMoveTask, onResizeTask, onLinkTasks }: GanttChartProps) {
  const { t, i18n } = useTranslation();
  const isRtl = i18n.dir() === 'rtl';
  const gridBodyRef = useRef<HTMLDivElement>(null);
  const timelineBodyRef = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [viewportHeight, setViewportHeight] = useState(480);
  const [collapsedWbs, setCollapsedWbs] = useState<ReadonlySet<string>>(() => new Set());
  const [drag, setDrag] = useState<DragState | null>(null);

  useEffect(() => {
    const node = timelineBodyRef.current;
    if (!node) return undefined;
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) setViewportHeight(entry.contentRect.height);
    });
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  const range = useMemo(() => computeDateRange(tasks), [tasks]);
  const totalWidth = range.totalDays * DAY_WIDTH;

  const parentWbsCodes = useMemo(() => computeParentWbsCodes(tasks), [tasks]);
  const flattenedTasks = useMemo(() => computeVisibleTasks(tasks, collapsedWbs), [tasks, collapsedWbs]);
  const totalHeight = flattenedTasks.length * ROW_HEIGHT;

  const rowIndexById = useMemo(() => {
    const map = new Map<string, number>();
    flattenedTasks.forEach((task, index) => map.set(task.id, index));
    return map;
  }, [flattenedTasks]);

  const barsById = useMemo(() => {
    const map = new Map<string, TaskBar>();
    for (const task of tasks) {
      const bar = layoutTaskBar(task, range);
      if (bar) map.set(task.id, bar);
    }
    return map;
  }, [tasks, range]);

  const depsByPredecessorRow = useMemo(
    () => groupDependenciesByPredecessorRow(dependencies, rowIndexById),
    [dependencies, rowIndexById],
  );

  const todayX = useMemo(() => diffDays(range.start, todayUtc()) * DAY_WIDTH, [range]);

  const weekTicks = useMemo(() => {
    const locale = i18n.language === 'ar' ? 'ar' : 'en';
    const ticks: { x: number; label: string }[] = [];
    for (let day = 0; day <= range.totalDays; day += 7) {
      const date = addDaysUtc(range.start, day);
      ticks.push({ x: day * DAY_WIDTH, label: date.toLocaleDateString(locale, { month: 'short', day: 'numeric' }) });
    }
    return ticks;
  }, [range, i18n.language]);

  const startIndex = Math.max(0, Math.floor(scrollTop / ROW_HEIGHT) - OVERSCAN_ROWS);
  const visibleRowCount = Math.ceil(viewportHeight / ROW_HEIGHT) + OVERSCAN_ROWS * 2;
  const endIndex = Math.min(flattenedTasks.length, startIndex + visibleRowCount);
  const visibleTasks = flattenedTasks.slice(startIndex, endIndex);

  const visibleDependencies: { dependency: DependencyResponse; predRow: number; succRow: number }[] = [];
  for (let row = startIndex; row < endIndex; row++) {
    const bucket = depsByPredecessorRow.get(row);
    if (!bucket) continue;
    for (const dependency of bucket) {
      const succRow = rowIndexById.get(dependency.successorTaskId);
      if (succRow === undefined || succRow < startIndex || succRow >= endIndex) continue;
      visibleDependencies.push({ dependency, predRow: row, succRow });
    }
  }

  function syncScroll(top: number, other: HTMLDivElement | null) {
    setScrollTop(top);
    if (other && other.scrollTop !== top) other.scrollTop = top;
  }

  function handleGridScroll(event: UIEvent<HTMLDivElement>) {
    syncScroll(event.currentTarget.scrollTop, timelineBodyRef.current);
  }

  function handleTimelineScroll(event: UIEvent<HTMLDivElement>) {
    syncScroll(event.currentTarget.scrollTop, gridBodyRef.current);
  }

  function toggleWbs(wbsCode: string) {
    setCollapsedWbs((current) => {
      const next = new Set(current);
      if (next.has(wbsCode)) next.delete(wbsCode);
      else next.add(wbsCode);
      return next;
    });
  }

  /** A physical pixel delta, signed so that a positive result always means "later" — see the
   * component doc comment on why RTL needs the flip and the preview doesn't. */
  function signedDays(deltaClientX: number): number {
    return Math.round((isRtl ? -deltaClientX : deltaClientX) / DAY_WIDTH);
  }

  function beginMove(event: PointerEvent<HTMLElement>, task: TaskResponse) {
    if (!onMoveTask) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    setDrag({ kind: 'move', task, startClientX: event.clientX, currentClientX: event.clientX });
  }

  function beginResize(event: PointerEvent<HTMLElement>, task: TaskResponse) {
    if (!onResizeTask) return;
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    setDrag({ kind: 'resize', task, startClientX: event.clientX, currentClientX: event.clientX });
  }

  function beginLink(event: PointerEvent<HTMLElement>, task: TaskResponse) {
    if (!onLinkTasks) return;
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    setDrag({
      kind: 'link',
      task,
      startClientX: event.clientX,
      startClientY: event.clientY,
      currentClientX: event.clientX,
      currentClientY: event.clientY,
    });
  }

  function handleDragMove(event: PointerEvent<HTMLElement>) {
    if (!drag) return;
    // A handle's pointermove/up still bubbles to the bar underneath even though
    // setPointerCapture retargets it to the handle (capture changes the target, not whether it
    // bubbles) — without this, every move/end during a handle-drag double-fires: once on the
    // handle, once again on the bar's own identical listener.
    event.stopPropagation();
    setDrag({ ...drag, currentClientX: event.clientX, currentClientY: event.clientY } as DragState);
  }

  function handleDragEnd(event: PointerEvent<HTMLElement>) {
    if (!drag) return;
    event.stopPropagation();
    const deltaClientX = event.clientX - drag.startClientX;

    if (drag.kind === 'move' && onMoveTask) {
      const deltaDays = signedDays(deltaClientX);
      if (deltaDays !== 0 && drag.task.scheduleStart) {
        onMoveTask(drag.task, toIsoDate(addDaysUtc(parseIsoDate(drag.task.scheduleStart), deltaDays)));
      }
    } else if (drag.kind === 'resize' && onResizeTask) {
      const deltaDays = signedDays(deltaClientX);
      const nextDuration = Math.max(1, drag.task.durationDays + deltaDays);
      if (nextDuration !== drag.task.durationDays) {
        onResizeTask(drag.task, nextDuration);
      }
    } else if (drag.kind === 'link' && onLinkTasks) {
      const dropTarget = document
        .elementFromPoint(event.clientX, event.clientY)
        ?.closest<HTMLElement>('[data-task-id]');
      const successor = dropTarget && tasks.find((task) => task.id === dropTarget.dataset.taskId);
      if (successor && successor.id !== drag.task.id) {
        onLinkTasks(drag.task, successor);
      }
    }

    setDrag(null);
  }

  return (
    <div className="gantt" role="group" aria-label={t('pages.projectWorkspace.schedule.gantt.ariaLabel')}>
      <div className="gantt-legend">
        <span className="gantt-legend__item">
          <span className="gantt-legend__swatch gantt-legend__swatch--normal" aria-hidden="true" />
          {t('pages.projectWorkspace.schedule.gantt.legendNormal')}
        </span>
        <span className="gantt-legend__item">
          <span className="gantt-legend__swatch gantt-legend__swatch--critical" aria-hidden="true" />
          {t('pages.projectWorkspace.schedule.gantt.legendCritical')}
        </span>
        <span className="gantt-legend__item">
          <span className="gantt-legend__swatch gantt-legend__swatch--today" aria-hidden="true" />
          {t('pages.projectWorkspace.schedule.gantt.legendToday')}
        </span>
      </div>

      <div className="gantt-panes">
        <div className="gantt-grid" ref={gridBodyRef} onScroll={handleGridScroll}>
          <div className="gantt-grid__header">
            <span className="gantt-col gantt-col--wbs">{t('pages.projectWorkspace.schedule.columns.wbs')}</span>
            <span className="gantt-col gantt-col--name">{t('pages.projectWorkspace.schedule.columns.name')}</span>
            <span className="gantt-col gantt-col--duration">
              {t('pages.projectWorkspace.schedule.columns.duration')}
            </span>
            <span className="gantt-col gantt-col--percent">
              {t('pages.projectWorkspace.schedule.columns.percent')}
            </span>
          </div>
          <div className="gantt-grid__spacer" style={{ height: totalHeight }}>
            {visibleTasks.map((task, i) => {
              const rowIndex = startIndex + i;
              const isParent = task.wbsCode !== null && parentWbsCodes.has(task.wbsCode);
              const isCollapsed = task.wbsCode !== null && collapsedWbs.has(task.wbsCode);
              return (
                <div
                  key={task.id}
                  data-task-id={task.id}
                  className={
                    task.isCritical ? 'gantt-grid__row gantt-grid__row--critical' : 'gantt-grid__row'
                  }
                  style={{ top: rowIndex * ROW_HEIGHT }}
                >
                  <span className="gantt-col gantt-col--wbs">{task.wbsCode ?? '—'}</span>
                  <span
                    className="gantt-col gantt-col--name"
                    style={{ paddingInlineStart: 8 + wbsDepth(task.wbsCode) * 14 }}
                    title={task.name}
                  >
                    {isParent && (
                      <button
                        type="button"
                        className="gantt-wbs-toggle"
                        aria-expanded={!isCollapsed}
                        aria-label={t(
                          isCollapsed
                            ? 'pages.projectWorkspace.schedule.gantt.expand'
                            : 'pages.projectWorkspace.schedule.gantt.collapse',
                          { name: task.name },
                        )}
                        onClick={() => toggleWbs(task.wbsCode!)}
                      >
                        {isCollapsed ? '▸' : '▾'}
                      </button>
                    )}
                    {task.name}
                  </span>
                  <span className="gantt-col gantt-col--duration">{task.durationDays}</span>
                  <span className="gantt-col gantt-col--percent">{task.percentComplete}%</span>
                </div>
              );
            })}
          </div>
        </div>

        <div className="gantt-timeline" ref={timelineBodyRef} onScroll={handleTimelineScroll}>
          <div className="gantt-timeline__header" style={{ width: totalWidth }}>
            {weekTicks.map((tick) => (
              <span key={tick.x} className="gantt-tick" style={{ insetInlineStart: tick.x }}>
                {tick.label}
              </span>
            ))}
          </div>
          <div className="gantt-timeline__spacer" style={{ width: totalWidth, height: totalHeight }}>
            {weekTicks.map((tick) => (
              <span
                key={tick.x}
                className="gantt-gridline"
                style={{ insetInlineStart: tick.x, height: totalHeight }}
                aria-hidden="true"
              />
            ))}

            {todayX >= 0 && todayX <= totalWidth && (
              <span
                className="gantt-today-line"
                style={{ insetInlineStart: todayX, height: totalHeight }}
                aria-hidden="true"
              />
            )}

            <svg className="gantt-arrows" width={totalWidth} height={totalHeight} aria-hidden="true">
              <defs>
                <marker
                  id="gantt-arrow-head"
                  viewBox="0 0 10 10"
                  refX="8"
                  refY="5"
                  markerWidth="6"
                  markerHeight="6"
                  orient="auto-start-reverse"
                >
                  <path d="M0,0 L10,5 L0,10 z" />
                </marker>
              </defs>
              {visibleDependencies.map(({ dependency, predRow, succRow }) => {
                const predBar = barsById.get(dependency.predecessorTaskId);
                const succBar = barsById.get(dependency.successorTaskId);
                if (!predBar || !succBar) return null;

                const predX = dependencyAnchorX(predBar, predecessorEdge(dependency.type));
                const succX = dependencyAnchorX(succBar, successorEdge(dependency.type));
                const predY = predRow * ROW_HEIGHT + ROW_HEIGHT / 2;
                const succY = succRow * ROW_HEIGHT + ROW_HEIGHT / 2;
                const midX = predX + Math.max(10, (succX - predX) / 2);

                return (
                  <path
                    key={dependency.id}
                    className="gantt-arrow-path"
                    d={`M ${predX} ${predY} H ${midX} V ${succY} H ${succX}`}
                    markerEnd="url(#gantt-arrow-head)"
                  />
                );
              })}
            </svg>

            {visibleTasks.map((task, i) => {
              const rowIndex = startIndex + i;
              const bar = barsById.get(task.id);
              if (!bar) return null;
              const top = rowIndex * ROW_HEIGHT;
              const isDraggingThis = drag?.task.id === task.id;
              const previewDeltaX = isDraggingThis ? drag.currentClientX - drag.startClientX : 0;
              const widthDelta =
                isDraggingThis && drag.kind === 'resize' ? (isRtl ? -previewDeltaX : previewDeltaX) : 0;

              if (bar.isMilestone) {
                return (
                  <span
                    key={task.id}
                    data-task-id={task.id}
                    className={task.isCritical ? 'gantt-milestone gantt-milestone--critical' : 'gantt-milestone'}
                    style={{
                      insetInlineStart: bar.x,
                      top: top + ROW_HEIGHT / 2 - bar.width / 2,
                      transform: isDraggingThis && drag.kind === 'move' ? `translateX(${previewDeltaX}px) rotate(45deg)` : undefined,
                      touchAction: 'none',
                      cursor: onMoveTask ? 'grab' : undefined,
                    }}
                    title={task.name}
                    onPointerDown={(event) => beginMove(event, task)}
                    onPointerMove={handleDragMove}
                    onPointerUp={handleDragEnd}
                  >
                    {onLinkTasks && (
                      <button
                        type="button"
                        className="gantt-link-handle"
                        aria-label={t('pages.projectWorkspace.schedule.gantt.linkFrom', { name: task.name })}
                        onPointerDown={(event) => beginLink(event, task)}
                        onPointerMove={handleDragMove}
                        onPointerUp={handleDragEnd}
                      />
                    )}
                  </span>
                );
              }

              return (
                <div
                  key={task.id}
                  data-task-id={task.id}
                  className={
                    task.isCritical
                      ? isDraggingThis
                        ? 'gantt-bar gantt-bar--critical gantt-bar--dragging'
                        : 'gantt-bar gantt-bar--critical'
                      : isDraggingThis
                        ? 'gantt-bar gantt-bar--dragging'
                        : 'gantt-bar'
                  }
                  style={{
                    insetInlineStart: bar.x,
                    width: Math.max(DAY_WIDTH, bar.width + widthDelta),
                    top: top + 4,
                    transform: isDraggingThis && drag.kind === 'move' ? `translateX(${previewDeltaX}px)` : undefined,
                    touchAction: 'none',
                    cursor: onMoveTask ? 'grab' : undefined,
                  }}
                  title={task.name}
                  onPointerDown={(event) => beginMove(event, task)}
                  onPointerMove={handleDragMove}
                  onPointerUp={handleDragEnd}
                >
                  <span
                    className="gantt-bar__fill"
                    style={{ inlineSize: `${Math.min(100, Math.max(0, task.percentComplete))}%` }}
                  />
                  {onResizeTask && (
                    <button
                      type="button"
                      className="gantt-resize-handle"
                      aria-label={t('pages.projectWorkspace.schedule.gantt.resize', { name: task.name })}
                      onPointerDown={(event) => beginResize(event, task)}
                      onPointerMove={handleDragMove}
                      onPointerUp={handleDragEnd}
                    />
                  )}
                  {onLinkTasks && (
                    <button
                      type="button"
                      className="gantt-link-handle"
                      aria-label={t('pages.projectWorkspace.schedule.gantt.linkFrom', { name: task.name })}
                      onPointerDown={(event) => beginLink(event, task)}
                      onPointerMove={handleDragMove}
                      onPointerUp={handleDragEnd}
                    />
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {drag?.kind === 'link' && (
        <svg className="gantt-link-preview" aria-hidden="true">
          <line x1={drag.startClientX} y1={drag.startClientY} x2={drag.currentClientX} y2={drag.currentClientY} />
        </svg>
      )}
    </div>
  );
}
