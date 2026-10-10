"""Pocket Wilds UI sprites added in T43 (Jardin, the second « meta » activity: a kitchen garden set with its own overlay;
doc 01 Contenu, sections 8 and 9 and S-ACT-006; doc 05 Sensations, sheet 24).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the HUD
icons: rounded filled shapes, one outline in a deeper tone of each material, light from the top left (a vertical gradient,
one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round dots, five-point stars, bubbles,
hearts and paws only (the poppy has five round petals, the sunflower fourteen round petals kept apart, the drop has a
soft round tip; nothing has a four-point outline); nothing religious; nothing enters anything (the berries, the
florets, the grains of the wheat and the dots of the packets stand clear of one another or overlap like real petals,
each with its own outline).

Every plant is drawn once, by its motif function (m_<plant>), at any scale: whole on its crop icon, small in the round
window of its seed packet.

Icons (Art/UI/Icons, shared so the inventory, the wishes and the rewards find them; own colours, never tinted, 128 x 128,
readable at 48 px):
- ICO_Seed_<Plant>, the 13 launch plants: a little paper seed packet, a little turned, its folded flap on top, the plant
  drawn in a round cream window, a band of the plant's dye colour at its foot with a hem of round dots.
- ICO_Crop_<Product>, the products of those plants that are not Bakery ingredients: the clover flower, the daisy, the
  round carrot, the poppy, the lavender (three sprigs tied with a ribbon), the indigo flower, the sunflower, the melon and
  the sweet pea flower. The sprite of a product id crop.<name> is ICO_Crop_<Name>.
- ICO_Ingredient_WheatFlour (a small cloth sack of flour with a wheat ear on it), ICO_Ingredient_Blueberry (three
  blueberries and a leaf), ICO_Ingredient_Apple (a red apple with its leaf), ICO_Ingredient_FlowerSugar (a small wooden
  bowl of pink flower sugar with a little flower on top): the missing Bakery ingredients (ICO_Ingredient_Strawberry and
  ICO_Ingredient_Hazelnut exist, T40). The sprite of ingredient.<name> is ICO_Ingredient_<Name>.
- ICO_Dye_<Name>, the 25 dyes of doc 01 table 9.2: a round drop with a soft round tip, its middle band in the exact
  colour of the table (lighter at the top, a touch deeper at the bottom), an outline in a deeper tone of that colour.
- ICO_WateringCan: the mint watering can of ICO_Act_Garden, tipped a little, three round drops falling from its rose.

Overlay (Art/UI/Activities/Garden, packed by the garden screen's pipeline):
- UI_Garden_LevelPill, 288 x 112, 9-slice (borders 128, 40, 56, 52, TextureImportRules): a cream pill with a round
  leaf-green badge at its left end (the level number sits on it) and a small two-leaf sprout peeking over the badge.
- UI_Garden_BedLabel, 160 x 104, 9-slice (borders 48): the soft rounded cream label floating over a bed (« Planter »,
  « Prêt dans 25 min »), a fine green outline and a soft shadow.
- UI_Garden_CoverSign, 192 x 192: the little wooden sign of a locked bed, two planks on a short post, two round nails,
  a tuft of round leaves at its foot (« Nouvelle parcelle » and its price sit on the board).
- UI_Garden_SeedPacketCard, 192 x 232, 9-slice (borders 64, 72, 64, 64): the seed sheet's card, cream paper, a soft
  leaf-green band at its foot where the count sits.
- UI_Garden_Drop, 64 x 64: a soft water drop (a bed watered).
- UI_Garden_Heart, 64 x 64: a small green heart (neighbours help).

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t43.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (ROOT, Canvas, MASTERS, RUNTIME_ICONS, blur, fill, hexrgb, lerp, sd_circle, sd_ellipse,
                           sd_polygon, sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t34a import sd_heart, sd_leaf
from pw_ui_sprites_t35a import SHADOW, drop_shadow
from pw_ui_sprites_t36 import WARM_OUTLINE, WOOD_OUTLINE, View, clover_head, gloss, sd_arc, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, sd_drop, top_rim, union
from pw_ui_sprites_t42 import daisy, rot

RUNTIME_GARDEN = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Activities", "Garden")

WIDTH = 4.5   # the family's outline on a 128 px icon
LEAF = ("B8E68A", "5E9E4A", "4F7A2E", "4F8A3F")   # top, bottom, outline, vein (T36, T40 to T42)
STEM = ("A6DB7A", "5E9E4A")


# ---------------------------------------------------------------- colour helpers

def shade_of(color, k):
    """A colour mixed toward black (k > 0) or white (k < 0), as hex."""
    r, g, b = hexrgb(color)
    if k >= 0:
        r, g, b = (int(round(v * (1.0 - k))) for v in (r, g, b))
    else:
        r, g, b = (int(round(v + (255 - v) * -k)) for v in (r, g, b))
    return "%02X%02X%02X" % (r, g, b)


def vgrad3(c, top, mid, bottom, y_top, y_mid_top, y_mid_bottom, y_bottom):
    """Vertical gradient: `top` at y_top to `mid` from y_mid_top to y_mid_bottom (flat), then to `bottom` at y_bottom."""
    upper = lerp(hexrgb(top), hexrgb(mid), np.clip((y_top - c.y) / max(y_top - y_mid_top, 1e-6), 0, 1))
    lower = lerp(hexrgb(mid), hexrgb(bottom), np.clip((y_mid_bottom - c.y) / max(y_mid_bottom - y_bottom, 1e-6), 0, 1))
    return np.where((c.y >= y_mid_bottom)[..., None], upper, lower)


# ---------------------------------------------------------------- pieces: a motif is a list of shapes and painters

class Motif:
    """The pieces of one drawing, back to front: each a shape (for the silhouette) and its painter."""

    def __init__(self, c, s):
        self.c, self.s, self.shapes, self.painters = c, s, [], []

    def add(self, d, top, bottom, line, y_top, y_bottom, lw=2.6, shade=None, gloss_at=None, light=None):
        """A filled piece with its own outline (lw x scale), a vertical gradient, an optional lower shade (colour) and an
        optional gloss (rx, ry, x, y, angle)."""
        c, s = self.c, self.s
        self.shapes.append(d)

        def paint():
            c.over(hexrgb(line), fill(d - lw * s))
            c.over(vgrad(c, top, bottom, y_top, y_bottom), fill(d))
            if shade:
                lower_shade(c, d, shade, (y_top + y_bottom) * 0.5, max(abs(y_top - y_bottom) * 0.5, 1.0),
                            width=max(4.0 * s, 1.2), strength=0.5)
            if light:
                top_rim(c, d, (y_top + y_bottom) * 0.5, max(abs(y_top - y_bottom) * 0.5, 1.0), 0.45, color=hexrgb(light))
            if gloss_at:
                rx, ry, gx, gy, ang = gloss_at
                gloss(c, rx * s, ry * s, gx, gy, ang, fill(d), 0.5, edge=max(1.0 * s, 0.4))

        self.painters.append(paint)
        return d

    def paint(self, painter):
        """A painter only (veins, dots, a highlight): drawn in order, no shape."""
        self.painters.append(painter)

    def draw(self, outline=True):
        c, s = self.c, self.s
        whole = union(*self.shapes)
        if outline:
            out = whole - WIDTH * s
            c.over(SHADOW, blur(shift(fill(out), 0, 3.0 * s), max(2.5 * s, 0.8)) * 0.24)
            c.over(hexrgb(WARM_OUTLINE), fill(out))
        for p in self.painters:
            p()
        return whole


def P(ox, oy, s, x, y):
    """A motif point (x, y in motif units) on the canvas."""
    return ox + x * s, oy + y * s


def stem(m, ox, oy, s, points, hw=2.8):
    c = m.c
    pts = [P(ox, oy, s, x, y) for x, y in points]
    d = sd_polyline(c, pts, hw * s)
    ys = [p[1] for p in pts]
    m.add(d, STEM[0], STEM[1], LEAF[2], max(ys), min(ys), lw=2.2)
    return d


def leaf(m, ox, oy, s, x, y, length, width, angle, vein=True):
    """A round leaf (a vesica) with its midrib."""
    c = m.c
    lx, ly = P(ox, oy, s, x, y)
    d = sd_leaf(c, length * s, width * s, lx, ly, angle)
    m.add(d, LEAF[0], LEAF[1], LEAF[2], ly + width * s, ly - width * s, lw=2.4)
    if vein:
        a = math.radians(angle)
        half = length * 0.36 * s

        def rib():
            line = sd_polyline(c, [(lx - math.cos(a) * half, ly - math.sin(a) * half), (lx + math.cos(a) * half, ly + math.sin(a) * half)],
                               max(0.9 * s, 0.35))
            c.over(hexrgb(LEAF[3]), fill(line) * fill(d) * 0.7)

        m.paint(rib)
    return d


# ---------------------------------------------------------------- the 13 plants (motif units: a 128 px icon, about +-46)

def m_clover(c, ox, oy, s):
    """Sweet Clover: a sprig, a round pink blossom of small florets over two round leaves and a short stem."""
    m = Motif(c, s)
    stem(m, ox, oy, s, [(-12.0, -42.0), (-4.0, -20.0), (4.0, 0.0)])
    leaf(m, ox, oy, s, -20.0, -18.0, 26.0, 10.0, 30.0)
    leaf(m, ox, oy, s, 12.0, -30.0, 26.0, 10.0, -25.0)
    hx, hy = P(ox, oy, s, 6.0, 12.0)
    hr = 21.0 * s
    head, florets = clover_head(c, hr, hx, hy)
    m.shapes.append(head)

    def blossom():
        c.over(hexrgb("B4495A"), fill(head - 2.4 * s))
        c.over(hexrgb("E46E8E"), fill(head))
        for ring, x, y, rx, ry, ang in florets:
            f = sd_ellipse(c, rx, ry, ox=x, oy=y, angle_deg=ang)
            light = np.clip((y - hy + hr) / (2.0 * hr) - (x - hx) / (4.0 * hr), 0, 1)
            top, low = ("FFC9D8", "F08BA8") if ring == 0 else ("FFE3EB", "F7AFC4")
            c.over(lerp(hexrgb(low), hexrgb(top), light), fill(f + 0.8 * s))
        c.over((255, 255, 255), fill(sd_circle(c, 2.8 * s, hx - 6.0 * s, hy + 6.5 * s)) * 0.8)

    m.paint(blossom)
    return m


BERRY = ("FF8C82", "D9403F", "9C2F35", "B8333A", "FFD6D0")  # the strawberry of T40: top, bottom, outline, shade, light


def m_strawberry(c, ox, oy, s):
    """Wild Strawberry: a small round berry, rows of round pale seeds, a crown of five round leaves and a short stem."""
    m = Motif(c, s)
    bx, by = P(ox, oy, s, 0.0, -8.0)
    body = sd_drop(View(c, bx, by, 0.0), 32.0 * s, 33.0 * s, -0.2)
    top, bottom, line, shade, light = BERRY
    m.add(body, top, bottom, line, by + 30.0 * s, by - 36.0 * s, lw=2.6, shade=shade, light=light,
          gloss_at=(8.0, 4.2, bx - 15.0 * s, by + 15.0 * s, 35.0))

    def seeds():
        for row_y, xs in ((14.0, (-20.0, -6.0, 8.0, 21.0)), (0.0, (-16.0, -2.0, 12.0)), (-13.0, (-22.0, -8.0, 6.0, 19.0)),
                          (-25.0, (-12.0, 2.0, 14.0)), (-35.0, (-4.0, 7.0))):
            for x in xs:
                sx, sy = bx + x * s, by + row_y * s
                c.over(hexrgb("C23A3D"), fill(sd_ellipse(c, 3.2 * s, 3.8 * s, ox=sx, oy=sy - 0.6 * s)) * fill(body) * 0.45)
                c.over(hexrgb("FBE7A2"), fill(sd_ellipse(c, 1.7 * s, 2.4 * s, ox=sx, oy=sy)) * fill(body))

    m.paint(seeds)
    cy = 22.0
    for k in range(5):
        a = math.radians(-90.0 + (k - 2) * 34.0 + 180.0)
        lx, ly = P(ox, oy, s, math.cos(a) * 14.0, cy + math.sin(a) * 6.0 + 2.0)
        d = sd_ellipse(c, 12.0 * s, 6.5 * s, ox=lx, oy=ly, angle_deg=math.degrees(a) * 0.55)
        m.add(d, LEAF[0], LEAF[1], LEAF[2], ly + 7.0 * s, ly - 7.0 * s, lw=2.2)
    stem(m, ox, oy, s, [(0.0, cy + 2.0), (3.0, cy + 13.0), (9.0, cy + 20.0)], 2.6)
    return m


def m_daisy(c, ox, oy, s):
    """Daisy: one daisy seen from the front (eleven round white petals, a honey heart) on a stem with two leaves."""
    m = Motif(c, s)
    stem(m, ox, oy, s, [(2.0, -44.0), (0.0, -20.0), (0.0, 0.0)])
    leaf(m, ox, oy, s, -13.0, -28.0, 24.0, 8.0, 25.0)
    leaf(m, ox, oy, s, 14.0, -34.0, 24.0, 8.0, -30.0)
    dx, dy = P(ox, oy, s, 0.0, 10.0)
    m.shapes.append(sd_circle(c, 31.0 * s, dx, dy))
    m.paint(lambda: daisy(c, dx, dy, 1.28 * s))
    return m


CARROT = ("FFB867", "E8742A", "B4561E", "D8642A")


def m_carrot(c, ox, oy, s):
    """Round Carrot: a plump round orange root, narrower toward its small tip, soft creases, three leafy fronds."""
    m = Motif(c, s)
    for x, y, length, width, angle in ((-12.0, 26.0, 30.0, 9.0, 115.0), (12.0, 26.0, 30.0, 9.0, 65.0), (0.0, 30.0, 34.0, 9.5, 90.0)):
        leaf(m, ox, oy, s, x, y, length, width, angle)
    rx, ry = P(ox, oy, s, 0.0, -8.0)
    root = smooth_min(sd_drop(View(c, rx, ry, 0.0), 29.0 * s, 28.0 * s, -0.28),
                      sd_polyline(c, [(rx, ry - 24.0 * s), (rx + 2.0 * s, ry - 36.0 * s)], 3.0 * s), 4.0 * s)
    top, bottom, line, shade = CARROT
    m.add(root, top, bottom, line, ry + 26.0 * s, ry - 34.0 * s, shade=shade, gloss_at=(7.0, 4.0, rx - 12.0 * s, ry + 12.0 * s, 30.0))

    def creases():
        for y, half in ((4.0, 10.0), (-8.0, 13.0), (-19.0, 8.0)):
            side = 1.0 if y < 0 else -1.0
            x0 = rx + side * 6.0 * s
            line_d = sd_polyline(c, [(x0 - half * s * 0.5, ry + y * s), (x0 + half * s * 0.5, ry + (y + 1.2) * s)], max(1.0 * s, 0.4))
            c.over(hexrgb(shade), fill(line_d) * fill(root + 2.0 * s) * 0.6)

    m.paint(creases)
    return m


POPPY = ("FF8B78", "D93A33", "9C2A2A", "C23A33")


def m_poppy(c, ox, oy, s):
    """Poppy: an open red poppy of five round petals overlapping like a cup, a dark heart with a ring of round dots, a
    curved stem and one leaf."""
    m = Motif(c, s)
    stem(m, ox, oy, s, [(-4.0, -44.0), (-2.0, -24.0), (0.0, -6.0)], 2.6)
    leaf(m, ox, oy, s, -15.0, -30.0, 24.0, 7.5, 30.0)
    cx, cy = P(ox, oy, s, 0.0, 10.0)
    top, bottom, line, shade = POPPY
    order = (90.0 + 144.0, 90.0 + 216.0, 90.0 + 72.0, 90.0 + 288.0, 90.0)   # the back petals first
    for ang in order:
        a = math.radians(ang)
        px, py = cx + math.cos(a) * 13.0 * s, cy + math.sin(a) * 12.0 * s
        d = sd_circle(c, 17.0 * s, px, py)
        m.add(d, top, bottom, line, py + 17.0 * s, py - 17.0 * s, lw=2.2, shade=shade)
    heart = sd_circle(c, 7.5 * s, cx, cy)
    m.add(heart, "6A4A5A", "3A2630", "2E1E26", cy + 7.5 * s, cy - 7.5 * s, lw=1.6)

    def stamens():
        for k in range(9):
            a = math.tau * k / 9.0
            c.over(hexrgb("2E1E26"), fill(sd_circle(c, 1.5 * s, cx + math.cos(a) * 10.5 * s, cy + math.sin(a) * 10.5 * s)) * 0.9)
        c.over((255, 255, 255), fill(sd_ellipse(c, 6.0 * s, 3.0 * s, ox=cx - 13.0 * s, oy=cy + 15.0 * s, angle_deg=30.0)) * 0.45)

    m.paint(stamens)
    return m


BLUE = ("9DB4F0", "4A5FA8", "2F3E78", "3E4F96")


def m_blueberry(c, ox, oy, s):
    """Blueberry: three round blueberries with their soft bloom and small round crowns, a leaf behind them."""
    m = Motif(c, s)
    leaf(m, ox, oy, s, 16.0, 22.0, 34.0, 11.0, 35.0)
    top, bottom, line, shade = BLUE
    berries = ((-15.0, -12.0, 16.0), (15.0, -14.0, 15.0), (0.0, 10.0, 16.0))
    for x, y, r in berries:
        bx, by = P(ox, oy, s, x, y)
        d = sd_circle(c, r * s, bx, by)
        m.add(d, top, bottom, line, by + r * s, by - r * s, lw=2.4, shade=shade,
              gloss_at=(r * 0.32, r * 0.18, bx - r * 0.4 * s, by + r * 0.42 * s, 30.0))

        def crown(bx=bx, by=by, r=r, d=d):
            cx, cy = bx + r * 0.18 * s, by + r * 0.42 * s
            c.over(hexrgb("2A3466"), fill(sd_circle(c, r * 0.24 * s, cx, cy)) * fill(d))
            c.over(hexrgb("6F80C4"), fill(np.abs(sd_circle(c, r * 0.24 * s, cx, cy)) - 0.7 * s) * fill(d) * 0.8)
            c.over((235, 240, 255), soft(-d - r * 0.35 * s, r * 0.4 * s) * fill(d) * np.clip((c.y - by) / (r * s), 0, 1) * 0.25)

        m.paint(crown)
    return m


LILAC = ("D6BCF5", "8A63C9", "5E3F94")


def m_lavender(c, ox, oy, s, tied=False):
    """Lavender: three sprigs fanning out, each a stem with a spike of small round purple florets; tied with a pink
    ribbon for the crop."""
    m = Motif(c, s)
    sprigs = ((-14.0, 18.0), (0.0, 0.0), (14.0, -18.0))   # top x offset, angle
    for tx, ang in sprigs:
        a = math.radians(90.0 + ang)
        base = (0.0, -42.0)
        top_pt = (base[0] + math.cos(a) * 78.0, base[1] + math.sin(a) * 78.0)
        stem(m, ox, oy, s, [base, ((base[0] + top_pt[0]) * 0.5, (base[1] + top_pt[1]) * 0.5)], 2.2)
        for k in range(6):
            t = 0.42 + k * 0.105
            fx, fy = P(ox, oy, s, base[0] + math.cos(a) * 78.0 * t, base[1] + math.sin(a) * 78.0 * t)
            r = (6.4 - k * 0.55) * s
            for side in (-1, 1):
                nx, ny = -math.sin(a) * side * r * 0.7, math.cos(a) * side * r * 0.7
                d = sd_ellipse(c, r, r * 1.15, ox=fx + nx, oy=fy + ny, angle_deg=ang)
                m.add(d, LILAC[0], LILAC[1], LILAC[2], fy + r, fy - r, lw=1.8)
    if tied:
        bx, by = P(ox, oy, s, 0.0, -20.0)
        band = sd_round_rect(c, 12.0 * s, 4.5 * s, 3.0 * s, ox=bx, oy=by)
        m.add(band, "FFD3DD", "EE8FAA", "B4507A", by + 5.0 * s, by - 5.0 * s, lw=2.0)
        for side in (-1, 1):
            loop = sd_ellipse(c, 7.5 * s, 4.5 * s, ox=bx + side * 15.0 * s, oy=by + 2.0 * s, angle_deg=side * 25.0)
            m.add(loop, "FFD3DD", "EE8FAA", "B4507A", by + 7.0 * s, by - 3.0 * s, lw=2.0)
    return m


WHEAT = ("FFE79A", "E0A43A", "A8702A", "D09034")


def m_wheat(c, ox, oy, s):
    """Golden Wheat: two golden ears, leaning apart, each a stem with plump grains alternating up to a round top grain."""
    m = Motif(c, s)
    top, bottom, line, shade = WHEAT
    for base_x, ang in ((-6.0, 14.0), (6.0, -12.0)):
        a = math.radians(90.0 + ang)
        base = (base_x, -44.0)
        stem(m, ox, oy, s, [base, (base[0] + math.cos(a) * 40.0, base[1] + math.sin(a) * 40.0)], 2.2)
        for k in range(7):
            t = 36.0 + k * 7.5
            gx, gy = base[0] + math.cos(a) * t, base[1] + math.sin(a) * t
            side = -1 if k % 2 == 0 else 1
            if k == 6:
                side = 0
            nx, ny = -math.sin(a) * side * 6.0, math.cos(a) * side * 6.0
            px, py = P(ox, oy, s, gx + nx, gy + ny)
            d = sd_ellipse(c, 4.6 * s, 7.2 * s, ox=px, oy=py, angle_deg=ang + side * 28.0)
            m.add(d, top, bottom, line, py + 7.0 * s, py - 7.0 * s, lw=1.9, shade=shade)
    return m


INDIGO = ("8C9CE8", "3E4C9E", "2A3370")


def m_indigo(c, ox, oy, s):
    """Meadow Indigo: a stem with two oval leaves under a cone of small round deep blue flowers, each with a pale heart."""
    m = Motif(c, s)
    stem(m, ox, oy, s, [(0.0, -44.0), (0.0, 0.0)], 2.6)
    leaf(m, ox, oy, s, -14.0, -30.0, 26.0, 9.0, 20.0)
    leaf(m, ox, oy, s, 14.0, -22.0, 26.0, 9.0, -20.0)
    rows = ((-6.0, (-14.0, 0.0, 14.0)), (6.0, (-10.0, 4.0, 15.0)), (17.0, (-9.0, 5.0)), (28.0, (-3.0, 9.0)), (38.0, (2.0,)))
    flowers = []
    for y, xs in rows:
        for x in xs:
            fx, fy = P(ox, oy, s, x, y)
            r = (7.0 - y * 0.06) * s
            d = sd_circle(c, r, fx, fy)
            m.add(d, INDIGO[0], INDIGO[1], INDIGO[2], fy + r, fy - r, lw=1.9)
            flowers.append((fx, fy, r, d))

    def hearts():
        for fx, fy, r, d in flowers:
            c.over(hexrgb("E3E8FF"), fill(sd_circle(c, r * 0.3, fx + r * 0.1, fy + r * 0.1)) * 0.85)

    m.paint(hearts)
    return m


SUN = ("FFE66E", "F2B12E", "B87A1E")


def m_sunflower(c, ox, oy, s, head_only=False):
    """Sunflower: fourteen round yellow petals kept apart around a big brown heart dotted with round seeds; a stem and a
    leaf below (the packet draws the head only)."""
    m = Motif(c, s)
    cx, cy = P(ox, oy, s, 0.0, 6.0 if not head_only else 0.0)
    if not head_only:
        stem(m, ox, oy, s, [(0.0, -46.0), (0.0, -18.0)], 3.0)
        leaf(m, ox, oy, s, 14.0, -36.0, 26.0, 9.0, -25.0)
    for k in range(14):
        a = math.radians(90.0 + 360.0 * k / 14.0)
        v = View(c, cx + math.cos(a) * 25.0 * s, cy + math.sin(a) * 25.0 * s, math.degrees(a) - 90.0)
        d = sd_drop(v, 5.6 * s, 10.5 * s, -0.3)
        m.add(d, SUN[0], SUN[1], SUN[2], cy + 34.0 * s, cy - 34.0 * s, lw=1.8)
    heart = sd_circle(c, 16.0 * s, cx, cy)
    m.add(heart, "9A6638", "5E3A1E", "4A2E16", cy + 16.0 * s, cy - 16.0 * s, lw=2.0)

    def seeds():
        for ring, count, dist in ((0, 1, 0.0), (1, 6, 6.0), (2, 11, 11.5)):
            for k in range(count):
                a = math.tau * k / count + ring * 0.3
                c.over(hexrgb("C08A50"), fill(sd_circle(c, 1.6 * s, cx + math.cos(a) * dist * s, cy + math.sin(a) * dist * s)) * 0.85)
        c.over((255, 255, 255), fill(sd_ellipse(c, 5.0 * s, 2.6 * s, ox=cx - 7.0 * s, oy=cy + 8.0 * s, angle_deg=30.0)) * 0.35)

    m.paint(seeds)
    return m


APPLE = ("FF9585", "D8403A", "9C2A2A", "B8333A")


def m_apple(c, ox, oy, s):
    """Dwarf Apple: a round red apple with its soft dip, a short brown stem, one green leaf and a big gloss."""
    m = Motif(c, s)
    ax, ay = P(ox, oy, s, 0.0, -6.0)
    body = smooth_min(sd_circle(c, 27.0 * s, ax - 9.0 * s, ay), sd_circle(c, 27.0 * s, ax + 9.0 * s, ay), 10.0 * s)
    body = np.maximum(body, -sd_circle(c, 7.0 * s, ax, ay + 31.0 * s))
    top, bottom, line, shade = APPLE
    stem_d = sd_polyline(c, [(ax, ay + 20.0 * s), (ax + 2.0 * s, ay + 30.0 * s), (ax + 6.0 * s, ay + 36.0 * s)], 2.6 * s)
    m.add(stem_d, "A0703C", "6E4626", "4A2E16", ay + 36.0 * s, ay + 20.0 * s, lw=1.8)
    leaf(m, ox, oy, s, 16.0, 32.0, 28.0, 9.0, 20.0)
    m.add(body, top, bottom, line, ay + 27.0 * s, ay - 27.0 * s, shade=shade,
          gloss_at=(9.0, 5.0, ax - 14.0 * s, ay + 12.0 * s, 35.0))

    def dots():
        for x, y in ((10.0, -4.0), (16.0, -14.0), (4.0, -16.0), (-6.0, -6.0)):
            c.over(hexrgb("FFD0B0"), fill(sd_circle(c, 1.1 * s, ax + x * s, ay + y * s)) * fill(body) * 0.7)

    m.paint(dots)
    return m


MELON = ("D2EBA3", "8DBF5E", "5E8A3A", "6FA84A")


def m_melon(c, ox, oy, s, slice_=False):
    """Round Melon: a round pale green melon with soft deeper stripes, a short curly stem and a small leaf; the crop has
    a slice in front, its peach flesh (the Melon Peach dye), a pale rind and a row of round seeds."""
    m = Motif(c, s)
    mx, my = P(ox, oy, s, -6.0 if slice_ else 0.0, -2.0 if slice_ else -6.0)
    body = sd_circle(c, 33.0 * s, mx, my)
    stem_d = sd_polyline(c, [(mx, my + 30.0 * s), (mx - 2.0 * s, my + 38.0 * s)], 2.8 * s)
    m.add(stem_d, STEM[0], STEM[1], LEAF[2], my + 38.0 * s, my + 30.0 * s, lw=2.0)
    leaf(m, ox, oy, s, 14.0, 32.0, 26.0, 8.5, 15.0)
    top, bottom, line, shade = MELON
    m.add(body, top, bottom, line, my + 33.0 * s, my - 33.0 * s, shade=shade)

    def stripes():
        for k in (-2, -1, 0, 1, 2):
            bend = k * 0.42
            pts = []
            for t in np.linspace(-1.0, 1.0, 13):
                pts.append((mx + (math.sin(bend) * 30.0 * math.cos(t * math.pi / 2.0)) * s, my + t * 31.0 * s))
            band = sd_polyline(c, pts, 2.2 * s)
            c.over(hexrgb("7FB257"), fill(band) * fill(body + 3.0 * s) * 0.6)
        tendril = sd_arc(c, 5.0 * s, 200.0, 520.0, 1.0 * s, mx - 9.0 * s, my + 38.0 * s)
        c.over(hexrgb(LEAF[2]), fill(tendril - 0.8 * s))
        c.over(hexrgb(STEM[0]), fill(tendril))
        gloss(c, 10.0 * s, 5.0 * s, mx - 13.0 * s, my + 16.0 * s, 35.0, fill(body), 0.5, edge=max(1.5 * s, 0.5))

    m.paint(stripes)
    if slice_:
        sx, sy = P(ox, oy, s, 18.0, -26.0)
        v = View(c, sx, sy, -12.0)
        wedge = np.maximum(sd_circle(v, 24.0 * s), v.y)           # a half disc, its flat cut up
        m.add(wedge, "D2EBA3", "6FA84A", MELON[2], sy, sy - 24.0 * s, lw=2.4)

        def flesh():
            inner = np.maximum(sd_circle(v, 19.5 * s), v.y + 1.5 * s)
            c.over(hexrgb("F2F6D8"), fill(inner + 2.0 * s))
            c.over(lerp(hexrgb("FFD2B4"), hexrgb("F7B58B"), np.clip((sy - c.y) / (14.0 * s), 0, 1)), fill(inner))
            for k in range(5):
                a = math.radians(-150.0 + 30.0 * k)
                px, py = 11.0 * math.cos(a), 11.0 * math.sin(a)
                gx, gy = rot(px * s, py * s, -12.0)
                c.over(hexrgb("FFF4E2"), fill(sd_ellipse(c, 1.4 * s, 2.4 * s, ox=sx + gx, oy=sy + gy, angle_deg=math.degrees(a) + 78.0)) * 0.95)
            c.over((255, 255, 255), fill(np.abs(v.y) - 0.9 * s) * fill(wedge) * 0.6)

        m.paint(flesh)
    return m


PEA_PINK = ("FFD0E0", "EE8FB5", "B4507A")
PEA_MAUVE = ("E3CCF6", "A87FD6", "6E4A9E")


def pea_flower(m, cx, cy, s, colours):
    """One sweet pea flower: a big round standard petal, two round wings below it, a small keel."""
    c = m.c
    top, bottom, line = colours
    std = sd_ellipse(c, 15.0 * s, 13.0 * s, ox=cx, oy=cy + 6.0 * s)
    m.add(std, top, bottom, line, cy + 19.0 * s, cy - 7.0 * s, lw=2.0)
    for side in (-1, 1):
        wing = sd_ellipse(c, 9.0 * s, 8.0 * s, ox=cx + side * 8.0 * s, oy=cy - 6.0 * s, angle_deg=side * 20.0)
        m.add(wing, top, bottom, line, cy + 2.0 * s, cy - 14.0 * s, lw=1.9)
    keel = sd_drop(View(c, cx, cy - 12.0 * s, 0.0), 4.0 * s, 6.0 * s, -0.3)
    m.add(keel, "FFF2F6", bottom, line, cy - 6.0 * s, cy - 18.0 * s, lw=1.6)


def m_sweet_pea(c, ox, oy, s):
    """Sweet Pea: two butterfly-like flowers, pink and mauve, on a curved stem with a leaf and a curly tendril."""
    m = Motif(c, s)
    stem(m, ox, oy, s, [(-2.0, -44.0), (2.0, -24.0), (-6.0, -4.0), (-12.0, 10.0)], 2.4)
    leaf(m, ox, oy, s, 14.0, -30.0, 24.0, 8.0, -20.0)
    tx, ty = P(ox, oy, s, 12.0, -10.0)

    def tendril():
        d = sd_arc(c, 6.0 * s, 160.0, 470.0, 1.1 * s, tx, ty)
        c.over(hexrgb(LEAF[2]), fill(d - 0.8 * s))
        c.over(hexrgb(STEM[0]), fill(d))

    m.paint(tendril)
    pea_flower(m, *P(ox, oy, s, -14.0, 22.0), s, PEA_MAUVE)
    pea_flower(m, *P(ox, oy, s, 12.0, 8.0), s, PEA_PINK)
    return m


# The 13 launch plants: sprite part, motif, the dye colour of its packet's band (doc 01 table 9.2).
PLANTS = (
    ("SweetClover", m_clover, "7CC37A"),
    ("WildStrawberry", m_strawberry, "F28CA0"),
    ("Daisy", m_daisy, "F5EBC8"),
    ("RoundCarrot", m_carrot, "F28E3C"),
    ("Poppy", m_poppy, "E2574C"),
    ("Blueberry", m_blueberry, "5A6FB8"),
    ("Lavender", m_lavender, "B79CD9"),
    ("GoldenWheat", m_wheat, "E3B65A"),
    ("MeadowIndigo", m_indigo, "4B5BA6"),
    ("Sunflower", lambda c, ox, oy, s: m_sunflower(c, ox, oy, s, head_only=True), "F4C542"),
    ("DwarfApple", m_apple, "A7D46F"),
    ("RoundMelon", m_melon, "F7B58B"),
    ("SweetPea", m_sweet_pea, "F2C4CE"),
)


# ---------------------------------------------------------------- seed packets

PACKET = (40.0, 50.0, 7.0, -5.0)   # half width, half height, corner radius, turn (degrees)
WINDOW_R = 27.0


def seed_packet(name, motif, colour):
    """ICO_Seed_<Plant>: a little paper seed packet, a little turned: cream paper with its warm outline, the folded flap
    on top (a deeper cream band, its fold line and a soft shade under it), the plant drawn in a round cream window ringed
    in its dye colour, a band of that colour at the foot with a hem of round cream dots."""
    c = Canvas(128, 128, cy=64.0)
    hw, hh, r, turn = PACKET
    v = View(c, 0.0, -1.0, turn)
    body = sd_round_rect(v, hw, hh, r)
    out = body - WIDTH
    c.over(SHADOW, blur(shift(fill(out), 0, 3.0), 2.5) * 0.24)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    c.over(vgrad(c, "FFFAEE", "F2E2C2", hh, -hh), fill(body))
    lower_shade(c, body, "E6D1AA", -20.0, 30.0, width=6.0, strength=0.5)
    flap = np.maximum(body, 34.0 - v.y)
    c.over(vgrad(c, "F4E3C0", "E8D0A4", hh, 30.0), fill(flap))
    c.over(hexrgb("C9A877"), fill(np.abs(v.y - 34.0) - 0.9) * fill(body + 2.0) * 0.9)
    c.over(hexrgb("B48F5C"), soft(-(34.0 - v.y) - 3.0, 3.0) * (v.y < 34.0) * fill(body) * 0.25)
    band_colour = colour
    band = np.maximum(body, v.y + 33.0)
    c.over(lerp(hexrgb(shade_of(band_colour, -0.15)), hexrgb(shade_of(band_colour, 0.08)), np.clip((-33.0 - v.y) / 17.0, 0, 1)), fill(band))
    c.over(hexrgb(shade_of(band_colour, 0.3)), fill(np.abs(v.y + 33.0) - 0.8) * fill(body + 2.0) * 0.8)
    for k in range(7):
        x = -27.0 + 9.0 * k
        px, py = rot(x, -38.5, turn)
        c.over(hexrgb("FFF8EA"), fill(sd_circle(c, 1.8, px, py - 1.0)) * 0.9)
    top_rim(c, body, 30.0, 20.0, 0.6)
    wx, wy = rot(0.0, 2.0, turn)
    wy -= 1.0
    window = sd_circle(c, WINDOW_R, wx, wy)
    c.over(hexrgb(shade_of(band_colour, 0.25)), fill(window - 2.2))
    c.over(vgrad(c, "FFFFFF", "FBF3E4", wy + WINDOW_R, wy - WINDOW_R), fill(window))
    motif(c, wx, wy, 0.5).draw(outline=False)
    gloss(c, 9.0, 3.2, -18.0, 40.0, turn, fill(body), 0.35, edge=1.5)
    return "ICO_Seed_" + name, c, RUNTIME_ICONS


def make_seed(name, motif, colour):
    return lambda: seed_packet(name, motif, colour)


# ---------------------------------------------------------------- crops and ingredients

def crop_icon(prefix, part, motif):
    def build():
        c = Canvas(128, 128, cy=64.0)
        motif(c, 0.0, 0.0, 1.0).draw(outline=True)
        return prefix + part, c, RUNTIME_ICONS
    return build


CROPS = (
    ("CloverFlower", m_clover),
    ("Daisy", m_daisy),
    ("RoundCarrot", m_carrot),
    ("Poppy", m_poppy),
    ("Lavender", lambda c, ox, oy, s: m_lavender(c, ox, oy, s, tied=True)),
    ("IndigoFlower", m_indigo),
    ("Sunflower", m_sunflower),
    ("Melon", lambda c, ox, oy, s: m_melon(c, ox, oy, s, slice_=True)),
    ("SweetPeaFlower", m_sweet_pea),
)


def icon_ingredient_wheat_flour():
    """ICO_Ingredient_WheatFlour: a small round cloth sack of flour, its open mouth folded down into a soft collar, a dome
    of white flour rising out of it, a string tied under the collar, a golden wheat ear printed on its front and a little
    flour spilled at its foot."""
    c = Canvas(128, 128, cy=64.0)
    sack = smooth_min(sd_ellipse(c, 38.0, 32.0, oy=-16.0), sd_round_rect(c, 26.0, 14.0, 8.0, oy=12.0), 12.0)
    collar = sd_ellipse(c, 31.0, 9.0, oy=22.0)
    flour = np.maximum(sd_ellipse(c, 24.0, 16.0, oy=24.0), 22.0 - c.y)
    spill = sd_ellipse(c, 12.0, 4.0, ox=34.0, oy=-46.0)
    out = union(sack, collar, flour, spill) - WIDTH
    c.over(SHADOW, blur(shift(fill(out), 0, 3.0), 2.5) * 0.24)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    c.over(vgrad(c, "FFFFFF", "EDE7DD", -42.0, -50.0), fill(spill))
    c.over(vgrad(c, "FFF9EC", "E3CDA4", 20.0, -48.0), fill(sack))
    lower_shade(c, sack, "D3B784", -28.0, 22.0, width=7.0, strength=0.55)
    for x0 in (-18.0, 16.0):
        crease = sd_polyline(c, [(x0, 6.0), (x0 * 1.15, -12.0), (x0 * 1.05, -30.0)], 1.0)
        c.over(hexrgb("D9C29B"), fill(crease) * fill(sack + 3.0) * 0.6)
    tie = np.maximum(np.abs(c.y - 11.0) - 2.4, sd_round_rect(c, 29.0, 8.0, 4.0, oy=11.0))
    c.over(hexrgb("A8702A"), fill(tie))
    c.over(hexrgb("E9C88E"), fill(np.maximum(np.abs(c.y - 11.6) - 0.9, sd_round_rect(c, 28.0, 8.0, 4.0, oy=11.0))) * 0.8)
    c.over(hexrgb("A8702A"), fill(union(sd_polyline(c, [(22.0, 10.0), (30.0, 2.0)], 1.6), sd_polyline(c, [(22.0, 10.0), (32.0, 8.0)], 1.6))))
    edge(c, collar, "B49A72", width=2.0)
    c.over(vgrad(c, "F6EAD2", "E3CDA4", 30.0, 14.0), fill(collar))
    edge(c, flour, "D8CDBE", width=1.6)
    c.over(vgrad(c, "FFFFFF", "F1ECE4", 40.0, 22.0), fill(flour))
    gloss(c, 7.0, 3.0, -8.0, 32.0, 15.0, fill(flour), 0.6, edge=1.5)
    m_wheat(c, 0.0, -22.0, 0.42).draw(outline=False)
    gloss(c, 8.0, 4.0, -22.0, -4.0, 30.0, fill(sack), 0.35, edge=2.0)
    return "ICO_Ingredient_WheatFlour", c, RUNTIME_ICONS


def icon_ingredient_flower_sugar():
    """ICO_Ingredient_FlowerSugar: a small round wooden bowl heaped with soft pink flower sugar, tiny round white crystals
    on the heap, a little five-petal flower resting on top."""
    c = Canvas(128, 128, cy=64.0)
    rim_y = -4.0
    bowl = np.maximum(sd_ellipse(c, 40.0, 30.0, oy=rim_y - 2.0), c.y - rim_y)
    rim = sd_ellipse(c, 40.0, 8.0, oy=rim_y)
    heap = np.maximum(sd_ellipse(c, 33.0, 26.0, oy=rim_y), rim_y - c.y)
    fx, fy = 6.0, 18.0
    petals = [sd_circle(c, 6.5, fx + math.cos(a) * 7.0, fy + math.sin(a) * 7.0)
              for a in (math.radians(90.0 + 72.0 * k) for k in range(5))]
    flower = union(*petals)
    out = union(bowl, rim, heap, flower) - WIDTH
    c.over(SHADOW, blur(shift(fill(out), 0, 3.0), 2.5) * 0.24)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    c.over(hexrgb(WOOD_OUTLINE), fill(rim))
    c.over(vgrad(c, "8A5A2E", "B87A3C", rim_y + 8.0, rim_y - 8.0), fill(rim + 2.2))
    edge(c, heap, "D9769A", width=2.2)
    c.over(vgrad(c, "FFE6EE", "F5A9C2", 22.0, rim_y), fill(heap))
    for x, y in ((-18.0, 2.0), (-8.0, 10.0), (16.0, 4.0), (24.0, -1.0), (-24.0, -2.0), (-2.0, 0.0), (10.0, -2.0), (-12.0, 16.0)):
        c.over((255, 255, 255), fill(sd_circle(c, 1.6, x, y)) * fill(heap) * 0.9)
    edge(c, bowl, WOOD_OUTLINE, width=2.6)
    c.over(vgrad(c, "E9B97A", "B87A3C", rim_y, -36.0), fill(bowl))
    lower_shade(c, bowl, "9A6234", -18.0, 16.0, width=6.0, strength=0.5)
    for k in range(3):
        y = -14.0 - 7.0 * k
        c.over(hexrgb("C98E52"), fill(np.abs(c.y - y) - 0.8) * fill(bowl + 4.0) * 0.5)
    lip = np.maximum(np.abs(rim) - 2.4, c.y - rim_y)
    c.over(vgrad(c, "F6D7A2", "D9A466", rim_y, rim_y - 9.0), fill(lip))
    for petal in petals:
        c.over(hexrgb("D07A9A"), fill(petal - 1.6))
    for petal in petals:
        c.over(vgrad(c, "FFFFFF", "FFD3E0", fy + 13.0, fy - 13.0), fill(petal))
    core = sd_circle(c, 3.8, fx, fy)
    c.over(hexrgb("C9822F"), fill(core - 1.2))
    c.over(vgrad(c, "FFE27A", "F2A93A", fy + 4.0, fy - 4.0), fill(core))
    gloss(c, 8.0, 3.0, -16.0, -12.0, 15.0, fill(bowl), 0.4, edge=1.5)
    return "ICO_Ingredient_FlowerSugar", c, RUNTIME_ICONS


# ---------------------------------------------------------------- the 25 dyes (doc 01, table 9.2)

DYES = (
    ("CloverGreen", "7CC37A"), ("StrawberryPink", "F28CA0"), ("DaisyCream", "F5EBC8"), ("CarrotOrange", "F28E3C"),
    ("PoppyRed", "E2574C"), ("BlueberryBlue", "5A6FB8"), ("LavenderLilac", "B79CD9"), ("WheatGold", "E3B65A"),
    ("MeadowIndigo", "4B5BA6"), ("SunYellow", "F4C542"), ("AppleGreen", "A7D46F"), ("MelonPeach", "F7B58B"),
    ("PowderPink", "F2C4CE"), ("HazelBrown", "A8754A"), ("WaterBloomPink", "E99AC4"), ("GentianBlue", "3F6FD1"),
    ("LingonberryGarnet", "9E2F46"), ("TropicalGreen", "2FA36B"), ("CactusCoral", "F47C6B"), ("CherryRed", "C8243F"),
    ("GlowTurquoise", "3CC9C0"), ("CloudWhite", "FAFAF7"), ("MoonSilver", "C9D3E3"), ("FernGreen", "5E8C3A"),
    ("NebulaBlue", "5B4FC9"),
)


def sd_water_drop(c, s=1.0, ox=0.0, oy=0.0):
    """A drop: a round belly that rises into a soft round tip (smooth union of a disc and a rounded cone)."""
    belly = sd_circle(c, 31.0 * s, ox, oy - 12.0 * s)
    cone = sd_polygon(c, [(ox - 24.0 * s, oy - 2.0 * s), (ox + 24.0 * s, oy - 2.0 * s), (ox, oy + 36.0 * s)]) - 6.0 * s
    return smooth_min(belly, cone, 9.0 * s)


def dye_drop(name, colour):
    """ICO_Dye_<Name>: a round drop of dye: its middle band in the exact colour of the table (lighter toward its tip, a
    touch deeper at the bottom), a deeper outline of that colour, a soft lower shade, a gloss and a round highlight; a
    small soft puddle of the same colour under it."""
    c = Canvas(128, 128, cy=62.0)
    drop = sd_water_drop(c, 1.0, 0.0, 2.0)
    puddle = sd_ellipse(c, 30.0, 6.0, oy=-43.0)
    out = union(drop, puddle) - WIDTH
    line = "A9A69C" if colour.upper() == "FAFAF7" else shade_of(colour, 0.4)
    c.over(SHADOW, blur(shift(fill(out), 0, 3.0), 2.5) * 0.22)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    c.over(hexrgb(line), fill(puddle - 1.6))
    c.over(vgrad(c, shade_of(colour, -0.25), colour, -38.0, -46.0), fill(puddle))
    c.over(hexrgb(line), fill(drop - 2.6))
    c.over(vgrad3(c, shade_of(colour, -0.35), colour, shade_of(colour, 0.1), 40.0, 4.0, -16.0, -42.0), fill(drop))
    lower_shade(c, drop, shade_of(colour, 0.18), -18.0, 22.0, width=7.0, strength=0.45)
    gloss(c, 6.0, 12.0, -13.0, 4.0, -20.0, fill(drop), 0.55, edge=2.0)
    c.over((255, 255, 255), fill(sd_circle(c, 3.2, -14.0, -20.0)) * 0.75)
    return "ICO_Dye_" + name, c, RUNTIME_ICONS


def make_dye(name, colour):
    return lambda: dye_drop(name, colour)


# ---------------------------------------------------------------- watering can

def icon_watering_can():
    """ICO_WateringCan: the mint watering can of ICO_Act_Garden, tipped a little forward: a round body with a pale band,
    a looped handle, a long spout ending in its rose, three round drops falling from the rose."""
    c = Canvas(128, 128, cy=64.0)
    cv = View(c, -10.0, -2.0, -14.0)
    body = sd_round_rect(cv, 30.0, 24.0, 16.0)
    handle = np.where(cv.x < -6.0, np.abs(np.hypot(cv.x + 24.0, cv.y - 6.0) - 20.0) - 4.6, 1e3)
    handle = np.maximum(handle, -body)
    spout = sd_polyline(cv, [(20.0, 4.0), (44.0, 20.0)], 5.2)
    rose = sd_ellipse(cv, 5.4, 10.5, ox=47.0, oy=22.0, angle_deg=-35.0)
    lid = sd_ellipse(cv, 14.0, 4.5, oy=24.0)
    can = union(body, handle, spout, rose, lid)
    drops = []
    for x, y in ((40.0, 2.0), (50.0, -10.0), (42.0, -20.0)):
        drops.append((sd_drop(View(c, x, y, 0.0), 4.0, 5.6, 0.45), x, y))
    out = union(can, *[d for d, _, _ in drops]) - WIDTH
    c.over(SHADOW, blur(shift(fill(out), 0, 3.0), 2.5) * 0.24)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    edge(c, can, "2F6E78", width=2.6)
    metal = lerp(hexrgb("C4F0EC"), hexrgb("4FA8A8"), np.clip((24.0 - cv.y) / 48.0, 0, 1))
    c.over(metal, fill(union(body, spout, rose)))
    c.over(vgrad(c, "9FDCD8", "3F8E8E", 40.0, 0.0), fill(handle))
    lower_shade(c, body, "3F8E8E", -10.0, 20.0, width=6.0, strength=0.5)
    c.over(hexrgb("2F6E78"), fill(np.abs(rose + 1.8) - 1.0) * 0.6)
    for k in range(-1, 2):
        hx, hy = 47.0 + k * 2.8, 22.0 + k * 4.0
        px = -10.0 + hx * math.cos(math.radians(-14.0)) - hy * math.sin(math.radians(-14.0))
        py = -2.0 + hx * math.sin(math.radians(-14.0)) + hy * math.cos(math.radians(-14.0))
        c.over(hexrgb("2F6E78"), fill(sd_circle(c, 1.1, px, py)) * 0.8)
    c.over(vgrad(c, "E9FBF8", "8FD3CF", 30.0, 18.0), fill(lid))
    c.over(hexrgb("2F6E78"), fill(np.abs(lid) - 0.9) * 0.5)
    band = np.abs(cv.y + 6.0) - 3.0
    c.over(hexrgb("E9FBF8"), fill(band) * fill(body) * 0.6)
    gloss(c, 9.0, 4.0, -24.0, 12.0, -10.0, fill(body), 0.7, edge=1.0)
    for d, x, y in drops:
        c.over(hexrgb("2F5E9E"), fill(d - 2.0))
        c.over(vgrad(c, "E3F3FF", "7FB8EC", y + 6.0, y - 6.0), fill(d))
        c.over((255, 255, 255), fill(sd_circle(c, 1.1, x - 1.2, y + 0.8)) * 0.9)
    return "ICO_WateringCan", c, RUNTIME_ICONS


# ---------------------------------------------------------------- the overlay

PILL = (132.0, 36.0)            # the level pill: half width, half height (canvas origin 64 px from the top, x 144)
BADGE = (-98.0, 0.0, 40.0)      # the badge: x, y, radius


def ui_garden_level_pill():
    """UI_Garden_LevelPill: a cream pill with a soft shadow and warm outline, a round leaf-green badge at its left end (a
    deeper green ring, a light top rim; the level number sits on it) and a small two-leaf sprout peeking over the badge.
    9-slice: the badge and the sprout stay in the left border (128 px), the pill's round end in the right one."""
    c = Canvas(288, 112, cx=144.0, cy=64.0)
    hw, hh = PILL
    pill = sd_round_rect(c, hw, hh, hh, ox=4.0)
    drop_shadow(c, pill, dy=3.0, radius=3.0, strength=0.18)
    c.over(hexrgb("D9C29B"), fill(pill))
    inner = pill + 3.0
    c.over(vgrad(c, "FFFCF5", "F6EAD3", hh, -hh), fill(inner))
    lower_shade(c, inner, "EAD8B8", -10.0, 26.0, width=6.0, strength=0.5)
    top_rim(c, inner, 10.0, 24.0, 0.8)
    bx, by, br = BADGE
    stem_d = sd_polyline(c, [(bx, by + br - 6.0), (bx + 1.0, by + br + 6.0)], 2.4)
    leaf_l = sd_leaf(c, 20.0, 7.0, bx - 9.0, by + br + 7.0, 160.0)
    leaf_r = sd_leaf(c, 22.0, 7.5, bx + 10.0, by + br + 9.0, 22.0)
    sprout = union(stem_d, leaf_l, leaf_r)
    disc = sd_circle(c, br, bx, by)
    drop_shadow(c, union(disc, sprout), dy=3.0, radius=3.0, strength=0.2)
    c.over(hexrgb(LEAF[2]), fill(sprout - 2.2))
    c.over(vgrad(c, LEAF[0], LEAF[1], by + br + 14.0, by + br - 2.0), fill(sprout))
    c.over(hexrgb("4F7A2E"), fill(disc))
    c.over(vgrad(c, "9FD67A", "4F8A3F", by + br, by - br), fill(disc + 3.0))
    c.over(hexrgb("C9EBA4"), fill(np.abs(sd_circle(c, br - 8.0, bx, by)) - 1.0) * 0.55)
    top_rim(c, disc + 3.0, by + 10.0, 24.0, 0.6, color=hexrgb("E3F5CF"))
    gloss(c, 12.0, 5.0, bx - 12.0, by + 20.0, 25.0, fill(disc + 3.0), 0.35, edge=2.0)
    return "UI_Garden_LevelPill", c, RUNTIME_GARDEN


def ui_garden_bed_label():
    """UI_Garden_BedLabel: the soft rounded label floating over a bed: a cream body lit from the top, a fine soft leaf
    green outline, a light top rim and a soft shadow (9-slice, borders 48)."""
    c = Canvas(160, 104, cy=50.0)
    body = sd_round_rect(c, 70.0, 36.0, 30.0)
    drop_shadow(c, body, dy=3.0, radius=3.5, strength=0.2)
    c.over(hexrgb("8EC36B"), fill(body))
    inner = body + 2.5
    c.over(vgrad(c, "FFFDF7", "F7EDD8", 36.0, -36.0), fill(inner))
    lower_shade(c, inner, "EADBBE", -10.0, 26.0, width=6.0, strength=0.45)
    top_rim(c, inner, 10.0, 24.0, 0.85)
    return "UI_Garden_BedLabel", c, RUNTIME_GARDEN


WOOD = ("EBC086", "C08447", "9A6234")


def ui_garden_cover_sign():
    """UI_Garden_CoverSign: the little wooden sign of a locked bed: a board of two soft planks with rounded corners, a soft
    grain, two round nails, on a short post, a tuft of round leaves at the post's foot."""
    c = Canvas(192, 192, cy=96.0)
    board = sd_round_rect(c, 78.0, 46.0, 14.0, oy=22.0)
    post = sd_round_rect(c, 9.0, 34.0, 4.0, oy=-52.0)
    tuft = union(*[sd_ellipse(c, 13.0, 7.0, ox=x, oy=-82.0 + abs(x) * -0.05, angle_deg=a) for x, a in ((-15.0, 35.0), (15.0, -35.0), (0.0, 90.0))])
    out = union(board, post, tuft) - 4.0
    c.over(SHADOW, blur(shift(fill(out), 0, 4.0), 3.5) * 0.24)
    c.over(hexrgb(WOOD_OUTLINE), fill(out))
    c.over(vgrad(c, "D9A466", "9A6234", -18.0, -86.0), fill(post))
    top, bottom, shade = WOOD
    c.over(vgrad(c, top, bottom, 68.0, -24.0), fill(board))
    seam = np.abs(c.y - 22.0) - 1.2
    c.over(hexrgb("8A5A2E"), fill(seam) * fill(board + 3.0) * 0.8)
    for y0 in (48.0, 36.0, 6.0, -8.0):
        pts = [(x, y0 + 1.8 * math.sin(x / 14.0 + y0)) for x in np.linspace(-64.0, 64.0, 17)]
        c.over(hexrgb("B87A3C"), fill(sd_polyline(c, pts, 0.9)) * fill(board + 6.0) * 0.45)
    lower_shade(c, board, shade, 0.0, 26.0, width=6.0, strength=0.4)
    top_rim(c, board, 50.0, 18.0, 0.55, color=hexrgb("FFE3B8"))
    for x in (-60.0, 60.0):
        nail = sd_circle(c, 4.0, x, 22.0)
        c.over(hexrgb("6E5A48"), fill(nail - 1.0))
        c.over(vgrad(c, "E6DCCF", "9C8C7A", 26.0, 18.0), fill(nail))
    c.over(hexrgb(LEAF[2]), fill(tuft - 2.2))
    c.over(vgrad(c, LEAF[0], LEAF[1], -72.0, -90.0), fill(tuft))
    return "UI_Garden_CoverSign", c, RUNTIME_GARDEN


def ui_garden_seed_packet_card():
    """UI_Garden_SeedPacketCard: the seed sheet's card: cream paper lit from the top, a warm fine outline, a soft shadow, a
    light top rim, a soft leaf-green band at its foot where the seed count sits (9-slice, borders 64, 72, 64, 64)."""
    c = Canvas(192, 232, cy=114.0)
    body = sd_round_rect(c, 84.0, 104.0, 22.0)
    drop_shadow(c, body, dy=4.0, radius=3.5, strength=0.2)
    c.over(hexrgb("D9C29B"), fill(body))
    inner = body + 3.0
    c.over(vgrad(c, "FFFCF5", "F8EDD8", 104.0, -104.0), fill(inner))
    band = np.maximum(inner, c.y + 62.0)
    c.over(vgrad(c, "E3F5CF", "C9EBA4", -62.0, -101.0), fill(band))
    c.over(hexrgb("A8D486"), fill(np.abs(c.y + 62.0) - 0.9) * fill(inner) * 0.8)
    lower_shade(c, inner, "B4DC92", -80.0, 24.0, width=6.0, strength=0.4)
    top_rim(c, inner, 80.0, 24.0, 0.8)
    return "UI_Garden_SeedPacketCard", c, RUNTIME_GARDEN


def ui_garden_drop():
    """UI_Garden_Drop: a soft water drop (a bed watered): pale sky to water blue, a deeper blue outline, a gloss and a
    round highlight."""
    c = Canvas(64, 64, cy=32.0)
    drop = sd_water_drop(c, 0.62, 0.0, 2.0)
    c.over(SHADOW, blur(shift(fill(drop - 3.0), 0, 2.0), 1.5) * 0.2)
    c.over(hexrgb("2F5E9E"), fill(drop - 2.6))
    c.over(vgrad(c, "E3F3FF", "6FAEE8", 26.0, -26.0), fill(drop))
    gloss(c, 3.6, 7.0, -7.5, 3.0, -20.0, fill(drop), 0.6, edge=1.2)
    c.over((255, 255, 255), fill(sd_circle(c, 2.0, -8.5, -11.0)) * 0.7)
    return "UI_Garden_Drop", c, RUNTIME_GARDEN


def ui_garden_heart():
    """UI_Garden_Heart: a small leaf-green heart (neighbours help): a deeper green outline, lit from the top, a gloss."""
    c = Canvas(64, 64, cy=32.0)
    heart = sd_heart(c, 46.0, 0.0, 1.0)
    c.over(SHADOW, blur(shift(fill(heart - 3.0), 0, 2.0), 1.5) * 0.2)
    c.over(hexrgb(LEAF[2]), fill(heart - 2.6))
    c.over(vgrad(c, LEAF[0], LEAF[1], 20.0, -22.0), fill(heart))
    lower_shade(c, heart, LEAF[3], -6.0, 16.0, width=4.0, strength=0.4)
    gloss(c, 5.0, 2.6, -8.0, 9.0, 35.0, fill(heart), 0.6, edge=1.0)
    return "UI_Garden_Heart", c, RUNTIME_GARDEN


T43_SPRITES = (tuple(make_seed(n, m, col) for n, m, col in PLANTS) +
               tuple(crop_icon("ICO_Crop_", n, m) for n, m in CROPS) +
               (crop_icon("ICO_Ingredient_", "Blueberry", m_blueberry), crop_icon("ICO_Ingredient_", "Apple", m_apple),
                icon_ingredient_wheat_flour, icon_ingredient_flower_sugar) +
               tuple(make_dye(n, col) for n, col in DYES) +
               (icon_watering_can, ui_garden_level_pill, ui_garden_bed_label, ui_garden_cover_sign,
                ui_garden_seed_packet_card, ui_garden_drop, ui_garden_heart))


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_GARDEN, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T43_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
