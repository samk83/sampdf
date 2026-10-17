# 웹 버전 배포용 이미지 (설계서 11-4 W0). 저장소 맨 위에서: docker build -t sam-drawing-tools .
# 실행: docker run -p 8080:8080 sam-drawing-tools  → http://localhost:8080
# 웹 모드로 돈다 (Hosting__Mode=Web): 사용자마다 파일 분리, 30분 안 쓰면 지움, 디스크에 저장 안 함.
#
# SDK_BASE / RUNTIME_BASE는 보통 그대로 둔다. 회사 프록시처럼 특별한 네트워크에서 미리 만든 이미지를 쓸 때만 바꾼다.
ARG SDK_BASE=mcr.microsoft.com/dotnet/sdk:8.0-jammy
ARG RUNTIME_BASE=mcr.microsoft.com/dotnet/aspnet:8.0-jammy

# 1) 화면 (React) — 웹 버전으로 빌드
FROM node:20-bookworm-slim AS web
WORKDIR /repo
COPY design ./design
COPY web/package.json web/package-lock.json ./web/
RUN cd web && npm ci --no-audit --no-fund
COPY web ./web
RUN cd web && npm run build:web

# 2) 서버 (C#)
FROM ${SDK_BASE} AS server
WORKDIR /repo
COPY . .
RUN dotnet publish src/DrawingReview.Web -c Release -o /app \
 && rm -rf /app/wwwroot
COPY --from=web /repo/web-dist /app/wwwroot

# 3) 실행 이미지: OpenCV가 쓰는 시스템 부품만 더한다
FROM ${RUNTIME_BASE}
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      libgtk-3-0 libfreetype6 libharfbuzz0b libdrm2 libx11-6 libatomic1 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=server /app ./
# 빠진 시스템 부품이 있으면 여기서 빌드를 멈춘다 (실행 중 비교 실패를 막음)
RUN test -n "$(ls /app/runtimes/linux-x64/native/*.so)" \
 && ! ldd /app/runtimes/linux-x64/native/*.so | grep "not found"
# .NET 8 이미지에 들어 있는 일반 사용자(app)로 실행 (관리자 권한 없이)
USER $APP_UID
ENV Hosting__Mode=Web \
    PORT=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "DrawingReview.Web.dll", "--no-browser"]
