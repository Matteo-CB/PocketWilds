"""Builds the Pocket Wilds font candidates (T31): TTF files with spacing and automatic kerning.

    python pw_font_build.py            # every candidate
    python pw_font_build.py Claire     # one family

Output: SourceArt/Fonts/Build/<File>.ttf
"""
import math
import os
import sys

import numpy as np
from fontTools.agl import UV2AGL
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from PIL import Image, ImageDraw

import pw_font_engine as E
from pw_font_glyphs import ACCENTS, CHARS, SPACES, Glyphs, Style

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Build"))
UPM = 1000

# Interface candidates (Pocket Round) and title candidates (Pocket Plush).
CANDIDATES = {
    "Douce": [
        Style("Douce", "PW Round Douce", "Regular", w=88, xh=520, n=2.0, wf=1.00, a_story=2, aperture=40),
        Style("Douce", "PW Round Douce", "SemiBold", w=112, xh=520, n=2.0, wf=1.00, a_story=2, aperture=40),
        Style("Douce", "PW Round Douce", "Bold", w=140, xh=520, n=2.0, wf=1.02, a_story=2, aperture=40),
    ],
    "Claire": [
        Style("Claire", "PW Round Claire", "Regular", w=86, xh=550, n=2.55, wf=0.93, a_story=2, aperture=44),
        Style("Claire", "PW Round Claire", "SemiBold", w=110, xh=550, n=2.55, wf=0.93, a_story=2, aperture=44),
        Style("Claire", "PW Round Claire", "Bold", w=138, xh=550, n=2.55, wf=0.95, a_story=2, aperture=44),
    ],
    "Caline": [
        Style("Caline", "PW Round Caline", "Regular", w=92, xh=530, n=2.2, wf=1.06, a_story=1, aperture=38, swell=0.10),
        Style("Caline", "PW Round Caline", "SemiBold", w=116, xh=530, n=2.2, wf=1.06, a_story=1, aperture=38, swell=0.10),
        Style("Caline", "PW Round Caline", "Bold", w=144, xh=530, n=2.2, wf=1.08, a_story=1, aperture=38, swell=0.10),
    ],
    "Bulle": [
        Style("Bulle", "PW Plush Bulle", "Black", w=150, xh=600, n=2.35, wf=0.98, a_story=1, aperture=48, bounce=16, tilt=2.0,
              tracking=-12, l_tail=False, grow=1.9, bar=0.80),
    ],
    "Guimauve": [
        Style("Guimauve", "PW Plush Guimauve", "Black", w=142, xh=590, n=2.1, wf=1.04, a_story=1, aperture=46, inflate=0.28,
              swell=0.08, tracking=-8, l_tail=False, grow=1.9, bar=0.78),
    ],
}

SB = {"S": 66.0, "R": 42.0, "D": 14.0, "O": 26.0, "P": 46.0}


def glyph_name(ch):
    cp = ord(ch)
    if ch == " ":
        return "space"
    return UV2AGL.get(cp, "uni%04X" % cp)


def side_bearing(style, cls, upper):
    v = SB[cls] * (1.0 + (style.w - 88.0) * 0.0018) + style.tracking
    if upper and cls in "SR":
        v *= 1.12
    return v


def make_glyph(g: Glyphs, style: Style, ch):
    if ch in ACCENTS:
        base, kind = ACCENTS[ch]
        shapes, lc, rc = getattr(g, CHARS[base])()
        bx0, _, bx1, _ = E.bounds(E.glyph_contours(shapes))
        upper = base.isupper()
        cx = (bx0 + bx1) / 2
        if base == "ı":
            cx = g.h
        base_y = style.cap if upper else style.xh
        shapes = shapes + g.accent(kind, cx, base_y, upper)
        return shapes, lc, rc, upper
    shapes, lc, rc = getattr(g, CHARS[ch])()
    upper = ch.isupper() or ch in "ÆŒØ"
    return shapes, lc, rc, upper


def build_style(style: Style):
    g = Glyphs(style)
    order = [".notdef", "space"]
    cmap = {}
    glyph_data = {}
    rng = np.random.default_rng(7)
    chars = list(CHARS.keys()) + list(ACCENTS.keys())
    for i, ch in enumerate(chars):
        shapes, lc, rc, upper = make_glyph(g, style, ch)
        contours = E.glyph_contours(shapes)
        if style.bounce or style.tilt:
            x0, y0, x1, y1 = E.bounds(contours)
            seed = sum(ord(c) for c in ch) * 7919 % 1000 / 1000.0
            dy = (seed - 0.5) * 2 * style.bounce
            ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
            if not ch.isalpha():
                dy, ang = dy * 0.3, 0.0
            contours = E.transform(contours, 0, dy, ang, (x0 + x1) / 2, (y0 + y1) / 2)
        x0, y0, x1, y1 = E.bounds(contours)
        figure = ch.isdigit() and style.tabular
        if figure:
            adv = g.dw + 2 * 58 * (1 + (style.w - 88) * 0.0018) + style.tracking * 2
            lsb = (adv - (x1 - x0)) / 2
        else:
            lsb = side_bearing(style, lc, upper)
            rsb = side_bearing(style, rc, upper)
            adv = lsb + (x1 - x0) + rsb
        contours = E.transform(contours, lsb - x0, 0)
        contours = [E.simplify(c) for c in contours]
        name = glyph_name(ch)
        glyph_data[name] = (contours, int(round(adv)), ch)
        order.append(name)
        cmap[ord(ch)] = name
    space_names = {" ": "space", " ": "uni00A0", " ": "uni202F", " ": "uni2009"}
    for ch, em in SPACES.items():
        name = space_names[ch]
        if name not in order:
            order.append(name)
        width = em * UPM * (1.0 + (style.w - 88) * 0.001) + style.tracking
        glyph_data[name] = ([], int(round(width)), ch)
        cmap[ord(ch)] = name
    # .notdef: a soft rounded box
    nd = [E.simplify(c) for c in E.glyph_contours([g.bowl(0, 420, 0, style.cap, 3.0)])]
    nd = E.transform(nd, 60, 0)
    glyph_data[".notdef"] = ([E.simplify(c) for c in nd], 540, None)
    return order, cmap, glyph_data


def render_mask(contours, adv, scale=0.25):
    w = int(math.ceil((adv + 400) * scale))
    h = int(math.ceil(1300 * scale))
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for c in contours:
        pts = [((x + 200) * scale, (1000 - y) * scale) for x, y in c]
        if len(pts) >= 3:
            d.polygon(pts, fill=255)
    return np.array(img) > 0


def profiles(contours, adv, scale=0.25, band=24):
    """Leftmost and rightmost ink per horizontal band (font units)."""
    m = render_mask(contours, adv, scale)
    rows = m.shape[0]
    bands = {}
    step = max(1, int(band * scale))
    for r0 in range(0, rows, step):
        sub = m[r0 : r0 + step]
        cols = np.where(sub.any(axis=0))[0]
        if len(cols) == 0:
            continue
        y = 1000 - (r0 + step / 2) / scale
        bands[round(y)] = (cols[0] / scale - 200, (cols[-1] + 1) / scale - 200)
    return bands


KERN_LEFT = "AFKLPRTVWXYkrvwxyfDOQ0.,’'\"”"
KERN_RIGHT = "AJTVWYXacdegoqsuvwxyzOQCG.,’'\"“"


def kerning(order, cmap, data, style):
    prof = {}
    for ch in set(KERN_LEFT + KERN_RIGHT + "H"):
        name = cmap.get(ord(ch))
        if name:
            contours, adv, _ = data[name]
            prof[ch] = (profiles(contours, adv), adv)
    href, hadv = prof["H"]

    def avg_gap(a, b):
        pa, adv_a = prof[a]
        pb, _ = prof[b]
        gaps = []
        for y, (l, r) in pa.items():
            if y in pb:
                gaps.append(adv_a - r + pb[y][0])
        if not gaps:
            return None, None
        return sum(gaps) / len(gaps), min(gaps)

    ref, _ = avg_gap("H", "H")
    pairs = {}
    for a in KERN_LEFT:
        for b in KERN_RIGHT:
            if a not in prof or b not in prof:
                continue
            avg, mn = avg_gap(a, b)
            if avg is None:
                continue
            k = 0.55 * (ref - avg)
            limit = -(mn - 0.30 * ref)
            k = max(k, limit)
            k = min(k, 0.0)
            if k <= -14:
                pairs[(cmap[ord(a)], cmap[ord(b)])] = int(round(k / 2) * 2)
    return pairs


def write_font(style: Style, order, cmap, data, pairs, path):
    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    glyphs = {}
    metrics = {}
    for name in order:
        contours, adv, _ = data[name]
        pen = TTGlyphPen(None)
        for c in contours:
            if len(c) < 3:
                continue
            pen.moveTo(tuple(int(v) for v in c[0]))
            for p in c[1:]:
                pen.lineTo(tuple(int(v) for v in p))
            pen.closePath()
        gl = pen.glyph()
        glyphs[name] = gl
        xmin = min((int(p[0]) for c in contours for p in c), default=0)
        metrics[name] = (adv, xmin)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=950, descent=-250)
    family = style.family
    sub = style.weight_name
    fb.setupNameTable({
        "familyName": family if sub in ("Regular", "Bold") else family + " " + sub,
        "styleName": sub if sub in ("Regular", "Bold") else "Regular",
        "uniqueFontIdentifier": f"{family}-{sub}-0.1",
        "fullName": f"{family} {sub}",
        "psName": (family + "-" + sub).replace(" ", ""),
        "version": "Version 0.100",
        "copyright": "Copyright 2026, Pocket Wilds project owner. Candidate, not final.",
        "typographicFamily": family,
        "typographicSubfamily": sub,
    })
    weight_class = {"Regular": 400, "SemiBold": 600, "Bold": 700, "Black": 900}[sub]
    fb.setupOS2(sTypoAscender=950, sTypoDescender=-250, sTypoLineGap=0, usWinAscent=1000, usWinDescent=300,
                sxHeight=int(style.xh), sCapHeight=int(style.cap), usWeightClass=weight_class,
                fsType=0, achVendID="PWLD")
    fb.setupPost()
    fb.setupHead(unitsPerEm=UPM)
    font = fb.font
    if pairs:
        kern = newTable("kern")
        kern.version = 0
        st = KernTable_format_0()
        st.version = 0
        st.coverage = 1
        st.format = 0
        st.kernTable = dict(pairs)
        st.apple = False
        kern.kernTables = [st]
        font["kern"] = kern
        fea = "languagesystem DFLT dflt;\nlanguagesystem latn dflt;\nfeature kern {\n"
        fea += "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(pairs.items()))
        fea += "} kern;\n"
        addOpenTypeFeaturesFromString(font, fea)
    font.save(path)


def file_name(style):
    return (style.family + "-" + style.weight_name).replace(" ", "")


def build(names=None):
    os.makedirs(OUT, exist_ok=True)
    charset = "".join(list(CHARS.keys()) + list(ACCENTS.keys()) + list(SPACES.keys()))
    with open(os.path.join(OUT, "charset.txt"), "w", encoding="utf-8") as f:
        f.write(charset)
    built = []
    for key, styles in CANDIDATES.items():
        if names and key not in names:
            continue
        for style in styles:
            order, cmap, data = build_style(style)
            pairs = kerning(order, cmap, data, style)
            path = os.path.join(OUT, file_name(style) + ".ttf")
            write_font(style, order, cmap, data, pairs, path)
            built.append((path, len(order), len(pairs)))
            print(f"{path}: {len(order)} glyphs, {len(pairs)} kerning pairs")
    return built


if __name__ == "__main__":
    build(sys.argv[1:] or None)
