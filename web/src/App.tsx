import { useState } from 'react';
import { ComparePage } from './compare/ComparePage';
import { PngTool } from './png/PngTool';
import type { Tab } from './ui/AppHeader';

/** 두 기능(도면 비교, 투명 PNG)을 탭으로 오간다. 비교 화면은 탭을 바꿔도 상태를 유지하도록 숨기기만 한다. */
export default function App() {
  const [tab, setTab] = useState<Tab>('compare');
  return (
    <>
      <div hidden={tab !== 'compare'}><ComparePage tab={tab} onTab={setTab} /></div>
      <div hidden={tab !== 'png'}><PngTool tab={tab} onTab={setTab} /></div>
    </>
  );
}
