# 기능 지도 (어디에 있고, 누가 부르는가)

기능을 고치기 전에 이 표에서 연결된 곳을 확인하고, 고친 뒤에는 이 문서도 함께 갱신한다.

## 프로젝트 구성

| 프로젝트 | 역할 | 실행 환경 |
|---|---|---|
| `src/DrawingReview.Engine` | PDF 엔진 경계와 PDFium + PDFsharp 구현. 화면과 무관한 PDF 처리 | Windows·Linux |
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
| 안전 저장 (임시 파일 → 검증 → 교체) | `Persistence/SafeFileSaver.cs` | — | `PdfiumDocument.SaveTo`, (향후) 프로젝트 데이터 저장 |
| 엔진 입구: 열기(메모리로 읽어 파일 잠금 없음), 그림(PNG·JPG·TIFF·BMP)→PDF, 벡터 PDF 쓰기 | `Pdfium/PdfiumEngine.cs` | `PdfiumDocument`, `ImagePdfBuilder`, `VectorPdfWriter` | 앱의 문서 열기, 웹 비교 |
| PDFium 직접 호출 선언 (그리기·글자·검색·책갈피·암호 해제 사본) + 전역 잠금 | `Pdfium/PdfiumNative.cs` | bblanchon.PDFium 바이너리 | `PdfiumView` |
| 읽기 뷰: 페이지 수·회전·CropBox, 그리기, 단어(가로·세로 이어 붙임), 검색, 책갈피, 라벨 | `Pdfium/PdfiumView.cs` | `PdfiumNative`, `PageGeometry` | `PdfiumDocument` |
| 문서 조작 (주석 추가·조회·수정·삭제, 상태·잠금, 증분 저장, 평탄화, 암호 문서 편집·재암호화, 보안 정보) | `Pdfium/PdfiumDocument.cs` | PDFsharp, `AnnotationWriter/Reader`, `ChangeTracker`, `IncrementalWriter`, `Flattener`, `SafeFileSaver` | `IPdfDocument` 사용처 전체 |
| 보이는 좌표 ↔ PDF 좌표 변환 (회전·CropBox 보정) | `Pdf/PageGeometry.cs` | — | Writer, Reader, View |
| 마크업 → 표준 PDF 주석 사전 (/Rect·/L·/Vertices·/InkList·/CL·/BS·/DRKind…) | `Pdf/AnnotationWriter.cs` | `AppearanceBuilder`, `StandardFonts` | `PdfiumDocument.AddMarkup` |
| 주석 외형(/AP) 내용 스트림 생성 (구름 호, 화살촉, 글 줄바꿈, 반투명) | `Pdf/AppearanceBuilder.cs`, `Pdf/TextLayout.cs` | `StandardFonts` | `AnnotationWriter` |
| PDF 주석 → `MarkupInfo` (종류 판별, 상태·잠금 읽기) | `Pdf/AnnotationReader.cs` | `PageGeometry` | `PdfiumDocument.GetMarkups` |
| 바뀐 객체 기억 → 파일 끝에 증분으로 덧붙이기 (xref 표/스트림) | `Pdf/ChangeTracker.cs`, `Pdf/IncrementalWriter.cs` | PDFsharp | `PdfiumDocument.Sync` |
| 주석 평탄화 (외형을 페이지 내용으로 녹여 넣기) | `Pdf/Flattener.cs` | — | `ExportFlattened` |
| 그림 → 한 장 PDF (JPEG 원본 넣기, 그 밖은 Flate RGB, DPI 읽기) | `Pdf/ImagePdfBuilder.cs`, `Pdf/PngEncoder.cs` | OpenCvSharp | `OpenImage` |
| 벡터 페이지 → 새 PDF (`IPdfEngine.WriteVectorPdf`) | `Pdf/VectorPdfWriter.cs` | `StandardFonts`, `SafeFileSaver` | `CadComparison` |
| 표준 글꼴: Helvetica(폭 표) + 한글 Adobe-Korea1 CID 글꼴 참조 (글꼴 파일 미포함) | `Pdf/StandardFonts.cs` | — | `AppearanceBuilder`, `VectorPdfWriter` |

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
| 리비전 표시: 하이라이트 · Cloud + △ 번호(Cloud에만, 글자 변경도 Cloud 설정 가능), 사용자 수정(번호·Cloud·△·하이라이트 위치) 반영 | `RevisionMarks.cs` | `MarkupDraft`, `IPdfDocument.AddMarkup` | 웹 비교 (예정), 테스트 |

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
| API: `/api/files`(올리기·정보·닫기), `/files/{id}/pages/{n}.png`, `/compare`, `/compare/{id}/pages/{i}/removed.png`, `/overlay.png`(색 구분 겹쳐 보기), `/compare/{id}/export`(번호·수정·글자 Cloud 반영) | `Api/Endpoints.cs` | `PageComparer.RenderRemovedLayer`, `RevisionMarks.Apply` | 웹 화면 `api.ts` |
| 화면으로 보내는 자료 모양 | `Api/Dtos.cs` | — | `Endpoints`, 테스트 |
| 웹 모드 (`Hosting__Mode=Web`: 0.0.0.0:`PORT`, 접속자 쿠키 `sdt_owner`로 파일·비교 나누기, 30분 안 쓰면 삭제, 동시 비교 2개, 업로드 100MB·전체 1.5GB(가득 차면 503), 보안 머리글, `/api/health`) | `Services/Hosting.cs`, `Program.cs` | `DocumentStore`·`CompareStore`의 owner | Docker 이미지 |
| Docker 이미지 (웹 빌드 + 서버 + OpenCV 시스템 부품, 일반 사용자로 실행) | `Dockerfile`, `.dockerignore` | `npm run build:web` | 호스팅 (W0) |

## 웹 화면 (`web/src`)

| 기능 | 담당 파일 | 사용하는 공통 기능 | 주요 호출 위치 |
|---|---|---|---|
| 앱 시작 (토큰·글꼴·CSS 불러오기), `#keytest`이면 키 시험 화면 | `app/main.tsx`, `app/App.tsx` | `design/tokens.css` | `index.html` |
| 공용 상태 보관소 (문서 탭 열기·닫기·전환·이름, 탭별 리본 탭·저장 안 됨, 상태 토글, 명령창 기록·입력·질문·이전 입력, 패널·명령창 숨김, 명령 목록 창) | `app/store.ts` (Zustand) | — | 공통 틀 전체 |
| 키보드 분배: 글자 → 명령창(입력칸·대화상자에 커서가 있으면 그 칸), F1·F2·F3·F7·F8·F9·Ctrl+0·Ctrl+9·Ctrl+O·Ctrl+E, 입력칸 밖에서 Ctrl+Z·Ctrl+Y·PgUp·PgDn(그 탭에 없는 명령이면 브라우저 기본), 빈 곳 Enter·Space = 실행, Esc = 취소 | `app/keyboard.ts`, `shell/useGlobalKeys.ts`, `shell/cmdlineFocus.ts` | `commands/runner` | `AppShell` |
| 명령 등록표 (1단계 명령 전부 + 2단계 3개, 별칭·단축키·쓸 수 있는 탭·갈래) | `commands/registry.ts` | — | 리본, 명령창, 명령 목록 창 |
| 명령 찾기·자동완성 순서·오타 제안(글자 차이 2 이하) | `commands/lookup.ts` | — | 명령창, runner |
| 입력 해석: 좌표 `100,200`·`@100,50`·`@500<90`·거리, 오스냅 한 번(END…NEA), 옵션 앞글자, 숫자 | `commands/parseInput.ts` | — | runner (U7a부터 점 질문) |
| 명령 실행: 질문(옵션·숫자·글자·점·고르기)과 답, 틀린 답은 빨간 줄 + 다시 묻기, Esc 취소, 빈 Enter 반복, 쓸 수 없는 탭·등록만 된 명령·2단계 안내, 버튼 = 명령(지금 명령 취소), 투명 명령(GRID 등) | `commands/runner.ts` | `commands/builtins`, `lookup`, `parseInput` | 명령창, 리본, 제목줄, 시작 화면, 상태 표시줄, 단축키 |
| 동작하는 명령: `?`/HELP, GRID, ORTHO, SNAP, PANELS, CMDLINE, TABNEXT, TABPREV, CMP, PNG, EXIT(저장 안 됐으면 명령창에서 `[Yes/No]`) + 비교 명령(`compare/commands.ts`를 합침) | `commands/builtins.ts` | `app/store`, `compare/commands` | runner |
| 시작 화면에 끌어다 놓은 파일 → 열 탭 정하기 | `app/dropRouting.ts` | — | `shell/HomeScreen` |
| 글꼴 앱 안에 넣기 (Pretendard·JetBrains Mono, 인터넷 없이) | `app/fonts.css` | npm `pretendard`, `@fontsource/jetbrains-mono` | `main.tsx` |
| 옛 PNG 화면 전용 밝은 스타일 (U4에서 지움) | `app/legacy.css` | — | `shell/AppShell` |
| 브라우저 키 시험 화면 (위험 시험 R7, 주소 끝 `#keytest`) | `app/KeyTest.tsx` | `i18n` | `App` |
| 화면 글자 번역 `t()` (영어 기준, 빠진 키는 영어, 1개/여러 개) | `i18n/index.ts`, `i18n/en.ts`, `i18n/ko.ts` | `app/store` (언어) | 새 화면 전체 |
| 공통 틀: 제목줄·리본·탭 내용·명령창·상태 표시줄, 저장 안 된 탭이 있으면 창 닫기 전 확인, 칸 밖에 떨어뜨린 파일 무시, 비교 탭을 닫으면 그 비교 정리 | `shell/AppShell.tsx` | 아래 부품 | `App` |
| 제목줄 (앱 이름, 문서 탭 열기·닫기·가운데 버튼 닫기, ●, ↶↷, Focus) | `shell/TitleBar.tsx` | `ui/IconButton`, `app/store` | `AppShell` |
| 리본 탭 6개 + 그룹·버튼 (설계서 2-4 표). 지금 탭을 다시 누르면 접고 펼침, 옛 화면(PNG) 탭은 접힌 채 시작, 비교 탭은 눌림(SBS·OV·SYNC)·흐림(결과 전 Next 등) | `shell/Ribbon.tsx`, `shell/ribbonLayout.ts` | `ui/RibbonButton` | `AppShell` (Home 탭 제외) |
| 명령창 (기록 2줄 + 입력 줄, 질문과 눌러서 고르는 [옵션], ↑↓ 이전 입력, Tab 완성·제안 받기, F2 기록 펼치기. 옛 화면 탭은 입력 줄만 + 마지막 기록) | `shell/CommandLine.tsx` | `commands/runner`, `ui/IconButton` | `AppShell` |
| 명령 자동완성 목록 (시안 04: 이름·별칭·설명·단축키, 최대 8줄) | `shell/Autocomplete.tsx` | `commands/lookup` | `CommandLine` |
| 명령 목록 창 (`?`·F1: 갈래별, 이 탭에서 못 씀·나중·Phase 2 표시, 거르기 칸, 누르면 실행) | `shell/CommandListDialog.tsx` | `ui/Dialog`, `commands/registry` | `AppShell` |
| 상태 표시줄 (상태, GRID·SNAP·ORTHO·OSNAP·DARK VIEW, 비교 탭: 페이지의 변경 수·못 연 파일·Sync pan·페이지·확대 비율, 서버 주소, 언어) | `shell/StatusBar.tsx` | `ui/StatusToggle` | `AppShell` |
| 시작 화면 (왼쪽 메뉴, **기능 카드 24개: 검색·갈래 고르기**, 최근 목록 자리, 창 어디나 끌어다 놓기). Ready 카드 = 그 기능 명령, 준비 중 카드 = 기능 안내 탭 | `shell/HomeScreen.tsx` | `apps/registry`, `app/dropRouting`, `commands/runner` | `AppShell` |
| 기능 등록표 (24개: 갈래·상태·화면 종류·명령·계획 단계) + 도구 찾기 | `apps/registry.ts` | `i18n` (`app.<id>.name/desc`) | 시작 화면, 기능 안내 탭 |
| 기능 안내 탭 (준비 중 기능: 설명·사용 흐름·계획, 같은 기능은 탭 하나) | `apps/AppInfoTab.tsx` | `apps/registry`, `app/store` (`openApp`) | `AppShell` (탭 종류 `app`) |
| 탭 종류별 리본 탭 (`RIBBON_TABS_FOR`), 그 탭에서 못 쓰는 명령의 리본 버튼은 흐리게 | `app/store.ts`, `shell/Ribbon.tsx` | `commands/registry` (`where`) | 공통 틀 |
| PC/웹 빌드 표시 (`VITE_TARGET`, `npm run build:web` → `web-dist/`) | `app/target.ts`, `web/.env.web` | — | 시작 화면 (Runs on this PC only / Web version) |
| 명령이 없는 버튼(시작 화면 Tool sets) → 명령창에 "나중에" 안내. 명령이 있는 버튼은 모두 `runCommand` | `shell/notYet.ts` | `app/store` (기록) | 시작 화면 |
| 새 기본 부품 (토큰만 사용): 아이콘 버튼, 리본 버튼("Phase 2" 꼬리표), 상태 토글, 대화상자(Esc 닫기, Tab이 상자 안에서만, 닫으면 커서 원위치) | `ui/IconButton.tsx`, `ui/RibbonButton.tsx`, `ui/StatusToggle.tsx`, `ui/Dialog.tsx` | lucide-react | 공통 틀 |
| PDF에 넣는 마크업 색 (tokens.css와 같은지 테스트) | `doc/palette.ts` | — | (U7부터) 저장 |
| 색 하드코딩 검사·마크업 색 비교 (테스트) | `app/tokens.test.ts` | `design/tokens.css` | `npm test` |
| 옛 위쪽 도구 막대·아이콘 버튼 (PNG 화면 안, U4에서 지움) | `ui/AppHeader.tsx` | lucide-react | 두 화면 |
| 서버 호출·자료 모양·파일 저장 | `api.ts` | — | 비교 화면, PNG 화면(저장) |
| 비교 탭 (설계서 2-2·2-3, 시안 02~05·11): 왼쪽 Pages · 원본/리비전 칸(머리 줄: 이름·페이지·크기·× 닫기) 또는 겹쳐 보기 · 아래 변경 표 · 오른쪽 패널, 떠 있는 페이지·확대 막대, 빠진 페이지 안내, 칸마다 파일 놓기·못 여는 파일 카드, 비교 실패 카드(Try again), 경고 알림, 탭 이름 `Compare · 원본 ↔ 리비전`, 저장 안 된 일이 있으면 탭 ●. 키: Delete = 고른 변경 빼기, Esc = 비교 취소 또는 고르기 해제, 겹쳐 보기 미세 조정 중 화살표(Shift 10배) | `compare/CompareTab.tsx` | 아래 부품들, `viewer/PageViewer` | `shell/AppShell` (비교 탭마다 하나) |
| 비교 상태 보관소 (탭마다: 두 파일, 결과, 설정, 보기 방식, 겹쳐 보기, 표시 수정·뺀 변경·검토됨 + 되돌리기·다시하기 기록, 고른 변경, 표 거르기) + 계산(△ 번호, 다음·이전 변경, 페이지 배지, CSV, 저장 안 된 일) | `compare/state.ts` (Zustand) | `doc/numbering`, `doc/geometry` | 비교 탭 부품 전체, 비교 명령 |
| 비교 탭에서 하는 일: 파일 열기(올리는 사이 탭을 닫으면 서버 파일도 닫음), 비교 시작·취소(서버 작업도 멈춤), 칸 닫기, 탭 닫기 정리, 바꾸기, 설정(제목란 무시는 다시 비교), 변경 고르기·다음·이전, 내보내기 넣기·빼기, 검토됨, PDF·CSV 내려받기, 탭 이름 | `compare/actions.ts` | `api.ts`, `app/store` (기록) | 비교 탭 부품, 비교 명령 |
| 비교 명령 (설계서 6-6): SWAP, SBS, OVERLAY(OV), SYNC, NEXT(N), PREV(PV), REVIEWED(RV, 다음으로), EXCLUDE(X), EXPORT, EXPORTCSV(EXC), NUDGE, NUMBERING(NUM, `[Each/Same]` + 시작 번호), 비교 탭의 OPEN·ZOOM(`[Extents/Window/In/Out]`, Window는 나중)·PAGE(번호 또는 `[Next/Previous]`)·UNDO·REDO. 리본 눌림·흐림 상태 | `compare/commands.ts` | `compare/actions`, `compare/state` | `commands/builtins` (합침), 리본 |
| 파일 고르기 창 (빈 칸 또는 정한 칸) | `compare/filePicker.ts` | `compare/actions` | OPEN, Browse files, Choose another file |
| 파일 놓는 칸 (시안 02: 끌어다 놓기, Browse files, 두 파일을 한 번에 놓으면 양쪽) | `compare/FileDrop.tsx` | `ui/Button` | 비교 탭 |
| 왼쪽 패널: Pages(페이지 그림 + 변경 수·변경 없음·추가·빠짐 배지), Bookmarks·Layers는 "나중" 안내 | `compare/PagesPanel.tsx` | `ui/Badge`, `ui/EmptyState` | 비교 탭 |
| 변경 표 (시안 04: 지금 페이지, 체크 = 내보내기에 넣기, △·종류·설명·일치도·상태, All/Text/Graphic 거르기, 접기, 누르면 그 자리로) | `compare/ChangeTable.tsx` | `ui/Table`, `ui/Badge` | 비교 탭 |
| 오른쪽 패널: 비교 전 = Compare settings(시작 번호·번호 방식·글자 구름·제목란 무시·지워진 선 + Compare 버튼), 결과 = 요약(차이 없음 안내) + 설정, 변경을 고르면 = Change(상태·△ 번호·내보내기 넣기·되돌리기), 겹쳐 보기 = Overlay(색 구분·투명도·구름 보이기·정렬·미세 조정) | `compare/RightPanel.tsx` | `ui/Toggle`, `ui/Button` | 비교 탭 |
| 비교 중 창 (시안 03: 움직이는 막대, 지난 초, 단계, Cancel·Esc) | `compare/ProgressDialog.tsx` | `ui/Button` | 비교 탭 |
| 표시 그리기·수정 (하이라이트·구름·△, 누르면 선택, 끌어 옮기기, 모서리로 크기 조절, △ 따로 옮기기, 끌기 한 번 = 되돌리기 한 번) | `compare/RevisionMarks.tsx` | `doc/geometry`, `doc/palette` | 비교 탭 |
| 좌우 칸과 구분선 (끌어서 크기 조절, 두 번 누르면 반반, ← →) | `compare/SplitPanes.tsx` | — | 비교 탭 |
| 도면 한 페이지 보기 (휠 확대, 끌어 이동, 해상도 단계 미리 받기, 겹침 층: 지워진 선·겹쳐 보기 원본 투명도·미세 조정 이동) | `viewer/PageViewer.tsx` | `viewer/view.ts` | 비교 탭 (U6부터 도면 탭) |
| 보기 계산 (맞춤, 커서 기준 확대, 변경 위치로 이동, 그림 배율 단계) | `viewer/view.ts` | — | `PageViewer`, 비교 탭 |
| △ 번호 매기기 (1,2,3… / A,B,C… / 같은 번호) | `doc/numbering.ts` | — | 비교 상태 (화면·내려받기 공통) |
| 구름 경로·△ 위치·설명 줄이기 (엔진 `RevisionMarks`와 같은 규칙) | `doc/geometry.ts` | — | `RevisionMarks`, `ChangeTable` |
| 새 기본 부품 (U3): 버튼(primary·default·ghost), 켜고 끄기 줄, 배지, 빈 상태·오류 카드, 알림, 표 | `ui/Button.tsx`, `ui/Toggle.tsx`, `ui/Badge.tsx`, `ui/EmptyState.tsx`, `ui/Toast.tsx`, `ui/Table.tsx` | lucide-react | 비교 탭 (이후 모든 탭) |
| 옛 파일 열기 칸 (PNG 화면 안, U4에서 지움) | `compare/DropZone.tsx` | — | PNG 화면 |
| 둘 중 하나 고르기 버튼 묶음 (PNG 화면 안, U4에서 지움) | `ui/Segmented.tsx` | — | PNG 화면 |
| 투명 PNG 화면 (슬라이더, 배경 색 자동·찍기, 부드럽게, 여백 자르기, 선 색 맞추기, 내려받기). PNG 탭 안에서 옛 디자인 그대로 | `png/PngTool.tsx` | `png/removeBackground.ts`, `DropZone` | `shell/AppShell` (PNG 탭마다 하나) |
| 배경 지우기 계산 (브라우저 안에서만) | `png/removeBackground.ts` | — | `PngTool` |
