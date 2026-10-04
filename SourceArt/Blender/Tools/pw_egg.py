"""Egg family source generator (T13). Rebuilds BL_Egg_Family.blend content from parameters.

One parametric egg surface drives everything, so every piece agrees:
  - SM_Egg_Base_LOD0 / LOD1: lathe of the egg profile. UV0 = (u, v) = ((phi - PHI0) / 2pi, theta / pi).
  - SM_Egg_Fragment_A/B/C: shell pieces cut along the crack line, solidified inward.
  - T_EggMask_Common.png: shared mask texture evaluated on the same surface (no baking):
      R spots, G leaf band, B tiny flowers, A cracks (1.0 = stage 1 lines, 0.6 = stage 2 lines).
    The crack lines are the fragment edges, so the egg breaks exactly where it cracked.
Vertex colors PW_Regions: G = 1 on the inner shell of fragments (inner shell color), else 0.
Blender: +Z up, front = -Y (same as animals). Pivot at the bottom of the egg.
"""
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Vector

# ---------------- Shape ----------------
HEIGHT = 0.42
RADIUS = 0.155
BOTTOM_BULGE = 0.12          # > 0: wider below the middle, like a real egg
PHI0 = math.pi / 2           # UV seam on the back (+Y), away from the camera

LOD_SEGMENTS = {0: 16, 1: 10}
LOD_BANDS = {0: 12, 1: 7}

SHELL_THICKNESS = 0.010

# Crack ring: zigzag around the egg slightly above the middle (theta measured from the bottom pole).
CRACK_POINTS = 12
CRACK_THETA = 0.56 * math.pi
CRACK_JAG = 0.05 * math.pi
SPLIT_COLUMNS = (3, 9)       # meridians (crack indices) where the lower shell splits in two (-X and +X)
SPLIT_THETA = 0.30 * math.pi
SPLIT_JAG = 0.09             # radians of phi at the side crack midpoint
CAP_RING_THETA = 0.80 * math.pi

MASK_SIZE = 256

# Stage 1 crack: front arc of the ring (indices) plus a short branch below the front point.
STAGE1_RING = (4, 5, 6, 7, 8)
STAGE1_BRANCH = ((6, 0.0, 0.0), (6, -0.085 * math.pi, 0.07))


def profile(theta):
    """Radius and height of the egg at polar angle theta (0 bottom, pi top)."""
    r = RADIUS * math.sin(theta) * (1.0 + BOTTOM_BULGE * math.cos(theta))
    z = HEIGHT * 0.5 * (1.0 - math.cos(theta))
    return r, z


def surface(theta, phi):
    r, z = profile(theta)
    return Vector((r * math.cos(phi), r * math.sin(phi), z))


def ring_phi(index, count):
    return PHI0 + 2.0 * math.pi * index / count


def crack_ring():
    """[(theta, phi)] of the zigzag crack, one point per crack index."""
    points = []
    for k in range(CRACK_POINTS):
        jag = CRACK_JAG if k % 2 == 0 else -CRACK_JAG
        points.append((CRACK_THETA + jag, ring_phi(k, CRACK_POINTS)))
    return points


def split_mid(k):
    """Jagged midpoint of the vertical side crack under crack index k."""
    phi = ring_phi(k, CRACK_POINTS) + (SPLIT_JAG if k == SPLIT_COLUMNS[0] else -SPLIT_JAG)
    return SPLIT_THETA, phi


# ---------------- Mesh helpers ----------------
def _new_object(name, bm, collections):
    me = bpy.data.meshes.get(name)
    if me is None:
        me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, me)
    obj.data = me
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    for c in collections:
        c.objects.link(obj)
    for poly in me.polygons:
        poly.use_smooth = True
    return obj


def _face(bm, uv, corners):
    """corners: [(BMVert, (u, v))] in any winding; normals are fixed afterwards."""
    face = bm.faces.new([c[0] for c in corners])
    for loop, (_, coord) in zip(face.loops, corners):
        loop[uv].uv = coord
    return face


def _orient_outward(bm, center=Vector((0.0, 0.0, HEIGHT * 0.5))):
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - center) < 0.0:
            f.normal_flip()


def build_shell(lod, collections):
    segments, bands = LOD_SEGMENTS[lod], LOD_BANDS[lod]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    bottom = bm.verts.new(surface(0.0, 0.0))
    top = bm.verts.new(surface(math.pi, 0.0))
    rings = []
    for i in range(1, bands):
        theta = math.pi * i / bands
        rings.append([bm.verts.new(surface(theta, ring_phi(j, segments))) for j in range(segments)])

    def v_of(i):
        return i / bands

    for j in range(segments):
        u0, u1 = j / segments, (j + 1) / segments
        jn = (j + 1) % segments
        _face(bm, uv, [(bottom, ((u0 + u1) * 0.5, 0.0)), (rings[0][jn], (u1, v_of(1))), (rings[0][j], (u0, v_of(1)))])
        for i in range(bands - 2):
            _face(bm, uv, [(rings[i][j], (u0, v_of(i + 1))), (rings[i][jn], (u1, v_of(i + 1))),
                           (rings[i + 1][jn], (u1, v_of(i + 2))), (rings[i + 1][j], (u0, v_of(i + 2)))])
        _face(bm, uv, [(rings[-1][j], (u0, v_of(bands - 1))), (rings[-1][jn], (u1, v_of(bands - 1))), (top, ((u0 + u1) * 0.5, 1.0))])

    bm.normal_update()
    _orient_outward(bm)
    obj = _new_object(f"SM_Egg_Base_LOD{lod}", bm, collections)
    _set_regions(obj, inner=False)
    return obj


def _uv(theta, phi):
    u = (phi - PHI0) / (2.0 * math.pi)
    return u, theta / math.pi


def _patch(name, grid, collections, poles=()):
    """grid: rows of (theta, phi); consecutive rows are joined by quads. poles: (row_index, theta) fans."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = [[bm.verts.new(surface(t, p)) for (t, p) in row] for row in grid]
    for r in range(len(grid) - 1):
        for c in range(len(grid[r]) - 1):
            a, b = grid[r][c], grid[r][c + 1]
            d, e = grid[r + 1][c], grid[r + 1][c + 1]
            _face(bm, uv, [(verts[r][c], _uv(*a)), (verts[r][c + 1], _uv(*b)),
                           (verts[r + 1][c + 1], _uv(*e)), (verts[r + 1][c], _uv(*d))])
    for row_index, pole_theta in poles:
        row = grid[row_index]
        pole = bm.verts.new(surface(pole_theta, 0.0))
        for c in range(len(row) - 1):
            a, b = row[c], row[c + 1]
            mid_phi = (a[1] + b[1]) * 0.5
            _face(bm, uv, [(verts[row_index][c], _uv(*a)), (verts[row_index][c + 1], _uv(*b)), (pole, _uv(pole_theta, mid_phi))])
    # Weld the seam column of closed rings (UVs stay per loop, so u = 0 and u = 1 both survive).
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.normal_update()
    _orient_outward(bm)
    obj = _new_object(name, bm, collections)
    _solidify(obj)
    _set_regions(obj, inner=True)
    return obj


def _solidify(obj):
    mod = obj.modifiers.new("Solidify", 'SOLIDIFY')
    mod.thickness = SHELL_THICKNESS
    mod.offset = -1.0
    mod.use_rim = True
    mod.use_even_offset = True
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    solid = bpy.data.meshes.new_from_object(evaluated)
    old = obj.data
    obj.modifiers.remove(mod)
    obj.data = solid
    name = old.name
    bpy.data.meshes.remove(old)
    solid.name = name
    for poly in solid.polygons:
        poly.use_smooth = True


def _set_regions(obj, inner):
    me = obj.data
    attr = me.color_attributes.get("PW_Regions") or me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    if inner:
        for v in me.vertices:
            attr.data[v.index].color = (0.0, 0.0 if _is_outer(v.co) else 1.0, 0.0, 0.0)
    else:
        for i in range(len(me.vertices)):
            attr.data[i].color = (0.0, 0.0, 0.0, 0.0)
    me.color_attributes.active_color = attr


def _theta_of(co):
    # Invert z = H/2 (1 - cos theta).
    c = 1.0 - 2.0 * co.z / HEIGHT
    return math.acos(max(-1.0, min(1.0, c)))


def _is_outer(co):
    """True when the point lies on the egg surface (within a third of the shell thickness)."""
    best = 1e9
    theta0 = _theta_of(co)
    for dt in (-0.06, -0.03, 0.0, 0.03, 0.06):
        t = min(max(theta0 + dt, 0.0), math.pi)
        r, z = profile(t)
        best = min(best, math.hypot(math.hypot(co.x, co.y) - r, co.z - z))
    return best < SHELL_THICKNESS * 0.35


def build_fragments(collections):
    ring = crack_ring()
    n = CRACK_POINTS
    # A: top cap, from the crack ring up to the top pole. Columns wrap with a seam column.
    cap_ring = [(CAP_RING_THETA, ring_phi(k, n)) for k in range(n + 1)]
    crack_row = [ring[k % n] if k < n else (ring[0][0], ring[0][1] + 2.0 * math.pi) for k in range(n + 1)]
    a = _patch("SM_Egg_Fragment_A", [crack_row, cap_ring], collections, poles=((1, math.pi),))

    # B (front, -Y) and C (back, +Y): lower shell split on the -X / +X meridians.
    left, right = SPLIT_COLUMNS

    def lower_half(k_from, k_to):
        top_row, mid_row = [], []
        for k in range(k_from, k_to + 1):
            theta, phi = ring[k % n]
            phi = ring_phi(k, n)  # unwrapped phi, continuous across the seam
            top_row.append((theta, phi))
            if k in (k_from, k_to):
                mid_theta, mid_phi = split_mid(k % n)
                mid_phi += ring_phi(k, n) - ring_phi(k % n, n)
                mid_row.append((mid_theta, mid_phi))
            else:
                mid_row.append((SPLIT_THETA, phi))
        return [top_row, mid_row]

    b = _patch("SM_Egg_Fragment_B", lower_half(left, right), collections, poles=((1, 0.0),))
    c = _patch("SM_Egg_Fragment_C", lower_half(right, left + n), collections, poles=((1, 0.0),))
    return a, b, c


# ---------------- Mask texture ----------------
SPOTS = [  # (theta, phi offset from the front, radius m)
    (0.22, 0.10, 0.030), (0.40, -0.55, 0.026), (0.36, 0.85, 0.024), (0.70, 0.25, 0.022),
    (0.62, -1.20, 0.028), (0.84, -0.40, 0.020), (0.30, 2.10, 0.027), (0.55, 2.80, 0.025),
    (0.75, 1.70, 0.022), (0.45, -2.40, 0.026), (0.88, 2.60, 0.019), (0.20, -1.70, 0.024),
]
FLOWERS = [  # (theta, phi offset from the front, radius m)
    (0.47, -0.25, 0.024), (0.30, 1.30, 0.021), (0.66, 1.05, 0.020), (0.38, -1.55, 0.022),
    (0.72, -0.85, 0.019), (0.28, 2.60, 0.021), (0.62, 2.30, 0.020), (0.50, -2.85, 0.022),
]
LEAF_BAND_THETA = 0.66 * math.pi
LEAF_COUNT = 12
FRONT_PHI = PHI0 + math.pi   # -Y


def _surface_np(theta, phi):
    r = RADIUS * np.sin(theta) * (1.0 + BOTTOM_BULGE * np.cos(theta))
    z = HEIGHT * 0.5 * (1.0 - np.cos(theta))
    return np.stack([r * np.cos(phi), r * np.sin(phi), z], axis=-1)


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def build_mask(size=MASK_SIZE):
    """Returns float32 RGBA array [size, size, 4], row 0 = v 0 (bottom), like Blender image pixels."""
    v = (np.arange(size) + 0.5) / size
    u = (np.arange(size) + 0.5) / size
    uu, vv = np.meshgrid(u, v)
    theta = vv * math.pi
    phi = PHI0 + uu * 2.0 * math.pi
    pos = _surface_np(theta, phi)

    spots = np.zeros_like(theta)
    for t, p, rad in SPOTS:
        c = _surface_np(np.array(t * math.pi), np.array(FRONT_PHI + p))
        d = np.linalg.norm(pos - c, axis=-1)
        spots = np.maximum(spots, _smoothstep(rad, rad * 0.78, d))

    # Leaf band: a thin wavy vine with pointed leaves alternating above and below it.
    rel = (phi - FRONT_PHI) % (2.0 * math.pi)
    wave = LEAF_BAND_THETA + 0.015 * math.pi * np.sin(rel * LEAF_COUNT * 0.5)
    arc = (theta - wave) * HEIGHT * 0.5 * 1.15          # approx meters along the meridian
    vine = _smoothstep(0.0040, 0.0024, np.abs(arc))
    leaves = np.zeros_like(theta)
    circumference = 2.0 * math.pi * RADIUS
    for i in range(LEAF_COUNT):
        side = 1.0 if i % 2 == 0 else -1.0
        center_rel = (i + 0.5) * 2.0 * math.pi / LEAF_COUNT
        # Local leaf frame in meters: x along the egg circumference, y along the meridian.
        dx = ((rel - center_rel + math.pi) % (2.0 * math.pi) - math.pi) * circumference / (2.0 * math.pi)
        dy = arc - side * 0.012
        tilt = math.radians(35.0) * side
        x = dx * math.cos(tilt) + dy * math.sin(tilt)
        y = -dx * math.sin(tilt) + dy * math.cos(tilt)
        half_length, half_width = 0.026, 0.011
        t = np.clip(1.0 - (x / half_length) ** 2, 0.0, 1.0)
        inside = _smoothstep(0.0012, -0.0006, np.abs(y) - half_width * t) * (np.abs(x) < half_length)
        leaves = np.maximum(leaves, inside)
    band = np.maximum(vine, leaves)

    flowers = np.zeros_like(theta)
    for t, p, rad in FLOWERS:
        tc, pc = t * math.pi, FRONT_PHI + p
        c = _surface_np(np.array(tc), np.array(pc))
        e_phi = _surface_np(np.array(tc), np.array(pc + 1e-3)) - c
        e_theta = _surface_np(np.array(tc + 1e-3), np.array(pc)) - c
        e_phi /= np.linalg.norm(e_phi)
        e_theta /= np.linalg.norm(e_theta)
        d = pos - c
        x = d @ e_phi
        y = d @ e_theta
        rho = np.hypot(x, y)
        alpha = np.arctan2(y, x)
        petal = rad * (0.5 + 0.5 * np.abs(np.cos(alpha * 2.5)))
        flowers = np.maximum(flowers, _smoothstep(petal, petal * 0.85, rho))

    cracks = np.zeros_like(theta)
    ring = crack_ring()
    n = CRACK_POINTS

    def draw(points, value, half_width=0.0042):
        nonlocal cracks
        for (t0, p0), (t1, p1) in zip(points[:-1], points[1:]):
            samples = 24
            ts = np.linspace(t0, t1, samples)
            ps = np.linspace(p0, p1, samples)
            line = _surface_np(ts, ps)
            d = np.full(theta.shape, 1e9)
            for q in line:
                d = np.minimum(d, np.linalg.norm(pos - q, axis=-1))
            cracks = np.maximum(cracks, value * _smoothstep(half_width, half_width * 0.45, d))

    def unwrapped(k):
        t, _ = ring[k % n]
        return t, ring_phi(k, n)

    stage1 = [unwrapped(k) for k in STAGE1_RING]
    k0, dt, dp = STAGE1_BRANCH[1]
    t6, p6 = unwrapped(k0)
    branch = [(t6, p6), (t6 + dt * 0.55, p6 + dp * 0.4), (t6 + dt, p6 + dp)]
    stage2_ring = [unwrapped(k) for k in range(STAGE1_RING[-1], STAGE1_RING[0] + n + 1)]
    left, right = SPLIT_COLUMNS
    side_cracks = []
    for k in (left, right):
        mt, mp = split_mid(k)
        side_cracks.append([unwrapped(k), (mt, mp), (0.12 * math.pi, mp)])

    draw(stage2_ring, 0.6)
    for path in side_cracks:
        draw(path, 0.6)
    draw(stage1, 1.0)
    draw(branch, 1.0)

    return np.stack([spots, band, flowers, cracks], axis=-1).astype(np.float32)


def save_mask(path, size=MASK_SIZE):
    data = build_mask(size)
    name = "T_EggMask_Common"
    image = bpy.data.images.get(name)
    if image is None or image.size[0] != size:
        if image is not None:
            bpy.data.images.remove(image)
        image = bpy.data.images.new(name, size, size, alpha=True, float_buffer=False)
    image.colorspace_settings.name = 'Non-Color'
    image.alpha_mode = 'CHANNEL_PACKED'
    image.pixels.foreach_set(data.ravel())
    image.filepath_raw = path
    image.file_format = 'PNG'
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save()
    return image, data


# ---------------- Scene ----------------
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


def build_all():
    cols = ensure_collections()
    lod0 = build_shell(0, [cols["GEO"], cols["EXPORT"]])
    lod1 = build_shell(1, [cols["LOD"], cols["EXPORT"]])
    frags = build_fragments([cols["GEO"], cols["EXPORT"]])
    return [lod0, lod1, *frags]


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ---------------- Review preview (WORK only, never exported) ----------------
PREVIEW_PALETTES = {
    # name: (shell, spots R, band G, flowers B, crack, inner, strengths (R, G, B))
    "Basic": ("F4EBD9", "A8D08D", "7DB563", "F2A7BA", "6B4A3A", "FFF9EE", (1.0, 0.0, 1.0)),
    "Focus": ("EEF1D8", "E3C46A", "6FAE58", "FFFFFF", "6B4A3A", "FFF9EE", (0.55, 1.0, 0.0)),
}


def _hex(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def preview_image(name, mask, palette, crack_stage):
    shell, spots, band, flowers, crack, _, strength = palette
    color = np.broadcast_to(_hex(shell), mask.shape[:2] + (3,)).copy()
    for channel, hex_color, k in ((0, spots, strength[0]), (1, band, strength[1]), (2, flowers, strength[2])):
        t = (mask[..., channel] * k)[..., None]
        color = color * (1.0 - t) + _hex(hex_color) * t
    if crack_stage > 0:
        threshold = 0.75 if crack_stage == 1 else 0.35
        t = _smoothstep(threshold - 0.05, threshold + 0.05, mask[..., 3])[..., None]
        color = color * (1.0 - t) + _hex(crack) * t
    rgba = np.concatenate([color, np.ones(mask.shape[:2] + (1,), np.float32)], axis=-1)
    size = mask.shape[0]
    image = bpy.data.images.get(name) or bpy.data.images.new(name, size, size, alpha=False)
    image.colorspace_settings.name = 'sRGB'
    image.pixels.foreach_set(rgba.ravel())
    image.pack()
    return image


def preview_material(name, image, inner_hex):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = image
    attr = nodes.new('ShaderNodeAttribute')
    attr.attribute_name = "PW_Regions"
    sep = nodes.new('ShaderNodeSeparateColor')
    mix = nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    inner = _hex(inner_hex)
    mix.inputs['B'].default_value = (float(inner[0]) ** 2.2, float(inner[1]) ** 2.2, float(inner[2]) ** 2.2, 1.0)
    links.new(attr.outputs['Color'], sep.inputs['Color'])
    links.new(sep.outputs[1], mix.inputs['Factor'])
    links.new(tex.outputs['Color'], mix.inputs['A'])
    links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.65
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return mat
