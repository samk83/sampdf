import { useEffect, useRef, useState } from 'react';
import { Download, Pipette, X } from 'lucide-react';
import { saveBlob } from '../api';
import { DropZone } from '../compare/DropZone';
import { AppHeader } from '../ui/AppHeader';
import { detectBackground, fromHex, opaqueBounds, removeBackground, toHex, type Rgb } from './removeBackground';

const ACCEPT = '.png,.jpg,.jpeg,.bmp,.webp,image/png,image/jpeg,image/bmp,image/webp';
/** 너무 큰 그림은 브라우저가 느려지므로 긴 변을 이 크기로 줄여 처리한다. */
const MAX_EDGE = 6000;

type Loaded = { name: string; width: number; height: number; pixels: ImageData };

/** 그림 파일 → 픽셀 (브라우저 안에서만). */
async function loadImage(file: File): Promise<Loaded> {
  const bitmap = await createImageBitmap(file);
  const k = Math.min(1, MAX_EDGE / Math.max(bitmap.width, bitmap.height));
  const width = Math.max(1, Math.round(bitmap.width * k));
  const height = Math.max(1, Math.round(bitmap.height * k));
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d')!;
  ctx.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();
  return { name: file.name, width, height, pixels: ctx.getImageData(0, 0, width, height) };
}

/**
 * 투명 PNG 만들기: 흰(또는 한 가지 색) 배경을 지운다. 도장·서명·로고·스캔한 도면 조각용.
 * 슬라이더를 움직이면 바로 다시 계산한다. 그림은 서버로 보내지 않는다.
 */
export function PngTool({ initialFile }: { initialFile?: File }) {
  const [image, setImage] = useState<Loaded | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [threshold, setThreshold] = useState(232);
  const [background, setBackground] = useState<Rgb>({ r: 255, g: 255, b: 255 });
  const [autoBackground, setAutoBackground] = useState(true);
  const [soft, setSoft] = useState(true);
  const [trim, setTrim] = useState(true);
  const [unify, setUnify] = useState(false);
  const [unifyColor, setUnifyColor] = useState('#000000');
  const [picking, setPicking] = useState(false);
  const [resultSize, setResultSize] = useState<{ width: number; height: number } | null>(null);
  const originalCanvas = useRef<HTMLCanvasElement>(null);
  const resultCanvas = useRef<HTMLCanvasElement>(null);

  const open = async (file: File) => {
    setBusy(true);
    setError(null);
    try {
      const loaded = await loadImage(file);
      setImage(loaded);
      setBackground(detectBackground(loaded.pixels.data, loaded.width, loaded.height));
      setAutoBackground(true);
    } catch {
      setError('그림을 열 수 없어요. PNG, JPG, BMP, WEBP 파일인지 확인해 주세요.');
    } finally {
      setBusy(false);
    }
  };

  // 시작 화면에서 끌어다 놓은 그림으로 탭이 열리면 바로 연다
  const initialOpened = useRef(false);
  useEffect(() => {
    if (initialOpened.current || !initialFile) return;
    initialOpened.current = true;
    void open(initialFile);
  }, []);

  // 원래 그림 그리기
  useEffect(() => {
    const c = originalCanvas.current;
    if (!c || !image) return;
    c.width = image.width;
    c.height = image.height;
    c.getContext('2d')!.putImageData(image.pixels, 0, 0);
  }, [image]);

  // 결과 다시 계산 (설정을 바꾸면 잠깐 기다렸다가: 슬라이더를 끄는 동안 계속 계산하지 않도록)
  useEffect(() => {
    if (!image) return;
    const timer = setTimeout(() => {
      const data = removeBackground(image.pixels.data, {
        threshold, background, soft, unifyColor: unify ? fromHex(unifyColor) : null,
      });
      const full = new ImageData(data, image.width, image.height);
      const box = trim ? opaqueBounds(data, image.width, image.height) : null;
      const c = resultCanvas.current;
      if (!c) return;
      const w = box?.width ?? image.width, h = box?.height ?? image.height;
      c.width = w;
      c.height = h;
      c.getContext('2d')!.putImageData(full, -(box?.x ?? 0), -(box?.y ?? 0));
      setResultSize({ width: w, height: h });
    }, 60);
    return () => clearTimeout(timer);
  }, [image, threshold, background, soft, trim, unify, unifyColor]);

  const pickAt = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!picking || !image) return;
    const rect = e.currentTarget.getBoundingClientRect();
    const x = Math.floor(((e.clientX - rect.left) / rect.width) * image.width);
    const y = Math.floor(((e.clientY - rect.top) / rect.height) * image.height);
    const i = (Math.min(image.height - 1, Math.max(0, y)) * image.width + Math.min(image.width - 1, Math.max(0, x))) * 4;
    const d = image.pixels.data;
    setBackground({ r: d[i], g: d[i + 1], b: d[i + 2] });
    setAutoBackground(false);
    setPicking(false);
  };

  const download = () => {
    resultCanvas.current?.toBlob((blob) => {
      if (blob && image) saveBlob(blob, image.name.replace(/\.[^.]+$/, '') + ' (transparent).png');
    }, 'image/png');
  };

  return (
    <div className="flex h-full flex-col bg-[#f4f5f7] text-legacy-ink">
      <AppHeader right={
        <button type="button" onClick={download} disabled={!image}
          className="inline-flex h-[38px] items-center gap-2 rounded-lg bg-legacy-accent px-3.5 text-[14px] leading-[20px] font-semibold text-white hover:bg-[#1e40af] disabled:bg-[#c7cdd8]">
          <Download className="h-4 w-4" aria-hidden /> PNG 내려받기
        </button>
      } />
      <div className="flex min-h-0 flex-1">
        <div className="grid min-w-0 flex-1 grid-cols-2 gap-px bg-[#d5d9e0]">
          <section aria-label="원래 그림" className="flex min-h-0 flex-col bg-legacy-canvas">
            <div className="flex h-10 flex-none items-center justify-between border-b border-legacy-line bg-white pl-3.5 pr-2 text-[13px]">
              <div className="flex min-w-0 gap-2">
                <span className="font-bold text-[#4a505c]">원래 그림</span>
                {image && <span className="truncate">{image.name}</span>}
                {image && <span className="text-[#6b7280]">· {image.width} × {image.height}</span>}
              </div>
              {image && (
                <button type="button" aria-label="그림 닫기" onClick={() => { setImage(null); setResultSize(null); }}
                  className="flex h-8 w-8 items-center justify-center rounded-md text-legacy-muted hover:bg-[#eef0f3]">
                  <X className="h-4 w-4" aria-hidden />
                </button>
              )}
            </div>
            <div className="relative min-h-0 flex-1">
              {image ? (
                <div className="flex h-full items-center justify-center p-7">
                  <canvas ref={originalCanvas} onClick={pickAt} aria-label={picking ? '배경 색을 찍을 곳을 누르세요' : '원래 그림'}
                    className={`max-h-full max-w-full shadow-[0_1px_3px_rgba(16,24,40,0.12)] ${picking ? 'cursor-crosshair outline outline-2 outline-offset-4 outline-legacy-accent' : ''}`} />
                </div>
              ) : (
                <DropZone title="그림 열기" busy={busy} error={error} onFile={open} accept={ACCEPT} formats={['PNG', 'JPG', 'BMP', 'WEBP']} />
              )}
            </div>
          </section>
          <section aria-label="투명 결과" className="flex min-h-0 flex-col bg-legacy-canvas">
            <div className="flex h-10 flex-none items-center gap-2 border-b border-legacy-line bg-white px-3.5 text-[13px]">
              <span className="font-bold text-[#4a505c]">결과</span>
              <span className="text-[#6b7280]">바둑판 무늬 = 투명{resultSize ? ` · ${resultSize.width} × ${resultSize.height}` : ''}</span>
            </div>
            <div className="flex min-h-0 flex-1 items-center justify-center p-7">
              {image ? (
                <canvas ref={resultCanvas} aria-label="배경을 지운 결과" className="checker max-h-full max-w-full shadow-[0_1px_3px_rgba(16,24,40,0.12)]" />
              ) : (
                <p className="text-[14px] leading-[20px] text-legacy-muted">왼쪽에 그림을 열면 여기에 결과가 보여요.</p>
              )}
            </div>
          </section>
        </div>

        <aside aria-label="설정" className="flex w-[300px] flex-none flex-col gap-5 border-l border-legacy-line bg-white p-[18px]">
          <div className="flex flex-col gap-2.5">
            <div className="flex items-baseline justify-between">
              <label htmlFor="threshold" className="text-[14px] leading-[20px] font-semibold">배경 기준</label>
              <span className="font-mono text-[13px] text-[#3f4552]">{threshold}</span>
            </div>
            <input id="threshold" type="range" min={150} max={254} value={threshold} onChange={(e) => setThreshold(Number(e.target.value))} className="w-full accent-[#1d4ed8]" />
            <div className="flex justify-between text-[12px] leading-[16px] text-legacy-muted"><span>연한 얼룩까지</span><span>배경 색만</span></div>
          </div>

          <div className="flex flex-col gap-2.5">
            <div className="text-[14px] leading-[20px] font-semibold">배경 색</div>
            <div className="flex items-center gap-2.5">
              <span className="h-9 w-9 rounded-lg border border-[#c3c8d1]" style={{ background: toHex(background) }} />
              <span className="flex-1 font-mono text-[13px] text-[#3f4552]">{toHex(background)}{autoBackground ? ' (자동)' : ''}</span>
              <button type="button" aria-label="그림에서 배경 색 찍기" title="그림에서 배경 색 찍기" aria-pressed={picking} disabled={!image}
                onClick={() => setPicking((p) => !p)}
                className={`flex h-10 w-10 items-center justify-center rounded-lg border disabled:opacity-40 ${picking ? 'border-legacy-accent bg-legacy-accent-soft text-legacy-accent' : 'border-[#d5d9e0] text-[#3f4552] hover:bg-[#f4f5f7]'}`}>
                <Pipette className="h-[17px] w-[17px]" aria-hidden />
              </button>
            </div>
            {picking && <p className="text-[12px] leading-[16px] text-legacy-accent">왼쪽 그림에서 배경 부분을 누르세요.</p>}
          </div>

          <div className="flex flex-col">
            <Toggle label="가장자리 부드럽게" checked={soft} onChange={setSoft} />
            <Toggle label="남는 여백 자르기" checked={trim} onChange={setTrim} />
            <Toggle label="선 색을 하나로 맞추기" checked={unify} onChange={setUnify} />
            {unify && (
              <label className="flex items-center justify-between pb-2 pl-1 text-[13px] text-[#3f4552]">
                선 색
                <input type="color" value={unifyColor} onChange={(e) => setUnifyColor(e.target.value)} className="h-8 w-12 cursor-pointer rounded border border-[#d5d9e0]" />
              </label>
            )}
          </div>

          <p className="mt-auto text-[12px] leading-[16px] leading-relaxed text-legacy-muted">그림은 브라우저 안에서만 처리돼요. 슬라이더를 움직이면 결과가 바로 바뀝니다.</p>
        </aside>
      </div>
    </div>
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex min-h-11 cursor-pointer items-center justify-between text-[14px] leading-[20px]">
      {label}
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="h-[18px] w-[18px] accent-[#1d4ed8]" />
    </label>
  );
}
