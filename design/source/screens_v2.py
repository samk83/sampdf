# Round 2 mockups (2026-10-17): screens added by the PRD change, drawn for both builds.
# One screen, two builds: PC (local engine, files on this PC) and Web (upload, download, 30-minute expiry, account).
# Only four places may differ: opening files, saving files, the privacy notice and the account/plan.
# Places that differ carry a dashed "PC only" / "Web only" marker (diff()) so the Owner can see them.
import json
import os
import re

from chrome import (T, I, page, root, titlebar, icon, icon_btn, btn, cmdline, statusbar, dialog, dim_overlay, section,
                    field, select, toggle_row, checkbox, grid2, esc)
import screens

LUCIDE = os.path.join(os.path.dirname(__file__), '..', '..', 'web', 'node_modules', 'lucide-react', 'dist', 'esm', 'icons')
TARGET = 'pc'


def set_target(t):
    global TARGET
    TARGET = t


def licon(name, size=16, color='currentColor', sw=2):
    """Lucide icon read from web/node_modules (same set as the app). Falls back to chrome.I."""
    path = os.path.join(LUCIDE, name + '.js')
    if not os.path.exists(path):
        return icon(name, size, color, sw)
    src = open(path, encoding='utf-8').read()
    nodes = re.findall(r'\[\s*"(\w+)",\s*(\{[^}]*\})\s*\]', src)
    inner = ''
    for tag, attrs in nodes:
        a = json.loads(re.sub(r'([{,]\s*)(\w+):', r'\1"\2":', attrs))
        a.pop('key', None)
        inner += f'<{tag} ' + ' '.join(f'{k}="{v}"' for k, v in a.items()) + f'></{tag}>'
    return (f'<svg aria-hidden="true" width="{size}" height="{size}" viewBox="0 0 24 24" fill="none" stroke="{color}" '
            f'stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round">{inner}</svg>')


def diff(inner, who=None, block=True, pos='top: -9px; right: 8px'):
    """Dashed outline + tag on a part that only one build has, or that behaves differently."""
    who = who or TARGET
    tag = {'pc': 'PC only', 'web': 'Web only', 'both': 'PC ≠ Web'}[who]
    disp = 'block' if block else 'inline-block'
    return (f'<div style="position: relative; display: {disp}; outline: 1.5px dashed {T["warn"]}; outline-offset: 3px; border-radius: 2px">{inner}'
            f'<span style="position: absolute; {pos}; z-index: 3; padding: 0 5px; border-radius: 2px; background: {T["warn"]}; color: #1a1f26; font-size: 10px; font-weight: 700; letter-spacing: 0.03em; line-height: 16px">{tag}</span></div>')


def mark_dialog(h, who):
    """diff() for a dialog: the dialog is absolutely placed, so mark it in place instead of wrapping it."""
    tag = {'pc': 'PC only', 'web': 'Web only'}[who]
    h = h.replace('<div role="dialog"', f'<div role="dialog" data-only="{who}"', 1)
    h = re.sub(r'(<div role="dialog" data-only="\w+" aria-label="[^"]*" style=")', r'\1outline: 1.5px dashed ' + T['warn'] + '; outline-offset: 4px; overflow: visible !important; ', h, 1)
    i = h.index('>', h.index('role="dialog"')) + 1
    return h[:i] + (f'<span style="position: absolute; top: -12px; right: 12px; z-index: 3; padding: 0 5px; border-radius: 2px; background: {T["warn"]}; color: #1a1f26; font-size: 10px; font-weight: 700; letter-spacing: 0.03em; line-height: 16px">{tag}</span>') + h[i:]


# ---------- shared frame ----------

def account_chip():
    if TARGET != 'web':
        return ''
    chip = (f'<button style="height: 24px; display: flex; align-items: center; gap: 6px; padding: 0 9px; border: 1px solid {T["border"]}; border-radius: 2px; background: transparent; color: {T["text2"]}; font: inherit; font-size: 12px">'
            f'{licon("circle-user-round", 14)}Sign in <span style="padding: 0 4px; border-radius: 2px; background: {T["pressed"]}; color: {T["muted"]}; font-size: 10.5px">Free</span></button>')
    return diff(chip, 'web', False, 'top: 26px; right: 0')


def top(tabs, note=''):
    bar = titlebar(tabs, note)
    if TARGET == 'web':
        bar = bar.replace('<button style="height: 24px; display: flex; align-items: center; gap: 6px; padding: 0 9px; border: 1px solid',
                          account_chip() + '<button style="height: 24px; display: flex; align-items: center; gap: 6px; padding: 0 9px; border: 1px solid', 1)
    return bar


def status(left):
    if TARGET == 'pc':
        right = f'<span>Engine ready · 127.0.0.1:5180</span>'
    else:
        right = diff(f'<span style="display: flex; align-items: center; gap: 5px">{licon("shield-check", 12)}Web · files deleted after 30 min idle</span>', 'web', False, 'top: -20px; right: 0')
    return statusbar(left, None, right + f'<span style="display: flex; align-items: center; gap: 4px">{icon("globe", 12, T["statusFg"])}EN</span>')


def cmd(lines, prompt='Command:', typed=''):
    return cmdline(lines, prompt, typed, None)


# ---------- 01 Home v2: tool launcher ----------

APPS = [  # (number, id, lucide icon, category, status, name, desc)
    (1, 'drawing-compare', 'git-compare', 'drawing', 'ready', 'Drawing Compare', 'Find what changed between two revisions. Clouds and △ numbers are added for you.'),
    (2, 'pdf-overlay', 'layers', 'drawing', 'soon', 'PDF Overlay', 'Lay two drawings on top of each other in colors.'),
    (3, 'pdf-markup', 'pen-line', 'drawing', 'soon', 'PDF Markup', 'Add text, arrows, shapes and highlights to a PDF.'),
    (4, 'revision-cloud', 'cloud', 'drawing', 'soon', 'Revision Cloud', 'Draw revision clouds with numbers and dates.'),
    (5, 'measurement', 'ruler', 'drawing', 'soon', 'Measurement', 'Set the scale and measure lengths, areas and angles.'),
    (6, 'drawing-alignment', 'crosshair', 'drawing', 'soon', 'Drawing Alignment', 'Line up two drawings by picking two points.'),
    (7, 'drawing-stamp', 'stamp', 'drawing', 'soon', 'Drawing Stamp', 'Stamp drawings as Reviewed, Approved or For Construction.'),
    (8, 'dwg-preview', 'drafting-compass', 'drawing', 'soon', 'DWG/DXF Preview', 'Look at model space and layouts without opening CAD.'),
    (9, 'pdf-viewer', 'eye', 'pdf', 'soon', 'PDF Viewer', 'Open, search and page through PDFs.'),
    (10, 'pdf-editor', 'file-pen', 'pdf', 'soon', 'PDF Editor', 'Add or change text, images and pages.'),
    (11, 'merge-pdf', 'merge', 'pdf', 'soon', 'Merge PDF', 'Join several PDFs into one in the order you choose.'),
    (12, 'split-pdf', 'split', 'pdf', 'soon', 'Split PDF', 'Pull pages out into separate PDFs.'),
    (13, 'reorder-pages', 'arrow-up-down', 'pdf', 'soon', 'Reorder Pages', 'Drag pages to move, copy, rotate or delete them.'),
    (14, 'crop-rotate', 'crop', 'pdf', 'soon', 'Crop & Rotate', 'Trim margins and turn pages.'),
    (15, 'compress-pdf', 'file-archive', 'pdf', 'soon', 'Compress PDF', 'Make a PDF smaller, see the size before and after.'),
    (16, 'watermark', 'droplet', 'pdf', 'soon', 'Watermark', 'Put text or an image across your pages.'),
    (17, 'remove-background', 'eraser', 'image', 'ready', 'Remove Background', 'Make the white background of a stamp or signature transparent.'),
    (18, 'pdf-to-image', 'file-image', 'image', 'soon', 'PDF to Image', 'Save pages as PNG or JPG at the resolution you need.'),
    (19, 'image-to-pdf', 'image-plus', 'image', 'soon', 'Image to PDF', 'Turn photos and scans into one PDF.'),
    (20, 'ocr', 'scan-text', 'image', 'soon', 'OCR', 'Read the text in scans so you can search and copy it.'),
    (21, 'extract-images', 'images', 'image', 'soon', 'Extract Images', 'Save the pictures inside a PDF.'),
    (22, 'signature', 'signature', 'image', 'soon', 'Signature', 'Draw or place your signature on a PDF.'),
    (23, 'redaction', 'eye-off', 'image', 'soon', 'Redaction', 'Remove private information for good.'),
    (24, 'batch-tools', 'list-checks', 'image', 'soon', 'Batch Tools', 'Run the same steps on many files at once.'),
]
CATS = [('all', 'All', 24), ('drawing', 'Drawing & CAD', 8), ('pdf', 'PDF Essentials', 8), ('image', 'Image & Conversion', 8)]


def status_tag(st):
    if st == 'ready':
        return f'<span style="padding: 0 5px; border-radius: 2px; background: rgba(140,233,154,0.14); color: {T["green"]}; font-size: 10.5px; font-weight: 600; line-height: 17px">Ready</span>'
    if st == 'experimental':
        return f'<span style="padding: 0 5px; border-radius: 2px; background: rgba(255,224,102,0.14); color: {T["warn"]}; font-size: 10.5px; font-weight: 600; line-height: 17px">Experimental</span>'
    return f'<span style="padding: 0 5px; border: 1px solid {T["border"]}; border-radius: 2px; color: {T["faint"]}; font-size: 10.5px; line-height: 15px">Coming soon</span>'


def app_card(a):
    n, _id, ic, cat, st, name, desc = a
    ready = st == 'ready'
    tag = status_tag(st) if st != 'soon' else f'<span style="font-size: 10.5px; color: {T["faint"]}">Coming soon</span>'
    return (f'<button style="height: 104px; display: flex; flex-direction: column; gap: 5px; padding: 10px 12px; border: 1px solid {T["accent"] if ready else T["border"]}; border-radius: 4px; '
            f'background: {T["panel"]}; color: {T["text"]}; font: inherit; text-align: left; overflow: hidden">'
            f'<span style="display: flex; align-items: center; justify-content: space-between; width: 100%"><span style="width: 26px; height: 26px; flex: none; border-radius: 2px; background: {T["accentBg"] if ready else T["pressed"]}; display: flex; align-items: center; justify-content: center">{licon(ic, 15, T["accent"] if ready else T["text2"], 1.9)}</span>{tag}</span>'
            f'<span style="font-size: 13px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{esc(name)}</span>'
            f'<span style="font-size: 11.5px; line-height: 1.4; color: {T["muted"]}; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden">{desc}</span></button>')


def home_nav():
    items = [('Home', 'house', True), ('Open drawing', 'file', False), ('Compare', 'git-compare', False), ('Remove Background', 'eraser', False), ('Settings', 'settings', False)]
    navh = ''.join(f'<button aria-current="{"page" if on else "false"}" style="height: 36px; display: flex; align-items: center; gap: 10px; padding: 0 12px; border: none; border-radius: 2px; background: {T["pressed"] if on else "transparent"}; color: {"#ffffff" if on else T["text2"]}; font: inherit; font-weight: {600 if on else 400}; text-align: left">{licon(ic, 17)}{l}</button>' for l, ic, on in items)
    plan = ''
    if TARGET == 'web':
        plan = diff(f'<div style="display: flex; flex-direction: column; gap: 6px; padding: 12px; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel2"]}">'
                    f'<span style="display: flex; align-items: center; gap: 6px; font-size: 12.5px; font-weight: 600">{licon("circle-user-round", 15)}Free plan</span>'
                    f'<span style="font-size: 11.5px; line-height: 1.5; color: {T["muted"]}">Files up to 100 MB. Sign in to keep recent files and get Pro tools.</span>'
                    f'{btn("Sign in", "ghost", None, 28, "; width: 100%")}</div>', 'web')
    return (f'<nav aria-label="Start" style="width: 212px; flex: none; display: flex; flex-direction: column; gap: 2px; padding: 16px 10px; background: {T["panel"]}; border-right: 1px solid {T["border"]}">'
            f'{navh}<div style="margin-top: auto">{plan}</div></nav>')


def privacy_strip():
    return diff(f'<p style="margin: 0; display: flex; align-items: flex-start; gap: 8px; padding: 8px 12px; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel"]}; font-size: 12px; line-height: 1.5; color: {T["text2"]}">'
                f'<span style="flex: none; margin-top: 1px; color: {T["accent"]}">{licon("shield-check", 15)}</span><span>Files are processed on our server only to run the tool and never shared. Open files are deleted after 30 minutes without use. <a href="#">Privacy</a></span></p>', 'web')


def recent_row():
    sketches = ['M14 14h90M14 40h90M14 66h90M14 14v52M50 14v52M86 14v52M104 14v52', 'M14 18h70v48H14zM84 18h30v24H84zM30 18v48M14 42h70',
                'M20 70V24l40-12 40 12v46M20 46h80M40 70V46M80 70V46']
    if TARGET == 'pc':
        recent = [('S-101_Structural_Plan_rev1.pdf', 'D:\\Projects\\…\\Structural · now', 0), ('S-101_Structural_Plan_rev0.pdf', 'D:\\Projects\\…\\Structural · yesterday', 0),
                  ('A-201_Elevations_rev2.pdf', 'D:\\Projects\\…\\Arch · Oct 14', 2)]
        title, note = 'Recent files', ''
    else:
        recent = [('S-101_Structural_Plan_rev1.pdf', 'Uploaded 4 min ago · 26 min left', 0)]
        title, note = 'Open in this session', 'Deleted after 30 min without use'
    rec = ''.join(
        f'<button style="display: flex; align-items: center; gap: 10px; padding: 6px; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel"]}; color: {T["text"]}; font: inherit; text-align: left; min-width: 0">'
        f'<span style="width: 64px; height: 44px; flex: none; padding: 4px; background: #f4f4f1; box-sizing: border-box; border-radius: 2px"><svg aria-hidden="true" viewBox="0 0 160 88" width="100%" height="100%"><rect x="1" y="1" width="158" height="86" fill="none" stroke="#3b3f46" stroke-width="3"></rect><path d="{sketches[s]}" fill="none" stroke="#3b3f46" stroke-width="3"></path></svg></span>'
        f'<span style="min-width: 0; display: flex; flex-direction: column; gap: 3px"><span style="font-size: 12.5px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{esc(n)}</span>'
        f'<span style="font-size: 11px; color: {T["faint"]}; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{esc(m)}</span></span></button>'
        for n, m, s in recent)
    head = f'<h2 style="margin: 0; display: flex; justify-content: space-between; font-size: 13px; font-weight: 600">{title}<span style="font-size: 11.5px; font-weight: 400; color: {T["faint"]}">{note}</span></h2>'
    cols = 3 if TARGET == 'pc' else 2
    block = f'<div style="display: flex; flex-direction: column; gap: 8px">{head}<div style="display: grid; grid-template-columns: repeat({cols}, minmax(0, 1fr)); gap: 8px">{rec}</div></div>'
    block = diff(block, TARGET, True, 'top: -9px; left: 160px')
    drop_txt = ('<b style="color: ' + T['text'] + '">Drop PDF or images here</b><br>opens from this PC' if TARGET == 'pc'
                else '<b style="color: ' + T['text'] + '">Drop PDF or images here</b><br>uploads, up to 100 MB each')
    drop = (f'<div style="height: 58px; display: flex; align-items: center; justify-content: center; gap: 10px; padding: 0 12px; text-align: center; border: 1.5px dashed {T["field"]}; border-radius: 4px; color: {T["muted"]}; font-size: 12.5px">'
            f'{licon("upload", 18)}<span>{drop_txt}</span></div>')
    return f'<div style="display: grid; grid-template-columns: minmax(0, 2.2fr) minmax(0, 1fr); gap: 14px; align-items: end">{block}{drop}</div>'


def home_main():
    chips = ''.join(
        f'<button aria-pressed="{"true" if k == "all" else "false"}" style="height: 28px; display: flex; align-items: center; gap: 6px; padding: 0 10px; border: 1px solid {T["accent"] if k == "all" else T["border"]}; border-radius: 2px; '
        f'background: {T["accentBg"] if k == "all" else "transparent"}; color: {T["text"] if k == "all" else T["text2"]}; font: inherit; font-size: 12px">{l}<span style="color: {T["faint"]}; font-size: 11px">{c}</span></button>'
        for k, l, c in CATS)
    search = (f'<div style="width: 340px; height: 32px; display: flex; align-items: center; gap: 8px; padding: 0 10px; border: 1px solid {T["field"]}; border-radius: 2px; background: {T["app"]}; color: {T["faint"]}; font-size: 12.5px">'
              f'{licon("search", 15)}<span style="flex: 1">Search tools</span><span style="padding: 0 5px; border: 1px solid {T["border"]}; border-radius: 2px; font-family: {T["mono"]}; font-size: 10.5px">Ctrl K</span></div>')
    cards = ''.join(app_card(a) for a in APPS)
    note = 'Runs on this PC only' if TARGET == 'pc' else 'Web version'
    priv = privacy_strip() if TARGET == 'web' else ''
    return f'''<main style="flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 14px; padding: 20px 28px; overflow: hidden">
<div style="display: flex; align-items: flex-end; justify-content: space-between"><div style="display: flex; flex-direction: column; gap: 3px"><h1 style="margin: 0; font-size: 20px; font-weight: 700">What would you like to do?</h1><span style="color: {T["muted"]}">Pick a tool, drop files anywhere, or type a command below.</span></div>
<span style="font-size: 12px; color: {T["faint"]}">{note}</span></div>
{priv}
<div style="display: flex; align-items: center; gap: 10px">{search}<div role="group" aria-label="Category" style="display: flex; gap: 6px">{chips}</div><span style="margin-left: auto; font-size: 11.5px; color: {T["faint"]}">2 ready · 22 coming soon</span></div>
<div style="display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 8px">{cards}</div>
{recent_row()}
</main>'''


def home_under(lines=None):
    lines = lines or [('Sam Drawing Tools 1.0 · type a command or ? for the list', ''), ('Command: <span style="color:#fff">?</span>  opens the full command list', '')]
    return (top([('Home', True)])
            + f'<div style="flex: 1; display: flex; min-height: 0">{home_nav()}{home_main()}</div>'
            + cmd(lines) + status('<span>Ready</span>'))


def RHome():
    return page('Round 2 · Home', root(home_under()))


# ---------- 14 Tool info tab (Coming soon) ----------

def RToolInfo():
    steps = ['Choose the PDFs (two or more)', 'Put them in order, pick page ranges', 'Merge', 'Save or download the merged PDF']
    sth = ''.join(f'<li style="display: flex; align-items: center; gap: 12px; font-size: 13.5px; color: {T["text2"]}"><span style="width: 24px; height: 24px; flex: none; border: 1px solid {T["field"]}; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 12px; color: {T["muted"]}">{i + 1}</span>{s}</li>' for i, s in enumerate(steps))
    art = f'''<div style="flex: 1; min-height: 0; display: flex; justify-content: center; padding: 48px 24px; background: {T["app"]}; overflow: hidden">
<article style="width: 640px; display: flex; flex-direction: column; gap: 20px">
<header style="display: flex; align-items: flex-start; gap: 16px"><span style="width: 48px; height: 48px; flex: none; border-radius: 4px; background: {T["pressed"]}; display: flex; align-items: center; justify-content: center">{licon("merge", 24, T["accent"], 1.9)}</span>
<div style="display: flex; flex-direction: column; gap: 5px"><span style="display: flex; align-items: center; gap: 8px; font-size: 12px; color: {T["muted"]}">11 · PDF Essentials {status_tag("soon")}</span>
<h1 style="margin: 0; font-size: 22px; font-weight: 700">Merge PDF is coming soon</h1><p style="margin: 0; font-size: 14px; color: {T["text2"]}">Join several PDFs into one in the order you choose.</p></div></header>
<p style="margin: 0; padding: 12px 16px; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel"]}; font-size: 13px; line-height: 1.55; color: {T["text2"]}">This tool has a place in the app but does not work yet. Here is how it will work when it is ready.</p>
<section style="display: flex; flex-direction: column; gap: 10px"><h2 style="margin: 0; font-size: 13px; font-weight: 600">How it will work</h2><ol style="margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 9px">{sth}</ol></section>
<section style="display: flex; align-items: center; gap: 8px; font-size: 13px"><b>Plan</b><span style="color: {T["muted"]}">Stage 2 · page tools (after Drawing Compare)</span></section>
<div>{btn("Back to Home", "ghost", None, 34)}</div>
</article></div>'''
    body = (top([('Home', False), ('Merge PDF', True)]) + art
            + cmd([('Command: <span style="color:#fff">MERGE</span>', 'cmd'), ('MERGE  Merge PDF is coming soon (stage 2).', '')]) + status('<span>Ready</span>'))
    return page('Round 2 · Tool info', root(body))


# ---------- 15/16 Job tab: Merge PDF ----------

def job_steps(current):
    names = ['Choose files', 'Order & options', 'Merge', 'Result']
    out = []
    for i, n in enumerate(names):
        done, cur = i < current, i == current
        mark = (f'<span style="width: 22px; height: 22px; flex: none; border-radius: 50%; background: {T["primary"]}; display: flex; align-items: center; justify-content: center">{icon("check", 12, "#ffffff", 3)}</span>' if done else
                f'<span style="width: 22px; height: 22px; flex: none; border-radius: 50%; box-sizing: border-box; border: 1.5px solid {T["accent"] if cur else T["field"]}; display: flex; align-items: center; justify-content: center; font-size: 11.5px; color: {T["text"] if cur else T["faint"]}">{i + 1}</span>')
        out.append(f'<li aria-current="{"step" if cur else "false"}" style="height: 38px; display: flex; align-items: center; gap: 10px; padding: 0 10px; border-radius: 2px; background: {T["pressed"] if cur else "transparent"}; color: {"#ffffff" if cur else (T["text2"] if done else T["faint"])}; font-weight: {600 if cur else 400}">{mark}{n}</li>')
    return (f'<aside aria-label="Steps" style="width: 212px; flex: none; display: flex; flex-direction: column; gap: 12px; padding: 16px 10px; background: {T["panel"]}; border-right: 1px solid {T["border"]}">'
            f'<span style="display: flex; align-items: center; gap: 8px; padding: 0 10px; font-size: 13px; font-weight: 700">{licon("merge", 16, T["accent"])}Merge PDF</span>'
            f'<ol style="margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 2px">{"".join(out)}</ol></aside>')


FILES = [('S-101_Structural_Plan_rev1.pdf', 3, '2.6 MB', 'All pages'), ('S-201_Sections_rev0.pdf', 2, '1.8 MB', 'All pages'), ('S-301_Details_rev0.pdf', 6, '4.1 MB', '1–4, 6')]


def file_rows():
    rows = []
    for i, (n, pg, sz, rng) in enumerate(FILES):
        where = ('D:\\Projects\\Sam Office New Build\\Structural' if TARGET == 'pc' else 'Uploaded · 28 min left')
        rows.append(f'<div role="row" style="height: 64px; display: grid; grid-template-columns: 20px 24px 52px minmax(0, 1fr) 120px 70px 28px; column-gap: 12px; align-items: center; padding: 0 14px; border-bottom: 1px solid {T["soft"]}; font-size: 12.5px">'
                    f'<span style="color: {T["faint"]}">{licon("grip-vertical", 16)}</span><span style="font-family: {T["mono"]}; color: {T["muted"]}">{i + 1}</span>'
                    f'<span style="height: 40px; padding: 3px; box-sizing: border-box; background: #f4f4f1; border: 1px solid {T["border"]}; border-radius: 2px"><svg aria-hidden="true" viewBox="0 0 60 40" width="100%" height="100%"><path d="M2 2h56v36H2z M2 14h56 M2 26h56 M20 2v36 M40 2v36" fill="none" stroke="#3b3f46" stroke-width="1.5"></path></svg></span>'
                    f'<span style="min-width: 0; display: flex; flex-direction: column; gap: 3px"><b style="font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{n}</b><span style="font-size: 11px; color: {T["faint"]}">{pg} pages · {where}</span></span>'
                    f'<span style="height: 28px; display: flex; align-items: center; padding: 0 8px; border: 1px solid {T["field"]}; border-radius: 2px; background: {T["app"]}; font-family: {T["mono"]}; font-size: 11.5px">{rng}</span>'
                    f'<span style="color: {T["faint"]}; text-align: right">{sz}</span>{icon_btn("x", "Remove", 26)}</div>')
    return ''.join(rows)


def job_center():
    add_note = ('Opens the app file browser on this PC' if TARGET == 'pc' else 'Uploads from your computer · up to 100 MB each')
    add = diff(f'<span style="display: flex; align-items: center; gap: 10px">{btn("Add files", "ghost", "plus", 32)}<span style="font-size: 11.5px; color: {T["faint"]}">{add_note}</span></span>', TARGET, False, 'top: -12px; right: -6px')
    return f'''<section aria-label="Files" style="flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 14px; padding: 20px 24px; background: {T["app"]}; overflow: hidden">
<div style="display: flex; align-items: center; justify-content: space-between"><div style="display: flex; flex-direction: column; gap: 3px"><h1 style="margin: 0; font-size: 17px; font-weight: 700">Order & options</h1><span style="font-size: 12.5px; color: {T["muted"]}">Drag rows to change the order. The merged PDF follows this list from top to bottom.</span></div>{add}</div>
<div role="grid" style="display: flex; flex-direction: column; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel"]}; overflow: hidden">
<div role="row" style="height: 30px; display: grid; grid-template-columns: 20px 24px 52px minmax(0, 1fr) 120px 70px 28px; column-gap: 12px; align-items: center; padding: 0 14px; background: {T["panel2"]}; border-bottom: 1px solid {T["border"]}; font-size: 11.5px; color: {T["muted"]}"><span></span><span>#</span><span></span><span>File</span><span>Pages</span><span style="text-align: right">Size</span><span></span></div>
{file_rows()}</div>
<div style="height: 64px; display: flex; align-items: center; justify-content: center; gap: 10px; border: 1.5px dashed {T["field"]}; border-radius: 4px; color: {T["muted"]}; font-size: 12.5px">{licon("upload", 18)}<span><b style="color: {T["text"]}">Drop more PDFs here</b> · they go to the end of the list</span></div>
</section>'''


def save_target():
    if TARGET == 'pc':
        return diff(f'<div style="display: flex; flex-direction: column; gap: 6px">{section("Save to")}'
                    f'<span style="min-height: 30px; box-sizing: border-box; display: flex; align-items: center; padding: 4px 8px; border: 1px solid {T["field"]}; border-radius: 2px; background: {T["app"]}; font-family: {T["mono"]}; font-size: 11.5px; word-break: break-all">D:\\Projects\\Sam Office New Build\\Structural</span>'
                    f'{btn("Change folder", "ghost", "folder", 28)}</div>', 'pc')
    return diff(f'<div style="display: flex; flex-direction: column; gap: 6px">{section("Save to")}'
                f'<span style="font-size: 12px; line-height: 1.5; color: {T["text2"]}">You download the merged PDF when it is ready. It stays on the server for 30 minutes, then it is deleted.</span></div>', 'web')


def job_options():
    content = (section('Output') + field('File name', 'S-set_merged.pdf', True)
               + toggle_row('Add a bookmark for each file', True) + toggle_row('Keep markups and comments', True)
               + save_target()
               + f'<div style="margin-top: auto; display: flex; flex-direction: column; gap: 8px"><span style="font-size: 11.5px; color: {T["faint"]}">3 files · 10 pages · ≈ 8.4 MB</span>{btn("Merge", "primary", None, 38, "; width: 100%")}</div>')
    return (f'<aside aria-label="Options" style="width: 280px; flex: none; display: flex; flex-direction: column; background: {T["panel"]}; border-left: 1px solid {T["border"]}">'
            f'<div style="height: 34px; flex: none; display: flex; align-items: center; padding: 0 14px; font-size: 12px; font-weight: 600; color: {T["text2"]}; border-bottom: 1px solid {T["soft"]}">Options</div>'
            f'<div style="flex: 1; display: flex; flex-direction: column; gap: 14px; padding: 14px">{content}</div></aside>')


def RJob():
    body = (top([('Home', False), ('Merge PDF · 3 files', True)], 'Not merged yet')
            + f'<div style="flex: 1; display: flex; min-height: 0">{job_steps(1)}{job_center()}{job_options()}</div>'
            + cmd([('Command: <span style="color:#fff">MERGE</span>', 'cmd'), ('MERGE  3 files added', '')]) + status('<span>Ready</span>'))
    return page('Round 2 · Job tab', root(body))


def RJobResult():
    if TARGET == 'pc':
        where = diff(f'<span style="display: flex; flex-direction: column; gap: 4px"><span style="font-size: 12px; color: {T["muted"]}">Saved to</span>'
                     f'<span style="font-family: {T["mono"]}; font-size: 12px">D:\\Projects\\Sam Office New Build\\Structural\\S-set_merged.pdf</span></span>', 'pc')
        actions = btn('Open in viewer', 'primary', None, 36) + diff(btn('Show in folder', 'ghost', 'folder', 36), 'pc', False, 'top: -12px; right: -6px') + btn('Merge more files', 'ghost', None, 36)
    else:
        where = diff(f'<span style="display: flex; align-items: flex-start; gap: 8px; font-size: 12.5px; line-height: 1.5; color: {T["text2"]}">{licon("clock", 15)}'
                     f'<span>Ready to download. The file is deleted from the server 30 minutes after you last use it. <a href="#">Delete now</a></span></span>', 'web')
        actions = diff(btn('Download PDF', 'primary', 'download', 36), 'web', False, 'top: -12px; right: -6px') + btn('Open in viewer', 'ghost', None, 36) + btn('Merge more files', 'ghost', None, 36)
    card = f'''<div style="flex: 1; min-height: 0; display: flex; justify-content: center; align-items: flex-start; padding: 56px 24px; background: {T["app"]}">
<div style="width: 620px; display: flex; flex-direction: column; gap: 18px; padding: 24px; border: 1px solid {T["border"]}; border-radius: 4px; background: {T["panel"]}">
<div style="display: flex; align-items: center; gap: 12px"><span style="width: 36px; height: 36px; border-radius: 50%; background: rgba(140,233,154,0.14); display: flex; align-items: center; justify-content: center">{icon("check", 18, T["green"], 2.6)}</span>
<div style="display: flex; flex-direction: column; gap: 3px"><h1 style="margin: 0; font-size: 17px; font-weight: 700">Merged 3 files into S-set_merged.pdf</h1><span style="font-size: 12.5px; color: {T["muted"]}">10 pages · 8.4 MB · 3 bookmarks · took 2 s</span></div></div>
{where}
<div style="display: flex; gap: 8px">{actions}</div></div></div>'''
    body = (top([('Home', False), ('Merge PDF · done', True)], 'Done')
            + f'<div style="flex: 1; display: flex; min-height: 0">{job_steps(3)}{card}</div>'
            + cmd([('Command: <span style="color:#fff">MERGE</span>', 'cmd'), ('MERGE  3 files added', ''), ('Merged 10 pages → S-set_merged.pdf', 'ok')]) + status('<span>Done</span>'))
    return page('Round 2 · Job result', root(body))


# ---------- 17 Open drawing ----------

def upload_dialog():
    rows = [('S-101_Structural_Plan_rev1.pdf', '2.6 MB', 100, ''), ('S-101_Structural_Plan_rev0.pdf', '2.4 MB', 62, ''),
            ('Site_survey_full.pdf', '140 MB', 0, 'Too large: the web version opens files up to 100 MB. Use the PC version or Compress PDF first.')]
    rh = ''
    for n, sz, pct, err in rows:
        if err:
            st = f'<span style="color: {T["redText"]}; display: flex">{icon("alert", 15)}</span>'
            bar = f'<span style="font-size: 11.5px; line-height: 1.45; color: {T["redText"]}">{err}</span>'
        else:
            st = (f'<span style="color: {T["green"]}; display: flex">{icon("check", 15, T["green"], 2.6)}</span>' if pct == 100 else f'<span style="font-family: {T["mono"]}; font-size: 11px; color: {T["muted"]}">{pct}%</span>')
            bar = (f'<span style="height: 4px; border-radius: 2px; background: {T["field"]}; position: relative"><span style="position: absolute; inset: 0 {100 - pct}% 0 0; border-radius: 2px; background: {T["green"] if pct == 100 else T["accent"]}"></span></span>')
        rh += (f'<div style="display: grid; grid-template-columns: 18px minmax(0, 1fr) 64px 36px; column-gap: 12px; align-items: center; padding: 10px 14px; border-bottom: 1px solid {T["soft"]}">'
               f'<span style="color: #9ec5fe">{icon("file", 15)}</span><span style="min-width: 0; display: flex; flex-direction: column; gap: 6px"><span style="font-size: 12.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis">{n}</span>{bar}</span>'
               f'<span style="font-size: 11.5px; color: {T["faint"]}; text-align: right">{sz}</span><span style="display: flex; justify-content: flex-end">{st}</span></div>')
    body_html = f'''<div style="display: flex; flex-direction: column; gap: 14px; padding: 18px">
<div style="height: 150px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; border: 1.5px dashed {T["accent"]}; border-radius: 4px; background: {T["accentBg"]}">
{licon("upload", 26, T["accent"])}<span style="font-size: 14px; font-weight: 600">Drop PDF or images here</span><span style="display: flex; align-items: center; gap: 8px; font-size: 12px; color: {T["muted"]}">or {btn("Choose files", "ghost", None, 28)}</span>
<span style="font-size: 11.5px; color: {T["faint"]}">PDF, PNG, JPG, TIFF · up to 100 MB each</span></div>
<div style="display: flex; flex-direction: column; border: 1px solid {T["border"]}; border-radius: 4px; overflow: hidden">{rh}</div>
{privacy_strip()}</div>'''
    footer = (f'<span style="font-size: 12px; color: {T["faint"]}">1 of 3 uploaded</span><span style="margin-left: auto"></span>{btn("Cancel", h=34)}{btn("Open", "primary", None, 34)}')
    return dim_overlay() + mark_dialog(dialog('Open drawing', body_html, footer, 720, 70), 'web')


def ROpen():
    lines = [('Command: <span style="color:#fff">OPEN</span>', 'cmd'), ('OPEN  Choose a file in the dialog', '')]
    if TARGET == 'pc':
        over = mark_dialog(screens.open_dialog(), 'pc')
    else:
        over = upload_dialog()
    return page('Round 2 · Open drawing', root(home_under(lines) + over))


# ---------- 18 Account & plan (web) ----------

def RAccount():
    plans = [('Free', '$0', ['All Ready tools', 'Files up to 100 MB', 'Files deleted after 30 min idle', 'No sign-in needed'], False),
             ('Pro', 'Price to be decided', ['Everything in Free', 'Files up to 300 MB', 'Batch Tools, OCR, Signature', 'Keep recent files for 7 days'], True)]
    ph = ''.join(
        f'<div style="flex: 1; display: flex; flex-direction: column; gap: 10px; padding: 16px; border: 1px solid {T["accent"] if pro else T["border"]}; border-radius: 4px; background: {T["panel2"]}">'
        f'<span style="display: flex; justify-content: space-between; align-items: baseline"><b style="font-size: 15px">{n}</b><span style="font-size: 12px; color: {T["muted"]}">{price}</span></span>'
        + ''.join(f'<span style="display: flex; gap: 8px; font-size: 12.5px; color: {T["text2"]}">{icon("check", 14, T["green"], 2.4)}{f}</span>' for f in feats)
        + f'<div style="margin-top: 4px">{btn("Current plan", "ghost", None, 32, "; width: 100%") if not pro else btn("Upgrade to Pro", "primary", None, 32, "; width: 100%")}</div></div>'
        for n, price, feats, pro in plans)
    body_html = f'''<div style="display: flex; flex-direction: column; gap: 16px; padding: 18px">
<div style="display: flex; gap: 14px; align-items: flex-end">{field("Email", '<span style="color: ' + T["faint"] + '">you@example.com</span>')}{btn("Continue", "primary", None, 30)}<span style="color: {T["faint"]}; font-size: 12px; padding-bottom: 7px">or</span>{btn("Continue with Google", "ghost", None, 30)}</div>
<div style="display: flex; gap: 12px">{ph}</div>
<span style="font-size: 11.5px; line-height: 1.5; color: {T["faint"]}">The PC version has no sign-in. Plans and prices are a draft for review.</span></div>'''
    footer = f'<span style="margin-left: auto"></span>{btn("Not now", h=34)}'
    over = dim_overlay() + mark_dialog(dialog('Sign in · plans', body_html, footer, 680, 110), 'web')
    return page('Round 2 · Account', root(home_under() + over))


# (file, function, targets, title, Korean note for the review board)
PAGES = [
    ('r2-01-home', RHome, ['pc', 'web'], 'Home · tool launcher (24 tools)',
     '시작 화면 v2. 24개 기능 카드·검색·갈래는 똑같고, 다른 곳은 웹의 개인정보 안내 띠·Free 플랜 칸·로그인 버튼, 최근 파일(PC: 내 PC 경로 / 웹: 이번 접속에 연 파일)뿐.'),
    ('r2-14-tool-info', RToolInfo, ['pc', 'web'], 'Tool info tab (Coming soon)',
     '아직 안 되는 기능 카드를 누르면 열리는 안내 탭. PC와 웹이 똑같음 (상태 표시줄·로그인 버튼만 다름).'),
    ('r2-15-job-merge', RJob, ['pc', 'web'], 'Job tab · Merge PDF (options)',
     '리본 없는 작은 작업 탭 (왼쪽 단계 · 가운데 파일 · 오른쪽 옵션). 다른 곳은 "Add files"가 여는 곳과 "Save to"(PC: 폴더 / 웹: 내려받기)뿐.'),
    ('r2-16-job-result', RJobResult, ['pc', 'web'], 'Job tab · result & save',
     '저장의 차이. PC는 고른 폴더에 바로 저장하고 "Show in folder", 웹은 "Download PDF"와 30분 뒤 삭제 안내.'),
    ('r2-17-open', ROpen, ['pc', 'web'], 'Open drawing',
     '파일 열기의 차이. PC는 앱 전용 열기 창(기존 13번), 웹은 올리기 창(100MB 한도, 진행 막대, 너무 큰 파일 안내). 버튼 이름·단축키(Ctrl+O)·명령(OPEN)은 같음.'),
    ('r2-18-account', RAccount, ['web'], 'Sign in & plans (web only)',
     '웹에만 있는 로그인·Free/Pro 창. 가격·Pro 내용은 검토용 초안. PC 버전은 로그인 없음.'),
]
