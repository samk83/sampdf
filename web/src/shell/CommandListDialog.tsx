import { useState } from 'react';
import { Search } from 'lucide-react';
import { activeTab, useApp } from '../app/store';
import { BUILTINS } from '../commands/builtins';
import { COMMAND_GROUPS, COMMANDS, type CommandDef } from '../commands/registry';
import { runCommand } from '../commands/runner';
import { useT, type MessageKey } from '../i18n';
import { Dialog } from '../ui/Dialog';

/** `?` / F1: 전체 명령 목록 (설계서 6-5). 탭별로 쓸 수 있는지, 나중 단위·2단계 표시. 누르면 실행. */
export function CommandListDialog() {
  const t = useT();
  const open = useApp((s) => s.helpOpen);
  const setUi = useApp((s) => s.setUi);
  const tab = useApp(activeTab);
  const [filter, setFilter] = useState('');
  if (!open) return null;

  const close = () => { setUi({ helpOpen: false }); setFilter(''); };
  const q = filter.trim().toUpperCase();
  const describe = (d: CommandDef) => t(`cmd.${d.name}` as MessageKey);
  const matches = (d: CommandDef) =>
    !q || d.name.includes(q) || d.aliases.some((a) => a.includes(q)) || describe(d).toUpperCase().includes(q);
  const groups = COMMAND_GROUPS.map((g) => ({ g, list: COMMANDS.filter((d) => d.group === g && matches(d)) })).filter((x) => x.list.length);

  const pick = (d: CommandDef) => {
    close();
    void runCommand(d.name);
  };

  return (
    <Dialog title={t('help.title')} closeLabel={t('help.close')} onClose={close} width={720}
      footer={<span className="text-xs text-fg-faint">{t('help.footer')}</span>}>
      <div className="sticky top-0 border-b border-subtle bg-surface px-[18px] py-2.5">
        <label className="flex h-control items-center gap-2 rounded-sm border border-strong bg-background px-2.5 focus-within:border-accent">
          <Search className="h-icon w-icon flex-none text-fg-faint" aria-hidden />
          <input aria-label={t('help.filter')} placeholder={t('help.filter')} value={filter} onChange={(e) => setFilter(e.target.value)}
            className="min-w-0 flex-1 bg-transparent text-fg outline-none placeholder:text-fg-faint focus-visible:shadow-none" />
        </label>
      </div>
      {groups.length === 0 && <p className="m-0 px-[18px] py-6 text-center text-fg-muted">{t('help.none', { text: filter })}</p>}
      {groups.map(({ g, list }) => (
        <section key={g} aria-label={t(`help.group.${g}` as MessageKey)} className="px-2 pb-2">
          <h3 className="m-0 px-2.5 pb-1 pt-3 text-2xs font-semibold uppercase tracking-[0.04em] text-fg-faint">{t(`help.group.${g}` as MessageKey)}</h3>
          {list.map((d) => {
            const here = !d.where || d.where.includes(tab.kind);
            const works = !!BUILTINS[d.name] && !d.phase;
            const note = d.phase ? t('soon.phase2') : !here ? t('help.notHere') : !works ? t('help.later') : '';
            return (
              <button key={d.name} type="button" onClick={() => pick(d)}
                className="flex h-[30px] w-full items-center gap-3 rounded-sm px-2.5 text-left hover:bg-pressed">
                <span className={`w-[104px] flex-none font-mono font-semibold ${here && !d.phase ? 'text-fg' : 'text-fg-faint'}`}>{d.name}</span>
                <span className="w-[56px] flex-none truncate font-mono text-accent">{d.aliases.join(', ')}</span>
                <span className={`min-w-0 flex-1 truncate text-sm ${here && !d.phase ? 'text-fg-secondary' : 'text-fg-faint'}`}>{describe(d)}</span>
                {note && <span className="flex-none text-2xs text-fg-faint">{note}</span>}
                <span className="w-[84px] flex-none text-right font-mono text-xs text-fg-faint">{d.shortcut ?? ''}</span>
              </button>
            );
          })}
        </section>
      ))}
    </Dialog>
  );
}
