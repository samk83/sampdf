import { useEffect, useMemo, useState, type KeyboardEvent } from 'react';
import { ChevronDown, ChevronUp, Terminal } from 'lucide-react';
import { useApp, type HistoryLine } from '../app/store';
import { hangulToLatin } from '../commands/hangul';
import { autocomplete, findCommand } from '../commands/lookup';
import { COMMANDS, type Ask } from '../commands/registry';
import { cancel, submit } from '../commands/runner';
import { useT, type MessageKey } from '../i18n';
import { IconButton } from '../ui/IconButton';
import { Autocomplete } from './Autocomplete';
import { registerCmdline } from './cmdlineFocus';

const AC_ID = 'cmd-suggestions';

const LINE_COLOR: Record<HistoryLine['kind'], string> = {
  info: 'text-fg-faint',
  cmd: 'text-fg-secondary',
  error: 'text-danger',
  ok: 'text-success',
};

/**
 * 명령창 (아래에 붙은 모양, 설계서 6장·시안 04·11): 기록 2줄 + 입력 줄 + 자동완성.
 * compact: 옛 비교·PNG 탭에서는 입력 줄만 (마지막 기록은 입력 줄 오른쪽에). 도면 칸을 넓게 두려고 U3·U4까지.
 */
export function CommandLine({ compact }: { compact?: boolean }) {
  const t = useT();
  const history = useApp((s) => s.history);
  const cmd = useApp((s) => s.cmd);
  const setCmd = useApp((s) => s.setCmd);
  const expanded = useApp((s) => s.historyExpanded);
  const setUi = useApp((s) => s.setUi);
  const [highlight, setHighlight] = useState(0);
  const [navigated, setNavigated] = useState(false);

  const describe = (name: string) => t(`cmd.${name}` as MessageKey);
  const items = useMemo(
    () => (cmd.ask ? [] : autocomplete(COMMANDS, hangulToLatin(cmd.input), (d) => describe(d.name))),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [cmd.input, cmd.ask, t],
  );
  /** ↑↓로 이전 입력을 넘기는 중에는 목록을 닫는다 (안 그러면 두 번째 ↑가 목록 안에서 움직임). */
  const [recallAt, setRecallAt] = useState<number | null>(null);
  const showList = items.length > 0 && recallAt === null;
  useEffect(() => { setHighlight(0); setNavigated(false); }, [cmd.input]);

  const welcome: HistoryLine[] = [
    { text: t('cmdline.welcome', { app: t('app.name') }), kind: 'info' },
    // 클라우드 미리보기 빌드(npm run build:preview)에서는 비교가 안 된다는 안내를 먼저
    ...(import.meta.env.VITE_PREVIEW === '1' ? [{ text: t('cmdline.preview'), kind: 'cmd' as const }] : []),
  ];
  const lines = history.length ? history : welcome;

  const setInput = (input: string) => setCmd({ input, suggestion: null });

  const run = (text: string) => {
    setInput('');
    setRecallAt(null);
    void submit(text);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    const textAsk = cmd.ask?.kind === 'text';
    // 한글 조합 중 Enter·Space: 조합을 먼저 끝내고(입력칸을 잠깐 떠났다 돌아옴) 실행한다
    if ((e.key === 'Enter' || (e.key === ' ' && !textAsk)) && e.nativeEvent.isComposing) {
      e.preventDefault();
      const el = e.currentTarget;
      el.blur();
      el.focus();
      setTimeout(() => run(chosen()), 0);
      return;
    }
    switch (e.key) {
      case 'ArrowUp':
      case 'ArrowDown': {
        e.preventDefault();
        const step = e.key === 'ArrowUp' ? -1 : 1;
        if (showList) {
          setHighlight((h) => (h + step + items.length) % items.length);
          setNavigated(true);
          return;
        }
        // 입력칸이 비었거나 이미 기록을 넘기는 중이면 이전 입력 다시 부르기
        if (cmd.input && recallAt === null) return;
        const { recall } = cmd;
        if (!recall.length) return;
        const at = Math.min(recall.length, Math.max(0, (recallAt ?? recall.length) + step));
        setRecallAt(at === recall.length ? null : at);
        setInput(recall[at] ?? '');
        return;
      }
      case 'Tab':
        if (showList) {
          e.preventDefault();
          setInput(items[highlight].def.name);
        } else if (cmd.suggestion && !cmd.input) {
          e.preventDefault();
          setInput(cmd.suggestion);
        }
        return;
      case 'Escape':
        e.preventDefault();
        setInput('');
        setRecallAt(null);
        cancel();
        return;
      case ' ':
        if (textAsk) return; // 글자를 묻는 중에는 띄어쓰기 (Enter만 끝)
        // 오토캐드처럼 Space = Enter. `ZOOM` Space → 질문 → `E` Space (리본은 `ZOOM E`를 한 번에 넘김)
        e.preventDefault();
        run(chosen());
        return;
      case 'Enter':
        e.preventDefault();
        run(chosen());
        return;
    }
  };

  /**
   * Enter로 실행할 글자: ↑↓로 고른 줄이 있으면 그 명령, 친 글자가 명령이 아닌데
   * 이름·별칭이 그 글자로 시작하는 명령이 있으면 첫 줄 (04: EXP + Enter = EXPORT), 아니면 친 그대로.
   */
  const chosen = () => {
    const input = useApp.getState().cmd.input; // 조합이 끝난 뒤 부를 때도 최신 글자
    if (!showList) return input;
    if (navigated) return items[highlight].def.name;
    if (!findCommand(COMMANDS, hangulToLatin(input)) && items[0].rank <= 2) return items[0].def.name;
    return input;
  };

  const historyBox = expanded && (
    <div role="log" aria-label={t('cmdline.history')}
      className="absolute bottom-full left-0 right-0 z-floating flex max-h-[40vh] min-h-[52px] flex-col-reverse overflow-auto border-t border-default bg-sunken py-1.5 pl-11 pr-3 shadow-md">
      <div className="flex flex-col gap-0.5">
        {lines.map((line, i) => <div key={i} className={`whitespace-pre-wrap break-words ${LINE_COLOR[line.kind]}`}>{line.text}</div>)}
      </div>
    </div>
  );

  const last = lines[lines.length - 1];

  return (
    <section aria-label={t('cmdline.label')} className="relative flex flex-none flex-col border-t border-default bg-sunken font-mono text-sm">
      {!compact && (
        <div role="log" aria-live="polite" className="flex min-h-[52px] flex-col justify-end gap-0.5 pb-1 pl-11 pr-3 pt-1.5">
          {lines.slice(-2).map((line, i) => (
            <div key={history.length + i} className={`truncate ${LINE_COLOR[line.kind]}`}>{line.text}</div>
          ))}
        </div>
      )}
      <div className="relative flex h-cmdline-input items-center gap-2.5 border-t border-subtle bg-background pl-3 pr-2">
        {historyBox}
        {showList && (
          <Autocomplete id={AC_ID} items={items} highlight={highlight} describe={describe}
            onPick={(name) => run(name)} onHover={(i) => { setHighlight(i); setNavigated(true); }} />
        )}
        <Terminal className="h-icon w-icon flex-none text-accent" aria-hidden />
        <label className="flex min-w-0 flex-1 items-center gap-2">
          <PromptLabel ask={cmd.ask} fallback={t('cmdline.prompt')} onOption={(o) => run(o)} />
          <input ref={registerCmdline} data-cmdline-input aria-label={t('cmdline.input')} value={cmd.input} spellCheck={false} autoComplete="off"
            role="combobox" aria-expanded={showList} aria-autocomplete="list" aria-controls={showList ? AC_ID : undefined}
            aria-activedescendant={showList ? `${AC_ID}-${highlight}` : undefined}
            onChange={(e) => { setInput(e.target.value); setRecallAt(null); }} onKeyDown={onKeyDown}
            className="min-w-0 flex-1 bg-transparent text-fg caret-fg outline-none focus-visible:shadow-none" />
        </label>
        {compact && !expanded && history.length > 0
          ? <span role="status" className={`min-w-0 max-w-[50%] flex-none truncate ${LINE_COLOR[last.kind]}`}>{last.text}</span>
          : <span className="flex-none font-sans text-xs text-fg-faint">{t('cmdline.hint')}</span>}
        <IconButton label={t(expanded ? 'cmdline.collapse' : 'cmdline.expand')} pressed={expanded} onClick={() => setUi({ historyExpanded: !expanded })}>
          {expanded ? <ChevronDown className="h-icon w-icon" aria-hidden /> : <ChevronUp className="h-icon w-icon" aria-hidden />}
        </IconButton>
      </div>
    </section>
  );
}

/** 입력칸 앞 글자: "Command:" 또는 명령의 질문. [옵션]은 밑줄, 눌러서 고를 수 있다. */
function PromptLabel({ ask, fallback, onOption }: { ask: Ask | null; fallback: string; onOption: (o: string) => void }) {
  if (!ask) return <span className="flex-none text-fg-secondary">{fallback}</span>;
  const options = 'options' in ask ? ask.options ?? [] : [];
  const def = 'defaultValue' in ask && ask.defaultValue !== undefined ? ` <${ask.defaultValue}>` : '';
  return (
    <span className="flex-none whitespace-nowrap text-fg-secondary">
      {ask.prompt}
      {options.length > 0 && (
        <>
          {' ['}
          {options.map((o, i) => (
            <span key={o}>
              {i > 0 && '/'}
              <button type="button" onClick={() => onOption(o)}
                className="text-accent underline underline-offset-2 hover:text-fg">{o}</button>
            </span>
          ))}
          {']'}
        </>
      )}
      {def}:
    </span>
  );
}
