"""Pocket Wilds UI sprites added in T44C (Journal du refuge et visiteurs rares; doc 01 Journal, doc 05 sheet 25).

Same method as pw_ui_sprites_t44b.py (signed distances rendered at 4x, box-filtered down), same family: rounded filled
shapes, one warm outline, light from the top left, a soft drop shadow. Owner rules: round dots, five-point stars (never four
points), hearts, paws and pearls; nothing religious or national; handmade only.

- Art/UI/Icons/ICO_Journal (128 x 128, the HUD button): a small scrapbook, coral cloth cover with a stitched spine, a cream
  page with a paw photo and a pink photo corner.
- Art/UI/Journal/UI_Visitor_Badge (96 x 96): a small round tag with a cream heart and a short tail pointing down, set above
  a visitor's head.
- Art/UI/Journal/UI_Journal_PageCard (256 x 320, Sliced, border 48 px on every side): a soft paper card with a strip of tape
  across its top left corner (the tape sits inside the border, the middle stretches).
- Art/UI/Journal/UI_Journal_MonthTab (192 x 64, Sliced, border 28 / 24 / 28 / 24): a rounded month label, peach, a pearl at
  its left end.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t44c.py) to write only these, or through pw_ui_sprites.py (main).
"""
import os

import numpy as np

from pw_ui_sprites import (ROOT, Canvas, MASTERS, RUNTIME_ICONS, fill, hexrgb, sd_circle, sd_polygon, sd_round_rect)
from pw_ui_sprites_t34a import sd_heart, sd_paw
from pw_ui_sprites_t35a import drop_shadow
from pw_ui_sprites_t36 import View, vgrad
from pw_ui_sprites_t37 import lower_shade, top_rim, union
from pw_ui_sprites_t44 import dots, family, piece

RUNTIME_JOURNAL = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Journal")

CREAM = ("FFFDF4", "F2E4C6", "C9B58F")      # paper: top, bottom, outline
CORAL = ("FFB0A0", "E5766A", "8A2E2E")      # cloth: top, bottom, outline
PINK = ("FFC9D6", "F08AA8", "A8456A")       # photo corner and tag: top, bottom, outline


def icon_journal():
    """ICO_Journal: a coral scrapbook with a stitched spine; on the cream page a paw photo held by a pink corner."""
    c = Canvas(128, 128, cy=62.0)
    cover = union(sd_round_rect(c, 40.0, 48.0, 10.0, 0.0, 0.0))
    spine = sd_round_rect(c, 9.0, 48.0, 8.0, -31.0, 0.0)
    family(c, [cover])
    piece(c, cover, CORAL[0], CORAL[1], CORAL[2], 48.0, -48.0, lw=2.4, shade="C9534A", rim="FFE3DC")
    piece(c, spine, "F08A7C", "C9483E", CORAL[2], 48.0, -48.0, lw=2.0, shade="A8383A")
    dots(c, [(-31.0, y) for y in (-33.0, -21.0, -9.0, 3.0, 15.0, 27.0, 39.0)], 1.9, "FFE9DD", 0.95)
    page = sd_round_rect(c, 25.0, 34.0, 6.0, 8.0, 0.0)
    piece(c, page, CREAM[0], CREAM[1], CREAM[2], 34.0, -34.0, lw=1.8, shade="E3D2AE")
    photo = sd_round_rect(c, 17.0, 17.0, 4.0, 8.0, -2.0)
    piece(c, photo, "E8F8D4", "B6DE90", "4F8A3F", 15.0, -19.0, lw=1.6)
    c.over(hexrgb("FFFFFF"), fill(sd_paw(c, 1.5, 8.0, -3.0) + 1.2) * 0.5)
    c.over(hexrgb("8A5A32"), fill(sd_paw(c, 1.5, 8.0, -3.0)))
    corner = sd_polygon(c, [(-9.0, 15.0), (-9.0, 25.0), (1.0, 15.0)]) - 1.2
    piece(c, corner, PINK[0], PINK[1], PINK[2], 25.0, 15.0, lw=1.3)
    corner2 = sd_polygon(c, [(25.0, -19.0), (25.0, -9.0), (15.0, -19.0)]) - 1.2
    piece(c, corner2, PINK[0], PINK[1], PINK[2], -9.0, -19.0, lw=1.3)
    return "ICO_Journal", c, RUNTIME_ICONS


def visitor_badge():
    """UI_Visitor_Badge: a round pink tag with a cream heart, a short rounded tail pointing at the visitor's head."""
    c = Canvas(96, 96, cy=44.0)
    disc = sd_circle(c, 33.0, 0.0, 8.0)
    tail = sd_polygon(c, [(-9.0, -14.0), (9.0, -14.0), (0.0, -34.0)]) - 3.0
    body = union(disc, tail)
    drop_shadow(c, body, dy=3.0, radius=3.0, strength=0.3)
    piece(c, body, PINK[0], PINK[1], PINK[2], 41.0, -34.0, lw=2.8, shade="C9587E", rim="FFF0F4", gl=(14.0, 5.0, -12.0, 26.0, 35.0))
    inner = sd_circle(c, 24.0, 0.0, 8.0)
    c.over(hexrgb("FFF8F0"), fill(np.abs(inner) - 1.3) * 0.7)
    heart = sd_heart(c, 30.0, 0.0, 7.0)
    c.over(hexrgb("A8456A"), fill(heart + 1.6) * 0.5)
    c.over(vgrad(c, "FFFFFF", "FFE9EE", 22.0, -8.0), fill(heart))
    return "UI_Visitor_Badge", c, RUNTIME_JOURNAL


def page_card():
    """UI_Journal_PageCard: a soft paper card, a faint warm edge, a strip of translucent tape on its top left corner."""
    c = Canvas(256, 320)
    card = sd_round_rect(c, 116.0, 148.0, 18.0)
    drop_shadow(c, card, dy=4.0, radius=4.0, strength=0.3)
    piece(c, card, CREAM[0], CREAM[1], CREAM[2], 148.0, -148.0, lw=2.4, rim="FFFFFF")
    c.over(hexrgb("E8D6B0"), fill(np.abs(card + 9.0) - 0.9) * 0.45)
    v = View(c, -102.0, 135.0, 45.0)
    tape = sd_round_rect(v, 20.0, 9.0, 3.0)
    c.over(hexrgb("B89A5E"), fill(tape + 1.4) * 0.35)
    c.over(hexrgb("FFE9B0"), fill(tape) * 0.88)
    c.over(hexrgb("FFF6D6"), fill(np.abs(tape + 3.5) - 0.7) * 0.7)
    return "UI_Journal_PageCard", c, RUNTIME_JOURNAL


def month_tab():
    """UI_Journal_MonthTab: a rounded peach label with a cream pearl at its left end; the text sits in the middle."""
    c = Canvas(192, 64)
    tab = sd_round_rect(c, 88.0, 26.0, 22.0)
    drop_shadow(c, tab, dy=2.5, radius=2.5, strength=0.28)
    piece(c, tab, "FFE6CC", "F5BE96", "A8622E", 26.0, -26.0, lw=2.4, shade="D9955E", rim="FFF4E6")
    dots(c, [(-74.0, 0.0)], 4.2, "FFF8EE", 1.0)
    return "UI_Journal_MonthTab", c, RUNTIME_JOURNAL


T44C_SPRITES = (icon_journal, visitor_badge, page_card, month_tab)


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_JOURNAL, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T44C_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
