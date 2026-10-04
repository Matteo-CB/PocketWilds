"""Grassland core kit generator (T15). Rebuilds BL_Biome_Grassland_Core.blend content from parameters.

Every prop is opaque, low poly, pivot at the base, +Z up, sized against the rabbit (0.65 m with ears).
Shared data layout for SH_EnvironmentOpaque / SH_GroundMaster (one palette row per renderer):
  - color attribute PW_Regions (POINT): R/G/B/A weights toward palette columns 1-4 over the base color.
  - UV map PW_Occlusion: x = baked occlusion (0 open, 1 deep), y = height factor (reserved for wind).
  Values are per vertex (no UV seams), so Unity never splits vertices for them.
Props (palette regions):
  rocks   base cream stone, G moss on top
  grass   base deep green, R lighter tips
  flowers base stem green, R petals or bells (palette varies), G flower heart
  tree    base bark, R foliage, G sunlit foliage, B shaded foliage
  ground  base grass, R light patches, G dark patches, B path, A soil sides (SH_GroundMaster)
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

ROCK_SIZES = {"S": (0.30, 0.26, 90), "M": (0.58, 0.46, 150), "L": (1.0, 0.78, 230)}  # width, height, target tris
GROUND_HALF = 7.0          # 14 m chunk
GROUND_FLAT_RADIUS = 5.0   # animals and egg live on a flat center
GROUND_DEPTH = 1.1


def smoothstep(e0, e1, x):
    if e0 == e1:
        return 1.0 if x >= e1 else 0.0
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def wave_noise(p, seed, octaves=3):
    """Deterministic smooth noise in roughly [-1, 1] from a few seeded sine waves."""
    rng = random.Random(seed)
    total, norm = 0.0, 0.0
    amp, freq = 1.0, 1.0
    for _ in range(octaves):
        for _ in range(3):
            d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
            total += amp * math.sin(p.dot(d) * freq * 2.4 + rng.uniform(0, 6.283))
            norm += amp
        amp *= 0.5
        freq *= 2.1
    return total / norm


# ---------------- Scene helpers ----------------
def ensure_collections():
    scene = bpy.context.scene
    result = {}
    for name in ("GEO", "LOD", "HELPERS", "EXPORT", "WORK"):
        c = bpy.data.collections.get(name)
        if c is None:
            c = bpy.data.collections.new(name)
            scene.collection.children.link(c)
        result[name] = c
    return result


def object_from_bmesh(name, bm, collections, smooth=True):
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.get(name) or bpy.data.objects.new(name, me)
    obj.data = me
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    for c in collections:
        c.objects.link(obj)
    for poly in me.polygons:
        poly.use_smooth = smooth
    return obj


def replace_with_evaluated(obj, modifier):
    """Applies one modifier without operators (works from any context)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    result = bpy.data.meshes.new_from_object(evaluated)
    old = obj.data
    obj.modifiers.remove(modifier)
    obj.data = result
    name = old.name
    bpy.data.meshes.remove(old)
    result.name = name
    return obj


def decimate_to(obj, target_tris):
    tris = triangle_count(obj)
    if tris <= target_tris:
        return obj
    mod = obj.modifiers.new("Decimate", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = target_tris / tris
    mod.use_collapse_triangulate = True
    return replace_with_evaluated(obj, mod)


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def mark_sharp_by_angle(obj, degrees):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    limit = math.radians(degrees)
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0.0) > limit)
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(me)
    bm.free()


def write_layers(obj, regions_fn, occlusion_fn):
    """regions_fn(co, normal, index) -> (r, g, b, a); occlusion_fn(co, normal, index) -> (occlusion, height)."""
    me = obj.data
    attr = me.color_attributes.get("PW_Regions") or me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="PW_Occlusion")
    per_vertex = []
    for v in me.vertices:
        attr.data[v.index].color = tuple(max(0.0, min(1.0, c)) for c in regions_fn(v.co, v.normal, v.index))
        occ, height = occlusion_fn(v.co, v.normal, v.index)
        per_vertex.append((max(0.0, min(1.0, occ)), max(0.0, min(1.0, height))))
    for loop in me.loops:
        uv.data[loop.index].uv = per_vertex[loop.vertex_index]
    me.color_attributes.active_color = attr


def set_custom_normals(obj, fn):
    me = obj.data
    normals = [fn(v.co, v.normal).normalized() for v in me.vertices]
    me.normals_split_custom_set_from_vertices(normals)


# ---------------- Rocks ----------------
def build_rock(size, collections, seed):
    width, height, target = ROCK_SIZES[size]
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=0.5)
    for v in bm.verts:
        p = v.co.normalized()
        bulge = 1.0 + 0.16 * wave_noise(p * 1.3, seed) + 0.06 * wave_noise(p * 3.1, seed + 7)
        v.co = Vector((p.x * width * 0.5 * bulge * (1.0 + 0.12 * p.y),
                       p.y * width * 0.45 * bulge,
                       p.z * height * 0.5 * bulge))
        # Flat-ish underside: rocks sit in the grass, never on a point.
        if v.co.z < -height * 0.28:
            v.co.z = -height * 0.28 + (v.co.z + height * 0.28) * 0.15
    bm.normal_update()
    min_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_z + height * 0.08  # sink 8% into the ground
    obj = object_from_bmesh(f"GL_Rock_{size}", bm, collections)
    decimate_to(obj, target)
    mark_sharp_by_angle(obj, 38.0)
    top = max(v.co.z for v in obj.data.vertices)

    def regions(co, n, i):
        moss = smoothstep(0.4, 0.85, n.z) * smoothstep(-0.35, 0.35, wave_noise(co * 4.0, seed + 3))
        return (0.0, moss, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.75 * (1.0 - smoothstep(0.0, top * 0.35, co.z)) + 0.2 * smoothstep(0.1, -0.6, n.z), co.z / max(top, 1e-4))

    write_layers(obj, regions, occlusion)
    return obj


# ---------------- Grass ----------------
GRASS_TUFTS = {"S": (6, 0.16, 101), "M": (8, 0.23, 202), "L": (10, 0.32, 303)}  # blades, height, seed


def build_grass(size, collections):
    blades, height, seed = GRASS_TUFTS[size]
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(blades):
        angle = (i / blades) * math.tau + rng.uniform(-0.3, 0.3)
        lean = rng.uniform(0.3, 0.6)
        h = height * rng.uniform(0.7, 1.0)
        w = height * rng.uniform(0.26, 0.34)
        base = Vector((math.cos(angle), math.sin(angle), 0.0)) * rng.uniform(0.0, height * 0.18)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0)) * w * 0.5
        mid = base + out * h * lean * 0.45 + Vector((0, 0, h * 0.55))
        tip = base + out * h * lean + Vector((0, 0, h))
        pts = [base - side, base + side, mid - side * 0.65, mid + side * 0.65, tip]
        front = [bm.verts.new(p) for p in pts]
        back = [bm.verts.new(p) for p in pts]
        for a, b, c in ((0, 1, 3), (0, 3, 2), (2, 3, 4)):
            bm.faces.new((front[a], front[b], front[c]))
            bm.faces.new((back[c], back[b], back[a]))
    obj = object_from_bmesh(f"GL_GrassTuft_{size}", bm, collections)
    # Soft upward normals: tufts light like the meadow they grow from, no harsh blade facets.
    set_custom_normals(obj, lambda co, n: Vector((co.x, co.y, 0.0)) * 0.6 + Vector((0, 0, 1.0)))

    def regions(co, n, i):
        return (smoothstep(0.15, 1.0, co.z / height), 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.6 * (1.0 - smoothstep(0.0, height * 0.45, co.z)), co.z / height)

    write_layers(obj, regions, occlusion)
    return obj


# ---------------- Flowers ----------------
def _prism(bm, a, b, radius, sides=4):
    """Thin closed prism from a to b (stems)."""
    axis = (b - a).normalized()
    ref = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
    u = axis.cross(ref).normalized()
    w = axis.cross(u).normalized()
    ring_a, ring_b = [], []
    for k in range(sides):
        t = k / sides * math.tau
        off = (u * math.cos(t) + w * math.sin(t)) * radius
        ring_a.append(bm.verts.new(a + off))
        ring_b.append(bm.verts.new(b + off * 0.8))
    for k in range(sides):
        n = (k + 1) % sides
        bm.faces.new((ring_a[k], ring_a[n], ring_b[n], ring_b[k]))
    bm.faces.new(list(reversed(ring_a)))
    bm.faces.new(ring_b)


def _leaf(bm, base, direction, length, width, lift=0.25):
    """Double-sided pointed leaf (4 verts per side)."""
    d = direction.normalized()
    side = Vector((-d.y, d.x, 0.0)).normalized() * width * 0.5
    mid = base + d * length * 0.5 + Vector((0, 0, length * lift))
    tip = base + d * length + Vector((0, 0, length * lift * 0.6))
    pts = [base, mid - side, tip, mid + side]
    f = [bm.verts.new(p) for p in pts]
    b = [bm.verts.new(p) for p in pts]
    bm.faces.new((f[0], f[1], f[2], f[3]))
    bm.faces.new((b[3], b[2], b[1], b[0]))


def _dome(bm, center, radius, height, sides=6):
    ring = [bm.verts.new(center + Vector((math.cos(k / sides * math.tau) * radius, math.sin(k / sides * math.tau) * radius, 0.0))) for k in range(sides)]
    top = bm.verts.new(center + Vector((0, 0, height)))
    bottom = bm.verts.new(center - Vector((0, 0, height * 0.3)))
    for k in range(sides):
        n = (k + 1) % sides
        bm.faces.new((ring[k], ring[n], top))
        bm.faces.new((ring[n], ring[k], bottom))


def _petal(bm, center, angle, length, width, tilt):
    out = Vector((math.cos(angle), math.sin(angle), 0.0))
    side = Vector((-out.y, out.x, 0.0)) * width * 0.5
    up = Vector((0, 0, 1))
    base = center + out * length * 0.15
    mid = center + out * length * 0.55 + up * tilt * 0.5
    tip = center + out * length + up * tilt
    thick = up * width * 0.12
    top = [bm.verts.new(p + thick) for p in (base, mid - side, tip, mid + side)]
    bot = [bm.verts.new(p - thick) for p in (base, mid - side, tip, mid + side)]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))


def build_flower(kind, collections):
    bm = bmesh.new()
    if kind == "A":  # daisy-like, five chunky petals
        h = 0.22
        _prism(bm, Vector((0, 0, 0)), Vector((0.01, 0.0, h)), 0.008)
        _leaf(bm, Vector((0, 0, 0.02)), Vector((1, 0.3, 0)), 0.09, 0.035)
        _leaf(bm, Vector((0, 0, 0.03)), Vector((-0.8, -0.5, 0)), 0.08, 0.03)
        head = Vector((0.01, 0.0, h))
        for k in range(5):
            _petal(bm, head, k / 5 * math.tau + 0.3, 0.07, 0.042, 0.012)
        _dome(bm, head + Vector((0, 0, 0.004)), 0.022, 0.016)
        heart_z = h
    elif kind == "B":  # bell flower, two hanging bells on an arched stem
        h = 0.24
        top = Vector((0.03, 0.0, h))
        _prism(bm, Vector((0, 0, 0)), top, 0.008)
        _leaf(bm, Vector((0, 0, 0.02)), Vector((-1, 0.4, 0)), 0.1, 0.035)
        for k, (dx, dz) in enumerate(((0.06, -0.03), (-0.02, -0.07))):
            attach = top + Vector((dx * 0.5, 0.0, dz * 0.3))
            bell_top = top + Vector((dx, 0.01 * k, dz))
            _prism(bm, attach, bell_top, 0.005, 3)
            sides = 6
            ring1 = [bm.verts.new(bell_top + Vector((math.cos(t) * 0.018, math.sin(t) * 0.018, -0.012))) for t in (i / sides * math.tau for i in range(sides))]
            ring2 = [bm.verts.new(bell_top + Vector((math.cos(t) * 0.032, math.sin(t) * 0.032, -0.05))) for t in (i / sides * math.tau for i in range(sides))]
            cap = bm.verts.new(bell_top)
            inner = bm.verts.new(bell_top + Vector((0, 0, -0.035)))
            for i in range(sides):
                n = (i + 1) % sides
                bm.faces.new((cap, ring1[i], ring1[n]))
                bm.faces.new((ring1[n], ring1[i], ring2[i], ring2[n]))
                bm.faces.new((ring2[n], ring2[i], inner))
        heart_z = None
    else:  # clover: two trefoils and a round pink-white head
        h = 0.16
        head = Vector((0.0, 0.01, h))
        _prism(bm, Vector((0, 0, 0)), head, 0.007)
        for base_angle, stem_h in ((0.4, 0.06), (2.6, 0.045)):
            leaf_center = Vector((math.cos(base_angle) * 0.05, math.sin(base_angle) * 0.05, stem_h))
            _prism(bm, Vector((0, 0, 0)), leaf_center, 0.005, 3)
            for k in range(3):
                a = base_angle + k / 3 * math.tau
                c = leaf_center + Vector((math.cos(a) * 0.022, math.sin(a) * 0.022, 0.004))
                ring = [bm.verts.new(c + Vector((math.cos(t) * 0.02, math.sin(t) * 0.02, 0.0))) for t in (i / 6 * math.tau for i in range(6))]
                ring_b = [bm.verts.new(v.co - Vector((0, 0, 0.003))) for v in ring]
                bm.faces.new(ring)
                bm.faces.new(list(reversed(ring_b)))
        bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=0.03,
                                  matrix=Matrix.Translation(head + Vector((0, 0, 0.02))))
        heart_z = None
    bm.normal_update()
    obj = object_from_bmesh(f"GL_Flower_{kind}", bm, collections)
    top = max(v.co.z for v in obj.data.vertices)

    def regions(co, n, i):
        if kind == "A":
            head = Vector((0.01, 0.0, heart_z))
            d = (co - head)
            radial = math.hypot(d.x, d.y)
            on_head = abs(d.z) < 0.03 and radial < 0.085
            heart = 1.0 if on_head and radial < 0.026 else 0.0
            petal = 1.0 if on_head and radial >= 0.026 else 0.0
            return (petal, heart, 0.0, 0.0)
        if kind == "B":
            return (1.0 if co.z < 0.215 and co.z > 0.11 and abs(co.x) < 0.11 and math.hypot(co.x - 0.03, co.y) > 0.012 else 0.0, 0.0, 0.0, 0.0)
        # Clover head is the only part above the leaves near the stem top.
        return (1.0 if co.z > h - 0.012 else 0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.5 * (1.0 - smoothstep(0.0, 0.08, co.z)), co.z / top)

    write_layers(obj, regions, occlusion)
    return obj


# ---------------- Tree ----------------
# Chibi tree: short S-curved trunk with flared roots and three branches into one smooth crown volume
# (clean union of a few masses, no random bumps), covered with small flat leaves laid like scales.
# The scales give a scalloped, leafy silhouette and close-up detail for 4 triangles each.
# Every foliage vertex (crown and leaves) takes its normal from the crown center: the crown shades
# as one soft plush volume (classic stylized foliage), the scales only shape the outline.
CANOPY_CENTER = Vector((0.0, 0.0, 1.8))
CANOPY_RADII = Vector((1.35, 1.25, 0.8))

# Crown lobes: center, radius. A broad dome: central mass, a ring of side lobes, one crown top.
TREE_MASSES = [
    (Vector((0.0, 0.0, 1.86)), 0.84),
    (Vector((-0.8, 0.1, 1.62)), 0.6),
    (Vector((0.82, -0.05, 1.64)), 0.62),
    (Vector((0.1, 0.66, 1.6)), 0.58),
    (Vector((0.05, -0.64, 1.56)), 0.55),
    (Vector((-0.56, -0.48, 1.72)), 0.5),
    (Vector((0.58, 0.48, 1.74)), 0.5),
    (Vector((-0.18, 0.08, 2.36)), 0.56),
]
TREE_BRANCHES = [  # toward a lobe center, with the branch radius at its base (seen under the crown)
    (Vector((-0.62, 0.08, 1.58)), 0.085),
    (Vector((0.62, -0.05, 1.6)), 0.085),
    (Vector((0.06, 0.5, 1.58)), 0.08),
]
TREE_TRUNK_TOP = Vector((0.04, 0.0, 1.5))  # buried in the crown: no visible trunk cap
# lod: (crown triangles, leaf scales, trunk sides, trunk rings, branches)
TREE_LODS = {0: (430, 52, 8, 5, True), 1: (250, 14, 6, 3, True), 2: (130, 0, 5, 2, False)}


def _trunk_axis(t):
    """Gentle S curve from the ground (t=0) to the trunk top (t=1)."""
    x = 0.06 * math.sin(t * math.pi * 1.1) - 0.02 * t
    return Vector((x + TREE_TRUNK_TOP.x * t, 0.015 * math.sin(t * math.pi * 2.0), t * TREE_TRUNK_TOP.z))


def _tube(bm, points, radii, sides, cap_top=True, flare=None):
    """Ring sweep along points; flare(k, angle) optionally pushes the first ring out (roots)."""
    rings = []
    for i, (p, r) in enumerate(zip(points, radii)):
        axis = (points[i + 1] - p).normalized() if i < len(points) - 1 else (p - points[i - 1]).normalized()
        ref = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
        u = axis.cross(ref).normalized()
        w = axis.cross(u).normalized()
        ring = []
        for k in range(sides):
            a = k / sides * math.tau
            radius = r * (flare(k, a) if (flare and i == 0) else 1.0)
            ring.append(bm.verts.new(p + (u * math.cos(a) + w * math.sin(a)) * radius))
        rings.append(ring)
    for a_ring, b_ring in zip(rings[:-1], rings[1:]):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((a_ring[k], a_ring[n], b_ring[n], b_ring[k]))
    if cap_top:
        bm.faces.new(rings[-1])
    return rings


def _crown_mesh():
    """Smooth union of the masses: metaball, voxel remesh, smoothing. Returns a temporary mesh."""
    work = bpy.data.collections["WORK"]
    data = bpy.data.metaballs.get("WORK_GL_Tree_A_Crown") or bpy.data.metaballs.new("WORK_GL_Tree_A_Crown")
    data.elements.clear()
    data.resolution = 0.08
    data.render_resolution = 0.08
    for center, radius in TREE_MASSES:
        e = data.elements.new(type='ELLIPSOID')
        e.co = center
        # With stiffness 2 and the default 0.6 threshold the surface sits at ~0.67 of the element
        # radius: scale up so each mass shows its nominal size, slightly flattened.
        # Stiffer field: lobes fuse at their base but keep their own rounded outline.
        e.radius = radius * 1.35
        e.size_x, e.size_y, e.size_z = 1.0, 1.0, 0.86
        e.stiffness = 3.0
    obj = bpy.data.objects.get("WORK_GL_Tree_A_Crown") or bpy.data.objects.new("WORK_GL_Tree_A_Crown", data)
    if obj.name not in work.objects:
        work.objects.link(obj)
    obj.hide_render = True
    obj.hide_viewport = False  # hidden objects are skipped by the depsgraph: keep it evaluated
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    obj.hide_set(True)
    temp = bpy.data.objects.new("WORK_GL_Tree_A_CrownTemp", mesh)
    work.objects.link(temp)
    remesh = temp.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = 0.05
    replace_with_evaluated(temp, remesh)
    smooth = temp.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.6
    smooth.iterations = 5
    replace_with_evaluated(temp, smooth)
    return temp


def _leaf_scale(bm, base, normal, down, size, lift):
    """Rounded leaf lying on the crown like a scale, pointing outward-down, tip barely lifted.
    Front face is forced to face outward; the back copy sits just under it."""
    n = normal.normalized()
    along = (down - n * down.dot(n))
    if along.length < 1e-3:
        along = n.cross(Vector((1, 0, 0)))
    along.normalize()
    side = n.cross(along).normalized()
    tip = base + along * size + n * size * lift
    mid = base + along * size * 0.48 + n * size * lift * 0.4
    pts = [base - along * size * 0.2, mid + side * size * 0.46, tip, mid - side * size * 0.46]
    front = bm.faces.new([bm.verts.new(p) for p in pts])
    back = bm.faces.new([bm.verts.new(p - n * 0.006) for p in reversed(pts)])
    front.normal_update()
    back.normal_update()
    if front.normal.dot(n) < 0.0:
        front.normal_flip()
    if back.normal.dot(n) > 0.0:
        back.normal_flip()


def build_tree(collections, lod_collections):
    from mathutils.bvhtree import BVHTree
    crown = _crown_mesh()
    results = []
    for lod, (crown_tris, scales, sides, rings, branches) in TREE_LODS.items():
        temp = bpy.data.objects.new("WORK_GL_Tree_A_LODTemp", crown.data.copy())
        bpy.data.collections["WORK"].objects.link(temp)
        decimate_to(temp, crown_tris)

        bm = bmesh.new()
        bm.from_mesh(temp.data)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        crown_end = len(bm.verts)
        tree = BVHTree.FromBMesh(bm)
        rng = random.Random(4242)
        # Leaf scales on the upper and side surface: Fibonacci directions, jittered, raycast onto the crown.
        golden = math.pi * (3.0 - math.sqrt(5.0))
        placed = 0
        for i in range(scales * 3):
            if placed >= scales:
                break
            z = 1.0 - 1.0 * (i + 0.5) / (scales * 3)   # upper hemisphere only
            r = math.sqrt(max(0.0, 1.0 - z * z))
            a = i * golden + rng.uniform(-0.2, 0.2)
            direction = Vector((math.cos(a) * r, math.sin(a) * r, z))
            if direction.z < 0.15:
                continue  # leaves sit on the lobe tops: side-on leaves would read as spikes
            origin = CANOPY_CENTER + Vector((direction.x * CANOPY_RADII.x, direction.y * CANOPY_RADII.y, direction.z * CANOPY_RADII.z)) * 2.0
            hit, normal, _, _ = tree.ray_cast(origin, -direction)
            if hit is None:
                continue
            size = rng.uniform(0.22, 0.3)
            _leaf_scale(bm, hit - normal * 0.015, normal, Vector((0, 0, -1)) + direction * 0.6, size, rng.uniform(0.08, 0.16))
            placed += 1
        foliage_end = len(bm.verts)

        points = [_trunk_axis(i / rings) for i in range(rings + 1)]
        radii = [0.27 - 0.1 * (i / rings) ** 0.7 for i in range(rings + 1)]
        _tube(bm, points, radii, sides, cap_top=True,
              flare=lambda k, a: 1.0 + 0.65 * max(0.0, math.cos(a * 2.0 + 0.4)) ** 3)
        if branches:
            for target, radius in TREE_BRANCHES:
                start = TREE_TRUNK_TOP + (target - TREE_TRUNK_TOP) * 0.05
                mid = TREE_TRUNK_TOP + (target - TREE_TRUNK_TOP) * 0.55 + Vector((0, 0, 0.08))
                _tube(bm, [start, mid, target], [radius * 1.3, radius, radius * 0.6], max(4, sides - 2))
        bm.normal_update()

        temp_mesh = temp.data
        bpy.data.objects.remove(temp)
        bpy.data.meshes.remove(temp_mesh)
        name = f"GL_Tree_A_LOD{lod}"
        obj = object_from_bmesh(name, bm, collections if lod == 0 else lod_collections)
        _tree_normals(obj, foliage_end)
        _paint_tree(obj, crown_end, foliage_end)
        results.append(obj)

    crown_mesh = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(crown_mesh)
    return results


def _canopy_dir(co):
    d = co - CANOPY_CENTER
    return Vector((d.x / CANOPY_RADII.x, d.y / CANOPY_RADII.y, d.z / CANOPY_RADII.z)).normalized()


def _tree_normals(obj, foliage_end):
    me = obj.data
    normals = []
    for v in me.vertices:
        if v.index < foliage_end:
            normals.append(v.normal.lerp(_canopy_dir(v.co), 0.8).normalized())
        else:
            normals.append(v.normal.copy())
    me.normals_split_custom_set_from_vertices(normals)


def _paint_tree(obj, crown_end, foliage_end):
    """Foliage: R foliage, G sunlit crown, B shaded underside. Bark: base color, A grooves (palette column 4).
    Leaf scales [crown_end, foliage_end) catch extra light at their tips: a scale pattern on the lit side."""
    sun_dir = Vector((0.45, -0.55, 0.7)).normalized()

    def regions(co, n, i):
        if i >= foliage_end:
            angle = math.atan2(co.y, co.x)
            grooves = smoothstep(0.55, 0.95, math.sin(angle * 5.0 + co.z * 3.0)) * 0.6
            return (0.0, 0.0, 0.0, grooves)
        c = _canopy_dir(co)
        sun = smoothstep(0.05, 0.85, c.dot(sun_dir)) * smoothstep(-0.25, 0.45, c.z)
        shade = smoothstep(-0.05, -0.75, c.z)
        # Scale-to-scale variation: some leaves catch a little more light, like real foliage.
        flicker = 0.18 * (0.5 + 0.5 * wave_noise(co * 3.1, 77, octaves=1))
        if i >= crown_end:
            return (1.0, min(1.0, 0.35 + sun * 0.75 + flicker), shade * 0.5, 0.0)
        return (1.0, min(1.0, sun * 0.85 + flicker * 0.5), shade, 0.0)

    def occlusion(co, n, i):
        if i >= foliage_end:
            under_canopy = smoothstep(0.95, 1.3, co.z)
            return (0.3 * (1.0 - smoothstep(0.0, 0.35, co.z)) + 0.5 * under_canopy, co.z / 3.0)
        depth = 1.0 - smoothstep(0.7, 1.0, (co - CANOPY_CENTER).length / 1.15)
        return (0.25 * smoothstep(0.0, -0.8, _canopy_dir(co).z) + 0.35 * depth, co.z / 3.0)

    write_layers(obj, regions, occlusion)


# ---------------- Ground ----------------
def superellipse(theta, power=4.0):
    c, s = math.cos(theta), math.sin(theta)
    r = (abs(c) ** power + abs(s) ** power) ** (-1.0 / power)
    return c * r, s * r


def build_ground(collections, rings=10, segments=56, seed=41):
    bm = bmesh.new()
    center = bm.verts.new((0.0, 0.0, 0.0))
    radii = [GROUND_HALF * (i / rings) ** 0.9 for i in range(1, rings + 1)]
    grid = []
    for ri, r in enumerate(radii):
        ring = []
        for j in range(segments):
            theta = j / segments * math.tau
            sx, sy = superellipse(theta)
            x, y = sx * r, sy * r
            dist = math.hypot(x, y)
            # Flat play area, gentle rolls outside it, soft rounded lip at the rim.
            rolls = smoothstep(GROUND_FLAT_RADIUS * 0.85, GROUND_FLAT_RADIUS * 1.25, dist)
            z = rolls * 0.22 * (0.5 + 0.5 * wave_noise(Vector((x * 0.25, y * 0.25, 0.0)), seed))
            if ri == rings - 1:
                z -= 0.06
            ring.append(bm.verts.new((x, y, z)))
        grid.append(ring)
    for j in range(segments):
        n = (j + 1) % segments
        bm.faces.new((center, grid[0][j], grid[0][n]))
    for ri in range(rings - 1):
        a, b = grid[ri], grid[ri + 1]
        for j in range(segments):
            n = (j + 1) % segments
            bm.faces.new((a[j], b[j], b[n], a[n]))
    # Diorama side: rounded lip then soil wall tapering inward, closed underneath.
    previous = grid[-1]
    for depth, inset in ((0.18, 0.08), (0.55, 0.16), (GROUND_DEPTH, 0.3)):
        ring = []
        for j in range(segments):
            theta = j / segments * math.tau
            sx, sy = superellipse(theta)
            r = GROUND_HALF - inset
            ring.append(bm.verts.new((sx * r, sy * r, -depth)))
        for j in range(segments):
            n = (j + 1) % segments
            bm.faces.new((previous[j], ring[j], ring[n], previous[n]))
        previous = ring
    bottom = bm.verts.new((0.0, 0.0, -GROUND_DEPTH))
    for j in range(segments):
        n = (j + 1) % segments
        bm.faces.new((previous[n], previous[j], bottom))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = object_from_bmesh("GL_Ground_Core_A", bm, collections)
    mark_sharp_by_angle(obj, 50.0)

    def regions(co, n, i):
        if co.z < -0.12:
            return (0.0, 0.0, 0.0, 1.0)  # soil wall
        light = smoothstep(0.25, 0.75, wave_noise(Vector((co.x * 0.35, co.y * 0.35, 1.0)), seed + 1))
        dark = smoothstep(0.35, 0.85, wave_noise(Vector((co.x * 0.3, co.y * 0.3, 2.0)), seed + 2))
        # A soft worn clearing where the egg waits, in the middle of the play area.
        clearing = 1.0 - smoothstep(0.7, 1.35, math.hypot(co.x, co.y + 0.6))
        soil_lip = smoothstep(-0.02, -0.12, co.z)
        return (light * 0.85, dark * 0.75, clearing * 0.5, soil_lip)

    def occlusion(co, n, i):
        lip = smoothstep(-0.05, -0.2, co.z) * (1.0 - smoothstep(-0.6, -1.0, co.z)) * 0.35
        under = smoothstep(-0.6, -1.05, co.z) * 0.6
        return (lip + under, 0.0)

    write_layers(obj, regions, occlusion)
    return obj


def build_all():
    cols = ensure_collections()
    geo, lod, export = cols["GEO"], cols["LOD"], cols["EXPORT"]
    out = [build_ground([geo, export])]
    out += [build_grass(s, [geo, export]) for s in ("S", "M", "L")]
    out += [build_flower(k, [geo, export]) for k in ("A", "B", "C")]
    out += build_tree([geo, export], [lod, export])
    out += [build_rock(s, [geo, export], seed) for s, seed in (("S", 11), ("M", 23), ("L", 37))]
    return out


# ---------------- Review preview (WORK only, never exported) ----------------
# Same 8-column layout as DEF_Palettes_Grassland: base, R, G, B, A, occlusion tint, accent.
PREVIEW_PALETTES = {
    "ground": ("8CC56B", "A9D77F", "6FA957", "E4D9A8", "9C7458", "4F6B45", "B4D873"),
    "grass": ("74B35A", "B2DE86", "B2DE86", "74B35A", "74B35A", "4E7A43", "B2DE86"),
    "rock": ("E8DCC4", "E8DCC4", "9CC77A", "E8DCC4", "E8DCC4", "A39478", "FFFFFF"),
    "tree": ("93694F", "7DB960", "A9D57C", "578F4B", "6E4C3A", "3B6338", "FFFFFF"),
    "flower_white": ("6FAF55", "F8F4EA", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF"),
    "flower_blue": ("6FAF55", "A9C8F2", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF"),
    "flower_pink": ("6FAF55", "F3C4D6", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF"),
}


def _srgb_to_linear(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def paint_preview(obj, palette_key):
    """Writes WORK_Preview (point color) = what the master shader computes, before lighting."""
    pal = [_srgb_to_linear(h) for h in PREVIEW_PALETTES[palette_key]]
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


def clear_preview(obj):
    me = obj.data
    prev = me.color_attributes.get("WORK_Preview")
    if prev is not None:
        me.color_attributes.remove(prev)
    if "PW_Regions" in me.color_attributes:
        me.color_attributes.active_color = me.color_attributes["PW_Regions"]
