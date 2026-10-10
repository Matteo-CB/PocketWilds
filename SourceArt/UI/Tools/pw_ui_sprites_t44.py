"""Pocket Wilds UI sprites added in T44A (Albums: the album icons and the 16 album covers; doc 01 Albums et autocollants).

Same vector-like method as pw_ui_sprites.py and pw_ui_sprites_t43.py (signed distances rendered at 4x, box-filtered down).
Same family as the HUD icons: rounded filled shapes, one outline in a deeper tone of each material, light from the top left
(a vertical gradient, one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round dots,
five-point stars, pearls, hearts and paws only (no four-point sparkle anywhere: the season leaves are plump vesicas set on
the diagonals, the snowflake has six arms with round tips); nothing religious; handmade only (every shape is drawn here).

Icons (Art/UI/Icons, own colours, never tinted, 128 x 128, readable at 48 px):
- ICO_Album: a small closed album (green cloth cover, cream page edges, a spine) with a round sticker on the cover.
- ICO_AlbumStar: a plump round five-point star with a soft album-page hint behind it (the sticker counter of the HUD).
- ICO_Star_Rating: three small round stars in a soft arc (rarity and rating).
- ICO_Event_Calendar: a calendar page with a coral header, binder rings, a grid of dots and a star on the day.
- ICO_Visitor: a little caramel suitcase with a heart tag on a string.
- ICO_World_Map: a folded map (meadow, paper, pond) with a dotted path ending in a round pin.
- ICO_Season: four tiny round leaves in the season colours (spring green, summer sun, autumn orange, winter ice).
- ICO_Sticker: a round sticker with a peeled corner, on its backing sheet.
Reused, not redrawn: ICO_Act_Expeditions (as ICO_Expedition) and ICO_Act_Garden (as ICO_Garden).

Covers (Art/UI/Albums/Covers, 256 x 320, UI_AlbumCover_<Name>): a soft cloth cover in the colour of its biome, a darker
spine with two cords, a stitched border of round dots, a cream medallion with a simple round emblem and a cream label
plate at the foot where the album's name is set. 13 biomes (Grassland, Forest, Lake, Mountain, Snow, Jungle, Desert,
Sakura, Mushrooms, CloudIslands, Lunar, Prehistoric, StarrySky) plus Events, Seasons and Activities.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t44.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (ROOT, Canvas, MASTERS, RUNTIME_ICONS, blur, fill, hexrgb, lerp, sd_circle, sd_ellipse,
                           sd_polygon, sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t34a import sd_cloud, sd_heart, sd_leaf, sd_paw
from pw_ui_sprites_t35a import SHADOW, drop_shadow
from pw_ui_sprites_t36 import WARM_OUTLINE, View, gloss, sd_arc, sd_rounded_star, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, sd_drop, top_rim, union
from pw_ui_sprites_t42 import daisy, rot
from pw_ui_sprites_t43 import WIDTH, shade_of, sd_water_drop

RUNTIME_COVERS = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Albums", "Covers")

GOLD = ("FFE98F", "F5AE3A", "C9782A", "E38E2C")   # top, bottom, outline, lower shade (the project's star)
CREAM = ("FFFDF4", "F2E4C6", "C9B58F")            # paper: top, bottom, outline


# ---------------------------------------------------------------- helpers

def family(c, shapes, dy=3.0, strength=0.24):
    """The family's outline: the union of the pieces grown by WIDTH in the warm brown, over a soft drop shadow."""
    out = union(*shapes) - WIDTH
    c.over(SHADOW, blur(shift(fill(out), 0, dy), 2.5) * strength)
    c.over(hexrgb(WARM_OUTLINE), fill(out))
    return out


def piece(c, d, top, bottom, line, y_top, y_bottom, lw=2.2, shade=None, rim=None, gl=None):
    """A filled piece with its own edge, a vertical gradient, an optional lower shade, top rim and gloss
    (rx, ry, x, y, angle)."""
    c.over(hexrgb(line), fill(d - lw))
    c.over(vgrad(c, top, bottom, y_top, y_bottom), fill(d))
    mid, span = (y_top + y_bottom) * 0.5, max(abs(y_top - y_bottom) * 0.5, 1.0)
    if shade:
        lower_shade(c, d, shade, mid, span, width=max(span * 0.18, 2.0), strength=0.5)
    if rim:
        top_rim(c, d, mid, span, 0.5, color=hexrgb(rim))
    if gl:
        gloss(c, gl[0], gl[1], gl[2], gl[3], gl[4], fill(d), 0.5, edge=1.2)


def gold_star(c, outer, inner, ox, oy, rounding=4.0, lw=2.4, angle=0.0):
    star = sd_rounded_star(c, outer, inner, ox, oy, rounding=rounding, angle_deg=angle)
    r = outer + rounding
    piece(c, star, GOLD[0], GOLD[1], GOLD[2], oy + r, oy - r, lw=lw, shade=GOLD[3], rim="FFF8D6",
          gl=(r * 0.2, r * 0.1, ox - r * 0.3, oy + r * 0.25, 35.0))
    return star


def dots(c, points, r, color, alpha=1.0, mask=None):
    for x, y in points:
        d = fill(sd_circle(c, r, x, y)) * alpha
        c.over(hexrgb(color), d if mask is None else d * mask)


def season_leaves(c, ox, oy, s, outline_w=2.4):
    """Four plump round leaves on the diagonals in the season colours (spring, summer, autumn, winter), a pearl between."""
    colours = (("D2F2A8", "8CCB62", "4F8A3F"),   # spring, top left
               ("FFE98A", "F2B93A", "B8801E"),   # summer, top right
               ("FFB67A", "E26E34", "A04A22"),   # autumn, bottom right
               ("E3F4FF", "9CCBEA", "4F87B5"))   # winter, bottom left
    angles = (135.0, 45.0, -45.0, -135.0)
    shapes = []
    for ang, col in zip(angles, colours):
        a = math.radians(ang)
        cx, cy = ox + math.cos(a) * 19.0 * s, oy + math.sin(a) * 19.0 * s
        shapes.append((sd_leaf(c, 48.0 * s, 17.0 * s, cx, cy, ang), col, cx, cy, ang))
    for d, col, cx, cy, ang in shapes:
        piece(c, d, col[0], col[1], col[2], cy + 20.0 * s, cy - 20.0 * s, lw=outline_w * s, shade=col[2])
        a = math.radians(ang)
        half = 15.0 * s
        rib = sd_polyline(c, [(cx - math.cos(a) * half, cy - math.sin(a) * half), (cx + math.cos(a) * half, cy + math.sin(a) * half)], max(0.9 * s, 0.4))
        c.over(hexrgb(col[2]), fill(rib) * fill(d + 2.0 * s) * 0.5)
    pearl = sd_circle(c, 6.0 * s, ox, oy)
    piece(c, pearl, "FFFFFF", "EADFCB", "B49A72", oy + 6.0 * s, oy - 6.0 * s, lw=1.6 * s)
    return union(*[d for d, *_ in shapes], pearl)


# ---------------------------------------------------------------- icons

def icon_album():
    """ICO_Album: a small closed album a little turned: cream page edges at its right and foot, a green cloth cover with a
    darker spine, a round sticker (coral ring, a gold star) on the cover."""
    c = Canvas(128, 128, cy=64.0)
    v = View(c, 0.0, -1.0, -6.0)
    pages = sd_round_rect(v, 38.0, 47.0, 7.0, ox=5.0, oy=-3.0)
    cover = sd_round_rect(v, 40.0, 50.0, 9.0, ox=-2.0)
    family(c, [pages, cover])
    piece(c, pages, CREAM[0], CREAM[1], CREAM[2], 40.0, -50.0, lw=1.8)
    for x in (39.0, 41.5):
        c.over(hexrgb("D9C29B"), fill(np.abs(v.x - x) - 0.5) * fill(pages) * (1.0 - fill(cover)) * 0.8)
    piece(c, cover, "A8DDA0", "6FB07C", "3F7A4C", 50.0, -50.0, lw=2.4, shade="4F8A5A", rim="E3F5CF")
    spine = np.maximum(cover, v.x + 26.0)
    c.over(lerp(hexrgb("6FB07C"), hexrgb("4F8A5A"), np.clip((50.0 - v.y) / 100.0, 0, 1)), fill(spine))
    c.over(hexrgb("3F7A4C"), fill(np.abs(v.x + 26.0) - 0.9) * fill(cover + 2.0) * 0.85)
    for y in (-30.0, 30.0):
        px, py = rot(-34.0, y, -6.0)
        dots(c, [(px, py - 1.0)], 1.9, "E3F5CF", 0.9)
    sx, sy = rot(10.0, 4.0, -6.0)
    sy -= 1.0
    disc = sd_circle(c, 22.0, sx, sy)
    c.over(SHADOW, blur(shift(fill(disc), 0, 2.0), 1.6) * 0.25)
    piece(c, disc, "FFFFFF", "F6EAD3", "D9C29B", sy + 22.0, sy - 22.0, lw=2.0)
    ring = sd_circle(c, 15.5, sx, sy)
    c.over(hexrgb("E86A86"), fill(ring))
    c.over(vgrad(c, "FFC0CF", "FF8DA8", sy + 15.0, sy - 15.0), fill(ring + 2.2))
    gold_star(c, 8.5, 4.6, sx, sy - 0.5, rounding=2.0, lw=1.6)
    gloss(c, 6.0, 2.6, sx - 9.0, sy + 13.0, 35.0, fill(disc), 0.5, edge=1.2)
    return "ICO_Album", c, RUNTIME_ICONS


def icon_album_star():
    """ICO_AlbumStar: a plump gold five-point star over a soft album-page hint (a cream page, a little turned, with its
    ruled lines and two round dots)."""
    c = Canvas(128, 128, cy=64.0)
    v = View(c, 8.0, 0.0, 9.0)
    page = sd_round_rect(v, 34.0, 48.0, 8.0)
    star = sd_rounded_star(c, 43.0, 24.0, -4.0, -3.0, rounding=9.0)
    family(c, [page, star])
    piece(c, page, CREAM[0], CREAM[1], CREAM[2], 48.0, -48.0, lw=2.0, shade="E6D1AA")
    for y in (-14.0, -26.0, -38.0):
        line = np.abs(v.y - y) - 0.9
        c.over(hexrgb("D9C29B"), fill(line) * fill(page + 6.0) * (v.x > 6.0) * 0.8)
    px, py = rot(20.0, 36.0, 9.0)
    dots(c, [(px + 8.0, py)], 3.2, "F2B8C6", 0.9, mask=fill(page))
    piece(c, star, GOLD[0], GOLD[1], GOLD[2], 52.0, -56.0, lw=2.8, shade=GOLD[3], rim="FFF8D6")
    c.over(hexrgb("FFF3B8"), soft(sd_circle(c, 12.0, -6.0, 2.0), 18.0) * fill(star) * 0.5)
    gloss(c, 9.0, 4.2, -17.0, 20.0, 36.0, fill(star), 0.7, edge=1.4)
    return "ICO_AlbumStar", c, RUNTIME_ICONS


def icon_star_rating():
    """ICO_Star_Rating: three small plump gold stars in a soft arc, the middle one higher and a little bigger."""
    c = Canvas(128, 128, cy=64.0)
    spec = ((-37.0, -10.0, 17.0, 9.5, -12.0), (0.0, 6.0, 20.0, 11.0, 0.0), (37.0, -10.0, 17.0, 9.5, 12.0))
    stars = [(sd_rounded_star(c, o, i, x, y, rounding=5.5, angle_deg=a), x, y, o + 5.5) for x, y, o, i, a in spec]
    family(c, [s for s, *_ in stars])
    for s, x, y, r in stars:
        piece(c, s, GOLD[0], GOLD[1], GOLD[2], y + r, y - r, lw=2.4, shade=GOLD[3], rim="FFF8D6",
              gl=(r * 0.22, r * 0.1, x - r * 0.3, y + r * 0.3, 35.0))
    return "ICO_Star_Rating", c, RUNTIME_ICONS


def icon_event_calendar():
    """ICO_Event_Calendar: a calendar page with a coral header and white round dots, two binder rings, a grid of cream dots
    and a gold star on the day of the event."""
    c = Canvas(128, 128, cy=64.0)
    body = sd_round_rect(c, 46.0, 46.0, 12.0, oy=-6.0)
    rings = [sd_round_rect(c, 4.5, 11.0, 4.5, ox=x, oy=40.0) for x in (-24.0, 24.0)]
    family(c, [body] + rings)
    piece(c, body, CREAM[0], CREAM[1], CREAM[2], 40.0, -52.0, lw=2.2, shade="E6D1AA")
    head = np.maximum(body, 18.0 - c.y)
    piece(c, head, "FFA394", "E5655E", "B4403E", 40.0, 18.0, lw=2.2, shade="C9483E", rim="FFD6CE")
    dots(c, [(-26.0, 29.0), (0.0, 29.0), (26.0, 29.0)], 3.0, "FFF6E2", 0.85, mask=fill(head))
    for r in rings:
        piece(c, r, "FFFFFF", "C9D3DB", "7A8791", 52.0, 30.0, lw=1.8)
    for x in (-26.0, 0.0, 26.0):
        dots(c, [(x, 2.0)], 6.2, "E3D2AE")
        dots(c, [(x, 2.8)], 4.6, "F5EAD0")
    dots(c, [(-26.0, -24.0), (26.0, -24.0)], 6.2, "E3D2AE")
    dots(c, [(-26.0, -23.2), (26.0, -23.2)], 4.6, "F5EAD0")
    gold_star(c, 14.0, 7.8, 0.0, -23.0, rounding=3.5, lw=2.0)
    return "ICO_Event_Calendar", c, RUNTIME_ICONS


def icon_visitor():
    """ICO_Visitor: a little caramel suitcase with two straps, a gold clasp and a round handle, a pink heart tag hanging
    from the handle on a string."""
    c = Canvas(128, 128, cy=64.0)
    body = sd_round_rect(c, 40.0, 29.0, 11.0, oy=-12.0)
    handle = sd_arc(c, 15.0, 0.0, 180.0, 3.4, 0.0, 17.0)
    heart = sd_heart(c, 30.0, -28.0, -26.0)
    string = sd_polyline(c, [(-14.0, 31.0), (-24.0, 12.0), (-28.0, -6.0)], 1.3)
    family(c, [body, handle, heart, string])
    piece(c, handle, "E9B97A", "A8622E", "6E3A18", 36.0, 17.0, lw=1.6)
    piece(c, body, "F6C688", "C9783A", "8A4E22", 17.0, -41.0, lw=2.4, shade="A8622E", rim="FFE3B8")
    for x in (-21.0, 21.0):
        strap = sd_round_rect(c, 4.5, 29.0, 1.5, ox=x, oy=-12.0)
        c.over(hexrgb("9A5A2A"), fill(np.maximum(strap, body)) * 0.9)
        c.over(hexrgb("D9945A"), fill(np.maximum(strap + 1.6, body)) * 0.55)
    for y in (12.0, -36.0):
        dots(c, [(x, y) for x in np.linspace(-30.0, 30.0, 8)], 1.1, "FFE3B8", 0.7, mask=fill(body + 2.0))
    clasp = sd_round_rect(c, 8.0, 5.5, 3.0, oy=11.0)
    piece(c, clasp, GOLD[0], GOLD[1], "9A6A1E", 16.0, 6.0, lw=1.5)
    dots(c, [(0.0, 11.0)], 1.6, "9A6A1E")
    c.over(hexrgb("8A5A2E"), fill(string) * 0.95)
    piece(c, heart, "FFC0CF", "F2708F", "B4405E", -10.0, -42.0, lw=2.2, shade="D9507A", rim="FFE3EA",
          gl=(4.0, 2.2, -33.0, -20.0, 35.0))
    hh = sd_heart(c, 9.0, -28.0, -27.0)
    c.over(hexrgb("FFF6F8"), fill(hh) * 0.95)
    gloss(c, 9.0, 3.4, -20.0, 10.0, 12.0, fill(body), 0.4, edge=1.5)
    return "ICO_Visitor", c, RUNTIME_ICONS


def icon_world_map():
    """ICO_World_Map: a folded map of three panels (meadow, paper with a round hill, a pond), its creases and zigzag edge,
    a dotted coral path and a round pin at its end."""
    c = Canvas(128, 128, cy=64.0)
    top = ((-49.0, 38.0), (-16.0, 32.0), (17.0, 38.0), (49.0, 32.0))
    bot = ((49.0, -38.0), (17.0, -32.0), (-16.0, -38.0), (-49.0, -32.0))
    shape = sd_polygon(c, [top[0], top[1], top[2], top[3], bot[0], bot[1], bot[2], bot[3]]) - 4.0
    family(c, [shape])
    c.over(hexrgb("8A6A3A"), fill(shape))
    panels = (("D2EFB4", "9BCB86", -60.0, -16.0), ("FFF6DE", "EDDDB8", -16.0, 17.0), ("CBE8F6", "9CCBE6", 17.0, 60.0))
    for ca, cb, x0, x1 in panels:
        inside = fill(shape + 2.2) * (c.x >= x0) * (c.x < x1)
        c.over(vgrad(c, ca, cb, 40.0, -40.0), inside)
    hill = sd_ellipse(c, 15.0, 11.0, ox=-31.0, oy=-6.0)
    c.over(hexrgb("6FAE5E"), fill(hill) * fill(shape + 2.2) * (c.x < -16.0))
    c.over(hexrgb("A8D88A"), fill(hill + 2.0) * fill(shape + 2.2) * (c.x < -16.0))
    pond = sd_ellipse(c, 13.0, 8.0, ox=35.0, oy=-18.0)
    c.over(hexrgb("5E9FCB"), fill(pond) * fill(shape + 2.2))
    c.over(hexrgb("A9D6F0"), fill(pond + 1.8) * fill(shape + 2.2))
    for x in (-16.0, 17.0):
        c.over(hexrgb("B49A6A"), fill(np.abs(c.x - x) - 0.8) * fill(shape + 2.2) * 0.8)
        c.over(hexrgb("FFFFFF"), fill(np.abs(c.x - x - 1.8) - 1.0) * fill(shape + 2.2) * 0.35)
    top_rim(c, shape + 2.0, 20.0, 20.0, 0.45)
    path = [(-40.0, -22.0), (-30.0, -14.0), (-20.0, -16.0), (-10.0, -6.0), (0.0, 0.0), (10.0, 6.0), (19.0, 12.0), (28.0, 18.0)]
    dots(c, path, 2.5, "9C3A32", 0.9)
    dots(c, [(x, y + 0.5) for x, y in path], 1.8, "F27A6E")
    pin = sd_circle(c, 8.5, 36.0, 22.0)
    c.over(SHADOW, blur(shift(fill(pin), 0, 2.0), 1.6) * 0.3)
    piece(c, pin, "FF8C82", "D9403F", "9C2F35", 31.0, 14.0, lw=2.0, shade="B8333A")
    dots(c, [(36.0, 22.5)], 3.0, "FFF6EE")
    gloss(c, 3.0, 1.6, 33.0, 26.0, 35.0, fill(pin), 0.6, edge=0.8)
    return "ICO_World_Map", c, RUNTIME_ICONS


def icon_season():
    """ICO_Season: four tiny round leaves in the season colours around a pearl."""
    c = Canvas(128, 128, cy=64.0)
    cluster = []
    # The silhouette first (outline + shadow), then the leaves with their own edges.
    angles = (135.0, 45.0, -45.0, -135.0)
    for ang in angles:
        a = math.radians(ang)
        cluster.append(sd_leaf(c, 48.0, 17.0, math.cos(a) * 19.0, math.sin(a) * 19.0, ang))
    cluster.append(sd_circle(c, 6.0))
    family(c, cluster)
    season_leaves(c, 0.0, 0.0, 1.0)
    return "ICO_Season", c, RUNTIME_ICONS


def icon_sticker():
    """ICO_Sticker: a round white sticker with a coral ring and a pink paw, its lower right corner peeled away and folded
    over (the back of the sticker, paler), lying on its backing sheet."""
    c = Canvas(128, 128, cy=64.0)
    sheet = sd_round_rect(c, 52.0, 52.0, 13.0, ox=1.0, oy=-1.0)
    sx0, sy0, r = -3.0, 3.0, 41.0
    n = (math.sqrt(0.5), -math.sqrt(0.5))
    d0 = 22.0
    qn = (c.x - sx0) * n[0] + (c.y - sy0) * n[1]
    disc = sd_circle(c, r, sx0, sy0)
    face = np.maximum(disc, qn - d0)                       # the sticker that is still stuck
    rx, ry = c.x - 2.0 * (qn - d0) * n[0], c.y - 2.0 * (qn - d0) * n[1]
    flap = np.maximum(np.hypot(rx - sx0, ry - sy0) - r, qn - d0)
    family(c, [sheet, face, flap])
    piece(c, sheet, "F1F6F8", "CBDAE2", "8FA6B3", 52.0, -52.0, lw=2.2, shade="B4C8D2", rim="FFFFFF")
    piece(c, face, "FFFFFF", "F3E8D2", "D9C29B", 44.0, -38.0, lw=2.2)
    ring = sd_circle(c, 30.0, sx0, sy0)
    c.over(hexrgb("E86A86"), fill(np.abs(ring) - 1.6) * fill(face + 1.0))
    c.over(hexrgb("FFC7D4"), fill(np.abs(ring + 3.0) - 0.8) * fill(face + 1.0) * 0.8)
    paw = sd_paw(c, 1.0, sx0 - 2.0, sy0 + 2.0)
    c.over(vgrad(c, "FF9DB4", "F2708F", sy0 + 20.0, sy0 - 14.0), fill(paw) * fill(face + 4.0))
    gloss(c, 11.0, 4.0, sx0 - 14.0, sy0 + 26.0, 30.0, fill(face), 0.5, edge=2.0)
    # the folded flap: the back of the sticker, with a soft shadow along the crease
    c.over(SHADOW, blur(shift(fill(flap), 1.5, 2.0), 1.6) * 0.3 * fill(face + 6.0))
    c.over(hexrgb("B9A783"), fill(flap))
    c.over(lerp(hexrgb("FFFDF6"), hexrgb("E9DEC6"), np.clip((qn - d0 + 26.0) / 26.0, 0, 1)), fill(flap + 2.0))
    c.over(hexrgb("FFFFFF"), fill(np.abs(qn - d0 + 1.4) - 1.0) * fill(flap) * 0.8)
    return "ICO_Sticker", c, RUNTIME_ICONS


# ---------------------------------------------------------------- emblems (drawn in a medallion of radius ~66; motif radius ~48)

def emblem(c, d, top, bottom, line, y_top, y_bottom, lw=2.6, shade=None):
    c.over(SHADOW, blur(shift(fill(d - lw), 0, 2.5), 2.0) * 0.22)
    piece(c, d, top, bottom, line, y_top, y_bottom, lw=lw, shade=shade)


def e_grassland(c, ox, oy):
    daisy(c, ox, oy, 1.75)


def e_forest(c, ox, oy):
    """An oak leaf: a plump leaf with three round lobes each side and a short stem."""
    d = sd_ellipse(c, 12.0, 40.0, ox=ox, oy=oy)
    for y, rad, w in ((-26.0, 9.0, 13.0), (-9.0, 11.0, 19.0), (8.0, 12.0, 21.0), (24.0, 10.0, 15.0)):
        for side in (-1, 1):
            d = smooth_min(d, sd_circle(c, rad, ox + side * (w - rad * 0.4), oy + y), 7.0)
    d = smooth_min(d, sd_circle(c, 9.0, ox, oy + 36.0), 6.0)
    stem = sd_polyline(c, [(ox, oy - 38.0), (ox + 3.0, oy - 52.0)], 2.8)
    emblem(c, stem, "A6DB7A", "5E9E4A", "3F7A2E", oy - 38.0, oy - 52.0, lw=2.0)
    emblem(c, d, "9FE08A", "3F9A52", "2F6E3A", oy + 46.0, oy - 40.0, shade="2F7A44")
    for y, spread in ((20.0, 15.0), (3.0, 17.0), (-14.0, 13.0)):
        for side in (-1, 1):
            vein = sd_polyline(c, [(ox, oy + y - 8.0), (ox + side * spread, oy + y + 3.0)], 0.9)
            c.over(hexrgb("2F6E3A"), fill(vein) * fill(d + 2.5) * 0.55)
    c.over(hexrgb("2F6E3A"), fill(sd_polyline(c, [(ox, oy - 34.0), (ox, oy + 34.0)], 1.1)) * fill(d + 2.5) * 0.7)


def e_lake(c, ox, oy):
    """A water drop with its round tip, a gloss and a round highlight."""
    drop = sd_water_drop(c, 1.35, ox, oy + 2.0)
    emblem(c, drop, "E3F3FF", "4F9ADB", "2F5E9E", oy + 42.0, oy - 44.0, shade="2F6FB8")
    gloss(c, 7.0, 14.0, ox - 15.0, oy + 2.0, -20.0, fill(drop), 0.6, edge=2.0)
    dots(c, [(ox - 16.0, oy + 20.0)], 4.0, "FFFFFF", 0.8)


def e_mountain(c, ox, oy):
    """A round snowy peak: a soft rounded triangle, a smaller peak behind, a snow cap with a scalloped edge."""
    back = sd_polygon(c, [(ox + 10.0, oy - 30.0), (ox + 30.0, oy + 6.0), (ox + 50.0, oy - 30.0)]) - 8.0
    emblem(c, back, "C9D6EA", "8FA3C4", "5A6E92", oy + 14.0, oy - 38.0, lw=2.2)
    peak = sd_polygon(c, [(ox - 40.0, oy - 32.0), (ox - 4.0, oy + 30.0), (ox + 30.0, oy - 32.0)]) - 9.0
    emblem(c, peak, "B4C4DE", "6F82A8", "465A80", oy + 40.0, oy - 40.0, shade="5A6E92")
    yb = oy + 8.0 - 7.0 * np.abs(np.sin((c.x - ox) * 0.105))
    cap = np.maximum(peak + 2.4, yb - c.y)
    c.over(hexrgb("9FB2D2"), fill(cap + 1.4))
    c.over(vgrad(c, "FFFFFF", "DDEBFA", oy + 40.0, oy + 4.0), fill(cap))


def e_snow(c, ox, oy):
    """A round snowflake: six arms with a pair of twigs and a round tip each, a round heart."""
    parts = [sd_circle(c, 9.0, ox, oy)]
    for k in range(6):
        a = math.radians(90.0 + 60.0 * k)
        ex, ey = ox + math.cos(a) * 40.0, oy + math.sin(a) * 40.0
        parts.append(sd_polyline(c, [(ox, oy), (ex, ey)], 3.6))
        parts.append(sd_circle(c, 6.0, ex, ey))
        mx, my = ox + math.cos(a) * 24.0, oy + math.sin(a) * 24.0
        for side in (-1, 1):
            b = a + side * math.radians(48.0)
            parts.append(sd_polyline(c, [(mx, my), (mx + math.cos(b) * 12.0, my + math.sin(b) * 12.0)], 2.8))
    flake = union(*parts)
    emblem(c, flake, "FFFFFF", "BFE0F5", "5A93BE", oy + 44.0, oy - 44.0, lw=2.4, shade="8EC4E6")
    dots(c, [(ox, oy)], 4.0, "8EC4E6", 0.8)


def e_jungle(c, ox, oy):
    """A big glossy leaf leaning right, with round holes along its sides, a midrib and a short stem."""
    ang = 62.0
    leaf = sd_leaf(c, 104.0, 36.0, ox, oy, ang)
    holes = []
    for t in (-18.0, 4.0, 26.0):
        for side in (-1, 1):
            hx, hy = rot(t, side * 15.0, ang)
            holes.append(sd_circle(c, 5.5, ox + hx, oy + hy))
    d = np.maximum(leaf, -union(*holes))
    sx, sy = rot(-52.0, 0.0, ang)
    ex, ey = rot(-62.0, 0.0, ang)
    stem = sd_polyline(c, [(ox + sx, oy + sy), (ox + ex, oy + ey)], 3.0)
    emblem(c, stem, "6FC87A", "2F8A4C", "17603F", oy + sy, oy + ey, lw=2.0)
    emblem(c, d, "7FE09A", "1F8A5C", "17603F", oy + 48.0, oy - 48.0, shade="17704A")
    a0x, a0y = rot(-46.0, 0.0, ang)
    a1x, a1y = rot(46.0, 0.0, ang)
    c.over(hexrgb("C9F0C9"), fill(sd_polyline(c, [(ox + a0x, oy + a0y), (ox + a1x, oy + a1y)], 1.5)) * fill(d + 2.5) * 0.8)
    gx, gy = rot(18.0, 12.0, ang)
    gloss(c, 12.0, 3.4, ox + gx, oy + gy, ang, fill(d), 0.45, edge=2.0)


def e_desert(c, ox, oy):
    """A sandstone arch: a thick round arch with soft strata, on a patch of pale sand."""
    sand = sd_ellipse(c, 46.0, 8.0, ox=ox, oy=oy - 38.0)
    emblem(c, sand, "FFF0C4", "F2D58E", "C9A24E", oy - 30.0, oy - 46.0, lw=2.0)
    outer = union(sd_circle(c, 38.0, ox, oy + 6.0), sd_round_rect(c, 38.0, 22.0, 6.0, ox=ox, oy=oy - 16.0))
    inner = union(sd_circle(c, 19.0, ox, oy + 4.0), sd_round_rect(c, 19.0, 24.0, 0.0, ox=ox, oy=oy - 22.0))
    d = np.maximum(outer, -inner)
    emblem(c, d, "FFC27A", "D9803A", "8A4A22", oy + 44.0, oy - 38.0, shade="B8602A")
    for y, k in ((-26.0, 0.0), (-8.0, 1.0), (10.0, 2.0), (26.0, 3.0)):
        wave = sd_polyline(c, [(ox + x, oy + y + 1.6 * math.sin(x * 0.18 + k)) for x in np.linspace(-40.0, 40.0, 17)], 0.9)
        c.over(hexrgb("A8602A"), fill(wave) * fill(d + 2.6) * 0.45)


def e_sakura(c, ox, oy):
    """A cherry petal with its small notch, and two tiny petals drifting near it."""
    def petal(px, py, s, ang):
        v = View(c, px, py, ang)
        d = sd_drop(v, 27.0 * s, 38.0 * s, -0.32)
        nx, ny = rot(0.0, 38.0 * s, ang)
        d = np.maximum(d, -sd_circle(c, 6.5 * s, px + nx, py + ny + 1.0 * s))
        return d
    for px, py, s, ang in ((ox - 36.0, oy - 24.0, 0.46, -55.0), (ox + 36.0, oy - 30.0, 0.4, 62.0)):
        emblem(c, petal(px, py, s, ang), "FFE3EB", "F5A9C2", "C85A82", py + 20.0, py - 20.0, lw=1.8)
    d = petal(ox, oy + 2.0, 1.0, 18.0)
    emblem(c, d, "FFE6EE", "F28FB0", "C85A82", oy + 44.0, oy - 40.0, shade="E0709A")
    for k in (-1, 0, 1):
        vx, vy = rot(k * 7.0, 28.0, 18.0)
        bx, by = rot(0.0, -30.0, 18.0)
        c.over(hexrgb("E0709A"), fill(sd_polyline(c, [(ox + bx, oy + 2.0 + by), (ox + vx, oy + 2.0 + vy)], 0.8)) * fill(d + 2.5) * 0.5)
    gloss(c, 6.0, 12.0, ox - 11.0, oy + 12.0, 18.0, fill(d), 0.5, edge=2.0)


def e_mushrooms(c, ox, oy):
    """A red mushroom cap with round cream dots on a plump cream stem."""
    stem = sd_round_rect(c, 15.0, 22.0, 9.0, ox=ox, oy=oy - 20.0)
    emblem(c, stem, "FFF9EC", "E8D6B2", "A8845A", oy + 2.0, oy - 42.0, shade="D4BC90")
    cap = np.maximum(sd_ellipse(c, 46.0, 36.0, ox=ox, oy=oy + 2.0), (oy + 2.0) - c.y - 4.0)
    cap = smooth_min(cap, sd_round_rect(c, 46.0, 5.0, 4.0, ox=ox, oy=oy - 1.0), 3.0)
    emblem(c, cap, "FF9A86", "D9403F", "9C2F35", oy + 40.0, oy - 4.0, shade="B8333A")
    for dx, dy, r in ((-22.0, 14.0, 7.5), (2.0, 24.0, 6.5), (23.0, 12.0, 7.0), (-2.0, 6.0, 4.0), (-34.0, 0.0, 3.4), (34.0, -0.5, 3.6)):
        dots(c, [(ox + dx, oy + dy)], r, "FFF6EE", 1.0, mask=fill(cap + 1.5))
    gloss(c, 12.0, 4.5, ox - 20.0, oy + 28.0, 25.0, fill(cap), 0.5, edge=2.0)


def e_clouds(c, ox, oy):
    """A small round cloud with a lighter puff and a soft blue shade."""
    cloud = sd_cloud(c, 2.6, ox, oy + 8.0)
    emblem(c, cloud, "FFFFFF", "BCD4F3", "4F7FC2", oy + 36.0, oy - 24.0, shade="8FB4E6")
    gloss(c, 11.0, 4.0, ox - 18.0, oy + 22.0, 25.0, fill(cloud), 0.7, edge=2.0)
    for x, y, r in ((-34.0, -30.0, 3.4), (30.0, -34.0, 4.4), (-8.0, -38.0, 2.8)):
        dots(c, [(ox + x, oy + y)], r, "BFD6F5")


def e_lunar(c, ox, oy):
    """A full moon: a pale disc with a few round craters and a soft gloss."""
    moon = sd_circle(c, 42.0, ox, oy)
    emblem(c, moon, "FFF6D8", "E3D29A", "A8965A", oy + 42.0, oy - 42.0, shade="CDBB7E")
    for dx, dy, r in ((-14.0, 14.0, 9.0), (16.0, 6.0, 6.5), (4.0, -16.0, 8.0), (-22.0, -10.0, 4.5), (24.0, -22.0, 4.0)):
        crater = sd_circle(c, r, ox + dx, oy + dy)
        c.over(hexrgb("D6C58A"), fill(crater) * fill(moon + 3.0))
        c.over(hexrgb("F8EEC4"), fill(sd_circle(c, r - 1.8, ox + dx + 0.8, oy + dy - 0.8)) * fill(moon + 3.0) * 0.7)
    gloss(c, 11.0, 4.0, ox - 20.0, oy + 28.0, 35.0, fill(moon), 0.5, edge=2.0)


def e_prehistoric(c, ox, oy):
    """A three-toed footprint pressed in clay: a plump rounded pad and three long round toes fanned above it."""
    pad = smooth_min(sd_ellipse(c, 22.0, 17.0, ox=ox, oy=oy - 16.0), sd_ellipse(c, 14.0, 22.0, ox=ox, oy=oy - 6.0), 8.0)
    toes = [sd_ellipse(c, 8.5, 15.0, ox=ox + x, oy=oy + y, angle_deg=a) for x, y, a in ((-24.0, 18.0, 28.0), (0.0, 28.0, 0.0), (24.0, 18.0, -28.0))]
    d = union(pad, *toes)
    emblem(c, d, "C99A68", "8A5A32", "5E3C20", oy + 44.0, oy - 34.0, shade="6E4524")
    for x, y in ((-22.0, 30.0), (0.0, 40.0), (22.0, 30.0)):
        dots(c, [(ox + x, oy + y)], 2.0, "E0BC8C", 0.7)
    gloss(c, 8.0, 3.0, ox - 8.0, oy - 4.0, 20.0, fill(pad), 0.3, edge=2.0)


def e_starry(c, ox, oy):
    """A round gold five-point star."""
    star = sd_rounded_star(c, 46.0, 26.0, ox, oy - 3.0, rounding=9.0)
    emblem(c, star, GOLD[0], GOLD[1], GOLD[2], oy + 56.0, oy - 56.0, lw=2.8, shade=GOLD[3])
    gloss(c, 9.0, 4.2, ox - 15.0, oy + 22.0, 36.0, fill(star), 0.7, edge=1.4)


def e_events(c, ox, oy):
    """A ribbon rosette: two ribbon tails, a scalloped round rosette, a cream ring and a gold star at its heart."""
    for side in (-1, 1):
        tail = sd_polygon(c, [(ox + side * 8.0, oy - 12.0), (ox + side * 30.0, oy - 12.0), (ox + side * 36.0, oy - 52.0),
                              (ox + side * 24.0, oy - 44.0), (ox + side * 10.0, oy - 54.0)]) - 3.0
        emblem(c, tail, "FF9A8C", "C9483E", "8A2E2E", oy - 10.0, oy - 56.0, lw=2.2, shade="A8383A")
    scallops = [sd_circle(c, 9.5, ox + math.cos(math.tau * k / 14.0) * 33.0, oy + 6.0 + math.sin(math.tau * k / 14.0) * 33.0) for k in range(14)]
    rose = union(sd_circle(c, 33.0, ox, oy + 6.0), *scallops)
    emblem(c, rose, "FFB6A6", "E5655E", "9C3A38", oy + 48.0, oy - 36.0, shade="C9483E")
    ring = sd_circle(c, 26.0, ox, oy + 6.0)
    c.over(hexrgb("FFF6E2"), fill(np.abs(ring) - 1.4) * 0.9)
    gold_star(c, 15.0, 8.4, ox, oy + 5.0, rounding=3.5, lw=2.0)


def e_seasons(c, ox, oy):
    season_leaves(c, ox, oy, 0.95)


def e_activities(c, ox, oy):
    """A round teal ball with a cream seam and a paw on it."""
    ball = sd_circle(c, 42.0, ox, oy)
    emblem(c, ball, "B4EFE6", "3FA89C", "256F68", oy + 42.0, oy - 42.0, shade="2F8A80")
    seam = sd_arc(c, 62.0, 60.0, 120.0, 2.0, ox, oy - 54.0)
    c.over(hexrgb("FFF6E2"), fill(seam) * fill(ball + 2.0) * 0.9)
    paw = sd_paw(c, 1.25, ox + 1.0, oy - 4.0)
    c.over(hexrgb("256F68"), fill(paw + 1.2) * 0.35)
    c.over(vgrad(c, "FFFFFF", "F6E8CC", oy + 20.0, oy - 20.0), fill(paw))
    gloss(c, 10.0, 4.0, ox - 20.0, oy + 26.0, 30.0, fill(ball), 0.5, edge=2.0)


# ---------------------------------------------------------------- covers

COVER_W, COVER_H = 256, 320
COVERS = (
    ("Grassland", ("A8E088", "6DB55A"), e_grassland),
    ("Forest", ("6FAE7A", "3F7A52"), e_forest),
    ("Lake", ("8FD0F0", "4E9BD0"), e_lake),
    ("Mountain", ("AAB9D4", "7384A8"), e_mountain),
    ("Snow", ("DCEFF8", "A9D0E6"), e_snow),
    ("Jungle", ("4FC088", "1F8A5C"), e_jungle),
    ("Desert", ("F7D68C", "DDA24E"), e_desert),
    ("Sakura", ("FACCDA", "EC9DB6"), e_sakura),
    ("Mushrooms", ("E4A08C", "B8604F"), e_mushrooms),
    ("CloudIslands", ("C9DEF8", "93B4E6"), e_clouds),
    ("Lunar", ("C2BEEC", "8C86C9"), e_lunar),
    ("Prehistoric", ("D9B07E", "A97B48"), e_prehistoric),
    ("StarrySky", ("6A6AC4", "37377E"), e_starry),
    ("Events", ("F69A92", "D9605E"), e_events),
    ("Seasons", ("9ADBC8", "5DB09B"), e_seasons),
    ("Activities", ("FBC47C", "E58E4A"), e_activities),
)


def perimeter(hw, hh, r, spacing):
    """Points along a rounded rectangle, `spacing` apart (the stitches)."""
    pts = []
    for x in np.arange(-hw + r, hw - r + 0.01, spacing):
        pts += [(x, hh), (x, -hh)]
    for y in np.arange(-hh + r, hh - r + 0.01, spacing):
        pts += [(hw, y), (-hw, y)]
    steps = max(int(r * math.pi / 2.0 / spacing), 2)
    for i in range(1, steps):
        a = (math.pi / 2.0) * i / steps
        for sx in (-1, 1):
            for sy in (-1, 1):
                pts.append((sx * (hw - r + math.sin(a) * r), sy * (hh - r + math.cos(a) * r)))
    return pts


def cover(name, colours, emblem_fn):
    """UI_AlbumCover_<Name>: the cloth cover of one album."""
    top, bottom = colours
    deep = shade_of(bottom, 0.42)
    c = Canvas(COVER_W, COVER_H, cy=160.0)
    body = sd_round_rect(c, 120.0, 152.0, 24.0)
    drop_shadow(c, body - 3.0, dy=5.0, radius=4.0, strength=0.3)
    c.over(hexrgb(deep), fill(body))
    inner = body + 3.5
    c.over(vgrad(c, top, bottom, 152.0, -152.0), fill(inner))
    weave = 0.035 * (np.sin(c.x * 1.9) * np.sin(c.y * 1.9)) + 0.02 * np.sin((c.x + c.y) * 0.9)
    c.over((255, 255, 255), np.clip(weave, 0, 1) * fill(inner))
    c.over(hexrgb(deep), np.clip(-weave, 0, 1) * fill(inner) * 0.6)
    spine = np.maximum(inner, c.x + 86.0)
    c.over(lerp(hexrgb(shade_of(top, 0.1)), hexrgb(shade_of(bottom, 0.22)), np.clip((152.0 - c.y) / 304.0, 0, 1)), fill(spine))
    c.over(hexrgb(deep), fill(np.abs(c.x + 86.0) - 1.4) * fill(inner + 2.0) * 0.85)
    c.over(hexrgb(shade_of(top, -0.35)), fill(np.abs(c.x + 83.0) - 0.8) * fill(inner + 2.0) * 0.5)
    for y in (-90.0, 90.0):   # the two cords of the spine
        cord = sd_round_rect(c, 31.0, 5.0, 2.5, ox=-103.0, oy=y)
        c.over(hexrgb(shade_of(bottom, 0.3)), fill(cord) * fill(inner))
        c.over(hexrgb(shade_of(top, -0.25)), fill(cord + 1.6) * fill(inner) * 0.8)
    stitch = [(x - 7.0, y) for x, y in perimeter(104.0, 136.0, 14.0, 14.0)]
    stitch = [(x, y) for x, y in stitch if x > -78.0 or abs(y) > 118.0]
    dots(c, stitch, 2.1, shade_of(top, -0.55), 0.9, mask=fill(inner))
    lower_shade(c, inner, shade_of(bottom, 0.2), -40.0, 120.0, width=12.0, strength=0.55)
    top_rim(c, inner, 90.0, 60.0, 0.6, color=hexrgb(shade_of(top, -0.5)))
    # the medallion and its emblem
    mx, my = 10.0, 36.0
    med = sd_circle(c, 70.0, mx, my)
    drop_shadow(c, med, dy=4.0, radius=3.5, strength=0.28)
    c.over(hexrgb(deep), fill(med))
    c.over(vgrad(c, "FFFBEE", "F1E0BC", my + 66.0, my - 66.0), fill(med + 3.0))
    c.over(hexrgb(shade_of("E6D1AA", 0.05)), fill(np.abs(sd_circle(c, 61.0, mx, my)) - 1.0) * 0.8)
    lower_shade(c, med + 3.0, "E0C898", my - 20.0, 70.0, width=8.0, strength=0.45)
    top_rim(c, med + 3.0, my + 20.0, 40.0, 0.7)
    emblem_fn(c, mx, my)
    # the label plate (the album's name is set on it)
    plate = sd_round_rect(c, 78.0, 21.0, 21.0, ox=10.0, oy=-104.0)
    drop_shadow(c, plate, dy=3.0, radius=3.0, strength=0.25)
    c.over(hexrgb(deep), fill(plate))
    c.over(vgrad(c, "FFFBEE", "F1E0BC", -83.0, -125.0), fill(plate + 2.8))
    top_rim(c, plate + 2.8, -104.0, 18.0, 0.7)
    dots(c, [(10.0 - 68.0, -104.0), (10.0 + 68.0, -104.0)], 2.4, shade_of(bottom, 0.1), 0.8)
    return "UI_AlbumCover_" + name, c, RUNTIME_COVERS


def make_cover(name, colours, fn):
    return lambda: cover(name, colours, fn)


T44_SPRITES = (icon_album, icon_album_star, icon_star_rating, icon_event_calendar, icon_visitor, icon_world_map,
               icon_season, icon_sticker) + tuple(make_cover(n, col, fn) for n, col, fn in COVERS)


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_COVERS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T44_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
