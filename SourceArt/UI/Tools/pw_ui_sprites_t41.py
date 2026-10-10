"""Pocket Wilds UI sprites added in T41 (Memory des empreintes, the fourth activity on the T37 framework and the first one
that is an interface screen only; doc 01 Contenu, S-ACT-004, « Thèmes par biome »: Prairie, a four-leaf clover with
round leaves on the card backs, a cream tablecloth behind; H-VFX-ACT-004-02, the glow card's gold edge).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Owner rules: round dots,
five-point stars, bubbles, hearts and paws only; the clover's four leaves are round, wider toward their outer end, kept
apart by a clear line of cream (a lucky clover, never a four-point star: no leaf has a point outward); nothing religious; nothing enters anything (the leaves,
the pads of a pawprint and the seeds of the cookie stand clear of one another).

Memory screen (Art/UI/Activities/Memory, built into PF_UI_Memory by the screen's pipeline). The three card sprites share
one canvas, 256 x 340, and one card body centered in it: 232 x 312 (about 3:4), corner radius 22, a 12 px margin on the
sides and 14 px above and below for the baked shadow and the glow. So a card back, a card face and the glow edge laid on
the same rectangle line up to the pixel, and a portrait or a pawprint centered on the card sits in the face's frame.
- UI_Memory_CardBack_Prairie: soft green border, a cream panel with a hem of round green dots, the round four-leaf clover
  in the middle (a little turned, a short curved stem) in a ring of round dots.
- UI_Memory_CardFace: the plain cream face, a soft warm outline and a soft inner border (inside it: 208 x 288).
- UI_Memory_GlowEdge: a soft gold edge along the card body, glowing outward 11 px and inward 9 px (alpha only where it
  glows): the glow card's gold edge, and its pair's glow when the stage pulses its alpha for 1 s.
- UI_Memory_DoubleBadge: 128 x 128, a round honey gold badge with « ×2 » drawn as rounded strokes in the style of Pocket
  Round (no font in the sprite), for a corner of the double pair's cards.
- UI_Memory_Tablecloth: 256 x 256, opaque, tiles seamlessly (a period of 64 px divides the tile): a very soft cream and
  sage gingham with a faint weave, the background of the screen (Image type Tiled).
- UI_Memory_Glint: 64 x 64, WHITE (tinted by the stage), one soft round glint of the match (H-VFX-ACT-004-01, « 2 éclats
  ronds »): a round core with a soft edge in a gentle round glow, never a four-point shape.

Pawprints (Art/UI/Activities/Memory with the screen's sprites, so the scene's HUD atlas never packs them; 128 x 128,
WHITE with light grey shading and a mid grey rim, so the stage tints them with the variant's coat colour: the rim turns
into a deeper tone of the coat, like every outline of the family). One per species, round pads only, three silhouettes
told apart at 64 px:
- UI_Paw_Rabbit: a long oval heel pad (the hind foot) under four small round toes close together: tall and narrow.
- UI_Paw_Squirrel: a small palm of three round pads under four long slender toes spread wide like fingers.
- UI_Paw_Hedgehog: a broad bean-shaped palm under five small round toes in a tight arc: a little hand, wide and low.

Reward icon (own colors, Art/UI/Icons, on the result card beside ICO_Treat_CloverCrunch, readable at 48 px):
- ICO_Treat_SeedCookie: the Memory's 3-star treat (Biscuit aux graines, treat.seed_cookie), the round biscuit of
  ICO_Treat_CloverCrunch with a scalloped cookie-cutter edge, three plump seeds of ICO_Seed in a pinwheel and a scatter
  of small round cream and dark seeds.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t41.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (ROOT, Canvas, MASTERS, RUNTIME_ICONS, blur, fill, hexrgb, lerp, normalize, sd_circle,
                           sd_ellipse, sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t35a import drop_shadow
from pw_ui_sprites_t36 import WARM_OUTLINE, View, gloss, sd_arc, sticker, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, scalloped, sd_drop, top_rim, union

RUNTIME_MEMORY = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Activities", "Memory")

WIDTH = 4.5  # the family's outline on a 128 px icon

# ---------------------------------------------------------------- the card

CARD_W, CARD_H = 256, 340
BODY = (116.0, 156.0, 22.0)  # the card body: half width, half height, corner radius (centered in the canvas)
CARD_LINE = 3.0              # the card's own outline, inside the body
LEAF = ("B8E68A", "5E9E4A", "4F7A2E", "4F8A3F")  # top, bottom, outline, vein: the clover sprig's green (T36, T40)
BORDER = ("C4E4A0", "8EC36B", "6E9E52")          # the back's soft green border: top, bottom, outline
CREAM = ("FFF9EC", "F5E5C4")                      # the back's panel: top, bottom
PANEL_INSET = 15.0       # the panel inside the border
HEM_INSET = 11.0         # the hem of round dots inside the panel
HEM_SPACING = 12.5       # between two hem dots (along the path)
HEM_DOT = 2.3

CLOVER = (0.0, 12.0, -12.0)   # the clover's heart and turn (degrees)
LEAVES = ((24.0, 27.5), (23.4, 27.0), (24.6, 28.0), (23.8, 27.3))  # each leaf: half width, half length (a little uneven)
LEAF_D = 28.0            # each leaf's center from the heart, along its bisector
LEAF_TAPER = -0.42       # rounder and wider toward its outer end, narrow (but round) at the heart
LEAF_GAP = 8.6           # between two leaves: their two 3 px edges and a clear line of cream
STEM = ((-1.5, -6.0), (-9.0, -42.0), (-7.0, -58.0), (1.0, -70.0))  # the stem down the gap of the lower leaves, curving
RING = (82.0, 20, 2.6)   # the ring of round dots around the clover: radius, count, dot radius


def card_body(c):
    hw, hh, r = BODY
    return sd_round_rect(c, hw, hh, r)


def card_shadow(c, body):
    """The card's soft baked shadow, the same under the back and the face."""
    drop_shadow(c, body, dy=4.0, radius=3.5, strength=0.22)


def rounded_rect_path(hw, hh, r, spacing):
    """Points evenly spaced along a rounded rectangle (centered), starting at the middle of the top side."""
    sx, sy = hw - r, hh - r
    pieces = [("line", (0.0, hh), (sx, hh)), ("arc", (sx, sy), 90.0, 0.0), ("line", (hw, sy), (hw, -sy)),
              ("arc", (sx, -sy), 0.0, -90.0), ("line", (sx, -hh), (-sx, -hh)), ("arc", (-sx, -sy), -90.0, -180.0),
              ("line", (-hw, -sy), (-hw, sy)), ("arc", (-sx, sy), 180.0, 90.0), ("line", (-sx, hh), (0.0, hh))]
    lengths = [math.dist(p[1], p[2]) if p[0] == "line" else abs(p[3] - p[2]) / 180.0 * math.pi * r for p in pieces]
    total = sum(lengths)
    count = max(int(round(total / spacing)), 1)
    points = []
    for k in range(count):
        s = k * total / count
        for piece, length in zip(pieces, lengths):
            if s <= length + 1e-9:
                if piece[0] == "line":
                    (ax, ay), (bx, by) = piece[1], piece[2]
                    t = s / length if length > 0 else 0.0
                    points.append((ax + (bx - ax) * t, ay + (by - ay) * t))
                else:
                    (cx, cy), a0, a1 = piece[1], piece[2], piece[3]
                    a = math.radians(a0 + (a1 - a0) * (s / length))
                    points.append((cx + r * math.cos(a), cy + r * math.sin(a)))
                break
            s -= length
    return points


def clover_leaves(c):
    """The four round leaves around the clover's heart: each a round leaf, wider toward its outer end and narrow but
    round at the heart (a clover leaflet without its notch), kept to its own quarter with a clear gap to its neighbours.
    Returns (leaf distance, leaf center, direction, half length) per leaf."""
    ox, oy, turn = CLOVER
    leaves = []
    for k, (hw, hh) in enumerate(LEAVES):
        theta = math.radians(turn + 45.0 + 90.0 * k)
        cx, cy = ox + LEAF_D * math.cos(theta), oy + LEAF_D * math.sin(theta)
        leaf = sd_drop(View(c, cx, cy, math.degrees(theta) - 90.0), hw, hh, LEAF_TAPER)
        px, py = c.x - ox, c.y - oy
        a1, a2 = theta - math.pi / 4.0, theta + math.pi / 4.0
        s1 = math.cos(a1) * py - math.sin(a1) * px   # > 0 on the leaf's side of its first boundary
        s2 = math.sin(a2) * px - math.cos(a2) * py   # > 0 on the leaf's side of its second boundary
        wedge = np.maximum(LEAF_GAP * 0.5 - s1, LEAF_GAP * 0.5 - s2)
        leaf = -smooth_min(-leaf, -wedge, 4.0)
        leaves.append((leaf, (cx, cy), theta, hh))
    return leaves


def draw_clover(c):
    """The round four-leaf clover: its soft shadow on the cream, the stem, each leaf with its deeper edge, a gradient lit
    from the top left, a pale round band and a fine vein, a darker heart, one gloss."""
    ox, oy, _ = CLOVER
    leaves = clover_leaves(c)
    stem = sd_polyline(c, [(ox + x, oy + y) for x, y in STEM], 3.6)
    whole = union(stem, *[leaf for leaf, *_ in leaves])
    c.over((70, 90, 40), blur(shift(fill(whole - 2.5), 0, 3.0), 2.5) * 0.22)
    top, bottom, line, vein = LEAF
    edge(c, stem, line, width=2.6)
    c.over(vgrad(c, "9FD077", "5E9E4A", oy - 4.0, oy - 60.0), fill(stem))
    for leaf, (cx, cy), theta, length in leaves:
        edge(c, leaf, line, width=3.0)
    for leaf, (cx, cy), theta, length in leaves:
        inside = fill(leaf)
        light = np.clip(((c.y - cy) - (c.x - cx)) / (2.4 * length) + 0.5, 0, 1)
        c.over(lerp(hexrgb(bottom), hexrgb(top), light), inside)
        # The pale band of a clover leaf, made round: a soft arc across the leaf around the heart.
        band = sd_arc(c, LEAF_D + 1.0, math.degrees(theta) - 34.0, math.degrees(theta) + 34.0, 1.5, ox, oy)
        c.over(hexrgb("DDF3C2"), fill(band) * fill(leaf + 4.5) * 0.65)
        rib = sd_polyline(c, [(ox + 5.0 * math.cos(theta), oy + 5.0 * math.sin(theta)),
                              (ox + (LEAF_D + length * 0.45) * math.cos(theta), oy + (LEAF_D + length * 0.45) * math.sin(theta))], 0.9)
        c.over(hexrgb(vein), fill(rib) * inside * 0.55)
        c.over(hexrgb(vein), soft(-leaf - 4.0, 4.0) * np.clip((cy - c.y) / length, 0, 1) * inside * 0.35)
    c.over(hexrgb(line), fill(sd_circle(c, 3.6, ox, oy)) * 0.9)
    leaf, (cx, cy), theta, length = leaves[1]  # the top-left leaf catches the light
    gloss(c, 8.0, 4.2, cx - 6.0, cy + 7.0, 35.0, fill(leaf), 0.5, edge=1.5)


def ui_memory_card_back():
    """UI_Memory_CardBack_Prairie: the Prairie card back. A soft green border with its deeper outline and a light top rim,
    a cream panel with a hem of round green dots, the round four-leaf clover in the middle in a ring of round dots."""
    c = Canvas(CARD_W, CARD_H)
    body = card_body(c)
    card_shadow(c, body)
    top, bottom, line = BORDER
    c.over(hexrgb(line), fill(body))
    c.over(vgrad(c, top, bottom, BODY[1], -BODY[1]), fill(body + CARD_LINE))
    lower_shade(c, body + CARD_LINE, "7DB45C", -60.0, 90.0, width=6.0, strength=0.5)
    top_rim(c, body + CARD_LINE, 60.0, 90.0, 0.6, color=hexrgb("E6F5D2"))
    hw, hh, r = BODY
    panel = sd_round_rect(c, hw - PANEL_INSET, hh - PANEL_INSET, r - PANEL_INSET * 0.6)
    c.over(hexrgb("79A957"), fill(panel - 2.0) * 0.85)
    c.over(vgrad(c, CREAM[0], CREAM[1], hh - PANEL_INSET, -(hh - PANEL_INSET)), fill(panel))
    c.over(hexrgb("E3CFA6"), soft(-panel - 5.0, 5.0) * np.clip(-c.y / 120.0, 0, 1) * fill(panel) * 0.45)
    for x, y in rounded_rect_path(hw - PANEL_INSET - HEM_INSET, hh - PANEL_INSET - HEM_INSET, 6.0, HEM_SPACING):
        c.over(hexrgb("A7CF86"), fill(sd_circle(c, HEM_DOT, x, y)) * 0.9)
    ring_r, count, dot = RING
    ox, oy, turn = CLOVER
    for k in range(count):
        a = math.radians(turn + 90.0 + 360.0 * k / count)
        c.over(hexrgb("B9DA9C"), fill(sd_circle(c, dot, ox + ring_r * math.cos(a), oy + ring_r * math.sin(a))) * 0.9)
    draw_clover(c)
    return "UI_Memory_CardBack_Prairie", c, RUNTIME_MEMORY


def ui_memory_card_face():
    """UI_Memory_CardFace: the plain cream face of a turned card, a soft warm outline, a light top rim, a soft inner border
    (208 x 288 inside) and a whisper of shade toward its lower edge; the portrait or the pawprint is laid on it."""
    c = Canvas(CARD_W, CARD_H)
    body = card_body(c)
    card_shadow(c, body)
    c.over(hexrgb("D9C29B"), fill(body))
    c.over(vgrad(c, "FFFCF5", "F8EDD8", BODY[1], -BODY[1]), fill(body + CARD_LINE))
    lower_shade(c, body + CARD_LINE, "EAD8B8", -60.0, 90.0, width=8.0, strength=0.5)
    top_rim(c, body + CARD_LINE, 60.0, 90.0, 0.8)
    hw, hh, r = BODY
    frame = np.abs(sd_round_rect(c, hw - 12.0, hh - 12.0, r - 8.0)) - 1.2
    c.over(hexrgb("E6D2AD"), fill(frame) * 0.9)
    return "UI_Memory_CardFace", c, RUNTIME_MEMORY


def ui_memory_glow_edge():
    """UI_Memory_GlowEdge: the glow card's gold edge (H-VFX-ACT-004-02), laid on a card: a soft gold rim along the card
    body (honey gold lit from the top, a deeper amber edge, a pale line at its heart), a warm glow fading 11 px outward
    and 9 px inward, strong enough to read on the green border of a back as on a cream face. Only the glow has alpha:
    the card shows through."""
    c = Canvas(CARD_W, CARD_H)
    body = card_body(c)
    outward = np.clip(1.0 - body / 11.5, 0.0, 1.0) ** 1.6 * (body > 0)
    inward = np.clip(1.0 + body / 9.0, 0.0, 1.0) ** 2 * (body <= 0)
    c.over(hexrgb("FFC44A"), outward * 0.95 + inward * 0.45)
    rim = np.abs(body + 1.5) - 3.8
    c.over(hexrgb("C97A22"), fill(rim - 0.8) * 0.9)
    c.over(vgrad(c, "FFE58A", "F2A33A", BODY[1], -BODY[1]), fill(rim))
    c.over(hexrgb("FFF8D8"), fill(np.abs(body + 1.5) - 1.1) * 0.85)
    return "UI_Memory_GlowEdge", c, RUNTIME_MEMORY


GOLD = ("FFE48A", "F0A73A", "A8662A", "E0892C", "FFF6C8")  # the challenge medal's honey gold (T37, T40)
BADGE_R = 46.0
TIMES = (-20.5, -6.1, 8.0, 4.6)  # the × sign: center, arm reach along each diagonal, stroke half width
TWO = (13.0, 8.9, 12.5, 5.6, -21.6, 1.0, 25.5)  # the 2: bowl center, radius, stroke, base height, base left, base right


def times_two(c):
    """« ×2 » as rounded strokes in the style of Pocket Round (Bold): a small round-capped ×, then the 2: a round bowl
    over the top, a diagonal down to the left and a flat foot, every end round."""
    x0, y0, reach, hw = TIMES
    d = reach / math.sqrt(2.0)
    times = np.minimum(sd_polyline(c, [(x0 - d, y0 - d), (x0 + d, y0 + d)], hw),
                       sd_polyline(c, [(x0 - d, y0 + d), (x0 + d, y0 - d)], hw))
    bx, by, r, sw, base, left, right = TWO
    end = math.radians(-38.0)
    ex, ey = bx + r * math.cos(end), by + r * math.sin(end)
    bowl = sd_arc(c, r, -38.0, 168.0, sw, bx, by)
    two = np.minimum(bowl, sd_polyline(c, [(ex, ey), (left, base), (right, base)], sw))
    return times, two


def ui_memory_double_badge():
    """UI_Memory_DoubleBadge: the double pair's badge (the pair is worth 200), a round honey gold badge with its deeper
    edge, a light top rim and a gloss, « ×2 » in white with a deeper amber edge."""
    c = Canvas(128, 128, cy=64.0)
    disc = sd_circle(c, BADGE_R)
    top, bottom, line, shade, light = GOLD
    sticker(c, disc, line, WIDTH)
    c.over(vgrad(c, top, bottom, BADGE_R, -BADGE_R), fill(disc))
    lower_shade(c, disc, shade, -10.0, 34.0, width=7.0, strength=0.55)
    top_rim(c, disc, 10.0, 30.0, 0.7, color=hexrgb(light))
    times, two = times_two(c)
    glyphs = np.minimum(times, two)
    c.over((110, 60, 20), blur(shift(fill(glyphs - 3.0), 0, 2.0), 1.5) * 0.25)
    c.over(hexrgb("9A5A22"), fill(glyphs - 3.0))
    c.over(vgrad(c, "FFFFFF", "FFF1D6", 26.0, -26.0), fill(glyphs))
    gloss(c, 12.0, 5.0, -18.0, 28.0, 28.0, fill(disc) * (1.0 - fill(glyphs - 3.0)), 0.45, edge=2.0)
    return "UI_Memory_DoubleBadge", c, RUNTIME_MEMORY


TILE = 256
CHECK = 64.0  # the gingham's period: a band of 32 px and a gap of 32 px, four checks a tile


def ui_memory_tablecloth():
    """UI_Memory_Tablecloth: the cream tablecloth (Prairie), a very soft gingham of pale sage bands on cream, darker where
    two bands cross, a faint weave in the bands; opaque, seamless at the tile's edges."""
    c = Canvas(TILE, TILE)
    u = c.x + TILE * 0.5  # 0 to 256 from the left
    w = c.y + TILE * 0.5  # 0 to 256 from the bottom

    def band(t):
        k = np.mod(t, CHECK) - CHECK * 0.5          # -32 to 32 around each band's middle
        return np.clip(0.5 - (np.abs(k) - CHECK * 0.25) / 1.2, 0.0, 1.0)

    vertical, horizontal = band(u), band(w)
    weave_v = 0.5 + 0.5 * np.cos(2.0 * np.pi * u / 4.0)
    weave_h = 0.5 + 0.5 * np.cos(2.0 * np.pi * w / 4.0)
    c.over(hexrgb("FFF7E8"), np.ones_like(u))
    c.over(hexrgb("D7E6BF"), vertical * (0.38 + 0.06 * weave_h))
    c.over(hexrgb("D7E6BF"), horizontal * (0.38 + 0.06 * weave_v))
    c.over(hexrgb("FFFDF6"), (1.0 - np.maximum(vertical, horizontal)) * 0.05 * weave_v * weave_h)
    return "UI_Memory_Tablecloth", c, RUNTIME_MEMORY


def ui_memory_glint():
    """UI_Memory_Glint: one soft round glint for the match (two of them pop beside the small heart, H-VFX-ACT-004-01).
    White for tinting: a round core whose edge softens over 3 px, a gentle round glow fading to the canvas edge. Round
    everywhere: no ray, no point."""
    c = Canvas(64, 64)
    r = np.hypot(c.x, c.y)
    c.over((255, 255, 255), np.clip(1.0 - (r - 9.0) / 21.0, 0.0, 1.0) ** 2.2 * 0.55)
    c.over((255, 255, 255), soft(r - 10.0, 3.0))
    return "UI_Memory_Glint", c, RUNTIME_MEMORY


# ---------------------------------------------------------------- pawprints (white, tinted with the coat)

PAW_RIM = "969696"   # mid grey: tinted, a deeper tone of the coat (readable even for a white coat)
PAW_SHADE = "DCDCDC"
PAW_LINE = 2.8       # the rim's width on a 128 px pawprint


def arc_toes(cx, cy, radius, angles, rx, ry):
    """Toes on an arc around (cx, cy), each turned to point away from it: (x, y, half width, half height, turn)."""
    return tuple((cx + radius * math.sin(math.radians(a)), cy + radius * math.cos(math.radians(a)), rx, ry, -a)
                 for a in angles)


# Pads: (x, y, half width, half height, turn in degrees); palms of merged circles: (x, y, radius).
RABBIT = ((0.0, -18.0, 16.5, 29.0, 0.0),) + arc_toes(0.0, 8.0, 32.0, (-45.0, -15.0, 15.0, 45.0), 6.4, 8.2)
SQUIRREL_PALM = ((-10.5, -14.0, 10.5), (10.5, -14.0, 10.5), (0.0, -26.0, 10.0))  # three round pads, merged
SQUIRREL_TOES = arc_toes(0.0, -16.0, 39.0, (-56.0, -19.0, 19.0, 56.0), 6.4, 12.8)
HEDGEHOG_PALM = ((-12.5, -12.0, 13.0), (12.5, -12.0, 13.0), (0.0, -17.0, 14.5))  # a broad bean, merged
HEDGEHOG_TOES = arc_toes(0.0, -14.0, 36.0, (-66.0, -33.0, 0.0, 33.0, 66.0), 7.0, 8.0)
# Each print scaled to about 110 px on its longer side and centered by measure (readable at 64 px, gaps of 4 px or more).
PAW_FIT = {'rabbit': dict(scale=1.17, oy=0.0), 'squirrel': dict(scale=1.255, oy=1.7), 'hedgehog': dict(scale=1.35, oy=1.15)}


def pads_of(c, ellipses=(), merged=(), merge_k=4.0, scale=1.0, oy=0.0):
    """The pads of a pawprint as separate distance fields: each ellipse, and the merged round palm (circles), the whole
    print scaled about the center and moved up by oy (output px) to fill the canvas."""
    s = scale
    pads = [sd_ellipse(c, rx * s, ry * s, ox=x * s, oy=y * s + oy, angle_deg=a) for x, y, rx, ry, a in ellipses]
    if merged:
        palm = sd_circle(c, merged[0][2] * s, merged[0][0] * s, merged[0][1] * s + oy)
        for x, y, r in merged[1:]:
            palm = smooth_min(palm, sd_circle(c, r * s, x * s, y * s + oy), merge_k * s)
        pads.append(palm)
    return pads


def draw_pads(c, pads):
    """White pads for tinting: a mid grey rim inside each pad (a deeper tone once tinted), white lit from the top, a light
    grey shade inside the lower edge, a soft light line inside the upper edge."""
    for pad in pads:
        c.over(hexrgb(PAW_RIM), fill(pad))
        inside = pad + PAW_LINE
        c.over(lerp((255, 255, 255), (242, 242, 242), np.clip((40.0 - c.y) / 90.0, 0, 1)), fill(inside))
        c.over(hexrgb(PAW_SHADE), soft(-inside - 4.0, 4.0) * np.clip(-(c.y - 10.0) / 60.0 + 0.3, 0, 1) * fill(inside) * 0.8)
        top_rim(c, inside, 0.0, 60.0, 0.7)


def ui_paw_rabbit():
    """UI_Paw_Rabbit: the rabbit's print, its long hind foot: a long oval heel pad under four small round toes close
    together in an arc. Tall and narrow."""
    c = Canvas(128, 128, cy=64.0)
    draw_pads(c, pads_of(c, RABBIT, **PAW_FIT['rabbit']))
    return "UI_Paw_Rabbit", c, RUNTIME_MEMORY


def ui_paw_squirrel():
    """UI_Paw_Squirrel: the squirrel's print: a small palm of three round pads under four long slender toes spread wide
    like little fingers."""
    c = Canvas(128, 128, cy=64.0)
    draw_pads(c, pads_of(c, SQUIRREL_TOES, SQUIRREL_PALM, **PAW_FIT['squirrel']))
    return "UI_Paw_Squirrel", c, RUNTIME_MEMORY


def ui_paw_hedgehog():
    """UI_Paw_Hedgehog: the hedgehog's print, a little hand: a broad bean-shaped palm under five small round toes in a
    tight arc. Wide and low."""
    c = Canvas(128, 128, cy=64.0)
    draw_pads(c, pads_of(c, HEDGEHOG_TOES, HEDGEHOG_PALM, merge_k=6.0, **PAW_FIT['hedgehog']))
    return "UI_Paw_Hedgehog", c, RUNTIME_MEMORY


# ---------------------------------------------------------------- reward icon

COOKIE = (40.0, 12, 7.5, -2.0)  # the cookie: radius, bumps of its cutter edge, bump radius, center height
SEEDS = ((90.0, 17.5), (210.0, 17.5), (330.0, 17.5))  # the three plump seeds: direction from the center, distance
SEED_SIZE = (7.6, 11.5, 0.22)   # half width, half height, taper (narrower toward the tip, pointing outward)
SESAME = ((-24.0, 18.0, 40.0), (25.0, 16.0, -35.0), (-27.0, -16.0, -50.0), (22.0, -22.0, 30.0), (1.0, -32.0, 85.0),
          (-4.0, 30.0, 95.0))   # small cream seeds: x, y, turn
POPPY = ((-14.0, 26.0), (14.0, 27.0), (-31.0, 2.0), (31.0, 0.0), (-13.0, -29.0), (14.0, -32.0))  # small dark seeds


def seed_shape(c, direction, distance):
    """A plump seed of ICO_Seed lying on the cookie, its rounded tip pointing outward."""
    a = math.radians(direction)
    ox, oy = math.cos(a) * distance, COOKIE[3] + math.sin(a) * distance
    v = View(c, ox, oy, direction - 90.0)
    hw, hh, taper = SEED_SIZE
    width = hw * (1.0 - taper * v.y / hh)
    return normalize(np.sqrt((v.x / np.maximum(width, 1.0)) ** 2 + (v.y / hh) ** 2) - 1.0), v


def icon_treat_seed_cookie():
    """ICO_Treat_SeedCookie (Biscuit aux graines, the Memory's 3-star treat): the round golden biscuit of
    ICO_Treat_CloverCrunch with a scalloped cookie-cutter edge and a pressed rim, three plump seeds of ICO_Seed in a
    pinwheel at its heart (brown, a pale seam, each with its own deeper edge), small round cream and dark seeds scattered
    around them, a honey gloss."""
    c = Canvas(128, 128, cy=64.0)
    r, bumps, bump_r, cy = COOKIE
    biscuit = scalloped(c, r, bumps, bump_r, 0.0, cy, k=3.0)
    sticker(c, biscuit, WARM_OUTLINE, WIDTH)
    c.over(vgrad(c, "F6D08E", "D8994F", cy + r + bump_r, cy - r - bump_r), fill(biscuit))
    lower_shade(c, biscuit, "C47F3C", cy, r, width=7.0, strength=0.7)
    c.over(hexrgb("D59A55"), fill(np.abs(sd_circle(c, r - 6.0, 0.0, cy)) - 1.1) * 0.6)
    top_rim(c, biscuit, cy + 10.0, r, 0.55, color=hexrgb("FBE2B0"))
    for x, y, turn in SESAME:
        s = sd_ellipse(c, 2.3, 3.6, ox=x, oy=cy + y, angle_deg=turn)
        c.over(hexrgb("B87A3C"), fill(s - 1.0) * 0.7)
        c.over(hexrgb("FFF3D6"), fill(s))
    for x, y in POPPY:
        c.over(hexrgb("6B4A33"), fill(sd_circle(c, 1.9, x, cy + y)) * 0.9)
    seeds = [seed_shape(c, d, dist) for d, dist in SEEDS]
    c.over((90, 50, 20), blur(shift(fill(union(*[s for s, _ in seeds]) - 2.5), 0, 2.0), 1.5) * 0.3)
    for seed, v in seeds:
        edge(c, seed, "6E4426", width=2.5)
    for seed, v in seeds:
        inside = fill(seed)
        c.over(vgrad(c, "DDA46A", "93602F", cy + 26.0, cy - 26.0), inside)
        seam = np.abs(v.x + 0.12 * v.y) - 0.9
        c.over(hexrgb("F2CB91"), fill(seam) * fill(seed + 3.0) * 0.85)
        c.over(hexrgb("82522A"), soft(-seed - 3.0, 3.0) * np.clip((-c.y + cy + 4.0) / 16.0, 0, 1) * inside * 0.5)
    gloss(c, 9.0, 4.0, -22.0, cy + 26.0, 30.0, fill(biscuit) * (1.0 - fill(union(*[s for s, _ in seeds]) - 2.5)), 0.4,
          edge=2.0)
    return "ICO_Treat_SeedCookie", c, RUNTIME_ICONS


T41_SPRITES = (ui_memory_card_back, ui_memory_card_face, ui_memory_glow_edge, ui_memory_double_badge, ui_memory_tablecloth,
               ui_paw_rabbit, ui_paw_squirrel, ui_paw_hedgehog, icon_treat_seed_cookie, ui_memory_glint)


def main():
    for folder in (RUNTIME_MEMORY, RUNTIME_ICONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T41_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
