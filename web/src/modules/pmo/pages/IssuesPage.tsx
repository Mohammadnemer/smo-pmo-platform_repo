import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Badge, Button, Card, CardBody, CardHeader } from '../../../shared/ui';
import { RAID_SEVERITIES, type RaidItemResponse } from '../api/pmoTypes';
import {
  useCreateRaidItem,
  usePortfoliosQuery,
  useProgramsQuery,
  useProjectsQuery,
  useRaidQuery,
} from '../api/usePmoQueries';
import './PmoPages.css';
import './IssuesPage.css';

type BadgeTone = 'ok' | 'warn' | 'risk' | 'neutral' | 'brand';

// The PMO-only issue log. There is no `Issue` entity and no `/pmo/issues` endpoint: an issue
// is a RAID item whose Category is "Issue" (PmoValidation.cs's fixed vocabulary), so this
// page reads the tenant-wide RAID list and narrows it here. That keeps it a UI-only surface
// — no new module wiring — and keeps one write path for RAID (POST /pmo/raid) shared with the
// project workspace's board.
const ISSUE_CATEGORY = 'Issue';

const SEVERITY_TONE: Record<string, BadgeTone> = { Low: 'neutral', Medium: 'warn', High: 'risk', Critical: 'risk' };
const SEVERITY_KEY: Record<string, string> = {
  Low: 'pages.projectWorkspace.raid.severities.low',
  Medium: 'pages.projectWorkspace.raid.severities.medium',
  High: 'pages.projectWorkspace.raid.severities.high',
  Critical: 'pages.projectWorkspace.raid.severities.critical',
};

// RaidItem.Status is a free string server-side (PmoValidation.cs validates Category/Severity
// only), so "closed" is matched by value rather than by an enum we do not have — anything
// else counts as still open, which is the safe direction for an issue log.
function isClosed(status: string): boolean {
  return status.toLowerCase() === 'closed';
}

function statusTone(status: string): BadgeTone {
  if (isClosed(status)) return 'ok';
  return status === 'Mitigating' ? 'brand' : 'warn';
}

// Same reason: with no server-side vocabulary there is nothing to translate exhaustively, so
// the values the demo seed and the RAID forms actually write get a label and anything else
// falls through to the raw string rather than showing a missing-key placeholder.
const STATUS_KEY: Record<string, string> = {
  Open: 'pages.issues.statuses.open',
  Mitigating: 'pages.issues.statuses.mitigating',
  Closed: 'pages.issues.statuses.closed',
};

// Severity order drives the sort — the API returns RAID items by title, which is the wrong
// first read for an issue log: the worst open issue should be at the top.
const SEVERITY_RANK: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

function severityRank(severity: string): number {
  return SEVERITY_RANK[severity] ?? SEVERITY_RANK.Low + 1;
}

function todayIso(): string {
  const now = new Date();
  const month = `${now.getMonth() + 1}`.padStart(2, '0');
  const day = `${now.getDate()}`.padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

// DueDate is a DateOnly serialised as yyyy-MM-dd, so a plain string compare is the whole
// comparison — no timezone shifting from parsing it into a Date first.
function isOverdue(item: RaidItemResponse, today: string): boolean {
  return !isClosed(item.status) && item.dueDate !== null && item.dueDate < today;
}

export function IssuesPage() {
  const { t } = useTranslation();

  const raid = useRaidQuery({});
  const projects = useProjectsQuery();
  const programs = useProgramsQuery();
  const portfolios = usePortfoliosQuery();
  const createIssue = useCreateRaidItem();

  const [severityFilter, setSeverityFilter] = useState('');
  const [openOnly, setOpenOnly] = useState(true);
  const [projectFilter, setProjectFilter] = useState('');

  const today = todayIso();

  const issues = useMemo(() => {
    const all = (raid.data ?? []).filter((item) => item.category === ISSUE_CATEGORY);
    return all
      .filter((item) => (severityFilter ? item.severity === severityFilter : true))
      .filter((item) => (openOnly ? !isClosed(item.status) : true))
      .filter((item) => (projectFilter ? item.projectId === projectFilter : true))
      .sort((a, b) => severityRank(a.severity) - severityRank(b.severity) || a.title.localeCompare(b.title));
  }, [raid.data, severityFilter, openOnly, projectFilter]);

  const counts = useMemo(() => {
    const all = (raid.data ?? []).filter((item) => item.category === ISSUE_CATEGORY);
    const open = all.filter((item) => !isClosed(item.status));
    return {
      open: open.length,
      critical: open.filter((item) => item.severity === 'Critical' || item.severity === 'High').length,
      overdue: open.filter((item) => isOverdue(item, today)).length,
    };
  }, [raid.data, today]);

  // An issue can hang off a portfolio, a program or a project (RaidItem carries all three
  // nullable owners), so the scope cell resolves whichever one is set rather than assuming
  // every issue is a project issue.
  function scopeCell(item: RaidItemResponse) {
    if (item.projectId) {
      const project = projects.data?.find((candidate) => candidate.id === item.projectId);
      return <Link to={`/pmo/projects/${item.projectId}`}>{project?.name ?? item.projectId}</Link>;
    }
    if (item.programId) {
      const program = programs.data?.find((candidate) => candidate.id === item.programId);
      return <Link to={`/pmo/home?programId=${item.programId}`}>{program?.name ?? item.programId}</Link>;
    }
    if (item.portfolioId) {
      const portfolio = portfolios.data?.find((candidate) => candidate.id === item.portfolioId);
      return <Link to={`/pmo/programs?portfolioId=${item.portfolioId}`}>{portfolio?.name ?? item.portfolioId}</Link>;
    }
    return t('pages.issues.unscoped');
  }

  const [title, setTitle] = useState('');
  const [severity, setSeverity] = useState<string>(RAID_SEVERITIES[1]);
  const [projectId, setProjectId] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [description, setDescription] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!title.trim() || !projectId) {
      return;
    }

    createIssue.mutate(
      {
        title: title.trim(),
        description: description.trim() || null,
        category: ISSUE_CATEGORY,
        severity,
        status: 'Open',
        ownerUserId: null,
        dueDate: dueDate || null,
        portfolioId: null,
        programId: null,
        projectId,
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

  return (
    <section className="pmo-page">
      <h1>{t('pages.issues.title')}</h1>
      <p className="issues-page__lead">{t('pages.issues.lead')}</p>

      <div className="issues-summary">
        <Card>
          <CardBody>
            <span className="issues-summary__label">{t('pages.issues.summary.open')}</span>
            <strong className="issues-summary__value">{counts.open}</strong>
          </CardBody>
        </Card>
        <Card>
          <CardBody>
            <span className="issues-summary__label">{t('pages.issues.summary.highSeverity')}</span>
            <strong className="issues-summary__value">{counts.critical}</strong>
          </CardBody>
        </Card>
        <Card>
          <CardBody>
            <span className="issues-summary__label">{t('pages.issues.summary.overdue')}</span>
            <strong className="issues-summary__value">{counts.overdue}</strong>
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <h2>{t('pages.issues.log.title')}</h2>
        </CardHeader>
        <CardBody>
          <div className="issues-filters">
            <label>
              {t('pages.issues.filters.severity')}
              <select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value)}>
                <option value="">{t('pages.issues.filters.allSeverities')}</option>
                {RAID_SEVERITIES.map((option) => (
                  <option key={option} value={option}>
                    {t(SEVERITY_KEY[option])}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.issues.filters.project')}
              <select value={projectFilter} onChange={(event) => setProjectFilter(event.target.value)}>
                <option value="">{t('pages.issues.filters.allProjects')}</option>
                {projects.data?.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="issues-filters__check">
              <input type="checkbox" checked={openOnly} onChange={(event) => setOpenOnly(event.target.checked)} />
              {t('pages.issues.filters.openOnly')}
            </label>
          </div>

          {raid.isLoading && <p>{t('pages.issues.loading')}</p>}
          {raid.isError && <Badge tone="risk">{t('pages.issues.error')}</Badge>}
          {raid.isSuccess && issues.length === 0 && <p>{t('pages.issues.empty')}</p>}
          {raid.isSuccess && issues.length > 0 && (
            <div className="pmo-table-wrap">
              <table className="pmo-table">
                <thead>
                  <tr>
                    <th scope="col">{t('pages.issues.columns.title')}</th>
                    <th scope="col">{t('pages.issues.columns.scope')}</th>
                    <th scope="col">{t('pages.issues.columns.severity')}</th>
                    <th scope="col">{t('pages.issues.columns.status')}</th>
                    <th scope="col">{t('pages.issues.columns.due')}</th>
                  </tr>
                </thead>
                <tbody>
                  {issues.map((item) => (
                    <tr key={item.id}>
                      <th scope="row">
                        <span className="issues-table__title">{item.title}</span>
                        {item.description && <span className="issues-table__description">{item.description}</span>}
                      </th>
                      <td>{scopeCell(item)}</td>
                      <td>
                        <Badge tone={SEVERITY_TONE[item.severity] ?? 'neutral'}>
                          {t(SEVERITY_KEY[item.severity] ?? item.severity)}
                        </Badge>
                      </td>
                      <td>
                        <Badge tone={statusTone(item.status)}>
                          {STATUS_KEY[item.status] ? t(STATUS_KEY[item.status]) : item.status}
                        </Badge>
                      </td>
                      <td>
                        {item.dueDate ? (
                          <span className={isOverdue(item, today) ? 'issues-table__due is-overdue' : undefined}>
                            {item.dueDate}
                            {isOverdue(item, today) && <> ({t('pages.issues.overdue')})</>}
                          </span>
                        ) : (
                          '—'
                        )}
                      </td>
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
          <h2>{t('pages.issues.form.title')}</h2>
        </CardHeader>
        <CardBody>
          <form className="pmo-form" onSubmit={handleSubmit}>
            <label>
              {t('pages.issues.form.project')}
              <select value={projectId} onChange={(event) => setProjectId(event.target.value)} required>
                <option value="" disabled>
                  {t('pages.issues.form.selectProject')}
                </option>
                {projects.data?.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.issues.form.issueTitle')}
              <input value={title} onChange={(event) => setTitle(event.target.value)} required maxLength={200} />
            </label>
            <label>
              {t('pages.issues.form.severity')}
              <select value={severity} onChange={(event) => setSeverity(event.target.value)}>
                {RAID_SEVERITIES.map((option) => (
                  <option key={option} value={option}>
                    {t(SEVERITY_KEY[option])}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('pages.issues.form.dueDate')}
              <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
            </label>
            <label>
              {t('pages.issues.form.description')}
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <Button type="submit" variant="primary" disabled={createIssue.isPending}>
              {t(createIssue.isPending ? 'pages.issues.form.creating' : 'pages.issues.form.create')}
            </Button>
            {createIssue.isError && <Badge tone="risk">{t('pages.issues.form.error')}</Badge>}
          </form>
        </CardBody>
      </Card>
    </section>
  );
}
