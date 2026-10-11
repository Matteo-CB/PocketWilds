"""Pocket Wilds UI sprites added in T49 (« L'Éveil des Terriers », event.burrows, the first complete event).

Same method as pw_ui_sprites_t48.py (signed distances rendered at 4x, box-filtered down), same family as the HUD icons:
rounded filled shapes, one warm outline per icon, light from the top left (a vertical gradient, one deeper shade, one gloss),
a soft warm drop shadow below. The event's mood: tender sprout green, cream, warm earth, little earth mounds and ROUND dew
beads (owner rules: round beads and round five-point stars, never a four-point sparkle; nothing religious; handmade only, no
generator, no library art).

Icons (128 x 128, Art/UI/Icons, readable at 24 and 48 dp):
- ICO_Event_Pousses: the event currency « Pousses », a round cream coin with a tender green rim, a little earth mound and a
  plump two-leaf sprout, a ring of tiny round dew pearls.
- ICO_Effect_SproutBurst: the hatch effect « Éclosion de pousses », a cream egg on a soft green patch inside a ring of six
  little sprouts and six round dew beads.
- ICO_Frame_EventBurrowsGold: the gold version of the « Racines » frame, a gold ring with rounded warm roots at its foot and
  tender sprouts at its top.

Banner (Art/UI/Events, 688 x 254, shown at 1032 x 380 behind the event hub's banner and its announce page):
- UI_EventBanner_Burrows: soft green hills, three warm earth mounds with sprouts, round dew beads on the leaves and in the
  grass, a few cream flowers and a warm morning glow at the right. The top left stays empty for the event's name and date,
  the edges fade inside the banner panel's rounded corners.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t49.py) to write only these.
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, ROOT, RUNTIME_ICONS, blur, fill, hexrgb, sd_circle, sd_egg, sd_ellipse, sd_polyline,
                           sd_round_rect)
from pw_ui_sprites_t36 import vgrad
from pw_ui_sprites_t37 import union
from pw_ui_sprites_t44 import GOLD, dots, family, piece

RUNTIME_EVENTS = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Events")

CREAM = ("FFFDF4", "F2E4C6", "C9B58F")       # paper and coin face: top, bottom, outline
SPROUT = ("D8F5A8", "7CBF5A", "3F7A2E")      # tender leaves: top, bottom, outline
STEM = ("B8E68A", "5E9E4A", "3F7A2E")        # stems
EARTH = ("D9A878", "9C6B45", "5E3C20")       # warm earth mounds
MOSS = ("E4F5C8", "A8D17F", "4F8A3F")        # the coin's rim and the green patch
DEW = ("FFFFFF", "CBE9F7", "7FB2CF")         # round dew beads: top, bottom, outline


def sprout(c, x, y, s, lw=1.6, leaf=SPROUT):
    """A plump two-leaf sprout standing at (x, y): a short stem and two round leaves. Returns the union for outlines."""
    stem = sd_polyline(c, [(x, y), (x + 0.6 * s, y + 9.0 * s), (x, y + 16.0 * s)], 1.7 * s)
    left = sd_ellipse(c, 9.5 * s, 5.2 * s, x - 8.0 * s, y + 19.0 * s, 28.0)
    right = sd_ellipse(c, 9.5 * s, 5.2 * s, x + 8.0 * s, y + 21.0 * s, -32.0)
    return stem, left, right


def draw_sprout(c, parts, x, y, s, lw=1.6, leaf=SPROUT):
    stem, left, right = parts
    piece(c, stem, STEM[0], STEM[1], STEM[2], y + 16.0 * s, y, lw=lw * 0.8)
    for d, ly in ((left, y + 19.0 * s), (right, y + 21.0 * s)):
        piece(c, d, leaf[0], leaf[1], leaf[2], ly + 5.5 * s, ly - 5.5 * s, lw=lw, shade="5E9E4A")
    # A soft middle vein on each leaf.
    for cx, cy, a in ((x - 8.0 * s, y + 19.0 * s, 28.0), (x + 8.0 * s, y + 21.0 * s, -32.0)):
        r = math.radians(a)
        dx, dy = math.cos(r) * 5.5 * s, math.sin(r) * 5.5 * s
        vein = sd_polyline(c, [(cx - dx, cy - dy), (cx + dx, cy + dy)], max(0.45 * s, 0.35))
        c.over(hexrgb("F2FFD8"), fill(vein) * 0.55)


def dew(c, x, y, r, lw=1.2, alpha=1.0):
    """A round dew bead: a pale blue-white pearl with a bright round highlight (never a four-point glint)."""
    d = sd_circle(c, r, x, y)
    c.over(hexrgb(DEW[2]), fill(d - lw) * alpha)
    c.over(vgrad(c, DEW[0], DEW[1], y + r, y - r), fill(d) * alpha)
    c.over(hexrgb("FFFFFF"), fill(sd_circle(c, r * 0.36, x - r * 0.32, y + r * 0.34)) * 0.95 * alpha)
    return d


def icon_pousses():
    """ICO_Event_Pousses: a round cream coin with a tender green rim, an earth mound and a two-leaf sprout, tiny dew pearls."""
    c = Canvas(128, 128, cy=64.0)
    disc = sd_circle(c, 46.0, 0.0, 0.0)
    family(c, [disc])
    piece(c, disc, MOSS[0], MOSS[1], MOSS[2], 46.0, -46.0, lw=2.6, shade="8CBF62", rim="F6FFE8", gl=(16.0, 8.0, -16.0, 22.0, 35.0))
    face = sd_circle(c, 35.0, 0.0, 0.0)
    c.over(hexrgb("7FAF5A"), fill(face - 1.6))
    c.over(vgrad(c, CREAM[0], CREAM[1], 35.0, -35.0), fill(face))
    pearls = [(math.cos(math.radians(a)) * 40.5, math.sin(math.radians(a)) * 40.5) for a in range(15, 375, 30)]
    dots(c, pearls, 2.0, "FFFFFF", 0.9)
    mound = np.maximum(sd_ellipse(c, 22.0, 12.0, 0.0, -20.0), face)
    piece(c, mound, EARTH[0], EARTH[1], EARTH[2], -8.0, -32.0, lw=1.6, shade="7E5032")
    dots(c, [(-9.0, -18.0), (6.0, -23.0), (12.0, -16.0)], 1.4, "7E5032", 0.7)
    parts = sprout(c, 0.0, -10.0, 1.45)
    c.over(hexrgb("3F7A2E"), fill(union(*parts) - 2.0) * 0.35)
    draw_sprout(c, parts, 0.0, -10.0, 1.45, lw=1.8)
    dew(c, 16.0, 25.0, 3.4, lw=1.0)
    return "ICO_Event_Pousses", c, RUNTIME_ICONS


def icon_sprout_burst():
    """ICO_Effect_SproutBurst: a cream egg on a soft green patch in a ring of six sprouts and six round dew beads."""
    c = Canvas(128, 128, cy=66.0)
    patch = sd_ellipse(c, 50.0, 22.0, 0.0, -24.0)
    egg = sd_egg(c, 15.0, 19.0, 0.18, -8.0)
    family(c, [patch, egg], dy=2.0, strength=0.18)
    piece(c, patch, MOSS[0], MOSS[1], MOSS[2], -2.0, -46.0, lw=2.0, shade="8CBF62", rim="F6FFE8")
    ring = []
    for i in range(6):
        a = math.radians(90.0 + i * 60.0)
        ring.append((math.cos(a) * 42.0, math.sin(a) * 16.0 - 24.0 + (14.0 if i == 0 else 0.0)))
    back = [p for p in ring if p[1] > -24.0]
    front = [p for p in ring if p[1] <= -24.0]
    for x, y in back:
        parts = sprout(c, x, y, 0.95)
        c.over(hexrgb("3F7A2E"), fill(union(*parts) - 1.8) * 0.6)
        draw_sprout(c, parts, x, y, 0.95, lw=1.4)
    c.over(hexrgb("A88F6A"), fill(egg - 2.2))
    c.over(vgrad(c, "FFFDF6", "EBDDC0", 11.0, -27.0), fill(egg))
    c.over(hexrgb("FFFFFF"), fill(sd_ellipse(c, 4.0, 6.0, -5.0, 2.0, 20.0)) * 0.8)
    for x, y in front:
        parts = sprout(c, x, y, 1.05)
        c.over(hexrgb("3F7A2E"), fill(union(*parts) - 1.8) * 0.6)
        draw_sprout(c, parts, x, y, 1.05, lw=1.5)
    beads = []
    for i in range(6):
        a = math.radians(120.0 + i * 60.0)
        beads.append((math.cos(a) * 50.0, math.sin(a) * 19.0 - 22.0, 4.2 if math.sin(a) < 0 else 3.4))
    for x, y, r in beads:
        dew(c, x, y, r, lw=1.1)
    dew(c, -30.0, 34.0, 3.0, lw=1.0)
    dew(c, 34.0, 40.0, 2.4, lw=0.9)
    return "ICO_Effect_SproutBurst", c, RUNTIME_ICONS


def root_shape(c, ox, oy, s, flip):
    """Three rounded roots hanging from a round knot (the « Racines » motif), mirrored when flip."""
    k = -1.0 if flip else 1.0
    paths = (((0.0, 0.0), (-3.0 * k, -8.0), (-10.0 * k, -14.0), (-15.0 * k, -22.0)),
             ((0.0, 0.0), (1.0 * k, -10.0), (2.0 * k, -20.0)),
             ((0.0, 0.0), (5.0 * k, -7.0), (12.0 * k, -12.0), (16.0 * k, -19.0)))
    shapes = [sd_polyline(c, [(ox + x * s, oy + y * s) for x, y in p], (3.0 - 0.3 * i) * s) for i, p in enumerate(paths)]
    shapes.append(sd_circle(c, 4.4 * s, ox, oy))
    return union(*shapes)


def icon_frame_gold():
    """ICO_Frame_EventBurrowsGold: a gold ring, rounded warm roots at its foot, tender sprouts at its top."""
    c = Canvas(128, 128, cy=64.0)
    outer = sd_circle(c, 44.0, 0.0, 0.0)
    ring = np.maximum(outer, -sd_circle(c, 30.0, 0.0, 0.0))
    roots_l = root_shape(c, -26.0, -24.0, 1.05, False)
    roots_r = root_shape(c, 26.0, -24.0, 1.05, True)
    family(c, [ring, roots_l, roots_r])
    piece(c, ring, GOLD[0], GOLD[1], GOLD[2], 44.0, -44.0, lw=2.4, shade=GOLD[3], rim="FFF8D6", gl=(14.0, 6.0, -18.0, 26.0, 35.0))
    c.over(hexrgb("FFF4C2"), fill(np.abs(sd_circle(c, 37.0, 0.0, 0.0)) - 0.9) * 0.7)
    for d in (roots_l, roots_r):
        piece(c, d, EARTH[0], EARTH[1], EARTH[2], -18.0, -48.0, lw=1.6, shade="7E5032", rim="F0D6B0")
    for x, y in ((-24.0, 30.0), (24.0, 30.0)):
        parts = sprout(c, x, y, 0.95)
        c.over(hexrgb("3F7A2E"), fill(union(*parts) - 1.8) * 0.8)
        draw_sprout(c, parts, x, y, 0.95, lw=1.4)
    dew(c, 0.0, 44.0, 4.0, lw=1.1)
    return "ICO_Frame_EventBurrowsGold", c, RUNTIME_ICONS


def banner_burrows():
    """UI_EventBanner_Burrows: hills, warm earth mounds, sprouts and round dew beads; the top left stays clear for the texts."""
    w, h = 688, 254
    c = Canvas(w, h, cx=0.0, cy=float(h))     # origin bottom left, y up, x right
    # A warm morning glow behind the featured species (top right), very soft.
    glow = np.clip(1.0 - np.hypot((c.x - 520.0) / 210.0, (c.y - 170.0) / 120.0), 0.0, 1.0) ** 1.6
    c.over(hexrgb("FFE7B0"), glow * 0.55)
    # Far hills: tender green, no outline (distance), each a little paler than the next.
    hills = ((300.0, -40.0, 240.0, 110.0, ("E2F3C6", "C4E2A0")), (560.0, -30.0, 230.0, 120.0, ("D6EFB4", "B4DA8C")),
             (720.0, -50.0, 200.0, 130.0, ("CDEAA6", "A8D17F")))
    for x, y, rx, ry, (top, bottom) in hills:
        d = sd_ellipse(c, rx, ry, x, y)
        c.over(vgrad(c, top, bottom, y + ry, 0.0), fill(d) * 0.95)
    # The near meadow: a gentle wave along the bottom, across the whole banner (the currency chip sits on it).
    wave = c.y - (30.0 + 6.0 * np.sin(c.x / 46.0) + 4.0 * np.sin(c.x / 17.0 + 1.3))
    c.over(vgrad(c, "BFE39A", "8CC86A", 40.0, 0.0), fill(wave))
    c.over(hexrgb("E9F8D2"), fill(np.abs(wave + 1.0) - 0.8) * 0.6)
    # Three warm earth mounds with soil crumbs, the middle one the largest.
    mounds = ((470.0, 24.0, 74.0, 46.0), (612.0, 22.0, 54.0, 34.0), (330.0, 22.0, 46.0, 28.0))
    for x, y, rx, ry in mounds:
        d = np.maximum(sd_ellipse(c, rx, ry, x, y), -(c.y - (y - 4.0)))
        c.over((60, 40, 20), blur(fill(d - 3.0), 2.0) * 0.18)
        piece(c, d, EARTH[0], EARTH[1], EARTH[2], y + ry, y - 4.0, lw=2.2, shade="7E5032", rim="F2D3AE")
        crumbs = [(x - rx * 0.45, y + ry * 0.35), (x + rx * 0.2, y + ry * 0.55), (x + rx * 0.55, y + ry * 0.2),
                  (x - rx * 0.1, y + ry * 0.15)]
        dots(c, crumbs, max(2.0, rx * 0.04), "7E5032", 0.55)
        # The opening of a burrow at the foot of the big mound.
        if rx > 70.0:
            hole = sd_ellipse(c, 15.0, 9.0, x + 6.0, y + 8.0)
            c.over(hexrgb("5E3C20"), fill(hole - 1.6))
            c.over(vgrad(c, "6E4524", "3E2614", y + 17.0, y), fill(hole))
    # Grass blades in front of the mounds' feet: round-tipped, tender green.
    rng = np.random.RandomState(103)
    for x in np.linspace(250.0, 680.0, 22):
        x = float(x + rng.uniform(-8.0, 8.0))
        hgt = float(rng.uniform(10.0, 18.0))
        lean = float(rng.uniform(-5.0, 5.0))
        blade = sd_polyline(c, [(x, 18.0), (x + lean * 0.5, 18.0 + hgt * 0.6), (x + lean, 18.0 + hgt)], 2.2)
        c.over(hexrgb("4F8A3F"), fill(blade - 1.0))
        c.over(vgrad(c, "C9F09A", "6CB04E", 18.0 + hgt, 18.0), fill(blade))
    # Sprouts: on each mound and in the meadow (big at the right, small towards the texts).
    sprouts = ((470.0, 66.0, 2.1), (446.0, 56.0, 1.3), (612.0, 52.0, 1.7), (330.0, 46.0, 1.4), (540.0, 26.0, 1.2),
               (668.0, 28.0, 1.3), (390.0, 26.0, 1.0), (250.0, 24.0, 0.9), (180.0, 22.0, 0.8))
    for x, y, s in sprouts:
        parts = sprout(c, x, y, s)
        c.over((40, 70, 30), blur(shift_down(fill(union(*parts) - 1.6), c, 2.0), 1.5) * 0.15)
        c.over(hexrgb("3F7A2E"), fill(union(*parts) - 2.0) * 0.8)
        draw_sprout(c, parts, x, y, s, lw=1.8)
    # Little cream flowers (five round petals and a warm heart).
    for x, y, s in ((560.0, 34.0, 1.0), (290.0, 30.0, 0.85), (650.0, 40.0, 0.9), (120.0, 26.0, 0.7)):
        petals = union(*[sd_circle(c, 4.2 * s, x + math.cos(math.radians(90 + k * 72)) * 5.0 * s,
                                   y + math.sin(math.radians(90 + k * 72)) * 5.0 * s) for k in range(5)])
        c.over(hexrgb("C9B58F"), fill(petals - 1.2))
        c.over(vgrad(c, "FFFFFF", "F6EBD2", y + 9.0 * s, y - 9.0 * s), fill(petals))
        c.over(hexrgb("F2B93A"), fill(sd_circle(c, 2.6 * s, x, y)))
    # Round dew beads: on the leaves' tips, in the grass, and a few soft round ones floating in the glow.
    for x, y, r in ((488.0, 112.0, 5.2), (452.0, 104.0, 4.0), (627.0, 90.0, 4.4), (343.0, 78.0, 3.6), (553.0, 54.0, 3.4),
                    (402.0, 30.0, 3.0), (264.0, 30.0, 2.8), (676.0, 50.0, 3.2), (520.0, 14.0, 3.0)):
        dew(c, x, y, r, lw=1.2)
    for x, y, r in ((560.0, 190.0, 5.0), (610.0, 150.0, 3.6), (500.0, 214.0, 3.0), (650.0, 205.0, 4.2)):
        c.over(hexrgb("FFFFFF"), fill(sd_circle(c, r, x, y)) * 0.35)
        c.over(hexrgb("FFFFFF"), fill(np.abs(sd_circle(c, r, x, y)) - 0.6) * 0.5)
    # Fade inside the banner panel's rounded corners (radius 40 px at this size) so nothing touches its edge.
    inside = sd_round_rect(c, w * 0.5 - 2.0, h * 0.5 - 2.0, 40.0, w * 0.5, h * 0.5)
    mask = np.clip(-inside / 6.0, 0.0, 1.0)
    c.rgba *= mask[..., None]
    return "UI_EventBanner_Burrows", c, RUNTIME_EVENTS


def shift_down(alpha, c, px):
    """The alpha moved down by px output pixels (a contact shadow under a sprout)."""
    n = int(round(px * (c.rgba.shape[0] / c.h)))
    out = np.zeros_like(alpha)
    out[n:, :] = alpha[:-n, :] if n > 0 else alpha
    return out


T49_SPRITES = (icon_pousses, icon_sprout_burst, icon_frame_gold, banner_burrows)


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_EVENTS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T49_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
