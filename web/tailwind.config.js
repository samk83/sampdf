/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"IBM Plex Sans KR"', 'system-ui', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'monospace'],
      },
      colors: {
        ink: '#16181d',
        muted: '#5b6170',
        line: '#e3e5ea',
        canvas: '#e9ebef',
        accent: { DEFAULT: '#1d4ed8', soft: '#e0ecff' },
        cloud: '#dc2626',
      },
    },
  },
  plugins: [],
};
