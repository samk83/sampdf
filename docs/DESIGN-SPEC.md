# 설계서 (DESIGN-SPEC): Sam Drawing Tools 웹앱

> 3단계 "설계" 결과 문서. 확정 시안(`design/screens/`, `design/brand.md`, `design/tokens.css`)을 실제로 만들 수 있게
> 화면 구조·부품·데이터·명령·서버 연결·개발 순서를 정한다. Owner 승인 후 4단계 개발로 넘어간다.
>
> 상태: **초안 v0.2 (2026-10-16) — 1·2차 회의 범위(1~6장) 작성.** 7~10장은 3차 회의에서 채운다.
> 근거 브리핑: 프로젝트 파일 `design-phase/설계-브리핑.md`

## 결정 표시 읽는 법

| 표시 | 뜻 |
|---|---|
| ✅ 확정 | Owner가 이미 정한 것 (`docs/DECISIONS.md`에 있음) |
| 🟡 추천안 적용 (Owner 확인 대기) | Owner가 아직 답하지 않아 Claude 추천안으로 적어 둔 것. **바꾸고 싶으면 번호로 말하면 된다** (예: "D3은 숨김으로") |
| ⏳ 나중에 | 2단계 이후 |

## 목차

1. 범위 확정 (1차) — v0.1
2. 화면 구조 (1차) — v0.1
3. 파일 열기·저장 (1차) — v0.1
4. 부품 목록·토큰 연결 (2차) — v0.2
5. 상태·데이터 모델 (2차) — v0.2
6. 명령 시스템 (2차) — v0.2
7. 서버 API (3차)
8. 다국어 (3차)
9. 개발 순서와 단위별 완료 기준 (3차)
10. 위험 시험 계획 (3차)

---

## 1. 범위 확정

### 1-1. 이번(1단계)에 만드는 것

| 영역 | 내용 | 근거 화면 |
|---|---|---|
| 공통 틀 | 제목줄(문서 탭)·리본·좌우 패널·명령창·상태 표시줄, 포커스 모드(F11), 어두운 도면 보기(DV) | 전체, 09 |
| 시작 | 큰 카드 3개, 최근 파일, 최근 비교, 끌어다 놓기 | 01 |
| 도면 비교 | 지금 웹 기능 전부를 새 디자인으로 (대기·진행·결과·겹쳐 보기·변경 표·표시 고치기·내보내기) | 02~06, 11 |
| 도면 검토 | PDF 보기(페이지·썸네일·책갈피·확대), 기존 주석 보이기, 마크업 목록, Bluebeam식 마크업 | (07~09의 틀) |
| 작도 1단계 | 선·폴리선·원·사각형·리비전 구름·글자·이동·복사·지우기, 내 레이어, 직교·격자, 내가 그린 것에 오스냅, 해칭(경계 그리기·선택한 닫힌 도형, 무늬 5개) | 07, 08 |
| 명령창 | 모든 기능을 명령으로, 별칭, 좌표 입력, 자동완성, 오타 제안 | 04, 07, 08, 11 |
| 저장·내보내기 | 열고·저장하고·자동 저장, 편집 가능 / 녹여 넣기 내보내기 | 06, 3장 |
| 투명 PNG | 지금 기능을 새 디자인으로 | 12 |
| 설정 | 언어·단위·축척, 오스냅 종류, 명령 별칭, 작도, 비교 기본값 | 10 |
| 다국어 | 영어 기본 → 한국어 | 10 |

### 1-2. 시안과 1단계 범위가 다른 곳 (브리핑 2장 A~H 결정)

| 번호 | 시안에 보이는 것 | 결정 | 표시 |
|---|---|---|---|
| D1 | 07·08·10: PDF 도면 선에 오스냅, "Drawing layers (PDF)" 목록, 설정 "Snap to drawing lines from the PDF", 리본 Distance·Scale, 해칭 "Pick inside an area" | 1단계에서는 **자리는 그대로 두고 흐리게(비활성) + "Phase 2" 꼬리표**. 누르면 명령창에 `DIST is coming in phase 2.` 같은 안내. 오스냅은 내가 그린 것에만 동작 | 🟡 추천안 적용 |
| D2 | 01 최근 파일·마크업 수, 07 "Autosaved 1 min ago" | 3장 방식(서버가 내 PC 경로로 열고 저장)으로 가능하게 한다 | 🟡 추천안 적용 |
| D3 | 06 비교 내보내기의 "My markups" | 비교 탭에서도 그리기·마크업 가능. 단 **오른쪽(리비전) 칸에만**. 왼쪽(원본) 칸에서 그리기 명령을 쓰면 명령창이 `Draw on the revised side.` 안내 | 🟡 추천안 적용 |
| D4 | Draw 탭과 기획의 Markup 도구가 겹침, Home·Markup·View 리본 내용은 시안에 없음 | **Draw = CAD식 작도, Markup = Bluebeam식 표시**. 리비전 구름은 두 탭에 같은 명령(`REVCLOUD`)으로. 탭별 버튼은 2-4 | 🟡 추천안 적용 |
| D5 | 검토 화면에 마크업 목록 없음 (09에 "Markups 5"만) | 검토 탭의 아래 칸 = **마크업 목록**(비교 탭의 변경 표와 같은 표 부품). 기본은 접힘, `MARKUPS` 명령·리본 버튼으로 펼침. 포커스 모드에서는 "Markups 5 Tab" 버튼으로 떠 있는 목록 | 🟡 추천안 적용 |
| D6 | 상태 표시줄 mm 좌표, "Scale 1:100" | 1단계 축척 = **페이지마다 숫자로 입력**(기본값은 설정의 "Drawing scale"). 좌표·`@500<90` 입력은 이 축척을 쓴 실제 mm. 두 점 찍어 맞추기는 ⏳ 2단계 | 🟡 추천안 적용 |
| D7 | 제목줄 되돌리기 하나 | **문서 탭마다 따로** 되돌리기·다시 하기 | 🟡 추천안 적용 |
| D8 | 설정의 "日本語 Planned" | 언어 목록은 실제 있는 언어만 보여 준다 (English, 한국어) | 🟡 추천안 적용 |
| D9 | 10 설정의 "Import from AutoCAD .pgp" | ⏳ 나중에. 버튼은 흐리게 + "Later" | 🟡 추천안 적용 |
| D10 | 비교·검토 모두 오토캐드식 명령창, 단축키 오토캐드 방식 | 그대로 | ✅ 확정 |
| D11 | 그린 것은 PDF 주석, 녹여 넣기는 내보낼 때 선택 | 그대로 | ✅ 확정 |

### 1-3. 이번에 만들지 않는 것

| 항목 | 이유 |
|---|---|
| ⏳ 작도 2단계 (PDF 선 오스냅·안쪽 클릭 해칭·PDF 레이어 켜고 끄기·축척 맞추기·거리 재기) | DECISIONS 2026-10-16. 위험 시험(10장) 후 |
| ⏳ 비교 작업 저장·불러오기, OCR | PLAN 3장 "나중에" |
| ⏳ `.pgp` 별칭 가져오기, 일본어 | D8, D9 |
| ✖ Takeoff·협업·클라우드·DWG 웹·Word 비교·AI 배경 지우기·CAD 수준 편집 | PLAN 8장 범위 밖 |
| ✖ 데스크톱(WPF) 새 기능 | 유지만 한다 (CLAUDE.md) |

---

## 2. 화면 구조

### 2-1. 큰 틀: 문서 탭 = 작업 하나 🟡 추천안 적용

제목줄의 탭 하나가 **작업 하나**다. 탭은 4종류:

| 탭 종류 | 탭 이름 예 | 만드는 방법 | 처음 열리는 리본 탭 |
|---|---|---|---|
| Home | `Home` | 항상 맨 앞에 하나, 닫을 수 없음 | 리본 없음 (01) |
| Drawing (도면 검토) | `S-101_Structural_Plan_rev1.pdf ●` | Open, 최근 파일, PDF 하나 끌어다 놓기, `OPEN` | Markup |
| Compare (도면 비교) | `Compare · S-101 rev0 ↔ rev1` | Compare 카드, PDF 두 개 끌어다 놓기, `CMP` | Compare |
| PNG (투명 PNG) | `Transparent PNG · stamp_scan.jpg` | PNG 카드, 그림 하나 끌어다 놓기, `PNG` | Tools |

- `●` = 저장 안 된 변경 있음. 닫을 때 저장 여부를 묻는다.
- 같은 파일을 두 번 열면 새 탭 대신 이미 열린 탭으로 간다.
- 탭 전환은 즉시(애니메이션 없음, brand.md). 탭을 바꿔도 각 탭의 확대·선택·명령 기록은 그대로 남는다.
- 리본 탭 6개(Home·Compare·Markup·Draw·View·Tools)는 **어느 문서 탭에서나 보이고**, 그 탭에서 쓸 수 없는 버튼은 흐리게 (예: Drawing 탭에서 Compare 리본의 Swap).
- 포커스 모드(F11)는 탭이 아니라 **지금 탭의 보기 상태**. Drawing·Compare 탭에서만 켜진다 (Home·PNG에서는 F11이 아무것도 안 함 + 명령창 안내).

### 2-2. 영역 배치 (brand.md 화면 뼈대 그대로)

```
┌ 제목줄 32px ── 앱 이름 · 문서 탭들 ·········· ↶ ↷ · 저장 상태 · Focus F11 ┐
├ 리본 탭 30px ── Home Compare Markup Draw View Tools                       ┤
├ 리본 78px ──── 그룹(아이콘+글자) … | 오른쪽 작은 설정 칸                   ┤
├ 왼쪽 패널 212 ┬ 도면 영역 (가운데) ─────────────────┬ 오른쪽 패널 256 ┤
│ Pages        │                                     │ Properties     │
│ Bookmarks    │                                     │ (선택에 따라)    │
│ Layers       ├ 아래 칸: 변경 표 / 마크업 목록 (접힘) ┤                │
│              ├ 명령창 (기록 2~3줄 + 입력 32px) ──────┤                │
├ 상태 표시줄 26px ── 명령·오스냅 · GRID SNAP ORTHO OSNAP DARK VIEW · 좌표 · 축척 · 페이지 · 언어 ┤
```

| 영역 | 탭 종류별 내용 |
|---|---|
| 왼쪽 패널 | Drawing: Pages·Bookmarks·Layers / Compare: Pages(변경 개수 배지)·Bookmarks·Layers(내 레이어만) / PNG: 없음 / Home: 왼쪽 메뉴(01) |
| 오른쪽 패널 | 선택한 것의 속성. 아무것도 안 골랐으면 지금 명령·도구의 기본값. Compare 대기 중에는 "Compare settings"(02), 겹쳐 보기에서는 "Overlay"(05), PNG는 "Transparent PNG"(12) |
| 아래 칸 | Compare: 변경 표(04) / Drawing: 마크업 목록(D5) / PNG·Home: 없음 |
| 명령창 위치 | Compare·Home·PNG: 아래에 붙음 / Drawing: 도면 위에 떠 있음(`--color-cmdline-float`, 07) / 포커스 모드: 떠 있음(09) |
| 좌우 패널 접기 | 패널 머리를 두 번 누르거나 `PANELS` 명령. 1280px 화면에서는 오른쪽 패널이 기본 접힘 |

### 2-3. 탭 종류별 화면 상태

| 탭 | 상태 | 화면 |
|---|---|---|
| Home | 기본 | 01 |
| Compare | 파일 기다림 → 비교 중 → 결과(나란히 / 겹쳐 보기) → 내보내기 창 / 오류 / 차이 없음 | 02 → 03 → 04 / 05 → 06 / 11 |
| Drawing | 여는 중 → 보기 → 명령 실행 중(그리기) → 저장 중·저장됨 / 오류(암호·깨진 파일) | 07, 08, 09 |
| PNG | 그림 기다림 → 결과 | 12 |
| 공통 | 설정 창 (어느 탭에서나 `OPTIONS`/`OP`) | 10 |

"차이 없음" 상태는 시안에 없다 → 개발 때 `EmptyState` 부품으로 만들고 캡처해 Owner 확인.

### 2-4. 리본 탭별 버튼 🟡 추천안 적용 (시안에 없던 Home·Markup·View는 새로 정함)

괄호 안은 그 버튼이 부르는 명령. 리본 버튼은 **명령창에 그 명령을 쳐 주는 것과 똑같이** 동작한다 (6장).

| 리본 탭 | 그룹 → 버튼 | 근거 |
|---|---|---|
| **Home** | File: New compare(`CMP`) · Open(`OPEN`) · Save(`QSAVE`) · Save as(`SAVEAS`) · Export PDF(`EXPORT`) / Edit: Undo(`U`) · Redo(`REDO`) · Copy(`COPYCLIP`) · Paste(`PASTECLIP`) · Erase(`E`) / Tools: Transparent PNG(`PNG`) · Settings(`OP`) | 새로 정함 |
| **Compare** | Files: Open · Swap(`SWAP`) / View: Side by side(`SBS`) · Overlay(`OV`) · Sync pan(`SYNC`) / Revision marks: Cloud · Highlight · Delta / Review: Previous(`PREV`) · Next(`NEXT`) · Reviewed(`REVIEWED`) / Output: Export PDF · List CSV(`EXPORTCSV`) / 오른쪽 칸: △ numbering(Start·Each +1), Detect(Text→highlight, Ignore title block) | 04 그대로 |
| **Markup** | Select: Select · Pan / Annotate: Text box(`TB`) · Callout(`CA`) · Note(`NOTE`) / Shapes: Cloud(`RC`) · Rectangle · Ellipse(`EL`) · Line · Arrow(`AR`) · Pen(`PEN`) · Highlight(`HL`) / Revision: Delta(`DELTA`) / List: Markups(`MARKUPS`) · Status(`STATUS`) / 오른쪽 칸: Layer · Color · Line width | 새로 정함 (PLAN 기능 4) |
| **Draw** | Select: Select · Pan / Draw: Line(`L`) · Polyline(`PL`) · Circle(`C`) · Rectangle(`REC`) · Rev cloud(`RC`) · Hatch(`H`) · Text(`T`) / Modify: Move(`M`) · Copy(`CO`) · Erase(`E`) / Revision: Delta / Measure: Distance · Scale (D1: 흐리게, Phase 2) / 오른쪽 칸: Layer · Style(ByLayer·선 굵기·투명도) | 07 그대로 |
| **View** | Navigate: Fit(`ZOOM E`) · Zoom in · Zoom out · Previous page · Next page / Display: Dark view(`DV`) · Grid(`GRID`) · Focus(`FOCUS`, F11) / Panels: Pages · Bookmarks · Layers · Properties · Command line(Ctrl+9) | 새로 정함 |
| **Tools** | PNG 탭일 때: Image · Background · Edges · Output (12 그대로). 다른 탭일 때: Transparent PNG · Settings · Command list(`?`) | 12 + 새로 정함 |

> Markup 탭의 Text box·Callout은 Draw 탭의 Text(`T`, 한 줄 글자)와 다르다: 글상자·지시선이 있는 Bluebeam식 메모. 둘 다 PDF FreeText로 저장된다.

### 2-5. 키보드 규칙 (오토캐드 방식, ✅ 확정 + 세부 🟡)

| 키 | 동작 |
|---|---|
| 글자 키 | 명령창에 입력 (단, 패널의 입력칸에 커서가 있을 때는 그 칸에) 🟡 |
| Enter / Space | 실행. 빈 입력이면 직전 명령 반복 |
| Esc | 지금 명령 취소, 한 번 더 누르면 선택 해제 |
| F2 | 명령 기록 펼치기 |
| F3 / F7 / F8 / F9 | OSNAP / GRID / ORTHO / SNAP 켜고 끄기 (오토캐드와 같음) 🟡 |
| F11 | 포커스 모드 |
| Ctrl+Z / Ctrl+Y | 되돌리기 / 다시 하기 (지금 탭만, D7) |
| Ctrl+S / Ctrl+O / Ctrl+E | 저장 / 열기 / 내보내기 |
| Ctrl+Tab | 다음 문서 탭 |
| Shift (그리는 중) | 직교 잠깐 켜고 끄기 (10 설정) |
| 휠 / 가운데 버튼 끌기 | 확대·축소 / 이동 (오토캐드와 같음) 🟡 |

---

## 3. 파일 열기·저장 🟡 추천안 적용 (브리핑 3-5 ①)

### 3-1. 원칙

1. **서버가 내 PC의 파일 경로로 직접 읽고 쓴다.** 서버는 지금처럼 `127.0.0.1`(내 PC 안에서만 닿는 주소)에서만 요청을 받는다.
2. **원본은 바꾸지 않는 것이 기본.**
   - Compare: 원본·리비전 파일은 절대 바꾸지 않는다. 결과는 항상 "내보내기"로 새 파일.
   - Drawing: Ctrl+S는 그 파일에 저장(Bluebeam과 같음). 대신 **그 파일을 처음 덮어쓸 때 한 번 `.bak` 사본**을 남긴다 🟡.
3. 저장은 지금 엔진의 **안전 저장**(`SafeFileSaver`: 임시 파일에 쓰고 → 검사하고 → 바꿔 치기)을 쓴다. 저장 중 꺼져도 원본이 깨지지 않는다.
4. 저장 직전에 파일이 밖에서 바뀌었는지(수정 시각) 확인하고, 바뀌었으면 묻는다.

### 3-2. 여는 방법

| 방법 | 동작 | 저장할 때 |
|---|---|---|
| Open 버튼·`OPEN`·Ctrl+O | **앱 전용 파일 열기 창**: 서버가 보여 주는 내 PC 폴더(최근 폴더·바탕 화면·문서·드라이브)에서 고름 | 같은 경로에 저장 |
| 시작 화면 최근 파일 | 기억해 둔 경로로 바로 열기. 파일이 없어졌으면 목록에서 흐리게 + "Remove" | 같은 경로 |
| 끌어다 놓기 | 지금처럼 파일 내용을 서버에 올림 (브라우저가 경로를 알려 주지 않음) | 처음 저장 때 **저장 위치를 묻는다** (Save as와 같음) |

> 앱 전용 파일 열기 창은 시안에 없다 → 2차 회의 전에 간단한 화면 1장을 시안 스크립트(`design/source/`)로 추가해 Owner 확인. 🟡

### 3-3. 저장 종류

| 종류 | 명령 | 결과 |
|---|---|---|
| 저장 | `QSAVE`, Ctrl+S | Drawing 탭: 주석을 넣어 같은 파일에 저장 (편집 가능한 마크업) |
| 다른 이름으로 저장 | `SAVEAS` | 새 경로에 저장, 탭이 새 파일을 가리킴 |
| 내보내기 | `EXPORT`, Ctrl+E | 06 화면. 편집 가능 / 녹여 넣기, 페이지 범위. **새 파일** (기본 위치: 원본 폴더, 이름 `…_marked.pdf`) 🟡 |
| 자동 저장 | (자동, 기본 2분) 🟡 | 원본은 건드리지 않고 **복구 사본**을 앱 데이터 폴더에. 정상 저장·닫기 후 지움. 비정상 종료 후 다시 열면 "복구할까요?" |
| PNG 내려받기 | 12 화면 | 지금처럼 브라우저 내려받기 (작은 그림이라 충분) |

### 3-4. 앱이 따로 보관하는 것 (앱 데이터 폴더)

위치: Windows `%APPDATA%\SamDrawingTools\` 🟡 (Linux 개발 때는 `~/.config/SamDrawingTools/`)

| 파일 | 내용 |
|---|---|
| `settings.json` | 언어·단위·축척 기본값, 오스냅 종류, 명령 별칭, 비교 기본값, 작도 설정 |
| `recent.json` | 최근 파일·최근 비교 (경로, 마지막 연 시각, 마크업 수·변경 수). 최근 20개 🟡 |
| `thumbs\` | 최근 파일 썸네일 그림 |
| `recovery\` | 자동 저장 복구 사본 |

### 3-5. 안전 규칙

- 서버의 파일 열기·저장 API는 **`127.0.0.1`에서 온 요청만** 받는다 (지금 `Program.cs`가 이 주소에만 열림 — 바꾸지 않는다).
- 다른 웹사이트가 내 PC 서버를 부르지 못하게 **요청 출처 검사**(같은 주소에서 온 화면만 허용)를 추가한다 🟡.
- 서버는 PDF·그림 확장자만 열고, 저장은 `.pdf`·`.bak`·앱 데이터 폴더에만 쓴다.

---

## 4. 부품 목록·토큰 연결

### 4-1. 폴더 구조 (`web/src`) 🟡 추천안 적용

기능별로 폴더를 나눈다. 한 폴더 = 한 가지 일.

| 폴더 | 하는 일 | 지금 있는 파일에서 오는 것 |
|---|---|---|
| `app/` | 앱 시작, 문서 탭 관리, 키보드 분배(2-5), 공용 상태 보관소 | `App.tsx`, `main.tsx` |
| `shell/` | 공통 틀: `AppShell`, `TitleBar`, `DocTabs`, `Ribbon`, `SidePanel`, `StatusBar`, `CommandLine`, `FocusOverlay` | `ui/AppHeader.tsx` → 대체 |
| `ui/` | 기본 부품 (4-2) | `ui/Segmented.tsx` 살림 |
| `commands/` | 명령 등록표, 입력 해석(좌표·옵션), 자동완성, 오타 제안, 명령별 대본 (6장) | 새로 |
| `viewer/` | 도면 한 페이지 보기(확대·이동·해상도 단계), 보기 계산 | `compare/PageViewer.tsx`, `compare/view.ts` |
| `doc/` | 마크업·작도 데이터(5장): 도형 종류, 레이어, 되돌리기, 클릭 판정, 오스냅, SVG 그리기, 구름·△ 계산 | `compare/geometry.ts`, `compare/numbering.ts`, `compare/Marks.tsx`의 그리기 부분 |
| `drawing/` | 도면 검토 탭: 페이지·책갈피·레이어 패널, 마크업 목록, 속성 패널 | 새로 |
| `compare/` | 비교 탭: 대기·진행·결과·겹쳐 보기, 변경 표, 비교 설정 | `compare/*` 나머지 (`ComparePage.tsx`는 상태를 `app/`로 옮기며 나눔) |
| `png/` | 투명 PNG 탭 | `png/*` 그대로 (겉모습만) |
| `i18n/` | 화면 글자 파일 `en.ts`·`ko.ts`, `t()` 함수 (8장) | 새로 |
| `api/` | 서버 호출 (7장) | `api.ts` |

### 4-2. 기본 부품 (`ui/`)

design.md 6장 목록을 이 앱에 맞게 고른 것. 모든 부품은 Hover·Focus·Pressed·Disabled 상태를 가지며 색·간격은 토큰만 쓴다.

| 부품 | 쓰는 곳 (화면 번호) | 메모 |
|---|---|---|
| `Button` (primary·default·ghost) | 02 Compare, 06 Export, 10 Save | primary는 `--color-primary`, 높이 `--size-control` |
| `IconButton` | 제목줄 ↶↷, 패널 머리, 포커스 도구 막대 | Lucide 16px, 툴팁 필수 |
| `RibbonButton` | 리본 | Lucide 20px(선 1.7) + 글자, 눌림 상태(켜짐/꺼짐), "Phase 2" 꼬리표(D1) |
| `Toggle` | 오른쪽 패널, 설정 | 02·04의 스위치 |
| `StatusToggle` | 상태 표시줄 GRID·SNAP·ORTHO·OSNAP·DARK VIEW | 고정폭 글꼴 |
| `Select` | Status, Color, Layer, Numbering | 색 견본 표시 가능 |
| `TextInput` / `NumberInput` | 속성(Start X, Length), 파일 이름 | 숫자칸은 단위 표시, ↑↓로 증감 |
| `Slider` | 투명도, Tolerance, Snap distance | 값 숫자 같이 표시 |
| `Checkbox` / `Radio` | 06, 10 | |
| `ChoiceCard` | 06 편집 가능/녹여 넣기, 08 무늬 | 큰 선택 칸 |
| `Dialog` | 06 Export, 10 Settings, 저장 확인, 파일 열기 창 | `--shadow-lg`, Esc 닫기, 포커스 가둠 |
| `ProgressDialog` | 03 비교 중 | 단계 목록 + 취소 |
| `Toast` | 11 "Page 2 looks scanned", 저장됨 | 오른쪽 위, "Don't show again" |
| `Table` | 04 변경 표, 마크업 목록 | 체크·정렬·필터·선택 동기화, 접기 |
| `Tabs` | 왼쪽 패널 Pages·Bookmarks·Layers | |
| `Badge` | "4 changes", "Reviewed", "CURRENT" | 상태색 |
| `EmptyState` / `ErrorState` | 02 빈 칸, 11 오류, 차이 없음 | 원인 + 해결 버튼 (design.md 5장 9번) |
| `Tooltip` | 모든 아이콘 버튼 | 명령 이름·단축키 함께 (예: "Line · L") |
| `ContextBar` | 04 구름 위 미니 도구 막대, 09 선택 말풍선 | 도면 위에 떠 있음 |

### 4-3. 기능 부품

| 부품 | 폴더 | 쓰는 곳 |
|---|---|---|
| `PageViewer` | `viewer/` | 모든 도면 칸 (살림, 겉모습·그리기 층 연결만 바꿈) |
| `MarkupLayer` | `doc/` | 도면 위 SVG: 그린 것·선택 손잡이·그리는 중 고무줄 선·오스냅 표시 |
| `RevisionMarks` | `compare/` | 비교 결과의 하이라이트·구름·△ (살림, 5장 도형 모델 위로 옮김) |
| `PageThumbnails`, `BookmarkTree`, `LayerList` | `drawing/` | 왼쪽 패널 |
| `PropertiesPanel` | `drawing/` | 오른쪽 패널. 선택한 것의 종류마다 칸 묶음이 다름 (07 Line, 08 Hatch, 04 Change) |
| `MarkupList` | `drawing/` | 아래 칸 (D5), `Table` 사용 |
| `ChangeTable` | `compare/` | 아래 칸 (04), `Table` 사용, 지금 `ChangeList.tsx` 대체 |
| `Autocomplete` | `shell/` | 명령창 위 4칸 목록 (04) |
| `FileBrowser` | `shell/` | 앱 전용 파일 열기 창 (3-2) |

### 4-4. 디자인 토큰 연결 🟡 추천안 적용 (브리핑 3-7 ①)

1. **토큰 파일은 하나만**: `design/tokens.css`를 웹 코드가 **직접 불러온다** (복사하지 않음). 토큰을 고치면 시안 화면과 앱이 같이 바뀐다.
2. **Tailwind 이름 → 토큰 변수**로 연결 (`tailwind.config.js`):

| Tailwind에서 쓰는 이름 | 연결되는 토큰 |
|---|---|
| `bg-background`, `bg-surface`, `bg-chrome`, `bg-canvas`, `bg-sunken`, `bg-pressed` | `--color-background` … `--color-surface-pressed` |
| `bg-primary`, `text-on-primary`, `text-accent`, `bg-accent-subtle` | `--color-primary` … |
| `text-fg`, `text-fg-secondary`, `text-fg-muted`, `text-fg-faint` | `--color-text` … `--color-text-faint` |
| `text-success`, `text-warning`, `text-danger` | 상태색 |
| `border-default`, `border-subtle`, `border-strong` | 테두리 색 |
| `p-1`~`p-8`, `gap-…` | `--space-1`~`--space-8` |
| `text-2xs`~`text-display` | `--font-size-*` |
| `rounded-sm/md/lg` | `--radius-*` |
| `shadow-sm/md/lg/sheet` | `--shadow-*` |
| `h-titlebar`, `w-panel-left` … | `--size-*` |
| `duration-fast/normal`, `ease-standard` | `--motion-*` |
| `z-floating`, `z-dropdown`, `z-modal` … | `--z-*` |

3. 옛 값(`tailwind.config.js`의 밝은 색·IBM Plex, `index.css`의 `#16181d` 등)은 **모두 지운다**.
4. **글꼴은 앱 안에 넣는다**: 시안은 인터넷(CDN)에서 글꼴을 받지만, 이 앱은 인터넷 없이도 돌아야 한다(PLAN 5장). Pretendard·JetBrains Mono 글꼴 파일을 npm 패키지로 받아 함께 빌드한다 (둘 다 무료 OFL 라이선스 → `docs/THIRD_PARTY.md`에 기록). 🟡
5. **자동 검사(테스트) 2개**:
   - 코드(`.ts`·`.tsx`·`.css`)에 `#색코드`나 `rgb(…)`를 직접 쓰면 실패. 예외는 `doc/palette.ts` 하나뿐.
   - `doc/palette.ts`(PDF에 저장할 실제 마크업 색: 빨강·노랑·주황·파랑 등)의 값이 `tokens.css`의 `--markup-*` 값과 같은지 비교. PDF에는 변수가 아닌 실제 색 숫자가 들어가야 해서 이 파일이 필요하다.

---

## 5. 상태·데이터 모델

### 5-1. 상태 보관소 🟡 추천안 적용: Zustand

"상태"는 지금 화면이 기억하는 값이다. 명령창·리본·패널·되돌리기가 같은 값을 함께 보고 바꾸므로, 앱 전체가 함께 쓰는 **공용 보관소 하나**에 둔다 (Zustand = 이 일을 하는 작은 무료 라이브러리).

| 보관 칸 | 내용 |
|---|---|
| 앱 전체 | 열린 탭 목록·지금 탭, 설정(언어·별칭·오스냅 종류·축척 기본값…), 포커스 모드, 상태 표시줄 토글(GRID·SNAP·ORTHO·OSNAP·DV), 명령창 기록 |
| Drawing 탭마다 | 파일(경로·서버 문서 ID·페이지 크기), 지금 페이지, 페이지별 보기(확대·위치), **도형 목록**, 레이어 표, 선택, 되돌리기 기록, 저장 안 됨 표시, 페이지별 축척 |
| Compare 탭마다 | 지금 `ComparePage.tsx`가 가진 값 그대로 (원본·리비전 파일, 비교 결과, 표시 고친 것, 제외 목록, 필터, 보기 방식…) + 리비전 칸의 도형 목록(D3) + 되돌리기 기록 |
| PNG 탭마다 | 지금 `PngTool.tsx`가 가진 값 |

규칙: 화면 부품은 보관소의 값을 **읽기만** 하고, 값을 바꾸는 것은 **명령**(6장)을 통해서만 한다. 그래야 되돌리기·명령 기록·리본·단축키가 한 길로 모인다. (확대·이동·패널 크기처럼 되돌릴 필요 없는 보기 값은 예외)

### 5-2. 좌표 ✅ 엔진과 같은 규칙 + 🟡 화면 표시 방식

| 구분 | 규칙 |
|---|---|
| 저장·계산 | 모든 도형은 **페이지 좌표(pt, 1pt = 1/72인치)**, 원점은 보이는 페이지 왼쪽 위, 아래로 갈수록 Y 증가. 엔진(`PageGeometry.cs`)·지금 비교 표시와 같음 |
| 화면 | `View`(확대율·위치)로 바꿔서 그림. 지금 `view.ts` 그대로 |
| 사람에게 보이는 좌표 (상태 표시줄·속성·입력) | **실제 mm = 종이 mm × 축척**. 종이 mm = pt × 25.4 ÷ 72. 원점은 **페이지 왼쪽 아래, 위로 갈수록 Y 증가** (CAD 습관과 같게) 🟡 |
| 각도 | 0° = 오른쪽, 반시계 방향이 + (오토캐드와 같음). `@500<90` = 위로 500mm |
| 축척 | 페이지마다 하나 (D6). 기본값은 설정의 Drawing scale. PDF에는 문서 단위 앱 전용 값으로 저장 (`/DRScale`) |

예: 축척 1:100, A1 도면에서 `@2000<0` → 오른쪽으로 종이 20mm = 약 56.7pt.

### 5-3. 도형(마크업) 데이터 모양

화면이 기억하는 도형 한 개 = **공통 칸 + 종류별 모양**.

| 공통 칸 | 뜻 |
|---|---|
| `id` | 고유 이름. PDF 주석의 `/NM`과 같은 값 (엔진이 이미 문서 전체에서 고유하게 만듦) |
| `page` | 페이지 번호 |
| `kind` | 종류 (아래 표) |
| `layer` | 레이어 이름 (5-4) |
| `style` | 선 색·채움 색·선 굵기(mm)·투명도·글자 크기. 색은 `ByLayer`(레이어 색 따라감) 가능 |
| `meta` | 작성자, 만든·고친 시각, 메모, 검토 상태(Open·In Progress·Resolved·Reopened), 잠금 |
| `readOnly` | 이 앱이 고칠 수 없는 주석(스탬프·링크 등) = 보이기만 함 |

| `kind` | 모양 데이터 | PDF 저장 형식 | 엔진에 지금 있나 |
|---|---|---|---|
| `line` | 시작점·끝점 | Line | ✅ |
| `arrow` | 시작점·끝점 | Line + 화살촉 | ✅ |
| `polyline` | 점 목록, 닫힘 여부 | 열림: **PolyLine** / 닫힘: Polygon | ❌ PolyLine 추가 필요 |
| `rect` | 사각형 | Square | ✅ |
| `circle` | 중심·반지름 | Circle (사각형으로 바꿔 저장) | ✅ (Ellipse로) |
| `ellipse` | 사각형 | Circle | ✅ |
| `cloud` | 점 목록(닫힘), 볼록함 | Polygon + 구름 테두리 | ✅ |
| `text` | 위치·글자·높이·각도 | FreeText (테두리 없음) | ✅ (테두리 없는 옵션 확인) |
| `textbox` | 사각형·글자 | FreeText | ✅ |
| `callout` | 글 사각형·지시선 끝·꺾임점 | FreeText Callout | ✅ |
| `ink` | 획 목록 | Ink | ✅ |
| `highlight` | 사각형 | Square + 곱하기 혼합 | ✅ |
| `delta` | 위치·번호 | 삼각형 + 글자 (지금 `RevisionMarks`와 같은 방식) | ✅ |
| `hatch` | 경계 점 목록(여러 개 가능), 무늬·축척·각도 | 채운 Polygon + 무늬 겉모습 | ❌ 추가 필요, **위험 R2** (10장) |
| `other` | 테두리 사각형만 | 원래 주석 그대로 보존 | ✅ (`MarkupKind.Other`) |

### 5-4. 레이어 🟡 추천안 적용

| 항목 | 규칙 |
|---|---|
| 레이어 표 | 이름·색·선 굵기·켜짐·잠금. 기본 레이어 `0`, `REVIEW`, `HATCH`, `NOTES`, `REV-CLOUDS`(비교 구름이 자동으로 들어감, 07 "auto") |
| 도형 → 레이어 | 각 주석에 앱 전용 꼬리표 `/DRLayer` |
| 레이어 표 저장 | PDF 문서 정보에 앱 전용 값 `/DRLayers` (글자로 적은 표) |
| 끄기·잠금 | **앱 화면에서만** 적용. 저장한 PDF에서 주석이 숨겨지지는 않는다. 내보내기 창(06)에서 레이어를 골라 뺄 수 있다 |
| ByLayer 색 | 저장할 때 실제 색으로 바꿔 넣고, `/DRByLayer` 표시를 남겨 다시 열면 ByLayer로 돌아온다 (다른 프로그램은 실제 색만 봄) |
| PDF 진짜 레이어(OCG) | ⏳ 2단계 위험 시험 후 |

### 5-5. 화면에 그리는 방식 🟡 추천안 적용: SVG

- 도면 자체: 지금처럼 서버가 그린 그림(PNG). **이때 이 앱이 고칠 수 있는 주석은 빼고** 그리게 한다 (엔진 `RenderPage(includeMarkups:false)`가 이미 있음). 그렇지 않으면 같은 구름이 두 번 보인다.
  - 단, 이 앱이 못 고치는 주석(스탬프 등)은 그림에 남아야 한다 → 엔진에 "고칠 수 있는 주석만 빼고 그리기" 옵션 추가 필요.
- 그린 것: 페이지 위 **SVG 한 장** (좌표 = 페이지 pt). 지금 비교 표시와 같은 방식.
- 선 굵기는 종이 기준 실제 굵기(축척과 무관)로 그리되, 화면에서 1px보다 가늘어지지 않게 한다.
- 어두운 도면 보기(DV): 도면 그림에만 `--drawing-dark-filter`. SVG 마크업 색은 바꾸지 않는다 (brand.md).

### 5-6. 고르기·클릭 판정

| 동작 | 규칙 (오토캐드 방식) 🟡 |
|---|---|
| 클릭 | 화면에서 5px 안의 도형을 고름. 겹치면 위(나중에 그린 것) |
| 왼쪽→오른쪽 끌기 | 창 안에 **완전히** 들어온 것만 (실선 파란 상자) |
| 오른쪽→왼쪽 끌기 | 창에 **걸친** 것 모두 (점선 초록 상자) |
| Shift+클릭 | 고른 것에서 빼기 |
| 고르고 명령 / 명령 후 고르기 | 둘 다 됨 (MOVE·COPY·ERASE) |
| 잠긴 레이어·`readOnly` | 고를 수 없음 |

### 5-7. 오스냅 🟡 추천안 적용: 브라우저에서 계산

| 항목 | 규칙 |
|---|---|
| 대상 (1단계) | **내가 그린 도형만** + 비교 탭의 구름 |
| 종류 | END(□) · MID(△) · CEN(○) · QUA · INT(✕) · PER · NEA. 켤 종류는 설정(10) |
| 거리 | 화면 10px (설정 "Snap distance") |
| 우선순위 | 커서에 가장 가까운 점, 같으면 END > INT > MID > CEN > QUA > PER > NEA |
| 한 번만 쓰기 | 점을 물을 때 `END`, `MID` 등을 치면 다음 점 하나만 그 종류로 |
| 표시 | `--snap-marker` 색 기호 + 이름표 (07 "Intersection") |
| 구조 | 오스냅 점을 내주는 곳을 **"점 공급자"**로 나눠 둔다. 1단계 공급자 = 내 도형. 2단계에 "PDF 도면 선" 공급자를 붙이면 화면·명령은 그대로 |

직교(ORTHO): 직전 점 기준 0°·90°로만. 격자(GRID·SNAP): 설정의 Grid spacing(실제 mm) 간격 점에 붙음.

### 5-8. 되돌리기 ✅ D7 (탭마다)

- 명령 하나가 끝날 때 "바뀐 도형의 전·후 모습"을 한 묶음으로 기록 (예: MOVE로 3개를 옮기면 1묶음).
- 그리는 중 `Undo` 옵션(LINE의 마지막 선 지우기)은 명령 안에서만 쓰는 것으로, 명령이 끝나면 1묶음이 된다.
- 비교 탭의 표시 고치기·지우기·제외도 같은 방식으로 기록 (지금 따로 있는 되돌리기를 합침).
- 탭마다 최근 200묶음. 저장해도 기록은 남는다.

### 5-9. 저장할 때 서버로 보내는 것

- 도형마다 "새로 생김 / 바뀜 / 지워짐"을 표시해 두고, 저장할 때 **바뀐 것만** 보낸다.
- 서버는 바뀐 주석을 같은 `id`로 지우고 다시 넣는다 (엔진 `DeleteMarkup` + `AddMarkup(id)` 이미 있음).
- `readOnly` 주석은 절대 건드리지 않는다.

---

## 6. 명령 시스템

### 6-1. 원칙

1. **모든 기능은 명령 하나**다. 리본 버튼·단축키·오른쪽 패널 버튼·떠 있는 도구 막대는 모두 "명령창에 그 명령을 대신 쳐 주는 것"이다.
2. 명령은 **등록표 한 장**(`commands/registry.ts`)에 모두 적는다. 리본·자동완성·설정의 별칭 표·`?` 명령 목록은 이 표를 읽어 만든다 → 버튼은 있는데 명령이 없는 일이 생기지 않는다.
3. 명령 이름은 영어 고정, 설명·프롬프트 글자는 번역 (8장).

### 6-2. 등록표 한 줄의 모양

| 칸 | 뜻 | 예 |
|---|---|---|
| `name` | 명령 이름 | `LINE` |
| `aliases` | 기본 별칭 (설정에서 바꿀 수 있음) | `L` |
| `description` | 설명 (번역 키) | "Draw straight line segments" |
| `shortcut` | 단축키 | 없음 / `Ctrl+S` |
| `where` | 쓸 수 있는 탭 | Drawing, Compare(리비전 칸) |
| `needs` | 미리 필요한 것 | 고른 도형, 열린 문서 등 |
| `phase` | 1 / 2 / later | 2면 흐리게 + "Phase 2" (D1) |
| `run` | 실행 대본 (6-3) | |

쓸 수 없는 곳에서 부르면 실행하지 않고 이유를 알려 준다: `LINE is not available on the Home tab.`

### 6-3. 실행 대본 (프롬프트 단계)

명령마다 **"무엇을 차례로 묻는지"를 적은 짧은 대본**이 있다. 대본이 쓸 수 있는 질문은 5가지뿐이다.

| 질문 | 받는 입력 | 예 |
|---|---|---|
| 점 | 클릭(오스냅·직교·격자 적용), 좌표 글자, 거리 숫자 | `Specify first point:` |
| 숫자 | 숫자, 또는 두 점 클릭 | `Specify radius:` |
| 옵션 | 대괄호 안 단어 (밑줄 글자를 치거나 클릭) | `[Undo/Close]` |
| 글자 | 한 줄 글자 (Enter로 끝) | `Enter text:` |
| 고르기 | 도형 고르기 (5-6), Enter로 끝 | `Select objects:` |

공통: Esc = 명령 취소(그때까지 그린 것은 남지 않음, 단 LINE·PLINE처럼 선을 하나씩 확정하는 명령은 확정된 선까지 남음 — 오토캐드와 같음). 기본값이 있는 질문은 `<12>`처럼 보여 주고 Enter로 받는다 (12 화면 `Tolerance <12>:`).

### 6-4. 입력 해석

| 입력 | 뜻 |
|---|---|
| `100,200` | 절대 좌표 (실제 mm, 원점 왼쪽 아래) |
| `@100,50` | 직전 점에서 오른쪽 100, 위 50 |
| `@500<90` | 직전 점에서 500 거리, 90° 방향 |
| `2000` (점을 물을 때) | 커서 방향으로 2000 (07 Tips) |
| `U`, `C` 등 | 옵션의 밑줄 글자 |
| `END`, `MID`, `INT`, `CEN`, `QUA`, `PER`, `NEA` | 다음 점 하나만 그 오스냅 |
| 빈 Enter / Space | 명령 중: 지금 단계 끝내기 / 명령 없을 때: 직전 명령 반복 |
| 글자 질문 중 Space | 띄어쓰기 (Enter만 끝) |

틀린 입력은 빨간 글자 + 고칠 방법: `Invalid point. Use x,y or @distance<angle.`

### 6-5. 자동완성·오타 제안·기록

| 기능 | 규칙 |
|---|---|
| 자동완성 (04) | 한 글자부터. 순서: ① 이름·별칭이 정확히 같음 ② 이름·별칭이 그 글자로 시작 ③ 설명에 그 단어 포함. 최대 8줄, 4칸(이름·별칭·설명·단축키). ↑↓ 고르기, Tab 완성, Enter 실행 |
| 오타 제안 (11) | 아는 명령이 없으면 글자 차이 2개 이하 중 가장 가까운 것: `Unknown command "LNE". Did you mean LINE (L)? Press Tab to use it.` |
| `?` / F1 | 전체 명령 목록 창 (탭별·phase 표시) |
| 기록 | 명령창 위 2~3줄, F2로 펼침. 입력칸이 비었을 때 ↑↓로 이전 입력 다시 부르기 |

### 6-6. 1단계 명령 목록 🟡 추천안 적용

별칭은 오토캐드와 같게 하되, 이 앱에 없는 오토캐드 명령의 별칭은 이 앱 명령에 썼다 (예: `X` = EXCLUDE, 오토캐드는 EXPLODE).

**공통**

| 명령 | 별칭 | 단축키 | 하는 일 |
|---|---|---|---|
| `OPEN` | | Ctrl+O | 파일 열기 창 |
| `QSAVE` | | Ctrl+S | 저장 |
| `SAVEAS` | | Ctrl+Shift+S | 다른 이름으로 저장 |
| `EXPORT` | `EXP` | Ctrl+E | 내보내기 창 (06) |
| `EXIT` | | Ctrl+W | 지금 탭 닫기 |
| `CMP` | `COMPARE` | | 새 비교 탭 |
| `PNG` | | | 새 투명 PNG 탭 |
| `OPTIONS` | `OP` | | 설정 창 (10) |
| `UNDO` | `U` | Ctrl+Z | 되돌리기 |
| `REDO` | | Ctrl+Y | 다시 하기 |
| `ZOOM` | `Z` | 휠 | `[Extents/Window/In/Out]` |
| `PAN` | `P` | 가운데 버튼 | 화면 이동 |
| `PAGE` | `PG` | PgUp·PgDn | `[Next/Previous]` 또는 번호 |
| `FOCUS` | | F11 | 포커스 모드 |
| `DV` | `DARKVIEW` | | 어두운 도면 보기 |
| `GRID` / `ORTHO` / `SNAP` / `OSNAP` | `OS`(설정 열기) | F7 / F8 / F9 / F3 | 상태 토글 |
| `PANELS` | | Ctrl+0 | 좌우 패널 숨기기·보이기 |
| `PSCALE` | | | 지금 페이지 축척 입력 (D6) |
| `?` | `HELP` | F1 | 명령 목록 |

**Draw (작도)**

| 명령 | 별칭 | 대본 (프롬프트) |
|---|---|---|
| `LINE` | `L` | `Specify first point:` → `Specify next point or [Undo]:` 반복 (3점부터 `[Undo/Close]`) |
| `PLINE` | `PL` | `Specify start point:` → `Specify next point or [Undo/Close]:` 반복 → Enter |
| `CIRCLE` | `C` | `Specify center point:` → `Specify radius or [Diameter]:` |
| `RECTANG` | `REC` | `Specify first corner:` → `Specify other corner or [Dimensions]:` |
| `REVCLOUD` | `RC` | `Specify first corner or [Polygon/Object/ArcSize]:` → 사각형(기본) / 점 찍기 / 닫힌 도형 골라 구름으로 |
| `HATCH` | `H` | `Specify first boundary point or [Select objects/Pattern/Scale/Angle]:` → `Specify next point or [Undo/Close/…]:` (08). `Pick internal point`는 Phase 2 |
| `TEXT` | `T`, `DT` | `Specify start point:` → `Specify height <2.5>:` → `Specify rotation <0>:` → `Enter text:` |
| `MOVE` | `M` | `Select objects:` → `Specify base point:` → `Specify second point:` |
| `COPY` | `CO`, `CP` | MOVE와 같고 두 번째 점을 반복 (`[Exit]`) |
| `ERASE` | `E` | `Select objects:` → Enter |
| `LAYER` | `LA` | 왼쪽 패널 Layers 열기 (`-LAYER`로 명령창에서 `[New/Set/On/Off/Lock/Unlock/Color]`) |
| `PROPERTIES` | `PR`, `CH` | 오른쪽 패널 열기 |
| `DELTA` | | `Specify location:` → `Enter label <다음 번호>:` |

**Markup (Bluebeam식 표시)**

| 명령 | 별칭 | 하는 일 |
|---|---|---|
| `TEXTBOX` | `TB` | 글상자: 두 구석 → 글자 |
| `CALLOUT` | `CA` | 지시선 끝 → (꺾임점) → 글 상자 → 글자 |
| `NOTE` | | 메모 아이콘 |
| `ARROW` | `AR` | 시작점 → 끝점 |
| `ELLIPSE` | `EL` | 두 구석 |
| `PEN` | | 끌어서 자유 곡선, Enter 끝 |
| `HIGHLIGHT` | `HL` | 두 구석 (노랑 반투명) |
| `STATUS` | `ST` | 고른 마크업의 검토 상태 `[Open/InProgress/Resolved/Reopened]` |
| `MARKUPS` | `ML` | 마크업 목록 펼치기·접기 (D5) |

**Compare (비교 탭)**

| 명령 | 별칭 | 하는 일 |
|---|---|---|
| `SWAP` | | 원본·리비전 바꾸기 |
| `SBS` | | 나란히 보기 |
| `OVERLAY` | `OV` | 겹쳐 보기 (05) |
| `SYNC` | | 같이 이동 켜고 끄기 |
| `NEXT` / `PREV` | `N` / `PV` | 다음·이전 변경 |
| `REVIEWED` | `RV` | 지금 변경을 검토됨으로 |
| `EXCLUDE` | `X` | 지금 변경을 내보내기에서 빼기 |
| `EXPORTCSV` | `EXC` | 변경 목록 CSV |
| `NUDGE` | | 겹쳐 보기 정렬 미세 조정 (05) |
| `NUMBERING` | `NUM` | △ 번호 방식 `[Each/Same]`, 시작 번호 |

**Tools (투명 PNG 탭)**

| 명령 | 하는 일 |
|---|---|
| `PASTECLIP` | 클립보드 그림 붙여넣기 (Ctrl+V) |
| `BGREMOVE` | 흰 배경 지우기 |
| `BGPICK` | 배경 색 찍기 |
| `TOLERANCE` | 기준값 `<12>` |
| `SMOOTH` | 가장자리 부드럽게 |
| `CROP` | 여백 자르기 |
| `PNGOUT` | PNG 내려받기 |
| `COPYCLIP` | 결과를 클립보드로 (Ctrl+C) |

**2단계 (등록만 하고 흐리게, D1)**: `DIST`(`DI`) 거리 재기, `CALIBRATE` 두 점 축척 맞추기, `PDFLAYER` PDF 레이어 켜고 끄기, HATCH의 `Pick internal point`.

### 6-7. 키보드가 어디로 가나 (2-5 보충)

| 상황 | 글자 키가 가는 곳 |
|---|---|
| 패널·대화상자의 입력칸에 커서가 있음 | 그 입력칸 |
| 명령이 "글자"를 묻는 중 (TEXT, TEXTBOX) | 명령창 (Space도 글자) |
| 그 밖 | 명령창 |

단축키(Ctrl+…, F키)는 입력칸에 커서가 있어도 동작한다. 단, Ctrl+Z·Ctrl+C·Ctrl+V는 입력칸 안에서는 그 칸의 되돌리기·복사·붙여넣기.

---

## 부록 A. 1차 회의에서 Owner가 확인할 것

답이 없으면 아래 추천안대로 2차 회의(4~6장)로 넘어간다.

| # | 질문 | 적용한 추천안 |
|---|---|---|
| Q1 | 파일 열기·저장 방식 | 서버가 내 PC 경로로 직접 (3장) |
| Q2 | 탭 구조 | 문서 탭 = 작업 하나 (2-1) |
| Q3 | 2단계 기능을 1단계 화면에 어떻게 | 흐리게 + "Phase 2" (D1) |
| Q4 | Draw / Markup 탭 나누기 | Draw = 작도, Markup = Bluebeam식 표시 (D4, 2-4) |
| Q5 | 비교 탭에서 그리기 | 리비전 칸에만 (D3) |
| Q6 | Drawing 탭 Ctrl+S | 같은 파일에 저장 + 처음 한 번 `.bak` (3-1) |
| Q7 | 축척 | 페이지마다 숫자로 입력 (D6) |
| Q8 | 앱 전용 파일 열기 창 시안 1장 추가 | 추가해서 확인받기 (3-2) |
| Q9 | 실제 CAD PDF 샘플 1~2개 (회사 정보 없는 것) | 10장 위험 시험에 필요. 2차 회의 전에 받으면 좋음 |

## 부록 B. 2차 회의(4~6장)에서 Owner가 확인할 것

답이 없으면 아래 추천안대로 3차 회의(7~10장)로 넘어간다.

| # | 질문 | 적용한 추천안 |
|---|---|---|
| Q10 | 상태 보관 방식 | Zustand (5-1) |
| Q11 | 화면에 보이는 좌표 | 실제 mm, 원점 왼쪽 아래, 위로 Y 증가 (5-2) |
| Q12 | 레이어 저장 | 앱 전용 꼬리표 `/DRLayer`, 끄기·잠금은 앱 화면에서만 (5-4) |
| Q13 | 그리는 방식 | SVG (5-5) |
| Q14 | 고르기 방식 | 오토캐드 방식 (왼→오 = 완전히 안, 오→왼 = 걸친 것) (5-6) |
| Q15 | 오스냅 계산 | 브라우저 (5-7) |
| Q16 | 글꼴 | 앱 안에 넣기 (인터넷 없이 동작) (4-4) |
| Q17 | 명령·별칭 목록 | 6-6 표. 빠졌거나 바꾸고 싶은 명령이 있으면 말하기 |
