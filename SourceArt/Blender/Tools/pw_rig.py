"""Family skeletons from a bone table (T46). Source of BL_Rig_<Family>.blend.

The SmallQuadruped skeleton (T06) was placed by hand; MediumQuadruped and the next families are built here so the rig
file always has its recipe. Same logical bone names as SmallQuadruped (doc RIGS « same logical names where sensible »)
and the same bone axes: each bone's local X points to the side the SmallQuadruped bone's X points to, so a clip value
(degrees around local X) bends every family the same way.

Usage inside Blender (a new empty file, own call), then:
    import pw_rig; pw_rig.build_rig_file("MediumQuadruped")
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
