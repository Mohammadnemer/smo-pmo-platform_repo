import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../shared/ui';

export function WorkflowPage() {
  const { t } = useTranslation();
  return (
    <section>
      <h1>{t('pages.workflow.title')}</h1>
      <Card>
        <CardBody>
          <p>{t('pages.workflow.body')}</p>
        </CardBody>
      </Card>
    </section>
  );
}
