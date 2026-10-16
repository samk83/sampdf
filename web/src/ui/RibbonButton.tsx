import type { LucideIcon } from 'lucide-react';

type Props = {
  icon: LucideIcon;
  label: string;
  tooltip: string;
  onClick: () => void;
  pressed?: boolean;
  /** 흐리게 + 꼬리표 (예: "Phase 2", 설계서 D1). 눌러도 명령창에 안내만 나온다. */
  tag?: string;
};

/** 리본 버튼: 20px 아이콘(선 1.7) + 글자 (brand.md 아이콘 규칙). */
export function RibbonButton({ icon: Icon, label, tooltip, onClick, pressed, tag }: Props) {
  return (
    <button type="button" title={tooltip} onClick={onClick} aria-pressed={pressed} aria-disabled={tag ? true : undefined}
      className={`flex min-w-[52px] flex-col items-center justify-center ${tag ? 'gap-0.5' : 'gap-1'} rounded-sm px-1 text-xs transition-colors duration-fast ease-standard ${
        tag ? 'text-fg-faint' : pressed ? 'bg-pressed text-fg shadow-[inset_0_0_0_1px_var(--color-accent)]' : 'text-fg hover:bg-pressed'
      }`}>
      <Icon className={`h-icon-ribbon w-icon-ribbon ${tag ? 'opacity-50' : ''}`} strokeWidth={1.7} aria-hidden />
      <span className="whitespace-nowrap">{label}</span>
      {tag && (
        <span className="rounded-sm border border-strong px-1 text-2xs leading-none text-fg-faint">
          {tag}
        </span>
      )}
    </button>
  );
}
