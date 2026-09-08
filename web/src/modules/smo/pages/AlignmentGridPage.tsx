import type { CSSProperties, ReactNode } from 'react';
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { localized } from '../../../shared/i18n/localized';
import { Badge, Card, CardBody } from '../../../shared/ui';
import { RagSummary } from '../RagSummary';
import { RAG_KEY, RAG_TONE, formatValue } from '../rag';
import {
  buildAlignmentGrid,
  buildLinkGraph,
  chainSteps,
  findObjective,
  traceChain,
  type AlignmentGrid,
  type AlignmentRow,
  type ChainStep,
} from '../alignment';
import { useScorecardQuery, useStrategiesQuery } from '../api/useScorecardQuery';
import type { RagStatus, ScorecardObjective } from '../api/scorecardTypes';
import './AlignmentGridPage.css';

/**
 * The alignment grid — a third view onto the same scorecard aggregate the scorecard (F4)
 * and strategy map (F5) read.
 *
 * Perspective runs down the rows, strategic theme across the columns, and each objective
 * sits at the intersection of the two. Selecting one traces its whole cause-effect chain:
 * everything off the chain dims, and the links between the objectives still lit are drawn
 * as arrows over the grid.
 *
 * The arrows go into one absolutely-positioned SVG rather than per-cell, because a link
 * routes between arbitrary cells and would otherwise be clipped by the cell it starts in.
 * Coordinates come from `offsetLeft`/`offsetTop` against the wrapper, which are measured
 * from the wrapper's left edge regardless of writing direction — so the same maths holds
 * once the browser mirrors the grid for RTL, with no second code path.
 */

interface EdgePath {
  key: string;
  d: string;
}

/** Minimum vertical arc on a link, so a short hop still reads as a curve. */
const MIN_ARC = 24;

/** RagStatus values, named — the API sends the enum's underlying number (scorecardTypes). */
const RAG_AMBER: RagStatus = 2;
const RAG_RED: RagStatus = 1;

export function AlignmentGridPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.language;

  const strategies = useStrategiesQuery();
  const strategyId = strategies.data?.[0]?.id;
  const scorecard = useScorecardQuery(strategyId);
  const data = scorecard.data;

  const [selectedId, setSelectedId] = useState<string | null>(null);

  const wrapperRef = useRef<HTMLDivElement | null>(null);
  const cardRefs = useRef(new Map<string, HTMLElement>());
  const [edges, setEdges] = useState<EdgePath[]>([]);

  const grid = useMemo(() => (data ? buildAlignmentGrid(data) : null), [data]);
  const graph = useMemo(() => buildLinkGraph(data?.objectiveLinks ?? []), [data]);
  const chain = useMemo(
    () => (selectedId === null ? null : traceChain(graph, selectedId)),
    [graph, selectedId],
  );
  const steps = useMemo(
    () => (data && chain && selectedId ? chainSteps(data, chain, selectedId) : []),
    [data, chain, selectedId],
  );

  const registerCard = useCallback((id: string, element: HTMLElement | null) => {
    if (element) cardRefs.current.set(id, element);
    else cardRefs.current.delete(id);
  }, []);

  const drawEdges = useCallback(() => {
    if (!chain || !data) {
      setEdges([]);
      return;
    }

    const next: EdgePath[] = [];
    for (const link of data.objectiveLinks) {
      if (!chain.has(link.sourceObjectiveId) || !chain.has(link.targetObjectiveId)) continue;

      const from = cardRefs.current.get(link.sourceObjectiveId);
      const to = cardRefs.current.get(link.targetObjectiveId);
      if (!from || !to) continue;

      next.push({ key: link.id, d: pathBetween(from, to) });
    }

    setEdges(next);
  }, [chain, data]);

  // Layout effect, not effect: this reads the cards' boxes, so it has to run after React
  // commits the DOM but before the browser paints — otherwise the arrows land one frame
  // behind the dimming they belong to.
  useLayoutEffect(() => {
    drawEdges();
  }, [drawEdges]);

  // Column widths are fractional, so anything that resizes the grid — the window, the nav
  // collapsing, a web font finally loading — moves every card the arrows are anchored to.
  useEffect(() => {
    const wrapper = wrapperRef.current;
    if (!wrapper) return;

    const observer = new ResizeObserver(() => drawEdges());
    observer.observe(wrapper);
    return () => observer.disconnect();
  }, [drawEdges]);

  if (strategies.isLoading || (strategyId !== undefined && scorecard.isLoading)) {
    return <PageShell>{t('pages.alignmentGrid.loading')}</PageShell>;
  }

  const isEmpty = strategies.isSuccess && strategies.data.length === 0;
  if (strategies.isError || scorecard.isError || isEmpty || !data || !grid) {
    return (
      <PageShell>
        <Badge tone={isEmpty ? 'neutral' : 'risk'}>
          {t(isEmpty ? 'pages.alignmentGrid.empty' : 'pages.alignmentGrid.error')}
        </Badge>
      </PageShell>
    );
  }

  const selected = findObjective(grid, selectedId);

  return (
    <section className="alignment-grid-page">
      <div className="alignment-grid-page__heading">
        <h1>{t('pages.alignmentGrid.title')}</h1>
        <p className="alignment-grid-page__hint">{t('pages.alignmentGrid.hint')}</p>
      </div>

      <div className="alignment-grid-page__layout">
        <Card className="alignment-grid-page__grid-card">
          <div className="alignment-grid-page__toolbar">
            <RagLegend />
            <div className="alignment-grid-page__toolbar-actions">
              {selectedId === null ? (
                <span className="alignment-grid-page__trace-hint">{t('pages.alignmentGrid.traceHint')}</span>
              ) : (
                <button type="button" className="alignment-grid-page__clear" onClick={() => setSelectedId(null)}>
                  {t('pages.alignmentGrid.clearTrace')}
                </button>
              )}
            </div>
          </div>

          <div className="alignment-grid-page__scroll">
            <div className="alignment-grid-page__wrapper" ref={wrapperRef}>
              {/* aria-hidden: the arrows restate the chain the detail panel already lists
                  in reading order, so announcing them again is noise. */}
              <svg className="alignment-grid-page__edges" aria-hidden="true">
                <defs>
                  <marker
                    id="alignment-grid-arrow"
                    viewBox="0 0 8 8"
                    refX="6"
                    refY="4"
                    markerWidth="6"
                    markerHeight="6"
                    orient="auto"
                  >
                    <path d="M0 0 L8 4 L0 8 z" fill="var(--brand)" />
                  </marker>
                </defs>
                {edges.map((edge) => (
                  <path
                    key={edge.key}
                    d={edge.d}
                    fill="none"
                    stroke="var(--brand)"
                    strokeWidth="1.6"
                    markerEnd="url(#alignment-grid-arrow)"
                  />
                ))}
              </svg>

              <div
                className="alignment-grid-page__grid"
                style={{ '--column-count': grid.columns.length } as CSSProperties}
              >
                <div className="alignment-grid-page__corner" />
                {grid.columns.map((column) => (
                  <div key={column.id} className="alignment-grid-page__column-head">
                    <span className="alignment-grid-page__code" dir="ltr">
                      {column.theme?.code ?? '—'}
                    </span>
                    <span className="alignment-grid-page__column-name">
                      {column.theme
                        ? localized(column.theme.name, column.theme.nameAr, language)
                        : t('pages.alignmentGrid.unthemed')}
                    </span>
                  </div>
                ))}

                {grid.rows.map((row, rowIndex) => (
                  <GridRow
                    key={row.perspective.perspective.id}
                    row={row}
                    rowIndex={rowIndex}
                    language={language}
                    chain={chain}
                    selectedId={selectedId}
                    onSelect={setSelectedId}
                    registerCard={registerCard}
                  />
                ))}
              </div>
            </div>
          </div>
        </Card>

        <Card className="alignment-grid-page__panel">
          {selected ? (
            <DetailPanel
              selected={selected}
              steps={steps}
              language={language}
              onClose={() => setSelectedId(null)}
            />
          ) : (
            <SummaryPanel grid={grid} linkCount={data.objectiveLinks.length} />
          )}
        </Card>
      </div>
    </section>
  );
}

function PageShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.alignmentGrid.title')}</h1>
      <Card>
        <CardBody>{children}</CardBody>
      </Card>
    </section>
  );
}

function RagLegend() {
  const { t } = useTranslation();
  const entries = [
    { tone: 'ok', key: 'pages.scorecard.rag.green' },
    { tone: 'warn', key: 'pages.scorecard.rag.amber' },
    { tone: 'risk', key: 'pages.scorecard.rag.red' },
  ];

  return (
    <div className="alignment-grid-page__legend">
      {entries.map((entry) => (
        <span key={entry.tone} className="alignment-grid-page__legend-item">
          <span className={`alignment-grid-page__dot alignment-grid-page__dot--${entry.tone}`} aria-hidden="true" />
          {t(entry.key)}
        </span>
      ))}
    </div>
  );
}

interface GridRowProps {
  row: AlignmentRow;
  rowIndex: number;
  language: string;
  chain: Set<string> | null;
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  registerCard: (id: string, element: HTMLElement | null) => void;
}

function GridRow({ row, rowIndex, language, chain, selectedId, onSelect, registerCard }: GridRowProps) {
  const { t } = useTranslation();
  const perspective = row.perspective.perspective;

  return (
    <>
      <div className="alignment-grid-page__row-head">
        <span className="alignment-grid-page__code" dir="ltr">
          {String(rowIndex + 1).padStart(2, '0')}
        </span>
        <span className="alignment-grid-page__row-name">
          {localized(perspective.name, perspective.nameAr, language)}
        </span>
      </div>

      {row.cells.map((cell, columnIndex) => (
        <div
          key={columnIndex}
          className={`alignment-grid-page__cell${cell.length === 0 ? ' alignment-grid-page__cell--empty' : ''}`}
        >
          {cell.length === 0
            ? t('pages.alignmentGrid.emptyCell')
            : cell.map((entry) => (
                <ObjectiveCard
                  key={entry.objective.id}
                  entry={entry}
                  language={language}
                  inChain={chain === null || chain.has(entry.objective.id)}
                  isSelected={entry.objective.id === selectedId}
                  onSelect={onSelect}
                  registerCard={registerCard}
                />
              ))}
        </div>
      ))}
    </>
  );
}

interface ObjectiveCardProps {
  entry: ScorecardObjective;
  language: string;
  inChain: boolean;
  isSelected: boolean;
  onSelect: (id: string | null) => void;
  registerCard: (id: string, element: HTMLElement | null) => void;
}

/**
 * The card's spine colour is the objective's *stored* roll-up health — the value the X2
 * worker writes from delivery, never recomputed here (CLAUDE.md). That value is NotSet
 * until delivery has rolled up, so the card also carries the KPI RAG mix the aggregate
 * already counted, which is what makes it readable on day one rather than a grey box.
 */
function ObjectiveCard({ entry, language, inChain, isSelected, onSelect, registerCard }: ObjectiveCardProps) {
  const objective = entry.objective;
  const tone = RAG_TONE[objective.health];

  return (
    <button
      type="button"
      ref={(element) => registerCard(objective.id, element)}
      className={[
        'alignment-grid-page__objective',
        `alignment-grid-page__objective--${tone}`,
        !inChain && 'alignment-grid-page__objective--dimmed',
        isSelected && 'alignment-grid-page__objective--selected',
      ]
        .filter(Boolean)
        .join(' ')}
      aria-pressed={isSelected}
      onClick={() => onSelect(isSelected ? null : objective.id)}
    >
      <span className="alignment-grid-page__objective-title">
        <span className={`alignment-grid-page__dot alignment-grid-page__dot--${tone}`} aria-hidden="true" />
        {localized(objective.name, objective.nameAr, language)}
      </span>
      <span className="alignment-grid-page__objective-meta">
        <RagSummary counts={entry.kpiRagCounts} />
      </span>
    </button>
  );
}

function SummaryPanel({ grid, linkCount }: { grid: AlignmentGrid; linkCount: number }) {
  const { t } = useTranslation();

  const stats = [
    { value: grid.objectives.length, label: t('pages.alignmentGrid.summary.objectives'), tone: '' },
    { value: linkCount, label: t('pages.alignmentGrid.summary.links'), tone: '' },
    {
      value: grid.objectives.filter((entry) => entry.objective.health === RAG_AMBER).length,
      label: t('pages.alignmentGrid.summary.atRisk'),
      tone: 'warn',
    },
    {
      value: grid.objectives.filter((entry) => entry.objective.health === RAG_RED).length,
      label: t('pages.alignmentGrid.summary.offTrack'),
      tone: 'risk',
    },
  ];

  return (
    <div className="alignment-grid-page__panel-body">
      <span className="alignment-grid-page__panel-kicker">{t('pages.alignmentGrid.summary.title')}</span>
      <p className="alignment-grid-page__panel-lead">{t('pages.alignmentGrid.summary.body')}</p>

      <div className="alignment-grid-page__stats">
        {stats.map((stat) => (
          <div key={stat.label} className="alignment-grid-page__stat">
            <span
              className={`alignment-grid-page__stat-value${stat.tone ? ` alignment-grid-page__stat-value--${stat.tone}` : ''}`}
              dir="ltr"
            >
              {stat.value}
            </span>
            <span className="alignment-grid-page__stat-label">{stat.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

interface DetailPanelProps {
  selected: ScorecardObjective;
  steps: ChainStep[];
  language: string;
  onClose: () => void;
}

function DetailPanel({ selected, steps, language, onClose }: DetailPanelProps) {
  const { t } = useTranslation();
  const objective = selected.objective;
  const selectedStep = steps.find((step) => step.isSelected);

  return (
    <div className="alignment-grid-page__panel-body">
      <div className="alignment-grid-page__panel-head">
        <span className="alignment-grid-page__panel-kicker">
          {selectedStep ? localized(selectedStep.perspectiveName, selectedStep.perspectiveNameAr, language) : ''}
        </span>
        <button
          type="button"
          className="alignment-grid-page__close"
          onClick={onClose}
          aria-label={t('pages.alignmentGrid.detail.close')}
        >
          ×
        </button>
      </div>

      <h2 className="alignment-grid-page__panel-title">
        {localized(objective.name, objective.nameAr, language)}
      </h2>
      <div className="alignment-grid-page__panel-tags">
        <Badge tone={RAG_TONE[objective.health]}>{t(RAG_KEY[objective.health])}</Badge>
      </div>
      {/* dir="auto": TargetState is the one label on this panel with no Arabic twin in the
          contract (it is free-form prose, not a label), so under AR it is usually English
          sitting in an RTL block — which drags its leading number to the wrong end unless
          the browser is allowed to infer direction from the text itself. */}
      {objective.targetState && (
        <p className="alignment-grid-page__target-state" dir="auto">
          {objective.targetState}
        </p>
      )}

      <section className="alignment-grid-page__section">
        <h3>{t('pages.alignmentGrid.detail.chain')}</h3>
        {steps.length <= 1 ? (
          <p className="alignment-grid-page__section-empty">{t('pages.alignmentGrid.detail.noChain')}</p>
        ) : (
          <ol className="alignment-grid-page__chain">
            {steps.map((step) => (
              <li key={step.objective.objective.id} className={step.isSelected ? 'is-selected' : undefined}>
                <span className="alignment-grid-page__chain-perspective">
                  {localized(step.perspectiveName, step.perspectiveNameAr, language)}
                </span>
                <span className="alignment-grid-page__chain-name">
                  {localized(step.objective.objective.name, step.objective.objective.nameAr, language)}
                </span>
              </li>
            ))}
          </ol>
        )}
      </section>

      <section className="alignment-grid-page__section">
        <h3>{t('pages.alignmentGrid.detail.kpis')}</h3>
        {selected.kpis.length === 0 ? (
          <p className="alignment-grid-page__section-empty">{t('pages.alignmentGrid.detail.noKpis')}</p>
        ) : (
          selected.kpis.map((scorecardKpi) => {
            const kpi = scorecardKpi.kpi;
            const unit = kpi.unit ? ` ${kpi.unit}` : '';
            return (
              <div key={kpi.id} className="alignment-grid-page__kpi">
                <span className="alignment-grid-page__kpi-name">
                  <span
                    className={`alignment-grid-page__dot alignment-grid-page__dot--${RAG_TONE[kpi.rag]}`}
                    aria-hidden="true"
                  />
                  {localized(kpi.name, kpi.nameAr, language)}
                </span>
                <span className="alignment-grid-page__kpi-values">
                  <span>
                    {t('pages.alignmentGrid.detail.target')}{' '}
                    <b dir="ltr">{kpi.target === null ? '—' : `${formatValue(kpi.target)}${unit}`}</b>
                  </span>
                  <span>
                    {t('pages.alignmentGrid.detail.actual')}{' '}
                    <b dir="ltr">{kpi.actual === null ? '—' : `${formatValue(kpi.actual)}${unit}`}</b>
                  </span>
                </span>
                <AchievementBar percent={kpi.achievementPercent} rag={kpi.rag} />
              </div>
            );
          })
        )}
      </section>

      <section className="alignment-grid-page__section">
        <h3>{t('pages.alignmentGrid.detail.initiatives')}</h3>
        {selected.initiatives.length === 0 ? (
          <p className="alignment-grid-page__section-empty">{t('pages.alignmentGrid.detail.noInitiatives')}</p>
        ) : (
          selected.initiatives.map((initiative) => (
            <div key={initiative.id} className="alignment-grid-page__initiative">
              <span>{localized(initiative.name, initiative.nameAr, language)}</span>
              <Badge tone={RAG_TONE[initiative.health]}>{initiative.status}</Badge>
            </div>
          ))
        )}
      </section>
    </div>
  );
}

function AchievementBar({ percent, rag }: { percent: number | null; rag: RagStatus }) {
  if (percent === null) return null;
  const clamped = Math.max(0, Math.min(100, percent));

  return (
    <span className="alignment-grid-page__bar">
      <span
        className={`alignment-grid-page__bar-fill alignment-grid-page__bar-fill--${RAG_TONE[rag]}`}
        style={{ inlineSize: `${clamped}%` }}
      />
    </span>
  );
}

/**
 * A cubic bezier from the cause's edge to the effect's edge.
 *
 * Causes sit below their effects (Learning &amp; Growth feeding Financial), so the usual
 * case leaves the source's top edge and arrives at the target's bottom edge. Two objectives
 * in the same perspective are the exception — there is no vertical room between them, so
 * the curve arcs over the top rather than collapsing into a flat horizontal line.
 */
function pathBetween(from: HTMLElement, to: HTMLElement): string {
  const a = { x: from.offsetLeft, y: from.offsetTop, w: from.offsetWidth, h: from.offsetHeight };
  const b = { x: to.offsetLeft, y: to.offsetTop, w: to.offsetWidth, h: to.offsetHeight };

  if (Math.abs(a.y - b.y) < 8) {
    const sourceIsAfter = a.x > b.x;
    const x1 = sourceIsAfter ? a.x : a.x + a.w;
    const x2 = sourceIsAfter ? b.x + b.w : b.x;
    const y1 = a.y + a.h / 2;
    const y2 = b.y + b.h / 2;
    const lift = Math.min(30, Math.abs(x1 - x2) * 0.35) + 10;
    return `M${x1} ${y1} C ${x1} ${y1 - lift}, ${x2} ${y2 - lift}, ${x2} ${y2}`;
  }

  const x1 = a.x + a.w / 2;
  const y1 = a.y;
  const x2 = b.x + b.w / 2;
  const y2 = b.y + b.h;
  const arc = Math.max(MIN_ARC, (y1 - y2) * 0.55);
  return `M${x1} ${y1} C ${x1} ${y1 - arc}, ${x2} ${y2 + arc}, ${x2} ${y2}`;
}
