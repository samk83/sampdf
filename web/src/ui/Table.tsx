import type { ReactNode } from 'react';

export type Column<T> = { key: string; header: ReactNode; width?: string; align?: 'left' | 'right' | 'center'; render: (row: T) => ReactNode };

type Props<T> = {
  label: string;
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  selectedKey?: string | null;
  onRowClick?: (row: T) => void;
  /** 흐리게 보일 줄 (예: 내보내기에서 뺀 변경). */
  dimmed?: (row: T) => boolean;
  empty?: ReactNode;
};

/**
 * 표 (설계서 4-2: 변경 표·마크업 목록). 머리 줄 고정, 고른 줄은 왼쪽 강조 막대 (시안 04).
 * 줄 안의 버튼·체크 칸을 눌러도 줄 선택이 같이 일어나지 않게 하려면 그 부품에서 stopPropagation.
 */
export function Table<T>({ label, columns, rows, rowKey, selectedKey, onRowClick, dimmed, empty }: Props<T>) {
  if (!rows.length && empty) return <>{empty}</>;
  return (
    <div className="min-h-0 flex-1 overflow-auto">
      <table aria-label={label} className="w-full table-fixed border-collapse text-sm">
        <colgroup>{columns.map((c) => <col key={c.key} style={c.width ? { width: c.width } : undefined} />)}</colgroup>
        <thead className="sticky top-0 z-[1] bg-sunken">
          <tr className="h-[26px] border-b border-subtle text-left text-xs text-fg-muted">
            {columns.map((c) => <th key={c.key} scope="col" className={`px-2.5 font-normal ${c.align === 'right' ? 'text-right' : c.align === 'center' ? 'text-center' : ''}`}>{c.header}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const key = rowKey(row);
            const on = key === selectedKey;
            return (
              <tr key={key} aria-selected={on} onClick={() => onRowClick?.(row)} tabIndex={onRowClick ? 0 : undefined}
                onKeyDown={(e) => {
                  if (onRowClick && e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); onRowClick(row); }
                }}
                className={`h-7 cursor-pointer border-b border-subtle ${on ? 'bg-accent-subtle shadow-[inset_3px_0_0_var(--color-accent)]' : 'hover:bg-pressed'} ${dimmed?.(row) ? 'text-fg-faint' : 'text-fg-secondary'}`}>
                {columns.map((c) => <td key={c.key} className={`truncate px-2.5 ${c.align === 'right' ? 'text-right' : c.align === 'center' ? 'text-center' : ''}`}>{c.render(row)}</td>)}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
