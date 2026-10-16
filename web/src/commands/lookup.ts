import type { CommandDef } from './registry';

/** 설정에서 바꾼 별칭 (명령 이름 → 별칭 목록). 없으면 기본 별칭 (U9에서 편집). */
export type AliasOverrides = Record<string, string[]>;

export const aliasesOf = (def: CommandDef, overrides: AliasOverrides = {}) => overrides[def.name] ?? def.aliases;

/** 이름이나 별칭이 정확히 같은 명령 (대소문자 무시). */
export function findCommand(commands: CommandDef[], word: string, overrides: AliasOverrides = {}): CommandDef | undefined {
  const w = word.trim().toUpperCase();
  if (!w) return undefined;
  return commands.find((c) => c.name === w) ?? commands.find((c) => aliasesOf(c, overrides).includes(w));
}

/**
 * 자동완성 (설계서 6-5): ① 이름·별칭이 정확히 같음 ② 그 글자로 시작 ③ 설명에 그 글자 포함. 최대 8개.
 * describe는 지금 언어의 설명을 돌려준다.
 */
/** 자동완성 한 줄. rank: 0 정확히 같음 · 1 이름이 그 글자로 시작 · 2 별칭이 시작 · 3 설명에 포함. */
export type Completion = { def: CommandDef; rank: number };

export function autocomplete(
  commands: CommandDef[], typed: string, describe: (def: CommandDef) => string, overrides: AliasOverrides = {}, limit = 8,
): Completion[] {
  const w = typed.trim().toUpperCase();
  if (!w || /\s/.test(w)) return [];
  const rank = (c: CommandDef): number => {
    const names = [c.name, ...aliasesOf(c, overrides)];
    if (names.includes(w)) return 0;
    if (c.name.startsWith(w)) return 1;
    if (names.some((n) => n.startsWith(w))) return 2;
    if (w.length >= 2 && describe(c).toUpperCase().includes(w)) return 3;
    return -1;
  };
  return commands
    .map((c, i) => ({ c, r: rank(c), i }))
    .filter((x) => x.r >= 0)
    .sort((a, b) => a.r - b.r || a.c.name.length - b.c.name.length || a.i - b.i)
    .slice(0, limit)
    .map((x) => ({ def: x.c, rank: x.r }));
}

/** 글자 차이 (넣기·빼기·바꾸기 횟수). */
export function editDistance(a: string, b: string): number {
  const d = Array.from({ length: a.length + 1 }, (_, i) => [i, ...Array(b.length).fill(0)]);
  for (let j = 1; j <= b.length; j++) d[0][j] = j;
  for (let i = 1; i <= a.length; i++)
    for (let j = 1; j <= b.length; j++)
      d[i][j] = Math.min(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
  return d[a.length][b.length];
}

/** 오타 제안 (설계서 6-5): 명령 이름 중 글자 차이 2개 이하에서 가장 가까운 것. 너무 짧은 입력은 제안하지 않음. */
export function suggestCommand(commands: CommandDef[], word: string): CommandDef | undefined {
  const w = word.trim().toUpperCase();
  if (w.length < 3) return undefined;
  let best: { c: CommandDef; d: number } | undefined;
  for (const c of commands) {
    const d = editDistance(w, c.name);
    if (d <= 2 && (!best || d < best.d)) best = { c, d };
  }
  return best?.c;
}
