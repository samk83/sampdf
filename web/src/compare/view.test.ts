import { describe, expect, it } from 'vitest';
import { fitView, focusRect, renderScale, zoomAt } from './view';

describe('view', () => {
  it('fits a page into the pane with margins', () => {
    const v = fitView({ width: 1000, height: 500 }, { width: 1048, height: 800 });
    expect(v.zoom).toBeCloseTo(1);
    expect(v.x).toBeCloseTo(24);
    expect(v.y).toBeCloseTo(150);
  });

  it('zooms around the cursor point', () => {
    const v = zoomAt({ zoom: 1, x: 0, y: 0 }, 2, 100, 100);
    // 페이지 점 (100,100)이 그대로 화면 (100,100)에 남는다
    expect(100 * v.zoom + v.x).toBeCloseTo(100);
    expect(100 * v.zoom + v.y).toBeCloseTo(100);
  });

  it('centers a change region', () => {
    const v = focusRect({ x0: 400, y0: 380, x1: 600, y1: 390 }, { width: 800, height: 600 }, { zoom: 0.5, x: 0, y: 0 });
    expect(500 * v.zoom + v.x).toBeCloseTo(400);
    expect(385 * v.zoom + v.y).toBeCloseTo(300);
  });

  it('picks render scale steps', () => {
    expect(renderScale(0.6, 1)).toBe(1);
    expect(renderScale(0.6, 2)).toBe(1.5);
    expect(renderScale(20, 2)).toBe(8);
  });
});
