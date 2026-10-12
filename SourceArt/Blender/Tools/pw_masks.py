"""Region and pattern masks for animal meshes (first procedural pass, hand-editable afterwards).

Writes, on every LOD of a species:
  - color attribute PW_Regions (POINT): R secondary, G belly, B detail, A eyes. B and A are kept.
  - UV map PW_Masks (UV0 in Unity): x = mask A, y = mask B. No seams: every corner of a vertex
    gets the same value, so Unity does not split vertices.
  - UV map PW_Masks2 (UV1 in Unity), only when a rule returns a sixth value: x = mask C, y = 0 (Field Hamster back
    spots). SH_AnimalMaster reads pattern mask 3 (C) from uv1.x.
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

    # Mask B: both ear tips in a soft gradient (T46, mastery color Moon: « bouts d'oreilles bleu nuit en dégradé »).
    mask_b = smoothstep(0.50, 0.95, t) if on_ear else 0.0

    return secondary, belly, mask_a, mask_b


def _path_project(co, points):
    """Closest point on a polyline: (arc length, distance, total length)."""
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


def squirrel_masks(co, n, ctx):
    """Secondary = tail, belly = chest, chin and muzzle, mask A = tail tip and ear tufts,
    mask B = dorsal stripe (reserved)."""
    axes = ctx["ears"]
    side = "L" if co.x > 0 else "R"
    base, tip = axes[side]
    p, t = intersect_point_line(co, base, tip)
    radial = (co - p).length
    on_ear = co.z > 0.40 and radial < 0.05 and -0.05 < t < 0.65

    tail = 0.0
    s, d, total = _path_project(co, ctx["tail_path"])
    if d < 0.13 and not on_ear:
        tail = smoothstep(0.01, 0.06, s) * max(smoothstep(0.17, 0.22, co.y), smoothstep(0.30, 0.36, co.z))

    head_c = ctx["head_center"]
    hr = ctx["head_radii"]
    dh = co - head_c
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)

    belly = 0.0
    if not on_ear:
        front = max(n.dot(Vector((0, -1, 0))), 0.85 * n.dot(Vector((0, 0, -1))))
        body = smoothstep(0.25, 0.65, front) * (1.0 - smoothstep(0.19, 0.25, co.z)) * (1.0 - smoothstep(0.03, 0.11, co.y))
        body *= smoothstep(0.85, 1.05, head_s)
        nose = ctx["nose"]
        muzzle = smoothstep(0.050, 0.022, (co - nose).length)
        # Chin and throat below the nose join the chest bib.
        facing = n.dot(Vector((0, -1, 0)))
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.004, nose.z - 0.030, co.z)
        chin *= 1.0 - smoothstep(1.05, 1.25, head_s)
        belly = max(body, muzzle, chin) * (1.0 - tail)

    mask_a = 0.0
    if tail > 0.0:
        mask_a = smoothstep(0.68 * total, 0.86 * total, s) * tail
    if on_ear:
        mask_a = max(mask_a, smoothstep(0.16, 0.30, t))

    mask_b = smoothstep(0.035, 0.015, abs(co.x)) * smoothstep(-0.03, 0.02, co.y) * (1.0 - smoothstep(0.17, 0.21, co.y))
    mask_b *= smoothstep(0.2, 0.5, n.z) * (1.0 - belly) * (1.0 - tail)
    return tail, belly, mask_a, mask_b


def hedgehog_masks(co, n, ctx):
    """Secondary = spine shell (every lobe and the skin under them), belly = snout and underside,
    mask A = one mushroom spot on every third lobe, mask B = light lobe tips."""
    part = ctx.get("part", 0)
    lobes = ctx.get("lobes") or []
    nose = ctx["nose"]
    if part > 0 and part <= len(lobes):
        f = lobes[part - 1]
        span = f["tip"] - f["base"]
        t = max(0.0, min(1.0, (co - f["base"]).dot(span) / span.length_squared))
        tips = smoothstep(0.50, 0.85, t)
        spot = smoothstep(0.30, 0.42, t) * (1.0 - smoothstep(0.62, 0.74, t)) if part % 3 == 1 else 0.0
        return 1.0, 0.0, spot, tips

    # Face boundary leans forward with height: the shell starts at the forehead.
    boundary = -0.085 - 0.6 * (co.z - 0.16)
    face = smoothstep(boundary + 0.02, boundary - 0.02, co.y)
    skirt = smoothstep(0.085, 0.115, co.z)
    shell = skirt * (1.0 - face)
    underside = smoothstep(-0.25, -0.65, n.z)
    snout = smoothstep(0.065, 0.030, (co - nose).length)
    belly = max(underside, snout) * (1.0 - shell)
    return shell, belly, 0.0, 0.0


FIELDMOUSE_EARS = ((0.064, -0.078, 0.246), (-0.064, -0.078, 0.246))


def fieldmouse_masks(co, n, ctx):
    """Secondary = inner ears, tail and paws (pale pink), belly = chest, underside, chin and muzzle,
    mask A = piebald patches (spots), mask B = thin dark stripe along the back."""
    ear_c = Vector(FIELDMOUSE_EARS[0] if co.x > 0 else FIELDMOUSE_EARS[1])
    ear_d = (co - ear_c).length
    on_ear = co.z > 0.205 and ear_d < 0.060 and abs(co.x) > 0.025
    facing = n.dot(Vector((0, -1, 0)))
    inner = smoothstep(0.15, 0.45, facing) * smoothstep(0.046, 0.030, ear_d) if on_ear else 0.0

    s, d, total = _path_project(co, ctx["tail_path"])
    tail = smoothstep(0.035, 0.022, d) * smoothstep(0.005, 0.03, s) * smoothstep(0.125, 0.150, co.y) if total > 0 else 0.0
    paws = smoothstep(0.024, 0.010, co.z) * (1.0 - tail)
    secondary = max(inner, tail, paws)

    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    belly = 0.0
    if not on_ear and tail < 0.5:
        front = max(facing, 0.85 * n.dot(Vector((0, 0, -1))))
        body = smoothstep(0.25, 0.65, front) * (1.0 - smoothstep(0.12, 0.16, co.z)) * (1.0 - smoothstep(0.03, 0.11, co.y))
        body *= smoothstep(0.85, 1.05, head_s)
        under = smoothstep(-0.30, -0.70, n.z) * (1.0 - smoothstep(0.10, 0.14, co.y))
        nose = ctx["nose"]
        muzzle = smoothstep(0.045, 0.020, (co - nose).length)
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.002, nose.z - 0.026, co.z)
        chin *= 1.0 - smoothstep(1.05, 1.25, head_s)
        belly = max(body, under, muzzle, chin) * (1.0 - paws)

    mask_a = 0.0
    if not on_ear and tail < 0.5:
        for center, radius in ctx["spots"]:
            mask_a = max(mask_a, smoothstep(radius * 1.15, radius * 0.35, (co - Vector(center)).length))
        mask_a *= 1.0 - belly * 0.85

    mask_b = smoothstep(0.024, 0.010, abs(co.x)) * smoothstep(-0.11, -0.06, co.y) * (1.0 - smoothstep(0.12, 0.15, co.y))
    mask_b *= smoothstep(0.25, 0.55, n.z) * (1.0 - belly) * (1.0 - tail)
    return secondary, belly, mask_a, mask_b


GUINEAPIG_EARS = ((0.062, -0.105, 0.200), (-0.062, -0.105, 0.200))


def guineapig_masks(co, n, ctx):
    """Secondary = ears and paws (pale pink), belly = underside, chin and muzzle, mask A = big patches
    (tricolor, marshmallow, milk cloud), mask B = white blaze up the face and a belt around the middle."""
    ear_c = Vector(GUINEAPIG_EARS[0] if co.x > 0 else GUINEAPIG_EARS[1])
    on_ear = co.z > 0.180 and (co - ear_c).length < 0.034 and abs(co.x) > 0.035
    ears = 1.0 if on_ear else 0.0
    paws = smoothstep(0.022, 0.010, co.z)
    secondary = max(ears, paws)

    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    nose = ctx["nose"]
    facing = n.dot(Vector((0, -1, 0)))
    belly = 0.0
    if not on_ear:
        under = smoothstep(-0.30, -0.70, n.z)
        muzzle = smoothstep(0.050, 0.024, (co - nose).length)
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.002, nose.z - 0.030, co.z)
        chin *= 1.0 - smoothstep(1.05, 1.25, head_s)
        belly = max(under, muzzle, chin) * (1.0 - paws)

    mask_a = 0.0
    if not on_ear:
        for center, radius in ctx["spots"]:
            mask_a = max(mask_a, smoothstep(radius * 1.15, radius * 0.35, (co - Vector(center)).length))
        mask_a *= 1.0 - belly * 0.85

    blaze = smoothstep(0.026, 0.012, abs(co.x)) * smoothstep(-0.07, -0.10, co.y) * smoothstep(nose.z - 0.005, nose.z + 0.010, co.z)
    belt = smoothstep(0.015, 0.035, co.y) * (1.0 - smoothstep(0.075, 0.095, co.y))
    mask_b = max(blaze, belt) * (1.0 - belly) * (1.0 - secondary)
    return secondary, belly, mask_a, mask_b


FIELDHAMSTER_EARS = ((0.062, -0.100, 0.255), (-0.062, -0.100, 0.255))
FIELDHAMSTER_CHEEKS = ((0.066, -0.140, 0.136), (-0.066, -0.140, 0.136))


def fieldhamster_masks(co, n, ctx):
    """Secondary = inner ears and paws (pale pink), belly = white underside, chest, lower cheeks, chin and muzzle over
    the golden back. Mask A = ear glow (the whole ear), mask B = cheek band (a soft stripe over each cheek pouch,
    from under the eye to the jaw), mask C (second UV map, uv1.x) = round back spots along the spine."""
    ear_c = Vector(FIELDHAMSTER_EARS[0] if co.x > 0 else FIELDHAMSTER_EARS[1])
    ear_d = (co - ear_c).length
    on_ear = co.z > 0.220 and ear_d < 0.052 and abs(co.x) > 0.030
    facing = n.dot(Vector((0, -1, 0)))
    inner = smoothstep(0.10, 0.40, facing) * smoothstep(0.038, 0.024, ear_d) if on_ear else 0.0
    paws = smoothstep(0.024, 0.010, co.z)
    secondary = max(inner, paws)

    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    nose = ctx["nose"]
    belly = 0.0
    if not on_ear:
        under = smoothstep(-0.20, -0.60, n.z)
        # White flanks below a soft wavy line, golden above: the classic field hamster coat.
        line = 0.085 + 0.012 * math.sin(co.y * 38.0)
        low = 1.0 - smoothstep(line - 0.012, line + 0.012, co.z)
        chest = smoothstep(0.30, 0.70, facing) * (1.0 - smoothstep(0.13, 0.17, co.z)) * smoothstep(0.85, 1.05, head_s)
        muzzle = smoothstep(0.046, 0.022, (co - nose).length)
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.002, nose.z - 0.028, co.z)
        cheek_low = 0.0
        for c in FIELDHAMSTER_CHEEKS:
            d = co - Vector(c)
            if d.x * c[0] > 0:
                cheek_low = max(cheek_low, smoothstep(0.065, 0.040, d.length) * smoothstep(0.004, -0.014, d.z))
        belly = max(under, low, chest, muzzle, chin, cheek_low) * (1.0 - paws)

    mask_a = smoothstep(0.056, 0.040, ear_d) * smoothstep(0.208, 0.228, co.z) if abs(co.x) > 0.025 else 0.0

    mask_b = 0.0
    if not on_ear:
        for c in FIELDHAMSTER_CHEEKS:
            d = co - Vector(c)
            if d.x * c[0] > 0:
                # A slanted band across the pouch: distance to a plane tilted from the eye down to the jaw.
                band = abs(d.z * 0.82 - d.y * 0.57 - 0.010)
                mask_b = max(mask_b, smoothstep(0.016, 0.007, band) * smoothstep(0.082, 0.066, d.length))
        mask_b *= 1.0 - belly * 0.6

    mask_c = 0.0
    if not on_ear:
        for center, radius in ctx["spots"]:
            mask_c = max(mask_c, smoothstep(radius * 1.15, radius * 0.35, (co - Vector(center)).length))
        mask_c *= (1.0 - belly) * smoothstep(1.0, 1.15, head_s)
    return secondary, belly, mask_a, mask_b, None, mask_c


MOLE_HANDS = (((0.108, -0.130, 0.038), (0.128, -0.166, 0.034)), ((-0.108, -0.130, 0.038), (-0.128, -0.166, 0.034)))


def mole_masks(co, n, ctx):
    """T49 Mole. Secondary = mitten hands, pointed snout tip, hind feet and tail tip (pink), belly = soft underside and chin.
    Mask A = hands and nose (every variant: vivid pink on Mocha, gold on Golden Paws, pink glow on Star Nose; the nose bead
    joins through the rule's nose_mask_a). Mask B = paler muzzle and cheeks. Mask C (second UV map, uv1.x) = the back,
    soft round patches like sunlit moss (Golden Moss)."""
    hand = 0.0
    for paddle, tip in MOLE_HANDS:
        if co.x * paddle[0] <= 0:
            continue
        side = smoothstep(0.070, 0.086, abs(co.x))
        for c, r in ((paddle, (0.040, 0.070, 0.052)), (tip, (0.030, 0.036, 0.040))):
            d = co - Vector(c)
            s = math.sqrt((d.x / r[0]) ** 2 + (d.y / r[1]) ** 2 + (d.z / r[2]) ** 2)
            hand = max(hand, smoothstep(1.15, 0.90, s) * side)
    nose = ctx["nose"]
    tip = smoothstep(0.036, 0.020, (co - nose).length)
    feet = smoothstep(0.022, 0.010, co.z) * smoothstep(0.050, 0.070, co.y)
    tail = smoothstep(0.178, 0.196, co.y) * smoothstep(0.050, 0.060, co.z)
    secondary = max(hand, tip, feet, tail)

    facing = n.dot(Vector((0, -1, 0)))
    under = smoothstep(-0.25, -0.65, n.z)
    chin = smoothstep(0.20, 0.60, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.004, nose.z - 0.022, co.z)         * smoothstep(-0.10, -0.15, co.y)
    belly = max(under, chin) * (1.0 - secondary)

    mask_a = max(hand, smoothstep(0.030, 0.016, (co - nose).length))
    muzzle = smoothstep(0.080, 0.042, (co - Vector((0.0, -0.188, 0.078))).length)
    mask_b = muzzle * (1.0 - tip) * (1.0 - hand)

    mask_c = 0.0
    top = smoothstep(0.30, 0.75, n.z) * smoothstep(-0.125, -0.065, co.y) * (1.0 - smoothstep(0.150, 0.190, co.y))
    if top > 0.0:
        moss = 0.0
        for center, radius in ctx["spots"]:
            moss = max(moss, smoothstep(radius * 1.15, radius * 0.35, (co - Vector(center)).length))
        mask_c = top * (0.55 + 0.45 * moss) * (1.0 - hand)
    return secondary, belly, mask_a, mask_b, None, mask_c


def _tube_rings(species, co, ctx):
    """T51 ringed tube tails: (on the tail 0..1, in a ring 0..1) from the arc length on tail_path. The ring edges are a near
    step: the tube's loops sit in pairs around every edge (pw_species.tube_loops), so each LOD shows crisp rings."""
    path = ctx["tail_path"]
    if not path:
        return 0.0, 0.0
    import pw_species
    cfg = pw_species.SPECS[species]["tube_tail"]
    s, d, total = _path_project(co, path)
    f = s / total
    r_max = max(r for _, r in cfg["radius"])
    on = smoothstep(r_max * 1.45 + 0.004, r_max * 1.15 + 0.002, d) * smoothstep(0.08, 0.15, f)
    if ctx.get("tube"):
        # T51 polish: the tube's own vertices are always on the tail. The low tails leave the rump at 10 to 15 % of their
        # length, inside the fade above, and their underside took the belly color there.
        on = 1.0
    ring = 0.0
    for a, b in cfg["rings"]:
        ring = max(ring, smoothstep(a - 0.003, a + 0.003, f) * (1.0 - smoothstep(b - 0.003, b + 0.003, f)))
    return on, ring * on


def _face_frame(co, ctx):
    """Head-relative helpers shared by the T51 face rules: (ellipsoid distance to the head, front-facing weight)."""
    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    front = smoothstep(hc.y + 0.030, hc.y - 0.012, co.y)
    return head_s, front


RACCOON_EARS = (0.066, -0.094, 0.300)


def raccoon_masks(co, n, ctx):
    """T51 Raccoon. Secondary = inner ears and little hands and feet (cream pink), belly = white brows, muzzle, lower cheeks,
    throat and underside, detail (fifth value) = the dark face mask: a rounded patch around each eye sweeping out and down
    to the cheek, joined across the bridge (every variant shows it in its detail color, like the nose bead). Mask A and
    mask B = the tail rings and the dark tip (« anneaux (B) », « lueur sur les anneaux (A) »)."""
    on, ring = _tube_rings("Raccoon", co, ctx)
    head_s, front = _face_frame(co, ctx)
    nose = ctx["nose"]
    facing = n.dot(Vector((0, -1, 0)))
    ear_c = Vector((RACCOON_EARS[0] if co.x > 0 else -RACCOON_EARS[0], RACCOON_EARS[1], RACCOON_EARS[2]))
    on_ear = co.z > 0.274 and (co - ear_c).length < 0.044 and abs(co.x) > 0.030
    inner = smoothstep(0.10, 0.45, facing) * smoothstep(0.030, 0.018, (co - ear_c).length) if on_ear else 0.0
    paws = smoothstep(0.022, 0.010, co.z) * (1.0 - on)
    secondary = max(inner, paws)

    patch = brow = 0.0
    if head_s < 1.4 and front > 0.0 and not on_ear:
        cu, su = math.cos(math.radians(-24)), math.sin(math.radians(-24))
        for e, _ in ctx["eyes"]:
            if co.x * e.x <= 0:
                continue
            dx = abs(co.x) - abs(e.x); dz = co.z - e.z
            # Long axis out and down; inward it rises and reaches the bridge, where both patches meet: one bandit mask.
            u = dx * cu + dz * su - 0.007
            v = -dx * su + dz * cu + 0.002
            patch = max(patch, smoothstep(1.12, 0.86, math.sqrt((u / 0.049) ** 2 + (v / 0.028) ** 2)))
            brow = max(brow, smoothstep(1.15, 0.80, math.sqrt(((dx + 0.002) / 0.032) ** 2 + ((dz - 0.034) / 0.013) ** 2)))
        patch *= front
        brow *= front * (1.0 - patch)
    eye_z = ctx["eyes"][0][0].z if ctx["eyes"] else nose.z + 0.03
    muzzle = smoothstep(0.052, 0.032, (co - nose).length) * smoothstep(eye_z - 0.006, eye_z - 0.018, co.z)
    cheek = front * smoothstep(eye_z - 0.022, eye_z - 0.040, co.z) * (1.0 - smoothstep(1.15, 1.35, head_s))
    throat = smoothstep(0.30, 0.70, facing) * smoothstep(0.180, 0.150, co.z) * smoothstep(-0.07, -0.10, co.y)
    under = smoothstep(-0.25, -0.65, n.z)
    belly = max(muzzle, cheek, brow, throat, under) * (1.0 - patch) * (1.0 - on) * (1.0 - paws) * (0.0 if on_ear else 1.0)
    return secondary, belly, ring, ring, patch * (1.0 - on)


REDPANDA_EARS = ((0.060, -0.092, 0.270), (0.071, -0.090, 0.296))


def redpanda_masks(co, n, ctx):
    """T51 Red Panda. Secondary = the white face (round muzzle, a big cheek patch under each eye, a dot above each eye; the
    red tear track from the inner eye corner to the mouth stays coat color) and the ears' fronts. Belly = the dark legs,
    underside and chest. Mask A = tail rings and ear tips (glow), mask B = tail rings (« anneaux »), mask C (uv1.x) = two
    small light dots on each cheek."""
    on, ring = _tube_rings("RedPanda", co, ctx)
    head_s, front = _face_frame(co, ctx)
    nose = ctx["nose"]
    facing = n.dot(Vector((0, -1, 0)))
    sx = 1.0 if co.x > 0 else -1.0
    base = Vector((sx * REDPANDA_EARS[0][0], REDPANDA_EARS[0][1], REDPANDA_EARS[0][2]))
    tip = Vector((sx * REDPANDA_EARS[1][0], REDPANDA_EARS[1][1], REDPANDA_EARS[1][2]))
    ear_d = min((co - base).length, (co - tip).length)
    on_ear = co.z > 0.250 and ear_d < 0.042 and abs(co.x) > 0.030
    ear_front = smoothstep(-0.05, 0.35, facing) if on_ear else 0.0
    ear_tip = smoothstep(0.290, 0.306, co.z) if on_ear else 0.0

    face = dots = 0.0
    if head_s < 1.4 and not on_ear and ctx["eyes"]:
        e = ctx["eyes"][0][0]
        ex, ez = abs(e.x), e.z
        ax, az = abs(co.x), co.z
        muzzle = smoothstep(0.050, 0.033, (co - nose).length) * smoothstep(ez - 0.004, ez - 0.016, az)
        cheek = smoothstep(1.15, 0.85, math.sqrt(((ax - 0.060) / 0.032) ** 2 + ((az - (ez - 0.030)) / 0.027) ** 2))
        brow = smoothstep(1.15, 0.80, math.sqrt(((ax - ex * 0.80) / 0.013) ** 2 + ((az - (ez + 0.031)) / 0.011) ** 2))
        # Tear track: distance to the segment from below the inner eye corner to the mouth corner, in the face plane.
        p0 = Vector((ex * 0.55, ez - 0.014)); p1 = Vector((0.026, nose.z - 0.022))
        q = Vector((ax, az)); seg = p1 - p0
        t = max(0.0, min(1.0, (q - p0).dot(seg) / seg.length_squared))
        track = smoothstep(0.0090, 0.0050, (q - (p0 + seg * t)).length)
        face = max(muzzle, cheek, brow) * (1.0 - track) * front
        for cx, cz in ((0.052, ez - 0.034), (0.068, ez - 0.022)):
            dots = max(dots, smoothstep(0.0095, 0.0050, math.sqrt((ax - cx) ** 2 + (az - cz) ** 2)))
        dots *= front
    secondary = max(face, ear_front)
    legs = smoothstep(0.080, 0.054, co.z)
    under = smoothstep(-0.25, -0.65, n.z)
    chest = smoothstep(0.30, 0.70, facing) * smoothstep(0.185, 0.160, co.z) * smoothstep(-0.05, -0.08, co.y) * smoothstep(1.0, 1.15, head_s)
    belly = max(legs, under, chest) * (1.0 - on) * (1.0 - face)
    return secondary, belly, max(ring, ear_tip), ring, None, dots


def _fawn_spots():
    """Fawn spots: two rows each side of the spine and one lower row on the flanks, rump to shoulder."""
    spots = []
    for row, (x, z, r) in enumerate(((0.026, 0.402, 0.013), (0.050, 0.385, 0.012), (0.066, 0.350, 0.011))):
        for k in range(6):
            y = -0.030 + k * 0.034 + (0.017 if row % 2 else 0.0)
            for sx in (1, -1):
                spots.append(((sx * x, y, z), r))
    return spots


def fawn_masks(co, n, ctx):
    """Secondary = inner ears (rose beige), belly = underside, chest, inner legs, muzzle, chin and tail tuft,
    detail = hooves (with the nose), mask A = round spots, mask B = light fern stripes along the back."""
    axes = ctx["ears"]
    side = "L" if co.x > 0 else "R"
    base, tip = axes[side]
    p, t = intersect_point_line(co, base, tip)
    radial = (co - p).length
    on_ear = co.z > 0.50 and radial < 0.05 and -0.1 < t < 2.4 and abs(co.x) > 0.035
    facing = n.dot(Vector((0, -1, 0)))
    inner = smoothstep(0.15, 0.5, facing) * smoothstep(0.25, 0.6, t) * (1.0 - smoothstep(1.7, 2.2, t)) if on_ear else 0.0

    hooves = smoothstep(0.040, 0.030, co.z)
    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    belly = 0.0
    if not on_ear:
        under = smoothstep(-0.25, -0.65, n.z) * smoothstep(0.24, 0.27, co.z)
        # The inner side of the legs (the normal turned toward the body's middle) is lighter; the outside keeps the coat.
        toward_middle = -n.x * (1.0 if co.x > 0 else -1.0)
        inner_leg = smoothstep(0.25, 0.65, toward_middle) * (1.0 - smoothstep(0.24, 0.30, co.z)) * smoothstep(0.05, 0.08, co.z)
        nose = ctx["nose"]
        muzzle = smoothstep(0.045, 0.022, (co - nose).length)
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z + 0.002, nose.z - 0.030, co.z)
        chin *= 1.0 - smoothstep(1.05, 1.25, head_s)
        chest = smoothstep(0.35, 0.7, facing) * smoothstep(-0.02, -0.08, co.y) * (1.0 - smoothstep(0.33, 0.37, co.z)) * smoothstep(0.85, 1.05, head_s)
        tuft = smoothstep(0.195, 0.21, co.y) * smoothstep(-0.1, -0.5, n.z + 0.3 * n.y)
        belly = max(under, inner_leg, muzzle, chin, chest, tuft) * (1.0 - hooves)

    mask_a = 0.0
    if not on_ear and co.z > 0.30:
        for center, radius in ctx["spots"]:
            mask_a = max(mask_a, smoothstep(radius * 1.15, radius * 0.45, (co - Vector(center)).length))
        mask_a *= 1.0 - belly
    mask_b = 0.0
    if co.z > 0.36 and -0.06 < co.y < 0.17:
        spine = smoothstep(0.010, 0.004, abs(co.x))
        branch = abs(((co.y + abs(co.x) * 1.4) / 0.032) % 1.0 - 0.5)
        fern = smoothstep(0.16, 0.06, branch) * smoothstep(0.050, 0.030, abs(co.x)) * smoothstep(0.006, 0.012, abs(co.x))
        mask_b = max(spine, fern) * smoothstep(0.35, 0.6, n.z) * (1.0 - belly)
    return inner, belly, mask_a, mask_b, hooves


# T51 Fox and Badger (MediumQuadruped).
FOX_EAR_BASE = (0.048, -0.176, 0.376)   # same as pw_species
FOX_EAR_AXIS = (0.403, 0.139, 0.904)
FOX_EAR_LEN = 0.122

FOX_LEAVES = (((0.066, -0.012, 0.218), 0.030), ((0.068, 0.052, 0.206), 0.032), ((0.062, 0.104, 0.226), 0.026))


def fox_masks(co, n, ctx):
    """T51 Fox. Secondary = inner ears (rosy cream), belly = white cheeks, chin, chest ruff, underside and the tail tip,
    detail = dark socks and the backs of the ear tips (with the nose). Mask A = tail tip and ear tips (white tip on Red,
    the glow of Firefly, Ember, Bilberry, Golden visitor), mask B = the cross over the shoulders and the face above the
    white cheeks (Cross, Mist), mask C = stylized leaves on the flanks and the tail tip (Gold Leaf)."""
    sx = 1.0 if co.x > 0 else -1.0
    base = Vector((sx * FOX_EAR_BASE[0], FOX_EAR_BASE[1], FOX_EAR_BASE[2]))
    axis = Vector((sx * FOX_EAR_AXIS[0], FOX_EAR_AXIS[1], FOX_EAR_AXIS[2])).normalized()
    rel = co - base
    along = rel.dot(axis); te = along / FOX_EAR_LEN
    radial = (rel - axis * along).length
    on_ear = co.z > 0.360 and radial < 0.060 and -0.05 < te < 1.2 and abs(co.x) > 0.022
    facing = n.dot(Vector((0, -1, 0)))
    inner = smoothstep(0.10, 0.45, facing) * smoothstep(0.10, 0.28, te) * (1.0 - smoothstep(0.86, 0.98, te)) if on_ear else 0.0
    ear_tip = smoothstep(0.50, 0.70, te) * (1.0 - inner * 0.9) if on_ear else 0.0

    s, d, total = _path_project(co, ctx["tail_path"])
    tail = smoothstep(0.010, 0.050, s) * smoothstep(0.125, 0.150, co.y) if d < 0.11 else 0.0
    tip = smoothstep(0.66 * total, 0.84 * total, s) * tail

    socks = smoothstep(0.096, 0.064, co.z) if co.y < 0.02 else smoothstep(0.070, 0.044, co.z)
    socks *= 1.0 - tail

    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    nose = ctx["nose"]
    belly = 0.0
    if not on_ear:
        # White cheeks and upper lip below a line from the nose rising to the eye level at the ruff sides.
        line = nose.z + 0.002 + 0.38 * abs(co.x)
        cheeks = smoothstep(line + 0.008, line - 0.008, co.z) * smoothstep(-0.150, -0.185, co.y) * (1.0 - smoothstep(1.30, 1.45, head_s))
        chin = smoothstep(0.30, 0.70, max(facing, 0.8 * n.dot(Vector((0, 0, -1))))) * smoothstep(nose.z, nose.z - 0.030, co.z) * (1.0 - smoothstep(1.05, 1.25, head_s))
        ruff = smoothstep(0.25, 0.60, facing) * smoothstep(-0.070, -0.110, co.y) * (1.0 - smoothstep(0.262, 0.292, co.z)) * smoothstep(0.85, 1.05, head_s) * smoothstep(0.120, 0.150, co.z) * smoothstep(0.062, 0.040, abs(co.x))
        under = smoothstep(-0.30, -0.70, n.z) * smoothstep(0.110, 0.130, co.z) * (1.0 - tail)
        belly = max(cheeks, chin, ruff, under, tip) * (1.0 - socks)

    mask_a = max(tip, smoothstep(0.55, 0.78, te) if on_ear else 0.0)

    mask_b = 0.0
    if not on_ear and co.z > 0.150 and tail < 0.5:
        spine = smoothstep(0.024, 0.012, abs(co.x)) * smoothstep(-0.135, -0.110, co.y) * (1.0 - smoothstep(0.050, 0.075, co.y)) * smoothstep(0.30, 0.60, n.z)
        band = smoothstep(0.028, 0.014, abs(co.y + 0.062)) * smoothstep(0.160, 0.195, co.z)
        mask_b = max(spine, band) * (1.0 - belly) * (1.0 - socks)
    if not on_ear and co.y < -0.140:
        # Face: forehead, around the eyes and the top of the muzzle (above the white cheeks), fading behind the eyes.
        face = (1.0 - smoothstep(1.05, 1.25, head_s)) * smoothstep(-0.165, -0.205, co.y) * smoothstep(-0.35, 0.10, facing + 0.3 * n.z)
        mask_b = max(mask_b, face * (1.0 - belly))

    mask_c = tip
    if not on_ear and tail < 0.5 and n.x * sx > 0.2:
        lean = Vector((0.0, 0.819, 0.574))
        for c, half in FOX_LEAVES:
            cc = Vector((sx * c[0], c[1], c[2]))
            r = co - cc
            u = r.dot(lean); v = (r - lean * u); v.x = 0.0
            leaf = smoothstep(1.0, 0.62, math.sqrt((u / half) ** 2 + (v.length / (half * 0.42)) ** 2))
            mask_c = max(mask_c, leaf * (1.0 - belly))
    return inner, belly, mask_a, mask_b, max(socks, ear_tip), mask_c


BADGER_EARS = ((0.068, -0.206, 0.256), (-0.068, -0.206, 0.256))


def badger_masks(co, n, ctx):
    """T51 Badger. Secondary = white blaze from the nose over the forehead and crown, white cheeks and ear rims, belly =
    lighter underside and chest, detail = the two dark face stripes from the nose over the eyes to the ears, the dark
    paws and the nose. Mask A = paws and muzzle tip (Amber), mask B = the pale blaze, a little wider (Sage, Licorice),
    mask C = small round back spots (Sage)."""
    nose = ctx["nose"]
    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    ear_c = Vector(BADGER_EARS[0] if co.x > 0 else BADGER_EARS[1])
    on_ear = (co - ear_c).length < 0.030 and co.z > 0.238 and abs(co.x) > 0.050
    face = (1.0 - smoothstep(1.15, 1.35, head_s)) * smoothstep(-0.130, -0.170, co.y)
    # Lateral bands, narrow at the nose and wider over the eyes: blaze |x| < wc, stripe wc..ws, cheeks beyond.
    k = smoothstep(nose.y, nose.y + 0.090, co.y)   # 0 at the nose, 1 behind the eyes
    wc = 0.007 + 0.011 * k
    ws = 0.034 + 0.044 * k
    low = nose.z - 0.004 + 0.030 * k        # the stripe's lower edge rises a little toward the ears
    ax = abs(co.x)
    top = smoothstep(low - 0.006, low + 0.006, co.z)
    blaze = smoothstep(wc + 0.004, wc - 0.004, ax) * top
    stripe = smoothstep(wc - 0.003, wc + 0.004, ax) * smoothstep(ws + 0.005, ws - 0.004, ax) * top
    cheek = 1.0 - stripe - blaze
    paws = smoothstep(0.046, 0.026, co.z)
    secondary = 0.0; detail = 0.0
    if on_ear:
        secondary = smoothstep(0.016, 0.026, (co - ear_c).length)   # light ear rims, darker ear centers
        detail = smoothstep(0.020, 0.012, (co - ear_c).length)
    elif face > 0.0:
        secondary = face * max(blaze, cheek)
        detail = face * stripe
        # The white blaze runs back over the crown and fades on the neck.
    crown = smoothstep(0.020, 0.010, abs(co.x)) * smoothstep(-0.11, -0.17, co.y) * (1.0 - face) * smoothstep(0.30, 0.6, n.z) if co.z > 0.25 else 0.0
    secondary = max(secondary, crown)

    facing = n.dot(Vector((0, -1, 0)))
    under = smoothstep(-0.25, -0.65, n.z) * smoothstep(0.070, 0.095, co.z)
    chest = smoothstep(0.30, 0.65, facing) * (1.0 - smoothstep(0.15, 0.20, co.z)) * smoothstep(-0.13, -0.17, co.y) * (1.0 - face) * smoothstep(0.085, 0.11, co.z)
    belly = max(under, chest) * (1.0 - secondary) * (1.0 - detail) * (1.0 - paws)

    muzzle = smoothstep(0.036, 0.020, (co - nose).length)
    mask_a = max(paws, muzzle)
    mask_b = face * smoothstep(wc + 0.010, wc + 0.002, ax) * top if face > 0.0 else 0.0
    mask_b = max(mask_b, crown)
    mask_c = 0.0
    if co.z > 0.20 and not on_ear and face < 0.5:
        for c, r in ctx["spots"]:
            mask_c = max(mask_c, smoothstep(r * 1.15, r * 0.35, (co - Vector(c)).length))
        mask_c *= smoothstep(0.35, 0.65, n.z)
    return secondary, belly, mask_a, mask_b, max(detail, paws), mask_c


BADGER_SPOTS = [((0.0, -0.040, 0.294), 0.020), ((0.045, 0.010, 0.290), 0.018), ((-0.045, 0.045, 0.290), 0.018),
                ((0.0, 0.100, 0.296), 0.020), ((0.050, 0.140, 0.282), 0.016), ((-0.048, -0.075, 0.276), 0.016),
                ((-0.036, 0.170, 0.282), 0.016)]


RULES = {
    "Rabbit": dict(
        fn=rabbit_masks,
        head_center=(0.0, -0.085, 0.30),
        spots=[((0.06, 0.12, 0.27), 0.060), ((-0.07, 0.03, 0.25), 0.055), ((0.0, 0.17, 0.22), 0.050),
               ((0.125, 0.10, 0.15), 0.055), ((-0.125, 0.13, 0.12), 0.055), ((0.11, -0.01, 0.12), 0.045),
               ((0.075, -0.17, 0.33), 0.045)],
    ),
    "Hedgehog": dict(
        fn=hedgehog_masks,
        head_center=(0.0, -0.105, 0.165),
        head_radii=(0.078, 0.085, 0.078),
        spots=[],
    ),
    "FieldMouse": dict(
        fn=fieldmouse_masks,
        head_center=(0.0, -0.100, 0.175),
        head_radii=(0.084, 0.080, 0.076),
        # Big piebald patches (A-SP-004): back, both flanks, rump and one over the left eye and ear.
        spots=[((0.040, 0.055, 0.170), 0.070), ((-0.080, 0.020, 0.110), 0.060), ((0.0, 0.125, 0.130), 0.060),
               ((0.085, 0.095, 0.085), 0.055), ((-0.050, -0.125, 0.215), 0.045), ((-0.070, 0.110, 0.070), 0.045)],
    ),
    "GuineaPig": dict(
        fn=guineapig_masks,
        head_center=(0.0, -0.125, 0.142),
        head_radii=(0.084, 0.082, 0.077),
        # Big patches (A-SP-005): over one eye and ear, the shoulders, both flanks and the rump.
        spots=[((0.055, -0.135, 0.175), 0.060), ((-0.060, -0.040, 0.170), 0.075), ((0.085, 0.050, 0.130), 0.070),
               ((-0.095, 0.110, 0.110), 0.070), ((0.030, 0.150, 0.165), 0.065)],
    ),
    "FieldHamster": dict(
        fn=fieldhamster_masks,
        head_center=(0.0, -0.112, 0.166),
        head_radii=(0.090, 0.084, 0.084),
        # Back spots (mask C): five round spots down the spine and two on the upper flanks.
        spots=[((0.0, -0.030, 0.245), 0.040), ((0.0, 0.045, 0.255), 0.044), ((0.0, 0.115, 0.232), 0.040),
               ((0.075, 0.010, 0.222), 0.034), ((-0.075, 0.010, 0.222), 0.034), ((0.065, 0.120, 0.203), 0.030),
               ((-0.065, 0.120, 0.203), 0.030)],
    ),
    "Mole": dict(
        fn=mole_masks,
        head_center=(0.0, -0.135, 0.088),
        head_radii=(0.072, 0.082, 0.068),
        # The nose bead joins mask A with the hands (detail region kept for its color).
        nose_mask_a=1.0,
        # Back patches (mask C): six soft round moss cushions over the back.
        spots=[((0.0, -0.040, 0.180), 0.046), ((0.050, 0.025, 0.185), 0.040), ((-0.052, 0.060, 0.180), 0.042),
               ((0.0, 0.110, 0.176), 0.046), ((0.046, 0.130, 0.160), 0.036), ((-0.040, -0.015, 0.178), 0.036)],
    ),
    # T51 Forest, SmallQuadruped: rings on the tube tails, faces by head-relative shapes (no spots).
    "Raccoon": dict(
        fn=raccoon_masks,
        head_center=(0.0, -0.112, 0.222),
        head_radii=(0.100, 0.090, 0.088),
        spots=[],
    ),
    "RedPanda": dict(
        fn=redpanda_masks,
        head_center=(0.0, -0.106, 0.208),
        head_radii=(0.090, 0.080, 0.080),
        spots=[],
    ),
    "Fawn": dict(
        fn=fawn_masks,
        head_center=(0.0, -0.175, 0.468),
        head_radii=(0.074, 0.084, 0.071),
        spots=_fawn_spots(),
    ),
    "Fox": dict(
        fn=fox_masks,
        head_center=(0.0, -0.188, 0.320),
        head_radii=(0.090, 0.083, 0.080),
        spots=[],
    ),
    "Badger": dict(
        fn=badger_masks,
        head_center=(0.0, -0.230, 0.218),
        head_radii=(0.080, 0.086, 0.064),
        spots=BADGER_SPOTS,
    ),
    "Squirrel": dict(
        fn=squirrel_masks,
        head_center=(0.0, -0.080, 0.300),
        head_radii=(0.132, 0.126, 0.132),
        spots=[],
    ),
}


# T52 Boar Piglet (HeavyQuadruped). Ear base, axis and inner face (+X side), as in pw_species.
BOARPIGLET_EAR_BASE = (0.062, -0.180, 0.362)
BOARPIGLET_EAR_AXIS = (0.363, -0.559, 0.745)
BOARPIGLET_EAR_FRONT = (-0.245, -0.829, -0.502)
# Light stripes along each flank: angle around the body axis, degrees from the side up toward the spine.
BOARPIGLET_STRIPES = (62.0, 34.0, 6.0)
BOARPIGLET_SPOTS = [((0.0, -0.040, 0.320), 0.026), ((0.062, 0.005, 0.302), 0.024), ((-0.064, 0.050, 0.300), 0.024),
                    ((0.0, 0.095, 0.316), 0.026), ((0.066, 0.125, 0.284), 0.022), ((-0.060, -0.055, 0.288), 0.022),
                    ((-0.030, 0.160, 0.298), 0.022), ((0.094, 0.060, 0.244), 0.020), ((-0.098, 0.090, 0.234), 0.020)]


def boarpiglet_masks(co, n, ctx):
    """T52 Boar Piglet. Secondary = the round snout disc and the inner ears (rose beige), belly = soft underside, detail =
    the two nostril beads only (their own tag). Mask A = snout disc and curly tail (Golden Truffle glow), mask B = three
    light stripes along each flank, shoulder to rump, curving a little down toward the rump (Chestnut Stripe, Powder Pink,
    Mint Stripe), mask C = small round spots over the back (Acorn, Golden Truffle)."""
    hc = ctx["head_center"]; hr = ctx["head_radii"]
    dh = co - hc
    head_s = math.sqrt((dh.x / hr[0]) ** 2 + (dh.y / hr[1]) ** 2 + (dh.z / hr[2]) ** 2)
    disc_r = (co.x / 0.051) ** 2 + ((co.z - 0.250) / 0.045) ** 2
    disc = smoothstep(-0.30, -0.62, n.y) * smoothstep(-0.326, -0.342, co.y) * smoothstep(1.15, 0.80, disc_r)
    side = 1.0 if co.x > 0 else -1.0
    b = Vector((side * BOARPIGLET_EAR_BASE[0], BOARPIGLET_EAR_BASE[1], BOARPIGLET_EAR_BASE[2]))
    u = Vector((side * BOARPIGLET_EAR_AXIS[0], BOARPIGLET_EAR_AXIS[1], BOARPIGLET_EAR_AXIS[2])).normalized()
    t = (co - b).dot(u); radial = (co - b - u * t).length
    front = Vector((BOARPIGLET_EAR_FRONT[0] * side, BOARPIGLET_EAR_FRONT[1], BOARPIGLET_EAR_FRONT[2]))
    on_ear = co.z > 0.350 and 0.004 < t < 0.105 and radial < 0.056
    inner = 0.0
    if on_ear:
        inner = smoothstep(0.10, 0.45, n.dot(front)) * smoothstep(0.014, 0.028, t) * smoothstep(0.040, 0.026, radial)
    secondary = max(disc, inner)
    tube = ctx.get("tube", 0) == 1
    # Only the true underside: the chest and face stay the main color.
    belly = 0.0 if tube else (smoothstep(-0.45, -0.85, n.z) * smoothstep(0.030, 0.050, co.z) * smoothstep(0.150, 0.115, co.z)
                              * (1.0 - secondary))
    mask_a = 1.0 if tube else disc
    stripes = 0.0
    if not tube and head_s > 1.15 and co.z > 0.080 and not on_ear:
        phi = math.degrees(math.atan2(co.z - 0.200, max(abs(co.x), 1e-6)))
        along = smoothstep(-0.115, -0.070, co.y) * smoothstep(0.200, 0.155, co.y)
        drift = 6.0 * smoothstep(-0.08, 0.18, co.y)
        for c in BOARPIGLET_STRIPES:
            stripes = max(stripes, smoothstep(11.0, 5.0, abs(phi - (c - drift))))
        stripes *= along * (1.0 - smoothstep(-0.30, -0.65, n.z))
    mask_c = 0.0
    if not tube and co.z > 0.20 and head_s > 1.2:
        for c, r in ctx["spots"]:
            mask_c = max(mask_c, smoothstep(r * 1.15, r * 0.35, (co - Vector(c)).length))
    return secondary, belly, mask_a, stripes, 0.0, mask_c


RULES["BoarPiglet"] = dict(fn=boarpiglet_masks, head_center=(0.0, -0.195, 0.280), head_radii=(0.118, 0.105, 0.110),
                           spots=BOARPIGLET_SPOTS)


# ---------------------------------------------------------------- T52 Owl (Bird family)
# T52 polish: the body is pw_species' analytic surface (SPECS["Owl"]["surface"]); the rule reads the same wing, tail and
# toe data through pw_species (wing_coords, tail_coords, toe_shapes). Beak: (centre, half sizes, long axis), +X side.
OWL_BEAK = ((0, -0.135, 0.271), (0.010, 0.013, 0.011), (0, -0.42, -0.91))
# Crisp mask shapes, cut into the mesh by pw_species.cut_mask_shapes (LOD0 and LOD1) so the vertex masks switch within a
# thin ring: (point on the surface, outward normal, turn in degrees). Constellation stars (mask D): round five-point
# stars only (straight sides, rounded tips), radius = tip radius, inner = valley ratio, edge = ring width. Two sit on the
# wing plates (well inside their edge), the others on the back and the back of the head, away from the neck band.
OWL_STARS = dict(radius=0.021, inner=0.50, edge=0.0035, items=[
    ((0.0804, 0.1226, 0.3326), (0.435, 0.882, 0.179), 12), ((-0.0893, 0.1156, 0.3418), (-0.496, 0.839, 0.221), -14),
    ((0.0201, 0.1443, 0.2804), (0.083, 0.995, 0.046), 20), ((-0.0334, 0.1437, 0.2133), (-0.144, 0.990, -0.004), -6),
    ((0.1726, 0.0303, 0.1865), (0.992, 0.112, 0.059), 8), ((0.0784, 0.1283, 0.1616), (0.412, 0.905, -0.103), -18),
    ((-0.1653, 0.0527, 0.1439), (-0.937, 0.313, -0.156), 16), ((-0.1357, 0.0609, 0.3557), (-0.851, 0.428, 0.303), -10)])
# Facial discs (secondary) and their raised rims (mask A): frames on the base egg, turned a little to the front. radii:
# rim inner edge, disc edge; segs per LOD (LOD1 keeps the disc edge only). pw_species.surface_mesh sculpts the dish and
# the rim on these frames (SPECS["Owl"]["surface"]["disc_profile"]).
OWL_DISCS = dict(radii=(0.044, 0.056), edge=0.0035, segs=(20, 12), items=[
    ((0.064, -0.1215, 0.318), (0.197, -0.974, 0.114), 0), ((-0.064, -0.1215, 0.318), (-0.197, -0.974, 0.114), 0)])
# Belly (oval on the round front: x half, z centre, z half) and its specks (mask C): small round dots in rows, cut like
# the discs (radius, edge ring, segments per LOD), frames on the belly surface. LOD2 (no cut) gets a faint even tint.
OWL_BELLY = (0.100, 0.150, 0.112)
OWL_DOTS = dict(radius=0.0075, edge=0.0025, segs=(8, 6), items=[
    ((-0.034, -0.1555, 0.172), (-0.165, -0.986, -0.017), 0), ((0.034, -0.1555, 0.172), (0.165, -0.986, -0.017), 0),
    ((-0.060, -0.1442, 0.136), (-0.330, -0.923, -0.197), 0), ((0.0, -0.1537, 0.136), (0.0, -0.980, -0.197), 0),
    ((0.060, -0.1442, 0.136), (0.330, -0.923, -0.197), 0), ((-0.034, -0.1406, 0.100), (-0.177, -0.917, -0.360), 0),
    ((0.034, -0.1406, 0.100), (0.177, -0.917, -0.360), 0)])


def star_outline(R, inner, tip=3):
    """A round five-point star, counter-clockwise, first tip up (+y): straight sides, rounded tips (tip=3: two shoulder
    points and a lowered apex; tip=1: the lowered apex only, inside the tip=3 outline)."""
    pts = []
    for i in range(5):
        a = math.pi / 2 + i * 2 * math.pi / 5
        t = Vector((R * math.cos(a), R * math.sin(a)))
        vp = Vector((inner * R * math.cos(a - math.pi / 5), inner * R * math.sin(a - math.pi / 5)))
        vn = Vector((inner * R * math.cos(a + math.pi / 5), inner * R * math.sin(a + math.pi / 5)))
        if tip == 1:
            pts.append(t * 0.94)
        else:
            pts += [t + (vp - t) * 0.20, t * 0.955, t + (vn - t) * 0.20]
        pts.append(vn)
    return pts


def circle_outline(R, segs):
    return [Vector((R * math.cos(2 * math.pi * i / segs), R * math.sin(2 * math.pi * i / segs))) for i in range(segs)]


def rot2(p, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return Vector((p.x * c - p.y * s, p.x * s + p.y * c))


def poly_sdf(p, pts):
    """Signed distance from p to a closed 2D outline (negative inside)."""
    d = 1e9; inside = False
    for i in range(len(pts)):
        a = pts[i]; b = pts[(i + 1) % len(pts)]
        e = b - a; t = max(0.0, min(1.0, (p - a).dot(e) / e.dot(e)))
        d = min(d, (p - (a + e * t)).length)
        if (a.y > p.y) != (b.y > p.y) and p.x < a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y):
            inside = not inside
    return -d if inside else d


def shape_frame(c, n):
    """(center, normal, t1, t2) of a mask shape: t1 is up (or forward on a top face), t2 = n x t1."""
    c = Vector(c); n = Vector(n).normalized()
    ref = Vector((0, 0, 1)) if abs(n.z) < 0.85 else Vector((0, -1, 0))
    t1 = (ref - n * ref.dot(n)).normalized()
    return c, n, t1, n.cross(t1)


def _shape_plane(co, item, reach):
    c, n, t1, t2 = shape_frame(item[0], item[1])
    v = co - c
    if abs(v.dot(n)) > 0.025:
        return None
    p = Vector((v.dot(t1), v.dot(t2)))
    return p if p.length < reach else None


def star_mask(co, stars):
    R = stars["radius"]; edge = stars["edge"]
    ref = star_outline(R, stars["inner"], 3)
    best = 0.0
    for item in stars["items"]:
        p = _shape_plane(co, item, R * 1.4)
        if p is not None:
            best = max(best, smoothstep(0.6 * edge, 0.15 * edge, poly_sdf(rot2(p, -item[2]), ref)))
    return best


def disc_masks(co, discs):
    """(disc, rim): 1 inside the disc edge; the rim ring from the rim inner edge to the disc edge."""
    r1, r2 = discs["radii"]; e = discs["edge"]
    disc = rim = 0.0
    for item in discs["items"]:
        p = _shape_plane(co, item, r2 * 1.5)
        if p is not None:
            r = p.length
            d = smoothstep(r2 + 0.6 * e, r2 + 0.15 * e, r)
            disc = max(disc, d); rim = max(rim, d * smoothstep(r1 + 0.15 * e, r1 + 0.6 * e, r))
    return disc, rim


def mask_shapes(species, level):
    """The shapes pw_species.cut_mask_shapes cuts on an LOD: (frame item, outlines from the inside out, same point count)."""
    rule = RULES[species]; out = []
    S = rule.get("stars")
    if S:
        R = S["radius"]; k = S["inner"]; e = S["edge"]; tip = 3 if level == 0 else 1
        for item in S["items"]:
            out.append((item, [[rot2(p, item[2]) for p in star_outline(R, k, tip)],
                               [rot2(p, item[2]) for p in star_outline(R + e, (k * R + e) / (R + e), tip)]]))
    D = rule.get("discs")
    if D:
        r1, r2 = D["radii"]; e = D["edge"]; segs = D["segs"][level]
        for item in D["items"]:
            out.append((item, [circle_outline(r, segs) for r in ((r1, r1 + e, r2, r2 + e) if level == 0 else (r2, r2 + e))]))
    # T52 polish: round dots (the Owl's belly specks), so a mask dot never reads as a diamond around a single vertex; LOD1
    # keeps the dot outline only (a soft edge over the bridge).
    P = rule.get("dots")
    if P:
        r = P["radius"]; e = P["edge"]; segs = P["segs"][level]
        for item in P["items"]:
            out.append((item, [circle_outline(x, segs) for x in ((r, r + e) if level == 0 else (r,))]))
    return out


def dot_mask(co, dots):
    """1 inside a round dot, 0 past its edge ring (dots: radius, edge, items as the discs)."""
    r = dots["radius"]; e = dots["edge"]; best = 0.0
    for item in dots["items"]:
        p = _shape_plane(co, item, r * 2.0)
        if p is not None:
            best = max(best, smoothstep(r + 0.6 * e, r + 0.15 * e, p.length))
    return best


def _ell_s(co, center, half, rot=None, axis=None, mirror_x=False):
    from mathutils import Euler
    c = Vector(center)
    if mirror_x:
        c.x = -c.x
        rot = (rot[0], -rot[1], -rot[2]) if rot else None
        axis = (-axis[0], axis[1], axis[2]) if axis else None
    q = Vector(axis).to_track_quat('Y', 'Z') if axis else Euler([math.radians(a) for a in rot], 'XYZ').to_quaternion()
    d = q.inverted() @ (co - c)
    return math.sqrt((d.x / half[0]) ** 2 + (d.y / half[1]) ** 2 + (d.z / half[2]) ** 2)


def owl_masks(co, n, ctx):
    """T52 Owl. Secondary = the two round facial discs, belly = the round cream front, detail = beak and toes. Mask A = wing
    tips, the lower edge of the tail fan and the disc rims (the glow area: Honey Wings tips, Full Moon discs), mask B = two
    light bands across each wing (Honey Wings), mask C = soft belly specks (Periwinkle), mask D = the Constellation's round
    five-point stars. Discs and stars are cut into LOD0 and LOD1, so their edges are crisp."""
    import pw_species
    S = pw_species.SPECS["Owl"]["surface"]
    facing = n.dot(Vector((0, -1, 0)))
    disc, rim = disc_masks(co, OWL_DISCS)
    beak = 1.0 - smoothstep(1.08, 1.32, _ell_s(co, OWL_BEAK[0], OWL_BEAK[1], axis=OWL_BEAK[2]))
    toe = min(_ell_s(co, c, h, (0, 0, math.degrees(a))) for c, h, a in pw_species.toe_shapes("Owl", heel=False))
    foot = (1.0 - smoothstep(1.05, 1.30, toe)) * smoothstep(0.034, 0.020, co.z)
    detail = max(beak, foot)
    # Wings: the raised plates (pw_species.surface_mesh), up to the middle of their soft edge.
    ws, wt = (float(x) for x in pw_species.wing_coords(S["wing"], co.y, co.z))
    wing = (1.0 - smoothstep(0.84, 0.92, ws)) * smoothstep(0.30, 0.60, abs(n.x))
    e = math.sqrt((co.x / OWL_BELLY[0]) ** 2 + ((co.z - OWL_BELLY[1]) / OWL_BELLY[2]) ** 2)
    belly = (1.0 - smoothstep(0.74, 1.12, e)) * smoothstep(0.05, 0.40, facing) * (1.0 - wing) * (1.0 - disc) * (1.0 - detail)
    T = S["tail"]
    ts, _ = pw_species.tail_coords(T, co.x, co.z)
    tail_tip = (1.0 - smoothstep(0.80, 0.92, float(ts))) * smoothstep(0.2, 0.5, n.y) * smoothstep(T["center"][1], T["center"][1] - 0.6 * T["half"][1], co.z)
    wing_tip = wing * smoothstep(0.62, 0.88, wt)
    mask_a = max(wing_tip, tail_tip, rim)
    mask_b = 0.0
    if wing > 0.0:
        for tb in (0.30, 0.50):
            mask_b = max(mask_b, 1.0 - smoothstep(0.022, 0.040, abs(wt - tb)))
        mask_b *= wing * (1.0 - wing_tip)
    mask_c = dot_mask(co, OWL_DOTS) * (1.0 - detail) if ctx.get("lod", 0) < 2 else 0.22 * belly
    mask_d = star_mask(co, OWL_STARS) * (1.0 - detail)
    return disc * (1.0 - detail), belly, mask_a, mask_b, detail, mask_c, mask_d


RULES["Owl"] = dict(fn=owl_masks, head_center=(0.0, -0.005, 0.335), head_radii=(0.154, 0.135, 0.105), spots=[],
                    stars=OWL_STARS, discs=OWL_DISCS, dots=OWL_DOTS)


def _lobes(species):
    try:
        import pw_species
    except ImportError:
        return []
    if pw_species.SPECS.get(species, {}).get("lobe_mode") != "mesh":
        return []
    return pw_species.lobe_frames(species)


def _tail_path(species):
    try:
        import pw_species
    except ImportError:
        return []
    return pw_species.SPECS.get(species, {}).get("tail_path", [])


def apply(species, armature_name=None, nose_object="WORK_Nose"):
    rule = RULES[species]
    if armature_name is None:
        try:
            import pw_species
            armature_name = pw_species.SPECS.get(species, {}).get("armature", "SK_SmallQuadruped")
        except ImportError:
            armature_name = "SK_SmallQuadruped"
    armature = bpy.data.objects[armature_name]
    ctx = {
        "ears": _ear_axes(armature) if "Ear_L_01" in armature.data.bones else {},  # T52: the Bird has no ear bones
        "head_center": Vector(rule["head_center"]),
        "nose": bpy.data.objects[nose_object].location.copy(),
        "spots": rule["spots"],
        "head_radii": rule.get("head_radii", (0.150, 0.135, 0.140)),
        "eyes": [(bpy.data.objects[n].location.copy(), bpy.data.objects[n].get("pw_radius", 0.023))
                 for n in ("WORK_Eye_L", "WORK_Eye_R") if n in bpy.data.objects],
        "tail_path": _tail_path(species),
        "lobes": _lobes(species),
    }
    report = []
    for lod in range(3):
        obj = bpy.data.objects.get(f"SKM_{species}_LOD{lod}")
        if obj is None:
            continue
        ctx["lod"] = lod  # T52 polish: a rule may soften a detail the far LOD cannot carry
        me = obj.data
        regions = me.color_attributes["PW_Regions"]
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        uv = me.uv_layers.new(name="PW_Masks")
        part_attr = me.attributes.get("PW_Part")
        tube_attr = me.attributes.get("PW_TailTube")
        per_vertex = []
        per_vertex_c = []
        for v in me.vertices:
            ctx["part"] = part_attr.data[v.index].value if part_attr is not None else 0
            ctx["tube"] = tube_attr.data[v.index].value if tube_attr is not None else 0
            r, g, b, a = regions.data[v.index].color
            if a > 0.5 or b > 0.5:
                # Eyes and nose keep their own tags and no pattern (T49: a rule may put the nose bead on mask A).
                per_vertex.append((rule.get("nose_mask_a", 0.0) if a <= 0.5 else 0.0, 0.0))
                per_vertex_c.append((0.0, 0.0))
                continue
            out = rule["fn"](v.co, v.normal, ctx)
            sec, belly, ma, mb = out[:4]
            # A fifth value paints the detail region too (Fawn hooves share the nose's detail color); None keeps it.
            detail = max(b, out[4]) if len(out) > 4 and out[4] is not None else b
            regions.data[v.index].color = (sec, belly, detail, a)
            per_vertex.append((ma, mb))
            # T52: a seventh value is mask D (uv1.y): the Owl's Constellation stars.
            per_vertex_c.append((out[5] if len(out) > 5 else 0.0, out[6] if len(out) > 6 else 0.0))
        for loop in me.loops:
            uv.data[loop.index].uv = per_vertex[loop.vertex_index]
        if any(c[0] > 0.0 or c[1] > 0.0 for c in per_vertex_c):
            uv2 = me.uv_layers.new(name="PW_Masks2")
            for loop in me.loops:
                uv2.data[loop.index].uv = per_vertex_c[loop.vertex_index]
        report.append((obj.name, len(me.vertices)))
    return report
