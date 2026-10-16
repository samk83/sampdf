import { describe, expect, it } from 'vitest';
import { compareTitle, routeDrop } from './dropRouting';

const file = (name: string, type = '') => new File(['x'], name, { type });

describe('시작 화면에 끌어다 놓기', () => {
  it('두 개면 비교 (앞의 두 개, 첫 파일이 원본)', () => {
    const r = routeDrop([file('a.pdf'), file('b.pdf'), file('c.pdf')]);
    expect(r.kind).toBe('compare');
    expect(r.kind === 'compare' && r.files.map((f) => f.name)).toEqual(['a.pdf', 'b.pdf']);
  });

  it('그림 하나는 투명 PNG', () => {
    expect(routeDrop([file('stamp.JPG')]).kind).toBe('png');
    expect(routeDrop([file('scan', 'image/png')]).kind).toBe('png');
  });

  it('PDF 하나는 비교의 원본으로 (도면 탭은 U6)', () => {
    expect(routeDrop([file('S-101.pdf', 'application/pdf')]).kind).toBe('compare');
  });

  it('지원하지 않는 파일만 있으면 안내', () => {
    expect(routeDrop([file('notes.docx')]).kind).toBe('unsupported');
    expect(routeDrop([]).kind).toBe('unsupported');
  });

  it('지원하지 않는 파일은 빼고 센다', () => {
    const r = routeDrop([file('notes.txt'), file('a.pdf')]);
    expect(r.kind).toBe('compare');
  });

  it('비교 탭 이름은 확장자를 뺀 파일 이름', () => {
    expect(compareTitle('Compare', [file('S-101 rev0.pdf'), file('S-101 rev1.pdf')])).toBe('Compare · S-101 rev0 ↔ S-101 rev1');
    expect(compareTitle('Compare', [])).toBeUndefined();
  });
});
