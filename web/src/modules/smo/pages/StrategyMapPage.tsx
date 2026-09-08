import { useMemo, useState } from 'react';
import {
  Background,
  Controls,
  Handle,
  MarkerType,
  Position,
  ReactFlow,
  type Edge,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useTranslation } from 'react-i18next';
import { isRtl } from '../../../shared/i18n/i18n';
import { localized } from '../../../shared/i18n/localized';
import { Badge, Button, Card, CardBody } from '../../../shared/ui';
import { RagSummary } from '../RagSummary';
import { RAG_KEY, RAG_TONE, formatValue } from '../rag';
import { useScorecardQuery, useStrategiesQuery } from '../api/useScorecardQuery';
import type { ObjectiveResponse, RagCounts, ScorecardResponse } from '../api/scorecardTypes';
import './StrategyMapPage.css';

// Layout constants for the hand-rolled grid — no auto-layout library (dagre/elkjs): the
// demo tree is small and regular, and CLAUDE.md's "keep MVP lean" guidance applies to
// dependencies as much as infrastructure. A tenant with a much larger, irregular objective
// tree may want a real layout engine later; tracked as a follow-up, not built preemptively.
const NODE_WIDTH = 200;
const NODE_HEIGHT = 84;
const COLUMN_GAP = 36;
const ROW_HEIGHT = NODE_HEIGHT + 90;
// Fixed x=0 gutter every perspective's row label sits in; objectives extend outward from it
// so the label column stays aligned as a vertical axis regardless of how many objectives a
// given perspective has.
const LABEL_GUTTER = 176;

interface ObjectiveNodeData {
  objective: ObjectiveResponse;
  kpiRagCounts: RagCounts;
  language: string;
  selected: boolean;
  [key: string]: unknown; // xyflow's Node<T> requires an index signature on the data type
}

interface PerspectiveLabelData {
  name: string;
  [key: string]: unknown;
}

type ObjectiveNode = Node<ObjectiveNodeData, 'objective'>;
type PerspectiveLabelNode = Node<PerspectiveLabelData, 'perspectiveLabel'>;

function ObjectiveNodeCard({ data }: NodeProps<ObjectiveNode>) {
  const { t } = useTranslation();
  const { objective, kpiRagCounts, language, selected } = data;
  const name = localized(objective.name, objective.nameAr, language);
  const tone = RAG_TONE[objective.health];

  return (
    <div
      className={[
        'strategy-map-page__node',
        `strategy-map-page__node--${tone}`,
        selected && 'strategy-map-page__node--selected',
      ].filter(Boolean).join(' ')}
      title={t(RAG_KEY[objective.health])}
    >
      <Handle type="target" position={Position.Bottom} />
      <div className="strategy-map-page__node-name">{name}</div>
      <RagSummary counts={kpiRagCounts} />
      <Handle type="source" position={Position.Top} />
    </div>
  );
}

function PerspectiveLabelNodeCard({ data }: NodeProps<PerspectiveLabelNode>) {
  return <div className="strategy-map-page__row-label">{data.name}</div>;
}

const nodeTypes = { objective: ObjectiveNodeCard, perspectiveLabel: PerspectiveLabelNodeCard };

/** Builds the swim-lane grid (rows = perspectives, top-to-bottom in display order — the
 * classic Financial-at-top BSC map) and the cause-effect edges, mirrored for RTL around the
 * label gutter at x=0 so reading order flips without touching any node's own row/column
 * layout. */
function buildGraph(data: ScorecardResponse, language: string, selectedId: string | null) {
  const direction = isRtl(language) ? -1 : 1;
  const nodes: (ObjectiveNode | PerspectiveLabelNode)[] = [];

  data.perspectives.forEach((perspective, rowIndex) => {
    const y = rowIndex * ROW_HEIGHT;
    const name = localized(perspective.perspective.name, perspective.perspective.nameAr, language);

    nodes.push({
      id: `perspective-${perspective.perspective.id}`,
      type: 'perspectiveLabel',
      position: { x: 0, y },
      data: { name },
      // Explicit width/height (not just CSS): React Flow otherwise waits a frame for
      // ResizeObserver to measure the node before fitView/edge routing has real numbers to
      // work with — giving it the real, fixed size up front avoids that round trip.
      width: 150,
      height: NODE_HEIGHT,
      draggable: false,
      selectable: false,
      connectable: false,
    });

    perspective.objectives.forEach((scorecardObjective, colIndex) => {
      // position.x is always a node's *start* (left) edge, in both directions — negating it
      // alone mirrors where a node starts but not where it ends, so an RTL node's right edge
      // (start + NODE_WIDTH) swings back across x=0 and lands on top of the label column.
      // Mirroring the node's whole [start, start+width) span instead keeps the same gap on
      // both sides of x=0.
      const ltrStart = LABEL_GUTTER + colIndex * (NODE_WIDTH + COLUMN_GAP);
      const x = direction === 1 ? ltrStart : -(ltrStart + NODE_WIDTH);
      nodes.push({
        id: scorecardObjective.objective.id,
        type: 'objective',
        position: { x, y },
        width: NODE_WIDTH,
        height: NODE_HEIGHT,
        data: {
          objective: scorecardObjective.objective,
          kpiRagCounts: scorecardObjective.kpiRagCounts,
          language,
          selected: scorecardObjective.objective.id === selectedId,
        },
        draggable: false,
      });
    });
  });

  const edges: Edge[] = data.objectiveLinks.map((link) => ({
    id: link.id,
    source: link.sourceObjectiveId,
    target: link.targetObjectiveId,
    type: 'smoothstep',
    markerEnd: { type: MarkerType.ArrowClosed, color: 'var(--text-mute)' },
    style: { stroke: 'var(--border-strong)', strokeWidth: 1.5 },
  }));

  return { nodes, edges };
}

function DrillDownDrawer({ objective, onClose }: { objective: ReturnType<typeof findSelectedObjective>; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  if (!objective) return null;
  const language = i18n.language;
  const name = localized(objective.objective.name, objective.objective.nameAr, language);

  return (
    <Card className="strategy-map-page__drawer" role="dialog" aria-label={name}>
      <div className="strategy-map-page__drawer-header">
        <div>
          <h3>{name}</h3>
          {objective.objective.targetState && (
            <p className="strategy-map-page__drawer-target">{objective.objective.targetState}</p>
          )}
        </div>
        <Button variant="ghost" onClick={onClose} aria-label={t('pages.strategyMap.drawer.close')}>
          ×
        </Button>
      </div>
      <CardBody>
        <RagSummary counts={objective.kpiRagCounts} />

        <h4 className="strategy-map-page__drawer-section">{t('pages.strategyMap.drawer.kpis')}</h4>
        {objective.kpis.length === 0 ? (
          <p className="strategy-map-page__drawer-empty">{t('pages.strategyMap.drawer.noKpis')}</p>
        ) : (
          <ul className="strategy-map-page__drawer-list">
            {objective.kpis.map((scorecardKpi) => (
              <li key={scorecardKpi.kpi.id}>
                <span className="strategy-map-page__drawer-item-name">
                  {localized(scorecardKpi.kpi.name, scorecardKpi.kpi.nameAr, language)}
                </span>
                <span className="strategy-map-page__drawer-item-meta">
                  {scorecardKpi.kpi.actual === null ? '—' : formatValue(scorecardKpi.kpi.actual)}
                  {scorecardKpi.kpi.target !== null && ` / ${formatValue(scorecardKpi.kpi.target)}`}
                  {scorecardKpi.kpi.unit ? ` ${scorecardKpi.kpi.unit}` : ''}
                </span>
                <Badge tone={RAG_TONE[scorecardKpi.kpi.rag]}>{t(RAG_KEY[scorecardKpi.kpi.rag])}</Badge>
              </li>
            ))}
          </ul>
        )}

        <h4 className="strategy-map-page__drawer-section">{t('pages.strategyMap.drawer.initiatives')}</h4>
        {objective.initiatives.length === 0 ? (
          <p className="strategy-map-page__drawer-empty">{t('pages.strategyMap.drawer.noInitiatives')}</p>
        ) : (
          <ul className="strategy-map-page__drawer-list">
            {objective.initiatives.map((initiative) => (
              <li key={initiative.id}>
                <span className="strategy-map-page__drawer-item-name">
                  {localized(initiative.name, initiative.nameAr, language)}
                </span>
                {initiative.sponsor && (
                  <span className="strategy-map-page__drawer-item-meta">{initiative.sponsor}</span>
                )}
                <Badge tone="brand">{initiative.status}</Badge>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  );
}

function findSelectedObjective(data: ScorecardResponse | undefined, selectedId: string | null) {
  if (!data || !selectedId) return undefined;
  for (const perspective of data.perspectives) {
    const found = perspective.objectives.find((o) => o.objective.id === selectedId);
    if (found) return found;
  }
  return undefined;
}

export function StrategyMapPage() {
  const { t, i18n } = useTranslation();
  const strategies = useStrategiesQuery();
  const strategyId = strategies.data?.[0]?.id;
  // includeHidden=false (the default) keeps this in sync with what the scorecard renders,
  // and the aggregate's ObjectiveLinks already drops any edge into a hidden perspective.
  const scorecard = useScorecardQuery(strategyId);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const isLoading = strategies.isLoading || (strategyId !== undefined && scorecard.isLoading);
  const isError = strategies.isError || scorecard.isError;
  const isEmpty = strategies.isSuccess && strategies.data.length === 0;

  const language = i18n.language;
  const { nodes, edges } = useMemo(
    () => (scorecard.data ? buildGraph(scorecard.data, language, selectedId) : { nodes: [], edges: [] }),
    [scorecard.data, language, selectedId],
  );

  const selectedObjective = findSelectedObjective(scorecard.data, selectedId);

  if (isLoading) {
    return (
      <section>
        <h1>{t('pages.strategyMap.title')}</h1>
        <Card>
          <CardBody>
            <p>{t('pages.strategyMap.loading')}</p>
          </CardBody>
        </Card>
      </section>
    );
  }

  if (isError || isEmpty || !scorecard.data) {
    return (
      <section>
        <h1>{t('pages.strategyMap.title')}</h1>
        <Card>
          <CardBody>
            <Badge tone={isEmpty ? 'neutral' : 'risk'}>
              {t(isEmpty ? 'pages.strategyMap.empty' : 'pages.strategyMap.error')}
            </Badge>
          </CardBody>
        </Card>
      </section>
    );
  }

  return (
    <section className="strategy-map-page">
      <h1>{t('pages.strategyMap.title')}</h1>
      <p className="strategy-map-page__hint">{t('pages.strategyMap.hint')}</p>

      <div className="strategy-map-page__layout">
        <Card className="strategy-map-page__canvas-card">
          <div className="strategy-map-page__canvas" dir="ltr">
            <ReactFlow
              // `fitView` only computes its pan/zoom once, on mount — it does not re-run when
              // `nodes` changes later. Every node's x is negated on a language switch
              // (mirroring for RTL), so without a remount the canvas keeps the LTR transform
              // and the mirrored graph renders partly outside the viewport. Keying on
              // `language` forces a clean remount, and so a fresh fitView, each toggle.
              key={language}
              nodes={nodes}
              edges={edges}
              nodeTypes={nodeTypes}
              onNodeClick={(_, node) => {
                if (node.type === 'objective') setSelectedId(node.id);
              }}
              onPaneClick={() => setSelectedId(null)}
              nodesDraggable={false}
              nodesConnectable={false}
              elementsSelectable
              fitView
              fitViewOptions={{ padding: 0.2 }}
              proOptions={{ hideAttribution: true }}
            >
              <Background gap={24} />
              <Controls showInteractive={false} />
            </ReactFlow>
          </div>
        </Card>

        {selectedObjective && (
          <DrillDownDrawer objective={selectedObjective} onClose={() => setSelectedId(null)} />
        )}
      </div>
    </section>
  );
}
