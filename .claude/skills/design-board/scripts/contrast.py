#!/usr/bin/env python3
"""명도 대비 계산 도우미 (WCAG 2.x 공식).

사용법:
  python3 contrast.py "#111827" "#ffffff"          두 색의 대비 계산
  python3 contrast.py --tokens design/tokens.css   토큰 파일의 주요 글자/배경 조합 점검

기준: 본문 글자 4.5:1 이상, 큰 글자(약 24px, 굵게 19px 이상)·아이콘 3:1 이상.
"""
import re
import sys

# 토큰 파일에서 점검할 (글자, 배경, 기준) 조합. 없는 변수는 건너뛴다.
PAIRS = [
    ("--color-text", "--color-background", 4.5),
    ("--color-text", "--color-surface", 4.5),
    ("--color-text-muted", "--color-background", 4.5),
    ("--color-text-muted", "--color-surface", 4.5),
    ("--color-on-primary", "--color-primary", 4.5),
    ("--color-on-accent", "--color-accent", 4.5),
    ("--color-danger", "--color-background", 4.5),
    ("--color-success", "--color-background", 3.0),
    ("--color-border", "--color-background", 1.0),  # 테두리는 참고용 (장식이면 기준 없음)
]


def parse_hex(value: str):
    v = value.strip().lstrip("#")
    if len(v) == 3:
        v = "".join(c * 2 for c in v)
    if len(v) == 8:  # 투명도는 무시
        v = v[:6]
    if not re.fullmatch(r"[0-9a-fA-F]{6}", v):
        raise ValueError(f"16진수 색이 아닙니다: {value}")
    return tuple(int(v[i:i + 2], 16) / 255 for i in (0, 2, 4))


def luminance(rgb):
    def chan(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (chan(c) for c in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def ratio(a: str, b: str) -> float:
    la, lb = luminance(parse_hex(a)), luminance(parse_hex(b))
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def read_tokens(path: str) -> dict:
    text = open(path, encoding="utf-8").read()
    # 첫 번째 :root 블록(밝은 테마)의 단순 16진수 값만 읽는다
    root = re.search(r":root\s*\{(.*?)\}", text, re.S)
    body = root.group(1) if root else text
    return {m.group(1): m.group(2) for m in re.finditer(r"(--[\w-]+)\s*:\s*(#[0-9a-fA-F]{3,8})\s*;", body)}


def main(argv):
    if len(argv) == 3 and argv[1] != "--tokens":
        r = ratio(argv[1], argv[2])
        print(f"{argv[1]} / {argv[2]} = {r:.2f}:1  본문 {'통과' if r >= 4.5 else '미달'} · 큰 글자 {'통과' if r >= 3 else '미달'}")
        return 0 if r >= 4.5 else 1
    if len(argv) == 3 and argv[1] == "--tokens":
        tokens = read_tokens(argv[2])
        failed = 0
        checked = 0
        for fg, bg, need in PAIRS:
            if fg in tokens and bg in tokens:
                r = ratio(tokens[fg], tokens[bg])
                checked += 1
                ok = r >= need
                failed += 0 if ok else 1
                print(f"{'통과' if ok else '미달'}  {fg} / {bg} = {r:.2f}:1 (기준 {need}:1)")
        if checked == 0:
            print("점검할 조합을 찾지 못했습니다. --color-text, --color-background 같은 변수 이름을 쓰는지 확인하세요.")
            return 1
        print(f"점검 {checked}개 · 미달 {failed}개")
        return 1 if failed else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
