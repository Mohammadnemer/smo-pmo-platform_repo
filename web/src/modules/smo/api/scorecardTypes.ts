// Mirrors src/SMO/SmoContracts.cs's read models exactly — field names are the camelCase
// System.Text.Json default, and the enums below are unconverted (no JsonStringEnumConverter
// is registered on the API), so they arrive as the C# enum's underlying number.

/** RagStatus: NotSet = 0, Red = 1, Amber = 2, Green = 3. */
export type RagStatus = 0 | 1 | 2 | 3;

/** KpiDirection: HigherIsBetter = 0, LowerIsBetter = 1. */
export type KpiDirection = 0 | 1;

/** KpiFrequency: Monthly = 0, Quarterly = 1, SemiAnnual = 2, Annual = 3. */
export type KpiFrequency = 0 | 1 | 2 | 3;

export interface StrategyResponse {
  id: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  vision: string | null;
  mission: string | null;
  horizonStart: string | null;
  horizonEnd: string | null;
  health: RagStatus;
  healthScore: number | null;
  healthComputedAt: string | null;
}

export interface PerspectiveResponse {
  id: string;
  strategyId: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  displayOrder: number;
  isHidden: boolean;
}

export interface ObjectiveResponse {
  id: string;
  perspectiveId: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  targetState: string | null;
  ownerUserId: string | null;
  orgUnitId: string | null;
  displayOrder: number;
  health: RagStatus;
  healthScore: number | null;
  healthComputedAt: string | null;
}

export interface KpiResponse {
  id: string;
  objectiveId: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  unit: string | null;
  direction: KpiDirection;
  frequency: KpiFrequency;
  baseline: number | null;
  target: number | null;
  actual: number | null;
  actualAsOf: string | null;
  greenThresholdPercent: number;
  amberThresholdPercent: number;
  formula: string | null;
  dataSource: string | null;
  ownerUserId: string | null;
  rag: RagStatus;
  achievementPercent: number | null;
}

export interface KpiMeasurementResponse {
  id: string;
  kpiId: string;
  periodStart: string;
  periodEnd: string;
  value: number;
  note: string | null;
}

export interface InitiativeResponse {
  id: string;
  objectiveId: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  sponsor: string | null;
  budget: number | null;
  budgetCurrency: string | null;
  expectedBenefit: string | null;
  startDate: string | null;
  endDate: string | null;
  status: string;
  health: RagStatus;
  healthScore: number | null;
  healthComputedAt: string | null;
}

export interface RagCounts {
  green: number;
  amber: number;
  red: number;
  notSet: number;
}

/** A cause-effect edge between two objectives — the strategy map's (F5) arrows. */
export interface ObjectiveLinkResponse {
  id: string;
  sourceObjectiveId: string;
  targetObjectiveId: string;
  note: string | null;
}

export interface ScorecardKpi {
  kpi: KpiResponse;
  trend: KpiMeasurementResponse[];
}

export interface ScorecardObjective {
  objective: ObjectiveResponse;
  kpiRagCounts: RagCounts;
  kpis: ScorecardKpi[];
  initiatives: InitiativeResponse[];
}

export interface ScorecardPerspective {
  perspective: PerspectiveResponse;
  kpiRagCounts: RagCounts;
  objectives: ScorecardObjective[];
}

export interface ScorecardResponse {
  strategy: StrategyResponse;
  kpiRagCounts: RagCounts;
  perspectives: ScorecardPerspective[];
  objectiveLinks: ObjectiveLinkResponse[];
}
