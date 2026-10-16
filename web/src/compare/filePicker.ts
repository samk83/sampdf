import { ACCEPT } from './FileDrop';
import { openFile } from './actions';
import type { SideKey } from './state';

/**
 * 파일 고르기 창을 열어 비교 탭의 한 칸에 연다 (OPEN 명령, Browse files 버튼).
 * 브라우저는 사람이 누르거나 친 직후에만 이 창을 열어 준다 (명령창 Enter·버튼 누름은 됨).
 */
export function pickFileFor(tabId: string, side: SideKey): void {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = ACCEPT;
  input.onchange = () => {
    const f = input.files?.[0];
    if (f) void openFile(tabId, side, f);
  };
  input.click();
}
