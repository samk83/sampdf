# design.md — UI/UX Design & Development Rules

## 1. 기본 설계 철학 (Design Philosophy)

모든 앱은 기능의 복잡성보다 사용자의 이해와 사용 편의성을 우선한다.

**핵심 원칙**
- Simple First: 누구나 쉽게 이해하고 사용할 수 있어야 한다.
- UI First: 주요 기능을 구현하기 전에 화면 구조와 사용자 흐름을 설계한다.
- Minimal Interaction: 불필요한 클릭, 입력, 단계, 기억 부담을 최소화한다.
- Consistent Experience: 모든 화면에서 일관된 디자인 언어를 사용한다.
- Visual Feedback: 사용자 행동에 대한 결과를 명확하게 보여준다.
- Reusable Design: 한번 만든 디자인 컴포넌트는 다른 화면에서도 재사용한다.
- Progressive Disclosure: 복잡한 기능은 필요한 순간에만 보여준다.

디자인은 무조건 화려하게 만들지 않는다. 목적에 맞는 심플하고 완성도 높은 인터페이스를 추구한다.

---

## 2. UI 우선 개발 프로세스

신규 앱이나 기능 개발 시 다음 순서를 따른다.

### Step 1. 화면 및 기능 기획
- 앱의 목적과 핵심 사용자 정의
- 주요 기능 및 우선순위 정리
- 화면 구성 및 정보 구조 설계
- 사용자 흐름(User Flow) 작성
- 모바일 및 데스크톱 사용 환경 검토

### Step 2. 디자인 설계
- 디자인 방향 및 레퍼런스 검토
- 화면 레이아웃 설계
- 컬러, 타이포그래피, 간격 정의
- 컴포넌트 구성
- 인터랙션 및 애니메이션 정의

### Step 3. UI 프로토타입
- 가능하면 실제 화면으로 확인할 수 있는 UI Playground 구성
- 주요 화면 및 인터랙션 시각화
- 실제 데이터와 예시 데이터 상태 검토
- 구현 전에 사용성 문제 검토

### Step 4. 기능 구현
- 확정된 디자인 시스템 재사용
- 재사용 가능한 컴포넌트 단위 개발
- 기존 기능에 미치는 영향 확인
- 반응형 및 접근성 기준 준수

### Step 5. 테스트 및 개선
- 기능 테스트
- UI 시각적 검토
- 사용자 흐름 테스트
- 오류 및 예외 상태 테스트
- 문제점 기록 및 개선

간단한 기능에는 이 과정을 경량화하되 핵심 검증은 생략하지 않는다.

---

## 3. Design System

디자인 시스템의 기본 정의는 `design.md`에서 관리한다.

실제 구현은 CSS Variables 기반의 Design Tokens를 사용한다.

### 필수 디자인 토큰
- Color: 기본색, 강조색, 배경색, 텍스트색, 상태색
- Typography: 폰트, 크기, 굵기, 줄 간격
- Spacing: 여백 및 요소 간 거리
- Radius: 모서리 곡률
- Shadow: 그림자 및 깊이 표현
- Border: 테두리 스타일
- Motion: 애니메이션 시간 및 움직임
- Breakpoint: 화면 크기별 반응형 기준
- Z-index: 레이어 우선순위

### CSS Variables 예시

```css
:root {
  --color-primary: #2563eb;
  --color-background: #ffffff;
  --color-surface: #f8fafc;
  --color-text: #111827;
  --color-border: #e5e7eb;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-6: 24px;

  --radius-sm: 6px;
  --radius-md: 10px;
  --radius-lg: 16px;

  --motion-fast: 150ms;
  --motion-normal: 250ms;
}
```

위 값은 시작 예시이며, 실제 앱의 디자인 방향에 맞게 수정한다.

### 디자인 시스템 관리 원칙
- 컴포넌트에 색상과 간격을 무분별하게 하드코딩하지 않는다.
- 공통 디자인은 토큰을 사용한다.
- Dark Mode 및 테마 확장을 고려한다.
- 기존 컴포넌트를 먼저 확인하고 재사용한다.
- 공통 컴포넌트 수정 시 다른 화면에 미치는 영향을 확인한다.
- 프로젝트별 브랜드 디자인과 공통 디자인 시스템을 분리한다.
  - 공통 규칙: 이 파일(`design.md`). 하네스에서 복사해 온 원본을 그대로 쓴다.
  - 프로젝트 브랜드: `design/brand.md`(브랜드 규칙)와 `design/tokens.css`(실제 토큰 값). 시안 확정 때 만든다.

---

## 4. 디자인 참고 자료 및 도구

다음 자료들은 디자인 참고 및 개발 도구로 활용한다.

| 도구 | 용도 |
|---|---|
| Mobbin | 실제 앱 UI, 화면 구성, 사용자 흐름 참고 |
| keyframe.gallery | 모션 그래픽, UI 애니메이션 참고 |
| Higgsfield | 영상, 시각 효과 및 비주얼 콘셉트 참고 |
| Lucide | 일관된 UI 아이콘 |
| Apple Human Interface Guidelines | 모바일 UI/UX 설계 참고 |
| Google Material Design | 컴포넌트 및 인터랙션 참고 |
| Microsoft Fluent Design | 데스크톱 UI/UX 참고 |

### 활용 규칙
- 참고 디자인은 그대로 복제하지 않고 프로젝트에 맞게 재해석한다.
- 여러 디자인 스타일을 무분별하게 혼합하지 않는다.
- 아이콘은 기본적으로 Lucide를 우선 사용한다.
- 앱 고유의 브랜드 아이콘이나 특별한 시각 요소는 별도로 제작할 수 있다.
- 외부 이미지, 아이콘, 템플릿 및 영상의 라이선스를 확인한다.
- 참고 웹사이트나 디자인 파일을 직접 확인할 수 없는 경우 임의로 내용을 추측하지 않는다.

### 필요한 도구·자료·로그인은 작업할 때 요청한다
- 작업 중 도구나 자료가 필요하면 추측하거나 조용히 건너뛰지 말고 Owner에게 요청한다.
  요청에는 **무엇이 필요한지 / 왜 필요한지 / 어떻게 주면 되는지**(캡처 붙여넣기, 파일 올리기, 링크)를 함께 적는다.
- 로그인이 필요한 사이트(예: Mobbin)는 기본적으로 **Owner에게 로그인(또는 필요한 화면 캡처)을 요청한다.**
  클라우드 세션의 Claude는 Owner의 로그인 상태를 쓸 수 없으므로 보통 캡처를 받는다.
  PC의 Claude 데스크톱 앱 + Chrome 연결을 쓰는 경우에는 Owner가 로그인한 브라우저를 직접 볼 수 있다.
- Owner가 안 된다고 하면 그 자료 없이 넘어가고, 무엇이 빠졌는지 결과에 적는다.
- 자료를 기다리는 동안에는 그 자료 없이 할 수 있는 부분만 진행한다.

### 무료 참고 자료·오픈소스 (2026-10-15 확인)
✅ 자동 접속 확인 · ⚠️ 사이트는 있으나 자동 접속이 막혀 있음(사람 브라우저로는 열림)

| 분류 | 자료 | 메모 |
|---|---|---|
| 아이콘 | ✅ [Lucide](https://lucide.dev) | 기본. CDN 예: `https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/house.svg` |
| 아이콘 | ✅ [Tabler Icons](https://tabler.io/icons) · ✅ [Phosphor](https://phosphoricons.com) · ✅ [Heroicons](https://heroicons.com) | Lucide에 없을 때 보조 |
| 글꼴 | ✅ [Google Fonts](https://fonts.google.com) | |
| 글꼴(한국어) | Pretendard (저장소 ⚠️) | CDN ✅ `https://cdn.jsdelivr.net/gh/orioncactus/pretendard@v1.3.9/dist/web/static/pretendard.css` |
| 토큰 | ✅ [Open Props](https://open-props.style) | CSS 변수 토큰 모음. 3장 방식과 맞음 |
| 부품(웹) | ✅ [shadcn/ui](https://ui.shadcn.com) · ✅ [Radix UI](https://www.radix-ui.com) | 오픈소스 컴포넌트 |
| 가이드 | ✅ [Material Design 3](https://m3.material.io) · ✅ [Fluent 2](https://fluent2.microsoft.design) · ✅ [Apple HIG](https://developer.apple.com/design/human-interface-guidelines) | |
| 화면 참고 | ✅ [Collect UI](https://collectui.com) · ✅ [Godly](https://godly.website) · ⚠️ [Lapa Ninja](https://www.lapa.ninja) · ⚠️ [Land-book](https://land-book.com) | 무료 열람 |
| 화면 참고 | ✅ [Mobbin](https://mobbin.com) | 대부분 로그인·유료 → 로그인 요청 규칙 |
| 모션 | ✅ [keyframe.gallery](https://www.keyframe.gallery) · ✅ [Higgsfield](https://higgsfield.ai) · ✅ [LottieFiles 무료](https://lottiefiles.com/free-animations) · ✅ [Animista](https://animista.net) · ✅ [Motion](https://motion.dev) | |
| 일러스트·사진 | ✅ [unDraw](https://undraw.co) · ⚠️ Unsplash · ⚠️ Pexels | 라이선스 확인 |
| 품질 기준 | ✅ [NN/g 10 Heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/) · ✅ [WCAG 2.2](https://www.w3.org/TR/WCAG22/) · ✅ [WebAIM 명도 대비 검사](https://webaim.org/resources/contrastchecker/) | |

---

## 5. UX Usability Standards

모든 신규 UI 및 UX 개발은 Nielsen Norman Group의 10가지 사용성 휴리스틱 원칙을 기본 품질 기준으로 적용한다.

1. **Visibility of System Status** — 현재 상태, 로딩, 저장, 처리 결과를 명확하게 보여준다.
2. **Match Between System and Real World** — 사용자가 이해하기 쉬운 용어와 자연스러운 작업 순서를 사용한다.
3. **User Control and Freedom** — 취소, 뒤로 가기, 되돌리기, 종료 기능을 적절히 제공한다.
4. **Consistency and Standards** — 화면별 디자인과 동작 방식을 일관되게 유지한다.
5. **Error Prevention** — 실수하기 쉬운 입력과 위험한 작업을 사전에 예방한다.
6. **Recognition Rather Than Recall** — 사용자가 정보를 외우지 않고도 기능을 발견하고 사용할 수 있도록 한다.
7. **Flexibility and Efficiency of Use** — 초보자는 쉽게, 숙련자는 빠르게 사용할 수 있도록 한다.
8. **Aesthetic and Minimalist Design** — 불필요한 시각적 요소와 정보를 제거한다.
9. **Help Users Recognize, Diagnose, and Recover from Errors** — 오류 원인과 해결 방법을 이해하기 쉽게 안내한다.
10. **Help and Documentation** — 필요한 경우 간단하고 상황에 맞는 도움말을 제공한다.

### UX 개발 규칙
- 구현 전에 관련 UX 원칙을 검토한다.
- 불필요한 클릭과 사용자 입력을 최소화한다.
- 중요한 작업에는 명확한 피드백을 제공한다.
- 파괴적 작업에는 적절한 확인 또는 복구 기능을 제공한다.
- 오류 메시지는 원인과 해결 방법을 포함한다.
- 모든 원칙을 기계적으로 적용하지 않고 실제 사용자 상황과 작업 목적에 맞게 판단한다.

---

## 6. UI Component Standards

UI는 재사용 가능한 컴포넌트 중심으로 개발한다.

### 기본 컴포넌트
- Button
- Input / Textarea
- Select / Dropdown
- Checkbox / Radio
- Modal / Dialog
- Toast / Notification
- Card
- Tabs
- Navigation
- Tooltip
- Table / List
- Progress / Loading
- Empty State
- Error State

### 컴포넌트 개발 규칙
- 동일한 기능의 컴포넌트를 중복 개발하지 않는다.
- 크기, 색상, 상태는 디자인 토큰과 Variant로 관리한다.
- Hover, Focus, Active, Disabled 상태를 정의한다.
- 필요하면 Loading, Success, Error 상태를 제공한다.
- 재사용성과 유지보수성을 우선한다.
- 과도하게 추상화하여 단순한 변경도 어렵게 만들지 않는다.

---

## 7. Responsive Design

웹과 PWA는 모바일, 태블릿, 데스크톱 환경을 고려한다.

- 기본적으로 반응형 레이아웃을 사용한다.
- 화면 크기보다 콘텐츠와 실제 사용 패턴을 기준으로 레이아웃을 조정한다.
- 터치와 마우스 입력을 모두 고려한다.
- 모바일에서 주요 버튼과 조작 요소의 터치 영역을 충분히 확보한다.
- 작은 화면에서 가로 스크롤이나 요소 겹침이 발생하지 않도록 한다.
- 복잡한 데스크톱 화면은 모바일에서 필요한 정보 중심으로 재구성한다.
- 앱의 목적에 따라 Desktop First 또는 Mobile First 전략을 선택한다.

---

## 8. Motion & Interaction Standards

애니메이션은 시각적 장식보다 사용자의 이해와 피드백을 돕는 목적으로 사용한다.

### 참고 자료
- keyframe.gallery
- Higgsfield
- Apple / Material / Fluent Motion Guidelines

### 개발 규칙
- 버튼 클릭, 화면 전환, 상태 변경에 적절한 피드백을 제공한다.
- 일관된 애니메이션 속도와 움직임을 사용한다.
- 과도한 애니메이션은 피한다.
- 로딩 및 처리 중에는 진행 상태를 알린다.
- 시스템의 Reduced Motion 설정을 존중한다.
- 애니메이션으로 앱의 성능과 반응성을 저하시키지 않는다.
- 필수 기능을 이해하는 데 애니메이션이 필요하도록 설계하지 않는다.

---

## 9. Accessibility Standards

접근성은 기본 UI 품질의 일부로 취급한다.

- WCAG 2.2 AA를 목표 기준으로 사용한다.
- 일반 텍스트의 명도 대비는 기본적으로 4.5:1 이상을 목표로 한다.
