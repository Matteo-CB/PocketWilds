"""T47 accessories: rigid props worn on the animal sockets (doc Accessoires et tenues). Source of
SourceArt/Blender/Accessories/BL_Accessories.blend and of the FBX in Assets/_Game/Art/Props/Accessories/.

Socket space (the convention every accessory follows)
- The sockets SOCK_HeadTop, SOCK_Neck, SOCK_Back, SOCK_TailBase, SOCK_Ear_L/R are empties parented to the family bones
  (pw_species.place_sockets, pw_rig.SOCKETS). They are world-aligned at rest, with the same axes in every species of
  SmallQuadruped and MediumQuadruped: Blender +Z up, -Y the animal's forward (its face), X across the body.
- HeadTop sits on the top of the skull surface, Back on the top of the back surface (ray cast from above), Neck and
  TailBase inside the body, Ear_L/R at the ear bones.
- Each accessory is modeled in that space: object origin = the attach point at the socket, no rotation, no scale,
  +Z up, -Y toward the face. In Unity (pw_export writes Blender (x, y, z) as Unity (-x, z, -y)) the mesh arrives with
  +Y up and +Z forward, so it is a rigid child of the socket with an identity local transform; the per-family
  fitting profile and the per-species fit only add an offset and a uniform scale.
- Nothing enters the animal: build_all() fits each accessory at rest on the Field Hamster LOD0 (the companion, the
  first wearer), lifting it until every vertex and face center keeps FIT_CLEARANCE above the skin and the ears.
  The measured lift is baked into the mesh and printed. Other species get their offset from the fitting profile.

Shared material data (one accessory material for every accessory, SH_EnvironmentOpaque contract): color attribute
PW_Regions (R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x occlusion, y 0).

ACC_FlowerCrown (cosmetic.flower_crown, socket HeadTop, under 350 triangles): a chunky oval ring tilted 28 degrees to
follow the forehead, sitting in front of the ears, six round five-petal flowers on top and six soft leaves between them.
Regions: base = ring and leaves, R = petals, G = flower centers.

    palette   base (ring, leaves)   R (petals)   G (flower centers)
    meadow    7FB069                F8F4EA       F2C94C
    blossom   8CBF7A                F3BDD3       F7DF7A
    frost     A9C9C4                DDEBFA       C9B8F0

ACC_SeedBasket (accessory.fieldhamster.seedbasket, socket Back, about 180 triangles): a small round woven basket with a
soft weave ripple, a rim band and three sunflower seeds on a mound. Concave bottom so it rests on a round back.
Regions: base = weave (C99A62), R = rim and foot band (A87A48), G = seeds (F0D9A0, accent 5E4A3A).

Usage inside Blender:
    import pw_accessories as pa
    pa.new_file()            # own call: empty file saved as BL_Accessories.blend
    pa.build_all()           # builds, fits on the hamster, returns triangles, sizes and lifts
    pa.export_all()          # ACC_FlowerCrown.fbx, ACC_SeedBasket.fbx (pw_export preset); saves the blend
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

BLEND = "SourceArt/Blender/Accessories/BL_Accessories.blend"
FBX_DIR = "Assets/_Game/Art/Props/Accessories"
BUDGETS = {"ACC_FlowerCrown": 350, "ACC_SeedBasket": 200}
FIT_CLEARANCE = 0.003
# Field Hamster fitting reference: the LOD0 mesh and its sockets as placed by pw_species.place_sockets (measured).
FIT_BLEND = "SourceArt/Blender/Animals/FieldHamster/BL_Animal_FieldHamster.blend"
FIT_MESH = "SKM_FieldHamster_LOD0"
FIT_SOCKETS = {"HeadTop": (0.0, -0.107, 0.255), "Back": (0.0, 0.040, 0.260)}

CROWN_OFFSET_Y = -0.048     # the ring sits on the forehead, clear in front of the ears
CROWN_PITCH = 28.0          # degrees, front lower
UP = Vector((0.0, 0.0, 1.0))


def _root():
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.dirname(os.path.dirname(os.path.dirname(here)))


def new_file():
    bpy.ops.wm.read_homefile(use_empty=True)
    path = os.path.join(_root(), *BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path)
    return path


def _collections():
    scene = bpy.context.scene
    out = []
    for name in ("EXPORT", "WORK"):
        col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if col.name not in scene.collection.children:
            scene.collection.children.link(col)
        out.append(col)
    return out


class _Tags:
    """Region tag per vertex while a bmesh is built (to_mesh keeps the vertex order)."""

    def __init__(self, bm):
        self.bm = bm
        self.tags = []

    def mark(self, tag):
        self.tags.extend([tag] * (len(self.bm.verts) - len(self.tags)))


# ---------------------------------------------------------------- fitting

def _reference(socket):
    """The hamster LOD0 in socket space (WORK_Ref_FieldHamster, hidden) and its BVH."""
    name = "WORK_Ref_FieldHamster"
    obj = bpy.data.objects.get(name)
    if obj is None:
        path = os.path.join(_root(), *FIT_BLEND.split("/"))
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.meshes = [FIT_MESH]
        me = dst.meshes[0]
        me.name = name
        obj = bpy.data.objects.new(name, me)
        _collections()[1].objects.link(obj)
        obj.hide_viewport = True
        obj.hide_render = True
    verts = [v.co - Vector(FIT_SOCKETS[socket]) for v in obj.data.vertices]
    polys = [tuple(p.vertices) for p in obj.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def _fit_lift(bm, bvh, lo=-0.02, hi=0.05, step=0.0005):
    """Smallest lift along +Z that keeps every vertex and face center FIT_CLEARANCE outside the reference."""
    pts = [v.co.copy() for v in bm.verts] + [f.calc_center_median() for f in bm.faces]
    for k in range(int(round((hi - lo) / step)) + 1):
        dz = lo + k * step
        ok = True
        for p in pts:
            q = p + Vector((0.0, 0.0, dz))
            loc, nor, _, dist = bvh.find_nearest(q)
            if loc is not None and (q - loc).dot(nor) < FIT_CLEARANCE:
                ok = False
                break
        if ok:
            return dz
    raise RuntimeError("No lift found within %.3f m" % hi)


def _finish(name, bm, tags, regions, occlusion, col, socket):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    lift = _fit_lift(bm, _reference(socket))
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, 0.0, lift))
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old)
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    attr = me.color_attributes.get("PW_Regions") or me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="PW_Occlusion")
    per_vertex = []
    for v in me.vertices:
        attr.data[v.index].color = regions[tags.tags[v.index]]
        per_vertex.append((occlusion(v.co, tags.tags[v.index]), 0.0))
    for loop in me.loops:
        uv.data[loop.index].uv = per_vertex[loop.vertex_index]
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    obj["pw_socket"] = "SOCK_" + socket
    obj["pw_fit_lift"] = round(lift, 4)
    return obj, lift


# ---------------------------------------------------------------- flower crown

def _flower(bm, center, normal, radius, turn):
    """Five round petals as a closed lens (20 triangles), then a domed heart (4 triangles).
    Returns the vertex count of the petals."""
    u = normal.orthogonal().normalized()
    w = normal.cross(u).normalized()
    outline = []
    for k in range(5):
        for da, r in ((0.0, 1.0), (0.5, 0.52)):
            a = turn + (k + da) * math.tau / 5
            outline.append(center + (u * math.cos(a) + w * math.sin(a)) * (radius * r) + normal * (0.15 * radius * r * r))
    ring = [bm.verts.new(p) for p in outline]
    top = bm.verts.new(center + normal * radius * 0.12)
    bottom = bm.verts.new(center - normal * radius * 0.12)
    n = len(ring)
    for k in range(n):
        m = (k + 1) % n
        bm.faces.new((top, ring[k], ring[m]))
        bm.faces.new((bottom, ring[m], ring[k]))
    return n + 2


def _heart(bm, center, normal, radius, turn):
    u = normal.orthogonal().normalized()
    w = normal.cross(u).normalized()
    hr = radius * 0.32
    ring = [bm.verts.new(center + (u * math.cos(a) + w * math.sin(a)) * hr + normal * radius * 0.10)
            for a in (turn + j * math.tau / 4 for j in range(4))]
    apex = bm.verts.new(center + normal * radius * 0.30)
    for j in range(4):
        bm.faces.new((apex, ring[j], ring[(j + 1) % 4]))


def _leaf(bm, base, direction, side, normal, length, width):
    """A soft leaf: diamond outline with a raised middle on both faces (8 triangles)."""
    tip = base + direction * length
    mid = base + direction * (length * 0.45)
    s1 = bm.verts.new(mid + side * width)
    s2 = bm.verts.new(mid - side * width)
    b = bm.verts.new(base)
    t = bm.verts.new(tip)
    mt = bm.verts.new(mid + normal * width * 0.28)
    mb = bm.verts.new(mid - normal * width * 0.28)
    for a, c in ((b, s1), (s1, t), (t, s2), (s2, b)):
        bm.faces.new((a, c, mt))
        bm.faces.new((c, a, mb))


def build_flower_crown(col):
    bm = bmesh.new()
    tags = _Tags(bm)
    ax, ay, tube, seg, sides = 0.030, 0.022, 0.0062, 14, 5
    frame = Matrix.Translation((0.0, CROWN_OFFSET_Y, 0.0)) @ Matrix.Rotation(math.radians(CROWN_PITCH), 4, 'X')
    rot = frame.to_3x3()

    def centerline(th):
        c = Vector((ax * math.cos(th), ay * math.sin(th), 0.0))
        n = Vector((math.cos(th) / ax, math.sin(th) / ay, 0.0)).normalized()
        t = Vector((-ax * math.sin(th), ay * math.cos(th), 0.0)).normalized()
        return frame @ c, rot @ n, rot @ t

    grid = []
    for i in range(seg):
        th = math.tau * i / seg - math.pi / 2
        # A gentle chunky wobble: the ring is hand rolled, never a perfect torus.
        r = tube * (1.0 + 0.10 * math.sin(3 * th + 0.7))
        c, n, _ = centerline(th)
        z = rot @ UP
        grid.append([bm.verts.new(c + n * r * math.cos(ph) + z * r * math.sin(ph))
                     for ph in (math.tau * j / sides for j in range(sides))])
    for i in range(seg):
        a, b = grid[i], grid[(i + 1) % seg]
        for j in range(sides):
            k = (j + 1) % sides
            bm.faces.new((a[j], b[j], b[k], a[k]))
    tags.mark(0)
    z = rot @ UP
    for k in range(6):
        th = -math.pi / 2 + k * math.tau / 6
        c, n, t = centerline(th)
        normal = (n * 0.35 + z).normalized()
        radius = 0.0118 if k % 2 == 0 else 0.0102
        center = c + z * tube * 0.95 + n * tube * 0.15
        turn = 0.4 * k
        _flower(bm, center, normal, radius, turn)
        tags.mark(1)
        _heart(bm, center, normal, radius, turn)
        tags.mark(2)
        # A leaf between this flower and the next, lying along the ring and leaning out.
        lc, ln, lt = centerline(th + math.tau / 12)
        _leaf(bm, lc + z * tube * 0.55 + ln * tube * 0.5, (lt * 0.8 + ln * 0.6).normalized(), (z * 0.6 - ln * 0.3).normalized(),
              (ln * 0.4 + z).normalized(), 0.016, 0.0055)
        tags.mark(0)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_FlowerCrown", bm, tags, regions, lambda co, tag: 0.18 if tag == 0 else 0.0, col, "HeadTop")


# ---------------------------------------------------------------- seed basket

def build_seed_basket(col):
    bm = bmesh.new()
    tags = _Tags(bm)
    seg = 12

    def ring(radius, z, wobble=0.0):
        return [bm.verts.new((math.cos(a) * (radius + (wobble if i % 2 else -wobble)), math.sin(a) * (radius + (wobble if i % 2 else -wobble)), z))
                for i, a in enumerate(math.tau * k / seg + 0.13 for k in range(seg))]

    def band(lo, hi):
        for k in range(seg):
            m = (k + 1) % seg
            bm.faces.new((lo[k], lo[m], hi[m], hi[k]))

    base = ring(0.022, 0.0)
    tags.mark(1)
    weave = ring(0.0275, 0.015, 0.0012)    # the weave ripple: every other strand stands out
    tags.mark(0)
    rim_out = ring(0.0310, 0.030)
    rim_in = ring(0.0270, 0.031)
    tags.mark(1)
    fill_edge = ring(0.0250, 0.022)
    tags.mark(0)
    band(base, weave)
    band(weave, rim_out)
    band(rim_out, rim_in)
    band(rim_in, fill_edge)
    apex = bm.verts.new((0.0, 0.0, 0.0265))
    bottom = bm.verts.new((0.0, 0.0, 0.004))   # concave foot: rests on its edge on a round back
    tags.mark(0)
    for k in range(seg):
        m = (k + 1) % seg
        bm.faces.new((fill_edge[k], fill_edge[m], apex))
        bm.faces.new((base[m], base[k], bottom))
    # Three sunflower seeds standing in the mound, tips up and leaning out.
    for center, lean in (((0.007, 0.004, 0.031), (0.35, 0.2, 1.0)), ((-0.008, 0.003, 0.0315), (-0.4, 0.1, 1.0)),
                         ((0.0, -0.009, 0.0305), (0.05, -0.45, 1.0))):
        _seed(bm, Vector(center), Vector(lean).normalized())
    tags.mark(2)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}

    def occlusion(co, tag):
        if tag == 2:
            return 0.0
        return 0.35 if co.z < 0.006 else (0.25 if co.length < 0.026 and co.z > 0.02 else 0.0)

    return _finish("ACC_SeedBasket", bm, tags, regions, occlusion, col, "Back")


def _seed(bm, center, axis, segments=5, rings=3):
    """A plump sunflower seed: a low ellipsoid along axis (20 triangles), the tip a little pointed."""
    w = axis
    u = w.orthogonal().normalized()
    v = w.cross(u).normalized()
    rx, ry, rz = 0.0046, 0.0030, 0.0085

    def point(x, y, z):
        taper = 1.0 - 0.25 * max(0.0, z)
        return center + u * (x * rx * taper) + v * (y * ry * taper) + w * (z * rz)

    top = bm.verts.new(point(0, 0, 1))
    grid = []
    for j in range(1, rings):
        phi = math.pi * j / rings
        grid.append([bm.verts.new(point(math.sin(phi) * math.cos(t), math.sin(phi) * math.sin(t), math.cos(phi)))
                     for t in (math.tau * k / segments for k in range(segments))])
    bot = bm.verts.new(point(0, 0, -1))
    for k in range(segments):
        m = (k + 1) % segments
        bm.faces.new((top, grid[0][k], grid[0][m]))
        bm.faces.new((bot, grid[-1][m], grid[-1][k]))
    for a, b in zip(grid, grid[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            bm.faces.new((a[k], b[k], b[m], a[m]))


# ---------------------------------------------------------------- build, export

def build_all():
    col, work = _collections()
    report = {}
    for builder in (build_flower_crown, build_seed_basket):
        obj, lift = builder(col)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        if tris > BUDGETS[obj.name]:
            raise RuntimeError(f"{obj.name}: {tris} triangles over {BUDGETS[obj.name]}")
        pts = [Vector(c) for c in obj.bound_box]
        size = tuple(round(max(p[a] for p in pts) - min(p[a] for p in pts), 3) for a in range(3))
        report[obj.name] = dict(tris=tris, size=size, lift=round(lift, 4), min_z=round(min(p.z for p in pts), 4))
    bpy.ops.wm.save_mainfile()
    return report


def export_all():
    ns = {}
    exec(open(os.path.join(_root(), "SourceArt", "Blender", "Tools", "pw_export.py")).read(), ns)
    out = [ns["export_static"](f"{FBX_DIR}/{name}.fbx", (name,))[0] for name in ("ACC_FlowerCrown", "ACC_SeedBasket")]
    bpy.ops.wm.save_mainfile()
    return out
