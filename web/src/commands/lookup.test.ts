import { describe, expect, it } from 'vitest';
import { en } from '../i18n/en';
import { autocomplete, editDistance, findCommand, suggestCommand } from './lookup';
import { COMMANDS } from './registry';

const describeCmd = (d: { name: string }) => en[`cmd.${d.name}` as keyof typeof en] as string;
const names = (typed: string) => autocomplete(COMMANDS, typed, describeCmd).map((c) => c.def.name);

describe('명령 등록표 (설계서 6-2, 6-6)', () => {
  it('이름·별칭이 겹치지 않는다', () => {
    const all = COMMANDS.flatMap((c) => [c.name, ...c.aliases]);
    expect(new Set(all).size).toBe(all.length);
  });
  it('모든 명령에 영어 설명이 있다', () => {
    for (const c of COMMANDS) expect(describeCmd(c), c.name).toBeTruthy();
  });
  it('2단계 명령은 phase 2 갈래', () => {
    expect(COMMANDS.filter((c) => c.phase === 2).map((c) => c.name)).toEqual(['DIST', 'CALIBRATE', 'PDFLAYER']);
    expect(COMMANDS.filter((c) => c.phase === 2).every((c) => c.group === 'phase2')).toBe(true);
  });
});

describe('명령 찾기', () => {
  it('이름·별칭, 대소문자 무시', () => {
    expect(findCommand(COMMANDS, 'line')?.name).toBe('LINE');
    expect(findCommand(COMMANDS, 'l')?.name).toBe('LINE');
    expect(findCommand(COMMANDS, '?')?.name).toBe('HELP');
    expect(findCommand(COMMANDS, 'op')?.name).toBe('OPTIONS');
    expect(findCommand(COMMANDS, 'LNE')).toBeUndefined();
  });
  it('설정에서 바꾼 별칭을 쓴다', () => {
    expect(findCommand(COMMANDS, 'LI', { LINE: ['LI'] })?.name).toBe('LINE');
    expect(findCommand(COMMANDS, 'L', { LINE: ['LI'] })).toBeUndefined();
  });
});

describe('자동완성 (6-5, 시안 04)', () => {
  it('EXP: 별칭이 정확히 같은 EXPORT가 맨 위, 그다음 EXP로 시작하는 이름', () => {
    expect(names('EXP').slice(0, 2)).toEqual(['EXPORT', 'EXPORTCSV']);
  });
  it('정확히 같은 별칭이 먼저: L → LINE', () => {
    expect(names('l')[0]).toBe('LINE');
  });
  it('설명에 든 단어도 찾는다 (2글자부터)', () => {
    expect(names('cloud')).toContain('REVCLOUD');
  });
  it('최대 8줄, 띄어쓰기가 있으면 목록 없음', () => {
    expect(names('C').length).toBeLessThanOrEqual(8);
    expect(names('ZOOM E')).toEqual([]);
    expect(names('')).toEqual([]);
  });
});

describe('오타 제안 (6-5, 시안 11)', () => {
  it('글자 차이', () => {
    expect(editDistance('LNE', 'LINE')).toBe(1);
    expect(editDistance('GIRD', 'GRID')).toBe(2);
  });
  it('LNE → LINE, 너무 먼 글자는 제안 없음', () => {
    expect(suggestCommand(COMMANDS, 'LNE')?.name).toBe('LINE');
    expect(suggestCommand(COMMANDS, 'ortoh')?.name).toBe('ORTHO');
    expect(suggestCommand(COMMANDS, 'QQQQQQ')).toBeUndefined();
    expect(suggestCommand(COMMANDS, 'XY')).toBeUndefined();
  });
});

describe('리본 버튼 = 명령 (6-1)', () => {
  it('리본의 모든 버튼이 등록표의 명령을 부른다', async () => {
    const { RIBBON } = await import('../shell/ribbonLayout');
    for (const groups of Object.values(RIBBON))
      for (const g of groups)
        for (const b of g.buttons) expect(findCommand(COMMANDS, b.command.split(' ')[0]), b.command).toBeDefined();
  });
});
