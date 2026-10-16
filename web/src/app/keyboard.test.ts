import { describe, expect, it } from 'vitest';
import { routeKey, type KeyInput } from './keyboard';

const key = (k: string, more: Partial<KeyInput> = {}): KeyInput => ({
  key: k, ctrl: false, shift: false, alt: false, meta: false, target: 'body', modalOpen: false, ...more,
});

describe('키보드 분배 (설계서 2-5, 6-7)', () => {
  it('빈 곳에서 글자는 명령창으로', () => {
    expect(routeKey(key('l'))).toEqual({ kind: 'type', char: 'l' });
    expect(routeKey(key('?', { shift: true }))).toEqual({ kind: 'type', char: '?' });
    expect(routeKey(key('L', { target: 'control' }))).toEqual({ kind: 'type', char: 'L' });
  });

  it('입력칸에 커서가 있으면 글자는 그 칸으로 (명령창으로 안 감)', () => {
    expect(routeKey(key('l', { target: 'field' }))).toBeNull();
    expect(routeKey(key(' ', { target: 'field' }))).toBeNull();
    expect(routeKey(key('Enter', { target: 'field' }))).toBeNull();
    expect(routeKey(key('Escape', { target: 'field' }))).toBeNull();
  });

  it('명령창 입력칸은 자기가 처리', () => {
    expect(routeKey(key('l', { target: 'cmdline' }))).toBeNull();
  });

  it('대화상자가 떠 있으면 글자는 명령창으로 안 감', () => {
    expect(routeKey(key('l', { modalOpen: true }))).toBeNull();
    expect(routeKey(key('Escape', { modalOpen: true }))).toBeNull();
  });

  it('단축키는 입력칸에서도 동작', () => {
    for (const target of ['body', 'field', 'cmdline', 'control'] as const) {
      expect(routeKey(key('F7', { target }))).toEqual({ kind: 'run', command: 'GRID' });
      expect(routeKey(key('0', { ctrl: true, target }))).toEqual({ kind: 'run', command: 'PANELS' });
    }
    expect(routeKey(key('F1'))).toEqual({ kind: 'run', command: 'HELP' });
    expect(routeKey(key('F2'))).toEqual({ kind: 'history' });
    expect(routeKey(key('F3'))).toEqual({ kind: 'osnap' });
    expect(routeKey(key('F8'))).toEqual({ kind: 'run', command: 'ORTHO' });
    expect(routeKey(key('F9'))).toEqual({ kind: 'run', command: 'SNAP' });
    expect(routeKey(key('9', { ctrl: true }))).toEqual({ kind: 'run', command: 'CMDLINE' });
  });

  it('아직 명령이 없는 키와 브라우저 키는 건드리지 않는다', () => {
    expect(routeKey(key('F11'))).toBeNull();
    expect(routeKey(key('s', { ctrl: true }))).toBeNull();
    expect(routeKey(key('z', { ctrl: true }))).toBeNull();
    expect(routeKey(key('c', { ctrl: true }))).toBeNull();
    expect(routeKey(key('Tab'))).toBeNull();
    expect(routeKey(key('ArrowUp'))).toBeNull();
    expect(routeKey(key('F7', { shift: true }))).toBeNull();
  });

  it('빈 곳의 Enter·Space = 실행(직전 명령 반복), 버튼 위에서는 버튼 누르기', () => {
    expect(routeKey(key('Enter'))).toEqual({ kind: 'submit' });
    expect(routeKey(key(' '))).toEqual({ kind: 'submit' });
    expect(routeKey(key('Enter', { target: 'control' }))).toBeNull();
    expect(routeKey(key(' ', { target: 'control' }))).toBeNull();
  });

  it('한글 입력 상태여도 자판 위치로 영어 글자', () => {
    expect(routeKey(key('Process', { code: 'KeyL' }))).toEqual({ kind: 'type', char: 'l' });
    expect(routeKey(key('ㅣ', { code: 'KeyL' }))).toEqual({ kind: 'type', char: 'l' });
    expect(routeKey(key('Process', { code: 'KeyL', target: 'field' }))).toBeNull();
  });

  it('빈 곳의 Esc = 명령 취소', () => {
    expect(routeKey(key('Escape'))).toEqual({ kind: 'cancel' });
  });
});
