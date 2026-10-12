"""Forest Magnifique (T50, lane B): the seven forest grounds, the brook, moss rocks, props, scatter, and the Grassland's
ForestEdge ground opened to the east. Source: SourceArt/Blender/Environment/Forest/BL_Biome_Forest_Ground.blend.

Same data contract as the Grassland scripts: PW_Regions color attribute (palette columns 1-4), PW_Occlusion uv (x
occlusion, y wind height 0 at the base to 1 at the tips), pivot at the base on the ground, +Z up, Blender -X = Unity
east (to_blender). Features are written in Unity world coordinates (x east along the strip, z away from the camera):
every ground evaluates the same world fields, so two neighbours always meet with the same height (0 on a joined side)
and the same paint on their shared vertices (96 segments everywhere, like GL_Ground_ForestEdge).

Headless, from the project root (Blender 5.2):
    blender -b --factory-startup --python SourceArt/Blender/Tools/pw_forest_ground.py -- forest
        builds BL_Biome_Forest_Ground.blend from scratch, saves it, exports FO_Grounds, FO_Water, FO_Props, FO_Scatter.fbx
    blender -b --factory-startup --python SourceArt/Blender/Tools/pw_forest_ground.py -- edge
        opens BL_Biome_Grassland_Magnifique.blend, rebuilds GL_Ground_ForestEdge (joins -X and +X, forest-edge paint on
        its east third), saves it, re-exports GL_Grounds_T33B.fbx with the same six object names
    blender -b --factory-startup --python SourceArt/Blender/Tools/pw_forest_ground.py -- check
        seam report: positions and final ground colors on every seam from ForestEdge (x 105) to LakeTransition (x 189)
    blender -b --factory-startup --python SourceArt/Blender/Tools/pw_forest_ground.py -- preview
        review sheet Docs/Captures/Review/T50_ground_props_sheet.png (Workbench, palette colors as the shaders mix them)
Inside Blender, after pw_grassland.py, pw_grassland_t33.py and pw_grassland_t33b.py in the same namespace:
    ns["build_forest_grounds"](), ns["build_brook_water"](), ns["build_forest_props"](), ns["build_forest_scatter"](),
    ns["build_forest_edge_t50"]()   # in BL_Biome_Grassland_Magnifique.blend only

Palette rows (BiomeWizard.ForestSpec) and the regions painted for them:
  palette.forest.ground  FO_Ground_*: R moss, G leaf litter, B meadow grass, A worn path (the path is in A here, not B as
                         in the Grassland: a forest worn mask reads colors32.a); soil walls lean to G; uv.x occlusion.
  palette.forest.water   FO_Brook_Water (SH_WaterLite): color R foam, G depth, A falling water; uv x across, y along (m).
  palette.forest.rock    FO_MossRock_*, FO_SteppingStones: base stone, R moss cushion, G light stone, B lichen, A moss rim.
  palette.forest.log     FO_Stump, FO_HollowLog, FO_RootArch, FO_TinyBridge, FO_Twig, FO_Acorn, FO_PineCone: base bark,
                         R moss, G light bark ridges, B amber (cut rings, nut), A pale wood (cut faces, planks, tips).
  palette.forest.tree    FO_Leaf_A/B: base dry brown (veins, stems), R deep green, B amber, A fresh light green.
  palette.forest.fern    FO_MossTuft, FO_Clover: base green, R deep green, G light top, B pale stalk, A pale tips, chevron.

World anchors for the chunk layouts (Unity world x, z; the chunk origin is x 112 + 14 k):
  path            z = -path_y(x) (pw_grassland_t33b.path_y, GrasslandPipeline.PathZ), fades out before the Lake rim
  root mounds     ROOT_MOUNDS (DeepForest, one in Entry): trees on their tops
  ancient tree    ANCIENT_TREE_SPOT (Clearing, far side, a low rise)
  giant stump     GIANT_STUMP_SPOT (Hollow, far lip of the dip)
  brook           BROOK_POINTS (Brook chunk coordinates); bridge_spot() gives the TinyBridge position and yaw
  mushroom stump  MUSHROOM_SPOT (MushroomCorner): FO_Stump in the litter disc, mushroom ring 0.85-1.35 m around it
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

FO_SEED = 5003
FO_POWER = 4.0
SEGMENTS = 96

# name: (Unity origin x, Blender joins, seed, rings). Blender "-X" is the Unity east side, "+X" the west side.
FOREST = {
    "FO_Ground_Entry": (112.0, ("-X", "+X"), 501, 16),
    "FO_Ground_DeepForest": (126.0, ("-X", "+X"), 503, 18),
    "FO_Ground_Clearing": (140.0, ("-X", "+X"), 505, 16),
    "FO_Ground_Hollow": (154.0, ("-X", "+X"), 507, 20),
    "FO_Ground_Brook": (168.0, ("-X", "+X"), 509, 24),
    "FO_Ground_MushroomCorner": (182.0, ("-X", "+X"), 511, 16),
    "FO_Ground_LakeTransition": (196.0, ("+X",), 513, 18),  # the east rim stays closed: the Lake comes in T57
}

# ---------------- world features (Unity world x, z) ----------------
ROOT_MOUNDS = [(115.8, 4.4, 0.9, 0.1), (122.6, 3.7, 1.0, 0.13), (125.0, 4.7, 1.1, 0.16), (128.9, 3.6, 1.0, 0.14),
               (123.8, -3.3, 0.9, 0.12), (129.4, -3.0, 1.0, 0.15)]  # x, z, radius, height
CLEARING = (140.0, 0.2, 5.6, 5.0)            # center x, z, half extents: the sunny meadow
ANCIENT_TREE_SPOT = (138.8, 2.6, 2.4, 0.14)  # a low rise on the far side, mossy, litter under the crown
HOLLOW = (154.0, 0.3, 4.2, 0.32)             # center x, z, radius, depth (gentle round dip)
GIANT_STUMP_SPOT = (154.4, 4.9, 1.8, 0.08)   # on the far lip of the dip
GLADE = (181.6, 1.2)                         # the mossy glade of the mushroom corner
MUSHROOM_SPOT = (180.9, 3.0)                 # the stump spot, mushroom ring around it
LAKE_LOW = (195.0, 202.5, -0.3)              # the ground lowers gently toward the closed east rim
BROOK_ORIGIN = 168.0
BROOK_POINTS = [(1.9, 6.1), (1.35, 4.6), (0.1, 3.15), (-0.85, 1.6), (-0.7, -0.2), (0.15, -2.0), (0.85, -3.9),
                (0.5, -5.6), (0.7, -7.0)]   # Brook chunk coordinates: a spring on the far side, a fall over the near rim
BROOK_HALF = 0.5

# sRGB rows (base, R, G, B, A, occlusion, accent) for the checks, the x 105 seam solve and the preview. FO rows: BiomeWizard.ForestSpec
# (T50 art pass: light moss floor, warm litter, light tan path; keep both in step, then run "-- edge" and "-- forest" again);
# the Grassland ground row: DEF_Palettes_Grassland (palette.grassland.ground).
PALETTES = {
    "fo.tree": ("7A5A42", "4C8C50", "6EAE5A", "E2AE4E", "A0CC78", "3A3428", "FFE6B4"),
    "fo.ground": ("78A656", "6AB24E", "AE8A5C", "8FB878", "E2CFA0", "4C5E36", "B8CC7C"),
    "fo.rock": ("9A9586", "7AAE58", "B0AB9A", "D2CCB2", "C0D2A0", "4A473C", "F2E6C8"),
    "fo.log": ("6E4E38", "78AA58", "8E6C4C", "E0AC48", "B4966C", "3A2C22", "FFE2A8"),
    "fo.fern": ("5A9C50", "4A8A48", "86BE62", "ACD07A", "C8E29A", "2E4428", "EEF6D0"),
    "fo.water": ("6FA8A0", "8CC4B8", "C8E6DC", "4E8A80", "E8F4EE", "2E4A46", "FFFFFF"),
    "gl.ground": ("78A862", "8FB878", "5F9152", "B8C98E", "8F6A50", "4A6542", "9CBC74"),
}
MACRO_SCALE, MACRO_STRENGTH = 0.21, 0.45  # MAT_Ground (SH_GroundMaster)


# ---------------- fast cached noise (same values as pw_grassland.wave_noise) ----------------
_NOISE = {}


def fnoise(seed, x, y, z):
    terms = _NOISE.get(seed)
    if terms is None:
        import random
        rng = random.Random(seed)
        terms = []
        amp, freq = 1.0, 1.0
        for _ in range(3):
            for _ in range(3):
                d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
                terms.append((d.x * freq * 2.4, d.y * freq * 2.4, d.z * freq * 2.4, rng.uniform(0, 6.283), amp))
            amp *= 0.5
            freq *= 2.1
        norm = sum(t[4] for t in terms)
        terms = [(a, b, c, p, w / norm) for a, b, c, p, w in terms]
        _NOISE[seed] = terms
    return sum(w * math.sin(x * a + y * b + z * c + p) for a, b, c, p, w in terms)


def lerp(a, b, t):
    return a + (b - a) * t


def _dome(wx, wz, cx, cz, radius, height):
    d = math.hypot(wx - cx, wz - cz)
    if d >= radius:
        return 0.0
    k = 0.5 + 0.5 * math.cos(math.pi * d / radius)
    return height * k * k * (3.0 - 2.0 * k)


# ---------------- the brook (world coordinates) ----------------
class Brook:
    def __init__(self):
        pts = catmull([(BROOK_ORIGIN + x, z) for x, z in BROOK_POINTS], 0.15)
        s = [0.0]
        for i in range(1, len(pts)):
            s.append(s[-1] + (pts[i] - pts[i - 1]).length)
        self.pts, self.s, self.total = pts, s, s[-1]

    def nearest(self, wx, wz):
        p = Vector((wx, wz, 0.0))
        best, best_s = 1e9, 0.0
        pts, s = self.pts, self.s
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (a + ab * t - p).length
            if d < best:
                best, best_s = d, s[i] + (s[i + 1] - s[i]) * t
        return best, best_s

    def level(self, s):
        return -0.07 * (s / self.total)

    def half(self, s):
        return BROOK_HALF * (0.6 + 0.4 * smoothstep(0.0, 2.5, s)) * (1.0 + 0.1 * math.sin(s * 1.9))


_BROOK = None


def brook():
    global _BROOK
    if _BROOK is None:
        _BROOK = Brook()
    return _BROOK


def near_brook(wx):
    return BROOK_ORIGIN - 7.5 < wx < BROOK_ORIGIN + 7.5


def bridge_spot():
    """Where the worn path crosses the brook: Brook chunk (x, z), yaw in degrees (Unity, around +Y), water width."""
    b = brook()
    best = None
    for k in range(-60, 61):
        x = k * 0.05
        wx = BROOK_ORIGIN + x
        d, s = b.nearest(wx, -path_y(wx))
        if best is None or d < best[0]:
            best = (d, x, s)
    _, x, s = best
    wx = BROOK_ORIGIN + x
    slope = ((-path_y(wx + 0.5)) - (-path_y(wx - 0.5))) / 1.0
    return x, -path_y(wx), -math.degrees(math.atan(slope)), 2.0 * b.half(s)


# ---------------- ground height and paint ----------------
def lake_low(wx):
    return LAKE_LOW[2] * smoothstep(LAKE_LOW[0], LAKE_LOW[1], wx)


def hollow01(wx, wz):
    d = math.hypot(wx - HOLLOW[0], wz - HOLLOW[1])
    return 0.0 if d >= HOLLOW[2] else _dome(wx, wz, HOLLOW[0], HOLLOW[1], HOLLOW[2], 1.0)


def forest_height(name, x, y, rim):
    """Top surface height at Blender chunk coordinates (x, y) of ground `name`."""
    origin, joins, seed, _ = FOREST[name]
    wx, wz = origin - x, -y
    join = _join_weight(x, joins)
    free = 1.0 - join
    rolls = smoothstep(GROUND_FLAT_RADIUS * 0.85, GROUND_FLAT_RADIUS * 1.25, math.hypot(x, y)) * free
    z = rolls * 0.22 * (0.5 + 0.5 * fnoise(seed, x * 0.25, y * 0.25, 0.0))
    if rim:
        z -= 0.06 * free
    lift = 0.0
    for mx, mz, r, h in ROOT_MOUNDS:
        lift += _dome(wx, wz, mx, mz, r, h)
    lift += _dome(wx, wz, *ANCIENT_TREE_SPOT)
    lift += _dome(wx, wz, *GIANT_STUMP_SPOT)
    lift -= HOLLOW[3] * hollow01(wx, wz)
    z += lift * free
    z += lake_low(wx)
    if near_brook(wx):
        b = brook()
        d, s = b.nearest(wx, wz)
        half = b.half(s)
        w = (1.0 - smoothstep(half * 0.55, half * 1.25, d)) * smoothstep(-0.2, 0.5, s)
        if w > 0.0:
            target = b.level(s) - 0.16 * max(0.0, 1.0 - (d / half) ** 2) - 0.03
            if rim:
                target = max(target, -0.12)
            z = z + (min(z, target) - z) * w
    return z


def clearing01(wx, wz):
    e = math.hypot((wx - CLEARING[0]) / CLEARING[2], (wz - CLEARING[1]) / CLEARING[3])
    return 1.0 - smoothstep(0.6, 1.0, e)


def forest_field(wx, wz):
    """Forest palette weights at a world point of the top surface: (moss R, litter G, meadow B, path A, occlusion)."""
    # 0 the Grassland meadow, 1 the forest floor: the Forest starts as the Grassland's meadow at the x 105 seam (its meadow
    # column is the Grassland's light grass, so both sides meet with the same colors) and turns to moss inside the Entry
    # along an irregular, noisy line (never a straight band).
    edge = smoothstep(105.2, 111.5, wx + 1.2 * fnoise(FO_SEED + 9, wx * 0.45, wz * 0.45, 9.0))
    inside = smoothstep(105.4, 107.8, wx)       # forest-only marks (moss patches, carpet) start past the seam
    carpet = smoothstep(105.6, 108.0, wx) * (1.0 - smoothstep(109.0, 113.0, wx))  # moss carpet under the last grass
    deep = smoothstep(108.0, 121.0, wx)
    light = smoothstep(0.25, 0.75, fnoise(PATCH_SEED + 1, wx * 0.35, wz * 0.35, 1.0))
    dark = smoothstep(0.35, 0.85, fnoise(PATCH_SEED + 2, wx * 0.3, wz * 0.3, 2.0))
    mossn = 0.5 + 0.5 * fnoise(FO_SEED + 1, wx * 0.3, wz * 0.3, 3.0)
    littern = 0.5 + 0.5 * fnoise(FO_SEED + 2, wx * 0.42, wz * 0.42, 4.0)
    grassn = 0.5 + 0.5 * fnoise(FO_SEED + 3, wx * 0.55, wz * 0.55, 5.0)
    clear = clearing01(wx, wz)
    glade = 1.0 - smoothstep(3.0, 5.2, math.hypot(wx - GLADE[0], wz - GLADE[1]))
    damp = smoothstep(194.0, 202.0, wx)
    hol = hollow01(wx, wz)

    # Meadow grass over moss: the Grassland's light patches stay grassy longest; it comes back in the sunny clearing.
    grass = (1.0 - edge) * (0.35 + 0.6 * light - 0.3 * dark)  # the Grassland's light and dark grass patches
    grass = max(grass, clear * (0.55 + 0.4 * grassn))
    # Moss: patches everywhere, a full carpet under the grass, the glade, the slopes of the hollow.
    cover = 0.46 + 0.16 * deep + 0.45 * glade + 0.6 * hol * (1.0 - hol) - 0.15 * damp
    mossn += 0.12 * fnoise(FO_SEED + 4, wx * 1.1, wz * 1.1, 7.0)  # finer, softer patch edges
    patch = smoothstep(1.0 - cover - 0.22, 1.0 - cover + 0.22, mossn)
    moss = max(patch * inside, carpet, min(1.0, grass * 1.6) * inside)
    # Leaf litter drifts: more under the canopy, pooled at the bottom of the hollow, little in the clearing.
    lcover = 0.10 + 0.26 * deep - 0.4 * clear + 0.5 * hol * hol
    litter = smoothstep(1.0 - lcover - 0.16, 1.0 - lcover + 0.16, littern) * 0.85 * (1.0 - 0.45 * patch)
    # Between the moss carpets the floor is leaf litter; warm bare soil shows only in small spots (cozy, never dark).
    soiln = 0.5 + 0.5 * fnoise(FO_SEED + 5, wx * 0.8, wz * 0.8, 8.0)
    gaps = (1.0 - max(patch, carpet)) * (0.5 + 0.3 * littern) * smoothstep(107.0, 115.0, wx)
    gaps *= 1.0 - 0.55 * smoothstep(0.66, 0.86, soiln)
    litter = max(litter, gaps)
    occ = 0.08 * deep * (1.0 - clear) + 0.06 * hol + 0.1 * damp

    # The ancient tree's rise: a moss ring, litter under the crown, no meadow at its foot.
    dt = math.hypot(wx - ANCIENT_TREE_SPOT[0], wz - ANCIENT_TREE_SPOT[1])
    litter = max(litter, 0.4 * (1.0 - smoothstep(0.8, 2.6, dt)))
    grass *= smoothstep(1.0, 2.8, dt)
    # The mushroom corner: a litter disc for the stump, a rich soil ring for the mushrooms, lush moss around.
    ds = math.hypot(wx - MUSHROOM_SPOT[0], wz - MUSHROOM_SPOT[1])
    disc = 1.0 - smoothstep(0.45, 0.65, ds)
    ring = smoothstep(0.75, 0.95, ds) * (1.0 - smoothstep(1.25, 1.5, ds))
    lush = (1.0 - smoothstep(1.5, 3.2, ds)) * (1.0 - ring) * (1.0 - disc)
    moss = max(moss * (1.0 - 0.7 * ring) * (1.0 - 0.6 * disc), lush)
    litter = max(litter * (1.0 - 0.85 * ring), 0.7 * disc)
    occ += 0.05 * ring

    # The worn path continues from the Grassland (path_y), fern-lined under the trees, and fades before the Lake rim.
    dz = abs(wz + path_y(wx))
    width = 0.7 + 0.1 * math.sin(wx * 0.83 + 1.3)
    # Softer at the seam: the Grassland palette meets it there with the same color (its path is a lighter cream).
    path = (0.45 + 0.25 * smoothstep(105.0, 111.0, wx)) * (1.0 - smoothstep(0.26, width, dz))
    path *= 1.0 - smoothstep(200.2, 202.6, wx)
    band = smoothstep(width - 0.05, width + 0.25, dz) * (1.0 - smoothstep(width + 0.55, width + 1.05, dz))
    band *= edge * (1.0 - clear)
    moss = max(moss, band * 0.95)
    litter *= 1.0 - band * 0.85
    grass *= 1.0 - band * 0.7

    # The Lake side: damp darker soil, less moss and litter.
    moss *= 1.0 - 0.45 * damp
    litter *= 1.0 - 0.6 * damp
    grass *= 1.0 - damp

    if near_brook(wx):
        b = brook()
        d, s = b.nearest(wx, wz)
        half = b.half(s)
        st = smoothstep(-0.2, 0.4, s)
        wet = (1.0 - smoothstep(half * 0.7, half * 1.35, d)) * st
        bank = smoothstep(half * 0.9, half * 1.3, d) * (1.0 - smoothstep(half * 1.8, half * 3.0, d)) * st
        moss = max(moss * (1.0 - wet), bank)
        litter *= (1.0 - wet) * (1.0 - 0.6 * bank)
        grass *= 1.0 - wet
        path = max(path * (1.0 - wet), wet * 0.32)
        occ = max(occ, wet * 0.42)
    return (moss, litter, grass, path, min(0.5, occ))


WALL = (0.0, 0.85, 0.0, 0.0)


def wall_occlusion(z_rel):
    """The Grassland soil wall shading (lip under the rim, darker toward the bottom), on the height below the rim."""
    lip = smoothstep(-0.05, -0.2, z_rel) * (1.0 - smoothstep(-0.6, -1.0, z_rel)) * 0.35
    under = smoothstep(-0.6, -1.05, z_rel) * 0.6
    return lip + under


# ---------------- palette mixing (as SH_GroundMaster) and the seam solver ----------------
def _lin(hex_color):
    import numpy as np
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return np.array([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def palette(key):
    import numpy as np
    return np.array([_lin(h) for h in PALETTES[key]])


def macro_at(wx, wz):
    px, py = wx * MACRO_SCALE, wz * MACRO_SCALE
    m = math.sin(px) * math.sin(py * 1.31) + 0.5 * math.sin(px * 2.7 + py * 1.9 + 1.7)
    return max(0.0, min(1.0, 0.5 + m * 0.33))


def ground_mix(w, pal, macro):
    c = pal[0] + (pal[6] - pal[0]) * (macro * MACRO_STRENGTH)
    for k in range(4):
        c = c + (pal[k + 1] - c) * w[k]
    return c + (pal[5] - c) * w[4]


def solve_weights(target, pal, macro, w0, lam):
    """Weights (R, G, B, A, occlusion) whose ground color is closest to `target` (linear), staying near w0.
    Damped least squares (Levenberg-Marquardt) inside [0, 1], from w0 and a few fixed starts; the best fit wins."""
    import numpy as np
    w0 = np.clip(np.array(w0, float), 0.0, 1.0)

    def residual(w):
        return ground_mix(w, pal, macro) - target

    def cost(w):
        r = residual(w)
        return float(r @ r + lam * ((w - w0) @ (w - w0)))

    starts = [w0, np.zeros(5), np.array([1.0, 0.0, 0.6, 0.3, 0.0]), np.array([1.0, 0.5, 0.3, 0.3, 0.0]),
              np.array([0.5, 0.3, 0.9, 0.5, 0.0]), np.array([1.0, 0.0, 0.0, 0.0, 0.3])]
    best, best_cost = w0, cost(w0)
    for start in starts:
        w, c, mu = start.copy(), cost(start), 1e-3
        for _ in range(40):
            r = residual(w)
            jac = np.empty((3, 5))
            for k in range(5):
                d = w.copy()
                d[k] += 1e-5
                jac[:, k] = (residual(d) - r) / 1e-5
            h = jac.T @ jac + lam * np.eye(5)
            g = jac.T @ r + lam * (w - w0)
            step = np.linalg.solve(h + mu * (np.diag(np.diag(h)) + 1e-9 * np.eye(5)), -g)
            trial = np.clip(w + step, 0.0, 1.0)
            tc = cost(trial)
            if tc < c:
                w, c, mu = trial, tc, mu * 0.3
            else:
                mu *= 10.0
                if mu > 1e8:
                    break
        if c < best_cost:
            best, best_cost = w, c
    return best


def to_srgb255(c):
    out = []
    for x in c:
        x = max(0.0, min(1.0, float(x)))
        out.append(255.0 * (x * 12.92 if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055))
    return out


# ---------------- grounds ----------------
def build_forest_ground(name, collections):
    origin, joins, seed, rings = FOREST[name]
    bm = bmesh.new()
    center = bm.verts.new((0.0, 0.0, forest_height(name, 0.0, 0.0, False)))
    radii = [GROUND_HALF * (i / rings) ** 0.9 for i in range(1, rings + 1)]
    grid = []
    for ri, r in enumerate(radii):
        ring = []
        for j in range(SEGMENTS):
            sx, sy = _contour(j / SEGMENTS * math.tau, FO_POWER, joins)
            x, y = sx * r, sy * r
            ring.append(bm.verts.new((x, y, forest_height(name, x, y, ri == rings - 1))))
        grid.append(ring)
    for j in range(SEGMENTS):
        bm.faces.new((center, grid[0][j], grid[0][(j + 1) % SEGMENTS]))
    for ri in range(rings - 1):
        a, b = grid[ri], grid[ri + 1]
        for j in range(SEGMENTS):
            n = (j + 1) % SEGMENTS
            bm.faces.new((a[j], b[j], b[n], a[n]))
    previous = grid[-1]
    # Diorama sides as pw_grassland_t33b: a joined side keeps its soil wall on the seam plane; walls follow the lake slope.
    for depth, inset in ((0.18, 0.08), (0.55, 0.16), (GROUND_DEPTH, 0.3)):
        ring = []
        for j in range(SEGMENTS):
            sx, sy = _contour(j / SEGMENTS * math.tau, FO_POWER, joins)
            keep = _join_weight(sx * GROUND_HALF, joins)
            low = lake_low(origin - sx * GROUND_HALF)
            ring.append(bm.verts.new((sx * (GROUND_HALF - inset * (1.0 - keep)), sy * (GROUND_HALF - inset), -depth + low)))
        for j in range(SEGMENTS):
            n = (j + 1) % SEGMENTS
            bm.faces.new((previous[j], ring[j], ring[n], previous[n]))
        previous = ring
    bottom = bm.verts.new((0.0, 0.0, -GROUND_DEPTH + lake_low(origin + 7.0) * 0.5))
    for j in range(SEGMENTS):
        bm.faces.new((previous[(j + 1) % SEGMENTS], previous[j], bottom))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = object_from_bmesh(name, bm, collections)
    mark_sharp_by_angle(obj, 50.0)
    top_count = 1 + rings * SEGMENTS
    cache = {}

    def field(co, i):
        if i not in cache:
            wx, wz = origin - co.x, -co.y
            if i < top_count:
                cache[i] = forest_field(wx, wz)
            else:
                cache[i] = WALL + (wall_occlusion(co.z - lake_low(wx)),)
        return cache[i]

    write_layers(obj, lambda co, n, i: field(co, i)[:4], lambda co, n, i: (field(co, i)[4], 0.0))
    if origin - 7.0 < 110.5:
        _match_walls_to_grassland(obj, origin, top_count)
    return obj


def _match_walls_to_grassland(obj, origin, top_count):
    """Entry's west walls meet ForestEdge's soil walls (Grassland palette A + occlusion): solved to the same colors at the
    seam, easing into the forest walls by x 110.5."""
    import numpy as np
    fo, gl = palette("fo.ground"), palette("gl.ground")
    me = obj.data
    attr = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Occlusion"]
    occ = {}
    for loop in me.loops:
        occ[loop.vertex_index] = uv.data[loop.index].uv[0]
    new_occ = {}
    for v in me.vertices:
        if v.index < top_count:
            continue
        wx, wz = origin - v.co.x, -v.co.y
        t = smoothstep(105.0, 110.5, wx)
        if t >= 1.0:
            continue
        w0 = list(attr.data[v.index].color) + [occ[v.index]]
        m = macro_at(wx, wz)
        gl_color = gl[4] + (gl[5] - gl[4]) * wall_occlusion(v.co.z)
        target = gl_color + (ground_mix(np.array(w0), fo, m) - gl_color) * t
        w = solve_weights(target, fo, m, w0, 2e-4 * t + 1e-7)
        attr.data[v.index].color = tuple(float(x) for x in w[:4])
        new_occ[v.index] = float(w[4])
    for loop in me.loops:
        if loop.vertex_index in new_occ:
            uv.data[loop.index].uv = (new_occ[loop.vertex_index], 0.0)


def build_forest_grounds():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    return {name: triangle_count(build_forest_ground(name, target)) for name in FOREST}


# ---------------- GL_Ground_ForestEdge (BL_Biome_Grassland_Magnifique.blend) ----------------
def build_forest_edge_t50():
    """Rebuilds GL_Ground_ForestEdge with pw_grassland_t33b (joins -X and +X), then eases its east third (x 100.3 to
    105) into the forest floor: Grassland palette weights solved to the colors of the forest field, so the grass thins
    into moss and meets FO_Ground_Entry at x 105 with the same colors. The west two thirds stay as T33B painted them."""
    import numpy as np
    cols = ensure_collections()
    obj = build_ground_t33b("GL_Ground_ForestEdge", [cols["GEO"], cols["EXPORT"]])
    origin, _, _, rings, segments, _, _ = STRIP["GL_Ground_ForestEdge"]
    top_count = 1 + rings * segments
    fo, gl = palette("fo.ground"), palette("gl.ground")
    me = obj.data
    attr = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Occlusion"]
    occ = {}
    for loop in me.loops:
        occ[loop.vertex_index] = uv.data[loop.index].uv[0]
    new_occ = {}
    for v in me.vertices:
        if v.index >= top_count:
            continue
        wx, wz = origin - v.co.x, -v.co.y
        t = smoothstep(100.3, 105.0, wx)
        if t <= 0.0:
            continue
        w0 = list(attr.data[v.index].color) + [occ[v.index]]
        m = macro_at(wx, wz)
        own = ground_mix(np.array(w0), gl, m)
        target = own + (ground_mix(np.array(forest_field(wx, wz)), fo, m) - own) * t
        w = solve_weights(target, gl, m, w0, 2e-4 * (1.0 - t) + 1e-7)
        attr.data[v.index].color = tuple(float(x) for x in w[:4])
        new_occ[v.index] = float(w[4])
    for loop in me.loops:
        if loop.vertex_index in new_occ:
            uv.data[loop.index].uv = (new_occ[loop.vertex_index], 0.0)
    return obj


# ---------------- brook water ----------------
def build_brook_water(collections=None):
    """FO_Brook_Water (SH_WaterLite, like GL_Stream_A): from the spring on the far side to a little fall over the near rim,
    in Brook chunk coordinates."""
    if collections is None:
        cols = ensure_collections()
        collections = [cols["GEO"], cols["EXPORT"]]
    b = brook()
    pts = [Vector((-(p.x - BROOK_ORIGIN), -p.y, 0.0)) for p in b.pts]
    s = b.s
    bm = bmesh.new()
    columns = 7
    rows, colors, uvs = [], [], []

    def add_row(p, tangent, along, z, half, falling, foam_bias):
        side = Vector((-tangent.y, tangent.x, 0.0)).normalized()
        row = []
        for k in range(columns):
            u = k / (columns - 1)
            off = (u - 0.5) * 2.0
            row.append(bm.verts.new(p + side * off * half + Vector((0, 0, z))))
            edge = abs(off)
            colors.append((max(smoothstep(0.55, 1.0, edge), foam_bias), 1.0 - edge * edge, 0.0, falling))
            uvs.append((u, along))
        rows.append(row)

    for i, p in enumerate(pts):
        tangent = (pts[min(len(pts) - 1, i + 1)] - pts[max(0, i - 1)]).normalized()
        spring = 1.0 - smoothstep(0.0, 0.9, s[i])
        add_row(p, tangent, s[i], b.level(s[i]) + 0.01, b.half(s[i]) * 0.92, 0.0, spring * 0.8)
    last, prev = pts[-1], pts[-2]
    tangent = (last - prev).normalized()
    fall = [(0.08, -0.03, 0.95, 0.3), (0.16, -0.16, 0.9, 0.6), (0.18, -0.42, 0.8, 1.0), (0.14, -0.75, 0.62, 1.0),
            (0.1, -1.05, 0.42, 1.0), (0.06, -1.32, 0.2, 1.0)]
    base_half = b.half(s[-1]) * 0.92
    along = s[-1]
    for k, (out, z, width, falling) in enumerate(fall):
        along += 0.28
        add_row(last + tangent * out, tangent, along, b.level(s[-1]) + z, base_half * width, falling, 0.35 + 0.15 * k)
    for r in range(len(rows) - 1):
        a, c = rows[r], rows[r + 1]
        for k in range(columns - 1):
            bm.faces.new((a[k], a[k + 1], c[k + 1], c[k]))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0 and f.calc_center_median().z > -0.1:
            f.normal_flip()
    obj = object_from_bmesh("FO_Brook_Water", bm, collections, smooth=True)
    _water_layers(obj, colors, uvs)
    return obj


# ---------------- moss rocks ----------------
MOSS_ROCKS = {  # width, depth, height, target tris, seed, icosphere subdivisions
    "S": (0.36, 0.3, 0.24, 90, 1601, 2),
    "M": (0.72, 0.6, 0.46, 160, 1603, 3),
    "L": (1.2, 0.95, 0.74, 240, 1607, 3),
    "Hero": (1.95, 1.5, 1.18, 560, 1611, 4),
}


def build_moss_rock(kind, collections):
    """Soft rounded boulder with a moss cushion on top: the moss vertices stand a little proud of the stone."""
    width, depth, height, target, seed, subdiv = MOSS_ROCKS[kind]
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=0.5)
    lobe = Vector((0.75, -0.35, 0.2)).normalized()
    for v in bm.verts:
        p = v.co.normalized()
        bulge = 1.0 + 0.12 * fnoise(seed, p.x * 1.2, p.y * 1.2, p.z * 1.2) + 0.045 * fnoise(seed + 7, p.x * 2.6, p.y * 2.6, p.z * 2.6)
        if kind == "Hero":
            bulge += 0.16 * max(0.0, p.dot(lobe)) ** 3  # a second shoulder: a broad resting stone
        co = Vector((p.x * width * 0.5 * bulge, p.y * depth * 0.5 * bulge, p.z * height * 0.5 * bulge))
        if co.z > height * 0.3:
            co.z = height * 0.3 + (co.z - height * 0.3) * 0.72  # a rounded shelf where the moss sits
        if co.z < -height * 0.28:
            co.z = -height * 0.28 + (co.z + height * 0.28) * 0.15  # flat underside: rocks sit in the floor
        v.co = co
    min_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_z + height * 0.08
    bm.normal_update()
    name = f"FO_MossRock_{kind}"
    obj = object_from_bmesh(name, bm, collections)
    decimate_to(obj, target)
    me = obj.data
    me.update()
    thickness = 0.012 + 0.012 * height

    def moss_of(co, n):
        return smoothstep(0.32, 0.7, n.z + 0.22 * fnoise(seed + 3, co.x * 3.0, co.y * 3.0, co.z * 3.0))

    masks = [moss_of(v.co, v.normal) for v in me.vertices]
    for v, m in zip(me.vertices, masks):
        v.co += v.normal * thickness * m
    me.update()
    mark_sharp_by_angle(obj, 40.0)
    top = max(v.co.z for v in me.vertices)

    def regions(co, n, i):
        m = masks[i]
        rim = 4.0 * m * (1.0 - m)
        light_stone = smoothstep(-0.1, 0.6, n.z) * 0.45 * (1.0 - m)
        lichen = (1.0 if fnoise(seed + 11, co.x * 7.0, co.y * 7.0, co.z * 7.0) > 0.5 else 0.0) * (1.0 - m) * 0.75
        return (m, light_stone, lichen, min(1.0, rim * 0.8 + m * smoothstep(0.85, 1.0, n.z) * 0.3))

    def occlusion(co, n, i):
        return (0.7 * (1.0 - smoothstep(0.0, top * 0.3, co.z)) + 0.2 * smoothstep(0.1, -0.6, n.z), co.z / top)

    write_layers(obj, regions, occlusion)
    return obj


def build_stepping_stones(collections):
    """FO_SteppingStones: five flat mossy stones in a gentle curve across the brook (local X along the crossing)."""
    import random
    bm = bmesh.new()
    rng = random.Random(1701)
    for k in range(5):
        x = -0.9 + k * 0.45
        size = rng.uniform(0.17, 0.23)
        _lump(bm, Vector((x, 0.08 * math.sin(k * 1.3) + rng.uniform(-0.04, 0.04), -0.02)),
              Vector((size, size * 0.85, 0.07)), 1710 + k, subdivisions=2)

    def regions(co, n, i):
        wet = 1.0 - smoothstep(-0.04, 0.02, co.z)
        moss = smoothstep(0.75, 0.95, n.z) * smoothstep(-0.1, 0.4, fnoise(1720, co.x * 5.0, co.y * 5.0, 0.0)) * (1.0 - wet)
        return (moss, smoothstep(0.5, 0.95, n.z) * 0.5 * (1.0 - wet), 0.0, moss * 0.4)

    return _finish("FO_SteppingStones", bm, collections, regions,
                   lambda co, n, i: (0.3 * (1.0 - smoothstep(-0.05, 0.03, co.z)), 0.0))


# ---------------- wood props ----------------
def _bark_regions(axis_fn=None, moss_side=Vector((0.0, -0.4, 1.0)), cut=None, pale=None):
    moss_dir = moss_side.normalized()

    def regions(co, n, i):
        if cut is not None and i in cut:
            return cut[i]
        if pale is not None and i in pale:
            return (0.0, 0.0, 0.0, pale[i])
        angle = math.atan2(co.y, co.x) if axis_fn is None else axis_fn(co)
        ridges = smoothstep(0.45, 0.95, math.sin(angle * 9.0 + co.z * 4.0 + co.x * 3.0)) * 0.55
        moss = smoothstep(0.25, 0.7, n.dot(moss_dir)) * smoothstep(-0.4, 0.2, fnoise(1801, co.x * 3.0, co.y * 3.0, co.z * 3.0))
        return (moss, ridges * (1.0 - moss), 0.0, 0.0)

    return regions


def build_fo_stump(collections):
    """FO_Stump: an old cut stump, root flares into the floor, a slanted cut with growth rings, moss on its north side."""
    bm = bmesh.new()
    sides = 12
    profile = [(-0.03, 0.42), (0.07, 0.34), (0.2, 0.3), (0.36, 0.285), (0.5, 0.28)]
    rings = []
    for k, (z, r) in enumerate(profile):
        ring = []
        for j in range(sides):
            a = j / sides * math.tau
            flare = 1.0 + (0.6 if k == 0 else 0.25 if k == 1 else 0.0) * max(0.0, math.cos(a * 5.0 + 0.4)) ** 2
            wob = 1.0 + 0.04 * math.sin(a * 3.0 + k)
            zz = z + (0.035 * math.sin(a + 0.6) if k == len(profile) - 1 else 0.0)
            ring.append(bm.verts.new((math.cos(a) * r * flare * wob, math.sin(a) * r * flare * wob, zz)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for j in range(sides):
            n = (j + 1) % sides
            bm.faces.new((a[j], a[n], b[n], b[j]))
    # Cut face: its own vertices (bark rim, two growth rings, heart), colored apart from the bark.
    top = rings[-1]
    cut = {}
    first = len(bm.verts)
    rim = [bm.verts.new(v.co.copy()) for v in top]
    mid = [bm.verts.new(Vector((v.co.x * 0.82, v.co.y * 0.82, v.co.z - 0.008))) for v in top]
    inner = [bm.verts.new(Vector((v.co.x * 0.45, v.co.y * 0.45, v.co.z - 0.012))) for v in top]
    heart = bm.verts.new(Vector((0.0, 0.0, profile[-1][0] - 0.01)))
    for j in range(sides):
        n = (j + 1) % sides
        bm.faces.new((rim[j], rim[n], mid[n], mid[j]))
        bm.faces.new((mid[j], mid[n], inner[n], inner[j]))
        bm.faces.new((inner[j], inner[n], heart))
    bm.verts.index_update()
    for v in rim:
        cut[v.index] = (0.0, 0.0, 1.0, 0.35)
    for v in mid:
        cut[v.index] = (0.0, 0.0, 1.0, 0.9)
    for v in inner:
        cut[v.index] = (0.0, 0.0, 1.0, 0.45)
    cut[heart.index] = (0.0, 0.0, 1.0, 0.85)
    # Three roots running out along the floor.
    for a in (0.4, 2.5, 4.4):
        d = Vector((math.cos(a), math.sin(a), 0.0))
        _tube(bm, [d * 0.3 + Vector((0, 0, 0.1)), d * 0.58 + Vector((0, 0, 0.03)), d * 0.8 + Vector((0, 0, -0.07))],
              [0.09, 0.068, 0.05], 6, cap_top=True)
    bm.normal_update()
    obj = object_from_bmesh("FO_Stump", bm, collections)
    mark_sharp_by_angle(obj, 50.0)

    def occlusion(co, n, i):
        if i in cut:
            return (0.08, 0.0)
        return (0.55 * (1.0 - smoothstep(0.0, 0.16, co.z)), 0.0)

    write_layers(obj, _bark_regions(cut=cut), occlusion)
    return obj


def build_hollow_log(collections):
    """FO_HollowLog: a fallen hollow log, open ends with pale wood rims, dark inside, moss along its back, a branch stub."""
    bm = bmesh.new()
    sides, length, outer, inner = 10, 1.7, 0.3, 0.2
    xs = [-length * 0.5 + length * t for t in (0.0, 0.22, 0.5, 0.78, 1.0)]

    def axis(x):
        t = (x + length * 0.5) / length
        return Vector((x, 0.06 * math.sin(t * math.pi), outer * 0.9 + 0.02 * math.sin(t * math.pi * 2.0)))

    def ring(x, radius, k):
        c = axis(x)
        out = []
        for j in range(sides):
            a = j / sides * math.tau + 0.15
            wob = 1.0 + 0.05 * math.sin(a * 3.0 + k * 1.7)
            p = c + Vector((0.0, math.cos(a), math.sin(a))) * radius * wob
            if radius == outer:
                p.z = max(p.z, 0.025)  # a flattened belly resting on the floor
            out.append(p)
        return out

    outer_rings = [[bm.verts.new(p) for p in ring(x, outer * (1.0 - 0.06 * abs(x)), k)] for k, x in enumerate(xs)]
    inner_rings = [[bm.verts.new(p) for p in ring(x, inner * (1.0 - 0.06 * abs(x)), k)] for k, x in enumerate(xs)]
    for a, b in zip(outer_rings, outer_rings[1:]):
        for j in range(sides):
            n = (j + 1) % sides
            bm.faces.new((a[j], a[n], b[n], b[j]))
    for a, b in zip(inner_rings, inner_rings[1:]):
        for j in range(sides):
            n = (j + 1) % sides
            bm.faces.new((b[j], b[n], a[n], a[j]))
    bm.verts.index_update()
    inside = {v.index for r in inner_rings for v in r}
    pale = {}
    # Open ends: annulus with their own vertices (pale wood with an amber ring).
    for end, sign in ((0, -1.0), (len(xs) - 1, 1.0)):
        o = [bm.verts.new(v.co.copy()) for v in outer_rings[end]]
        m = [bm.verts.new(outer_rings[end][j].co.lerp(inner_rings[end][j].co, 0.5) + Vector((sign * 0.004, 0, 0))) for j in range(sides)]
        i_ = [bm.verts.new(v.co.copy()) for v in inner_rings[end]]
        for j in range(sides):
            n = (j + 1) % sides
            quads = ((o[j], o[n], m[n], m[j]), (m[j], m[n], i_[n], i_[j]))
            for q in quads:
                bm.faces.new(q if sign > 0 else tuple(reversed(q)))
        bm.verts.index_update()
        for v in o + i_:
            pale[v.index] = 0.9
        for v in m:
            pale[v.index] = 0.45
    _tube(bm, [axis(0.25) + Vector((0, -0.05, 0.22)), axis(0.33) + Vector((0, -0.12, 0.38))], [0.06, 0.04], 6, cap_top=True)
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = object_from_bmesh("FO_HollowLog", bm, collections)
    mark_sharp_by_angle(obj, 50.0)
    base = _bark_regions(axis_fn=lambda co: math.atan2(co.z - outer * 0.9, co.y))
    # Amber growth ring between the pale rims: B under A.
    cut = {i: (0.0, 0.0, 1.0, a) for i, a in pale.items()}

    def regions(co, n, i):
        if i in cut:
            return cut[i]
        if i in inside:
            return (0.0, 0.0, 0.0, 0.0)
        return base(co, n, i)

    def occlusion(co, n, i):
        if i in inside:
            return (0.75, 0.0)
        return (0.5 * (1.0 - smoothstep(0.0, 0.14, co.z)), 0.0)

    write_layers(obj, regions, occlusion)
    return obj


def build_root_arch(collections):
    """FO_RootArch: a big old root arching out of the floor and back in, a thinner strand along it, flared feet."""
    bm = bmesh.new()
    main = catmull([(-0.95, -0.07), (-0.72, 0.3), (-0.3, 0.6), (0.2, 0.66), (0.62, 0.4), (0.93, -0.07)], 0.16)
    pts = [Vector((p.x, 0.04 * math.sin(p.x * 3.0), p.y)) for p in main]
    n = len(pts)
    radii = [0.15 - 0.05 * math.sin(math.pi * k / (n - 1)) + 0.02 * math.sin(k * 1.3) for k in range(n)]
    _tube(bm, pts, radii, 8, cap_top=False)
    strand = [p + Vector((0.0, 0.12, -0.02)) for p in pts[1:-1:2]]
    _tube(bm, strand, [0.055 - 0.02 * math.sin(math.pi * k / max(1, len(strand) - 1)) for k in range(len(strand))], 5,
          cap_top=True)
    for foot, d in ((pts[0], -1.0), (pts[-1], 1.0)):
        for side in (-1.0, 1.0):
            a = Vector((d * 0.6, side * 0.8, 0.0)).normalized()
            base = Vector((foot.x, foot.y, 0.06))
            _tube(bm, [base, base + a * 0.24 + Vector((0, 0, -0.03)), base + a * 0.4 + Vector((0, 0, -0.12))],
                  [0.08, 0.06, 0.045], 5, cap_top=True)
    bm.normal_update()
    obj = object_from_bmesh("FO_RootArch", bm, collections)
    mark_sharp_by_angle(obj, 55.0)
    top = max(v.co.z for v in obj.data.vertices)

    write_layers(obj, _bark_regions(axis_fn=lambda co: math.atan2(co.z, co.y) + co.x * 2.0, moss_side=Vector((0, -0.2, 1.0))),
                 lambda co, n, i: (0.5 * (1.0 - smoothstep(0.0, 0.15, co.z)) + 0.25 * smoothstep(0.0, -0.7, n.z), co.z / top))
    return obj


def build_tiny_bridge(collections):
    """FO_TinyBridge: a little arched plank bridge for the brook crossing. Local X along the path (span 2.1 m), pivot at
    the middle of the span on the bank level; both ends rest on the banks."""
    bm = bmesh.new()
    span, width, rise = 2.1, 0.78, 0.22

    def deck_z(x):
        return rise * (1.0 - (2.0 * x / span) ** 2)

    stringer_x = [-span * 0.5 + span * t / 6 for t in range(7)]
    for y in (-0.3, 0.3):
        _tube(bm, [Vector((x, y, deck_z(x) + 0.02 - (0.05 if abs(x) > span * 0.45 else 0.0))) for x in stringer_x],
              [0.065] * len(stringer_x), 6, cap_top=True)
    plank_start = len(bm.verts)
    planks = 7
    for k in range(planks):
        x = -span * 0.42 + k * (span * 0.84 / (planks - 1))
        z = deck_z(x) + 0.095
        slope = math.atan(-8.0 * rise * x / (span * span))
        first = len(bm.verts)
        _box(bm, Vector((0.0, 0.0, 0.0)), Vector((0.235, width + 0.04 * math.sin(k * 2.1), 0.04)))
        bm.verts.ensure_lookup_table()
        rot = Matrix.Rotation(-slope, 3, 'Y')
        for idx in range(first, len(bm.verts)):
            v = bm.verts[idx]
            v.co = rot @ v.co + Vector((x, 0.012 * math.sin(k * 1.7), z))
    plank_end = len(bm.verts)
    for x in (-0.78, 0.78):
        for y in (-0.36, 0.36):
            _box(bm, Vector((x, y, deck_z(x) + 0.24)), Vector((0.06, 0.06, 0.42)))
    rail_start = len(bm.verts)
    for y in (-0.36, 0.36):
        _tube(bm, [Vector((x, y, deck_z(x) + 0.42)) for x in (-0.8, -0.4, 0.0, 0.4, 0.8)], [0.032] * 5, 5, cap_top=True)
    bm.normal_update()
    obj = object_from_bmesh("FO_TinyBridge", bm, collections, smooth=False)
    mark_sharp_by_angle(obj, 40.0)

    def regions(co, n, i):
        if plank_start <= i < plank_end:
            return (0.0, 0.0, 0.0, 0.85 if n.z > 0.5 else 0.55)
        if i >= rail_start:
            return (0.0, 0.7, 0.0, 0.0)
        moss = smoothstep(0.4, 0.9, n.z) * (1.0 - smoothstep(0.02, 0.12, co.z))
        return (moss, 0.3 * smoothstep(0.5, 0.95, math.sin(co.x * 21.0)), 0.0, 0.0)

    write_layers(obj, regions, lambda co, n, i: (0.4 * (1.0 - smoothstep(-0.02, 0.1, co.z)), 0.0))
    return obj


def build_forest_props():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    out = [build_moss_rock(k, target) for k in MOSS_ROCKS]
    out += [build_fo_stump(target), build_hollow_log(target), build_root_arch(target), build_tiny_bridge(target),
            build_stepping_stones(target)]
    return {o.name: triangle_count(o) for o in out}


# ---------------- scatter ----------------
def _leaf_blade(bm, base, direction, length, width, lobes, curl, two_sided=True):
    """Leaf lying on the floor: a vein from `base` along `direction`, lobed edges, sides curling up a little.
    Returns the vein vertex positions (for the paint)."""
    d = Vector((direction.x, direction.y, 0.0)).normalized()
    side = Vector((-d.y, d.x, 0.0))
    steps = 5
    vein, left, right = [], [], []
    for k in range(steps + 1):
        t = k / steps
        w = width * 0.5 * math.sin(math.pi * min(1.0, t * 1.05)) ** 0.8 * (1.0 + (0.3 * math.cos(t * lobes * math.tau) if lobes else 0.0))
        c = base + d * length * t + Vector((0, 0, 0.004 + curl * 0.5 * t))
        vein.append(c)
        left.append(c + side * w + Vector((0, 0, curl)))
        right.append(c - side * w + Vector((0, 0, curl)))
    for flip in ((False, True) if two_sided else (False,)):
        off = Vector((0, 0, -0.002)) if flip else Vector((0, 0, 0))
        vv = [bm.verts.new(p + off) for p in vein]
        lv = [bm.verts.new(p + off) for p in left]
        rv = [bm.verts.new(p + off) for p in right]
        for k in range(steps):
            for quad in ((vv[k], rv[k], rv[k + 1], vv[k + 1]), (vv[k], vv[k + 1], lv[k + 1], lv[k])):
                bm.faces.new(tuple(reversed(quad)) if flip else quad)
    return vein


def build_fo_leaf(kind, collections):
    bm = bmesh.new()
    veins = []
    if kind == "A":  # an oak-like lobed leaf, amber with a brown vein
        veins += _leaf_blade(bm, Vector((0.0, -0.05, 0.0)), Vector((0.1, 1.0, 0.0)), 0.1, 0.055, 3, 0.008)
    else:  # two round birch-like leaves, one fresh, one turning amber
        veins += _leaf_blade(bm, Vector((-0.03, -0.02, 0.0)), Vector((-0.6, 1.0, 0.0)), 0.065, 0.05, 0, 0.006)
        veins += _leaf_blade(bm, Vector((0.01, -0.03, 0.0)), Vector((0.9, 0.5, 0.0)), 0.06, 0.046, 0, 0.007)
    bm.normal_update()

    def regions(co, n, i):
        on_vein = min((Vector((co.x - p.x, co.y - p.y, 0.0))).length for p in veins) < 0.003
        if kind == "A":
            return (0.0, 0.0, 0.0 if on_vein else 1.0, 0.0)
        fresh = 1.0 if co.x < -0.005 else 0.0
        return (0.0, 0.0, 0.0 if on_vein else 1.0 - 0.6 * fresh, 0.0 if on_vein else fresh * 0.9)

    return _finish(f"FO_Leaf_{kind}", bm, collections, regions, lambda co, n, i: (0.15, 0.0), up_normals=0.5)


def build_fo_twig(collections):
    bm = bmesh.new()
    main = [Vector((-0.13, 0.0, 0.012)), Vector((-0.04, 0.012, 0.014)), Vector((0.05, -0.006, 0.012)), Vector((0.13, 0.01, 0.01))]
    _tube(bm, main, [0.009, 0.008, 0.007, 0.005], 4, cap_top=True)
    for start, end, r in ((main[1], Vector((0.0, 0.07, 0.016)), 0.005), (main[2], Vector((0.1, -0.06, 0.012)), 0.004)):
        _tube(bm, [start, start.lerp(end, 0.5) + Vector((0, 0, 0.004)), end], [r, r * 0.8, r * 0.5], 4, cap_top=True)
    bm.normal_update()
    tips = [main[0], main[-1]]

    def regions(co, n, i):
        tip = 1.0 if any((co - t).length < 0.012 for t in tips) else 0.0
        return (0.0, smoothstep(0.3, 0.9, n.z) * 0.6, 0.0, tip * 0.8)

    return _finish("FO_Twig", bm, collections, regions, lambda co, n, i: (0.3 * (1.0 - smoothstep(0.0, 0.012, co.z)), 0.0))


def _lathe_x(bm, profile, sides, center, roll=0.0, jag=None):
    """Closed lathe around the local X axis: profile = [(x, radius)], tips where radius is 0."""
    rings = []
    for k, (x, r) in enumerate(profile):
        if r <= 0.0:
            rings.append(bm.verts.new(center + Vector((x, 0.0, 0.0))))
            continue
        ring = []
        for j in range(sides):
            a = j / sides * math.tau + roll + (math.pi / sides if (jag and k % 2) else 0.0)
            rr = r * (1.0 + (jag if jag and (j + k) % 2 == 0 else 0.0))
            ring.append(bm.verts.new(center + Vector((x, math.cos(a) * rr, math.sin(a) * rr))))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if isinstance(a, list) and isinstance(b, list):
            for j in range(sides):
                n = (j + 1) % sides
                bm.faces.new((a[j], b[j], b[n], a[n]))
        elif isinstance(b, list):
            for j in range(sides):
                bm.faces.new((a, b[j], b[(j + 1) % sides]))
        else:
            for j in range(sides):
                bm.faces.new((a[(j + 1) % sides], a[j], b))
    return rings


def build_fo_acorn(collections):
    """An acorn lying on its side with its cap, and a loose cap beside it (4.5 cm, a little larger than life)."""
    bm = bmesh.new()
    r = 0.014
    nut = _lathe_x(bm, [(-0.024, 0.0), (-0.02, r * 0.55), (-0.01, r * 0.95), (0.004, r), (0.012, r * 0.9)], 7,
                   Vector((0.0, 0.0, r)))
    cap_start = len(bm.verts)
    _lathe_x(bm, [(0.008, r * 1.12), (0.015, r * 1.05), (0.021, r * 0.6), (0.023, 0.0)], 7, Vector((0.0, 0.0, r)))
    _tube(bm, [Vector((0.022, 0.0, r)), Vector((0.03, 0.002, r + 0.002))], [0.002, 0.0015], 3, cap_top=True)
    loose_start = len(bm.verts)
    cap2 = Vector((0.03, 0.035, 0.0))
    ring_a = [bm.verts.new(cap2 + Vector((math.cos(j / 7 * math.tau) * 0.014, math.sin(j / 7 * math.tau) * 0.014, 0.009))) for j in range(7)]
    ring_b = [bm.verts.new(cap2 + Vector((math.cos(j / 7 * math.tau) * 0.01, math.sin(j / 7 * math.tau) * 0.01, 0.002))) for j in range(7)]
    tip = bm.verts.new(cap2 + Vector((0.0, 0.0, 0.0)))
    inner = bm.verts.new(cap2 + Vector((0.0, 0.0, 0.006)))
    for j in range(7):
        n = (j + 1) % 7
        bm.faces.new((ring_a[j], ring_b[j], ring_b[n], ring_a[n]))
        bm.faces.new((ring_b[n], ring_b[j], tip))
        bm.faces.new((ring_a[n], inner, ring_a[j]))
    bm.normal_update()

    def regions(co, n, i):
        if i >= cap_start:
            return (0.0, 0.55 if (i % 2 == 0) else 0.2, 0.0, 0.0)
        tip_dark = 1.0 - smoothstep(-0.024, -0.016, co.x)
        return (0.0, 0.0, 0.7 * (1.0 - tip_dark), 0.45 * (1.0 - tip_dark) * smoothstep(0.0, 0.8, n.z))

    return _finish("FO_Acorn", bm, collections, regions, lambda co, n, i: (0.35 * (1.0 - smoothstep(0.0, 0.01, co.z)), 0.0))


def build_fo_pinecone(collections):
    """A small pine cone lying on the floor: staggered scale rings, pale scale tips."""
    bm = bmesh.new()
    r = 0.022
    prof = [(-0.036, 0.0), (-0.03, r * 0.55), (-0.018, r * 0.92), (-0.004, r), (0.01, r * 0.9), (0.022, r * 0.68),
            (0.031, r * 0.38), (0.036, 0.0)]
    _lathe_x(bm, prof, 6, Vector((0.0, 0.0, r * 0.95)), roll=0.2, jag=0.32)
    _tube(bm, [Vector((-0.036, 0.0, r * 0.95)), Vector((-0.045, 0.002, r * 0.8))], [0.004, 0.003], 3, cap_top=True)
    bm.normal_update()
    axis_z = r * 0.95

    def regions(co, n, i):
        # Scale tips are the vertices pushed out by the stagger (jag): radius above the profile's own radius there.
        radial = math.hypot(co.y, co.z - axis_z)
        local = max((rr for x, rr in prof if abs(x - co.x) < 0.004), default=0.0)
        tip = smoothstep(1.08, 1.18, radial / local) if local > 0.0 else 0.0
        return (0.0, 0.35, 0.25 * tip, tip * 0.85)

    return _finish("FO_PineCone", bm, collections, regions,
                   lambda co, n, i: (0.4 * (1.0 - smoothstep(0.0, 0.014, co.z)) + 0.15 * smoothstep(0.2, -0.8, n.z), 0.0))


def build_fo_mosstuft(collections):
    """A soft moss clump: three low cushions and three tiny stalks with round capsules (they sway a little)."""
    import random
    bm = bmesh.new()
    rng = random.Random(1901)
    cushions = []
    for cx, cy, rad, h in ((0.0, 0.0, 0.07, 0.036), (0.06, 0.035, 0.05, 0.028), (-0.05, 0.04, 0.045, 0.024)):
        sides = 7
        cushions.append((len(bm.verts), len(bm.verts) + 2 * sides + 1, Vector((cx, cy, -h * 0.9))))
        ring1 = [bm.verts.new((cx + math.cos(j / sides * math.tau) * rad * rng.uniform(0.9, 1.1),
                               cy + math.sin(j / sides * math.tau) * rad * rng.uniform(0.9, 1.1), -0.004)) for j in range(sides)]
        ring2 = [bm.verts.new((cx + math.cos((j + 0.5) / sides * math.tau) * rad * 0.7,
                               cy + math.sin((j + 0.5) / sides * math.tau) * rad * 0.7, h * 0.75)) for j in range(sides)]
        top = bm.verts.new((cx, cy, h))
        for j in range(sides):
            n = (j + 1) % sides
            bm.faces.new((ring1[j], ring1[n], ring2[j]))
            bm.faces.new((ring1[n], ring2[n], ring2[j]))
            bm.faces.new((ring2[j], ring2[n], top))
    stalk_start = len(bm.verts)
    heads = []
    for a, h in ((0.6, 0.085), (2.6, 0.07), (4.4, 0.078)):
        base = Vector((math.cos(a) * 0.025, math.sin(a) * 0.025, 0.02))
        head = base + Vector((math.cos(a) * 0.012, math.sin(a) * 0.012, h - 0.02))
        _prism(bm, base, head, 0.0022, 3)
        _bipyramid(bm, head + Vector((0, 0, 0.005)), 0.0045, 0.014, 4, a)
        heads.append(head + Vector((0, 0, 0.005)))
    bm.normal_update()

    def regions(co, n, i):
        if i >= stalk_start:
            cap = 1.0 if any((co - h).length < 0.009 for h in heads) else 0.0
            return (0.0, 0.0, 1.0 - cap, cap)
        return (smoothstep(0.012, -0.004, co.z), smoothstep(0.012, 0.034, co.z), 0.0, 0.0)

    def occlusion(co, n, i):
        if i >= stalk_start:
            return (0.0, min(1.0, co.z / 0.09))
        return (0.4 * (1.0 - smoothstep(0.0, 0.02, co.z)), 0.0)

    obj = _finish("FO_MossTuft", bm, collections, regions, occlusion, up_normals=0.6)
    me = obj.data
    normals = []
    for v in me.vertices:
        c = next((c for a, b, c in cushions if a <= v.index < b), None)
        normals.append((v.co - c).normalized().lerp(Vector((0, 0, 1)), 0.25).normalized() if c is not None
                       else v.normal.lerp(Vector((0, 0, 1)), 0.6).normalized())
    me.normals_split_custom_set_from_vertices(normals)
    return obj


def build_fo_clover(collections):
    """Wood sorrel: three trefoils of heart-shaped leaflets on short stalks, folded a little along the midrib."""
    import random
    bm = bmesh.new()
    rng = random.Random(1951)
    for k in range(3):
        a0 = k / 3 * math.tau + rng.uniform(-0.3, 0.3)
        center = Vector((math.cos(a0) * 0.045, math.sin(a0) * 0.045, 0.045 + 0.012 * k))
        _prism(bm, Vector((0, 0, 0)), center, 0.0025, 3)
        for j in range(3):
            a = a0 + j / 3 * math.tau + 0.5
            out = Vector((math.cos(a), math.sin(a), 0.0))
            side = Vector((-out.y, out.x, 0.0))
            w = 0.03
            pts = [center, center + out * w * 0.45 + side * w * 0.5 + Vector((0, 0, 0.004)),
                   center + out * w + side * w * 0.42 + Vector((0, 0, 0.002)), center + out * w * 0.82 + Vector((0, 0, -0.003)),
                   center + out * w - side * w * 0.42 + Vector((0, 0, 0.002)), center + out * w * 0.45 - side * w * 0.5 + Vector((0, 0, 0.004))]
            vs = [bm.verts.new(p) for p in pts]
            for f in range(1, len(vs) - 1):
                bm.faces.new((vs[0], vs[f + 1], vs[f]))  # counter-clockwise from above: the leaflets face up
    bm.normal_update()

    def regions(co, n, i):
        leaf = 1.0 if co.z > 0.038 else 0.0
        r = math.hypot(co.x, co.y)
        return (0.0, leaf * smoothstep(0.03, 0.07, r), 0.0 if leaf else 0.8, leaf * (1.0 - smoothstep(0.035, 0.05, r)) * 0.6)

    return _finish("FO_Clover", bm, collections, regions,
                   lambda co, n, i: (0.4 * (1.0 - smoothstep(0.0, 0.03, co.z)), min(1.0, co.z / 0.07)), up_normals=0.8)


def build_forest_scatter():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    out = [build_fo_leaf("A", target), build_fo_leaf("B", target), build_fo_twig(target), build_fo_acorn(target),
           build_fo_pinecone(target), build_fo_mosstuft(target), build_fo_clover(target)]
    return {o.name: triangle_count(o) for o in out}


# ---------------- export ----------------
FBX_SETS = {
    "Assets/_Game/Art/Environment/Forest/Models/FO_Grounds.fbx": ("FO_Ground_",),
    "Assets/_Game/Art/Environment/Forest/Models/FO_Water.fbx": ("FO_Brook_Water",),
    "Assets/_Game/Art/Environment/Forest/Models/FO_Props.fbx": ("FO_MossRock_", "FO_Stump", "FO_HollowLog", "FO_RootArch",
                                                               "FO_TinyBridge", "FO_SteppingStones"),
    "Assets/_Game/Art/Environment/Forest/Models/FO_Scatter.fbx": ("FO_Leaf_", "FO_Twig", "FO_Acorn", "FO_PineCone",
                                                                 "FO_MossTuft", "FO_Clover"),
}
GL_T33B_GROUNDS = ("GL_Ground_StreamBend", "GL_Ground_Orchard", "GL_Ground_Hill", "GL_Ground_WindmillRise",
                   "GL_Ground_PondEdge", "GL_Ground_ForestEdge")


def export_forest():
    return {path: export_static(path, prefixes)[1] for path, prefixes in FBX_SETS.items()}


# ---------------- checks ----------------
def _append_objects(blend, names):
    with bpy.data.libraries.load(blend, link=False) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects if n in names]
    return {o.name: o for o in data_to.objects if o is not None}


def _final_colors(obj, origin, pal):
    """Final ground color (linear) per vertex as SH_GroundMaster mixes it, with the world macro variation."""
    import numpy as np
    me = obj.data
    attr = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Occlusion"]
    occ = [0.0] * len(me.vertices)
    for loop in me.loops:
        occ[loop.vertex_index] = uv.data[loop.index].uv[0]
    out = {}
    for v in me.vertices:
        wx, wz = origin - v.co.x, -v.co.y
        out[v.index] = ground_mix(np.array(list(attr.data[v.index].color) + [occ[v.index]]), pal, macro_at(wx, wz))
    return out


def _side(obj, x_side):
    return [v for v in obj.data.vertices if abs(v.co.x - x_side) < 1e-4]


def seam_report(grass_blend):
    """Every seam from ForestEdge (x 105) to LakeTransition: vertex positions, flatness, final colors (sRGB 0-255)."""
    gl = _append_objects(grass_blend, {"GL_Ground_ForestEdge"})
    chain = [("GL_Ground_ForestEdge", gl["GL_Ground_ForestEdge"], STRIP["GL_Ground_ForestEdge"][0], "gl.ground")]
    chain += [(n, bpy.data.objects[n], FOREST[n][0], "fo.ground") for n in FOREST]
    lines = []
    for (wn, wo, worig, wpal), (en, eo, eorig, epal) in zip(chain, chain[1:]):
        west = _side(wo, -GROUND_HALF)
        east = _side(eo, GROUND_HALF)
        key = lambda v: (round(-v.co.y, 4), round(v.co.z, 4))
        wmap = {key(v): v for v in west}
        emap = {key(v): v for v in east}
        shared = sorted(set(wmap) & set(emap))
        wc = _final_colors(wo, worig, palette(wpal))
        ec = _final_colors(eo, eorig, palette(epal))
        top_diff, wall_diff, top_z = 0.0, 0.0, 0.0
        for k in shared:
            a, b = to_srgb255(wc[wmap[k].index]), to_srgb255(ec[emap[k].index])
            d = max(abs(x - y) for x, y in zip(a, b))
            if k[1] > -0.1:
                top_diff = max(top_diff, d)
                top_z = max(top_z, abs(k[1]))
            else:
                wall_diff = max(wall_diff, d)
        pos = max((abs((worig - wmap[k].co.x) - (eorig - emap[k].co.x)) for k in shared), default=0.0)
        lines.append(f"[T50B] seam {wn} | {en} at x {worig + GROUND_HALF:.0f}: {len(shared)} shared vertices "
                     f"({len(west)} west, {len(east)} east), max x gap {pos:.6f} m, max |z| on top {top_z:.5f} m, "
                     f"max color diff top {top_diff:.2f}, walls {wall_diff:.2f} (sRGB 0-255)")
    for line in lines:
        print(line)
    return lines


def fbx_vertex_diff(old_path, new_path):
    """Imports two FBX files and compares the vertex positions of every mesh object they share (exact)."""
    def load(path, tag):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        objs = {}
        for o in set(bpy.data.objects) - before:
            if o.type == 'MESH':
                objs[o.name.split(".")[0]] = [tuple(v.co) for v in o.data.vertices]
                o.name = tag + o.name
        return objs
    old, new = load(old_path, "OLD_"), load(new_path, "NEW_")
    for name in sorted(set(old) | set(new)):
        a, b = old.get(name), new.get(name)
        if a is None or b is None:
            print(f"[T50B] fbx {name}: only in {'new' if a is None else 'old'}")
            continue
        same = a == b
        moved = sum(1 for p, q in zip(a, b) if p != q) if len(a) == len(b) else -1
        west_same = len(a) == len(b) and all(p == q for p, q in zip(a, b) if p[0] > 1e-4)
        print(f"[T50B] fbx {name}: {len(a)} -> {len(b)} vertices, identical {same}, moved {moved}, "
              f"Unity-west half identical {west_same}")


# ---------------- review sheet (never saved into a blend) ----------------
PREVIEW_PALETTE = {"FO_MossRock": "fo.rock", "FO_SteppingStones": "fo.rock", "FO_Leaf": "fo.tree", "FO_MossTuft": "fo.fern",
                   "FO_Clover": "fo.fern"}


def _preview_paint(obj, key, origin=None):
    """WORK_Preview = the color the master shaders compute before lighting (ground: with the world macro variation)."""
    import numpy as np
    pal = palette(key)
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Occlusion"]
    occ = [0.0] * len(me.vertices)
    for loop in me.loops:
        occ[loop.vertex_index] = uv.data[loop.index].uv[0]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for v in me.vertices:
        r, g, b, a = regions.data[v.index].color
        if key == "fo.water":
            c = pal[0] + (pal[3] - pal[0]) * (g * 0.75)
            c = c + (pal[4] - c) * min(1.0, r * 0.85 + a * 0.5)
        else:
            m = macro_at(origin - v.co.x, -v.co.y) if origin is not None else 0.0
            c = ground_mix(np.array([r, g, b, a, occ[v.index]]), pal, m)
        prev.data[v.index].color = (float(c[0]), float(c[1]), float(c[2]), 1.0)
    me.color_attributes.active_color = prev
    names = [a.name for a in me.color_attributes]
    me.color_attributes.render_color_index = names.index("WORK_Preview")


def _look(cam, target, offset):
    cam.location = Vector(target) + Vector(offset)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()


def _shot(scene, cam, path, res, target, offset, ortho=None, lens=35.0):
    scene.render.resolution_x, scene.render.resolution_y = res
    _look(cam, target, offset)
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'
        cam.data.lens = lens
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def _place(name, location, rot_z=0.0):
    obj = bpy.data.objects[name]
    if obj.name not in bpy.context.scene.collection.objects and not obj.users_collection:
        bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (0.0, 0.0, rot_z)
    obj.hide_render = False
    obj.hide_set(False)
    return obj


def render_sheet(grass_blend, out_path):
    """Strip overview (Grassland PondEdge and ForestEdge, the seven forest grounds, the brook), the x 105 seam, the brook
    crossing, the props and the scatter, composed into one PNG (rows: strip west, strip east, seam + brook, props,
    scatter)."""
    import numpy as np
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'VERTEX'
    shading.show_shadows = True
    shading.shadow_intensity = 0.45
    shading.show_cavity = True
    shading.cavity_type = 'WORLD'
    scene.display.light_direction = (0.45, 0.55, 0.75)
    scene.view_settings.view_transform = 'Standard'
    world = scene.world or bpy.data.worlds.new("WORK_World")
    scene.world = world
    world.color = (0.80, 0.86, 0.84)
    gl = _append_objects(grass_blend, {"GL_Ground_ForestEdge", "GL_Ground_PondEdge", "GL_Stream_B"})
    for o in list(scene.objects):
        o.hide_render = True
    for name, origin in (("GL_Ground_PondEdge", 84.0), ("GL_Ground_ForestEdge", 98.0)):
        _preview_paint(gl[name], "gl.ground", origin)
        _place(name, (-origin, 0.0, 0.0))
    _preview_paint(gl["GL_Stream_B"], "fo.water")
    _place("GL_Stream_B", (-84.0, 0.0, 0.0))
    for name, (origin, _, _, _) in FOREST.items():
        _preview_paint(bpy.data.objects[name], "fo.ground", origin)
        _place(name, (-origin, 0.0, 0.0))
    _preview_paint(bpy.data.objects["FO_Brook_Water"], "fo.water")
    _place("FO_Brook_Water", (-BROOK_ORIGIN, 0.0, 0.0))
    for o in bpy.data.collections["EXPORT"].all_objects:
        if not o.name.startswith(("FO_Ground_", "FO_Brook_Water")):
            key = next((v for k, v in PREVIEW_PALETTE.items() if o.name.startswith(k)), "fo.log")
            _preview_paint(o, key)
    # Props where the layouts will likely put them, to judge the floor around them.
    bx, bz, yaw, _ = bridge_spot()
    _place("FO_TinyBridge", (-(BROOK_ORIGIN + bx), -bz, 0.0), math.radians(-yaw))
    _place("FO_SteppingStones", (-(BROOK_ORIGIN + 0.8), 3.95, -0.05), math.radians(80.0))
    _place("FO_MossRock_Hero", (-(GIANT_STUMP_SPOT[0] + 2.6), -GIANT_STUMP_SPOT[1] + 0.3, 0.0), 0.4)
    _place("FO_MossRock_L", (-(BROOK_ORIGIN + 2.6), -5.2, 0.0), 1.1)
    _place("FO_MossRock_M", (-(BROOK_ORIGIN - 2.2), 5.6, 0.0), 2.0)
    _place("FO_Stump", (-MUSHROOM_SPOT[0], -MUSHROOM_SPOT[1], 0.0), 0.3)
    _place("FO_HollowLog", (-(126.0 - 2.4), 4.0, 0.0), 0.35)
    _place("FO_RootArch", (-(126.0 + 3.2), -3.0, 0.15), -0.3)
    _place("FO_MossRock_S", (-(140.0 - 3.6), 2.2, 0.0), 0.0)

    cam = bpy.data.objects.new("WORK_Camera", bpy.data.cameras.new("WORK_Camera"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.clip_end = 600.0
    folder = os.path.join(os.environ.get("TEMP", os.path.dirname(out_path)), "t50b_parts")
    os.makedirs(folder, exist_ok=True)
    parts = [
        _shot(scene, cam, os.path.join(folder, "a.png"), (2400, 560), (-112.0, -0.6, -0.3), (0.0, 62.0, 70.0), ortho=71.0),
        _shot(scene, cam, os.path.join(folder, "b.png"), (2400, 560), (-168.0, -0.6, -0.3), (0.0, 62.0, 70.0), ortho=71.0),
        _shot(scene, cam, os.path.join(folder, "c.png"), (1200, 760), (-105.0, -0.8, 0.0), (0.0, 11.5, 9.5), lens=30.0),
        _shot(scene, cam, os.path.join(folder, "d.png"), (1200, 760), (-167.6, -0.4, 0.0), (0.0, 10.5, 8.0), lens=30.0),
        _shot(scene, cam, os.path.join(folder, "g.png"), (800, 540), (-140.0, -1.0, 0.0), (0.0, 12.0, 8.0), lens=30.0),
        _shot(scene, cam, os.path.join(folder, "h.png"), (800, 540), (-154.0, -1.0, -0.1), (0.0, 12.0, 8.0), lens=30.0),
        _shot(scene, cam, os.path.join(folder, "i.png"), (800, 540), (-182.0, -1.0, 0.0), (0.0, 12.0, 8.0), lens=30.0),
    ]
    # Props and scatter lineups on a neutral floor.
    for o in scene.objects:
        o.hide_render = True
    floor_me = bpy.data.meshes.new("WORK_Floor")
    bmf = bmesh.new()
    bmesh.ops.create_grid(bmf, x_segments=2, y_segments=2, size=60.0)
    bmf.to_mesh(floor_me)
    bmf.free()
    floor = bpy.data.objects.new("WORK_Floor", floor_me)
    scene.collection.objects.link(floor)
    fc = floor_me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for d in fc.data:
        d.color = (0.34, 0.4, 0.24, 1.0)
    floor_me.color_attributes.active_color = fc
    floor.location = (60.0, 0.0, -0.002)

    def lineup(names, x, gap):
        for name in names:
            o = bpy.data.objects[name]
            lo, hi = min(v.co.x for v in o.data.vertices), max(v.co.x for v in o.data.vertices)
            _place(name, (x - lo, 0.0, 0.0), -0.55 if name == "FO_HollowLog" else 0.0)
            x += hi - lo + gap
        return x

    props = ["FO_MossRock_S", "FO_MossRock_M", "FO_MossRock_L", "FO_MossRock_Hero", "FO_Stump", "FO_HollowLog",
             "FO_RootArch", "FO_TinyBridge", "FO_SteppingStones"]
    end = lineup(props, 30.0, 0.45)
    parts.append(_shot(scene, cam, os.path.join(folder, "e.png"), (2400, 620), ((30.0 + end) * 0.5, 0.0, 0.5),
                       (0.0, 9.0, 4.6), ortho=end - 29.4))
    for name in props:
        bpy.data.objects[name].hide_render = True
    scatter = ["FO_Leaf_A", "FO_Leaf_B", "FO_Twig", "FO_Acorn", "FO_PineCone", "FO_MossTuft", "FO_Clover"]
    end = lineup(scatter, 60.0, 0.06)
    parts.append(_shot(scene, cam, os.path.join(folder, "f.png"), (2400, 520), ((60.0 + end) * 0.5, 0.0, 0.03),
                       (0.0, 1.0, 0.8), ortho=end - 59.9))
    imgs = [bpy.data.images.load(p, check_existing=False) for p in parts]
    arrays = [np.array(i.pixels[:], dtype=np.float32).reshape(i.size[1], i.size[0], 4) for i in imgs]
    rows = [[arrays[0]], [arrays[1]], [arrays[2], arrays[3]], [arrays[4], arrays[5], arrays[6]], [arrays[7]], [arrays[8]]]
    gap = 8
    heights = [max(a.shape[0] for a in row) for row in rows]
    total = sum(heights) + gap * (len(rows) + 1)
    sheet = np.ones((total, 2400 + 2 * gap, 4), dtype=np.float32)
    sheet[..., :3] = 0.96
    y = total - gap  # Blender pixel rows run bottom-up: the first row goes on top
    for row, h in zip(rows, heights):
        x0 = gap
        for a in row:
            sheet[y - a.shape[0]:y, x0:x0 + a.shape[1]] = a
            x0 += a.shape[1]
        y -= h + gap
    out = bpy.data.images.new("WORK_Sheet", 2400 + 2 * gap, total, alpha=False)
    out.pixels = sheet.ravel()
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    print("[T50B] sheet", out_path, tuple(out.size))
    return out_path


# ---------------- headless entry ----------------
def _root_from(tools_dir):
    return os.path.abspath(os.path.join(tools_dir, "..", "..", ".."))


def _load_namespace(tools_dir):
    ns = {"__file__": os.path.join(tools_dir, "pw_forest_ground.py")}
    for script in ("pw_grassland.py", "pw_grassland_t33.py", "pw_grassland_t33b.py", "pw_export.py", "pw_forest_ground.py"):
        path = os.path.join(tools_dir, script)
        exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    ns["TOOLS_DIR"] = tools_dir
    return ns


def _cli():
    tools_dir = os.path.dirname(os.path.abspath(__file__))
    root = _root_from(tools_dir)
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    command = args[0] if args else "forest"
    forest_blend = os.path.join(root, "SourceArt", "Blender", "Environment", "Forest", "BL_Biome_Forest_Ground.blend")
    grass_blend = os.path.join(root, "SourceArt", "Blender", "Environment", "Grassland", "BL_Biome_Grassland_Magnifique.blend")
    if command == "forest":
        bpy.ops.wm.read_factory_settings(use_empty=True)
        os.makedirs(os.path.dirname(forest_blend), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=forest_blend)
        ns = _load_namespace(tools_dir)
        report = {}
        report.update(ns["build_forest_grounds"]())
        water = ns["build_brook_water"]()
        report[water.name] = ns["triangle_count"](water)
        report.update(ns["build_forest_props"]())
        report.update(ns["build_forest_scatter"]())
        for o in bpy.data.collections["WORK"].objects:
            o.hide_set(True)
        bpy.ops.wm.save_as_mainfile(filepath=forest_blend)
        exported = ns["export_forest"]()
        for name, tris in report.items():
            print(f"[T50B] {name}: {tris} tris")
        for path, names in exported.items():
            print(f"[T50B] {path}: {len(names)} objects")
        print("[T50B] bridge spot (Brook chunk x, z, yaw, water width):", tuple(round(v, 3) for v in ns["bridge_spot"]()))
    elif command == "edge":
        bpy.ops.wm.open_mainfile(filepath=grass_blend)
        ns = _load_namespace(tools_dir)
        obj = ns["build_forest_edge_t50"]()
        print(f"[T50B] {obj.name}: {ns['triangle_count'](obj)} tris")
        bpy.ops.wm.save_as_mainfile(filepath=grass_blend)
        out, names = ns["export_static"]("Assets/_Game/Art/Environment/Grassland/Models/GL_Grounds_T33B.fbx", GL_T33B_GROUNDS)
        print("[T50B] exported", out, sorted(names))
    elif command == "check":
        bpy.ops.wm.open_mainfile(filepath=forest_blend)
        ns = _load_namespace(tools_dir)
        ns["seam_report"](grass_blend)
    elif command == "fbxdiff":
        bpy.ops.wm.read_factory_settings(use_empty=True)
        ns = _load_namespace(tools_dir)
        ns["fbx_vertex_diff"](args[1], args[2])
    elif command == "preview":
        bpy.ops.wm.open_mainfile(filepath=forest_blend)
        ns = _load_namespace(tools_dir)
        ns["render_sheet"](grass_blend, os.path.join(root, "Docs", "Captures", "Review", "T50_ground_props_sheet.png"))
    else:
        raise SystemExit("pw_forest_ground.py -- forest | edge | check | preview")


if __name__ == "__main__":
    _cli()
