import { Fullscreen, GitCompare, Redo2, Undo2, X } from 'lucide-react';
import { HOME_TAB_ID, useApp, type DocTab } from '../app/store';
import { useT, type MessageKey } from '../i18n';
import { IconButton } from '../ui/IconButton';
import { useNotYet } from './notYet';

/** 탭에 보이는 이름: 정한 이름이 없으면 종류별 기본 이름. */
export function useTabTitle() {
  const t = useT();
  return (tab: DocTab) => tab.title ?? t(`tabs.${tab.kind}` as MessageKey);
}

/** 제목줄: 앱 이름 · 문서 탭 · 되돌리기·다시 하기 · 포커스 모드 (설계서 2-2). */
export function TitleBar() {
  const t = useT();
  const notYet = useNotYet();
  const tabs = useApp((s) => s.tabs);
  const activeId = useApp((s) => s.activeId);
  const activate = useApp((s) => s.activate);
  const closeTab = useApp((s) => s.closeTab);
  const title = useTabTitle();

  const requestClose = (tab: DocTab) => {
    if (tab.dirty && !window.confirm(t('tabs.confirmClose', { name: title(tab) }))) return;
    closeTab(tab.id);
  };

  return (
    <header className="flex h-titlebar flex-none items-center gap-4 border-b border-subtle bg-chrome pl-3 pr-2">
      <span className="flex flex-none items-center gap-2">
        <span className="flex h-5 w-5 items-center justify-center rounded-sm bg-primary text-on-primary">
          <GitCompare className="h-[13px] w-[13px]" strokeWidth={2.4} aria-hidden />
        </span>
        <span className="font-bold">{t('app.name')}</span>
      </span>

      <div role="tablist" aria-label={t('tabs.label')} className="flex h-titlebar min-w-0 items-end gap-0.5 overflow-hidden">
        {tabs.map((tab) => {
          const active = tab.id === activeId;
          const name = title(tab);
          return (
            <div key={tab.id}
              className={`group flex h-touch min-w-0 max-w-[260px] items-center rounded-t-sm ${active ? 'bg-surface text-fg' : 'text-fg-muted hover:text-fg'}`}>
              <button type="button" role="tab" aria-selected={active} onClick={() => activate(tab.id)}
                onAuxClick={(e) => { if (e.button === 1 && tab.id !== HOME_TAB_ID) requestClose(tab); }}
                className={`flex h-full min-w-0 items-center gap-2 truncate px-3 text-xs ${active ? 'font-semibold' : ''}`}>
                <span className="truncate">{name}</span>
                {tab.dirty && <span className="text-warning" aria-label={t('tabs.unsaved')}>●</span>}
              </button>
              {tab.id !== HOME_TAB_ID && (
                <button type="button" aria-label={t('tabs.close', { name })} title={t('tabs.close', { name })} onClick={() => requestClose(tab)}
                  className="mr-1 flex h-[18px] w-[18px] flex-none items-center justify-center rounded-sm text-fg-faint hover:bg-pressed hover:text-fg">
                  <X className="h-3 w-3" aria-hidden />
                </button>
              )}
            </div>
          );
        })}
      </div>

      <div className="ml-auto flex flex-none items-center gap-1">
        <IconButton label={t('titlebar.undo')} onClick={() => notYet('UNDO')}><Undo2 className="h-[15px] w-[15px]" aria-hidden /></IconButton>
        <IconButton label={t('titlebar.redo')} onClick={() => notYet('REDO')}><Redo2 className="h-[15px] w-[15px]" aria-hidden /></IconButton>
        <button type="button" title={t('titlebar.focusHint')} onClick={() => notYet('FOCUS', 'later')}
          className="ml-2 flex h-6 items-center gap-1.5 rounded-sm border border-default px-2 text-xs text-fg-secondary hover:bg-pressed">
          <Fullscreen className="h-[13px] w-[13px]" aria-hidden />
          {t('titlebar.focus')} <span className="text-fg-faint">F11</span>
        </button>
      </div>
    </header>
  );
}
