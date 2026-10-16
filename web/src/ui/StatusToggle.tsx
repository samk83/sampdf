type Props = { label: string; on: boolean; onToggle: () => void; title?: string };

/** 상태 표시줄의 GRID·SNAP·ORTHO·OSNAP·DARK VIEW 켜고 끄기 (brand.md 화면 뼈대). */
export function StatusToggle({ label, on, onToggle, title }: Props) {
  return (
    <button type="button" aria-pressed={on} title={title} onClick={onToggle}
      className={`h-[18px] rounded-sm border px-1.5 text-2xs tracking-[0.03em] transition-colors duration-fast ease-standard ${
        on ? 'border-accent bg-accent-subtle font-bold text-accent' : 'border-default text-fg-faint hover:text-fg-secondary'
      }`}>
      {label}
    </button>
  );
}
