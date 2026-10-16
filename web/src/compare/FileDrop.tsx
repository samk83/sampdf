import { useState } from 'react';
import { FolderOpen, Loader2, Upload } from 'lucide-react';
import { useT } from '../i18n';
import { Button } from '../ui/Button';

export const ACCEPT = '.pdf,.png,.jpg,.jpeg,.tif,.tiff,.bmp,application/pdf,image/png,image/jpeg,image/tiff,image/bmp';

type Props = { title: string; busy: boolean; onFiles: (files: File[]) => void; onBrowse: () => void };

/** 빈 도면 칸 (시안 02): 칸 전체에 끌어다 놓기 + Browse files. 두 파일을 한 번에 놓으면 양쪽에 (부르는 쪽이 나눔). */
export function FileDrop({ title, busy, onFiles, onBrowse }: Props) {
  const t = useT();
  const [over, setOver] = useState(false);
  return (
    <div className="flex h-full w-full bg-canvas p-[22px]"
      onDragOver={(e) => { e.preventDefault(); setOver(true); }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        e.stopPropagation();
        setOver(false);
        const files = [...e.dataTransfer.files];
        if (files.length) onFiles(files.slice(0, 2));
      }}>
      <div className={`flex flex-1 flex-col items-center justify-center gap-3 rounded-sm border-2 border-dashed p-6 text-center transition-colors duration-fast ${
        over ? 'border-accent bg-accent-subtle' : 'border-accent'}`}>
        <span className="mb-1 flex h-16 w-16 items-center justify-center rounded-full bg-pressed text-accent">
          {busy ? <Loader2 className="h-7 w-7 animate-spin motion-reduce:animate-none" aria-hidden /> : <Upload className="h-7 w-7" aria-hidden />}
        </span>
        <span className="text-lg font-bold text-fg">{busy ? t('compare.drop.opening') : title}</span>
        <span className="text-md text-fg-muted">{t('compare.drop.formats')}</span>
        <div className="flex gap-2 pt-2">
          <Button variant="primary" icon={<FolderOpen className="h-icon w-icon" aria-hidden />} disabled={busy} onClick={onBrowse}>{t('compare.drop.browse')}</Button>
        </div>
        <span className="pt-3 text-sm text-fg-muted">{t('compare.drop.tip')}</span>
      </div>
    </div>
  );
}
