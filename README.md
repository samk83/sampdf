# Drawing Review PDF (개인용)

Bluebeam Revu에 익숙한 흐름으로 쓰는 **Windows용 PDF 도면 검토 프로그램**. 개인 실험·사용 전용이며 외부 배포는 하지 않는다. Takeoff(물량 산출)는 범위에서 제외한다.

- 기술: C# / .NET 8, WPF(화면, 1단계부터), MuPDF.NET(PDF 엔진)
- 진행 상황: **0단계 엔진 검증 완료(잠정)** → 결과는 [docs/engine-validation.md](docs/engine-validation.md)
- 기능 지도: [docs/FEATURES.md](docs/FEATURES.md) · 변경 이력: [docs/CHANGELOG.md](docs/CHANGELOG.md) · 라이선스: [docs/THIRD_PARTY.md](docs/THIRD_PARTY.md)

## 폴더 구조

```
src/DrawingReview.Engine/      PDF 엔진 경계 + MuPDF 구현 (화면 없음)
tests/DrawingReview.Engine.Tests/  자동 테스트
tests/DrawingReview.TestData/  가상 도면 생성기
tools/EngineSpike/             0단계 검증 실행기
samples/virtual/               가상 도면 PDF
validation/output/             검증 결과 PDF, 미리보기 PNG, 보고서
docs/                          설계·검증·기능 문서
```

## Windows에서 실행해 보기 (0단계)

> 화면이 있는 프로그램은 1단계에서 만든다. 지금은 자동 테스트와 검증 결과 파일을 확인하는 단계다.

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
4. **검증 결과 다시 만들기** (선택): `validation/output` 폴더의 파일이 새로 만들어진다
   ```
   dotnet run --project tools/EngineSpike
   ```
5. `validation/output/markup-roundtrip.pdf`를 Acrobat Reader와 Bluebeam에서 열어
   [docs/engine-validation.md의 체크리스트](docs/engine-validation.md#sam이-직접-확인할-항목-windows)를 확인한다.
