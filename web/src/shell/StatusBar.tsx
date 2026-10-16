import { Globe } from 'lucide-react';
import { useApp, type DocTab, type StatusToggleId } from '../app/store';
import { useT, type MessageKey } from '../i18n';
import { StatusToggle } from '../ui/StatusToggle';

const TOGGLES: StatusToggleId[] = ['grid', 'snap', 'ortho', 'osnap', 'darkView'];
const TOGGLE_KEY: Record<StatusToggleId, string> = { grid: 'F7', snap: 'F9', ortho: 'F8', osnap: 'F3', darkView: 'DV' };

/** 상태 표시줄 (brand.md): 지금 상태 · 작도 토글(도면·비교 탭) · 서버 주소 · 언어. */
export function StatusBar({ tab }: { tab: DocTab }) {
  const t = useT();
  const toggles = useApp((s) => s.toggles);
  const toggle = useApp((s) => s.toggle);
  const language = useApp((s) => s.language);
  const showToggles = tab.kind === 'drawing' || tab.kind === 'compare';
  const left = tab.kind === 'compare' ? t('status.compare') : tab.kind === 'png' ? t('status.png') : t('status.ready');

  return (
    <footer className="flex h-statusbar flex-none items-center gap-4 border-t border-subtle bg-chrome px-3 text-xs text-fg-secondary">
      <span>{left}</span>
      {showToggles && (
        <span role="group" aria-label={t('status.toggles')} className="flex gap-1">
          {TOGGLES.map((id) => (
            <StatusToggle key={id} label={t(`status.toggle.${id}` as MessageKey)} on={toggles[id]} onToggle={() => toggle(id)}
              title={`${t(`status.toggle.${id}` as MessageKey)} (${TOGGLE_KEY[id]})`} />
          ))}
        </span>
      )}
      <span className="ml-auto flex items-center gap-4">
        <span title={t('status.server')}>{window.location.host}</span>
        <span className="flex items-center gap-1" title={t('status.language')}>
          <Globe className="h-3 w-3" aria-hidden />
          {language.toUpperCase()}
        </span>
      </span>
    </footer>
  );
}
