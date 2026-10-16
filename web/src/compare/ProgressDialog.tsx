import { useEffect, useState } from 'react';
import { Check, Loader2 } from 'lucide-react';
import { useT, type MessageKey } from '../i18n';
import { Button } from '../ui/Button';

type Props = { onCancel: () => void };

/**
 * 비교 중 창 (시안 03). 서버가 단계별 진행을 알려 주지 않아서 % 대신 움직이는 막대와 지난 시간을 보여 준다
 * (단계: 파일 올리기 ✓ → 비교 중 → 구름·△ 붙이기). Esc·Cancel = 비교 취소 (서버 작업도 멈춤).
 */
export function ProgressDialog({ onCancel }: Props) {
  const t = useT();
  const [seconds, setSeconds] = useState(0);
  useEffect(() => {
    const started = Date.now();
    const timer = setInterval(() => setSeconds(Math.floor((Date.now() - started) / 1000)), 500);
    return () => clearInterval(timer);
  }, []);

  const steps: { key: MessageKey; state: 'done' | 'now' | 'next' }[] = [
    { key: 'compare.progress.files', state: 'done' },
    { key: 'compare.progress.compare', state: 'now' },
    { key: 'compare.progress.marks', state: 'next' },
  ];

  return (
    <div className="absolute inset-0 z-floating flex items-center justify-center">
      <div role="dialog" aria-modal="false" aria-label={t('compare.progress.title')}
        className="flex w-[444px] max-w-[calc(100%-32px)] flex-col gap-4 rounded-lg border border-strong bg-surface p-[22px] shadow-lg">
        <div className="flex items-baseline justify-between">
          <h2 className="m-0 text-lg font-bold text-fg">{t('compare.progress.title')}</h2>
          <span className="font-mono text-sm text-accent" aria-live="polite">{t('compare.progress.elapsed', { n: seconds })}</span>
        </div>
        <div className="h-1.5 overflow-hidden rounded-full bg-pressed">
          <div className="h-full w-1/3 animate-[progress_1.4s_ease-in-out_infinite] rounded-full bg-accent motion-reduce:w-full motion-reduce:animate-none motion-reduce:opacity-60" />
        </div>
        <ol className="m-0 flex list-none flex-col gap-2.5 p-0">
          {steps.map((s) => (
            <li key={s.key} className={`flex items-center gap-2.5 text-md ${s.state === 'next' ? 'text-fg-faint' : 'text-fg'}`}>
              {s.state === 'done' && <span className="flex h-[18px] w-[18px] items-center justify-center rounded-full bg-accent-subtle text-accent"><Check className="h-3 w-3" aria-hidden /></span>}
              {s.state === 'now' && <Loader2 className="h-[18px] w-[18px] animate-spin text-accent motion-reduce:animate-none" aria-hidden />}
              {s.state === 'next' && <span className="h-[18px] w-[18px] rounded-full border border-strong" />}
              {t(s.key)}
            </li>
          ))}
        </ol>
        <div className="flex items-center justify-between border-t border-subtle pt-3.5">
          <span className="text-sm text-fg-muted">{t('compare.progress.local')}</span>
          <Button onClick={onCancel}>{t('compare.progress.cancel')}</Button>
        </div>
      </div>
    </div>
  );
}
