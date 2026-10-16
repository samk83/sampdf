# Sam Drawing Tools (sampdf)

Sam이 혼자 쓰는 도면 검토 프로그램. Bluebeam Revu처럼 PDF 도면을 보고, 원본과 리비전을 비교해 바뀐 곳에 구름·△ 번호·하이라이트를 자동으로 표시하고, 투명 PNG를 만든다. 내 PC에서만 돌아가는 웹앱(브라우저 화면 + C# 엔진).

## 먼저 읽을 것
- 기획: `docs/PLAN.md`
- 디자인 규칙: `design.md` (공통, 하네스 원본 그대로) · `design/brand.md`, `design/tokens.css` (이 프로젝트, 시안 확정 후 생김)
- 시안 진행 기록: `design/README.md` · 확정 화면: `design/screens/index.html` (브라우저로 열기)
- 다음 세션 시작 안내: `docs/NEXT-SESSION.md`
- 설계서: `docs/DESIGN-SPEC.md` (화면 구조·부품·데이터·명령·API·개발 순서 U1~U12)
- 기능 지도: `docs/FEATURES.md` · 결정 기록: `docs/DECISIONS.md` · 변경 이력: `docs/CHANGELOG.md`

## 진행 단계
- [x] 1. 기획서로 시작 (기획서 v0.2 + Drawing Difference Checker PRD, 이후 Owner 결정 반영 → `docs/PLAN.md`)
- [x] 2. 시안 (design-board) — 확정: A 배치 + 포커스 모드 + S1 Slate, 영어 기본. 기록 `design/README.md`, 결과 `design/brand.md`·`design/tokens.css`·`design/screens/`
- [x] 3. 설계 (확정 시안 기준 화면 구조·부품·데이터 연결, Owner 승인) — `docs/DESIGN-SPEC.md` 2026-10-16 승인
- [ ] 4. 개발 (작은 단위로 구현·테스트·커밋) — 진행 중: U1·U2 완료, 다음 U3 (`docs/DESIGN-SPEC.md` 9장)
- [ ] 5. 출시 전 점검 (ship-check)

참고: 엔진(PDF 처리·비교)과 첫 웹 화면은 시안 단계 전에 먼저 만들어져 있다. 시안 확정 후 웹 화면을 확정 디자인으로 다시 맞춘다.

## 작업 규칙
- Owner의 작업 원칙: 회의 → 비판적 검토 → 변경 계획 브리핑 → "코딩 진행할까요?" → 명확한 지시 후에만 코딩. 질문이 오면 멈추고 회의로 돌아간다.
- UI를 만들거나 고칠 때는 `design.md`와 `design/tokens.css`를 따른다. 색·간격을 하드코딩하지 않고 토큰을 쓴다. 아이콘은 Lucide.
- 기능을 추가·수정하면 `docs/FEATURES.md`를, 중요한 결정을 하면 `docs/DECISIONS.md`를, 작업 단위마다 `docs/CHANGELOG.md`를 갱신한다.
- 확인하지 못한 것은 확인했다고 말하지 않는다 (특히 Windows 실행).
- 필요한 도구·자료·로그인은 작업할 때 Owner에게 요청한다 (무엇이·왜·어떻게).

## 기술 선택
- 엔진: C# .NET 8, MuPDF.NET(PDF), OpenCvSharp(비교), ACadSharp(DWG, 웹에서는 안 씀)
- 웹: ASP.NET Core 서버(127.0.0.1:5180) + React·TypeScript·Tailwind·lucide-react (`web/` → 빌드 결과 `src/DrawingReview.Web/wwwroot`)
- 데스크톱(WPF, `src/DrawingReview.App`)은 유지만 하고 새 개발은 웹에서 한다.
- 실행 환경: Windows PC 브라우저. 개발·검증은 Linux 클라우드 세션 (Windows 확인은 Owner)

## 자주 쓰는 명령
- 실행(Windows): `run-web.cmd` 더블클릭 또는 `dotnet run --project src\DrawingReview.Web -c Release`
- .NET 테스트: `dotnet test`
- 웹 화면: `cd web && npm install && npm run build` (테스트: `npm test`)
