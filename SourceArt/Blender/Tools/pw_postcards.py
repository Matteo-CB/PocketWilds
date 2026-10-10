"""T42 Expeditions (doc 01 Contenu, section 7.1): the postcard set of « Prés fleuris », the team's picnic at the foot of a
hill covered with daisies, white butterflies around.

Intention (charte « fait main », section 10, step 1)
- Character: a sunny, soft, slow afternoon. A gingham blanket laid on the meadow, plump and a little rumpled at its
  edges, a small wicker picnic basket with its rose cloth folded over the rim, a round hill sprinkled with daisies
  rising behind, and cabbage-white butterflies, in the exact family of the Prairie props (pw_grassland,
  pw_grassland_t33, pw_grassland_t39_props), the T38 set (pw_grooming: GR_SetBackdrop, its round hills and backdrop
  daisies) and the T40 basket (pw_nutdrop: the same weave, the same honey wicker).
- What the Prairie already had, checked first: GR_SetBackdrop (meadow floor, three rows of round hills, a few round
  trees, ten backdrop daisies) shared by the activity sets; the kit's daisy GL_Flower_A and grass tufts (explicit entries
  of Biome_Grassland_Kit); GL_Life_Butterfly with yellow, blue and golden rows only, and the refuge scene holds it (a set
  bundle may not copy it). Missing, so made here: the blanket, the basket with its cloth, a hill covered with daisies
  (the backdrop's hills are bare and far), white butterflies (two wing poses).
- Scale, from the animals' measured bodies (DEF_AnimalVisualCatalog, LOD0 at scale 1): rabbit 0.54 m long, 0.33 m
  wide, 0.66 m tall with its ears; squirrel 0.64 x 0.30 x 0.55 m; hedgehog 0.53 x 0.33 x 0.33 m. The blanket (1.9 x
  1.1 m) holds three of them side by side, a little staggered, and the basket at its right end, with room around even
  for the squirrel's tail and a size gene of +15 %; the basket (0.31 m) is the T40 basket's size;
  the hill is 4.6 m wide and 1.15 m tall, three rabbits high behind the team; a butterfly spans 0.1 m (the refuge's are
  shown 2.3 times their mesh, about 0.1 m too).
- Counted parts, the same in every export: blanket 19 x 11 checks (gingham: stripes on both borders), 1 rolled hem;
  basket 5 woven rows of 8 over-bumps, 1 rolled rim, 1 arched handle, 1 puffed cloth, 1 flap over the front rim with
  its pale border; hill 1 mound, 84 daisies (5 round lobes and a domed heart each); butterfly 1 body, 4 rounded wings.
- Wanted imperfections: the blanket's edges rise and fall in soft waves and its checks curve toward the rounded
  corners; the hill's crest leans to the right; daisies vary in size and tilt, laid by a fixed seed; the cloth's flap
  hangs off centre.
- Nothing spiky: rounded blanket corners, rounded daisy lobes (never a star), rounded wings, no antennae.
- Budgets (triangles): blanket 2,600; meadow 900; basket 1,100; hill with its daisies 2,600; each butterfly 130.

Data contract: as pw_grassland.py (one palette row per renderer, SH_EnvironmentOpaque): color attribute PW_Regions
(R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion, y height
factor). PC_PALETTES holds the proposed rows (base, R, G, B, A, occlusion, accent); the previews use them. The
blanket's checks are crisp: each check owns its vertices (its color never bleeds into the next one) and every vertex
of the top keeps the smooth normal of the cloth (custom normals), so no seam shows in the light.

Axes: authored for the Blender front view (camera on -Y, looking +Y, X to the right of the picture). pw_export.py writes
Blender (x, y, z) as Unity (-x, z, -y); PostcardSetPipeline turns the props half a turn, so Blender +X is Unity +X (right
of the postcard) and Blender +Y is Unity +Z (away from the camera). Pivots: blanket, basket, hill and butterflies at the
center of their footprint on the ground (z = 0); a butterfly's pivot is its body's center.

Usage inside Blender (the live session or a fresh file), after pw_grassland.py and pw_nutdrop.py in the same namespace
(the T40 shape helpers and review studio are reused):
    ns["build_postcards"]()          # scene "Postcards", every object in its export collection, budgets checked
    ns["write_postcards_blend"]()    # bpy.data.libraries.write: the scene to SourceArt/Blender/Activities/
Then, still in the live session (Blender MCP only), after pw_export.py in the same namespace:
    ns["export_postcards_live"]()    # PC_FlowerMeadows.fbx in Assets/_Game/Art/Activities/Expeditions/, its header
                                     # naming BL_Activity_Postcards.blend (the file just written with these objects)
    ns["render_postcard_previews"](out_dir, refs)   # review images; temporary camera, light, materials
Or in BL_Activity_Postcards.blend itself (headless is fine), after pw_export.py in the same namespace:
    ns["finish_postcards_blend"]()   # once: the export collection takes the standard name EXPORT, file saved
    ns["export_postcards"]()         # the same FBX
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

PC_SCENE = "Postcards"
PC_COLLECTION = "PC_EXPORT"         # EXPORT is taken in the Prairie file; renamed EXPORT in the activity's own file
PC_BLEND = "SourceArt/Blender/Activities/BL_Activity_Postcards.blend"
PC_OUT = "Assets/_Game/Art/Activities/Expeditions"
PC_EXPORTS = {
    "PC_FlowerMeadows.fbx": ("PC_Blanket", "PC_Basket", "PC_DaisyHill", "PC_Butterfly_Open", "PC_Butterfly_Up", "PC_Meadow",
                             "PC_Daisy", "PC_Daisy_Clump"),
}
PC_BUDGETS = {"PC_Blanket": 2600, "PC_Basket": 1100, "PC_DaisyHill": 2600, "PC_Butterfly_Open": 180,
              "PC_Butterfly_Up": 180, "PC_Meadow": 900, "PC_Daisy": 260, "PC_Daisy_Clump": 700}

# Proposed palette rows (base, R, G, B, A, occlusion, accent), sRGB hex like DEF_Palettes_Grassland.
PC_PALETTES = {
    # warm white; R the single stripes (soft sky blue); G the crossings (deeper blue); B the rolled hem; A unused.
    # Blue on purpose: the russet squirrel, the brown rabbit and the hedgehog all stand out on it (grid point 15).
    "palette.postcard.blanket": ("FFFBF2", "B7D3EE", "7EA8DB", "6E99CF", "FFFBF2", "6D7096", "FFFFFF"),
    # wicker honey (the T40 basket's row as tuned in Unity); R the weave's over-bumps; G rim and handle; B the rose
    # cloth; A the cloth's pale border.
    "palette.postcard.basket": ("D3A05E", "EBC98C", "E4BB7C", "F2A5AE", "FFF6EC", "6F5048", "FFFFFF"),
    # meadow (the backdrop's base); R sunny crest; G daisy petals; B daisy hearts; A deeper grass on the lower slope.
    "palette.postcard.daisy_hill": ("8DC166", "BCD97C", "FAF6EC", "F2C24F", "76AA57", "4E6D5D", "FFFFFF"),
    # body warm taupe; R the white wings; G the soft warm cream at the wings' roots and the forewings' tips; B, A the body.
    # T42b: warm, never grey (the first plum-grey body and lilac tips read as grey specks from the set camera).
    "palette.postcard.butterfly_white": ("8A7466", "FFFDF8", "F1DDBE", "8A7466", "8A7466", "A8917F", "FFFFFF"),
    # T42b foreground daisies: base stem green; R white petals; G yellow heart; B leaves; A cream near the heart.
    "palette.postcard.daisy": ("6DA24F", "FFFDF7", "F2C24F", "7FB75C", "F4E6C6", "4E6D5D", "FFFFFF"),
    # the backdrop's meadow (palette.grooming.set_backdrop base); R sunny patches; G deeper patches.
    "palette.postcard.meadow": ("8DC166", "A6D07A", "7DB55C", "8DC166", "8DC166", "4E6D5D", "FFFFFF"),
}

PC_ROWS = {"PC_Blanket": "palette.postcard.blanket", "PC_Basket": "palette.postcard.basket",
           "PC_DaisyHill": "palette.postcard.daisy_hill", "PC_Butterfly_Open": "palette.postcard.butterfly_white",
           "PC_Butterfly_Up": "palette.postcard.butterfly_white", "PC_Meadow": "palette.postcard.meadow",
           "PC_Daisy": "palette.postcard.daisy", "PC_Daisy_Clump": "palette.postcard.daisy"}

UP = Vector((0.0, 0.0, 1.0))


# ---------------------------------------------------------------- scene

def _pc_scene():
    scene = bpy.data.scenes.get(PC_SCENE) or bpy.data.scenes.new(PC_SCENE)
    col = bpy.data.collections.get(PC_COLLECTION) or bpy.data.collections.new(PC_COLLECTION)
    if col.name not in scene.collection.children:
        scene.collection.children.link(col)
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    return scene, col


def _pc_replace(name):
    old = bpy.data.objects.get(name)
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _pc_object(name, bm, col, regions, occlusion, normals=None, recalc=True):
    """Like _nd_finish, plus optional custom normals: normals(index) -> Vector or None (None keeps the mesh's own).
    recalc=False keeps the winding as built (open sheets: recalc_face_normals guesses their side at random)."""
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    _pc_replace(name)
    obj = object_from_bmesh(name, bm, [col], smooth=True)
    write_layers(obj, regions, occlusion)
    if normals is not None:
        me = obj.data
        custom = []
        for v in me.vertices:
            n = normals(v.index)
            custom.append((n if n is not None else v.normal).normalized())
        me.normals_split_custom_set_from_vertices(custom)
    return obj


def _superellipse(theta, power):
    """Distance from the center to a unit superellipse along theta (1 on the axes, less toward the corners)."""
    c, s = abs(math.cos(theta)), abs(math.sin(theta))
    return (c ** power + s ** power) ** (-1.0 / power)


# ---------------------------------------------------------------- blanket

BLANKET_HALF = (0.95, 0.55)          # half size (m): 1.9 x 1.1
BLANKET_CHECKS = (19, 11)            # odd: a stripe runs along each border
BLANKET_SUB = 2                      # quads per check side
BLANKET_POWER = 7.0                  # the outline's superellipse: straight edges, soft rounded corners
BLANKET_TOP = 0.008                  # the cloth over the meadow (m)
BLANKET_WAVE = 0.0045                # the edges rise and fall this much
BLANKET_HEM = (0.007, 0.011, 0.008)  # the rolled hem's reach outward at its top, widest and foot


def _blanket_point(u, v):
    """The cloth's surface at (u, v) in [-1, 1]^2: the square pulled into the rounded outline along each ray (the checks
    curve softly toward the corners), the edges in slow waves, the middle flat where the team sits."""
    if abs(u) < 1e-9 and abs(v) < 1e-9:
        f = 1.0
    else:
        th = math.atan2(v, u)
        square = 1.0 / max(abs(math.cos(th)), abs(math.sin(th)))
        f = _superellipse(th, BLANKET_POWER) / square
    x, y = u * f * BLANKET_HALF[0], v * f * BLANKET_HALF[1]
    edge = smoothstep(0.62, 1.0, max(abs(u), abs(v)))
    wave = 0.5 + 0.32 * math.sin(u * 7.3 + 1.1) + 0.24 * math.sin(v * 6.1 - 0.4) + 0.14 * math.sin((u + v) * 11.0)
    # A wanted imperfection: the front-left corner lies a little puffed up.
    puff = 0.004 * smoothstep(0.55, 1.0, -u) * smoothstep(0.55, 1.0, -v)
    return Vector((x, y, BLANKET_TOP + edge * BLANKET_WAVE * max(0.0, min(1.2, wave)) + puff))


def _blanket_normal(u, v, h=1e-3):
    du = _blanket_point(u + h, v) - _blanket_point(u - h, v)
    dv = _blanket_point(u, v + h) - _blanket_point(u, v - h)
    n = du.cross(dv)
    return n.normalized() if n.length > 1e-12 else UP.copy()


def build_blanket(col):
    """PC_Blanket: a gingham picnic blanket 1.9 x 1.1 m, soft rounded corners, the cloth 8 mm over the meadow with slow
    waves along its edges, a plump rolled hem down to the grass, a flat underside (never seen). Each check owns its
    vertices, so the gingham stays crisp; the top's vertices share the cloth's smooth normal. Pivot at the center on
    the ground. Regions: base warm white, R single stripes, G crossings, B the hem."""
    bm = bmesh.new()
    nx, ny = BLANKET_CHECKS
    sub = BLANKET_SUB
    tags, normals = [], {}

    def uv(i, j):
        return -1.0 + 2.0 * i / (nx * sub), -1.0 + 2.0 * j / (ny * sub)

    for ci in range(nx):
        for cj in range(ny):
            stripe = (ci % 2 == 0) + (cj % 2 == 0)
            grid = []
            for a in range(sub + 1):
                row = []
                for b in range(sub + 1):
                    u, v = uv(ci * sub + a, cj * sub + b)
                    vert = bm.verts.new(_blanket_point(u, v))
                    normals[len(tags)] = _blanket_normal(u, v)
                    tags.append(("top", stripe))
                    row.append(vert)
                grid.append(row)
            for a in range(sub):
                for b in range(sub):
                    bm.faces.new((grid[a][b], grid[a + 1][b], grid[a + 1][b + 1], grid[a][b + 1]))

    # The outline, counterclockwise seen from above, on the grid's boundary lines.
    ring_uv = []
    total_x, total_y = nx * sub, ny * sub
    ring_uv += [uv(i, 0) for i in range(total_x)]
    ring_uv += [uv(total_x, j) for j in range(total_y)]
    ring_uv += [uv(i, total_y) for i in range(total_x, 0, -1)]
    ring_uv += [uv(0, j) for j in range(total_y, 0, -1)]
    edge = [_blanket_point(u, v) for u, v in ring_uv]
    n = len(edge)
    rings = []
    for k, p in enumerate(edge):
        t = edge[(k + 1) % n] - edge[k - 1]
        out = Vector((t.y, -t.x, 0.0)).normalized()
        if out.dot(Vector((p.x, p.y, 0.0))) < 0.0:
            out = -out
        z = p.z
        profile = [p, p + out * BLANKET_HEM[0] + UP * 0.0025, p + out * BLANKET_HEM[1] - UP * (z * 0.45),
                   Vector((p.x, p.y, 0.0005)) + out * BLANKET_HEM[2]]
        ring = []
        for q in profile:
            ring.append(bm.verts.new(q))
            tags.append(("hem", 0))
        rings.append(ring)
    for k in range(n):
        a, b = rings[k], rings[(k + 1) % n]
        for s in range(len(a) - 1):
            bm.faces.new((a[s], a[s + 1], b[s + 1], b[s]))
    center = bm.verts.new((0.0, 0.0, 0.0005))
    tags.append(("under", 0))
    for k in range(n):
        bm.faces.new((center, rings[(k + 1) % n][-1], rings[k][-1]))

    def regions(co, nrm, i):
        tag, stripe = tags[i]
        if tag == "top":
            return (1.0, 0.0, 0.0, 0.0) if stripe == 1 else (0.0, 1.0, 0.0, 0.0) if stripe == 2 else (0.0, 0.0, 0.0, 0.0)
        return (0.0, 0.0, 1.0, 0.0)

    def occlusion(co, nrm, i):
        tag, _ = tags[i]
        if tag == "top":
            # The folds' hollows a touch deeper; the open cloth almost clean.
            return (0.03 + 4.0 * max(0.0, BLANKET_TOP + 0.002 - co.z), co.z / 0.02)
        if tag == "hem":
            return (0.08 + 0.45 * (1.0 - smoothstep(0.0, 0.01, co.z)), co.z / 0.02)
        return (0.6, 0.0)

    # Every check is its own open sheet: the winding is built facing up (the hem outward, the underside down), kept as is.
    obj = _pc_object("PC_Blanket", bm, col, regions, occlusion, normals=lambda i: normals.get(i), recalc=False)
    return obj


# ---------------------------------------------------------------- basket with its cloth

BASKET_HALF = (0.155, 0.108)        # plan half size at the rim (m)
BASKET_POWER = 3.2                  # the plan: a soft rounded rectangle
BASKET_SEG = 24
BASKET_BUMPS = 8                    # over-bumps per woven row
# (scale of the plan, z) from the rim down to the foot; then the cloth's dome from the inner rim up.
BASKET_WALL = [
    (0.93, 0.146),                  # 0 inner rim (the cloth tucks in here)
    (1.0, 0.152),                   # 1 rim top
    (1.045, 0.142),                 # 2 rim outside
    (1.0, 0.129),                   # 3 rim under (crease)
    (1.0, 0.112),                   # 4..8 five woven rows
    (0.978, 0.091),
    (0.952, 0.070),
    (0.922, 0.049),
    (0.888, 0.028),
    (0.85, 0.008),                  # 9 foot
    (0.80, 0.0),                    # 10 foot ring on z = 0
]
BASKET_ROWS = {4: 0, 5: 1, 6: 2, 7: 3, 8: 4}
BASKET_DOME = [(0.84, 0.158), (0.66, 0.172), (0.40, 0.181)]
BASKET_DOME_TOP = 0.184
HANDLE_SPAN = 0.128                 # the handle's feet, from the center along x (inside the rim)
HANDLE_RISE = (0.128, 0.300)        # its feet's height (hidden under the cloth) and its top
HANDLE_RADIUS = 0.0105
HANDLE_SIDES = 8


def _basket_plan(th, scale):
    r = _superellipse(th, BASKET_POWER)
    return Vector((math.cos(th) * r * BASKET_HALF[0] * scale, math.sin(th) * r * BASKET_HALF[1] * scale, 0.0))


def _basket_weave(i, th):
    row = BASKET_ROWS.get(i)
    if row is None:
        return 1.0
    return 1.0 + 0.034 * math.cos(BASKET_BUMPS * th + row * math.pi)


def build_basket(col):
    """PC_Basket: a small wicker picnic basket 0.31 x 0.22 m, 0.15 m to the rim, 0.30 m to the top of its arched handle;
    five woven rows whose soft over-bumps alternate row by row (the T40 weave), a rolled rim, a puffed rose cloth filling
    the opening and one flap folded over the front rim with its pale border. Pivot at the bottom center (the foot ring on
    z = 0). Regions: base wicker, R over-bumps, G rim and handle, B cloth, A the cloth's border."""
    bm = bmesh.new()
    tags = []

    def mark(tag, count):
        tags.extend([tag] * count)

    rings = []
    for i, (scale, z) in enumerate(BASKET_WALL):
        ring = []
        for k in range(BASKET_SEG):
            th = math.tau * k / BASKET_SEG
            p = _basket_plan(th, scale * _basket_weave(i, th))
            ring.append(bm.verts.new((p.x, p.y, z)))
        rings.append(ring)
        mark(("wall", i), BASKET_SEG)
    for a, b in zip(rings, rings[1:]):
        for k in range(BASKET_SEG):
            m = (k + 1) % BASKET_SEG
            bm.faces.new((a[k], b[k], b[m], a[m]))
    under = bm.verts.new((0.0, 0.0, 0.006))
    mark(("under", 0), 1)
    for k in range(BASKET_SEG):
        bm.faces.new((under, rings[-1][(k + 1) % BASKET_SEG], rings[-1][k]))
    # The cloth's dome closes the opening from the inner rim (the cloth is tucked in there).
    dome = [rings[0]]
    for scale, z in BASKET_DOME:
        ring = []
        for k in range(BASKET_SEG):
            th = math.tau * k / BASKET_SEG
            puff = 1.0 + 0.05 * math.sin(3.0 * th + 0.7)
            p = _basket_plan(th, scale)
            ring.append(bm.verts.new((p.x, p.y, z * puff if scale < 0.8 else z)))
        dome.append(ring)
        mark(("cloth", 0), BASKET_SEG)
    for a, b in zip(dome, dome[1:]):
        for k in range(BASKET_SEG):
            m = (k + 1) % BASKET_SEG
            bm.faces.new((a[k], a[m], b[m], b[k]))
    top = bm.verts.new((0.012, -0.006, BASKET_DOME_TOP))
    mark(("cloth", 0), 1)
    for k in range(BASKET_SEG):
        bm.faces.new((dome[-1][k], dome[-1][(k + 1) % BASKET_SEG], top))

    # The flap: from under the dome near the front rim, over the rim and down the front wall, off centre to the left;
    # 6 mm clear of the widest weave bump everywhere (it never enters the wicker).
    x0 = -0.045
    front = -BASKET_HALF[1]
    path = [Vector((x0, front * 0.55, 0.162)), Vector((x0, front * 0.86, 0.163)), Vector((x0 - 0.004, front * 1.10, 0.150)),
            Vector((x0 - 0.008, front * 1.12, 0.124)), Vector((x0 - 0.012, front * 1.07, 0.094)),
            Vector((x0 - 0.014, front * 1.035, 0.080))]
    widths = [0.050, 0.050, 0.047, 0.040, 0.026, 0.006]
    flap = _nd_loft(bm, path, widths, [0.0045] * len(path), 6, up=Vector((0.0, -1.0, 0.25)).normalized(),
                    end=path[-1] + Vector((0.0, -0.002, -0.006)))
    for ring in flap:
        for k, _ in enumerate(ring):
            mark(("flap", k), 1)
    mark(("flap_tip", 0), 1)

    # The handle: an arch along x, its feet hidden under the cloth inside the rim.
    pts = []
    for j in range(9):
        t = math.pi * j / 8
        pts.append(Vector((-HANDLE_SPAN * math.cos(t), 0.0, HANDLE_RISE[0] + (HANDLE_RISE[1] - HANDLE_RISE[0]) * math.sin(t))))
    handle = _nd_loft(bm, pts, [HANDLE_RADIUS] * 9, [HANDLE_RADIUS] * 9, HANDLE_SIDES, up=Vector((0.0, -1.0, 0.0)))
    for j, ring in enumerate(handle):
        for k, _ in enumerate(ring):
            mark(("handle", j * HANDLE_SIDES + k), 1)

    def regions(co, n, i):
        tag, data = tags[i]
        th = math.atan2(co.y / BASKET_HALF[1], co.x / BASKET_HALF[0])
        if tag == "wall":
            if data in (0, 1, 2, 3):
                twist = 0.5 + 0.5 * math.cos(BASKET_BUMPS * 2 * th + data * 1.4)
                return (0.0, 1.0 - 0.5 * twist, 0.0, 0.0)
            row = BASKET_ROWS.get(data)
            if row is not None:
                over = max(0.0, math.cos(BASKET_BUMPS * th + row * math.pi))
                return (smoothstep(0.0, 0.7, over), 0.0, 0.0, 0.0)
            return (0.0, 0.0, 0.0, 0.0)
        if tag == "cloth":
            return (0.0, 0.0, 1.0, 0.0)
        if tag == "flap":
            # The border runs along both long edges of the flap (the ring's side points).
            side = abs(math.cos(math.tau * data / 6))
            return (0.0, 0.0, 1.0 - smoothstep(0.7, 0.95, side), smoothstep(0.7, 0.95, side))
        if tag == "flap_tip":
            return (0.0, 0.0, 0.0, 1.0)
        if tag == "handle":
            j, k = divmod(data, HANDLE_SIDES)
            twist = 0.5 + 0.5 * math.cos(k * math.tau / HANDLE_SIDES + j * 1.9)
            return (0.35 * twist, 1.0 - 0.35 * twist, 0.0, 0.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        tag, data = tags[i]
        h = co.z / 0.30
        if tag == "wall":
            if data == 3:
                return (0.5, h)
            if data == 0:
                return (0.3, h)
            row = BASKET_ROWS.get(data)
            if row is not None:
                th = math.atan2(co.y / BASKET_HALF[1], co.x / BASKET_HALF[0])
                return (0.05 + 0.4 * max(0.0, -math.cos(BASKET_BUMPS * th + row * math.pi)), h)
            if data >= 9:
                return (0.5, h)
            return (0.05, h)
        if tag == "cloth":
            # The folds sink near the rim and under the flap's root.
            return (0.08 + 0.3 * (1.0 - smoothstep(0.15, 0.175, co.z)), h)
        if tag == "under":
            return (0.6, h)
        if tag == "handle":
            return (0.06 + 0.3 * (1.0 - smoothstep(0.15, 0.2, co.z)), h)
        return (0.06, h)

    return _pc_object("PC_Basket", bm, col, regions, occlusion)


# ---------------------------------------------------------------- the hill covered with daisies

HILL_HALF = (2.3, 1.25)             # footprint half size (m)
HILL_POWER = 2.4
HILL_HEIGHT = 1.15
HILL_RINGS = [0.0, 0.14, 0.28, 0.42, 0.56, 0.68, 0.80, 0.90, 0.97, 1.0, 1.06]
HILL_SEG = 36
HILL_SINK = 0.035                   # the outer ring under the meadow (the hill grows from it, never floats)
DAISIES = 84
DAISY_RADIUS = (0.034, 0.056)
DAISY_LIFT = 0.010


def _hill_height(rho, th):
    if rho >= 1.0:
        return -HILL_SINK * smoothstep(1.0, 1.06, rho)
    prof = (1.0 - rho * rho) ** 1.6
    lean = 0.84 + 0.16 * math.cos(th) * math.sqrt(rho)          # the crest leans to the right of the picture
    bumps = 1.0 + 0.05 * math.sin(th * 3.0 + 0.6) * rho
    return HILL_HEIGHT * prof * lean * bumps


def _hill_point(rho, th):
    r = _superellipse(th, HILL_POWER) * rho
    return Vector((math.cos(th) * r * HILL_HALF[0], math.sin(th) * r * HILL_HALF[1], _hill_height(rho, th)))


def build_daisy_hill(col):
    """PC_DaisyHill: a round hill 4.6 m wide, 2.5 m deep, 1.15 m tall, its crest leaning right, its outer ring 3.5 cm under
    the meadow, and 84 daisies lying on its front and top (5 round lobes and a domed heart each, 6.8 to 11 cm wide), each
    10 mm over the grass, none in another, none in the hill (every lobe's tip measured above the surface). Pivot at the
    footprint's center on the ground. Regions: base meadow, R sunny crest, G daisy petals, B daisy hearts, A deeper grass
    low on the slopes."""
    bm = bmesh.new()
    tags = []
    rings = []
    for rho in HILL_RINGS[1:]:
        ring = [bm.verts.new(_hill_point(rho, math.tau * k / HILL_SEG)) for k in range(HILL_SEG)]
        rings.append(ring)
        tags.extend([("hill", 0)] * HILL_SEG)
    apex = bm.verts.new(_hill_point(0.0, 0.0))
    tags.append(("hill", 0))
    for k in range(HILL_SEG):
        bm.faces.new((apex, rings[0][k], rings[0][(k + 1) % HILL_SEG]))
    for a, b in zip(rings, rings[1:]):
        for k in range(HILL_SEG):
            m = (k + 1) % HILL_SEG
            bm.faces.new((a[k], b[k], b[m], a[m]))
    # Closed underneath: a flat cap under the meadow.
    cap = bm.verts.new((0.0, 0.0, -HILL_SINK))
    tags.append(("hill", 0))
    for k in range(HILL_SEG):
        bm.faces.new((cap, rings[-1][(k + 1) % HILL_SEG], rings[-1][k]))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    tree = BVHTree.FromBMesh(bm)

    def surface(x, y):
        hit = tree.ray_cast(Vector((x, y, 5.0)), Vector((0.0, 0.0, -1.0)))
        return hit if hit[0] is not None else None

    rng = random.Random(4242)
    placed = []
    tries = 0
    toward_camera = Vector((0.0, -1.0, 0.0))
    while len(placed) < DAISIES and tries < 6000:
        tries += 1
        th = rng.uniform(math.pi * 0.98, math.pi * 2.02)          # the front half, a little over the sides
        rho = math.sqrt(rng.uniform(0.0, 0.86))
        if rng.random() < 0.18:
            th = rng.uniform(0.0, math.tau)                       # a few on the top and the back's crest
            rho = rng.uniform(0.0, 0.45)
        p = _hill_point(rho, th)
        hit = surface(p.x, p.y)
        if hit is None or hit[0].z < 0.06:
            continue
        r = rng.uniform(*DAISY_RADIUS)
        if any((hit[0] - q).length < r + rq + 0.012 for q, rq, _, _ in placed):
            continue
        n = hit[1].normalized()
        facing = (n * 0.62 + UP * 0.38 + toward_camera * 0.22).normalized()
        placed.append((hit[0].copy(), r, facing, rng.uniform(0.0, math.tau)))
    if len(placed) < DAISIES:
        raise RuntimeError(f"PC_DaisyHill: only {len(placed)} of {DAISIES} daisies found room")

    lifted = 0
    for center, r, facing, turn in placed:
        u = facing.orthogonal().normalized()
        w = facing.cross(u).normalized()
        # Five round lobes: each a rounded tip (its middle point) between two lower shoulders; the shoulders of two
        # neighbours meet in a shallow valley (never a point: no star).
        outline = []
        for lobe in range(5):
            for offset, reach in ((-0.28, 0.78), (0.0, 1.0), (0.28, 0.78)):
                a = turn + (lobe + offset) / 5 * math.tau
                outline.append((math.cos(a), math.sin(a), r * reach))
        base = center + facing * DAISY_LIFT
        pts = [base + (u * cx + w * cy) * rr for cx, cy, rr in outline]
        # Lift the whole daisy until every outline point is 3 mm over the hill (nothing in the hill).
        lift = 0.0
        for q in pts:
            hit = tree.ray_cast(q + UP * 0.5, Vector((0.0, 0.0, -1.0)))
            if hit[0] is not None:
                lift = max(lift, hit[0].z + 0.003 - q.z)
        if lift > 0.0:
            lifted += 1
            base += UP * lift
            pts = [q + UP * lift for q in pts]
        ring = [bm.verts.new(q) for q in pts]
        tags.extend([("petal", 0)] * len(ring))
        mid = bm.verts.new(base + facing * 0.004)
        tags.append(("petal", 0))
        for k in range(len(ring)):
            bm.faces.new((mid, ring[k], ring[(k + 1) % len(ring)]))
        heart = [bm.verts.new(base + facing * 0.005 + (u * math.cos(t) + w * math.sin(t)) * r * 0.34)
                 for t in (turn + j / 5 * math.tau for j in range(5))]
        tags.extend([("heart", 0)] * 5)
        top = bm.verts.new(base + facing * (0.005 + r * 0.22))
        tags.append(("heart", 0))
        for j in range(5):
            bm.faces.new((top, heart[j], heart[(j + 1) % 5]))

    def regions(co, n, i):
        tag, _ = tags[i]
        if tag == "petal":
            return (0.0, 1.0, 0.0, 0.0)
        if tag == "heart":
            return (0.0, 0.0, 1.0, 0.0)
        sunny = smoothstep(0.62, 0.97, n.z) * smoothstep(0.35, 0.9, co.z / HILL_HEIGHT) * 0.75
        low = (1.0 - smoothstep(0.0, 0.42, co.z)) * 0.55
        return (sunny, 0.0, 0.0, low)

    def occlusion(co, n, i):
        tag, _ = tags[i]
        if tag == "petal":
            return (0.02, co.z / HILL_HEIGHT)
        if tag == "heart":
            return (0.08, co.z / HILL_HEIGHT)
        foot = 0.32 * (1.0 - smoothstep(-0.02, 0.16, co.z))
        return (foot + 0.08 * smoothstep(0.2, -0.4, n.z), max(0.0, co.z) / HILL_HEIGHT)

    # Daisies are one-sided discs: recalc would flip some toward the hill; their winding faces `facing` already.
    bm.normal_update()
    _pc_replace("PC_DaisyHill")
    obj = object_from_bmesh("PC_DaisyHill", bm, [col], smooth=True)
    write_layers(obj, regions, occlusion)
    obj["pc_daisies"] = len(placed)
    obj["pc_daisies_lifted"] = lifted
    return obj


# ---------------------------------------------------------------- white butterflies

BUTTERFLY_SPAN = 0.10
BUTTERFLY_OUTLINE = 14              # points around each wing (T42b: round at the size of the postcard's near butterflies)


def build_butterfly(col, name, raise_deg):
    """PC_Butterfly_<pose>: a cabbage white 0.10 m across, a plump body along y (head toward -y) and four rounded wings
    (lenses), raised `raise_deg` from flat; pivot at the body's center. Regions: base body, R wings, G the soft grey tips
    of the forewings and the warm cream at the roots of every wing (T42b: a warm white, never grey)."""
    bm = bmesh.new()
    tags = []
    body = [Vector((0.0, y, 0.0)) for y in (-0.016, -0.009, 0.0, 0.009, 0.017)]
    _nd_loft(bm, body, [0.004, 0.0052, 0.0048, 0.0038, 0.0024], [0.004, 0.0052, 0.0048, 0.0038, 0.0024], 5,
             start=Vector((0.0, -0.0205, 0.0)), end=Vector((0.0, 0.0215, 0.0)))
    tags.extend([("body", 0)] * (5 * 5 + 2))
    half = BUTTERFLY_SPAN * 0.5
    wings = [  # (root y, reach along x, reach along y, lean of the tip along y, tip)
        (-0.003, half, 0.030, -0.012, True),
        (0.006, half * 0.72, 0.024, 0.016, False),
    ]
    tilt = math.radians(raise_deg)
    for side in (-1.0, 1.0):
        for root_y, reach, depth, lean, fore in wings:
            outline = []
            for k in range(BUTTERFLY_OUTLINE):
                a = math.tau * k / BUTTERFLY_OUTLINE
                # A rounded wing: an ellipse from the root, fuller at the outer edge.
                ex = (0.5 + 0.5 * math.cos(a)) * reach
                ey = math.sin(a) * depth * 0.5 * (0.75 + 0.25 * math.cos(a)) + lean * (ex / reach) ** 1.5
                local = Vector((side * (0.004 + ex), root_y + ey, 0.0))
                # Raised around the body's axis (y).
                x = local.x * math.cos(tilt)
                z = abs(local.x) * math.sin(tilt)
                outline.append(Vector((x, local.y, z)))
            c = sum(outline, Vector()) / len(outline)
            nrm = Vector((-side * math.sin(tilt), 0.0, math.cos(tilt)))
            _nd_lens(bm, c, outline, 0.0012, 0.0012, nrm)
            tags.extend([("fore" if fore else "hind", c.x)] * (len(outline) + 2))

    def regions(co, n, i):
        tag, data = tags[i]
        if tag == "body":
            return (0.0, 0.0, 0.0, 0.0)
        reach = math.hypot(co.x, co.z)
        tip = smoothstep(0.78, 0.98, reach / half) * 0.8 if tag == "fore" else 0.0
        root = 0.55 * (1.0 - smoothstep(0.08, 0.42, reach / half))
        return (1.0, max(tip, root), 0.0, 0.0)

    def occlusion(co, n, i):
        tag, _ = tags[i]
        if tag == "body":
            return (0.25, 0.5)
        return (0.04 + 0.2 * (1.0 - smoothstep(0.0, 0.012, math.hypot(co.x, co.z))), 0.5)

    return _pc_object(name, bm, col, regions, occlusion)


# ---------------------------------------------------------------- foreground daisies (T42b)

DAISY_PETALS = 12
# (base x, base y, height, lean x, lean y, head radius, turn) per head; the lean bends the stem toward the camera (-y).
DAISY_SINGLE = [(0.0, 0.0, 0.15, 0.010, -0.030, 0.042, 0.10)]
DAISY_CLUMP = [(-0.035, 0.010, 0.110, -0.020, -0.020, 0.036, 0.40),
               (0.012, -0.004, 0.155, 0.008, -0.035, 0.044, 0.00),
               (0.075, 0.020, 0.128, 0.030, -0.020, 0.039, 0.25)]
# (root x, root y, angle, length) per leaf, lying low around the foot.
DAISY_LEAVES = [(-0.004, 0.032, 2.4, 0.055), (0.006, -0.004, -0.5, 0.050), (0.010, 0.004, 0.9, 0.046)]


def build_daisy(col, name, heads, leaves):
    """PC_Daisy / PC_Daisy_Clump: meadow daisies for the front of the postcard (the kit's GL_Flower_A is made for the
    refuge's high camera: from the set camera its head is seen edge-on on a long stem). Short green stems bending toward
    the camera, each head tilted about 50 degrees toward it: twelve rounded petals around a domed yellow heart (never a
    star: each petal is a round tip between two shoulders, notched at its foot), two or three leaves at the base. Pivot at
    the foot on the ground; the stems start 6 mm under it (rooted). Regions: base stem, R petals, G heart, B leaves, A cream
    near the heart."""
    bm = bmesh.new()
    tags = []
    placed = []
    for bx, by, height, lx, ly, radius, turn in heads:
        base = Vector((bx, by, 0.0))
        top = base + Vector((lx, ly, height))
        pts = [base + Vector((0.0, 0.0, -0.006)), base + Vector((lx * 0.08, ly * 0.1, height * 0.32)),
               base + Vector((lx * 0.35, ly * 0.45, height * 0.66)), top]
        _nd_loft(bm, pts, [0.0050, 0.0045, 0.0040, 0.0036], [0.0050, 0.0045, 0.0040, 0.0036], 5)
        tags.extend([("stem", height)] * (len(pts) * 5))
        facing = Vector((lx * 1.5, -0.76, 0.64)).normalized()
        center = top + facing * 0.004
        for other, r in placed:
            if (other - center).length < radius + r + 0.005:
                raise RuntimeError(f"{name}: two heads too close")
        placed.append((center, radius))
        u = facing.orthogonal().normalized()
        w = facing.cross(u).normalized()
        outline = []
        for k in range(DAISY_PETALS):
            for offset, reach in ((-0.5, 0.5), (-0.3, 0.93), (0.0, 1.0), (0.3, 0.93)):
                a = turn + (k + offset) / DAISY_PETALS * math.tau
                outline.append(center + (u * math.cos(a) + w * math.sin(a)) * radius * reach)
        _nd_lens(bm, center, outline, 0.004, 0.003, facing)
        tags.extend([("petal", (center, radius))] * (len(outline) + 2))
        # The heart: a closed dome sitting just over the petals.
        ring = [bm.verts.new(center + facing * 0.0035 + (u * math.cos(t) + w * math.sin(t)) * radius * 0.3)
                for t in (turn + j / 8 * math.tau for j in range(8))]
        apex = bm.verts.new(center + facing * (0.0035 + radius * 0.3))
        under = bm.verts.new(center + facing * 0.0035)
        for j in range(8):
            bm.faces.new((apex, ring[j], ring[(j + 1) % 8]))
            bm.faces.new((under, ring[(j + 1) % 8], ring[j]))
        tags.extend([("heart", 0)] * 10)
    for lx, ly, angle, length in leaves:
        root = Vector((lx, ly, 0.004))
        d = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-d.y, d.x, 0.0))
        outline = []
        for k in range(8):
            t = k / 8 * math.tau
            along = (0.5 - 0.5 * math.cos(t)) * length
            outline.append(root + d * along + side * (math.sin(t) * length * 0.22) + UP * (0.012 * math.sin(math.pi * along / length)))
        c = sum(outline, Vector()) / len(outline)
        _nd_lens(bm, c, outline, 0.003, 0.002, (UP * 0.9 - d * 0.2).normalized())
        tags.extend([("leaf", 0)] * (len(outline) + 2))

    def regions(co, n, i):
        tag, data = tags[i]
        if tag == "petal":
            center, radius = data
            cream = 1.0 - smoothstep(0.32, 0.7, (co - center).length / radius)
            return (1.0, 0.0, 0.0, 0.85 * cream)
        if tag == "heart":
            return (0.0, 1.0, 0.0, 0.0)
        if tag == "leaf":
            return (0.0, 0.0, 1.0, 0.0)
        return (0.0, 0.0, 0.0, 0.0)

    def occlusion(co, n, i):
        tag, data = tags[i]
        if tag == "stem":
            return (0.05 + 0.25 * (1.0 - smoothstep(0.0, 0.06, co.z)), co.z / 0.16)
        if tag == "leaf":
            return (0.12, 0.0)
        if tag == "heart":
            return (0.06, 1.0)
        return (0.02, 1.0)

    return _pc_object(name, bm, col, regions, occlusion)


# ---------------------------------------------------------------- the meadow floor

MEADOW_RINGS = [0.0, 0.6, 1.3, 2.1, 3.0, 4.0, 5.2, 6.5]
MEADOW_SEG = 40


def build_meadow(col):
    """PC_Meadow: the set's flat meadow floor, a disc 13 m across on z = 0 (the T38 backdrop's floor has a hole in its
    middle, where the grooming mound stands; the postcard set lays this floor over it and keeps the backdrop's hills),
    soft sunny and deeper patches, a faint shade toward its rim. Pivot at its center. Regions: base meadow, R sunny
    patches, G deeper patches."""
    bm = bmesh.new()
    rings = []
    for r in MEADOW_RINGS[1:]:
        rings.append([bm.verts.new((math.cos(t) * r, math.sin(t) * r, 0.0))
                      for t in (math.tau * k / MEADOW_SEG + 0.37 * r for k in range(MEADOW_SEG))])
    center = bm.verts.new((0.0, 0.0, 0.0))
    for k in range(MEADOW_SEG):
        bm.faces.new((center, rings[0][k], rings[0][(k + 1) % MEADOW_SEG]))
    for a, b in zip(rings, rings[1:]):
        for k in range(MEADOW_SEG):
            m = (k + 1) % MEADOW_SEG
            bm.faces.new((a[k], b[k], b[m], a[m]))

    def regions(co, n, i):
        p = Vector((co.x * 0.42, co.y * 0.42, 1.0))
        sunny = smoothstep(0.15, 0.75, wave_noise(p, 5401))
        deep = smoothstep(0.25, 0.85, wave_noise(p * 1.3, 5402)) * (1.0 - sunny)
        return (0.55 * sunny, 0.45 * deep, 0.0, 0.0)

    def occlusion(co, n, i):
        return (0.1 * smoothstep(4.5, 6.5, math.hypot(co.x, co.y)), 0.0)

    obj = _pc_object("PC_Meadow", bm, col, regions, occlusion, recalc=False)
    return obj


# ---------------------------------------------------------------- build, write, export

def build_postcards():
    """Builds every postcard object in the scene "Postcards" (export collection) and returns triangles, sizes and the
    lowest corner of each. Never touches another scene's objects."""
    scene, col = _pc_scene()
    out = [build_blanket(col), build_basket(col), build_daisy_hill(col), build_butterfly(col, "PC_Butterfly_Open", 12.0),
           build_butterfly(col, "PC_Butterfly_Up", 62.0), build_meadow(col),
           build_daisy(col, "PC_Daisy", DAISY_SINGLE, DAISY_LEAVES[:2]),
           build_daisy(col, "PC_Daisy_Clump", DAISY_CLUMP, DAISY_LEAVES)]
    report = {}
    for obj in out:
        tris = triangle_count(obj)
        budget = PC_BUDGETS[obj.name]
        if tris > budget:
            raise RuntimeError(f"{obj.name}: {tris} triangles over its budget of {budget}")
        pts = [Vector(c) for c in obj.bound_box]
        lo = [min(p[a] for p in pts) for a in range(3)]
        hi = [max(p[a] for p in pts) for a in range(3)]
        report[obj.name] = (tris, tuple(round(hi[a] - lo[a], 4) for a in range(3)), tuple(round(x, 4) for x in lo))
    return report


def _pc_root():
    marker = os.sep + "SourceArt" + os.sep
    path = bpy.data.filepath
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ first (or pass the project root).")
    return path.split(marker)[0]


def _pc_clear_previews():
    scene = bpy.data.scenes.get(PC_SCENE)
    for obj in (scene.objects if scene else []):
        if obj.type == 'MESH':
            prev = obj.data.color_attributes.get("WORK_Preview")
            if prev is not None:
                obj.data.color_attributes.remove(prev)
            if "PW_Regions" in obj.data.color_attributes:
                obj.data.color_attributes.active_color = obj.data.color_attributes["PW_Regions"]
            if obj.name in PC_ROWS:
                obj.data.materials.clear()


def write_postcards_blend(root=None):
    """Writes the Postcards scene and everything it uses to BL_Activity_Postcards.blend (compressed). The open file
    stays as it is."""
    root = root or _pc_root()
    _pc_clear_previews()
    path = os.path.join(root, *PC_BLEND.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.data.libraries.write(path, {bpy.data.scenes[PC_SCENE]}, fake_user=True, compress=True)
    return path


def finish_postcards_blend():
    """Run once inside BL_Activity_Postcards.blend: its export collection takes the standard name, the file is saved."""
    col = bpy.data.collections.get(PC_COLLECTION)
    if col is not None and bpy.data.collections.get("EXPORT") is None:
        col.name = "EXPORT"
    bpy.ops.wm.save_mainfile(compress=True)


def export_postcards():
    """The FBX file, with pw_export.export_static (load pw_export.py in the same namespace first). Exact names, so a
    prefix never pulls in another object."""
    _pc_clear_previews()
    done = {}
    for name, names in PC_EXPORTS.items():
        path, objects = export_static(PC_OUT + "/" + name, names)
        if sorted(objects) != sorted(names):
            raise RuntimeError(f"{name}: exported {objects}, expected {names}")
        done[name] = objects
    return done


def export_postcards_live(root=None):
    """The same FBX straight from the live session that built the scene, right after write_postcards_blend() (load
    pw_export.py in the same namespace first for FBX_PRESET): the exact objects of the export collection, the project
    preset. The live file is another .blend (the Prairie's); the exporter records BL_Activity_Postcards.blend as the
    source (FBX ApplicationNativeFile), the file that holds these very objects, written a moment before."""
    import io_scene_fbx.export_fbx_bin as fbx_bin

    root = root or _pc_root()
    source = os.path.join(root, *PC_BLEND.split("/"))
    if not os.path.exists(source):
        raise RuntimeError("Write the scene first: write_postcards_blend()")
    _pc_clear_previews()
    scene = bpy.data.scenes[PC_SCENE]
    window = bpy.context.window
    previous = window.scene if window is not None else None
    if window is not None:
        window.scene = scene

    class _Data:
        def __getattr__(self, name):
            return source if name == "filepath" else getattr(bpy.data, name)

    class _Bpy:
        data = _Data()

        def __getattr__(self, name):
            return getattr(bpy, name)

    real = fbx_bin.bpy
    done = {}
    try:
        fbx_bin.bpy = _Bpy()
        for name, names in PC_EXPORTS.items():
            out = os.path.join(root, *PC_OUT.split("/"), name)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            layer = scene.view_layers[0]
            for o in scene.objects:
                o.select_set(False, view_layer=layer)
            chosen = [bpy.data.objects[n] for n in names]
            for o in chosen:
                o.hide_set(False, view_layer=layer)
                o.select_set(True, view_layer=layer)
            layer.objects.active = chosen[0]
            with bpy.context.temp_override(window=window, scene=scene, view_layer=layer):
                bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, bake_anim=False, **FBX_PRESET)
            done[name] = [o.name for o in chosen]
    finally:
        fbx_bin.bpy = real
        if window is not None and previous is not None:
            window.scene = previous
    return done


# ---------------------------------------------------------------- review images

PC_ANIMALS = {
    "Rabbit": ("SKM_Rabbit_LOD0", ("B07A52", "F2B8B5", "F3E3CC", "E98A93", "1E1414")),
    "Squirrel": ("SKM_Squirrel_LOD0", ("C9693A", "B4552E", "F4E3CB", "5E3329", "1A1210")),
    "Hedgehog": ("SKM_Hedgehog_LOD0", ("B48A66", "6E4B36", "EEDCC4", "2A1E1C", "140E0D")),
}
# The team's places on the blanket for the review mock (Blender x, y, facing yaw in degrees, 0 = facing the camera):
# the same soft triangle PostcardSetPipeline measures in Unity.
PC_MOCK_SEATS = [("Squirrel", -0.62, 0.0, 8.0), ("Rabbit", -0.20, 0.12, 0.0), ("Hedgehog", 0.22, 0.0, -8.0)]
PC_MOCK_BASKET = (0.70, 0.10, 8.0)
PC_MOCK_HILL = (0.35, 2.15)
PC_MOCK_BUTTERFLIES = [("PC_Butterfly_Open", (-0.55, 0.9, 0.95), 30.0), ("PC_Butterfly_Up", (0.75, 1.25, 1.35), -40.0),
                       ("PC_Butterfly_Open", (0.05, 1.6, 1.55), 75.0), ("PC_Butterfly_Up", (-0.25, 0.55, 0.78), -15.0)]


def render_postcard_previews(out_dir, refs, size=1.0):
    """Review images in out_dir (T42_props_*.png), EEVEE, one sun from the top left of the image, palette colors computed
    like SH_EnvironmentOpaque. refs: {"Rabbit"/"Squirrel"/"Hedgehog": BL_Animal_<Species>.blend, "grooming":
    BL_Activity_Grooming.blend, "magnifique": BL_Biome_Grassland_Magnifique.blend}, read only. Everything made here is
    removed afterwards; nothing is saved."""
    scene = bpy.data.scenes[PC_SCENE]
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    st = NdStudio(scene, size)
    props = {o.name: o for o in scene.objects if o.type == 'MESH' and o.name in PC_ROWS}
    for name, obj in props.items():
        st.dress(obj, PC_PALETTES[PC_ROWS[name]])
    files = []

    def place(obj, loc, rot_z=0.0, scale=1.0):
        obj.location = loc
        obj.rotation_euler = (0.0, 0.0, math.radians(rot_z))
        obj.scale = (scale, scale, scale)

    try:
        # 1. The props: the blanket from above at three quarters, the basket on it, both butterflies over it.
        place(props["PC_Blanket"], (0.0, 0.0, 0.0))
        place(props["PC_Basket"], (0.3, 0.18, 0.0), -14.0)
        place(props["PC_Butterfly_Open"], (-0.32, -0.05, 0.25), 25.0, 2.0)
        place(props["PC_Butterfly_Up"], (-0.05, -0.2, 0.3), -30.0, 2.0)
        ground = _nd_ground(st, 8.0)
        st.show([props["PC_Blanket"], props["PC_Basket"], props["PC_Butterfly_Open"], props["PC_Butterfly_Up"], ground])
        st.aim(Vector((0.55, -1.75, 1.35)), Vector((0.0, 0.0, 0.05)), 34.0, 1400, 900)
        files.append(st.shot(os.path.join(out_dir, "T42_props_blanket_basket.png")))

        # 2. The basket close up (its weave, the cloth and its flap, the handle).
        st.show([props["PC_Blanket"], props["PC_Basket"], ground])
        st.aim(Vector((0.62, -0.42, 0.42)), Vector((0.3, 0.18, 0.12)), 30.0, 1000, 800)
        files.append(st.shot(os.path.join(out_dir, "T42_props_basket.png")))

        # 3. The hill covered with daisies with the T38 backdrop behind, front view.
        backdrop = _nd_append_mesh(st, refs["grooming"], "GR_SetBackdrop", ND_REF_ROWS["set_backdrop"])
        # As PostcardSetPipeline lays it: twice its size, centred under the hill, its floor (FLOOR_Z -0.30 x 2) under z = 0.
        place(backdrop, (0.0, PC_MOCK_HILL[1], 0.60), 0.0, 2.0)
        place(props["PC_DaisyHill"], (PC_MOCK_HILL[0], PC_MOCK_HILL[1], 0.0))
        place(props["PC_Meadow"], (0.0, 0.0, 0.0))
        backdrop.location.z -= 0.02                       # its floor under the meadow, its hills rising through it
        st.show([props["PC_DaisyHill"], props["PC_Meadow"], backdrop])
        st.aim(Vector((0.3, -5.5, 1.3)), Vector((0.3, 2.0, 0.6)), 34.0, 1400, 800)
        files.append(st.shot(os.path.join(out_dir, "T42_props_hill.png")))

        # 4. The postcard frame mock (512 x 768 portrait, the Unity set's camera): the team on the blanket, the basket,
        # the hill behind, butterflies.
        team = []
        for species, x, y, yaw in PC_MOCK_SEATS:
            mesh, colors = PC_ANIMALS[species]
            animal = _nd_append_mesh(st, refs[species], mesh, animal=colors)
            top = props["PC_Blanket"].location.z + BLANKET_TOP + BLANKET_WAVE + 0.004
            place(animal, (x, y, top), yaw)          # the animals face Blender -Y, the camera
            team.append(animal)
        place(props["PC_Basket"], (PC_MOCK_BASKET[0], PC_MOCK_BASKET[1], BLANKET_TOP + 0.001), PC_MOCK_BASKET[2])
        flies = []
        for name, loc, yaw in PC_MOCK_BUTTERFLIES:
            fly = st.copy(props[name], PC_PALETTES[PC_ROWS[name]], Vector(loc), (0.0, 0.0, math.radians(yaw)), 2.0)
            flies.append(fly)
        st.show([props["PC_Blanket"], props["PC_Basket"], props["PC_DaisyHill"], props["PC_Meadow"], backdrop] + team + flies)
        st.aim(Vector((0.05, -4.45, 1.2)), Vector((0.05, 0.4, 0.62)), 35.0, 512, 768)   # the Unity set camera
        files.append(st.shot(os.path.join(out_dir, "T42_props_postcard.png")))
    finally:
        st.remove()
        _pc_clear_previews()
    return files
