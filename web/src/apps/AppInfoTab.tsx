import { CircleHelp, House } from 'lucide-react';
import { HOME_TAB_ID, useApp } from '../app/store';
import { useT, type MessageKey } from '../i18n';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';
import { EmptyState } from '../ui/EmptyState';
import { appById, appDescKey, appNameKey } from './registry';

const FLOW: Record<'job' | 'document', MessageKey[]> = {
  job: ['appTab.flow.job.1', 'appTab.flow.job.2', 'appTab.flow.job.3', 'appTab.flow.job.4'],
  document: ['appTab.flow.document.1', 'appTab.flow.document.2', 'appTab.flow.document.3'],
};

/**
 * 기능 안내 탭 (설계서 11-3): 아직 준비 중인 기능의 카드를 누르면 열린다.
 * 이름·설명·사용 흐름(예정)·계획 단계를 보여 주고, 파일은 받지 않는다 (PRD 15장: 준비 중은 분명히 표시).
 */
export function AppInfoTab({ appId }: { appId: string }) {
  const t = useT();
  const activate = useApp((s) => s.activate);
  const app = appById(appId);
  if (!app) return <EmptyState icon={CircleHelp}>{appId}</EmptyState>;
  const Icon = app.icon;
  const name = t(appNameKey(app));

  return (
    <div className="flex h-full justify-center overflow-auto bg-background px-6 py-10">
      <article className="flex w-full max-w-[640px] flex-col gap-5">
        <header className="flex items-start gap-4">
          <span className="flex h-12 w-12 flex-none items-center justify-center rounded-md bg-pressed">
            <Icon className="h-6 w-6 text-accent" strokeWidth={1.9} aria-hidden />
          </span>
          <div className="flex min-w-0 flex-col gap-1">
            <span className="flex flex-wrap items-center gap-2 text-xs text-fg-muted">
              <span>{t(`home.category.${app.category}` as MessageKey)}</span>
              <Badge>{t(`apps.status.${app.status}` as MessageKey)}</Badge>
            </span>
            <h1 className="m-0 text-xl font-bold leading-tight">{t('appTab.soonTitle', { name })}</h1>
            <p className="m-0 text-md text-fg-secondary">{t(appDescKey(app))}</p>
          </div>
        </header>

        <p className="m-0 rounded-md border border-default bg-surface px-4 py-3 text-sm text-fg-secondary">{t('appTab.soonBody')}</p>

        <section aria-labelledby={`flow-${app.id}`} className="flex flex-col gap-2.5">
          <h2 id={`flow-${app.id}`} className="m-0 text-sm font-semibold">{t('appTab.flow')}</h2>
          <ol className="m-0 flex list-none flex-col gap-2 p-0">
            {FLOW[app.screen].map((key, i) => (
              <li key={key} className="flex items-center gap-3 text-md text-fg-secondary">
                <span className="flex h-6 w-6 flex-none items-center justify-center rounded-full border border-strong text-xs text-fg-muted">{i + 1}</span>
                {t(key)}
              </li>
            ))}
          </ol>
        </section>

        <section className="flex items-center gap-2 text-sm">
          <span className="font-semibold">{t('appTab.plan')}</span>
          <span className="text-fg-muted">{t(`apps.stage.${app.stage}` as MessageKey)}</span>
        </section>

        <div>
          <Button icon={<House className="h-icon w-icon" aria-hidden />} onClick={() => activate(HOME_TAB_ID)}>{t('appTab.backHome')}</Button>
        </div>
      </article>
    </div>
  );
}
