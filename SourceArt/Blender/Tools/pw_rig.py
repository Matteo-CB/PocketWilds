"""Family skeletons from a bone table (T46). Source of BL_Rig_<Family>.blend.

The SmallQuadruped skeleton (T06) was placed by hand; MediumQuadruped and the next families are built here so the rig
file always has its recipe. Same logical bone names as SmallQuadruped (doc RIGS « same logical names where sensible »)
and the same bone axes: each bone's local X points to the side the SmallQuadruped bone's X points to, so a clip value
(degrees around local X) bends every family the same way.

Usage inside Blender (a new empty file, own call), then:
    import pw_rig; pw_rig.build_rig_file("MediumQuadruped")
T51: in a species file started from the Fawn, pw_rig.fit_species("Fox") moves the bones to that species' data
(SPECIES_BONES) before the blockout; directions and rolls never change.
Blender space: the animal faces -Y, Z up, feet on z = 0.
"""
import os
import bpy
from mathutils import Matrix, Vector

# name: (parent, head, tail, x sign) — x sign: the local X axis points to +X (1) or -X (-1) of the world.
MEDIUM_QUADRUPED = {
    # Proportions of the Fawn (A-SP-006, about 72 x 62 cm with its ears): long legs, the back at 0.36 m.
    "Root": (None, (0, 0, 0), (0, 0, 0.08), 1),
    "Pelvis": ("Root", (0, 0.150, 0.340), (0, 0.070, 0.350), -1),
    "Spine": ("Pelvis", (0, 0.070, 0.350), (0, -0.020, 0.355), -1),
    "Chest": ("Spine", (0, -0.020, 0.355), (0, -0.090, 0.380), -1),
    "Neck": ("Chest", (0, -0.090, 0.380), (0, -0.130, 0.460), 1),
    "Head": ("Neck", (0, -0.130, 0.460), (0, -0.130, 0.600), 1),
    "Ear_L_01": ("Head", (0.040, -0.120, 0.530), (0.085, -0.100, 0.565), 1),
    "Ear_L_02": ("Ear_L_01", (0.085, -0.100, 0.565), (0.130, -0.080, 0.600), 1),
    "Ear_R_01": ("Head", (-0.040, -0.120, 0.530), (-0.085, -0.100, 0.565), 1),
    "Ear_R_02": ("Ear_R_01", (-0.085, -0.100, 0.565), (-0.130, -0.080, 0.600), 1),
    "FrontLeg_L_Upper": ("Chest", (0.055, -0.060, 0.310), (0.055, -0.065, 0.190), -1),
    "FrontLeg_L_Lower": ("FrontLeg_L_Upper", (0.055, -0.065, 0.190), (0.055, -0.070, 0.070), -1),
    "FrontPaw_L": ("FrontLeg_L_Lower", (0.055, -0.070, 0.070), (0.055, -0.085, 0.000), -1),
    "FrontLeg_R_Upper": ("Chest", (-0.055, -0.060, 0.310), (-0.055, -0.065, 0.190), -1),
    "FrontLeg_R_Lower": ("FrontLeg_R_Upper", (-0.055, -0.065, 0.190), (-0.055, -0.070, 0.070), -1),
    "FrontPaw_R": ("FrontLeg_R_Lower", (-0.055, -0.070, 0.070), (-0.055, -0.085, 0.000), -1),
    "Tail_01": ("Pelvis", (0, 0.190, 0.360), (0, 0.215, 0.345), 1),
    "Tail_02": ("Tail_01", (0, 0.215, 0.345), (0, 0.235, 0.325), 1),
    "Tail_03": ("Tail_02", (0, 0.235, 0.325), (0, 0.250, 0.300), 1),
    "HindLeg_L_Upper": ("Pelvis", (0.060, 0.130, 0.310), (0.065, 0.090, 0.210), -1),
    "HindLeg_L_Lower": ("HindLeg_L_Upper", (0.065, 0.090, 0.210), (0.065, 0.140, 0.110), 1),
    "HindPaw_L": ("HindLeg_L_Lower", (0.065, 0.140, 0.110), (0.065, 0.135, 0.000), -1),
    "HindLeg_R_Upper": ("Pelvis", (-0.060, 0.130, 0.310), (-0.065, 0.090, 0.210), -1),
    "HindLeg_R_Lower": ("HindLeg_R_Upper", (-0.065, 0.090, 0.210), (-0.065, 0.140, 0.110), 1),
    "HindPaw_R": ("HindLeg_R_Lower", (-0.065, 0.140, 0.110), (-0.065, 0.135, 0.000), -1),
}

# The same sockets as SmallQuadruped (names and parent bones); species place them on their mesh (pw_species).
SOCKETS = {
    "SOCK_HeadTop": ("Head", (0, -0.150, 0.600)),
    "SOCK_Neck": ("Neck", (0, -0.120, 0.420)),
    "SOCK_Back": ("Spine", (0, 0.030, 0.420)),
    "SOCK_TailBase": ("Tail_01", (0, 0.200, 0.360)),
    "SOCK_Ear_L": ("Ear_L_01", (0.040, -0.120, 0.530)),
    "SOCK_Ear_R": ("Ear_R_01", (-0.040, -0.120, 0.530)),
}

FAMILIES = {"MediumQuadruped": (MEDIUM_QUADRUPED, SOCKETS)}

# T52 HeavyQuadruped (RIG-003): proportions of the Boar Piglet (A-SP-012, about 64 x 43 cm), a round barrel body (back at
# 0.32 m), a big round head on a short neck, short stubby legs, ears on two bones leaning out and forward, a curly tail on
# two. The quadruped bone names, parents and axes (sockets and Unity paths unchanged): positive X swings a leg forward,
# lifts the front on Pelvis, Spine and Chest, nods Neck and Head down, folds the hock back, lifts Tail_01 and flops an ear
# forward and down.
HEAVY_QUADRUPED = {
    "Root": (None, (0, 0, 0), (0, 0, 0.08), 1),
    "Pelvis": ("Root", (0, 0.120, 0.210), (0, 0.050, 0.213), -1),
    "Spine": ("Pelvis", (0, 0.050, 0.213), (0, -0.025, 0.215), -1),
    "Chest": ("Spine", (0, -0.025, 0.215), (0, -0.085, 0.225), -1),
    "Neck": ("Chest", (0, -0.085, 0.225), (0, -0.140, 0.255), 1),
    "Head": ("Neck", (0, -0.140, 0.255), (0, -0.140, 0.375), 1),
    "Ear_L_01": ("Head", (0.062, -0.180, 0.362), (0.0765, -0.2024, 0.3918), 1),
    "Ear_L_02": ("Ear_L_01", (0.0765, -0.2024, 0.3918), (0.0929, -0.2275, 0.4253), 1),
    "Ear_R_01": ("Head", (-0.062, -0.180, 0.362), (-0.0765, -0.2024, 0.3918), 1),
    "Ear_R_02": ("Ear_R_01", (-0.0765, -0.2024, 0.3918), (-0.0929, -0.2275, 0.4253), 1),
    "FrontLeg_L_Upper": ("Chest", (0.064, -0.062, 0.150), (0.065, -0.065, 0.088), -1),
    "FrontLeg_L_Lower": ("FrontLeg_L_Upper", (0.065, -0.065, 0.088), (0.066, -0.067, 0.032), -1),
    "FrontPaw_L": ("FrontLeg_L_Lower", (0.066, -0.067, 0.032), (0.066, -0.079, 0.000), -1),
    "FrontLeg_R_Upper": ("Chest", (-0.064, -0.062, 0.150), (-0.065, -0.065, 0.088), -1),
    "FrontLeg_R_Lower": ("FrontLeg_R_Upper", (-0.065, -0.065, 0.088), (-0.066, -0.067, 0.032), -1),
    "FrontPaw_R": ("FrontLeg_R_Lower", (-0.066, -0.067, 0.032), (-0.066, -0.079, 0.000), -1),
    "Tail_01": ("Pelvis", (0, 0.204, 0.243), (0, 0.231, 0.257), 1),
    "Tail_02": ("Tail_01", (0, 0.231, 0.257), (0, 0.248, 0.283), 1),
    "HindLeg_L_Upper": ("Pelvis", (0.068, 0.128, 0.150), (0.070, 0.118, 0.092), -1),
    "HindLeg_L_Lower": ("HindLeg_L_Upper", (0.070, 0.118, 0.092), (0.070, 0.130, 0.034), 1),
    "HindPaw_L": ("HindLeg_L_Lower", (0.070, 0.130, 0.034), (0.070, 0.120, 0.000), -1),
    "HindLeg_R_Upper": ("Pelvis", (-0.068, 0.128, 0.150), (-0.070, 0.118, 0.092), -1),
    "HindLeg_R_Lower": ("HindLeg_R_Upper", (-0.070, 0.118, 0.092), (-0.070, 0.130, 0.034), 1),
    "HindPaw_R": ("HindLeg_R_Lower", (-0.070, 0.130, 0.034), (-0.070, 0.120, 0.000), -1),
}
# The quadruped sockets on the same parent bones (species place them on their mesh).
HEAVY_SOCKETS = {
    "SOCK_HeadTop": ("Head", (0, -0.195, 0.390)),
    "SOCK_Neck": ("Neck", (0, -0.118, 0.222)),
    "SOCK_Back": ("Spine", (0, 0.030, 0.320)),
    "SOCK_TailBase": ("Tail_01", (0, 0.212, 0.246)),
    "SOCK_Ear_L": ("Ear_L_01", (0.062, -0.180, 0.362)),
    "SOCK_Ear_R": ("Ear_R_01", (-0.062, -0.180, 0.362)),
}
FAMILIES["HeavyQuadruped"] = (HEAVY_QUADRUPED, HEAVY_SOCKETS)

# T52 Bird (RIG-004): proportions of the Owl (A-SP-010, about 36 x 44 cm), a round body with the head merged in. No beak,
# feather or toe bone. Axes: positive X turns tip Body forward (a bow), nod Neck and Head down, swing the wings back,
# swing the legs forward, lift the toes (Foot) and the tail; Head Y turns the head, Head Z tilts it; on the wings Z flaps
# (Wing_L out with negative values, Wing_R out with positive ones). The legs hang from Root, so a bow never moves the feet.
BIRD = {
    "Root": (None, (0, 0, 0), (0, 0, 0.08), 1),
    "Body": ("Root", (0, 0.000, 0.060), (0, 0.000, 0.250), 1),
    "Neck": ("Body", (0, 0.000, 0.250), (0, -0.004, 0.292), 1),
    "Head": ("Neck", (0, -0.004, 0.292), (0, -0.004, 0.420), 1),
    "Wing_L_Upper": ("Body", (0.118, 0.004, 0.258), (0.148, 0.034, 0.165), 1),
    "Wing_L_Lower": ("Wing_L_Upper", (0.148, 0.034, 0.165), (0.138, 0.090, 0.078), 1),
    "Wing_R_Upper": ("Body", (-0.118, 0.004, 0.258), (-0.148, 0.034, 0.165), 1),
    "Wing_R_Lower": ("Wing_R_Upper", (-0.148, 0.034, 0.165), (-0.138, 0.090, 0.078), 1),
    "Leg_L": ("Root", (0.052, -0.030, 0.085), (0.052, -0.052, 0.024), -1),
    "Foot_L": ("Leg_L", (0.052, -0.052, 0.024), (0.052, -0.098, 0.010), -1),
    "Leg_R": ("Root", (-0.052, -0.030, 0.085), (-0.052, -0.052, 0.024), -1),
    "Foot_R": ("Leg_R", (-0.052, -0.052, 0.024), (-0.052, -0.098, 0.010), -1),
    "Tail_01": ("Body", (0, 0.118, 0.102), (0, 0.158, 0.082), 1),
    "Tail_02": ("Tail_01", (0, 0.158, 0.082), (0, 0.192, 0.068), 1),
}
# Four sockets (the owl hides the ears: no Ear sockets on the family). Back sits between the wings.
BIRD_SOCKETS = {
    "SOCK_HeadTop": ("Head", (0, -0.020, 0.432)),
    "SOCK_Neck": ("Neck", (0, -0.104, 0.250)),
    "SOCK_Back": ("Body", (0, 0.112, 0.262)),
    "SOCK_TailBase": ("Tail_01", (0, 0.134, 0.096)),
}
FAMILIES["Bird"] = (BIRD, BIRD_SOCKETS)

# T51: bone placement per species on the same skeleton, as data. A Generic clip sets each bone's local rotation, so a
# species may move and resize its bones but never turn them: every bone keeps the family direction and roll (the Fawn's),
# only its head and length change. Entry: (head, length), or a length alone for a bone that starts where its parent
# ends in the family table (spine, neck, leg, ear and tail chains). _L bones are mirrored onto _R. Root never moves (the
# clips lift it in metres). Unlisted bones keep the family placement.
SPECIES_BONES = {
    # Fox (A-SP-007, about 80 x 50 cm with its ears and tail): back at 0.21 m, short legs, big head at 0.31 m, ears on
    # the skull top, three tail bones along the plume.
    "Fox": {
        "Pelvis": ((0, 0.105, 0.205), 0.070), "Spine": 0.075, "Chest": 0.065, "Neck": 0.065, "Head": 0.120,
        "Ear_L_01": ((0.048, -0.176, 0.378), 0.050), "Ear_L_02": 0.050,
        "FrontLeg_L_Upper": ((0.044, -0.078, 0.160), 0.065), "FrontLeg_L_Lower": 0.060, "FrontPaw_L": 0.036,
        "HindLeg_L_Upper": ((0.050, 0.100, 0.160), 0.055), "HindLeg_L_Lower": 0.060, "HindPaw_L": 0.055,
        "Tail_01": ((0, 0.140, 0.215), 0.075), "Tail_02": ((0, 0.236, 0.214), 0.080), "Tail_03": ((0, 0.330, 0.238), 0.060),
    },
    # Badger (A-SP-009, about 69 x 30 cm): a low heavy body (back at 0.20 m), short thick legs, the head low and
    # forward with its pivot behind the eyes, small ears, a short tail.
    "Badger": {
        "Pelvis": ((0, 0.150, 0.195), 0.085), "Spine": 0.095, "Chest": 0.075, "Neck": 0.040,
        "Head": ((0, -0.178, 0.215), 0.100),
        "Ear_L_01": ((0.060, -0.204, 0.248), 0.025), "Ear_L_02": 0.025,
        "FrontLeg_L_Upper": ((0.072, -0.098, 0.140), 0.060), "FrontLeg_L_Lower": 0.055, "FrontPaw_L": 0.026,
        "HindLeg_L_Upper": ((0.076, 0.140, 0.135), 0.055), "HindLeg_L_Lower": 0.050, "HindPaw_L": 0.039,
        "Tail_01": ((0, 0.228, 0.196), 0.030), "Tail_02": 0.025, "Tail_03": 0.020,
    },
}


def species_bones(species, family="MediumQuadruped"):
    """Resolved {bone: (head, tail)} for a species (family table where it lists nothing)."""
    table = FAMILIES[family][0]
    data = dict(SPECIES_BONES.get(species, {}))
    for name, entry in list(data.items()):
        mirrored = name.replace("_L_", "_R_") if "_L_" in name else (name[:-2] + "_R" if name.endswith("_L") else None)
        if mirrored and mirrored not in data:
            data[mirrored] = ((-entry[0][0], entry[0][1], entry[0][2]), entry[1]) if isinstance(entry, tuple) else entry
    out = {}
    for name, (parent, head, tail, _) in table.items():  # parents come first in the table
        entry = data.get(name)
        if entry is None:
            out[name] = (Vector(head), Vector(tail))
            continue
        direction = (Vector(tail) - Vector(head)).normalized()
        if isinstance(entry, tuple):
            new_head, length = Vector(entry[0]), entry[1]
        else:
            if parent is None or (Vector(table[parent][2]) - Vector(head)).length > 1e-6:
                raise ValueError(f"{species}.{name}: give a head, the family bone does not start at its parent's end")
            new_head, length = out[parent][1].copy(), entry
        out[name] = (new_head, new_head + direction * length)
    return out


def fit_species(species, family="MediumQuadruped"):
    """Moves the bones of SK_<family> in the open species file to SPECIES_BONES (heads and lengths only). Returns the
    largest change of a bone's rest orientation (radians); it must stay 0 so every family clip plays unchanged."""
    bones = FAMILIES[family][0]
    arm = bpy.data.objects["SK_" + family]
    before = {b.name: b.matrix_local.to_3x3().copy() for b in arm.data.bones}
    placed = species_bones(species, family)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for name, (head, tail) in placed.items():
        e = eb[name]
        e.head = head
        e.tail = tail
        y = (tail - head).normalized()
        x = Vector((bones[name][3], 0.0, 0.0))
        x = (x - y * x.dot(y)).normalized()
        e.align_roll(x.cross(y))
    bpy.ops.object.mode_set(mode='OBJECT')
    worst = 0.0
    for b in arm.data.bones:
        m = b.matrix_local.to_3x3()
        worst = max(worst, max(m.col[i].angle(before[b.name].col[i]) for i in range(3)))
    return worst


def _collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def build_armature(family):
    """Creates SK_<family> from its table (replacing an existing one) and its sockets. Returns the armature object."""
    bones, sockets = FAMILIES[family]
    name = "SK_" + family
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    data = bpy.data.armatures.get(name) or bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    for coll in ("RIG", "EXPORT"):
        _collection(coll).objects.link(arm)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = data.edit_bones
    for b in list(eb):
        eb.remove(b)
    for bone, (parent, head, tail, xsign) in bones.items():
        e = eb.new(bone)
        e.head = head
        e.tail = tail
        # Local X to the side the SmallQuadruped bone's X points to: Z = X × Y.
        y = (Vector(tail) - Vector(head)).normalized()
        x = Vector((xsign, 0.0, 0.0))
        x = (x - y * x.dot(y)).normalized()
        e.align_roll(x.cross(y))
        e.use_deform = True
    for bone, (parent, _, _, _) in bones.items():
        if parent:
            eb[bone].parent = eb[parent]
            eb[bone].use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    data.display_type = 'STICK'
    for sock, (bone, co) in sockets.items():
        o = bpy.data.objects.get(sock)
        if o is None:
            o = bpy.data.objects.new(sock, None)
            o.empty_display_type = 'PLAIN_AXES'
            o.empty_display_size = 0.03
            for coll in ("SOCKETS", "EXPORT"):
                _collection(coll).objects.link(o)
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = bone
        o.matrix_world = Matrix.Translation(co)
    return arm


def axes_report(family):
    """Each bone's local X (rounded), to compare with SmallQuadruped's conventions."""
    arm = bpy.data.objects["SK_" + family]
    return {b.name: tuple(round(v, 2) for v in b.matrix_local.to_3x3().col[0]) for b in arm.data.bones}


def build_rig_file(family, folder=None):
    """A new BL_Rig_<family>.blend next to BL_Rig_SmallQuadruped.blend with the armature and its sockets."""
    folder = folder or os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Rigs")
    for c in ("RIG", "SOCKETS", "EXPORT", "WORK"):
        _collection(c)
    build_armature(family)
    path = os.path.join(folder, f"BL_Rig_{family}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    return path
