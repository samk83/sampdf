import type { ReactNode } from 'react';

/** 둘 이상 중 하나를 고르는 버튼 묶음 (예: 나란히 / 겹쳐 보기). */
export function Segmented<T extends string>({ label, value, options, onChange }: {
  label: string;
  value: T;
  options: { value: T; label: string; icon?: ReactNode }[];
  onChange: (v: T) => void;
}) {
  return (
    <div role="radiogroup" aria-label={label} className="flex rounded-lg bg-[#eef0f3] p-[3px]">
      {options.map((o) => (
        <button key={o.value} type="button" role="radio" aria-checked={value === o.value} onClick={() => onChange(o.value)}
          className={`inline-flex h-[30px] items-center gap-1.5 whitespace-nowrap rounded-md px-2.5 text-[13px] ${
            value === o.value ? 'bg-white font-semibold text-legacy-ink shadow-[0_1px_2px_rgba(0,0,0,0.08)]' : 'text-[#3f4552] hover:text-legacy-ink'
          }`}>
          {o.icon}
          {o.label}
        </button>
      ))}
    </div>
  );
}
