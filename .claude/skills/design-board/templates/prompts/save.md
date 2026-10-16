확정한 {안 이름} 디자인을 개발 단계에서 쓸 수 있게 프로젝트 파일로 저장해 줘.

먼저 읽을 파일: design.md, design/README.md, 그리고 확정한 최종 캔버스 ({캔버스 링크})
틀 파일: .claude/skills/design-board/templates/brand.md, .claude/skills/design-board/templates/tokens.css

저장할 파일:
1. design/tokens.css — 틀의 모든 항목(색, 글자, 간격, 모서리, 그림자, 테두리, 모션, 화면 크기 기준, 레이어)을 확정 안의 실제 값으로 채운다. 다크 모드를 쓰지 않으면 그 부분은 주석으로 "쓰지 않음"이라고 적는다.
2. design/brand.md — 틀을 채운다: 분위기, 색 역할, 글꼴, 아이콘, 이미지·모션 규칙, 하지 말 것.
3. design/screens/{화면이름}.html — 최종 화면마다 1개. 각 파일은 ../tokens.css를 불러 쓰고 브라우저로 바로 열려야 한다. 색·간격은 토큰 변수만 쓴다.
4. design/README.md — "확정" 부분에 고른 안, 캔버스 링크, 저장한 파일 목록을 적는다.

앱 코드는 만들지 않는다. 다 저장하면 파일 목록을 알려 줘.
