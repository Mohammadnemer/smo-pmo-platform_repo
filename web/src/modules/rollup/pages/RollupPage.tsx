import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function RollupPage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.rollup.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.rollup.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
