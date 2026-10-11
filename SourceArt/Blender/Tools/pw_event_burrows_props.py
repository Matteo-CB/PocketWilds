"""T49 L'Éveil des Terriers (event.burrows, P-EVT-103): the event decorations, the Souvenir and the monument.
Source of SourceArt/Blender/Events/BL_Event_Burrows.blend and of the FBX in Assets/_Game/Art/Events/Burrows/Models/
(one FBX per piece, named like its object).

Intention (charte « fait main », section 10, step 1)
- Mood of the event: tender sprout green, cream, warm earth, little earth mounds and ROUND dew beads. Everything is
  chunky, soft and rounded like our animals and the T47 Terrier douillet; nothing spiky, no four-point star.
- Scale (refuge, our animals are 20 to 65 cm tall): small decorations 0.3 to 0.8 m, shed and greenhouse about 1.3 m,
  the monument about 2.7 m across.
- Wanted imperfections: lumpy mounds (fixed-seed wave noise), leaning sprouts, planks a degree off, uneven moss.
- Collision rule: flat bottom on the ground (lowest vertex at z = 0, nothing below), one simple footprint each.
- Budgets (triangles): decorations under 1,500, Coffret ones under 2,000, the monument under 2,500.

Pieces (ids E-DEC-EVT-burrows-01..13, E-LMK-EVT-burrows)
    01 Decor_Burrows_FlowerEntrance  Entrée de terrier fleurie   track T3 (gold version: gold row)
    02 Decor_Burrows_MossMound       Monticule de mousse         track T8 (gold row)
    03 Decor_Burrows_DewMushrooms    Champignons de rosée        track T13 (gold row)
    04 Decor_Burrows_WateringCan     Arrosoir                    track T18 (gold row)
    05 Decor_Burrows_Wheelbarrow     Brouette miniature          shop 500
    06 Decor_Burrows_SeedlingPots    Pots de semis               shop 500
    07 Decor_Burrows_BurrowSign      Panneau du terrier          shop 650 (a carved burrow and sprout, no text)
    08 Decor_Burrows_WormCushion     Coussin ver de terre        shop 800 (a plush ring cushion with a smile)
    09 Decor_Burrows_GardenShed      Cabane de jardin            Coffret
    10 Decor_Burrows_Greenhouse      Serre miniature             Coffret
    11 Decor_Burrows_WoodenBench     Banc en bois                Coffret
    12 Decor_Burrows_GardenFence     Clôture de jardin           Coffret
    13 Decor_Burrows_Souvenir        Souvenir (track T5): a round plaque on a stake; the plaque face (A) is a flat
                                     disc left blank, the date is data drawn over it in Unity, never in a texture
    -- Landmark_Burrows_RootBurrow   Grand terrier aux racines   gold T30

Data contract: as pw_grassland_t39_props.py and pw_grassland_t47_props.py (one palette row per renderer,
SH_EnvironmentOpaque): color attribute PW_Regions (R/G/B/A weights toward palette columns 1-4 over the base color)
and uv PW_Occlusion (x baked occlusion, y height factor 0 at the ground, 1 at the top). PALETTES holds the proposed
rows (base, R, G, B, A, occlusion, accent), sRGB hex; the four track pieces also have a gold row (same model).

    row                                     base    R       G       B       A       occl.   accent  regions
    palette.event_burrows.flower_entrance   8A6646  7FAF5A  F2C94C  CFEFF2  F8F4EA  3E2E24  FFFFFF  earth; moss, leaves; hearts; dew; petals
    palette.event_burrows.moss_mound        8A6646  6FA34E  A6D07A  CFEFF2  D8D2C4  3E2E24  FFFFFF  earth; moss; sunlit moss, sprouts; dew; pebbles
    palette.event_burrows.dew_mushrooms     E58A6B  7FAF5A  F3E6CC  CFEFF2  FBF6EA  5A4034  FFFFFF  caps; moss; stems; dew; cap spots
    palette.event_burrows.watering_can      79A88F  5E8C74  D9A85A  CFEFF2  F8F4EA  34483E  FFFFFF  tin; handle, bands; brass, heart; dew; petals
    palette.event_burrows.gold              D9A441  E8C25A  B8862E  FFF0B0  FFF6D8  7A5A22  FFFFFF  every region gold (track gold line)
    palette.event_burrows.wheelbarrow       F0E3C4  8DC26F  C9925A  5E4E44  7A5640  4A3A2E  FFFFFF  tub; rim, sprout; wood; tire; soil
    palette.event_burrows.seedling_pots     D08A5E  86C25E  6B4A36  CFEFF2  E8D2A8  4A3A2E  FFFFFF  terracotta; sprouts; soil; dew; tray, label
    palette.event_burrows.burrow_sign       B5835A  86C25E  8A6646  3B2A22  E9D2A6  4A3A2E  FFFFFF  wood; sprouts; earth; carved hole, nails; board face
    palette.event_burrows.worm_cushion      F2A9A6  E58F95  F8EEDC  3A2A2E  F28C9A  8C5A5E  FFFFFF  plush; soft rings; buttons; eyes, smile; cheeks
    palette.event_burrows.garden_shed       DDB98C  7FAF5A  8C5E3C  BFE0E3  F4EAD2  4A3A2E  FFFFFF  boards; roof, sprouts; frame wood; window; door, petals
    palette.event_burrows.greenhouse        B5835A  86C25E  D08A5E  CFE8DF  F6F0E2  4A3A2E  FFFFFF  wood; sprouts; terracotta; glass; frame
    palette.event_burrows.wooden_bench      C9925A  86C25E  8C5E3C  F2C94C  F8F4EA  4A3A2E  FFFFFF  planks; sprouts; legs, arms; heart; petals
    palette.event_burrows.garden_fence      F3EBDD  7FB85A  C9925A  F2C94C  F3BDD3  6A5A4A  FFFFFF  pickets; leaves; posts, rails; hearts; petals
    palette.event_burrows.souvenir          B5835A  86C25E  8A6646  CFEFF2  F4EAD2  4A3A2E  FFFFFF  wood; sprouts; earth; dew; plaque face
    palette.event_burrows.root_burrow       8A6646  7FAF5A  9A6A45  CFEFF2  F8F4EA  2E221B  FFFFFF  earth; moss, sprouts; roots; dew; petals

Axes: pw_export.py writes Blender (x, y, z) as Unity (-x, z, -y). Pivots at the center of the footprint on the
ground; fronts (entrances, doors, faces, sign boards) face -Y (Unity +Z).

Usage (headless is fine; BL_Event_Burrows.blend is written from scratch):
    import pw_event_burrows_props as eb
    eb.new_file()       # empty file saved as BL_Event_Burrows.blend
    eb.build_all()      # every piece in EXPORT; returns triangles, sizes and footprints; saves
    eb.export_all()     # one FBX per piece (pw_export preset); saves
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

BLEND = "SourceArt/Blender/Events/BL_Event_Burrows.blend"
FBX_DIR = "Assets/_Game/Art/Events/Burrows/Models"
UP = Vector((0.0, 0.0, 1.0))
COFFRET = ("Decor_Burrows_GardenShed", "Decor_Burrows_Greenhouse", "Decor_Burrows_WoodenBench", "Decor_Burrows_GardenFence")
LANDMARK = "Landmark_Burrows_RootBurrow"

PALETTES = {
    "palette.event_burrows.flower_entrance": ("8A6646", "7FAF5A", "F2C94C", "CFEFF2", "F8F4EA", "3E2E24", "FFFFFF"),
    "palette.event_burrows.moss_mound": ("8A6646", "6FA34E", "A6D07A", "CFEFF2", "D8D2C4", "3E2E24", "FFFFFF"),
    "palette.event_burrows.dew_mushrooms": ("E58A6B", "7FAF5A", "F3E6CC", "CFEFF2", "FBF6EA", "5A4034", "FFFFFF"),
    "palette.event_burrows.watering_can": ("79A88F", "5E8C74", "D9A85A", "CFEFF2", "F8F4EA", "34483E", "FFFFFF"),
    "palette.event_burrows.gold": ("D9A441", "E8C25A", "B8862E", "FFF0B0", "FFF6D8", "7A5A22", "FFFFFF"),
    "palette.event_burrows.wheelbarrow": ("F0E3C4", "8DC26F", "C9925A", "5E4E44", "7A5640", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.seedling_pots": ("D08A5E", "86C25E", "6B4A36", "CFEFF2", "E8D2A8", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.burrow_sign": ("B5835A", "86C25E", "8A6646", "3B2A22", "E9D2A6", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.worm_cushion": ("F2A9A6", "E58F95", "F8EEDC", "3A2A2E", "F28C9A", "8C5A5E", "FFFFFF"),
    "palette.event_burrows.garden_shed": ("DDB98C", "7FAF5A", "8C5E3C", "BFE0E3", "F4EAD2", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.greenhouse": ("B5835A", "86C25E", "D08A5E", "CFE8DF", "F6F0E2", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.wooden_bench": ("C9925A", "86C25E", "8C5E3C", "F2C94C", "F8F4EA", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.garden_fence": ("F3EBDD", "7FB85A", "C9925A", "F2C94C", "F3BDD3", "6A5A4A", "FFFFFF"),
    "palette.event_burrows.souvenir": ("B5835A", "86C25E", "8A6646", "CFEFF2", "F4EAD2", "4A3A2E", "FFFFFF"),
    "palette.event_burrows.root_burrow": ("8A6646", "7FAF5A", "9A6A45", "CFEFF2", "F8F4EA", "2E221B", "FFFFFF"),
}
ROWS = {}           # object name -> palette row key (filled by the builders)
CLAMPED = {}        # object name -> depth pressed onto the ground (stem and root ends), metres
WEIGHTS = {"base": (0, 0, 0, 0), "R": (1, 0, 0, 0), "G": (0, 1, 0, 0), "B": (0, 0, 1, 0), "A": (0, 0, 0, 1),
           "hole": (0, 0, 0, 0)}


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


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def wave_noise(p, seed, octaves=2):
    """Deterministic smooth noise in roughly [-1, 1] from a few seeded sine waves (as pw_grassland.wave_noise)."""
    rng = random.Random(seed)
    total, norm, amp, freq = 0.0, 0.0, 1.0, 1.0
    for _ in range(octaves):
        for _ in range(3):
            d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
            total += amp * math.sin(p.dot(d) * freq * 2.4 + rng.uniform(0, 6.283))
            norm += amp
        amp *= 0.5
        freq *= 2.1
    return total / norm


class Kit:
    """One piece under construction: a bmesh and a region tag per vertex (to_mesh keeps the vertex order)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.tags = []

    def mark(self, tag):
        self.tags.extend([tag] * (len(self.bm.verts) - len(self.tags)))

    def face(self, verts, smooth=True):
        f = self.bm.faces.new(verts)
        f.smooth = smooth
        return f

    def bvh(self):
        return BVHTree.FromBMesh(self.bm)


def _frame(origin, axis):
    w = axis.normalized()
    u = w.orthogonal().normalized()
    if abs(w.z) < 0.95:
        u = UP.cross(w).normalized()
    v = w.cross(u)
    m = Matrix((u, v, w)).transposed().to_4x4()
    m.translation = origin
    return m


# ---------------------------------------------------------------- primitives

def lathe(k, profile, seg, frame=None, wobble=None, marks=None, turn=0.0):
    """Revolves (r, h) pairs listed from the bottom center outward and up (poles where r is 0) around frame's +Z."""
    frame = frame or Matrix.Identity(4)
    rings = []
    for i, (r, h) in enumerate(profile):
        if r <= 1e-9:
            rings.append([k.bm.verts.new(frame @ Vector((0.0, 0.0, h)))])
        else:
            ring = []
            for j in range(seg):
                a = turn + math.tau * j / seg
                f = wobble(a, h) if wobble else 1.0
                ring.append(k.bm.verts.new(frame @ Vector((math.cos(a) * r * f, math.sin(a) * r * f, h))))
            rings.append(ring)
        if marks:
            k.mark(marks[i])
    for lo, hi in zip(rings, rings[1:]):
        for j in range(seg):
            m = (j + 1) % seg
            if len(lo) == 1 and len(hi) > 1:
                k.face((lo[0], hi[m], hi[j]))
            elif len(hi) == 1 and len(lo) > 1:
                k.face((lo[j], lo[m], hi[0]))
            elif len(lo) > 1:
                k.face((lo[j], lo[m], hi[m], hi[j]))
    return rings


def tube(k, pts, radii, sides, cap0=True, cap1=True, flat_end=False):
    n = len(pts)
    tangents = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    normal = tangents[0].orthogonal().normalized()
    rings = []
    for i in range(n):
        t = tangents[i]
        normal = (normal - t * normal.dot(t)).normalized()
        b = t.cross(normal)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        rings.append([k.bm.verts.new(pts[i] + (normal * math.cos(a) + b * math.sin(a)) * r)
                      for a in (math.tau * j / sides for j in range(sides))])
    for r0, r1 in zip(rings, rings[1:]):
        for j in range(sides):
            m = (j + 1) % sides
            k.face((r0[j], r0[m], r1[m], r1[j]))
    for cap, ring, sign in ((cap0, rings[0], -1.0), (cap1, rings[-1], 1.0)):
        if not cap:
            continue
        i = 0 if sign < 0 else n - 1
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        tip = k.bm.verts.new(pts[i] + tangents[i] * (sign * r * (0.0 if flat_end and sign > 0 else 0.7)))
        for j in range(sides):
            m = (j + 1) % sides
            k.face((ring[m], ring[j], tip) if sign < 0 else (ring[j], ring[m], tip), smooth=not (flat_end and sign > 0))
    return rings


def torus(k, center, axis, major, minor, seg, sides):
    f = _frame(center, axis)
    rings = []
    for i in range(seg):
        a = math.tau * i / seg
        c = Vector((math.cos(a), math.sin(a), 0.0))
        rings.append([k.bm.verts.new(f @ (c * (major + minor * math.cos(b)) + Vector((0.0, 0.0, minor * math.sin(b)))))
                      for b in (math.tau * j / sides for j in range(sides))])
    for i in range(seg):
        r0, r1 = rings[i], rings[(i + 1) % seg]
        for j in range(sides):
            m = (j + 1) % sides
            k.face((r0[j], r1[j], r1[m], r0[m]))


def ellipsoid(k, center, radii, seg, rings, axis=UP, floor=None):
    f = _frame(center, axis)
    top = k.bm.verts.new(f @ Vector((0.0, 0.0, radii[2])))
    grid = []
    for j in range(1, rings):
        phi = math.pi * j / rings
        grid.append([k.bm.verts.new(f @ Vector((math.sin(phi) * math.cos(t) * radii[0], math.sin(phi) * math.sin(t) * radii[1],
                                                math.cos(phi) * radii[2])))
                     for t in (math.tau * i / seg for i in range(seg))])
    bot = k.bm.verts.new(f @ Vector((0.0, 0.0, -radii[2])))
    for i in range(seg):
        m = (i + 1) % seg
        k.face((top, grid[0][i], grid[0][m]))
        k.face((bot, grid[-1][m], grid[-1][i]))
    for a, b in zip(grid, grid[1:]):
        for i in range(seg):
            m = (i + 1) % seg
            k.face((a[i], b[i], b[m], a[m]))
    if floor is not None:
        for v in [top, bot] + [v for row in grid for v in row]:
            v.co.z = max(v.co.z, floor)


def rbox(k, center, size, bevel, segs=2, rot=None):
    """A soft box: chamfered (segs 1) or rounded (segs 2) edges, flat big faces."""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    for v in tmp.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    originals = set(tmp.faces)
    bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=segs, profile=0.5, affect='EDGES')
    m = Matrix.Translation(center) @ (rot.to_4x4() if rot is not None else Matrix.Identity(4))
    vmap = {}
    for v in tmp.verts:
        vmap[v] = k.bm.verts.new(m @ v.co)
    for f in tmp.faces:
        k.face([vmap[v] for v in f.verts], smooth=f not in originals)
    tmp.free()


def slab(k, outline, depth, frame, chamfer=0.0, front_tag=None):
    """An extruded rounded outline (2D, counter-clockwise, in frame's xy; frame's -Z is the front, toward -Y when
    the frame looks there). chamfer > 0 adds the soft inset rings on both faces."""
    n = len(outline)
    cx = sum(p[0] for p in outline) / n
    cy = sum(p[1] for p in outline) / n

    def ring(z, inset):
        return [k.bm.verts.new(frame @ Vector((x + (cx - x) * inset, y + (cy - y) * inset, z))) for x, y in outline]

    layers = []
    if chamfer > 0:
        layers.append(ring(-depth * 0.5, chamfer))
        layers.append(ring(-depth * 0.5 + depth * 0.25, 0.0))
        layers.append(ring(depth * 0.5 - depth * 0.25, 0.0))
        layers.append(ring(depth * 0.5, chamfer))
    else:
        layers.append(ring(-depth * 0.5, 0.0))
        layers.append(ring(depth * 0.5, 0.0))
    for a, b in zip(layers, layers[1:]):
        for i in range(n):
            m = (i + 1) % n
            k.face((a[i], a[m], b[m], b[i]))
    k.mark("base")
    front_c = k.bm.verts.new(frame @ Vector((cx, cy, -depth * 0.5)))
    back_c = k.bm.verts.new(frame @ Vector((cx, cy, depth * 0.5)))
    if front_tag:
        k.tags.extend([front_tag, "base"])
        k.bm.verts.index_update()
        for v in layers[0]:
            k.tags[v.index] = front_tag
    else:
        k.mark("base")
    for i in range(n):
        m = (i + 1) % n
        k.face((layers[0][m], layers[0][i], front_c), smooth=False)
        k.face((layers[-1][i], layers[-1][m], back_c), smooth=False)


def mound(k, rx, ry, h, seg, rings, seed, lump=0.05, top_flat=0.08):
    """A lumpy earth dome from a top pole down to a ring on the ground (z = 0); open bottom."""
    def point(theta, phi):
        d = Vector((math.sin(phi) * math.cos(theta), math.sin(phi) * math.sin(theta), math.cos(phi)))
        bump = 1.0 + lump * wave_noise(d * 2.3, seed) * math.sin(phi) ** 0.5
        p = Vector((d.x * rx * bump, d.y * ry * bump, d.z * h * bump))
        p.z *= (1.0 - top_flat) + top_flat * math.sin(phi)
        return p

    top = k.bm.verts.new(point(0.0, 0.0))
    grid = []
    for j in range(1, rings + 1):
        phi = (math.pi / 2) * j / rings
        row = [k.bm.verts.new(point(math.tau * i / seg, phi)) for i in range(seg)]
        if j == rings:
            for v in row:
                v.co.z = 0.0
        grid.append(row)
    for i in range(seg):
        k.face((top, grid[0][i], grid[0][(i + 1) % seg]))
    for a, b in zip(grid, grid[1:]):
        for i in range(seg):
            m = (i + 1) % seg
            k.face((a[i], b[i], b[m], a[m]))


def surf(bvh, x, y):
    loc, nor, _, _ = bvh.ray_cast(Vector((x, y, 10.0)), Vector((0.0, 0.0, -1.0)))
    return loc, nor


def round_leaf(k, base, direction, normal, length, width, curl=0.25):
    d = direction.normalized()
    n = (normal - d * normal.dot(d)).normalized()
    s = n.cross(d)

    def at(t, side):
        w = width * math.sin(math.pi * min(t, 0.999)) ** 0.75
        return base + d * (length * t) + s * (side * w) + n * (curl * length * t * t)

    outline = [base] + [at(t, 1.0) for t in (0.16, 0.42, 0.68, 0.88)] + [at(1.0, 0.0)] + \
              [at(t, -1.0) for t in (0.88, 0.68, 0.42, 0.16)]
    ring = [k.bm.verts.new(p) for p in outline]
    c = at(0.45, 0.0)
    top = k.bm.verts.new(c + n * width * 0.22)
    bot = k.bm.verts.new(c - n * width * 0.16)
    for i in range(len(ring)):
        m = (i + 1) % len(ring)
        k.face((ring[m], ring[i], top))
        k.face((ring[i], ring[m], bot))


def sprout(k, base, scale=1.0, turn=0.0, leaves=2, lean=(0.0, 0.0), up=UP):
    """A tender sprout: a short bent stem and two (or four) rounded leaves. About 70 triangles (two leaves)."""
    u = up.normalized()
    side = Vector((math.cos(turn), math.sin(turn), 0.0))
    side = (side - u * side.dot(u)).normalized()
    fwd = u.cross(side)
    leanv = side * lean[0] + fwd * lean[1]
    pts = [base - u * 0.01 * scale, base + (u * 0.045 + leanv * 0.01) * scale, base + (u * 0.085 + leanv * 0.025) * scale]
    tube(k, pts, [0.0055 * scale, 0.0048 * scale, 0.0040 * scale], 5, cap0=False, cap1=True)
    tip = pts[-1] - u * 0.003 * scale
    for i in range(leaves):
        a = turn + i * math.tau / leaves + (0.35 if leaves > 2 and i % 2 else 0.0)
        dside = Vector((math.cos(a), math.sin(a), 0.0))
        dside = (dside - u * dside.dot(u)).normalized()
        rise = 0.62 if i % 2 == 0 else 0.48
        direction = dside * 0.78 + u * rise
        normal = u - dside * 0.7
        length = (0.06 if i < 2 else 0.042) * scale
        round_leaf(k, tip, direction, normal, length, length * 0.48, curl=0.22)


def daisy(k, center, normal, radius, turn, petal="A", heart="G"):
    """Five round petals (a cupped lens, 40 triangles) and a domed heart (6 triangles), as pw_grassland_t39_props."""
    u = normal.orthogonal().normalized()
    w = normal.cross(u).normalized()
    pts = []
    step = math.tau / 5
    for i in range(5):
        a0 = turn + i * step
        for da, r in ((0.0, 0.4), (0.2, 0.88), (0.5, 1.0), (0.8, 0.88)):
            a = a0 + da * step
            pts.append(center + (u * math.cos(a) + w * math.sin(a)) * (radius * r) + normal * (0.16 * radius * r * r))
    ring = [k.bm.verts.new(p) for p in pts]
    top = k.bm.verts.new(center + normal * radius * 0.1)
    bottom = k.bm.verts.new(center - normal * radius * 0.14)
    for i in range(len(ring)):
        m = (i + 1) % len(ring)
        k.face((top, ring[i], ring[m]))
        k.face((bottom, ring[m], ring[i]))
    k.mark(petal)
    hr = radius * 0.34
    lift = radius * 0.1 * (1.0 - 0.34) + radius * 0.035
    hearts = [k.bm.verts.new(center + (u * math.cos(t) + w * math.sin(t)) * hr + normal * lift)
              for t in (turn + j / 6 * math.tau for j in range(6))]
    apex = k.bm.verts.new(center + normal * (radius * 0.3))
    for j in range(6):
        k.face((apex, hearts[j], hearts[(j + 1) % 6]))
    k.mark(heart)


def dew(k, center, r, tag="B"):
    """A round dew bead (36 triangles), a touch flattened where it rests."""
    ellipsoid(k, center, (r, r, r * 0.86), 6, 4)
    k.mark(tag)


def hole(k, bvh, z, width, height, depth, seg=12, x=0.0):
    """A dark round mouth just proud of the front surface (-Y), with a short tunnel sinking into the piece."""
    hit, nor, _, _ = bvh.ray_cast(Vector((x, -20.0, z)), Vector((0.0, 1.0, 0.0)))
    n = Vector((0.0, -1.0, 0.0)).lerp(nor, 0.5).normalized()
    u = Vector((1.0, 0.0, 0.0))
    w = n.cross(u).normalized()
    if w.z < 0:
        w = -w
    mouth = hit + n * (0.012 * width / 0.1)
    rim = [k.bm.verts.new(mouth + u * (math.cos(a) * width) + w * (math.sin(a) * height))
           for a in (math.tau * i / seg for i in range(seg))]
    deep = [k.bm.verts.new(mouth - n * depth + (v.co - mouth) * 0.72) for v in rim]
    back = k.bm.verts.new(mouth - n * depth * 1.2)
    for i in range(seg):
        m = (i + 1) % seg
        k.face((rim[i], rim[m], deep[m], deep[i]))
        k.face((deep[i], deep[m], back))
    k.mark("hole")
    return mouth, n, u, w


def on_surface(bvh, origin, direction):
    loc, nor, _, _ = bvh.ray_cast(origin, direction)
    return loc, nor


# ---------------------------------------------------------------- finishing

def finish(k, name, col, row, regions=None, occlusion=None):
    bm = k.bm
    assert len(k.tags) == len(bm.verts), (name, len(k.tags), len(bm.verts))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    low = min(v.co.z for v in bm.verts)
    if low < -0.12:
        raise RuntimeError("%s: %.1f mm under the ground" % (name, -low * 1000))
    for v in bm.verts:          # flat bottom: what reaches under the ground is pressed onto it
        v.co.z = max(0.0, v.co.z)
    CLAMPED[name] = round(-min(low, 0.0), 4)
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old)
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    col.objects.link(obj)
    height = max(v.co.z for v in me.vertices) or 1.0
    attr = me.color_attributes.get("PW_Regions") or me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="PW_Occlusion")
    per_vertex = []
    for v in me.vertices:
        tag = k.tags[v.index]
        rgba = regions(v.co, v.normal, tag) if regions else None
        attr.data[v.index].color = tuple(max(0.0, min(1.0, c)) for c in (rgba or WEIGHTS[tag]))
        occ = occlusion(v.co, v.normal, tag) if occlusion else None
        if occ is None:
            occ = 0.85 if tag == "hole" else 0.28 * (1.0 - smoothstep(0.0, min(0.12, height * 0.3), v.co.z))
        per_vertex.append((max(0.0, min(1.0, occ)), max(0.0, min(1.0, v.co.z / height))))
    for loop in me.loops:
        uv.data[loop.index].uv = per_vertex[loop.vertex_index]
    me.color_attributes.active_color = attr
    obj["pw_palette"] = row
    ROWS[name] = row
    return obj


def mossy(threshold=0.45, z0=0.0, z1=0.1, amount=0.85):
    """Earth with moss creeping over the top (R by the surface slope), other tags by their weights."""
    def regions(co, nrm, tag):
        if tag == "earth":
            return (amount * smoothstep(threshold, threshold + 0.4, nrm.z) * smoothstep(z0, z1, co.z), 0.0, 0.0, 0.0)
        return None
    return regions


# ---------------------------------------------------------------- track decorations

def build_flower_entrance(col):
    k = Kit()
    mound(k, 0.36, 0.30, 0.30, 20, 6, seed=103, lump=0.05)
    k.mark("earth")
    bvh = k.bvh()
    mouth, n, u, w = hole(k, bvh, 0.112, 0.104, 0.094, 0.08)
    rng = random.Random(31)
    for i in range(7):
        a = math.pi * (-0.08 + 1.16 * i / 6)
        p = mouth + u * (math.cos(a) * 0.150) + w * (math.sin(a) * 0.142)
        loc, nor = on_surface(bvh, p + n * 0.3, -n)
        if loc is None:
            continue
        facing = (nor + n * 0.5 + UP * 0.3).normalized()
        daisy(k, loc + nor * 0.016, facing, 0.036 + 0.007 * rng.random(), 0.6 * i, petal="A", heart="G")
    for i in range(6):
        a = math.pi * (-0.08 + 1.16 * (i + 0.5) / 6)
        p = mouth + u * (math.cos(a) * 0.178) + w * (math.sin(a) * 0.168)
        loc, nor = on_surface(bvh, p + n * 0.3, -n)
        if loc is None:
            continue
        out = (u * math.cos(a) + w * math.sin(a))
        round_leaf(k, loc + nor * 0.004, out, nor, 0.050, 0.022, curl=0.3)
    for x, y, r, h in ((0.04, 0.08, 0.13, 0.04), (-0.17, 0.10, 0.09, 0.03), (0.19, 0.04, 0.08, 0.03)):
        loc, nor = surf(bvh, x, y)
        ellipsoid(k, loc - nor * (h * 0.35), (r, r * 0.85, h), 7, 3, axis=nor)
    k.mark("R")
    for x, y, s, t in ((-0.06, 0.12, 1.05, 0.4), (0.13, 0.13, 0.85, 1.9)):
        loc, nor = surf(bvh, x, y)
        sprout(k, loc, s, t)
    k.mark("R")
    for x, y in ((0.06, 0.04), (-0.17, 0.08), (0.20, 0.0)):
        loc, nor = surf(bvh, x, y)
        dew(k, loc + nor * 0.012, 0.016)
    return finish(k, "Decor_Burrows_FlowerEntrance", col, "palette.event_burrows.flower_entrance", mossy(0.5, 0.12, 0.24))


def build_moss_mound(col):
    k = Kit()
    mound(k, 0.32, 0.27, 0.22, 20, 6, seed=211, lump=0.07, top_flat=0.12)
    k.mark("earth")
    bvh = k.bvh()
    rng = random.Random(7)
    for x, y, r, h in ((0.0, 0.0, 0.17, 0.055), (-0.15, 0.06, 0.11, 0.045), (0.15, -0.05, 0.11, 0.04),
                       (0.05, 0.15, 0.10, 0.04), (-0.08, -0.14, 0.10, 0.04)):
        loc, nor = surf(bvh, x, y)
        ellipsoid(k, loc - nor * (h * 0.3), (r, r * (0.8 + 0.15 * rng.random()), h), 8, 3, axis=nor)
    k.mark("moss")
    for x, y, r in ((0.30, -0.12, 0.05), (-0.29, -0.10, 0.04)):
        ellipsoid(k, Vector((x, y, 0.0)), (r, r * 0.85, r * 0.75), 7, 3, floor=0.0)
    k.mark("A")
    bvh = k.bvh()
    for x, y, s, t in ((0.02, 0.02, 1.15, 0.3), (-0.13, 0.08, 0.85, 2.2), (0.13, 0.10, 0.75, 4.1)):
        loc, nor = surf(bvh, x, y)
        sprout(k, loc - Vector((0, 0, 0.004)), s, t)
    k.mark("G")
    for x, y, r in ((0.08, -0.04, 0.017), (-0.10, -0.06, 0.014), (0.16, -0.08, 0.013), (-0.02, 0.12, 0.012)):
        loc, nor = surf(bvh, x, y)
        dew(k, loc + nor * r * 0.7, r)

    def regions(co, nrm, tag):
        if tag == "earth":
            return (0.9 * smoothstep(0.35, 0.8, nrm.z) * smoothstep(0.05, 0.16, co.z), 0.0, 0.0, 0.0)
        if tag == "moss":
            sun = smoothstep(0.6, 0.98, nrm.z) * 0.7
            return (1.0 - sun, sun, 0.0, 0.0)
        return None

    return finish(k, "Decor_Burrows_MossMound", col, "palette.event_burrows.moss_mound", regions)


def build_dew_mushrooms(col):
    k = Kit()
    mound(k, 0.17, 0.15, 0.05, 14, 3, seed=59, lump=0.08, top_flat=0.2)
    k.mark("R")
    bvh = k.bvh()
    for x, y, height, cap, lean, spots in ((0.02, 0.02, 0.29, 0.095, (0.05, 0.03), 5),
                                            (-0.09, -0.04, 0.20, 0.072, (-0.16, -0.05), 3),
                                            (0.09, -0.07, 0.135, 0.055, (0.14, -0.12), 2)):
        loc, nor = surf(bvh, x, y)
        axis = Vector((lean[0], lean[1], 1.0)).normalized()
        f = _frame(loc - Vector((0, 0, 0.01)), axis)
        sr = cap * 0.30
        lathe(k, [(sr * 1.25, 0.0), (sr * 1.08, height * 0.35), (sr * 0.92, height * 0.75), (sr * 0.85, height - 0.01)],
              10, f)
        k.mark("G")
        cf = f @ Matrix.Translation((0.0, 0.0, height - 0.012))
        R = cap
        lathe(k, [(0.0, 0.05 * R), (0.55 * R, -0.04 * R), (0.97 * R, 0.02 * R), (1.0 * R, 0.12 * R), (0.88 * R, 0.38 * R),
                  (0.62 * R, 0.60 * R), (0.30 * R, 0.72 * R), (0.0, 0.75 * R)], 12, cf,
              marks=["G", "G", "base", "base", "base", "base", "base", "base"])
        for s in range(spots):
            az = 0.7 + s * math.tau / spots + 0.3 * (s % 2)
            el = 0.50 if s % 2 == 0 else 0.30
            rr = 0.76 * R if el > 0.4 else 0.92 * R
            c = cf @ Vector((math.cos(az) * rr, math.sin(az) * rr, el * R))
            ax = (cf.to_3x3() @ Vector((math.cos(az) * 0.62, math.sin(az) * 0.62, 0.78 if el > 0.4 else 0.5))).normalized()
            ellipsoid(k, c, (0.13 * R, 0.13 * R, 0.035 * R), 6, 2, axis=ax)
        k.mark("A")
        dew(k, cf @ Vector((0.18 * R, -0.1 * R, 0.75 * R + 0.06 * R)), 0.10 * R + 0.004)
    sprout(k, surf(bvh, -0.05, 0.10)[0], 0.8, 2.6)
    k.mark("R")
    for x, y in ((0.12, 0.05), (-0.12, 0.05)):
        loc, nor = surf(bvh, x, y)
        dew(k, loc + nor * 0.008, 0.012)
    return finish(k, "Decor_Burrows_DewMushrooms", col, "palette.event_burrows.dew_mushrooms")


def build_watering_can(col):
    k = Kit()
    body = Matrix.Translation((0.0, 0.04, 0.0))
    profile = [(0.0, 0.0), (0.095, 0.0), (0.108, 0.012), (0.116, 0.050), (0.115, 0.090), (0.113, 0.132), (0.104, 0.170),
               (0.080, 0.195), (0.052, 0.205), (0.057, 0.214), (0.047, 0.219), (0.040, 0.210), (0.0, 0.200)]
    lathe(k, profile, 14, body,
          marks=["R", "R", "base", "R", "base", "R", "base", "base", "base", "R", "R", "R", "base"])
    spout = [Vector(p) for p in ((0.0, -0.030, 0.050), (0.0, -0.100, 0.075), (0.0, -0.170, 0.130), (0.0, -0.215, 0.190),
                                 (0.0, -0.236, 0.226))]
    tube(k, spout, [0.022, 0.018, 0.0145, 0.012, 0.011], 7, cap0=False, cap1=False)
    k.mark("base")
    t = (spout[-1] - spout[-2]).normalized()
    lathe(k, [(0.0, -0.004), (0.012, 0.0), (0.030, 0.022), (0.034, 0.030), (0.030, 0.036), (0.0, 0.037)], 12,
          _frame(spout[-1] - t * 0.004, t))
    k.mark("G")
    handle = [Vector(p) for p in ((0.0, 0.150, 0.160), (0.0, 0.178, 0.240), (0.0, 0.128, 0.312), (0.0, 0.040, 0.326),
                                  (0.0, -0.028, 0.290), (0.0, -0.044, 0.205))]
    tube(k, _catmull_d(handle, 3), 0.014, 7, cap0=False, cap1=False)
    k.mark("R")
    daisy(k, Vector((0.121, 0.04, 0.092)), Vector((1.0, 0.0, 0.08)).normalized(), 0.034, 0.3, petal="A", heart="G")
    bvh = k.bvh()
    for x, y, r in ((0.050, 0.110, 0.013), (-0.065, 0.075, 0.011)):
        loc, nor = surf(bvh, x, y)
        dew(k, loc + nor * r * 0.7, r)
    return finish(k, "Decor_Burrows_WateringCan", col, "palette.event_burrows.watering_can")


# ---------------------------------------------------------------- shop decorations

def build_wheelbarrow(col):
    k = Kit()
    tub = (Matrix.Translation((0.0, 0.02, 0.14)) @ Matrix.Rotation(math.radians(7.0), 4, 'X')
           @ Matrix.Diagonal((0.92, 1.25, 1.0, 1.0)))
    lathe(k, [(0.0, 0.0), (0.10, 0.0), (0.150, 0.040), (0.172, 0.100), (0.180, 0.135), (0.172, 0.150), (0.158, 0.141),
              (0.148, 0.100), (0.110, 0.040), (0.0, 0.030)], 14, tub,
          marks=["base", "base", "base", "base", "R", "R", "R", "base", "base", "base"])
    soil = tub @ Vector((0.0, 0.0, 0.045))
    ellipsoid(k, soil, (0.10, 0.13, 0.03), 8, 3)
    k.mark("A")
    sprout(k, soil + Vector((0.01, 0.02, 0.02)), 0.95, 0.8)
    k.mark("R")
    torus(k, Vector((0.0, -0.285, 0.105)), Vector((1.0, 0.0, 0.0)), 0.075, 0.030, 14, 6)
    k.mark("B")
    lathe(k, [(0.0, -0.040), (0.030, -0.035), (0.036, 0.0), (0.030, 0.035), (0.0, 0.040)], 8,
          _frame(Vector((0.0, -0.285, 0.105)), Vector((1.0, 0.0, 0.0))))
    for sgn in (-1.0, 1.0):
        tube(k, [Vector((sgn * 0.040, -0.285, 0.105)), Vector((sgn * 0.075, -0.16, 0.150))], 0.013, 5, cap0=False,
             cap1=False)
        tube(k, [Vector((sgn * 0.085, -0.17, 0.135)), Vector((sgn * 0.120, 0.10, 0.170)), Vector((sgn * 0.150, 0.30, 0.250)),
                 Vector((sgn * 0.156, 0.40, 0.290))], 0.016, 6, cap0=False, cap1=True)
        ellipsoid(k, Vector((sgn * 0.157, 0.415, 0.297)), (0.022, 0.034, 0.022), 8, 4,
                  axis=Vector((0.0, 1.0, 0.45)).normalized())
        tube(k, [Vector((sgn * 0.120, 0.120, 0.165)), Vector((sgn * 0.128, 0.150, 0.0))], 0.017, 6, cap0=False,
             cap1=True, flat_end=True)
    k.mark("G")
    return finish(k, "Decor_Burrows_Wheelbarrow", col, "palette.event_burrows.wheelbarrow")


def build_seedling_pots(col):
    k = Kit()
    rbox(k, Vector((0.0, 0.0, 0.015)), (0.50, 0.21, 0.03), 0.008, 2)
    k.mark("A")
    for x, y, s, turn in ((-0.150, 0.004, 1.0, 0.2), (0.020, -0.010, 0.86, 1.1), (0.165, 0.008, 0.72, 2.0)):
        f = Matrix.Translation((x, y, 0.03)) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Scale(s, 4)
        lathe(k, [(0.0, 0.0), (0.048, 0.0), (0.062, 0.085), (0.072, 0.088), (0.073, 0.108), (0.064, 0.110),
                  (0.059, 0.098), (0.057, 0.094), (0.0, 0.096)], 10, f,
              marks=["base"] * 7 + ["G", "G"])
    for x, y, s, t, leaves in ((-0.165, 0.010, 1.0, 0.3, 2), (-0.125, -0.012, 0.8, 2.0, 2), (0.022, -0.010, 1.1, 1.0, 4),
                               (0.165, 0.008, 0.65, 2.6, 2)):
        z = 0.03 + 0.096 * (1.0 if x < -0.1 else (0.86 if x < 0.1 else 0.72))
        sprout(k, Vector((x, y, z)), s, t, leaves=leaves)
    k.mark("R")
    tube(k, [Vector((-0.115, 0.03, 0.12)), Vector((-0.108, 0.035, 0.205))], 0.0035, 4, cap0=False, cap1=True)
    rbox(k, Vector((-0.104, 0.036, 0.205)), (0.042, 0.006, 0.028), 0.0025, 1)
    k.mark("A")
    for p, r in (((-0.185, 0.04, 0.19), 0.009), ((0.06, -0.03, 0.17), 0.008)):
        dew(k, Vector(p), r)
    return finish(k, "Decor_Burrows_SeedlingPots", col, "palette.event_burrows.seedling_pots")


def _superellipse(w, h, n, power=4.0):
    pts = []
    for i in range(n):
        a = math.tau * i / n
        c, s = math.cos(a), math.sin(a)
        pts.append((w * 0.5 * math.copysign(abs(c) ** (2 / power), c), h * 0.5 * math.copysign(abs(s) ** (2 / power), s)))
    return pts


def build_burrow_sign(col):
    k = Kit()
    mound(k, 0.14, 0.12, 0.06, 14, 3, seed=17, lump=0.08, top_flat=0.2)
    k.mark("G")
    bvh = k.bvh()
    rbox(k, Vector((0.0, 0.005, 0.30)), (0.05, 0.05, 0.60), 0.01, 1)
    k.mark("base")
    board = (Matrix.Translation((0.0, -0.035, 0.46)) @ Matrix.Rotation(math.radians(2.5), 4, 'Y')
             @ Matrix.Rotation(math.radians(90.0), 4, 'X'))
    # frame local -Z is world -Y (the front): rotation 90 degrees about X sends local +Z to world -Y, so flip.
    board = board @ Matrix.Diagonal((1.0, 1.0, -1.0, 1.0))
    slab(k, _superellipse(0.42, 0.25, 20, 4.0), 0.04, board, chamfer=0.06, front_tag="A")
    front = board @ Vector((0.0, 0.0, -0.021))
    fwd = -(board.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
    right = (board.to_3x3() @ Vector((1.0, 0.0, 0.0))).normalized()
    upv = (board.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
    hc = front + right * 0.06 - upv * 0.035
    ellipsoid(k, hc + fwd * 0.002, (0.040, 0.034, 0.006), 10, 2, axis=fwd)
    k.mark("B")
    arc = [hc + fwd * 0.004 + right * (math.cos(a) * 0.052) + upv * (math.sin(a) * 0.046) for a in
           (math.pi * i / 6 for i in range(7))]
    tube(k, arc, 0.012, 5)
    k.mark("G")
    sb = front + fwd * 0.004 - right * 0.09 - upv * 0.06
    tube(k, [sb, sb + upv * 0.05 + right * 0.004, sb + upv * 0.09 + right * 0.012], 0.0055, 5, cap0=True, cap1=True)
    for sgn in (-1.0, 1.0):
        round_leaf(k, sb + upv * 0.088 + right * 0.012, right * sgn * 0.8 + upv * 0.6, fwd, 0.055, 0.026, curl=0.05)
    k.mark("R")
    for sgn in (-1.0, 1.0):
        ellipsoid(k, front + fwd * 0.002 + right * (sgn * 0.17) + upv * 0.085, (0.009, 0.009, 0.005), 6, 3, axis=fwd)
    k.mark("B")
    for x, y, s, t in ((0.08, -0.04, 0.95, 0.5), (-0.07, 0.05, 0.8, 2.4)):
        loc, nor = surf(bvh, x, y)
        sprout(k, loc, s, t)
    k.mark("R")
    return finish(k, "Decor_Burrows_BurrowSign", col, "palette.event_burrows.burrow_sign")


def build_worm_cushion(col):
    """A plush ring cushion shaped like a round little earthworm: nine soft stuffed rings, a lifted round head
    with bead eyes, blush cheeks and a smile, three cream tufting buttons. Pastel, never gross."""
    k = Kit()
    count, sides, rho, rr = 30, 8, 0.158, 0.078
    start, sweep = math.radians(-70.0), math.radians(318.0)
    centers, radii = [], []
    for i in range(count):
        s = i / (count - 1)
        a = start + sweep * s
        head = max(0.0, 1.0 - s / 0.16)
        r = rr * (0.80 + 0.20 * math.sin(math.pi * min(1.0, s * 1.6 + 0.12)) ** 0.5) * (1.0 + 0.07 * math.cos(math.tau * s * 9))
        if s > 0.85:
            r *= 1.0 - 0.35 * (s - 0.85) / 0.15
        z = r * 0.82 + 0.085 * head * head
        out = rho + 0.07 * head * head
        centers.append(Vector((out * math.cos(a), out * math.sin(a), z)))
        radii.append(r)
    hc, t = centers[0], (centers[0] - centers[1]).normalized()
    tc, tt = centers[-1], (centers[-1] - centers[-2]).normalized()
    pts = [hc + t * radii[0] * 0.78, hc + t * radii[0] * 0.42] + centers + [tc + tt * radii[-1] * 0.42, tc + tt * radii[-1] * 0.78]
    rad = [radii[0] * 0.62, radii[0] * 0.92] + radii + [radii[-1] * 0.92, radii[-1] * 0.62]
    rings = tube(k, pts, rad, sides, cap0=True, cap1=True)
    k.bm.verts.index_update()
    k.mark("base")
    for i, ring in enumerate(rings[2:-2]):
        s = i / (count - 1)
        groove = math.cos(math.tau * s * 9) < -0.7
        for v in ring:
            if v.co.z < centers[i].z:
                v.co.z = centers[i].z - (centers[i].z - v.co.z) * 0.86
            k.tags[v.index] = "R" if groove else "base"
    for v in k.bm.verts:
        v.co.z = max(0.0, v.co.z)
    bvh = k.bvh()
    side = UP.cross(t).normalized()

    def on_head(d, out=0.0):
        d = d.normalized()
        loc, nor, _, _ = bvh.ray_cast(hc + d * 0.5, -d)
        return loc + nor * out, nor

    for sgn in (-1.0, 1.0):
        loc, nor = on_head(t * 0.8 + side * (sgn * 0.42) + UP * 0.42, 0.002)
        ellipsoid(k, loc, (0.0095, 0.0095, 0.0105), 6, 4, axis=nor)
    k.mark("B")
    for sgn in (-1.0, 1.0):
        loc, nor = on_head(t * 0.62 + side * (sgn * 0.72) - UP * 0.02, 0.0005)
        ellipsoid(k, loc, (0.013, 0.010, 0.0028), 6, 2, axis=nor)
    k.mark("A")
    smile = [on_head(t + side * (0.30 * math.cos(a)) + UP * (0.05 - 0.16 * math.sin(a)), 0.0018)[0] for a in
             (math.pi * i / 4 for i in range(5))]
    tube(k, smile, 0.0030, 4)
    k.mark("B")
    for i in (9, 16, 23):
        loc, nor = surf(bvh, centers[i].x, centers[i].y)
        ellipsoid(k, loc + nor * 0.001, (0.013, 0.013, 0.006), 8, 2, axis=nor)
    k.mark("G")
    return finish(k, "Decor_Burrows_WormCushion", col, "palette.event_burrows.worm_cushion")


# ---------------------------------------------------------------- Coffret decorations

def build_garden_shed(col):
    k = Kit()
    rbox(k, Vector((0.0, 0.0, 0.45)), (0.92, 0.78, 0.90), 0.03, 2)
    k.mark("base")
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            rbox(k, Vector((sx * 0.445, sy * 0.375, 0.46)), (0.085, 0.085, 0.92), 0.015, 1)
    k.mark("G")
    gv = [k.bm.verts.new(Vector(p)) for p in ((-0.46, -0.39, 0.90), (0.46, -0.39, 0.90), (0.0, -0.39, 1.17),
                                              (-0.46, 0.39, 0.90), (0.46, 0.39, 0.90), (0.0, 0.39, 1.17))]
    k.face((gv[0], gv[1], gv[2]), smooth=False)
    k.face((gv[4], gv[3], gv[5]), smooth=False)
    k.face((gv[1], gv[4], gv[5], gv[2]), smooth=False)
    k.face((gv[3], gv[0], gv[2], gv[5]), smooth=False)
    k.mark("base")
    alpha = math.radians(31.0)
    for sgn in (-1.0, 1.0):
        rot = Matrix.Rotation(sgn * alpha, 3, 'Y')
        c = Vector((sgn * 0.285, 0.0, 1.20 - 0.285 * math.tan(alpha) + 0.02))
        rbox(k, c, (0.66, 1.02, 0.075), 0.028, 2, rot=rot)
    k.mark("R")
    tube(k, [Vector((0.0, -0.52, 1.225)), Vector((0.0, 0.52, 1.225))], 0.042, 7)
    k.mark("G")
    door = [(-0.17, 0.0), (0.17, 0.0), (0.17, 0.42)] + [(0.17 * math.cos(a), 0.42 + 0.17 * math.sin(a)) for a in
                                                       (math.pi * i / 6 for i in range(1, 6))] + [(-0.17, 0.42)]
    df = Matrix.Translation((0.0, -0.405, 0.0)) @ Matrix.Rotation(math.radians(90.0), 4, 'X') @ \
        Matrix.Diagonal((1.0, 1.0, -1.0, 1.0))
    slab(k, door, 0.03, df, chamfer=0.05, front_tag="A")
    ellipsoid(k, Vector((0.11, -0.43, 0.30)), (0.018, 0.018, 0.018), 6, 4)
    k.mark("G")
    wc = Vector((-0.478, 0.04, 0.55))
    torus(k, wc, Vector((1.0, 0.0, 0.0)), 0.12, 0.026, 12, 5)
    tube(k, [wc + Vector((-0.004, -0.12, 0.0)), wc + Vector((-0.004, 0.12, 0.0))], 0.012, 4, cap0=False, cap1=False)
    tube(k, [wc + Vector((-0.004, 0.0, -0.12)), wc + Vector((-0.004, 0.0, 0.12))], 0.012, 4, cap0=False, cap1=False)
    k.mark("A")
    ellipsoid(k, wc + Vector((0.004, 0.0, 0.0)), (0.11, 0.11, 0.006), 12, 2, axis=Vector((1.0, 0.0, 0.0)))
    k.mark("B")
    rbox(k, Vector((-0.505, 0.04, 0.37)), (0.07, 0.38, 0.085), 0.012, 1)
    k.mark("G")
    for j, y in enumerate((-0.10, 0.04, 0.17)):
        daisy(k, Vector((-0.52, y, 0.43)), Vector((-0.45, 0.0, 1.0)).normalized(), 0.042, j * 0.9, petal="A", heart="G")
    for y, sgn in ((-0.03, 1.0), (0.11, -1.0)):
        round_leaf(k, Vector((-0.525, y, 0.415)), Vector((-0.5, sgn * 0.6, 0.6)), Vector((0.0, 0.0, 1.0)), 0.07, 0.03)
    k.mark("R")
    rbox(k, Vector((0.0, -0.53, 0.025)), (0.40, 0.17, 0.05), 0.012, 1)
    k.mark("base")
    for x, y, s, t in ((-0.52, -0.45, 1.6, 0.4), (0.53, -0.44, 1.3, 2.2)):
        sprout(k, Vector((x, y, 0.0)), s, t)
    k.mark("R")
    return finish(k, "Decor_Burrows_GardenShed", col, "palette.event_burrows.garden_shed")


def _arch(n, half, rise, base_z, power=2.6):
    pts = []
    for i in range(n):
        phi = math.pi * i / (n - 1)
        c, s = math.cos(phi), math.sin(phi)
        pts.append((half * math.copysign(abs(c) ** (2 / power), c), base_z + rise * abs(s) ** (2 / power)))
    return pts


def build_greenhouse(col):
    k = Kit()
    rbox(k, Vector((0.0, 0.0, 0.13)), (0.96, 0.82, 0.26), 0.025, 2)
    k.mark("base")
    arch = _arch(11, 0.42, 0.98, 0.26)
    front = [k.bm.verts.new(Vector((x, -0.38, z))) for x, z in arch]
    back = [k.bm.verts.new(Vector((x, 0.38, z))) for x, z in arch]
    for i in range(len(arch) - 1):
        k.face((front[i], back[i], back[i + 1], front[i + 1]))
    fc = k.bm.verts.new(Vector((0.0, -0.38, 0.26)))
    bc = k.bm.verts.new(Vector((0.0, 0.38, 0.26)))
    for i in range(len(arch) - 1):
        k.face((front[i + 1], front[i], fc), smooth=False)
        k.face((back[i], back[i + 1], bc), smooth=False)
    k.mark("B")
    for y in (-0.395, -0.13, 0.13, 0.395):
        out = _arch(11, 0.435, 0.995, 0.26)
        tube(k, [Vector((x, y, z)) for x, z in out], 0.022, 5, cap0=False, cap1=False)
    for i in (2, 5, 8):
        x, z = _arch(11, 0.43, 0.99, 0.26)[i]
        tube(k, [Vector((x, -0.40, z)), Vector((x, 0.40, z))], 0.016, 5, cap0=True, cap1=True)
    door = [Vector((-0.15, -0.405, 0.26)), Vector((-0.15, -0.405, 0.66))] + \
        [Vector((0.15 * math.cos(a), -0.405, 0.66 + 0.15 * math.sin(a))) for a in (math.pi * (1 - i / 6) for i in range(1, 6))] + \
        [Vector((0.15, -0.405, 0.66)), Vector((0.15, -0.405, 0.26))]
    tube(k, door, 0.018, 5, cap0=False, cap1=False)
    k.mark("A")
    ellipsoid(k, Vector((0.10, -0.42, 0.50)), (0.016, 0.016, 0.016), 6, 4)
    ellipsoid(k, Vector((0.0, 0.0, 1.30)), (0.045, 0.045, 0.05), 8, 4)
    k.mark("G")
    rbox(k, Vector((0.0, -0.50, 0.06)), (0.36, 0.16, 0.12), 0.015, 1)
    k.mark("base")
    rbox(k, Vector((-0.56, 0.02, 0.08)), (0.14, 0.62, 0.16), 0.015, 1)
    k.mark("G")
    for y, s, t in ((-0.18, 1.3, 0.3), (0.03, 1.1, 1.7), (0.22, 1.25, 3.0)):
        sprout(k, Vector((-0.56, y, 0.16)), s, t)
    k.mark("R")
    for x, s in ((0.36, 1.25), (0.545, 1.0)):
        f = Matrix.Translation((x, -0.54, 0.0)) @ Matrix.Scale(s, 4)
        lathe(k, [(0.0, 0.0), (0.048, 0.0), (0.062, 0.085), (0.072, 0.088), (0.073, 0.108), (0.064, 0.110), (0.0, 0.100)],
              9, f)
        k.mark("G")
        sprout(k, Vector((x, -0.54, 0.10 * s)), 0.9 * s, x * 7.0)
        k.mark("R")
    return finish(k, "Decor_Burrows_Greenhouse", col, "palette.event_burrows.greenhouse")


def build_wooden_bench(col):
    k = Kit()
    for j, y in enumerate((-0.12, 0.0, 0.12)):
        rbox(k, Vector((0.0, y, 0.335)), (0.92, 0.112, 0.036), 0.012, 2, rot=Matrix.Rotation(math.radians((j - 1) * 0.8), 3, 'Z'))
    tilt = Matrix.Rotation(math.radians(-14.0), 3, 'X')
    for y, z in ((0.205, 0.47), (0.235, 0.585)):
        rbox(k, Vector((0.0, y, z)), (0.92, 0.10, 0.034), 0.012, 2, rot=tilt)
    k.mark("base")
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            rbox(k, Vector((sx * 0.38, sy * 0.14, 0.158)), (0.065, 0.065, 0.316), 0.015, 1)
        rbox(k, Vector((sx * 0.38, 0.205, 0.50)), (0.055, 0.045, 0.36), 0.012, 1, rot=tilt)
        tube(k, [Vector((sx * 0.445, -0.16, 0.335)), Vector((sx * 0.452, -0.175, 0.44)), Vector((sx * 0.455, -0.08, 0.50)),
                 Vector((sx * 0.455, 0.19, 0.50))], 0.024, 7, cap0=False, cap1=True)
        ellipsoid(k, Vector((sx * 0.452, -0.18, 0.452)), (0.034, 0.034, 0.034), 8, 4)
    k.mark("G")
    for x, y, s, t in ((0.12, 0.02, 1.2, 0.3), (0.2, -0.05, 0.9, 2.3)):
        sprout(k, Vector((x, y, 0.0)), s, t)
    k.mark("R")
    daisy(k, Vector((-0.24, -0.06, 0.09)), Vector((0.0, -0.5, 1.0)).normalized(), 0.04, 0.5, petal="A", heart="B")
    tube(k, [Vector((-0.24, -0.04, 0.0)), Vector((-0.24, -0.055, 0.085))], 0.005, 4, cap0=False, cap1=False)
    k.mark("R")
    return finish(k, "Decor_Burrows_WoodenBench", col, "palette.event_burrows.wooden_bench")


def build_garden_fence(col):
    k = Kit()
    for sx in (-1.0, 1.0):
        rbox(k, Vector((sx * 0.62, 0.0, 0.28)), (0.075, 0.075, 0.56), 0.015, 1)
        ellipsoid(k, Vector((sx * 0.62, 0.0, 0.595)), (0.045, 0.045, 0.045), 8, 4)
    for z in (0.15, 0.40):
        rbox(k, Vector((0.0, 0.03, z)), (1.20, 0.03, 0.05), 0.01, 1)
    k.mark("G")
    rng = random.Random(5)
    for i in range(6):
        x = -0.5 + i * 0.2
        h = 0.48 + 0.04 * rng.random()
        pts = [(-0.05, 0.0), (0.05, 0.0), (0.05, h - 0.05)] + \
              [(0.05 * math.cos(a), h - 0.05 + 0.05 * math.sin(a)) for a in (math.pi * j / 6 for j in range(1, 6))] + \
              [(-0.05, h - 0.05)]
        f = (Matrix.Translation((x, -0.002, 0.0)) @ Matrix.Rotation(math.radians(rng.uniform(-1.5, 1.5)), 4, 'Y')
             @ Matrix.Rotation(math.radians(90.0), 4, 'X') @ Matrix.Diagonal((1.0, 1.0, -1.0, 1.0)))
        slab(k, pts, 0.024, f)
    for j, (x, y, z) in enumerate(((-0.62, -0.044, 0.36), (-0.3, -0.022, 0.46), (0.42, -0.022, 0.12))):
        daisy(k, Vector((x, y, z)), Vector((0.0, -1.0, 0.25)).normalized(), 0.045, j, petal="A", heart="B")
    for z, sgn in ((0.14, 1.0), (0.24, -1.0), (0.33, 1.0), (0.45, -1.0)):
        round_leaf(k, Vector((-0.62 + sgn * 0.03, -0.040, z)), Vector((sgn * 0.8, -0.2, 0.55)), Vector((0.0, -1.0, 0.0)),
                   0.06, 0.027, curl=0.15)
    tube(k, [Vector((-0.6, -0.042, 0.0)), Vector((-0.63, -0.043, 0.2)), Vector((-0.6, -0.043, 0.40)),
             Vector((-0.62, -0.043, 0.47))], 0.006, 4, cap0=False, cap1=True)
    for x, s, t in ((0.15, 1.1, 0.3), (0.5, 0.9, 2.0)):
        sprout(k, Vector((x, -0.06, 0.0)), s, t)
    k.mark("R")
    return finish(k, "Decor_Burrows_GardenFence", col, "palette.event_burrows.garden_fence")


# ---------------------------------------------------------------- souvenir and monument

def build_souvenir(col):
    k = Kit()
    mound(k, 0.14, 0.12, 0.06, 14, 3, seed=29, lump=0.08, top_flat=0.2)
    k.mark("G")
    bvh = k.bvh()
    rbox(k, Vector((0.0, 0.012, 0.16)), (0.035, 0.035, 0.32), 0.008, 1)
    k.mark("base")
    n = Vector((0.0, -math.cos(math.radians(10.0)), math.sin(math.radians(10.0))))
    pf = _frame(Vector((0.0, -0.010, 0.385)), n)
    lathe(k, [(0.0, -0.016), (0.100, -0.016), (0.110, -0.008), (0.112, 0.006), (0.104, 0.014), (0.094, 0.012),
              (0.088, 0.006), (0.0, 0.006)], 18, pf, marks=["base"] * 6 + ["A", "A"])
    top = pf @ Vector((0.0, 0.110, 0.0))
    sprout(k, top - UP * 0.004, 0.85, 0.2)
    k.mark("R")
    for x, y, s, t in ((0.07, -0.03, 0.9, 0.6), (-0.08, 0.02, 0.8, 2.2), (0.03, 0.07, 0.7, 4.0)):
        loc, nor = surf(bvh, x, y)
        sprout(k, loc, s, t)
    k.mark("R")
    for x, y in ((-0.05, -0.06), (0.09, 0.04)):
        loc, nor = surf(bvh, x, y)
        dew(k, loc + nor * 0.008, 0.012)
    return finish(k, "Decor_Burrows_Souvenir", col, "palette.event_burrows.souvenir")


def build_root_burrow(col):
    k = Kit()
    mound(k, 1.35, 1.15, 1.20, 28, 9, seed=303, lump=0.05, top_flat=0.10)
    k.mark("earth")
    bvh = k.bvh()
    hole(k, bvh, 0.42, 0.42, 0.38, 0.32, seg=16)
    roots = [
        ([(-0.64, -0.98, 0.0), (-0.68, -1.12, 0.36), (-0.50, -1.16, 0.80), (0.0, -1.10, 1.00), (0.50, -1.10, 0.82),
          (0.68, -1.02, 0.46), (0.72, -0.94, 0.0)], (0.15, 0.10), 8, 2),
        ([(0.0, -1.08, 0.98), (0.06, -0.86, 1.10), (0.14, -0.55, 1.20), (0.20, -0.30, 1.24)], (0.12, 0.07), 6, 2),
        ([(-0.80, -0.62, 0.62), (-0.94, -0.86, 0.46), (-0.88, -1.06, 0.20), (-0.82, -1.14, 0.0)], (0.11, 0.07), 6, 2),
        ([(0.84, -0.50, 0.72), (1.00, -0.74, 0.50), (1.02, -0.96, 0.22), (0.98, -1.04, 0.0)], (0.10, 0.065), 6, 2),
    ]
    for guides, (r0, r1), sides, per in roots:
        pts = _catmull_d([Vector(g) for g in guides], per)
        tube(k, pts, [r0 + (r1 - r0) * abs(2 * i / (len(pts) - 1) - 1) ** 0.7 for i in range(len(pts))], sides)
    # Two roots creeping down the back flanks, half in the earth.
    for guides in (((-0.30, 0.20, 1.22), (-0.80, 0.45, 0.85), (-1.15, 0.55, 0.35), (-1.30, 0.62, 0.0)),
                   ((0.40, 0.35, 1.18), (0.85, 0.62, 0.72), (1.05, 0.75, 0.25), (1.10, 0.78, 0.0))):
        pts = []
        for p in _catmull_d([Vector(g) for g in guides], 2):
            loc, nor, _, _ = bvh.find_nearest(p)
            pts.append(loc + nor * 0.035)
        tube(k, pts, [0.09 - 0.035 * i / (len(pts) - 1) for i in range(len(pts))], 6)
    k.mark("G")
    for x, y, r, h in ((0.0, 0.05, 0.42, 0.10), (-0.55, 0.30, 0.30, 0.08), (0.55, 0.15, 0.32, 0.08),
                       (0.15, 0.62, 0.30, 0.07), (-0.35, -0.45, 0.25, 0.07), (0.40, -0.40, 0.24, 0.07)):
        loc, nor = surf(bvh, x, y)
        ellipsoid(k, loc - nor * (h * 0.35), (r, r * 0.85, h), 8, 3, axis=nor)
    k.mark("R")
    bvh2 = k.bvh()
    for x, y, s, t in ((-0.05, 0.05, 5.8, 0.5), (-0.45, 0.25, 4.2, 2.0), (0.48, 0.18, 4.0, 3.6), (0.12, 0.55, 3.6, 1.2),
                       (-0.30, -0.45, 3.4, 4.6), (0.62, -0.30, 3.1, 5.5)):
        loc, nor = surf(bvh2, x, y)
        sprout(k, loc - UP * 0.02, s, t)
    k.mark("R")
    for x, y, r in ((0.22, 0.08, 0.055), (-0.30, 0.05, 0.05), (0.30, -0.25, 0.045), (-0.62, -0.10, 0.045),
                    (0.70, 0.30, 0.05), (-0.10, 0.50, 0.045), (0.85, -0.20, 0.04), (-0.85, 0.35, 0.04), (0.0, -0.70, 0.045)):
        loc, nor = surf(bvh2, x, y)
        dew(k, loc + nor * r * 0.9, r * 1.3)
    return finish(k, LANDMARK, col, "palette.event_burrows.root_burrow", mossy(0.45, 0.35, 0.8))


def _catmull_d(points, per=2):
    out = []
    n = len(points)
    for i in range(n - 1):
        p0, p1, p2, p3 = points[max(i - 1, 0)], points[i], points[i + 1], points[min(i + 2, n - 1)]
        for j in range(per):
            t = j / per
            out.append(0.5 * ((2 * p1) + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * (t * t)
                              + (3 * p1 - p0 - 3 * p2 + p3) * (t * t * t)))
    out.append(points[-1].copy())
    return out


# ---------------------------------------------------------------- build, export

BUILDERS = (build_flower_entrance, build_moss_mound, build_dew_mushrooms, build_watering_can, build_wheelbarrow,
            build_seedling_pots, build_burrow_sign, build_worm_cushion, build_garden_shed, build_greenhouse,
            build_wooden_bench, build_garden_fence, build_souvenir, build_root_burrow)


def budget(name):
    if name == LANDMARK:
        return 2500
    return 2000 if name in COFFRET else 1500


def build_all():
    col, work = _collections()
    report = {}
    for builder in BUILDERS:
        obj = builder(col)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        if tris > budget(obj.name):
            raise RuntimeError(f"{obj.name}: {tris} triangles over {budget(obj.name)}")
        co = [v.co for v in obj.data.vertices]
        lo = [min(c[a] for c in co) for a in range(3)]
        hi = [max(c[a] for c in co) for a in range(3)]
        report[obj.name] = dict(tris=tris, size=tuple(round(hi[a] - lo[a], 3) for a in range(3)),
                                min_z=round(lo[2], 4), row=ROWS[obj.name])
    bpy.ops.wm.save_mainfile()
    return report


def export_all():
    ns = {}
    exec(open(os.path.join(_root(), "SourceArt", "Blender", "Tools", "pw_export.py")).read(), ns)
    out = []
    for obj in bpy.data.collections["EXPORT"].all_objects:
        if obj.type == 'MESH':
            out.append(ns["export_static"](f"{FBX_DIR}/{obj.name}.fbx", (obj.name,))[0])
    bpy.ops.wm.save_mainfile()
    return out
