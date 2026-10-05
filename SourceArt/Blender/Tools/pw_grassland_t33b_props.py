"""Grassland Magnifique part 2 (T33B): landmarks and the last small props.

Load after pw_grassland.py, pw_grassland_t33.py and pw_grassland_t33b.py, in the same namespace:
    ns["build_landmarks_t33b"]()   # GL_Landmark_OldTree_LOD0-2, GL_Landmark_Windmill_LOD0-2, GL_Landmark_Windmill_Sails_LOD0-2
    ns["build_props_t33b"]()       # GL_Fence_B, GL_WellTiny, GL_Edge_A, GL_Edge_B, GL_StreamStones

The windmill sails are their own object, pivot at the hub, turning around Blender Y (Unity Z): the renderer's rotor
flag makes SH_EnvironmentOpaque spin them, no script.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

# ---------------- landmarks ----------------

OLD_TREE = dict(
    masses=[(Vector((0.0, 0.0, 3.55)), 1.45), (Vector((1.35, 0.25, 3.25)), 0.95), (Vector((-1.3, -0.2, 3.3)), 1.0),
            (Vector((0.2, 1.2, 3.2)), 0.9), (Vector((-0.2, -1.25, 3.35)), 0.92), (Vector((0.75, -0.6, 4.25)), 0.85),
            (Vector((-0.7, 0.55, 4.3)), 0.82), (Vector((0.1, 0.1, 4.6)), 0.66),
            (Vector((1.95, 0.6, 2.95)), 0.74), (Vector((-1.85, 0.9, 3.0)), 0.72), (Vector((0.9, -1.75, 3.0)), 0.7),
            (Vector((-1.05, -1.55, 3.05)), 0.7), (Vector((0.5, 1.85, 2.9)), 0.66)],
    center=Vector((0.0, 0.0, 3.6)), radii=Vector((2.3, 2.1, 1.35)),
)
# lod: (crown triangles, leaf scales, trunk sides, trunk rings, roots, branches, swing)
OLD_TREE_LODS = {0: (900, 96, 10, 7, 5, True, True), 1: (420, 30, 7, 4, 3, True, True), 2: (200, 0, 6, 3, 0, False, False)}


def build_old_tree(collections, lod_collections):
    """GL_Landmark_OldTree: a wide, gnarled old tree with a rope swing; the Hill chunk's landmark."""
    from mathutils.bvhtree import BVHTree
    crown = _crown(OLD_TREE["masses"], "GL_Landmark_OldTree", resolution=0.12, voxel=0.07)
    results = []
    for lod, (crown_tris, scales, sides, rings, roots, branches, swing) in OLD_TREE_LODS.items():
        temp = bpy.data.objects.new("WORK_OldTree_LODTemp", crown.data.copy())
        bpy.data.collections["WORK"].objects.link(temp)
        decimate_to(temp, crown_tris)
        bm = bmesh.new()
        bm.from_mesh(temp.data)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        crown_end = len(bm.verts)
        tree = BVHTree.FromBMesh(bm)
        _scales_on(bm, tree, OLD_TREE["center"], OLD_TREE["radii"], scales, (0.3, 0.42), 811)
        foliage_end = len(bm.verts)
        points = []
        for i in range(rings + 1):
            t = i / rings
            points.append(Vector((0.18 * math.sin(t * 2.4), 0.12 * t * t, t * 2.75)))
        radii = [0.62 - 0.26 * (i / rings) ** 0.8 for i in range(rings + 1)]
        _tube(bm, points, radii, sides, cap_top=True,
              flare=lambda k, a: 1.0 + (0.7 if k == 0 else 0.25 if k == 1 else 0.0) * max(0.0, math.cos(a * 2.5 + 0.7)) ** 2)
        for r in range(roots):
            a = r / max(1, roots) * math.tau + 0.4
            d = Vector((math.cos(a), math.sin(a), 0.0))
            _tube(bm, [d * 0.35 + Vector((0, 0, 0.32)), d * 0.95 + Vector((0, 0, 0.06)), d * 1.35 + Vector((0, 0, -0.06))],
                  [0.2, 0.12, 0.05], 5, cap_top=True)
        if branches:
            for target, radius in ((Vector((1.25, 0.3, 3.05)), 0.16), (Vector((-1.2, -0.15, 3.1)), 0.15),
                                   (Vector((0.15, 1.1, 3.0)), 0.13)):
                start = Vector((0.15, 0.1, 2.45))
                mid = start + (target - start) * 0.5 + Vector((0, 0, 0.15))
                _tube(bm, [start, mid, target], [radius * 1.4, radius, radius * 0.55], max(4, sides - 3))
        swing_start = len(bm.verts)
        if swing:
            # Rope swing hanging from the long east branch, seat at knee height, facing the camera.
            for x in (0.72, 1.12):
                _prism(bm, Vector((x, 0.32, 2.98)), Vector((x, 0.32, 0.62)), 0.018, sides=4)
            _box(bm, Vector((0.92, 0.32, 0.6)), Vector((0.62, 0.22, 0.05)))
        bm.normal_update()
        data = temp.data
        bpy.data.objects.remove(temp)
        bpy.data.meshes.remove(data)
        obj = object_from_bmesh(f"GL_Landmark_OldTree_LOD{lod}", bm, collections if lod == 0 else lod_collections)
        _paint_foliage(obj, OLD_TREE["center"], OLD_TREE["radii"], crown_end, foliage_end, bark_grooves=True,
                       height=OLD_TREE["center"].z + OLD_TREE["radii"].z)
        if swing:
            attr = obj.data.color_attributes["PW_Regions"]
            for v in obj.data.vertices:
                if v.index >= swing_start:
                    # Rope and seat: plain bark wood (no foliage regions, no grooves).
                    attr.data[v.index].color = (0.0, 0.0, 0.0, 0.0)
        results.append(obj)
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    return results


def _ring(bm, z, radius, sides, phase=0.0):
    return [bm.verts.new((math.cos(phase + k / sides * math.tau) * radius, math.sin(phase + k / sides * math.tau) * radius, z))
            for k in range(sides)]


def _bridge(bm, a, b):
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))


def _windmill_tower(lod):
    """Stone tower with a wooden cap. Regions: base cream stone, R wood, G moss, B roof shingles, A dark openings."""
    sides = {0: 16, 1: 10, 2: 6}[lod]
    levels = {0: [(0.0, 1.22), (0.18, 1.18), (0.6, 1.12), (1.2, 1.06), (1.8, 0.99), (2.4, 0.93), (2.9, 0.87), (3.35, 0.82)],
              1: [(0.0, 1.2), (1.2, 1.04), (2.4, 0.9), (3.35, 0.82)],
              2: [(0.0, 1.2), (3.35, 0.82)]}[lod]
    bm = bmesh.new()
    rings = [_ring(bm, z, r, sides, phase=math.pi / sides) for z, r in levels]
    for a, b in zip(rings, rings[1:]):
        _bridge(bm, a, b)
    eave = _ring(bm, 3.38, 1.02, sides, phase=math.pi / sides)
    _bridge(bm, rings[-1], eave)
    cap_levels = {0: [(3.5, 1.0), (3.95, 0.86), (4.3, 0.6), (4.52, 0.3)], 1: [(3.55, 0.98), (4.2, 0.62), (4.5, 0.3)],
                  2: [(3.6, 0.95), (4.45, 0.35)]}[lod]
    previous = eave
    for z, r in cap_levels:
        ring = _ring(bm, z, r, sides, phase=math.pi / sides)
        _bridge(bm, previous, ring)
        previous = ring
    apex = bm.verts.new((0.0, 0.0, 4.68))
    for k in range(sides):
        bm.faces.new((previous[k], previous[(k + 1) % sides], apex))
    if lod < 2:
        # Stone plinth around the foot, a step at the door.
        plinth = [_ring(bm, z, r, sides, phase=math.pi / sides) for z, r in ((0.0, 1.36), (0.22, 1.33), (0.26, 1.2))]
        _bridge(bm, plinth[0], plinth[1])
        _bridge(bm, plinth[1], plinth[2])
        _box(bm, Vector((0.0, 1.38, 0.06)), Vector((0.8, 0.3, 0.12)))
    if lod == 0:
        # Wooden gallery around the tower under the cap: a deck ring and eight posts with a rail.
        deck_in = _ring(bm, 2.62, 0.9, sides)
        deck_out = _ring(bm, 2.62, 1.28, sides)
        deck_low = _ring(bm, 2.54, 1.28, sides)
        _bridge(bm, deck_in, deck_out)
        _bridge(bm, deck_out, deck_low)
        for k in range(8):
            a = k / 8 * math.tau + math.pi / 8
            base = Vector((math.cos(a) * 1.22, math.sin(a) * 1.22, 2.62))
            _prism(bm, base, base + Vector((0, 0, 0.42)), 0.03, sides=4)
        rail = _ring(bm, 3.02, 1.22, sides)
        rail_low = _ring(bm, 2.98, 1.22, sides)
        _bridge(bm, rail, rail_low)
    if lod < 2:
        # Arched door and two windows on the camera side (+Y), proud of the wall: dark inside a wood frame.
        _box(bm, Vector((0.0, 1.17, 0.48)), Vector((0.58, 0.08, 0.96)))
        _box(bm, Vector((0.0, 1.2, 0.48)), Vector((0.44, 0.06, 0.82)))
        for x, z in ((-0.42, 2.05), (0.4, 2.6)):
            r = 0.9 if z > 2.3 else 0.97
            _box(bm, Vector((x, r + 0.06, z)), Vector((0.34, 0.06, 0.4)))
            _box(bm, Vector((x, r + 0.09, z)), Vector((0.24, 0.05, 0.3)))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _windmill_sails(lod, cloth=None):
    """Four sails around the hub at the origin, in the XZ plane. `cloth` collects the canvas vertex indices."""
    bm = bmesh.new()
    hub_sides = {0: 8, 1: 6, 2: 4}[lod]
    _tube(bm, [Vector((0, -0.12, 0)), Vector((0, 0.18, 0))], [0.17, 0.15], hub_sides, cap_top=True)
    for k in range(4):
        a = k / 4 * math.tau + 0.3
        d = Vector((math.cos(a), 0.0, math.sin(a)))
        side = Vector((-math.sin(a), 0.0, math.cos(a)))
        if lod < 2:
            _prism(bm, d * 0.12 + Vector((0, 0.1, 0)), d * 2.15 + Vector((0, 0.1, 0)), 0.045, sides=4)
        inner, outer, width, twist = 0.42, 2.2, 0.56, 0.08
        quad = [d * inner + Vector((0, 0.12, 0)), d * outer + Vector((0, 0.12 + twist, 0)),
                d * outer + side * width + Vector((0, 0.1 + twist, 0)), d * inner + side * width * 0.9 + Vector((0, 0.1, 0))]
        start = len(bm.verts)
        _faces_two_sided(bm, quad)
        if cloth is not None:
            cloth.update(range(start, len(bm.verts)))
        if lod == 0:
            for t in (0.3, 0.55, 0.8):
                p = d * (inner + (outer - inner) * t)
                _prism(bm, p + Vector((0, 0.14, 0)), p + side * width * 0.96 + Vector((0, 0.13, 0)), 0.014, sides=4)
    bm.normal_update()
    return bm


def build_windmill(collections, lod_collections):
    out = []
    for lod in range(3):
        target = collections if lod == 0 else lod_collections
        tower = object_from_bmesh(f"GL_Landmark_Windmill_LOD{lod}", _windmill_tower(lod), target, smooth=False)
        mark_sharp_by_angle(tower, 35.0)

        def tower_regions(co, n, i):
            r = math.hypot(co.x, co.y)
            roof = 1.0 if co.z > 3.36 else 0.0
            gallery = 1.0 if 2.5 < co.z < 3.08 and r > 1.0 else 0.0
            front = co.y > 0.95 and abs(co.x) < 0.75 and co.z < 2.5 and r < 1.3
            dark = 1.0 if front and n.y > 0.7 and (co.y > 1.22 or (co.y > 0.97 and abs(co.x) > 0.25 and co.z > 1.5)) else 0.0
            frame = 1.0 if (front and not dark) or gallery else 0.0
            moss = smoothstep(0.3, 0.0, co.z) * (1.0 - roof) * (1.0 if r > 1.15 else 0.6)
            return (frame, moss, roof, dark)

        def tower_occlusion(co, n, i):
            # Stone courses and blocks, shingle rows on the roof: shading in the occlusion column, no texture.
            ground = 0.35 * (1.0 - smoothstep(0.0, 0.4, co.z))
            if co.z > 3.36:
                rows = 0.2 * smoothstep(0.55, 0.95, abs(math.sin(co.z * 14.0)))
                return (ground + rows, 0.0)
            angle = math.atan2(co.y, co.x)
            course = math.floor(co.z * 4.2)
            block = wave_noise(Vector((math.floor(angle * 4.0 + course * 0.5), course, 3.0)), 211, octaves=1)
            mortar = 0.22 * smoothstep(0.8, 0.98, abs(math.sin(co.z * 13.2)))
            return (ground + mortar + 0.16 * block, 0.0)

        write_layers(tower, tower_regions, tower_occlusion)
        canvas = set()
        sails = object_from_bmesh(f"GL_Landmark_Windmill_Sails_LOD{lod}", _windmill_sails(lod, canvas), target, smooth=False)

        def sail_regions(co, n, i):
            # Wood (R) for hub, arms and slats; the canvas keeps the base color (cream cloth).
            return (0.0 if i in canvas else 1.0, 0.0, 0.0, 0.0)

        write_layers(sails, sail_regions, lambda co, n, i: (0.0, 0.0))
        out += [tower, sails]
    return out


def build_landmarks_t33b():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    lods = [cols["LOD"], cols["EXPORT"]]
    out = build_old_tree(target, lods) + build_windmill(target, lods)
    return {o.name: triangle_count(o) for o in out}


# ---------------- small props ----------------

def build_fence_b(collections):
    """GL_Fence_B: a picket fence section (1.4 m), pointed pickets on two rails."""
    bm = bmesh.new()
    for k in range(7):
        x = -0.6 + k * 0.2
        h = 0.62 + 0.04 * math.sin(k * 1.7)
        _box(bm, Vector((x, 0.0, h * 0.5)), Vector((0.085, 0.035, h)))
        _faces_two_sided(bm, [Vector((x - 0.0425, 0.0, h)), Vector((x + 0.0425, 0.0, h)), Vector((x, 0.0, h + 0.09))])
    for z in (0.18, 0.46):
        _box(bm, Vector((0.0, -0.03, z)), Vector((1.42, 0.035, 0.06)))
    bm.normal_update()
    return _finish("GL_Fence_B", bm, collections,
                   lambda co, n, i: (0.0, 0.0, 0.0, 0.3 * smoothstep(0.55, 0.7, co.z)),
                   lambda co, n, i: (0.35 * (1.0 - smoothstep(0.0, 0.12, co.z)), 0.0), smooth=False)


def build_well(collections):
    """GL_WellTiny: stone ring with dark water, two posts, a little gable roof and a bucket. Regions: base stone,
    R wood, G moss, B water, A roof shingles."""
    bm = bmesh.new()
    sides = 10
    outer = [_ring(bm, z, r, sides) for z, r in ((0.0, 0.52), (0.5, 0.5), (0.58, 0.47))]
    inner = [_ring(bm, z, r, sides) for z, r in ((0.58, 0.36), (0.42, 0.36))]
    _bridge(bm, outer[0], outer[1])
    _bridge(bm, outer[1], outer[2])
    _bridge(bm, outer[2], inner[0])
    _bridge(bm, inner[0], inner[1])
    water = bm.verts.new((0.0, 0.0, 0.42))
    for k in range(sides):
        bm.faces.new((inner[1][k], inner[1][(k + 1) % sides], water))
    for x in (-0.44, 0.44):
        _box(bm, Vector((x, 0.0, 0.95)), Vector((0.08, 0.08, 1.0)))
    _prism(bm, Vector((-0.44, 0.0, 1.12)), Vector((0.44, 0.0, 1.12)), 0.035, sides=6)
    roof_start = len(bm.verts)
    for sign in (-1.0, 1.0):
        _box(bm, Vector((0.0, sign * 0.3, 1.52)), Vector((1.15, 0.62, 0.04)))
    bm.verts.ensure_lookup_table()
    for k in range(roof_start, len(bm.verts)):
        v = bm.verts[k]
        # Gable: the boards rise toward the ridge.
        v.co.z += 0.3 - abs(v.co.y) * 0.45
    _cup(bm, Vector((0.12, 0.0, 0.78)), 0.09, 0.14, 8, flare=0.1)
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    def regions(co, n, i):
        roof = 1.0 if roof_start <= i < roof_start + 16 else 0.0
        wood = 1.0 if (abs(abs(co.x) - 0.44) < 0.06 and co.z > 0.5) or (abs(co.z - 1.12) < 0.05 and abs(co.x) < 0.45) else 0.0
        inside = co.z < 0.45 and math.hypot(co.x, co.y) < 0.37 and co.z > 0.4
        moss = smoothstep(0.2, 0.0, co.z) * (1.0 - wood)
        return (wood * (1.0 - roof), moss, 1.0 if inside else 0.0, roof)

    def occlusion(co, n, i):
        inside = co.z < 0.58 and math.hypot(co.x, co.y) < 0.38
        return (0.3 * (1.0 - smoothstep(0.0, 0.15, co.z)) + (0.3 if inside else 0.0), 0.0)

    return _finish("GL_WellTiny", bm, collections, regions, occlusion, smooth=False)


def build_edge(kind, collections):
    """Island rim pieces for the north and south rims. A: stones bedded in the lip; B: hanging roots and a grass
    fringe over the soil wall. Pivot on the rim edge, +Y outward."""
    bm = bmesh.new()
    rng = random.Random(1200 + ord(kind))
    if kind == "A":
        for k in range(4):
            x = -0.9 + k * 0.6 + rng.uniform(-0.12, 0.12)
            size = rng.uniform(0.22, 0.36)
            _lump(bm, Vector((x, rng.uniform(-0.05, 0.12), rng.uniform(-0.25, 0.02))),
                  Vector((size, size * 0.8, size * 0.65)), 1300 + k, subdivisions=1)
    else:
        for k in range(6):
            x = -0.9 + k * 0.36 + rng.uniform(-0.08, 0.08)
            length = rng.uniform(0.35, 0.75)
            _tube(bm, [Vector((x, 0.0, 0.0)), Vector((x + 0.03, 0.08, -length * 0.5)), Vector((x - 0.02, 0.1, -length))],
                  [0.035, 0.025, 0.01], 4, cap_top=True)
        for k in range(9):
            _curved_blade(bm, Vector((-1.0 + k * 0.25, -0.02, 0.0)), Vector((0.0, 1.0, 0.0)), 0.22, 0.09, 0.9, 3)

    def regions(co, n, i):
        if kind == "A":
            return (0.0, smoothstep(0.55, 0.9, n.z), 0.0, 0.0)
        grass = 1.0 if co.z > -0.05 else 0.0
        return (grass, grass * smoothstep(-0.02, 0.15, co.z), 0.0, 0.0)

    return _finish(f"GL_Edge_{kind}", bm, collections, regions,
                   lambda co, n, i: (0.3 * smoothstep(0.0, -0.4, co.z), 0.0), smooth=kind == "A")


def build_stream_stones(collections):
    """GL_StreamStones: four flat stepping stones in a gentle line across the stream (local X along the path)."""
    bm = bmesh.new()
    rng = random.Random(1401)
    for k in range(4):
        x = -0.75 + k * 0.5
        size = rng.uniform(0.2, 0.26)
        _lump(bm, Vector((x, rng.uniform(-0.08, 0.08), -0.02)), Vector((size, size * 0.85, 0.075)), 1410 + k, subdivisions=1)

    def regions(co, n, i):
        wet = 1.0 - smoothstep(-0.04, 0.02, co.z)
        return (0.0, smoothstep(0.7, 0.95, n.z) * 0.6 * (1.0 - wet), 0.0, wet * 0.6)

    return _finish("GL_StreamStones", bm, collections, regions,
                   lambda co, n, i: (0.25 * (1.0 - smoothstep(-0.05, 0.03, co.z)), 0.0))


def build_props_t33b():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    out = [build_fence_b(target), build_well(target), build_edge("A", target), build_edge("B", target),
           build_stream_stones(target)]
    return {o.name: triangle_count(o) for o in out}
