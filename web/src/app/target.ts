/**
 * 어느 버전으로 빌드했는가 (설계서 11-4 P0). 같은 코드로 두 가지를 만든다.
 * pc = 내 PC 서버(127.0.0.1)와 함께 도는 버전 (기본) · web = 인터넷에 올릴 웹 버전 (W0에서 서버를 붙임).
 * `npm run build:web`이 `.env.web`의 VITE_TARGET=web을 읽는다.
 */
export type BuildTarget = 'pc' | 'web';

export const TARGET: BuildTarget = import.meta.env.VITE_TARGET === 'web' ? 'web' : 'pc';
