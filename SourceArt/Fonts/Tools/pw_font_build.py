"""Builds the Pocket Wilds fonts: TTF files with spacing and automatic kerning.

    python pw_font_build.py                  # the shipped families (Pocket Round, Pocket Plush)
    python pw_font_build.py PocketRound      # one family
    python pw_font_build.py --candidates     # the T31 review candidates (Douce, Claire, Caline, Bulle, Guimauve)

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
from pw_font_glyphs import SPACES, Glyphs, Style, charset, decompose

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

# Owner's choice, 5 October 2026: Caline for the interface (Pocket Round), Guimauve for titles (Pocket Plush).
FAMILIES = {
    "PocketRound": [
        Style("PocketRound", "Pocket Round", "Regular", w=92, xh=530, n=2.2, wf=1.06, a_story=1, aperture=38, swell=0.10),
        Style("PocketRound", "Pocket Round", "SemiBold", w=116, xh=530, n=2.2, wf=1.06, a_story=1, aperture=38, swell=0.10),
        Style("PocketRound", "Pocket Round", "Bold", w=144, xh=530, n=2.2, wf=1.08, a_story=1, aperture=38, swell=0.10),
    ],
    "PocketPlush": [
        Style("PocketPlush", "Pocket Plush", "Black", w=142, xh=590, n=2.1, wf=1.04, a_story=1, aperture=46, inflate=0.28,
              swell=0.08, tracking=-8, l_tail=False, grow=1.9, bar=0.78),
    ],
}
VERSION = "1.000"
COPYRIGHT = "Copyright 2026 Pocket Wilds. Rights holder to be confirmed by the project owner."

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


def build_style(style: Style):
    g = Glyphs(style)
    order = [".notdef", "space"]
    cmap = {}
    glyph_data = {}
    for ch in charset():
        shapes, lc, rc, upper, (x0, x1) = g.make(ch)
        contours = E.glyph_contours(shapes)
        if style.bounce or style.tilt:
            bx0, by0, bx1, by1 = E.bounds(contours)
            seed = sum(ord(c) for c in ch) * 7919 % 1000 / 1000.0
            dy = (seed - 0.5) * 2 * style.bounce
            ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
            if not ch.isalpha():
                dy, ang = dy * 0.3, 0.0
            contours = E.transform(contours, 0, dy, ang, (bx0 + bx1) / 2, (by0 + by1) / 2)
        # Spacing follows the letter; accents wider than a narrow letter (î, ĩ) may overhang a little.
        figure = ch.isdigit() and ch.isascii() and style.tabular
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
    cmap[0x00AD] = cmap[ord("-")]  # soft hyphen shows as a hyphen when a line breaks on it
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


def _accented_variants(bases):
    """Accented letters grouped by base letter: {'e': ['è', 'é', 'ê', 'ë', 'ē', 'ĕ', 'ė', 'ę', 'ě'], ...}."""
    out = {}
    for ch in charset():
        if ch in bases:
            continue
        try:
            base, _ = decompose(ch)
        except KeyError:
            continue
        base = {"ı": "i", "ȷ": "j"}.get(base, base)
        if base in bases:
            out.setdefault(base, []).append(ch)
    return out


def kerning(order, cmap, data, style):
    """Pairs of the base alphabet from ink profiles, then the same pairs with an accented right letter (Té, Vö, Yę).
    Every pair is measured on its own glyphs, so an accent never ends up touching the letter before it."""
    variants = _accented_variants(KERN_RIGHT)
    prof = {}
    for ch in set(KERN_LEFT + KERN_RIGHT + "H" + "".join(c for v in variants.values() for c in v)):
        name = cmap.get(ord(ch))
        if name:
            contours, adv, _ = data[name]
            prof[ch] = (profiles(contours, adv), adv)

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

    def value(a, b):
        if a not in prof or b not in prof:
            return None
        avg, mn = avg_gap(a, b)
        if avg is None:
            return None
        k = 0.55 * (ref - avg)
        limit = -(mn - 0.30 * ref)
        k = min(max(k, limit), 0.0)
        return int(round(k / 2) * 2) if k <= -14 else None

    pairs = {}
    for a in KERN_LEFT:
        for b in KERN_RIGHT:
            k = value(a, b)
            if k is None:
                continue
            pairs[(cmap[ord(a)], cmap[ord(b)])] = k
            for v in variants.get(b, []):
                kv = value(a, v)
                if kv is not None:
                    pairs[(cmap[ord(a)], cmap[ord(v)])] = kv
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
        "uniqueFontIdentifier": f"{family}-{sub}-{VERSION}",
        "fullName": f"{family} {sub}",
        "psName": (family + "-" + sub).replace(" ", ""),
        "version": f"Version {VERSION}",
        "copyright": COPYRIGHT,
        "typographicFamily": family,
        "typographicSubfamily": sub,
    })
    weight_class = {"Regular": 400, "SemiBold": 600, "Bold": 700, "Black": 900}[sub]
    fb.setupOS2(sTypoAscender=950, sTypoDescender=-250, sTypoLineGap=0, usWinAscent=1000, usWinDescent=300,
                sxHeight=int(style.xh), sCapHeight=int(style.cap), usWeightClass=weight_class,
                fsType=0, achVendID="PWLD")
    fb.setupPost()
    fb.setupHead(unitsPerEm=UPM, fontRevision=float(VERSION))
    font = fb.font
    if pairs and len(pairs) <= 10000:  # the legacy kern subtable is limited to 64 KB; GPOS always has every pair
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
    if pairs:
        fea = "languagesystem DFLT dflt;\nlanguagesystem latn dflt;\nfeature kern {\n"
        fea += "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(pairs.items()))
        fea += "} kern;\n"
        addOpenTypeFeaturesFromString(font, fea)
    font.save(path)


def write_kerning(data, pairs, path):
    """Pairs as characters for Unity (FontPipeline reads them; TextCore's own import caches stale font tables)."""
    lines = ["# left\tright\tvalue (font units, 1000 per em)"]
    for (a, b), v in sorted(pairs.items()):
        lines.append(f"{ord(data[a][2]):04X}\t{ord(data[b][2]):04X}\t{v}")
    with open(path[:-4] + ".kern.tsv", "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def file_name(style):
    return (style.family + "-" + style.weight_name).replace(" ", "")


def build(names=None, families=None):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "charset.txt"), "w", encoding="utf-8") as f:
        f.write("".join(charset() + list(SPACES.keys())))
    built = []
    for key, styles in (families or FAMILIES).items():
        if names and key not in names:
            continue
        for style in styles:
            order, cmap, data = build_style(style)
            pairs = kerning(order, cmap, data, style)
            path = os.path.join(OUT, file_name(style) + ".ttf")
            write_font(style, order, cmap, data, pairs, path)
            write_kerning(data, pairs, path)
            built.append((path, len(order), len(pairs)))
            print(f"{path}: {len(order)} glyphs, {len(pairs)} kerning pairs")
    return built


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--candidates" in args:
        args.remove("--candidates")
        build(args or None, CANDIDATES)
    else:
        build(args or None)
