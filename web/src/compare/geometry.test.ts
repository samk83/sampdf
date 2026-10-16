import { describe, expect, it } from 'vitest';
import { cloudPath, deltaTriangle, shortText } from './geometry';

describe('geometry', () => {
  it('builds a closed cloud path with arcs on every side', () => {
    const d = cloudPath({ x0: 0, y0: 0, x1: 120, y1: 24 }, 12);
    expect(d.startsWith('M0.0 0.0')).toBe(true);
    expect(d.endsWith('Z')).toBe(true);
    expect((d.match(/A/g) ?? []).length).toBe(10 + 2 + 10 + 2);
  });

  it('keeps the delta inside the page', () => {
    const t = deltaTriangle({ x0: 1180, y0: 0, x1: 1191, y1: 10 }, '1', 1191, 842);
    for (const p of t.points.split(' ')) {
      const [x, y] = p.split(',').map(Number);
      expect(x).toBeGreaterThanOrEqual(0);
      expect(x).toBeLessThanOrEqual(1191);
      expect(y).toBeGreaterThanOrEqual(0);
    }
  });

  it('extracts short text from engine descriptions', () => {
    expect(shortText('Text "BEAM" → "COLUMN"')).toBe('BEAM → COLUMN');
    expect(shortText('Drawing changed; text added "NEW"')).toBe('+ NEW');
    expect(shortText('Text removed "OLD"')).toBe('− OLD');
    expect(shortText('')).toBeNull();
  });
});
