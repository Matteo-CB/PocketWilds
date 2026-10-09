"""Builds the Pocket Wilds fonts: TTF files with spacing and automatic kerning.

    python pw_font_build.py                  # the shipped families (Pocket Round, Pocket Plush): the Latin core
    python pw_font_build.py PocketRound      # one family
    python pw_font_build.py --candidates     # the T31 review candidates (Douce, Claire, Caline, Bulle, Guimauve)

Script fallbacks (T39C-6), one TTF per script and region beside each core file (see SCRIPT_FONTS below):
    python pw_font_build.py --scripts               # the core, then every fallback, the report and the proof
    python pw_font_build.py --scripts=thai,han      # the core, then some fallbacks (tag or module SCRIPT name)
    python pw_font_build.py --only-scripts[=...]    # the fallbacks only: the core files are not rewritten
    python pw_font_build.py --check[=...]           # builds in memory and compares with SourceArt/Fonts/Build
The core bytes never depend on the fallbacks: the plain command writes exactly what it always wrote.

Output: SourceArt/Fonts/Build/<File>.ttf and <File>-<Tag>.ttf
"""
import hashlib
import importlib
import importlib.util
import io
import math
import os
import re
import sys
from dataclasses import dataclass

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
COPYRIGHT = "Copyright 2026 HiddenLab. All rights reserved."
MANUFACTURER = "HiddenLab"

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
        "manufacturer": MANUFACTURER,
        "designer": MANUFACTURER,
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


def build(names=None, families=None, keep=None):
    """Writes the core fonts. `keep` (a dict) receives each style's (order, cmap, data, pairs) for the fallbacks."""
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
            if keep is not None:
                keep[file_name(style)] = (order, cmap, data, pairs)
    return built


# ------------------------------------------------------------------------------------------- script fallbacks (T39C-6)
#
# TextMeshPro loads the core of a style for every language and adds the fallbacks of the active language only
# (Docs/Design/Localization_Plan.md, 2.4). Each fallback holds one script (or one region of it) and nothing of the
# core: no Latin, no space, no digits. Small scripts get static atlases, Hangul and Han dynamic ones.
#
#   <File>-CyrGrk.ttf    Cyrillic and Greek, kerning                        ru, uk, el
#   <File>-Viet.ttf      Vietnamese (and ə, the Croatian digraphs)          vi
#   <File>-Thai.ttf      Thai, GPOS mark and mkmk (+ ccmp)                  th
#   <File>-Kana.ttf      kana, CJK punctuation, full-width forms (Japan)    ja
#   <File>-PunctSC.ttf   CJK punctuation and full-width forms, Simplified Chinese shapes in the cmap    zh-Hans
#   <File>-PunctTC.ttf   the same with Traditional Chinese shapes                                       zh-Hant
#   <File>-Hangul.ttf    compatibility jamo and the 11,172 syllables (composites of the jamo parts)     ko
#   <File>-HanJP.ttf, -HanSC.ttf, -HanTC.ttf   the Han ideographs of one region                     ja, zh-Hans, zh-Hant
#
# TextMeshPro has no locl, so a region's shapes live in its own file under the standard code points.
#
# Glyph index ranges. TextMeshPro finds kerning and mark records by glyph index in the asset of the current character
# without checking which asset its neighbour comes from (TMP_Text.cs: "Handle Kerning", "Handle Diacritical Marks").
# A fallback whose indices overlapped the core's would get the record of an unrelated pair ("Г" then a space, or
# "A" then a kana). So indices never overlap: the core stays below 512, a fallback with kerning or mark records owns
# a range of 256, and the fallbacks without records start at 1280 (they may share it: they hold no record). Empty
# placeholder glyphs fill the gap, a few bytes each.
#
# Every fallback has the core's vertical metrics (hhea 950 / -250 / 0, OS/2 typo the same, win 1000 / 300), fixed
# timestamps, and post format 3: no glyph names (TextMeshPro and FreeType only use glyph indices).

CORE_GID_LIMIT = 512
OWN_RANGE = 256
SHARED_FIRST_GID = 1280


@dataclass(frozen=True)
class ScriptFont:
    tag: str            # file suffix: <File>-<tag>.ttf
    module: str         # the script module (pw_font_<script>.py beside this file)
    kind: str           # glyph collector: "simple", "kana" or "hangul"
    region: str | None  # Han region (JP, SC, TC) or kana locale (ja, zh-Hans, zh-Hant)
    first_gid: int      # glyph index of the first script glyph
    languages: tuple    # languages that load this file


SCRIPT_FONTS = [
    ScriptFont("CyrGrk", "pw_font_cyrillic_greek", "simple", None, 512, ("ru", "uk", "el")),
    ScriptFont("Viet", "pw_font_vietnamese", "simple", None, 768, ("vi",)),
    ScriptFont("Thai", "pw_font_thai", "simple", None, 1024, ("th",)),
    ScriptFont("Kana", "pw_font_kana", "kana", "ja", SHARED_FIRST_GID, ("ja",)),
    ScriptFont("PunctSC", "pw_font_kana", "kana", "zh-Hans", SHARED_FIRST_GID, ("zh-Hans",)),
    ScriptFont("PunctTC", "pw_font_kana", "kana", "zh-Hant", SHARED_FIRST_GID, ("zh-Hant",)),
    ScriptFont("Hangul", "pw_font_hangul", "hangul", None, SHARED_FIRST_GID, ("ko",)),
    # Han plugs in like any other module: charset(region) and make(ch, style, region). A region font is skipped
    # while its module is absent, and a new region is one more line here.
    ScriptFont("HanJP", "pw_font_han", "simple", "JP", SHARED_FIRST_GID, ("ja",)),
    ScriptFont("HanSC", "pw_font_han", "simple", "SC", SHARED_FIRST_GID, ("zh-Hans",)),
    ScriptFont("HanTC", "pw_font_han", "simple", "TC", SHARED_FIRST_GID, ("zh-Hant",)),
]

# The per-language list (core first): what Unity adds to the core asset's fallbacks when a language is active.
LANGUAGE_ORDER = ["ru", "uk", "el", "vi", "th", "ja", "ko", "zh-Hans", "zh-Hant"]
LINEBREAK_LEADING = "linebreak_leading.txt"     # TMP Settings > Line Breaking > Leading Characters
LINEBREAK_FOLLOWING = "linebreak_following.txt"  # TMP Settings > Line Breaking > Following Characters
REPORT = "scripts_report.txt"
METRICS = dict(ascent=950, descent=-250, line_gap=0, win_ascent=1000, win_descent=300)


def script_module(sf):
    """The module of a fallback, or None while it does not exist yet (a script still being drawn)."""
    if importlib.util.find_spec(sf.module) is None:
        return None
    return importlib.import_module(sf.module)


def select_scripts(selectors):
    """Fallbacks named by tag (HanJP) or by their module's SCRIPT (kana: Kana, PunctSC, PunctTC). None: all."""
    out = []
    for sf in SCRIPT_FONTS:
        mod = script_module(sf)
        if mod is None:
            print(f"{sf.tag}: {sf.module}.py not found, skipped")
            continue
        keys = {sf.tag.lower(), str(getattr(mod, "SCRIPT", "")).lower()}
        if not selectors or keys & {s.lower() for s in selectors}:
            out.append(sf)
    return out


def place_glyph(style, key, made):
    """build_style's steps for one Glyphs.make tuple: bounce and tilt, side bearings, simplify. Class "Z" on both sides
    is a zero-width glyph whose ink stays where it is drawn (Thai marks). Returns (contours, advance)."""
    shapes, lc, rc, upper, (x0, x1) = made
    contours = E.glyph_contours(shapes)
    if (style.bounce or style.tilt) and contours:  # U+3000 and the like have no ink
        bx0, by0, bx1, by1 = E.bounds(contours)
        seed = sum(ord(c) for c in key) * 7919 % 1000 / 1000.0
        dy = (seed - 0.5) * 2 * style.bounce
        ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
        if not key.isalpha():
            dy, ang = dy * 0.3, 0.0
        contours = E.transform(contours, 0, dy, ang, (bx0 + bx1) / 2, (by0 + by1) / 2)
    if lc == rc == "Z":
        dx, adv = 0.0, 0.0
    else:
        lsb = side_bearing(style, lc, upper)
        adv = lsb + (x1 - x0) + side_bearing(style, rc, upper)
        dx = lsb - x0
    contours = E.transform(contours, dx, 0) if contours else []
    return [E.simplify(c) for c in contours], int(round(adv))


# A glyph record: (name, contours or None, advance, code point or None, components or None). Components are
# (part name, dx, dy) for a TrueType composite.

def glyphs_simple(sf, mod, style):
    region = (sf.region,) if sf.region else ()
    out = []
    for ch in mod.charset(*region):
        if hasattr(mod, "place") and not region:
            contours, adv = mod.place(ch, style)       # the module's own placement (Thai)
        else:
            contours, adv = place_glyph(style, ch, mod.make(ch, style, *region))
        out.append((glyph_name(ch), contours, adv, ord(ch), None))
    return out


def glyphs_kana(sf, mod, style):
    """Kana (ja): the whole module, its regional extras and locl. PunctSC / PunctTC: punctuation and full-width forms
    only, with the region's shapes under the standard code points (region_cmap), curly quotes included."""
    regional = mod.region_cmap(sf.region)
    if sf.region == "ja":
        chars, extras = mod.charset(), mod.extra_glyphs()
    else:
        kana = set(mod.HIRAGANA + mod.KATAKANA + mod.KATAKANA_EXT)
        chars, extras = [c for c in mod.charset() if c not in kana], []
    chars = chars + [chr(cp) for cp in sorted(regional) if chr(cp) not in chars]
    out = []
    for ch in chars:
        extra = regional.get(ord(ch))
        contours, adv = place_glyph(style, ch, mod.make_extra(extra, style) if extra else mod.make(ch, style))
        assert adv == mod.advance(ch, style), (sf.tag, ch, adv)
        out.append((glyph_name(ch), contours, adv, ord(ch), None))
    names = {g[0] for g in out}
    for name in extras:
        # an extra reachable only through locl from a glyph of the core (the curly quotes) is useless here
        if glyph_name(mod.EXTRA_BASE[name]) in names:
            contours, adv = place_glyph(style, mod.EXTRA_BASE[name], mod.make_extra(name, style))
            out.append((name, contours, adv, None, None))
    return out


def glyphs_hangul(sf, mod, style):
    """Jamo parts as unencoded outlines, then the compatibility jamo, then every syllable as a composite of its parts
    (offset 0: the parts are drawn in place). A style with bounce or tilt gets full outlines instead."""
    if style.bounce or style.tilt:
        return glyphs_simple(sf, mod, style)
    chars = mod.charset()
    comps = {ch: mod.composite(ch, style) for ch in chars}
    need = {p for c in comps.values() if c for p in c}
    parts = mod.parts(style)
    out = []
    for name in mod.part_order(style):
        if name in need:
            out.append((name, [E.simplify(c) for c in E.glyph_contours(parts[name])], mod.advance("", style), None, None))
    for ch in chars:
        if comps[ch]:
            out.append((glyph_name(ch), None, mod.advance(ch, style), ord(ch), [(p, 0, 0) for p in comps[ch]]))
        else:
            contours, adv = place_glyph(style, ch, mod.make(ch, style))
            out.append((glyph_name(ch), contours, adv, ord(ch), None))
    return out


COLLECTORS = {"simple": glyphs_simple, "kana": glyphs_kana, "hangul": glyphs_hangul}


def languagesystems(mod):
    """DFLT dflt first, then the module's systems ("thai dflt" or ("cyrl", "dflt")), without duplicates."""
    out = [("DFLT", "dflt")]
    for ls in getattr(mod, "LANGUAGESYSTEMS", None) or [("latn", "dflt")]:
        pair = tuple(ls.split()) if isinstance(ls, str) else tuple(ls)
        if pair not in out:
            out.append(pair)
    return out


_SUB = re.compile(r"^\s*sub\s+([A-Za-z0-9_.]+)\s+by\s+([A-Za-z0-9_.]+)\s*;\s*$")


def keep_present(fea, names):
    """Drops single substitutions about glyphs this file does not hold (kana locl of the core's curly quotes)."""
    lines = []
    for line in fea.splitlines(keepends=True):
        m = _SUB.match(line)
        if m and not (m.group(1) in names and m.group(2) in names):
            continue
        lines.append(line)
    return "".join(lines)


def _glyph(contours):
    pen = TTGlyphPen(None)
    for c in contours:
        if len(c) < 3:
            continue
        pen.moveTo(tuple(int(v) for v in c[0]))
        for p in c[1:]:
            pen.lineTo(tuple(int(v) for v in p))
        pen.closePath()
    return pen.glyph()


def write_script_font(style, sf, order, cmap, records, notdef, fea, pairs):
    """write_font for a fallback: composites, placeholders, the module's features, post format 3. Returns the font."""
    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    glyphs = {".notdef": _glyph(notdef[0])}
    for name in order[1:sf.first_gid]:
        glyphs[name] = _glyph([])
    for name, contours, _, _, comps in records:
        if comps is None:
            glyphs[name] = _glyph(contours)
        else:
            pen = TTGlyphPen(glyphs)
            for part, dx, dy in comps:
                pen.addComponent(part, (1, 0, 0, 1, dx, dy))
            glyphs[name] = pen.glyph()
    fb.setupGlyf(glyphs)
    glyf = fb.font["glyf"]
    metrics = {".notdef": (notdef[1], min((int(p[0]) for c in notdef[0] for p in c), default=0))}
    for name in order[1:sf.first_gid]:
        metrics[name] = (0, 0)
    for name, contours, adv, _, comps in records:
        if comps is None:
            metrics[name] = (adv, min((int(p[0]) for c in contours for p in c), default=0))
        else:
            metrics[name] = (adv, getattr(glyf[name], "xMin", 0))
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=METRICS["ascent"], descent=METRICS["descent"])
    family = f"{style.family} {sf.tag}"
    sub = style.weight_name
    fb.setupNameTable({
        "familyName": family if sub in ("Regular", "Bold") else family + " " + sub,
        "styleName": sub if sub in ("Regular", "Bold") else "Regular",
        "uniqueFontIdentifier": f"{family}-{sub}-{VERSION}",
        "fullName": f"{family} {sub}",
        "psName": (family + "-" + sub).replace(" ", ""),
        "version": f"Version {VERSION}",
        "copyright": COPYRIGHT,
        "manufacturer": MANUFACTURER,
        "designer": MANUFACTURER,
        "typographicFamily": family,
        "typographicSubfamily": sub,
    })
    weight_class = {"Regular": 400, "SemiBold": 600, "Bold": 700, "Black": 900}[sub]
    fb.setupOS2(sTypoAscender=METRICS["ascent"], sTypoDescender=METRICS["descent"], sTypoLineGap=METRICS["line_gap"],
                usWinAscent=METRICS["win_ascent"], usWinDescent=METRICS["win_descent"],
                sxHeight=int(style.xh), sCapHeight=int(style.cap), usWeightClass=weight_class,
                fsType=0, achVendID="PWLD")
    fb.setupPost(keepGlyphNames=False)
    fb.setupHead(unitsPerEm=UPM, fontRevision=float(VERSION))  # created = modified = 0: identical bytes every run
    font = fb.font
    if pairs and len(pairs) <= 10000:
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
    if fea:
        addOpenTypeFeaturesFromString(font, fea)
    return font


def mark_records(font):
    """Mark-to-base and mark-to-mark attachments of the compiled GPOS, as (lookup, base glyph, base anchor, mark
    glyph, mark anchor): the records TextMeshPro builds from the font, written out for FontPipeline like kerning."""
    if "GPOS" not in font:
        return []
    rows = []
    for lookup in font["GPOS"].table.LookupList.Lookup:
        for st in lookup.SubTable:
            kind = lookup.LookupType
            if kind == 9:
                kind, st = st.ExtensionLookupType, st.ExtSubTable
            if kind == 4:
                marks = list(zip(st.MarkCoverage.glyphs, st.MarkArray.MarkRecord))
                for base, rec in zip(st.BaseCoverage.glyphs, st.BaseArray.BaseRecord):
                    for mark, mrec in marks:
                        a = rec.BaseAnchor[mrec.Class]
                        if a is not None:
                            rows.append(("mark", base, (a.XCoordinate, a.YCoordinate), mark,
                                         (mrec.MarkAnchor.XCoordinate, mrec.MarkAnchor.YCoordinate)))
            elif kind == 6:
                marks = list(zip(st.Mark1Coverage.glyphs, st.Mark1Array.MarkRecord))
                for base, rec in zip(st.Mark2Coverage.glyphs, st.Mark2Array.Mark2Record):
                    for mark, mrec in marks:
                        a = rec.Mark2Anchor[mrec.Class]
                        if a is not None:
                            rows.append(("mkmk", base, (a.XCoordinate, a.YCoordinate), mark,
                                         (mrec.MarkAnchor.XCoordinate, mrec.MarkAnchor.YCoordinate)))
    return rows


def _codepoints(cmap):
    out = {}
    for cp, name in sorted(cmap.items()):
        out.setdefault(name, cp)
    return out


def tsv_kerning(pairs, cps):
    lines = ["# left\tright\tvalue (font units, 1000 per em)"]
    for a, b, v in sorted((cps[a], cps[b], v) for (a, b), v in pairs.items()):
        lines.append(f"{a:04X}\t{b:04X}\t{v}")
    return "\n".join(lines) + "\n"


def tsv_cross(cross, cps, tag):
    lines = ["# Pairs across the core and this font. TextMeshPro reads a pair from the asset of its LEFT character",
             "# (column 'asset'); the right glyph index comes from the other font. The disjoint index ranges make",
             "# these records safe to keep in the asset permanently.",
             "# left\tright\tvalue (font units, 1000 per em)\tasset"]
    rows = sorted((cps[a], cps[b], v, tag if own_left else "core") for (a, b), (v, own_left) in cross.items())
    lines += [f"{a:04X}\t{b:04X}\t{v}\t{owner}" for a, b, v, owner in rows]
    return "\n".join(lines) + "\n"


def tsv_marks(rows, cps):
    lines = ["# lookup\tbase\tbase x\tbase y\tmark\tmark x\tmark y (code points; anchors in font units, 1000 per em)",
             "# mark = MarkToBase (base: a letter), mkmk = MarkToMark (base: the mark the second mark sits on)"]
    out = sorted((k, cps[b], ba, cps[m], ma) for k, b, ba, m, ma in rows)
    lines += [f"{k}\t{b:04X}\t{ba[0]}\t{ba[1]}\t{m:04X}\t{ma[0]}\t{ma[1]}" for k, b, ba, m, ma in out]
    return "\n".join(lines) + "\n"


def core_of(style):
    order, cmap, data = build_style(style)
    return order, cmap, data, kerning(order, cmap, data, style)


def build_script_font(sf, style, core):
    """One fallback for one style. core = (order, cmap, data, pairs) of the style's core font.
    Returns {file name: bytes} (the TTF and its sidecars) and a summary dict."""
    mod = script_module(sf)
    records = COLLECTORS[sf.kind](sf, mod, style)
    names = [r[0] for r in records]
    assert len(set(names)) == len(names), f"{sf.tag}: duplicate glyph names"
    core_order, core_cmap, core_data, core_pairs = core
    assert len(core_order) <= CORE_GID_LIMIT, "the core outgrew its glyph index range: raise CORE_GID_LIMIT"
    assert sf.first_gid >= CORE_GID_LIMIT
    order = [".notdef"] + [f"pad{i:04d}" for i in range(1, sf.first_gid)] + names
    cmap = {r[3]: r[0] for r in records if r[3] is not None}
    own = set(names)

    # Kerning, measured as the test fonts measure it (core and script glyphs together), then split: pairs inside this
    # font go in its GPOS and .kern.tsv, pairs across the core and this font in .crosskern.tsv.
    intra, cross = {}, {}
    if hasattr(mod, "kerning"):
        cmap_all = dict(core_cmap)
        cmap_all.update(cmap)
        data_all = dict(core_data)
        data_all.update({r[0]: (r[1], r[2], chr(r[3]) if r[3] is not None else None) for r in records})
        for (a, b), v in mod.kerning(cmap_all, data_all, style, core_pairs).items():
            if a in own and b in own:
                intra[(a, b)] = v
            elif a in own or b in own:
                cross[(a, b)] = (v, a in own)

    # The module's features (Thai ccmp, mark, mkmk; kana locl), minus rules about glyphs of the core. The Chinese
    # punctuation files need no locl: their cmap already gives the regional shapes.
    fea = ""
    body = "" if sf.kind == "kana" and sf.region != "ja" else getattr(mod, "features", lambda s: "")(style)
    body = keep_present(body, own)
    if intra or body.strip():
        fea = "".join(f"languagesystem {s} {l};\n" for s, l in languagesystems(mod))
        if intra:
            fea += "feature kern {\n" + "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(intra.items())) + "} kern;\n"
        fea += body
    font = write_script_font(style, sf, order, cmap, records, core_data[".notdef"][:2], fea, intra)
    marks = mark_records(font)
    if sf.first_gid >= SHARED_FIRST_GID:
        assert not (intra or cross or marks), f"{sf.tag} has records: give it its own glyph index range"
    else:
        assert len(records) <= OWN_RANGE, f"{sf.tag}: {len(records)} glyphs do not fit its range of {OWN_RANGE}"
    buf = io.BytesIO()
    font.save(buf)
    base = f"{file_name(style)}-{sf.tag}"
    files = {base + ".ttf": buf.getvalue()}
    cps = _codepoints(cmap)
    cps.update({k: v for k, v in _codepoints(core_cmap).items() if k not in cps})
    if hasattr(mod, "kerning"):
        files[base + ".kern.tsv"] = tsv_kerning(intra, cps).encode("ascii")
        files[base + ".crosskern.tsv"] = tsv_cross(cross, cps, sf.tag).encode("ascii")
    if marks:
        files[base + ".marks.tsv"] = tsv_marks(marks, cps).encode("ascii")
    summary = dict(glyphs=len(order), script_glyphs=len(records), codepoints=len(cmap), intra=len(intra),
                   cross=len(cross), marks=len(marks), composites=sum(r[4] is not None for r in records))
    return files, summary


def shipped_styles(names=None):
    return [st for key, styles in FAMILIES.items() if not names or key in names for st in styles]


def linebreak_files():
    import pw_font_kana as K
    return {LINEBREAK_LEADING: K.LINE_START_FORBIDDEN.encode("utf-8"),
            LINEBREAK_FOLLOWING: K.LINE_END_FORBIDDEN.encode("utf-8")}


def build_scripts(selectors=None, names=None, core=None, write=True):
    """Every selected fallback of every shipped style. Returns {file name: bytes}; written to Build unless write is
    False. `core` maps file_name(style) to the style's (order, cmap, data, pairs), built here when missing."""
    core = core if core is not None else {}
    fonts = select_scripts(selectors)
    out = {}
    for style in shipped_styles(names):
        key = file_name(style)
        if key not in core:
            core[key] = core_of(style)
        for sf in fonts:
            files, s = build_script_font(sf, style, core[key])
            out.update(files)
            if write:
                for fname, blob in files.items():
                    with open(os.path.join(OUT, fname), "wb") as f:
                        f.write(blob)
            print(f"{key}-{sf.tag}.ttf: {s['glyphs']} glyphs ({s['script_glyphs']} from index {sf.first_gid}), "
                  f"{s['codepoints']} code points, {len(files[key + '-' + sf.tag + '.ttf'])} bytes, "
                  f"{s['intra']} pairs, {s['cross']} cross pairs, {s['marks']} mark records")
    if any(sf.module == "pw_font_kana" for sf in fonts):
        out.update(linebreak_files())
        if write:
            for fname, blob in linebreak_files().items():
                with open(os.path.join(OUT, fname), "wb") as f:
                    f.write(blob)
    return out


def _sha(blob):
    return hashlib.sha256(blob).hexdigest()


def quiet_zero_timestamps():
    """Reading our fonts makes fontTools warn that created = modified = 0 is 'very low': that is intended."""
    import logging
    logging.getLogger("fontTools.ttLib.tables._h_e_a_d").setLevel(logging.ERROR)


def write_report():
    """SourceArt/Fonts/Build/scripts_report.txt from the files on disk: glyph counts, sizes and hashes."""
    from fontTools.ttLib import TTFont
    quiet_zero_timestamps()

    def read(fname):
        with open(os.path.join(OUT, fname), "rb") as f:
            return f.read()

    def rows_of(fname):
        p = os.path.join(OUT, fname)
        if not os.path.exists(p):
            return None
        with open(p, encoding="ascii") as f:
            return sum(1 for line in f if line.strip() and not line.startswith("#"))

    lines = ["Pocket Wilds fonts: the Latin core and the script fallbacks (pw_font_build.py --scripts)",
             "",
             "Units: font units, 1000 per em. Every file: hhea ascent 950, descent -250, line gap 0; OS/2 typo 950 / -250 / 0,",
             "win 1000 / 300; head created = modified = 0. Fallbacks: post format 3 (no glyph names), no Latin glyph.",
             "Glyph indices: core below 512; CyrGrk 512, Viet 768, Thai 1024 (own ranges: they carry pair or mark",
             "records); every other fallback from 1280. Placeholders (empty glyphs) fill the indices below a fallback's first.",
             "",
             "Core (unchanged by the fallbacks)",
             "file\tglyphs\tbytes\tsha256"]
    for style in shipped_styles():
        for ext in (".ttf", ".kern.tsv"):
            fname = file_name(style) + ext
            blob = read(fname)
            glyphs = TTFont(io.BytesIO(blob))["maxp"].numGlyphs if ext == ".ttf" else rows_of(fname)
            lines.append(f"{fname}\t{glyphs}{' pairs' if ext != '.ttf' else ''}\t{len(blob)}\t{_sha(blob)}")
    lines += ["", "Fallbacks",
              "file\tfirst index\tglyphs (script / total)\tcode points\tcomposites\tkern pairs\tcross pairs\t"
              "mark records\tfeatures\tink y min / max\tbytes\tsha256"]
    for style in shipped_styles():
        for sf in SCRIPT_FONTS:
            base = f"{file_name(style)}-{sf.tag}"
            if not os.path.exists(os.path.join(OUT, base + ".ttf")):
                lines.append(f"{base}.ttf\tnot built")
                continue
            blob = read(base + ".ttf")
            font = TTFont(io.BytesIO(blob))
            n = font["maxp"].numGlyphs
            comps = sum(1 for g in font.getGlyphOrder() if font["glyf"][g].isComposite())
            feats = sorted({fr.FeatureTag for t in ("GSUB", "GPOS") if t in font
                            for fr in font[t].table.FeatureList.FeatureRecord})
            kern, crossk, marks = rows_of(base + ".kern.tsv"), rows_of(base + ".crosskern.tsv"), rows_of(base + ".marks.tsv")
            head = font["head"]
            lines.append("\t".join(str(v) for v in [
                base + ".ttf", sf.first_gid, f"{n - sf.first_gid} / {n}", len(font.getBestCmap()), comps,
                "-" if kern is None else kern, "-" if crossk is None else crossk, "-" if marks is None else marks,
                " ".join(feats) or "-", f"{head.yMin} / {head.yMax}", len(blob), _sha(blob)]))
            for ext in (".kern.tsv", ".crosskern.tsv", ".marks.tsv"):
                if os.path.exists(os.path.join(OUT, base + ext)):
                    side = read(base + ext)
                    lines.append(f"  {base + ext}\t{len(side)}\t{_sha(side)}")
    lines += ["", "Languages: fallbacks to add after the core, in this order (Latin languages load the core only)"]
    for lang in LANGUAGE_ORDER:
        lines.append(f"{lang}\t" + " ".join(sf.tag for sf in SCRIPT_FONTS if lang in sf.languages))
    lines += ["", "Line breaking (TMP Settings), from pw_font_kana (JIS X 4051 kinsoku, Latin marks included)"]
    for fname in (LINEBREAK_LEADING, LINEBREAK_FOLLOWING):
        if os.path.exists(os.path.join(OUT, fname)):
            blob = read(fname)
            lines.append(f"{fname}\t{len(blob.decode('utf-8'))} characters\t{_sha(blob)}")
    lines.append("Korean: TMP Settings useModernHangulLineBreakingRules = true (break at spaces).")
    lines.append("Thai: words are separated by U+200B in the translations (the Thai font maps it, zero width).")
    with open(os.path.join(OUT, REPORT), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    return os.path.join(OUT, REPORT)


def check(selectors=None, names=None):
    """Builds the core and the fallbacks in memory and compares them with the files in Build. True when identical."""
    ok = True
    core = {}
    for style in shipped_styles(names):
        key = file_name(style)
        core[key] = core_of(style)
        order, cmap, data, pairs = core[key]
        buf = io.BytesIO()
        write_font(style, order, cmap, data, pairs, buf)
        ok &= _compare(key + ".ttf", buf.getvalue())
    for fname, blob in build_scripts(selectors, names, core, write=False).items():
        ok &= _compare(fname, blob)
    return ok


def _compare(fname, blob):
    p = os.path.join(OUT, fname)
    same = os.path.exists(p) and open(p, "rb").read() == blob
    print(f"{fname}: {'identical' if same else 'DIFFERENT'} ({_sha(blob)[:16]})")
    return same


def _selectors(args, flag):
    for a in args:
        if a == flag:
            return None
        if a.startswith(flag + "="):
            return [s for s in a.split("=", 1)[1].split(",") if s]
    return None


def main(args):
    if "--candidates" in args:
        args.remove("--candidates")
        build(args or None, CANDIDATES)
        return 0
    flags = [a for a in args if a.startswith("--")]
    names = [a for a in args if not a.startswith("--")] or None
    if not flags:
        build(names)
        return 0
    flag = flags[0].split("=", 1)[0]
    selectors = _selectors(flags, flag)
    if flag == "--check":
        return 0 if check(selectors, names) else 1
    if flag not in ("--scripts", "--only-scripts"):
        raise SystemExit(f"unknown option {flag}")
    core = {}
    if flag == "--scripts":
        build(names, keep=core)
    os.makedirs(OUT, exist_ok=True)
    build_scripts(selectors, names, core)
    print(write_report())
    import pw_font_scripts_proof
    print(pw_font_scripts_proof.main([]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
