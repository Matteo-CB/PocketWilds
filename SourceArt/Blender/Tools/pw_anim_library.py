
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
Head bone (cheeks puff, no shape key, no new bone). Every clip keys the Head scale at 1 so none inherits a squash.

T49: MoleDig (the Mole's dig, safe for the whole family), MoleEat (its low nibble) and PeekOut (the photo pose
« Sortir du terrier »). MoleSleep_Enter/_Loop/_Exit, MoleReact and MoleHappy replace the family Sleep, ReactTouch and
Happy on the Mole, whose low snout and big mitten hands went below the ground. Every Mole clip is measured frame by
frame on SKM_Mole_LOD0 and its star nose module: the lowest vertex stays at or above the ground.

Ground contact (2026-10-12, « Aucune collision »): measured frame by frame on every small species (LOD0 and LOD2, prefab
scale included), the family Sleep, Locomotion_Hop, Locomotion_Walk, Happy, Happy_Bounce, ReactTouch, Eat_Loop and
Social_Nose took paws, bellies or heads 2 to 40 mm below the ground. The PLANTED keys (end of file, solved by
pw_anim_ground.py) lift the Root where a belly needs it and bend the legs with the paws kept flat, so nothing goes below
the ground. Sleep lowers the body 22 mm instead of 30 (the Field Mouse's belly) and folds the legs onto the ground;
LowSleep_Enter/_Loop/_Exit is the sleep of the Hedgehog, Guinea Pig and Field Hamster, whose bellies already rest on the
ground (their override controllers swap it in). FamilyClipGroundTests (EditMode) checks every clip each species plays."""
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
SLEEP = {("Root", "lz"): -0.022, ("Chest", "rx"): -6, ("Neck", "rx"): 18, ("Head", "rx"): 12, ("Head", "rz"): 8,
         ("Ear_L_01", "rx"): -48, ("Ear_R_01", "rx"): -48, ("Ear_L_02", "rx"): -10, ("Ear_R_02", "rx"): -10,
         ("Ear_L_01", "rz"): -6, ("Ear_R_01", "rz"): 6,
         ("FrontLeg_L_Upper", "rx"): -28, ("FrontLeg_R_Upper", "rx"): -28, ("FrontLeg_L_Lower", "rx"): 20, ("FrontLeg_R_Lower", "rx"): 20,
         ("HindLeg_L_Upper", "rx"): 28, ("HindLeg_R_Upper", "rx"): 28, ("HindLeg_L_Lower", "rx"): -10, ("HindLeg_R_Lower", "rx"): -10,
         ("Tail_01", "rx"): -15}

# The low-bellied species' sleep pose (LowSleep): SLEEP without the body drop and the leg fold.
LOW_SLEEP = {k: v for k, v in SLEEP.items() if k[0] not in ("Root", "Chest") and "Leg" not in k[0]}

# The Mole's sleep pose (T49): mittens forward and in beside the chin (L and R mirrored on Z), head lowered and turned
# onto them, hind legs splayed so the belly rests on the ground. Measured on SKM_Mole_LOD0 and its star nose module.
MOLE_SLEEP = {("Root", "lz"): -0.001, ("Neck", "rx"): 6, ("Head", "rx"): 5, ("Head", "ry"): 8, ("Head", "rz"): 5,
              ("FrontLeg_L_Upper", "rx"): 12, ("FrontLeg_R_Upper", "rx"): 12,
              ("FrontLeg_L_Lower", "rx"): -8, ("FrontLeg_R_Lower", "rx"): -8,
              ("FrontLeg_L_Upper", "rz"): -9, ("FrontLeg_R_Upper", "rz"): 9,
              ("HindLeg_L_Upper", "rz"): 6, ("HindLeg_R_Upper", "rz"): -6,
              ("Tail_01", "rx"): -12, ("Ear_L_01", "rx"): -20, ("Ear_R_01", "rx"): -20}

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
    t[("Root", "lz")] = [(0, -0.022), (60, -0.020), (120, -0.022)]
    t[("Head", "rx")] = [(0, 12), (60, 13.2), (120, 12)]
    t[("Ear_L_01", "rx")] = [(0, -48), (80, -48), (83, -42), (88, -48), (120, -48)]
    c["AN_SmallQuadruped_Sleep_Loop"] = (120, True, t)

    # ---------------- Sleep_Exit (Wake): quick stretch back to neutral. 0.6 s.
    t = {k: [(0, v), (12, 0), (18, 0)] for k, v in SLEEP.items()}
    t[("Chest", "rx")] = [(0, -6), (8, 7), (13, 2), (18, 0)]
    t[("Root", "lz")] = [(0, -0.022), (8, 0.005), (18, 0)]
    t[("Head", "rx")] = [(0, 12), (7, -6), (12, -2), (18, 0)]
    for side in ("L", "R"):
        t[(f"Ear_{side}_01", "rx")] = [(0, -48), (6, -10), (10, 6), (14, -2), (18, 0)]
        t[(f"Ear_{side}_02", "rx")] = [(0, -10), (7, 0), (11, 8), (15, -2), (18, 0)]
    c["AN_SmallQuadruped_Sleep_Exit"] = (18, False, t)

    # ---------------- LowSleep_Enter / _Loop / _Exit: the sleep of the round, low-bellied species (Hedgehog, Guinea Pig,
    # Field Hamster), whose belly already rests on the ground: the same head, ears, chest and tail as Sleep, the body
    # does not lower and the paws stay planted. Same lengths as the family Sleep clips.
    t = {}
    for k, v in LOW_SLEEP.items():
        t[k] = [(0, 0), (12, v * 0.7), (24, v)]
    for side in ("L", "R"):
        t[(f"Ear_{side}_01", "rx")] = [(0, 0), (8, -8), (18, -52), (24, -48)]
        t[(f"Ear_{side}_02", "rx")] = [(0, 0), (10, -2), (20, -14), (24, -10)]
    c["AN_SmallQuadruped_LowSleep_Enter"] = (24, False, t)

    t = hold(LOW_SLEEP, 120)
    t[("Chest", "rx")] = [(0, 0), (60, 1.2), (120, 0)]
    t[("Head", "rx")] = [(0, 12), (60, 13.2), (120, 12)]
    t[("Ear_L_01", "rx")] = [(0, -48), (80, -48), (83, -42), (88, -48), (120, -48)]
    c["AN_SmallQuadruped_LowSleep_Loop"] = (120, True, t)

    t = {k: [(0, v), (12, 0), (18, 0)] for k, v in LOW_SLEEP.items()}
    t[("Chest", "rx")] = [(0, 0), (8, 7), (13, 2), (18, 0)]
    t[("Root", "lz")] = [(0, 0), (8, 0.004), (18, 0)]
    t[("Head", "rx")] = [(0, 12), (7, -6), (12, -2), (18, 0)]
    for side in ("L", "R"):
        t[(f"Ear_{side}_01", "rx")] = [(0, -48), (6, -10), (10, 6), (14, -2), (18, 0)]
        t[(f"Ear_{side}_02", "rx")] = [(0, -10), (7, 0), (11, 8), (15, -2), (18, 0)]
    c["AN_SmallQuadruped_LowSleep_Exit"] = (18, False, t)

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

    # ---------------- MoleDig (T49, L'Éveil des Terriers): scratches the ground with both front paws in turn (two
    # strokes each), the head low over the spot, then sniffs. 2 s one-shot (a pooled hole decal plays under it).
    # Safe on every SmallQuadruped: the upper legs only swing forward (a backward swing would sink the paws), each stroke
    # ends on the rest pose (the paw meets the ground, never below), Chest only lifts, and Neck + Head stay within 12
    # degrees so the Mole's low snout keeps above the ground.
    def strokes(starts, peak):
        keys = [(0, 0)]
        for s in starts:
            keys += [(s, 0), (s + 4, peak[0]), (s + 9, peak[1]), (s + 10, 0)]
        return keys + [(60, 0)]
    t = {("Neck", "rx"): [(0, 0), (6, 5), (44, 5), (48, 6), (56, 2), (60, 0)],
         ("Head", "rx"): [(0, 0), (6, 3), (44, 3), (47, 6), (49, 3), (51, 6), (53, 3), (56, 1), (60, 0)],
         ("Head", "rz"): [(0, 0), (10, 4), (20, -4), (30, 4), (40, -4), (46, 0), (60, 0)],
         ("Chest", "rx"): [(0, 0), (6, 2), (46, 2), (56, 0), (60, 0)],
         ("Pelvis", "rz"): [(0, 0), (11, 1.5), (21, -1.5), (31, 1.5), (41, -1.5), (48, 0), (60, 0)],
         ("Tail_01", "rz"): [(0, 0), (20, 6), (40, -6), (60, 0)]}
    for side, starts in (("L", (6, 26)), ("R", (16, 36))):
        t[(f"FrontLeg_{side}_Upper", "rx")] = strokes(starts, (22, 2))
        t[(f"FrontLeg_{side}_Lower", "rx")] = strokes(starts, (-14, -2))
        t[(f"FrontPaw_{side}", "rx")] = strokes(starts, (14, 4))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, 8), (44, 8), (50, 4), (60, 0)]))
    c["AN_SmallQuadruped_MoleDig"] = (60, False, t)

    # ---------------- PeekOut (T49 photo pose pose.peek_out « Sortir du terrier »): lowers the muzzle towards the ground,
    # sniffs, pops back up looking a little higher, then a quick little shake (head turns and tilts, ears flap) and
    # settles. 1.8 s one-shot. No body point below the ground on any species: only Neck and Head lower (9 and 8
    # degrees at most, checked on the Mole's snout), Chest only lifts, Root only rises, the legs stay at rest.
    t = {("Neck", "rx"): [(0, 0), (12, 9), (22, 9), (30, -4), (36, -4), (46, 0), (54, 0)],
         ("Head", "rx"): [(0, 0), (12, 7), (15, 8), (18, 6), (21, 7.5), (24, 7), (30, -10), (34, -8), (44, -2), (50, 0), (54, 0)],
         ("Head", "ry"): [(0, 0), (32, 0), (35, 12), (38, -12), (41, 9), (44, -7), (47, 3), (50, 0), (54, 0)],
         ("Head", "rz"): [(0, 0), (32, 0), (35, -5), (38, 5), (41, -4), (44, 3), (48, 0), (54, 0)],
         ("Chest", "rx"): [(0, 0), (12, 2), (24, 2), (30, 5), (40, 3), (50, 0), (54, 0)],
         ("Root", "lz"): [(0, 0), (26, 0), (30, 0.006), (36, 0.004), (44, 0), (54, 0)],
         ("Tail_01", "rx"): [(0, 0), (30, 10), (36, -4), (44, 0), (54, 0)]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (12, 10), (24, 10), (30, -8), (36, -4), (40, 6), (44, -4), (48, 0), (54, 0)]))
    t.update(both("Ear_{}_01", "rz", [(0, 0), (32, 0), (35, -6), (38, 6), (41, -4), (44, 0), (54, 0)], -1))
    c["AN_SmallQuadruped_PeekOut"] = (54, False, t)

    # ---------------- MoleEat (T49): the Mole's Eat_Loop. The family Eat (Neck 22, Head 24, Root down 6 mm) swings a
    # low pointed snout back under the chin and below the ground; here the head only dips a little and nibbles, the
    # chest lifts 2 degrees. 2 s loop, same length as Eat_Loop.
    nib = [(0, 4), (4, 7), (8, 4), (12, 7), (16, 4), (26, 4), (30, 7), (34, 4), (38, 7), (42, 4), (60, 4)]
    t = {("Neck", "rx"): [(0, 4), (60, 4)], ("Head", "rx"): nib, ("Head", "rz"): [(0, 0), (30, 3), (60, 0)],
         ("Chest", "rx"): [(0, 2), (30, 3), (60, 2)]}
    t.update(both("Ear_{}_01", "rx", [(0, 8), (30, 11), (60, 8)]))
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 6), (60, 6)]))
    c["AN_SmallQuadruped_MoleEat"] = (60, True, t)

    # ---------------- MoleWalk (T49): the Mole's low shuffle (its Move clip). The family Walk swings each leg 16 degrees
    # back, which sinks the big mitten hands 2 cm into the ground; here every paw only steps forward and plants back on
    # its rest pose, in diagonal pairs, with a 5 mm bob and a soft sway. 0.8 s loop, same length as Locomotion_Walk.
    def step(at, amp):
        return [(0, 0), (at, 0), (at + 6, amp), (at + 12, 0), (24, 0)] if at else [(0, 0), (6, amp), (12, 0), (24, 0)]
    t = {("FrontLeg_L_Upper", "rx"): step(0, 14), ("FrontLeg_R_Upper", "rx"): step(12, 14),
         ("FrontLeg_L_Lower", "rx"): step(0, -10), ("FrontLeg_R_Lower", "rx"): step(12, -10),
         ("FrontPaw_L", "rx"): step(0, 8), ("FrontPaw_R", "rx"): step(12, 8),
         ("HindLeg_R_Upper", "rx"): step(0, 8), ("HindLeg_L_Upper", "rx"): step(12, 8),
         ("HindLeg_R_Lower", "rx"): step(0, 6), ("HindLeg_L_Lower", "rx"): step(12, 6),
         ("Root", "lz"): [(0, 0), (6, 0.005), (12, 0), (18, 0.005), (24, 0)],
         ("Chest", "rz"): [(0, 0), (6, 2.5), (12, 0), (18, -2.5), (24, 0)],
         ("Pelvis", "rz"): [(0, 0), (6, -2), (12, 0), (18, 2), (24, 0)],
         ("Head", "rz"): [(0, 0), (6, -1.5), (12, 0), (18, 1.5), (24, 0)],
         ("Tail_01", "rz"): [(0, 0), (6, 4), (12, 0), (18, -4), (24, 0)]}
    c["AN_SmallQuadruped_MoleWalk"] = (24, True, t)

    # ---------------- MoleSleep_Enter / _Loop / _Exit (T49): the Mole's sleep. The family SLEEP lowers the Root 3 cm and
    # drops the head, which sinks the low snout 6.7 cm into the ground. Here the Mole curls a little: the big mitten hands
    # come forward and in beside the chin, the head lowers and turns to rest on them, the hind legs splay so the belly
    # settles on the ground (Root down 1 mm only). Same lengths as the family Sleep clips.
    t = {}
    for k, v in MOLE_SLEEP.items():
        if k[0].startswith("FrontLeg"):  # the hands come in first...
            t[k] = [(0, 0), (8, v * 0.6), (16, v), (24, v)]
        elif k[0] in ("Neck", "Head") and k[1] == "rx":  # ...then the head lowers onto them
            t[k] = [(0, 0), (8, v * 0.2), (18, v * 0.85), (24, v)]
        else:
            t[k] = [(0, 0), (10, v * 0.7), (24, v)]
    c["AN_SmallQuadruped_MoleSleep_Enter"] = (24, False, t)

    t = hold(MOLE_SLEEP, 120)
    t[("Root", "lz")] = [(0, -0.001), (60, -0.0004), (120, -0.001)]
    t[("Chest", "rx")] = [(0, 0), (60, 0.8), (120, 0)]
    t[("Head", "rx")] = [(0, 5), (60, 5.6), (120, 5)]
    t[("Ear_L_01", "rx")] = [(0, -20), (80, -20), (83, -15), (88, -20), (120, -20)]
    t[("FrontPaw_R", "rx")] = [(0, 0), (92, 0), (95, 6), (99, 0), (120, 0)]  # a little dream twitch, the hand only rises
    c["AN_SmallQuadruped_MoleSleep_Loop"] = (120, True, t)

    t = {k: [(0, v), (10, 0), (18, 0)] for k, v in MOLE_SLEEP.items()}
    t[("Root", "lz")] = [(0, -0.001), (8, 0.003), (18, 0)]
    t[("Chest", "rx")] = [(0, 0), (8, 6), (13, 2), (18, 0)]
    t[("Neck", "rx")] = [(0, 6), (7, -4), (12, -1), (18, 0)]
    t[("Head", "rx")] = [(0, 5), (7, -8), (12, -2), (18, 0)]
    for side in ("L", "R"):
        t[(f"FrontLeg_{side}_Upper", "rx")] = [(0, 12), (6, 18), (14, 0), (18, 0)]
        t[(f"FrontLeg_{side}_Lower", "rx")] = [(0, -8), (6, -14), (14, 0), (18, 0)]
        t[(f"Ear_{side}_01", "rx")] = [(0, -20), (6, -4), (10, 4), (14, -1), (18, 0)]
    c["AN_SmallQuadruped_MoleSleep_Exit"] = (18, False, t)

    # ---------------- MoleReact (T49): the Mole's ReactTouch. The family clip compacts (Root down 1 cm, chest down) and
    # sinks the mittens 2 cm; here a small startled lean back: the front lifts, the head rises with a curious tilt and
    # both mitten hands come up. Nothing dips: Root only rises, every rotation lifts. 0.6 s, same length as ReactTouch.
    t = {("Root", "lz"): [(0, 0), (3, 0.003), (9, 0.002), (18, 0)],
         ("Pelvis", "rx"): [(0, 0), (3, 3), (10, 2), (18, 0)],
         ("Chest", "rx"): [(0, 0), (3, 5), (9, 4), (18, 0)],
         ("Neck", "rx"): [(0, 0), (3, -6), (10, -3), (18, 0)],
         ("Head", "rx"): [(0, 0), (3, -8), (7, -10), (12, -6), (18, 0)],
         ("Head", "rz"): [(0, 0), (5, 0), (8, 8), (13, 6), (18, 0)],
         ("Tail_01", "rx"): [(0, 0), (4, 10), (10, 0), (18, 0)]}
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (3, 22), (10, 18), (18, 0)]))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 0), (3, -10), (10, -8), (18, 0)]))
    t.update(both("FrontPaw_{}", "rx", [(0, 0), (3, 12), (6, 16), (10, 10), (18, 0)]))
    t.update(both("Ear_{}_01", "rx", [(0, 0), (3, 8), (7, -14), (12, -6), (18, 0)]))
    c["AN_SmallQuadruped_MoleReact"] = (18, False, t)

    # ---------------- MoleHappy (T49): the Mole's Happy. A happy wiggle: the front lifts a little, the mitten hands wave
    # in turn, the chest sways and the head bobs and tilts. The hind feet stay planted (Root and Pelvis keep still, the
    # sway rides the Spine and Chest). 1 s, same length as Happy.
    wave_hi = [(0, 0), (6, 20), (12, 4), (18, 20), (24, 4), (28, 0), (30, 0)]
    wave_lo = [(0, 0), (6, 4), (12, 20), (18, 4), (24, 20), (28, 0), (30, 0)]
    t = {("Chest", "rx"): [(0, 0), (4, 3), (26, 3), (30, 0)],
         ("Chest", "rz"): [(0, 0), (6, 3), (12, -3), (18, 3), (24, -3), (28, 0), (30, 0)],
         ("Spine", "rz"): [(0, 0), (6, -1.5), (12, 1.5), (18, -1.5), (24, 1.5), (28, 0), (30, 0)],
         ("Head", "rx"): [(0, 0), (4, -5), (9, -2), (15, -5), (21, -2), (26, -4), (30, 0)],
         ("Head", "rz"): [(0, 0), (6, 6), (12, -6), (18, 6), (24, -6), (28, 0), (30, 0)],
         ("Tail_01", "rz"): [(0, 0), (5, 8), (10, -8), (15, 8), (20, -8), (25, 4), (30, 0)],
         ("FrontLeg_L_Upper", "rx"): wave_hi, ("FrontLeg_R_Upper", "rx"): wave_lo,
         ("FrontLeg_L_Lower", "rx"): [(f, -v * 0.6) for f, v in wave_hi],
         ("FrontLeg_R_Lower", "rx"): [(f, -v * 0.6) for f, v in wave_lo],
         ("FrontPaw_L", "rx"): [(f, v * 0.7) for f, v in wave_hi],
         ("FrontPaw_R", "rx"): [(f, v * 0.7) for f, v in wave_lo]}
    t.update(both("Ear_{}_01", "rx", [(0, 0), (6, -8), (12, -4), (18, -8), (24, -4), (30, 0)]))
    c["AN_SmallQuadruped_MoleHappy"] = (30, False, t)

    # ---------------- RaccoonWash (T51, A-SP-008): sits back on its haunches, lifts both little hands in front of its chest
    # and rubs them together as if washing them (four rubs, the head bowed to watch them, a small tilt), then settles.
    # 2.4 s one-shot. The hind leg angles are solved so the hind paws stay where they rest while the pelvis tips back 30
    # degrees; the tail lifts to stay clear of the ground; the hands meet but never pass through each other or the body
    # (checked frame by frame on the Raccoon and the Red Panda).
    def hold_in(v, a=14, b=58, n=72):
        return [(0, 0), (a, v), (b, v), (n, 0)]

    def rub(base, amp, phase):
        keys = [(0, 0), (6, base * 0.1), (16, base)]
        keys += [(f, base + amp * (1 if (i + phase) % 2 == 0 else -1)) for i, f in enumerate(range(18, 54, 5))]
        return keys + [(57, base), (66, base * 0.15), (72, 0)]
    t = {("Pelvis", "rx"): hold_in(30), ("Spine", "rx"): hold_in(4), ("Chest", "rx"): hold_in(6),
         ("Neck", "rx"): hold_in(22),
         ("Head", "rx"): [(0, 0), (14, 22), (24, 25), (34, 22), (44, 25), (54, 22), (58, 22), (72, 0)],
         ("Head", "rz"): [(0, 0), (16, 6), (56, 6), (72, 0)],
         ("Tail_01", "rx"): hold_in(14)}
    t.update(both("HindLeg_{}_Upper", "rx", hold_in(-31.9)))
    t.update(both("HindLeg_{}_Upper", "ry", hold_in(5.4), -1))
    t.update(both("HindLeg_{}_Upper", "rz", hold_in(-7.6), -1))
    t.update(both("HindLeg_{}_Lower", "rx", hold_in(0.6)))
    t.update(both("HindPaw_{}", "rx", hold_in(3.5)))
    t[("FrontLeg_L_Upper", "rx")] = rub(38, 6, 0); t[("FrontLeg_R_Upper", "rx")] = rub(38, 6, 1)
    t[("FrontLeg_L_Lower", "rx")] = rub(-48, 6, 1); t[("FrontLeg_R_Lower", "rx")] = rub(-48, 6, 0)
    t[("FrontLeg_L_Upper", "rz")] = rub(-15, 2, 0); t[("FrontLeg_R_Upper", "rz")] = [(f, -v) for f, v in rub(-15, 2, 0)]
    t[("FrontPaw_L", "rx")] = rub(18, 6, 0); t[("FrontPaw_R", "rx")] = rub(18, 6, 1)
    c["AN_SmallQuadruped_RaccoonWash"] = (72, False, t)

    # ---------------- RedPandaStand (T51, A-SP-011): rises on its hind legs to look bigger, front paws lifted and spread,
    # looks around to one side then the other, then settles back on all fours. 2.8 s one-shot. The pelvis tips back 50
    # degrees and the spine and chest straighten the torso; the hind leg angles are solved so the hind paws stay planted
    # (Root lifts 1 cm as the legs unfold); the tail lifts to stay clear of the ground.
    def stand(v):
        return [(0, 0), (16, v), (68, v), (84, 0)]
    t = {("Root", "lz"): stand(0.010), ("Pelvis", "rx"): stand(50), ("Spine", "rx"): stand(12), ("Chest", "rx"): stand(14),
         ("Neck", "rx"): stand(30), ("Head", "rx"): [(0, 0), (16, 22), (40, 24), (64, 22), (68, 22), (84, 0)],
         ("Neck", "ry"): [(0, 0), (16, 0), (28, 10), (40, 10), (52, -10), (62, -10), (70, 0), (84, 0)],
         ("Head", "ry"): [(0, 0), (16, 0), (28, 24), (40, 24), (52, -24), (62, -24), (70, 0), (84, 0)],
         ("Head", "rz"): [(0, 0), (16, 0), (28, 6), (40, 6), (52, -6), (62, -6), (70, 0), (84, 0)],
         ("Tail_01", "rx"): stand(30)}
    t.update(both("HindLeg_{}_Upper", "rx", stand(-57.9)))
    t.update(both("HindLeg_{}_Upper", "ry", stand(6.2), -1))
    t.update(both("HindLeg_{}_Upper", "rz", stand(-15.0), -1))
    t.update(both("HindLeg_{}_Lower", "rx", stand(-10.5)))
    t.update(both("HindPaw_{}", "rx", stand(-1.8)))
    # The torso already stands the front legs out forward: a small extra lift and spread raises the paws without
    # stretching the short legs (their top follows the chest).
    t.update(both("FrontLeg_{}_Upper", "rx", [(0, 0), (8, 4), (18, 16), (24, 12), (66, 14), (76, 3), (84, 0)]))
    t.update(both("FrontLeg_{}_Upper", "rz", [(0, 0), (10, 2), (18, 10), (66, 10), (76, 1), (84, 0)], -1))
    t.update(both("FrontLeg_{}_Lower", "rx", [(0, 0), (10, -6), (18, -22), (66, -22), (76, -3), (84, 0)]))
    t.update(both("FrontPaw_{}", "rx", [(0, 0), (18, 20), (24, 14), (30, 20), (66, 20), (84, 0)]))
    c["AN_SmallQuadruped_RedPandaStand"] = (84, False, t)
    for name, keys in PLANTED.items():
        c["AN_SmallQuadruped_" + name][2].update(keys)
    return c

# ---------------- Ground contact (2026-10-12, « Aucune collision »). Solved keys that replace the hand keys of these
# channels: the Root lifts where a belly or a straight leg would go below the ground, and the legs bend (Upper, Lower)
# with the paw kept flat so every paw stays on or above the ground (the Sleep clips also bring the paws down onto it).
# Solved frame by frame on the near-ground vertices of every species that plays the clip (LOD0 and LOD2, the prefab
# scale included); re-run pw_anim_ground.py whenever the body keys of one of these clips change.
PLANTED = {
    "Sleep_Enter": {
        ("FrontLeg_L_Upper", "rx"): [(0, 0), (1, -3.8), (2, -6.9), (3, -10.5), (5, -18.3), (7, -26.3), (9, -33.7), (10, -37.1), (11, -40.2), (12, -42.9), (13, -45.3), (14, -47.3), (15, -48.9), (16, -50.3), (17, -51.4), (18, -52.4), (19, -53.1), (21, -54), (22, -54.3), (24, -54.5)],
        ("FrontLeg_L_Lower", "rx"): [(0, 0), (1, 8.5), (2, 14.9), (4, 28.9), (6, 42.2), (7, 48.1), (8, 53.3), (9, 57.8), (10, 61.6), (11, 64.8), (12, 67.4), (13, 69.4), (14, 71), (15, 72.2), (16, 73.1), (18, 74.4), (20, 75.2), (22, 75.6), (24, 75.7)],
        ("FrontPaw_L", "rx"): [(0, 0), (1, -4.7), (3, -11), (4, -13.8), (5, -16.2), (6, -18.3), (7, -19.8), (8, -20.8), (9, -21.2), (10, -21.2), (11, -20.9), (12, -20.4), (16, -17.6), (18, -16.5), (21, -15.6), (24, -15.3)],
        ("FrontLeg_R_Upper", "rx"): [(0, 0), (1, -3.8), (2, -6.9), (3, -10.5), (7, -26.5), (9, -33.8), (10, -37.2), (11, -40.3), (12, -43.1), (13, -45.4), (14, -47.4), (15, -49.1), (16, -50.5), (17, -51.6), (18, -52.5), (19, -53.2), (21, -54.1), (22, -54.4), (24, -54.5)],
        ("FrontLeg_R_Lower", "rx"): [(0, 0), (1, 8.5), (2, 14.9), (5, 36.3), (6, 42.7), (7, 48.5), (8, 53.6), (9, 58.1), (10, 62), (11, 65.2), (12, 67.7), (13, 69.8), (14, 71.3), (15, 72.5), (16, 73.5), (17, 74.2), (19, 75.1), (21, 75.6), (24, 75.8)],
        ("FrontPaw_R", "rx"): [(0, 0), (1, -4.7), (4, -14), (5, -16.6), (6, -18.6), (7, -20.1), (8, -21), (9, -21.4), (10, -21.4), (11, -21.1), (12, -20.5), (16, -17.8), (18, -16.7), (21, -15.7), (24, -15.4)],
        ("HindLeg_L_Upper", "rx"): [(0, 0), (1, 0.6), (2, 1.8), (3, 3.6), (4, 5.9), (5, 8.7), (6, 11.9), (8, 18.8), (9, 22.4), (10, 28.3), (11, 33.8), (12, 38.7), (13, 42.9), (14, 46.4), (15, 49.3), (16, 51.6), (17, 53.4), (18, 54.9), (19, 56), (20, 56.8), (21, 57.3), (22, 57.7), (24, 58)],
        ("HindLeg_L_Lower", "rx"): [(0, 0), (2, 1.5), (3, 2.6), (7, 8.1), (9, 10.4), (10, 15.1), (11, 18.7), (12, 21.3), (13, 23), (14, 24.1), (15, 24.8), (17, 25.3), (24, 25.2)],
        ("HindPaw_L", "rx"): [(0, 0), (1, 0.2), (2, -0.2), (3, -0.9), (4, -1.8), (5, -3), (6, -4.6), (7, -6.5), (9, -11), (10, -12), (11, -13.6), (15, -22.1), (16, -23.9), (17, -25.4), (18, -26.7), (19, -27.8), (20, -28.6), (21, -29.1), (22, -29.5), (24, -29.8)],
        ("HindLeg_R_Upper", "rx"): [(0, 0), (1, 0.6), (2, 1.8), (3, 3.6), (4, 5.9), (5, 8.7), (6, 11.9), (8, 18.8), (9, 22.4), (10, 28.3), (11, 33.8), (12, 38.7), (13, 42.9), (14, 46.4), (15, 49.3), (16, 51.6), (17, 53.4), (18, 54.9), (19, 56), (20, 56.8), (21, 57.3), (22, 57.7), (24, 58)],
        ("HindLeg_R_Lower", "rx"): [(0, 0), (2, 1.5), (3, 2.6), (7, 8.1), (9, 10.4), (10, 15.1), (11, 18.7), (12, 21.3), (13, 23), (14, 24.1), (15, 24.8), (17, 25.3), (24, 25.2)],
        ("HindPaw_R", "rx"): [(0, 0), (1, 0.2), (2, -0.2), (3, -0.9), (4, -1.8), (5, -3), (6, -4.6), (7, -6.5), (9, -11), (10, -12), (11, -13.6), (15, -22.1), (16, -23.9), (17, -25.4), (18, -26.7), (19, -27.8), (20, -28.6), (21, -29.1), (22, -29.5), (24, -29.8)]},
    "Sleep_Loop": {
        ("FrontLeg_L_Upper", "rx"): [(0, -54.5), (13, -54.2), (45, -52.8), (60, -52.4), (75, -52.8), (107, -54.2), (120, -54.5)],
        ("FrontLeg_L_Lower", "rx"): [(0, 75.7), (14, 75.3), (46, 72.9), (53, 72.6), (60, 72.5), (67, 72.6), (74, 72.9), (106, 75.3), (120, 75.7)],
        ("FrontPaw_L", "rx"): [(0, -15.3), (60, -15.6), (120, -15.3)],
        ("FrontLeg_R_Upper", "rx"): [(0, -54.5), (13, -54.3), (46, -52.8), (60, -52.5), (74, -52.8), (107, -54.3), (120, -54.5)],
        ("FrontLeg_R_Lower", "rx"): [(0, 75.8), (13, 75.5), (46, 73.1), (54, 72.7), (60, 72.6), (66, 72.7), (74, 73.1), (107, 75.5), (120, 75.8)],
        ("FrontPaw_R", "rx"): [(0, -15.4), (60, -15.7), (120, -15.4)],
        ("HindLeg_L_Upper", "rx"): [(0, 58), (6, 57.8), (13, 57.3), (21, 56.4), (45, 53.3), (53, 52.7), (60, 52.5), (67, 52.7), (75, 53.3), (99, 56.4), (107, 57.3), (114, 57.8), (120, 58)],
        ("HindLeg_L_Lower", "rx"): [(0, 25.2), (7, 25.1), (14, 24.6), (22, 23.7), (46, 20.8), (53, 20.2), (60, 20), (67, 20.2), (74, 20.8), (98, 23.7), (106, 24.6), (113, 25.1), (120, 25.2)],
        ("HindPaw_L", "rx"): [(0, -29.8), (120, -29.8)],
        ("HindLeg_R_Upper", "rx"): [(0, 58), (6, 57.8), (13, 57.3), (21, 56.4), (45, 53.3), (53, 52.7), (60, 52.5), (67, 52.7), (75, 53.3), (99, 56.4), (107, 57.3), (114, 57.8), (120, 58)],
        ("HindLeg_R_Lower", "rx"): [(0, 25.2), (7, 25.1), (14, 24.6), (22, 23.7), (46, 20.8), (53, 20.2), (60, 20), (67, 20.2), (74, 20.8), (98, 23.7), (106, 24.6), (113, 25.1), (120, 25.2)],
        ("HindPaw_R", "rx"): [(0, -29.8), (120, -29.8)]},
    "Sleep_Exit": {
        ("FrontLeg_L_Upper", "rx"): [(0, -54.5), (1, -53), (2, -49.1), (3, -43.1), (4, -35.2), (5, -25), (6, -14), (8, -7.3), (9, -4.4), (10, -2.1), (11, -0.6), (12, 0), (18, 0)],
        ("FrontLeg_L_Lower", "rx"): [(0, 75.7), (1, 74.1), (2, 69.4), (3, 61.2), (4, 48.9), (5, 30.4), (6, 10), (8, 5.2), (9, 3.1), (10, 1.5), (11, 0.4), (12, 0), (18, 0)],
        ("FrontPaw_L", "rx"): [(0, -15.3), (1, -15.7), (2, -16.4), (3, -16.3), (4, -14.2), (5, -8.4), (6, -1), (7, -3.4), (8, -4.9), (9, -5.4), (10, -5.2), (11, -4.4), (13, -2), (14, -1.1), (15, -0.5), (16, -0.2), (17, 0), (18, 0)],
        ("FrontLeg_R_Upper", "rx"): [(0, -54.5), (1, -53.1), (2, -49.2), (3, -43.3), (4, -35.3), (5, -25), (6, -14), (8, -7.3), (9, -4.4), (10, -2.1), (11, -0.6), (12, 0), (18, 0)],
        ("FrontLeg_R_Lower", "rx"): [(0, 75.8), (1, 74.3), (2, 69.7), (3, 61.6), (4, 49.1), (5, 30.4), (6, 10), (8, 5.2), (9, 3.1), (10, 1.5), (11, 0.4), (12, 0), (18, 0)],
        ("FrontPaw_R", "rx"): [(0, -15.4), (1, -15.8), (2, -16.6), (3, -16.5), (4, -14.3), (5, -8.4), (6, -1), (7, -3.4), (8, -4.9), (9, -5.4), (10, -5.2), (11, -4.4), (13, -2), (14, -1.1), (15, -0.5), (16, -0.2), (17, 0), (18, 0)],
        ("HindLeg_L_Upper", "rx"): [(0, 58), (1, 54.8), (2, 46.4), (3, 34.2), (4, 27.6), (6, 15.4), (7, 10.1), (8, 6.1), (9, 3), (10, 0.5), (11, -1), (12, -1.3), (16, 0), (18, 0)],
        ("HindLeg_L_Lower", "rx"): [(0, 25.2), (1, 23.4), (2, 17.5), (3, 6.9), (4, 3.2), (5, 0.4), (6, -2.4), (7, -4.5), (8, -5), (10, -4.2), (11, -3.7), (12, -3.1), (16, 0), (18, 0)],
        ("HindPaw_L", "rx"): [(0, -29.8), (1, -28.7), (2, -26.6), (3, -25.8), (4, -23.2), (7, -14.2), (9, -7.4), (10, -4.7), (11, -2.8), (12, -1.8), (16, 0), (18, 0)],
        ("HindLeg_R_Upper", "rx"): [(0, 58), (1, 54.8), (2, 46.4), (3, 34.3), (4, 27.6), (6, 15.4), (7, 10.1), (8, 6.1), (9, 3), (10, 0.5), (11, -1), (12, -1.3), (16, 0), (18, 0)],
        ("HindLeg_R_Lower", "rx"): [(0, 25.2), (1, 23.4), (2, 17.5), (3, 7), (4, 3.2), (6, -2.4), (7, -4.5), (8, -5), (10, -4.2), (11, -3.7), (12, -3.1), (16, 0), (18, 0)],
        ("HindPaw_R", "rx"): [(0, -29.8), (1, -28.7), (2, -26.6), (3, -25.7), (4, -23.2), (7, -14.2), (9, -7.4), (10, -4.7), (11, -2.8), (12, -1.8), (16, 0), (18, 0)]},
    "LowSleep_Enter": {
        ("Root", "lz"): [(0, 0.0002), (24, 0.0002)]},
    "LowSleep_Loop": {
        ("Root", "lz"): [(0, 0.0002), (120, 0.0002)]},
    "LowSleep_Exit": {
        ("Root", "lz"): [(0, 0.0002), (1, 0.0004), (2, 0.0008), (3, 0.0013), (5, 0.0027), (6, 0.0034), (7, 0.0038), (8, 0.004), (9, 0.0039), (10, 0.0036), (11, 0.0031), (14, 0.0014), (16, 0.0006), (17, 0.0003), (18, 0.0002)],
        ("FrontPaw_L", "rx"): [(0, 0), (1, 0), (3, -2.2), (5, -4.8), (6, -5.9), (7, -6.7), (8, -7), (9, -6.6), (10, -5.7), (13, -2), (14, -1.1), (15, -0.5), (16, -0.2), (17, 0), (18, 0)],
        ("FrontPaw_R", "rx"): [(0, 0), (1, 0), (3, -2.2), (5, -4.8), (6, -5.9), (7, -6.7), (8, -7), (9, -6.6), (10, -5.7), (13, -2), (14, -1.1), (15, -0.5), (16, -0.2), (17, 0), (18, 0)],
        ("HindLeg_L_Upper", "rx"): [(0, 0), (2, -0.2), (6, -1.4), (8, -1.7), (10, -1.5), (16, 0), (18, 0)],
        ("HindLeg_L_Lower", "rx"): [(0, 0), (1, 0), (2, -0.4), (3, -0.9), (6, -3.2), (7, -3.6), (8, -3.8), (10, -3.4), (16, 0), (18, 0)],
        ("HindPaw_L", "rx"): [(0, 0), (1, 0), (2, -0.2), (3, -0.5), (6, -1.8), (8, -2.2), (10, -2), (16, 0), (18, 0)],
        ("HindLeg_R_Upper", "rx"): [(0, 0), (2, -0.2), (6, -1.4), (8, -1.7), (10, -1.5), (16, 0), (18, 0)],
        ("HindLeg_R_Lower", "rx"): [(0, 0), (1, 0), (2, -0.4), (3, -0.9), (6, -3.2), (7, -3.6), (8, -3.8), (10, -3.4), (16, 0), (18, 0)],
        ("HindPaw_R", "rx"): [(0, 0), (1, 0), (2, -0.2), (3, -0.5), (6, -1.8), (8, -2.2), (10, -2), (16, 0), (18, 0)]},
    "ReactTouch": {
        ("Root", "lz"): [(0, 0.0115), (1, 0.0098), (2, 0.0055), (3, 0.0032), (4, 0.0034), (5, 0.0037), (7, 0.0046), (8, 0.0049), (9, 0.0051), (10, 0.0052), (11, 0.0052), (12, 0.005), (13, 0.0046), (14, 0.0039), (16, 0.0024), (17, 0.0018), (18, 0.0013)],
        ("FrontLeg_L_Upper", "rx"): [(0, 0), (2, 0), (3, -5.6), (4, -4.9), (5, -3), (6, 0), (18, 0)],
        ("FrontLeg_L_Lower", "rx"): [(0, 0), (2, 0), (3, 13.1), (4, 11.6), (5, 7.1), (6, 0), (18, 0)],
        ("FrontPaw_L", "rx"): [(0, 0), (1, 0), (2, 1.5), (3, -1.6), (4, -0.8), (5, 1.4), (6, 4.4), (8, 0.9), (9, 0), (18, 0)],
        ("FrontLeg_R_Upper", "rx"): [(0, 0), (2, 0), (3, -5.6), (4, -4.9), (5, -3), (6, 0), (18, 0)],
        ("FrontLeg_R_Lower", "rx"): [(0, 0), (2, 0), (3, 13.1), (4, 11.6), (5, 7.1), (6, 0), (18, 0)],
        ("FrontPaw_R", "rx"): [(0, 0), (1, 0), (2, 1.5), (3, -1.6), (4, -0.8), (5, 1.4), (6, 4.4), (8, 0.9), (9, 0), (18, 0)]},
    "Locomotion_Hop": {
        ("Root", "lz"): [(0, 0.0027), (1, 0.003), (2, 0.0026), (3, 0.0044), (4, 0.0112), (5, 0.0226), (6, 0.035), (7, 0.0454), (8, 0.0524), (9, 0.0553), (10, 0.0529), (11, 0.0457), (12, 0.0347), (13, 0.0218), (14, 0.011), (15, 0.0056), (17, 0.0004), (18, -0.001), (19, 0.0005), (20, 0.0027)],
        ("FrontLeg_L_Upper", "rx"): [(0, -12), (1, -13.6), (2, -15.6), (3, -17.3), (4, -18), (5, -14.3), (6, -5.4), (7, 5.4), (8, 15), (9, 20.8), (10, 23.4), (11, 24), (12, 17.8), (13, 6), (14, -16.7), (15, -22.5), (16, -21.7), (17, -21.2), (18, -19.6), (19, -11.3), (20, -12)],
        ("FrontLeg_L_Lower", "rx"): [(0, 6), (1, 5.7), (2, 4.9), (3, 3.7), (4, 2.1), (5, 0.2), (9, -8), (10, -9.4), (11, -10), (12, -3.2), (13, 4), (14, 37.7), (15, 40.1), (16, 33), (17, 29.9), (18, 25.9), (19, 6), (20, 6)],
        ("FrontPaw_L", "rx"): [(0, 0), (1, 0.3), (2, 1.3), (3, 2.6), (6, 7.4), (7, 8.7), (8, 9.7), (9, 10), (10, 5.9), (11, -1.9), (12, -6), (13, 3), (14, -9.6), (15, -9.6), (16, -6.4), (17, -5.5), (18, -3.9), (19, 6.3), (20, 0)],
        ("FrontLeg_R_Upper", "rx"): [(0, -12), (1, -13.6), (2, -15.6), (3, -17.3), (4, -18), (5, -14.3), (6, -5.4), (7, 5.4), (8, 15), (9, 20.8), (10, 23.4), (11, 24), (12, 17.8), (13, 6), (14, -16.7), (15, -22.5), (16, -21.7), (17, -21.2), (18, -19.6), (19, -11.3), (20, -12)],
        ("FrontLeg_R_Lower", "rx"): [(0, 6), (1, 5.7), (2, 4.9), (3, 3.7), (4, 2.1), (5, 0.2), (9, -8), (10, -9.4), (11, -10), (12, -3.2), (13, 4), (14, 37.7), (15, 40.1), (16, 33), (17, 29.9), (18, 25.8), (19, 6), (20, 6)],
        ("FrontPaw_R", "rx"): [(0, 0), (1, 0.3), (2, 1.3), (3, 2.6), (6, 7.4), (7, 8.7), (8, 9.7), (9, 10), (10, 5.9), (11, -1.9), (12, -6), (13, 3), (14, -9.6), (15, -9.6), (16, -6.4), (17, -5.4), (18, -3.9), (19, 6.3), (20, 0)],
        ("HindLeg_L_Upper", "rx"): [(0, 18), (1, 16.9), (2, 15.2), (3, 10), (4, -0.8), (5, -12.5), (6, -18), (7, -17.3), (9, -14), (10, -12.6), (11, -10.7), (12, -7.5), (13, -2), (14, 6.2), (15, 14.5), (16, 20), (17, 23.2), (18, 23.8), (19, 20.4), (20, 18)],
        ("HindLeg_L_Lower", "rx"): [(0, -8), (1, -8.4), (4, -11.6), (5, -12), (7, 10), (8, 9.3), (10, 6), (12, 4.1), (13, 2.7), (14, 0), (16, -8.2), (17, -7.7), (18, -6.7), (19, -8.5), (20, -8)],
        ("HindPaw_L", "rx"): [(0, 0), (2, 0.3), (3, 0.9), (4, 2), (5, 4), (6, 11.6), (7, 20), (8, 22.8), (9, 23.9), (10, 24), (11, 22), (12, 17.4), (13, 12), (14, 7.5), (15, 4.1), (16, 2), (17, -29.2), (18, -29.6), (19, 0), (20, 0)],
        ("HindLeg_R_Upper", "rx"): [(0, 18), (1, 16.9), (2, 15.2), (3, 10), (4, -0.8), (5, -12.5), (6, -18), (7, -17.3), (9, -14), (10, -12.6), (11, -10.7), (12, -7.5), (13, -2), (14, 6.2), (15, 14.5), (16, 20), (17, 23.2), (18, 23.8), (19, 20.4), (20, 18)],
        ("HindLeg_R_Lower", "rx"): [(0, -8), (1, -8.4), (4, -11.6), (5, -12), (7, 10), (8, 9.3), (10, 6), (12, 4.1), (13, 2.7), (14, 0), (16, -8.2), (17, -7.7), (18, -6.7), (19, -8.5), (20, -8)],
        ("HindPaw_R", "rx"): [(0, 0), (2, 0.3), (3, 0.9), (4, 2), (5, 4), (6, 11.6), (7, 20), (8, 22.8), (9, 23.9), (10, 24), (11, 22), (12, 17.4), (13, 12), (14, 7.5), (15, 4.1), (16, 2), (17, -29.2), (18, -29.6), (19, 0), (20, 0)]},
    "Locomotion_Walk": {
        ("Root", "lz"): [(0, 0.0035), (1, 0.004), (3, 0.006), (4, 0.0067), (5, 0.0071), (6, 0.0073), (7, 0.0071), (8, 0.0066), (9, 0.0059), (11, 0.004), (12, 0.0035), (13, 0.004), (15, 0.006), (16, 0.0067), (17, 0.0071), (18, 0.0073), (19, 0.0071), (20, 0.0066), (21, 0.0059), (23, 0.004), (24, 0.0035)],
        ("FrontPaw_L", "rx"): [(0, 0), (7, 0), (8, 2.7), (9, 7.3), (10, 11), (11, 13.7), (12, 14.5), (13, 13.8), (14, 12.7), (15, 11.3), (16, 9.5), (17, 6.8), (18, 2.4), (19, 0), (24, 0)],
        ("FrontPaw_R", "rx"): [(0, 14.5), (1, 13.8), (2, 12.7), (3, 11.3), (4, 9.5), (5, 6.8), (6, 2.4), (7, 0), (19, 0), (20, 2.7), (21, 7.2), (22, 11), (23, 13.7), (24, 14.5)],
        ("HindPaw_L", "rx"): [(0, 8.7), (1, 7.8), (2, 5.8), (3, 3.6), (4, 1), (5, 0), (19, 0), (20, 0.6), (22, 5.7), (23, 7.6), (24, 8.7)],
        ("HindPaw_R", "rx"): [(0, 0), (7, 0), (8, 0.6), (9, 3.2), (11, 7.6), (12, 8.7), (13, 7.8), (14, 5.6), (16, 0.7), (17, 0), (24, 0)]},
    "Happy": {
        ("Root", "lz"): [(0, 0.005), (1, 0.0031), (3, -0.0051), (4, -0.007), (5, 0.0013), (6, 0.0206), (7, 0.0425), (8, 0.0606), (9, 0.0738), (10, 0.0821), (11, 0.085), (12, 0.0802), (13, 0.0682), (14, 0.0517), (16, 0.0151), (17, 0.0009), (18, -0.005), (19, -0.0009), (20, 0.0063), (21, 0.0093), (22, 0.0068), (23, 0.0037), (24, 0.0011), (25, 0.0003), (27, 0.0004), (30, 0.0004)],
        ("FrontLeg_L_Upper", "rx"): [(0, 0), (1, -1.6), (2, -5), (3, -11.8), (4, -15.6), (5, -6.7), (6, 1.3), (7, 10.7), (8, 18.7), (9, 22), (10, 21.9), (11, 21.5), (12, 20.3), (13, 18), (14, 12.2), (15, 3.5), (16, -4.5), (17, -8), (18, -16), (19, -5.9), (21, -2.1), (22, -0.6), (23, 0), (30, 0)],
        ("FrontLeg_L_Lower", "rx"): [(0, 0), (2, 0), (3, 7.7), (4, 13.1), (5, 0), (17, 0), (18, 20.1), (19, 0), (30, 0)],
        ("FrontPaw_L", "rx"): [(0, 0), (1, 0), (2, 2.5), (3, 5.7), (4, 4.5), (5, 0), (16, 0), (17, 3.7), (18, -0.8), (19, 2.5), (20, 0), (30, 0)],
        ("FrontLeg_R_Upper", "rx"): [(0, 0), (1, -1.6), (2, -5), (3, -11.7), (4, -15.6), (5, -6.7), (6, 1.3), (7, 10.7), (8, 18.7), (9, 22), (10, 21.9), (11, 21.5), (12, 20.3), (13, 18), (14, 12.2), (15, 3.5), (16, -4.5), (17, -8), (18, -15.9), (19, -5.9), (21, -2.1), (22, -0.6), (23, 0), (30, 0)],
        ("FrontLeg_R_Lower", "rx"): [(0, 0), (2, 0), (3, 7.7), (4, 13), (5, 0), (17, 0), (18, 20), (19, 0), (30, 0)],
        ("FrontPaw_R", "rx"): [(0, 0), (1, 0), (2, 2.4), (3, 5.7), (4, 4.5), (5, 0), (16, 0), (17, 3.9), (18, -0.8), (19, 2.4), (20, 0), (30, 0)],
        ("HindLeg_L_Upper", "rx"): [(0, 0), (1, 3.4), (2, 11), (3, 23.2), (4, 26.5), (5, 15.7), (7, -11.7), (8, -18), (9, -17.8), (10, -16.7), (11, -14.3), (12, -10), (13, -3.6), (14, 3.7), (15, 10.7), (16, 16), (17, 18), (18, 22.1), (19, 14), (21, 6.7), (22, 4), (23, 2.3), (24, 1.2), (25, 0.5), (26, 0.1), (27, 0), (30, 0)],
        ("HindLeg_L_Lower", "rx"): [(0, 0), (1, -1.6), (2, -5), (3, -1.4), (4, -3.5), (5, -7.2), (7, 5.2), (8, 8), (9, 7.8), (10, 7.2), (11, 6), (12, 4), (13, 1.2), (14, -2), (15, -4.9), (16, -7.1), (17, -8), (18, 1), (19, -5.2), (20, -2.8), (21, -0.8), (22, 0), (30, 0)],
        ("HindPaw_L", "rx"): [(0, 0), (2, 0.1), (3, -26.9), (4, -32.8), (5, 2), (6, 7.4), (7, 16.4), (8, 22), (9, 23.2), (10, 23.8), (12, 24), (13, 21.2), (14, 14.8), (15, 8), (16, 3.3), (17, 0.7), (18, -23), (19, 0), (30, 0)],
        ("HindLeg_R_Upper", "rx"): [(0, 0), (1, 3.4), (2, 11), (3, 23.2), (4, 26.5), (5, 15.7), (7, -11.7), (8, -18), (9, -17.8), (10, -16.7), (11, -14.3), (12, -10), (13, -3.6), (14, 3.7), (15, 10.7), (16, 16), (17, 18), (18, 22.1), (19, 14), (21, 6.7), (22, 4), (23, 2.3), (24, 1.2), (25, 0.5), (26, 0.1), (27, 0), (30, 0)],
        ("HindLeg_R_Lower", "rx"): [(0, 0), (1, -1.6), (2, -5), (3, -1.4), (4, -3.5), (5, -7.2), (7, 5.2), (8, 8), (9, 7.8), (10, 7.2), (11, 6), (12, 4), (13, 1.2), (14, -2), (15, -4.9), (16, -7.1), (17, -8), (18, 1), (19, -5.2), (20, -2.8), (21, -0.8), (22, 0), (30, 0)],
        ("HindPaw_R", "rx"): [(0, 0), (2, 0.1), (3, -26.9), (4, -32.8), (5, 2), (6, 7.4), (7, 16.4), (8, 22), (9, 23.2), (10, 23.8), (12, 24), (13, 21.2), (14, 14.8), (15, 8), (16, 3.3), (17, 0.7), (18, -23), (19, 0), (30, 0)]},
    "Happy_Bounce": {
        ("Root", "lz"): [(0, 0.003), (1, 0.0009), (2, -0.0029), (3, -0.005), (4, 0), (5, 0.0112), (6, 0.0232), (7, 0.031), (8, 0.0339), (9, 0.0348), (10, 0.0353), (11, 0.0289), (12, 0.0151), (13, 0.0027), (14, -0.0022), (15, -0.003), (16, 0.001), (17, 0.0097), (18, 0.0185), (19, 0.023), (20, 0.0237), (21, 0.0239), (22, 0.0244), (23, 0.0199), (24, 0.0103), (25, 0.0027), (26, 0.0004), (27, 0), (28, 0.0008), (29, 0.0022), (30, 0.0029)],
        ("HindLeg_L_Upper", "rx"): [(0, 0), (1, -0.3), (2, 0.4), (3, 0.3), (4, -3.5), (5, -5.5), (6, -6.8), (7, -7.7), (8, -8), (9, -7.2), (10, -5.2), (11, -2.8), (12, -0.8), (13, 0), (14, 1.1), (15, 1.6), (16, -2.4), (18, -4.8), (19, -5.7), (20, -6), (21, -5.4), (24, -0.6), (25, 0), (30, 0)],
        ("HindLeg_L_Lower", "rx"): [(0, 0), (1, 0), (2, 3.8), (3, 6.7), (4, 1.2), (5, 0), (13, 0), (15, 6.4), (16, 0), (30, 0)],
        ("HindPaw_L", "rx"): [(0, 0), (1, 0), (2, 1.2), (3, 3.4), (4, 2.2), (5, 0), (13, 0), (14, -0.6), (15, 2.9), (16, 0), (30, 0)],
        ("HindLeg_R_Upper", "rx"): [(0, 0), (1, -0.3), (2, 0.4), (3, 0.3), (4, -3.5), (5, -5.5), (6, -6.8), (7, -7.7), (8, -8), (9, -7.2), (10, -5.2), (11, -2.8), (12, -0.8), (13, 0), (14, 1.1), (15, 1.6), (16, -2.4), (18, -4.8), (19, -5.7), (20, -6), (21, -5.4), (24, -0.6), (25, 0), (30, 0)],
        ("HindLeg_R_Lower", "rx"): [(0, 0), (1, 0), (2, 3.8), (3, 6.7), (4, 1.2), (5, 0), (13, 0), (15, 6.5), (16, 0), (30, 0)],
        ("HindPaw_R", "rx"): [(0, 0), (1, 0), (2, 1.2), (3, 3.4), (4, 2.2), (5, 0), (13, 0), (14, -0.6), (15, 2.9), (16, 0), (30, 0)]},
    "Eat_Loop": {
        ("Root", "lz"): [(0, 0.0013), (6, 0.0013), (15, 0.001), (30, 0.0009), (45, 0.001), (54, 0.0013), (60, 0.0013)]},
    "Social_Nose": {
        ("Root", "lz"): [(0, 0.0009), (1, 0.0012), (2, 0.0016), (5, 0.0029), (6, 0.0032), (7, 0.0034), (8, 0.0036), (10, 0.0036), (26, 0.0036), (29, 0.0035), (31, 0.0031), (33, 0.0025), (37, 0.0011), (38, 0.0009), (39, 0.0007)]},
}


def build_all():
    made = []
    for name, (frames, loop, tracks) in clips().items():
        build(name, frames, loop, tracks)
        made.append(name)
    ARM.animation_data.action = None
    for pb in ARM.pose.bones:
        pb.rotation_euler = (0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
    return made
