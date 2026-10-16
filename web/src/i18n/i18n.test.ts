import { describe, expect, it } from 'vitest';
import { en } from './en';
import { formatMessage, missingKeys, translate, LANGUAGES } from './index';

describe('i18n t() (설계서 8장)', () => {
  it('키의 영어 글자를 돌려주고 {자리}를 채운다', () => {
    expect(translate('en', 'home.title')).toBe('What would you like to do?');
    expect(translate('en', 'tabs.close', { name: 'S-101.pdf' })).toBe('Close S-101.pdf');
  });

  it('한국어에 없는 키는 영어로 보여 준다', () => {
    expect(translate('ko', 'home.title')).toBe(en['home.title']);
  });

  it('1개/여러 개 글자를 n으로 고른다', () => {
    const plural = { one: '1 change', other: '{n} changes' };
    expect(formatMessage(plural, { n: 1 })).toBe('1 change');
    expect(formatMessage(plural, { n: 4 })).toBe('4 changes');
    expect(formatMessage(plural, { n: 0 })).toBe('0 changes');
  });

  it('없는 자리 이름은 그대로 둔다', () => {
    expect(formatMessage('{a} and {b}', { a: 'x' })).toBe('x and {b}');
  });

  it('빠진 한국어 키 목록을 알려 준다 (U11에서 0개가 되어야 함)', () => {
    const missing = missingKeys('ko');
    console.info(`ko: ${missing.length} / ${Object.keys(en).length} keys missing (filled in U11)`);
    expect(missing).not.toContain('app.name');
    expect(missingKeys('en')).toEqual([]);
  });

  it('언어 목록은 실제로 있는 언어만 (D8)', () => {
    expect(LANGUAGES.map((l) => l.code)).toEqual(['en', 'ko']);
  });
});
