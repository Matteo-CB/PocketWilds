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
  first wearer) along its rest axis: Up (HeadTop and Back pieces, and the Neck pieces on the nape) lifts it until every
  vertex and face center keeps FIT_CLEARANCE above the skin and the ears; Forward (a piece worn on the chest: none since
  the T49 nape pieces, the chin and front legs cross the chest in the eat, sleep and inspect poses) pushes it along -Y
  until it keeps FIT_CLEARANCE in front of the chest. The measured lift or push is baked into the mesh and printed. In
  Unity, AccessoryFitPipeline searches each species' scale, offset and lift (or push) along the same axis over every
  clip pose.

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

T49 outfits of L'Éveil des Terriers (event.burrows, O-OUT-103-01..07). All rigid like the T47 ones: nothing wraps
around the body, so one shape rests on very different species (scale and offset per species). The goggles sit on the
head top with two short side clips (no strap behind the head); the scarf (a knot, two folds, two short tails) and the
overalls' strap junction rest on the nape, the top of the neck behind the head, which the three-quarter camera from
above shows best (rest axis Up, undersides on the nape surface NAPE_Z / NAPE_RX / NAPE_RY); the shovel lies along
the spine on a little pad with a band and a tiny buckle; the straps are a crossed X bent to a round saddle. Regions as
above.

    piece (outfit, socket)                    palette   base                R                    G                  B
    ACC_SproutHat (01, HeadTop)               default   A8744E cap          F4EBD6 band          86CC5A sprout      -
                                              gold      E2B04A              FFF0C4               F2D27A             -
    ACC_SeedSack (02, Back)                   default   D8B98A burlap       7A5A3E cord          F0D9A0 seeds       8DC26F patch
                                              gold      E3B955              B8862C               FFF0C2             F5D77A
    ACC_BurrowGoggles (03, HeadTop)           default   8A5A3C side clips   D7A24A rims, bridge  A9DCE6 lenses      -
                                              gold      B9832F              E8BE55               FFF0BF             -
    ACC_GardenerScarf (04, Neck, nape)       default   E07B5A fabric       A9D47E leaf print    F4E3C1 hem         -
                                              gold      E2B54C              FFF1C9               C2902F             -
    ACC_MinerHelmet (05, HeadTop)             default   F2C14E shell        D9A33A brim, crest   6E6A66 lamp body   FFF2B3 lamp lens
                                              gold (05) E6B43F              C08A2A               F0D27A             FFF6D6
    ACC_GoldenShovel (06, Back)               default   8A5A3C pad, band    C9925A handle, grip  B8C2C8 blade, buckle -
                                              gold (06) A8742E              E2B04A               F5D06A             -
    ACC_OverallsBib (07, Neck, nape)         default   7E9FC4 denim        E0B04E buttons       6B8DB5 inset panel -
                                              gold      D9AE4C              FFF0C4               C4962F             -
    ACC_OverallsStraps (07, Back)             default   7E9FC4 denim        E0B04E buttons       6B8DB5 cross patch -
                                              gold      D9AE4C              FFF0C4               C4962F             -

The miner lamp is a painted round lens (no light). The Bib (the strap junction at the nape: a soft patch, two buttons
and two strap stubs toward the shoulders) and the Straps (back) of outfit 07 are two separate rigid pieces on two
sockets that read as one pair of straps from above; the strap ends finish in round buttons.

Usage inside Blender:
    import pw_accessories as pa
    pa.new_file()            # own call: empty file saved as BL_Accessories.blend
    pa.build_all()           # builds, fits on the hamster, returns triangles, sizes and lifts (only=[names] for some)
    pa.export_all()          # one FBX per accessory (pw_export preset), names=[...] for some; saves the blend
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

BLEND = "SourceArt/Blender/Accessories/BL_Accessories.blend"
FBX_DIR = "Assets/_Game/Art/Props/Accessories"
BUDGETS = {"ACC_FlowerCrown": 350, "ACC_SeedBasket": 200,
           # T49 (small 40-250, medium 150-500, large back under 700)
           "ACC_SproutHat": 250, "ACC_SeedSack": 500, "ACC_BurrowGoggles": 500, "ACC_GardenerScarf": 500,
           "ACC_MinerHelmet": 500, "ACC_GoldenShovel": 700, "ACC_OverallsBib": 500, "ACC_OverallsStraps": 500}
FIT_CLEARANCE = 0.003
# Field Hamster fitting reference: the LOD0 mesh and its sockets as placed by pw_species.place_sockets. Checked in T49
# against the SOCK_* empties of BL_Animal_FieldHamster.blend and SKM_FieldHamster.fbx at rest (equal to 1 mm).
FIT_BLEND = "SourceArt/Blender/Animals/FieldHamster/BL_Animal_FieldHamster.blend"
FIT_MESH = "SKM_FieldHamster_LOD0"
FIT_SOCKETS = {"HeadTop": (0.0, -0.107, 0.255), "Back": (0.0, 0.040, 0.260), "Neck": (0.0, -0.070, 0.140)}
T47_PIECES = ("ACC_FlowerCrown", "ACC_SeedBasket")
T49_PIECES = ("ACC_SproutHat", "ACC_SeedSack", "ACC_BurrowGoggles", "ACC_GardenerScarf", "ACC_MinerHelmet",
              "ACC_GoldenShovel", "ACC_OverallsBib", "ACC_OverallsStraps")
HAT_OFFSET_Y, HAT_PITCH = -0.046, 22.0          # sprout hat: on the forehead, in front of the ears
HELMET_OFFSET_Y, HELMET_PITCH = -0.050, 24.0

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


def _fit_push(bm, bvh, lo=-0.02, hi=0.05, step=0.0005):
    """Smallest push along the socket's forward (-Y) that keeps every vertex and face center FIT_CLEARANCE outside."""
    pts = [v.co.copy() for v in bm.verts] + [f.calc_center_median() for f in bm.faces]
    for k in range(int(round((hi - lo) / step)) + 1):
        dy = lo + k * step
        ok = True
        for p in pts:
            q = p + Vector((0.0, -dy, 0.0))
            loc, nor, _, _ = bvh.find_nearest(q)
            if loc is not None and (q - loc).dot(nor) < FIT_CLEARANCE:
                ok = False
                break
        if ok:
            return dy
    raise RuntimeError("No push found within %.3f m" % hi)


def _finish(name, bm, tags, regions, occlusion, col, socket, fit="lift"):
    """Fits the piece at rest on the hamster and bakes it: fit "lift" (rest axis Up) the smallest lift along +Z, fit
    "push" (rest axis Forward) the smallest push along -Y. regions: {tag: rgba} or a function (co, tag, index) -> rgba."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bvh = _reference(socket)
    if fit == "lift":
        lift = _fit_lift(bm, bvh)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, 0.0, lift))
    else:
        lift = _fit_push(bm, bvh)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, -lift, 0.0))
    ground = FIT_SOCKETS[socket][2] + min(v.co.z for v in bm.verts)
    if ground < 0.003:
        raise RuntimeError("%s: %.1f mm above the ground" % (name, ground * 1000))
    if callable(regions):
        bm.verts.index_update()
        region_of = [regions(v.co, tags.tags[v.index], v.index) for v in bm.verts]
    else:
        region_of = [regions[t] for t in tags.tags]
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
        attr.data[v.index].color = region_of[v.index]
        per_vertex.append((occlusion(v.co, tags.tags[v.index]), 0.0))
    for loop in me.loops:
        uv.data[loop.index].uv = per_vertex[loop.vertex_index]
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    obj["pw_socket"] = "SOCK_" + socket
    obj["pw_fit_lift"] = round(lift, 4)
    obj["pw_rest_axis"] = "Up" if fit == "lift" else "Forward"
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


# ---------------------------------------------------------------- T49 helpers

SECTION6 = ((-0.5, 0.0), (-0.3, 0.5), (0.3, 0.5), (0.5, 0.0), (0.3, -0.5), (-0.3, -0.5))   # soft flat strap
SECTION4 = ((-0.5, 0.0), (0.0, 0.5), (0.5, 0.0), (0.0, -0.5))


def _lathe(bm, tags, profile, seg, frame, marks, wobble=None):
    """Revolves (r, h) pairs listed from the bottom center outward and up (poles where r is 0) around frame's +Z."""
    rings = []
    for (r, h), tag in zip(profile, marks):
        if r <= 1e-9:
            rings.append([bm.verts.new(frame @ Vector((0.0, 0.0, h)))])
        else:
            ring = []
            for k in range(seg):
                a = math.tau * k / seg
                f = wobble(a, h) if wobble else 1.0
                ring.append(bm.verts.new(frame @ Vector((math.cos(a) * r * f, math.sin(a) * r * f, h))))
            rings.append(ring)
        tags.mark(tag)
    for lo, hi in zip(rings, rings[1:]):
        for k in range(seg):
            m = (k + 1) % seg
            if len(lo) == 1 and len(hi) > 1:
                bm.faces.new((lo[0], hi[m], hi[k]))
            elif len(hi) == 1 and len(lo) > 1:
                bm.faces.new((lo[k], lo[m], hi[0]))
            elif len(lo) > 1:
                bm.faces.new((lo[k], lo[m], hi[m], hi[k]))
    return rings


def _axis_frame(origin, axis):
    w = axis.normalized()
    u = w.orthogonal().normalized()
    v = w.cross(u)
    m = Matrix((u, v, w)).transposed().to_4x4()
    m.translation = origin
    return m


def _tube(bm, pts, radii, sides, cap0=True, cap1=True):
    """A soft tube through pts (rotation-minimizing frames), rounded caps."""
    n = len(pts)
    tangents = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    normal = tangents[0].orthogonal().normalized()
    rings = []
    for i in range(n):
        t = tangents[i]
        normal = (normal - t * normal.dot(t)).normalized()
        b = t.cross(normal)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        rings.append([bm.verts.new(pts[i] + (normal * math.cos(a) + b * math.sin(a)) * r)
                      for a in (math.tau * k / sides for k in range(sides))])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((r0[k], r0[m], r1[m], r1[k]))
    for cap, ring, sign in ((cap0, rings[0], -1.0), (cap1, rings[-1], 1.0)):
        if not cap:
            continue
        i = 0 if sign < 0 else n - 1
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        tip = bm.verts.new(pts[i] + tangents[i] * (sign * r * 0.7))
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((ring[m], ring[k], tip) if sign < 0 else (ring[k], ring[m], tip))
    return rings


def _torus(bm, center, axis, major, minor, seg, sides):
    f = _axis_frame(center, axis)
    rings = []
    for i in range(seg):
        a = math.tau * i / seg
        c = Vector((math.cos(a), math.sin(a), 0.0))
        rings.append([bm.verts.new(f @ (c * (major + minor * math.cos(b)) + Vector((0.0, 0.0, minor * math.sin(b)))))
                      for b in (math.tau * k / sides for k in range(sides))])
    for i in range(seg):
        r0, r1 = rings[i], rings[(i + 1) % seg]
        for k in range(sides):
            m = (k + 1) % sides
            bm.faces.new((r0[k], r1[k], r1[m], r0[m]))


def _dome(bm, center, axis, radius, height, seg=8):
    """A flattened round button: an ellipsoid of 2 * seg + 2 * seg triangles around axis."""
    f = _axis_frame(center, axis)
    top = bm.verts.new(f @ Vector((0.0, 0.0, height)))
    mid = [bm.verts.new(f @ Vector((math.cos(a) * radius, math.sin(a) * radius, height * 0.45)))
           for a in (math.tau * k / seg for k in range(seg))]
    low = [bm.verts.new(f @ Vector((math.cos(a) * radius * 0.82, math.sin(a) * radius * 0.82, -height * 0.4)))
           for a in (math.tau * k / seg for k in range(seg))]
    bot = bm.verts.new(f @ Vector((0.0, 0.0, -height * 0.6)))
    for k in range(seg):
        m = (k + 1) % seg
        bm.faces.new((mid[k], mid[m], top))
        bm.faces.new((low[k], low[m], mid[m], mid[k]))
        bm.faces.new((low[m], low[k], bot))


def _round_leaf(bm, base, direction, normal, length, width, curl=0.25):
    """A tender rounded leaf: a closed lens of 10 outline points (20 triangles), the tip round, curling up."""
    d = direction.normalized()
    n = (normal - d * normal.dot(d)).normalized()
    s = n.cross(d)

    def at(t, side):
        w = width * math.sin(math.pi * min(t, 0.999)) ** 0.75
        return base + d * (length * t) + s * (side * w) + n * (curl * length * t * t)

    outline = [base] + [at(t, 1.0) for t in (0.16, 0.42, 0.68, 0.88)] + [at(1.0, 0.0)] + \
              [at(t, -1.0) for t in (0.88, 0.68, 0.42, 0.16)]
    ring = [bm.verts.new(p) for p in outline]
    c = at(0.45, 0.0)
    top = bm.verts.new(c + n * width * 0.22)
    bot = bm.verts.new(c - n * width * 0.16)
    for k in range(len(ring)):
        m = (k + 1) % len(ring)
        bm.faces.new((ring[m], ring[k], top))
        bm.faces.new((ring[k], ring[m], bot))


def _band(bm, tags, stations, width, thick, tag, closed=False, section=SECTION6, axis=None, lift=None,
          hem_tag=None, leaf_cells=(), leaf_tag=None, widths=None, out=None):
    """A soft strap along stations [(point, normal)]: its center `out` along the normal (default FIT_CLEARANCE + 1.2 mm
    + half the thickness: a strap lying on a skin point), plus lift[i]. leaf_cells: segments whose outer middle face
    carries a leaf-shaped print (an island 0.4 mm proud)."""
    n = len(stations)
    base = FIT_CLEARANCE + 0.0012 + thick * 0.5 if out is None else out
    centers = [loc + nor * (base + (lift[i] if lift else 0.0)) for i, (loc, nor) in enumerate(stations)]
    rings = []
    for i in range(n):
        if closed:
            t = centers[(i + 1) % n] - centers[i - 1]
        else:
            t = centers[min(i + 1, n - 1)] - centers[max(i - 1, 0)]
        t.normalize()
        nor = stations[i][1]
        if axis is not None:
            side = (axis - t * axis.dot(t)).normalized()
            up = t.cross(side)
            if up.dot(nor) < 0:
                up = -up
        else:
            side = nor.cross(t).normalized()
            up = t.cross(side)
        w = widths[i] if widths else width
        ring = []
        for sx, sz in section:
            ring.append(bm.verts.new(centers[i] + side * (sx * w) + up * (sz * thick)))
            tags.tags.append(hem_tag if (hem_tag is not None and sz == 0.0) else tag)
        rings.append(ring)
    m = len(section)
    for i in range(n if closed else n - 1):
        a, b = rings[i], rings[(i + 1) % n]
        for j in range(m):
            k = (j + 1) % m
            if i in leaf_cells and j == 1 and m == 6:
                _leaf_print(bm, tags, a[1], b[1], b[2], a[2], tag, leaf_tag)
            else:
                bm.faces.new((a[j], b[j], b[k], a[k]))
    if not closed:
        for ring, i, sign in ((rings[0], 0, -1.0), (rings[-1], n - 1, 1.0)):
            t = (centers[1] - centers[0]) if sign < 0 else (centers[-1] - centers[-2])
            tip = bm.verts.new(centers[i] + t.normalized() * (sign * max(thick, 0.004) * 0.6))
            tags.tags.append(tag)
            for j in range(m):
                q = (j + 1) % m
                bm.faces.new((ring[q], ring[j], tip) if sign < 0 else (ring[j], ring[q], tip))
    return rings, centers


def _leaf_print(bm, tags, a0, b0, b1, a1, tag, leaf_tag):
    """Replaces the quad a0 b0 b1 a1 by a border around a leaf-shaped diamond, and lays the leaf island on it."""
    c = (a0.co + b0.co + b1.co + a1.co) * 0.25
    along = (b0.co + b1.co - a0.co - a1.co) * 0.5
    across = (a1.co + b1.co - a0.co - b0.co) * 0.5
    normal = along.cross(across).normalized()
    shape = (c - along * 0.42, c - across * 0.30, c + along * 0.42, c + across * 0.30)   # L, T, R, B
    border = []
    for p in shape:
        border.append(bm.verts.new(p))
        tags.tags.append(tag)
    L, T, R, B = border
    for face in ((a0, b0, T), (b0, b1, R), (b1, a1, B), (a1, a0, L), (a0, T, L), (b0, R, T), (b1, B, R), (a1, L, B),
                 (L, T, R, B)):
        bm.faces.new(face)
    leaf = []
    for p in shape:
        leaf.append(bm.verts.new(p + normal * 0.0004))
        tags.tags.append(leaf_tag)
    bm.faces.new(leaf)


def _simple_occlusion(low=0.25):
    return lambda co, tag: low if tag == 0 else 0.0


# ---------------------------------------------------------------- sprout hat (outfit 01)

def build_sprout_hat(col):
    """A little round felt cap in soil brown with a cream band, two tender sprout leaves on a short stem."""
    bm = bmesh.new()
    tags = _Tags(bm)
    frame = (Matrix.Translation((0.0, HAT_OFFSET_Y, 0.0)) @ Matrix.Rotation(math.radians(HAT_PITCH), 4, 'X')
             @ Matrix.Diagonal((1.15, 1.06, 1.15, 1.0)))
    profile = [(0.0, 0.0062), (0.0262, 0.0), (0.0277, 0.0042), (0.0262, 0.0079), (0.0236, 0.0106), (0.0186, 0.0186),
               (0.0106, 0.0238), (0.0, 0.0256)]
    _lathe(bm, tags, profile, 12, frame, (0, 1, 1, 1, 0, 0, 0, 0),
           wobble=lambda a, h: 1.0 + 0.025 * math.sin(3 * a + 0.4))
    stem = [frame @ Vector(p) for p in ((0.0, 0.0, 0.0226), (0.0009, -0.0004, 0.0315), (0.0021, -0.0010, 0.0385))]
    _tube(bm, stem, [0.0027, 0.0023, 0.0019], 5, cap0=False, cap1=True)
    rot = frame.to_3x3().normalized()
    for sgn, yaw in ((1.0, 0.25), (-1.0, -0.15)):
        direction = rot @ Vector((sgn * 0.78, -yaw * 0.3, 0.62))
        normal = rot @ Vector((-sgn * 0.62, 0.0, 0.78))
        _round_leaf(bm, stem[-1] - rot @ Vector((0.0, 0.0, 0.001)), direction, normal, 0.032, 0.0155, curl=0.22)
    tags.mark(2)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_SproutHat", bm, tags, regions, lambda co, tag: 0.22 if (tag == 0 and co.z < 0.004) else 0.0,
                   col, "HeadTop")


# ---------------------------------------------------------------- miner helmet (outfit 05)

def build_miner_helmet(col):
    """A rounded helmet with a soft brim (wider in front), a crest and a round decorative lamp. No light."""
    bm = bmesh.new()
    tags = _Tags(bm)
    frame = (Matrix.Translation((0.0, HELMET_OFFSET_Y, 0.0)) @ Matrix.Rotation(math.radians(HELMET_PITCH), 4, 'X')
             @ Matrix.Diagonal((1.0, 0.94, 1.0, 1.0)))

    def brim(a, h):
        front = max(0.0, -math.sin(a))
        return 1.0 + (0.16 * front * front if h < 0.0047 else 0.0)

    profile = [(0.0, 0.0085), (0.0272, 0.0016), (0.0340, 0.0), (0.0356, 0.0028), (0.0312, 0.0046), (0.0288, 0.0062),
               (0.0278, 0.0150), (0.0222, 0.0252), (0.0120, 0.0312), (0.0, 0.0326)]
    _lathe(bm, tags, profile, 14, frame, (0, 1, 1, 1, 1, 0, 0, 0, 0, 0), wobble=brim)
    rot = frame.to_3x3()
    crest = []
    for k in range(5):
        th = math.radians(-58 + k * 29)          # over the top, front to back
        crest.append(frame @ Vector((0.0, math.sin(th) * 0.0262, 0.0062 + math.cos(th) * 0.0262 + 0.0016)))
    _tube(bm, crest, [0.0026, 0.0034, 0.0036, 0.0034, 0.0026], 5)
    tags.mark(1)
    lamp_at = frame @ Vector((0.0, -0.0262, 0.0165))
    lamp_axis = (rot @ Vector((0.0, -0.92, 0.36))).normalized()
    lamp = _axis_frame(lamp_at - lamp_axis * 0.003, lamp_axis)
    _lathe(bm, tags, [(0.0, 0.0), (0.0102, 0.0), (0.0112, 0.0062), (0.0100, 0.0088), (0.0084, 0.0086), (0.0, 0.0118)],
           9, lamp, (2, 2, 2, 2, 3, 3))
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0), 3: (0.0, 0.0, 1.0, 0.0)}
    return _finish("ACC_MinerHelmet", bm, tags, regions, lambda co, tag: 0.2 if (tag == 0 and co.z < 0.003) else 0.0,
                   col, "HeadTop")


# ---------------------------------------------------------------- burrow goggles (outfit 03)

GOGGLES_Y, GOGGLES_PITCH = -0.040, 34.0         # on the forehead in front of the ears, lenses looking up and forward
GOGGLES_SIZE = 1.3                               # drawn small, worn with lenses a little larger than the eyes


def build_burrow_goggles(col):
    """Two round goggles pushed up on the forehead, a brass bridge between them and two short leather side clips with a
    round rivet, all resting on the top of the head (no strap behind the head)."""
    bm = bmesh.new()
    tags = _Tags(bm)
    major, minor = 0.0105, 0.0038
    pitch = math.radians(GOGGLES_PITCH)
    inner = {}
    for sgn in (-1.0, 1.0):
        axis = Vector((sgn * 0.16, -math.sin(pitch), math.cos(pitch))).normalized()
        center = Vector((sgn * 0.0165, GOGGLES_Y, minor + 0.0012))
        _torus(bm, center, axis, major, minor, 10, 4)
        tags.mark(1)
        _lathe(bm, tags, [(0.0, -0.0008), (0.0098, 0.0), (0.0, 0.0042)], 10, _axis_frame(center + axis * 0.0004, axis),
               (2, 2, 2))
        side = Vector((sgn, 0.0, 0.0))
        side = (side - axis * side.dot(axis)).normalized()
        inner[sgn] = center - side * major
        # The side clip: a short soft tab from the outer rim, outward and a little back, following the skull down.
        o = center + side * (major + minor * 0.6)
        nor = Vector((sgn * 0.35, 0.0, 0.94)).normalized()
        path = [o, o + Vector((sgn * 0.006, 0.003, -0.0030)), o + Vector((sgn * 0.0115, 0.0055, -0.0065))]
        _band(bm, tags, [(p, nor) for p in path], 0.0075, 0.0028, 0, section=SECTION4, out=0.0)
        _dome(bm, path[-1] + nor * 0.0016, nor, 0.0031, 0.0022, seg=6)
        tags.mark(1)
    il, ir = inner[-1.0], inner[1.0]
    mid = (il + ir) * 0.5 + Vector((0.0, 0.0, 0.0032))
    _tube(bm, [il, mid, ir], 0.0026, 5, cap0=False, cap1=False)
    tags.mark(1)
    bmesh.ops.scale(bm, vec=(GOGGLES_SIZE,) * 3, verts=bm.verts)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_BurrowGoggles", bm, tags, regions, lambda co, tag: 0.15 if tag == 0 else 0.0, col, "HeadTop")


# ---------------------------------------------------------------- nape pieces (outfits 04, 07 strap junction)

NAPE_Y = 0.010                  # Neck socket space: the knot and the strap junction, on the nape behind the head
NAPE_Z = 0.1135                 # drawn just under the hamster's nape (0.116 over SOCK_Neck); the fit lifts it to rest
NAPE_RX, NAPE_RY = 0.13, 0.35   # the nape the undersides follow: round across the neck, almost flat along it
NAPE_CY = 0.022                 # the crest of that nape along the neck (+Y: toward the tail)
SCARF_SIZE, JUNCTION_SIZE = 1.15, 1.15  # drawn small, worn this much larger around the nape point (still lying on it)


def _nape(x, y):
    """Height and normal of the nape surface the nape pieces lie on (Neck socket space)."""
    z = NAPE_Z - x * x / (2 * NAPE_RX) - (y - NAPE_CY) ** 2 / (2 * NAPE_RY)
    return z, Vector((x / NAPE_RX, (y - NAPE_CY) / NAPE_RY, 1.0)).normalized()


def _on_nape(x, y, out=0.0):
    z, n = _nape(x, y)
    return Vector((x, y, z)) + n * out, n


def _rest_on_nape(verts, size=1.0):
    """Scales the piece around the nape point (0, NAPE_Y) along the nape, each vertex keeping its height over it times
    size, then lays the underside of every part on the nape surface: no vertex goes under it."""
    for v in verts:
        h = v.co.z - _nape(v.co.x, v.co.y)[0]
        x, y = v.co.x * size, NAPE_Y + (v.co.y - NAPE_Y) * size
        v.co = Vector((x, y, _nape(x, y)[0] + max(h * size, 0.0)))


def build_gardener_scarf(col):
    """A scarf knotted at the nape: a soft round knot on the top of the neck behind the head, two little folds of the
    cloth tucked into it at the sides (the scarf coming round the neck) and two short tails lying back over the
    shoulders with a leaf print and cream hems. Rigid: nothing goes round the neck; made for the three-quarter view from
    above."""
    bm = bmesh.new()
    tags = _Tags(bm)
    # The two tails, a V opening toward the tail over the shoulders (the knot sits on their roots).
    for sgn, length, spread in ((-1.0, 0.029, 31.0), (1.0, 0.026, 24.0)):
        a = math.radians(spread)
        dx, dy = sgn * math.sin(a), math.cos(a)
        ox, oy = sgn * 0.0030, NAPE_Y + 0.0030
        stations = [_on_nape(ox + dx * length * t, oy + dy * length * t) for t in (0.0, 0.3, 0.6, 0.85, 1.0)]
        _band(bm, tags, stations, 0.012, 0.0032, 0, hem_tag=2, leaf_cells=(1, 2), leaf_tag=1,
              widths=[0.0100, 0.0135, 0.0142, 0.0122, 0.0080], out=0.0020)
    # The two folds, out to the sides and a little forward, lying on the neck.
    for sgn in (-1.0, 1.0):
        base, nor = _on_nape(sgn * 0.0050, NAPE_Y - 0.0005, 0.0018)
        tip = _on_nape(sgn * 0.0185, NAPE_Y - 0.0065, 0.0018)[0]
        _round_leaf(bm, base, tip - base, nor, (tip - base).length, 0.0072, curl=0.08)
        tags.mark(0)
    # The knot: a soft round ball, a little wider than long, a gentle hand-tied wobble.
    seg, rx, ry, rz = 10, 0.0098, 0.0084, 0.0068
    c, nor = _on_nape(0.0, NAPE_Y, 0.0048)
    f = _axis_frame(c, nor)
    top = bm.verts.new(f @ Vector((0.0, 0.0, rz)))
    rings = []
    for k in (1, 2, 3, 4):
        phi = math.pi * k / 5
        rings.append([bm.verts.new(f @ Vector((math.sin(phi) * math.cos(a) * rx * (1.0 + 0.05 * math.sin(2 * a + 0.5)),
                                               math.sin(phi) * math.sin(a) * ry, math.cos(phi) * rz)))
                      for a in (math.tau * j / seg for j in range(seg))])
    bot = bm.verts.new(f @ Vector((0.0, 0.0, -rz)))
    for j in range(seg):
        m = (j + 1) % seg
        bm.faces.new((top, rings[0][j], rings[0][m]))
        bm.faces.new((bot, rings[-1][m], rings[-1][j]))
        for r0, r1 in zip(rings, rings[1:]):
            bm.faces.new((r0[m], r0[j], r1[j], r1[m]))
    tags.mark(0)
    _rest_on_nape(bm.verts, SCARF_SIZE)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_GardenerScarf", bm, tags, regions, lambda co, tag: 0.12 if tag == 0 else 0.0, col, "Neck")


def build_overalls_bib(col):
    """The overalls' strap junction at the nape, continuing ACC_OverallsStraps on the back: a small soft denim patch on
    the top of the neck where the two straps meet, a darker inset panel, two round buttons at its back corners and the
    two strap stubs leaving them toward the shoulders, where the crossed straps of the back begin."""
    bm = bmesh.new()
    tags = _Tags(bm)
    thick = 0.0040                       # as the straps of ACC_OverallsStraps
    for sgn in (-1.0, 1.0):              # the stubs, back and out; they start under the patch
        pts = [(sgn * 0.0060, NAPE_Y + 0.0020), (sgn * 0.0128, NAPE_Y + 0.0140), (sgn * 0.0196, NAPE_Y + 0.0260),
               (sgn * 0.0255, NAPE_Y + 0.0365)]
        _band(bm, tags, [_on_nape(x, y) for x, y in pts], 0.0150, thick, 0, out=thick * 0.5 + 0.0003)
    start = len(bm.verts)
    # The patch: a soft cushion, a little wider than long, rounded all round.
    cy = NAPE_Y + 0.0015
    frame = Matrix.Translation((0.0, cy, 0.0)) @ Matrix.Diagonal((1.0, 0.82, 1.0, 1.0))
    profile = [(0.0, 0.0), (0.0150, 0.0), (0.0158, 0.0022), (0.0140, 0.0042), (0.0095, 0.0053), (0.0, 0.0057)]
    _lathe(bm, tags, profile, 14, frame, (0,) * len(profile), wobble=lambda a, h: 1.0 + 0.04 * math.cos(2 * a))
    # The inset panel on its top (a lens 0.4 mm proud).
    ring = [bm.verts.new((math.cos(a) * 0.0072, cy - 0.0008 + math.sin(a) * 0.0050, 0.0058)) for a in
            (math.tau * k / 8 + math.pi / 8 for k in range(8))]
    cap = bm.verts.new((0.0, cy - 0.0008, 0.0062))
    for k in range(8):
        bm.faces.new((ring[k], ring[(k + 1) % 8], cap))
    tags.mark(2)
    for v in list(bm.verts)[start:]:     # the patch lies on the nape
        v.co.z += _nape(v.co.x, v.co.y)[0] + 0.0004
    for sgn in (-1.0, 1.0):              # the buttons where the straps meet the patch
        loc, nor = _on_nape(sgn * 0.0088, cy + 0.0058, 0.0050 + 0.0004)
        _dome(bm, loc + nor * 0.0010, nor, 0.0043, 0.0026)
    tags.mark(1)
    _rest_on_nape(bm.verts, JUNCTION_SIZE)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_OverallsBib", bm, tags, regions, lambda co, tag: 0.12 if tag == 0 else 0.0, col, "Neck")


# ---------------------------------------------------------------- seed sack (outfit 02)

def build_seed_sack(col):
    """A plump burlap sack, gathered and tied with a cord, three seeds peeking out of the ruffle, a round green
    patch sewn on the front. Concave foot: it rests on its edge on a round back."""
    bm = bmesh.new()
    tags = _Tags(bm)
    frame = Matrix.Translation((0.0, 0.004, 0.0)) @ Matrix.Rotation(math.radians(-6.0), 4, 'X')

    def lumps(a, h):
        if h < 0.058:
            return 1.0 + 0.035 * math.sin(3 * a + 0.6) * math.sin(math.pi * min(h, 0.058) / 0.058)
        return 1.0 + 0.16 * math.cos(5 * a + 0.3) if h > 0.064 else 1.0

    profile = [(0.0, 0.0065), (0.0200, 0.0012), (0.0270, 0.0), (0.0340, 0.0080), (0.0375, 0.0220), (0.0355, 0.0370),
               (0.0280, 0.0490), (0.0160, 0.0565), (0.0105, 0.0605), (0.0175, 0.0690), (0.0195, 0.0735),
               (0.0135, 0.0755), (0.0, 0.0715)]
    _lathe(bm, tags, profile, 11, frame, [0] * len(profile), wobble=lumps)
    _torus(bm, frame @ Vector((0.0, 0.0, 0.0607)), frame.to_3x3() @ UP, 0.0118, 0.0029, 10, 4)
    rot = frame.to_3x3()
    for sgn in (-1.0, 1.0):
        a = frame @ Vector((sgn * 0.006, -0.0118, 0.0600))
        b = frame @ Vector((sgn * 0.010, -0.0160, 0.0480))
        _tube(bm, [a, (a + b) * 0.5 + rot @ Vector((0.0, -0.0015, 0.0)), b], 0.0020, 4, cap0=False, cap1=False)
        _dome(bm, b, rot @ Vector((sgn * 0.3, -0.5, -0.8)), 0.0034, 0.0034, seg=6)
    tags.mark(1)
    for center, lean in (((0.0045, 0.0030, 0.0770), (0.35, 0.2, 1.0)), ((-0.0050, 0.0010, 0.0765), (-0.4, 0.1, 1.0)),
                         ((0.0, -0.0060, 0.0758), (0.05, -0.45, 1.0))):
        _seed(bm, frame @ Vector(center), (rot @ Vector(lean)).normalized())
    tags.mark(2)
    # The round patch, sewn on the front (-Y) of the belly.
    pc = frame @ Vector((0.0, -0.0372, 0.026))
    pn = (rot @ Vector((0.0, -1.0, 0.05))).normalized()
    pf = _axis_frame(pc, pn)
    ring = [bm.verts.new(pf @ Vector((math.cos(a) * 0.0115, math.sin(a) * 0.0115, 0.0004))) for a in
            (math.tau * k / 8 for k in range(8))]
    cap = bm.verts.new(pf @ Vector((0.0, 0.0, 0.0016)))
    for k in range(8):
        bm.faces.new((ring[k], ring[(k + 1) % 8], cap))
    tags.mark(3)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0), 3: (0.0, 0.0, 1.0, 0.0)}

    def occlusion(co, tag):
        if tag != 0:
            return 0.0
        return 0.32 if co.z < 0.006 else (0.22 if 0.056 < co.z < 0.064 else 0.0)

    return _finish("ACC_SeedSack", bm, tags, regions, occlusion, col, "Back")


# ---------------------------------------------------------------- golden shovel (outfit 06)

SHOVEL_SIZE = 1.1       # drawn at 0.128 m, worn at 0.141 m along the spine

def build_golden_shovel(col):
    """A small round-bladed shovel lying along the spine, blade toward the tail and a round grip toward the head,
    held on a soft leather pad by a little band with a tiny buckle. Nothing goes around the body."""
    bm = bmesh.new()
    tags = _Tags(bm)
    along, side = Vector((0.0, 1.0, 0.0)), Vector((1.0, 0.0, 0.0))     # +Y: toward the tail
    hz, hr = 0.0048, 0.0036                                            # handle axis height and radius (sunk in the pad)
    pad = Matrix.Translation((0.0, -0.006, 0.0)) @ Matrix.Diagonal((1.0, 1.32, 1.0, 1.0))
    _lathe(bm, tags, [(0.0, 0.0), (0.0102, 0.0002), (0.0108, 0.0013), (0.0096, 0.0023), (0.0, 0.0025)], 10, pad,
           (0, 0, 0, 0, 0))
    band = []
    for s_, dz in ((-0.0080, 0.0012), (-0.0068, 0.0044), (-0.0044, 0.0078), (0.0, 0.0092), (0.0044, 0.0078),
                   (0.0068, 0.0044), (0.0080, 0.0012)):
        band.append(Vector((s_, -0.006, dz)))
    _tube(bm, band, 0.0018, 4, cap0=False, cap1=False)
    tags.mark(0)
    _tube(bm, [Vector((0.0, -0.041, hz - 0.0010)), Vector((0.0, -0.008, hz)), Vector((0.0, 0.025, hz))], hr, 6,
          cap0=False, cap1=False)
    _torus(bm, Vector((0.0, -0.0465, 0.0032)), UP, 0.0080, 0.0030, 10, 4)
    tags.mark(1)
    collar = Matrix((side, UP, along)).transposed().to_4x4()
    collar.translation = Vector((0.0, 0.023, hz - 0.0006))
    _lathe(bm, tags, [(0.0, 0.0), (0.0050, 0.0), (0.0054, 0.0058), (0.0, 0.0080)], 8, collar, (2, 2, 2, 2))
    # The blade: a rounded spade, a shallow scoop (its edges a little higher), toward the tail.
    cx, ax, ay = 0.051, 0.0200, 0.0160
    outline = []
    for k in range(14):
        phi = math.tau * k / 14
        cphi, sphi = math.cos(phi), math.sin(phi)
        p = 2.0 if cphi > 0 else 3.2
        outline.append((cx + ax * math.copysign(abs(cphi) ** (2 / p), cphi), ay * math.copysign(abs(sphi) ** (2 / p), sphi)))

    def B(l, s_, z):
        return along * l + side * s_ + UP * z

    top_ring = [bm.verts.new(B(l, s_, 0.0030 + 0.0020 * (s_ / ay) ** 2)) for l, s_ in outline]
    bot_ring = [bm.verts.new(B(l, s_, 0.0004 + 0.0020 * (s_ / ay) ** 2)) for l, s_ in outline]
    tc, bc = bm.verts.new(B(cx, 0.0, 0.0028)), bm.verts.new(B(cx, 0.0, 0.0002))
    for k in range(14):
        m = (k + 1) % 14
        bm.faces.new((top_ring[k], top_ring[m], tc))
        bm.faces.new((bot_ring[m], bot_ring[k], bc))
        bm.faces.new((bot_ring[k], bot_ring[m], top_ring[m], top_ring[k]))
    _tube(bm, [B(cx - ax * 0.93, -0.0140, 0.0040), B(cx - ax * 1.0, 0.0, 0.0034), B(cx - ax * 0.93, 0.0140, 0.0040)],
          0.0020, 4)
    # The tiny buckle on the band, on the left of the handle.
    bn = Vector((-0.62, 0.0, 0.78)).normalized()
    _torus(bm, Vector((-0.0066, -0.006, 0.0046)) + bn * 0.0019, bn, 0.0029, 0.0009, 6, 3)
    tags.mark(2)
    bmesh.ops.scale(bm, vec=(SHOVEL_SIZE, SHOVEL_SIZE, SHOVEL_SIZE), verts=bm.verts)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_GoldenShovel", bm, tags, regions, lambda co, tag: 0.15 if tag == 0 else 0.0, col, "Back")


# ---------------------------------------------------------------- gardener overalls (outfit 07: bib + straps)

STRAPS_RX, STRAPS_RY = 0.09, 1.0       # the saddle the crossed straps are bent to (round across, almost flat along)


def build_overalls_straps(col):
    """The two straps crossed on the back as one rigid saddle (the second rides over the first at the cross), a round
    patch on the cross, round buttons at the four ends."""
    bm = bmesh.new()
    tags = _Tags(bm)

    def saddle(x, y):
        z = -x * x / (2 * STRAPS_RX) - y * y / (2 * STRAPS_RY)
        return Vector((x, y, z)), Vector((x / STRAPS_RX, y / STRAPS_RY, 1.0)).normalized()

    shoulder, hip, half = -0.036, 0.044, 0.036
    cross_y = (shoulder + hip) * 0.5
    thick = 0.0040
    ends = []
    for k, sgn in enumerate((-1.0, 1.0)):
        stations, lift = [], []
        for i in range(7):
            t = i / 6
            x, y = sgn * half * (1 - 2 * t), shoulder + (hip - shoulder) * t
            stations.append(saddle(x, y))
            lift.append(0.0042 * math.exp(-((math.hypot(x, y - cross_y)) / 0.016) ** 2) if k == 1 else 0.0)
        _band(bm, tags, stations, 0.0185, thick, 0, lift=lift, out=thick * 0.5)
        ends += [stations[0], stations[-1]]
    tags.mark(0)
    loc, nor = saddle(0.0, cross_y)
    _dome(bm, loc + nor * (thick * 2 + 0.0042 + 0.0004), nor, 0.0145, 0.0036, seg=10)
    tags.mark(2)
    for loc, nor in ends:
        _dome(bm, loc + nor * (thick + 0.0010), nor, 0.0080, 0.0034)
    tags.mark(1)
    regions = {0: (0.0, 0.0, 0.0, 0.0), 1: (1.0, 0.0, 0.0, 0.0), 2: (0.0, 1.0, 0.0, 0.0)}
    return _finish("ACC_OverallsStraps", bm, tags, regions, lambda co, tag: 0.12 if tag == 0 else 0.0, col, "Back")


# ---------------------------------------------------------------- build, export

BUILDERS = {}


def build_all(only=None):
    col, work = _collections()
    report = {}
    BUILDERS.update({"ACC_FlowerCrown": build_flower_crown, "ACC_SeedBasket": build_seed_basket,
                     "ACC_SproutHat": build_sprout_hat, "ACC_SeedSack": build_seed_sack,
                     "ACC_BurrowGoggles": build_burrow_goggles, "ACC_GardenerScarf": build_gardener_scarf,
                     "ACC_MinerHelmet": build_miner_helmet, "ACC_GoldenShovel": build_golden_shovel,
                     "ACC_OverallsBib": build_overalls_bib, "ACC_OverallsStraps": build_overalls_straps})
    for name, builder in BUILDERS.items():
        if only is not None and name not in only:
            continue
        obj, lift = builder(col)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        if tris > BUDGETS[obj.name]:
            raise RuntimeError(f"{obj.name}: {tris} triangles over {BUDGETS[obj.name]}")
        pts = [Vector(c) for c in obj.bound_box]
        size = tuple(round(max(p[a] for p in pts) - min(p[a] for p in pts), 3) for a in range(3))
        report[obj.name] = dict(tris=tris, size=size, lift=round(lift, 4), min_z=round(min(p.z for p in pts), 4))
    bpy.ops.wm.save_mainfile()
    return report


def export_all(names=None):
    ns = {}
    exec(open(os.path.join(_root(), "SourceArt", "Blender", "Tools", "pw_export.py")).read(), ns)
    out = [ns["export_static"](f"{FBX_DIR}/{name}.fbx", (name,))[0] for name in (names or T47_PIECES + T49_PIECES)]
    bpy.ops.wm.save_mainfile()
    return out
