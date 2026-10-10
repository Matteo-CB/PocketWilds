"""Builds the profile rewards' data from doc « 01 Succès, titres, cadres, poses et journal », sections 2 to 5 (our own
script, T44B).

usage: python Tools/Data/pw_profile_data.py --spec <path of 01_Succes_titres_cadres_poses_journal.md>

Reads the markdown tables of the 200 achievements (section 2), the titles (3), the profile frames (4) and the photo poses
(5) and writes:
  Assets/_Game/Simulation/Achievements/ProfileData.Generated.cs  the 200 achievements (key, category, counter and its
                                                                 parameter, four rising thresholds, the Gold decoration,
                                                                 the Star extra), the 322 titles, 42 frames and 47 poses
                                                                 (key, source, its value and subject, gallery order)
  Assets/_Game/Editor/UI/ProfileNames.Generated.cs               the en/fr texts as rows { key, English, French }: names,
                                                                 goal lines with {n}, source lines, category names
Fails loudly (exit 1, the row named) on any table, row, id, threshold, source or name it cannot read, on thresholds that
do not rise and on duplicate ids: the 200 achievements are validated data, never guessed.
"""
import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pw_album_data import ACTIVITY_EN, EVENT_EN, SPECIES  # noqa: E402  (the build's names, shared with T44A)

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DATA = os.path.join(ROOT, 'Assets', '_Game', 'Simulation', 'Achievements', 'ProfileData.Generated.cs')
OUT_NAMES = os.path.join(ROOT, 'Assets', '_Game', 'Editor', 'UI', 'ProfileNames.Generated.cs')
ALBUM_NAMES = os.path.join(ROOT, 'Assets', '_Game', 'Editor', 'UI', 'AlbumNames.Generated.cs')


class DataError(Exception):
    pass


def fail(message):
    raise DataError(message)


# ---------------------------------------------------------------------------------------------------- fixed vocabularies

CATEGORIES = [
    ('Collection', 'Collection', 'Collection', 'Collection'),
    ('Biomes', 'Biomes', 'Biomes', 'Biomes'),
    ('Maîtrise', 'Mastery', 'Mastery', 'Maîtrise'),
    ('Éclosions', 'Hatching', 'Hatching', 'Éclosions'),
    ('Activités', 'Activities', 'Activities', 'Activités'),
    ('Rendez-vous du jour', 'Daily', 'Daily Visit', 'Rendez-vous du jour'),
    ('Expéditions et Jardin', 'ExpeditionsGarden', 'Expeditions and Garden', 'Expéditions et Jardin'),
    ('Albums', 'Albums', 'Albums', 'Albums'),
    ('Décoration', 'Decoration', 'Decoration', 'Décoration'),
    ('Liens et compagnons', 'Bonds', 'Bonds and Companions', 'Liens et compagnons'),
    ('Événements', 'Events', 'Events', 'Événements'),
    ('Renouveau et Essence', 'Renewal', 'Renewal and Essence', 'Renouveau et Essence'),
    ('Refuge', 'Refuge', 'Refuge', 'Refuge'),
]
EXPECTED_PER_CATEGORY = [20, 52, 10, 12, 26, 10, 12, 10, 12, 12, 10, 8, 6]

# The 13 biomes in their order (AlbumData.BiomeIndex): code of the decorations, French name in the tables.
BIOMES = [
    ('GL', 'grassland', 'Prairie'), ('FO', 'forest', 'Forêt'), ('LA', 'lake', 'Lac'), ('MO', 'mountain', 'Montagne'),
    ('SN', 'snow', 'Neige'), ('JU', 'jungle', 'Jungle'), ('DE', 'desert', 'Désert'), ('SA', 'sakura', 'Sakura'),
    ('MU', 'mushroom', 'Champignons'), ('CL', 'cloudlands', 'Îles des nuages'), ('LU', 'lunar', 'Lunaire'),
    ('PR', 'prehistoric', 'Préhistorique'), ('SL', 'starlight', 'Ciel étoilé'),
]
BIOME_TYPES = {'Éclosions': ('BiomeHatches', 17), 'Variantes': ('BiomeVariants', 18),
               'Expéditions': ('BiomeExpeditions', 19), 'Liens': ('BiomeBonds', 20)}

# The counter each achievement follows (AchievementCounter in AchievementData.cs) and its parameter (an activity number).
COUNTERS = {
    1: 'SpeciesDiscovered', 2: 'VariantsDiscovered', 3: 'BaseSetsComplete', 4: 'SecretVariants', 5: 'TraitTiny',
    6: 'TraitGiant', 7: 'TraitShiny', 8: 'TraitGolden', 9: 'TraitAlbino', 10: 'TraitMelanistic', 11: 'SpeciesTraitPairs',
    12: 'RarityRare', 13: 'RarityEpic', 14: 'RarityLegendary', 15: 'RarityFabulous', 16: 'VisitorsKept',
    17: 'BiomeCodexComplete', 18: 'CodexPercent', 19: 'AnimalsNamed', 20: 'Favorites',
    73: 'SpeciesMastery5', 74: 'SpeciesMastery10', 75: 'SpeciesMastery20', 76: 'SpeciesMastery30',
    77: 'MasteryMilestones', 78: 'MasteryVariants', 79: 'BiomeMastery12', 80: 'BiomeMastery36',
    81: 'EventSpeciesMastery10', 82: 'MasteryLevels',
    83: 'Hatches', 84: 'FocusHatches', 85: 'DiscoveryHatches', 86: 'EventEggs', 87: 'HatchBiomes', 88: 'NewbornTaps',
    89: 'BreedingHatches', 90: 'BreedingMutations', 91: 'ThirdGeneration', 92: 'BestDayHatches', 93: 'FullEventEggs',
    94: 'HatchRarities',
    95: ('ActivityProgress', 1), 96: ('ActivityProgress', 2), 97: ('ActivityProgress', 3), 98: ('ActivityProgress', 4),
    99: ('ActivityProgress', 7), 100: ('ActivityProgress', 8), 101: ('ActivityProgress', 9),
    102: ('ActivityProgress', 10), 103: ('ActivityProgress', 11), 104: ('ActivityProgress', 12),
    105: ('ActivityProgress', 13), 106: ('ActivityProgress', 14), 107: ('ActivityProgress', 15),
    108: ('ActivityProgress', 16), 109: ('ActivityProgress', 17), 110: ('ActivityProgress', 18),
    111: 'ActivityStars', 112: 'ChallengesThreeStars', 113: 'PersonalBests', 114: 'ActivitiesStarred',
    115: 'PracticeGames', 116: 'PerfectActivities', 117: 'ThreeStarGames', 118: 'AnimalsPlayed',
    119: 'ActivityBondPoints', 120: 'ActivityStickers',
    121: 'DailyGifts', 122: 'WishesGranted', 123: 'CalendarDays', 124: 'CalendarsComplete', 125: 'JournalsComplete',
    126: 'JournalGoals', 127: 'AnimalsWished', 128: 'CakeWishes', 129: 'VisitorsTapped', 130: 'FullWishDays',
    131: 'ExpeditionsDone', 132: 'ExpeditionHours', 133: 'Destinations', 134: 'LongExpeditions',
    135: 'TeamExpeditions', 136: 'ExpeditionDiscoveryEggs', 137: 'SeedsPlanted', 138: 'FlowersHarvested',
    139: 'FruitsHarvested', 140: 'DyesHarvested', 141: 'PlantsHarvested', 142: 'SproutsWatered',
    143: 'StickersOwned', 144: 'PagesComplete', 145: 'BiomeAlbumsComplete', 146: 'EventPagesComplete',
    147: 'AlbumStarsEarned', 148: 'StickersPicked', 149: 'RecordStickers', 150: 'BiomePagesHalf',
    151: 'SeasonPagesComplete', 152: 'ExpeditionStickers',
    153: 'DecorationsOwned', 154: 'DecorationsPlaced', 155: 'CommonDecorations', 156: 'ProgressionDecorations',
    157: 'LandmarksPlaced', 158: 'CozyAreas', 159: 'Naps', 160: 'Sits', 161: 'Meals', 162: 'Perches', 163: 'Recolors',
    164: 'SouvenirsPlaced',
    165: 'Bond1Animals', 166: 'Bond3Animals', 167: 'Bond5Animals', 168: 'BondPoints', 169: 'Pets', 170: 'AnimalsPetted',
    171: 'AccessoriesEquipped', 172: 'FullOutfits', 173: 'Photos', 174: 'PosesUsed', 175: 'TreatsGiven',
    176: 'EventAnimalsBond5',
    177: 'EventsJoined', 178: 'EventPathTiers', 179: 'FeaturedAnimals', 180: 'FreePathsComplete', 181: 'EventCurrency',
    182: 'EventQuests', 183: 'FeaturedChallengeStars', 184: 'ChallengeOutfits', 185: 'SharedGoals',
    186: 'EventVariants',
    187: 'Renewals', 188: 'EssenceLevels', 189: 'BiomesAtRenewal', 190: 'RunHatches', 191: 'EssenceRecords',
    192: 'ProductionBeaten', 193: 'EssenceMilestones', 194: 'RunLightLevels',
    195: 'RefugeLevel', 196: 'BiomesUnlocked', 197: 'ZonesOpen', 198: 'LightLevels', 199: 'RefugeSeasons',
    200: 'JournalPages',
}

# Event species: their content ids as the roster writes them (doc 03, « ID de contenu »: one word, Ermine and Chickadee).
EVENT_SPECIES = {
    'stoat': 'ermine', 'angorarabbit': 'angorarabbit', 'mole': 'mole', 'hummingbird': 'hummingbird',
    'storkchick': 'storkchick', 'dormouse': 'dormouse', 'quokka': 'quokka', 'fennec': 'fennec',
    'flyingsquirrel': 'flyingsquirrel', 'chipmunk': 'chipmunk', 'firesalamander': 'firesalamander', 'tit': 'chickadee',
}
SPECIES_ALIASES = {'rainette aux yeux rouges': 'rainette'}

FORBIDDEN = [chr(0x2014), chr(0x2728), chr(0x2726), chr(0x2727)]  # em dash in French, the four-point sparkle and its kin


# ---------------------------------------------------------------------------------------------------- markdown reading

def cells(line):
    line = line.strip()
    if not (line.startswith('|') and line.endswith('|')):
        return None
    return [c.strip() for c in line[1:-1].split('|')]


def section_bounds(lines, number):
    start = next((i for i, l in enumerate(lines) if l.startswith(f'## {number}. ')), None)
    if start is None:
        fail(f'section {number} not found')
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith('## ')), len(lines))
    return start, end


def table_rows(lines, start, end, columns, first_cell):
    """Rows of the first table after `start` (before `end` or the next heading) whose header begins with `first_cell`."""
    rows = []
    header = False
    for i in range(start + 1, end):
        text = lines[i]
        if text.startswith('#'):
            break
        c = cells(text)
        if c is None:
            if header and rows and text.strip():
                break
            continue
        if not header:
            if c[0] == first_cell:
                if len(c) != columns:
                    fail(f'line {i + 1}: a table of {columns} columns expected, found {len(c)}')
                header = True
            continue
        if set(c[0]) <= {'-'}:
            continue
        if len(c) != columns:
            fail(f'line {i + 1}: {len(c)} cells, {columns} expected « {text.strip()[:60]} »')
        rows.append((i + 1, c))
    if not header:
        fail(f'line {start + 1}: no table under « {lines[start].strip()} »')
    return rows


def number(text, where):
    t = text.replace(' ', '').replace(' ', '').replace(' ', '')
    if not re.fullmatch(r'\d+', t):
        fail(f'{where}: unreadable threshold « {text} »')
    return int(t)


def heading_at(lines, start, end, prefix):
    at = next((i for i in range(start, end) if lines[i].startswith(prefix)), None)
    if at is None:
        fail(f'heading « {prefix} » not found')
    return at


# ---------------------------------------------------------------------------------------------------- achievements

class Achievement:
    def __init__(self, n, category, fr, en, goal_fr, goal_en, thresholds, line):
        self.n = n
        self.key = f'achievement.{n:03d}'
        self.category = category
        self.fr = fr
        self.en = en
        self.goal_fr = goal_fr
        self.goal_en = goal_en
        self.thresholds = thresholds
        self.line = line
        self.decoration = ''
        self.extra = ''
        self.counter = ''
        self.param = 0


def split_goal(text, where):
    parts = text.split(' / ')
    if len(parts) != 2:
        fail(f'{where}: the condition needs « français / english », found « {text} »')
    fr, en = parts[0].strip(), parts[1].strip()
    if '{n}' not in fr or '{n}' not in en:
        fail(f'{where}: the condition lines need {{n}}')
    return fr, en


def parse_achievements(lines):
    start, end = section_bounds(lines, 2)
    found = []
    for c_index, (fr_head, enum_name, en_name, fr_name) in enumerate(CATEGORIES):
        at = heading_at(lines, start, end, f'### {fr_head} (ACH-')
        m = re.match(r'### .+ \(ACH-(\d{3}) à (\d{3})\)$', lines[at].strip())
        if not m:
            fail(f'line {at + 1}: unreadable category heading')
        first, last = int(m.group(1)), int(m.group(2))
        biome = enum_name == 'Biomes'
        rows = table_rows(lines, at, end, 11 if biome else 9, 'ID')
        if len(rows) != last - first + 1 or len(rows) != EXPECTED_PER_CATEGORY[c_index]:
            fail(f'line {at + 1}: {len(rows)} achievements under {fr_head}, {last - first + 1} expected')
        for k, (line, c) in enumerate(rows):
            m = re.fullmatch(r'ACH-(\d{3})', c[0])
            where = f'line {line} ({c[0]})'
            if not m or int(m.group(1)) != first + k:
                fail(f'{where}: ACH-{first + k:03d} expected here')
            n = int(m.group(1))
            if biome:
                b_index = (n - 21) // 4
                code, slug, fr_biome = BIOMES[b_index]
                if c[1] != fr_biome:
                    fail(f'{where}: biome « {c[1]} » where « {fr_biome} » was expected')
                if c[2] not in BIOME_TYPES:
                    fail(f'{where}: unknown type « {c[2]} »')
                counter, deco = BIOME_TYPES[c[2]]
                if list(BIOME_TYPES).index(c[2]) != (n - 21) % 4:
                    fail(f'{where}: type « {c[2]} » out of its order')
                goal_fr, goal_en = split_goal(c[5], where)
                a = Achievement(n, enum_name, c[3], c[4], goal_fr, goal_en, [number(x, where) for x in c[6:10]], line)
                if c[10] != f'E-DEC-{code}-{deco}':
                    fail(f'{where}: Gold decoration « {c[10]} » where E-DEC-{code}-{deco} was expected')
                a.decoration = f'decor.achievement.{slug}.{deco}'
                a.counter, a.param = counter, b_index
            else:
                goal_fr, goal_en = split_goal(c[3], where)
                a = Achievement(n, enum_name, c[1], c[2], goal_fr, goal_en, [number(x, where) for x in c[4:8]], line)
                extra = c[8]
                if extra != 'aucun':
                    if not re.fullmatch(r'(title|frame|pose)\.[a-z0-9_]+', extra):
                        fail(f'{where}: unreadable Star extra « {extra} »')
                    a.extra = extra
                spec = COUNTERS.get(n)
                if spec is None:
                    fail(f'{where}: no counter for ACH-{n:03d}')
                a.counter, a.param = (spec, 0) if isinstance(spec, str) else spec
            t = a.thresholds
            if not (0 < t[0] < t[1] < t[2] < t[3]):
                fail(f'{where}: thresholds {t} do not rise')
            found.append(a)
    if [a.n for a in found] != list(range(1, 201)):
        fail('section 2: the 200 achievements ACH-001 to ACH-200 are not all there in order')
    pairs = set()
    for a in found:
        if (a.counter, a.param) in pairs:
            fail(f'line {a.line}: counter {a.counter} ({a.param}) is followed twice')
        pairs.add((a.counter, a.param))
    return found


# ---------------------------------------------------------------------------------------------------- profile items

class Item:
    def __init__(self, key, fr, en, source, value, subject, line, src_en, src_fr):
        self.key = key
        self.fr = fr
        self.en = en
        self.source = source
        self.value = value
        self.subject = subject
        self.line = line
        self.src_en = src_en
        self.src_fr = src_fr


class Context:
    def __init__(self, achievements, companions_en):
        self.ach = {a.n: a for a in achievements}
        self.events_fr = {}  # 101..112 -> French event name (title table « Événement »)
        self.companions_fr = {}  # 101..113 -> French companion name
        self.companions_en = companions_en
        self.species = []  # build ids of the 90 species in the doc's order
        self.biome_en = {}
        self.biome_fr = {}  # « de la Prairie »


def achievement_source(ctx, text, where):
    m = re.fullmatch(r'ACH-(\d{3}) Étoile', text)
    if not m or int(m.group(1)) not in ctx.ach:
        fail(f'{where}: unreadable achievement source « {text} »')
    a = ctx.ach[int(m.group(1))]
    return ('Achievement', 4, a.n, f'Star tier of “{a.en}”', f'Palier Étoile du succès « {a.fr} »')


def start_source(text, where):
    if text != 'Donné à la création du compte':
        fail(f'{where}: unreadable starting source « {text} »')
    return ('Start', 0, 0, 'Given when the account is created', 'Donné à la création du compte')


def moments_source(text, where):
    m = re.match(r'^(\d+) moments dans la page « Moments » du journal', text)
    if not m:
        fail(f'{where}: unreadable journal source « {text} »')
    n = int(m.group(1))
    return ('JournalMoments', n, 0, f'{n} moments on the journal\'s Moments page',
            f'{n} moments dans la page « Moments » du journal')


def companion_en(ctx, fr, where):
    en = ctx.companions_en.get(fr)
    if not en:
        fail(f'{where}: no English name for the companion « {fr} » in AlbumNames.Generated.cs')
    return en


def parse_titles(lines, ctx):
    start, end = section_bounds(lines, 3)
    items = []

    def add(key, fr, en, src, line):
        source, value, subject, s_en, s_fr = src
        items.append(Item(key, fr, en, source, value, subject, line, s_en, s_fr))

    at = heading_at(lines, start, end, '### Titre de départ')
    rows = table_rows(lines, at, end, 4, 'ID')
    for line, c in rows:
        add(c[0], c[1], c[2], start_source(c[3], f'line {line}'), line)
    if len(rows) != 1:
        fail('the starting title must be alone')

    at = heading_at(lines, start, end, '### Titres des succès')
    rows = table_rows(lines, at, end, 4, 'ID')
    for line, c in rows:
        add(c[0], c[1], c[2], achievement_source(ctx, c[3], f'line {line}'), line)
    if len(rows) != 43:
        fail(f'{len(rows)} achievement titles, 43 expected')

    at = heading_at(lines, start, end, "### Titres des pages d'événement")
    rows = table_rows(lines, at, end, 5, 'ID')
    if len(rows) != 12:
        fail(f'{len(rows)} event page titles, 12 expected')
    for k, (line, c) in enumerate(rows):
        n = 101 + k
        ctx.events_fr[n] = c[1]
        add(c[0], c[2], c[3], ('EventPage', 12, n, f'Complete the {EVENT_EN[n]} page in the event album',
                               f'Page « {c[1]} » complète dans l\'album des événements'), line)

    at = heading_at(lines, start, end, "### Titres des pages d'activité")
    rows = table_rows(lines, at, end, 4, 'ID')
    if len(rows) != 18:
        fail(f'{len(rows)} activity page titles, 18 expected')
    for k, (line, c) in enumerate(rows):
        m = re.fullmatch(r'S-ACT-(\d{3}) (.+)', c[1])
        if not m or int(m.group(1)) != k + 1:
            fail(f'line {line}: S-ACT-{k + 1:03d} expected')
        n = k + 1
        add(c[0], c[2], c[3], ('ActivityPage', 12, n, f'Complete the {ACTIVITY_EN[n]} page in the activity album',
                               f'Page « {m.group(2)} » complète dans l\'album des activités'), line)

    at = heading_at(lines, start, end, '### Titres des compagnons')
    rows = table_rows(lines, at, end, 4, 'ID')
    if len(rows) != 13:
        fail(f'{len(rows)} companion titles, 13 expected')
    for k, (line, c) in enumerate(rows):
        m = re.fullmatch(r'A-SP-(\d{3}) (.+)', c[1])
        if not m or int(m.group(1)) != 101 + k:
            fail(f'line {line}: A-SP-{101 + k} expected')
        n = 101 + k
        ctx.companions_fr[n] = m.group(2)
        en = companion_en(ctx, m.group(2), f'line {line}')
        add(c[0], c[2], c[3], ('Companion', 25, n, f'Bond 25 with the {en}', f'Lien 25 avec : {m.group(2)}'), line)

    at = heading_at(lines, start, end, '### Titres du niveau du refuge')
    rows = table_rows(lines, at, end, 4, 'ID')
    if len(rows) != 7:
        fail(f'{len(rows)} refuge level titles, 7 expected')
    for line, c in rows:
        lv = number(c[1], f'line {line}')
        add(c[0], c[2], c[3], ('RefugeLevel', lv, 0, f'Refuge level {lv}', f'Niveau {lv} du refuge'), line)

    at = heading_at(lines, start, end, '### Titres de la maîtrise de biome')
    rows = table_rows(lines, at, end, 4, 'Biome')
    if len(rows) != 13:
        fail(f'{len(rows)} biome mastery rows, 13 expected')
    for b, (line, c) in enumerate(rows):
        if c[0] != f'biome.{BIOMES[b][1]}':
            fail(f'line {line}: biome.{BIOMES[b][1]} expected')
        for col, level in ((1, 10), (2, 25), (3, 50)):
            m = re.fullmatch(r'(title\.[a-z_]+) : (.+) / (.+)', c[col])
            if not m:
                fail(f'line {line}: unreadable cell « {c[col]} »')
            if level == 10:
                fr_m = re.fullmatch(r'Complice (.+)', m.group(2))
                en_m = re.fullmatch(r'(.+) Pal', m.group(3))
                if not fr_m or not en_m:
                    fail(f'line {line}: the mastery 10 title must read « Complice … / … Pal »')
                ctx.biome_fr[b] = fr_m.group(1)
                ctx.biome_en[b] = en_m.group(1)
            add(m.group(1), m.group(2), m.group(3),
                ('BiomeMastery', level, b, f'{ctx.biome_en[b]} mastery {level}', f'Maîtrise {ctx.biome_fr[b]} {level}'),
                line)

    at = heading_at(lines, start, end, "### Titres de la maîtrise d'espèce")
    rows = table_rows(lines, at, end, 6, 'Espèce')
    if len(rows) != 90:
        fail(f'{len(rows)} species rows, 90 expected')
    for s, (line, c) in enumerate(rows):
        m = re.fullmatch(r'species\.([a-z]+) \((.+)\)', c[0])
        if not m:
            fail(f'line {line}: unreadable species « {c[0]} »')
        spec_key, fr_species = m.group(1), m.group(2)
        if s < 78:
            name = SPECIES_ALIASES.get(fr_species.lower(), fr_species.lower())
            if name not in SPECIES:
                fail(f'line {line}: the species « {fr_species} » has no build id (pw_album_data.SPECIES)')
            build = SPECIES[name]
            if c[1] != BIOMES[s // 6][2]:
                fail(f'line {line}: « {c[1]} » where the biome {BIOMES[s // 6][2]} was expected')
        else:
            if spec_key not in EVENT_SPECIES or not c[1].startswith('Événement ('):
                fail(f'line {line}: unknown event species « {spec_key} »')
            build = EVENT_SPECIES[spec_key]
        ctx.species.append('species.' + build)
        for id_col, name_col, level, prefix in ((2, 3, 30, 'pal'), (4, 5, 100, 'heart')):
            if c[id_col] != f'title.{prefix}_{spec_key}':
                fail(f'line {line}: title.{prefix}_{spec_key} expected, found {c[id_col]}')
            nm = re.fullmatch(r'(.+) / (.+)', c[name_col])
            if not nm:
                fail(f'line {line}: unreadable names « {c[name_col]} »')
            en_species = re.fullmatch(r'(.+) Pal', c[3].split(' / ')[1])
            if not en_species:
                fail(f'line {line}: the mastery 30 English title must end with « Pal »')
            add(c[id_col], nm.group(1), nm.group(2),
                ('SpeciesMastery', level, s, f'{en_species.group(1)}: mastery {level}',
                 f'{fr_species} : maîtrise {level}'), line)

    at = heading_at(lines, start, end, '### Titres des saisons du Refuge Pass')
    rows = table_rows(lines, at, end, 4, 'ID')
    if len(rows) != 8:
        fail(f'{len(rows)} Pass titles, 8 expected')
    for k, (line, c) in enumerate(rows):
        if number(c[1], f'line {line}') != k + 1:
            fail(f'line {line}: season {k + 1} expected')
        add(c[0], c[2], c[3], ('PassSeason', 40, k + 1, f'Refuge Pass season {k + 1}, free track level 40',
                               f'Refuge Pass, saison {k + 1}, niveau 40 de la piste gratuite'), line)

    at = heading_at(lines, start, end, '### Titre du Journal des moments')
    rows = table_rows(lines, at, end, 4, 'ID')
    for line, c in rows:
        add(c[0], c[1], c[2], moments_source(c[3], f'line {line}'), line)
    if len(rows) != 1:
        fail('the journal title must be alone')

    if len(items) != 322:
        fail(f'{len(items)} titles, 322 expected')
    return items


def parse_frames(lines, ctx):
    start, end = section_bounds(lines, 4)
    at = heading_at(lines, start, end, '### Liste')
    rows = table_rows(lines, at, end, 5, 'ID')
    events_by_fr = {v: k for k, v in ctx.events_fr.items()}
    biome_by_fr = {f'Maîtrise {v}': b for b, v in ctx.biome_fr.items()}
    # The frames name the Lunar, Prehistoric and Starlight biomes their own way (« du biome Lunaire »).
    for b, (_, _, fr) in enumerate(BIOMES):
        for art in ('de la ', 'du ', 'des ', 'de ', 'du biome ', ''):
            biome_by_fr.setdefault(f'Maîtrise {art}{fr}', b)
    items = []
    for line, c in rows:
        where = f'line {line} ({c[0]})'
        src = c[4]
        m = re.fullmatch(r'(Maîtrise .+) 20', src)
        if src == 'Donné à la création du compte':
            s = start_source(src, where)
        elif m and m.group(1) in biome_by_fr:
            b = biome_by_fr[m.group(1)]
            s = ('BiomeMastery', 20, b, f'{ctx.biome_en[b]} mastery 20', f'Maîtrise {ctx.biome_fr[b]} 20')
        elif re.fullmatch(r'(.+), (?:parcours gratuit )?palier 30', src):
            ev = re.fullmatch(r'(.+), (?:parcours gratuit )?palier 30', src).group(1)
            if ev not in events_by_fr:
                fail(f'{where}: unknown event « {ev} »')
            n = events_by_fr[ev]
            s = ('EventTrack', 30, n, f'{EVENT_EN[n]}, free path tier 30', f'{ev}, palier 30 du parcours gratuit')
        elif re.fullmatch(r'Niveau du refuge (\d+)', src):
            lv = int(re.fullmatch(r'Niveau du refuge (\d+)', src).group(1))
            s = ('RefugeLevel', lv, 0, f'Refuge level {lv}', f'Niveau {lv} du refuge')
        elif src.startswith('ACH-'):
            s = achievement_source(ctx, src, where)
        else:
            s = moments_source(src, where)
        items.append(Item(c[0], c[1], c[2], s[0], s[1], s[2], line, s[3], s[4]))
    if len(items) != 42:
        fail(f'{len(items)} frames, 42 expected')
    counts = {}
    for it in items:
        counts[it.source] = counts.get(it.source, 0) + 1
    if counts != {'Start': 1, 'BiomeMastery': 13, 'EventTrack': 12, 'RefugeLevel': 3, 'Achievement': 12,
                  'JournalMoments': 1}:
        fail(f'frames by source {counts} do not match « Répartition »')
    return items


def parse_poses(lines, ctx):
    start, end = section_bounds(lines, 5)
    at = heading_at(lines, start, end, '### Liste')
    rows = table_rows(lines, at, end, 6, 'ID')
    events_by_fr = {v: k for k, v in ctx.events_fr.items()}
    companions_by_fr = {v: k for k, v in ctx.companions_fr.items()}
    items = []
    for line, c in rows:
        where = f'line {line} ({c[0]})'
        src = c[5]
        if src == 'De base':
            s = ('PhotoMode', 0, 0, 'Given when photo mode opens', "Donnée à l'ouverture du mode photo")
        elif src in events_by_fr:
            n = events_by_fr[src]
            s = ('EventGoal', 0, n, f'{EVENT_EN[n]}, shared goal', f'{src}, objectif commun')
        elif re.fullmatch(r'(.+), lien 20', src):
            fr = re.fullmatch(r'(.+), lien 20', src).group(1)
            if fr not in companions_by_fr:
                fail(f'{where}: unknown companion « {fr} »')
            n = companions_by_fr[fr]
            en = companion_en(ctx, fr, where)
            s = ('Companion', 20, n, f'Bond 20 with the {en}', f'Lien 20 avec : {fr}')
        elif re.fullmatch(r'Saison (\d) du Pass, Premium\+', src):
            k = int(re.fullmatch(r'Saison (\d) du Pass, Premium\+', src).group(1))
            s = ('PassSeason', 0, k, f'Refuge Pass season {k}, Premium+', f'Refuge Pass, saison {k}, Premium+')
        elif src.startswith('ACH-'):
            s = achievement_source(ctx, src, where)
        else:
            fail(f'{where}: unreadable pose source « {src} »')
        items.append(Item(c[0], c[1], c[2], s[0], s[1], s[2], line, s[3], s[4]))
    if len(items) != 47:
        fail(f'{len(items)} poses, 47 expected')
    counts = {}
    for it in items:
        counts[it.source] = counts.get(it.source, 0) + 1
    if counts != {'PhotoMode': 8, 'EventGoal': 12, 'Companion': 13, 'PassSeason': 8, 'Achievement': 6}:
        fail(f'poses by source {counts} do not match « Volumes »')
    return items


# ---------------------------------------------------------------------------------------------------- checks and output

def companions_english():
    """The companions' English names, from the albums' text rows (sticker 07 of each biome page 1)."""
    names = {}
    with open(ALBUM_NAMES, encoding='utf-8') as f:
        for line in f:
            m = re.search(r'\{ "sticker\.[a-z]{2}\.07\.name", "(.+?)", "(.+?)" \}', line)
            if m:
                names[m.group(2)] = m.group(1)
    if len(names) != 13:
        fail(f'{len(names)} companion names in AlbumNames.Generated.cs, 13 expected')
    return names


def check(achievements, titles, frames, poses):
    keys = set()
    for kind, items, prefix in (('title', titles, 'title.'), ('frame', frames, 'frame.'), ('pose', poses, 'pose.')):
        for it in items:
            if not re.fullmatch(re.escape(prefix) + r'[a-z0-9_]+', it.key):
                fail(f'line {it.line}: unreadable {kind} id « {it.key} »')
            if it.key in keys:
                fail(f'line {it.line}: duplicate id {it.key}')
            keys.add(it.key)
    by_key = {it.key: it for it in titles + frames + poses}
    extras = 0
    for a in achievements:
        if not a.extra:
            continue
        extras += 1
        it = by_key.get(a.extra)
        if it is None:
            fail(f'line {a.line}: {a.key} gives « {a.extra} », which no table lists')
        if it.source != 'Achievement' or it.subject != a.n:
            fail(f'line {a.line}: « {a.extra} » is not listed as ACH-{a.n:03d} Étoile')
    sourced = sum(1 for it in titles + frames + poses if it.source == 'Achievement')
    if extras != sourced or extras != 43 + 12 + 6:
        fail(f'{extras} Star extras in the achievements, {sourced} items sourced by achievements, 61 expected')
    if sum(1 for a in achievements if a.decoration) != 52:
        fail('52 Gold decorations expected (ACH-021 to 072)')
    texts = []
    for a in achievements:
        texts += [a.fr, a.en, a.goal_fr, a.goal_en]
    for it in titles + frames + poses:
        texts += [it.fr, it.en, it.src_en, it.src_fr]
    for t in texts:
        if not t:
            fail('an empty text')
        for bad in FORBIDDEN:
            if bad in t:
                fail(f'« {t} » holds a forbidden character U+{ord(bad):04X}')


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def write_data(achievements, titles, frames, poses, species):
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_profile_data.py from doc « 01 Succès, titres, cadres, poses et journal », sections 2 to 5.')
    w('// Do not edit by hand: change the doc and run the script again (it fails on any table it cannot read).')
    w('// </auto-generated>')
    w('#nullable enable')
    w('')
    w('namespace PocketWilds.Simulation')
    w('{')
    w('    public static partial class AchievementData')
    w('    {')
    w(f'        public const int Count = {len(achievements)};')
    w('')
    w('        private static readonly AchievementRow[] Rows =')
    w('        {')
    for a in achievements:
        t = a.thresholds
        w(f'            new AchievementRow({cs(a.key)}, AchievementCategory.{a.category}, AchievementCounter.{a.counter}, '
          f'{a.param}, {t[0]}, {t[1]}, {t[2]}, {t[3]}, {cs(a.decoration)}, {cs(a.extra)}),')
    w('        };')
    w('    }')
    w('')
    w('    public static partial class ProfileData')
    w('    {')
    w(f'        public const int TitleCount = {len(titles)};')
    w(f'        public const int FrameCount = {len(frames)};')
    w(f'        public const int PoseCount = {len(poses)};')
    w(f'        public const int SpeciesCount = {len(species)};')
    w('')
    w('        /// <summary>The 78 biome species (6 per biome, in biome order) then the 12 event species: their content ids.</summary>')
    w('        private static readonly string[] SpeciesKeys =')
    w('        {')
    for k in range(0, len(species), 6):
        w('            ' + ' '.join(cs(s) + ',' for s in species[k:k + 6]))
    w('        };')
    for name, items in (('TitleRows', titles), ('FrameRows', frames), ('PoseRows', poses)):
        w('')
        w(f'        private static readonly ProfileRow[] {name} =')
        w('        {')
        for order, it in enumerate(items):
            w(f'            new ProfileRow({cs(it.key)}, ProfileSource.{it.source}, {it.value}, {it.subject}, {order + 1}),')
        w('        };')
    w('    }')
    w('}')
    return '\n'.join(out) + '\n'


def write_names(achievements, titles, frames, poses):
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_profile_data.py from doc « 01 Succès, titres, cadres, poses et journal », sections 2 to 5.')
    w('// Do not edit by hand.')
    w('// </auto-generated>')
    w('namespace PocketWilds.Editor.UI')
    w('{')
    w('    /// <summary>')
    w('    /// The texts of the 200 achievements (name, goal line with {n}), their 13 categories, the 322 titles, 42 frames and')
    w('    /// 47 poses (name, source line): rows { key, English, French }.')
    w('    /// </summary>')
    w('    public static class ProfileTexts')
    w('    {')
    w('        private static string[,] _texts;')
    w('')
    w('        /// <summary>Built on first use (the screen pipeline finds this class by reflection).</summary>')
    w('        public static string[,] Texts => _texts ??= Build();')
    w('')
    w('        private static string[,] Build() => new string[,]')
    w('        {')
    for fr_head, enum_name, en_name, fr_name in CATEGORIES:
        w(f'            {{ {cs("achievement.category." + enum_name.lower() + ".name")}, {cs(en_name)}, {cs(fr_name)} }},')
    for a in achievements:
        w(f'            {{ {cs(a.key + ".name")}, {cs(a.en)}, {cs(a.fr)} }},')
        w(f'            {{ {cs(a.key + ".goal")}, {cs(a.goal_en)}, {cs(a.goal_fr)} }},')
    for it in titles + frames + poses:
        w(f'            {{ {cs(it.key + ".name")}, {cs(it.en)}, {cs(it.fr)} }},')
        w(f'            {{ {cs(it.key + ".source")}, {cs(it.src_en)}, {cs(it.src_fr)} }},')
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
        achievements = parse_achievements(lines)
        ctx = Context(achievements, companions_english())
        titles = parse_titles(lines, ctx)
        frames = parse_frames(lines, ctx)
        poses = parse_poses(lines, ctx)
        check(achievements, titles, frames, poses)
    except DataError as e:
        print('pw_profile_data: ' + str(e), file=sys.stderr)
        return 1
    for path, text in ((OUT_DATA, write_data(achievements, titles, frames, poses, ctx.species)),
                       (OUT_NAMES, write_names(achievements, titles, frames, poses))):
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    print(f'pw_profile_data: {len(achievements)} achievements, {len(titles)} titles, {len(frames)} frames, '
          f'{len(poses)} poses written.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
