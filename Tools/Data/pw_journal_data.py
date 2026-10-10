"""Builds the refuge journal's data from doc « 01 Succès, titres, cadres, poses et journal », section 6 (our own script,
T44C).

usage: python Tools/Data/pw_journal_data.py --spec <path of 01_Succes_titres_cadres_poses_journal.md>

Reads the table « Moments enregistrés (63) » and writes:
  Assets/_Game/Simulation/Journal/JournalData.Generated.cs  the 63 moments (key, the system its icon shows, image kind,
                                                            repeatable or once, the variables its text uses) and their
                                                            indices as constants (JournalMoment.FirstHatch…)
  Assets/_Game/Editor/UI/JournalNames.Generated.cs          the en/fr texts as rows { key, English, French }:
                                                            « memory.<name>.text » with the doc's variables, the 12 rare
                                                            visitors' names (« visitor.<name>.name », the albums' season
                                                            stickers 01 to 03), the « New visitor! » banner and the
                                                            names of doc 05's 50 world surprises (« surprise.<name>.name »,
                                                            the journal's « Moments » page), read from its SUR headings
Fails loudly (exit 1, the row named) on any row, id, image, variable or text it cannot read, on texts whose English and
French variables differ, on a count that is not the heading's and on duplicate ids: the moments are validated data.
"""
import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DATA = os.path.join(ROOT, 'Assets', '_Game', 'Simulation', 'Journal', 'JournalData.Generated.cs')
OUT_NAMES = os.path.join(ROOT, 'Assets', '_Game', 'Editor', 'UI', 'JournalNames.Generated.cs')
ALBUM_NAMES = os.path.join(ROOT, 'Assets', '_Game', 'Editor', 'UI', 'AlbumNames.Generated.cs')


class DataError(Exception):
    pass


def fail(message):
    raise DataError(message)


# The variables the doc allows (section 6 « Règles »).
VARIABLES = ['name', 'species', 'variant', 'biome', 'zone', 'activity', 'score', 'destination', 'plant', 'page',
             'decoration', 'level', 'count', 'event', 'visitor', 'companion', 'season', 'essence', 'parent1', 'parent2',
             'outfit', 'achievement', 'title', 'circle', 'date']

IMAGES = [
    ('Capture du refuge', 'RefugeCapture'),
    ("Capture de l'animal", 'AnimalCapture'),
    ('Capture de l’animal', 'AnimalCapture'),
    ('Vignette existante', 'Thumbnail'),
    ('Carte postale', 'Postcard'),
    ('Photo du joueur', 'Photo'),
]

# The system whose small icon the page shows (JournalSystem in JournalData.cs).
SYSTEMS = {
    'Refuge': ['first_egg', 'three_animals', 'zone_opened', 'refuge_level', 'population', 'season'],
    'Hatch': ['first_hatch'],
    'Collection': ['first_name', 'first_favorite', 'new_species', 'first_variant', 'first_rare', 'first_epic',
                   'first_legendary', 'first_fabulous', 'first_secret', 'first_tiny', 'first_giant', 'first_shiny',
                   'first_golden', 'first_albino', 'first_melanistic', 'biome_codex'],
    'Mastery': ['mastery_10', 'mastery_30'],
    'Biomes': ['biome_unlocked'],
    'Bonds': ['first_bond_1', 'first_bond_3', 'first_bond_5', 'companion_joined', 'companion_bond_25'],
    'Activities': ['first_activity', 'first_three_stars', 'first_record', 'activity_perfect'],
    'Expeditions': ['first_expedition', 'postcard'],
    'Garden': ['first_harvest', 'garden_complete'],
    'Daily': ['first_wish', 'calendar_complete', 'weekly_complete'],
    'Visitors': ['first_visitor', 'visitor_stays'],
    'Albums': ['album_page', 'album_complete'],
    'Decoration': ['first_decoration', 'first_landmark'],
    'Breeding': ['first_breeding', 'first_mutation', 'third_generation'],
    'Events': ['event_start', 'event_featured', 'event_souvenir', 'event_goal'],
    'Renewal': ['first_renewal', 'renewal_record'],
    'Photo': ['first_photo', 'player_photo'],
    'Outfit': ['first_outfit'],
    'Achievements': ['first_star_achievement', 'first_title'],
    'Circle': ['first_circle'],
}

# The 12 rare visitors in season order (Éveil, Soleil, Récolte, Givre), sticker 01 to 03 of each season page: the same
# order as VisitorData.cs. Their names are the albums' season stickers'.
VISITOR_KEYS = [
    'visitor.white_fawn', 'visitor.meadow_rabbit', 'visitor.flowery_hedgehog',
    'visitor.golden_fox', 'visitor.sun_squirrel', 'visitor.rainbow_duck',
    'visitor.amber_badger', 'visitor.russet_owl', 'visitor.golden_chipmunk',
    'visitor.white_stoat', 'visitor.snow_rabbit', 'visitor.crystal_tit',
]
BANNER = ('visitor.new.banner', 'New visitor!', 'Nouveau visiteur !')

FORBIDDEN = [chr(0x2014), chr(0x2728), chr(0x2726), chr(0x2727)]  # em dash, the four-point sparkle and its kin


def cells(line):
    line = line.strip()
    if not (line.startswith('|') and line.endswith('|')):
        return None
    return [c.strip() for c in line[1:-1].split('|')]


def pascal(name):
    return ''.join(p[:1].upper() + p[1:] for p in name.split('_'))


class Moment:
    def __init__(self, line, key, trigger, fr, en, image, system, repeatable, variables):
        self.line = line
        self.key = key
        self.trigger = trigger
        self.fr = fr
        self.en = en
        self.image = image
        self.system = system
        self.repeatable = repeatable
        self.variables = variables


def variables_of(text, where):
    found = re.findall(r'\{([^{}]*)\}', text)
    for v in found:
        if v not in VARIABLES:
            fail(f'{where}: unknown variable {{{v}}} in « {text} »')
    if text.count('{') != len(found) or text.count('}') != len(found):
        fail(f'{where}: unbalanced braces in « {text} »')
    return found


def parse(lines):
    start = next((i for i, l in enumerate(lines) if l.startswith('### Moments enregistrés')), None)
    if start is None:
        fail('heading « ### Moments enregistrés (N) » not found')
    m = re.search(r'\((\d+)\)', lines[start])
    if not m:
        fail(f'line {start + 1}: no count in « {lines[start].strip()} »')
    expected = int(m.group(1))
    system_of = {}
    for system, names in SYSTEMS.items():
        for n in names:
            if n in system_of:
                fail(f'{n} listed under two systems')
            system_of[n] = system
    moments = []
    header = False
    for i in range(start + 1, len(lines)):
        text = lines[i]
        if text.startswith('#'):
            break
        c = cells(text)
        if c is None:
            if header and moments and text.strip():
                break
            continue
        where = f'line {i + 1}'
        if not header:
            if c[0] != 'ID' or len(c) != 6:
                fail(f'{where}: header « ID | Moment | Déclencheur | Texte français | Texte anglais | Image » expected')
            header = True
            continue
        if set(c[0]) <= {'-', ':'}:
            continue
        if len(c) != 6:
            fail(f'{where}: {len(c)} cells, 6 expected « {text.strip()[:60]} »')
        key, _, trigger, fr, en, image = c
        if not re.fullmatch(r'memory\.[a-z0-9_]+', key):
            fail(f'{where}: unreadable moment id « {key} »')
        name = key[len('memory.'):]
        if name not in system_of:
            fail(f'{where}: {key} has no system (SYSTEMS in this script)')
        kind = next((k for prefix, k in IMAGES if image.startswith(prefix)), None)
        if kind is None:
            fail(f'{where}: unreadable image « {image} »')
        if not fr or not en:
            fail(f'{where}: an empty text')
        vf = variables_of(fr, where)
        ve = variables_of(en, where)
        if sorted(vf) != sorted(ve):
            fail(f'{where}: the French and English texts use different variables ({vf} / {ve})')
        if len(set(vf)) != len(vf):
            fail(f'{where}: a variable used twice')
        if len(vf) > 2:
            fail(f'{where}: more than 2 variables (JournalState keeps two values a page)')
        repeatable = '(répétable)' in trigger
        moments.append(Moment(i + 1, key, trigger, fr, en, kind, system_of[name], repeatable, vf))
    if not header:
        fail(f'line {start + 1}: no table under the heading')
    if len(moments) != expected:
        fail(f'{len(moments)} moments read, the heading says {expected}')
    keys = set()
    for mo in moments:
        if mo.key in keys:
            fail(f'line {mo.line}: duplicate id {mo.key}')
        keys.add(mo.key)
    missing = [n for n in system_of if 'memory.' + n not in keys]
    if missing:
        fail(f'moments listed in SYSTEMS but not in the table: {missing}')
    for mo in moments:
        for t in (mo.fr, mo.en):
            for bad in FORBIDDEN:
                if bad in t:
                    fail(f'line {mo.line}: « {t} » holds a forbidden character U+{ord(bad):04X}')
    return moments


def visitor_names():
    names = {}
    with open(ALBUM_NAMES, encoding='utf-8') as f:
        for line in f:
            m = re.search(r'\{ "sticker\.sea\.([1-4])\.0([1-3])\.name", "(.+?)", "(.+?)" \}', line)
            if m:
                names[(int(m.group(1)) - 1) * 3 + int(m.group(2)) - 1] = (m.group(3), m.group(4))
    if len(names) != 12:
        fail(f'{len(names)} season visitor stickers in AlbumNames.Generated.cs, 12 expected')
    return [(VISITOR_KEYS[i] + '.name', names[i][0], names[i][1]) for i in range(12)]


SURPRISE_HEADING = re.compile(r'^#### SUR-(\d\d) (.+?) \(([^()]+)\)\s*$')
# The key the code already uses for SUR-06 (JournalHooks.VisitorSurpriseKey); the others come from their English names.
SURPRISE_KEYS = {6: 'visitor_arrival'}


def surprise_names(path):
    with open(path, encoding='utf-8') as f:
        lines = f.read().splitlines()
    rows, seen = [], set()
    for line in lines:
        if not line.startswith('#### SUR-'):
            continue
        m = SURPRISE_HEADING.match(line)
        if not m:
            fail(f'unreadable surprise heading: {line!r}')
        number, fr, en = int(m.group(1)), m.group(2).strip(), m.group(3).strip()
        slug = SURPRISE_KEYS.get(number) or re.sub(r'[^a-z0-9]+', '_', en.lower().replace("'", '')).strip('_')
        slug = re.sub(r'^(a|the)_', '', slug)
        if number != len(rows) + 1 or slug in seen:
            fail(f'surprise SUR-{number:02d} out of order or duplicate key {slug}')
        seen.add(slug)
        rows.append((f'surprise.{slug}.name', en, fr))
    if len(rows) != 50:
        fail(f'{len(rows)} surprises in doc 05, 50 expected')
    return rows


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def write_data(moments):
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_journal_data.py from doc « 01 Succès, titres, cadres, poses et journal », section 6.')
    w('// Do not edit by hand: change the doc and run the script again (it fails on any row it cannot read).')
    w('// </auto-generated>')
    w('#nullable enable')
    w('')
    w('namespace PocketWilds.Simulation')
    w('{')
    w('    public static partial class JournalData')
    w('    {')
    w(f'        public const int Count = {len(moments)};')
    w('')
    combos = []
    for mo in moments:
        t = tuple(mo.variables)
        if t and t not in combos:
            combos.append(t)
    w('        private static readonly string[] NoVariables = System.Array.Empty<string>();')
    for n, t in enumerate(combos):
        w(f'        private static readonly string[] Variables{n} = {{ {", ".join(cs(v) for v in t)} }};')
    w('')
    w('        private static readonly JournalMomentRow[] Rows =')
    w('        {')
    for mo in moments:
        t = tuple(mo.variables)
        vars_name = f'Variables{combos.index(t)}' if t else 'NoVariables'
        w(f'            new JournalMomentRow({cs(mo.key)}, JournalSystem.{mo.system}, JournalImageKind.{mo.image}, '
          f'{"true" if mo.repeatable else "false"}, {vars_name}),')
    w('        };')
    w('    }')
    w('')
    w('    /// <summary>The moments\' indices into <see cref="JournalData"/> (the order of the doc\'s table).</summary>')
    w('    public static class JournalMoment')
    w('    {')
    for n, mo in enumerate(moments):
        w(f'        public const int {pascal(mo.key[len("memory."):])} = {n};')
    w('    }')
    w('}')
    return '\n'.join(out) + '\n'


def write_names(moments, visitors, surprises):
    out = []
    w = out.append
    w('// <auto-generated>')
    w('// Written by Tools/Data/pw_journal_data.py from doc « 01 Succès, titres, cadres, poses et journal », section 6.')
    w('// Do not edit by hand.')
    w('// </auto-generated>')
    w('namespace PocketWilds.Editor.UI')
    w('{')
    w('    /// <summary>')
    w('    /// The texts of the 63 journal moments (« memory.<name>.text », variables in braces), the 12 rare visitors\' names')
    w('    /// the « New visitor! » banner and the names of the 50 world surprises: rows { key, English, French }.')
    w('    /// </summary>')
    w('    public static class JournalTexts')
    w('    {')
    w('        private static string[,] _texts;')
    w('')
    w('        /// <summary>Built on first use (the screen pipeline finds this class by reflection).</summary>')
    w('        public static string[,] Texts => _texts ??= Build();')
    w('')
    w('        private static string[,] Build() => new string[,]')
    w('        {')
    for mo in moments:
        w(f'            {{ {cs(mo.key + ".text")}, {cs(mo.en)}, {cs(mo.fr)} }},')
    for key, en, fr in visitors + [BANNER] + surprises:
        w(f'            {{ {cs(key)}, {cs(en)}, {cs(fr)} }},')
    w('        };')
    w('    }')
    w('}')
    return '\n'.join(out) + '\n'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--spec', required=True)
    parser.add_argument('--surprises', required=True, help='doc 05 « Sensations et surprises » (the SUR headings)')
    args = parser.parse_args()
    with open(args.spec, encoding='utf-8') as f:
        lines = f.read().splitlines()
    try:
        moments = parse(lines)
        visitors = visitor_names()
        surprises = surprise_names(args.surprises)
    except DataError as e:
        print('pw_journal_data: ' + str(e), file=sys.stderr)
        return 1
    for path, text in ((OUT_DATA, write_data(moments)), (OUT_NAMES, write_names(moments, visitors, surprises))):
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    repeatable = sum(1 for mo in moments if mo.repeatable)
    print(f'pw_journal_data: {len(moments)} moments ({repeatable} repeatable), {len(visitors)} visitor names, {len(surprises)} surprise names written.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
