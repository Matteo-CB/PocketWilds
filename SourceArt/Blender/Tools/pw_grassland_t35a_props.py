"""Grassland T35A: what closes the next zone of the Prairie (doc 02 Améliorations, section 4.6).

Load after pw_grassland.py, pw_grassland_t33.py, pw_grassland_t33b.py and pw_grassland_t33b_props.py, in the same
namespace:
    ns["build_zone_props_t35a"]()   # GL_ZoneHedge (2 m hedge segment), GL_ZoneSign (wooden sign on a post)

The hedge is built like the meadow's own bushes (one lobed crown from a smooth union of round masses, leaf scales
on the upper crown, the same foliage painting), long along X so the game lays segments end to end. The sign is a
post and two planks of the fence's wood; it reads the same from both sides (the price is drawn by the UI above it).
"""
import math

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
