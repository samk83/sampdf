import type { LucideIcon } from 'lucide-react';
import {
  ArrowLeftRight, Bookmark, Check, ChevronLeft, ChevronRight, ChevronDown, ChevronUp, Circle, CircleDot, ClipboardPaste, Cloud,
  Columns2, Copy, Download, Eraser, FileDown, Files, FolderOpen, Fullscreen, GitCompare, Grid2x2, Hand, Hash, Highlighter, Image,
  Layers, Link2, List, ListTree, Maximize, MessageSquare, Moon, Move, MousePointer2, MoveUpRight, PenLine, Redo2, Ruler, Save,
  SaveAll, Scaling, Settings, SlidersHorizontal, Slash, Spline, Square, Table, Terminal, Triangle, Type, Undo2, ZoomIn, ZoomOut,
} from 'lucide-react';
import type { RibbonTabId } from '../app/store';

/** 리본 버튼 하나. 누르면 명령창에 command를 쳐 준 것과 같다 (설계서 6-1). */
export type RibbonButtonDef = {
  id: string;
  icon: LucideIcon;
  /** 부르는 명령 (명령창에 쳐 주는 글자). */
  command: string;
  /** 툴팁에 보여 줄 별칭·단축키 (예: "L", "Ctrl+S"). */
  hint?: string;
  /** 2단계 기능은 흐리게 + "Phase 2" 꼬리표 (설계서 D1). */
  phase?: 2;
};
export type RibbonGroupDef = { id: string; buttons: RibbonButtonDef[] };

const select: RibbonGroupDef = {
  id: 'select',
  buttons: [
    { id: 'select', icon: MousePointer2, command: 'SELECT' },
    { id: 'pan', icon: Hand, command: 'PAN', hint: 'P' },
  ],
};
const delta: RibbonButtonDef = { id: 'delta', icon: Triangle, command: 'DELTA' };

/** 리본 탭별 그룹·버튼 (설계서 2-4). */
export const RIBBON: Record<RibbonTabId, RibbonGroupDef[]> = {
  home: [
    { id: 'file', buttons: [
      { id: 'newCompare', icon: GitCompare, command: 'CMP' },
      { id: 'open', icon: FolderOpen, command: 'OPEN', hint: 'Ctrl+O' },
      { id: 'save', icon: Save, command: 'QSAVE', hint: 'Ctrl+S' },
      { id: 'saveAs', icon: SaveAll, command: 'SAVEAS', hint: 'Ctrl+Shift+S' },
      { id: 'exportPdf', icon: FileDown, command: 'EXPORT', hint: 'Ctrl+E' },
    ] },
    { id: 'edit', buttons: [
      { id: 'undo', icon: Undo2, command: 'U', hint: 'Ctrl+Z' },
      { id: 'redo', icon: Redo2, command: 'REDO', hint: 'Ctrl+Y' },
      { id: 'copy', icon: Copy, command: 'COPYCLIP' },
      { id: 'paste', icon: ClipboardPaste, command: 'PASTECLIP' },
      { id: 'erase', icon: Eraser, command: 'E', hint: 'Delete' },
    ] },
    { id: 'tools', buttons: [
      { id: 'transparentPng', icon: Image, command: 'PNG' },
      { id: 'settings', icon: Settings, command: 'OP' },
      { id: 'commandList', icon: ListTree, command: '?', hint: 'F1' },
    ] },
  ],
  compare: [
    { id: 'files', buttons: [
      { id: 'open', icon: FolderOpen, command: 'OPEN', hint: 'Ctrl+O' },
      { id: 'swap', icon: ArrowLeftRight, command: 'SWAP' },
    ] },
    { id: 'view', buttons: [
      { id: 'sideBySide', icon: Columns2, command: 'SBS' },
      { id: 'overlay', icon: Layers, command: 'OV' },
      { id: 'syncPan', icon: Link2, command: 'SYNC' },
    ] },
    { id: 'revisionMarks', buttons: [
      { id: 'cloud', icon: Cloud, command: 'RC' },
      { id: 'highlight', icon: Highlighter, command: 'HL' },
      delta,
    ] },
    { id: 'review', buttons: [
      { id: 'previous', icon: ChevronLeft, command: 'PREV' },
      { id: 'next', icon: ChevronRight, command: 'NEXT' },
      { id: 'reviewed', icon: Check, command: 'REVIEWED' },
    ] },
    { id: 'output', buttons: [
      { id: 'exportPdf', icon: Download, command: 'EXPORT', hint: 'Ctrl+E' },
      { id: 'listCsv', icon: Table, command: 'EXPORTCSV' },
    ] },
  ],
  markup: [
    select,
    { id: 'annotate', buttons: [
      { id: 'textBox', icon: Type, command: 'TB' },
      { id: 'callout', icon: MessageSquare, command: 'CA' },
    ] },
    { id: 'shapes', buttons: [
      { id: 'cloud', icon: Cloud, command: 'RC' },
      { id: 'rectangle', icon: Square, command: 'REC' },
      { id: 'ellipse', icon: Circle, command: 'EL' },
      { id: 'line', icon: Slash, command: 'L' },
      { id: 'arrow', icon: MoveUpRight, command: 'AW' },
      { id: 'pen', icon: PenLine, command: 'PEN' },
      { id: 'highlight', icon: Highlighter, command: 'HL' },
    ] },
    { id: 'revision', buttons: [delta] },
    { id: 'list', buttons: [
      { id: 'markups', icon: List, command: 'MARKUPS' },
      { id: 'status', icon: CircleDot, command: 'STATUS', hint: 'RS' },
    ] },
  ],
  draw: [
    select,
    { id: 'draw', buttons: [
      { id: 'line', icon: Slash, command: 'L' },
      { id: 'polyline', icon: Spline, command: 'PL' },
      { id: 'circle', icon: Circle, command: 'C' },
      { id: 'rectangle', icon: Square, command: 'REC' },
      { id: 'revCloud', icon: Cloud, command: 'RC' },
      { id: 'hatch', icon: Hash, command: 'H' },
      { id: 'text', icon: Type, command: 'T' },
    ] },
    { id: 'modify', buttons: [
      { id: 'move', icon: Move, command: 'M' },
      { id: 'copy', icon: Copy, command: 'CO' },
      { id: 'erase', icon: Eraser, command: 'E', hint: 'Delete' },
    ] },
    { id: 'revision', buttons: [delta] },
    { id: 'measure', buttons: [
      { id: 'distance', icon: Ruler, command: 'DIST', phase: 2 },
      { id: 'scale', icon: Scaling, command: 'CALIBRATE', phase: 2 },
    ] },
  ],
  view: [
    { id: 'navigate', buttons: [
      { id: 'fit', icon: Maximize, command: 'ZOOM E' },
      { id: 'zoomIn', icon: ZoomIn, command: 'ZOOM I' },
      { id: 'zoomOut', icon: ZoomOut, command: 'ZOOM O' },
      { id: 'previousPage', icon: ChevronUp, command: 'PAGE P', hint: 'PgUp' },
      { id: 'nextPage', icon: ChevronDown, command: 'PAGE N', hint: 'PgDn' },
    ] },
    { id: 'display', buttons: [
      { id: 'darkView', icon: Moon, command: 'DV' },
      { id: 'grid', icon: Grid2x2, command: 'GRID', hint: 'F7' },
      { id: 'focus', icon: Fullscreen, command: 'FOCUS', hint: 'F11' },
    ] },
    { id: 'panels', buttons: [
      { id: 'pages', icon: Files, command: 'PAGES' },
      { id: 'bookmarks', icon: Bookmark, command: 'BOOKMARKS' },
      { id: 'layers', icon: Layers, command: 'LAYER' },
      { id: 'properties', icon: SlidersHorizontal, command: 'PROPERTIES' },
      { id: 'commandLine', icon: Terminal, command: 'CMDLINE', hint: 'Ctrl+9' },
    ] },
  ],
  tools: [
    { id: 'tools', buttons: [
      { id: 'transparentPng', icon: Image, command: 'PNG' },
      { id: 'settings', icon: Settings, command: 'OP' },
      { id: 'commandList', icon: ListTree, command: '?', hint: 'F1' },
    ] },
  ],
};
