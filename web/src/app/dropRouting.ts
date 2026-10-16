/** 시작 화면에 파일을 끌어다 놓았을 때 어느 탭을 열지 정한다 (설계서 2-1). */
export type DropRoute =
  | { kind: 'compare'; files: File[] }
  | { kind: 'png'; files: File[] }
  | { kind: 'unsupported' };

const isPdf = (f: File) => f.type === 'application/pdf' || /\.pdf$/i.test(f.name);
const isImage = (f: File) => /^image\/(png|jpeg|tiff|bmp|webp)$/.test(f.type) || /\.(png|jpe?g|tiff?|bmp|webp)$/i.test(f.name);

/**
 * - 두 개 이상: 앞의 두 개로 비교 (첫 파일 = 원본)
 * - 그림 하나: 투명 PNG
 * - PDF 하나: 지금은 비교 탭의 원본으로 연다 (도면 검토 탭은 U5·U6에서 생김)
 */
export function routeDrop(dropped: File[]): DropRoute {
  const files = dropped.filter((f) => isPdf(f) || isImage(f));
  if (files.length >= 2) return { kind: 'compare', files: files.slice(0, 2) };
  if (files.length === 1) return isImage(files[0]) ? { kind: 'png', files } : { kind: 'compare', files };
  return { kind: 'unsupported' };
}

/** 비교 탭 이름: "Compare · S-101 rev0 ↔ rev1" 처럼 확장자를 뺀 파일 이름. */
export function compareTitle(base: string, files: File[]): string | undefined {
  const names = files.map((f) => f.name.replace(/\.[^.]+$/, ''));
  if (names.length === 2) return `${base} · ${names[0]} ↔ ${names[1]}`;
  if (names.length === 1) return `${base} · ${names[0]}`;
  return undefined;
}
