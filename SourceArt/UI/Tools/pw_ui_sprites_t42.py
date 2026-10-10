"""Pocket Wilds UI sprites added in T42 (Expéditions, the first « meta » activity: a timer screen of its own; doc 01
Contenu, section 7 and S-ACT-005; doc 05 Sensations, sheet 23; H-VFX-ACT-005-02, the postcard's soft ring and 6 petals).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Soft, round, cream and
warm like the activity screens of T37 to T41. Owner rules: round dots, five-point stars, bubbles, hearts and paws only (the
daisy has eleven round petals kept apart, the « + » has round ends; nothing has a four-point outline); nothing religious;
nothing enters anything (the hills and the dots of the map, the petals, the pads of the paw and the three dots stand clear
of one another).

Screen (Art/UI/Activities/Expeditions, packed by the screen's pipeline; names start with UI_ or ICO_ so
TextureImportRules imports them as sprites; the editor-rendered destination cards of PostcardLayout, EXP_*, live in the
Destinations subfolder):
- UI_Exp_MapPaper, 512 x 320: the map card's paper, a soft parchment with rounded corners (its body 496 x 300, a baked
  shadow), a dotted path of round dots wandering across it and tiny round hills near its edges. Image type Simple.
- UI_Exp_DestLocked, 256 x 192: a destination not open yet, a soft locked silhouette (rounded card 240 x 176 of muted
  paper, hills and a round tree in silhouette) with a small padlock on a cream disc.
- UI_Exp_Slot_Empty, _Away, _Back, _Locked, 192 x 192, 9-slice (borders 64 on every side, TextureImportRules): the four
  states of a slot card on one body (160 x 156, corner radius 30, centered 2 px above the middle): Empty pressed into the
  paper (an inner shade, no shadow), Away the cream card, Back the cream card warmed with a honey glow edge, Locked muted.
- UI_Exp_Ring_Track, UI_Exp_Ring_Fill, 128 x 128: the soft timer ring (radius 49, 14 px wide); the fill is WHITE (light
  grey shading) for a token tint, drawn with Image Filled Radial 360 from the top over the cream track.
- UI_Exp_Plus, 96 x 96: the « + » of an empty slot, two round-ended strokes in leaf green.
- UI_Exp_Chip, UI_Exp_Chip_Selected, 160 x 120, 9-slice (borders 64, 60, 64, 56): the duration chip, cream, or honey with
  a soft glow when chosen; the clock icon and the label (« 1 h ») sit on it.
- ICO_Exp_Clock_1h, _4h, _8h, _12h, 96 x 96: a small round clock face, the time away filled in honey from twelve o'clock
  (a twelfth, a third, two thirds, the whole face) with one hand at its end.
- UI_Exp_PostcardFrame, 208 x 208, 9-slice (borders 72), Image type TILED at 592 x 848: the white deckled border of the
  postcard around its 512 x 768 window (40 px in from the sprite's edge), a soft shadow, a fine mount line and a soft
  inner shade over the picture's edge. The deckle repeats every 64 px and the middle tile is 64 px, so the frame tiles
  without a seam at 592 x 848 (7 x 11 tiles); any size of 144 + 64 k on each side works.
- UI_Exp_Stamp_FlowerMeadows, 128 x 128: the round stamp of Prés fleuris: a scalloped white stamp, a daisy over a green
  hill in a pale sky.
- UI_Exp_Postmark, 128 x 128: the round postmark ring in soft brown ink (two rings and a row of round dots), laid half
  over the stamp.
- UI_Exp_Petal, 64 x 64: one round petal of the postcard's six (H-VFX-ACT-005-02), white to blush, its round end up.

Icons (Art/UI/Activities/Expeditions with the screen's sprites, own colours, readable at 48 px):
- ICO_Exp_Reason_Biome (a leaf), ICO_Exp_Reason_Family (a paw), ICO_Exp_Reason_Species (three round dots), 96 x 96: the
  team rating's reasons (same biome, favourite family, three species).
- ICO_Postcard, 128 x 128: the back of a postcard (handwritten lines, a stamp and its postmark).
- ICO_Expedition_Back, 128 x 128: a small honey gold door bell ringing (a team is back).
Reused from the kit, not redrawn: ICO_Act_Expeditions, ICO_Lock, ICO_Timer, ICO_Challenge, ICO_Currency_Gems, UI_Ring_Soft
(the postcard's soft ring), UI_Star_Result_Full and _Empty.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t42.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (ROOT, SS, Canvas, MASTERS, blur, fill, hexrgb, lerp, normalize, sd_circle, sd_ellipse,
                           sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t35a import SHADOW, drop_shadow
from pw_ui_sprites_t36 import WARM_OUTLINE, View, gloss, sd_arc, sticker, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, scalloped, sd_drop, top_rim, union
from pw_ui_sprites_t41 import arc_toes, pads_of

RUNTIME_EXPEDITIONS = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Activities", "Expeditions")

WIDTH = 4.5           # the family's outline on a 128 px icon
LEAF = ("B8E68A", "5E9E4A", "4F7A2E", "4F8A3F")    # top, bottom, outline, vein (T36, T40, T41)
HILL = ("C9EBA4", "8EC36B", "6E9E52", "7DB45C")    # the map's hills: top, bottom, outline, base shade
GOLD = ("FFE48A", "F0A73A", "A8662A", "E0892C", "FFF6C8")  # honey gold of the medals and the padlock
CARD = ("FFFCF5", "F8EDD8", "D9C29B", "EAD8B8")    # the cream card face of T41: top, bottom, outline, lower shade
INK = "7D6553"        # the postmark's soft brown ink


def dome(c, rx, ry, ox, oy):
    """A round hill: the upper half of an ellipse standing on its baseline (oy)."""
    return np.maximum(sd_ellipse(c, rx, ry, ox=ox, oy=oy), oy - c.y)


def sample(c, field, x, y):
    """The value of a field (one per 4x sub-pixel) at the canvas point (x, y)."""
    col = int(round((x - c.x[0, 0]) * SS))
    row = int(round((c.y[0, 0] - y) * SS))
    return float(field[min(max(row, 0), field.shape[0] - 1), min(max(col, 0), field.shape[1] - 1)])


def catmull(points, step):
    """Points every `step` px along a Catmull-Rom curve through `points` (the dotted path)."""
    dense = []
    pts = [points[0]] + list(points) + [points[-1]]
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = (np.array(p, dtype=np.float64) for p in pts[i - 1:i + 3])
        for t in np.linspace(0.0, 1.0, 60, endpoint=False):
            t2, t3 = t * t, t * t * t
            dense.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    dense.append(np.array(points[-1], dtype=np.float64))
    out, run = [dense[0]], 0.0
    for a, b in zip(dense[:-1], dense[1:]):
        run += float(np.hypot(*(b - a)))
        if run >= step:
            out.append(b)
            run = 0.0
    return out


# ---------------------------------------------------------------- the map card

MAP_BODY = (248.0, 150.0, 28.0)   # half width, half height, corner radius (canvas origin 156 px from the top)
MAP_HILLS = ((-206.0, -104.0, 26.0, 17.0), (-159.0, -106.0, 17.0, 11.0), (-224.0, 96.0, 15.0, 10.0),
             (180.0, 92.0, 22.0, 15.0), (222.0, 90.0, 14.0, 9.0), (36.0, -122.0, 19.0, 12.0),
             (71.0, -124.0, 12.0, 8.0), (208.0, -100.0, 16.0, 11.0))  # x, baseline y, half width, height; 4 px apart
MAP_PATH = ((-178.0, -78.0), (-120.0, -20.0), (-40.0, 10.0), (20.0, -34.0), (96.0, -40.0), (132.0, 10.0),
            (150.0, 46.0))
MAP_DOT = (3.4, 15.0)             # the path's dots: radius, spacing along the path


def ui_exp_map_paper():
    """UI_Exp_MapPaper: the map card's paper: a soft parchment lit from the top, warmer and a little deeper toward its
    rounded edges, faint soft mottling; a dotted path of round dots wandering from the lower left to the upper right, and
    tiny round hills (a light top, a deeper outline, a soft ground shade) near the corners and the lower edge."""
    c = Canvas(512, 320, cy=156.0)
    hw, hh, r = MAP_BODY
    body = sd_round_rect(c, hw, hh, r)
    drop_shadow(c, body, dy=4.0, radius=4.0, strength=0.22)
    c.over(hexrgb("C9A877"), fill(body))
    paper = body + 2.5
    c.over(vgrad(c, "FCF3DF", "F2E2C0", hh, -hh), fill(paper))
    mottle = (np.sin(c.x / 37.0 + 0.6) * np.sin(c.y / 23.0 + 1.3) + 0.6 * np.sin((c.x + c.y) / 51.0 + 2.2)) * 0.5 + 0.5
    c.over(hexrgb("EAD3A6"), fill(paper) * np.clip(mottle, 0, 1) ** 2 * 0.22)
    c.over(hexrgb("E0C391"), soft(-paper - 16.0, 16.0) * fill(paper) * 0.45)
    top_rim(c, paper, 40.0, 110.0, 0.55, color=hexrgb("FFF9EA"))
    hills = [dome(c, rx, ry, x, y) for x, y, rx, ry in MAP_HILLS]
    for (x, y, rx, ry), h in zip(MAP_HILLS, hills):
        c.over(hexrgb("DDC394"), fill(sd_ellipse(c, rx * 1.12, 3.2, ox=x, oy=y)) * 0.7)
    for (x, y, rx, ry), h in zip(MAP_HILLS, hills):
        c.over(hexrgb(HILL[2]), fill(h - 2.2))
        c.over(vgrad(c, HILL[0], HILL[1], y + ry, y), fill(h))
        c.over(hexrgb(HILL[3]), soft(-h - 3.0, 3.0) * np.clip((y + ry * 0.45 - c.y) / ry, 0, 1) * fill(h) * 0.6)
        gloss(c, rx * 0.28, ry * 0.18, x - rx * 0.35, y + ry * 0.62, 25.0, fill(h), 0.45, edge=1.0)
    near_hill = union(*hills)
    dot_r, spacing = MAP_DOT
    for x, y in catmull(MAP_PATH, spacing):
        if sample(c, near_hill, x, y) > dot_r + 5.0:   # a dot never touches a hill
            c.over(hexrgb("B5865A"), fill(sd_circle(c, dot_r, x, y)) * 0.8)
    # The path ends at a small round goal: a ring of the path's colour around a coral dot.
    gx, gy = MAP_PATH[-1]
    c.over(hexrgb("B5865A"), fill(np.abs(sd_circle(c, 9.0, gx + 8.0, gy + 13.0)) - 1.8) * 0.8)
    c.over(hexrgb("D9805E"), fill(sd_circle(c, 4.2, gx + 8.0, gy + 13.0)) * 0.9)
    return "UI_Exp_MapPaper", c, RUNTIME_EXPEDITIONS


def padlock(c, ox, oy, s=1.0):
    """A small padlock (ICO_Lock's honey gold body and pale shackle), centered on its body."""
    shackle = sd_arc(c, 10.0 * s, 0.0, 180.0, 3.2 * s, ox, oy + 9.0 * s)
    legs = union(sd_polyline(c, [(ox - 10.0 * s, oy + 9.0 * s), (ox - 10.0 * s, oy + 2.0 * s)], 3.2 * s),
                 sd_polyline(c, [(ox + 10.0 * s, oy + 9.0 * s), (ox + 10.0 * s, oy + 2.0 * s)], 3.2 * s))
    loop = np.minimum(shackle, legs)
    edge(c, loop, WARM_OUTLINE, width=2.2 * s)
    c.over(vgrad(c, "F4F1EA", "BDB6AA", oy + 22.0 * s, oy), fill(loop))
    body = sd_round_rect(c, 15.0 * s, 11.5 * s, 5.0 * s, ox, oy - 4.0 * s)
    edge(c, body, WARM_OUTLINE, width=2.4 * s)
    top, bottom, line, shade, light = GOLD
    c.over(vgrad(c, top, bottom, oy + 7.5 * s, oy - 15.5 * s), fill(body))
    lower_shade(c, body, shade, oy - 4.0 * s, 10.0 * s, width=3.0 * s, strength=0.6)
    hole = union(sd_circle(c, 2.8 * s, ox, oy - 2.0 * s), sd_polyline(c, [(ox, oy - 3.0 * s), (ox, oy - 9.0 * s)], 1.3 * s))
    c.over(hexrgb("8A5222"), fill(hole))
    gloss(c, 4.0 * s, 2.0 * s, ox - 7.0 * s, oy + 3.0 * s, 20.0, fill(body), 0.5, edge=0.8)


def ui_exp_dest_locked():
    """UI_Exp_DestLocked: a destination not open yet: a rounded card of soft muted paper, a quiet landscape in
    silhouette (two rows of round hills, a round tree, a pale round sun), and a small padlock on a cream disc."""
    c = Canvas(256, 192, cy=94.0)
    card = sd_round_rect(c, 120.0, 88.0, 20.0)
    drop_shadow(c, card, dy=3.0, radius=3.0, strength=0.18)
    c.over(hexrgb("BEB1A0"), fill(card))
    inner = card + 3.0
    c.over(vgrad(c, "EEE8DE", "E0D7CA", 88.0, -88.0), fill(inner))
    c.over(hexrgb("F6F1E8"), fill(sd_circle(c, 15.0, 62.0, 40.0)) * fill(inner) * 0.8)
    back = union(dome(c, 70.0, 42.0, -64.0, -40.0), dome(c, 80.0, 36.0, 60.0, -48.0))
    c.over(hexrgb("D3C8B9"), fill(back) * fill(inner))
    tree = union(sd_circle(c, 17.0, -78.0, 18.0), sd_polyline(c, [(-78.0, 4.0), (-78.0, -40.0)], 4.0))
    c.over(hexrgb("C6BAA9"), fill(tree) * fill(inner))
    front = union(dome(c, 96.0, 30.0, -40.0, -88.0), dome(c, 90.0, 26.0, 88.0, -90.0))
    c.over(hexrgb("C3B6A5"), fill(front) * fill(inner))
    c.over(hexrgb("D7CCBD"), soft(-inner - 10.0, 10.0) * fill(inner) * 0.5)
    disc = sd_circle(c, 30.0, 0.0, 2.0)
    c.over((90, 70, 50), blur(shift(fill(disc), 0, 2.0), 2.0) * 0.18)
    c.over(hexrgb("D8CCBA"), fill(disc - 1.5))
    c.over(vgrad(c, "FFFBF2", "F2E8D6", 32.0, -28.0), fill(disc))
    padlock(c, 0.0, 0.0, 1.05)
    return "UI_Exp_DestLocked", c, RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- the slot cards (9-slice, borders 64)

SLOT = (80.0, 78.0, 30.0)   # body half width, half height, corner radius; canvas origin 94 px from the top
SLOT_LINE = 3.0


def slot_body(c):
    hw, hh, r = SLOT
    return sd_round_rect(c, hw, hh, r)


def ui_exp_slot_empty():
    """UI_Exp_Slot_Empty: a free slot, pressed into the paper: a slightly deeper cream, a soft shade inside its upper edge
    (lit from above, it is a hollow), a light line along its lower edge, a fine soft outline. No shadow."""
    c = Canvas(192, 192, cy=94.0)
    body = slot_body(c)
    hh = SLOT[1]
    c.over(hexrgb("FFFBF2"), fill(body - 1.5) * np.clip((-c.y - 20.0) / 40.0, 0, 1) * 0.9)
    c.over(hexrgb("E3D2B2"), fill(body))
    inner = body + 2.0
    c.over(vgrad(c, "F1E5CD", "F7EEDC", hh, -hh), fill(inner))
    c.over(hexrgb("D9C39C"), soft(-inner - 9.0, 9.0) * np.clip((c.y + 10.0) / 60.0, 0, 1) * fill(inner) * 0.6)
    return "UI_Exp_Slot_Empty", c, RUNTIME_EXPEDITIONS


def card_face(c, body, top, bottom, line, shade, shadow=0.22):
    """The cream card of a slot (T41's card face): soft shadow, warm outline, gradient, lower shade, light top rim."""
    hh = SLOT[1]
    drop_shadow(c, body, dy=4.0, radius=3.5, strength=shadow)
    c.over(hexrgb(line), fill(body))
    inner = body + SLOT_LINE
    c.over(vgrad(c, top, bottom, hh, -hh), fill(inner))
    lower_shade(c, inner, shade, -30.0, 60.0, width=8.0, strength=0.5)
    top_rim(c, inner, 30.0, 50.0, 0.8)
    return inner


def ui_exp_slot_away():
    """UI_Exp_Slot_Away: a team is away: the plain cream card (team portraits, the ring and the time sit on it)."""
    c = Canvas(192, 192, cy=94.0)
    top, bottom, line, shade = CARD
    card_face(c, slot_body(c), top, bottom, line, shade)
    return "UI_Exp_Slot_Away", c, RUNTIME_EXPEDITIONS


def ui_exp_slot_back():
    """UI_Exp_Slot_Back: the team is back: the cream card warmed (a honey cream), a honey glow 12 px outward and a soft
    gold rim with a pale line at its heart (the glow of T41's glow edge, quieter), so a slot that waits reads at once."""
    c = Canvas(192, 192, cy=94.0)
    body = slot_body(c)
    outward = np.clip(1.0 - body / 12.5, 0.0, 1.0) ** 1.8 * (body > 0)
    c.over(hexrgb("FFC44A"), outward * 0.85)
    card_face(c, body, "FFFAEC", "FBE8C2", "C97A22", "F0D3A0", shadow=0.0)
    rim = np.abs(body + 1.5) - 3.2
    c.over(vgrad(c, "FFE58A", "F2A33A", SLOT[1], -SLOT[1]), fill(rim) * 0.95)
    c.over(hexrgb("FFF8D8"), fill(np.abs(body + 1.5) - 0.9) * 0.8)
    inward = np.clip(1.0 + (body + 4.5) / 9.0, 0.0, 1.0) ** 2 * (body + 4.5 <= 0)
    c.over(hexrgb("FFD36B"), inward * 0.3)
    return "UI_Exp_Slot_Back", c, RUNTIME_EXPEDITIONS


def ui_exp_slot_locked():
    """UI_Exp_Slot_Locked: a slot not owned yet: the card in muted greyish beige, a softer shadow (ICO_Lock and the price
    sit on it)."""
    c = Canvas(192, 192, cy=94.0)
    card_face(c, slot_body(c), "EEE9E1", "E0D8CC", "BDB2A5", "D2C8BA", shadow=0.14)
    return "UI_Exp_Slot_Locked", c, RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- the timer ring

RING = (49.0, 7.0)   # centre radius, half width


def ui_exp_ring_track():
    """UI_Exp_Ring_Track: the ring's track, a soft cream groove: a deeper tone inside its outer edge (lit from above, it
    is a hollow), a light line along its inner lower edge."""
    c = Canvas(128, 128)
    rc, hw = RING
    rr = np.hypot(c.x, c.y)
    ring = np.abs(rr - rc) - hw
    c.over(hexrgb("DCC9A6"), fill(ring - 1.0))
    c.over(hexrgb("EFE3CB"), fill(ring))
    c.over(hexrgb("D6C29E"), soft(-ring - 3.5, 3.5) * np.clip((c.y + 10.0) / 50.0, 0, 1) * fill(ring) * 0.55)
    return "UI_Exp_Ring_Track", c, RUNTIME_EXPEDITIONS


def ui_exp_ring_fill():
    """UI_Exp_Ring_Fill: the ring's fill, WHITE for a token tint (Image Filled, Radial 360 from the top): white lit from
    the top, a light grey shade inside its lower edge, a mid grey rim that tints into a deeper tone."""
    c = Canvas(128, 128)
    rc, hw = RING
    ring = np.abs(np.hypot(c.x, c.y) - rc) - hw
    c.over(hexrgb("A8A8A8"), fill(ring))
    inner = ring + 1.6
    c.over(lerp((255, 255, 255), (238, 238, 238), np.clip((50.0 - c.y) / 100.0, 0, 1)), fill(inner))
    c.over(hexrgb("DADADA"), soft(-inner - 3.0, 3.0) * np.clip(-c.y / 50.0, 0, 1) * fill(inner) * 0.7)
    c.over((255, 255, 255), fill(np.abs(np.hypot(c.x, c.y) - (rc + 2.0)) - 0.9) * np.clip(c.y / 50.0, 0, 1) * 0.6)
    return "UI_Exp_Ring_Fill", c, RUNTIME_EXPEDITIONS


def ui_exp_plus():
    """UI_Exp_Plus: the « + » of an empty slot: two plump strokes with round ends in leaf green, a deeper green outline
    over a soft shadow, a light top rim and a gloss."""
    c = Canvas(96, 96, cy=46.0)
    plus = np.minimum(sd_polyline(c, [(-22.0, 0.0), (22.0, 0.0)], 8.5), sd_polyline(c, [(0.0, -22.0), (0.0, 22.0)], 8.5))
    top, bottom, line, _ = LEAF
    sticker(c, plus, line, 3.6, dy=3.0, radius=2.5, strength=0.25)
    c.over(vgrad(c, top, bottom, 30.0, -30.0), fill(plus))
    lower_shade(c, plus, "4F8A3F", -4.0, 26.0, width=4.0, strength=0.45)
    top_rim(c, plus, 0.0, 30.0, 0.6, color=hexrgb("E3F5CF"))
    gloss(c, 6.0, 2.6, -11.0, 3.5, 0.0, fill(plus), 0.45, edge=1.0)
    return "UI_Exp_Plus", c, RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- the duration chips (9-slice, borders 64, 60, 64, 56)

CHIP = (70.0, 42.0)   # body half width, half height (a pill); canvas origin 58 px from the top


def chip_body(c):
    hw, hh = CHIP
    return sd_round_rect(c, hw, hh, hh)


def ui_exp_chip():
    """UI_Exp_Chip: a duration not chosen: a cream pill, a warm outline, a light top rim, a soft shadow."""
    c = Canvas(160, 120, cy=58.0)
    body = chip_body(c)
    drop_shadow(c, body, dy=3.0, radius=3.0, strength=0.18)
    c.over(hexrgb("D9C29B"), fill(body))
    inner = body + 3.0
    c.over(vgrad(c, "FFFCF5", "F6EAD3", CHIP[1], -CHIP[1]), fill(inner))
    lower_shade(c, inner, "EAD8B8", -10.0, 30.0, width=6.0, strength=0.5)
    top_rim(c, inner, 10.0, 25.0, 0.8)
    return "UI_Exp_Chip", c, RUNTIME_EXPEDITIONS


def ui_exp_chip_selected():
    """UI_Exp_Chip_Selected: the duration chosen: a honey pill with its deeper amber outline, a soft warm glow around
    it, a light top rim."""
    c = Canvas(160, 120, cy=58.0)
    body = chip_body(c)
    c.over(hexrgb("FFD36B"), np.clip(1.0 - body / 9.0, 0.0, 1.0) ** 1.8 * (body > 0) * 0.8)
    drop_shadow(c, body, dy=3.0, radius=3.0, strength=0.14)
    c.over(hexrgb("C9822F"), fill(body))
    inner = body + 3.5
    c.over(vgrad(c, "FFEDB8", "F7C46A", CHIP[1], -CHIP[1]), fill(inner))
    lower_shade(c, inner, "EBA54A", -10.0, 30.0, width=6.0, strength=0.5)
    top_rim(c, inner, 10.0, 25.0, 0.85, color=hexrgb("FFF8E0"))
    return "UI_Exp_Chip_Selected", c, RUNTIME_EXPEDITIONS


CLOCK_R = 36.0


def clock_icon(hours):
    """A small round clock face for a duration: a honey tan rim, a cream face with twelve round tick dots (bigger at
    twelve, three, six and nine), the time away filled in soft honey from twelve o'clock clockwise (hours / 12 of the
    face) with a fine deeper edge, one brown hand at its end and a round center."""
    c = Canvas(96, 96, cy=46.0)
    face = sd_circle(c, CLOCK_R)
    sticker(c, face, "A8703A", 3.6, dy=3.0, radius=2.5, strength=0.22)
    c.over(vgrad(c, "FFFBF0", "F2E3C7", CLOCK_R, -CLOCK_R), fill(face))
    lower_shade(c, face, "E6D2AE", -8.0, 30.0, width=5.0, strength=0.5)
    span = 360.0 * hours / 12.0
    angle = (90.0 - np.degrees(np.arctan2(c.y, c.x))) % 360.0   # clockwise from twelve o'clock
    wedge_r = CLOCK_R - 7.0
    inside = sd_circle(c, wedge_r)
    if hours >= 12:
        sector = inside
    else:
        a_end = math.radians(90.0 - span)
        # Distance to the two radii bounding the sector, signed by the angle.
        d_start = np.where(c.y > 0, np.abs(c.x), np.hypot(c.x, c.y))
        ex, ey = math.cos(a_end), math.sin(a_end)
        t = np.clip(c.x * ex + c.y * ey, 0.0, None)
        d_end = np.hypot(c.x - ex * t, c.y - ey * t)
        on = angle <= span
        sector = np.where(on, np.maximum(inside, -np.minimum(d_start, d_end)), np.minimum(d_start, d_end))
        sector = np.maximum(sector, inside)
    c.over(hexrgb("E9A53E"), fill(sector - 1.2) * 0.85)
    c.over(vgrad(c, "FFE39A", "FFC85E", wedge_r, -wedge_r), fill(sector + 0.4))
    for k in range(12):
        a = math.radians(90.0 - 30.0 * k)
        big = k % 3 == 0
        c.over(hexrgb("9C6A3E"), fill(sd_circle(c, 2.4 if big else 1.6, (CLOCK_R - 5.5) * math.cos(a), (CLOCK_R - 5.5) * math.sin(a))) * 0.9)
    a = math.radians(90.0 - span)
    hand = sd_polyline(c, [(0.0, 0.0), (22.0 * math.cos(a), 22.0 * math.sin(a))], 2.6)
    c.over(hexrgb("7A4A26"), fill(hand))
    c.over(hexrgb("7A4A26"), fill(sd_circle(c, 4.6)))
    c.over(hexrgb("FFE9B0"), fill(sd_circle(c, 1.7)))
    gloss(c, 9.0, 4.0, -14.0, 22.0, 30.0, fill(face), 0.35, edge=2.0)
    return c


def icon_exp_clock_1h():
    """ICO_Exp_Clock_1h: one hour, a twelfth of the face."""
    return "ICO_Exp_Clock_1h", clock_icon(1), RUNTIME_EXPEDITIONS


def icon_exp_clock_4h():
    """ICO_Exp_Clock_4h: four hours, a third of the face."""
    return "ICO_Exp_Clock_4h", clock_icon(4), RUNTIME_EXPEDITIONS


def icon_exp_clock_8h():
    """ICO_Exp_Clock_8h: eight hours, two thirds of the face."""
    return "ICO_Exp_Clock_8h", clock_icon(8), RUNTIME_EXPEDITIONS


def icon_exp_clock_12h():
    """ICO_Exp_Clock_12h: twelve hours, the whole face."""
    return "ICO_Exp_Clock_12h", clock_icon(12), RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- the postcard (9-slice, borders 72, Image type Tiled)

FRAME = 208            # the sprite's side
FRAME_BORDER = 72      # its 9-slice borders
FRAME_TILE = 64        # the middle tile, and the deckle's period
FRAME_FULL = (592, 848)  # the frame at its size on screen: the 512 x 768 window and 40 px all around
FRAME_WINDOW = 40.0    # from the frame's edge to the picture
FRAME_PAPER = 14.0     # from the frame's edge to the paper's edge (26 px of white paper around the picture)
DECKLE = {  # per edge: (amplitude, phase) for the harmonics 1 to 5 of the 64 px period: irregular, periodic
    'top': ((1.0, 0.3), (0.7, 1.7), (0.45, 2.1), (0.35, 0.9), (0.25, 4.0)),
    'bottom': ((0.9, 2.4), (0.75, 0.2), (0.5, 3.3), (0.3, 1.5), (0.25, 5.1)),
    'left': ((1.0, 4.4), (0.65, 2.9), (0.5, 0.6), (0.35, 3.8), (0.2, 1.2)),
    'right': ((0.95, 1.1), (0.7, 5.5), (0.45, 4.2), (0.3, 2.6), (0.25, 0.4)),
}


def deckle(s, harmonics):
    """The torn paper's edge offset (px) along an edge at position s: harmonics of the 64 px period, so it repeats."""
    return sum(a * np.sin(2.0 * math.pi * (k + 1) * s / FRAME_TILE + p) for k, (a, p) in enumerate(harmonics))


def frame_coords(c):
    """Each sprite pixel's place on the full frame (x right, y down, px): the left and top borders as they are, the middle
    tile anywhere along the edge (here its first copy), the right and bottom borders at the far side. Every 64 px period
    lines up, so the deckle and the shadow join without a seam when the middle tiles."""
    px = c.x + FRAME * 0.5
    py = FRAME * 0.5 - c.y
    far = FRAME - FRAME_BORDER
    fx = np.where(px >= far, px + (FRAME_FULL[0] - FRAME), px)
    fy = np.where(py >= far, py + (FRAME_FULL[1] - FRAME), py)
    return fx, fy


def frame_rect(fx, fy, inset, radius):
    """A rounded rectangle inset from the full frame's edges, as a signed distance on the full frame."""
    w, h = FRAME_FULL
    hw, hh = w * 0.5 - inset, h * 0.5 - inset
    qx = np.abs(fx - w * 0.5) - hw + radius
    qy = np.abs(fy - h * 0.5) - hh + radius
    return np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0)) + np.minimum(np.maximum(qx, qy), 0.0) - radius


def ui_exp_postcard_frame():
    """UI_Exp_PostcardFrame: the postcard's white border: thick white paper with a soft deckled edge (an irregular
    torn-paper line, a faint fringe of fibres), a warm white toward its edge, a soft shadow below it, a fine mount line
    around the picture and a soft shade over the picture's edge, as if the picture sat a hair under the paper. The
    window is clear: the postcard's picture shows through."""
    c = Canvas(FRAME, FRAME)
    fx, fy = frame_coords(c)
    w, h = FRAME_FULL
    dist_h = np.minimum(fy, h - fy)        # to the top or bottom edge
    dist_v = np.minimum(fx, w - fx)        # to the left or right edge
    along_h = np.where(fy < h * 0.5, deckle(fx, DECKLE['top']), deckle(fx, DECKLE['bottom']))
    along_v = np.where(fx < w * 0.5, deckle(fy, DECKLE['left']), deckle(fy, DECKLE['right']))
    k = np.clip((dist_v - dist_h) / 12.0 + 0.5, 0.0, 1.0)  # 1 near a horizontal edge, 0 near a vertical one
    offset = along_h * k + along_v * (1.0 - k)
    paper = frame_rect(fx, fy, FRAME_PAPER, 7.0) - offset
    window = frame_rect(fx, fy, FRAME_WINDOW, 1.5)
    border = np.maximum(paper, -window)
    c.over(SHADOW, blur(shift(fill(border), 0, 3.0), 4.0) * 0.26)
    c.over(hexrgb("FFFFFF"), soft(border - 0.8, 1.6) * 0.45)          # the fringe of fibres
    c.over(lerp(hexrgb("FFFFFF"), hexrgb("F6F0E4"), np.clip(1.0 - (-paper) / 10.0, 0, 1)), fill(border))
    c.over(hexrgb("E9E2D4"), fill(np.abs(window) - 0.6) * (window > -2.0))  # the mount line around the picture
    shade = np.clip(1.0 + window / 6.0, 0.0, 1.0) ** 2 * (window < 0)
    c.over(hexrgb("5A4632"), shade * 0.2)
    return "UI_Exp_PostcardFrame", c, RUNTIME_EXPEDITIONS


DAISY = (11, 14.5, 4.0, 8.0, 7.0)  # petals, petal center distance, half width, half length, heart radius


def daisy(c, ox, oy, s=1.0, outline="C9B9A0"):
    """A daisy seen from the front: eleven round white petals, wider toward their round outer end and kept apart, a
    deeper cream outline, a honey heart with its amber edge and a gloss."""
    count, dist, hw, hl, heart = DAISY
    petals = []
    for k in range(count):
        a = math.radians(90.0 + 360.0 * k / count)
        v = View(c, ox + dist * s * math.cos(a), oy + dist * s * math.sin(a), math.degrees(a) - 90.0)
        petals.append(sd_drop(v, hw * s, hl * s, -0.35))
    for p in petals:
        c.over(hexrgb(outline), fill(p - 1.2 * s))
    for p in petals:
        c.over(vgrad(c, "FFFFFF", "F1ECE4", oy + 22.0 * s, oy - 22.0 * s), fill(p))
    core = sd_circle(c, heart * s, ox, oy)
    c.over(hexrgb("C9822F"), fill(core - 1.5 * s))
    c.over(vgrad(c, "FFE27A", "F2A93A", oy + heart * s, oy - heart * s), fill(core))
    gloss(c, 2.6 * s, 1.4 * s, ox - 2.4 * s, oy + 2.6 * s, 30.0, fill(core), 0.6, edge=0.5)


def ui_exp_stamp_flower_meadows():
    """UI_Exp_Stamp_FlowerMeadows: the round stamp of Prés fleuris: a white stamp with a scalloped edge (the stamp's
    perforations, made round), a fine cream ring, inside a pale sky over a soft green hill, a daisy standing in front."""
    c = Canvas(128, 128, cy=62.0)
    stamp = scalloped(c, 50.0, 24, 4.2, 0.0, 0.0, k=2.0)
    drop_shadow(c, stamp, dy=3.0, radius=2.5, strength=0.22)
    c.over(hexrgb("D8CBB6"), fill(stamp))
    c.over(vgrad(c, "FFFFFF", "F7F2EA", 54.0, -54.0), fill(stamp + 1.6))
    pic = sd_circle(c, 40.0)
    c.over(hexrgb("E3D3B8"), fill(pic - 1.8))
    c.over(vgrad(c, "D4EEFA", "EEF8FC", 40.0, -10.0), fill(pic))
    hill = dome(c, 58.0, 34.0, 0.0, -32.0)
    c.over(vgrad(c, HILL[0], HILL[1], 2.0, -40.0), fill(hill) * fill(pic))
    for x, y in ((-24.0, -18.0), (22.0, -22.0), (-8.0, -30.0), (30.0, -12.0)):
        c.over(hexrgb("FFFFFF"), fill(sd_circle(c, 2.4, x, y)) * 0.95)
        c.over(hexrgb("FFD45A"), fill(sd_circle(c, 1.0, x, y)))
    stem = sd_polyline(c, [(1.0, -36.0), (0.0, -6.0)], 2.2)
    c.over(hexrgb("5E9E4A"), fill(stem) * fill(pic))
    daisy(c, 0.0, 8.0, 0.95)
    return "UI_Exp_Stamp_FlowerMeadows", c, RUNTIME_EXPEDITIONS


def ui_exp_postmark():
    """UI_Exp_Postmark: the postmark, soft brown ink: an outer and an inner ring and a row of round dots between them,
    the ink a little uneven (a hand stamp, never a hard print); clear in the middle."""
    c = Canvas(128, 128)
    rr = np.hypot(c.x, c.y)
    ink = np.minimum(np.abs(rr - 52.0) - 2.0, np.abs(rr - 39.0) - 1.6)
    for k in range(18):
        a = math.tau * k / 18.0 + 0.1
        ink = np.minimum(ink, sd_circle(c, 2.1, 45.5 * math.cos(a), 45.5 * math.sin(a)))
    uneven = 0.78 + 0.22 * (0.5 + 0.5 * np.sin(c.x / 9.0 + 0.7) * np.sin(c.y / 13.0 + 2.0))
    c.over(hexrgb(INK), fill(ink) * uneven * 0.85)
    return "UI_Exp_Postmark", c, RUNTIME_EXPEDITIONS


def ui_exp_petal():
    """UI_Exp_Petal: one round petal of the postcard's six (H-VFX-ACT-005-02): wider toward its round outer end (up),
    white with a blush toward its base, a fine soft pink outline, a faint middle vein."""
    c = Canvas(64, 64)
    petal = sd_drop(View(c, 0.0, -1.0), 13.0, 25.0, -0.35)
    c.over(hexrgb("F0BBC8"), fill(petal - 1.4))
    c.over(vgrad(c, "FFFFFF", "FFDDE6", 22.0, -24.0), fill(petal))
    c.over(hexrgb("F6C9D4"), fill(sd_polyline(c, [(0.0, -18.0), (0.0, 8.0)], 0.7)) * fill(petal + 3.0) * 0.6)
    gloss(c, 4.0, 2.0, -4.5, 12.0, 20.0, fill(petal), 0.5, edge=0.8)
    return "UI_Exp_Petal", c, RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- the rating's reasons (96 x 96)

def icon_exp_reason_biome():
    """ICO_Exp_Reason_Biome (from the destination's biome): one round leaf, a little turned, its tip rounded, a short
    stem, a deeper green outline, a gradient lit from the top left, a fine midrib and a gloss."""
    c = Canvas(96, 96, cy=46.0)
    v = View(c, 2.0, 4.0, -35.0)
    leaf = sd_drop(v, 20.0, 31.0, 0.32)
    stem = sd_polyline(c, [(-14.0, -21.0), (-24.0, -34.0)], 3.0)
    top, bottom, line, vein = LEAF
    sticker(c, union(leaf, stem), line, 3.4, dy=3.0, radius=2.5, strength=0.22)
    c.over(vgrad(c, "9FD077", "5E9E4A", -18.0, -36.0), fill(stem))
    light = np.clip(((c.y - 4.0) - (c.x - 2.0)) / 70.0 + 0.5, 0, 1)
    c.over(lerp(hexrgb(bottom), hexrgb(top), light), fill(leaf))
    rib = sd_polyline(c, [(-12.0, -18.0), (16.0, 26.0)], 1.2)
    c.over(hexrgb(vein), fill(rib) * fill(leaf + 4.0) * 0.7)
    for t, side in ((0.35, 1), (0.55, -1), (0.7, 1)):
        bx, by = -12.0 + 28.0 * t, -18.0 + 44.0 * t
        c.over(hexrgb(vein), fill(sd_polyline(c, [(bx, by), (bx + side * 9.0 + 3.0, by + 6.0 - side * 3.0)], 0.8)) * fill(leaf + 5.0) * 0.5)
    gloss(c, 7.0, 3.2, -6.0, 18.0, 55.0, fill(leaf), 0.45, edge=1.2)
    return "ICO_Exp_Reason_Biome", c, RUNTIME_EXPEDITIONS


PAW = ((0.0, -6.0, 15.0), (-11.0, -10.0, 11.0), (11.0, -10.0, 11.0))  # the palm, merged round pads
PAW_TOES = arc_toes(0.0, -8.0, 33.0, (-54.0, -18.0, 18.0, 54.0), 5.8, 7.4)


def icon_exp_reason_family():
    """ICO_Exp_Reason_Family (the destination's favourite family): a warm brown paw, a round palm under four round toes
    kept apart, each pad with its deeper outline, lit from the top, a gloss on each."""
    c = Canvas(96, 96, cy=46.0)
    pads = pads_of(c, PAW_TOES, PAW, merge_k=5.0, scale=1.0, oy=2.0)
    whole = union(*pads)
    c.over(SHADOW, blur(shift(fill(whole - 2.4), 0, 3.0), 2.5) * 0.22)
    for pad in pads:
        c.over(hexrgb("6B4226"), fill(pad - 2.4))
    for pad in pads:
        c.over(vgrad(c, "D9A273", "A5683D", 36.0, -30.0), fill(pad))
        lower_shade(c, pad, "8E5A33", -6.0, 30.0, width=3.5, strength=0.45)
        top_rim(c, pad, -20.0, 50.0, 0.5, color=hexrgb("F2CDA6"))
    return "ICO_Exp_Reason_Family", c, RUNTIME_EXPEDITIONS


DOTS = ((-19.0, -12.0, "FFB3A4", "E2675A", "B4473C"), (19.0, -12.0, "FFE08A", "EFA83A", "B97A22"),
        (0.0, 20.0, "B5DFF7", "5BA6D8", "3C7BAA"))   # x, y, top, bottom, outline
DOT_R = 12.0


def icon_exp_reason_species():
    """ICO_Exp_Reason_Species (three different species): three round dots in a little triangle, coral, honey and sky,
    each with its deeper outline, a gradient and a gloss, 7 px of clear space between their outlines."""
    c = Canvas(96, 96, cy=48.0)
    shapes = [sd_circle(c, DOT_R, x, y) for x, y, *_ in DOTS]
    c.over(SHADOW, blur(shift(fill(union(*shapes) - 3.0), 0, 3.0), 2.5) * 0.22)
    for (x, y, top, bottom, line), s in zip(DOTS, shapes):
        c.over(hexrgb(line), fill(s - 3.0))
        c.over(vgrad(c, top, bottom, y + DOT_R, y - DOT_R), fill(s))
        gloss(c, 4.5, 2.4, x - 4.0, y + 5.5, 30.0, fill(s), 0.55, edge=0.8)
    return "ICO_Exp_Reason_Species", c, RUNTIME_EXPEDITIONS


# ---------------------------------------------------------------- icons (128 x 128)

def icon_postcard():
    """ICO_Postcard: the back of a postcard, a little turned: cream card with its warm outline, three handwritten lines
    on the left (round-ended strokes, gently wavy), a fine divider, two address lines, a small scalloped coral stamp with
    a white daisy dot in the top right and a postmark ring over its corner."""
    c = Canvas(128, 128, cy=62.0)
    turn = -8.0
    v = View(c, 0.0, 0.0, turn)
    card = sd_round_rect(v, 50.0, 36.0, 6.0)
    sticker(c, card, WARM_OUTLINE, WIDTH)
    c.over(vgrad(c, "FFFCF4", "F3E6CC", 40.0, -40.0), fill(card))
    lower_shade(c, card, "E6D1AA", -10.0, 30.0, width=6.0, strength=0.5)
    ink = hexrgb("9C7A5A")

    def line(points, hw=2.3):
        return sd_polyline(v, points, hw)

    lines = []
    for y, length in ((14.0, 30.0), (2.0, 34.0), (-10.0, 24.0)):
        pts = [(-40.0 + t, y + 1.6 * math.sin(t / 5.0)) for t in np.linspace(0.0, length, 9)]
        lines.append(line(pts))
    c.over(ink, fill(union(*lines)) * 0.85)
    c.over(hexrgb("D9C29B"), fill(line([(2.0, 22.0), (2.0, -26.0)], 0.9)))
    c.over(ink, fill(union(line([(12.0, -6.0), (40.0, -6.0)], 1.8), line([(12.0, -18.0), (36.0, -18.0)], 1.8))) * 0.7)
    sx, sy = 31.0, 17.0
    stamp = sd_round_rect(View(c, *rot(sx, sy, turn), turn), 10.0, 12.0, 2.0)
    bumps = stamp
    for k in range(-2, 3):
        for side in (-1, 1):
            bumps = smooth_min(bumps, sd_circle(View(c, *rot(sx, sy, turn), turn), 2.0, 10.0 * side, 5.0 * k), 1.0)
            bumps = smooth_min(bumps, sd_circle(View(c, *rot(sx, sy, turn), turn), 2.0, 5.0 * k * 0.8, 12.0 * side), 1.0)
    c.over(hexrgb("B4473C"), fill(bumps - 1.6))
    c.over(vgrad(c, "FFB3A4", "E2675A", 30.0, 4.0), fill(bumps))
    px, py = rot(sx, sy, turn)
    c.over(hexrgb("FFFFFF"), fill(sd_circle(c, 4.0, px, py)))
    c.over(hexrgb("FFD45A"), fill(sd_circle(c, 1.7, px, py)))
    rr = np.hypot(c.x - (px - 9.0), c.y - (py - 8.0))
    c.over(hexrgb(INK), fill(np.abs(rr - 9.5) - 1.2) * fill(card) * 0.75)
    gloss(c, 12.0, 3.5, -22.0, 28.0, turn, fill(card), 0.35, edge=2.0)
    return "ICO_Postcard", c, RUNTIME_EXPEDITIONS


def rot(x, y, angle_deg):
    """A point of a turned shape (its local x, y) on the canvas."""
    a = math.radians(angle_deg)
    return x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)


def icon_expedition_back():
    """ICO_Expedition_Back: a small honey gold door bell ringing: a round dome flaring to a rolled rim, a small loop on
    top, a round clapper swinging out below, two short soft arcs of sound on each side; deeper amber outline, a gradient
    lit from the top left, a gloss on the dome."""
    c = Canvas(128, 128, cy=62.0)
    v = View(c, 0.0, 0.0, 10.0)
    bell = smooth_min(sd_circle(v, 24.0, 0.0, 10.0),
                      np.maximum(np.abs(v.x) - (24.0 + (10.0 - v.y) * 0.42), np.maximum(v.y - 10.0, -24.0 - v.y)), 6.0)
    rim = sd_ellipse(c, 37.0, 7.5, ox=rot(0.0, -24.0, 10.0)[0], oy=rot(0.0, -24.0, 10.0)[1], angle_deg=10.0)
    body = np.minimum(bell, rim)
    loop = sd_arc(v, 7.5, 0.0, 180.0, 3.0, 0.0, 34.0)
    clapper = sd_circle(c, 7.5, *rot(5.0, -34.0, 10.0))
    top, bottom, line, shade, light = GOLD
    whole = union(body, loop, clapper)
    sticker(c, whole, line, 3.8, dy=3.0, radius=2.5, strength=0.24)
    c.over(vgrad(c, "FFF1C2", "D9A24A", 42.0, 26.0), fill(loop))
    c.over(vgrad(c, "E8B455", "B8762E", -28.0, -42.0), fill(clapper))
    c.over(vgrad(c, top, bottom, 34.0, -30.0), fill(body))
    lower_shade(c, body, shade, -4.0, 28.0, width=6.0, strength=0.55)
    c.over(hexrgb(line), fill(np.abs(rim) - 1.0) * (c.y > rot(0.0, -24.0, 10.0)[1] - 1.0) * 0.6)
    top_rim(c, body, 0.0, 30.0, 0.6, color=hexrgb(light))
    gloss(c, 7.0, 4.0, -12.0, 20.0, 60.0, fill(bell), 0.55, edge=1.2)
    for side in (-1, 1):
        for r, hw in ((44.0, 2.6), (54.0, 2.2)):
            a0, a1 = (150.0, 190.0) if side < 0 else (-10.0, 30.0)
            arc = sd_arc(c, r, a0, a1, hw, 0.0, 4.0)
            c.over(hexrgb("E0892C"), fill(arc - 1.2) * 0.9)
            c.over(hexrgb("FFD27A"), fill(arc))
    return "ICO_Expedition_Back", c, RUNTIME_EXPEDITIONS


T42_SPRITES = (ui_exp_map_paper, ui_exp_dest_locked, ui_exp_slot_empty, ui_exp_slot_away, ui_exp_slot_back,
               ui_exp_slot_locked, ui_exp_ring_track, ui_exp_ring_fill, ui_exp_plus, ui_exp_chip, ui_exp_chip_selected,
               icon_exp_clock_1h, icon_exp_clock_4h, icon_exp_clock_8h, icon_exp_clock_12h, ui_exp_postcard_frame,
               ui_exp_stamp_flower_meadows, ui_exp_postmark, ui_exp_petal, icon_exp_reason_biome,
               icon_exp_reason_family, icon_exp_reason_species, icon_postcard, icon_expedition_back)


def main():
    for folder in (RUNTIME_EXPEDITIONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T42_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
