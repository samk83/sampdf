import { en, type Messages } from './en';
import { ko } from './ko';
import { useApp } from '../app/store';

export type Language = 'en' | 'ko';
export type MessageKey = keyof Messages;
type Plural = { one: string; other: string };
type Params = Record<string, string | number>;

const tables: Record<Language, Partial<Record<MessageKey, string | Plural>>> = { en, ko };

/** 실제로 있는 언어만 (설계서 D8). */
export const LANGUAGES: { code: Language; label: string }[] = [
  { code: 'en', label: 'English' },
  { code: 'ko', label: '한국어' },
];

/**
 * 화면 글자 하나를 고른 언어로 돌려준다. 그 언어에 없는 키는 영어로 (설계서 8장).
 * `{n}` 같은 자리는 params 값으로 바꾸고, `{ one, other }` 글자는 params.n으로 고른다.
 */
export function translate(lang: Language, key: MessageKey, params?: Params): string {
  return formatMessage(tables[lang][key] ?? en[key], params);
}

/** 글자 하나를 완성한다: 1개/여러 개 고르기 → {자리} 채우기. */
export function formatMessage(entry: string | Plural, params?: Params): string {
  let text: string;
  if (typeof entry === 'string') text = entry;
  else text = Number(params?.n) === 1 ? entry.one : entry.other;
  if (params) text = text.replace(/\{(\w+)\}/g, (m, name: string) => (name in params ? String(params[name]) : m));
  return text;
}

/** 영어 기준 파일에 있는데 그 언어 파일에 없는 키 목록 (U11에서 0개여야 함). */
export function missingKeys(lang: Language): MessageKey[] {
  return (Object.keys(en) as MessageKey[]).filter((k) => !(k in tables[lang]));
}

/** 화면 부품에서 쓰는 번역 함수. 언어를 바꾸면 바로 다시 그려진다. */
export function useT() {
  const lang = useApp((s) => s.language);
  return (key: MessageKey, params?: Params) => translate(lang, key, params);
}
