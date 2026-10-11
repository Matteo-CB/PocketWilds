"""Pocket Wilds UI sprites added in T48 (the events framework: the event hub, the HUD rail button, the shop's « Événement »
section, the end summary and the Memory Shop).

Same method as pw_ui_sprites_t36.py (signed distances rendered at 4x, box-filtered down), same family as the HUD icons:
rounded filled shapes, one warm outline per icon, light from the top left (a vertical gradient, one deeper shade, one gloss),
a soft warm drop shadow below. Owner rules: round dots, five-point stars (never four points), hearts, paws and pearls;
nothing religious; handmade only (no generator, no library art).

Icons (128 x 128, Art/UI/Icons, readable at 24 and 48 dp):
- ICO_Event: the HUD rail button of an event, a little coral pennant with a cream paw on a wooden pole topped by a gold pearl.
- ICO_Event_GoldBook: the Carnet d'or, a golden notebook with an embossed paw, a coral elastic band and cream page edges.
- ICO_Event_Box: the Coffret, a round wooden box with a golden band and a little golden heart clasp, a gold pearl on its lid.
- ICO_Event_Currency: the generic event token (each event gets its own later), a round pearl coin with a rose paw and a ring
  of tiny pearls.
- ICO_EventEgg_Odds: the odds button of the event egg, an « i » in a round sky badge.
- ICO_Souvenir: the dated Souvenir, a small wooden frame holding a meadow and a sun, a pink heart on top and a cream date tag.
- ICO_Quest: an event quest, a cream scroll between two wooden rolls with a plump green check.
- ICO_Event_Outfit: an outfit reward, a mint neck scarf with a round knot and cream pearls.
- ICO_Event_Points: the Carnet d'or's bonus points, the project's gold five-point star with a green up-arrow badge.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t48.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, fill, hexrgb, sd_circle, sd_ellipse, sd_polygon, sd_round_rect)
from pw_ui_sprites_t34a import sd_heart, sd_paw
from pw_ui_sprites_t36 import View, sd_rounded_star, vgrad
from pw_ui_sprites_t37 import union
from pw_ui_sprites_t44 import GOLD, dots, family, gold_star, piece

CREAM = ("FFFDF4", "F2E4C6", "C9B58F")      # paper: top, bottom, outline
CORAL = ("FFB7A3", "EE7A66", "8A2E2E")      # cloth: top, bottom, outline
WOOD = ("EDC48C", "B97B42", "5E3F24")       # wood: top, bottom, outline
PEARL = ("FFF8F4", "F2C8D2", "A86478")      # pearl coin: top, bottom, outline
SKY = ("D6F1FF", "7FC0EA", "3F79A8")        # info badge: top, bottom, outline
MINT = ("D4F5E0", "6CC79A", "2F7A5A")       # scarf: top, bottom, outline
GREEN = ("C8F0A0", "62B04C", "2F6A2A")      # check and arrow badge: top, bottom, outline


def icon_event():
    """ICO_Event: a coral swallowtail pennant with a cream paw, on a wooden pole topped by a gold pearl."""
    c = Canvas(128, 128, cy=64.0)
    pole = sd_round_rect(c, 4.0, 50.0, 4.0, -36.0, -6.0)
    knob = sd_circle(c, 8.0, -36.0, 46.0)
    flag = sd_polygon(c, [(-28.0, 38.0), (42.0, 38.0), (30.0, 15.0), (42.0, -8.0), (-28.0, -8.0)]) - 4.0
    family(c, [pole, knob, flag])
    piece(c, pole, WOOD[0], WOOD[1], WOOD[2], 44.0, -56.0, lw=2.0, shade="9A6434")
    piece(c, flag, CORAL[0], CORAL[1], CORAL[2], 42.0, -12.0, lw=2.4, shade="C9534A", rim="FFE3DC", gl=(14.0, 5.0, -10.0, 32.0, 8.0))
    piece(c, knob, GOLD[0], GOLD[1], GOLD[2], 54.0, 38.0, lw=2.0, shade=GOLD[3], gl=(3.0, 2.0, -38.0, 49.0, 35.0))
    paw = sd_paw(c, 1.05, 4.0, 14.0)
    c.over(hexrgb("A8463E"), fill(paw + 1.4) * 0.45)
    c.over(vgrad(c, "FFFFFF", "FFE9DE", 30.0, 0.0), fill(paw))
    dots(c, [(-16.0, -2.0), (-2.0, -2.0), (12.0, -2.0)], 2.0, "FFE9DE", 0.9)
    return "ICO_Event", c, RUNTIME_ICONS


def icon_gold_book():
    """ICO_Event_GoldBook: a golden notebook, an embossed paw, a coral elastic band, cream page edges at its right and foot."""
    c = Canvas(128, 128, cy=62.0)
    pages = sd_round_rect(c, 37.0, 45.0, 9.0, 7.0, -4.0)
    cover = sd_round_rect(c, 37.0, 46.0, 10.0, 1.0, 1.0)
    spine = sd_round_rect(c, 9.0, 46.0, 8.0, -32.0, 1.0)
    family(c, [pages, cover, spine])
    piece(c, pages, CREAM[0], CREAM[1], CREAM[2], 40.0, -50.0, lw=2.0, shade="E3D2AE")
    c.over(hexrgb("D9C79F"), fill(np.abs(sd_round_rect(c, 33.0, 41.0, 7.0, 9.0, -6.0)) - 0.7) * 0.6)
    piece(c, cover, GOLD[0], GOLD[1], GOLD[2], 47.0, -45.0, lw=2.4, shade=GOLD[3], rim="FFF8D6", gl=(16.0, 6.0, -4.0, 32.0, 25.0))
    piece(c, spine, "F7C860", "D98A2A", GOLD[2], 47.0, -45.0, lw=2.0, shade="B86E22")
    dots(c, [(-32.0, y) for y in (-33.0, -19.0, -5.0, 9.0, 23.0, 37.0)], 2.0, "FFF4CC", 0.95)
    band = sd_round_rect(c, 3.6, 46.0, 2.0, 28.0, 1.0)
    piece(c, band, CORAL[0], CORAL[1], CORAL[2], 47.0, -45.0, lw=1.4)
    paw = sd_paw(c, 1.2, 0.0, 6.0)
    c.over(hexrgb("B8701E"), fill(paw + 0.6) * 0.75)
    c.over(vgrad(c, "FFF6C8", "F7C860", 24.0, -12.0), fill(paw - 1.2))
    return "ICO_Event_GoldBook", c, RUNTIME_ICONS


def icon_box():
    """ICO_Event_Box: a round wooden box with a golden band and a golden heart clasp, a gold pearl on the lid."""
    c = Canvas(128, 128, cy=60.0)
    body = sd_round_rect(c, 44.0, 28.0, 18.0, 0.0, -18.0)
    lid = sd_round_rect(c, 48.0, 12.0, 12.0, 0.0, 16.0)
    knob = sd_circle(c, 8.0, 0.0, 34.0)
    family(c, [body, lid, knob])
    piece(c, body, WOOD[0], WOOD[1], WOOD[2], 10.0, -46.0, lw=2.4, shade="9A6434", rim="FFE2B8")
    for x in (-24.0, 0.0, 24.0):
        c.over(hexrgb("A86E3C"), fill(np.maximum(sd_round_rect(c, 1.0, 24.0, 1.0, x, -20.0), body + 4.0)) * 0.35)
    band = np.maximum(sd_round_rect(c, 60.0, 6.5, 2.0, 0.0, -10.0), body)
    piece(c, band, GOLD[0], GOLD[1], GOLD[2], -3.0, -17.0, lw=1.6, shade=GOLD[3])
    piece(c, lid, "F2CF9A", "C48A4E", WOOD[2], 28.0, 4.0, lw=2.4, shade="A86E3C", rim="FFF0D6", gl=(16.0, 4.0, -14.0, 22.0, 6.0))
    piece(c, knob, GOLD[0], GOLD[1], GOLD[2], 42.0, 26.0, lw=2.0, shade=GOLD[3], gl=(3.0, 2.0, -2.0, 37.0, 35.0))
    heart = sd_heart(c, 20.0, 0.0, -11.0)
    piece(c, heart, "FFF2B0", "F2B33A", GOLD[2], 0.0, -21.0, lw=1.8, gl=(3.0, 2.0, -4.0, -6.0, 35.0))
    return "ICO_Event_Box", c, RUNTIME_ICONS


def icon_currency():
    """ICO_Event_Currency: a round pearl coin, a rose paw in its middle, a ring of tiny pearls."""
    c = Canvas(128, 128, cy=64.0)
    disc = sd_circle(c, 46.0, 0.0, 0.0)
    family(c, [disc])
    piece(c, disc, PEARL[0], PEARL[1], PEARL[2], 46.0, -46.0, lw=2.6, shade="E0A2B4", rim="FFFFFF", gl=(16.0, 8.0, -16.0, 22.0, 35.0))
    c.over(hexrgb("E3A9BA"), fill(np.abs(sd_circle(c, 35.0, 0.0, 0.0)) - 1.2) * 0.8)
    pearls = [(math.cos(math.radians(a)) * 40.5, math.sin(math.radians(a)) * 40.5) for a in range(0, 360, 30)]
    dots(c, pearls, 2.0, "FFFFFF", 0.9)
    paw = sd_paw(c, 1.2, 0.0, 2.0)
    c.over(hexrgb("FFFFFF"), fill(paw + 1.4) * 0.6)
    c.over(vgrad(c, "E893AE", "B85A7A", 22.0, -20.0), fill(paw))
    return "ICO_Event_Currency", c, RUNTIME_ICONS


def icon_odds():
    """ICO_EventEgg_Odds: an « i » in a round sky badge."""
    c = Canvas(128, 128, cy=64.0)
    disc = sd_circle(c, 44.0, 0.0, 0.0)
    family(c, [disc])
    piece(c, disc, SKY[0], SKY[1], SKY[2], 44.0, -44.0, lw=2.6, shade="5F9FD0", rim="F0FAFF", gl=(15.0, 7.0, -15.0, 20.0, 35.0))
    letter = union(sd_circle(c, 8.0, 0.0, 22.0), sd_round_rect(c, 7.0, 20.0, 6.0, 0.0, -12.0))
    c.over(hexrgb("2F5F86"), fill(letter + 1.6) * 0.4)
    c.over(vgrad(c, "FFFFFF", "E6F4FF", 30.0, -32.0), fill(letter))
    return "ICO_EventEgg_Odds", c, RUNTIME_ICONS


def icon_souvenir():
    """ICO_Souvenir: a small wooden frame with a meadow and a sun, a pink heart on top, a cream date tag at its foot."""
    c = Canvas(128, 128, cy=62.0)
    frame = sd_round_rect(c, 40.0, 42.0, 10.0, 0.0, 6.0)
    tag = sd_round_rect(c, 24.0, 9.0, 8.0, 14.0, -40.0)
    heart = sd_heart(c, 20.0, 0.0, 50.0)
    family(c, [frame, tag, heart])
    piece(c, frame, WOOD[0], WOOD[1], WOOD[2], 48.0, -36.0, lw=2.4, shade="9A6434", rim="FFE2B8")
    inner = sd_round_rect(c, 28.0, 30.0, 5.0, 0.0, 7.0)
    c.over(hexrgb("7A522E"), fill(inner + 1.6))
    c.over(vgrad(c, "E2F5FF", "BFE6FF", 37.0, -23.0), fill(inner))
    hill = np.maximum(sd_circle(c, 36.0, -10.0, -44.0), inner)
    c.over(vgrad(c, "B8E890", "6CB64E", -8.0, -23.0), fill(hill))
    hill2 = np.maximum(sd_circle(c, 30.0, 22.0, -42.0), inner)
    c.over(vgrad(c, "A6DE7E", "5AA444", -12.0, -23.0), fill(hill2))
    sun = sd_circle(c, 7.0, 13.0, 23.0)
    piece(c, sun, GOLD[0], GOLD[1], GOLD[2], 30.0, 16.0, lw=1.2)
    piece(c, heart, "FFC9D6", "F08AA8", "A8456A", 58.0, 42.0, lw=1.8, gl=(3.0, 2.0, -4.0, 53.0, 35.0))
    piece(c, tag, CREAM[0], CREAM[1], CREAM[2], -31.0, -49.0, lw=1.8)
    dots(c, [(4.0, -40.0), (14.0, -40.0), (24.0, -40.0)], 2.4, "8A5A32", 0.9)
    return "ICO_Souvenir", c, RUNTIME_ICONS


def icon_quest():
    """ICO_Quest: a cream scroll between two wooden rolls, soft written lines and a plump green check."""
    c = Canvas(128, 128, cy=64.0)
    paper = sd_round_rect(c, 32.0, 38.0, 5.0, 0.0, 0.0)
    top = sd_round_rect(c, 40.0, 8.0, 8.0, 0.0, 39.0)
    bottom = sd_round_rect(c, 40.0, 8.0, 8.0, 0.0, -39.0)
    family(c, [paper, top, bottom])
    piece(c, paper, CREAM[0], CREAM[1], CREAM[2], 38.0, -38.0, lw=2.0, shade="E3D2AE")
    for y in (22.0, 12.0):
        c.over(hexrgb("CDB58C"), fill(sd_round_rect(c, 18.0, 2.0, 2.0, -2.0, y)) * 0.8)
    for roll in (top, bottom):
        piece(c, roll, "F2D3A0", "C99A5E", WOOD[2], 6.0, -6.0, lw=2.0)
    c.over(vgrad(c, "F2D3A0", "C99A5E", 47.0, 31.0), fill(top))
    c.over(vgrad(c, "F2D3A0", "C99A5E", -31.0, -47.0), fill(bottom))
    dots(c, [(-40.0, 39.0), (40.0, 39.0), (-40.0, -39.0), (40.0, -39.0)], 3.0, "8A5A32", 0.9)
    short = sd_round_rect(View(c, -12.0, -12.0, -45.0), 10.0, 5.5, 5.5)
    long_ = sd_round_rect(View(c, 7.0, -2.0, 50.0), 20.0, 5.5, 5.5)
    check = union(short, long_)
    piece(c, check, GREEN[0], GREEN[1], GREEN[2], 14.0, -20.0, lw=2.2, shade="4F9A3E")
    return "ICO_Quest", c, RUNTIME_ICONS


def icon_outfit():
    """ICO_Event_Outfit: a mint neck scarf, a round knot and cream pearls."""
    c = Canvas(128, 128, cy=62.0)
    band = sd_round_rect(c, 44.0, 9.0, 9.0, 0.0, 26.0)
    tri = sd_polygon(c, [(-34.0, 20.0), (34.0, 20.0), (0.0, -36.0)]) - 6.0
    knot = sd_circle(c, 10.0, 0.0, 22.0)
    family(c, [band, tri, knot])
    piece(c, tri, MINT[0], MINT[1], MINT[2], 26.0, -42.0, lw=2.4, shade="4FA67C", rim="F0FFF6", gl=(12.0, 5.0, -10.0, 10.0, 20.0))
    dots(c, [(-14.0, 6.0), (14.0, 6.0), (0.0, -8.0), (0.0, -22.0)], 3.0, "FFF8EE", 0.95, mask=fill(tri))
    piece(c, band, "BFEFD2", "5DB88C", MINT[2], 35.0, 17.0, lw=2.2, shade="4FA67C", rim="F0FFF6")
    piece(c, knot, "C8F5DA", "4FAE80", MINT[2], 32.0, 12.0, lw=2.0, gl=(4.0, 2.0, -3.0, 27.0, 35.0))
    return "ICO_Event_Outfit", c, RUNTIME_ICONS


def icon_points():
    """ICO_Event_Points: the project's gold five-point star with a green up-arrow badge at its foot."""
    c = Canvas(128, 128, cy=64.0)
    star = sd_rounded_star(c, 40.0, 19.0, -6.0, 4.0, rounding=5.0)
    badge = sd_circle(c, 17.0, 32.0, -32.0)
    family(c, [star, badge])
    gold_star(c, 40.0, 19.0, -6.0, 4.0, rounding=5.0)
    piece(c, badge, GREEN[0], GREEN[1], GREEN[2], -15.0, -49.0, lw=2.2, shade="4F9A3E", gl=(5.0, 2.0, 26.0, -22.0, 35.0))
    arrow = union(sd_polygon(c, [(32.0, -20.0), (22.0, -31.0), (42.0, -31.0)]) - 1.2, sd_round_rect(c, 3.6, 7.0, 2.0, 32.0, -37.0))
    c.over(hexrgb("2F6A2A"), fill(arrow + 1.2) * 0.4)
    c.over(hexrgb("FFFFFF"), fill(arrow))
    return "ICO_Event_Points", c, RUNTIME_ICONS


T48_SPRITES = (icon_event, icon_gold_book, icon_box, icon_currency, icon_odds, icon_souvenir, icon_quest, icon_outfit, icon_points)


def main():
    for folder in (RUNTIME_ICONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T48_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
