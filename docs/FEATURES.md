# 기능 지도 (어디에 있고, 누가 부르는가)

기능을 고치기 전에 이 표에서 연결된 곳을 확인하고, 고친 뒤에는 이 문서도 함께 갱신한다.

## 프로젝트 구성

| 프로젝트 | 역할 | 실행 환경 |
|---|---|---|
| `src/DrawingReview.Engine` | PDF 엔진 경계와 MuPDF 구현. 화면과 무관한 PDF 처리 | Windows·Linux |
| `tests/DrawingReview.Engine.Tests` | 엔진 자동 테스트 | Windows·Linux |
| `tests/DrawingReview.TestData` | 가상 도면 생성기 (테스트·검증 도구 공용) | Windows·Linux |
| `tools/EngineSpike` | 0단계 검증 실행기: 샘플·결과 PDF·보고서 생성 | Windows·Linux |

## Engine 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 엔진 교체 경계 (`IPdfEngine`, `IPdfDocument`) | `Documents/IPdfDocument.cs` | — | 테스트, EngineSpike, (향후) 앱 |
| 좌표 모델 (`PagePoint`, `PageRect`, `PageInfo`) | `Geometry/PageGeometry.cs` | — | 엔진 전체, 마크업 모델 |
| 마크업 입력 모델 (`*Draft`) | `Markups/MarkupDraft.cs` | 좌표 모델, `MarkupStyle` | `IPdfDocument.AddMarkup` |
| 마크업 조회 모델 (`MarkupInfo`, `MarkupKind`) | `Markups/MarkupInfo.cs` | 좌표 모델 | `GetMarkups`, (향후) Review List |
| 스타일·색상 (`MarkupStyle`, `RgbColor`) | `Markups/MarkupStyle.cs` | — | 마크업 입력 모델, (향후) Saved Tools |
| 안전 저장 (임시 파일 → 검증 → 교체) | `Persistence/SafeFileSaver.cs` | — | `MuPdfDocument.SaveTo`, (향후) 프로젝트 데이터 저장 |
| MuPDF 열기 (메모리로 읽어 파일 잠금 없음) | `MuPdf/MuPdfEngine.cs` | — | 앱의 문서 열기 |
| 문서 조작 (페이지 정보, 렌더링, 검색, 주석 조회·삭제, 저장, 보안 정보) | `MuPdf/MuPdfDocument.cs` | `SafeFileSaver`, `MuPdfCoordinates` | `IPdfDocument` 사용처 전체 |
| 마크업 → 표준 PDF 주석 변환 | `MuPdf/MuPdfMarkupWriter.cs` | `MuPdfCoordinates` | `MuPdfDocument.AddMarkup` |
| 보이는 좌표 ↔ MuPDF/PDF 좌표 변환 (회전·CropBox 보정) | `MuPdf/MuPdfCoordinates.cs` | — | Writer, Document |

## 마크업 종류별 PDF 저장 형식

| 도구 | PDF 형식 | 비고 |
|---|---|---|
| 글상자 | FreeText | 테두리 굵기 = `LineWidth`, 테두리 색 = 글자색 |
| Callout | FreeText + `/IT /FreeTextCallout` + `/CL` | 지시선 좌표는 직접 계산 (engine-validation #3) |
| 선 / 화살표 | Line (+ `/LE /ClosedArrow`) | |
| 사각형 / 타원 | Square / Circle | |
| Cloud | Polygon + `/BE /S /C` | `Intensity` = 구름 볼록함 |
| 펜 | Ink | |
| 영역 하이라이트 | Square + 채움 + `/BM /Multiply` + `/DRKind /Highlight` | engine-validation #9 |
| 기타 기존 주석 | 읽기만 하고 보존 (`MarkupKind.Other`) | 메모, 링크, 스탬프 등 |
