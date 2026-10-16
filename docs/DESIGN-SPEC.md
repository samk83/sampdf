# 설계서 (DESIGN-SPEC): Sam Drawing Tools 웹앱

> 3단계 "설계" 결과 문서. 확정 시안(`design/screens/`, `design/brand.md`, `design/tokens.css`)을 실제로 만들 수 있게
> 화면 구조·부품·데이터·명령·서버 연결·개발 순서를 정한다. Owner 승인 후 4단계 개발로 넘어간다.
>
> 상태: **초안 v0.1 (2026-10-16) — 1차 회의 범위(1~3장)만 작성.** 4~10장은 2·3차 회의에서 채운다.
> 근거 브리핑: 프로젝트 파일 `design-phase/설계-브리핑.md`

## 결정 표시 읽는 법

| 표시 | 뜻 |
|---|---|
| ✅ 확정 | Owner가 이미 정한 것 (`docs/DECISIONS.md`에 있음) |
| 🟡 추천안 적용 (Owner 확인 대기) | Owner가 아직 답하지 않아 Claude 추천안으로 적어 둔 것. **바꾸고 싶으면 번호로 말하면 된다** (예: "D3은 숨김으로") |
| ⏳ 나중에 | 2단계 이후 |

## 목차

1. 범위 확정 (1차) — 이 초안
2. 화면 구조 (1차) — 이 초안
3. 파일 열기·저장 (1차) — 이 초안
4. 부품 목록·토큰 연결 (2차)
5. 상태·데이터 모델 (2차)
6. 명령 시스템 (2차)
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

2차 회의에서 정할 것(미리 보기): 상태 보관 방식(추천 Zustand), 좌표·레이어 저장(추천 PDF 좌표 + 앱 전용 꼬리표), 그리기 방식(추천 SVG), 오스냅 계산 위치(추천 브라우저), 명령 등록표와 1단계 명령 전체 목록, 토큰 연결(추천 Tailwind → `tokens.css` 변수).
