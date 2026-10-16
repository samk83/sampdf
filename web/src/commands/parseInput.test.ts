import { describe, expect, it } from 'vitest';
import { matchOption, parseNumber, parsePoint, resolvePoint } from './parseInput';

describe('입력 해석 (설계서 6-4)', () => {
  it('절대·상대·극좌표·거리', () => {
    expect(parsePoint('100,200')).toEqual({ kind: 'absolute', point: { x: 100, y: 200 } });
    expect(parsePoint(' -5.5 , .5 ')).toEqual({ kind: 'absolute', point: { x: -5.5, y: 0.5 } });
    expect(parsePoint('@100,50')).toEqual({ kind: 'relative', dx: 100, dy: 50 });
    expect(parsePoint('@500<90')).toEqual({ kind: 'polar', distance: 500, angleDeg: 90 });
    expect(parsePoint('2000')).toEqual({ kind: 'distance', distance: 2000 });
  });
  it('오스냅 한 번만: end, MID …', () => {
    expect(parsePoint('end')).toEqual({ kind: 'osnap', mode: 'END' });
    expect(parsePoint('NEA')).toEqual({ kind: 'osnap', mode: 'NEA' });
  });
  it('틀린 입력은 undefined', () => {
    for (const bad of ['', '100,', 'abc', '@<90', '1,2,3']) expect(parsePoint(bad), bad).toBeUndefined();
  });
  it('실제 점으로 바꾸기', () => {
    const last = { x: 10, y: 20 };
    expect(resolvePoint({ kind: 'relative', dx: 5, dy: -5 }, last)).toEqual({ x: 15, y: 15 });
    const p = resolvePoint({ kind: 'polar', distance: 100, angleDeg: 90 }, last)!;
    expect(p.x).toBeCloseTo(10);
    expect(p.y).toBeCloseTo(120);
    const d = resolvePoint({ kind: 'distance', distance: 50 }, last, 0)!;
    expect(d).toEqual({ x: 60, y: 20 });
    expect(resolvePoint({ kind: 'relative', dx: 1, dy: 1 }, undefined)).toBeUndefined();
  });
  it('옵션: 단어 전체 또는 앞글자', () => {
    expect(matchOption('u', ['Undo', 'Close'])).toBe('Undo');
    expect(matchOption('CL', ['Undo', 'Close'])).toBe('Close');
    expect(matchOption('close', ['Undo', 'Close'])).toBe('Close');
    expect(matchOption('x', ['Undo', 'Close'])).toBeUndefined();
    expect(matchOption('', ['Undo'])).toBeUndefined();
  });
  it('숫자', () => {
    expect(parseNumber('12')).toBe(12);
    expect(parseNumber('2.5')).toBe(2.5);
    expect(parseNumber('1,000')).toBeUndefined();
    expect(parseNumber('abc')).toBeUndefined();
  });
});
