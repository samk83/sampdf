import type { Rect } from '../api';

// 화면 표시용 도형 계산. 엔진(RevisionMarks.cs)이 PDF에 넣는 모양과 같은 규칙을 따른다.

export const inflate = (r: Rect, d: number): Rect => ({ x0: r.x0 - d, y0: r.y0 - d, x1: r.x1 + d, y1: r.y1 + d });

export const clampRect = (r: Rect, w: number, h: number): Rect => ({
  x0: Math.max(0, r.x0),
  y0: Math.max(0, r.y0),
  x1: Math.min(w, r.x1),
  y1: Math.min(h, r.y1),
});

/** 사각형을 따라 바깥으로 볼록한 호를 이어 그린 구름 모양 SVG 경로. 호 하나의 길이는 약 arc pt. */
export function cloudPath(r: Rect, arc = 12): string {
  const pts: [number, number][] = [
    [r.x0, r.y0],
    [r.x1, r.y0],
    [r.x1, r.y1],
    [r.x0, r.y1],
  ];
  let d = `M${r.x0.toFixed(1)} ${r.y0.toFixed(1)}`;
  for (let k = 0; k < 4; k++) {
    const [ax, ay] = pts[k];
    const [bx, by] = pts[(k + 1) % 4];
    const len = Math.hypot(bx - ax, by - ay);
    const n = Math.max(1, Math.round(len / arc));
    for (let i = 1; i <= n; i++) {
      const x = ax + ((bx - ax) * i) / n;
      const y = ay + ((by - ay) * i) / n;
      const radius = len / n / 2 + 0.6;
      // 시계 방향으로 도는 사각형에서 sweep=1이면 호가 바깥으로 볼록하다 (화면 좌표는 y가 아래로)
      d += ` A${radius.toFixed(1)} ${radius.toFixed(1)} 0 0 1 ${x.toFixed(1)} ${y.toFixed(1)}`;
    }
  }
  return d + ' Z';
}

/** △ 리비전 기호 위치: 구름 오른쪽 위 바깥. 위 공간이 없으면 아래로, 페이지 밖이면 안쪽으로. */
export function deltaTriangle(cloud: Rect, label: string, pageW: number, pageH: number, size = 20) {
  const s = size * (1 + 0.3 * Math.max(0, label.length - 1));
  const h = (s * Math.sqrt(3)) / 2;
  const anchor = inflate(cloud, 5);
  const cx = Math.min(Math.max(anchor.x1, s / 2 + 1), pageW - s / 2 - 1);
  let bottom = anchor.y0 - 2;
  if (bottom - h < 1) bottom = Math.min(pageH - 1, anchor.y1 + 2 + h);
  const top = bottom - h;
  return {
    points: `${cx},${top} ${cx + s / 2},${bottom} ${cx - s / 2},${bottom}`,
    labelX: cx,
    labelY: top + (h * 2) / 3,
    fontSize: Math.max(6, s * 0.5),
  };
}

/** 엔진 설명 문장('Text "BEAM" → "COLUMN"')에서 화면용 짧은 글자를 뽑는다. */
export function shortText(description: string): string | null {
  const change = /"([^"]*)" → "([^"]*)"/.exec(description);
  if (change) return `${change[1]} → ${change[2]}`;
  const added = /added "([^"]*)"/i.exec(description);
  if (added) return `+ ${added[1]}`;
  const removed = /removed "([^"]*)"/i.exec(description);
  if (removed) return `− ${removed[1]}`;
  return null;
}
