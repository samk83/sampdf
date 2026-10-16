import { describe, expect, it } from 'vitest';
import { detectBackground, opaqueBounds, removeBackground } from './removeBackground';

// 4×1 그림: 흰색, 연한 회색(얼룩), 중간 회색(가장자리), 검정(선)
const px = new Uint8ClampedArray([255, 255, 255, 255, 245, 245, 245, 255, 200, 200, 200, 255, 0, 0, 0, 255]);
const white = { r: 255, g: 255, b: 255 };

describe('removeBackground', () => {
  it('makes background transparent and keeps ink opaque', () => {
    const out = removeBackground(px, { threshold: 240, background: white, soft: false, unifyColor: null });
    expect(out[3]).toBe(0);      // 흰색
    expect(out[7]).toBe(0);      // 차이 10 ≤ 허용 15 → 지움
    expect(out[11]).toBe(255);
    expect(out[15]).toBe(255);
  });

  it('soft edges become semi-transparent and recover the ink color', () => {
    const out = removeBackground(px, { threshold: 240, background: white, soft: true, unifyColor: null });
    expect(out[11]).toBeGreaterThan(0);
    expect(out[11]).toBeLessThan(255);
    expect(out[8]).toBeLessThan(200); // 흰색이 섞인 회색 → 더 진한 원래 색
    expect(out[15]).toBe(255);
  });

  it('unifies line color', () => {
    const out = removeBackground(px, { threshold: 240, background: white, soft: false, unifyColor: { r: 10, g: 20, b: 200 } });
    expect([out[12], out[13], out[14]]).toEqual([10, 20, 200]);
  });

  it('detects paper color from the border and finds opaque bounds', () => {
    const w = 5, h = 5;
    const img = new Uint8ClampedArray(w * h * 4).fill(240);
    const i = (2 * w + 2) * 4;
    img[i] = img[i + 1] = img[i + 2] = 0;
    expect(detectBackground(img, w, h)).toEqual({ r: 240, g: 240, b: 240 });
    const out = removeBackground(img, { threshold: 250, background: { r: 240, g: 240, b: 240 }, soft: false, unifyColor: null });
    expect(opaqueBounds(out, w, h)).toEqual({ x: 2, y: 2, width: 1, height: 1 });
  });
});
