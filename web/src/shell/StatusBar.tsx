import { Globe } from 'lucide-react';
import { useApp, type DocTab, type StatusToggleId } from '../app/store';
import { useT, type MessageKey } from '../i18n';
import { toggleAid } from '../commands/builtins';
import { runCommand } from '../commands/runner';
import { useCompare } from '../compare/state';
import { StatusToggle } from '../ui/StatusToggle';

const TOGGLES: StatusToggleId[] = ['grid', 'snap', 'ortho', 'osnap', 'darkView'];
const TOGGLE_KEY: Record<StatusToggleId, string> = { grid: 'F7', snap: 'F9', ortho: 'F8', osnap: 'F3', darkView: 'DV' };

/** 상태 표시줄 (brand.md): 지금 상태 · 작도 토글(도면·비교 탭) · 서버 주소 · 언어. */
export function StatusBar({ tab }: { tab: DocTab }) {
  const t = useT();
  const toggles = useApp((s) => s.toggles);
  const language = useApp((s) => s.language);
  const showToggles = tab.kind === 'drawing' || tab.kind === 'compare';
  // GRID·SNAP·ORTHO는 그 명령, OSNAP은 F3처럼 켜고 끄기만 (OSNAP 명령은 설정 창), DARK VIEW는 DARKVIEW 명령 (U10)
  const onToggle = (id: StatusToggleId) =>
    id === 'osnap' ? toggleAid('osnap') : void runCommand(id === 'darkView' ? 'DARKVIEW' : id.toUpperCase(), { quiet: id !== 'darkView' });
  const cmp = useCompare((x) => (tab.kind === 'compare' ? x.tabs[tab.id] : undefined));
  let left = tab.kind === 'compare' ? t('status.compare') : tab.kind === 'png' ? t('status.png') : t('status.ready');
  // 비교 탭: 지금 페이지의 변경 수 · 페이지 · 동기 이동 · 확대 비율 (시안 04)
  const extra: string[] = [];
  if (cmp?.comparing) left = `${left} · ${t('status.comparing')}`;
  else if (cmp?.result) {
    const page = cmp.result.pages[cmp.pairIndex];
    left = `${left} · ${t('status.changesOnPage', { n: page?.changes.length ?? 0, p: cmp.pairIndex + 1 })}`;
  } else if (cmp && (cmp.left.error || cmp.right.error)) {
    left = t('status.openFailed', { n: Number(!!cmp.left.error) + Number(!!cmp.right.error) });
  }
  if (cmp?.sync && cmp.viewMode === 'side' && (cmp.left.file || cmp.right.file)) extra.push(t('status.syncOn'));
  if (cmp?.result) extra.push(t('status.pageOf', { n: cmp.pairIndex + 1, total: cmp.result.pages.length }));
  if (cmp?.zoom) extra.push(`${Math.round(cmp.zoom * 100)}%`);

  return (
    <footer className="flex h-statusbar flex-none items-center gap-4 border-t border-subtle bg-chrome px-3 text-xs text-fg-secondary">
      <span>{left}</span>
      {showToggles && (
        <span role="group" aria-label={t('status.toggles')} className="flex gap-1">
          {TOGGLES.map((id) => (
            <StatusToggle key={id} label={t(`status.toggle.${id}` as MessageKey)} on={toggles[id]} onToggle={() => onToggle(id)}
              title={`${t(`status.toggle.${id}` as MessageKey)} (${TOGGLE_KEY[id]})`} />
          ))}
        </span>
      )}
      <span className="ml-auto flex items-center gap-4">
        {extra.map((x) => <span key={x}>{x}</span>)}
        <span title={t('status.server')}>{window.location.host}</span>
        <span className="flex items-center gap-1" title={t('status.language')}>
          <Globe className="h-3 w-3" aria-hidden />
          {language.toUpperCase()}
        </span>
      </span>
    </footer>
  );
}
