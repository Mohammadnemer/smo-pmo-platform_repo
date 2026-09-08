import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader } from '../../../shared/ui';
import { GanttChart } from '../gantt/GanttChart';
import { generateStressSchedule, readStressTaskCount } from '../gantt/stressData';
import { RAID_CATEGORIES, RAID_SEVERITIES, type TaskResponse, type TaskWriteModel } from '../api/pmoTypes';
import {
  useCreateDependency,
  useCreateRaidItem,
  usePortfolioQuery,
  useProgramQuery,
  useProjectQuery,
  useRaidQuery,
  useScheduleQuery,
  useUpdateTask,
} from '../api/usePmoQueries';
import './PmoPages.css';
import './ProjectWorkspacePage.css';

type BadgeTone = 'ok' | 'warn' | 'risk' | 'neutral' | 'brand';

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

const SEVERITY_TONE: Record<string, BadgeTone> = { Low: 'neutral', Medium: 'warn', High: 'risk', Critical: 'risk' };
const CATEGORY_KEY: Record<string, string> = {
  Risk: 'pages.projectWorkspace.raid.categories.risk',
  Action: 'pages.projectWorkspace.raid.categories.action',
  Issue: 'pages.projectWorkspace.raid.categories.issue',
  Decision: 'pages.projectWorkspace.raid.categories.decision',
};
const SEVERITY_KEY: Record<string, string> = {
  Low: 'pages.projectWorkspace.raid.severities.low',
  Medium: 'pages.projectWorkspace.raid.severities.medium',
  High: 'pages.projectWorkspace.raid.severities.high',
  Critical: 'pages.projectWorkspace.raid.severities.critical',
};

// RaidItem.Status has no fixed vocabulary server-side (PmoValidation.cs validates Category/
// Severity only) — this covers the values the demo seed and this page's own form use, with
// a neutral fallback for anything else rather than guessing at a tone.
function raidStatusTone(status: string): BadgeTone {
  switch (status) {
    case 'Closed':
      return 'ok';
    case 'Mitigating':
      return 'brand';
    default:
      return 'warn';
  }
}

// TaskResponse carries every field TaskWriteModel needs plus the server-computed schedule
// ones (ScheduleStart/Finish/TotalFloatDays/IsCritical) — PUT /tasks/{id} replaces the whole
// write model, so a drag that only changes one field (duration, constraintStart) still has to
// round-trip the rest of the task unchanged.
function toTaskWriteModel(task: TaskResponse): TaskWriteModel {
  return {
    projectId: task.projectId,
    name: task.name,
    description: task.description,
    wbsCode: task.wbsCode,
    durationDays: task.durationDays,
    isMilestone: task.isMilestone,
    percentComplete: task.percentComplete,
    assigneeUserId: task.assigneeUserId,
    constraintStart: task.constraintStart,
  };
}

export function ProjectWorkspacePage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const location = useLocation();

  const project = useProjectQuery(id);
  const program = useProgramQuery(project.data?.programId);
  const portfolio = usePortfolioQuery(program.data?.portfolioId);
  const schedule = useScheduleQuery(id);
  const raid = useRaidQuery({ projectId: id });
  const createRaidItem = useCreateRaidItem();
  const updateTask = useUpdateTask();
  const createDependency = useCreateDependency();

  // Dev-only stress harness for F7's "10k-task project scrolls and renders smoothly" line —
  // `?stress=10000` swaps in a synthetic schedule instead of the API's, never reachable in a
  // production build. See gantt/stressData.ts.
  const stressTaskCount = import.meta.env.DEV ? readStressTaskCount(location.search) : null;
  const stressSchedule = useMemo(
    () => (stressTaskCount ? generateStressSchedule(stressTaskCount, id ?? 'stress') : null),
    [stressTaskCount, id],
  );
  const effectiveSchedule = stressSchedule ?? schedule.data;

  const [title, setTitle] = useState('');
  const [category, setCategory] = useState<string>(RAID_CATEGORIES[0]);
  const [severity, setSeverity] = useState<string>(RAID_SEVERITIES[1]);
  const [description, setDescription] = useState('');
  const [dueDate, setDueDate] = useState('');

  function handleRaidSubmit(event: FormEvent) {
    event.preventDefault();
    if (!title.trim() || !id) {
      return;
    }

    createRaidItem.mutate(
      {
        title: title.trim(),
        description: description.trim() || null,
        category,
        severity,
        status: 'Open',
        ownerUserId: null,
        dueDate: dueDate || null,
        portfolioId: null,
        programId: null,
        projectId: id,
      },
      {
        onSuccess: () => {
          setTitle('');
          setDescription('');
          setDueDate('');
        },
      },
    );
  }

  if (project.isLoading) {
    return (
      <section className="pmo-page">
        <p>{t('pages.projectWorkspace.loading')}</p>
      </section>
    );
  }

  if (project.isError || !project.data) {
    return (
      <section className="pmo-page">
        <Badge tone="risk">{t('pages.projectWorkspace.notFound')}</Badge>
      </section>
    );
  }

  const data = project.data;

  return (
    <section className="project-workspace">
      <nav className="project-workspace__breadcrumb" aria-label={t('pages.projectWorkspace.breadcrumbLabel')}>
        {portfolio.data && (
          <Link to={`/pmo/programs?portfolioId=${portfolio.data.id}`}>{portfolio.data.name}</Link>
        )}
        {program.data && (
          <>
            <span aria-hidden="true">/</span>
            <Link to={`/pmo/home?programId=${program.data.id}`}>{program.data.name}</Link>
          </>
        )}
      </nav>

      <div className="project-workspace__heading">
        <h1>{data.name}</h1>
        <Badge tone={statusTone(data.status)}>{data.status}</Badge>
      </div>
      {data.description && <p className="project-workspace__description">{data.description}</p>}

      <Card>
        <CardHeader>
          <h2>{t('pages.projectWorkspace.schedule.title')}</h2>
        </CardHeader>
        <CardBody>
          {!stressSchedule && schedule.isLoading && <p>{t('pages.projectWorkspace.schedule.loading')}</p>}
          {!stressSchedule && schedule.isError && (
            <Badge tone="risk">{t('pages.projectWorkspace.schedule.error')}</Badge>
          )}
          {effectiveSchedule && effectiveSchedule.tasks.length === 0 && (
            <p>{t('pages.projectWorkspace.schedule.empty')}</p>
          )}
          {effectiveSchedule && effectiveSchedule.tasks.length > 0 && (
            <>
              {effectiveSchedule.projectFinish && (
                <p className="project-workspace__finish">
                  {t('pages.projectWorkspace.schedule.finish', { date: effectiveSchedule.projectFinish })}
                  {stressSchedule &&
                    ` — ${t('pages.projectWorkspace.schedule.gantt.stressLabel', { count: stressSchedule.tasks.length })}`}
                </p>
              )}
              <GanttChart
                tasks={effectiveSchedule.tasks}
                dependencies={effectiveSchedule.dependencies}
                // Disabled for the synthetic stress schedule — it has no real project behind
                // it for a mutation to write to.
                onMoveTask={
                  stressSchedule
                    ? undefined
                    : (task, constraintStart) =>
                        updateTask.mutate({
                          id: task.id,
                          model: { ...toTaskWriteModel(task), constraintStart },
                        })
                }
                onResizeTask={
                  stressSchedule
                    ? undefined
                    : (task, durationDays) =>
                        updateTask.mutate({ id: task.id, model: { ...toTaskWriteModel(task), durationDays } })
                }
                onLinkTasks={
                  stressSchedule
                    ? undefined
                    : (predecessor, successor) =>
                        createDependency.mutate({
                          projectId: predecessor.projectId,
                          predecessorTaskId: predecessor.id,
                          successorTaskId: successor.id,
                          type: 'FS',
                          lagDays: 0,
                        })
                }
              />
              {(updateTask.isError || createDependency.isError) && (
                <Badge tone="risk">{t('pages.projectWorkspace.schedule.gantt.error')}</Badge>
              )}
            </>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader>
          <h2>{t('pages.projectWorkspace.raid.title')}</h2>
        </CardHeader>
        <CardBody>
          {raid.isLoading && <p>{t('pages.projectWorkspace.raid.loading')}</p>}
          {raid.isError && <Badge tone="risk">{t('pages.projectWorkspace.raid.error')}</Badge>}
          {raid.isSuccess && raid.data.length === 0 && <p>{t('pages.projectWorkspace.raid.empty')}</p>}
          {raid.isSuccess && raid.data.length > 0 && (
            <div className="pmo-table-wrap">
              <table className="pmo-table">
                <thead>
                  <tr>
                    <th scope="col">{t('pages.projectWorkspace.raid.columns.title')}</th>
                    <th scope="col">{t('pages.projectWorkspace.raid.columns.category')}</th>
                    <th scope="col">{t('pages.projectWorkspace.raid.columns.severity')}</th>
                    <th scope="col">{t('pages.projectWorkspace.raid.columns.status')}</th>
                    <th scope="col">{t('pages.projectWorkspace.raid.columns.due')}</th>
                  </tr>
                </thead>
                <tbody>
                  {raid.data.map((item) => (
                    <tr key={item.id}>
                      <th scope="row">{item.title}</th>
                      <td>{t(CATEGORY_KEY[item.category] ?? item.category)}</td>
                      <td>
                        <Badge tone={SEVERITY_TONE[item.severity] ?? 'neutral'}>
                          {t(SEVERITY_KEY[item.severity] ?? item.severity)}
                        </Badge>
                      </td>
                      <td>
                        <Badge tone={raidStatusTone(item.status)}>{item.status}</Badge>
                      </td>
                      <td>{item.dueDate ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <form className="pmo-form project-workspace__raid-form" onSubmit={handleRaidSubmit}>
            <label>
              {t('pages.projectWorkspace.raid.form.title')}
              <input value={title} onChange={(event) => setTitle(event.target.value)} required maxLength={200} />
            </label>
            <label>
              {t('pages.projectWorkspace.raid.form.category')}
              <select value={category} onChange={(event) => setCategory(event.target.value)}>
                {RAID_CATEGORIES.map((option) => (
                  <option key={option} value={option}>
                    {t(CATEGORY_KEY[option])}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.projectWorkspace.raid.form.severity')}
              <select value={severity} onChange={(event) => setSeverity(event.target.value)}>
                {RAID_SEVERITIES.map((option) => (
                  <option key={option} value={option}>
                    {t(SEVERITY_KEY[option])}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.projectWorkspace.raid.form.dueDate')}
              <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
            </label>
            <label>
              {t('pages.projectWorkspace.raid.form.description')}
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <Button type="submit" variant="primary" disabled={createRaidItem.isPending}>
              {t(
                createRaidItem.isPending
                  ? 'pages.projectWorkspace.raid.form.creating'
                  : 'pages.projectWorkspace.raid.form.create',
              )}
            </Button>
            {createRaidItem.isError && <Badge tone="risk">{t('pages.projectWorkspace.raid.form.error')}</Badge>}
          </form>
        </CardBody>
      </Card>
    </section>
  );
}
