import sys
from chrome import *

OUT = './out/'  # canvas (.dc.html) output; design/screens comes from export_static.py
DOC_TABS_REVIEW = [('S-101_Structural_Plan_rev1.pdf', True), ('Compare · S-101 rev0 ↔ rev1', False)]
DOC_TABS_COMPARE = [('S-101_Structural_Plan_rev1.pdf', False), ('Compare · S-101 rev0 ↔ rev1', True)]

# ---------- shared pieces ----------

def draw_ribbon(active_tool):
    def b(label, ic, key, color='currentColor'):
        return (label, ic, {'key': key, 'on': label == active_tool, 'color': color})
    groups = [
        ('Select', [b('Select', 'pointer', 'Esc'), b('Pan', 'hand', 'P')]),
        ('Draw', [b('Line', 'line', 'L'), b('Polyline', 'pline', 'PL'), b('Circle', 'circle', 'C'), b('Rectangle', 'rect', 'REC'),
                  b('Rev cloud', 'cloud', 'RC', '#ff6b6b'), b('Hatch', 'hatch', 'H'), b('Text', 'text', 'T')]),
        ('Modify', [b('Move', 'move', 'M'), b('Copy', 'copy', 'CO'), b('Erase', 'erase', 'E')]),
        ('Revision', [b('△ Delta', 'tri', 'DT', '#ff6b6b')]),
        ('Measure', [b('Distance', 'ruler', 'DI'), b('Scale', 'ruler', 'SC')]),
    ]
    extra = ribbon_extra([
        f'<span style="display: flex; align-items: center; gap: 8px"><span style="font-size: 11px; color: {T["faint"]}; width: 38px">Layer</span>'
        f'<span style="width: 150px; height: 24px; box-sizing: border-box; display: flex; align-items: center; gap: 6px; padding: 0 8px; border: 1px solid {T["field"]}; border-radius: 4px; background: {T["app"]}; font-size: 12px">'
        f'<span style="width: 10px; height: 10px; border-radius: 2px; background: {"#ff922b" if active_tool == "Hatch" else T["red"]}"></span>{"HATCH" if active_tool == "Hatch" else "REVIEW"}<span style="margin-left: auto; color: {T["faint"]}">{icon("chevdown", 12)}</span></span></span>',
        f'<span style="display: flex; align-items: center; gap: 8px"><span style="font-size: 11px; color: {T["faint"]}; width: 38px">Style</span>{chip("ByLayer")}{chip("0.35 mm")}{chip("100%")}</span>',
    ])
    return ribbon('Draw', groups, extra)


def layers_panel(current='REVIEW'):
    mine = [('0', '#e6e8eb', True, False), ('REVIEW', T['red'], True, False), ('HATCH', '#ff922b', True, False),
            ('NOTES', '#4dabf7', True, False), ('REV-CLOUDS', '#ff6b6b', True, True)]
    pdf = [('S-GRID', True), ('S-COLS', True), ('S-BEAM', True), ('A-ANNO', True), ('A-DIMS', False), ('TITLE', True)]

    def row(name, color, vis, lock, cur, auto=False):
        bg = T['accentBg'] if cur else 'transparent'
        bar = f'box-shadow: inset 3px 0 0 {T["accent"]};' if cur else ''
        cur_mark = f'<span style="font-size: 10px; color: {T["accent"]}; font-weight: 700">CURRENT</span>' if cur else ''
        auto_mark = f'<span style="font-size: 10px; color: {T["faint"]}">auto</span>' if auto else ''
        return (f'<div style="height: 30px; display: flex; align-items: center; gap: 8px; padding: 0 10px 0 12px; background: {bg}; {bar}">'
                f'<span style="width: 11px; height: 11px; border-radius: 2px; background: {color}"></span>'
                f'<span style="flex: 1; font-family: {T["mono"]}; font-size: 12px">{name}</span>{cur_mark}{auto_mark}'
                f'<span style="color: {T["text2"] if vis else T["faint"]}; display: flex">{icon("eye" if vis else "eyeoff", 14)}</span>'
                f'<span style="color: {T["text2"] if lock else T["faint"]}; display: flex; opacity: {1 if lock else 0.6}">{icon("lock" if lock else "unlock", 14)}</span></div>')

    rows_mine = ''.join(row(n, c, v, l, n == current, n == 'REV-CLOUDS') for n, c, v, l in mine)
    rows_pdf = ''.join(
        f'<div style="height: 28px; display: flex; align-items: center; gap: 8px; padding: 0 10px 0 12px; color: {T["text2"] if v else T["faint"]}">'
        f'<span style="flex: 1; font-family: {T["mono"]}; font-size: 12px">{n}</span><span style="display: flex">{icon("eye" if v else "eyeoff", 14)}</span></div>' for n, v in pdf)
    return f'''<div style="flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden">
<div style="display: flex; align-items: center; gap: 6px; padding: 10px 10px 6px 12px">{section("My layers")}<span style="margin-left: auto; display: flex; gap: 2px">{icon_btn("plus", "New layer", 24, isz=14)}{icon_btn("settings", "Layer properties", 24, isz=14)}</span></div>
{rows_mine}
<div style="display: flex; align-items: center; gap: 6px; padding: 14px 10px 6px 12px; border-top: 1px solid {T["soft"]}; margin-top: 8px">{section("Drawing layers (PDF)")}<span style="margin-left: auto; font-size: 11px; color: {T["faint"]}">6</span></div>
{rows_pdf}
<div style="margin: auto 10px 10px; padding: 10px; border: 1px solid {T["border"]}; border-radius: 6px; font-size: 11.5px; line-height: 1.5; color: {T["muted"]}">Drawing layers come from the CAD export. Hiding them only changes the view, never the file.</div>
</div>'''


def snap_marker(kind, x, y, label):
    s = 9
    if kind == 'end':
        m = f'<rect x="{x - s}" y="{y - s}" width="{2 * s}" height="{2 * s}" fill="none" stroke="{T["snap"]}" stroke-width="2.5"></rect>'
    elif kind == 'int':
        m = (f'<line x1="{x - s}" y1="{y - s}" x2="{x + s}" y2="{y + s}" stroke="{T["snap"]}" stroke-width="2.8"></line>'
             f'<line x1="{x - s}" y1="{y + s}" x2="{x + s}" y2="{y - s}" stroke="{T["snap"]}" stroke-width="2.8"></line>')
    elif kind == 'mid':
        m = f'<polygon points="{x},{y - s - 2} {x + s + 2},{y + s} {x - s - 2},{y + s}" fill="none" stroke="{T["snap"]}" stroke-width="2.5"></polygon>'
    else:
        m = f'<circle cx="{x}" cy="{y}" r="{s}" fill="none" stroke="{T["snap"]}" stroke-width="2.5"></circle>'
    w = 10 + len(label) * 7.4
    tip = (f'<rect x="{x + 16}" y="{y + 14}" width="{w}" height="22" rx="3" fill="#1b1d21" stroke="{T["snap"]}" stroke-width="1"></rect>'
           f'<text x="{x + 21}" y="{y + 29.5}" fill="#ffffff" font-family="Pretendard, sans-serif" font-size="12.5" font-weight="600">{label}</text>') if label else ''
    return m + tip


def crosshair(x, y, size=34):
    c = T['cross']
    return (f'<line x1="{x - size}" y1="{y}" x2="{x + size}" y2="{y}" stroke="{c}" stroke-width="1"></line>'
            f'<line x1="{x}" y1="{y - size}" x2="{x}" y2="{y + size}" stroke="{c}" stroke-width="1"></line>'
            f'<rect x="{x - 5}" y="{y - 5}" width="10" height="10" fill="none" stroke="{c}" stroke-width="1"></rect>')


def dyn_input(x, y, fields):
    out, cx = '', x
    for i, (val, active) in enumerate(fields):
        w = 12 + len(val) * 8
        fill = '#ffffff' if active else '#eef1f5'
        stroke = T['accent'] if active else '#9aa1ab'
        out += (f'<rect x="{cx}" y="{y}" width="{w}" height="22" rx="2" fill="{fill}" stroke="{stroke}" stroke-width="{1.6 if active else 1}"></rect>'
                f'<text x="{cx + 6}" y="{y + 15.5}" fill="#1b1f24" font-family="JetBrains Mono, monospace" font-size="12.5">{val}</text>')
        cx += w + 4
    return out


def review_status(snap=True, ortho=False, extra_left=''):
    left = f'<span>{extra_left}</span>' if extra_left else ''
    right = (f'<span style="font-family: {T["mono"]}">X 48,020 · Y 21,500 mm</span><span>Scale 1:100</span><span>Page 1 / 3</span>'
             f'<span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>')
    return statusbar(left, DRAFT_TOGGLES(osnap=snap, ortho=ortho) + [('DARK VIEW', T['invert'])], right)


def snap_modes_bar(active=('END', 'MID', 'INT', 'CEN', 'PER', 'NEA')):
    allm = ['END', 'MID', 'INT', 'CEN', 'QUA', 'PER', 'NEA']
    return ''.join(f'<span style="padding: 0 3px; color: {"#ffffff" if m in active else "rgba(255,255,255,0.5)"}; font-weight: {600 if m in active else 400}">{m}</span>' for m in allm)


# ---------- screens ----------

def FReviewLine():
    # first point: end of beam B1 (568,215); cursor snapped to grid intersection (580,430)
    ov = (f'<line x1="568" y1="215" x2="580" y2="430" stroke="{T["red"]}" stroke-width="2"></line>'
          f'<line x1="580" y1="430" x2="580" y2="300" stroke="#2f7bf6" stroke-width="1" stroke-dasharray="4 4"></line>'
          f'<rect x="562" y="209" width="12" height="12" fill="#ffffff" stroke="{T["red"]}" stroke-width="1.5"></rect>'
          + snap_marker('int', 580, 430, 'Intersection') + crosshair(580, 430)
          + dyn_input(596, 330, [('215.3', True), ('< 86.8°', False)]))
    fl = T['floatCmd']
    canvas = canvas_area(sheet('version="new" review="{{true}}" show-marks="{{false}}"', ov, 800) + (page_nav('1 / 3', '96%') if not fl else ''), extra='; padding-bottom: 104px' if fl else '')
    props = (section('Line · drawing') + grid2(select('Layer', '<span style="display: flex; align-items: center; gap: 6px"><span style="width: 10px; height: 10px; border-radius: 2px; background: #e5383b"></span>REVIEW</span>', True),
                                              select('Color', 'ByLayer'), select('Lineweight', '0.35 mm'),
                                              select('Linetype', 'Continuous'), field('Opacity', '100%'))
             + section('Geometry') + grid2(field('Start X', '47,880', True), field('Start Y', '33,560', True),
                                          field('Length', '2,153', True), field('Angle', '86.8°', True))
             + f'<div style="margin-top: auto; display: flex; flex-direction: column; gap: 8px; padding: 10px; border: 1px solid {T["border"]}; border-radius: 6px; font-size: 12px; color: {T["muted"]}; line-height: 1.5">'
               f'<span style="color: {T["text2"]}; font-weight: 600">Tips</span>Type a length and press Enter to draw in the cursor direction. <span style="font-family: {T["mono"]}; color: {T["text"]}">@2000&lt;90</span> = relative polar. Hold Shift to toggle ORTHO.</div>')
    body = (titlebar(DOC_TABS_REVIEW, 'Autosaved 1 min ago', True) + draw_ribbon('Line')
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Layers", layers_panel())}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{canvas}'
            + cmdline([('Command: <span style="color:#fff">L</span>', 'cmd'), ('LINE', ''), ('Specify first point: _endp of B1', '')],
                      'Specify next point or', '', ['Undo'], float_=fl)
            + f'</div>{right_panel("Properties", props)}</div>'
            + review_status(extra_left=f'LINE · OSNAP {snap_modes_bar()}'))
    return page('Final · Review · LINE', root(body))


def hatch_swatch(kind, selected=False, size=40):
    pid = f'sw-{kind}'
    c = '#ff922b'
    if kind == 'Solid':
        fill, defs = c, ''
    elif kind == 'Dots':
        defs = f'<pattern id="{pid}" width="6" height="6" patternUnits="userSpaceOnUse"><circle cx="3" cy="3" r="1.1" fill="{c}"></circle></pattern>'
        fill = f'url(#{pid})'
    elif kind == 'Cross':
        defs = f'<pattern id="{pid}" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><path d="M0 0V7M0 0H7" stroke="{c}" stroke-width="1.2"></path></pattern>'
        fill = f'url(#{pid})'
    elif kind == 'Concrete':
        defs = (f'<pattern id="{pid}" width="14" height="14" patternUnits="userSpaceOnUse"><path d="M2 3l3 1-2 2zM9 8l3 2-3 1zM5 11h1" stroke="{c}" fill="none" stroke-width="1"></path>'
                f'<circle cx="11" cy="3" r="0.9" fill="{c}"></circle><circle cx="3" cy="9" r="0.7" fill="{c}"></circle></pattern>')
        fill = f'url(#{pid})'
    else:  # ANSI31
        defs = f'<pattern id="{pid}" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><path d="M0 0V6" stroke="{c}" stroke-width="1.2"></path></pattern>'
        fill = f'url(#{pid})'
    ring = f'2px solid {T["accent"]}' if selected else f'1px solid {T["field"]}'
    return (f'<button aria-pressed="{"true" if selected else "false"}" style="display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 4px 0; border: none; border-radius: 6px; background: {T["pressed"] if selected else "transparent"}; color: {T["text2"]}; font: inherit; font-size: 11px">'
            f'<svg aria-hidden="true" width="{size}" height="{size}" viewBox="0 0 40 40" style="border-radius: 4px; outline: {ring}; background: {T["app"]}"><defs>{defs}</defs><rect x="0" y="0" width="40" height="40" fill="{fill}"></rect></svg>{kind}</button>')


def FReviewHatch():
    pts = [(112, 227), (568, 227), (568, 418)]
    cur = (112, 418)
    poly = ' '.join(f'{x},{y}' for x, y in pts + [cur])
    ov = (f'<defs><pattern id="hp-ansi31" width="10" height="10" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><path d="M0 0V10" stroke="#ff922b" stroke-width="1.6"></path></pattern></defs>'
          f'<polygon points="{poly}" fill="url(#hp-ansi31)" opacity="0.75"></polygon>'
          f'<polyline points="{" ".join(f"{x},{y}" for x, y in pts)}" fill="none" stroke="#ff922b" stroke-width="2"></polyline>'
          f'<line x1="568" y1="418" x2="112" y2="418" stroke="#ff922b" stroke-width="2" stroke-dasharray="7 5"></line>'
          f'<line x1="112" y1="418" x2="112" y2="227" stroke="#ff922b" stroke-width="1.2" stroke-dasharray="3 5"></line>'
          + ''.join(f'<rect x="{x - 5}" y="{y - 5}" width="10" height="10" fill="#ffffff" stroke="#ff922b" stroke-width="1.6"></rect>' for x, y in pts)
          + snap_marker('end', 112, 418, 'Endpoint') + crosshair(112, 418)
          + dyn_input(300, 434, [('4,560', True), ('< 180°', False)]))
    fl = T['floatCmd']
    canvas = canvas_area(sheet('version="new" review="{{true}}" show-marks="{{false}}"', ov, 800) + (page_nav('1 / 3', '96%') if not fl else ''), extra='; padding-bottom: 104px' if fl else '')
    swatches = ''.join(hatch_swatch(k, k == 'ANSI31') for k in ['Solid', 'ANSI31', 'Cross', 'Dots', 'Concrete'])
    props = (section('Pattern') + f'<div style="display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 2px">{swatches}</div>'
             + grid2(field('Scale', '1.00', True), field('Angle', '0°', True))
             + swatch_row('Color', ['#ff922b', T['red'], '#4dabf7', '#51cf66', '#e6e8eb', '#868e96'], 0)
             + slider('Transparency', '25%', 25)
             + select('Layer', '<span style="display: flex; align-items: center; gap: 6px"><span style="width: 10px; height: 10px; border-radius: 2px; background: #ff922b"></span>HATCH</span>')
             + section('Boundary')
             + f'<div style="display: flex; flex-direction: column; gap: 10px">{radio("Draw boundary", True, "Click points, Enter or C to close")}{radio("Select closed object", False, "Rectangle, circle, cloud or closed polyline")}'
               f'<div style="opacity: 0.55">{radio("Pick inside an area", False, "Coming in phase 2 (needs drawing-line detection)")}</div></div>')
    body = (titlebar(DOC_TABS_REVIEW, 'Autosaved 1 min ago', True) + draw_ribbon('Hatch')
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Layers", layers_panel("HATCH"))}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{canvas}'
            + cmdline([('Command: <span style="color:#fff">H</span>', 'cmd'), ('HATCH  Pattern = ANSI31 · Scale = 1.00 · Angle = 0°', ''), ('Specify first boundary point or [Select objects/Pattern/Scale/Angle]: _endp', '')],
                      'Specify next point or', '', ['Undo', 'Close', 'Pattern', 'Scale', 'Angle'], float_=fl)
            + f'</div>{right_panel("Properties · Hatch", props)}</div>'
            + review_status(extra_left=f'HATCH · 3 points · OSNAP {snap_modes_bar()}'))
    return page('Final · Review · HATCH', root(body))


def FFocus():
    tools = [('Select', 'pointer', 'Esc', True), ('Line', 'line', 'L', False), ('Circle', 'circle', 'C', False), ('Rectangle', 'rect', 'REC', False),
             ('Rev cloud', 'cloud', 'RC', False), ('Hatch', 'hatch', 'H', False), ('Text', 'text', 'T', False), ('Callout', 'callout', 'CA', False), ('△ Delta', 'tri', 'DT', False)]
    rail = ''.join(
        f'<button aria-label="{l} ({k})" title="{l} ({k})" style="width: 42px; height: 42px; border: none; border-radius: 10px; background: {T["primary"] if on else "transparent"}; color: {"#ffffff" if on else T["text2"]}; display: flex; align-items: center; justify-content: center">{icon(ic, 19)}</button>'
        for l, ic, k, on in tools)
    pill = f'border-radius: 14px; background: rgba(35,38,43,0.92); border: 1px solid {T["border"]}; box-shadow: 0 10px 30px rgba(0,0,0,0.45)'
    bubble = (f'<div role="dialog" aria-label="Selected callout" style="position: absolute; left: 990px; top: 150px; width: 230px; display: flex; flex-direction: column; gap: 10px; padding: 12px; {pill}">'
              f'<div style="display: flex; align-items: center; gap: 8px"><span style="width: 10px; height: 10px; border-radius: 2px; background: {T["red"]}"></span><span style="font-weight: 700">Callout</span><span style="font-size: 12px; color: {T["faint"]}">REVIEW</span></div>'
              f'<div style="display: flex; gap: 6px">' + ''.join(f'<span style="width: 22px; height: 22px; border-radius: 50%; background: {c}; box-shadow: {"0 0 0 2px " + T["panel"] + ", 0 0 0 4px #ffffff" if i == 0 else "none"}"></span>' for i, c in enumerate([T['red'], '#ff922b', '#4dabf7', '#51cf66', '#e6e8eb'])) + '</div>'
              f'<div style="display: flex; gap: 6px">{btn("Edit text", h=30)}{btn("Status: Open", h=30)}{icon_btn("erase", "Erase (E)", 30)}</div>'
              f'<span style="font-size: 11.5px; color: {T["faint"]}">More in the command line, e.g. <span style="font-family: {T["mono"]}; color: {T["text2"]}">PROPERTIES</span></span></div>')
    toggles = ''.join(
        f'<span style="height: 22px; display: flex; align-items: center; padding: 0 7px; border-radius: 5px; font-size: 10.5px; font-weight: 700; letter-spacing: 0.03em; {"background: " + T["accentBg"] + "; color: #9ec5fe" if on else "color: " + T["faint"]}">{n}</span>'
        for n, on in DRAFT_TOGGLES() + [('DARK VIEW', T['invert'])])
    body = f'''<div style="position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; padding: 70px 80px 120px; background: {T["canvas"]}">
{sheet('version="new" review="{{true}}" show-marks="{{false}}"', '', 1060)}
</div>
<div style="position: absolute; left: 16px; top: 16px; height: 44px; display: flex; align-items: center; gap: 10px; padding: 0 14px 0 8px; {pill}">
<span style="width: 28px; height: 28px; border-radius: 7px; background: {T["primary"]}; display: flex; align-items: center; justify-content: center">{icon("logo", 15, "#ffffff", 2.4)}</span>
<span style="font-weight: 600">S-101_Structural_Plan_rev1.pdf</span><span style="color: {T["yellow"]}" aria-label="Unsaved changes">●</span><span style="font-size: 12px; color: {T["faint"]}">Page 1 / 3</span></div>
<div style="position: absolute; right: 16px; top: 16px; height: 44px; display: flex; align-items: center; gap: 4px; padding: 0 6px; {pill}">
{icon_btn("undo", "Undo", 32)}{icon_btn("redo", "Redo", 32)}{icon_btn("layers", "Layers (LA)", 32)}
<button style="height: 32px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: none; border-radius: 8px; background: {T["pressed"]}; color: {T["text"]}; font: inherit; font-size: 12.5px">{icon("focus", 14)}Exit focus <span style="color: {T["faint"]}">F11</span></button></div>
<div role="toolbar" aria-orientation="vertical" aria-label="Tools" style="position: absolute; left: 16px; top: 50%; transform: translateY(-50%); display: flex; flex-direction: column; gap: 2px; padding: 5px; {pill}">{rail}</div>
<div style="position: absolute; right: 16px; top: 50%; transform: translateY(-50%); display: flex; flex-direction: column; gap: 2px; padding: 5px; {pill}">{icon_btn("plus", "Zoom in", 36, T["text"])}<span style="text-align: center; font-size: 11px; color: {T["faint"]}">112%</span>{icon_btn("minus", "Zoom out", 36, T["text"])}{icon_btn("focus", "Fit (Z E)", 36, T["text"])}</div>
{bubble}
<div style="position: absolute; left: 50%; bottom: 18px; transform: translateX(-50%); width: 900px; display: flex; flex-direction: column; {pill}; font-family: {T["mono"]}; font-size: 12px; overflow: hidden">
<div style="padding: 8px 14px 4px 44px; color: {T["faint"]}">Command: <span style="color: {T["text2"]}">CA</span>  CALLOUT · 1 created on layer REVIEW</div>
<div style="height: 40px; display: flex; align-items: center; gap: 10px; padding: 0 8px 0 14px; border-top: 1px solid {T["soft"]}">
<span style="color: {T["accent"]}; display: flex">{icon("terminal", 16)}</span><span style="flex: 1; color: {T["text2"]}">Command: <span style="display: inline-block; width: 7px; height: 15px; vertical-align: -3px; background: {T["text"]}"></span></span>
<span style="display: flex; gap: 3px; font-family: 'Pretendard', sans-serif">{toggles}</span>
<button style="height: 28px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: none; border-radius: 7px; background: {T["pressed"]}; color: {T["text2"]}; font-family: 'Pretendard', sans-serif; font-size: 12px">{icon("list", 14)}Markups 5 <span style="color: {T["faint"]}">Tab</span></button></div></div>'''
    return page('Final · Focus mode', f'<div style="position: relative; height: 900px; overflow: hidden; background: {T["canvas"]}; color: {T["text"]}; font-family: \'Pretendard\', sans-serif; font-size: 13px">{body}</div>')


# ---------- compare ----------

def compare_ribbon(view='Side by side', disabled=False):
    d = {'disabled': True} if disabled else {}
    groups = [
        ('Files', [('Open', 'folder', {'key': 'OPEN'}), ('Swap', 'swap', {'key': 'SWAP', **d})]),
        ('View', [('Side by side', 'split', {'on': view == 'Side by side', 'key': 'SBS', **d}), ('Overlay', 'overlay', {'on': view == 'Overlay', 'key': 'OV', **d}),
                  ('Sync pan', 'link', {'on': True, 'key': 'SYNC', **d})]),
        ('Revision marks', [('Cloud', 'cloud', {'color': '#ff6b6b', 'key': 'RC', **d}), ('Highlight', 'hl', {'color': T['yellow'], 'key': 'HL', **d}), ('△ Delta', 'tri', {'color': '#ff6b6b', 'key': 'DT', **d})]),
        ('Review', [('Previous', 'prev', {'key': 'P', **d}), ('Next', 'next', {'key': 'N', **d}), ('Reviewed', 'check', {'key': 'Space', **d})]),
        ('Output', [('Export PDF', 'download', {'key': 'EXPORT', **d}), ('List CSV', 'table', {'key': 'EXPORTCSV', **d})]),
    ]
    extra = ribbon_extra([
        f'<span style="display: flex; align-items: center; gap: 8px"><span style="font-size: 11px; color: {T["faint"]}; width: 76px">△ numbering</span>{chip("Start 1")}{chip("Each +1")}</span>',
        f'<span style="display: flex; align-items: center; gap: 8px"><span style="font-size: 11px; color: {T["faint"]}; width: 76px">Detect</span>{chip("Text → highlight")}{chip("Ignore title block")}</span>',
    ])
    return ribbon('Compare', groups, extra)


def pages_panel(counts=('4', '0', 'New'), active=0, empty=False):
    items = []
    for i, c in enumerate(counts):
        on = i == active
        if c == 'New':
            badge = f'<span style="padding: 1px 6px; border-radius: 3px; background: rgba(140,233,154,0.14); color: {T["green"]}; font-size: 11px">New page</span>'
        elif c == '0':
            badge = f'<span style="font-size: 11px; color: {T["faint"]}">No changes</span>'
        elif c == '–':
            badge = f'<span style="font-size: 11px; color: {T["faint"]}">–</span>'
        else:
            badge = f'<span style="padding: 1px 6px; border-radius: 3px; background: rgba(229,56,59,0.18); color: {T["redText"]}; font-size: 11px">{c} changes</span>'
        items.append(f'<button style="display: flex; flex-direction: column; gap: 6px; padding: 8px; border: 1px solid {T["accent"] if on else T["border"]}; border-radius: 6px; background: {T["accentBg"] if on else "transparent"}; color: {T["text"]}; font: inherit; text-align: left">'
                     f'<span style="height: 92px; display: block; border-radius: 3px; background: #f4f4f1"></span>'
                     f'<span style="display: flex; justify-content: space-between; align-items: center; font-size: 12px">Page {i + 1}{badge}</span></button>')
    return f'<div style="flex: 1; min-height: 0; display: flex; flex-direction: column; gap: 10px; padding: 12px; overflow: hidden">{"".join(items)}</div>'


def pane_header(tag, name, meta, new=False):
    tag_st = f'background: rgba(61,139,253,0.2); color: #9ec5fe' if new else f'background: {T["pressed"]}; color: {T["text2"]}'
    return (f'<div style="height: 34px; flex: none; display: flex; align-items: center; gap: 10px; padding: 0 12px; background: {T["panel2"]}; border-bottom: 1px solid {T["soft"]}">'
            f'<span style="padding: 2px 8px; border-radius: 4px; {tag_st}; font-size: 11.5px; font-weight: 600">{tag}</span>'
            f'<span style="font-size: 12.5px; font-weight: 500">{name}</span><span style="margin-left: auto; font-size: 12px; color: {T["faint"]}">{meta}</span></div>')


def split(left, right):
    sep = (f'<div role="separator" aria-orientation="vertical" aria-label="Resize panes" style="width: 10px; flex: none; display: flex; align-items: center; justify-content: center; background: {T["chrome"]}; border-left: 1px solid {T["border"]}; border-right: 1px solid {T["border"]}">'
           f'<span style="width: 4px; height: 40px; border-radius: 2px; background: {T["accent"]}"></span></div>')
    return f'<div style="flex: 1; min-height: 0; display: flex">{left}{sep}{right}</div>'


def pane(header, inner, pad=14):
    return (f'<div style="flex: 1; min-width: 0; display: flex; flex-direction: column">{header}'
            f'<div style="flex: 1; min-height: 0; position: relative; display: flex; align-items: center; justify-content: center; padding: {pad}px; background: {T["canvas"]}">{inner}</div></div>')


CHANGES = [
    (1, 'Graphic', 'Beam B4 (W8×18) added', 'C-3', '98%', 'Open'),
    (2, 'Text', '"BEAM" → "COLUMN"', 'F-3', '100%', 'Reviewed'),
    (3, 'Graphic', 'Beam B2 (W8×15) removed', 'B-3', '96%', 'Open'),
    (4, 'Text', 'Dimension 3000 → 3500', 'C-5', '100%', 'Open'),
]


def changes_table(selected=1, height=150):
    rows = []
    for n, kind, desc, loc, conf, st in CHANGES:
        sel = n == selected
        kb = 'rgba(255,212,59,0.14); color: #ffe066' if kind == 'Text' else 'rgba(229,56,59,0.16); color: #ff8787'
        sb = f'rgba(140,233,154,0.14); color: {T["green"]}' if st == 'Reviewed' else f'rgba(173,181,189,0.14); color: {T["text2"]}'
        rows.append(f'<tr style="height: 28px; border-top: 1px solid {T["soft"]}; background: {T["accentBg"] if sel else "transparent"}; box-shadow: {"inset 3px 0 0 " + T["accent"] if sel else "none"}">'
                    f'<td style="padding-left: 12px; width: 28px">{checkbox("", n != 2)}</td>'
                    f'<td style="width: 44px; color: #ff8787; font-weight: 700">△{n}</td>'
                    f'<td style="width: 90px"><span style="padding: 1px 7px; border-radius: 3px; background: {kb}; font-size: 11.5px">{kind}</span></td>'
                    f'<td>{desc}</td><td style="width: 70px; color: {T["muted"]}">{loc}</td><td style="width: 70px; color: {T["muted"]}">{conf}</td>'
                    f'<td style="width: 96px"><span style="padding: 1px 7px; border-radius: 3px; background: {sb}; font-size: 11.5px">{st}</span></td></tr>')
    return f'''<section aria-label="Changes" style="height: {height}px; flex: none; display: flex; flex-direction: column; background: {T["panel"]}; border-top: 1px solid {T["border"]}">
<div style="height: 32px; flex: none; display: flex; align-items: center; gap: 8px; padding: 0 10px 0 12px; border-bottom: 1px solid {T["soft"]}">
<span style="font-size: 12px; font-weight: 600">Changes <span style="color: #9ec5fe">4</span></span><span style="font-size: 12px; color: {T["faint"]}">· Reviewed 1 · Excluded 0</span>
<span style="margin-left: 10px; display: flex; gap: 4px">{chip("All")}{chip("Text 2", False)}{chip("Graphic 2", False)}</span>
<span style="margin-left: auto">{icon_btn("chevdown", "Collapse (Ctrl+J)", 26)}</span></div>
<div style="flex: 1; overflow: hidden"><table style="width: 100%; border-collapse: collapse; font-size: 12.5px">
<thead><tr style="height: 26px; color: {T["faint"]}; text-align: left; background: {T["panel2"]}"><th style="padding-left: 12px; font-weight: 500"></th><th style="font-weight: 500">△</th><th style="font-weight: 500">Type</th><th style="font-weight: 500">Description</th><th style="font-weight: 500">Grid</th><th style="font-weight: 500">Match</th><th style="font-weight: 500">Status</th></tr></thead>
<tbody>{"".join(rows)}</tbody></table></div></section>'''


def change_props(n=1):
    return (f'<div style="display: flex; align-items: center; gap: 10px"><span style="width: 34px; height: 30px; border-radius: 6px; background: rgba(229,56,59,0.16); color: #ff8787; font-weight: 700; display: flex; align-items: center; justify-content: center">△{n}</span>'
            f'<span style="display: flex; flex-direction: column; gap: 2px"><span style="font-weight: 600">Beam B4 (W8×18) added</span><span style="font-size: 11.5px; color: {T["faint"]}">Graphic change · grid C-3 · 98% match</span></span></div>'
            + grid2(select('Status', 'Open'), field('△ label', '1', True))
            + toggle_row('Show cloud', True) + toggle_row('Include in export', True)
            + f'<label style="display: flex; flex-direction: column; gap: 4px"><span style="font-size: 11px; color: {T["faint"]}">Note</span><span style="height: 58px; box-sizing: border-box; padding: 8px; border: 1px solid {T["field"]}; border-radius: 4px; background: {T["app"]}; color: {T["muted"]}">Confirm connection detail with engineer</span></label>'
            + section('Cloud style') + grid2(select('Color', '<span style="display: flex; align-items: center; gap: 6px"><span style="width: 10px; height: 10px; border-radius: 2px; background: #e5383b"></span>Red</span>'), select('Arc size', 'Medium'))
            + f'<div style="margin-top: auto; display: flex; gap: 6px">{btn("Previous", ic="prev", extra="; flex: 1")}{btn("Next", "primary", "next", extra="; flex: 1")}</div>')


def compare_body(selected=1, popup=True, cmd=None):
    old = sheet('version="old" show-marks="{{false}}"')
    mini = (f'<div style="position: absolute; z-index: 3; top: 58px; right: 36px; display: flex; align-items: center; gap: 2px; padding: 3px; border-radius: 7px; background: rgba(35,38,43,0.96); border: 1px solid {T["field"]}; box-shadow: 0 6px 16px rgba(0,0,0,0.45)">'
            f'<span style="padding: 0 8px; font-size: 12px; font-weight: 700; color: #ff8787">△1</span>{icon_btn("cloud", "Edit cloud shape", 28)}{icon_btn("msg", "Add note", 28)}{icon_btn("eyeoff", "Exclude from export", 28)}{icon_btn("check", "Mark reviewed (Space)", 28)}</div>')
    new = sheet(f'version="new" selected="{{{{{selected}}}}}"') + mini
    panes = split(pane(pane_header('Original', 'S-101_rev0.pdf', 'REV 0 · 2026-09-01'), old),
                  pane(pane_header('Revised', 'S-101_rev1.pdf', 'REV 1 · 2026-10-14', True), new))
    if cmd is None:
        pop = cmd_popup([('EXPORT', 'EXP', 'Export marked PDF', 'Ctrl+E'), ('EXPORTCSV', 'EXC', 'Export change list (CSV)', ''),
                         ('EXCLUDE', 'X', 'Exclude selected change from export', ''), ('EXIT', '', 'Close this comparison', '')]) if popup else ''
        cmd = cmdline([('Command: <span style="color:#fff">N</span>', 'cmd'), ('NEXT  Change △1 of 4 · Beam B4 (W8×18) added', ''), ('Command: <span style="color:#fff">RV</span>  REVIEWED △2', '')],
                      'Command:', 'EXP', None, pop)
    return panes, cmd


def FCompare():
    panes, cmd = compare_body()
    body = (titlebar(DOC_TABS_COMPARE, 'Compared 2 min ago') + compare_ribbon()
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel())}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column">{panes}{changes_table()}{cmd}</div>'
            + f'{right_panel("Change", change_props())}</div>'
            + statusbar('<span>Compare · 4 changes on page 1</span>', None,
                        f'<span>Sync pan on</span><span>Page 1 / 3</span><span>96%</span><span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Compare result', root(body))


def FCompareEmpty():
    old = sheet('version="old" show-marks="{{false}}"')
    drop = (f'<div style="width: 100%; height: 100%; box-sizing: border-box; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 14px; border: 2px dashed {T["accent"]}; border-radius: 12px; background: rgba(61,139,253,0.05)">'
            f'<span style="width: 64px; height: 64px; border-radius: 50%; background: {T["accentBg"]}; display: flex; align-items: center; justify-content: center">{icon("upload", 28, "#9ec5fe", 1.8)}</span>'
            f'<span style="font-size: 17px; font-weight: 700">Drop the revised drawing here</span>'
            f'<span style="color: {T["muted"]}">PDF, PNG, JPG or TIFF · up to 300 MB</span>'
            f'<div style="display: flex; gap: 8px">{btn("Browse files", "primary", "folder", 36)}{btn("Pick a recent file", "ghost", None, 36)}</div>'
            f'<span style="font-size: 12px; color: {T["faint"]}">Tip: drop two files at once and the older one goes on the left.</span></div>')
    panes = split(pane(pane_header('Original', 'S-101_rev0.pdf', '3 pages · 2.4 MB'), old),
                  pane(pane_header('Revised', 'Not selected yet', '', True), drop, 22))
    settings = (section('Before you compare')
                + grid2(field('△ start number', '1', True), select('Numbering', 'Each change +1'))
                + toggle_row('Highlight text changes', True, 'Changed words get a yellow highlight')
                + toggle_row('Cloud text changes too', True)
                + toggle_row('Ignore title block', True, 'Bottom-right area is skipped')
                + toggle_row('Show removed lines in red', True)
                + f'<div style="margin-top: auto">{btn("Compare", "primary", None, 38, "; width: 100%; opacity: 0.45")}<span style="display: block; margin-top: 6px; text-align: center; font-size: 11.5px; color: {T["faint"]}">Add the revised drawing to start</span></div>')
    cmd = cmdline([('Command: <span style="color:#fff">CMP</span>', 'cmd'), ('COMPARE  Original = S-101_rev0.pdf (3 pages)', '')],
                  'Select revised drawing or', '', ['Browse', 'Recent', 'Swap'])
    body = (titlebar([('Compare · new', True)], 'Not saved') + compare_ribbon(disabled=True)
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel(("–", "–", "–"), 0))}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column">{panes}{cmd}</div>'
            + f'{right_panel("Compare settings", settings)}</div>'
            + statusbar('<span>Waiting for the revised drawing</span>', None, f'<span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Compare · waiting', root(body))


def FCompareRunning():
    old = sheet('version="old" show-marks="{{false}}"')
    new = sheet('version="new" show-marks="{{false}}"', extra_style='; opacity: 0.55')
    steps = [('Align pages', 'done', '0.3 s'), ('Find text changes', 'done', '2 found'), ('Find graphic changes', 'run', 'page 2 of 3'), ('Place clouds and △ numbers', 'todo', '')]
    st = ''
    for name, s, note in steps:
        if s == 'done':
            ic = f'<span style="width: 20px; height: 20px; border-radius: 50%; background: rgba(140,233,154,0.16); color: {T["green"]}; display: flex; align-items: center; justify-content: center">{icon("check", 12, T["green"], 3)}</span>'
        elif s == 'run':
            ic = f'<span style="width: 20px; height: 20px; box-sizing: border-box; border-radius: 50%; border: 2.5px solid {T["field"]}; border-top-color: {T["accent"]}"></span>'
        else:
            ic = f'<span style="width: 20px; height: 20px; box-sizing: border-box; border-radius: 50%; border: 1.5px solid {T["field"]}"></span>'
        st += f'<div style="display: flex; align-items: center; gap: 10px; color: {T["text"] if s != "todo" else T["faint"]}">{ic}<span style="flex: 1">{name}</span><span style="font-size: 12px; color: {T["faint"]}">{note}</span></div>'
    card = (f'<div role="status" aria-live="polite" style="position: absolute; z-index: 6; left: 50%; top: 50%; transform: translate(-50%, -50%); width: 400px; display: flex; flex-direction: column; gap: 16px; padding: 22px; border: 1px solid {T["field"]}; border-radius: 12px; background: {T["panel"]}; box-shadow: 0 24px 60px rgba(0,0,0,0.6)">'
            f'<div style="display: flex; align-items: baseline; justify-content: space-between"><span style="font-size: 16px; font-weight: 700">Comparing drawings…</span><span style="font-family: {T["mono"]}; color: #9ec5fe">58%</span></div>'
            f'<span style="height: 6px; border-radius: 3px; background: {T["field"]}; overflow: hidden"><span style="display: block; width: 58%; height: 100%; background: {T["accent"]}"></span></span>'
            f'<div style="display: flex; flex-direction: column; gap: 10px">{st}</div>'
            f'<div style="display: flex; align-items: center; justify-content: space-between; padding-top: 12px; border-top: 1px solid {T["soft"]}"><span style="font-size: 12px; color: {T["faint"]}">About 10 seconds left · runs on this PC only</span>{btn("Cancel", "ghost", None, 30)}</div></div>')
    panes = split(pane(pane_header('Original', 'S-101_rev0.pdf', '3 pages'), old),
                  pane(pane_header('Revised', 'S-101_rev1.pdf', '3 pages', True), new))
    cmd = cmdline([('Command: <span style="color:#fff">CMP</span>', 'cmd'), ('COMPARE  S-101_rev0.pdf ↔ S-101_rev1.pdf · 3 pages', ''), ('Page 1 done · 4 changes', 'ok')],
                  'Comparing page 2 of 3…  press Esc to cancel', '', None)
    body = (titlebar([('Compare · S-101 rev0 ↔ rev1', True)], 'Working…') + compare_ribbon(disabled=True)
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel(("4", "…", "…"), 1))}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{panes}{card}{cmd}</div></div>'
            + statusbar('<span>Comparing · page 2 of 3</span>', None, f'<span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Compare · running', root(body))


def FOverlay():
    stack = (f'<div style="position: relative; width: 100%; max-width: 860px; aspect-ratio: 1191 / 842; background: #ffffff; box-shadow: 0 6px 24px rgba(0,0,0,0.5)">'
             f'<div style="position: absolute; inset: 0; opacity: 0.55"><dc-import name="Sheet" lang="en" version="old" show-marks="{{{{false}}}}" ink="#e03131" grid="#f1a3a3" hint-size="100%,100%"></dc-import></div>'
             f'<div style="position: absolute; inset: 0; mix-blend-mode: multiply"><dc-import name="Sheet" lang="en" version="new" show-marks="{{{{false}}}}" paper="transparent" ink="#1c7ed6" grid="#9cc7f2" hint-size="100%,100%"></dc-import></div></div>')
    legend = (f'<div style="position: absolute; top: 14px; left: 14px; display: flex; align-items: center; gap: 14px; padding: 6px 12px; border-radius: 7px; background: rgba(35,38,43,0.94); border: 1px solid {T["border"]}; font-size: 12px">'
              f'<span style="display: flex; align-items: center; gap: 6px"><span style="width: 14px; height: 4px; border-radius: 2px; background: #e03131"></span>Original only (removed)</span>'
              f'<span style="display: flex; align-items: center; gap: 6px"><span style="width: 14px; height: 4px; border-radius: 2px; background: #1c7ed6"></span>Revised only (added)</span>'
              f'<span style="display: flex; align-items: center; gap: 6px"><span style="width: 14px; height: 4px; border-radius: 2px; background: #3b2f5c"></span>Same in both</span></div>')
    canvas = canvas_area(stack + legend + page_nav('1 / 3', '118%'))
    props = (section('Colors') + grid2(select('Original', '<span style="display: flex; align-items: center; gap: 6px"><span style="width: 10px; height: 10px; border-radius: 2px; background: #e03131"></span>Red</span>'),
                                       select('Revised', '<span style="display: flex; align-items: center; gap: 6px"><span style="width: 10px; height: 10px; border-radius: 2px; background: #1c7ed6"></span>Blue</span>'))
             + slider('Original opacity', '55%', 55) + slider('Revised opacity', '100%', 100)
             + toggle_row('Show revision marks', False, 'Clouds and △ on top of the overlay')
             + section('Alignment') + f'<span style="font-size: 12px; color: {T["muted"]}">Auto aligned · offset 0.0, 0.0 mm · rotation 0.00°</span>'
             + f'<div style="display: flex; gap: 6px">{btn("Nudge", ic="move", extra="; flex: 1")}{btn("Reset", extra="; flex: 1")}</div>'
             + f'<span style="font-size: 11.5px; color: {T["faint"]}; line-height: 1.5">Arrow keys move the original by 0.1 mm while Nudge is on. Shift = 1 mm.</span>')
    cmd = cmdline([('Command: <span style="color:#fff">OV</span>', 'cmd'), ('OVERLAY  view on', '')], 'Original opacity', '55', None)
    body = (titlebar(DOC_TABS_COMPARE, 'Compared 2 min ago') + compare_ribbon('Overlay')
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel())}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{canvas}{cmd}</div>'
            + f'{right_panel("Overlay", props)}</div>'
            + statusbar('<span>Overlay · original 55%</span>', None, f'<span>Page 1 / 3</span><span>118%</span><span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Compare · overlay', root(body))


def FExport():
    panes, cmd = compare_body(popup=False, cmd=cmdline([('Command: <span style="color:#fff">EXPORT</span>', 'cmd')], 'EXPORT  Choose options in the dialog', '', None))
    under = (titlebar(DOC_TABS_COMPARE, 'Compared 2 min ago') + compare_ribbon()
             + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel())}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column">{panes}{changes_table()}{cmd}</div>'
             + f'{right_panel("Change", change_props())}</div>' + statusbar('<span>Compare · 4 changes on page 1</span>', None, ''))
    body_html = f'''<div style="display: flex; flex-direction: column; gap: 18px; padding: 18px">
{field("File name", "S-101_rev1_marked.pdf", True)}
<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 18px">
<div style="display: flex; flex-direction: column; gap: 10px">{section("Pages")}{radio("All pages (3)", True)}{radio("Current page")}{radio("Pages with changes only (1)")}</div>
<div style="display: flex; flex-direction: column; gap: 10px">{section("Include")}{checkbox("Revision clouds and △", True, "4 changes · 0 excluded")}{checkbox("Text highlights", True)}{checkbox("My markups", True, "Layers: REVIEW, HATCH, NOTES")}{checkbox("Change list page at the end", False)}</div>
</div>
<div style="display: flex; flex-direction: column; gap: 10px">{section("How to save marks")}
<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px">
<div style="padding: 12px; border: 2px solid {T["accent"]}; border-radius: 8px; background: {T["accentBg"]}">{radio("Editable markups", True, "Stay movable in Bluebeam / Acrobat. Recommended.")}</div>
<div style="padding: 12px; border: 1px solid {T["field"]}; border-radius: 8px">{radio("Flatten into drawing", False, "Burned in, can't be edited later.")}</div>
</div></div>
<div style="display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-radius: 6px; background: {T["panel2"]}; font-size: 12px; color: {T["muted"]}">{icon("info", 15, "#9ec5fe")}The original files are never changed. A new PDF is saved to your Downloads folder.</div>
</div>'''
    footer = f'<span style="font-size: 12px; color: {T["faint"]}">≈ 3.1 MB</span><span style="margin-left: auto"></span>{btn("Cancel", h=34)}{btn("Export PDF", "primary", "download", 34)}'
    return page('Final · Export PDF', root(under + dim_overlay() + dialog('Export marked PDF', body_html, footer, 600, 120)))


def start_body(cmd_lines=None):
    nav = [('Home', 'home', True), ('Open drawing', 'file', False), ('Compare', 'logo', False), ('Transparent PNG', 'image', False), ('Tool sets', 'tools', False), ('Settings', 'settings', False)]
    navh = ''.join(f'<button aria-current="{"page" if on else "false"}" style="height: 36px; display: flex; align-items: center; gap: 10px; padding: 0 12px; border: none; border-radius: 6px; background: {T["pressed"] if on else "transparent"}; color: {"#ffffff" if on else T["text2"]}; font: inherit; font-weight: {600 if on else 400}; text-align: left">{icon(ic, 17)}{l}</button>' for l, ic, on in nav)
    actions = [('Compare drawings', 'Put the old and new revision side by side. Changes get clouds and △ numbers.', 'logo', 'CMP', True),
               ('Open drawing', 'Review a PDF with clouds, text, hatch and AutoCAD-style drafting.', 'file', 'OPEN', False),
               ('Transparent PNG', 'Remove the white background from a stamp or signature.', 'image', 'PNG', False)]
    acts = ''.join(
        f'<button style="height: 136px; display: flex; flex-direction: column; justify-content: space-between; padding: 16px 18px; border: 1px solid {T["accent"] if p else T["border"]}; border-radius: 10px; background: {"linear-gradient(135deg, " + T["primary"] + " 0%, " + T["primaryDeep"] + " 100%)" if p else T["panel"]}; color: {T["text"]}; font: inherit; text-align: left">'
        f'<span style="display: flex; justify-content: space-between; width: 100%"><span style="width: 38px; height: 38px; border-radius: 8px; background: {"rgba(255,255,255,0.18)" if p else T["pressed"]}; display: flex; align-items: center; justify-content: center">{icon(ic, 20, "#ffffff" if p else "#9ec5fe", 1.9)}</span>'
        f'<span style="height: 20px; padding: 0 7px; border: 1px solid {"rgba(255,255,255,0.5)" if p else T["field"]}; border-radius: 4px; font-family: {T["mono"]}; font-size: 11px; display: flex; align-items: center">{k}</span></span>'
        f'<span style="display: flex; flex-direction: column; gap: 4px"><span style="font-size: 15px; font-weight: 700">{t}</span><span style="font-size: 12px; color: {"rgba(255,255,255,0.85)" if p else T["muted"]}; line-height: 1.45">{d}</span></span></button>'
        for t, d, ic, k, p in actions)
    sketches = ['M14 14h90M14 40h90M14 66h90M14 14v52M50 14v52M86 14v52M104 14v52', 'M14 18h70v48H14zM84 18h30v24H84zM30 18v48M14 42h70',
                'M20 70V24l40-12 40 12v46M20 46h80M40 70V46M80 70V46', 'M14 14h100v56H14zM14 34h100M40 14v56M80 14v56']
    recent = [('S-101_Structural_Plan_rev1.pdf', '3 pp · now', '5 markups', '#ff8787', 0), ('S-101_Structural_Plan_rev0.pdf', '3 pp · yesterday', '', '', 0),
              ('A-201_Elevations_rev2.pdf', '2 pp · Oct 14', '2 markups', '#ff8787', 2), ('M-101_Mechanical_Plan.pdf', '4 pp · Oct 10', 'Approved', T['green'], 3)]
    rec = ''.join(
        f'<button style="display: flex; flex-direction: column; padding: 0; border: 1px solid {T["border"]}; border-radius: 8px; background: {T["panel"]}; color: {T["text"]}; font: inherit; text-align: left; overflow: hidden">'
        f'<span style="height: 96px; display: block; padding: 10px; background: #f4f4f1; box-sizing: border-box"><svg aria-hidden="true" viewBox="0 0 160 88" width="100%" height="100%"><rect x="1" y="1" width="158" height="86" fill="none" stroke="#3b3f46"></rect><path d="{sketches[s]}" fill="none" stroke="#3b3f46"></path></svg></span>'
        f'<span style="display: flex; flex-direction: column; gap: 3px; padding: 9px 10px"><span style="font-size: 12.5px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{n}</span>'
        f'<span style="display: flex; justify-content: space-between; gap: 6px; font-size: 11.5px; color: {T["faint"]}; white-space: nowrap"><span>{m}</span><span style="color: {c}">{tag}</span></span></span></button>'
        for n, m, tag, c, s in recent)
    comps = [('S-101 rev0 ↔ rev1', '3 pages · just now', 4, 1), ('A-201 rev1 ↔ rev2', '2 pages · Oct 14', 7, 7), ('M-101 rev3 ↔ rev4', '4 pages · Oct 10 · 1 new page', 12, 9)]
    comp = ''.join(
        f'<button style="display: flex; align-items: center; gap: 12px; padding: 11px 14px; border: none; border-bottom: 1px solid {T["soft"]}; background: transparent; color: {T["text"]}; font: inherit; text-align: left">'
        f'<span style="width: 32px; height: 32px; flex: none; border-radius: 7px; background: {T["accentBg"]}; display: flex; align-items: center; justify-content: center">{icon("logo", 16, "#6ea8fe")}</span>'
        f'<span style="flex: 1; display: flex; flex-direction: column; gap: 2px"><span style="font-size: 12.5px; font-weight: 600">{n}</span><span style="font-size: 11.5px; color: {T["faint"]}">{m}</span></span>'
        f'<span style="padding: 2px 6px; border-radius: 4px; background: rgba(229,56,59,0.16); color: #ff8787; font-size: 11.5px">{c} changes</span>'
        f'<span style="padding: 2px 6px; border-radius: 4px; background: rgba(140,233,154,0.12); color: {T["green"]}; font-size: 11.5px">{r} reviewed</span></button>'
        for n, m, c, r in comps)
    main = f'''<main style="flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 20px; padding: 24px 30px; overflow: hidden">
<div style="display: flex; align-items: flex-end; justify-content: space-between"><div style="display: flex; flex-direction: column; gap: 4px"><h1 style="margin: 0; font-size: 22px; font-weight: 700">What would you like to do?</h1><span style="color: {T["muted"]}">Drop files anywhere in this window, or type a command below.</span></div>
<span style="font-size: 12px; color: {T["faint"]}">Engine ready · runs on this PC only</span></div>
<div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px">{acts}</div>
<div style="flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr); gap: 18px">
<section aria-label="Recent files" style="display: flex; flex-direction: column; gap: 10px"><h2 style="margin: 0; font-size: 14px; font-weight: 600">Recent files</h2><div style="display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px">{rec}</div>
<div style="height: 120px; display: flex; align-items: center; justify-content: center; gap: 12px; border: 1.5px dashed {T["field"]}; border-radius: 10px; color: {T["muted"]}">{icon("upload", 20)}<span><b style="color: {T["text"]}">Drop PDF or images here</b> · drop two to compare right away</span></div></section>
<section aria-label="Recent comparisons" style="display: flex; flex-direction: column; gap: 10px"><h2 style="margin: 0; font-size: 14px; font-weight: 600">Recent comparisons</h2><div style="display: flex; flex-direction: column; border: 1px solid {T["border"]}; border-radius: 8px; background: {T["panel"]}; overflow: hidden">{comp}</div></section>
</div></main>'''
    cmd = cmdline(cmd_lines or [('Sam Drawing Tools 1.0 · type a command or ? for the list', ''), ('Command: <span style="color:#fff">?</span>  opens the full command list', '')], 'Command:', '', None)
    body = (titlebar([('Home', True)], '')
            + f'<div style="flex: 1; display: flex; min-height: 0"><nav aria-label="Start" style="width: 212px; flex: none; display: flex; flex-direction: column; gap: 2px; padding: 16px 10px; background: {T["panel"]}; border-right: 1px solid {T["border"]}">{navh}</nav>{main}</div>'
            + cmd + statusbar('<span>Ready</span>', None, f'<span>127.0.0.1:5180</span><span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return body


def FStart():
    return page('Final · Home', root(start_body()))


def FOpen():
    """Open drawing dialog (DESIGN-SPEC 3-2): the app's own file browser, served by the local engine."""
    under = start_body([('Command: <span style="color:#fff">OPEN</span>', 'cmd'), ('OPEN  Choose a file in the dialog', '')])
    places = [('Recent folders', 'clock', False), ('Desktop', 'monitor', False), ('Documents', 'file', False), ('Downloads', 'download', False),
              ('Projects', 'folder', True), ('C:', 'hdd', False), ('D:', 'hdd', False)]
    placeh = ''.join(
        f'<button style="height: 32px; display: flex; align-items: center; gap: 9px; padding: 0 10px; border: none; border-radius: 2px; background: {T["pressed"] if on else "transparent"}; color: {"#ffffff" if on else T["text2"]}; font: inherit; font-size: 12.5px; font-weight: {600 if on else 400}; text-align: left">{icon(ic, 15)}{l}</button>'
        for l, ic, on in places)
    crumbs = ['D:', 'Projects', 'Sam Office New Build', 'Structural']
    crumbh = f'<span style="color: {T["faint"]}">{icon("chevright", 13)}</span>'.join(
        f'<span style="color: {"#ffffff" if i == len(crumbs) - 1 else T["text2"]}; font-weight: {600 if i == len(crumbs) - 1 else 400}">{c}</span>' for i, c in enumerate(crumbs))
    rows = [('folder', 'Superseded', '', 'Oct 2', '', False, False),
            ('file', 'S-101_Structural_Plan_rev0.pdf', '3 pages', 'Sep 1', '2.4 MB', False, False),
            ('file', 'S-101_Structural_Plan_rev1.pdf', '3 pages · 5 markups', 'Oct 14', '2.6 MB', True, False),
            ('file', 'S-201_Sections_rev0.pdf', '2 pages', 'Sep 1', '1.8 MB', False, False),
            ('file', 'S-301_Details_rev0.pdf', '6 pages', 'Sep 3', '4.1 MB', False, False),
            ('image', 'site_photo_stamp.jpg', 'image', 'Oct 15', '0.9 MB', False, False),
            ('file', 'S-501_Foundation_rev2.pdf', 'password-protected', 'Oct 9', '3.0 MB', False, True)]
    rowh = ''.join(
        f'<div role="row" aria-selected="{"true" if sel else "false"}" style="height: 34px; display: grid; grid-template-columns: 24px minmax(0, 1fr) 140px 70px 64px; column-gap: 10px; align-items: center; padding: 0 12px; border-bottom: 1px solid {T["soft"]}; '
        f'background: {T["accentBg"] if sel else "transparent"}; box-shadow: {"inset 2px 0 0 " + T["accent"] if sel else "none"}; font-size: 12.5px; color: {T["text"]}">'
        f'<span style="color: {"#ffd43b" if ic == "folder" else ("#ff8787" if warn else "#9ec5fe")}">{icon(ic, 15)}</span>'
        f'<span style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-weight: {600 if sel else 400}">{n}</span>'
        f'<span style="color: {"#ff8787" if warn else T["faint"]}; font-size: 11.5px">{meta}</span>'
        f'<span style="color: {T["faint"]}; font-size: 11.5px">{d}</span><span style="color: {T["faint"]}; font-size: 11.5px; text-align: right">{sz}</span></div>'
        for ic, n, meta, d, sz, sel, warn in rows)
    head = (f'<div role="row" style="height: 30px; display: grid; grid-template-columns: 24px minmax(0, 1fr) 140px 70px 64px; column-gap: 10px; align-items: center; padding: 0 12px; '
            f'background: {T["panel2"]}; border-bottom: 1px solid {T["border"]}; font-size: 11.5px; color: {T["muted"]}"><span></span><span>Name</span><span>Info</span><span>Modified</span><span style="text-align: right">Size</span></div>')
    thumb = (f'<div style="height: 128px; padding: 10px; background: #f4f4f1; border: 1px solid {T["border"]}; border-radius: 2px; box-sizing: border-box">'
             f'<svg aria-hidden="true" viewBox="0 0 160 100" width="100%" height="100%"><rect x="1" y="1" width="158" height="98" fill="none" stroke="#3b3f46"></rect>'
             f'<path d="M14 14h120M14 44h120M14 74h120M14 14v60M54 14v60M94 14v60M134 14v60" fill="none" stroke="#3b3f46"></path><rect x="108" y="80" width="48" height="16" fill="none" stroke="#3b3f46"></rect></svg></div>')
    preview = (f'<aside aria-label="Preview" style="width: 200px; flex: none; display: flex; flex-direction: column; gap: 10px; padding: 12px; border-left: 1px solid {T["soft"]}">{thumb}'
               f'<span style="font-size: 12.5px; font-weight: 600; word-break: break-all">S-101_Structural_Plan_rev1.pdf</span>'
               f'<span style="font-size: 11.5px; color: {T["muted"]}; line-height: 1.6">3 pages · A1<br>5 markups · layers REVIEW, NOTES<br>Modified Oct 14, 16:20</span>'
               f'<span style="font-size: 11.5px; color: {T["faint"]}; line-height: 1.5">Saving with Ctrl+S writes back to this file. A .bak copy is kept the first time.</span></aside>')
    body_html = f'''<div style="display: flex; flex-direction: column">
<div style="height: 44px; display: flex; align-items: center; gap: 8px; padding: 0 12px; border-bottom: 1px solid {T["soft"]}">{icon_btn("arrowup", "Up one folder", 28)}
<div style="flex: 1; min-width: 0; height: 30px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: 1px solid {T["field"]}; border-radius: 2px; background: {T["app"]}; font-size: 12.5px">{icon("folder", 14, T["faint"])}{crumbh}</div>
<div style="width: 180px; height: 30px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: 1px solid {T["field"]}; border-radius: 2px; background: {T["app"]}; color: {T["faint"]}; font-size: 12.5px">{icon("search", 14)}Filter this folder</div></div>
<div style="height: 360px; display: flex; min-height: 0">
<nav aria-label="Places" style="width: 168px; flex: none; display: flex; flex-direction: column; gap: 2px; padding: 10px 8px; border-right: 1px solid {T["soft"]}; background: {T["panel2"]}">{placeh}</nav>
<div role="grid" aria-label="Files" style="flex: 1; min-width: 0; display: flex; flex-direction: column; overflow: hidden">{head}{rowh}</div>{preview}</div>
<div style="display: flex; align-items: center; gap: 10px; padding: 12px 18px; border-top: 1px solid {T["soft"]}">
<span style="font-size: 12px; color: {T["muted"]}">Path</span>
<div style="flex: 1; height: 30px; display: flex; align-items: center; padding: 0 10px; border: 1px solid {T["accent"]}; border-radius: 2px; background: {T["app"]}; font-family: {T["mono"]}; font-size: 12px">D:\\Projects\\Sam Office New Build\\Structural\\S-101_Structural_Plan_rev1.pdf</div>
<div style="height: 30px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: 1px solid {T["field"]}; border-radius: 2px; font-size: 12.5px; color: {T["text2"]}">PDF and images{icon("chevdown", 14)}</div></div></div>'''
    footer = (f'<span style="display: flex; align-items: center; gap: 6px; font-size: 12px; color: {T["faint"]}">{icon("info", 14)}Files are read from this PC only. Dragging files into the window also works.</span>'
              f'<span style="margin-left: auto"></span>{btn("Cancel", h=34)}{btn("Open", "primary", "folder", 34)}')
    return page('Final · Open drawing', root(under + dim_overlay() + dialog("Open drawing", body_html, footer, 1000, 90)))


def review_under():
    canvas = canvas_area(sheet('version="new" review="{{true}}" show-marks="{{false}}"', '', 800))
    return (titlebar(DOC_TABS_REVIEW, 'Autosaved 1 min ago', True) + draw_ribbon('Select')
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Layers", layers_panel())}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{canvas}'
            + cmdline([('Command: <span style="color:#fff">OP</span>', 'cmd'), ('OPTIONS', '')], 'Command:', '', None)
            + f'</div>{right_panel("Properties", "")}</div>' + review_status())


def FSettings():
    nav = ['Language & units', 'Object snap', 'Drafting', 'Command line', 'Compare', 'Shortcuts']
    navh = ''.join(f'<button style="height: 34px; padding: 0 12px; border: none; border-radius: 6px; background: {T["pressed"] if i == 0 else "transparent"}; color: {"#ffffff" if i == 0 else T["text2"]}; font: inherit; font-weight: {600 if i == 0 else 400}; text-align: left">{n}</button>' for i, n in enumerate(nav))
    snaps = [('Endpoint', 'END', 'end', True), ('Midpoint', 'MID', 'mid', True), ('Intersection', 'INT', 'int', True), ('Center', 'CEN', 'cen', True),
             ('Quadrant', 'QUA', 'cen', False), ('Perpendicular', 'PER', 'end', True), ('Nearest', 'NEA', 'end', True)]

    def glyph(kind):
        s = {'end': '<rect x="3" y="3" width="12" height="12" fill="none" stroke="#f59f00" stroke-width="2"></rect>',
             'mid': '<polygon points="9,2 16,15 2,15" fill="none" stroke="#f59f00" stroke-width="2"></polygon>',
             'int': '<path d="M3 3l12 12M3 15 15 3" stroke="#f59f00" stroke-width="2.2"></path>',
             'cen': '<circle cx="9" cy="9" r="6" fill="none" stroke="#f59f00" stroke-width="2"></circle>'}[kind]
        return f'<svg aria-hidden="true" width="18" height="18" viewBox="0 0 18 18">{s}</svg>'
    snap_rows = ''.join(f'<div style="display: flex; align-items: center; gap: 10px">{checkbox("", on)}{glyph(k)}<span style="flex: 1; font-size: 13px; color: {T["text2"]}">{n}</span><span style="font-family: {T["mono"]}; font-size: 11.5px; color: {T["faint"]}">{a}</span></div>' for n, a, k, on in snaps)
    aliases = [('L', 'LINE'), ('PL', 'PLINE'), ('C', 'CIRCLE'), ('REC', 'RECTANG'), ('RC', 'REVCLOUD'), ('H', 'HATCH'), ('T', 'TEXT'), ('M', 'MOVE'), ('CO', 'COPY'), ('E', 'ERASE'), ('LA', 'LAYER'), ('DI', 'DIST')]
    al = ''.join(f'<span style="display: flex; gap: 8px"><span style="width: 34px; color: {T["accent"]}">{a}</span><span style="color: {T["text2"]}">{c}</span></span>' for a, c in aliases)
    lang_list = ''.join(
        f'<div style="height: 32px; display: flex; align-items: center; gap: 10px; padding: 0 10px; border-radius: 5px; background: {T["accentBg"] if on else "transparent"}">{radio("", on)}<span style="flex: 1">{n}</span><span style="font-size: 11.5px; color: {T["faint"]}">{s}</span></div>'
        for n, s, on in [('English', 'Default', True), ('한국어 (Korean)', '100% translated', False), ('日本語 (Japanese)', 'Planned', False)])
    body_html = f'''<div style="display: flex; min-height: 0; height: 548px">
<nav aria-label="Settings sections" style="width: 190px; flex: none; display: flex; flex-direction: column; gap: 2px; padding: 12px; border-right: 1px solid {T["soft"]}">{navh}</nav>
<div style="flex: 1; display: grid; grid-template-columns: 1fr 1fr; gap: 24px; padding: 18px 20px; overflow: hidden">
<div style="display: flex; flex-direction: column; gap: 14px">
{section("Language & units")}
<div style="display: flex; flex-direction: column; gap: 2px; padding: 4px; border: 1px solid {T["border"]}; border-radius: 6px">{lang_list}</div>
<span style="font-size: 11.5px; color: {T["faint"]}; line-height: 1.5">Menus and messages change right away. Command names (LINE, HATCH…) stay in English in every language, like AutoCAD.</span>
{grid2(select("Units", "Millimeters"), select("Decimals", "0.0"))}
{section("Command line")}
<div style="display: grid; grid-template-columns: 1fr 1fr; gap: 6px 14px; padding: 10px 12px; border: 1px solid {T["border"]}; border-radius: 6px; font-family: {T["mono"]}; font-size: 12px">{al}</div>
<div style="display: flex; gap: 8px">{btn("Edit aliases…", h=30)}{btn("Import from AutoCAD .pgp", h=30)}</div>
</div>
<div style="display: flex; flex-direction: column; gap: 14px">
{section("Object snap (OSNAP · F3)")}
<div style="display: flex; flex-direction: column; gap: 10px">{snap_rows}</div>
{slider("Snap distance (aperture)", "10 px", 40)}
{toggle_row("Snap to drawing lines from the PDF", True, "Works on CAD-exported PDFs. Scanned pages snap to markups only.")}
{section("Drafting")}
{grid2(field("Grid spacing", "1000 mm", True), select("Drawing scale", "1 : 100"))}
{toggle_row("Shift toggles ORTHO while drawing", True)}
{toggle_row("Dark drawing view", False, "Shows the drawing like AutoCAD model space (inverted colors). Exported PDFs stay white. Command: DV")}
</div></div></div>'''
    footer = f'{btn("Reset to defaults", h=34)}<span style="margin-left: auto"></span>{btn("Cancel", h=34)}{btn("Save", "primary", None, 34)}'
    return page('Final · Settings', root(review_under() + dim_overlay() + dialog('Settings', body_html, footer, 880, 90)))


def FError():
    old = sheet('version="old" show-marks="{{false}}"')
    err = (f'<div role="alert" style="width: 380px; display: flex; flex-direction: column; gap: 12px; padding: 22px; border: 1px solid rgba(255,135,135,0.4); border-radius: 12px; background: {T["panel"]}">'
           f'<span style="width: 44px; height: 44px; border-radius: 50%; background: rgba(229,56,59,0.16); display: flex; align-items: center; justify-content: center">{icon("alert", 22, T["redText"])}</span>'
           f'<span style="font-size: 16px; font-weight: 700">Can\'t open S-101_rev1.pdf</span>'
           f'<span style="color: {T["text2"]}; line-height: 1.55">The file is password-protected. Open it in your PDF app, remove the password (or save a copy without it), then try again.</span>'
           f'<div style="display: flex; gap: 8px">{btn("Choose another file", "primary", "folder", 34)}{btn("Try again", h=34)}</div>'
           f'<span style="font-size: 11.5px; color: {T["faint"]}">Error code PDF-ENC-01 · nothing was uploaded anywhere</span></div>')
    panes = split(pane(pane_header('Original', 'S-101_rev0.pdf', '3 pages · 2.4 MB'), old),
                  pane(pane_header('Revised', 'S-101_rev1.pdf', 'Failed', True), err))
    toast = (f'<div role="status" style="position: absolute; z-index: 6; top: 48px; right: 16px; width: 360px; display: flex; gap: 12px; padding: 14px; border: 1px solid {T["field"]}; border-left: 3px solid {T["warn"]}; border-radius: 8px; background: {T["panel"]}; box-shadow: 0 12px 30px rgba(0,0,0,0.5)">'
             f'<span style="color: {T["warn"]}; display: flex">{icon("info", 18, T["warn"])}</span>'
             f'<span style="flex: 1; display: flex; flex-direction: column; gap: 4px"><span style="font-weight: 600">Page 2 looks scanned</span>'
             f'<span style="font-size: 12px; color: {T["muted"]}; line-height: 1.5">It has no CAD lines, so OSNAP will only snap to your own markups on this page. Comparing still works.</span>'
             f'<span style="display: flex; gap: 14px; font-size: 12px"><a href="#" style="text-decoration: none">Got it</a><a href="#" style="text-decoration: none; color: {T["muted"]}">Don\'t show again</a></span></span>'
             f'{icon_btn("x", "Dismiss", 24, isz=13)}</div>')
    empty = (f'<section aria-label="Changes" style="height: 120px; flex: none; display: flex; flex-direction: column; background: {T["panel"]}; border-top: 1px solid {T["border"]}">'
             f'<div style="height: 32px; display: flex; align-items: center; padding: 0 12px; border-bottom: 1px solid {T["soft"]}; font-size: 12px; font-weight: 600">Changes</div>'
             f'<div style="flex: 1; display: flex; align-items: center; justify-content: center; gap: 10px; color: {T["muted"]}">{icon("table", 18)}<span>No changes yet. They will appear here after a successful comparison.</span></div></section>')
    cmd = cmdline([('Command: <span style="color:#fff">LNE</span>', 'cmd'), ('Unknown command "LNE". Did you mean LINE (L)? Press Tab to use it.', 'err'),
                   ('COMPARE  Could not read S-101_rev1.pdf (password-protected)', 'err')], 'Command:', '', None)
    body = (titlebar([('Compare · new', True)], 'Not saved') + compare_ribbon(disabled=True)
            + f'<div style="flex: 1; display: flex; min-height: 0">{left_panel("Pages", pages_panel(("–", "–", "–"), 0))}<div style="flex: 1; min-width: 0; display: flex; flex-direction: column; position: relative">{panes}{toast}{empty}{cmd}</div></div>'
            + statusbar(f'<span style="display: flex; align-items: center; gap: 6px">{icon("alert", 13, "#ffffff")}1 file could not be opened</span>', None, f'<span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Error & empty states', root(body))


def stamp(bg):
    c = '#d6336c'
    return (f'<svg role="img" aria-label="Approval stamp" viewBox="0 0 300 300" width="300" height="300" style="display: block; {bg}">'
            f'<circle cx="150" cy="150" r="118" fill="none" stroke="{c}" stroke-width="7"></circle><circle cx="150" cy="150" r="100" fill="none" stroke="{c}" stroke-width="2.5"></circle>'
            f'<text x="150" y="132" fill="{c}" font-family="Pretendard, sans-serif" font-size="34" font-weight="700" text-anchor="middle">APPROVED</text>'
            f'<line x1="70" y1="148" x2="230" y2="148" stroke="{c}" stroke-width="2.5"></line>'
            f'<text x="150" y="182" fill="{c}" font-family="Pretendard, sans-serif" font-size="22" font-weight="600" text-anchor="middle">SAM KIM</text>'
            f'<text x="150" y="210" fill="{c}" font-family="JetBrains Mono, monospace" font-size="15" text-anchor="middle">2026.10.16</text></svg>')


def FPng():
    groups = [('Image', [('Open', 'folder', {'key': 'OPEN'}), ('Paste', 'copy', {'key': 'Ctrl+V'})]),
              ('Background', [('Remove white', 'wand', {'on': True, 'key': 'BG'}), ('Pick color', 'pointer', {'key': 'PICK'})]),
              ('Edges', [('Smooth', 'circle', {'key': 'SMOOTH'}), ('Crop', 'crop', {'on': True, 'key': 'CROP'})]),
              ('Output', [('Download PNG', 'download', {'key': 'SAVE'}), ('Copy', 'copy', {'key': 'Ctrl+C'})])]
    rib = ribbon('Tools', groups, '')
    checker = 'background-color: #ffffff; background-image: linear-gradient(45deg, #d9dce1 25%, transparent 25%), linear-gradient(-45deg, #d9dce1 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #d9dce1 75%), linear-gradient(-45deg, transparent 75%, #d9dce1 75%); background-size: 20px 20px; background-position: 0 0, 0 10px, 10px -10px, -10px 0'
    before = f'<div style="padding: 40px 60px; background: #fbfbf7; box-shadow: 0 6px 24px rgba(0,0,0,0.5)">{stamp("")}</div>'
    after = f'<div style="padding: 10px; {checker}; box-shadow: 0 6px 24px rgba(0,0,0,0.5)">{stamp("")}</div>'
    panes = split(pane(pane_header('Before', 'stamp_scan.jpg', '1200 × 900 · white background'), before),
                  pane(pane_header('After', 'stamp_scan.png', '640 × 640 · transparent', True), after))
    props = (section('Background') + slider('Tolerance', '18', 36) + f'<span style="font-size: 11.5px; color: {T["faint"]}; margin-top: -6px">Higher removes more near-white pixels (paper shadow, scan noise).</span>'
             + toggle_row('Remove only from the edges', False, 'Keeps white inside letters')
             + section('Edges') + slider('Smoothing', '1 px', 20) + toggle_row('Crop to content', True) + field('Padding', '10 px', True)
             + section('Output') + grid2(select('Size', 'Original'), select('Format', 'PNG 32-bit'))
             + f'<div style="margin-top: auto">{btn("Download PNG", "primary", "download", 38, "; width: 100%")}</div>')
    cmd = cmdline([('Command: <span style="color:#fff">PNG</span>', 'cmd'), ('TRANSPARENT PNG  stamp_scan.jpg loaded · white removed (tolerance 12)', ''), ('Crop to content · 640 × 640', 'ok')],
                  'Tolerance &lt;12&gt;:', '18', None)
    body = (titlebar([('Transparent PNG · stamp_scan.jpg', True)], 'Ready to download') + rib
            + f'<div style="flex: 1; display: flex; min-height: 0"><div style="flex: 1; min-width: 0; display: flex; flex-direction: column">{panes}{cmd}</div>{right_panel("Transparent PNG", props, 280)}</div>'
            + statusbar('<span>Image processed on this PC only</span>', None, f'<span>Zoom 100%</span><span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, "#ffffff")}EN</span>'))
    return page('Final · Transparent PNG', root(body))


SCREENS = {'FReviewLine': FReviewLine, 'FReviewHatch': FReviewHatch, 'FFocus': FFocus,
           'FCompare': FCompare, 'FCompareEmpty': FCompareEmpty, 'FCompareRunning': FCompareRunning,
           'FOverlay': FOverlay, 'FExport': FExport, 'FStart': FStart,
           'FSettings': FSettings, 'FError': FError, 'FPng': FPng, 'FOpen': FOpen}

if __name__ == '__main__':
    name = sys.argv[1]
    skin = sys.argv[2] if len(sys.argv) > 2 else 'Revu'
    out = sys.argv[3] if len(sys.argv) > 3 else name
    apply_skin(skin)
    html_out = SCREENS[name]()
    open(OUT + out + '.dc.html', 'w', encoding='utf-8').write(html_out)
    print(out, len(html_out))
