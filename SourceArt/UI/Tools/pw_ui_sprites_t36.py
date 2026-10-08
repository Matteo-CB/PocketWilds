"""Pocket Wilds UI sprites added in T36 (Rendez-vous du jour: welcome gifts, daily gift box, 28-day calendar, wishes,
weekly journal, return gift; doc 01 « Contenu des rendez-vous, des activités et de l'accueil », sections 2 to 6).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the
HUD icons: rounded filled shapes, one warm outline per icon, light from the top left (a vertical or side gradient, one
deeper shade, one gloss), a soft warm drop shadow below. Owner rules: round dots, five-point stars, hearts, paws and
pearls only (no four-point sparkle), nothing religious, no bow on the gift box (a round wooden box with a paw).

Icons (own colors, Atlas_CommonHUD, readable at 24 and 48 dp):
- ICO_Today: the right-rail button of the day's rendez-vous, a day page with a paw and a little sun rising behind it.
- ICO_Gift: the round wooden gift box with a paw on its lid, a soft rose band and a heart tag (the three layers
  below, in one image).
- ICO_Wish: a soft thought bubble holding a small heart.
- ICO_Wish_Done: a plump green check with a little heart (a wish come true).
- ICO_Calendar_Days: a calendar page whose round day boxes fill with paw stamps, the last one a star.
- ICO_WeeklyJournal: a small notebook with an elastic band, a ribbon bookmark and a paw.
- ICO_Ticket: an activity ticket with round notches, a dotted tear line and a star.
- ICO_Seed: a plump seed opening its first two leaves.
- ICO_Treat: a heart biscuit with pink icing (any treat).
- ICO_Treat_CloverCrunch: the Prairie treat, a round crunchy biscuit topped with a sweet clover sprig.
- ICO_Swap: two round arrows chasing each other (swap a wish).
- ICO_Decoration: a terracotta flower pot (a decoration item).

Kit (Atlas_CommonHUD):
- UI_GiftBox_Base, UI_GiftBox_Lid, UI_GiftBox_Band: the big gift box of the Today screen, three layers on one 256 px
  frame. The base holds the shadow, the body, the band's outline, the heart tag and the open top seen when the lid
  lifts; the lid covers that top exactly; the band is white and greys so the UI tints it cream, sky blue or gold by
  gift size.
- UI_Bubble_Wish: the world bubble above an animal, white and greys for tinting, two trailing dots toward the animal.
  The bubble's circle is centered on the frame so the ring fills around the image center.
- UI_Bubble_Ring: the progress ring of the bubble, same frame, a full white ring (Image fill Radial 360 or the shader
  cuts it by angle).
- UI_Calendar_Box: one day box of the 28-day strip, white and greys for tinting, a header for the day number.
- UI_PawStamp: the paw stamp put on a claimed day, a rose ink stamp (ring, paw, a few dry spots).
- UI_Journal_Chest: the weekly journal's completion chest, round wooden chest with a golden heart clasp.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t36.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, RUNTIME_KIT, blur, fill, hexrgb, lerp, normalize, sd_circle,
                           sd_ellipse, sd_polygon, sd_polyline, sd_round_rect, shift, soft, star_points)
from pw_ui_sprites_t34a import sd_heart, sd_leaf, sd_paw
from pw_ui_sprites_t35a import drop_shadow, warm_orb

WOOD_OUTLINE = "5E3F24"
WARM_OUTLINE = "8A5A2E"
TINT_OUTLINE = (146, 104, 72)  # the HUD's warm brown kept in tinted kit sprites (as UI_Egg_Shell)
SHADOW_TAG = (80, 52, 20)


# ---------------------------------------------------------------- helpers

class View:
    """The canvas grid seen through a move, a rotation and a squash, for shapes lying on a tilted or foreshortened
    surface. Ellipse distances stay screen distances (normalized on the grid); circles and rounded rectangles are
    exact under a plain rotation (sx = sy = 1)."""

    def __init__(self, c, ox=0.0, oy=0.0, angle_deg=0.0, sx=1.0, sy=1.0):
        a = math.radians(angle_deg)
        x, y = c.x - ox, c.y - oy
        self.x = (x * math.cos(a) + y * math.sin(a)) / sx
        self.y = (-x * math.sin(a) + y * math.cos(a)) / sy


def rotate_points(points, angle_deg, ox=0.0, oy=0.0):
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(ox + x * ca - y * sa, oy + x * sa + y * ca) for x, y in points]


def vgrad(c, top, bottom, y_top, y_bottom):
    """Vertical gradient from `top` (at y_top) to `bottom` (at y_bottom)."""
    return lerp(hexrgb(top), hexrgb(bottom), np.clip((y_top - c.y) / (y_top - y_bottom), 0, 1))


def sticker(c, shape, color, width, dy=4.0, radius=3.0, strength=0.28):
    """The family's outline: the shape grown by `width` in one warm color over a soft drop shadow."""
    outline = shape - width
    drop_shadow(c, outline, dy, radius, strength)
    c.over(hexrgb(color), fill(outline))
    return outline


def gloss(c, rx, ry, ox, oy, angle_deg, mask, strength=0.8, edge=0.0):
    g = sd_ellipse(c, rx, ry, ox=ox, oy=oy, angle_deg=angle_deg)
    c.over((255, 255, 255), (soft(g, edge) if edge > 0 else fill(g)) * mask * strength)


def sd_arc(c, r, a0_deg, a1_deg, half_width, ox=0.0, oy=0.0):
    """Stroke along a circle arc from a0 to a1 (degrees, counterclockwise), round caps."""
    x, y = c.x - ox, c.y - oy
    angle = np.degrees(np.arctan2(y, x))
    span = (a1_deg - a0_deg) % 360.0
    on = ((angle - a0_deg) % 360.0) <= span
    a0, a1 = math.radians(a0_deg), math.radians(a1_deg)
    ends = np.minimum(np.hypot(x - r * math.cos(a0), y - r * math.sin(a0)), np.hypot(x - r * math.cos(a1), y - r * math.sin(a1)))
    return np.where(on, np.abs(np.hypot(x, y) - r), ends) - half_width


def sd_cylinder(c, a, b, y_top, y_bottom, ox=0.0):
    """A round box seen from slightly above: its side and its top and bottom ellipses (a wide, b deep)."""
    side = sd_round_rect(c, a, (y_top - y_bottom) * 0.5, 0.0, ox=ox, oy=(y_top + y_bottom) * 0.5)
    return np.minimum(side, np.minimum(sd_ellipse(c, a, b, ox=ox, oy=y_top), sd_ellipse(c, a, b, ox=ox, oy=y_bottom)))


def front_curve(c, a, b, height, ox=0.0):
    """Height of the front edge of a horizontal circle (ellipse a x b around `height`) at each column."""
    return height - b * np.sqrt(np.clip(1.0 - ((c.x - ox) / a) ** 2, 0.0, 1.0))


def sd_band(c, a, b, low, high, ox=0.0):
    """A strip around a cylinder between two heights, bounded by the front curves (approximate distance)."""
    return normalize(np.maximum(front_curve(c, a, b, low, ox) - c.y, c.y - front_curve(c, a, b, high, ox)))


def sd_rounded_star(c, outer, inner, ox=0.0, oy=0.0, rounding=3.0, angle_deg=0.0):
    """A plump five-point star (the project's star), rounded by `rounding` px."""
    points = rotate_points(star_points(outer, inner), angle_deg, ox, oy)
    return sd_polygon(c, points) - rounding


def side_shade(c, light, dark, a, ox=0.0):
    """Cylinder shading: light on the left (the light comes from the top left), deeper toward the right edge."""
    return lerp(hexrgb(light), hexrgb(dark), np.clip((c.x - ox + 0.45 * a) / (1.45 * a), 0, 1) ** 1.2)


def tinted(grey, tint):
    """Greys (HxWx3, 0-1) multiplied by a tint color, the way the UI's Image color tints a white sprite."""
    if tint is None:
        return grey
    return grey * (np.array(hexrgb(tint), dtype=np.float64) / 255.0)


def page_rings(c, xs, y, scale=1.0):
    """The two binder rings across the top edge of a day page."""
    for x in xs:
        ring = sd_round_rect(c, 3.6 * scale, 8.5 * scale, 3.6 * scale, ox=x, oy=y)
        c.over(hexrgb("5B4232"), fill(ring - 3.0 * scale))
        c.over(lerp(hexrgb("FFFFFF"), hexrgb("D9D2C6"), np.clip((y + 8.0 * scale - c.y) / (16.0 * scale), 0, 1)), fill(ring))


# ---------------------------------------------------------------- the day page icons

def icon_today():
    """ICO_Today: a cream day page with a coral header and a big paw, a warm sun rising behind its top edge (half an
    orb and a fan of long round rays, the Light's sun)."""
    c = Canvas(128, 128, cy=64.0)
    px, py, hw, hh = 0.0, -16.0, 42.0, 33.0
    top = py + hh
    rays = None
    for k in range(5):
        a = math.radians(26.0 + 32.0 * k)
        d = sd_ellipse(c, 4.6, 8.0, ox=px + math.cos(a) * 31.0, oy=top + math.sin(a) * 31.0, angle_deg=math.degrees(a) - 90.0)
        rays = d if rays is None else np.minimum(rays, d)
    orb = sd_circle(c, 20.0, px, top + 2.0)
    sun = np.minimum(orb, rays)
    page = sd_round_rect(c, hw, hh, 12.0, ox=px, oy=py)
    drop_shadow(c, np.minimum(sun, page) - 4.5)
    # The sun first: its outline, rays and orb; the page then lays its own outline over it.
    c.over(hexrgb("E0892C"), fill(sun - 4.5))
    c.over(vgrad(c, "FFE48A", "F7B23B", top + 40.0, top + 10.0), fill(rays))
    c.over((255, 255, 255), soft(-rays - 2.5, 1.5) * fill(rays) * 0.45)
    warm_orb(c, 20.0, px, top + 2.0)
    c.over(hexrgb("5B4232"), fill(page - 4.5))
    split = top - 15.0
    c.over(vgrad(c, "FFFBF0", "F3E2C4", top, py - hh), fill(page))
    c.over(hexrgb("E6CFA6"), soft(-page - 7.0, 7.0) * np.clip((py - c.y) / hh, 0, 1) * fill(page) * 0.7)
    header = fill(page) * np.clip(0.5 + (c.y - split) * 4.0, 0, 1)
    c.over(vgrad(c, "FFB49A", "F2826A", top, split), header)
    c.over(hexrgb("D9634F"), fill(np.abs(c.y - split) - 1.1) * fill(page) * 0.8)
    c.over((255, 255, 255), soft(-page - 2.5, 2.0) * header * np.clip((c.y - top + 7.0) / 7.0, 0, 1) * 0.6)
    for x in (px - 27.0, px + 27.0):
        c.over(hexrgb("C9584A"), fill(sd_circle(c, 3.0, x, top - 7.5)))
    c.over(hexrgb("E0892C"), fill(sd_paw(c, 1.12, px, py - 7.5)))
    gloss(c, 4.0, 9.0, px - 30.0, py + 0.0, -6.0, fill(page), 0.5, edge=2.0)
    return "ICO_Today", c, RUNTIME_ICONS


def icon_calendar_days():
    """ICO_Calendar_Days: a cream calendar page with a green header and nine round day boxes: the first five carry a
    rose paw stamp, the next ones wait, the last one is a golden star (a big day)."""
    c = Canvas(128, 128, cy=63.0)
    px, py, hw, hh = 0.0, -4.0, 44.0, 42.0
    page = sd_round_rect(c, hw, hh, 12.0, ox=px, oy=py)
    sticker(c, page, "5B4232", 4.5)
    top = py + hh
    c.over(vgrad(c, "FFFBF0", "F3E2C4", top, py - hh), fill(page))
    c.over(hexrgb("E6CFA6"), soft(-page - 7.0, 7.0) * np.clip((py - c.y) / hh, 0, 1) * fill(page) * 0.7)
    split = top - 16.0
    header = fill(page) * np.clip(0.5 + (c.y - split) * 4.0, 0, 1)
    c.over(vgrad(c, "A6DB7A", "6FAE52", top, split), header)
    c.over(hexrgb("4F8A3F"), fill(np.abs(c.y - split) - 1.1) * fill(page) * 0.8)
    c.over((255, 255, 255), soft(-page - 2.5, 2.0) * header * np.clip((c.y - top + 8.0) / 8.0, 0, 1) * 0.6)
    page_rings(c, (px - 20.0, px + 20.0), top)
    k = 0
    for row in range(3):
        for col in range(3):
            x, y = px - 25.0 + col * 25.0, split - 14.0 - row * 21.0
            if k < 5:
                dot = sd_circle(c, 8.2, x, y)
                c.over(hexrgb("C24F72"), fill(dot - 1.8))
                c.over(lerp(hexrgb("FF9FB5"), hexrgb("EC6E8C"), np.clip((y + 8.0 - c.y) / 16.0, 0, 1)), fill(dot))
                c.over(hexrgb("FFF1F4"), fill(sd_paw(c, 0.36, x, y - 0.6)) * 0.95)
            elif k < 8:
                ring = np.abs(sd_circle(c, 7.4, x, y)) - 1.5
                c.over(hexrgb("D7BF96"), fill(ring))
            else:
                star = sd_rounded_star(c, 12.5, 6.2, x, y - 0.5, rounding=1.5)
                c.over(hexrgb("C9782A"), fill(star - 2.0))
                c.over(lerp(hexrgb("FFE48A"), hexrgb("F7B23B"), np.clip((y + 11.0 - c.y) / 22.0, 0, 1)), fill(star))
            k += 1
    gloss(c, 4.0, 10.0, px - 34.0, py + 6.0, -6.0, fill(page), 0.5, edge=2.0)
    return "ICO_Calendar_Days", c, RUNTIME_ICONS


# ---------------------------------------------------------------- the round wooden gift box

class GiftBox:
    """Geometry of the round wooden gift box, in icon units (s = 1 is about 92 px wide), shared by ICO_Gift and the
    three Today screen layers so they always match."""

    def __init__(self, s):
        self.s = s
        self.a, self.b = 38.0 * s, 12.0 * s           # body radius and depth
        self.top, self.bottom = 3.0 * s, -46.0 * s    # body side
        self.la, self.lb = 42.5 * s, 13.5 * s         # lid radius and depth
        self.lid_low, self.lid_high = -1.0 * s, 11.0 * s
        self.band_low, self.band_high = -33.5 * s, -21.0 * s
        self.width = 3.9 * s                          # outline (4.5 px on the 128 px icon, as the family)

    def body(self, c):
        return sd_cylinder(c, self.a, self.b, self.top, self.bottom)

    def lid(self, c):
        return sd_cylinder(c, self.la, self.lb, self.lid_high, self.lid_low)

    def band(self, c):
        return np.maximum(sd_band(c, self.a, self.b, self.band_low, self.band_high), self.body(c))

    def draw_base(self, c):
        s, w = self.s, self.width
        body = self.body(c)
        drop_shadow(c, body - w, dy=4.5 * s, radius=3.2 * s, strength=0.3)
        c.over(hexrgb(WOOD_OUTLINE), fill(body - w))
        inside = fill(body)
        c.over(side_shade(c, "F7DBAE", "C99A62", self.a), inside)
        c.over(hexrgb("B98652"), soft(-body - 6.0 * s, 6.0 * s) * np.clip((self.bottom - c.y + 6.0 * s) / (16.0 * s), 0, 1) * inside * 0.6)
        # Bent wood: two thin grain lines following the curve, above and below the band.
        for h in (self.band_high + 7.0 * s, self.band_low - 6.5 * s):
            line = np.abs(c.y - front_curve(c, self.a, self.b, h)) - 0.75 * s
            c.over(hexrgb("BC8A55"), fill(line) * inside * 0.5)
        gloss(c, 3.6 * s, 11.0 * s, -self.a * 0.62, self.band_low - 2.0 * s, 0.0, inside, 0.35, edge=2.0 * s)
        # The band's outline lies on the base so the tinted band keeps a brown edge.
        c.over(hexrgb(WOOD_OUTLINE), fill(np.maximum(self.band(c) - 1.9 * s, body)) * inside)
        # The open top, seen when the lid lifts: a light wood rim around a deep warm inside.
        rim = sd_ellipse(c, self.a, self.b, oy=self.top)
        hole = sd_ellipse(c, self.a - 5.0 * s, self.b - 3.6 * s, oy=self.top + 0.6 * s)
        c.over(vgrad(c, "FBE6C4", "E2B87F", self.top + self.b, self.top - self.b), fill(rim))
        c.over(hexrgb("8E5E34"), fill(np.abs(hole) - 0.9 * s) * 0.8)
        c.over(vgrad(c, "4E321C", "7A4E2A", self.top + self.b, self.top - self.b), fill(hole))
        self.draw_tag(c)
        return body

    def draw_tag(self, c):
        """A round gift tag with a heart, hanging on a short string from under the band's lower edge."""
        s = self.s
        tx = 0.46 * self.a
        edge = self.band_low - self.b * math.sqrt(1.0 - 0.46 * 0.46)
        cx, cy, r = tx + 3.0 * s, edge - 8.5 * s, 6.8 * s
        string = sd_polyline(c, [(tx, edge + 2.0 * s), (cx, cy + r)], 0.9 * s)
        tag = sd_circle(c, r, cx, cy)
        c.over(hexrgb(WOOD_OUTLINE), fill(string))
        c.over(SHADOW_TAG, blur(shift(fill(tag - 1.8 * s), 0.8 * s, 1.6 * s), 1.2 * s) * 0.35)
        c.over(hexrgb(WOOD_OUTLINE), fill(tag - 1.8 * s))
        c.over(lerp(hexrgb("FFFBF0"), hexrgb("F1DEC0"), np.clip((cy + r - c.y) / (2.0 * r), 0, 1)), fill(tag))
        c.over(hexrgb("D99A62"), fill(sd_circle(c, 1.1 * s, cx, cy + r * 0.62)))
        heart_view = View(c, cx, cy - 0.6 * s, 12.0)
        heart = sd_heart(heart_view, 7.4 * s, 0.0, 0.0)
        c.over(lerp(hexrgb("FF9FB8"), hexrgb("EC6E8C"), np.clip((cy + 3.0 * s - c.y) / (7.0 * s), 0, 1)), fill(heart))

    def draw_band(self, c, tint=None):
        s = self.s
        band = self.band(c)
        inside = fill(band)
        grey = lerp((255, 255, 255), (206, 206, 206), np.clip((c.x + 0.4 * self.a) / (1.4 * self.a), 0, 1) ** 1.3)
        c.over(tinted(grey, tint), inside)
        # A lighter upper edge and a soft lower shade give the strip its thickness.
        upper = np.abs(c.y - front_curve(c, self.a, self.b, self.band_high - 1.6 * s)) - 0.9 * s
        c.over(tinted(np.ones_like(grey), tint) ** 0.35, fill(upper) * inside * 0.75)
        lower = soft(c.y - front_curve(c, self.a, self.b, self.band_low + 2.5 * s), 2.5 * s)
        c.over(tinted(np.full_like(grey, 0.80), tint), lower * inside * 0.6)
        # Three round pearls along the band, the gift's only ornament (no bow).
        for k, x in enumerate((-0.52, 0.0, 0.52)):
            bx = x * self.a
            by = (self.band_low + self.band_high) * 0.5 - self.b * math.sqrt(1.0 - x * x)
            pearl = sd_circle(c, 2.9 * s * (1.0 if k == 1 else 0.85), bx, by)
            c.over(tinted(np.full_like(grey, 0.74), tint), fill(pearl - 0.9 * s) * inside)
            c.over(tinted(np.ones_like(grey), tint) ** 0.2, fill(pearl) * inside)
        return band

    def draw_lid(self, c):
        s, w = self.s, self.width
        lid = self.lid(c)
        c.over(hexrgb(WOOD_OUTLINE), fill(lid - w))
        side = fill(lid)
        c.over(side_shade(c, "F8DEB4", "CD9E68", self.la), side)
        c.over(hexrgb("BC8A55"), fill(np.abs(c.y - front_curve(c, self.la, self.lb, self.lid_low + 5.5 * s)) - 0.7 * s) * side * 0.45)
        c.over(hexrgb("B98652"), soft(-lid - 3.0 * s, 3.0 * s) * np.clip((self.lid_low - c.y + 4.0 * s) / (8.0 * s), 0, 1) * side * 0.6)
        top = sd_ellipse(c, self.la, self.lb, oy=self.lid_high)
        c.over(hexrgb("C08E58"), fill(np.abs(top) - 0.9 * s) * side * 0.9)
        face = fill(top + 0.9 * s)
        c.over(vgrad(c, "FFEDD0", "F2CE99", self.lid_high + self.lb, self.lid_high - self.lb), face)
        c.over(hexrgb("E2B87F"), soft(-top - 4.0 * s, 4.0 * s) * np.clip((self.lid_high - c.y) / self.lb, 0, 1) * face * 0.5)
        # The paw lies on the lid, foreshortened like the top it is painted on.
        paw_view = View(c, 0.0, self.lid_high - 0.4 * s, 0.0, 1.0, 0.5)
        paw = sd_paw(paw_view, 0.92 * s, 0.0, 0.0)
        c.over(hexrgb("B98049"), fill(paw) * face)
        gloss(c, 9.0 * s, 2.6 * s, -self.la * 0.42, self.lid_high + self.lb * 0.48, 8.0, face, 0.55, edge=1.5 * s)
        gloss(c, 2.6 * s, 3.6 * s, -self.la * 0.7, (self.lid_low + self.lid_high) * 0.5 - 2.0 * s, 0.0, side, 0.35, edge=1.2 * s)
        return lid


def icon_gift():
    """ICO_Gift: the round wooden gift box, paw on the lid, a soft rose band with three pearls, a heart tag. No bow."""
    c = Canvas(128, 128, cy=42.0)
    box = GiftBox(1.16)
    box.draw_base(c)
    box.draw_band(c, tint="F7A8BE")
    box.draw_lid(c)
    return "ICO_Gift", c, RUNTIME_ICONS


GIFT_SIZE, GIFT_SCALE, GIFT_CY = 256, 2.45, 82.0


def kit_gift_base():
    """UI_GiftBox_Base: shadow, body, the band's brown edges, the heart tag and the open top (seen when the lid lifts)."""
    c = Canvas(GIFT_SIZE, GIFT_SIZE, cy=GIFT_CY)
    GiftBox(GIFT_SCALE).draw_base(c)
    return "UI_GiftBox_Base", c, RUNTIME_KIT


def kit_gift_lid():
    """UI_GiftBox_Lid: the lid with its paw, on the base's frame; it covers the open top exactly."""
    c = Canvas(GIFT_SIZE, GIFT_SIZE, cy=GIFT_CY)
    GiftBox(GIFT_SCALE).draw_lid(c)
    return "UI_GiftBox_Lid", c, RUNTIME_KIT


def kit_gift_band():
    """UI_GiftBox_Band: the band in white and greys (tinted cream, sky blue or gold by gift size), on the same frame."""
    c = Canvas(GIFT_SIZE, GIFT_SIZE, cy=GIFT_CY)
    GiftBox(GIFT_SCALE).draw_band(c)
    return "UI_GiftBox_Band", c, RUNTIME_KIT


# ---------------------------------------------------------------- wishes

def icon_wish():
    """ICO_Wish: a soft round thought bubble with two trailing dots, a small pink heart inside."""
    c = Canvas(128, 128, cy=64.0)
    bx, by, r = 8.0, 10.0, 39.0
    bubble = sd_circle(c, r, bx, by)
    dot1 = sd_circle(c, 8.5, -32.0, -30.0)
    dot2 = sd_circle(c, 5.0, -46.0, -46.0)
    shape = np.minimum(bubble, np.minimum(dot1, dot2))
    sticker(c, shape, "6E7FB0", 4.5)
    body = fill(shape)
    c.over(vgrad(c, "FFFFFF", "DCE9F7", by + r, by - r), body)
    c.over(hexrgb("C6D8EE"), soft(-bubble - 6.0, 6.0) * np.clip((by - c.y) / r, 0, 1) * fill(bubble) * 0.7)
    heart = sd_heart(c, 40.0, bx, by - 1.0)
    c.over(hexrgb("C24F72"), fill(heart - 3.0))
    c.over(lerp(hexrgb("FFA3BE"), hexrgb("F0668E"), np.clip((by + 16.0 - c.y) / 34.0, 0, 1)), fill(heart))
    gloss(c, 4.2, 2.6, bx - 8.0, by + 7.5, 35.0, fill(heart), 0.85)
    gloss(c, 6.0, 11.0, bx - 22.0, by + 16.0, -32.0, fill(bubble), 0.85, edge=2.0)
    return "ICO_Wish", c, RUNTIME_ICONS


def icon_wish_done():
    """ICO_Wish_Done: a plump green check, a little pink heart resting in its upper left."""
    c = Canvas(128, 128, cy=68.0)
    check = sd_polyline(c, [(-34.0, -3.0), (-11.0, -26.0), (35.0, 20.0)], 10.5)
    hx, hy = -28.0, 33.0
    heart = sd_heart(c, 42.0, hx, hy)
    sticker(c, np.minimum(check, heart), "3F7A33", 4.5)
    c.over(vgrad(c, "B4E38A", "5E9E4A", 30.0, -37.0), fill(check))
    c.over(hexrgb("4F8A3F"), soft(-check - 3.0, 3.0) * np.clip((-6.0 - c.y) / 22.0, 0, 1) * fill(check) * 0.5)
    c.over(hexrgb("D5F2B8"), fill(sd_polyline(c, [(-28.0, 0.0), (-15.0, -13.0)], 2.4)) * fill(check) * 0.8)
    c.over(hexrgb("D5F2B8"), fill(sd_polyline(c, [(-3.0, -12.0), (28.0, 19.0)], 2.4)) * fill(check) * 0.8)
    # The heart sits over the check's end: its own rose outline, then the fill.
    c.over(hexrgb("C24F72"), fill(heart - 4.0))
    c.over(lerp(hexrgb("FFA3BE"), hexrgb("F0668E"), np.clip((hy + 17.0 - c.y) / 34.0, 0, 1)), fill(heart))
    gloss(c, 4.4, 2.6, hx - 8.0, hy + 7.0, 35.0, fill(heart), 0.85)
    return "ICO_Wish_Done", c, RUNTIME_ICONS


BUBBLE_W, BUBBLE_H = 144, 224
BUBBLE_R = 58.0


def bubble_dots(c):
    return np.minimum(sd_circle(c, 10.0, -18.0, -77.0), sd_circle(c, 6.2, -31.0, -94.0))


def kit_bubble_wish():
    """UI_Bubble_Wish: the round world bubble above an animal, white and greys for tinting, the HUD's brown outline,
    two trailing dots toward the animal below. A faint groove inside the rim is where the progress ring runs.
    The circle is centered on the frame (the ring fills around the image center); the frame's top is empty."""
    c = Canvas(BUBBLE_W, BUBBLE_H)
    bubble = sd_circle(c, BUBBLE_R)
    shape = np.minimum(bubble, bubble_dots(c))
    outline = shape - 4.5
    c.over((60, 60, 60), blur(shift(fill(outline), 0, 4), 3.0) * 0.22)
    c.over(TINT_OUTLINE, fill(outline))
    body = fill(shape)
    c.over(lerp((255, 255, 255), (228, 228, 228), np.clip((BUBBLE_R - c.y) / (2.0 * BUBBLE_R), 0, 1)), body)
    c.over((210, 210, 210), soft(-bubble - 7.0, 7.0) * np.clip(-c.y / BUBBLE_R, 0, 1) * fill(bubble) * 0.7)
    groove = np.abs(sd_circle(c, BUBBLE_R - 8.5)) - 3.4
    c.over((214, 214, 214), fill(groove) * 0.55)
    c.over((255, 255, 255), soft(sd_ellipse(c, 7.0, 13.0, ox=-30.0, oy=24.0, angle_deg=-34.0), 2.5) * fill(bubble) * 0.9)
    return "UI_Bubble_Wish", c, RUNTIME_KIT


def kit_bubble_ring():
    """UI_Bubble_Ring: a full white progress ring on UI_Bubble_Wish's frame, in the bubble's groove."""
    c = Canvas(BUBBLE_W, BUBBLE_H)
    ring = np.abs(sd_circle(c, BUBBLE_R - 8.5)) - 3.6
    c.over(lerp((255, 255, 255), (238, 238, 238), np.clip(-c.y / BUBBLE_R, 0, 1)), fill(ring))
    return "UI_Bubble_Ring", c, RUNTIME_KIT


# ---------------------------------------------------------------- calendar strip

def kit_calendar_box():
    """UI_Calendar_Box: one day box of the 28-day strip, white and greys for tinting. A soft rounded tile with a header
    band for the day number (runtime text) and two little binder holes; the reward icon sits in the body."""
    c = Canvas(112, 120, cy=58.0)
    tile = sd_round_rect(c, 50.0, 50.0, 18.0)
    c.over((40, 40, 40), blur(shift(fill(tile), 0, 4), 3.0) * 0.24)
    c.over(lerp((255, 255, 255), (236, 236, 236), np.clip((50.0 - c.y) / 100.0, 0, 1)), fill(tile))
    c.over((214, 214, 214), soft(-tile - 7.0, 7.0) * np.clip(-c.y / 50.0, 0, 1) * fill(tile) * 0.8)
    header = fill(tile) * np.clip(0.5 + (c.y - 27.0) * 4.0, 0, 1)
    c.over((228, 228, 228), header)
    c.over((206, 206, 206), fill(np.abs(c.y - 27.0) - 0.9) * fill(tile) * 0.9)
    for x in (-30.0, 30.0):
        c.over((184, 184, 184), fill(sd_circle(c, 3.4, x, 39.0)))
        c.over((236, 236, 236), fill(sd_circle(c, 1.4, x - 0.8, 39.6)))
    c.over((255, 255, 255), soft(-tile - 2.5, 2.0) * fill(tile) * np.clip(c.y / 50.0, 0, 1) * 0.8)
    return "UI_Calendar_Box", c, RUNTIME_KIT


def kit_paw_stamp():
    """UI_PawStamp: a rose ink stamp, slightly turned: a thick ring, a thin inner ring and a paw. The ink is a touch
    lighter on the left where the stamp pressed less, with a few dry round spots (placed by hand, no noise)."""
    c = Canvas(96, 96)
    v = View(c, 0.0, 0.0, -14.0)
    ring = np.abs(np.hypot(v.x, v.y) - 37.0) - 4.6
    inner = np.abs(np.hypot(v.x, v.y) - 29.5) - 1.3
    paw = sd_paw(v, 1.08, 0.0, -1.0)
    ink = np.minimum(np.minimum(ring, inner), paw)
    for angle, dist, r in ((128.0, 37.5, 1.9), (151.0, 36.0, 1.3), (205.0, 38.5, 2.3), (305.0, 37.0, 1.5), (37.0, 36.3, 1.1)):
        a = math.radians(angle)
        ink = np.maximum(ink, -(np.hypot(v.x - math.cos(a) * dist, v.y - math.sin(a) * dist) - r))
    ink = np.maximum(ink, -sd_circle(c, 1.6, -5.5, -12.0))
    # Ink is a little translucent (the reward under it still shows) and lighter where the stamp pressed less.
    pressure = 0.74 + 0.18 * np.clip((c.x + 34.0) / 50.0, 0, 1)
    c.over(lerp(hexrgb("E86A7E"), hexrgb("CF4F68"), np.clip((30.0 - c.y) / 60.0, 0, 1)), fill(ink) * pressure)
    return "UI_PawStamp", c, RUNTIME_KIT


# ---------------------------------------------------------------- weekly journal

def icon_weekly_journal():
    """ICO_WeeklyJournal: a sky blue notebook, cream pages on its right edge, a darker elastic band, a rose ribbon
    bookmark hanging below and a cream paw on the cover."""
    c = Canvas(128, 128, cy=62.0)
    cx, cy = -5.0, 4.0
    cover = sd_round_rect(c, 35.0, 43.0, 11.0, ox=cx, oy=cy)
    pages = sd_round_rect(c, 34.0, 40.0, 9.0, ox=cx + 7.0, oy=cy - 1.5)
    ribbon_pts = [(cx + 10.0, cy - 30.0), (cx + 22.0, cy - 30.0), (cx + 22.0, cy - 57.0), (cx + 16.0, cy - 51.0),
                  (cx + 10.0, cy - 57.0)]
    ribbon = sd_polygon(c, ribbon_pts) - 1.2
    shape = np.minimum(np.minimum(cover, pages), ribbon)
    sticker(c, shape, "3E5A86", 4.5)
    c.over(vgrad(c, "E57A9A", "C95476", cy - 30.0, cy - 57.0), fill(ribbon))
    c.over((255, 255, 255), fill(sd_polyline(c, [(cx + 13.0, cy - 33.0), (cx + 13.0, cy - 50.0)], 1.1)) * fill(ribbon) * 0.45)
    c.over(hexrgb("FFF6E2"), fill(pages))
    for k in range(3):
        line = np.abs(c.x - (cx + 36.0 + k * 3.0)) - 0.6
        c.over(hexrgb("E3D2B2"), fill(line) * fill(pages) * (c.y < cy + 33.0) * (c.y > cy - 37.0))
    c.over(hexrgb("3E5A86"), fill(cover - 4.0) * fill(pages + 0.5) * (c.x > cx + 28.0))
    c.over(vgrad(c, "A9D3F5", "5E95D2", cy + 43.0, cy - 43.0), fill(cover))
    c.over(hexrgb("4F82C0"), soft(-cover - 6.0, 6.0) * np.clip((cy - c.y) / 43.0, 0, 1) * fill(cover) * 0.6)
    band = sd_round_rect(c, 4.2, 44.0, 2.0, ox=cx + 22.0, oy=cy)
    c.over(vgrad(c, "4F78B5", "38598E", cy + 43.0, cy - 43.0), fill(band) * fill(cover))
    c.over(hexrgb("7FA4D6"), fill(np.abs(c.x - (cx + 20.0)) - 0.7) * fill(band) * fill(cover) * 0.8)
    c.over(hexrgb("FFF4D6"), fill(sd_paw(c, 1.0, cx - 5.0, cy - 2.0)))
    gloss(c, 4.5, 12.0, cx - 24.0, cy + 22.0, -8.0, fill(cover), 0.4, edge=2.5)
    return "ICO_WeeklyJournal", c, RUNTIME_ICONS


def kit_journal_chest():
    """UI_Journal_Chest: the weekly journal's completion chest. A plump wooden chest with a round dome lid, darker
    rounded straps with golden studs, and a round golden clasp holding a heart."""
    c = Canvas(192, 192, cy=100.0)
    w = 6.0
    body = sd_round_rect(c, 66.0, 34.0, 16.0, oy=-32.0)
    dome = np.maximum(sd_ellipse(c, 66.0, 46.0, oy=6.0), 6.0 - c.y)
    lip = sd_round_rect(c, 69.0, 8.5, 7.0, oy=4.0)
    lid = np.minimum(dome, lip)
    shape = np.minimum(body, lid)
    sticker(c, shape, WOOD_OUTLINE, w, dy=5.5, radius=4.5, strength=0.3)
    # Body: warm planks.
    c.over(vgrad(c, "E9BD84", "BF8650", 2.0, -66.0), fill(body))
    for y in (-21.0, -43.0):
        c.over(hexrgb("A8733F"), fill(np.abs(c.y - y) - 1.1) * fill(body) * 0.7)
    c.over(hexrgb("A8703C"), soft(-body - 8.0, 8.0) * np.clip((-40.0 - c.y) / 26.0, 0, 1) * fill(body) * 0.6)
    # Lid: the dome catches the light from the top left.
    c.over(lerp(hexrgb("F4CF98"), hexrgb("CC9257"), np.clip((52.0 - c.y + c.x * 0.25) / 60.0, 0, 1)), fill(dome))
    for k in (1, 2):
        arc = np.abs(sd_ellipse(c, 66.0 - k * 19.0, 46.0 - k * 13.0, oy=6.0)) - 1.0
        c.over(hexrgb("B07C46"), fill(arc) * fill(dome) * (c.y > 8.0) * 0.45)
    c.over(hexrgb(WOOD_OUTLINE), fill(lip - 3.0))
    c.over(vgrad(c, "D9A266", "A8703C", 12.5, -4.5), fill(lip))
    c.over(hexrgb("F2C88E"), fill(np.abs(c.y - 10.0) - 1.0) * fill(lip) * 0.8)
    # Straps with golden studs.
    for sx in (-42.0, 42.0):
        strap = np.minimum(sd_round_rect(c, 7.5, 40.0, 5.0, ox=sx, oy=-26.0), np.maximum(sd_round_rect(c, 7.5, 60.0, 5.0, ox=sx, oy=10.0), dome))
        strap = np.maximum(strap, shape)
        c.over(hexrgb(WOOD_OUTLINE), fill(strap - 2.5) * fill(shape))
        c.over(vgrad(c, "A8703C", "7A4E2A", 50.0, -66.0), fill(strap))
        c.over(hexrgb("C48B52"), fill(np.abs(c.x - (sx - 4.0)) - 0.9) * fill(strap) * 0.7)
        for sy in (-56.0, -12.0):
            stud = sd_circle(c, 3.6, sx, sy)
            c.over(hexrgb("A8662A"), fill(stud - 1.4))
            c.over(lerp(hexrgb("FFF0A8"), hexrgb("F2B544"), np.clip((sy + 3.0 - c.y) / 6.0, 0, 1)), fill(stud))
    # The round golden clasp with a heart.
    clasp = sd_circle(c, 17.0, 0.0, -2.0)
    c.over(hexrgb("A8662A"), fill(clasp - 4.0))
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F2A93B"), np.clip((15.0 - c.y) / 34.0, 0, 1)), fill(clasp))
    c.over(hexrgb("E39A2E"), soft(-clasp - 4.0, 4.0) * np.clip((-2.0 - c.y) / 17.0, 0, 1) * fill(clasp) * 0.7)
    heart = sd_heart(c, 16.5, 0.0, -3.0)
    c.over(hexrgb("8A4A26"), fill(heart))
    c.over((255, 255, 255), fill(sd_ellipse(c, 4.0, 2.4, ox=-7.0, oy=6.0, angle_deg=30.0)) * fill(clasp) * 0.8)
    gloss(c, 12.0, 6.0, -34.0, 36.0, 28.0, fill(dome), 0.45, edge=3.0)
    return "UI_Journal_Chest", c, RUNTIME_KIT


# ---------------------------------------------------------------- rewards and actions

def icon_ticket():
    """ICO_Ticket: an activity ticket, slightly turned: coral card with round notches, a dotted tear line before the
    stub, a cream five-point star on the main part and a round dot on the stub."""
    c = Canvas(128, 128, cy=64.0)
    angle = 14.0
    v = View(c, 0.0, 0.0, angle)
    card = sd_round_rect(v, 51.0, 30.0, 9.0)
    for nx in (-51.0, 51.0):
        card = np.maximum(card, -(np.hypot(v.x - nx, v.y) - 9.5))
    sticker(c, card, "B4495A", 4.5)
    inside = fill(card)
    c.over(lerp(hexrgb("FFB8B4"), hexrgb("EE7F8A"), np.clip((30.0 - v.y) / 60.0, 0, 1)), inside)
    c.over(hexrgb("DB6475"), soft(-card - 6.0, 6.0) * np.clip(-v.y / 30.0, 0, 1) * inside * 0.6)
    for k in range(6):
        dot = np.hypot(v.x - 22.0, v.y - (-21.0 + k * 8.4)) - 2.1
        c.over(hexrgb("C9586A"), fill(dot) * inside)
    star = sd_rounded_star(c, 19.5, 9.4, *rotate_points([(-13.0, -0.5)], angle)[0], rounding=2.5, angle_deg=angle)
    c.over(hexrgb("C9586A"), fill(star - 2.6))
    c.over(lerp(hexrgb("FFFBEF"), hexrgb("FFE6C0"), np.clip((18.0 - v.y) / 36.0, 0, 1)), fill(star))
    sx, sy = rotate_points([(37.0, 0.0)], angle)[0]
    c.over(hexrgb("C9586A"), fill(sd_circle(c, 7.0, sx, sy) - 2.0))
    c.over(hexrgb("FFF3E2"), fill(sd_circle(c, 7.0, sx, sy)))
    c.over((255, 255, 255), soft(-card - 2.5, 2.0) * inside * np.clip(v.y / 30.0, 0, 1) * 0.55)
    return "ICO_Ticket", c, RUNTIME_ICONS


def icon_seed():
    """ICO_Seed: a plump brown seed, a pale seam along it, opening a green stem and two round leaves."""
    c = Canvas(128, 128, cy=64.5)
    v = View(c, -4.0, -20.0, -16.0)
    seed = normalize(np.sqrt((v.x / (28.0 * (1.0 - 0.22 * v.y / 33.0))) ** 2 + (v.y / 33.0) ** 2) - 1.0)
    stem = sd_polyline(c, [(1.0, 8.0), (3.0, 20.0), (2.0, 30.0)], 3.6)
    left = sd_leaf(c, 38.0, 12.5, -14.0, 37.0, 158.0)
    right = sd_leaf(c, 40.0, 13.5, 19.0, 41.0, 24.0)
    sprout = np.minimum(stem, np.minimum(left, right))
    shape = np.minimum(seed, sprout)
    sticker(c, shape, WOOD_OUTLINE, 4.5)
    c.over(vgrad(c, "8FD06A", "4F8A3F", 56.0, 12.0), fill(sprout))
    for leaf, ox, oy, ang in ((left, -14.0, 37.0, 158.0), (right, 19.0, 41.0, 24.0)):
        a = math.radians(ang)
        rib = sd_polyline(c, [(ox - math.cos(a) * 11.0, oy - math.sin(a) * 11.0), (ox + math.cos(a) * 11.0, oy + math.sin(a) * 11.0)], 1.0)
        c.over(hexrgb("4F8A3F"), fill(rib) * fill(leaf) * 0.7)
    c.over((255, 255, 255), fill(sd_ellipse(c, 6.5, 2.4, ox=-15.0, oy=41.0, angle_deg=-22.0)) * fill(left) * 0.5)
    c.over(vgrad(c, "DDA46A", "93602F", 14.0, -52.0), fill(seed))
    c.over(hexrgb("82522A"), soft(-seed - 6.0, 6.0) * np.clip((-14.0 - c.y) / 34.0, 0, 1) * fill(seed) * 0.6)
    seam = np.abs(v.x + 0.18 * v.y - 0.0) - 1.3
    c.over(hexrgb("F2CB91"), fill(seam) * fill(seed + 7.0) * 0.85)
    gloss(c, 4.5, 10.0, -16.0, -10.0, -20.0, fill(seed), 0.55, edge=2.0)
    return "ICO_Seed", c, RUNTIME_ICONS


def icon_treat():
    """ICO_Treat: a golden heart biscuit with a rose icing heart on it (any treat: it gives bond)."""
    c = Canvas(128, 128, cy=62.0)
    biscuit = sd_heart(c, 98.0, 0.0, 2.0)
    sticker(c, biscuit, WARM_OUTLINE, 4.5)
    c.over(vgrad(c, "F6CF8C", "D99A52", 44.0, -44.0), fill(biscuit))
    c.over(hexrgb("C47F3C"), soft(-biscuit - 7.0, 7.0) * np.clip(-c.y / 40.0, 0, 1) * fill(biscuit) * 0.7)
    c.over(hexrgb("FBE2B0"), soft(-biscuit - 3.0, 2.0) * fill(biscuit) * np.clip(c.y / 40.0, 0, 1) * 0.7)
    icing = sd_heart(c, 60.0, 0.0, 3.0)
    c.over(hexrgb("D9638A"), fill(icing - 2.0) * 0.35)
    c.over(vgrad(c, "FFC2D3", "F48BAA", 30.0, -24.0), fill(icing))
    c.over(hexrgb("EC7398"), soft(-icing - 4.0, 4.0) * np.clip(-c.y / 24.0, 0, 1) * fill(icing) * 0.5)
    for ox, oy in ((-34.0, 22.0), (34.0, 22.0), (0.0, -36.0)):
        c.over(hexrgb("B87A3C"), fill(sd_circle(c, 2.4, ox, oy)) * fill(biscuit) * 0.8)
    gloss(c, 6.5, 3.8, -12.0, 15.0, 35.0, fill(icing), 0.85)
    return "ICO_Treat", c, RUNTIME_ICONS


def clover_head(c, r, ox, oy):
    """A sweet clover blossom: a round head of small florets pointing outward (distance field, florets list)."""
    florets = []
    for ring, count, dist, rx, ry, turn in ((0, 11, r * 0.78, r * 0.26, r * 0.42, 0.0), (1, 7, r * 0.36, r * 0.24, r * 0.36, 0.4)):
        for k in range(count):
            a = math.tau * k / count + turn
            florets.append((ring, ox + math.cos(a) * dist, oy + math.sin(a) * dist, rx, ry, math.degrees(a) + 90.0))
    head = sd_circle(c, r * 0.7, ox, oy)
    for _, x, y, rx, ry, ang in florets:
        head = np.minimum(head, sd_ellipse(c, rx, ry, ox=x, oy=y, angle_deg=ang))
    return head, florets


def icon_treat_clover_crunch():
    """ICO_Treat_CloverCrunch (Trèfle croquant, the Prairie treat): a round crunchy biscuit with docked holes, topped
    with a sweet clover sprig, a round pink blossom of small florets and two round leaves."""
    c = Canvas(128, 128, cy=64.0)
    biscuit = sd_circle(c, 45.0, 0.0, -2.0)
    sticker(c, biscuit, WARM_OUTLINE, 4.5)
    c.over(vgrad(c, "F6D08E", "D8994F", 43.0, -47.0), fill(biscuit))
    c.over(hexrgb("C47F3C"), soft(-biscuit - 7.0, 7.0) * np.clip((-2.0 - c.y) / 45.0, 0, 1) * fill(biscuit) * 0.7)
    rim = np.abs(sd_circle(c, 36.0, 0.0, -2.0)) - 1.1
    c.over(hexrgb("D59A55"), fill(rim) * 0.6)
    for k in range(10):
        a = math.tau * k / 10.0 + 0.3
        c.over(hexrgb("B87A3C"), fill(sd_circle(c, 2.2, math.cos(a) * 40.5, -2.0 + math.sin(a) * 40.5)) * 0.85)
    # The sprig: stem, two round leaves, one blossom.
    hx, hy, hr = 7.0, 10.0, 17.0
    stem = sd_polyline(c, [(-14.0, -26.0), (-6.0, -10.0), (2.0, 2.0)], 2.6)
    leaf_a = sd_ellipse(c, 11.5, 8.5, ox=-21.0, oy=-8.0, angle_deg=30.0)
    leaf_b = sd_ellipse(c, 11.5, 8.5, ox=4.0, oy=-21.0, angle_deg=-20.0)
    green = np.minimum(np.minimum(stem, leaf_a), leaf_b)
    head, florets = clover_head(c, hr, hx, hy)
    sprig = np.minimum(green, head)
    c.over((90, 50, 30), blur(shift(fill(sprig - 2.6), 0, 2.0), 1.5) * 0.3)
    c.over(hexrgb("4F7A2E"), fill(green - 2.6))
    c.over(hexrgb("B4495A"), fill(head - 2.6))
    c.over(vgrad(c, "A6DB7A", "5E9E4A", 2.0, -30.0), fill(green))
    for ox, oy, ang in ((-21.0, -8.0, 30.0), (4.0, -21.0, -20.0)):
        a = math.radians(ang)
        c.over(hexrgb("4F8A3F"), fill(sd_polyline(c, [(ox - math.cos(a) * 7.0, oy - math.sin(a) * 7.0), (ox + math.cos(a) * 7.0, oy + math.sin(a) * 7.0)], 0.9)) * 0.7)
    # Florets: deeper pink at the tips of the outer ring, paler toward the heart, lit from the top left.
    c.over(hexrgb("E46E8E"), fill(head))
    for ring, x, y, rx, ry, ang in florets:
        f = sd_ellipse(c, rx, ry, ox=x, oy=y, angle_deg=ang)
        light = np.clip((y - hy + hr) / (2.0 * hr) - (x - hx) / (4.0 * hr), 0, 1)
        top, low = ("FFC9D8", "F08BA8") if ring == 0 else ("FFE3EB", "F7AFC4")
        c.over(lerp(hexrgb(low), hexrgb(top), light), fill(f + 0.8))
    c.over((255, 255, 255), fill(sd_circle(c, 2.6, hx - 5.0, hy + 6.0)) * 0.8)
    gloss(c, 9.0, 4.0, -22.0, 26.0, 30.0, fill(biscuit) * (1.0 - fill(sprig - 2.6)), 0.4, edge=2.0)
    return "ICO_Treat_CloverCrunch", c, RUNTIME_ICONS


def arrow_head(c, r, angle_deg, length, half_width, ox=0.0, oy=0.0):
    """Rounded arrow head at a counterclockwise arc's end, pointing along the arc."""
    a = math.radians(angle_deg)
    ex, ey = ox + r * math.cos(a), oy + r * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)
    rx, ry = math.cos(a), math.sin(a)
    back = 3.0
    pts = [(ex - tx * back + rx * half_width, ey - ty * back + ry * half_width),
           (ex + tx * length, ey + ty * length),
           (ex - tx * back - rx * half_width, ey - ty * back - ry * half_width)]
    return sd_polygon(c, pts) - 2.5


def icon_swap():
    """ICO_Swap: two round sky blue arrows chasing each other around a circle (swap a wish)."""
    c = Canvas(128, 128, cy=64.0)
    r, hw = 33.0, 9.0
    shape = None
    for a0, a1 in ((28.0, 140.0), (208.0, 320.0)):
        d = np.minimum(sd_arc(c, r, a0, a1, hw), arrow_head(c, r, a1, 17.0, 18.0))
        shape = d if shape is None else np.minimum(shape, d)
    sticker(c, shape, "2F5E9E", 4.5)
    c.over(vgrad(c, "B4DEFA", "4F92D6", 46.0, -46.0), fill(shape))
    c.over(hexrgb("3F7FC4"), soft(-shape - 3.5, 3.5) * np.clip((0.0 - c.y) / 40.0, 0, 1) * fill(shape) * 0.5)
    c.over((255, 255, 255), soft(-shape - 2.5, 1.5) * fill(shape) * np.clip(c.y / 40.0, 0, 1) * 0.7)
    gloss(c, 6.0, 3.0, -10.0, 33.0, 8.0, fill(shape), 0.7)
    return "ICO_Swap", c, RUNTIME_ICONS


def icon_decoration():
    """ICO_Decoration: a terracotta flower pot with a little heart, a pink five-petal flower and two leaves opening
    beside it (a decoration item)."""
    c = Canvas(128, 128, cy=60.5)
    pot = sd_polygon(c, [(-26.0, -20.0), (26.0, -20.0), (19.0, -48.0), (-19.0, -48.0)]) - 3.5
    rim_y = -17.0
    rim = sd_round_rect(c, 34.0, 7.5, 5.5, oy=rim_y)
    stem = sd_polyline(c, [(0.0, -9.0), (0.0, 20.0)], 3.2)
    leaf_l = sd_leaf(c, 34.0, 10.0, -17.0, 0.0, 155.0)
    leaf_r = sd_leaf(c, 34.0, 10.0, 17.0, 0.0, 25.0)
    fy, pr, pd = 30.0, 10.0, 12.5
    petals = None
    for k in range(5):
        a = math.tau * k / 5.0 + math.pi / 2.0
        d = sd_circle(c, pr, math.cos(a) * pd, fy + math.sin(a) * pd)
        petals = d if petals is None else np.minimum(petals, d)
    leaves = np.minimum(stem, np.minimum(leaf_l, leaf_r))
    shape = np.minimum(np.minimum(pot, rim), np.minimum(leaves, petals))
    sticker(c, shape, "7A3F2A", 4.5)
    c.over(vgrad(c, "8FD06A", "4F8A3F", 12.0, -10.0), fill(leaves))
    for ox, oy, ang in ((-17.0, 0.0, 155.0), (17.0, 0.0, 25.0)):
        a = math.radians(ang)
        c.over(hexrgb("4F8A3F"), fill(sd_polyline(c, [(ox - math.cos(a) * 10.0, oy - math.sin(a) * 10.0), (ox + math.cos(a) * 10.0, oy + math.sin(a) * 10.0)], 0.9)) * 0.7)
    c.over((255, 255, 255), fill(sd_ellipse(c, 6.0, 2.2, ox=-21.0, oy=4.0, angle_deg=-25.0)) * fill(leaf_l) * 0.45)
    c.over(hexrgb("7A3F2A"), fill(petals - 4.5))
    c.over(vgrad(c, "FFC6D6", "F08AA8", fy + 23.0, fy - 23.0), fill(petals))
    c.over((255, 255, 255), soft(-petals - 2.5, 2.0) * fill(petals) * np.clip((c.y - fy) / 20.0, 0, 1) * 0.5)
    c.over(hexrgb("C9782A"), fill(sd_circle(c, 7.5, 0.0, fy) - 1.8))
    c.over(lerp(hexrgb("FFF0A0"), hexrgb("F7C04A"), np.clip((fy + 6.5 - c.y) / 13.0, 0, 1)), fill(sd_circle(c, 6.5, 0.0, fy)))
    c.over((255, 255, 255), fill(sd_circle(c, 2.0, -2.2, fy + 2.4)) * 0.8)
    c.over(vgrad(c, "EFA477", "C8653F", -20.0, -52.0), fill(pot))
    c.over(hexrgb("B4573A"), soft(-pot - 5.0, 5.0) * np.clip((-36.0 - c.y) / 16.0, 0, 1) * fill(pot) * 0.6)
    c.over(hexrgb("7A3F2A"), fill(rim - 3.0))
    c.over(vgrad(c, "F7B98E", "D9774C", rim_y + 7.5, rim_y - 7.5), fill(rim))
    c.over(hexrgb("FFD9B8"), fill(np.abs(c.y - (rim_y + 3.5)) - 1.0) * fill(rim) * 0.7)
    c.over(hexrgb("FFF1E0"), fill(sd_heart(c, 14.0, 0.0, -35.0)) * 0.9)
    gloss(c, 3.2, 7.0, -15.0, -32.0, -12.0, fill(pot), 0.4, edge=2.0)
    return "ICO_Decoration", c, RUNTIME_ICONS


T36_SPRITES = (icon_today, icon_gift, kit_gift_base, kit_gift_lid, kit_gift_band, icon_wish, icon_wish_done,
               kit_bubble_wish, kit_bubble_ring, icon_calendar_days, kit_calendar_box, kit_paw_stamp, icon_weekly_journal,
               kit_journal_chest, icon_ticket, icon_seed, icon_treat, icon_treat_clover_crunch, icon_swap,
               icon_decoration)


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_KIT, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T36_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
