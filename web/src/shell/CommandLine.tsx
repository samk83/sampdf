import { ChevronUp, Terminal } from 'lucide-react';
import { useApp, type HistoryLine } from '../app/store';
import { useT } from '../i18n';

const LINE_COLOR: Record<HistoryLine['kind'], string> = {
  info: 'text-fg-faint',
  cmd: 'text-fg-secondary',
  error: 'text-danger',
  ok: 'text-success',
};

/**
 * 명령창 자리 (아래에 붙은 모양, 01·04). 기록 2줄 + 입력 줄.
 * 입력·자동완성·실행은 U2에서 만든다. 지금은 버튼을 눌렀을 때의 안내가 기록에 쌓인다.
 */
export function CommandLine() {
  const t = useT();
  const history = useApp((s) => s.history);
  const shown: HistoryLine[] = history.length
    ? history.slice(-2)
    : [
        { text: t('cmdline.welcome', { app: t('app.name') }), kind: 'info' },
        { text: t('cmdline.notYet'), kind: 'info' },
      ];

  return (
    <section aria-label={t('cmdline.label')} className="relative flex flex-none flex-col border-t border-default bg-sunken font-mono text-sm">
      <div role="log" aria-live="polite" className="flex min-h-[52px] flex-col justify-end gap-0.5 pb-1 pl-11 pr-3 pt-1.5">
        {shown.map((line, i) => (
          <div key={history.length + i} className={`truncate ${LINE_COLOR[line.kind]}`}>{line.text}</div>
        ))}
      </div>
      <div className="flex h-cmdline-input items-center gap-2.5 border-t border-subtle bg-background pl-3 pr-2">
        <Terminal className="h-icon w-icon flex-none text-accent" aria-hidden />
        <label className="flex min-w-0 flex-1 items-center gap-2">
          <span className="text-fg-secondary">{t('cmdline.prompt')}</span>
          <input aria-label={t('cmdline.input')} disabled className="min-w-0 flex-1 bg-transparent text-fg outline-none" />
        </label>
        <ChevronUp className="h-icon w-icon flex-none text-fg-faint" aria-hidden />
      </div>
    </section>
  );
}
