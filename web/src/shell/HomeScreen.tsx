import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Eraser, File as FileIcon, GitCompare, House, Search, SearchX, Settings, ShieldCheck, Upload, type LucideIcon } from 'lucide-react';
import { compareTitle, routeDrop } from '../app/dropRouting';
import { useApp } from '../app/store';
import { TARGET } from '../app/target';
import { APPS, CATEGORIES, appDescKey, appNameKey, searchApps, type AppCategory, type AppDef } from '../apps/registry';
import { isAppReady, openAppById } from '../apps/openApp';
import { translate, useT, type MessageKey } from '../i18n';
import { runCommand } from '../commands/runner';
import { Badge } from '../ui/Badge';
import { EmptyState } from '../ui/EmptyState';

/**
 * 시작 화면 (확정 시안 01 + 설계서 11-3 기능 카드). 큰 카드 3개 자리에 기능 24개 카드(검색·갈래)를 둔다.
 * 최근 파일·비교 목록은 U5에서 채운다.
 * `px-[30px]` 같은 대괄호 크기는 시안 값 그대로 (간격 토큰 4·8·12…에 없는 값).
 */
export function HomeScreen({ active }: { active: boolean }) {
  const t = useT();
  const openTab = useApp((s) => s.openTab);
  const log = useApp((s) => s.log);
  const panelsHidden = useApp((s) => s.panelsHidden);
  const [over, setOver] = useState(false);

  const openCompare = (files: File[]) => openTab('compare', { files, title: compareTitle(t('tabs.compare'), files) });
  const openPng = (files: File[]) => openTab('png', { files, title: `${t('tabs.png')} · ${files[0].name}` });

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
      {!panelsHidden && <nav aria-label={t('home.nav')} className="flex w-panel-left flex-none flex-col gap-0.5 border-r border-default bg-surface px-2.5 py-4">
        <NavItem icon={House} label={t('home.nav.home')} current onClick={() => undefined} />
        <NavItem icon={FileIcon} label={t('home.nav.open')} onClick={() => void runCommand('OPEN')} />
        <NavItem icon={GitCompare} label={t('home.nav.compare')} onClick={() => void runCommand('CMP')} />
        <NavItem icon={Eraser} label={t('home.nav.png')} onClick={() => void runCommand('PNG')} />
        <NavItem icon={Settings} label={t('home.nav.settings')} onClick={() => void runCommand('OPTIONS')} />
      </nav>}

      <main className="flex min-w-0 flex-1 flex-col gap-3.5 overflow-auto px-7 py-5">
        <div className="flex items-end justify-between gap-4">
          <div className="flex flex-col gap-1">
            <h1 className="m-0 text-xl font-bold leading-tight">{t('home.title')}</h1>
            <span className="text-fg-muted">{t('home.subtitle')}</span>
          </div>
          <span className="text-xs text-fg-faint">{t(TARGET === 'web' ? 'home.web' : 'home.local')}</span>
        </div>

        {TARGET === 'web' && (
          <p className="m-0 flex items-start gap-2 rounded-md border border-default bg-surface px-3 py-2 text-sm text-fg-secondary">
            <ShieldCheck className="mt-0.5 h-icon w-icon flex-none text-accent" aria-hidden />
            <span>{t('home.privacy')}</span>
          </p>
        )}

        <ToolLauncher active={active} />

        <div className="grid flex-none grid-cols-[minmax(0,2.2fr)_minmax(0,1fr)] items-end gap-3.5">
          <section aria-label={t(TARGET === 'web' ? 'home.sessionFiles' : 'home.recentFiles')} className="flex flex-col gap-2">
            <h2 className="m-0 flex justify-between gap-3 text-sm font-semibold">
              {t(TARGET === 'web' ? 'home.sessionFiles' : 'home.recentFiles')}
              {TARGET === 'web' && <span className="text-xs font-normal text-fg-faint">{t('home.sessionFiles.note')}</span>}
            </h2>
            <EmptyBox>{t(TARGET === 'web' ? 'home.sessionFiles.empty' : 'home.recentFiles.empty')}</EmptyBox>
          </section>
          <div className={`flex min-h-[58px] items-center justify-center gap-2.5 rounded-md border-[1.5px] border-dashed px-3 text-center text-sm transition-colors duration-fast ${
            over ? 'border-accent bg-accent-subtle text-fg' : 'border-strong text-fg-muted'}`}>
            <Upload className="h-icon-ribbon w-icon-ribbon flex-none" aria-hidden />
            {over ? <b className="text-fg">{t('home.drop.over')}</b> : (
              <span><b className="text-fg">{t('home.drop.title')}</b><br />{t(TARGET === 'web' ? 'home.drop.hintWeb' : 'home.drop.hint')}</span>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}

function NavItem({ icon: Icon, label, current, onClick }: { icon: LucideIcon; label: string; current?: boolean; onClick: () => void }) {
  return (
    <button type="button" aria-current={current ? 'page' : undefined} onClick={onClick}
      className={`flex h-9 items-center gap-2.5 rounded-sm px-3 text-left transition-colors duration-fast ${
        current ? 'bg-pressed font-semibold text-fg' : 'text-fg-secondary hover:bg-pressed hover:text-fg'}`}>
      <Icon className="h-[17px] w-[17px]" aria-hidden />
      <span className="flex-1">{label}</span>
    </button>
  );
}

type Filter = AppCategory | 'all';

/** 기능 카드 묶음: 검색 칸 + 갈래 고르기 + 카드 격자 (설계서 11-3, 보완 시안 r2-01). Ctrl+K로 검색 칸. */
function ToolLauncher({ active }: { active: boolean }) {
  const t = useT();
  const language = useApp((s) => s.language);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const searchRef = useRef<HTMLInputElement>(null);
  const inCategory = filter === 'all' ? APPS : APPS.filter((a) => a.category === filter);
  // 지금 언어와 영어 이름·설명 둘 다로 찾는다 (한국어 화면에서 "merge"로도 찾게)
  const found = searchApps(inCategory, query, (app) => [appNameKey(app), appDescKey(app)].flatMap((k) => [translate(language, k), translate('en', k)]));
  const readyCount = found.filter(isAppReady).length;

  useEffect(() => {
    if (!active) return;
    const onKey = (e: KeyboardEvent) => {
      if (useApp.getState().helpOpen) return; // 명령 목록 창이 열려 있으면 그 창이 키를 가진다
      if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        e.stopPropagation();
        searchRef.current?.focus();
        searchRef.current?.select();
      }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [active]);

  return (
    <section aria-label={t('home.tools')} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2.5">
        <label className="flex h-control w-full max-w-[340px] items-center gap-2 rounded-sm border border-strong bg-sunken px-2.5 focus-within:border-accent">
          <Search className="h-icon w-icon flex-none text-fg-faint" aria-hidden />
          <input ref={searchRef} type="search" value={query} placeholder={t('home.search')} aria-label={t('home.search')} aria-describedby="home-search-hint" aria-keyshortcuts="Control+K"
            onChange={(e) => setQuery(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') { if (query) setQuery(''); else e.currentTarget.blur(); } }}
            className="min-w-0 flex-1 bg-transparent text-sm text-fg outline-none placeholder:text-fg-faint" />
          <kbd className="flex-none rounded-sm border border-default px-1 font-mono text-2xs text-fg-faint">Ctrl K</kbd>
        </label>
        <span id="home-search-hint" className="sr-only">{t('home.searchHint')}</span>
        <div role="group" aria-label={t('home.category')} className="flex flex-wrap gap-1.5">
          {(['all', ...CATEGORIES] as Filter[]).map((c) => (
            <button key={c} type="button" aria-pressed={filter === c} onClick={() => setFilter(c)}
              className={`flex h-control items-center gap-1.5 rounded-sm border px-2.5 text-sm transition-colors duration-fast ${
                filter === c ? 'border-accent bg-accent-subtle text-fg' : 'border-default text-fg-secondary hover:bg-pressed hover:text-fg'}`}>
              {t(`home.category.${c}` as MessageKey)}{' '}
              <span className="text-xs text-fg-faint">{c === 'all' ? APPS.length : APPS.filter((a) => a.category === c).length}</span>
            </button>
          ))}
        </div>
        <span aria-live="polite" className="ml-auto text-xs text-fg-faint">{t('apps.summary', { ready: readyCount, soon: found.length - readyCount })}</span>
      </div>

      {found.length === 0 ? (
        <div className="rounded-md border border-default bg-surface">
          <EmptyState icon={SearchX}>{t('home.searchEmpty', { q: query.trim() })}</EmptyState>
        </div>
      ) : (
        <ul className="m-0 grid list-none grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6 gap-2 p-0">
          {found.map((app) => <li key={app.id} className="flex"><ToolCard app={app} /></li>)}
        </ul>
      )}
    </section>
  );
}

/** 기능 카드 하나 (보완 시안 r2-01). Ready면 그 기능의 명령을 부르고, 준비 중이면 기능 안내 탭을 연다. */
function ToolCard({ app }: { app: AppDef }) {
  const t = useT();
  const Icon = app.icon;
  const name = t(appNameKey(app));
  const ready = isAppReady(app);
  return (
    <button type="button" onClick={() => openAppById(app.id)} title={ready ? t('apps.openHint', { name }) : t('apps.soonHint', { name })}
      className={`flex min-h-[104px] w-full flex-col gap-1.5 rounded-md border bg-surface px-3 py-2.5 text-left transition-colors duration-fast hover:bg-pressed ${
        ready ? 'border-accent' : 'border-default hover:border-strong'}`}>
      <span className="flex w-full items-center justify-between gap-2">
        <span className={`flex h-[26px] w-[26px] flex-none items-center justify-center rounded-sm ${ready ? 'bg-accent-subtle' : 'bg-pressed'}`}>
          <Icon className={`h-icon w-icon ${ready ? 'text-accent' : 'text-fg-secondary'}`} strokeWidth={1.9} aria-hidden />
        </span>
        {app.status === 'soon'
          ? <span className="text-2xs text-fg-faint">{t('apps.status.soon')}</span>
          : <Badge tone={app.status === 'ready' ? 'success' : 'warning'}>{t(`apps.status.${app.status}` as MessageKey)}</Badge>}
      </span>
      <span className="truncate font-semibold text-fg">{name}</span>
      <span className="line-clamp-2 text-xs leading-[1.4] text-fg-muted">{t(appDescKey(app))}</span>
    </button>
  );
}

function EmptyBox({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-[58px] items-center justify-center rounded-md border border-default bg-surface px-4 text-center text-sm text-fg-faint">
      {children}
    </div>
  );
}
