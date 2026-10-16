import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import type { PageSize } from '../api';
import { renderScale, zoomAt, type View } from './view';

/** 그림을 미리 받아 두었다가 다 받으면 바꿔 끼운다 (확대할 때 깜빡이지 않게). */
function useProgressiveSrc(src: string | null): string | null {
  const [shown, setShown] = useState<string | null>(src);
  useEffect(() => {
    if (!src) {
      setShown(null);
      return;
    }
    let alive = true;
    const img = new Image();
    img.onload = () => alive && setShown(src);
    img.src = src;
    return () => {
      alive = false;
    };
  }, [src]);
  return shown;
}

/** 페이지 위에 겹칠 그림 층: 지워진 선(곱하기 혼합), 겹쳐 보기의 원본(투명도) 등. */
export type Layer = { key: string; url: (scale: number) => string; opacity: number; blend?: 'multiply' | 'normal' };

type Props = {
  page: PageSize;
  imageUrl: (scale: number) => string;
  view: View | null;
  onViewChange: (v: View) => void;
  onResize?: (size: { width: number; height: number }) => void;
  layers?: Layer[];
  /** 페이지 좌표(viewBox = 페이지 pt)로 그리는 표시. */
  overlay?: ReactNode;
  /** 표시가 아닌 곳(빈 곳)을 눌렀을 때 (선택 해제용). */
  onBackgroundPointerDown?: () => void;
  label: string;
};

function LayerImage({ layer, scale }: { layer: Layer; scale: number }) {
  const src = useProgressiveSrc(layer.url(scale));
  if (!src) return null;
  return (
    <img src={src} alt="" draggable={false} className="absolute inset-0 h-full w-full select-none"
      style={{ opacity: layer.opacity, mixBlendMode: layer.blend === 'multiply' ? 'multiply' : 'normal' }} />
  );
}

/**
 * 도면 한 페이지 보기: 휠로 확대·축소(커서 기준), 끌어서 이동.
 * 보기 상태(view)는 부모가 갖고 있어 두 칸을 함께 움직일 수 있다.
 */
export function PageViewer({ page, imageUrl, view, onViewChange, onResize, layers, overlay, onBackgroundPointerDown, label }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const drag = useRef<{ x: number; y: number; view: View } | null>(null);
  const dpr = typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1;

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el || !onResize) return;
    const ro = new ResizeObserver(() => onResize({ width: el.clientWidth, height: el.clientHeight }));
    ro.observe(el);
    onResize({ width: el.clientWidth, height: el.clientHeight });
    return () => ro.disconnect();
  }, [onResize]);

  // 휠 확대는 페이지 스크롤을 막아야 하므로 passive: false로 직접 등록한다
  useEffect(() => {
    const el = ref.current;
    if (!el || !view) return;
    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const rect = el.getBoundingClientRect();
      const factor = Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0015));
      onViewChange(zoomAt(view, factor, e.clientX - rect.left, e.clientY - rect.top));
    };
    el.addEventListener('wheel', onWheel, { passive: false });
    return () => el.removeEventListener('wheel', onWheel);
  }, [view, onViewChange]);

  const scale = view ? renderScale(view.zoom, dpr) : 1;
  const src = useProgressiveSrc(view ? imageUrl(scale) : null);

  const box = view
    ? { left: view.x, top: view.y, width: page.width * view.zoom, height: page.height * view.zoom }
    : undefined;

  return (
    <div
      ref={ref}
      role="img"
      aria-label={label}
      className="relative h-full w-full cursor-grab overflow-hidden bg-canvas active:cursor-grabbing"
      onPointerDown={(e) => {
        if (!view || (e.button !== 0 && e.button !== 1)) return;
        onBackgroundPointerDown?.();
        (e.target as Element).setPointerCapture?.(e.pointerId);
        drag.current = { x: e.clientX, y: e.clientY, view };
      }}
      onPointerMove={(e) => {
        const d = drag.current;
        if (!d) return;
        onViewChange({ ...d.view, x: d.view.x + e.clientX - d.x, y: d.view.y + e.clientY - d.y });
      }}
      onPointerUp={() => (drag.current = null)}
      onPointerCancel={() => (drag.current = null)}
    >
      {box && (
        <div className="absolute bg-white shadow-[0_1px_3px_rgba(16,24,40,0.12),0_8px_24px_rgba(16,24,40,0.08)]" style={box}>
          {src && <img src={src} alt="" draggable={false} className="absolute inset-0 h-full w-full select-none" />}
          {layers?.map((l) => <LayerImage key={l.key} layer={l} scale={scale} />)}
          {overlay && (
            <svg className="absolute inset-0 h-full w-full" viewBox={`0 0 ${page.width} ${page.height}`} preserveAspectRatio="none">
              {overlay}
            </svg>
          )}
        </div>
      )}
    </div>
  );
}
