"""Test fonts and proofs for the Han ideographs (FH). Not shipped: the merge hook in pw_font_build.py comes later.

    python pw_font_han_test.py            # test fonts for every region and style, then the proofs
    python pw_font_han_test.py fonts      # fonts only
    python pw_font_han_test.py proofs     # proof fonts (Latin + kana + Han, local) and proof sheets
    python pw_font_han_test.py sheets     # proof sheets only, reusing the proof fonts

Fonts: SourceArt/Fonts/Build/Test/PocketRound-<Weight>-Han<JP|SC|TC>-Test.ttf and
PocketPlush-Black-Han<JP|SC|TC>-Test.ttf: the Latin of the style (exactly as build_style makes it) plus the Han
glyphs of one region, written by pw_font_build.write_font. Each language loads its own region's font.
Proofs: SourceArt/Fonts/Proofs/HAN_*.png (rendered through FreeType by Pillow, like Unity's TextCore).
"""
import os
import sys

from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

import pw_font_engine as E
import pw_font_han as H
from pw_font_build import FAMILIES, build_style, file_name, glyph_name, kerning, side_bearing, write_font

HERE = os.path.dirname(os.path.abspath(__file__))
TEST = os.path.normpath(os.path.join(HERE, "..", "Build", "Test"))
PROOFS = os.path.normpath(os.path.join(HERE, "..", "Proofs"))
STYLES = FAMILIES["PocketRound"] + FAMILIES["PocketPlush"]

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
NIGHT = (44, 36, 52)
MOON = (250, 240, 222)
ORANGE = (252, 172, 84)


def test_path(style, region):
    return os.path.join(TEST, f"{file_name(style)}-Han{region}-Test.ttf")


def build_test_font(style, region):
    """Latin of the style plus the Han glyphs of one region, through the same spacing code as build_style."""
    order, cmap, data = build_style(style)
    for ch in H.charset(region):
        shapes, lc, rc, upper, (x0, x1) = H.make(ch, style, region)
        contours = E.glyph_contours(shapes)
        lsb = side_bearing(style, lc, upper)
        rsb = side_bearing(style, rc, upper)
        adv = lsb + (x1 - x0) + rsb
        contours = E.transform(contours, lsb - x0, 0)
        contours = [E.simplify(c) for c in contours]
        name = glyph_name(ch)
        data[name] = (contours, int(round(adv)), ch)
        order.append(name)
        cmap[ord(ch)] = name
    pairs = kerning(order, cmap, data, style)
    os.makedirs(TEST, exist_ok=True)
    path = test_path(style, region)
    write_font(style, order, cmap, data, pairs, path)
    # a test family name, so a test font never passes for the shipped one
    font = TTFont(path, recalcTimestamp=False)
    tag = f"Han{region} Test"
    names = font["name"]
    for rec in list(names.names):
        if rec.nameID in (1, 3, 4, 6, 16):
            s = rec.toUnicode()
            if rec.nameID == 6:
                s = s + "-Han" + region + "Test"
            elif rec.nameID == 3:
                s = s + "-Han" + region
            else:
                s = s.replace(style.family, style.family + " " + tag)
            rec.string = s
    font.save(path)
    return path, len(order)


def build_fonts():
    out = []
    for region in H.REGIONS:
        for style in STYLES:
            path, n = build_test_font(style, region)
            out.append((path, n, os.path.getsize(path)))
            print(f"{os.path.basename(path)}: {n} glyphs, {os.path.getsize(path) // 1024} KB")
    return out


# ------------------------------------------------------------------------------------------------ proofs

SENTENCES_KANA = {
    "JP": [
        ("新しい仲間！", "UI"),
        ("報酬を受け取る · 金貨 1,200", "reward"),
        ("卵がかえった！", "egg"),
        ("毎日ボーナス · 宝箱を開ける", "daily"),
        ("ウサギは花の近くで眠っている。", "tutorial"),
        ("おめでとう！星を三つ見つけた", "reward"),
        ("設定  音楽  言語  確認  戻る", "settings"),
        ("練習 · 挑戦成功", "activity"),
    ],
    "SC": [
        ("新朋友！", "UI"),
        ("领取奖励 · 宝石 1,200", "reward"),
        ("蛋孵化了！", "egg"),
        ("每日奖励 · 开始游戏", "daily"),
        ("小兔子在花园里玩。", "tutorial"),
        ("恭喜！你找到了星星", "reward"),
        ("设置  音乐  语言  确认  取消", "settings"),
        ("练习 · 挑战成功", "activity"),
    ],
    "TC": [
        ("新朋友！", "UI"),
        ("領取獎勵 · 寶石 1,200", "reward"),
        ("蛋孵化了！", "egg"),
        ("每日獎勵 · 開始遊戲", "daily"),
        ("小兔子在花園裡玩。", "tutorial"),
        ("恭喜！你找到了星星", "reward"),
        ("設定  音樂  語言  確認  取消", "settings"),
        ("練習 · 挑戰成功", "activity"),
    ],
}
SENTENCES = {
    "JP": [
        ("新種発見!", "UI"),
        ("報酬受取 · 金貨 1,200", "reward"),
        ("毎日 三回 無料", "shop"),
        ("卵 孵化 完了", "egg"),
        ("図鑑 · 動物 48 / 120", "collection"),
        ("設定  音楽  言語  確認", "settings"),
        ("春 夏 秋 冬 · 朝 昼 夜", "seasons"),
        ("森 草原 池 川 · 宝箱", "world"),
    ],
    "SC": [
        ("新朋友!", "UI"),
        ("领取奖励 · 宝石 1,200", "reward"),
        ("蛋孵化了", "egg"),
        ("每日奖励 · 开始游戏", "daily"),
        ("小兔子在花园里玩", "tutorial"),
        ("恭喜!你找到了星星", "reward"),
        ("设置  音乐  语言  确认  取消", "settings"),
        ("练习 · 挑战成功", "activity"),
    ],
    "TC": [
        ("新朋友!", "UI"),
        ("領取獎勵 · 寶石 1,200", "reward"),
        ("蛋孵化了", "egg"),
        ("每日獎勵 · 開始遊戲", "daily"),
        ("小兔子在花園裡玩", "tutorial"),
        ("恭喜!你找到了星星", "reward"),
        ("設定  音樂  語言  確認  取消", "settings"),
        ("練習 · 挑戰成功", "activity"),
    ],
}
LATIN = {"JP": "New friend! · Lv.12", "SC": "New friend! · Lv.12", "TC": "New friend! · Lv.12"}


PROOF_FONTS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "TestResults", "Fonts", "Han"))
KANA_REGION = {"JP": None, "SC": "zh-Hans", "TC": "zh-Hant"}


def _kana_module():
    try:
        import pw_font_kana
        return pw_font_kana
    except Exception:
        return None


def proof_font_path(style, region):
    return os.path.join(PROOF_FONTS, f"proof_{file_name(style)}-Han{region}.ttf")


def build_proof_font(style, region):
    """Proofs only (local, TestResults): Latin + the kana module of FJ (kana and CJK punctuation, with the Chinese
    punctuation forms for SC and TC) + our Han glyphs, so the sample sentences read as real text."""
    kana = _kana_module()
    order, cmap, data = build_style(style)

    def add(name, ch, tup):
        shapes, lc, rc, upper, (x0, x1) = tup
        contours = E.glyph_contours(shapes)
        lsb = side_bearing(style, lc, upper)
        rsb = side_bearing(style, rc, upper)
        adv = lsb + (x1 - x0) + rsb
        contours = [E.simplify(c) for c in E.transform(contours, lsb - x0, 0)]
        data[name] = (contours, int(round(adv)), ch)
        if name not in order:
            order.append(name)
    if kana is not None:
        for ch in kana.charset():
            if ord(ch) in cmap:
                continue
            name = glyph_name(ch)
            add(name, ch, kana.make(ch, style))
            cmap[ord(ch)] = name
        over = kana.region_cmap(KANA_REGION[region]) if KANA_REGION[region] else {}
        for cp, gname in over.items():
            add(gname, chr(cp), kana.make_extra(gname, style))
            cmap[cp] = gname
    for ch in H.charset(region):
        name = glyph_name(ch)
        add(name, ch, H.make(ch, style, region))
        cmap[ord(ch)] = name
    os.makedirs(PROOF_FONTS, exist_ok=True)
    path = proof_font_path(style, region)
    write_font(style, order, cmap, data, {}, path)
    return path


def _font(style, region, size):
    path = proof_font_path(style, region)
    if not os.path.exists(path):
        path = test_path(style, region)
    return ImageFont.truetype(path, size)


def _covered(text, region):
    cs = set(H.charset(region))
    missing = [c for c in text if "一" <= c <= "鿿" and c not in cs]
    if missing:
        print("not drawn yet, left out of the proof:", "".join(missing))
    return "".join(c for c in text if not ("一" <= c <= "鿿") or c in cs)


def proof_grid(region, style, cols=24, px=56):
    chars = H.charset(region)
    rows = (len(chars) + cols - 1) // cols
    pad = 24
    W = cols * (px + 8) + 2 * pad
    Hh = rows * (px + 14) + 2 * pad + 60
    img = Image.new("RGB", (W, Hh), CREAM)
    d = ImageDraw.Draw(img)
    f = _font(style, region, px)
    lab = _font(style, region, 22)
    d.text((pad, 16), f"Pocket {style.family.split()[-1]} {style.weight_name} · Han {region} · {len(chars)} glyphs", font=lab, fill=SOFT)
    for i, ch in enumerate(chars):
        x = pad + (i % cols) * (px + 8)
        y = pad + 50 + (i // cols) * (px + 14)
        d.text((x, y), ch, font=f, fill=INK)
    out = os.path.join(PROOFS, f"HAN_{region}_grid_{file_name(style)}.png")
    img.save(out)
    return out


def proof_sentences(region, dark=False):
    """Game lines at 96, 48, 28 and 18 px in every style, with the Latin of the same style."""
    bg, fg, sub = (NIGHT, MOON, (190, 176, 160)) if dark else (CREAM, INK, SOFT)
    W = 1800
    lines = SENTENCES_KANA[region] if _kana_module() else SENTENCES[region]
    img = Image.new("RGB", (W, 4400), bg)
    d = ImageDraw.Draw(img)
    y = 30
    lab = _font(STYLES[0], region, 24)
    d.text((40, y), f"Han {region} · Pocket Round Regular / SemiBold / Bold · Pocket Plush Black", font=lab, fill=sub)
    y += 50
    for style in STYLES:
        big = _font(style, region, 96)
        d.text((40, y), _covered(lines[0][0], region) + "  " + LATIN[region], font=big, fill=fg)
        y += 130
    for size in (48, 28, 18):
        y += 20
        d.text((40, y), f"{size} px", font=lab, fill=sub)
        y += 36
        for style in STYLES:
            f = _font(style, region, size)
            for text, _ in lines[1:5] if size == 48 else lines[1:]:
                d.text((40, y), _covered(text, region), font=f, fill=fg)
                y += int(size * 1.45)
            y += 8
    img = img.crop((0, 0, W, min(y + 30, img.height)))
    out = os.path.join(PROOFS, f"HAN_{region}_sentences_{'dark' if dark else 'light'}.png")
    img.save(out)
    return out


def proof_styles(region, sample="新友卵動物花森星光宝箱報酬"):
    """The four styles side by side, each with its own Latin, at 120 px."""
    sample = "".join(c for c in sample if c in set(H.charset(region))) or H.charset(region)[:12]
    img = Image.new("RGB", (1900, 4 * 170 + 60), CREAM)
    d = ImageDraw.Draw(img)
    y = 30
    for style in STYLES:
        f = _font(style, region, 120)
        d.text((40, y), "Ag " + sample[:9], font=f, fill=INK)
        y += 170
    out = os.path.join(PROOFS, f"HAN_{region}_styles.png")
    img.save(out)
    return out


def proof_regions():
    """The shared code points whose glyph differs by region, side by side (JP, SC, TC)."""
    d0 = H.data()
    diff = []
    for ch in sorted(set(H.charset("JP")) & set(H.charset("SC")) & set(H.charset("TC"))):
        sigs = []
        for r in H.REGIONS:
            g = H.build_glyph(ch, STYLES[0], r)
            sigs.append(tuple((s.kind, tuple((round(p[0]), round(p[1])) for p in s.pts)) for s in g.strokes))
        if len(set(sigs)) > 1:
            diff.append(ch)
    px = 110
    cols = 3
    img = Image.new("RGB", (60 + cols * (px + 30) * 1 + 200, 80 + len(diff) * (px + 16)), CREAM)
    dr = ImageDraw.Draw(img)
    lab = _font(STYLES[0], "JP", 22)
    dr.text((20, 20), "JP / SC / TC", font=lab, fill=SOFT)
    for i, ch in enumerate(diff):
        y = 70 + i * (px + 16)
        for j, r in enumerate(H.REGIONS):
            f = _font(STYLES[0], r, px)
            dr.text((40 + j * (px + 30), y), ch, font=f, fill=INK)
        dr.text((40 + 3 * (px + 30), y + px // 3), "U+%04X" % ord(ch), font=lab, fill=SOFT)
    out = os.path.join(PROOFS, "HAN_regional_forms.png")
    img.save(out)
    return out, diff


def build_proofs(reuse=False):
    os.makedirs(PROOFS, exist_ok=True)
    for region in H.REGIONS:
        for style in STYLES:
            if not (reuse and os.path.exists(proof_font_path(style, region))):
                build_proof_font(style, region)
    outs = []
    for region in H.REGIONS:
        outs.append(proof_grid(region, STYLES[0]))
        outs.append(proof_grid(region, STYLES[3], cols=24, px=56))
        outs.append(proof_sentences(region, dark=False))
        outs.append(proof_sentences(region, dark=True))
        outs.append(proof_styles(region))
    o, diff = proof_regions()
    outs.append(o)
    for o in outs:
        print(o)
    return outs


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("all", "fonts"):
        build_fonts()
    if what in ("all", "proofs"):
        build_proofs()
    if what == "sheets":            # proof images only, from the proof fonts already built
        build_proofs(reuse=True)
