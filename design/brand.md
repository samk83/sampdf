# 브랜드 디자인: Sam Drawing Tools

> 공통 규칙은 프로젝트 루트의 `design.md`. 이 파일은 이 앱만의 디자인 결정이다. 실제 값은 `tokens.css`.

## 확정 안
- 시안: **A안(Revu Pro Dark) 배치 + D안 포커스 모드 + S1 Slate 스킨** · 확정일: 2026-10-16
- 한 줄 콘셉트: 오토캐드처럼 시스템적이고 빠른 도면 작업대. 어두운 슬레이트 화면, 각진 모양, 명령창 하나로 모든 기능을 부른다.
- 참고는 했지만 복제하지 않는다: Bluebeam(리본·패널 배치), AutoCAD(슬레이트 색감·명령창·상태 토글). 회사 로고·고유 색은 쓰지 않는다.

## 화면 뼈대 (모든 화면 공통)
| 영역 | 크기 토큰 | 내용 |
|---|---|---|
| 제목줄 | `--size-titlebar` | 앱 이름, 열린 문서 탭(저장 안 됨 ●), 되돌리기·다시 하기, 저장 상태, Focus F11 |
| 리본 탭 + 리본 | `--size-ribbon-tabs` + `--size-ribbon` | 탭: Home · Compare · Markup · Draw · View · Tools. 그룹(아이콘 20px + 글자) + 오른쪽 작은 설정 칸 |
| 왼쪽 패널 | `--size-panel-left` | 탭: Pages · Bookmarks · Layers |
| 도면 | `--color-canvas` 바탕 위 흰 종이 | 페이지·확대 막대(떠 있음) |
| 오른쪽 패널 | `--size-panel-right` | Properties (선택한 것에 따라 내용이 바뀜) |
| 명령창 | `--size-cmdline-input` | 기록 2~3줄 + 입력 줄. 검토 화면은 도면 위에 떠 있고(`--color-cmdline-float`), 비교 화면은 아래 표와 겹치지 않게 붙어 있음 |
| 상태 표시줄 | `--size-statusbar` | 현재 명령·오스냅 종류 · GRID · SNAP · ORTHO · OSNAP · DARK VIEW 토글 · 좌표(mm) · 축척 · 페이지 · 언어 |
| 포커스 모드 (F11) | — | 위 영역을 숨기고 도면만. 떠 있는 패널(문서 이름, 도구 막대, 확대, 명령창, 선택 말풍선) |

## 색 역할
| 역할 | 토큰 | 값 | 쓰는 곳 |
|---|---|---|---|
| 기본색 | `--color-primary` | #126fa8 | 주요 버튼 채움 (흰 글자 5.43:1) |
| 강조색 | `--color-accent` | #2ba8e0 | 선택 테두리, 켜진 토글, 링크, 명령 옵션 [Undo/Close] |
| 배경 | `--color-background` / `--color-surface` | #232a33 / #2a323d | 앱 바탕·입력칸 / 패널·리본·대화상자 |
| 제목줄·상태 | `--color-chrome` | #1a1f26 | 제목줄, 상태 표시줄 |
| 작업 공간 | `--color-canvas` | #1c2229 | 도면 뒤 |
| 글자 | `--color-text` / `--color-text-secondary` / `--color-text-muted` / `--color-text-faint` | #e3e8ee / #c3ccd6 / #a0acba / #93a0ae | 본문 / 버튼 / 보조 / 단축키·단위(보조 정보만) |
| 상태 | `--color-success` · `--color-warning` · `--color-danger` | #8ce99a · #ffe066 · #ff8787 | 검토됨·완료 / 안내 / 오류·지우기 |
| 도면 표시 | `--markup-red` · `--markup-yellow` · `--markup-orange` · `--markup-blue` | #e5383b · #ffd43b · #ff922b · #1c7ed6 | 구름·△ / 글자 하이라이트 / 해칭 / 메모. 흰 종이 기준이며 내보낸 PDF와 같은 색 |
| 오스냅 | `--snap-marker` | #f59f00 | 오스냅 표시(□ 끝점, △ 중간점, ✕ 교차점, ○ 중심) |
| 겹쳐 보기 | `--overlay-original` / `--overlay-revised` | #e03131 / #1c7ed6 | 원본만 있는 선 / 리비전만 있는 선 |

## 글꼴
- 기본: Pretendard (한국어·영어 모두). 명령창·좌표·레이어 이름·단축키: JetBrains Mono (`--font-family-mono`)
- 크기: 10.5(상태 토글·그룹 이름) · 11.5(리본 글자·보조) · 12.5(표·패널·명령창) · 13(본문) · 15(대화상자 제목) · 22(시작 화면 제목)
- 숫자: 좌표·치수는 고정폭 글꼴로 자리 맞춤

## 모양
- 모서리: 2px(버튼·입력칸·토글) · 3px(떠 있는 명령창·메뉴) · 4px(대화상자·카드). 원형은 색 견본·라디오만
- 그림자: 떠 있는 것만(`--shadow-md`), 대화상자(`--shadow-lg`), 도면 종이(`--shadow-sheet`)
- 테두리: 1px. 패널 사이 `--border-default`, 패널 안 `--border-subtle`, 입력칸 `--border-control`

## 아이콘·이미지
- 아이콘: Lucide, 선 굵기 2px(리본은 1.7px), 크기 16px(작은 버튼) / 20px(리본)
- 리본 아이콘은 단색. 구름·△는 빨강, 하이라이트는 노랑으로 의미 색만 쓴다
- 이미지·일러스트: 쓰지 않음 (도면 썸네일만)

## 명령창 규칙
- 오토캐드 방식: 글자를 치면 명령창으로 입력, Enter/Space 실행, Enter만 치면 직전 명령 반복, Esc 취소
- 프롬프트: `Specify next point or [Undo/Close]:` 처럼 옵션을 대괄호로, 옵션 글자는 강조색 밑줄(누를 수 있음)
- 자동완성: 이름 · 별칭 · 설명 · 단축키 네 칸, 위아래 화살표 선택, Tab 완성
- 오류: 빨간 글자 + 해결 제안 (예: `Unknown command "LNE". Did you mean LINE (L)?`)
- 명령 이름은 모든 언어에서 영어. 설명·메시지만 번역

## 움직임
- 기본 속도: `--motion-fast` 100ms(눌림·토글) / `--motion-normal` 180ms(패널 열기·메뉴). 튀는 효과 없음
- 화면 전환: 탭 전환은 즉시. 포커스 모드는 패널이 180ms 동안 사라짐
- Reduced Motion 설정 시: 모든 전환 0ms

## 화면 목록 (확정 시안)
| 화면 | 파일 | 메모 |
|---|---|---|
| 시작 | `screens/01-home.html` | 큰 카드 3개, 최근 파일·비교, 끌어다 놓기 |
| 비교 · 두 번째 파일 기다림 | `screens/02-compare-waiting.html` | 빈 상태, 비교 설정 |
| 비교 · 진행 중 | `screens/03-compare-running.html` | 진행률, 단계, 취소 |
| 비교 · 결과 | `screens/04-compare-result.html` | 나란히, 미니 도구 막대, 변경 표, 명령 자동완성 |
| 비교 · 겹쳐 보기 | `screens/05-compare-overlay.html` | 빨강/파랑, 투명도, 정렬 미세 조정 |
| PDF 내보내기 | `screens/06-export-pdf.html` | 편집 가능한 마크업(추천) / 녹여 넣기 |
| 검토 · LINE + 오스냅 | `screens/07-review-line-osnap.html` | 그리는 중, 교차점 오스냅, 길이·각도 입력, 레이어 패널 |
| 검토 · HATCH | `screens/08-review-hatch.html` | 경계 그리기, 무늬 5개, 안쪽 클릭은 2단계 |
| 포커스 모드 | `screens/09-focus-mode-dark-view.html` | 어두운 도면 보기를 켠 모습 |
| 설정 | `screens/10-settings.html` | 언어, 오스냅, 명령 별칭, 어두운 도면 보기 |
| 오류·빈 상태 | `screens/11-error-empty.html` | 암호 PDF, 스캔 페이지 안내, 오타 명령 제안 |
| 투명 PNG | `screens/12-transparent-png.html` | 전·후, 설정 |
| 파일 열기 창 | `screens/13-open-drawing.html` | 앱 전용 파일 열기(장소·경로·목록·미리보기). 설계 승인 후 추가 (DESIGN-SPEC 3-2) |

화면 파일은 `design/source/export_static.py`가 만든다 (직접 고치지 말고 `design/source/screens.py`를 고친 뒤 다시 실행).

## 다크 모드
- 앱은 어두운 테마 하나만 쓴다 (밝은 테마 없음).
- **어두운 도면 보기**(명령 `DV`, 상태 표시줄 DARK VIEW): 도면 그림에만 `--drawing-dark-filter`를 건다. 화면 색 토큰은 바뀌지 않고, 내보낸 PDF는 항상 흰 종이.
