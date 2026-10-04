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
TREE_LOD_RATIOS = (1.0, 0.45, 0.2)
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
TREE_MASSES = [  # center, radius
    (Vector((0.0, 0.05, 1.95)), 0.82),
    (Vector((-0.62, 0.12, 1.55)), 0.6),
    (Vector((0.6, -0.12, 1.6)), 0.64),
    (Vector((0.05, -0.35, 1.5)), 0.5),
    (Vector((0.15, 0.45, 1.55)), 0.48),
]


def build_tree(collections, lod_collections):
    work = bpy.data.collections["WORK"]
    # Foliage: metaball union for soft lumpy masses, then remesh and decimate per LOD.
    mb_data = bpy.data.metaballs.get("WORK_GL_Tree_A_Foliage") or bpy.data.metaballs.new("WORK_GL_Tree_A_Foliage")
    mb_data.elements.clear()
    mb_data.resolution = 0.12
    mb_data.render_resolution = 0.12
    for center, radius in TREE_MASSES:
        e = mb_data.elements.new()
        e.co = center
        e.radius = radius
        e.stiffness = 2.2
    rng = random.Random(77)
    for _ in range(9):
        c, r = TREE_MASSES[rng.randrange(len(TREE_MASSES))]
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 1))).normalized()
        e = mb_data.elements.new()
        e.co = c + d * r * 0.8
        e.radius = r * 0.42
        e.stiffness = 2.0
    mb = bpy.data.objects.get("WORK_GL_Tree_A_Foliage") or bpy.data.objects.new("WORK_GL_Tree_A_Foliage", mb_data)
    if mb.name not in work.objects:
        work.objects.link(mb)
    mb.hide_render = True
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    foliage_mesh = bpy.data.meshes.new_from_object(mb.evaluated_get(depsgraph))

    results = []
    for lod in range(len(TREE_LOD_RATIOS)):
        bm = bmesh.new()
        bm.from_mesh(foliage_mesh)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.01)
        temp = object_from_bmesh("WORK_GL_Tree_A_FoliageTemp", bm, [work])
        decimate_to(temp, {0: 760, 1: 330, 2: 150}[lod])

        bm = bmesh.new()
        bm.from_mesh(temp.data)
        foliage_verts = len(bm.verts)
        _trunk(bm, sides=8 if lod < 2 else 5, rings=4 if lod == 0 else (3 if lod == 1 else 2))
        temp_mesh = temp.data
        bpy.data.objects.remove(temp)
        bpy.data.meshes.remove(temp_mesh)

        name = f"GL_Tree_A_LOD{lod}"
        obj = object_from_bmesh(name, bm, collections if lod == 0 else lod_collections)
        _paint_tree(obj, foliage_verts)
        results.append(obj)
    bpy.data.meshes.remove(foliage_mesh)
    return results


def _trunk(bm, sides, rings):
    """Short tapered trunk with a flared base, ending inside the canopy."""
    pts = []
    for i in range(rings + 1):
        t = i / rings
        z = t * 1.45
        radius = 0.15 + 0.13 * (1.0 - t) ** 2.2 + (0.07 if i == 0 else 0.0)
        bend = Vector((0.05 * math.sin(t * 2.0), 0.0, 0.0))
        pts.append((z, radius, bend))
    previous = None
    for z, radius, bend in pts:
        ring = [bm.verts.new(Vector((math.cos(k / sides * math.tau) * radius, math.sin(k / sides * math.tau) * radius, z)) + bend) for k in range(sides)]
        if previous:
            for k in range(sides):
                n = (k + 1) % sides
                bm.faces.new((previous[k], previous[n], ring[n], ring[k]))
        previous = ring
    bm.faces.new(previous)


def _paint_tree(obj, foliage_verts):
    """Vertices [0, foliage_verts) are foliage, the rest is bark (built in that order)."""
    canopy_center = Vector((0.0, 0.0, 1.7))
    sun_dir = Vector((0.4, -0.5, 0.75)).normalized()

    def regions(co, n, i):
        if i >= foliage_verts:
            return (0.0, 0.0, 0.0, 0.0)
        sun = smoothstep(0.25, 0.85, n.z) * smoothstep(-0.3, 0.6, n.dot(sun_dir))
        shade = smoothstep(-0.1, -0.7, n.z)
        return (1.0, sun, shade, 0.0)

    def occlusion(co, n, i):
        if i >= foliage_verts:
            return (0.35 * (1.0 - smoothstep(0.0, 0.5, co.z)) + 0.45 * smoothstep(1.0, 1.4, co.z), co.z / 2.8)
        inner = 1.0 - smoothstep(0.55, 1.0, (co - canopy_center).length / 1.2)
        return (0.25 * smoothstep(0.0, -0.8, n.z) + 0.3 * inner, co.z / 2.8)

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
    "tree": ("9B7156", "86C266", "B6DE84", "5F9E52", "86C266", "3F6A3C", "FFFFFF"),
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
