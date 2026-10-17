# 오라클 클라우드 배포 계획 (Always Free ARM)

웹 버전을 오라클 클라우드 무료 서버 1대에 올리는 순서. **가입·카드 등록·실제 배포는 Owner 승인 후에만 한다.**

## 1. 서버 사양 (2026-10-17 확인)

| 항목 | 값 |
|---|---|
| 지역(Home Region) | **Canada Southeast (Toronto) `ca-toronto-1`**. 가입 때 한 번 고르면 못 바꾸고, 무료 서버는 이 지역에서만 만들 수 있다 |
| 서버 모양(shape) | `VM.Standard.A1.Flex` (ARM, Ampere) · **2 OCPU · 12 GB** (2026-06-15부터 무료 한도가 4→2 OCPU, 24→12 GB로 줄었음) |
| 운영체제 | Canonical **Ubuntu 24.04** (aarch64) |
| 디스크 | 기본 50 GB (무료 합계 200 GB) |
| 밖으로 나가는 데이터 | 월 10 TB 무료 |
| 비용 | $0 (무료 한도 안). 카드 등록은 필요 |

## 2. 이 저장소에 준비된 것

| 파일 | 하는 일 |
|---|---|
| `Dockerfile` (저장소 맨 위) | x64·arm64 둘 다 만든다. ARM 서버에서 그냥 빌드하면 arm64용 |
| `deploy/oracle/docker-compose.yml` | 앱 + Caddy(HTTPS 자동, 비밀번호 잠금). 사용 시간 한도 끔(`Hosting__MonthlyLimitHours=0`), 메모리 10 GB 제한 |
| `deploy/oracle/Caddyfile` | 주소·비밀번호·큰 비교 10분 대기 |
| `deploy/oracle/setup.sh` | 서버에서 한 번 실행: Docker 설치 → 방화벽 80·443 → 비밀번호 → 빌드·실행 |

## 3. Owner가 할 일 (승인 후, 순서대로)

| # | 어디서 | 할 일 |
|---|---|---|
| 1 | https://signup.cloud.oracle.com | 가입. **Home Region = Canada Southeast (Toronto)**. 카드 등록(무료 확인용) |
| 2 | 오라클 콘솔 → **Billing & Cost Management → Upgrade and Manage Payment** | (권장) **Pay As You Go**로 올리기 + **Budgets**에서 월 $1 알림 만들기. 이유는 아래 5번 |
| 3 | 콘솔 → **Compute → Instances → Create instance** | Image: **Canonical Ubuntu 24.04** · Shape: **Ampere → VM.Standard.A1.Flex, 2 OCPU, 12 GB** · Networking: 새 VCN + **Assign a public IPv4 address** · SSH keys: **Generate a key pair for me → Save private key** (파일을 잘 보관) |
| 4 | 인스턴스 화면 → **Subnet → Default Security List → Add Ingress Rules** | Source CIDR `0.0.0.0/0`, TCP, Destination Port Range `80` 규칙 1개 + `443` 규칙 1개 추가 |
| 5 | Windows PowerShell | 아래 "서버 접속·설치" 명령 실행 |

"Out of host capacity"가 나오면: 다른 Availability Domain을 고르거나 잠시 뒤 다시. Pay As You Go 계정이 자리를 더 잘 받는다는 사용자 보고가 많다(오라클 공식 보장은 아님).

### 서버 접속·설치 (PowerShell)

키 파일을 `C:\Users\<내이름>\.ssh\oracle.key`에 두고, `<공인IP>`는 인스턴스 화면의 Public IP:

```powershell
ssh -i $HOME\.ssh\oracle.key ubuntu@<공인IP>
```

서버 안에서:

```bash
git clone https://github.com/samk83/sampdf.git ~/sampdf
bash ~/sampdf/deploy/oracle/setup.sh
```

- 저장소가 비공개라서 `git clone`이 아이디·비밀번호를 묻는다: 아이디 = GitHub 아이디, 비밀번호 = GitHub **Personal access token** (Settings → Developer settings → Fine-grained tokens, 이 저장소 Contents: Read-only).
- `setup.sh`가 사이트 주소(엔터 = `<IP>.sslip.io`, 도메인 없이 HTTPS 됨), 로그인 아이디·비밀번호를 묻는다.
- `sslip.io` 주소로 인증서가 안 받아지면(발급 횟수 제한 등) 도메인을 하나 사서 서버 IP로 연결하고 `.env`의 `SITE_ADDRESS`를 바꾼 뒤 `sudo docker compose up -d`.
- 처음 빌드는 10~20분. 끝나면 `https://<주소>`로 접속 → 아이디·비밀번호 입력.

### 업데이트 (새 코드 받기)

```bash
cd ~/sampdf && git pull && cd deploy/oracle && sudo docker compose up -d --build
```

## 4. 확인한 것 / 못 한 것

| 항목 | 결과 |
|---|---|
| arm64 이미지 빌드 (클라우드 세션, QEMU ARM 흉내) | 통과. 빠진 시스템 부품 검사(ldd) 통과 |
| arm64 이미지에서 실제 비교 (PDF·PNG 샘플: 올리기 → 페이지 그림 → 비교 → 겹친 그림 → 표시 PDF 내보내기) | 통과. 변경 수가 x64와 같음 (PDF 4곳, PNG 5곳) |
| x64 이미지 (Ubuntu 24.04로 바꾼 뒤) | 같은 시험 통과 |
| docker compose + Caddy 비밀번호 | 비밀번호 없음·틀림 → 401, 맞음 → 열림 (로컬 http로 확인) |
| .NET 테스트 | 311개 통과 |
| 진짜 오라클 ARM 서버에서 실행·속도 | **못 함** (가입 전). HTTPS 인증서 발급도 실제 서버에서 확인 |

## 5. 위험과 대책

| 위험 | 대책 |
|---|---|
| **7일 동안 CPU(95%)·네트워크·메모리가 모두 20% 미만이면 무료 서버를 회수할 수 있음**. 혼자 가끔 쓰면 해당됨 | Pay As You Go로 올리면 회수 안 된다는 사용자 보고가 많음(오라클 문서엔 명시 없음, 무료 한도 안이면 $0). 월 $1 예산 알림 같이 설정. 회수돼도 위 설치 명령으로 다시 만들 수 있음(서버에 저장하는 데이터 없음) |
| 무료 한도를 넘는 서버를 만들면 요금 | 2 OCPU·12 GB 서버 1대만. 예산 알림 |
| 인터넷에 열려 있음 (로그인 기능 전) | Caddy 비밀번호로 잠금. 로그인(Clerk)은 나중 단계 |
| 무료 한도가 또 줄 수 있음 (이미 한 번 공지 없이 줄었음) | Docker 이미지라 Cloud Run·Fly.io로 그대로 옮길 수 있음 |
| arm64 OpenCV 부품이 시스템 부품(FFmpeg 6 등)에 의존 | 실행 이미지를 Ubuntu 24.04로 고정, 빌드 때 ldd로 빠진 부품 검사 |

출처: docs.oracle.com/en-us/iaas/Content/FreeTier/resourceref.htm (Always Free Resources, 2026-06-12 수정), infoq.com/news/2026/07/oracle-cloud-free-tier-limits, terminalbytes.com/oracle-cloud-free-tier-changes-2026
