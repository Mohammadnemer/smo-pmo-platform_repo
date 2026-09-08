import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function NotificationsPage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.notifications.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.notifications.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
