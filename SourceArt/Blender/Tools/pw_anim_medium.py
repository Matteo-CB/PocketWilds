"""MediumQuadruped animation library (T46). Source of the AN_MediumQuadruped_* actions (BL_Rig_MediumQuadruped.blend).

Rotations only (degrees, bone-local XYZ) plus the Root lift (« lz », the Root bone points up so it rides location Y),
as in pw_anim_library (SmallQuadruped). Bone axes follow SmallQuadruped (pw_rig): on the legs, Pelvis, Spine and
Chest a positive X turn swings the bone forward or lifts the front; on Neck and Head it nods down; on the hock
(HindLeg_*_Lower) it folds the lower leg back and up.

Usage inside Blender, with BL_Rig_MediumQuadruped.blend open:
    import pw_anim_medium; pw_anim_medium.build_all()
    import pw_export; pw_export.export_animation_library("MediumQuadruped")
Clips (doc RIGS, RIG-002): Idle_A/B, Locomotion_Walk, Locomotion_Trot, Happy, ReactTouch, Sleep_Enter/Loop/Exit,
Eat_Loop, Social_Nose, Inspect; T49 PeekOut (photo pose « Sortir du terrier »). In place, 30 fps, no bone the family lacks.
"""
import math
import bpy

FAMILY = "MediumQuadruped"
ROT = 'pose.bones["{}"].rotation_euler'
LOC = 'pose.bones["{}"].location'
LEGS = ("FrontLeg_L", "FrontLeg_R", "HindLeg_L", "HindLeg_R")


def arm():
    return bpy.data.objects["SK_" + FAMILY]


def both(fmt, channel, keys, mirror=1):
    return {(fmt.format("L"), channel): keys, (fmt.format("R"), channel): [(f, v * mirror) for f, v in keys]}


def wave(frames, phase, amp, step=4, harmonic=1, offset=0.0):
    """Keys of a cosine over one loop (frames), shifted by phase frames, sampled every step frames."""
    keys = []
    for f in range(0, frames + 1, step):
        keys.append((f, offset + amp * math.cos(2 * math.pi * harmonic * (f - phase) / frames)))
    return keys


def pulse(frames, phase, peak_at, width, amp, step=2):
    """Zero, a smooth bump of amp centred peak_at frames after phase (loop-wrapped), width frames wide."""
    keys = []
    for f in range(0, frames + 1, step):
        d = ((f - phase - peak_at + frames / 2) % frames) - frames / 2
        x = max(0.0, 1.0 - abs(d) / (width / 2))
        keys.append((f, amp * (0.5 - 0.5 * math.cos(math.pi * x))))
    return keys


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
    for pb in a.pose.bones:
        pb.rotation_mode = 'XYZ'
    for pb in a.pose.bones:
        for i, ch in enumerate(("rx", "ry", "rz")):
            keys = tracks.get((pb.name, ch), [(0, 0.0), (frames, 0.0)])
            fc = act.fcurve_ensure_for_datablock(a, ROT.format(pb.name), index=i, group_name=pb.name)
            for f, v in keys:
                fc.keyframe_points.insert(f, math.radians(v), options={'FAST'})
    lift = tracks.get(("Root", "lz"), [(0, 0.0), (frames, 0.0)])
    for i in range(3):
        fc = act.fcurve_ensure_for_datablock(a, LOC.format("Root"), index=i, group_name="Root")
        for f, v in (lift if i == 1 else [(0, 0.0), (frames, 0.0)]):
            fc.keyframe_points.insert(f, v, options={'FAST'})
    for fc in _fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
            kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
        if loop:
            fc.modifiers.new('CYCLES')
        fc.update()
    return act


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


def hold(pose, frames):
    return {k: [(0, v), (frames, v)] for k, v in pose.items()}


def blend_keys(a, b, frames, ease_at):
    """From pose a at 0 to pose b at ease_at, held to frames (missing channels are 0)."""
    keys = set(a) | set(b)
    return {k: [(0, a.get(k, 0.0)), (ease_at, b.get(k, 0.0)), (frames, b.get(k, 0.0))] for k in keys}


# Lying down, legs folded under the body, head resting turned to one side (the belly 3 cm above the ground).
SLEEP = {
    # Measured on the Fawn: the hind hooves touch the ground, the front cannons tuck under the chest (knees forward).
    ("Root", "lz"): -0.185,
    ("Pelvis", "rx"): 3, ("Chest", "rx"): -4,
    ("Neck", "rx"): 20, ("Neck", "ry"): 24, ("Head", "rx"): -16, ("Head", "rz"): 10,
    ("FrontLeg_L_Upper", "rx"): 50, ("FrontLeg_R_Upper", "rx"): 50,
    ("FrontLeg_L_Lower", "rx"): -150, ("FrontLeg_R_Lower", "rx"): -150,
    ("FrontPaw_L", "rx"): -20, ("FrontPaw_R", "rx"): -20,
    ("HindLeg_L_Upper", "rx"): 70, ("HindLeg_R_Upper", "rx"): 70,
    ("HindLeg_L_Lower", "rx"): 120, ("HindLeg_R_Lower", "rx"): 120,
    ("HindPaw_L", "rx"): 20, ("HindPaw_R", "rx"): 20,
    ("Ear_L_01", "rx"): 25, ("Ear_R_01", "rx"): 25,
    ("Tail_01", "rx"): 10, ("Tail_02", "rx"): 8,
}


def walk(frames, amp_front, amp_hind, lift_front, lift_hind, bob, phases):
    t = {}
    for leg, p in zip(LEGS, phases):
        front = leg.startswith("Front")
        t[(leg + "_Upper", "rx")] = wave(frames, p, amp_front if front else amp_hind)
        if front:
            t[(leg + "_Lower", "rx")] = pulse(frames, p, frames * 0.68, frames * 0.38, -lift_front)
            t[(leg.replace("Leg", "Paw"), "rx")] = pulse(frames, p, frames * 0.62, frames * 0.3, -lift_front * 0.5)
        else:
            t[(leg + "_Lower", "rx")] = pulse(frames, p, frames * 0.68, frames * 0.38, lift_hind)
            t[(leg.replace("Leg", "Paw"), "rx")] = pulse(frames, p, frames * 0.66, frames * 0.34, -lift_hind * 0.6)
    t[("Root", "lz")] = wave(frames, 0, bob, harmonic=2)
    t[("Neck", "rx")] = wave(frames, frames * 0.1, 3, harmonic=2)
    t[("Head", "rx")] = wave(frames, frames * 0.2, -2, harmonic=2)
    t[("Pelvis", "ry")] = wave(frames, phases[2], 2.5)
    t[("Chest", "ry")] = wave(frames, phases[0], -2.5)
    t[("Tail_01", "rz")] = wave(frames, 0, 6)
    return t


def clips():
    c = {}
    n = FAMILY
    # ---------------- Idle_A: breathing, ear flicks, a tail flick. 3.2 s loop.
    t = {("Chest", "rx"): [(0, 0), (48, 1.4), (96, 0)], ("Spine", "rx"): [(0, 0), (48, -0.7), (96, 0)],
         ("Neck", "rx"): [(0, 0), (40, -2), (96, 0)], ("Head", "rz"): [(0, 0), (36, 2.5), (76, -1.5), (96, 0)],
         ("Ear_L_01", "rx"): [(0, 0), (24, 0), (27, -8), (32, -2), (36, 0), (96, 0)],
         ("Ear_R_01", "rx"): [(0, 0), (60, 0), (63, -8), (68, -1), (72, 0), (96, 0)],
         ("Tail_01", "rx"): [(0, 0), (50, 0), (53, -14), (58, 0), (96, 0)], ("Tail_02", "rx"): [(0, 0), (52, 0), (55, -10), (60, 0), (96, 0)]}
    c[f"AN_{n}_Idle_A"] = (96, True, t)
    # ---------------- Idle_B (signature « oreilles aux aguets »): stops, head up, ears turn one way then the other.
    t = {("Neck", "rx"): [(0, 0), (16, -8), (100, -8), (120, 0)], ("Head", "rx"): [(0, 0), (16, -4), (100, -4), (120, 0)],
         ("Head", "ry"): [(0, 0), (20, 0), (34, 18), (60, 18), (74, -14), (100, -14), (116, 0), (120, 0)],
         ("Ear_L_01", "rz"): [(0, 0), (24, 0), (32, 18), (60, 18), (70, -14), (100, -14), (116, 0), (120, 0)],
         ("Ear_R_01", "rz"): [(0, 0), (28, 0), (36, 18), (62, 18), (72, -14), (102, -14), (116, 0), (120, 0)],
         ("Ear_L_01", "rx"): [(0, 0), (20, -10), (104, -10), (120, 0)], ("Ear_R_01", "rx"): [(0, 0), (20, -10), (104, -10), (120, 0)]}
    c[f"AN_{n}_Idle_B"] = (120, False, t)
    # ---------------- Walk: four beats (left hind, left fore, right hind, right fore), 1.07 s loop.
    c[f"AN_{n}_Locomotion_Walk"] = (32, True, walk(32, 16, 15, 38, 26, 0.006, (8, 16, 0, 24)))
    # ---------------- Trot: diagonal pairs, 0.67 s loop.
    c[f"AN_{n}_Locomotion_Trot"] = (20, True, walk(20, 22, 20, 52, 34, 0.012, (0, 10, 10, 0)))
    # ---------------- Happy: a little hop of joy, tail wagging. 1 s one-shot.
    t = {("Root", "lz"): [(0, 0), (5, -0.025), (10, 0.075), (15, 0.02), (18, -0.01), (22, 0.012), (26, 0), (30, 0)],
         ("Chest", "rx"): [(0, 0), (5, -5), (10, 8), (16, -4), (22, 2), (30, 0)],
         ("Neck", "rx"): [(0, 0), (5, 6), (10, -10), (16, 2), (30, 0)],
         ("Tail_01", "rz"): [(0, 0), (4, 22), (8, -22), (12, 22), (16, -22), (20, 16), (24, -10), (30, 0)],
         ("Tail_01", "rx"): [(0, 0), (6, -25), (24, -25), (30, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (5, -8), (10, 28), (16, 4), (30, 0)]))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 0), (6, 0), (10, -40), (16, -4), (30, 0)]))
    t.update(both("HindLeg_{}_Upper", "rx", [(0, 0), (5, 10), (10, -24), (16, 2), (30, 0)]))
    t.update(both("HindLeg_{}_Lower", "rx", [(0, 0), (6, 0), (10, 26), (16, 2), (30, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, 6), (10, -14), (18, 4), (30, 0)]))
    c[f"AN_{n}_Happy"] = (30, False, t)
    # ---------------- ReactTouch: a soft start, head back, ears back. 0.6 s one-shot.
    t = {("Neck", "rx"): [(0, 0), (4, -12), (10, -6), (18, 0)], ("Head", "rx"): [(0, 0), (4, -8), (10, -3), (18, 0)],
         ("Root", "lz"): [(0, 0), (3, -0.012), (8, 0.004), (18, 0)], ("Tail_01", "rx"): [(0, 0), (4, -20), (12, -8), (18, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (4, 28), (12, 10), (18, 0)]))
    c[f"AN_{n}_ReactTouch"] = (18, False, t)
    # ---------------- Sleep: lies down legs folded (enter 1 s), breathes (4 s), gets up (0.8 s).
    c[f"AN_{n}_Sleep_Enter"] = (30, False, blend_keys({}, SLEEP, 30, 24))
    t = hold(SLEEP, 120)
    t[("Chest", "rx")] = [(0, SLEEP[("Chest", "rx")]), (60, SLEEP[("Chest", "rx")] + 1.5), (120, SLEEP[("Chest", "rx")])]
    t[("Ear_L_01", "rx")] = [(0, 25), (70, 25), (73, 18), (78, 25), (120, 25)]
    c[f"AN_{n}_Sleep_Loop"] = (120, True, t)
    c[f"AN_{n}_Sleep_Exit"] = (24, False, blend_keys(SLEEP, {}, 24, 20))
    # ---------------- Eat: head down into the tall grass (the muzzle at about 19 cm), nibbles. 2 s loop.
    t = {("Neck", "rx"): [(0, 75), (60, 75)], ("Head", "rx"): [(0, 30), (10, 35), (20, 30), (30, 35), (40, 30), (50, 35), (60, 30)],
         ("Chest", "rx"): [(0, -10), (60, -10)], ("Ear_L_01", "rx"): [(0, 6), (30, 10), (60, 6)], ("Ear_R_01", "rx"): [(0, 6), (34, 2), (60, 6)],
         ("Tail_01", "rx"): [(0, 0), (40, 0), (43, -12), (48, 0), (60, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 4), (60, 4)]))
    c[f"AN_{n}_Eat_Loop"] = (60, True, t)
    # ---------------- Social_Nose: neck forward, nose to nose, ears forward. 1.3 s one-shot.
    t = {("Neck", "rx"): [(0, 0), (10, 18), (30, 18), (39, 0)], ("Head", "rx"): [(0, 0), (10, -6), (30, -6), (39, 0)],
         ("Tail_01", "rz"): [(0, 0), (14, 12), (20, -12), (26, 10), (32, 0), (39, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, -12), (30, -12), (39, 0)]))
    c[f"AN_{n}_Social_Nose"] = (39, False, t)
    # ---------------- Inspect: lowers its head to sniff, then looks up. 1.5 s one-shot.
    t = {("Neck", "rx"): [(0, 0), (12, 36), (28, 36), (38, -4), (45, 0)], ("Head", "rx"): [(0, 0), (12, 18), (20, 22), (28, 18), (38, -2), (45, 0)],
         ("Head", "rz"): [(0, 0), (16, 6), (26, -4), (45, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, -14), (30, -14), (45, 0)]))
    c[f"AN_{n}_Inspect"] = (45, False, t)
    # ---------------- PeekOut (T49 photo pose « Sortir du terrier »): lowers the muzzle towards the ground (less than
    # Eat, which stops at about 19 cm), sniffs, pops back up a little higher, a quick shake (head turns and tilts, ears
    # flap), settles. 1.8 s one-shot. Chest and legs stay at rest, so no hoof moves; Root only rises.
    t = {("Neck", "rx"): [(0, 0), (14, 55), (24, 55), (32, -6), (38, -6), (48, 0), (54, 0)],
         ("Head", "rx"): [(0, 0), (14, 22), (17, 26), (20, 21), (23, 25), (26, 22), (32, -10), (36, -8), (46, -2), (50, 0), (54, 0)],
         ("Head", "ry"): [(0, 0), (34, 0), (37, 14), (40, -14), (43, 10), (46, -7), (49, 3), (52, 0), (54, 0)],
         ("Head", "rz"): [(0, 0), (34, 0), (37, -6), (40, 6), (43, -4), (46, 3), (50, 0), (54, 0)],
         ("Root", "lz"): [(0, 0), (28, 0), (32, 0.008), (38, 0.005), (46, 0), (54, 0)],
         ("Tail_01", "rx"): [(0, 0), (30, -14), (36, 4), (44, 0), (54, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (14, -10), (26, -10), (32, 8), (37, -6), (40, 8), (43, -6), (47, 0), (54, 0)]))
    t.update(both("Ear_{}_01", "rz", [(0, 0), (34, 0), (37, -8), (40, 8), (43, -5), (46, 0), (54, 0)], -1))
    c[f"AN_{n}_PeekOut"] = (54, False, t)
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
    return made
