"""Test fonts, proofs and checks for Korean Hangul (pw_font_hangul).

    python pw_font_hangul_test.py              # fonts, proofs and checks
    python pw_font_hangul_test.py fonts        # Build/Test/PocketRound-<Weight>-Hangul-Test.ttf, PocketPlush-Black-...
    python pw_font_hangul_test.py proofs       # Proofs/HANGUL_*.png
    python pw_font_hangul_test.py checks       # composition, identity, gaps, metrics and determinism checks

The test fonts hold our Latin (pw_font_build, unchanged) plus Hangul: the jamo parts as unencoded glyphs and every
syllable as a TrueType composite of its parts. Checks may render Malgun Gothic (Windows) in memory ONLY to confirm
that each of our glyphs is the right character, by structure (counters, pieces and stroke ends of the skeleton,
the stems, bars and ticks of the vowels), never by shape; nothing of it is stored or shipped, its side-by-side
review sheets go to a temporary folder outside the project.
"""
import hashlib
import io
import json
import math
import os
import random
import sys
import tempfile
import time

import numpy as np
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0
from PIL import Image, ImageDraw, ImageFont

import pw_font_engine as E
import pw_font_hangul as K
from pw_font_build import COPYRIGHT, FAMILIES, MANUFACTURER, UPM, VERSION, build_style, file_name, kerning

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.normpath(os.path.join(HERE, ".."))
TEST = os.path.join(FONTS, "Build", "Test")
PROOFS = os.path.join(FONTS, "Proofs")
REVIEW_TMP = os.path.join(tempfile.gettempdir(), "pw_font_hangul_review")
MALGUN = r"C:\Windows\Fonts\malgun.ttf"

STYLES = list(FAMILIES["PocketRound"]) + list(FAMILIES["PocketPlush"])

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
NIGHT = (38, 32, 44)
NIGHT_INK = (255, 244, 226)

# Game-like lines written for these proofs (nothing religious).
UI_WORDS = ["새 친구!", "보상 받기", "계속하기", "설정", "확인", "취소", "상점", "가방", "도감", "알 부화"]
SENTENCES = [
    ("알이 부화했어요!", "The egg hatched!"),
    ("아기 토끼가 태어났어요 · 희귀", "A baby rabbit was born · Rare"),
    ("새로운 종 발견! 붉은 다람쥐", "New species found! Red squirrel"),
    ("화면을 눌러 먹이를 주세요.", "Tap the screen to give food."),
    ("고슴도치가 꽃 옆에서 낮잠을 자요.", "The hedgehog naps by the flowers."),
    ("다시 오신 걸 환영해요! +24.8K 빛", "Welcome back! +24.8K Light"),
    ("알을 3개 부화하세요 · 2/3", "Hatch 3 eggs · 2/3"),
    ("초원 꾸러미 · ₩1,990", "Meadow pack · ₩1,990"),
    ("오늘의 만남: 햄스터를 쓰다듬어 주세요~", "Today's visit: pet the hamster~"),
    ("다람쥐 헌 쳇바퀴에 타고파", "(pangram: every basic consonant)"),
]
STRESS = "이 뷁 햏 꿿 쀍 똠 앉 닭 읊 핥 밟 넋 값 흙 왠 웬 의 꽤 쐐 뙜 쓩 츄 퓨 켜 쳬 롓 뭬"

# Common Korean words (everyday and game words): their syllables give the frequent-syllable list.
COMMON_WORDS = (
    "이다 하다 있다 것이 그리고 우리 나는 그는 사람 한국 시간 오늘 내일 어제 지금 정말 진짜 우리나라 생각 사랑 친구 "
    "학교 선생님 학생 공부 회사 일하다 집에 가족 부모님 어머니 아버지 동생 언니 오빠 누나 형님 아이 아기 "
    "물건 음식 밥을 먹다 마시다 커피 우유 과일 사과 바나나 딸기 포도 수박 고기 생선 채소 김치 라면 빵과 "
    "자동차 버스 지하철 기차 비행기 공항 도시 서울 부산 시장 가게 상점 은행 병원 약국 경찰 우체국 "
    "날씨 비가 눈이 바람 하늘 구름 햇빛 봄 여름 가을 겨울 아침 점심 저녁 밤에 주말 월요일 화요일 수요일 "
    "목요일 금요일 토요일 일요일 하나 둘 셋 넷 다섯 여섯 일곱 여덟 아홉 열 백 천 만 원 개 마리 번 "
    "좋아요 감사합니다 고맙습니다 미안합니다 안녕하세요 안녕히 가세요 계세요 네 아니요 괜찮아요 "
    "그러나 하지만 그래서 그런데 왜냐하면 때문에 위해서 대해서 통해서 따라서 그러면 만약 "
    "문제 질문 대답 이야기 말씀 소리 음악 노래 영화 사진 그림 책을 신문 잡지 편지 전화 컴퓨터 "
    "게임 시작 계속 설정 확인 취소 저장 불러오기 레벨 경험치 보상 받기 선물 상자 열기 구매 "
    "동물 토끼 다람쥐 고슴도치 햄스터 여우 사슴 곰돌이 고양이 강아지 새끼 오리 거북이 개구리 나비 "
    "숲속 초원 연못 언덕 정원 꽃잎 나무 풀밭 버섯 열매 씨앗 알을 부화 성장 먹이 쓰다듬기 놀이 "
    "경제 정치 사회 문화 역사 과학 기술 정보 교육 환경 세계 국가 정부 대통령 국민 시민 지역 "
    "중요한 필요한 가능한 다양한 새로운 같은 다른 많은 적은 작은 크게 높은 낮은 빠른 느린 "
    "했습니다 합니다 됩니다 입니다 있습니다 없습니다 했어요 해요 돼요 이에요 예요 할까요 "
    "모든 각각 서로 함께 다시 아직 이미 벌써 항상 자주 가끔 전혀 거의 너무 아주 매우 조금 "
)

LATIN_LINE = "Pocket Wilds 0123456789 !?.,"


# ------------------------------------------------------------------------------------------- fonts


def _contours(shapes):
    return [E.simplify(c) for c in E.glyph_contours(shapes)]


def _glyph_from_contours(contours):
    pen = TTGlyphPen(None)
    for c in contours:
        if len(c) < 3:
            continue
        pen.moveTo(tuple(int(v) for v in c[0]))
        for p in c[1:]:
            pen.lineTo(tuple(int(v) for v in p))
        pen.closePath()
    return pen.glyph()


def _xmin(contours):
    return min((int(p[0]) for c in contours for p in c), default=0)


def hangul_glyphs(style, chars=None):
    """Hangul glyph records: ordered list of (name, kind, payload, advance, unicode or None).
    kind 'outline' -> payload contours; kind 'composite' -> payload list of (part name, dx, dy, matrix or None)."""
    chars = K.charset() if chars is None else chars
    need = set()
    for ch in chars:
        comp = K.composite(ch, style)
        if comp:
            need.update(comp)
    parts = K.parts(style)
    out = []
    bounds = {}
    for name in K.part_order(style):
        if name not in need:
            continue
        cs = _contours(parts[name])
        out.append((name, "outline", cs, K.advance("", style), None))
        bounds[name] = _xmin(cs)
    for ch in chars:
        comp = K.composite(ch, style)
        name = "uni%04X" % ord(ch)
        adv = K.advance(ch, style)
        if comp:
            dy, mat = _bounce(ch, style)
            out.append((name, "composite", [(n, 0, dy, mat) for n in comp], adv, ord(ch)))
        else:
            shapes, *_ = K.make(ch, style)
            out.append((name, "outline", _contours(shapes), adv, ord(ch)))
    return out, bounds


def _bounce(ch, style):
    """The Latin display bounce and tilt of build_style, when a style has them (none of the shipped ones)."""
    if not (style.bounce or style.tilt):
        return 0, None
    seed = sum(ord(c) for c in ch) * 7919 % 1000 / 1000.0
    dy = int(round((seed - 0.5) * 2 * style.bounce))
    ang = math.radians(((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt)
    return dy, (math.cos(ang), math.sin(ang), -math.sin(ang), math.cos(ang)) if ang else None


def write_test_font(style, path, chars=None, latin=True):
    """Latin of the shipped family (same glyphs and kerning as write_font) plus Hangul. Returns the font bytes."""
    if latin:
        order, cmap, data = build_style(style)
        pairs = kerning(order, cmap, data, style)
    else:
        order, cmap, data, pairs = [".notdef"], {}, {".notdef": ([], 500, None)}, {}
    records, part_xmin = hangul_glyphs(style, chars)
    fb = FontBuilder(UPM, isTTF=True)
    glyph_order = list(order) + [r[0] for r in records]
    fb.setupGlyphOrder(glyph_order)
    for r in records:
        if r[4] is not None:
            cmap[r[4]] = r[0]
    fb.setupCharacterMap(cmap)
    glyphs, metrics = {}, {}
    for name in order:
        contours, adv, _ = data[name]
        glyphs[name] = _glyph_from_contours(contours)
        metrics[name] = (adv, _xmin(contours))
    for name, kind, payload, adv, _ in records:
        if kind == "outline":
            glyphs[name] = _glyph_from_contours(payload)
            metrics[name] = (adv, _xmin(payload))
        else:
            pen = TTGlyphPen(glyphs)
            for part, dx, dy, mat in payload:
                if mat:
                    pen.addComponent(part, (mat[0], mat[1], mat[2], mat[3], dx, dy))
                else:
                    pen.addComponent(part, (1, 0, 0, 1, dx, dy))
            glyphs[name] = pen.glyph()
            metrics[name] = (adv, min(part_xmin[p[0]] for p in payload))
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=950, descent=-250)
    family = style.family + " Hangul Test"
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
                sxHeight=int(style.xh), sCapHeight=int(style.cap), usWeightClass=weight_class, fsType=0,
                achVendID="PWLD", ulUnicodeRange1=(1 << 0) | (1 << 1), ulUnicodeRange2=(1 << (52 - 32)) | (1 << (56 - 32)),
                ulCodePageRange1=(1 << 0) | (1 << 19))
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
    if pairs:
        fea = "languagesystem DFLT dflt;\nlanguagesystem latn dflt;\nfeature kern {\n"
        fea += "".join(f"  pos {a} {b} {v};\n" for (a, b), v in sorted(pairs.items()))
        fea += "} kern;\n"
        addOpenTypeFeaturesFromString(font, fea)
    buf = io.BytesIO()
    font.save(buf)
    raw = buf.getvalue()
    if path:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(raw)
    return raw


def test_path(style):
    return os.path.join(TEST, file_name(style) + "-Hangul-Test.ttf")


def build_fonts():
    out = []
    for style in STYLES:
        t = time.time()
        p = test_path(style)
        raw = write_test_font(style, p)
        out.append((p, len(raw)))
        print(f"{p}: {len(raw) / 1024:.0f} KB, {time.time() - t:.0f} s")
    return out


# ------------------------------------------------------------------------------------------- proofs


def _font(style, size):
    return ImageFont.truetype(test_path(style), size)


def _label_font(size):
    return ImageFont.truetype(test_path(FAMILIES["PocketRound"][1]), size)


def proof_grids():
    """Every glyph: the compatibility jamo, then the syllables by initial (rows: medials, columns: finals)."""
    files = []
    for style in STYLES:
        tag = file_name(style).replace("Pocket", "")
        cell = 46
        f = _font(style, 36)
        lab = _label_font(18)
        # jamo page
        jamo = [chr(c) for c in range(0x3131, 0x318F)]
        cols = 24
        rows = math.ceil(len(jamo) / cols)
        img = Image.new("RGB", (40 + cols * cell + 20, 70 + rows * (cell + 8) + 20), CREAM)
        d = ImageDraw.Draw(img)
        d.text((40, 16), f"{style.family} {style.weight_name} · compatibility jamo U+3131-U+318E", font=lab, fill=SOFT)
        for i, ch in enumerate(jamo):
            x = 40 + (i % cols) * cell
            y = 60 + (i // cols) * (cell + 8)
            d.text((x + cell / 2, y + cell / 2), ch, font=f, fill=INK, anchor="mm")
        p = os.path.join(PROOFS, f"HANGUL_grid_{tag}_jamo.png")
        img.save(p)
        files.append(p)
        # syllables: 4 initials per page, 21 x 28 each
        for page in range(5):
            inits = list(range(page * 4, min(19, page * 4 + 4)))
            block_w = 28 * cell
            W = 60 + 2 * block_w + 60
            H = 60 + 2 * (21 * cell + 50) + 20
            img = Image.new("RGB", (W, H), CREAM)
            d = ImageDraw.Draw(img)
            d.text((40, 14), f"{style.family} {style.weight_name} · syllables, page {page + 1}/5 (rows: medials, columns: finals)",
                   font=lab, fill=SOFT)
            for k, li in enumerate(inits):
                bx = 40 + (k % 2) * (block_w + 40)
                by = 50 + (k // 2) * (21 * cell + 50)
                d.text((bx, by), K.INITIALS[li], font=lab, fill=SOFT)
                for vi in range(21):
                    for ti in range(28):
                        ch = chr(K.SBASE + (li * 21 + vi) * 28 + ti)
                        d.text((bx + ti * cell + cell / 2, by + 24 + vi * cell + cell / 2), ch, font=f, fill=INK, anchor="mm")
            p = os.path.join(PROOFS, f"HANGUL_grid_{tag}_{page + 1}.png")
            img.save(p)
            files.append(p)
    return files


def proof_sentences():
    """Game lines at 96, 48, 28 and 18 px on light and dark, with the Latin of the same style."""
    files = []
    for style in STYLES:
        tag = file_name(style).replace("Pocket", "")
        for dark in (False, True):
            bg, ink, soft = (NIGHT, NIGHT_INK, (190, 170, 150)) if dark else (CREAM, INK, SOFT)
            W, H = 1800, 2700
            img = Image.new("RGB", (W, H), bg)
            d = ImageDraw.Draw(img)
            lab = _label_font(24)
            y = 30
            d.text((40, y), f"{style.family} {style.weight_name} · 한국어 · Korean", font=_font(style, 56), fill=ink)
            y += 90
            for size in (96, 48, 28, 18):
                d.text((40, y), f"{size} px", font=lab, fill=soft)
                y += 34
                f = _font(style, size)
                lines = SENTENCES[:2] if size == 96 else SENTENCES[:6] if size == 48 else SENTENCES
                for ko, en in lines:
                    d.text((40, y), ko, font=f, fill=ink)
                    y += int(size * 1.32)
                    if size >= 48:
                        d.text((40, y), en, font=f, fill=soft)
                        y += int(size * 1.32)
                if size <= 48:
                    d.text((40, y), "  ".join(UI_WORDS), font=f, fill=ink)
                    y += int(size * 1.4)
                y += 24
            d.text((40, y), "Stress: " + STRESS, font=_font(style, 40), fill=ink)
            y += 70
            img = img.crop((0, 0, W, y + 30))
            p = os.path.join(PROOFS, f"HANGUL_text_{tag}_{'dark' if dark else 'light'}.png")
            img.save(p)
            files.append(p)
    return files


def proof_styles():
    """The four styles side by side with their Latin, and the UI words on buttons."""
    W, H = 1800, 1500
    img = Image.new("RGB", (W, H), CREAM)
    d = ImageDraw.Draw(img)
    lab = _label_font(24)
    y = 30
    for style in STYLES:
        d.text((40, y), f"{style.family} {style.weight_name}", font=lab, fill=SOFT)
        y += 36
        f = _font(style, 84)
        d.text((40, y), "새 친구! Hello 보상 받기", font=f, fill=INK)
        y += 112
        f = _font(style, 52)
        d.text((40, y), "알이 부화했어요 · Egg 12/20 · 다람쥐", font=f, fill=INK)
        y += 84
    # buttons
    y += 10
    bx = 40
    for style, word in zip(STYLES, ["계속하기", "보상 받기", "확인", "새 친구!"]):
        f = _font(style, 48)
        w = d.textlength(word, font=f)
        d.rounded_rectangle((bx, y, bx + w + 60, y + 90), 40, fill=(252, 172, 84))
        d.text((bx + 30 + w / 2, y + 45), word, font=f, fill=(255, 255, 255), anchor="mm")
        bx += w + 100
    y += 120
    # vertical centering with Latin punctuation and digits
    f = _font(FAMILIES["PocketRound"][0], 96)
    d.text((40, y), "(가) “말” 1,250원! 정말? 네… 3:00", font=f, fill=INK)
    yb = y + f.getmetrics()[0]
    d.line((40, yb, 1760, yb), fill=(252, 172, 84), width=1)
    y += 150
    img = img.crop((0, 0, W, y))
    p = os.path.join(PROOFS, "HANGUL_styles.png")
    img.save(p)
    return [p]


def proof_layouts():
    """The six layout types (rows) and the crowded syllables, large, in the four styles (columns)."""
    rows = ["가 개 게 이 아", "고 구 그 노 요", "과 괘 궈 궤 긔", "각 갠 걸 읽 앉", "곡 굴 긁 놓 몫", "곽 뷁 꿿 햏 쀍"]
    size, cols = 84, 5
    col_w = cols * size + 40
    img = Image.new("RGB", (40 + col_w * len(STYLES), 70 + len(rows) * 112 + 30), CREAM)
    d = ImageDraw.Draw(img)
    lab = _label_font(22)
    for k, style in enumerate(STYLES):
        d.text((40 + k * col_w, 20), f"{style.family} {style.weight_name}", font=lab, fill=SOFT)
    y = 60
    for r in rows:
        for k, style in enumerate(STYLES):
            d.text((40 + k * col_w, y), r.replace(" ", ""), font=_font(style, size), fill=INK)
        y += 112
    p = os.path.join(PROOFS, "HANGUL_layouts.png")
    img.save(p)
    return [p]


def build_proofs():
    os.makedirs(PROOFS, exist_ok=True)
    files = proof_styles() + proof_layouts() + proof_sentences() + proof_grids()
    for p in files:
        print(p)
    return files


# ------------------------------------------------------------------------------------------- checks
#
# Identity is checked by structure, which does not depend on the design or the weight: counters (holes), connected
# pieces and stroke ends of the skeleton for consonants; stems, bars, ticks and their side for vowels. Our glyphs
# are compared with Malgun Gothic (Regular or Bold: either may match), rendered in memory only.

MALGUN_BOLD = r"C:\Windows\Fonts\malgunbd.ttf"
# Design differences that are not identity errors: Malgun joins the two letters of ㄵ, we keep every letter apart.
DESIGN_DIFFERENCES = {"ㄵ"}


def _render(font, ch, size=120):
    img = Image.new("L", (size * 2, size * 2), 0)
    ImageDraw.Draw(img).text((size, size), ch, font=font, fill=255, anchor="mm")
    return np.asarray(img) > 127


def thin(mask):
    """Zhang-Suen thinning (skeleton of a binary image), computed inside the ink box only."""
    ys, xs = np.where(mask)
    out = np.zeros_like(mask, dtype=bool)
    if len(ys) == 0:
        return out
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    out[y0:y1, x0:x1] = _thin(mask[y0:y1, x0:x1])
    return out


def _thin(mask):
    a = np.pad(mask.astype(np.uint8), 1)
    changed = True
    while changed:
        changed = False
        for step in (0, 1):
            P2, P3, P4, P5 = a[:-2, 1:-1], a[:-2, 2:], a[1:-1, 2:], a[2:, 2:]
            P6, P7, P8, P9 = a[2:, 1:-1], a[2:, :-2], a[1:-1, :-2], a[:-2, :-2]
            c = a[1:-1, 1:-1]
            nb = [P2, P3, P4, P5, P6, P7, P8, P9]
            nsum = sum(n.astype(int) for n in nb)
            seq = nb + [P2]
            trans = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(int) for i in range(8))
            if step == 0:
                m = (c == 1) & (nsum >= 2) & (nsum <= 6) & (trans == 1) & (P2 * P4 * P6 == 0) & (P4 * P6 * P8 == 0)
            else:
                m = (c == 1) & (nsum >= 2) & (nsum <= 6) & (trans == 1) & (P2 * P4 * P8 == 0) & (P2 * P6 * P8 == 0)
            if m.any():
                c[m] = 0
                changed = True
    return a[1:-1, 1:-1].astype(bool)


def _components(mask, eight=True, min_area=1):
    """Connected pieces of a binary image (pieces smaller than min_area pixels are ignored): union-find over the
    runs of each row (fast)."""
    parent = []

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    prev, sizes = [], []
    for row in mask:
        d = np.diff(np.concatenate([[0], row.astype(np.int8), [0]]))
        starts, ends = np.where(d == 1)[0], np.where(d == -1)[0]
        cur = []
        k = 0
        for a, b in zip(starts, ends):
            i = len(parent)
            parent.append(i)
            lo, hi = (a - 1, b + 1) if eight else (a, b)
            while k < len(prev) and prev[k][1] <= lo:
                k += 1
            j = k
            while j < len(prev) and prev[j][0] < hi:
                ri, rj = find(i), find(prev[j][2])
                if ri != rj:
                    parent[ri] = rj
                j += 1
            cur.append((a, b, i))
            sizes.append(b - a)
        prev = cur
    area = {}
    for i in range(len(parent)):
        r = find(i)
        area[r] = area.get(r, 0) + sizes[i]
    return sum(1 for v in area.values() if v >= min_area)


def topology(mask):
    """(counters, pieces, stroke ends)."""
    sk = thin(mask)
    a = np.pad(sk, 1).astype(int)
    nb = sum(np.roll(np.roll(a, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx)[1:-1, 1:-1]
    ends = int((sk & (nb == 1)).sum())
    # a counter smaller than (0.02 em)^2 is a rasterization pocket in a sharp joint, not a counter
    em = mask.shape[0] / 2
    holes = _components(~np.pad(mask, 1), eight=False, min_area=max(4, int((0.02 * em) ** 2))) - 1
    return holes, _components(mask), ends


def _max_runs(arr):
    best = np.zeros(arr.shape[0], int)
    cur = np.zeros(arr.shape[0], int)
    for j in range(arr.shape[1]):
        cur = np.where(arr[:, j], cur + 1, 0)
        best = np.maximum(best, cur)
    return best


def _groups(idx):
    out = []
    for i in idx:
        if out and i - out[-1][-1] <= 2:
            out[-1].append(i)
        else:
            out.append([i])
    return out


def _longest_run(line):
    """(length, start) of the longest run of True."""
    best, start, cur, s0 = 0, 0, 0, 0
    for j, v in enumerate(line):
        if v:
            if cur == 0:
                s0 = j
            cur += 1
            if cur > best:
                best, start = cur, s0
        else:
            cur = 0
    return best, start


def _bridge(line, gap=3):
    """Close gaps of up to gap pixels (a skeleton line broken at a joint stays one stroke)."""
    out = line.copy()
    idx = np.where(line)[0]
    for a, b in zip(idx, idx[1:]):
        if 1 < b - a <= gap + 1:
            out[a:b] = True
    return out


def _run_at(line, i):
    """(start, end) of the run of True that contains index i (or the nearest one)."""
    idx = np.where(line)[0]
    if len(idx) == 0:
        return i, i
    if not line[i]:
        i = int(idx[np.argmin(np.abs(idx - i))])
    a = i
    while a > 0 and line[a - 1]:
        a -= 1
    b = i
    while b + 1 < len(line) and line[b + 1]:
        b += 1
    return a, b + 1


def vowel_structure(mask, em):
    """(main stems, bars, stems up from a bar, stems down from a bar, ticks left, ticks between, ticks right).
    Strokes are found on the skeleton and measured on the ink; they are told apart by how they join, not by
    their length: a vertical whose end meets the inside of a horizontal stands on a bar (ㅗ ㅜ); a short
    horizontal whose end meets the inside of a vertical is a tick (ㅏ ㅓ)."""
    sk = thin(mask)
    dv = sk | np.roll(sk, 1, 1) | np.roll(sk, -1, 1)
    dh = sk | np.roll(sk, 1, 0) | np.roll(sk, -1, 0)
    vr, hr = _max_runs(dv.T), _max_runs(dh)
    gap = max(3, int(0.03 * em))
    vert = []
    for g in _groups(np.where(vr >= 0.06 * em)[0]):
        n, y0 = _longest_run(_bridge(dv[:, g].any(axis=1), gap))
        c = int(round(np.mean(g)))
        a, b = _run_at(mask[:, c], y0 + n // 2)
        vert.append((float(np.mean(g)), a, b, n))
    hor = []
    for g in _groups(np.where(hr >= 0.06 * em)[0]):
        n, x0 = _longest_run(_bridge(dh[g].any(axis=0), gap))
        r = int(round(np.mean(g)))
        a, b = _run_at(mask[r], x0 + n // 2)
        hor.append((float(np.mean(g)), a, b, n))
    width = mask.sum() / max(1, sk.sum())
    # tolerance at a joint (ends are measured on the ink: half a stroke from the other centerline), depth inside
    me, mi = 0.55 * width + 0.02 * em, 0.02 * em

    def v_on_h(v, h):   # the end of v meets the inside of h
        return (abs(v[1] - h[0]) <= me or abs(v[2] - h[0]) <= me) and h[1] + mi < v[0] < h[2] - mi

    def h_on_v(h, v):   # an end of h meets the inside of v
        return (abs(h[1] - v[0]) <= me or abs(h[2] - v[0]) <= me) and v[1] + mi < h[0] < v[2] - mi

    bar_stems = {i for i, v in enumerate(vert) if any(v_on_h(v, h) for h in hor)}
    mains = [v for i, v in enumerate(vert) if i not in bar_stems]
    ticks = [h for h in hor if any(h_on_v(h, v) for v in mains) and h[3] < 0.32 * em
             and not any(v_on_h(vert[i], h) for i in bar_stems)]
    bars = [h for h in hor if h not in ticks]
    up = down = 0
    for i in bar_stems:
        v = vert[i]
        h = next(h for h in hor if v_on_h(v, h))
        if (v[1] + v[2]) / 2 < h[0]:
            up += 1
        else:
            down += 1
    tl = tb = tr = 0
    mx = [v[0] for v in mains]
    for h in ticks:
        cx = (h[1] + h[2]) / 2
        if cx < min(mx):
            tl += 1
        elif cx > max(mx):
            tr += 1
        else:
            tb += 1
    return len(mains), len(bars), up, down, tl, tb, tr


def _part_mask(shapes, size=120):
    """Our part (an unencoded glyph) rendered alone at the scale of the references."""
    s = size / 1000.0
    W = H = size * 2
    acc = np.zeros((H, W), dtype=np.int32)
    for c in E.glyph_contours(shapes):
        if len(c) < 3:
            continue
        img = Image.new("L", (W, H), 0)
        ImageDraw.Draw(img).polygon([(x * s + size * 0.5, (850 - y) * s + size * 0.4) for x, y in c], fill=1)
        m = np.asarray(img, dtype=np.int32)
        acc += m if E.signed_area(c) < 0 else -m
    return acc != 0


class References:
    """Structure of each jamo in Malgun Gothic Regular and Bold, computed in memory (never stored)."""

    def __init__(self, size=200):
        self.size = size
        self.fonts = [ImageFont.truetype(MALGUN, size), ImageFont.truetype(MALGUN_BOLD, size)]
        self.cons, self.vow = {}, {}

    def consonant(self, ch):
        if ch not in self.cons:
            self.cons[ch] = {topology(_render(f, ch, self.size)) for f in self.fonts}
        return self.cons[ch]

    def vowel(self, ch):
        if ch not in self.vow:
            self.vow[ch] = {vowel_structure(_render(f, ch, self.size), self.size) for f in self.fonts}
        return self.vow[ch]


def check_jamo_structure(style, refs):
    """Every modern compatibility jamo: same structure as the reference."""
    f = ImageFont.truetype(test_path(style), refs.size)
    fails = []
    for c in range(0x3131, 0x3164):
        ch = chr(c)
        if ch in DESIGN_DIFFERENCES:
            continue
        mask = _render(f, ch, refs.size)
        if ch in K.VCLASS:
            ours, ref = vowel_structure(mask, refs.size), refs.vowel(ch)
            hard = ours not in ref
        else:
            ours, ref = topology(mask), refs.consonant(ch)
            hard = ours[:2] not in {r[:2] for r in ref}
        if ours not in ref:
            fails.append((ch, ours, sorted(ref), "hard" if hard else "ends"))
    return fails


def check_archaic_jamo(style, refs):
    """Archaic jamo (U+3165..U+318E): counters and pieces as in the reference (stroke ends vary with the design)."""
    f = ImageFont.truetype(test_path(style), refs.size)
    fails = []
    for c in range(0x3165, 0x318F):
        ch = chr(c)
        ours = topology(_render(f, ch, refs.size))[:1]
        ref = {topology(_render(rf, ch, refs.size))[:1] for rf in refs.fonts}
        if ours not in ref:
            fails.append((ch, ours, sorted(ref)))
    return fails


def check_part_structure(style, refs):
    """Every jamo part of every layout group (the shapes the syllables are made of): same structure as the
    reference jamo; vowel parts are compared with the reference vowel."""
    h = K._h(style)
    fails, n = [], 0
    vname = {v: k for k, v in K.VNAME.items()}
    cname = {v: k for k, v in K.CNAME.items()}
    for name, shapes in h.parts().items():
        kind, jam = name.split(".")[1], name.split(".")[2]
        ch = vname[jam] if kind == "V" else cname[jam]
        if ch in DESIGN_DIFFERENCES:
            continue
        n += 1
        mask = _part_mask(shapes, refs.size)
        if kind == "V":
            ours, ref = vowel_structure(mask, refs.size), refs.vowel(ch)
            hard = ours not in ref
        else:
            ours, ref = topology(mask), refs.consonant(ch)
            hard = ours[:2] not in {r[:2] for r in ref}
        if ours not in ref:
            fails.append((name, ch, ours, sorted(ref), "hard" if hard else "ends"))
    return fails, n


def check_syllable_structure(style, refs, chars):
    """Whole syllables: their structure is exactly the sum of the structures of their parts (counters and pieces),
    so composing never merges, closes or breaks a jamo; the parts themselves are checked against the reference.
    (A direct comparison of whole syllables with Malgun is not meaningful: its jamo often touch, ours never do.
    Syllables are compared with it visually, on the review sheets.)"""
    f = ImageFont.truetype(test_path(style), refs.size)
    h = K._h(style)
    parts = h.parts()
    cache = {}
    fails = []
    for ch in chars:
        ours = topology(_render(f, ch, refs.size))
        total = [0, 0, 0]
        for name in h.composite(ch):
            if name not in cache:
                cache[name] = topology(_part_mask(parts[name], refs.size))
            total = [a + b for a, b in zip(total, cache[name])]
        if ours[:2] != tuple(total[:2]):
            fails.append((ch, ours, tuple(total), "hard"))
        elif ours != tuple(total):
            fails.append((ch, ours, tuple(total), "ends"))
    return fails


def check_composition(style):
    """Each syllable references exactly the parts of its own jamo. The jamo come from Unicode's canonical
    decomposition (NFD and the character names), independently of the arithmetic of the module."""
    import unicodedata
    comp = {unicodedata.name(chr(c)).split()[-1]: chr(c) for c in range(0x3131, 0x318F) if c != 0x3164}
    h = K._h(style)
    used, bad = set(), []
    for ch in K.charset()[94:]:
        jamo = [comp[unicodedata.name(j).split()[-1]] for j in unicodedata.normalize("NFD", ch)]
        l, m = jamo[0], jamo[1]
        t = jamo[2] if len(jamo) == 3 else ""
        vc = K.VCLASS[m]
        want = [h.name_initial(l, vc, bool(t)), h.name_medial(m, bool(t))]
        if t:
            want.append(h.name_final(t, K.KIND[vc]))
        got = h.composite(ch)
        used.update(got)
        if got != want:
            bad.append(ch)
    unused = [n for n in h.parts() if n not in used]
    return bad, unused


def _raster(shapes, scale=0.25):
    """Ink mask of shapes (non-zero winding), em 1000 -> 250 px."""
    W, H = int(1100 * scale), int(1100 * scale)
    acc = np.zeros((H, W), dtype=np.int32)
    for c in E.glyph_contours(shapes):
        if len(c) < 3:
            continue
        img = Image.new("L", (W, H), 0)
        pts = [((x + 50) * scale, (900 - y) * scale) for x, y in c]
        ImageDraw.Draw(img).polygon(pts, fill=1)
        m = np.asarray(img, dtype=np.int32)
        acc += m if E.signed_area(c) < 0 else -m
    return acc != 0


def _dilate(mask, r):
    out = mask.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r and (dx or dy):
                out |= np.roll(np.roll(mask, dy, axis=0), dx, axis=1)
    return out


def check_gaps(style, scale=0.25):
    """Nothing touching: the ink of the initial, the medial and the final of every syllable stays apart by at least
    a gap (in font units). Returns the smallest gaps found and the pairs below the limit."""
    h = K._h(style)
    parts = h.parts()
    masks = {n: _raster(s, scale) for n, s in parts.items()}
    limit_units = 28.0
    radii = [1, 2, 3, 4, 5, 6, 8, 10, 12]
    dil = {}

    def gap(a, b):
        """Smallest dilation radius (px) at which a touches b."""
        ma, mb = masks[a], masks[b]
        if (ma & mb).any():
            return 0
        for r in radii:
            k = (a, r)
            if k not in dil:
                dil[k] = _dilate(ma, r)
            if (dil[k] & mb).any():
                return r
        return 99
    pairs = set()
    for ch in K.charset()[94:]:
        names = h.composite(ch)
        for i in range(len(names)):
            for j in range(i + 1, len(names)):
                pairs.add((names[i], names[j]))
    results = []
    for a, b in sorted(pairs):
        results.append((gap(a, b) / scale, a, b))
    results.sort()
    bad = [r for r in results if r[0] < limit_units]
    return results[:10], bad, len(pairs)


def check_metrics(style):
    """Optical size and color of simple and crowded syllables: ink width and height (font units) and ink
    coverage of the em square (percent)."""
    f = ImageFont.truetype(test_path(style), 200)
    out = {}
    for ch in "이가아나각닭뷁꿿햏쀍똠읊":
        a = _render(f, ch, 200)
        ys, xs = np.where(a)
        out[ch] = (int((xs.max() - xs.min() + 1) * 5), int((ys.max() - ys.min() + 1) * 5), round(float(a.sum()) / 400.0, 1))
    return out


def frequent_syllables(n=200):
    seen = []
    counts = {}
    for ch in COMMON_WORDS:
        if K.SBASE <= ord(ch) < K.SBASE + 11172:
            counts[ch] = counts.get(ch, 0) + 1
            if ch not in seen:
                seen.append(ch)
    first = {c: i for i, c in enumerate(seen)}
    seen.sort(key=lambda c: (-counts[c], first[c]))
    return seen[:n]


def review_sheet(style, chars, path, cols=25):
    """Side-by-side review (ours over the reference) kept outside the project."""
    cell = 64
    rows = math.ceil(len(chars) / cols)
    img = Image.new("RGB", (cols * cell + 20, rows * cell * 2 + 20), (255, 255, 255))
    d = ImageDraw.Draw(img)
    fo = ImageFont.truetype(test_path(style), 48)
    fr = ImageFont.truetype(MALGUN, 48)
    for i, ch in enumerate(chars):
        x = 10 + (i % cols) * cell + cell / 2
        y = 10 + (i // cols) * cell * 2
        d.text((x, y + cell / 2), ch, font=fo, fill=(0, 0, 0), anchor="mm")
        d.text((x, y + cell * 1.5), ch, font=fr, fill=(150, 150, 150), anchor="mm")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def run_checks(styles=None):
    styles = styles or STYLES
    rnd = random.Random(20261009)
    sample300 = [chr(K.SBASE + rnd.randrange(11172)) for _ in range(300)]
    freq = frequent_syllables(200)
    refs = References()
    report = {"frequent": "".join(freq), "random": "".join(sample300)}
    for style in styles:
        key = f"{style.family} {style.weight_name}"
        t = time.time()
        r = {}
        bad, unused = check_composition(style)
        r["composition"] = {"wrong": bad, "unused_parts": unused}
        r["jamo_structure_fails"] = check_jamo_structure(style, refs)
        r["archaic_jamo_fails"] = check_archaic_jamo(style, refs)
        pf, pn = check_part_structure(style, refs)
        r["part_structure"] = {"parts": pn, "fails": pf}
        r["random300_structure_fails"] = check_syllable_structure(style, refs, sample300)
        r["frequent200_structure_fails"] = check_syllable_structure(style, refs, freq)
        low, badg, npairs = check_gaps(style)
        r["gaps"] = {"pairs": npairs, "smallest": [(round(g), a, b) for g, a, b in low], "below_limit": len(badg)}
        r["metrics"] = check_metrics(style)
        report[key] = r
        tag = file_name(style).replace("Pocket", "")
        review_sheet(style, sample300, os.path.join(REVIEW_TMP, f"review_random300_{tag}.png"))
        review_sheet(style, freq, os.path.join(REVIEW_TMP, f"review_frequent200_{tag}.png"))
        review_sheet(style, [chr(c) for c in range(0x3131, 0x318F)], os.path.join(REVIEW_TMP, f"review_jamo_{tag}.png"), 24)
        def hard(lst):
            return sum(1 for x in lst if x[-1] == "hard")
        print(key, f"{time.time() - t:.0f} s;",
              f"composition wrong {len(bad)}, unused parts {len(unused)};",
              f"jamo differences {hard(r['jamo_structure_fails'])} (+{len(r['jamo_structure_fails']) - hard(r['jamo_structure_fails'])} stroke ends only),",
              f"archaic {len(r['archaic_jamo_fails'])};",
              f"parts {hard(pf)}/{pn} (+{len(pf) - hard(pf)} stroke ends only);",
              f"syllables not equal to their parts {hard(r['random300_structure_fails'])}/300 random,",
              f"{hard(r['frequent200_structure_fails'])}/200 frequent;",
              f"gaps below limit {len(badg)}/{npairs}", flush=True)
    return report


def check_determinism(style):
    a = write_test_font(style, None)
    b = write_test_font(style, None)
    return hashlib.sha256(a).hexdigest() == hashlib.sha256(b).hexdigest(), len(a)


def build_hashes():
    build = os.path.join(FONTS, "Build")
    return {n: sha(os.path.join(build, n)) for n in sorted(os.listdir(build)) if n.endswith(".ttf")}


if __name__ == "__main__":
    what = sys.argv[1:] or ["fonts", "proofs", "checks"]
    before = build_hashes()
    if "fonts" in what:
        build_fonts()
    if "proofs" in what:
        build_proofs()
    if "checks" in what:
        rep = run_checks()
        same, size = check_determinism(FAMILIES["PocketRound"][0])
        rep["deterministic"] = same
        rep["sizes_kb"] = {os.path.basename(test_path(s)): round(os.path.getsize(test_path(s)) / 1024) for s in STYLES}
        out = os.path.join(REVIEW_TMP, "hangul_checks.json")
        os.makedirs(REVIEW_TMP, exist_ok=True)
        with open(out, "w", encoding="utf-8") as f:
            json.dump(rep, f, ensure_ascii=False, indent=1, default=str)
        print("deterministic:", same, "checks:", out)
    after = build_hashes()
    print("shipped fonts unchanged:", before == after)
