import { useRef, useState } from 'react';
import { Loader2, Upload } from 'lucide-react';

export const ACCEPT = '.pdf,.png,.jpg,.jpeg,.tif,.tiff,.bmp,application/pdf,image/png,image/jpeg,image/tiff,image/bmp';
const FORMATS = ['PDF', 'PNG', 'JPG', 'TIFF'];

type Props = {
  title: string;
  busy: boolean;
  error: string | null;
  onFile: (file: File) => void;
  accept?: string;
  formats?: string[];
};

/** 빈 칸 가운데의 "파일 열기": 클릭하거나 끌어다 놓기. 칸 전체가 끌어다 놓기 영역이다. */
export function DropZone({ title, busy, error, onFile, accept = ACCEPT, formats = FORMATS }: Props) {
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const pick = (files: FileList | null) => {
    const f = files?.[0];
    if (f) onFile(f);
  };
  return (
    <div
      className="flex h-full w-full items-center justify-center p-6"
      onDragOver={(e) => {
        e.preventDefault();
        setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setOver(false);
        pick(e.dataTransfer.files);
      }}
    >
      <button
        type="button"
        disabled={busy}
        onClick={() => input.current?.click()}
        className={`flex min-h-[300px] w-full max-w-[520px] flex-col items-center justify-center gap-3.5 rounded-2xl border-2 border-dashed p-8 text-center transition-colors ${
          over ? 'border-accent bg-accent/10' : 'border-[#9aa3b2] bg-white/60 hover:border-accent hover:bg-accent/5'
        }`}
      >
        <span className="flex h-14 w-14 items-center justify-center rounded-[14px] bg-accent-soft text-accent">
          {busy ? <Loader2 className="h-7 w-7 animate-spin" aria-hidden /> : <Upload className="h-7 w-7" aria-hidden />}
        </span>
        <span className="text-lg font-bold">{busy ? '여는 중…' : title}</span>
        <span className="text-sm text-[#4a505c]">클릭하거나 여기로 끌어다 놓으세요</span>
        <span className="flex flex-wrap justify-center gap-1.5">
          {formats.map((f) => (
            <span key={f} className="rounded-full border border-[#d5d9e0] bg-white px-2 py-0.5 text-xs text-[#3f4552]">{f}</span>
          ))}
        </span>
        {error && <span role="alert" className="max-w-[420px] text-sm font-medium text-red-700">{error}</span>}
      </button>
      <input ref={input} type="file" accept={accept} className="hidden" onChange={(e) => { pick(e.target.files); e.target.value = ''; }} />
    </div>
  );
}
