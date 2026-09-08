import { isRtl } from './i18n';

/**
 * Picks the Arabic twin of a bilingual API field when the current language is Arabic and
 * a translation actually exists, falling back to the English value otherwise — every SMO
 * (and eventually PMO) field pairs `Name`/`NameAr` at the data layer (CLAUDE.md's i18n
 * non-negotiable), but not every row has bothered to fill the Arabic column in yet.
 */
export function localized(en: string, ar: string | null | undefined, language: string): string {
  return isRtl(language) && ar ? ar : en;
}
