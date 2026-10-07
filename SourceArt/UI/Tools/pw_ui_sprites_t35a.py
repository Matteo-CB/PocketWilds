"""Pocket Wilds UI sprites added in T35A (upgrades, zones, refuge level, biome gates; doc 02 Améliorations, section 12).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Own colors, readable
at 24-32 dp, filled and rounded like the other HUD icons. Owner rules: no four-point sparkle anywhere (round rays,
round dots and five-point stars only), nothing religious (the level medallion is a scalloped cookie-like rosette with
a paw, never a glowing ring or a crown of leaves).

- ICO_Upgrade: a paw lifting a warm orb (the HUD's Upgrades button).
- ICO_Upgrade_Production: a warm orb in a ring of round rays (the card's circle behind it takes the biome's tint).
- ICO_Upgrade_Nests: a small round nest holding an egg.
- ICO_Upgrade_Curiosity: a round magnifying glass over a paw print.
- ICO_ZoneSign: the zone's rounded wooden board on its two posts.
- ICO_RefugeLevel: a round scalloped medallion with a paw on top; the level number is runtime text in its middle.
- ICO_BiomeMastery: a leaf in a ring.
- ICO_Check: a round green badge with a soft check (conditions met, levels reached).

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t35a.py) to write only these, or through pw_ui_sprites.py (main).
"""
import math
import os

import numpy as np

from pw_ui_sprites import (Canvas, MASTERS, RUNTIME_ICONS, blur, fill, hexrgb, lerp, sd_circle, sd_ellipse, sd_polyline,
                           sd_round_rect, shift, smooth_min, soft)
from pw_ui_sprites_t34a import sd_leaf, sd_paw

OUTLINE = "8A5A2E"
SHADOW = (80, 52, 20)


def drop_shadow(c, shape, dy=4.0, radius=3.0, strength=0.28):
    c.over(SHADOW, blur(shift(fill(shape), 0, dy), radius) * strength)


def warm_orb(c, r, ox, oy):
    """The Light orb of the currency icon, at any size: warm gradient, deeper lower shade, bright core, gloss."""
    orb = sd_circle(c, r, ox, oy)
    t = np.clip((oy + r - c.y) / (2.0 * r), 0, 1)
    c.over(lerp(hexrgb("FFF6C8"), hexrgb("FFC23A"), t), fill(orb))
    c.over(hexrgb("F5A532"), soft(-orb - r * 0.2, r * 0.2) * np.clip((oy - c.y) / r, 0, 1) * fill(orb) * 0.8)
    core = sd_circle(c, r * 0.47, ox, oy + r * 0.13)
    c.over(hexrgb("FFFDF0"), soft(core, r * 0.33) * fill(orb) * 0.85)
    gloss = sd_ellipse(c, r * 0.24, r * 0.14, ox=ox - r * 0.33, oy=oy + r * 0.47, angle_deg=35.0)
    c.over((255, 255, 255), fill(gloss) * 0.8)
    return orb


def icon_upgrade():
    """ICO_Upgrade: a big cream paw print, its two middle toes holding up a warm orb that rests on them. No arrow."""
    c = Canvas(128, 128, cy=64.0)
    orb_y, orb_r = 32.0, 21.0
    orb = sd_circle(c, orb_r, 0.0, orb_y)
    pad = sd_ellipse(c, 25.0, 20.0, ox=0.0, oy=-34.0)
    toes = None
    for tx, ty, rx, ry, ang in ((-34.0, -6.0, 8.0, 10.5, 24.0), (-13.0, 6.0, 8.0, 10.5, 8.0),
                                (13.0, 6.0, 8.0, 10.5, -8.0), (34.0, -6.0, 8.0, 10.5, -24.0)):
        d = sd_ellipse(c, rx, ry, ox=tx, oy=ty, angle_deg=ang)
        toes = d if toes is None else np.minimum(toes, d)
    paw = np.minimum(pad, toes)
    outline = np.minimum(paw, orb) - 4.5
    drop_shadow(c, outline)
    c.over(hexrgb(OUTLINE), fill(outline))
    t = np.clip((16.0 - c.y) / 70.0, 0, 1)
    c.over(lerp(hexrgb("FFF1D6"), hexrgb("E6C397"), t), fill(paw))
    # Warm light falls from the orb on the toes under it.
    c.over(hexrgb("FFE29A"), soft(sd_circle(c, orb_r, 0.0, orb_y), 22.0) * fill(paw) * 0.6)
    c.over(hexrgb("F2B0A2"), fill(sd_ellipse(c, 14.0, 9.5, ox=0.0, oy=-37.0)) * 0.45)
    for tx in (-13.0, 13.0):
        c.over(hexrgb("F2B0A2"), fill(sd_ellipse(c, 4.0, 4.5, ox=tx, oy=3.0)) * 0.35)
    warm_orb(c, orb_r, 0.0, orb_y)
    return "ICO_Upgrade", c, RUNTIME_ICONS


def icon_upgrade_production():
    """ICO_Upgrade_Production: a warm orb in a ring of ten round rays (a plush little sun)."""
    c = Canvas(128, 128, cy=63.0)
    rays = None
    for k in range(10):
        a = math.tau * k / 10.0 + math.pi / 2.0
        d = sd_circle(c, 7.0, math.cos(a) * 42.0, math.sin(a) * 42.0)
        rays = d if rays is None else np.minimum(rays, d)
    orb = sd_circle(c, 28.0)
    outline = np.minimum(orb, rays) - 4.5
    drop_shadow(c, outline)
    c.over(hexrgb("E0892C"), fill(outline))
    c.over(lerp(hexrgb("FFE48A"), hexrgb("F7B23B"), np.clip((48.0 - c.y) / 96.0, 0, 1)), fill(rays))
    c.over((255, 255, 255), soft(-rays - 3.0, 2.0) * fill(rays) * np.clip(c.y / 48.0, 0, 1) * 0.5)
    warm_orb(c, 28.0, 0.0, 0.0)
    return "ICO_Upgrade_Production", c, RUNTIME_ICONS


def icon_upgrade_nests():
    """ICO_Upgrade_Nests: a round woven nest holding a cream egg with the Prairie's green band."""
    c = Canvas(128, 128, cy=60.0)
    egg_y = 8.0
    egg = sd_ellipse(c, 21.0, 27.0, ox=0.0, oy=egg_y)
    # The nest: a wide bowl, flat top hidden behind the egg's lower half.
    bowl = np.maximum(sd_ellipse(c, 50.0, 30.0, ox=0.0, oy=-14.0), c.y - 2.0)
    rim = sd_ellipse(c, 48.0, 11.0, ox=0.0, oy=-2.0)
    nest = smooth_min(bowl, rim, 3.0)
    outline = np.minimum(nest, egg) - 4.5
    drop_shadow(c, outline)
    c.over(hexrgb("5E3F24"), fill(outline))
    # Egg first (the rim passes in front of its lower half).
    t = np.clip((egg_y + 27.0 - c.y) / 54.0, 0, 1)
    c.over(lerp(hexrgb("FFF9EC"), hexrgb("EBD6B0"), t), fill(egg))
    period, amplitude = 14.0, 3.5
    wave = (np.abs((c.x / period) % 1.0 - 0.5) * 4.0 - 1.0) * amplitude
    c.over(hexrgb("7FBC5E"), fill(np.abs(c.y - (egg_y + 2.0 + wave)) - 4.0) * fill(egg))
    c.over((255, 255, 255), soft(sd_ellipse(c, 4.0, 8.0, ox=-9.0, oy=egg_y + 13.0, angle_deg=-20.0), 2.0) * fill(egg) * 0.8)
    # Woven nest: warm straw, darker twigs crossing it, a lighter rim.
    c.over(lerp(hexrgb("C9925A"), hexrgb("8E5E34"), np.clip((-2.0 - c.y) / 44.0, 0, 1)), fill(bowl))
    for k in range(7):
        x0 = -44.0 + k * 14.0
        twig = sd_polyline(c, [(x0 - 6.0, -6.0), (x0 + 8.0, -38.0)], 1.6)
        c.over(hexrgb("7A4E2A"), fill(twig) * fill(bowl) * 0.7)
        twig = sd_polyline(c, [(x0 + 6.0, -8.0), (x0 - 6.0, -34.0)], 1.4)
        c.over(hexrgb("DDB07A"), fill(twig) * fill(bowl) * 0.6)
    c.over(lerp(hexrgb("E6BC82"), hexrgb("B07C46"), np.clip((8.0 - c.y) / 18.0, 0, 1)), fill(rim))
    for k in range(9):
        x0 = -40.0 + k * 10.0
        c.over(hexrgb("8E5E34"), fill(sd_polyline(c, [(x0 - 4.0, -6.0), (x0 + 4.0, 3.0)], 1.3)) * fill(rim) * 0.7)
    return "ICO_Upgrade_Nests", c, RUNTIME_ICONS


def icon_upgrade_curiosity():
    """ICO_Upgrade_Curiosity: a round magnifying glass over a brown paw print, seen bigger through the lens."""
    c = Canvas(128, 128, cy=64.0)
    lens_x, lens_y, r = -8.0, 10.0, 31.0
    lens = sd_circle(c, r, lens_x, lens_y)
    frame = np.abs(lens) - 5.5
    handle = sd_polyline(c, [(lens_x + 24.0, lens_y - 24.0), (lens_x + 46.0, lens_y - 46.0)], 7.5)
    outline = np.minimum(np.minimum(frame, handle), lens) - 4.0
    drop_shadow(c, outline)
    c.over(hexrgb("4E3B2C"), fill(outline))
    c.over(lerp(hexrgb("EAF6FF"), hexrgb("BFDDF2"), np.clip((lens_y + r - c.y) / (2.0 * r), 0, 1)), fill(lens))
    c.over(hexrgb("8A5A2E"), fill(sd_paw(c, 1.25, lens_x, lens_y + 1.0)) * fill(lens))
    c.over((255, 255, 255), soft(sd_ellipse(c, 9.0, 5.0, ox=lens_x - 13.0, oy=lens_y + 15.0, angle_deg=35.0), 2.5) * fill(lens) * 0.85)
    c.over(lerp(hexrgb("D99A52"), hexrgb("A9692E"), np.clip((lens_y + r - c.y) / (2.0 * r), 0, 1)), fill(frame))
    c.over(hexrgb("F2C27E"), soft(-frame - 2.0, 1.5) * fill(frame) * np.clip((c.y - lens_y) / r, 0, 1) * 0.8)
    c.over(lerp(hexrgb("7FBC5E"), hexrgb("4F8A3F"), np.clip((lens_y - 24.0 - c.y) / 22.0, 0, 1)), fill(handle))
    c.over(hexrgb("A9DB86"), fill(sd_polyline(c, [(lens_x + 28.0, lens_y - 25.0), (lens_x + 42.0, lens_y - 39.0)], 2.0)) * fill(handle) * 0.7)
    return "ICO_Upgrade_Curiosity", c, RUNTIME_ICONS


def icon_zone_sign():
    """ICO_ZoneSign: the zone's rounded wooden board of planks, high on two thin posts, like the 3D sign."""
    c = Canvas(128, 128, cy=62.0)
    posts = None
    for px in (-24.0, 24.0):
        post = sd_round_rect(c, 5.0, 38.0, 2.0, ox=px, oy=-18.0)
        posts = post if posts is None else np.minimum(posts, post)
    board = sd_round_rect(c, 48.0, 26.0, 11.0, oy=16.0)
    shape = np.minimum(posts, board)
    outline = shape - 4.5
    drop_shadow(c, outline)
    c.over(hexrgb("5E3F24"), fill(outline))
    c.over(lerp(hexrgb("B8804A"), hexrgb("8E5E34"), np.clip((-10.0 - c.y) / 46.0, 0, 1)), fill(posts))
    c.over(lerp(hexrgb("E3B37A"), hexrgb("C48B52"), np.clip((42.0 - c.y) / 52.0, 0, 1)), fill(board))
    for y in (25.0, 7.5):
        c.over(hexrgb("A8733F"), fill(np.abs(c.y - y) - 1.2) * fill(board) * 0.75)
    c.over(hexrgb("F6D3A0"), soft(-board - 3.0, 2.0) * fill(board) * np.clip((c.y - 16.0) / 26.0, 0, 1) * 0.8)
    for nx in (-24.0, 24.0):
        for ny in (33.0, 0.0):
            c.over(hexrgb("6B4528"), fill(sd_circle(c, 2.3, nx, ny)))
    return "ICO_ZoneSign", c, RUNTIME_ICONS


def icon_refuge_level():
    """ICO_RefugeLevel: a round scalloped medallion (fourteen soft bumps), cream face, a small paw on top.
    The level number is runtime text over its middle, so the face stays plain below the paw."""
    c = Canvas(160, 160, cy=78.0)
    rosette = sd_circle(c, 62.0)
    for k in range(14):
        a = math.tau * k / 14.0
        rosette = smooth_min(rosette, sd_circle(c, 12.0, math.cos(a) * 62.0, math.sin(a) * 62.0), 4.0)
    face = sd_circle(c, 52.0)
    outline = rosette - 4.5
    drop_shadow(c, outline, dy=5.0, radius=4.0)
    c.over(hexrgb("C9782A"), fill(outline))
    c.over(lerp(hexrgb("FFE08A"), hexrgb("F2A93B"), np.clip((74.0 - c.y) / 148.0, 0, 1)), fill(rosette))
    c.over(hexrgb("FFF3C4"), soft(-rosette - 3.0, 2.5) * fill(rosette) * np.clip(c.y / 70.0, 0, 1) * 0.8)
    c.over(hexrgb("D98A2C"), fill(np.abs(face) - 2.5) * 0.8)
    c.over(lerp(hexrgb("FFFBF0"), hexrgb("F6E6C8"), np.clip((52.0 - c.y) / 104.0, 0, 1)), fill(face))
    c.over(hexrgb("E9D2A6"), soft(-face - 8.0, 8.0) * np.clip(-c.y / 52.0, 0, 1) * fill(face) * 0.7)
    c.over(hexrgb("E39A3A"), fill(sd_paw(c, 0.72, 0.0, 33.0)))
    return "ICO_RefugeLevel", c, RUNTIME_ICONS


def icon_biome_mastery():
    """ICO_BiomeMastery: a plump Prairie leaf with its midrib, in a soft golden ring."""
    c = Canvas(128, 128, cy=64.0)
    ring = np.abs(sd_circle(c, 44.0)) - 7.0
    leaf = sd_leaf(c, 74.0, 22.0, 0.0, 0.0, 45.0)
    outline = np.minimum(ring, leaf) - 4.0
    drop_shadow(c, outline)
    c.over(hexrgb("6B4E1E"), fill(outline))
    c.over(lerp(hexrgb("FFE48A"), hexrgb("E9A23B"), np.clip((51.0 - c.y) / 102.0, 0, 1)), fill(ring))
    c.over((255, 255, 255), soft(-ring - 2.0, 1.5) * fill(ring) * np.clip(c.y / 44.0, 0, 1) * 0.6)
    c.over(lerp(hexrgb("A6DB7A"), hexrgb("5E9E4A"), np.clip((30.0 - c.y - c.x * 0.4) / 60.0, 0, 1)), fill(leaf))
    rib = sd_polyline(c, [(-22.0, -22.0), (20.0, 20.0)], 1.8)
    c.over(hexrgb("4F8A3F"), fill(rib) * fill(leaf) * 0.85)
    for t in (-8.0, 4.0):
        c.over(hexrgb("4F8A3F"), fill(sd_polyline(c, [(t, t), (t - 10.0, t + 6.0)], 1.2)) * fill(leaf) * 0.6)
        c.over(hexrgb("4F8A3F"), fill(sd_polyline(c, [(t, t), (t + 6.0, t - 10.0)], 1.2)) * fill(leaf) * 0.6)
    c.over((255, 255, 255), soft(sd_ellipse(c, 3.5, 9.0, ox=-10.0, oy=4.0, angle_deg=45.0), 2.0) * fill(leaf) * 0.5)
    return "ICO_BiomeMastery", c, RUNTIME_ICONS


def icon_check():
    """ICO_Check: a round green badge with a soft white check (round caps)."""
    c = Canvas(96, 96, cy=47.0)
    disc = sd_circle(c, 34.0)
    outline = disc - 3.5
    drop_shadow(c, outline, dy=3.0, radius=2.0, strength=0.24)
    c.over(hexrgb("4F8A3F"), fill(outline))
    c.over(lerp(hexrgb("A6DB7A"), hexrgb("6FAE52"), np.clip((34.0 - c.y) / 68.0, 0, 1)), fill(disc))
    c.over((255, 255, 255), soft(-disc - 2.0, 1.5) * fill(disc) * np.clip(c.y / 34.0, 0, 1) * 0.5)
    check = sd_polyline(c, [(-15.0, 1.0), (-4.0, -11.0), (16.0, 12.0)], 5.5)
    c.over((255, 255, 255), fill(check))
    return "ICO_Check", c, RUNTIME_ICONS


T35A_SPRITES = (icon_upgrade, icon_upgrade_production, icon_upgrade_nests, icon_upgrade_curiosity, icon_zone_sign,
                icon_refuge_level, icon_biome_mastery, icon_check)


def main():
    for folder in (RUNTIME_ICONS, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T35A_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
