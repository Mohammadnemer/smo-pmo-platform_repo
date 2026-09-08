import { useMsal } from '@azure/msal-react';
import { useTranslation } from 'react-i18next';
import { useHealthQuery } from '../../../shared/api/health';
import { Badge, Card, CardBody } from '../../../shared/ui';

export function HomePage() {
  const { accounts } = useMsal();
  const account = accounts[0];
  const health = useHealthQuery();
  const { t } = useTranslation();

  return (
    <section>
      <h1>{t('pages.home.title')}</h1>
      <p>{t('pages.home.welcome', { name: account?.name ?? account?.username ?? t('topbar.signedIn') })}</p>
      <Card>
        <CardBody>
          <p>
            {t('pages.home.apiLabel')}{' '}
            {health.isLoading && t('pages.home.apiChecking')}
            {health.isError && <Badge tone="risk">{t('pages.home.apiUnreachable')}</Badge>}
            {health.data && (
              <Badge tone="ok">
                {t('pages.home.apiStatus', { status: health.data.status, service: health.data.service })}
              </Badge>
            )}
          </p>
        </CardBody>
      </Card>
    </section>
  );
}
