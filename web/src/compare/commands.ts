import { activeTab, useApp } from '../app/store';
import type { CommandContext } from '../commands/registry';
import {
  exportCsv, exportMarked, setIncluded, setReviewed, setSettings, stepTo, swapSides,
} from './actions';
import { compareTab, useCompare } from './state';
import { pickFileFor } from './filePicker';

/**
 * 비교 탭 명령 (설계서 6-6 Compare 표 + 공통 명령 중 비교 탭에서 동작하는 것).
 * 등록표의 where로 비교 탭에서만 불리지만, 공통 명령(ZOOM·PAGE·UNDO…)은 탭 종류를 직접 본다.
 */

const isCompare = (ctx: CommandContext) => useApp.getState().tabs.find((t) => t.id === ctx.tabId)?.kind === 'compare';
const patch = (ctx: CommandContext, p: Parameters<ReturnType<typeof useCompare.getState>['patch']>[1]) => useCompare.getState().patch(ctx.tabId, p);
const t = (ctx: CommandContext) => compareTab(ctx.tabId);

/** 결과가 필요한 명령인데 아직 없으면 안내하고 false. */
function needResult(ctx: CommandContext): boolean {
  if (t(ctx).result) return true;
  ctx.print(ctx.t('compare.cmd.noResult'), 'error');
  return false;
}

/** 고른 변경이 필요한 명령. */
function needSelection(ctx: CommandContext): string | null {
  if (!needResult(ctx)) return null;
  const id = t(ctx).selectedId;
  if (!id) ctx.print(ctx.t('compare.cmd.noSelection'), 'error');
  return id;
}

const later = (ctx: CommandContext, name: string) => ctx.print(ctx.t('soon.laterMsg', { cmd: name }));

export const COMPARE_COMMANDS: Record<string, (ctx: CommandContext) => void | Promise<void>> = {
  SWAP: (ctx) => {
    if (!swapSides(ctx.tabId)) ctx.print(ctx.t('compare.cmd.noFiles'), 'error');
  },
  SBS: (ctx) => {
    patch(ctx, { viewMode: 'side' });
    ctx.print(ctx.t('compare.cmd.sideBySide'));
  },
  OVERLAY: (ctx) => {
    if (!needResult(ctx)) return;
    const on = t(ctx).viewMode !== 'overlay';
    patch(ctx, (s) => ({ viewMode: on ? 'overlay' : 'side', overlay: { ...s.overlay, nudging: false } }));
    ctx.print(ctx.t(on ? 'compare.cmd.overlayOn' : 'compare.cmd.sideBySide'));
  },
  SYNC: (ctx) => {
    const on = !t(ctx).sync;
    patch(ctx, { sync: on });
    ctx.print(ctx.t(on ? 'cmdline.on' : 'cmdline.off', { name: ctx.t('compare.syncName') }));
  },
  NEXT: (ctx) => {
    if (needResult(ctx) && !stepTo(ctx.tabId, 1)) ctx.print(ctx.t('compare.cmd.last'));
  },
  PREV: (ctx) => {
    if (needResult(ctx) && !stepTo(ctx.tabId, -1)) ctx.print(ctx.t('compare.cmd.first'));
  },
  REVIEWED: (ctx) => {
    const id = needSelection(ctx);
    if (!id) return;
    setReviewed(ctx.tabId, id, true);
    // 검토하면 다음 변경으로 (Bluebeam처럼 차례로 훑기)
    stepTo(ctx.tabId, 1);
  },
  EXCLUDE: (ctx) => {
    const id = needSelection(ctx);
    if (!id) return;
    const included = t(ctx).marks.excluded.includes(id);
    setIncluded(ctx.tabId, id, included);
    ctx.print(ctx.t(included ? 'compare.cmd.included' : 'compare.cmd.excluded'));
  },
  EXPORT: async (ctx) => {
    if (needResult(ctx)) await exportMarked(ctx.tabId);
  },
  EXPORTCSV: (ctx) => {
    if (needResult(ctx)) exportCsv(ctx.tabId);
  },
  NUDGE: (ctx) => {
    const s = t(ctx);
    if (!needResult(ctx)) return;
    if (s.viewMode !== 'overlay' || s.overlay.byColor) return ctx.print(ctx.t('compare.cmd.nudgeNeedsOverlay'), 'error');
    const on = !s.overlay.nudging;
    patch(ctx, { overlay: { ...s.overlay, nudging: on } });
    ctx.print(ctx.t(on ? 'compare.cmd.nudgeOn' : 'compare.cmd.nudgeOff'));
  },
  NUMBERING: async (ctx) => {
    const s = t(ctx).settings;
    const mode = await ctx.ask({ kind: 'option', prompt: ctx.t('compare.ask.numbering'), options: ['Each', 'Same'], defaultValue: s.numbering === 'same' ? 'Same' : 'Each' });
    if (!('option' in mode)) return;
    const start = await ctx.ask({ kind: 'text', prompt: `${ctx.t('compare.ask.start')} <${s.startLabel}>` });
    const label = 'text' in start ? start.text.trim().slice(0, 4) : s.startLabel;
    setSettings(ctx.tabId, { numbering: mode.option === 'Same' ? 'same' : 'sequential', startLabel: label || s.startLabel });
  },
  OPEN: (ctx) => {
    if (!isCompare(ctx)) return later(ctx, 'OPEN');
    const s = t(ctx);
    // 빈 칸부터 채운다 (둘 다 차 있으면 리비전을 바꿈)
    pickFileFor(ctx.tabId, !s.left.file ? 'left' : 'right');
  },
  ZOOM: async (ctx) => {
    if (!isCompare(ctx)) return later(ctx, 'ZOOM');
    const a = await ctx.ask({ kind: 'option', prompt: ctx.t('compare.ask.zoom'), options: ['Extents', 'Window', 'In', 'Out'], defaultValue: 'Extents' });
    if (!('option' in a)) return;
    if (a.option === 'Window') return later(ctx, 'ZOOM Window');
    const kind = a.option === 'In' ? 'in' : a.option === 'Out' ? 'out' : 'fit';
    patch(ctx, (s) => ({ viewRequest: { kind, n: (s.viewRequest?.n ?? 0) + 1 } }));
  },
  PAGE: async (ctx) => {
    if (!isCompare(ctx)) return later(ctx, 'PAGE');
    const s = t(ctx);
    const count = s.result?.pages.length ?? Math.max(s.left.file?.pages.length ?? 0, s.right.file?.pages.length ?? 0);
    if (count < 1) return ctx.print(ctx.t('compare.cmd.noFiles'), 'error');
    const a = await ctx.ask({ kind: 'number', prompt: ctx.t('compare.ask.page', { n: count }), options: ['Next', 'Previous'], defaultValue: undefined });
    let to = s.pairIndex;
    if ('option' in a) to += a.option === 'Next' ? 1 : -1;
    else if ('number' in a) to = Math.round(a.number) - 1;
    else return;
    if (to < 0 || to >= count) return ctx.print(ctx.t('compare.cmd.noPage', { n: count }), 'error');
    patch(ctx, { pairIndex: to, selectedId: null });
  },
  UNDO: (ctx) => {
    if (!isCompare(ctx)) return later(ctx, 'UNDO');
    if (!useCompare.getState().undo(ctx.tabId)) ctx.print(ctx.t('compare.cmd.nothingToUndo'));
  },
  REDO: (ctx) => {
    if (!isCompare(ctx)) return later(ctx, 'REDO');
    if (!useCompare.getState().redo(ctx.tabId)) ctx.print(ctx.t('compare.cmd.nothingToRedo'));
  },
};

/** 리본 버튼 상태 (눌림·흐림). 비교 탭이 아니면 비어 있음. */
export function compareRibbonState(): { pressed: Set<string>; disabled: Set<string> } {
  const pressed = new Set<string>();
  const disabled = new Set<string>();
  const tab = activeTab(useApp.getState());
  if (tab.kind !== 'compare') return { pressed, disabled };
  const s = compareTab(tab.id);
  if (s.viewMode === 'side') pressed.add('SBS');
  else pressed.add('OV');
  if (s.sync) pressed.add('SYNC');
  if (!s.left.file && !s.right.file) disabled.add('SWAP');
  if (!s.result) for (const c of ['OV', 'PREV', 'NEXT', 'REVIEWED', 'EXPORT', 'EXPORTCSV']) disabled.add(c);
  if (s.viewMode === 'overlay') disabled.add('SYNC');
  return { pressed, disabled };
}
