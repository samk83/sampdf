// C# 웹 서버(/api)와 주고받는 자료와 호출. 좌표는 모두 페이지 좌표(pt, 왼쪽 위 원점).

export type Rect = { x0: number; y0: number; x1: number; y1: number };
export type PageSize = { width: number; height: number };
export type FileInfo = { id: string; name: string; isImage: boolean; pages: PageSize[] };

export type Summary = { total: number; added: number; removed: number; modified: number; text: number; graphic: number };

export type Change = {
  id: string;
  number: number;
  kind: 'added' | 'removed' | 'modified';
  category: 'text' | 'graphic';
  confidence: 'high' | 'low';
  bounds: Rect;
  boundsInOld: Rect;
  highlights: Rect[];
  description: string;
};

export type PagePair = {
  index: number;
  oldPage: number | null;
  newPage: number | null;
  isAddedPage: boolean;
  isRemovedPage: boolean;
  alignment: string | null;
  summary: Summary;
  changes: Change[];
};

export type CompareResult = {
  id: string;
  originalId: string;
  revisionId: string;
  total: Summary;
  pages: PagePair[];
  warnings: string[];
};

async function fail(r: Response): Promise<never> {
  let message = `${r.status} ${r.statusText}`;
  try {
    const body = await r.json();
    if (body?.error) message = body.error;
  } catch {
    /* 본문이 JSON이 아니면 상태 코드만 */
  }
  throw new Error(message);
}

export async function uploadFile(file: File): Promise<FileInfo> {
  const form = new FormData();
  form.append('file', file);
  const r = await fetch('/api/files', { method: 'POST', body: form });
  if (!r.ok) return fail(r);
  return r.json();
}

export function closeFile(id: string): void {
  void fetch(`/api/files/${id}`, { method: 'DELETE' });
}

export const pageImageUrl = (fileId: string, page: number, scale: number) => `/api/files/${fileId}/pages/${page}.png?scale=${scale}`;

export const removedLayerUrl = (compareId: string, pairIndex: number, scale: number) =>
  `/api/compare/${compareId}/pages/${pairIndex}/removed.png?scale=${scale}`;

export async function runCompare(originalId: string, revisionId: string, ignoreTitleBlock: boolean): Promise<CompareResult> {
  const r = await fetch('/api/compare', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ originalId, revisionId, ignoreTitleBlock }),
  });
  if (!r.ok) return fail(r);
  return r.json();
}

/** 표시가 들어간 PDF를 받아 브라우저 내려받기로 저장한다. */
export async function exportPdf(compareId: string, revisionLabel: string, excluded: string[], fallbackName: string): Promise<void> {
  const r = await fetch(`/api/compare/${compareId}/export`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ revisionLabel, excluded }),
  });
  if (!r.ok) return fail(r);
  const blob = await r.blob();
  const disposition = r.headers.get('Content-Disposition') ?? '';
  const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition) ?? /filename="?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  saveBlob(blob, name);
}

export function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
