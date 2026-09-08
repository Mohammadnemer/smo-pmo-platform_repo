// Mirrors src/PMO/PmoContracts.cs's read/write models exactly — field names are the
// camelCase System.Text.Json default. Unlike SMO, PMO entities have no bilingual Name/NameAr
// pair yet (PmoEntities.cs), so there is nothing for `localized()` to pick between here.

export interface PortfolioResponse {
  id: string;
  name: string;
  description: string | null;
}

export interface ProgramResponse {
  id: string;
  portfolioId: string;
  name: string;
  description: string | null;
}

export interface ProjectResponse {
  id: string;
  programId: string;
  name: string;
  description: string | null;
  startDate: string | null;
  status: string;
}

export interface TaskResponse {
  id: string;
  projectId: string;
  name: string;
  description: string | null;
  wbsCode: string | null;
  durationDays: number;
  isMilestone: boolean;
  percentComplete: number;
  assigneeUserId: string | null;
  constraintStart: string | null;
  scheduleStart: string | null;
  scheduleFinish: string | null;
  totalFloatDays: number | null;
  isCritical: boolean;
}

export interface DependencyResponse {
  id: string;
  projectId: string;
  predecessorTaskId: string;
  successorTaskId: string;
  type: string;
  lagDays: number;
}

export interface RaidItemResponse {
  id: string;
  title: string;
  description: string | null;
  category: string;
  severity: string;
  status: string;
  ownerUserId: string | null;
  dueDate: string | null;
  portfolioId: string | null;
  programId: string | null;
  projectId: string | null;
}

export interface ScheduleResponse {
  projectId: string;
  calendarAnchor: string;
  projectFinish: string | null;
  tasks: TaskResponse[];
  dependencies: DependencyResponse[];
}

export interface PortfolioWriteModel {
  name: string;
  description: string | null;
}

export interface ProgramWriteModel {
  portfolioId: string;
  name: string;
  description: string | null;
}

export interface ProjectWriteModel {
  programId: string;
  name: string;
  description: string | null;
  startDate: string | null;
  status: string | null;
}

export interface TaskWriteModel {
  projectId: string;
  name: string;
  description: string | null;
  wbsCode: string | null;
  durationDays: number;
  isMilestone: boolean;
  percentComplete: number;
  assigneeUserId: string | null;
  // "Start no earlier than" (F8: dragging a task in the Gantt) — null means purely
  // dependency-driven, matching CriticalPathEngine.cs's TaskInput.ConstraintStartDay.
  constraintStart: string | null;
}

export interface DependencyWriteModel {
  projectId: string;
  predecessorTaskId: string;
  successorTaskId: string;
  type: string;
  lagDays: number;
}

export interface RaidItemWriteModel {
  title: string;
  description: string | null;
  category: string;
  severity: string;
  status: string | null;
  ownerUserId: string | null;
  dueDate: string | null;
  portfolioId: string | null;
  programId: string | null;
  projectId: string | null;
}

// PmoValidation.cs's fixed vocabularies — the create forms use these directly rather than
// letting a free-text field drift from what the API will actually accept.
export const RAID_CATEGORIES = ['Risk', 'Action', 'Issue', 'Decision'] as const;
export const RAID_SEVERITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

// Project.Status is a plain string B7's fixed flow will own the transitions for (PmoEntities.cs)
// — no fixed vocabulary is validated server-side, so this is a UI convenience list, not a contract.
export const PROJECT_STATUSES = ['Draft', 'Active', 'OnHold', 'Completed', 'Cancelled'] as const;
