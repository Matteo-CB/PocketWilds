"""Thai test fonts and proofs (FT).

    python pw_font_thai_test.py            # test fonts + every proof sheet
    python pw_font_thai_test.py fonts      # test fonts only
    python pw_font_thai_test.py quick      # one fast glyph grid (Regular) for design iterations

Test fonts: SourceArt/Fonts/Build/Test/PocketRound-<Weight>-Thai-Test.ttf and PocketPlush-Black-Thai-Test.ttf
(Latin from pw_font_build plus the Thai module, features merged). The shipped fonts in Build/ are never touched.
Proofs: SourceArt/Fonts/Proofs/THAI_*.png, laid out by a small engine that follows TextMeshPro's own rules
(cmap, the game's pre-shaping step, GPOS mark-to-base and mark-to-mark read back from the built font).
"""
import os
import sys

os.environ.setdefault("SOURCE_DATE_EPOCH", "1791590400")  # fixed timestamps: byte-identical builds

from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.ttLib import TTFont
from PIL import Image, ImageChops, ImageDraw, ImageFont

import pw_font_thai as M
from pw_font_build import FAMILIES, build_style, glyph_name, kerning, write_font

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
TEST = os.path.join(ROOT, "SourceArt", "Fonts", "Build", "Test")
PROOFS = os.path.join(ROOT, "SourceArt", "Fonts", "Proofs")

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
NIGHT = (44, 36, 52)
NIGHT_INK = (255, 244, 226)
ORANGE = (252, 172, 84)


def styles():
    return FAMILIES["PocketRound"] + FAMILIES["PocketPlush"]


def test_path(style, loopless=False):
    name = (style.family + "-" + style.weight_name).replace(" ", "")
    return os.path.join(TEST, f"{name}-Thai{'-Loopless' if loopless else ''}-Test.ttf")


def kern_fea(pairs):
    if not pairs:
        return ""
    return "feature kern {\n" + "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(pairs.items())) + "} kern;\n"


def build_font(style, path=None, loopless=False):
    """Latin (pw_font_build) + Thai (pw_font_thai), the way the merge hook will do it."""
    order, cmap, data = build_style(style)
    pairs = kerning(order, cmap, data, style)
    for ch in M.charset():
        name = glyph_name(ch)
        contours, adv = M.place(ch, style, loopless)
        if name not in data:
            order.append(name)
        data[name] = (contours, adv, ch)
        cmap[ord(ch)] = name
    path = path or test_path(style, loopless)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    s2 = type(style)(**{**style.__dict__, "family": style.family + " Thai Test"})
    write_font(s2, order, cmap, data, pairs, path)
    fea = "languagesystem DFLT dflt;\nlanguagesystem latn dflt;\n"
    fea += "".join(f"languagesystem {ls};\n" for ls in M.LANGUAGESYSTEMS)
    fea += kern_fea(pairs) + M.features(style, loopless)
    font = TTFont(path)
    addOpenTypeFeaturesFromString(font, fea)
    font.save(path)
    return path, len(order)


def quick(weight=0, loopless=False):
    import tempfile
    style = styles()[weight]
    path, n = build_font(style, os.path.join(tempfile.gettempdir(), "pw_thai_quick.ttf"), loopless)
    f = ImageFont.truetype(path, 120)
    chars = [c for c in M.THAI if not M.is_mark(c)]
    cols = 12
    cell = 150
    img = Image.new("RGB", (cols * cell + 40, ((len(chars) + cols - 1) // cols) * 190 + 40), CREAM)
    d = ImageDraw.Draw(img)
    lab = ImageFont.truetype(path, 18)
    for i, ch in enumerate(chars):
        x, y = 20 + (i % cols) * cell, 20 + (i // cols) * 190
        d.line([(x, y + 140), (x + cell - 10, y + 140)], fill=(230, 220, 200))
        d.text((x + 20, y + 140), ch, font=f, fill=INK, anchor="ls")
        d.text((x, y + 170), "%04X" % ord(ch), font=lab, fill=SOFT)
    os.makedirs(PROOFS, exist_ok=True)
    out = os.path.join(PROOFS, "_quick.png")  # scratch sheet, not a deliverable
    img.save(out)
    return out



def zoom(chars, weight=0, size=260, out="_zoom.png", build=True, loopless=False):
    style = styles()[weight]
    import tempfile
    path = os.path.join(tempfile.gettempdir(), f"pw_thai_quick{weight}{'L' if loopless else ''}.ttf")
    if build or not os.path.exists(path):
        build_font(style, path, loopless)
    f = ImageFont.truetype(path, size)
    cols = 6
    cw, ch_ = int(size * 1.15), int(size * 1.55)
    img = Image.new("RGB", (cols * cw + 40, ((len(chars) + cols - 1) // cols) * ch_ + 40), CREAM)
    d = ImageDraw.Draw(img)
    for i, c in enumerate(chars):
        x, y = 20 + (i % cols) * cw, 20 + (i // cols) * ch_
        base = y + int(size * 1.05)
        for yy, col in ((0, (225, 210, 190)), (style.xh, (240, 228, 210)), (round(style.xh * 1.035), (210, 225, 240))):
            py = base - yy * size / 1000
            d.line([(x, py), (x + cw - 20, py)], fill=col)
        d.text((x + int(size * 0.25), base), c, font=f, fill=INK, anchor="ls")
    img.save(os.path.join(PROOFS, out))
    return os.path.join(PROOFS, out)


# ---------------------------------------------------------------------------------------------------------------
# A small layout engine that follows TextMeshPro (com.unity.ugui 2.0.0, TextMeshProUGUI.GenerateTextMesh):
# cmap, advances, legacy kern pairs, then for a mark (TMP_TextParsingUtilities.IsBaseGlyph is false):
#   - right after its base: MarkToBase(mark, base);
#   - otherwise: MarkToMark against each earlier mark back to the base, else MarkToBase on the last base.
# The mark origin becomes base origin + base anchor - mark anchor (mkmk: the earlier mark's placed origin).
# ---------------------------------------------------------------------------------------------------------------

def tmp_is_base(c):
    return not (0x300 <= c <= 0x36F or c == 0xE31 or 0xE34 <= c <= 0xE3A or 0xE47 <= c <= 0xE4E)


class Shaper:
    def __init__(self, path):
        self.path = path
        self.tt = TTFont(path)
        self.cmap = self.tt.getBestCmap()
        self.hmtx = self.tt["hmtx"].metrics
        self.upm = self.tt["head"].unitsPerEm
        self.kern = {}
        if "kern" in self.tt:
            self.kern = dict(self.tt["kern"].kernTables[0].kernTable)
        self.mb, self.mm = {}, {}
        if "GPOS" in self.tt:
            self._read_gpos()

    def _read_gpos(self):
        gpos = self.tt["GPOS"].table
        for lk in gpos.LookupList.Lookup:
            for st in lk.SubTable:
                if lk.LookupType == 4:
                    marks = st.MarkCoverage.glyphs
                    bases = st.BaseCoverage.glyphs
                    for mi, mg in enumerate(marks):
                        rec = st.MarkArray.MarkRecord[mi]
                        ma = (rec.MarkAnchor.XCoordinate, rec.MarkAnchor.YCoordinate)
                        for bi, bg in enumerate(bases):
                            a = st.BaseArray.BaseRecord[bi].BaseAnchor[rec.Class]
                            if a is not None:
                                self.mb[(mg, bg)] = ((a.XCoordinate, a.YCoordinate), ma)
                elif lk.LookupType == 6:
                    marks = st.Mark1Coverage.glyphs
                    bases = st.Mark2Coverage.glyphs
                    for mi, mg in enumerate(marks):
                        rec = st.Mark1Array.MarkRecord[mi]
                        ma = (rec.MarkAnchor.XCoordinate, rec.MarkAnchor.YCoordinate)
                        for bi, bg in enumerate(bases):
                            a = st.Mark2Array.Mark2Record[bi].Mark2Anchor[rec.Class]
                            if a is not None:
                                self.mm[(mg, bg)] = ((a.XCoordinate, a.YCoordinate), ma)

    def layout(self, text, preshape=True, gpos=True, legacy=False):
        """[(char, x, y)] in font units (y up, baseline 0), and the total advance."""
        if preshape:
            text = M.preshape(text, legacy)
        out = []
        pen = 0.0
        last_base = None
        info = []  # (glyph name, origin x, y offset)
        for i, ch in enumerate(text):
            cp = ord(ch)
            g = self.cmap.get(cp, ".notdef")
            adv = self.hmtx[g][0]
            k = 0
            if i + 1 < len(text):
                g2 = self.cmap.get(ord(text[i + 1]), ".notdef")
                k = self.kern.get((g, g2), 0)
            x, y = pen, 0.0
            if tmp_is_base(cp):
                last_base = i
            elif gpos and i > 0 and last_base is not None:
                done = False
                if last_base == i - 1:
                    rec = self.mb.get((g, info[last_base][0]))
                    if rec:
                        x = info[last_base][1] + rec[0][0] - rec[1][0]
                        y = rec[0][1] - rec[1][1]
                else:
                    for j in range(i - 1, last_base, -1):
                        rec = self.mm.get((g, info[j][0]))
                        if rec:
                            x = info[j][1] + rec[0][0] - rec[1][0]
                            y = info[j][2] + rec[0][1] - rec[1][1]
                            done = True
                            break
                    if not done:
                        rec = self.mb.get((g, info[last_base][0]))
                        if rec:
                            x = info[last_base][1] + rec[0][0] - rec[1][0]
                            y = rec[0][1] - rec[1][1]
            info.append((g, x, y))
            out.append((ch, x, y))
            pen += adv + k
        return out, pen


_FONT_CACHE = {}


def pil_font(path, px):
    key = (path, px)
    if key not in _FONT_CACHE:
        _FONT_CACHE[key] = ImageFont.truetype(path, px)
    return _FONT_CACHE[key]


def draw_text(img, xy, text, shaper, px, color, preshape=True, gpos=True, legacy=False):
    """Draws text laid out by the TMP-like engine; returns the advance in pixels."""
    f = pil_font(shaper.path, px)
    x0, base = xy
    sc = px / shaper.upm
    glyphs, adv = shaper.layout(text, preshape, gpos, legacy)
    layer = Image.new("L", img.size, 0)
    for ch, x, y in glyphs:
        if ch in (" ", "​"):
            continue
        mask, off = f.getmask2(ch, mode="L", anchor="ls")
        if mask.size[0] == 0:
            continue
        m = Image.frombytes("L", mask.size, bytes(mask))
        px_ = int(round(x0 + x * sc + off[0]))
        py_ = int(round(base - y * sc + off[1]))
        box = (px_, py_, px_ + m.size[0], py_ + m.size[1])
        cb = (max(box[0], 0), max(box[1], 0), min(box[2], img.size[0]), min(box[3], img.size[1]))
        if cb[2] <= cb[0] or cb[3] <= cb[1]:
            continue
        m = m.crop((cb[0] - box[0], cb[1] - box[1], cb[2] - box[0], cb[3] - box[1]))
        layer.paste(ImageChops.lighter(layer.crop(cb), m), cb[:2])
    img.paste(Image.new("RGB", img.size, color), (0, 0), layer)
    return adv * sc

# ---------------------------------------------------------------------------------------------------------------
# Proof sheets
# ---------------------------------------------------------------------------------------------------------------

ZWSP = "\u200b"
GAME_LINES = [
    ("เพื่อนใหม่!", "New friend!"),
    ("รับรางวัล", "Claim reward"),
    ("ไข่ฟักแล้ว", "The egg hatched"),
    ("ยินดีต้อนรับกลับ", "Welcome back"),
    ("แตะ\u200bที่\u200bไข่\u200bเพื่อ\u200bช่วย\u200bให้\u200bลูก\u200bสัตว์\u200bฟัก\u200bออก\u200bมา", "Tap the egg to help the baby animal hatch"),
    ("ได้รับ 50 อัญมณี และ\u200bแปรง\u200bทองคำ", "You got 50 gems and the golden brush"),
    ("กระต่าย\u200bหลับ\u200bอยู่\u200bใกล้\u200bดอกไม้", "The rabbit naps near the flowers"),
    ("ภารกิจ\u200bประจำวัน 2/3", "Daily mission 2/3"),
    ("ร้านค้า · แพ็ก\u200bทุ่งหญ้า ฿35", "Shop · Meadow pack ฿35"),
]
STACKS = "กี่ ปี้ ญุ ฐุ ฎุ ฏู ปั่น ฟื้น น้ำ ค่ำ สิทธิ์ ก็ ทั้ง ฝั่ง"
STYLE_LABEL = {"Regular": "Pocket Round Regular", "SemiBold": "Pocket Round SemiBold", "Bold": "Pocket Round Bold",
               "Black": "Pocket Plush Black"}


def text_width(shaper, text, px):
    return shaper.layout(text)[1] * px / shaper.upm


def label(img, xy, text, path, px, color):
    """Latin labels in the same test font (our own Latin)."""
    d = ImageDraw.Draw(img)
    d.text(xy, text, font=pil_font(path, px), fill=color, anchor="ls")


def proof_glyphs(style, path):
    """Every glyph of the module in a grid: marks on อ, legacy PUA variants with the base they follow."""
    sh = Shaper(path)
    cells = []
    for ch in M.charset():
        if ch in M.ZW:
            cells.append(("", "200B zero width"))
        elif ch in M.MARKS:
            cells.append(("อ" + ch, "%04X" % ord(ch)))
        elif ch in M.PUA_OF:
            base, kind = M.PUA_OF[ch]
            ctx = {"rd": "", "l": "ป", "d": "ฎ" if base in M.BELOW else "ก", "dl": "ป"}[kind]
            cells.append((ctx + ch, "%04X %s" % (ord(ch), kind)))
        else:
            cells.append((ch, "%04X" % ord(ch)))
    cols, cw, chh, px = 10, 200, 230, 100
    W = cols * cw + 60
    H = ((len(cells) + cols - 1) // cols) * chh + 140
    img = Image.new("RGB", (W, H), CREAM)
    label(img, (30, 60), f"Thai · {STYLE_LABEL[style.weight_name]} · {len(M.charset())} code points", path, 34, INK)
    d = ImageDraw.Draw(img)
    for i, (text, lab) in enumerate(cells):
        x, y = 30 + (i % cols) * cw, 110 + (i // cols) * chh
        base = y + 150
        d.line([(x, base), (x + cw - 20, base)], fill=(232, 220, 200))
        d.line([(x, base - style.xh * px / 1000), (x + cw - 20, base - style.xh * px / 1000)], fill=(240, 232, 220))
        if text:
            w = text_width(sh, text, px)
            draw_text(img, (x + (cw - 20 - w) / 2, base), text, sh, px, INK, preshape=False)
        label(img, (x, y + 210), lab, path, 20, SOFT)
    out = os.path.join(PROOFS, f"THAI_glyphs_{style.weight_name}.png")
    img.save(out)
    return out


def wrap(shaper, text, px, width):
    """Greedy line breaking at spaces and zero-width spaces (U+200B), like TMP's word wrapping."""
    words, cur = [], ""
    for ch in text:
        cur += ch
        if ch in (" ", ZWSP):
            words.append(cur)
            cur = ""
    if cur:
        words.append(cur)
    lines, line = [], ""
    for w_ in words:
        if line and text_width(shaper, (line + w_).rstrip(" " + ZWSP), px) > width:
            lines.append(line)
            line = w_
        else:
            line += w_
    if line:
        lines.append(line)
    return lines


def proof_text(style, path, dark=False):
    """Game lines at 96, 48, 28 and 18 px, with the Latin of the same style; light or dark background."""
    sh = Shaper(path)
    bg, ink, soft = (NIGHT, NIGHT_INK, (190, 176, 200)) if dark else (CREAM, INK, SOFT)
    W = 2000
    blocks = [(96, GAME_LINES[:3]), (48, GAME_LINES[:6]), (28, GAME_LINES), (18, GAME_LINES)]
    H = 160 + sum(int(px * 1.75) * len(lines) * 2 + 70 for px, lines in blocks) + 220
    img = Image.new("RGB", (W, H), bg)
    label(img, (40, 70), f"Thai · {STYLE_LABEL[style.weight_name]} · {'dark' if dark else 'light'}", path, 36, ink)
    y = 150
    for px, lines in blocks:
        label(img, (40, y + 30), f"{px} px", path, 24, soft)
        y += 50
        for th, en in lines:
            y += int(px * 1.45)
            draw_text(img, (40, y), th, sh, px, ink)
            y += int(px * 1.25)
            label(img, (40, y), en, path, max(14, int(px * 0.8)), soft)
            y += int(px * 0.15)
        y += 40
    # stacked marks at 48 px
    y += 70
    draw_text(img, (40, y), STACKS, sh, 48, ink)
    out = os.path.join(PROOFS, f"THAI_text_{style.weight_name}_{'dark' if dark else 'light'}.png")
    img.crop((0, 0, W, y + 60)).save(out)
    return out


def proof_styles(paths):
    """The four styles side by side with the Latin of the same style."""
    W, H = 2400, 1500
    img = Image.new("RGB", (W, H), CREAM)
    y = 90
    for style, path in paths:
        sh = Shaper(path)
        label(img, (40, y), STYLE_LABEL[style.weight_name], path, 30, SOFT)
        y += 120
        x = 40
        x += draw_text(img, (x, y), "เพื่อนใหม่! ไข่ฟักแล้ว", sh, 84, INK) + 60
        label(img, (x, y), "New friend! The egg hatched", path, 84, INK)
        y += 90
        draw_text(img, (40, y + 30), GAME_LINES[4][0], sh, 40, INK)
        label(img, (40 + text_width(sh, GAME_LINES[4][0], 40) + 50, y + 30), GAME_LINES[4][1], path, 40, INK)
        y += 140
    out = os.path.join(PROOFS, "THAI_styles.png")
    img.crop((0, 0, W, min(H, y + 20))).save(out)
    return out


def proof_loops(style):
    """Looped (chosen) and loopless on one sheet, same metrics and weight."""
    p_loop = test_path(style)
    import tempfile
    p_free = os.path.join(tempfile.gettempdir(), "pw_thai_loopless_" + style.weight_name + ".ttf")
    build_font(style, p_free, loopless=True)
    img = Image.new("RGB", (2200, 1260), CREAM)
    rows = [("Bouclé (choisi)", p_loop), ("Sans boucles", p_free)]
    y = 100
    for title, path in rows:
        sh = Shaper(path)
        label(img, (40, y), title, p_loop, 34, SOFT)
        y += 150
        draw_text(img, (40, y), "บ ข ด ล ถ ภ ม น อ ว", sh, 110, INK)
        y += 140
        draw_text(img, (40, y), "เพื่อนใหม่! รับรางวัล ไข่ฟักแล้ว", sh, 72, INK)
        y += 90
        draw_text(img, (40, y), GAME_LINES[4][0], sh, 28, INK)
        draw_text(img, (1100, y), GAME_LINES[4][0], sh, 18, INK)
        y += 110
    out = os.path.join(PROOFS, "THAI_loops_vs_loopless.png")
    img.crop((0, 0, 2200, y)).save(out)
    return out


def proof_marks(style, path):
    """Mark positioning: TMP with mark/mkmk and the pre-shaper (shipped path), the legacy PUA fallback, and raw
    text without either (what TMP shows if mark/mkmk are off and no pre-shaping runs)."""
    sh = Shaper(path)
    rows = ["กิ กี กึ กื กั ก็ กํ ก่ ก้ ก๊ ก๋ ก์", "กุ กู กฺ กี่ กี้ กั้ กิ์ กุ่ กู้ ทำ ค่ำ น้ำ",
            "ปิ ปี่ ป้ ปั้น ปู่ ฝั่ง ฟื้น ฬ่ ฬิ", "ญุ ฐุ ฎุ ฏู ญ ฐ ฎ ฏ ฤๅ ฦๅ"]
    modes = [("GPOS mark + mkmk, pre-shaping (path for TMP)", dict(preshape=True, gpos=True, legacy=False), INK),
             ("legacy PUA pre-shaping, no GPOS (fallback)", dict(preshape=True, gpos=False, legacy=True), (150, 70, 40)),
             ("raw: no pre-shaping, no GPOS (to avoid)", dict(preshape=False, gpos=False, legacy=False), (120, 120, 140))]
    px = 84
    img = Image.new("RGB", (2100, len(modes) * (len(rows) * int(px * 1.9) + 90) + 60), CREAM)
    y = 60
    for title, kw, col in modes:
        label(img, (40, y + 20), title, path, 28, SOFT)
        y += 40
        for t in rows:
            y += int(px * 1.9)
            draw_text(img, (40, y - int(px * 0.55)), t, sh, px, col, **kw)
        y += 50
    out = os.path.join(PROOFS, f"THAI_marks_{style.weight_name}.png")
    img.save(out)
    return out


def proof_wrap(style, path):
    """Line breaks on U+200B: the same tutorial line in boxes of three widths."""
    sh = Shaper(path)
    text = GAME_LINES[4][0] + " " + GAME_LINES[6][0]
    px = 36
    img = Image.new("RGB", (2000, 420), CREAM)
    x = 40
    for width in (820, 520, 380):
        d = ImageDraw.Draw(img)
        lines = wrap(sh, text, px, width)
        d.rounded_rectangle((x - 16, 60, x + width + 16, 80 + len(lines) * int(px * 1.9) + 20), 18, outline=(220, 200, 170), width=3)
        y = 80 + int(px * 1.3)
        for ln in lines:
            draw_text(img, (x, y), ln.rstrip(" " + ZWSP), sh, px, INK)
            y += int(px * 1.9)
        x += width + 70
    label(img, (40, 390), "Coupures aux espaces de largeur nulle U+200B (glyphe vide, avance 0)", path, 26, SOFT)
    out = os.path.join(PROOFS, "THAI_line_breaks.png")
    img.save(out)
    return out


# ---------------------------------------------------------------------------------------------------------------
# Structure check against Leelawadee UI (correctness only: blurred comparison and hole counts, nothing kept)
# ---------------------------------------------------------------------------------------------------------------

LEELA = r"C:\Windows\Fonts\LeelawUI.ttf"


def _norm_glyph(font, ch, px=200, size=(48, 80), blur=3.0):
    import numpy as np
    from PIL import ImageFilter
    img = Image.new("L", (px * 2, px * 2), 0)
    d = ImageDraw.Draw(img)
    d.text((px // 2, int(px * 1.35)), ch, font=font, fill=255, anchor="ls")
    a = np.array(img)
    cols = np.where(a.max(axis=0) > 60)[0]
    rows = np.where(a.max(axis=1) > 60)[0]
    if len(cols) == 0:
        return None, None
    # crop: ink columns, but a fixed band of rows (baseline-relative) so descenders and ascenders count
    band = a[int(px * 0.15):int(px * 1.80), cols[0]:cols[-1] + 1]
    im = Image.fromarray(band).resize(size, Image.LANCZOS)
    sharp = np.array(im) > 110
    im = im.filter(ImageFilter.GaussianBlur(blur))
    v = np.array(im, dtype=float).ravel()
    v -= v.mean()
    n = np.linalg.norm(v)
    return (v / n if n else v), sharp


def _holes(font, ch, px=110):
    """Enclosed background regions (counters) of a glyph, by flood fill from the border."""
    import numpy as np
    img = Image.new("L", (px * 2, px * 2), 0)
    ImageDraw.Draw(img).text((px // 2, int(px * 1.35)), ch, font=font, fill=255, anchor="ls")
    ink = np.array(img) > 128
    bg = ~ink
    h, w = bg.shape
    seen = np.zeros_like(bg)
    count = 0
    for sy in range(h):
        for sx in range(w):
            if bg[sy, sx] and not seen[sy, sx]:
                stack = [(sy, sx)]
                seen[sy, sx] = True
                border = False
                size = 0
                while stack:
                    y, x = stack.pop()
                    size += 1
                    if y in (0, h - 1) or x in (0, w - 1):
                        border = True
                    for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                        if 0 <= ny < h and 0 <= nx < w and bg[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))
                if not border and size >= 4:
                    count += 1
    return count


def structure_check(path):
    """Top-1 and top-3 identity against Leelawadee UI after a strong blur, plus counter counts."""
    import numpy as np
    ours_f = ImageFont.truetype(path, 200)
    ref_f = ImageFont.truetype(LEELA, 200)
    chars = [c for c in M.THAI if not M.is_mark(c) and c != M.SARA_AM]
    A, B = [], []
    for c in chars:
        A.append(_norm_glyph(ours_f, c)[0])
        B.append(_norm_glyph(ref_f, c)[0])
    A, B = np.array(A), np.array(B)
    sim = A @ B.T
    top1, top3, confusions = 0, 0, []
    for i, c in enumerate(chars):
        order = list(np.argsort(-sim[i]))
        if order[0] == i:
            top1 += 1
        else:
            confusions.append((c, chars[order[0]], round(float(sim[i, i]), 3), round(float(sim[i, order[0]]), 3)))
        if i in order[:3]:
            top3 += 1
    hole_rows = []
    for c in chars:
        hole_rows.append((c, _holes(ImageFont.truetype(path, 110), c), _holes(ImageFont.truetype(LEELA, 110), c)))
    return dict(n=len(chars), top1=top1, top3=top3, confusions=confusions, holes=hole_rows, sim=sim, chars=chars)


def proof_check(path, result):
    """Side-by-side review sheet: our glyph, then the reference blurred to its structure, with the match."""
    from PIL import ImageFilter
    chars = result["chars"]
    sim = result["sim"]
    ours_f = ImageFont.truetype(path, 110)
    ref_f = ImageFont.truetype(LEELA, 110)
    cols, cw, chh = 6, 330, 200
    img = Image.new("RGB", (cols * cw + 40, ((len(chars) + cols - 1) // cols) * chh + 120), CREAM)
    label(img, (30, 60), f"Contrôle de structure · Leelawadee UI floutée, rendue pour vérifier, jamais copiée · top-1 {result['top1']}/{result['n']}",
          path, 28, INK)
    import numpy as np
    for i, c in enumerate(chars):
        x, y = 20 + (i % cols) * cw, 100 + (i // cols) * chh
        ImageDraw.Draw(img).text((x + 20, y + 140), c, font=ours_f, fill=INK, anchor="ls")
        ref = Image.new("L", (150, 190), 255)
        ImageDraw.Draw(ref).text((20, 140), c, font=ref_f, fill=0, anchor="ls")
        ref = ref.filter(ImageFilter.GaussianBlur(4.0))
        img.paste(Image.merge("RGB", [ref.point(lambda v: 255 - (255 - v) * 0.55)] * 3), (x + 150, y))
        best = int(np.argmax(sim[i]))
        ok = best == i
        label(img, (x, y + 185), ("ok " if ok else "x ") + "%04X" % ord(c), path, 18, (90, 140, 70) if ok else (190, 70, 50))
    out = os.path.join(PROOFS, "THAI_check_leelawadee.png")
    img.save(out)
    return out


# ---------------------------------------------------------------------------------------------------------------

def sha(path):
    import hashlib
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def build_all():
    out = []
    for style in styles():
        path, n = build_font(style)
        out.append((style, path, n))
        print(f"{path}: {n} glyphs, {os.path.getsize(path)} bytes")
    return out


def determinism():
    """Builds every test font twice into a scratch folder and compares bytes."""
    import tempfile
    same = True
    with tempfile.TemporaryDirectory() as tmp:
        for style in styles():
            a = os.path.join(tmp, "a.ttf")
            b = os.path.join(tmp, "b.ttf")
            M._CACHE.clear()
            build_font(style, a)
            M._CACHE.clear()
            build_font(style, b)
            ok = sha(a) == sha(b) == sha(test_path(style))
            same &= ok
            print(style.weight_name, "deterministic" if ok else "DIFFERENT")
    return same


def main(args):
    os.makedirs(PROOFS, exist_ok=True)
    built = build_all()
    if "fonts" in args:
        return
    paths = [(s, p) for s, p, _ in built]
    for style, path in paths:
        print(proof_glyphs(style, path))
        print(proof_text(style, path, dark=False))
        print(proof_text(style, path, dark=True))
        print(proof_marks(style, path))
    print(proof_styles(paths))
    print(proof_loops(styles()[0]))
    print(proof_wrap(styles()[0], paths[0][1]))
    res = structure_check(paths[0][1])
    print(proof_check(paths[0][1], res))
    print("structure check: top-1 %d/%d, top-3 %d/%d" % (res["top1"], res["n"], res["top3"], res["n"]))
    for c, got, s_self, s_best in res["confusions"]:
        print("  closest to another letter: U+%04X -> U+%04X (%.3f vs %.3f)" % (ord(c), ord(got), s_self, s_best))
    same_holes = sum(1 for _, a, b in res["holes"] if a == b)
    print("counters equal to the reference: %d/%d" % (same_holes, len(res["holes"])))
    for c, a, b in res["holes"]:
        if a != b:
            print("  U+%04X counters ours %d, reference %d" % (ord(c), a, b))
    if "determinism" in args or not args:
        print("deterministic:", determinism())


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "quick":
        print(quick(int(args[1]) if len(args) > 1 else 0, "loopless" in args))
    else:
        main(args)
