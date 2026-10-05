"""SmallQuadruped species pipeline: the Rabbit recipe (T06, T07, T09, T11) as reusable steps (T23).

Each step is one short call from Blender MCP (long single calls can hang the bridge):

    import pw_species as ps
    bpy.ops.wm.open_mainfile(filepath=r".../BL_Animal_Rabbit.blend")   # own call
    ps.start_from(r".../BL_Animal_Rabbit.blend", "Squirrel")   # same skeleton, sockets and capture helper
    ps.build_blockout("Squirrel")       # metaball volumes from SPECS (editable in WORK)
    ps.place_features("Squirrel")       # bead eyes and nose on the blockout surface
    ps.build_lod0("Squirrel")           # quad body + beads + PW_Regions
    ps.skin_lod0("Squirrel")            # heat weights + family rules
    ps.build_lods("Squirrel")           # LOD1 quadriflow, LOD2 symmetric collapse, weights from LOD0
    ps.place_sockets("Squirrel")
    pw_masks.apply("Squirrel"); pw_export.export_species_model("Squirrel")

Blender space: animal faces -Y, Z up, feet on z = 0. The skeleton SK_SmallQuadruped is never edited
here, so every AN_SmallQuadruped clip plays on every species of the family.
"""
import math
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import intersect_point_line

ARMATURE = "SK_SmallQuadruped"
# Metaball element radius: with stiffness 2 the surface of an ellipsoid lands on its half sizes.
K = 1.0 / 0.575

EYE = (0, 0, 0, 1)
NOSE = (0, 0, 1, 0)


def _ell(center, half, rot=(0, 0, 0), stiff=2.0, mirror=False, part=None):
    """part="head": the volume moves rigidly with the Head bone (face beads never pop out)."""
    return dict(center=center, half=half, rot=rot, stiff=stiff, mirror=mirror, part=part)


SPECS = {
    # Squirrel (A-SP-002): slightly narrower face than Rabbit, compact body, front paws tucked close to
    # the chest, small tufted ears on the Ear_01 bones, very large curled tail as one solid volume.
    "Squirrel": dict(
        blockout=[
            _ell((0, -0.080, 0.300), (0.132, 0.126, 0.132), part="head"),
            _ell((0.058, -0.128, 0.258), (0.050, 0.050, 0.046), stiff=1.6, mirror=True, part="head"),  # cheeks
            _ell((0, -0.176, 0.262), (0.040, 0.032, 0.030), stiff=1.4, part="head"),  # small muzzle
            _ell((0, -0.005, 0.135), (0.098, 0.092, 0.100)),                        # chest
            _ell((0, 0.090, 0.140), (0.112, 0.105, 0.112)),                         # body
            _ell((0.078, 0.115, 0.088), (0.055, 0.072, 0.068), mirror=True),        # haunches
            _ell((0.080, 0.050, 0.026), (0.036, 0.072, 0.026), mirror=True),        # hind feet
            _ell((0.046, -0.055, 0.065), (0.026, 0.028, 0.048), mirror=True),       # front legs, tucked
            _ell((0.046, -0.078, 0.022), (0.027, 0.032, 0.021), mirror=True),       # front paws
            # Tail: S curve hugging the back, wide plume, curling forward at the top. Back-edge lobes
            # give the stylized fluffy silhouette with one solid volume.
            _ell((0, 0.212, 0.152), (0.050, 0.052, 0.055)),
            _ell((0, 0.258, 0.212), (0.070, 0.072, 0.085), rot=(25, 0, 0)),
            _ell((0, 0.285, 0.295), (0.088, 0.094, 0.105)),
            _ell((0, 0.278, 0.388), (0.092, 0.094, 0.100)),
            _ell((0, 0.235, 0.452), (0.082, 0.078, 0.074), rot=(-35, 0, 0)),
            _ell((0, 0.172, 0.462), (0.052, 0.050, 0.046)),
            _ell((0, 0.140, 0.448), (0.036, 0.036, 0.034)),
            _ell((0, 0.332, 0.255), (0.060, 0.045, 0.060), stiff=1.2),
            _ell((0, 0.345, 0.350), (0.066, 0.050, 0.065), stiff=1.2),
            _ell((0, 0.315, 0.440), (0.062, 0.048, 0.058), stiff=1.2),
            _ell((0, 0.262, 0.498), (0.050, 0.040, 0.040), stiff=1.2),
        ],
        # Ears follow the shared ear bones: (distance along the bone axis, half sizes). The first
        # volume starts inside the skull so the ear grows out of the head.
        ears=[(-0.005, (0.034, 0.020, 0.050)), (0.040, (0.017, 0.013, 0.025))],
        tail_path=[(0, 0.205, 0.150), (0, 0.255, 0.205), (0, 0.283, 0.290), (0, 0.278, 0.385),
                   (0, 0.235, 0.452), (0, 0.172, 0.462), (0, 0.140, 0.448)],
        eyes=dict(x=0.056, z=0.305, r=0.024, sink=0.35),
        nose=dict(x=0.0, z=0.270, r=0.011, sink=0.45),
        head_center=(0, -0.080, 0.300),
        head_radii=(0.132, 0.126, 0.132),
        ear_mode="short",
        ear_gain=0.5,
        lod0_faces=1220,
        lod1_mode="collapse",
        lod1_tris=960,
        lod2_tris=420,
        back_probe_y=0.07,
    ),
}


# ---------------------------------------------------------------- helpers

def view3d():
    area = next(a for a in bpy.context.screen.areas if a.type == 'VIEW_3D')
    region = next(r for r in area.regions if r.type == 'WINDOW')
    return area, region


def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def check(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    nonmanifold = sum(1 for e in bm.edges if not e.is_manifold)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    bm.free()
    return dict(verts=len(obj.data.vertices), faces=len(obj.data.polygons), tris=tris(obj),
                nonmanifold=nonmanifold, boundary=boundary)


def _select_only(obj):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _remove_object(name):
    o = bpy.data.objects.get(name)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)
    m = bpy.data.meshes.get(name)
    if m is not None and m.users == 0:
        bpy.data.meshes.remove(m)


def ear_axes(arm):
    """Ear bone chain per side: (base, tip) in rest pose."""
    axes = {}
    for side in ("L", "R"):
        b1 = arm.data.bones[f"Ear_{side}_01"]; b2 = arm.data.bones[f"Ear_{side}_02"]
        axes[side] = (b1.head_local.copy(), b2.tail_local.copy())
    return axes


def path_project(co, points):
    """Closest point on a polyline: (arc length from the first point, distance, total length)."""
    best = (0.0, 1e9); run = 0.0
    for a, b in zip(points[:-1], points[1:]):
        a = Vector(a); b = Vector(b); seg = (b - a).length
        p, t = intersect_point_line(co, a, b)
        t = max(0.0, min(1.0, t)); p = a + (b - a) * t
        d = (co - p).length
        if d < best[1]:
            best = (run + seg * t, d)
        run += seg
    return best[0], best[1], run


def _hi_mesh(species):
    meta = bpy.data.objects[f"WORK_{species}_Blockout"]
    meta.hide_viewport = False
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(meta.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    meta.hide_viewport = True
    return me


def _inside_test(bvh):
    def inside(p):
        count = 0; origin = p.copy(); d = Vector((0, 0, 1))
        for _ in range(8):
            hit = bvh.ray_cast(origin, d)
            if hit[0] is None:
                break
            count += 1; origin = hit[0] + d * 1e-5
        return count % 2 == 1
    return inside


# ---------------------------------------------------------------- file

def start_from(template_path, species):
    """Opens a family member's file and saves it as the new species. Keeps rig, sockets and helpers,
    turns the template LOD0 into a hidden size reference and removes the rest of its geometry."""
    import os
    if os.path.normcase(bpy.data.filepath) != os.path.normcase(template_path):
        raise RuntimeError("Open the template first (in its own call): " + template_path)
    src = os.path.basename(template_path).replace("BL_Animal_", "").replace(".blend", "")
    out = template_path.replace(src, species)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    work = bpy.data.collections["WORK"]
    for o in list(bpy.data.objects):
        if o.name == f"SKM_{src}_LOD0":
            o.name = f"WORK_Ref_{src}"; o.data.name = f"WORK_Ref_{src}"
            for c in list(o.users_collection):
                c.objects.unlink(o)
            work.objects.link(o)
            o.parent = None
            for m in list(o.modifiers):
                o.modifiers.remove(m)
            o.hide_viewport = True; o.hide_render = True
        elif o.name.startswith(f"SKM_{src}_") or (o.name.startswith(f"WORK_{src}_") and o.type == 'MESH'):
            bpy.data.objects.remove(o, do_unlink=True)
    meta = bpy.data.objects.get(f"WORK_{src}_Blockout")
    if meta is not None:
        meta.name = f"WORK_{species}_Blockout"; meta.data.name = f"WORK_{species}_Blockout"
    bpy.context.scene.name = species
    t = bpy.data.texts.get("WORK_capture.py")
    if t is not None:
        s = t.as_string().replace(f"Animals\\{src}\\Captures", f"Animals\\{species}\\Captures")
        t.clear(); t.write(s)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    bpy.ops.wm.save_as_mainfile(filepath=out)
    return out


# ---------------------------------------------------------------- blockout

def build_blockout(species):
    spec = SPECS[species]
    meta = bpy.data.objects[f"WORK_{species}_Blockout"]
    mb = meta.data
    mb.elements.clear()

    def add(center, half, rot, stiff):
        e = mb.elements.new(type='ELLIPSOID')
        e.co = center; e.radius = K
        e.size_x, e.size_y, e.size_z = half
        e.rotation = Euler([math.radians(a) for a in rot], 'XYZ').to_quaternion()
        e.stiffness = stiff

    for el in spec["blockout"]:
        c = el["center"]
        add(c, el["half"], el["rot"], el["stiff"])
        if el["mirror"]:
            add((-c[0], c[1], c[2]), el["half"], (el["rot"][0], -el["rot"][1], -el["rot"][2]), el["stiff"])

    # Ears ride the shared ear bones so their animation bends them from the base.
    arm = bpy.data.objects[ARMATURE]
    for side, (base, tip) in ear_axes(arm).items():
        axis = (tip - base).normalized()
        rot = axis.to_track_quat('Z', 'Y')
        for dist, half in spec.get("ears", []):
            e = mb.elements.new(type='ELLIPSOID')
            e.co = base + axis * dist; e.radius = K
            e.size_x, e.size_y, e.size_z = half
            e.rotation = rot
            e.stiffness = 2.0
    bpy.context.view_layer.update()
    return len(mb.elements)


def place_features(species):
    """Bead eyes and nose: ray cast from the front onto the blockout surface, sunk into it."""
    spec = SPECS[species]
    hi = _hi_mesh(species)
    bm = bmesh.new(); bm.from_mesh(hi); bvh = BVHTree.FromBMesh(bm)
    placed = {}
    e = spec["eyes"]; n = spec["nose"]
    # Rabbit convention: WORK_Eye_R sits on +X.
    for name, x, z, r, sink, base_r in (("WORK_Eye_R", e["x"], e["z"], e["r"], e["sink"], 0.021),
                                        ("WORK_Eye_L", -e["x"], e["z"], e["r"], e["sink"], 0.021),
                                        ("WORK_Nose", n["x"], n["z"], n["r"], n["sink"], 0.011)):
        hit, normal, _, _ = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
        o = bpy.data.objects[name]
        o.location = hit - normal * (r * sink)
        o.scale = (r / base_r,) * 3
        o["pw_radius"] = r
        placed[name] = tuple(round(c, 4) for c in o.location)
    bm.free(); bpy.data.meshes.remove(hi)
    return placed


# ---------------------------------------------------------------- bodies

def _quad_body(name, hi_me, target_faces, pre_decimate):
    """pre_decimate: ratio of a triangle collapse before QuadriFlow (0 = none). Raw voxel meshes with
    degenerate faces make QuadriFlow refuse the input; 0.5 keeps the detail for LOD0."""
    _remove_object(name)
    me = hi_me.copy(); me.name = name
    o = bpy.data.objects.new(name, me)
    bpy.data.collections["WORK"].objects.link(o)
    _select_only(o)
    area, region = view3d()
    with bpy.context.temp_override(area=area, region=region, object=o, active_object=o,
                                   selected_objects=[o], selected_editable_objects=[o]):
        me.remesh_voxel_size = 0.005
        bpy.ops.object.voxel_remesh()
        if pre_decimate:
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.triangulate(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
            d = o.modifiers.new("Decimate", 'DECIMATE'); d.ratio = pre_decimate
            bpy.ops.object.modifier_apply(modifier="Decimate")
        o.data.use_mirror_x = True
        bpy.ops.object.quadriflow_remesh(use_mesh_symmetry=True, use_preserve_sharp=False, use_preserve_boundary=False,
                                         preserve_attributes=False, smooth_normals=True, mode='FACES',
                                         target_faces=target_faces, seed=7)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0015)
    hb = bmesh.new(); hb.from_mesh(hi_me); bvh = BVHTree.FromBMesh(hb)
    # Relax the quad grid, then snap back to the true blockout surface. Two rounds.
    for _ in range(2):
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in bm.verts:
            co = bvh.find_nearest(v.co)[0]
            if co is not None:
                v.co = co
    # Exact mirror symmetry and ground contact.
    for v in bm.verts:
        if abs(v.co.x) < 0.001:
            v.co.x = 0.0
        if v.co.z < 0.0:
            v.co.z = 0.0
    bm.to_mesh(o.data); bm.free(); hb.free()
    for p in o.data.polygons:
        p.use_smooth = True
    o.hide_viewport = True; o.hide_render = True
    return o


def _with_beads(body, out_name, beads, collection):
    """Body + bead spheres (faces fully inside the body removed) + PW_Regions tags."""
    bb = bmesh.new(); bb.from_mesh(body.data); bvh = BVHTree.FromBMesh(bb); bb.free()
    inside = _inside_test(bvh)
    final = bmesh.new(); final.from_mesh(body.data)
    region_of = {}
    for center, u, v, r, tag in beads:
        b = bmesh.new()
        bmesh.ops.create_uvsphere(b, u_segments=u, v_segments=v, radius=r)
        bmesh.ops.translate(b, verts=b.verts, vec=center)
        bmesh.ops.delete(b, geom=[f for f in b.faces if all(inside(x.co) for x in f.verts)], context='FACES')
        tmp = bpy.data.meshes.new("tmp"); b.to_mesh(tmp); b.free()
        n0 = len(final.verts); final.from_mesh(tmp); bpy.data.meshes.remove(tmp)
        for i in range(n0, len(final.verts)):
            region_of[i] = tag
    _remove_object(out_name)
    me = bpy.data.meshes.new(out_name); final.to_mesh(me); final.free()
    for p in me.polygons:
        p.use_smooth = True
    attr = me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    for i, d in enumerate(attr.data):
        d.color = region_of.get(i, (0, 0, 0, 0))
    o = bpy.data.objects.new(out_name, me)
    bpy.data.collections[collection].objects.link(o)
    bpy.data.collections["EXPORT"].objects.link(o)
    return o, region_of


def _bead_sources():
    return {n: (bpy.data.objects[n].location.copy(), bpy.data.objects[n].get("pw_radius", 0.011 if n == "WORK_Nose" else 0.023))
            for n in ("WORK_Eye_R", "WORK_Eye_L", "WORK_Nose")}


def build_lod0(species):
    spec = SPECS[species]
    hi = _hi_mesh(species)
    body = _quad_body(f"WORK_{species}_BodyRemesh", hi, spec["lod0_faces"], pre_decimate=0.5)
    bpy.data.meshes.remove(hi)
    src = _bead_sources()
    beads = [(src["WORK_Eye_R"][0], 8, 6, src["WORK_Eye_R"][1], EYE),
             (src["WORK_Eye_L"][0], 8, 6, src["WORK_Eye_L"][1], EYE),
             (src["WORK_Nose"][0], 6, 4, src["WORK_Nose"][1], NOSE)]
    lod0, _ = _with_beads(body, f"SKM_{species}_LOD0", beads, "GEO")
    for n in src:
        bpy.data.objects[n].hide_viewport = True; bpy.data.objects[n].hide_render = True
    return check(lod0)


# ---------------------------------------------------------------- skinning

def _get(vg, v):
    return {vg[g.group].name: g.weight for g in v.groups}


def _put(vg, v, w):
    w = {k: x for k, x in w.items() if x > 1e-4}
    total = sum(w.values()) or 1.0
    for g in list(v.groups):
        vg[g.group].remove([v.index])
    for k, x in w.items():
        vg[k].add([v.index], x / total, 'REPLACE')


def _blend(a, b, t):
    keys = set(a) | set(b)
    return {k: a.get(k, 0.0) * (1 - t) + b.get(k, 0.0) * t for k in keys}


def tail_weights(species, co):
    """(membership 0..1, weights) for the solid tail volume, or (0, None)."""
    spec = SPECS[species]
    path = spec.get("tail_path")
    if not path:
        return 0.0, None
    s, d, total = path_project(co, path)
    if d > 0.13:
        return 0.0, None
    member = smooth(0.005, 0.05, s) * max(smooth(0.17, 0.22, co.y), smooth(0.30, 0.36, co.z))
    upper = smooth(0.08, 0.20, s)
    return member, {"Tail_01": 1.0 - upper, "Tail_02": upper}


def _ear_weights(species, arm, co):
    spec = SPECS[species]
    side = "L" if co.x > 0 else "R"
    base, tip = ear_axes(arm)[side]
    p, t = intersect_point_line(co, base, tip)
    radial = (co - p).length
    if spec["ear_mode"] == "short":
        if co.z < 0.37 or radial > 0.05 or t < -0.15 or t > 0.65:
            return None
        # Small ears turn as one piece with a damped share of the family ear motion. The blend starts
        # inside the skull so the ear base bends over 5 cm instead of folding.
        ear = spec.get("ear_gain", 1.0) * smooth(-0.15, 0.10, t)
        return {"Head": 1.0 - ear, f"Ear_{side}_01": ear}
    if co.z < 0.38 or radial > 0.075 or t < -0.12 or t > 1.25:
        return None
    head = 1.0 - smooth(-0.02, 0.18, t)
    ear2 = smooth(0.35, 0.72, t)
    return {"Head": head, f"Ear_{side}_01": max(0.0, 1.0 - head - ear2), f"Ear_{side}_02": ear2}


def skin_lod0(species):
    """Heat weights, then the family rules proven on Rabbit: rigid face, soft head-to-neck ellipsoid,
    legs only drive the lower limbs, ears by bone axis, tail by its path. Max 3 influences."""
    spec = SPECS[species]
    arm = bpy.data.objects[ARMATURE]
    obj = bpy.data.objects[f"SKM_{species}_LOD0"]
    me = obj.data; vg = obj.vertex_groups
    regions = me.color_attributes["PW_Regions"].data
    for g in list(vg):
        vg.remove(g)
    for m in list(obj.modifiers):
        obj.modifiers.remove(m)
    obj.parent = None
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    area, region = view3d()
    with bpy.context.temp_override(area=area, region=region):
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    for b in arm.data.bones:
        if b.name not in vg:
            vg.new(name=b.name)

    hc = Vector(spec["head_center"])
    head_parts = []
    for el in spec["blockout"]:
        if el.get("part") == "head":
            head_parts.append((Vector(el["center"]), Vector(el["half"])))
            if el["mirror"]:
                c = el["center"]
                head_parts.append((Vector((-c[0], c[1], c[2])), Vector(el["half"])))

    def head_s(co):
        best = 1e9
        for c, h in head_parts:
            d = co - c
            best = min(best, math.sqrt((d.x / h.x) ** 2 + (d.y / h.y) ** 2 + (d.z / h.z) ** 2))
        return best

    def head_band(co):
        # Face side stays rigid further out; the back of the head bends into the neck as on Rabbit.
        d = co - hc
        front = smooth(-0.2, 0.5, -d.y / max(d.length, 1e-6))
        return 0.88 + 0.22 * front, 1.30 + 0.15 * front

    for v in me.vertices:
        co = v.co; w = _get(vg, v)
        r, g, b, a = regions[v.index].color
        if a > 0.5 or b > 0.5:
            _put(vg, v, {"Head": 1.0}); continue
        # Legs only drive the lower part of haunches and shoulders; the volume above follows the trunk.
        for side in ("L", "R"):
            k = f"HindLeg_{side}_Upper"
            if k in w:
                keep = smooth(0.15, 0.085, co.z)
                moved = w[k] * (1 - keep); w[k] -= moved; w["Pelvis"] = w.get("Pelvis", 0) + moved
            k = f"FrontLeg_{side}_Upper"
            if k in w:
                keep = smooth(0.14, 0.09, co.z)
                moved = w[k] * (1 - keep); w[k] -= moved; w["Chest"] = w.get("Chest", 0) + moved
        # Heat weights leak Tail/Ear/Head into the wrong volumes on big shapes: rebuilt below.
        w = {k: x for k, x in w.items() if not k.startswith("Ear") and not k.startswith("Tail")}
        s = head_s(co)
        e0, e1 = head_band(co)
        head_w = 1.0 - smooth(e0, e1, s)
        if head_w > 0.0:
            rest = {k: x for k, x in w.items() if k != "Head"} or {"Neck": 1.0}
            rt = sum(rest.values())
            w = {k: x / rt * (1 - head_w) for k, x in rest.items()}
            w["Head"] = w.get("Head", 0) + head_w
            if 0.0 < head_w < 1.0:
                w["Neck"] = w.get("Neck", 0) + 0.5 * (1 - head_w) * (1 - abs(2 * head_w - 1))
        elif "Head" in w and s > e1:
            w.pop("Head")
        if not w:
            w = {"Spine": 1.0}
        _put(vg, v, w)

    _select_only(obj)
    with bpy.context.temp_override(area=area, region=region, object=obj, active_object=obj):
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
        for g in vg:
            if g.name.startswith("Ear") or g.name.startswith("Tail"):
                continue
            vg.active_index = g.index
            bpy.ops.object.vertex_group_smooth(group_select_mode='ACTIVE', factor=0.35, repeat=2, expand=0.0)
        bpy.ops.object.mode_set(mode='OBJECT')

    stats = {"ear": 0, "tail": 0}
    for v in me.vertices:
        co = v.co
        r, g, b, a = regions[v.index].color
        if a > 0.5 or b > 0.5:
            _put(vg, v, {"Head": 1.0}); continue
        ew = _ear_weights(species, arm, co)
        if ew is not None:
            _put(vg, v, ew); stats["ear"] += 1; continue
        # Smoothing leaks neck weight into the face: the rigid core goes back to Head only.
        if head_s(co) <= head_band(co)[0]:
            _put(vg, v, {"Head": 1.0}); continue
        member, tw = tail_weights(species, co)
        if tw is not None and member > 0.0:
            _put(vg, v, _blend(_get(vg, v), tw, member)); stats["tail"] += 1

    with bpy.context.temp_override(area=area, region=region, object=obj, active_object=obj):
        bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.03, keep_single=True)
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=3)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    obj.parent = arm
    counts = [len([g for g in v.groups if g.weight > 0]) for v in me.vertices]
    stats["max_influences"] = max(counts); stats["unweighted"] = counts.count(0)
    return stats


# ---------------------------------------------------------------- LODs

def _collapse(src, name, target_tris):
    """Symmetric triangle collapse of a body mesh down to target_tris."""
    _remove_object(name)
    me = src.data.copy(); me.name = name
    o = bpy.data.objects.new(name, me); bpy.data.collections["WORK"].objects.link(o)
    o.hide_viewport = False
    _select_only(o)
    area, region = view3d()
    with bpy.context.temp_override(area=area, region=region, object=o, active_object=o,
                                   selected_objects=[o], selected_editable_objects=[o]):
        d = o.modifiers.new("Decimate", 'DECIMATE'); d.decimate_type = 'COLLAPSE'
        d.use_symmetry = True; d.symmetry_axis = 'X'; d.use_collapse_triangulate = True
        d.ratio = target_tris / max(1, tris(src))
        bpy.ops.object.modifier_apply(modifier="Decimate")
    for p in o.data.polygons:
        p.use_smooth = True
    o.hide_viewport = True; o.hide_render = True
    return o


def build_lods(species):
    spec = SPECS[species]
    arm = bpy.data.objects[ARMATURE]
    lod0 = bpy.data.objects[f"SKM_{species}_LOD0"]
    if spec.get("lod1_mode") == "collapse":
        # Small features (squirrel ears) survive a collapse of LOD0 better than a coarse QuadriFlow.
        lod1_body = _collapse(bpy.data.objects[f"WORK_{species}_BodyRemesh"], f"WORK_{species}_LOD1_Body",
                              spec["lod1_tris"] - 60)
    else:
        hi = _hi_mesh(species)
        lod1_body = _quad_body(f"WORK_{species}_LOD1_Body", hi, spec["lod1_faces"], pre_decimate=0.2)
        bpy.data.meshes.remove(hi)
    # LOD2: symmetric collapse of LOD1 (triangles are fine for the far LOD).
    lod2_body = _collapse(lod1_body, f"WORK_{species}_LOD2_Body", spec["lod2_tris"] - 25)
    area, region = view3d()

    src = _bead_sources()
    report = {}
    lod1_beads = [(src["WORK_Eye_R"][0], 6, 4, src["WORK_Eye_R"][1], EYE),
                  (src["WORK_Eye_L"][0], 6, 4, src["WORK_Eye_L"][1], EYE),
                  (src["WORK_Nose"][0], 5, 3, src["WORK_Nose"][1], NOSE)]
    # LOD2 eyes: slightly bigger, re-placed on the simplified surface so they never sink.
    bb = bmesh.new(); bb.from_mesh(lod2_body.data); bvh2 = BVHTree.FromBMesh(bb); bb.free()
    lod2_beads = []
    for n in ("WORK_Eye_R", "WORK_Eye_L"):
        loc, r = src[n]
        hit, normal, _, _ = bvh2.ray_cast(Vector((loc.x, -1.0, loc.z)), Vector((0, 1, 0)))
        rr = r * 1.13
        lod2_beads.append((hit - normal * (rr * 0.25), 6, 3, rr, EYE))
    for level, body, beads in ((1, lod1_body, lod1_beads), (2, lod2_body, lod2_beads)):
        o, region_of = _with_beads(body, f"SKM_{species}_LOD{level}", beads, "LOD")
        o.parent = arm
        mod = o.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
        dt = o.modifiers.new("WeightTransfer", 'DATA_TRANSFER')
        dt.object = lod0; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}
        dt.vert_mapping = 'POLYINTERP_NEAREST'; dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
        _select_only(o)
        with bpy.context.temp_override(area=area, region=region, object=o, active_object=o,
                                       selected_objects=[o], selected_editable_objects=[o]):
            bpy.ops.object.datalayout_transfer(modifier="WeightTransfer")
            bpy.ops.object.modifier_move_to_index(modifier="WeightTransfer", index=0)
            bpy.ops.object.modifier_apply(modifier="WeightTransfer")
            vg = o.vertex_groups
            for i in region_of:
                for g in list(o.data.vertices[i].groups):
                    vg[g.group].remove([i])
                vg["Head"].add([i], 1.0, 'REPLACE')
            bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.03, keep_single=True)
            bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=3)
            bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
        counts = [len(v.groups) for v in o.data.vertices]
        report[o.name] = dict(check(o), max_influences=max(counts), unweighted=counts.count(0))
    return report


# ---------------------------------------------------------------- sockets

def place_sockets(species):
    """Standard SmallQuadruped sockets, world-aligned at rest (same convention for every species)."""
    spec = SPECS[species]
    arm = bpy.data.objects[ARMATURE]
    lod0 = bpy.data.objects[f"SKM_{species}_LOD0"]
    bvh = BVHTree.FromObject(lod0, bpy.context.evaluated_depsgraph_get())

    def top(x, y):
        return bvh.ray_cast(Vector((x, y, 1.0)), Vector((0, 0, -1)))[0]

    hc = spec["head_center"]
    spec_sockets = {
        "SOCK_HeadTop": (top(0.0, hc[1] + 0.005), "Head"),
        "SOCK_Neck": (Vector((0.0, -0.080, 0.215)), "Neck"),
        "SOCK_Back": (top(0.0, spec.get("back_probe_y", 0.06)), "Spine"),
        "SOCK_TailBase": (Vector((0.0, 0.215, 0.172)), "Tail_01"),
        "SOCK_Ear_L": (arm.data.bones["Ear_L_01"].head_local.copy(), "Ear_L_01"),
        "SOCK_Ear_R": (arm.data.bones["Ear_R_01"].head_local.copy(), "Ear_R_01"),
    }
    out = {}
    for name, (co, bone) in spec_sockets.items():
        o = bpy.data.objects[name]
        o.parent = arm; o.parent_type = 'BONE'; o.parent_bone = bone
        o.matrix_world = Matrix.Translation(co)
        out[name] = (tuple(round(c, 3) for c in co), bone)
    return out
