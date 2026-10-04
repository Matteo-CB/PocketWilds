"""Region and pattern masks for animal meshes (first procedural pass, hand-editable afterwards).

Writes, on every LOD of a species:
  - color attribute PW_Regions (POINT): R secondary, G belly, B detail, A eyes. B and A are kept.
  - UV map PW_Masks (UV0 in Unity): x = mask A, y = mask B. No seams: every corner of a vertex
    gets the same value, so Unity does not split vertices.
Rules are species data (RULES dict), evaluated on rest-pose positions so all LODs match.
"""
import math
import bpy
from mathutils import Vector
from mathutils.geometry import intersect_point_line


def smoothstep(e0, e1, x):
    if e0 == e1:
        return 1.0 if x >= e1 else 0.0
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def _ear_axes(armature):
    axes = {}
    for side in ("L", "R"):
        b1 = armature.data.bones[f"Ear_{side}_01"]
        b2 = armature.data.bones[f"Ear_{side}_02"]
        axes[side] = (b1.head_local.copy(), b2.tail_local.copy())
    return axes


def rabbit_masks(co, n, ctx):
    """Returns (secondary, belly, maskA, maskB) for one rest-pose vertex."""
    axes = ctx["ears"]
    side = "L" if co.x > 0 else "R"
    base, tip = axes[side]
    p, t = intersect_point_line(co, base, tip)
    radial = (co - p).length
    on_ear = co.z > 0.38 and radial < 0.075 and -0.05 < t < 1.25

    secondary = 0.0
    if on_ear:
        facing = n.dot(Vector((0, -1, 0)))
        secondary = smoothstep(0.2, 0.6, facing) * smoothstep(0.10, 0.25, t) * (1.0 - smoothstep(0.80, 0.97, t))

    head_c = ctx["head_center"]
    d = co - head_c
    head_s = math.sqrt((d.x / 0.150) ** 2 + (d.y / 0.135) ** 2 + (d.z / 0.140) ** 2)

    belly = 0.0
    if not on_ear:
        front = max(n.dot(Vector((0, -1, 0))), 0.85 * n.dot(Vector((0, 0, -1))))
        body = smoothstep(0.25, 0.65, front) * (1.0 - smoothstep(0.20, 0.26, co.z)) * (1.0 - smoothstep(0.04, 0.12, co.y))
        body *= smoothstep(0.85, 1.05, head_s)
        muzzle = smoothstep(0.070, 0.030, (co - ctx["nose"]).length)
        tail = smoothstep(0.205, 0.235, co.y) * smoothstep(0.11, 0.13, co.z) * (1.0 - smoothstep(0.06, 0.08, abs(co.x)))
        belly = max(body, muzzle, tail)

    # Mask A: soft spots on back, sides, one eye patch and the left ear tip.
    mask_a = 0.0
    for center, radius in ctx["spots"]:
        # Wide soft edge: values are interpolated per vertex, a steep edge shows the triangles.
        mask_a = max(mask_a, smoothstep(radius * 1.15, radius * 0.35, (co - Vector(center)).length))
    if on_ear and side == "L":
        mask_a = max(mask_a, smoothstep(0.55, 0.7, t))
    mask_a *= 1.0 - belly * 0.85

    # Mask B: Dutch-style saddle (rear half) plus both ears. Reserved for future variants.
    mask_b = smoothstep(0.02, 0.06, co.y)
    if on_ear:
        mask_b = max(mask_b, smoothstep(0.05, 0.2, t))
    mask_b *= 1.0 - belly

    return secondary, belly, mask_a, mask_b


RULES = {
    "Rabbit": dict(
        fn=rabbit_masks,
        head_center=(0.0, -0.085, 0.30),
        spots=[((0.06, 0.12, 0.27), 0.060), ((-0.07, 0.03, 0.25), 0.055), ((0.0, 0.17, 0.22), 0.050),
               ((0.125, 0.10, 0.15), 0.055), ((-0.125, 0.13, 0.12), 0.055), ((0.11, -0.01, 0.12), 0.045),
               ((0.075, -0.17, 0.33), 0.045)],
    ),
}


def apply(species, armature_name="SK_SmallQuadruped", nose_object="WORK_Nose"):
    rule = RULES[species]
    armature = bpy.data.objects[armature_name]
    ctx = {
        "ears": _ear_axes(armature),
        "head_center": Vector(rule["head_center"]),
        "nose": bpy.data.objects[nose_object].location.copy(),
        "spots": rule["spots"],
    }
    report = []
    for lod in range(3):
        obj = bpy.data.objects.get(f"SKM_{species}_LOD{lod}")
        if obj is None:
            continue
        me = obj.data
        regions = me.color_attributes["PW_Regions"]
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        uv = me.uv_layers.new(name="PW_Masks")
        per_vertex = []
        for v in me.vertices:
            r, g, b, a = regions.data[v.index].color
            if a > 0.5 or b > 0.5:
                # Eyes and nose keep their own tags and no pattern.
                per_vertex.append((0.0, 0.0))
                continue
            sec, belly, ma, mb = rule["fn"](v.co, v.normal, ctx)
            regions.data[v.index].color = (sec, belly, b, a)
            per_vertex.append((ma, mb))
        for loop in me.loops:
            uv.data[loop.index].uv = per_vertex[loop.vertex_index]
        report.append((obj.name, len(me.vertices)))
    return report
