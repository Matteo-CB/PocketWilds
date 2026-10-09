"""Pocket Wilds UI sprites added in T39 (Cache-cache, the second activity on the T37 framework; doc 01 Contenu,
S-ACT-002, the items table; doc « Mini-jeux & activités », section 3).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the
HUD icons: rounded filled shapes, one outline in a deeper tone of each material, light from the top left (a vertical
gradient, one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round dots, five-point
stars, bubbles, hearts and paws only (the whistle's two notes are round dots, never sharp lines or a four-point star),
nothing religious, nothing enters anything (the paw lies behind the glass, the string runs through its ring, the notes
float clear of the whistle).

Item icons (own colors, never tinted; inside the round item buttons of the HUD, IActivityHudView.ShowItems):
- ICO_Magnifier: the Loupe, a round magnifying glass, a honey gold rim, a soft pale blue lens with one curved gloss
  and a small paw print seen through it, a coral collar and a warm wooden handle with a round knob.
- ICO_Whistle: the Sifflet, a small round wooden whistle (its round chamber shows the wood's end grain), its window
  open on top, two soft round notes floating up from it (the « deux notes de flûte douce » of J-SFX-ACT-002-05), a
  coral string through a little gold ring. Also the whistle of Photo Safari (S-ACT-010).

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t39.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, fill, hexrgb, lerp, sd_circle, sd_polygon, sd_polyline,
                           sd_round_rect, smooth_min, soft)
from pw_ui_sprites_t34a import sd_paw
from pw_ui_sprites_t35a import drop_shadow
from pw_ui_sprites_t36 import View, gloss, sd_arc, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, sd_drop, top_rim, union

WIDTH = 4.5  # the family's outline on a 128 px icon

# The two items' materials: one warm wood (deeper than the grooming brush's, so the large whistle reads as wood), the
# challenge medal's honey gold, the photo camera's coral.
WOOD = ("F3C07E", "B46F35", "5E3F24", "95552A", "FCE2BC")  # top, bottom, outline, lower shade, light streak
GOLD = ("FFE48A", "F0A73A", "A8662A", "E0892C", "FFF6C8")
CORAL = ("FFA594", "E2654F", "9C3E3A", "C9503C", "FFD2C8")


def soft_dot(c, r, x, y, top, bottom, line=None, line_width=2.0):
    """A soft round dot (a note, a bead): its own deeper edge (none when the edge is already drawn), a vertical
    gradient, a tiny round gloss."""
    dot = sd_circle(c, r, x, y)
    if line is not None:
        c.over(hexrgb(line), fill(dot - line_width))
    c.over(vgrad(c, top, bottom, y + r, y - r), fill(dot))
    c.over((255, 255, 255), fill(sd_circle(c, r * 0.3, x - r * 0.35, y + r * 0.38)) * 0.85)
    return dot


# ---------------------------------------------------------------- magnifier

LENS_X, LENS_Y = -10.0, 10.0  # the lens center
RIM_R, GLASS_R = 34.0, 25.0   # the rim's outer radius, the glass radius
HANDLE_ANGLE = -45.0          # the handle points to the bottom right
COLLAR = (RIM_R - 3.0, RIM_R + 8.0, 8.6)        # from, to (along the handle axis), half width
HANDLE = (RIM_R + 8.0, RIM_R + 34.0, 7.2, 9.6)  # from, to, half width at the neck, knob radius
PAW_SCALE, PAW_X, PAW_Y, PAW_TURN = 0.66, 1.0, -1.0, -14.0  # in the lens frame (from the lens center)
PAW_COLOR = ("7FA8D6", "5D88C0")  # the paw seen through the pale blue glass: a deeper blue of the lens


def magnifier_shapes(c):
    """The magnifier's pieces in icon units: lens disc, glass, collar, handle (and the handle's view)."""
    disc = sd_circle(c, RIM_R, LENS_X, LENS_Y)
    glass = sd_circle(c, GLASS_R, LENS_X, LENS_Y)
    hv = View(c, LENS_X, LENS_Y, HANDLE_ANGLE)  # +x along the handle, away from the lens
    c0, c1, ch = COLLAR
    collar = sd_round_rect(hv, (c1 - c0) * 0.5, ch, 3.5, ox=(c0 + c1) * 0.5)
    h0, h1, hw, knob = HANDLE
    handle = smooth_min(sd_polyline(hv, [(h0, 0.0), (h1 - knob * 0.4, 0.0)], hw), sd_circle(hv, knob, h1, 0.0), 6.0)
    return disc, glass, collar, handle, hv


def icon_magnifier():
    """ICO_Magnifier: the Loupe item. A round lens in a honey gold rim, a soft pale blue glass with a small deeper blue
    paw print seen through it, one curved gloss and a round glint, a coral collar, a warm wooden handle ending in a
    round knob with a hanging hole."""
    c = Canvas(128, 128, cy=64.0)
    disc, glass, collar, handle, hv = magnifier_shapes(c)
    drop_shadow(c, union(disc, collar, handle) - WIDTH)
    # The handle and its knob: the whistle's wood, lit along its upper side, a light streak, a hanging hole.
    top, bottom, line, shade, light = WOOD
    edge(c, handle, line)
    c.over(lerp(hexrgb(top), hexrgb(bottom), np.clip((6.0 - hv.y) / 16.0, 0, 1)), fill(handle))
    c.over(hexrgb(shade), soft(-handle - 4.0, 4.0) * np.clip((-hv.y + 1.0) / 8.0, 0, 1) * fill(handle) * 0.55)
    streak = sd_polyline(hv, [(HANDLE[0] + 4.0, 3.6), (HANDLE[1] - 2.0, 4.4)], 1.4)
    c.over(hexrgb(light), fill(streak) * fill(handle) * 0.85)
    c.over(hexrgb("6B4528"), fill(sd_circle(hv, 2.6, HANDLE[1] + 1.0, -0.5)))
    # The coral collar joining the handle to the rim.
    top, bottom, line, shade, light = CORAL
    edge(c, collar, line)
    c.over(lerp(hexrgb(top), hexrgb(bottom), np.clip((7.0 - hv.y) / 15.0, 0, 1)), fill(collar))
    c.over(hexrgb(light), fill(sd_polyline(hv, [(COLLAR[0] + 3.0, 4.6), (COLLAR[1] - 2.5, 4.6)], 1.2)) * fill(collar) * 0.8)
    # The rim: honey gold, a deeper lower shade, a light upper line.
    top, bottom, line, shade, light = GOLD
    edge(c, disc, line)
    rim = fill(disc)
    c.over(vgrad(c, top, bottom, LENS_Y + RIM_R, LENS_Y - RIM_R), rim)
    lower_shade(c, disc, shade, LENS_Y - 4.0, 26.0, width=4.0, strength=0.6)
    top_rim(c, disc, LENS_Y + 6.0, 24.0, 0.7, color=hexrgb(light))
    # The glass, set in the rim: a thin deeper line, pale blue lit from the top, a deeper inner edge.
    c.over(hexrgb(line), fill(glass - 1.6))
    inside = fill(glass)
    c.over(vgrad(c, "F2FAFF", "B4DCF4", LENS_Y + GLASS_R, LENS_Y - GLASS_R), inside)
    # The paw behind the glass, a little turned, in a deeper blue of the lens.
    pv = View(c, LENS_X + PAW_X, LENS_Y + PAW_Y, PAW_TURN)
    paw = sd_paw(pv, PAW_SCALE)
    c.over(vgrad(c, PAW_COLOR[0], PAW_COLOR[1], LENS_Y + 14.0, LENS_Y - 14.0), fill(paw) * inside)
    c.over(hexrgb("8DC1E6"), soft(-glass - 5.0, 5.0) * np.clip((LENS_Y - c.y + 4.0) / 24.0, 0, 1) * inside * 0.6)
    # One curved gloss along the upper left of the glass, a small round glint under its end.
    arc = sd_arc(c, GLASS_R - 6.5, 112.0, 162.0, 2.6, ox=LENS_X, oy=LENS_Y)
    c.over((255, 255, 255), fill(arc) * inside * 0.95)
    c.over((255, 255, 255), fill(sd_circle(c, 2.3, LENS_X - 16.0, LENS_Y - 2.0)) * inside * 0.8)
    return "ICO_Magnifier", c, RUNTIME_ICONS


# ---------------------------------------------------------------- whistle

WHISTLE_X, WHISTLE_Y, WHISTLE_TILT = 3.0, 5.0, -13.0  # the chamber's center, the tilt (the mouth end rises)
CHAMBER_R = 22.5
MOUTH = ((-44.0, 19.0), (0.0, 22.5), (0.0, 2.0), (-44.0, 6.0))  # the mouthpiece, in the whistle's frame
NOTCH = ((-10.5, 28.5), (-10.5, 17.0), (-7.0, 17.0), (1.5, 28.5))  # the window cut in the top, its slanted lip right
WOOD_FACE = ("F8D7A6", "CB8A4A", "B8763A")  # the chamber's round face: top, bottom, end grain ring
GRAIN = ((12.5, False), (2.1, True))  # the end grain on the face: growth rings and the heart (radius, filled)
RING_ANGLE = -50.0  # where the gold ring sits on the chamber's edge (whistle frame, degrees)
NOTES = ((8.5, 45.0, 4.0, CORAL), (25.0, 48.5, 5.0, GOLD))  # x, y (icon frame), radius, material


def to_icon(x, y):
    """A point of the whistle's frame in icon units."""
    a = math.radians(WHISTLE_TILT)
    return WHISTLE_X + x * math.cos(a) - y * math.sin(a), WHISTLE_Y + x * math.sin(a) + y * math.cos(a)


def whistle_shapes(c):
    """The whistle's pieces in icon units: chamber, mouthpiece, the window notch, and the whistle's view."""
    wv = View(c, WHISTLE_X, WHISTLE_Y, WHISTLE_TILT)
    chamber = sd_circle(wv, CHAMBER_R)
    # Polygons are filled on the canvas grid (sd_polygon), so their points are turned into icon units first.
    mouth = sd_polygon(c, [to_icon(x, y) for x, y in MOUTH]) - 3.0
    notch = sd_polygon(c, [to_icon(x, y) for x, y in NOTCH]) - 1.2
    return chamber, mouth, notch, wv


LOOP = (4.0, -15.5, 9.0, 10.0)  # the string loop's center from the ring (x, y), its half width and half height
LOOP_LEAN, LOOP_TAPER = 12.0, 0.45  # the hanging loop's lean (degrees) and how much narrower its top is
BEAD_AT = (1.0, -5.5)  # the bead closing the loop, from the ring
CORD = 2.2  # the string's half width


def string_shapes(c):
    """The gold ring on the chamber's edge and the coral string through it, closed by a round bead."""
    a = math.radians(RING_ANGLE)
    rx, ry = to_icon(math.cos(a) * (CHAMBER_R + 3.5), math.sin(a) * (CHAMBER_R + 3.5))
    ring = np.abs(sd_circle(c, 4.6, rx, ry)) - 1.9
    bead_x, bead_y = rx + BEAD_AT[0], ry + BEAD_AT[1]
    lx, ly, lw, lh = LOOP
    # The loop hangs from the bead, narrow at its top, round at its bottom, leaning a little away from the whistle.
    lv = View(c, rx + lx, ry + ly, LOOP_LEAN)
    loop = np.abs(sd_drop(lv, lw, lh, LOOP_TAPER)) - CORD
    string = union(sd_polyline(c, [(rx + 0.5, ry - 4.0), (bead_x, bead_y)], CORD), loop)
    bead_r = 4.2
    bead = sd_circle(c, bead_r, bead_x, bead_y)
    return ring, string, bead, (rx, ry), (bead_x, bead_y, bead_r)


def icon_whistle():
    """ICO_Whistle: the Sifflet item. A small round wooden whistle seen from the side, its round chamber showing the
    end grain, the mouthpiece rising a little to the left, the window cut in its top, a coral string through a small
    gold ring, two soft round notes floating up from the window."""
    c = Canvas(128, 128, cy=64.0)
    chamber, mouth, notch, wv = whistle_shapes(c)
    body = np.maximum(union(chamber, mouth), -notch)
    ring, string, bead, (rx, ry), (bead_x, bead_y, bead_r) = string_shapes(c)
    notes = [sd_circle(c, r, x, y) for x, y, r, _ in NOTES]
    drop_shadow(c, union(body, ring, string, bead, *notes) - WIDTH)
    top, bottom, line, shade, light = CORAL
    edge(c, union(string, bead), line, width=2.6)
    c.over(vgrad(c, top, bottom, ry, bead_y - 18.0), fill(string))
    soft_dot(c, bead_r, bead_x, bead_y, top, bottom)
    top, bottom, line, shade, light = GOLD
    edge(c, ring, line, width=2.0)
    c.over(vgrad(c, top, bottom, ry + 6.0, ry - 6.0), fill(ring))
    # The wood: mouthpiece and chamber, one outline around both (the notch is filled by it: a dark slot).
    top, bottom, line, shade, light = WOOD
    edge(c, body, line)
    c.over(vgrad(c, top, bottom, WHISTLE_Y + 28.0, WHISTLE_Y - 22.5), fill(body))
    lower_shade(c, mouth, shade, WHISTLE_Y + 4.0, 16.0, width=4.0, strength=0.5)
    # The chamber's round face in front: its edge drawn, lighter end grain, one growth ring, lit from the top left.
    face = np.maximum(chamber, -notch)
    c.over(hexrgb(line), fill(np.abs(chamber + 0.2) - 1.3) * fill(mouth + 2.0) * fill(face - 1.0) * 0.7)
    top, bottom, ring_color = WOOD_FACE
    c.over(vgrad(c, top, bottom, WHISTLE_Y + CHAMBER_R, WHISTLE_Y - CHAMBER_R), fill(face + 1.4))
    for r, filled in GRAIN:
        grain = sd_circle(wv, r, 1.0, -1.0)
        c.over(hexrgb(ring_color), fill(grain if filled else np.abs(grain) - 1.0) * 0.6)
    lower_shade(c, chamber, shade, WHISTLE_Y - 4.0, 20.0, width=5.0, strength=0.5)
    top_rim(c, face, WHISTLE_Y + 4.0, 20.0, 0.5)
    # Light: a streak along the mouthpiece top, a gloss on the chamber's face.
    streak = sd_polyline(wv, [(-39.0, 16.0), (-14.5, 19.0)], 1.4)
    c.over(hexrgb(light), fill(streak) * fill(mouth + 1.0) * 0.85)
    gx, gy = to_icon(-11.0, 10.0)
    gloss(c, 6.0, 3.0, gx, gy, 40.0 + WHISTLE_TILT, fill(face + 1.4), 0.65)
    # The two notes, round and soft, floating up clear of the whistle.
    for x, y, r, (top, bottom, line, _, _) in NOTES:
        soft_dot(c, r, x, y, top, bottom, line)
    return "ICO_Whistle", c, RUNTIME_ICONS


T39_SPRITES = (icon_magnifier, icon_whistle)


def main():
    for folder in (RUNTIME_ICONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T39_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
