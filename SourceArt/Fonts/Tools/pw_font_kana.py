"""Japanese kana, CJK punctuation and full-width forms for the Pocket Wilds fonts (FJ, script "kana").

Same pen as the Latin (pw_font_engine strokes with round caps, the Style's swell and inflate), drawn as stroke
skeletons in a 0..100 design box (y down, the kana face) and mapped onto the ideographic em box. Every glyph has a
full-em advance (1000), no kerning.

API for the merge hook in pw_font_build.py:
    SCRIPT                      "kana"
    charset()                   encoded code points (as str), in order
    make(ch, style)             the Glyphs.make tuple (shapes, lc, rc, upper, (x0, x1)); with build_style's spacing
                                formula it gives an advance of exactly ADVANCE and leaves the ink where it is drawn
    advance(ch, style)          ADVANCE (0 for the combining marks U+3099 and U+309A)
    extra_glyphs()              unencoded glyph names (regional punctuation), in order
    make_extra(name, style)     same tuple as make()
    features(style)             feaLib code: locl for Chinese punctuation (no languagesystem lines)
    LANGUAGESYSTEMS             the (script, language) pairs the features need
    region_cmap(region)         {code point: glyph name} overrides for a per-region font ("zh-Hans", "zh-Hant", "zh-HK")
    LINE_START_FORBIDDEN        TextMeshPro "Leading Characters" (may not start a line)
    LINE_END_FORBIDDEN          TextMeshPro "Following Characters" (may not end a line)

Units: font units, 1000 per em, y up. Ideographic em box -120..880 (center 380), ideographic face 880.
"""
import math
import unicodedata

import numpy as np
from fontTools.agl import UV2AGL

from pw_font_engine import Dot, Path, Poly, Stroke, bounds, glyph_contours
from pw_font_glyphs import CHARS as LATIN_CHARS
from pw_font_glyphs import Glyphs, _map_points

SCRIPT = "kana"
ADVANCE = 1000
EM_BOTTOM, EM_TOP = -120, 880
CY = (EM_BOTTOM + EM_TOP) / 2.0       # 380: vertical center shared with the Han face
FACE = 880.0                          # ideographic face (kanji, 〇, 々)
KANA_FACE = 860.0                     # design box; a typical kana inks ~800 units, 0.9 x the ideographic face
KATA_SCALE = 0.965                    # katakana a touch smaller than hiragana
SMALL_SCALE = 0.70                    # small kana (ぁ ゃ ッ ...)
TENSION = 0.38                        # cubic handles = chord x tension (0.39 ~ circular arcs)
KANA_INFLATE = 0.55                   # share of the Style's inflate on the long kana strokes (Pocket Plush)

# Design box -> em: x 0..100 left to right, y 0..100 top to bottom.
_SB_P = 46.0                          # pw_font_build.SB["P"]: spacing class used for the fixed advance


def glyph_name(ch):
    """Same naming as pw_font_build.glyph_name."""
    if ch == " ":
        return "space"
    return UV2AGL.get(ord(ch), "uni%04X" % ord(ch))


# ---------------------------------------------------------------------------------------------------------------
# Skeletons. A glyph is a list of strokes; a stroke is a list of points (x, y) or (x, y, angle). The angle is the
# direction of travel in design space (degrees, 0 = right, 90 = down). Interior tangents are automatic (bisector),
# end tangents mirror the next one (natural arcs). Sharp corners are separate strokes sharing a point: the round
# caps make the soft maru gothic corner. ("dot", x, y, scale) is a round dot.
# ---------------------------------------------------------------------------------------------------------------

HIRA = {
    "あ": [[(18, 27), (80, 23)],
          [(44, 7), (43, 38), (46, 62), (52, 80)],
          [(70, 39), (56, 57), (38, 77), (26, 87), (15, 83), (14, 70), (25, 58), (48, 49), (71, 50), (85, 60), (87, 75),
           (78, 87), (60, 94)]],
    "い": [[(22, 22), (19, 47), (23, 69), (31, 79), (38, 73)],
          [(69, 31), (78, 43), (83, 61)]],
    "う": [[(38, 9), (62, 16)],
          [(24, 45), (43, 35), (63, 33), (78, 43), (79, 60), (69, 77), (52, 89), (35, 96)]],
    "え": [[(38, 9), (62, 16)],
          [(24, 40), (68, 37)], [(68, 37), (24, 88)], [(24, 88), (48, 74)],
          [(48, 74), (55, 85), (66, 91), (86, 90)]],
    "お": [[(14, 30), (54, 28)],
          [(34, 8), (34, 48), (33, 66), (29, 81), (19, 87), (10, 80), (12, 68), (35, 59), (57, 55), (77, 60), (87, 73),
           (80, 87), (61, 94)],
          [(70, 19), (84, 31)]],
    "か": [[(12, 40), (37, 36), (58, 35), (66, 45), (65, 66), (59, 85), (51, 92)], [(51, 92), (42, 86)],
          [(40, 9), (36, 44), (28, 70), (17, 91)],
          [(76, 28), (84, 40), (88, 55)]],
    "き": [[(24, 24), (72, 20)],
          [(18, 45), (80, 41)],
          [(40, 6), (54, 35), (67, 62)],
          [(35, 69), (30, 79), (36, 88), (52, 92), (74, 91)]],
    "く": [[(64, 10), (30, 49)], [(30, 49), (64, 90)]],
    "け": [[(18, 14), (16, 46), (18, 74), (24, 88), (30, 82)],
          [(36, 38), (88, 35)],
          [(70, 10), (71, 50), (67, 72), (56, 88), (41, 96)]],
    "こ": [[(28, 25), (58, 21, 0), (70, 27, 35)],
          [(24, 67), (30, 81), (48, 87), (80, 84)]],
    "さ": [[(18, 32), (80, 28)],
          [(42, 8), (55, 35), (67, 61)],
          [(34, 69), (30, 80), (38, 89), (54, 92), (74, 92)]],
    "し": [[(32, 10), (30, 48), (32, 73), (42, 87), (58, 90), (74, 80), (84, 64)]],
    "す": [[(12, 32), (88, 30)],
          [(54, 8), (54, 46), (51, 60), (43, 64), (37, 56), (41, 47), (51, 46), (56, 55), (56, 69), (50, 84), (40, 96)]],
    "せ": [[(10, 42), (90, 38)],
          [(70, 14), (70, 52), (68, 60), (62, 64)],
          [(32, 14), (32, 72), (36, 84), (48, 90), (82, 90)]],
    "そ": [[(30, 14), (64, 11)], [(64, 11), (22, 46)], [(22, 46), (84, 40)],
          [(84, 40), (62, 50), (50, 66), (52, 84), (70, 94)]],
    "た": [[(12, 30), (50, 27)],
          [(34, 8), (28, 46), (17, 91)],
          [(56, 46), (82, 44)],
          [(52, 69), (56, 83), (68, 90), (86, 89)]],
    "ち": [[(16, 28), (76, 24)],
          [(42, 6), (36, 33), (30, 58)],
          [(30, 58), (52, 47), (72, 50), (82, 64), (76, 81), (58, 91), (40, 94)]],
    "つ": [[(12, 40), (36, 31), (64, 30), (84, 40), (86, 58), (74, 76), (54, 86), (33, 88)]],
    "て": [[(14, 26), (84, 20)],
          [(84, 20), (58, 32), (44, 52), (46, 76), (60, 90), (75, 94)]],
    "と": [[(34, 12), (36, 30), (40, 46)],
          [(76, 30), (48, 46), (30, 62), (28, 78), (40, 88), (60, 91), (77, 90)]],
    "な": [[(12, 28), (46, 26)],
          [(32, 8), (26, 40), (14, 70)],
          [(64, 30), (80, 44)],
          [(60, 48), (60, 62), (58, 79), (52, 90), (39, 92), (34, 84), (44, 76), (62, 78), (84, 91)]],
    "に": [[(16, 14), (14, 50), (17, 76), (24, 90)],
          [(42, 30), (77, 27)],
          [(40, 66), (44, 78), (58, 82), (84, 80)]],
    "ぬ": [[(22, 22), (32, 50), (42, 78)],
          [(60, 8), (44, 46), (24, 82), (16, 80), (14, 64), (26, 44), (48, 32), (72, 36), (86, 52), (85, 72), (80, 87),
           (72, 92), (66, 86), (72, 79), (84, 82), (94, 92)]],
    "ね": [[(26, 8), (26, 94)],
          [(8, 36), (34, 32)], [(34, 32), (10, 86)],
          [(10, 86), (30, 58), (50, 40), (70, 36), (84, 48), (85, 70), (80, 86), (72, 92), (66, 86), (72, 79), (84, 82),
           (94, 92)]],
    "の": [[(54, 28), (44, 58), (28, 84), (16, 80), (12, 62), (20, 40), (40, 24), (62, 20), (82, 30), (90, 52), (84, 74),
           (68, 88), (52, 94)]],
    "は": [[(16, 10), (14, 50), (16, 76), (22, 90)],
          [(36, 34), (86, 32)],
          [(64, 10), (64, 68), (62, 80), (54, 90), (41, 90), (36, 81), (46, 74), (64, 76), (86, 90)]],
    "ひ": [[(12, 26), (36, 22)],
          [(36, 22), (22, 54), (26, 80), (44, 88), (60, 74), (68, 48), (72, 30)],
          [(72, 30), (82, 52), (94, 62)]],
    "ふ": [[(44, 8), (56, 22)],
          [(46, 34), (58, 48), (60, 66), (52, 84), (40, 86)],
          [(24, 62), (12, 84)],
          [(74, 58), (88, 80)]],
    "へ": [[(8, 62), (28, 40), (36, 38), (92, 80)]],
    "ほ": [[(16, 10), (15, 50), (16, 76), (22, 90)],
          [(38, 18), (84, 16)],
          [(38, 44), (82, 42)],
          [(61, 18), (61, 70), (59, 80), (52, 90), (39, 90), (34, 81), (44, 74), (62, 76), (86, 90)]],
    "ま": [[(20, 26), (80, 22)],
          [(22, 48), (78, 44)],
          [(48, 6), (48, 70), (46, 83), (38, 90), (25, 88), (21, 79), (32, 73), (50, 74), (76, 88)]],
    "み": [[(18, 20), (48, 16)],
          [(48, 16), (36, 48), (26, 72), (20, 84), (12, 82), (13, 70), (26, 62), (48, 60), (72, 64), (92, 74)],
          [(70, 30), (68, 62), (62, 82), (54, 94)]],
    "む": [[(10, 28), (54, 26)],
          [(32, 5), (32, 50), (29, 64), (19, 67), (11, 58), (17, 47), (30, 50), (33, 64), (34, 82), (44, 91), (68, 91),
           (80, 82), (83, 66)],
          [(72, 22), (86, 38)]],
    "め": [[(22, 22), (32, 50), (42, 76)],
          [(62, 6), (40, 47), (19, 82)],
          [(19, 82), (13, 70), (15, 55), (26, 42), (48, 32), (71, 34), (87, 48), (87, 70), (73, 86), (54, 92)]],
    "も": [[(38, 8), (32, 40), (32, 66), (38, 84), (54, 92), (72, 88), (82, 72), (82, 58)],
          [(14, 32), (64, 30)],
          [(12, 56), (66, 54)]],
    "や": [[(10, 46), (40, 37), (68, 34), (86, 44), (84, 60), (64, 66)],
          [(48, 8), (58, 21)],
          [(30, 6), (40, 52), (52, 94)]],
    "ゆ": [[(18, 22), (15, 48), (17, 68), (21, 78)],
          [(21, 78), (27, 60), (40, 41), (60, 31), (80, 36), (89, 52), (85, 68), (68, 76), (48, 73)],
          [(56, 5), (58, 38), (58, 64), (52, 84), (40, 96)]],
    "よ": [[(46, 38), (76, 35)],
          [(44, 8), (44, 72), (42, 85), (32, 90), (21, 86), (21, 77), (32, 71), (52, 73), (80, 88)]],
    "ら": [[(36, 8), (52, 20)],
          [(24, 30), (22, 48), (20, 64)],
          [(20, 64), (40, 50), (64, 48), (80, 60), (78, 78), (60, 90), (38, 93)]],
    "り": [[(24, 16), (22, 40), (24, 58), (30, 64), (36, 56)],
          [(68, 12), (72, 40), (70, 64), (60, 84), (44, 96)]],
    "る": [[(22, 22), (70, 18)], [(70, 18), (20, 64)],
          [(20, 64), (44, 50), (70, 52), (84, 66), (80, 82), (66, 92), (48, 92), (42, 84), (50, 77), (60, 82), (58, 92)]],
    "れ": [[(24, 8), (24, 94)],
          [(8, 36), (34, 32)], [(34, 32), (10, 86)],
          [(10, 86), (30, 58), (46, 42), (56, 40), (62, 52), (62, 76), (70, 90), (84, 84), (92, 72)]],
    "ろ": [[(22, 22), (70, 18)], [(70, 18), (20, 64)],
          [(20, 64), (44, 50), (70, 52), (84, 66), (80, 84), (62, 93), (40, 94)]],
    "わ": [[(26, 8), (26, 94)],
          [(8, 36), (34, 32)], [(34, 32), (10, 86)],
          [(10, 86), (30, 58), (50, 40), (70, 36), (86, 48), (88, 68), (78, 84), (58, 93)]],
    "ゐ": [[(26, 12), (46, 8)], [(46, 8), (34, 38), (22, 66), (14, 86)],
          [(14, 86), (22, 68), (36, 48), (56, 38), (76, 42), (89, 58), (86, 78), (72, 90), (58, 90), (54, 81), (63, 76),
           (72, 82), (74, 92)]],
    "ゑ": [[(22, 9), (68, 6)], [(68, 6), (16, 54)],
          [(16, 54), (36, 42), (56, 39), (65, 48), (58, 59), (45, 58), (42, 48)],
          [(6, 90), (24, 76), (38, 66), (46, 74), (52, 86), (60, 74), (70, 66), (80, 78), (94, 90)]],
    "を": [[(20, 24), (66, 21)],
          [(44, 8), (36, 30), (26, 50)], [(26, 50), (44, 40), (60, 44), (64, 56)],
          [(82, 50), (56, 62), (38, 74), (34, 86), (46, 94), (78, 93)]],
    "ん": [[(54, 10), (16, 90)],
          [(16, 90), (32, 62), (44, 54), (52, 62), (54, 80), (60, 92), (74, 88), (86, 68)]],
    "ゝ": [[(34, 30), (52, 42), (62, 56), (58, 69), (42, 78)]],
}

KATA = {
    "ア": [[(14, 18), (84, 16)], [(84, 16), (76, 30), (60, 44)],
          [(44, 34), (43, 58), (36, 78), (22, 92)]],
    "イ": [[(72, 8), (52, 32), (14, 58)],
          [(52, 36), (52, 94)]],
    "ウ": [[(50, 6), (50, 22)],
          [(16, 24), (16, 46)],
          [(16, 24), (84, 24)], [(84, 24), (82, 48), (70, 74), (42, 94)]],
    "エ": [[(20, 22), (80, 22)], [(50, 22), (50, 82)], [(10, 82), (90, 82)]],
    "オ": [[(10, 34), (90, 34)],
          [(62, 8), (62, 80), (60, 90), (52, 94)],
          [(56, 38), (36, 66), (12, 84)]],
    "カ": [[(16, 30), (80, 30)], [(80, 30), (79, 70), (74, 88), (64, 94), (56, 88)],
          [(46, 8), (44, 48), (34, 74), (18, 92)]],
    "キ": [[(16, 32), (84, 27)], [(10, 58), (90, 53)], [(44, 8), (56, 94)]],
    "ク": [[(40, 8), (14, 46)],
          [(34, 22), (80, 22)], [(80, 22), (74, 52), (54, 78), (22, 96)]],
    "ケ": [[(36, 8), (12, 48)],
          [(30, 30), (88, 30)],
          [(64, 30), (62, 60), (52, 80), (36, 94)]],
    "コ": [[(18, 20), (80, 20)], [(80, 20), (80, 82)], [(18, 82), (80, 82)]],
    "サ": [[(8, 36), (92, 36)], [(30, 12), (30, 60)], [(70, 10), (70, 56), (62, 78), (40, 94)]],
    "シ": [[(16, 12), (34, 24)], [(10, 38), (28, 50)], [(14, 92), (46, 76), (70, 54), (88, 24)]],
    "ス": [[(20, 18), (76, 18)], [(76, 18), (68, 42), (48, 70), (14, 92)],
          [(52, 58), (86, 90)]],
    "セ": [[(10, 42), (86, 36)], [(86, 36), (70, 56)],
          [(34, 10), (34, 76), (38, 86), (50, 89), (84, 89)]],
    "ソ": [[(22, 20), (34, 46)], [(82, 16), (74, 50), (56, 74), (30, 94)]],
    "タ": [[(40, 8), (14, 46)],
          [(34, 22), (80, 22)], [(80, 22), (74, 52), (54, 78), (22, 96)],
          [(36, 46), (70, 66)]],
    "チ": [[(78, 8), (54, 16), (26, 22)], [(10, 46), (90, 46)], [(52, 18), (52, 64), (44, 84), (26, 96)]],
    "ツ": [[(14, 22), (24, 40)], [(40, 16), (50, 34)], [(84, 18), (76, 52), (58, 76), (32, 94)]],
    "テ": [[(22, 16), (78, 16)], [(10, 44), (90, 44)], [(52, 44), (50, 68), (40, 84), (28, 94)]],
    "ト": [[(36, 6), (36, 94)], [(38, 40), (78, 58)]],
    "ナ": [[(10, 36), (90, 36)], [(56, 8), (56, 58), (48, 80), (26, 94)]],
    "ニ": [[(20, 26), (80, 26)], [(10, 80), (90, 80)]],
    "ヌ": [[(18, 20), (78, 20)], [(78, 20), (66, 50), (44, 76), (12, 94)],
          [(34, 44), (80, 86)]],
    "ネ": [[(44, 2), (56, 12)],
          [(16, 27), (80, 27)], [(80, 27), (56, 51), (10, 77)],
          [(48, 50), (48, 96)],
          [(64, 57), (87, 75)]],
    "ノ": [[(78, 10), (72, 42), (54, 70), (22, 92)]],
    "ハ": [[(36, 26), (28, 56), (12, 84)], [(62, 26), (76, 52), (90, 84)]],
    "ヒ": [[(28, 46), (80, 32)], [(28, 10), (28, 78), (32, 86), (44, 89), (86, 89)]],
    "フ": [[(16, 20), (82, 20)], [(82, 20), (76, 52), (56, 78), (24, 94)]],
    "ヘ": [[(8, 62), (28, 40), (36, 40), (92, 82)]],
    "ホ": [[(10, 34), (90, 34)], [(50, 8), (50, 86), (48, 92), (42, 94)], [(30, 52), (12, 78)], [(70, 52), (88, 78)]],
    "マ": [[(12, 22), (86, 22)], [(86, 22), (70, 46), (50, 62)],
          [(38, 48), (64, 84)]],
    "ミ": [[(28, 14), (70, 24)], [(32, 42), (66, 52)], [(22, 70), (78, 86)]],
    "ム": [[(46, 10), (32, 44), (16, 80)], [(16, 80), (82, 72)],
          [(66, 50), (86, 90)]],
    "メ": [[(76, 10), (60, 52), (14, 92)], [(28, 36), (82, 82)]],
    "モ": [[(18, 20), (82, 20)], [(8, 50), (92, 50)], [(44, 20), (44, 78), (48, 86), (60, 89), (88, 89)]],
    "ヤ": [[(8, 42), (88, 30)], [(88, 30), (76, 52)],
          [(36, 8), (52, 94)]],
    "ユ": [[(18, 28), (74, 28)], [(74, 28), (72, 82)], [(8, 82), (92, 82)]],
    "ヨ": [[(18, 18), (80, 18)], [(24, 50), (80, 50)], [(18, 84), (80, 84)], [(80, 18), (80, 84)]],
    "ラ": [[(20, 14), (80, 14)],
          [(14, 40), (82, 40)], [(82, 40), (76, 64), (58, 84), (28, 96)]],
    "リ": [[(24, 14), (24, 62)], [(74, 8), (74, 54), (66, 78), (40, 96)]],
    "ル": [[(32, 12), (32, 52), (24, 76), (10, 92)],
          [(62, 10), (62, 88)], [(62, 88), (90, 62)]],
    "レ": [[(26, 10), (26, 88)], [(26, 88), (60, 72), (88, 46)]],
    "ロ": [[(16, 18), (16, 86)], [(16, 18), (84, 18)], [(84, 18), (84, 86)], [(16, 86), (84, 86)]],
    "ワ": [[(16, 20), (16, 46)], [(16, 20), (84, 20)], [(84, 20), (80, 52), (62, 78), (38, 94)]],
    "ヰ": [[(10, 30), (90, 30)], [(10, 62), (90, 62)], [(32, 30), (32, 62)], [(66, 8), (66, 94)]],
    "ヱ": [[(16, 22), (80, 22)], [(80, 22), (68, 42)], [(48, 36), (48, 82)], [(10, 82), (90, 82)]],
    "ヲ": [[(16, 20), (82, 20)], [(14, 48), (80, 48)], [(82, 20), (76, 56), (54, 82), (26, 96)]],
    "ン": [[(16, 22), (34, 38)], [(16, 90), (46, 74), (70, 52), (86, 26)]],
    "ヽ": [[(36, 34), (64, 62)]],
    "゠": [[(26, 42), (74, 36)], [(26, 64), (74, 58)]],
}


# Loops that grow with the weight: (stroke, first point, last point, anchor x, anchor y, gain). The anchor sits on the
# side the loop must not move (the baseline, the stem), so a heavier loop grows up and away from it.
LOOPS = {
    "あ": [(2, 3, 6, 52, 46, 0.8)],
    "お": [(1, 3, 6, 44, 50, 0.7)],
    "す": [(1, 2, 7, 57, 54, 1.0)],
    "な": [(3, 2, 7, 60, 93, 1.0)],
    "ぬ": [(1, 11, 13, 82, 84, 1.0)],
    "ね": [(3, 7, 9, 82, 84, 1.0)],
    "は": [(2, 2, 7, 64, 92, 1.0)],
    "ほ": [(3, 2, 7, 61, 92, 1.0)],
    "ま": [(2, 2, 7, 48, 92, 1.0)],
    "み": [(1, 3, 6, 30, 62, 0.8)],
    "む": [(1, 2, 6, 33, 58, 1.0)],
    "よ": [(1, 2, 7, 44, 92, 1.0)],
    "る": [(2, 6, 9, 57, 93, 1.0)],
    "ゐ": [(2, 8, 11, 71, 93, 1.0)],
    "ゑ": [(2, 2, 6, 40, 47, 0.7)],
}

def _unit(v):
    n = math.hypot(v[0], v[1])
    return (v[0] / n, v[1] / n) if n > 1e-9 else (1.0, 0.0)


def smooth_path(points):
    """Path through font-unit points [(x, y, angle_or_None)], angles in font space (degrees, y up)."""
    P = [(float(p[0]), float(p[1])) for p in points]
    n = len(P)
    path = Path(*P[0])
    if n == 2 and points[0][2] is None and points[1][2] is None:
        return path.line(*P[1])
    T = [None] * n
    for i, p in enumerate(points):
        if p[2] is not None:
            a = math.radians(p[2])
            T[i] = (math.cos(a), math.sin(a))
    for i in range(1, n - 1):
        if T[i] is None:
            d0 = _unit((P[i][0] - P[i - 1][0], P[i][1] - P[i - 1][1]))
            d1 = _unit((P[i + 1][0] - P[i][0], P[i + 1][1] - P[i][1]))
            T[i] = _unit((d0[0] + d1[0], d0[1] + d1[1]))

    def mirror(t, c):
        d = t[0] * c[0] + t[1] * c[1]
        return _unit((2 * d * c[0] - t[0], 2 * d * c[1] - t[1]))

    if T[0] is None:
        c = _unit((P[1][0] - P[0][0], P[1][1] - P[0][1]))
        T[0] = mirror(T[1], c) if T[1] is not None else c
    if T[-1] is None:
        c = _unit((P[-1][0] - P[-2][0], P[-1][1] - P[-2][1]))
        T[-1] = mirror(T[-2], c)
    for i in range(n - 1):
        d = math.hypot(P[i + 1][0] - P[i][0], P[i + 1][1] - P[i][1]) * TENSION
        path.curve(P[i][0] + T[i][0] * d, P[i][1] + T[i][1] * d,
                   P[i + 1][0] - T[i + 1][0] * d, P[i + 1][1] - T[i + 1][1] * d, P[i + 1][0], P[i + 1][1])
    return path


class Kana:
    """Glyph factory for one Style."""

    def __init__(self, style):
        self.s = style
        self.g = Glyphs(style)
        plush = style.inflate > 0
        # Kana strokes a little lighter than the Latin stem: more strokes per em, same color on the line.
        self.km = 0.80 if plush else 0.90
        self.w = style.w * self.km
        self.h = self.w / 2
        self.face = KANA_FACE * (1.03 if plush else 1.0)
        self.dot_r = max(self.w * 0.66, self.w * 0.5 + 10)
        # Loops open up as the pen gets heavier, so their counters stay open in Bold and in the puffy Black.
        w_eff = self.w * (1.0 + 0.5 * style.inflate * KANA_INFLATE)
        self.loop_f = max(1.0, w_eff / (92.0 * 0.90)) ** 0.75

    # ---------------- strokes ----------------

    def st(self, path, mult=1.0):
        """The Latin pen (Glyphs.st: swell at the ends, inflate in the middle) at kana weight. Kana strokes are long,
        so the Plush belly is gentler (KANA_INFLATE) to keep the same look as on the short Latin strokes."""
        s = self.s
        fn = None
        if not path.closed and (s.swell > 0 or s.inflate > 0):
            sw, inf = s.swell, s.inflate * KANA_INFLATE

            def fn(t, sw=sw, inf=inf):
                end = max(0.0, 1.0 - min(t, 1.0 - t) * 8.0)
                return 1.0 + sw * end + inf * math.sin(math.pi * t)
        return Stroke(path, self.w * mult, fn)

    def line(self, x0, y0, x1, y1, mult=1.0):
        return self.st(Path(x0, y0).line(x1, y1), mult)

    def to_em(self, x, y, k=None, cx=500.0, cy=CY):
        k = self.face / 100.0 if k is None else k
        return cx + (x - 50.0) * k, cy - (y - 50.0) * k

    def skeleton(self, strokes, scale=1.0, dx=0.0, dy=0.0, mult=1.0, face=None, loops=None, warp=0.0):
        """Design-space strokes -> engine shapes. scale/dx/dy act in design space around the box center.
        warp > 0 pushes points near the top-right corner away from it (room for dakuten and handakuten)."""
        k = (face or self.face) / 100.0 * scale
        if warp > 0:
            strokes = [st if (st and st[0] == "dot") else [_warp(p, warp) for p in st] for st in strokes]
        if loops and self.loop_f > 1.0:
            strokes = [list(st) for st in strokes]
            for si, i0, i1, ax, ay, gain in loops:
                f = 1.0 + (self.loop_f - 1.0) * gain
                for i in range(i0, i1 + 1):
                    p = strokes[si][i]
                    strokes[si][i] = (ax + (p[0] - ax) * f, ay + (p[1] - ay) * f) + tuple(p[2:])
        shapes = []
        for stroke in strokes:
            if stroke and stroke[0] == "dot":
                _, x, y, r = stroke
                X, Y = self.to_em(x + dx, y + dy, k)
                shapes.append(Dot(X, Y, self.dot_r * r))
                continue
            pts = []
            for p in stroke:
                X, Y = self.to_em(p[0] + dx, p[1] + dy, k)
                a = None if len(p) < 3 else -p[2]
                pts.append((X, Y, a))
            shapes.append(self.st(smooth_path(pts), mult))
        return shapes

    # ---------------- kana ----------------

    def base(self, ch):
        if ch in HIRA:
            return self.skeleton(HIRA[ch], loops=LOOPS.get(ch))
        if ch in KATA:
            return self.skeleton(KATA[ch], KATA_SCALE)
        raise KeyError(ch)

    def small(self, ch):
        """Small kana: the full letter scaled down, centered, sitting on the kana baseline."""
        data = HIRA.get(ch) or KATA.get(ch)
        sc = SMALL_SCALE * (KATA_SCALE if ch in KATA else 1.0)
        # Box center stays at x = 50; the bottom (y = 96) stays put.
        dy = (96 - 50) * (1 - sc) / sc
        return self.skeleton(data, sc, dx=-2.0 / sc, dy=dy, mult=0.94, loops=LOOPS.get(ch))

    def marks(self, kind, dx=0.0, dy=0.0):
        """Dakuten (two round dots) or handakuten (a ring) at the top right of the em, inside the advance at every
        weight. dx, dy: design units (y down)."""
        k = self.face / 100.0
        cy = MARK_CY - dy * k
        if kind == "dakuten":
            # Dots grow slower than the pen: heavy weights keep a compact, airy pair.
            r = 47.0 * (self.w / 82.8) ** 0.5
            gap = 30.0 + 0.12 * self.w
            xr = MARK_RIGHT - r + dx * k
            return [Dot(xr - 2 * r - gap, cy, r), Dot(xr, cy, r)]
        # The ring pen grows slower than the letters' pen and the ring opens up with it: the hole stays clear.
        rw = 0.72 * self.w * (82.8 / self.w) ** 0.35
        r = max(65.0, 1.08 * rw)
        cx = MARK_RIGHT - 24 - r - rw / 2 + dx * k
        return [Stroke(Path(cx + r, cy).arc(cx, cy, r, r, 0, 360, 2.0).close(), rw)]

    # ---------------- composition ----------------

    def make(self, ch):
        """Shapes and advance for one encoded character."""
        shapes, adv = self._make(ch)
        if adv and shapes and ch not in "＿":
            shapes = _inside(shapes)
        return shapes, adv

    def _make(self, ch):
        cp = ord(ch)
        if ch in HIRA or ch in KATA:
            return self.base(ch), ADVANCE
        if cp in (0x3099, 0x309A):
            # Combining marks: no advance, the mark sits on the previous em.
            shapes = self.marks("dakuten" if cp == 0x3099 else "handakuten")
            return [_shift_em(sh, -ADVANCE) for sh in shapes], 0
        if cp in (0x309B, 0x309C):
            # Spacing marks: drawn at the upper left of their own em, next to the letter before.
            shapes = self.marks("dakuten" if cp == 0x309B else "handakuten", dx=-78)
            return shapes, ADVANCE
        voiced = _voiced(ch) or {"ゞ": ("ゝ", "dakuten"), "ヾ": ("ヽ", "dakuten")}.get(ch)
        if voiced:
            return self.voiced(*voiced), ADVANCE
        sb = _small_base(ch)
        if sb and (sb in HIRA or sb in KATA):
            return self.small(sb), ADVANCE
        if 0xFF01 <= cp <= 0xFF5E or 0xFFE0 <= cp <= 0xFFE6:
            return self.fullwidth(ch), ADVANCE
        if ch in PUNCT_METHODS:
            return getattr(self, PUNCT_METHODS[ch])(), ADVANCE
        raise KeyError(f"no kana glyph for {ch!r} (U+{cp:04X})")

    def voiced(self, base, kind):
        """Letter plus dakuten or handakuten. If a mark would touch the letter, the mark climbs a little first,
        then the letter gives way (smaller, towards the lower left)."""
        shapes, marks = [], []
        self.last_fit = None
        for i, (sc, dx, dy, warp, mdy) in enumerate(VOICE_FITS):
            shapes = self._fit(base, sc, dx, dy, warp)
            marks = self.marks(kind, 0.0, mdy)
            if not (_mask(glyph_contours(shapes), grow=MARK_CLEAR + 0.16 * self.w) & _mask(glyph_contours(marks))).any():
                self.last_fit = i
                break
        return shapes + marks

    def _fit(self, base, sc, dx, dy, warp=0.0):
        if base in HIRA:
            return self.skeleton(HIRA[base], sc, dx=dx, dy=dy, loops=LOOPS.get(base), warp=warp)
        return self.skeleton(KATA[base], KATA_SCALE * sc, dx=dx, dy=dy, warp=warp)

    # ---------------- CJK symbols and punctuation (em coordinates) ----------------

    def kanji(self, strokes, mult=1.0):
        """Ideograph-sized skeleton (FACE), for 々 〆 〒."""
        return self.skeleton(strokes, face=FACE * 0.94, mult=mult)

    def comma_jp(self, cx=232.0, cy=88.0):
        """、: a soft tick from the upper left, fuller at its end."""
        a = (cx - 46, cy + 48)
        b = (cx + 38, cy - 40)
        mid = ((a[0] + b[0]) / 2 + 6, (a[1] + b[1]) / 2 + 6)
        p = smooth_path([(a[0], a[1], None), (mid[0], mid[1], None), (b[0], b[1], None)])
        return [Stroke(p, self.w * 1.12, lambda t: 0.82 + 0.30 * t)]

    def period_jp(self, cx=226.0, cy=86.0):
        """。: an open ring."""
        r = 76 + self.w * 0.10
        return [self.st(Path(cx + r, cy).arc(cx, cy, r, r, 0, 360, 2.0).close(), 0.72)]

    def p_ideocomma(self):
        return self.comma_jp()

    def p_ideoperiod(self):
        return self.period_jp()

    def p_ditto(self):
        """〃: two tall soft strokes leaning right, side by side."""
        out = []
        for x in (420.0, 600.0):
            k = [(x + 70, CY + 240, 248), (x + 30, CY + 60, 240), (x - 50, CY - 130, 222)]
            out.append(self.st(Path(k[0][0], k[0][1]).spline(k, 0.40), 0.95))
        return out

    def p_iteration(self):
        """々: ideographic iteration mark (a short sweep, a hooked bar, a long dot to the lower right)."""
        return self.kanji([[(44, 6), (30, 28), (14, 46)],
                           [(32, 26), (72, 26)], [(72, 26), (56, 48), (34, 66)],
                           [(32, 46), (50, 64), (68, 92)]])

    def p_closing(self):
        """〆: closing mark (a stroke rising on the left and falling to the right, crossed by a long rising one)."""
        return self.kanji([[(16, 86), (16, 58), (20, 38), (28, 30), (38, 38), (60, 72)],
                           [(18, 94), (48, 56), (84, 8)]])

    def p_zero(self):
        """〇: ideographic zero, as tall as the ideographs."""
        r = FACE * 0.47
        return [self.g.bowl(500 - r, 500 + r, CY - r, CY + r, 2.0, mult=self.km)]

    def p_postal(self):
        """〒: postal mark."""
        return self.kanji([[(14, 16), (86, 16)], [(14, 42), (86, 42)], [(50, 42), (50, 94)]])

    def p_wave(self):
        """〜 and ～: one soft wave across the em."""
        y = CY
        a = 52
        k = [(110, y - a * 0.45, 62), (300, y + a, 0), (700, y - a, 0), (890, y + a * 0.45, 62)]
        return [self.st(Path(k[0][0], k[0][1]).spline(k, 0.42))]

    def p_middledot(self):
        return [Dot(500, CY, self.dot_r * 1.18)]

    def p_prolong(self):
        """ー: the long vowel mark, across the kana face."""
        return self.skeleton([[(5, 50), (95, 50)]])

    # Brackets: opening ones sit in the right half of the em, closing ones in the left half (180 degrees about the
    # em center), so the space falls outside the bracketed text.
    BT, BB = 832.0, -72.0     # bracket top and bottom (ink)

    def _flip(self, shapes):
        return [_rot180(sh) for sh in shapes]

    def b_corner(self):
        h = self.h * 0.95
        x, top = 568.0, self.BT - h
        return [self.line(x, top, x, CORNER_FOOT, 0.95), self.line(x, top, 890, top, 0.95)]

    def b_whitecorner(self):
        m = 0.82
        h = self.h * m
        x0, top = 548.0 + h, self.BT - h
        x1, y1 = x0 + 122 + self.w * 0.55, top - 122 - self.w * 0.55
        x2, y2 = 900.0 - h, CORNER_FOOT
        pts = [(x0, top), (x2, top), (x2, y1), (x1, y1), (x1, y2), (x0, y2)]
        return [self.line(*pts[i], *pts[(i + 1) % 6], m) for i in range(6)]

    def b_angle(self, x0=600.0, x1=840.0, mult=0.95):
        h = self.h * mult
        top, bot = self.BT - h - 20, self.BB + h + 20
        return [self.line(x1, top, x0, CY, mult), self.line(x0, CY, x1, bot, mult)]

    def b_dblangle(self):
        return self.b_angle(560, 790, 0.9) + self.b_angle(700, 930, 0.9)

    def b_lenticular(self, white=False):
        """【 (black) and 〖 (white): a flat outer side, a hollow inner side, arms tapering to the right."""
        r = self.h * 0.8
        x0 = 604.0 + r
        x1 = 890.0 - r
        top, bot = self.BT - r, self.BB + r
        inner = []
        for i in range(25):
            t = i / 24
            y = top + (bot - top) * t
            depth = math.sin(math.pi * t) ** 0.85
            inner.append((x1 - (x1 - x0 - 40) * depth * 0.78, y))
        pts = [(x0, top)] + inner + [(x0, bot)]
        if not white:
            return self.g.fill(pts, r)
        m = 0.70
        return [self.line(*pts[i], *pts[(i + 1) % len(pts)], m) for i in range(len(pts))]

    def b_tortoise(self):
        """〔: angled ends and a straight back."""
        m = 0.95
        h = self.h * m
        xb, xe = 640.0, 880.0
        top, bot = self.BT - h, self.BB + h
        a, b, c, d = (xe, top), (xb, top - 110), (xb, bot + 110), (xe, bot)
        return [self.line(*a, *b, m), self.line(*b, *c, m), self.line(*c, *d, m)]

    def p_corner_l(self):
        return self.b_corner()

    def p_corner_r(self):
        return self._flip(self.b_corner())

    def p_wcorner_l(self):
        return self.b_whitecorner()

    def p_wcorner_r(self):
        return self._flip(self.b_whitecorner())

    def p_angle_l(self):
        return self.b_angle()

    def p_angle_r(self):
        return self._flip(self.b_angle())

    def p_dblangle_l(self):
        return self.b_dblangle()

    def p_dblangle_r(self):
        return self._flip(self.b_dblangle())

    def p_lent_l(self):
        return self.b_lenticular()

    def p_lent_r(self):
        return self._flip(self.b_lenticular())

    def p_wlent_l(self):
        return self.b_lenticular(True)

    def p_wlent_r(self):
        return self._flip(self.b_lenticular(True))

    def p_tort_l(self):
        return self.b_tortoise()

    def p_tort_r(self):
        return self._flip(self.b_tortoise())

    def p_space(self):
        return []

    # ---------------- full-width forms ----------------

    def latin(self, ch):
        shapes, _, _, _, _ = self.g.make(ch)
        return shapes

    def centered(self, shapes, cx=500.0, max_w=860.0, dy=0.0):
        x0, _, x1, _ = bounds(glyph_contours(shapes))
        w = x1 - x0
        if w > max_w:
            # Condense wide letters (W, M, %) without thinning the strokes.
            sx = max_w / w
            shapes = [_map_points(sh, lambda x, y, sx=sx, x0=x0: (x0 + (x - x0) * sx, y)) for sh in shapes]
            x0, _, x1, _ = bounds(glyph_contours(shapes))
        return [_shift_em(sh, cx - (x0 + x1) / 2, dy) for sh in shapes]

    def fw_paren(self):
        """（: a tall soft arc in the right half."""
        m = 0.95
        h = self.h * m
        top, bot = self.BT - h, self.BB + h
        k = [(860, top, 214), (650, CY, 270), (860, bot, 326)]
        return [self.st(Path(k[0][0], k[0][1]).spline(k, 0.40), m)]

    def fw_bracket(self):
        """［"""
        m = 0.95
        h = self.h * m
        x, xe = 662.0, 872.0
        top, bot = self.BT - h, self.BB + h
        return [self.line(xe, top, x, top, m), self.line(x, top, x, bot, m), self.line(x, bot, xe, bot, m)]

    def fw_brace(self):
        """｛: the Latin brace, raised onto the ideographic center."""
        shapes = self.latin("{")
        x0, y0, x1, y1 = bounds(glyph_contours(shapes))
        return [_shift_em(sh, 0, CY - (y0 + y1) / 2) for sh in shapes]

    def fullwidth(self, ch):
        cp = ord(ch)
        if cp >= 0xFFE0:
            return self.centered(self.latin({0xFFE0: "¢", 0xFFE1: "£", 0xFFE5: "¥", 0xFFE6: "₩"}[cp]))
        a = chr(cp - 0xFEE0)
        if a in "([{":
            shapes = {"(": self.fw_paren, "[": self.fw_bracket, "{": self.fw_brace}[a]()
            return self.centered(shapes, cx=OPEN_CX) if a == "{" else shapes
        if a in ")]}":
            opening = {")": "(", "]": "[", "}": "{"}[a]
            return self._flip(self.fullwidth(chr(ord(opening) + 0xFEE0)))
        if a in ",.":
            return self.centered(self.latin(a), cx=LOW_CX)
        if a == "~":
            return self.p_wave()
        if a == "_":
            h = self.g.h
            return [self.g.line(h, -110, ADVANCE - h, -110)]
        if a == "-":
            return self.centered(self.latin("−"))
        return self.centered(self.latin(a))

    # ---------------- regional variants (unencoded) ----------------

    def extra(self, name):
        region = name.split(".")[1]
        ch = EXTRA_BASE[name]
        if region == "zht":
            # Traditional Chinese (Taiwan, Hong Kong): the mark in the middle of the em.
            shapes, _ = self.make(ch)
            x0, y0, x1, y1 = bounds(glyph_contours(shapes))
            return [_shift_em(sh, 500 - (x0 + x1) / 2, CY - (y0 + y1) / 2) for sh in shapes]
        if region == "zhs":
            # Simplified Chinese: ！？：； in the left half.
            return self.centered(self.latin(chr(ord(ch) - 0xFEE0)), cx=SC_LEFT_CX)
        # Full-width curly quotes for Chinese: opening ones hug the text on their right, closing ones on their left.
        shapes = self.latin(ch)
        x0, y0, x1, y1 = bounds(glyph_contours(shapes))
        cx = 720.0 if ch in "“‘" else 280.0
        return [_shift_em(sh, cx - (x0 + x1) / 2, QUOTE_TOP - y1) for sh in shapes]


OPEN_CX = 700.0       # ink center of ｛ (opening brackets hug the text that follows)
CORNER_FOOT = 120.0   # lower end of 「 and 『 (long arms, as Japanese corner brackets are)
LOW_CX = 232.0        # ， ． in the lower left (Japanese, Simplified Chinese)
SC_LEFT_CX = 268.0    # ！？：； in Simplified Chinese
QUOTE_TOP = 812.0     # top of the full-width curly quotes

# (letter scale, letter dx, letter dy, mark dx, mark dy) in design units, tried in order.
# (letter scale, letter dx, letter dy, corner warp, mark dy) in design units, tried in order: as drawn, the mark a
# little higher, the letter's corner making room, then the letter a little smaller towards the lower left.
VOICE_FITS = [(1.0, 0, 0, 0, 0), (1.0, 0, 0, 0, -2), (1.0, 0, 0, 5, -2), (1.0, 0, 0, 9, -2),
              (0.97, -1.5, 1.5, 9, -2), (0.95, -2.5, 2.5, 11, -2), (0.93, -3.5, 3.5, 13, -2),
              (0.91, -5, 4.5, 14, -2), (0.89, -6.5, 5.5, 15, -2), (0.87, -8, 6.5, 16, -2)]
WARP_CENTER = (93.0, 3.0)   # design-space center of the room made for the marks
WARP_SIGMA = 19.0
MARK_RIGHT = 984.0    # right edge of the dakuten ink (inside the advance)
MARK_CY = 733.0       # height of the dakuten and handakuten centers
INK_MARGIN = 14.0     # kana ink stays this far inside the em
MARK_CLEAR = 30.0     # base clearance between a mark and its letter (plus 0.16 x the pen)

PUNCT_METHODS = {
    "　": "p_space", "、": "p_ideocomma", "。": "p_ideoperiod", "〃": "p_ditto", "々": "p_iteration",
    "〆": "p_closing", "〇": "p_zero", "〈": "p_angle_l", "〉": "p_angle_r", "《": "p_dblangle_l", "》": "p_dblangle_r",
    "「": "p_corner_l", "」": "p_corner_r", "『": "p_wcorner_l", "』": "p_wcorner_r", "【": "p_lent_l", "】": "p_lent_r",
    "〒": "p_postal", "〔": "p_tort_l", "〕": "p_tort_r", "〖": "p_wlent_l", "〗": "p_wlent_r", "〜": "p_wave",
    "・": "p_middledot", "ー": "p_prolong",
}

EXTRA_BASE = {
    "uni3001.zht": "、", "uni3002.zht": "。", "uniFF0C.zht": "，", "uniFF0E.zht": "．",
    "uniFF01.zhs": "！", "uniFF1A.zhs": "：", "uniFF1B.zhs": "；", "uniFF1F.zhs": "？",
    "quotedblleft.full": "“", "quotedblright.full": "”", "quoteleft.full": "‘", "quoteright.full": "’",
}


def _warp(p, amount):
    """Push a design point away from WARP_CENTER, strongest near it (Gaussian falloff)."""
    mx, my = WARP_CENTER
    vx, vy = p[0] - mx, p[1] - my
    d2 = vx * vx + vy * vy
    f = amount * math.exp(-d2 / (2 * WARP_SIGMA ** 2))
    n = math.sqrt(d2) or 1.0
    return (p[0] + vx / n * f, p[1] + vy / n * f) + tuple(p[2:])


def _inside(shapes):
    """Shift the ink horizontally so it stays INK_MARGIN inside the em (heavy loops, big dots)."""
    x0, _, x1, _ = bounds(glyph_contours(shapes))
    lo, hi = INK_MARGIN, ADVANCE - INK_MARGIN
    if x0 < lo and x1 < hi:
        return [_shift_em(sh, min(lo - x0, hi - x1)) for sh in shapes]
    if x1 > hi and x0 > lo:
        return [_shift_em(sh, -min(x1 - hi, x0 - lo)) for sh in shapes]
    return shapes


def _shift_em(shape, dx, dy=0.0):
    return _map_points(shape, lambda x, y: (x + dx, y + dy))


def _rot180(shape):
    return _map_points(shape, lambda x, y: (ADVANCE - x, 2 * CY - y))


def _mask(contours, scale=0.125, grow=0.0):
    """Union mask of contours on a fixed em grid (x -1200..1200, y -400..1200)."""
    from PIL import Image, ImageDraw, ImageFilter
    W, H = int(2400 * scale), int(1600 * scale)
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    for c in contours:
        pts = [((x + 1200) * scale, (1200 - y) * scale) for x, y in c]
        if len(pts) >= 3:
            d.polygon(pts, fill=255)
    if grow > 0:
        n = max(3, int(grow * scale) * 2 + 1)
        img = img.filter(ImageFilter.MaxFilter(n))
    return np.array(img) > 0


# ---------------------------------------------------------------------------------------------------------------
# Character sets
# ---------------------------------------------------------------------------------------------------------------

def _small_base(ch):
    """'HIRAGANA LETTER SMALL A' -> 'あ'."""
    name = unicodedata.name(ch)
    if " SMALL " not in name:
        return None
    try:
        return unicodedata.lookup(name.replace(" SMALL ", " "))
    except KeyError:
        return None


def _voiced(ch):
    """(base, 'dakuten'|'handakuten') from the canonical decomposition, or None."""
    d = unicodedata.normalize("NFD", ch)
    if len(d) == 2 and d[1] in "゙゚":
        return d[0], ("dakuten" if d[1] == "゙" else "handakuten")
    return None


HIRAGANA = [chr(c) for c in range(0x3041, 0x3097)] + ["゙", "゚", "゛", "゜", "ゝ", "ゞ"]
KATAKANA = [chr(c) for c in range(0x30A0, 0x30FF)]
KATAKANA_EXT = [chr(c) for c in range(0x31F0, 0x3200)]
CJK_PUNCT = list("　、。〃々〆〇〈〉《》「」『』【】〒〔〕〖〗〜")
FULLWIDTH = [chr(c) for c in range(0xFF01, 0xFF5F)] + list("￠￡￥￦")


def charset():
    out = []
    for c in HIRAGANA + KATAKANA + KATAKANA_EXT + CJK_PUNCT + FULLWIDTH:
        if c not in out:
            out.append(c)
    return out


# ---------------------------------------------------------------------------------------------------------------
# Module API
# ---------------------------------------------------------------------------------------------------------------

_CACHE = {}


def _factory(style):
    key = repr(style)
    k = _CACHE.get(key)
    if k is None:
        k = _CACHE[key] = Kana(style)
    return k


def _side_bearing(style):
    """pw_font_build.side_bearing(style, 'P', False)."""
    return _SB_P * (1.0 + (style.w - 88.0) * 0.0018) + style.tracking


def _tuple(shapes, adv, style):
    """Glyphs.make tuple whose spacing (build_style: lsb + (x1 - x0) + rsb) gives exactly `adv` and no shift."""
    sb = _side_bearing(style)
    if adv == 0:
        # Zero-width mark: x1 - x0 = -2 sb gives advance 0, x0 = lsb keeps the ink in place (one em to the left).
        return shapes, "P", "P", False, (sb, -sb)
    return shapes, "P", "P", False, (sb, adv - sb)


def make(ch, style):
    shapes, adv = _factory(style).make(ch)
    return _tuple(shapes, adv, style)


def advance(ch, style=None):
    return 0 if ord(ch) in (0x3099, 0x309A) else ADVANCE


def extra_glyphs():
    return list(EXTRA_BASE.keys())


def make_extra(name, style):
    return _tuple(_factory(style).extra(name), ADVANCE, style)


REGION_LOOKUPS = {
    "zhs": ["uniFF01.zhs", "uniFF1A.zhs", "uniFF1B.zhs", "uniFF1F.zhs",
            "quotedblleft.full", "quotedblright.full", "quoteleft.full", "quoteright.full"],
    "zht": ["uni3001.zht", "uni3002.zht", "uniFF0C.zht", "uniFF0E.zht",
            "quotedblleft.full", "quotedblright.full", "quoteleft.full", "quoteright.full"],
}
REGIONS = {"zh-Hans": "zhs", "zh-CN": "zhs", "zh-SG": "zhs",
           "zh-Hant": "zht", "zh-TW": "zht", "zh-HK": "zht", "zh-MO": "zht"}

LANGUAGESYSTEMS = [("DFLT", "dflt"), ("latn", "dflt"), ("kana", "dflt"), ("kana", "JAN"),
                   ("hani", "dflt"), ("hani", "ZHS"), ("hani", "ZHT"), ("hani", "ZHH"), ("hani", "ZHTM")]


def _base_name(extra):
    return glyph_name(EXTRA_BASE[extra])


def features(style=None):
    """locl for Chinese punctuation (feaLib). Declare LANGUAGESYSTEMS first (languagesystem_code())."""
    out = []
    for key, names in REGION_LOOKUPS.items():
        subs = "".join(f"  sub {_base_name(n)} by {n};\n" for n in names)
        out.append(f"lookup locl_{key} {{\n{subs}}} locl_{key};\n")
    out.append("feature locl {\n  script hani;\n"
               "    language ZHS exclude_dflt; lookup locl_zhs;\n"
               "    language ZHT exclude_dflt; lookup locl_zht;\n"
               "    language ZHH exclude_dflt; lookup locl_zht;\n"
               "    language ZHTM exclude_dflt; lookup locl_zht;\n"
               "} locl;\n")
    return "".join(out)


def languagesystem_code():
    return "".join(f"languagesystem {s} {l};\n" for s, l in LANGUAGESYSTEMS)


def region_cmap(region):
    """{code point: glyph name} to override in a per-region font (TextMeshPro has no locl)."""
    key = REGIONS.get(region)
    if not key:
        return {}
    return {ord(EXTRA_BASE[n]): n for n in REGION_LOOKUPS[key]}


# Line breaking (JIS X 4051 kinsoku), for TMP_Settings: characters that may not start a line ("Leading Characters")
# and characters that may not end one ("Following Characters"). Latin marks included so mixed text breaks alike.
LINE_START_FORBIDDEN = (
    "!%),.:;?]}¢°’”‰′″℃›»"
    "、。，．・：；？！゛゜ヽヾゝゞ々〃〻ー"
    "ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ"
    "ㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ"
    "）］｝〕〉》」』】〙〗〟｠"
    "‐゠–〜～…‥％￠"
)
LINE_END_FORBIDDEN = (
    "([{£¥$€#‘“‹«"
    "（［｛〔〈《「『【〘〖〝｟"
    "＄￡￥＃"
)
