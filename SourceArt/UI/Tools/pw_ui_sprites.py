"""Pocket Wilds UI sprite generator (I-KIT core shapes + HUD icons).

Vector-like source: every sprite is described here by signed distance shapes, rendered at 4x and
box-filtered down (clean anti-aliasing), so any size or tweak is a re-run, never a repaint.
Shape kit sprites are white/grey so color tokens tint them at runtime (no recolored copies).
Icons carry their own colors (filled/rounded hybrid, readable at 24-32 dp).

Usage: python SourceArt/UI/Tools/pw_ui_sprites.py
Writes runtime PNGs into Assets/_Game/Art/UI/{Kit,Icons}/ and 4x masters into SourceArt/UI/Masters/.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SS = 4
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
RUNTIME_KIT = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Kit")
RUNTIME_ICONS = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Icons")
RUNTIME_SHOP = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Shop")  # Atlas_Shop: shop-only art
MASTERS = os.path.join(ROOT, "SourceArt", "UI", "Masters")


# ---------------------------------------------------------------- canvas helpers

class Canvas:
    """Premultiplied RGBA float canvas at SS x resolution. Coordinates are output pixels, y up,
    origin at (cx, cy) measured from the top-left corner."""

    def __init__(self, width, height, cx=None, cy=None):
        self.w, self.h = width, height
        self.rgba = np.zeros((height * SS, width * SS, 4), dtype=np.float64)
        ys, xs = np.mgrid[0:height * SS, 0:width * SS].astype(np.float64)
        cx = width * 0.5 if cx is None else cx
        cy = height * 0.5 if cy is None else cy
        self.x = (xs + 0.5) / SS - cx
        self.y = cy - (ys + 0.5) / SS

    def over(self, color, alpha):
        """Composite a straight color (rgb tuple 0-255 or HxWx3 array 0-1) with coverage alpha."""
        if isinstance(color, tuple):
            color = np.array(color[:3], dtype=np.float64) / 255.0
        a = np.clip(alpha, 0.0, 1.0)[..., None]
        self.rgba[..., :3] = color * a + self.rgba[..., :3] * (1.0 - a)
        self.rgba[..., 3:] = a + self.rgba[..., 3:] * (1.0 - a)

    def image(self, downsample=True):
        data = self.rgba
        if downsample:
            data = data.reshape(self.h, SS, self.w, SS, 4).mean(axis=(1, 3))
        alpha = data[..., 3:]
        rgb = np.where(alpha > 1e-6, data[..., :3] / np.maximum(alpha, 1e-6), 0.0)
        out = np.concatenate([rgb, alpha], axis=-1)
        return Image.fromarray(np.round(np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")


def fill(d):
    """Coverage of a signed distance (output px), one sub-pixel wide edge."""
    return np.clip(0.5 - d * SS, 0.0, 1.0)


def soft(d, width):
    """Smooth falloff over `width` output pixels outside the shape (glows, inner bands)."""
    t = np.clip(0.5 - d / max(width, 1e-6), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(alpha, radius_px):
    """Gaussian blur of a coverage mask (radius in output pixels)."""
    img = Image.fromarray(np.round(np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L")
    img = img.filter(ImageFilter.GaussianBlur(radius_px * SS))
    return np.asarray(img, dtype=np.float64) / 255.0


def shift(alpha, dx_px, dy_px):
    """Move a mask by output pixels (dy positive = down on screen)."""
    out = np.zeros_like(alpha)
    sx, sy = int(round(dx_px * SS)), int(round(dy_px * SS))
    h, w = alpha.shape
    ys0, ys1 = max(0, sy), min(h, h + sy)
    xs0, xs1 = max(0, sx), min(w, w + sx)
    out[ys0:ys1, xs0:xs1] = alpha[ys0 - sy:ys1 - sy, xs0 - sx:xs1 - sx]
    return out


def normalize(f):
    """Turn an implicit function into an approximate distance near its zero set."""
    gy, gx = np.gradient(f)
    grad = np.sqrt(gx * gx + gy * gy) * SS
    return f / np.maximum(grad, 1e-6)


def lerp(a, b, t):
    a = np.array(a, dtype=np.float64) / 255.0
    b = np.array(b, dtype=np.float64) / 255.0
    t = np.clip(t, 0.0, 1.0)[..., None]
    return a + (b - a) * t


def hexrgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


# ---------------------------------------------------------------- shapes

def sd_circle(c, r, ox=0.0, oy=0.0):
    return np.hypot(c.x - ox, c.y - oy) - r


def sd_round_rect(c, hw, hh, r, ox=0.0, oy=0.0):
    qx = np.abs(c.x - ox) - hw + r
    qy = np.abs(c.y - oy) - hh + r
    outside = np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0))
    inside = np.minimum(np.maximum(qx, qy), 0.0)
    return outside + inside - r


def sd_ellipse(c, rx, ry, ox=0.0, oy=0.0, angle_deg=0.0):
    a = math.radians(angle_deg)
    x, y = c.x - ox, c.y - oy
    u = x * math.cos(a) + y * math.sin(a)
    v = -x * math.sin(a) + y * math.cos(a)
    return normalize(np.sqrt((u / rx) ** 2 + (v / ry) ** 2) - 1.0)


def sd_egg(c, half_width, half_height, taper, oy=0.0):
    y = c.y - oy
    width = half_width * (1.0 - taper * y / half_height)
    return normalize(np.sqrt((c.x / width) ** 2 + (y / half_height) ** 2) - 1.0)


def smooth_min(a, b, k):
    """Polynomial smooth union: blends two shapes with a soft fillet of about k pixels."""
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b + (a - b) * h - k * h * (1.0 - h)


def sd_polyline(c, points, half_width):
    d = np.full(c.x.shape, 1e9)
    for (ax, ay), (bx, by) in zip(points[:-1], points[1:]):
        px, py = c.x - ax, c.y - ay
        ex, ey = bx - ax, by - ay
        t = np.clip((px * ex + py * ey) / (ex * ex + ey * ey), 0.0, 1.0)
        d = np.minimum(d, np.hypot(px - ex * t, py - ey * t))
    return d - half_width


def sd_polygon(c, points):
    """Exact signed distance to a closed polygon (segment distance, sign from a raster fill).
    Exact distances keep offsets (outlines, rounded tips) even on thin points."""
    d = np.full(c.x.shape, 1e9)
    count = len(points)
    for i in range(count):
        ax, ay = points[i]
        bx, by = points[(i + 1) % count]
        px, py = c.x - ax, c.y - ay
        ex, ey = bx - ax, by - ay
        t = np.clip((px * ex + py * ey) / (ex * ex + ey * ey), 0.0, 1.0)
        d = np.minimum(d, np.hypot(px - ex * t, py - ey * t))
    h, w = c.x.shape
    x0 = c.x[0, 0] - 0.5 / SS
    y0 = c.y[0, 0] + 0.5 / SS
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon([((x - x0) * SS, (y0 - y) * SS) for x, y in points], fill=255)
    return np.where(np.asarray(mask) > 127, -d, d)


def superellipse_points(radius, exponent, oy=0.0, count=480):
    """|x|^p + |y|^p = r^p. p < 1 gives a four-point star with concave sides."""
    e = 2.0 / exponent
    points = []
    for i in range(count):
        t = 2.0 * math.pi * i / count
        ct, st = math.cos(t), math.sin(t)
        points.append((math.copysign(abs(ct) ** e, ct) * radius, math.copysign(abs(st) ** e, st) * radius + oy))
    return points


# ---------------------------------------------------------------- shape kit (tinted at runtime)

def kit_pill():
    """UI_Pill_9Slice: 132x136, pill body 120x116, cheap baked drop shadow below.
    Borders (L, B, R, T) = (64, 70, 64, 62), 4 px stretch in the middle."""
    w, h = 132, 136
    c = Canvas(w, h, cx=66.0, cy=62.0)
    body = sd_round_rect(c, 60.0, 58.0, 58.0)
    shadow = blur(shift(fill(body), 0, 5), 4.0)
    c.over((40, 52, 44), shadow * 0.26)
    t = np.clip((58.0 - c.y) / 116.0, 0, 1)
    base = lerp((255, 255, 255), (234, 234, 234), t)
    c.over(base, fill(body))
    # Soft lower bevel inside the edge: plush volume without extra sprites.
    bevel = soft(-body - 7.0, 7.0) * np.clip(-c.y / 58.0, 0, 1)
    c.over((214, 214, 214), bevel * fill(body) * 0.8)
    rim = soft(-body - 2.0, 1.5) * fill(body)
    c.over((246, 246, 246), rim * np.clip(c.y / 58.0, 0, 1))
    return "UI_Pill_9Slice", c, RUNTIME_KIT


def kit_icon_circle():
    """UI_Button_IconCircle: 256x256 round action button, white base for color-token tinting."""
    w, h = 256, 256
    c = Canvas(w, h, cx=128.0, cy=122.0)
    body = sd_circle(c, 112.0)
    shadow = blur(shift(fill(body), 0, 9), 7.0)
    c.over((36, 46, 38), shadow * 0.30)
    t = np.clip((112.0 - c.y) / 224.0, 0, 1)
    c.over(lerp((255, 255, 255), (226, 226, 226), t), fill(body))
    bevel = soft(-body - 14.0, 14.0) * np.clip(-c.y / 112.0, 0, 1) ** 1.2
    c.over((196, 196, 196), bevel * fill(body) * 0.85)
    ring = soft(-body - 3.0, 2.0) * fill(body)
    c.over((214, 214, 214), ring * 0.6)
    # Inner dome: a slightly lighter disc that reads as a soft, pressable cushion.
    dome = sd_circle(c, 92.0, oy=6.0)
    c.over((255, 255, 255), soft(dome, 10.0) * 0.55)
    return "UI_Button_IconCircle", c, RUNTIME_KIT


def kit_panel_primary():
    """UI_Panel_Primary_9Slice: 160x168, body 144x144 with 56 px corners, cheap baked shadow.
    Borders (L, B, R, T) = (64, 74, 64, 62). Near-opaque cream once tinted (backgroundLight)."""
    w, h = 160, 168
    c = Canvas(w, h, cx=80.0, cy=78.0)
    body = sd_round_rect(c, 72.0, 72.0, 56.0)
    shadow = blur(shift(fill(body), 0, 8), 6.0)
    c.over((34, 44, 38), shadow * 0.30)
    t = np.clip((72.0 - c.y) / 144.0, 0, 1)
    c.over(lerp((255, 255, 255), (243, 243, 243), t), fill(body))
    bevel = soft(-body - 9.0, 9.0) * np.clip(-c.y / 72.0, 0, 1)
    c.over((222, 222, 222), bevel * fill(body) * 0.7)
    rim = soft(-body - 2.5, 2.0) * fill(body)
    c.over((255, 255, 255), rim * np.clip(c.y / 72.0, 0, 1) * 0.9)
    return "UI_Panel_Primary_9Slice", c, RUNTIME_KIT


def kit_button_primary():
    """UI_Button_Primary_9Slice: 168x176, body 152x136 with 64 px corners, deeper bevel than panels
    so it reads pressable. Borders (L, B, R, T) = (72, 98, 72, 70)."""
    w, h = 168, 176
    c = Canvas(w, h, cx=84.0, cy=74.0)
    body = sd_round_rect(c, 76.0, 68.0, 64.0)
    shadow = blur(shift(fill(body), 0, 9), 6.0)
    c.over((36, 46, 38), shadow * 0.32)
    t = np.clip((68.0 - c.y) / 136.0, 0, 1)
    c.over(lerp((255, 255, 255), (228, 228, 228), t), fill(body))
    bevel = soft(-body - 13.0, 13.0) * np.clip(-c.y / 68.0, 0, 1) ** 1.1
    c.over((196, 196, 196), bevel * fill(body) * 0.85)
    ring = soft(-body - 3.0, 2.0) * fill(body)
    c.over((214, 214, 214), ring * 0.55)
    top = soft(-body - 3.5, 3.0) * fill(body) * np.clip(c.y / 68.0, 0, 1)
    c.over((255, 255, 255), top * 0.9)
    return "UI_Button_Primary_9Slice", c, RUNTIME_KIT


def kit_glow():
    """UI_Glow_Soft: 128x128 radial glow (white, tinted at runtime). Cheap glow behind rewards."""
    size = 128
    c = Canvas(size, size)
    r = np.hypot(c.x, c.y) / 62.0
    alpha = np.clip(1.0 - r, 0.0, 1.0) ** 1.4
    c.over((255, 255, 255), alpha)
    return "UI_Glow_Soft", c, RUNTIME_KIT


# ---------------------------------------------------------------- icons (own colors)

def icon_light():
    """ICO_Currency_Light: a warm light orb in a ring of eight round rays (a small plush sun).
    Owner rule 2026-10-06: no four-point sparkle star anywhere."""
    size = 128
    c = Canvas(size, size, cy=63.0)
    rays = None
    for k in range(8):
        a = math.tau * k / 8.0 + math.pi / 8.0
        d = sd_ellipse(c, 7.5, 11.0, ox=math.cos(a) * 41.0, oy=math.sin(a) * 41.0, angle_deg=math.degrees(a) + 90.0)
        rays = d if rays is None else np.minimum(rays, d)
    orb = sd_circle(c, 30.0)
    shape = np.minimum(orb, rays)
    outline = shape - 4.5
    shadow = blur(shift(fill(outline), 0, 4), 3.0)
    c.over((92, 58, 18), shadow * 0.28)
    c.over(hexrgb("E0892C"), fill(outline))
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F7B23B"), np.clip((50.0 - c.y) / 100.0, 0, 1)), fill(rays))
    t = np.clip((30.0 - c.y) / 60.0, 0, 1)
    c.over(lerp(hexrgb("FFF6C8"), hexrgb("FFC23A"), t), fill(orb))
    c.over(hexrgb("F5A532"), soft(-orb - 6.0, 6.0) * np.clip(-c.y / 30.0, 0, 1) * fill(orb) * 0.8)
    core = sd_circle(c, 14.0, oy=4.0)
    c.over(hexrgb("FFFDF0"), soft(core, 10.0) * fill(orb) * 0.85)
    gloss = sd_ellipse(c, 7.0, 4.2, ox=-10.0, oy=14.0, angle_deg=35.0)
    c.over((255, 255, 255), fill(gloss) * 0.8)
    return "ICO_Currency_Light", c, RUNTIME_ICONS


def icon_egg():
    """ICO_Hatch_Egg: cream egg with the Grassland zigzag band and a first hairline crack."""
    size = 160
    c = Canvas(size, size, cy=78.0)
    egg = sd_egg(c, 50.0, 64.0, 0.16, oy=2.0)
    outline = egg - 5.0
    shadow = blur(shift(fill(outline), 0, 5), 4.0)
    c.over((70, 48, 30), shadow * 0.26)
    c.over(hexrgb("8A5E3F"), fill(outline))
    t = np.clip((66.0 - c.y) / 128.0, 0, 1)
    c.over(lerp(hexrgb("FFF9EC"), hexrgb("F0DCB8"), t), fill(egg))

    # Zigzag band (triangle wave) across the belly.
    period, amplitude = 24.0, 6.5
    phase = (c.x / period) % 1.0
    wave = (np.abs(phase - 0.5) * 4.0 - 1.0) * amplitude
    band_center = -10.0 + wave
    band = np.abs(c.y - band_center) - 8.5
    inside = fill(egg)
    c.over(hexrgb("7FBC5E"), fill(band) * inside)
    lower_band = np.abs(c.y - (band_center - 6.0)) - 2.5
    c.over(hexrgb("629E48"), fill(lower_band) * fill(band) * inside * 0.8)
    for ox, oy, r in ((-22.0, 26.0, 6.0), (16.0, 34.0, 5.0), (-4.0, 50.0, 4.0), (26.0, 12.0, 4.0)):
        c.over(hexrgb("92C66A"), fill(sd_circle(c, r, ox, oy)) * inside)

    # Inner volume: warm bottom shade, then a hairline crack near the top (the egg is ready).
    lower = soft(-egg - 9.0, 9.0) * np.clip(-c.y / 60.0, 0, 1)
    c.over(hexrgb("D9BF95"), lower * inside * 0.75)
    crack = sd_polyline(c, [(6.0, 56.0), (12.0, 47.0), (7.0, 40.0), (16.0, 31.0), (13.0, 25.0)], 1.6)
    c.over(hexrgb("7A5236"), fill(crack) * inside)
    gloss = sd_ellipse(c, 8.0, 15.0, ox=-24.0, oy=30.0, angle_deg=-28.0)
    c.over((255, 255, 255), soft(gloss, 3.0) * inside * 0.8)
    return "ICO_Hatch_Egg", c, RUNTIME_ICONS


def kit_dot():
    """UI_Dot: 48x48 round dot (notification, page markers). White for tinting, soft lower bevel."""
    size = 48
    c = Canvas(size, size, cy=24.5)
    body = sd_circle(c, 19.0)
    c.over((40, 40, 40), blur(shift(fill(body), 0, 2), 1.5) * 0.25)
    c.over(lerp((255, 255, 255), (232, 232, 232), np.clip((19.0 - c.y) / 38.0, 0, 1)), fill(body))
    c.over((255, 255, 255), soft(-body - 2.0, 1.5) * fill(body) * np.clip(c.y / 19.0, 0, 1))
    return "UI_Dot", c, RUNTIME_KIT


def star_points(outer, inner, count=5, oy=0.0, rotation_deg=90.0):
    points = []
    for i in range(count * 2):
        radius = outer if i % 2 == 0 else inner
        angle = math.radians(rotation_deg) + math.pi * i / count
        points.append((math.cos(angle) * radius, math.sin(angle) * radius + oy))
    return points


def icon_rarity_star():
    """ICO_Rarity_Star: 64 px rounded five-point star, white for rarity tinting (shape, not color alone)."""
    size = 64
    c = Canvas(size, size, cy=31.0)
    star = sd_polygon(c, star_points(25.0, 11.5, oy=-1.0)) - 3.0
    c.over((40, 34, 28), blur(shift(fill(star - 2.5), 0, 2), 1.5) * 0.3)
    c.over((214, 214, 214), fill(star - 2.5))
    c.over(lerp((255, 255, 255), (238, 238, 238), np.clip((24.0 - c.y) / 48.0, 0, 1)), fill(star))
    gloss = sd_ellipse(c, 4.0, 2.4, ox=-4.0, oy=8.0, angle_deg=25.0)
    c.over((255, 255, 255), fill(gloss) * fill(star))
    return "ICO_Rarity_Star", c, RUNTIME_ICONS


def icon_back():
    """ICO_Back_Chevron: 96 px soft chevron (round caps), white for tinting."""
    size = 96
    c = Canvas(size, size)
    chevron = sd_polyline(c, [(10.0, 26.0), (-14.0, 0.0), (10.0, -26.0)], 8.0)
    c.over((255, 255, 255), fill(chevron))
    return "ICO_Back_Chevron", c, RUNTIME_ICONS


def icon_codex():
    """ICO_Codex_Book: closed storybook with a paw print, warm green cover, cream pages (I-HUD-005)."""
    size = 128
    c = Canvas(size, size, cy=63.0)
    cover = sd_round_rect(c, 38.0, 46.0, 12.0, ox=-2.0)
    pages = sd_round_rect(c, 36.0, 42.0, 10.0, ox=6.0, oy=-2.0)
    outline = np.minimum(cover, pages) - 5.0
    c.over((60, 44, 28), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("5B4232"), fill(outline))
    c.over(hexrgb("FFF6E2"), fill(pages))
    for k in range(3):
        line = np.abs(c.x - (35.0 + k * 3.2)) - 0.7
        c.over(hexrgb("E3D2B2"), fill(line) * fill(pages) * (c.y < 36.0) * (c.y > -40.0))
    t = np.clip((46.0 - c.y) / 92.0, 0, 1)
    c.over(lerp(hexrgb("8FCB6A"), hexrgb("5E9E4A"), t), fill(cover))
    spine = sd_round_rect(c, 6.0, 46.0, 4.0, ox=-36.0)
    c.over(hexrgb("4F8A3F"), fill(spine) * fill(cover))
    c.over(hexrgb("A9DB86"), soft(-cover - 3.0, 2.0) * fill(cover) * np.clip(c.y / 46.0, 0, 1) * 0.8)
    # Paw print on the cover.
    paw = "FFF4D6"
    c.over(hexrgb(paw), fill(sd_ellipse(c, 11.0, 9.5, ox=0.0, oy=-8.0)))
    for ox, oy, rx, ry, ang in ((-13.0, 6.0, 4.6, 5.8, 20.0), (-4.5, 13.0, 4.6, 6.0, 5.0),
                                (5.5, 13.0, 4.6, 6.0, -5.0), (14.0, 6.0, 4.6, 5.8, -20.0)):
        c.over(hexrgb(paw), fill(sd_ellipse(c, rx, ry, ox=ox, oy=oy, angle_deg=ang)))
    gloss = sd_ellipse(c, 5.0, 12.0, ox=-24.0, oy=24.0, angle_deg=-10.0)
    c.over((255, 255, 255), soft(gloss, 2.5) * fill(cover) * 0.35)
    return "ICO_Codex_Book", c, RUNTIME_ICONS


def icon_mastery():
    """ICO_Mastery_StarPaw (I-HUD-009): plump golden star carrying a paw print. Species mastery."""
    size = 128
    c = Canvas(size, size, cy=62.0)
    star = sd_polygon(c, star_points(52.0, 27.0, oy=-2.0)) - 7.0
    outline = star - 5.0
    c.over((80, 52, 20), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("C9782A"), fill(outline))
    t = np.clip((50.0 - c.y) / 100.0, 0, 1)
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F7B23B"), t), fill(star))
    c.over(hexrgb("E99A30"), soft(-star - 6.0, 6.0) * np.clip(-c.y / 40.0, 0, 1) * fill(star) * 0.8)
    paw = "8A5A2E"
    c.over(hexrgb(paw), fill(sd_ellipse(c, 10.5, 9.0, ox=0.0, oy=-9.0)))
    for ox, oy, rx, ry, ang in ((-12.5, 4.0, 4.2, 5.4, 20.0), (-4.2, 10.5, 4.2, 5.6, 5.0),
                                (4.8, 10.5, 4.2, 5.6, -5.0), (13.0, 4.0, 4.2, 5.4, -20.0)):
        c.over(hexrgb(paw), fill(sd_ellipse(c, rx, ry, ox=ox, oy=oy, angle_deg=ang)))
    gloss = sd_ellipse(c, 6.0, 3.4, ox=-10.0, oy=22.0, angle_deg=30.0)
    c.over((255, 255, 255), fill(gloss) * fill(star) * 0.7)
    return "ICO_Mastery_StarPaw", c, RUNTIME_ICONS


# ---------------------------------------------------------------- shop icons (T28)

def draw_gem(c, ox, oy, s, shadow=True):
    """One rounded brilliant-cut gem centered on (ox, oy), s = scale (1 = 88 px wide)."""
    u = 44.0 * s
    pts = [(-0.55, 0.62), (0.55, 0.62), (0.95, 0.18), (0.0, -0.92), (-0.95, 0.18)]
    body = sd_polygon(c, [(ox + x * u, oy + y * u) for x, y in pts]) - 3.0 * s
    outline = body - 4.0 * s
    if shadow:
        c.over((30, 40, 72), blur(shift(fill(outline), 0, 3.0 * s), 2.5 * s) * 0.28)
    c.over(hexrgb("3B5FAE"), fill(outline))
    inside = fill(body)
    t = np.clip(((oy + 0.62 * u) - c.y) / (1.54 * u), 0, 1)
    c.over(lerp(hexrgb("D6ECFF"), hexrgb("5A8FE0"), t), inside)
    ly = c.y - oy
    lx = c.x - ox
    pavilion = inside * (ly <= 0.18 * u)
    c.over(hexrgb("4777CF"), pavilion * (lx < 0) * 0.55)
    c.over(hexrgb("9CC6F7"), pavilion * (lx >= 0) * 0.35)
    table = sd_polygon(c, [(ox - 0.34 * u, oy + 0.62 * u), (ox + 0.34 * u, oy + 0.62 * u),
                           (ox + 0.22 * u, oy + 0.30 * u), (ox - 0.22 * u, oy + 0.30 * u)])
    c.over(hexrgb("F0F8FF"), fill(table) * inside * 0.85)
    for a, b in (((-0.95, 0.18), (0.95, 0.18)), ((-0.22, 0.30), (0.0, -0.92)), ((0.22, 0.30), (0.0, -0.92)),
                 ((-0.55, 0.62), (-0.22, 0.30)), ((0.55, 0.62), (0.22, 0.30)), ((-0.22, 0.30), (0.22, 0.30))):
        line = sd_polyline(c, [(ox + a[0] * u, oy + a[1] * u), (ox + b[0] * u, oy + b[1] * u)], 1.0 * max(s, 0.7))
        c.over((255, 255, 255), fill(line) * fill(body + 1.5 * s) * 0.4)
    gloss = sd_ellipse(c, 6.0 * s, 3.2 * s, ox=ox - 0.48 * u, oy=oy + 0.36 * u, angle_deg=28.0)
    c.over((255, 255, 255), fill(gloss) * inside * 0.9)


def icon_gem():
    """ICO_Currency_Gems: one rounded faceted gem, the premium currency (CurrencyGems token blue)."""
    c = Canvas(128, 128, cy=66.0)
    draw_gem(c, 0.0, 0.0, 1.22)
    return "ICO_Currency_Gems", c, RUNTIME_ICONS


# Pile layouts per pack tier, back to front: (x, y, scale). One composition, more gems per tier (UI doc
# SCREEN 015: never six unrelated illustrations).
GEM_PILES = (
    ((0.0, 0.0, 1.45),),
    ((18.0, 16.0, 0.95), (-12.0, -10.0, 1.15)),
    ((-24.0, 16.0, 0.9), (24.0, 16.0, 0.9), (0.0, -14.0, 1.15)),
    ((-34.0, 24.0, 0.72), (0.0, 30.0, 0.7), (34.0, 24.0, 0.72), (-18.0, -10.0, 0.98), (18.0, -10.0, 0.98)),
    ((-36.0, 30.0, 0.66), (0.0, 34.0, 0.64), (36.0, 30.0, 0.66), (-20.0, 6.0, 0.84), (20.0, 6.0, 0.84), (0.0, -24.0, 1.05)),
    ((-54.0, 34.0, 0.58), (-18.0, 38.0, 0.58), (18.0, 38.0, 0.58), (54.0, 34.0, 0.58),
     (-36.0, 10.0, 0.76), (0.0, 12.0, 0.76), (36.0, 10.0, 0.76), (-18.0, -20.0, 0.95), (18.0, -20.0, 0.95)),
)


def icon_gem_pack(tier):
    """ICO_GemPack_0n: the gem pile for pack tier n (1 smallest). Bigger tiers sit on a soft glow."""
    c = Canvas(192, 192, cy=100.0)
    pile = GEM_PILES[tier - 1]
    lowest = min(y - 40.0 * s for _, y, s in pile)
    c.over((40, 52, 90), blur(fill(sd_ellipse(c, 30.0 + 10.0 * tier, 8.0, oy=lowest - 2.0)), 4.0) * 0.22)
    if tier >= 4:
        glow = sd_circle(c, 40.0 + 6.0 * tier)
        c.over(hexrgb("CFE6FF"), soft(glow, 26.0) * 0.55)
    for x, y, s in pile:
        draw_gem(c, x, y, s, shadow=False)
    return "ICO_GemPack_%02d" % tier, c, RUNTIME_SHOP


def icon_shop_bag():
    """ICO_Shop_Bag: soft peach shopping bag with a round handle and a small gem emblem."""
    c = Canvas(128, 128, cy=60.0)
    bag = sd_round_rect(c, 40.0, 38.0, 14.0, oy=-14.0)
    handle = np.abs(sd_ellipse(c, 22.0, 24.0, oy=18.0)) - 4.5
    handle = np.where(c.y > 14.0, handle, 1e3)
    outline = np.minimum(bag - 5.0, handle - 4.0)
    c.over((80, 40, 30), blur(shift(fill(outline), 0, 4), 3.0) * 0.28)
    c.over(hexrgb("9C5A3C"), fill(outline))
    c.over(hexrgb("F7C9A0"), fill(handle))
    t = np.clip((24.0 - c.y) / 76.0, 0, 1)
    c.over(lerp(hexrgb("FFD7B0"), hexrgb("F49B6B"), t), fill(bag))
    rim = sd_round_rect(c, 40.0, 5.0, 4.0, oy=20.0)
    c.over(hexrgb("FFE6CC"), fill(rim) * fill(bag) * 0.9)
    c.over(hexrgb("E48356"), soft(-bag - 5.0, 5.0) * np.clip(-(c.y + 14.0) / 34.0, 0, 1) * fill(bag) * 0.7)
    draw_gem(c, 0.0, -18.0, 0.42, shadow=False)
    gloss = sd_ellipse(c, 5.0, 11.0, ox=-26.0, oy=-6.0, angle_deg=-8.0)
    c.over((255, 255, 255), soft(gloss, 2.5) * fill(bag) * 0.45)
    return "ICO_Shop_Bag", c, RUNTIME_ICONS


def icon_flower_crown():
    """ICO_Cosmetic_FlowerCrown: a leafy wreath seen slightly from above, five plump flowers on its front."""
    c = Canvas(160, 160, cy=86.0)
    rx, ry, oy = 56.0, 24.0, -2.0
    ring = np.abs(sd_ellipse(c, rx, ry, oy=oy)) - 8.0
    back = c.y > oy
    c.over((40, 60, 30), blur(shift(fill(ring - 4.0), 0, 5), 3.5) * 0.25)
    c.over(hexrgb("456F33"), fill(ring - 4.0))
    c.over(hexrgb("5E9447"), fill(ring) * back)
    c.over(lerp(hexrgb("A4D67E"), hexrgb("6AA84F"), np.clip((oy - c.y) / 30.0, 0, 1)), fill(ring) * (~back))
    # Leaves along the wreath, back ones darker.
    for k in range(16):
        a = math.tau * k / 16.0
        lx, ly = math.cos(a) * rx, math.sin(a) * ry + oy
        leaf = sd_ellipse(c, 10.0, 5.0, ox=lx, oy=ly, angle_deg=math.degrees(a) + 90.0 + (24.0 if k % 2 else -24.0))
        front = ly < oy
        c.over(hexrgb("456F33"), fill(leaf - 2.0))
        c.over(hexrgb("93D06B") if front and k % 2 else hexrgb("7CBB5A") if front else hexrgb("5E9447"), fill(leaf))
    # Flowers on the front arc, back to front.
    flowers = ((-48.0, -8.0, "FFFFFF", "F2C94C", 13.0), (48.0, -8.0, "F7DF7A", "F29C4C", 13.0),
               (-26.0, -20.0, "F3B4CC", "F2C94C", 15.0), (26.0, -20.0, "C9B4EE", "F2C94C", 15.0),
               (0.0, -25.0, "F7A8BE", "FFE07A", 17.0))
    for fx, fy, petal, heart, r in flowers:
        petals = None
        for p in range(5):
            a = math.tau * p / 5.0 + math.pi / 2.0
            d = sd_circle(c, r * 0.5, fx + math.cos(a) * r * 0.6, fy + math.sin(a) * r * 0.6)
            petals = d if petals is None else np.minimum(petals, d)
        c.over((60, 40, 40), blur(shift(fill(petals), 0, 2), 1.5) * 0.25)
        c.over(hexrgb("A0705A"), fill(petals - 2.2) * 0.8)
        c.over(lerp(hexrgb(petal), hexrgb(petal), np.zeros_like(c.y)), fill(petals))
        c.over(hexrgb("FFFFFF"), soft(-petals - 2.5, 2.0) * fill(petals) * np.clip((c.y - fy) / r, 0, 1) * 0.5)
        c.over(hexrgb(heart), fill(sd_circle(c, r * 0.3, fx, fy)))
        c.over((255, 255, 255), fill(sd_circle(c, r * 0.11, fx - r * 0.1, fy + r * 0.12)) * 0.75)
    return "ICO_Cosmetic_FlowerCrown", c, RUNTIME_SHOP


def icon_close():
    """ICO_Close_X: 96 px soft cross (round caps), white for tinting."""
    c = Canvas(96, 96)
    cross = np.minimum(sd_polyline(c, [(-20.0, -20.0), (20.0, 20.0)], 7.0), sd_polyline(c, [(-20.0, 20.0), (20.0, -20.0)], 7.0))
    c.over((255, 255, 255), fill(cross))
    return "ICO_Close_X", c, RUNTIME_ICONS


SHOP_SPRITES = (icon_gem, icon_shop_bag, icon_flower_crown, icon_close) + tuple(
    (lambda n: (lambda: icon_gem_pack(n)))(n) for n in range(1, 7))


SPRITES = (kit_pill, kit_icon_circle, kit_panel_primary, kit_button_primary, kit_glow, kit_dot,
           icon_light, icon_egg, icon_rarity_star, icon_back, icon_codex, icon_mastery) + SHOP_SPRITES


def main():
    # T34A sprites live in their own file (frames, badges, traits, hearts, gesture, splash); same method.
    from pw_ui_sprites_t34a import RUNTIME_CODEX, T34A_SPRITES
    # T35A sprites (upgrades, zones, refuge level, biome gates) too.
    from pw_ui_sprites_t35a import T35A_SPRITES
    # T35B sprites (egg choice pills).
    from pw_ui_sprites_t35b import T35B_SPRITES
    # T36 sprites (rendez-vous du jour: gift box, wishes, calendar, weekly journal, rewards).
    from pw_ui_sprites_t36 import T36_SPRITES
    # T37 sprites (activity framework: Play, practice, challenge, record, timer, lock, pause, result stars, one icon
    # per activity).
    from pw_ui_sprites_t37 import T37_SPRITES
    # T38 sprites (Toilettage: the swipe gesture paw, the three bows of the picker, the golden brush of the boost chip).
    from pw_ui_sprites_t38 import T38_SPRITES
    # T39 sprites (Cache-cache: the magnifier and the whistle of the item buttons).
    from pw_ui_sprites_t39 import T39_SPRITES
    # T40 sprites (Pluie de noisettes: the wide basket, the feather and the golden shower of the boost chip).
    from pw_ui_sprites_t40 import T40_SPRITES
    # T41 sprites (Memory des empreintes: the cards, the gold glow edge, the double badge, the tablecloth, the three
    # species' pawprints, the Seed Cookie).
    from pw_ui_sprites_t41 import RUNTIME_MEMORY, T41_SPRITES
    # T42 sprites (Expéditions: the map paper, the locked destination, the slot cards, the timer ring, the duration chips
    # and clocks, the postcard's frame, stamp, postmark and petal, the rating's reasons, the postcard and bell icons).
    from pw_ui_sprites_t42 import RUNTIME_EXPEDITIONS, T42_SPRITES
    for folder in (RUNTIME_KIT, RUNTIME_ICONS, RUNTIME_SHOP, RUNTIME_CODEX, RUNTIME_MEMORY, RUNTIME_EXPEDITIONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in (SPRITES + T34A_SPRITES + T35A_SPRITES + T35B_SPRITES + T36_SPRITES + T37_SPRITES + T38_SPRITES +
                  T39_SPRITES + T40_SPRITES + T41_SPRITES + T42_SPRITES):
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
