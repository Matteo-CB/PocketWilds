"""T44B review sheet: the 5 badges with the 13 category icons over them, the achievement button, and the 42 frames composed
around a sample portrait circle (ring + rim copy + motifs + ring style, as FrameArt.cs describes). Writes
Docs/Captures/Review/T44B_art_sheet.png."""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from pw_ui_sprites import ROOT, RUNTIME_ICONS
from pw_ui_sprites_t44b import FRAMES, RUNTIME_FRAMES, RUNTIME_PROFILE, TL, TR, BL, BR, TOP, BOT

CATS = ("Collection", "Biomes", "Mastery", "Hatching", "Activities", "Daily", "ExpeditionsGarden", "Albums", "Decoration",
        "Bonds", "Events", "Renewal", "Refuge")
SLOT_ANGLE = {TOP: 90, BOT: 270, TL: 135, TR: 45, BL: 225, BR: 315}


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def tint(im, color):
    a = np.asarray(im.convert("RGBA")).astype(np.float64)
    a[..., 0] *= color[0] / 255.0
    a[..., 1] *= color[1] / 255.0
    a[..., 2] *= color[2] / 255.0
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def load(path, size=None):
    im = Image.open(path).convert("RGBA")
    return im.resize((size, size), Image.LANCZOS) if size else im


def compose(frame, size, ring, motifs_cache, bg):
    key, ring_c, rim_c, style, count, colours, motifs = frame
    cell = Image.new("RGBA", (size, size), bg)
    cx = cy = size / 2.0
    radius = size * 0.5 * 0.78          # outer radius of the ring
    d = ImageDraw.Draw(cell)
    inner_r = radius * 204.0 / 246.0
    ring_mid = radius * 225.0 / 246.0
    # the sample portrait
    d.ellipse([cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r], fill=(236, 214, 178, 255))
    paw = motifs_cache["Paw"]
    pw = int(size * 0.34)
    cell.alpha_composite(tint(paw.resize((pw, pw), Image.LANCZOS), (170, 120, 80)), (int(cx - pw / 2), int(cy - pw / 2)))
    rd = int(radius * 2)
    # rim copy behind, then the ring
    if style == "gradient":
        base = tint(ring.resize((rd, rd), Image.LANCZOS), rgb(ring_c))
        low = tint(ring.resize((rd, rd), Image.LANCZOS), rgb(colours[0]))
        mask = Image.linear_gradient("L").resize((rd, rd))   # black top, white bottom
        base = Image.composite(low, base, mask)
    else:
        base = tint(ring.resize((rd, rd), Image.LANCZOS), rgb(ring_c))
    rim = tint(ring.resize((int(rd * 1.06), int(rd * 1.06)), Image.LANCZOS), rgb(rim_c))
    cell.alpha_composite(rim, (int(cx - rim.width / 2), int(cy - rim.height / 2)))
    cell.alpha_composite(base, (int(cx - rd / 2), int(cy - rd / 2)))
    d = ImageDraw.Draw(cell)
    if style == "double":
        r2 = inner_r - 5
        d.ellipse([cx - r2, cy - r2, cx + r2, cy + r2], outline=rgb(colours[0]) + (255,), width=max(2, size // 60))
    elif style == "pearls":
        pr = size * (0.022 if count > 12 else 0.04)
        for i in range(count):
            a = math.tau * i / count + math.pi / 2
            col = colours[-1] if (len(colours) == 2 and i % 7 == 0) else colours[i % len(colours)] if len(colours) != 2 else colours[0]
            x, y = cx + math.cos(a) * ring_mid, cy - math.sin(a) * ring_mid
            d.ellipse([x - pr, y - pr, x + pr, y + pr], fill=rgb(col) + (255,), outline=(120, 100, 80, 255))
    elif style == "bands":
        box = [cx - ring_mid, cy - ring_mid, cx + ring_mid, cy + ring_mid]
        for i, col in enumerate(colours):
            d.arc(box, 360.0 * i / count - 90, 360.0 * (i + 1) / count - 90, fill=rgb(col) + (255,), width=int(radius - inner_r) - 2)
    elif style == "dotted":
        for i in range(36):
            a = math.tau * i / 36
            x, y = cx + math.cos(a) * (inner_r - 6), cy - math.sin(a) * (inner_r - 6)
            d.ellipse([x - 1.6, y - 1.6, x + 1.6, y + 1.6], fill=rgb(colours[0]) + (255,))
    elif style == "ticks":
        for i in range(60):
            a = math.tau * i / 60
            r0, r1 = ring_mid - (6 if i % 5 == 0 else 3), ring_mid + (6 if i % 5 == 0 else 3)
            d.line([cx + math.cos(a) * r0, cy - math.sin(a) * r0, cx + math.cos(a) * r1, cy - math.sin(a) * r1], fill=rgb(colours[0]) + (255,), width=1)
    elif style == "lines":
        pts = [(cx + math.cos(math.radians(SLOT_ANGLE[m[1]])) * ring_mid, cy - math.sin(math.radians(SLOT_ANGLE[m[1]])) * ring_mid) for m in motifs]
        pts = [pts[0], pts[1], pts[3], pts[2], pts[0]] if len(pts) == 4 else pts
        d.line(pts, fill=rgb(colours[0]) + (255,), width=1)
    ms = int(size * 0.30)
    for sprite, slot, t in motifs:
        im = motifs_cache[sprite].resize((ms, ms), Image.LANCZOS)
        if t != "FFFFFF":
            im = tint(im, rgb(t))
        a = math.radians(SLOT_ANGLE[slot])
        cell.alpha_composite(im, (int(cx + math.cos(a) * ring_mid - ms / 2), int(cy - math.sin(a) * ring_mid - ms / 2)))
    return cell


def main():
    cache = {}
    for f in os.listdir(RUNTIME_FRAMES):
        if f.startswith("M_") and f.endswith(".png"):
            cache[f[2:-4]] = load(os.path.join(RUNTIME_FRAMES, f))
    ring = load(os.path.join(RUNTIME_FRAMES, "UI_Frame_Ring.png"))
    font = ImageFont.load_default()
    cols, cell = 7, 210
    width = cols * cell + 24
    bg_row = 12 + 192 + 12 + 96 + 12
    rows = math.ceil(len(FRAMES) / cols)
    sheet = Image.new("RGBA", (max(width, 1500), bg_row + 40 + rows * (cell + 14) + 12), (246, 238, 220, 255))
    dark = Image.new("RGBA", (sheet.width, 192 + 24), (52, 48, 70, 255))
    sheet.paste(dark, (0, 0))
    x = 12
    for name in ("Bronze", "Silver", "Gold", "Star", "Locked"):
        sheet.alpha_composite(load(os.path.join(RUNTIME_PROFILE, "UI_Badge_%s.png" % name)), (x, 12))
        x += 204
    sheet.alpha_composite(load(os.path.join(RUNTIME_ICONS, "ICO_Achievement.png")), (x, 12 + 32))
    x += 140
    # the icons over a gold badge (96 px) and over a bronze, silver and star badge for a few
    for i, cat in enumerate(CATS):
        ic = load(os.path.join(RUNTIME_ICONS, "ICO_AchCat_%s.png" % cat))
        tier = ("Bronze", "Silver", "Gold", "Star")[i % 4]
        b = load(os.path.join(RUNTIME_PROFILE, "UI_Badge_%s.png" % tier), 112)
        bx = 12 + i * 114
        sheet.alpha_composite(b, (bx, 12 + 192 + 12 + 6))
        sheet.alpha_composite(ic.resize((64, 64), Image.LANCZOS), (bx + 24, 12 + 192 + 12 + 6 + 24))
    y0 = bg_row + 40
    dr = ImageDraw.Draw(sheet)
    for i, fr in enumerate(FRAMES):
        im = compose(fr, cell - 10, ring, cache, (246, 238, 220, 255))
        px, py = 12 + (i % cols) * cell, y0 + (i // cols) * (cell + 14)
        sheet.alpha_composite(im, (px, py))
        dr.text((px + 4, py + cell - 8), fr[0], fill=(70, 55, 40, 255), font=font)
    out = os.path.join(ROOT, "Docs", "Captures", "Review", "T44B_art_sheet.png")
    sheet.convert("RGB").save(out, optimize=True)
    print("wrote", out, sheet.size)


if __name__ == "__main__":
    main()
