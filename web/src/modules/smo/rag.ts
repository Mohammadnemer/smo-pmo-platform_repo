import type { RagStatus } from './api/scorecardTypes';

export type BadgeTone = 'ok' | 'warn' | 'risk' | 'neutral';

/**
 * RAG → badge tone / i18n key. Shared by the scorecard (F4) and the strategy map (F5) — both
 * render the same `RagStatus` values, and F4 flagged this exact duplication as worth
 * extracting once a second real consumer showed up (docs/sessions/F4.md, Follow-ups).
 */
export const RAG_TONE: Record<RagStatus, BadgeTone> = { 0: 'neutral', 1: 'risk', 2: 'warn', 3: 'ok' };

export const RAG_KEY: Record<RagStatus, string> = {
  0: 'pages.scorecard.rag.notSet',
  1: 'pages.scorecard.rag.red',
  2: 'pages.scorecard.rag.amber',
  3: 'pages.scorecard.rag.green',
};

export function formatValue(value: number | null): string {
  return value === null ? '—' : String(Math.round(value * 100) / 100);
}
