import { useEffect, useRef, useState, type ReactNode } from 'react';
import { File as FileIcon, GitCompare, House, Image, Search, SearchX, Settings, Upload, Wrench, type LucideIcon } from 'lucide-react';
import { compareTitle, routeDrop } from '../app/dropRouting';
import { useApp } from '../app/store';
import { TARGET } from '../app/target';
import { APPS, CATEGORIES, appDescKey, appNameKey, searchApps, type AppCategory, type AppDef } from '../apps/registry';
import { translate, useT, type MessageKey } from '../i18n';
import { runCommand } from '../commands/runner';
import { Badge } from '../ui/Badge';
import { EmptyState } from '../ui/EmptyState';
import { useNotYet } from './notYet';

/**
 * 시작 화면 (확정 시안 01 + 설계서 11-3 기능 카드). 큰 카드 3개 자리에 기능 24개 카드(검색·갈래)를 둔다.
 * 최근 파일·비교 목록은 U5에서 채운다.
 * `px-[30px]` 같은 대괄호 크기는 시안 값 그대로 (간격 토큰 4·8·12…에 없는 값).
 */
export function HomeScreen({ active }: { active: boolean }) {
  const t = useT();
  const notYet = useNotYet();
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
        <NavItem icon={Image} label={t('home.nav.png')} onClick={() => void runCommand('PNG')} />
        <NavItem icon={Wrench} label={t('home.nav.toolSets')} tag={t('soon.laterTag')} onClick={() => notYet('TOOLSETS', 'later')} />
        <NavItem icon={Settings} label={t('home.nav.settings')} onClick={() => void runCommand('OPTIONS')} />
      </nav>}

      <main className="flex min-w-0 flex-1 flex-col gap-5 overflow-auto px-[30px] py-6">
        <div className="flex items-end justify-between gap-4">
          <div className="flex flex-col gap-1">
            <h1 className="m-0 text-xl font-bold leading-tight">{t('home.title')}</h1>
            <span className="text-fg-muted">{t('home.subtitle')}</span>
          </div>
          <span className="text-xs text-fg-faint">{t(TARGET === 'web' ? 'home.web' : 'home.local')}</span>
        </div>

        <ToolLauncher />

        <div className="grid flex-none grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)] gap-[18px]">
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

type Filter = AppCategory | 'all';

/** 기능 카드 묶음: 검색 칸 + 갈래 고르기 + 카드 격자 (설계서 11-3). */
function ToolLauncher() {
  const t = useT();
  const language = useApp((s) => s.language);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const inCategory = filter === 'all' ? APPS : APPS.filter((a) => a.category === filter);
  // 지금 언어와 영어 이름·설명 둘 다로 찾는다 (한국어 화면에서 "merge"로도 찾게)
  const found = searchApps(inCategory, query, (app) => [appNameKey(app), appDescKey(app)].flatMap((k) => [translate(language, k), translate('en', k)]));
  const grouped = filter === 'all' && !query.trim();

  return (
    <section aria-label={t('home.tools')} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex h-control w-full max-w-[280px] items-center gap-2 rounded-sm border border-strong bg-sunken px-2.5 focus-within:border-accent">
          <Search className="h-icon w-icon flex-none text-fg-faint" aria-hidden />
          <input type="search" value={query} placeholder={t('home.search')} aria-label={t('home.search')} aria-describedby="home-search-hint"
            onChange={(e) => setQuery(e.target.value)} onKeyDown={(e) => { if (e.key === 'Escape') { if (query) setQuery(''); else e.currentTarget.blur(); } }}
            className="min-w-0 flex-1 bg-transparent text-sm text-fg outline-none placeholder:text-fg-faint" />
        </label>
        <span id="home-search-hint" className="sr-only">{t('home.searchHint')}</span>
        <div role="group" aria-label={t('home.category')} className="flex flex-wrap gap-1">
          {(['all', ...CATEGORIES] as Filter[]).map((c) => (
            <button key={c} type="button" aria-pressed={filter === c} onClick={() => setFilter(c)}
              className={`h-control rounded-sm px-3 text-sm transition-colors duration-fast ${
                filter === c ? 'bg-pressed font-semibold text-fg' : 'text-fg-muted hover:bg-pressed hover:text-fg'}`}>
              {t(`home.category.${c}` as MessageKey)}
            </button>
          ))}
        </div>
        <span aria-live="polite" className="ml-auto text-xs text-fg-faint">{t('apps.count', { n: found.length })}</span>
      </div>

      {found.length === 0 ? (
        <div className="rounded-md border border-default bg-surface">
          <EmptyState icon={SearchX}>{t('home.searchEmpty', { q: query.trim() })}</EmptyState>
        </div>
      ) : grouped ? (
        CATEGORIES.map((c) => (
          <div key={c} className="flex flex-col gap-2">
            <h2 className="m-0 text-sm font-semibold text-fg-secondary">{t(`home.category.${c}` as MessageKey)}</h2>
            <ToolGrid apps={found.filter((a) => a.category === c)} />
          </div>
        ))
      ) : (
        <ToolGrid apps={found} />
      )}
    </section>
  );
}

function ToolGrid({ apps }: { apps: AppDef[] }) {
  return (
    <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(232px,1fr))] gap-2.5 p-0">
      {apps.map((app) => <li key={app.id} className="flex"><ToolCard app={app} /></li>)}
    </ul>
  );
}

/** 기능 카드 하나. Ready면 그 기능의 명령을 부르고, 준비 중이면 기능 안내 탭을 연다. */
function ToolCard({ app }: { app: AppDef }) {
  const t = useT();
  const openApp = useApp((s) => s.openApp);
  const Icon = app.icon;
  const name = t(appNameKey(app));
  const ready = app.status !== 'soon' && !!app.command;
  const open = () => (ready ? void runCommand(app.command!) : openApp(app.id));
  return (
    <button type="button" onClick={open} title={ready ? t('apps.openHint', { name }) : t('apps.soonHint', { name })}
      className={`flex min-h-[92px] w-full items-start gap-3 rounded-md border px-3.5 py-3 text-left transition-colors duration-fast ${
        ready ? 'border-default bg-surface hover:border-accent hover:bg-pressed' : 'border-default bg-surface hover:border-strong hover:bg-pressed'}`}>
      <span className={`flex h-9 w-9 flex-none items-center justify-center rounded-md ${ready ? 'bg-accent-subtle' : 'bg-pressed'}`}>
        <Icon className={`h-icon-ribbon w-icon-ribbon ${ready ? 'text-accent' : 'text-fg-muted'}`} strokeWidth={1.9} aria-hidden />
      </span>
      <span className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="flex items-center justify-between gap-2">
          <span className={`truncate font-semibold ${ready ? 'text-fg' : 'text-fg-secondary'}`}>{name}</span>
          <span className="flex flex-none items-center gap-1">
            {ready && app.command && <Badge tone="outline">{app.command}</Badge>}
            <Badge tone={app.status === 'ready' ? 'success' : 'neutral'}>{t(`apps.status.${app.status}` as MessageKey)}</Badge>
          </span>
        </span>
        <span className="line-clamp-2 text-xs leading-[1.45] text-fg-muted">{t(appDescKey(app))}</span>
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
