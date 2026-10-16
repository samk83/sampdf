import { describe, expect, it } from 'vitest';
import { assignLabels, nthLabel } from './numbering';

describe('numbering', () => {
  it('counts numbers and letters', () => {
    expect(nthLabel('1', 2)).toBe('3');
    expect(nthLabel('A', 1)).toBe('B');
    expect(nthLabel('Z', 1)).toBe('AA');
    expect(nthLabel('b', 1)).toBe('c');
    expect(nthLabel('R1', 0)).toBe('R1');
    expect(nthLabel('R1', 1)).toBe('R1-2');
    expect(nthLabel('', 0)).toBe('1');
  });

  it('assigns sequential or same labels', () => {
    expect(assignLabels(['a', 'b', 'c'], '1', 'sequential')).toEqual({ a: '1', b: '2', c: '3' });
    expect(assignLabels(['a', 'b'], 'B', 'same')).toEqual({ a: 'B', b: 'B' });
  });
});
