# 시안 생성 스크립트

확정 시안(S1 Slate)과 디자인 캔버스 화면을 만드는 Python 스크립트. 앱 코드가 아니라 **디자인 자료**다.

| 파일 | 하는 일 |
|---|---|
| `chrome.py` | 공통 부품(제목줄·리본·패널·명령창·상태 표시줄·대화상자)과 스킨 값(Revu / S1 / S2). S1 값이 `design/tokens.css`의 근거 |
| `screens.py` | 화면 12개 조립 (`SCREENS`). `python3 screens.py FCompare S1`은 캔버스용 `.dc.html`을 `./out/`에 만든다 |
| `sheet_svg.py` | 예시 도면 S-101을 SVG로 그림 (캔버스의 "Sheet" 부품과 같은 그림) |
| `export_static.py` | `design/screens/*.html`(브라우저에서 바로 열림, `../tokens.css` 사용)과 `screens/assets/*.svg`를 만든다 |

다시 만들기 (저장소 맨 위에서):
```bash
python3 design/source/export_static.py
```

화면을 고칠 때는 `design/screens/*.html`을 직접 고치지 말고 `screens.py`를 고친 뒤 다시 실행한다. 색 값을 바꾸면 `design/tokens.css`도 같이 바꾼다.
캔버스(원본 시안): https://claude.ai/artifact/9nRg4v9evkLkrJkiqtD9XE
