"""Pocket Wilds UI sprites added in T37 (activity framework: the Play button, the hub cards, the in-game HUD and the
result card; doc « Mini-jeux & activités », sections 2 and 3; doc 06 screens 029 and 030).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the
HUD icons: rounded filled shapes, a 4.5 px outline in a deeper tone of each material, light from the top left (a
vertical gradient, one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round five-point
stars, round dots, bubbles, hearts and paws only (never a four-point star), nothing religious (no moon crescent beside
a star, no water flower), nothing enters anything (the practice feather rests on the target, the ears stand behind
the bush, the drops fall between the can and the leaves).

Framework icons (own colors, readable at 24 and 48 dp):
- ICO_Play: the HUD rail button of the activities, a round toy ball of soft lobes (coral, cream, sky blue) with a paw.
- ICO_Practice: practice without reward, a soft target with a feather resting on its middle.
- ICO_Challenge: a fixed challenge, a round golden medal with a five-point star on a sky blue ribbon.
- ICO_Record: a new personal record, a rose ribbon rosette with a round golden star and two tails.
- ICO_Timer: a soft round leaf clock (cream face, green rim, a stem and a leaf on top), hands at ten past ten.
- ICO_Lock: the closed padlock of a locked activity card, the twin of ICO_Unlock.
- ICO_Pause: the in-game pause glyph, two round bars, white for tinting (like ICO_Close_X and ICO_Back_Chevron).

Result stars (Kit, same 192 px frame so they stack):
- UI_Star_Result_Empty: a chubby five-point star socket, soft tan outline, pale cream fill sunk under its upper rim.
- UI_Star_Result_Full: the same star in warm gold with a gloss; it covers the empty one to the last edge pixel.

Activity icons for the hub cards (until real captures exist), one per activity, each with its own colors and the
activity's gesture or object:
ICO_Act_Grooming, ICO_Act_HideAndSeek, ICO_Act_NutRain, ICO_Act_PawMemory, ICO_Act_Expeditions, ICO_Act_Garden,
ICO_Act_TreasureFishing, ICO_Act_LilyHop, ICO_Act_Bakery, ICO_Act_PhotoSafari, ICO_Act_CuddleMatch, ICO_Act_Slide,
ICO_Act_Constellations, ICO_Act_FireflyRhythm, ICO_Act_Bubbles, ICO_Act_TilePuzzle, ICO_Act_BurrowMaze,
ICO_Act_GrandContest.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t37.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, RUNTIME_KIT, blur, fill, hexrgb, lerp, normalize, sd_circle,
                           sd_ellipse, sd_polygon, sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t34a import sd_heart, sd_leaf, sd_paw
from pw_ui_sprites_t35a import drop_shadow
from pw_ui_sprites_t36 import (View, front_curve, gloss, rotate_points, sd_arc, sd_band, sd_cylinder, sd_rounded_star,
                               side_shade, sticker, vgrad)

WIDTH = 4.5  # the family's outline on a 128 px icon
SHADOW_BLUE = (40, 60, 100)  # contact shadow on snow and water


# ---------------------------------------------------------------- helpers

def edge(c, shape, color, width=WIDTH):
    """The outline of one piece drawn over what is already there (a piece in front of another)."""
    c.over(hexrgb(color), fill(shape - width))


def lower_shade(c, shape, color, y_mid, span, width=6.0, strength=0.6):
    """The deeper tone inside the lower edge of a shape (light from the top left)."""
    c.over(hexrgb(color), soft(-shape - width, width) * np.clip((y_mid - c.y) / span, 0, 1) * fill(shape) * strength)


def top_rim(c, shape, y_mid, span, strength=0.6, color=(255, 255, 255)):
    """A thin light line inside the upper edge of a shape."""
    c.over(color, soft(-shape - 2.5, 2.0) * fill(shape) * np.clip((c.y - y_mid) / span, 0, 1) * strength)


def union(*shapes):
    d = shapes[0]
    for s in shapes[1:]:
        d = np.minimum(d, s)
    return d


def midrib(c, ox, oy, half_length, angle_deg, half_width):
    """The midrib line across a leaf centered on (ox, oy) and turned by angle_deg."""
    a = math.radians(angle_deg)
    dx, dy = math.cos(a) * half_length, math.sin(a) * half_length
    return sd_polyline(c, [(ox - dx, oy - dy), (ox + dx, oy + dy)], half_width)


def ribbon_tail(c, p0, p1, half_width, notch):
    """A ribbon strip from p0 to p1 ending in a V notch (rounded by 1.5 px)."""
    ux, uy = p1[0] - p0[0], p1[1] - p0[1]
    n = math.hypot(ux, uy)
    ux, uy = ux / n, uy / n
    nx, ny = -uy, ux
    pts = [(p0[0] + nx * half_width, p0[1] + ny * half_width), (p1[0] + nx * half_width, p1[1] + ny * half_width),
           (p1[0] - ux * notch, p1[1] - uy * notch), (p1[0] - nx * half_width, p1[1] - ny * half_width),
           (p0[0] - nx * half_width, p0[1] - ny * half_width)]
    return sd_polygon(c, pts) - 1.5


def scalloped(c, r, bumps, bump_r, ox, oy, k=3.0):
    """A round rosette edge: a disc with soft bumps all around."""
    d = sd_circle(c, r, ox, oy)
    for i in range(bumps):
        a = math.tau * i / bumps + math.pi / 2.0
        d = smooth_min(d, sd_circle(c, bump_r, ox + math.cos(a) * r, oy + math.sin(a) * r), k)
    return d


def gold_star(c, outer, inner, ox, oy, rounding=2.2, edge_width=2.2, angle_deg=0.0):
    """A small rounded five-point golden star with its own deeper gold edge."""
    star = sd_rounded_star(c, outer, inner, ox, oy, rounding=rounding, angle_deg=angle_deg)
    c.over(hexrgb("C9782A"), fill(star - edge_width))
    c.over(lerp(hexrgb("FFE98F"), hexrgb("F5AE3A"), np.clip((oy + outer - c.y) / (2.0 * outer), 0, 1)), fill(star))
    return star


def sd_drop(v, half_width, half_height, taper):
    """An egg-like round shape on a view, narrower toward its top (`taper`). Far from the shape the distance is the
    plain distance to a circle, so it stays finite and smooth everywhere (pw_ui_sprites.sd_egg divides by its width)."""
    width = np.maximum(half_width * (1.0 - taper * v.y / half_height), half_width * 0.15)
    near = normalize(np.sqrt((v.x / width) ** 2 + (v.y / half_height) ** 2) - 1.0)
    size = max(half_width, half_height)
    r = np.hypot(v.x, v.y)
    return np.where(r < 1.5 * size, near, r - size)


def sd_hazelnut(c, s, ox, oy, angle_deg):
    """A hazelnut: a plump round shell with a small pointed tip on top, in icon units (s = 1 is about 26 px wide)."""
    v = View(c, ox, oy, angle_deg)
    body = sd_drop(v, 13.0 * s, 13.5 * s, 0.24)
    tip = sd_ellipse(v, 2.4 * s, 3.6 * s, ox=0.0, oy=13.0 * s)
    return smooth_min(body, tip, 2.4 * s), v


def draw_hazelnut(c, s, ox, oy, angle_deg):
    """The hazelnut's colors: a glossy chestnut shell lit from the top left, two soft streaks from the tip, a small pale
    rough base."""
    nut, v = sd_hazelnut(c, s, ox, oy, angle_deg)
    inside = fill(nut)
    c.over(lerp(hexrgb("D79656"), hexrgb("8A5530"), np.clip((13.0 * s - v.y) / (26.0 * s), 0, 1)), inside)
    for dx in (-4.5, 4.0):
        streak = sd_polyline(v, [(dx * 0.3 * s, 11.0 * s), (dx * s, 1.0 * s), (dx * 0.9 * s, -6.0 * s)], 0.7 * s)
        c.over(hexrgb("A86A38"), fill(streak) * inside * 0.6)
    base_y = -7.5 * s + 0.7 * s * np.sin(v.x * 1.1 / s)
    base = fill(v.y - base_y) * inside
    c.over(lerp(hexrgb("EBCFA4"), hexrgb("CFA676"), np.clip((-7.0 * s - v.y) / (6.0 * s), 0, 1)), base)
    for dx, dy in ((-3.5, -10.0), (2.0, -11.0), (5.5, -9.0)):
        c.over(hexrgb("B48552"), fill(np.hypot(v.x - dx * s, v.y - dy * s) - 0.8 * s) * base * 0.8)
    c.over(hexrgb("7A4A26"), soft(-nut - 4.0 * s, 4.0 * s) * np.clip((v.x + 2.0 * s) / (12.0 * s), 0, 1) * inside * 0.4)
    gl = sd_ellipse(v, 2.6 * s, 4.6 * s, ox=-5.0 * s, oy=4.0 * s, angle_deg=-18.0)
    c.over((255, 255, 255), soft(gl, 1.0 * s) * inside * 0.75)
    return nut


def icon_play():
    """ICO_Play: a round toy ball of six soft lobes around a pole turned toward the light (coral, cream, sky blue, a
    sunny sliver), a coral paw on the front lobe. One big round shape so the rail button reads at 48 dp."""
    c = Canvas(128, 128, cy=64.0)
    r, by = 45.0, 1.0
    ball = sd_circle(c, r, 0.0, by)
    sticker(c, ball, "8A4A3A", WIDTH)
    inside = fill(ball)
    # The sphere's normal at each pixel, then longitude and latitude around the pole.
    nx, ny = c.x / r, (c.y - by) / r
    nz = np.sqrt(np.clip(1.0 - nx * nx - ny * ny, 0.0, 1.0))
    p = np.array([-0.38, 0.62, 0.69])
    p /= np.linalg.norm(p)
    f = np.array([0.0, 0.0, 1.0]) - p[2] * p
    f /= np.linalg.norm(f)
    g = np.cross(p, f)
    along_p = nx * p[0] + ny * p[1] + nz * p[2]
    lon = np.arctan2(nx * g[0] + ny * g[1] + nz * g[2], nx * f[0] + ny * f[1] + nz * f[2])
    ring = r * np.sqrt(np.clip(1.0 - along_p * along_p, 0.0, 1.0))
    lobe = math.pi / 3.0
    colors = (("FFFBF2", "F3E2C6"), ("B8DEF8", "5E9AD6"), ("FFE79E", "F6B443"), ("FFB29E", "EF7565"),
              ("FFE79E", "F6B443"), ("FFB29E", "EF7565"))
    for k, (top, bottom) in enumerate(colors):
        start = -lobe / 2.0 + k * lobe
        rel = (lon - start) % math.tau
        inner = np.minimum(rel, lobe - rel)
        coverage = np.where(rel <= lobe, fill(-inner * ring), 0.0)
        c.over(vgrad(c, top, bottom, by + r, by - r), coverage * inside)
    cap = (np.arccos(np.clip(along_p, -1.0, 1.0)) - math.radians(17.0)) * r
    c.over(hexrgb("D9B48E"), fill(cap - 1.2) * inside)
    c.over(vgrad(c, "FFFFFF", "F3E6D0", by + r, by), fill(cap) * inside)
    paw_view = View(c, 2.0, by - 6.0, 31.0, 1.0, 0.92)
    c.over(hexrgb("EF7565"), fill(sd_paw(paw_view, 0.78, 0.0, 0.0)) * inside)
    lower_shade(c, ball, "C8653F", by - 4.0, 34.0, width=8.0, strength=0.4)
    top_rim(c, ball, by + 10.0, 30.0, 0.55)
    gloss(c, 9.0, 4.6, -22.0, by + 22.0, 40.0, inside, 0.8)
    return "ICO_Play", c, RUNTIME_ICONS


def icon_practice():
    """ICO_Practice (practice without reward): a soft cream and sage target, a lilac feather resting with its tip on the
    middle. Calm colors, no gold: nothing is at stake."""
    c = Canvas(128, 128, cy=64.0)
    tx, ty, r = -8.0, 8.0, 42.0
    target = sd_circle(c, r, tx, ty)
    # The feather: from its tip near the middle toward the lower right, its quill sticking out of the vane.
    a = math.radians(-44.0)
    ux, uy = math.cos(a), math.sin(a)
    tip = (tx + 5.0 * ux, ty + 5.0 * uy)

    def along(s):
        return tip[0] + ux * s, tip[1] + uy * s
    vane_c = along(31.0)
    vane = sd_leaf(c, 56.0, 11.5, vane_c[0], vane_c[1], -44.0)
    for s, side in ((24.0, 1.0), (36.0, -1.0), (44.0, 1.0)):
        px, py = along(s)
        nx, ny = -uy * side, ux * side
        outer = (px + nx * 13.0 + ux * 4.0, py + ny * 13.0 + uy * 4.0)
        slit = sd_polyline(c, [outer, (px + nx * 6.0, py + ny * 6.0)], 0.9)
        vane = np.maximum(vane, -slit)
    quill = sd_polyline(c, [along(50.0), along(66.0)], 2.4)
    feather = np.minimum(vane, quill)
    drop_shadow(c, np.minimum(target, feather) - WIDTH)
    edge(c, target, "4E7A3C")
    inside = fill(target)
    rings = ((r, "FFFAF0", "EFE1C6"), (31.0, "B4DE96", "79B65C"), (21.0, "FFFAF0", "EFE1C6"),
             (11.0, "FFC2A8", "F08C70"))
    for k, (rr, top, bottom) in enumerate(rings):
        disc = sd_circle(c, rr, tx, ty)
        if k:
            c.over(hexrgb("4E7A3C" if k < 3 else "B8583E"), fill(disc - 1.4) * 0.55)
        c.over(vgrad(c, top, bottom, ty + rr, ty - rr), fill(disc))
    lower_shade(c, target, "D9C29A", ty - 4.0, 30.0, width=6.0, strength=0.45)
    top_rim(c, target, ty + 8.0, 30.0, 0.55)
    gloss(c, 8.0, 4.0, tx - 22.0, ty + 24.0, 40.0, inside, 0.55)
    # Feather over the target.
    edge(c, feather, "6A55A8")
    c.over(lerp(hexrgb("E6DCFA"), hexrgb("A88FE0"), np.clip((along(0)[1] - c.y) / 50.0, 0, 1)), fill(vane))
    c.over(hexrgb("8C72CC"), soft(-vane - 4.0, 4.0) * np.clip((c.x - c.y - 40.0) / 30.0, 0, 1) * fill(vane) * 0.5)
    c.over(hexrgb("F3EEFF"), fill(sd_polyline(c, [along(3.0), along(52.0)], 1.1)) * fill(vane + 0.5) * 0.9)
    c.over(hexrgb("F3EEFF"), fill(quill + 0.4))
    return "ICO_Practice", c, RUNTIME_ICONS


def icon_challenge():
    """ICO_Challenge (a fixed challenge): a round golden medal with an embossed five-point star, hanging on a sky blue
    ribbon in a V."""
    c = Canvas(128, 128, cy=64.0)
    mx, my, r = 0.0, -17.0, 32.0
    medal = sd_circle(c, r, mx, my)
    left = sd_polygon(c, [(-38.0, 54.0), (-19.0, 54.0), (3.0, -8.0), (-15.0, -8.0)]) - 1.5
    right = sd_polygon(c, [(19.0, 54.0), (38.0, 54.0), (15.0, -8.0), (-3.0, -8.0)]) - 1.5
    drop_shadow(c, union(medal, left, right) - WIDTH)
    for strap, top, bottom, stripe in ((left, "8DBDE8", "4C86C6", (-28.5, 54.0, -6.0, -8.0)),
                                       (right, "A9D3F5", "5E95D2", (28.5, 54.0, 6.0, -8.0))):
        edge(c, strap, "2F5E9E")
        c.over(vgrad(c, top, bottom, 54.0, 0.0), fill(strap))
        x0, y0, x1, y1 = stripe
        c.over(hexrgb("E3F1FF"), fill(sd_polyline(c, [(x0, y0), (x1, y1)], 2.2)) * fill(strap + 1.0) * 0.85)
    edge(c, medal, "A8662A")
    inside = fill(medal)
    c.over(vgrad(c, "FFE48A", "F2A93B", my + r, my - r), inside)
    lower_shade(c, medal, "E0892C", my - 4.0, 24.0, width=5.0, strength=0.6)
    top_rim(c, medal, my + 6.0, 24.0, 0.6, color=hexrgb("FFF6C8"))
    inner = sd_circle(c, 23.5, mx, my)
    c.over(hexrgb("D9922E"), fill(np.abs(inner) - 1.3) * 0.85)
    c.over(hexrgb("FFF1B8"), fill(np.abs(inner + 2.6) - 0.9) * np.clip((c.y - my) / 20.0, 0, 1) * 0.7)
    star = sd_rounded_star(c, 17.5, 8.8, mx, my - 0.5, rounding=2.4)
    c.over(hexrgb("C9782A"), fill(star - 2.2))
    c.over(vgrad(c, "FFFBE6", "FFDF86", my + 17.0, my - 15.0), fill(star))
    gloss(c, 7.0, 3.6, mx - 15.0, my + 18.0, 38.0, inside, 0.75)
    return "ICO_Challenge", c, RUNTIME_ICONS


def icon_record():
    """ICO_Record (a new personal record): a rose ribbon rosette, its pleats around a cream disc holding a round golden
    star, two notched tails below."""
    c = Canvas(128, 128, cy=64.0)
    rx, ry = 0.0, 15.0
    tail_l = ribbon_tail(c, (-3.0, ry - 6.0), (-22.0, -52.0), 9.5, 7.0)
    tail_r = ribbon_tail(c, (3.0, ry - 6.0), (21.0, -50.0), 9.5, 7.0)
    rosette = scalloped(c, 32.0, 14, 8.0, rx, ry)
    drop_shadow(c, union(tail_l, tail_r, rosette) - WIDTH)
    for tail, top, bottom in ((tail_l, "E57A9A", "BF4D6E"), (tail_r, "F290AE", "CF5A7C")):
        edge(c, tail, "9E3A5A")
        c.over(vgrad(c, top, bottom, ry - 10.0, -52.0), fill(tail))
    edge(c, rosette, "9E3A5A")
    inside = fill(rosette)
    c.over(vgrad(c, "FFC0D2", "EC7C9C", ry + 40.0, ry - 40.0), inside)
    # Pleats: soft radial folds between the heart and the scalloped edge.
    ang = np.arctan2(c.y - ry, c.x - rx)
    dist = np.hypot(c.x - rx, c.y - ry)
    folds = 0.5 + 0.5 * np.cos(ang * 14.0)
    c.over(hexrgb("D9658A"), inside * folds * np.clip((dist - 22.0) / 6.0, 0, 1) * 0.35)
    lower_shade(c, rosette, "D45A80", ry - 6.0, 26.0, width=6.0, strength=0.5)
    top_rim(c, rosette, ry + 8.0, 26.0, 0.55)
    face = sd_circle(c, 20.5, rx, ry)
    c.over(hexrgb("B4495A"), fill(face - 2.2))
    c.over(vgrad(c, "FFFBF2", "F6E4CC", ry + 20.0, ry - 20.0), fill(face))
    lower_shade(c, face, "E8CFAA", ry - 4.0, 16.0, width=4.0, strength=0.6)
    gold_star(c, 16.0, 8.2, rx, ry - 0.5, rounding=2.2, edge_width=2.0)
    gloss(c, 6.5, 3.4, rx - 18.0, ry + 22.0, 38.0, inside, 0.7)
    return "ICO_Record", c, RUNTIME_ICONS


def icon_timer():
    """ICO_Timer: a soft round leaf clock, never an alarm: a green rim, a cream face with four round marks, two round
    hands at ten past ten, a short stem and a leaf on top."""
    c = Canvas(128, 128, cy=62.0)
    cx, cy, r = 0.0, -6.0, 41.0
    disc = sd_circle(c, r, cx, cy)
    stem = sd_polyline(c, [(cx - 1.0, cy + r - 4.0), (cx + 1.0, cy + r + 8.0)], 3.2)
    leaf = sd_leaf(c, 30.0, 9.5, cx + 15.0, cy + r + 10.0, 22.0)
    shape = union(disc, stem, leaf)
    sticker(c, shape, "3F7A33", WIDTH)
    c.over(vgrad(c, "A87A4C", "7A5232", cy + r + 8.0, cy + r - 4.0), fill(stem))
    c.over(vgrad(c, "B4E38A", "5E9E4A", cy + r + 18.0, cy + r + 2.0), fill(leaf))
    c.over(hexrgb("4F8A3F"), fill(midrib(c, cx + 15.0, cy + r + 10.0, 10.0, 22.0, 0.9)) * fill(leaf) * 0.7)
    inside = fill(disc)
    c.over(vgrad(c, "A6DB7A", "5E9E4A", cy + r, cy - r), inside)
    top_rim(c, disc, cy + 8.0, 30.0, 0.6)
    face = sd_circle(c, 31.5, cx, cy)
    c.over(hexrgb("4F8A3F"), fill(face - 1.6) * 0.9)
    c.over(vgrad(c, "FFFBF0", "F3E4C8", cy + 31.0, cy - 31.0), fill(face))
    # The face sits a little sunk in its rim: a soft shade under the upper edge.
    c.over(hexrgb("E3D0AE"), soft(-face - 5.0, 5.0) * np.clip((c.y - cy) / 24.0, 0, 1) * fill(face) * 0.7)
    for k in range(4):
        a = math.tau * k / 4.0
        c.over(hexrgb("C9A97A"), fill(sd_circle(c, 2.6, cx + math.cos(a) * 24.0, cy + math.sin(a) * 24.0)))
    for angle, length, half in ((30.0, 20.0, 2.8), (150.0, 14.0, 3.2)):
        a = math.radians(angle)
        hand = sd_polyline(c, [(cx, cy), (cx + math.cos(a) * length, cy + math.sin(a) * length)], half)
        c.over(hexrgb("6B4528"), fill(hand))
    c.over(hexrgb("6B4528"), fill(sd_circle(c, 5.0, cx, cy)))
    c.over(hexrgb("F2A93B"), fill(sd_circle(c, 2.6, cx, cy)))
    gloss(c, 8.0, 3.8, cx - 17.0, cy + 15.0, 38.0, fill(face), 0.6)
    return "ICO_Timer", c, RUNTIME_ICONS


def icon_lock():
    """ICO_Lock: the closed padlock of a locked activity, the twin of ICO_Unlock (gold body, keyhole, steel shackle
    with both legs in the body)."""
    c = Canvas(128, 128, cy=60.0)
    body = sd_round_rect(c, 36.0, 26.0, 10.0, oy=-28.0)
    r, cx0, top = 17.0, 0.0, 27.0
    legs = np.minimum(sd_round_rect(c, 0.01, 17.0, 0.0, ox=cx0 - r, oy=top - 17.0),
                      sd_round_rect(c, 0.01, 17.0, 0.0, ox=cx0 + r, oy=top - 17.0))
    arc = np.where(c.y >= top, np.abs(np.hypot(c.x - cx0, c.y - top) - r), 1e3)
    stroke = np.minimum(legs, arc) - 6.0
    outline = np.minimum(body - 4.5, stroke - 4.0)
    c.over((80, 52, 20), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("8E5A24"), fill(outline))
    c.over(lerp(hexrgb("F4F1EA"), hexrgb("C9C2B4"), np.clip((top + r - c.y) / 50.0, 0, 1)), fill(stroke))
    shine = np.where(c.x < cx0 - r, np.abs(c.x - (cx0 - r - 2.0)) - 1.2, 1e3)
    c.over((255, 255, 255), fill(shine) * fill(stroke) * 0.7)
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F2B544"), np.clip((-2.0 - c.y) / 52.0, 0, 1)), fill(body))
    c.over(hexrgb("E39A2E"), soft(-body - 5.0, 5.0) * np.clip((-28.0 - c.y) / 26.0, 0, 1) * fill(body) * 0.7)
    hole = np.minimum(sd_circle(c, 7.0, 0.0, -24.0), sd_round_rect(c, 3.0, 8.0, 3.0, oy=-34.0))
    c.over(hexrgb("8E5A24"), fill(hole))
    c.over((255, 255, 255), fill(sd_ellipse(c, 7.0, 3.2, ox=-18.0, oy=-12.0, angle_deg=15.0)) * 0.7)
    return "ICO_Lock", c, RUNTIME_ICONS


def icon_pause():
    """ICO_Pause: 96 px, two soft round-capped bars, white for tinting (the HUD's round button, like ICO_Close_X)."""
    c = Canvas(96, 96)
    bars = np.minimum(sd_round_rect(c, 7.5, 22.0, 7.5, ox=-13.0), sd_round_rect(c, 7.5, 22.0, 7.5, ox=13.0))
    c.over((255, 255, 255), fill(bars))
    return "ICO_Pause", c, RUNTIME_ICONS


# ---------------------------------------------------------------- result stars (shared frame)

STAR_SIZE, STAR_CY = 192, 99.0
STAR_OUTLINE = 6.5
STAR_EMPTY_OUTLINE = 5.5  # one pixel inside the full star's outline, so the full star hides it to the last edge pixel


def result_star(c):
    """The chubby result star: five round points, a deep inner radius, well rounded."""
    return sd_rounded_star(c, 70.0, 39.0, 0.0, 0.0, rounding=11.0)


def kit_star_result_empty():
    """UI_Star_Result_Empty: the star not yet won, a soft socket. Tan outline, pale cream fill, a soft shade under its
    upper rim (it is sunk) and a light lower rim. No drop shadow: it lies in the card."""
    c = Canvas(STAR_SIZE, STAR_SIZE, cy=STAR_CY)
    star = result_star(c)
    c.over(hexrgb("D9C3A0"), fill(star - STAR_EMPTY_OUTLINE))
    inside = fill(star)
    c.over(vgrad(c, "F1E5CF", "FBF4E8", 70.0, -64.0), inside)
    c.over(hexrgb("E2CFAE"), soft(-star - 9.0, 9.0) * np.clip((c.y + 6.0) / 40.0, 0, 1) * inside * 0.75)
    c.over(hexrgb("FFFDF7"), soft(-star - 3.0, 2.5) * np.clip((-c.y - 4.0) / 40.0, 0, 1) * inside * 0.9)
    return "UI_Star_Result_Empty", c, RUNTIME_KIT


def kit_star_result_full():
    """UI_Star_Result_Full: the won star, warm gold, a deeper lower shade, a light upper rim, a soft warm heart and one
    gloss. Same shape and frame as UI_Star_Result_Empty, which it covers exactly."""
    c = Canvas(STAR_SIZE, STAR_SIZE, cy=STAR_CY)
    star = result_star(c)
    drop_shadow(c, star - STAR_OUTLINE, dy=5.5, radius=4.5, strength=0.3)
    c.over(hexrgb("C9782A"), fill(star - STAR_OUTLINE))
    inside = fill(star)
    c.over(vgrad(c, "FFE48A", "F5AC38", 72.0, -66.0), inside)
    c.over(hexrgb("FFF3B8"), soft(sd_circle(c, 18.0, -6.0, 8.0), 26.0) * inside * 0.55)
    lower_shade(c, star, "E38E2C", -10.0, 50.0, width=10.0, strength=0.7)
    top_rim(c, star, 0.0, 60.0, 0.75, color=hexrgb("FFF8D6"))
    gloss(c, 12.0, 6.5, -21.0, 30.0, 36.0, inside, 0.8)
    return "UI_Star_Result_Full", c, RUNTIME_KIT


# ---------------------------------------------------------------- activity icons (hub cards)

def icon_act_grooming():
    """ICO_Act_Grooming: a wooden brush seen from the side, a row of soft pink bristle tufts under its back, a little
    leaf just brushed off flying away with two round dots."""
    c = Canvas(128, 128, cy=64.0)
    k = 1.14
    bv = View(c, -4.0, -9.0, -18.0)
    back = sd_round_rect(bv, 30.0 * k, 10.0 * k, 9.5 * k, ox=-6.0 * k, oy=6.0 * k)
    handle = sd_polyline(bv, [(20.0 * k, 9.0 * k), (46.0 * k, 11.0 * k)], 7.0 * k)
    tufts = sd_round_rect(bv, 27.0 * k, 4.0 * k, 3.0 * k, ox=-6.0 * k, oy=-5.0 * k)
    for i in range(8):
        tufts = smooth_min(tufts, sd_ellipse(bv, 3.7 * k, 8.5 * k, ox=(-30.5 + i * 7.0) * k, oy=-10.0 * k), 1.5 * k)
    brush = union(back, handle, tufts)
    lx, ly = -30.0, 41.0
    leaf = sd_leaf(c, 33.0, 10.5, lx, ly, 152.0)
    drop_shadow(c, union(brush, leaf) - WIDTH)
    edge(c, brush, "5E3F24")
    c.over(lerp(hexrgb("FFDCE6"), hexrgb("EE92AC"), np.clip((0.0 - bv.y) / (18.0 * k), 0, 1)), fill(tufts))
    for i in range(7):
        split = np.abs(bv.x - (-27.0 + i * 7.0) * k) - 0.8
        c.over(hexrgb("D9658A"), fill(split) * fill(tufts) * (bv.y < -3.0 * k) * 0.5)
    wood = lerp(hexrgb("F2CB91"), hexrgb("B98049"), np.clip((16.0 * k - bv.y) / (20.0 * k), 0, 1))
    c.over(wood, fill(np.minimum(back, handle)))
    c.over(hexrgb("A8703C"), soft(-back - 4.5, 4.5) * np.clip((4.0 * k - bv.y) / (8.0 * k), 0, 1) * fill(back) * 0.5)
    for (x0, y0), (x1, y1), w, mask, alpha in (((-30.0, 12.5), (14.0, 12.5), 1.4, back, 0.85),
                                               ((24.0, 13.5), (42.0, 14.5), 1.2, handle, 0.7)):
        light = sd_polyline(bv, [(x0 * k, y0 * k), (x1 * k, y1 * k)], w)
        c.over(hexrgb("FBE3BC"), fill(light) * fill(mask) * alpha)
    c.over(hexrgb("6B4528"), fill(np.hypot(bv.x - 41.0 * k, bv.y - 11.0 * k) - 2.8))
    edge(c, leaf, "3F7A33")
    c.over(vgrad(c, "B4E38A", "5E9E4A", ly + 10.0, ly - 10.0), fill(leaf))
    c.over(hexrgb("4F8A3F"), fill(midrib(c, lx, ly, 11.0, 152.0, 0.9)) * fill(leaf) * 0.7)
    gloss(c, 5.0, 2.0, lx + 3.0, ly + 4.0, -28.0, fill(leaf), 0.45)
    for dx, dy, rr in ((-5.0, 47.0, 3.8), (-52.0, 20.0, 3.2)):
        dot = sd_circle(c, rr, dx, dy)
        c.over(hexrgb("E39A3A"), fill(dot - 1.5))
        c.over(hexrgb("FFE9A8"), fill(dot))
    return "ICO_Act_Grooming", c, RUNTIME_ICONS


def icon_act_hide_and_seek():
    """ICO_Act_HideAndSeek: a round green bush, two soft cream ears with pink insides peeking from behind it."""
    c = Canvas(128, 128, cy=60.0)
    bush = union(sd_circle(c, 25.0, -24.0, -14.0), sd_circle(c, 29.0, 3.0, -2.0), sd_circle(c, 23.0, 29.0, -16.0),
                 sd_round_rect(c, 52.0, 14.0, 13.0, oy=-30.0))
    ears = []
    for ex, angle in ((-9.0, 10.0), (13.0, -12.0)):
        ears.append((sd_ellipse(c, 21.0, 8.5, ox=ex, oy=34.0, angle_deg=90.0 + angle), ex, angle))
    ear_shape = union(*[e[0] for e in ears])
    drop_shadow(c, np.minimum(bush, ear_shape) - WIDTH)
    edge(c, ear_shape, "8A5A2E")
    for ear, ex, angle in ears:
        c.over(vgrad(c, "FFF8EE", "EAD5BA", 55.0, 20.0), fill(ear))
        inner = sd_ellipse(c, 14.0, 4.2, ox=ex, oy=36.0, angle_deg=90.0 + angle)
        c.over(vgrad(c, "FFC2CF", "F28FA8", 50.0, 24.0), fill(inner))
        c.over((255, 255, 255), soft(-ear - 2.0, 1.5) * fill(ear) * np.clip((c.y - 34.0) / 20.0, 0, 1) * 0.6)
    edge(c, bush, "3F6B2E")
    inside = fill(bush)
    c.over(vgrad(c, "A6DB7A", "4F8A3F", 28.0, -44.0), inside)
    # Inner bumps: the three round masses of leaves, each lit on its top.
    for bx, by, br in ((-24.0, -14.0, 25.0), (3.0, -2.0, 29.0), (29.0, -16.0, 23.0)):
        d = sd_circle(c, br, bx, by)
        c.over(hexrgb("C2EC9E"), soft(-d - 3.0, 2.5) * fill(d) * np.clip((c.y - by) / br, 0, 1) * inside * 0.7)
    for bx, by, br in ((-24.0, -14.0, 25.0), (29.0, -16.0, 23.0)):
        d = np.abs(sd_circle(c, br, bx, by)) - 1.2
        c.over(hexrgb("4F8A3F"), fill(d) * inside * np.clip((c.y - by + 4.0) / 10.0, 0, 1) * 0.55)
    lower_shade(c, bush, "3F7A33", -26.0, 20.0, width=6.0, strength=0.45)
    for bx, by in ((-30.0, -4.0), (16.0, 10.0), (36.0, -14.0), (-6.0, -22.0)):
        berry = sd_circle(c, 3.4, bx, by)
        c.over(hexrgb("B4495A"), fill(berry - 1.2))
        c.over(hexrgb("F28FA8"), fill(berry))
        c.over((255, 255, 255), fill(sd_circle(c, 1.0, bx - 1.0, by + 1.0)) * 0.9)
    gloss(c, 7.0, 3.6, -30.0, 1.0, 38.0, inside, 0.45, edge=1.5)
    return "ICO_Act_HideAndSeek", c, RUNTIME_ICONS


def icon_act_nut_rain():
    """ICO_Act_NutRain: a woven basket below, three hazelnuts tumbling down toward it at different heights, one nut
    already resting in it."""
    c = Canvas(128, 128, cy=64.0)
    rim_y = -20.0
    bowl = np.maximum(sd_ellipse(c, 46.0, 32.0, ox=0.0, oy=rim_y - 4.0), c.y - rim_y)
    rim = sd_ellipse(c, 46.0, 9.0, ox=0.0, oy=rim_y)
    basket = smooth_min(bowl, rim, 3.0)
    nuts = ((-22.0, 26.0, 1.0, -14.0), (24.0, 34.0, 0.85, 16.0), (6.0, 6.0, 0.95, 6.0))
    shapes = [sd_hazelnut(c, s, x, y, a)[0] for x, y, s, a in nuts]
    inner = sd_hazelnut(c, 0.9, -22.0, rim_y + 4.0, -28.0)[0]
    drop_shadow(c, union(basket, inner, *shapes) - WIDTH)
    # The nut resting in the basket first: the basket's rim passes in front of its lower half.
    c.over(hexrgb("5E3F24"), fill(inner - 4.0))
    draw_hazelnut(c, 0.9, -22.0, rim_y + 4.0, -28.0)
    edge(c, basket, "5E3F24")
    c.over(lerp(hexrgb("D9A266"), hexrgb("9A6234"), np.clip((rim_y - c.y) / 32.0, 0, 1)), fill(bowl))
    for k in range(8):
        x0 = -42.0 + k * 12.0
        dark = sd_polyline(c, [(x0 - 4.0, rim_y - 6.0), (x0 + 6.0, rim_y - 32.0)], 1.5)
        light = sd_polyline(c, [(x0 + 5.0, rim_y - 7.0), (x0 - 3.0, rim_y - 29.0)], 1.3)
        c.over(hexrgb("8E5A2E"), fill(dark) * fill(bowl) * 0.6)
        c.over(hexrgb("F0C890"), fill(light) * fill(bowl) * 0.55)
    for y in (rim_y - 17.0, rim_y - 28.0):
        c.over(hexrgb("B98652"), fill(np.abs(c.y - y) - 1.0) * fill(bowl) * 0.6)
    lower_shade(c, bowl, "7A4E2A", rim_y - 20.0, 18.0, width=6.0, strength=0.45)
    c.over(vgrad(c, "F2CF98", "C48B52", rim_y + 9.0, rim_y - 9.0), fill(rim))
    for k in range(10):
        x0 = -40.0 + k * 9.0
        twig = sd_polyline(c, [(x0 - 3.5, rim_y - 6.0), (x0 + 3.5, rim_y + 5.0)], 1.2)
        c.over(hexrgb("9A6234"), fill(twig) * fill(rim) * 0.65)
    c.over(hexrgb("FFF0D0"), fill(np.abs(c.y - (rim_y + 4.0)) - 0.8) * fill(rim + 2.0) * 0.5)
    for (x, y, s, a), nut in zip(nuts, shapes):
        c.over(hexrgb("5E3F24"), fill(nut - 4.0))
        draw_hazelnut(c, s, x, y, a)
    return "ICO_Act_NutRain", c, RUNTIME_ICONS


def icon_act_paw_memory():
    """ICO_Act_PawMemory: two face-down memory cards, a lilac one behind and a sky blue one in front, each back with a
    cream border and a paw."""
    c = Canvas(128, 128, cy=64.0)
    cards = ((-15.0, 6.0, -13.0, "6A55A8", "D5C6F5", "A48ADF"), (15.0, -4.0, 11.0, "3E5A86", "B4DAF7", "5E95D2"))
    shapes = []
    for x, y, angle, *_ in cards:
        v = View(c, x, y, angle)
        shapes.append((sd_round_rect(v, 26.0, 36.0, 10.0), v))
    drop_shadow(c, union(*[s for s, _ in shapes]) - WIDTH)
    for (card, v), (x, y, angle, line, top, bottom) in zip(shapes, cards):
        edge(c, card, line)
        inside = fill(card)
        c.over(lerp(hexrgb(top), hexrgb(bottom), np.clip((36.0 - v.y) / 72.0, 0, 1)), inside)
        border = np.abs(sd_round_rect(v, 19.5, 29.5, 6.5)) - 1.3
        c.over(hexrgb("FFF6E6"), fill(border) * inside * 0.9)
        for px, py in ((-12.0, 22.0), (12.0, 22.0), (-12.0, -22.0), (12.0, -22.0)):
            c.over(hexrgb("FFF6E6"), fill(np.hypot(v.x - px, v.y - py) - 2.0) * inside * 0.9)
        c.over(hexrgb("FFF6E6"), fill(sd_paw(v, 0.62, 0.0, 0.5)) * inside)
        c.over(hexrgb(line), soft(-card - 5.0, 5.0) * np.clip((-v.y) / 36.0, 0, 1) * inside * 0.3)
        c.over((255, 255, 255), soft(-card - 2.5, 2.0) * inside * np.clip(v.y / 36.0, 0, 1) * 0.6)
        c.over((255, 255, 255), soft(sd_ellipse(v, 3.4, 9.0, ox=-19.0, oy=17.0), 1.5) * inside * 0.45)
    return "ICO_Act_PawMemory", c, RUNTIME_ICONS


def icon_act_expeditions():
    """ICO_Act_Expeditions: a cream map folded in three, a dotted path from a green wood to a little heart past a blue
    lake, a round orange backpack standing in front."""
    c = Canvas(128, 128, cy=64.0)
    mv = View(c, -16.0, 14.0, 10.0)
    paper = sd_round_rect(mv, 32.0, 25.0, 5.0)
    bx, by = 20.0, -18.0
    body = sd_round_rect(c, 26.0, 30.0, 13.0, ox=bx, oy=by)
    loop = np.where(c.y > by + 26.0, np.abs(np.hypot(c.x - bx, c.y - (by + 28.0)) - 8.0) - 2.6, 1e3)
    pack = np.minimum(body, loop)
    drop_shadow(c, np.minimum(paper, pack) - WIDTH)
    edge(c, paper, "7A5A3A")
    inside = fill(paper)
    c.over(lerp(hexrgb("FFF8E8"), hexrgb("F1DFBC"), np.clip((25.0 - mv.y) / 50.0, 0, 1)), inside)
    middle = fill(np.abs(mv.x) - 10.7)
    c.over(hexrgb("E9D3AA"), middle * inside * 0.55)
    for fx in (-10.7, 10.7):
        c.over(hexrgb("CDAE80"), fill(np.abs(mv.x - fx) - 0.7) * inside * 0.8)
    c.over(hexrgb("8FCB6A"), fill(sd_circle(mv, 7.5, -20.0, 10.0)) * inside)
    c.over(hexrgb("A6DB7A"), fill(sd_circle(mv, 4.5, -22.0, 12.0)) * inside)
    c.over(hexrgb("8EC8F2"), fill(sd_ellipse(mv, 8.0, 5.0, ox=2.0, oy=-13.0)) * inside)
    for px, py in ((-16.0, -2.0), (-10.0, -8.0), (-3.0, -4.0), (4.0, 2.0), (11.0, 7.0), (17.0, 12.0)):
        c.over(hexrgb("E0735E"), fill(np.hypot(mv.x - px, mv.y - py) - 1.9) * inside)
    hx, hy = rotate_points([(23.0, 15.0)], 10.0, -16.0, 14.0)[0]
    heart = sd_heart(c, 12.0, hx, hy)
    c.over(hexrgb("B4495A"), fill(heart - 1.3))
    c.over(hexrgb("F28FA8"), fill(heart))
    top_rim(c, paper, 14.0, 12.0, 0.5)
    # The backpack in front of the map.
    edge(c, pack, "8A4A26")
    c.over(vgrad(c, "C8784A", "9A5530", by + 38.0, by + 26.0), fill(loop))
    c.over(vgrad(c, "FFC48A", "E5813F", by + 30.0, by - 30.0), fill(body))
    lower_shade(c, body, "C8653F", by - 10.0, 20.0, width=6.0, strength=0.5)
    flap = np.maximum(body, np.minimum((by + 2.0) - c.y, sd_ellipse(c, 26.0, 10.0, ox=bx, oy=by + 2.0)))
    c.over(hexrgb("8A4A26"), fill(flap - 2.0) * fill(body))
    c.over(vgrad(c, "F79A5C", "D46A34", by + 30.0, by - 8.0), fill(flap))
    top_rim(c, body, by + 10.0, 20.0, 0.55)
    buckle = sd_round_rect(c, 4.5, 3.5, 1.5, ox=bx, oy=by - 7.0)
    c.over(hexrgb("8A4A26"), fill(buckle - 1.5))
    c.over(hexrgb("FFE08A"), fill(buckle))
    pocket = sd_round_rect(c, 15.0, 8.5, 6.0, ox=bx, oy=by - 20.0)
    c.over(hexrgb("8A4A26"), fill(pocket - 1.6) * 0.85)
    c.over(vgrad(c, "F7A468", "DC7038", by - 12.0, by - 28.0), fill(pocket))
    c.over(hexrgb("FFE2C2"), fill(np.abs(sd_round_rect(c, 11.5, 5.0, 3.5, ox=bx, oy=by - 20.0)) - 0.6) * 0.7)
    gloss(c, 3.6, 8.0, bx - 17.0, by + 4.0, -8.0, fill(body), 0.4, edge=2.0)
    return "ICO_Act_Expeditions", c, RUNTIME_ICONS


def icon_act_garden():
    """ICO_Act_Garden: a terracotta pot with a two-leaf sprout, a small mint watering can above it pouring three round
    drops on the leaves."""
    c = Canvas(128, 128, cy=64.0)
    px, rim_y = 22.0, -24.0
    pot = sd_polygon(c, [(px - 17.0, rim_y - 2.0), (px + 17.0, rim_y - 2.0), (px + 12.0, rim_y - 30.0),
                         (px - 12.0, rim_y - 30.0)]) - 3.0
    rim = sd_round_rect(c, 22.0, 6.0, 4.5, ox=px, oy=rim_y)
    stem = sd_polyline(c, [(px, rim_y + 4.0), (px + 1.0, rim_y + 16.0)], 2.8)
    leaf_l = sd_leaf(c, 25.0, 8.5, px - 11.0, rim_y + 20.0, 158.0)
    leaf_r = sd_leaf(c, 27.0, 9.0, px + 13.0, rim_y + 23.0, 24.0)
    sprout = union(stem, leaf_l, leaf_r)
    # The can, tipped forward above the pot: body, handle, spout, rose.
    cv = View(c, -28.0, 30.0, -24.0)
    body = sd_round_rect(cv, 18.0, 14.0, 10.0)
    handle = np.where(cv.x < -5.0, np.abs(np.hypot(cv.x + 16.0, cv.y - 3.0) - 12.5) - 2.8, 1e3)
    handle = np.maximum(handle, -body)
    spout = sd_polyline(cv, [(12.0, 3.0), (27.0, 11.0)], 3.4)
    rose = sd_ellipse(cv, 3.4, 6.8, ox=29.0, oy=12.0, angle_deg=-30.0)
    can = union(body, handle, spout, rose)
    drop_pts = ((10.0, 17.0), (17.0, 22.0), (19.0, 11.0))
    drops = [(sd_drop(View(c, x, y, 0.0), 2.8, 3.9, 0.45), x, y) for x, y in drop_pts]
    drop_shape = union(*[d for d, _, _ in drops])
    drop_shadow(c, union(pot, rim, sprout, can, drop_shape) - WIDTH)
    edge(c, sprout, "3F7A33")
    c.over(vgrad(c, "B4E38A", "4F8A3F", rim_y + 32.0, rim_y + 4.0), fill(sprout))
    for lx, ly, ang in ((px - 11.0, rim_y + 20.0, 158.0), (px + 13.0, rim_y + 23.0, 24.0)):
        a = math.radians(ang)
        c.over(hexrgb("4F8A3F"), fill(sd_polyline(c, [(lx - math.cos(a) * 9.0, ly - math.sin(a) * 9.0),
                                                       (lx + math.cos(a) * 9.0, ly + math.sin(a) * 9.0)], 0.8)) * 0.7)
    gloss(c, 5.5, 2.0, px - 13.0, rim_y + 24.0, -22.0, fill(leaf_l), 0.45)
    edge(c, union(pot, rim), "7A3F2A")
    c.over(vgrad(c, "EFA477", "C8653F", rim_y - 2.0, rim_y - 34.0), fill(pot))
    lower_shade(c, pot, "B4573A", rim_y - 20.0, 14.0, width=4.0, strength=0.55)
    c.over(hexrgb("7A3F2A"), fill(rim - 2.4))
    c.over(vgrad(c, "F7B98E", "D9774C", rim_y + 6.0, rim_y - 6.0), fill(rim))
    c.over(hexrgb("FFD9B8"), fill(np.abs(c.y - (rim_y + 2.5)) - 0.9) * fill(rim) * 0.7)
    gloss(c, 2.6, 6.0, px - 9.0, rim_y - 14.0, -10.0, fill(pot), 0.4, edge=1.5)
    edge(c, can, "2F6E78")
    metal = lerp(hexrgb("B4E8E4"), hexrgb("4FA8A8"), np.clip((14.0 - cv.y) / 28.0, 0, 1))
    c.over(metal, fill(union(body, spout, rose)))
    c.over(vgrad(c, "8FD3CF", "3F8E8E", 50.0, 26.0), fill(handle))
    lower_shade(c, body, "3F8E8E", 22.0, 16.0, width=5.0, strength=0.5)
    c.over(hexrgb("2F6E78"), fill(np.abs(rose + 1.4) - 0.8) * 0.6)
    band = np.abs(cv.y + 5.0) - 2.0
    c.over(hexrgb("E9FBF8"), fill(band) * fill(body) * 0.6)
    gloss(c, 5.5, 2.8, -37.0, 39.0, 18.0, fill(body), 0.7)
    for d, x, y in drops:
        c.over(hexrgb("2F5E9E"), fill(d - 2.0))
        c.over(vgrad(c, "E3F3FF", "7FB8EC", y + 4.0, y - 4.0), fill(d))
        c.over((255, 255, 255), fill(sd_circle(c, 0.9, x - 1.0, y + 0.7)) * 0.9)
    return "ICO_Act_Garden", c, RUNTIME_ICONS


def sd_scallop(c, hx, hy, radius, spread_deg):
    """A scallop shell hanging by its hinge at (hx, hy): a fan opening downward with round scallops on its edge, and
    its two little ears across the hinge."""
    pts = [(hx, hy)]
    for k in range(25):
        a = math.radians(-90.0 - spread_deg + 2.0 * spread_deg * k / 24.0)
        pts.append((hx + math.cos(a) * radius, hy + math.sin(a) * radius))
    fan = sd_polygon(c, pts) - 3.0
    for k in range(7):
        a = math.radians(-90.0 - spread_deg + 2.0 * spread_deg * (k + 0.5) / 7.0)
        bump = sd_circle(c, 6.4, hx + math.cos(a) * (radius - 1.5), hy + math.sin(a) * (radius - 1.5))
        fan = smooth_min(fan, bump, 2.0)
    ears = sd_round_rect(c, 12.0, 5.0, 4.0, ox=hx, oy=hy - 3.0)
    return smooth_min(fan, ears, 2.5)


def icon_act_treasure_fishing():
    """ICO_Act_TreasureFishing: a little wooden rod, its line lifting a pink scallop shell out of the water, two round
    drops falling back into the ripples below. No hook, no fish."""
    c = Canvas(128, 128, cy=64.0)
    water = sd_ellipse(c, 44.0, 11.0, ox=0.0, oy=-44.0)
    rod = sd_polyline(c, [(-52.0, 20.0), (-32.0, 40.0), (-6.0, 53.0)], 3.4)
    hx, hy = 8.0, 16.0
    shell = sd_scallop(c, hx, hy, 34.0, 56.0)
    line = sd_polyline(c, [(-6.0, 53.0), (hx, hy + 1.0)], 1.3)
    drops = [sd_drop(View(c, x, y, 0.0), 3.0, 4.2, 0.45) for x, y in ((-2.0, -30.0), (16.0, -36.0))]
    drop_shadow(c, union(water, rod, shell) - WIDTH, strength=0.24)
    edge(c, water, "3E6E9E")
    c.over(vgrad(c, "CFEAFB", "8EC8F2", -33.0, -55.0), fill(water))
    for rr in (30.0, 16.0):
        ring = np.abs(sd_ellipse(c, rr, rr * 0.26, ox=hx - 2.0, oy=-43.0)) - 1.3
        c.over(hexrgb("F2FAFF"), fill(ring) * fill(water) * 0.9)
    edge(c, rod, "5E3F24", 3.5)
    c.over(vgrad(c, "E3B37A", "A8733F", 53.0, 20.0), fill(rod))
    c.over(hexrgb("6B4528"), fill(line))
    edge(c, shell, "B4495A")
    inside = fill(shell)
    c.over(vgrad(c, "FFD8E2", "F08FA8", hy, hy - 40.0), inside)
    for k in range(6):
        a = math.radians(-90.0 - 56.0 + 112.0 * (k + 1.0) / 7.0)
        rib = sd_polyline(c, [(hx + math.cos(a) * 9.0, hy + math.sin(a) * 9.0),
                              (hx + math.cos(a) * 31.0, hy + math.sin(a) * 31.0)], 1.0)
        c.over(hexrgb("E06E90"), fill(rib) * inside * 0.75)
    lower_shade(c, shell, "E06E90", hy - 22.0, 16.0, width=5.0, strength=0.45)
    ears = sd_round_rect(c, 12.0, 5.0, 4.0, ox=hx, oy=hy - 3.0)
    c.over(hexrgb("B4495A"), fill(np.abs(ears) - 0.8) * inside * np.clip((hy - 5.0 - c.y) * 2.0, 0, 1) * 0.6)
    gloss(c, 3.2, 8.0, hx - 13.0, hy - 16.0, 30.0, inside, 0.6, edge=1.5)
    for d, (x, y) in zip(drops, ((-2.0, -30.0), (16.0, -36.0))):
        c.over(hexrgb("2F5E9E"), fill(d - 2.0))
        c.over(vgrad(c, "E3F3FF", "7FB8EC", y + 5.0, y - 5.0), fill(d))
    return "ICO_Act_TreasureFishing", c, RUNTIME_ICONS


def icon_act_lily_hop():
    """ICO_Act_LilyHop: a round lily pad with its notch on blue water, ripples around it, a cream paw print where
    someone just landed, a smaller pad behind and a dotted hop arc between them. No frog, no flower."""
    c = Canvas(128, 128, cy=64.0)
    water = sd_ellipse(c, 56.0, 30.0, ox=0.0, oy=-18.0)
    px, py = -6.0, -20.0

    def pad(ox, oy, rx, ry, notch_deg):
        d = sd_ellipse(c, rx, ry, ox=ox, oy=oy)
        a = math.radians(notch_deg)
        wedge = sd_polygon(c, [(ox, oy), (ox + math.cos(a - 0.22) * rx * 1.4, oy + math.sin(a - 0.22) * ry * 1.4),
                               (ox + math.cos(a + 0.22) * rx * 1.4, oy + math.sin(a + 0.22) * ry * 1.4)])
        return np.maximum(d, -(wedge - 1.5))
    big = pad(px, py, 36.0, 20.0, 60.0)
    small = pad(30.0, 20.0, 17.0, 9.5, 120.0)
    drop_shadow(c, water - WIDTH, dy=4.0, radius=3.0, strength=0.22)
    edge(c, water, "3E6E9E")
    c.over(vgrad(c, "CFEAFB", "86BFEA", 12.0, -48.0), fill(water))
    for rr in (44.0, 40.0):
        ring = np.abs(sd_ellipse(c, rr, rr * 0.56, ox=px, oy=py - 1.0)) - 1.2
        c.over(hexrgb("F2FAFF"), fill(ring) * fill(water) * (0.9 if rr > 42 else 0.55))
    c.over(hexrgb("F2FAFF"), fill(np.abs(sd_ellipse(c, 24.0, 12.0, ox=30.0, oy=19.0)) - 1.1) * fill(water) * 0.8)
    for p, ox, oy, rx, ry in ((small, 30.0, 20.0, 17.0, 9.5), (big, px, py, 36.0, 20.0)):
        c.over(hexrgb("2F6B3A"), blur(shift(fill(p), 0, 2.0), 1.5) * fill(water) * 0.35)
        edge(c, p, "3F7A33", 3.5 if rx < 20 else WIDTH)
        c.over(vgrad(c, "B4E38A", "5E9E4A", oy + ry, oy - ry), fill(p))
        for k in range(6):
            a = math.radians(-100.0 + 50.0 * k)
            tip = (ox + math.cos(a) * rx * 0.78, oy + math.sin(a) * ry * 0.78)
            vein = sd_polyline(c, [(ox, oy), tip], 0.9 if rx > 20 else 0.7)
            c.over(hexrgb("6FAE52"), fill(vein) * fill(p) * 0.8)
        lower_shade(c, p, "4F8A3F", oy - 2.0, ry, width=4.0, strength=0.5)
        top_rim(c, p, oy + 2.0, ry, 0.5)
    pv = View(c, px - 4.0, py - 1.0, -8.0, 1.0, 0.58)
    c.over(hexrgb("FFF4D6"), fill(sd_paw(pv, 0.78, 0.0, 0.0)) * fill(big))
    for k, t in enumerate((0.18, 0.38, 0.58, 0.78)):
        x = 26.0 + (px + 8.0 - 26.0) * t
        y = 24.0 + (py + 10.0 - 24.0) * t + 34.0 * t * (1.0 - t) * 1.8
        dot = sd_circle(c, 2.2 + 0.5 * k, x, y)
        c.over(hexrgb("C9782A"), fill(dot - 1.3))
        c.over(hexrgb("FFE48A"), fill(dot))
    return "ICO_Act_LilyHop", c, RUNTIME_ICONS


def icon_act_bakery():
    """ICO_Act_Bakery: a round layer cake on a little plate, cream icing dripping over the side, a pink jam layer and a
    glossy cherry on top."""
    c = Canvas(128, 128, cy=60.0)
    a, b = 36.0, 11.0
    top, bottom = 6.0, -34.0
    plate = sd_ellipse(c, 48.0, 12.0, ox=0.0, oy=bottom - 3.0)
    cake = sd_cylinder(c, a, b, top, bottom)
    cx, cy = 3.0, top + 17.0
    cherry = sd_circle(c, 9.5, cx, cy)
    stem = sd_polyline(c, [(cx + 1.0, cy + 7.0), (cx + 4.0, cy + 17.0), (cx + 10.0, cy + 23.0)], 1.6)
    drop_shadow(c, union(plate, cake, cherry, stem) - WIDTH)
    edge(c, plate, "6E7FB0")
    c.over(vgrad(c, "FFFFFF", "D6E2F0", bottom + 9.0, bottom - 15.0), fill(plate))
    c.over(hexrgb("BFD0E6"), fill(np.abs(sd_ellipse(c, 38.0, 8.0, ox=0.0, oy=bottom - 1.5)) - 1.0) * fill(plate) * 0.8)
    edge(c, cake, "8A5A2E")
    inside = fill(cake)
    c.over(side_shade(c, "F8D498", "D59650", a), inside)
    jam = np.maximum(sd_band(c, a, b, -20.0, -13.0), cake)
    c.over(side_shade(c, "FF9FB8", "D9638A", a), fill(jam))
    c.over(hexrgb("C24F72"), fill(np.abs(c.y - front_curve(c, a, b, -13.0)) - 0.8) * inside * 0.6)
    lower_shade(c, cake, "B98652", bottom + 6.0, 10.0, width=5.0, strength=0.5)
    # Icing: the top and a skirt of round drips over the side.
    skirt_y = front_curve(c, a, b, top - 7.0)
    drips = np.full(c.x.shape, 1e3)
    for dx, depth in ((-28.0, 6.0), (-16.0, 10.0), (-3.0, 7.0), (10.0, 12.0), (23.0, 7.0), (31.0, 5.0)):
        ystart = top - 7.0 - b * math.sqrt(max(0.0, 1.0 - (dx / a) ** 2))
        drips = np.minimum(drips, sd_polyline(c, [(dx, ystart), (dx, ystart - depth)], 3.6))
    icing = np.minimum(np.maximum(normalize(skirt_y - c.y), cake), np.maximum(drips, cake))
    icing = np.minimum(icing, sd_ellipse(c, a, b, oy=top))
    c.over(side_shade(c, "FFFFFF", "F4D4DE", a), fill(icing) * inside)
    c.over(vgrad(c, "FFFFFF", "FBE6EC", top + b, top - b), fill(sd_ellipse(c, a - 0.8, b - 0.8, oy=top)))
    c.over(hexrgb("E8B8C6"), fill(np.abs(icing) - 0.7) * inside * np.clip((top - c.y) / 4.0, 0, 1) * 0.8)
    for k, (dx, dy) in enumerate(((-20.0, 7.0), (18.0, 5.0), (-6.0, 1.0), (24.0, 9.0), (-26.0, 4.0))):
        col = ("F28FA8", "8EC8F2", "FFE08A", "A6DB7A", "C9B4EE")[k]
        c.over(hexrgb(col), fill(sd_round_rect(View(c, dx, top + dy - 2.0, 30.0 * (k - 2)), 2.6, 1.1, 1.1)))
    edge(c, union(cherry, stem), "8E2A36", 3.5)
    c.over(hexrgb("6F8A3A"), fill(stem))
    c.over(vgrad(c, "FF8A8A", "D83A4A", cy + 9.0, cy - 9.0), fill(cherry))
    lower_shade(c, cherry, "B02A3A", cy - 2.0, 8.0, width=3.0, strength=0.5)
    c.over((255, 255, 255), fill(sd_ellipse(c, 3.2, 2.0, ox=cx - 3.5, oy=cy + 3.5, angle_deg=35.0)) * 0.9)
    gloss(c, 3.6, 9.0, -a * 0.68, bottom + 14.0, 0.0, inside, 0.35, edge=2.0)
    return "ICO_Act_Bakery", c, RUNTIME_ICONS


def icon_act_photo_safari():
    """ICO_Act_PhotoSafari: a plush toy camera, sky blue with a cream band, a big round lens with a deep blue glass and
    two glosses, a coral shutter button and a small flash window."""
    c = Canvas(128, 128, cy=62.0)
    body = sd_round_rect(c, 48.0, 31.0, 15.0, oy=-8.0)
    bump = sd_round_rect(c, 16.0, 10.0, 6.0, ox=-20.0, oy=24.0)
    button = sd_round_rect(c, 8.0, 5.0, 3.5, ox=27.0, oy=25.0)
    camera = union(body, bump)
    drop_shadow(c, union(camera, button) - WIDTH)
    edge(c, button, "9C3E3A")
    c.over(vgrad(c, "FF9F8A", "E0654F", 30.0, 20.0), fill(button))
    edge(c, camera, "2E4E7E")
    c.over(vgrad(c, "B8DDF8", "5E95D2", 34.0, -40.0), fill(camera))
    band = np.maximum(sd_round_rect(c, 48.0, 31.0, 15.0, oy=-8.0), np.abs(c.y + 14.0) - 10.0)
    c.over(hexrgb("2E4E7E"), fill(band - 1.4) * fill(body) * 0.5)
    c.over(vgrad(c, "FFF8EA", "EAD6B4", -4.0, -24.0), fill(band))
    for k in range(9):
        x = -42.0 + k * 10.5
        c.over(hexrgb("D9C29A"), fill(sd_circle(c, 1.4, x, -14.0)) * fill(band) * 0.9)
    lower_shade(c, body, "4F82C0", -24.0, 16.0, width=6.0, strength=0.45)
    top_rim(c, camera, 4.0, 22.0, 0.6)
    flash = sd_round_rect(c, 7.5, 5.0, 3.0, ox=26.0, oy=9.0)
    c.over(hexrgb("2E4E7E"), fill(flash - 1.6))
    c.over(vgrad(c, "FFFBE0", "FFE08A", 14.0, 4.0), fill(flash))
    c.over(hexrgb("2E4E7E"), fill(sd_round_rect(c, 9.0, 4.5, 3.0, ox=-20.0, oy=26.0)))
    c.over(hexrgb("8FB8E0"), fill(sd_round_rect(c, 6.0, 2.0, 1.5, ox=-21.0, oy=27.0)) * 0.8)
    lx, ly = -2.0, -9.0
    ring = sd_circle(c, 25.0, lx, ly)
    c.over(hexrgb("2E4E7E"), fill(ring - 2.6))
    c.over(vgrad(c, "FBFAF6", "C9C2B4", ly + 25.0, ly - 25.0), fill(ring))
    glass = sd_circle(c, 17.0, lx, ly)
    c.over(hexrgb("2E4E7E"), fill(glass - 1.6))
    c.over(vgrad(c, "6F8FCF", "263A6E", ly + 17.0, ly - 17.0), fill(glass))
    c.over(hexrgb("9CC0F0"), fill(np.abs(sd_circle(c, 10.5, lx, ly)) - 1.1) * 0.6)
    c.over((255, 255, 255), fill(sd_ellipse(c, 6.5, 3.6, ox=lx - 6.0, oy=ly + 7.0, angle_deg=35.0)) * 0.9)
    c.over((255, 255, 255), fill(sd_circle(c, 2.0, lx + 7.0, ly - 6.0)) * 0.75)
    gloss(c, 3.6, 7.0, -40.0, 0.0, -6.0, fill(body), 0.45, edge=1.5)
    return "ICO_Act_PhotoSafari", c, RUNTIME_ICONS


def icon_act_cuddle_match():
    """ICO_Act_CuddleMatch: three matching round rose tiles with cream hearts in a gentle arc, the middle one lifted as
    it pops, three round dots above it."""
    c = Canvas(128, 128, cy=64.0)
    tiles = ((-40.0, -8.0, 18.0), (0.0, 1.0, 22.0), (40.0, -8.0, 18.0))
    shapes = [sd_circle(c, r, x, y) for x, y, r in tiles]
    drop_shadow(c, union(*shapes) - WIDTH)
    for (x, y, r), tile in zip(tiles, shapes):
        edge(c, tile, "A83E5E")
        c.over(vgrad(c, "FFC0D2", "E8708F", y + r, y - r), fill(tile))
        lower_shade(c, tile, "D45A80", y - 2.0, r, width=4.5, strength=0.55)
        top_rim(c, tile, y + 4.0, r, 0.55)
        heart = sd_heart(c, r * 1.12, x, y - 0.5)
        c.over(hexrgb("D9658A"), fill(heart - 1.6) * 0.5)
        c.over(vgrad(c, "FFFFFF", "FFE6EC", y + r * 0.5, y - r * 0.5), fill(heart))
        shine = sd_ellipse(c, r * 0.24, r * 0.13, ox=x - r * 0.48, oy=y + r * 0.5, angle_deg=40.0)
        c.over((255, 255, 255), fill(shine) * 0.85)
    for dx, dy, rr in ((-15.0, 34.0, 3.6), (0.0, 41.0, 4.4), (15.0, 34.0, 3.6)):
        dot = sd_circle(c, rr, dx, dy)
        c.over(hexrgb("C9782A"), fill(dot - 1.5))
        c.over(hexrgb("FFE48A"), fill(dot))
    return "ICO_Act_CuddleMatch", c, RUNTIME_ICONS


def icon_act_slide():
    """ICO_Act_Slide: a round winter window (sky blue, never a dark scene): a snowy slope going down to the right, a
    softer hill behind, a little wooden sled with red slats and a curled runner sliding down, round snow puffs behind
    it."""
    c = Canvas(128, 128, cy=64.0)
    disc = sd_circle(c, 47.0)
    sticker(c, disc, "3E6E9E", WIDTH)
    inside = fill(disc)
    c.over(vgrad(c, "B4DCF7", "86BEEA", 47.0, -10.0), inside)
    back_hill = sd_ellipse(c, 44.0, 24.0, ox=30.0, oy=-6.0)
    c.over(hexrgb("B9D4EE"), fill(back_hill - 1.4) * inside)
    c.over(vgrad(c, "F4FAFF", "DCEAF8", 18.0, -20.0), fill(back_hill) * inside)

    def profile(x):
        t = np.clip((x + 52.0) / 104.0, 0.0, 1.0)
        return 22.0 - 48.0 * (0.35 * t + 0.65 * t * t * (3.0 - 2.0 * t))
    surface = profile(c.x)
    snow = normalize(c.y - surface)
    c.over(hexrgb("8FB0D8"), fill(snow - 1.6) * inside)
    c.over(vgrad(c, "FFFFFF", "D2E2F4", 18.0, -46.0), fill(snow) * inside)
    c.over(hexrgb("A9BEDF"), soft(-disc - 8.0, 8.0) * np.clip((-20.0 - c.y) / 20.0, 0, 1) * fill(snow) * inside * 0.5)
    sx = 2.0
    sy = float(profile(np.array(sx)))
    slope = float(profile(np.array(sx + 0.5)) - profile(np.array(sx - 0.5)))
    ang = math.degrees(math.atan(slope))
    on_track = np.clip((c.x + 44.0) / 8.0, 0, 1) * np.clip((sx - 30.0 - c.x) / 4.0, 0, 1)
    c.over(hexrgb("A9BEDF"), fill(np.abs(c.y - (surface - 4.5)) - 0.9) * on_track * inside * 0.7)
    v = View(c, sx, sy, ang)
    k = 1.7
    runner = np.minimum(sd_polyline(v, [(-19.0 * k, 2.2 * k), (13.0 * k, 2.2 * k)], 1.9 * k),
                        sd_arc(v, 6.0 * k, -90.0, 125.0, 1.9 * k, ox=13.0 * k, oy=8.2 * k))
    for px in (-11.0, 4.0):
        runner = np.minimum(runner, sd_polyline(v, [(px * k, 2.2 * k), (px * k, 8.0 * k)], 1.6 * k))
    seat = sd_round_rect(v, 16.0 * k, 2.8 * k, 2.0 * k, ox=-4.0 * k, oy=10.0 * k)
    sled = np.minimum(runner, seat)
    pv = rotate_points([(-36.0, 12.0), (-44.0, 20.0), (-33.0, 24.0)], ang, sx, sy)
    puffs = [(pv[0][0], pv[0][1], 5.4), (pv[1][0], pv[1][1], 3.8), (pv[2][0], pv[2][1], 2.8)]
    puff = union(*[sd_circle(c, rr, x, y) for x, y, rr in puffs])
    c.over(SHADOW_BLUE, blur(shift(fill(sled - 3.0), 0, 2.0), 1.5) * inside * 0.3)
    edge(c, sled, "6B3A2A", 3.0)
    c.over(vgrad(c, "C48B52", "8E5E34", sy + 14.0, sy), fill(runner))
    c.over(lerp(hexrgb("FF9A86"), hexrgb("D9584A"), np.clip((13.5 * k - v.y) / (6.0 * k), 0, 1)), fill(seat))
    for sx2 in (-12.0, -4.0, 4.0):
        c.over(hexrgb("B8463A"), fill(np.abs(v.x - sx2 * k) - 0.6) * fill(seat) * 0.6)
    c.over(hexrgb("FFE0D6"), fill(np.abs(v.y - 12.0 * k) - 0.7) * fill(seat) * 0.7)
    c.over(hexrgb("8FB0D8"), fill(puff - 1.8) * inside)
    c.over(vgrad(c, "FFFFFF", "E3EEF9", sy + 30.0, sy + 8.0), fill(puff) * inside)
    for x, y, rr in ((-32.0, 34.0, 2.0), (22.0, 30.0, 1.6), (-8.0, 40.0, 1.4), (34.0, 14.0, 1.8)):
        c.over((255, 255, 255), fill(sd_circle(c, rr, x, y)) * inside * 0.95)
    gloss(c, 9.0, 4.5, -24.0, 30.0, 42.0, inside, 0.35, edge=2.0)
    return "ICO_Act_Slide", c, RUNTIME_ICONS


def icon_act_constellations():
    """ICO_Act_Constellations: a round night sky window (blue, never black), round five-point stars and round dots
    joined by soft lines into a small shape. No moon."""
    c = Canvas(128, 128, cy=64.0)
    sky = sd_circle(c, 47.0, 0.0, 0.0)
    sticker(c, sky, "2A3466", WIDTH)
    inside = fill(sky)
    c.over(vgrad(c, "5C70B4", "323F7A", 47.0, -47.0), inside)
    for x, y, r in ((-30.0, -16.0, 1.4), (28.0, 28.0, 1.2), (-8.0, 34.0, 1.3), (34.0, -10.0, 1.2), (-36.0, 12.0, 1.0),
                    (6.0, -36.0, 1.3)):
        c.over(hexrgb("A8B8EE"), fill(sd_circle(c, r, x, y)) * 0.9)
    nodes = [(-26.0, 18.0, "star", 11.0), (-6.0, 2.0, "dot", 4.6), (18.0, 14.0, "star", 9.5), (22.0, -16.0, "dot", 4.4),
             (-14.0, -22.0, "dot", 4.0)]
    links = ((0, 1), (1, 2), (2, 3), (1, 4), (4, 3))
    for i, j in links:
        a, b = nodes[i], nodes[j]
        c.over(hexrgb("FFF0C8"), fill(sd_polyline(c, [(a[0], a[1]), (b[0], b[1])], 1.3)) * inside * 0.75)
    for x, y, kind, r in nodes:
        if kind == "star":
            c.over(hexrgb("FFF3B0"), soft(sd_circle(c, r * 0.6, x, y), r * 0.9) * 0.35)
            gold_star(c, r, r * 0.5, x, y, rounding=1.6, edge_width=1.8)
        else:
            dot = sd_circle(c, r, x, y)
            c.over(hexrgb("FFF3B0"), soft(dot, r * 1.2) * 0.3)
            c.over(hexrgb("C9782A"), fill(dot - 1.5))
            c.over(vgrad(c, "FFF8D0", "FFD36A", y + r, y - r), fill(dot))
    top_rim(c, sky, 10.0, 36.0, 0.35, color=hexrgb("A8B8EE"))
    gloss(c, 9.0, 4.5, -24.0, 30.0, 42.0, inside, 0.3, edge=2.0)
    return "ICO_Act_Constellations", c, RUNTIME_ICONS


def icon_act_firefly_rhythm():
    """ICO_Act_FireflyRhythm: a soft lilac wave line, three round glowing fireflies riding it on the beat, each a
    glowing body with a small brown head and two pale wings lifted behind it."""
    c = Canvas(128, 128, cy=62.0)

    def wave(x):
        return 12.0 * np.sin((x + 8.0) * math.tau / 66.0) - 11.0
    xs = np.linspace(-54.0, 54.0, 55)
    line = sd_polyline(c, [(float(x), float(wave(x))) for x in xs], 4.2)
    flies = [(-37.0, 11.0), (0.0, 15.0), (36.0, 10.5)]
    bodies, wings = [], []
    for x, r in flies:
        y = float(wave(x))
        bodies.append(sd_circle(c, r, x, y))
        for side in (-1.0, 1.0):
            wing_x, wing_y = x + side * r * 0.85, y + r * 0.7
            wings.append(sd_ellipse(c, r * 0.95, r * 0.5, ox=wing_x, oy=wing_y, angle_deg=side * 52.0))
    wing = union(*wings)
    drop_shadow(c, union(line, wing, *bodies) - WIDTH, strength=0.24)
    edge(c, line, "6A55A8")
    c.over(vgrad(c, "DCCFF7", "A48ADF", 6.0, -22.0), fill(line))
    shine = sd_polyline(c, [(float(x), float(wave(x)) + 1.5) for x in xs], 0.9)
    c.over((255, 255, 255), fill(shine) * fill(line) * 0.55)
    edge(c, wing, "6E7FB0", 2.4)
    c.over(vgrad(c, "FFFFFF", "D6E6F7", 34.0, -6.0), fill(wing))
    for x, r in flies:
        y = float(wave(x))
        for side in (-1.0, 1.0):
            vein = sd_ellipse(c, r * 0.5, r * 0.18, ox=x + side * r * 0.95, oy=y + r * 0.85, angle_deg=side * 52.0)
            c.over(hexrgb("C6D8EE"), fill(vein) * 0.8)
    for (x, r), body in zip(flies, bodies):
        y = float(wave(x))
        c.over(hexrgb("FFF6A0"), soft(body, r * 0.8) * 0.6)
        edge(c, body, "8A7A22", 3.5)
        c.over(vgrad(c, "FFFDE0", "D6E25A", y + r, y - r), fill(body))
        c.over(hexrgb("FFFFF2"), soft(sd_circle(c, r * 0.35, x - r * 0.15, y + r * 0.15), r * 0.4) * fill(body) * 0.85)
        lower_shade(c, body, "B8C83E", y - 1.0, r, width=r * 0.3, strength=0.45)
        shine = sd_ellipse(c, r * 0.28, r * 0.16, ox=x - r * 0.4, oy=y + r * 0.42, angle_deg=40.0)
        c.over((255, 255, 255), fill(shine) * 0.85)
        head = np.maximum(sd_circle(c, r * 0.5, x, y + r * 1.02), -(body + 1.0))
        c.over(hexrgb("4E3B2C"), fill(head - 2.2))
        c.over(vgrad(c, "9A7A5E", "5E4A3A", y + r * 1.5, y + r * 0.6), fill(head))
        c.over((255, 255, 255), fill(sd_circle(c, r * 0.1, x - r * 0.17, y + r * 1.22)) * 0.6)
    return "ICO_Act_FireflyRhythm", c, RUNTIME_ICONS


def icon_act_bubbles():
    """ICO_Act_Bubbles: four soft round bubbles of different sizes and pastel colors, each with a deeper rim, a light
    upper edge, an oval gloss and a small round one."""
    c = Canvas(128, 128, cy=64.0)
    bubbles = ((-16.0, -14.0, 29.0, "D9EEFF", "8EC4F0", "3E6E9E"), (26.0, 14.0, 19.0, "FFE0EA", "F29BB6", "B4495A"),
               (-12.0, 34.0, 11.0, "E0F7EC", "8ED6B4", "3E8A6A"), (36.0, -30.0, 9.0, "EEE6FF", "BBA6EC", "6A55A8"))
    shapes = [sd_circle(c, r, x, y) for x, y, r, *_ in bubbles]
    drop_shadow(c, union(*shapes) - WIDTH, strength=0.22)
    for (x, y, r, light, deep, line), bubble in zip(bubbles, shapes):
        w = WIDTH if r > 15 else 3.5
        edge(c, bubble, line, w)
        inside = fill(bubble)
        c.over(vgrad(c, light, deep, y + r, y - r), inside)
        c.over(hexrgb(deep), soft(-bubble - r * 0.22, r * 0.22) * inside * 0.7)
        c.over((255, 255, 255), soft(-bubble - r * 0.12, r * 0.1) * np.clip((c.y - y) / r + 0.2, 0, 1) * inside * 0.55)
        shine = sd_ellipse(c, r * 0.3, r * 0.17, ox=x - r * 0.42, oy=y + r * 0.42, angle_deg=42.0)
        c.over((255, 255, 255), fill(shine) * 0.9)
        c.over((255, 255, 255), fill(sd_circle(c, r * 0.09, x - r * 0.12, y + r * 0.66)) * 0.8)
    return "ICO_Act_Bubbles", c, RUNTIME_ICONS


def icon_act_tile_puzzle():
    """ICO_Act_TilePuzzle: a wooden tray of 3 x 3 cream tiles carrying one rose heart picture, the lower right tile
    missing and its neighbour sliding toward the gap."""
    c = Canvas(128, 128, cy=64.0)
    tray = sd_round_rect(c, 50.0, 50.0, 15.0, oy=-1.0)
    sticker(c, tray, "5E3F24", WIDTH)
    c.over(vgrad(c, "E3B37A", "B07C46", 49.0, -51.0), fill(tray))
    well = sd_round_rect(c, 44.0, 44.0, 11.0, oy=-1.0)
    c.over(hexrgb("7A4E2A"), fill(well))
    c.over(hexrgb("5E3F24"), soft(-well - 4.0, 4.0) * np.clip((c.y + 1.0) / 30.0, 0, 1) * fill(well) * 0.8)
    top_rim(c, tray, 0.0, 40.0, 0.5)
    step, half = 29.0, 13.2
    heart_full = sd_heart(c, 74.0, 0.0, 0.0)
    for row in range(3):
        for col in range(3):
            if row == 2 and col == 2:
                continue
            slide = 6.0 if (row == 2 and col == 1) else 0.0
            x, y = -step + col * step + slide, step - row * step - 1.0
            tile = sd_round_rect(c, half, half, 5.0, ox=x, oy=y)
            c.over(hexrgb("5E3F24"), blur(shift(fill(tile), 0, 1.5), 1.0) * 0.4)
            c.over(vgrad(c, "FFFBF2", "F0DEC0", y + half, y - half), fill(tile))
            heart = sd_heart(c, 74.0, slide, 0.0) if slide else heart_full
            c.over(vgrad(c, "FFA3BE", "EC6E8C", y + half, y - half), fill(heart) * fill(tile))
            c.over(hexrgb("D9BE96"), fill(np.abs(c.y - (y - half + 1.6)) - 1.0) * fill(tile) * 0.6)
            c.over((255, 255, 255), soft(-tile - 1.5, 1.2) * fill(tile) * np.clip((c.y - y) / half, 0, 1) * 0.7)
    c.over((255, 255, 255), fill(sd_ellipse(c, 6.0, 3.4, ox=-9.0, oy=15.0, angle_deg=40.0)) * 0.7)
    return "ICO_Act_TilePuzzle", c, RUNTIME_ICONS


def icon_act_burrow_maze():
    """ICO_Act_BurrowMaze: a round grassy mound with a warm dark burrow entrance at its foot, a winding sandy path with
    round seeds leading from its top down into it."""
    c = Canvas(128, 128, cy=62.0)
    mound = np.maximum(sd_ellipse(c, 56.0, 50.0, ox=0.0, oy=-26.0), sd_round_rect(c, 60.0, 40.0, 12.0, oy=-4.0))
    sticker(c, mound, "5E3F24", WIDTH)
    inside = fill(mound)
    grass_edge = -16.0 + 3.0 * np.sin(c.x * 0.2 + 0.6)
    c.over(vgrad(c, "D9A266", "9A6234", -12.0, -46.0), inside)
    above = fill(grass_edge - c.y)
    c.over(vgrad(c, "B4E38A", "6FAE52", 24.0, -18.0), inside * above)
    c.over(hexrgb("4F8A3F"), fill(np.abs(c.y - grass_edge) - 1.2) * inside * 0.8)
    for x, y in ((-40.0, -28.0), (-26.0, -38.0), (34.0, -32.0), (46.0, -22.0), (-46.0, -40.0)):
        c.over(hexrgb("B98652"), fill(sd_ellipse(c, 3.2, 2.0, ox=x, oy=y)) * inside * 0.8)
    for x, y in ((-34.0, 4.0), (36.0, 2.0), (22.0, 16.0)):
        tuft = sd_polyline(c, [(x - 3.0, y + 3.0), (x, y - 1.0), (x + 3.0, y + 3.0)], 1.0)
        c.over(hexrgb("5E9E4A"), fill(tuft) * inside * 0.7)
    top_rim(c, mound, 0.0, 22.0, 0.55)
    hx, hy = 12.0, -31.0
    hole = np.maximum(sd_ellipse(c, 15.0, 13.0, ox=hx, oy=hy), -(c.y - (hy - 12.0)))
    path = [(-30.0, 18.0), (-14.0, 21.0), (-4.0, 12.0), (-16.0, 2.0), (-12.0, -8.0), (2.0, -12.0),
            (hx - 2.0, hy + 10.0)]
    road = sd_polyline(c, path, 5.2)
    c.over(hexrgb("C49A62"), fill(road - 1.6) * inside)
    c.over(vgrad(c, "FFF4D8", "F0D6A4", 24.0, -24.0), fill(road) * inside)
    c.over(hexrgb("7A4E2A"), fill(hole - 3.0))
    c.over(vgrad(c, "6B4528", "3E2A18", hy + 13.0, hy - 12.0), fill(hole))
    c.over(hexrgb("8E5E34"), fill(np.abs(sd_ellipse(c, 11.0, 9.0, ox=hx, oy=hy - 2.0)) - 0.8) * fill(hole) * 0.6)
    for k in range(len(path) - 2):
        x = (path[k][0] + path[k + 1][0]) * 0.5
        y = (path[k][1] + path[k + 1][1]) * 0.5
        seed = sd_ellipse(c, 2.4, 3.3, ox=x, oy=y, angle_deg=90.0 + 30.0 * (k % 3 - 1))
        c.over(hexrgb("6B4528"), fill(seed - 0.8))
        c.over(hexrgb("B07C46"), fill(seed))
    gloss(c, 8.0, 3.6, -30.0, 30.0, 30.0, inside, 0.45, edge=1.5)
    return "ICO_Act_BurrowMaze", c, RUNTIME_ICONS


def icon_act_grand_contest():
    """ICO_Act_GrandContest: a round two-tier lilac podium, a paw print on its top where the animal stands, a golden
    rosette with a heart and two sky blue tails pinned on its front."""
    c = Canvas(128, 128, cy=60.0)
    low = sd_cylinder(c, 50.0, 12.0, -18.0, -42.0)
    high = sd_cylinder(c, 33.0, 9.0, 6.0, -18.0)
    podium = np.minimum(low, high)
    rx, ry = 0.0, -14.0
    tail_l = ribbon_tail(c, (-3.0, ry - 4.0), (-14.0, -50.0), 6.5, 5.0)
    tail_r = ribbon_tail(c, (3.0, ry - 4.0), (14.0, -50.0), 6.5, 5.0)
    rosette = scalloped(c, 17.0, 12, 5.0, rx, ry)
    drop_shadow(c, union(podium, tail_l, tail_r, rosette) - WIDTH)
    edge(c, podium, "4A3E86")
    c.over(side_shade(c, "E6DEFA", "A898DE", 50.0), fill(low))
    low_top = sd_ellipse(c, 50.0 - 0.8, 12.0 - 0.8, oy=-18.0)
    c.over(vgrad(c, "F6F2FF", "DCD2F6", -18.0 + 12.0, -18.0 - 12.0), fill(low_top))
    c.over(hexrgb("4A3E86"), fill(np.abs(high) - 1.6) * fill(low) * 0.9 * np.clip((-12.0 - c.y) * 0.5, 0, 1))
    c.over(side_shade(c, "EEE8FF", "B2A3E6", 33.0), fill(high))
    top = sd_ellipse(c, 33.0 - 0.8, 9.0 - 0.8, oy=6.0)
    c.over(vgrad(c, "FFFFFF", "E6DEFA", 15.0, -3.0), fill(top))
    pv = View(c, 0.0, 6.0, 0.0, 1.0, 0.42)
    c.over(hexrgb("B6A6E8"), fill(sd_paw(pv, 0.62, 0.0, 0.0)) * fill(top))
    lower_shade(c, low, "8C7ACC", -36.0, 8.0, width=5.0, strength=0.5)
    for tail, top_c, bottom_c in ((tail_l, "8DBDE8", "4C86C6"), (tail_r, "A9D3F5", "5E95D2")):
        edge(c, tail, "2F5E9E", 3.5)
        c.over(vgrad(c, top_c, bottom_c, ry - 6.0, -50.0), fill(tail))
    edge(c, rosette, "A8662A", 3.5)
    c.over(vgrad(c, "FFE48A", "F2A93B", ry + 22.0, ry - 22.0), fill(rosette))
    ang = np.arctan2(c.y - ry, c.x - rx)
    dist = np.hypot(c.x - rx, c.y - ry)
    pleats = (0.5 + 0.5 * np.cos(ang * 12.0)) * np.clip((dist - 12.0) / 4.0, 0, 1)
    c.over(hexrgb("E0892C"), fill(rosette) * pleats * 0.35)
    face = sd_circle(c, 11.0, rx, ry)
    c.over(hexrgb("A8662A"), fill(face - 1.6))
    c.over(vgrad(c, "FFFBF2", "F6E4CC", ry + 11.0, ry - 11.0), fill(face))
    heart = sd_heart(c, 13.0, rx, ry - 0.5)
    c.over(vgrad(c, "FFA3BE", "EC6E8C", ry + 6.0, ry - 6.0), fill(heart))
    gloss(c, 3.0, 8.0, -38.0, -30.0, 0.0, fill(low), 0.35, edge=1.5)
    return "ICO_Act_GrandContest", c, RUNTIME_ICONS


T37_FRAMEWORK = (icon_play, icon_practice, icon_challenge, icon_record, icon_timer, icon_lock, icon_pause,
                 kit_star_result_empty, kit_star_result_full)
T37_ACTIVITIES = (icon_act_grooming, icon_act_hide_and_seek, icon_act_nut_rain, icon_act_paw_memory,
                  icon_act_expeditions, icon_act_garden, icon_act_treasure_fishing, icon_act_lily_hop, icon_act_bakery,
                  icon_act_photo_safari, icon_act_cuddle_match, icon_act_slide, icon_act_constellations,
                  icon_act_firefly_rhythm, icon_act_bubbles, icon_act_tile_puzzle, icon_act_burrow_maze,
                  icon_act_grand_contest)
T37_SPRITES = T37_FRAMEWORK + T37_ACTIVITIES


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_KIT, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T37_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
