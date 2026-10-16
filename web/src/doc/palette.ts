/**
 * PDF에 실제로 저장되는 마크업 색 (설계서 4-4). PDF에는 CSS 변수가 아니라 색 숫자가 들어가야 해서
 * 이 파일만 색 값을 직접 적는다. 값은 design/tokens.css의 같은 이름 토큰과 같아야 한다 (테스트로 확인).
 */
export const MARKUP_PALETTE = {
  '--markup-red': '#e5383b',
  '--markup-red-text': '#c92a2a',
  '--markup-yellow': '#ffd43b',
  '--markup-orange': '#ff922b',
  '--markup-blue': '#1c7ed6',
  '--overlay-original': '#e03131',
  '--overlay-revised': '#1c7ed6',
  '--snap-marker': '#f59f00',
  '--drawing-paper': '#ffffff',
  '--drawing-ink': '#1b1f24',
} as const;
