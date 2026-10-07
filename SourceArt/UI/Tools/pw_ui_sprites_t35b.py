"""Pocket Wilds UI sprites added in T35B (egg choice around the hatch button; doc 02 Œufs, sections 2.2 and 5).

Same vector-like method as pw_ui_sprites.py (signed distances rendered at 4x, box-filtered down). Both sprites are
drawn in white and greys so the HUD tints them with each egg's own shell and spot colors (EggDefinition.ShellColor and
SpotColor): the shell carries its volume as greys and its outline in the HUD's warm brown, the spots sit on the same
frame so the two images stack exactly. Owner rules: round dots only, no four-point sparkle, nothing religious.

- UI_Egg_Shell: the egg of a choice pill, outline, soft lower shade, light upper side.
- UI_Egg_Spots: the egg's round spots, same frame as UI_Egg_Shell.

Run alone (python SourceArt/UI/Tools/pw_ui_sprites_t35b.py) to write only these, or through pw_ui_sprites.py (main).
"""
import os

import numpy as np

from pw_ui_sprites import MASTERS, RUNTIME_KIT, Canvas, blur, fill, lerp, sd_circle, sd_egg, shift, soft

WIDTH, HEIGHT = 96, 112
CENTER_Y = 54.0


def egg_shape(c):
    return sd_egg(c, 34.0, 44.0, 0.16, oy=0.0)


def kit_egg_shell():
    """UI_Egg_Shell: white egg for tinting; the outline is the HUD's warm brown, shells are pale so it stays brown."""
    c = Canvas(WIDTH, HEIGHT, cy=CENTER_Y)
    egg = egg_shape(c)
    outline = egg - 4.0
    c.over((60, 60, 60), blur(shift(fill(outline), 0, 3), 2.5) * 0.22)
    c.over((146, 104, 72), fill(outline))
    t = np.clip((44.0 - c.y) / 88.0, 0, 1)
    c.over(lerp((255, 255, 255), (226, 226, 226), t), fill(egg))
    # Lower inner shade along the rim, strongest at the bottom: the egg sits round in its pill.
    lower = soft(-egg - 6.0, 6.0) * np.clip(-c.y / 40.0, 0, 1)
    c.over((205, 205, 205), lower * fill(egg) * 0.8)
    # The light side keeps the full tint: a soft brighter oval high on the left.
    light = sd_circle(c, 13.0, -11.0, 20.0)
    c.over((255, 255, 255), soft(light, 7.0) * fill(egg) * 0.9)
    return "UI_Egg_Shell", c, RUNTIME_KIT


def kit_egg_spots():
    """UI_Egg_Spots: round spots of three sizes inside the egg, white for tinting, on UI_Egg_Shell's frame."""
    c = Canvas(WIDTH, HEIGHT, cy=CENTER_Y)
    inside = fill(egg_shape(c) + 3.0)
    spots = ((-12.0, -10.0, 8.5), (13.0, 4.0, 6.5), (2.0, 24.0, 5.0), (-17.0, 14.0, 4.0), (16.0, -22.0, 4.5),
             (-2.0, -30.0, 3.5))
    for ox, oy, r in spots:
        dot = sd_circle(c, r, ox, oy)
        c.over((236, 236, 236), fill(dot) * inside)
        c.over((255, 255, 255), fill(sd_circle(c, r * 0.55, ox - r * 0.2, oy + r * 0.2)) * inside * 0.9)
    return "UI_Egg_Spots", c, RUNTIME_KIT


T35B_SPRITES = (kit_egg_shell, kit_egg_spots)


def main():
    for folder in (RUNTIME_KIT, MASTERS):
        os.makedirs(folder, exist_ok=True)
    for build in T35B_SPRITES:
        name, canvas, folder = build()
        canvas.image(True).save(os.path.join(folder, name + ".png"), optimize=True)
        canvas.image(False).save(os.path.join(MASTERS, name + "@4x.png"), optimize=True)
        print("wrote", name, canvas.w, "x", canvas.h)


if __name__ == "__main__":
    main()
