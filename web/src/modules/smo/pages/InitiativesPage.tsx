import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function InitiativesPage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.initiatives.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.initiatives.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
