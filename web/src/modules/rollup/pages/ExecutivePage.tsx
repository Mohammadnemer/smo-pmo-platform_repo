import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function ExecutivePage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.executive.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.executive.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
