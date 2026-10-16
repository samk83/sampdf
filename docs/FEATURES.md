# 기능 지도 (어디에 있고, 누가 부르는가)

기능을 고치기 전에 이 표에서 연결된 곳을 확인하고, 고친 뒤에는 이 문서도 함께 갱신한다.

## 프로젝트 구성

| 프로젝트 | 역할 | 실행 환경 |
|---|---|---|
| `src/DrawingReview.Engine` | PDF 엔진 경계와 MuPDF 구현. 화면과 무관한 PDF 처리 | Windows·Linux |
| `tests/DrawingReview.Engine.Tests` | 엔진 자동 테스트 | Windows·Linux |
| `tests/DrawingReview.TestData` | 가상 도면 생성기: PDF (`VirtualDrawings`, `ComparePairs`), CAD (`CadSamples`) (테스트·검증 도구 공용) | Windows·Linux |
| `tools/EngineSpike` | 0단계 검증 실행기: 샘플·결과 PDF·보고서 생성 | Windows·Linux |
| `src/DrawingReview.App.Core` | 화면 상태·로직 (ViewModel, 확대·이동 계산). WPF에 의존하지 않음 | Windows·Linux |
| `tests/DrawingReview.App.Core.Tests` | App.Core 자동 테스트 | Windows·Linux |
| `src/DrawingReview.App` | WPF 화면 (창, 테마, 도면 캔버스, 대화상자) | **Windows만** (Linux는 컴파일만) |
| `src/DrawingReview.Compare` | 도면 비교 엔진 (정렬, 차이, 글자 비교, 영역 묶기). 화면 없음 | Windows·Linux |
| `tests/DrawingReview.Compare.Tests` | 비교 검수 테스트 (기획서 12장) | Windows·Linux |
| `src/DrawingReview.Cad` | DWG/DXF 읽기·정규화·비교·PDF로 그리기·Cloud 내보내기 | Windows·Linux |
| `tests/DrawingReview.Cad.Tests` | CAD 비교 검증 테스트 | Windows·Linux |
| `src/DrawingReview.Web` | 도면 비교 웹앱 서버 (내 PC 전용, 127.0.0.1:5180) + 빌드된 화면 `wwwroot` | Windows·Linux |
| `tests/DrawingReview.Web.Tests` | 웹 서버 API 테스트 | Windows·Linux |
| `web/` | 웹 화면 원본 (React·TypeScript). `npm run build` → `src/DrawingReview.Web/wwwroot` | 개발할 때만 (Node.js) |

## Engine 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 엔진 교체 경계 (`IPdfEngine`, `IPdfDocument`) | `Documents/IPdfDocument.cs` | — | 테스트, EngineSpike, (향후) 앱 |
| 좌표 모델 (`PagePoint`, `PageRect`, `PageInfo`) | `Geometry/PageGeometry.cs` | — | 엔진 전체, 마크업 모델 |
| 마크업 입력 모델 (`*Draft`) | `Markups/MarkupDraft.cs` | 좌표 모델, `MarkupStyle` | `IPdfDocument.AddMarkup` |
| 마크업 조회 모델 (`MarkupInfo`, `MarkupKind`) | `Markups/MarkupInfo.cs` | 좌표 모델 | `GetMarkups`, (향후) Review List |
| 스타일·색상 (`MarkupStyle`, `RgbColor`) | `Markups/MarkupStyle.cs` | — | 마크업 입력 모델, (향후) Saved Tools |
| 안전 저장 (임시 파일 → 검증 → 교체) | `Persistence/SafeFileSaver.cs` | — | `MuPdfDocument.SaveTo`, (향후) 프로젝트 데이터 저장 |
| MuPDF 열기 (메모리로 읽어 파일 잠금 없음). 그림(PNG·JPG·TIFF·BMP)은 PDF로 바꿔 연다 | `MuPdf/MuPdfEngine.cs` | — | 앱의 문서 열기, 웹 비교 |
| 문서 조작 (페이지 정보, 렌더링, 검색, 주석 조회·삭제, 저장, 보안 정보) | `MuPdf/MuPdfDocument.cs` | `SafeFileSaver`, `MuPdfCoordinates` | `IPdfDocument` 사용처 전체 |
| 마크업 → 표준 PDF 주석 변환 | `MuPdf/MuPdfMarkupWriter.cs` | `MuPdfCoordinates` | `MuPdfDocument.AddMarkup` |
| 보이는 좌표 ↔ MuPDF/PDF 좌표 변환 (회전·CropBox 보정) | `MuPdf/MuPdfCoordinates.cs` | — | Writer, Document |
| 벡터 페이지 모델 (선·글자) | `Vector/VectorPage.cs` | 좌표 모델, `RgbColor` | `CadPlotter`, `WriteVectorPdf` |
| 벡터 페이지 → 새 PDF (`IPdfEngine.WriteVectorPdf`) | `MuPdf/MuPdfVectorWriter.cs` | `KoreanCidFont`, `SafeFileSaver` | `CadComparison` |
| Windows 한글 깨짐 보정 (MuPDF.NET 문자열 콜백을 UTF-8로 다시 등록) | `MuPdf/MuPdfUtf8Fix.cs` | — | `MuPdfEngine` 정적 생성자 (엔진을 처음 쓸 때 한 번) |
| 책갈피 제목을 PDF 객체에서 직접 읽기 (Windows 한글 깨짐 우회) | `MuPdf/MuPdfOutlineTitles.cs` | — | `MuPdfDocument.GetOutline` |
| 한글 글자용 CID 글꼴 참조 (글꼴 파일 미포함) | `MuPdf/KoreanCidFont.cs` | — | `MuPdfVectorWriter`, TestData `KoreanText` |

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

## App.Core 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 확대·이동 계산 (화면↔페이지 좌표, 맞춤, 휠 확대) | `Viewing/Viewport.cs` | `PageRect` | `DocumentViewModel`, WPF `PageView` |
| 엔진 호출 직렬화·백그라운드 실행 | `Services/EngineGate.cs` | — | `DocumentViewModel`, `MainViewModel.SaveDocumentAsync` |
| 대화상자 추상화 | `Services/IDialogService.cs` | — | `MainViewModel` (구현: WPF `WpfDialogService`, 테스트: `FakeDialogs`) |
| 도구 종류·단축키 | `Tools/ToolKind.cs` | — | `MainViewModel`, `PageView`, `MainWindow` |
| 탭 하나(문서): 페이지 이동, 미리보기/타일/썸네일 렌더링, 캐시 | `ViewModels/DocumentViewModel.cs` | `EngineGate`, `Viewport`, `IPdfDocument` | `MainViewModel`, WPF 화면 |
| 썸네일 항목 | `ViewModels/PageThumbnailViewModel.cs` | `RenderedImage` | 왼쪽 Thumbnails 패널 |
| 메인 창 상태: 열기·닫기·저장, 도구, 패널, 복사·붙여넣기, 자동 저장, 평탄화, 설정 적용 | `ViewModels/MainViewModel.cs` | `IPdfEngine`, `IDialogService`, `RecoveryService`, `ToolDefaults` | `MainWindow` |
| 마크업 편집 (추가·삭제·변경·상태·잠금·선택·되돌리기) | `ViewModels/DocumentViewModel.Markups.cs` | `UndoStack`, `EngineGate` | 캔버스, Review List, 속성 패널, 비교 |
| 마크업 한 줄 (목록·선택·속성 공통) | `ViewModels/MarkupItemViewModel.cs` | `MarkupInfo` | Review List, 캔버스, 속성 |
| Review List (필터·정렬·숨김 개수·선택 동기화·상태 일괄·CSV) | `ViewModels/ReviewListViewModel.cs` | `DocumentViewModel` | WPF `ReviewListPanel` |
| 속성 패널 (선택 편집 / 도구 기본값, 최근 색) | `ViewModels/PropertiesViewModel.cs` | `ToolDefaults`, `DocumentViewModel` | WPF `PropertiesPanel` |
| 도형 계산 (이동·크기·클릭 판정·손잡이·Shift 맞춤) | `Editing/DraftGeometry.cs` | — | `MarkupInteraction`, 비교 |
| 마우스 동작 (그리기·선택·이동·크기·글자 입력) | `Editing/MarkupInteraction.cs` | `DraftGeometry`, `ToolDefaults` | WPF `MarkupPageView` |
| 되돌리기 스택 | `Editing/UndoStack.cs` | — | `DocumentViewModel` |
| 도구별 기본 스타일·작성자 | `Editing/ToolDefaults.cs` | `MarkupStyle` | `MarkupInteraction`, 속성, 설정 |
| 설정 파일 (JSON) | `Services/SettingsStore.cs` | `SafeFileSaver` | WPF `App` |
| 자동 저장·복구 사본 | `Services/RecoveryService.cs` | — | `MainViewModel` |
| 도면 비교 작업 공간 (짝, 비교 실행, 수동 정렬, 검토, 합치기·나누기, Cloud, 내보내기, 프로젝트) | `Comparison/CompareViewModel.cs` | Compare 엔진, `DocumentViewModel`, `MarkupInteraction` | WPF `CompareWindow` |
| 비교 모델·Cloud 설정·동기화 | `Comparison/CompareModels.cs` | — | `CompareViewModel`, `CompareWindow` |
| 비교 프로젝트 파일 (.drcompare, CAD 여부 포함) | `Comparison/CompareProject.cs` | `SafeFileSaver` | `CompareViewModel` |
| CAD 비교 한 번: 읽기·비교·화면용 PDF(임시 폴더)·변경 영역 변환 | `Comparison/CadComparison.cs` | Cad 모듈, `IPdfEngine.WriteVectorPdf` | `CompareViewModel.OpenCadAsync`, `ExportCadCloudsAsync` |
| 검색·책갈피 | `ViewModels/SearchViewModel.cs` | `IPdfDocument.SearchText/GetWords/GetOutline` | WPF `SearchPanel`, `BookmarksPanel`, 캔버스 강조 |
| Saved Tools (세트, 스타일/문구·모양 저장, 놓기, 가져오기·내보내기) | `ViewModels/SavedToolsViewModel.cs` | `ToolDefaults`, `MarkupInteraction.BeginPlacement` | WPF `SavedToolsPanel`, 설정 |
| 인쇄 배치 (맞춤/실제 크기, 회전, 잘림 경고, 페이지 범위) | `Printing/PrintLayout.cs` | — | WPF `PrintWindow` |

## App (WPF) 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 프로그램 시작, 명령줄 파일 열기, 예외 처리 | `App.xaml(.cs)` | `MainViewModel`, `WpfDialogService` | — |
| 다크 테마·아이콘·컨트롤 스타일 | `Themes/Dark.xaml` | — | 모든 창 |
| 메인 창 배치, 단축키, 끌어다 놓기, 패널 접기, 닫기 확인 | `MainWindow.xaml(.cs)` | `MainViewModel`, `PageView` | `App` |
| 도면 캔버스 (미리보기+고해상도 타일, 휠 확대, 이동) | `Controls/PageView.cs` | `Viewport`, `DocumentViewModel`, `BitmapFactory` | `MainWindow` |
| 렌더링 결과 → WPF 이미지, 각종 변환기 | `Converters/Converters.cs` | `RenderedImage` | XAML 바인딩, `PageView` |
| 파일 선택·메시지·암호 창 | `Services/WpfDialogService.cs`, `Views/PasswordWindow.xaml` | — | `MainViewModel` (인터페이스 경유) |
| 마크업 캔버스 (선택 표시·손잡이·미리보기·범위 선택) | `Controls/MarkupPageView.cs` | `PageView`, `MarkupInteraction`, `DraftRenderer` | `MainWindow`, `CompareCanvas` |
| 마크업 미리보기 그리기 | `Controls/DraftRenderer.cs` | — | `MarkupPageView`, `CompareCanvas` |
| Review List 표 | `Views/ReviewListPanel.xaml(.cs)` | `ReviewListViewModel` | `MainWindow` |
| 속성 패널 | `Views/PropertiesPanel.xaml` | `PropertiesViewModel` | `MainWindow` |
| 비교 창·페이지 짝 창 (CAD 모드: DXF/DWG Cloud 내보내기, CAD 경고 버튼) | `Views/CompareWindow.xaml(.cs)`, `Views/PairWindow.xaml` | `CompareViewModel`, `ViewportSync` | `MainWindow` Compare 메뉴 (Compare Drawings / Compare CAD Drawings) |
| 비교 캔버스 (겹쳐 보기 그림, 변경 영역, 정렬 점) | `Controls/CompareCanvas.cs` | `MarkupPageView` | `CompareWindow` |
| 검색·책갈피·Saved Tools 패널 | `Views/SearchPanel`, `Views/BookmarksPanel`, `Views/SavedToolsPanel` | 각 ViewModel | `MainWindow` 왼쪽 패널 |
| 인쇄 미리보기·인쇄 | `Views/PrintWindow.xaml(.cs)` | `PrintLayout`, `DocumentViewModel.RenderPageAsync` | File → Print (Ctrl+P) |

## Compare 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 비교 결과·정렬 변환·설정 모델 | `Model/CompareModel.cs` | `PageRect` | 비교 전체 |
| 페이지 비교 (렌더→정렬→차이→글자→잡음→묶기), 겹쳐 보기, 합치기·나누기, PNG | `PageComparer.cs` | `Aligner`, `ImageOps`, `TextDiff` | `CompareViewModel` |
| 자동·수동 정렬 | `Aligner.cs` | OpenCV (ORB, RANSAC, ECC) | `PageComparer`, `CompareViewModel` |
| 이미지 처리·영역 묶기 | `ImageOps.cs` | OpenCV | `PageComparer`, `Aligner` |
| 단어 비교 (바뀐 단어 목록) | `TextDiff.cs` | `TextWord` | `PageComparer` |
| 변경 성격 구분: 글자 변경(하이라이트) / 그림 변경(Cloud), 바뀐 글자 "이전 → 개정" 설명 | `PageComparer.Classify` | `TextDiff`, `Grouping`(글자 밖 차이 픽셀 수) | 웹 비교 (예정), `RevisionMarks` |
| 문서 전체 비교 (페이지 짝, 페이지별 요약, 추가·빠진 페이지 알림) | `DocumentComparer.cs` | `PageComparer` | 웹 비교 (예정), 테스트 |
| 지워진 선만 담은 층 (화면에서 리비전 위에 흐리게 겹치기) | `PageComparer.RenderRemovedLayer` | `ImageOps` | 웹 비교 (예정) |
| 리비전 표시: 하이라이트 · Cloud + △ 리비전 번호(Cloud에만)를 표준 PDF 주석으로 | `RevisionMarks.cs` | `MarkupDraft`, `IPdfDocument.AddMarkup` | 웹 비교 (예정), 테스트 |

## Cad 모듈

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 기본 요소(선·원·호·폴리선·글자·치수)·변경 모델 | `CadModel.cs` | — | Cad 모듈 전체 |
| DWG/DXF 읽기, 블록 펼치기(회전·대칭 반영), 치수, 단위 변환, 경고 | `CadLoader.cs` | ACadSharp | `CadComparison`, 테스트 |
| 짝 맞추기(ID 미사용), 이동·변경·추가·삭제 분류, 치수 값·문자 비교 | `CadComparer.cs` | — | `CadComparison`, 테스트 |
| 모델(mm) ↔ PDF 페이지(pt) 변환, CAD → 벡터 페이지 그리기 | `CadPlotter.cs` | `VectorPage` (Engine) | `CadComparison` |
| 개정 도면 사본에 Revision Cloud 넣기 (DXF/DWG, REVISION 도면층) | `CadCloudExporter.cs` | ACadSharp, `SafeFileSaver` | `CompareViewModel.ExportCadCloudsAsync` |

## Web 모듈 (서버, `src/DrawingReview.Web`)

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 서버 시작 (127.0.0.1:5180, 업로드 300MB, 브라우저 자동 열기 `--no-browser`) | `Program.cs` | — | `run-web.cmd`, `dotnet run` |
| 올린 파일 보관 (메모리, 최근 24개, 문서별 잠금, 원본 바이트로 사본 열기) | `Services/DocumentStore.cs` | `MuPdfEngine` (그림은 PDF로) | `Endpoints` |
| 비교 실행·결과 보관 (제목란 무시, 두 문서 잠금 순서) | `Services/CompareStore.cs` | `DocumentComparer` | `Endpoints` |
| API: `/api/files`(올리기·정보·닫기), `/files/{id}/pages/{n}.png`, `/compare`, `/compare/{id}/pages/{i}/removed.png`, `/compare/{id}/export` | `Api/Endpoints.cs` | `PageComparer.RenderRemovedLayer`, `RevisionMarks.Apply` | 웹 화면 `api.ts` |
| 화면으로 보내는 자료 모양 | `Api/Dtos.cs` | — | `Endpoints`, 테스트 |

## 웹 화면 (`web/src`)

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 탭 전환 (도면 비교 / 투명 PNG, 비교 상태 유지) | `App.tsx` | — | `main.tsx` |
| 공통 위쪽 막대·아이콘 버튼 | `ui/AppHeader.tsx` | lucide-react | 두 화면 |
| 서버 호출·자료 모양·파일 저장 | `api.ts` | — | 비교 화면, PNG 화면(저장) |
| 비교 화면 (좌우 칸, 자동 비교, 함께 이동, 자동 맞춤, 요약, 경고, 내려받기) | `compare/ComparePage.tsx` | 아래 부품들 | `App` |
| 도면 한 페이지 보기 (휠 확대, 끌어 이동, 해상도 단계 미리 받기, 겹침 층) | `compare/PageViewer.tsx` | `compare/view.ts` | `ComparePage` |
| 보기 계산 (맞춤, 커서 기준 확대, 변경 위치로 이동, 그림 배율 단계) | `compare/view.ts` | — | `PageViewer`, `ComparePage` |
| 표시 그리기 (하이라이트·구름·△, 선택 상자) | `compare/Marks.tsx` | `compare/geometry.ts` | `ComparePage` |
| 구름 경로·△ 위치·설명 줄이기 (엔진 `RevisionMarks`와 같은 규칙) | `compare/geometry.ts` | — | `Marks`, `ChangeList` |
| 파일 열기 칸 (클릭·끌어다 놓기) | `compare/DropZone.tsx` | — | 비교 화면, PNG 화면 |
| 변경 목록 (필터, 선택, 지우기·되돌리기, 접기) | `compare/ChangeList.tsx` | `geometry.shortText` | `ComparePage` |
| 투명 PNG 화면 (슬라이더, 배경 색 자동·찍기, 부드럽게, 여백 자르기, 선 색 맞추기, 내려받기) | `png/PngTool.tsx` | `png/removeBackground.ts`, `DropZone` | `App` |
| 배경 지우기 계산 (브라우저 안에서만) | `png/removeBackground.ts` | — | `PngTool` |
