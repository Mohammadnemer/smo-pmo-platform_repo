import { useTranslation } from 'react-i18next';
import { Badge } from '../../shared/ui';
import { RAG_KEY } from './rag';
import type { RagCounts } from './api/scorecardTypes';
import './RagSummary.css';

/**
 * The real, already-computed KPI RAG mix for a perspective/objective/strategy — never a
 * fabricated roll-up badge (docs/sessions/F4.md). Shared between the scorecard (F4) and the
 * strategy map (F5).
 */
export function RagSummary({ counts }: { counts: RagCounts }) {
  const { t } = useTranslation();
  return (
    <div className="smo-rag-summary">
      {counts.green > 0 && <Badge tone="ok">{counts.green} {t(RAG_KEY[3])}</Badge>}
      {counts.amber > 0 && <Badge tone="warn">{counts.amber} {t(RAG_KEY[2])}</Badge>}
      {counts.red > 0 && <Badge tone="risk">{counts.red} {t(RAG_KEY[1])}</Badge>}
      {counts.notSet > 0 && <Badge tone="neutral">{counts.notSet} {t(RAG_KEY[0])}</Badge>}
    </div>
  );
}
