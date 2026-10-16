import type { ReactNode } from 'react';
import { GitCompare, ImageIcon, Columns2 } from 'lucide-react';

export type Tab = 'compare' | 'png';

type Props = { tab: Tab; onTab: (t: Tab) => void; left?: ReactNode; right?: ReactNode };

/** 모든 화면 공통 위쪽 막대: 이름, [도면 비교][투명 PNG] 탭, 화면별 도구 자리. */
export function AppHeader({ tab, onTab, left, right }: Props) {
  return (
    <header className="flex min-h-14 flex-wrap items-center justify-between gap-2.5 border-b border-line bg-white px-4 py-1.5">
      <div className="flex flex-wrap items-center gap-3.5">
        <div className="flex items-center gap-2">
          <span className="flex h-[30px] w-[30px] items-center justify-center rounded-[7px] bg-ink text-white">
            <GitCompare className="h-[18px] w-[18px]" aria-hidden />
          </span>
          <span className="text-[15px] font-bold">Sam Drawing Tools</span>
        </div>
        <nav aria-label="기능" className="flex rounded-lg bg-[#eef0f3] p-[3px]">
          <TabButton active={tab === 'compare'} onClick={() => onTab('compare')} icon={<Columns2 className="h-[15px] w-[15px]" aria-hidden />} label="도면 비교" />
          <TabButton active={tab === 'png'} onClick={() => onTab('png')} icon={<ImageIcon className="h-[15px] w-[15px]" aria-hidden />} label="투명 PNG" />
        </nav>
        {left}
      </div>
      <div className="flex flex-wrap items-center gap-2">{right}</div>
    </header>
  );
}

function TabButton({ active, onClick, icon, label }: { active: boolean; onClick: () => void; icon: ReactNode; label: string }) {
  return (
    <button type="button" onClick={onClick} aria-current={active ? 'page' : undefined}
      className={`inline-flex h-[34px] items-center gap-1.5 rounded-md px-3 text-[13px] ${
        active ? 'bg-white font-semibold text-ink shadow-[0_1px_2px_rgba(0,0,0,0.08)]' : 'text-[#3f4552] hover:text-ink'
      }`}>
      {icon}
      {label}
    </button>
  );
}

/** 아이콘만 있는 도구 버튼 (설명은 aria-label과 마우스를 올렸을 때 나오는 title). */
export function IconButton({ label, onClick, children, pressed, disabled }: {
  label: string; onClick: () => void; children: ReactNode; pressed?: boolean; disabled?: boolean;
}) {
  return (
    <button type="button" aria-label={label} title={label} aria-pressed={pressed} disabled={disabled} onClick={onClick}
      className={`flex h-9 w-9 items-center justify-center rounded-lg disabled:opacity-40 ${
        pressed ? 'bg-accent-soft text-accent' : 'text-[#3f4552] hover:bg-[#eef0f3]'
      }`}>
      {children}
    </button>
  );
}
