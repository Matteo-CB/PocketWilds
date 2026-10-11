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
from mathutils.kdtree import KDTree

ARMATURE = "SK_SmallQuadruped"
# Metaball element radius: with stiffness 2 the surface of an ellipsoid lands on its half sizes.
K = 1.0 / 0.575

EYE = (0, 0, 0, 1)
NOSE = (0, 0, 1, 0)


def _ell(center, half, rot=(0, 0, 0), stiff=2.0, mirror=False, part=None, axis=None):
    """part="head": the volume moves rigidly with the Head bone (face beads never pop out).
    axis: optional direction of the long (local Y) half size; replaces rot."""
    return dict(center=center, half=half, rot=rot, stiff=stiff, mirror=mirror, part=part, axis=axis)


def _shell_lobes(center, radii, rows, half, push=0.004, lean=0.72, stiff=1.4):
    """Large stylized spine lobes on an ellipsoid shell (A-SP-003: one sculpted volume, no quills).
    rows: (alpha, [beta...]) in degrees; alpha runs from the forehead (0) over the back to the rump (180),
    beta from the spine line (0) down the flank. Each lobe leans backward along the shell and a little
    outward, so its tip stands out of the silhouette. beta > 0 is mirrored."""
    c = Vector(center); r = Vector(radii)
    lobes = []
    for alpha, betas in rows:
        a = math.radians(alpha)
        for beta in betas:
            b = math.radians(beta)
            sp = Vector((math.sin(b), -math.cos(b) * math.cos(a), math.cos(b) * math.sin(a)))
            p = c + Vector((r.x * sp.x, r.y * sp.y, r.z * sp.z))
            n = Vector((sp.x / r.x, sp.y / r.y, sp.z / r.z)).normalized()
            t = Vector((0.0, r.y * math.cos(b) * math.sin(a), r.z * math.cos(b) * math.cos(a))).normalized()
            # Flank lobes lie flatter: a crown of side spikes reads as a mane from the front.
            lean_b = lean + (1.0 - lean) * 0.6 * min(1.0, beta / 70.0)
            axis = (t * lean_b + n * (1.0 - lean_b)).normalized()
            lobes.append(_ell(tuple(p + n * push), half, stiff=stiff, mirror=beta > 0.5, part="shell", axis=tuple(axis)))
    return lobes


HEDGEHOG_SHELL = ((0, 0.045, 0.160), (0.140, 0.170, 0.125))


def _chain(points, radii, stiff=2.0):
    """A thin solid volume along a polyline (Field Mouse tail): one ellipsoid per segment, overlapping."""
    out = []
    for i in range(len(points) - 1):
        a = Vector(points[i]); b = Vector(points[i + 1])
        r = radii[i]
        out.append(_ell(tuple((a + b) * 0.5), (r, (b - a).length * 0.62, r), stiff=stiff, axis=tuple((b - a).normalized())))
    return out


# The tail curls up behind the rump like a question mark: long and thin, but the body stays compact on the ground
# (a tail trailing 22 cm made the measured body longer than the squirrel's, so fewer hiding spots fit every species).
FIELDMOUSE_TAIL = [(0, 0.140, 0.090), (0, 0.188, 0.070), (0, 0.232, 0.078), (0, 0.262, 0.108),
                   (0, 0.272, 0.150), (0, 0.258, 0.190)]


FAWN_TAIL = [(0, 0.190, 0.362), (0, 0.212, 0.345), (0, 0.228, 0.322)]
# Slender legs as continuous chains (thin separate volumes broke apart): shoulder or hip, knee or hock, fetlock, hoof.
FAWN_FRONT_LEG = [(0.048, -0.058, 0.305), (0.052, -0.066, 0.190), (0.054, -0.072, 0.070), (0.055, -0.079, 0.016)]
FAWN_HIND_LEG = [(0.050, 0.112, 0.300), (0.058, 0.098, 0.208), (0.062, 0.140, 0.115), (0.063, 0.136, 0.016)]


def _legs(points, radii):
    """A leg chain on both sides."""
    return _chain(points, radii) + _chain([(-x, y, z) for x, y, z in points], radii)

SPECS = {
    # Fawn (A-SP-006), the first MediumQuadruped: a big gentle head with large leaf ears on the ear bones, bead
    # eyes, a short muzzle, slender legs with simple hooves, a small white tail tuft. The whole spec sits on
    # SK_MediumQuadruped (pw_rig), whose bones carry the same names as SmallQuadruped.
    "Fawn": dict(
        armature="SK_MediumQuadruped",
        blockout=[
            _ell((0, -0.175, 0.468), (0.074, 0.084, 0.071), part="head"),
            _ell((0.034, -0.205, 0.448), (0.034, 0.040, 0.034), stiff=1.6, mirror=True, part="head"),   # cheeks
            _ell((0, -0.245, 0.440), (0.036, 0.050, 0.033), rot=(-14, 0, 0), stiff=1.5, part="head"),   # muzzle
            _ell((0, -0.118, 0.402), (0.040, 0.044, 0.080), rot=(28, 0, 0)),                             # neck
            _ell((0, -0.050, 0.338), (0.066, 0.074, 0.074)),                                             # chest
            _ell((0, 0.045, 0.338), (0.070, 0.128, 0.070)),                                              # body
            _ell((0, 0.135, 0.346), (0.068, 0.070, 0.072)),                                              # rump
            _ell((0, 0.206, 0.352), (0.022, 0.028, 0.034), rot=(30, 0, 0), stiff=1.6),                   # tail tuft
            _ell((0.048, 0.112, 0.288), (0.034, 0.048, 0.070), mirror=True),                             # haunches
            _ell((0.054, -0.079, 0.022), (0.019, 0.023, 0.022), mirror=True),                            # front hooves
            _ell((0.063, 0.136, 0.022), (0.019, 0.023, 0.022), mirror=True),                             # hind hooves
        ] + _legs(FAWN_FRONT_LEG, (0.026, 0.019, 0.018)) + _legs(FAWN_HIND_LEG, (0.030, 0.021, 0.018)),
        # Leaf ears along the ear bones (distance along the bone axis, half sizes: thickness, width, length): the
        # flat side faces forward.
        ears=[(-0.004, (0.012, 0.018, 0.026)), (0.040, (0.012, 0.036, 0.048)), (0.088, (0.010, 0.024, 0.028))],
        # Long ears in two segments, bent by the family's ear bones (the Rabbit rule).
        ear_mode="long",
        tail_path=FAWN_TAIL,
        tail_member_y=(0.180, 0.196),
        eyes=dict(x=0.042, z=0.480, r=0.022, sink=0.35),
        nose=dict(x=0.0, z=0.446, r=0.013, sink=0.45),
        head_center=(0, -0.175, 0.468),
        head_radii=(0.074, 0.084, 0.071),
        # The legs below the body move with their bones; the trunk above follows the spine.
        hind_leg_band=(0.300, 0.250),
        front_leg_band=(0.300, 0.250),
        lod0_faces=1600,
        lod1_mode="collapse",
        lod1_tris=1200,
        lod2_tris=560,
        back_probe_y=0.04,
        sockets={
            "SOCK_Neck": (0.0, -0.112, 0.405),
            "SOCK_TailBase": (0.0, 0.205, 0.356),
            "SOCK_Ear_L": (-0.040, -0.120, 0.530),
            "SOCK_Ear_R": (0.040, -0.120, 0.530),
        },
    ),
    # Field Mouse (A-SP-004): the smallest of the meadow. Big round head, large round ears that belong to the
    # head (they turn with it), bead eyes, small pointed muzzle, compact pear body, short legs, a thin long tail
    # curling up at the end (one solid chain on the two tail bones, half their motion).
    "FieldMouse": dict(
        blockout=[
            _ell((0, -0.100, 0.175), (0.084, 0.080, 0.076), part="head"),
            _ell((0.040, -0.136, 0.152), (0.040, 0.040, 0.034), stiff=1.6, mirror=True, part="head"),  # cheeks
            _ell((0, -0.178, 0.148), (0.028, 0.046, 0.026), rot=(-10, 0, 0), stiff=1.5, part="head"),  # muzzle
            _ell((0.064, -0.078, 0.246), (0.052, 0.013, 0.050), rot=(-12, 28, 22), stiff=2.2, mirror=True, part="head"),  # ears
            _ell((0, -0.040, 0.108), (0.068, 0.070, 0.070)),                         # chest
            _ell((0, 0.050, 0.098), (0.082, 0.102, 0.082)),                          # body
            _ell((0.055, 0.085, 0.072), (0.040, 0.056, 0.050), mirror=True),        # haunches
            _ell((0.060, 0.045, 0.014), (0.022, 0.050, 0.014), mirror=True),        # hind feet
            _ell((0.038, -0.070, 0.045), (0.018, 0.020, 0.035), mirror=True),       # front legs
            _ell((0.038, -0.090, 0.013), (0.019, 0.026, 0.013), mirror=True),       # front paws
        ] + _chain(FIELDMOUSE_TAIL, (0.021, 0.019, 0.017, 0.016, 0.015)),
        tail_path=FIELDMOUSE_TAIL,
        tail_member_y=(0.125, 0.150),
        tail_gain=0.5,
        eyes=dict(x=0.044, z=0.188, r=0.022, sink=0.35),
        nose=dict(x=0.0, z=0.150, r=0.012, sink=0.45),
        head_center=(0, -0.100, 0.175),
        head_radii=(0.084, 0.080, 0.076),
        ear_mode="none",
        hind_leg_band=(0.055, 0.025),
        front_leg_band=(0.055, 0.025),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=1500,
        lod1_mode="collapse",
        lod1_tris=900,
        lod2_tris=420,
        back_probe_y=0.05,
        sockets={
            "SOCK_Neck": (0.0, -0.060, 0.130),
            "SOCK_TailBase": (0.0, 0.145, 0.088),
            "SOCK_Ear_L": (-0.066, -0.078, 0.270),
            "SOCK_Ear_R": (0.066, -0.078, 0.270),
        },
    ),
    # Guinea Pig (A-SP-005): a potato body with no neck and no tail, a broad blunt face with round cheeks, tiny
    # petal ears on the head, short legs hidden under the body (only the feet show).
    "GuineaPig": dict(
        blockout=[
            _ell((0, 0.025, 0.112), (0.108, 0.135, 0.098)),                          # body
            _ell((0, 0.105, 0.108), (0.104, 0.085, 0.092)),                          # round rump
            _ell((0, -0.070, 0.105), (0.092, 0.080, 0.090)),                         # chest
            _ell((0, -0.125, 0.142), (0.084, 0.082, 0.077), part="head"),
            _ell((0.045, -0.165, 0.120), (0.042, 0.040, 0.036), stiff=1.6, mirror=True, part="head"),  # cheeks
            _ell((0, -0.200, 0.122), (0.034, 0.030, 0.028), stiff=1.5, part="head"),  # blunt muzzle
            _ell((0.062, -0.105, 0.200), (0.026, 0.010, 0.020), rot=(-10, 35, 30), stiff=2.2, mirror=True, part="head"),  # ears
            _ell((0.050, -0.080, 0.045), (0.022, 0.022, 0.030), mirror=True),       # front legs
            _ell((0.050, -0.092, 0.016), (0.020, 0.026, 0.016), mirror=True),       # front feet
            _ell((0.068, 0.100, 0.045), (0.026, 0.026, 0.032), mirror=True),        # hind legs
            _ell((0.066, 0.092, 0.016), (0.024, 0.034, 0.016), mirror=True),        # hind feet
        ],
        eyes=dict(x=0.050, z=0.160, r=0.019, sink=0.35),
        nose=dict(x=0.0, z=0.128, r=0.012, sink=0.45),
        head_center=(0, -0.125, 0.142),
        head_radii=(0.084, 0.082, 0.077),
        ear_mode="none",
        hind_leg_band=(0.055, 0.025),
        front_leg_band=(0.055, 0.025),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=900,
        lod1_mode="collapse",
        lod1_tris=860,
        lod2_tris=400,
        back_probe_y=0.03,
        sockets={
            "SOCK_Neck": (0.0, -0.085, 0.175),
            "SOCK_TailBase": (0.0, 0.200, 0.110),
            "SOCK_Ear_L": (-0.062, -0.105, 0.212),
            "SOCK_Ear_R": (0.062, -0.105, 0.212),
        },
    ),
    # Field Hamster (T47, the Meadow Pack companion): a chubby round ball, no neck, a big head with soft cheek pouches
    # wider than the skull, round ears standing on the head (they turn with it), bead eyes, a tiny muzzle, short legs
    # with only the paws showing, no visible tail. Read against the Guinea Pig: rounder and taller (a ball, not a
    # potato), round ears twice as big, puffed cheeks, golden back over a white belly. The Stash clip squashes the Head
    # bone a little: the whole face is rigid on Head, so the cheeks puff the same on every LOD.
    "FieldHamster": dict(
        blockout=[
            _ell((0, 0.035, 0.128), (0.122, 0.122, 0.128)),                          # round body, a tall dome
            _ell((0, 0.098, 0.118), (0.116, 0.088, 0.110)),                          # round rump
            _ell((0, -0.058, 0.118), (0.106, 0.080, 0.104)),                         # chest
            _ell((0, -0.112, 0.166), (0.090, 0.084, 0.084), part="head"),
            _ell((0.066, -0.140, 0.136), (0.058, 0.052, 0.050), stiff=1.6, mirror=True, part="head"),  # cheek pouches
            _ell((0, -0.200, 0.142), (0.030, 0.026, 0.024), stiff=1.5, part="head"),  # tiny muzzle
            _ell((0.062, -0.100, 0.255), (0.042, 0.018, 0.040), rot=(-6, 20, 16), stiff=2.2, mirror=True, part="head"),  # round ears
            _ell((0.050, -0.088, 0.042), (0.022, 0.022, 0.030), mirror=True),       # front legs, short
            _ell((0.050, -0.104, 0.014), (0.020, 0.026, 0.014), mirror=True),       # front paws
            _ell((0.074, 0.092, 0.072), (0.044, 0.058, 0.054), mirror=True),        # haunches
            _ell((0.070, 0.084, 0.014), (0.024, 0.040, 0.014), mirror=True),        # hind feet
        ],
        eyes=dict(x=0.050, z=0.182, r=0.020, sink=0.35),
        nose=dict(x=0.0, z=0.150, r=0.012, sink=0.45),
        head_center=(0, -0.112, 0.166),
        head_radii=(0.090, 0.084, 0.084),
        ear_mode="none",
        hind_leg_band=(0.055, 0.025),
        front_leg_band=(0.055, 0.025),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=1120,
        lod1_mode="collapse",
        lod1_tris=880,
        lod2_tris=350,
        back_probe_y=0.04,
        sockets={
            "SOCK_Neck": (0.0, -0.070, 0.140),
            # The tail is hidden: the socket stays on the rump for the family hierarchy, no accessory uses it.
            "SOCK_TailBase": (0.0, 0.185, 0.100),
            "SOCK_Ear_L": (-0.062, -0.100, 0.272),
            "SOCK_Ear_R": (0.062, -0.100, 0.272),
        },
    ),
    # Mole (T49, A-SP-203, L'Éveil des Terriers): a low velvet body like a soft bean, the head flows into it with no
    # visible neck, bead eyes almost hidden in the fur, a pointed pink nose, very large rounded pink mitten hands held
    # out at the front sides (the front paws scaled up, palms turned out), small hind feet, a tiny tail. About 46 x 20 cm.
    # The hands stay low on the front leg bones (the dig clip scratches with them); the whole snout is rigid on Head.
    # Optional module (build_star_nose): the Star Nose variant's rosette of round lobes, a separate mesh rigid on Head.
    "Mole": dict(
        blockout=[
            _ell((0, 0.020, 0.110), (0.098, 0.165, 0.090)),                          # soft bean body
            _ell((0, 0.098, 0.104), (0.094, 0.084, 0.084)),                          # round rump
            _ell((0, -0.080, 0.104), (0.088, 0.080, 0.082)),                         # chest, no neck
            _ell((0, -0.135, 0.088), (0.072, 0.082, 0.068), part="head"),
            _ell((0, -0.205, 0.074), (0.034, 0.050, 0.030), rot=(-6, 0, 0), stiff=1.6, part="head"),  # tapering snout
            _ell((0, -0.240, 0.068), (0.017, 0.022, 0.016), stiff=1.5, part="head"),  # pointed nose tip
            _ell((0.076, -0.100, 0.056), (0.028, 0.030, 0.030), mirror=True),       # short arms
            # Mitten hands: big round flat paddles, palms turned out and a little forward, resting on the ground.
            _ell((0.108, -0.130, 0.038), (0.022, 0.058, 0.040), rot=(0, -10, -34), stiff=1.8, mirror=True),
            _ell((0.128, -0.166, 0.034), (0.018, 0.026, 0.030), rot=(0, -10, -34), stiff=1.6, mirror=True),  # round fingertips
            _ell((0.068, 0.110, 0.060), (0.034, 0.048, 0.044), mirror=True),        # haunches
            _ell((0.066, 0.118, 0.016), (0.019, 0.030, 0.012), mirror=True),        # small hind feet
            _ell((0, 0.184, 0.082), (0.011, 0.020, 0.011), rot=(-22, 0, 0), stiff=1.6),  # tiny tail
        ],
        eyes=dict(x=0.034, z=0.108, r=0.010, sink=0.55),
        nose=dict(x=0.0, z=0.068, r=0.013, sink=0.40),
        head_center=(0, -0.135, 0.088),
        head_radii=(0.072, 0.082, 0.068),
        ear_mode="none",
        hind_leg_band=(0.055, 0.025),
        # The whole mitten (up to 8 cm) follows the front leg bones; the chest above follows the trunk.
        front_leg_band=(0.088, 0.062),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=1100,
        lod1_mode="collapse",
        lod1_tris=900,
        lod2_tris=380,
        back_probe_y=0.030,
        sockets={
            "SOCK_Neck": (0.0, -0.085, 0.128),
            # No tail socket in use (hidden TailBase): it stays on the rump for the family hierarchy.
            "SOCK_TailBase": (0.0, 0.190, 0.080),
            "SOCK_Ear_L": (-0.050, -0.125, 0.150),
            "SOCK_Ear_R": (0.050, -0.125, 0.150),
        },
        # Star Nose rosette: 11 round lobes around the nose bead (lobe count, peak and valley radius in nose radii).
        star_nose=dict(lobes=11, peak=2.30, valley=1.70, inner=0.95),
    ),
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
    # Hedgehog (A-SP-003): compact oval body, tiny face with a rounded snout, short legs, spines as one
    # shell volume with large lobes. Small round ears belong to the head (no ear bone motion), no tail.
    "Hedgehog": dict(
        blockout=[
            _ell(HEDGEHOG_SHELL[0], HEDGEHOG_SHELL[1], part="shell"),
            _ell((0, 0.045, 0.125), (0.125, 0.150, 0.100)),                          # body core, flat belly
            _ell((0, -0.105, 0.165), (0.078, 0.085, 0.078), part="head"),            # face
            _ell((0.042, -0.140, 0.148), (0.038, 0.038, 0.034), stiff=1.6, mirror=True, part="head"),  # cheeks
            _ell((0, -0.190, 0.150), (0.036, 0.050, 0.032), rot=(-8, 0, 0), stiff=1.6, part="head"),  # snout
            _ell((0.058, -0.085, 0.226), (0.024, 0.012, 0.024), rot=(0, 25, 20), mirror=True, part="head"),  # ears
            _ell((0.048, -0.060, 0.045), (0.024, 0.024, 0.035), mirror=True),       # front legs, short
            _ell((0.048, -0.075, 0.016), (0.024, 0.030, 0.016), mirror=True),       # front feet
            _ell((0.070, 0.100, 0.045), (0.028, 0.028, 0.035), mirror=True),        # hind legs
            _ell((0.072, 0.085, 0.016), (0.026, 0.036, 0.016), mirror=True),        # hind feet
        ] + _shell_lobes(HEDGEHOG_SHELL[0], HEDGEHOG_SHELL[1], [
            (40, [0, 28]), (62, [14, 42]), (85, [0, 28, 54]), (108, [14, 40, 62]),
            (131, [0, 28, 54]), (154, [14, 40]), (172, [0, 26]),
        ], half=(0.040, 0.066, 0.018), push=0.010, lean=0.70, stiff=2.5),
        shell=HEDGEHOG_SHELL,
        eyes=dict(x=0.038, z=0.190, r=0.020, sink=0.35),
        nose=dict(x=0.0, z=0.158, r=0.016, sink=0.40),
        head_center=(0, -0.105, 0.165),
        head_radii=(0.078, 0.085, 0.078),
        ear_mode="none",
        # Short legs: the leg bones only drive the feet; the skirt above follows the trunk.
        hind_leg_band=(0.055, 0.025),
        front_leg_band=(0.055, 0.025),
        leg_band_bones=("Upper", "Lower"),
        # Remeshing melts 30 lobes into a crumpled shell: the body is remeshed without them and every
        # lobe is clean low-poly geometry per LOD (34 / 18 / 4 triangles), rigid on the body under it.
        lobe_mode="mesh",
        lod0_faces=720,
        lod1_mode="collapse",
        lod1_tris=1060,
        lod1_body_tris=500,
        lod2_tris=450,
        lod2_body_tris=290,
        back_probe_y=0.06,
    ),
}


# ---------------------------------------------------------------- helpers

def armature_of(species):
    """The species' family skeleton (SmallQuadruped unless its spec names another, e.g. SK_MediumQuadruped)."""
    return bpy.data.objects[SPECS[species].get("armature", ARMATURE)]


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

    def add(center, half, rot, stiff, axis=None):
        e = mb.elements.new(type='ELLIPSOID')
        e.co = center; e.radius = K
        e.size_x, e.size_y, e.size_z = half
        if axis is not None:
            e.rotation = Vector(axis).to_track_quat('Y', 'Z')
        else:
            e.rotation = Euler([math.radians(a) for a in rot], 'XYZ').to_quaternion()
        e.stiffness = stiff

    mesh_lobes = spec.get("lobe_mode") == "mesh"
    for el in spec["blockout"]:
        c = el["center"]
        axis = el.get("axis")
        if mesh_lobes and axis is not None:
            continue
        add(c, el["half"], el["rot"], el["stiff"], axis)
        if el["mirror"]:
            mirrored_axis = (-axis[0], axis[1], axis[2]) if axis is not None else None
            add((-c[0], c[1], c[2]), el["half"], (el["rot"][0], -el["rot"][1], -el["rot"][2]), el["stiff"], mirrored_axis)

    # Ears ride the shared ear bones so their animation bends them from the base.
    arm = armature_of(species)
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
    if mesh_lobes:
        # Editable preview of the LOD0 lobes next to the metaball body.
        name = f"WORK_{species}_Lobes"
        _remove_object(name)
        bm = bmesh.new(); _add_lobes(bm, lobe_frames(species), 0)
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        for p in me.polygons:
            p.use_smooth = True
        bpy.data.collections["WORK"].objects.link(bpy.data.objects.new(name, me))
    return len(mb.elements)


# ---------------------------------------------------------------- mesh lobes

# Per LOD: (segments around the lobe, ring positions from base 0 to tip 1).
LOBE_LEVELS = {0: (6, (0.0, 0.33, 0.66)), 1: (5, (0.0, 0.5)), 2: (3, (0.0,))}


def lobe_profile(t):
    # Full leaf-shaped lobe: wide most of the way, then a soft point.
    return (1.0 - t) ** 0.55


def lobe_frames(species):
    """Base, tip and cross-section axes of every mesh lobe (mirrored ones included), in blockout order."""
    spec = SPECS[species]
    sc, sr = Vector(spec["shell"][0]), Vector(spec["shell"][1])
    frames = []
    for el in spec["blockout"]:
        if el.get("axis") is None or el.get("part") != "shell":
            continue
        for mirrored in ((False, True) if el["mirror"] else (False,)):
            c = Vector(el["center"]); a = Vector(el["axis"]).normalized()
            if mirrored:
                c.x = -c.x; a.x = -a.x
            d = c - sc
            n = Vector((d.x / sr.x ** 2, d.y / sr.y ** 2, d.z / sr.z ** 2)).normalized()
            u = a.cross(n)
            if u.length < 1e-6:
                u = Vector((1.0, 0.0, 0.0))
            u.normalize()
            w = a.cross(u).normalized()  # u x w = a: rings wind counter-clockwise around the axis
            hx, hy, hz = el["half"]
            frames.append(dict(base=c - a * hy * 0.6, tip=c + a * hy, axis=a, u=u, w=w, wx=hx, wz=hz))
    return frames


def _add_lobes(bm, frames, level):
    """Closed low-poly lobes, outward normals. Returns the vertex index range of each lobe."""
    segs, rings = LOBE_LEVELS[level]
    ranges = []
    bm.verts.ensure_lookup_table()
    for f in frames:
        first = len(bm.verts)
        span = f["tip"] - f["base"]
        ring_verts = []
        for t in rings:
            center = f["base"] + span * t
            k = lobe_profile(t)
            ring = []
            for i in range(segs):
                th = 2.0 * math.pi * i / segs
                ring.append(bm.verts.new(center + f["u"] * (math.cos(th) * f["wx"] * k) + f["w"] * (math.sin(th) * f["wz"] * k)))
            ring_verts.append(ring)
        tip = bm.verts.new(f["tip"])
        for r0, r1 in zip(ring_verts[:-1], ring_verts[1:]):
            for i in range(segs):
                j = (i + 1) % segs
                bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
        last = ring_verts[-1]
        for i in range(segs):
            bm.faces.new((last[i], last[(i + 1) % segs], tip))
        bm.faces.new(list(reversed(ring_verts[0])))
        ranges.append((first, len(bm.verts)))
    return ranges


def _rigid_lobes(target, source, frames):
    """Every lobe takes the weights of the body vertex under its base: lobes never shear."""
    part_t = target.data.attributes.get("PW_Part")
    if part_t is None:
        return 0
    part_s = source.data.attributes.get("PW_Part")
    regions = source.data.color_attributes["PW_Regions"].data
    body = [v for v in source.data.vertices
            if (part_s is None or part_s.data[v.index].value == 0)
            and regions[v.index].color[3] < 0.5 and regions[v.index].color[2] < 0.5]
    kd = KDTree(len(body))
    for i, v in enumerate(body):
        kd.insert(v.co, i)
    kd.balance()
    svg = source.vertex_groups; tvg = target.vertex_groups
    cache = {}
    count = 0
    for v in target.data.vertices:
        p = part_t.data[v.index].value
        if p <= 0:
            continue
        if p not in cache:
            _, idx, _ = kd.find(frames[p - 1]["base"])
            cache[p] = {svg[g.group].name: g.weight for g in body[idx].groups if g.weight > 0.0}
        for index in [g.group for g in v.groups]:
            tvg[index].remove([v.index])
        for k, x in cache[p].items():
            tvg[k].add([v.index], x, 'REPLACE')
        count += 1
    return count


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


def _with_beads(body, out_name, beads, collection, lobes=None):
    """Body + bead spheres (faces fully inside the body removed) + PW_Regions tags.
    lobes: (frames, level) adds mesh lobes, tagged in the PW_Part attribute (lobe index + 1)."""
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
    lobe_ranges = _add_lobes(final, lobes[0], lobes[1]) if lobes else []
    _remove_object(out_name)
    me = bpy.data.meshes.new(out_name); final.to_mesh(me); final.free()
    for p in me.polygons:
        p.use_smooth = True
    attr = me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    for i, d in enumerate(attr.data):
        d.color = region_of.get(i, (0, 0, 0, 0))
    if lobe_ranges:
        part = me.attributes.new("PW_Part", 'INT', 'POINT')
        for index, (a, b) in enumerate(lobe_ranges):
            for i in range(a, b):
                part.data[i].value = index + 1
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
    lobes = (lobe_frames(species), 0) if spec.get("lobe_mode") == "mesh" else None
    lod0, _ = _with_beads(body, f"SKM_{species}_LOD0", beads, "GEO", lobes)
    preview = bpy.data.objects.get(f"WORK_{species}_Lobes")
    if preview is not None:
        preview.hide_viewport = True; preview.hide_render = True
    for n in src:
        bpy.data.objects[n].hide_viewport = True; bpy.data.objects[n].hide_render = True
    return check(lod0)


# ---------------------------------------------------------------- skinning

def _get(vg, v):
    return {vg[g.group].name: g.weight for g in v.groups}


def _put(vg, v, w):
    w = {k: x for k, x in w.items() if x > 1e-4}
    total = sum(w.values()) or 1.0
    # Read the indices first: removing a group invalidates the vertex's group elements (Blender 5).
    for index in [g.group for g in v.groups]:
        vg[index].remove([v.index])
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
    y0, y1 = spec.get("tail_member_y", (0.17, 0.22))
    member = smooth(0.005, 0.05, s) * max(smooth(y0, y1, co.y), smooth(0.30, 0.36, co.z))
    upper = smooth(0.08, 0.20, s)
    # A thin tail far below the tail bones (Field Mouse) takes only part of their motion; the rest follows the pelvis.
    gain = spec.get("tail_gain", 1.0)
    weights = {"Tail_01": (1.0 - upper) * gain, "Tail_02": upper * gain}
    if gain < 1.0:
        weights["Pelvis"] = 1.0 - gain
    return member, weights


def _ear_weights(species, arm, co):
    spec = SPECS[species]
    if spec.get("ear_mode") == "none":
        return None
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
    arm = armature_of(species)
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
        # Eyes and nose ride the head; a detail tag low on the legs is the Fawn's hooves (pw_masks), not the face.
        if a > 0.5 or (b > 0.5 and co.z > 0.1):
            _put(vg, v, {"Head": 1.0}); continue
        # Legs only drive the lower part of haunches and shoulders; the volume above follows the trunk.
        hind_band = spec.get("hind_leg_band", (0.15, 0.085))
        front_band = spec.get("front_leg_band", (0.14, 0.09))
        for side in ("L", "R"):
            for part in spec.get("leg_band_bones", ("Upper",)):
                k = f"HindLeg_{side}_{part}"
                if k in w:
                    keep = smooth(hind_band[0], hind_band[1], co.z)
                    moved = w[k] * (1 - keep); w[k] -= moved; w["Pelvis"] = w.get("Pelvis", 0) + moved
                k = f"FrontLeg_{side}_{part}"
                if k in w:
                    keep = smooth(front_band[0], front_band[1], co.z)
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
        # Eyes and nose ride the head; a detail tag low on the legs is the Fawn's hooves (pw_masks), not the face.
        if a > 0.5 or (b > 0.5 and co.z > 0.1):
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
    if spec.get("lobe_mode") == "mesh":
        stats["lobe_verts"] = _rigid_lobes(obj, obj, lobe_frames(species))
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
    arm = armature_of(species)
    lod0 = bpy.data.objects[f"SKM_{species}_LOD0"]
    if spec.get("lod1_mode") == "collapse":
        # Small features (squirrel ears) survive a collapse of LOD0 better than a coarse QuadriFlow.
        lod1_body = _collapse(bpy.data.objects[f"WORK_{species}_BodyRemesh"], f"WORK_{species}_LOD1_Body",
                              spec.get("lod1_body_tris", spec["lod1_tris"] - 60))
    else:
        hi = _hi_mesh(species)
        lod1_body = _quad_body(f"WORK_{species}_LOD1_Body", hi, spec["lod1_faces"], pre_decimate=0.2)
        bpy.data.meshes.remove(hi)
    # LOD2: symmetric collapse of LOD1 (triangles are fine for the far LOD).
    lod2_body = _collapse(lod1_body, f"WORK_{species}_LOD2_Body", spec.get("lod2_body_tris", spec["lod2_tris"] - 25))
    frames = lobe_frames(species) if spec.get("lobe_mode") == "mesh" else None
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
        o, region_of = _with_beads(body, f"SKM_{species}_LOD{level}", beads, "LOD", (frames, level) if frames else None)
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
        if frames:
            _rigid_lobes(o, lod0, frames)
        counts = [len(v.groups) for v in o.data.vertices]
        report[o.name] = dict(check(o), max_influences=max(counts), unweighted=counts.count(0))
    return report


# ---------------------------------------------------------------- variant modules

def _star_nose_mesh(name, nose, rn, cfg, phases):
    """One LOD of the rosette: a cupped scalloped ring around the nose bead (the bead shows through the hole), round
    lobes only (each lobe's outline is a half circle, never a point), closed at the back by a fan into the snout.
    phases: outline points per lobe, from its first valley (-0.5) towards the next one (+0.5). LOD0 draws the whole
    half circle (5 points) with a low forward crest per lobe; LOD1 keeps two points near the top of each lobe, so its
    outline is a soft 22-sided ring with no corner sharper than the LOD0 notches."""
    a = Vector((0.0, -1.0, -0.15)).normalized()          # forward along the snout, a little down
    u = Vector((1.0, 0.0, 0.0)); v = a.cross(u).normalized()
    c = nose - a * (rn * 0.25)
    n = cfg["lobes"]; r_in = rn * cfg["inner"]; rv = rn * cfg["valley"]; rp = rn * cfg["peak"]
    step = 2 * math.pi / n

    def at(theta, r, depth):
        return c + (u * math.cos(theta) + v * math.sin(theta)) * r + a * depth

    def cup(r):
        return -(r - r_in) * 0.30

    bm = bmesh.new()
    inner, outer, crest = [], [], []
    for k in range(n):
        t0 = math.pi / 2 + (k - 0.5) * step                # valley before lobe k
        inner.append(bm.verts.new(at(t0, r_in, 0.0)))
        lobe = []
        for phi in phases:
            r = rv + (rp - rv) * math.sqrt(max(0.0, 1.0 - (2 * phi) ** 2))
            lobe.append(bm.verts.new(at(t0 + (phi + 0.5) * step, r, cup(r))))
        outer.append(lobe)
        if len(phases) >= 4:
            rc = (r_in + rp) * 0.5
            crest.append(bm.verts.new(at(t0 + 0.5 * step, rc, cup(rc) + rn * 0.10)))
    back = bm.verts.new(c - a * (rn * 1.1))
    for k in range(n):
        i0, i1 = inner[k], inner[(k + 1) % n]
        ring = outer[k] + [outer[(k + 1) % n][0]]
        if crest:
            cr = crest[k]
            bm.faces.new((i0, i1, cr))
            bm.faces.new((i0, cr, ring[0]))
            bm.faces.new((cr, i1, ring[-1]))
            for j in range(len(ring) - 1):
                bm.faces.new((cr, ring[j + 1], ring[j]))
        else:
            bm.faces.new([i0, i1] + list(reversed(ring)))
        for j in range(len(ring) - 1):
            bm.faces.new((back, ring[j], ring[j + 1]))
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # The front must face forward (along a): flip everything if the recalculation chose the inside.
    front = [f for f in bm.faces if back not in f.verts]
    if front and sum(f.normal.dot(a) for f in front) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    _remove_object(name)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    # Detail region (the nose color) and mask A (hands and nose glow) on every vertex.
    col = me.color_attributes.new("PW_Regions", 'BYTE_COLOR', 'POINT')
    for d in col.data:
        d.color = NOSE
    uv = me.uv_layers.new(name="PW_Masks")
    for loop in me.loops:
        uv.data[loop.index].uv = (1.0, 0.0)
    return me


def build_star_nose(species="Mole"):
    """Variant module MOD_StarNose (Star Nose only): LOD0 and LOD1 meshes rigid on Head, none at LOD2. Named
    SKM_<species>_MOD_StarNose_LOD<n> so the body LODs stay first in name order; AnimalPipeline shows the module only on
    the variant whose moduleSlot asks for it."""
    spec = SPECS[species]; cfg = spec["star_nose"]
    arm = armature_of(species)
    nose = bpy.data.objects["WORK_Nose"]
    loc = nose.location.copy(); rn = nose.get("pw_radius", spec["nose"]["r"])
    report = {}
    for lod, phases in ((0, (-0.5, -0.3, -0.1, 0.1, 0.3)), (1, (-0.25, 0.25))):
        name = f"SKM_{species}_MOD_StarNose_LOD{lod}"
        me = _star_nose_mesh(name, loc, rn, cfg, phases)
        o = bpy.data.objects.new(name, me)
        bpy.data.collections["GEO" if lod == 0 else "LOD"].objects.link(o)
        bpy.data.collections["EXPORT"].objects.link(o)
        o.parent = arm
        mod = o.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
        g = o.vertex_groups.new(name="Head")
        g.add(list(range(len(me.vertices))), 1.0, 'REPLACE')
        report[name] = tris(o)
    return report


# ---------------------------------------------------------------- sockets

def place_sockets(species):
    """Standard SmallQuadruped sockets, world-aligned at rest (same convention for every species)."""
    spec = SPECS[species]
    arm = armature_of(species)
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
    # Species whose head, neck or tail sit far from the shared bones move those sockets; the parent bone stays the
    # family's (clips and the asset validator bind by the same hierarchy).
    for name, co in spec.get("sockets", {}).items():
        spec_sockets[name] = (Vector(co), spec_sockets[name][1])
    out = {}
    for name, (co, bone) in spec_sockets.items():
        o = bpy.data.objects[name]
        o.parent = arm; o.parent_type = 'BONE'; o.parent_bone = bone
        o.matrix_world = Matrix.Translation(co)
        out[name] = (tuple(round(c, 3) for c in co), bone)
    return out
