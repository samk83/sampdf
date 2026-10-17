# 0단계 엔진 검증 결과 (MuPDF.NET — 역사 기록)

> **2026-10-17**: 엔진을 PDFium + PDFsharp로 교체했다. 아래는 MuPDF.NET 시절 검증 기록이며, 새 엔진은 같은 검증 실행기(`tools/EngineSpike`)와 자동 테스트 294개로 다시 확인했다 (`docs/CHANGELOG.md`). 결과 파일 `validation/output/*`는 새 엔진으로 다시 만든 것이다.

기획서 12장 "0단계 — 엔진 검증"의 결과 기록이다. 실행 결과 원본은 `validation/output/report.md`에 있다.

- 검증 엔진: **MuPDF.NET 3.28.2.4** (MuPDF 1.28.2, AGPL-3.0 — 개인 로컬 사용)
- 검증 환경: Linux 클라우드 컨테이너, .NET 8. **Windows 실측은 아직 하지 않았다.**
- 검증 도면: 가상 도면(`samples/virtual/plan-set.pdf`, `dense-vector.pdf`). 실제 CAD/Revit 출력은 아직 시험하지 않았다.

## 결론 (잠정)

**MuPDF.NET을 1차 엔진으로 채택하고 다음 단계로 진행한다.** P0 마크업(글상자, Callout, 선, 화살표, 사각형, 타원, Cloud, 펜, 하이라이트)을 모두 표준 PDF 주석과 외형(/AP)으로 저장하고, 다시 열었을 때 종류, 위치, 작성자, 내용이 유지된다. 회전 페이지와 크롭 페이지 문제는 엔진 연결부에서 보정했다.

단, 아래 **"Sam이 직접 확인할 항목"**의 Acrobat Reader와 Bluebeam 확인이 끝나야 최종 확정한다.

## 자동 검증 항목 (64개 테스트 모두 통과)

| 항목 | 결과 | 테스트 |
|---|---|---|
| 9종 마크업 저장 → 다시 열기 (종류·작성자·주제·내용·위치) | ✅ | `MarkupRoundTripTests` |
| 회전 90/180/270°, CropBox 페이지에서 위치 유지 | ✅ | `Markup_position_is_kept_on_rotated_and_cropped_pages` |
| 렌더링 결과에서 보이는 위치에 실제로 그려지는지(픽셀 확인) | ✅ | `Rendered_markup_appears_at_visible_position` |
| 모든 새 주석에 외형 스트림(/AP) 존재 | ✅ | `Every_new_markup_has_an_appearance_stream` |
| Cloud = Polygon + `/BE /S /C` | ✅ | `Cloud_is_a_polygon_with_cloudy_border_effect` |
| Callout = FreeText + `/IT /FreeTextCallout` + `/CL` | ✅ | `Callout_uses_standard_freetext_callout_keys` |
| 기존 메모 주석·URL 링크·내부 링크 보존 | ✅ | `Existing_notes_and_links_are_preserved` |
| 한글 주석 내용·작성자 왕복 | ✅ | `Korean_comment_text_round_trips` |
| 한글·영문 텍스트 검색 (보이는 좌표로 반환) | ✅ | `Text_search_finds_english_and_korean_in_visible_coordinates` |
| 저장 실패 시 원본 유지, 임시 파일 정리 | ✅ | `SafeFileSaverTests` |
| 열어 둔 파일에 그대로 덮어쓰기 저장 (파일 잠금 없음) | ✅ | `Saving_over_the_opened_file_works_because_source_is_not_locked` |
| 암호 문서 감지 및 암호 입력 | ✅ | `Encrypted_document_requires_password` |
| 주석 50개 추가 후 파일 크기 1MB 미만 | ✅ | `Saving_does_not_grow_file_unreasonably` |
| 고유 주석 ID (페이지·저장 회차가 달라도 중복 없음) | ✅ | `Markup_ids_are_unique_across_pages_and_sessions` |

## 발견한 엔진 특성과 대응

| # | 발견 내용 | 대응 (코드 위치) |
|---|---|---|
| 1 | Square/Circle/Line/Polygon 생성 함수는 보이는 좌표를 받지만, **Ink/FreeText는 회전 전 좌표**를 받는다. | `MuPdfCoordinates.ToUnrotated`로 변환 (`MuPdfMarkupWriter`) |
| 2 | `Annot.Rect`와 검색 결과는 **회전 전 좌표**로 돌아온다. | `FromUnrotated`로 보이는 좌표로 변환 (`MuPdfDocument`) |
| 3 | FreeText `callout` 인자가 **CropBox 페이지에서 지시선을 잘못된 위치**에 쓴다. | `/IT`, `/CL`, `/Rect`, `/RD`를 직접 계산해 기록 (`ApplyCallout`) |
| 4 | 지시선이 없는 글상자에도 `/CL`을 기록한다. | 사전에서 `/CL` 삭제 |
| 5 | 기본 주석 ID(`/NM fitz-A0`)가 페이지마다 겹친다. | GUID로 교체 |
| 6 | FreeText 테두리 색(`borderColor`)은 richtext 모드에서만 가능하고, richtext는 **한글 글꼴 전체(약 20MB)를 포함**한다. | richtext 미사용. 테두리와 지시선은 글자색으로 그린다. |
| 7 | 한글 글상자 외형은 **비포함(Batang 참조)** 글꼴을 쓴다. MuPDF에서는 정상 표시된다. | Acrobat/Bluebeam 표시 확인 필요 (아래 체크리스트) |
| 8 | 한글 글상자 외형에서 **단어 사이 간격이 넓게** 그려진다. | 기능에는 문제없음. 외부 뷰어가 외형을 다시 만들면 달라질 수 있다. 후속 개선 후보. |
| 9 | PDF Highlight 하위 유형은 글자용이라 도면 영역에서는 **둥글게 왜곡**되고 회전 페이지에서 크기가 커진다. | 영역 하이라이트는 채움 사각형 + Multiply + 40% 불투명도 + `/DRKind /Highlight`로 저장 |
| 10 | `Page.InsertText(fontName: "korea")`는 한글을 깨뜨리고, `InsertHtmlBox`는 글꼴 전체(약 19MB)를 넣는다. | 가상 도면은 Adobe-Korea1 CID 글꼴 참조로 생성 (`KoreanText`). 나중에 평탄화와 스탬프에서 본문에 한글을 넣을 때도 같은 문제가 생기므로 이 방식을 재사용해야 한다. |
| 11 | `Page` 객체는 새 페이지 추가 등 구조 변경 후 무효화될 수 있다. | 매번 `doc[i]`로 새로 가져온다. |
| 12 | 기본 14 글꼴에 대해 "invalid font - FontDescriptor missing" 경고를 콘솔에 출력한다. | 표준상 정상(기본 14 글꼴은 설명자 불필요). 무시. |

## 성능 참고치 (Linux 컨테이너)

| 파일 | 열기 | 화면 맞춤 렌더(1600px) | 400% 부분 렌더 |
|---|---|---|---|
| plan-set.pdf (6쪽) | ~0 ms | ~40 ms | ~35 ms |
| dense-vector.pdf (선 약 3만 4천 개) | ~0 ms | ~300 ms | ~150 ms |

기획서 13장 성능 목표(100쪽·100MB, 첫 화면 5초)는 실제 도면집으로 Windows 기준 PC에서 다시 측정해야 한다. 파일을 메모리로 읽어 여는 방식이라 100MB 파일은 메모리를 그만큼 더 쓴다. 문제가 되면 파일 복사본을 여는 방식으로 바꾼다.

## Sam이 직접 확인할 항목 (Windows)

`validation/output/markup-roundtrip.pdf`를 내려받아 확인한다. 페이지마다 같은 9종 마크업이 들어 있다. (1쪽: 회전 없음, 2쪽: 90°, 3쪽: 크롭, 4쪽: 한글 문서, 5쪽: 180°, 6쪽: 270°)

| 확인 | Acrobat Reader | Bluebeam |
|---|---|---|
| 9종 마크업이 미리보기 이미지(`validation/output/previews`)와 같은 위치에 보인다 | ☐ | ☐ |
| Cloud가 구름 모양으로 보이고, 선택하면 다각형/구름 주석으로 인식된다 | ☐ | ☐ |
| Callout 화살표가 화살촉 위치를 가리키고, 글상자를 옮기면 지시선이 따라온다 | ☐ | ☐ |
| 한글 글상자("한글 주석: 간섭 확인 필요")가 네모(□)로 깨지지 않는다 | ☐ | ☐ |
| 글상자를 더블클릭해 글자를 고칠 수 있다 | ☐ | ☐ |
| 하이라이트가 반투명이라 아래 도면 선이 보인다 | ☐ | ☐ |
| 주석 목록(Comments / Markups List)에 작성자 "Sam", 주제 "Structural"이 보인다 | ☐ | ☐ |
| 1쪽의 기존 메모 주석과 URL 링크, 4쪽의 1쪽 이동 링크가 그대로 있다 | ☐ | ☐ |
| 그 프로그램에서 저장한 뒤 다시 이 앱(향후)으로 열 수 있다 | ☐ | ☐ |

결과가 다르면 어느 페이지의 어떤 마크업인지 알려 주시면 된다.

## 아직 검증하지 않은 것

- 실제 Revit/AutoCAD 출력 PDF, 스캔 PDF, 100쪽·100MB 도면집
- 인쇄와 평탄화(P0이지만 2단계에서 구현하면서 검증)
- 기존 전자서명이 있는 문서 저장 시 동작 (현재는 감지와 경고만 함)
