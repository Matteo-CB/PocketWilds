"""SmallQuadruped ground contact (« Aucune collision », 2026-10-12): measures how low every clip takes every small species
and solves the PLANTED keys of pw_anim_library.py so nothing goes below the ground.

Run from the project root with Python 3 and numpy (Blender is started headless, read-only, for the dumps):
    python SourceArt/Blender/Tools/pw_anim_ground.py check   # lowest vertex of every played clip on every species
    python SourceArt/Blender/Tools/pw_anim_ground.py solve   # re-solve PLANTED and write it into pw_anim_library.py

Method. The species share the SK_SmallQuadruped skeleton (identical bind poses), so a clip is evaluated once per frame
(forward kinematics) and skinned on each species LOD0 and LOD2 (top four weights, normalized, like Unity), times the
prefab scale. A species' floor is min(0, its rest pose's lowest vertex). For each fixed clip and frame:
- the Root lifts what the body (vertices with no leg weight) needs, plus up to RMAX of what the legs need: a straight leg
  would need a big bend to rise a few millimetres;
- then each leg whose vertices still go below the floor first turns its paw towards flat, and if that is not enough
  bends (two-bone IK on Upper and Lower rx, the ankle kept at its horizontal place) with the paw flat; CONTACT clips
  (the sleeps) also bring floating paws down onto the ground; STICKY keeps a paw planted one more frame next to a deep
  bend so the knee unfolds over two frames;
- the clips are rebuilt and checked at quarter frames; a frame next to a dip gets a wider margin and is solved again.
The hand keys stay the source of the body motion: the dump builds the clips with PLANTED emptied.
"""
import json, math, os, re, subprocess, sys, tempfile

BLENDER = "D:/SteamLibrary/steamapps/common/Blender/blender.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.environ.get("PW_ROOT") or os.path.normpath(os.path.join(HERE, "..", "..", ".."))  # PW_ROOT: another checkout
LIBRARY = os.path.join(ROOT, "SourceArt", "Blender", "Tools", "pw_anim_library.py")
SPECIES = ["Rabbit", "Squirrel", "FieldMouse", "Hedgehog", "GuineaPig", "FieldHamster", "Mole", "Raccoon", "RedPanda"]
# VisualRoot scale of PF_Animal_<Species> (AnimalPipeline.ScaledLengths); the others ride the family scale.
SCALE = {"Raccoon": 1.0594825, "RedPanda": 1.1647824}
# What each species plays: the family states (AnimalPipeline.FamilyStates + PeekOut) and its override table.
BASE = ["Idle_A", "Idle_B", "Locomotion_Hop", "Happy", "ReactTouch", "Sleep_Enter", "Sleep_Loop", "Sleep_Exit",
        "Eat_Loop", "Social_Nose", "Inspect", "PeekOut"]
LOW_SLEEP = {"Sleep_Enter": "LowSleep_Enter", "Sleep_Loop": "LowSleep_Loop", "Sleep_Exit": "LowSleep_Exit"}
LOW_BODY = dict({"Locomotion_Hop": "Locomotion_Walk", "Happy": "Happy_Bounce"}, **LOW_SLEEP)
OVERRIDES = {
    "Hedgehog": LOW_BODY, "GuineaPig": LOW_BODY,
    "FieldHamster": dict({"Locomotion_Hop": "Locomotion_Walk", "Happy": "FieldHamsterGreet", "Idle_B": "FieldHamsterStash"}, **LOW_SLEEP),
    "Mole": {"Locomotion_Hop": "MoleWalk", "Happy": "MoleHappy", "ReactTouch": "MoleReact", "Sleep_Enter": "MoleSleep_Enter",
             "Sleep_Loop": "MoleSleep_Loop", "Sleep_Exit": "MoleSleep_Exit", "Eat_Loop": "MoleEat", "Inspect": "MoleDig"},
    "Raccoon": {"Locomotion_Hop": "Locomotion_Walk", "Happy": "Happy_Bounce", "Inspect": "RaccoonWash"},
    "RedPanda": {"Locomotion_Hop": "Locomotion_Walk", "Happy": "Happy_Bounce", "Idle_B": "RedPandaStand"},
}
# The clips PLANTED fixes, in the order they are written, and their options.
FIX = ["Sleep_Enter", "Sleep_Loop", "Sleep_Exit", "LowSleep_Enter", "LowSleep_Loop", "LowSleep_Exit", "ReactTouch",
       "Locomotion_Hop", "Locomotion_Walk", "Happy", "Happy_Bounce", "Eat_Loop", "Social_Nose"]
CONTACT = {"Sleep_Enter", "Sleep_Loop", "Sleep_Exit", "LowSleep_Enter", "LowSleep_Loop", "LowSleep_Exit"}
RMAX = {"Social_Nose": 0.02, "Eat_Loop": 0.02, "Locomotion_Walk": 0.0, "Happy": 0.005}  # default 3 mm
STICKY = {"Happy": 20.0, "Happy_Bounce": 20.0, "Locomotion_Hop": 20.0}  # knee bends (degrees) that unfold over two frames
LEGS = {"FL": ("FrontLeg_L_Upper", "FrontLeg_L_Lower", "FrontPaw_L"),
        "FR": ("FrontLeg_R_Upper", "FrontLeg_R_Lower", "FrontPaw_R"),
        "HL": ("HindLeg_L_Upper", "HindLeg_L_Lower", "HindPaw_L"),
        "HR": ("HindLeg_R_Upper", "HindLeg_R_Lower", "HindPaw_R")}
BASE_MARGIN = 0.0004   # solved poses keep 0.4 mm above the floor
LEG_SHARE = 0.15       # a vertex with this much weight on a leg's bones moves with that leg
LIMIT = -0.0017        # every quarter frame keeps above this (0.3 mm inside FamilyClipGroundTests' 2 mm)
SUB = 4


def played(species):
    return {OVERRIDES.get(species, {}).get(c, c) for c in BASE}


def players(clip):
    return [s for s in SPECIES if clip in played(s)]


# ---------------------------------------------------------------- Blender side (headless, never saves)

def _blender_main(argv):
    import bpy
    import numpy as np
    mode = argv[0]
    arm = bpy.data.objects["SK_SmallQuadruped"]
    if mode == "skin":
        bones = list(arm.data.bones)
        index = {b.name: i for i, b in enumerate(bones)}
        data = {"bone_names": np.array([b.name for b in bones]),
                "parents": np.array([index[b.parent.name] if b.parent else -1 for b in bones], dtype=np.int32),
                "rest": np.array([np.array(b.matrix_local) for b in bones]), "arm_world": np.array(arm.matrix_world)}
        deform = [b.use_deform for b in bones]
        arm_inv = np.linalg.inv(np.array(arm.matrix_world))
        meshes = []
        for o in bpy.data.objects:
            if o.type != 'MESH' or o.find_armature() != arm or o.name.startswith("WORK_"):
                continue
            if "LOD0" not in o.name and "LOD2" not in o.name:
                continue
            me = o.data
            n = len(me.vertices)
            co = np.empty(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(n, 3)
            m = arm_inv @ np.array(o.matrix_world)
            co = co @ m[:3, :3].T + m[:3, 3]
            gmap = {g.index: index.get(g.name, -1) for g in o.vertex_groups}
            bi = np.full((n, 8), -1, dtype=np.int32); bw = np.zeros((n, 8))
            for v in me.vertices:
                gs = sorted(((g.weight, gmap.get(g.group, -1)) for g in v.groups
                             if gmap.get(g.group, -1) >= 0 and deform[gmap[g.group]] and g.weight > 0), reverse=True)[:8]
                for k, (w, b) in enumerate(gs):
                    bi[v.index, k] = b; bw[v.index, k] = w
            key = f"m{len(meshes)}"
            data[key + "_co"], data[key + "_bi"], data[key + "_bw"] = co, bi, bw
            meshes.append(key)
        data["meshes"] = np.array(meshes)
        np.savez(argv[1], **data)
    elif mode == "clips":
        lib, out, sub, planted = argv[1], argv[2], int(argv[3]), argv[4] == "1"
        ns = {"__name__": "pw_anim_library_ground"}
        exec(open(lib, encoding="utf-8").read(), ns)
        if not planted:
            ns["PLANTED"] = {}
        ns["build_all"]()
        pat = re.compile(r'pose\.bones\["(.+)"\]\.(rotation_euler|location|scale)')
        kind = {"rotation_euler": "rot", "location": "loc", "scale": "scl"}
        res = {}
        for act in bpy.data.actions:
            if not act.name.startswith("AN_SmallQuadruped_"):
                continue
            n = int(act.frame_end)
            ch = {}
            for fc in ns["fcurves"](act):
                m = pat.match(fc.data_path)
                if m:
                    ch[f"{m.group(1)}|{kind[m.group(2)]}|{fc.array_index}"] = [fc.evaluate(k / sub) for k in range(n * sub + 1)]
            res[act.name[len("AN_SmallQuadruped_"):]] = {"frames": n, "loop": act.use_cyclic, "sub": sub, "ch": ch}
        json.dump(res, open(out, "w"))


def _blender(blend, *args):
    subprocess.run([BLENDER, "-b", "--factory-startup", blend, "--python", os.path.abspath(__file__), "--", *map(str, args)],
                   check=True, capture_output=True)


def species_blend(s):
    return os.path.join(ROOT, "SourceArt", "Blender", "Animals", s, f"BL_Animal_{s}.blend")


def dump_clips(work, lib, sub, planted):
    out = os.path.join(work, f"clips_{sub}_{int(planted)}.json")
    _blender(species_blend("Rabbit"), "clips", lib, out, sub, 1 if planted else 0)
    return json.load(open(out))


# ---------------------------------------------------------------- numpy side

def _np():
    import numpy as np
    return np


def euler_matrix(rx, ry, rz):
    np = _np()
    cx, sx, cy, sy, cz, sz = np.cos(rx), np.sin(rx), np.cos(ry), np.sin(ry), np.cos(rz), np.sin(rz)
    return (np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]) @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
            @ np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]]))


def rot_x(deg):
    np = _np()
    r = np.radians(deg)
    m = np.eye(4)
    m[1, 1], m[1, 2], m[2, 1], m[2, 2] = np.cos(r), -np.sin(r), np.sin(r), np.cos(r)
    return m


class Species:
    def __init__(self, name, path):
        np = _np()
        d = np.load(path)
        self.name, self.scale = name, SCALE.get(name, 1.0)
        self.bone_names = [str(b) for b in d["bone_names"]]
        self.index = {b: i for i, b in enumerate(self.bone_names)}
        self.parents, self.rest, self.arm_world = d["parents"], d["rest"], d["arm_world"]
        self.rest_inv = np.linalg.inv(self.rest)
        self.local_rest = np.array([self.rest[i] if p < 0 else np.linalg.inv(self.rest[p]) @ self.rest[i]
                                    for i, p in enumerate(self.parents)])
        self.meshes = []
        for key in d["meshes"]:
            co, bi, bw = d[f"{key}_co"], d[f"{key}_bi"][:, :4].copy(), d[f"{key}_bw"][:, :4].copy()
            s = bw.sum(1, keepdims=True)
            keep = s[:, 0] > 1e-6
            bw[keep] /= s[keep]
            bi[bi < 0] = 0
            self.meshes.append((co, bi, bw, keep))
        self.floor = min(0.0, self.lowest(self.skin(self.fk(np.tile(np.eye(4), (len(self.bone_names), 1, 1))))))

    def fk(self, basis):
        np = _np()
        pose = np.empty((len(self.bone_names), 4, 4))
        for i, p in enumerate(self.parents):
            pose[i] = (self.local_rest[i] if p < 0 else pose[p] @ self.local_rest[i]) @ basis[i]
        return pose

    def skin(self, pose):
        return self.arm_world @ pose @ self.rest_inv

    def zs(self, skin, co, bi, bw, keep):
        np = _np()
        zrow = skin[:, 2, :]
        z = ((np.einsum("vkj,vj->vk", zrow[bi][:, :, :3], co) + zrow[bi][:, :, 3]) * bw).sum(1)
        return np.where(keep, z, co[:, 2]) * self.scale

    def lowest(self, skin):
        return min(float(self.zs(skin, *m).min()) for m in self.meshes)

    def basis(self, clip, k):
        np = _np()
        basis = np.tile(np.eye(4), (len(self.bone_names), 1, 1))
        ch = clip["ch"]
        def value(key, default):
            v = ch.get(key)
            return default if v is None else v[k]
        for b, i in self.index.items():
            m = np.eye(4)
            m[:3, :3] = euler_matrix(*[value(f"{b}|rot|{a}", 0.0) for a in range(3)]) @ np.diag(
                [value(f"{b}|scl|{a}", 1.0) for a in range(3)])
            m[:3, 3] = [value(f"{b}|loc|{a}", 0.0) for a in range(3)]
            basis[i] = m
        return basis


class Part:
    """One species LOD's vertices split by leg (the leg's bones carry at least LEG_SHARE of the weight) and body."""
    def __init__(self, sp, co, bi, bw, keep):
        np = _np()
        self.sp = sp
        self.by_leg = {}
        any_leg = np.zeros(len(co), dtype=bool)
        for leg, chain in LEGS.items():
            share = (np.isin(bi, [sp.index[b] for b in chain]) * bw).sum(1)
            m = (share >= LEG_SHARE) & keep & (co[:, 2] < 0.05)
            any_leg |= share >= LEG_SHARE
            self.by_leg[leg] = (co[m], bi[m], bw[m], keep[m])
        m = ~any_leg & keep
        self.body = (co[m], bi[m], bw[m], keep[m])

    def deficit(self, skin, part):
        if len(part[0]) == 0:
            return -1.0
        return self.sp.floor - float(self.sp.zs(skin, *part).min())


class Solver:
    def __init__(self, species):
        self.ref = species[0]  # the skeleton is shared
        self.parts = [Part(sp, *m) for sp in species for m in sp.meshes]
        sp = self.ref
        rest = sp.fk(_np().tile(_np().eye(4), (len(sp.bone_names), 1, 1)))
        self.rest_pitch = {leg: self.pitch(rest[sp.index[c[2]]]) for leg, c in LEGS.items()}
        self.margin = BASE_MARGIN

    @staticmethod
    def pitch(m):
        np = _np()
        y = m[:3, 1]
        return np.degrees(np.arctan2(y[2], np.hypot(y[0], y[1])))

    def pose_with(self, basis, leg, d):
        sp = self.ref
        b = basis.copy()
        for k, bone in enumerate(LEGS[leg]):
            if d[k]:
                b[sp.index[bone]] = b[sp.index[bone]] @ rot_x(d[k])
        pose = sp.fk(b)
        return pose, sp.skin(pose)

    def worst(self, skin, leg):
        return max(p.deficit(skin, p.by_leg[leg]) for p in self.parts)

    def body_need(self, basis):
        skin = self.ref.skin(self.ref.fk(basis))
        return max(p.deficit(skin, p.body) for p in self.parts)

    def flat_dp(self, basis, leg, du, dl):
        paw = self.ref.index[LEGS[leg][2]]
        dp = 0.0
        for _ in range(4):
            p0 = self.pitch(self.pose_with(basis, leg, (du, dl, dp))[0][paw])
            p1 = self.pitch(self.pose_with(basis, leg, (du, dl, dp + 1.0))[0][paw])
            if abs(p1 - p0) < 1e-6:
                break
            dp -= (p0 - self.rest_pitch[leg]) / (p1 - p0)
        return dp

    def ik(self, basis, leg, target):
        np = _np()
        paw = self.ref.index[LEGS[leg][2]]
        du = dl = 0.0
        for _ in range(8):
            a = self.pose_with(basis, leg, (du, dl, 0.0))[0][paw][:3, 3]
            r = np.array([target[1] - a[1], target[2] - a[2]])
            if np.abs(r).max() < 2e-5:
                break
            J = np.empty((2, 2))
            for k, (eu, el) in enumerate(((0.5, 0), (0, 0.5))):
                a2 = self.pose_with(basis, leg, (du + eu, dl + el, 0.0))[0][paw][:3, 3]
                J[:, k] = [(a2[1] - a[1]) / 0.5, (a2[2] - a[2]) / 0.5]
            try:
                step = np.clip(np.linalg.solve(J, r), -20, 20)
            except np.linalg.LinAlgError:
                break
            du += step[0]; dl += step[1]
        return du, dl

    def lifted(self, basis, leg, lo, hi):
        """Smallest ankle lift in [lo, hi] (paw flat, horizontal place kept) that keeps the leg above the floor."""
        np = _np()
        a0 = self.pose_with(basis, leg, (0, 0, 0))[0][self.ref.index[LEGS[leg][2]]][:3, 3].copy()
        def at(lift):
            du, dl = self.ik(basis, leg, a0 + np.array([0, 0, lift]))
            dp = self.flat_dp(basis, leg, du, dl)
            return (du, dl, dp), self.worst(self.pose_with(basis, leg, (du, dl, dp))[1], leg)
        best = at(hi)
        while best[1] > -self.margin and hi < 0.08:
            lo, hi = hi, hi * 2 if hi > 0 else 0.01
            best = at(hi)
        for _ in range(14):
            mid = (lo + hi) / 2
            d, w = at(mid)
            if w <= -self.margin:
                hi, best = mid, (d, w)
            else:
                lo = mid
        return best

    def solve(self, basis, leg, contact):
        """(du, dl, dp, mode, residual) for one leg at one frame."""
        w0 = self.worst(self.pose_with(basis, leg, (0, 0, 0))[1], leg)
        if contact and abs(w0 + self.margin) <= 0.0003:
            return 0.0, 0.0, 0.0, "ok", w0
        if contact and w0 < -self.margin:
            (du, dl, dp), w = self.lifted(basis, leg, -0.06, 0.04)
            return du, dl, dp, "contact", w
        if w0 <= 0.0001:
            return 0.0, 0.0, 0.0, "ok", w0
        dpf = self.flat_dp(basis, leg, 0, 0)
        if self.worst(self.pose_with(basis, leg, (0, 0, dpf))[1], leg) <= -self.margin:
            lo, hi = 0.0, 1.0
            for _ in range(12):
                mid = (lo + hi) / 2
                if self.worst(self.pose_with(basis, leg, (0, 0, dpf * mid))[1], leg) <= -self.margin:
                    hi = mid
                else:
                    lo = mid
            return 0.0, 0.0, dpf * hi, "paw", self.worst(self.pose_with(basis, leg, (0, 0, dpf * hi))[1], leg)
        (du, dl, dp), w = self.lifted(basis, leg, 0.0, 0.01)
        return du, dl, dp, "leg", w


def solve_clip(species, clip, name, extra):
    """Per frame: Root lift (m) and leg offsets (du, dl, dp, degrees)."""
    np = _np()
    solver = Solver([species[s] for s in players(name)])
    sp = solver.ref
    root = sp.index["Root"]
    contact, rmax, sticky = name in CONTACT, RMAX.get(name, 0.003), STICKY.get(name, 0.0)
    frames = list(range(clip["frames"] + 1))

    def lift_root(basis, r):
        b = basis.copy(); b[root] = basis[root].copy(); b[root][1, 3] += r
        return b

    bases, body, lift = [], [], []
    for f in frames:
        solver.margin = BASE_MARGIN + extra.get(f, 0.0)
        basis = sp.basis(clip, f * clip["sub"])
        b = solver.body_need(basis)
        b = b + solver.margin if b > -solver.margin else 0.0
        skin = sp.skin(sp.fk(lift_root(basis, b)))
        legs_need = max(0.0, max(solver.worst(skin, leg) for leg in LEGS) + solver.margin)
        bases.append(basis); body.append(b)
        lift.append(b if contact else b + min(rmax, legs_need))
    # smooth (a loop wraps), never below what the body needs
    lift, body = np.array(lift), np.array(body)
    def pad(a):
        return np.r_[a[-3:-1], a, a[1:3]] if clip["loop"] else np.r_[[a[0]] * 2, a, [a[-1]] * 2]
    peak = np.array([pad(lift)[i:i + 5].max() for i in range(len(lift))])
    smooth = np.convolve(pad(peak), np.array([1, 2, 3, 2, 1]) / 9.0, mode="valid")
    lift = np.maximum(np.maximum(smooth, body), 0.0)
    lift[lift < 0.0001] = 0.0

    out = {"root": [], **{leg: [] for leg in LEGS}}
    modes = {leg: [] for leg in LEGS}
    lifted = []
    for f, basis, r in zip(frames, bases, lift):
        solver.margin = BASE_MARGIN + extra.get(f, 0.0)
        basis = lift_root(basis, r)
        lifted.append(basis)
        out["root"].append((f, float(r)))
        for leg in LEGS:
            du, dl, dp, mode, _ = solver.solve(basis, leg, contact)
            out[leg].append((f, du, dl, dp))
            modes[leg].append(mode)
    if sticky and not contact:
        for leg in LEGS:
            for i in range(len(frames)):
                big = [j for j in (i - 1, i + 1) if 0 <= j < len(frames) and modes[leg][j] == "leg" and abs(out[leg][j][2]) > sticky]
                if modes[leg][i] == "ok" and big:
                    solver.margin = BASE_MARGIN + extra.get(frames[i], 0.0)
                    (du, dl, dp), w = solver.lifted(lifted[i], leg, -0.06, 0.04)
                    if w <= 0 and abs(dl) < abs(out[leg][big[0]][2]):
                        out[leg][i] = (frames[i], du, dl, dp)
    return out


def simplify(keys, tol):
    """Douglas-Peucker on (frame, value), linear; the first and last keys stay."""
    if len(keys) <= 2:
        return keys
    a, b = keys[0], keys[-1]
    dist = [abs(v - (a[1] + (f - a[0]) / (b[0] - a[0]) * (b[1] - a[1]))) for f, v in keys[1:-1]]
    i = max(range(len(dist)), key=dist.__getitem__) + 1
    if dist[i - 1] <= tol:
        return [a, b]
    return simplify(keys[:i + 1], tol)[:-1] + simplify(keys[i:], tol)


def planted_block(hand, solved):
    def fmt(keys, digits):
        return "[" + ", ".join(f"({f}, {round(v, digits) + 0:g})" for f, v in keys) + "]"
    lines = []
    for name in FIX:
        sol, clip = solved[name], hand[name]
        sub, ch = clip["sub"], clip["ch"]
        entries = []
        if any(r for _, r in sol["root"]):
            keys = [(f, ch["Root|loc|1"][f * sub] + r) for f, r in sol["root"]]
            entries.append(f'("Root", "lz"): {fmt(simplify(keys, 0.00004), 4)}')
        for leg, chain in LEGS.items():
            for k, bone in enumerate(chain):
                if max(abs(row[1 + k]) for row in sol[leg]) < 0.05:
                    continue
                keys = [(row[0], math.degrees(ch[f"{bone}|rot|0"][row[0] * sub]) + row[1 + k]) for row in sol[leg]]
                entries.append(f'("{bone}", "rx"): {fmt(simplify(keys, 0.1), 1)}')
        if entries:
            lines.append(f'    "{name}": {{\n        ' + ",\n        ".join(entries) + "},")
    return "PLANTED = {\n" + "\n".join(lines) + "\n}\n"


def write_block(lib_text, block):
    start = lib_text.index("PLANTED = {\n")
    end = lib_text.index("\n}\n", start) + 3
    return lib_text[:start] + block + lib_text[end:]


def load_species(work):
    out = {}
    for s in SPECIES:
        path = os.path.join(work, s + ".npz")
        _blender(species_blend(s), "skin", path)
        out[s] = Species(s, path)
    return out


def measure(species, clips, names):
    """{clip: {species: (lowest mm, frame)}} over every sample of the dumped clips."""
    res = {}
    for c in names:
        clip = clips[c]
        res[c] = {}
        for s in players(c):
            sp = species[s]
            low = min((sp.lowest(sp.skin(sp.fk(sp.basis(clip, k)))), k / clip["sub"]) for k in range(clip["frames"] * clip["sub"] + 1))
            res[c][s] = (round(low[0] * 1000, 1), low[1])
    return res


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "check"
    work = tempfile.mkdtemp(prefix="pw_ground_")
    species = load_species(work)
    if mode == "check":
        built = dump_clips(work, LIBRARY, SUB, True)
        res = measure(species, built, sorted(built))
        bad = 0
        for c, row in res.items():
            print(c.ljust(20), "  ".join(f"{s}:{v[0]}" for s, v in row.items()))
            bad += sum(1 for v in row.values() if v[0] < -2.0)
        print("played pairs below -2 mm:", bad)
        return
    hand = dump_clips(work, LIBRARY, 1, False)
    text = open(LIBRARY, encoding="utf-8").read()
    extra = {c: {} for c in FIX}
    solved, todo = {}, list(FIX)
    for it in range(12):
        for c in todo:
            solved[c] = solve_clip(species, hand[c], c, extra[c])
        trial = os.path.join(work, "pw_anim_library.py")
        open(trial, "w", encoding="utf-8", newline="\n").write(write_block(text, planted_block(hand, solved)))
        built = dump_clips(work, trial, SUB, True)
        todo = []
        for c in FIX:
            clip, bump = built[c], {}
            for s in players(c):
                sp = species[s]
                limit = max(min(LIMIT, sp.floor - 0.00005), -0.0019)
                for k in range(clip["frames"] * SUB + 1):
                    z = sp.lowest(sp.skin(sp.fk(sp.basis(clip, k))))
                    if z < limit:
                        for f in {k // SUB, -(-k // SUB)}:
                            bump[f] = max(bump.get(f, 0.0), limit - z + 0.0003)
            if bump:
                todo.append(c)
                for f, v in bump.items():
                    extra[c][f] = extra[c].get(f, 0.0) + v
        print("pass", it, "clips still dipping:", todo)
        if not todo:
            open(LIBRARY, "w", encoding="utf-8", newline="\n").write(write_block(text, planted_block(hand, solved)))
            print("PLANTED written to", LIBRARY)
            return
    raise SystemExit("Did not converge: " + ", ".join(todo))


if __name__ == "__main__":
    try:
        import bpy  # noqa: F401  (started by _blender: dump mode)
    except ImportError:
        main()
    else:
        _blender_main(sys.argv[sys.argv.index("--") + 1:])
