import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// 빌드 결과는 C# 웹 서버의 wwwroot로 바로 들어간다 (Sam은 Node.js 없이 .NET만으로 실행).
// 개발 중(npm run dev)에는 /api 요청을 C# 서버(5180)로 넘긴다.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/DrawingReview.Web/wwwroot',
    emptyOutDir: true,
  },
  server: {
    // design/tokens.css는 web 밖(저장소의 design 폴더)에 있다 (설계서 4-4: 토큰 파일 하나만)
    fs: { allow: ['..'] },
    proxy: { '/api': 'http://127.0.0.1:5180' },
  },
});
