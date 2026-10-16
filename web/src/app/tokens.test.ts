import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { describe, expect, it } from 'vitest';
import { MARKUP_PALETTE } from '../doc/palette';

const SRC = join(__dirname, '..');
const TOKENS = join(SRC, '..', '..', 'design', 'tokens.css');

/** 색 값을 직접 적어도 되는 파일: PDF에 넣는 실제 색 (설계서 4-4). */
const ALLOWED = ['doc/palette.ts'];

/**
 * 옛 비교·PNG 화면 (밝은 디자인). U3·U4에서 새 틀로 옮기면 목록에서 지운다.
 * 이 목록에 새 파일을 더하지 않는다.
 */
const LEGACY = [
  'app/legacy.css',
  'compare/ChangeList.tsx', 'compare/ComparePage.tsx', 'compare/DropZone.tsx', 'compare/Marks.tsx',
  'compare/PageViewer.tsx', 'compare/SettingsMenu.tsx', 'compare/SplitPanes.tsx',
  'png/PngTool.tsx', 'ui/AppHeader.tsx', 'ui/Segmented.tsx',
];

const HARD_COLOR = /#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(/;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx|css)$/.test(name) && !/\.test\.ts$/.test(name) ? [relative(SRC, path).replace(/\\/g, '/')] : [];
  });
}

function tokenValues(): Record<string, string> {
  const css = readFileSync(TOKENS, 'utf8');
  return Object.fromEntries([...css.matchAll(/(--[\w-]+):\s*([^;]+);/g)].map((m) => [m[1], m[2].trim().toLowerCase()]));
}

describe('design tokens (설계서 4-4)', () => {
  it('코드에 색을 직접 쓰지 않는다 (토큰 변수만)', () => {
    const offenders = sourceFiles(SRC)
      .filter((f) => !ALLOWED.includes(f) && !LEGACY.includes(f))
      .flatMap((f) =>
        readFileSync(join(SRC, f), 'utf8')
          .split('\n')
          .map((line, i) => ({ f, i, line }))
          .filter(({ line }) => HARD_COLOR.test(line))
          .map(({ f, i, line }) => `${f}:${i + 1}: ${line.trim()}`),
      );
    expect(offenders).toEqual([]);
  });

  it('옛 화면 목록에는 실제로 있는 파일만 남긴다', () => {
    const all = sourceFiles(SRC);
    expect(LEGACY.filter((f) => !all.includes(f))).toEqual([]);
  });

  it('PDF 마크업 색(palette.ts)이 tokens.css와 같다', () => {
    const tokens = tokenValues();
    for (const [name, value] of Object.entries(MARKUP_PALETTE)) {
      expect(tokens[name], name).toBe(value.toLowerCase());
    }
  });
});
