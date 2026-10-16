import type { Change, PageSize } from '../api';
import { clampRect, cloudPath, deltaTriangle, inflate } from './geometry';

type Props = {
  changes: Change[];
  page: PageSize;
  revisionLabel: string;
  selectedId: string | null;
  zoom: number;
};

/**
 * 리비전 도면 위의 표시 (화면용). PDF로 내려받을 때 엔진이 넣는 표시와 같은 규칙:
 * 글자 변경 → 노란 하이라이트, 그림 변경 → 빨간 구름 + △ 리비전 번호.
 */
export function RevisionMarks({ changes, page, revisionLabel, selectedId, zoom }: Props) {
  // 화면에서 너무 가늘어지지 않도록 선 굵기 하한을 둔다 (화면 1px 이상)
  const stroke = Math.max(1.5, 1.2 / zoom);
  return (
    <g>
      {changes.flatMap((c) =>
        c.highlights.map((h, i) => (
          <rect key={`${c.id}-h${i}`} x={h.x0 - 1} y={h.y0 - 1} width={h.x1 - h.x0 + 2} height={h.y1 - h.y0 + 2}
            fill="#facc15" opacity={0.45} style={{ mixBlendMode: 'multiply' }} />
        )),
      )}
      {changes
        .filter((c) => c.category === 'graphic' || c.highlights.length === 0)
        .map((c) => {
          const cloud = clampRect(inflate(c.bounds, 6), page.width, page.height);
          const t = deltaTriangle(cloud, revisionLabel, page.width, page.height);
          return (
            <g key={c.id}>
              <path d={cloudPath(cloud)} fill="none" stroke="#dc2626" strokeWidth={stroke} strokeLinejoin="round" />
              <polygon points={t.points} fill="#ffffff" fillOpacity={0.85} stroke="#dc2626" strokeWidth={stroke} strokeLinejoin="round" />
              <text x={t.labelX} y={t.labelY} fill="#dc2626" fontSize={t.fontSize} fontWeight={700} textAnchor="middle"
                dominantBaseline="central" fontFamily="Helvetica, Arial, sans-serif">
                {revisionLabel}
              </text>
            </g>
          );
        })}
      <Selection changes={changes} selectedId={selectedId} zoom={zoom} useOld={false} />
    </g>
  );
}

/** 선택한 변경을 파란 점선 상자로 (원본 칸에서는 원본 위치로). */
export function Selection({ changes, selectedId, zoom, useOld }: { changes: Change[]; selectedId: string | null; zoom: number; useOld: boolean }) {
  const c = changes.find((x) => x.id === selectedId);
  if (!c) return null;
  const r = inflate(useOld ? c.boundsInOld : c.bounds, 12);
  return (
    <rect x={r.x0} y={r.y0} width={r.x1 - r.x0} height={r.y1 - r.y0} rx={4} fill="none" stroke="#1d4ed8"
      strokeWidth={Math.max(1.5, 2 / zoom)} strokeDasharray={`${5 / Math.min(zoom, 1)} ${4 / Math.min(zoom, 1)}`} />
  );
}
