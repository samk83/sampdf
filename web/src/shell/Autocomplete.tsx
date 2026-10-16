import type { Completion } from '../commands/lookup';
import { useT } from '../i18n';

type Props = {
  id: string;
  items: Completion[];
  highlight: number;
  describe: (name: string) => string;
  onPick: (name: string) => void;
  onHover: (index: number) => void;
};

/** 명령 자동완성 목록 (시안 04). 4칸: 이름 · 별칭 · 설명 · 단축키. */
export function Autocomplete({ id, items, highlight, describe, onPick, onHover }: Props) {
  const t = useT();
  return (
    <div id={id} role="listbox" aria-label={t('cmdline.suggestions')}
      className="absolute bottom-full left-[32px] z-dropdown mb-0.5 flex w-[520px] max-w-[calc(100vw-48px)] flex-col rounded-md border border-strong bg-surface py-1 shadow-md">
      {items.map(({ def }, i) => {
        const on = i === highlight;
        return (
          <div key={def.name} id={`${id}-${i}`} role="option" aria-selected={on}
            onMouseDown={(e) => e.preventDefault() /* 입력칸 커서를 잃지 않게 */}
            onClick={() => onPick(def.name)} onMouseEnter={() => onHover(i)}
            className={`flex h-[30px] cursor-pointer items-center gap-3 px-3 ${on ? 'bg-accent-subtle shadow-[inset_3px_0_0_var(--color-accent)]' : ''}`}>
            <span className="w-[92px] flex-none font-semibold text-fg">{def.name}</span>
            <span className="w-[40px] flex-none truncate text-accent">{def.aliases.join(', ')}</span>
            <span className={`min-w-0 flex-1 truncate font-sans text-sm ${def.phase ? 'text-fg-faint' : 'text-fg-secondary'}`}>
              {describe(def.name)}{def.phase ? ` · ${t('soon.phase2')}` : ''}
            </span>
            {def.shortcut && <span className="flex-none text-fg-faint">{def.shortcut}</span>}
          </div>
        );
      })}
      <div className="mt-1 flex h-[26px] items-center gap-3.5 border-t border-subtle px-3 font-sans text-xs text-fg-faint">
        <span>{t('cmdline.ac.choose')}</span>
        <span>{t('cmdline.ac.complete')}</span>
        <span>{t('cmdline.ac.run')}</span>
        <span className="ml-auto">{t('cmdline.ac.all')}</span>
      </div>
    </div>
  );
}
