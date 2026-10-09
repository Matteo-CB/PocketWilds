"""Han ideographs for Pocket Round and Pocket Plush (FH): kanji, simplified and traditional hanzi.

Every character is drawn by our own code from our own data, written by hand:
  pw_font_han_components*.txt  stroke skeletons of components (radicals, phonetic parts, simple characters)
  pw_font_han_ids*.txt         the character set: each character as an Ideographic Description Sequence over the
                               components, per region (J = Japanese shinjitai, S = Simplified, T = Traditional)
The composition engine places the components (IDS operators), sets the stroke width per character so dense
characters keep an even colour, checks that unconnected strokes never touch, and renders round-pen strokes with
the Pocket Round terminals. No outline, skeleton or metric comes from any other font.

API for the build hook (pw_font_build.build_style):
  SCRIPT, REGIONS
  charset(region)            characters of one region, in data order
  make(ch, style, region)    the Glyphs.make tuple (shapes, lc, rc, upper, (x0, x1)); with build_style's
                             spacing it gives a 1000-unit advance and keeps the ink centred where it is
  features(style)            "" (no OpenType feature: one font per region)

Data syntax (both files, one entry per line, '#' starts a comment):
  NAME [props] : stroke | stroke ...     primitive: stroke skeletons on a 0..100 grid, y down
  NAME [props] = IDS                     composite: an IDS over other names
  NAME is one character or {ascii_name}, optionally .V (position variant: L R T B) and @J/@S/@T (regions).
  NAME.W is the whole-character drawing: used only when NAME is the character itself, never as a part (the
  bare name stays the narrow part other characters compose with).
  IDS: an operator may carry ratios and a sub-box, ⿰:0.4@0,6,100,100 A B; a component may carry a sub-box,
  A@x0,y0,x1,y1 (percent of its slot). The operator's sub-box places its whole subtree.
  stroke: kind[:curvature] x,y x,y ...   kinds h s t d p n z (z = turns and hooks); a point may end with
          '~' (smooth knot) or 'rN' (rounded corner of radius N).
  props:  w=share in a left-right split, h=share in a top-bottom split, a=min:max aspect (height / width),
          f=x0,y0,x1,y1 frame, in=x0,y0,x1,y1 inner box of an enclosure, al / ax alignment when the aspect
          is clamped (0..1).
  ids file lines: CHAR REGIONS STROKES IDS, where IDS may be '*' (the component of the same name) and STROKES is
  the expected stroke count (a number or J10,S9,T10) used by the verification.
"""
import glob
import math
import os
import re
from functools import lru_cache

import numpy as np

from pw_font_engine import Path, Stroke

HERE = os.path.dirname(os.path.abspath(__file__))

SCRIPT = "Han"
REGIONS = ("JP", "SC", "TC")
REGION_LETTER = {"JP": "J", "SC": "S", "TC": "T"}

# Metrics shared with the kana module (FJ): full-em advance, ideographic em box -120..880, ink face ~880.
ADVANCE = 1000
EM_BOTTOM, EM_TOP = -120, 880
FACE = 880.0
FACE_CX, FACE_CY = 500.0, 380.0

IDC = "⿰⿱⿲⿳⿴⿵⿶⿷⿸⿹⿺⿻"

REF_W = 92 * 0.80       # Han base stroke of Pocket Round Regular, the reference for density thinning
PITCH_K = 0.43          # stroke / pitch ratio where thinning starts (Regular)
PITCH_PCT = 25          # the character's pitch: this percentile of the gaps between neighbouring strokes
FMIN = 0.62             # thinnest stroke, relative to the style's base stroke
LOCAL_MIN = 0.45        # a stroke may thin locally (to 0.45 x the Regular stroke) rather than touch another
WHITE_MIN = 0.30        # clear white between unconnected strokes, x the Regular stroke
LOCAL_K = 0.50          # in the densest zones a stroke takes at most this part of the gap to its parallel neighbour
LOCAL_FMIN = 0.40       # ... but never thinner than this part of the style's base stroke
DENSE_K = 0.045         # heavy weights: lighter strokes per stroke above 14 (scaled by how much heavier than Regular)
DENSE_MIN = 0.78        # ... down to this factor at most
ENCLOSE_GAP = 0.78
PRIM_SCALE = 1.05       # whole characters drawn as one component (人, 大, 水...) open up to the composed ones' reach      # an enclosure hugs its content a little closer than two side-by-side parts


# ------------------------------------------------------------------------------------------------ data


class Spec:
    """One authored stroke: kind, points [(x, y, flag, radius)], curvature (p and n)."""

    __slots__ = ("kind", "pts", "curv")

    def __init__(self, kind, pts, curv=None):
        self.kind = kind
        self.pts = pts
        self.curv = curv


class Entry:
    __slots__ = ("name", "regions", "props", "strokes", "ids", "src")

    def __init__(self, name, regions, props, strokes=None, ids=None, src=""):
        self.name = name
        self.regions = regions
        self.props = props
        self.strokes = strokes
        self.ids = ids
        self.src = src


_NAME = re.compile(r"^(\{[^}]+\}|.)((?:\.[A-Z]+)?)((?:@[JST]+)?)$")
_PT = re.compile(r"^([pn]?)(-?[\d.]+),(-?[\d.]+)(\^?)(~|r[\d.]+)?$")


def _parse_props(text):
    props = {}
    for tok in text.split():
        k, v = tok.split("=", 1)
        if k in ("f", "in"):
            props[k] = tuple(float(x) for x in v.split(","))
        elif k == "a":
            lo, hi = v.split(":")
            props[k] = (float(lo), float(hi))
        else:
            props[k] = float(v)
    return props


def _parse_stroke(text, where):
    toks = text.split()
    head = toks[0]
    kind, curv = head, None
    if ":" in head:
        kind, c = head.split(":")
        curv = float(c)
    if kind not in ("h", "s", "t", "d", "p", "n", "z"):
        raise ValueError(f"{where}: unknown stroke kind {kind!r}")
    pts = []
    for tok in toks[1:]:
        m = _PT.match(tok)
        if not m:
            raise ValueError(f"{where}: bad point {tok!r}")
        flag, rad = "", 0.0
        if m.group(5) == "~":
            flag = "~"
        elif m.group(5):
            flag, rad = "r", float(m.group(5)[1:])
        if m.group(4):
            flag += "^"                 # snap this point onto the nearest earlier stroke of the component
        if m.group(1):
            flag = flag + m.group(1)      # the segment arriving at this point is a p or n curve
        pts.append((float(m.group(2)), float(m.group(3)), flag, rad))
    if len(pts) < 2:
        raise ValueError(f"{where}: a stroke needs two points")
    return Spec(kind, pts, curv)


_LOT = re.compile(r"^pw_font_han_(?:components|ids)_([a-z]\d\d)\.txt$")


def _data_files(kind):
    """Data files of one kind. Batch lots (pw_font_han_ids_c02.txt...) are all loaded, unless the environment
    variable PW_HAN_LOTS names the lots to load ("c02" or "c02,c03"; "none" = the shared files only), so a batch
    agent works on the shared files plus its own lot while the other lots are still being written."""
    files = sorted(glob.glob(os.path.join(HERE, f"pw_font_han_{kind}*.txt")))
    want = os.environ.get("PW_HAN_LOTS")
    if want is None:
        return files
    lots = {x.strip().lower() for x in want.split(",") if x.strip()}
    out = []
    for f in files:
        m = _LOT.match(os.path.basename(f))
        if m is None or m.group(1) in lots:
            out.append(f)
    return out


class Data:
    def __init__(self):
        self.entries = {}       # name -> [Entry]
        self.charsets = {r: [] for r in REGIONS}
        self.expected = {}      # (char, region) -> stroke count
        self.lines = {}         # (char, region) -> source line
        comps = _data_files("components")
        idss = _data_files("ids")
        for path in comps + idss:
            self._load(path, ids_file=path in idss)

    def _add(self, e):
        lst = self.entries.setdefault(e.name, [])
        for o in lst:
            if (o.regions is None and e.regions is None) or (o.regions and e.regions and o.regions & e.regions):
                raise ValueError(f"{e.src}: {e.name} already defined at {o.src}")
        lst.append(e)

    def _load(self, path, ids_file):
        base = os.path.basename(path)
        with open(path, encoding="utf-8") as f:
            for no, raw in enumerate(f, 1):
                line = raw.split("#", 1)[0].strip()
                if not line:
                    continue
                where = f"{base}:{no}"
                if ids_file and not re.match(r"^\S+\s*(\[[^\]]*\])?\s*[:=]", line):
                    self._load_char(line, where)
                    continue
                m = re.match(r"^(\S+?)\s*(?:\[([^\]]*)\])?\s*([:=])\s*(.*)$", line)
                if not m:
                    raise ValueError(f"{where}: cannot parse {line!r}")
                full, props, op, body = m.groups()
                nm = _NAME.match(full)
                if not nm:
                    raise ValueError(f"{where}: bad name {full!r}")
                name = nm.group(1) + nm.group(2)
                regions = set(nm.group(3)[1:]) if nm.group(3) else None
                p = _parse_props(props or "")
                if op == ":":
                    strokes = [_parse_stroke(s.strip(), where) for s in body.split("|") if s.strip()]
                    self._add(Entry(name, regions, p, strokes=strokes, src=where))
                else:
                    self._add(Entry(name, regions, p, ids=body.strip(), src=where))

    def _load_char(self, line, where):
        parts = line.split()
        if len(parts) < 4:
            raise ValueError(f"{where}: expected CHAR REGIONS STROKES IDS")
        ch, regs, strokes, ids = parts[0], parts[1], parts[2], "".join(parts[3:])
        if len(ch) != 1:
            raise ValueError(f"{where}: {ch!r} is not one character")
        letters = set(regs)
        if not letters <= set("JST"):
            raise ValueError(f"{where}: bad regions {regs!r}")
        counts = {}
        for tok in strokes.split(","):
            if tok[0] in "JST":
                for r in tok.rstrip("0123456789"):
                    counts[r] = int(tok.lstrip("JST"))
            else:
                for r in "JST":
                    counts[r] = int(tok)
        if ids != "*":
            self._add(Entry(ch, letters, {}, ids=ids, src=where))
        for region in REGIONS:
            L = REGION_LETTER[region]
            if L in letters:
                if ch in self.charsets[region]:
                    raise ValueError(f"{where}: {ch} listed twice for {region}")
                self.charsets[region].append(ch)
                self.expected[(ch, region)] = counts.get(L)
                self.lines[(ch, region)] = where

    def lookup(self, name, region):
        lst = self.entries.get(name)
        if not lst:
            return None
        L = REGION_LETTER[region]
        for e in lst:
            if e.regions and L in e.regions:
                return e
        for e in lst:
            if e.regions is None:
                return e
        return None


@lru_cache(maxsize=1)
def data():
    return Data()


def reload():
    data.cache_clear()
    _parse_ids.cache_clear()


def charset(region="JP"):
    return list(data().charsets[region])


def features(style):
    return ""


# ------------------------------------------------------------------------------------------------ IDS


@lru_cache(maxsize=None)
def _parse_ids(text):
    toks = []
    i = 0
    while i < len(text):
        c = text[i]
        if c.isspace():
            i += 1
            continue
        if c in IDC:
            j = i + 1
            ratios = None
            join = False
            if j < len(text) and text[j] == ":":
                k = j + 1
                while k < len(text) and (text[k].isdigit() or text[k] in ".,j"):
                    k += 1
                spec = text[j + 1:k]
                if "j" in spec:
                    join = True
                    spec = spec.replace("j", "")
                if spec:
                    ratios = tuple(float(x) for x in spec.split(",") if x)
                j = k
            sub = None
            if j < len(text) and text[j] == "@":        # sub-box for the whole subtree of this operator
                k = j + 1
                while k < len(text) and (text[k].isdigit() or text[k] in ".,-"):
                    k += 1
                sub = tuple(float(x) for x in text[j + 1:k].split(","))
                j = k
            toks.append(("op", c, (ratios, join, sub)))
            i = j
            continue
        if c == "{":
            j = text.index("}", i) + 1
            name = text[i:j]
        else:
            j = i + 1
            name = c
        var = ""
        if j < len(text) and text[j] == "." and j + 1 < len(text) and text[j + 1].isupper():
            k = j + 1
            while k < len(text) and text[k].isupper():
                k += 1
            var = text[j:k]
            j = k
        box = None
        if j < len(text) and text[j] == "@":
            k = j + 1
            while k < len(text) and (text[k].isdigit() or text[k] in ".,-"):
                k += 1
            box = tuple(float(x) for x in text[j + 1:k].split(","))
            j = k
        toks.append(("comp", name + var, box))
        i = j
    pos = [0]

    def node():
        t = toks[pos[0]]
        pos[0] += 1
        if t[0] == "comp":
            return t
        n = 3 if t[1] in "⿲⿳" else 2
        if t[1] == "⿻":
            n = 2
        kids = [node() for _ in range(n)]
        return ("op", t[1], t[2], kids)

    tree = node()
    if pos[0] != len(toks):
        raise ValueError(f"IDS {text!r}: trailing tokens")
    return tree


# ------------------------------------------------------------------------------------------------ style


class HanStyle:
    """Han stroke parameters derived from the Latin Style, so both scripts are one family."""

    def __init__(self, style):
        self.latin = style
        self.w = style.w * 0.80                 # base stroke of a simple character
        self.plush = style.inflate > 0
        self.swell = style.swell * 0.6
        self.inflate = style.inflate * 0.45
        self.face_skel = FACE - 0.85 * self.w   # skeleton box: the ink face stays ~880 at every weight
        self.unit = self.face_skel / 100.0      # font units per grid unit
        self.wg = self.w / self.unit            # base stroke in grid units
        self.white = 0.20 * self.wg + 4.0       # base white gap between parts (grid units)
        self.w_min = FMIN * self.w
        self.corner = 0.62 * self.w if self.plush else 0.0
        self.hscale = 0.94                      # horizontals a little lighter than verticals (optical)


# ------------------------------------------------------------------------------------------------ layout


class Placed:
    """A stroke placed in grid space of the whole face (0..100, y down)."""

    __slots__ = ("kind", "pts", "curv", "src", "inst", "orig")

    def __init__(self, kind, pts, curv, src, inst=0, orig=None):
        self.kind = kind
        self.pts = pts
        self.curv = curv
        self.src = src
        self.inst = inst
        self.orig = orig        # the authored points, for debugging


def _variants(name, ctx):
    """Position variant candidates. The innermost slot decides (the bottom of a left part takes the bottom
    form, not the narrow left form); a two-flag variant (.LB) wins when drawn; NAME.N forces the base."""
    base, dot, var = name.rpartition(".") if not name.endswith("}") and "." in name.split("}")[-1] else ("", "", "")
    if dot:
        return [base] if var == "N" else [name]
    ctx = [{"V": "T", "H": "L"}.get(c, c) for c in ctx]      # middles borrow the top / left forms
    out = []
    if len(ctx) >= 2:
        out.append(name + "." + ctx[-2] + ctx[-1])
    if ctx:
        out.append(name + "." + ctx[-1])
    out.append(name)
    return out


class Layout:
    def __init__(self, hs, region, gap_scale=1.0):
        self.hs = hs
        self.region = region
        self.gap_scale = gap_scale      # < 1 when the character's strokes thin (dense characters)
        self.d = data()
        self.inst = 0

    def resolve(self, name, ctx):
        for cand in _variants(name, ctx):
            e = self.d.lookup(cand, self.region)
            if e is not None:
                return self.follow(e)
        raise KeyError(f"no component {name!r} for {self.region} (context {''.join(ctx)})")

    def follow(self, e, depth=0):
        """An alias (NAME [props] = OTHER) takes the other entry's drawing with its own props on top."""
        if e.ids is None or depth > 8:
            return e
        tree = _parse_ids(e.ids)
        if tree[0] != "comp" or tree[2]:
            return e
        t = self.d.lookup(tree[1], self.region)
        if t is None:
            raise KeyError(f"{e.src}: alias target {tree[1]!r} missing for {self.region}")
        t = self.follow(t, depth + 1)
        props = dict(t.props)
        props.update(e.props)
        return Entry(e.name, e.regions, props, strokes=t.strokes, ids=t.ids, src=e.src)

    # -- measures used for automatic splits

    def counts(self, node, ctx):
        """(vertical-ish strokes, horizontal-ish strokes) of a node."""
        if node[0] == "comp":
            e = self.resolve(node[1], ctx)
            if e.ids is not None:
                return self.counts(_parse_ids(e.ids), ctx)
            nv = nh = 0
            for s in e.strokes:
                xs = [p[0] for p in s.pts]
                ys = [p[1] for p in s.pts]
                dx, dy = max(xs) - min(xs), max(ys) - min(ys)
                if s.kind == "d":
                    nv += 0.4
                    nh += 0.4
                elif dy > dx * 0.6:
                    nv += 1
                if dx > dy * 0.6 and s.kind != "d":
                    nh += 1
            return nv, nh
        nv = nh = 0
        for k in node[3]:
            a, b = self.counts(k, ctx)
            nv += a
            nh += b
        return nv, nh

    def share(self, node, ctx, key):
        if node[0] == "comp":
            e = self.resolve(node[1], ctx)
            if key in e.props:
                return e.props[key]
        return None

    def gap(self, extent):
        return self.hs.wg * self.gap_scale + self.hs.white * (0.78 + 0.22 * min(1.0, extent / 100.0))

    # -- placement

    def place(self, node, box, ctx, top=False, stick=None):
        if node[0] == "comp":
            if node[2] and not top:
                # explicit sub-box in percent of the slot
                x0, y0, x1, y1 = box
                bx0, by0, bx1, by1 = node[2]
                W, H = x1 - x0, y1 - y0
                box = (x0 + W * bx0 / 100, y0 + H * by0 / 100, x0 + W * bx1 / 100, y0 + H * by1 / 100)
            e = self.resolve(node[1], ctx)
            if e.ids is not None:
                return self.place(_parse_ids(e.ids), box, ctx, top, stick)
            return self.place_strokes(e, box, top, ctx, stick)
        _, op, spec, kids = node
        ratios, join = spec[0], spec[1]
        if len(spec) > 2 and spec[2] and not top:
            x0, y0, x1, y1 = box
            bx0, by0, bx1, by1 = spec[2]
            W, H = x1 - x0, y1 - y0
            box = (x0 + W * bx0 / 100, y0 + H * by0 / 100, x0 + W * bx1 / 100, y0 + H * by1 / 100)
        x0, y0, x1, y1 = box
        W, H = x1 - x0, y1 - y0
        out = []
        if op in "⿰⿲":
            n = len(kids)
            flags = ["L", "M", "R"] if n == 3 else ["L", "R"]
            g = 0.0 if join else self.gap(H)
            shares = self.split(kids, ctx, flags, ratios, "w", 0)
            avail = W - g * (n - 1)
            x = x0
            for k, sh, fl in zip(kids, shares, flags):
                wk = avail * sh
                st = None if not join else ("R" if fl == "L" else "L" if fl == "R" else None)
                out += self.place(k, (x, y0, x + wk, y1), ctx + ["H" if fl == "M" else fl], stick=st)
                x += wk + g
            return out
        if op in "⿱⿳":
            n = len(kids)
            flags = ["T", "M", "B"] if n == 3 else ["T", "B"]
            g = 0.0 if join else self.gap(W)
            shares = self.split(kids, ctx, flags, ratios, "h", 1)
            avail = H - g * (n - 1)
            y = y0
            for k, sh, fl in zip(kids, shares, flags):
                hk = avail * sh
                st = None if not join else ("B" if fl == "T" else "T" if fl == "B" else None)
                out += self.place(k, (x0, y, x1, y + hk), ctx + ["V" if fl == "M" else fl], stick=st)
                y += hk + g
            return out
        if op == "⿻":
            for k in kids:
                out += self.place(k, box, ctx)
            return out
        # enclosures: the outer component fills the box, the inner goes into its 'in' box
        outer, inner = kids
        e = None
        if outer[0] == "comp":
            o = self.d.lookup(outer[1] + ".O", self.region)     # a dedicated outer frame, if drawn
            e = self.follow(o) if o is not None else self.resolve(outer[1], ctx)
        if e is None or "in" not in e.props:
            raise KeyError(f"enclosure {outer!r} needs an 'in' box")
        strokes, fmap = self.map_entry(e, box, clamp=False)
        out += strokes
        ix0, iy0, ix1, iy1 = e.props["in"]
        (ax0, ay0), (ax1, ay1) = fmap(ix0, iy0), fmap(ix1, iy1)
        closed = {"⿴": "LTRB", "⿵": "LTR", "⿶": "LBR", "⿷": "TLB", "⿸": "TL", "⿹": "TR", "⿺": "LB"}[op]
        gx = 0.0 if join else ENCLOSE_GAP * self.gap(ay1 - ay0)
        gy = 0.0 if join else ENCLOSE_GAP * self.gap(ax1 - ax0)
        if "L" in closed:
            ax0 += gx
        if "R" in closed:
            ax1 -= gx
        if "T" in closed:
            ay0 += gy
        if "B" in closed:
            ay1 -= gy
        out += self.place(inner, (ax0, ay0, ax1, ay1), ctx)
        return out

    def split(self, kids, ctx, flags, ratios, key, axis):
        n = len(kids)
        if ratios:
            r = list(ratios) + [None] * (n - len(ratios))
        else:
            r = [self.share(k, ctx + [f if f != "M" else ("H" if axis == 0 else "V")], key) for k, f in zip(kids, flags)]
        if all(v is not None for v in r):
            s = sum(r)
            return [v / s for v in r]
        known = sum(v for v in r if v is not None)
        free = [i for i, v in enumerate(r) if v is None]
        if len(free) < n:
            rest = max(0.15, 1.0 - known)
            weights = [self.counts(kids[i], ctx)[axis] + 1.2 for i in free]
            tw = sum(weights)
            for i, wgt in zip(free, weights):
                r[i] = rest * wgt / tw
            s = sum(r)
            return [v / s for v in r]
        weights = [self.counts(k, ctx)[axis] + 1.2 for k in kids]
        tw = sum(weights)
        return [wgt / tw for wgt in weights]

    def frame(self, e):
        if "f" in e.props:
            return e.props["f"]
        xs = [p[0] for s in e.strokes for p in s.pts]
        ys = [p[1] for s in e.strokes for p in s.pts]
        if "in" in e.props:             # an enclosure's frame holds its inner box too
            ix0, iy0, ix1, iy1 = e.props["in"]
            xs += [ix0, ix1]
            ys += [iy0, iy1]
        return (min(xs), min(ys), max(xs), max(ys))

    def map_entry(self, e, box, clamp=True, ctx=(), edge=None):
        fx0, fy0, fx1, fy1 = self.frame(e)
        fw, fh = max(fx1 - fx0, 1e-6), max(fy1 - fy0, 1e-6)
        x0, y0, x1, y1 = box
        bw, bh = x1 - x0, y1 - y0
        if clamp:
            al_def = {"T": 0.0, "B": 1.0}.get(edge, None)
            ax_def = {"L": 0.0, "R": 1.0}.get(edge, None)
            if fh < 1.0 and fw >= 1.0:          # a lone horizontal stroke: keep it at the centre line
                cy = y0 + bh * (al_def if al_def is not None else e.props.get("al", 0.5))
                y0 = y1 = cy
                bh = 0.0
                fh = 1e-6
            elif fw < 1.0 and fh >= 1.0:        # a lone vertical stroke
                cx = x0 + bw * e.props.get("ax", 0.5)
                x0 = x1 = cx
                bw = 0.0
                fw = 1e-6
            else:
                a0 = fh / fw
                amin, amax = e.props.get("a", (a0 / 2.6, a0 * 2.0))
                # a part on top of or under another may flatten, a part beside another may stretch
                # (unless the component sets its own aspect range)
                if "a" in e.props:
                    pass
                elif ctx and ctx[-1] in "TBV":
                    amin *= 0.55
                elif ctx and ctx[-1] in "LRH":
                    amax *= 1.35
                a = bh / max(bw, 1e-6)
                if a > amax:
                    nh = bw * amax
                    al = al_def if al_def is not None else e.props.get("al", 0.5)
                    y0 = y0 + (bh - nh) * al
                    y1 = y0 + nh
                elif a < amin:
                    nw = bh / amin
                    ax = ax_def if ax_def is not None else e.props.get("ax", 0.5)
                    x0 = x0 + (bw - nw) * ax
                    x1 = x0 + nw
        sx = (x1 - x0) / fw
        sy = (y1 - y0) / fh

        def fmap(x, y):
            return (x0 + (x - fx0) * sx, y0 + (y - fy0) * sy)

        rs = min(abs(sx), abs(sy)) if min(abs(sx), abs(sy)) > 0 else max(abs(sx), abs(sy))
        out = []
        for s in e.strokes:
            pts = [(*fmap(x, y), fl, r * rs) for x, y, fl, r in s.pts]
            out.append(Placed(s.kind, pts, s.curv, e.name, self.inst, s.pts))
        self.inst += 1
        return out, fmap

    def place_strokes(self, e, box, top, ctx=(), stick=None):
        if top:
            # a primitive drawn as a whole character keeps its authored face coordinates, opened up slightly so its
            # long strokes reach about as far as those of composed characters
            k = PRIM_SCALE
            out = []
            for s in e.strokes:
                pts = [(50 + (x - 50) * k, 50 + (y - 50) * k, fl, r * k) for x, y, fl, r in s.pts]
                out.append(Placed(s.kind, pts, s.curv, e.name, self.inst, s.pts))
            self.inst += 1
            return out
        strokes, _ = self.map_entry(e, box, ctx=ctx, edge=stick)
        return strokes


# ------------------------------------------------------------------------------------------------ geometry


def _cubic(p0, p1, p2, p3, n):
    t = np.linspace(0, 1, n + 1)[1:, None]
    u = 1 - t
    return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3


def _pn_curve(s, e, kind, curv):
    """Auto-curved piě (p) or nà (n) from s to e: the piě starts steep and sweeps out, the nà sags a little."""
    d = e - s
    L = float(np.hypot(*d))
    if L < 1e-6:
        return np.array([e])
    u = d / L
    # font units are y up: the piě bulges to the lower right of its chord, the nà to the lower left
    if kind == "p":
        v = np.array([-u[1], u[0]])
        k = 0.30 if curv is None else curv
        th1, th2 = k * 80, k * 40
    else:
        v = np.array([u[1], -u[0]])
        k = 0.10 if curv is None else curv
        th1, th2 = k * 70, k * 70
    if k < 0:
        v = -v
    a1, a2 = math.radians(abs(th1)), math.radians(abs(th2))
    c1 = s + 0.36 * L * (u * math.cos(a1) + v * math.sin(a1))
    c2 = e - 0.32 * L * (u * math.cos(a2) - v * math.sin(a2))
    return _cubic(s, c1, c2, e, max(8, int(L / 4)))


def _seg_kind(flag):
    return "p" if "p" in flag else "n" if "n" in flag else ""


def _run_points(pts, kind, curv):
    """Dense centerline of one run (no sharp corner inside): lines, p/n curves, rounded corners, smooth knots."""
    P = [np.array(p[:2], float) for p in pts]
    if len(P) == 2 and kind in ("p", "n"):
        return np.vstack([P[0][None], _pn_curve(P[0], P[1], kind, curv)])
    out = [P[0]]
    smooth = any("~" in p[2] for p in pts[1:-1]) or (kind in ("p", "n") and len(P) > 2)
    if smooth:
        # Bessel tangents (chord weighted), handles of a third of each chord: no overshoot on uneven knots
        n = len(P)
        D = [P[i + 1] - P[i] for i in range(n - 1)]
        Lc = [max(float(np.hypot(*d)), 1e-6) for d in D]
        T = [None] * n
        for i in range(1, n - 1):
            t = (Lc[i] * D[i - 1] / Lc[i - 1] + Lc[i - 1] * D[i] / Lc[i]) / (Lc[i - 1] + Lc[i])
            T[i] = t / max(float(np.hypot(*t)), 1e-6)
        T[0] = D[0] / Lc[0]           # strokes leave their start along the first chord (a piě starts straight)
        T[-1] = D[-1] / Lc[-1]
        for i in range(n - 1):
            a, b = P[i], P[i + 1]
            h = Lc[i] / 3
            out.extend(_cubic(a, a + T[i] * h, b - T[i + 1] * h, b, max(6, int(Lc[i] / 4))))
        return np.array(out)
    # segments (lines or p/n curves) joined by rounded corners
    cur = P[0]
    for i in range(1, len(P)):
        p = P[i]
        sk = _seg_kind(pts[i][2])
        if sk:
            out.extend(_pn_curve(cur, p, sk, 0.16 if sk == "p" else 0.08))
            cur = p
            continue
        if i < len(P) - 1 and "r" in pts[i][2] and pts[i][3] > 0 and not _seg_kind(pts[i + 1][2]):
            a, b = P[i - 1], P[i + 1]
            la, lb = float(np.hypot(*(p - a))), float(np.hypot(*(b - p)))
            r = min(pts[i][3], la * 0.48, lb * 0.48)
            if r > 0.5:
                c1 = p + (a - p) / la * r
                c2 = p + (b - p) / lb * r
                out.extend(_line(cur, c1))
                k = 0.5523
                out.extend(_cubic(c1, c1 + (p - c1) * k, c2 + (p - c2) * k, c2, max(6, int(r / 2))))
                cur = c2
                continue
        out.extend(_line(cur, p))
        cur = p
    return np.array(out)


def _line(a, b):
    L = float(np.hypot(*(b - a)))
    n = max(1, int(L / 5))
    t = np.linspace(0, 1, n + 1)[1:, None]
    return list(a + (b - a) * t)


def _runs(pl, corner_r):
    """Split a stroke at its sharp corners (round joins come from the round caps). A corner can be rounded
    ('r' points, and every corner between straight segments in Plush) only between two straight segments."""
    pts = list(pl.pts)
    n = len(pts)
    if pl.kind in ("p", "n") and not any(_seg_kind(p[2]) for p in pts):
        return [pts]

    def straight(i):        # segment arriving at point i
        return not _seg_kind(pts[i][2])

    def seglen(i):
        return math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1])

    def turn(i):
        ax, ay = pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]
        bx, by = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
        c = (ax * bx + ay * by) / max(1e-6, math.hypot(ax, ay) * math.hypot(bx, by))
        return math.degrees(math.acos(max(-1.0, min(1.0, c))))

    fixed = []
    for i, (x, y, fl, r) in enumerate(pts):
        if 0 < i < n - 1 and "~" not in fl:
            can_round = straight(i) and straight(i + 1)
            if "r" not in fl and corner_r > 0:
                # an automatic rounded corner needs room on both sides and an open turn (hooks stay sharp)
                can_round = can_round and min(seglen(i), seglen(i + 1)) > 1.8 * corner_r and turn(i) < 125
            if corner_r > 0 and can_round and "r" not in fl:
                fl, r = fl + "r", corner_r
            elif "r" in fl and not can_round:
                fl = fl.replace("r", "")
        fixed.append((x, y, fl, r))
    runs = []
    cur = [fixed[0]]
    for i in range(1, n):
        cur.append(fixed[i])
        fl = fixed[i][2]
        if i < n - 1 and "~" not in fl and "r" not in fl:
            runs.append(cur)
            cur = [fixed[i]]
    runs.append(cur)
    return runs


# ------------------------------------------------------------------------------------------------ glyph


class HanGlyph:
    """Strokes of one character in font units, their widths, the touch check and the engine shapes."""

    def __init__(self, ch, region, hs, placed):
        self.ch = ch
        self.region = region
        self.hs = hs
        u = hs.unit
        X0 = FACE_CX - hs.face_skel / 2
        Y1 = FACE_CY + hs.face_skel / 2
        self.strokes = []
        for p in placed:
            pts = [(X0 + x * u, Y1 - y * u, fl, r * u) for x, y, fl, r in p.pts]
            self.strokes.append(Placed(p.kind, pts, p.curv, p.src, p.inst, p.orig))
        self.runs = []      # per stroke: list of dense point arrays
        for i, s in enumerate(self.strokes):
            if any("^" in p[2] for p in s.pts):
                self._snap(i)
            self.runs.append([_run_points(r, s.kind, s.curv) for r in _runs(s, hs.corner)])
        self._close_joints()
        self.widths = self._widths()
        self.problems = []
        self._touch_check()

    def _close_joints(self):
        """A stroke end that stops just short of another stroke of the same component was meant to meet it:
        in this design strokes either join or keep a clear white gap, never almost touch."""
        hs = self.hs
        for i, s in enumerate(self.strokes):
            thr = (0.30 if s.kind == "d" else 0.60) * hs.w
            moved = False
            for end in (0, -1):
                q = (self.runs[i][0][0] if end == 0 else self.runs[i][-1][-1])
                best, bp, bj = None, None, None
                for j, t in enumerate(self.strokes):
                    if j == i or t.inst != s.inst:
                        continue
                    P = np.vstack(self.runs[j])
                    dd = np.sqrt(((P - q) ** 2).sum(axis=1))
                    k = int(np.argmin(dd))
                    if best is None or dd[k] < best:
                        best, bp, bj = float(dd[k]), P[k], j
                # the nearest stroke is one this stroke already crosses or touches: the end is an overhang
                # (the verticals of 艹 above their bar) or already attached, not a joint left open
                if bj is not None and _polylines_cross(np.vstack(self.runs[i]), np.vstack(self.runs[bj])):
                    best = None
                if best is not None and 0.04 * hs.w < best < thr:
                    x, y, fl, r = s.pts[end]
                    s.pts[end] = (float(bp[0]), float(bp[1]), fl, r)
                    moved = True
            if moved:
                self.runs[i] = [_run_points(r, s.kind, s.curv) for r in _runs(s, hs.corner)]

    def _snap(self, i):
        s = self.strokes[i]
        prev = [np.vstack(self.runs[j]) for j in range(i) if self.strokes[j].inst == s.inst]
        if not prev:
            return
        allp = np.vstack(prev)
        pts = []
        for x, y, fl, r in s.pts:
            if "^" in fl:
                d = ((allp - (x, y)) ** 2).sum(axis=1)
                k = int(np.argmin(d))
                x, y = float(allp[k, 0]), float(allp[k, 1])
            pts.append((x, y, fl.replace("^", ""), r))
        s.pts = pts

    # -- even colour

    def _orient(self, a, b):
        dx, dy = abs(b[0] - a[0]), abs(b[1] - a[1])
        if dy <= dx * 0.36:
            return "h"
        if dx <= dy * 0.36:
            return "v"
        return "d"

    def _pitch(self, axis):
        """Low percentile of the distance between neighbouring parallel strokes along one axis."""
        segs = []
        for i, runs in enumerate(self.runs):
            if self.strokes[i].kind == "d":
                continue            # dots are local: the touch check gives them room
            for pts in runs:
                for a, b in zip(pts[:-1], pts[1:]):
                    o = self._orient(a, b)
                    if (axis == "v" and o == "h") or (axis == "h" and o == "v") or o == "d":
                        segs.append((i, a, b))
        if len(segs) < 2:
            return None
        k = 1 if axis == "v" else 0      # coordinate measured across
        j = 1 - k
        lo = min(min(a[j], b[j]) for _, a, b in segs)
        hi = max(max(a[j], b[j]) for _, a, b in segs)
        gaps = []
        for t in np.arange(lo + 6, hi - 5, 12.0):
            hits = []
            for i, a, b in segs:
                if (a[j] - t) * (b[j] - t) <= 0 and a[j] != b[j]:
                    f = (t - a[j]) / (b[j] - a[j])
                    hits.append((a[k] + (b[k] - a[k]) * f, i))
            hits.sort()
            for (c1, i1), (c2, i2) in zip(hits, hits[1:]):
                d = c2 - c1
                if i1 != i2 and d > self.hs.w * 0.35 and not self._conn[i1][i2]:
                    gaps.append(d)
        if not gaps:
            return None
        return float(np.percentile(gaps, PITCH_PCT))

    def _connections(self):
        """conn[i][j]: strokes i and j touch or cross (gaps between them are not counters)."""
        n = len(self.runs)
        pts = [np.vstack(r)[::2] for r in self.runs]
        conn = [[False] * n for _ in range(n)]
        lim = 0.3 * self.hs.w
        for i in range(n):
            for j in range(i + 1, n):
                A, B = pts[i], pts[j]
                if (A[:, 0].min() - B[:, 0].max() > lim or B[:, 0].min() - A[:, 0].max() > lim or
                        A[:, 1].min() - B[:, 1].max() > lim or B[:, 1].min() - A[:, 1].max() > lim):
                    continue
                if _min_dist(A, B) < lim:
                    conn[i][j] = conn[j][i] = True
        return conn

    def _widths(self):
        """Dense characters get thinner strokes for an even colour. The factor is measured against the Regular
        stroke, so every weight thins in the same proportion and Bold stays bolder; counters keep at least a
        third of the pitch."""
        hs = self.hs
        self._conn = self._connections()
        pv, ph = self._pitch("v"), self._pitch("h")
        fv = 1.0 if not pv else min(1.0, max(FMIN, PITCH_K * pv / REF_W))
        fh = 1.0 if not ph else min(1.0, max(FMIN, PITCH_K * ph / REF_W))
        wh = hs.w * hs.hscale * fv
        wv = hs.w * fh
        if pv:
            wh = max(min(wh, 0.64 * pv), hs.w * FMIN * hs.hscale * 0.9)
        if ph:
            wv = max(min(wv, 0.64 * ph), hs.w * FMIN * 0.9)
        self.wh, self.wv = wh, wv
        out = []
        for s, runs in zip(self.strokes, self.runs):
            lh = lv = ld = 0.0
            for pts in runs:
                for a, b in zip(pts[:-1], pts[1:]):
                    L = float(np.hypot(*(b - a)))
                    o = self._orient(a, b)
                    if o == "h":
                        lh += L
                    elif o == "v":
                        lv += L
                    else:
                        ld += L
            tot = lh + lv + ld or 1.0
            w = (lh * wh + lv * wv + ld * (wh + wv) / 2) / tot
            if s.kind == "d":
                w = (wh + wv) / 2 * 1.04
            out.append(w)
        out = self._local_thin(out)
        # heavy weights: the most complex characters (15 strokes and more) draw a little lighter overall, so 寶 or
        # 鑑 keep their counters open at small sizes; Regular is unchanged
        n = len(self.strokes)
        heavy = max(0.0, hs.w / REF_W - 1.0)
        if n >= 15 and heavy > 0:
            f = max(DENSE_MIN, 1.0 - DENSE_K * heavy * (n - 14))
            out = [w * f for w in out]
        return out

    def _local_thin(self, widths):
        """The densest zones of a character (the 王 and 缶 of 寶, the 目 of a small part) thin further than the
        character's own factor: a horizontal or vertical stroke takes at most about half of the distance to its
        nearest parallel, unconnected neighbour, so heavy weights stay open at small sizes."""
        hs = self.hs
        cap_k = min(0.58, LOCAL_K * (hs.w / REF_W) ** 0.35)
        info = []
        for s, runs in zip(self.strokes, self.runs):
            P = np.vstack(runs)
            if s.kind == "d" or len(P) < 2:
                info.append(None)
                continue
            dx, dy = np.ptp(P[:, 0]), np.ptp(P[:, 1])
            if dy <= dx * 0.36:
                info.append(("h", float(P[:, 1].mean()), float(P[:, 0].min()), float(P[:, 0].max())))
            elif dx <= dy * 0.36:
                info.append(("v", float(P[:, 0].mean()), float(P[:, 1].min()), float(P[:, 1].max())))
            else:
                info.append(None)
        out = list(widths)
        for i, a in enumerate(info):
            if a is None:
                continue
            best = None
            for j, b in enumerate(info):
                if j == i or b is None or b[0] != a[0] or self._conn[i][j]:
                    continue
                ov = min(a[3], b[3]) - max(a[2], b[2])
                if ov < 0.3 * min(a[3] - a[2], b[3] - b[2]):
                    continue
                d = abs(a[1] - b[1])
                if best is None or d < best:
                    best = d
            if best is not None:
                out[i] = min(out[i], max(hs.w * LOCAL_FMIN, cap_k * best))
        return out

    # -- nothing touches

    def _dense(self, i):
        pts = np.vstack(self.runs[i])
        seg = np.hypot(*np.diff(pts, axis=0).T)
        s = np.concatenate([[0], np.cumsum(seg)])
        n = max(2, int(s[-1] / 8) + 1)
        t = np.linspace(0, s[-1], n)
        return np.stack([np.interp(t, s, pts[:, 0]), np.interp(t, s, pts[:, 1])], axis=1)

    def _length(self, i):
        return float(sum(np.hypot(*np.diff(p, axis=0).T).sum() for p in self.runs[i]))

    def _pair(self, i, j, dense, widths, white_min):
        """None when the pair is fine, else (d, need, pa, pb)."""
        hs = self.hs
        A, B = dense[i], dense[j]
        lim = hs.w * 2.5
        if (A[:, 0].min() - B[:, 0].max() > lim or B[:, 0].min() - A[:, 0].max() > lim or
                A[:, 1].min() - B[:, 1].max() > lim or B[:, 1].min() - A[:, 1].max() > lim):
            return None
        d, pa, pb = _closest(A, B)
        if d < 0.22 * hs.w:
            return None         # joined by design
        need = (widths[i] + widths[j]) / 2 + white_min
        if d >= need:
            return None
        if any(k != i and k != j and _pt_dist(pa, dense[k]) < 0.5 * hs.w and _pt_dist(pb, dense[k]) < 0.5 * hs.w
               for k in range(len(dense))):
            return None         # both meet a third stroke there (木: piě and nà leave the same crossing)
        return d, need, pa, pb

    def _contacts(self, m, dense):
        """(start attached, end attached, touches elsewhere) for stroke m."""
        hs = self.hs
        P = dense[m]
        others = [dense[j] for j in range(len(dense)) if j != m]
        if not others:
            return False, False, False
        Q = np.vstack(others)

        def near(p):
            return float(np.sqrt(((Q - p) ** 2).sum(axis=1)).min()) < 0.12 * hs.w
        inner = any(near(p) for p in P[2:-2]) if len(P) > 4 else False
        return near(P[0]), near(P[-1]), inner

    def _make_room(self, m, other_pt, deficit, dense):
        """Shorten a short stroke from its free end nearest the neighbour; nudge it away only if it touches
        nothing (an isolated dot). Strokes that join others never move away from their joints."""
        hs = self.hs
        s = self.strokes[m]
        if len(s.pts) != 2:
            return False
        a0, a1, inner = self._contacts(m, dense)
        (x0, y0, f0, r0), (x1, y1, f1, r1) = s.pts
        L = math.hypot(x1 - x0, y1 - y0)
        if L < 1e-6:
            return False
        d0 = math.hypot(x0 - other_pt[0], y0 - other_pt[1])
        d1 = math.hypot(x1 - other_pt[0], y1 - other_pt[1])
        ux, uy = (x1 - x0) / L, (y1 - y0) / L
        cut = min(deficit * 1.15, L * (0.45 if L < 2.8 * hs.w else 0.18))
        if d1 <= d0 and not a1:
            x1, y1 = x1 - ux * cut, y1 - uy * cut
        elif d0 < d1 and not a0:
            x0, y0 = x0 + ux * cut, y0 + uy * cut
        else:
            cut = 0.0
        rest = deficit * 1.15 - cut
        if rest > 0 and not (a0 or a1 or inner):
            # nudge the whole stroke away from the neighbour, a little
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            vx, vy = cx - other_pt[0], cy - other_pt[1]
            vn = math.hypot(vx, vy) or 1.0
            sh = min(rest, 0.45 * hs.w)
            x0, y0, x1, y1 = x0 + vx / vn * sh, y0 + vy / vn * sh, x1 + vx / vn * sh, y1 + vy / vn * sh
        if (x0, y0, x1, y1) == (s.pts[0][0], s.pts[0][1], s.pts[1][0], s.pts[1][1]):
            return False
        s.pts = [(x0, y0, f0, r0), (x1, y1, f1, r1)]
        self.runs[m] = [_run_points(r, s.kind, s.curv) for r in _runs(s, hs.corner)]
        return True

    def _touch_check(self):
        hs = self.hs
        white_min = WHITE_MIN * REF_W     # the same clear white in every weight
        widths = list(self.widths)
        n = len(self.runs)
        dense = [self._dense(i) for i in range(n)]
        # 1. short strokes (dots, ticks) make room for their neighbours
        for _ in range(3):
            changed = False
            for i in range(n):
                for j in range(i + 1, n):
                    r = self._pair(i, j, dense, widths, white_min)
                    if r is None:
                        continue
                    d, need, pa, pb = r
                    li, lj = self._length(i), self._length(j)
                    short_i = self.strokes[i].kind == "d" or li < 2.8 * hs.w
                    short_j = self.strokes[j].kind == "d" or lj < 2.8 * hs.w
                    if short_i and (not short_j or li <= lj):
                        m, other = i, pb
                    elif short_j:
                        m, other = j, pa
                    else:
                        # two long strokes: the one whose free end is the close point gives way
                        ends_i = (dense[i][0], dense[i][-1])
                        ends_j = (dense[j][0], dense[j][-1])
                        if min(float(np.hypot(*(e - pa))) for e in ends_i) < 0.6 * hs.w:
                            m, other = i, pb
                        elif min(float(np.hypot(*(e - pb))) for e in ends_j) < 0.6 * hs.w:
                            m, other = j, pa
                        else:
                            continue
                    if self._make_room(m, other, need - d, dense):
                        dense[m] = self._dense(m)
                        changed = True
            if not changed:
                break
        self.dense = dense
        # an end that lands on another stroke is a joint, not a terminal: no swell there
        self.free = []
        for i, P in enumerate(dense):
            ends = []
            for q in (P[0], P[-1]):
                near = min((float(np.sqrt(((D - q) ** 2).sum(axis=1)).min()) for j, D in enumerate(dense) if j != i),
                           default=1e9)
                ends.append(near > 0.55 * hs.w)
            self.free.append(ends)
        # 2. what is still too close thins locally, down to LOCAL_MIN
        for i in range(n):
            for j in range(i + 1, n):
                r = self._pair(i, j, dense, widths, white_min)
                if r is None:
                    continue
                d, need, pa, pb = r
                total = 2.0 * (d - white_min)          # the two widths must sum to this
                floor = REF_W * LOCAL_MIN
                a, b = widths[i], widths[j]
                if total < 2 * floor:
                    self.problems.append((i, j, round(d, 1), round(need, 1)))
                    continue
                # the thicker stroke gives way first, then both
                big, small = (a, b) if a >= b else (b, a)
                cut = a + b - total
                d1 = min(cut, big - small)
                big -= d1
                cut -= d1
                big -= cut / 2
                small -= cut / 2
                if a >= b:
                    widths[i], widths[j] = big, small
                else:
                    widths[i], widths[j] = small, big
        self.widths = widths

    # -- shapes

    def shapes(self):
        hs = self.hs
        out = []
        total_len = []
        for runs in self.runs:
            total_len.append(sum(float(np.hypot(*np.diff(p, axis=0).T).sum()) for p in runs))
        for si, (s, runs) in enumerate(zip(self.strokes, self.runs)):
            w = self.widths[si]
            Ltot = total_len[si] or 1.0
            offset = 0.0
            for ri, pts in enumerate(runs):
                L = float(np.hypot(*np.diff(pts, axis=0).T).sum()) or 1.0
                start_end = ri == 0 and self.free[si][0]
                last = ri == len(runs) - 1
                hook = last and len(runs) > 1 and L < 1.6 * w
                end_free = last and not hook and self.free[si][1]
                out.append(Stroke(_path(pts), w, _width_fn(hs, w, L, Ltot, offset, start_end, end_free, s.kind)))
                offset += L
        return out

    def ink_bounds(self):
        xs0, xs1, ys0, ys1 = [], [], [], []
        for si, runs in enumerate(self.runs):
            h = self.widths[si] * (0.5 + 0.5 * (self.hs.swell + self.hs.inflate))
            for pts in runs:
                xs0.append(pts[:, 0].min() - h)
                xs1.append(pts[:, 0].max() + h)
                ys0.append(pts[:, 1].min() - h)
                ys1.append(pts[:, 1].max() + h)
        return min(xs0), min(ys0), max(xs1), max(ys1)


def _polylines_cross(A, B):
    """True when two polylines cross or touch (an end already on the other line counts; collinear does not)."""
    if len(A) < 2 or len(B) < 2:
        return False
    a0, a1 = A[:-1][:, None, :], A[1:][:, None, :]
    b0, b1 = B[:-1][None, :, :], B[1:][None, :, :]

    def side(p, q, r):
        return (q[..., 0] - p[..., 0]) * (r[..., 1] - p[..., 1]) - (q[..., 1] - p[..., 1]) * (r[..., 0] - p[..., 0])
    d1, d2 = side(a0, a1, b0), side(a0, a1, b1)
    d3, d4 = side(b0, b1, a0), side(b0, b1, a1)
    # a crossing may fall exactly on a sample point of both lines: count touching, not collinear overlap
    hit = (d1 * d2 <= 0) & (d3 * d4 <= 0) & ~((d1 == 0) & (d2 == 0)) & ~((d3 == 0) & (d4 == 0))
    return bool(np.any(hit))


def _min_dist(A, B):
    d = A[:, None, :] - B[None, :, :]
    return float(np.sqrt((d * d).sum(axis=2)).min())


def _closest(A, B):
    d = A[:, None, :] - B[None, :, :]
    dd = (d * d).sum(axis=2)
    k = int(np.argmin(dd))
    i, j = divmod(k, dd.shape[1])
    return float(math.sqrt(dd[i, j])), A[i], B[j]


def _pt_dist(p, P):
    return float(np.sqrt(((P - p) ** 2).sum(axis=1)).min())


def _path(pts):
    p = Path(pts[0][0], pts[0][1])
    p.points = [(float(x), float(y)) for x, y in pts]
    return p


def _width_fn(hs, w, L, Ltot, offset, true_start, true_end, kind):
    sw = hs.swell
    inf = hs.inflate if Ltot > 2.2 * w else hs.inflate * 0.4
    Ls = min(max(0.125 * Ltot, 0.35 * w), 0.8 * w)

    def fn(t):
        s = t * L
        m = 1.0
        if true_start and sw:
            m += sw * max(0.0, 1.0 - s / Ls)
        if true_end and sw:
            m += sw * max(0.0, 1.0 - (L - s) / Ls)
        if inf:
            m += inf * math.sin(math.pi * min(1.0, max(0.0, (offset + s) / Ltot)))
        return m
    return fn


# ------------------------------------------------------------------------------------------------ API


def build_glyph(ch, style, region="JP"):
    hs = _hanstyle(style)
    d = data()
    e = d.lookup(ch + ".W", region) if ch in d.charsets[region] else None
    if e is None:
        e = d.lookup(ch, region)
    if e is None:
        raise KeyError(f"{ch} (U+{ord(ch):04X}) has no data for {region}")
    lay = Layout(hs, region)
    if e.ids is not None:
        placed = lay.place(_parse_ids(e.ids), (0.0, 0.0, 100.0, 100.0), [], top=True)
        g = HanGlyph(ch, region, hs, placed)
        # a dense character draws thinner strokes, so its parts can sit closer: lay it out again with gaps
        # measured on the strokes it really draws (more room inside 寶, 實, 鑑 at heavy weights)
        r = float(np.median(g.widths)) / hs.w
        if r < 0.92:
            lay = Layout(hs, region, gap_scale=max(0.6, r))
            placed = lay.place(_parse_ids(e.ids), (0.0, 0.0, 100.0, 100.0), [], top=True)
            g = HanGlyph(ch, region, hs, placed)
    else:
        placed = lay.place_strokes(e, (0.0, 0.0, 100.0, 100.0), True)
        g = HanGlyph(ch, region, hs, placed)
    # optical centring: the ink box centred on the advance
    x0, y0, x1, y1 = g.ink_bounds()
    dx = ADVANCE / 2 - (x0 + x1) / 2
    for runs in g.runs:
        for pts in runs:
            pts[:, 0] += dx
    return g


_HS = {}


def _hanstyle(style):
    key = (style.key, style.weight_name, style.w)
    if key not in _HS:
        _HS[key] = HanStyle(style)
    return _HS[key]


def make(ch, style, region="JP"):
    """Glyphs.make tuple. build_style puts lsb = side_bearing(P) and shifts by lsb - x0 = 0, so the ink stays
    centred and the advance is lsb + (x1 - x0) + rsb = 1000."""
    from pw_font_build import side_bearing
    g = build_glyph(ch, style, region)
    sb_l = side_bearing(style, "P", False)
    sb_r = side_bearing(style, "P", False)
    return g.shapes(), "P", "P", False, (sb_l, ADVANCE - sb_r)


def stroke_count(ch, region="JP"):
    """Strokes in our data for one character (expanded through every component)."""
    from pw_font_build import FAMILIES
    g = build_glyph(ch, FAMILIES["PocketRound"][0], region)
    return len(g.strokes)
