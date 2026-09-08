export const supportedThemes = ['light', 'dark'] as const;
export type ThemeMode = (typeof supportedThemes)[number];

const STORAGE_KEY = 'smo-pmo-theme';

function isThemeMode(value: string | null): value is ThemeMode {
  return supportedThemes.includes(value as ThemeMode);
}

function prefersDark(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function getInitialTheme(): ThemeMode {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (isThemeMode(stored)) return stored;
  return prefersDark() ? 'dark' : 'light';
}

function applyDocumentTheme(theme: ThemeMode) {
  document.documentElement.dataset.theme = theme;
}

type Listener = (theme: ThemeMode) => void;
const listeners = new Set<Listener>();

let current = getInitialTheme();
applyDocumentTheme(current);

export function getTheme(): ThemeMode {
  return current;
}

export function setTheme(theme: ThemeMode) {
  current = theme;
  applyDocumentTheme(theme);
  localStorage.setItem(STORAGE_KEY, theme);
  listeners.forEach((listener) => listener(theme));
}

export function subscribeTheme(listener: Listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
