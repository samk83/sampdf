# {프로젝트 이름}

{한두 문장: 무엇을 하는 앱인지, 누가 쓰는지}

## 먼저 읽을 것
- 기획: `docs/PLAN.md`
- 디자인 규칙: `design.md` (공통) · `design/brand.md`, `design/tokens.css` (이 프로젝트, 시안 확정 후 생김)
- 기능 지도: `docs/FEATURES.md` · 결정 기록: `docs/DECISIONS.md`

## 진행 단계
- [x] 1. 기획서로 시작 (kickoff)
- [ ] 2. 시안 (design-board) — Owner가 `/design`으로 5안 → 3안 → 1안 확정. 기록은 `design/README.md`
- [ ] 3. 설계 (구조·데이터·화면 연결, Owner 승인)
- [ ] 4. 개발 (작은 단위로 구현·테스트·커밋)
- [ ] 5. 출시 전 점검 (ship-check)

## 작업 규칙
- UI를 만들거나 고칠 때는 `design.md`와 `design/tokens.css`를 따른다. 색·간격을 하드코딩하지 않고 토큰을 쓴다.
- 기능을 추가·수정하면 `docs/FEATURES.md`를, 중요한 결정을 하면 `docs/DECISIONS.md`를 함께 갱신한다.
- 필요한 도구·자료·로그인은 작업할 때 Owner에게 요청한다 (무엇이·왜·어떻게).

## 기술 선택
- {예: 웹 앱 — Vite + React + TypeScript}
- 실행 환경: {예: 브라우저 (PC·휴대폰)}

## 자주 쓰는 명령
- {설계·개발 단계에서 채움: 설치, 실행, 테스트, 빌드}
