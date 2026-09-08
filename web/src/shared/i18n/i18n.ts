import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import ar from './resources/ar.json';
import en from './resources/en.json';

export const supportedLanguages = ['en', 'ar'] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

const STORAGE_KEY = 'smo-pmo-lang';

function isSupportedLanguage(value: string | null): value is SupportedLanguage {
  return supportedLanguages.includes(value as SupportedLanguage);
}

function getInitialLanguage(): SupportedLanguage {
  const stored = localStorage.getItem(STORAGE_KEY);
  return isSupportedLanguage(stored) ? stored : 'en';
}

export function isRtl(language: string): boolean {
  return language === 'ar';
}

function applyDocumentDirection(language: string) {
  document.documentElement.lang = language;
  document.documentElement.dir = isRtl(language) ? 'rtl' : 'ltr';
}

void i18next.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ar: { translation: ar },
  },
  lng: getInitialLanguage(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

// The initial `lng` above doesn't fire `languageChanged` (it only fires on
// explicit i18next.changeLanguage calls), so apply it once up front here —
// every subsequent switch (the AR/EN toggle) is then covered by the listener.
applyDocumentDirection(i18next.language);

i18next.on('languageChanged', (language) => {
  applyDocumentDirection(language);
  localStorage.setItem(STORAGE_KEY, language);
});

export default i18next;
