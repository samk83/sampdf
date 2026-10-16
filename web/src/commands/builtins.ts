import { activeTab, HOME_TAB_ID, useApp, type StatusToggleId } from '../app/store';
import { COMPARE_COMMANDS } from '../compare/commands';
import { translate, type MessageKey } from '../i18n';
import type { CommandContext } from './registry';

const st = () => useApp.getState();
const tr = (key: MessageKey, params?: Record<string, string | number>) => translate(st().language, key, params);


/** 작도 보조 켜고 끄기 + 기록 한 줄. F3·F7·F8·F9와 상태 표시줄 토글이 쓴다. */
export function toggleAid(id: StatusToggleId) {
  st().toggle(id);
  const on = st().toggles[id];
  st().log({ text: tr(on ? 'cmdline.on' : 'cmdline.off', { name: tr(`aid.${id}` as MessageKey) }), kind: 'info' });
}

export function togglePanels() {
  const hidden = !st().panelsHidden;
  st().setUi({ panelsHidden: hidden });
  st().log({ text: tr(hidden ? 'cmdline.off' : 'cmdline.on', { name: tr('aid.panels') }), kind: 'info' });
}

export function toggleCmdline() {
  st().setUi({ cmdlineHidden: !st().cmdlineHidden });
}

function switchTab(step: 1 | -1, ctx: CommandContext) {
  const { tabs, activeId, activate } = st();
  if (tabs.length < 2) return ctx.print(ctx.t('ask.onlyTab'));
  const i = tabs.findIndex((t) => t.id === activeId);
  activate(tabs[(i + step + tabs.length) % tabs.length].id);
}

/**
 * 실제로 동작하는 명령 (U2 공통 + U3 비교 탭). 나머지 명령은 등록만 되어 있고,
 * 부르면 "다음 업데이트에서" 안내가 나온다 (runner.ts).
 */
export const BUILTINS: Record<string, (ctx: CommandContext) => void | Promise<void>> = {
  HELP: () => st().setUi({ helpOpen: true }),
  GRID: () => toggleAid('grid'),
  ORTHO: () => toggleAid('ortho'),
  SNAP: () => toggleAid('snap'),
  PANELS: () => togglePanels(),
  CMDLINE: () => toggleCmdline(),
  TABNEXT: (ctx) => switchTab(1, ctx),
  TABPREV: (ctx) => switchTab(-1, ctx),
  CMP: () => { st().openTab('compare'); },
  PNG: () => { st().openTab('png'); },
  EXIT: async (ctx) => {
    const tab = activeTab(st());
    if (tab.id === HOME_TAB_ID) return ctx.print(ctx.t('ask.homeCantClose'), 'error');
    if (tab.dirty) {
      const name = tab.title ?? ctx.t(`tabs.${tab.kind}` as MessageKey);
      const answer = await ctx.ask({ kind: 'option', prompt: ctx.t('ask.exitUnsaved', { name }), options: ['Yes', 'No'], defaultValue: 'No' });
      if (!('option' in answer) || answer.option !== 'Yes') return;
    }
    st().closeTab(tab.id);
  },
  ...COMPARE_COMMANDS,
};

/** 다른 명령이 도는 중에도 끼어들어 실행하는 명령 (오토캐드의 투명 명령처럼, 지금 명령을 취소하지 않음). */
export const TRANSPARENT = new Set(['HELP', 'GRID', 'ORTHO', 'SNAP', 'PANELS', 'CMDLINE']);
