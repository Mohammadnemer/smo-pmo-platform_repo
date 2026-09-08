import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function StrategyPage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.strategy.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.strategy.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
