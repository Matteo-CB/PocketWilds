"""Pocket Wilds UI sprites added in T38 (Toilettage, the first activity on the T37 framework; doc 01 Contenu, S-ACT-001;
doc « Mini-jeux & activités », section 3; A15 tutorial).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Same family as the
HUD icons: rounded filled shapes, one outline in a deeper tone of each material, light from the top left (a vertical
gradient, one deeper lower shade, one gloss), a soft warm drop shadow below. Owner rules: round dots, five-point
stars, bubbles, hearts and paws only (never a four-point star: the golden brush's glints are round dots), nothing
religious (the bow is two loops, a knot and two short splayed tails, never a crossed ribbon), the gesture is a plush
paw, never a hand.

Kit (Atlas_CommonHUD, next to UI_Gesture_Tap):
- UI_Gesture_Swipe: the tutorial paw of UI_Gesture_Tap seen slightly from the side, its pads down on the coat as it
  slides to the right, three soft round-capped motion arcs behind it. The A15 « Brosse le pelage » gesture.

Icons (own colors, never tinted; inside the round picker buttons and the boost chip):
- ICO_Bow_Pink, ICO_Bow_SkyBlue, ICO_Bow_Cream: the same ribbon bow (two round loops, a round knot, two short notched
  tails) in three colors, one per GroomingBow. The cream bow keeps a soft warm outline so it reads on cream buttons.
- ICO_GoldenBrush: the grooming brush of ICO_Act_Grooming in warm gold, a soft gloss and two round glints, for the
  golden brush boost chip.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t38.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, RUNTIME_KIT, blur, fill, hexrgb, lerp, sd_circle, sd_ellipse,
                           sd_polyline, sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t35a import drop_shadow
from pw_ui_sprites_t36 import View, gloss, vgrad
from pw_ui_sprites_t37 import edge, lower_shade, ribbon_tail, sd_drop, top_rim, union

WIDTH = 4.5  # the family's outline on a 128 px icon


# ---------------------------------------------------------------- gesture

PAW_OUTLINE = "6B4E3A"  # UI_Gesture_Tap's outline
PAW_PINK = "F7B3C2"     # UI_Gesture_Tap's pads
PAW_TOES = ((-40.0, 22.0, 13.0, 16.0, 24.0), (-14.0, 42.0, 13.0, 17.0, 8.0), (15.0, 42.0, 13.0, 17.0, -8.0),
            (40.0, 22.0, 13.0, 16.0, -24.0))


def sd_tap_paw(v, s):
    """The silhouette of UI_Gesture_Tap's paw on a view (s = 1 is the tap paw's size), and its main pad alone."""
    pad = sd_ellipse(v, 34.0 * s, 29.0 * s, ox=0.0, oy=-14.0 * s, angle_deg=-8.0)
    toes = None
    for tx, ty, rx, ry, ang in PAW_TOES:
        d = sd_ellipse(v, rx * s, ry * s, ox=tx * s, oy=ty * s, angle_deg=ang)
        toes = d if toes is None else np.minimum(toes, d)
    return np.minimum(pad, toes), pad


# The paw on the 192 px frame: its center, its lean into the slide (toes forward), its turn on its side (the face seen
# a little narrower) and the plush thickness seen on the trailing side.
SWIPE_SCALE, SWIPE_X, SWIPE_Y, SWIPE_LEAN, SWIPE_TURN = 0.9, 30.0, 4.0, -16.0, 0.86
SWIPE_DEPTH = (-6.0, -4.0)
# The path the paw slides along: a wide dome, like the round back of the coat, its top just under the pad. Three arcs
# follow it behind the paw: (lane, far x, near x, near half width, far half width).
PATH_RADIUS, PATH_X, PATH_TOP, LANE = 190.0, 18.0, -12.0, 21.0
ARCS = ((1, -62.0, -26.0, 4.6, 2.8), (0, -78.0, -30.0, 5.8, 3.2), (-1, -60.0, -34.0, 4.6, 2.8))
ARC_OUTLINE = 4.0
ARC_FADE = 0.2  # the far end of an arc keeps 80 % of its strength and a lighter warm outline: the trail softens
ARC_FAR_OUTLINE = "A9876A"


def sd_soft_arc(c, r, a0_deg, a1_deg, half0, half1, ox=0.0, oy=0.0):
    """A stroke along a circle arc from a0 to a1 (degrees, counterclockwise, a short span), its half width going from
    half0 at a0 to half1 at a1, round caps of the same widths (soft, never pointed). Also returns the position along
    the stroke (0 at a0, 1 at a1)."""
    x, y = c.x - ox, c.y - oy
    angle = np.degrees(np.arctan2(y, x))
    span = (a1_deg - a0_deg) % 360.0
    along = ((angle - a0_deg + 180.0) % 360.0) - 180.0
    t = np.clip(along / span, 0.0, 1.0)
    on = (along >= 0.0) & (along <= span)
    half = half0 + (half1 - half0) * t
    a0, a1 = math.radians(a0_deg), math.radians(a1_deg)
    cap0 = np.hypot(x - r * math.cos(a0), y - r * math.sin(a0)) - half0
    cap1 = np.hypot(x - r * math.cos(a1), y - r * math.sin(a1)) - half1
    return np.minimum(np.where(on, np.abs(np.hypot(x, y) - r) - half, 1e3), np.minimum(cap0, cap1)), t


def motion_arcs(c):
    """The three soft motion arcs behind the paw, parallel to its path, fuller near the paw and thinner behind it.
    Returns each arc's distance and its position along the arc (0 near the paw)."""
    oy = PATH_TOP - PATH_RADIUS
    arcs = []
    for lane, far_x, near_x, near, far in ARCS:
        r = PATH_RADIUS + lane * LANE
        a0 = math.degrees(math.acos((near_x - PATH_X) / r))
        a1 = math.degrees(math.acos((far_x - PATH_X) / r))
        arcs.append(sd_soft_arc(c, r, a0, a1, near, far, ox=PATH_X, oy=oy))
    return arcs


def layer_over(c, layer, strength):
    """Composite a separately drawn layer (premultiplied) over the canvas, scaled by a strength field."""
    m = np.clip(strength, 0.0, 1.0)[..., None]
    a = layer.rgba[..., 3:] * m
    c.rgba[..., :3] = layer.rgba[..., :3] * m + c.rgba[..., :3] * (1.0 - a)
    c.rgba[..., 3:] = a + c.rgba[..., 3:] * (1.0 - a)


def swipe_contact():
    """Where the pad touches the coat on the 192 px frame, in pixels from the frame's center (x right, y up): the
    point the HUD slides along the gesture's path."""
    a = math.radians(SWIPE_LEAN)
    lx, ly = 1.5 * SWIPE_TURN, -16.0 * SWIPE_SCALE
    return SWIPE_X + lx * math.cos(a) - ly * math.sin(a), SWIPE_Y + lx * math.sin(a) + ly * math.cos(a)


def kit_gesture_swipe():
    """UI_Gesture_Swipe: 192 px, the tutorial paw of UI_Gesture_Tap (same cream, pink pads, brown outline) turned a
    little on its side and leaning into the slide, pads down on the coat, its plush thickness seen on the trailing
    side, three soft round-capped arcs behind it that soften toward their far end. Drawn sliding to the right."""
    c = Canvas(192, 192)
    s = SWIPE_SCALE
    face_v = View(c, SWIPE_X, SWIPE_Y, SWIPE_LEAN, SWIPE_TURN, 1.0)
    back_v = View(c, SWIPE_X + SWIPE_DEPTH[0], SWIPE_Y + SWIPE_DEPTH[1], SWIPE_LEAN, SWIPE_TURN, 1.0)
    face, _ = sd_tap_paw(face_v, s)
    back = sd_ellipse(back_v, 34.0 * s, 29.0 * s, ox=0.0, oy=-14.0 * s, angle_deg=-8.0)
    paw = np.minimum(face, back)
    # The arcs first, each on its own layer so it fades as a whole (outline, fill and shadow) toward its far end.
    for arc, t in motion_arcs(c):
        layer = Canvas(192, 192)
        layer.over((50, 40, 30), blur(shift(fill(arc - ARC_OUTLINE), 0, 4), 3.0) * 0.22)
        layer.over(lerp(hexrgb(PAW_OUTLINE), hexrgb(ARC_FAR_OUTLINE), t), fill(arc - ARC_OUTLINE))
        layer.over(lerp(hexrgb("FFF9F0"), hexrgb("EADCC6"), np.clip((30.0 - c.y) / 80.0, 0, 1)), fill(arc))
        layer_over(c, layer, 1.0 - ARC_FADE * t)
    # The paw, as UI_Gesture_Tap: shadow, outline, cream, pink pads, one gloss.
    c.over((50, 40, 30), blur(shift(fill(paw - 5.5), 0, 6), 5.0) * 0.32)
    c.over(hexrgb(PAW_OUTLINE), fill(paw - 5.5))
    c.over(lerp(hexrgb("EFE1CB"), hexrgb("D6C0A2"), np.clip((30.0 - c.y) / 80.0, 0, 1)), fill(back))
    c.over(hexrgb("C9AF8E"), fill(face - 1.4) * fill(back) * 0.7)
    c.over(lerp(hexrgb("FFFFFF"), hexrgb("F2E8DA"), np.clip((50.0 - c.y) / 100.0, 0, 1)), fill(face))
    c.over(hexrgb(PAW_PINK), fill(sd_ellipse(face_v, 22.0 * s, 17.0 * s, ox=1.5, oy=-16.0 * s, angle_deg=-8.0)))
    for tx, ty, _, _, ang in PAW_TOES:
        bean = sd_ellipse(face_v, 6.5 * s, 8.0 * s, ox=tx * s + 1.0, oy=(ty - 2.0) * s, angle_deg=ang)
        c.over(hexrgb(PAW_PINK), fill(bean))
    c.over((255, 255, 255), fill(sd_ellipse(face_v, 7.0 * s, 4.0 * s, ox=-10.0 * s, oy=-6.0 * s, angle_deg=20.0)) * 0.7)
    return "UI_Gesture_Swipe", c, RUNTIME_KIT


# ---------------------------------------------------------------- bows

# Per color: loop top, loop bottom, outline, inner fold (the loop's inside), tail top, tail bottom, sheen.
BOWS = {
    "Pink": ("FFC6D5", "EF85A3", "9E3A5A", "D9668A", "F28FAE", "D0607F", "FFE3EB"),
    "SkyBlue": ("C9E8FD", "71ABE0", "2F5E9E", "4F8BCB", "8DBDEB", "5A92CF", "E8F5FF"),
    "Cream": ("FFFCF4", "F0DDBC", "B88B58", "DCC198", "F3E3C6", "DCC29A", "FFFFFF"),
}
LOOP_TILT = 12.0  # the loops rise a little outward, like small wings


def bow_loop(c, side):
    """One loop on its own view (+x outward, +y up): a round body and a narrower neck into the knot, blended."""
    v = View(c, side * 6.0, 6.0, side * LOOP_TILT, side, 1.0)
    body = sd_ellipse(v, 18.5, 17.0, ox=25.0, oy=1.0)
    neck = sd_ellipse(v, 12.0, 8.5, ox=10.0, oy=-1.0)
    return smooth_min(body, neck, 7.0), v


def bow_shapes(c):
    """The bow's pieces in icon units: the two loops (with their views), the knot, the two tails."""
    loops = [bow_loop(c, side) for side in (-1.0, 1.0)]
    knot = sd_ellipse(c, 11.5, 12.5, ox=0.0, oy=4.0)
    tails = (ribbon_tail(c, (-4.0, -2.0), (-20.0, -32.0), 7.5, 6.0), ribbon_tail(c, (4.0, -2.0), (20.0, -32.0), 7.5, 6.0))
    return loops, knot, tails


def icon_bow(color):
    """ICO_Bow_<color>: a ribbon bow, two round loops with their inner fold, a round knot, two short notched tails."""
    c = Canvas(128, 128, cy=60.0)
    top, bottom, line, inner, tail_top, tail_bottom, sheen = BOWS[color]
    loops, knot, tails = bow_shapes(c)
    drop_shadow(c, union(*[l for l, _ in loops], knot, *tails) - WIDTH)
    for k, tail in enumerate(tails):
        edge(c, tail, line)
        c.over(vgrad(c, tail_top, tail_bottom, 0.0, -36.0), fill(tail))
        side = -1.0 if k == 0 else 1.0
        streak = sd_polyline(c, [(side * 6.0, -7.0), (side * 16.5, -26.0)], 1.3)
        c.over(hexrgb(sheen), fill(streak) * fill(tail + 1.5) * 0.55)
        c.over(hexrgb(inner), soft(-tail - 4.0, 4.0) * np.clip((-c.y - 6.0) / 24.0, 0, 1) * fill(tail) * 0.4)
    for loop, v in loops:
        edge(c, loop, line)
        inside = fill(loop)
        c.over(vgrad(c, top, bottom, 30.0, -14.0), inside)
        lower_shade(c, loop, inner, 2.0, 18.0, width=5.0, strength=0.5)
        # The loop's inside, seen through its opening beside the knot: a teardrop fold narrowing outward, its upper
        # lip drawn, its lower part melting into the loop.
        fv = View(v, 12.5, 0.0, -90.0 + 4.0)
        hole = np.maximum(sd_drop(fv, 8.5, 13.5, 0.38), loop + 3.5)
        lip = np.clip((c.y - (6.0 - 0.2 * np.abs(c.x))) / 6.0 + 0.5, 0, 1)
        c.over(hexrgb(line), fill(np.abs(hole) - 0.9) * lip * inside * 0.5)
        c.over(vgrad(c, inner, bottom, 9.0, -6.0), fill(hole) * inside)
        top_rim(c, loop, 10.0, 14.0, 0.35)
    edge(c, knot, line)
    c.over(vgrad(c, top, bottom, 16.0, -8.0), fill(knot))
    lower_shade(c, knot, inner, 1.0, 10.0, width=4.0, strength=0.6)
    # The ribbon wraps the knot: two soft folds following its sides.
    fold = np.abs(sd_ellipse(c, 7.5, 9.5, ox=0.0, oy=4.0)) - 0.8
    c.over(hexrgb(inner), fill(fold) * np.clip((np.abs(c.x) - 4.5) / 2.0, 0, 1) * fill(knot + 1.5) * 0.45)
    gloss(c, 6.0, 3.2, -36.0, 17.0, 26.0, fill(loops[0][0]), 0.7, edge=1.0)
    gloss(c, 4.6, 2.4, 25.0, 17.0, 26.0, fill(loops[1][0]), 0.3, edge=1.0)
    gloss(c, 3.2, 2.0, -4.0, 10.0, 30.0, fill(knot), 0.8)
    return "ICO_Bow_" + color, c, RUNTIME_ICONS


def icon_bow_pink():
    return icon_bow("Pink")


def icon_bow_sky_blue():
    return icon_bow("SkyBlue")


def icon_bow_cream():
    return icon_bow("Cream")


# ---------------------------------------------------------------- golden brush

def brush_shapes(c, k, ox, oy, angle_deg):
    """The grooming brush of ICO_Act_Grooming (same pieces, same proportions) on a view: back, handle, bristle tufts.
    k = 1.14 is the hub icon's size."""
    bv = View(c, ox, oy, angle_deg)
    back = sd_round_rect(bv, 30.0 * k, 10.0 * k, 9.5 * k, ox=-6.0 * k, oy=6.0 * k)
    handle = sd_polyline(bv, [(20.0 * k, 9.0 * k), (46.0 * k, 11.0 * k)], 7.0 * k)
    tufts = sd_round_rect(bv, 27.0 * k, 4.0 * k, 3.0 * k, ox=-6.0 * k, oy=-5.0 * k)
    for i in range(8):
        tufts = smooth_min(tufts, sd_ellipse(bv, 3.7 * k, 8.5 * k, ox=(-30.5 + i * 7.0) * k, oy=-10.0 * k), 1.5 * k)
    return back, handle, tufts, bv


def round_glint(c, r, x, y):
    """A round glint: a bright point of light, white in its heart and gold toward its rim, in a soft warm glow, with
    the family's thin gold edge so it reads on a cream chip. Never a pointed star."""
    dot = sd_circle(c, r, x, y)
    c.over(hexrgb("FFD66A"), soft(dot, r * 1.8) * 0.7)
    c.over(hexrgb("E39A3A"), fill(dot - 1.2))
    c.over(lerp(hexrgb("FFFFF6"), hexrgb("FFC94F"), np.clip(np.hypot(c.x - x, c.y - y) / r, 0, 1) ** 1.6), fill(dot))


def icon_golden_brush():
    """ICO_GoldenBrush: the grooming brush in warm gold (back and handle a deep warm gold, bristles a paler honey
    gold), one soft gloss on the back, two round glints above it. The boost chip of the golden brush."""
    c = Canvas(128, 128, cy=64.0)
    k = 1.12
    back, handle, tufts, bv = brush_shapes(c, k, -9.5, -4.0, -18.0)
    brush = union(back, handle, tufts)
    drop_shadow(c, brush - WIDTH)
    edge(c, brush, "A8662A")
    c.over(lerp(hexrgb("FFF6CC"), hexrgb("F2C35A"), np.clip((0.0 - bv.y) / (18.0 * k), 0, 1)), fill(tufts))
    for i in range(7):
        split = np.abs(bv.x - (-27.0 + i * 7.0) * k) - 0.8
        c.over(hexrgb("D9922E"), fill(split) * fill(tufts) * (bv.y < -3.0 * k) * 0.55)
    gold = lerp(hexrgb("FFE48A"), hexrgb("EFA33A"), np.clip((16.0 * k - bv.y) / (20.0 * k), 0, 1))
    c.over(gold, fill(np.minimum(back, handle)))
    c.over(hexrgb("D9822C"), soft(-back - 4.5, 4.5) * np.clip((4.0 * k - bv.y) / (8.0 * k), 0, 1) * fill(back) * 0.55)
    for (x0, y0), (x1, y1), w, mask, alpha in (((-30.0, 12.5), (14.0, 12.5), 1.4, back, 0.9),
                                               ((24.0, 13.5), (42.0, 14.5), 1.2, handle, 0.75)):
        light = sd_polyline(bv, [(x0 * k, y0 * k), (x1 * k, y1 * k)], w)
        c.over(hexrgb("FFF8D6"), fill(light) * fill(mask) * alpha)
    c.over(hexrgb("A8662A"), fill(np.hypot(bv.x - 41.0 * k, bv.y - 11.0 * k) - 3.0))
    g = sd_ellipse(c, 7.6, 2.9, ox=-28.5, oy=7.0, angle_deg=-18.0)
    c.over((255, 255, 255), soft(g, 1.5) * fill(back) * 0.75)
    round_glint(c, 4.6, 26.0, 31.0)
    round_glint(c, 3.2, 40.0, 21.0)
    return "ICO_GoldenBrush", c, RUNTIME_ICONS


T38_SPRITES = (kit_gesture_swipe, icon_bow_pink, icon_bow_sky_blue, icon_bow_cream, icon_golden_brush)


def main():
    for folder in (RUNTIME_ICONS, RUNTIME_KIT, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T38_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
