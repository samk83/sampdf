import type { PageSize, Rect } from '../api';

/** 화면 보기 상태: zoom = 1pt당 화면 픽셀, x·y = 칸 안에서 페이지 왼쪽 위가 놓인 화면 위치(px). */
export type View = { zoom: number; x: number; y: number };

export const MIN_ZOOM = 0.05;
export const MAX_ZOOM = 12;

export const clampZoom = (z: number) => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z));

/** 페이지 전체가 칸에 들어오도록 맞춘 보기 (여백 24px). */
export function fitView(page: PageSize, pane: { width: number; height: number }, margin = 24): View {
  const zoom = clampZoom(Math.min((pane.width - margin * 2) / page.width, (pane.height - margin * 2) / page.height));
  return { zoom, x: (pane.width - page.width * zoom) / 2, y: (pane.height - page.height * zoom) / 2 };
}

/** 화면의 한 점(px)을 고정한 채 확대·축소. */
export function zoomAt(v: View, factor: number, px: number, py: number): View {
  const zoom = clampZoom(v.zoom * factor);
  const k = zoom / v.zoom;
  return { zoom, x: px - (px - v.x) * k, y: py - (py - v.y) * k };
}

/** 영역(pt)이 칸 가운데 오도록 이동하고, 영역이 칸의 약 40%를 차지하도록 확대 (너무 크게 키우지는 않음). */
export function focusRect(r: Rect, pane: { width: number; height: number }, current: View): View {
  const w = Math.max(r.x1 - r.x0, 20);
  const h = Math.max(r.y1 - r.y0, 20);
  const zoom = clampZoom(Math.min(Math.max(current.zoom, Math.min((pane.width * 0.4) / w, (pane.height * 0.4) / h)), 4));
  const cx = (r.x0 + r.x1) / 2;
  const cy = (r.y0 + r.y1) / 2;
  return { zoom, x: pane.width / 2 - cx * zoom, y: pane.height / 2 - cy * zoom };
}

/** 서버에 요청할 그림 배율: 화면에 필요한 해상도 이상인 가장 작은 단계 (같은 그림을 다시 받지 않도록 단계로 나눈다). */
const STEPS = [0.5, 1, 1.5, 2, 3, 4, 6, 8];
export function renderScale(zoom: number, dpr: number): number {
  const need = zoom * dpr;
  return STEPS.find((s) => s >= need) ?? STEPS[STEPS.length - 1];
}
