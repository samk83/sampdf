# Shared building blocks for the final-round artboards (A + Focus, English).
import html

FONT_FACES = """@font-face{font-family:'Pretendard';src:url(/_blob/9e69a201039a9830f0a4e2a85ce0c0bd) format('woff2');font-weight:400;font-display:swap}
@font-face{font-family:'Pretendard';src:url(/_blob/1901157fa16f20fc8ca2fac9663ca0cc) format('woff2');font-weight:500;font-display:swap}
@font-face{font-family:'Pretendard';src:url(/_blob/41549062fe2021d039acc0ab7891498a) format('woff2');font-weight:600;font-display:swap}
@font-face{font-family:'Pretendard';src:url(/_blob/c968216bb8b9f7129efe7e8b75e16118) format('woff2');font-weight:700;font-display:swap}"""

# Design tokens (become design/tokens.css once confirmed).
T = dict(
    chrome='#141619', app='#1b1d21', panel='#23262b', panel2='#1f2226', canvas='#15171b',
    border='#363b42', soft='#2b2f35', field='#454b54',
    text='#e6e8eb', text2='#c9ced6', muted='#a3a9b3', faint='#8c939e',
    accent='#3d8bfd', primary='#1f6feb', accentBg='rgba(61,139,253,0.16)', pressed='#2f3640',
    red='#e5383b', redText='#ff8787', yellow='#ffd43b', green='#8ce99a', warn='#ffe066',
    snap='#f59f00', mono="'JetBrains Mono', monospace",
)

I = {
    'logo': 'M6 3a3 3 0 1 0 0 6a3 3 0 1 0 0-6 M18 15a3 3 0 1 0 0 6a3 3 0 1 0 0-6 M13 6h3a2 2 0 0 1 2 2v7 M11 18H8a2 2 0 0 1-2-2V9',
    'pointer': 'M4.037 4.688a.495.495 0 0 1 .651-.651l16 6.5a.5.5 0 0 1-.063.947l-6.124 1.58a2 2 0 0 0-1.438 1.435l-1.579 6.126a.5.5 0 0 1-.947.063z',
    'hand': 'M18 11V6a2 2 0 0 0-4 0 M14 10V4a2 2 0 0 0-4 0v2 M10 10.5V6a2 2 0 0 0-4 0v8 M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15',
    'line': 'M5 19 19 5',
    'pline': 'M3 17l6-6 4 4 8-8',
    'circle': 'M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20',
    'rect': 'M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z',
    'cloud': 'M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z',
    'hatch': 'M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M3 13 13 3 M3 21 21 3 M11 21 21 11',
    'text': 'M4 7V4h16v3 M9 20h6 M12 4v16',
    'callout': 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
    'hl': 'm9 11-6 6v3h9l3-3 m22 12-4.6 4.6a2 2 0 0 1-2.8 0l-5.2-5.2a2 2 0 0 1 0-2.8L14 4',
    'tri': 'M13.73 4a2 2 0 0 0-3.46 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z',
    'move': 'M12 2v20 M2 12h20 M5 9l-3 3 3 3 M9 5l3-3 3 3 M15 19l-3 3-3-3 M19 9l3 3-3 3',
    'copy': 'M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2',
    'erase': 'm7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21 M22 21H7 M5 11l9 9',
    'layers': 'M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z M2 12a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12 M2 17a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17',
    'magnet': 'm6 15-4-4 6.75-6.77a7.79 7.79 0 0 1 11 11L13 22l-4-4 6.39-6.36a2.14 2.14 0 0 0-3-3L6 15 M5 8l4 4 M12 15l4 4',
    'ortho': 'M3 3v18h18',
    'grid': 'M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18 M15 3v18',
    'ruler': 'M21.3 15.3a2.4 2.4 0 0 1 0 3.4l-2.6 2.6a2.4 2.4 0 0 1-3.4 0L2.7 8.7a2.41 2.41 0 0 1 0-3.4l2.6-2.6a2.41 2.41 0 0 1 3.4 0Z M14.5 12.5l2-2 M11.5 9.5l2-2 M8.5 6.5l2-2 M17.5 15.5l2-2',
    'file': 'M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M14 2v4a2 2 0 0 0 2 2h4',
    'folder': 'M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z',
    'swap': 'M8 3 4 7l4 4 M4 7h16 M16 21l4-4-4-4 M20 17H4',
    'split': 'M3 3h18v18H3z M12 3v18',
    'overlay': 'M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z M22 12.65l-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65',
    'link': 'M9 17H7A5 5 0 0 1 7 7h2 M15 7h2a5 5 0 1 1 0 10h-2 M8 12h8',
    'prev': 'm15 18-6-6 6-6', 'next': 'm9 18 6-6-6-6',
    'check': 'M20 6 9 17l-5-5',
    'download': 'M12 15V3 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5',
    'table': 'M12 3v18 M3 3h18v18H3z M3 9h18 M3 15h18',
    'image': 'M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M9 7a2 2 0 1 0 0 4a2 2 0 1 0 0-4 M21 15l-3.1-3.1a2 2 0 0 0-2.8 0L6 21',
    'home': 'M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8 M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z',
    'settings': 'M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6',
    'tools': 'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z',
    'focus': 'M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2',
    'undo': 'M9 14 4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11',
    'redo': 'm15 14 5-5-5-5 M20 9H9.5A5.5 5.5 0 0 0 4 14.5A5.5 5.5 0 0 0 9.5 20H13',
    'save': 'M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7 M7 3v4a1 1 0 0 0 1 1h7',
    'eye': 'M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0 M12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6',
    'eyeoff': 'M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49 M14.084 14.158a3 3 0 0 1-4.242-4.242 M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143 M2 2l20 20',
    'lock': 'M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M7 11V7a5 5 0 0 1 10 0v4',
    'unlock': 'M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M7 11V7a5 5 0 0 1 9.9-1',
    'plus': 'M5 12h14 M12 5v14', 'minus': 'M5 12h14',
    'x': 'M18 6 6 18 M6 6l12 12',
    'terminal': 'm4 17 6-6-6-6 M12 19h8',
    'chevup': 'm18 15-6-6-6 6', 'chevdown': 'm6 9 6 6 6-6',
    'search': 'M11 3a8 8 0 1 0 0 16a8 8 0 1 0 0-16 M21 21l-4.3-4.3',
    'upload': 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M17 8l-5-5-5 5 M12 3v12',
    'alert': 'm21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3 M12 9v4 M12 17h.01',
    'info': 'M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M12 16v-4 M12 8h.01',
    'globe': 'M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M12 2a14.5 14.5 0 0 0 0 20a14.5 14.5 0 0 0 0-20 M2 12h20',
    'list': 'M3 6h.01 M8 6h13 M3 12h.01 M8 12h13 M3 18h.01 M8 18h13',
    'msg': 'M7.9 20A9 9 0 1 0 4 16.1L2 22Z',
    'star': 'M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z',
    'crop': 'M6 2v14a2 2 0 0 0 2 2h14 M18 22V8a2 2 0 0 0-2-2H2',
    'wand': 'M15 4V2 M15 16v-2 M8 9h2 M20 9h2 M17.8 11.8 19 13 M15 9h.01 M17.8 6.2 19 5 M3 21l9-9 M12.2 6.2 11 5',
}

BASE = dict(T, status=T['primary'], statusFg='#ffffff', cross='#1b1f24', primaryDeep='#1a5bc4', panelRGB='35,38,43', invert=False, floatCmd=False, sharp=False, skin='Revu')
SKINS = {
    'Revu': {},
    # AutoCAD-inspired slate (reinterpreted, not Autodesk colors)
    'S1': dict(chrome='#1a1f26', app='#232a33', panel='#2a323d', panel2='#252c36', canvas='#1c2229',
               border='#3a4553', soft='#313a46', field='#4a5666', text='#e3e8ee', text2='#c3ccd6', muted='#a0acba', faint='#93a0ae',
               accent='#2ba8e0', primary='#126fa8', accentBg='rgba(43,168,224,0.16)', pressed='#35404d',
               status='#1a1f26', statusFg='#c3ccd6', primaryDeep='#0d5584', panelRGB='42,50,61', floatCmd=True, sharp=True, skin='S1'),
}
SKINS['S2'] = dict(SKINS['S1'], canvas='#0f1216', invert=True, cross='#e3e8ee', skin='S2')


def apply_skin(name):
    T.clear(); T.update(BASE); T.update(SKINS[name])


esc = html.escape


def icon(name, size=16, color='currentColor', sw=2):
    d = I[name]
    paths = ''.join(f'<path d="{p.strip() if p.strip().startswith(("M", "m")) else "M" + p.strip()}"></path>' for p in split_path(d))
    return (f'<svg aria-hidden="true" width="{size}" height="{size}" viewBox="0 0 24 24" fill="none" stroke="{color}" '
            f'stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round">{paths}</svg>')


def split_path(d):
    # Lucide icons are multi-path; we store them as one string with ' M'/' m' separators.
    out, cur = [], ''
    tokens = d.replace(' m', ' \x00m').replace(' M', ' \x00M').split('\x00')
    for t in tokens:
        if t.strip():
            out.append(t.strip())
    return out


def page(title, body, extra_css=''):
    body = body.replace('rgba(35,38,43,', 'rgba(' + T['panelRGB'] + ',')
    if T['sharp']:
        body = sharpen(body)
    return f'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{esc(title)}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600&amp;display=swap">
<style>
{FONT_FACES}
body{{margin:0}}
a{{color:#6ea8fe}}a:hover{{color:#9ec5fe}}
{extra_css}
</style>
</helmet>
{body}
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1440,"height":900}}}}'>
class Component extends DCLogic {{
  renderVals() {{
    return {{}};
  }}
}}
</script>
</body>
</html>
'''


def root(inner, extra=''):
    return (f'<div style="position: relative; height: 900px; display: flex; flex-direction: column; background: {T["app"]}; '
            f'color: {T["text"]}; font-family: \'Pretendard\', sans-serif; font-size: 13px; overflow: hidden{extra}">\n{inner}\n</div>')


def icon_btn(name, label, size=28, fg=None, bg='transparent', isz=15):
    fg = fg or T['text2']
    return (f'<button aria-label="{esc(label)}" title="{esc(label)}" style="width: {size}px; height: {size - 2}px; border: none; border-radius: 5px; '
            f'background: {bg}; color: {fg}; display: flex; align-items: center; justify-content: center">{icon(name, isz)}</button>')


def titlebar(tabs, right_note='Saved', unsaved=False):
    t = []
    for i, (label, active) in enumerate(tabs):
        dot = f' <span style="color: {T["yellow"]}" aria-label="Unsaved changes">●</span>' if (active and unsaved) else ''
        if active:
            t.append(f'<button role="tab" aria-selected="true" style="display: flex; align-items: center; gap: 8px; height: 28px; padding: 0 12px; border: none; border-radius: 6px 6px 0 0; background: {T["panel"]}; color: #ffffff; font: inherit; font-size: 12px; font-weight: 600">{esc(label)}{dot}</button>')
        else:
            t.append(f'<button role="tab" aria-selected="false" style="height: 28px; padding: 0 12px; border: none; border-radius: 6px 6px 0 0; background: transparent; color: {T["muted"]}; font: inherit; font-size: 12px">{esc(label)}</button>')
    return f'''<div style="height: 32px; flex: none; display: flex; align-items: center; gap: 14px; padding: 0 8px 0 12px; background: {T["chrome"]}; border-bottom: 1px solid {T["soft"]}">
<span style="display: flex; align-items: center; gap: 8px"><span style="width: 20px; height: 20px; border-radius: 5px; background: {T["primary"]}; display: flex; align-items: center; justify-content: center">{icon("logo", 13, "#ffffff", 2.4)}</span><span style="font-weight: 700">Sam Drawing Tools</span></span>
<div role="tablist" aria-label="Open documents" style="display: flex; gap: 2px; height: 32px; align-items: flex-end">{"".join(t)}</div>
<div style="margin-left: auto; display: flex; align-items: center; gap: 4px">
{icon_btn("undo", "Undo (Ctrl+Z)")}{icon_btn("redo", "Redo (Ctrl+Y)")}
<span style="margin: 0 8px; font-size: 12px; color: {T["muted"]}">{esc(right_note)}</span>
<button style="height: 24px; display: flex; align-items: center; gap: 6px; padding: 0 9px; border: 1px solid {T["border"]}; border-radius: 5px; background: transparent; color: {T["text2"]}; font: inherit; font-size: 12px">{icon("focus", 13)}Focus <span style="color: {T["faint"]}">F11</span></button>
</div>
</div>'''


RIBBON_TABS = ['Home', 'Compare', 'Markup', 'Draw', 'View', 'Tools']


def ribbon(active, groups, extra=''):
    tabs = []
    for name in RIBBON_TABS:
        if name == active:
            tabs.append(f'<button role="tab" aria-selected="true" style="height: 28px; padding: 0 14px; border: none; border-radius: 6px 6px 0 0; background: {T["panel"]}; color: #ffffff; font: inherit; font-weight: 600; box-shadow: inset 0 2px 0 {T["accent"]}">{name}</button>')
        else:
            tabs.append(f'<button role="tab" aria-selected="false" style="height: 28px; padding: 0 14px; border: none; background: transparent; color: {T["muted"]}; font: inherit">{name}</button>')
    gs = []
    for gname, items in groups:
        bs = []
        for it in items:
            label, ic = it[0], it[1]
            o = it[2] if len(it) > 2 else {}
            on, dis = o.get('on'), o.get('disabled')
            col = o.get('color', 'currentColor')
            bg = T['pressed'] if on else 'transparent'
            ring = f'inset 0 0 0 1px {T["accent"]}' if on else 'none'
            fg = '#5f6672' if dis else T['text']
            title = label + (f' ({o["key"]})' if o.get('key') else '')
            dis_attr = ' disabled' if dis else ''
            pressed = f' aria-pressed="{"true" if on else "false"}"' if 'on' in o else ''
            bs.append(f'<button{dis_attr}{pressed} title="{esc(title)}" style="min-width: 52px; padding: 0 4px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; border: none; border-radius: 6px; background: {bg}; color: {fg}; font: inherit; font-size: 11.5px; box-shadow: {ring}">'
                      f'{icon(ic, 20, "#5f6672" if dis else col, 1.7)}{esc(label)}</button>')
        gs.append(f'<div style="display: flex; flex-direction: column; padding: 0 8px; border-right: 1px solid {T["border"]}"><div style="display: flex; gap: 2px; flex: 1">{"".join(bs)}</div><div style="text-align: center; font-size: 10.5px; color: {T["faint"]}">{esc(gname)}</div></div>')
    return f'''<div role="tablist" aria-label="Ribbon" style="height: 30px; flex: none; display: flex; align-items: flex-end; gap: 2px; padding: 0 10px">{"".join(tabs)}</div>
<div style="height: 78px; flex: none; display: flex; align-items: stretch; padding: 6px 8px; background: {T["panel"]}; border-bottom: 1px solid {T["border"]}">{"".join(gs)}{extra}</div>'''


def ribbon_extra(rows):
    """Small labelled controls at the right of the ribbon. rows: list of html strings."""
    return f'<div style="display: flex; flex-direction: column; justify-content: center; gap: 6px; padding: 0 12px">{"".join(rows)}</div>'


def chip(text, on=True):
    if on:
        return f'<span style="padding: 3px 8px; border: 1px solid {T["field"]}; border-radius: 4px; background: {T["app"]}; font-size: 12px">{text}</span>'
    return f'<span style="padding: 3px 8px; border: 1px dashed {T["field"]}; border-radius: 4px; color: {T["faint"]}; font-size: 12px">{text}</span>'


def side_tabs(tabs, active):
    out = []
    for t in tabs:
        if t == active:
            out.append(f'<button role="tab" aria-selected="true" style="flex: 1; border: none; background: transparent; color: #ffffff; font: inherit; font-size: 12px; font-weight: 600; box-shadow: inset 0 -2px 0 {T["accent"]}">{t}</button>')
        else:
            out.append(f'<button role="tab" aria-selected="false" style="flex: 1; border: none; background: transparent; color: {T["muted"]}; font: inherit; font-size: 12px">{t}</button>')
    return f'<div role="tablist" style="height: 34px; flex: none; display: flex; border-bottom: 1px solid {T["soft"]}">{"".join(out)}</div>'


def left_panel(active, content, width=212):
    return (f'<aside aria-label="{active}" style="width: {width}px; flex: none; display: flex; flex-direction: column; background: {T["panel"]}; border-right: 1px solid {T["border"]}; min-height: 0">'
            f'{side_tabs(["Pages", "Bookmarks", "Layers"], active)}{content}</aside>')


def right_panel(title, content, width=256):
    return (f'<aside aria-label="Properties" style="width: {width}px; flex: none; display: flex; flex-direction: column; background: {T["panel"]}; border-left: 1px solid {T["border"]}; min-height: 0">'
            f'<div style="height: 34px; flex: none; display: flex; align-items: center; padding: 0 14px; font-size: 12px; font-weight: 600; color: {T["text2"]}; border-bottom: 1px solid {T["soft"]}">{title}</div>'
            f'<div style="flex: 1; min-height: 0; display: flex; flex-direction: column; gap: 14px; padding: 14px; overflow: hidden">{content}</div></aside>')


def field(label, value, mono=False, wide=False):
    ff = f'font-family: {T["mono"]}; font-size: 12px;' if mono else ''
    return (f'<label style="display: flex; flex-direction: column; gap: 4px{"; grid-column: span 2" if wide else ""}"><span style="font-size: 11px; color: {T["faint"]}">{label}</span>'
            f'<span style="height: 30px; box-sizing: border-box; display: flex; align-items: center; justify-content: space-between; padding: 0 8px; border: 1px solid {T["field"]}; border-radius: 4px; background: {T["app"]}; {ff}">{value}</span></label>')


def select(label, value, wide=False):
    return field(label, f'{value}<span style="color: {T["faint"]}">{icon("chevdown", 13)}</span>', wide=wide)


def grid2(*cells):
    return f'<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px">{"".join(cells)}</div>'


def section(title):
    return f'<span style="font-size: 11px; font-weight: 600; letter-spacing: 0.04em; color: {T["faint"]}; text-transform: uppercase">{title}</span>'


def toggle_row(label, on=True, note=''):
    knob = (f'<span style="width: 30px; height: 17px; flex: none; border-radius: 9px; background: {T["primary"] if on else T["field"]}; position: relative">'
            f'<span style="position: absolute; {"right" if on else "left"}: 2px; top: 2px; width: 13px; height: 13px; border-radius: 50%; background: #ffffff"></span></span>')
    n = f'<span style="font-size: 11.5px; color: {T["faint"]}">{note}</span>' if note else ''
    return f'<label style="display: flex; align-items: center; justify-content: space-between; gap: 8px; font-size: 12.5px; color: {T["text2"]}"><span style="display: flex; flex-direction: column; gap: 2px">{label}{n}</span>{knob}</label>'


def swatch_row(label, colors, selected=0):
    sw = ''.join(f'<span style="width: 22px; height: 22px; border-radius: 4px; background: {c}; border: {"2px solid #ffffff" if i == selected else "1px solid " + T["field"]}; box-sizing: border-box"></span>' for i, c in enumerate(colors))
    return f'<div style="display: flex; flex-direction: column; gap: 6px"><span style="font-size: 11px; color: {T["faint"]}">{label}</span><div style="display: flex; gap: 6px">{sw}</div></div>'


def slider(label, value_text, pct):
    return (f'<div style="display: flex; flex-direction: column; gap: 6px"><span style="display: flex; justify-content: space-between; font-size: 11px; color: {T["faint"]}">{label}<span style="color: {T["text"]}; font-family: {T["mono"]}">{value_text}</span></span>'
            f'<span style="position: relative; height: 4px; border-radius: 2px; background: {T["field"]}"><span style="position: absolute; left: 0; top: 0; bottom: 0; width: {pct}%; border-radius: 2px; background: {T["accent"]}"></span>'
            f'<span style="position: absolute; left: calc({pct}% - 7px); top: -5px; width: 14px; height: 14px; border-radius: 50%; background: #ffffff; box-shadow: 0 1px 3px rgba(0,0,0,0.5)"></span></span></div>')


def btn(label, kind='ghost', ic=None, h=32, extra=''):
    if kind == 'primary':
        st = f'border: none; background: {T["primary"]}; color: #ffffff; font-weight: 600'
    elif kind == 'danger':
        st = f'border: 1px solid rgba(255,135,135,0.45); background: transparent; color: {T["redText"]}'
    else:
        st = f'border: 1px solid {T["field"]}; background: transparent; color: {T["text2"]}'
    i = icon(ic, 15) if ic else ''
    return f'<button style="height: {h}px; display: flex; align-items: center; justify-content: center; gap: 6px; padding: 0 12px; border-radius: 5px; {st}; font: inherit; font-size: 12.5px{extra}">{i}{label}</button>'


def cmdline(history, prompt, typed='', options=None, popup='', height=None, float_=False):
    """AutoCAD-style command line. history: list of (text, kind) kind in '', 'cmd', 'err', 'ok'."""
    lines = []
    for text, kind in history:
        col = {'': T['faint'], 'cmd': T['text2'], 'err': T['redText'], 'ok': T['green']}[kind]
        lines.append(f'<div style="color: {col}; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{text}</div>')
    opts = ''
    if options:
        opts = ' ' + '[' + '/'.join(f'<span style="color: {T["accent"]}; text-decoration: underline; text-underline-offset: 2px">{o}</span>' for o in options) + ']'
    caret = f'<span style="display: inline-block; width: 7px; height: 15px; margin-left: 1px; vertical-align: -3px; background: {T["text"]}"></span>'
    sec = (f'position: absolute; left: 12px; right: 12px; bottom: 12px; z-index: 4; display: flex; flex-direction: column; background: rgba(26,31,38,0.86); border: 1px solid {T["border"]}; border-radius: 3px; box-shadow: 0 8px 24px rgba(0,0,0,0.45)'
           if float_ else f'flex: none; position: relative; display: flex; flex-direction: column; background: {T["panel2"]}; border-top: 1px solid {T["border"]}')
    row_bg = 'rgba(35,42,51,0.92)' if float_ else T["app"]
    return f'''<section aria-label="Command line" style="{sec}; font-family: {T["mono"]}; font-size: 12px">
{popup}
<div style="display: flex; flex-direction: column; justify-content: flex-end; gap: 2px; padding: 6px 12px 4px 44px; min-height: 52px; box-sizing: border-box">{"".join(lines)}</div>
<div style="height: 32px; display: flex; align-items: center; gap: 10px; padding: 0 8px 0 12px; border-top: 1px solid {T["soft"]}; background: {row_bg}">
<span style="color: {T["accent"]}; display: flex">{icon("terminal", 16)}</span>
<span role="textbox" aria-label="Command input" style="flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; color: {T["text"]}"><span style="color: {T["text2"]}">{prompt}</span>{opts} <span style="color: #ffffff">{typed}</span>{caret}</span>
<span style="font-family: 'Pretendard', sans-serif; font-size: 11.5px; color: {T["faint"]}">Enter run · Esc cancel · F2 history</span>
{icon_btn("chevup", "Expand history (F2)", 26)}
</div>
</section>'''


def cmd_popup(items, left=44, bottom=34):
    rows = []
    for i, (name, alias, desc, key) in enumerate(items):
        bg = T['accentBg'] if i == 0 else 'transparent'
        bar = f'box-shadow: inset 3px 0 0 {T["accent"]};' if i == 0 else ''
        rows.append(f'<div role="option" aria-selected="{"true" if i == 0 else "false"}" style="height: 30px; display: flex; align-items: center; gap: 12px; padding: 0 12px; background: {bg}; {bar}">'
                    f'<span style="width: 92px; color: #ffffff; font-weight: 600">{name}</span><span style="width: 40px; color: {T["accent"]}">{alias}</span>'
                    f'<span style="flex: 1; font-family: \'Pretendard\', sans-serif; font-size: 12.5px; color: {T["text2"]}">{desc}</span><span style="color: {T["faint"]}">{key}</span></div>')
    return (f'<div role="listbox" aria-label="Command suggestions" style="position: absolute; left: {left}px; bottom: {bottom}px; width: 520px; z-index: 5; display: flex; flex-direction: column; padding: 4px 0; border: 1px solid {T["field"]}; border-radius: 8px; background: {T["panel"]}; box-shadow: 0 12px 30px rgba(0,0,0,0.5)">'
            f'{"".join(rows)}<div style="height: 26px; display: flex; align-items: center; gap: 14px; padding: 0 12px; border-top: 1px solid {T["soft"]}; font-family: \'Pretendard\', sans-serif; font-size: 11.5px; color: {T["faint"]}"><span>↑↓ choose</span><span>Tab complete</span><span>Enter run</span><span style="margin-left: auto">All commands: type ? or F1</span></div></div>')


def statusbar(left, toggles=None, right=''):
    tg = ''
    if toggles is not None:
        parts = []
        for name, on in toggles:
            if on and T['sharp']:
                parts.append(f'<button aria-pressed="true" style="height: 18px; padding: 0 6px; border: 1px solid {T["accent"]}; border-radius: 2px; background: {T["accentBg"]}; color: {T["accent"]}; font: inherit; font-size: 10.5px; font-weight: 700; letter-spacing: 0.03em">{name}</button>')
            elif T['sharp']:
                parts.append(f'<button aria-pressed="false" style="height: 18px; padding: 0 6px; border: 1px solid {T["border"]}; border-radius: 2px; background: transparent; color: {T["faint"]}; font: inherit; font-size: 10.5px; letter-spacing: 0.03em">{name}</button>')
            elif on:
                parts.append(f'<button aria-pressed="true" style="height: 18px; padding: 0 6px; border: none; border-radius: 3px; background: rgba(255,255,255,0.92); color: {T["primary"]}; font: inherit; font-size: 10.5px; font-weight: 700; letter-spacing: 0.03em">{name}</button>')
            else:
                parts.append(f'<button aria-pressed="false" style="height: 18px; padding: 0 6px; border: 1px solid rgba(255,255,255,0.45); border-radius: 3px; background: transparent; color: rgba(255,255,255,0.85); font: inherit; font-size: 10.5px; letter-spacing: 0.03em">{name}</button>')
        tg = f'<span style="display: flex; gap: 4px">{"".join(parts)}</span>'
    return (f'<div style="height: 26px; flex: none; display: flex; align-items: center; gap: 16px; padding: 0 12px; background: {T["status"]}; color: {T["statusFg"]}; font-size: 11.5px; border-top: 1px solid {T["soft"] if T["sharp"] else T["status"]}">'
            f'{left}{tg}<span style="margin-left: auto; display: flex; gap: 16px; align-items: center">{right}</span></div>')


DRAFT_TOGGLES = lambda osnap=True, ortho=False, grid=False, snap=False: [('GRID', grid), ('SNAP', snap), ('ORTHO', ortho), ('OSNAP', osnap)]


def sheet(attrs, overlay='', max_w=None, extra_style=''):
    mw = f'max-width: {max_w}px; ' if max_w else ''
    ov = (f'<svg aria-hidden="true" viewBox="0 0 1191 842" style="position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible">{overlay}</svg>' if overlay else '')
    return (f'<div style="position: relative; width: 100%; {mw}aspect-ratio: 1191 / 842; box-shadow: 0 6px 24px rgba(0,0,0,0.5){extra_style}">'
            + (f'<div style="position: absolute; inset: 0; filter: invert(1) hue-rotate(180deg)">' if T['invert'] else '')
            + f'<dc-import name="Sheet" lang="en" {attrs} hint-size="100%,100%"></dc-import>' + ('</div>' if T['invert'] else '') + f'{ov}</div>')


def canvas_area(inner, label='Drawing', pad=18, extra=''):
    return (f'<section aria-label="{label}" style="flex: 1; min-width: 0; min-height: 0; position: relative; display: flex; align-items: center; justify-content: center; padding: {pad}px; background: {T["canvas"]}{extra}">{inner}</section>')


def page_nav(page='1 / 3', zoom='96%'):
    return (f'<div style="position: absolute; bottom: 12px; left: 50%; transform: translateX(-50%); display: flex; align-items: center; gap: 2px; padding: 3px; border-radius: 8px; background: rgba(35,38,43,0.94); border: 1px solid {T["border"]}">'
            f'{icon_btn("prev", "Previous page", 28, T["text"])}<span style="padding: 0 6px; font-size: 12px">{page}</span>{icon_btn("next", "Next page", 28, T["text"])}'
            f'<span style="width: 1px; height: 18px; background: {T["field"]}"></span>{icon_btn("minus", "Zoom out", 28, T["text"])}<span style="width: 44px; text-align: center; font-size: 12px">{zoom}</span>{icon_btn("plus", "Zoom in", 28, T["text"])}'
            f'<button style="height: 26px; padding: 0 8px; border: none; border-radius: 5px; background: transparent; color: {T["muted"]}; font: inherit; font-size: 12px">Fit</button></div>')


def dim_overlay():
    return '<div style="position: absolute; inset: 0; z-index: 8; background: rgba(10,11,13,0.62)"></div>'


def dialog(title, body, footer, width=560, top=110):
    return (f'<div role="dialog" aria-label="{esc(title)}" style="position: absolute; z-index: 9; left: 50%; top: {top}px; transform: translateX(-50%); width: {width}px; display: flex; flex-direction: column; border: 1px solid {T["field"]}; border-radius: 10px; background: {T["panel"]}; box-shadow: 0 30px 80px rgba(0,0,0,0.6); overflow: hidden">'
            f'<div style="height: 48px; display: flex; align-items: center; padding: 0 8px 0 18px; border-bottom: 1px solid {T["soft"]}"><span style="font-size: 15px; font-weight: 700">{esc(title)}</span><span style="margin-left: auto">{icon_btn("x", "Close (Esc)", 30)}</span></div>'
            f'{body}<div style="height: 60px; display: flex; align-items: center; gap: 8px; padding: 0 18px; border-top: 1px solid {T["soft"]}; background: {T["panel2"]}">{footer}</div></div>')


def checkbox(label, on=True, note='', disabled=False):
    box = (f'<span style="width: 16px; height: 16px; flex: none; border-radius: 4px; box-sizing: border-box; display: flex; align-items: center; justify-content: center; '
           + (f'background: {T["primary"]}; color: #ffffff">{icon("check", 12, "#ffffff", 3)}' if on else f'border: 1.5px solid {T["field"]}">') + '</span>')
    n = f'<span style="font-size: 11.5px; color: {T["faint"]}">{note}</span>' if note else ''
    col = '#6b7280' if disabled else T['text2']
    return f'<label style="display: flex; align-items: flex-start; gap: 10px; font-size: 13px; color: {col}{"; opacity: 0.6" if disabled else ""}">{box}<span style="display: flex; flex-direction: column; gap: 2px">{label}{n}</span></label>'


def radio(label, on=False, note=''):
    dot = (f'<span style="width: 16px; height: 16px; flex: none; border-radius: 50%; box-sizing: border-box; border: {"5px solid " + T["primary"] if on else "1.5px solid " + T["field"]}; background: {"#ffffff" if on else "transparent"}"></span>')
    n = f'<span style="font-size: 11.5px; color: {T["faint"]}">{note}</span>' if note else ''
    return f'<label style="display: flex; align-items: flex-start; gap: 10px; font-size: 13px; color: {T["text2"]}">{dot}<span style="display: flex; flex-direction: column; gap: 2px">{label}{n}</span></label>'


def sharpen(h):
    import re as _re
    def f(m):
        v = int(m.group(1))
        return f'border-radius: {2 if v <= 8 else 4}px' if v > 2 else m.group(0)
    h = _re.sub(r'border-radius: (\d+)px(?![^;"]*px)', f, h)
    return h
