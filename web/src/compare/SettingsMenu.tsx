import { useEffect, useRef, useState } from 'react';
import { Settings2 } from 'lucide-react';
import type { NumberingMode } from './numbering';
import { Segmented } from '../ui/Segmented';

export type MarkSettings = {
  numbering: NumberingMode;
  startLabel: string;
  cloudText: boolean;
  ignoreTitle: boolean;
};

/** "표시 설정" 버튼과 펼침 상자: △ 번호 방식·시작 번호, 글자 변경도 구름, 제목란 무시. */
export function SettingsMenu({ value, onChange }: { value: MarkSettings; onChange: (v: MarkSettings) => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (e: PointerEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    window.addEventListener('pointerdown', close);
    window.addEventListener('keydown', esc);
    return () => {
      window.removeEventListener('pointerdown', close);
      window.removeEventListener('keydown', esc);
    };
  }, [open]);
  const set = (patch: Partial<MarkSettings>) => onChange({ ...value, ...patch });
  return (
    <div ref={ref} className="relative">
      <button type="button" aria-expanded={open} onClick={() => setOpen((o) => !o)}
        className={`inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-[13px] ${open ? 'border-legacy-accent bg-legacy-accent-soft text-legacy-accent' : 'border-[#d5d9e0] text-[#3f4552] hover:bg-[#f4f5f7]'}`}>
        <Settings2 className="h-4 w-4" aria-hidden />
        표시 설정
      </button>
      {open && (
        <div role="dialog" aria-label="표시 설정" className="absolute right-0 top-11 z-20 flex w-[300px] flex-col gap-4 rounded-xl border border-legacy-line bg-white p-4 shadow-xl">
          <div className="flex flex-col gap-2">
            <span className="text-[13px] font-semibold">△ 리비전 번호</span>
            <Segmented label="번호 방식" value={value.numbering} onChange={(numbering) => set({ numbering })}
              options={[{ value: 'sequential', label: '하나씩 올라감' }, { value: 'same', label: '모두 같은 번호' }]} />
            <label className="flex items-center justify-between text-[13px] text-[#3f4552]">
              {value.numbering === 'sequential' ? '시작 번호' : '번호'}
              <input value={value.startLabel} maxLength={4} aria-label="리비전 번호" onChange={(e) => set({ startLabel: e.target.value })}
                className="h-9 w-20 rounded-md border border-[#c3c8d1] px-2 font-mono text-[14px] leading-[20px] text-legacy-ink" />
            </label>
            <span className="text-[12px] leading-[16px] text-legacy-muted">{value.numbering === 'sequential' ? '예: 1 → 1, 2, 3…  /  A → A, B, C…' : '모든 구름에 같은 번호를 붙여요.'}</span>
          </div>
          <label className="flex cursor-pointer items-center justify-between text-[13px]">
            글자 변경도 구름으로 표시
            <input type="checkbox" checked={value.cloudText} onChange={(e) => set({ cloudText: e.target.checked })} className="h-4 w-4 accent-[#1d4ed8]" />
          </label>
          <label className="flex cursor-pointer items-center justify-between text-[13px]">
            제목란(오른쪽 아래) 무시
            <input type="checkbox" checked={value.ignoreTitle} onChange={(e) => set({ ignoreTitle: e.target.checked })} className="h-4 w-4 accent-[#1d4ed8]" />
          </label>
        </div>
      )}
    </div>
  );
}
