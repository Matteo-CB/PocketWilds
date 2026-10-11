"""Grassland T47 (Meadow Pack): Decor_CozyBurrow, « Terrier douillet » (decor.cozy_burrow), a player decoration.

Intention (charte « fait main », section 10, step 1)
- Character: a soft mossy earth mound a field hamster would call home. Round and plump, a round dark entrance at the
  front with a tiny wooden frame (two stubby posts and a rounded lintel), moss cushions on the top, five round daisies.
- Scale: footprint about 1.2 x 1.0 m, 0.4 m tall (the hamster is 0.42 m long, the rabbit 0.65 m with its ears), so the
  entrance reads at the size of the meadow animals.
- Wanted imperfections: the mound is lumpy by a fixed-seed wave noise; the moss cushions and daisies are uneven.
- Nothing spiky; budget under 1500 triangles.

Data contract: as pw_grassland_t39_props.py (one palette row per renderer, SH_EnvironmentOpaque): color attribute
PW_Regions (R/G/B/A weights toward palette columns 1-4 over the base color) and uv PW_Occlusion (x baked occlusion,
y height factor). Regions: base = earth, R = moss, G = honey wood (frame) and daisy hearts, B = the entrance shade,
A = daisy petals. T47_PALETTES holds the proposed row (base, R, G, B, A, occlusion, accent).

Axes: pw_export.py writes Blender (x, y, z) as Unity (-x, z, -y). Pivot at the center of the base on the ground; the
entrance faces -Y (Unity +Z).

Usage inside Blender, in BL_Biome_Grassland_T47.blend (saved under SourceArt), with pw_grassland.py,
pw_grassland_t39_props.py and pw_export.py executed in the same namespace first:
    ns["build_t47_props"]()      # Decor_CozyBurrow in the EXPORT collection; returns triangles, size, pivot
    ns["export_t47"]()           # Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T47.fbx, saves the blend
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

T47_BLEND = "SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_T47.blend"
T47_FBX = "Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T47.fbx"
T47_BUDGETS = {"Decor_CozyBurrow": 1500}
T47_PALETTES = {
    "palette.grassland.cozy_burrow": ("8A6646", "7FAF5A", "D9A85A", "3B2A22", "F8F4EA", "4A3A2E", "FFFFFF"),
}

BURROW_RADII = Vector((0.56, 0.47, 0.40))
ENTRANCE = dict(width=0.115, height=0.105, center_z=0.125)


def _t47_collections():
    scene = bpy.context.scene
    out = []
    for name in ("EXPORT", "WORK"):
        col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if col.name not in scene.collection.children:
            scene.collection.children.link(col)
        out.append(col)
    return out


def _mound_point(theta, phi):
    """Lumpy dome surface: phi 0 at the top, pi/2 at the ground."""
    d = Vector((math.sin(phi) * math.cos(theta), math.sin(phi) * math.sin(theta), math.cos(phi)))
    p = Vector((d.x * BURROW_RADII.x, d.y * BURROW_RADII.y, d.z * BURROW_RADII.z))
    bump = 1.0 + 0.045 * wave_noise(d * 2.3, 47, octaves=2) * math.sin(phi) ** 0.5
    p = Vector((p.x * bump, p.y * bump, p.z * bump))
    # A softer flatter top: the dome is a cushion, not a ball.
    p.z = p.z * (0.92 + 0.08 * math.sin(phi))
    return p


def _surface_z(x, y, bvh):
    hit = bvh.ray_cast(Vector((x, y, 2.0)), Vector((0, 0, -1)))
    return hit[0], hit[1]


def build_cozy_burrow(col):
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    tags = []

    def mark(tag):
        tags.extend([tag] * (len(bm.verts) - len(tags)))

    seg, rings = 26, 8
    top = bm.verts.new(_mound_point(0.0, 0.0))
    grid = []
    for j in range(1, rings + 1):
        phi = (math.pi / 2) * j / rings
        grid.append([bm.verts.new(_mound_point(math.tau * k / seg, phi)) for k in range(seg)])
    # Skirt 2 cm under the ground: the mound is rooted, never set on the grass.
    grid.append([bm.verts.new(Vector((v.co.x * 1.01, v.co.y * 1.01, -0.02))) for v in grid[-1]])
    for k in range(seg):
        bm.faces.new((top, grid[0][k], grid[0][(k + 1) % seg]))
    for a, b in zip(grid, grid[1:]):
        for k in range(seg):
            m = (k + 1) % seg
            bm.faces.new((a[k], b[k], b[m], a[m]))
    mark("earth")
    bm.verts.ensure_lookup_table()
    tmp = bmesh.new()
    tmp_me = bpy.data.meshes.new("WORK_tmp")
    bm.to_mesh(tmp_me)
    tmp.from_mesh(tmp_me)
    bvh = BVHTree.FromBMesh(tmp)
    tmp.free()
    bpy.data.meshes.remove(tmp_me)

    # Entrance: a dark round mouth just proud of the front surface, with a short tunnel ring sinking into the mound.
    hit, nor, _, _ = bvh.ray_cast(Vector((0.0, -2.0, ENTRANCE["center_z"])), Vector((0, 1, 0)))
    n = Vector((0.0, -1.0, 0.0)).lerp(nor, 0.5).normalized()
    u = Vector((1.0, 0.0, 0.0))
    w = n.cross(u).normalized()
    if w.z < 0:
        w = -w
    mouth = hit + n * 0.006
    rim = [bm.verts.new(mouth + u * (math.cos(a) * ENTRANCE["width"]) + w * (math.sin(a) * ENTRANCE["height"]))
           for a in (math.tau * k / 12 for k in range(12))]
    deep = [bm.verts.new(mouth - n * 0.05 + (v.co - mouth) * 0.72) for v in rim]
    back = bm.verts.new(mouth - n * 0.06)
    for k in range(12):
        m = (k + 1) % 12
        bm.faces.new((rim[k], rim[m], deep[m], deep[k]))
        bm.faces.new((deep[k], deep[m], back))
    mark("hole")

    # Tiny wooden frame: two stubby posts and a rounded lintel arching over the mouth, a little in front of it.
    frame_c = mouth + n * 0.012
    ring_r = 0.016
    arc = []
    for i in range(11):
        a = math.pi * i / 10
        c = frame_c + u * (math.cos(a) * (ENTRANCE["width"] + 0.022)) + w * (math.sin(a) * (ENTRANCE["height"] + 0.020))
        arc.append(c)
    posts = [arc[0] - w * 0.0, arc[-1] - w * 0.0]
    path_r = [frame_c + u * (ENTRANCE["width"] + 0.022) - w * (ENTRANCE["center_z"] + 0.02)] + arc[:1]
    path_l = arc[-1:] + [frame_c - u * (ENTRANCE["width"] + 0.022) - w * (ENTRANCE["center_z"] + 0.02)]
    for path in (path_r, arc, path_l):
        rings_v = []
        for i, c in enumerate(path):
            t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
            a1 = n.cross(t).normalized()
            rings_v.append([bm.verts.new(c + (n * math.cos(b) + a1 * math.sin(b)) * ring_r) for b in (math.tau * k / 5 for k in range(5))])
        for r0, r1 in zip(rings_v, rings_v[1:]):
            for k in range(5):
                m = (k + 1) % 5
                bm.faces.new((r0[k], r0[m], r1[m], r1[k]))
    del posts
    mark("wood")

    # Moss cushions on the top and shoulders (each a soft low ellipsoid, half sunk in the mound).
    for x, y, r, h in ((0.05, 0.05, 0.16, 0.05), (-0.20, 0.12, 0.12, 0.04), (0.24, -0.05, 0.11, 0.035),
                       (-0.06, -0.22, 0.10, 0.035), (0.18, 0.24, 0.10, 0.035), (-0.30, -0.10, 0.09, 0.03)):
        p, nn = _surface_z(x, y, bvh)
        if p is None:
            continue
        _ellipsoid(bm, p - nn * (h * 0.35), Vector((r, r * 0.85, h)), 7, 3, axis=nn)
    mark("moss")

    # Five round daisies on the moss and the earth, facing up and a little outward.
    for k, (x, y, r) in enumerate(((0.10, -0.02, 0.040), (-0.16, 0.18, 0.034), (0.28, 0.06, 0.036),
                                   (-0.26, -0.18, 0.032), (0.04, 0.26, 0.034))):
        p, nn = _surface_z(x, y, bvh)
        if p is None:
            continue
        facing = (nn + Vector((0, 0, 1.2))).normalized()
        n0 = len(bm.verts)
        petals, heart = _add_daisy(bm, p + nn * 0.045 + facing * 0.004, facing, r, 0.7 * k)
        tags.extend(["petal"] * petals + ["heart"] * heart)
        assert len(tags) == n0 + petals + heart

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = object_from_bmesh("Decor_CozyBurrow", bm, [col])
    height = max(v.co.z for v in obj.data.vertices)
    weights = {"earth": (0, 0, 0, 0), "hole": (0, 0, 1, 0), "wood": (0, 1, 0, 0), "moss": (1, 0, 0, 0),
               "petal": (0, 0, 0, 1), "heart": (0, 1, 0, 0)}

    def regions(co, nrm, i):
        tag = tags[i]
        if tag == "earth":
            # Moss creeps over the top of the mound; the earth shows on the flanks.
            moss = smoothstep(0.45, 0.85, nrm.z) * smoothstep(0.16, 0.30, co.z)
            return (moss * 0.85, 0.0, 0.0, 0.0)
        return weights[tag]

    def occlusion(co, nrm, i):
        tag = tags[i]
        h = min(1.0, max(0.0, co.z) / height)
        if tag == "hole":
            return (0.65, h)
        if tag in ("petal", "heart"):
            return (0.03, h)
        return (0.30 * (1.0 - smoothstep(0.0, 0.10, co.z)), h)

    write_layers(obj, regions, occlusion)
    return obj


def build_t47_props():
    col, work = _t47_collections()
    obj = build_cozy_burrow(col)
    tris = triangle_count(obj)
    if tris > T47_BUDGETS[obj.name]:
        raise RuntimeError(f"{obj.name}: {tris} triangles over {T47_BUDGETS[obj.name]}")
    pts = [Vector(c) for c in obj.bound_box]
    lo = [min(p[a] for p in pts) for a in range(3)]
    hi = [max(p[a] for p in pts) for a in range(3)]
    ground = [v.co for v in obj.data.vertices if v.co.z <= 0.0]
    footprint = (round(max(c.x for c in ground) - min(c.x for c in ground), 3), round(max(c.y for c in ground) - min(c.y for c in ground), 3))
    bpy.ops.wm.save_mainfile()
    return {obj.name: dict(tris=tris, size=tuple(round(hi[a] - lo[a], 3) for a in range(3)), footprint=footprint,
                           pivot_min=tuple(round(x, 3) for x in lo))}


def export_t47():
    root = project_root()
    chosen = [o for o in bpy.data.collections["EXPORT"].all_objects if o.type == 'MESH' and o.name.startswith("Decor_")]
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in chosen:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = chosen[0]
    out = os.path.join(root, *T47_FBX.split("/"))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, bake_anim=False, **FBX_PRESET)
    bpy.ops.wm.save_mainfile()
    return out, sorted(o.name for o in chosen)
