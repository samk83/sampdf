// 흰(또는 한 가지 색) 배경을 투명하게 만드는 계산. 브라우저 안에서만 처리하고 서버로 보내지 않는다.

export type Rgb = { r: number; g: number; b: number };

export type Options = {
  /** 배경 기준 150~254: 높을수록 배경 색에 아주 가까운 것만 지운다 (255 − 기준 = 허용 차이). */
  threshold: number;
  background: Rgb;
  /** 가장자리 부드럽게: 배경과 선 사이를 반투명으로 이어 계단 모양을 줄이고, 배경 색이 비치는 테두리(헤일로)를 걷어낸다. */
  soft: boolean;
  /** 선 색을 하나로 맞추기 (null이면 원래 색 유지). */
  unifyColor: Rgb | null;
};

/** 테두리 픽셀들의 가운데값으로 배경 색을 고른다 (스캔한 종이색 자동 인식). */
export function detectBackground(data: Uint8ClampedArray, width: number, height: number): Rgb {
  const rs: number[] = [], gs: number[] = [], bs: number[] = [];
  const add = (x: number, y: number) => {
    const i = (y * width + x) * 4;
    rs.push(data[i]); gs.push(data[i + 1]); bs.push(data[i + 2]);
  };
  const step = Math.max(1, Math.floor(Math.max(width, height) / 200));
  for (let x = 0; x < width; x += step) { add(x, 0); add(x, height - 1); }
  for (let y = 0; y < height; y += step) { add(0, y); add(width - 1, y); }
  const median = (a: number[]) => a.sort((p, q) => p - q)[a.length >> 1] ?? 255;
  return { r: median(rs), g: median(gs), b: median(bs) };
}

/** 새 RGBA 배열을 돌려준다 (입력은 바꾸지 않음). */
export function removeBackground(src: Uint8ClampedArray, o: Options): Uint8ClampedArray {
  const out = new Uint8ClampedArray(src.length);
  const tol = Math.max(0, 255 - o.threshold);
  const ramp = tol * 2 + 12; // 부드럽게: 허용 차이 밖으로 이만큼에 걸쳐 0 → 불투명
  const { r: br, g: bg, b: bb } = o.background;
  for (let i = 0; i < src.length; i += 4) {
    const r = src[i], g = src[i + 1], b = src[i + 2], a0 = src[i + 3];
    const diff = Math.max(Math.abs(r - br), Math.abs(g - bg), Math.abs(b - bb));
    let alpha: number;
    if (diff <= tol) alpha = 0;
    else if (!o.soft) alpha = 1;
    else alpha = Math.min(1, (diff - tol) / ramp);
    let nr = r, ng = g, nb = b;
    if (o.unifyColor) {
      nr = o.unifyColor.r; ng = o.unifyColor.g; nb = o.unifyColor.b;
    } else if (o.soft && alpha > 0 && alpha < 1) {
      // 섞인 배경 색을 빼서 원래 선 색을 되살린다: c = a·선 + (1−a)·배경
      nr = (r - br * (1 - alpha)) / alpha;
      ng = (g - bg * (1 - alpha)) / alpha;
      nb = (b - bb * (1 - alpha)) / alpha;
    }
    out[i] = nr; out[i + 1] = ng; out[i + 2] = nb;
    out[i + 3] = Math.round(alpha * a0);
  }
  return out;
}

/** 투명하지 않은 픽셀을 모두 담는 가장 작은 영역 (여백 자르기). 모두 투명하면 null. */
export function opaqueBounds(data: Uint8ClampedArray, width: number, height: number, minAlpha = 8) {
  let x0 = width, y0 = height, x1 = -1, y1 = -1;
  for (let y = 0; y < height; y++)
    for (let x = 0; x < width; x++)
      if (data[(y * width + x) * 4 + 3] >= minAlpha) {
        if (x < x0) x0 = x;
        if (x > x1) x1 = x;
        if (y < y0) y0 = y;
        if (y > y1) y1 = y;
      }
  return x1 < 0 ? null : { x: x0, y: y0, width: x1 - x0 + 1, height: y1 - y0 + 1 };
}

export const toHex = (c: Rgb) => '#' + [c.r, c.g, c.b].map((v) => Math.round(v).toString(16).padStart(2, '0')).join('').toUpperCase();
export const fromHex = (h: string): Rgb => ({ r: parseInt(h.slice(1, 3), 16), g: parseInt(h.slice(3, 5), 16), b: parseInt(h.slice(5, 7), 16) });
