"""Forest Magnifique vegetation kit (T50, lane A): four trees, two landmarks, bushes, ferns and mushrooms.

Intention (charte « fait main », section 10, step 1)
- Character: a cozy, sunny woodland, never dark. Plump rounded crowns made like the approved Prairie trees: ONE lobed
  crown per tree (stiff metaball union, voxel remesh, light smoothing), rounded leaf scales laid on the lobe tops,
  normals that shade every lobe as a soft cushion inside one crown volume. No random bumps, no spiky outline.
- Four trees that read apart at a glance:
    FO_Tree_A  broad round oak: thick trunk with root flares, four limbs, a wide domed crown of big cushions.
    FO_Tree_B  tall slim: a soft woodland fir of five stacked rounded tiers and a round bud on top (never a point).
    FO_Tree_C  leaning: a curved trunk reaching toward Blender +X, an umbrella crown off its foot, a balancing tuft.
    FO_Tree_D  birch-like: twin light trunks with soft dark marks, a high oval crown of clean round cushions (the
               oak's method, smaller), small leaf scales, golden touches.
- Landmarks: FO_Landmark_AncientTree (the Clearing's broad hero tree; a bare horizontal branch toward Blender +X,
  Unity west at yaw 0, where an owl can sit later; a knot hole on the camera side) and FO_Landmark_GiantStump (the
  Hollow's hollow stump: a soft wavy rim, a little round-topped plank door and a round amber window on the camera
  side, shelf fungi, moss cushions, roots).
- Undergrowth: FO_Bush_A (round mound), FO_Bush_B (wide and low, little round amber flowers), FO_Bush_C (tall cluster
  with light young tips); FO_Fern_A (wide arching rosette), FO_Fern_B (young upright fronds and a round curled
  fiddlehead), every frond edged with soft round lobes and a blunt round tip (never a pointed leaflet);
  FO_Mushroom_A (one plump red-orange toadstool with soft round cream dots), FO_Mushroom_B (two cream buns with a
  toasted top), FO_Mushroom_C (a cluster of three small caps, red-orange and amber); smooth round caps of 12 to 14
  segments with dome normals.
- Scale: rabbit 0.65 m with its ears. Trees stand 4.2 to 5.3 m, crowns start above 1.3 m (the collision bands cut
  trunks from crowns at 0.95 m), the ancient tree 6.4 m, the stump 1.5 m with a 0.77 m door, bushes 0.6 to 1 m.
- Wanted imperfections: lobes of uneven sizes at fixed seeds, S-curved trunks, a wavy stump rim; nothing random.

Data contract (as pw_grassland.py): color attribute PW_Regions (R/G/B/A weights toward palette columns 1-4 over the
base color), uv PW_Occlusion (x baked occlusion toward column 5, y wind height 0 at the base to 1 at the tips), pivot
at the base on the ground, +Z up, Blender -X = Unity east. LOD0 of trees and landmarks is <name>_LOD0 (like
GL_Tree_A_LOD0), then _LOD1 and _LOD2; pieces under 800 triangles have no LOD.

Palette regions (rows of BiomeWizard.ForestSpec: base, R, G, B, A, occlusion, accent):
  palette.forest.tree      trees A/B/C and the ancient tree: base bark, R deep leaves, G sunlit leaves, B amber
                           leaves (a few scales), A light young green (lobe tops, scale tips) and moss on the bark.
  palette.forest.birch     FO_Tree_D (NEW ROW, proposed in FOREST_PALETTES): base light bark, R/G/B as the tree row,
                           A dark bark marks.
  palette.forest.log       the giant stump: base bark, R moss, G inner wood and door planks, B amber (window, knob,
                           shelf fungi), A cut wood (rim, step, plank variation, fungus undersides).
  palette.forest.bush      bushes: R deep leaves, G sunlit, B amber flowers (FO_Bush_B), A light young tips.
  palette.forest.fern      ferns: base stems, R deep heart, G fronds, B light frond tips, A fiddlehead.
  palette.forest.mushroom  mushrooms: base cream stems, R red-orange caps, G cream caps and spots, B amber blush,
                           A pale gills.

Usage (headless, from the project root):
    "<blender>" -b --factory-startup --python SourceArt/Blender/Tools/pw_forest.py -- --build --preview <dir>
  --build      builds everything, saves BL_Biome_Forest_Magnifique.blend and exports FO_Vegetation.fbx
  --preview    renders one image per piece into <dir> and the review sheet Docs/Captures/Review/T50_vegetation_sheet.png
               (temporary camera, light, ground and materials; nothing is saved afterwards)
Without --build, --preview opens the saved blend first. Inside a live Blender, exec this file in a namespace and call
build_forest(), save_forest(), export_forest() and render_forest_previews(dir).
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def _tools_dir():
    path = globals().get("__file__")
    if path and os.path.exists(path):
        return os.path.dirname(os.path.abspath(path))
    marker = os.sep + "SourceArt" + os.sep
    if marker in bpy.data.filepath:
        return os.path.join(bpy.data.filepath.split(marker)[0], "SourceArt", "Blender", "Tools")
    raise RuntimeError("Run pw_forest.py with --python, or from a .blend saved under SourceArt/.")


TOOLS = _tools_dir()
PROJECT = os.path.normpath(os.path.join(TOOLS, "..", "..", ".."))
if "object_from_bmesh" not in globals():
    for _helper in ("pw_grassland.py", "pw_export.py"):
        _path = os.path.join(TOOLS, _helper)
        exec(compile(open(_path, encoding="utf-8").read(), _path, "exec"), globals())

FO_BLEND = "SourceArt/Blender/Environment/Forest/BL_Biome_Forest_Magnifique.blend"
FO_FBX = "Assets/_Game/Art/Environment/Forest/Models/FO_Vegetation.fbx"
FO_SHEET = "Docs/Captures/Review/T50_vegetation_sheet.png"
FO_PREFIXES = ("FO_Tree_", "FO_Landmark_", "FO_Bush_", "FO_Fern_", "FO_Mushroom_")
UP = Vector((0.0, 0.0, 1.0))
SUN = Vector((0.45, -0.55, 0.7)).normalized()   # the Prairie's painted sun: lit sides match across biomes
# Budgets (LOD0 triangles) and LOD ratios from the T50 brief.
# Lane F polish (T50 round 2): ferns up to 220 triangles (soft round lobes), mushrooms up to 200 (smooth round caps).
BUDGETS = {"FO_Tree_": (600, 1800), "FO_Landmark_": (800, 2500), "FO_Bush_": (150, 500), "FO_Fern_": (20, 220),
           "FO_Mushroom_": (20, 200)}
LOD_RATIOS = {1: (0.35, 0.55), 2: (0.15, 0.25)}

# Palette rows (sRGB hex, base, R, G, B, A, occlusion, accent): BiomeWizard.ForestSpec (T50 art pass values), with the birch row.
FOREST_PALETTES = {
    "palette.forest.tree": ("7A5A42", "4C8C50", "6EAE5A", "E2AE4E", "A0CC78", "3A3428", "FFE6B4"),
    "palette.forest.bush": ("5A9A54", "4A8A4E", "8ABE64", "EBC066", "B4D688", "32422A", "FFE6B4"),
    "palette.forest.ground": ("78A656", "6AB24E", "AE8A5C", "ABCA7C", "E2CFA0", "4C5E36", "B8CC7C"),
    "palette.forest.log": ("6E4E38", "78AA58", "8E6C4C", "E0AC48", "B4966C", "3A2C22", "FFE2A8"),
    "palette.forest.mushroom": ("E8DCC4", "C8643E", "F2E6D0", "D9A441", "F7F0E0", "5A4034", "FFFFFF"),
    "palette.forest.fern": ("5A9C50", "4A8A48", "86BE62", "ACD07A", "C8E29A", "2E4428", "EEF6D0"),
    # NEW (FO_Tree_D): light birch bark as the base, leaves like the tree row, A = the dark bark marks.
    "palette.forest.birch": ("E6DDC8", "58984F", "92C462", "E6C05A", "4A423A", "3A3428", "FFF2CC"),
}
PIECE_PALETTES = {
    "FO_Tree_A": "palette.forest.tree", "FO_Tree_B": "palette.forest.tree", "FO_Tree_C": "palette.forest.tree",
    "FO_Tree_D": "palette.forest.birch", "FO_Landmark_AncientTree": "palette.forest.tree",
    "FO_Landmark_GiantStump": "palette.forest.log", "FO_Bush_": "palette.forest.bush", "FO_Fern_": "palette.forest.fern",
    "FO_Mushroom_": "palette.forest.mushroom",
}
REPORT = {}


def palette_of(name):
    for key, row in PIECE_PALETTES.items():
        if name.startswith(key):
            return row
    raise KeyError(name)


def clamp01(x):
    return max(0.0, min(1.0, x))


# ---------------------------------------------------------------- parts bookkeeping

class Parts:
    """Vertex ranges of the parts written into one bmesh (crown, scales, bark...), so painting knows each vertex."""

    def __init__(self):
        self.ranges = []

    def add(self, kind, start, end, data=None):
        if end > start:
            self.ranges.append((start, end, kind, data or {}))

    def table(self, count):
        out = [("none", {}, 0)] * count
        for s, e, k, d in self.ranges:
            for i in range(s, min(e, count)):
                out[i] = (k, d, i - s)
        return out


# ---------------------------------------------------------------- mesh helpers

def fo_tube(bm, points, radii, sides, cap=True, shape=None, phase=0.0):
    """Ring sweep along points with parallel-transported frames (no twist); shape(i, k, angle) scales a vertex."""
    n = len(points)
    rings = []
    u_prev = None
    for i, p in enumerate(points):
        if i == 0:
            axis = points[1] - points[0]
        elif i == n - 1:
            axis = points[-1] - points[-2]
        else:
            axis = points[i + 1] - points[i - 1]
        axis = axis.normalized()
        if u_prev is None:
            ref = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
            u = (ref - axis * ref.dot(axis)).normalized()
        else:
            u = (u_prev - axis * u_prev.dot(axis)).normalized()
        w = axis.cross(u)
        u_prev = u
        ring = []
        for k in range(sides):
            a = phase + k / sides * math.tau
            m = shape(i, k, a) if shape else 1.0
            ring.append(bm.verts.new(p + (u * math.cos(a) + w * math.sin(a)) * (radii[i] * m)))
        rings.append(ring)
    for ra, rb in zip(rings[:-1], rings[1:]):
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((ra[k], ra[m], rb[m], rb[k]))
    if cap:
        bm.faces.new(rings[-1])
    return rings


def fo_catmull(ctrl, count):
    """count points on a Catmull-Rom curve through ctrl (both ends included)."""
    pts = [ctrl[0] * 2.0 - ctrl[1]] + list(ctrl) + [ctrl[-1] * 2.0 - ctrl[-2]]
    segs = len(ctrl) - 1
    out = []
    for i in range(count):
        u = i / (count - 1) * segs
        k = min(int(u), segs - 1)
        t = u - k
        p0, p1, p2, p3 = pts[k], pts[k + 1], pts[k + 2], pts[k + 3]
        out.append(0.5 * ((p1 * 2.0) + (p2 - p0) * t + (p0 * 2.0 - p1 * 5.0 + p2 * 4.0 - p3) * (t * t)
                          + (p1 * 3.0 - p0 - p2 * 3.0 + p3) * (t * t * t)))
    return out


def fo_lerp_list(values, count):
    out = []
    for i in range(count):
        u = i / (count - 1) * (len(values) - 1)
        k = min(int(u), len(values) - 2)
        t = u - k
        out.append(values[k] * (1.0 - t) + values[k + 1] * t)
    return out


def fo_branch(bm, parts, ctrl, radii, rings, sides, kind="bark", cap=False, data=None, shape=None, phase=0.0):
    start = len(bm.verts)
    pts = fo_catmull([Vector(p) for p in ctrl], rings) if rings != len(ctrl) else [Vector(p) for p in ctrl]
    fo_tube(bm, pts, fo_lerp_list(radii, rings), sides, cap=cap, shape=shape, phase=phase)
    parts.add(kind, start, len(bm.verts), data)
    return pts


def fo_octa(bm, center, radius, height=None):
    h = height or radius
    ring = [bm.verts.new(center + Vector((math.cos(k * math.pi / 2) * radius, math.sin(k * math.pi / 2) * radius, 0)))
            for k in range(4)]
    top = bm.verts.new(center + Vector((0, 0, h)))
    bottom = bm.verts.new(center - Vector((0, 0, h)))
    for k in range(4):
        n = (k + 1) % 4
        bm.faces.new((ring[k], ring[n], top))
        bm.faces.new((ring[n], ring[k], bottom))


def fo_face(bm, verts, expected):
    """New face, flipped if needed so its normal follows the expected direction."""
    f = bm.faces.new(verts)
    f.normal_update()
    if f.normal.dot(expected) < 0.0:
        f.normal_flip()
    return f


# ---------------------------------------------------------------- crowns

def fo_crown(masses, name, resolution, voxel, smooth_iterations=5):
    """One lobed crown: stiff metaball union of ellipsoid lobes (center, radius, squash), voxel remesh, smoothing.
    Returns a temporary object in WORK (the Prairie method of pw_grassland._crown_mesh, per-lobe squash added)."""
    work = bpy.data.collections["WORK"]
    data = bpy.data.metaballs.new("WORK_" + name)
    data.resolution = resolution
    data.render_resolution = resolution
    for m in masses:
        e = data.elements.new(type='ELLIPSOID')
        e.co = m[0]
        e.radius = m[1] * 1.35
        e.size_x, e.size_y, e.size_z = 1.0, 1.0, m[2]
        e.stiffness = 3.0
    obj = bpy.data.objects.new("WORK_" + name, data)
    work.objects.link(obj)
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(obj)
    bpy.data.metaballs.remove(data)
    temp = bpy.data.objects.new("WORK_" + name + "_Temp", mesh)
    work.objects.link(temp)
    remesh = temp.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = voxel
    replace_with_evaluated(temp, remesh)
    smooth = temp.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.6
    smooth.iterations = smooth_iterations
    replace_with_evaluated(temp, smooth)
    return temp


def lobes(entries, squash):
    """(center, radius) or (center, radius, squash) tuples -> (Vector, radius, squash)."""
    out = []
    for e in entries:
        out.append((Vector(e[0]), e[1], e[2] if len(e) > 2 else squash))
    return out


def lobe_dir(co, masses):
    """Blended outward direction of the lobes around co (ellipsoid normals weighted by their field)."""
    acc = Vector((0.0, 0.0, 0.0))
    for c, r, sz in masses:
        d = co - c
        dn = Vector((d.x, d.y, d.z / sz))
        L = dn.length / (r * 1.3)
        if L >= 1.0 or dn.length < 1e-6:
            continue
        w = (1.0 - L * L) ** 2
        acc += Vector((d.x, d.y, d.z / (sz * sz))).normalized() * w
    return acc.normalized() if acc.length > 1e-6 else None


def crown_dir(co, center, radii):
    d = co - center
    return Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).normalized()


def foliage_normal(co, own, masses, center, radii, weights=(0.25, 0.45, 0.30)):
    cd = crown_dir(co, center, radii)
    lob = lobe_dir(co, masses) or cd
    return (own.normalized() * weights[0] + lob * weights[1] + cd * weights[2]).normalized()


LEAF = ((-0.12, 0.0), (0.2, 0.46), (0.64, 0.4), (1.0, 0.0), (0.64, -0.4), (0.2, -0.46))


def fo_leaf(bm, base, normal, along_hint, size, lift, width=1.0):
    """A rounded leaf scale lying on the crown: six-point soft ogive, edges slightly cupped, tip lifted.
    Front faces outward, a back copy sits 8 mm under it (both culled sides closed). Returns the leaf axis."""
    n = normal.normalized()
    along = along_hint - n * along_hint.dot(n)
    if along.length < 1e-4:
        along = n.orthogonal()
    along.normalize()
    side = n.cross(along).normalized()
    pts = []
    for t, w in LEAF:
        rise = size * (lift * max(0.0, t) ** 2 + 0.05 * (2.0 * abs(w)) ** 2)
        pts.append(base + along * (t * size) + side * (w * size * width) + n * rise)
    front = bm.faces.new([bm.verts.new(p) for p in pts])
    back = bm.faces.new([bm.verts.new(p - n * 0.008) for p in reversed(pts)])
    front.normal_update()
    back.normal_update()
    if front.normal.dot(n) < 0.0:
        front.normal_flip()
    if back.normal.dot(n) > 0.0:
        back.normal_flip()
    return along


def scale_candidates(masses, count, size_range, lift_range, seed, amber_ratio, cap=0.7):
    """Leaf scale candidates on every lobe's upper cap (golden angle), sorted so the topmost of each lobe come first:
    a LOD that keeps the first N scales keeps one or two per lobe top."""
    rng = random.Random(seed)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    total = sum(m[1] ** 2 for m in masses)
    out = []
    for li, (c, r, sz) in enumerate(masses):
        k = max(2, int(round(count * 2.4 * r * r / total)))
        phase = rng.uniform(0.0, math.tau)
        for j in range(k):
            z = 1.0 - cap * (j + 0.5) / k
            rr = math.sqrt(max(0.0, 1.0 - z * z))
            a = phase + j * golden + rng.uniform(-0.25, 0.25)
            d = Vector((math.cos(a) * rr, math.sin(a) * rr, z))
            out.append(((j + 0.5) / k + rng.uniform(0.0, 0.06), li, d, rng.uniform(*size_range),
                        rng.uniform(*lift_range), rng.random() < amber_ratio, rng.uniform(0.0, math.tau)))
    out.sort(key=lambda x: x[0])
    return out


def place_scales(bm, parts, bvh, masses, cands, count, center, min_nz=0.18, spacing=0.85, width=1.0):
    placed = []
    for rank, li, d, size, lift, amber, spin in cands:
        if len(placed) >= count:
            break
        c, r, sz = masses[li]
        hit, normal, _, _ = bvh.ray_cast(c + d * (r * 3.0), -d)
        if hit is None or normal.z < min_nz or (hit - c).length > r * 1.45:
            continue
        if any((hit - p).length < spacing * (size + s) * 0.5 for p, s in placed):
            continue
        out = Vector((hit.x - center.x, hit.y - center.y, 0.0))
        if out.length < 0.12 * r:
            out = Vector((math.cos(spin), math.sin(spin), 0.0))
        hint = out.normalized() * 0.8 - UP * 0.6
        base = hit - normal * 0.015
        start = len(bm.verts)
        along = fo_leaf(bm, base, normal, hint, size, lift, width)
        parts.add("scale", start, len(bm.verts), dict(base=base, along=along, size=size, amber=amber, normal=normal.copy()))
        placed.append((hit, size))
    return len(placed)


def bbox_of(bm_or_obj):
    vs = [v.co for v in (bm_or_obj.verts if isinstance(bm_or_obj, bmesh.types.BMesh) else bm_or_obj.data.vertices)]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def build_crowned(spec, collections, lod_collections):
    """A crowned piece (tree, landmark, bush): crown per LOD (decimated from one union), leaf scales, wood, painting."""
    name = spec["name"]
    masses = spec["masses"]
    crown = fo_crown(masses, name, spec["resolution"], spec["voxel"], spec.get("smooth", 5))
    if spec.get("sink") is not None:
        lo, hi = bbox_of(crown)
        shift = Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -lo.z - spec["sink"]))
        crown.data.transform(Matrix.Translation(shift))
        masses = [(c + shift, r, sz) for c, r, sz in masses]
    lo, hi = bbox_of(crown)
    center = spec.get("center") or (lo + hi) * 0.5
    radii = spec.get("radii") or (hi - lo) * 0.5
    sc = spec["scales"]
    cands = scale_candidates(masses, sc["count"], sc["size"], sc["lift"], sc["seed"], sc["amber"])
    results = []
    lods = spec["lods"]
    for lod, cfg in enumerate(lods):
        temp = bpy.data.objects.new("WORK_" + name + "_LODTemp", crown.data.copy())
        bpy.data.collections["WORK"].objects.link(temp)
        decimate_to(temp, cfg["crown"])
        bm = bmesh.new()
        bm.from_mesh(temp.data)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        data = temp.data
        bpy.data.objects.remove(temp)
        bpy.data.meshes.remove(data)
        parts = Parts()
        parts.add("crown", 0, len(bm.verts))
        bvh = BVHTree.FromBMesh(bm)
        placed = place_scales(bm, parts, bvh, masses, cands, cfg["scales"], center, sc.get("min_nz", 0.18),
                              sc.get("spacing", 0.85), sc.get("width", 1.0))
        if spec.get("extra"):
            spec["extra"](bm, parts, bvh, lod, masses, center, radii)
        if spec.get("wood"):
            spec["wood"](bm, parts, lod)
        bm.normal_update()
        obj_name = f"{name}_LOD{lod}" if len(lods) > 1 else name
        obj = object_from_bmesh(obj_name, bm, collections if lod == 0 else lod_collections)
        spec["paint"](obj, parts.table(len(obj.data.vertices)), masses, center, radii)
        foliage_z = [v.co.z for v, row in zip(obj.data.vertices, parts.table(len(obj.data.vertices)))
                     if row[0] in ("crown", "scale")]
        REPORT.setdefault(obj_name, {})["scales"] = placed
        REPORT[obj_name]["crown_bottom"] = round(min(foliage_z), 3) if foliage_z else None
        results.append(obj)
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    return results


# ---------------------------------------------------------------- painting

def tree_painter(height, crown_bottom, bark="oak", gains=(0.85, 0.35), under_gain=0.42, seed=0, weights=(0.25, 0.45, 0.30)):
    """Painter for palette.forest.tree / palette.forest.birch pieces (see the module docstring)."""
    g_gain, a_gain = gains

    def paint(obj, table, masses, center, radii):
        def fol(co, n, row):
            own = row[1]["normal"] if row[0] == "scale" else n
            return foliage_normal(co, own, masses, center, radii, weights)

        def regions(co, n, i):
            row = table[i]
            kind, data, local = row
            if kind in ("crown", "scale"):
                f = fol(co, n, row)
                sun = smoothstep(0.0, 0.85, f.dot(SUN)) * smoothstep(-0.3, 0.4, f.z)
                top = smoothstep(0.35, 0.95, f.z)
                flick = 0.16 * (0.5 + 0.5 * wave_noise(co * 2.7, seed + 77, octaves=1))
                birch = bark == "birch"   # the birch row's A is the dark bark mark: its leaves brighten through G
                if kind == "crown":
                    if birch:
                        return (1.0, min(1.0, 0.15 + sun * g_gain + flick * 0.6 + top * a_gain * 0.6), 0.0, 0.0)
                    return (1.0, min(1.0, sun * g_gain + flick * 0.6), 0.0, top * a_gain)
                tip = clamp01((co - data["base"]).dot(data["along"]) / data["size"])
                if data["amber"]:
                    return (1.0, 0.35 + 0.3 * sun, 0.32 + 0.22 * tip, 0.0)
                if birch:
                    return (1.0, min(1.0, 0.42 + sun * 0.6 + flick + 0.2 * tip * top), 0.0, 0.0)
                return (1.0, min(1.0, 0.35 + sun * 0.7 + flick), 0.0, (0.12 + 0.55 * tip * tip) * (0.45 + 0.55 * top))
            if kind == "knot":
                return (0.0, 0.0, 0.0, 0.0)
            # Bark: moss patches low on the trunk and on root tops (tree row), dark marks (birch row).
            if bark == "birch":
                marks = smoothstep(0.3, 0.62, wave_noise(Vector((co.x * 3.2, co.y * 3.2, co.z * 7.5)), seed + 9, 2))
                foot = smoothstep(0.55, 0.0, co.z) * 0.85
                return (0.0, 0.0, 0.0, max(marks * 0.85, foot))
            moss = smoothstep(0.8, 0.05, co.z) * smoothstep(-0.15, 0.45, wave_noise(co * 2.4, seed + 5, 2)) * 0.85
            if data.get("root"):
                moss = max(moss, smoothstep(0.35, 0.9, n.z) * 0.35)
            return (0.0, 0.0, 0.0, moss)

        def occlusion(co, n, i):
            row = table[i]
            kind, data, local = row
            h = clamp01(co.z / height)
            if kind in ("crown", "scale"):
                f = fol(co, n, row)
                under = smoothstep(0.15, -0.75, f.z)
                d = co - center
                depth = 1.0 - smoothstep(0.62, 1.0, Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).length)
                if kind == "scale":
                    return (0.5 * under_gain * under, h)
                return (under_gain * under + 0.22 * depth, h)
            if kind == "knot":
                return ((0.22, 0.08, 0.7, 0.95)[min(3, local // 8)], h)   # outer ring, raised rim, recess, hole
            ground = 0.32 * (1.0 - smoothstep(0.0, 0.35, co.z))
            canopy = 0.26 * smoothstep(crown_bottom - 0.8, crown_bottom + 0.1, co.z)
            return (ground + canopy, h)

        write_layers(obj, regions, occlusion)
        me = obj.data
        normals = []
        for v in me.vertices:
            row = table[v.index]
            normals.append(fol(v.co, v.normal, row) if row[0] in ("crown", "scale") else v.normal.copy())
        me.normals_split_custom_set_from_vertices(normals)

    return paint


def bush_painter(seed):
    def paint(obj, table, masses, center, radii):
        height = max(v.co.z for v in obj.data.vertices)

        def fol(co, n, row):
            own = row[1]["normal"] if row[0] == "scale" else n
            return foliage_normal(co, own, masses, center, radii, (0.2, 0.6, 0.2))

        def regions(co, n, i):
            row = table[i]
            kind, data, local = row
            if kind == "flower":
                return (0.0, 0.0, 1.0, 0.0)
            f = fol(co, n, row)
            sun = smoothstep(0.0, 0.85, f.dot(SUN)) * smoothstep(-0.3, 0.4, f.z)
            top = smoothstep(0.35, 0.95, f.z)
            flick = 0.16 * (0.5 + 0.5 * wave_noise(co * 4.0, seed + 31, octaves=1))
            if kind == "scale":
                tip = clamp01((co - data["base"]).dot(data["along"]) / data["size"])
                young = data.get("young", 0.0)
                return (1.0, min(1.0, 0.35 + sun * 0.7 + flick), 0.0, max(young * (0.4 + 0.6 * tip), (0.1 + 0.5 * tip * tip) * top))
            return (1.0, min(1.0, sun * 0.85 + flick * 0.6), 0.0, top * 0.3)

        def occlusion(co, n, i):
            row = table[i]
            h = clamp01(co.z / height)
            if row[0] == "flower":
                return (0.3 if row[2] == 0 else 0.03, h)     # a soft warm heart, clean amber petals
            f = fol(co, n, row)
            under = smoothstep(0.15, -0.75, f.z)
            ground = 0.3 * (1.0 - smoothstep(0.0, 0.12, co.z))
            if row[0] == "scale":
                return (0.2 * under, h)
            d = co - center
            depth = 1.0 - smoothstep(0.62, 1.0, Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).length)
            return (0.4 * under + 0.2 * depth + ground, h)

        write_layers(obj, regions, occlusion)
        me = obj.data
        normals = []
        for v in me.vertices:
            row = table[v.index]
            if row[0] == "flower":   # domed: the flower's normal, opened a little toward each petal
                d = row[1]
                normals.append((d["normal"] + (v.co - d["center"]) * (0.35 / d["size"])).normalized())
            else:
                normals.append(fol(v.co, v.normal, row))
        me.normals_split_custom_set_from_vertices(normals)

    return paint


# ---------------------------------------------------------------- trees

def _tree_lods(c0, s0, c1, s1, c2):
    return [dict(crown=c0, scales=s0), dict(crown=c1, scales=s1), dict(crown=c2, scales=0)]


def _ring_lobes(count, dists, zs, rs, phase, squash=None):
    out = []
    for k in range(count):
        a = phase + k / count * math.tau
        d, z, r = dists[k % len(dists)], zs[k % len(zs)], rs[k % len(rs)]
        e = ((math.cos(a) * d, math.sin(a) * d, z), r)
        out.append(e + ((squash,) if squash else ()))
    return out


# FO_Tree_A: broad round oak.
OAK_MASSES = lobes([((0.0, 0.0, 3.15), 1.05)]
                   + _ring_lobes(6, (1.3, 1.2, 1.35, 1.25, 1.3, 1.2), (2.75, 2.85, 2.7, 2.8, 2.72, 2.9),
                                 (0.8, 0.72, 0.78, 0.74, 0.8, 0.7), 0.2)
                   + _ring_lobes(3, (0.72,), (3.6,), (0.72, 0.7, 0.68), 0.8)
                   + [((0.1, -0.05, 4.0), 0.6)], 0.84)


def _oak_wood(bm, parts, lod):
    sides = (12, 8, 6)[lod]
    rings = (7, 5, 3)[lod]
    ctrl = [(0.0, 0.0, -0.06), (0.05, 0.03, 0.7), (0.1, 0.02, 1.4), (0.06, -0.02, 2.1), (0.02, 0.0, 2.75)]

    def flare(i, k, a):
        lobe = (0.5 + 0.5 * math.cos(a * 4.0 + 0.4)) ** 2
        amp = (0.4, 0.22, 0.12)[lod] if i == 0 else (0.14 if i == 1 and lod == 0 else 0.0)
        return 1.0 + amp * lobe

    fo_branch(bm, parts, ctrl, [0.36, 0.3, 0.27, 0.25, 0.22], rings, sides, cap=True, shape=flare)
    if lod < 2:
        for target, base_z in (((1.0, 0.25, 2.8), 1.75), ((-0.95, -0.15, 2.85), 1.85), ((0.15, 0.95, 2.82), 1.95),
                               ((-0.2, -0.95, 2.9), 2.0)):
            t = Vector(target)
            start = Vector((0.06, 0.0, base_z))
            mid = start + (t - start) * 0.55 + Vector((0, 0, 0.14))
            fo_branch(bm, parts, [start, mid, t], [0.13, 0.095, 0.06], 3 if lod == 0 else 2, 6 if lod == 0 else 5)
    if lod == 0:
        for k in range(5):
            a = (math.tau * k - 0.4) / 5.0
            d = Vector((math.cos(a), math.sin(a), 0.0))
            fo_branch(bm, parts, [d * 0.2 + Vector((0, 0, 0.34)), d * 0.46 + Vector((0, 0, 0.1)),
                                  d * 0.66 + Vector((0, 0, -0.02)), d * 0.76 + Vector((0, 0, -0.14))],
                      [0.15, 0.11, 0.085, 0.07], 4, 5, data={"root": True})


# FO_Tree_B: tall slim soft fir (stacked rounded tiers).
FIR_TIERS = [  # core z, core radius, ring lobes, ring distance, ring radius, ring drop, phase, tier offset (x, y)
    (1.8, 0.7, 8, 0.86, 0.48, -0.34, 0.0, (0.03, -0.02)),
    (2.58, 0.6, 7, 0.72, 0.43, -0.3, 0.45, (-0.04, 0.03)),
    (3.3, 0.5, 7, 0.58, 0.37, -0.26, 0.2, (0.04, 0.03)),
    (3.96, 0.4, 6, 0.44, 0.31, -0.21, 0.7, (-0.02, -0.03)),
    (4.5, 0.3, 4, 0.26, 0.24, -0.14, 0.3, (0.0, 0.01)),
]


def _fir_masses():
    """Five drooping tiers of uneven cushions, each a sloped skirt (an inner ring between the core and the outer
    cushions), slightly off-axis, ending in a small rounded tier: a plain woodland fir, never a decorated tree."""
    rng = random.Random(5150)
    out = []
    for z, cr, n, dist, rr, drop, phase, (ox, oy) in FIR_TIERS:
        out.append(((ox, oy, z + 0.05), cr * 0.85, 0.75))
        for k in range(n):
            a = phase + k / n * math.tau + rng.uniform(-0.18, 0.18)
            d = dist * rng.uniform(0.92, 1.08)
            out.append(((ox + math.cos(a) * d, oy + math.sin(a) * d, z + drop + rng.uniform(-0.04, 0.04)),
                        rr * rng.uniform(0.88, 1.1), 0.7))
            b = a + math.pi / n
            out.append(((ox + math.cos(b) * d * 0.5, oy + math.sin(b) * d * 0.5, z + drop * 0.4), rr * 0.95, 0.72))
    return lobes(out, 0.7)


FIR_MASSES = _fir_masses()


def _fir_wood(bm, parts, lod):
    sides = (8, 6, 5)[lod]
    top = (4.4, 3.0, 1.9)[lod]
    rings = (5, 3, 2)[lod]

    def flare(i, k, a):
        return 1.0 + (0.18 * (0.5 + 0.5 * math.cos(a * 3.0)) ** 2 if i == 0 else 0.0)

    fo_branch(bm, parts, [(0.0, 0.0, -0.06), (0.02, 0.01, top * 0.5), (0.0, 0.0, top)], [0.2, 0.15, 0.1], rings, sides,
              cap=True, shape=flare)
    if lod == 0:
        for k in range(4):
            a = k / 4 * math.tau + 0.4
            d = Vector((math.cos(a), math.sin(a), 0.0))
            fo_branch(bm, parts, [d * 0.1 + Vector((0, 0, 0.24)), d * 0.3 + Vector((0, 0, 0.07)),
                                  d * 0.44 + Vector((0, 0, -0.03)), d * 0.5 + Vector((0, 0, -0.13))],
                      [0.1, 0.075, 0.06, 0.05], 4, 5, data={"root": True})


# FO_Tree_C: leaning, umbrella crown off its foot, balancing tuft.
LEAN_MASSES = lobes([((1.3, 0.1, 3.15), 0.92), ((2.1, 0.0, 3.0), 0.68), ((0.55, 0.3, 3.05), 0.64),
                     ((1.45, 0.8, 2.98), 0.6), ((1.2, -0.62, 3.02), 0.62), ((1.75, 0.35, 3.6), 0.58),
                     ((0.95, -0.2, 3.55), 0.55), ((2.0, -0.45, 3.3), 0.48),
                     ((-0.3, -0.22, 2.4), 0.52, 0.82), ((0.02, -0.5, 2.5), 0.42, 0.82),
                     ((-0.5, 0.12, 2.52), 0.42, 0.82), ((-0.08, -0.12, 2.82), 0.4, 0.82),
                     ((-0.72, -0.25, 2.36), 0.34, 0.82)], 0.78)
LEAN_TRUNK = [(0.0, 0.0, -0.06), (0.1, 0.0, 0.5), (0.32, 0.02, 1.1), (0.62, 0.04, 1.65), (0.9, 0.06, 2.15),
              (1.1, 0.08, 2.55), (1.22, 0.1, 2.9)]


def _lean_wood(bm, parts, lod):
    sides = (9, 7, 5)[lod]
    rings = (8, 5, 3)[lod]

    def flare(i, k, a):
        if i != 0:
            return 1.0
        # A buttress on the side away from the lean holds the tree, small flares around.
        return 1.0 + 0.5 * (0.5 + 0.5 * math.cos(a - math.pi)) ** 3 + 0.1 * (0.5 + 0.5 * math.cos(a * 3.0 + 0.3)) ** 2

    fo_branch(bm, parts, LEAN_TRUNK, [0.33, 0.29, 0.26, 0.23, 0.21, 0.19, 0.17], rings, sides, cap=True, shape=flare)
    branch_sides = (6, 5, 4)[lod]
    branch_rings = (3, 3, 2)[lod]
    # The side branch carries the balancing tuft (kept at every LOD, or the tuft would float).
    fo_branch(bm, parts, [(0.48, 0.03, 1.4), (0.1, -0.1, 1.95), (-0.26, -0.2, 2.38)], [0.15, 0.11, 0.07],
              branch_rings, branch_sides)
    if lod < 2:
        for target in ((1.95, 0.0, 2.98), (0.62, 0.28, 3.0)):
            start = Vector((1.0, 0.07, 2.4))
            t = Vector(target)
            fo_branch(bm, parts, [start, start + (t - start) * 0.55 + Vector((0, 0, 0.1)), t], [0.11, 0.08, 0.05],
                      branch_rings, branch_sides)
    if lod == 0:
        for a in (math.pi + 0.5, math.pi - 0.6, 0.9):
            d = Vector((math.cos(a), math.sin(a), 0.0))
            fo_branch(bm, parts, [d * 0.2 + Vector((0, 0, 0.3)), d * 0.44 + Vector((0, 0, 0.09)),
                                  d * 0.62 + Vector((0, 0, -0.02)), d * 0.7 + Vector((0, 0, -0.14))],
                      [0.14, 0.1, 0.08, 0.065], 4, 5, data={"root": True})


# FO_Tree_D: birch-like, twin light trunks, a high oval crown built like the oak's: one core, a ring of five round
# cushions (the first one sits over the second trunk's top), a ring of four above and a round top. Few, clean lobes of
# near sizes: no small lumps.
BIRCH_CENTER = (-0.12, 0.0)


def _birch_ring(angles, dists, zs, rs, stretch=(1.1, 0.92), shift=0.0):
    """Ring of cushions around the crown axis, stretched along Blender X (the line of the two trunk tops)."""
    cx, cy = BIRCH_CENTER
    return [((cx + shift + math.cos(a) * d * stretch[0], cy + math.sin(a) * d * stretch[1], z), r)
            for a, d, z, r in zip(angles, dists, zs, rs)]


BIRCH_MASSES = lobes([((BIRCH_CENTER[0], BIRCH_CENTER[1], 3.95), 0.68, 0.92)]
                     + _birch_ring([3.44 + k * math.tau / 5 for k in range(5)], (0.78, 0.72, 0.74, 0.7, 0.72),
                                   (3.34, 3.46, 3.42, 3.5, 3.44), (0.52, 0.48, 0.5, 0.47, 0.49))
                     + _birch_ring([4.07 + k * math.tau / 4 for k in range(4)], (0.48, 0.46, 0.5, 0.47),
                                   (4.32, 4.36, 4.3, 4.34), (0.46, 0.44, 0.45, 0.43), (1.0, 1.0), 0.06)
                     + [((0.0, 0.02, 4.78), 0.42)], 0.88)
BIRCH_TRUNKS = [
    ([(0.06, 0.02, -0.06), (0.1, 0.03, 0.8), (0.12, 0.06, 1.6), (0.18, 0.08, 2.4), (0.24, 0.1, 3.1), (0.28, 0.12, 3.8)],
     [0.135, 0.12, 0.105, 0.09, 0.075, 0.06]),
    ([(-0.06, -0.02, -0.06), (-0.12, -0.04, 0.7), (-0.25, -0.08, 1.4), (-0.4, -0.12, 2.1), (-0.52, -0.15, 2.7),
      (-0.6, -0.16, 3.2)], [0.12, 0.105, 0.09, 0.075, 0.065, 0.055]),
]


def _birch_wood(bm, parts, lod):
    sides = (7, 6, 5)[lod]
    rings = (10, 5, 3)[lod]
    for ctrl, radii in BIRCH_TRUNKS:
        fo_branch(bm, parts, ctrl, radii, rings, sides, cap=True)

    def flare(i, k, a):
        return 1.0 + (0.16 * (0.5 + 0.5 * math.cos(a * 2.0 + 0.5)) ** 2 if i == 0 else 0.0)

    fo_branch(bm, parts, [(0.0, 0.0, -0.06), (0.0, 0.0, 0.16), (0.0, 0.0, 0.38)], [0.24, 0.2, 0.13],
              (3, 2, 2)[lod], (8, 6, 5)[lod], cap=True, shape=flare)
    if lod == 0:
        for ctrl in (((0.17, 0.08, 2.5), (0.42, 0.3, 3.0), (0.55, 0.3, 3.45)), ((-0.42, -0.12, 2.2), (-0.7, -0.25, 2.65),
                                                                            (-0.85, -0.3, 2.95))):
            fo_branch(bm, parts, ctrl, [0.04, 0.03, 0.02], 2, 4)


def build_trees(collections, lod_collections):
    out = []
    out += build_crowned(dict(
        name="FO_Tree_A", masses=OAK_MASSES, resolution=0.1, voxel=0.07, center=Vector((0.0, 0.0, 3.2)),
        radii=Vector((2.05, 2.0, 1.25)), lods=_tree_lods(900, 56, 440, 14, 260), wood=_oak_wood,
        scales=dict(count=56, size=(0.26, 0.34), lift=(0.04, 0.1), seed=5101, amber=0.08),
        paint=tree_painter(4.5, 2.1, "oak", (0.85, 0.35), seed=11)), collections, lod_collections)
    out += build_crowned(dict(
        name="FO_Tree_B", masses=FIR_MASSES, resolution=0.07, voxel=0.05, center=Vector((0.0, 0.0, 3.0)),
        radii=Vector((1.3, 1.3, 2.1)), lods=_tree_lods(820, 50, 420, 12, 250), wood=_fir_wood,
        scales=dict(count=50, size=(0.18, 0.25), lift=(0.03, 0.08), seed=5102, amber=0.0, min_nz=0.22),
        paint=tree_painter(5.2, 1.35, "oak", (0.6, 0.3), under_gain=0.5, seed=12, weights=(0.3, 0.5, 0.2))),
        collections, lod_collections)
    out += build_crowned(dict(
        name="FO_Tree_C", masses=LEAN_MASSES, resolution=0.09, voxel=0.06, center=Vector((1.35, 0.1, 3.2)),
        radii=Vector((1.6, 1.5, 0.95)), lods=_tree_lods(700, 50, 360, 12, 210), wood=_lean_wood,
        scales=dict(count=50, size=(0.24, 0.32), lift=(0.04, 0.1), seed=5103, amber=0.1),
        paint=tree_painter(4.1, 2.0, "oak", (0.85, 0.35), seed=13, weights=(0.25, 0.5, 0.25))),
        collections, lod_collections)
    out += build_crowned(dict(
        name="FO_Tree_D", masses=BIRCH_MASSES, resolution=0.07, voxel=0.05, center=Vector((-0.12, 0.0, 4.0)),
        radii=Vector((1.35, 1.15, 1.12)), lods=_tree_lods(760, 72, 360, 18, 220), wood=_birch_wood,
        scales=dict(count=72, size=(0.12, 0.16), lift=(0.04, 0.1), seed=5104, amber=0.14),
        paint=tree_painter(5.2, 2.85, "birch", (0.95, 0.45), seed=14, weights=(0.25, 0.5, 0.25))),
        collections, lod_collections)
    return out


# ---------------------------------------------------------------- landmarks

ANCIENT_MASSES = lobes([((0.0, 0.0, 4.6), 1.6, 0.8)]
                       + _ring_lobes(7, (2.05, 1.9, 2.15, 1.95, 2.1, 1.9, 2.0), (4.05, 4.15, 4.0, 4.1, 4.0, 4.2, 4.05),
                                     (1.08, 1.0, 1.12, 1.02, 1.1, 0.98, 1.05), 0.3, 0.8)
                       + _ring_lobes(4, (1.1,), (5.05,), (0.95, 0.9, 0.92, 0.88), 0.7, 0.8)
                       + [((0.2, 0.1, 5.65), 0.8, 0.8), ((3.86, 0.02, 3.28), 0.46, 0.86), ((4.12, -0.28, 3.18), 0.34, 0.86),
                          ((3.6, 0.3, 3.36), 0.32, 0.86)], 0.8)
ANCIENT_TRUNK = [(0.0, 0.0, -0.06), (0.1, 0.05, 0.8), (0.22, 0.04, 1.6), (0.12, -0.02, 2.4), (0.0, 0.0, 3.2),
                 (-0.05, 0.0, 3.9)]
OWL_BRANCH = [(0.2, 0.04, 2.42), (0.9, 0.06, 2.6), (1.7, 0.03, 2.72), (2.5, -0.01, 2.78), (3.2, 0.0, 2.86),
              (3.72, 0.0, 3.12)]
OWL_RADII = [0.27, 0.21, 0.165, 0.14, 0.11, 0.08]


def _ancient_wood(bm, parts, lod):
    sides = (14, 10, 8)[lod]
    rings = (7, 4, 3)[lod]

    def trunk_shape(i, k, a):
        lobe = (0.5 + 0.5 * math.cos(a * 4.0 + 0.2)) ** 2
        flare = 1.0 + (0.36 if i == 0 else 0.12 if i == 1 else 0.0) * lobe
        fluting = 1.0 + (0.035 * (1 if k % 2 == 0 else -1) if lod == 0 and i > 0 else 0.0)
        return flare * fluting

    fo_branch(bm, parts, ANCIENT_TRUNK, [0.8, 0.66, 0.58, 0.55, 0.52, 0.48], rings, sides, cap=True, shape=trunk_shape)
    # The owl branch: long, nearly horizontal from x 1.7 to 3.2, ends in its own small leafy tuft.
    fo_branch(bm, parts, OWL_BRANCH, OWL_RADII, (6, 4, 3)[lod], (7, 5, 5)[lod])
    if lod < 2:
        for target in ((1.45, 0.62, 3.95), (-0.95, 1.2, 4.0), (-1.2, -0.85, 3.95), (0.55, -1.4, 4.0)):
            start = Vector((0.0, 0.0, 3.0))
            t = Vector(target)
            fo_branch(bm, parts, [start, start + (t - start) * 0.5 + Vector((0, 0, 0.12)), t], [0.26, 0.19, 0.12],
                      3 if lod == 0 else 2, 6 if lod == 0 else 5)
        for k in range(5 if lod == 0 else 4):
            a = (math.tau * k - 0.2) / (5.0 if lod == 0 else 4.0) + 0.15
            d = Vector((math.cos(a), math.sin(a), 0.0))
            fo_branch(bm, parts, [d * 0.6 + Vector((0, 0, 0.55)), d * 1.0 + Vector((0, 0, 0.22)),
                                  d * 1.28 + Vector((0, 0, 0.02)), d * 1.42 + Vector((0, 0, -0.16))],
                      [0.3, 0.21, 0.15, 0.11], 4 if lod == 0 else 3, 6 if lod == 0 else 5, data={"root": True})
    if lod == 0:
        # A knot hole on the camera side (Blender +Y) at 1.5 m: raised rim, dark recess.
        c = Vector((0.2, 0.04, 1.5))
        surface = c + Vector((0.0, 0.6, 0.0))
        start = len(bm.verts)
        rings_k = []
        for (rx, rz, out) in ((0.14, 0.18, -0.03), (0.105, 0.14, 0.05), (0.07, 0.1, -0.03)):
            rings_k.append([bm.verts.new(surface + Vector((math.cos(t) * rx, out, math.sin(t) * rz)))
                            for t in (j / 8 * math.tau for j in range(8))])
        center = bm.verts.new(surface + Vector((0.0, -0.09, 0.0)))
        for ra, rb in zip(rings_k[:-1], rings_k[1:]):
            for j in range(8):
                m = (j + 1) % 8
                fo_face(bm, (ra[j], ra[m], rb[m], rb[j]), Vector((0, 1, 0)))
        for j in range(8):
            fo_face(bm, (rings_k[-1][j], rings_k[-1][(j + 1) % 8], center), Vector((0, 1, 0)))
        parts.add("knot", start, len(bm.verts), {"count": len(bm.verts) - start})


def build_ancient_tree(collections, lod_collections):
    return build_crowned(dict(
        name="FO_Landmark_AncientTree", masses=ANCIENT_MASSES, resolution=0.12, voxel=0.08,
        center=Vector((0.0, 0.0, 4.6)), radii=Vector((3.1, 3.0, 1.6)),
        lods=[dict(crown=1000, scales=80), dict(crown=520, scales=20), dict(crown=330, scales=0)], wood=_ancient_wood,
        scales=dict(count=80, size=(0.34, 0.46), lift=(0.04, 0.1), seed=5201, amber=0.12),
        paint=tree_painter(6.4, 3.25, "oak", (0.85, 0.38), seed=21)), collections, lod_collections)


def owl_perch(obj):
    """Where an owl can sit: the point of the owl branch's top with the most room above it (ray up to the crown)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    pts = fo_catmull([Vector(p) for p in OWL_BRANCH], 40)
    radii = fo_lerp_list(OWL_RADII, 40)
    best = None
    for p, r in zip(pts, radii):
        if not 1.4 <= p.x <= 3.3:
            continue
        top = p + Vector((0, 0, r))
        hit = bvh.ray_cast(top + Vector((0, 0, 0.02)), UP)
        room = (hit[0] - top).length if hit[0] is not None else 9.0
        score = min(room, 1.0) - abs(p.x - 2.5) * 0.15
        if best is None or score > best[0]:
            best = (score, top, room)
    return best


# Giant stump: a soft wavy rim, hollow inside, door and window on the camera side (Blender +Y = Unity -Z).
def _rim_z(a):
    return 1.4 + 0.09 * math.cos(3.0 * a + 0.5) + 0.045 * math.cos(5.0 * a + 1.9)


def _stump_flare(a):
    return 1.0 + 0.16 * (0.5 + 0.5 * math.cos(6.0 * a)) ** 1.5


STUMP_OUTER = [  # (radius, z or callable(a)) from the ground to the rim, then inside
    (lambda a: 1.0 * _stump_flare(a), -0.06), (lambda a: 0.94 * (1.0 + 0.06 * (0.5 + 0.5 * math.cos(6.0 * a)) ** 1.5), 0.1),
    (0.86, 0.32), (0.82, 0.62), (0.8, 0.92), (0.79, lambda a: _rim_z(a) - 0.28), (0.78, lambda a: _rim_z(a) - 0.06),
    (0.74, _rim_z), (0.62, lambda a: _rim_z(a) - 0.01), (0.58, lambda a: _rim_z(a) - 0.07),
    (0.56, lambda a: _rim_z(a) - 0.45), (0.5, 0.72),
]
STUMP_RIM_TOP = (7, 8)       # ring indices of the cut-wood band
STUMP_INNER_FROM = 8


def _stump_surface(a, z):
    """Outer radius of the stump body at angle a and height z (no fluting), for the door and window."""
    prev = None
    for r, zz in STUMP_OUTER[:7]:
        rr = r(a) if callable(r) else r
        z0 = zz(a) if callable(zz) else zz
        if prev and prev[1] <= z <= z0:
            t = (z - prev[1]) / max(1e-6, z0 - prev[1])
            return prev[0] * (1 - t) + rr * t
        prev = (rr, z0)
    return prev[0]


def _stump_body(bm, parts, lod):
    sides = (36, 20, 12)[lod]
    keep = list(range(len(STUMP_OUTER))) if lod < 2 else [0, 1, 2, 4, 6, 7, 8, 10, 11]
    rings = []
    for idx in keep:
        r, zz = STUMP_OUTER[idx]
        start = len(bm.verts)
        ring = []
        for k in range(sides):
            a = k / sides * math.tau
            rr = r(a) if callable(r) else r
            z = zz(a) if callable(zz) else zz
            if 1 <= idx <= 6 and lod == 0:
                rr *= 1.0 + 0.03 * (1 if k % 2 == 0 else -1)   # soft bark ridges
            ring.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        parts.add("stump", start, len(bm.verts), {"ring": idx})
        rings.append(ring)
    for ra, rb in zip(rings[:-1], rings[1:]):
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((ra[k], ra[m], rb[m], rb[k]))
    bm.faces.new(rings[-1])   # the hollow's floor, facing up


def _stump_door(bm, parts, lod):
    """The little round-topped door, tilted with the stump's taper: frame, four planks, a knob, a step."""
    z0, z1 = 0.08, 0.77
    y0 = _stump_surface(math.pi / 2, z0) * 1.03 + 0.012
    y1 = _stump_surface(math.pi / 2, z1) * 1.03 + 0.012
    origin = Vector((0.0, y0, z0))
    v_dir = Vector((0.0, y1 - y0, z1 - z0)).normalized()
    u_dir = Vector((1.0, 0.0, 0.0))
    w_dir = v_dir.cross(u_dir).normalized()
    if w_dir.y < 0:
        w_dir = -w_dir
    half, spring = 0.23, 0.46

    def at(u, v, w):
        return origin + u_dir * u + v_dir * v + w_dir * w

    def arch_v(u):
        return spring + math.sqrt(max(0.0, half * half - u * u))

    arch_n = (9, 5, 5)[lod]
    outline = [(-half, -0.03)] + [(math.cos(math.pi - j / (arch_n - 1) * math.pi) * half,
                                   spring + math.sin(math.pi - j / (arch_n - 1) * math.pi) * half) for j in range(arch_n)] \
        + [(half, -0.03)]
    frame = 0.05

    def offset(u, v):
        if v <= spring:
            return (u + math.copysign(frame, u), v)
        d = Vector((u, v - spring))
        d.normalize()
        return (u + d.x * frame, v + d.y * frame)

    start = len(bm.verts)
    front_w, back_w = 0.035, -0.09
    inner_front = [bm.verts.new(at(u, v, front_w)) for u, v in outline]
    outer_front = [bm.verts.new(at(*offset(u, v), front_w)) for u, v in outline]
    inner_door = [bm.verts.new(at(u, v, 0.0)) for u, v in outline] if lod < 2 else None
    outer_back = [bm.verts.new(at(*offset(u, v), back_w)) for u, v in outline] if lod < 2 else None
    for j in range(len(outline) - 1):
        fo_face(bm, (inner_front[j], inner_front[j + 1], outer_front[j + 1], outer_front[j]), w_dir)
        if lod < 2:
            u, v = outline[j]
            out_dir = at(*offset(u, v), 0.0) - at(u, v, 0.0)
            fo_face(bm, (outer_front[j], outer_front[j + 1], outer_back[j + 1], outer_back[j]), out_dir)
            fo_face(bm, (inner_door[j], inner_door[j + 1], inner_front[j + 1], inner_front[j]), -out_dir)
    parts.add("door_frame", start, len(bm.verts))
    # Planks: four boards with thin seams (the dark wood behind shows between them).
    planks = 4
    for p in range(planks):
        start = len(bm.verts)
        ua = -half + p * (2 * half / planks) + 0.004
        ub = -half + (p + 1) * (2 * half / planks) - 0.004
        um = (ua + ub) * 0.5
        pts = [at(ua, -0.02, 0.0), at(ub, -0.02, 0.0), at(ub, arch_v(ub), 0.0), at(um, arch_v(um), 0.0),
               at(ua, arch_v(ua), 0.0)]
        fo_face(bm, [bm.verts.new(q) for q in pts], w_dir)
        parts.add("plank", start, len(bm.verts), {"plank": p})
    start = len(bm.verts)
    fo_octa(bm, at(0.14, 0.3, 0.03), 0.026, 0.026)
    parts.add("knob", start, len(bm.verts))
    if lod == 0:
        # A wooden step slice in front of the door, sunk 1 cm into the ground.
        start = len(bm.verts)
        c = Vector((0.0, y0 + 0.24, 0.0))
        rings = fo_tube(bm, [c + Vector((0, 0, -0.01)), c + Vector((0, 0, 0.045))], [0.17, 0.16], 8, cap=True)
        parts.add("step", start, len(bm.verts))


def _stump_window(bm, parts, lod):
    a = math.pi / 2 + 0.55
    z = 1.04
    r_surf = _stump_surface(a, z) * 1.03
    n = Vector((math.cos(a), math.sin(a), 0.0))
    t = Vector((-math.sin(a), math.cos(a), 0.0))
    c = n * (r_surf + 0.012) + Vector((0, 0, z))
    segs = (10, 8, 6)[lod]
    rin, rout = 0.1, 0.145

    def ring(radius, w):
        return [bm.verts.new(c + (t * math.cos(j / segs * math.tau) + UP * math.sin(j / segs * math.tau)) * radius + n * w)
                for j in range(segs)]

    start = len(bm.verts)
    if lod < 2:
        inner_front, outer_front = ring(rin, 0.03), ring(rout, 0.03)
        inner_glass, outer_back = ring(rin, 0.0), ring(rout, -0.07)
        for j in range(segs):
            m = (j + 1) % segs
            radial = (t * math.cos((j + 0.5) / segs * math.tau) + UP * math.sin((j + 0.5) / segs * math.tau))
            fo_face(bm, (inner_front[j], inner_front[m], outer_front[m], outer_front[j]), n)
            fo_face(bm, (outer_front[j], outer_front[m], outer_back[m], outer_back[j]), radial)
            fo_face(bm, (inner_glass[j], inner_glass[m], inner_front[m], inner_front[j]), -radial)
        parts.add("window_frame", start, len(bm.verts))
    start = len(bm.verts)
    glass = ring(rin, 0.0 if lod < 2 else 0.02)
    fo_face(bm, glass, n)
    parts.add("window_glass", start, len(bm.verts))


def _stump_details(bm, parts, lod):
    if lod == 2:
        return
    # Shelf fungi on the back and the side: amber half discs, pale undersides.
    fungi = ((-0.45, 0.58, 0.12), (-0.8, 0.8, 0.1), (-0.62, 0.42, 0.085), (2.75, 0.45, 0.1))
    for a, z, radius in (fungi if lod == 0 else fungi[:2]):
        n = Vector((math.cos(a), math.sin(a), 0.0))
        t = Vector((-math.sin(a), math.cos(a), 0.0))
        c = n * (_stump_surface(a, z) - 0.03) + Vector((0, 0, z))
        rim_top, rim_bot = [], []
        for j in range(6):
            phi = j / 5 * math.pi
            p = c + t * (math.cos(phi) * radius) + n * (math.sin(phi) * radius * 0.85 + 0.03)
            rim_top.append(p - UP * 0.004 * math.sin(phi))
            rim_bot.append(p - UP * 0.022)
        start = len(bm.verts)
        top_c = bm.verts.new(c + UP * 0.016)
        bot_c = bm.verts.new(c - UP * 0.03)
        vt = [bm.verts.new(p) for p in rim_top]
        parts.add("fungus_top", start, len(bm.verts))
        start = len(bm.verts)
        vb = [bm.verts.new(p) for p in rim_bot]
        bottom_center = bot_c
        for j in range(5):
            fo_face(bm, (top_c, vt[j], vt[j + 1]), UP)
            fo_face(bm, (bottom_center, vb[j + 1], vb[j]), -UP)
            mid = (rim_top[j] + rim_top[j + 1]) * 0.5 - c
            fo_face(bm, (vt[j], vb[j], vb[j + 1], vt[j + 1]), mid)
        parts.add("fungus_under", start, len(bm.verts))
    if lod == 0:
        # Moss cushions sitting on the rim.
        for a in (0.4, 2.3, 4.15):
            c = Vector((math.cos(a) * 0.68, math.sin(a) * 0.68, _rim_z(a) - 0.02))
            start = len(bm.verts)
            ring1 = [bm.verts.new(c + Vector((math.cos(j / 8 * math.tau) * 0.15, math.sin(j / 8 * math.tau) * 0.12, -0.01)))
                     for j in range(8)]
            ring2 = [bm.verts.new(c + Vector((math.cos(j / 8 * math.tau) * 0.1, math.sin(j / 8 * math.tau) * 0.08, 0.06)))
                     for j in range(8)]
            top = bm.verts.new(c + Vector((0, 0, 0.085)))
            for j in range(8):
                m = (j + 1) % 8
                fo_face(bm, (ring1[j], ring1[m], ring2[m], ring2[j]), (ring1[j].co - c) + UP * 0.3)
                fo_face(bm, (ring2[j], ring2[m], top), UP)
            parts.add("moss", start, len(bm.verts))


def _stump_roots(bm, parts, lod):
    if lod == 2:
        return
    angles = [k * math.pi / 3 for k in range(6)] if lod == 0 else [0.0, math.pi, 4 * math.pi / 3]
    for a in angles:
        d = Vector((math.cos(a), math.sin(a), 0.0))
        fo_branch(bm, parts, [d * 0.8 + Vector((0, 0, 0.44)), d * 1.06 + Vector((0, 0, 0.22)),
                              d * 1.22 + Vector((0, 0, 0.04)), d * 1.28 + Vector((0, 0, -0.14))],
                  [0.26, 0.2, 0.15, 0.13], 4 if lod == 0 else 3, 8 if lod == 0 else 6, kind="stump_root")


def build_giant_stump(collections, lod_collections):
    out = []
    for lod in range(3):
        bm = bmesh.new()
        parts = Parts()
        _stump_body(bm, parts, lod)
        _stump_roots(bm, parts, lod)
        _stump_door(bm, parts, lod)
        _stump_window(bm, parts, lod)
        _stump_details(bm, parts, lod)
        bm.normal_update()
        name = f"FO_Landmark_GiantStump_LOD{lod}"
        obj = object_from_bmesh(name, bm, collections if lod == 0 else lod_collections)
        _paint_stump(obj, parts.table(len(obj.data.vertices)))
        out.append(obj)
    return out


def _paint_stump(obj, table):
    height = max(v.co.z for v in obj.data.vertices)

    def regions(co, n, i):
        kind, data, local = table[i]
        if kind == "stump":
            ring = data["ring"]
            if ring in STUMP_RIM_TOP:
                return (0.0, 0.0, 0.0, 1.0)                       # cut wood band on the rim
            if ring >= STUMP_INNER_FROM:
                return (0.0, 1.0, 0.0, 0.25 if ring == STUMP_INNER_FROM else 0.0)   # inner wood
            moss = smoothstep(0.5, 0.95, co.z / 1.4) * smoothstep(-0.1, 0.5, wave_noise(co * 2.2, 61, 2)) * 0.9
            foot = smoothstep(0.35, 0.0, co.z) * smoothstep(0.0, 0.5, wave_noise(co * 3.0, 62, 2)) * 0.7
            return (max(moss, foot), 0.0, 0.0, 0.0)
        if kind == "stump_root":
            return (smoothstep(0.4, 0.9, n.z) * 0.4, 0.0, 0.0, 0.0)
        if kind == "plank":
            return (0.0, 1.0, 0.0, (0.0, 0.35, 0.12, 0.45)[data["plank"]])
        if kind in ("knob", "window_glass", "fungus_top"):
            return (0.0, 0.0, 1.0, 0.0)
        if kind in ("step", "fungus_under"):
            return (0.0, 0.0, 0.0, 1.0 if kind == "fungus_under" or n.z > 0.5 else 0.3)
        if kind == "moss":
            return (1.0, 0.0, 0.0, 0.0)
        return (0.0, 0.0, 0.0, 0.0)                                # door and window frames: bark

    def occlusion(co, n, i):
        kind, data, local = table[i]
        h = clamp01(co.z / height)
        if kind == "stump":
            ring = data["ring"]
            if ring >= STUMP_INNER_FROM + 1:
                return (0.35 + 0.45 * smoothstep(1.3, 0.7, co.z), h)   # the hollow darkens downward
            return (0.3 * (1.0 - smoothstep(0.0, 0.3, co.z)), h)
        if kind == "plank":
            return (0.12 + 0.18 * (1.0 - smoothstep(0.0, 0.3, co.z)), h)
        if kind == "window_glass":
            return (0.0, h)
        if kind == "stump_root":
            return (0.3 * (1.0 - smoothstep(0.0, 0.25, co.z)), h)
        return (0.1, h)

    write_layers(obj, regions, occlusion)


# ---------------------------------------------------------------- bushes

BUSHES = {
    # masses, leaf scales, little round flowers, young tips, crown triangles, seed. Cushions just overlapping (like the oak's
    # crown) so each one keeps its round outline inside the union.
    "A": (lobes([((0, 0, 0.34), 0.3)]
                + [((math.cos(a) * 0.38, math.sin(a) * 0.38, 0.25), r) for a, r in
                   ((0.2, 0.25), (1.45, 0.23), (2.7, 0.25), (3.95, 0.23), (5.15, 0.24))]
                + [((0.1, 0.05, 0.56), 0.2), ((-0.12, -0.08, 0.52), 0.18)], 0.88), 28, 0, 0.0, 260, 5301),
    "B": (lobes([((-0.5, 0.05, 0.17), 0.22), ((-0.18, 0.22, 0.2), 0.25), ((0.18, 0.2, 0.19), 0.24), ((0.5, -0.02, 0.16), 0.21),
                 ((0.22, -0.22, 0.18), 0.23), ((-0.2, -0.22, 0.19), 0.24), ((0.0, 0.0, 0.34), 0.24)], 0.8),
          20, 6, 0.0, 240, 5302),
    "C": (lobes([((math.cos(a) * 0.24, math.sin(a) * 0.24, 0.26), 0.22) for a in (0.3, 1.87, 3.44, 5.01)]
                + [((math.cos(a) * 0.17, math.sin(a) * 0.17, 0.55), 0.2) for a in (1.0, 3.1, 5.2)]
                + [((0.03, 0.02, 0.76), 0.21)], 0.9), 28, 0, 0.75, 260, 5303),
}


def _flower(bm, parts, bvh, hit, normal, size, spin):
    """A little round flower laid on the crown: five wide round petals (two rim points each, never a tip) around a
    soft domed heart, one fan of 15 triangles. The petal rim hugs the crown 4 mm above it (no gap, no back face)."""
    n = normal.normalized()
    t1 = n.orthogonal().normalized()
    t2 = n.cross(t1)
    rim = []
    for p in range(5):
        th = spin + p * math.tau / 5.0
        for off, rf in ((-0.42, 1.0), (0.42, 1.0), (0.63, 0.7)):    # wide round petals, a shallow notch between
            q = hit + (t1 * math.cos(th + off) + t2 * math.sin(th + off)) * (size * rf)
            h, nn, _, _ = bvh.ray_cast(q + n * 0.05, -n)
            rim.append((h + nn * 0.004) if h is not None else q + n * 0.004)
    start = len(bm.verts)
    heart = bm.verts.new(hit + n * (0.004 + size * 0.2))
    ring = [bm.verts.new(q) for q in rim]
    for j in range(len(ring)):
        fo_face(bm, (heart, ring[j], ring[(j + 1) % len(ring)]), n)
    parts.add("flower", start, len(bm.verts), {"center": hit.copy(), "normal": n, "size": size})


def _flowers(count, seed):
    """count little round amber flowers spread over the upper crown, clear of each other and of the leaf scales."""
    def extra(bm, parts, bvh, lod, masses, center, radii):
        rng = random.Random(seed)
        scales = [(d["base"] + d["along"] * (d["size"] * 0.45), d["size"] * 0.6) for s, e, k, d in parts.ranges
                  if k == "scale"]
        placed = []
        for i in range(count * 24):
            if len(placed) >= count:
                break
            a = i * 2.39996 + rng.uniform(-0.3, 0.3)
            z = rng.uniform(0.3, 0.82)
            d = Vector((math.cos(a) * math.sqrt(1 - z * z), math.sin(a) * math.sqrt(1 - z * z), z))
            origin = center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * 2.2
            hit, normal, _, _ = bvh.ray_cast(origin, -d)
            if hit is None or normal.z < 0.3:
                continue
            size = rng.uniform(0.038, 0.046)
            if any((hit - p).length < (size + s) * 1.7 for p, s in placed):
                continue
            if any((hit - c).length < size + r for c, r in scales):
                continue
            _flower(bm, parts, bvh, hit, normal, size, rng.uniform(0.0, math.tau))
            placed.append((hit, size))
    return extra


def build_bushes(collections):
    out = []
    for kind, (masses, scales, flowers, young, crown_tris, seed) in BUSHES.items():
        painter = bush_painter(seed)
        objs = build_crowned(dict(
            name=f"FO_Bush_{kind}", masses=masses, resolution=0.04, voxel=0.028, smooth=3, sink=0.04,
            lods=[dict(crown=crown_tris, scales=scales)],
            scales=dict(count=scales, size=(0.1, 0.13), lift=(0.02, 0.05), seed=seed, amber=0.0, min_nz=0.05,
                        spacing=0.75),
            extra=_flowers(flowers, seed) if flowers else None, paint=painter), collections, collections)
        if young:
            # Bush C: light young tips on the upper scales (A), the rest of the crown stays deep.
            obj = objs[0]
            attr = obj.data.color_attributes["PW_Regions"]
            top = max(v.co.z for v in obj.data.vertices)
            for v in obj.data.vertices:
                if v.co.z > top * 0.62:
                    r, g, b, a = attr.data[v.index].color
                    attr.data[v.index].color = (r, g, b, max(a, young * smoothstep(top * 0.62, top, v.co.z)))
        out += objs
    return out


# ---------------------------------------------------------------- ferns

def _arch(base, out, length, a0, a1, steps=48):
    """Arching spine: leaves the ground at a0 degrees, curves down to a1 at the tip. Returns at(t)."""
    curve = [Vector((0.0, 0.0))]
    for k in range(steps):
        th = math.radians(a0 + (a1 - a0) * ((k + 0.5) / steps) ** 1.3)
        curve.append(curve[-1] + Vector((math.cos(th), math.sin(th))) * (length / steps))

    def at(t):
        u = min(1.0, max(0.0, t)) * steps
        k = min(int(u), steps - 1)
        c = curve[k].lerp(curve[k + 1], u - k)
        return base + out * c.x + UP * c.y

    return at


def _lobe_profile(lobes, start=0.03, stop=0.84, notch=0.46, stem=0.07, shoulders=((0.33, 0.92), (0.67, 1.0)),
                  tip=((0.48, 0.48),), end=0.17, stagger=False):
    """Rows (t along the frond, left width share, right width share) of a frond edge made of soft round lobes. Each
    lobe is a rounded top of two shoulder rows between notches, so no row ever ends in a point; the frond ends in a
    small round tip lobe cut by a short blunt edge. lobes: (length share, width share) from the base to the tip.
    stagger: the right side runs one row behind the left, its lobes a third of a lobe nearer the tip (subopposite
    leaflets, like a real frond) for the same triangles."""
    total = sum(length for length, _ in lobes)
    seq = []
    t = start
    for i, (length, w) in enumerate(lobes):
        span = (stop - start) * length / total
        for f, share in shoulders:
            seq.append((t + span * f, w * share))
        t += span
        following = lobes[i + 1][1] if i + 1 < len(lobes) else tip[0][1] * 1.4
        seq.append((t, min(w, following) * notch))
    if stagger:
        right = [lobes[0][1] * notch * 0.8] + [w for _, w in seq[:-1]]
    else:
        right = [w for _, w in seq]
    rows = [(0.0, stem, stem)] + [(tt, w, wr) for (tt, w), wr in zip(seq, right)]
    for f, share in tip:
        rows.append((t + (1.0 - t) * f, share, share * (0.92 if stagger else 1.0)))
    rows.append((1.0, end, end))
    return rows


def _frond(bm, parts, base, azimuth, length, width, a0, a1, rows, cup=0.16):
    """One arching frond with a soft lobed edge (rows from _lobe_profile): a strip, double-sided so both culled sides
    stay closed, leaflet edges cupped up a little above the rachis."""
    out = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
    side0 = Vector((-out.y, out.x, 0.0))
    at = _arch(base, out, length, a0, a1)
    reach = max(0.05, (at(1.0) - base).dot(out))
    for face_side in (0, 1):
        start = len(bm.verts)
        lift = UP * (0.0 if face_side == 0 else -0.004)
        verts = []
        for t, wl, wr in rows:
            p = at(t)
            left = p - side0 * (width * wl * 0.5) + UP * (width * wl * cup) + lift
            right = p + side0 * (width * wr * 0.5) + UP * (width * wr * cup) + lift
            verts.append((bm.verts.new(left), bm.verts.new(right)))
        for va, vb in zip(verts[:-1], verts[1:]):
            face = (va[0], va[1], vb[1], vb[0])
            f = bm.faces.new(face if face_side == 0 else tuple(reversed(face)))
            f.normal_update()
            if (f.normal.z < 0) == (face_side == 0):
                f.normal_flip()
        parts.add("frond", start, len(bm.verts), {"base": base, "out": out, "reach": reach, "side": face_side})


def _fiddlehead(bm, parts, base, azimuth, height):
    """A young curled frond: a straight stalk rising into a round spiral of six soft steps (about 330 degrees, the
    radius closing by a tenth per step), a thin 4-sided tube ending in a small cap."""
    out = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
    rr = 0.046
    c = base + out * rr + UP * (height * 0.82)
    pts = [base]
    for j in range(7):
        th = math.pi - j * 0.95
        r = rr * (1.0 - j * 0.09)
        pts.append(c + out * (math.cos(th) * r) + UP * (math.sin(th) * r))
    start = len(bm.verts)
    fo_tube(bm, pts, [0.011, 0.012, 0.0135, 0.014, 0.0135, 0.0125, 0.011, 0.009], 4, cap=True, phase=math.pi / 4)
    parts.add("fiddle", start, len(bm.verts), {"base": base})


def build_ferns(collections):
    out = []
    # A: wide arching rosette, five fronds, each edged with two fully round lobes a side (three shoulder rows each) and
    # a round tip lobe.
    bm = bmesh.new()
    parts = Parts()
    rows = _lobe_profile([(1.0, 0.92), (0.92, 0.84)], stop=0.74, notch=0.52,
                         shoulders=((0.17, 0.76), (0.5, 1.0), (0.83, 0.78)), tip=((0.36, 0.56), (0.72, 0.46)), end=0.18)
    for az, length, a0 in ((0.25, 0.64, 62), (1.5, 0.58, 70), (2.75, 0.66, 58), (3.95, 0.56, 72), (5.1, 0.62, 66)):
        _frond(bm, parts, Vector((math.cos(az) * 0.02, math.sin(az) * 0.02, -0.01)), az, length, 0.29, a0, -24, rows)
    bm.normal_update()
    out.append(_finish_fern("FO_Fern_A", bm, parts, collections))
    # B: young upright fronds (two round lobes a side and a round tip) and one curled fiddlehead.
    bm = bmesh.new()
    parts = Parts()
    rows = _lobe_profile([(1.0, 0.92), (0.9, 0.82)], start=0.06, stop=0.8)
    for az, length, a0 in ((0.6, 0.46, 72), (1.9, 0.42, 76), (3.1, 0.48, 70), (4.3, 0.4, 74), (5.4, 0.44, 72)):
        # bases a little apart and splayed, so the young fronds open like a vase instead of closing into a cup
        _frond(bm, parts, Vector((math.cos(az) * 0.035, math.sin(az) * 0.035, -0.01)), az, length, 0.2, a0, 10, rows)
    _fiddlehead(bm, parts, Vector((0.02, -0.01, -0.01)), 1.2, 0.26)
    bm.normal_update()
    out.append(_finish_fern("FO_Fern_B", bm, parts, collections))
    return out


def _finish_fern(name, bm, parts, collections):
    obj = object_from_bmesh(name, bm, collections)
    table = parts.table(len(obj.data.vertices))
    height = max(v.co.z for v in obj.data.vertices)

    def along(co, data):
        d = co - data["base"]
        return clamp01(Vector((d.x, d.y, 0.0)).dot(data["out"]) / data["reach"])

    def regions(co, n, i):
        kind, data, local = table[i]
        if kind == "fiddle":
            return (0.0, 0.0, 0.0, smoothstep(0.08, 0.2, co.z))
        t = along(co, data)
        stem = 1.0 - smoothstep(0.08, 0.2, t)
        return (0.5 * (1.0 - smoothstep(0.15, 0.45, t)), 1.0 - stem * 0.7, smoothstep(0.62, 0.98, t) * 0.85, 0.0)

    def occlusion(co, n, i):
        kind, data, local = table[i]
        if kind == "fiddle":
            return (0.25 * (1.0 - smoothstep(0.0, 0.1, co.z)), clamp01(co.z / height))
        t = along(co, data)
        return (0.32 * (1.0 - smoothstep(0.0, 0.3, t)), t)

    write_layers(obj, regions, occlusion)
    # Soft lighting: frond normals lean up (like the meadow tufts), so the leaflets never flicker dark; the back copy
    # takes its front's normal (a thin sunlit leaf), so undersides never turn into a dark cup at the heart.
    me = obj.data
    normals = []
    for v in me.vertices:
        kind, data, local = table[v.index]
        if kind == "frond":
            own = v.normal if data["side"] == 0 else -v.normal
            normals.append(own.lerp(UP, 0.55).normalized())
        else:
            normals.append(v.normal.copy())
    me.normals_split_custom_set_from_vertices(normals)
    return obj


# ---------------------------------------------------------------- mushrooms

# Cap profiles (radius share, height share) from the rim up, the apex closing them: plump domes, never a cone.
CAP_DOME = ((1.0, 0.0), (0.95, 0.38), (0.75, 0.74), (0.38, 0.95))     # the toadstool
CAP_BUN = ((1.0, 0.0), (0.88, 0.55), (0.48, 0.9))
CAP_SMALL = ((1.0, 0.0), (0.76, 0.7))


def _mushroom(bm, parts, base, stem_h, stem_r, cap_r, cap_h, segs, stem_sides, cap_kind, profile=CAP_BUN, spots=0,
              seed=0, tilt=Vector((0, 0, 0)), stem_rings=1, gill_ring=True):
    """A round-capped mushroom: slightly bulging stem, smooth domed cap (segs even, 12 to 16), a pale underside (a gill
    ring sloping up to a ring half as fine, or one flat disc on the small ones) and soft round domed dots."""
    axis = (UP + tilt).normalized()
    top = base + axis * stem_h
    start = len(bm.verts)
    if stem_rings == 2:
        fo_tube(bm, [base - axis * 0.01, base + axis * (stem_h * 0.45), top],
                [stem_r * 1.15, stem_r * 0.95, stem_r * 0.85], stem_sides, cap=False)
    else:
        fo_tube(bm, [base - axis * 0.01, top], [stem_r * 1.1, stem_r * 0.85], stem_sides, cap=False)
    parts.add("stem", start, len(bm.verts), {"base": base, "axis": axis})
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    z0 = top - axis * (cap_h * 0.18)
    shape = {"kind": cap_kind, "center": z0, "h": cap_h, "r": cap_r, "axis": axis}

    def ring(radius, zf, count):
        return [bm.verts.new(z0 + (u * math.cos(j / count * math.tau) + w * math.sin(j / count * math.tau)) * radius
                             + axis * (cap_h * zf)) for j in range(count)]

    start = len(bm.verts)
    rings = [ring(cap_r * rf, zf, segs) for rf, zf in profile]
    apex = bm.verts.new(z0 + axis * cap_h)
    for ra, rb in zip(rings[:-1], rings[1:]):
        for j in range(segs):
            m = (j + 1) % segs
            fo_face(bm, (ra[j], ra[m], rb[m], rb[j]), (ra[j].co - z0) + axis * 0.5)
    for j in range(segs):
        fo_face(bm, (rings[-1][j], rings[-1][(j + 1) % segs], apex), axis)
    parts.add("cap", start, len(bm.verts), shape)
    # Underside on its own rim vertices, so the cap keeps its dome normals up to the rim crease.
    start = len(bm.verts)
    rim = ring(cap_r * profile[0][0], profile[0][1], segs)
    if gill_ring:
        half = segs // 2
        inner = ring(stem_r * 1.25, 0.14, half)
        for i in range(half):
            r0, r1, r2 = rim[2 * i], rim[2 * i + 1], rim[(2 * i + 2) % segs]
            i0, i1 = inner[i], inner[(i + 1) % half]
            fo_face(bm, (r0, r1, i0), -axis)
            fo_face(bm, (r1, r2, i1), -axis)
            fo_face(bm, (r1, i1, i0), -axis)
        fo_face(bm, list(reversed(inner)), -axis)
    else:
        fo_face(bm, list(reversed(rim)), -axis)
    parts.add("gills", start, len(bm.verts), {"center": z0, "axis": axis})
    if spots:
        # Soft round dots: a domed eight-point fan per dot, every point laid back on the cap 1.8 mm above it.
        cap_bvh = BVHTree.FromBMesh(bm)
        rng = random.Random(seed)
        for k in range(spots):
            a = k / spots * math.tau + rng.uniform(-0.35, 0.35)
            zf = (0.5, 0.36, 0.62, 0.42, 0.56)[k % 5] if k < spots - 1 else 0.93
            rf = math.sqrt(max(0.0, 1.0 - zf * zf))
            d = (u * math.cos(a) + w * math.sin(a)) * (cap_r * rf) + axis * (cap_h * zf)
            hit, nrm, _, _ = cap_bvh.ray_cast(z0 + d * 3.0, -d.normalized())
            if hit is None:
                continue
            t1 = nrm.orthogonal().normalized()
            t2 = nrm.cross(t1)
            rad = cap_r * (rng.uniform(0.12, 0.15) if k < spots - 1 else 0.1)
            start = len(bm.verts)
            centre = bm.verts.new(hit + nrm * 0.0032)
            pts = []
            for j in range(8):
                q = hit + (t1 * math.cos(j / 8 * math.tau) + t2 * math.sin(j / 8 * math.tau)) * rad
                h2, n2, _, _ = cap_bvh.ray_cast(q + nrm * 0.03, -nrm)
                pts.append(bm.verts.new((h2 + n2 * 0.0018) if h2 is not None else q + nrm * 0.0018))
            for j in range(8):
                fo_face(bm, (centre, pts[j], pts[(j + 1) % 8]), nrm)
            parts.add("spot", start, len(bm.verts), shape)


def build_mushrooms(collections):
    out = []
    specs = {
        "A": [dict(base=Vector((0, 0, 0)), stem_h=0.15, stem_r=0.034, cap_r=0.12, cap_h=0.085, segs=14, stem_sides=7,
                   cap_kind="red", profile=CAP_DOME, stem_rings=2, spots=6, seed=5401)],
        "B": [dict(base=Vector((0.0, 0.0, 0)), stem_h=0.1, stem_r=0.032, cap_r=0.085, cap_h=0.062, segs=12, stem_sides=6,
                   cap_kind="cream", tilt=Vector((0.06, 0.02, 0))),
              dict(base=Vector((0.12, 0.05, 0)), stem_h=0.065, stem_r=0.025, cap_r=0.06, cap_h=0.045, segs=12,
                   stem_sides=6, cap_kind="cream", tilt=Vector((-0.12, -0.05, 0)))],
        "C": [dict(base=Vector((0.0, 0.0, 0)), stem_h=0.09, stem_r=0.018, cap_r=0.05, cap_h=0.04, segs=12, stem_sides=5,
                   cap_kind="red", gill_ring=False, tilt=Vector((-0.08, 0.04, 0))),
              dict(base=Vector((0.07, 0.03, 0)), stem_h=0.065, stem_r=0.015, cap_r=0.04, cap_h=0.032, segs=12,
                   stem_sides=5, cap_kind="amber", profile=CAP_SMALL, gill_ring=False, tilt=Vector((0.15, 0.05, 0))),
              dict(base=Vector((0.02, -0.065, 0)), stem_h=0.05, stem_r=0.013, cap_r=0.034, cap_h=0.028, segs=12,
                   stem_sides=5, cap_kind="redamber", profile=CAP_SMALL, gill_ring=False,
                   tilt=Vector((0.05, -0.18, 0)))],
    }
    for kind, items in specs.items():
        bm = bmesh.new()
        parts = Parts()
        for it in items:
            _mushroom(bm, parts, **it)
        bm.normal_update()
        obj = object_from_bmesh(f"FO_Mushroom_{kind}", bm, collections)
        _paint_mushroom(obj, parts.table(len(obj.data.vertices)))
        out.append(obj)
    return out


def _dome_normal(co, data):
    """Normal of the cap's ellipsoid (radius r, height h) at co: smooth round shading for the cap and its dots."""
    d = co - data["center"]
    a = d.dot(data["axis"])
    radial = d - data["axis"] * a
    n = radial / (data["r"] * data["r"]) + data["axis"] * (max(a, 0.0) / (data["h"] * data["h"]))
    return n.normalized() if n.length > 1e-9 else data["axis"].copy()


def _paint_mushroom(obj, table):
    height = max(v.co.z for v in obj.data.vertices)

    def regions(co, n, i):
        kind, data, local = table[i]
        if kind == "cap":
            top = clamp01((co - data["center"]).dot(data["axis"]) / data["h"])
            ck = data["kind"]
            if ck == "red":
                return (1.0, 0.0, 0.15 * top, 0.0)
            if ck == "amber":
                return (0.0, 0.0, 1.0, 0.0)
            if ck == "redamber":
                return (1.0, 0.0, 0.55, 0.0)
            return (0.0, 1.0, 0.45 * smoothstep(0.3, 1.0, top), 0.0)     # cream bun, toasted top
        if kind == "spot":
            return (1.0, 1.0, 0.0, 0.0)
        if kind == "gills":
            return (0.0, 0.0, 0.0, 1.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        kind, data, local = table[i]
        h = clamp01(co.z / height)
        if kind == "stem":
            return (0.35 * (1.0 - smoothstep(0.0, 0.05, co.z)) + 0.2 * smoothstep(height * 0.5, height * 0.8, co.z), h)
        if kind == "gills":
            return (0.3, h)
        return (0.0, h)

    write_layers(obj, regions, occlusion)
    # Smooth round shading: dome normals on caps and dots, radial on stems, a soft downward underside.
    normals = []
    for v in obj.data.vertices:
        kind, data, local = table[v.index]
        if kind in ("cap", "spot"):
            normals.append(_dome_normal(v.co, data))
        elif kind == "stem":
            d = v.co - data["base"]
            radial = d - data["axis"] * d.dot(data["axis"])
            normals.append(radial.normalized() if radial.length > 1e-9 else v.normal.copy())
        elif kind == "gills":
            d = v.co - data["center"]
            radial = d - data["axis"] * d.dot(data["axis"])
            normals.append((-data["axis"] + radial * 2.0).normalized())
        else:
            normals.append(v.normal.copy())
    obj.data.normals_split_custom_set_from_vertices(normals)


# ---------------------------------------------------------------- build, save, export

def reset_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for data in (bpy.data.meshes, bpy.data.cameras, bpy.data.lights, bpy.data.metaballs):
        for block in list(data):
            if block.users == 0:
                data.remove(block)


def build_forest():
    cols = ensure_collections()
    geo, lod, export = cols["GEO"], cols["LOD"], cols["EXPORT"]
    out = []
    out += build_trees([geo, export], [lod, export])
    out += build_ancient_tree([geo, export], [lod, export])
    out += build_giant_stump([geo, export], [lod, export])
    out += build_bushes([geo, export])
    out += build_ferns([geo, export])
    out += build_mushrooms([geo, export])
    for o in list(cols["WORK"].objects):
        data = o.data
        bpy.data.objects.remove(o)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)
    return report(out)


def report(objs):
    rows = {}
    problems = []
    lod0 = {}
    for obj in objs:
        tris = triangle_count(obj)
        lo, hi = bbox_of(obj)
        entry = REPORT.get(obj.name, {})
        entry.update(tris=tris, size=[round(hi[a] - lo[a], 2) for a in range(3)], min_z=round(lo.z, 3))
        rows[obj.name] = entry
        base = obj.name[:-5] if obj.name[-5:-1] == "_LOD" else obj.name
        lod = int(obj.name[-1]) if obj.name[-5:-1] == "_LOD" else 0
        if lod == 0:
            lod0[base] = tris
            lo_b, hi_b = next(v for k, v in BUDGETS.items() if obj.name.startswith(k))
            if not lo_b <= tris <= hi_b:
                problems.append(f"{obj.name}: {tris} tris outside {lo_b}-{hi_b}")
    for name, entry in rows.items():
        if name[-5:-1] == "_LOD" and name[-1] != "0":
            base, lod = name[:-5], int(name[-1])
            ratio = entry["tris"] / lod0[base]
            entry["ratio"] = round(ratio, 2)
            lo_r, hi_r = LOD_RATIOS[lod]
            if not lo_r <= ratio <= hi_r:
                problems.append(f"{name}: {ratio:.2f} of LOD0 outside {lo_r}-{hi_r}")
    return {"pieces": rows, "problems": problems}


def save_forest():
    path = os.path.join(PROJECT, *FO_BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    clear_previews()
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    return path


def export_forest():
    clear_previews()
    return export_static(FO_FBX, FO_PREFIXES)


def clear_previews():
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]


# ---------------------------------------------------------------- review images (temporary, never saved)

def _linear(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def paint_preview(obj, row_key):
    """WORK_Preview (point color) = what SH_EnvironmentOpaque computes from the regions, before lighting."""
    pal = [_linear(h) for h in FOREST_PALETTES[row_key]]
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Occlusion"]
    occ = [0.0] * len(me.vertices)
    for loop in me.loops:
        occ[loop.vertex_index] = uv.data[loop.index].uv[0]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for v in me.vertices:
        r, g, b, a = regions.data[v.index].color
        c = pal[0].copy()
        for weight, col in ((r, pal[1]), (g, pal[2]), (b, pal[3]), (a, pal[4])):
            c = c.lerp(col, weight)
        c = c.lerp(pal[5], occ[v.index])
        prev.data[v.index].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev


RABBIT = ("SKM_Rabbit_LOD0", ("B07A52", "F2B8B5", "F3E3CC", "E98A93", "1E1414"))


def _load_rabbit(collection):
    path = os.path.join(PROJECT, "SourceArt", "Blender", "Animals", "Rabbit", "BL_Animal_Rabbit.blend")
    try:
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.objects = [n for n in src.objects if n == RABBIT[0]]
        obj = dst.objects[0]
    except Exception as exc:  # the scale reference is optional
        print("[pw_forest] rabbit reference unavailable:", exc)
        return None
    collection.objects.link(obj)
    obj.parent = None
    for m in list(obj.modifiers):
        obj.modifiers.remove(m)
    obj.matrix_world = Matrix.Identity(4)
    cols = [_linear(h) for h in RABBIT[1]]
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for i, data in enumerate(regions.data):
        c = cols[0].copy()
        for weight, colr in zip(data.color, cols[1:]):
            c = c.lerp(colr, weight)
        prev.data[i].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev
    return obj


def render_forest_previews(out_dir, sheet_path=None, tile=512):
    """One image per piece (LOD0, three-quarter view, low pitch for the silhouette), the LOD steps of each tree, a
    lineup at the refuge camera's 40 degree pitch with the rabbit, and the review sheet. Nothing is saved."""
    import numpy as np
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    shots = bpy.data.collections.new("WORK_Review")
    scene.collection.children.link(shots)
    mat = bpy.data.materials.new("WORK_Preview_Material")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    attr = mat.node_tree.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "WORK_Preview"
    mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.12
    ground_mat = bpy.data.materials.new("WORK_Ground_Material")
    try:
        ground_mat.use_nodes = True
    except Exception:
        pass
    gb = next(n for n in ground_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Base Color"].default_value = tuple(_linear("6F9450")) + (1.0,)
    gb.inputs["Roughness"].default_value = 0.95

    world = bpy.data.worlds.new("WORK_World")
    try:
        world.use_nodes = True
    except Exception:
        pass
    nt = world.node_tree
    nt.nodes.clear()
    coords, sep = nt.nodes.new("ShaderNodeTexCoord"), nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp, bg, wout = nt.nodes.new("ShaderNodeValToRGB"), nt.nodes.new("ShaderNodeBackground"), nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(coords.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    nt.links.new(bg.outputs[0], wout.inputs[0])
    ramp.color_ramp.elements[0].color = tuple(_linear("DCE6D2")) + (1.0,)
    ramp.color_ramp.elements[1].position = 0.5
    ramp.color_ramp.elements[1].color = tuple(_linear("9CC3D8")) + (1.0,)
    bg.inputs[1].default_value = 1.0
    scene.world = world
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    engine = next((e for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT") if e in engines), "BLENDER_WORKBENCH")
    scene.render.engine = engine
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = 'PNG'
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 32
    if engine == "BLENDER_WORKBENCH":
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'VERTEX'

    pieces = [o for o in bpy.data.collections["EXPORT"].all_objects if o.type == 'MESH']
    for o in pieces:
        paint_preview(o, palette_of(o.name))
        o.data.materials.clear()
        o.data.materials.append(mat)
    temp = []

    def keep(obj):
        shots.objects.link(obj)
        temp.append(obj)
        return obj

    bpy.ops.mesh.primitive_plane_add(size=80.0, location=(0, 0, 0))
    ground = bpy.context.active_object
    for c in list(ground.users_collection):
        c.objects.unlink(ground)
    keep(ground)
    ground.data.materials.append(ground_mat)
    rabbit = _load_rabbit(shots)
    if rabbit is not None:
        rabbit.data.materials.clear()
        rabbit.data.materials.append(mat)
        temp.append(rabbit)
    cam_data = bpy.data.cameras.new("WORK_Camera")
    cam = keep(bpy.data.objects.new("WORK_Camera", cam_data))
    scene.camera = cam
    sun_data = bpy.data.lights.new("WORK_Sun", 'SUN')
    sun_data.energy = 3.4
    sun_data.color = (1.0, 0.95, 0.86)
    sun_data.angle = math.radians(8.0)
    sun = keep(bpy.data.objects.new("WORK_Sun", sun_data))

    def show(objs):
        visible = set(objs) | {ground, cam, sun}
        for o in scene.objects:
            o.hide_render = o not in visible

    def aim(objs, pitch, yaw, width, height, ortho=None, margin=1.08):
        bpy.context.view_layer.update()
        pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        center = (lo + hi) * 0.5
        radius = (hi - lo).length * 0.5
        p, y = math.radians(pitch), math.radians(yaw)
        direction = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), -math.sin(p)))
        scene.render.resolution_x, scene.render.resolution_y = width, height
        cam_data.clip_start, cam_data.clip_end = 0.02, 200.0
        if ortho:
            cam_data.type = 'ORTHO'
            cam_data.sensor_fit = 'AUTO'
            cam_data.ortho_scale = ortho
            cam.location = center - direction * 60.0
        else:
            cam_data.type = 'PERSP'
            cam_data.sensor_fit = 'VERTICAL'
            cam_data.angle_y = math.radians(30.0)
            cam.location = center - direction * (radius * margin / math.sin(math.radians(15.0)))
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        right = direction.cross(UP).normalized()
        toward_sun = (-right * 0.7 - direction * 0.5 + UP * 1.3).normalized()
        sun.rotation_euler = (-toward_sun).to_track_quat('-Z', 'Y').to_euler()

    def shoot(name):
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        return scene.render.filepath

    tiles = []
    by_name = {o.name: o for o in pieces}
    order = ["FO_Tree_A_LOD0", "FO_Tree_B_LOD0", "FO_Tree_C_LOD0", "FO_Tree_D_LOD0", "FO_Landmark_AncientTree_LOD0",
             "FO_Landmark_GiantStump_LOD0", "FO_Bush_A", "FO_Bush_B", "FO_Bush_C", "FO_Fern_A", "FO_Fern_B",
             "FO_Mushroom_A", "FO_Mushroom_B", "FO_Mushroom_C"]
    for name in order:
        obj = by_name[name]
        show([obj])
        aim([obj], 14.0 if "Tree" in name else 38.0 if "Fern" in name else 24.0, 22.0, tile, tile,
            margin=0.95 if "Tree" in name else 0.8)
        tiles.append(shoot(f"FO_{name}.png"))
    # LOD steps: each tree's LOD0, LOD1, LOD2 side by side (copies sharing the mesh data).
    lod_tile = None
    for base in ("FO_Tree_A", "FO_Tree_B", "FO_Tree_C", "FO_Tree_D", "FO_Landmark_AncientTree", "FO_Landmark_GiantStump"):
        copies = []
        width = max(by_name[base + "_LOD0"].dimensions.x, 2.0) * 1.15
        for lod in range(3):
            src = by_name[f"{base}_LOD{lod}"]
            dup = keep(bpy.data.objects.new("WORK_" + src.name, src.data))
            dup.location = ((lod - 1) * width, 0.0, 0.0)
            copies.append(dup)
        show(copies)
        aim(copies, 14.0, 0.0, tile * 2, tile, margin=0.62)
        path = shoot(f"FO_{base}_LODs.png")
        if base == "FO_Tree_A":
            lod_tile = path
    # Lineup at the refuge camera pitch (40 degrees) with the rabbit for scale.
    lineup = []
    x = 0.0
    for name in order:
        src = by_name[name]
        w = src.dimensions.x
        dup = keep(bpy.data.objects.new("WORK_Line_" + name, src.data))
        x += w * 0.5 + 0.25
        lo, hi = bbox_of(src)
        dup.location = (x - (lo.x + hi.x) * 0.5, 0.0, 0.0)
        x += w * 0.5 + 0.25
        lineup.append(dup)
    if rabbit is not None:
        rabbit.location = (-0.7, 0.4, 0.0)
        lineup.append(rabbit)
    show(lineup)
    aim(lineup, 40.0, 0.0, tile * 5, tile, ortho=x + 1.0)
    line_path = shoot("FO_lineup_refuge_pitch.png")
    # Silhouettes (64 px tall each), dark on light: the four trees and the ancient tree must read apart.
    show(lineup[:5])
    aim(lineup[:5], 2.0, 0.0, 640, 128, ortho=sum(o.dimensions.x for o in lineup[:5]) + 3.0)
    for o in lineup[:5]:
        o.data.materials.clear()
    bsdf.inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1.0)
    mat.node_tree.links.remove(next(l for l in mat.node_tree.links if l.to_socket == bsdf.inputs["Base Color"]))
    ground.hide_render = True
    for o in lineup[:5]:
        o.data.materials.append(mat)
    shoot("FO_silhouettes.png")
    ground.hide_render = False

    if sheet_path:
        cols, rows = 5, 3
        H, W = rows * tile + tile, cols * tile
        canvas = np.ones((H, W, 4), np.float32) * np.array([0.86, 0.9, 0.84, 1.0], np.float32)

        def load(path):
            img = bpy.data.images.load(path, check_existing=False)
            w, h = img.size
            px = np.empty(w * h * 4, np.float32)
            img.pixels.foreach_get(px)
            bpy.data.images.remove(img)
            return px.reshape(h, w, 4)

        for idx, path in enumerate(tiles + ([lod_tile] if lod_tile else [])):
            r, c = divmod(idx, cols)
            px = load(path)
            h, w = px.shape[:2]
            y0 = H - (r + 1) * tile
            if w > tile:   # the LOD strip: keep its middle part, scaled to one tile by sampling
                xs = np.linspace(0, w - 1, tile).astype(int)
                ys = np.linspace(0, h - 1, tile // 2).astype(int)
                small = px[ys][:, xs]
                canvas[y0 + tile // 4:y0 + tile // 4 + tile // 2, c * tile:(c + 1) * tile] = small
            else:
                canvas[y0:y0 + tile, c * tile:(c + 1) * tile] = px
        canvas[0:tile, :] = load(line_path)
        out = bpy.data.images.new("WORK_Sheet", W, H, alpha=False)
        out.pixels.foreach_set(canvas.ravel())
        out.filepath_raw = sheet_path
        out.file_format = 'PNG'
        out.save()
        bpy.data.images.remove(out)
    return tiles


# ---------------------------------------------------------------- command line

def _main(argv):
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    result = {}
    if "--build" in args:
        reset_scene()
        result = build_forest()
        ancient = bpy.data.objects.get("FO_Landmark_AncientTree_LOD0")
        if ancient is not None:
            best = owl_perch(ancient)
            if best:
                _, top, room = best
                # Unity local position of the perch (Blender (x, y, z) -> Unity (-x, z, -y)).
                result["owl_perch_unity"] = [round(-top.x, 2), round(top.z, 2), round(-top.y, 2)]
                result["owl_perch_room_above"] = round(room, 2)
        if "--nosave" not in args:     # --nosave: iterate on the look without touching the blend or the FBX
            result["blend"] = save_forest()
            result["fbx"] = export_forest()
    print("[pw_forest] " + json.dumps(result, indent=1, default=str))
    if "--preview" in args:
        out_dir = args[args.index("--preview") + 1]
        if "--build" not in args:
            bpy.ops.wm.open_mainfile(filepath=os.path.join(PROJECT, *FO_BLEND.split("/")))
        sheet = args[args.index("--sheet") + 1] if "--sheet" in args else os.path.join(PROJECT, *FO_SHEET.split("/"))
        render_forest_previews(out_dir, sheet)
        result["previews"] = out_dir
    print("[pw_forest] " + json.dumps(result, indent=1, default=str))


if __name__ == "__main__":
    _main(sys.argv)
