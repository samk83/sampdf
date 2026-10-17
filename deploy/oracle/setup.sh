#!/usr/bin/env bash
# 오라클 ARM 서버(Ubuntu 24.04)에서 한 번 실행: Docker 설치 → 방화벽 80·443 열기 → 비밀번호 설정 → 앱 빌드·실행.
# 사용: bash ~/sampdf/deploy/oracle/setup.sh
# 다시 실행해도 된다 (이미 된 단계는 건너뜀). 코드 업데이트만 할 때는 README의 "업데이트" 명령을 쓴다.
set -euo pipefail
cd "$(dirname "$0")"

if [ "$(uname -m)" != "aarch64" ]; then
  echo "주의: 이 서버는 ARM(aarch64)이 아닙니다 ($(uname -m)). 그래도 동작은 하지만 오라클 무료 ARM 서버가 맞는지 확인하세요."
fi

# 1) Docker
if ! command -v docker >/dev/null; then
  echo "== Docker 설치"
  curl -fsSL https://get.docker.com | sudo sh
fi

# 2) 서버 안 방화벽: 오라클 Ubuntu 이미지는 기본으로 22번(SSH)만 열고 나머지는 REJECT 한다.
#    REJECT 줄 바로 위에 80·443을 넣고, 재부팅 후에도 남게 rules.v4에도 같은 두 줄만 넣는다
#    (netfilter-persistent save는 Docker가 만든 규칙까지 저장해 재부팅 뒤 꼬일 수 있어 쓰지 않는다).
echo "== 방화벽 80·443 열기"
for port in 80 443; do
  if ! sudo iptables -C INPUT -p tcp -m state --state NEW -m tcp --dport "$port" -j ACCEPT 2>/dev/null; then
    pos=$(sudo iptables -L INPUT --line-numbers -n | awk '$2=="REJECT"{print $1; exit}')
    sudo iptables -I INPUT "${pos:-1}" -p tcp -m state --state NEW -m tcp --dport "$port" -j ACCEPT
  fi
  rules=/etc/iptables/rules.v4
  if [ -f "$rules" ] && ! sudo grep -q -- "--dport $port -j ACCEPT" "$rules"; then
    if sudo grep -q '^-A INPUT -j REJECT' "$rules"; then
      sudo sed -i "/^-A INPUT -j REJECT/i -A INPUT -p tcp -m state --state NEW -m tcp --dport $port -j ACCEPT" "$rules"
    else
      echo "주의: $rules 에 REJECT 줄이 없어 $port 규칙을 저장하지 않았습니다 (재부팅 후 다시 setup.sh 실행)."
    fi
  fi
done

# 3) 주소·아이디·비밀번호 (.env). 이미 있으면 그대로 둔다.
if [ ! -f .env ]; then
  ip=$(curl -4fsS --max-time 10 https://ifconfig.me || true)
  default_site=""
  [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] && default_site="${ip//./-}.sslip.io"
  read -rp "사이트 주소 [엔터 = ${default_site:-없음, 직접 입력}]: " site
  site=${site:-$default_site}
  [[ "$site" =~ ^[A-Za-z0-9.:-]+$ ]] || { echo "사이트 주소가 비었거나 이상합니다 (예: 158-101-1-2.sslip.io)."; exit 1; }
  read -rp "로그인 아이디 [엔터 = sam]: " user
  user=${user:-sam}
  [[ "$user" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "아이디는 영문·숫자·. _ - 만 됩니다."; exit 1; }
  read -rsp "로그인 비밀번호 (화면에 안 보임): " pass; echo
  [ -n "$pass" ] || { echo "비밀번호가 비어 있습니다."; exit 1; }
  hash=$(sudo docker run --rm caddy:2 caddy hash-password --plaintext "$pass")
  umask 077
  # 작은따옴표: 비밀번호 해시 안의 $ 기호를 docker compose가 바꾸지 않게
  printf "SITE_ADDRESS='%s'\nLOGIN_USER='%s'\nLOGIN_HASH='%s'\n" "$site" "$user" "$hash" > .env
  echo ".env 저장함 (비밀번호는 해시로만 저장)"
fi

# 4) 빌드·실행 (처음엔 10~20분 걸릴 수 있음)
echo "== 앱 빌드·실행"
sudo docker compose up -d --build
sudo docker compose ps
echo
echo "완료. 브라우저로 https://$(grep SITE_ADDRESS .env | cut -d\' -f2) 접속 (인증서 받는 데 1분쯤 걸릴 수 있음)"
