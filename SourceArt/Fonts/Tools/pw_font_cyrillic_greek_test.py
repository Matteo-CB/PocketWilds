"""Test fonts and proofs for the Cyrillic and Greek module (pw_font_cyrillic_greek).

    python pw_font_cyrillic_greek_test.py           # test fonts (Build/Test) and proofs (Proofs/CYRGRK_*.png)
    python pw_font_cyrillic_greek_test.py --fonts   # test fonts only
    python pw_font_cyrillic_greek_test.py --check   # builds twice in memory and compares the bytes

The shipped fonts in SourceArt/Fonts/Build are never written: test fonts go to Build/Test with their own family name.
The helpers here (add_module, build_test, proof drawing) are shared with pw_font_vietnamese_test.py.
"""
import dataclasses
import hashlib
import io
import os
import sys

os.environ.setdefault("SOURCE_DATE_EPOCH", "0")  # fixed head timestamps: identical bytes on every run

from PIL import Image, ImageDraw, ImageFont

import pw_font_build as B
import pw_font_engine as E

HERE = os.path.dirname(os.path.abspath(__file__))
BUILD_TEST = os.path.normpath(os.path.join(HERE, "..", "Build", "Test"))
PROOFS = os.path.normpath(os.path.join(HERE, "..", "Proofs"))

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
DARK = (40, 29, 21)
DARK_SOFT = (200, 175, 150)
LINE = (236, 224, 204)

STYLES = [(fam, st) for fam in ("PocketRound", "PocketPlush") for st in B.FAMILIES[fam]]


# ------------------------------------------------------------------------------------------- build

def add_module(style, order, cmap, data, module):
    """Adds a script module's glyphs exactly as build_style processes the Latin ones."""
    added = 0
    for ch in module.charset():
        if ord(ch) in cmap:
            continue
        shapes, lc, rc, upper, (x0, x1) = module.make(ch, style)
        contours = E.glyph_contours(shapes)
        if style.bounce or style.tilt:
            bx0, by0, bx1, by1 = E.bounds(contours)
            seed = sum(ord(c) for c in ch) * 7919 % 1000 / 1000.0
            dy = (seed - 0.5) * 2 * style.bounce
            ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
            if not ch.isalpha():
                dy, ang = dy * 0.3, 0.0
            contours = E.transform(contours, 0, dy, ang, (bx0 + bx1) / 2, (by0 + by1) / 2)
        lsb = B.side_bearing(style, lc, upper)
        rsb = B.side_bearing(style, rc, upper)
        adv = lsb + (x1 - x0) + rsb
        contours = E.transform(contours, lsb - x0, 0)
        contours = [E.simplify(c) for c in contours]
        name = B.glyph_name(ch)
        data[name] = (contours, int(round(adv)), ch)
        order.append(name)
        cmap[ord(ch)] = name
        added += 1
    return added


def build_test(style, modules, tag, out_dir=BUILD_TEST, write=True):
    """Latin plus the modules, kerning of both, written as <File>-<tag>-Test.ttf. Returns (path, glyphs, pairs, bytes)."""
    order, cmap, data = B.build_style(style)
    for m in modules:
        add_module(style, order, cmap, data, m)
    pairs = B.kerning(order, cmap, data, style)
    for m in modules:
        pairs.update(m.kerning(cmap, data, style, pairs))
    test_style = dataclasses.replace(style, family=f"{style.family} {tag} Test")
    path = os.path.join(out_dir, f"{B.file_name(style)}-{tag}-Test.ttf")
    if write:
        os.makedirs(out_dir, exist_ok=True)
        B.write_font(test_style, order, cmap, data, pairs, path)
        B.write_kerning(data, pairs, path)
        with open(path, "rb") as f:
            blob = f.read()
    else:
        tmp = io.BytesIO()
        B.write_font(test_style, order, cmap, data, pairs, tmp)
        blob = tmp.getvalue()
    return path, len(order), len(pairs), blob


def test_path(style, tag):
    return os.path.join(BUILD_TEST, f"{B.file_name(style)}-{tag}-Test.ttf")


def latin_path(style):
    return os.path.join(HERE, "..", "Build", B.file_name(style) + ".ttf")


def label(style):
    return f"{style.family} {style.weight_name}"


# ------------------------------------------------------------------------------------------- proofs

def font(path, size):
    return ImageFont.truetype(path, size)


def grid_sheet(style, tag, chars, title, cols=16, cell=112):
    rows = (len(chars) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell, rows * cell + 100), CREAM)
    d = ImageDraw.Draw(img)
    p = test_path(style, tag)
    d.text((20, 20), f"{title} · {label(style)} · {len(chars)}", font=font(p, 44), fill=INK)
    big, lab = font(p, 64), font(latin_path(B.FAMILIES["PocketRound"][0]), 14)
    for i, ch in enumerate(chars):
        x, y = (i % cols) * cell, (i // cols) * cell + 100
        d.rectangle((x, y, x + cell - 1, y + cell - 1), outline=LINE)
        d.line((x + 6, y + 82, x + cell - 6, y + 82), fill=(244, 236, 222))
        d.text((x + cell / 2, y + 82), ch, font=big, fill=INK, anchor="ms")
        d.text((x + 4, y + 2), "%04X" % ord(ch), font=lab, fill=(170, 140, 110))
    return img


def sentences_sheet(tag, blocks, dark, sizes=(96, 48, 28, 18), style=None, width=2400):
    """blocks: [(language name, [lines])]. Every line at every size, in Pocket Round Regular (or the given style)."""
    style = style or B.FAMILIES["PocketRound"][0]
    bg, ink, soft = (DARK, CREAM, DARK_SOFT) if dark else (CREAM, INK, SOFT)
    p = test_path(style, tag)
    heights = 110
    for _, lines in blocks:
        heights += 50 + sum(int(s * 1.42) for s in sizes) * len(lines) + 20
    img = Image.new("RGB", (width, heights), bg)
    d = ImageDraw.Draw(img)
    d.text((40, 30), f"{label(style)} · {'fond sombre' if dark else 'fond clair'} · {', '.join(str(s) for s in sizes)} px",
           font=font(p, 40), fill=ink)
    y = 110
    for name, lines in blocks:
        d.text((40, y), name, font=font(p, 30), fill=soft)
        y += 50
        for line in lines:
            for s in sizes:
                d.text((40, y), line, font=font(p, s), fill=ink)
                y += int(s * 1.42)
        y += 20
    return img


def styles_sheet(tag, latin_line, script_lines, width=2600):
    """The four styles side by side: the Latin line in the shipped font, then the script lines in the test font."""
    rows = []
    for _, st in STYLES:
        rows.append(st)
    size = 64
    h = 100 + len(rows) * (60 + size * 1.5 * (1 + len(script_lines))) + 40
    img = Image.new("RGB", (width, int(h)), CREAM)
    d = ImageDraw.Draw(img)
    p0 = test_path(B.FAMILIES["PocketRound"][0], tag)
    d.text((40, 30), "Les quatre styles · latin livré puis la nouvelle écriture", font=font(p0, 40), fill=INK)
    y = 100
    for st in rows:
        d.text((40, y), label(st), font=font(p0, 28), fill=SOFT)
        y += 50
        d.text((40, y), latin_line, font=font(latin_path(st), size), fill=INK)
        y += int(size * 1.5)
        for line in script_lines:
            d.text((40, y), line, font=font(test_path(st, tag), size), fill=INK)
            y += int(size * 1.5)
        y += 10
    return img


def metrics_sheet(tag, chars, title, size=170):
    """Glyphs of every style over the vertical metrics: baseline, x-height, cap height, ascender 950, win ascent 1000,
    descender -250. Shows that marks stay inside the line."""
    k = size / 1000.0
    cols = len(chars)
    W = int(260 + cols * size * 0.98)
    row = int(size * 1.55)
    img = Image.new("RGB", (W, 90 + row * len(STYLES)), CREAM)
    d = ImageDraw.Draw(img)
    p0 = test_path(B.FAMILIES["PocketRound"][0], tag)
    d.text((20, 20), title, font=font(p0, 34), fill=INK)
    for i, (_, st) in enumerate(STYLES):
        base = 90 + i * row + int(size * 1.08)
        for y, col, name in ((0, (200, 170, 140), "0"), (st.xh, (225, 205, 180), "x"), (st.cap, (225, 205, 180), "C"),
                             (950, (120, 170, 220), "950"), (1000, (220, 120, 110), "1000"), (-250, (120, 170, 220), "-250")):
            yy = base - y * k
            d.line((210, yy, W, yy), fill=col)
            d.text((170, yy - 8), name, font=font(p0, 13), fill=col)
        d.text((20, base - size * 0.5), label(st), font=font(p0, 20), fill=SOFT)
        f = font(test_path(st, tag), size)
        asc = f.getmetrics()[0]
        x = 230
        for ch in chars:
            d.text((x, base - asc), ch, font=f, fill=INK)
            x += int(f.getlength(ch)) + int(size * 0.12)
    return img


def save(img, name):
    os.makedirs(PROOFS, exist_ok=True)
    path = os.path.join(PROOFS, name)
    img.save(path, optimize=True)
    return path


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


# ------------------------------------------------------------------------------------------- this module

TAG = "CyrGrk"

LATIN_LINE = "Nouvelle espèce ! Récolter · Hatch 3 eggs"

RUSSIAN = ["Новый друг!", "Собрать награду", "Нажми на яйцо, чтобы оно вылупилось.",
           "Награда: 500 самоцветов и редкий значок № 7", "Съешь же ещё этих мягких французских булок да выпей чаю."]
UKRAINIAN = ["Новий друг!", "Зібрати нагороду", "Їжачок знайшов ґудзик біля ставка.",
             "Чуєш їх, доцю, га? Кумедна ж ти, прощайся без ґольфів!"]
OTHERS = ["Беларуская: Новы сябар! Збяры ўзнагароду", "Српски: Нови пријатељ! Љубазни фењерџија чађавог лица хоће да ми покаже штос.",
          "Македонски: Нов пријател! Ѕвезда, ѓердан, ќебе, ќерка", "Български: Нов приятел! Събери наградата"]
GREEK = ["Νέος φίλος!", "Πάρε την ανταμοιβή", "Πάτησε το αυγό για να εκκολαφθεί.",
         "Κέρδισες 500 πετράδια! Ο σκαντζόχοιρος κοιμάται δίπλα στα λουλούδια.",
         "Ζωηρή ψαρόγατα βγήκε ξαφνικά στο θερινό λιβάδι; Ένα καΐκι, αϋπνία, ΠΡΟΪΟΝ · ΆΈΉΊΌΎΏ"]
CAPS = ["НОВЫЙ ДРУГ! СОБРАТЬ НАГРАДУ · ЁЖИК", "ΝΕΟΣ ΦΙΛΟΣ! ΠΑΡΕ ΤΗΝ ΑΝΤΑΜΟΙΒΗ"]


def main(args):
    import pw_font_cyrillic_greek as M
    if "--check" in args:
        ok = True
        for _, st in STYLES:
            a = build_test(st, [M], TAG, write=False)[3]
            b = build_test(st, [M], TAG, write=False)[3]
            same = a == b
            ok &= same
            print(f"{label(st)}: {'identical' if same else 'DIFFERENT'} ({len(a)} bytes)")
        return 0 if ok else 1
    for _, st in STYLES:
        path, n, k, blob = build_test(st, [M], TAG)
        print(f"{path}: {n} glyphs, {k} kerning pairs, {len(blob)} bytes, sha256 {hashlib.sha256(blob).hexdigest()[:16]}")
    if "--fonts" in args:
        return 0
    chars = M.charset()
    out = []
    for _, st in STYLES:
        out.append(save(grid_sheet(st, TAG, chars, "Cyrillique et grec"), f"CYRGRK_grid_{B.file_name(st)}.png"))
    blocks = [("Русский", RUSSIAN), ("Українська", UKRAINIAN), ("Другие языки", OTHERS), ("Ελληνικά", GREEK), ("Capitales", CAPS)]
    out.append(save(sentences_sheet(TAG, blocks, False), "CYRGRK_sentences_light.png"))
    out.append(save(sentences_sheet(TAG, blocks, True), "CYRGRK_sentences_dark.png"))
    plush = B.FAMILIES["PocketPlush"][0]
    out.append(save(sentences_sheet(TAG, [("Pocket Plush", RUSSIAN[:2] + UKRAINIAN[:2] + GREEK[:2] + CAPS)], False, (96, 48, 28),
                                    plush), "CYRGRK_titles_plush.png"))
    out.append(save(styles_sheet(TAG, LATIN_LINE, ["Новый друг! Собрать награду", "Νέος φίλος! Πάρε την ανταμοιβή"]),
                    "CYRGRK_styles.png"))
    out.append(save(metrics_sheet(TAG, "ЁЙЇЎҐДЩџђфбΆΏΫΐΰβζξψ", "Accents, ascendantes et descendantes dans la hauteur de ligne"),
                    "CYRGRK_metrics.png"))
    for p in out:
        print(p)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
