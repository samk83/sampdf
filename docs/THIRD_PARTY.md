# 사용한 오픈소스와 라이선스

이 프로젝트는 개인 로컬 사용 전용이다(기획서 14장). 프로그램을 다른 사람에게 전달하거나 네트워크 서비스로 바꿀 때는 아래 조건을 다시 확인한다.

| 구성 요소 | 버전 | 용도 | 라이선스 | 주소 |
|---|---|---|---|---|
| MuPDF.NET | 3.28.2.4 (버전 올릴 때 `MuPdfUtf8Fix` 재확인) | PDF 렌더링·주석·저장 엔진 | AGPL-3.0 또는 Artifex 상용 | https://github.com/ArtifexSoftware/MuPDF.NET |
| MuPDF | 1.28.2 (MuPDF.NET에 포함) | PDF 엔진 본체 | AGPL-3.0 또는 Artifex 상용 | https://github.com/ArtifexSoftware/mupdf |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModel 도우미 (속성 알림, 명령) | MIT | https://github.com/CommunityToolkit/dotnet |
| OpenCvSharp4 (+ runtime.win, 테스트용 official.runtime.linux-x64) | 4.13.0.20260627 | 도면 비교: 정렬(ORB, RANSAC, ECC), 이미지 차이 | Apache-2.0 (OpenCV: Apache-2.0) | https://github.com/shimat/opencvsharp |
| ACadSharp | 3.8.0 | DWG·DXF 읽기·쓰기 (CAD 비교, Cloud 사본 저장) | MIT | https://github.com/DomCR/ACadSharp |
| React, React DOM | 18.3.1 | 웹 화면 | MIT | https://react.dev |
| lucide-react | 0.460.0 | 웹 화면 아이콘 | ISC | https://lucide.dev |
| Tailwind CSS | 3.4.15 | 웹 화면 스타일 (빌드할 때만) | MIT | https://tailwindcss.com |
| Vite, TypeScript, Vitest | 5.4.11, 5.6.3, 2.1.5 | 웹 화면 빌드·검사 (배포물에 포함되지 않음) | MIT, Apache-2.0, MIT | https://vitejs.dev |
| IBM Plex Sans KR / Mono | — | 웹 화면 글꼴 (Google Fonts에서 불러옴. 인터넷이 없으면 시스템 글꼴) | SIL OFL 1.1 | https://github.com/IBM/plex |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.10 | 웹 서버 자동 테스트 (배포물에 포함되지 않음) | MIT | https://github.com/dotnet/aspnetcore |
| xUnit | 2.5.3 | 자동 테스트 (배포물에 포함되지 않음) | Apache-2.0 | https://github.com/xunit/xunit |

새 구성 요소를 추가하면 이 표에 버전과 라이선스를 함께 기록한다.
