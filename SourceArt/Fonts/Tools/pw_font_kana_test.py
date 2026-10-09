"""Test fonts, proofs and checks for the kana module (FJ).

    python pw_font_kana_test.py            # test fonts + proofs
    python pw_font_kana_test.py fonts      # test fonts only
    python pw_font_kana_test.py check DIR  # structure check against Yu Gothic, review sheets written to DIR (not the repo)

Test fonts: SourceArt/Fonts/Build/Test/PocketRound-<Weight>-Kana-Test.ttf, PocketPlush-Black-Kana-Test.ttf
(the shipped Latin from build_style, plus every kana glyph processed exactly like build_style processes Glyphs.make).
Proofs: SourceArt/Fonts/Proofs/KANA_*.png. The shipped fonts in SourceArt/Fonts/Build are never touched.
"""
import json
import math
import os
import shutil
import sys
import tempfile
import unicodedata

import numpy as np
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import pw_font_engine as E
import pw_font_kana as K
from pw_font_build import COPYRIGHT, FAMILIES, MANUFACTURER, UPM, VERSION, build_style, kerning, side_bearing

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.normpath(os.path.join(HERE, ".."))
TEST = os.path.join(FONTS, "Build", "Test")
PROOFS = os.path.join(FONTS, "Proofs")
YU_GOTHIC = r"C:\Windows\Fonts\YuGothM.ttc"   # verification only, never copied (see check())

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
NIGHT = (38, 31, 46)
MOON = (246, 238, 226)
DIM = (178, 164, 150)
ORANGE = (252, 172, 84)
PANEL = (255, 255, 252)


def styles():
    return [s for key in ("PocketRound", "PocketPlush") for s in FAMILIES[key]]


def test_path(style):
    return os.path.join(TEST, (style.family + "-" + style.weight_name).replace(" ", "") + "-Kana-Test.ttf")


# ---------------------------------------------------------------------------------------------------------------
# Build (mirrors pw_font_build.build_style for every glyph the module makes)
# ---------------------------------------------------------------------------------------------------------------

def process(style, key, made):
    """build_style's per-glyph steps on a Glyphs.make tuple: bounce/tilt, spacing, simplify."""
    shapes, lc, rc, upper, (x0, x1) = made
    contours = E.glyph_contours(shapes)
    if (style.bounce or style.tilt) and contours:
        bx0, by0, bx1, by1 = E.bounds(contours)
        seed = sum(ord(c) for c in key) * 7919 % 1000 / 1000.0
        dy = (seed - 0.5) * 2 * style.bounce
        ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
        if not key.isalpha():
            dy, ang = dy * 0.3, 0.0
        contours = E.transform(contours, 0, dy, ang, (bx0 + bx1) / 2, (by0 + by1) / 2)
    lsb = side_bearing(style, lc, upper)
    rsb = side_bearing(style, rc, upper)
    adv = lsb + (x1 - x0) + rsb
    contours = E.transform(contours, lsb - x0, 0)
    contours = [E.simplify(c) for c in contours]
    return contours, int(round(adv))


def build_kana_style(style):
    order, cmap, data = build_style(style)
    for ch in K.charset():
        name = K.glyph_name(ch)
        contours, adv = process(style, ch, K.make(ch, style))
        assert adv == K.advance(ch, style), (ch, adv)
        if name not in data:
            order.append(name)
        data[name] = (contours, adv, ch)
        cmap[ord(ch)] = name
    for name in K.extra_glyphs():
        contours, adv = process(style, K.EXTRA_BASE[name], K.make_extra(name, style))
        order.append(name)
        data[name] = (contours, adv, None)
    return order, cmap, data


def write_test_font(style, order, cmap, data, pairs, path, family_suffix=" Kana Test", features=True):
    """pw_font_build.write_font plus the module's languagesystems and locl."""
    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    glyphs, metrics = {}, {}
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
        glyphs[name] = pen.glyph()
        xmin = min((int(p[0]) for c in contours for p in c), default=0)
        metrics[name] = (adv, xmin)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=950, descent=-250)
    family = style.family + family_suffix
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
    fea = K.languagesystem_code()
    if pairs:
        fea += "feature kern {\n" + "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(pairs.items())) + "} kern;\n"
    if features:
        fea += K.features(style)
    addOpenTypeFeaturesFromString(font, fea)
    font.save(path)


def build_fonts():
    os.makedirs(TEST, exist_ok=True)
    out = []
    for style in styles():
        order, cmap, data = build_kana_style(style)
        pairs = kerning(order, cmap, data, style)
        path = test_path(style)
        write_test_font(style, order, cmap, data, pairs, path)
        out.append((path, len(order), len(pairs)))
        print(f"{path}: {len(order)} glyphs, {len(pairs)} kerning pairs, {os.path.getsize(path)} bytes")
    return out


def region_fonts(tmp):
    """Per-region copies (cmap overrides from region_cmap) for the regional proof; written to a temp folder."""
    out = {}
    style = FAMILIES["PocketRound"][0]
    order, cmap, data = build_kana_style(style)
    for region in ("ja", "zh-Hans", "zh-Hant"):
        cm = dict(cmap)
        cm.update(K.region_cmap(region))
        path = os.path.join(tmp, f"region-{region}.ttf")
        write_test_font(style, order, cm, data, {}, path, family_suffix=" Region " + region, features=False)
        out[region] = path
    return out


# ---------------------------------------------------------------------------------------------------------------
# Proofs
# ---------------------------------------------------------------------------------------------------------------

def font(path, size):
    return ImageFont.truetype(path, size)


LABEL = None


def label_font(size):
    return font(test_path(FAMILIES["PocketRound"][1]), size)


SENTENCES = [
    "あたらしい なかま！",
    "ごほうびを うけとる",
    "タマゴが かえったよ",
    "ショップ・コイン ×500",
    "レベル 12 に アップ！ ★3",
    "「モチ」を ブラッシング しよう。",
    "きょうの おねがい 2/3 、 のこり 5ふん",
    "ハムスターが ねむそうに あくびを したよ。",
    "Pocket Wilds へ ようこそ！ Ｎｅｗ ＃１",
]

SAMPLE_LATIN = "Pocket Wilds 2026 : New friend!"


def save(img, name):
    os.makedirs(PROOFS, exist_ok=True)
    path = os.path.join(PROOFS, name)
    img.save(path, optimize=True)
    return path


def grid_sheet(style):
    path = test_path(style)
    chars = K.charset()
    cols, cell, size = 20, 92, 64
    rows = math.ceil(len(chars) / cols)
    top = 110
    img = Image.new("RGB", (cols * cell + 60, top + rows * (cell + 18) + 40), CREAM)
    d = ImageDraw.Draw(img)
    d.text((30, 24), f"{style.family} {style.weight_name} · kana, CJK punctuation, full-width ({len(chars)})",
           font=label_font(34), fill=INK)
    d.text((30, 68), "Abc 0123 · あいう アイウ 、。「」 ＡＢＣ０１２", font=font(path, 30), fill=SOFT)
    f = font(path, size)
    small = label_font(13)
    for i, ch in enumerate(chars):
        r, c = divmod(i, cols)
        x, y = 30 + c * cell, top + r * (cell + 18)
        d.rectangle((x, y, x + cell - 6, y + cell - 6), outline=(236, 226, 210))
        # em box guides: left and right edges of the advance
        if ord(ch) in (0x3099, 0x309A):
            d.text((x + 10, y + 8), "あ", font=f, fill=(225, 214, 198))
            d.text((x + 10 + size, y + 8), ch, font=f, fill=INK)
        else:
            d.text((x + 10, y + 8), ch, font=f, fill=INK)
        d.text((x + 2, y + cell - 4), f"{ord(ch):04X}", font=small, fill=SOFT)
    return img


def sentences_sheet(dark):
    """Game sentences at 96, 48, 28 and 18 px, the four styles in two columns."""
    bg, fg, soft = (NIGHT, MOON, DIM) if dark else (CREAM, INK, SOFT)
    colw, W = 1180, 2400
    img = Image.new("RGB", (W, 3200), bg)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "Pocket Round · Pocket Plush · kana" + (" · fond sombre" if dark else " · fond clair"),
           font=label_font(40), fill=fg)
    bottom = 0
    for i, style in enumerate(styles()):
        path = test_path(style)
        x = 40 + (i % 2) * colw
        y = 110 + (i // 2) * 1520
        d.text((x, y), f"{style.family} {style.weight_name}", font=label_font(28), fill=soft)
        y += 44
        for px in (96, 48, 28, 18):
            f = font(path, px)
            lines = SENTENCES[:2] if px == 96 else SENTENCES[:5] if px == 48 else SENTENCES
            for line in lines:
                d.text((x, y), line, font=f, fill=fg)
                y += int(px * 1.45)
            y += 10
        bottom = max(bottom, y)
    return img.crop((0, 0, W, bottom + 20))


def styles_sheet():
    W = 2400
    img = Image.new("RGB", (W, 1500), CREAM)
    d = ImageDraw.Draw(img)
    d.text((40, 24), "Les 4 styles côte à côte, avec le latin du même style", font=label_font(38), fill=INK)
    colw = W // 4
    for i, style in enumerate(styles()):
        path = test_path(style)
        x = 30 + i * colw
        y = 100
        d.text((x, y), f"{style.weight_name}", font=label_font(30), fill=SOFT)
        y += 50
        for line, px in [("Hamster", 80), ("はむすたー", 80), ("ハムスター", 80), ("Coins 500", 64),
                         ("コイン500", 64), ("「なかま」", 64), ("ＡＢＣ１２３", 56), ("あたらしい", 56),
                         ("Shop ショップ", 48), ("ごほうび！", 48), ("Lv.12 レベル", 48)]:
            d.text((x, y), line, font=font(path, px), fill=INK)
            y += int(px * 1.28)
    return img.crop((0, 0, W, y + 30))


def ui_mock(dark=False):
    """Phone-sized mock: title in Plush, UI text in Round."""
    bg, fg, soft, panel = (NIGHT, MOON, DIM, (58, 49, 68)) if dark else (CREAM, INK, SOFT, PANEL)
    W, H = 1080, 1280
    img = Image.new("RGB", (W, H), bg)
    d = ImageDraw.Draw(img)
    plush = lambda s: font(test_path(FAMILIES["PocketPlush"][0]), s)
    reg = lambda s: font(test_path(FAMILIES["PocketRound"][0]), s)
    semi = lambda s: font(test_path(FAMILIES["PocketRound"][1]), s)
    bold = lambda s: font(test_path(FAMILIES["PocketRound"][2]), s)
    d.text((W / 2, 80), "ショップ", font=plush(88), fill=fg, anchor="mm")
    d.rounded_rectangle((60, 160, W - 60, 470), 46, fill=panel)
    d.text((110, 195), "そうげんパック", font=plush(60), fill=fg)
    d.text((110, 282), "ハムスター ＋ ジェム 500こ", font=semi(40), fill=soft)
    d.text((110, 340), "いちどだけの とくべつセット", font=reg(36), fill=soft)
    d.rounded_rectangle((W - 420, 380, W - 100, 450), 35, fill=ORANGE)
    d.text((W - 260, 415), "¥160", font=bold(42), fill=INK, anchor="mm")
    d.rounded_rectangle((60, 520, W - 60, 760), 46, fill=panel)
    d.text((110, 555), "おかえりなさい", font=semi(46), fill=soft)
    d.text((110, 620), "+24.8K", font=plush(96), fill=fg)
    d.rounded_rectangle((W - 420, 650, W - 100, 730), 40, fill=ORANGE)
    d.text((W - 260, 690), "うけとる", font=bold(44), fill=INK, anchor="mm")
    d.rounded_rectangle((60, 810, W - 60, 1080), 46, fill=panel)
    d.text((110, 845), "きょうの おねがい", font=plush(52), fill=fg)
    yy = 925
    for line in ["モチを ブラッシング・1/1", "タマゴを 3こ かえす・2/3", "かくれんぼで あそぶ・0/1"]:
        d.text((110, yy), line, font=reg(38), fill=fg)
        yy += 48
    d.rounded_rectangle((60, 1120, W - 60, 1230), 55, fill=(255, 236, 205) if not dark else (92, 70, 60))
    d.text((W / 2, 1175), "あたらしい なかま！ リスが きたよ", font=semi(44), fill=fg, anchor="mm")
    return img


def regional_sheet():
    tmp = tempfile.mkdtemp(prefix="pw_kana_region_")
    try:
        paths = region_fonts(tmp)
        W = 1800
        img = Image.new("RGB", (W, 760), CREAM)
        d = ImageDraw.Draw(img)
        d.text((40, 24), "Ponctuation régionale (locl) : japonais, chinois simplifié, chinois traditionnel",
               font=label_font(34), fill=INK)
        d.text((40, 66), "〇 tient la place des idéogrammes (module Han)", font=label_font(24), fill=SOFT)
        # 〇 stands in for the ideographs (drawn by the Han module, not in this test font).
        samples = {"ja": "なかま、ごほうび。「ＯＫ」！？：", "zh-Hans": "〇〇，〇〇。“〇”！？：；",
                   "zh-Hant": "〇〇，〇〇。「〇」！？、"}
        y = 120
        for region, path in paths.items():
            d.text((40, y + 30), region, font=label_font(30), fill=SOFT)
            f = font(path, 84)
            x = 220
            # Cells show the em boxes; ideographs are missing on purpose (Han agent), only the punctuation matters.
            for ch in samples[region]:
                d.rectangle((x, y, x + 84, y + 110), outline=(232, 220, 204))
                d.text((x, y + 8), ch, font=f, fill=INK)
                x += 92
            y += 200
        return img.crop((0, 0, W, y + 20))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def proofs():
    out = []
    for style in styles():
        out.append(save(grid_sheet(style), f"KANA_grid_{style.family.replace(' ', '')}-{style.weight_name}.png"))
    out.append(save(sentences_sheet(False), "KANA_sentences_light.png"))
    out.append(save(sentences_sheet(True), "KANA_sentences_dark.png"))
    out.append(save(styles_sheet(), "KANA_styles.png"))
    out.append(save(ui_mock(False), "KANA_ui_light.png"))
    out.append(save(ui_mock(True), "KANA_ui_dark.png"))
    out.append(save(regional_sheet(), "KANA_regional.png"))
    for p in out:
        print(p)
    return out


# ---------------------------------------------------------------------------------------------------------------
# Structure check against Yu Gothic (verification only)
# ---------------------------------------------------------------------------------------------------------------
# Yu Gothic is rendered to a raster, blurred and compared with our raster: nothing is traced, copied or stored in
# the repository. The review sheets go to a folder outside the repository.

CHECK_SIZE = 96


def _mark_kind(img):
    """'dakuten' (two separate dots) or 'handakuten' (a ring) from the top-right corner of a sharp render."""
    a = np.asarray(img) >= 128
    size = CHECK_SIZE
    o = size // 2   # render origin; Pillow puts the ascender (950) at the top
    row = lambda y: int(o + (950 - y) * size / 1000.0)
    col = lambda x: int(o + x * size / 1000.0)
    # The mark band: centers at K.MARK_CY, right half of the em.
    region = a[row(K.MARK_CY + 125): row(K.MARK_CY - 125), col(600): col(1000) + 2]
    sub = Image.fromarray((region * 255).astype(np.uint8))
    n = _components(sub)
    holes = _components(Image.fromarray(((~region) * 255).astype(np.uint8))) - 1
    if n >= 2 and holes == 0:
        return "dakuten"
    if holes >= 1:
        return "handakuten"
    return "?"
NORM = 48


def _render(path, ch, size=CHECK_SIZE, index=0):
    f = ImageFont.truetype(path, size, index=index)
    img = Image.new("L", (size * 2, size * 2), 0)
    ImageDraw.Draw(img).text((size // 2, size // 2), ch, font=f, fill=255)
    return img


def _norm(img, blur=3.0, mask_corner=False):
    box = img.getbbox()
    if not box:
        return None, None
    crop = img.crop(box)
    if mask_corner:
        # Blank the top-right corner where dakuten and handakuten sit (same share of the box for both fonts).
        w, h = crop.size
        ImageDraw.Draw(crop).rectangle((int(w * 0.70), 0, w, int(h * 0.30)), fill=0)
        box2 = crop.getbbox()
        if box2:
            crop = crop.crop(box2)
    w, h = crop.size
    s = (NORM - 6) / max(w, h)
    crop = crop.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    canvas = Image.new("L", (NORM, NORM), 0)
    canvas.paste(crop, ((NORM - crop.size[0]) // 2, (NORM - crop.size[1]) // 2))
    a = np.asarray(canvas.filter(ImageFilter.GaussianBlur(blur)), dtype=float)
    a -= a.mean()
    n = np.linalg.norm(a)
    return (a / n if n else a), box


def _components(img, thr=128):
    """Connected ink components (4-neighbour) of a binarized render."""
    a = np.asarray(img) >= thr
    seen = np.zeros_like(a, dtype=bool)
    count = 0
    H, W = a.shape
    for y0 in range(H):
        for x0 in range(W):
            if a[y0, x0] and not seen[y0, x0]:
                count += 1
                stack = [(y0, x0)]
                seen[y0, x0] = True
                while stack:
                    y, x = stack.pop()
                    for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                        if 0 <= yy < H and 0 <= xx < W and a[yy, xx] and not seen[yy, xx]:
                            seen[yy, xx] = True
                            stack.append((yy, xx))
    return count


def _kind(c):
    if K._voiced(c) or c in "ゞヾ":
        return "voiced"
    if K._small_base(c):
        return "small"
    return "base"


_LETTERS = {"hiragana": [c for c in K.HIRAGANA if unicodedata.category(c) == "Lo"],
            "katakana": [c for c in K.KATAKANA + K.KATAKANA_EXT if unicodedata.category(c) == "Lo"]}
# Identification runs inside each group: small kana against small kana, voiced against voiced (the blur and the
# size normalization make ぁ and あ, or が and か, the same shape on purpose).
CHECK_GROUPS = {f"{script} {kind}": [c for c in chars if _kind(c) == kind]
                for script, chars in _LETTERS.items() for kind in ("base", "voiced", "small")}
CHECK_GROUPS["symbols"] = list("、。〃々〆〇〈〉《》「」『』【】〒〔〕〖〗〜・ー")


def check(outdir, style=None):
    """For every character: is our glyph closest to the right Yu Gothic character among its group?"""
    os.makedirs(outdir, exist_ok=True)
    style = style or FAMILIES["PocketRound"][0]
    ours = test_path(style)
    tag = (style.family + "-" + style.weight_name).replace(" ", "")
    report = {}
    for group, chars in CHECK_GROUPS.items():
        ref = {}
        for c in chars:
            ref[c] = _norm(_render(YU_GOTHIC, c))[0]
        mine = {c: _norm(_render(ours, c))[0] for c in chars}
        rows = []
        for c in chars:
            scores = sorted(((float((mine[c] * ref[r]).sum()), r) for r in chars if ref[r] is not None), reverse=True)
            own = float((mine[c] * ref[c]).sum())
            rank = [r for _, r in scores].index(c) + 1
            comp_ours = _components(_render(ours, c))
            comp_ref = _components(_render(YU_GOTHIC, c))
            row = {"ch": c, "cp": f"{ord(c):04X}", "own": round(own, 3), "rank": rank,
                   "best": scores[0][1], "best_score": round(scores[0][0], 3),
                   "components": [comp_ours, comp_ref]}
            if group.endswith("voiced"):
                # The blur hides dots versus ring: the letter must win among letters with the same mark kind... or be
                # the sibling with the same base; the mark itself is checked on the sharp render (dots vs ring).
                base = (K._voiced(c) or ("ゝ" if c == "ゞ" else "ヽ", "dakuten"))[0]
                # Letter under the mark: mask the mark corner of both renders, then find the closest plain letter.
                pool = CHECK_GROUPS[group.replace("voiced", "base")]
                mine_m = _norm(_render(ours, c), mask_corner=True)[0]
                ref_m = {r: _norm(_render(YU_GOTHIC, r), mask_corner=True)[0] for r in pool}
                best = max(pool, key=lambda r: float((mine_m * ref_m[r]).sum()))
                row["base_best"] = best
                row["base_ok"] = best == base
                row["mark_ok"] = _mark_kind(_render(ours, c)) == (K._voiced(c) or ("", "dakuten"))[1]
            rows.append(row)
        report[group] = rows
        sheet(group, chars, ours, os.path.join(outdir, f"check_{tag}_{group.replace(' ', '_')}.png"), rows)
    with open(os.path.join(outdir, f"check_{tag}.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)
    for group, rows in report.items():
        ok = sum(1 for r in rows if r["rank"] == 1)
        print(f"{group}: {ok}/{len(rows)} identified first; mean own score {np.mean([r['own'] for r in rows]):.3f}")
        if group.endswith("voiced"):
            bad = [(r["ch"], r["base_best"]) for r in rows if not r["base_ok"]]
            print(f"    base letter right (mark corner masked): {sum(r['base_ok'] for r in rows)}/{len(rows)} {bad}, "
                  f"mark kind right (2 dots / ring): {sum(r['mark_ok'] for r in rows)}/{len(rows)}")
        for r in rows:
            if r["rank"] != 1 or r["own"] < 0.55 or r["components"][0] != r["components"][1]:
                print("   ", r["ch"], r["cp"], "own", r["own"], "rank", r["rank"], "best", r["best"], r["best_score"],
                      "components ours/ref", r["components"])
    return report


def sheet(group, chars, ours, path, rows=None):
    """Review sheet: our glyph (left) and the reference (right, light) for each character."""
    cols, cell = 10, 150
    rows_n = math.ceil(len(chars) / cols)
    img = Image.new("RGB", (cols * cell + 20, rows_n * cell + 20), (255, 255, 255))
    d = ImageDraw.Draw(img)
    fo = ImageFont.truetype(ours, 64)
    fr = ImageFont.truetype(YU_GOTHIC, 64)
    sm = ImageFont.truetype(ours, 14)
    info = {r["ch"]: r for r in rows or []}
    for i, c in enumerate(chars):
        r, k = divmod(i, cols)
        x, y = 10 + k * cell, 10 + r * cell
        d.text((x + 4, y + 10), c, font=fo, fill=(30, 30, 30))
        d.text((x + 74, y + 10), c, font=fr, fill=(150, 170, 220))
        if c in info:
            t = info[c]
            col = (40, 120, 40) if t["rank"] == 1 else (200, 40, 40)
            d.text((x + 4, y + 100), f"{t['cp']} {t['own']:.2f} #{t['rank']}", font=sm, fill=col)
    img.save(path)


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[:1] == ["check"]:
        out = args[1] if len(args) > 1 else tempfile.mkdtemp(prefix="pw_kana_check_")
        for st in styles():
            if len(args) < 3 or args[2] in (st.family + "-" + st.weight_name).replace(" ", ""):
                print(f"== {st.family} {st.weight_name}")
                check(out, st)
    elif args[:1] == ["fonts"]:
        build_fonts()
    else:
        build_fonts()
        proofs()
