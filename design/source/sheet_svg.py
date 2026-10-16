# Static SVG version of the canvas "Sheet" component (sample drawing S-101).
# Used by export_static.py so design/screens/*.html open in any browser.
import math

EN = dict(stair='STAIR ST-1', office='OFFICE 101', meeting='MEETING 102', project='SAM OFFICE NEW BUILD · STRUCTURAL',
          sheet='S-101  2F STRUCTURAL PLAN', scale='SCALE 1:100', stairNote='RECHECK STAIR WIDTH',
          call1='CHECK GAP: NEW TRUSS', call2='TO EXISTING BEAM', see='SEE FDN DETAIL S-501')


def cloud_path(x0, y0, x1, y1):
    pts = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    d = f'M{x0:.1f} {y0:.1f}'
    for k in range(4):
        a, b = pts[k], pts[(k + 1) % 4]
        ln = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, round(ln / 12))
        for i in range(1, n + 1):
            x = a[0] + (b[0] - a[0]) * i / n
            y = a[1] + (b[1] - a[1]) * i / n
            r = ln / n / 2 + 0.6
            d += f' A{r:.1f} {r:.1f} 0 0 1 {x:.1f} {y:.1f}'
    return d + ' Z'


def sheet_svg(version='new', show_marks=True, show_ghost=True, selected=1, review=False,
              paper='#ffffff', ink='#1b1f24', grid='#9aa1ab', cloud='#e5383b', hl='#ffd43b', accent='#2f7bf6'):
    new = version == 'new'
    t = EN
    marks_on = new and not review and show_marks
    ghost_on = new and not review and show_ghost
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1191 842" font-family="Pretendard, sans-serif">',
         f'<rect width="1191" height="842" fill="{paper}"/>',
         f'<rect x="30" y="30" width="1131" height="782" fill="none" stroke="{ink}" stroke-width="2.4"/>']
    vx = [100 + i * 120 for i in range(9)]
    hy = [100 + j * 110 for j in range(6)]
    o.append(f'<g stroke="{grid}" stroke-width="0.9" stroke-dasharray="10 4 2 4">'
             + ''.join(f'<line x1="{x}" y1="88" x2="{x}" y2="676"/>' for x in vx)
             + ''.join(f'<line x1="86" y1="{y}" x2="1086" y2="{y}"/>' for y in hy) + '</g>')
    o.append(f'<g fill="{paper}" stroke="{ink}" stroke-width="1.4">' + ''.join(f'<circle cx="{x}" cy="64" r="13"/>' for x in vx)
             + ''.join(f'<circle cx="64" cy="{y}" r="13"/>' for y in hy) + '</g>')
    o.append(f'<g fill="{ink}" font-size="13" font-weight="600" text-anchor="middle" dominant-baseline="central">'
             + ''.join(f'<text x="{x}" y="64">{"ABCDEFGHI"[i]}</text>' for i, x in enumerate(vx))
             + ''.join(f'<text x="64" y="{y}">{j + 1}</text>' for j, y in enumerate(hy)) + '</g>')
    cols = [(94 + i * 120, 94 + j * 110) for i in (0, 4, 8) for j in range(6)] + [(94 + i * 120, 94 + j * 110) for i in (2, 6) for j in (0, 3, 5)]
    o.append(f'<g fill="{ink}">' + ''.join(f'<rect x="{x}" y="{y}" width="12" height="12"/>' for x, y in cols) + '</g>')
    o.append(f'<g stroke="{ink}" stroke-width="2.6" fill="none"><rect x="100" y="100" width="960" height="550"/><line x1="580" y1="100" x2="580" y2="650"/><line x1="100" y1="430" x2="580" y2="430"/></g>')
    beams = '<line x1="112" y1="215" x2="568" y2="215"/><line x1="592" y1="452" x2="1048" y2="452"/>'
    beams += '<line x1="830" y1="185" x2="1010" y2="185"/>' if new else '<line x1="400" y1="385" x2="600" y2="385"/>'
    o.append(f'<g stroke="{ink}" stroke-width="5" stroke-linecap="square">{beams}</g>')
    labels = '<text x="118" y="206">B1  W12×26</text><text x="598" y="443">B3  W10×22</text>'
    labels += '<text x="836" y="176">B4  W8×18</text>' if new else '<text x="406" y="376">B2  W8×15</text>'
    o.append(f'<g fill="{ink}" font-size="11.5" font-weight="500">{labels}</g>')
    stair = ''.join(f'<line x1="{x}" y1="540" x2="{x}" y2="624"/>' for x in range(832, 905, 12))
    o.append(f'<g stroke="{ink}" stroke-width="1" fill="none"><rect x="820" y="540" width="96" height="84"/>{stair}'
             '<line x1="340" y1="580" x2="460" y2="580"/><line x1="340" y1="572" x2="340" y2="588"/><line x1="460" y1="572" x2="460" y2="588"/>'
             '<line x1="335" y1="585" x2="345" y2="575"/><line x1="455" y1="585" x2="465" y2="575"/></g>')
    o.append(f'<g fill="{ink}" font-size="14" font-weight="500"><text x="700" y="318">VERIFY CLEARANCE AT {"COLUMN" if new else "BEAM"}</text>'
             f'<text x="400" y="570" text-anchor="middle">{"3500" if new else "3000"}</text>'
             f'<text x="828" y="532" font-size="11">{t["stair"]}</text><text x="120" y="398" font-size="12">{t["office"]}</text><text x="620" y="252" font-size="12">{t["meeting"]}</text></g>')
    o.append(f'<g stroke="{ink}" stroke-width="1.4" fill="none"><rect x="860" y="700" width="301" height="112"/>'
             '<line x1="860" y1="737" x2="1161" y2="737"/><line x1="860" y1="774" x2="1161" y2="774"/><line x1="1080" y1="737" x2="1080" y2="812"/></g>')
    o.append(f'<g fill="{ink}" font-size="11"><text x="870" y="724" font-weight="600">{t["project"]}</text><text x="870" y="760">{t["sheet"]}</text>'
             f'<text x="1090" y="760">REV {"1" if new else "0"}</text><text x="870" y="797">{t["scale"]}</text><text x="1090" y="797">{"2026.10.14" if new else "2026.09.01"}</text></g>')
    if ghost_on:
        o.append(f'<g opacity="0.45" stroke="{cloud}" fill="{cloud}"><line x1="400" y1="385" x2="600" y2="385" stroke-width="5"/>'
                 f'<text x="406" y="376" font-size="11.5" stroke="none">B2  W8×15</text></g>')
    if marks_on:
        changes = [(818, 162, 1022, 199), (856, 298, 936, 328), (392, 362, 608, 396), (378, 552, 422, 578)]
        g = [f'<rect x="864" y="305" width="64" height="18" fill="{hl}" opacity="0.5"/><rect x="384" y="557" width="32" height="17" fill="{hl}" opacity="0.5"/>']
        for i, (x0, y0, x1, y1) in enumerate(changes):
            s, h = 20, 20 * math.sqrt(3) / 2
            cx, bottom = x1 + 5, y0 - 7
            top = bottom - h
            g.append(f'<path d="{cloud_path(x0, y0, x1, y1)}" fill="none" stroke="{cloud}" stroke-width="2.2" stroke-linejoin="round"/>'
                     f'<polygon points="{cx},{top:.2f} {cx + s / 2},{bottom} {cx - s / 2},{bottom}" fill="{paper}" stroke="{cloud}" stroke-width="2" stroke-linejoin="round"/>'
                     f'<text x="{cx}" y="{top + h * 2 / 3:.2f}" fill="{cloud}" font-size="11" font-weight="700" text-anchor="middle" dominant-baseline="central">{i + 1}</text>')
        if selected:
            x0, y0, x1, y1 = changes[selected - 1]
            g.append(f'<rect x="{x0 - 10}" y="{y0 - 32}" width="{x1 - x0 + 34}" height="{y1 - y0 + 42}" rx="4" fill="none" stroke="{accent}" stroke-width="2" stroke-dasharray="6 4"/>')
        o.append('<g>' + ''.join(g) + '</g>')
    if review:
        o.append(f'<g><rect x="594" y="432" width="76" height="16" fill="{hl}" opacity="0.5"/>'
                 f'<path d="{cloud_path(810, 530, 926, 634)}" fill="none" stroke="#e5383b" stroke-width="2.2" stroke-linejoin="round"/>'
                 f'<text x="934" y="548" fill="#e5383b" font-size="12" font-weight="600">{t["stairNote"]}</text>'
                 '<line x1="650" y1="160" x2="575" y2="208" stroke="#e5383b" stroke-width="2"/><polygon points="568,213 582,209 576,201" fill="#e5383b"/>'
                 '<rect x="650" y="124" width="232" height="54" fill="#ffffff" stroke="#e5383b" stroke-width="2"/>'
                 f'<text x="662" y="146" fill="#c92a2a" font-size="13" font-weight="600">{t["call1"]}</text>'
                 f'<text x="662" y="166" fill="#c92a2a" font-size="13" font-weight="600">{t["call2"]}</text>'
                 '<rect x="122" y="470" width="214" height="40" fill="#ffffff" stroke="#1c7ed6" stroke-width="1.8"/>'
                 f'<text x="134" y="495" fill="#1864ab" font-size="13" font-weight="600">{t["see"]}</text>'
                 '<line x1="300" y1="640" x2="372" y2="592" stroke="#1c7ed6" stroke-width="2"/><polygon points="378,588 364,590 370,598" fill="#1c7ed6"/>'
                 f'<rect x="644" y="120" width="244" height="62" fill="none" stroke="{accent}" stroke-width="1.5" stroke-dasharray="5 4"/>'
                 + ''.join(f'<rect x="{x}" y="{y}" width="10" height="10" fill="#ffffff" stroke="{accent}" stroke-width="1.5"/>' for x, y in ((639, 115), (883, 115), (639, 177), (883, 177)))
                 + f'<circle cx="568" cy="213" r="6" fill="#ffffff" stroke="{accent}" stroke-width="1.5"/></g>')
    o.append('</svg>')
    return '\n'.join(o)
