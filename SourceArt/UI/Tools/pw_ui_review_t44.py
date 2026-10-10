"""T44A review sheet: the 8 album icons (and the two reused activity icons) at 128 px and 48 px, the 16 covers, on a light
and a dark ground. Writes Docs/Captures/Review/T44A_icons_sheet.png."""
import os

from PIL import Image

from pw_ui_sprites import ROOT, RUNTIME_ICONS
from pw_ui_sprites_t44 import COVERS, RUNTIME_COVERS

ICONS = ("ICO_Album", "ICO_AlbumStar", "ICO_Star_Rating", "ICO_Event_Calendar", "ICO_Visitor", "ICO_World_Map", "ICO_Season",
         "ICO_Sticker", "ICO_Act_Expeditions", "ICO_Act_Garden")


def main():
    cw, ch = 192, 240
    width = 8 * (cw + 12) + 12
    sheet = Image.new("RGBA", (width, 12 + 140 + 70 + 12 + 2 * (ch + 12) + 12), (246, 238, 220, 255))
    dark = Image.new("RGBA", (width, 70 + 12), (52, 48, 70, 255))
    sheet.paste(dark, (0, 12 + 140))
    for i, name in enumerate(ICONS):
        im = Image.open(os.path.join(RUNTIME_ICONS, name + ".png")).convert("RGBA")
        x = 12 + i * 150
        sheet.alpha_composite(im, (x, 12))
        sheet.alpha_composite(im.resize((48, 48), Image.LANCZOS), (x + 40, 12 + 140 + 12))
        sheet.alpha_composite(im.resize((48, 48), Image.LANCZOS), (x + 40 + 0, 12 + 140 + 12)) if False else None
    for i, (name, _, _) in enumerate(COVERS):
        im = Image.open(os.path.join(RUNTIME_COVERS, "UI_AlbumCover_" + name + ".png")).convert("RGBA").resize((cw, ch), Image.LANCZOS)
        sheet.alpha_composite(im, (12 + (i % 8) * (cw + 12), 12 + 140 + 70 + 12 + (i // 8) * (ch + 12)))
    out = os.path.join(ROOT, "Docs", "Captures", "Review", "T44A_icons_sheet.png")
    sheet.convert("RGB").save(out, optimize=True)
    print("wrote", out, sheet.size)


if __name__ == "__main__":
    main()
