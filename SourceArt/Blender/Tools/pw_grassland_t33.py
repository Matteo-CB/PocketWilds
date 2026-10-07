"""Grassland Magnifique kit (T33): scatter vocabulary, bushes, more trees, hero rocks, small props, ambient life.

Same data contract as pw_grassland.py (PW_Regions color attribute, PW_Occlusion uv: x occlusion, y height factor
0 at the root to 1 at the tip, read by SH_FoliageWind for wind). Pivot at the base, +Z up, rabbit = 0.65 m.

Usage inside Blender (the grassland helpers must be loaded first, in the same namespace):
    ns = {}
    exec(open(r"<project>/SourceArt/Blender/Tools/pw_grassland.py").read(), ns)
    exec(open(r"<project>/SourceArt/Blender/Tools/pw_grassland_t33.py").read(), ns)
    ns["build_scatter_kit"]()      # GL_GrassWisp_*, GL_Flower_D/E/F, GL_Pebble_*, GL_Leaf_A, GL_Petals_A, ...
    ns["build_props_kit"]()        # GL_Bush_*, GL_Tree_B/C/D (LOD0-2), GL_RockHero_*, GL_Log, GL_Stump, ...
    ns["build_life_kit"]()         # GL_Life_Butterfly, GL_Life_Bee, GL_Life_Bird

Palette regions per family:
  wisp     base deep green, R lighter tips, G straw seed heads
  flowers  base stem green, R petals / florets, G heart
  pebble   base stone, G moss
  leaf     base green, R yellowing tip, G vein
  petals   R petal color (flower palettes)
  clover   base dark green, R lighter leaflets, G white chevron
  mushroom base cream stem, R cap, G spots
  bush     like trees: R foliage, G sunlit, B shaded, A blossoms or berries
  wood     base bark, R cut wood, G moss, A grooves
  life     butterfly: R wings, G marks, B body; bee: R wings, G body, B stripes; bird: R breast, G wing, B beak
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---------------- small mesh helpers ----------------


def _faces_two_sided(bm, points):
    front = [bm.verts.new(p) for p in points]
    back = [bm.verts.new(p) for p in points]
    for i in range(1, len(points) - 1):
        bm.faces.new((front[0], front[i], front[i + 1]))
        bm.faces.new((back[i + 1], back[i], back[0]))


def _curved_blade(bm, base, out, height, width, lean, segments):
    """Double-sided tapered blade bending outward: (segments - 1) quads and a tip triangle per side."""
    side = Vector((-out.y, out.x, 0.0)).normalized() * width * 0.5
    rows = []
    for k in range(segments):
        t = k / segments
        p = base + out * height * lean * t * t + Vector((0, 0, height * t))
        taper = 1.0 - 0.75 * t
        rows.append((p - side * taper, p + side * taper))
    tip = base + out * height * lean + Vector((0, 0, height))
    for face_side in (0, 1):
        verts = [(bm.verts.new(a), bm.verts.new(b)) for a, b in rows]
        tip_v = bm.verts.new(tip)
        for k in range(segments - 1):
            a0, b0 = verts[k]
            a1, b1 = verts[k + 1]
            quad = (a0, b0, b1, a1) if face_side == 0 else (a1, b1, b0, a0)
            bm.faces.new(quad)
        a, b = verts[-1]
        bm.faces.new((a, b, tip_v) if face_side == 0 else (tip_v, b, a))


def _bipyramid(bm, center, radius, height, sides=4, twist=0.0):
    ring = [bm.verts.new(center + Vector((math.cos(k / sides * math.tau + twist) * radius,
                                         math.sin(k / sides * math.tau + twist) * radius, 0.0))) for k in range(sides)]
    top = bm.verts.new(center + Vector((0, 0, height * 0.5)))
    bottom = bm.verts.new(center - Vector((0, 0, height * 0.5)))
    for k in range(sides):
        n = (k + 1) % sides
        bm.faces.new((ring[k], ring[n], top))
        bm.faces.new((ring[n], ring[k], bottom))


def _cup(bm, center, radius, depth, petals, flare=0.35):
    """Open flower cup: rounded petals rising from the heart, double-sided."""
    for k in range(petals):
        a = k / petals * math.tau
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0)) * radius * 0.62
        base = center + out * radius * 0.12
        mid = center + out * radius * 0.62 + Vector((0, 0, depth * 0.55))
        tip = center + out * radius * (0.95 + flare * 0.2) + Vector((0, 0, depth))
        _faces_two_sided(bm, [base, mid - side, tip, mid + side])


def _lump(bm, center, radii, seed, subdivisions=1):
    """Low-poly rounded lump (pebbles, mushroom caps, bodies)."""
    rng = random.Random(seed)
    temp = bmesh.new()
    bmesh.ops.create_icosphere(temp, subdivisions=subdivisions, radius=1.0)
    mapping = {}
    for v in temp.verts:
        d = v.co.normalized()
        bulge = 1.0 + rng.uniform(-0.12, 0.12)
        mapping[v] = bm.verts.new(center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * bulge)
    for f in temp.faces:
        bm.faces.new([mapping[v] for v in f.verts])
    temp.free()


def _finish(name, bm, collections, regions, occlusion, smooth=True, up_normals=0.0):
    bm.normal_update()
    obj = object_from_bmesh(name, bm, collections, smooth=smooth)
    if up_normals > 0.0:
        set_custom_normals(obj, lambda co, n: n.lerp(Vector((0, 0, 1)), up_normals))
    write_layers(obj, regions, occlusion)
    return obj


def _height(obj):
    return max(max(v.co.z for v in obj.data.vertices), 1e-4)


# ---------------- scatter: grass wisps ----------------
WISPS = {"S": (4, 0.24, 0, 501), "M": (4, 0.34, 1, 502), "L": (4, 0.46, 1, 503)}  # blades, height, seed stalks, seed


def build_wisp(size, collections):
    blades, height, stalks, seed = WISPS[size]
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(blades):
        a = i / blades * math.tau + rng.uniform(-0.4, 0.4)
        out = Vector((math.cos(a), math.sin(a), 0.0))
        h = height * rng.uniform(0.75, 1.0)
        _curved_blade(bm, out * rng.uniform(0.0, 0.02), out, h, height * 0.11, rng.uniform(0.25, 0.5), 3)
    seed_heads = []
    for k in range(stalks):
        # One soft oat-like plume, barely above the blades: a hint of seeds, not a field of sticks.
        a = rng.uniform(0, math.tau)
        top = Vector((math.cos(a) * height * 0.1, math.sin(a) * height * 0.1, height * rng.uniform(0.98, 1.06)))
        _prism(bm, Vector((0, 0, 0)), top, 0.0045, 3)
        head = top + Vector((0, 0, height * 0.08))
        _bipyramid(bm, head, height * 0.05, height * 0.17, 4, a)
        seed_heads.append(head)

    def regions(co, n, i):
        on_seed = any((co - h).length < height * 0.095 for h in seed_heads)
        return (smoothstep(0.2, 0.95, co.z / height), 1.0 if on_seed else 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.55 * (1.0 - smoothstep(0.0, height * 0.4, co.z)), min(1.0, co.z / height))

    return _finish(f"GL_GrassWisp_{size}", bm, collections, regions, occlusion, up_normals=0.65)


# ---------------- scatter: flowers D (poppy), E (lavender), F (buttercup) ----------------
def build_flower_t33(kind, collections):
    bm = bmesh.new()
    rng = random.Random({"D": 61, "E": 62, "F": 63}[kind])
    hearts = []
    florets_top = 0.0
    if kind == "D":  # poppy: one tall slightly bent stem, an open four-petal cup, a dark heart
        h = 0.27
        head = Vector((0.025, 0.01, h))
        _prism(bm, Vector((0, 0, 0)), head, 0.007, 4)
        _leaf(bm, Vector((0, 0, 0.025)), Vector((0.8, -0.6, 0)), 0.09, 0.03)
        _cup(bm, head, 0.06, 0.04, 4, flare=0.5)
        _dome(bm, head + Vector((0, 0, 0.012)), 0.016, 0.014, 5)
        hearts.append(head + Vector((0, 0, 0.014)))
    elif kind == "E":  # lavender: two stems, each a spike of stacked florets
        h = 0.32
        for k, (dx, dy, lean) in enumerate(((0.0, 0.0, 0.0), (0.03, 0.02, 0.05))):
            top = Vector((dx + lean, dy, h * (0.85 + 0.15 * k)))
            _prism(bm, Vector((dx * 0.3, dy * 0.3, 0.0)), top - Vector((0, 0, 0.07)), 0.005, 3)
            for f in range(4):
                c = top - Vector((0, 0, 0.022 * f))
                _bipyramid(bm, c, 0.014 - f * 0.001, 0.03, 4, f * 0.6 + k)
            florets_top = max(florets_top, top.z - 0.09)
        _leaf(bm, Vector((0, 0, 0.02)), Vector((-0.7, 0.5, 0)), 0.1, 0.018, lift=0.5)
        _leaf(bm, Vector((0, 0, 0.02)), Vector((0.6, -0.6, 0)), 0.09, 0.018, lift=0.5)
    else:  # buttercup: three short stems, small glossy cups
        h = 0.17
        for k in range(3):
            a = k / 3 * math.tau + 0.4
            head = Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, h * (0.8 + 0.12 * k)))
            _prism(bm, Vector((0, 0, 0)), head, 0.005, 3)
            _cup(bm, head, 0.032, 0.022, 5)
            hearts.append(head + Vector((0, 0, 0.004)))
        _leaf(bm, Vector((0, 0, 0.012)), Vector((1, 0.2, 0)), 0.07, 0.04, lift=0.15)
    bm.normal_update()
    top_z = max(v.co.z for v in bm.verts)

    def regions(co, n, i):
        if kind == "E":
            return (1.0 if co.z > florets_top else 0.0, 0.0, 0.0, 0.0)
        near_heart = any((co - hc).length < (0.02 if kind == "D" else 0.011) for hc in hearts)
        on_head = any(abs(co.z - hc.z) < 0.05 and math.hypot(co.x - hc.x, co.y - hc.y) < 0.075 for hc in hearts)
        if near_heart:
            return (0.0, 1.0, 0.0, 0.0)
        return (1.0 if on_head else 0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.5 * (1.0 - smoothstep(0.0, 0.08, co.z)), min(1.0, co.z / top_z))

    return _finish(f"GL_Flower_{kind}", bm, collections, regions, occlusion)


# ---------------- scatter: ground details ----------------
def build_pebble(kind, collections):
    bm = bmesh.new()
    if kind == "A":
        _lump(bm, Vector((0, 0, 0.02)), Vector((0.07, 0.055, 0.04)), 71)
    elif kind == "B":
        _lump(bm, Vector((0, 0, 0.018)), Vector((0.1, 0.045, 0.032)), 72)
    else:  # C: a pair
        _lump(bm, Vector((-0.04, 0.0, 0.016)), Vector((0.05, 0.04, 0.03)), 73)
        _lump(bm, Vector((0.05, 0.03, 0.012)), Vector((0.035, 0.03, 0.022)), 74)
    for v in bm.verts:
        v.co.z = max(v.co.z, -0.004)

    def regions(co, n, i):
        return (0.0, smoothstep(0.55, 0.95, n.z) * 0.6, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.5 * smoothstep(0.02, -0.004, co.z), 0.0)

    return _finish(f"GL_Pebble_{kind}", bm, collections, regions, occlusion, smooth=True)


def build_leaf(collections):
    """Fallen leaf, slightly curled, lying on the grass."""
    bm = bmesh.new()
    pts = [Vector((0, -0.05, 0.004)), Vector((-0.028, -0.01, 0.012)), Vector((-0.024, 0.03, 0.014)),
           Vector((0, 0.06, 0.008)), Vector((0.024, 0.03, 0.014)), Vector((0.028, -0.01, 0.012))]
    _faces_two_sided(bm, pts)

    def regions(co, n, i):
        return (smoothstep(-0.01, 0.06, co.y), 1.0 if abs(co.x) < 0.004 else 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.2, 0.0)

    return _finish("GL_Leaf_A", bm, collections, regions, occlusion, up_normals=0.5)


def build_petals(collections):
    """A few fallen petals: a soft color note at the foot of flower patches."""
    bm = bmesh.new()
    rng = random.Random(81)
    for k in range(4):
        a = rng.uniform(0, math.tau)
        c = Vector((math.cos(a) * rng.uniform(0.02, 0.07), math.sin(a) * rng.uniform(0.02, 0.07), 0.006))
        d = Vector((math.cos(a * 1.7), math.sin(a * 1.7), 0.0)) * 0.018
        s = Vector((-d.y, d.x, 0.0)) * 0.6
        _faces_two_sided(bm, [c - d, c - s, c + d * 1.2, c + s])

    def regions(co, n, i):
        return (1.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.1, 0.0)

    return _finish("GL_Petals_A", bm, collections, regions, occlusion, up_normals=0.7)


def build_clover(collections):
    """Low clover patch hugging the ground: three trefoils, one-sided (seen from above)."""
    bm = bmesh.new()
    rng = random.Random(91)
    for k in range(3):
        a0 = k / 3 * math.tau + rng.uniform(-0.3, 0.3)
        center = Vector((math.cos(a0) * 0.06, math.sin(a0) * 0.06, 0.025 + 0.01 * k))
        _prism(bm, Vector((0, 0, 0)), center, 0.004, 3)
        for j in range(3):
            a = a0 + j / 3 * math.tau
            out = Vector((math.cos(a), math.sin(a), 0.0))
            side = Vector((-out.y, out.x, 0.0))
            tip = center + out * 0.035
            fan = [center, center + out * 0.012 + side * 0.016, tip + side * 0.008, tip - side * 0.008,
                   center + out * 0.012 - side * 0.016]
            vs = [bm.verts.new(p) for p in fan]
            for f in range(1, len(vs) - 1):
                bm.faces.new((vs[0], vs[f], vs[f + 1]))

    def regions(co, n, i):
        r = math.hypot(co.x, co.y)
        return (0.6 if co.z > 0.02 else 0.0, 1.0 if 0.02 < r < 0.045 and co.z > 0.02 and (int(r * 400) % 3 == 0) else 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.45 * (1.0 - smoothstep(0.0, 0.03, co.z)), min(1.0, co.z / 0.05))

    return _finish("GL_Clover_A", bm, collections, regions, occlusion, up_normals=0.8)


def build_mushroom(collections):
    """Two tiny toadstools: cream stems, rounded caps with pale spots."""
    bm = bmesh.new()
    caps = []
    for (x, y, h, r) in ((0.0, 0.0, 0.07, 0.035), (0.045, 0.025, 0.045, 0.024)):
        _prism(bm, Vector((x, y, 0.0)), Vector((x, y, h)), r * 0.32, 5)
        cap_c = Vector((x, y, h - r * 0.15))
        ring = [bm.verts.new(cap_c + Vector((math.cos(k / 6 * math.tau) * r, math.sin(k / 6 * math.tau) * r, 0.0))) for k in range(6)]
        top = bm.verts.new(cap_c + Vector((0, 0, r * 0.75)))
        under = bm.verts.new(cap_c + Vector((0, 0, r * 0.1)))
        for k in range(6):
            n = (k + 1) % 6
            bm.faces.new((ring[k], ring[n], top))
            bm.faces.new((ring[n], ring[k], under))
        caps.append((cap_c, r))

    def regions(co, n, i):
        for c, r in caps:
            if co.z >= c.z - 0.002 and (co - c).length <= r * 1.05 and n.z > -0.2:
                spot = 1.0 if (int((co.x + co.y) * 300) % 4 == 0 and n.z > 0.5) else 0.0
                return (1.0, spot, 0.0, 0.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.5 * (1.0 - smoothstep(0.0, 0.03, co.z)), 0.0)

    return _finish("GL_Mushroom_Tiny", bm, collections, regions, occlusion)


def build_scatter_kit():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    out = [build_wisp(s, target) for s in ("S", "M", "L")]
    out += [build_flower_t33(k, target) for k in ("D", "E", "F")]
    out += [build_pebble(k, target) for k in ("A", "B", "C")]
    out += [build_leaf(target), build_petals(target), build_clover(target), build_mushroom(target)]
    return {o.name: triangle_count(o) for o in out}


# ---------------- props: crowns (bushes and trees share the T15 crown method) ----------------
def _crown(masses, name, resolution=0.08, voxel=0.05, squash=0.86, stiffness=3.0):
    """Smooth union of ellipsoid masses: metaball, voxel remesh, smoothing (pw_grassland._crown_mesh, parametrized)."""
    work = bpy.data.collections["WORK"]
    data = bpy.data.metaballs.get("WORK_" + name) or bpy.data.metaballs.new("WORK_" + name)
    data.elements.clear()
    data.resolution = resolution
    data.render_resolution = resolution
    for center, radius in masses:
        e = data.elements.new(type='ELLIPSOID')
        e.co = center
        e.radius = radius * 1.35
        e.size_x, e.size_y, e.size_z = 1.0, 1.0, squash
        e.stiffness = stiffness
    obj = bpy.data.objects.get("WORK_" + name) or bpy.data.objects.new("WORK_" + name, data)
    if obj.name not in work.objects:
        work.objects.link(obj)
    obj.hide_render = True
    obj.hide_viewport = False
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    obj.hide_set(True)
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


def _scales_on(bm, crown_bvh, center, radii, count, size_range, seed):
    """Leaf scales laid on the upper crown (Fibonacci directions raycast onto the surface)."""
    rng = random.Random(seed)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    placed = 0
    for i in range(count * 3):
        if placed >= count:
            break
        z = 1.0 - (i + 0.5) / (count * 3)
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a = i * golden + rng.uniform(-0.2, 0.2)
        direction = Vector((math.cos(a) * r, math.sin(a) * r, z))
        if direction.z < 0.12:
            continue
        origin = center + Vector((direction.x * radii.x, direction.y * radii.y, direction.z * radii.z)) * 2.2
        hit, normal, _, _ = crown_bvh.ray_cast(origin, -direction)
        if hit is None:
            continue
        _leaf_scale(bm, hit - normal * 0.012, normal, Vector((0, 0, -1)) + direction * 0.6, rng.uniform(*size_range),
                    rng.uniform(0.08, 0.16))
        placed += 1


def _paint_foliage(obj, center, radii, crown_end, foliage_end, accent_fn=None, bark_grooves=True, height=3.0):
    sun_dir = Vector((0.45, -0.55, 0.7)).normalized()

    def cdir(co):
        d = co - center
        return Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).normalized()

    def regions(co, n, i):
        if i >= foliage_end:
            if not bark_grooves:
                return (0.0, 0.0, 0.0, 0.0)
            angle = math.atan2(co.y, co.x)
            return (0.0, 0.0, 0.0, smoothstep(0.55, 0.95, math.sin(angle * 5.0 + co.z * 3.0)) * 0.6)
        c = cdir(co)
        sun = smoothstep(0.05, 0.85, c.dot(sun_dir)) * smoothstep(-0.25, 0.45, c.z)
        shade = smoothstep(-0.05, -0.75, c.z)
        flicker = 0.18 * (0.5 + 0.5 * wave_noise(co * 3.1, 77, octaves=1))
        accent = accent_fn(co, c, i) if accent_fn else 0.0
        if i >= crown_end:
            return (1.0, min(1.0, 0.35 + sun * 0.75 + flicker), shade * 0.5, accent)
        return (1.0, min(1.0, sun * 0.85 + flicker * 0.5), shade, accent)

    def occlusion(co, n, i):
        if i >= foliage_end:
            return (0.3 * (1.0 - smoothstep(0.0, 0.3, co.z)), min(1.0, co.z / height))
        depth = 1.0 - smoothstep(0.7, 1.0, (co - center).length / max(radii))
        return (0.25 * smoothstep(0.0, -0.8, cdir(co).z) + 0.3 * depth, min(1.0, co.z / height))

    write_layers(obj, regions, occlusion)

    me = obj.data
    normals = [v.normal.lerp(cdir(v.co), 0.8).normalized() if v.index < foliage_end else v.normal.copy() for v in me.vertices]
    me.normals_split_custom_set_from_vertices(normals)


BUSHES = {
    # name: masses, scale count, accent (blossom dots), crown triangles
    "A": ([(Vector((0, 0, 0.32)), 0.36), (Vector((0.26, 0.1, 0.26)), 0.25), (Vector((-0.24, -0.06, 0.25)), 0.26),
           (Vector((0.02, -0.2, 0.28)), 0.24), (Vector((0.0, 0.18, 0.44)), 0.22)], 22, False, 220),
    "B": ([(Vector((0, 0, 0.22)), 0.3), (Vector((0.38, 0.05, 0.18)), 0.24), (Vector((-0.38, -0.02, 0.19)), 0.25),
           (Vector((0.15, 0.22, 0.2)), 0.2), (Vector((-0.12, -0.24, 0.2)), 0.2)], 20, True, 220),
    "C": ([(Vector((0, 0, 0.35)), 0.26), (Vector((0, 0, 0.62)), 0.22), (Vector((0.08, 0.05, 0.85)), 0.15),
           (Vector((-0.1, -0.06, 0.5)), 0.2)], 18, False, 200),
}


def build_bush(kind, collections):
    from mathutils.bvhtree import BVHTree
    masses, scales, blossoms, crown_tris = BUSHES[kind]
    crown = _crown(masses, f"GL_Bush_{kind}", resolution=0.05, voxel=0.035)
    decimate_to(crown, crown_tris)
    bm = bmesh.new()
    bm.from_mesh(crown.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    # Sink the bush slightly into the ground: 4 cm, or more when its lowest mass sits high, so the lowest leaves always
    # end 3 cm under the ground (T34E: Bush C stood 12.6 cm above its pivot and floated on the meadow).
    lowest = min(v.co.z for v in bm.verts)
    sink = 0.04 if lowest - 0.04 <= 0.01 else lowest + 0.03
    for v in bm.verts:
        v.co.z -= sink
    crown_end = len(bm.verts)
    tree = BVHTree.FromBMesh(bm)
    zs = [v.co.z for v in bm.verts]
    center = Vector((0, 0, (min(zs) + max(zs)) * 0.5))
    radii = Vector((max(abs(v.co.x) for v in bm.verts), max(abs(v.co.y) for v in bm.verts), (max(zs) - min(zs)) * 0.5))
    _scales_on(bm, tree, center, radii, scales, (0.1, 0.15), 600 + ord(kind))
    foliage_end = len(bm.verts)
    bm.normal_update()
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    obj = object_from_bmesh(f"GL_Bush_{kind}", bm, collections)
    rng_field = 300 + ord(kind)

    def accent(co, c, i):
        if not blossoms or c.z < 0.0:
            return 0.0
        return 1.0 if wave_noise(co * 9.0, rng_field, octaves=1) > 0.62 else 0.0

    _paint_foliage(obj, center, radii, crown_end, foliage_end, accent_fn=accent, bark_grooves=False, height=max(zs))
    return obj


# Trees B (asymmetric canopy), C (slim birch-like), D (fruit tree with tiny apples).
TREES = {
    "B": dict(
        masses=[(Vector((0.3, 0.0, 1.95)), 0.8), (Vector((1.0, 0.1, 1.75)), 0.55), (Vector((-0.4, 0.2, 1.75)), 0.5),
                (Vector((0.5, 0.55, 1.7)), 0.5), (Vector((0.6, -0.55, 1.8)), 0.48), (Vector((0.2, 0.0, 2.45)), 0.5)],
        trunk_top=Vector((0.25, 0.0, 1.55)), trunk_radius=0.24, lean=0.25, center=Vector((0.35, 0.0, 1.95)),
        radii=Vector((1.2, 1.0, 0.75)), branches=[(Vector((0.95, 0.1, 1.7)), 0.08), (Vector((-0.35, 0.2, 1.7)), 0.07)],
        grooves=True),
    "C": dict(
        masses=[(Vector((0.0, 0.0, 2.2)), 0.55), (Vector((0.05, 0.05, 2.75)), 0.45), (Vector((0.0, 0.0, 1.7)), 0.5),
                (Vector((0.3, 0.1, 2.15)), 0.35), (Vector((-0.28, -0.1, 2.3)), 0.35), (Vector((0.0, 0.0, 3.15)), 0.28)],
        trunk_top=Vector((0.02, 0.0, 1.9)), trunk_radius=0.13, lean=0.04, center=Vector((0.0, 0.0, 2.35)),
        radii=Vector((0.65, 0.6, 1.05)), branches=[], grooves=True),
    "D": dict(
        masses=[(Vector((0.0, 0.0, 1.6)), 0.72), (Vector((0.65, 0.0, 1.45)), 0.5), (Vector((-0.65, 0.05, 1.45)), 0.5),
                (Vector((0.05, 0.6, 1.45)), 0.48), (Vector((0.0, -0.6, 1.45)), 0.48), (Vector((0.0, 0.0, 2.05)), 0.45)],
        trunk_top=Vector((0.0, 0.0, 1.25)), trunk_radius=0.2, lean=0.08, center=Vector((0.0, 0.0, 1.6)),
        radii=Vector((1.1, 1.05, 0.7)), branches=[(Vector((0.55, 0.0, 1.4)), 0.07), (Vector((-0.55, 0.05, 1.4)), 0.07)],
        grooves=False, fruit=14),
}
TREE_LODS_T33 = {0: (420, 46, 8, 5, True), 1: (240, 12, 6, 3, True), 2: (120, 0, 5, 2, False)}


def build_tree_t33(kind, collections, lod_collections):
    from mathutils.bvhtree import BVHTree
    spec = TREES[kind]
    crown = _crown(spec["masses"], f"GL_Tree_{kind}")
    top = spec["trunk_top"]
    results = []
    for lod, (crown_tris, scales, sides, rings, branches) in TREE_LODS_T33.items():
        temp = bpy.data.objects.new(f"WORK_GL_Tree_{kind}_LODTemp", crown.data.copy())
        bpy.data.collections["WORK"].objects.link(temp)
        decimate_to(temp, crown_tris)
        bm = bmesh.new()
        bm.from_mesh(temp.data)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        crown_end = len(bm.verts)
        tree = BVHTree.FromBMesh(bm)
        _scales_on(bm, tree, spec["center"], spec["radii"], scales, (0.2, 0.28), 700 + ord(kind))
        if spec.get("fruit") and lod < 2:
            rng = random.Random(900)
            count = spec["fruit"] if lod == 0 else spec["fruit"] // 2
            golden = math.pi * (3.0 - math.sqrt(5.0))
            placed = 0
            for i in range(count * 4):
                if placed >= count:
                    break
                z = 0.6 - 1.2 * (i + 0.5) / (count * 4)
                r = math.sqrt(max(0.0, 1.0 - z * z))
                a = i * golden * 1.7
                d = Vector((math.cos(a) * r, math.sin(a) * r, z))
                origin = spec["center"] + Vector((d.x * spec["radii"].x, d.y * spec["radii"].y, d.z * spec["radii"].z)) * 2.0
                hit, normal, _, _ = tree.ray_cast(origin, -d)
                if hit is None:
                    continue
                _bipyramid(bm, hit + normal * 0.03, 0.045, 0.085, 5, a)
                placed += 1
        foliage_end = len(bm.verts)
        points = []
        for i in range(rings + 1):
            t = i / rings
            points.append(Vector((spec["lean"] * t * t + top.x * t, 0.012 * math.sin(t * math.pi * 2.0), t * top.z)))
        r0 = spec["trunk_radius"]
        radii = [r0 - r0 * 0.35 * (i / rings) ** 0.7 for i in range(rings + 1)]
        _tube(bm, points, radii, sides, cap_top=True, flare=lambda k, a: 1.0 + 0.55 * max(0.0, math.cos(a * 2.0 + 0.4)) ** 3)
        if branches:
            for target, radius in spec["branches"]:
                start = top + (target - top) * 0.05
                mid = top + (target - top) * 0.55 + Vector((0, 0, 0.08))
                _tube(bm, [start, mid, target], [radius * 1.3, radius, radius * 0.6], max(4, sides - 2))
        bm.normal_update()
        data = temp.data
        bpy.data.objects.remove(temp)
        bpy.data.meshes.remove(data)
        obj = object_from_bmesh(f"GL_Tree_{kind}_LOD{lod}", bm, collections if lod == 0 else lod_collections)
        _paint_foliage(obj, spec["center"], spec["radii"], crown_end, foliage_end, bark_grooves=spec["grooves"],
                       height=spec["center"].z + spec["radii"].z)
        if spec.get("fruit"):
            _paint_fruit(obj, crown_end, foliage_end, spec)
        results.append(obj)
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    return results


def _paint_fruit(obj, crown_end, foliage_end, spec):
    """Apples are the small bipyramids above the crown surface: region A (palette column 4) on them."""
    me = obj.data
    attr = me.color_attributes["PW_Regions"]
    # Apples are 7-vertex bipyramids (isolated islands in the foliage range); leaf scales have 8 vertices.
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    seen = set()
    apples = []
    for v in bm.verts:
        if v.index < crown_end or v.index >= foliage_end or v.index in seen:
            continue
        stack, island = [v], []
        while stack:
            x = stack.pop()
            if x.index in seen:
                continue
            seen.add(x.index)
            island.append(x.index)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen:
                    stack.append(o)
        if len(island) == 7:
            apples.extend(island)
    bm.free()
    for i in apples:
        attr.data[i].color = (0.0, 0.0, 0.0, 1.0)


def build_rock_hero(kind, collections):
    sizes = {"A": (1.7, 1.05, 480, 141), "B": (1.2, 1.45, 420, 157)}
    width, height, target, seed = sizes[kind]
    ROCK_SIZES["H" + kind] = (width, height, target)
    obj = build_rock("H" + kind, collections, seed)
    obj.name = f"GL_RockHero_{kind}"
    obj.data.name = obj.name
    return obj


def _wood_regions(axis_end, cut_threshold=0.75):
    def regions(co, n, i):
        cut = 1.0 if abs(n.dot(axis_end)) > cut_threshold else 0.0
        angle = math.atan2(co.z, co.y)
        grooves = smoothstep(0.5, 0.95, math.sin(angle * 7.0 + co.x * 4.0)) * 0.6 * (1.0 - cut)
        moss = smoothstep(0.6, 0.95, n.z) * smoothstep(-0.2, 0.4, wave_noise(co * 3.0, 33)) * (1.0 - cut)
        return (cut, moss, 0.0, grooves)
    return regions


def build_log(collections):
    bm = bmesh.new()
    length, radius = 1.1, 0.16
    points = [Vector((-length * 0.5 + length * t, 0.0, radius * 0.9)) for t in (0.0, 0.33, 0.66, 1.0)]
    rings = _tube(bm, points, [radius, radius * 1.03, radius * 0.97, radius * 0.95], 8, cap_top=True)
    bm.faces.new(list(reversed(rings[0])))
    bm.normal_update()

    def occlusion(co, n, i):
        return (0.5 * smoothstep(radius * 0.5, 0.0, co.z), 0.0)

    obj = object_from_bmesh("GL_Log", bm, collections)
    mark_sharp_by_angle(obj, 45.0)
    write_layers(obj, _wood_regions(Vector((1, 0, 0))), occlusion)
    return obj


def build_stump(collections):
    bm = bmesh.new()
    points = [Vector((0, 0, 0.0)), Vector((0, 0, 0.18)), Vector((0.01, 0, 0.34))]
    _tube(bm, points, [0.3, 0.24, 0.23], 9, cap_top=True,
          flare=lambda k, a: 1.0 + 0.6 * max(0.0, math.cos(a * 3.0 + 0.7)) ** 3)
    bm.normal_update()

    def occlusion(co, n, i):
        return (0.5 * (1.0 - smoothstep(0.0, 0.15, co.z)), 0.0)

    obj = object_from_bmesh("GL_Stump", bm, collections)
    mark_sharp_by_angle(obj, 45.0)
    write_layers(obj, _wood_regions(Vector((0, 0, 1)), 0.9), occlusion)
    return obj


def _box(bm, center, size, rounded=0.0):
    temp = bmesh.new()
    bmesh.ops.create_cube(temp, size=1.0)
    mapping = {}
    for v in temp.verts:
        mapping[v] = bm.verts.new(center + Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z)))
    for f in temp.faces:
        bm.faces.new([mapping[v] for v in f.verts])
    temp.free()


def build_fence(collections):
    bm = bmesh.new()
    for x in (-0.55, 0.55):
        _box(bm, Vector((x, 0, 0.27)), Vector((0.09, 0.09, 0.56)))
    for z in (0.22, 0.42):
        _box(bm, Vector((0, 0, z)), Vector((1.24, 0.05, 0.07)))
    bm.normal_update()

    def regions(co, n, i):
        top = 1.0 if n.z > 0.9 else 0.0
        return (top * 0.6, smoothstep(0.5, 0.9, n.z) * 0.3, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.4 * (1.0 - smoothstep(0.0, 0.12, co.z)), 0.0)

    obj = object_from_bmesh("GL_Fence_A", bm, collections, smooth=False)
    write_layers(obj, regions, occlusion)
    return obj


def build_haybale(collections):
    bm = bmesh.new()
    radius, width = 0.34, 0.5
    points = [Vector((-width * 0.5, 0, radius)), Vector((-width * 0.45, 0, radius)), Vector((width * 0.45, 0, radius)), Vector((width * 0.5, 0, radius))]
    rings = _tube(bm, points, [radius * 0.88, radius, radius, radius * 0.88], 12, cap_top=True)
    bm.faces.new(list(reversed(rings[0])))
    bm.normal_update()

    def regions(co, n, i):
        side = abs(n.x) > 0.8
        swirl = smoothstep(0.3, 0.9, math.sin(math.hypot(co.y, co.z - radius) * 40.0)) if side else 0.0
        straw = smoothstep(0.3, 0.9, math.sin(math.atan2(co.z - radius, co.y) * 18.0 + co.x * 6.0)) * 0.5
        return (swirl, 0.0, straw if not side else 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.5 * (1.0 - smoothstep(0.0, 0.15, co.z)), 0.0)

    obj = object_from_bmesh("GL_HayBale", bm, collections)
    write_layers(obj, regions, occlusion)
    return obj


def build_birdbath(collections):
    bm = bmesh.new()
    _tube(bm, [Vector((0, 0, 0)), Vector((0, 0, 0.06)), Vector((0, 0, 0.32)), Vector((0, 0, 0.4))],
          [0.18, 0.15, 0.08, 0.12], 8, cap_top=False)
    # Basin: outer bowl, rim, inner water disc.
    rim_out = [bm.verts.new(Vector((math.cos(k / 10 * math.tau) * 0.3, math.sin(k / 10 * math.tau) * 0.3, 0.5))) for k in range(10)]
    bottom = [bm.verts.new(Vector((math.cos(k / 10 * math.tau) * 0.13, math.sin(k / 10 * math.tau) * 0.13, 0.4))) for k in range(10)]
    rim_in = [bm.verts.new(Vector((math.cos(k / 10 * math.tau) * 0.25, math.sin(k / 10 * math.tau) * 0.25, 0.48))) for k in range(10)]
    water_c = bm.verts.new(Vector((0, 0, 0.47)))
    for k in range(10):
        n = (k + 1) % 10
        bm.faces.new((bottom[k], bottom[n], rim_out[n], rim_out[k]))
        bm.faces.new((rim_out[k], rim_out[n], rim_in[n], rim_in[k]))
        bm.faces.new((rim_in[k], rim_in[n], water_c))
    bm.normal_update()

    def regions(co, n, i):
        water = 1.0 if co.z > 0.46 and math.hypot(co.x, co.y) < 0.2 and n.z > 0.9 else 0.0
        return (0.0, smoothstep(0.6, 0.95, n.z) * 0.4 * (1.0 - water), water, 0.0)

    def occlusion(co, n, i):
        return (0.4 * (1.0 - smoothstep(0.0, 0.1, co.z)), 0.0)

    obj = object_from_bmesh("GL_BirdBath", bm, collections)
    mark_sharp_by_angle(obj, 40.0)
    write_layers(obj, regions, occlusion)
    return obj


def build_props_kit():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    lods = [cols["LOD"], cols["EXPORT"]]
    out = [build_bush(k, target) for k in ("A", "B", "C")]
    for k in ("B", "C", "D"):
        out += build_tree_t33(k, target, lods)
    out += [build_rock_hero(k, target) for k in ("A", "B")]
    out += [build_log(target), build_stump(target), build_fence(target), build_haybale(target), build_birdbath(target)]
    return {o.name: triangle_count(o) for o in out}


# ---------------- ambient life (SH_AmbientLife: wings flap in the shader) ----------------
# uv.y = flap weight (0 on the body, 1 at the wing tip). Vertex color A = side (+1 right wing, 0 left) is
# not needed: the shader flaps around the body's local X axis using the vertex's own Y sign.
def build_butterfly(collections):
    bm = bmesh.new()
    _lump(bm, Vector((0, 0, 0)), Vector((0.006, 0.03, 0.006)), 11, subdivisions=1)
    for sign in (-1.0, 1.0):
        fore = [Vector((sign * 0.004, 0.012, 0)), Vector((sign * 0.05, 0.035, 0.004)), Vector((sign * 0.055, 0.0, 0.002)),
                Vector((sign * 0.006, -0.004, 0))]
        hind = [Vector((sign * 0.004, -0.004, 0)), Vector((sign * 0.04, -0.012, 0.002)), Vector((sign * 0.03, -0.04, 0.0)),
                Vector((sign * 0.004, -0.018, 0))]
        _faces_two_sided(bm, fore)
        _faces_two_sided(bm, hind)

    def regions(co, n, i):
        wing = 1.0 if abs(co.x) > 0.008 else 0.0
        marks = 1.0 if wing and math.hypot(abs(co.x) - 0.035, co.y - 0.012) < 0.009 else 0.0
        return (wing, marks, 1.0 - wing, 0.0)

    def occlusion(co, n, i):
        # uv.y = wing weight: SH_AmbientLife hinges these vertices at the body (x = 0.004).
        return (0.0, 1.0 if abs(co.x) > 0.0045 else 0.0)

    return _finish("GL_Life_Butterfly", bm, collections, regions, occlusion, smooth=False)


def build_bee(collections):
    bm = bmesh.new()
    _lump(bm, Vector((0, 0, 0)), Vector((0.012, 0.02, 0.012)), 21, subdivisions=1)
    for sign in (-1.0, 1.0):
        _faces_two_sided(bm, [Vector((sign * 0.006, 0.004, 0.009)), Vector((sign * 0.028, 0.01, 0.014)),
                              Vector((sign * 0.026, -0.006, 0.012)), Vector((sign * 0.006, -0.004, 0.009))])

    def regions(co, n, i):
        # Palette order is base, R, G, B: the body (G) comes before the stripes (B) so the stripes stay visible.
        wing = 1.0 if abs(co.x) > 0.008 and co.z > 0.008 else 0.0
        stripe = 1.0 if (not wing) and int((co.y + 0.03) * 150) % 2 == 0 else 0.0
        return (wing, 1.0 - wing, stripe, 0.0)

    def occlusion(co, n, i):
        # uv.y = wing weight, hinge at x = 0.006, z = 0.009.
        return (0.0, 1.0 if abs(co.x) > 0.0065 and co.z > 0.008 else 0.0)

    return _finish("GL_Life_Bee", bm, collections, regions, occlusion, smooth=False)


def build_bird(collections):
    """Small round songbird sitting on the grass (hops, pecks and flights are animated by AmbientLife).
    Two flat wings hinge at the shoulders (x = 0.034, z = 0.082): SH_AmbientLife folds them inside the body
    on the ground and flaps them in flight. uv.y = 1 marks the wing vertices."""
    bm = bmesh.new()
    _lump(bm, Vector((0, 0, 0.055)), Vector((0.045, 0.06, 0.045)), 31, subdivisions=2)
    _lump(bm, Vector((0, 0.05, 0.095)), Vector((0.032, 0.032, 0.03)), 32, subdivisions=1)
    _bipyramid(bm, Vector((0, 0.088, 0.095)), 0.008, 0.02, 3)
    _faces_two_sided(bm, [Vector((-0.012, -0.05, 0.07)), Vector((0.012, -0.05, 0.07)), Vector((0.0, -0.1, 0.09))])
    bm.verts.ensure_lookup_table()
    wing_start = len(bm.verts)
    for sign in (-1.0, 1.0):
        _faces_two_sided(bm, [Vector((sign * 0.034, 0.026, 0.082)), Vector((sign * 0.078, 0.022, 0.084)),
                              Vector((sign * 0.114, -0.01, 0.083)), Vector((sign * 0.1, -0.05, 0.081)),
                              Vector((sign * 0.034, -0.04, 0.08))])

    def regions(co, n, i):
        beak = 1.0 if co.y > 0.078 else 0.0
        if i >= wing_start:
            return (0.0, 1.0, 0.0, 0.0)
        breast = 1.0 if co.y > 0.0 and co.z < 0.08 and n.y > 0.2 and not beak else 0.0
        wing = 1.0 if abs(co.x) > 0.035 and co.z > 0.04 else 0.0
        return (breast, wing, beak, 0.0)

    def occlusion(co, n, i):
        if i >= wing_start:
            return (0.0, 1.0)
        return (0.4 * (1.0 - smoothstep(0.0, 0.04, co.z)), 0.0)

    return _finish("GL_Life_Bird", bm, collections, regions, occlusion)


def build_life_kit():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    out = [build_butterfly(target), build_bee(target), build_bird(target)]
    return {o.name: triangle_count(o) for o in out}
