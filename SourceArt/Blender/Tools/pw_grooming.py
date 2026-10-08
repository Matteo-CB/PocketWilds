"""T38 Grooming (Toilettage, doc 01 Contenu S-ACT-001; doc Mini-jeux, section 3): the Prairie grooming set and props.

Intention (charte « fait main », section 10, step 1)
- Character: soft, cosy, tactile. A little diorama (a grass mound with a knitted cushion, round hills behind) and
  chunky, round, slightly oversized props that read on a phone in the grooming close-up.
- Scale, measured on the real animals (LOD0 of BL_Animal_Rabbit, _Squirrel, _Hedgehog, read-only): rabbit 0.30 m wide,
  0.51 m long, 0.65 m with the ears, feet inside a 0.22 m radius, SOCK_HeadTop at 0.438 m on the fur with 3 cm of free
  space in front of the ears; squirrel 0.54 m tall, SOCK_HeadTop 0.432 m; hedgehog 0.32 m, its SOCK_HeadTop sits 2 cm
  under the spines (fur top 0.279 m).
- Counted parts, the same in every export: brush 7 bristle tufts; daisies 5 petals; clover 3 leaflets; burr 12 nubs;
  bow 2 loops, 2 tails, 1 knot.
- Wanted imperfections: the mound's outline wobbles by 4 %; tufts, flowers and trees are laid by fixed seeds.
- Budgets (triangles): mound + cushion + flowers 2,500, backdrop 3,000, brush 600, each debris 120, burr 300, bow 400,
  soap bubble 300.

Data contract: as pw_grassland.py (one palette row per renderer, SH_EnvironmentOpaque): color attribute PW_Regions
(R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion, y height).
GROOMING_PALETTES holds the proposed rows (base, R, G, B, A, occlusion, accent); the previews use them.

Axes: pw_export.py writes Blender (x, y, z) as Unity (-x, z, -y). The animal stands at the set origin (the cushion's
top center) facing Blender -Y, Unity +Z (its own forward); the set camera stands on that side; the hills arc around
Blender +Y (Unity -Z).

Usage inside Blender (the live session or a fresh file), after pw_grassland.py in the same namespace:
    ns["build_grooming"]()             # scene "Grooming", every object below in its export collection
    ns["write_grooming_blend"]()       # bpy.data.libraries.write: the scene to SourceArt/Blender/Activities/
Then in BL_Activity_Grooming.blend (headless is fine), after pw_export.py in the same namespace:
    ns["finish_grooming_blend"]()      # once: the export collection takes the standard name EXPORT, file saved
    ns["export_grooming"]()            # the five FBX files of Assets/_Game/Art/Activities/Grooming/
    ns["render_grooming_previews"](out_dir, rabbit_blend)   # review images; temporary camera, light, materials
In a file that already has an EXPORT collection of its own (the open Prairie file), the grooming scene's collection is
GR_EXPORT, so nothing of that file is touched.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

SCENE = "Grooming"
FLOOR_Z = -0.30                     # the meadow floor, under the cushion top (set origin)
BLEND = "SourceArt/Blender/Activities/BL_Activity_Grooming.blend"
OUT = "Assets/_Game/Art/Activities/Grooming"
EXPORTS = {
    "GR_Set_Prairie.fbx": ("GR_SetMound", "GR_SetBackdrop"),
    "GR_Brush.fbx": ("GR_Brush",),
    "GR_Debris.fbx": ("GR_Debris_",),
    "GR_Burr.fbx": ("GR_Burr",),
    "GR_Bow.fbx": ("GR_Bow",),
    "GR_SoapBubble.fbx": ("GR_SoapBubble",),
}
BUDGETS = {"GR_SetMound": 2500, "GR_SetBackdrop": 3000, "GR_Brush": 600, "GR_Burr": 300, "GR_Bow": 400,
           "GR_SoapBubble": 300, "GR_Debris_": 120}

# Proposed palette rows (base, R, G, B, A, occlusion, accent), sRGB hex like DEF_Palettes_Grassland.
GROOMING_PALETTES = {
    # mound grass base; R cushion knit (dusty rose); G its cream stripes; B daisy petals and clover heads; A hearts.
    "palette.grooming.set_mound": ("8DC166", "E3A3A9", "F3DCCB", "F5EFE2", "F2C94C", "4E6D5D", "B4D873"),
    # meadow and hills; R sunny crests, daisy hearts; G distance haze; B daisy petals; A bark.
    "palette.grooming.set_backdrop": ("8DC166", "D5E08B", "A9C9C2", "F5EFE2", "8F6B50", "4E6D5D", "B4D873"),
    # wood; R bristles; G the inlaid oval on the back; occlusion.
    "palette.grooming.brush": ("C69467", "EBDCBF", "E3C08F", "A87850", "C69467", "6F5A57", "FFFFFF"),
    "palette.grooming.brush_gold": ("E2B24A", "EBDCBF", "F7DC8E", "C2902F", "E2B24A", "8A6A4A", "FFFFFF"),
    # base leaf green; R light green; G petal cream; B bark; A warm yellow.
    "palette.grooming.debris": ("86BD5E", "C3DF8A", "F6EFE0", "9C7352", "F0CB5A", "4E6650", "FFFFFF"),
    "palette.grooming.burr": ("8A7B48", "B8A764", "8A7B48", "8A7B48", "8A7B48", "4E4733", "FFFFFF"),
    # ribbon in base and R (the material's tint), G the knot a shade darker.
    "palette.grooming.bow_pink": ("F2AAC1", "F2AAC1", "DE8CA9", "F2AAC1", "F2AAC1", "9A6A88", "FFFFFF"),
    "palette.grooming.bow_sky": ("9EC8EE", "9EC8EE", "7FAEDD", "9EC8EE", "9EC8EE", "5F7AA0", "FFFFFF"),
    "palette.grooming.bow_cream": ("F5E7CA", "F5E7CA", "E5CFA6", "F5E7CA", "F5E7CA", "9C8A78", "FFFFFF"),
}


# ---------------------------------------------------------------- scene

def _export_collection():
    """The grooming scene and its export collection (EXPORT in its own file, GR_EXPORT next to another kit)."""
    scene = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    own = bpy.data.collections.get("EXPORT")
    name = "EXPORT" if own is None or own.name in scene.collection.children else "GR_EXPORT"
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if col.name not in scene.collection.children:
        scene.collection.children.link(col)
    return scene, col


def _replace(name):
    old = bpy.data.objects.get(name)
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


class _Parts:
    """Remembers which part each vertex belongs to while a bmesh is built (vertex order is kept by to_mesh)."""

    def __init__(self, bm):
        self.bm = bm
        self.tags = []
        self.data = []

    def mark(self, tag, data=None):
        n = len(self.bm.verts) - len(self.tags)
        self.tags.extend([tag] * n)
        self.data.extend([data] * n)


# ---------------------------------------------------------------- shape helpers

def _loft(bm, centers, half_side, half_up, sides, up=Vector((0.0, 0.0, 1.0)), start=None, end=None, shape=None,
          phase=0.0):
    """Tube through `centers`: rings of `sides` vertices, an ellipse half_side wide along the side axis and half_up
    along the up axis (one value per center). start/end: apex points closing the ends with a rounded tip (None leaves
    the end open inside another part). shape(i, k, point, side, up) may move a vertex. Faces wind outward."""
    rings = []
    n = len(centers)
    for i, c in enumerate(centers):
        t = (centers[min(i + 1, n - 1)] - centers[max(i - 1, 0)]).normalized()
        u = up - t * up.dot(t)
        if u.length < 1e-6:
            u = t.orthogonal()
        u.normalize()
        s = u.cross(t).normalized()
        ring = []
        for k in range(sides):
            a = phase + k / sides * math.tau
            p = c + s * (math.cos(a) * half_side[i]) + u * (math.sin(a) * half_up[i])
            if shape is not None:
                p = shape(i, k, p, s, u)
            ring.append(bm.verts.new(p))
        rings.append(ring)
    for ra, rb in zip(rings, rings[1:]):
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((ra[k], ra[m], rb[m], rb[k]))
    if start is not None:
        apex = bm.verts.new(start)
        for k in range(sides):
            bm.faces.new((rings[0][(k + 1) % sides], rings[0][k], apex))
    if end is not None:
        apex = bm.verts.new(end)
        for k in range(sides):
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], apex))
    return rings


def _sphere(bm, center, radii, segments, rings, displace=None, rotation=None):
    """UV ellipsoid; displace(direction) gives a radius factor; rotation (3x3) turns it. Faces wind outward."""
    def point(d):
        f = displace(d) if displace else 1.0
        p = Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * f
        return center + (rotation @ p if rotation is not None else p)

    top = bm.verts.new(point(Vector((0.0, 0.0, 1.0))))
    grid = []
    for j in range(1, rings):
        phi = math.pi * j / rings
        grid.append([bm.verts.new(point(Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)))))
                     for th in (math.tau * k / segments for k in range(segments))])
    bottom = bm.verts.new(point(Vector((0.0, 0.0, -1.0))))
    for k in range(segments):
        m = (k + 1) % segments
        bm.faces.new((top, grid[0][k], grid[0][m]))
        bm.faces.new((bottom, grid[-1][m], grid[-1][k]))
    for a, b in zip(grid, grid[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            bm.faces.new((a[k], b[k], b[m], a[m]))


def _revolve(bm, profile, segments, top=None, radius=None, xy=(1.0, 1.0), close_bottom=None):
    """Surface around Z from a profile [(r, z), ...] read from the top down; `top` closes it with a center vertex at
    that height, `close_bottom` with one at that height. radius(i, theta) scales ring i (wobble, knit ribs)."""
    rings = []
    for i, (r, z) in enumerate(profile):
        ring = []
        for k in range(segments):
            th = math.tau * k / segments
            f = radius(i, th) if radius else 1.0
            ring.append(bm.verts.new((math.cos(th) * r * f * xy[0], math.sin(th) * r * f * xy[1], z)))
        rings.append(ring)
    if top is not None:
        apex = bm.verts.new((0.0, 0.0, top))
        for k in range(segments):
            bm.faces.new((apex, rings[0][k], rings[0][(k + 1) % segments]))
    for a, b in zip(rings, rings[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            bm.faces.new((a[k], b[k], b[m], a[m]))
    if close_bottom is not None:
        apex = bm.verts.new((0.0, 0.0, close_bottom))
        for k in range(segments):
            bm.faces.new((apex, rings[-1][(k + 1) % segments], rings[-1][k]))
    return rings


def _lens(bm, center, outline, top_lift, bottom_drop, normal=Vector((0.0, 0.0, 1.0))):
    """A flat rounded piece: outline points (counter-clockwise seen along `normal`), a raised center on top and a
    lowered one underneath, so the smooth normals round its edge."""
    ring = [bm.verts.new(p) for p in outline]
    top = bm.verts.new(center + normal * top_lift)
    bottom = bm.verts.new(center - normal * bottom_drop)
    n = len(ring)
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((top, ring[k], ring[m]))
        bm.faces.new((bottom, ring[m], ring[k]))
    return ring, top, bottom


def _settle(bm, tolerance=0.0006):
    """Lays the piece on z = 0 with the pivot under its contact: the x, y center of the lowest vertices."""
    zmin = min(v.co.z for v in bm.verts)
    low = [v.co for v in bm.verts if v.co.z <= zmin + tolerance]
    offset = Vector((-sum(p.x for p in low) / len(low), -sum(p.y for p in low) / len(low), -zmin))
    for v in bm.verts:
        v.co += offset
    return offset


def _finish(name, bm, col, regions, occlusion, smooth=True):
    bm.normal_update()
    _replace(name)
    obj = object_from_bmesh(name, bm, [col], smooth=smooth)
    write_layers(obj, regions, occlusion)
    return obj


# ---------------------------------------------------------------- brush

BRUSH_HEAD = (0.026, 0.034)                        # half width (x), half length (y) of the oval back
BRUSH_PROFILE = [(0.35, 0.0370), (0.68, 0.0348), (0.90, 0.0300), (1.0, 0.0230), (0.97, 0.0170), (0.86, 0.0150)]
BRUSH_TOP = 0.0375                                  # profile above: (scale, z) from the top down
BRUSH_UNDER = 0.0150
BRUSH_TUFT_RADIUS = 0.0078
BRUSH_TUFTS = [(0.0, 0.0), (0.0, -0.0165), (0.0, 0.0165), (-0.0143, -0.0083), (-0.0143, 0.0083), (0.0143, -0.0083),
               (0.0143, 0.0083)]


def build_brush(col):
    """GR_Brush: a domed oval wooden back with an inlaid oval, a short chunky handle with a rounded end, seven soft
    rounded bristle tufts underneath. Bristles down (Unity -Y), handle toward Blender +Y (Unity -Z), 0.117 m long; the
    pivot is the center of the bristle tips (z = 0). Regions: base wood, R bristles, G the inlay (a gold material tints
    base and G only: palette.grooming.brush_gold)."""
    bm = bmesh.new()
    parts = _Parts(bm)
    _revolve(bm, BRUSH_PROFILE, 18, top=BRUSH_TOP, xy=BRUSH_HEAD, close_bottom=BRUSH_UNDER)
    parts.mark("head")
    _loft(bm, [Vector((0.0, 0.026, 0.0250)), Vector((0.0, 0.045, 0.0275)), Vector((0.0, 0.062, 0.0300)),
               Vector((0.0, 0.074, 0.0315)), Vector((0.0, 0.0795, 0.0321))],
          [0.0108, 0.0100, 0.0096, 0.0094, 0.0072], [0.0068, 0.0064, 0.0062, 0.0062, 0.0050], 8,
          end=Vector((0.0, 0.0832, 0.0323)))
    parts.mark("handle")
    r = BRUSH_TUFT_RADIUS
    for x, y in BRUSH_TUFTS:
        splay = Vector((x, y, 0.0)) * 0.12
        centers = [Vector((x, y, 0.0160)), Vector((x, y, 0.0068)) + splay * 0.5, Vector((x, y, 0.0019)) + splay * 0.9]
        _loft(bm, centers, [r * 0.94, r * 1.0, r * 0.84], [r * 0.94, r * 1.0, r * 0.84], 8,
              up=Vector((1.0, 0.0, 0.0)), end=Vector((x, y, 0.0)) + splay)
        parts.mark("bristle")

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "bristle":
            return (1.0, 0.0, 0.0, 0.0)
        inlay = 1.0 if tag == "head" and co.z > 0.0365 else 0.0
        return (0.0, inlay, 0.0, 0.0)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        if tag == "bristle":
            return (0.45 * smoothstep(0.005, 0.016, co.z), co.z / BRUSH_TOP)
        under = 0.3 * smoothstep(-0.3, -0.8, n.z)
        neck = 0.18 * (1.0 - smoothstep(0.026, 0.036, co.y)) if tag == "handle" else 0.0
        return (under + neck, co.z / BRUSH_TOP)

    return _finish("GR_Brush", bm, col, regions, occlusion)


# ---------------------------------------------------------------- debris

def _flat_loft(bm, xs, ys, half_w, half_t, sides, lift=None, start=None, end=None, shape=None):
    """A flat piece lying on z = 0 along a curve in the XY plane: half widths and half thicknesses per station."""
    centers = []
    for i, (x, y) in enumerate(zip(xs, ys)):
        centers.append(Vector((x, y, half_t[i] + (lift(i) if lift else 0.0))))
    st = None if start is None else Vector((start[0], start[1], half_t[0] + (lift(0) if lift else 0.0)))
    en = None if end is None else Vector((end[0], end[1], half_t[-1] + (lift(len(xs) - 1) if lift else 0.0)))
    return _loft(bm, centers, half_w, half_t, sides, start=st, end=en, shape=shape)


def build_debris_grass_blade(col):
    """GR_Debris_GrassBlade: a chunky grass blade, 4 cm, gently curved, its rounded tip curling up. Base green, R the
    lighter tip."""
    bm = bmesh.new()
    n = 8
    ts = [i / (n - 1) for i in range(n)]
    xs = [(t - 0.5) * 0.036 for t in ts]
    ys = [0.0035 * math.sin(t * math.pi) - 0.0012 * t for t in ts]
    hw = [0.0043, 0.0050, 0.0050, 0.0047, 0.0043, 0.0038, 0.0033, 0.0027]
    ht = [0.0012] * (n - 1) + [0.0010]
    _flat_loft(bm, xs, ys, hw, ht, 6, lift=lambda i: 0.0035 * max(0.0, ts[i] - 0.55) ** 2 / 0.2025,
               start=(xs[0] - 0.0034, ys[0]), end=(xs[-1] + 0.0036, ys[-1] - 0.0004))
    offset = _settle(bm)

    def regions(co, n_, i):
        t = (co.x - offset.x + 0.0214) / 0.043
        return (smoothstep(0.25, 1.0, t) * 0.85, 0.0, 0.0, 0.0)

    return _finish("GR_Debris_GrassBlade", bm, col, regions, _flat_occlusion)


def _flat_occlusion(co, n, i):
    return (0.3 * smoothstep(-0.2, -0.9, n.z), min(1.0, co.z / 0.01))


def build_debris_daisy_petal(col):
    """GR_Debris_DaisyPetal: one round daisy petal, 3 cm, a little cupped, the tip lifted. G cream, A the yellow
    base where it held the flower."""
    bm = bmesh.new()
    xs = [0.0, 0.004, 0.0085, 0.013, 0.0175, 0.0215, 0.0245]
    hw = [0.0024, 0.0042, 0.0055, 0.0062, 0.0063, 0.0056, 0.0040]
    ht = [0.0011] * 7

    def cup(i, k, p, s, u):
        if k in (0, 3):
            p = p + Vector((0.0, 0.0, 0.0010 * hw[i] / 0.0063))
        return p

    _flat_loft(bm, xs, [0.0] * 7, hw, ht, 6, lift=lambda i: 0.0026 * (xs[i] / 0.0245) ** 2,
               start=(-0.0018, 0.0), end=(0.0274, 0.0), shape=cup)
    offset = _settle(bm)

    def regions(co, n, i):
        t = (co.x - offset.x + 0.0018) / 0.0292
        return (0.0, 1.0, 0.0, 0.8 * (1.0 - smoothstep(0.04, 0.4, t)))

    return _finish("GR_Debris_DaisyPetal", bm, col, regions, _flat_occlusion)


def build_debris_clover_leaf(col):
    """GR_Debris_CloverLeaf: three round heart-shaped leaflets and a short stem, 3 cm. Base green, R the pale middle
    of each leaflet."""
    bm = bmesh.new()
    parts = _Parts(bm)
    for k in range(3):
        a = math.radians(90.0 + 120.0 * k)
        d = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-d.y, d.x, 0.0))
        c = d * 0.0074 + Vector((0.0, 0.0, 0.0011))
        outline = []
        for j in range(12):
            phi = j / 12 * math.tau
            # A round heart: a soft notch at the outer end, narrower toward the middle of the clover.
            notch = 1.0 - 0.26 * math.exp(-(min(phi, math.tau - phi) / 0.4) ** 2)
            narrow = 0.8 + 0.2 * math.cos(phi)
            outline.append(c + d * (math.cos(phi) * 0.0069 * notch) + side * (math.sin(phi) * 0.0066 * notch * narrow))
        _lens(bm, c, outline, 0.0015, 0.0011)
        parts.mark("leaflet")
    _loft(bm, [Vector((0.0, -0.001, 0.0011)), Vector((0.001, -0.0065, 0.0011)), Vector((0.0028, -0.0115, 0.0011))],
          [0.0011] * 3, [0.0011] * 3, 4, end=Vector((0.0034, -0.0127, 0.0011)))
    parts.mark("stem")
    _settle(bm)

    def regions(co, n, i):
        if parts.tags[i] == "stem":
            return (0.25, 0.0, 0.0, 0.0)
        return (0.55 if co.z > 0.0022 else 0.0, 0.0, 0.0, 0.0)

    return _finish("GR_Debris_CloverLeaf", bm, col, regions, _flat_occlusion)


def build_debris_leaf(col):
    """GR_Debris_Leaf: a round little leaf from the meadow's trees, 3.6 cm with its stem, folded along a light
    midrib. Base green warmed by A; R the midrib; B the stem."""
    bm = bmesh.new()
    parts = _Parts(bm)
    xs = [0.0, 0.006, 0.0125, 0.019, 0.0245, 0.029]
    hw = [0.0045, 0.0088, 0.0101, 0.0092, 0.0068, 0.0036]
    ht = [0.0010] * 6

    def fold(i, k, p, s, u):
        lift_edge = 0.0013 * hw[i] / 0.0101
        if k in (0, 4):
            return p + Vector((0.0, 0.0, lift_edge))
        if k in (1, 3, 5, 7):
            return p + Vector((0.0, 0.0, lift_edge * 0.45))
        return p

    _flat_loft(bm, xs, [0.0004 * math.sin(x * 120.0) for x in xs], hw, ht, 8,
               lift=lambda i: 0.0022 * (xs[i] / 0.029) ** 3, start=(-0.0012, 0.0), end=(0.0330, 0.0004), shape=fold)
    parts.mark("leaf")
    _loft(bm, [Vector((0.0015, 0.0, 0.0011)), Vector((-0.004, 0.0004, 0.0009)), Vector((-0.0072, 0.0012, 0.0009))],
          [0.0009] * 3, [0.0009] * 3, 4, end=Vector((-0.0081, 0.0014, 0.0009)))
    parts.mark("stem")
    _settle(bm)
    midrib = set()
    # The leaf's top middle vertices (k = 2 of each ring) carry the midrib.
    for ring_start in range(0, 6 * 8, 8):
        midrib.add(ring_start + 2)

    def regions(co, n, i):
        if parts.tags[i] == "stem":
            return (0.0, 0.0, 0.6, 0.2)
        return (1.0 if i in midrib else 0.0, 0.0, 0.0, 0.14)

    return _finish("GR_Debris_Leaf", bm, col, regions, _flat_occlusion)


def build_debris_twig(col):
    """GR_Debris_Twig: a little forked twig, 4 cm, rounded ends showing the light wood. B bark, G the cut ends."""
    bm = bmesh.new()
    parts = _Parts(bm)
    xs = [-0.019, -0.009, 0.0, 0.009, 0.019]
    ys = [-0.002, 0.0005, 0.0015, 0.0, -0.0025]
    radii = [0.0034, 0.0032, 0.0030, 0.0027, 0.0024]
    _flat_loft(bm, xs, ys, radii, radii, 6, start=(-0.0218, -0.0027), end=(0.0215, -0.0032))
    parts.mark("main")
    fr = [0.0022, 0.0019, 0.0016]
    _flat_loft(bm, [0.002, 0.0085, 0.0122], [0.0015, 0.0075, 0.0118], fr, fr, 6, end=(0.0136, 0.0136))
    parts.mark("fork")
    offset = _settle(bm)
    # The rounded ends show the light wood under the bark.
    ends = [Vector((x, y, 0.0)) + offset for x, y in ((-0.0218, -0.0027), (0.0215, -0.0032), (0.0136, 0.0136))]

    def regions(co, n, i):
        cut = max(1.0 - smoothstep(0.0, 0.0035, math.hypot(co.x - p.x, co.y - p.y)) for p in ends)
        return (0.0, 0.6 * cut, 1.0, 0.0)

    return _finish("GR_Debris_Twig", bm, col, regions, _flat_occlusion)


def build_debris_seed(col):
    """GR_Debris_Seed: a dandelion seed lying on its side: a small brown seed, a thin stalk and a soft round cream
    tuft (a ball, never rays), 3.8 cm. G the tuft, B the seed."""
    bm = bmesh.new()
    parts = _Parts(bm)
    body = [Vector((-0.0175, 0.0, 0.0018)), Vector((-0.0145, 0.0, 0.0018)), Vector((-0.0112, 0.0, 0.0018)),
            Vector((-0.0088, 0.0, 0.0018))]
    rb = [0.0011, 0.0018, 0.0017, 0.0010]
    _loft(bm, body, rb, rb, 6, start=Vector((-0.0189, 0.0, 0.0018)))
    parts.mark("seed")
    _loft(bm, [Vector((-0.0092, 0.0, 0.0018)), Vector((0.0062, 0.0, 0.0066))], [0.00095, 0.00095], [0.00095, 0.00095], 4)
    parts.mark("stalk")
    _sphere(bm, Vector((0.0122, 0.0, 0.0074)), Vector((0.0074, 0.0077, 0.0072)), 8, 5)
    parts.mark("tuft")
    _settle(bm)

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "seed":
            return (0.0, 0.0, 1.0, 0.0)
        if tag == "stalk":
            return (0.0, 0.7, 0.35, 0.0)
        return (0.0, 1.0, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.28 * smoothstep(-0.2, -0.9, n.z), min(1.0, co.z / 0.015))

    return _finish("GR_Debris_Seed", bm, col, regions, occlusion)


# ---------------------------------------------------------------- burr

BURR_RADIUS = 0.0135


def build_burr(col):
    """GR_Burr: a burdock burr made cute: a soft olive-brown ball with twelve short round nubs (icosahedron
    directions, blunt domes, never spikes), 3.7 cm; it rests on three nubs, pivot under the middle of those three nubs.
    R the lighter nub tops. Normals are set so each nub shades like a little dome."""
    bm = bmesh.new()
    R = BURR_RADIUS
    phi = (1.0 + 5 ** 0.5) / 2.0
    dirs = []
    for a, b in ((1, phi), (-1, phi), (1, -phi), (-1, -phi)):
        dirs += [Vector((0, a, b)), Vector((a, b, 0)), Vector((b, 0, a))]
    dirs = [d.normalized() for d in dirs]
    # A face of the icosahedron points down: the burr stands on three nubs.
    face = (Vector((0, 1, phi)) + Vector((0, -1, phi)) + Vector((phi, 0, 1))).normalized()
    turn = face.rotation_difference(Vector((0.0, 0.0, -1.0))).to_matrix()
    dirs = [turn @ d for d in dirs]
    _sphere(bm, Vector(), Vector((R, R, R)), 8, 6)
    normals = [v.co.normalized() for v in bm.verts]
    rng = random.Random(3811)
    for d in dirs:
        u = d.orthogonal().normalized()
        w = d.cross(u).normalized()
        length = rng.uniform(1.36, 1.41)
        ring_dirs = [u * math.cos(a) + w * math.sin(a) for a in (k / 6 * math.tau for k in range(6))]
        rb = [bm.verts.new(d * (0.92 * R) + r * (0.42 * R)) for r in ring_dirs]
        rm = [bm.verts.new(d * (1.18 * R) + r * (0.33 * R)) for r in ring_dirs]
        apex = bm.verts.new(d * (length * R))
        normals += [(d * 0.35 + r).normalized() for r in ring_dirs]
        normals += [(d * 0.8 + r * 0.8).normalized() for r in ring_dirs]
        normals.append(d.copy())
        for k in range(6):
            m = (k + 1) % 6
            bm.faces.new((rb[k], rb[m], rm[m], rm[k]))
            bm.faces.new((rm[k], rm[m], apex))
    offset = _settle(bm, tolerance=0.0008)
    center = offset.copy()

    def regions(co, n, i):
        return (smoothstep(1.08 * R, 1.3 * R, (co - center).length), 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        dist = (co - center).length
        return (0.36 * (1.0 - smoothstep(0.97 * R, 1.14 * R, dist)) + 0.15 * smoothstep(-0.2, -0.9, n.z), co.z / (2.8 * R))

    obj = _finish("GR_Burr", bm, col, regions, occlusion)
    obj.data.normals_split_custom_set_from_vertices(normals)
    return obj


# ---------------------------------------------------------------- bow

def build_bow(col):
    """GR_Bow: a plush ribbon bow, 6.2 cm wide, facing Blender -Y (Unity +Z, like the animal): two puffy loops, two
    short tails, a round knot. Pivot at the attach point under the knot; nothing goes below it. Regions: R the ribbon
    (the material's pink, sky blue or cream), G the knot, a shade darker."""
    bm = bmesh.new()
    parts = _Parts(bm)
    _sphere(bm, Vector((0.0, 0.0, 0.0106)), Vector((0.0078, 0.0066, 0.0106)), 8, 6)
    parts.mark("knot")
    xs = [0.004, 0.0095, 0.0155, 0.021, 0.0255, 0.0289]
    a = [0.0050, 0.0074, 0.0099, 0.0114, 0.0119, 0.0103]
    b = [0.0042, 0.0056, 0.0065, 0.0070, 0.0068, 0.0058]
    for sign in (1.0, -1.0):
        centers = [Vector((sign * x, 0.0, 0.0150 + 0.21 * x)) for x in xs]

        def dimple(i, k, p, s, u, centers=centers):
            # A soft crease down the front of each loop: the ribbon folds back on itself.
            front = Vector((0.0, -1.0, 0.0))
            if 1 <= i <= 4 and (p - centers[i]).normalized().dot(front) > 0.9:
                return p - front * (b[i] * 0.3)
            return p

        _loft(bm, centers, b, a, 10, up=Vector((0.0, 0.0, 1.0)), end=Vector((sign * 0.0316, 0.0, 0.0150 + 0.21 * 0.0316)),
              shape=dimple)
        parts.mark("loop")
        tail = [Vector((sign * 0.002, -0.0028, 0.0062)), Vector((sign * 0.0105, -0.0040, 0.0052)),
                Vector((sign * 0.0175, -0.0046, 0.0047)), Vector((sign * 0.0230, -0.0048, 0.0044))]

        def notch(i, k, p, s, u, last=len(tail) - 1):
            # Ribbon ends cut in a V: both corners reach out, the middle stays back.
            if i == last and k in (0, 3):
                return p + s.cross(u) * 0.0028
            return p

        _loft(bm, tail, [0.0040, 0.0040, 0.0041, 0.0041], [0.0013, 0.0013, 0.0013, 0.0012], 6,
              up=Vector((0.0, -1.0, 0.0)), end=tail[-1] + Vector((sign * 0.0006, 0.0, 0.0)), shape=notch)
        parts.mark("tail")

    def regions(co, n, i):
        knot = 1.0 if parts.tags[i] == "knot" else 0.0
        return (1.0, knot, 0.0, 0.0)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        if tag == "loop":
            pinch = 1.0 - smoothstep(0.006, 0.016, abs(co.x))
            return (0.38 * pinch + 0.18 * smoothstep(-0.3, -0.9, n.z), co.z / 0.035)
        if tag == "tail":
            return (0.3 * (1.0 - smoothstep(0.004, 0.012, abs(co.x))), co.z / 0.035)
        return (0.12 * smoothstep(-0.2, -0.9, n.z), co.z / 0.035)

    return _finish("GR_Bow", bm, col, regions, occlusion)


# ---------------------------------------------------------------- soap bubble

def build_soap_bubble(col):
    """GR_SoapBubble: a smooth sphere of radius 1 (scaled in Unity), 288 triangles, smooth normals; uv y = height."""
    bm = bmesh.new()
    _sphere(bm, Vector(), Vector((1.0, 1.0, 1.0)), 16, 10)
    return _finish("GR_SoapBubble", bm, col, lambda co, n, i: (0.0, 0.0, 0.0, 0.0),
                   lambda co, n, i: (0.0, (co.z + 1.0) * 0.5))


# ---------------------------------------------------------------- the set: mound, cushion, flowers

MOUND_PROFILE = [(0.300, -0.0800), (0.400, -0.0830), (0.475, -0.0970), (0.535, -0.1260), (0.580, -0.1660),
                 (0.612, -0.2120), (0.633, -0.2600), (0.646, FLOOR_Z)]
MOUND_SEGMENTS = 36
CUSHION_PROFILE = [(0.13, 0.0), (0.235, 0.0), (0.29, -0.003), (0.325, -0.012), (0.348, -0.028), (0.356, -0.046),
                   (0.350, -0.062), (0.333, -0.074), (0.305, -0.0799)]
CUSHION_SEGMENTS = 48
CUSHION_RIBS = 24


def mound_wobble(i, th):
    """The mound's outline: round, a little uneven (4 %), the same everywhere it is measured."""
    ramp = smoothstep(1.0, 3.0, float(i))
    return 1.0 + ramp * (0.022 * math.sin(3.0 * th + 0.7) + 0.014 * math.sin(5.0 * th + 2.1))


def mound_radius(th):
    """Radius of the mound's foot (on the floor) in direction th."""
    return MOUND_PROFILE[-1][0] * mound_wobble(len(MOUND_PROFILE) - 1, th)


def _knit(i, th):
    side = {3: 0.5, 4: 1.0, 5: 1.0, 6: 1.0, 7: 0.5}.get(i, 0.0)
    return 1.0 + 0.006 * side * math.cos(CUSHION_RIBS * th)


def _grass_tuft(bm, base, blades, height, seed, away):
    """A chunky grass tuft (double-sided blades) leaning away from `away` (the mound's center)."""
    rng = random.Random(seed)
    out_dir = Vector((base.x - away.x, base.y - away.y, 0.0)).normalized()
    for i in range(blades):
        angle = math.atan2(out_dir.y, out_dir.x) + rng.uniform(-1.3, 1.3)
        lean = rng.uniform(0.25, 0.55)
        h = height * rng.uniform(0.72, 1.0)
        w = height * rng.uniform(0.28, 0.36)
        d = Vector((math.cos(angle), math.sin(angle), 0.0))
        root = base + d * rng.uniform(0.0, height * 0.14)
        side = Vector((-d.y, d.x, 0.0)) * w * 0.5
        mid = root + d * h * lean * 0.45 + Vector((0.0, 0.0, h * 0.55))
        tip = root + d * h * lean + Vector((0.0, 0.0, h))
        pts = [root - side, root + side, mid - side * 0.62, mid + side * 0.62, tip]
        front = [bm.verts.new(p) for p in pts]
        back = [bm.verts.new(p) for p in pts]
        for a, b, c in ((0, 1, 3), (0, 3, 2), (2, 3, 4)):
            bm.faces.new((front[a], front[b], front[c]))
            bm.faces.new((back[c], back[b], back[a]))


def _daisy(bm, parts, base, height, petal, seed, facing=Vector((0.0, -0.35, 1.0))):
    """A small round daisy: a stem, five round petals around a domed heart, one leaf at the foot."""
    rng = random.Random(seed)
    normal = facing.normalized()
    head = base + Vector((rng.uniform(-0.008, 0.008), rng.uniform(-0.008, 0.008), height))
    _loft(bm, [base, base + (head - base) * 0.5 + Vector((0.004, 0.0, 0.0)), head - normal * 0.003],
          [0.0034, 0.0031, 0.0029], [0.0034, 0.0031, 0.0029], 4)
    parts.mark("stem")
    _leaf(bm, base + Vector((0.0, 0.0, 0.006)), Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.0)), 0.035, 0.014, 0.3)
    parts.mark("stem")
    u = normal.orthogonal().normalized()
    w = normal.cross(u).normalized()
    turn = rng.uniform(0.0, math.tau)
    for k in range(5):
        a = turn + k / 5 * math.tau
        d = u * math.cos(a) + w * math.sin(a)
        side = normal.cross(d).normalized()
        c = head + d * (0.0062 + petal * 0.5) + normal * 0.0015
        outline = []
        for j in range(6):
            t = j / 6 * math.tau
            outline.append(c + d * (math.cos(t) * petal * 0.5) + side * (math.sin(t) * petal * 0.31) + normal * (
                0.0018 * math.cos(t)))
        _lens(bm, c, outline, 0.0016, 0.0012, normal=normal)
        parts.mark("petal")
    ring = [head + (u * math.cos(t) + w * math.sin(t)) * 0.0078 + normal * 0.0012 for t in (j / 6 * math.tau for j in range(6))]
    _lens(bm, head + normal * 0.0012, ring, 0.0052, 0.0022, normal=normal)
    parts.mark("heart")


def _trefoil(bm, parts, base, height, leaflet, seed, tilt=0.18):
    """Clover: a stalk and three heart-shaped leaflets held almost flat."""
    rng = random.Random(seed)
    top = base + Vector((rng.uniform(-0.006, 0.006), rng.uniform(-0.006, 0.006), height))
    _loft(bm, [base, top], [0.0024, 0.0021], [0.0024, 0.0021], 3)
    parts.mark("clover_stem")
    turn = rng.uniform(0.0, math.tau)
    for k in range(3):
        a = turn + k / 3 * math.tau
        d = Vector((math.cos(a), math.sin(a), tilt)).normalized()
        side = Vector((-math.sin(a), math.cos(a), 0.0))
        normal = side.cross(d).normalized()
        if normal.z < 0.0:
            normal = -normal
        c = top + d * (leaflet * 0.62)
        outline = []
        for j in range(6):
            phi = j / 6 * math.tau
            notch = 0.8 if j == 0 else 1.0
            narrow = 0.8 + 0.2 * math.cos(phi)
            outline.append(c + d * (math.cos(phi) * leaflet * 0.55 * notch) + side * (math.sin(phi) * leaflet * 0.5 * narrow))
        # Wind the outline counter-clockwise around the normal.
        if (outline[1] - c).cross(outline[2] - c).dot(normal) < 0.0:
            outline.reverse()
        _lens(bm, c, outline, 0.0022, 0.0016, normal=normal)
        parts.mark("clover_leaf")


def _ring_spots(count, r_min, r_max, seed, sectors, taken, clearance):
    """Deterministic spots around the mound's foot: (x, y, angle, radius_needed) kept apart from `taken`."""
    rng = random.Random(seed)
    spots = []
    tries = 0
    while len(spots) < count and tries < 4000:
        tries += 1
        lo, hi = sectors[len(spots) % len(sectors)]
        th = math.radians(rng.uniform(lo, hi))
        r = mound_radius(th) + rng.uniform(r_min, r_max)
        p = Vector((math.cos(th) * r, math.sin(th) * r, 0.0))
        if all((p - q).length > clearance + qr for q, qr in taken):
            spots.append((p, th))
            taken.append((p, clearance))
    return spots


def build_set_mound(col):
    """GR_SetMound: a soft round grass mound (1.3 m wide, 0.30 m high) carrying a round knitted cushion (0.71 m) whose
    top is flat and level at the origin (z = 0): the animal stands on it. Tiny daisies, clover tufts and grass tufts
    stand on the floor around its foot, never on the cushion. The mound's foot meets the floor of GR_SetBackdrop at
    z = -0.30 (same origin). Regions: base grass, R cushion, G knit ribs, B petals and clover heads, A daisy hearts."""
    bm = bmesh.new()
    parts = _Parts(bm)
    _revolve(bm, MOUND_PROFILE, MOUND_SEGMENTS, radius=mound_wobble)
    parts.mark("mound")
    _revolve(bm, CUSHION_PROFILE, CUSHION_SEGMENTS, top=0.0, radius=_knit)
    parts.mark("cushion")
    floor = Vector((0.0, 0.0, FLOOR_Z))
    taken = []
    # Daisies first (they need the most room), then clover, then grass tufts filling between them.
    daisy_sectors = [(-125, -95), (-70, -40), (200, 230), (-20, 15), (160, 190)]
    for k, (p, th) in enumerate(_ring_spots(5, 0.07, 0.17, 4101, daisy_sectors, taken, 0.045)):
        _daisy(bm, parts, p + floor, 0.07 + 0.012 * (k % 3), 0.017, 4110 + k)
    clover_sectors = [(-100, -75), (230, 255), (-40, -15), (190, 215)]
    for k, (p, th) in enumerate(_ring_spots(4, 0.05, 0.12, 4201, clover_sectors, taken, 0.04)):
        _trefoil(bm, parts, p + floor, 0.032 + 0.008 * (k % 2), 0.019, 4210 + k)
        q = p + Vector((math.cos(th + 1.7), math.sin(th + 1.7), 0.0)) * 0.022
        _trefoil(bm, parts, q + floor, 0.024, 0.016, 4230 + k)
        if k in (0, 3):
            stalk = q + Vector((math.cos(th - 1.2), math.sin(th - 1.2), 0.0)) * 0.03
            _loft(bm, [stalk + floor, stalk + floor + Vector((0.0, 0.0, 0.05))], [0.0022, 0.0019], [0.0022, 0.0019], 3)
            parts.mark("clover_stem")
            _sphere(bm, stalk + floor + Vector((0.0, 0.0, 0.058)), Vector((0.011, 0.011, 0.0095)), 6, 4)
            parts.mark("clover_head")
    tuft_sectors = [(-140, -115), (-85, -60), (-35, -5), (5, 40), (100, 135), (140, 170), (175, 205), (210, 240)]
    for k, (p, th) in enumerate(_ring_spots(8, 0.03, 0.06, 4301, tuft_sectors, taken, 0.03)):
        _grass_tuft(bm, p + floor, 5, 0.062, 4310 + k, Vector((0.0, 0.0, 0.0)))
        parts.mark("tuft")

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "cushion":
            r = math.hypot(co.x, co.y)
            th = math.atan2(co.y, co.x)
            if co.z > -0.0005 and r < 0.26:
                # Knitted rounds on top: a cream ring between rose ones.
                return (1.0, 0.85 * (1.0 - smoothstep(0.01, 0.045, abs(r - 0.13))) if r > 0.01 else 0.0, 0.0, 0.0)
            # The side: cream and rose rows (ring by ring), a hint of the knit rib between stitches.
            row = 1.0 if (co.z > -0.0062 or -0.05 < co.z < -0.04) else 0.0
            rib = 0.12 * (0.5 - 0.5 * math.cos(CUSHION_RIBS * th))
            return (1.0, min(1.0, 0.85 * row + rib), 0.0, 0.0)
        if tag == "petal":
            return (0.0, 0.0, 1.0, 0.0)
        if tag == "heart":
            return (0.0, 0.0, 0.0, 1.0)
        if tag == "clover_head":
            return (0.0, 0.0, 0.88, 0.08)
        if tag == "clover_leaf":
            return (0.0, 0.0, 0.18 if n.z > 0.85 else 0.0, 0.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        h = co.z - FLOOR_Z
        if tag == "mound":
            r = math.hypot(co.x, co.y)
            foot = 0.5 * (1.0 - smoothstep(0.0, 0.1, h))
            crease = 0.42 * (1.0 - smoothstep(0.3, 0.41, r)) if co.z > -0.1 else 0.0
            return (foot + crease, 0.0)
        if tag == "cushion":
            return (0.42 * smoothstep(-0.05, -0.0799, co.z), 0.0)
        if tag in ("stem", "clover_stem", "tuft"):
            return (0.5 * (1.0 - smoothstep(0.0, 0.05, h)), min(1.0, h / 0.08))
        if tag == "clover_leaf":
            return (0.3, min(1.0, h / 0.08))
        return (0.05, min(1.0, h / 0.08))

    return _finish("GR_SetMound", bm, col, regions, occlusion)


# ---------------------------------------------------------------- the set: backdrop

FLOOR_RINGS = [0.58, 0.68, 0.82, 1.05, 1.4, 1.85, 2.6]
FLOOR_SEGMENTS = 32
# Hill layers around the +Y side: (first and last angle in degrees, step, base radius, crest radius, kind,
# bumps (center deg, half width deg, height above the floor), lowest crest, haze). Seen from the reference camera
# (2.1 m from the animal, 0.9 m above the floor), the near crests stay just under the eye, the middle ones behind the
# animal's head, the far ones at the top of the frame: the animal is seen against hills and sky only.
HILLS = [
    (0.0, 180.0, 2.8, 2.25, 2.95, "near",
     [(4, 18, 0.55), (27, 17, 0.78), (50, 19, 0.62), (74, 18, 0.88), (98, 17, 0.70), (121, 19, 0.85), (145, 17, 0.64),
      (168, 18, 0.80), (190, 15, 0.50)], 0.30, 0.03),
    (-10.0, 190.0, 3.0, 3.6, 4.9, "mid",
     [(-8, 22, 1.20), (16, 20, 1.50), (40, 22, 1.26), (64, 20, 1.62), (88, 22, 1.38), (112, 20, 1.66), (136, 22, 1.34),
      (160, 20, 1.55), (186, 22, 1.25)], 0.85, 0.40),
    (-15.0, 195.0, 4.5, 6.5, 8.2, "far",
     [(-5, 32, 2.15), (35, 30, 2.6), (80, 32, 2.35), (122, 30, 2.7), (165, 32, 2.45), (200, 30, 2.2)], 1.7, 0.76),
]


def hill_height(bumps, low, th_deg):
    """Crest height: round bumps (half ellipses) joined by a soft maximum, never under `low`."""
    total = low ** 4
    for c, w, h in bumps:
        x = (th_deg - c) / w
        if abs(x) < 1.0:
            total += (h * math.sqrt(1.0 - x * x)) ** 4
    return total ** 0.25


def _hill_layer(bm, parts, first, last, step, r_base, r_crest, kind, bumps, low, haze):
    count = int(round((last - first) / step))
    rows = []
    base_z = FLOOR_Z - (0.02 if kind == "near" else 0.35)
    for j in range(count + 1):
        th_deg = first + (last - first) * j / count
        th = math.radians(th_deg)
        h = hill_height(bumps, low, th_deg)
        # The arc's two ends sink into the floor (near layer) or behind the next hills.
        h *= 0.35 + 0.65 * smoothstep(0.0, 14.0, min(th_deg - first, last - th_deg))
        rc = r_crest + 0.18 * math.sin(th * 2.3 + r_base) + 0.25 * (h - low)
        d = Vector((math.cos(th), math.sin(th), 0.0))
        if kind == "far":
            prof = [(r_base, base_z), (r_base + 0.6 * (rc - r_base), FLOOR_Z + 0.82 * h), (rc, FLOOR_Z + h)]
        else:
            prof = [(r_base, base_z), (r_base + 0.32 * (rc - r_base), FLOOR_Z + 0.36 * h),
                    (r_base + 0.66 * (rc - r_base), FLOOR_Z + 0.82 * h), (rc, FLOOR_Z + h),
                    (rc + 0.55, FLOOR_Z + 0.55 * h)]
            if kind == "mid":
                prof = [prof[0], prof[2], prof[3], prof[4]]
        rows.append([bm.verts.new(d * r + Vector((0.0, 0.0, z))) for r, z in prof])
    for a, b in zip(rows, rows[1:]):
        for k in range(len(a) - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    parts.mark("hill", (kind, haze))


def _round_crown(bm, center, radius, seed, segments=8, rings=6):
    rng = random.Random(seed)
    lobes = [Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 1))).normalized() for _ in range(5)]

    def bumps(d):
        return 1.0 + 0.09 * sum(max(0.0, d.dot(l)) ** 6 for l in lobes) - 0.08 * max(0.0, -d.z)

    _sphere(bm, center, Vector((radius, radius, radius * 0.92)), segments, rings, displace=bumps)


def _cast_down(tree, x, y):
    hit = tree.ray_cast(Vector((x, y, 10.0)), Vector((0.0, 0.0, -1.0)))
    return hit[0]


def _small_daisy(bm, parts, base, height, petal, seed, haze):
    """A backdrop daisy: a thin stem, five round petals, a heart (one-sided, seen from above)."""
    rng = random.Random(seed)
    head = base + Vector((0.0, 0.0, height))
    _loft(bm, [base, head], [0.004, 0.0035], [0.004, 0.0035], 3)
    parts.mark("bd_stem", haze)
    turn = rng.uniform(0.0, math.tau)
    up = Vector((0.0, -0.25, 1.0)).normalized()
    u = up.orthogonal().normalized()
    w = up.cross(u).normalized()
    for k in range(5):
        a = turn + k / 5 * math.tau
        d = u * math.cos(a) + w * math.sin(a)
        side = up.cross(d).normalized()
        p0 = head + d * petal * 0.25
        pts = [p0, p0 + d * petal * 0.45 - side * petal * 0.34, p0 + d * petal - side * petal * 0.2,
               p0 + d * petal + side * petal * 0.2, p0 + d * petal * 0.45 + side * petal * 0.34]
        vs = [bm.verts.new(p) for p in pts]
        bm.faces.new((vs[0], vs[1], vs[2]))
        bm.faces.new((vs[0], vs[2], vs[3]))
        bm.faces.new((vs[0], vs[3], vs[4]))
        parts.mark("bd_petal", haze)
    ring = [bm.verts.new(head + (u * math.cos(t) + w * math.sin(t)) * petal * 0.36) for t in (j / 5 * math.tau for j in range(5))]
    apex = bm.verts.new(head + up * petal * 0.22)
    for j in range(5):
        bm.faces.new((apex, ring[j], ring[(j + 1) % 5]))
    parts.mark("bd_heart", haze)


def build_set_backdrop(col):
    """GR_SetBackdrop: the meadow floor around the mound (z = -0.30, a contact shade at the mound's foot) and three
    layers of round Prairie hills in an arc behind it (Blender +Y), warm greens fading to blue-green; round trees,
    and daisies on them. Same origin as GR_SetMound. Regions: base meadow, R sunny crests and daisy hearts, G
    distance haze, B daisy petals, A bark."""
    bm = bmesh.new()
    parts = _Parts(bm)
    _revolve(bm, [(r, FLOOR_Z) for r in FLOOR_RINGS], FLOOR_SEGMENTS)
    parts.mark("floor")
    for layer in HILLS:
        _hill_layer(bm, parts, *layer)
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)

    # Round trees: on the near hills' back slopes (they rise from behind the crest) and on the middle hills, framing
    # the view; none straight behind the animal.
    trees = [(8.0, 3.32, 0.40, 0.36, 0.03, 5101), (26.0, 3.36, 0.34, 0.30, 0.03, 5102), (154.0, 3.36, 0.38, 0.34, 0.03, 5103),
             (172.0, 3.3, 0.33, 0.30, 0.03, 5104), (30.0, 5.2, 0.44, 0.4, 0.40, 5105), (150.0, 5.2, 0.46, 0.42, 0.40, 5106)]
    for th_deg, r, crown, trunk_h, haze, seed in trees:
        th = math.radians(th_deg)
        x, y = math.cos(th) * r, math.sin(th) * r
        ground = _cast_down(tree, x, y)
        if ground is None:
            raise RuntimeError(f"No hill under the tree at {th_deg} degrees")
        # The crown never enters the hill (the crest in front of it, the slope under it): lift the trunk if needed.
        center = ground + Vector((0.0, 0.0, trunk_h + crown * 0.85))
        lift = 0.0
        for j in range(1, 6):
            for k in range(12):
                a, b = math.pi * j / 6, math.tau * k / 12
                p = center + Vector((math.sin(a) * math.cos(b), math.sin(a) * math.sin(b), math.cos(a) * 0.92)) * crown
                hit = tree.ray_cast(p, Vector((0.0, 0.0, 1.0)))
                if hit[0] is not None:
                    lift = max(lift, hit[0].z - p.z)
        trunk_h += lift + (0.015 if lift > 0.0 else 0.0)
        base = ground - Vector((0.0, 0.0, 0.03))
        top = ground + Vector((0.0, 0.0, trunk_h + crown * 0.45))
        far = r > 4.0
        _loft(bm, [base, base + (top - base) * 0.5 + Vector((0.008, 0.0, 0.0)), top], [0.1, 0.075, 0.066],
              [0.1, 0.075, 0.066], 5 if far else 6)
        parts.mark("trunk", haze)
        center = ground + Vector((0.0, 0.0, trunk_h + crown * 0.85))
        _round_crown(bm, center, crown, seed, segments=10 if far else 14, rings=7 if far else 8)
        parts.mark("crown", (haze, center, crown))

    # Daisies on the floor and on the near hills.
    rng = random.Random(5301)
    placed = []
    spots = [(30, 1.45), (66, 1.85), (84, 1.2), (114, 1.95), (128, 1.4), (154, 1.8), (176, 1.3), (82, 2.5), (47, 2.45),
             (150, 2.5)]
    for k, (th_deg, r) in enumerate(spots):
        th = math.radians(th_deg + rng.uniform(-3, 3))
        x, y = math.cos(th) * r, math.sin(th) * r
        ground = _cast_down(tree, x, y)
        if ground is None:
            continue
        haze = 0.04 if r > 2.3 else 0.0
        _small_daisy(bm, parts, ground - Vector((0.0, 0.0, 0.004)), rng.uniform(0.06, 0.09), rng.uniform(0.032, 0.04),
                     5310 + k, haze)
        placed.append(ground)

    def regions(co, n, i):
        tag = parts.tags[i]
        data = parts.data[i]
        if tag == "floor":
            light = smoothstep(0.2, 0.8, wave_noise(Vector((co.x * 0.55, co.y * 0.55, 1.0)), 5401))
            return (0.22 * light, 0.0, 0.0, 0.0)
        if tag == "hill":
            kind, haze = data
            sunny = smoothstep(0.55, 0.95, n.z) * (0.42 if kind == "near" else 0.3 if kind == "mid" else 0.18)
            return (sunny, haze, 0.0, 0.0)
        if tag == "trunk":
            return (0.0, data, 0.0, 1.0 - 0.4 * data)
        if tag == "crown":
            haze, center, radius = data
            d = (co - center).normalized()
            return (0.32 * smoothstep(0.35, 0.95, d.z), haze, 0.0, 0.0)
        if tag == "bd_petal":
            return (0.0, data, 1.0 - data, 0.0)
        if tag == "bd_heart":
            return (1.0, data, 0.0, 0.0)
        return (0.0, data or 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        data = parts.data[i]
        if tag == "floor":
            r = math.hypot(co.x, co.y)
            th = math.atan2(co.y, co.x)
            contact = 0.48 * (1.0 - smoothstep(0.0, 0.22, r - mound_radius(th)))
            return (contact + 0.12 * smoothstep(2.6, 3.1, r) * (1.0 if co.y > 0.0 else 0.0), 0.0)
        if tag == "hill":
            kind, haze = data
            valley = 0.3 * (1.0 - smoothstep(FLOOR_Z, FLOOR_Z + 0.25, co.z)) if kind == "near" else 0.0
            return (valley + 0.12 * smoothstep(0.2, -0.5, n.z), 0.0)
        if tag == "crown":
            haze, center, radius = data
            d = (co - center).normalized()
            return (0.12 + 0.42 * smoothstep(0.25, -0.75, d.z), 0.0)
        if tag == "trunk":
            return (0.3, 0.0)
        return (0.05, 0.0)

    return _finish("GR_SetBackdrop", bm, col, regions, occlusion)


# ---------------------------------------------------------------- build, save, export

def build_grooming():
    """Builds every grooming object in the scene "Grooming" (export collection) and returns their triangle counts.
    Never touches another scene's objects."""
    scene, col = _export_collection()
    out = [build_brush(col), build_debris_grass_blade(col), build_debris_daisy_petal(col),
           build_debris_clover_leaf(col), build_debris_leaf(col), build_debris_twig(col), build_debris_seed(col),
           build_burr(col), build_bow(col), build_soap_bubble(col), build_set_backdrop(col), build_set_mound(col)]
    report = {}
    for obj in out:
        tris = triangle_count(obj)
        budget = next(v for k, v in BUDGETS.items() if obj.name.startswith(k))
        if tris > budget:
            raise RuntimeError(f"{obj.name}: {tris} triangles over its budget of {budget}")
        pts = [Vector(c) for c in obj.bound_box]
        size = tuple(round(max(p[a] for p in pts) - min(p[a] for p in pts), 4) for a in range(3))
        report[obj.name] = (tris, size)
    return report


def _project_root():
    marker = os.sep + "SourceArt" + os.sep
    path = bpy.data.filepath
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ first (or pass the project root).")
    return path.split(marker)[0]


def _clear_previews():
    for obj in bpy.data.scenes[SCENE].objects:
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]


def write_grooming_blend(root=None):
    """Writes the grooming scene and everything it uses to BL_Activity_Grooming.blend (compressed). The open file
    stays as it is."""
    root = root or _project_root()
    _clear_previews()
    path = os.path.join(root, *BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {bpy.data.scenes[SCENE]}, fake_user=True, compress=True)
    return path


def finish_grooming_blend():
    """Run once inside BL_Activity_Grooming.blend: its export collection takes the standard name and the file is
    saved compressed."""
    col = bpy.data.collections.get("GR_EXPORT")
    if col is not None and bpy.data.collections.get("EXPORT") is None:
        col.name = "EXPORT"
    bpy.ops.wm.save_mainfile(compress=True)


def export_grooming():
    """The FBX files, with pw_export.export_static (load pw_export.py in the same namespace first)."""
    _clear_previews()
    done = {}
    for name, prefixes in EXPORTS.items():
        path, objects = export_static(OUT + "/" + name, prefixes)
        done[name] = objects
    return done


# ---------------------------------------------------------------- review images

def _hex_linear(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def paint_preview(obj, palette_key):
    """WORK_Preview (point color) = what SH_EnvironmentOpaque computes from the regions, before lighting."""
    pal = [_hex_linear(h) for h in GROOMING_PALETTES[palette_key]]
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


def _paint_animal(obj, primary, secondary, belly, detail, eye):
    """The animal master's blend order (primary, secondary R, belly G, detail B, eye A), for scale shots."""
    cols = [_hex_linear(h) for h in (primary, secondary, belly, detail, eye)]
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for i, data in enumerate(regions.data):
        r, g, b, a = data.color
        c = cols[0].copy()
        for weight, col in ((r, cols[1]), (g, cols[2]), (b, cols[3]), (a, cols[4])):
            c = c.lerp(col, weight)
        prev.data[i].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev


def _preview_material(alpha=1.0):
    name = "WORK_PreviewMaterial" if alpha >= 1.0 else "WORK_PreviewBubble"
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    attr = nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "WORK_Preview"
    mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.15
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Roughness"].default_value = 0.1
    return mat


def _preview_world(scene):
    world = bpy.data.worlds.get("WORK_PreviewWorld") or bpy.data.worlds.new("WORK_PreviewWorld")
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    coords = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    bg = nt.nodes.new("ShaderNodeBackground")
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(coords.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    nt.links.new(bg.outputs[0], out.inputs[0])
    els = ramp.color_ramp.elements
    els[0].position = 0.0
    els[0].color = tuple(_hex_linear("CFE3E4")) + (1.0,)
    els[1].position = 0.45
    els[1].color = tuple(_hex_linear("7FB2E6")) + (1.0,)
    bg.inputs[1].default_value = 1.0
    scene.world = world
    return world


def render_grooming_previews(out_dir, rabbit_blend=None, size=1.0):
    """Review images in out_dir (T38_props_*.png): the set in the game's 3/4 close-up with the brown rabbit for scale,
    the brush (wood and gold), the debris, burr and bubble, the three bows on the rabbit's head. Everything made here
    is removed afterwards and every object is put back at the origin."""
    scene = bpy.data.scenes[SCENE]
    prev_col = bpy.data.collections.new("WORK_PreviewShots")
    scene.collection.children.link(prev_col)
    temp_objects = []
    material = _preview_material()
    world = _preview_world(scene)
    try:
        engine = "BLENDER_EEVEE"
        scene.render.engine = engine
    except TypeError:
        scene.render.engine = "CYCLES"
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = False
    for flag in [f for f in dir(scene.render) if f.startswith("use_stamp")]:
        setattr(scene.render, flag, False)   # no text written into the review PNGs
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 48
    objs = {o.name: o for o in scene.objects if o.type == 'MESH'}
    keys = {"GR_SetMound": "palette.grooming.set_mound", "GR_SetBackdrop": "palette.grooming.set_backdrop",
            "GR_Brush": "palette.grooming.brush", "GR_Burr": "palette.grooming.burr",
            "GR_Bow": "palette.grooming.bow_pink"}
    for name, obj in objs.items():
        key = keys.get(name, "palette.grooming.debris" if name.startswith("GR_Debris_") else None)
        if key:
            paint_preview(obj, key)
        else:
            prev = obj.data.color_attributes.get("WORK_Preview") or obj.data.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
            for d in prev.data:
                d.color = tuple(_hex_linear("DCEBF7")) + (1.0,)
            obj.data.color_attributes.active_color = prev
        obj.data.materials.clear()
        obj.data.materials.append(material)

    def copy(obj, key, location, rotation=(0.0, 0.0, 0.0), scale=1.0):
        dup = obj.copy()
        dup.data = obj.data.copy()
        prev_col.objects.link(dup)
        dup.location = location
        dup.rotation_euler = rotation
        dup.scale = (scale, scale, scale)
        if key:
            paint_preview(dup, key)
        temp_objects.append(dup)
        return dup

    rabbit = None
    if rabbit_blend:
        with bpy.data.libraries.load(rabbit_blend, link=False) as (src, dst):
            dst.objects = [n for n in src.objects if n == "SKM_Rabbit_LOD0"]
        rabbit = dst.objects[0]
        rabbit.modifiers.clear()
        rabbit.parent = None
        prev_col.objects.link(rabbit)
        _paint_animal(rabbit, "B07A52", "F2B8B5", "F3E3CC", "E98A93", "1E1414")
        rabbit.data.materials.clear()
        rabbit.data.materials.append(material)
        temp_objects.append(rabbit)

    cam_data = bpy.data.cameras.new("WORK_PreviewCamera")
    cam = bpy.data.objects.new("WORK_PreviewCamera", cam_data)
    prev_col.objects.link(cam)
    scene.camera = cam
    sun_data = bpy.data.lights.new("WORK_PreviewSun", 'SUN')
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.95, 0.86)
    sun_data.angle = math.radians(9.0)
    sun = bpy.data.objects.new("WORK_PreviewSun", sun_data)
    prev_col.objects.link(sun)
    temp_objects += [cam, sun]

    def aim(eye, target, lens_fov_deg, width, height):
        cam.location = eye
        direction = target - eye
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        cam_data.sensor_fit = 'VERTICAL'
        cam_data.angle_y = math.radians(lens_fov_deg)
        cam_data.clip_start = 0.01
        cam_data.clip_end = 60.0
        scene.render.resolution_x = int(width * size)
        scene.render.resolution_y = int(height * size)
        # Sun from the top left of the image, a little behind the camera (charte, section 5).
        forward = direction.normalized()
        right = forward.cross(Vector((0.0, 0.0, 1.0))).normalized()
        toward_sun = (-right * 0.75 - forward * 0.45 + Vector((0.0, 0.0, 1.0)) * 1.35).normalized()
        sun.rotation_euler = (-toward_sun).to_track_quat('-Z', 'Y').to_euler()

    def show(names):
        for o in scene.objects:
            if o.type == 'MESH':
                o.hide_render = o.name not in names

    def shot(file_name):
        scene.render.filepath = os.path.join(out_dir, file_name)
        bpy.ops.render.render(write_still=True, scene=scene.name)
        return scene.render.filepath

    files = []
    animal_target = Vector((0.0, 0.04, 0.3))
    set_names = {"GR_SetMound", "GR_SetBackdrop"}
    if rabbit:
        set_names.add(rabbit.name)
    show(set_names)
    for side, az in (("right", 35.0), ("left", -35.0)):
        a = math.radians(az)
        eye = animal_target + Vector((math.sin(a), -math.cos(a), 0.0)) * 2.1 + Vector((0.0, 0.0, 0.3))
        aim(eye, animal_target, 32.0, 540, 1170)
        files.append(shot(f"T38_props_set_{side}.png"))
    aim(Vector((2.2, -5.2, 2.6)), Vector((0.0, 1.6, 0.0)), 40.0, 1200, 800)
    files.append(shot("T38_props_set_wide.png"))

    # Brush (wood and gold), from above and from below.
    brush = objs["GR_Brush"]
    gold = copy(brush, "palette.grooming.brush_gold", (0.09, 0.0, 0.0))
    under = copy(brush, "palette.grooming.brush", (-0.09, 0.0, 0.07), rotation=(math.pi, 0.0, 0.0))
    show({"GR_Brush", gold.name, under.name})
    aim(Vector((0.13, -0.26, 0.2)), Vector((0.0, 0.02, 0.025)), 30.0, 1000, 700)
    files.append(shot("T38_props_brush.png"))

    # Debris, burr and bubble lined up (bubble at 3 cm radius), 4 cm grid.
    lineup = ["GR_Debris_GrassBlade", "GR_Debris_DaisyPetal", "GR_Debris_CloverLeaf", "GR_Debris_Leaf",
              "GR_Debris_Twig", "GR_Debris_Seed", "GR_Burr"]
    for k, name in enumerate(lineup):
        objs[name].location = ((k % 4) * 0.05 - 0.075, (k // 4) * 0.055, 0.0)
    objs["GR_SoapBubble"].location = (0.075, 0.055, 0.016)
    objs["GR_SoapBubble"].scale = (0.016, 0.016, 0.016)
    show(set(lineup) | {"GR_SoapBubble"})
    aim(Vector((0.0, -0.19, 0.19)), Vector((0.012, 0.028, 0.0)), 34.0, 1000, 700)
    files.append(shot("T38_props_debris.png"))
    for name in lineup:
        objs[name].location = (0.0, 0.0, 0.0)
    objs["GR_SoapBubble"].location = (0.0, 0.0, 0.0)
    objs["GR_SoapBubble"].scale = (1.0, 1.0, 1.0)

    # The three bows, then one on the rabbit's head (SOCK_HeadTop 0.438 m) with debris on its back and the brush.
    bow = objs["GR_Bow"]
    sky = copy(bow, "palette.grooming.bow_sky", (0.075, 0.0, 0.0))
    cream = copy(bow, "palette.grooming.bow_cream", (0.15, 0.0, 0.0))
    bow.location = (0.0, 0.0, 0.0)
    show({"GR_Bow", sky.name, cream.name})
    aim(Vector((0.075, -0.2, 0.06)), Vector((0.075, 0.0, 0.016)), 30.0, 1000, 600)
    files.append(shot("T38_props_bows.png"))
    # Silhouettes at 64 px (charte, grid point 1): each prop black in its own 64 px cell, light and dark grounds.
    cells = ["GR_Brush", "GR_Bow", "GR_Burr", "GR_Debris_Leaf", "GR_Debris_CloverLeaf", "GR_Debris_DaisyPetal",
             "GR_Debris_GrassBlade", "GR_Debris_Twig", "GR_Debris_Seed"]
    saved = {}
    for k, name in enumerate(cells):
        o = objs[name]
        saved[name] = (o.location.copy(), o.rotation_euler.copy(), o.scale.copy())
        extent = max(o.dimensions)
        o.scale = (0.08 / extent,) * 3
        o.rotation_euler = (0.0, 0.0, math.pi * 0.5) if name == "GR_Brush" else (0.0, 0.0, 0.0)
        fit = o.rotation_euler.to_matrix() * (0.08 / extent)
        corners = [fit @ Vector(c) for c in o.bound_box]
        middle = Vector([(min(c[a] for c in corners) + max(c[a] for c in corners)) * 0.5 for a in range(3)])
        o.location = Vector((k * 0.1, 0.0, 0.0)) - middle
    show(set(cells))
    previous_engine = scene.render.engine
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = 'FLAT'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.0, 0.0, 0.0)
    cam_data.type = 'ORTHO'
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.ortho_scale = 0.1 * len(cells)
    cam.location = (0.1 * (len(cells) - 1) * 0.5, -0.6, 0.6 - 0.012)
    cam.rotation_euler = (math.radians(45.0), 0.0, 0.0)
    scene.render.resolution_x = 64 * len(cells)
    scene.render.resolution_y = 64
    for ground, color in (("light", "EDEDE6"), ("dark", "2E3440")):
        world.color = tuple(_hex_linear(color))
        world.use_nodes = False
        files.append(shot(f"T38_props_silhouettes_{ground}.png"))
    world.use_nodes = True
    cam_data.type = 'PERSP'
    cam_data.sensor_fit = 'VERTICAL'
    scene.render.engine = previous_engine
    for name, (loc, rot, scl) in saved.items():
        objs[name].location, objs[name].rotation_euler, objs[name].scale = loc, rot, scl

    if rabbit:
        # Props laid ON the fur like the stage will: a ray finds the coat, the pivot goes on it, up = its normal.
        fur_mesh = bmesh.new()
        fur_mesh.from_mesh(rabbit.data)
        fur_mesh.transform(rabbit.matrix_world)
        fur = BVHTree.FromBMesh(fur_mesh)
        fur_mesh.free()

        def on_fur(source, key, x, y, yaw, lift=0.0, from_side=None):
            origin = Vector((x, y, 1.0)) if from_side is None else from_side
            direction = Vector((0.0, 0.0, -1.0)) if from_side is None else (Vector((x, y, 0.3)) - from_side).normalized()
            hit, normal, _, _ = fur.ray_cast(origin, direction)
            dup = copy(source, key, hit + normal * lift)
            dup.rotation_euler = (normal.to_track_quat('Z', 'Y') @ Matrix.Rotation(yaw, 4, 'Z').to_quaternion()).to_euler()
            return dup

        on_head = copy(bow, "palette.grooming.bow_pink", (0.0, -0.075, 0.4384))
        leaf = on_fur(objs["GR_Debris_Leaf"], "palette.grooming.debris", 0.05, 0.1, 0.6)
        petal = on_fur(objs["GR_Debris_DaisyPetal"], "palette.grooming.debris", 0.075, 0.18, 1.9)
        blade = on_fur(objs["GR_Debris_GrassBlade"], "palette.grooming.debris", -0.02, 0.2, -0.4)
        burr = on_fur(objs["GR_Burr"], "palette.grooming.burr", 0.0, 0.0, 0.0, from_side=Vector((0.6, 0.12, 0.22)))
        tool = on_fur(brush, "palette.grooming.brush", -0.03, 0.14, -1.2, lift=0.012)
        show(set_names | {on_head.name, leaf.name, petal.name, blade.name, burr.name, tool.name})
        aim(Vector((1.45, -0.75, 1.2)), Vector((0.0, 0.06, 0.3)), 28.0, 900, 1100)
        files.append(shot("T38_props_family.png"))
    # Clean up: temporary objects, preview colors and materials.
    for o in temp_objects:
        data = o.data
        bpy.data.objects.remove(o)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
            elif isinstance(data, bpy.types.Light):
                bpy.data.lights.remove(data)
    bpy.data.collections.remove(prev_col)
    for obj in objs.values():
        obj.hide_render = False
        obj.data.materials.clear()
    _clear_previews()
    bpy.data.materials.remove(material)
    scene.world = None
    bpy.data.worlds.remove(world)
    return files
