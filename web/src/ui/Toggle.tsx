type Props = { label: string; note?: string; on: boolean; onChange: (on: boolean) => void; disabled?: boolean };

/** 켜고 끄는 스위치 줄 (시안 02·04 오른쪽 패널): 이름 + 설명 + 스위치. */
export function Toggle({ label, note, on, onChange, disabled }: Props) {
  return (
    <label className={`flex items-center justify-between gap-3 ${disabled ? 'opacity-50' : 'cursor-pointer'}`}>
      <span className="flex min-w-0 flex-col">
        <span className="text-sm text-fg-secondary">{label}</span>
        {note && <span className="text-xs text-fg-faint">{note}</span>}
      </span>
      <button type="button" role="switch" aria-checked={on} aria-label={label} disabled={disabled} onClick={() => onChange(!on)}
        className={`relative h-[18px] w-[30px] flex-none rounded-full transition-colors duration-fast ease-standard ${on ? 'bg-primary' : 'bg-pressed'}`}>
        <span className={`absolute top-[3px] h-3 w-3 rounded-full bg-fg transition-[left] duration-fast ease-standard ${on ? 'left-[15px]' : 'left-[3px]'}`} />
      </button>
    </label>
  );
}
