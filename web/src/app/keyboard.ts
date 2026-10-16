/**
 * 키보드가 어디로 가나 (설계서 2-5, 6-7). 순수 함수라 테스트로 확인한다.
 * 실제 연결은 shell/useGlobalKeys.ts.
 */

/** 키를 누를 때 커서(포커스)가 있는 곳. */
export type KeyTarget =
  | 'cmdline' // 명령창 입력칸
  | 'field' // 패널·대화상자의 입력칸 (글자는 그 칸으로)
  | 'control' // 버튼·탭 (Enter·Space는 그 버튼을 누름)
  | 'body'; // 그 밖 (도면 위 등)

export type KeyInput = { key: string; code?: string; ctrl: boolean; shift: boolean; alt: boolean; meta: boolean; target: KeyTarget; modalOpen: boolean };

export type KeyAction =
  | { kind: 'run'; command: string; contextual?: boolean } // 명령 실행 (단축키). contextual: 그 탭에 없는 명령이면 브라우저 기본 동작 그대로
  | { kind: 'osnap' } // F3: 오스냅 켜고 끄기 (OSNAP 명령은 설정 창이라 따로)
  | { kind: 'history' } // F2: 기록 펼치기
  | { kind: 'type'; char: string } // 명령창에 글자 넣기
  | { kind: 'submit' } // 빈 곳에서 Enter·Space = 명령창 Enter (직전 명령 반복)
  | { kind: 'cancel' }; // 빈 곳에서 Esc = 지금 명령 취소

/**
 * 단축키 (동작하는 명령만). 입력칸에 커서가 있어도 동작한다 (6-7).
 * F11·Ctrl+S 같은 키는 그 명령이 생기는 단위에서 연결한다 (지금은 브라우저 기본 동작 그대로).
 */
const F_KEYS: Record<string, KeyAction> = {
  F1: { kind: 'run', command: 'HELP' },
  F2: { kind: 'history' },
  F3: { kind: 'osnap' },
  F7: { kind: 'run', command: 'GRID' },
  F8: { kind: 'run', command: 'ORTHO' },
  F9: { kind: 'run', command: 'SNAP' },
};
const CTRL_KEYS: Record<string, KeyAction> = {
  '0': { kind: 'run', command: 'PANELS' },
  '9': { kind: 'run', command: 'CMDLINE' },
  o: { kind: 'run', command: 'OPEN', contextual: true },
  e: { kind: 'run', command: 'EXPORT', contextual: true },
};
/** 입력칸 밖에서만: 입력칸 안의 Ctrl+Z·PgUp은 그 칸의 글자 되돌리기·이동이다. */
const CTRL_OUTSIDE_FIELDS: Record<string, KeyAction> = {
  z: { kind: 'run', command: 'UNDO', contextual: true },
  y: { kind: 'run', command: 'REDO', contextual: true },
};
const PLAIN_OUTSIDE_FIELDS: Record<string, KeyAction> = {
  PageUp: { kind: 'run', command: 'PAGE Previous', contextual: true },
  PageDown: { kind: 'run', command: 'PAGE Next', contextual: true },
};

export function routeKey(k: KeyInput): KeyAction | null {
  const plain = !k.ctrl && !k.alt && !k.meta;
  if (plain && !k.shift && F_KEYS[k.key]) return F_KEYS[k.key];
  const ctrlKey = k.key.length === 1 ? k.key.toLowerCase() : k.key;
  const inField = k.target === 'cmdline' || k.target === 'field';
  if (k.ctrl && !k.alt && !k.meta && !k.shift) {
    if (CTRL_KEYS[ctrlKey]) return CTRL_KEYS[ctrlKey];
    if (!inField && !k.modalOpen && CTRL_OUTSIDE_FIELDS[ctrlKey]) return CTRL_OUTSIDE_FIELDS[ctrlKey];
  }
  if (!plain) return null;
  // 대화상자가 떠 있으면 글자·Enter·Esc는 대화상자 몫
  if (k.modalOpen) return null;
  // 입력칸에서는 그 칸이 글자를 받는다 (명령창은 자기 입력칸이 직접 처리)
  if (inField) return null;
  if (!k.shift && PLAIN_OUTSIDE_FIELDS[k.key]) return PLAIN_OUTSIDE_FIELDS[k.key];
  if (k.key === 'Escape') return { kind: 'cancel' };
  if (k.key === 'Enter' || k.key === ' ') return k.target === 'body' ? { kind: 'submit' } : null;
  if (k.key.length === 1 && k.key >= ' ' && k.key <= '~') return { kind: 'type', char: k.key };
  // 한글 입력 상태: key가 'Process'나 한글이어도 자판 위치(code)로 영어 글자를 보낸다
  const letter = k.code?.match(/^Key([A-Z])$/);
  if (letter) return { kind: 'type', char: k.shift ? letter[1] : letter[1].toLowerCase() };
  if (k.key.length === 1) return { kind: 'type', char: k.key };
  return null;
}

/** 포커스가 있는 요소가 어떤 곳인지. */
export function classifyTarget(el: Element | null): KeyTarget {
  if (!el || el === el.ownerDocument?.body) return 'body';
  if (el.closest('[data-cmdline-input]')) return 'cmdline';
  if (el.closest('input, textarea, select, [contenteditable=""], [contenteditable="true"]')) return 'field';
  if (el.closest('button, a[href], summary, [tabindex]:not([tabindex="-1"]), [role="button"], [role="tab"], [role="option"], [role="checkbox"], [role="switch"], [role="radio"], [role="menuitem"], [role="slider"]')) return 'control';
  return 'body';
}
