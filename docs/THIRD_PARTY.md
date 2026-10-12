# 사용한 오픈소스와 라이선스

이 프로젝트는 개인 로컬 사용 전용이다(기획서 14장). 프로그램을 다른 사람에게 전달하거나 네트워크 서비스로 바꿀 때는 아래 조건을 다시 확인한다.

| 구성 요소 | 버전 | 용도 | 라이선스 | 주소 |
|---|---|---|---|---|
| MuPDF.NET | 3.28.2.4 | PDF 렌더링·주석·저장 엔진 | AGPL-3.0 또는 Artifex 상용 | https://github.com/ArtifexSoftware/MuPDF.NET |
| MuPDF | 1.28.2 (MuPDF.NET에 포함) | PDF 엔진 본체 | AGPL-3.0 또는 Artifex 상용 | https://github.com/ArtifexSoftware/mupdf |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModel 도우미 (속성 알림, 명령) | MIT | https://github.com/CommunityToolkit/dotnet |
| OpenCvSharp4 (+ runtime.win, 테스트용 official.runtime.linux-x64) | 4.13.0.20260627 | 도면 비교: 정렬(ORB, RANSAC, ECC), 이미지 차이 | Apache-2.0 (OpenCV: Apache-2.0) | https://github.com/shimat/opencvsharp |
| ACadSharp | 3.8.0 | DWG·DXF 읽기·쓰기 (CAD 비교, Cloud 사본 저장) | MIT | https://github.com/DomCR/ACadSharp |
| xUnit | 2.5.3 | 자동 테스트 (배포물에 포함되지 않음) | Apache-2.0 | https://github.com/xunit/xunit |

새 구성 요소를 추가하면 이 표에 버전과 라이선스를 함께 기록한다.
