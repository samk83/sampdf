import type { TabKind } from '../app/store';
import type { MessageKey } from '../i18n';
import type { PointInput } from './parseInput';

/** 명령이 묻는 질문 (설계서 6-3). point·select는 U7a에서 도면 클릭과 연결된다. */
export type Ask =
  | { kind: 'option'; prompt: string; options: string[]; defaultValue?: string }
  | { kind: 'number'; prompt: string; options?: string[]; defaultValue?: number }
  | { kind: 'text'; prompt: string }
  | { kind: 'point'; prompt: string; options?: string[] }
  | { kind: 'select'; prompt: string };

/**
 * 질문의 답. option = 고른 옵션 단어(대괄호 안 영어 그대로), done = 빈 Enter (지금 단계 끝내기).
 * point는 해석만 한 입력이다 (직전 점·커서 방향을 아는 명령이 resolvePoint로 실제 점을 만든다).
 */
export type Answer = { option: string } | { number: number } | { text: string } | { point: PointInput } | { done: true };

export type CommandContext = {
  /** 명령창에 질문을 보이고 답을 기다린다. Esc면 CommandCancelled로 끝난다. */
  ask: (ask: Ask) => Promise<Answer>;
  /** 명령창 기록에 한 줄. */
  print: (text: string, kind?: 'info' | 'ok' | 'error') => void;
  /** 지금 문서 탭. */
  tabId: string;
  /** 지금 언어로 번역 (명령은 React 밖에서 돌아서 useT를 못 쓴다). */
  t: (key: MessageKey, params?: Record<string, string | number>) => string;
};

/** 명령 목록 창(`?`)에서 묶는 갈래 (설계서 6-6 표). */
export type CommandGroup = 'common' | 'draw' | 'markup' | 'compare' | 'tools' | 'phase2';
export const COMMAND_GROUPS: CommandGroup[] = ['common', 'draw', 'markup', 'compare', 'tools', 'phase2'];

export type CommandDef = {
  /** 명령 이름 (모든 언어에서 영어). */
  name: string;
  group: CommandGroup;
  /** 기본 별칭 (설정에서 바꿀 수 있음, U9). */
  aliases: string[];
  /** 단축키 표시 (실제 키 연결은 app/keyboard.ts). */
  shortcut?: string;
  /** 쓸 수 있는 탭. 없으면 모든 탭. */
  where?: TabKind[];
  /** 2 = 2단계 기능 (흐리게 + "Phase 2", D1). 설명 글자는 en.ts의 `cmd.<이름>`. */
  phase?: 2;
  /** 실행. 없으면 "다음 업데이트에서" 안내 (등록만 된 명령). */
  run?: (ctx: CommandContext) => void | Promise<void>;
};

export const ALL_TABS: TabKind[] = ['home', 'drawing', 'compare', 'png'];
const DOC: TabKind[] = ['drawing', 'compare'];
const CMP: TabKind[] = ['compare'];
const PNG: TabKind[] = ['png'];
const DRAWING: TabKind[] = ['drawing'];

/**
 * 1단계 명령 목록 (설계서 6-6). 실행 함수(run)는 commands/builtins.ts에서 붙인다.
 * 리본·자동완성·`?` 목록·설정의 별칭 표가 모두 이 표를 읽는다.
 */
export const COMMANDS: CommandDef[] = [
  // 공통
  { name: 'OPEN', group: 'common', aliases: [], shortcut: 'Ctrl+O' },
  { name: 'QSAVE', group: 'common', aliases: [], shortcut: 'Ctrl+S', where: DOC },
  { name: 'SAVEAS', group: 'common', aliases: [], shortcut: 'Ctrl+Shift+S', where: DOC },
  { name: 'EXPORT', group: 'common', aliases: ['EXP'], shortcut: 'Ctrl+E', where: DOC },
  { name: 'EXIT', group: 'common', aliases: [] },
  { name: 'TABNEXT', group: 'common', aliases: [] },
  { name: 'TABPREV', group: 'common', aliases: [] },
  { name: 'CMP', group: 'common', aliases: ['COMPARE'] },
  { name: 'PNG', group: 'common', aliases: [] },
  { name: 'OPTIONS', group: 'common', aliases: ['OP'] },
  { name: 'UNDO', group: 'common', aliases: ['U'], shortcut: 'Ctrl+Z', where: [...DOC, 'png'] },
  { name: 'REDO', group: 'common', aliases: [], shortcut: 'Ctrl+Y', where: [...DOC, 'png'] },
  { name: 'ZOOM', group: 'common', aliases: ['Z'], where: DOC },
  { name: 'PAN', group: 'common', aliases: ['P'], where: DOC },
  { name: 'PAGE', group: 'common', aliases: ['PG'], shortcut: 'PgUp/PgDn', where: DOC },
  { name: 'FOCUS', group: 'common', aliases: [], shortcut: 'F11', where: DOC },
  { name: 'DARKVIEW', group: 'common', aliases: ['DV'], where: DOC },
  { name: 'GRID', group: 'common', aliases: [], shortcut: 'F7' },
  { name: 'ORTHO', group: 'common', aliases: [], shortcut: 'F8' },
  { name: 'SNAP', group: 'common', aliases: [], shortcut: 'F9' },
  { name: 'OSNAP', group: 'common', aliases: ['OS'], shortcut: 'F3' },
  { name: 'PANELS', group: 'common', aliases: [], shortcut: 'Ctrl+0' },
  { name: 'CMDLINE', group: 'common', aliases: [], shortcut: 'Ctrl+9' },
  { name: 'PSCALE', group: 'common', aliases: [], where: DRAWING },
  { name: 'HELP', group: 'common', aliases: ['?'], shortcut: 'F1' },
  { name: 'SELECT', group: 'common', aliases: [], where: DOC },
  { name: 'COPYCLIP', group: 'common', aliases: [], shortcut: 'Ctrl+C', where: [...DOC, 'png'] },
  { name: 'PASTECLIP', group: 'common', aliases: [], shortcut: 'Ctrl+V', where: [...DOC, 'png'] },
  { name: 'PAGES', group: 'common', aliases: [], where: DOC },
  { name: 'BOOKMARKS', group: 'common', aliases: [], where: DRAWING },
  // 작도 (Draw)
  { name: 'LINE', group: 'draw', aliases: ['L'], where: DOC },
  { name: 'PLINE', group: 'draw', aliases: ['PL'], where: DOC },
  { name: 'CIRCLE', group: 'draw', aliases: ['C'], where: DOC },
  { name: 'RECTANG', group: 'draw', aliases: ['REC'], where: DOC },
  { name: 'REVCLOUD', group: 'draw', aliases: ['RC'], where: DOC },
  { name: 'HATCH', group: 'draw', aliases: ['H'], where: DOC },
  { name: 'TEXT', group: 'draw', aliases: ['T', 'DT'], where: DOC },
  { name: 'MOVE', group: 'draw', aliases: ['M'], where: DOC },
  { name: 'COPY', group: 'draw', aliases: ['CO', 'CP'], where: DOC },
  { name: 'ERASE', group: 'draw', aliases: ['E'], shortcut: 'Delete', where: DOC },
  { name: 'LAYER', group: 'draw', aliases: ['LA'], where: DOC },
  { name: 'PROPERTIES', group: 'draw', aliases: ['PR', 'CH'], where: DOC },
  { name: 'DELTA', group: 'draw', aliases: [], where: DOC },
  // 표시 (Markup)
  { name: 'TEXTBOX', group: 'markup', aliases: ['TB'], where: DOC },
  { name: 'CALLOUT', group: 'markup', aliases: ['CA'], where: DOC },
  { name: 'ARROW', group: 'markup', aliases: ['AW'], where: DOC },
  { name: 'ELLIPSE', group: 'markup', aliases: ['EL'], where: DOC },
  { name: 'PEN', group: 'markup', aliases: [], where: DOC },
  { name: 'HIGHLIGHT', group: 'markup', aliases: ['HL'], where: DOC },
  { name: 'STATUS', group: 'markup', aliases: ['RS'], where: DOC },
  { name: 'MARKUPS', group: 'markup', aliases: ['MK'], where: DRAWING },
  // 비교
  { name: 'SWAP', group: 'compare', aliases: [], where: CMP },
  { name: 'SBS', group: 'compare', aliases: [], where: CMP },
  { name: 'OVERLAY', group: 'compare', aliases: ['OV'], where: CMP },
  { name: 'SYNC', group: 'compare', aliases: [], where: CMP },
  { name: 'NEXT', group: 'compare', aliases: ['N'], where: CMP },
  { name: 'PREV', group: 'compare', aliases: ['PV'], where: CMP },
  { name: 'REVIEWED', group: 'compare', aliases: ['RV'], where: CMP },
  { name: 'EXCLUDE', group: 'compare', aliases: ['X'], where: CMP },
  { name: 'EXPORTCSV', group: 'compare', aliases: ['EXC'], where: CMP },
  { name: 'NUDGE', group: 'compare', aliases: [], where: CMP },
  { name: 'NUMBERING', group: 'compare', aliases: ['NUM'], where: CMP },
  // 투명 PNG
  { name: 'BGREMOVE', group: 'tools', aliases: [], where: PNG },
  { name: 'BGPICK', group: 'tools', aliases: [], where: PNG },
  { name: 'TOLERANCE', group: 'tools', aliases: [], where: PNG },
  { name: 'SMOOTH', group: 'tools', aliases: [], where: PNG },
  { name: 'CROP', group: 'tools', aliases: [], where: PNG },
  { name: 'PNGOUT', group: 'tools', aliases: [], where: PNG },
  // 2단계 (등록만, 흐리게)
  { name: 'DIST', group: 'phase2', aliases: ['DI'], where: DOC, phase: 2 },
  { name: 'CALIBRATE', group: 'phase2', aliases: [], where: DRAWING, phase: 2 },
  { name: 'PDFLAYER', group: 'phase2', aliases: [], where: DRAWING, phase: 2 },
];
