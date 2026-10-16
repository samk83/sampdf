import { useRef } from 'react';
import type { Change, PageSize, Rect } from '../api';
import { clampRect, cloudPath, deltaAt, deltaTriangle, inflate } from './geometry';

/** 사용자가 고친 표시 하나 (없는 값은 자동). 내려받을 때 그대로 서버로 보낸다. */
export type MarkEdit = { cloud?: Rect; delta?: { x: number; y: number }; highlights?: Rect[] };

export const hasCloud = (c: Change, cloudText: boolean) => c.category === 'graphic' || c.highlights.length === 0 || cloudText;
export const autoCloud = (c: Change, page: PageSize) => clampRect(inflate(c.bounds, 6), page.width, page.height);
export const autoHighlights = (c: Change) => c.highlights.map((h) => inflate(h, 1));

type Drag =
  | { kind: 'move-cloud' | 'move-delta'; id: string; x: number; y: number; cloud: Rect; delta: { x: number; y: number } }
  | { kind: 'resize-cloud'; id: string; corner: number; x: number; y: number; cloud: Rect }
  | { kind: 'move-hl' | 'resize-hl'; id: string; index: number; corner?: number; x: number; y: number; highlights: Rect[] };

type Props = {
  changes: Change[];
  page: PageSize;
  labels: Record<string, string>;
  cloudText: boolean;
  edits: Record<string, MarkEdit>;
  selectedId: string | null;
  zoom: number;
  onSelect: (id: string) => void;
  onEdit: (id: string, edit: MarkEdit) => void;
};

const MIN = 6; // 크기를 줄일 때 최소 크기 (pt)

/** 모서리 번호 0=왼쪽 위, 1=오른쪽 위, 2=오른쪽 아래, 3=왼쪽 아래 를 끌어 사각형 크기를 바꾼다. */
function resize(r: Rect, corner: number, dx: number, dy: number): Rect {
  const n = { ...r };
  if (corner === 0 || corner === 3) n.x0 = Math.min(r.x0 + dx, r.x1 - MIN);
  else n.x1 = Math.max(r.x1 + dx, r.x0 + MIN);
  if (corner === 0 || corner === 1) n.y0 = Math.min(r.y0 + dy, r.y1 - MIN);
  else n.y1 = Math.max(r.y1 + dy, r.y0 + MIN);
  return n;
}

const shift = (r: Rect, dx: number, dy: number): Rect => ({ x0: r.x0 + dx, y0: r.y0 + dy, x1: r.x1 + dx, y1: r.y1 + dy });
const corners = (r: Rect) => [[r.x0, r.y0], [r.x1, r.y0], [r.x1, r.y1], [r.x0, r.y1]] as const;
const CURSORS = ['nwse-resize', 'nesw-resize', 'nwse-resize', 'nesw-resize'];

/**
 * 리비전 도면 위의 표시 (화면용, 수정 가능). PDF로 내려받을 때 엔진이 넣는 표시와 같은 규칙:
 * 글자 변경 → 노란 하이라이트, 그림 변경(설정에 따라 글자 변경도) → 빨간 구름 + △ 번호.
 * 표시를 누르면 선택되고, 끌어서 옮기거나 모서리로 크기를 바꿀 수 있다. △는 따로 옮길 수 있다.
 */
export function RevisionMarks({ changes, page, labels, cloudText, edits, selectedId, zoom, onSelect, onEdit }: Props) {
  const drag = useRef<Drag | null>(null);
  const stroke = Math.max(1.5, 1.2 / zoom); // 화면에서 너무 가늘어지지 않게
  const handle = 9 / zoom; // 손잡이 크기: 화면에서 약 9px
  const hit = 12 / zoom; // 구름 선을 잡기 쉬운 굵기

  const start = (e: React.PointerEvent, d: Drag) => {
    e.stopPropagation(); // 도면 이동(끌기)으로 넘어가지 않게
    (e.currentTarget as Element).setPointerCapture(e.pointerId);
    drag.current = d;
    onSelect(d.id);
  };

  const move = (e: React.PointerEvent) => {
    const d = drag.current;
    if (!d) return;
    const dx = (e.clientX - d.x) / zoom;
    const dy = (e.clientY - d.y) / zoom;
    const prev = edits[d.id] ?? {};
    if (d.kind === 'move-cloud') onEdit(d.id, { ...prev, cloud: shift(d.cloud, dx, dy), delta: prev.delta ? { x: d.delta.x + dx, y: d.delta.y + dy } : undefined });
    else if (d.kind === 'move-delta') onEdit(d.id, { ...prev, delta: { x: d.delta.x + dx, y: d.delta.y + dy } });
    else if (d.kind === 'resize-cloud') onEdit(d.id, { ...prev, cloud: resize(d.cloud, d.corner, dx, dy) });
    else if (d.kind === 'move-hl' || d.kind === 'resize-hl') {
      const hl = d.highlights.map((h, i) => (i !== d.index ? h : d.kind === 'move-hl' ? shift(h, dx, dy) : resize(h, d.corner!, dx, dy)));
      onEdit(d.id, { ...prev, highlights: hl });
    }
  };

  const end = () => (drag.current = null);

  return (
    <g onPointerMove={move} onPointerUp={end} onPointerCancel={end}>
      {changes.map((c) => {
        const e = edits[c.id] ?? {};
        const selected = c.id === selectedId;
        const highlights = e.highlights ?? autoHighlights(c);
        return (
          <g key={`${c.id}-hl`}>
            {highlights.map((h, i) => (
              <g key={i}>
                <rect x={h.x0} y={h.y0} width={h.x1 - h.x0} height={h.y1 - h.y0} fill="#facc15" opacity={0.45}
                  style={{ mixBlendMode: 'multiply', cursor: 'move' }}
                  onPointerDown={(ev) => start(ev, { kind: 'move-hl', id: c.id, index: i, x: ev.clientX, y: ev.clientY, highlights })} />
                {selected && corners(h).map(([x, y], k) => (
                  <rect key={k} x={x - handle / 2} y={y - handle / 2} width={handle} height={handle} fill="#ffffff" stroke="#a16207"
                    strokeWidth={1.2 / zoom} style={{ cursor: CURSORS[k] }}
                    onPointerDown={(ev) => start(ev, { kind: 'resize-hl', id: c.id, index: i, corner: k, x: ev.clientX, y: ev.clientY, highlights })} />
                ))}
              </g>
            ))}
          </g>
        );
      })}
      {changes.filter((c) => hasCloud(c, cloudText)).map((c) => {
        const e = edits[c.id] ?? {};
        const selected = c.id === selectedId;
        const label = labels[c.id] ?? '1';
        const cloud = e.cloud ?? autoCloud(c, page);
        const t = e.delta ? deltaAt(e.delta, label, page.width, page.height) : deltaTriangle(cloud, label, page.width, page.height);
        const deltaCenter = { x: t.labelX, y: t.labelY };
        const path = cloudPath(cloud);
        return (
          <g key={c.id}>
            {/* 선택하면 구름 안쪽 전체로 끌어 옮길 수 있다. 선택 전에는 안쪽을 비워 도면 이동을 막지 않는다 */}
            {selected && (
              <rect x={cloud.x0} y={cloud.y0} width={cloud.x1 - cloud.x0} height={cloud.y1 - cloud.y0} fill="transparent" style={{ cursor: 'move' }}
                onPointerDown={(ev) => start(ev, { kind: 'move-cloud', id: c.id, x: ev.clientX, y: ev.clientY, cloud, delta: deltaCenter })} />
            )}
            <path d={path} fill="none" stroke="#dc2626" strokeWidth={stroke} strokeLinejoin="round" pointerEvents="none" />
            <path d={path} fill="none" stroke="transparent" strokeWidth={hit} style={{ cursor: 'move' }}
              onPointerDown={(ev) => start(ev, { kind: 'move-cloud', id: c.id, x: ev.clientX, y: ev.clientY, cloud, delta: deltaCenter })} />
            <g style={{ cursor: 'grab' }} onPointerDown={(ev) => start(ev, { kind: 'move-delta', id: c.id, x: ev.clientX, y: ev.clientY, cloud, delta: deltaCenter })}>
              <polygon points={t.points} fill="#ffffff" fillOpacity={0.85} stroke="#dc2626" strokeWidth={stroke} strokeLinejoin="round" />
              <text x={t.labelX} y={t.labelY} fill="#dc2626" fontSize={t.fontSize} fontWeight={700} textAnchor="middle"
                dominantBaseline="central" fontFamily="Helvetica, Arial, sans-serif" style={{ userSelect: 'none' }}>
                {label}
              </text>
            </g>
            {selected && corners(cloud).map(([x, y], k) => (
              <rect key={k} x={x - handle / 2} y={y - handle / 2} width={handle} height={handle} fill="#ffffff" stroke="#1d4ed8"
                strokeWidth={1.2 / zoom} style={{ cursor: CURSORS[k] }}
                onPointerDown={(ev) => start(ev, { kind: 'resize-cloud', id: c.id, corner: k, x: ev.clientX, y: ev.clientY, cloud })} />
            ))}
          </g>
        );
      })}
      <Selection changes={changes} selectedId={selectedId} zoom={zoom} useOld={false} edits={edits} page={page} cloudText={cloudText} />
    </g>
  );
}

/** 선택한 변경을 파란 점선 상자로 (원본 칸에서는 원본 위치로). */
export function Selection({ changes, selectedId, zoom, useOld, edits, page, cloudText }: {
  changes: Change[]; selectedId: string | null; zoom: number; useOld: boolean;
  edits?: Record<string, MarkEdit>; page?: PageSize; cloudText?: boolean;
}) {
  const c = changes.find((x) => x.id === selectedId);
  if (!c) return null;
  const edited = !useOld && edits?.[c.id]?.cloud;
  const base = useOld ? c.boundsInOld : edited ? edited : page && hasCloud(c, cloudText ?? false) ? autoCloud(c, page) : c.bounds;
  const r = inflate(base, 8);
  return (
    <rect x={r.x0} y={r.y0} width={r.x1 - r.x0} height={r.y1 - r.y0} rx={4} fill="none" stroke="#1d4ed8" pointerEvents="none"
      strokeWidth={Math.max(1.5, 2 / zoom)} strokeDasharray={`${5 / Math.min(zoom, 1)} ${4 / Math.min(zoom, 1)}`} />
  );
}
