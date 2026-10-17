import { describe, expect, it } from 'vitest';
import { COMMANDS } from '../commands/registry';
import { en } from '../i18n/en';
import { ko } from '../i18n/ko';
import { translate } from '../i18n';
import { APPS, CATEGORIES, STAGES, appById, appDescKey, appNameKey, searchApps } from './registry';

describe('기능 등록표 (설계서 11-2)', () => {
  it('PRD의 기능 24개가 번호 1~24로 한 번씩 있다', () => {
    expect(APPS).toHaveLength(24);
    expect(APPS.map((a) => a.number).sort((a, b) => a - b)).toEqual(Array.from({ length: 24 }, (_, i) => i + 1));
    expect(new Set(APPS.map((a) => a.id)).size).toBe(24);
  });

  it('갈래마다 8개씩 (PRD 4장)', () => {
    for (const c of CATEGORIES) expect(APPS.filter((a) => a.category === c)).toHaveLength(8);
  });

  it('동작하는 기능은 명령 등록표에 있는 명령으로 열린다', () => {
    const names = new Set(COMMANDS.flatMap((c) => [c.name, ...c.aliases]));
    for (const app of APPS.filter((a) => a.status !== 'soon')) {
      expect(app.command, app.id).toBeTruthy();
      expect(names.has(app.command!), app.id).toBe(true);
    }
    expect(appById('drawing-compare')?.command).toBe('CMP');
  });

  it('준비 중 기능은 명령이 없고 계획 단계가 1보다 뒤', () => {
    for (const app of APPS.filter((a) => a.status === 'soon')) {
      expect(app.command, app.id).toBeUndefined();
      expect(app.stage, app.id).toBeGreaterThan(1);
    }
  });

  it('갈래·상태·계획 단계 글자가 영어와 한국어로 있다 (화면에서 키를 만들어 쓰므로 타입 검사가 못 잡음)', () => {
    const keys = [
      ...CATEGORIES.map((c) => `home.category.${c}`),
      ...['ready', 'experimental', 'soon'].map((x) => `apps.status.${x}`),
      ...STAGES.map((n) => `apps.stage.${n}`),
    ];
    for (const key of keys) {
      expect(key in en, key).toBe(true);
      expect(key in ko, key).toBe(true);
    }
    for (const app of APPS) expect(STAGES).toContain(app.stage);
  });

  it('모든 기능에 이름·설명 글자가 영어와 한국어로 있다', () => {
    for (const app of APPS) {
      for (const key of [appNameKey(app), appDescKey(app)]) {
        expect(key in en, key).toBe(true);
        expect(key in ko, key).toBe(true);
      }
    }
  });
});

describe('도구 찾기', () => {
  const texts = (lang: 'en' | 'ko') => (app: (typeof APPS)[number]) =>
    [appNameKey(app), appDescKey(app)].flatMap((k) => [translate(lang, k), translate('en', k)]);

  it('빈 검색어는 전부', () => {
    expect(searchApps(APPS, '  ', texts('en'))).toHaveLength(24);
  });

  it('이름·설명·명령으로 찾고 대소문자를 가리지 않는다', () => {
    expect(searchApps(APPS, 'merge', texts('en')).map((a) => a.id)).toEqual(['merge-pdf']);
    expect(searchApps(APPS, 'cmp', texts('en')).map((a) => a.id)).toEqual(['drawing-compare']);
    expect(searchApps(APPS, 'DWG', texts('en')).map((a) => a.id)).toContain('dwg-preview');
  });

  it('낱말이 여럿이면 모두 들어 있어야 한다', () => {
    expect(searchApps(APPS, 'pdf split', texts('en')).map((a) => a.id)).toEqual(['split-pdf']);
  });

  it('한국어 화면에서도 한국어와 영어로 찾는다', () => {
    expect(searchApps(APPS, '합치기', texts('ko')).map((a) => a.id)).toEqual(['merge-pdf']);
    expect(searchApps(APPS, 'merge', texts('ko')).map((a) => a.id)).toEqual(['merge-pdf']);
  });
});
