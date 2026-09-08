import { useSyncExternalStore } from 'react';
import { getTheme, setTheme, subscribeTheme, type ThemeMode } from './theme';

export function useTheme(): { theme: ThemeMode; setTheme: (theme: ThemeMode) => void } {
  const theme = useSyncExternalStore(subscribeTheme, getTheme);
  return { theme, setTheme };
}
