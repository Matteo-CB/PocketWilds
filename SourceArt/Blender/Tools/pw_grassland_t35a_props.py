"""Grassland T35A: what closes the next zone of the Prairie (doc 02 Améliorations, section 4.6).

Load after pw_grassland.py, pw_grassland_t33.py, pw_grassland_t33b.py and pw_grassland_t33b_props.py, in the same
namespace:
    ns["build_zone_props_t35a"]()   # GL_ZoneHedge (2 m hedge segment), GL_ZoneSign (wooden sign on a post)
    ns["build_biome_gate_forest"]()  # GL_BiomeGate_Forest (the Forest's gate at the start of the passage)

The hedge is built like the meadow's own bushes (one lobed crown from a smooth union of round masses, leaf scales
on the upper crown, the same foliage painting), long along X so the game lays segments end to end. The sign is a
post and two planks of the fence's wood; it reads the same from both sides (the price is drawn by the UI above it).
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

# Lobes along the hedge: (x, y, z) center, radius. A row of round bushes grown into one another, with smaller
# rounded tops between them; ends rounded so two segments meet without a seam.
HEDGE_MASSES = [
    (Vector((-0.86, 0.03, 0.22)), 0.24), (Vector((-0.55, -0.09, 0.25)), 0.28), (Vector((-0.2, 0.08, 0.27)), 0.27),
    (Vector((0.14, -0.07, 0.26)), 0.29), (Vector((0.49, 0.08, 0.25)), 0.27), (Vector((0.84, -0.03, 0.22)), 0.24),
    (Vector((-0.7, 0.0, 0.4)), 0.17), (Vector((-0.37, -0.02, 0.45)), 0.2), (Vector((-0.02, 0.03, 0.47)), 0.19),
    (Vector((0.31, -0.02, 0.45)), 0.2), (Vector((0.66, 0.02, 0.4)), 0.17),
]
HEDGE_TRIS = 470
HEDGE_SCALES = 26


def build_zone_hedge(collections):
    """GL_ZoneHedge: a low hedge segment, 2 m long, about 0.6 m tall and 0.7 m deep."""
    from mathutils.bvhtree import BVHTree
    crown = _crown(HEDGE_MASSES, "GL_ZoneHedge", resolution=0.045, voxel=0.03, stiffness=4.0)
    decimate_to(crown, HEDGE_TRIS)
    bm = bmesh.new()
    bm.from_mesh(crown.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    # Rooted in the ground like the bushes.
    for v in bm.verts:
        v.co.z -= 0.04
    crown_end = len(bm.verts)
    tree = BVHTree.FromBMesh(bm)
    zs = [v.co.z for v in bm.verts]
    center = Vector((0, 0, (min(zs) + max(zs)) * 0.5))
    radii = Vector((max(abs(v.co.x) for v in bm.verts), max(abs(v.co.y) for v in bm.verts), (max(zs) - min(zs)) * 0.5))
    _scales_on(bm, tree, center, radii, HEDGE_SCALES, (0.1, 0.15), 3501)
    foliage_end = len(bm.verts)
    bm.normal_update()
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    obj = object_from_bmesh("GL_ZoneHedge", bm, collections)
    _paint_foliage(obj, center, radii, crown_end, foliage_end, bark_grooves=False, height=max(zs))
    return obj


def build_zone_sign(collections):
    """GL_ZoneSign: a little notice board, one wide plank-built board on two posts, the meadow fence's wood."""
    bm = bmesh.new()

    def block(center, size, top_round=0.0, tilt=0.0):
        """A box; top_round pulls the top corners down (rounded ends), tilt turns it a hair around Y."""
        temp = bmesh.new()
        bmesh.ops.create_cube(temp, size=1.0)
        angle = math.radians(tilt)
        mapping = {}
        for v in temp.verts:
            x, y, z = v.co.x * size.x, v.co.y * size.y, v.co.z * size.z
            if top_round > 0.0 and z > 0.0 and abs(x) > size.x * 0.45:
                z -= top_round
            co = Vector((x * math.cos(angle) - z * math.sin(angle), y, x * math.sin(angle) + z * math.cos(angle)))
            mapping[v] = bm.verts.new(center + co)
        for f in temp.faces:
            bm.faces.new([mapping[v] for v in f.verts])
        temp.free()

    # Two posts, their tops cut into a low point, just above the board.
    for x in (-0.36, 0.36):
        block(Vector((x, 0.035, 0.47)), Vector((0.075, 0.075, 0.94)))
        apex = bm.verts.new(Vector((x, 0.035, 0.995)))
        corners = [bm.verts.new(Vector((x + dx, 0.035 + dy, 0.94))) for dx, dy in ((-0.0375, -0.0375), (0.0375, -0.0375), (0.0375, 0.0375), (-0.0375, 0.0375))]
        for k in range(4):
            bm.faces.new((corners[k], corners[(k + 1) % 4], apex))
    # The board: three planks side by side, each a hair different, the top one with rounded ends.
    block(Vector((0.0, -0.025, 0.84)), Vector((0.86, 0.045, 0.13)), top_round=0.035, tilt=0.8)
    block(Vector((0.0, -0.025, 0.705)), Vector((0.84, 0.045, 0.125)), tilt=-0.5)
    block(Vector((0.0, -0.025, 0.575)), Vector((0.85, 0.045, 0.12)), tilt=0.4)
    bm.normal_update()

    def regions(co, n, i):
        # R: the board's faces a shade lighter (sun-worn); G: a little moss on what faces up; A: grain on the posts.
        face = 1.0 if abs(n.y) > 0.8 and 0.5 < co.z < 0.92 and abs(co.x) < 0.42 else 0.0
        moss = smoothstep(0.6, 0.95, n.z) * 0.35
        grain = smoothstep(0.55, 0.95, math.sin(co.z * 22.0 + co.x * 9.0)) * 0.4 * (1.0 - face)
        return (face * 0.55, moss, 0.0, grain)

    def occlusion(co, n, i):
        return (0.4 * (1.0 - smoothstep(0.0, 0.14, co.z)), 0.0)

    obj = object_from_bmesh("GL_ZoneSign", bm, collections, smooth=False)
    write_layers(obj, regions, occlusion)
    return obj


def build_zone_props_t35a():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    for name in ("GL_ZoneHedge", "GL_ZoneSign"):
        old = bpy.data.objects.get(name)
        if old is not None:
            data = old.data
            bpy.data.objects.remove(old)
            if data is not None and data.users == 0:
                bpy.data.meshes.remove(data)
    out = [build_zone_hedge(target), build_zone_sign(target)]
    return {o.name: triangle_count(o) for o in out}


# The Forest's gate (doc 02 Améliorations, sections 5.2 and 12): two old trunks on either side of the path, their tops
# bending over it and crossing into one leafy arch, and branches woven between them that close the way until the Forest
# opens. Span along X (the game lays it along the seam), front toward -Y, under 1,500 triangles, the trees' wood and
# foliage painting (one mesh, one material).
GATE_HALF_SPAN = 1.25
# The trunks are drawn for a 1.6 m half span and a 3 m arch, then scaled to the gate's size (x, z).
GATE_SCALE = Vector((GATE_HALF_SPAN / 1.6, 1.0, 0.86))
GATE_TRUNKS = (
    # (points of the trunk from its foot to its tip, radius at the foot)
    ([(-1.6, 0.0, 0.0), (-1.72, 0.03, 0.55), (-1.74, 0.0, 1.15), (-1.64, -0.03, 1.7), (-1.38, 0.0, 2.2), (-0.98, 0.04, 2.6),
      (-0.5, 0.02, 2.88), (0.0, -0.02, 3.02), (0.42, -0.05, 3.06)], 0.22),
    ([(1.6, 0.0, 0.0), (1.7, -0.04, 0.6), (1.71, 0.0, 1.2), (1.6, 0.04, 1.72), (1.35, 0.02, 2.18), (0.95, -0.03, 2.56),
      (0.48, -0.06, 2.84), (0.0, 0.03, 2.98), (-0.4, 0.06, 3.0)], 0.21),
)


def _gate_masses():
    """A leafy garland along the arch: round lobes from one shoulder over the top to the other, big and small in turn
    and a little in front or behind, so the outline stays bumpy like the meadow's bushes."""
    masses = []
    sx, sz = GATE_SCALE.x, GATE_SCALE.z
    for i in range(9):
        u = i / 8.0
        angle = math.pi * (0.93 - 0.86 * u)
        x = math.cos(angle) * 1.66 * sx
        z = (1.5 + math.sin(angle) * 1.55) * sz
        size = (0.3 if i % 2 == 0 else 0.24) + 0.1 * math.sin(u * math.pi)
        masses.append((Vector((x, 0.12 if i % 3 == 0 else -0.09, z)), size))
    for x, y, z, r in ((-0.5, 0.05, 3.25, 0.26), (0.42, -0.06, 3.28, 0.28), (-0.04, 0.1, 3.4, 0.2)):
        masses.append((Vector((x * sx, y, z * sz)), r))
    return masses


GATE_MASSES = _gate_masses()
GATE_CROWN_TRIS = 560
GATE_SCALES = 40
GATE_WEAVE = (
    # (from, middle, to) for a 1.6 m half span: branches woven across the opening, from trunk to trunk.
    ((-1.58, 0.02, 0.28), (0.0, 0.12, 0.66), (1.56, -0.02, 1.2)),
    ((-1.58, -0.02, 1.2), (0.0, -0.1, 0.7), (1.55, 0.02, 0.3)),
    ((-1.6, 0.03, 0.72), (0.0, -0.04, 1.02), (1.6, 0.0, 0.66)),
)
GATE_WEAVE_LEAVES = 30


def _bezier3(a, b, c, steps):
    return [a * (1 - t) ** 2 + b * 2 * (1 - t) * t + c * t * t for t in (i / steps for i in range(steps + 1))]


def build_biome_gate_forest(collections=None):
    """GL_BiomeGate_Forest: about 2.9 m wide, 3.1 m tall; the closed state (woven branches) is part of the mesh."""
    from mathutils.bvhtree import BVHTree
    if collections is None:
        cols = ensure_collections()
        collections = [cols["GEO"], cols["EXPORT"]]
    old = bpy.data.objects.get("GL_BiomeGate_Forest")
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    crown = _crown(GATE_MASSES, "GL_BiomeGate_Forest", resolution=0.05, voxel=0.04, stiffness=4.2)
    decimate_to(crown, GATE_CROWN_TRIS)
    bm = bmesh.new()
    bm.from_mesh(crown.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    crown_end = len(bm.verts)
    tree = BVHTree.FromBMesh(bm)
    center = Vector((0.0, 0.0, 2.85 * GATE_SCALE.z))
    radii = Vector((1.75 * GATE_SCALE.x, 0.5, 0.55))
    _scales_on(bm, tree, center, radii, GATE_SCALES, (0.16, 0.24), 3517)
    foliage_end = len(bm.verts)

    # The two trunks: thick at the foot with roots, thinner as they bend over the path and cross at the top.
    for points, r0 in GATE_TRUNKS:
        pts = [Vector((p[0] * GATE_SCALE.x, p[1], p[2] * GATE_SCALE.z)) for p in points]
        n = len(pts) - 1
        radii_t = [r0 * (1.0 - 0.62 * (i / n) ** 0.9) for i in range(n + 1)]
        _tube(bm, pts, radii_t, 7, cap_top=True, flare=lambda k, a: 1.0 + 0.6 * max(0.0, math.cos(a * 2.0 + 0.6)) ** 3)
        # A thinner branch winds around each trunk, then leaves it at the top.
        helix = []
        for i in range(13):
            t = i / 12.0
            f = t * n
            j = min(int(f), n - 1)
            base = pts[j].lerp(pts[j + 1], f - j)
            axis = (pts[j + 1] - pts[j]).normalized()
            side = axis.cross(Vector((0, 1, 0))).normalized()
            up = axis.cross(side).normalized()
            angle = t * math.tau * 1.6
            helix.append(base + (side * math.cos(angle) + up * math.sin(angle)) * (r0 * (1.0 - 0.6 * t) + 0.035))
        _tube(bm, helix, [0.055 * (1.0 - 0.55 * i / 12.0) for i in range(13)], 4, cap_top=True)

    # The closed way: four branches woven from trunk to trunk, a few leaves along them.
    rng = random.Random(3519)
    leaf_points = []
    for a, b, c in GATE_WEAVE:
        a, b, c = (Vector((q[0] * GATE_SCALE.x, q[1], q[2] * GATE_SCALE.z)) for q in (a, b, c))
        pts = _bezier3(a, b, c, 6)
        _tube(bm, pts, [0.06, 0.052, 0.046, 0.044, 0.046, 0.052, 0.06], 5, cap_top=True)
        leaf_points.extend(pts[1:-1])
    weave_leaves = len(bm.verts)
    for k in range(GATE_WEAVE_LEAVES):
        p = leaf_points[(k * 7) % len(leaf_points)]
        side = -1.0 if k % 2 == 0 else 1.0
        normal = Vector((rng.uniform(-0.3, 0.3), side, rng.uniform(0.2, 0.7))).normalized()
        _leaf_scale(bm, p + normal * 0.04, normal, Vector((rng.uniform(-1, 1), 0, -0.6)), rng.uniform(0.12, 0.17),
                    rng.uniform(0.1, 0.2))
    weave_leaves_end = len(bm.verts)
    bm.normal_update()

    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    obj = object_from_bmesh("GL_BiomeGate_Forest", bm, collections)
    _paint_foliage(obj, center, radii, crown_end, foliage_end, bark_grooves=True, height=3.1)
    # The weave's leaves are foliage too (sunny, a little lighter than the crown's scales).
    attr = obj.data.color_attributes["PW_Regions"]
    for v in obj.data.vertices:
        if weave_leaves <= v.index < weave_leaves_end:
            attr.data[v.index].color = (1.0, 0.75, 0.1, 0.0)
    return {obj.name: triangle_count(obj)}

