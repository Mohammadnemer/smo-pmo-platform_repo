import { useTranslation } from 'react-i18next';
import { localized } from '../../../shared/i18n/localized';
import { Badge, Card, CardBody, CardHeader, Sparkline } from '../../../shared/ui';
import { RagSummary } from '../RagSummary';
import { RAG_KEY, RAG_TONE, formatValue } from '../rag';
import { useScorecardQuery, useStrategiesQuery } from '../api/useScorecardQuery';
import type { KpiResponse, ScorecardKpi, ScorecardObjective } from '../api/scorecardTypes';
import './ScorecardPage.css';

function KpiRow({ scorecardKpi, language }: { scorecardKpi: ScorecardKpi; language: string }) {
  const { t } = useTranslation();
  const kpi: KpiResponse = scorecardKpi.kpi;
  const name = localized(kpi.name, kpi.nameAr, language);
  const unit = kpi.unit ? ` ${kpi.unit}` : '';

  return (
    <tr>
      <th scope="row">{name}</th>
      <td>{kpi.baseline === null ? '—' : `${formatValue(kpi.baseline)}${unit}`}</td>
      <td>{kpi.target === null ? '—' : `${formatValue(kpi.target)}${unit}`}</td>
      <td>{kpi.actual === null ? '—' : `${formatValue(kpi.actual)}${unit}`}</td>
      <td>{kpi.achievementPercent === null ? '—' : `${formatValue(kpi.achievementPercent)}%`}</td>
      <td>
        <Badge tone={RAG_TONE[kpi.rag]}>{t(RAG_KEY[kpi.rag])}</Badge>
      </td>
      <td>
        <Sparkline
          values={scorecardKpi.trend.map((point) => point.value)}
          tone={RAG_TONE[kpi.rag]}
          ariaLabel={t('pages.scorecard.trendFor', { name })}
        />
      </td>
    </tr>
  );
}

function ObjectiveCard({ objective, language }: { objective: ScorecardObjective; language: string }) {
  const { t } = useTranslation();
  const name = localized(objective.objective.name, objective.objective.nameAr, language);

  return (
    <Card className="scorecard-page__objective">
      <CardHeader>
        <div className="scorecard-page__objective-heading">
          <h3>{name}</h3>
          <RagSummary counts={objective.kpiRagCounts} />
        </div>
        {objective.objective.targetState && (
          <p className="scorecard-page__target-state">{objective.objective.targetState}</p>
        )}
      </CardHeader>
      <CardBody>
        <div className="scorecard-page__kpi-table-wrap">
          <table className="scorecard-page__kpi-table">
            <thead>
              <tr>
                <th scope="col">{t('pages.scorecard.columns.kpi')}</th>
                <th scope="col">{t('pages.scorecard.columns.baseline')}</th>
                <th scope="col">{t('pages.scorecard.columns.target')}</th>
                <th scope="col">{t('pages.scorecard.columns.actual')}</th>
                <th scope="col">{t('pages.scorecard.columns.achievement')}</th>
                <th scope="col">{t('pages.scorecard.columns.rag')}</th>
                <th scope="col">{t('pages.scorecard.columns.trend')}</th>
              </tr>
            </thead>
            <tbody>
              {objective.kpis.map((scorecardKpi) => (
                <KpiRow key={scorecardKpi.kpi.id} scorecardKpi={scorecardKpi} language={language} />
              ))}
            </tbody>
          </table>
        </div>

        {objective.initiatives.length > 0 && (
          <ul className="scorecard-page__initiatives">
            {objective.initiatives.map((initiative) => (
              <li key={initiative.id}>
                <span className="scorecard-page__initiative-name">
                  {localized(initiative.name, initiative.nameAr, language)}
                </span>
                {initiative.sponsor && (
                  <span className="scorecard-page__initiative-meta"> · {initiative.sponsor}</span>
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

export function ScorecardPage() {
  const { t, i18n } = useTranslation();
  const strategies = useStrategiesQuery();
  const strategyId = strategies.data?.[0]?.id;
  const scorecard = useScorecardQuery(strategyId);

  const isLoading = strategies.isLoading || (strategyId !== undefined && scorecard.isLoading);
  const isError = strategies.isError || scorecard.isError;
  const isEmpty = strategies.isSuccess && strategies.data.length === 0;

  if (isLoading) {
    return (
      <section>
        <h1>{t('pages.scorecard.title')}</h1>
        <Card>
          <CardBody>
            <p>{t('pages.scorecard.loading')}</p>
          </CardBody>
        </Card>
      </section>
    );
  }

  if (isError || isEmpty || !scorecard.data) {
    return (
      <section>
        <h1>{t('pages.scorecard.title')}</h1>
        <Card>
          <CardBody>
            <Badge tone={isEmpty ? 'neutral' : 'risk'}>
              {t(isEmpty ? 'pages.scorecard.empty' : 'pages.scorecard.error')}
            </Badge>
          </CardBody>
        </Card>
      </section>
    );
  }

  const data = scorecard.data;
  const language = i18n.language;
  const strategyName = localized(data.strategy.name, data.strategy.nameAr, language);
  // Vision/Mission have no Arabic twin in the contract (StrategyResponse) — unlike Name,
  // they're free-form prose rather than a label, so there's nothing to pick between here.
  const strategyCopy = [data.strategy.vision, data.strategy.mission].filter(Boolean).join(' — ');

  return (
    <section className="scorecard-page">
      <h1>{t('pages.scorecard.title')}</h1>

      <Card className="scorecard-page__strategy">
        <CardHeader>
          <div className="scorecard-page__strategy-heading">
            <h2>{strategyName}</h2>
            <RagSummary counts={data.kpiRagCounts} />
          </div>
        </CardHeader>
        <CardBody>
          {strategyCopy && <p className="scorecard-page__strategy-copy">{strategyCopy}</p>}
          {data.strategy.horizonStart && data.strategy.horizonEnd && (
            <p className="scorecard-page__horizon">
              {t('pages.scorecard.horizon', {
                start: data.strategy.horizonStart,
                end: data.strategy.horizonEnd,
              })}
            </p>
          )}
        </CardBody>
      </Card>

      {data.perspectives.map((perspective) => (
        <section key={perspective.perspective.id} className="scorecard-page__perspective">
          <div className="scorecard-page__perspective-heading">
            <h2>{localized(perspective.perspective.name, perspective.perspective.nameAr, language)}</h2>
            <RagSummary counts={perspective.kpiRagCounts} />
          </div>

          {perspective.objectives.map((objective) => (
            <ObjectiveCard key={objective.objective.id} objective={objective} language={language} />
          ))}
        </section>
      ))}
    </section>
  );
}
