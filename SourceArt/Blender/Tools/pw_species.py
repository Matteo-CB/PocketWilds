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

Birds (T52) run the whole recipe in one call, from a MediumQuadruped file opened first:
    ps.build_bird("Owl", r".../BL_Animal_Fawn.blend"); pw_export.export_species_model("Owl")

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


# T51 Fox (A-SP-007) and Badger (A-SP-009), MediumQuadruped members placed on SK_MediumQuadruped by
# pw_rig.SPECIES_BONES (heads and lengths only, the family bone directions).
FOX_TAIL = [(0, 0.140, 0.215), (0, 0.205, 0.212), (0, 0.270, 0.218), (0, 0.330, 0.238), (0, 0.376, 0.272),
            (0, 0.402, 0.315)]
FOX_FRONT_LEG = [(0.042, -0.074, 0.165), (0.044, -0.080, 0.092), (0.045, -0.086, 0.040), (0.045, -0.092, 0.016)]
FOX_HIND_LEG = [(0.048, 0.100, 0.165), (0.051, 0.084, 0.110), (0.053, 0.112, 0.055), (0.053, 0.108, 0.016)]
# Fox ear: base on the skull and the upright ear axis (24 degrees outward, 8 back), +X side (pw_masks has the same).
FOX_EAR_BASE = (0.048, -0.176, 0.376)
FOX_EAR_AXIS = (0.403, 0.139, 0.904)

BADGER_TAIL = [(0, 0.228, 0.196), (0, 0.262, 0.184), (0, 0.290, 0.170)]
BADGER_FRONT_LEG = [(0.072, -0.098, 0.118), (0.073, -0.104, 0.056), (0.074, -0.108, 0.018)]
BADGER_HIND_LEG = [(0.076, 0.136, 0.112), (0.078, 0.138, 0.056), (0.078, 0.134, 0.018)]


def _fox_ears():
    """Tall soft triangle ears off the family ear bones' axis: a wide base, a narrower middle, a rounded tip (flat
    side forward). They turn as one piece with Ear_01 (ear_mode short, ear_zone)."""
    b = Vector(FOX_EAR_BASE); u = Vector(FOX_EAR_AXIS).normalized()
    return [_ell(tuple(b + u * along), half, rot=(-8, 24, 6), mirror=True)
            for along, half in ((0.026, (0.044, 0.019, 0.040)), (0.062, (0.030, 0.016, 0.034)), (0.094, (0.017, 0.013, 0.025)))]


def _eased_path(start, length, knots, step=0.005):
    """T51 polish: a tail centre line in the mid plane from its heading along the arc. knots: (arc length in metres, angle
    above horizontal in degrees, pointing back), eased between knots, so the bend stays gentle and a thick tube never
    folds on the inside of a curve. Points every step, from start (inside the rump) to the tip. The tube is built along
    it, and the tail weights and ring masks project onto the same polyline, so every LOD's rings fall on the same arc
    lengths."""
    def angle(s):
        for (s0, a0), (s1, a1) in zip(knots[:-1], knots[1:]):
            if s <= s1:
                t = max(0.0, min(1.0, (s - s0) / (s1 - s0)))
                return math.radians(a0 + (a1 - a0) * (1.0 - math.cos(math.pi * t)) * 0.5)
        return math.radians(knots[-1][1])
    y, z = start[1], start[2]
    out = [tuple(start)]
    n = int(round(length / step)); sub = 8
    for i in range(n):
        for k in range(sub):
            a = angle((i + (k + 0.5) / sub) * step)
            y += math.cos(a) * step / sub; z += math.sin(a) * step / sub
        out.append((0.0, round(y, 5), round(z, 5)))
    return out


# T51 ringed tails (tube_tail), held low (T51 polish): each leaves the rump 16 to 18 degrees above horizontal, arches
# gently back and down and lifts its round tip a little. The Raccoon's droops lower; the Red Panda's, thicker, stays a
# little higher and longer. The far half also follows Root (tail_root), so the clips that tip the pelvis back or lower
# the body (RaccoonWash, RedPandaStand, Sleep) never bring it to the ground.
RACCOON_TAIL = _eased_path((0, 0.170, 0.143), 0.228, [(0.0, 16), (0.035, 16), (0.145, -40), (0.228, 3)])
REDPANDA_TAIL = _eased_path((0, 0.170, 0.147), 0.232, [(0.0, 18), (0.040, 18), (0.150, -34), (0.232, 5)])

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
    # Fox (T51, A-SP-007): soft triangular face with white cheek ruffs, big upright ears turning with the ear bones, a
    # compact chest with a ruff, short legs with dark socks, a huge plume on the three tail bones. About 75 x 48 cm.
    "Fox": dict(
        armature="SK_MediumQuadruped",
        blockout=[
            _ell((0, -0.188, 0.320), (0.090, 0.083, 0.080), part="head"),
            _ell((0.052, -0.208, 0.288), (0.052, 0.047, 0.044), stiff=1.6, mirror=True, part="head"),   # cheek ruffs
            _ell((0.086, -0.184, 0.274), (0.027, 0.030, 0.025), stiff=1.5, mirror=True, part="head"),   # ruff tufts
            _ell((0, -0.262, 0.290), (0.031, 0.056, 0.030), rot=(-10, 0, 0), stiff=1.5, part="head"),  # muzzle
            _ell((0, -0.128, 0.254), (0.054, 0.052, 0.064), rot=(28, 0, 0)),                              # neck
            _ell((0, -0.120, 0.214), (0.054, 0.040, 0.062), stiff=1.6),                                   # chest ruff
            _ell((0, -0.068, 0.202), (0.074, 0.068, 0.081)),                                              # chest
            _ell((0, 0.020, 0.200), (0.072, 0.110, 0.078)),                                               # body
            _ell((0, 0.096, 0.204), (0.074, 0.066, 0.077)),                                               # rump
            _ell((0.046, 0.094, 0.168), (0.036, 0.052, 0.060), mirror=True),                              # haunches
            _ell((0.045, -0.098, 0.019), (0.024, 0.032, 0.019), mirror=True),                             # front paws
            _ell((0.053, 0.102, 0.019), (0.024, 0.034, 0.019), mirror=True),                              # hind paws
            # The plume: low behind the rump, wide in the middle, the tip curling up (one solid volume).
            _ell((0, 0.172, 0.214), (0.040, 0.046, 0.040), axis=(0, 1.0, -0.04)),
            _ell((0, 0.236, 0.214), (0.058, 0.060, 0.056), axis=(0, 1.0, 0.04)),
            _ell((0, 0.296, 0.226), (0.070, 0.062, 0.066), axis=(0, 0.96, 0.26)),
            _ell((0, 0.348, 0.252), (0.064, 0.056, 0.060), axis=(0, 0.80, 0.60)),
            _ell((0, 0.384, 0.292), (0.046, 0.046, 0.044), axis=(0, 0.55, 0.84)),
        ] + _fox_ears() + _legs(FOX_FRONT_LEG, (0.029, 0.025, 0.023)) + _legs(FOX_HIND_LEG, (0.033, 0.026, 0.023)),
        ear_mode="short",
        ear_gain=0.7,
        ear_zone=(0.362, 0.080, -0.30, 1.45, 0.0, 0.30),
        tail_path=FOX_TAIL,
        tail_member_y=(0.125, 0.150),
        tail_split=(0.07, 0.13, 0.17, 0.23),
        eyes=dict(x=0.040, z=0.330, r=0.022, sink=0.35),
        nose=dict(x=0.0, z=0.296, r=0.013, sink=0.60),
        head_center=(0, -0.188, 0.320),
        head_radii=(0.090, 0.083, 0.080),
        hind_leg_band=(0.150, 0.105),
        front_leg_band=(0.150, 0.105),
        lod0_faces=1650,
        lod1_mode="collapse",
        lod1_tris=1300,
        lod2_tris=560,
        back_probe_y=0.03,
        sockets={
            "SOCK_Neck": (0.0, -0.120, 0.258),
            "SOCK_TailBase": (0.0, 0.150, 0.222),
            "SOCK_Ear_L": (-0.048, -0.176, 0.378),
            "SOCK_Ear_R": (0.048, -0.176, 0.378),
        },
    ),
    # Badger (T51, A-SP-009): a low heavy rounded body, a long tapered head flowing from the shoulders with the face
    # stripes painted by pw_masks, small round ears on the head, short thick legs, a short tail. About 69 x 29 cm.
    "Badger": dict(
        armature="SK_MediumQuadruped",
        blockout=[
            _ell((0, -0.230, 0.218), (0.080, 0.086, 0.064), part="head"),
            _ell((0.050, -0.250, 0.200), (0.044, 0.048, 0.040), stiff=1.6, mirror=True, part="head"),   # cheeks
            _ell((0, -0.322, 0.199), (0.028, 0.068, 0.026), rot=(-3, 0, 0), stiff=1.5, part="head"),   # tapered snout
            _ell((0.068, -0.206, 0.256), (0.022, 0.011, 0.017), rot=(-10, 45, 30), stiff=2.0, mirror=True, part="head"),  # ears
            _ell((0, -0.160, 0.196), (0.086, 0.068, 0.074)),                                              # thick neck
            _ell((0, -0.092, 0.180), (0.098, 0.090, 0.090)),                                              # chest
            _ell((0, 0.035, 0.182), (0.106, 0.165, 0.094)),                                               # low heavy body
            _ell((0, 0.158, 0.184), (0.104, 0.092, 0.096)),                                               # rump
            _ell((0.068, 0.140, 0.128), (0.046, 0.058, 0.058), mirror=True),                              # haunches
            _ell((0.074, -0.120, 0.017), (0.030, 0.041, 0.017), mirror=True),                             # front paws
            _ell((0.078, 0.126, 0.017), (0.031, 0.042, 0.017), mirror=True),                              # hind paws
            _ell((0, 0.256, 0.180), (0.030, 0.046, 0.025), rot=(-15, 0, 0), stiff=1.6),                   # short tail
        ] + _legs(BADGER_FRONT_LEG, (0.035, 0.031)) + _legs(BADGER_HIND_LEG, (0.037, 0.032)),
        ear_mode="none",
        tail_path=BADGER_TAIL,
        tail_member_y=(0.225, 0.245),
        eyes=dict(x=0.038, z=0.230, r=0.019, sink=0.35),
        nose=dict(x=0.0, z=0.202, r=0.017, sink=0.45),
        head_center=(0, -0.230, 0.218),
        head_radii=(0.080, 0.086, 0.064),
        hind_leg_band=(0.115, 0.080),
        front_leg_band=(0.115, 0.080),
        lod0_faces=1640,
        lod1_mode="collapse",
        lod1_tris=1150,
        lod2_tris=480,
        back_probe_y=0.03,
        sockets={
            "SOCK_Neck": (0.0, -0.150, 0.222),
            "SOCK_TailBase": (0.0, 0.236, 0.196),
            "SOCK_Ear_L": (-0.066, -0.206, 0.252),
            "SOCK_Ear_R": (0.066, -0.206, 0.252),
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
    # Raccoon (T51, A-SP-008, Forest): a round chubby body a little higher at the rump, a big round head with fluffy cheek
    # ruffs, a short muzzle with a small tip, small round ears on the head, short legs with little hands, a fat bushy ringed
    # tail held low behind (tube_tail: clean loops on the ring edges, so the rings stay crisp on LOD1). The dark face mask, the
    # brows and the rings are vertex masks (pw_masks raccoon_masks). Modelled on the Rabbit bones; the prefab is scaled up
    # uniformly to its size (about 62 x 38 cm).
    "Raccoon": dict(
        blockout=[
            _ell((0, -0.112, 0.222), (0.100, 0.090, 0.088), part="head"),
            # Fluffy cheek ruffs, wider than the skull and tilted down and out: the raccoon's face shape.
            _ell((0.074, -0.100, 0.210), (0.048, 0.038, 0.036), rot=(0, 10, 0), stiff=1.2, mirror=True, part="head"),
            _ell((0, -0.188, 0.192), (0.034, 0.044, 0.030), rot=(-8, 0, 0), stiff=1.5, part="head"),  # short muzzle
            _ell((0, -0.224, 0.196), (0.017, 0.016, 0.015), stiff=1.5, part="head"),  # small nose tip
            _ell((0.066, -0.094, 0.300), (0.034, 0.013, 0.032), rot=(-4, 14, 22), stiff=2.2, mirror=True, part="head"),  # round ears
            _ell((0, -0.035, 0.135), (0.080, 0.074, 0.084)),                         # chest
            _ell((0, 0.045, 0.135), (0.090, 0.106, 0.090)),                          # round body
            _ell((0, 0.115, 0.141), (0.088, 0.076, 0.088)),                          # rump, a little higher
            _ell((0.060, 0.112, 0.092), (0.038, 0.052, 0.050), mirror=True),        # haunches
            _ell((0.064, 0.100, 0.042), (0.026, 0.030, 0.036), mirror=True),        # short hind legs
            _ell((0.064, 0.082, 0.014), (0.025, 0.042, 0.014), mirror=True),        # hind feet
            _ell((0.050, -0.058, 0.060), (0.024, 0.025, 0.048), mirror=True),       # short front legs
            _ell((0.052, -0.076, 0.014), (0.024, 0.030, 0.014), mirror=True),       # little hands
        ],
        tail_path=RACCOON_TAIL,
        tail_member_y=(0.176, 0.204),
        # radius: (fraction of the length, radius) for a soft swell; rings: dark bands as fractions (the last one runs into
        # the round tip); step / edge / tip loops per LOD; segs around per LOD; oval: side and up half-size factors.
        # T51 polish: thick and bushy (10 cm across at its fullest, a little wider than tall), blunt round tip.
        tube_tail=dict(radius=[(0.0, 0.034), (0.25, 0.046), (0.58, 0.050), (1.0, 0.045)],
                       rings=[(0.27, 0.37), (0.47, 0.57), (0.67, 0.77), (0.87, 1.01)], cap=0.16,
                       step=(0.045, 0.085, 0.15), edge=(0.012, 0.016, 0.0), tip=(4, 2, 1), segs=(14, 8, 6), oval=(1.04, 0.95)),
        # The tail motion fades in over 5 cm from where it leaves the rump (tail_member_s, arc lengths in metres), so its
        # root never moves against the body; Root takes 40 % of the far half, so RaccoonWash and Sleep keep it off the ground.
        tail_member_s=(0.020, 0.075),
        tail_root=(0.10, 0.20, 0.40),
        eyes=dict(x=0.044, z=0.230, r=0.019, sink=0.35),
        nose=dict(x=0.0, z=0.198, r=0.013, sink=0.45),
        head_center=(0, -0.112, 0.222),
        head_radii=(0.100, 0.090, 0.088),
        ear_mode="none",
        hind_leg_band=(0.100, 0.060),
        front_leg_band=(0.100, 0.060),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=1060,
        lod1_mode="collapse",
        lod1_tris=1080,
        lod1_body_tris=740,
        lod2_tris=440,
        lod2_body_tris=290,
        back_probe_y=0.050,
        sockets={
            "SOCK_Neck": (0.0, -0.066, 0.160),
            "SOCK_TailBase": (0.0, 0.196, 0.160),
            "SOCK_Ear_L": (-0.066, -0.094, 0.324),
            "SOCK_Ear_R": (0.066, -0.094, 0.324),
        },
    ),
    # Red Panda (T51, A-SP-011, Forest): a round face with white cheek patches, a short round muzzle, small soft triangle
    # ears, a plush body slightly hunched (the rump higher than the shoulders), sturdy short legs, a very thick ringed tail,
    # about as wide as the thigh, held low and sweeping back (tube_tail). Face marks, dark legs and rings are vertex masks
    # (pw_masks redpanda_masks).
    # Modelled on the Rabbit bones; the prefab is scaled up uniformly to its size (about 66 x 40 cm).
    "RedPanda": dict(
        blockout=[
            _ell((0, -0.106, 0.208), (0.090, 0.080, 0.080), part="head"),
            _ell((0.058, -0.118, 0.186), (0.048, 0.044, 0.042), stiff=1.6, mirror=True, part="head"),  # round cheeks
            _ell((0, -0.176, 0.180), (0.034, 0.036, 0.028), stiff=1.5, part="head"),  # short round muzzle
            _ell((0, -0.205, 0.184), (0.017, 0.014, 0.014), stiff=1.5, part="head"),  # nose tip
            _ell((0.060, -0.092, 0.270), (0.033, 0.011, 0.026), rot=(-8, 16, 20), stiff=2.2, mirror=True, part="head"),  # ear base
            _ell((0.071, -0.090, 0.296), (0.017, 0.010, 0.019), rot=(-8, 16, 20), stiff=2.2, mirror=True, part="head"),  # ear tip
            _ell((0, -0.040, 0.136), (0.086, 0.080, 0.088)),                         # chest
            _ell((0, 0.048, 0.142), (0.100, 0.114, 0.098)),                          # plush body
            _ell((0, 0.120, 0.150), (0.094, 0.080, 0.094)),                          # rump, higher: the soft hunch
            _ell((0.064, 0.115, 0.096), (0.042, 0.056, 0.056), mirror=True),        # haunches
            _ell((0.066, 0.098, 0.044), (0.030, 0.034, 0.040), mirror=True),        # sturdy hind legs
            _ell((0.068, 0.080, 0.015), (0.027, 0.045, 0.015), mirror=True),        # hind feet
            _ell((0.052, -0.055, 0.060), (0.028, 0.028, 0.050), mirror=True),       # sturdy front legs
            _ell((0.054, -0.074, 0.015), (0.027, 0.032, 0.015), mirror=True),       # front paws
        ],
        tail_path=REDPANDA_TAIL,
        tail_member_y=(0.176, 0.204),
        # T51 polish: 11 cm across at its fullest, like the thigh seen from the side, nearly even to the round tip.
        tube_tail=dict(radius=[(0.0, 0.038), (0.22, 0.050), (0.55, 0.056), (1.0, 0.049)],
                       rings=[(0.24, 0.33), (0.43, 0.52), (0.62, 0.71), (0.81, 0.89)], cap=0.16,
                       step=(0.045, 0.085, 0.15), edge=(0.012, 0.016, 0.0), tip=(4, 2, 1), segs=(14, 10, 6), oval=(1.04, 0.95)),
        # The tail motion fades in over 5 cm from where it leaves the rump (tail_member_s), so its root never moves against
        # the body; Root takes 40 % of the far half, so RedPandaStand and Sleep keep it off the ground.
        tail_member_s=(0.030, 0.085),
        tail_root=(0.10, 0.20, 0.40),
        eyes=dict(x=0.040, z=0.218, r=0.019, sink=0.35),
        nose=dict(x=0.0, z=0.186, r=0.013, sink=0.45),
        head_center=(0, -0.106, 0.208),
        head_radii=(0.090, 0.080, 0.080),
        ear_mode="none",
        hind_leg_band=(0.100, 0.060),
        front_leg_band=(0.100, 0.060),
        leg_band_bones=("Upper", "Lower"),
        lod0_faces=1150,
        lod1_mode="collapse",
        lod1_tris=1200,
        lod1_body_tris=830,
        lod2_tris=500,
        lod2_body_tris=330,
        back_probe_y=0.050,
        sockets={
            "SOCK_Neck": (0.0, -0.066, 0.156),
            "SOCK_TailBase": (0.0, 0.198, 0.162),
            "SOCK_Ear_L": (-0.064, -0.091, 0.296),
            "SOCK_Ear_R": (0.064, -0.091, 0.296),
        },
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


# T52 Boar Piglet (A-SP-012), the first HeavyQuadruped (pw_rig.HEAVY_QUADRUPED is fitted to it): a round barrel body, a
# big round head with chubby cheeks, a short snout ending in a round disc with two nostril beads (nose pair), bead eyes,
# soft triangle ears leaning forward on the ear bones, short stubby legs with rounded hooves, a little curly tail (tube
# tail). Stripes, spots and the snout disc are vertex masks (pw_masks boarpiglet_masks). About 64 x 43 cm. Built from the
# Badger file: start_from, then the SK_MediumQuadruped armature is swapped for pw_rig.build_armature("HeavyQuadruped").
BOARPIGLET_FRONT_LEG = [(0.064, -0.062, 0.125), (0.065, -0.065, 0.070), (0.066, -0.067, 0.020)]
BOARPIGLET_HIND_LEG = [(0.068, 0.128, 0.120), (0.070, 0.123, 0.070), (0.070, 0.126, 0.020)]
# Ear base on the skull and the ear axis (26 degrees outward, 34 forward), +X side; pw_masks has the same.
BOARPIGLET_EAR_BASE = (0.062, -0.180, 0.362)
BOARPIGLET_EAR_AXIS = (0.363, -0.559, 0.745)
# The curl: leaves the rump 28 degrees above horizontal, turns up and over (a little more than half a turn), tip forward.
BOARPIGLET_TAIL = _eased_path((0, 0.198, 0.240), 0.085, [(0.0, 28), (0.024, 28), (0.068, 212), (0.085, 228)])


def _boarpiglet_ears():
    b = Vector(BOARPIGLET_EAR_BASE); u = Vector(BOARPIGLET_EAR_AXIS).normalized()
    return [_ell(tuple(b + u * along), half, rot=(34, 26, 0), mirror=True)
            for along, half in ((0.024, (0.048, 0.013, 0.040)), (0.060, (0.030, 0.011, 0.030)))]


SPECS["BoarPiglet"] = dict(
    armature="SK_HeavyQuadruped",
    blockout=[
        _ell((0, -0.195, 0.280), (0.118, 0.105, 0.110), part="head"),
        _ell((0.054, -0.228, 0.245), (0.054, 0.060, 0.052), stiff=1.6, mirror=True, part="head"),   # chubby cheeks
        _ell((0, -0.298, 0.246), (0.047, 0.050, 0.043), rot=(-4, 0, 0), stiff=1.6, part="head"),   # snout
        _ell((0, -0.340, 0.250), (0.051, 0.016, 0.045), stiff=2.0, part="head"),                     # snout disc
        _ell((0, -0.120, 0.232), (0.095, 0.060, 0.095)),                                             # neck
        _ell((0, -0.045, 0.207), (0.114, 0.085, 0.114)),                                             # chest
        _ell((0, 0.050, 0.200), (0.122, 0.135, 0.120)),                                              # round barrel body
        _ell((0, 0.130, 0.207), (0.114, 0.080, 0.114)),                                              # rump
        _ell((0.062, -0.058, 0.140), (0.048, 0.055, 0.060), mirror=True),                            # shoulders
        _ell((0.066, 0.128, 0.140), (0.050, 0.060, 0.066), mirror=True),                             # haunches
        _ell((0.066, -0.071, 0.016), (0.028, 0.032, 0.016), mirror=True),                            # front hooves
        _ell((0.070, 0.122, 0.016), (0.029, 0.033, 0.016), mirror=True),                             # hind hooves
    ] + _boarpiglet_ears() + _legs(BOARPIGLET_FRONT_LEG, (0.032, 0.029)) + _legs(BOARPIGLET_HIND_LEG, (0.034, 0.030)),
    ear_mode="short",
    ear_gain=0.8,
    ear_zone=(0.345, 0.050, -0.10, 1.30, 0.10, 0.40),
    tail_path=BOARPIGLET_TAIL,
    tube_tail=dict(radius=[(0.0, 0.0120), (0.4, 0.0095), (1.0, 0.0085)], rings=[], cap=0.12,
                   step=(0.065, 0.12, 0.25), edge=(0.0, 0.0, 0.0), tip=(3, 2, 1), segs=(8, 6, 4), oval=(1.0, 1.0)),
    # Only the tube behind the rump follows the tail bones (the round back is as high as the tail: no height band); the
    # curl hands over to Tail_02 after 2.5 to 5 cm (the third split pair is past the tip: the family has two tail bones).
    tail_member_y=(0.216, 0.228),
    tail_member_s=(0.010, 0.030),
    tail_member_z=(9.0, 9.5),
    tail_split=(0.025, 0.050, 9.0, 9.1),
    eyes=dict(x=0.054, z=0.298, r=0.025, sink=0.35),
    nose=dict(x=0.018, z=0.252, r=0.0105, sink=0.45, pair=True),
    head_center=(0, -0.195, 0.280),
    head_radii=(0.118, 0.105, 0.110),
    hind_leg_band=(0.120, 0.080),
    front_leg_band=(0.120, 0.080),
    lod0_faces=1650,
    lod1_mode="collapse",
    lod1_tris=1300,
    lod1_body_tris=1120,
    lod2_tris=560,
    lod2_body_tris=470,
    back_probe_y=0.03,
    sockets={
        "SOCK_Neck": (0.0, -0.118, 0.222),
        "SOCK_TailBase": (0.0, 0.212, 0.246),
        "SOCK_Ear_L": (-0.062, -0.180, 0.362),
        "SOCK_Ear_R": (0.062, -0.180, 0.362),
    },
)


# T52 Owl (A-SP-010, Forest), the first Bird on SK_Bird (pw_rig.BIRD). T52 polish: one smooth egg with the head merged
# in (a superellipse crown, slightly flattened, soft shoulders, a rounder belly at the front), no metaball seams. Sculpted
# by offsets along the normal (surface_mesh): two large facial discs (a shallow dish and a soft raised rim, cut crisp by
# cut_mask_shapes), short rounded wings as raised plates on the sides (soft edge, a faint crease around), a short tail fan
# low on the back; tiny toes peek out under the belly (exact union). Big bead eyes centred in the discs, a small drop beak
# (the nose bead, shape_beak). No individual feather. About 35 x 44 cm. Built by build_bird("Owl", <Fawn file>): the
# surface drives the wing and tail bones (surface_parts), the toe ellipsoids the feet. The mask rule reads the same data
# (pw_masks owl_masks, disc frames in pw_masks OWL_DISCS).
SPECS["Owl"] = dict(
    armature="SK_Bird",
    trunk_bone="Body",
    surface=dict(
        # Egg: centre height, top and bottom, half width, front depth (belly low, face high), back depth, exponents
        # (horizontal p, crown q_top: above 2 flattens the crown into soft shoulders).
        # gentle: collapse weight of the smooth body (surface_collapse_weights).
        gentle=0.985,
        egg=dict(zc=0.218, top=0.440, bottom=0.008, a=0.162, b_belly=0.160, b_face=0.134, face_z=(0.16, 0.33),
                 b_back=0.146, p=2.15, q_top=3.1, q_bottom=2.5),
        # Facial discs (frames: pw_masks OWL_DISCS): offset along the normal by radius, a dish then a soft rim whose
        # crest spans the mask A rim ring (radii 0.044 to 0.056, cut rings at 0.044, 0.0475, 0.056, 0.0595).
        disc_profile=((0.0, -0.0055), (0.030, -0.0050), (0.044, -0.0020), (0.0475, 0.0030), (0.056, 0.0032),
                      (0.0595, 0.0006), (0.066, -0.0008), (0.080, 0.0)),
        # Wings, side view (y, z): top at the shoulder, tip low at the back, half width, widest at tc, thickness (thin at
        # the shoulder), soft edge (s), faint crease; bone_edge, top_fade and lower_t place the Wing_Upper / Wing_Lower
        # weights.
        wing=dict(top=(-0.010, 0.262), tip=(0.094, 0.060), half=0.060, tc=0.40, thick=0.016, thin_top=0.20,
                  edge=(0.74, 1.0), crease=0.0015, bone_edge=(0.76, 1.40), top_fade=(0.0, 0.30), lower_t=(0.45, 0.70)),
        # Tail fan, back view (x, z): centre, half sizes (rounded square outline), thickness (thin top, full lower edge).
        tail=dict(center=(0.0, 0.094), half=(0.050, 0.026), thick=0.012, thin_top=0.25, edge=(0.66, 1.0),
                  bone_edge=(0.90, 1.12)),
        # Toes: pivot of the +X foot, yaw of each toe, toe half sizes, reach from the pivot; heel: an optional pad
        # (centre, half sizes); pad: the feathered swell of the underside over each foot ((x, y), height, (sx, sy)).
        # The toes ride Root (root_band): AN_Bird leans tip the bird forward about y = -0.078 (LEAN_LIFT), so toes this
        # far back stay within 2 mm of the ground through Eat and Social.
        toes=dict(pivot=(0.050, -0.0600, 0.0095), yaws=(-26, 0, 26), half=(0.0110, 0.0150, 0.0095), reach=0.015,
                  heel=None, pad=((0.050, -0.060), 0.013, (0.026, 0.026))),
    ),
    # Metaball reference of the same volumes (WORK, editable); the head and foot parts drive the skinning.
    blockout=[
        _ell((0, 0.004, 0.226), (0.162, 0.152, 0.212), part="trunk"),                                # egg
        _ell((0, -0.005, 0.335), (0.154, 0.135, 0.105), part="head"),                                # head, merged
        _ell((0.064, -0.118, 0.318), (0.064, 0.030, 0.064), rot=(0, 0, 26), mirror=True, part="head"),  # facial discs
        _ell((0.050, -0.072, 0.009), (0.024, 0.022, 0.010), mirror=True, part="foot"),               # toes
    ],
    ear_mode="none",
    # Big bead eyes centred in the discs; segs (u, v) per LOD with the pole facing out, so their outline is round.
    eyes=dict(x=0.064, z=0.318, r=0.033, sink=0.30, segs=((16, 5), (8, 4), (8, 3))),
    # The nose bead is the beak: shape_beak draws it into a little drop pointing down.
    nose=dict(x=0.0, z=0.272, r=0.014, sink=0.40),
    head_center=(0, -0.005, 0.335),
    head_radii=(0.154, 0.135, 0.105),
    # Part bones (apply_part_weights): wings and tail come from the surface (surface_parts); the tiny hidden feet stay on
    # Root with the lowest body (root_band), the Leg and Foot bones carry no skin.
    part_weights={},
    part_strip=("Wing", "Leg", "Foot"),
    # The lowest body follows Root, so a bow never sinks the belly into the ground.
    root_band=(0.035, 0.110),
    # The merged head hands over to the body along a band (surface_head_weights): low and wide at the front under the
    # rigid discs, above the wing tops on the sides and the back; Neck takes up to half at its middle.
    head_z=dict(front=(0.150, 0.262), back=(0.245, 0.330), neck=0.5),
    ground_seat=0.0035,
    lod0_tris=1530,
    lod1_mode="collapse",
    lod1_tris=960,
    lod1_body_tris=540,
    lod2_tris=420,
    lod2_body_tris=390,
    sockets={
        "SOCK_HeadTop": (0.0, -0.020, 0.440),
        "SOCK_Neck": (0.0, -0.149, 0.236),
        "SOCK_Back": (0.0, 0.145, 0.262),
        "SOCK_TailBase": (0.0, 0.137, 0.106),
    },
)


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
    if SPECS[species].get("surface"):
        return surface_mesh(species)  # T52 polish: an analytic body (birds)
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
    for side, (base, tip) in (ear_axes(arm).items() if spec.get("ears") else ()):  # T52: the Bird has no ear bones
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
        o["pw_normal"] = tuple(normal)  # T52 polish: eyes.segs beads face out along it
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
    for bead in beads:
        center, u, v, r, tag = bead[:5]
        b = bmesh.new()
        bmesh.ops.create_uvsphere(b, u_segments=u, v_segments=v, radius=r)
        if len(bead) > 5:
            # T52 polish: the pole faces out (bead[5]), so the rim where the bead meets the face is one round ring.
            bmesh.ops.rotate(b, verts=b.verts, cent=(0, 0, 0), matrix=Vector(bead[5]).to_track_quat('Z', 'Y').to_matrix())
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


def _nose_pair(spec, src, u, v):
    """T52: nose=dict(pair=True) makes the nose bead one of two nostrils on a snout disc (Boar Piglet): the mirrored one."""
    if not spec["nose"].get("pair"):
        return []
    loc, r = src["WORK_Nose"]
    return [(Vector((-loc.x, loc.y, loc.z)), u, v, r, NOSE)]


def _eye_bead(spec, src, name, level, u, v):
    """An eye bead for LOD level: default (u, v), or spec eyes "segs" per LOD with the pole facing out (T52 polish)."""
    segs = spec["eyes"].get("segs")
    if not segs:
        return (src[name][0], u, v, src[name][1], EYE)
    return (src[name][0], segs[level][0], segs[level][1], src[name][1], EYE, Vector(bpy.data.objects[name]["pw_normal"]))


def _bead_sources():
    return {n: (bpy.data.objects[n].location.copy(), bpy.data.objects[n].get("pw_radius", 0.011 if n == "WORK_Nose" else 0.023))
            for n in ("WORK_Eye_R", "WORK_Eye_L", "WORK_Nose")}


def build_lod0(species):
    spec = SPECS[species]
    hi = _hi_mesh(species)
    if spec.get("lod0_tris"):
        # T52 polish: an analytic surface is collapsed straight to the LOD0 budget; the collapse keeps the disc rims,
        # wing edges and toes where a coarse quad remesh would crumple them.
        src = bpy.data.objects.new(f"WORK_{species}_Hi", hi); bpy.data.collections["WORK"].objects.link(src)
        body = _collapse(src, f"WORK_{species}_BodyRemesh", spec["lod0_tris"], surface_collapse_weights(species, hi))
        bpy.data.objects.remove(src, do_unlink=True)
        _beautify_gentle(body)
        for v in body.data.vertices:
            v.co.z = max(0.0, v.co.z)
    else:
        body = _quad_body(f"WORK_{species}_BodyRemesh", hi, spec["lod0_faces"], pre_decimate=0.5)
    bpy.data.meshes.remove(hi)
    src = _bead_sources()
    beads = [_eye_bead(spec, src, n, 0, 8, 6) for n in ("WORK_Eye_R", "WORK_Eye_L")] + [
             (src["WORK_Nose"][0], 6, 4, src["WORK_Nose"][1], NOSE)]
    beads += _nose_pair(spec, src, 6, 4)
    lobes =(lobe_frames(species), 0) if spec.get("lobe_mode") == "mesh" else None
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
    # T51 polish: tail_member_s moves the start of the tail motion past the rump (thick tube tails), so the ring where
    # the tail meets the body moves with the body and never presses into it or lifts out of it.
    m0, m1 = spec.get("tail_member_s", (0.005, 0.05))
    # T52: the height band is species data too (a round back as high as the tail, Boar Piglet, must stay off the tail).
    z0, z1 = spec.get("tail_member_z", (0.30, 0.36))
    member = smooth(m0, m1, s) * max(smooth(y0, y1, co.y), smooth(z0, z1, co.z))
    upper = smooth(0.08, 0.20, s)
    # A thin tail far below the tail bones (Field Mouse) takes only part of their motion; the rest follows the pelvis.
    gain = spec.get("tail_gain", 1.0)
    weights = {"Tail_01": (1.0 - upper) * gain, "Tail_02": upper * gain}
    split = spec.get("tail_split")
    if split:
        # T51: a long plume (Fox) hands over along its arc length to Tail_02, then Tail_03 (arc lengths are species data).
        t2 = smooth(split[0], split[1], s); t3 = smooth(split[2], split[3], s)
        weights = {"Tail_01": (1.0 - t2) * gain, "Tail_02": (t2 - t3) * gain, "Tail_03": t3 * gain}
    if gain < 1.0:
        weights["Pelvis"] = 1.0 - gain
    root = spec.get("tail_root")
    if root:
        # T51 polish: a low heavy tail (Raccoon, Red Panda) hands a share of its far part to Root (arc lengths s0, s1 and
        # the share are species data). The base keeps the full tail motion; the far part keeps the flicks and sways at a
        # softer amplitude and stays near its rest height when a clip tips the pelvis back or lowers the body.
        k = root[2] * smooth(root[0], root[1], s)
        weights = {b: x * (1.0 - k) for b, x in weights.items()}
        weights["Root"] = weights.get("Root", 0.0) + k
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
        # T51: the ear zone (lowest z, radius around the ear bones, t range, blend range) is species data; the defaults
        # are the Squirrel's. The Fox's tall upright ears stand off its ear bones' axis.
        z0, r0, ta, tb, b0, b1 = spec.get("ear_zone", (0.37, 0.05, -0.15, 0.65, -0.15, 0.10))
        if co.z < z0 or radial > r0 or t < ta or t > tb:
            return None
        # Small ears turn as one piece with a damped share of the family ear motion. The blend starts
        # inside the skull so the ear base bends over 5 cm instead of folding.
        ear = spec.get("ear_gain", 1.0) * smooth(b0, b1, t)
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
            w = {spec.get("trunk_bone", "Spine"): 1.0}
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

    # T51 polish: a tube tail (add_tail_tube) is a shell of its own, so heat weights give its root whichever bone is
    # nearest (Spine on the Red Panda) while the rump around it follows Pelvis, and a clip that bends the back pushed the
    # root out through the body. Its body share now copies the nearest body vertex, so the ring where the tail meets the
    # body moves exactly with it; the tail rule below then blends in the tail bones along the tail.
    tube_attr = me.attributes.get("PW_TailTube")
    tube_ids = [v.index for v in me.vertices if tube_attr.data[v.index].value == 1] if tube_attr is not None else []
    if tube_ids:
        tubeset = set(tube_ids)
        body_ids = [v.index for v in me.vertices if v.index not in tubeset]
        kd = KDTree(len(body_ids))
        for i, k in enumerate(body_ids):
            kd.insert(me.vertices[k].co, i)
        kd.balance()
        snap = {k: _get(vg, me.vertices[k]) for k in body_ids}
        for k in tube_ids:
            _, i, _ = kd.find(me.vertices[k].co)
            _put(vg, me.vertices[k], snap[body_ids[i]])

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

def _collapse(src, name, target_tris, weights=None):
    """Symmetric triangle collapse of a body mesh down to target_tris. weights (T52 polish): per vertex 0..1, lower
    values collapse less (1 everywhere is the plain collapse)."""
    _remove_object(name)
    me = src.data.copy(); me.name = name
    o = bpy.data.objects.new(name, me); bpy.data.collections["WORK"].objects.link(o)
    o.hide_viewport = False
    if weights is not None:
        g = o.vertex_groups.new(name="WORK_Collapse")
        for i, w in enumerate(weights):
            g.add([i], w, 'REPLACE')
    _select_only(o)
    area, region = view3d()
    with bpy.context.temp_override(area=area, region=region, object=o, active_object=o,
                                   selected_objects=[o], selected_editable_objects=[o]):
        d = o.modifiers.new("Decimate", 'DECIMATE'); d.decimate_type = 'COLLAPSE'
        d.use_symmetry = True; d.symmetry_axis = 'X'; d.use_collapse_triangulate = True
        d.ratio = target_tris / max(1, tris(src))
        if weights is not None:
            d.vertex_group = "WORK_Collapse"
        bpy.ops.object.modifier_apply(modifier="Decimate")
    if weights is not None:
        o.vertex_groups.remove(o.vertex_groups["WORK_Collapse"])
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
    lod1_beads = [_eye_bead(spec, src, n, 1, 6, 4) for n in ("WORK_Eye_R", "WORK_Eye_L")] + [
                  (src["WORK_Nose"][0], 5, 3, src["WORK_Nose"][1], NOSE)] + _nose_pair(spec, src, 5, 3)
    # LOD2 eyes: slightly bigger, re-placed on the simplified surface so they never sink.
    bb = bmesh.new(); bb.from_mesh(lod2_body.data); bvh2 = BVHTree.FromBMesh(bb); bb.free()
    lod2_beads = []
    for n in ("WORK_Eye_R", "WORK_Eye_L"):
        loc, r = src[n]
        hit, normal, _, _ = bvh2.ray_cast(Vector((loc.x, -1.0, loc.z)), Vector((0, 1, 0)))
        rr = r * 1.13
        segs = spec["eyes"].get("segs")
        lod2_beads.append((hit - normal * (rr * 0.25), 6, 3, rr, EYE) if not segs else
                          (hit - normal * (rr * 0.25), segs[2][0], segs[2][1], rr, EYE, normal))
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


# ---------------------------------------------------------------- tube tails (T51)

def tube_profile(cfg, f):
    """Tail radius at fraction f of its length: a soft swell through the radius points, then a round tip over the cap."""
    cap = cfg["cap"]; pts = cfg["radius"]
    g = min(f, 1.0 - cap)
    r = pts[-1][1]
    for (f0, r0), (f1, r1) in zip(pts[:-1], pts[1:]):
        if g <= f1:
            r = r0 + (r1 - r0) * smooth(f0, f1, g)
            break
    if f > 1.0 - cap:
        u = min(1.0, (f - (1.0 - cap)) / cap)
        r *= math.sqrt(max(0.0, 1.0 - u * u))
    return r


def tube_loops(cfg, level):
    """Loop fractions along the tail: an even run, a pair of loops around every ring edge (the color turns between them,
    so the rings stay crisp at every LOD; a single loop when edge is 0), and a round tip."""
    cap = cfg["cap"]; step = cfg["step"][level]; d = cfg["edge"][level]
    fixed = []
    for a, b in cfg["rings"]:
        for e in (a, b):
            if e < 1.0:
                fixed += [e - d, e + d] if d > 0 else [e]
    tip = cfg["tip"][level]
    capf = [1.0 - cap + cap * math.sin(k / (tip + 1) * math.pi / 2) for k in range(1, tip + 1)]
    n = max(1, int(round((1.0 - cap) / step)))
    even = [(1.0 - cap) * k / n for k in range(n + 1)] + capf
    keep = [f for f in even if all(abs(f - x) > step * 0.45 for x in fixed)]
    return sorted(set(round(f, 5) for f in keep + fixed))


def _add_tail_tube(bm, species, level):
    """Adds the tube along tail_path to a bmesh (outward normals, closed base, round tip). Returns the new vertices."""
    spec = SPECS[species]; cfg = spec["tube_tail"]
    pts = [Vector(p) for p in spec["tail_path"]]
    seglen = [(b - a).length for a, b in zip(pts[:-1], pts[1:])]
    total = sum(seglen)
    dirs = [(b - a).normalized() for a, b in zip(pts[:-1], pts[1:])]
    vt = [dirs[0]] + [(dirs[i - 1] + dirs[i]).normalized() for i in range(1, len(dirs))] + [dirs[-1]]

    def at(s):
        run = 0.0
        for i, length in enumerate(seglen):
            if s <= run + length or i == len(seglen) - 1:
                t = min(1.0, max(0.0, (s - run) / length)) if length > 0 else 0.0
                return pts[i].lerp(pts[i + 1], t), vt[i].lerp(vt[i + 1], t).normalized()
            run += length

    segs = cfg["segs"][level]; ox, oz = cfg.get("oval", (1.0, 1.0))
    side = Vector((1.0, 0.0, 0.0))
    loops, new = [], []
    for f in tube_loops(cfg, level):
        c, t = at(f * total)
        w = t.cross(side).normalized()
        u = w.cross(t).normalized()
        r = tube_profile(cfg, f)
        ring = [bm.verts.new(c + u * (math.cos(2 * math.pi * i / segs) * r * ox) + w * (math.sin(2 * math.pi * i / segs) * r * oz))
                for i in range(segs)]
        loops.append(ring); new += ring
    tip = bm.verts.new(at(total)[0]); base = bm.verts.new(at(0.0)[0])
    new += [tip, base]
    faces = []
    for r0, r1 in zip(loops[:-1], loops[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            faces.append(bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
    for i in range(segs):
        j = (i + 1) % segs
        faces.append(bm.faces.new((loops[-1][i], loops[-1][j], tip)))
        faces.append(bm.faces.new((loops[0][j], loops[0][i], base)))
    for f in faces:
        f.smooth = True
    return new


def add_tail_tube(species, lods=(0,)):
    """T51 ringed tails: appends the tube to SKM_<species>_LOD<n>, removes its faces buried in the body and tags its
    vertices (PW_TailTube = 1). Run on LOD0 after build_lod0 (skin_lod0 weights it by the tail rule), then on LOD1 and LOD2
    after build_lods: they take the weights of the nearest LOD0 tube vertex."""
    report = {}
    lod0 = bpy.data.objects[f"SKM_{species}_LOD0"]
    for level in lods:
        o = bpy.data.objects[f"SKM_{species}_LOD{level}"]
        me = o.data
        bb = bmesh.new(); bb.from_mesh(me); bvh = BVHTree.FromBMesh(bb); bb.free()
        inside = _inside_test(bvh)
        bm = bmesh.new(); bm.from_mesh(me)
        # Layers first: adding a layer later invalidates the new vertices' Python references.
        tag = bm.verts.layers.int.get("PW_TailTube") or bm.verts.layers.int.new("PW_TailTube")
        col = bm.verts.layers.color.get("PW_Regions") or bm.verts.layers.float_color.get("PW_Regions")
        dl = bm.verts.layers.deform.verify()
        new = _add_tail_tube(bm, species, level)
        newset = set(new)
        buried = [f for f in bm.faces if all(v in newset for v in f.verts) and all(inside(v.co) for v in f.verts)]
        bmesh.ops.delete(bm, geom=buried, context='FACES')
        new = [v for v in new if v.is_valid]
        # The LOD collapse can leave a foot a millimetre or two below the ground: back onto it (nothing enters anything).
        for v in bm.verts:
            if v.co.z < 0.0:
                v.co.z = 0.0
        for v in new:
            v[tag] = 1
            if col is not None:
                v[col] = (0.0, 0.0, 0.0, 0.0)
        if level > 0:
            src = lod0.data
            tube = src.attributes["PW_TailTube"].data
            idx = [v.index for v in src.vertices if tube[v.index].value == 1]
            kd = KDTree(len(idx))
            for i, k in enumerate(idx):
                kd.insert(src.vertices[k].co, i)
            kd.balance()
            for v in new:
                _, i, _ = kd.find(v.co)
                for g in src.vertices[idx[i]].groups:
                    name = lod0.vertex_groups[g.group].name
                    if g.weight > 0.0 and name in o.vertex_groups:
                        v[dl][o.vertex_groups[name].index] = g.weight
        bm.to_mesh(me); bm.free(); me.update()
        report[o.name] = dict(tube_verts=len(new), tris=tris(o))
    return report


# ---------------------------------------------------------------- birds (T52)

def _part_elements(spec, parts=None):
    """Blockout ellipsoids (mirrored ones included) as (part, center, half, inverse rotation, side)."""
    out = []
    for el in spec["blockout"]:
        if parts is not None and el.get("part") not in parts:
            continue
        for mirrored in ((False, True) if el["mirror"] else (False,)):
            c = Vector(el["center"]); rot = el["rot"]; axis = el.get("axis")
            if mirrored:
                c.x = -c.x; rot = (rot[0], -rot[1], -rot[2]); axis = (-axis[0], axis[1], axis[2]) if axis else None
            q = Vector(axis).to_track_quat('Y', 'Z') if axis else Euler([math.radians(a) for a in rot], 'XYZ').to_quaternion()
            out.append((el.get("part"), c, Vector(el["half"]), q.inverted(), "L" if c.x > 0 else "R"))
    return out


def _part_s(co, e):
    d = e[3] @ (co - e[1]); h = e[2]
    return math.sqrt((d.x / h.x) ** 2 + (d.y / h.y) ** 2 + (d.z / h.z) ** 2)


def apply_part_weights(species):
    """After skin_lod0: bones driven by blockout parts (spec part_weights {part: (bone pattern with {} for L/R, s0, s1,
    outside)}): membership 1 inside the part ellipsoid's normalized distance s0, 0 beyond s1; with outside > 0, only where
    the surface stands out of the trunk ellipsoids (a wing's outer face, not the body under it). part_strip bone prefixes
    leave the heat weights first; root_band (z0, z1) keeps the lowest body on Root. Max 3 influences."""
    spec = SPECS[species]; rules = spec["part_weights"]
    obj = bpy.data.objects[f"SKM_{species}_LOD0"]
    me = obj.data; vg = obj.vertex_groups
    regions = me.color_attributes["PW_Regions"].data
    elems = _part_elements(spec)
    trunk = [e for e in elems if e[0] == "trunk"]
    strip = tuple(spec.get("part_strip", ()))
    rb = spec.get("root_band")
    stats = {"part": 0, "root": 0}
    for v in me.vertices:
        co = v.co
        r, g, b, a = regions[v.index].color
        if a > 0.5 or (b > 0.5 and co.z > 0.1):
            continue
        w = {k: x for k, x in _get(vg, v).items() if not k.startswith(strip)} or {spec["trunk_bone"]: 1.0}
        if spec.get("head_z"):
            w = surface_head_weights(species, co, spec["trunk_bone"])
        if rb:
            member, _ = tail_weights(species, co)
            kr = (1.0 - smooth(rb[0], rb[1], co.z)) * (1.0 - member)
            if kr > 0.0:
                w = _blend(w, {"Root": 1.0}, kr); stats["root"] += 1
        parts = {}
        for e in elems:
            rule = rules.get(e[0])
            if not rule:
                continue
            m = 1.0 - smooth(rule[1], rule[2], _part_s(co, e))
            if m > 0.0 and rule[3] > 0.0 and trunk:
                m *= smooth(1.0, rule[3], min(_part_s(co, t) for t in trunk))
            if m > 0.0:
                bone = rule[0].format(e[4])
                parts[bone] = max(parts.get(bone, 0.0), m)
        if spec.get("surface"):
            for bone, m in surface_parts(species, co, v.normal).items():
                parts[bone] = max(parts.get(bone, 0.0), m)
        if parts:
            M = max(parts.values()); tot = sum(parts.values())
            # T52 polish: a surface part keeps the head's share, so a head turn moves the plate edge with the skin
            # around it instead of shearing it.
            keep = w.get("Head", 0.0) if spec.get("surface") else 0.0
            w = _blend({k: x for k, x in w.items() if k != "Head"} if keep else w, {k: x / tot for k, x in parts.items()}, M)
            if keep:
                w = {k: x * (1.0 - keep) for k, x in w.items()}; w["Head"] = w.get("Head", 0.0) + keep
            stats["part"] += 1
        _put(vg, v, dict(sorted(w.items(), key=lambda kv: -kv[1])[:3]))
    counts = [len([g for g in v.groups if g.weight > 0]) for v in me.vertices]
    stats["max_influences"] = max(counts); stats["unweighted"] = counts.count(0)
    return stats


def carve_grooves(species, obj):
    """A soft crease of a few millimetres where a part meets the trunk (spec grooves: parts, depth, width (how close the
    two blended volumes are, how far from both), zmax): the surface goes in along its normal. Beads are left alone."""
    spec = SPECS[species]; g = spec["grooves"]
    parts = _part_elements(spec, g["parts"]); trunk = _part_elements(spec, ("trunk",))
    me = obj.data
    regions = me.color_attributes["PW_Regions"].data
    moves = []
    for v in me.vertices:
        r, gg, b, a = regions[v.index].color
        if a > 0.5 or b > 0.5 or v.co.z > g["zmax"]:
            continue
        sw = min(_part_s(v.co, e) for e in parts); st = min(_part_s(v.co, e) for e in trunk)
        k = math.exp(-((sw - st) / g["width"][0]) ** 2) * (1.0 - smooth(g["width"][1], g["width"][1] + 0.2, max(sw, st)))
        k *= 1.0 - smooth(g["zmax"] - 0.03, g["zmax"], v.co.z)
        if k > 0.02:
            moves.append((v.index, v.co - v.normal * (g["depth"] * k)))
    for i, co in moves:
        me.vertices[i].co = co
    me.update()
    return len(moves)


def shape_beak(obj, center):
    """The nose bead becomes a beak: a little drop pointing down and a touch forward (rigid on Head like every bead)."""
    me = obj.data
    regions = me.color_attributes["PW_Regions"].data
    c = Vector(center); count = 0
    for v in me.vertices:
        r, g, b, a = regions[v.index].color
        if b > 0.5 and a < 0.5 and (v.co - c).length < 0.03:
            d = v.co - c
            down = max(0.0, -d.z)
            v.co = c + Vector((d.x * 0.92, d.y - 0.45 * down, d.z * (1.55 if d.z < 0 else 0.9)))
            count += 1
    me.update()
    return count


def cut_mask_shapes(species, obj, level):
    """Cuts the species' mask shapes (pw_masks.mask_shapes: stars, discs) into obj's surface: a fan inside the first
    outline, rings between the outlines (each ring is where a vertex mask switches, so the edge is crisp), bridged to the
    hole left by the body faces under the shape (eye and nose beads are never cut). Points are snapped along the shape
    normal, so a vertex's shape coordinates match the mask rule's. New vertices get the body region. Returns (triangles
    added, shapes skipped: the faces there do not make a simple ring)."""
    import pw_masks
    before = tris(obj)
    bm = bmesh.new(); bm.from_mesh(obj.data)
    col = bm.verts.layers.color.get("PW_Regions") or bm.verts.layers.float_color.get("PW_Regions")

    def bead(f):
        return any(v[col][3] > 0.5 or (v[col][2] > 0.5 and v.co.z > 0.1) for v in f.verts)
    bm.verts.index_update()
    bvh = BVHTree.FromPolygons([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces if not bead(f)])
    skipped = 0
    shapes = pw_masks.mask_shapes(species, level)
    # T52 polish: two shapes close together (the facial discs) are split by the plane between them first, so each hole
    # stays on its own side and the two cuts never share a face.
    walls = {}
    for i, (a, oa) in enumerate(shapes):
        for j, (b, ob) in enumerate(shapes[:i]):
            ca, cb = Vector(a[0]), Vector(b[0])
            ra, rb = max(p.length for p in oa[-1]), max(p.length for p in ob[-1])
            if (ca - cb).length < ra + rb + 0.012:
                mid = (ca + cb) / 2; w = (ca - cb).normalized()
                near = [f for f in bm.faces if not bead(f) and (f.calc_center_median() - mid).length < ra + rb]
                geom = list({x for f in near for x in list(f.verts) + list(f.edges)}) + near
                bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=mid, plane_no=w)
                walls.setdefault(i, []).append((mid, w)); walls.setdefault(j, []).append((mid, -w))
    for index, (item, outlines) in enumerate(shapes):
        c, n, t1, t2 = pw_masks.shape_frame(item[0], item[1])
        wall = walls.get(index, [])

        def snap(p2):
            p = c + t1 * p2.x + t2 * p2.y
            hit = bvh.ray_cast(p + n * 0.03, -n)[0]
            return hit if hit is not None else bvh.find_nearest(p)[0]

        def ang(co):
            return math.atan2((co - c).dot(t2), (co - c).dot(t1)) % (2 * math.pi)
        outer = max(p.length for p in outlines[-1])
        loop = None

        def plane_r(co):
            d = co - c
            return (d - n * d.dot(n)).length
        # T52 polish: irregular (collapsed) faces may pinch the selection; try finer reaches and a selection by face centre,
        # and keep only a hole that encloses the outline.
        for reach, by_centre in [(outer + 0.004 * k, m) for k in (1, 2, 3, 4.5, 6, 8, 10) for m in (False, True)]:
            if by_centre:
                sel = [f for f in bm.faces if not bead(f) and plane_r(f.calc_center_median()) < reach
                       and (f.calc_center_median() - c).dot(n) > -0.02]
            else:
                sel = [f for f in bm.faces if not bead(f) and any((v.co - c).length < reach and (v.co - c).dot(n) > -0.02 for v in f.verts)]
            if wall:
                sel = [f for f in sel if all((f.calc_center_median() - m).dot(w) > 0.0 for m, w in wall)]
            selset = set(sel)
            boundary = [v for v in {v for f in sel for v in f.verts} if any(f not in selset for f in v.link_faces)]
            bset = set(boundary)
            nxt = {}
            for f in sel:
                for e in f.edges:
                    a, b = e.verts
                    if a in bset and b in bset and sum(1 for x in e.link_faces if x in selset) == 1:
                        nxt.setdefault(a, []).append(b); nxt.setdefault(b, []).append(a)
            ok = bool(nxt) and all(len(x) == 2 for x in nxt.values()) and len(nxt) == len(boundary)
            if ok:
                loop = [boundary[0]]; prev = None
                while True:
                    cand = [x for x in nxt[loop[-1]] if x is not prev]
                    if cand[0] is loop[0]:
                        break
                    prev = loop[-1]; loop.append(cand[0])
                ok = len(loop) == len(boundary)
            if ok:
                steps = [((ang(loop[(i + 1) % len(loop)].co) - ang(loop[i].co) + math.pi) % (2 * math.pi)) - math.pi
                         for i in range(len(loop))]
                if all(s < 0 for s in steps):
                    loop.reverse()
                elif not all(s > 0 for s in steps):
                    ok = False
            if ok and min(plane_r(v.co) for v in loop) > outer - 0.004:
                break
            loop = None
        if loop is None:
            skipped += 1
            continue
        # A hole edge vertex close to the outline moves 4 mm clear of it (no sliver face in the bridge), back on the surface.
        for v in loop:
            d = v.co - c; dp = d - n * d.dot(n)
            if dp.length < outer + 0.004:
                p = c + dp * ((outer + 0.004) / max(dp.length, 1e-6)) + n * d.dot(n)
                v.co = bvh.ray_cast(p + n * 0.03, -n)[0] or p
        bmesh.ops.delete(bm, geom=sel, context='FACES')
        C = bm.verts.new(snap(Vector((0.0, 0.0))))
        rings = [[bm.verts.new(snap(p)) for p in ol] for ol in outlines]
        for v in [C] + [v for r in rings for v in r]:
            v[col] = (0.0, 0.0, 0.0, 0.0)
        new = []
        m = len(rings[0])
        for i in range(m):
            new.append(bm.faces.new((C, rings[0][i], rings[0][(i + 1) % m])))
            for a, b in zip(rings[:-1], rings[1:]):
                new.append(bm.faces.new((a[i], b[i], b[(i + 1) % m], a[(i + 1) % m])))

        def ordered(vs):
            i0 = min(range(len(vs)), key=lambda i: ang(vs[i].co))
            vs = vs[i0:] + vs[:i0]
            a = [ang(v.co) for v in vs]
            for i in range(1, len(a)):
                while a[i] < a[i - 1]:
                    a[i] += 2 * math.pi
            return vs + [vs[0]], a + [a[0] + 2 * math.pi]
        A, aa = ordered(rings[-1]); B, bb = ordered(loop)
        i = j = 0
        bridge = []
        while i < len(A) - 1 or j < len(B) - 1:
            if j == len(B) - 1 or (i < len(A) - 1 and aa[i + 1] < bb[j + 1]):
                bridge.append(bm.faces.new((A[i], A[i + 1], B[j]))); i += 1
            else:
                bridge.append(bm.faces.new((A[i], B[j + 1], B[j]))); j += 1
        for f in new + bridge:
            f.normal_update()
            if f.normal.dot(n) < 0:
                f.normal_flip()
        # T52 polish: the bridge diagonals (outline to hole) turn where that gives rounder triangles, so no long sliver
        # folds when the skin bends.
        ring = set(A); bset = set(bridge)
        diag = [e for f in bridge for e in f.edges if (e.verts[0] in ring) != (e.verts[1] in ring)
                and all(x in bset for x in e.link_faces) and len(e.link_faces) == 2]
        res = bmesh.ops.beautify_fill(bm, faces=bridge, edges=list(set(diag)), use_restrict_tag=False, method='ANGLE')
        for f in new + [f for f in bridge if f.is_valid] + [f for f in res["geom"] if isinstance(f, bmesh.types.BMFace)]:
            f.normal_update()
            if f.normal.dot(n) < 0:
                f.normal_flip()
            f.smooth = True
    bm.normal_update()
    bm.to_mesh(obj.data); bm.free()
    obj.data.update()
    return tris(obj) - before, skipped


def copy_weights_to_unweighted(obj, src):
    """Vertices with no group (cut after the weight transfer) take the weights of the nearest src vertex at rest."""
    kd = KDTree(len(src.data.vertices))
    for v in src.data.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    sg = src.vertex_groups; tg = obj.vertex_groups
    count = 0
    for v in obj.data.vertices:
        if len(v.groups):
            continue
        _, i, _ = kd.find(v.co)
        for g in src.data.vertices[i].groups:
            name = sg[g.group].name
            (tg.get(name) or tg.new(name=name)).add([v.index], g.weight, 'REPLACE')
        count += 1
    return count


# ---------------------------------------------------------------- bird surface (T52 polish)
# A species with spec["surface"] skips the metaballs: its body is one analytic egg, sculpted by offsets along the normal,
# so it is smooth by construction (no seam where blended volumes meet). Numpy for the dense mesh, floats for one point.

def _np_smooth(e0, e1, x):
    import numpy as np
    t = np.clip((np.asarray(x, dtype=float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def egg_value(E, P):
    """Superquadric egg of spec surface "egg" at points P (n, 3): below 1 inside, 1 on the surface."""
    import numpy as np
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    dz = z - E["zc"]
    up = dz >= 0
    c = np.where(up, E["top"] - E["zc"], E["zc"] - E["bottom"])
    q = np.where(up, E["q_top"], E["q_bottom"])
    bf = E["b_belly"] + (E["b_face"] - E["b_belly"]) * _np_smooth(E["face_z"][0], E["face_z"][1], z)
    b = np.where(y < 0, bf, E["b_back"])
    h = (np.abs(x / E["a"]) ** E["p"] + np.abs(y / b) ** E["p"]) ** (q / E["p"])
    return h + np.abs(dz / c) ** q


def wing_coords(W, y, z):
    """Side view wing coordinates (s, t): s = 1 on the wing outline (an egg outline, widest at tc), t from the top (0)
    to the tip (1). Floats or numpy arrays."""
    import numpy as np
    (ty, tz), (py, pz) = W["top"], W["tip"]
    L = math.hypot(py - ty, pz - tz); ay, az = (py - ty) / L, (pz - tz) / L
    dy = np.asarray(y, dtype=float) - ty; dz = np.asarray(z, dtype=float) - tz
    t = (dy * ay + dz * az) / L; v = dy * az - dz * ay
    span = np.where(t < W["tc"], W["tc"], 1.0 - W["tc"])
    return np.sqrt(((t - W["tc"]) / span) ** 2 + (v / W["half"]) ** 2), t


def tail_coords(T, x, z):
    """Back view tail fan (s, k): s = 1 on its rounded square outline, k its thickness share (thin top, full lower edge)."""
    import numpy as np
    (cx, cz), (hx, hz) = T["center"], T["half"]
    x = np.asarray(x, dtype=float); z = np.asarray(z, dtype=float)
    s = (((x - cx) / hx) ** 4 + ((z - cz) / hz) ** 4) ** 0.25
    return s, T["thin_top"] + (1 - T["thin_top"]) * _np_smooth(cz + hz, cz - hz, z)


def toe_shapes(species, heel=True):
    """(centre, half sizes, yaw in radians) of the toe ellipsoids of both feet (spec surface "toes"), heel pads last."""
    T = SPECS[species]["surface"]["toes"]; toes = []; heels = []
    for sx in (1, -1):
        piv = Vector(T["pivot"]); piv.x *= sx
        for yaw in T["yaws"]:
            a = math.radians(yaw * sx)
            toes.append((piv + Vector((math.sin(a), -math.cos(a), 0.0)) * T["reach"], T["half"], a))
        if T.get("heel"):
            c, h = T["heel"]
            heels.append((Vector((c[0] * sx, c[1], c[2])), h, 0.0))
    return toes + (heels if heel else [])


def surface_mesh(species):
    """The dense analytic body of spec["surface"] (about 85k triangles): the egg (bisection along rays from its centre),
    the facial disc profile around the pw_masks disc frames, wing and tail plates, then the toes by an exact union."""
    import numpy as np
    import pw_masks
    S = SPECS[species]["surface"]; E = S["egg"]
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=S.get("subdiv", 7), radius=1.0)
    D = np.array([v.co[:] for v in bm.verts]); D /= np.linalg.norm(D, axis=1)[:, None]
    C = np.array((0.0, 0.0, E["zc"]))
    lo = np.full(len(D), 0.01); hi = np.full(len(D), 0.6)
    for _ in range(40):
        mid = (lo + hi) / 2
        inside = egg_value(E, C + D * mid[:, None]) < 1.0
        lo = np.where(inside, mid, lo); hi = np.where(inside, hi, mid)
    P = C + D * ((lo + hi) / 2)[:, None]
    for v, p in zip(bm.verts, P):
        v.co = p
    bm.normal_update()
    N = np.array([v.normal[:] for v in bm.verts])
    off = np.zeros(len(P))
    prof = np.array(S["disc_profile"]); best = np.full(len(P), 9.0)
    for c, n, _ in pw_masks.RULES[species]["discs"]["items"]:
        c = np.array(c); n = np.array(n) / np.linalg.norm(n)
        v = P - c; dn = v @ n
        best = np.minimum(best, np.where(dn > -0.03, np.linalg.norm(v - dn[:, None] * n, axis=1), 9.0))
    off += np.interp(best, prof[:, 0], prof[:, 1], right=0.0)
    W = S["wing"]
    s, t = wing_coords(W, P[:, 1], P[:, 2])
    thick = W["thick"] * (W["thin_top"] + (1 - W["thin_top"]) * _np_smooth(0.0, 0.55, t))
    off += (thick * (1 - _np_smooth(W["edge"][0], W["edge"][1], s))
            - W["crease"] * np.exp(-((s - 1.06) / 0.07) ** 2)) * _np_smooth(0.30, 0.60, np.abs(N[:, 0]))
    T = S["tail"]
    s, k = tail_coords(T, P[:, 0], P[:, 2])
    off += T["thick"] * k * (1 - _np_smooth(T["edge"][0], T["edge"][1], s)) * _np_smooth(0.2, 0.5, N[:, 1])
    # Feathered legs: the underside swells softly down over each foot, so the toes come out of the belly.
    pad = S["toes"].get("pad")
    if pad:
        (px, py), h, (sx, sy) = pad
        for side in (1, -1):
            off += h * np.exp(-((P[:, 0] - side * px) / sx) ** 2 - ((P[:, 1] - py) / sy) ** 2) * _np_smooth(0.2, 0.6, -N[:, 2])
    for v, p in zip(bm.verts, P + N * off[:, None]):
        v.co = p
    for c, h, yaw in toe_shapes(species):
        tb = bmesh.new()
        bmesh.ops.create_uvsphere(tb, u_segments=24, v_segments=12, radius=1.0)
        bmesh.ops.transform(tb, verts=tb.verts, matrix=Matrix.Translation(c) @ Matrix.Rotation(yaw, 4, 'Z')
                            @ Matrix.Diagonal((h[0], h[1], h[2], 1.0)))
        tmp = bpy.data.meshes.new("WORK_tmp"); tb.to_mesh(tmp); tb.free()
        bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    me = bpy.data.meshes.new(f"WORK_{species}_Shells"); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(me.name, me); bpy.context.scene.collection.objects.link(o)
    union = o.modifiers.new("Union", 'BOOLEAN'); union.operation = 'UNION'; union.solver = 'EXACT'; union.use_self = True
    empty = bpy.data.collections.new("WORK_tmp_union"); union.operand_type = 'COLLECTION'; union.collection = empty
    out = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(o, do_unlink=True); bpy.data.meshes.remove(me); bpy.data.collections.remove(empty)
    return out


def _beautify_gentle(obj, max_angle=12.0, rounds=3):
    """After a collapse: edges between two nearly coplanar faces turn where that gives rounder triangles (no new point),
    so the gentle body loses its long thin faces; creases (rims, wing edges, toes) are left alone."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    for _ in range(rounds):
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(9.0) < math.radians(max_angle)]
        bmesh.ops.beautify_fill(bm, faces=list({f for e in edges for f in e.link_faces}), edges=edges, method='ANGLE')
    bm.normal_update(); bm.to_mesh(obj.data); bm.free()
    for p in obj.data.polygons:
        p.use_smooth = True


def surface_collapse_weights(species, me):
    """Collapse weights for the dense surface: the gentle body (spec surface "gentle", a little under 1) keeps more of its
    points than the sculpted features (discs, wing edges, toes and pads at 1), so long thin faces do not facet the
    silhouette (the cut rebuilds the discs anyway)."""
    import numpy as np
    import pw_masks
    S = SPECS[species]["surface"]
    gentle = S.get("gentle")
    if not gentle:
        return None
    P = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", P); P = P.reshape(-1, 3)
    feat = P[:, 2] < 0.03
    for c, n, _ in pw_masks.RULES[species]["discs"]["items"]:
        feat |= np.linalg.norm(P - np.array(c), axis=1) < 0.08
    s, _ = wing_coords(S["wing"], P[:, 1], P[:, 2])
    feat |= (s > 0.65) & (s < 1.15) & (np.abs(P[:, 0]) > 0.11)
    return np.where(feat, 1.0, gentle).tolist()


def surface_head_weights(species, co, trunk):
    """T52 polish, a head merged into the body: Head, Neck and trunk shares from the height (spec head_z: the band where
    the head hands over to the body, at the front and on the sides and back, and the Neck share at its middle), the
    facial discs rigid on Head. A head turn twists the upper body over the whole band instead of folding a narrow seam."""
    import pw_masks
    H = SPECS[species]["head_z"]
    # Front share by the angle around the body (a turn twists along it, so the band shifts over a wide arc).
    front = smooth(0.20, 0.90, -co.y / max(math.hypot(co.x, co.y), 1e-6))
    z0 = H["back"][0] + (H["front"][0] - H["back"][0]) * front
    z1 = H["back"][1] + (H["front"][1] - H["back"][1]) * front
    h = smooth(z0, z1, co.z)
    D = pw_masks.RULES[species].get("discs")
    for c, n, _ in (D["items"] if D else ()):
        d = co - Vector(c); n = Vector(n).normalized(); dn = d.dot(n)
        if dn > -0.03:
            r = (d - n * dn).length
            h = max(h, 1.0 - smooth(D["radii"][1] + D["edge"] + 0.004, D["radii"][1] + D["edge"] + 0.020, r))
    neck = min(1.0 - h, H["neck"] * (1.0 - abs(2.0 * h - 1.0)))
    return {k: x for k, x in (("Head", h), ("Neck", neck), (trunk, 1.0 - h - neck)) if x > 1e-4}


def surface_parts(species, co, n):
    """Bone claims of the surface wings and tail at a rest point (co, normal), {bone: 0..1} with the strongest claim the
    membership (apply_part_weights): Wing_Upper holds the plate, Wing_Lower its tip half, Tail_01 the fan."""
    S = SPECS[species]["surface"]; out = {}
    W = S["wing"]
    s, t = (float(x) for x in wing_coords(W, co.y, co.z))
    # The claim fades in from the shoulder (top_fade along t): the plate bends out from its hinge.
    m = (1.0 - smooth(W["bone_edge"][0], W["bone_edge"][1], s)) * smooth(0.30, 0.60, abs(n.x)) * smooth(*W["top_fade"], t)
    if m > 0.0:
        side = "L" if co.x > 0 else "R"
        lo = smooth(W["lower_t"][0], W["lower_t"][1], t); k = max(lo, 1.0 - lo)
        out[f"Wing_{side}_Upper"] = m * (1.0 - lo) / k
        out[f"Wing_{side}_Lower"] = m * lo / k
    T = S["tail"]
    s, _ = tail_coords(T, co.x, co.z)
    m = (1.0 - smooth(T["bone_edge"][0], T["bone_edge"][1], float(s))) * smooth(0.2, 0.5, n.y)
    if m > 0.0:
        out["Tail_01"] = m
    return out


def snap_to_surface(species, obj):
    """Body vertices (beads apart) go back onto the analytic surface along their normal, so the rings cut_mask_shapes
    snapped onto the decimated faces sit exactly on the sculpted disc rims. Never below the ground. Then every body
    vertex takes the surface's own normal (custom normals, exported): long thin faces around the cut rings shade as
    smoothly as the surface itself."""
    hi = _hi_mesh(species)
    hb = bmesh.new(); hb.from_mesh(hi); hb.normal_update(); hb.faces.ensure_lookup_table(); bvh = BVHTree.FromBMesh(hb)
    bpy.data.meshes.remove(hi)
    me = obj.data; regions = me.color_attributes["PW_Regions"].data; moved = 0
    for v in me.vertices:
        r, g, b, a = regions[v.index].color
        if a > 0.5 or (b > 0.5 and v.co.z > 0.1):
            continue
        n = v.normal.copy()
        hits = [h[0] for h in (bvh.ray_cast(v.co + n * 0.008, -n, 0.016), bvh.ray_cast(v.co - n * 0.008, n, 0.016))
                if h[0] is not None]
        p = min(hits, key=lambda q: (q - v.co).length) if hits else bvh.find_nearest(v.co)[0]
        if p is not None:
            v.co = p; moved += 1
        v.co.z = max(0.0, v.co.z)
    me.update()
    normals = []
    for v in me.vertices:
        r, g, b, a = regions[v.index].color
        loc, _, i, _ = (None, None, None, None) if a > 0.5 or (b > 0.5 and v.co.z > 0.1) else bvh.find_nearest(v.co)
        if loc is None:
            normals.append(v.normal.copy()); continue
        f = hb.faces[i]
        w = [1.0 / max((x.co - loc).length, 1e-6) for x in f.verts]
        normals.append(sum((x.normal * k for x, k in zip(f.verts, w)), Vector()).normalized())
    me.normals_split_custom_set_from_vertices(normals)
    hb.free()
    return moved


def build_bird(species, template_path):
    """T52 Bird recipe, the whole file in one call (headless is fine). Open template_path (a MediumQuadruped member,
    e.g. BL_Animal_Fawn.blend) first in its own call. Saves SourceArt/Blender/Animals/<species>/BL_Animal_<species>.blend
    with SK_Bird (pw_rig.build_armature), LOD0-2, four sockets and the masks; then pw_export.export_species_model."""
    import pw_rig, pw_masks
    report = {"file": start_from(template_path, species)}
    for name in ("SK_MediumQuadruped", "SOCK_Ear_L", "SOCK_Ear_R") + tuple(o.name for o in bpy.data.objects if o.name.startswith("WORK_Ref_")):
        o = bpy.data.objects.get(name)
        if o is not None:
            bpy.data.objects.remove(o, do_unlink=True)
    for a in [a for a in bpy.data.armatures if a.users == 0]:
        bpy.data.armatures.remove(a)
    pw_rig.build_armature(SPECS[species]["armature"][3:])
    report["elements"] = build_blockout(species)
    report["features"] = place_features(species)
    report["lod0_body"] = build_lod0(species)
    lod0 = bpy.data.objects[f"SKM_{species}_LOD0"]
    nose = bpy.data.objects["WORK_Nose"].location.copy()
    report["beak"] = shape_beak(lod0, nose)
    if SPECS[species].get("grooves"):
        report["grooves"] = carve_grooves(species, lod0)
    report["cut0"] = cut_mask_shapes(species, lod0, 0)
    if SPECS[species].get("surface"):
        report["snap0"] = snap_to_surface(species, lod0)
    report["skin"] = skin_lod0(species)
    report["parts"] = apply_part_weights(species)
    report["lods"] = build_lods(species)
    lod1 = bpy.data.objects[f"SKM_{species}_LOD1"]; lod2 = bpy.data.objects[f"SKM_{species}_LOD2"]
    shape_beak(lod1, nose)
    if SPECS[species].get("grooves"):
        carve_grooves(species, lod1)
    report["cut1"] = cut_mask_shapes(species, lod1, 1)
    if SPECS[species].get("surface"):
        report["snap1"] = snap_to_surface(species, lod1)
    report["cut1_weighted"] = copy_weights_to_unweighted(lod1, lod0)
    for o in (lod1, lod2):   # a collapse can leave a vertex a hair under the ground
        for v in o.data.vertices:
            v.co.z = max(0.0, v.co.z)
    if SPECS[species].get("surface"):
        report["snap2"] = snap_to_surface(species, lod2)
    seat = SPECS[species].get("ground_seat")
    for o in ((lod1, lod2) if seat else ()):   # a collapse that lost the toe soles stands back on the ground
        for v in o.data.vertices:
            if v.co.z < seat:
                v.co.z = 0.0
    report["sockets"] = place_sockets(species)
    report["masks"] = pw_masks.apply(species)
    report["tris"] = {o.name: tris(o) for o in (lod0, lod1, lod2)}
    bpy.ops.wm.save_mainfile()
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
    }
    for side in ("L", "R"):
        ear = arm.data.bones.get(f"Ear_{side}_01")
        if ear is not None:
            spec_sockets[f"SOCK_Ear_{side}"] = (ear.head_local.copy(), ear.name)
    # T52: a table family (pw_rig.FAMILIES) gives the parent bones and the socket list (the Bird: four, no ears).
    import pw_rig
    family = pw_rig.FAMILIES.get(arm.name[3:])
    if family is not None:
        spec_sockets = {k: (co, family[1][k][0]) for k, (co, _) in spec_sockets.items() if k in family[1]}
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
