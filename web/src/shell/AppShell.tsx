import { useCallback, useEffect } from 'react';
import { activeTab, LEGACY_KINDS, useApp, type DocTab } from '../app/store';
import { ComparePage } from '../compare/ComparePage';
import { PngTool } from '../png/PngTool';
import { CommandLine } from './CommandLine';
import { HomeScreen } from './HomeScreen';
import { Ribbon } from './Ribbon';
import { StatusBar } from './StatusBar';
import { TitleBar } from './TitleBar';

/**
 * 공통 틀 (설계서 2-2): 제목줄 · 리본 · 탭 내용 · 명령창 · 상태 표시줄.
 * 모든 문서 탭은 그려 둔 채 숨기기만 해서, 탭을 바꿔도 확대·선택 같은 상태가 그대로 남는다 (2-1).
 * 비교·PNG 탭 안은 아직 옛 화면이다 (U3·U4에서 새 틀로 옮김).
 */
export function AppShell() {
  const tabs = useApp((s) => s.tabs);
  const active = useApp(activeTab);

  // 저장 안 된 탭이 있으면 브라우저 창을 닫기 전에 묻는다 (2-5)
  useEffect(() => {
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (useApp.getState().tabs.some((t) => t.dirty)) e.preventDefault();
    };
    // 파일 놓는 칸 밖에 떨어뜨려도 브라우저가 그 파일로 페이지를 바꾸지 않게 (열린 탭을 잃음)
    const stop = (e: DragEvent) => e.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    window.addEventListener('dragover', stop);
    window.addEventListener('drop', stop);
    return () => {
      window.removeEventListener('beforeunload', onBeforeUnload);
      window.removeEventListener('dragover', stop);
      window.removeEventListener('drop', stop);
    };
  }, []);

  return (
    <div className="flex h-full flex-col bg-background font-sans text-md text-fg">
      <TitleBar />
      {active.kind !== 'home' && <Ribbon tab={active} />}
      <div className="relative min-h-0 flex-1">
        {tabs.map((tab) => (
          <div key={tab.id} role="tabpanel" hidden={tab.id !== active.id} className="absolute inset-0">
            <TabContent tab={tab} active={tab.id === active.id} />
          </div>
        ))}
      </div>
      <CommandLine compact={LEGACY_KINDS.includes(active.kind)} />
      <StatusBar tab={active} />
    </div>
  );
}

function TabContent({ tab, active }: { tab: DocTab; active: boolean }) {
  const setDirty = useApp((s) => s.setDirty);
  const onDirtyChange = useCallback((dirty: boolean) => setDirty(tab.id, dirty), [setDirty, tab.id]);
  switch (tab.kind) {
    case 'home':
      return <HomeScreen active={active} />;
    case 'compare':
      return <div className="legacy h-full"><ComparePage active={active} initialFiles={tab.files} onDirtyChange={onDirtyChange} /></div>;
    case 'png':
      return <div className="legacy h-full"><PngTool initialFile={tab.files?.[0]} /></div>;
    case 'drawing':
      return null; // U6
  }
}
