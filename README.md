# Drawing Review PDF (개인용)

Bluebeam Revu에 익숙한 흐름으로 쓰는 **Windows용 PDF 도면 검토 프로그램**. 개인 실험·사용 전용이며 외부 배포는 하지 않는다. Takeoff(물량 산출)는 범위에서 제외한다.

- 기술: C# / .NET 8, WPF(화면, 1단계부터), MuPDF.NET(PDF 엔진)
- 진행 상황: 0단계 엔진 검증, 1단계 화면 뼈대, 2단계 P0 대부분, **도면 비교 MVP(PDF)** 구현, **DWG/DXF 비교**(치수 포함, 비교 창 연결, DXF/DWG Cloud 내보내기) 구현. Windows 실행 확인 대기. 화면 시안: https://claude.ai/artifact/NRj4xhpQiv7qZ8ynkrACbw
- 0단계 결과 → 결과는 [docs/engine-validation.md](docs/engine-validation.md)
- 기능 지도: [docs/FEATURES.md](docs/FEATURES.md) · 변경 이력: [docs/CHANGELOG.md](docs/CHANGELOG.md) · 라이선스: [docs/THIRD_PARTY.md](docs/THIRD_PARTY.md)

## 폴더 구조

```
src/DrawingReview.Engine/      PDF 엔진 경계 + MuPDF 구현 (화면 없음)
src/DrawingReview.App.Core/    화면 상태·로직 (WPF 비의존, 테스트 가능)
src/DrawingReview.App/         WPF 프로그램 (Windows 전용)
src/DrawingReview.Compare/     도면 비교 엔진 (OpenCV)
src/DrawingReview.Cad/         DWG/DXF 비교·그리기·Cloud 내보내기
tests/DrawingReview.Engine.Tests/  자동 테스트
tests/DrawingReview.TestData/  가상 도면 생성기
tools/EngineSpike/             0단계 검증 실행기
samples/virtual/               가상 도면 PDF
validation/output/             검증 결과 PDF, 미리보기 PNG, 보고서
docs/                          설계·검증·기능 문서
```

## Windows에서 실행해 보기


1. **.NET 8 SDK 설치** (C# 프로그램을 만들고 실행하는 도구)
   - https://dotnet.microsoft.com/download/dotnet/8.0 → "SDK" → Windows x64 설치 파일 실행
   - 확인: 시작 메뉴에서 "명령 프롬프트"를 열고 아래 명령을 입력해 `8.0.xxx`가 보이면 정상
     ```
     dotnet --version
     ```
2. **코드 내려받기** (Git 설치 필요: https://git-scm.com/download/win)
   ```
   git clone https://github.com/samk83/sampdf.git
   cd sampdf
   git checkout claude/gracious-cori-qj4ytn
   ```
3. **자동 테스트 실행**: 마지막 줄에 `Passed!`가 보이면 정상
   ```
   dotnet test
   ```
4. **프로그램 실행**: 어두운 창이 뜨면 정상. `Ctrl+O`로 `samples/virtual/plan-set.pdf`를 열어 본다.
   ```
   dotnet run --project src/DrawingReview.App
   ```
5. **검증 결과 다시 만들기** (선택): `validation/output` 폴더의 파일이 새로 만들어진다
   ```
   dotnet run --project tools/EngineSpike
   ```
6. `validation/output/markup-roundtrip.pdf`를 Acrobat Reader와 Bluebeam에서 열어
   [docs/engine-validation.md의 체크리스트](docs/engine-validation.md#sam이-직접-확인할-항목-windows)를 확인한다.
7. **PDF 비교 시험 파일**: `samples/compare/original.pdf`(원본)와 `revision.pdf`(리비전). 그림 비교용 `original.png`, `revision.png`도 있다. Compare → Compare Drawings…에서 차례로 고른다.
8. **CAD 비교 해 보기** (선택): 메뉴 Compare → Compare CAD Drawings (DWG/DXF)… 에서 이전·개정 DWG(또는 DXF)를 차례로 고른다.
   변경 목록에 설명이 보이면 정상. 변경을 수락(Accept)한 뒤 "Export clouds to DXF/DWG…"로 사본을 만들고 AutoCAD에서 REVISION 도면층을 확인한다.
