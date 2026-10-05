"""Specimen sheets for the font candidates (T31). Pillow renders through FreeType, like Unity's TextCore.

    python pw_font_proof.py quick PWRoundClaire   # one fast sheet for design iterations
    python pw_font_proof.py all                   # every candidate review sheet into TestResults/Fonts/Review
    python pw_font_proof.py final                 # Pocket Round proofs into TestResults/Fonts/Final (doc 05, step 5)
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
BUILD = os.path.join(ROOT, "SourceArt", "Fonts", "Build")
OUT = os.path.join(ROOT, "TestResults", "Fonts")
REVIEW = os.path.join(OUT, "Review")
TEMP_FONT = os.path.join(ROOT, "Assets", "TextMesh Pro", "Fonts", "LiberationSans.ttf")
WORLD = os.path.join(ROOT, "TestResults", "Captures", "T30_world_close.png")

INK = (61, 43, 31)
SOFT = (125, 98, 72)
CREAM = (255, 251, 242)
PANEL = (255, 255, 252)
ORANGE = (252, 172, 84)
GREEN = (114, 176, 86)

UI = {"Douce": "PWRoundDouce", "Claire": "PWRoundClaire", "Câline": "PWRoundCaline"}
TITLES = {"Bulle": "PWPlushBulle", "Guimauve": "PWPlushGuimauve"}

ALPHABET = [
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
    "abcdefghijklmnopqrstuvwxyz",
    "0123456789 €$£¥₩₽₹₺ %+−=×",
    "àâäçéèêëîïôöùûüÿœæß ÀÉÈÊÇÔŒ",
    ".,:;!?¡¿ '\"’“”«» ()[] &#°•…",
]

GAME_LINES = [
    ("Nouvelle espèce !", "New species!"),
    ("Écureuil roux · Rare", "Red Squirrel · Rare"),
    ("Bon retour · +24,8 K Lumière", "Welcome back · +24.8K Light"),
    ("Pack de la Prairie · 1,99 €", "Meadow Pack · €1.99"),
    ("Hamster des champs + 500 Gemmes", "Field Hamster + 500 Gems"),
    ("Fais éclore 3 œufs · 2/3", "Hatch 3 eggs · 2/3"),
    ("Le hérisson s'endort près des fleurs.", "The hedgehog naps near the flowers."),
]


def font(path, size):
    return ImageFont.truetype(path, size)


def ttf(name):
    return os.path.join(BUILD, name + ".ttf")


def quick(family):
    styles = ["Regular", "SemiBold", "Bold"] if "Round" in family else ["Black"]
    img = Image.new("RGB", (1800, 1500), CREAM)
    d = ImageDraw.Draw(img)
    y = 30
    for st in styles:
        f = font(ttf(family + "-" + st), 78)
        for line in ALPHABET[:3]:
            d.text((40, y), line, font=f, fill=INK)
            y += 96
        y += 10
    os.makedirs(OUT, exist_ok=True)
    out = os.path.join(OUT, f"quick_{family}.png")
    img.save(out)
    return out


def rounded(d, box, r, fill):
    d.rounded_rectangle(box, r, fill=fill)


def sheet_interface(label, fam):
    """Alphabet in three weights, phone sizes, game lines in mock UI."""
    W, H = 1600, 2600
    img = Image.new("RGB", (W, H), CREAM)
    d = ImageDraw.Draw(img)
    bold = font(ttf(fam + "-Bold"), 96)
    d.text((60, 50), label, font=bold, fill=INK)
    d.text((60, 165), "Police d'interface · Regular, SemiBold, Bold", font=font(ttf(fam + "-Regular"), 40), fill=SOFT)
    y = 250
    for weight in ["Regular", "SemiBold", "Bold"]:
        f = font(ttf(fam + "-" + weight), 54)
        d.text((60, y), weight, font=font(ttf(fam + "-SemiBold"), 30), fill=SOFT)
        y += 44
        for line in ALPHABET:
            d.text((60, y), line, font=f, fill=INK)
            y += 70
        y += 24
    # Phone sizes: a 1080 px wide phone shows 1 dp as about 2.75 px.
    y += 10
    d.text((60, y), "Tailles réelles sur téléphone (en dp)", font=font(ttf(fam + "-SemiBold"), 38), fill=SOFT)
    y += 60
    for dp in [14, 16, 18, 22, 28, 36]:
        px = int(dp * 2.75)
        d.text((60, y), f"{dp}", font=font(ttf(fam + "-SemiBold"), 30), fill=SOFT)
        d.text((150, y), "Un lapin tout doux vient d'éclore ! 12 400", font=font(ttf(fam + "-Regular"), px), fill=INK)
        y += int(px * 1.35) + 8
    img = img.crop((0, 0, W, min(H, y + 40)))
    return img


def ui_mock(label, fam, title_fam=None, language=0):
    """A phone-sized mock with the main UI text roles: counters, buttons, cards, toasts."""
    W, H = 1080, 1500
    img = Image.new("RGB", (W, H), CREAM)
    d = ImageDraw.Draw(img)
    reg = lambda s: font(ttf(fam + "-Regular"), s)
    semi = lambda s: font(ttf(fam + "-SemiBold"), s)
    bold = lambda s: font(ttf(fam + "-Bold"), s)
    title = (lambda s: font(ttf(title_fam + "-Black"), s)) if title_fam else bold
    fr = language == 0
    d.text((W / 2, 70), "Boutique" if fr else "Shop", font=title(84), fill=INK, anchor="mm")
    rounded(d, (60, 150, W - 60, 470), 46, PANEL)
    d.text((110, 190), "Pack de la Prairie" if fr else "Meadow Pack", font=title(60), fill=INK)
    d.text((110, 275), "Hamster des champs + 500 Gemmes" if fr else "Field Hamster + 500 Gems", font=semi(40), fill=SOFT)
    d.text((110, 335), "Offre unique" if fr else "One-time offer", font=reg(36), fill=SOFT)
    rounded(d, (W - 420, 380, W - 100, 450), 35, ORANGE)
    d.text((W - 260, 415), "1,99 €" if fr else "€1.99", font=bold(42), fill=INK, anchor="mm")
    rounded(d, (60, 520, W - 60, 760), 46, PANEL)
    d.text((110, 560), "Bon retour" if fr else "Welcome back", font=semi(46), fill=SOFT)
    d.text((110, 625), "+24,8 K" if fr else "+24.8K", font=title(96), fill=INK)
    rounded(d, (W - 420, 650, W - 100, 730), 40, ORANGE)
    d.text((W - 260, 690), "Récolter" if fr else "Collect", font=bold(44), fill=INK, anchor="mm")
    rounded(d, (60, 810, W - 60, 1060), 46, PANEL)
    d.text((110, 845), "Souhaits du jour" if fr else "Today's wishes", font=title(52), fill=INK)
    lines = ["Brosse Mochi · 1/1", "Fais éclore 3 œufs · 2/3", "Joue à Cache-cache · 0/1"] if fr else \
        ["Brush Mochi · 1/1", "Hatch 3 eggs · 2/3", "Play Hide and Seek · 0/1"]
    yy = 920
    for line in lines:
        d.text((110, yy), line, font=reg(38), fill=INK)
        yy += 46
    rounded(d, (60, 1110, W - 60, 1220), 55, (255, 236, 205))
    d.text((W / 2, 1165), "Nouvelle espèce ! Écureuil roux" if fr else "New species! Red Squirrel", font=semi(44), fill=INK, anchor="mm")
    rounded(d, (W / 2 - 290, 1270, W / 2 + 290, 1340), 35, PANEL)
    d.text((W / 2, 1305), "12 400  +86/s" if fr else "12,400  +86/s", font=bold(46), fill=INK, anchor="mm")
    d.text((W / 2, 1420), label, font=reg(34), fill=SOFT, anchor="mm")
    return img


def title_treatments(label, fam):
    """Hero titles as they will look in game: cream fill, brown outline, soft shadow, over the meadow."""
    base = Image.open(WORLD).convert("RGB") if os.path.exists(WORLD) else Image.new("RGB", (1200, 760), GREEN)
    base = base.resize((1200, 760))
    img = base.copy()
    items = [("Nouvelle espèce !", 120, 150), ("Les Aurores", 104, 330), ("+500 Gemmes", 92, 490), ("Pack de la Prairie", 74, 640)]
    for text, size, y in items:
        f = font(ttf(fam + "-Black"), size)
        shadow = Image.new("L", img.size, 0)
        ImageDraw.Draw(shadow).text((600, y + 12), text, font=f, fill=150, anchor="mm", stroke_width=14, stroke_fill=150)
        shadow = shadow.filter(ImageFilter.GaussianBlur(10))
        img.paste((40, 28, 20), (0, 0), shadow)
        d = ImageDraw.Draw(img)
        d.text((600, y), text, font=f, fill=(255, 248, 232), anchor="mm", stroke_width=12, stroke_fill=(86, 56, 36))
    d = ImageDraw.Draw(img)
    d.text((24, 724), label, font=font(ttf(fam + "-Black"), 28), fill=(255, 255, 255))
    return img


def display_sheet(label, fam):
    W = 1600
    img = Image.new("RGB", (W, 1700), CREAM)
    d = ImageDraw.Draw(img)
    big = font(ttf(fam + "-Black"), 120)
    d.text((60, 40), label, font=big, fill=INK)
    d.text((60, 190), "Police de titres · révélations, événements, gros gains", font=font(ttf("PWRoundClaire-Regular"), 40), fill=SOFT)
    y = 270
    f = font(ttf(fam + "-Black"), 76)
    for line in ALPHABET:
        d.text((60, y), line, font=f, fill=INK)
        y += 104
    y += 20
    for line in ["La Pluie d'Étoiles", "Le Festival des Lucioles", "Écureuil volant · Prestige", "x6  +1 250  13 000"]:
        d.text((60, y), line, font=font(ttf(fam + "-Black"), 92), fill=INK)
        y += 126
    return img.crop((0, 0, W, y + 30))


def comparison():
    W = 1600
    img = Image.new("RGB", (W, 1500), CREAM)
    d = ImageDraw.Draw(img)
    rows = [("Actuelle (temporaire)", TEMP_FONT, TEMP_FONT)] + [(k, ttf(v + "-Regular"), ttf(v + "-Bold")) for k, v in UI.items()]
    y = 40
    for name, reg_path, bold_path in rows:
        d.text((60, y), name, font=font(ttf("PWRoundClaire-SemiBold"), 34), fill=SOFT)
        y += 50
        d.text((60, y), "Nouvelle espèce ! Écureuil roux", font=font(bold_path, 64), fill=INK)
        y += 84
        d.text((60, y), "Le hérisson s'endort près des fleurs. 12 400 · 1,99 €", font=font(reg_path, 46), fill=INK)
        y += 90
    return img.crop((0, 0, W, y + 20))


def save(img, name, quality=88):
    os.makedirs(REVIEW, exist_ok=True)
    path = os.path.join(REVIEW, name)
    if name.endswith(".jpg"):
        img.convert("RGB").save(path, quality=quality, optimize=True)
    else:
        img.save(path, optimize=True)
    return path


def all_sheets():
    out = [save(comparison(), "compare_interface.jpg")]
    for label, fam in UI.items():
        out.append(save(sheet_interface(label, fam), f"sheet_{fam}.jpg"))
        out.append(save(ui_mock(label, fam), f"mock_{fam}_fr.jpg"))
        out.append(save(ui_mock(label, fam, None, 1), f"mock_{fam}_en.jpg"))
        for tlabel, tfam in TITLES.items():
            out.append(save(ui_mock(f"{label} + {tlabel}", fam, tfam), f"mock_{fam}_{tfam}_fr.jpg"))
    for label, fam in TITLES.items():
        out.append(save(display_sheet(label, fam), f"sheet_{fam}.jpg"))
        out.append(save(title_treatments(label, fam), f"titles_{fam}.jpg"))
    return out


# ---------------- shipped family proofs (doc 05, step 5) ----------------

FINAL_OUT = os.path.join(OUT, "Final")
LOCALIZATION = os.path.join(ROOT, "Assets", "_Game", "Content", "Localization")
DARK = (40, 29, 21)

# One pangram or dense sentence per doc 05 language, to see every accent in use.
LANGUAGES = [
    ("Français", "Portez ce vieux whisky au juge blond qui fume. Ça, c'est l'été à Noël !"),
    ("English", "The quick brown fox jumps over the lazy dog."),
    ("Español", "El veloz murciélago hindú comía feliz cardillo y kiwi. ¿Qué tal? ¡Olé!"),
    ("Deutsch", "Zwölf Boxkämpfer jagen Viktor quer über den großen Sylter Deich."),
    ("Italiano", "Ma la volpe, col suo balzo, ha raggiunto il quieto Fido. Perché è così?"),
    ("Português", "Luís argüia à Júlia que «brações, fé, chá, óxido, pôr, zângão» eram palavras."),
    ("Nederlands", "Pa's wijze lynx bezag vroom het fikse aquaduct. Één, twee, drie."),
    ("Polski", "Pchnąć w tę łódź jeża lub ośm skrzyń fig."),
    ("Türkçe", "Pijamalı hasta yağız şoföre çabucak güvendi. İstanbul'da ılık bir gün."),
    ("Svenska", "Flygande bäckasiner söka hwila på mjuka tuvor."),
    ("Dansk", "Høj bly gom vandt fræk sexquiz på wc."),
    ("Norsk", "Vår sære Zulu fra badeøya spilte jo whist og quickstep i min taxi."),
    ("Suomi", "Törkylempijävongahdus. Älä hätäile, öljy on hyvää."),
    ("Čeština", "Příliš žluťoučký kůň úpěl ďábelské ódy."),
    ("Slovenčina", "Kŕdeľ šťastných ďatľov učí pri ústí Váhu mĺkveho koňa obhrýzať kôru."),
    ("Română", "Muzicologă în bej vând whisky și tequila, preț fix."),
    ("Magyar", "Árvíztűrő tükörfúrógép, öt szép szűz lány őrült írót nyúz."),
    ("Latviešu", "Glāžšķūņa rūķīši dzērumā čiepj Baha koncertflīģeļu vākus."),
    ("Lietuvių", "Įlinkdama fechtuotojo špaga sublykčiojusi pragręžė apvalų arbūzą."),
    ("Indonesia", "Muharjo seorang xenofobia universal yang takut pada warga jazirah, contohnya Qatar."),
]

# Same list as FontCoverageTests.PriceSamples (the test is the source of truth for coverage).
PRICES = [
    "1,99 €", "€1.99", "$1.99", "US$1.99", "CA$2.49", "A$2.99", "NZ$2.99", "HK$15.00", "S$2.98", "MX$39.00",
    "R$ 9,90", "£1.79", "¥300", "CN¥14.00", "₩2,500", "₹149.00", "₽199", "199 ₽", "₺49,99", "49,99 TL",
    "₫49.000", "49.000 ₫", "₱99.00", "₪9.90", "฿39.00", "₦900.00", "₴49,00", "₸990", "₡990", "₲9.900",
    "GH₵19.99", "Rp 15.000", "RM 8.90", "8,99 zł", "29,00 kr", "49,90 Kč", "1 990 Ft", "4,99 lei", "CHF 2.00",
    "CHF 1’000.00", "R 34,99", "SAR 7.99", "AED 7.99", "KSh 250", "Rs 450", "1 234,56 €", "¢99",
]


def _round(weight, size):
    return font(ttf("PocketRound-" + weight), size)


def charset_sheet(weight):
    sys.path.insert(0, HERE)
    from pw_font_glyphs import charset
    chars = charset()
    cols, cell = 20, 96
    rows = (len(chars) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell, rows * cell + 90), CREAM)
    d = ImageDraw.Draw(img)
    d.text((20, 18), f"Pocket Round {weight} · {len(chars)} caractères", font=_round("Bold", 44), fill=INK)
    big, lab = _round(weight, 58), _round("Regular", 13)
    for i, ch in enumerate(chars):
        x, y = (i % cols) * cell, (i // cols) * cell + 90
        d.rectangle((x, y, x + cell - 1, y + cell - 1), outline=(236, 224, 204))
        d.text((x + cell / 2, y + 74), ch, font=big, fill=INK, anchor="ms")
        d.text((x + 4, y + 2), "%04X" % ord(ch), font=lab, fill=(170, 140, 110))
    return img


def languages_sheet(dark):
    W = 2000
    bg, ink, soft = (DARK, CREAM, (200, 175, 150)) if dark else (CREAM, INK, SOFT)
    img = Image.new("RGB", (W, 120 + len(LANGUAGES) * 92), bg)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "Pocket Round · langues de la version 1", font=_round("Bold", 48), fill=ink)
    y = 120
    for name, text in LANGUAGES:
        d.text((40, y + 10), name, font=_round("SemiBold", 26), fill=soft)
        d.text((260, y), text, font=_round("Regular", 40), fill=ink)
        y += 92
    return img


def prices_sheet():
    W, cols = 2000, 4
    rows = (len(PRICES) + cols - 1) // cols
    img = Image.new("RGB", (W, 140 + rows * 70 * 3 + 60), CREAM)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "Prix des stores · SemiBold, Regular, Bold", font=_round("Bold", 48), fill=INK)
    y = 130
    for weight in ("SemiBold", "Regular", "Bold"):
        for i, price in enumerate(PRICES):
            x = 40 + (i % cols) * (W - 80) // cols
            row = i // cols
            d.rounded_rectangle((x, y + row * 70, x + 420, y + row * 70 + 56), 28, fill=ORANGE if weight == "Bold" else PANEL)
            d.text((x + 210, y + row * 70 + 28), price, font=_round(weight, 34), fill=INK, anchor="mm")
        y += rows * 70 + 20
    return img


def sizes_sheet():
    W = 2600
    sizes = [10, 12, 14, 16, 18, 20, 24, 28, 32, 40, 48, 64]
    text = "Nouvelle espèce ! Le lapin roux s'endort près des fleurs. 1,99 € · 12 400"
    block = 40 + sum(int(s * 1.5) + 16 for s in sizes)
    img = Image.new("RGB", (W, 110 + 2 * block), CREAM)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "Tailles en pixels · fond clair et fond sombre", font=_round("Bold", 44), fill=INK)
    d.rectangle((0, 110 + block, W, 110 + 2 * block), fill=DARK)
    for top, ink in ((110, INK), (110 + block, CREAM)):
        y = top + 20
        for s in sizes:
            d.text((40, y), f"{s} px", font=_round("SemiBold", 18), fill=ink)
            d.text((120, y), text, font=_round("Regular", s), fill=ink)
            y += int(s * 1.5) + 16
    return img


def localized_strings():
    import yaml
    tables = {}
    for code in ("fr", "en"):
        with open(os.path.join(LOCALIZATION, f"LOC_{code}.asset"), encoding="utf-8") as f:
            text = f.read()
        body = text[text.index("MonoBehaviour:"):]
        tables[code] = [(e["key"], str(e["value"])) for e in yaml.safe_load(body)["MonoBehaviour"]["entries"]]
    return tables


def strings_sheet():
    tables = localized_strings()
    W = 2000
    n = max(len(v) for v in tables.values())
    img = Image.new("RGB", (W, 120 + n * 46), CREAM)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "Tous les textes du jeu · français et anglais", font=_round("Bold", 44), fill=INK)
    for col, code in enumerate(("fr", "en")):
        x = 40 + col * (W // 2)
        for i, (key, value) in enumerate(tables[code]):
            y = 120 + i * 46
            d.text((x, y + 8), key, font=_round("Regular", 16), fill=(170, 140, 110))
            d.text((x + 330, y), value.replace("\n", " ⏎ "), font=_round("Regular", 30), fill=INK)
    return img


def final_sheets():
    os.makedirs(FINAL_OUT, exist_ok=True)
    out = []

    def keep(img, name):
        path = os.path.join(FINAL_OUT, name)
        img.save(path, optimize=True)
        out.append(path)

    for weight in ("Regular", "SemiBold", "Bold"):
        keep(charset_sheet(weight), f"charset_{weight}.png")
    keep(languages_sheet(False), "languages_light.png")
    keep(languages_sheet(True), "languages_dark.png")
    keep(prices_sheet(), "prices.png")
    keep(sizes_sheet(), "sizes.png")
    keep(strings_sheet(), "strings.png")
    return out


if __name__ == "__main__":
    if sys.argv[1] == "quick":
        print(quick(sys.argv[2]))
    elif sys.argv[1] == "final":
        for p in final_sheets():
            print(p)
    else:
        for p in all_sheets():
            print(p)
