import { describe, expect, it } from 'vitest';
import { keyName } from './KeyTest';

const ev = (key: string, mods: Partial<Record<'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey', boolean>> = {}) =>
  ({ key, ctrlKey: false, shiftKey: false, altKey: false, metaKey: false, ...mods });

describe('키 이름', () => {
  it('F키와 조합 키', () => {
    expect(keyName(ev('F11'))).toBe('F11');
    expect(keyName(ev('0', { ctrlKey: true }))).toBe('Ctrl+0');
    expect(keyName(ev('F', { ctrlKey: true, shiftKey: true }))).toBe('Ctrl+Shift+F');
    expect(keyName(ev('f', { ctrlKey: true, shiftKey: true }))).toBe('Ctrl+Shift+F');
  });
});
