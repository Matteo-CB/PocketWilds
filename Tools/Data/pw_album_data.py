"""Builds the albums' data from doc « 01 Albums et autocollants », sections 2 to 5 (our own script, T44A).

usage: python Tools/Data/pw_album_data.py --spec <path of 01_Albums_autocollants.md>

Reads the markdown tables of the biome albums (section 2), the event pages (3), the season pages (4) and the activity pages
(5) and writes:
  Assets/_Game/Simulation/Albums/AlbumData.Generated.cs  the 16 albums, 60 pages and 720 stickers (index, key, page, slot,
                                                         rarity, drawable or fixed, fixed source and its value, preferred
                                                         source, hint)
  Assets/_Game/Editor/UI/AlbumNames.Generated.cs         the en/fr names of albums, pages and stickers as text rows
Fails loudly (exit 1, the row named) on any table, row, id, rarity, source or name it cannot read: the 720 stickers are
validated data, never guessed.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DATA = os.path.join(ROOT, 'Assets', '_Game', 'Simulation', 'Albums', 'AlbumData.Generated.cs')
OUT_NAMES = os.path.join(ROOT, 'Assets', '_Game', 'Editor', 'UI', 'AlbumNames.Generated.cs')


class DataError(Exception):
    pass


def fail(message):
    raise DataError(message)


# ---------------------------------------------------------------------------------------------------- fixed vocabularies

BIOME_EN = {
    'grassland': 'Meadow', 'forest': 'Forest', 'lake': 'Lake', 'mountain': 'Mountain', 'snow': 'Snow', 'jungle': 'Jungle',
    'desert': 'Desert', 'sakura': 'Sakura', 'mushroom': 'Mushrooms', 'cloudlands': 'Cloud Islands', 'lunar': 'Moonlight',
    'prehistoric': 'Prehistoric', 'starlight': 'Starry Sky',
}

# Zones per biome page 2 (section 2: 8 for the Prairie, 6 for the Snow, 7 for the others).
ZONES = {'grassland': 8, 'snow': 6}
DEFAULT_ZONES = 7

EVENT_EN = {
    101: 'The Auroras', 102: 'The Grand Contest', 103: 'Burrow Awakening', 104: 'The Blooming', 105: 'The Great Migration',
    106: 'The Firefly Festival', 107: 'Sunny Days', 108: 'The Star Shower', 109: 'The Wind Race', 110: 'The Golden Harvest',
    111: 'Mushroom Night', 112: 'The First Frost',
}

SEASON_EN = {1: 'Awakening', 2: 'Sunshine', 3: 'Harvest', 4: 'Frost'}

# S-ACT-001 to 018 (ActivityId values), their French names as the docs write them, the English names of the build.
ACTIVITY_ENUM = {
    1: 'Grooming', 2: 'HideAndSeek', 3: 'NutRain', 4: 'PawMemory', 5: 'Expeditions', 6: 'Garden', 7: 'TreasureFishing',
    8: 'LilyHop', 9: 'Bakery', 10: 'PhotoSafari', 11: 'CuddleMatch', 12: 'Slide', 13: 'Constellations', 14: 'FireflyRhythm',
    15: 'Bubbles', 16: 'TilePuzzle', 17: 'BurrowMaze', 18: 'GrandContest',
}
ACTIVITY_EN = {
    1: 'Grooming', 2: 'Hide and Seek', 3: 'Nut Drop', 4: 'Pawprint Memory', 5: 'Expeditions', 6: 'Garden',
    7: 'Treasure Fishing', 8: 'Lily Hop', 9: 'Bakery', 10: 'Photo Safari', 11: 'Hug Match', 12: 'Slide',
    13: 'Constellations', 14: 'Firefly Rhythm', 15: 'Bubbles', 16: 'Tile Puzzle', 17: 'Burrow Maze', 18: 'Grand Contest',
}
ACTIVITY_FR = {
    'toilettage': 1, 'cache-cache': 2, 'pluie de noisettes': 3, 'memory': 4, 'memory des empreintes': 4,
    'expéditions': 5, 'jardin': 6, 'pêche aux trésors': 7, 'saut de nénuphars': 8, 'pâtisserie': 9, 'safari photo': 10,
    'match des câlins': 11, 'glissade': 12, 'constellations': 13, 'rythme des lucioles': 14, 'bulles': 15, 'taquin': 16,
    'labyrinthe du terrier': 17, 'grand concours': 18,
}
META_ACTIVITIES = {5, 6, 11, 18}

# « Souhait d'un {espèce} »: the species' content id (species.<english>), as the build names them.
SPECIES = {
    'lapin': 'rabbit', 'écureuil': 'squirrel', 'hérisson': 'hedgehog', 'mulot': 'field_mouse', "cochon d'inde": 'guinea_pig',
    'faon': 'fawn', 'renard': 'fox', 'raton laveur': 'raccoon', 'blaireau': 'badger', 'chouette': 'owl',
    'panda roux': 'red_panda', 'marcassin': 'boar_piglet', 'canard': 'duckling', 'grenouille': 'frog', 'loutre': 'otter',
    'castor': 'beaver', 'axolotl': 'axolotl', 'tortue': 'turtle', 'chevreau': 'goat_kid', 'marmotte': 'marmot',
    'lynx': 'lynx', 'aigle': 'eaglet', 'yack': 'yak', 'pika': 'pika', 'renard polaire': 'arctic_fox', 'manchot': 'penguin',
    'ourson polaire': 'polar_bear_cub', 'bébé phoque': 'seal_pup', 'harfang': 'snowy_owl', 'faon de renne': 'reindeer_calf',
    'capybara': 'capybara', 'singe': 'monkey', 'paresseux': 'sloth', 'perroquet': 'parrot', 'bébé tapir': 'tapir',
    'rainette': 'tree_frog', 'gerboise': 'jerboa', 'suricate': 'meerkat', 'chat des sables': 'sand_cat',
    'tatou': 'armadillo', 'gecko léopard': 'leopard_gecko', 'petit oryx': 'oryx', 'grue': 'crane',
    'canard mandarin': 'mandarin_duck', 'macaque': 'macaque', 'martre': 'marten', 'pangolin': 'pangolin',
    'chevrotain': 'chevrotain', 'musaraigne': 'shrew', 'crapaud': 'toad', 'roussette': 'fruit_bat', 'truffon': 'truffon',
    'kiwi': 'kiwi', 'opossum': 'opossum', 'alpaga': 'alpaca', "poussin d'albatros": 'albatross_chick',
    'baleineau des nuages': 'cloud_whale', 'phalanger volant': 'sugar_glider', 'lapin plume': 'feather_rabbit',
    'chinchilla': 'chinchilla', 'louveteau': 'wolf', 'loris lent': 'slow_loris', 'kinkajou': 'kinkajou', 'wombat': 'wombat',
    'moufette': 'skunk', 'engoulevent': 'nightjar', 'petit rex': 'tiny_rex', 'bébé stégosaure': 'stegosaurus',
    'bébé brachiosaure': 'brachiosaurus', 'bébé ptérodactyle': 'pterosaur', 'bébé parasaurolophus': 'parasaurolophus',
    'bébé mammouth': 'mammoth', 'faon des étoiles': 'star_fawn', 'raie des nébuleuses': 'nebula_ray',
    'lémurien des étoiles': 'star_lemur', 'hippocampe des étoiles': 'star_seahorse', 'paon des étoiles': 'star_peafowl',
    'koala des comètes': 'comet_koala',
}
# The roster's content ids (doc 03 « Roster complet », « ID de contenu »): one word, no underscore; the young animals keep
# their roster names (calf, pup, hatchling...).
ROSTER_IDS = {
    'duckling': 'duck', 'eaglet': 'eagle', 'yak': 'yakcalf', 'tapir': 'tapircalf', 'tree_frog': 'redeyedfrog',
    'oryx': 'oryxcalf', 'crane': 'cranechick', 'mandarin_duck': 'mandarinduckling', 'alpaca': 'alpacacria',
    'cloud_whale': 'cloudwhalecalf', 'wolf': 'wolfpup', 'tiny_rex': 'rexhatchling', 'stegosaurus': 'stegosaurushatchling',
    'brachiosaurus': 'brachiosaurushatchling', 'pterosaur': 'pterosaurhatchling',
    'parasaurolophus': 'parasaurolophushatchling', 'mammoth': 'mammothcalf', 'star_peafowl': 'starpeachick',
}
SPECIES = {name: ROSTER_IDS.get(english, english.replace('_', '')) for name, english in SPECIES.items()}

# « Tirage pendant {météo} » (biome weather and light) and « Tirage de saison pendant {météo} »: the weather's key.
WEATHER = {
    'pluie fine': 'light_rain', 'arc-en-ciel': 'rainbow', 'brume du matin': 'morning_mist', 'rayons de soleil': 'sun_rays',
    'voile sur la lune': 'moon_veil', 'voile de nébuleuse': 'nebula_veil', 'vent de plumes': 'feather_wind', 'vent': 'wind',
    'souffle du volcan': 'volcano_breath', 'passage des nuages': 'passing_clouds', 'pluie tropicale': 'tropical_rain',
    'pluie de spores': 'spore_rain', "pluie d'étoiles filantes": 'shooting_star_shower', 'neige légère': 'light_snow',
    'mer de nuages': 'sea_of_clouds', 'brume bleue': 'blue_mist', 'brume argentée': 'silver_mist', 'brume': 'mist',
    'bruine de printemps': 'spring_drizzle', 'brise de sable': 'sand_breeze', 'bourrasque de pétales': 'petal_gust',
    "averse du désert": 'desert_shower', 'averse chaude': 'warm_shower', 'petite pluie': 'small_rain',
    'lumière chaude': 'warm_light', 'chute des feuilles': 'falling_leaves', 'givre': 'frost', 'neige fine': 'fine_snow',
}

RARITY = {'Commun': 'Common', 'Rare': 'Rare', 'Brillant': 'Shiny'}
FORBIDDEN = ['\u2014', '\u2728', '\u2726', '\u2727']  # em dash in French, the four-point sparkle and its kin


# ---------------------------------------------------------------------------------------------------- markdown reading

def cells(line):
    line = line.strip()
    if not (line.startswith('|') and line.endswith('|')):
        return None
    return [c.strip() for c in line[1:-1].split('|')]


def strip_article(text):
    return re.sub(r"^(la |le |les |l'|l’|une |un |des |du |de la |de l'|d'un |d'une )", '', text.strip(), flags=re.I)


class Sticker:
    def __init__(self, code, fr, en, rarity, source, line):
        self.code = code
        self.fr = fr
        self.en = en
        self.rarity = rarity
        self.source = source
        self.line = line
        self.drawable = True
        self.fixed = 'None'
        self.fixed_value = 0
        self.pref = 'None'
        self.pref_activity = 0
        self.pref_subject = ''
        self.hint = 'Activity'


class Page:
    def __init__(self, key, kind, number, fr, en, subject, line):
        self.key = key
        self.kind = kind
        self.number = number
        self.fr = fr
        self.en = en
        self.subject = subject
        self.line = line
        self.stickers = []


def read_rows(lines, start, prefix, first=1):
    """The 12 sticker rows of the table that follows line `start` (a heading), each id beginning with `prefix`."""
    rows = []
    i = start + 1
    header_seen = False
    while i < len(lines):
        text = lines[i]
        if text.startswith('#'):
            break
        c = cells(text)
        if c is not None:
            if c[0] == 'ID':
                if len(c) != 6:
                    fail(f'line {i + 1}: a sticker table needs 6 columns, found {len(c)}')
                header_seen = True
            elif header_seen and c[0].startswith('STK-'):
                if len(c) != 6:
                    fail(f'line {i + 1}: {c[0]} has {len(c)} cells, 6 expected')
                if not c[0].startswith(prefix):
                    fail(f'line {i + 1}: {c[0]} is not in {prefix}')
                rarity = RARITY.get(c[4])
                if rarity is None:
                    fail(f'line {i + 1}: {c[0]} unknown rarity « {c[4]} »')
                rows.append(Sticker(c[0], c[1], c[2], rarity, c[5], i + 1))
            elif header_seen and not c[0].startswith('-'):
                fail(f'line {i + 1}: unreadable row « {text.strip()[:60]} »')
        elif header_seen and rows and text.strip():
            break
        i += 1
    if not header_seen:
        fail(f'line {start + 1}: no sticker table under « {lines[start].strip()} »')
    if len(rows) != 12:
        fail(f'line {start + 1}: {len(rows)} stickers under « {lines[start].strip()} », 12 expected')
    for n, s in enumerate(rows):
        expected = f'{prefix}{n + first:02d}'
        if s.code != expected:
            fail(f'line {s.line}: {s.code} found where {expected} was expected')
    return rows


def section_bounds(lines, number):
    start = next((i for i, l in enumerate(lines) if l.startswith(f'## {number}. ')), None)
    if start is None:
        fail(f'section {number} not found')
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith('## ')), len(lines))
    return start, end


# ---------------------------------------------------------------------------------------------------- the four sections

def parse_biomes(lines):
    start, end = section_bounds(lines, 2)
    biomes = []
    for i in range(start, end):
        c = cells(lines[i])
        if c and len(c) == 6 and re.fullmatch(r'\d+', c[0]):
            m = re.fullmatch(r'biome\.([a-z]+)', c[2])
            p = re.fullmatch(r'STK-([A-Z]{2})', c[3])
            if not m or not p or c[5] != f'album.{m.group(1)}':
                fail(f'line {i + 1}: unreadable biome row')
            biomes.append((int(c[0]), c[1], m.group(1), p.group(1)))
    if [b[0] for b in biomes] != list(range(1, 14)):
        fail('section 2: the 13 biomes in order are not all there')
    pages = []
    for order, fr, slug, prefix in biomes:
        if slug not in BIOME_EN:
            fail(f'biome {slug} has no English name')
        for n in (1, 2):
            key = f'page.{slug}.{n}'
            at = next((i for i in range(start, end) if lines[i].startswith('#### ') and f'({key})' in lines[i]), None)
            if at is None:
                fail(f'section 2: heading of {key} not found')
            m = re.match(r'#### Page \d : (.+?) \(', lines[at])
            if not m:
                fail(f'line {at + 1}: unreadable page heading')
            page = Page(key, 'Biome', n, m.group(1), 'Residents' if n == 1 else 'The Biome', f'biome.{slug}', at + 1)
            page.album_key = f'album.{slug}'
            page.album_fr = fr
            page.album_en = BIOME_EN[slug]
            # Page 2's ids are 13 to 24.
            page.stickers = read_rows(lines, at, f'STK-{prefix}-', 1 if n == 1 else 13)
            page.biome_slug = slug
            pages.append(page)
    return pages


def parse_events(lines):
    start, end = section_bounds(lines, 3)
    pages = []
    for i in range(start, end):
        m = re.match(r'### 3\.\d+ P-EVT-(1\d\d) (.+) \(STK-EVT-(1\d\d)\)', lines[i])
        if not m:
            continue
        number = int(m.group(1))
        if number != int(m.group(3)) or number not in EVENT_EN:
            fail(f'line {i + 1}: unreadable event heading')
        page = Page(f'page.event.{number}', 'Event', number, m.group(2), EVENT_EN[number], '', i + 1)
        page.stickers = read_rows(lines, i, f'STK-EVT-{number}-')
        pages.append(page)
    if [p.number for p in pages] != list(range(101, 113)):
        fail('section 3: the 12 event pages 101 to 112 are not all there')
    return pages


def parse_seasons(lines):
    start, end = section_bounds(lines, 4)
    pages = []
    for i in range(start, end):
        m = re.match(r'### 4\.\d (.+?) \(STK-SEA-(\d)\)', lines[i])
        if not m:
            continue
        number = int(m.group(2))
        page = Page(f'page.season.{number}', 'Season', number, m.group(1), SEASON_EN[number], '', i + 1)
        page.stickers = read_rows(lines, i, f'STK-SEA-{number}-')
        pages.append(page)
    if [p.number for p in pages] != [1, 2, 3, 4]:
        fail('section 4: the 4 season pages are not all there')
    return pages


def parse_activities(lines):
    start, end = section_bounds(lines, 5)
    pages = []
    for i in range(start, end):
        m = re.match(r'### 5\.\d+ S-ACT-(0\d\d) (.+) \(STK-ACT-(0\d\d)\)', lines[i])
        if not m:
            continue
        number = int(m.group(1))
        if number != int(m.group(3)):
            fail(f'line {i + 1}: unreadable activity heading')
        page = Page(f'page.activity.{number:03d}', 'Activity', number, m.group(2), ACTIVITY_EN[number], '', i + 1)
        page.stickers = read_rows(lines, i, f'STK-ACT-{number:03d}-')
        pages.append(page)
    if [p.number for p in pages] != list(range(1, 19)):
        fail('section 5: the 18 activity pages are not all there')
    return pages


# ---------------------------------------------------------------------------------------------------- sources

def activity_of(name, where):
    key = name.strip().lower()
    if key not in ACTIVITY_FR:
        fail(f'{where}: unknown activity « {name} »')
    return ACTIVITY_FR[key]


def weather_of(name, where):
    key = strip_article(name).lower()
    if key not in WEATHER:
        fail(f'{where}: unknown weather « {name} »')
    return WEATHER[key]


def drawable_preference(s, season, where):
    """The preferred source of a drawable sticker (section 1.6 table) and its hint."""
    src = s.source.strip()
    m = re.match(r"^Offert à l'ouverture des albums, puis (.+)$", src)
    if m:
        s.fixed = 'Offered'
        src = m.group(1)
    if src in ('Tirage, garantie Brillant', 'Tirage de saison, garantie Brillant'):
        s.pref, s.hint = 'None', 'Activity'
        return
    m = re.match(r"^Souhait d['’](?:un |une )?(.+)$", src)
    if m:
        sp = m.group(1).strip().lower()
        if sp not in SPECIES:
            fail(f'{where}: unknown species « {m.group(1)} »')
        s.pref, s.pref_subject, s.hint = 'Wish', 'species.' + SPECIES[sp], 'Wish'
        return
    if src.startswith('Cadeau du jour'):
        s.pref, s.hint = 'DailyGift', 'Gift'
        return
    if src == 'Premier moment signature vu (garanti), puis tirage pendant le moment signature':
        s.fixed, s.pref, s.hint = 'SignatureMoment', 'SignatureMoment', 'World'
        return
    m = re.match(r"^Événement du monde", src)
    if m:
        s.pref, s.hint = 'WorldEvent', 'World'
        return
    if not season:
        if src == 'Tirage le matin':
            s.pref, s.hint = 'Morning', 'World'
            return
        if src == 'Tirage le soir':
            s.pref, s.hint = 'Evening', 'World'
            return
        m = re.match(r'^Tirage pendant (.+)$', src)
        if m:
            s.pref, s.pref_subject, s.hint = 'Weather', weather_of(m.group(1), where), 'World'
            return
        m = re.match(r"^Récolte du Jardin dans .+$", src)
        if m:
            s.pref, s.pref_activity, s.hint = 'GardenInBiome', 6, 'Garden'
            return
        m = re.match(r"^(.+?) dans (?:la |le |les |l'|l’)(.+)$", src)
        if m:
            s.pref, s.pref_activity, s.hint = 'ActivityInBiome', activity_of(m.group(1), where), 'Activity'
            return
    else:
        if src == 'Tirage de saison le matin':
            s.pref, s.hint = 'Morning', 'World'
            return
        if src == 'Tirage de saison le soir':
            s.pref, s.hint = 'Evening', 'World'
            return
        m = re.match(r'^Tirage de saison pendant (.+)$', src)
        if m:
            s.pref, s.pref_subject, s.hint = 'SeasonWeather', weather_of(m.group(1), where), 'World'
            return
        m = re.match(r"^Tirage de saison d['’](?:un |une )(.+)$", src)
        if m:
            what = m.group(1).strip()
            low = what.lower()
            if low == 'expédition':
                s.pref, s.hint = 'SeasonExpedition', 'Expedition'
            elif low == 'souhait':
                s.pref, s.hint = 'SeasonWish', 'Wish'
            elif low == "partie d'activité":
                s.pref, s.hint = 'SeasonActivity', 'Activity'
            elif low == 'récolte du jardin':
                s.pref, s.pref_activity, s.hint = 'SeasonGarden', 6, 'Garden'
            else:
                mm = re.match(r'^partie de (.+)$', what)
                s.pref, s.pref_activity, s.hint = 'SeasonActivity', activity_of(mm.group(1) if mm else what, where), 'Activity'
            return
    fail(f'{where}: unreadable preferred source « {src} »')


def classify(pages):
    for page in pages:
        for slot, s in enumerate(page.stickers):
            where = f'line {s.line} ({s.code})'
            src = s.source.strip()
            if page.kind == 'Biome' and page.number == 1:
                if slot == 6:
                    if src != "Offert en silhouette à l'ouverture de l'album":
                        fail(f'{where}: slot 07 must be the companion offered in silhouette')
                    s.drawable, s.fixed, s.hint = False, 'Companion', 'World'
                else:
                    drawable_preference(s, False, where)
            elif page.kind == 'Biome':
                zones = ZONES.get(page.biome_slug, DEFAULT_ZONES)
                if slot < zones:
                    ok = (re.match(r"^Ouverture de l'album du biome \(zone d'arrivée\)", src) and slot == 0) \
                        or (re.match(r"^Ouverture de la zone \(agrandissement (\d)\)$", src) and slot > 0) \
                        or (re.match(r"^Ouverture d.+ \(zone de passage\)$", src) and slot == zones - 1)
                    if not ok:
                        fail(f'{where}: « {src} » is not the zone {slot} of the biome')
                    m = re.match(r"^Ouverture de la zone \(agrandissement (\d)\)$", src)
                    if m and int(m.group(1)) != slot:
                        fail(f'{where}: expansion {m.group(1)} in zone slot {slot}')
                    s.drawable, s.fixed, s.fixed_value, s.hint = False, 'Zone', slot, 'Zone'
                else:
                    if src.startswith('Ouverture'):
                        fail(f'{where}: a zone past the {zones} zones of {page.biome_slug}')
                    drawable_preference(s, False, where)
                    if slot == 11 and s.fixed != 'SignatureMoment':
                        fail(f'{where}: sticker 24 must be the signature moment')
            elif page.kind == 'Event':
                s.drawable = False
                m = re.match(r'^Parcours, palier (\d+)$', src)
                if m:
                    s.fixed, s.fixed_value, s.hint = 'EventTrack', int(m.group(1)), 'Event'
                    continue
                m = re.match(r'^Défis, (\d+) étoiles$', src)
                if m:
                    s.fixed, s.fixed_value, s.hint = 'EventChallengeStars', int(m.group(1)), 'Challenge'
                    continue
                m = re.match(r'^Quête longue [ABC] \((\d+)e\)$', src)
                if m:
                    s.fixed, s.fixed_value, s.hint = 'EventQuest', int(m.group(1)), 'Event'
                    continue
                fail(f'{where}: unreadable event source « {src} »')
            elif page.kind == 'Season':
                if slot < 3:
                    if not re.match(r"^Premier toucher d", src):
                        fail(f'{where}: slots 01 to 03 are the rare visitors')
                    s.drawable, s.fixed, s.fixed_value, s.hint = False, 'Visitor', slot + 1, 'Visitor'
                else:
                    drawable_preference(s, True, where)
            else:
                activity_source(page, slot, s, src, where)
    return pages


def activity_source(page, slot, s, src, where):
    act = page.number
    drawable_slots = {0, 1, 2, 3, 4, 5, 7}
    if slot in drawable_slots:
        allowed = {5: "Tirage d'expédition", 6: 'Récolte', 11: 'Chance par étoiles, 10 niveaux', 18: 'Inscription ou votes'}
        expected = allowed.get(act, 'Chance par étoiles, record')
        if src != expected:
            fail(f'{where}: « {src} » where « {expected} » was expected')
        s.hint = {5: 'Expedition', 6: 'Garden'}.get(act, 'Activity')
        return
    s.drawable = False
    rules = []
    if act not in META_ACTIVITIES:
        rules = [(r'^Première partie à 3 étoiles$', 'ThreeStars', None, 'Challenge'),
                 (r'^(\d+) étoiles de défi', 'ChallengeStars', 1, 'Challenge'),
                 (r'^(\d+)e nouveau record$', 'Records', 1, 'Record')]
    elif act == 5:
        rules = [(r'^Première expédition terminée$', 'ExpeditionsDone', 'one', 'Expedition'),
                 (r'^(\d+) expéditions terminées$', 'ExpeditionsDone', 1, 'Expedition'),
                 (r'^Expéditions vers (\d+) biomes différents$', 'ExpeditionBiomes', 1, 'Expedition'),
                 (r'^(\d+) expéditions de 12 h terminées$', 'LongExpeditions', 1, 'Expedition')]
    elif act == 6:
        rules = [(r'^Première récolte$', 'Harvests', 'one', 'Garden'),
                 (r'^(\d+) récoltes$', 'Harvests', 1, 'Garden'),
                 (r'^(\d+) plantes récoltées$', 'PlantsHarvested', 1, 'Garden'),
                 (r'^(\d+) teintures obtenues$', 'DyesObtained', 1, 'Garden'),
                 (r'^Jardin niveau (\d+)$', 'GardenLevel', 1, 'Garden')]
    elif act == 11:
        rules = [(r'^Niveau (\d+)$', 'CuddleLevel', 1, 'Activity')]
    elif act == 18:
        rules = [(r'^Première inscription$', 'ContestEntries', 'one', 'Activity'),
                 (r'^(\d+) inscriptions$', 'ContestEntries', 1, 'Activity'),
                 (r'^Ruban de bronze$', 'ContestRibbon', 'bronze', 'Activity'),
                 (r"^Ruban d'argent$", 'ContestRibbon', 'silver', 'Activity'),
                 (r"^Ruban d'or$", 'ContestRibbon', 'gold', 'Activity')]
    for pattern, kind, group, hint in rules:
        m = re.match(pattern, src)
        if not m:
            continue
        if group is None or group == 'one':
            value = 3 if kind == 'ThreeStars' else 1
        elif group in ('bronze', 'silver', 'gold'):
            value = {'bronze': 1, 'silver': 2, 'gold': 3}[group]
        else:
            value = int(m.group(group))
        s.fixed, s.fixed_value, s.hint = kind, value, hint
        if kind == 'ThreeStars' and slot != 6:
            fail(f'{where}: the first 3-star game is slot 07')
        return
    fail(f'{where}: unreadable fixed source « {src} »')


# ---------------------------------------------------------------------------------------------------- checks and output

def check(pages):
    if len(pages) != 60:
        fail(f'{len(pages)} pages, 60 expected')
    keys = set()
    for page in pages:
        counts = {'Common': 0, 'Rare': 0, 'Shiny': 0}
        for s in page.stickers:
            counts[s.rarity] += 1
            key = sticker_key(s.code)
            if key in keys:
                fail(f'line {s.line}: duplicate id {key}')
            keys.add(key)
            for text in (s.fr, s.en):
                if not text:
                    fail(f'line {s.line}: {s.code} has an empty name')
                for bad in FORBIDDEN:
                    if bad in text:
                        fail(f'line {s.line}: {s.code} name holds a forbidden character U+{ord(bad):04X}')
        if counts != {'Common': 7, 'Rare': 4, 'Shiny': 1}:
            fail(f'{page.key}: {counts}, 7/4/1 expected')
        shiny = [s for s in page.stickers if s.rarity == 'Shiny']
        if page.kind in ('Biome', 'Season') and not shiny[0].drawable:
            fail(f'{page.key}: its Shiny must be drawable (Shiny guarantee)')
    if len(keys) != 720:
        fail(f'{len(keys)} stickers, 720 expected')


def sticker_key(code):
    m = re.fullmatch(r'STK-([A-Z]+)-(?:(\d+)-)?(\d\d)', code)
    if not m:
        fail(f'unreadable id {code}')
    head = m.group(1).lower()
    return f'sticker.{head}.{m.group(2)}.{m.group(3)}' if m.group(2) else f'sticker.{head}.{m.group(3)}'


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def albums_of(pages):
    albums = []
    for page in pages:
        if page.kind == 'Biome':
            if page.number == 1:
                albums.append((page.album_key, 'Biome', page.subject, page.album_en, page.album_fr))
        elif page.kind == 'Event' and page.number == 101:
            albums.append(('album.event', 'Event', '', 'Events', 'Événements'))
        elif page.kind == 'Season' and page.number == 1:
            albums.append(('album.season', 'Season', '', 'Seasons', 'Saisons'))
        elif page.kind == 'Activity' and page.number == 1:
            albums.append(('album.activity', 'Activity', '', 'Activities', 'Activités'))
    if len(albums) != 16:
        fail(f'{len(albums)} albums, 16 expected')
    return albums


def write_data(pages, albums):
    album_index = {a[0]: i for i, a in enumerate(albums)}
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_album_data.py from doc « 01 Albums et autocollants », sections 2 to 5. Do not edit by hand:')
    w('// change the doc and run the script again (it fails on any table it cannot read).')
    w('// </auto-generated>')
    w('#nullable enable')
    w('')
    w('namespace PocketWilds.Simulation')
    w('{')
    w('    public static partial class AlbumData')
    w('    {')
    w(f'        public const int AlbumCount = {len(albums)};')
    w(f'        public const int PageCount = {len(pages)};')
    w(f'        public const int StickerCount = {sum(len(p.stickers) for p in pages)};')
    w('')
    w('        private static readonly AlbumRow[] AlbumRows =')
    w('        {')
    first_page = {}
    page_count = {}
    for i, page in enumerate(pages):
        a = page.album_key if page.kind == 'Biome' else f'album.{page.kind.lower()}'
        first_page.setdefault(a, i)
        page_count[a] = page_count.get(a, 0) + 1
    for key, kind, subject, en, fr in albums:
        w(f'            new AlbumRow({cs(key)}, AlbumKind.{kind}, {cs(subject)}, {first_page[key]}, {page_count[key]}),')
    w('        };')
    w('')
    w('        private static readonly PageRow[] PageRows =')
    w('        {')
    for i, page in enumerate(pages):
        a = page.album_key if page.kind == 'Biome' else f'album.{page.kind.lower()}'
        w(f'            new PageRow({cs(page.key)}, {album_index[a]}, AlbumKind.{page.kind}, {page.number}, {cs(page.subject)}),')
    w('        };')
    w('')
    w('        private static readonly StickerRow[] StickerRows =')
    w('        {')
    for p, page in enumerate(pages):
        w(f'            // {page.key}')
        for slot, s in enumerate(page.stickers):
            act = f'ActivityId.{ACTIVITY_ENUM[s.pref_activity]}' if s.pref_activity else 'ActivityId.None'
            w(f'            new StickerRow({cs(sticker_key(s.code))}, {p}, {slot}, StickerRarity.{s.rarity}, '
              f'{"true" if s.drawable else "false"}, StickerFixedSource.{s.fixed}, {s.fixed_value}, '
              f'StickerPreference.{s.pref}, {act}, {cs(s.pref_subject)}, StickerHint.{s.hint}),')
    w('        };')
    w('    }')
    w('}')
    return '\n'.join(out) + '\n'


def write_names(pages, albums):
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_album_data.py from doc « 01 Albums et autocollants », sections 2 to 5. Do not edit by hand.')
    w('// </auto-generated>')
    w('namespace PocketWilds.Editor.UI')
    w('{')
    w('    /// <summary>The names of the 16 albums, 60 pages and 720 stickers: rows { key, English, French }.</summary>')
    w('    public static class StickerTexts')
    w('    {')
    w('        private static string[,] _texts;')
    w('')
    w('        /// <summary>Built on first use (the screen pipeline finds this class by reflection).</summary>')
    w('        public static string[,] Texts => _texts ??= Build();')
    w('')
    w('        private static string[,] Build() => new string[,]')
    w('        {')
    for key, kind, subject, en, fr in albums:
        w(f'            {{ {cs(key + ".name")}, {cs(en)}, {cs(fr)} }},')
    for page in pages:
        w(f'            {{ {cs(page.key + ".name")}, {cs(page.en)}, {cs(page.fr)} }},')
    for page in pages:
        for s in page.stickers:
            w(f'            {{ {cs(sticker_key(s.code) + ".name")}, {cs(s.en)}, {cs(s.fr)} }},')
    w('        };')
    w('    }')
    w('}')
    return '\n'.join(out) + '\n'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--spec', required=True)
    args = parser.parse_args()
    with open(args.spec, encoding='utf-8') as f:
        lines = f.read().splitlines()
    try:
        pages = parse_biomes(lines) + parse_events(lines) + parse_seasons(lines) + parse_activities(lines)
        classify(pages)
        check(pages)
        for page in pages:
            for bad in FORBIDDEN:
                if bad in page.fr or bad in page.en:
                    fail(f'{page.key}: page name holds a forbidden character')
        albums = albums_of(pages)
    except DataError as e:
        print('pw_album_data: ' + str(e), file=sys.stderr)
        return 1
    for path, text in ((OUT_DATA, write_data(pages, albums)), (OUT_NAMES, write_names(pages, albums))):
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    drawable = sum(1 for p in pages for s in p.stickers if s.drawable)
    print(f'pw_album_data: {len(albums)} albums, {len(pages)} pages, {sum(len(p.stickers) for p in pages)} stickers '
          f'({drawable} drawable) written.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
