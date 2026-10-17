# 사용한 오픈소스와 라이선스

이 프로젝트의 코드는 비공개(All rights reserved)다. 아래 구성 요소는 모두 **코드 공개 의무가 없는 라이선스**(BSD·MIT·Apache·OFL·ISC)만 골라 썼으므로 PC 배포·웹 서비스·판매에 그대로 쓸 수 있다. 저작권·라이선스 고지는 배포물에 함께 넣는다.

> 2026-10-17: MuPDF.NET(AGPL)을 제거하고 PDFium + PDFsharp로 교체했다 (`docs/DECISIONS.md`). **PDFsharp는 6.2.x까지만 MIT**이고 6.5부터 AGPL이므로 버전을 올리지 않는다.

| 구성 요소 | 버전 | 용도 | 라이선스 | 주소 |
|---|---|---|---|---|
| PDFium (bblanchon.PDFium.Win32 / .Linux) | 157.0.8100 | PDF 그리기·글자·검색·책갈피·암호 확인 (크롬의 PDF 엔진, 직접 P/Invoke) | BSD-3-Clause (PDFium) / 바이너리 묶음 MIT | https://pdfium.googlesource.com/pdfium · https://github.com/bblanchon/pdfium-binaries |
| PDFsharp | **6.2.4 (올리지 말 것: 6.5+는 AGPL)** | PDF 객체 읽기·쓰기(주석 사전·외형·증분 저장·평탄화·암호 걸기), 가상 도면 생성 | MIT | https://github.com/empira/PDFsharp |
| Nanum Gothic (`fonts-nanum`, Docker 이미지에만) | Debian 패키지 | 글꼴이 들어 있지 않은 한글 PDF를 리눅스 서버에서 그릴 때 대체 글꼴 | SIL OFL 1.1 | https://hangeul.naver.com/font |
| CommunityToolkit.Mvvm | 8.4.2 | ViewModel 도우미 (속성 알림, 명령) | MIT | https://github.com/CommunityToolkit/dotnet |
| OpenCvSharp4 (+ runtime.win, 리눅스는 official.runtime.linux-x64) | 4.13.0.20260627 | 도면 비교: 정렬(ORB, RANSAC, ECC), 이미지 차이. 엔진에서 PNG·JPG·TIFF·BMP 디코딩 | Apache-2.0 (OpenCV: Apache-2.0) | https://github.com/shimat/opencvsharp |
| ACadSharp | 3.8.0 | DWG·DXF 읽기·쓰기 (CAD 비교, Cloud 사본 저장) | MIT | https://github.com/DomCR/ACadSharp |
| React, React DOM | 18.3.1 | 웹 화면 | MIT | https://react.dev |
| lucide-react | 0.460.0 | 웹 화면 아이콘 | ISC | https://lucide.dev |
| Tailwind CSS | 3.4.15 | 웹 화면 스타일 (빌드할 때만) | MIT | https://tailwindcss.com |
| Vite, TypeScript, Vitest | 5.4.11, 5.6.3, 2.1.5 | 웹 화면 빌드·검사 (배포물에 포함되지 않음) | MIT, Apache-2.0, MIT | https://vitejs.dev |
| Pretendard | 1.3.9 (npm `pretendard`) | 웹 화면 기본 글꼴 (한국어·영어). 앱 안에 넣음 (400·500·600·700) | SIL OFL 1.1 | https://github.com/orioncactus/pretendard |
| JetBrains Mono | 5.2.8 (npm `@fontsource/jetbrains-mono`) | 명령창·좌표·단축키 글꼴. 앱 안에 넣음 (라틴 400·500·600) | SIL OFL 1.1 | https://github.com/JetBrains/JetBrainsMono |
| Zustand | 5.0.15 | 웹 화면 공용 상태 보관소 | MIT | https://github.com/pmndrs/zustand |
| @types/node | 22.10.2 | 테스트에서 파일 읽기 타입 (배포물에 포함되지 않음) | MIT | https://github.com/DefinitelyTyped/DefinitelyTyped |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.10 | 웹 서버 자동 테스트 (배포물에 포함되지 않음) | MIT | https://github.com/dotnet/aspnetcore |
| xUnit | 2.5.3 | 자동 테스트 (배포물에 포함되지 않음) | Apache-2.0 | https://github.com/xunit/xunit |

새 구성 요소를 추가하면 이 표에 버전과 라이선스를 함께 기록한다.
