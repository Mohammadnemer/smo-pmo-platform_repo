import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { HomePage } from '../modules/platform/pages/HomePage';
import { NotificationsPage } from '../modules/notifications/pages/NotificationsPage';
import { IssuesPage } from '../modules/pmo/pages/IssuesPage';
import { PortfoliosPage } from '../modules/pmo/pages/PortfoliosPage';
import { ProgramsPage } from '../modules/pmo/pages/ProgramsPage';
import { ProjectsPage } from '../modules/pmo/pages/ProjectsPage';
import { ProjectWorkspacePage } from '../modules/pmo/pages/ProjectWorkspacePage';
import { RiskPage } from '../modules/pmo/pages/RiskPage';
import { ExecutivePage } from '../modules/rollup/pages/ExecutivePage';
import { RollupPage } from '../modules/rollup/pages/RollupPage';
import { AlignmentGridPage } from '../modules/smo/pages/AlignmentGridPage';
import { InitiativesPage } from '../modules/smo/pages/InitiativesPage';
import { ScorecardPage } from '../modules/smo/pages/ScorecardPage';
import { StrategyMapPage } from '../modules/smo/pages/StrategyMapPage';
import { StrategyPage } from '../modules/smo/pages/StrategyPage';
import { WorkflowPage } from '../modules/workflow/pages/WorkflowPage';
import { AppShell } from '../shared/layout/AppShell';

export function AppRouter() {
  return (
    <BrowserRouter>
      <ProtectedRoute>
        <AppShell>
          <Routes>
            <Route path="/" element={<HomePage />} />

            <Route path="/smo" element={<Navigate to="/smo/home" replace />} />
            <Route path="/smo/home" element={<StrategyPage />} />
            <Route path="/smo/initiatives" element={<InitiativesPage />} />
            <Route path="/smo/map" element={<StrategyMapPage />} />
            <Route path="/smo/scorecard" element={<ScorecardPage />} />
            <Route path="/smo/alignment" element={<AlignmentGridPage />} />

            <Route path="/pmo" element={<Navigate to="/pmo/home" replace />} />
            <Route path="/pmo/home" element={<ProjectsPage />} />
            <Route path="/pmo/portfolios" element={<PortfoliosPage />} />
            <Route path="/pmo/programs" element={<ProgramsPage />} />
            <Route path="/pmo/risk" element={<RiskPage />} />
            <Route path="/pmo/issues" element={<IssuesPage />} />
            <Route path="/pmo/projects/:id" element={<ProjectWorkspacePage />} />

            <Route path="/executive" element={<Navigate to="/executive/home" replace />} />
            <Route path="/executive/home" element={<ExecutivePage />} />

            <Route path="/settings/workflow" element={<WorkflowPage />} />
            <Route path="/settings/rollup" element={<RollupPage />} />
            <Route path="/settings/notifications" element={<NotificationsPage />} />

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AppShell>
      </ProtectedRoute>
    </BrowserRouter>
  );
}
