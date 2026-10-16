import { Cloud, Highlighter, PanelRightClose, RotateCcw, Trash2 } from 'lucide-react';
import type { Change } from '../api';
import { shortText } from './geometry';

export type Filter = 'all' | 'text' | 'graphic';

const KIND_LABEL: Record<Change['kind'], string> = { added: '추가', removed: '삭제', modified: '변경' };

export function changeTitle(c: Change): string {
  return c.category === 'text' ? '글자' : KIND_LABEL[c.kind];
}

type Props = {
  changes: Change[];
  removedCount: number;
  filter: Filter;
  onFilter: (f: Filter) => void;
  selectedId: string | null;
  labels: Record<string, string>;
  editedIds: string[];
  onResetEdits: () => void;
  onSelect: (c: Change) => void;
  onRemove: (c: Change) => void;
  onRestoreAll: () => void;
  onCollapse: () => void;
};

/** 오른쪽 변경 목록: 종류 필터, 항목 누르면 그 위치로, 휴지통으로 지우기(PDF에서 빠짐). */
export function ChangeList({ changes, removedCount, filter, onFilter, selectedId, labels, editedIds, onResetEdits, onSelect, onRemove, onRestoreAll, onCollapse }: Props) {
  const edited = new Set(editedIds);
  const shown = changes.filter((c) => filter === 'all' || c.category === filter);
  const count = (f: Filter) => changes.filter((c) => f === 'all' || c.category === f).length;
  return (
    <aside aria-label="변경 목록" className="flex w-[300px] flex-none flex-col border-l border-line bg-white">
      <div className="flex h-12 items-center justify-between border-b border-[#eef0f3] pl-4 pr-2">
        <h2 className="text-[15px] font-bold">변경 {changes.length}건</h2>
        <button type="button" aria-label="변경 목록 접기" onClick={onCollapse}
          className="flex h-9 w-9 items-center justify-center rounded-lg text-muted hover:bg-[#eef0f3]">
          <PanelRightClose className="h-[18px] w-[18px]" aria-hidden />
        </button>
      </div>
      <div role="group" aria-label="종류 필터" className="flex flex-wrap gap-1.5 px-3 pt-3">
        {(['all', 'text', 'graphic'] as Filter[]).map((f) => (
          <button key={f} type="button" aria-pressed={filter === f} onClick={() => onFilter(f)}
            className={`inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-[13px] ${
              filter === f ? 'border-ink bg-ink font-semibold text-white' : 'border-[#d5d9e0] bg-white text-ink hover:bg-[#f4f5f7]'
            }`}>
            {f === 'text' && <Highlighter className="h-3.5 w-3.5" aria-hidden />}
            {f === 'graphic' && <Cloud className="h-3.5 w-3.5" aria-hidden />}
            {f === 'all' ? '전체' : f === 'text' ? '글자' : '그림'} {count(f)}
          </button>
        ))}
      </div>
      <ul className="flex flex-1 flex-col gap-0.5 overflow-auto p-2">
        {shown.length === 0 && <li className="p-6 text-center text-sm text-muted">이 페이지에는 변경이 없어요.</li>}
        {shown.map((c) => {
          const text = shortText(c.description);
          const selected = c.id === selectedId;
          return (
            <li key={c.id}>
              <div className={`flex items-center gap-2.5 rounded-[10px] p-2.5 ${selected ? 'bg-[#eff4ff] outline outline-2 outline-accent' : 'hover:bg-[#f4f5f7]'}`}>
                <button type="button" onClick={() => onSelect(c)} className="flex min-w-0 flex-1 items-center gap-2.5 text-left">
                  <span className={`flex h-[26px] w-[26px] flex-none items-center justify-center rounded-md ${
                    c.category === 'text' ? 'bg-[#fef3c7] text-[#a16207]' : 'bg-[#fee2e2] text-[#b91c1c]'}`}>
                    {c.category === 'text' ? <Highlighter className="h-[15px] w-[15px]" aria-hidden /> : <Cloud className="h-[15px] w-[15px]" aria-hidden />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-semibold">
                      {c.number} · {changeTitle(c)}
                      {labels[c.id] && <span className="ml-1.5 font-mono text-xs font-bold text-cloud">△{labels[c.id]}</span>}
                      {edited.has(c.id) && <span className="ml-1.5 rounded bg-accent-soft px-1.5 py-px text-[11px] font-medium text-accent">수정됨</span>}
                      {c.confidence === 'low' && <span className="ml-1.5 rounded bg-[#eef0f3] px-1.5 py-px text-[11px] font-medium text-[#4a505c]">확인 필요</span>}
                    </span>
                    {text && <span className="block truncate font-mono text-xs text-muted">{text}</span>}
                  </span>
                </button>
                <button type="button" aria-label={`변경 ${c.number} 지우기`} onClick={() => onRemove(c)}
                  className="flex h-[34px] w-[34px] flex-none items-center justify-center rounded-lg text-[#6b7280] hover:bg-white hover:text-red-700">
                  <Trash2 className="h-[15px] w-[15px]" aria-hidden />
                </button>
              </div>
            </li>
          );
        })}
      </ul>
      <div className="flex flex-col gap-1.5 border-t border-[#eef0f3] px-4 py-3 text-xs text-muted">
        <span>항목을 누르면 그 위치로 이동해요. 도면의 구름·△·하이라이트는 끌어서 옮기고, 선택하면 모서리로 크기를 바꿀 수 있어요. Delete 키로 지우기.</span>
        {editedIds.length > 0 && (
          <button type="button" onClick={onResetEdits} className="inline-flex items-center gap-1 self-start font-semibold text-accent hover:underline">
            <RotateCcw className="h-3.5 w-3.5" aria-hidden /> 표시 수정 {editedIds.length}개 되돌리기
          </button>
        )}
        {removedCount > 0 && (
          <button type="button" onClick={onRestoreAll} className="inline-flex items-center gap-1 self-start font-semibold text-accent hover:underline">
            <RotateCcw className="h-3.5 w-3.5" aria-hidden /> 지운 {removedCount}개 되돌리기
          </button>
        )}
      </div>
    </aside>
  );
}
