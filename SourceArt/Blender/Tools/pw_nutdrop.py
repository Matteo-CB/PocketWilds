"""T40 Nut Drop (Pluie de noisettes, doc 01 Contenu S-ACT-003): the Prairie basket, the falling items, the hazel tree.

Intention (charte « fait main », section 10, step 1)
- Character: generous, round, harvest-warm. A plump wicker basket, chunky nuts and berries that read at a glance on a
  phone while they fall, and a big round hazel crown hanging over the scene with its nut clusters, in the exact
  family of the Prairie props (pw_grassland, pw_grassland_t33) and the T38 grooming props (pw_grooming).
- Scale, measured read-only on the LOD0 models (rest pose): rabbit SOCK_HeadTop at 0.438 m, its ears rise from 3.5 cm
  behind the socket to 0.648 m; squirrel SOCK_HeadTop 0.432 m, ears 4.6 cm above it; hedgehog SOCK_HeadTop 0.260 m,
  spines 6 cm above it. The basket is 0.31 m wide and 0.165 m deep (the rabbit's head is 0.19 m wide); the falling
  items are oversized on purpose (0.13 to 0.16 m, half the basket) so each one reads while it falls.
- Counted parts, the same in every export: basket 7 woven rows of 7 over-bumps, 2 dyed rows, 1 rolled rim; hazelnut
  7 faint stripes; acorn 1 cup, 1 stem; berries 3 and 1 leaf; pinecone 18 rounded scales in 4 rows (5, 5, 5, 3);
  feather 1 vane, 1 shaft; hazel tree 14 crown lobes, 1 trunk, 1 stem and 1 branch, 8 nut clusters (20 nuts).
- Wanted imperfections: the weave bumps alternate row by row; the feather bows and leans; the crown is a union of
  uneven lobes; leaves and clusters are laid by fixed seeds.
- Nothing spiky: the pinecone scales are rounded lenses, the acorn and hazelnut tips are soft nubs, the hazel husks
  are round-lobed collars, the feather has no split barbs.
- Budgets (triangles): basket 900; each falling item 300; hazel canopy + trunk 4,000.

Data contract: as pw_grassland.py (one palette row per renderer, SH_EnvironmentOpaque): color attribute PW_Regions
(R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion, y height
factor). ND_PALETTES holds the proposed rows (base, R, G, B, A, occlusion, accent); the previews use them.

Axes: pw_export.py writes Blender (x, y, z) as Unity (-x, z, -y). The animal faces Blender -Y (Unity +Z) toward the
set camera. Basket: pivot at the bottom center (the foot ring rests on z = 0, the underside is dished 1 cm). Items:
pivot at the center of their bounds. Hazel tree: both objects share the pivot at the base of the trunk on the ground;
the trunk stands at the left of the screen (Blender -X), the crown reaches over the playfield toward +X.

Usage inside Blender (the live session or a fresh file), after pw_grassland.py in the same namespace:
    ns["build_nutdrop"]()            # scene "NutDrop", every object in its export collection, budgets checked
    ns["write_nutdrop_blend"]()      # bpy.data.libraries.write: the scene to SourceArt/Blender/Activities/
Then in BL_Activity_NutDrop.blend (headless is fine), after pw_export.py in the same namespace:
    ns["finish_nutdrop_blend"]()     # once: the export collection takes the standard name EXPORT, file saved
    ns["export_nutdrop"]()           # ND_Props.fbx and ND_HazelTree.fbx in Assets/_Game/Art/Activities/NutDrop/
    ns["measure_canopy"](canopy, trunk)            # the underside over the playfield corridor, its clearances
    ns["render_nutdrop_previews"](out_dir, refs, fits)   # review images; temporary camera, light, materials
In BL_Animal_<Species>.blend (headless, read only, nothing saved), after pw_grassland.py and this file:
    ns["fit_basket"](species)        # how far the basket must stand off SOCK_HeadTop to clear the head and ears
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ND_SCENE = "NutDrop"
ND_COLLECTION = "ND_EXPORT"         # EXPORT is taken in the Prairie file; renamed EXPORT in the activity's own file
ND_WORK = "ND_WORK"
ND_BLEND = "SourceArt/Blender/Activities/BL_Activity_NutDrop.blend"
ND_OUT = "Assets/_Game/Art/Activities/NutDrop"
ND_EXPORTS = {
    "ND_Props.fbx": ("ND_Basket", "ND_Hazelnut", "ND_Acorn", "ND_Berry", "ND_GoldenNut", "ND_Pinecone", "ND_Feather"),
    "ND_HazelTree.fbx": ("ND_HazelCanopy", "ND_HazelTrunk"),
}
ND_BUDGETS = {"ND_Basket": 900, "ND_Hazelnut": 300, "ND_Acorn": 300, "ND_Berry": 300, "ND_GoldenNut": 300,
              "ND_Pinecone": 300, "ND_Feather": 300, "ND_Hazel": 4000}

# Proposed palette rows (base, R, G, B, A, occlusion, accent), sRGB hex like DEF_Palettes_Grassland.
ND_PALETTES = {
    # wicker honey; R the over-bumps of the weave; G the rolled rim; B the inside; A the dyed rows.
    "palette.nut_drop.basket": ("B97A3C", "F0C987", "E2AE68", "A06C3A", "D9786A", "5A3B33", "FFFFFF"),
    # shell chestnut; R the pale cap; G the faint stripes.
    "palette.nut_drop.hazelnut": ("B0703F", "E8CFA2", "93582F", "B0703F", "B0703F", "5E3B2E", "FFFFFF"),
    # nut tan; R the cup; G the cup's ridges and lip; B the stem; A the soft tip.
    "palette.nut_drop.acorn": ("C48D50", "8E6B4A", "B39370", "6E4E38", "9C7A3E", "5A4234", "FFFFFF"),
    # berry red; R the calyx dot; G the stems; B the leaf; A the warm cheek of each berry.
    "palette.nut_drop.berry": ("D8453F", "5A2E2C", "8A8A4E", "6FAE57", "E9705A", "7A2A3A", "FFFFFF"),
    # gold; R the pale cap; G the soft highlight; B the faint stripes and the deeper gold underneath.
    "palette.nut_drop.golden_nut": ("D99A2B", "F0CB66", "FFE9A6", "B06A1C", "D99A2B", "86552A", "FFFFFF"),
    # scales chestnut; R the lighter scale rims; G the dark core between scales; B the stem.
    "palette.nut_drop.pinecone": ("A8714A", "D2A372", "5E4030", "7A5A3E", "A8714A", "4F3427", "FFFFFF"),
    # warm white; R the cream down at the base; G the shaft; B the quill; A the cream tip.
    "palette.nut_drop.feather": ("FFF6E6", "EDCF9E", "E2C08C", "C9A06C", "F0D8B4", "9E93AE", "FFFFFF"),
    # nut shell (base); R foliage; G sunlit; B shaded; A the husks (palette.grassland.tree greens).
    "palette.nut_drop.hazel_canopy": ("A86A3C", "7DB960", "A9D57C", "578F4B", "C9DC86", "3B6338", "FFFFFF"),
    # bark; R the pale lenticel bands; G moss at the foot; A grooves.
    "palette.nut_drop.hazel_trunk": ("8F6A52", "B3967C", "8DAA5C", "8F6A52", "6E4C3A", "4E3B38", "FFFFFF"),
}

UP = Vector((0.0, 0.0, 1.0))


# ---------------------------------------------------------------- scene

def _nd_scene():
    """The NutDrop scene, its export collection and a work collection; the scene is shown in the window so its
    depsgraph evaluates the crown. No other scene's objects are touched."""
    scene = bpy.data.scenes.get(ND_SCENE) or bpy.data.scenes.new(ND_SCENE)
    cols = []
    for name in (ND_COLLECTION, ND_WORK):
        col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if col.name not in scene.collection.children:
            scene.collection.children.link(col)
        cols.append(col)
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    return scene, cols[0], cols[1]


def _nd_replace(name):
    old = bpy.data.objects.get(name)
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


class _NdParts:
    """Which part each vertex belongs to while a bmesh is built (to_mesh keeps the vertex order)."""

    def __init__(self, bm):
        self.bm = bm
        self.tags = []
        self.data = []

    def mark(self, tag, data=None):
        n = len(self.bm.verts) - len(self.tags)
        self.tags.extend([tag] * n)
        self.data.extend([data] * n)


# ---------------------------------------------------------------- shape helpers

def _nd_revolve(bm, profile, segments, top=None, bottom=None, radius=None, phase=0.0):
    """Surface around Z from a profile [(r, z), ...]; `top` / `bottom` close the first / last ring with a center vertex
    at that height. radius(i, theta) scales ring i. Winding follows the profile; _nd_finish fixes the normals."""
    rings = []
    for i, (r, z) in enumerate(profile):
        ring = []
        for k in range(segments):
            th = phase + math.tau * k / segments
            f = radius(i, th) if radius else 1.0
            ring.append(bm.verts.new((math.cos(th) * r * f, math.sin(th) * r * f, z)))
        rings.append(ring)
    if top is not None:
        apex = bm.verts.new((0.0, 0.0, top))
        for k in range(segments):
            bm.faces.new((apex, rings[0][k], rings[0][(k + 1) % segments]))
    for a, b in zip(rings, rings[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            bm.faces.new((a[k], b[k], b[m], a[m]))
    if bottom is not None:
        apex = bm.verts.new((0.0, 0.0, bottom))
        for k in range(segments):
            bm.faces.new((apex, rings[-1][(k + 1) % segments], rings[-1][k]))
    return rings


def _nd_sphere(bm, center, radii, segments, rings, rotation=None, displace=None):
    """UV ellipsoid (poles on local Z), turned by `rotation` (3x3); displace(direction) scales the radius."""
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


def _nd_loft(bm, centers, half_side, half_up, sides, up=UP, start=None, end=None, shape=None):
    """Tube through `centers`: rings of `sides` vertices, an ellipse half_side wide along the side axis and half_up
    along the up axis; start / end apex points close the ends (None leaves the end open inside another part)."""
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
            a = k / sides * math.tau
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


def _nd_lens(bm, center, outline, top_lift, bottom_drop, normal):
    """A flat rounded piece: outline points around `normal`, a raised center on top and a lowered one underneath."""
    ring = [bm.verts.new(p) for p in outline]
    top = bm.verts.new(center + normal * top_lift)
    bottom = bm.verts.new(center - normal * bottom_drop)
    n = len(ring)
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((top, ring[k], ring[m]))
        bm.faces.new((bottom, ring[m], ring[k]))
    return ring, top, bottom


def _nd_center(bm):
    """Moves the piece so the center of its bounds is the origin; returns the offset."""
    lo = [min(v.co[a] for v in bm.verts) for a in range(3)]
    hi = [max(v.co[a] for v in bm.verts) for a in range(3)]
    offset = Vector([-(lo[a] + hi[a]) * 0.5 for a in range(3)])
    for v in bm.verts:
        v.co += offset
    return offset


def _nd_outward(bm, center=None):
    """Recomputes face normals so every closed part faces out."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()


def _nd_finish(name, bm, col, regions, occlusion, recalc=True):
    if recalc:
        _nd_outward(bm)
    bm.normal_update()
    _nd_replace(name)
    obj = object_from_bmesh(name, bm, [col], smooth=True)
    write_layers(obj, regions, occlusion)
    return obj


# ---------------------------------------------------------------- basket

BASKET_SEGMENTS = 28
BASKET_WEAVE = 7                   # over-bumps per row
# (r, z) along the wall: inner floor, inner wall, rolled rim, four woven rows (bulge, crease), foot, dished underside.
BASKET_PROFILE = [
    (0.074, 0.028),                # 0 inner floor
    (0.098, 0.036),                # 1 inner wall foot
    (0.121, 0.092),                # 2 inner wall
    (0.134, 0.146),                # 3 inner rim
    (0.147, 0.164),                # 4 rim top
    (0.158, 0.151),                # 5 rim outside
    (0.150, 0.135),                # 6 rim under (crease)
    (0.152, 0.120),                # 7 to 13: seven woven rows, one ring each, a smooth bowl
    (0.148, 0.103),
    (0.143, 0.086),                # 9, 10: the dyed rows
    (0.138, 0.069),
    (0.132, 0.052),
    (0.125, 0.035),
    (0.117, 0.019),
    (0.105, 0.0),                  # 14 foot ring (rests on z = 0)
    (0.070, 0.006),                # 15 underside
]
BASKET_FLOOR = 0.028
BASKET_UNDER = 0.010
BASKET_ROWS = {i: i - 7 for i in range(7, 14)}
BASKET_DYED = (9, 10)
BASKET_RIM = (3, 4, 5, 6)


def _basket_weave(i, th):
    row = BASKET_ROWS.get(i)
    if row is None:
        return 1.0
    return 1.0 + 0.036 * math.cos(BASKET_WEAVE * th + row * math.pi)


def build_basket(col):
    """ND_Basket: a round wicker basket 0.32 m wide, 0.165 m deep, no handle: a smooth bowl woven in seven rows whose
    soft over-bumps alternate row by row (the basket weave), two dyed rows in the middle, a plump rolled rim, a smooth
    inside (floor at 2.8 cm) seen from the 3/4 high camera. Pivot at the bottom center: the foot ring rests on z = 0, the underside is dished (1 cm) so it sits on
    a round head. Regions: base wicker, R over-bumps, G rim, B inside, A the dyed rows."""
    bm = bmesh.new()
    _nd_revolve(bm, BASKET_PROFILE, BASKET_SEGMENTS, top=BASKET_FLOOR, bottom=BASKET_UNDER, radius=_basket_weave)
    n_profile = len(BASKET_PROFILE) * BASKET_SEGMENTS

    def ring_index(i):
        # Vertices keep their creation order: the rings, then the floor center, then the underside center.
        if i == n_profile:
            return 0
        if i == n_profile + 1:
            return len(BASKET_PROFILE) - 1
        return i // BASKET_SEGMENTS

    def regions(co, n, i):
        r = ring_index(i)
        th = math.atan2(co.y, co.x)
        if r <= 2:
            return (0.0, 0.0, 1.0, 0.0)
        if r in BASKET_RIM:
            # A rolled rim, twisted like a rope: a lighter twist travels around it.
            twist = 0.5 + 0.5 * math.cos(BASKET_WEAVE * 2 * th + (r - 3) * 1.4)
            inside = 0.6 if r == 3 else 0.0
            return (1.0, 1.0 - 0.6 * twist, inside, 0.0)
        row = BASKET_ROWS.get(r)
        if row is not None:
            # Over-bumps lighter, under-gaps darker (occlusion): a staggered weave; in the dyed row the dyed strand
            # goes over and the natural one under.
            over = max(0.0, math.cos(BASKET_WEAVE * th + row * math.pi))
            return (smoothstep(0.0, 0.7, over), 0.0, 0.0, 0.85 if r in BASKET_DYED else 0.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        r = ring_index(i)
        if r <= 2:
            # Deeper toward the floor; the floor's edge darkest.
            depth = 1.0 - smoothstep(BASKET_FLOOR, 0.15, co.z)
            corner = 0.15 if r == 1 else 0.0
            return (0.18 + 0.3 * depth + corner, co.z / 0.165)
        if r == 6:
            return (0.5, co.z / 0.165)
        row = BASKET_ROWS.get(r)
        if row is not None:
            under = max(0.0, -math.cos(BASKET_WEAVE * math.atan2(co.y, co.x) + row * math.pi))
            return (0.05 + 0.42 * under, co.z / 0.165)
        if r in (14, 15):
            return (0.5, co.z / 0.165)
        if r == 3:
            return (0.12, co.z / 0.165)
        return (0.04, co.z / 0.165)

    return _nd_finish("ND_Basket", bm, col, regions, occlusion)


# ---------------------------------------------------------------- hazelnut and golden nut

# (r, z) from the pale cap down to the soft tip.
HAZEL_PROFILE = [(0.024, 0.0565), (0.041, 0.0540), (0.0525, 0.0490), (0.0585, 0.0395), (0.0645, 0.0200),
                 (0.0660, 0.0), (0.0625, -0.0215), (0.0525, -0.0415), (0.0365, -0.0560), (0.0170, -0.0660)]
HAZEL_TOP = 0.0575
HAZEL_TIP = -0.0715
HAZEL_SEGMENTS = 14
HAZEL_STRIPES = 7


def _nut_body(bm):
    """The hazelnut body; returns each vertex's ring (creation order: the rings, the cap center, the tip)."""
    _nd_revolve(bm, HAZEL_PROFILE, HAZEL_SEGMENTS, top=HAZEL_TOP, bottom=HAZEL_TIP)
    n = len(HAZEL_PROFILE) * HAZEL_SEGMENTS
    ring_of = {i: i // HAZEL_SEGMENTS for i in range(n)}
    ring_of[n] = -1                      # cap center
    ring_of[n + 1] = len(HAZEL_PROFILE)  # tip
    return ring_of


def build_hazelnut(col, golden=False):
    """ND_Hazelnut: a plump round hazelnut 0.13 m wide, its pale flat cap on top, a soft rounded tip below, seven
    faint darker stripes running down the shell. Pivot at the center. Regions: base shell, R cap, G stripes.
    ND_GoldenNut (golden=True): the same shape; base gold, R cap, G a soft highlight on the top left of the front,
    B the stripes and a deeper gold underneath."""
    bm = bmesh.new()
    ring_of = _nut_body(bm)
    offset = _nd_center(bm)
    light = Vector((-0.55, -0.55, 0.62)).normalized()

    def regions(co, n, i):
        r = ring_of[i]
        cap = 1.0 if r <= 1 else (0.55 if r == 2 else 0.0)
        th = math.atan2(co.y, co.x)
        stripe = 0.0
        if 3 <= r <= 8:
            stripe = 0.6 * max(0.0, math.cos(HAZEL_STRIPES * th)) ** 2 * (1.0 - smoothstep(6.0, 8.5, float(r)))
        if not golden:
            return (cap, stripe, 0.0, 0.0)
        d = (co - offset).normalized()
        shine = smoothstep(0.78, 0.97, d.dot(light)) * (1.0 - cap)
        warm = 0.5 * smoothstep(-0.2, -0.8, d.z)
        return (cap, shine, max(stripe * 0.8, warm), 0.0)

    def occlusion(co, n, i):
        r = ring_of[i]
        rim = 0.22 if r == 2 else 0.0
        return (rim, 0.0)

    return _nd_finish("ND_GoldenNut" if golden else "ND_Hazelnut", bm, col, regions, occlusion)


# ---------------------------------------------------------------- acorn

ACORN_NUT = [(0.0455, 0.020), (0.0478, -0.006), (0.0428, -0.034), (0.0290, -0.057), (0.0105, -0.0725)]
ACORN_TIP = -0.0785
# The cup from the stem down: two soft ridges, a groove between them, a thick rolled lip, its inside hidden by the nut.
ACORN_CUP = [(0.022, 0.0625), (0.0405, 0.0548), (0.0482, 0.0438), (0.0568, 0.0305), (0.0562, 0.0162),
             (0.0505, 0.0075), (0.0430, 0.0160)]
ACORN_CUP_TOP = 0.0655
ACORN_SEGMENTS = 12


def build_acorn(col):
    """ND_Acorn: an oval tan nut 0.16 m tall under a deep cupped cap (two soft ridges, a thick rounded lip), a short
    stub stem on top, a soft nub at the tip. Pivot at the center. Regions: base nut, R cup, G the cup's ridges and lip,
    B stem, A the tip."""
    bm = bmesh.new()
    parts = _NdParts(bm)
    _nd_revolve(bm, ACORN_NUT, ACORN_SEGMENTS, bottom=ACORN_TIP, phase=math.pi / ACORN_SEGMENTS)
    parts.mark("nut")
    _nd_revolve(bm, ACORN_CUP, ACORN_SEGMENTS, top=ACORN_CUP_TOP)
    parts.mark("cup")
    _nd_loft(bm, [Vector((0.0, 0.0, 0.060)), Vector((0.0015, 0.0, 0.0705)), Vector((0.0035, 0.0, 0.0775))],
             [0.0092, 0.0084, 0.0078], [0.0092, 0.0084, 0.0078], 5, up=Vector((1.0, 0.0, 0.0)),
             end=Vector((0.0048, 0.0, 0.0820)))
    parts.mark("stem")
    _nd_outward(bm)
    _nd_center(bm)
    nut_count = len(ACORN_NUT) * ACORN_SEGMENTS + 1
    cup_top = nut_count + len(ACORN_CUP) * ACORN_SEGMENTS

    def cup_ring(i):
        return -1 if i == cup_top else (i - nut_count) // ACORN_SEGMENTS

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "stem":
            return (0.0, 0.0, 1.0, 0.0)
        if tag == "cup":
            light = {1: 0.55, 3: 0.6, 4: 0.85, 5: 0.35}.get(cup_ring(i), 0.0)
            return (1.0, light, 0.0, 0.0)
        ring = i // ACORN_SEGMENTS
        tip = 1.0 if ring >= 4 or i >= nut_count - 1 else 0.0
        return (0.0, 0.0, 0.0, tip)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        if tag == "nut":
            ring = i // ACORN_SEGMENTS
            return (0.45 if ring == 0 else (0.1 if ring == 1 else 0.0), 0.0)
        if tag == "cup":
            return ({-1: 0.12, 0: 0.08, 2: 0.3, 6: 0.4}.get(cup_ring(i), 0.0), 0.0)
        return (0.15, 0.0)

    return _nd_finish("ND_Acorn", bm, col, regions, occlusion, recalc=False)


# ---------------------------------------------------------------- berries

BERRIES = [(Vector((-0.0345, 0.0, -0.0215)), 0.0375), (Vector((0.0350, 0.006, -0.0255)), 0.0370),
           (Vector((0.0005, -0.010, 0.0245)), 0.0355)]
BERRY_JOINT = Vector((0.0020, 0.004, 0.0705))


def build_berry(col):
    """ND_Berry: three round red hedge berries (hawthorn-like, 7 to 7.5 cm each) on a tiny forked stem with one small
    round leaf, 0.15 m across. Each berry shows a dark calyx dot opposite its stem and shades as a true sphere (its
    normals point from its center). Pivot at the center. Regions: base red, R calyx, G stems, B leaf, A the warm lower
    cheek of each berry."""
    bm = bmesh.new()
    parts = _NdParts(bm)
    for k, (c, r) in enumerate(BERRIES):
        axis = (c - BERRY_JOINT).normalized()            # local -Z (the calyx pole) points away from the stem
        rot = Vector((0.0, 0.0, -1.0)).rotation_difference(axis).to_matrix()
        _nd_sphere(bm, c, Vector((r, r, r * 0.96)), 8, 6, rotation=rot)
        parts.mark("berry", (c, r, axis))
    for c, r in BERRIES:
        d = (BERRY_JOINT - c).normalized()
        a = c + d * (r * 0.75)
        _nd_loft(bm, [a, BERRY_JOINT + (a - BERRY_JOINT) * 0.08], [0.0034, 0.0030], [0.0034, 0.0030], 3,
                 up=Vector((0.0, 1.0, 0.0)))
        parts.mark("stem")
    _nd_loft(bm, [BERRY_JOINT - Vector((0.0, 0.0, 0.004)), BERRY_JOINT + Vector((0.004, 0.0, 0.010))],
             [0.0042, 0.0036], [0.0042, 0.0036], 3, up=Vector((0.0, 1.0, 0.0)),
             end=BERRY_JOINT + Vector((0.0055, 0.0, 0.0145)))
    parts.mark("stem")
    # One small round leaf leaning back and to the left from the joint: an oval with a soft rounded point.
    base = BERRY_JOINT + Vector((-0.002, 0.004, 0.002))
    along = Vector((-0.72, 0.30, 0.62)).normalized()
    normal = Vector((0.15, -0.85, 0.5)).normalized()
    normal = (normal - along * normal.dot(along)).normalized()
    side = normal.cross(along).normalized()
    length, width = 0.044, 0.027
    center = base + along * (length * 0.5)
    outline = []
    for j in range(8):
        t = j / 8 * math.tau
        tip = 1.0 + 0.1 * max(0.0, math.cos(t)) ** 4
        outline.append(center + along * (math.cos(t) * length * 0.5 * tip) + side * (math.sin(t) * width * 0.5)
                       + normal * (0.003 * math.cos(t) ** 2))
    if (outline[1] - center).cross(outline[2] - center).dot(normal) < 0.0:
        outline.reverse()
    _nd_lens(bm, center, outline, 0.0035, 0.0022, normal)
    parts.mark("leaf")
    _nd_outward(bm)
    offset = _nd_center(bm)

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "stem":
            return (0.0, 1.0, 0.0, 0.0)
        if tag == "leaf":
            return (0.0, 0.0, 1.0, 0.0)
        c, r, axis = parts.data[i]
        d = (co - offset - c).normalized()
        calyx = smoothstep(0.93, 0.995, d.dot(axis))
        cheek = 0.55 * smoothstep(0.1, 0.8, d.dot(axis)) * (1.0 - calyx)
        return (calyx, 0.0, 0.0, cheek)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        if tag == "berry":
            c, r, axis = parts.data[i]
            p = co - offset
            near = 0.0
            for c2, r2 in BERRIES:
                if c2 is c:
                    continue
                near = max(near, 1.0 - smoothstep(0.0, 0.012, (p - c2).length - r2))
            stem_side = smoothstep(0.75, 0.98, (p - c).normalized().dot(-axis))
            return (0.4 * near + 0.25 * stem_side, 0.0)
        if tag == "leaf":
            return (0.15, 0.0)
        return (0.1, 0.0)

    obj = _nd_finish("ND_Berry", bm, col, regions, occlusion, recalc=False)
    normals = []
    for v in obj.data.vertices:
        if parts.tags[v.index] == "berry":
            c = parts.data[v.index][0]
            normals.append((v.co - offset - c).normalized())
        else:
            normals.append(v.normal.copy())
    obj.data.normals_split_custom_set_from_vertices(normals)
    return obj


# ---------------------------------------------------------------- pinecone

PINE_CORE = (Vector((0.0, 0.0, -0.007)), Vector((0.047, 0.047, 0.064)))
# rows from the stem down: (height on the core, scales, width factor, scale height)
PINE_ROWS = [(0.038, 5, 1.42, 0.046), (0.009, 5, 1.42, 0.048), (-0.023, 5, 1.42, 0.044), (-0.049, 3, 1.55, 0.040)]
PINE_POINTS = [(0.0, -0.50), (0.50, -0.02), (0.38, 0.32), (0.14, 0.50), (-0.14, 0.50), (-0.38, 0.32), (-0.50, -0.02)]


def _core_surface(p):
    """The point of the pinecone's core under p (along the line from the core's center) and the core normal there."""
    c, radii = PINE_CORE
    d = p - c
    q = Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z))
    s = c + d / q.length
    e = s - c
    nrm = Vector((e.x / radii.x ** 2, e.y / radii.y ** 2, e.z / radii.z ** 2)).normalized()
    return s, nrm


def build_pinecone(col):
    """ND_Pinecone: a round chunky pinecone 0.11 m wide, 0.15 m tall: eighteen rounded scales in four offset rows
    (5, 5, 5, 3), each a plump lens that follows the core's curve and lifts its round lower lip like a roof shingle,
    tucked under the row above (no thin edge stands off the core); a dark core shows between them; a short stub stem.
    Pivot at the center. Regions: base scales, R lighter scale lips, G core, B stem."""
    bm = bmesh.new()
    parts = _NdParts(bm)
    c, radii = PINE_CORE
    _nd_sphere(bm, c, radii, 6, 3)
    parts.mark("core")
    for row, (z, count, wfac, height) in enumerate(PINE_ROWS):
        r_core = radii.x * math.sqrt(max(0.0, 1.0 - ((z - c.z) / radii.z) ** 2))
        for k in range(count):
            th = math.tau * k / count + (row % 2) * math.pi / count + 0.15 * row
            radial = Vector((math.cos(th), math.sin(th), 0.0))
            mid, normal = _core_surface(Vector((radial.x * r_core, radial.y * r_core, z)) + radial * 0.001)
            down = (Vector((0.0, 0.0, -1.0)) - normal * -normal.z).normalized()   # along the surface, toward the lip
            side = normal.cross(down).normalized()
            w = math.tau * r_core / count * 0.5 * wfac
            outline = []
            for a, b in PINE_POINTS:
                flat = mid + side * (a * 2.0 * w) + down * (b * height)
                s, sn = _core_surface(flat)
                lip = smoothstep(-0.1, 0.45, b)                 # the lower lip stands off the core, the top hugs it
                outline.append(s + sn * (0.0015 + 0.0065 * lip))
            centre = mid + down * (height * 0.1)
            cs, cn = _core_surface(centre)
            if (outline[1] - cs).cross(outline[2] - cs).dot(cn) < 0.0:
                outline.reverse()
            ring = [bm.verts.new(p) for p in outline]
            top = bm.verts.new(cs + cn * 0.0105)
            bottom = bm.verts.new(cs - cn * 0.004)
            for j in range(len(ring)):
                m = (j + 1) % len(ring)
                bm.faces.new((top, ring[j], ring[m]))
                bm.faces.new((bottom, ring[m], ring[j]))
            parts.mark("scale", (cs, cn, down, height))
    _nd_loft(bm, [Vector((0.0, 0.0, 0.046)), Vector((0.002, 0.0, 0.063)), Vector((0.005, 0.0, 0.070))],
             [0.0092, 0.0084, 0.0078], [0.0092, 0.0084, 0.0078], 4, up=Vector((1.0, 0.0, 0.0)),
             end=Vector((0.0068, 0.0, 0.0745)))
    parts.mark("stem")
    _nd_outward(bm)
    offset = _nd_center(bm)

    def along(co, i):
        cs, cn, down, height = parts.data[i]
        return (co - offset - cs).dot(down) / height

    def regions(co, n, i):
        tag = parts.tags[i]
        if tag == "core":
            return (0.0, 1.0, 0.0, 0.0)
        if tag == "stem":
            return (0.0, 0.0, 1.0, 0.0)
        return (smoothstep(0.05, 0.4, along(co, i)), 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        tag = parts.tags[i]
        if tag == "core":
            return (0.55, 0.0)
        if tag == "stem":
            return (0.2, 0.0)
        return (0.42 * (1.0 - smoothstep(-0.4, 0.05, along(co, i))), 0.0)

    obj = _nd_finish("ND_Pinecone", bm, col, regions, occlusion, recalc=False)
    # Each scale shades like a small pillow: normals lean from its own normal toward its outline.
    normals = []
    for v in obj.data.vertices:
        if parts.tags[v.index] == "scale":
            cs, cn, down, height = parts.data[v.index]
            radial = v.co - offset - cs
            radial -= cn * radial.dot(cn)
            normals.append((cn + radial * 22.0).normalized() if radial.length > 1e-5 else cn.copy())
        else:
            normals.append(v.normal.copy())
    obj.data.normals_split_custom_set_from_vertices(normals)
    return obj


# ---------------------------------------------------------------- feather

# Stations along the shaft (z, half width on the left, half width on the right): a downy base with two soft lobes a
# side, the widest point a third of the way up, a soft notch on the left, a rounded tip.
FEATHER = [(-0.066, 0.008, 0.007), (-0.058, 0.021, 0.017), (-0.050, 0.016, 0.013), (-0.042, 0.028, 0.022),
           (-0.032, 0.034, 0.026), (-0.018, 0.040, 0.030), (-0.002, 0.042, 0.031), (0.014, 0.041, 0.030),
           (0.026, 0.031, 0.029), (0.036, 0.038, 0.027), (0.050, 0.034, 0.024), (0.063, 0.027, 0.019),
           (0.073, 0.019, 0.013), (0.079, 0.010, 0.007)]
FEATHER_TIP = 0.0835
FEATHER_QUILL = -0.086


def _feather_axis(z):
    """The shaft: bowed toward the back (Blender +Y) and curling a little to the right toward the tip."""
    t = (z - FEATHER_QUILL) / (FEATHER_TIP - FEATHER_QUILL)
    return Vector((0.010 * t ** 3, 0.013 * math.sin(math.pi * t) - 0.004 * t, z))


def build_feather(col):
    """ND_Feather: a soft round feather 0.17 m long facing the camera (Blender -Y): a wide vane, broader on the left of
    the shaft, with two soft downy lobes a side at its base, one soft rounded notch on the left, a rounded tip curling
    right, bowed backward; a cream shaft running from inside the vane to a short rounded quill. Pivot at the center.
    Regions: base warm white, R cream down, G shaft, B quill, A the cream tip; the vane's rim shades a little cooler
    (occlusion)."""
    bm = bmesh.new()
    parts = _NdParts(bm)
    centers, half_w, thick = [], [], []
    for z, left, right in FEATHER:
        a = _feather_axis(z)
        centers.append(a + Vector(((right - left) * 0.5, 0.0, 0.0)))   # the vane's middle sits left of the shaft
        half_w.append((left + right) * 0.5)
        t = (z - FEATHER_QUILL) / (FEATHER_TIP - FEATHER_QUILL)
        thick.append(0.0042 - 0.0018 * t)
    _nd_loft(bm, centers, half_w, thick, 8, up=Vector((0.0, -1.0, 0.0)),
             start=_feather_axis(FEATHER[0][0] - 0.004), end=_feather_axis(FEATHER_TIP) + Vector((0.002, 0.0, 0.0)))
    parts.mark("vane")
    shaft = [_feather_axis(z) + Vector((0.0, -0.0008, 0.0)) for z in (FEATHER_QUILL + 0.004, -0.072, -0.055, 0.0, 0.06)]
    _nd_loft(bm, shaft, [0.0042, 0.0042, 0.0036, 0.0028, 0.0016], [0.0042, 0.0042, 0.0048, 0.0042, 0.0016], 4,
             up=Vector((0.0, -1.0, 0.0)), start=_feather_axis(FEATHER_QUILL) + Vector((0.0, -0.0008, 0.0)))
    parts.mark("shaft")
    _nd_outward(bm)
    offset = _nd_center(bm)
    n_vane = len(FEATHER) * 8

    def regions(co, n_, i):
        p = co - offset
        if parts.tags[i] == "shaft":
            quill = 1.0 - smoothstep(-0.072, -0.062, p.z)
            return (0.0, 1.0, quill, 0.0)
        down = 1.0 - smoothstep(-0.04, 0.02, p.z)
        tip = 0.7 * smoothstep(0.045, 0.08, p.z)
        return (0.95 * down, 0.0, 0.0, tip)

    def occlusion(co, n_, i):
        p = co - offset
        if parts.tags[i] == "shaft":
            return (0.05, 0.0)
        rim = 0.24 if i < n_vane and i % 8 in (0, 4) else 0.0
        groove = 0.12 * (1.0 - smoothstep(0.0, 0.01, abs(p.x - _feather_axis(p.z).x)))
        return (rim + groove + 0.18 * (1.0 - smoothstep(-0.07, -0.05, p.z)), 0.0)

    return _nd_finish("ND_Feather", bm, col, regions, occlusion, recalc=False)


# ---------------------------------------------------------------- hazel tree

# Crown lobes (center, radius), tree-local: the trunk's base is the origin, the crown reaches over +X. Three tiers:
# the upper dome, a middle ring, and a lower front fringe (seen from the camera below and in front) whose lobes hang
# a little lower, so the crown reads as layered rounded tiers.
HAZEL_LIFT = 0.32                    # the whole crown stands this much higher than the first sketch: underside 2.5 m
HAZEL_LOBES = [(c + Vector((0.0, 0.0, HAZEL_LIFT)), r) for c, r in [
    (Vector((0.95, 0.25, 3.25)), 0.78), (Vector((0.05, 0.20, 3.05)), 0.60), (Vector((1.90, 0.25, 3.05)), 0.62),
    (Vector((1.00, 0.45, 3.62)), 0.50),
    (Vector((-0.45, 0.05, 2.85)), 0.42), (Vector((2.45, 0.05, 2.85)), 0.44), (Vector((0.50, 0.75, 2.95)), 0.50),
    (Vector((1.50, 0.80, 2.95)), 0.50),
    (Vector((0.25, -0.40, 2.72)), 0.44), (Vector((0.95, -0.50, 2.64)), 0.47), (Vector((1.65, -0.45, 2.68)), 0.45),
    (Vector((2.25, -0.30, 2.74)), 0.40),
    (Vector((1.00, 0.40, 2.78)), 0.50), (Vector((0.42, 0.10, 2.74)), 0.42),
]]
HAZEL_SQUASH = 0.86
HAZEL_CROWN_TRIS = 2000
HAZEL_LEAVES = 72
# Nut clusters: (x, y, nuts) found from below (ray up), or (x, z, nuts, "front") from the camera side (ray along +Y).
# The playfield is the plane y = 0 (tree-local) between x = 0.55 and 2.05 (its middle 1.3 m right of the trunk): no
# cluster, branch or trunk comes within 0.2 m of it there, so a falling item never meets one; the underside clusters
# hang behind it, the front ones from the front fringe.
HAZEL_CLUSTERS = [(0.30, 0.44, 3), (0.80, 0.48, 2), (1.30, 0.46, 3), (1.80, 0.48, 2), (2.30, 0.40, 3),
                  (0.62, 2.94, 2, "front"), (1.30, 2.87, 3, "front"), (1.98, 2.94, 2, "front")]
HAZEL_FIELD = (0.55, 2.05, 0.2)      # x from, x to, half depth of the corridor kept clear around y = 0
HAZEL_NUT = 0.054                    # nut radius on the tree
HAZEL_NUT_VERTS = 20 + 12 + 6        # shell (6 x 4 ellipsoid), husk (10 + 2), little stalk (3 x 2)


def _lobe_field(p):
    """Each lobe's normalized closeness at p (1 at its center, 0 at its surface), squash applied."""
    out = []
    for c, r in HAZEL_LOBES:
        d = p - c
        out.append(1.0 - Vector((d.x, d.y, d.z / HAZEL_SQUASH)).length / r)
    return out


def _hazel_crown(work):
    """Stiff metaball union of the lobes, voxel remesh, smoothing (the Prairie crown method), in ND_WORK."""
    data = bpy.data.metaballs.new("WORK_ND_HazelCrown")
    data.resolution = 0.08
    data.render_resolution = 0.08
    for center, radius in HAZEL_LOBES:
        e = data.elements.new(type='ELLIPSOID')
        e.co = center
        e.radius = radius * 1.35
        e.size_x, e.size_y, e.size_z = 1.0, 1.0, HAZEL_SQUASH
        e.stiffness = 3.0
    obj = bpy.data.objects.new("WORK_ND_HazelCrown", data)
    work.objects.link(obj)
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(obj)
    bpy.data.metaballs.remove(data)
    temp = bpy.data.objects.new("WORK_ND_HazelCrown_Temp", mesh)
    work.objects.link(temp)
    remesh = temp.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = 0.06
    replace_with_evaluated(temp, remesh)
    smooth = temp.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.6
    smooth.iterations = 5
    replace_with_evaluated(temp, smooth)
    decimate_to(temp, HAZEL_CROWN_TRIS)
    return temp


def _hazel_leaf(bm, base, normal, along, size, lift):
    """A round hazel leaf lying on the crown like a scale (pw_grassland._leaf_scale, pointing along `along`)."""
    n = normal.normalized()
    a = along - n * along.dot(n)
    if a.length < 1e-3:
        a = n.orthogonal()
    a.normalize()
    side = n.cross(a).normalized()
    tip = base + a * size + n * size * lift
    mid = base + a * size * 0.5 + n * size * lift * 0.4
    pts = [base - a * size * 0.2, mid + side * size * 0.5, tip, mid - side * size * 0.5]
    front = bm.faces.new([bm.verts.new(p) for p in pts])
    back = bm.faces.new([bm.verts.new(p - n * 0.006) for p in reversed(pts)])
    front.normal_update()
    back.normal_update()
    if front.normal.dot(n) < 0.0:
        front.normal_flip()
    if back.normal.dot(n) > 0.0:
        back.normal_flip()
    return [base - a * size * 0.2, mid, tip]


def _hazel_husk(bm, center, axis, radius, turn):
    """The leafy husk over a nut: a collar of five round lobes (outline 10 points) domed over its top."""
    w = axis.normalized()
    u = w.orthogonal().normalized()
    v = w.cross(u).normalized()
    outline = []
    for j in range(10):
        a = turn + j / 10 * math.tau
        lobe = 1.0 if j % 2 == 0 else 0.82
        outline.append(center + (u * math.cos(a) + v * math.sin(a)) * radius * 1.12 * lobe
                       + w * (radius * (0.05 if j % 2 == 0 else 0.22)))
    ring = [bm.verts.new(p) for p in outline]
    top = bm.verts.new(center + w * radius * 1.22)
    under = bm.verts.new(center + w * radius * 0.55)
    for k in range(10):
        m = (k + 1) % 10
        bm.faces.new((top, ring[k], ring[m]))
        bm.faces.new((under, ring[m], ring[k]))


def _inside_closed(bvh, p):
    """True when p is inside the closed crown: the first surface met going up faces up (seen from inside)."""
    hit, normal, _, _ = bvh.ray_cast(p, UP)
    return hit is not None and normal.z > 0.0


def _plan_clusters(bvh):
    """Where each nut cluster hangs: the crown point above it, the joint under it, its nuts. A cluster drops until
    every nut clears the crown by a quarter of its radius."""
    rng = random.Random(4141)
    plans = []
    for spec in HAZEL_CLUSTERS:
        if len(spec) == 4:
            x, z, count, _ = spec
            hit, normal, _, _ = bvh.ray_cast(Vector((x, -5.0, z)), Vector((0.0, 1.0, 0.0)))
            hang = Vector((0.0, -0.35, -1.0)).normalized()
        else:
            x, y, count = spec
            hit, normal, _, _ = bvh.ray_cast(Vector((x, y, 0.0)), UP)
            hang = Vector((0.0, 0.0, -1.0))
        if hit is None:
            raise RuntimeError(f"ND_HazelCanopy: no crown at the cluster {spec}")
        turn = rng.uniform(0.0, math.tau)
        spread = 0.061 if count == 2 else 0.070
        drop = 0.05
        while True:
            joint = hit + hang * drop
            nuts = []
            for k in range(count):
                a = turn + k / count * math.tau
                nuts.append(joint + Vector((math.cos(a), math.sin(a), 0.0)) * spread
                            + Vector((0.0, 0.0, -HAZEL_NUT * 1.05 - 0.012 * (k % 2))))
            if all(bvh.find_nearest(c)[3] > HAZEL_NUT * 1.25 and not _inside_closed(bvh, c) for c in nuts):
                break
            drop += 0.01
            if drop > 0.4:
                raise RuntimeError(f"ND_HazelCanopy: the cluster {spec} finds no room")
        plans.append((hit, hang, joint, nuts, turn))
    return plans


def build_hazel_canopy(col, work):
    """ND_HazelCanopy: a big round hazel crown 3.7 m wide, its underside 2.6 m above the trunk's foot (2.74 m over
    the playfield corridor), seen from below and in front: an upper dome, a middle ring and a lower front fringe of
    rounded lobes (layered tiers, each lobe shaded on its own), leaf scales over the top and the front like the
    Prairie trees, eight clusters of two or three hazelnuts in round-lobed husks hanging from the underside and the front edge. Pivot at the base of
    the trunk (ND_HazelTrunk). Regions: base nut shell, R foliage, G sunlit, B shaded, A husks."""
    crown = _hazel_crown(work)
    bm = bmesh.new()
    bm.from_mesh(crown.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    data = crown.data
    bpy.data.objects.remove(crown)
    bpy.data.meshes.remove(data)
    crown_end = len(bm.verts)
    bvh = BVHTree.FromBMesh(bm)
    lo = Vector([min(v.co[a] for v in bm.verts) for a in range(3)])
    hi = Vector([max(v.co[a] for v in bm.verts) for a in range(3)])
    center = (lo + hi) * 0.5
    radii = (hi - lo) * 0.5
    plans = _plan_clusters(bvh)
    keep_clear = [p[0] for p in plans]

    # Leaves: Fibonacci directions over the crown, raycast onto it, none on the unseen back nor near a cluster; on the
    # underside they point outward from the crown's middle (shingles seen from below), on top down the slope.
    rng = random.Random(4040)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    placed = 0
    for i in range(HAZEL_LEAVES * 4):
        if placed >= HAZEL_LEAVES:
            break
        z = 1.0 - 2.0 * (i + 0.5) / (HAZEL_LEAVES * 4)
        r = math.sqrt(max(0.0, 1.0 - z * z))
        a = i * golden + rng.uniform(-0.2, 0.2)
        d = Vector((math.cos(a) * r, math.sin(a) * r, z))
        size, lift = rng.uniform(0.24, 0.32), rng.uniform(0.08, 0.15)
        if d.y > 0.5 and d.z < 0.35:
            continue
        origin = center + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * 2.0
        hit, normal, _, _ = bvh.ray_cast(origin, -d)
        if hit is None or any((hit - q).length < 0.22 + size for q in keep_clear):
            continue
        flat = Vector((hit.x - center.x, hit.y - center.y, 0.0))
        if d.z > 0.15 or flat.length < 1e-3:
            along = Vector((0.0, 0.0, -1.0)) + d * 0.6
        else:
            along = flat.normalized() + Vector((0.0, 0.0, -0.3))
        _hazel_leaf(bm, hit - normal * 0.014, normal, along, size, lift)
        placed += 1
    leaves_end = len(bm.verts)

    # Nut clusters: a short stalk out of the foliage, the nuts around a joint under it, each capped by its husk.
    kind = ["crown"] * crown_end + ["leaf"] * (leaves_end - crown_end)
    nut_of = {}
    for hit, hang, joint, nuts, turn in plans:
        start = len(bm.verts)
        _nd_loft(bm, [hit - hang * 0.04, joint], [0.013, 0.011], [0.013, 0.011], 3, up=Vector((1.0, 0.0, 0.0)))
        kind += ["stalk"] * (len(bm.verts) - start)
        for k, c in enumerate(nuts):
            axis = (joint - c).normalized()
            rot = Vector((0.0, 0.0, 1.0)).rotation_difference(axis).to_matrix()
            start = len(bm.verts)
            _nd_sphere(bm, c, Vector((HAZEL_NUT, HAZEL_NUT, HAZEL_NUT * 1.12)), 6, 4, rotation=rot)
            kind += ["shell"] * (len(bm.verts) - start)
            nut_of.update({j: (c, axis) for j in range(start, len(bm.verts))})
            start = len(bm.verts)
            _hazel_husk(bm, c, axis, HAZEL_NUT, turn + k)
            kind += ["husk"] * (len(bm.verts) - start)
            nut_of.update({j: (c, axis) for j in range(start, len(bm.verts))})
            start = len(bm.verts)
            _nd_loft(bm, [c + axis * HAZEL_NUT * 0.9, joint], [0.007, 0.006], [0.007, 0.006], 3,
                     up=Vector((1.0, 0.0, 0.0)))
            kind += ["stalk"] * (len(bm.verts) - start)
    bm.normal_update()
    _nd_replace("ND_HazelCanopy")
    obj = object_from_bmesh("ND_HazelCanopy", bm, [col])
    me = obj.data
    sun_dir = Vector((-0.55, -0.45, 0.70)).normalized()

    def cdir(co):
        d = co - center
        return Vector((d.x / radii.x, d.y / radii.y, d.z / radii.z)).normalized()

    def lobe_dir(co):
        field = _lobe_field(co)
        order = sorted(range(len(field)), key=lambda j: -field[j])
        c, r = HAZEL_LOBES[order[0]]
        d = co - c
        d = Vector((d.x, d.y, d.z / HAZEL_SQUASH)).normalized()
        crease = 1.0 - smoothstep(0.0, 0.16, field[order[0]] - field[order[1]])
        return d, crease

    def regions(co, n, i):
        k = kind[i]
        if k == "shell":
            return (0.0, 0.0, 0.0, 0.0)
        if k == "husk":
            return (0.0, 0.0, 0.0, 1.0)
        if k == "stalk":
            return (0.0, 0.0, 0.0, 0.3)
        c = cdir(co)
        ld, crease = lobe_dir(co)
        mix = (c * 0.45 + ld * 0.55).normalized()
        sun = smoothstep(0.05, 0.85, mix.dot(sun_dir)) * smoothstep(-0.35, 0.4, mix.z)
        shade = smoothstep(-0.05, -0.75, mix.z) * (1.0 - 0.35 * smoothstep(0.2, 0.8, ld.z))
        flicker = 0.18 * (0.5 + 0.5 * wave_noise(co * 2.4, 4077, octaves=1))
        if k == "leaf":
            return (1.0, min(1.0, 0.3 + sun * 0.75 + flicker), shade * 0.55, 0.0)
        # Each lobe's lower front keeps a lit rim: the tiers read from below.
        rim = 0.3 * smoothstep(0.1, 0.6, -ld.y) * smoothstep(-0.5, 0.1, ld.z) * (1.0 - crease)
        return (1.0, min(1.0, sun * 0.85 + flicker * 0.5 + rim), shade * (1.0 - 0.5 * rim), 0.0)

    def occlusion(co, n, i):
        k = kind[i]
        h = min(1.0, max(0.0, co.z / hi.z))
        if k == "shell":
            return (0.08, h)
        if k == "husk":
            return (0.12, h)
        if k == "stalk":
            return (0.42, h)
        ld, crease = lobe_dir(co)
        e = co - center
        depth = 1.0 - smoothstep(0.75, 1.0, Vector((e.x / radii.x, e.y / radii.y, e.z / radii.z)).length)
        under = 0.22 * smoothstep(0.0, -0.8, cdir(co).z)
        return (min(0.85, under + 0.28 * depth + 0.38 * crease), h)

    write_layers(obj, regions, occlusion)
    # Normals: crown and leaves shade as soft lobes inside one soft volume; nuts and husks shade as round balls from
    # their nut's center (the 6-sided nuts read round), stalks keep their own.
    normals = []
    for v in me.vertices:
        if kind[v.index] in ("crown", "leaf"):
            ld, _ = lobe_dir(v.co)
            target = (cdir(v.co) * 0.45 + ld * 0.55).normalized()
            normals.append(v.normal.lerp(target, 0.75).normalized())
        elif v.index in nut_of:
            c, axis = nut_of[v.index]
            d = v.co - c
            along = axis * d.dot(axis)
            normals.append((d - along + along / 1.25).normalized())
        else:
            normals.append(v.normal.copy())
    me.normals_split_custom_set_from_vertices(normals)
    return obj, crown_end


# The hazel's trunk: one chunky main trunk (flared foot, 3 cm under the ground), a second stem parting from it, one
# branch reaching under the crown toward the middle: points, radii, sides. Every end is hidden inside the crown.
HAZEL_STEMS = [
    ([Vector((0.0, 0.0, -0.03)), Vector((0.01, 0.0, 0.15)), Vector((0.05, 0.0, 0.80)), Vector((0.14, 0.02, 1.50)),
      Vector((0.25, 0.05, 2.10)), Vector((0.36, 0.08, 2.60)), Vector((0.46, 0.10, 3.05))],
     [0.330, 0.285, 0.245, 0.215, 0.190, 0.165, 0.150], 12),
    ([Vector((0.03, 0.03, 0.85)), Vector((-0.10, 0.11, 1.55)), Vector((-0.27, 0.16, 2.30)), Vector((-0.38, 0.17, 2.95))],
     [0.140, 0.118, 0.100, 0.088], 8),
]
HAZEL_BRANCH = ([Vector((0.27, 0.07, 2.05)), Vector((0.58, 0.25, 2.42)), Vector((0.92, 0.40, 2.70)),
                 Vector((1.12, 0.45, 2.88))], [0.105, 0.082, 0.066, 0.052], 6)


def build_hazel_trunk(col, canopy, crown_end):
    """ND_HazelTrunk: one chunky smooth trunk (0.33 m at its flared foot, 3 cm under the ground) rising into the
    crown at the left, a second stem parting from it at 0.85 m, one branch reaching under the crown behind the
    playfield; every top end is hidden inside the crown (checked). Pivot at the base of the trunk, on the ground. Regions: base bark, R pale lenticel bands, G moss
    at the foot, A grooves."""
    tree_bm = bmesh.new()
    tree_bm.from_mesh(canopy.data)
    tree_bm.verts.ensure_lookup_table()
    for f in [f for f in tree_bm.faces if any(v.index >= crown_end for v in f.verts)]:
        tree_bm.faces.remove(f)
    crown_bvh = BVHTree.FromBMesh(tree_bm)
    tree_bm.free()
    bm = bmesh.new()
    parts = _NdParts(bm)
    ends = []
    for k, (points, radii, sides) in enumerate(HAZEL_STEMS):
        def flare(i, j, p, s, u, points=points, radii=radii, sides=sides, k=k):
            if i > 1 or k > 0:
                return p
            a = j / sides * math.tau
            push = (0.5 if i == 0 else 0.22) * max(0.0, math.cos(a * 2.0 + 0.4)) ** 3
            return p + (p - points[i]).normalized() * radii[i] * push
        end = points[-1] + (points[-1] - points[-2]).normalized() * radii[-1] * 0.8
        _nd_loft(bm, points, radii, radii, sides, up=Vector((0.0, 1.0, 0.0)), shape=flare, end=end)
        parts.mark("stem", k)
        ends += [points[-1], end]
    points, radii, sides = HAZEL_BRANCH
    end = points[-1] + (points[-1] - points[-2]).normalized() * 0.03
    _nd_loft(bm, points, radii, radii, sides, up=UP, end=end)
    parts.mark("branch")
    ends += [points[-1], end]
    for tip in ends:
        if not _inside_closed(crown_bvh, tip):
            raise RuntimeError(f"ND_HazelTrunk: the end at {tuple(round(x, 3) for x in tip)} shows outside the crown")
    bm.normal_update()

    def regions(co, n, i):
        lenticel = smoothstep(0.6, 0.95, math.sin(co.z * 23.0 + 1.7 * math.sin(co.x * 9.0 + co.y * 7.0))) * \
            smoothstep(0.2, 0.9, math.sin(math.atan2(co.y, co.x) * 3.0 + co.z * 2.0) * 0.5 + 0.5)
        moss = (1.0 - smoothstep(0.08, 0.38, co.z)) * smoothstep(-0.2, 0.6, n.y + 0.3 * n.x)
        groove = 0.5 * smoothstep(0.7, 0.98, math.sin(math.atan2(co.y - 0.02, co.x) * 4.0 + co.z * 1.3))
        return (0.75 * lenticel * (1.0 - moss), moss, 0.0, groove * (1.0 - moss))

    def occlusion(co, n, i):
        foot = 0.38 * (1.0 - smoothstep(0.0, 0.35, co.z))
        crown_shade = 0.45 * smoothstep(2.0, 2.75, co.z)
        return (foot + crown_shade, min(1.0, co.z / 4.1))

    _nd_replace("ND_HazelTrunk")
    obj = object_from_bmesh("ND_HazelTrunk", bm, [col])
    write_layers(obj, regions, occlusion)
    return obj


# ---------------------------------------------------------------- build, save, export

def build_nutdrop():
    """Builds every Nut Drop object in the scene "NutDrop" (export collection) and returns triangles, sizes and the
    lowest corner of each. Never touches another scene's objects; the work collection is removed afterwards."""
    scene, col, work = _nd_scene()
    out = [build_basket(col), build_hazelnut(col), build_acorn(col), build_berry(col), build_hazelnut(col, golden=True),
           build_pinecone(col), build_feather(col)]
    canopy, crown_end = build_hazel_canopy(col, work)
    trunk = build_hazel_trunk(col, canopy, crown_end)
    out += [canopy, trunk]
    for o in list(work.objects):
        data = o.data
        bpy.data.objects.remove(o)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)
    bpy.data.collections.remove(work)
    report = {}
    tree_tris = 0
    for obj in out:
        tris = triangle_count(obj)
        if obj.name in ("ND_HazelCanopy", "ND_HazelTrunk"):
            tree_tris += tris
        else:
            budget = ND_BUDGETS[obj.name]
            if tris > budget:
                raise RuntimeError(f"{obj.name}: {tris} triangles over its budget of {budget}")
        pts = [Vector(c) for c in obj.bound_box]
        lo = [min(p[a] for p in pts) for a in range(3)]
        hi = [max(p[a] for p in pts) for a in range(3)]
        report[obj.name] = (tris, tuple(round(hi[a] - lo[a], 4) for a in range(3)), tuple(round(x, 4) for x in lo))
    if tree_tris > ND_BUDGETS["ND_Hazel"]:
        raise RuntimeError(f"ND_HazelCanopy + ND_HazelTrunk: {tree_tris} triangles over 4000")
    return report


def _nd_root():
    marker = os.sep + "SourceArt" + os.sep
    path = bpy.data.filepath
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ first (or pass the project root).")
    return path.split(marker)[0]


def _nd_clear_previews():
    scene = bpy.data.scenes.get(ND_SCENE)
    for obj in (scene.objects if scene else []):
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]


def write_nutdrop_blend(root=None):
    """Writes the NutDrop scene and everything it uses to BL_Activity_NutDrop.blend (compressed). The open file stays
    as it is."""
    root = root or _nd_root()
    _nd_clear_previews()
    path = os.path.join(root, *ND_BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {bpy.data.scenes[ND_SCENE]}, fake_user=True, compress=True)
    return path


def finish_nutdrop_blend():
    """Run once inside BL_Activity_NutDrop.blend: its export collection takes the standard name and the file is saved
    compressed."""
    col = bpy.data.collections.get(ND_COLLECTION)
    if col is not None and bpy.data.collections.get("EXPORT") is None:
        col.name = "EXPORT"
    bpy.ops.wm.save_mainfile(compress=True)


def export_nutdrop():
    """The FBX files, with pw_export.export_static (load pw_export.py in the same namespace first). Exact names, so a
    prefix never pulls in another object."""
    _nd_clear_previews()
    done = {}
    for name, names in ND_EXPORTS.items():
        path, objects = export_static(ND_OUT + "/" + name, names)
        extra = [o for o in objects if o not in names]
        if extra or len(objects) != len(names):
            raise RuntimeError(f"{name}: exported {objects}, expected {names}")
        done[name] = objects
    return done


# ---------------------------------------------------------------- measures

def measure_canopy(canopy, trunk=None, step=0.05):
    """The canopy's lowest point over the playfield corridor (tree-local x in HAZEL_FIELD, |y| <= its half depth),
    the lowest point of the whole canopy, and how close to the plane y = 0 anything hanging under the crown (nut
    clusters) or the trunk and branch come inside the corridor's x range."""
    bm = bmesh.new()
    bm.from_mesh(canopy.data)
    bvh = BVHTree.FromBMesh(bm)
    x0, x1, half = HAZEL_FIELD
    low = (None, 99.0)
    x = x0
    while x <= x1 + 1e-6:
        y = -half
        while y <= half + 1e-6:
            hit = bvh.ray_cast(Vector((x, y, 0.0)), UP)[0]
            if hit is not None and hit.z < low[1]:
                low = ((round(x, 2), round(y, 2)), round(hit.z, 3))
            y += step
        x += step
    lowest = min(v.co.z for v in bm.verts)
    hanging = [v.co for v in bm.verts if x0 <= v.co.x <= x1 and v.co.z < low[1]]
    bm.free()
    out = {"corridor_lowest": low, "canopy_lowest": round(lowest, 3),
           "clusters_nearest_y": round(min((abs(p.y) for p in hanging), default=9.0), 3)}
    if trunk is not None:
        wood = [v.co for v in trunk.data.vertices if x0 <= v.co.x <= x1 and v.co.z < low[1]]
        out["trunk_nearest_y"] = round(min((abs(p.y) for p in wood), default=9.0), 3)
    return out


def fit_basket(species, offsets=(0.0, -0.02, -0.04, -0.06), gap=0.004, scale=(1.0, 1.0, 1.0)):
    """Read-only, in a file holding SKM_<species>_LOD0 and SOCK_HeadTop (rest pose): the basket is set on the socket,
    moved forward by each offset (Blender -Y), then raised in 2 mm steps until it neither overlaps the body nor comes
    closer than `gap`. Returns, per offset, the lift above the socket and the part that set it. `scale` sizes the
    basket around its pivot first (the wide basket special: (1.5, 1.0, 1.0) widens it across the screen). Builds the
    basket in a temporary collection, removed afterwards; nothing is saved."""
    if bpy.data.objects.get("ND_Basket") is not None:
        raise RuntimeError("fit_basket runs in an animal's file, never next to the real ND_Basket")
    body = bpy.data.objects[f"SKM_{species}_LOD0"]
    sock = bpy.data.objects["SOCK_HeadTop"].matrix_world.translation.copy()
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    bm.from_object(body, dg)
    bm.transform(body.matrix_world)
    body_bvh = BVHTree.FromBMesh(bm)
    body_pts = [v.co.copy() for v in bm.verts]
    bm.free()
    temp = bpy.data.collections.new("WORK_ND_Fit")
    bpy.context.scene.collection.children.link(temp)
    basket = build_basket(temp)
    local = bmesh.new()
    local.from_mesh(basket.data)
    local.transform(Matrix.Diagonal(Vector(scale)).to_4x4())
    results = {}
    for fwd in offsets:
        lift = -0.03
        while lift < 0.4:
            m = Matrix.Translation(sock + Vector((0.0, fwd, lift)))
            moved = local.copy()
            moved.transform(m)
            b_bvh = BVHTree.FromBMesh(moved)
            hit = b_bvh.overlap(body_bvh)
            near = min(b_bvh.find_nearest(p)[3] for p in body_pts if (p - sock).length < 0.45) if not hit else 0.0
            moved.free()
            if not hit and near >= gap:
                break
            lift += 0.002
        # Which part set the lift: the body point nearest to the basket at that height.
        m = Matrix.Translation(sock + Vector((0.0, fwd, lift)))
        moved = local.copy()
        moved.transform(m)
        b_bvh = BVHTree.FromBMesh(moved)
        p_near = min((p for p in body_pts if (p - sock).length < 0.45), key=lambda p: b_bvh.find_nearest(p)[3])
        moved.free()
        part = "ears" if p_near.z > sock.z + 0.01 and p_near.y > sock.y + 0.02 else "head"
        results[round(fwd, 3)] = {"lift": round(lift, 3), "set_by": part,
                                  "near_point": tuple(round(c, 3) for c in p_near)}
    local.free()
    data = basket.data
    bpy.data.objects.remove(basket)
    bpy.data.meshes.remove(data)
    bpy.data.collections.remove(temp)
    return {"socket": tuple(round(c, 4) for c in sock), "fits": results}


# ---------------------------------------------------------------- review images

ND_ROWS = {"ND_Basket": "palette.nut_drop.basket", "ND_Hazelnut": "palette.nut_drop.hazelnut",
           "ND_Acorn": "palette.nut_drop.acorn", "ND_Berry": "palette.nut_drop.berry",
           "ND_GoldenNut": "palette.nut_drop.golden_nut", "ND_Pinecone": "palette.nut_drop.pinecone",
           "ND_Feather": "palette.nut_drop.feather", "ND_HazelCanopy": "palette.nut_drop.hazel_canopy",
           "ND_HazelTrunk": "palette.nut_drop.hazel_trunk"}
# Reference rows for the review images (DEF_Palettes_Grassland, GROOMING_PALETTES) and the animals' first variant
# colors (primary, secondary, belly, detail, eye: AnimalPipeline).
ND_REF_ROWS = {
    "ground": ("78A862", "8FB878", "5F9152", "B8C98E", "8F6A50", "4A6542", "9CBC74"),
    "tree": ("93694F", "7DB960", "A9D57C", "578F4B", "6E4C3A", "3B6338", "FFFFFF"),
    "fruit_tree": ("8E6650", "76B35C", "A3D27C", "538C49", "E25B4C", "3A6337", "FFFFFF"),
    "bush": ("6A4E3C", "6FAE57", "9ED07A", "4C8644", "6FAE57", "355E33", "FFFFFF"),
    "set_backdrop": ("8DC166", "D5E08B", "A9C9C2", "F5EFE2", "8F6B50", "4E6D5D", "B4D873"),
    "brush": ("C69467", "EBDCBF", "E3C08F", "A87850", "C69467", "6F5A57", "FFFFFF"),
    "bow_pink": ("F2AAC1", "F2AAC1", "DE8CA9", "F2AAC1", "F2AAC1", "9A6A88", "FFFFFF"),
    "burr": ("8A7B48", "B8A764", "8A7B48", "8A7B48", "8A7B48", "4E4733", "FFFFFF"),
}
ND_ANIMALS = {
    "Rabbit": ("SKM_Rabbit_LOD0", ("B07A52", "F2B8B5", "F3E3CC", "E98A93", "1E1414")),
    "Squirrel": ("SKM_Squirrel_LOD0", ("C9693A", "B4552E", "F4E3CB", "5E3329", "1A1210")),
    "Hedgehog": ("SKM_Hedgehog_LOD0", ("B48A66", "6E4B36", "EEDCC4", "2A1E1C", "140E0D")),
}


def _nd_linear(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def nd_paint_preview(obj, row):
    """WORK_Preview (point color) = what SH_EnvironmentOpaque computes from the regions, before lighting."""
    pal = [_nd_linear(h) for h in row]
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


def _nd_paint_animal(obj, colors):
    cols = [_nd_linear(h) for h in colors]
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    prev = me.color_attributes.get("WORK_Preview") or me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for i, data in enumerate(regions.data):
        c = cols[0].copy()
        for weight, colr in zip(data.color, cols[1:]):
            c = c.lerp(colr, weight)
        prev.data[i].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = prev


def _nd_preview_material():
    mat = bpy.data.materials.get("WORK_ND_Material") or bpy.data.materials.new("WORK_ND_Material")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = next((n for n in nt.nodes if n.type == "ATTRIBUTE"), None) or nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "WORK_Preview"
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.15
    return mat


def _nd_preview_world(scene):
    world = bpy.data.worlds.get("WORK_ND_World") or bpy.data.worlds.new("WORK_ND_World")
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    coords, sep = nt.nodes.new("ShaderNodeTexCoord"), nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp, bg = nt.nodes.new("ShaderNodeValToRGB"), nt.nodes.new("ShaderNodeBackground")
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(coords.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    nt.links.new(bg.outputs[0], out.inputs[0])
    ramp.color_ramp.elements[0].color = tuple(_nd_linear("CFE3E4")) + (1.0,)
    ramp.color_ramp.elements[1].position = 0.45
    ramp.color_ramp.elements[1].color = tuple(_nd_linear("7FB2E6")) + (1.0,)
    bg.inputs[1].default_value = 1.0
    scene.world = world
    return world


class NdStudio:
    """Temporary camera, sun, material and world in the NutDrop scene for review images; remove() cleans all up."""

    def __init__(self, scene, size=1.0):
        self.scene = scene
        self.size = size
        self.temp = []
        self.col = bpy.data.collections.new("WORK_ND_Review")
        scene.collection.children.link(self.col)
        self.mat = _nd_preview_material()
        self.world = _nd_preview_world(scene)
        try:
            scene.render.engine = "BLENDER_EEVEE"
        except TypeError:
            scene.render.engine = "CYCLES"
        scene.view_settings.view_transform = "Standard"
        scene.render.film_transparent = False
        for flag in [f for f in dir(scene.render) if f.startswith("use_stamp")]:
            setattr(scene.render, flag, False)      # no text written into the review PNGs
        if hasattr(scene, "eevee"):
            scene.eevee.taa_render_samples = 48
        cam_data = bpy.data.cameras.new("WORK_ND_Camera")
        self.cam = self.keep(bpy.data.objects.new("WORK_ND_Camera", cam_data))
        scene.camera = self.cam
        sun_data = bpy.data.lights.new("WORK_ND_Sun", 'SUN')
        sun_data.energy = 3.2
        sun_data.color = (1.0, 0.95, 0.86)
        sun_data.angle = math.radians(9.0)
        self.sun = self.keep(bpy.data.objects.new("WORK_ND_Sun", sun_data))

    def keep(self, obj):
        self.col.objects.link(obj)
        self.temp.append(obj)
        return obj

    def dress(self, obj, row):
        nd_paint_preview(obj, row)
        obj.data.materials.clear()
        obj.data.materials.append(self.mat)

    def copy(self, obj, row, location, rotation=(0.0, 0.0, 0.0), scale=1.0):
        dup = self.keep(bpy.data.objects.new("WORK_" + obj.name, obj.data.copy()))
        dup.location = location
        dup.rotation_euler = rotation
        dup.scale = (scale, scale, scale)
        self.dress(dup, row)
        return dup

    def aim(self, eye, target, fov_deg, width, height):
        cam, data = self.cam, self.cam.data
        cam.location = eye
        direction = (target - eye).normalized()
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        data.type = 'PERSP'
        data.sensor_fit = 'VERTICAL'
        data.angle_y = math.radians(fov_deg)
        data.clip_start = 0.01
        data.clip_end = 80.0
        self.scene.render.resolution_x = int(width * self.size)
        self.scene.render.resolution_y = int(height * self.size)
        # Sun from the top left of the image, a little behind the camera (charte, section 5).
        right = direction.cross(UP).normalized()
        toward_sun = (-right * 0.75 - direction * 0.45 + UP * 1.35).normalized()
        self.sun.rotation_euler = (-toward_sun).to_track_quat('-Z', 'Y').to_euler()

    def show(self, objs):
        names = {o.name for o in objs}
        for o in self.scene.objects:
            if o.type == 'MESH':
                o.hide_render = o.name not in names

    def shot(self, path):
        self.scene.render.filepath = path
        bpy.ops.render.render(write_still=True, scene=self.scene.name)
        return path

    def remove(self):
        for o in self.temp:
            data = o.data
            bpy.data.objects.remove(o)
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Camera):
                    bpy.data.cameras.remove(data)
                elif isinstance(data, bpy.types.Light):
                    bpy.data.lights.remove(data)
        self.temp = []
        bpy.data.collections.remove(self.col)
        for o in self.scene.objects:
            if o.type == 'MESH':
                o.hide_render = False
                if o.name in ND_ROWS:
                    o.data.materials.clear()
        _nd_clear_previews()
        if self.mat.users == 0:
            bpy.data.materials.remove(self.mat)
        self.scene.world = None
        if self.world.users == 0:
            bpy.data.worlds.remove(self.world)


# SOCK_HeadTop of each animal in its rest pose, read in its own file (fit_basket prints it); the review images set
# the basket on it with the (forward, lift) offsets fit_basket measured.
ND_SOCKETS = {"Rabbit": Vector((0.0, -0.075, 0.4381)), "Squirrel": Vector((0.0, -0.075, 0.4318)),
              "Hedgehog": Vector((0.0, -0.100, 0.2595))}


def _nd_ground(st, radius=6.0, row=None):
    """A flat meadow disc at z = 0 for the review images (not exported)."""
    gbm = bmesh.new()
    bmesh.ops.create_circle(gbm, cap_ends=True, radius=radius, segments=64)
    me = bpy.data.meshes.new("WORK_ND_Ground")
    gbm.to_mesh(me)
    gbm.free()
    ground = st.keep(bpy.data.objects.new("WORK_ND_Ground", me))
    regions = me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    for v in me.vertices:
        regions.data[v.index].color = (0.25, 0.0, 0.0, 0.0)
    me.uv_layers.new(name="PW_Occlusion")
    st.dress(ground, row or ND_REF_ROWS["ground"])
    return ground


def _nd_append_mesh(st, path, mesh_name, row=None, animal=None):
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.meshes = [n for n in src.meshes if n == mesh_name]
    if not dst.meshes:
        raise RuntimeError(f"{mesh_name} not found in {path}")
    obj = st.keep(bpy.data.objects.new("WORK_REF_" + mesh_name, dst.meshes[0]))
    if animal is not None:
        _nd_paint_animal(obj, animal)
        obj.data.materials.clear()
        obj.data.materials.append(st.mat)
    else:
        st.dress(obj, row)
    return obj


def _nd_world_bvh(obj):
    bpy.context.view_layer.update()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def nd_drop_in(item, basket, others=(), gap=0.002):
    """Lowers a caught item into the basket until it rests on the floor or on the items already there, never in the
    weave nor in another item: it starts high above its x, y and goes down in 2 mm steps while it keeps `gap` from
    everything. Returns its lowest point above the basket's floor."""
    solid = [_nd_world_bvh(basket)] + [_nd_world_bvh(o) for o in others]
    x, y = item.location.x, item.location.y
    z = basket.location.z + 0.40
    last_free = None
    while z > basket.location.z:
        item.location = (x, y, z)
        mine = _nd_world_bvh(item)
        pts = [item.matrix_world @ v.co for v in item.data.vertices]
        touching = any(mine.overlap(t) for t in solid) or             any(t.find_nearest(p, gap)[0] is not None for t in solid for p in pts)
        if touching:
            break
        last_free = z
        z -= 0.002
    if last_free is None:
        raise RuntimeError(f"{item.name}: no room in the basket at ({x}, {y})")
    item.location = (x, y, last_free)
    bpy.context.view_layer.update()
    return min((item.matrix_world @ v.co).z for v in item.data.vertices) - (basket.location.z + BASKET_FLOOR)


def render_nutdrop_previews(out_dir, refs, fits, size=1.0):
    """Review images in out_dir (T40_props_*.png), EEVEE, one sun from the top left of the image, palette colors
    computed like SH_EnvironmentOpaque. refs: {"Rabbit"/"Squirrel": BL_Animal_<Species>.blend, "grooming":
    BL_Activity_Grooming.blend, "magnifique": BL_Biome_Grassland_Magnifique.blend}, read only. fits: {species:
    (forward, lift)} from fit_basket. Everything made here is removed afterwards; nothing is saved."""
    scene = bpy.data.scenes[ND_SCENE]
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    st = NdStudio(scene, size)
    props = {o.name: o for o in scene.objects if o.type == 'MESH' and o.name in ND_ROWS}
    for name, obj in props.items():
        st.dress(obj, ND_PALETTES[ND_ROWS[name]])
    files = []

    def place(obj, loc, rot=(0.0, 0.0, 0.0), scale=1.0):
        obj.location = loc
        obj.rotation_euler = rot
        obj.scale = (scale, scale, scale)

    def shot(name):
        files.append(st.shot(os.path.join(out_dir, name)))

    items = ["ND_Hazelnut", "ND_Acorn", "ND_Berry", "ND_GoldenNut", "ND_Pinecone", "ND_Feather"]
    # 1. The falling items, side by side at their real size, from the front and a little above.
    for k, name in enumerate(items):
        place(props[name], (k * 0.19 - 0.475, 0.0, 0.0))
    st.show([props[n] for n in items])
    st.aim(Vector((0.0, -1.25, 0.45)), Vector((0.0, 0.0, 0.0)), 20.0, 1400, 520)
    shot("T40_props_items.png")

    # 2. The basket from the 3/4 high camera, three caught items resting on its floor and on each other, none in the
    # weave (their lowest points measured against the inside).
    basket = props["ND_Basket"]
    place(basket, (0.0, 0.0, 0.0))
    inside = [(props["ND_Hazelnut"], (0.0, 0.0, 0.3)), (props["ND_Acorn"], (0.0, 1.45, 0.9)),
              (props["ND_Berry"], (0.45, 0.0, 0.2))]
    rests = {}
    for k, (obj, rot) in enumerate(inside):
        # The lowest resting place among a few spots: the first item on the floor, the next ones beside it.
        best = None
        spots = [(-0.034, 0.012)] if k == 0 else [(math.cos(j * 0.7) * 0.05, math.sin(j * 0.7) * 0.05) for j in range(9)]
        for x, y in spots:
            place(obj, (x, y, 0.0), rot)
            h = nd_drop_in(obj, basket, [o for o, _ in inside[:k]])
            if best is None or h < best[0]:
                best = (h, obj.location.copy())
        obj.location = best[1]
        rests[obj.name] = round(best[0], 3)
    print("Caught items, lowest point above the basket floor (m):", rests)
    gold = st.copy(props["ND_GoldenNut"], ND_PALETTES["palette.nut_drop.golden_nut"], (0.36, 0.0, 0.0645))
    pine = st.copy(props["ND_Pinecone"], ND_PALETTES["palette.nut_drop.pinecone"], (-0.33, 0.05, 0.0728))
    ground = _nd_ground(st)
    st.show([basket, gold, pine, ground] + [o for o, _ in inside])
    st.aim(Vector((0.25, -0.85, 0.72)), Vector((0.05, 0.0, 0.07)), 30.0, 1200, 800)
    shot("T40_props_basket.png")

    # 3. The hazel tree beside the Prairie's fruit tree and bush, the brown rabbit for scale.
    canopy, trunk = props["ND_HazelCanopy"], props["ND_HazelTrunk"]
    place(canopy, (0.0, 0.0, 0.0))
    place(trunk, (0.0, 0.0, 0.0))
    ref_tree = _nd_append_mesh(st, refs["magnifique"], "GL_Tree_D_LOD0", ND_REF_ROWS["fruit_tree"])
    ref_bush = _nd_append_mesh(st, refs["magnifique"], "GL_Bush_A", ND_REF_ROWS["bush"])
    rabbit = _nd_append_mesh(st, refs["Rabbit"], ND_ANIMALS["Rabbit"][0], animal=ND_ANIMALS["Rabbit"][1])
    place(ref_tree, (4.6, 0.6, 0.0), (0.0, 0.0, 0.5))
    place(ref_bush, (3.0, -0.6, 0.0), (0.0, 0.0, 0.3))
    place(rabbit, (1.6, -0.8, 0.0), (0.0, 0.0, 0.35))
    st.show([canopy, trunk, ref_tree, ref_bush, rabbit, ground])
    st.aim(Vector((2.6, -11.0, 2.6)), Vector((2.4, 0.0, 1.9)), 34.0, 1400, 760)
    shot("T40_props_tree.png")

    # 4. A mock of the game frame (portrait): the meadow and hills of the T38 set, the hazel at the left, its crown
    # over the playfield, the squirrel under it with the basket on its head, items falling in the plane y = 0.
    backdrop = _nd_append_mesh(st, refs["grooming"], "GR_SetBackdrop", ND_REF_ROWS["set_backdrop"])
    place(backdrop, (0.0, 0.0, 0.30))
    squirrel = _nd_append_mesh(st, refs["Squirrel"], ND_ANIMALS["Squirrel"][0], animal=ND_ANIMALS["Squirrel"][1])
    place(squirrel, (0.0, 0.0, 0.0))
    fwd, lift = fits["Squirrel"]
    head_basket = st.copy(basket, ND_PALETTES["palette.nut_drop.basket"],
                          ND_SOCKETS["Squirrel"] + Vector((0.0, fwd, lift)))
    rim = head_basket.location.z + 0.164
    caught = st.copy(props["ND_Hazelnut"], ND_PALETTES["palette.nut_drop.hazelnut"],
                     head_basket.location + Vector((-0.03, 0.02, 0.0)))
    nd_drop_in(caught, head_basket)
    field_x = HAZEL_FIELD[0] + (HAZEL_FIELD[1] - HAZEL_FIELD[0]) * 0.5
    place(canopy, (-field_x, 0.0, 0.0))
    place(trunk, (-field_x, 0.0, 0.0))
    falling = [("ND_Hazelnut", (-0.42, 0.0, 2.25), (0.3, 0.2, 0.0)), ("ND_Acorn", (0.36, 0.0, 1.85), (0.0, 0.5, 0.3)),
               ("ND_Berry", (-0.18, 0.0, 1.32), (0.2, 0.0, 0.4)), ("ND_GoldenNut", (0.55, 0.0, 2.4), (0.0, 0.3, 0.0)),
               ("ND_Pinecone", (0.12, 0.0, 2.05), (0.0, -0.3, 0.0)), ("ND_Feather", (-0.62, 0.0, 1.62), (0.0, 0.5, 0.0))]
    air = []
    for name, loc, rot in falling:
        air.append(st.copy(props[name], ND_PALETTES[ND_ROWS[name]], loc, rot))
    floor = _nd_ground(st, radius=1.2, row=ND_REF_ROWS["set_backdrop"])
    st.show([backdrop, floor, canopy, trunk, squirrel, head_basket, caught] + air)
    st.aim(Vector((0.0, -3.6, 1.35)), Vector((0.0, 0.0, 1.3)), 58.0, 540, 1170)
    shot("T40_props_game.png")
    place(canopy, (0.0, 0.0, 0.0))
    place(trunk, (0.0, 0.0, 0.0))

    # 5. The family: the squirrel and the rabbit with the basket (each at its measured fit), the items around, the
    # T38 brush, bow and burr, a Prairie bush.
    place(squirrel, (-0.25, 0.0, 0.0), (0.0, 0.0, 0.25))
    sq_basket = st.copy(basket, ND_PALETTES["palette.nut_drop.basket"], (0.0, 0.0, 0.0))
    sq_basket.parent = squirrel
    sq_basket.location = ND_SOCKETS["Squirrel"] + Vector((0.0, fits["Squirrel"][0], fits["Squirrel"][1]))
    place(rabbit, (0.45, 0.25, 0.0), (0.0, 0.0, -0.3))
    rb_basket = st.copy(basket, ND_PALETTES["palette.nut_drop.basket"], (0.0, 0.0, 0.0))
    rb_basket.parent = rabbit
    rb_basket.location = ND_SOCKETS["Rabbit"] + Vector((0.0, fits["Rabbit"][0], fits["Rabbit"][1]))
    brush = _nd_append_mesh(st, refs["grooming"], "GR_Brush", ND_REF_ROWS["brush"])
    bow = _nd_append_mesh(st, refs["grooming"], "GR_Bow", ND_REF_ROWS["bow_pink"])
    burr = _nd_append_mesh(st, refs["grooming"], "GR_Burr", ND_REF_ROWS["burr"])
    place(brush, (0.12, -0.42, 0.0), (0.0, 0.0, 0.9))
    place(bow, (0.25, -0.5, 0.0))
    place(burr, (0.33, -0.42, 0.0))
    lay = [("ND_Hazelnut", (-0.55, -0.35)), ("ND_Acorn", (-0.38, -0.5)), ("ND_Berry", (-0.18, -0.55)),
           ("ND_GoldenNut", (0.0, -0.6)), ("ND_Pinecone", (0.55, -0.45)), ("ND_Feather", (0.75, -0.3))]
    on_ground = []
    for name, (x, y) in lay:
        o = props[name]
        dims = o.dimensions
        z = dims.z * 0.5 if name != "ND_Feather" else 0.01
        rot = (math.pi * 0.5, 0.0, 0.4) if name == "ND_Feather" else (0.0, 0.0, 0.3)
        if name == "ND_Feather":
            z = 0.0105
        place(o, (x, y, z), rot)
        on_ground.append(o)
    place(ref_bush, (-0.9, 0.9, 0.0), (0.0, 0.0, 0.2))
    st.show([squirrel, sq_basket, rabbit, rb_basket, brush, bow, burr, ref_bush, ground] + on_ground)
    st.aim(Vector((0.9, -2.3, 1.25)), Vector((0.0, -0.05, 0.3)), 30.0, 1200, 820)
    shot("T40_props_family.png")

    # 6. Silhouettes at 64 px (charte, grid point 1), light and dark grounds.
    cells = ["ND_Basket"] + items + ["ND_HazelCanopy"]
    saved = {}
    for k, name in enumerate(cells):
        o = props[name]
        saved[name] = (o.location.copy(), o.rotation_euler.copy(), o.scale.copy())
        extent = max(o.dimensions) if name != "ND_HazelCanopy" else 3.9
        o.rotation_euler = (0.0, 0.0, 0.0)
        o.scale = (0.08 / extent,) * 3
        corners = [Vector(c) * (0.08 / extent) for c in o.bound_box]
        middle = Vector([(min(c[a] for c in corners) + max(c[a] for c in corners)) * 0.5 for a in range(3)])
        o.location = Vector((k * 0.1, 0.0, 0.0)) - middle
    trunk.scale = props["ND_HazelCanopy"].scale.copy()
    trunk.location = props["ND_HazelCanopy"].location.copy()
    st.show([props[n] for n in cells] + [trunk])
    previous = scene.render.engine
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = 'FLAT'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.0, 0.0, 0.0)
    cam = st.cam
    cam.data.type = 'ORTHO'
    cam.data.sensor_fit = 'HORIZONTAL'
    cam.data.ortho_scale = 0.1 * len(cells)
    cam.location = (0.1 * (len(cells) - 1) * 0.5, -0.6, 0.6 * math.tan(math.radians(20.0)))
    cam.rotation_euler = (math.radians(70.0), 0.0, 0.0)
    scene.render.resolution_x = 64 * len(cells)
    scene.render.resolution_y = 64
    for ground_name, color in (("light", "EDEDE6"), ("dark", "2E3440")):
        st.world.use_nodes = False
        st.world.color = tuple(_nd_linear(color))
        shot(f"T40_props_silhouettes_{ground_name}.png")
    st.world.use_nodes = True
    scene.render.engine = previous
    for name, (loc, rot, scl) in saved.items():
        props[name].location, props[name].rotation_euler, props[name].scale = loc, rot, scl

    for o in props.values():
        place(o, (0.0, 0.0, 0.0))
    st.remove()
    return files
