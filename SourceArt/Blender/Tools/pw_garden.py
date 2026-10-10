"""T43 « Jardin » (doc 01 Contenu, section 8): le potager du refuge. The garden set (five raised wooden beds along a
path of round stepping stones, a low fence, a watering can, a wicker harvest basket, the bed cover of a locked plot) and
the 13 launch plants in their 3 stages (doc 01, table 8.2), all built by this script.

Intention (charte « fait main », section 10, step 1)
- Character: a little kitchen garden behind the refuge, sunny and tidy but soft: plump rounded planks, dark crumbly
  soil, round cream stepping stones, a low wooden fence with round post tops, a teal watering can and a honey wicker
  trug. Plants are cute, round and generous: round leaves, flowers made of round petals around a domed heart, berries
  and fruits as plump spheres, soft bumpy spikes (never a needle), one small round tree. The exact family of the
  Prairie props (pw_grassland), the T38 set (pw_grooming: GR_SetBackdrop, reused behind the fence), the T40 and T42
  baskets (pw_nutdrop, pw_postcards: the same weave, the same honey wicker).
- What the Prairie already had, checked first: GR_SetBackdrop (meadow floor with a hole in its middle, three rows of
  round hills, round trees, backdrop daisies), shared by the activity sets through Activity_Shared: reused behind the
  fence. The kit's grass tufts and flowers (explicit entries of Biome_Grassland_Kit): the set pipeline may dress the
  edges with them. Missing, so made here: every garden prop, a meadow floor without a hole, and the 39 plant models
  (the kit's flowers are single meadow flowers for the refuge, not crops in three stages).
- Scale: a bed is 1.04 x 0.88 m and 0.27 m high (about 1.0 x 0.8 m as the brief asks; its soil is 0.92 x 0.76 m so a
  plant of radius 0.36 m keeps 2 cm from the planks). A ripe plant is 0.2 to 0.6 m tall; the sunflower 1.0 m, the
  dwarf apple tree 0.95 m. Stepping stones 0.24 to 0.28 m across, the fence 0.42 m high, the can 0.24 m high, the
  basket 0.42 x 0.30 m.
- Counted parts, the same in every export: bed 8 planks (2 rows), 4 corner posts with round caps, 1 soil top, 4 clods;
  cover 1 puffed cloth, 1 skirt with a wavy hem and 1 sage band, 1 A-frame sign; path 15 stones; fence 11 posts, 2 rails;
  can 1 body, 1 spout and its rose, 1 top handle; basket 4 woven rows of 10 over-bumps, 1 rolled rim, 1 arched handle.
- Wanted imperfections: plank tops bow a little, one plank sits a touch higher; the soil has soft clods in its corners;
  the cloth puffs and its hem waves; the stones vary in size and step a little left and right; leaves vary in length,
  droop and turn, laid by fixed seeds; flowers turn a little either way around the camera's direction.
- Nothing spiky: leaves are rounded lenses, petals round tips between shoulders (never a star), spikes are soft beads,
  the wheat ears have no awns, the carrot tops are round leaflets.
- Budgets (triangles): each plant stage 150 to 900; the set props together under 6,000 (each model once).

Data contract: as pw_grassland.py (one palette row per renderer, SH_EnvironmentOpaque): color attribute PW_Regions
(R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion, y height
factor). GD_PALETTES holds the rows (base, R, G, B, A, occlusion, accent); GardenSetPipeline copies them. Every plant of
one species shares its row palette.garden.<plant> (its three stages use the same columns).

Axes: authored for the Blender front view (camera on -Y, looking +Y, above). pw_export.py writes Blender (x, y, z) as
Unity (-x, z, -y); GardenSetPipeline turns the models half a turn, so Blender +X is Unity +X and Blender +Y is Unity +Z
(away from the camera). Pivots: a bed and its cover at the center of the bed's footprint on the ground; a plant at its
root on the soil (z = 0 is the soil top under the plant; stems start 12 mm under it, rooted); the path, the fence and the
ground at the set's origin (the pipeline places them at its origin); can and basket at the center of their foot.

Usage inside Blender (the live session or a fresh file), after pw_grassland.py, pw_nutdrop.py and pw_postcards.py in the
same namespace (the T40 shape helpers and review studio are reused):
    ns["build_garden"]()             # scene "Garden", every object in its export collection, budgets and fits checked
    ns["write_garden_blend"]()       # bpy.data.libraries.write: the scene to SourceArt/Blender/Activities/
Then, still in the live session, after pw_export.py in the same namespace:
    ns["export_garden_live"]()       # GD_GardenSet.fbx and GD_GardenPlants.fbx in Assets/_Game/Art/Activities/Garden/,
                                     # their header naming BL_Activity_Garden.blend (the file just written)
    ns["render_garden_previews"](out_dir, refs)   # review images; temporary camera, light, materials
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

GD_SCENE = "Garden"
GD_COLLECTION = "GD_EXPORT"         # EXPORT is taken in the Prairie file
GD_BLEND = "SourceArt/Blender/Activities/BL_Activity_Garden.blend"
GD_OUT = "Assets/_Game/Art/Activities/Garden"
GD_SET_PROPS = ("GD_Ground", "GD_Bed", "GD_BedCover", "GD_Path", "GD_Fence", "GD_WateringCan", "GD_Basket")

# (runtime key without "plant.", object stem): the 12 starter plants and the Prairie's Sweet Pea, in the catalogue order.
GD_PLANTS = [
    ("sweet_clover", "SweetClover"), ("wild_strawberry", "WildStrawberry"), ("daisy", "Daisy"),
    ("round_carrot", "RoundCarrot"), ("poppy", "Poppy"), ("blueberry", "Blueberry"), ("lavender", "Lavender"),
    ("golden_wheat", "GoldenWheat"), ("meadow_indigo", "MeadowIndigo"), ("sunflower", "Sunflower"),
    ("dwarf_apple", "DwarfApple"), ("round_melon", "RoundMelon"), ("sweet_pea", "SweetPea"),
]


def plant_object_name(stem, stage):
    return f"GD_Plant_{stem}_{stage}"


GD_PLANT_OBJECTS = tuple(plant_object_name(stem, s) for _, stem in GD_PLANTS for s in (1, 2, 3))
GD_EXPORTS = {
    "GD_GardenSet.fbx": GD_SET_PROPS,
    "GD_GardenPlants.fbx": GD_PLANT_OBJECTS,
}
GD_BUDGETS = {"GD_Ground": 700, "GD_Bed": 900, "GD_BedCover": 700, "GD_Path": 900, "GD_Fence": 1100,
              "GD_WateringCan": 600, "GD_Basket": 1000}
GD_SET_BUDGET = 6000
GD_PLANT_TRIS = (150, 900)

# The plants' room (m): the soil of a bed is 0.92 x 0.76; a plant's footprint radius stays under PLANT_RADIUS, except
# its rooted stems; the sunflower and the tree may be taller than the others.
PLANT_RADIUS = 0.36
PLANT_HEIGHT = 0.62
TALL_PLANTS = {"Sunflower": 1.02, "DwarfApple": 0.97}

# Proposed palette rows (base, R, G, B, A, occlusion, accent), sRGB hex like DEF_Palettes_Grassland.
GD_PALETTES = {
    # planks warm wood; R sunny plank tops and edges; G soil (dark, warm, never black); B crumbs and clods; A posts.
    "palette.garden.bed": ("BF8758", "DDB07E", "6A4B3B", "8C6A52", "A8734C", "5A4446", "FFFFFF"),
    # warm oat linen; R the soft sage band; G sign wood; B the sign's pale face; A the sign's border.
    "palette.garden.cover": ("E6D2AC", "9FBF88", "C9955F", "F3DDB0", "A9764B", "857892", "FFFFFF"),
    # cream stone; R sunny tops; G moss at the foot; B warm grey speckle.
    "palette.garden.path": ("CDBFA6", "DCD0B8", "9FBF74", "CDBFA9", "E6DCC8", "6E6A7E", "FFFFFF"),
    # fence wood (lighter than the beds); R round post tops; G rails; B moss at the foot.
    "palette.garden.fence": ("D4A874", "E9C796", "C49460", "8DAA5C", "D4A874", "6A5250", "FFFFFF"),
    # painted soft teal; R pale highlight bands; G the brass rose and rim; B handles; A the dark inside.
    "palette.garden.watering_can": ("7FBFC4", "B4DEDD", "E3BE62", "5E9FA8", "35585E", "4B5C7A", "FFFFFF"),
    # wicker honey (the T40 basket's row); R over-bumps; G rim and handle; B the inside; A the dyed rows.
    "palette.garden.basket": ("D3A05E", "EBC98C", "E4BB7C", "B9884D", "DC8C7E", "6F5048", "FFFFFF"),
    # meadow (the backdrop's base); R sunny patches; G deeper patches; B trodden earth along the path.
    "palette.garden.ground": ("8DC166", "A6D07A", "7DB55C", "B9A27C", "8DC166", "4E6D5D", "FFFFFF"),
    # Plants: base stems; R leaves; then each plant's own colours (comments).
    # G pink flower heads; B their pale tips; A the pale leaf marks.
    "palette.garden.sweet_clover": ("5F9A48", "79B95A", "EE8FB4", "FFD3E3", "A5D483", "3F6346", "FFFFFF"),
    # G white petals; B red fruit; A yellow hearts and seeds.
    "palette.garden.wild_strawberry": ("5E9646", "74B055", "FFFCF4", "E2403D", "F5C84C", "4A3D48", "FFFFFF"),
    # G white petals; B yellow hearts; A the cream near the heart and the buds' pale tips.
    "palette.garden.daisy": ("5F9C49", "79B65A", "FFFDF7", "F2C24F", "F4E6C6", "4E6D5D", "FFFFFF"),
    # G orange root; B its sunny shoulder; A the root's soft rings.
    "palette.garden.round_carrot": ("5C9C44", "86C463", "EE8630", "FFB45E", "D86E25", "5A4A40", "FFFFFF"),
    # base hairy grey-green stems; G red petals; B the dark heart; A the pale seed knob and buds.
    "palette.garden.poppy": ("7FA864", "8DBA72", "E8463B", "3B2C35", "B9D58E", "4E3E48", "FFFFFF"),
    # base woody stems; R leaves; G pink bells; B blueberries; A their pale bloom.
    "palette.garden.blueberry": ("7A5E4C", "6CA758", "F4B5C6", "4C69C6", "97ADE6", "3D4A5E", "FFFFFF"),
    # base grey-green stems; R grey leaves; G green spikes; B purple spikes; A lilac tips.
    "palette.garden.lavender": ("8CA488", "AEBFAA", "98BE6C", "9474D2", "C8B3EE", "4F4A66", "FFFFFF"),
    # base green blades; R light green; G gold; B pale gold; A deeper gold.
    "palette.garden.golden_wheat": ("7DB35A", "9CCB71", "E8BE55", "F5DC8E", "C99A42", "6B5A3E", "FFFFFF"),
    # G deep blue flowers; B light buds; A mid blue.
    "palette.garden.meadow_indigo": ("5E9050", "77AE62", "3D52B8", "A7C0EA", "6B85D8", "34395E", "FFFFFF"),
    # G yellow petals; B the brown heart; A light petal tips and the heart's ring.
    "palette.garden.sunflower": ("6AA14A", "7BB759", "F8C531", "7A4A2B", "FFE27E", "5A4632", "FFFFFF"),
    # base bark; R foliage; G red apples; B white blossom; A pink blossom.
    "palette.garden.dwarf_apple": ("8E6650", "74B25A", "E04B42", "FFF6F2", "F4A6BB", "3F5A40", "FFFFFF"),
    # base vines; R leaves; G yellow flowers; B pale rind; A darker stripes.
    "palette.garden.round_melon": ("5E9A46", "79B55A", "F4CF46", "C8D98A", "7FAF5A", "4A5E3E", "FFFFFF"),
    # base vines; R leaves; G pink flowers; B mauve flowers; A the bamboo stake.
    "palette.garden.sweet_pea": ("6CA552", "86BE68", "F5A3C6", "B69BE0", "D9BE84", "4E4A62", "FFFFFF"),
}

GD_ROWS = {"GD_Ground": "palette.garden.ground", "GD_Bed": "palette.garden.bed", "GD_BedCover": "palette.garden.cover",
           "GD_Path": "palette.garden.path", "GD_Fence": "palette.garden.fence",
           "GD_WateringCan": "palette.garden.watering_can", "GD_Basket": "palette.garden.basket"}
for _key, _stem in GD_PLANTS:
    for _s in (1, 2, 3):
        GD_ROWS[plant_object_name(_stem, _s)] = "palette.garden." + _key

UP = Vector((0.0, 0.0, 1.0))
# Toward the set camera from a plant (GardenSetPipeline's camera, 44 degrees down, in front): flowers face it.
TO_CAMERA = Vector((0.0, -0.72, 0.69)).normalized()

BASE = (0.0, 0.0, 0.0, 0.0)
RR = (1.0, 0.0, 0.0, 0.0)
GG = (0.0, 1.0, 0.0, 0.0)
BB = (0.0, 0.0, 1.0, 0.0)
AA = (0.0, 0.0, 0.0, 1.0)


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


# ---------------------------------------------------------------- layout shared with GardenSetPipeline

BED_HALF = (0.52, 0.44)              # outer half size (m): 1.04 x 0.88
BED_PLANK = 0.05                     # plank thickness
SOIL_HALF = (BED_HALF[0] - BED_PLANK - 0.01, BED_HALF[1] - BED_PLANK - 0.01)   # 0.46 x 0.38, the soil inside
PLANK_TOP = 0.258
SOIL_TOP = 0.240                     # soil height at a plant's root (the bed's center, the mound's top)
POST_TOP = 0.266
BED_X = 0.76                         # beds alternate left and right of the path
BED_DZ = 0.78                        # plot i stands BED_DZ * i behind plot 0 (Blender +Y)
FENCE_Y = 4.30
FENCE_HALF = 2.60
FENCE_BOW = 0.40                     # the fence's ends come this much toward the camera


def bed_center(i):
    return Vector(((-BED_X if i % 2 == 0 else BED_X), BED_DZ * i, 0.0))


# ---------------------------------------------------------------- scene

def _gd_scene():
    scene = bpy.data.scenes.get(GD_SCENE) or bpy.data.scenes.new(GD_SCENE)
    col = bpy.data.collections.get(GD_COLLECTION) or bpy.data.collections.new(GD_COLLECTION)
    if col.name not in scene.collection.children:
        scene.collection.children.link(col)
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    return scene, col


class Parts:
    """Per-vertex region and occlusion tags while a bmesh is built (to_mesh keeps the vertex order). A region is a
    (r, g, b, a) tuple or a function of the vertex position; occlusion likewise (a number or a function)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.regions = []
        self.occ = []
        self.rooted = []

    def tag(self, region, occ=0.05, rooted=False):
        n = len(self.bm.verts) - len(self.regions)
        self.regions.extend([region] * n)
        self.occ.extend([occ] * n)
        self.rooted.extend([rooted] * n)

    def region(self, co, i):
        r = self.regions[i]
        return r(co) if callable(r) else r

    def occlusion(self, co, i):
        o = self.occ[i]
        return o(co) if callable(o) else o


def finish(name, parts, col, height, ground_occ=0.35, ground_z=0.0, recalc=True, scale=1.0):
    """The object from its parts: regions as tagged (read at the authored size), occlusion as tagged plus a soft shade
    near the ground (z under ground_z + 5 cm), height factor z / height; the whole piece then scaled by `scale` around
    its pivot (a plant's size for the phone)."""
    bm = parts.bm
    bm.verts.ensure_lookup_table()
    regs = [parts.region(v.co.copy(), i) for i, v in enumerate(bm.verts)]
    occs = [parts.occlusion(v.co.copy(), i) for i, v in enumerate(bm.verts)]
    if scale != 1.0:
        for v in bm.verts:
            v.co *= scale
        height *= scale
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    _nd_replace(name)
    obj = object_from_bmesh(name, bm, [col], smooth=True)

    def regions(co, n, i):
        return regs[i]

    def occlusion(co, n, i):
        foot = ground_occ * (1.0 - smoothstep(ground_z - 0.01, ground_z + 0.05, co.z))
        return (min(0.85, occs[i] + foot), max(0.0, co.z) / height)

    write_layers(obj, regions, occlusion)
    obj["gd_rooted"] = [i for i, r in enumerate(parts.rooted) if r] or [-1]
    return obj


# ---------------------------------------------------------------- the parametric plant generator

def _frame(d, hint=UP):
    d = d.normalized()
    s = hint.cross(d)
    if s.length < 1e-6:
        s = d.orthogonal()
    s.normalize()
    return d, s, d.cross(s).normalized()


def curve(base, top, lean=None, bow=0.0, count=4):
    """Points of a stem from base to top, bowed sideways by `bow` (m) toward `lean` at its middle."""
    pts = []
    side = lean.normalized() if lean is not None and lean.length > 1e-6 else Vector()
    for j in range(count):
        t = j / (count - 1)
        pts.append(base.lerp(top, t) + side * (bow * math.sin(math.pi * t)))
    return pts


def stem(p, pts, r0, r1=None, sides=5, region=BASE, occ=0.08, cap=True, rooted=True):
    """A tube through pts tapering from r0 to r1; starts 12 mm under the soil when rooted (its open foot hidden)."""
    r1 = r0 * 0.7 if r1 is None else r1
    pts = list(pts)
    if rooted:
        pts = [pts[0] - UP * 0.012] + pts
    n = len(pts)
    radii = [r0 + (r1 - r0) * (j / (n - 1)) for j in range(n)]
    d = (pts[-1] - pts[-2]).normalized()
    _nd_loft(p.bm, pts, radii, radii, sides, end=(pts[-1] + d * r1 * 0.8) if cap else None)
    p.tag(region, occ, rooted)


def leaf(p, base, d, length, width, hint=UP, droop=0.25, cup=0.2, n=8, region=RR, occ=0.06, lift=0.004, taper=0.25,
         notch=0.0):
    """A rounded leaf: an oval lens from `base` along `d`, `width` across, its tip drooping by droop x length and its
    sides cupped up by cup x width / 2; taper > 0 narrows it toward the tip, notch > 0 dents the tip (a heart)."""
    d, s, nrm = _frame(d, hint)
    out = []
    for k in range(n):
        t = math.tau * k / n
        a = 0.5 - 0.5 * math.cos(t)
        w = math.sin(t) * 0.5 * width * (1.0 - taper * a * a)
        reach = length * a * (1.0 - notch * smoothstep(0.85, 1.0, a) * (1.0 - abs(math.sin(t)) * 3.0))
        drop = -droop * length * a * a
        out.append(base + d * reach + s * w + nrm * (drop + cup * abs(w)))
    center = base + d * (length * 0.5) + nrm * (-droop * length * 0.25)
    _nd_lens(p.bm, center, out, lift, lift * 0.5, nrm)
    p.tag(region, occ)


def round_leaflets(p, at, normal, size, count=3, turn=0.0, region=RR, occ=0.05, notch=0.18, spread=1.0):
    """Clover-like leaflets: `count` round lenses around `at`, each a little heart (its outer tip dented)."""
    nrm = normal.normalized()
    u = nrm.orthogonal().normalized()
    w = nrm.cross(u).normalized()
    for j in range(count):
        a = turn + math.tau * j / count * spread
        d = u * math.cos(a) + w * math.sin(a)
        side = nrm.cross(d).normalized()
        c = at + d * size * 0.52
        out = []
        for k in range(8):
            t = math.tau * k / 8
            r = size * 0.5 * (1.0 - notch * max(0.0, math.cos(t)) ** 8)
            out.append(c + d * (math.cos(t) * r) + side * (math.sin(t) * r) + nrm * (0.08 * size * math.cos(t)))
        _nd_lens(p.bm, c + nrm * 0.001, out, size * 0.06, size * 0.03, nrm)
        p.tag(region, occ)


def flower(p, center, facing, radius, petals=5, turn=0.0, cup=0.0, heart=0.32, dome=0.28, petal=None, heart_region=BB,
           petal_region=GG, occ=0.03, shoulders=((-0.5, 0.55), (-0.3, 0.93), (0.0, 1.0), (0.3, 0.93)), heart_segments=7):
    """Round petals around a domed heart (the daisy of pw_postcards, generalised): each petal a round tip between two
    shoulders, notched at its foot (never a star); `cup` raises the petals' tips along `facing`. petal: region of a
    petal point from its reach (0 at the heart, 1 at the tip) or a fixed region."""
    f = facing.normalized()
    u = f.orthogonal().normalized()
    w = f.cross(u).normalized()
    out, reaches = [], []
    for k in range(petals):
        for offset, reach in shoulders:
            a = turn + (k + offset) / petals * math.tau
            out.append(center + (u * math.cos(a) + w * math.sin(a)) * radius * reach + f * (cup * radius * reach * reach))
            reaches.append(reach)
    ring, top, bottom = _nd_lens(p.bm, center, out, radius * 0.06, radius * 0.05, f)
    if petal is None:
        p.tag(petal_region, occ)
    else:
        start = len(p.regions)
        p.tag(petal_region, occ)
        for j, r in enumerate(reaches):
            p.regions[start + j] = petal(r)
        p.regions[start + len(reaches)] = petal(0.0)
        p.regions[start + len(reaches) + 1] = petal(0.0)
    if heart > 0.0:
        hr = radius * heart
        base = center + f * (radius * 0.07 + cup * radius * heart * heart)
        hring = [p.bm.verts.new(base + (u * math.cos(t) + w * math.sin(t)) * hr)
                 for t in (turn + j / heart_segments * math.tau for j in range(heart_segments))]
        apex = p.bm.verts.new(base + f * (hr * dome))
        under = p.bm.verts.new(base - f * (hr * 0.15))
        for j in range(heart_segments):
            m = (j + 1) % heart_segments
            p.bm.faces.new((apex, hring[j], hring[m]))
            p.bm.faces.new((under, hring[m], hring[j]))
        p.tag(heart_region, occ + 0.04)


def ball(p, center, radii, axis=None, segments=7, rings=5, region=GG, occ=0.04, displace=None):
    """An ellipsoid (radii x, y, z), its poles along `axis` (default up)."""
    rot = None
    if axis is not None:
        rot = UP.rotation_difference(axis.normalized()).to_matrix()
    if not isinstance(radii, Vector):
        radii = Vector((radii, radii, radii))
    _nd_sphere(p.bm, center, radii, segments, rings, rotation=rot, displace=displace)
    p.tag(region, occ)


def calyx(p, center, axis, radius, lobes=5, region=BASE, occ=0.05):
    """A little round-lobed collar (the green cap of a berry or a bud): never pointed."""
    out = []
    a0 = axis.normalized()
    u = a0.orthogonal().normalized()
    w = a0.cross(u).normalized()
    for k in range(lobes):
        for offset, reach in ((-0.5, 0.45), (0.0, 1.0)):
            a = (k + offset) / lobes * math.tau
            out.append(center + (u * math.cos(a) + w * math.sin(a)) * radius * reach - a0 * (radius * 0.25 * reach))
    _nd_lens(p.bm, center, out, radius * 0.18, radius * 0.08, a0)
    p.tag(region, occ)


def spike(p, base, axis, length, radius, rings=9, sides=6, bumps=0.3, region=GG, tip_region=None, occ=0.05):
    """A soft beaded spike (lavender, wheat ears, indigo racemes): a tube whose rings bulge in turn, round at both ends."""
    a0 = axis.normalized()
    pts = [base + a0 * (length * j / (rings - 1)) for j in range(rings)]
    radii = [radius * (0.55 + 0.45 * math.sin(math.pi * (0.12 + 0.88 * j / (rings - 1)))) for j in range(rings)]

    def shape(i, k, pnt, s, u):
        bead = 1.0 + bumps * (0.5 + 0.5 * math.cos(math.pi * i + k * math.pi * 2.0 / sides * 2.0))
        c = pts[i]
        return c + (pnt - c) * bead

    _nd_loft(p.bm, pts, radii, radii, sides, start=base - a0 * radius * 0.5, end=pts[-1] + a0 * radius * 0.8, shape=shape)
    if tip_region is None:
        p.tag(region, occ)
    else:
        p.tag(lambda co, b=base, a=a0, L=length, r0=region, r1=tip_region: mix(r0, r1, smoothstep(0.45, 1.0, (co - b).dot(a) / L)), occ)


def crown(p, center, radii, seed, segments=11, rings=7, region=RR, occ=0.08):
    """A round leafy crown: an ellipsoid with soft uneven lobes (the backdrop trees' recipe), never spiky."""
    rng = random.Random(seed)
    lobes = [Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.3, 1))).normalized() for _ in range(6)]

    def bumps(d):
        return 1.0 + 0.11 * sum(max(0.0, d.dot(l)) ** 5 for l in lobes) - 0.06 * max(0.0, -d.z)

    _nd_sphere(p.bm, center, radii, segments, rings, displace=bumps)
    p.tag(region, lambda co, c=center, rz=radii.z: occ + 0.3 * (1.0 - smoothstep(-rz, rz * 0.6, co.z - c.z)))


# ---------------------------------------------------------------- the plants, three stages each (doc 01, table 8.2)

def _around(rng, count, r0, r1, turn=0.0, jitter=0.35):
    """count directions around the up axis with radii between r0 and r1."""
    out = []
    for j in range(count):
        a = turn + math.tau * (j + rng.uniform(-jitter, jitter)) / count
        out.append((a, rng.uniform(r0, r1)))
    return out


def _dir(a, up=0.0):
    return Vector((math.cos(a), math.sin(a), up)).normalized()


def _face(rng, spread=0.25, up=0.0):
    """Toward the camera, turned a little either way."""
    v = TO_CAMERA + Vector((rng.uniform(-spread, spread), rng.uniform(-spread * 0.4, spread * 0.4), up))
    return v.normalized()


def _trefoil_tuft(p, rng, count, r0, r1, h0, h1, size0, size1):
    for a, r in _around(rng, count, r0, r1, rng.uniform(0, 6.28)):
        h = rng.uniform(h0, h1)
        top = Vector((math.cos(a) * r, math.sin(a) * r, h))
        stem(p, curve(Vector((math.cos(a) * r * 0.2, math.sin(a) * r * 0.2, 0.0)), top, _dir(a), 0.015, 3), 0.0045,
             0.0035, sides=4, region=BASE)
        nrm = (UP * 1.0 + _dir(a) * 0.35 + TO_CAMERA * 0.25).normalized()
        round_leaflets(p, top, nrm, rng.uniform(size0, size1), 3, rng.uniform(0, 6.28),
                       region=lambda co, t=top: mix(RR, AA, 0.35 * (1.0 - smoothstep(0.0, 0.02, (co - t).length))))


def build_sweet_clover(stage, rng):
    """Trèfle doux: deux feuilles rondes / touffe de trèfles / touffe avec 3 fleurs roses rondes."""
    p = Parts()
    if stage == 1:
        for x, y, h, turn in ((-0.045, 0.01, 0.075, 0.3), (0.05, -0.02, 0.06, 1.9)):
            base = Vector((x, y, 0.0))
            top = base + Vector((0.004, -0.004, h))
            stem(p, curve(base, top, Vector((1, 0, 0)), 0.006), 0.006, 0.0045, region=BASE)
            for side in (-1.0, 1.0):
                d = Vector((math.cos(turn) * side, math.sin(turn) * side, 0.55))
                leaf(p, top, d, 0.06, 0.05, droop=0.25, cup=0.35, taper=0.0, region=RR)
        _trefoil_tuft(p, rng, 1, 0.0, 0.01, 0.065, 0.065, 0.04, 0.04)
        return p, 0.16
    count = 9 if stage == 2 else 7
    _trefoil_tuft(p, rng, count, 0.06, 0.2, 0.09, 0.16, 0.07, 0.09)
    if stage == 3:
        for a, r, h in ((-1.9, 0.09, 0.26), (-0.85, 0.13, 0.22), (-2.75, 0.13, 0.21)):
            top = Vector((math.cos(a) * r, math.sin(a) * r, h))
            stem(p, curve(Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, 0.0)), top, _dir(a), 0.02, 3), 0.0055, 0.0045,
                 region=BASE)
            ball(p, top + UP * 0.034, Vector((0.044, 0.044, 0.05)), segments=7, rings=5,
                 region=lambda co, t=top: mix(GG, BB, smoothstep(0.04, 0.08, co.z - t.z)),
                 displace=lambda d: 1.0 + 0.08 * math.cos(7.0 * math.atan2(d.y, d.x)) * (1.0 - abs(d.z)))
            calyx(p, top + UP * 0.006, UP, 0.024, region=RR)
    return p, 0.32


def _trifoliate(p, base, top, a, size, region=RR):
    stem(p, curve(base, top, _dir(a), 0.015, 4), 0.0045, 0.0035, sides=4, region=BASE)
    nrm = (UP + _dir(a) * 0.5 + TO_CAMERA * 0.2).normalized()
    _, s, _ = _frame(_dir(a), UP)
    for j, turn in enumerate((-0.9, 0.0, 0.9)):
        d = (_dir(a + turn) + UP * 0.25).normalized()
        leaf(p, top, d, size * (1.12 if j == 1 else 1.0), size * 0.78, droop=0.12, cup=0.25, taper=0.2, region=region)


def build_wild_strawberry(stage, rng):
    """Fraise des bois: pousse à 3 feuilles / petites fleurs blanches / fraises rouges rondes."""
    p = Parts()
    if stage == 1:
        _trifoliate(p, Vector((0.0, 0.0, 0.0)), Vector((0.01, -0.01, 0.085)), -1.2, 0.055)
        _trifoliate(p, Vector((0.02, 0.01, 0.0)), Vector((0.05, 0.03, 0.06)), 0.6, 0.04)
        return p, 0.15
    for a, r in _around(rng, 5, 0.08, 0.14, 0.4):
        top = Vector((math.cos(a) * r, math.sin(a) * r, rng.uniform(0.14, 0.19)))
        _trifoliate(p, Vector((math.cos(a) * 0.02, math.sin(a) * 0.02, 0.0)), top, a, rng.uniform(0.075, 0.09))
    if stage == 2:
        for a, r, h in ((-1.6, 0.17, 0.2), (-0.6, 0.2, 0.17), (-2.5, 0.19, 0.16), (1.3, 0.15, 0.21)):
            top = Vector((math.cos(a) * r, math.sin(a) * r, h))
            stem(p, curve(Vector((0.0, 0.0, 0.0)), top, _dir(a), 0.03, 3), 0.004, 0.003, sides=4)
            flower(p, top, _face(rng, 0.2), 0.036, 5, rng.uniform(0, 1), cup=0.15, heart=0.36, dome=0.35,
                   shoulders=((-0.42, 0.62), (-0.22, 0.95), (0.0, 1.0), (0.22, 0.95)), heart_region=AA, petal_region=GG)
        return p, 0.25
    # Four plump red berries hanging on arched stems at the front and sides (the camera sees them all).
    for a, r, h in ((-1.55, 0.21, 0.07), (-0.7, 0.24, 0.065), (-2.4, 0.24, 0.07), (0.25, 0.25, 0.06)):
        tip = Vector((math.cos(a) * r, math.sin(a) * r, h))
        stem(p, [Vector((0, 0, 0)), Vector((math.cos(a) * r * 0.5, math.sin(a) * r * 0.5, h + 0.09)), tip + UP * 0.055], 0.0045,
             0.0035, sides=4)
        c = tip
        start = len(p.regions)
        ball(p, c, Vector((0.038, 0.038, 0.045)), segments=7, rings=5, region=BB,
             displace=lambda d: (1.0 - 0.24 * max(0.0, -d.z)) * (1.0 + 0.04 * math.cos(5.0 * math.atan2(d.y, d.x))))
        for j in range(start, len(p.regions)):
            if j % 4 == 1:
                p.regions[j] = mix(BB, AA, 0.4)
        calyx(p, c + UP * 0.04, UP, 0.028, region=RR)
    return p, 0.22


def _rosette(p, rng, count, length, width, lift=0.25, droop=0.12, turn=0.0, region=RR):
    for a, l in _around(rng, count, length * 0.85, length * 1.1, turn, 0.25):
        leaf(p, Vector((0.0, 0.0, 0.006)), _dir(a, lift), l, width, droop=droop, cup=0.25, taper=-0.1, region=region)


def build_daisy(stage, rng):
    """Marguerite: rosette de feuilles / boutons fermés / 3 marguerites ouvertes."""
    p = Parts()
    _rosette(p, rng, 9 if stage == 1 else 8, 0.1 if stage == 1 else 0.14, 0.05 if stage == 1 else 0.06)
    if stage == 1:
        leaf(p, Vector((0.0, 0.0, 0.006)), Vector((0.1, -0.2, 1.0)), 0.06, 0.035, droop=0.1, cup=0.3, region=RR)
        return p, 0.08
    heads = ((-0.085, -0.05, 0.25, 0.4), (0.03, -0.1, 0.3, 0.0), (0.1, 0.01, 0.22, 0.25))
    for x, y, h, turn in heads:
        top = Vector((x, y, h))
        stem(p, curve(Vector((x * 0.25, y * 0.25, 0.0)), top, Vector((x, y, 0)), 0.02, 4), 0.0055, 0.0045)
        if stage == 2:
            ball(p, top + UP * 0.014, Vector((0.025, 0.025, 0.031)), segments=7, rings=5,
                 region=lambda co, t=top: mix(BASE, mix(RR, AA, 0.7), smoothstep(0.024, 0.044, co.z - t.z)))
            calyx(p, top + UP * 0.004, UP, 0.022, region=RR)
        else:
            f = _face(rng, 0.18)
            flower(p, top + f * 0.004, f, 0.068, 12, turn, cup=0.08, heart=0.3, dome=0.4,
                   petal=lambda r: mix(GG, AA, 0.85 * (1.0 - smoothstep(0.3, 0.7, r))), heart_region=BB)
    return p, 0.36


def _frond(p, base, d, length, leaflets, size, region=RR):
    """A carrot top: a thin stalk with round leaflets in pairs along it, a rounded one at its tip."""
    d = d.normalized()
    tip = base + d * length
    pts = curve(base, tip, (d.cross(UP).cross(d)) * -1.0, length * 0.08, 3)
    stem(p, pts, 0.004, 0.0025, sides=4, rooted=base.z <= 0.001)
    _, s, nrm = _frame(d, UP)
    for j in range(leaflets):
        t = 0.45 + 0.5 * j / max(1, leaflets - 1)
        at = base.lerp(tip, t)
        for side in (-1.0, 1.0):
            dd = (s * side + d * 0.6 + UP * 0.15).normalized()
            leaf(p, at, dd, size * (1.0 - 0.25 * t), size * 0.75, droop=0.1, cup=0.2, n=6, taper=0.0, region=region)
    leaf(p, tip, d, size * 0.85, size * 0.7, droop=0.1, cup=0.2, n=6, taper=0.0, region=region)


def build_round_carrot(stage, rng):
    """Carotte ronde: fanes fines / fanes touffues / grosse carotte ronde qui dépasse du sol."""
    p = Parts()
    if stage == 1:
        for a in (-1.4, 0.4, 2.3):
            _frond(p, Vector((0, 0, 0)), _dir(a, 2.6), 0.11, 2, 0.028)
        return p, 0.14
    if stage == 3:
        # The root: a big round carrot, two thirds out of the soil (its foot in the soil, where it grows).
        start = len(p.regions)
        ball(p, Vector((0.0, 0.0, 0.055)), Vector((0.12, 0.12, 0.11)), segments=11, rings=7,
             region=lambda co: mix(mix(GG, AA, 0.7 * (0.5 + 0.5 * math.cos(co.z * 120.0)) * (1.0 - smoothstep(0.08, 0.15, co.z))),
                                   BB, smoothstep(0.09, 0.16, co.z) * 0.8),
             occ=lambda co: 0.03 + 0.25 * (1.0 - smoothstep(-0.03, 0.03, co.z)))
        p.rooted[start:] = [True] * (len(p.regions) - start)
        top = 0.16
        count, length = 7, 0.2
    else:
        top = 0.0
        count, length = 8, 0.22
    for j in range(count):
        a = math.tau * j / count + rng.uniform(-0.25, 0.25)
        _frond(p, Vector((math.cos(a) * 0.012, math.sin(a) * 0.012, top)), _dir(a, rng.uniform(2.0, 3.2)),
               length * rng.uniform(0.85, 1.1), 2, 0.04)
    if stage == 2:
        # The orange shoulder just showing at the soil.
        start = len(p.regions)
        ball(p, Vector((0.0, 0.0, -0.006)), Vector((0.045, 0.045, 0.022)), segments=7, rings=4, region=GG)
        p.rooted[start:] = [True] * (len(p.regions) - start)
    return p, 0.36 if stage == 3 else 0.24


def build_poppy(stage, rng):
    """Coquelicot: tiges velues / boutons penchés / 3 coquelicots ouverts."""
    p = Parts()
    for a, l in _around(rng, 6, 0.09, 0.13, 0.2):
        leaf(p, Vector((0, 0, 0.006)), _dir(a, 0.5), l, 0.04, droop=0.25, cup=0.2, taper=0.3, region=RR)
    fuzzy = lambda i, k, pnt, s, u: pnt + (s * math.cos(k) + u * math.sin(k)) * 0.0012 * ((i + k) % 2)
    if stage == 1:
        for a, h in ((-1.3, 0.12), (0.5, 0.1), (2.2, 0.11)):
            top = Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, h))
            pts = [Vector((0, 0, -0.012))] + curve(Vector((0, 0, 0)), top, _dir(a), 0.01, 5)
            _nd_loft(p.bm, pts, [0.0055] * len(pts), [0.0055] * len(pts), 5, end=top + UP * 0.006, shape=fuzzy)
            p.tag(BASE, 0.08, True)
            leaf(p, top, _dir(a, 1.5), 0.04, 0.025, droop=0.2, cup=0.2, region=RR)
        return p, 0.14
    heads = ((-0.09, -0.06, 0.3, 0.2), (0.04, -0.11, 0.34, 1.1), (0.1, 0.04, 0.27, 0.6))
    for x, y, h, turn in heads:
        top = Vector((x, y, h))
        if stage == 2:
            # Nodding: the stem arches over at its top, the bud hangs.
            hook = top + Vector((x * 0.3, -0.03, -0.035))
            pts = [Vector((x * 0.2, y * 0.2, -0.012)), Vector((x * 0.25, y * 0.25, 0.0)), Vector((x * 0.6, y * 0.6, h * 0.55)),
                   top, top + Vector((x * 0.15, -0.02, 0.008)), hook]
            _nd_loft(p.bm, pts, [0.0045] * len(pts), [0.0045] * len(pts), 5, shape=fuzzy)
            p.tag(BASE, 0.08, True)
            axis = (hook - top).normalized()
            ball(p, hook + axis * 0.024, Vector((0.02, 0.02, 0.03)), axis=axis, segments=7, rings=5,
                 region=lambda co, c=hook + axis * 0.03, ax=axis: mix(AA, GG, 0.8 * smoothstep(0.012, 0.03, (co - c).dot(ax))))
        else:
            pts = [Vector((x * 0.2, y * 0.2, -0.012))] + curve(Vector((x * 0.25, y * 0.25, 0.0)), top, Vector((x, y, 0)), 0.02, 4)
            _nd_loft(p.bm, pts, [0.0045] * len(pts), [0.0045] * len(pts), 5, end=top + UP * 0.004, shape=fuzzy)
            p.tag(BASE, 0.08, True)
            f = _face(rng, 0.2)
            flower(p, top + f * 0.006, f, 0.075, 4, turn, cup=0.42, heart=0.32, dome=0.45,
                   shoulders=((-0.5, 0.62), (-0.36, 0.86), (-0.18, 0.98), (0.0, 1.0), (0.18, 0.98), (0.36, 0.86)),
                   petal=lambda r: mix(BB, GG, smoothstep(0.08, 0.3, r)), heart_region=BB)
            # The pale seed knob in the dark heart.
            ball(p, top + f * (0.006 + 0.075 * (0.07 + 0.42 * 0.32 * 0.32) + 0.024 * 0.42), Vector((0.011, 0.011, 0.008)), axis=f,
                 segments=6, rings=3, region=AA)
    return p, 0.4


def _crown_leaves(p, rng, center, radii, count, size, front=-0.1, lift=1.03, region=RR):
    """Leaf scales lying on a crown's surface where the camera sees it (its top and front), each a rounded lens
    turned along the surface: the crown reads as foliage, never as a smooth ball."""
    placed = 0
    tries = 0
    while placed < count and tries < 600:
        tries += 1
        th = rng.uniform(0, math.tau)
        z = rng.uniform(-0.3, 0.95)
        d = Vector((math.cos(th) * math.sqrt(1 - z * z), math.sin(th) * math.sqrt(1 - z * z), z))
        if d.dot(TO_CAMERA) < front:
            continue
        at = center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * lift
        tangent = d.cross(UP).normalized() if abs(d.z) < 0.95 else Vector((1, 0, 0))
        tangent = (tangent * math.cos(rng.uniform(-1, 1)) + UP * 0.4).normalized()
        leaf(p, at, tangent, size * rng.uniform(0.9, 1.15), size * 0.62, hint=d, droop=0.15, cup=0.25, n=6,
             region=mix(region, (0, 0, 0, 0), rng.uniform(0.0, 0.25)))
        placed += 1


def _bush(p, rng, center, radii, leaves, leaf_size, seed):
    crown(p, center, radii, seed, segments=9, rings=5, region=mix(RR, BASE, 0.35))
    for j in range(3):
        a = math.tau * j / 3 + 0.4
        stem(p, [Vector((0, 0, 0)), Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, center.z - radii.z * 0.6))], 0.012, 0.01,
             region=BASE, cap=False)
    _crown_leaves(p, rng, center, radii, leaves, leaf_size)


def _bush_points(rng, center, radii, count, min_gap, front=0.2):
    pts = []
    tries = 0
    while len(pts) < count and tries < 2000:
        tries += 1
        th = rng.uniform(0, math.tau)
        z = rng.uniform(-0.35, 0.8)
        d = Vector((math.cos(th) * math.sqrt(1 - z * z), math.sin(th) * math.sqrt(1 - z * z), z))
        if d.dot(TO_CAMERA) < front:
            continue
        at = center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z))
        if any((at - q).length < min_gap for q, _ in pts):
            continue
        pts.append((at, d))
    return pts


def build_blueberry(stage, rng):
    """Myrtillier: petit buisson / buisson à clochettes roses / buisson couvert de myrtilles."""
    p = Parts()
    if stage == 1:
        for a, h in ((-1.2, 0.13), (0.6, 0.11), (2.3, 0.12)):
            top = Vector((math.cos(a) * 0.035, math.sin(a) * 0.035, h))
            stem(p, curve(Vector((0, 0, 0)), top, _dir(a), 0.01), 0.006, 0.004, region=BASE)
            for j, t in enumerate((0.45, 0.7, 1.0)):
                at = Vector((0, 0, 0)).lerp(top, t)
                leaf(p, at, _dir(a + 1.9 * j, 0.6), 0.042, 0.028, droop=0.15, cup=0.25, n=6, region=RR)
        return p, 0.16
    center = Vector((0.0, 0.0, 0.17))
    radii = Vector((0.2, 0.18, 0.15))
    _bush(p, rng, center, radii, 26, 0.068, 61)
    if stage == 2:
        for at, d in _bush_points(rng, center, radii * 1.08, 7, 0.06, 0.15):
            down = (d * 0.3 - UP).normalized()
            ball(p, at + down * 0.013, Vector((0.018, 0.018, 0.023)), axis=-down, segments=6, rings=4, region=GG, occ=0.03)
            calyx(p, at, -down, 0.013, region=RR)
        return p, 0.36
    for at, d in _bush_points(rng, center, radii * 1.06, 12, 0.05, 0.15):
        c = at + d * 0.014
        ball(p, c, 0.023, segments=6, rings=4,
             region=lambda co, cc=c: mix(BB, AA, 0.75 * smoothstep(0.008, 0.022, (co - cc).dot(TO_CAMERA))))
    return p, 0.36


def _tuft(p, rng, count, length, width, spread, region, lean=0.75):
    for a, l in _around(rng, count, length * 0.8, length * 1.1, rng.uniform(0, 6.28), 0.4):
        d = Vector((math.cos(a) * spread, math.sin(a) * spread, lean))
        leaf(p, Vector((math.cos(a) * 0.01, math.sin(a) * 0.01, 0.004)), d, l, width, droop=0.18, cup=0.3, n=6,
             taper=0.35, region=region)


def build_lavender(stage, rng):
    """Lavande: touffe grise / épis verts / épis violets."""
    p = Parts()
    _tuft(p, rng, 14 if stage == 1 else 10, 0.11 if stage == 1 else 0.13, 0.018, 0.6, RR)
    if stage == 1:
        return p, 0.13
    for j in range(8):
        a = math.tau * j / 8 + rng.uniform(-0.2, 0.2)
        h = rng.uniform(0.26, 0.34)
        top = Vector((math.cos(a) * 0.13, math.sin(a) * 0.13, h))
        stem(p, curve(Vector((math.cos(a) * 0.01, math.sin(a) * 0.01, 0.0)), top, _dir(a), 0.015, 3), 0.0045, 0.0035,
             sides=4, cap=False)
        axis = (top - Vector((0, 0, 0))).normalized()
        spike(p, top, axis, 0.095, 0.02, rings=7, sides=5, bumps=0.35,
              region=GG if stage == 2 else BB, tip_region=GG if stage == 2 else AA)
    return p, 0.44


def _blade(p, base, d, length, width, region, droop=0.25):
    leaf(p, base, d, length, width, droop=droop, cup=0.35, n=6, taper=0.6, region=region, lift=0.002)


def build_golden_wheat(stage, rng):
    """Blé doré: brins verts / épis verts / épis dorés."""
    p = Parts()
    ripe = stage == 3
    blade_region = mix(BASE, RR, 0.5) if not ripe else mix(RR, GG, 0.55)
    for a, l in _around(rng, 12 if stage == 1 else 7, 0.12, 0.17, 0.3):
        leaf(p, Vector((math.cos(a) * 0.01, math.sin(a) * 0.01, 0.003)), Vector((math.cos(a) * 0.4, math.sin(a) * 0.4, 1.0)),
             l if stage == 1 else l * 1.1, 0.022, droop=0.25, cup=0.35, n=8 if stage == 1 else 6, taper=0.6,
             region=blade_region, lift=0.002)
    if stage == 1:
        return p, 0.16
    for j in range(6):
        a = math.tau * j / 6 + rng.uniform(-0.25, 0.25)
        r = rng.uniform(0.06, 0.13)
        h = rng.uniform(0.4, 0.46)
        nod = Vector((math.cos(a) * 0.03, math.sin(a) * 0.03 - 0.02, 0.0)) if ripe else Vector()
        top = Vector((math.cos(a) * r, math.sin(a) * r, h)) + nod
        stem(p, curve(Vector((math.cos(a) * 0.015, math.sin(a) * 0.015, 0.0)), top, _dir(a), 0.02 + (0.02 if ripe else 0.0), 3),
             0.005, 0.004, sides=4, cap=False, region=BASE if not ripe else mix(GG, AA, 0.6))
        axis = ((top - Vector((0, 0, 0))).normalized() + (nod * 6.0)).normalized()
        spike(p, top, axis, 0.1, 0.019, rings=8, sides=6, bumps=0.4,
              region=mix(RR, BASE, 0.3) if not ripe else GG, tip_region=mix(RR, BASE, 0.3) if not ripe else BB)
    return p, 0.56


def build_meadow_indigo(stage, rng):
    """Indigo des prés: tige à feuilles ovales / grappes de boutons / grappes de fleurs bleu profond."""
    p = Parts()
    stems = ((0.0, 0.0, 0.16, 0.0),) if stage == 1 else ((-0.1, -0.04, 0.32, -1.2), (0.08, -0.07, 0.36, 0.2),
                                                             (0.01, 0.09, 0.3, 1.9))
    for x, y, h, turn in stems:
        base = Vector((x * 0.3, y * 0.3, 0.0))
        top = Vector((x, y, h))
        stem(p, curve(base, top, Vector((x, y, 0.0)) if stage > 1 else Vector((1, 0, 0)), 0.012, 3 if stage == 3 else 4), 0.006,
             0.0045, region=BASE)
        pairs = 3 if stage == 1 else 2
        for j in range(pairs):
            t = 0.3 + 0.5 * j / pairs
            at = base.lerp(top, t)
            for side in (-1.0, 1.0):
                a = turn + j * 1.4 + (0.0 if side > 0 else math.pi)
                leaf(p, at, _dir(a, 0.45), 0.062 - 0.008 * j, 0.034, droop=0.2, cup=0.25, n=6 if stage == 3 else 8, region=RR)
        if stage == 1:
            leaf(p, top, Vector((0.1, -0.2, 1.0)), 0.038, 0.024, droop=0.1, cup=0.3, region=RR)
            continue
        axis = (top - base).normalized()
        if stage == 2:
            # A cluster of round buds: a soft beaded cone.
            spike(p, top, axis, 0.1, 0.03, rings=7, sides=6, bumps=0.45, region=mix(RR, BB, 0.25), tip_region=mix(BB, AA, 0.3))
            continue
        # The raceme: little five-petal flowers in four tiers, narrowing upward, on a short top stem.
        stem(p, [top, top + axis * 0.1], 0.004, 0.0025, sides=4, rooted=False)
        for k in range(4):
            z = 0.014 + k * 0.026
            ring_r = 0.028 * (1.0 - 0.2 * k)
            count = 3 if k < 3 else 1
            for m in range(count):
                a = turn + m * math.tau / count + k * 0.9
                c = top + axis * z + Vector((math.cos(a) * ring_r, math.sin(a) * ring_r, 0.0)) * (1.0 if count > 1 else 0.0)
                f = (Vector((math.cos(a), math.sin(a), 0.4)) * 0.5 + TO_CAMERA * 0.5).normalized()
                flower(p, c, f, 0.024, 5, a, cup=0.3, heart=0.0, shoulders=((-0.45, 0.6), (0.0, 1.0)),
                       petal=lambda r: mix(AA, GG, smoothstep(0.2, 0.8, r)))
    return p, (0.2 if stage == 1 else 0.48)


def _heart_leaf(p, at, d, size, droop=0.25, region=RR):
    leaf(p, at, d, size, size * 0.85, droop=droop, cup=0.25, n=10, taper=0.45, region=region)


def build_sunflower(stage, rng):
    """Tournesol: grosse pousse / tige haute, bouton vert / grande fleur jaune tournée vers la caméra."""
    p = Parts()
    if stage == 1:
        top = Vector((0.0, 0.0, 0.12))
        stem(p, curve(Vector((0, 0, 0)), top, Vector((1, 0, 0)), 0.006), 0.014, 0.011, sides=6, region=BASE)
        for side in (-1.0, 1.0):
            leaf(p, top, Vector((side, -0.15, 0.35)), 0.09, 0.06, droop=0.25, cup=0.3, taper=0.0, region=RR)
        for side in (-1.0, 1.0):
            _heart_leaf(p, top + UP * 0.01, Vector((0.25 * side, side * 0.9, 0.9)), 0.055, region=RR)
        leaf(p, top + UP * 0.012, Vector((0.0, -0.3, 1.0)), 0.035, 0.026, droop=0.05, cup=0.4, taper=0.2, region=RR)
        leaf(p, top + UP * 0.012, Vector((0.0, 0.3, 1.0)), 0.03, 0.022, droop=0.05, cup=0.4, taper=0.2, region=RR)
        return p, 0.18
    h = 0.62 if stage == 2 else 0.8
    top = Vector((0.0, -0.02, h))
    stem(p, curve(Vector((0, 0, 0)), top, Vector((0, -1, 0)), 0.02, 5), 0.017, 0.013, sides=6, region=BASE)
    for j, (t, a, size) in enumerate(((0.22, -0.9, 0.15), (0.24, 2.2, 0.14), (0.48, 0.6, 0.13), (0.5, -2.6, 0.12),
                                       (0.72, -1.7, 0.1), (0.74, 1.4, 0.095))):
        at = Vector((0, 0, 0)).lerp(top, t)
        _heart_leaf(p, at, _dir(a, 0.25), size, droop=0.35, region=RR)
    if stage == 2:
        ball(p, top + UP * 0.03, Vector((0.055, 0.055, 0.045)), segments=9, rings=5, region=mix(BASE, RR, 0.5))
        calyx(p, top + UP * 0.055, UP, 0.06, lobes=9, region=RR)
        return p, 0.74
    f = TO_CAMERA
    neck = top + f * 0.02
    stem(p, [top, neck], 0.013, 0.013, sides=6, rooted=False, cap=False)
    calyx(p, neck + f * 0.005, f, 0.12, lobes=11, region=RR)
    flower(p, neck + f * 0.02, f, 0.19, 14, 0.1, cup=0.12, heart=0.46, dome=0.22, heart_segments=12,
           shoulders=((-0.5, 0.56), (-0.3, 0.9), (-0.12, 0.99), (0.12, 0.99), (0.3, 0.9)),
           petal=lambda r: mix(GG, AA, 0.5 * smoothstep(0.75, 1.0, r)),
           heart_region=lambda co, c=neck + f * 0.02: mix(BB, AA, 0.35 * smoothstep(0.05, 0.08, (co - c).length) *
                                                      (0.5 + 0.5 * math.cos(((co - c).length) * 240.0))))
    return p, 1.0


def build_dwarf_apple(stage, rng):
    """Pommier nain: jeune tige / petit arbre fleuri blanc et rose / petit arbre avec 4 pommes rouges."""
    p = Parts()
    if stage == 1:
        top = Vector((0.01, 0.0, 0.26))
        stem(p, curve(Vector((0, 0, 0)), top, Vector((1, 0, 0)), 0.01, 5), 0.011, 0.006, region=BASE)
        twig = Vector((-0.06, -0.01, 0.2))
        stem(p, [Vector((0, 0, 0.14)), twig], 0.005, 0.0035, rooted=False)
        for at, a in ((top, -1.2), (top, 0.4), (top, 2.0), (top + Vector((0, 0, -0.04)), -2.6), (twig, 2.8), (twig, -2.0)):
            leaf(p, at, _dir(a, 0.5), 0.055, 0.034, droop=0.2, cup=0.25, n=8, region=RR)
        return p, 0.3
    # The trunk with two forks into the crown; the crown round and full (radius 0.3, top at 0.88 m), leaf scales on it.
    center = Vector((0.0, 0.02, 0.62))
    radii = Vector((0.3, 0.28, 0.25))
    stem(p, curve(Vector((0, 0, 0)), Vector((0.01, 0.01, 0.42)), Vector((1, 0, 0)), 0.012, 4), 0.03, 0.022, sides=7,
         region=BASE, cap=False)
    for a in (-0.6, 2.2):
        stem(p, [Vector((0.01, 0.01, 0.38)), Vector((math.cos(a) * 0.1, math.sin(a) * 0.08 + 0.02, 0.55))], 0.016, 0.01,
             rooted=False, cap=False)
    crown(p, center, radii, 71, segments=10, rings=7, region=RR)
    _crown_leaves(p, random.Random(7171), center, radii, 20, 0.075, front=0.0, lift=1.0)
    if stage == 2:
        for k, (at, d) in enumerate(_bush_points(rng, center, radii * 1.05, 10, 0.11, 0.1)):
            f = (d * 0.6 + TO_CAMERA * 0.4).normalized()
            pink = k % 3 == 0
            flower(p, at + d * 0.01, f, 0.045, 5, rng.uniform(0, 1), cup=0.2, heart=0.32, dome=0.4, heart_segments=5,
                   shoulders=((-0.36, 0.7), (0.0, 1.0), (0.3, 0.9)),
                   petal=(lambda r: mix(AA, BB, 0.25)) if pink else (lambda r: mix(BB, AA, 0.5 * (1 - smoothstep(0.2, 0.6, r)))),
                   heart_region=mix(GG, AA, 0.3))
        return p, 0.95
    apples = ((-0.17, -0.2, 0.5), (0.12, -0.24, 0.55), (0.25, -0.1, 0.47), (-0.04, -0.27, 0.72))
    for x, y, z in apples:
        c = Vector((x, y, z))
        d = (c - center)
        d = Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).normalized()
        c = center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * 1.0 + d * 0.045
        ball(p, c, Vector((0.062, 0.062, 0.056)), segments=8, rings=5,
             region=lambda co, cc=c: GG if (co - cc).dot((TO_CAMERA + UP * 0.6).normalized()) < 0.03 else mix(GG, AA, 0.45),
             displace=lambda dd: 1.0 - 0.18 * max(0.0, dd.z) ** 6)
        stem(p, [c + UP * 0.048, c + UP * 0.075 + Vector((0.006, 0, 0))], 0.0045, 0.0035, sides=4, rooted=False, region=BASE)
        leaf(p, c + UP * 0.068, Vector((0.6, -0.4, 0.5)), 0.04, 0.024, droop=0.1, cup=0.3, n=6, region=RR)
    return p, 0.95


def build_round_melon(stage, rng):
    """Melon rond: feuilles rampantes / fleurs jaunes / deux melons ronds."""
    p = Parts()
    vines = [(-2.6, 0.3), (-0.5, 0.3), (1.4, 0.28)] if stage > 1 else [(-2.2, 0.2), (-0.4, 0.22)]
    for a, length in vines:
        pts = [Vector((0, 0, -0.012)), Vector((0, 0, 0.01))]
        for j in range(1, 4):
            t = j / 3
            pts.append(Vector((math.cos(a + 0.4 * t) * length * t, math.sin(a + 0.4 * t) * length * t, 0.012)))
        _nd_loft(p.bm, pts, [0.006] * len(pts), [0.006] * len(pts), 5, end=pts[-1] + _dir(a + 0.4) * 0.006)
        p.tag(BASE, 0.12, True)
        count = 2 if stage == 1 else 3
        for j in range(count):
            t = (j + 1) / (count + 0.3)
            at = Vector((math.cos(a + 0.4 * t) * length * t, math.sin(a + 0.4 * t) * length * t, 0.014))
            d = _dir(a + 0.4 * t + (1.4 if j % 2 else -1.4), 0.45)
            size = rng.uniform(0.085, 0.1) if stage > 1 else rng.uniform(0.075, 0.09)
            stem(p, [at, at + d * 0.02 + UP * 0.012], 0.0035, 0.003, sides=4, rooted=False)
            leaf(p, at + d * 0.02 + UP * 0.012, (d + UP * 0.2).normalized(), size, size * 0.95, droop=0.2, cup=0.18, n=10,
                 taper=0.15, notch=0.0, region=mix(RR, BASE, rng.uniform(0.0, 0.3)))
        if stage == 2:
            tip = Vector((math.cos(a + 0.4) * length * 0.75, math.sin(a + 0.4) * length * 0.75, 0.05))
            stem(p, [tip - UP * 0.035, tip], 0.003, 0.0025, sides=4, rooted=False)
            flower(p, tip, _face(rng, 0.25), 0.03, 5, rng.uniform(0, 1), cup=0.3, heart=0.3, dome=0.4,
                   shoulders=((-0.42, 0.62), (-0.22, 0.95), (0.0, 1.0), (0.22, 0.95)), petal_region=GG,
                   heart_region=mix(GG, AA, 0.5))
    if stage == 1:
        leaf(p, Vector((0, 0, 0.01)), Vector((0.1, -0.3, 1.0)), 0.05, 0.045, droop=0.2, cup=0.3, region=RR)
        return p, 0.12
    if stage == 2:
        stem(p, [Vector((0.02, -0.02, 0.01)), Vector((0.03, -0.06, 0.07))], 0.003, 0.0025, sides=4, rooted=False)
        flower(p, Vector((0.03, -0.06, 0.07)), _face(rng, 0.1), 0.032, 5, 0.4, cup=0.3, heart=0.3, dome=0.4,
               shoulders=((-0.42, 0.62), (-0.22, 0.95), (0.0, 1.0), (0.22, 0.95)), petal_region=GG,
               heart_region=mix(GG, AA, 0.5))
        return p, 0.16
    for c, r in ((Vector((-0.12, -0.12, 0.0)), 0.105), (Vector((0.13, -0.08, 0.0)), 0.092)):
        c = c + UP * (r * 0.92 + 0.004)
        ball(p, c, Vector((r, r, r * 0.92)), segments=12, rings=7,
             region=lambda co, cc=c: mix(BB, AA, smoothstep(0.4, 0.9, abs(math.cos(3.0 * math.atan2(co.y - cc.y, co.x - cc.x))))),
             occ=lambda co, cc=c, rr=r: 0.03 + 0.25 * (1.0 - smoothstep(cc.z - rr, cc.z - rr * 0.4, co.z)))
        stem(p, [c + UP * r * 0.9, c + UP * (r * 0.9 + 0.025) + Vector((0.01, 0, 0))], 0.005, 0.004, sides=4, rooted=False)
    return p, 0.26


def _stake(p):
    """A little tripod of three bamboo canes tied at the top (the sweet pea climbs it); the canes stand in the soil."""
    top = Vector((0.0, 0.01, 0.5))
    feet = [Vector((math.cos(a) * 0.12, math.sin(a) * 0.12 + 0.01, 0.0)) for a in (-math.pi / 2, math.pi / 6, 5 * math.pi / 6)]
    for f in feet:
        tip = top + (top - f).normalized() * 0.03
        stem(p, [f, f.lerp(top, 0.5), tip], 0.0095, 0.007, sides=5, region=AA, occ=0.06)
    # The twine: a small round knot at the crossing.
    ball(p, top + UP * 0.004, Vector((0.017, 0.017, 0.014)), segments=6, rings=4, region=AA, occ=0.12)
    return top, feet


def build_sweet_pea(stage, rng):
    """Pois de senteur (on a little stake): vrilles fines / tuteur couvert de feuilles / fleurs roses et mauves."""
    p = Parts()
    top, feet = _stake(p)
    if stage == 1:
        for f, a in zip(feet[:2], (0.6, 2.4)):
            base = f + Vector((0.025 * math.cos(a), 0.025 * math.sin(a), 0.0))
            tip = base + Vector((0.0, 0.0, 0.11))
            stem(p, curve(base, tip, f - base, 0.01), 0.0045, 0.0035, sides=4)
            for j, t in enumerate((0.5, 0.85)):
                at = base.lerp(tip, t)
                for side in (-1.0, 1.0):
                    leaf(p, at, _dir(a + side * 1.3, 0.5), 0.042, 0.028, droop=0.15, cup=0.25, n=6, region=RR)
            # A fine curl of tendril reaching for the cane.
            pts = [tip + Vector((math.cos(t) * 0.014, math.sin(t) * 0.014, 0.009 * t)) for t in (0.0, 1.5, 3.0, 4.5)]
            stem(p, pts, 0.0026, 0.002, sides=4, rooted=False)
        return p, 0.52
    # The vine climbs the canes in a wide spiral; generous leaves in pairs along it (the stake covered with leaves).
    pts = []
    for j in range(12):
        t = j / 11
        a = -1.3 + t * math.tau * 1.3
        r = 0.095 * (1.0 - t) + 0.045
        pts.append(Vector((math.cos(a) * r, math.sin(a) * r + 0.01, 0.015 + t * 0.44)))
    pts[0] = Vector((pts[0].x, pts[0].y, 0.0))
    stem(p, pts, 0.005, 0.004, sides=4)
    for j in range(1, 12):
        at = pts[j]
        a = math.atan2(at.y - 0.01, at.x)
        out = Vector((math.cos(a), math.sin(a), 0.0))
        for side in (-1.0, 1.0):
            d = (out + Vector((-math.sin(a), math.cos(a), 0.0)) * side * 0.8 + UP * 0.4).normalized()
            leaf(p, at + out * 0.004, d, 0.066 - 0.018 * j / 11, 0.04, droop=0.15, cup=0.25, n=6,
                 region=mix(RR, BASE, 0.25 * (j % 2)))
    if stage == 3:
        spots = [pts[j] for j in (3, 5, 6, 8, 9, 10, 11)]
        for k, at in enumerate(spots):
            a = math.atan2(at.y - 0.01, at.x)
            out = Vector((math.cos(a), math.sin(a), 0.0))
            c = at + out * 0.045 + UP * 0.014
            f = (out * 0.35 + TO_CAMERA * 0.65).normalized()
            colour = GG if k % 2 == 0 else BB
            # The standard: a big ruffled round petal; the wings: a small cupped pair in front of it.
            flower(p, c, f, 0.046, 3, a, cup=0.15, heart=0.0, shoulders=((-0.4, 0.8), (-0.2, 0.97), (0.0, 1.0), (0.2, 0.97)),
                   petal_region=colour)
            ball(p, c + f * 0.011 - UP * 0.011, Vector((0.018, 0.018, 0.014)), axis=f, segments=5, rings=3,
                 region=mix(colour, BB if colour == GG else GG, 0.25))
    return p, 0.56


GD_BUILDERS = {
    "SweetClover": build_sweet_clover, "WildStrawberry": build_wild_strawberry, "Daisy": build_daisy,
    "RoundCarrot": build_round_carrot, "Poppy": build_poppy, "Blueberry": build_blueberry, "Lavender": build_lavender,
    "GoldenWheat": build_golden_wheat, "MeadowIndigo": build_meadow_indigo, "Sunflower": build_sunflower,
    "DwarfApple": build_dwarf_apple, "RoundMelon": build_round_melon, "SweetPea": build_sweet_pea,
}


# Each plant's size on the phone: every stage scaled alike around its root (the authored sizes are a first sketch;
# these make the ripe plant fill its bed and read from the set camera).
PLANT_SCALE = {"SweetClover": 1.3, "WildStrawberry": 1.25, "Daisy": 1.5, "RoundCarrot": 1.4, "Poppy": 1.42,
               "Blueberry": 1.45, "Lavender": 1.38, "GoldenWheat": 1.08, "MeadowIndigo": 1.25, "Sunflower": 1.0,
               "DwarfApple": 1.0, "RoundMelon": 1.06, "SweetPea": 1.12}


def build_plant(col, stem_name, stage):
    """One stage of one plant: its builder with a fixed seed (the same model at every build), scaled for the phone."""
    rng = random.Random(sum(map(ord, stem_name)) * 31 + stage)
    parts, height = GD_BUILDERS[stem_name](stage, rng)
    return finish(plant_object_name(stem_name, stage), parts, col, max(0.05, height), ground_occ=0.3,
                  scale=PLANT_SCALE[stem_name])


def measure_plant(obj):
    """Footprint radius (m, every vertex above -2 cm), height, lowest point of the parts that are not rooted."""
    me = obj.data
    radius = max(math.hypot(v.co.x, v.co.y) for v in me.vertices if v.co.z > -0.02)
    height = max(v.co.z for v in me.vertices)
    return radius, height


# ---------------------------------------------------------------- the set props

def _rounded_rect_ring(cx, cy, hx, hy, corner, n_corner=2):
    """Points of a rounded rectangle (counterclockwise) with `n_corner` + 1 points per corner arc."""
    pts = []
    for qx, qy, a0 in ((1, 1, 0.0), (-1, 1, 90.0), (-1, -1, 180.0), (1, -1, 270.0)):
        ccx, ccy = cx + qx * (hx - corner), cy + qy * (hy - corner)
        for j in range(n_corner + 1):
            a = math.radians(a0 + 90.0 * j / n_corner)
            pts.append(Vector((ccx + math.cos(a) * corner, ccy + math.sin(a) * corner, 0.0)))
    return pts


def _plank(p, a, b, z0, z1, thick, bow, region_fn, sides=8, segments=2):
    """A rounded plank from a to b (its axis on the floor), its cross-section a soft rounded rectangle thick x (z1 - z0),
    its top bowed up by `bow` at the middle; closed ends."""
    axis = (b - a)
    length = axis.length
    d = axis.normalized()
    out = Vector((d.y, -d.x, 0.0))     # outward normal for a counterclockwise bed outline
    h = (z1 - z0)
    section = []
    for k in range(sides):
        t = math.tau * (k + 0.5) / sides
        c, s = math.cos(t), math.sin(t)
        # superellipse section (power 5): flat faces, soft edges
        f = (abs(c) ** 5 + abs(s) ** 5) ** (-1.0 / 5)
        section.append((c * f * thick * 0.5, s * f * h * 0.5))
    centers = []
    for j in range(segments + 1):
        t = j / segments
        centers.append(a + axis * t + UP * ((z0 + z1) * 0.5 + bow * math.sin(math.pi * t)))
    rings = []
    for c in centers:
        rings.append([p.bm.verts.new(c + out * sx + UP * sz) for sx, sz in section])
    for ra, rb in zip(rings, rings[1:]):
        for k in range(sides):
            m = (k + 1) % sides
            p.bm.faces.new((ra[k], ra[m], rb[m], rb[k]))
    ea = p.bm.verts.new(centers[0])
    eb = p.bm.verts.new(centers[-1])
    for k in range(sides):
        m = (k + 1) % sides
        p.bm.faces.new((rings[0][m], rings[0][k], ea))
        p.bm.faces.new((rings[-1][k], rings[-1][m], eb))
    p.tag(region_fn, lambda co: 0.06 + 0.3 * (1.0 - smoothstep(0.0, 0.06, co.z)))


def _soil_height(x, y):
    r = math.hypot(x / SOIL_HALF[0], y / SOIL_HALF[1])
    mound = 0.008 * max(0.0, 1.0 - (math.hypot(x, y) / 0.42) ** 2)
    crumbs = 0.0025 * (math.sin(x * 61.0 + 1.3) * math.sin(y * 53.0 - 0.4)) * smoothstep(0.2, 0.6, r)
    return SOIL_TOP - 0.008 + mound + crumbs


def build_bed(col):
    """GD_Bed: a raised wooden bed 1.04 x 0.88 m, 0.26 m to the plank tops; two rows of plump rounded planks (the long
    ones run the full length, the short ones between), four inner corner posts with round caps a little higher, and
    the dark crumbly soil filling it to 0.24 m with a soft mound in the middle (the plants' root at its top) and four
    soft clods in its corners. Pivot at the footprint's center on the ground. Regions: base planks, R sunny tops and
    edges, G soil, B crumbs and clods, A posts."""
    p = Parts()
    hx, hy = BED_HALF
    t = BED_PLANK
    rows = [(0.0, 0.124), (0.130, PLANK_TOP)]

    def wood(co):
        return mix(BASE, RR, 0.85 * smoothstep(PLANK_TOP - 0.03, PLANK_TOP, co.z) + 0.25 * smoothstep(0.1, 0.124, co.z) *
                   (1.0 - smoothstep(0.124, 0.13, co.z)))

    for r, (z0, z1) in enumerate(rows):
        lift = 0.004 if r == 1 else 0.0
        # Long planks (front and back) run the full length; short planks fit between them.
        _plank(p, Vector((-hx, -hy + t * 0.5, 0.0)), Vector((hx, -hy + t * 0.5, 0.0)), z0, z1 + lift * 0.5, t, 0.003 * r, wood)
        _plank(p, Vector((hx, hy - t * 0.5, 0.0)), Vector((-hx, hy - t * 0.5, 0.0)), z0, z1 + lift, t, 0.002 * r, wood)
        _plank(p, Vector((hx - t * 0.5, -hy + t, 0.0)), Vector((hx - t * 0.5, hy - t, 0.0)), z0, z1, t, 0.0, wood, segments=1)
        _plank(p, Vector((-hx + t * 0.5, hy - t, 0.0)), Vector((-hx + t * 0.5, -hy + t, 0.0)), z0, z1, t, 0.0, wood, segments=1)
    # Corner posts inside the corners, their round caps 14 mm over the planks.
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            c = Vector((sx * (hx - t - 0.03), sy * (hy - t - 0.03), 0.0))
            prof = [(0.03, 0.02), (0.03, POST_TOP - 0.02), (0.026, POST_TOP - 0.006), (0.014, POST_TOP)]
            rings = _nd_revolve(p.bm, prof, 8, top=None, bottom=None, phase=math.pi / 8)
            apex = p.bm.verts.new((0.0, 0.0, POST_TOP + 0.002))
            for k in range(8):
                p.bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 8], apex))
            for v in [v for ring in rings for v in ring] + [apex]:
                v.co.x += c.x
                v.co.y += c.y
            p.tag(lambda co: mix(AA, RR, 0.6 * smoothstep(POST_TOP - 0.012, POST_TOP, co.z)), 0.08)
    # The soil: a grid inside the planks (its rim tucked 6 mm into the wood).
    nx, ny = 9, 7
    sx0, sy0 = hx - t + 0.006, hy - t + 0.006
    grid = []
    for i in range(nx + 1):
        row = []
        for j in range(ny + 1):
            x = -sx0 + 2 * sx0 * i / nx
            y = -sy0 + 2 * sy0 * j / ny
            row.append(p.bm.verts.new((x, y, _soil_height(x, y))))
        grid.append(row)
    for i in range(nx):
        for j in range(ny):
            p.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    p.tag(lambda co: mix(GG, BB, 0.35 * smoothstep(0.3, 0.9, wave_noise(Vector((co.x * 9.0, co.y * 9.0, 0.0)), 4301) * 0.5 + 0.5)),
          lambda co: 0.05 + 0.25 * smoothstep(0.36, 0.46, max(abs(co.x) / SOIL_HALF[0] * 0.46, abs(co.y) / SOIL_HALF[1] * 0.46)))
    # Four soft clods, all outside the plants' circle (radius 0.40 from the center).
    rng = random.Random(4303)
    clods = 0
    while clods < 4:
        x = rng.uniform(-SOIL_HALF[0] + 0.03, SOIL_HALF[0] - 0.03)
        y = rng.uniform(-SOIL_HALF[1] + 0.03, SOIL_HALF[1] - 0.03)
        if math.hypot(x, y) < 0.42:
            continue
        r = rng.uniform(0.014, 0.022)
        ball(p, Vector((x, y, _soil_height(x, y) + r * 0.15)), Vector((r * 1.3, r * 1.1, r * 0.45)), segments=6, rings=4,
             region=mix(GG, BB, 0.45), occ=0.12)
        clods += 1
    return finish("GD_Bed", p, col, PLANK_TOP, ground_occ=0.3)


def _bed_outline(th, push, corner=0.02):
    """Distance from the bed's center to its outline grown by `push` (a rounded rectangle, corner radius corner + push)
    along the direction th."""
    a, b, r = BED_HALF[0] + push, BED_HALF[1] + push, corner + push
    c, s = abs(math.cos(th)), abs(math.sin(th))
    tx = a / c if c > 1e-9 else 1e9
    if tx * s <= b - r:
        return tx
    ty = b / s if s > 1e-9 else 1e9
    if ty * c <= a - r:
        return ty
    cx, cy = a - r, b - r
    dc = c * cx + s * cy
    return dc + math.sqrt(max(0.0, dc * dc - (cx * cx + cy * cy) + r * r))


def _cover_top(x, y):
    """The cloth's top over the bed: puffed (a little over the soil's mound), lifted over the posts' caps."""
    hx, hy = BED_HALF[0] + 0.012, BED_HALF[1] + 0.012
    u, v = min(1.0, abs(x) / hx), min(1.0, abs(y) / hy)
    return PLANK_TOP + 0.014 + 0.034 * (1.0 - u * u) * (1.0 - v * v)


def build_bed_cover(col):
    """GD_BedCover: the soft linen cloth over a locked bed: a puffed top (lifted over the corner posts), a skirt hanging
    10 mm outside the planks down to about 0.13 m with a slow wavy hem and a sage band, and a small wooden A-frame
    sign standing on the cloth near the front (the overlay writes « Nouvelle parcelle » over it). Pivot as the bed's.
    Regions: base linen, R the sage band, G sign wood, B the sign's face, A its border."""
    p = Parts()
    hx, hy = BED_HALF[0] + 0.012, BED_HALF[1] + 0.012
    # Top: a grid in (u, v), pulled into the bed's outline grown by 12 mm (a rounded rectangle): its rim is outside the
    # planks everywhere, so the skirt falls clear of the wood.
    us = [-1.0, -0.94, -0.62, -0.3, 0.0, 0.3, 0.62, 0.94, 1.0]
    vs = [-1.0, -0.92, -0.5, 0.0, 0.5, 0.92, 1.0]
    nu, nv = len(us) - 1, len(vs) - 1
    grid = []
    for i in range(nu + 1):
        row = []
        for j in range(nv + 1):
            x0, y0 = us[i] * hx, vs[j] * hy
            f = 1.0
            if abs(x0) > 1e-9 or abs(y0) > 1e-9:
                th = math.atan2(y0, x0)
                f = _bed_outline(th, 0.012) / min(hx / max(1e-9, abs(math.cos(th))), hy / max(1e-9, abs(math.sin(th))))
            x, y = x0 * f, y0 * f
            row.append(p.bm.verts.new((x, y, _cover_top(x, y))))
        grid.append(row)
    for i in range(nu):
        for j in range(nv):
            p.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    p.tag(BASE, lambda co: 0.03 + 0.12 * (1.0 - smoothstep(PLANK_TOP + 0.01, PLANK_TOP + 0.03, co.z)))
    # The skirt from the top's rim down: each ring on the bed's outline grown by `push`, a sage band, a wavy hem.
    ring_idx = [(i, 0) for i in range(nu)] + [(nu, j) for j in range(nv)] + [(i, nv) for i in range(nu, 0, -1)] +                [(0, j) for j in range(nv, 0, -1)]
    edge = [grid[i][j] for i, j in ring_idx]
    levels = [(0.03, 0.013, BASE), (0.064, 0.014, BASE), (0.072, 0.0142, RR), (0.088, 0.0145, RR), (0.096, 0.0148, BASE),
              (0.13, 0.016, BASE)]
    rings = [edge]
    n = len(edge)
    for depth, push, region in levels:
        ring = []
        for v0 in edge:
            th = math.atan2(v0.co.y, v0.co.x)
            d = Vector((math.cos(th), math.sin(th), 0.0))
            wave = 0.005 * math.sin(th * 9.0 + 0.6) * smoothstep(0.06, 0.13, depth)
            z = PLANK_TOP + 0.008 - depth - 0.01 * math.sin(th * 5.0 + 1.1) * smoothstep(0.1, 0.13, depth)
            q = d * (_bed_outline(th, push) + max(0.0, wave))
            ring.append(p.bm.verts.new((q.x, q.y, z)))
        rings.append(ring)
        p.tag(region, lambda co: 0.06 + 0.22 * (1.0 - smoothstep(0.13, 0.24, co.z)))
    for a, b in zip(rings, rings[1:]):
        for k in range(n):
            m = (k + 1) % n
            p.bm.faces.new((a[k], b[k], b[m], a[m]))
    # The A-frame sign near the front, standing on the cloth (its feet 2 mm over it).
    sx, sy = 0.16, -0.17
    board_c = Vector((sx, sy, 0.0))
    tilt = math.radians(18.0)
    fwd = Vector((0.0, -math.cos(tilt), math.sin(tilt)))       # the board's face toward the camera, leaning back
    upb = Vector((0.0, math.sin(tilt), math.cos(tilt)))
    foot_z = max(_cover_top(sx - 0.07, sy), _cover_top(sx + 0.07, sy), _cover_top(sx, sy + 0.05)) + 0.002
    bw, bh = 0.26, 0.15
    center = board_c + UP * (foot_z + 0.045) + upb * (bh * 0.5)
    outline = []
    for x, y in [(q.x, q.y) for q in _rounded_rect_ring(0.0, 0.0, bw * 0.5, bh * 0.5, 0.03, 2)]:
        outline.append(center + Vector((x, 0.0, 0.0)) + upb * y)
    _nd_lens(p.bm, center, outline, 0.009, 0.009, fwd)
    face_start = len(p.regions)
    p.tag(GG, 0.04)
    inner = [center + fwd * 0.0095 + Vector((q.x, 0, 0)) + upb * q.y for q in _rounded_rect_ring(0.0, 0.0, bw * 0.4, bh * 0.36, 0.022, 2)]
    _nd_lens(p.bm, center + fwd * 0.0095, inner, 0.002, 0.001, fwd)
    p.tag(BB, 0.02)
    # Two legs in front and one strut behind, all on the cloth.
    for lx in (-0.07, 0.07):
        top = center + Vector((lx, 0.0, 0.0)) - upb * (bh * 0.3)
        foot = Vector((sx + lx, sy - 0.01, foot_z))
        stem(p, [foot, top], 0.007, 0.006, sides=5, region=AA, rooted=False, cap=False)
    stem(p, [Vector((sx, sy + 0.06, _cover_top(sx, sy + 0.06) + 0.002)), center - fwd * 0.008 + upb * 0.02], 0.006, 0.005,
         sides=5, region=AA, rooted=False, cap=False)
    obj = finish("GD_BedCover", p, col, PLANK_TOP + 0.2, ground_occ=0.0)
    return obj


# Stepping stones: (x, y, radius) along the path from the front (under the overlay's sheet) to the fence; between the
# beds (|x| <= BED_X - BED_HALF[0] - 0.04 - radius) they step gently left and right.
def path_stones():
    rng = random.Random(4311)
    stones = []
    y = -1.25
    k = 0
    while y < FENCE_Y - 0.45:
        r = rng.uniform(0.12, 0.14)
        x = 0.05 * math.sin(k * 1.9 + 0.4) + rng.uniform(-0.01, 0.01)
        limit = BED_X - BED_HALF[0] - 0.04 - r
        x = max(-limit, min(limit, x))
        stones.append((x, y, r))
        y += 2.0 * r + rng.uniform(0.07, 0.1)
        k += 1
    return stones


def build_path(col):
    """GD_Path: round cream stepping stones from the front to the fence, each a soft dome 2 cm over the meadow, its edge
    sunk 1 cm in it (laid in the ground), a little moss at its foot. Pivot at the set's origin. Regions: base stone,
    R sunny top, G moss, B speckle."""
    p = Parts()
    seg = 9
    for idx, (x, y, r) in enumerate(path_stones()):
        rng = random.Random(4400 + idx)
        squash = rng.uniform(0.86, 1.0)
        turn = rng.uniform(0, math.tau)
        prof = [(r * 0.62, 0.020), (r * 0.96, 0.011), (r * 1.01, -0.010)]
        rings = []
        for rr, z in prof:
            ring = []
            for k in range(seg):
                a = turn + math.tau * k / seg
                wob = 1.0 + 0.05 * math.sin(a * 3.0 + idx)
                ring.append(p.bm.verts.new((x + math.cos(a) * rr * wob, y + math.sin(a) * rr * wob * squash, z)))
            rings.append(ring)
        apex = p.bm.verts.new((x, y, 0.022))
        for k in range(seg):
            p.bm.faces.new((apex, rings[0][k], rings[0][(k + 1) % seg]))
        for a, b in zip(rings, rings[1:]):
            for k in range(seg):
                m = (k + 1) % seg
                p.bm.faces.new((a[k], b[k], b[m], a[m]))
        p.tag(lambda co, i=idx: mix(mix(RR, BB, 0.4 if (i % 3 == 0) else 0.0), GG, 1.0 - smoothstep(-0.006, 0.006, co.z)) if co.z > 0.012
              else mix(BASE, GG, 0.7 * (1.0 - smoothstep(-0.006, 0.008, co.z))),
              lambda co: 0.04 + 0.3 * (1.0 - smoothstep(-0.01, 0.01, co.z)))
    return finish("GD_Path", p, col, 0.03, ground_occ=0.0, recalc=False)


def fence_point(x):
    t = x / FENCE_HALF
    return Vector((x, FENCE_Y - FENCE_BOW * t * t, 0.0))


def build_fence(col):
    """GD_Fence: a low wooden fence across the back of the garden, its ends curving toward the camera: 11 round posts
    0.42 m high with domed tops, two plump rails 0.15 and 0.31 m up passing through them (one object), a touch of moss
    at the posts' feet. Pivot at the set's origin. Regions: base wood, R post tops, G rails, B moss."""
    p = Parts()
    posts = 11
    xs = [-FENCE_HALF + 2 * FENCE_HALF * i / (posts - 1) for i in range(posts)]
    for i, x in enumerate(xs):
        c = fence_point(x)
        h = 0.42 + 0.012 * math.sin(i * 2.3)
        prof = [(0.036, -0.02), (0.036, h - 0.03), (0.03, h - 0.008), (0.016, h + 0.004)]
        rings = _nd_revolve(p.bm, prof, 8, phase=0.3 * i)
        apex = p.bm.verts.new((0.0, 0.0, h + 0.008))
        for k in range(8):
            p.bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 8], apex))
        for v in [v for ring in rings for v in ring] + [apex]:
            v.co.x += c.x
            v.co.y += c.y
        p.tag(lambda co, hh=h: mix(mix(BASE, BB, 0.8 * (1.0 - smoothstep(0.0, 0.05, co.z))), RR, smoothstep(hh - 0.03, hh, co.z)),
              lambda co: 0.06 + 0.3 * (1.0 - smoothstep(-0.02, 0.06, co.z)))
    for z in (0.15, 0.31):
        pts = [fence_point(-FENCE_HALF - 0.04 + (2 * FENCE_HALF + 0.08) * j / 14) + UP * (z + 0.006 * math.sin(j * 1.7)) for j in range(15)]
        for q in pts:
            q.y -= 0.004
        _nd_loft(p.bm, pts, [0.022] * 15, [0.028] * 15, 6, start=pts[0] - Vector((0.02, 0, 0)), end=pts[-1] + Vector((0.02, 0, 0)))
        p.tag(lambda co: mix(GG, RR, 0.5 * smoothstep(0.0, 0.02, co.z % 0.16 - 0.15)), 0.06)
    return finish("GD_Fence", p, col, 0.45, ground_occ=0.0)


def build_watering_can(col):
    """GD_WateringCan: a round teal watering can 0.24 m high: a plump body with a pale band, a rim and a recessed top,
    a long spout rising toward -X with its round brass rose, an arched top handle. Pivot at the foot's center on the
    ground. Regions: base paint, R bands, G brass, B handle, A the dark opening."""
    p = Parts()
    prof = [(0.0, 0.0), (0.072, 0.0), (0.088, 0.012), (0.094, 0.05), (0.094, 0.10), (0.088, 0.135), (0.072, 0.158),
            (0.05, 0.168), (0.046, 0.172), (0.04, 0.166)]
    rings = _nd_revolve(p.bm, prof[1:], 12, bottom=None)
    under = p.bm.verts.new((0.0, 0.0, 0.004))
    for k in range(12):
        p.bm.faces.new((under, rings[0][(k + 1) % 12], rings[0][k]))
    inner = p.bm.verts.new((0.0, 0.0, 0.15))
    for k in range(12):
        p.bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 12], inner))
    p.tag(lambda co: AA if co.z > 0.15 and math.hypot(co.x, co.y) < 0.045 else
          (RR if 0.05 < co.z < 0.07 else (GG if co.z > 0.164 else BASE)),
          lambda co: 0.04 + 0.25 * (1.0 - smoothstep(0.0, 0.02, co.z)) + (0.4 if co.z > 0.15 and math.hypot(co.x, co.y) < 0.045 else 0.0))
    # The spout from the lower front of the body up and out toward -X; its rose a round cap.
    pts = [Vector((-0.07, 0.0, 0.04)), Vector((-0.12, 0.0, 0.085)), Vector((-0.17, 0.0, 0.14)), Vector((-0.205, 0.0, 0.18))]
    _nd_loft(p.bm, pts, [0.016, 0.012, 0.010, 0.010], [0.016, 0.012, 0.010, 0.010], 6)
    p.tag(BASE, 0.06)
    d = (pts[-1] - pts[-2]).normalized()
    ball(p, pts[-1] + d * 0.012, Vector((0.026, 0.026, 0.013)), axis=d, segments=8, rings=4, region=GG, occ=0.04)
    # The top handle: an arch from the back of the body over the opening.
    arch = [Vector((0.07 - 0.14 * j / 6, 0.0, 0.15 + 0.08 * math.sin(math.pi * j / 6) + (0.01 if j < 3 else 0.0))) for j in range(7)]
    arch[0] = Vector((0.075, 0.0, 0.125))
    arch[-1] = Vector((-0.055, 0.0, 0.155))
    _nd_loft(p.bm, arch, [0.009] * 7, [0.012] * 7, 6)
    p.tag(BB, 0.05)
    return finish("GD_WateringCan", p, col, 0.24, ground_occ=0.0)


GBASKET_HALF = (0.21, 0.15)
GBASKET_SEG = 28
GBASKET_BUMPS = 10
GBASKET_WALL = [(0.92, 0.118), (1.0, 0.13), (1.05, 0.122), (1.0, 0.11), (0.985, 0.092), (0.955, 0.07), (0.92, 0.048),
                (0.885, 0.026), (0.85, 0.008), (0.80, 0.0)]
GBASKET_ROWS = {4: 0, 5: 1, 6: 2, 7: 3}
GBASKET_INSIDE = [(0.86, 0.10), (0.80, 0.05), (0.74, 0.022)]


def build_harvest_basket(col):
    """GD_Basket: a wicker harvest trug 0.42 x 0.30 m, 0.13 m to the rim, open (the harvests fly in), four woven rows of
    ten soft over-bumps alternating row by row (the T40 weave), one dyed row, a rolled rim, the woven inside and its
    floor, an arched handle 0.3 m high over the long axis. Pivot at the bottom center on the ground. Regions: base
    wicker, R over-bumps, G rim and handle, B the inside, A the dyed row."""
    p = Parts()

    def plan(th, scale):
        r = _superellipse(th, 2.6)
        return Vector((math.cos(th) * r * GBASKET_HALF[0] * scale, math.sin(th) * r * GBASKET_HALF[1] * scale, 0.0))

    tags = []
    rings = []
    for i, (scale, z) in enumerate(GBASKET_WALL):
        row = GBASKET_ROWS.get(i)
        ring = []
        for k in range(GBASKET_SEG):
            th = math.tau * k / GBASKET_SEG
            weave = 1.0 if row is None else 1.0 + 0.03 * math.cos(GBASKET_BUMPS * th + row * math.pi)
            q = plan(th, scale * weave)
            ring.append(p.bm.verts.new((q.x, q.y, z)))
        rings.append(ring)
        p.tag(lambda co, i=i: _gbasket_region(co, i), lambda co, i=i: 0.05 + (0.35 if i in (3, 8, 9) else 0.0))
    for a, b in zip(rings, rings[1:]):
        for k in range(GBASKET_SEG):
            m = (k + 1) % GBASKET_SEG
            p.bm.faces.new((a[k], b[k], b[m], a[m]))
    under = p.bm.verts.new((0.0, 0.0, 0.004))
    p.tag(BASE, 0.6)
    for k in range(GBASKET_SEG):
        p.bm.faces.new((under, rings[-1][(k + 1) % GBASKET_SEG], rings[-1][k]))
    inside = [rings[0]]
    for scale, z in GBASKET_INSIDE:
        ring = []
        for k in range(GBASKET_SEG):
            q = plan(math.tau * k / GBASKET_SEG, scale)
            ring.append(p.bm.verts.new((q.x, q.y, z)))
        inside.append(ring)
        p.tag(BB, lambda co: 0.2 + 0.35 * (1.0 - smoothstep(0.02, 0.11, co.z)))
    for a, b in zip(inside, inside[1:]):
        for k in range(GBASKET_SEG):
            m = (k + 1) % GBASKET_SEG
            p.bm.faces.new((a[k], a[m], b[m], b[k]))
    floor = p.bm.verts.new((0.0, 0.0, 0.02))
    p.tag(BB, 0.5)
    for k in range(GBASKET_SEG):
        p.bm.faces.new((inside[-1][k], inside[-1][(k + 1) % GBASKET_SEG], floor))
    pts = [Vector((-0.178 * math.cos(math.pi * j / 8), 0.0, 0.1 + 0.2 * math.sin(math.pi * j / 8))) for j in range(9)]
    _nd_loft(p.bm, pts, [0.011] * 9, [0.011] * 9, 7)
    p.tag(lambda co: mix(GG, RR, 0.35 * (0.5 + 0.5 * math.cos(co.x * 90.0))), lambda co: 0.06)
    return finish("GD_Basket", p, col, 0.3, ground_occ=0.0, recalc=False)


def _gbasket_region(co, i):
    th = math.atan2(co.y / GBASKET_HALF[1], co.x / GBASKET_HALF[0])
    if i in (0, 1, 2, 3):
        twist = 0.5 + 0.5 * math.cos(GBASKET_BUMPS * 2 * th + i * 1.4)
        return (0.0, 1.0 - 0.5 * twist, 0.0, 0.0)
    row = GBASKET_ROWS.get(i)
    if row is not None:
        over = max(0.0, math.cos(GBASKET_BUMPS * th + row * math.pi))
        if row == 1:
            return (0.0, 0.0, 0.0, 0.6 + 0.4 * smoothstep(0.0, 0.7, over))
        return (smoothstep(0.0, 0.7, over), 0.0, 0.0, 0.0)
    return (0.0, 0.0, 0.0, 0.0)


GROUND_RINGS = [0.0, 0.7, 1.5, 2.4, 3.4, 4.5, 5.8, 7.2, 8.6]
GROUND_SEG = 40
GROUND_CENTER = Vector((0.0, 1.6, 0.0))


def build_ground(col):
    """GD_Ground: the garden's meadow floor, a flat disc 17 m across on z = 0 centred under the beds (the T38
    backdrop's floor has a hole in its middle; the set lays this floor over it and keeps its hills), soft sunny and
    deeper patches, a trodden strip of earth along the path and around the beds' feet. Pivot at the set's origin.
    Regions: base meadow, R sunny patches, G deeper patches, B trodden earth."""
    p = Parts()
    rings = []
    for r in GROUND_RINGS[1:]:
        rings.append([p.bm.verts.new(GROUND_CENTER + Vector((math.cos(t) * r, math.sin(t) * r, 0.0)))
                      for t in (math.tau * k / GROUND_SEG + 0.37 * r for k in range(GROUND_SEG))])
    center = p.bm.verts.new(GROUND_CENTER)
    for k in range(GROUND_SEG):
        p.bm.faces.new((center, rings[0][k], rings[0][(k + 1) % GROUND_SEG]))
    for a, b in zip(rings, rings[1:]):
        for k in range(GROUND_SEG):
            m = (k + 1) % GROUND_SEG
            p.bm.faces.new((a[k], b[k], b[m], a[m]))

    def region(co):
        q = Vector((co.x * 0.42, co.y * 0.42, 1.0))
        sunny = smoothstep(0.15, 0.75, wave_noise(q, 5401))
        deep = smoothstep(0.25, 0.85, wave_noise(q * 1.3, 5402)) * (1.0 - sunny)
        trodden = (1.0 - smoothstep(0.25, 0.9, abs(co.x))) * smoothstep(-1.6, -0.6, co.y) * (1.0 - smoothstep(3.6, 4.2, co.y))
        return (0.55 * sunny * (1 - trodden), 0.45 * deep * (1 - trodden), 0.55 * trodden, 0.0)

    p.tag(region, lambda co: 0.1 * smoothstep(6.0, 8.6, (co - GROUND_CENTER).length))
    return finish("GD_Ground", p, col, 1.0, ground_occ=0.0, recalc=False)


# ---------------------------------------------------------------- checks

def _bvh(obj, matrix=None):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if matrix is not None:
        bm.transform(matrix)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def check_cover(bed, cover):
    """The cover never enters the bed: no triangle pair crosses, and every cover vertex keeps 2 mm from the bed."""
    tb, tc = _bvh(bed), _bvh(cover)
    crossing = len(tb.overlap(tc))
    nearest = min(tb.find_nearest(v.co)[3] for v in cover.data.vertices)
    return crossing, nearest


def check_plants_in_bed(bed, plants):
    """Each plant at the bed's root point: parts above the soil never cross the bed (planks, posts, clods), the
    footprint stays inside the soil. Rooted parts (stems' feet, the carrot's root, the canes) are in the soil by design."""
    tb = _bvh(bed)
    out = {}
    root = Matrix.Translation((0.0, 0.0, SOIL_TOP))
    for obj in plants:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.transform(root)
        # Only the faces whose vertices are all above the soil under them (rooted feet excluded).
        keep = [f for f in bm.faces if all(v.co.z > _soil_height(v.co.x, v.co.y) + 0.001 for v in f.verts)]
        geom = [f for f in bm.faces if f not in set(keep)]
        bmesh.ops.delete(bm, geom=geom, context='FACES')
        tree = BVHTree.FromBMesh(bm)
        crossing = len(tb.overlap(tree)) if len(bm.faces) else 0
        bm.free()
        r, h = measure_plant(obj)
        rooted = set(obj.get("gd_rooted", []))
        low = min(((v.co.z + SOIL_TOP - _soil_height(v.co.x, v.co.y)) for v in obj.data.vertices
                   if v.index not in rooted and math.hypot(v.co.x, v.co.y) > 0.05), default=1.0)
        out[obj.name] = (round(r, 3), round(h, 3), crossing, round(low, 4))
    return out


def check_layout():
    """The path's stones clear of every bed, the beds clear of each other, the fence behind them all."""
    problems = []
    beds = [bed_center(i) for i in range(5)]
    for x, y, r in path_stones():
        for i, c in enumerate(beds):
            dx = max(0.0, abs(x - c.x) - BED_HALF[0])
            dy = max(0.0, abs(y - c.y) - BED_HALF[1])
            if math.hypot(dx, dy) < r + 0.03:
                problems.append(f"stone ({x:.2f}, {y:.2f}) near bed {i}")
    for i in range(5):
        for j in range(i + 1, 5):
            a, b = beds[i], beds[j]
            if abs(a.x - b.x) < 2 * BED_HALF[0] + 0.05 and abs(a.y - b.y) < 2 * BED_HALF[1] + 0.05:
                problems.append(f"bed {i} near bed {j}")
    back = max(c.y for c in beds) + BED_HALF[1]
    if min(fence_point(x).y for x in (-0.8, 0.0, 0.8)) < back + 0.3:
        problems.append("fence too close behind the beds")
    return problems


# ---------------------------------------------------------------- build, write, export

def build_garden():
    """Builds every garden object in the scene "Garden" (export collection) and returns triangles and sizes, the fits
    of the plants in a bed, the cover's clearance and the layout's problems. Never touches another scene's objects."""
    scene, col = _gd_scene()
    report = {}
    props = [build_ground(col), build_bed(col), build_bed_cover(col), build_path(col), build_fence(col),
             build_watering_can(col), build_harvest_basket(col)]
    total = 0
    for obj in props:
        tris = triangle_count(obj)
        total += tris
        if tris > GD_BUDGETS[obj.name]:
            raise RuntimeError(f"{obj.name}: {tris} triangles over its budget of {GD_BUDGETS[obj.name]}")
        pts = [Vector(c) for c in obj.bound_box]
        report[obj.name] = (tris, tuple(round(max(q[a] for q in pts) - min(q[a] for q in pts), 3) for a in range(3)))
    if total > GD_SET_BUDGET:
        raise RuntimeError(f"set props: {total} triangles over {GD_SET_BUDGET}")
    report["set_total"] = total
    plants = []
    problems = []
    for _, stem_name in GD_PLANTS:
        for stage in (1, 2, 3):
            obj = build_plant(col, stem_name, stage)
            plants.append(obj)
            tris = triangle_count(obj)
            r, h = measure_plant(obj)
            limit_h = TALL_PLANTS.get(stem_name, PLANT_HEIGHT)
            if not (GD_PLANT_TRIS[0] <= tris <= GD_PLANT_TRIS[1]):
                problems.append(f"{obj.name}: {tris} triangles")
            if r > PLANT_RADIUS:
                problems.append(f"{obj.name}: radius {r:.3f}")
            if h > limit_h:
                problems.append(f"{obj.name}: height {h:.3f}")
            report[obj.name] = (tris, round(r, 3), round(h, 3))
    bed = bpy.data.objects["GD_Bed"]
    report["cover"] = check_cover(bed, bpy.data.objects["GD_BedCover"])
    fits = check_plants_in_bed(bed, plants)
    for name, (r, h, crossing, low) in fits.items():
        if crossing:
            problems.append(f"{name}: {crossing} triangle pairs cross the bed")
        if low < 0.0:
            problems.append(f"{name}: a part {low:.4f} under the soil")
    problems += check_layout()
    report["problems"] = problems
    return report


def _gd_root():
    marker = os.sep + "SourceArt" + os.sep
    path = bpy.data.filepath
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ first (or pass the project root).")
    return path.split(marker)[0]


def _gd_clear_previews():
    scene = bpy.data.scenes.get(GD_SCENE)
    for obj in (scene.objects if scene else []):
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]
            if obj.name in GD_ROWS:
                obj.data.materials.clear()


def _gd_arrange():
    """Lays the objects out for anyone opening the .blend: the set at its place (beds and plants in a grid beside it);
    pivots are untouched (only object locations move, never exported: the FBX exporter writes the meshes at their
    object transforms, so export_garden_live() puts every object back at the origin first)."""
    scene = bpy.data.scenes[GD_SCENE]
    for obj in scene.objects:
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)


def write_garden_blend(root=None):
    """Writes the Garden scene and everything it uses to BL_Activity_Garden.blend (compressed). The open file stays as
    it is."""
    root = root or _gd_root()
    _gd_clear_previews()
    _gd_arrange()
    path = os.path.join(root, *GD_BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {bpy.data.scenes[GD_SCENE]}, fake_user=True, compress=True)
    return path


def export_garden_live(root=None):
    """The two FBX files straight from the live session that built the scene, right after write_garden_blend() (load
    pw_export.py in the same namespace first for FBX_PRESET): the exact objects of each export, the project preset, the
    header recording BL_Activity_Garden.blend (FBX ApplicationNativeFile), the file holding these very objects."""
    import io_scene_fbx.export_fbx_bin as fbx_bin

    root = root or _gd_root()
    source = os.path.join(root, *GD_BLEND.split("/"))
    if not os.path.exists(source):
        raise RuntimeError("Write the scene first: write_garden_blend()")
    _gd_clear_previews()
    _gd_arrange()
    scene = bpy.data.scenes[GD_SCENE]
    window = bpy.context.window
    previous = window.scene if window is not None else None
    if window is not None:
        window.scene = scene

    class _Data:
        def __getattr__(self, name):
            return source if name == "filepath" else getattr(bpy.data, name)

    class _Bpy:
        data = _Data()

        def __getattr__(self, name):
            return getattr(bpy, name)

    real = fbx_bin.bpy
    done = {}
    try:
        fbx_bin.bpy = _Bpy()
        for name, names in GD_EXPORTS.items():
            out = os.path.join(root, *GD_OUT.split("/"), name)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            layer = scene.view_layers[0]
            for o in scene.objects:
                o.select_set(False, view_layer=layer)
            chosen = [bpy.data.objects[n] for n in names]
            for o in chosen:
                o.hide_set(False, view_layer=layer)
                o.select_set(True, view_layer=layer)
            layer.objects.active = chosen[0]
            with bpy.context.temp_override(window=window, scene=scene, view_layer=layer):
                bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, bake_anim=False, **FBX_PRESET)
            done[name] = len(chosen)
    finally:
        fbx_bin.bpy = real
        if window is not None and previous is not None:
            window.scene = previous
    return done


def finish_garden_blend():
    """Run once inside BL_Activity_Garden.blend: its export collection takes the standard name, the file is saved."""
    col = bpy.data.collections.get(GD_COLLECTION)
    if col is not None and bpy.data.collections.get("EXPORT") is None:
        col.name = "EXPORT"
    bpy.ops.wm.save_mainfile(compress=True)


def export_garden():
    """The same FBX files from BL_Activity_Garden.blend itself, with pw_export.export_static (exact names)."""
    _gd_clear_previews()
    done = {}
    for name, names in GD_EXPORTS.items():
        path, objects = export_static(GD_OUT + "/" + name, names)
        if sorted(objects) != sorted(names):
            raise RuntimeError(f"{name}: exported {sorted(objects)}, expected {sorted(names)}")
        done[name] = len(objects)
    return done


# ---------------------------------------------------------------- review images

# The set camera of GardenSetPipeline in Blender axes (Unity (x, y, z) is Blender (x, z, y) after the half turn).
GD_CAMERA_EYE = Vector((0.0, -7.652, 8.462))
GD_CAMERA_TARGET = Vector((0.0, 0.80, 0.30))
GD_CAMERA_FOV = 30.0
GD_MOCK_PLANTS = ["Sunflower", "WildStrawberry", "DwarfApple", "Lavender", "RoundMelon"]   # stage 3 in beds 0 to 4


def _gd_world(scene, top, bottom):
    world = _nd_preview_world(scene)
    ramp = next(n for n in world.node_tree.nodes if n.type == "VALTORGB")
    ramp.color_ramp.elements[0].color = tuple(_nd_linear(bottom)) + (1.0,)
    ramp.color_ramp.elements[1].color = tuple(_nd_linear(top)) + (1.0,)
    return world


def render_garden_previews(out_dir, refs, size=1.0):
    """Review images in out_dir (T43_props_*.png), EEVEE, one sun from the top left of the image, palette colors
    computed like SH_EnvironmentOpaque. refs: {"grooming": BL_Activity_Grooming.blend} read only. Everything made here
    is removed afterwards; nothing is saved."""
    scene = bpy.data.scenes[GD_SCENE]
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    _gd_arrange()
    st = NdStudio(scene, size)
    objs = {o.name: o for o in scene.objects if o.type == 'MESH' and o.name in GD_ROWS}
    for name, obj in objs.items():
        st.dress(obj, GD_PALETTES[GD_ROWS[name]])
    files = []
    try:
        # 1. The set mock through the Unity set camera (1080 x 2340 at half size): beds, a ripe plant in each of the
        # three owned beds and two more, covers on beds 3 and 4 shown beside, the path, fence, can, basket, backdrop.
        shown = [objs["GD_Ground"], objs["GD_Path"], objs["GD_Fence"]]
        backdrop = _nd_append_mesh(st, refs["grooming"], "GR_SetBackdrop", ND_REF_ROWS["set_backdrop"])
        backdrop.location = (0.0, 1.6, 0.6 - 0.02)
        backdrop.scale = (2.0, 2.0, 2.0)
        shown.append(backdrop)
        for i in range(5):
            c = bed_center(i)
            bed = st.copy(objs["GD_Bed"], GD_PALETTES["palette.garden.bed"], c)
            shown.append(bed)
            if i >= 3:
                shown.append(st.copy(objs["GD_BedCover"], GD_PALETTES["palette.garden.cover"], c))
            else:
                name = plant_object_name(GD_MOCK_PLANTS[i], 3)
                shown.append(st.copy(objs[name], GD_PALETTES[GD_ROWS[name]], c + UP * SOIL_TOP))
        # As GardenSetPipeline places them (CanAt, CanYaw 150 and BasketAt, BasketYaw 192: Unity yaw 180 - a is Blender a).
        shown.append(st.copy(objs["GD_Basket"], GD_PALETTES["palette.garden.basket"], Vector((0.6, -0.08, 0.0)),
                             (0.0, 0.0, math.radians(-12.0))))
        shown.append(st.copy(objs["GD_WateringCan"], GD_PALETTES["palette.garden.watering_can"], Vector((1.16, -0.06, 0.0)),
                             (0.0, 0.0, math.radians(30.0))))
        st.show(shown)
        st.aim(GD_CAMERA_EYE, GD_CAMERA_TARGET, GD_CAMERA_FOV, 540, 1170)
        files.append(st.shot(os.path.join(out_dir, "T43_props_set.png")))

        # 1b. The props close up: a bed with a ripe poppy, a covered bed with its sign, the can and the basket, stones.
        ground = _nd_ground(st, 4.0, GD_PALETTES["palette.garden.ground"])
        close = [ground,
                 st.copy(objs["GD_Bed"], GD_PALETTES["palette.garden.bed"], Vector((-0.62, 0.0, 0.0))),
                 st.copy(objs[plant_object_name("Poppy", 3)], GD_PALETTES["palette.garden.poppy"], Vector((-0.62, 0.0, SOIL_TOP))),
                 st.copy(objs["GD_Bed"], GD_PALETTES["palette.garden.bed"], Vector((0.66, 0.3, 0.0))),
                 st.copy(objs["GD_BedCover"], GD_PALETTES["palette.garden.cover"], Vector((0.66, 0.3, 0.0))),
                 st.copy(objs["GD_WateringCan"], GD_PALETTES["palette.garden.watering_can"], Vector((-0.25, -0.78, 0.0)),
                         (0.0, 0.0, math.radians(30.0))),
                 st.copy(objs["GD_Basket"], GD_PALETTES["palette.garden.basket"], Vector((0.42, -0.72, 0.0)),
                         (0.0, 0.0, math.radians(-12.0)))]
        st.show(close)
        st.aim(Vector((0.35, -3.3, 2.1)), Vector((0.05, -0.1, 0.18)), 32.0, 1400, 900)
        files.append(st.shot(os.path.join(out_dir, "T43_props_closeup.png")))

        # 2. Every plant in its three stages side by side, seen from the set camera's direction, on a light and a dark
        # background: one image per plant and tone (T43_plant_<Plant>_<tone>.png; the review sheet composes them).
        def strip(stem_name, tone, top, bottom):
            _gd_world(scene, top, bottom)
            shown = []
            for s in (1, 2, 3):
                name = plant_object_name(stem_name, s)
                shown.append(st.copy(objs[name], GD_PALETTES[GD_ROWS[name]], Vector(((s - 2) * 0.8, 0.0, 0.0))))
            st.show(shown)
            mid = Vector((0.0, 0.0, 0.36 if stem_name not in TALL_PLANTS else 0.5))
            st.aim(mid + TO_CAMERA * 7.0, mid, 13.0 if stem_name not in TALL_PLANTS else 14.5, 720, 400)
            path = st.shot(os.path.join(out_dir, f"T43_plant_{stem_name}_{tone}.png"))
            for o in shown:
                st.temp.remove(o)
                data = o.data
                bpy.data.objects.remove(o)
                if data.users == 0:
                    bpy.data.meshes.remove(data)
            return path

        for _, stem_name in GD_PLANTS:
            files.append(strip(stem_name, "light", "F4F1E8", "E9F0E4"))
            files.append(strip(stem_name, "dark", "2E3446", "3A4152"))
        # 3. Silhouettes: every ripe plant in black on white, each about 64 px tall (charte, grid point 1).
        black = bpy.data.materials.get("WORK_GD_Black") or bpy.data.materials.new("WORK_GD_Black")
        black.use_nodes = True
        bsdf = next(n for n in black.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)
        bsdf.inputs["Roughness"].default_value = 1.0
        for key in ("Specular IOR Level", "Specular"):
            if key in bsdf.inputs:
                bsdf.inputs[key].default_value = 0.0
        _gd_world(scene, "FFFFFF", "FFFFFF")
        shown = []
        for k, (_, stem_name) in enumerate(GD_PLANTS):
            name = plant_object_name(stem_name, 3)
            o = st.copy(objs[name], GD_PALETTES[GD_ROWS[name]], Vector(((k - 6) * 0.8, 0.0, 0.0)))
            o.data.materials.clear()
            o.data.materials.append(black)
            shown.append(o)
        st.show(shown)
        st.sun.data.energy = 0.0
        mid = Vector((0.0, 0.0, 0.45))
        st.aim(mid + TO_CAMERA * 40.0, mid, 2.0, 960, 120)
        files.append(st.shot(os.path.join(out_dir, "T43_props_silhouettes.png")))
        st.sun.data.energy = 3.2
    finally:
        st.remove()
        _gd_clear_previews()
        mat = bpy.data.materials.get("WORK_GD_Black")
        if mat is not None and mat.users == 0:
            bpy.data.materials.remove(mat)
    return files
