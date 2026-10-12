"""Grassland Magnifique part 2 (T33B): the strip of eight chunks, stream and pond water, landmarks, last props.

Same data contract as pw_grassland.py (PW_Regions color attribute, PW_Occlusion uv). The FBX export maps Blender
(x, y, z) to Unity (-x, z, -y): Unity east (+X, along the strip) is Blender -X, the gameplay camera side (Unity -Z) is
Blender +Y. Features below are written in Unity chunk coordinates (x east, z away from the camera) and converted
with to_blender(); world-space patterns use the Unity world x (origin - Blender x) so they continue across seams.

Usage inside Blender, after pw_grassland.py and pw_grassland_t33.py in the same namespace:
    ns["build_grounds_t33b"]()     # GL_Ground_Core_B (rejoined) + GL_Ground_<Chunk> for the six new chunks
                                   # (T50: then pw_forest_ground.build_forest_edge_t50() for GL_Ground_ForestEdge)
    ns["build_water_t33b"]()       # GL_Stream_A (stream + waterfall), GL_Stream_B (pond)
    ns["build_landmarks_t33b"]()   # GL_Landmark_OldTree_LOD0-2, GL_Landmark_Windmill_LOD0-2 + _Sails_LOD0-2
    ns["build_props_t33b"]()       # GL_Fence_B, GL_WellTiny, GL_Edge_A/B, GL_StreamStones

Water vertex data (SH_WaterLite): color R foam, G depth (0 at the banks, 1 in the middle), A falling water;
uv x across the flow (0..1), uv y along the flow in meters (ripples scroll along it).
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

CHUNK = 14.0

# ---------------- the strip ----------------

# name: (Unity origin x, Blender joins, seed, rings, segments, contour power, features)
# Blender "-X" is the Unity east side, "+X" the Unity west side. Core_A and Core_B keep their T15/T27 seed, resolution,
# power and joins: their shared seam stays vertex-identical (only the colors are rebuilt).
STRIP = {
    "GL_Ground_Core_A": (0.0, ("-X",), 41, 10, 56, 4.0, {"clearing": True}),
    "GL_Ground_Core_B": (14.0, ("-X", "+X"), 97, 10, 56, 5.0, {}),
    "GL_Ground_StreamBend": (28.0, ("-X", "+X"), 61, 24, 128, 4.0, {"stream": True}),
    "GL_Ground_Orchard": (42.0, ("-X", "+X"), 63, 16, 96, 4.0, {}),
    "GL_Ground_Hill": (56.0, ("-X", "+X"), 67, 18, 104, 4.0, {"hill": ((0.4, 2.4), 4.8, 1.25)}),
    "GL_Ground_WindmillRise": (70.0, ("-X", "+X"), 71, 18, 104, 4.0, {"hill": ((0.9, 2.6), 4.4, 0.7)}),
    "GL_Ground_PondEdge": (84.0, ("-X", "+X"), 73, 24, 128, 4.0, {"pond": True}),
    # T50: the Forest continues east of ForestEdge, joined at x 105 (FO_Ground_Entry, pw_forest_ground.py). Only the
    # east half changed (square contour, flat seam); pw_forest_ground.build_forest_edge_t50 repaints its east third.
    "GL_Ground_ForestEdge": (98.0, ("-X", "+X"), 79, 16, 96, 4.0, {"forest": True}),
}

def to_blender(x, z):
    """Unity chunk coordinates (x east, z away from the camera) to Blender (x, y)."""
    return (-x, -z)


# Stream of the StreamBend chunk, Unity coordinates: a spring on the far side, a bend through the meadow, a small
# waterfall over the near rim of the island (toward the camera).
STREAM_POINTS = [to_blender(x, z) for x, z in
                 [(-2.1, 6.2), (-1.5, 4.3), (0.5, 2.4), (1.5, 0.0), (0.5, -2.6), (1.0, -5.0), (1.35, -7.0)]]
STREAM_HALF = 0.62
POND_CENTER = to_blender(0.3, 2.3)
POND_RADII = (2.7, 1.95)
POND_LEVEL = -0.07


def path_y(wx):
    """The worn path running east along the strip (world x -> Blender y)."""
    return 0.85 * math.sin(wx * 0.19 + 0.6) + 0.35 * math.sin(wx * 0.51 + 1.9) - 0.35


def catmull(points, step=0.2):
    pts = [Vector((p[0], p[1], 0.0)) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-2].copy())
    return out


_STREAM = None


def stream_curve():
    global _STREAM
    if _STREAM is None:
        pts = catmull(STREAM_POINTS, 0.15)
        s = [0.0]
        for i in range(1, len(pts)):
            s.append(s[-1] + (pts[i] - pts[i - 1]).length)
        _STREAM = (pts, s)
    return _STREAM


def stream_nearest(x, y):
    """Distance to the stream axis and arc length there."""
    pts, s = stream_curve()
    p = Vector((x, y, 0.0))
    best, best_s = 1e9, 0.0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
        d = (a + ab * t - p).length
        if d < best:
            best, best_s = d, s[i] + (s[i + 1] - s[i]) * t
    return best, best_s


def stream_level(s):
    """Water surface height along the stream: a gentle fall from the spring to the rim."""
    total = stream_curve()[1][-1]
    return 0.0 - 0.08 * (s / total)


def stream_half(s):
    total = stream_curve()[1][-1]
    return STREAM_HALF * (0.6 + 0.4 * smoothstep(0.0, 2.5, s)) * (1.0 + 0.12 * math.sin(s * 1.7))


def pond_e(x, y):
    return math.hypot((x - POND_CENTER[0]) / POND_RADII[0], (y - POND_CENTER[1]) / POND_RADII[1])


def bump(x, y, center, radius, height):
    d = math.hypot(x - center[0], y - center[1])
    if d >= radius:
        return 0.0
    k = 0.5 + 0.5 * math.cos(math.pi * d / radius)
    return height * k * k * (3.0 - 2.0 * k)


# ---------------- grounds ----------------

def build_ground_t33b(name, collections):
    origin_x, joins, seed, rings, segments, power, features = STRIP[name]
    bm = bmesh.new()
    center = bm.verts.new((0.0, 0.0, 0.0))
    radii = [GROUND_HALF * (i / rings) ** 0.9 for i in range(1, rings + 1)]

    def height(x, y, ri):
        dist = math.hypot(x, y)
        join = _join_weight(x, joins)
        rolls = smoothstep(GROUND_FLAT_RADIUS * 0.85, GROUND_FLAT_RADIUS * 1.25, dist) * (1.0 - join)
        z = rolls * 0.22 * (0.5 + 0.5 * wave_noise(Vector((x * 0.25, y * 0.25, 0.0)), seed))
        if ri == rings - 1:
            z -= 0.06 * (1.0 - join)
        if "hill" in features:
            c, r, h = features["hill"]
            z += bump(x, y, to_blender(*c), r, h)
        if features.get("stream"):
            d, s = stream_nearest(x, y)
            half = stream_half(s)
            w = 1.0 - smoothstep(half * 0.55, half * 1.25, d)
            w *= smoothstep(-0.2, 0.5, s)
            target = stream_level(s) - 0.16 * max(0.0, 1.0 - (d / half) ** 2) - 0.03
            if ri == rings - 1:
                target = max(target, -0.12)
            z = z + (min(z, target) - z) * w
        if features.get("pond"):
            e = pond_e(x, y)
            w = 1.0 - smoothstep(0.72, 1.12, e)
            target = POND_LEVEL - 0.28 * max(0.0, 1.0 - e * e) - 0.03
            z = z + (min(z, target) - z) * w
        return z

    grid = []
    for ri, r in enumerate(radii):
        ring = []
        for j in range(segments):
            theta = j / segments * math.tau
            sx, sy = _contour(theta, power, joins)
            x, y = sx * r, sy * r
            ring.append(bm.verts.new((x, y, height(x, y, ri))))
        grid.append(ring)
    for j in range(segments):
        n = (j + 1) % segments
        bm.faces.new((center, grid[0][j], grid[0][n]))
    for ri in range(rings - 1):
        a, b = grid[ri], grid[ri + 1]
        for j in range(segments):
            n = (j + 1) % segments
            bm.faces.new((a[j], b[j], b[n], a[n]))
    previous = grid[-1]
    for depth, inset in ((0.18, 0.08), (0.55, 0.16), (GROUND_DEPTH, 0.3)):
        ring = []
        for j in range(segments):
            theta = j / segments * math.tau
            sx, sy = _contour(theta, power, joins)
            keep = _join_weight(sx * GROUND_HALF, joins)
            r = GROUND_HALF - inset
            ring.append(bm.verts.new((sx * (GROUND_HALF - inset * (1.0 - keep)), sy * r, -depth)))
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
    obj = object_from_bmesh(name, bm, collections)
    mark_sharp_by_angle(obj, 50.0)

    def wet(x, y):
        """1 in the stream bed and the pond basin, fading up the banks."""
        value = 0.0
        if features.get("stream"):
            d, s = stream_nearest(x, y)
            value = max(value, (1.0 - smoothstep(stream_half(s) * 0.7, stream_half(s) * 1.35, d)) * smoothstep(-0.2, 0.4, s))
        if features.get("pond"):
            value = max(value, 1.0 - smoothstep(0.85, 1.25, pond_e(x, y)))
        return value

    def regions(co, n, i):
        if co.z < -0.12 and wet(co.x, co.y) < 0.5:
            return (0.0, 0.0, 0.0, 1.0)  # soil wall
        # Unity world x and z: the patches and the path run continuously from one chunk to the next.
        wx, wz = origin_x - co.x, -co.y
        light = smoothstep(0.25, 0.75, wave_noise(Vector((wx * 0.35, wz * 0.35, 1.0)), PATCH_SEED + 1))
        dark = smoothstep(0.35, 0.85, wave_noise(Vector((wx * 0.3, wz * 0.3, 2.0)), PATCH_SEED + 2))
        if features.get("forest"):
            dark = min(1.0, dark * 0.6 + 0.45 * smoothstep(-2.0, 5.0, -co.x))
        # The worn path starts past the egg clearing and continues from chunk to chunk into the Forest (T50: it used to
        # fade out past x 100, where the island ended).
        worn = 0.62 * (1.0 - smoothstep(0.32, 0.8, abs(wz + path_y(wx))))
        worn *= smoothstep(3.0, 6.0, wx)
        if features.get("clearing"):
            # The start island keeps its soft worn clearing where the egg waits (T15 strength).
            worn = max(worn, 0.5 * (1.0 - smoothstep(0.7, 1.35, math.hypot(co.x, co.y + 0.6))))
        w = wet(co.x, co.y)
        worn = max(worn * (1.0 - w), w * 0.45)
        soil = max(smoothstep(-0.02, -0.12, co.z) * (1.0 - w), w * 0.55)
        return (light * 0.85 * (1.0 - w), dark * 0.75, worn, soil)

    def occlusion(co, n, i):
        lip = smoothstep(-0.05, -0.2, co.z) * (1.0 - smoothstep(-0.6, -1.0, co.z)) * 0.35
        under = smoothstep(-0.6, -1.05, co.z) * 0.6
        return (max(lip + under, wet(co.x, co.y) * 0.4), 0.0)

    write_layers(obj, regions, occlusion)
    return obj


def build_grounds_t33b():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    return {name: triangle_count(build_ground_t33b(name, target)) for name in STRIP}


# ---------------- water ----------------

def _water_layers(obj, colors, uvs):
    me = obj.data
    attr = me.color_attributes.get("PW_Regions") or me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="PW_Occlusion")
    for v in me.vertices:
        attr.data[v.index].color = colors[v.index]
    for loop in me.loops:
        uv.data[loop.index].uv = uvs[loop.vertex_index]
    me.color_attributes.active_color = attr


def build_stream(collections):
    """GL_Stream_A: the StreamBend ribbon, from the spring to a little waterfall down the near rim."""
    pts, s = stream_curve()
    bm = bmesh.new()
    columns = 7
    rows = []
    colors, uvs = [], []

    def add_row(p, tangent, along, z, half, falling, foam_bias):
        side = Vector((-tangent.y, tangent.x, 0.0)).normalized()
        row = []
        for k in range(columns):
            u = k / (columns - 1)
            off = (u - 0.5) * 2.0
            v = bm.verts.new(p + side * off * half + Vector((0, 0, z)))
            row.append(v)
            edge = abs(off)
            foam = max(smoothstep(0.55, 1.0, edge), foam_bias)
            colors.append((foam, 1.0 - edge * edge, 0.0, falling))
            uvs.append((u, along))
        rows.append(row)

    for i, p in enumerate(pts):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        tangent = (b - a).normalized()
        spring = 1.0 - smoothstep(0.0, 0.9, s[i])
        add_row(p, tangent, s[i], stream_level(s[i]) + 0.01, stream_half(s[i]) * 0.92, 0.0, spring * 0.8)
    # Over the rim: the water bends down the soil wall, narrowing, and ends in a foamy tip.
    last, prev = pts[-1], pts[-2]
    tangent = (last - prev).normalized()
    fall = [(0.08, -0.03, 0.95, 0.3), (0.16, -0.16, 0.9, 0.6), (0.18, -0.42, 0.8, 1.0), (0.14, -0.75, 0.62, 1.0),
            (0.1, -1.05, 0.42, 1.0), (0.06, -1.32, 0.2, 1.0)]
    base_half = stream_half(s[-1]) * 0.92
    along = s[-1]
    for k, (out, z, width, falling) in enumerate(fall):
        p = last + tangent * out
        along += 0.28
        add_row(p, tangent, along, stream_level(s[-1]) + z, base_half * width, falling, 0.35 + 0.15 * k)
    for r in range(len(rows) - 1):
        a, b = rows[r], rows[r + 1]
        for k in range(columns - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0 and f.calc_center_median().z > -0.1:
            f.normal_flip()
    obj = object_from_bmesh("GL_Stream_A", bm, collections, smooth=True)
    _water_layers(obj, colors, uvs)
    return obj


def build_pond(collections):
    """GL_Stream_B: the PondEdge pond surface, an ellipse a little wider than the basin rim (hidden by the banks)."""
    bm = bmesh.new()
    rings, segments = 6, 40
    colors, uvs = [], []
    center = bm.verts.new((POND_CENTER[0], POND_CENTER[1], POND_LEVEL))
    colors.append((0.0, 1.0, 0.0, 0.0))
    # Planar flow coordinates (a polar uv would seam along one radius): ripples drift slowly across the pond.
    uvs.append((POND_CENTER[0] * 0.5, POND_CENTER[1] * 0.5))
    grid = []
    for ri in range(1, rings + 1):
        e = 1.08 * ri / rings
        ring = []
        for j in range(segments):
            a = j / segments * math.tau
            x = POND_CENTER[0] + math.cos(a) * POND_RADII[0] * e
            y = POND_CENTER[1] + math.sin(a) * POND_RADII[1] * e
            ring.append(bm.verts.new((x, y, POND_LEVEL)))
            colors.append((smoothstep(0.7, 1.0, e), max(0.0, 1.0 - e * e), 0.0, 0.0))
            uvs.append((x * 0.5, y * 0.5))
        grid.append(ring)
    for j in range(segments):
        n = (j + 1) % segments
        bm.faces.new((center, grid[0][j], grid[0][n]))
    for ri in range(rings - 1):
        a, b = grid[ri], grid[ri + 1]
        for j in range(segments):
            n = (j + 1) % segments
            bm.faces.new((a[j], b[j], b[n], a[n]))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    obj = object_from_bmesh("GL_Stream_B", bm, collections, smooth=True)
    _water_layers(obj, colors, uvs)
    return obj


def build_water_t33b():
    cols = ensure_collections()
    target = [cols["GEO"], cols["EXPORT"]]
    return {o.name: triangle_count(o) for o in (build_stream(target), build_pond(target))}
