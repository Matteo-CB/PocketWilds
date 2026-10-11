
"""SmallQuadruped animation library. Source of the AN_SmallQuadruped_* actions (BL_Rig_SmallQuadruped.blend).
Rotations only (degrees, bone-local XYZ), plus the Root height ("lz") for in-place bounces, so every
species of the family can reuse the clips. Re-running rebuilds the actions.

Usage inside Blender, with BL_Rig_SmallQuadruped.blend open:
    exec(open(r"<project>/SourceArt/Blender/Tools/pw_anim_library.py").read())
    build_all()
    exec(open(r"<project>/SourceArt/Blender/Tools/pw_export.py").read()); export_animation_library("SmallQuadruped")

T34E (charte « fait main », section 8):
- The Root bone points up, so its local Y is world up: the bounce now lifts the body. It used to ride the bone's
  local Z, which is the animal's forward axis, and every hop slid the body 5 to 8 cm forward and back.
- Ear twitches at rest stay within 8 degrees.

T47: scale channels ("sx", "sy", "sz", bone-local) for a subtle squash; only the Field Hamster Stash uses them, on the
Head bone (cheeks puff, no shape key, no new bone). Every clip keys the Head scale at 1 so none inherits a squash."""
import bpy, math

ARM = bpy.data.objects["SK_SmallQuadruped"]
ROT = 'pose.bones["{}"].rotation_euler'
LOC = 'pose.bones["{}"].location'
SCL = 'pose.bones["{}"].scale'
AXIS = {"rx": 0, "ry": 1, "rz": 2, "lz": 2}
SCALE_CHANNELS = ("sx", "sy", "sz")


def head_scale_channels():
    """Bone-local scale channels of the Head bone that run along world X (width) and world Z (height) at rest, so
    the squash reads the same whatever the bone's roll."""
    m = ARM.data.bones["Head"].matrix_local.to_3x3()
    axes = [m.col[i].normalized() for i in range(3)]
    width = max(range(3), key=lambda i: abs(axes[i].x))
    height = max((i for i in range(3) if i != width), key=lambda i: abs(axes[i].z))
    return SCALE_CHANNELS[width], SCALE_CHANNELS[height]

def both(name_fmt, channel, keys, mirror_sign=1):
    """Same keys for L and R. mirror_sign=-1 flips R values (outward ear splay on Z)."""
    return {(name_fmt.format("L"), channel): keys,
            (name_fmt.format("R"), channel): [(f, v * mirror_sign) for f, v in keys]}

def build(name, frames, loop, tracks):
    act = bpy.data.actions.get(name)
    if act:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, frames
    act.use_cyclic = loop
    ARM.animation_data_create()
    ARM.animation_data.action = act
    for pb in ARM.pose.bones:
        pb.rotation_mode = 'XYZ'
    # Every bone gets complete channels so clips never inherit a stale pose.
    for pb in ARM.pose.bones:
        for i in range(3):
            key = (pb.name, ("rx", "ry", "rz")[i])
            keys = tracks.get(key, [(0, 0.0), (frames, 0.0)])
            fc = act.fcurve_ensure_for_datablock(ARM, ROT.format(pb.name), index=i, group_name=pb.name)
            for f, v in keys:
                fc.keyframe_points.insert(f, math.radians(v), options={'FAST'})
    # "lz" is the lift: the Root bone points up (its local Y is world up), so it rides location Y.
    root_keys = tracks.get(("Root", "lz"), [(0, 0.0), (frames, 0.0)])
    for i in range(3):
        fc = act.fcurve_ensure_for_datablock(ARM, LOC.format("Root"), index=i, group_name="Root")
        keys = root_keys if i == 1 else [(0, 0.0), (frames, 0.0)]
        for f, v in keys:
            fc.keyframe_points.insert(f, v, options={'FAST'})
    # Scale (squash) channels: the Head always, any other bone only when the clip keys it.
    scale_bones = ["Head"] + sorted({b for (b, ch) in tracks if ch in SCALE_CHANNELS and b != "Head"})
    for bone in scale_bones:
        for i, ch in enumerate(SCALE_CHANNELS):
            fc = act.fcurve_ensure_for_datablock(ARM, SCL.format(bone), index=i, group_name=bone)
            for f, v in tracks.get((bone, ch), [(0, 1.0), (frames, 1.0)]):
                fc.keyframe_points.insert(f, v, options={'FAST'})
    for fc in fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
            kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
        if loop:
            fc.modifiers.new('CYCLES')
        fc.update()
    return act

def fcurves(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out.extend(bag.fcurves)
        return out

# Shared sleep pose (degrees).
SLEEP = {("Root", "lz"): -0.030, ("Chest", "rx"): -6, ("Neck", "rx"): 18, ("Head", "rx"): 12, ("Head", "rz"): 8,
         ("Ear_L_01", "rx"): -48, ("Ear_R_01", "rx"): -48, ("Ear_L_02", "rx"): -10, ("Ear_R_02", "rx"): -10,
         ("Ear_L_01", "rz"): -6, ("Ear_R_01", "rz"): 6,
         ("FrontLeg_L_Upper", "rx"): -28, ("FrontLeg_R_Upper", "rx"): -28, ("FrontLeg_L_Lower", "rx"): 20, ("FrontLeg_R_Lower", "rx"): 20,
         ("HindLeg_L_Upper", "rx"): 28, ("HindLeg_R_Upper", "rx"): 28, ("HindLeg_L_Lower", "rx"): -10, ("HindLeg_R_Lower", "rx"): -10,
         ("Tail_01", "rx"): -15}

def hold(pose, frames):
    return {k: [(0, v), (frames, v)] for k, v in pose.items()}

def clips():
    c = {}
    # ---------------- Idle_A: calm breathing, alternate ear twitches, tail flick. 3.2 s loop.
    t = {("Chest", "rx"): [(0, 0), (48, 1.6), (96, 0)], ("Spine", "rx"): [(0, 0), (48, -0.8), (96, 0)],
         ("Root", "lz"): [(0, 0), (48, 0.0015), (96, 0)],
         ("Head", "rx"): [(0, 0), (30, -2), (60, 1.5), (96, 0)], ("Head", "rz"): [(0, 0), (40, 2), (80, -1), (96, 0)],
         ("Ear_L_01", "rx"): [(0, 0), (26, 0), (29, -7), (34, -2), (38, 0), (96, 0)],
         ("Ear_L_02", "rx"): [(0, 0), (29, 0), (32, -5), (37, 2), (41, 0), (96, 0)],
         ("Ear_R_01", "rx"): [(0, 0), (62, 0), (65, -7), (70, -1), (74, 0), (96, 0)],
         ("Ear_R_02", "rx"): [(0, 0), (65, 0), (68, -5), (73, 2), (77, 0), (96, 0)],
         ("Tail_01", "rx"): [(0, 0), (50, 0), (53, 8), (57, 0), (96, 0)]}
    t.update(both("Ear_{}_01", "rz", [(0, 0), (48, -2), (96, 0)], -1))
    c["AN_SmallQuadruped_Idle_A"] = (96, True, t)

    # ---------------- Idle_B: looks aside, one ear follows. 4 s, loop-safe.
    t = {("Neck", "ry"): [(0, 0), (20, 0), (32, 8), (84, 8), (100, 0), (120, 0)],
         ("Head", "ry"): [(0, 0), (20, 0), (34, 22), (84, 22), (102, 0), (120, 0)],
         ("Head", "rz"): [(0, 0), (22, 0), (36, 6), (80, 6), (100, 0), (120, 0)],
         ("Head", "rx"): [(0, 0), (34, -4), (60, -6), (84, -4), (102, 0), (120, 0)],
         ("Ear_L_01", "rx"): [(0, 0), (26, 0), (36, -7.5), (46, -5), (84, -5), (98, 0), (120, 0)],
         ("Ear_L_02", "rx"): [(0, 0), (30, 0), (40, -6), (50, -2), (98, 0), (120, 0)],
         ("Ear_R_01", "rx"): [(0, 0), (40, 0), (48, 6), (84, 6), (100, 0), (120, 0)],
         ("Chest", "rx"): [(0, 0), (60, 1.2), (120, 0)]}
    c["AN_SmallQuadruped_Idle_B"] = (120, False, t)

    # ---------------- Locomotion_Hop: in place, 0.67 s loop. Both hind legs push together.
    t = {("Root", "lz"): [(0, 0), (3, 0.004), (6, 0.035), (9, 0.055), (12, 0.032), (14, 0.008), (16, 0.0), (18, -0.004), (20, 0)],
         ("Pelvis", "rx"): [(0, 2), (5, 8), (9, 4), (13, -6), (16, -2), (20, 2)],
         ("Chest", "rx"): [(0, -2), (5, 6), (9, 4), (13, -7), (16, -3), (20, -2)],
         ("Neck", "rx"): [(0, 2), (6, -3), (10, -4), (14, 5), (17, 3), (20, 2)],
         ("Head", "rx"): [(0, 1), (6, -3), (10, -2), (14, 4), (17, 2), (20, 1)],
         ("Tail_01", "rx"): [(0, 0), (8, 10), (14, -6), (20, 0)]}
    t.update(both("HindLeg_{}_Upper", "rx", [(0, 18), (3, 10), (6, -18), (9, -14), (13, -2), (16, 20), (18, 22), (20, 18)]))
    t.update(both("HindLeg_{}_Lower", "rx", [(0, -8), (5, -12), (7, 10), (10, 6), (14, 0), (17, -10), (20, -8)]))
    t.update(both("HindPaw_{}", "rx", [(0, 0), (5, 4), (7, 20), (10, 24), (13, 12), (16, 2), (20, 0)]))
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, -12), (4, -18), (8, 15), (11, 24), (13, 6), (16, -10), (20, -12)]))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 6), (8, -6), (11, -10), (13, 4), (20, 6)]))
    t.update(both("FrontPaw_{}", "rx", [(0, 0), (9, 10), (12, -6), (14, 0), (20, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 4), (6, -6), (10, -16), (14, -4), (17, 8), (20, 4)]))
    t.update(both("Ear_{}_02", "rx", [(0, 2), (7, -4), (11, -12), (15, -2), (18, 9), (20, 2)]))
    c["AN_SmallQuadruped_Locomotion_Hop"] = (20, True, t)

    # ---------------- Happy: small joyful bounce with ears up. 1 s one-shot.
    t = {("Root", "lz"): [(0, 0), (4, -0.012), (7, 0.04), (11, 0.085), (15, 0.03), (18, -0.01), (21, 0.006), (25, 0), (30, 0)],
         ("Chest", "rx"): [(0, 0), (4, -6), (8, 10), (12, 8), (17, -8), (21, 3), (26, 0), (30, 0)],
         ("Pelvis", "rx"): [(0, 0), (4, 4), (9, -6), (16, 4), (22, 0), (30, 0)],
         ("Head", "rx"): [(0, 0), (4, 4), (9, -10), (13, -8), (18, 6), (23, -2), (28, 0), (30, 0)],
         ("Tail_01", "rx"): [(0, 0), (9, 14), (17, -6), (24, 4), (30, 0)]}
    t.update(both("HindLeg_{}_Upper", "rx", [(0, 0), (4, 22), (8, -18), (12, -10), (17, 18), (22, 4), (28, 0), (30, 0)]))
    t.update(both("HindLeg_{}_Lower", "rx", [(0, 0), (4, -10), (8, 8), (12, 4), (17, -8), (22, 0), (30, 0)]))
    t.update(both("HindPaw_{}", "rx", [(0, 0), (5, 2), (8, 22), (12, 24), (15, 8), (18, 0), (30, 0)]))
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (4, -10), (9, 22), (13, 18), (17, -8), (23, 0), (30, 0)]))
    t.update(both("Ear_{}_01", "rz", [(0, 0), (5, 0), (10, -14), (16, -4), (21, -8), (27, 0), (30, 0)], -1))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (5, 6), (10, -14), (14, -10), (18, 12), (23, -4), (28, 0), (30, 0)]))
    t.update(both("Ear_{}_02", "rx", [(0, 0), (6, 6), (11, -12), (15, -6), (19, 14), (24, -5), (29, 0), (30, 0)]))
    c["AN_SmallQuadruped_Happy"] = (30, False, t)

    # ---------------- ReactTouch: compacts, looks up with a tilt, ears perk. 0.6 s.
    t = {("Root", "lz"): [(0, 0), (3, -0.01), (9, -0.006), (18, 0)],
         ("Chest", "rx"): [(0, 0), (3, -6), (9, -3), (18, 0)],
         ("Neck", "rx"): [(0, 0), (3, 6), (10, 0), (18, 0)],
         ("Head", "rx"): [(0, 0), (3, 4), (7, -10), (12, -6), (18, 0)],
         ("Head", "rz"): [(0, 0), (5, 0), (8, 10), (13, 8), (18, 0)],
         ("Tail_01", "rx"): [(0, 0), (4, 10), (10, 0), (18, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (3, 8), (7, -14), (12, -6), (18, 0)]))
    t.update(both("Ear_{}_02", "rx", [(0, 0), (4, 8), (8, -10), (13, -2), (18, 0)]))
    c["AN_SmallQuadruped_ReactTouch"] = (18, False, t)

    # ---------------- Sleep_Enter: settles down, ears fold back with a little lag. 0.8 s.
    t = {}
    for k, v in SLEEP.items():
        t[k] = [(0, 0), (12, v * 0.7), (24, v)]
    for side, s in (("L", 1), ("R", 1)):
        t[(f"Ear_{side}_01", "rx")] = [(0, 0), (8, -8), (18, -52), (24, -48)]
        t[(f"Ear_{side}_02", "rx")] = [(0, 0), (10, -2), (20, -14), (24, -10)]
    c["AN_SmallQuadruped_Sleep_Enter"] = (24, False, t)

    # ---------------- Sleep_Loop: slow breathing, one small ear twitch. 4 s loop.
    t = hold(SLEEP, 120)
    t[("Chest", "rx")] = [(0, -6), (60, -4.5), (120, -6)]
    t[("Root", "lz")] = [(0, -0.030), (60, -0.027), (120, -0.030)]
    t[("Head", "rx")] = [(0, 12), (60, 13.2), (120, 12)]
    t[("Ear_L_01", "rx")] = [(0, -48), (80, -48), (83, -42), (88, -48), (120, -48)]
    c["AN_SmallQuadruped_Sleep_Loop"] = (120, True, t)

    # ---------------- Sleep_Exit (Wake): quick stretch back to neutral. 0.6 s.
    t = {k: [(0, v), (12, 0), (18, 0)] for k, v in SLEEP.items()}
    t[("Chest", "rx")] = [(0, -6), (8, 7), (13, 2), (18, 0)]
    t[("Root", "lz")] = [(0, -0.03), (8, 0.005), (18, 0)]
    t[("Head", "rx")] = [(0, 12), (7, -6), (12, -2), (18, 0)]
    for side in ("L", "R"):
        t[(f"Ear_{side}_01", "rx")] = [(0, -48), (6, -10), (10, 6), (14, -2), (18, 0)]
        t[(f"Ear_{side}_02", "rx")] = [(0, -10), (7, 0), (11, 8), (15, -2), (18, 0)]
    c["AN_SmallQuadruped_Sleep_Exit"] = (18, False, t)

    # ---------------- Eat_Loop: head down, quick nibbles. 2 s loop.
    nib = [(0, 24), (4, 20), (8, 24), (12, 20), (16, 24), (26, 24), (30, 21), (34, 24), (38, 21), (42, 24), (60, 24)]
    t = {("Neck", "rx"): [(0, 22), (60, 22)], ("Head", "rx"): nib, ("Head", "rz"): [(0, 0), (30, 3), (60, 0)],
         ("Chest", "rx"): [(0, -4), (30, -3), (60, -4)], ("Root", "lz"): [(0, -0.006), (60, -0.006)]}
    t.update(both("Ear_{}_01", "rx", [(0, 8), (30, 11), (60, 8)]))
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 6), (60, 6)]))
    c["AN_SmallQuadruped_Eat_Loop"] = (60, True, t)

    # ---------------- Social_Nose: reaches forward for a nose touch. 1.3 s.
    t = {("Neck", "rx"): [(0, 0), (10, 12), (26, 12), (39, 0)],
         ("Head", "rx"): [(0, 0), (10, -10), (14, -7), (18, -10), (22, -7), (26, -10), (39, 0)],
         ("Chest", "rx"): [(0, 0), (10, 4), (26, 4), (39, 0)],
         ("Pelvis", "rx"): [(0, 0), (10, -2), (26, -2), (39, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, 10), (26, 10), (39, 0)]))
    c["AN_SmallQuadruped_Social_Nose"] = (39, False, t)

    # ---------------- Inspect: lowers and tilts the head, sniffs. 1.5 s.
    t = {("Neck", "rx"): [(0, 0), (10, 14), (34, 14), (45, 0)],
         ("Head", "rx"): [(0, 0), (10, 12), (18, 16), (24, 12), (30, 15), (36, 8), (45, 0)],
         ("Head", "rz"): [(0, 0), (12, 12), (32, 12), (45, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (10, 12), (34, 10), (45, 0)]))
    t.update(both("Ear_{}_01", "rz", [(0, 0), (14, -6), (34, -6), (45, 0)], -1))
    c["AN_SmallQuadruped_Inspect"] = (45, False, t)

    # ---------------- Locomotion_Walk: low-bodied scurry (doc: optional for low species), 0.8 s loop.
    # Diagonal pairs (front L + hind R), two small bobs, counter-swaying chest and pelvis.
    fwd = [(0, 16), (6, 0), (12, -16), (18, 0), (24, 16)]
    back = [(0, -16), (6, 0), (12, 16), (18, 0), (24, -16)]
    lift_l = [(0, 0), (12, 0), (18, -12), (24, 0)]
    lift_r = [(0, 0), (6, -12), (12, 0), (24, 0)]
    t = {("FrontLeg_L_Upper", "rx"): fwd, ("FrontLeg_R_Upper", "rx"): back,
         ("HindLeg_R_Upper", "rx"): fwd, ("HindLeg_L_Upper", "rx"): back,
         ("FrontLeg_L_Lower", "rx"): lift_l, ("FrontLeg_R_Lower", "rx"): lift_r,
         ("HindLeg_R_Lower", "rx"): [(f, -v * 0.6) for f, v in lift_l], ("HindLeg_L_Lower", "rx"): [(f, -v * 0.6) for f, v in lift_r],
         ("Root", "lz"): [(0, 0), (6, 0.007), (12, 0), (18, 0.007), (24, 0)],
         ("Chest", "rz"): [(0, 0), (6, 2.5), (12, 0), (18, -2.5), (24, 0)],
         ("Pelvis", "rz"): [(0, 0), (6, -2), (12, 0), (18, 2), (24, 0)],
         ("Head", "rx"): [(0, 0), (6, -2), (12, 0), (18, -2), (24, 0)],
         ("Head", "rz"): [(0, 0), (6, -1.5), (12, 0), (18, 1.5), (24, 0)],
         ("Tail_01", "rz"): [(0, 0), (6, 4), (12, 0), (18, -4), (24, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, -3), (12, 0), (18, -3), (24, 0)]))
    c["AN_SmallQuadruped_Locomotion_Walk"] = (24, True, t)

    # ---------------- Happy_Bounce: two low bounces instead of a high hop (low species). 1 s, same length as Happy.
    t = {("Root", "lz"): [(0, 0), (3, -0.008), (7, 0.03), (10, 0.035), (13, 0), (15, -0.006), (19, 0.022), (22, 0.024), (25, 0), (27, -0.003), (30, 0)],
         ("Chest", "rx"): [(0, 0), (3, -5), (8, 4), (13, -4), (16, -3), (20, 3), (25, -2), (30, 0)],
         ("Pelvis", "rx"): [(0, 0), (3, 3), (8, -3), (13, 3), (20, -2), (25, 1), (30, 0)],
         ("Head", "rx"): [(0, 0), (4, 3), (9, -10), (14, -4), (20, -9), (26, -2), (30, 0)],
         ("Head", "rz"): [(0, 0), (10, 6), (20, -6), (30, 0)],
         ("Tail_01", "rx"): [(0, 0), (9, 8), (16, -4), (22, 6), (30, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (8, 10), (13, 0), (20, 8), (25, 0), (30, 0)]))
    t.update(both("HindLeg_{}_Upper", "rx", [(0, 0), (8, -8), (13, 0), (20, -6), (25, 0), (30, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, 5), (10, -10), (15, 4), (20, -8), (26, 0), (30, 0)]))
    c["AN_SmallQuadruped_Happy_Bounce"] = (30, False, t)

    # ---------------- FieldHamsterGreet (T47): the companion's hello when the player comes back. 1 s one-shot.
    # A small lean back, one front paw lifts and waggles while the other lifts a little, a bright nod, ears perk.
    # Legs only swing forward (paws rise, never below the ground); Root lifts 8 mm at most.
    t = {("Root", "lz"): [(0, 0), (4, 0.0), (9, 0.008), (16, 0.006), (22, 0.002), (30, 0)],
         ("Chest", "rx"): [(0, 0), (4, 0), (9, -5), (18, -4), (25, 0), (30, 0)],
         ("Neck", "rx"): [(0, 0), (8, -4), (13, 6), (18, -2), (22, 4), (27, 0), (30, 0)],
         ("Head", "rx"): [(0, 0), (4, 3), (8, -9), (13, 12), (18, -4), (22, 8), (27, 0), (30, 0)],
         ("Head", "rz"): [(0, 0), (8, 0), (13, 6), (20, 6), (27, 0), (30, 0)],
         ("FrontLeg_L_Upper", "rx"): [(0, 0), (4, 0), (9, 32), (20, 30), (26, 4), (30, 0)],
         ("FrontLeg_L_Lower", "rx"): [(0, 0), (5, 0), (9, -16), (12, -6), (15, -18), (18, -6), (21, -16), (26, -2), (30, 0)],
         ("FrontPaw_L", "rx"): [(0, 0), (5, 0), (9, 10), (12, 22), (15, 8), (18, 22), (21, 10), (26, 0), (30, 0)],
         ("FrontLeg_R_Upper", "rx"): [(0, 0), (4, 0), (10, 12), (19, 10), (25, 0), (30, 0)],
         ("FrontLeg_R_Lower", "rx"): [(0, 0), (5, 0), (10, -6), (19, -5), (25, 0), (30, 0)],
         ("Tail_01", "rx"): [(0, 0), (9, 8), (18, -3), (26, 0), (30, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (4, 4), (9, -10), (14, -6), (19, -9), (26, 0), (30, 0)]))
    t.update(both("Ear_{}_01", "rz", [(0, 0), (9, -6), (19, -4), (27, 0), (30, 0)], -1))
    c["AN_SmallQuadruped_FieldHamsterGreet"] = (30, False, t)

    # ---------------- FieldHamsterStash (T47): sniffs the ground, picks a seed with both paws, tucks it in a cheek
    # in two pushes (the cheeks puff by a subtle Head squash: 7 % wider, 3 % lower), a happy wiggle, back to rest.
    # 2.4 s one-shot. Safe on every SmallQuadruped: rotations within the Eat and Inspect ranges, paws only rise.
    width, height = head_scale_channels()
    t = {("Neck", "rx"): [(0, 0), (8, 14), (20, 14), (28, 2), (50, 0), (72, 0)],
         ("Head", "rx"): [(0, 0), (8, 14), (11, 18), (14, 14), (17, 18), (20, 12), (28, -4), (34, 2), (38, -3), (44, 1), (50, -2), (62, 0), (72, 0)],
         ("Head", "rz"): [(0, 0), (8, 6), (20, 4), (28, 0), (50, 0), (53, 5), (56, -5), (59, 4), (62, 0), (72, 0)],
         ("Chest", "rx"): [(0, 0), (8, 0), (20, 0), (28, -3), (48, -3), (60, 0), (72, 0)],
         ("Root", "lz"): [(0, 0), (20, 0), (28, 0.004), (48, 0.004), (60, 0), (72, 0)],
         ("Head", width): [(0, 1.0), (26, 1.0), (31, 1.045), (34, 1.035), (39, 1.07), (52, 1.07), (64, 1.02), (72, 1.0)],
         ("Head", height): [(0, 1.0), (26, 1.0), (31, 0.985), (34, 0.99), (39, 0.97), (52, 0.97), (64, 0.993), (72, 1.0)],
         ("Tail_01", "rx"): [(0, 0), (40, 0), (44, 8), (50, -4), (56, 0), (72, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (8, 8), (16, 10), (24, 26), (32, 28), (40, 22), (48, 4), (56, 0), (72, 0)]))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 0), (8, -4), (24, -22), (32, -24), (40, -18), (48, -2), (56, 0), (72, 0)]))
    t.update(both("FrontPaw_{}", "rx", [(0, 0), (24, 14), (32, 18), (40, 12), (48, 0), (72, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (8, 8), (20, 8), (30, -4), (40, -6), (52, -2), (62, 0), (72, 0)]))
    c["AN_SmallQuadruped_FieldHamsterStash"] = (72, False, t)
    return c

def build_all():
    made = []
    for name, (frames, loop, tracks) in clips().items():
        build(name, frames, loop, tracks)
        made.append(name)
    ARM.animation_data.action = None
    for pb in ARM.pose.bones:
        pb.rotation_euler = (0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
    return made
