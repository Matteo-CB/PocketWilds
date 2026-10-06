"""Pocket Wilds UI sprites added in T34A (feedback and screens of the slice).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down).
- Codex art (Atlas_Codex): rarity frames UI_Frame_* (white, tinted by the rarity token, each with its own motif
  so rarity never depends on color alone), rarity badges ICO_Rarity_* (white, tinted), trait badges ICO_Trait_*
  (own colors, symbol not text).
- Kit (Atlas_CommonHUD): UI_Ring_Soft (tap ripple, landing pulse), UI_Gesture_Tap (tutorial hand),
  UI_Splash_Gradient (boot screen).
- Icons (Atlas_CommonHUD): ICO_Favorite_Heart, ICO_Favorite_HeartOutline, ICO_Info, ICO_Paw, ICO_Unlock, ICO_Warning.

Run through pw_ui_sprites.py (main), which imports this file.
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, ROOT, RUNTIME_ICONS, RUNTIME_KIT, blur, fill, hexrgb, lerp, sd_circle,
                           sd_ellipse, sd_polygon, sd_polyline, sd_round_rect, shift, smooth_min, soft, star_points)

RUNTIME_CODEX = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Codex")  # Atlas_Codex

RARITIES = ("Common", "Uncommon", "Rare", "Epic", "Legendary", "Fabulous", "Secret")


# ---------------------------------------------------------------- shared shapes

def sd_heart(c, size, ox=0.0, oy=0.0):
    """Plump heart of about `size` px wide, centered."""
    s = size / 1.18
    px = np.abs(c.x - ox) / s
    py = (c.y - oy) / s + 0.62
    a = (px - 0.25) ** 2 + (py - 0.75) ** 2
    upper = np.sqrt(a) - math.sqrt(2.0) / 4.0
    m = 0.5 * np.maximum(px + py, 0.0)
    b = np.minimum(px * px + (py - 1.0) ** 2, (px - m) ** 2 + (py - m) ** 2)
    lower = np.sqrt(b) * np.sign(px - py)
    d = np.where(py + px > 1.0, upper, lower)
    return d * s


def sd_paw(c, scale, ox=0.0, oy=0.0):
    pad = sd_ellipse(c, 11.0 * scale, 9.5 * scale, ox=ox, oy=oy - 8.0 * scale)
    toes = None
    for tx, ty, rx, ry, ang in ((-13.0, 6.0, 4.8, 6.0, 20.0), (-4.5, 13.0, 4.8, 6.2, 5.0),
                                (5.0, 13.0, 4.8, 6.2, -5.0), (13.5, 6.0, 4.8, 6.0, -20.0)):
        d = sd_ellipse(c, rx * scale, ry * scale, ox=ox + tx * scale, oy=oy + ty * scale, angle_deg=ang)
        toes = d if toes is None else np.minimum(toes, d)
    return np.minimum(pad, toes)


def sd_cloud(c, scale, ox=0.0, oy=0.0):
    d = sd_circle(c, 9.0 * scale, ox - 9.0 * scale, oy)
    d = smooth_min(d, sd_circle(c, 12.0 * scale, ox + 2.0 * scale, oy + 4.0 * scale), 3.0 * scale)
    d = smooth_min(d, sd_circle(c, 8.5 * scale, ox + 12.0 * scale, oy - 1.0 * scale), 3.0 * scale)
    d = smooth_min(d, sd_round_rect(c, 19.0 * scale, 5.0 * scale, 5.0 * scale, ox=ox + 1.0 * scale, oy=oy - 5.0 * scale), 2.0 * scale)
    return d


def sd_leaf(c, length, width, ox, oy, angle_deg):
    a = math.radians(angle_deg)
    x = (c.x - ox) * math.cos(a) + (c.y - oy) * math.sin(a)
    y = -(c.x - ox) * math.sin(a) + (c.y - oy) * math.cos(a)
    # Vesica: intersection of two circles.
    r = (length * length / 4.0 + width * width) / (2.0 * width)
    d1 = np.hypot(x, y - (width - r)) - r
    d2 = np.hypot(x, y + (width - r)) - r
    return np.maximum(d1, d2)


def sd_diamond(c, rx, ry, ox=0.0, oy=0.0):
    return (np.abs(c.x - ox) / rx + np.abs(c.y - oy) / ry - 1.0) * min(rx, ry) * 0.7


def sd_crown(c, scale, ox=0.0, oy=0.0):
    base = sd_round_rect(c, 16.0 * scale, 4.0 * scale, 2.0 * scale, ox=ox, oy=oy - 6.0 * scale)
    pts = [(-16.0, -4.0), (-16.0, 8.0), (-8.0, 1.0), (0.0, 12.0), (8.0, 1.0), (16.0, 8.0), (16.0, -4.0)]
    body = sd_polygon(c, [(ox + x * scale, oy + y * scale) for x, y in pts]) - 1.2 * scale
    tips = np.minimum(np.minimum(sd_circle(c, 2.8 * scale, ox - 16.0 * scale, oy + 9.0 * scale),
                                 sd_circle(c, 3.2 * scale, ox, oy + 13.5 * scale)),
                      sd_circle(c, 2.8 * scale, ox + 16.0 * scale, oy + 9.0 * scale))
    return np.minimum(np.minimum(base, body), tips)


def motif(c, rarity, scale, ox, oy):
    """Distance field of the motif that identifies a rarity (frame top and badge symbol)."""
    if rarity == "Common":
        return sd_circle(c, 6.0 * scale, ox, oy)
    if rarity == "Uncommon":
        # A sprout: short stem and two leaves opening upward.
        stem = sd_polyline(c, [(ox, oy - 11.0 * scale), (ox, oy + 1.0 * scale)], 2.2 * scale)
        left = sd_leaf(c, 17.0 * scale, 5.5 * scale, ox - 7.0 * scale, oy + 5.0 * scale, -40.0)
        right = sd_leaf(c, 17.0 * scale, 5.5 * scale, ox + 7.0 * scale, oy + 5.0 * scale, 40.0)
        return np.minimum(stem, np.minimum(left, right))
    if rarity == "Rare":
        return sd_diamond(c, 9.0 * scale, 12.0 * scale, ox, oy)
    if rarity == "Epic":
        d = None
        for k, dx in enumerate((-11.0, 0.0, 11.0)):
            h = 12.0 if k == 1 else 8.5
            g = sd_diamond(c, 5.0 * scale, h * scale, ox + dx * scale, oy + (2.0 if k == 1 else -1.0) * scale)
            d = g if d is None else np.minimum(d, g)
        return d
    if rarity == "Legendary":
        return sd_crown(c, 0.95 * scale, ox, oy)
    if rarity == "Fabulous":
        return sd_cloud(c, 0.95 * scale, ox, oy)
    # Secret: a small star with a hollow heart (a hidden thing).
    star = sd_polygon(c, [(ox + x * scale, oy + y * scale) for x, y in star_points(13.0, 6.0)]) - 1.5 * scale
    return np.maximum(star, -(sd_circle(c, 3.2 * scale, ox, oy) ))


# ---------------------------------------------------------------- rarity frames (Atlas_Codex)

def frame(rarity):
    """UI_Frame_<rarity>: 256 x 256 square portrait frame, white for the rarity token. Rounded ring, a soft
    inner lip, and the rarity motif in a medallion on the top edge. Extra cues: Rare, Legendary and Fabulous
    double ring, Epic, Legendary and Fabulous round pearls in the lower corners (never a sparkle star)."""
    size = 256
    c = Canvas(size, size, cy=124.0)
    outer = sd_round_rect(c, 116.0, 116.0, 44.0)
    inner = sd_round_rect(c, 100.0, 100.0, 32.0)
    ring = np.maximum(outer, -inner)
    shadow = blur(shift(fill(ring), 0, 4), 3.0)
    c.over((40, 34, 28), shadow * 0.25)
    t = np.clip((116.0 - c.y) / 232.0, 0, 1)
    c.over(lerp((255, 255, 255), (226, 226, 226), t), fill(ring))
    c.over((200, 200, 200), soft(-ring - 4.0, 4.0) * fill(ring) * np.clip(-c.y / 116.0, 0, 1) * 0.6)
    c.over((255, 255, 255), soft(-ring - 2.0, 1.5) * fill(ring) * np.clip(c.y / 116.0, 0, 1) * 0.9)
    if rarity in ("Rare", "Fabulous", "Legendary"):
        line = np.abs(sd_round_rect(c, 93.0, 93.0, 27.0)) - 2.0
        c.over((236, 236, 236), fill(line) * 0.95)
    if rarity in ("Epic", "Legendary", "Fabulous"):
        for sx, sy in ((-100.0, -100.0), (100.0, -100.0)):
            pearl = sd_circle(c, 8.0, sx, sy)
            c.over((190, 190, 190), fill(pearl - 2.0))
            c.over(lerp((255, 255, 255), (232, 232, 232), np.clip((sy + 8.0 - c.y) / 16.0, 0, 1)), fill(pearl))
            c.over((255, 255, 255), fill(sd_circle(c, 2.6, sx - 2.5, sy + 3.0)))
    # Medallion on the top edge with the motif cut in a slightly darker tone.
    medal = sd_circle(c, 24.0, 0.0, 98.0)
    c.over((40, 34, 28), blur(shift(fill(medal), 0, 3), 2.5) * 0.22)
    c.over(lerp((255, 255, 255), (232, 232, 232), np.clip((122.0 - c.y) / 48.0, 0, 1)), fill(medal))
    c.over((210, 210, 210), fill(np.abs(medal + 3.0) - 1.2) * 0.8)
    m = motif(c, rarity, 0.95, 0.0, 98.0)
    c.over((176, 176, 176), fill(m))
    c.over((150, 150, 150), soft(-m - 2.0, 2.0) * fill(m) * np.clip((98.0 - c.y) / 12.0, 0, 1) * 0.6)
    return "UI_Frame_" + rarity, c, RUNTIME_CODEX


def rarity_badge(rarity):
    """ICO_Rarity_<rarity>: 72 px round badge with the rarity motif, white for the rarity token."""
    size = 72
    c = Canvas(size, size, cy=35.0)
    body = sd_circle(c, 30.0)
    c.over((40, 34, 28), blur(shift(fill(body), 0, 2), 2.0) * 0.28)
    c.over(lerp((255, 255, 255), (228, 228, 228), np.clip((30.0 - c.y) / 60.0, 0, 1)), fill(body))
    c.over((205, 205, 205), fill(np.abs(body + 3.5) - 1.2) * 0.8)
    m = motif(c, rarity, 0.95, 0.0, 0.0)
    c.over((150, 150, 150), fill(m))
    c.over((255, 255, 255), soft(-body - 2.0, 1.5) * fill(body) * np.clip(c.y / 30.0, 0, 1) * 0.8)
    return "ICO_Rarity_" + rarity, c, RUNTIME_CODEX


# ---------------------------------------------------------------- trait badges (Atlas_Codex, own colors)

def trait_badge(name, back, edge, draw):
    size = 96
    c = Canvas(size, size, cy=47.0)
    body = sd_circle(c, 40.0)
    outline = body - 4.0
    c.over((50, 40, 30), blur(shift(fill(outline), 0, 3), 2.5) * 0.28)
    c.over(hexrgb(edge), fill(outline))
    c.over(lerp(hexrgb(back), hexrgb(edge), np.clip((40.0 - c.y) / 80.0, 0, 1) * 0.35), fill(body))
    draw(c)
    c.over((255, 255, 255), soft(-body - 2.5, 2.0) * fill(body) * np.clip(c.y / 40.0, 0, 1) * 0.55)
    return "ICO_Trait_" + name, c, RUNTIME_CODEX


def trait_tiny():
    def draw(c):
        c.over(hexrgb("FFFFFF"), fill(sd_paw(c, 0.62, 0.0, -2.0)))
        for ang in (45.0, 135.0, 225.0, 315.0):
            a = math.radians(ang)
            x0, y0 = math.cos(a) * 30.0, math.sin(a) * 30.0
            x1, y1 = math.cos(a) * 21.0, math.sin(a) * 21.0
            c.over(hexrgb("FFFFFF"), fill(sd_polyline(c, [(x0, y0), (x1, y1)], 2.6)) * 0.9)
    return trait_badge("Tiny", "B9A6E8", "7D64B8", draw)


def trait_giant():
    def draw(c):
        c.over(hexrgb("FFFFFF"), fill(sd_paw(c, 1.25, 0.0, 0.0)))
    return trait_badge("Giant", "F6B26B", "C77A2E", draw)


def trait_shiny():
    """Shiny: a glossy pearl with a bright sheen (owner rule: no sparkle star)."""
    def draw(c):
        pearl = sd_circle(c, 22.0, 0.0, -2.0)
        c.over(hexrgb("2F6E9E"), fill(pearl - 3.0))
        c.over(lerp(hexrgb("FFFFFF"), hexrgb("CFE9FA"), np.clip((20.0 - c.y) / 44.0, 0, 1)), fill(pearl))
        sheen = np.abs(sd_circle(c, 14.0, -3.0, 2.0)) - 2.6
        arc = (c.x < -2.0) & (c.y > 0.0)
        c.over((255, 255, 255), fill(sheen) * arc * fill(pearl))
        c.over((255, 255, 255), fill(sd_circle(c, 4.5, -9.0, 9.0)))
    return trait_badge("Shiny", "8EC8F2", "4D8CC2", draw)


def trait_golden():
    def draw(c):
        star = sd_polygon(c, star_points(27.0, 13.0, oy=-1.0)) - 3.0
        c.over(hexrgb("B8741E"), fill(star - 2.5))
        c.over(lerp(hexrgb("FFF0A8"), hexrgb("F7B23B"), np.clip((26.0 - c.y) / 52.0, 0, 1)), fill(star))
        c.over((255, 255, 255), fill(sd_ellipse(c, 4.5, 2.6, ox=-5.0, oy=9.0, angle_deg=30.0)) * 0.8)
    return trait_badge("Golden", "FFE3A0", "D6992F", draw)


def trait_albino():
    def draw(c):
        c.over(hexrgb("FFFFFF"), fill(sd_circle(c, 22.0)))
        c.over(hexrgb("F28CA8"), fill(sd_circle(c, 7.5, 0.0, 1.0)))
        c.over((255, 255, 255), fill(sd_circle(c, 2.5, -2.5, 4.0)))
    return trait_badge("Albino", "F7E6EE", "C79AAE", draw)


def trait_melanistic():
    def draw(c):
        moon = np.maximum(sd_circle(c, 22.0), -sd_circle(c, 19.0, 10.0, 8.0))
        c.over(hexrgb("FFF3C4"), fill(moon))
        c.over(hexrgb("FFF3C4"), fill(sd_circle(c, 3.2, -15.0, 16.0)))
        c.over(hexrgb("FFF3C4"), fill(sd_circle(c, 2.2, -22.0, 4.0)))
    return trait_badge("Melanistic", "5E5A86", "36325C", draw)


# ---------------------------------------------------------------- kit (Atlas_CommonHUD, tinted)

def kit_ring_soft():
    """UI_Ring_Soft: 128 px soft ring, white for tinting (tap ripple under the tutorial hand, mote landing pulse)."""
    c = Canvas(128, 128)
    r = np.hypot(c.x, c.y)
    alpha = np.exp(-((r - 50.0) / 7.0) ** 2) + np.exp(-((r - 42.0) / 14.0) ** 2) * 0.25
    c.over((255, 255, 255), np.clip(alpha, 0, 1))
    return "UI_Ring_Soft", c, RUNTIME_KIT


def kit_gesture_tap():
    """UI_Gesture_Tap: 192 px plush paw, cream with pink pads and a brown outline, slightly tilted. It presses on
    the target while a soft ring spreads (TapHint). Its pad center is the touch point (sprite center)."""
    size = 192
    c = Canvas(size, size)
    pad = sd_ellipse(c, 34.0, 29.0, ox=0.0, oy=-14.0, angle_deg=-8.0)
    toes = None
    for tx, ty, rx, ry, ang in ((-40.0, 22.0, 13.0, 16.0, 24.0), (-14.0, 42.0, 13.0, 17.0, 8.0),
                                (15.0, 42.0, 13.0, 17.0, -8.0), (40.0, 22.0, 13.0, 16.0, -24.0)):
        d = sd_ellipse(c, rx, ry, ox=tx, oy=ty, angle_deg=ang)
        toes = d if toes is None else np.minimum(toes, d)
    paw = np.minimum(pad, toes)
    outline = paw - 5.5
    c.over((50, 40, 30), blur(shift(fill(outline), 0, 6), 5.0) * 0.32)
    c.over(hexrgb("6B4E3A"), fill(outline))
    c.over(lerp(hexrgb("FFFFFF"), hexrgb("F2E8DA"), np.clip((50.0 - c.y) / 100.0, 0, 1)), fill(paw))
    inner_pad = sd_ellipse(c, 22.0, 17.0, ox=0.0, oy=-16.0, angle_deg=-8.0)
    c.over(hexrgb("F7B3C2"), fill(inner_pad))
    for tx, ty, ang in ((-40.0, 22.0, 24.0), (-14.0, 42.0, 8.0), (15.0, 42.0, -8.0), (40.0, 22.0, -24.0)):
        c.over(hexrgb("F7B3C2"), fill(sd_ellipse(c, 6.5, 8.0, ox=tx, oy=ty - 2.0, angle_deg=ang)))
    c.over((255, 255, 255), fill(sd_ellipse(c, 7.0, 4.0, ox=-10.0, oy=-6.0, angle_deg=20.0)) * 0.7)
    return "UI_Gesture_Tap", c, RUNTIME_KIT


def kit_splash_gradient():
    """UI_Splash_Gradient: 64 x 512 soft sky-to-meadow gradient for the boot screen (stretched, own colors)."""
    c = Canvas(64, 512)
    t = np.clip((c.y + 256.0) / 512.0, 0, 1)
    top, mid, low = hexrgb("BFE3F5"), hexrgb("FFF6E2"), hexrgb("CFE8B2")
    upper = lerp(mid, top, np.clip((t - 0.45) / 0.55, 0, 1))
    lower = lerp(low, mid, np.clip(t / 0.45, 0, 1))
    color = np.where((t > 0.45)[..., None], upper, lower)
    c.over(color, np.ones_like(t))
    return "UI_Splash_Gradient", c, RUNTIME_KIT


# ---------------------------------------------------------------- icons (Atlas_CommonHUD)

def icon_heart():
    """ICO_Favorite_Heart: plump pink heart, favorite on."""
    c = Canvas(128, 128, cy=62.0)
    heart = sd_heart(c, 92.0, oy=2.0)
    outline = heart - 5.0
    c.over((90, 30, 50), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("C24F72"), fill(outline))
    c.over(lerp(hexrgb("FFA3BE"), hexrgb("F0668E"), np.clip((40.0 - c.y) / 80.0, 0, 1)), fill(heart))
    c.over(hexrgb("E0517A"), soft(-heart - 6.0, 6.0) * np.clip(-c.y / 40.0, 0, 1) * fill(heart) * 0.6)
    c.over((255, 255, 255), fill(sd_ellipse(c, 9.0, 5.5, ox=-18.0, oy=20.0, angle_deg=35.0)) * 0.85)
    return "ICO_Favorite_Heart", c, RUNTIME_ICONS


def icon_heart_outline():
    """ICO_Favorite_HeartOutline: the same heart, outline only (favorite off). White for tinting."""
    c = Canvas(128, 128, cy=62.0)
    heart = sd_heart(c, 92.0, oy=2.0)
    ring = np.abs(heart + 1.0) - 5.5
    c.over((255, 255, 255), fill(ring))
    return "ICO_Favorite_HeartOutline", c, RUNTIME_ICONS


def icon_info():
    """ICO_Info: 96 px soft "i" in a ring, white for tinting (egg odds button)."""
    c = Canvas(96, 96)
    ring = np.abs(sd_circle(c, 36.0)) - 5.0
    dot = sd_circle(c, 6.5, 0.0, 17.0)
    stem = sd_round_rect(c, 5.5, 15.0, 5.5, oy=-9.0)
    c.over((255, 255, 255), fill(np.minimum(ring, np.minimum(dot, stem))))
    return "ICO_Info", c, RUNTIME_ICONS


def icon_paw():
    """ICO_Paw: 96 px paw print, white for tinting (loading dots, small accents)."""
    c = Canvas(96, 96, cy=46.0)
    paw = sd_paw(c, 1.65, 0.0, 0.0)
    c.over((255, 255, 255), fill(paw))
    return "ICO_Paw", c, RUNTIME_ICONS


def icon_unlock():
    """ICO_Unlock: open padlock. Gold body with a keyhole, round steel shackle lifted on its right leg."""
    c = Canvas(128, 128, cy=60.0)
    body = sd_round_rect(c, 36.0, 26.0, 10.0, oy=-28.0)
    # Shackle: a stroke along a U (two legs and a half circle). Left leg enters the body, right leg ends 12 px above it.
    r, cx0, top = 17.0, -8.0, 38.0
    legs = np.minimum(sd_round_rect(c, 0.01, 17.0, 0.0, ox=cx0 - r, oy=top - 17.0),
                      sd_round_rect(c, 0.01, 2.0, 0.0, ox=cx0 + r, oy=top - 2.0))
    arc = np.where(c.y >= top, np.abs(np.hypot(c.x - cx0, c.y - top) - r), 1e3)
    stroke = np.minimum(legs, arc) - 6.0
    outline = np.minimum(body - 4.5, stroke - 4.0)
    c.over((80, 52, 20), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("8E5A24"), fill(outline))
    c.over(lerp(hexrgb("F4F1EA"), hexrgb("C9C2B4"), np.clip((top + r - c.y) / 50.0, 0, 1)), fill(stroke))
    c.over((255, 255, 255), fill(np.where(c.x < cx0 - r, np.abs(c.x - (cx0 - r - 2.0)) - 1.2, 1e3)) * fill(stroke) * 0.7)
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F2B544"), np.clip((-2.0 - c.y) / 52.0, 0, 1)), fill(body))
    c.over(hexrgb("E39A2E"), soft(-body - 5.0, 5.0) * np.clip((-28.0 - c.y) / 26.0, 0, 1) * fill(body) * 0.7)
    hole = np.minimum(sd_circle(c, 7.0, 0.0, -24.0), sd_round_rect(c, 3.0, 8.0, 3.0, oy=-34.0))
    c.over(hexrgb("8E5A24"), fill(hole))
    c.over((255, 255, 255), fill(sd_ellipse(c, 7.0, 3.2, ox=-18.0, oy=-12.0, angle_deg=15.0)) * 0.7)
    return "ICO_Unlock", c, RUNTIME_ICONS


def icon_warning():
    """ICO_Warning: soft rounded triangle with a "!" (warningSoft yellow, never red)."""
    c = Canvas(128, 128, cy=60.0)
    tri = sd_polygon(c, [(0.0, 46.0), (48.0, -36.0), (-48.0, -36.0)]) - 8.0
    outline = tri - 5.0
    c.over((80, 60, 20), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("B98A2A"), fill(outline))
    c.over(lerp(hexrgb("FFE9A6"), hexrgb("F2C14E"), np.clip((40.0 - c.y) / 80.0, 0, 1)), fill(tri))
    bar = sd_round_rect(c, 6.0, 15.0, 6.0, oy=4.0)
    dot = sd_circle(c, 6.5, 0.0, -24.0)
    c.over(hexrgb("6B4E1E"), fill(np.minimum(bar, dot)))
    return "ICO_Warning", c, RUNTIME_ICONS


T34A_SPRITES = tuple((lambda r: (lambda: frame(r)))(r) for r in RARITIES) + \
    tuple((lambda r: (lambda: rarity_badge(r)))(r) for r in RARITIES) + \
    (trait_tiny, trait_giant, trait_shiny, trait_golden, trait_albino, trait_melanistic,
     kit_ring_soft, kit_gesture_tap, kit_splash_gradient,
     icon_heart, icon_heart_outline, icon_info, icon_paw, icon_unlock, icon_warning)
