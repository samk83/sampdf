import { useRef, useState, type ReactNode } from 'react';

/**
 * 좌우 두 칸과 가운데 구분선. 구분선을 끌면 칸 너비가 바뀌고(20~80%), 두 번 누르면 반반으로 돌아간다.
 * 키보드: 구분선에 초점을 두고 ← → 로 조절.
 */
export function SplitPanes({ left, right }: { left: ReactNode; right: ReactNode }) {
  const [ratio, setRatio] = useState(0.5);
  const box = useRef<HTMLDivElement>(null);
  const dragging = useRef(false);
  const set = (r: number) => setRatio(Math.min(0.8, Math.max(0.2, r)));
  return (
    <div ref={box} className="flex min-h-0 min-w-0 flex-1"
      onPointerMove={(e) => {
        if (!dragging.current || !box.current) return;
        const rect = box.current.getBoundingClientRect();
        set((e.clientX - rect.left) / rect.width);
      }}
      onPointerUp={() => (dragging.current = false)}>
      <div className="flex min-w-0" style={{ width: `${ratio * 100}%` }}>{left}</div>
      <div
        role="separator"
        aria-orientation="vertical"
        aria-label="칸 나누기 (끌어서 크기 조절, 두 번 누르면 반반)"
        aria-valuenow={Math.round(ratio * 100)}
        tabIndex={0}
        onPointerDown={(e) => {
          (e.currentTarget as Element).setPointerCapture(e.pointerId);
          dragging.current = true;
        }}
        onDoubleClick={() => setRatio(0.5)}
        onKeyDown={(e) => {
          if (e.key === 'ArrowLeft') set(ratio - 0.02);
          if (e.key === 'ArrowRight') set(ratio + 0.02);
        }}
        className="group relative flex w-2.5 flex-none cursor-col-resize items-center justify-center bg-[#c3c8d1] hover:bg-accent/60 focus:bg-accent/60 focus:outline-none"
      >
        <span className="flex flex-col gap-1" aria-hidden>
          {[0, 1, 2].map((i) => <span key={i} className="h-1 w-1 rounded-full bg-white/90" />)}
        </span>
      </div>
      <div className="flex min-w-0 flex-1">{right}</div>
    </div>
  );
}
