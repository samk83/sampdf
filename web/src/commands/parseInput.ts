/**
 * 명령창 입력 해석 (설계서 6-4). 좌표는 사람이 보는 실제 mm, 원점 왼쪽 아래, 위로 Y 증가 (5-2).
 * 페이지 pt로 바꾸는 일은 축척을 아는 도면 탭(U7a)이 한다.
 */
export type Point = { x: number; y: number };

export type PointInput =
  | { kind: 'absolute'; point: Point }
  | { kind: 'relative'; dx: number; dy: number }
  | { kind: 'polar'; distance: number; angleDeg: number }
  | { kind: 'distance'; distance: number }
  | { kind: 'osnap'; mode: OsnapMode };

export const OSNAP_MODES = ['END', 'MID', 'INT', 'CEN', 'QUA', 'PER', 'NEA'] as const;
export type OsnapMode = (typeof OSNAP_MODES)[number];

const NUM = String.raw`[-+]?(?:\d+\.?\d*|\.\d+)`;
const ABS = new RegExp(`^(${NUM})\\s*,\\s*(${NUM})$`);
const REL = new RegExp(`^@(${NUM})\\s*,\\s*(${NUM})$`);
const POLAR = new RegExp(`^@(${NUM})\\s*<\\s*(${NUM})$`);
const DIST = new RegExp(`^(${NUM})$`);

/** 점 입력 글자를 해석한다. 알아볼 수 없으면 undefined. */
export function parsePoint(text: string): PointInput | undefined {
  const s = text.trim().toUpperCase();
  let m: RegExpMatchArray | null;
  if ((m = s.match(REL))) return { kind: 'relative', dx: +m[1], dy: +m[2] };
  if ((m = s.match(POLAR))) return { kind: 'polar', distance: +m[1], angleDeg: +m[2] };
  if ((m = s.match(ABS))) return { kind: 'absolute', point: { x: +m[1], y: +m[2] } };
  if ((m = s.match(DIST))) return { kind: 'distance', distance: +m[1] };
  if ((OSNAP_MODES as readonly string[]).includes(s)) return { kind: 'osnap', mode: s as OsnapMode };
  return undefined;
}

/** 해석한 입력을 실제 점으로. last = 직전 점, cursorAngleDeg = 직전 점에서 커서 방향 (거리만 친 경우). */
export function resolvePoint(input: PointInput, last: Point | undefined, cursorAngleDeg = 0): Point | undefined {
  const polar = (d: number, a: number): Point | undefined => {
    if (!last) return undefined;
    const r = (a * Math.PI) / 180;
    return { x: last.x + d * Math.cos(r), y: last.y + d * Math.sin(r) };
  };
  switch (input.kind) {
    case 'absolute': return input.point;
    case 'relative': return last && { x: last.x + input.dx, y: last.y + input.dy };
    case 'polar': return polar(input.distance, input.angleDeg);
    case 'distance': return polar(input.distance, cursorAngleDeg);
    case 'osnap': return undefined;
  }
}

/**
 * 옵션 고르기: 대괄호 안 단어 중 입력과 같은 것, 또는 밑줄 글자(대문자로 된 앞부분)로 시작하는 것.
 * 예: options ['Undo','Close'], 'u' → 'Undo', 'CL' → 'Close'.
 */
export function matchOption(text: string, options: string[]): string | undefined {
  const s = text.trim().toUpperCase();
  if (!s) return undefined;
  return options.find((o) => o.toUpperCase() === s) ?? options.find((o) => o.toUpperCase().startsWith(s));
}

/** 숫자 입력. 쉼표 천 단위는 받지 않는다(좌표 구분과 헷갈림). */
export function parseNumber(text: string): number | undefined {
  const s = text.trim();
  return new RegExp(`^${NUM}$`).test(s) ? Number(s) : undefined;
}
