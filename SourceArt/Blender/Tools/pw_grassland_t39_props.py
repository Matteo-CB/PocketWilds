"""Grassland T39 (Cache-cache, doc 01 Contenu S-ACT-002; doc Mini-jeux S-ACT-002): the Prairie's hiding decor.

Doc 01 gives the Prairie theme of the hide-and-seek: « hautes herbes, derrière les petits arbres, derrière les buissons
fleuris ». The game plays in the refuge, so these are permanent props of the eight Prairie chunks, placed among the
T33 kit (trees, GL_Bush_A/B/C, rocks, the instanced meadow tufts) by the chunk pipeline.

Intention (charte « fait main », section 10, step 1)
- Character: lush, soft, a little plump. A tall grass clump reads as one generous green tuft a rabbit can sit behind,
  never as a field of thin sticks; a flowered bush is a meadow bush (GL_Bush_A/B) wearing round daisies.
- Scale, measured read-only on the LOD0 models: rabbit 0.30 m wide, 0.51 m long, 0.65 m with its ears; squirrel
  0.54 m tall, 0.62 m long with its tail; hedgehog 0.32 m tall. GL_Bush_A 0.96 x 0.78 x 0.57 m, GL_Bush_C 0.79 m
  tall, GL_GrassWisp_L 0.54 m, GL_GrassTuft_L 0.31 m. The tall grass stands 0.45 to 0.6 m (taller and much fuller
  than the meadow's own tufts), the flowered bushes 0.55 to 0.7 m, like the meadow bushes.
- Counted parts, the same in every export: every tall grass clump has 28 blades (4 upright in the middle, 6 fans of
  3, 6 short skirt blades); A wears 3 pink clovers, B 3 round seed heads, C 2 white clovers and 1 pink; every daisy
  has 5 round petals and a domed heart; bush A 8 daisies, bush B 7; 16 leaf scales on each bush.
- Wanted imperfections: blades lean, arch, cross and twist by fixed seeds; the crowns are unions of uneven lobes.
- Nothing spiky: the blades are wide and end in a soft ogive (widest low, rounded shoulders before the tip), never a
  needle; clover heads and seed heads are balls, daisy petals are round lobes.
- Budgets (triangles): tall grass 500 each, flowered bush 650 each.

Data contract: as pw_grassland.py and pw_grassland_t33.py (one palette row per renderer): color attribute PW_Regions
(R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion, y height
factor: 0 at the ground, 1 at the tips, read by SH_FoliageWind for wind). Tall grass regions follow the meadow grass
(base deep green, R lighter tips, G straw seed heads) plus B pink clover heads, A white clover heads. Flowered bushes
follow GL_Bush (R foliage, G sunlit, B shaded, A blossoms = the daisy petals); the bush has no bark, so the base
column is the daisy hearts. T39_PALETTES holds the proposed rows (base, R, G, B, A, occlusion, accent).

Axes: pw_export.py writes Blender (x, y, z) as Unity (-x, z, -y). Pivots at the center of the base on the ground.

Usage inside Blender (the live session or a fresh file), after pw_grassland.py and pw_grassland_t33.py in the same
namespace:
    ns["build_t39_props"]()          # scene "HideSeek": GL_TallGrass_A/B/C, GL_FlowerBush_A/B in its export collection
    ns["write_t39_blend"]()          # bpy.data.libraries.write: the scene to BL_Biome_Grassland_T39.blend
Then in BL_Biome_Grassland_T39.blend (headless is fine; nothing is saved), after pw_export.py in the same namespace:
    ns["export_t39"]()               # Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T39.fbx
    ns["render_t39_previews"](out_dir, refs)   # review images; temporary camera, light, materials
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

T39_SCENE = "HideSeek"
T39_COLLECTION = "GL39_EXPORT"      # EXPORT is taken in the Prairie file; export_t39 reads either name
T39_WORK = "GL39_WORK"
T39_BLEND = "SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_T39.blend"
T39_FBX = "Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T39.fbx"
T39_BUDGETS = {"GL_TallGrass_": 500, "GL_FlowerBush_": 650}

# Proposed palette rows (base, R, G, B, A, occlusion, accent), sRGB hex like DEF_Palettes_Grassland.
T39_PALETTES = {
    # Meadow grass (palette.grassland.grass) with its clovers: B pink heads, A white heads.
    "palette.grassland.tall_grass": ("66A253", "9FCE78", "D0CB9A", "F3C4D6", "F8F4EA", "476F3E", "B9D56C"),
    # Meadow bush foliage (palette.grassland.bush_blossom); base the daisy hearts, A the petals.
    "palette.grassland.flower_bush_pink": ("F2C94C", "73B25A", "A2D27E", "4F8A46", "F3BDD3", "355E33", "FFFFFF"),
    "palette.grassland.flower_bush_cream": ("F2C94C", "73B25A", "A2D27E", "4F8A46", "F8F1DD", "355E33", "FFFFFF"),
    "palette.grassland.flower_bush_yellow": ("F29C4C", "73B25A", "A2D27E", "4F8A46", "F7DF7A", "355E33", "FFFFFF"),
}

UP = Vector((0.0, 0.0, 1.0))


# ---------------------------------------------------------------- scene

def _t39_scene():
    """The HideSeek scene, its export collection and a work collection; the scene is shown in the window so its
    depsgraph evaluates the crowns. No other scene's objects are touched."""
    scene = bpy.data.scenes.get(T39_SCENE) or bpy.data.scenes.new(T39_SCENE)
    cols = []
    for name in (T39_COLLECTION, T39_WORK):
        col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if col.name not in scene.collection.children:
            scene.collection.children.link(col)
        cols.append(col)
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    return scene, cols[0], cols[1]


def _t39_replace(name):
    old = bpy.data.objects.get(name)
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


class _T39Parts:
    """Which part each vertex belongs to while a bmesh is built (to_mesh keeps the vertex order)."""

    def __init__(self, bm):
        self.bm = bm
        self.tags = []
        self.data = []

    def mark(self, tag, data=None):
        n = len(self.bm.verts) - len(self.tags)
        self.tags.extend([tag] * n)
        self.data.extend([data] * n)


def _ellipsoid(bm, center, radii, segments, rings, axis=UP):
    """UV ellipsoid around `axis` (radii.z along it). Faces wind outward."""
    w = axis.normalized()
    u = w.orthogonal().normalized()
    v = w.cross(u).normalized()

    def point(x, y, z):
        return center + u * (x * radii.x) + v * (y * radii.y) + w * (z * radii.z)

    top = bm.verts.new(point(0.0, 0.0, 1.0))
    grid = []
    for j in range(1, rings):
        phi = math.pi * j / rings
        grid.append([bm.verts.new(point(math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)))
                     for th in (math.tau * k / segments for k in range(segments))])
    bottom = bm.verts.new(point(0.0, 0.0, -1.0))
    for k in range(segments):
        m = (k + 1) % segments
        bm.faces.new((top, grid[0][k], grid[0][m]))
        bm.faces.new((bottom, grid[-1][m], grid[-1][k]))
    for a, b in zip(grid, grid[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            bm.faces.new((a[k], b[k], b[m], a[m]))


def _stalk(bm, a, b, radius):
    """Open three-sided stalk from a to b (its ends are hidden in the ground and in a head)."""
    axis = (b - a).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u).normalized()
    ra = [bm.verts.new(a + (u * math.cos(t) + w * math.sin(t)) * radius) for t in (k / 3 * math.tau for k in range(3))]
    rb = [bm.verts.new(b + (u * math.cos(t) + w * math.sin(t)) * radius * 0.8) for t in (k / 3 * math.tau for k in range(3))]
    for k in range(3):
        m = (k + 1) % 3
        bm.faces.new((ra[k], ra[m], rb[m], rb[k]))


# ---------------------------------------------------------------- tall grass

BLADE_ROWS = {
    # rows along the blade (t, width factor), then the tip at t = 1: gathered at the root, widest low, a soft ogive
    5: ((0.0, 0.5), (0.3, 1.0), (0.6, 0.92), (0.82, 0.68), (0.94, 0.38)),     # 18 triangles
    4: ((0.0, 0.5), (0.4, 1.0), (0.74, 0.8), (0.92, 0.42)),                  # 14 triangles
    3: ((0.0, 0.55), (0.5, 1.0), (0.84, 0.55)),                              # 10 triangles
}


def _blade_point(root, out, length, lean, t):
    """Center of the blade at t: it rises, then arches outward (reach length * lean at the tip, which sinks with
    the arch)."""
    return root + out * (length * lean * t ** 2.2) + UP * (length * t * (1.0 - 0.5 * lean * t ** 1.5))


def _soft_blade(bm, root, out, length, width, lean, twist, rows=5):
    """A chunky double-sided grass blade ending in a soft ogive (3, 4 or 5 rows and an apex: 10, 14 or 18 triangles)."""
    out = Vector((out.x, out.y, 0.0)).normalized()
    pairs = []
    for t, f in BLADE_ROWS[rows]:
        c = _blade_point(root, out, length, lean, t)
        a = twist * t
        side = Vector((-out.y, out.x, 0.0)) * math.cos(a) + out * math.sin(a) * 0.35
        side = side.normalized() * (width * 0.5 * f)
        pairs.append((c - side, c + side))
    tip = _blade_point(root, out, length, lean, 1.0)
    for face_side in (0, 1):
        verts = [(bm.verts.new(a), bm.verts.new(b)) for a, b in pairs]
        apex = bm.verts.new(tip)
        for k in range(len(verts) - 1):
            a0, b0 = verts[k]
            a1, b1 = verts[k + 1]
            bm.faces.new((a0, b0, b1, a1) if face_side == 0 else (a1, b1, b0, a0))
        a, b = verts[-1]
        bm.faces.new((a, b, apex) if face_side == 0 else (apex, b, a))
    return tip


def _clover(bm, parts, base, head, radius, kind):
    """A meadow clover at the foot of the clump: a stalk and a round head, 30 triangles (the clump's skirt blades
    stand in for its leaves)."""
    _stalk(bm, base, head - UP * radius * 0.6, 0.0042)
    parts.mark("stalk")
    _ellipsoid(bm, head, Vector((radius, radius, radius * 0.88)), 6, 3)
    parts.mark(kind)


def _seed_head(bm, parts, base, head, radius):
    """A round seed head on a slim stalk rising a little above the blades (straw, region G), 30 triangles."""
    _stalk(bm, base, head - UP * radius * 0.8, 0.004)
    parts.mark("stalk")
    _ellipsoid(bm, head, Vector((radius, radius, radius * 1.15)), 6, 3)
    parts.mark("seed")


# Clumps. inner: (count, length, lean, width) of the upright middle blades; groups: (count, root radius) of the
# blade fans around them, each a center blade (length, lean, width) and two side blades (length, lean, width);
# skirt: (count, root radius, length, lean, width) of the short blades arching low that fill the clump's foot;
# extras at the foot (kind, azimuth deg, radius, head height); squash (x, y) of the footprint (C: low and long).
TALL_GRASS = {
    "A": dict(inner=(4, 0.6, 0.15, 0.095), groups=(6, 0.075), center=(0.52, 0.36, 0.09), side=(0.45, 0.46, 0.085),
              skirt=(6, 0.09, 0.3, 0.5, 0.08),
              extras=[("clover_pink", 30, 0.17, 0.07), ("clover_pink", 150, 0.165, 0.085), ("clover_pink", 265, 0.175, 0.065)],
              seed=3901, squash=(1.0, 1.0)),
    "B": dict(inner=(4, 0.55, 0.18, 0.095), groups=(6, 0.08), center=(0.48, 0.4, 0.09), side=(0.42, 0.5, 0.085),
              skirt=(6, 0.095, 0.29, 0.52, 0.08),
              extras=[("seed", 10, 0.04, 0.55), ("seed", 135, 0.035, 0.525), ("seed", 250, 0.04, 0.565)],
              seed=3902, squash=(1.0, 1.0)),
    "C": dict(inner=(4, 0.5, 0.2, 0.095), groups=(6, 0.085), center=(0.44, 0.42, 0.09), side=(0.38, 0.52, 0.085),
              skirt=(6, 0.1, 0.27, 0.54, 0.08),
              extras=[("clover_white", 20, 0.19, 0.07), ("clover_white", 200, 0.195, 0.08), ("clover_pink", 110, 0.15, 0.065)],
              seed=3903, squash=(1.42, 0.85)),
}
ROOT_Z = -0.02          # blades start 2 cm under the ground: the clump is rooted, never set on the grass


def build_tall_grass(kind, col):
    """GL_TallGrass_<kind>: a lush clump of soft rounded blades in groups, clovers or seed heads at its foot.
    Regions: base deep green, R lighter tips, G straw seed heads, B pink clover heads, A white clover heads.
    uv PW_Occlusion: x dark at the roots and inside the clump, y 0 at the ground to 1 at each blade's tip."""
    spec = TALL_GRASS[kind]
    rng = random.Random(spec["seed"])
    sx, sy = spec["squash"]
    bm = bmesh.new()
    parts = _T39Parts(bm)
    blades = []

    def squash(v):
        return Vector((v.x * sx, v.y * sy, v.z))

    def blade(root, az, length, lean, width, rows, inner):
        out = Vector((math.cos(az), math.sin(az), 0.0))
        tip = _soft_blade(bm, squash(root) + UP * ROOT_Z, squash(out), length, width, lean, rng.uniform(-0.6, 0.6), rows)
        parts.mark("blade", (len(blades), inner, tip.z))
        blades.append(tip)

    count, length, lean, width = spec["inner"]
    for k in range(count):
        az = (k + rng.uniform(-0.25, 0.25)) / count * math.tau
        root = Vector((math.cos(az), math.sin(az), 0.0)) * rng.uniform(0.015, 0.035)
        blade(root, az, length * rng.uniform(0.92, 1.0), lean * rng.uniform(0.8, 1.2), width, 5, True)
    groups, r0 = spec["groups"]
    for k in range(groups):
        az = (k + 0.5 + rng.uniform(-0.15, 0.15)) / groups * math.tau
        d = Vector((math.cos(az), math.sin(az), 0.0))
        root = d * r0 * rng.uniform(0.9, 1.1)
        c_len, c_lean, c_w = spec["center"]
        # Each fan leans a little off its root direction: neighbouring fans cross instead of reading as a star.
        cross = rng.uniform(-0.3, 0.3)
        blade(root, az + cross, c_len * rng.uniform(0.94, 1.06), c_lean * rng.uniform(0.9, 1.1), c_w, 4, False)
        s_len, s_lean, s_w = spec["side"]
        for sign in (-1.0, 1.0):
            side = Vector((-d.y, d.x, 0.0)) * (0.016 * sign)
            spread = math.radians(rng.uniform(14.0, 20.0)) * sign
            blade(root + side, az + cross + spread, s_len * rng.uniform(0.92, 1.08), s_lean * rng.uniform(0.9, 1.1), s_w, 4,
                  False)
    count, r1, length, lean, width = spec["skirt"]
    for k in range(count):
        az = (k + 0.25 + rng.uniform(-0.12, 0.12)) / count * math.tau
        root = Vector((math.cos(az), math.sin(az), 0.0)) * r1 * rng.uniform(0.9, 1.1)
        blade(root, az, length * rng.uniform(0.9, 1.1), lean * rng.uniform(0.92, 1.08), width, 3, False)
    blade_height = max(t.z for t in blades)
    blades_bvh = BVHTree.FromBMesh(bm)

    def clear_of_blades(base, head, radius):
        """The head keeps 4 mm of air around it and its stalk 3 mm: a flower never passes through a blade."""
        if blades_bvh.find_nearest(head)[3] < radius * 1.15 + 0.004:
            return False
        return all(blades_bvh.find_nearest(base.lerp(head, s / 6.0))[3] > 0.007 for s in range(1, 6))

    for extra, az_deg, r, h in spec["extras"]:
        a = math.radians(az_deg)
        d = Vector((math.cos(a), math.sin(a), 0.0))
        # Moved out from the clump 5 mm at a time until it stands free (measured, never guessed).
        for step in range(30):
            rr = r + 0.005 * step
            if extra == "seed":
                base, head, radius = squash(d * rr) + UP * ROOT_Z, squash(d * (rr + 0.05)) + UP * h, 0.02
            else:
                base, head, radius = squash(d * (rr - 0.02)) + UP * ROOT_Z, squash(d * rr) + UP * h, 0.032
            if clear_of_blades(base, head, radius):
                break
        else:
            raise RuntimeError(f"GL_TallGrass_{kind}: no free room for the {extra} at {az_deg} degrees")
        if extra == "seed":
            _seed_head(bm, parts, base, head, radius)
        else:
            _clover(bm, parts, base, head, radius, extra)
    bm.normal_update()
    tips_z = max(v.co.z for v in bm.verts)
    reach = max(math.hypot(v.co.x / sx, v.co.y / sy) for v in bm.verts)

    _t39_replace(f"GL_TallGrass_{kind}")
    obj = object_from_bmesh(f"GL_TallGrass_{kind}", bm, [col])
    me = obj.data
    # Soft dome normals (like GL_GrassTuft, scaled to the clump): it lights as one plush tuft, never blade by blade.
    normals = []
    for v in me.vertices:
        tag = parts.tags[v.index]
        if tag in ("clover_pink", "clover_white", "seed"):
            normals.append(v.normal.lerp(UP, 0.35).normalized())
            continue
        radial = Vector((v.co.x / sx, v.co.y / sy, 0.0)) / max(reach, 1e-4)
        normals.append((radial * 0.85 + UP * (0.75 + 0.5 * min(1.0, max(0.0, v.co.z) / blade_height))).normalized())
    me.normals_split_custom_set_from_vertices(normals)

    def blade_t(co, i):
        index, inner, tip = parts.data[i]
        return max(0.0, min(1.0, (co.z - ROOT_Z) / (tip - ROOT_Z)))

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "blade":
            index = parts.data[i][0]
            t = blade_t(co, i)
            # Lighter tips, a little less on every third blade so neighbours never read stamped.
            return (smoothstep(0.12, 1.0, t) * (0.8 if index % 3 == 1 else 1.0), 0.0, 0.0, 0.0)
        if tag == "seed":
            return (0.0, 1.0, 0.0, 0.0)
        if tag == "clover_pink":
            return (0.0, 0.0, 1.0, 0.0)
        if tag == "clover_white":
            return (0.0, 0.0, 0.0, 1.0)
        return (0.25, 0.2, 0.0, 0.0)          # stalks: a little lighter, a hint of straw

    def occlusion(co, n, i):
        tag = parts.tags[i]
        root = 0.66 * (1.0 - smoothstep(ROOT_Z, ROOT_Z + 0.26, co.z))
        if tag == "blade":
            inner = parts.data[i][1]
            t = blade_t(co, i)
            deep = 0.14 * (1.0 - smoothstep(0.3, 0.85, t)) if inner else 0.0
            return (root + deep, t)
        height = min(1.0, max(0.0, co.z) / tips_z)
        if tag in ("clover_pink", "clover_white", "seed"):
            return (0.04, height)
        return (root, height)

    write_layers(obj, regions, occlusion)
    return obj


# ---------------------------------------------------------------- flowered bushes

FLOWER_BUSHES = {
    # masses (center, radius), leaf scales, daisies, daisy radius, crown triangles, seed
    "A": ([(Vector((0.0, 0.0, 0.33)), 0.34), (Vector((0.22, 0.08, 0.27)), 0.25), (Vector((-0.21, -0.05, 0.27)), 0.26),
           (Vector((0.02, -0.19, 0.28)), 0.24), (Vector((-0.04, 0.19, 0.29)), 0.24), (Vector((0.03, 0.03, 0.5)), 0.24)],
          16, 8, 0.058, 210, 3911),
    "B": ([(Vector((-0.19, 0.0, 0.28)), 0.29), (Vector((0.2, 0.04, 0.26)), 0.28), (Vector((0.0, -0.17, 0.24)), 0.22),
           (Vector((0.02, 0.18, 0.25)), 0.22), (Vector((-0.12, 0.02, 0.49)), 0.2), (Vector((0.15, -0.02, 0.47)), 0.19)],
          16, 7, 0.056, 210, 3912),
}
PETALS = 5


def _crown_t39(masses, name, work, resolution=0.05, voxel=0.035, squash=0.86, stiffness=3.0):
    """pw_grassland_t33._crown in the HideSeek scene's own work collection: stiff metaball union, voxel remesh,
    smoothing."""
    data = bpy.data.metaballs.new("WORK_" + name)
    data.resolution = resolution
    data.render_resolution = resolution
    for center, radius in masses:
        e = data.elements.new(type='ELLIPSOID')
        e.co = center
        e.radius = radius * 1.35
        e.size_x, e.size_y, e.size_z = 1.0, 1.0, squash
        e.stiffness = stiffness
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
    smooth.iterations = 5
    replace_with_evaluated(temp, smooth)
    return temp


def _daisy_outline(center, normal, radius, turn, cup):
    """Five round petals: 4 outline points each (valley, shoulder, tip, shoulder), slightly cupped."""
    u = normal.orthogonal().normalized()
    w = normal.cross(u).normalized()
    pts = []
    step = math.tau / PETALS
    for k in range(PETALS):
        a0 = turn + k * step
        for da, r in ((0.0, 0.4), (0.2, 0.88), (0.5, 1.0), (0.8, 0.88)):
            a = a0 + da * step
            pts.append(center + (u * math.cos(a) + w * math.sin(a)) * (radius * r) + normal * (cup * radius * r * r))
    return pts, u, w


def _add_daisy(bm, center, normal, radius, turn):
    """A round daisy laid on the crown: petal disc (lens, 40 triangles) and a domed heart (6 triangles).
    Returns the vertex counts of the petals and the heart."""
    outline, u, w = _daisy_outline(center, normal, radius, turn, 0.16)
    ring = [bm.verts.new(p) for p in outline]
    top = bm.verts.new(center + normal * radius * 0.1)
    bottom = bm.verts.new(center - normal * radius * 0.14)
    n = len(ring)
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((top, ring[k], ring[m]))
        bm.faces.new((bottom, ring[m], ring[k]))
    petals = n + 2
    hr = radius * 0.34
    lift = radius * 0.1 * (1.0 - 0.34) + radius * 0.035
    heart = [bm.verts.new(center + (u * math.cos(t) + w * math.sin(t)) * hr + normal * lift)
             for t in (turn + j / 6 * math.tau for j in range(6))]
    apex = bm.verts.new(center + normal * (radius * 0.1 + radius * 0.2))
    for j in range(6):
        bm.faces.new((apex, heart[j], heart[(j + 1) % 6]))
    return petals, 7


def _inside(bvh, p, margin=0.0):
    loc, nor, _, dist = bvh.find_nearest(p)
    if loc is None:
        return False
    return (p - loc).dot(nor) < margin


def build_flower_bush(kind, col, work):
    """GL_FlowerBush_<kind>: a meadow bush (GL_Bush_A/B method: lobed crown, leaf scales, crown-centered normals,
    the same foliage painting) wearing round daisies laid on the crown, never inside it nor on a leaf scale.
    Regions: R foliage, G sunlit, B shaded, A daisy petals; daisy hearts take the base color."""
    masses, scales, daisies, radius, crown_tris, seed = FLOWER_BUSHES[kind]
    crown = _crown_t39(masses, f"GL_FlowerBush_{kind}", work)
    decimate_to(crown, crown_tris)
    bm = bmesh.new()
    bm.from_mesh(crown.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    # Rooted like the meadow bushes: the lowest leaves end 4 cm under the ground; pivot under the crown's middle.
    zs = [v.co.z for v in bm.verts]
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    shift = Vector((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, -min(zs) - 0.04))
    for v in bm.verts:
        v.co += shift
    crown_end = len(bm.verts)
    crown_bvh = BVHTree.FromBMesh(bm)
    zs = [v.co.z for v in bm.verts]
    center = Vector((0.0, 0.0, (min(zs) + max(zs)) * 0.5))
    radii = Vector((max(abs(v.co.x) for v in bm.verts), max(abs(v.co.y) for v in bm.verts), (max(zs) - min(zs)) * 0.5))

    # Daisies first, so the top of the crown (what the refuge camera sees most) wears some: directions spread over
    # the upper crown (golden angle), each laid on the surface facing out and up, never dipping into the crown.
    rng = random.Random(seed)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    placed = []
    flower_ranges = []
    flowers_bm = bmesh.new()
    # Candidates by rings from the top down (one on top, three on the shoulders, then the sides), then a golden-angle
    # spiral as a fallback, so every bush wears daisies where the camera looks.
    candidates = []
    for z, n in ((0.99, 1), (0.8, 3), (0.5, 4), (0.25, 5)):
        phase = rng.uniform(0.0, math.tau)
        candidates += [(z, phase + k / n * math.tau) for k in range(n)]
    candidates += [(0.95 - 0.8 * ((k * 0.618034) % 1.0), k * golden) for k in range(1, 300)]
    for z, a in candidates:
        if len(placed) >= daisies:
            break
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a += rng.uniform(-0.2, 0.2)
        direction = Vector((math.cos(a) * r, math.sin(a) * r, z))
        origin = center + Vector((direction.x * radii.x, direction.y * radii.y, direction.z * radii.z)) * 2.2
        hit, normal, _, _ = crown_bvh.ray_cast(origin, -direction)
        if hit is None or any((hit - p).length < radius * 2.6 for p in placed):
            continue
        facing = (normal * 0.62 + UP * 0.38).normalized()
        turn = rng.uniform(0.0, math.tau)
        lift = 0.004
        while lift <= 0.016:
            c = hit + facing * lift
            outline, _, _ = _daisy_outline(c, facing, radius, turn, 0.16)
            if not any(_inside(crown_bvh, p, 0.003) for p in outline):
                break
            lift += 0.002
        else:
            continue
        start = len(bm.verts)
        _add_daisy(bm, c, facing, radius, turn)
        _add_daisy(flowers_bm, c, facing, radius, turn)
        flower_ranges.append((start, len(bm.verts), c.copy(), facing.copy()))
        placed.append(hit)
    if len(placed) < daisies:
        raise RuntimeError(f"GL_FlowerBush_{kind}: only {len(placed)} of {daisies} daisies found room")
    flowers_bvh = BVHTree.FromBMesh(flowers_bm)
    flowers_bm.free()

    # Leaf scales like _scales_on (pw_grassland_t33), skipping any scale that would touch a daisy.
    scales_start = len(bm.verts)
    rng = random.Random(600 + seed)
    count = 0
    for i in range(scales * 4):
        if count >= scales:
            break
        z = 1.0 - (i + 0.5) / (scales * 4)
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a = i * golden + rng.uniform(-0.2, 0.2)
        direction = Vector((math.cos(a) * r, math.sin(a) * r, z))
        if direction.z < 0.12:
            continue
        origin = center + Vector((direction.x * radii.x, direction.y * radii.y, direction.z * radii.z)) * 2.2
        hit, normal, _, _ = crown_bvh.ray_cast(origin, -direction)
        size, lift = rng.uniform(0.1, 0.15), rng.uniform(0.08, 0.16)
        if hit is None:
            continue
        probe = bmesh.new()
        _leaf_scale(probe, hit - normal * 0.012, normal, Vector((0, 0, -1)) + direction * 0.6, size, lift)
        touches = BVHTree.FromBMesh(probe).overlap(flowers_bvh)
        near = any(flowers_bvh.find_nearest(v.co)[3] < 0.01 for v in probe.verts)
        probe.free()
        if touches or near:
            continue
        _leaf_scale(bm, hit - normal * 0.012, normal, Vector((0, 0, -1)) + direction * 0.6, size, lift)
        count += 1
    foliage_end = len(bm.verts)
    bm.normal_update()
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)

    _t39_replace(f"GL_FlowerBush_{kind}")
    obj = object_from_bmesh(f"GL_FlowerBush_{kind}", bm, [col])
    height = max(v.co.z for v in obj.data.vertices)
    sun_dir = Vector((0.45, -0.55, 0.7)).normalized()

    def cdir(co):
        d = co - center
        return Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).normalized()

    def flower_of(i):
        for start, end, c, facing in flower_ranges:
            if start <= i < end:
                return i - start, c, facing
        return None

    def regions(co, n, i):
        f = flower_of(i)
        if f is not None:
            k, c, facing = f
            return (0.0, 0.0, 0.0, 1.0) if k < 4 * PETALS + 2 else (0.0, 0.0, 0.0, 0.0)
        cd = cdir(co)
        sun = smoothstep(0.05, 0.85, cd.dot(sun_dir)) * smoothstep(-0.25, 0.45, cd.z)
        shade = smoothstep(-0.05, -0.75, cd.z)
        flicker = 0.18 * (0.5 + 0.5 * wave_noise(co * 3.1, 77, octaves=1))
        if i >= crown_end:
            return (1.0, min(1.0, 0.35 + sun * 0.75 + flicker), shade * 0.5, 0.0)
        return (1.0, min(1.0, sun * 0.85 + flicker * 0.5), shade, 0.0)

    def occlusion(co, n, i):
        f = flower_of(i)
        h = min(1.0, max(0.0, co.z) / height)
        if f is not None:
            k, c, facing = f
            return (0.22 if k == 4 * PETALS + 1 else 0.03, h)
        depth = 1.0 - smoothstep(0.7, 1.0, (co - center).length / max(radii))
        return (0.25 * smoothstep(0.0, -0.8, cdir(co).z) + 0.3 * depth, h)

    write_layers(obj, regions, occlusion)
    me = obj.data
    normals = []
    for v in me.vertices:
        f = flower_of(v.index)
        if f is None:
            normals.append(v.normal.lerp(cdir(v.co), 0.8).normalized())
            continue
        k, c, facing = f
        radial = v.co - c
        radial -= facing * radial.dot(facing)
        normals.append((facing + radial * 4.0 + UP * 0.25).normalized())
    me.normals_split_custom_set_from_vertices(normals)
    return obj


# ---------------------------------------------------------------- build, save, export

def build_t39_props():
    """Builds the five props in the scene "HideSeek" (export collection) and returns triangles, sizes and pivots.
    Never touches another scene's objects; the work collection is removed afterwards."""
    scene, col, work = _t39_scene()
    out = [build_tall_grass(k, col) for k in ("A", "B", "C")]
    out += [build_flower_bush(k, col, work) for k in ("A", "B")]
    for o in list(work.objects):
        data = o.data
        bpy.data.objects.remove(o)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)
    bpy.data.collections.remove(work)
    report = {}
    for obj in out:
        tris = triangle_count(obj)
        budget = next(v for k, v in T39_BUDGETS.items() if obj.name.startswith(k))
        if tris > budget:
            raise RuntimeError(f"{obj.name}: {tris} triangles over its budget of {budget}")
        pts = [Vector(c) for c in obj.bound_box]
        lo = [min(p[a] for p in pts) for a in range(3)]
        hi = [max(p[a] for p in pts) for a in range(3)]
        report[obj.name] = (tris, tuple(round(hi[a] - lo[a], 3) for a in range(3)), tuple(round(x, 3) for x in lo))
    return report


def _t39_root():
    marker = os.sep + "SourceArt" + os.sep
    path = bpy.data.filepath
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ first (or pass the project root).")
    return path.split(marker)[0]


def _t39_clear_previews():
    scene = bpy.data.scenes.get(T39_SCENE)
    for obj in (scene.objects if scene else []):
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]


def write_t39_blend(root=None):
    """Writes the HideSeek scene and everything it uses to BL_Biome_Grassland_T39.blend (compressed). The open file
    stays as it is."""
    root = root or _t39_root()
    _t39_clear_previews()
    path = os.path.join(root, *T39_BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {bpy.data.scenes[T39_SCENE]}, fake_user=True, compress=True)
    return path


def export_t39():
    """GL_Props_T39.fbx with the project preset (pw_export.FBX_PRESET; load pw_export.py first). Run inside
    BL_Biome_Grassland_T39.blend so the FBX header names it. Nothing is saved."""
    root = project_root()
    col = bpy.data.collections.get("EXPORT") or bpy.data.collections[T39_COLLECTION]
    chosen = [o for o in col.all_objects if o.type == 'MESH' and o.name.startswith(("GL_TallGrass_", "GL_FlowerBush_"))]
    if not chosen:
        raise RuntimeError("Nothing to export")
    _t39_clear_previews()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in chosen:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = chosen[0]
    out = os.path.join(root, *T39_FBX.split("/"))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, bake_anim=False, **FBX_PRESET)
    return out, sorted(o.name for o in chosen)


# ---------------------------------------------------------------- review images

def _t39_linear(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def t39_paint_preview(obj, row):
    """WORK_Preview (point color) = what SH_EnvironmentOpaque computes from the regions, before lighting."""
    pal = [_t39_linear(h) for h in row]
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
        for weight, colr in ((r, pal[1]), (g, pal[2]), (b, pal[3]), (a, pal[4])):
            c = c.lerp(colr, weight)
        c = c.lerp(pal[5], occ[v.index])
        prev.data[v.index].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev


# Reference rows for the review images (DEF_Palettes_Grassland) and the animals' first variant colors (primary,
# secondary, belly, detail, eye: AnimalPipeline).
T39_REF_PALETTES = {
    "grass": ("66A253", "9FCE78", "D0CB9A", "66A253", "66A253", "476F3E", "B9D56C"),
    "bush": ("6A4E3C", "6FAE57", "9ED07A", "4C8644", "6FAE57", "355E33", "FFFFFF"),
    "ground": ("78A862", "8FB878", "5F9152", "B8C98E", "8F6A50", "4A6542", "9CBC74"),
}
T39_ANIMALS = {
    "Rabbit": ("SKM_Rabbit_LOD0", ("B07A52", "F2B8B5", "F3E3CC", "E98A93", "1E1414")),
    "Squirrel": ("SKM_Squirrel_LOD0", ("C9693A", "B4552E", "F4E3CB", "5E3329", "1A1210")),
    "Hedgehog": ("SKM_Hedgehog_LOD0", ("B48A66", "6E4B36", "EEDCC4", "2A1E1C", "140E0D")),
}


def _t39_paint_animal(obj, colors):
    cols = [_t39_linear(h) for h in colors]
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for i, data in enumerate(regions.data):
        c = cols[0].copy()
        for weight, colr in zip(data.color, cols[1:]):
            c = c.lerp(colr, weight)
        prev.data[i].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev


def _t39_world_bvh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def render_t39_previews(out_dir, refs, size=1.0):
    """Review images in out_dir (T39_props_*.png), rendered with EEVEE, one sun from the top left of the image and the
    palette colors computed like SH_EnvironmentOpaque. refs: {"core": BL_Biome_Grassland_Core.blend,
    "magnifique": BL_Biome_Grassland_Magnifique.blend, "Rabbit"/"Squirrel"/"Hedgehog": BL_Animal_<Species>.blend},
    read only. Everything made here is removed afterwards; nothing is saved."""
    scene = bpy.data.scenes[T39_SCENE]
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    shots = bpy.data.collections.new("WORK_T39_Review")
    scene.collection.children.link(shots)
    temp = []
    mat = bpy.data.materials.new("WORK_T39_Material")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    attr = mat.node_tree.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "WORK_Preview"
    mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.15
    world = bpy.data.worlds.new("WORK_T39_World")
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    coords, sep = nt.nodes.new("ShaderNodeTexCoord"), nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp, bg, out = nt.nodes.new("ShaderNodeValToRGB"), nt.nodes.new("ShaderNodeBackground"), nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(coords.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    nt.links.new(bg.outputs[0], out.inputs[0])
    ramp.color_ramp.elements[0].color = tuple(_t39_linear("CFE3E4")) + (1.0,)
    ramp.color_ramp.elements[1].position = 0.45
    ramp.color_ramp.elements[1].color = tuple(_t39_linear("7FB2E6")) + (1.0,)
    scene.world = world
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "CYCLES"
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = False
    for flag in [f for f in dir(scene.render) if f.startswith("use_stamp")]:
        setattr(scene.render, flag, False)
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 48

    def keep(obj):
        shots.objects.link(obj)
        temp.append(obj)
        return obj

    def dress(obj, row):
        t39_paint_preview(obj, row)
        obj.data.materials.clear()
        obj.data.materials.append(mat)

    props = {o.name: o for o in scene.objects if o.type == 'MESH' and o.name.startswith(("GL_TallGrass_", "GL_FlowerBush_"))}
    rows = {"GL_TallGrass_A": "palette.grassland.tall_grass", "GL_TallGrass_B": "palette.grassland.tall_grass",
            "GL_TallGrass_C": "palette.grassland.tall_grass", "GL_FlowerBush_A": "palette.grassland.flower_bush_pink",
            "GL_FlowerBush_B": "palette.grassland.flower_bush_cream"}
    for name, obj in props.items():
        dress(obj, T39_PALETTES[rows[name]])

    def copy(obj, row, location, yaw=0.0):
        dup = keep(bpy.data.objects.new("WORK_" + obj.name, obj.data.copy()))
        dup.location = location
        dup.rotation_euler = (0.0, 0.0, yaw)
        dress(dup, row)
        return dup

    # References from the Prairie files and the animals (appended into this unsaved session, removed at the end).
    ref = {}
    for key, names in (("magnifique", ("GL_Bush_A", "GL_GrassWisp_L")), ("core", ("GL_GrassTuft_L",))):
        with bpy.data.libraries.load(refs[key], link=False) as (src, dst):
            dst.meshes = [n for n in src.meshes if n in names]
        for me in dst.meshes:
            ref[me.name] = keep(bpy.data.objects.new("WORK_REF_" + me.name, me))
            dress(ref[me.name], T39_REF_PALETTES["bush" if "Bush" in me.name else "grass"])
    animals = {}
    for species, (lod0, colors) in T39_ANIMALS.items():
        with bpy.data.libraries.load(refs[species], link=False) as (src, dst):
            dst.meshes = [n for n in src.meshes if n == lod0]
        animal = keep(bpy.data.objects.new("WORK_REF_" + species, dst.meshes[0]))
        _t39_paint_animal(animal, colors)
        animal.data.materials.clear()
        animal.data.materials.append(mat)
        animals[species] = animal
    ground_bm = bmesh.new()
    bmesh.ops.create_circle(ground_bm, cap_ends=True, radius=12.0, segments=64)
    ground_me = bpy.data.meshes.new("WORK_T39_Ground")
    ground_bm.to_mesh(ground_me)
    ground_bm.free()
    ground = keep(bpy.data.objects.new("WORK_T39_Ground", ground_me))
    regions = ground_me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    for v in ground_me.vertices:
        regions.data[v.index].color = (0.25, 0.0, 0.0, 0.0)
    ground_me.uv_layers.new(name="PW_Occlusion")
    dress(ground, T39_REF_PALETTES["ground"])

    cam_data = bpy.data.cameras.new("WORK_T39_Camera")
    cam = keep(bpy.data.objects.new("WORK_T39_Camera", cam_data))
    scene.camera = cam
    sun_data = bpy.data.lights.new("WORK_T39_Sun", 'SUN')
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.95, 0.86)
    sun_data.angle = math.radians(9.0)
    sun = keep(bpy.data.objects.new("WORK_T39_Sun", sun_data))

    def aim(target, pitch_deg, distance, fov_deg, width, height, yaw_deg=0.0):
        """The refuge camera: fixed pitch and vertical field of view, looking along Blender +Y (Unity -Z)."""
        p, y = math.radians(pitch_deg), math.radians(yaw_deg)
        direction = Vector((math.sin(y) * math.cos(p), math.cos(y) * math.cos(p), -math.sin(p)))
        cam.location = target - direction * distance
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        cam_data.sensor_fit = 'VERTICAL'
        cam_data.angle_y = math.radians(fov_deg)
        cam_data.clip_start = 0.05
        cam_data.clip_end = 80.0
        scene.render.resolution_x = int(width * size)
        scene.render.resolution_y = int(height * size)
        right = direction.cross(UP).normalized()
        toward_sun = (-right * 0.75 - direction * 0.45 + UP * 1.35).normalized()
        sun.rotation_euler = (-toward_sun).to_track_quat('-Z', 'Y').to_euler()

    def show(objs):
        names = {o.name for o in objs} | {ground.name}
        for o in scene.objects:
            if o.type == 'MESH':
                o.hide_render = o.name not in names

    def shot(file_name):
        scene.render.filepath = os.path.join(out_dir, file_name)
        bpy.ops.render.render(write_still=True, scene=scene.name)
        return scene.render.filepath

    def place(obj, x, y, yaw=0.0):
        obj.location = (x, y, 0.0)
        obj.rotation_euler = (0.0, 0.0, yaw)

    files = []
    # 1. The family: the five props among the Prairie's own bush, tufts and the rabbit.
    line = [("GL_TallGrass_A", -1.95, 0.0), ("GL_TallGrass_B", -1.2, 0.05), ("GL_TallGrass_C", -0.4, 0.0),
            ("GL_FlowerBush_A", 0.6, 0.05), ("GL_FlowerBush_B", 1.7, 0.0)]
    for name, x, y in line:
        place(props[name], x, y)
    place(ref["GL_Bush_A"], 0.95, 1.35, 0.4)
    place(ref["GL_GrassTuft_L"], -0.55, 1.0)
    place(ref["GL_GrassWisp_L"], -1.4, 1.1)
    place(animals["Rabbit"], -0.15, -0.85, 0.5)
    show(list(props.values()) + list(ref.values()) + [animals["Rabbit"]])
    aim(Vector((0.0, 0.25, 0.25)), 40.0, 6.2, 30.0, 1400, 800)
    files.append(shot("T39_props_family.png"))

    # 2. Close-ups: the three clumps, the bushes in their three palettes.
    for name, x in (("GL_TallGrass_A", -0.72), ("GL_TallGrass_B", 0.0), ("GL_TallGrass_C", 0.75)):
        place(props[name], x, 0.0)
    show([props["GL_TallGrass_A"], props["GL_TallGrass_B"], props["GL_TallGrass_C"]])
    aim(Vector((0.0, 0.0, 0.25)), 32.0, 2.9, 30.0, 1200, 700)
    files.append(shot("T39_props_tallgrass.png"))
    yellow = copy(props["GL_FlowerBush_A"], T39_PALETTES["palette.grassland.flower_bush_yellow"], (1.0, 0.25, 0.0), 2.1)
    place(props["GL_FlowerBush_A"], -1.0, 0.0)
    place(props["GL_FlowerBush_B"], 0.0, 0.1)
    show([props["GL_FlowerBush_A"], props["GL_FlowerBush_B"], yellow])
    aim(Vector((0.0, 0.1, 0.28)), 34.0, 3.6, 30.0, 1200, 700)
    files.append(shot("T39_props_flowerbush.png"))

    # 3. Hiding, seen by the refuge camera (pitch 40 degrees, 30 degree field, portrait): each animal stands behind its
    # decor (Blender +Y, away from the camera), moved back until nothing of it enters the decor (measured on the
    # meshes), part of its silhouette left in view.
    hide = [("GL_TallGrass_A", "Rabbit", -0.55, 0.9, 0.35), ("GL_FlowerBush_A", "Squirrel", 0.55, 0.15, -0.5),
            ("GL_TallGrass_C", "Hedgehog", -0.3, -0.75, 0.2)]
    gaps = {}
    scene_objs = [ref["GL_Bush_A"], ref["GL_GrassTuft_L"]]
    place(ref["GL_Bush_A"], 0.75, 1.55, 1.1)
    place(ref["GL_GrassTuft_L"], -0.95, -0.2)
    for name, species, x, y, yaw in hide:
        place(props[name], x, y, yaw)
        animal = animals[species]
        bpy.context.view_layer.update()
        decor = _t39_world_bvh(props[name])
        back = 0.0
        while True:
            place(animal, x + 0.04, y + 0.18 + back, 0.3)
            bpy.context.view_layer.update()
            body = _t39_world_bvh(animal)
            if not body.overlap(decor):
                verts = [animal.matrix_world @ v.co for v in list(animal.data.vertices)[::7]]
                gap = min(decor.find_nearest(p)[3] for p in verts)
                if gap >= 0.01:
                    break
            back += 0.01
        gaps[species] = round(gap, 3)
        scene_objs += [props[name], animal]
    show(scene_objs)
    aim(Vector((0.0, 0.35, 0.2)), 40.0, 7.0, 30.0, 540, 1170)
    files.append(shot("T39_props_hide.png"))
    aim(Vector((0.0, 0.35, 0.2)), 40.0, 9.5, 30.0, 540, 1170)
    files.append(shot("T39_props_hide_game.png"))

    # 4. Silhouettes at 64 px (charte, grid point 1), light and dark grounds.
    cells = list(props.values())
    for k, o in enumerate(cells):
        extent = max(o.dimensions)
        o.scale = (0.08 / extent,) * 3
        o.rotation_euler = (0.0, 0.0, 0.0)
        o.location = (k * 0.1, 0.0, 0.0)
    show(cells)
    ground.hide_render = True
    previous = scene.render.engine
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = 'FLAT'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.0, 0.0, 0.0)
    cam_data.type = 'ORTHO'
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.ortho_scale = 0.1 * len(cells)
    cam.location = (0.1 * (len(cells) - 1) * 0.5, -0.6, 0.6 + 0.03)
    cam.rotation_euler = (math.radians(45.0), 0.0, 0.0)
    scene.render.resolution_x = 64 * len(cells)
    scene.render.resolution_y = 64
    for name_, color in (("light", "EDEDE6"), ("dark", "2E3440")):
        world.use_nodes = False
        world.color = tuple(_t39_linear(color))
        files.append(shot(f"T39_props_silhouettes_{name_}.png"))
    scene.render.engine = previous
    for o in cells:
        o.scale = (1.0, 1.0, 1.0)
        o.location = (0.0, 0.0, 0.0)

    # Clean up.
    for o in temp:
        data = o.data
        bpy.data.objects.remove(o)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
            elif isinstance(data, bpy.types.Light):
                bpy.data.lights.remove(data)
    bpy.data.collections.remove(shots)
    for o in props.values():
        o.hide_render = False
        o.location = (0.0, 0.0, 0.0)
        o.rotation_euler = (0.0, 0.0, 0.0)
        o.data.materials.clear()
    _t39_clear_previews()
    bpy.data.materials.remove(mat)
    scene.world = None
    bpy.data.worlds.remove(world)
    return files, gaps
