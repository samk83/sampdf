import { useEffect, useRef, useState, type ReactNode } from 'react';
import { File as FileIcon, GitCompare, House, Image, Settings, Upload, Wrench, type LucideIcon } from 'lucide-react';
import { compareTitle, routeDrop } from '../app/dropRouting';
import { useApp } from '../app/store';
import { useT } from '../i18n';
import { useNotYet } from './notYet';

/**
 * 시작 화면 (확정 시안 01). 최근 파일·비교 목록은 U5에서 채운다.
 * `px-[30px]`·`h-[136px]` 같은 대괄호 크기는 시안 값 그대로 (간격 토큰 4·8·12…에 없는 값).
 */
export function HomeScreen({ active }: { active: boolean }) {
  const t = useT();
  const notYet = useNotYet();
  const openTab = useApp((s) => s.openTab);
  const log = useApp((s) => s.log);
  const [over, setOver] = useState(false);

  const openCompare = (files?: File[]) => openTab('compare', { files, title: files ? compareTitle(t('tabs.compare'), files) : undefined });
  const openPng = (files?: File[]) => openTab('png', { files, title: files ? `${t('tabs.png')} · ${files[0].name}` : undefined });

  const handleDrop = (dropped: File[]) => {
    const route = routeDrop(dropped);
    if (route.kind === 'compare') openCompare(route.files);
    else if (route.kind === 'png') openPng(route.files);
    else log({ text: t('home.drop.unsupported'), kind: 'error' });
  };
  const handleDropRef = useRef(handleDrop);
  handleDropRef.current = handleDrop;

  // "Drop files anywhere in this window": 시작 화면이 보일 때 창 어디에 놓아도 연다
  useEffect(() => {
    if (!active) return;
    const onOver = (e: DragEvent) => {
      if (!e.dataTransfer?.types.includes('Files')) return;
      e.preventDefault();
      setOver(true);
    };
    const onLeave = (e: DragEvent) => { if (!e.relatedTarget) setOver(false); };
    const onDrop = (e: DragEvent) => {
      e.preventDefault();
      setOver(false);
      handleDropRef.current(Array.from(e.dataTransfer?.files ?? []));
    };
    window.addEventListener('dragover', onOver);
    window.addEventListener('dragleave', onLeave);
    window.addEventListener('drop', onDrop);
    return () => {
      window.removeEventListener('dragover', onOver);
      window.removeEventListener('dragleave', onLeave);
      window.removeEventListener('drop', onDrop);
    };
  }, [active]);

  return (
    <div className="flex h-full min-h-0">
      <nav aria-label={t('home.nav')} className="flex w-panel-left flex-none flex-col gap-0.5 border-r border-default bg-surface px-2.5 py-4">
        <NavItem icon={House} label={t('home.nav.home')} current onClick={() => undefined} />
        <NavItem icon={FileIcon} label={t('home.nav.open')} onClick={() => notYet('OPEN', 'later')} />
        <NavItem icon={GitCompare} label={t('home.nav.compare')} onClick={() => openCompare()} />
        <NavItem icon={Image} label={t('home.nav.png')} onClick={() => openPng()} />
        <NavItem icon={Wrench} label={t('home.nav.toolSets')} tag={t('soon.laterTag')} onClick={() => notYet('TOOLSETS', 'later')} />
        <NavItem icon={Settings} label={t('home.nav.settings')} onClick={() => notYet('OPTIONS', 'later')} />
      </nav>

      <main className="flex min-w-0 flex-1 flex-col gap-5 overflow-auto px-[30px] py-6">
        <div className="flex items-end justify-between gap-4">
          <div className="flex flex-col gap-1">
            <h1 className="m-0 text-xl font-bold leading-tight">{t('home.title')}</h1>
            <span className="text-fg-muted">{t('home.subtitle')}</span>
          </div>
          <span className="text-xs text-fg-faint">{t('home.local')}</span>
        </div>

        <div className="grid grid-cols-3 gap-[14px]">
          <ActionCard primary icon={GitCompare} command="CMP" title={t('home.card.compare.title')} desc={t('home.card.compare.desc')} onClick={() => openCompare()} />
          <ActionCard icon={FileIcon} command="OPEN" title={t('home.card.open.title')} desc={t('home.card.open.desc')} onClick={() => notYet('OPEN', 'later')} />
          <ActionCard icon={Image} command="PNG" title={t('home.card.png.title')} desc={t('home.card.png.desc')} onClick={() => openPng()} />
        </div>

        <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)] gap-[18px]">
          <section aria-label={t('home.recentFiles')} className="flex flex-col gap-2.5">
            <h2 className="m-0 text-sm font-semibold">{t('home.recentFiles')}</h2>
            <EmptyBox>{t('home.recentFiles.empty')}</EmptyBox>
            <div className={`flex h-[120px] items-center justify-center gap-3 rounded-lg border-[1.5px] border-dashed transition-colors duration-fast ${
              over ? 'border-accent bg-accent-subtle text-fg' : 'border-strong text-fg-muted'}`}>
              <Upload className="h-icon-ribbon w-icon-ribbon" aria-hidden />
              {over ? <b className="text-fg">{t('home.drop.over')}</b> : (
                <span><b className="text-fg">{t('home.drop.title')}</b> · {t('home.drop.hint')}</span>
              )}
            </div>
          </section>
          <section aria-label={t('home.recentComparisons')} className="flex flex-col gap-2.5">
            <h2 className="m-0 text-sm font-semibold">{t('home.recentComparisons')}</h2>
            <EmptyBox>{t('home.recentComparisons.empty')}</EmptyBox>
          </section>
        </div>
      </main>
    </div>
  );
}

function NavItem({ icon: Icon, label, current, tag, onClick }: { icon: LucideIcon; label: string; current?: boolean; tag?: string; onClick: () => void }) {
  return (
    <button type="button" aria-current={current ? 'page' : undefined} onClick={onClick}
      className={`flex h-9 items-center gap-2.5 rounded-sm px-3 text-left transition-colors duration-fast ${
        current ? 'bg-pressed font-semibold text-fg' : tag ? 'text-fg-faint' : 'text-fg-secondary hover:bg-pressed hover:text-fg'}`}>
      <Icon className="h-[17px] w-[17px]" aria-hidden />
      <span className="flex-1">{label}</span>
      {tag && <span className="rounded-sm border border-strong px-1 text-2xs">{tag}</span>}
    </button>
  );
}

function ActionCard({ icon: Icon, command, title, desc, primary, onClick }: {
  icon: LucideIcon; command: string; title: string; desc: string; primary?: boolean; onClick: () => void;
}) {
  return (
    <button type="button" onClick={onClick}
      className={`flex h-[136px] flex-col justify-between rounded-lg border px-[18px] py-4 text-left transition-colors duration-fast ${
        primary ? 'border-accent bg-gradient-to-br from-primary to-primary-deep text-on-primary hover:brightness-110' : 'border-default bg-surface text-fg hover:border-strong hover:bg-pressed'}`}>
      <span className="flex w-full justify-between">
        <span className={`flex h-[38px] w-[38px] items-center justify-center rounded-md ${primary ? 'bg-primary-deep' : 'bg-pressed'}`}>
          <Icon className={`h-icon-ribbon w-icon-ribbon ${primary ? '' : 'text-accent'}`} strokeWidth={1.9} aria-hidden />
        </span>
        <span className={`flex h-5 items-center rounded-sm border px-[7px] font-mono text-2xs ${primary ? 'border-on-primary' : 'border-strong'}`}>{command}</span>
      </span>
      <span className="flex flex-col gap-1">
        <span className="text-lg font-bold">{title}</span>
        <span className={`text-xs leading-[1.45] ${primary ? 'opacity-90' : 'text-fg-muted'}`}>{desc}</span>
      </span>
    </button>
  );
}

function EmptyBox({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-[96px] items-center justify-center rounded-md border border-default bg-surface px-4 text-center text-sm text-fg-faint">
      {children}
    </div>
  );
}
