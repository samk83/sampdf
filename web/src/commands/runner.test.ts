import { beforeEach, describe, expect, it } from 'vitest';
import { activeTab, HOME_TAB_ID, initialState, useApp } from '../app/store';
import { cancel, interpret, promptText, resetRunner, runCommand, submit } from './runner';

const s = () => useApp.getState();
const lines = () => s().history.map((l) => l.text);
const lastLine = () => s().history[s().history.length - 1];

beforeEach(() => {
  resetRunner();
  useApp.setState(initialState());
});

describe('명령 실행 (U2 완료 기준)', () => {
  it('? → 명령 목록 창', async () => {
    await submit('?');
    expect(s().helpOpen).toBe(true);
    expect(lines()[0]).toBe('Command: ?');
  });

  it('GRID·ORTHO·SNAP 켜고 끄기 + <Grid on> 기록', async () => {
    await submit('grid');
    expect(s().toggles.grid).toBe(true);
    expect(lastLine().text).toBe('<Grid on>');
    await submit('GRID');
    expect(s().toggles.grid).toBe(false);
    await submit('ortho');
    expect(s().toggles.ortho).toBe(true);
    await submit('snap');
    expect(s().toggles.snap).toBe(true);
  });

  it('PANELS·CMDLINE 숨기고 보이기', async () => {
    await submit('PANELS');
    expect(s().panelsHidden).toBe(true);
    await submit('PANELS');
    expect(s().panelsHidden).toBe(false);
    await submit('CMDLINE');
    expect(s().cmdlineHidden).toBe(true);
  });

  it('TABNEXT·TABPREV는 돌아가며 바꾼다', async () => {
    const c = s().openTab('compare');
    const p = s().openTab('png');
    await submit('TABNEXT');
    expect(s().activeId).toBe(HOME_TAB_ID);
    await submit('TABNEXT');
    expect(s().activeId).toBe(c);
    await submit('TABPREV');
    expect(s().activeId).toBe(HOME_TAB_ID);
    await submit('TABPREV');
    expect(s().activeId).toBe(p);
  });

  it('EXIT: 지금 탭 닫기, Home은 못 닫음', async () => {
    await submit('EXIT');
    expect(lastLine()).toEqual({ text: 'The Home tab cannot be closed.', kind: 'error' });
    s().openTab('png');
    await submit('EXIT');
    expect(s().tabs).toHaveLength(1);
  });

  it('EXIT: 저장 안 된 탭은 명령창에서 묻는다 (기본 No)', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    expect(s().cmd.ask?.kind).toBe('option');
    expect(promptText(s().cmd.ask!)).toBe('Compare has unsaved changes. Close it anyway? [Yes/No] <No>:');
    await submit(''); // 빈 Enter = 기본값 No
    await running;
    expect(s().tabs.map((t) => t.id)).toContain(c);
    const again = submit('EXIT');
    await submit('y');
    await again;
    expect(s().tabs.map((t) => t.id)).not.toContain(c);
  });

  it('틀린 답은 빨간 줄 + 다시 묻는다', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    await submit('maybe');
    expect(lastLine()).toEqual({ text: 'Invalid option. Type one of: Yes, No.', kind: 'error' });
    expect(s().cmd.ask).not.toBeNull();
    await submit('Yes');
    await running;
    expect(s().tabs).toHaveLength(1);
  });

  it('Esc는 지금 명령을 취소한다', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    expect(cancel()).toBe(true);
    await running;
    expect(lastLine().text).toBe('*Cancel*');
    expect(s().cmd.running).toBeNull();
    expect(s().tabs).toHaveLength(2);
    expect(cancel()).toBe(false);
  });

  it('명령 뒤에 미리 친 답을 차례로 넣는다 (EXIT Y)', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    await submit('EXIT Y');
    expect(s().tabs).toHaveLength(1);
  });

  it('빈 Enter = 직전 명령 반복', async () => {
    await submit('GRID');
    await submit('');
    expect(s().toggles.grid).toBe(false);
    expect(lines()).toContain('Command: GRID');
  });

  it('LNE → 제안, Tab에 쓸 명령을 기억', async () => {
    await submit('LNE');
    expect(lastLine()).toEqual({ text: 'Unknown command "LNE". Did you mean LINE (L)? Press Tab to use it.', kind: 'error' });
    expect(s().cmd.suggestion).toBe('LINE');
    await submit('QQQQQQ');
    expect(lastLine().text).toBe('Unknown command "QQQQQQ". Type ? for the list.');
    expect(s().cmd.suggestion).toBeNull();
  });

  it('쓸 수 없는 탭에서는 이유를 알려 준다', async () => {
    await submit('L');
    expect(lastLine()).toEqual({ text: 'LINE is not available on the Home tab.', kind: 'error' });
    s().openTab('png');
    await submit('SWAP');
    expect(lastLine().text).toBe('SWAP is not available on the Transparent PNG tab.');
  });

  it('등록만 된 명령 (OP·FOCUS·DARKVIEW)과 2단계 명령은 안내만', async () => {
    await submit('OP');
    expect(lastLine().text).toBe('OPTIONS is coming in a later update.');
    s().openTab('compare');
    await submit('DV');
    expect(lastLine().text).toBe('DARKVIEW is coming in a later update.');
    expect(s().toggles.darkView).toBe(false);
    await submit('FOCUS');
    expect(lastLine().text).toBe('FOCUS is coming in a later update.');
    await submit('DIST');
    expect(lastLine().text).toBe('DIST is coming in phase 2.');
  });

  it('CMP·PNG는 새 탭을 연다', async () => {
    await submit('compare');
    expect(activeTab(s()).kind).toBe('compare');
    await submit('png');
    expect(activeTab(s()).kind).toBe('png');
  });

  it('입력 기록: 같은 입력이 이어지면 한 번만', async () => {
    await submit('GRID');
    await submit('GRID');
    await submit('ORTHO');
    expect(s().cmd.recall).toEqual(['GRID', 'ORTHO']);
  });
});

describe('버튼·단축키 = 명령 (6-1)', () => {
  it('quiet면 Command: 줄 없이 결과만', async () => {
    await runCommand('GRID', { quiet: true });
    expect(lines()).toEqual(['<Grid on>']);
  });

  it('다른 명령 중에 버튼을 누르면 그 명령을 취소하고 시작', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    await runCommand('PNG');
    await running;
    expect(activeTab(s()).kind).toBe('png');
    expect(s().cmd.ask).toBeNull();
    expect(s().cmd.running).toBeNull();
  });

  it('GRID 같은 투명 명령은 지금 명령을 취소하지 않는다', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    await runCommand('GRID', { quiet: true });
    expect(s().toggles.grid).toBe(true);
    expect(s().cmd.running).toBe('EXIT');
    await submit('Y');
    await running;
    expect(s().tabs).toHaveLength(1);
  });
});

describe('질문 답 해석', () => {
  it('숫자 질문: 숫자·옵션·기본값', () => {
    const ask = { kind: 'number' as const, prompt: 'Tolerance', defaultValue: 12, options: ['Pick'] };
    expect(interpret(ask, '20')).toEqual({ number: 20 });
    expect(interpret(ask, '')).toEqual({ number: 12 });
    expect(interpret(ask, 'p')).toEqual({ option: 'Pick' });
    expect(interpret(ask, 'abc')).toEqual({ error: 'cmdline.invalidNumber' });
    expect(promptText(ask)).toBe('Tolerance [Pick] <12>:');
  });
  it('점 질문', () => {
    const ask = { kind: 'point' as const, prompt: 'Specify next point', options: ['Undo', 'Close'] };
    expect(interpret(ask, '@500<90')).toEqual({ point: { kind: 'polar', distance: 500, angleDeg: 90 } });
    expect(interpret(ask, 'c')).toEqual({ option: 'Close' });
    expect(interpret(ask, '')).toEqual({ done: true });
    expect(interpret(ask, 'zz')).toEqual({ error: 'cmdline.invalidPoint' });
  });
  it('글자 질문은 띄어쓰기를 그대로', () => {
    expect(interpret({ kind: 'text', prompt: 'Enter text' }, ' A B ')).toEqual({ text: ' A B ' });
  });
});

describe('리뷰 지적 반영', () => {
  it('한/영을 안 바꾸고 친 명령도 실행 (ㅎ갸ㅇ = GRID)', async () => {
    await submit('ㅎ갸ㅇ');
    expect(s().toggles.grid).toBe(true);
  });

  it('버튼으로 바꾼 명령은 *Cancel* 줄을 남긴다', async () => {
    const c = s().openTab('compare');
    s().setDirty(c, true);
    const running = submit('EXIT');
    await runCommand('PNG');
    await running;
    expect(lines()).toContain('*Cancel*');
  });

  it('취소된 명령이 나중에 질문해도 새 명령을 방해하지 않는다', async () => {
    const { COMMANDS } = await import('./registry');
    const { BUILTINS } = await import('./builtins');
    let release!: () => void;
    const gate = new Promise<void>((r) => { release = r; });
    let lateAsk: Promise<unknown> | undefined;
    BUILTINS.SLOWTEST = async (ctx) => { await gate; lateAsk = ctx.ask({ kind: 'text', prompt: 'late' }); await lateAsk; };
    COMMANDS.push({ name: 'SLOWTEST', group: 'common', aliases: [] });
    try {
      const slow = submit('SLOWTEST');
      expect(cancel()).toBe(true);
      release();
      await slow;
      await expect(lateAsk).rejects.toThrow('cancelled');
      expect(s().cmd.ask).toBeNull();
    } finally {
      COMMANDS.pop();
      delete BUILTINS.SLOWTEST;
    }
  });
});
