"""ALL_SCRIPTS proof: one line per script for every shipped style, set from the built fonts the way TextMeshPro sets
them (T39C-6).

    python pw_font_scripts_proof.py     # SourceArt/Fonts/Proofs/ALL_SCRIPTS.png from the fonts in SourceArt/Fonts/Build

Layout rules, as in TMP_Text (com.unity.ugui 2.0):
- each character comes from the first font of the language's stack that maps it: the core, then the language's
  fallbacks in pw_font_build.SCRIPT_FONTS order (TMP's fallback search);
- advances from hmtx; kerning from each font's .kern.tsv, only when both characters come from the same font (TMP finds
  a pair by glyph index in the asset of the current character, and the index ranges never overlap);
- Thai marks from the .marks.tsv with TMP's mark-to-base and mark-to-mark rules, after pw_font_thai.preshape as the
  game runs it;
- every glyph rendered by FreeType (Pillow), like TextCore.
Characters missing from every font of the stack are drawn as red boxes and counted.
"""
import os
import sys

from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

import pw_font_build as B

PROOFS = os.path.normpath(os.path.join(B.HERE, "..", "Proofs"))
OUT_NAME = "ALL_SCRIPTS.png"

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
LINE = (236, 224, 204)
MISSING = (214, 64, 52)

# (label for the owner, language, line). Game-like text, nothing religious; scripts mixed with the core's digits,
# punctuation and Latin as real strings are.
LINES = [
    ("Latin (noyau)", None, "Nouvelle espèce ! Écureuil roux · Hatch 3 eggs 2/3 · 1 200 €"),
    ("Cyrillique", "ru", "Новый друг! Собрать награду · Їжачок знайшов ґудзик. «Ёжик» № 7"),
    ("Grec", "el", "Νέος φίλος! Πάρε την ανταμοιβή · 500 πετράδια · ΠΡΟΪΟΝ, καΐκι"),
    ("Vietnamien", "vi", "Bạn mới! Nhận phần thưởng · Chú nhím ngủ gần những bông hoa. ẮỚỰ"),
    ("Thaï", "th", "เพื่อนใหม่! รับรางวัล · ได้รับ 50 อัญมณี · น้ำ ปั่น ฟื้น ญุ ฐุ ก็"),
    ("Japonais", "ja", "新しい仲間！報酬を受け取る · 金貨 1,200 · ウサギは花の近くで眠っている。"),
    ("Coréen", "ko", "새 친구! 알이 부화했어요! · 보상 받기 · 초원 꾸러미 ₩1,990"),
    ("Chinois simplifié", "zh-Hans", "新朋友！领取奖励 · 宝石 1,200 · 小兔子在花园里玩。"),
    ("Chinois traditionnel", "zh-Hant", "新朋友！領取獎勵 · 寶石 1,200 · 小兔子在花園裡玩。"),
]

# TMP_TextParsingUtilities.IsBaseGlyph: the code points TMP treats as marks (the ranges our fonts can hold).
_MARK_RANGES = [(0x300, 0x36F), (0x1AB0, 0x1AFF), (0x1DC0, 0x1DFF), (0x20D0, 0x20FF), (0xFE20, 0xFE2F),
                (0xE31, 0xE31), (0xE34, 0xE3A), (0xE47, 0xE4E)]


def tmp_is_base(cp):
    return not any(a <= cp <= b for a, b in _MARK_RANGES)


def _rows(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="ascii") as f:
        return [line.rstrip("\n").split("\t") for line in f if line.strip() and not line.startswith("#")]


class Face:
    """One built TTF with what TMP would import from it: cmap, advances, kerning and mark records."""

    def __init__(self, path):
        font = TTFont(path)
        self.path = path
        self.cmap = font.getBestCmap()
        hmtx = font["hmtx"]
        self.adv = {cp: hmtx[name][0] for cp, name in self.cmap.items()}
        self.notdef = hmtx[font.getGlyphOrder()[0]][0]
        stem = path[:-4]
        self.kern = {(int(a, 16), int(b, 16)): int(v) for a, b, v, *_ in _rows(stem + ".kern.tsv")}
        self.base, self.mkmk = {}, {}
        for kind, b, bx, by, m, mx, my in _rows(stem + ".marks.tsv"):
            table = self.base if kind == "mark" else self.mkmk
            table[(int(b, 16), int(m, 16))] = ((int(bx), int(by)), (int(mx), int(my)))
        self._pil = {}

    def pil(self, px):
        if px not in self._pil:
            self._pil[px] = ImageFont.truetype(self.path, px)
        return self._pil[px]


def stack(style, language):
    """The core of the style, then the fallbacks the language loads (None: Latin, the core only)."""
    paths = [os.path.join(B.OUT, B.file_name(style) + ".ttf")]
    for sf in B.SCRIPT_FONTS:
        if language in sf.languages:
            p = os.path.join(B.OUT, f"{B.file_name(style)}-{sf.tag}.ttf")
            if os.path.exists(p):
                paths.append(p)
    return paths


def layout(text, faces):
    """[(face or None, char, x, y)] in font units (y up from the baseline), the pen advance and the missing chars."""
    def face_of(ch):
        return next((f for f in faces if ord(ch) in f.cmap), None)

    out, info, missing = [], [], []
    pen, last_base = 0.0, None
    for i, ch in enumerate(text):
        cp = ord(ch)
        face = face_of(ch)
        if face is None:
            missing.append(ch)
        adv = face.adv[cp] if face else faces[0].notdef
        xp = yp = 0.0
        is_base = tmp_is_base(cp)
        if face and not is_base and info:
            applied = False
            if last_base is not None and last_base == len(info) - 1:
                b = info[last_base]
                rec = face.base.get((b[1], cp)) if b[0] is face else None
                if rec:
                    xp, yp = b[2] - pen + rec[0][0] - rec[1][0], rec[0][1] - rec[1][1]
            else:
                for k in range(len(info) - 1, -1 if last_base is None else last_base, -1):
                    p = info[k]
                    rec = face.mkmk.get((p[1], cp)) if p[0] is face else None
                    if rec:
                        xp, yp = p[2] - pen + rec[0][0] - rec[1][0], p[3] + rec[0][1] - rec[1][1]
                        applied = True
                        break
                if not applied and last_base is not None:
                    b = info[last_base]
                    rec = face.base.get((b[1], cp)) if b[0] is face else None
                    if rec:
                        xp, yp = b[2] - pen + rec[0][0] - rec[1][0], rec[0][1] - rec[1][1]
        if is_base:
            last_base = len(info)
        kern = 0
        if face and i + 1 < len(text) and face_of(text[i + 1]) is face:
            kern = face.kern.get((cp, ord(text[i + 1])), 0)
        info.append((face, cp, pen + xp, yp))
        out.append((face, ch, pen + xp, yp))
        pen += adv + kern
    return out, pen, missing


def draw_line(d, x0, baseline, text, faces, px, color):
    glyphs, _, missing = layout(text, faces)
    sc = px / B.UPM
    for face, ch, x, y in glyphs:
        if ch.isspace() or ch == "​":
            continue
        gx, gy = int(round(x0 + x * sc)), int(round(baseline - y * sc))
        if face is None:
            d.rectangle((gx + 2, gy - int(px * 0.7), gx + int(px * 0.5), gy), outline=MISSING, width=2)
            continue
        d.text((gx, gy), ch, font=face.pil(px), fill=color, anchor="ls")
    return missing


def main(args):
    import pw_font_thai as T
    B.quiet_zero_timestamps()
    styles = B.shipped_styles()
    px, label_px = 40, 20
    pitch = int(px * 1.8)
    width = 2500
    block = 70 + pitch * len(LINES) + 40
    img = Image.new("RGB", (width, 120 + block * len(styles)), CREAM)
    d = ImageDraw.Draw(img)
    core0 = Face(os.path.join(B.OUT, B.file_name(styles[0]) + ".ttf"))
    d.text((40, 40), "Toutes les écritures · noyau latin puis polices de repli, mises en page comme TextMeshPro",
           font=core0.pil(36), fill=INK)
    faces_cache = {}
    y = 120
    missing_total = {}
    for style in styles:
        core = Face(os.path.join(B.OUT, B.file_name(style) + ".ttf"))
        d.text((40, y + 10), f"{style.family} {style.weight_name}", font=core.pil(30), fill=SOFT)
        y += 70
        for label, lang, text in LINES:
            paths = stack(style, lang)
            faces = [faces_cache.setdefault(p, Face(p)) for p in paths]
            base = y + int(px * 1.15)
            d.line((330, base, width - 40, base), fill=LINE)
            d.text((40, base), label, font=core0.pil(label_px), fill=INK, anchor="ls")
            tags = " + ".join(["noyau"] + [os.path.basename(p)[len(B.file_name(style)) + 1:-4] for p in paths[1:]])
            d.text((40, base + 22), tags, font=core0.pil(15), fill=SOFT, anchor="ls")
            line = T.preshape(text) if lang == "th" else text
            miss = draw_line(d, 330, base, line, faces, px, INK)
            if miss:
                missing_total[(B.file_name(style), label)] = "".join(miss)
            y += pitch
        y += 40
    os.makedirs(PROOFS, exist_ok=True)
    path = os.path.join(PROOFS, OUT_NAME)
    img.save(path, optimize=True)
    for (st, label), chars in missing_total.items():
        print(f"{st} {label}: {len(chars)} missing ({chars})")
    return path


if __name__ == "__main__":
    print(main(sys.argv[1:]))
