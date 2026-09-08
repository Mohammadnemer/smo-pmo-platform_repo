import { useMsal } from '@azure/msal-react';
import type { ComponentType, CSSProperties, PropsWithChildren, SVGProps } from 'react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, useLocation } from 'react-router-dom';
import { useBranding } from '../branding/useBranding';
import { supportedLanguages } from '../i18n/i18n';
import { useTheme } from '../theme/useTheme';
import './AppShell.css';
import {
  AlertTriangleIcon,
  BellIcon,
  BreadcrumbSepIcon,
  ChevronIcon,
  FlagIcon,
  FolderIcon,
  GaugeIcon,
  GridIcon,
  InboxIcon,
  LayersIcon,
  ListIcon,
  MapIcon,
  MoonIcon,
  PresentationIcon,
  SearchIcon,
  SunIcon,
  TargetIcon,
  TrendIcon,
  WorkflowIcon,
} from './icons';

// The branded, RTL-mirrored shell from the design prototype (F2 scope), extended with the
// prototype's workspace switcher (F3 addendum — see docs/sessions/F3.md). The left nav is
// NOT one flat list: it fully swaps per workspace, same as the prototype (its PMO nav has
// Portfolios/Programs/Projects as peers, not nested under one another). Each workspace's
// own overview page is mounted at `/<prefix>/home` but keeps its domain-specific nav label
// (Strategy/Projects/Dashboard) rather than a literal "Home" — there is no standalone
// global "Home" row in the left nav (removed: with every workspace having its own landing
// page, a separate app-level Home row was redundant clutter); the brand mark/name in the
// top bar links to "/" instead. The cross-cutting items (Workflow/Rollup/Notifications,
// under `/settings/*`) stay outside any workspace and are always visible.
type WorkspaceId = 'smo' | 'pmo' | 'executive';

interface NavEntry {
  to: string;
  key: string;
  end?: boolean;
  Icon: ComponentType<SVGProps<SVGSVGElement>>;
  flip?: boolean;
}

const workspaces: { id: WorkspaceId; prefix: string; to: string; key: string }[] = [
  { id: 'smo', prefix: '/smo', to: '/smo/home', key: 'workspace.smo' },
  { id: 'pmo', prefix: '/pmo', to: '/pmo/home', key: 'workspace.pmo' },
  { id: 'executive', prefix: '/executive', to: '/executive/home', key: 'workspace.executive' },
];

// First entry in each list is that workspace's overview, mounted at `/<prefix>/home`.
const workspaceNavItems: Record<WorkspaceId, NavEntry[]> = {
  smo: [
    { to: '/smo/home', key: 'nav.strategyOverview', end: true, Icon: TargetIcon },
    { to: '/smo/initiatives', key: 'nav.initiatives', Icon: FlagIcon },
    { to: '/smo/map', key: 'nav.strategyMap', Icon: MapIcon },
    { to: '/smo/scorecard', key: 'nav.scorecard', Icon: GaugeIcon },
    { to: '/smo/alignment', key: 'nav.alignmentGrid', Icon: GridIcon },
  ],
  pmo: [
    { to: '/pmo/home', key: 'nav.projectsOverview', end: true, Icon: FolderIcon },
    { to: '/pmo/portfolios', key: 'nav.portfolios', Icon: LayersIcon },
    { to: '/pmo/programs', key: 'nav.programs', Icon: ListIcon },
    { to: '/pmo/risk', key: 'nav.risk', Icon: AlertTriangleIcon },
  ],
  executive: [{ to: '/executive/home', key: 'nav.executiveDashboard', end: true, Icon: PresentationIcon }],
};

const trailingItems: NavEntry[] = [
  { to: '/settings/workflow', key: 'nav.workflow', Icon: WorkflowIcon, flip: true },
  { to: '/settings/rollup', key: 'nav.rollup', Icon: TrendIcon },
  { to: '/settings/notifications', key: 'nav.notifications', Icon: InboxIcon },
];

function matchWorkspace(pathname: string): WorkspaceId | undefined {
  return workspaces.find((ws) => pathname === ws.prefix || pathname.startsWith(`${ws.prefix}/`))?.id;
}

function isEntryActive(pathname: string, entry: NavEntry): boolean {
  return entry.end ? pathname === entry.to : pathname === entry.to || pathname.startsWith(`${entry.to}/`);
}

function initials(name?: string) {
  if (!name) return '?';
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}

export function AppShell({ children }: PropsWithChildren) {
  const { instance, accounts } = useMsal();
  const account = accounts[0];
  const { t, i18n } = useTranslation();
  const { theme, setTheme } = useTheme();
  const branding = useBranding();
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();

  const activeWorkspaceId = matchWorkspace(location.pathname);
  const currentWorkspaceItems = activeWorkspaceId ? workspaceNavItems[activeWorkspaceId] : [];
  const allEntries = [...currentWorkspaceItems, ...trailingItems];
  const activeEntry = allEntries.find((entry) => isEntryActive(location.pathname, entry));

  return (
    <div className="app-shell">
      <header className="app-shell__topbar">
        <NavLink to="/" className="app-shell__brand" aria-label={t('nav.home')}>
          {branding.logoUrl ? (
            <img className="app-shell__brand-mark app-shell__brand-mark--logo" src={branding.logoUrl} alt="" />
          ) : (
            <span className="app-shell__brand-mark" aria-hidden="true">
              {branding.logoMark}
            </span>
          )}
          <span className="app-shell__brand-name">{branding.displayName}</span>
        </NavLink>

        <div className="app-shell__workspace-switch" role="group" aria-label={t('topbar.workspace')}>
          {workspaces.map((ws) => (
            <NavLink key={ws.id} to={ws.to} className={ws.id === activeWorkspaceId ? 'is-active' : undefined}>
              {t(ws.key)}
            </NavLink>
          ))}
        </div>

        <div className="app-shell__search">
          <SearchIcon className="app-shell__search-icon" aria-hidden="true" />
          <input type="text" placeholder={t('topbar.searchPlaceholder')} aria-label={t('topbar.searchPlaceholder')} />
        </div>

        <div className="app-shell__topbar-actions">
          <button type="button" className="app-shell__icon-button" aria-label={t('topbar.notifications')}>
            <BellIcon aria-hidden="true" />
          </button>

          <button
            type="button"
            className="app-shell__icon-button"
            aria-label={t(theme === 'dark' ? 'topbar.lightTheme' : 'topbar.darkTheme')}
            onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
          >
            {theme === 'dark' ? <SunIcon aria-hidden="true" /> : <MoonIcon aria-hidden="true" />}
          </button>

          <div className="app-shell__lang-switch" role="group" aria-label={t('topbar.language')}>
            {supportedLanguages.map((lng) => (
              <button
                key={lng}
                type="button"
                className={i18n.language === lng ? 'is-active' : undefined}
                aria-pressed={i18n.language === lng}
                onClick={() => void i18n.changeLanguage(lng)}
              >
                {lng.toUpperCase()}
              </button>
            ))}
          </div>

          <div className="app-shell__user">
            <span className="app-shell__avatar" aria-hidden="true">
              {initials(account?.name ?? account?.username)}
            </span>
            <span className="app-shell__user-meta">
              <span className="app-shell__user-name">{account?.name ?? account?.username ?? t('topbar.signedIn')}</span>
              <span className="app-shell__user-role">{t('topbar.role')}</span>
            </span>
            <button type="button" className="app-shell__sign-out" onClick={() => instance.logoutRedirect()}>
              {t('topbar.signOut')}
            </button>
          </div>
        </div>
      </header>

      <div className="app-shell__body">
        <nav className={`app-shell__nav${collapsed ? ' is-collapsed' : ''}`} aria-label={t('app.name')}>
          <button
            type="button"
            className="app-shell__nav-toggle"
            onClick={() => setCollapsed((value) => !value)}
            aria-label={t(collapsed ? 'shell.expandNav' : 'shell.collapseNav')}
          >
            <ChevronIcon
              className="nav-collapse-icon"
              aria-hidden="true"
              style={{ '--flip': collapsed ? -1 : 1 } as CSSProperties}
            />
          </button>

          {currentWorkspaceItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `app-shell__nav-item${isActive ? ' is-active' : ''}`}
            >
              <item.Icon className={item.flip ? 'icon-flip-rtl' : undefined} aria-hidden="true" />
              <span className="app-shell__nav-label">{t(item.key)}</span>
            </NavLink>
          ))}

          <div className="app-shell__nav-trailing">
            {trailingItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) => `app-shell__nav-item${isActive ? ' is-active' : ''}`}
              >
                <item.Icon className={item.flip ? 'icon-flip-rtl' : undefined} aria-hidden="true" />
                <span className="app-shell__nav-label">{t(item.key)}</span>
              </NavLink>
            ))}
          </div>
        </nav>

        <main className="app-shell__main">
          <div className="app-shell__breadcrumb">
            <span>{t('nav.home')}</span>
            {activeEntry && (
              <>
                <BreadcrumbSepIcon className="app-shell__breadcrumb-sep icon-flip-rtl" aria-hidden="true" />
                <span className="is-current">{t(activeEntry.key)}</span>
              </>
            )}
          </div>
          {children}
        </main>
      </div>
    </div>
  );
}
