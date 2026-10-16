/** @type {import('tailwindcss').Config} */
// 색·간격·글자 크기는 모두 design/tokens.css 변수로 연결한다 (설계서 4-4). 값을 여기 직접 쓰지 않는다.
const v = (name) => `var(--${name})`;
const scale = (prefix, keys) => Object.fromEntries(keys.map((k) => [k, v(`${prefix}-${k}`)]));

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        background: v('color-background'),
        surface: v('color-surface'),
        chrome: v('color-chrome'),
        canvas: v('color-canvas'),
        sunken: v('color-surface-sunken'),
        pressed: v('color-surface-pressed'),
        primary: { DEFAULT: v('color-primary'), deep: v('color-primary-deep') },
        'on-primary': v('color-on-primary'),
        accent: { DEFAULT: v('color-accent'), subtle: v('color-accent-subtle') },
        'on-accent': v('color-on-accent'),
        fg: {
          DEFAULT: v('color-text'),
          secondary: v('color-text-secondary'),
          muted: v('color-text-muted'),
          faint: v('color-text-faint'),
        },
        success: v('color-success'),
        warning: v('color-warning'),
        danger: v('color-danger'),
        'cmdline-float': v('color-cmdline-float'),
        // 옛 화면(비교·PNG) 전용. U3·U4에서 그 화면을 새 디자인으로 옮기면 지운다.
        legacy: {
          ink: '#16181d',
          muted: '#5b6170',
          line: '#e3e5ea',
          canvas: '#e9ebef',
          accent: '#1d4ed8',
          'accent-soft': '#e0ecff',
          cloud: '#dc2626',
        },
      },
      borderColor: {
        default: v('color-border'),
        subtle: v('color-border-subtle'),
        strong: v('color-border-strong'),
      },
      spacing: scale('space', ['1', '2', '3', '4', '5', '6', '8']),
      fontFamily: {
        sans: v('font-family-base'),
        mono: v('font-family-mono'),
      },
      fontSize: scale('font-size', ['2xs', 'xs', 'sm', 'md', 'lg', 'xl', 'display']),
      fontWeight: scale('font-weight', ['regular', 'medium', 'semibold', 'bold']),
      lineHeight: scale('line-height', ['tight', 'normal']),
      borderRadius: scale('radius', ['sm', 'md', 'lg', 'full']),
      boxShadow: scale('shadow', ['sm', 'md', 'lg', 'sheet']),
      height: {
        titlebar: v('size-titlebar'),
        'ribbon-tabs': v('size-ribbon-tabs'),
        ribbon: v('size-ribbon'),
        statusbar: v('size-statusbar'),
        'cmdline-input': v('size-cmdline-input'),
        control: v('size-control'),
        icon: v('size-icon'),
        'icon-ribbon': v('size-icon-ribbon'),
        touch: v('touch-target-min'),
      },
      width: {
        'panel-left': v('size-panel-left'),
        'panel-right': v('size-panel-right'),
        icon: v('size-icon'),
        'icon-ribbon': v('size-icon-ribbon'),
        touch: v('touch-target-min'),
      },
      minWidth: { touch: v('touch-target-min') },
      minHeight: { touch: v('touch-target-min') },
      transitionDuration: { fast: v('motion-fast'), normal: v('motion-normal') },
      transitionTimingFunction: { standard: v('motion-ease') },
      // 비교 중 막대 (ProgressDialog): 왼쪽에서 오른쪽으로 계속 흐른다
      keyframes: { progress: { '0%': { transform: 'translateX(-100%)' }, '100%': { transform: 'translateX(300%)' } } },
      zIndex: scale('z', ['base', 'canvas-overlay', 'floating', 'dropdown', 'modal', 'toast']),
    },
  },
  plugins: [],
};
