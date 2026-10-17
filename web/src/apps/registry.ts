import type { LucideIcon } from 'lucide-react';
import {
  ArrowUpDown, Cloud, Crop, Crosshair, DraftingCompass, Droplet, Eraser, Eye, EyeOff, FileArchive, FileImage, FilePen, GitCompare,
  ImagePlus, Images, Layers, ListChecks, Merge, PenLine, Ruler, ScanText, Signature, Split, Stamp,
} from 'lucide-react';
import type { MessageKey } from '../i18n';

/** 시작 화면 갈래 (설계서 11-1). */
export type AppCategory = 'drawing' | 'pdf' | 'image';
export const CATEGORIES: AppCategory[] = ['drawing', 'pdf', 'image'];

/** ready = 동작 · experimental = 동작하지만 검증 중 · soon = 자리만 (설계서 11-1). */
export type AppStatus = 'ready' | 'experimental' | 'soon';

/** document = 리본·명령창이 있는 문서 탭 · job = 리본 없는 작은 작업 탭 (설계서 11-3). */
export type AppScreen = 'document' | 'job';

/**
 * 계획 단계 (설계서 11-4 순서). 1 = 지금 있음, 2 = 페이지 도구, 3 = DWG/DXF 빠른 보기,
 * 4 = 도면 전문, 5 = 고급 PDF, 6 = 보안(Redaction).
 */
export type AppStage = 1 | 2 | 3 | 4 | 5 | 6;
export const STAGES: AppStage[] = [1, 2, 3, 4, 5, 6];

export type AppDef = {
  /** 폴더·글자 키 이름 (`app.<id>.name`, `app.<id>.desc`). */
  id: string;
  /** PRD 번호 (01~24). */
  number: number;
  category: AppCategory;
  icon: LucideIcon;
  status: AppStatus;
  screen: AppScreen;
  /** Ready 기능을 여는 명령 (명령 등록표에 있어야 함). */
  command?: string;
  stage: AppStage;
};

/**
 * 기능 등록표 (설계서 11-2). 시작 화면 카드와 검색이 이 표를 읽는다.
 * 새 기능 = 여기에 한 줄 + 그 기능 폴더. 기능이 동작하면 status와 command를 채운다.
 */
export const APPS: AppDef[] = [
  // Drawing & CAD
  { id: 'drawing-compare', number: 1, category: 'drawing', icon: GitCompare, status: 'ready', screen: 'document', command: 'CMP', stage: 1 },
  { id: 'pdf-overlay', number: 2, category: 'drawing', icon: Layers, status: 'soon', screen: 'document', stage: 4 },
  { id: 'pdf-markup', number: 3, category: 'drawing', icon: PenLine, status: 'soon', screen: 'document', stage: 4 },
  { id: 'revision-cloud', number: 4, category: 'drawing', icon: Cloud, status: 'soon', screen: 'document', stage: 4 },
  { id: 'measurement', number: 5, category: 'drawing', icon: Ruler, status: 'soon', screen: 'document', stage: 4 },
  { id: 'drawing-alignment', number: 6, category: 'drawing', icon: Crosshair, status: 'soon', screen: 'document', stage: 4 },
  { id: 'drawing-stamp', number: 7, category: 'drawing', icon: Stamp, status: 'soon', screen: 'document', stage: 4 },
  { id: 'dwg-preview', number: 8, category: 'drawing', icon: DraftingCompass, status: 'soon', screen: 'document', stage: 3 },
  // PDF Essentials
  { id: 'pdf-viewer', number: 9, category: 'pdf', icon: Eye, status: 'soon', screen: 'document', stage: 4 },
  { id: 'pdf-editor', number: 10, category: 'pdf', icon: FilePen, status: 'soon', screen: 'document', stage: 5 },
  { id: 'merge-pdf', number: 11, category: 'pdf', icon: Merge, status: 'soon', screen: 'job', stage: 2 },
  { id: 'split-pdf', number: 12, category: 'pdf', icon: Split, status: 'soon', screen: 'job', stage: 2 },
  { id: 'reorder-pages', number: 13, category: 'pdf', icon: ArrowUpDown, status: 'soon', screen: 'job', stage: 2 },
  { id: 'crop-rotate', number: 14, category: 'pdf', icon: Crop, status: 'soon', screen: 'job', stage: 2 },
  { id: 'compress-pdf', number: 15, category: 'pdf', icon: FileArchive, status: 'soon', screen: 'job', stage: 5 },
  { id: 'watermark', number: 16, category: 'pdf', icon: Droplet, status: 'soon', screen: 'job', stage: 2 },
  // Image & Conversion
  { id: 'remove-background', number: 17, category: 'image', icon: Eraser, status: 'ready', screen: 'job', command: 'PNG', stage: 1 },
  { id: 'pdf-to-image', number: 18, category: 'image', icon: FileImage, status: 'soon', screen: 'job', stage: 2 },
  { id: 'image-to-pdf', number: 19, category: 'image', icon: ImagePlus, status: 'soon', screen: 'job', stage: 2 },
  { id: 'ocr', number: 20, category: 'image', icon: ScanText, status: 'soon', screen: 'job', stage: 5 },
  { id: 'extract-images', number: 21, category: 'image', icon: Images, status: 'soon', screen: 'job', stage: 2 },
  { id: 'signature', number: 22, category: 'image', icon: Signature, status: 'soon', screen: 'job', stage: 5 },
  { id: 'redaction', number: 23, category: 'image', icon: EyeOff, status: 'soon', screen: 'job', stage: 6 },
  { id: 'batch-tools', number: 24, category: 'image', icon: ListChecks, status: 'soon', screen: 'job', stage: 5 },
];

export const appById = (id: string): AppDef | undefined => APPS.find((a) => a.id === id);

export const appNameKey = (app: AppDef) => `app.${app.id}.name` as MessageKey;
export const appDescKey = (app: AppDef) => `app.${app.id}.desc` as MessageKey;

/**
 * 검색 칸 거르기: 이름·설명(지금 언어와 영어 둘 다)·명령 이름에 검색어가 모두 들어 있는 기능.
 * 띄어쓰기로 나눈 낱말마다 확인하고, 대소문자는 가리지 않는다.
 */
export function searchApps(apps: AppDef[], query: string, texts: (app: AppDef) => string[]): AppDef[] {
  const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  if (words.length === 0) return apps;
  return apps.filter((app) => {
    const hay = [...texts(app), app.command ?? '', app.id].join(' ').toLowerCase();
    return words.every((w) => hay.includes(w));
  });
}
