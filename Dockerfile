# 웹 버전 배포용 이미지 (설계서 11-4 W0). 저장소 맨 위에서: docker build -t sam-drawing-tools .
# 실행: docker run -p 8080:8080 sam-drawing-tools  → http://localhost:8080
# 오라클 클라우드 ARM 서버(arm64)용: docker buildx build --platform linux/arm64 -t sam-drawing-tools:arm64 .
#   (ARM 서버 위에서 그냥 docker build 해도 arm64로 만들어진다. 자세한 건 deploy/oracle/README.md)
# 웹 모드로 돈다 (Hosting__Mode=Web): 사용자마다 파일 분리, 30분 안 쓰면 지움, 디스크에 저장 안 함.
#
# NODE_BASE / SDK_BASE / RUNTIME_BASE는 보통 그대로 둔다. 회사 프록시처럼 특별한 네트워크에서 미리 만든 이미지를 쓸 때만 바꾼다.
# 실행 이미지는 Ubuntu 24.04(noble): arm64용 OpenCV 부품이 24.04의 시스템 부품(FFmpeg 6, libtiff6)에 맞춰져 있다.
ARG NODE_BASE=node:20-bookworm-slim
ARG SDK_BASE=mcr.microsoft.com/dotnet/sdk:8.0-noble
ARG RUNTIME_BASE=mcr.microsoft.com/dotnet/aspnet:8.0-noble

# 1·2단계는 빌드하는 컴퓨터의 CPU로 돌고(빠름), 결과만 대상 CPU(x64/arm64)용으로 만든다.
# 1) 화면 (React) — 웹 버전으로 빌드
FROM --platform=$BUILDPLATFORM ${NODE_BASE} AS web
WORKDIR /repo
COPY design ./design
COPY web/package.json web/package-lock.json ./web/
RUN cd web && npm ci --no-audit --no-fund
COPY web ./web
RUN cd web && npm run build:web

# 2) 서버 (C#)
FROM --platform=$BUILDPLATFORM ${SDK_BASE} AS server
ARG TARGETARCH
WORKDIR /repo
COPY . .
RUN case "${TARGETARCH:-amd64}" in amd64) RID=linux-x64 ;; arm64) RID=linux-arm64 ;; *) echo "지원하지 않는 CPU: $TARGETARCH" >&2; exit 1 ;; esac \
 && dotnet publish src/DrawingReview.Web -c Release -r $RID --self-contained false -o /app \
 && rm -rf /app/wwwroot
COPY --from=web /repo/web-dist /app/wwwroot

# 3) 실행 이미지: OpenCV가 쓰는 시스템 부품 + 한글 글꼴(fonts-nanum: PDF에 글꼴이 안 들어 있는 한글을 PDFium이 그릴 때 쓴다)
#    arm64용 OpenCV는 FFmpeg·Tesseract·그림 형식 부품을 시스템 것으로 쓰므로 더 설치한다 (x64용은 안에 들어 있음).
FROM ${RUNTIME_BASE}
ARG TARGETARCH
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      libgtk-3-0t64 libfreetype6 libharfbuzz0b libdrm2 libx11-6 libatomic1 fonts-nanum \
      $(if [ "$TARGETARCH" = "arm64" ]; then echo \
        libtesseract5 libavcodec60 libavformat60 libavutil58 libswscale7 \
        libjpeg-turbo8 libwebp7 libwebpmux3 libwebpdemux2 libpng16-16t64 libtiff6 zlib1g; fi) \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=server /app ./
# 빠진 시스템 부품이 있으면 여기서 빌드를 멈춘다 (실행 중 비교 실패를 막음)
RUN test -f /app/libOpenCvSharpExtern.so && test -f /app/libpdfium.so \
 && ! ldd /app/*.so | grep "not found"
# .NET 8 이미지에 들어 있는 일반 사용자(app)로 실행 (관리자 권한 없이)
USER $APP_UID
ENV Hosting__Mode=Web \
    PORT=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "DrawingReview.Web.dll", "--no-browser"]
