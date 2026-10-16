import { useApp, type DocTab } from '../app/store';
import { useT, type MessageKey } from '../i18n';
import { RibbonButton } from '../ui/RibbonButton';
import { useNotYet } from './notYet';
import { RIBBON, RIBBON_TABS } from './ribbonLayout';

/** 리본 탭 줄 + 리본 (설계서 2-4). 버튼 = 명령 (U2부터 실행, 지금은 명령창에 안내). */
export function Ribbon({ tab }: { tab: DocTab }) {
  const t = useT();
  const notYet = useNotYet();
  const setRibbonTab = useApp((s) => s.setRibbonTab);

  return (
    <div className="flex-none">
      <div role="tablist" aria-label={t('ribbon.label')} className="flex h-ribbon-tabs items-end gap-0.5 px-2.5">
        {RIBBON_TABS.map((id) => {
          const active = id === tab.ribbonTab;
          return (
            <button key={id} type="button" role="tab" aria-selected={active} onClick={() => setRibbonTab(tab.id, id)}
              className={`h-touch rounded-t-sm px-3.5 ${
                active ? 'bg-surface font-semibold text-fg shadow-[inset_0_2px_0_var(--color-accent)]' : 'text-fg-muted hover:text-fg'
              }`}>
              {t(`ribbon.tab.${id}` as MessageKey)}
            </button>
          );
        })}
      </div>
      <div role="toolbar" aria-label={t(`ribbon.tab.${tab.ribbonTab}` as MessageKey)}
        className="flex h-ribbon items-stretch overflow-x-auto border-b border-default bg-surface px-2 py-1.5">
        {RIBBON[tab.ribbonTab].map((group) => (
          <div key={group.id} role="group" aria-label={t(`ribbon.group.${group.id}` as MessageKey)}
            className="flex flex-none flex-col border-r border-default px-2">
            <div className="flex flex-1 gap-0.5">
              {group.buttons.map((b) => {
                const label = t(`ribbon.btn.${b.id}` as MessageKey);
                const tooltip = `${label} · ${b.hint ? `${b.command} · ${b.hint}` : b.command}` +
                  (b.phase ? ` (${t('soon.phase2')})` : '');
                return (
                  <RibbonButton key={b.id} icon={b.icon} label={label} tooltip={tooltip}
                    tag={b.phase ? t('soon.phase2') : undefined}
                    onClick={() => notYet(b.command, b.phase ? 'phase2' : 'command')} />
                );
              })}
            </div>
            <div className="text-center text-2xs text-fg-faint">{t(`ribbon.group.${group.id}` as MessageKey)}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
