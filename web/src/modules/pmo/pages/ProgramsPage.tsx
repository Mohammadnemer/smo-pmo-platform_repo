import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader } from '../../../shared/ui';
import { useCreateProgram, usePortfoliosQuery, useProgramsQuery, useProjectsQuery } from '../api/usePmoQueries';
import './PmoPages.css';

export function ProgramsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const portfolioId = searchParams.get('portfolioId') ?? undefined;

  const portfolios = usePortfoliosQuery();
  const programs = useProgramsQuery(portfolioId);
  const projects = useProjectsQuery();
  const createProgram = useCreateProgram();

  const portfolioName = portfolios.data?.find((portfolio) => portfolio.id === portfolioId)?.name;

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedPortfolioId, setSelectedPortfolioId] = useState('');

  const projectCounts = new Map<string, number>();
  for (const project of projects.data ?? []) {
    projectCounts.set(project.programId, (projectCounts.get(project.programId) ?? 0) + 1);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!name.trim() || !selectedPortfolioId) {
      return;
    }

    createProgram.mutate(
      { portfolioId: selectedPortfolioId, name: name.trim(), description: description.trim() || null },
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
      <h1>{t('pages.programs.title')}</h1>

      {portfolioId && (
        <p className="pmo-filter-banner">
          <span>{t('pages.programs.filteredBy', { name: portfolioName ?? portfolioId })}</span>
          <button type="button" className="pmo-filter-clear" onClick={() => setSearchParams({})}>
            {t('pages.programs.clearFilter')}
          </button>
        </p>
      )}

      <Card>
        <CardBody>
          {programs.isLoading && <p>{t('pages.programs.loading')}</p>}
          {programs.isError && <Badge tone="risk">{t('pages.programs.error')}</Badge>}
          {programs.isSuccess && programs.data.length === 0 && <p>{t('pages.programs.empty')}</p>}
          {programs.isSuccess && programs.data.length > 0 && (
            <div className="pmo-table-wrap">
              <table className="pmo-table">
                <thead>
                  <tr>
                    <th scope="col">{t('pages.programs.columns.name')}</th>
                    <th scope="col">{t('pages.programs.columns.portfolio')}</th>
                    <th scope="col">{t('pages.programs.columns.description')}</th>
                    <th scope="col">{t('pages.programs.columns.projects')}</th>
                  </tr>
                </thead>
                <tbody>
                  {programs.data.map((program) => (
                    <tr key={program.id}>
                      <th scope="row">
                        <Link to={`/pmo/home?programId=${program.id}`}>{program.name}</Link>
                      </th>
                      <td>{portfolios.data?.find((portfolio) => portfolio.id === program.portfolioId)?.name ?? '—'}</td>
                      <td>{program.description ?? '—'}</td>
                      <td>{projectCounts.get(program.id) ?? 0}</td>
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
          <h2>{t('pages.programs.form.title')}</h2>
        </CardHeader>
        <CardBody>
          <form className="pmo-form" onSubmit={handleSubmit}>
            <label>
              {t('pages.programs.form.portfolio')}
              <select
                value={selectedPortfolioId}
                onChange={(event) => setSelectedPortfolioId(event.target.value)}
                required
              >
                <option value="" disabled>
                  {t('pages.programs.form.selectPortfolio')}
                </option>
                {portfolios.data?.map((portfolio) => (
                  <option key={portfolio.id} value={portfolio.id}>
                    {portfolio.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.programs.form.name')}
              <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={200} />
            </label>
            <label>
              {t('pages.programs.form.description')}
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <Button type="submit" variant="primary" disabled={createProgram.isPending}>
              {t(createProgram.isPending ? 'pages.programs.form.creating' : 'pages.programs.form.create')}
            </Button>
            {createProgram.isError && <Badge tone="risk">{t('pages.programs.form.error')}</Badge>}
          </form>
        </CardBody>
      </Card>
    </section>
  );
}
