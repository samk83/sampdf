// △ 리비전 번호 매기기: "하나씩 올라감"이면 1, 2, 3… (또는 A, B, C…), "같은 번호"면 모두 시작 번호.

export type NumberingMode = 'sequential' | 'same';

/** 시작 번호에서 k번째(0부터) 번호. 숫자면 더하고, 영문 글자면 A→B…Z→AA, 그 밖은 "시작-k+1". */
export function nthLabel(start: string, k: number): string {
  const s = start.trim() || '1';
  if (/^\d+$/.test(s)) return String(Number(s) + k);
  if (/^[A-Za-z]+$/.test(s)) {
    const upper = s === s.toUpperCase();
    let n = 0;
    for (const ch of s.toUpperCase()) n = n * 26 + (ch.charCodeAt(0) - 64);
    n += k;
    let out = '';
    while (n > 0) {
      const r = (n - 1) % 26;
      out = String.fromCharCode(65 + r) + out;
      n = Math.floor((n - 1) / 26);
    }
    return upper ? out : out.toLowerCase();
  }
  return k === 0 ? s : `${s}-${k + 1}`;
}

/** 순서대로 놓인 ID들에 번호를 붙인다. */
export function assignLabels(ids: string[], start: string, mode: NumberingMode): Record<string, string> {
  const out: Record<string, string> = {};
  ids.forEach((id, k) => (out[id] = mode === 'same' ? start.trim() || '1' : nthLabel(start, k)));
  return out;
}
