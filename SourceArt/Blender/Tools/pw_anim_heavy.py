"""HeavyQuadruped animation library (T52). Source of the AN_HeavyQuadruped_* actions (BL_Rig_HeavyQuadruped.blend).

Rotations only (degrees, bone-local XYZ) plus the Root lift (« lz », the Root bone points up so it rides location Y),
as in pw_anim_medium. Bone axes follow SmallQuadruped (pw_rig): on the legs, Pelvis, Spine and Chest a positive X turn
swings the bone forward or lifts the front; on Neck and Head it nods down; on the hock (HindLeg_*_Lower) it folds the
lower leg back and up; on Tail_01 it lifts the tail; on the ears (Ear_*_01, Ear_*_02) it flops the ear forward and down.
Head Y turns the head (positive to the animal's left, +X), Head Z tilts it; Pelvis and Spine Y roll the body.

Usage inside Blender, with BL_Rig_HeavyQuadruped.blend open:
    import pw_anim_heavy; pw_anim_heavy.build_all()
    import pw_export; pw_export.export_animation_library("HeavyQuadruped")
Clips (doc RIGS, RIG-003: HeavyIdle, WaddleWalk, HappyBounce, React, Sleep, Eat, Inspect), named like every family
library so the Unity controller finds them: Idle_A/B, Locomotion_Walk (a cute waddle), Happy (a little bounce),
ReactTouch, Sleep_Enter/Loop/Exit (a belly-down stretch, front legs forward and hind legs back), Eat_Loop (roots in the
leaves), Social_Nose, Inspect (the snout push through the leaves, the Boar Piglet's signature with Eat) and PeekOut (photo
pose). In place, 30 fps, no high-frequency jiggle. Measured on SKM_BoarPiglet LOD0 and LOD2: no vertex below the ground
beyond 2 mm, no head, leg or tail through the body. The front never dips (Chest and Spine X stay at 0 or above, Root
only rises outside Sleep), so no hoof sinks while the head moves.
"""
import math
import bpy

FAMILY = "HeavyQuadruped"
ROT = 'pose.bones["{}"].rotation_euler'
LOC = 'pose.bones["{}"].location'
LEGS = ("FrontLeg_L", "FrontLeg_R", "HindLeg_L", "HindLeg_R")


def arm():
    return bpy.data.objects["SK_" + FAMILY]


def both(fmt, channel, keys, mirror=1):
    return {(fmt.format("L"), channel): keys, (fmt.format("R"), channel): [(f, v * mirror) for f, v in keys]}


def wave(frames, phase, amp, step=2, harmonic=1, offset=0.0):
    """Keys of a cosine over one loop (frames), shifted by phase frames, sampled every step frames."""
    return [(f, offset + amp * math.cos(2 * math.pi * harmonic * (f - phase) / frames)) for f in range(0, frames + 1, step)]


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


# Belly-down stretch (« sploot »): the belly rests on the ground, the front legs reach forward under the chin, the hind
# legs stretch back, the head rests low and turned a little, ears soft. Measured on the Boar Piglet.
SLEEP = {
    ("Root", "lz"): -0.0655,
    ("Neck", "rx"): 9, ("Head", "rx"): 7, ("Head", "rz"): 6, ("Head", "ry"): 8,
    ("FrontLeg_L_Upper", "rx"): 72, ("FrontLeg_R_Upper", "rx"): 72,
    ("FrontLeg_L_Lower", "rx"): -6, ("FrontLeg_R_Lower", "rx"): -6,
    ("HindLeg_L_Upper", "rx"): -72, ("HindLeg_R_Upper", "rx"): -72,
    ("HindLeg_L_Lower", "rx"): 10, ("HindLeg_R_Lower", "rx"): 10,
    ("HindPaw_L", "rx"): -10, ("HindPaw_R", "rx"): -10,
    ("Ear_L_01", "rx"): 18, ("Ear_R_01", "rx"): 18, ("Ear_L_02", "rx"): 10, ("Ear_R_02", "rx"): 10,
    ("Tail_01", "rx"): -10,
}

# Rooting in the leaves: the snout low over the ground, nudging.
EAT = {("Neck", "rx"): 40, ("Head", "rx"): 16}


def walk(frames, amp_front, amp_hind, lift_front, lift_hind, roll, phases):
    """A short-legged four-beat waddle: the body rolls toward the leg taking the weight and lifts just enough that no
    hoof sinks while it rolls; the head counter-rolls a little, the ears bounce, the curly tail wiggles."""
    t = {}
    for leg, p in zip(LEGS, phases):
        front = leg.startswith("Front")
        t[(leg + "_Upper", "rx")] = wave(frames, p, amp_front if front else amp_hind)
        if front:
            t[(leg + "_Lower", "rx")] = pulse(frames, p, frames * 0.68, frames * 0.40, -lift_front)
            t[(leg.replace("Leg", "Paw"), "rx")] = pulse(frames, p, frames * 0.62, frames * 0.32, -lift_front * 0.5)
        else:
            t[(leg + "_Lower", "rx")] = pulse(frames, p, frames * 0.68, frames * 0.40, lift_hind)
            t[(leg.replace("Leg", "Paw"), "rx")] = pulse(frames, p, frames * 0.66, frames * 0.34, -lift_hind * 0.6)
    # The lift follows the roll: the lower hip side drops by its half width times the sine of the roll (with margin).
    t[("Root", "lz")] = [(f, 0.074 * 1.25 * math.sin(math.radians(roll * abs(math.cos(2 * math.pi * f / frames)))))
                         for f in range(0, frames + 1, 2)]
    t[("Pelvis", "ry")] = wave(frames, 0, roll)
    t[("Chest", "ry")] = wave(frames, 0, -roll * 0.35)
    t[("Pelvis", "rz")] = wave(frames, frames * 0.25, 3)
    t[("Chest", "rz")] = wave(frames, frames * 0.25, -3)
    t[("Head", "rz")] = wave(frames, 0, -roll * 0.5)
    t[("Neck", "rx")] = wave(frames, frames * 0.1, 2.5, harmonic=2)
    t[("Head", "rx")] = wave(frames, frames * 0.2, -2, harmonic=2)
    t.update(both("Ear_{}_01", "rx", wave(frames, frames * 0.15, 5, harmonic=2, offset=4)))
    t.update(both("Ear_{}_02", "rx", wave(frames, frames * 0.25, 7, harmonic=2, offset=3)))
    t[("Tail_01", "rz")] = wave(frames, 0, 16, harmonic=2)
    return t


def clips():
    c = {}
    n = FAMILY
    # ---------------- Idle_A: breathing, little snout sniffs, ear flicks, a curly tail wiggle. 3.2 s loop.
    t = {("Chest", "rx"): [(0, 0), (48, 1.6), (96, 0)], ("Spine", "rx"): [(0, 0), (48, -0.6), (96, 0)],
         ("Neck", "rx"): [(0, 0), (44, -1.5), (96, 0)],
         ("Head", "rx"): [(0, 0), (14, 0), (17, -3), (20, 0), (23, -3), (26, 0), (96, 0)],
         ("Head", "rz"): [(0, 0), (36, 3), (76, -2), (96, 0)],
         ("Ear_L_01", "rx"): [(0, 0), (30, 0), (33, 12), (38, 3), (42, 0), (96, 0)],
         ("Ear_L_02", "rx"): [(0, 0), (31, 0), (35, 10), (40, 0), (96, 0)],
         ("Ear_R_01", "rx"): [(0, 0), (62, 0), (65, 12), (70, 3), (74, 0), (96, 0)],
         ("Ear_R_02", "rx"): [(0, 0), (63, 0), (67, 10), (72, 0), (96, 0)],
         ("Tail_01", "rz"): [(0, 0), (54, 0), (57, 18), (60, -18), (63, 12), (66, 0), (96, 0)]}
    c[f"AN_{n}_Idle_A"] = (96, True, t)
    # ---------------- Idle_B: raises the snout and sniffs the air, looks one way then the other, ears perked.
    t = {("Neck", "rx"): [(0, 0), (16, -7), (100, -7), (120, 0)],
         ("Head", "rx"): [(0, 0), (16, -8), (30, -8), (33, -12), (36, -8), (39, -12), (42, -8), (100, -8), (120, 0)],
         ("Head", "ry"): [(0, 0), (44, 0), (56, 20), (72, 20), (84, -18), (100, -18), (114, 0), (120, 0)],
         ("Tail_01", "rx"): [(0, 0), (16, 8), (104, 8), (120, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (16, -12), (104, -12), (120, 0)]))
    c[f"AN_{n}_Idle_B"] = (120, False, t)
    # ---------------- Walk: a four-beat waddle (left hind, left fore, right hind, right fore), 0.8 s loop.
    c[f"AN_{n}_Locomotion_Walk"] = (24, True, walk(24, 18, 16, 40, 30, 4.0, (6, 18, 0, 12)))
    # ---------------- Happy: two little bounces of joy, legs tucked in the air, ears flopping, tail wagging. 1.2 s.
    t = {("Root", "lz"): [(0, 0), (4, 0), (9, 0.042), (13, 0.004), (15, 0), (20, 0.026), (25, 0.002), (27, 0), (36, 0)],
         ("Spine", "rx"): [(0, 0), (4, 0), (9, 5), (14, 0), (20, 3), (25, 0), (36, 0)],
         ("Neck", "rx"): [(0, 0), (4, 6), (9, -8), (14, 4), (20, -5), (26, 2), (36, 0)],
         ("Head", "rx"): [(0, 0), (9, -4), (14, 3), (20, -3), (26, 0), (36, 0)],
         ("Tail_01", "rz"): [(0, 0), (3, 20), (6, -20), (9, 20), (12, -20), (15, 18), (18, -18), (22, 12), (26, -8), (30, 0), (36, 0)],
         ("Tail_01", "rx"): [(0, 0), (5, 14), (28, 14), (36, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (5, 0), (9, 22), (13, 0), (16, 0), (20, 14), (24, 0), (36, 0)]))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 0), (5, 0), (9, -40), (13, 0), (16, 0), (20, -26), (24, 0), (36, 0)]))
    t.update(both("HindLeg_{}_Upper", "rx", [(0, 0), (5, 0), (9, -18), (13, 0), (16, 0), (20, -12), (24, 0), (36, 0)]))
    t.update(both("HindLeg_{}_Lower", "rx", [(0, 0), (5, 0), (9, 30), (13, 0), (16, 0), (20, 20), (24, 0), (36, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, -10), (11, 14), (15, -6), (20, 10), (24, -4), (29, 5), (33, 0), (36, 0)]))
    t.update(both("Ear_{}_02", "rx", [(0, 0), (7, -8), (12, 14), (17, -6), (22, 10), (26, -3), (31, 4), (36, 0)]))
    c[f"AN_{n}_Happy"] = (36, False, t)
    # ---------------- ReactTouch: a soft start, snout back, ears back, a tiny lift. 0.6 s.
    t = {("Neck", "rx"): [(0, 0), (4, -10), (10, -5), (18, 0)], ("Head", "rx"): [(0, 0), (4, -6), (10, -2), (18, 0)],
         ("Root", "lz"): [(0, 0), (3, 0.010), (8, 0.003), (18, 0)], ("Tail_01", "rx"): [(0, 0), (4, 15), (12, 5), (18, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (4, -18), (12, -6), (18, 0)]))
    t.update(both("Ear_{}_02", "rx", [(0, 0), (5, -10), (13, -3), (18, 0)]))
    c[f"AN_{n}_ReactTouch"] = (18, False, t)
    # ---------------- Sleep: stretches out belly down (enter 1 s), breathes (4 s), gets up (0.8 s). The legs reach out
    # first and the body lowers after, so no hoof sweeps below the ground; on waking the body rises first.
    t = {}
    for k, v in SLEEP.items():
        if k[0] == "Root":
            t[k] = [(0, 0), (10, v * 0.15), (24, v), (30, v)]
        elif k[0].startswith(("FrontLeg", "HindLeg", "FrontPaw", "HindPaw")):
            t[k] = [(0, 0), (12, v * 0.85), (20, v), (30, v)]
        else:
            t[k] = [(0, 0), (14, v * 0.3), (26, v), (30, v)]
    c[f"AN_{n}_Sleep_Enter"] = (30, False, t)
    t = hold(SLEEP, 120)
    t[("Chest", "rx")] = [(0, 0), (60, 1.5), (120, 0)]
    t[("Spine", "rx")] = [(0, 0), (60, 0.6), (120, 0)]
    t[("Ear_L_01", "rx")] = [(0, 18), (70, 18), (73, 10), (78, 18), (120, 18)]
    t[("Tail_01", "rz")] = [(0, 0), (90, 0), (93, 12), (97, -8), (100, 0), (120, 0)]
    c[f"AN_{n}_Sleep_Loop"] = (120, True, t)
    t = {}
    for k, v in SLEEP.items():
        if k[0] == "Root":
            t[k] = [(0, v), (10, v * 0.3), (16, 0.004), (20, 0.002), (24, 0)]
        elif k[0].startswith(("FrontLeg", "HindLeg", "FrontPaw", "HindPaw")):
            t[k] = [(0, v), (8, v), (18, v * 0.1), (24, 0)]
        else:
            t[k] = [(0, v), (12, v * 0.4), (24, 0)]
    c[f"AN_{n}_Sleep_Exit"] = (24, False, t)
    # ---------------- Eat: roots in the leaves, the snout low over the ground nudging forward. 2 s loop.
    nx, hx = EAT[("Neck", "rx")], EAT[("Head", "rx")]
    t = {("Neck", "rx"): [(0, nx), (10, nx - 4), (20, nx), (30, nx - 4), (40, nx), (50, nx - 3), (60, nx)],
         ("Head", "rx"): [(0, hx), (8, hx - 8), (15, hx), (20, hx), (28, hx - 8), (35, hx), (40, hx), (48, hx - 6), (55, hx), (60, hx)],
         ("Head", "ry"): [(0, 0), (20, 4), (40, -4), (60, 0)],
         ("Ear_L_01", "rx"): [(0, 10), (30, 14), (60, 10)], ("Ear_R_01", "rx"): [(0, 10), (34, 6), (60, 10)],
         ("Tail_01", "rz"): [(0, 0), (40, 0), (43, 16), (46, -16), (49, 0), (60, 0)]}
    c[f"AN_{n}_Eat_Loop"] = (60, True, t)
    # ---------------- Social_Nose: snout forward and up, nose to nose, a little nuzzle, ears forward. 1.3 s.
    t = {("Neck", "rx"): [(0, 0), (10, 14), (30, 14), (39, 0)], ("Head", "rx"): [(0, 0), (10, -10), (30, -10), (39, 0)],
         ("Head", "ry"): [(0, 0), (14, 0), (18, 6), (22, -6), (26, 4), (30, 0), (39, 0)],
         ("Tail_01", "rz"): [(0, 0), (12, 16), (16, -16), (20, 14), (24, -12), (28, 0), (39, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, 8), (30, 8), (39, 0)]))
    c[f"AN_{n}_Social_Nose"] = (39, False, t)
    # ---------------- Inspect (signature « groin fouineur »): snout down into the leaves, two pushes forward that scoop
    # up, then the head comes up with a happy little shake. 2 s one-shot. Legs at rest; nothing dips.
    t = {("Neck", "rx"): [(0, 0), (10, 28), (16, 28), (20, 22), (24, 30), (28, 28), (32, 22), (36, 30), (42, 26), (48, -4), (54, -2), (60, 0)],
         ("Head", "rx"): [(0, 0), (10, 20), (16, 20), (20, 8), (24, 22), (28, 20), (32, 8), (36, 22), (42, 18), (48, -8), (54, -2), (60, 0)],
         ("Head", "ry"): [(0, 0), (46, 0), (49, 10), (52, -10), (55, 6), (58, 0), (60, 0)],
         ("Head", "rz"): [(0, 0), (46, 0), (49, -4), (52, 4), (55, -2), (60, 0)],
         ("Tail_01", "rz"): [(0, 0), (18, 0), (21, 16), (24, -16), (27, 14), (30, -12), (33, 10), (36, 0), (60, 0)],
         ("Tail_01", "rx"): [(0, 0), (12, 10), (44, 10), (60, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, 12), (20, 4), (24, 14), (32, 4), (36, 14), (46, -8), (50, 6), (54, -3), (60, 0)]))
    t.update(both("Ear_{}_02", "rx", [(0, 0), (11, 8), (21, 2), (25, 10), (33, 2), (37, 10), (47, -6), (51, 5), (60, 0)]))
    c[f"AN_{n}_Inspect"] = (60, False, t)
    # ---------------- PeekOut (photo pose « Sortir du terrier »): snout down, sniffs, pops up a little higher, a quick
    # shake (head turns and tilts, ears flap), settles. 1.8 s one-shot. Legs at rest; Root only rises.
    t = {("Neck", "rx"): [(0, 0), (14, 16), (24, 16), (32, -8), (38, -8), (48, 0), (54, 0)],
         ("Head", "rx"): [(0, 0), (14, 20), (17, 24), (20, 19), (23, 23), (26, 20), (32, -12), (36, -10), (46, -2), (50, 0), (54, 0)],
         ("Spine", "rx"): [(0, 0), (28, 0), (32, 2.5), (44, 0), (54, 0)],
         ("Head", "ry"): [(0, 0), (34, 0), (37, 14), (40, -14), (43, 10), (46, -7), (49, 3), (52, 0), (54, 0)],
         ("Head", "rz"): [(0, 0), (34, 0), (37, -6), (40, 6), (43, -4), (46, 3), (50, 0), (54, 0)],
         ("Root", "lz"): [(0, 0), (28, 0), (32, 0.008), (38, 0.005), (46, 0), (54, 0)],
         ("Tail_01", "rx"): [(0, 0), (30, 14), (36, -4), (44, 0), (54, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (14, 10), (26, 10), (32, -8), (37, 8), (40, -8), (43, 6), (47, 0), (54, 0)]))
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
