import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader } from '../../../shared/ui';
import { useCreatePortfolio, usePortfoliosQuery, useProgramsQuery } from '../api/usePmoQueries';
import './PmoPages.css';

export function PortfoliosPage() {
  const { t } = useTranslation();
  const portfolios = usePortfoliosQuery();
  const programs = useProgramsQuery();
  const createPortfolio = useCreatePortfolio();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const programCounts = new Map<string, number>();
  for (const program of programs.data ?? []) {
    programCounts.set(program.portfolioId, (programCounts.get(program.portfolioId) ?? 0) + 1);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!name.trim()) {
      return;
    }

    createPortfolio.mutate(
      { name: name.trim(), description: description.trim() || null },
      {
        onSuccess: () => {
          setName('');
          setDescription('');
        },
      },
    );
  }

  return (
    <section className="pmo-page">
      <h1>{t('pages.portfolios.title')}</h1>

      <Card>
        <CardBody>
          {portfolios.isLoading && <p>{t('pages.portfolios.loading')}</p>}
          {portfolios.isError && <Badge tone="risk">{t('pages.portfolios.error')}</Badge>}
          {portfolios.isSuccess && portfolios.data.length === 0 && <p>{t('pages.portfolios.empty')}</p>}
          {portfolios.isSuccess && portfolios.data.length > 0 && (
            <div className="pmo-table-wrap">
              <table className="pmo-table">
                <thead>
                  <tr>
                    <th scope="col">{t('pages.portfolios.columns.name')}</th>
                    <th scope="col">{t('pages.portfolios.columns.description')}</th>
                    <th scope="col">{t('pages.portfolios.columns.programs')}</th>
                  </tr>
                </thead>
                <tbody>
                  {portfolios.data.map((portfolio) => (
                    <tr key={portfolio.id}>
                      <th scope="row">
                        <Link to={`/pmo/programs?portfolioId=${portfolio.id}`}>{portfolio.name}</Link>
                      </th>
                      <td>{portfolio.description ?? '—'}</td>
                      <td>{programCounts.get(portfolio.id) ?? 0}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader>
          <h2>{t('pages.portfolios.form.title')}</h2>
        </CardHeader>
        <CardBody>
          <form className="pmo-form" onSubmit={handleSubmit}>
            <label>
              {t('pages.portfolios.form.name')}
              <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={200} />
            </label>
            <label>
              {t('pages.portfolios.form.description')}
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <Button type="submit" variant="primary" disabled={createPortfolio.isPending}>
              {t(createPortfolio.isPending ? 'pages.portfolios.form.creating' : 'pages.portfolios.form.create')}
            </Button>
            {createPortfolio.isError && <Badge tone="risk">{t('pages.portfolios.form.error')}</Badge>}
          </form>
        </CardBody>
      </Card>
    </section>
  );
}
