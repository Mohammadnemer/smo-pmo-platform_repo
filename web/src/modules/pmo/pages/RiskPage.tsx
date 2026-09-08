import { useTranslation } from 'react-i18next';
import { Card, CardBody, CardHeader } from '../../../shared/ui';
import './PmoPages.css';

// UI-only shell for the PMO "Risk" nav entry — no API calls and no PMO module wiring yet.
// The RAID board on the project workspace stays the source of risk data until this page is
// given its own query; this is deliberately just the placeholder surface.
export function RiskPage() {
  const { t } = useTranslation();

  return (
    <section className="pmo-page">
      <h1>{t('pages.risk.title')}</h1>

      <Card>
        <CardHeader>
          <h2>{t('pages.risk.register.title')}</h2>
        </CardHeader>
        <CardBody>
          <p>{t('pages.risk.register.empty')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
