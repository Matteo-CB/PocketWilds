"""Pocket Wilds UI sprites added in T40 (Pluie de noisettes, the third activity on the T37 framework; doc 01 Contenu,
S-ACT-003, the specials of the « Objets » table; doc « Mini-jeux & activités », section 3).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the
HUD icons: rounded filled shapes, one outline in a deeper tone of each material, light from the top left (a vertical
gradient, one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round dots, five-point
stars, bubbles, hearts and paws only (the golden shower's trails are round dots, never a four-point star), nothing
religious, nothing enters anything (the arrows stand clear of the basket, each trail dot floats clear of its nut, of the
other dots and of the other nuts).

Boost icons (own colors, never tinted; inside the HUD's boost chip, IActivityHudView.ShowBoost: about 62 units in the
104 unit chip, readable at 48 px):
- ICO_WideBasket: the Panier large, a wide round wicker basket seen a little from above (its inner wall, a twisted rim,
  the weave of ICO_Act_NutRain), two soft honey gold arrows pointing outward on its sides: the basket 50 % wider.
- ICO_Feather: the Plume, a soft round sky blue feather, its round tip up and to the right, a soft notch on each side of
  its vane, a light shaft, a cream quill and a little down at its base: everything falls slower.
- ICO_GoldenShower: the Averse dorée, three golden hazelnuts (the hazelnut of ICO_Act_NutRain in warm gold) falling at
  different heights, each under a trail of three round soft dots, smaller and smaller upward (H-VFX-ACT-003-02): only
  golden hazelnuts fall.

Reward icons (own colors, never tinted; on the reward cells of the result card, RewardIconSet.items, beside
ICO_Treat_CloverCrunch, readable at 48 px):
- ICO_Treat_MushroomBiscuit: T39's 3-star treat (Biscuit champignon), a round biscuit shaped like a mushroom, its cap
  iced in soft rose with round cream dots, its short stem plain biscuit.
- ICO_Treat_ToastedHazelnut: T40's 3-star treat (Noisette grillée), two warm toasted hazelnut kernels, golden brown with
  patches of their papery skin and a honey glaze, two soft wisps of warmth above them.
- ICO_Ingredient_Hazelnut: the fresh Hazelnut ingredient, a hazelnut in its glossy shell with its pale base, a broad
  green hazel leaf behind it and a short twig: never toasted, never glazed.
- ICO_Ingredient_Strawberry: the Strawberry ingredient, a small round wild strawberry, its round pale seeds, a crown of
  five round green leaves and a short stem.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t40.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, blur, fill, hexrgb, lerp, sd_circle, sd_ellipse, sd_polygon,
                           sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t35a import SHADOW, drop_shadow
from pw_ui_sprites_t36 import WARM_OUTLINE, View, gloss, sticker, vgrad
from pw_ui_sprites_t37 import draw_hazelnut, edge, lower_shade, sd_drop, sd_hazelnut, top_rim, union

WIDTH = 4.5  # the family's outline on a 128 px icon

# Materials: top, bottom, outline, lower shade, light streak.
WICKER = ("E2AE6E", "9A6234", "5E3F24", "7A4E2A", "F8DDB0")  # the bowl of ICO_Act_NutRain
RIM = ("F6D7A2", "C48B52")       # the twisted rim: top, bottom
INSIDE = ("B17A42", "6E4626")    # the inner wall seen over the front rim: lit back, shaded floor
GOLD = ("FFE48A", "F0A73A", "A8662A", "E0892C", "FFF6C8")  # the challenge medal's honey gold
SKY = ("E2F2FF", "86BDEA", "35649F", "5E95D0", "F6FBFF")   # a sky blue near ICO_Bow_SkyBlue's
SHADOW_SOFT = SHADOW  # the family's warm drop shadow, lighter under the floating dots


def blur_dot(c, shape):
    """The soft shadow of a small floating dot: its disc moved down 2 px and blurred 1.5 px."""
    return blur(shift(fill(shape), 0, 2), 1.5)


# ---------------------------------------------------------------- wide basket

RIM_Y = 14.0     # the rim's center height
BASKET_A = 37.0  # the basket's half width at its rim
RIM_B = 10.0     # the rim's half depth (the basket seen a little from above)
BOWL_DEPTH = 38.0  # from the rim's center down to the bowl's bottom
BASE_FLAT = 7.0    # the bottom flattened over this height, so it stands
OPENING = (30.5, 5.4)  # the opening inside the rim: half width, half depth
ROWS = (12.0, 24.0)    # the weave's two rows, depth under the rim
STAKES = 8             # the stakes across the front
ARROW_Y = RIM_Y - 25.0  # the arrows' height, beside the lower bowl, clear of the rim
ARROW = (47.5, 55.5, 8.0, 2.0)  # base x, tip x, base half height, rounding


def basket_shapes(c):
    """The wide basket's pieces in icon units: bowl (under the rim), rim ring, the opening, and the whole basket."""
    bottom = RIM_Y - BOWL_DEPTH
    bowl = np.maximum(sd_ellipse(c, BASKET_A, BOWL_DEPTH, ox=0.0, oy=RIM_Y), c.y - RIM_Y)
    bowl = -smooth_min(-bowl, c.y - (bottom + BASE_FLAT), 3.0)
    rim = sd_ellipse(c, BASKET_A + 1.5, RIM_B, ox=0.0, oy=RIM_Y)
    opening = sd_ellipse(c, OPENING[0], OPENING[1], ox=0.0, oy=RIM_Y + 0.8)
    return bowl, rim, opening, smooth_min(bowl, rim, 3.0)


def arrow_shapes(c):
    """The two soft arrows pointing outward from the basket's sides (rounded triangles)."""
    x0, x1, h, r = ARROW
    shapes = []
    for side in (-1.0, 1.0):
        points = [(side * x0, ARROW_Y + h), (side * x1, ARROW_Y), (side * x0, ARROW_Y - h)]
        shapes.append(sd_polygon(c, points) - r)
    return shapes


def row_curve(c, depth):
    """The front curve of a weave row `depth` under the rim (the basket narrows as it goes down)."""
    a = BASKET_A * math.sqrt(max(1.0 - (depth / BOWL_DEPTH) ** 2, 0.05))
    b = RIM_B * a / BASKET_A
    return RIM_Y - depth - b * np.sqrt(np.clip(1.0 - (c.x / a) ** 2, 0.0, 1.0))


def icon_wide_basket():
    """ICO_WideBasket: the Panier large boost. A wide round wicker basket seen a little from above: its inner wall in
    the opening, a twisted rim, stakes and two woven rows following the curve of the bowl, a flat foot; two soft honey
    gold arrows point outward on its sides."""
    c = Canvas(128, 128, cy=64.0)
    bowl, rim, opening, basket = basket_shapes(c)
    arrows = arrow_shapes(c)
    drop_shadow(c, union(basket, *arrows) - WIDTH)
    # The arrows: honey gold, their own deeper edge, a light upper line.
    top, bottom, line, shade, light = GOLD
    for arrow in arrows:
        edge(c, arrow, line, width=2.4)
        c.over(vgrad(c, top, bottom, ARROW_Y + ARROW[2] + 2.0, ARROW_Y - ARROW[2] - 2.0), fill(arrow))
        top_rim(c, arrow, ARROW_Y, 6.0, 0.6, color=hexrgb(light))
    # The bowl: wicker, lit from the top left, stakes and rows following its curve, a deeper lower shade.
    top, bottom, line, shade, light = WICKER
    edge(c, basket, line)
    inside = fill(bowl)
    c.over(vgrad(c, top, bottom, RIM_Y, RIM_Y - BOWL_DEPTH), inside)
    for k in range(STAKES):
        u = -1.0 + 2.0 * (k + 0.5) / STAKES
        x_top = u * BASKET_A * 0.97
        stake = sd_polyline(c, [(x_top, RIM_Y - RIM_B * math.sqrt(1.0 - u * u) - 1.0), (x_top * 0.78, RIM_Y - BOWL_DEPTH)], 1.5)
        c.over(hexrgb("8E5A2E"), fill(stake) * inside * 0.55)
        shine = sd_polyline(c, [(x_top + 2.6, RIM_Y - RIM_B * math.sqrt(1.0 - u * u) - 3.0), (x_top * 0.78 + 2.2, RIM_Y - BOWL_DEPTH + 4.0)], 1.0)
        c.over(hexrgb(light), fill(shine) * inside * 0.35)
    for depth in ROWS:
        row = np.abs(c.y - row_curve(c, depth)) - 1.6
        c.over(hexrgb("F0C890"), fill(row) * inside * 0.7)
        c.over(hexrgb("8E5A2E"), fill(np.abs(c.y - row_curve(c, depth) + 2.6) - 0.8) * inside * 0.45)
    lower_shade(c, bowl, shade, RIM_Y - 20.0, 14.0, width=6.0, strength=0.5)
    # The rim: a twisted band of light wicker around the opening, diagonal twists, a light upper line.
    ring = fill(rim) * (1.0 - fill(opening))
    c.over(vgrad(c, RIM[0], RIM[1], RIM_Y + RIM_B, RIM_Y - RIM_B), fill(rim))
    for k in range(13):
        x0 = -BASKET_A + 3.0 + k * (2.0 * BASKET_A - 6.0) / 12.0
        twist = sd_polyline(c, [(x0 - 3.2, RIM_Y - RIM_B), (x0 + 3.2, RIM_Y + RIM_B)], 1.1)
        c.over(hexrgb("A86E3A"), fill(twist) * ring * 0.55)
    top_rim(c, rim, RIM_Y + 3.0, 6.0, 0.6, color=hexrgb("FFF0D0"))
    # The opening: the inner wall behind, lit at the back, deeper toward the front rim, its edge drawn.
    c.over(hexrgb(line), fill(opening - 1.2) * 0.8)
    c.over(vgrad(c, INSIDE[0], INSIDE[1], RIM_Y + OPENING[1] + 0.8, RIM_Y - OPENING[1] + 0.8), fill(opening))
    for k in range(7):
        x0 = -OPENING[0] + 6.0 + k * (2.0 * OPENING[0] - 12.0) / 6.0
        rib = sd_polyline(c, [(x0, RIM_Y + 0.8 + OPENING[1]), (x0 * 0.94, RIM_Y - 1.0)], 0.9)
        c.over(hexrgb("8A5730"), fill(rib) * fill(opening) * 0.5)
    gloss(c, 7.0, 2.4, -24.0, RIM_Y + 6.4, 8.0, fill(rim) * (1.0 - fill(opening + 1.0)), 0.55)
    return "ICO_WideBasket", c, RUNTIME_ICONS


# ---------------------------------------------------------------- feather

FEATHER_X, FEATHER_Y, FEATHER_TILT = -2.5, 10.5, -40.0  # the vane's center; +y of its frame points up and to the right
VANE = (17.5, 37.0, -0.32)  # half width, half length, taper (negative: wider toward the round tip)
BEND = 6.0                  # how far the shaft curves (px at both ends)
QUILL = 16.0                # the bare quill below the vane
NOTCHES = ((-1.0, -2.0, 6.5), (1.0, -14.0, 6.0))  # side, height along the shaft, depth
DOWN = ((-1.0, -30.0, 5.2, -24.0), (1.0, -32.5, 4.4, -26.5))  # the down at the base: side, height, radius, root


class Bent:
    """A view whose x axis curves with the shaft: the shaft is a parabola through the vane's center."""

    def __init__(self, v, bend, half):
        self.x = v.x - bend * (v.y / half) ** 2
        self.y = v.y


def feather_point(x, y):
    """A point of the bent feather frame in icon units."""
    vx = x + BEND * (y / VANE[1]) ** 2
    a = math.radians(FEATHER_TILT)
    return FEATHER_X + vx * math.cos(a) - y * math.sin(a), FEATHER_Y + vx * math.sin(a) + y * math.cos(a)


def feather_shapes(c):
    """The feather's pieces: the vane (with its two notches), the shaft line inside it, the quill, the down, and the
    bent frame."""
    bv = Bent(View(c, FEATHER_X, FEATHER_Y, FEATHER_TILT), BEND, VANE[1])
    hw, hh, taper = VANE
    vane = sd_drop(bv, hw, hh, taper)
    for side, y, depth in NOTCHES:
        width = hw * (1.0 - taper * y / hh)
        outer = feather_point(side * (width + 4.0), y + 5.0)
        tip = feather_point(side * (width - depth), y - 2.0)
        back = feather_point(side * (width + 4.0), y - 3.5)
        vane = np.maximum(vane, -(sd_polygon(c, [outer, tip, back]) - 0.6))
    shaft = sd_polyline(c, [feather_point(0.0, -hh + 2.0), feather_point(0.0, hh * 0.72)], 1.3)
    quill = sd_polyline(c, [feather_point(0.0, -hh - QUILL), feather_point(0.0, -hh + 6.0)], 2.6)
    tufts = []
    for side, y, r, root in DOWN:
        tufts.append(smooth_min(sd_circle(c, r, *feather_point(side * 8.0, y)),
                                sd_polyline(c, [feather_point(side * 2.0, root), feather_point(side * 8.0, y)], r * 0.6), 2.0))
    down = union(*tufts)
    return vane, shaft, quill, down, bv


def icon_feather():
    """ICO_Feather: the Plume boost. A soft round sky blue feather leaning to the right, its round tip up, a soft notch
    on each side of the vane, the light shaft through it, faint barbs, a cream quill below, a little down at its base."""
    c = Canvas(128, 128, cy=64.0)
    vane, shaft, quill, down, bv = feather_shapes(c)
    feather = union(smooth_min(vane, down, 3.0), quill)
    drop_shadow(c, feather - WIDTH)
    top, bottom, line, shade, light = SKY
    edge(c, feather, line)
    # The quill (the cream of the practice feather's shaft), then the down, then the vane over them.
    c.over(vgrad(c, "FFFAF0", "E6D6BC", FEATHER_Y - 20.0, FEATHER_Y - 64.0), fill(quill))
    c.over(vgrad(c, "F4FAFF", "BCDDF6", FEATHER_Y - 22.0, FEATHER_Y - 48.0), fill(smooth_min(vane, down, 3.0)))
    inside = fill(vane)
    c.over(vgrad(c, top, bottom, FEATHER_Y + 38.0, FEATHER_Y - 36.0), inside)
    # Faint barbs from the shaft toward the edges, sloping to the tip; a deeper shade along the right edge.
    hw, hh, taper = VANE
    for y in (-18.0, -6.0, 6.0, 18.0):
        for side in (-1.0, 1.0):
            width = hw * (1.0 - taper * y / hh)
            barb = sd_polyline(c, [feather_point(side * 2.5, y), feather_point(side * (width - 4.0), y + 7.0)], 0.8)
            c.over(hexrgb(shade), fill(barb) * inside * 0.28)
    c.over(hexrgb(shade), soft(-vane - 5.0, 5.0) * np.clip((bv.x + 2.0) / 12.0, 0, 1) * inside * 0.45)
    top_rim(c, vane, FEATHER_Y, 30.0, 0.55, color=hexrgb(light))
    c.over(hexrgb(light), fill(shaft) * inside * 0.95)
    gx, gy = feather_point(-8.0, 16.0)
    gloss(c, 6.0, 3.0, gx, gy, 90.0 + FEATHER_TILT - 12.0, inside, 0.7)
    return "ICO_Feather", c, RUNTIME_ICONS


# ---------------------------------------------------------------- golden shower

FALL = 12.0  # the shower falls a little to the left: each trail rises up and to the right of its nut (degrees)
NUTS = ((-30.0, 2.0, 0.92, 38.0, 3), (29.0, 10.0, 0.86, 64.0, 2), (-6.0, -33.0, 1.0, 22.0, 3))  # x, y, size, tilt, dots
DOTS = (3.9, 3.0, 2.2)  # the trail's dots, nearest first
DOT_FADE = (1.0, 0.82, 0.62)  # and their strength: the trail fades upward
DOT_LINE = 1.5          # their deeper gold edge
DOT_GAP = 3.0           # the clear space between a nut and its first dot, and between two dots
NUT_REACH = 14.2        # a nut's reach (size 1) across its tip, toward its trail


def golden_hazelnut(c, s, ox, oy, angle_deg):
    """The hazelnut of ICO_Act_NutRain in warm gold: a glossy gold shell lit from the top left, two soft streaks from
    the tip, a deeper amber rough base with three small dots, a deeper shade on the right, one gloss."""
    nut, v = sd_hazelnut(c, s, ox, oy, angle_deg)
    inside = fill(nut)
    c.over(lerp(hexrgb("FFE892"), hexrgb("E9A23A"), np.clip((13.0 * s - v.y) / (26.0 * s), 0, 1)), inside)
    for dx in (-4.5, 4.0):
        streak = sd_polyline(v, [(dx * 0.3 * s, 11.0 * s), (dx * s, 1.0 * s), (dx * 0.9 * s, -6.0 * s)], 0.7 * s)
        c.over(hexrgb("C9822E"), fill(streak) * inside * 0.5)
    base_y = -7.5 * s + 0.7 * s * np.sin(v.x * 1.1 / s)
    base = fill(v.y - base_y) * inside
    c.over(lerp(hexrgb("EDB357"), hexrgb("C98536"), np.clip((-7.0 * s - v.y) / (6.0 * s), 0, 1)), base)
    for dx, dy in ((-3.5, -10.0), (2.0, -11.0), (5.5, -9.0)):
        c.over(hexrgb("A8662A"), fill(np.hypot(v.x - dx * s, v.y - dy * s) - 0.8 * s) * base * 0.7)
    c.over(hexrgb("B86E26"), soft(-nut - 4.0 * s, 4.0 * s) * np.clip((v.x + 2.0 * s) / (12.0 * s), 0, 1) * inside * 0.4)
    gl = sd_ellipse(v, 2.6 * s, 4.6 * s, ox=-5.0 * s, oy=4.0 * s, angle_deg=-18.0)
    c.over((255, 255, 255), soft(gl, 1.0 * s) * inside * 0.85)
    return nut


def trail(x, y, s, count):
    """The trail of round dots behind a falling nut at (x, y), size s: centers, radii and strengths, nearest first,
    along the fall line, each clear of the nut and of the last dot."""
    ux, uy = math.sin(math.radians(FALL)), math.cos(math.radians(FALL))
    reach = NUT_REACH * s + WIDTH
    dots = []
    for r, strength in list(zip(DOTS, DOT_FADE))[:count]:
        d = reach + DOT_GAP + DOT_LINE + r
        dots.append((x + ux * d, y + uy * d, r, strength))
        reach = d + r + DOT_LINE
    return dots


def icon_golden_shower():
    """ICO_GoldenShower: the Averse dorée boost. Three golden hazelnuts tumbling down at different heights, a little to
    the left, each under a trail of round soft gold dots that grow smaller and fainter upward."""
    c = Canvas(128, 128, cy=64.0)
    nuts = [sd_hazelnut(c, s, x, y, a)[0] for x, y, s, a, _ in NUTS]
    dots = [d for x, y, s, _, n in NUTS for d in trail(x, y, s, n)]
    discs = [sd_circle(c, r, x, y) for x, y, r, _ in dots]
    drop_shadow(c, union(*nuts) - WIDTH)
    for (x, y, r, strength), disc in zip(dots, discs):
        c.over(SHADOW_SOFT, blur_dot(c, disc - DOT_LINE) * 0.18 * strength)
        c.over(hexrgb("C98A34"), fill(disc - DOT_LINE) * strength)
        c.over(vgrad(c, "FFF4BE", "F5B848", y + r, y - r), fill(disc) * strength)
        c.over((255, 255, 255), fill(sd_circle(c, r * 0.32, x - r * 0.33, y + r * 0.36)) * 0.85 * strength)
    for (x, y, s, a, _), nut in zip(NUTS, nuts):
        edge(c, nut, GOLD[2])
        golden_hazelnut(c, s, x, y, a)
    return "ICO_GoldenShower", c, RUNTIME_ICONS


# ---------------------------------------------------------------- reward icons

ICING = ("FFC4B8", "EC7569", "B8443F", "D65A52", "FFE8E2")  # top, bottom, edge, lower shade, light
CREAM_DOTS = ((-24.0, 18.0, 6.2), (2.0, 27.0, 5.0), (25.0, 14.0, 6.6), (-6.0, 6.0, 4.4), (-36.0, 3.0, 3.6), (37.0, 0.5, 3.4))
CAP_Y, CAP_RX, CAP_RY = 0.0, 49.0, 40.0   # the cap's rim height, half width, dome height
LIP = 10.0                                  # the rim's half depth (the cap's round underside)
STEM = (16.0, 17.0, 9.0, -23.0)             # half width, half height, rounding, center height


def mushroom_shapes(c):
    """The mushroom biscuit's pieces: the cap (a dome over a round lip), the stem under it, the icing on the cap."""
    dome = np.maximum(sd_ellipse(c, CAP_RX, CAP_RY, oy=CAP_Y), CAP_Y - c.y)
    lip = sd_ellipse(c, CAP_RX, LIP, oy=CAP_Y)
    cap = smooth_min(dome, lip, 4.0)
    hw, hh, r, sy = STEM
    stem = sd_round_rect(c, hw, hh, r, oy=sy)
    drip = CAP_Y + 2.5 + 2.2 * np.cos(c.x * 0.21)  # the icing's soft lower edge, a little wavy
    icing = np.maximum(cap + 3.0, drip - c.y)
    return cap, stem, icing


def icon_treat_mushroom_biscuit():
    """ICO_Treat_MushroomBiscuit (Biscuit champignon, T39's 3-star treat): a round biscuit shaped like a mushroom, its
    cap iced in soft rose with round cream dots, a biscuit lip under the icing, a short stem of plain biscuit with two
    docked holes."""
    c = Canvas(128, 128, cy=64.0)
    cap, stem, icing = mushroom_shapes(c)
    body = union(cap, stem)
    sticker(c, body, WARM_OUTLINE, WIDTH)
    # The stem, then the cap's biscuit over it (the lip is in front of the stem's top).
    c.over(vgrad(c, "F7DCA6", "D9A05C", -6.0, -40.0), fill(stem))
    lower_shade(c, stem, "C47F3C", -30.0, 10.0, width=6.0, strength=0.55)
    for x, y in ((-4.0, -18.0), (3.0, -29.0)):
        c.over(hexrgb("B87A3C"), fill(sd_circle(c, 2.2, x, y)) * 0.85)
    edge(c, cap, WARM_OUTLINE)
    c.over(vgrad(c, "F6D08E", "D8994F", CAP_Y + 12.0, CAP_Y - LIP), fill(cap))
    for k in range(9):
        x = -36.0 + k * 9.0
        c.over(hexrgb("B87A3C"), fill(sd_circle(c, 1.6, x, CAP_Y - 5.0 + 0.0008 * x * x)) * fill(cap) * 0.7)
    # The icing: soft rose, its own deeper edge, lit from the top left, round cream dots.
    top, bottom, line, shade, light = ICING
    c.over(hexrgb(line), fill(icing - 1.2) * 0.85)
    inside = fill(icing)
    c.over(vgrad(c, top, bottom, CAP_Y + CAP_RY, CAP_Y), inside)
    lower_shade(c, icing, shade, CAP_Y + 10.0, 14.0, width=5.0, strength=0.45)
    for x, y, r in CREAM_DOTS:
        dot = sd_circle(c, r, x, y)
        c.over(hexrgb("D98A80"), fill(dot - 0.9) * inside * 0.6)
        c.over(vgrad(c, "FFFBF2", "F3E2C8", y + r, y - r), fill(dot) * inside)
    top_rim(c, icing, CAP_Y + 20.0, 16.0, 0.55, color=hexrgb(light))
    gloss(c, 9.0, 4.0, -20.0, CAP_Y + 31.0, 28.0, inside, 0.55, edge=2.0)
    return "ICO_Treat_MushroomBiscuit", c, RUNTIME_ICONS


KERNELS = ((16.0, -4.0, 1.0, -20.0), (-16.0, -14.0, 1.06, 12.0))  # x, y, size, tilt: the back kernel first
WISPS = ((-13.0, 23.0, 15.0), (6.0, 28.0, 14.0))  # each wisp of warmth: its lower end (x, y) and its height


def sd_kernel(c, s, ox, oy, angle_deg):
    """A toasted hazelnut kernel: a plump round nut, a little pointed at its tip, no shell (s = 1 is about 40 px wide)."""
    v = View(c, ox, oy, angle_deg)
    body = sd_drop(v, 21.0 * s, 21.5 * s, 0.1)
    tip = sd_ellipse(v, 3.4 * s, 4.0 * s, oy=20.5 * s)
    return smooth_min(body, tip, 3.0 * s), v


def draw_kernel(c, s, ox, oy, angle_deg):
    """The kernel's colors: golden brown toasted flesh lit from the top left, patches of its darker papery skin on the
    shaded side, two soft cracks of the toasting, a honey glaze gloss."""
    nut, v = sd_kernel(c, s, ox, oy, angle_deg)
    inside = fill(nut)
    c.over(lerp(hexrgb("F2C27E"), hexrgb("B8732F"), np.clip((22.0 * s - v.y) / (44.0 * s), 0, 1)), inside)
    for px, py, rx, ry, a in ((9.0, -6.0, 9.0, 6.5, 30.0), (-2.0, -15.0, 8.0, 4.5, -10.0), (12.0, 10.0, 5.0, 3.6, 60.0)):
        patch = sd_ellipse(v, rx * s, ry * s, ox=px * s, oy=py * s, angle_deg=a)
        c.over(hexrgb("8E5226"), soft(patch, 1.6 * s) * inside * 0.55)
    for pts in (((-6.0, 14.0), (-3.0, 4.0), (-5.0, -6.0)), ((4.0, 16.0), (7.0, 8.0))):
        crack = sd_polyline(v, [(x * s, y * s) for x, y in pts], 0.8 * s)
        c.over(hexrgb("9A5A28"), fill(crack) * inside * 0.55)
    c.over(hexrgb("8A4A20"), soft(-nut - 5.0 * s, 5.0 * s) * np.clip((v.x + 2.0 * s) / (16.0 * s), 0, 1) * inside * 0.45)
    gl = sd_ellipse(v, 3.4 * s, 7.0 * s, ox=-8.0 * s, oy=6.0 * s, angle_deg=-14.0)
    c.over((255, 255, 255), soft(gl, 1.2 * s) * inside * 0.8)
    c.over(hexrgb("FFF1D2"), fill(sd_circle(v, 1.6 * s, -11.0 * s, -3.0 * s)) * inside * 0.7)
    return nut


def wisp(c, x0, y0, height):
    """A soft wisp of warmth rising: one gentle S, round-capped, a little thinner toward its top."""
    lower = [(x0 + 3.2 * math.sin(math.pi * 1.25 * k / 10.0), y0 + height * k / 10.0) for k in range(6)]
    upper = [(x0 + 3.2 * math.sin(math.pi * 1.25 * k / 10.0), y0 + height * k / 10.0) for k in range(5, 11)]
    return np.minimum(sd_polyline(c, lower, 2.8), sd_polyline(c, upper, 2.2))


def icon_treat_toasted_hazelnut():
    """ICO_Treat_ToastedHazelnut (Noisette grillée, T40's 3-star treat): two warm toasted hazelnut kernels, golden brown
    with patches of their papery skin, two soft cracks and a honey glaze, the front one leaning on the other's side, two
    soft wisps of warmth above them."""
    c = Canvas(128, 128, cy=64.0)
    nuts = [sd_kernel(c, s, x, y, a)[0] for x, y, s, a in KERNELS]
    wisps = [wisp(c, x, y, h) for x, y, h in WISPS]
    sticker(c, union(*nuts), WARM_OUTLINE, WIDTH)
    for (x, y, s, a), nut in zip(KERNELS, nuts):
        edge(c, nut, WARM_OUTLINE)
        draw_kernel(c, s, x, y, a)
    for w in wisps:
        c.over(hexrgb("C98E52"), fill(w - 1.8))
        c.over(vgrad(c, "FFFFFF", "FBE8CC", 44.0, 22.0), fill(w))
    return "ICO_Treat_ToastedHazelnut", c, RUNTIME_ICONS


LEAF = ("B8E68A", "5E9E4A", "4F7A2E", "4F8A3F")  # top, bottom, outline, vein: the clover sprig's green
HAZEL = (2.25, -13.0, -11.0, -10.0)  # the fresh hazelnut's size (s), x, y, tilt (s = 1 is about 26 px wide)
HAZEL_LEAF = (19.0, 19.0, 60.0, 30.0, 38.0)  # the leaf behind it: center x, y, length, width, angle
TWIG = ((-6.5, 24.0), (-3.0, 33.0), (4.0, 39.0))  # the short twig from the nut's tip to the leaf


def hazel_leaf(c):
    """The hazel leaf behind the nut: a broad round leaf with a soft point, its midrib and three pairs of veins."""
    x, y, length, width, angle = HAZEL_LEAF
    v = View(c, x, y, angle)
    leaf = sd_drop(View(c, x, y, angle - 90.0), width * 0.5, length * 0.5, 0.42)
    return leaf, v


def icon_ingredient_hazelnut():
    """ICO_Ingredient_Hazelnut (the Hazelnut ingredient): a fresh hazelnut in its shell (the hazelnut of ICO_Act_NutRain:
    glossy chestnut, its pale rough base), a broad round hazel leaf behind it with its veins, a short twig from its tip.
    Fresh and plain: no glaze, no toasting."""
    c = Canvas(128, 128, cy=64.0)
    s, hx, hy, ha = HAZEL
    nut, _ = sd_hazelnut(c, s, hx, hy, ha)
    leaf, lv = hazel_leaf(c)
    twig = sd_polyline(c, list(TWIG), 2.4)
    sticker(c, union(nut, leaf, twig), WARM_OUTLINE, WIDTH)
    top, bottom, line, vein = LEAF
    edge(c, union(leaf, twig), line)
    c.over(vgrad(c, top, bottom, 47.0, -7.0), fill(leaf))
    c.over(hexrgb("7A5232"), fill(twig))
    _, _, length, width, _ = HAZEL_LEAF
    c.over(hexrgb(vein), fill(sd_polyline(lv, [(-length * 0.48, 0.0), (length * 0.42, 0.0)], 1.0)) * fill(leaf) * 0.75)
    for k in range(3):
        bx = -length * 0.28 + k * length * 0.22
        for side in (-1.0, 1.0):
            vein_line = sd_polyline(lv, [(bx, 0.0), (bx + width * 0.32, side * width * 0.32)], 0.8)
            c.over(hexrgb(vein), fill(vein_line) * fill(leaf) * 0.55)
    top_rim(c, leaf, 23.0, 14.0, 0.45, color=hexrgb("E8F7D2"))
    edge(c, nut, "6E4426")
    draw_hazelnut(c, s, hx, hy, ha)
    return "ICO_Ingredient_Hazelnut", c, RUNTIME_ICONS


BERRY = ("FF8C82", "D9403F", "9C2F35", "B8333A", "FFD6D0")  # top, bottom, outline, lower shade, light
BERRY_Y = -11.0
SEED_ROWS = ((-2.0, (-17.0, -2.0, 13.0)), (-15.0, (-24.0, -9.0, 6.0, 21.0)), (-28.0, (-14.0, 1.0, 15.0)),
             (11.0, (-24.0, -9.0, 6.0, 21.0)), (-39.0, (-5.0, 7.0)))  # row height, seed x (from the berry's center)
CALYX = 5       # leaves of the green crown
CALYX_Y = 25.0  # the crown's center height


def berry_shape(c):
    """The wild strawberry's body: round, a little wider near its top, a soft rounded point below."""
    v = View(c, 0.0, BERRY_Y, 0.0)
    return sd_drop(v, 36.0, 37.0, -0.2), v


def calyx_shapes(c):
    """The green crown on top: five round leaves fanning out from the stem, and the short stem."""
    leaves = []
    for k in range(CALYX):
        a = math.radians(-90.0 + (k - (CALYX - 1) / 2.0) * 34.0 + 180.0)
        lx, ly = math.cos(a) * 15.0, CALYX_Y + math.sin(a) * 6.0 + 2.0
        leaves.append(sd_ellipse(c, 13.0, 7.0, ox=lx, oy=ly, angle_deg=math.degrees(a) * 0.55))
    stem = sd_polyline(c, [(0.0, CALYX_Y + 2.0), (3.0, CALYX_Y + 14.0), (9.0, CALYX_Y + 22.0)], 3.0)
    return union(*leaves), stem


def icon_ingredient_strawberry():
    """ICO_Ingredient_Strawberry (the Strawberry ingredient): a small round wild strawberry, ripe red lit from the top
    left, rows of small round pale seeds each in a soft dimple, a crown of five round green leaves and a short stem."""
    c = Canvas(128, 128, cy=64.0)
    body, v = berry_shape(c)
    crown, stem = calyx_shapes(c)
    sticker(c, union(body, crown, stem), WARM_OUTLINE, WIDTH)
    top, bottom, line, shade, light = BERRY
    edge(c, body, line)
    inside = fill(body)
    c.over(vgrad(c, top, bottom, BERRY_Y + 34.0, BERRY_Y - 40.0), inside)
    lower_shade(c, body, shade, BERRY_Y - 10.0, 26.0, width=7.0, strength=0.5)
    for y, xs in SEED_ROWS:
        for x in xs:
            sx, sy = x, BERRY_Y + y
            dimple = sd_ellipse(c, 3.4, 4.0, ox=sx, oy=sy - 0.6)
            c.over(hexrgb("C23A3D"), fill(dimple) * inside * 0.45)
            seed = sd_ellipse(c, 1.8, 2.5, ox=sx, oy=sy)
            c.over(hexrgb("FBE7A2"), fill(seed) * inside)
    top_rim(c, body, BERRY_Y + 8.0, 26.0, 0.5, color=hexrgb(light))
    gloss(c, 8.5, 4.5, -17.0, BERRY_Y + 17.0, 35.0, inside, 0.6, edge=1.5)
    # The crown and the stem over the berry's top.
    gtop, gbottom, gline, vein = LEAF
    edge(c, union(crown, stem), gline)
    c.over(vgrad(c, gtop, gbottom, CALYX_Y + 10.0, CALYX_Y - 8.0), fill(crown))
    c.over(vgrad(c, "A6DB7A", "5E9E4A", CALYX_Y + 24.0, CALYX_Y), fill(stem))
    top_rim(c, crown, CALYX_Y, 6.0, 0.45, color=hexrgb("E8F7D2"))
    return "ICO_Ingredient_Strawberry", c, RUNTIME_ICONS


T40_SPRITES = (icon_wide_basket, icon_feather, icon_golden_shower, icon_treat_mushroom_biscuit, icon_treat_toasted_hazelnut,
               icon_ingredient_hazelnut, icon_ingredient_strawberry)


def main():
    for folder in (RUNTIME_ICONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T40_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
