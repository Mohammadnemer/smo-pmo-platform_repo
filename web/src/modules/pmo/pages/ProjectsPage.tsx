import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader } from '../../../shared/ui';
import { PROJECT_STATUSES } from '../api/pmoTypes';
import { useCreateProject, useProgramsQuery, useProjectsQuery } from '../api/usePmoQueries';
import './PmoPages.css';

type BadgeTone = 'ok' | 'warn' | 'risk' | 'neutral' | 'brand';

// Project.Status is a free string B7's fixed flow will eventually own (PmoEntities.cs) — this
// is a display convenience for the known MVP values, not a contract; anything else falls
// back to neutral rather than guessing.
function statusTone(status: string): BadgeTone {
  switch (status) {
    case 'Active':
      return 'ok';
    case 'OnHold':
      return 'warn';
    case 'Cancelled':
      return 'risk';
    case 'Completed':
      return 'brand';
    default:
      return 'neutral';
  }
}

export function ProjectsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const programId = searchParams.get('programId') ?? undefined;

  const programs = useProgramsQuery();
  const projects = useProjectsQuery(programId);
  const createProject = useCreateProject();

  const programName = programs.data?.find((program) => program.id === programId)?.name;

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedProgramId, setSelectedProgramId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [status, setStatus] = useState<string>(PROJECT_STATUSES[0]);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!name.trim() || !selectedProgramId) {
      return;
    }

    createProject.mutate(
      {
        programId: selectedProgramId,
        name: name.trim(),
        description: description.trim() || null,
        startDate: startDate || null,
        status,
      },
      {
        onSuccess: () => {
          setName('');
          setDescription('');
          setStartDate('');
          setStatus(PROJECT_STATUSES[0]);
        },
      },
    );
  }

  return (
    <section className="pmo-page">
      <h1>{t('pages.projects.title')}</h1>

      {programId && (
        <p className="pmo-filter-banner">
          <span>{t('pages.projects.filteredBy', { name: programName ?? programId })}</span>
          <button type="button" className="pmo-filter-clear" onClick={() => setSearchParams({})}>
            {t('pages.projects.clearFilter')}
          </button>
        </p>
      )}

      <Card>
        <CardBody>
          {projects.isLoading && <p>{t('pages.projects.loading')}</p>}
          {projects.isError && <Badge tone="risk">{t('pages.projects.error')}</Badge>}
          {projects.isSuccess && projects.data.length === 0 && <p>{t('pages.projects.empty')}</p>}
          {projects.isSuccess && projects.data.length > 0 && (
            <div className="pmo-table-wrap">
              <table className="pmo-table">
                <thead>
                  <tr>
                    <th scope="col">{t('pages.projects.columns.name')}</th>
                    <th scope="col">{t('pages.projects.columns.program')}</th>
                    <th scope="col">{t('pages.projects.columns.status')}</th>
                    <th scope="col">{t('pages.projects.columns.startDate')}</th>
                  </tr>
                </thead>
                <tbody>
                  {projects.data.map((project) => (
                    <tr key={project.id}>
                      <th scope="row">
                        <Link to={`/pmo/projects/${project.id}`}>{project.name}</Link>
                      </th>
                      <td>{programs.data?.find((program) => program.id === project.programId)?.name ?? '—'}</td>
                      <td>
                        <Badge tone={statusTone(project.status)}>{project.status}</Badge>
                      </td>
                      <td>{project.startDate ?? '—'}</td>
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
          <h2>{t('pages.projects.form.title')}</h2>
        </CardHeader>
        <CardBody>
          <form className="pmo-form" onSubmit={handleSubmit}>
            <label>
              {t('pages.projects.form.program')}
              <select
                value={selectedProgramId}
                onChange={(event) => setSelectedProgramId(event.target.value)}
                required
              >
                <option value="" disabled>
                  {t('pages.projects.form.selectProgram')}
                </option>
                {programs.data?.map((program) => (
                  <option key={program.id} value={program.id}>
                    {program.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.projects.form.name')}
              <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={200} />
            </label>
            <label>
              {t('pages.projects.form.startDate')}
              <input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
            </label>
            <label>
              {t('pages.projects.form.status')}
              <select value={status} onChange={(event) => setStatus(event.target.value)}>
                {PROJECT_STATUSES.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.projects.form.description')}
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <Button type="submit" variant="primary" disabled={createProject.isPending}>
              {t(createProject.isPending ? 'pages.projects.form.creating' : 'pages.projects.form.create')}
            </Button>
            {createProject.isError && <Badge tone="risk">{t('pages.projects.form.error')}</Badge>}
          </form>
        </CardBody>
      </Card>
    </section>
  );
}
