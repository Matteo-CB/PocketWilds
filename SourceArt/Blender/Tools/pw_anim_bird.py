"""Bird animation library (T52, RIG-004). Source of the AN_Bird_* actions (BL_Rig_Bird.blend).

Rotations only (degrees, bone-local XYZ), the Root lift (« lz », the Root bone points up so it rides location Y), a Root lean (Root X: the whole round bird tips
forward with a lift, Eat and Social) and the
Head scale (« sx », « sy », « sz »: the owl fluffs its feathers; AssetImportRules keeps a Head scale that leaves 1, as for
the Field Hamster). Bone axes (pw_rig.BIRD): positive X tips Body forward (a bow), nods Neck and Head down, swings the
wings back, the legs forward, lifts the toes (Foot) and the tail; Head Y turns the head (positive: to the bird's left),
Head Z tilts it; Wing Z flaps (Wing_L out with negative values, Wing_R out with positive ones: both() mirrors with -1).
The legs hang from Root: a bow never moves the feet, and every clip keeps them on the ground or lifts them with Root.

Usage inside Blender, with BL_Rig_Bird.blend open:
    import pw_anim_bird; pw_anim_bird.build_all()
    import pw_export; pw_export.export_animation_library("Bird")
Clips (the family states of AnimalPipeline): Idle_A, Idle_B (the owl's slow cute head turn), Locomotion_Hop (a ground
hop, in place), Happy (HappyWing: a little wing flutter), ReactTouch (a soft fluff of the feathers), Sleep_Enter, Sleep_Loop,
Sleep_Exit, Eat_Loop (pecks the ground), Social_Nose (a small bow), Inspect (a head tilt). 30 fps, no flight or perch yet.
"""
import math
import bpy

FAMILY = "Bird"
ROT = 'pose.bones["{}"].rotation_euler'
LOC = 'pose.bones["{}"].location'
SCALE = 'pose.bones["{}"].scale'


def arm():
    return bpy.data.objects["SK_" + FAMILY]


def both(fmt, channel, keys, mirror=1):
    return {(fmt.format("L"), channel): keys, (fmt.format("R"), channel): [(f, v * mirror) for f, v in keys]}


def wings_out(keys):
    """Both wings flap out by the given (negative = out on the left) amounts."""
    return both("Wing_{}_Upper", "rz", keys, -1)


def _fcurves(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out.extend(bag.fcurves)
        return out


def build(name, frames, loop, tracks):
    a = arm()
    act = bpy.data.actions.get(name)
    if act:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, frames
    act.use_cyclic = loop
    a.animation_data_create()
    a.animation_data.action = act
    still = [(0, 0.0), (frames, 0.0)]
    for pb in a.pose.bones:
        pb.rotation_mode = 'XYZ'
        for i, ch in enumerate(("rx", "ry", "rz")):
            fc = act.fcurve_ensure_for_datablock(a, ROT.format(pb.name), index=i, group_name=pb.name)
            for f, v in tracks.get((pb.name, ch), still):
                fc.keyframe_points.insert(f, math.radians(v), options={'FAST'})
    lift = tracks.get(("Root", "lz"), still)
    for i in range(3):
        fc = act.fcurve_ensure_for_datablock(a, LOC.format("Root"), index=i, group_name="Root")
        for f, v in (lift if i == 1 else still):
            fc.keyframe_points.insert(f, v, options={'FAST'})
    # The Head scale is keyed in every clip (1 when unused), so no clip inherits another's fluff when baked.
    for i, ch in enumerate(("sx", "sy", "sz")):
        fc = act.fcurve_ensure_for_datablock(a, SCALE.format("Head"), index=i, group_name="Head")
        for f, v in tracks.get(("Head", ch), [(0, 1.0), (frames, 1.0)]):
            fc.keyframe_points.insert(f, v, options={'FAST'})
    for fc in _fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
            kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
        if loop:
            fc.modifiers.new('CYCLES')
        fc.update()
    return act


def hold(pose, frames):
    return {k: [(0, v), (frames, v)] for k, v in pose.items()}


def blend_keys(a, b, frames, ease_at):
    """From pose a at 0 to pose b at ease_at, held to frames (missing channels are 0, a missing scale 1)."""
    rest = lambda k: 1.0 if k[1].startswith("s") else 0.0
    keys = set(a) | set(b)
    return {k: [(0, a.get(k, rest(k))), (ease_at, b.get(k, rest(k))), (frames, b.get(k, rest(k)))] for k in keys}


def fluff(keys):
    """Head scale keys (s) as a feather fluff: wider and deeper, a little less tall."""
    return {("Head", "sx"): keys, ("Head", "sz"): keys, ("Head", "sy"): [(f, 1.0 + (v - 1.0) * 0.5) for f, v in keys]}


# A whole-body lean (Root X, degrees) lifts Root by this much per degree, so the round belly never dips below the ground.
LEAN_LIFT = 0.00136

# Asleep: the head sinks forward and tilts, the feathers fluff a little, the wings relax, the tail droops.
SLEEP = {
    ("Body", "rx"): 2, ("Neck", "rx"): 2, ("Head", "rx"): 6, ("Head", "rz"): 12, ("Head", "ry"): 4,
    ("Wing_L_Upper", "rx"): 3, ("Wing_R_Upper", "rx"): 3, ("Tail_01", "rx"): -4,
    ("Head", "sx"): 1.03, ("Head", "sy"): 1.015, ("Head", "sz"): 1.03,
}


def clips():
    c = {}
    n = FAMILY
    # ---------------- Idle_A: breathing, a blink-like head dip, a glance, a tail flick. 3.2 s loop.
    t = {("Body", "rx"): [(0, 0), (48, 1.2), (96, 0)],
         ("Head", "rx"): [(0, 0), (58, 0), (62, 5), (68, 1), (72, 0), (96, 0)],
         ("Head", "rz"): [(0, 0), (30, 3), (70, -2), (96, 0)],
         ("Head", "ry"): [(0, 0), (20, 0), (30, 6), (56, 6), (66, 0), (96, 0)],
         ("Tail_01", "rx"): [(0, 0), (80, 0), (83, 10), (88, 0), (96, 0)]}
    t.update(both("Wing_{}_Upper", "rx", [(0, 0), (48, 1.5), (96, 0)]))
    c[f"AN_{n}_Idle_A"] = (96, True, t)
    # ---------------- Idle_B (signature « tête qui tourne »): turns the head slowly to one side, then the other, in a
    # limited cute range (32 degrees, shared by Body, Neck and Head so the soft neck never folds), tilting it with the
    # turn. 4 s one-shot.
    t = {("Body", "ry"): [(0, 0), (14, 0), (30, 6), (52, 6), (68, -6), (96, -6), (112, 0), (120, 0)],
         ("Neck", "ry"): [(0, 0), (14, 0), (30, 12), (52, 12), (68, -12), (96, -12), (112, 0), (120, 0)],
         ("Head", "ry"): [(0, 0), (14, 0), (30, 14), (52, 14), (68, -14), (96, -14), (112, 0), (120, 0)],
         ("Head", "rz"): [(0, 0), (14, 0), (30, 8), (52, 8), (68, -8), (96, -8), (112, 0), (120, 0)],
         ("Head", "rx"): [(0, 0), (14, 0), (30, -3), (96, -3), (112, 0), (120, 0)]}
    c[f"AN_{n}_Idle_B"] = (120, False, t)
    # ---------------- Locomotion_Hop: a ground hop, in place (the simulation moves the bird). Crouch, push, 4 cm up with
    # the toes hanging and the wings a little open, land, settle. 0.6 s loop.
    t = {("Root", "lz"): [(0, 0), (3, 0), (6, 0.024), (9, 0.040), (12, 0.026), (15, 0), (18, 0)],
         ("Body", "rx"): [(0, 3), (3, 5), (6, -2), (9, -3), (12, 0), (15, 5), (18, 3)],
         ("Head", "rx"): [(0, -2), (3, -3), (9, 2), (15, -3), (18, -2)],
         ("Tail_01", "rx"): [(0, 2), (6, 8), (10, -4), (15, 4), (18, 2)]}
    t.update(both("Leg_{}", "rx", [(0, 0), (4, 0), (8, -14), (12, -8), (15, 0), (18, 0)]))
    t.update(both("Foot_{}", "rx", [(0, 0), (5, 0), (8, -18), (12, -10), (14, 0), (18, 0)]))
    t.update(wings_out([(0, 0), (5, 0), (9, -10), (13, 0), (18, 0)]))
    c[f"AN_{n}_Locomotion_Hop"] = (18, True, t)
    # ---------------- Happy (HappyWing): a little wing flutter with two small bounces and a head wobble. 1 s one-shot.
    t = {("Root", "lz"): [(0, 0), (4, 0), (8, 0.022), (12, 0.004), (16, 0.018), (20, 0), (30, 0)],
         ("Head", "rz"): [(0, 0), (6, 10), (14, -10), (22, 8), (30, 0)],
         ("Body", "rx"): [(0, 0), (4, 3), (8, -3), (14, 2), (20, -1), (30, 0)],
         ("Tail_01", "rx"): [(0, 0), (5, 14), (10, 4), (15, 14), (22, 0), (30, 0)]}
    t.update(wings_out([(0, 0), (3, -28), (6, -6), (9, -30), (12, -6), (15, -30), (18, -6), (21, -24), (25, -4), (30, 0)]))
    t.update(both("Wing_{}_Lower", "rz", [(0, 0), (4, -8), (7, 0), (10, -8), (13, 0), (16, -8), (19, 0), (30, 0)], -1))
    c[f"AN_{n}_Happy"] = (30, False, t)
    # ---------------- ReactTouch: a soft fluff of the feathers (the head and shoulders puff up), a little shiver, the
    # wings open a touch. 0.8 s one-shot.
    t = {("Body", "ry"): [(0, 0), (2, 4), (4, -4), (6, 3), (8, -3), (10, 2), (12, 0), (24, 0)],
         ("Head", "rx"): [(0, 0), (3, -6), (12, -2), (24, 0)],
         ("Root", "lz"): [(0, 0), (2, 0.008), (6, 0.002), (12, 0), (24, 0)]}
    t.update(fluff([(0, 1.0), (4, 1.08), (12, 1.05), (24, 1.0)]))
    t.update(wings_out([(0, 0), (3, -12), (10, -6), (18, 0), (24, 0)]))
    c[f"AN_{n}_ReactTouch"] = (24, False, t)
    # ---------------- Sleep: dozes off (enter 1 s), breathes slowly with the head sunk (4 s), wakes (0.8 s).
    c[f"AN_{n}_Sleep_Enter"] = (30, False, blend_keys({}, SLEEP, 30, 24))
    t = hold(SLEEP, 120)
    t[("Body", "rx")] = [(0, 2), (60, 3.2), (120, 2)]
    for ch, base in (("sx", 1.03), ("sz", 1.03)):
        t[("Head", ch)] = [(0, base), (60, base + 0.012), (120, base)]
    t[("Tail_01", "rx")] = [(0, -4), (80, -4), (84, 2), (90, -4), (120, -4)]
    c[f"AN_{n}_Sleep_Loop"] = (120, True, t)
    t = blend_keys(SLEEP, {}, 24, 18)
    t[("Head", "rz")] = [(0, 12), (12, -4), (18, 3), (24, 0)]
    c[f"AN_{n}_Sleep_Exit"] = (24, False, t)
    # ---------------- Eat_Loop: the round bird pecks with its whole body (the head merged in never folds into the belly):
    # Root leans 14 degrees and bobs to 24 three times, rising with the lean so the belly stays above the ground; the legs
    # swing forward by the same angle, so the feet stay flat. 2 s loop.
    lean = [(0, 14)]
    for start in (0, 20, 40):
        lean += [(start + 6, 24), (start + 10, 14), (start + 20, 14)]
    t = {("Root", "rx"): lean, ("Root", "lz"): [(f, LEAN_LIFT * v) for f, v in lean],
         ("Body", "rx"): [(0, 3), (60, 3)], ("Head", "rx"): [(0, 4), (60, 4)],
         ("Tail_01", "rx"): [(0, 4), (60, 4)]}
    t.update(both("Leg_{}", "rx", lean))
    c[f"AN_{n}_Eat_Loop"] = (60, True, t)
    # ---------------- Social_Nose: a small bow, the head tilted, the wings a touch open. 1.3 s one-shot.
    t = {("Root", "rx"): [(0, 0), (12, 10), (26, 10), (39, 0)],
         ("Root", "lz"): [(0, 0), (12, LEAN_LIFT * 10), (26, LEAN_LIFT * 10), (39, 0)],
         ("Body", "rx"): [(0, 0), (12, 3), (26, 3), (39, 0)], ("Head", "rx"): [(0, 0), (12, 8), (26, 8), (39, 0)],
         ("Head", "rz"): [(0, 0), (14, 8), (26, 8), (39, 0)], ("Tail_01", "rx"): [(0, 0), (12, 6), (26, 6), (39, 0)]}
    t.update(wings_out([(0, 0), (12, -6), (26, -6), (39, 0)]))
    t.update(both("Leg_{}", "rx", [(0, 0), (12, 10), (26, 10), (39, 0)]))
    c[f"AN_{n}_Social_Nose"] = (39, False, t)
    # ---------------- Inspect: a curious head tilt to one side, then the other. 1.5 s one-shot.
    t = {("Head", "rz"): [(0, 0), (10, 13), (24, 13), (30, -10), (38, -10), (45, 0)],
         ("Head", "rx"): [(0, 0), (10, 8), (38, 8), (45, 0)], ("Neck", "rx"): [(0, 0), (10, 6), (38, 6), (45, 0)],
         ("Head", "ry"): [(0, 0), (10, 6), (24, 6), (30, -6), (38, -6), (45, 0)]}
    c[f"AN_{n}_Inspect"] = (45, False, t)
    return c


def build_all():
    made = []
    for name, (frames, loop, tracks) in clips().items():
        build(name, frames, loop, tracks)
        made.append(name)
    a = arm()
    a.animation_data.action = None
    for pb in a.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    return made
