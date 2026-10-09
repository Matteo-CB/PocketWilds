"""Korean Hangul for Pocket Round and Pocket Plush (fonts for every language, 2026-10-09).

Jamo are drawn by our own stroke skeletons in the Caline / Guimauve manner: round caps, near-monoline strokes,
puffier and rounder in Plush. Nothing comes from another font.

Syllables U+AC00..U+D7A3 are composed algorithmically: index = (initial * 21 + medial) * 28 + final.
Every jamo is drawn once per layout group, already in place in the em, so a syllable is 2 or 3 references to
these parts (TrueType composite glyphs, offset 0): the TTF stays small and every syllable of a group shares the
same tuned geometry.

Layout groups: the vowel shape (13 classes: right stem vowels, below vowels, mixed vowels, split by width and
tick side) x final present or not. The six classic layout types are the three vowel kinds (V right, H below,
M mixed) with and without a final. Stroke widths shrink from the simple groups (V without final) to the crowded
ones (M with final), horizontal strokes more than vertical ones, like good Hangul fonts.

API for the merge hook in pw_font_build.py:
    SCRIPT                  "hangul"
    charset()               U+3131..U+318E (94 compatibility jamo) then U+AC00..U+D7A3 (11,172 syllables), as str
    make(ch, style)         the Glyphs.make tuple; with build_style's spacing it gives an advance of exactly
                            ADVANCE (1000) and leaves the ink where it is drawn; syllables as full outlines
    advance(ch, style)      ADVANCE (1000) for every character
    parts(style)            {part glyph name: shapes}: the unencoded jamo parts, already in place in the em
    part_order(style)       the part names in a stable order
    composite(ch, style)    the part names of a syllable (components at offset 0), None for a jamo
    features(style)         "" (no OpenType feature needed)
Small fonts: write the parts as glyphs and each syllable as a composite of composite(ch) (see
pw_font_hangul_test.write_test_font); make() is the fallback when composites are not wanted (much larger file).
"""
import math
import unicodedata

import numpy as np

import pw_font_engine as E
from pw_font_engine import Dot, Path, Stroke

SCRIPT = "hangul"
ADVANCE = 1000
SBASE = 0xAC00

INITIALS = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ"
MEDIALS = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ"
FINALS = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ"

# ASCII names for the part glyphs.
CNAME = {
    "ㄱ": "g", "ㄲ": "kk", "ㄳ": "gs", "ㄴ": "n", "ㄵ": "nj", "ㄶ": "nh", "ㄷ": "d", "ㄸ": "tt", "ㄹ": "r", "ㄺ": "rg",
    "ㄻ": "rm", "ㄼ": "rb", "ㄽ": "rs", "ㄾ": "rt", "ㄿ": "rp", "ㅀ": "rh", "ㅁ": "m", "ㅂ": "b", "ㅃ": "pp", "ㅄ": "bs",
    "ㅅ": "s", "ㅆ": "ss", "ㅇ": "ng", "ㅈ": "j", "ㅉ": "jj", "ㅊ": "ch", "ㅋ": "k", "ㅌ": "t", "ㅍ": "p", "ㅎ": "h",
}
VNAME = dict(zip(MEDIALS, "a ae ya yae eo e yeo ye o wa wae oe yo u wo we wi yu eu ui i".split()))

# Vowel classes: where the vowel sits and how much room it takes.
VCLASS = {
    "ㅏ": "A", "ㅑ": "A", "ㅣ": "I", "ㅓ": "E", "ㅕ": "E", "ㅐ": "AE", "ㅒ": "AE", "ㅔ": "EE", "ㅖ": "EE",
    "ㅗ": "O", "ㅛ": "O", "ㅜ": "U", "ㅠ": "U", "ㅡ": "EU",
    "ㅘ": "OA", "ㅚ": "OA", "ㅙ": "OAE", "ㅝ": "UE", "ㅟ": "UE", "ㅞ": "UEE", "ㅢ": "EUI",
}
VCLASSES = ["A", "I", "E", "AE", "EE", "O", "U", "EU", "OA", "OAE", "UE", "UEE", "EUI"]
KIND = {"A": "V", "I": "V", "E": "V", "AE": "V", "EE": "V", "O": "H", "U": "H", "EU": "H",
        "OA": "M", "OAE": "M", "UE": "M", "UEE": "M", "EUI": "M"}

# Clusters drawn as two (or three) consonants side by side: parts and the share of the width of each.
CLUSTER = {
    "ㄲ": ("ㄱㄱ", (0.46, 0.54)), "ㄸ": ("ㄷㄷ", (0.5, 0.5)), "ㅃ": ("ㅂㅂ", (0.5, 0.5)), "ㅆ": ("ㅅㅅ", (0.46, 0.54)),
    "ㅉ": ("ㅈㅈ", (0.47, 0.53)), "ㄳ": ("ㄱㅅ", (0.46, 0.54)), "ㄵ": ("ㄴㅈ", (0.42, 0.58)), "ㄶ": ("ㄴㅎ", (0.40, 0.60)),
    "ㄺ": ("ㄹㄱ", (0.52, 0.48)), "ㄻ": ("ㄹㅁ", (0.5, 0.5)), "ㄼ": ("ㄹㅂ", (0.5, 0.5)), "ㄽ": ("ㄹㅅ", (0.5, 0.5)),
    "ㄾ": ("ㄹㅌ", (0.5, 0.5)), "ㄿ": ("ㄹㅍ", (0.5, 0.5)), "ㅀ": ("ㄹㅎ", (0.48, 0.52)), "ㅄ": ("ㅂㅅ", (0.5, 0.5)),
    # archaic letters (compatibility jamo only)
    "ㅥ": ("ㄴㄴ", (0.5, 0.5)), "ㅦ": ("ㄴㄷ", (0.5, 0.5)), "ㅧ": ("ㄴㅅ", (0.46, 0.54)), "ㅨ": ("ㄴㅿ", (0.46, 0.54)),
    "ㅩ": ("ㄹㄱㅅ", (0.36, 0.30, 0.34)), "ㅪ": ("ㄹㄷ", (0.5, 0.5)), "ㅫ": ("ㄹㅂㅅ", (0.35, 0.32, 0.33)),
    "ㅬ": ("ㄹㅿ", (0.5, 0.5)), "ㅭ": ("ㄹㆆ", (0.5, 0.5)), "ㅮ": ("ㅁㅂ", (0.5, 0.5)), "ㅯ": ("ㅁㅅ", (0.5, 0.5)),
    "ㅰ": ("ㅁㅿ", (0.5, 0.5)), "ㅲ": ("ㅂㄱ", (0.5, 0.5)), "ㅳ": ("ㅂㄷ", (0.5, 0.5)), "ㅴ": ("ㅂㅅㄱ", (0.34, 0.33, 0.33)),
    "ㅵ": ("ㅂㅅㄷ", (0.34, 0.33, 0.33)), "ㅶ": ("ㅂㅈ", (0.5, 0.5)), "ㅷ": ("ㅂㅌ", (0.5, 0.5)), "ㅺ": ("ㅅㄱ", (0.5, 0.5)),
    "ㅻ": ("ㅅㄴ", (0.5, 0.5)), "ㅼ": ("ㅅㄷ", (0.5, 0.5)), "ㅽ": ("ㅅㅂ", (0.5, 0.5)), "ㅾ": ("ㅅㅈ", (0.5, 0.5)),
    "ㆀ": ("ㅇㅇ", (0.5, 0.5)), "ㆂ": ("ㆁㅅ", (0.5, 0.5)), "ㆃ": ("ㆁㅿ", (0.5, 0.5)), "ㆅ": ("ㅎㅎ", (0.5, 0.5)),
}
# Light letters (ㅸ: a consonant over a small ring).
LIGHT = {"ㅱ": "ㅁ", "ㅸ": "ㅂ", "ㅹ": "ㅃ", "ㆄ": "ㅍ"}
# Archaic compound vowels (compatibility jamo only): a below vowel then a right vowel.
OLD_VOWELS = {"ㆇ": ("ㅛ", "ㅑ"), "ㆈ": ("ㅛ", "ㅒ"), "ㆉ": ("ㅛ", "ㅣ"), "ㆊ": ("ㅠ", "ㅕ"), "ㆋ": ("ㅠ", "ㅖ"),
              "ㆌ": ("ㅠ", "ㅣ")}


def _check_tables():
    """The jamo orders must match Unicode (a wrong order would give wrong syllables)."""
    comp = {unicodedata.name(chr(c)).split()[-1]: chr(c) for c in range(0x3131, 0x318F) if c != 0x3164}
    lead = "".join(comp[unicodedata.name(chr(0x1100 + i)).split()[-1]] for i in range(19))
    vow = "".join(comp[unicodedata.name(chr(0x1161 + i)).split()[-1]] for i in range(21))
    tail = "".join(comp[unicodedata.name(chr(0x11A8 + i)).split()[-1]] for i in range(27))
    assert (lead, vow, tail) == (INITIALS, MEDIALS, FINALS), "jamo tables differ from Unicode"


_check_tables()


def decompose(ch):
    """(initial, medial, final or '') of a precomposed syllable."""
    i = ord(ch) - SBASE
    if not 0 <= i < 11172:
        raise KeyError(ch)
    return INITIALS[i // 588], MEDIALS[(i % 588) // 28], ("" if i % 28 == 0 else FINALS[i % 28 - 1])


def charset():
    """Compatibility jamo U+3131..U+318E, then the 11,172 syllables U+AC00..U+D7A3."""
    return [chr(c) for c in range(0x3131, 0x318F)] + [chr(c) for c in range(SBASE, SBASE + 11172)]


# ------------------------------------------------------------------------------------------- style


class HStyle:
    """Hangul parameters derived from the Latin Style, so both scripts share weight and feel."""

    def __init__(self, style):
        self.style = style
        self.plush = style.inflate > 0
        # Same stem as the Latin in simple syllables; crowded ones get thinner strokes (GROUP and CAPS).
        self.w0 = style.w * (1.10 if self.plush else 0.97)
        self.inflate = style.inflate * 0.70
        self.swell = style.swell
        self.n = style.n
        self.heavy = max(0.0, (self.w0 - 89.0) / 89.0)  # 0 for Regular, about 0.57 for Bold

    # (vertical factor, horizontal factor) per kind and final.
    GROUP = {("V", False): (1.00, 0.93), ("H", False): (0.98, 0.90), ("M", False): (0.95, 0.87),
             ("V", True): (0.93, 0.86), ("H", True): (0.91, 0.82), ("M", True): (0.89, 0.80), ("C", False): (1.0, 0.93)}
    # Largest (vertical, horizontal) widths per group in font units: in heavy weights the crowded syllables
    # keep open counters, as in good bold Hangul.
    CAPS = {("V", False): (165, 150), ("H", False): (148, 104), ("M", False): (124, 98),
            ("V", True): (134, 90), ("H", True): (120, 62), ("M", True): (110, 60), ("C", False): (160, 150)}

    # Two-stem vowels (ㅐ ㅔ ㅙ ㅞ) leave less room to the initial: lighter verticals in heavy weights.
    CLASS_CAP = {"AE": 116, "EE": 110, "OAE": 106, "UEE": 102}

    def widths(self, kind, fin, vc=None):
        fv, fh = self.GROUP[(kind, fin)]
        cv, ch = self.CAPS[(kind, fin)]
        if fin:
            # heavy weights keep a little more weight in the syllables with a final (a crowded jamo still thins
            # itself to keep its counters): the color of a line stays even
            ch += 14 * min(1.0, self.heavy * 1.75)
        # the display style is used large: a little more weight where it fits (not in the crowded groups)
        k = (1.08 if not fin else 0.97) if self.plush else 1.0
        cv, ch = cv * k, ch * k
        wv = min(self.w0 * fv, cv, self.CLASS_CAP.get(vc, 999) * k)
        return wv, min(self.w0 * fh, ch, wv * 0.97)


# ------------------------------------------------------------------------------------------- pen


class Pen:
    """Strokes with round caps; the width follows the direction (vertical wv, horizontal wh)."""

    def __init__(self, hs, wv, wh):
        self.hs, self.wv, self.wh = hs, wv, wh
        self.rv, self.rh = wv / 2, wh / 2
        self.shapes = []

    def _add(self, path, inflate=True, taper=1.0):
        """taper < 1: the stroke starts thinner (a branch leaving another stroke keeps the joint light)."""
        pts = np.array(path.points, dtype=float)
        if len(pts) < 2:
            return
        closed = path.closed
        if closed:
            d = np.roll(pts, -1, axis=0) - np.roll(pts, 1, axis=0)
        else:
            d = np.empty_like(pts)
            d[1:-1] = pts[2:] - pts[:-2]
            d[0] = pts[1] - pts[0]
            d[-1] = pts[-1] - pts[-2]
        nrm = np.linalg.norm(d, axis=1)
        nrm[nrm == 0] = 1
        vert = (d[:, 1] / nrm) ** 2
        w = self.wh + (self.wv - self.wh) * vert
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        s = np.concatenate([[0], np.cumsum(seg)])
        length = s[-1]
        u = s / length if length > 0 else s
        if not closed:
            hs = self.hs
            if hs.swell > 0:
                end = np.maximum(0.0, 1.0 - np.minimum(u, 1.0 - u) * 8.0)
                w = w * (1.0 + hs.swell * end)
            if hs.inflate > 0 and inflate:
                w = w * (1.0 + hs.inflate * min(1.0, length / 420.0) * np.sin(np.pi * u) * (0.35 + 0.65 * vert))
            if taper < 1.0:
                k = np.clip(u / 0.35, 0.0, 1.0)
                w = w * (taper + (1.0 - taper) * (3 * k * k - 2 * k * k * k))
        uu, ww = u.copy(), w.copy()

        def fn(v, uu=uu, ww=ww):
            return float(np.interp(v, uu, ww))
        self.shapes.append(Stroke(path, 1.0, fn))

    def seg(self, x0, y0, x1, y1):
        if math.hypot(x1 - x0, y1 - y0) < 1.0:
            x1 += 0.5
        self._add(Path(x0, y0).line(x1, y1))

    def hbar(self, xa, xb, y):
        """Horizontal stroke whose ink spans xa..xb."""
        r = self.rh
        if xb - xa < 2 * r + 2:
            xa, xb = (xa + xb) / 2 - r - 1, (xa + xb) / 2 + r + 1
        self.seg(xa + r, y, xb - r, y)

    def vbar(self, x, ya, yb):
        """Vertical stroke whose ink spans ya..yb."""
        r = self.rv
        if yb - ya < 2 * r + 2:
            ya, yb = (ya + yb) / 2 - r - 1, (ya + yb) / 2 + r + 1
        self.seg(x, ya + r, x, yb - r)

    def poly(self, pts, allow_close=True):
        """Connected strokes through centerline points. Round: one stroke per segment (crisp inner corners, like
        the Latin E or L). Plush: one path with rounded corners (soft, bubbly inner corners); a path that comes
        back to its start is closed."""
        if not self.hs.plush or len(pts) < 3:
            # One stroke per segment. Where a thick stroke meets a thinner one at a corner, its round cap would
            # stick out past the thin stroke into the counter: it is pulled back so both outer edges meet flush.
            segs = list(zip(pts, pts[1:]))
            loop = len(pts) > 3 and math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) < 1.0
            k = 1.0 + self.hs.swell

            def width(a, b):
                dx, dy = b[0] - a[0], b[1] - a[1]
                n = math.hypot(dx, dy) or 1.0
                return self.wh + (self.wv - self.wh) * (dy / n) ** 2
            ws = [width(a, b) for a, b in segs]
            for i, (a, b) in enumerate(segs):
                dx, dy = b[0] - a[0], b[1] - a[1]
                n = math.hypot(dx, dy) or 1.0
                ux, uy = dx / n, dy / n
                c0 = max(0.0, (ws[i] - ws[i - 1]) / 2 * k) if (i > 0 or loop) else 0.0
                c1 = max(0.0, (ws[i] - ws[(i + 1) % len(segs)]) / 2 * k) if (i < len(segs) - 1 or loop) else 0.0
                f = min(1.0, 0.45 * n / max(c0 + c1, 1e-6))
                c0, c1 = c0 * f, c1 * f
                self.seg(a[0] + ux * c0, a[1] + uy * c0, b[0] - ux * c1, b[1] - uy * c1)
            return
        closed = allow_close and math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) < 1.0
        if closed:
            pts = pts[:-1]
        hs = self.hs
        hmax = max(self.rv, self.rh) * (1.0 + (0.0 if closed else hs.inflate) + hs.swell * 0.5)
        rad = hmax * 1.32
        n = len(pts)
        corners = range(n) if closed else range(1, n - 1)
        arcs = {}
        for i in corners:
            a, c, b = (np.array(pts[(i - 1) % n], float), np.array(pts[i], float), np.array(pts[(i + 1) % n], float))
            d1, d2 = c - a, b - c
            l1, l2 = np.linalg.norm(d1), np.linalg.norm(d2)
            d1, d2 = d1 / l1, d2 / l2
            turn = math.acos(max(-1.0, min(1.0, float(np.dot(d1, d2)))))
            if turn < 1e-3:
                continue
            off = min(rad * math.tan(turn / 2), 0.49 * l1, 0.49 * l2)
            r = off / math.tan(turn / 2)
            if r < hmax * 1.06:
                # too tight to round without a fold: draw the two sides as separate strokes
                if closed:
                    self.poly(list(pts[i:]) + list(pts[:i + 1]), False)
                else:
                    self.poly(list(pts[:i + 1]))
                    self.poly(list(pts[i:]))
                return
            k = 4.0 / 3.0 * math.tan(turn / 4) * r
            arcs[i] = (c - d1 * off, c + d2 * off, d1, d2, k)
        if closed:
            start = arcs[0][1] if 0 in arcs else np.array(pts[0], float)
            p = Path(*start)
            order = list(range(1, n)) + [0]
        else:
            p = Path(*pts[0])
            order = list(range(1, n))
        for i in order:
            if i in arcs:
                pa, pb, d1, d2, k = arcs[i]
                p.line(*pa)
                p.curve(*(pa + d1 * k), *(pb - d2 * k), *pb)
            else:
                p.line(*pts[i])
        if closed:
            p.close()
        self._add(p)

    def curve(self, knots, tension=0.39, taper=1.0):
        x, y, _ = knots[0]
        path = Path(x, y).spline(knots, tension)
        self._add(path, taper=taper)
        return path

    def ring(self, x0, y0, x1, y1):
        """Closed ring whose ink fills the box."""
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        rx, ry = (x1 - x0) / 2 - self.rv, (y1 - y0) / 2 - self.rh
        n = self.hs.n
        p = Path(*E.superellipse(cx, cy, rx, ry, 0, n)).arc(cx, cy, rx, ry, 0, 360, n).close()
        self._add(p)

    def dot(self, x, y, r):
        self.shapes.append(Dot(x, y, r))


# ------------------------------------------------------------------------------------------- consonants
#
# Each consonant fills an ink box (x0, y0, x1, y1). form: 'v' initial before a right vowel (sweeping legs),
# 'm' initial before a mixed vowel, 'h' initial over a below vowel, 'f' final, 'c' standalone letter.


def _shrink(box, sx, sy=None):
    x0, y0, x1, y1 = box
    sy = sx if sy is None else sy
    dx, dy = (x1 - x0) * sx, (y1 - y0) * sy
    return (x0 + dx, y0 + dy, x1 - dx, y1 - dy)


class _Local:
    """Temporarily different stroke widths for one letter (a crowded letter thins itself a little)."""

    def __init__(self, pen, wv=None, wh=None):
        self.pen, self.wv, self.wh = pen, wv, wh

    def __enter__(self):
        p = self.pen
        self.old = (p.wv, p.wh, p.rv, p.rh)
        if self.wv is not None:
            p.wv, p.rv = self.wv, self.wv / 2
        if self.wh is not None:
            p.wh, p.rh = self.wh, self.wh / 2
        return p

    def __exit__(self, *a):
        p = self.pen
        p.wv, p.wh, p.rv, p.rh = self.old


def _fit(w, room, n, floor):
    """Width of n parallel strokes in room. Counters of 0.66 w + 8 are wanted; to keep them the strokes may thin
    down to floor; below that the counters may shrink, but never under 0.40 w + 6 (a closed counter changes the
    letter), even if the strokes must go thinner than floor (not under 0.45 w)."""
    need = n * w + (n - 1) * (0.66 * w + 8)
    if need <= room:
        return w
    nice = (room - (n - 1) * 8) / (n + (n - 1) * 0.66)
    tight = (room - (n - 1) * 6) / (n + (n - 1) * 0.40)
    return max(w * 0.45, max(nice, min(w * floor, tight)))


def _rows(pen, H, n, floor=0.78):
    return _Local(pen, wh=_fit(pen.wh, H, n, floor))


def _cols(pen, W, n, floor=0.72):
    return _Local(pen, wv=_fit(pen.wv, W, n, floor))


def _leg_x(path_pts, y):
    """x of a stroke centerline at height y (first crossing)."""
    for (xa, ya), (xb, yb) in zip(path_pts, path_pts[1:]):
        if (ya - y) * (yb - y) <= 0 and ya != yb:
            return xa + (xb - xa) * (y - ya) / (yb - ya)
    return path_pts[-1][0]


def _giyeok(pen, box, form, mid=False):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3 if mid else 2):
        rv, rh = pen.rv, pen.rh
        yt = y1 - rh
        xr = x1 - rv
        if form in ("v", "m", "n") and H > max(0.45 * W, 2.4 * pen.wv):
            sweep = {"v": 1.0, "m": 0.7, "n": 0.42}[form]
            if mid:
                sweep *= 0.72   # ㅋ: the bar needs the room under it, the leg sweeps less
            if H < 0.75 * W:
                sweep *= max(0.3, H / (0.75 * W))   # a short box: a short sweep, or the leg folds back
            k = [(xr, yt, 270), (xr - W * 0.05 * sweep, y0 + H * 0.52, 259 - 7 * sweep),
                 (x0 + W * (0.40 - 0.24 * sweep), y0 + rv * 0.95, 238 - 14 * sweep)]
            p = Path(xr, yt).spline(k, 0.42)
            pen.hbar(x0, xr + rh * 0.2, yt)
            pen._add(p)
            if mid:
                ym = y0 + (yt - y0) * 0.50
                pen.hbar(x0 + W * 0.03, _leg_x(p.points, ym) + rv * 0.4, ym)
        else:
            pen.poly([(x0 + rh, yt), (xr, yt), (xr, y0 + rv)])
            if mid:
                ym = y0 + (yt - rh - y0) * 0.52
                pen.hbar(x0, xr, ym)


def _nieun(pen, box, form):
    x0, y0, x1, y1 = box
    pen.poly([(x0 + pen.rv, y1 - pen.rv), (x0 + pen.rv, y0 + pen.rh), (x1 - pen.rh, y0 + pen.rh)])


def _digeut(pen, box, form, mid=False):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3 if mid else 2), _cols(pen, W, 2, 0.8):
        rv, rh = pen.rv, pen.rh
        top = x1 - W * 0.03
        pen.poly([(top - rh, y1 - rh), (x0 + rv, y1 - rh), (x0 + rv, y0 + rh), (x1 - rh, y0 + rh)])
        if mid:
            ym = (y0 + y1) / 2
            pen.hbar(x0 + rv, top - W * 0.03, ym)


def _rieul(pen, box, form):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3, 0.74), _cols(pen, W, 2, 0.8):
        # the short joins of ㄹ stay close to the bar width, or their ends would fill the small counters
        pen.wv = min(pen.wv, pen.wh * 1.35)
        pen.rv = pen.wv / 2
        rv, rh = pen.rv, pen.rh
        yt, yb = y1 - rh, y0 + rh
        ym = (yt + yb) / 2
        pen.poly([(x0 + rh, yt), (x1 - rv, yt), (x1 - rv, ym), (x0 + rv, ym), (x0 + rv, yb), (x1 - rh, yb)])


def _mieum(pen, box, form):
    x0, y0, x1, y1 = box
    with _rows(pen, y1 - y0, 2), _cols(pen, x1 - x0, 2):
        rv, rh = pen.rv, pen.rh
        pen.poly([(x0 + rv, y1 - rh), (x1 - rv, y1 - rh), (x1 - rv, y0 + rh), (x0 + rv, y0 + rh), (x0 + rv, y1 - rh)])


def _bieup(pen, box, form):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3, 0.76), _cols(pen, W, 2):
        rv, rh = pen.rv, pen.rh
        yb = y0 + rh
        ym = yb + (H - pen.wh) * 0.50
        pen.poly([(x0 + rv, y1 - rv), (x0 + rv, yb), (x1 - rv, yb), (x1 - rv, y1 - rv)])
        pen.hbar(x0 + rv, x1 - rv, ym)


def _wedge(pen, x0, y0, x1, y1, ax, ay, form, join):
    """The two legs of ㅅ (and of ㅈ under its bar): the left leg sweeps from the apex, the right leg branches off
    it. Tangents are set around each chord, so the legs bow gently whatever the box shape."""
    H = ay - y0
    W = x1 - x0
    if W < 0.62 * H:
        join = min(0.60, join + 0.06)
    narrow = W < 0.62 * H
    short = H < 0.45 * W
    hv = min(1.0, pen.hs.heavy * 1.6)
    k = 1.0 - 0.10 * hv - (0.06 + 0.10 * hv if narrow else 0.0) - (0.04 + 0.08 * hv if short else 0.0)
    with _Local(pen, wv=pen.wv * k, wh=pen.wh * max(k, 0.94)):
        _wedge_legs(pen, x0, y0, x1, y1, ax, ay, form, join, H)


def _wedge_legs(pen, x0, y0, x1, y1, ax, ay, form, join, H):
    r = (pen.rv + pen.rh) / 2
    lx, ly = x0 + r * 1.02, y0 + r
    chord = math.degrees(math.atan2(ly - ay, lx - ax)) % 360
    bow = 13 if form == "v" else 8
    if x1 - x0 < 0.62 * H:
        bow = 8 if form == "v" else 5
    mid = (ax + (lx - ax) * 0.55, ay + (ly - ay) * 0.55)
    left = Path(ax, ay).spline([(ax, ay, chord + bow), (mid[0] + (3 if form == "v" else 1.5) * bow * 0.6, mid[1] - bow * 0.6, chord),
                                (lx, ly, chord - bow)], 0.40)
    pen._add(left)
    sy = ay - H * join
    sx = _leg_x(left.points, sy)
    ex, ey = x1 - r * 1.02, y0 + r
    rc = math.degrees(math.atan2(ey - sy, ex - sx)) % 360
    pen.curve([(sx, sy, rc - 7), (ex, ey, rc + 5)], 0.40, taper=0.86 if pen.hs.plush else 0.74)


def _keep_angle(box, k=2.0):
    """A very wide, short box is narrowed (centered) so diagonal legs keep a real slope."""
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    if W > k * H:
        d = (W - k * H) / 2
        x0, x1 = x0 + d, x1 - d
    return x0, y0, x1, y1


def _siot(pen, box, form):
    x0, y0, x1, y1 = _keep_angle(box)
    W, H = x1 - x0, y1 - y0
    r = (pen.rv + pen.rh) / 2
    if form == "v":
        _wedge(pen, x0, y0, x1, y1, x0 + W * (0.60 if W < 0.62 * H else 0.54), y1 - r, "v", 0.50)
    else:
        _wedge(pen, x0, y0, x1, y1, x0 + W * 0.50, y1 - r, "h", 0.36 if 0.55 * W < H < 1.6 * W else 0.48)


def _ieung(pen, box, form):
    # a ring looks bigger than a square letter: smaller box, aspect kept near a circle
    sx = {"v": 0.07, "m": 0.06, "h": 0.06, "f": 0.05, "c": 0.06}[form]
    x0, y0, x1, y1 = _shrink(box, sx, 0.045)
    W, H = x1 - x0, y1 - y0
    if H > W * 1.08:
        d = (H - W * 1.08) / 2
        y0, y1 = y0 + d, y1 - d
    elif W > H * 1.30:
        d = (W - H * 1.30) / 2
        x0, x1 = x0 + d, x1 - d
    with _Local(pen, wv=min(pen.wv, (x1 - x0) * 0.29), wh=min(pen.wh, (y1 - y0) * 0.27)):
        pen.ring(x0, y0 - 6, x1, y1 + 6)


def _jieut(pen, box, form, tick=False):
    x0, y0, x1, y1 = _keep_angle(box, 2.2)
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3.4 if tick else 2.6, 0.76):
        rh = pen.rh
        r = (pen.rv + pen.rh) / 2
        th = 0.0
        tw = min(pen.wv, 0.85 * pen.wh + 10)   # the tick of ㅊ: a little lighter, so it reads as a stroke
        if tick:
            th = min(max(H * 0.15, tw * 1.25), H * 0.28)
        yb = y1 - th - rh
        if form == "v":
            cx = x0 + W * 0.52
            pen.hbar(x0 + W * 0.02, x1 - W * 0.07, yb)
            _wedge(pen, x0, y0, x1, y1, cx, yb, "v", 0.52 if W > 0.62 * H else 0.58)
        else:
            cx = x0 + W * 0.50
            pen.hbar(x0 + W * 0.03, x1 - W * 0.03, yb)
            _wedge(pen, x0, y0, x1, y1, cx, yb, "h", 0.36 if 0.55 * W < (yb - y0) < 1.6 * W else 0.50)
        if tick:
            with _Local(pen, wv=tw):
                pen.vbar(cx, yb, y1)


def _tieut(pen, box, form):
    _digeut(pen, box, form, mid=True)


def _pieup(pen, box, form):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3):
        wh = pen.wh
        # the bars overhang the legs (or ㅍ reads as ㅁ) and the legs keep a counter between them
        span = W * 0.96                       # the top bar
        o = 0.75 * wh                         # visible overhang
        wl = min(pen.wv, (span - 2 * o) / 2.6)
        if wl < 0.45 * pen.wv:
            wl = 0.45 * pen.wv
            o = max(0.30 * wh, (span - 2.6 * wl) / 2)
        with _Local(pen, wv=wl):
            yt, yb = y1 - pen.rh, y0 + pen.rh
            pen.hbar(x0 + W * 0.02, x1 - W * 0.02, yt)
            pen.hbar(x0, x1, yb)
            a = max(W * 0.24, W * 0.02 + o + wl / 2)
            a = min(a, (W - 1.5 * wl) / 2)
            pen.vbar(x0 + a, yb, yt)
            pen.vbar(x1 - a, yb, yt)


def _hieut(pen, box, form, tick=True):
    x0, y0, x1, y1 = box
    W, H = x1 - x0, y1 - y0
    with _rows(pen, H, 3.3 if tick else 2.6, 0.74):
        rh, wh = pen.rh, pen.wh
        tw = min(pen.wv, 0.85 * wh + 10)   # the tick of ㅎ: a little lighter, so it reads as a stroke
        t = (max(0.16 * H, 1.25 * tw) if form == "v" else max(0.12 * H, 1.25 * tw)) if tick else 0.0
        g = max(0.50 * wh, 0.07 * H, 18)
        ring_h = H - t - wh - g
        if ring_h < 0.56 * H:
            k = max(0.45, (H - wh - 0.56 * H) / max(1.0, t + g))
            t, g = t * k, max(g * k, 0.42 * wh + 8)
            ring_h = H - t - wh - g
        yb = y1 - t - rh
        cx = (x0 + x1) / 2
        ring_w = min(W * (0.80 if form in ("v", "m") else 0.66), ring_h * {"v": 1.12, "m": 1.30}.get(form, 1.42))
        if form not in ("v", "m") and W < 0.9 * H:
            # narrow box (a cluster such as ㄶ ㅀ): a round ring as wide as the letter allows
            ring_w = W * 0.86
            ring_h = min(ring_h, ring_w * 1.06)
        bar_w = min(W, max(ring_w * 1.30, W * (0.84 if form in ("v", "m") else 0.70)))
        pen.hbar(cx - bar_w / 2, cx + bar_w / 2, yb)
        if tick:
            with _Local(pen, wv=tw):
                pen.vbar(cx, yb, y1)
        # the ring keeps an open counter (at least about half its size) in every weight
        with _Local(pen, wv=min(pen.wv, ring_w * 0.29), wh=min(pen.wh, ring_h * 0.27)):
            pen.ring(cx - ring_w / 2, y0 - 5, cx + ring_w / 2, y0 + ring_h)


def _pansiot(pen, box, form):
    """ㅿ: a soft triangle."""
    x0, y0, x1, y1 = box
    k = min(1.0, (x1 - x0) * 0.17 / max(pen.wv, 1.0))  # a small triangle keeps an open counter
    with _Local(pen, wv=pen.wv * k, wh=pen.wh * k):
        r = (pen.rv + pen.rh) / 2
        ax = (x0 + x1) / 2
        pen.seg(ax, y1 - r, x0 + r, y0 + pen.rh)
        pen.seg(ax, y1 - r, x1 - r, y0 + pen.rh)
        pen.hbar(x0, x1, y0 + pen.rh)


def _yesieung(pen, box, form):
    x0, y0, x1, y1 = box
    H = y1 - y0
    th = max(H * 0.18, pen.wv)
    pen.vbar((x0 + x1) / 2, y1 - th - pen.rv, y1)
    _ieung(pen, (x0, y0, x1, y1 - th), form)


def _yeorinhieut(pen, box, form):
    _hieut(pen, box, form, tick=False)


SIMPLE = {
    "ㄱ": lambda p, b, f: _giyeok(p, b, f), "ㄴ": _nieun, "ㄷ": lambda p, b, f: _digeut(p, b, f), "ㄹ": _rieul,
    "ㅁ": _mieum, "ㅂ": _bieup, "ㅅ": _siot, "ㅇ": _ieung, "ㅈ": lambda p, b, f: _jieut(p, b, f),
    "ㅊ": lambda p, b, f: _jieut(p, b, f, tick=True), "ㅋ": lambda p, b, f: _giyeok(p, b, f, mid=True), "ㅌ": _tieut,
    "ㅍ": _pieup, "ㅎ": lambda p, b, f: _hieut(p, b, f), "ㅿ": _pansiot, "ㆁ": _yesieung, "ㆆ": _yeorinhieut,
}

# Optical size: closed and busy letters look bigger than open ones.
OPTICAL = {"ㅁ": 0.035, "ㅂ": 0.02, "ㅍ": 0.025, "ㅌ": 0.01, "ㄷ": 0.01}


def consonant(pen, ch, box, form):
    if ch in CLUSTER:
        parts, shares = CLUSTER[ch]
        x0, y0, x1, y1 = box
        W = x1 - x0
        n = len(parts)
        gap = max(0.55 * pen.wv + 14, 0.05 * W)
        avail = W - gap * (n - 1)
        narrow = min(shares) * avail
        with _Local(pen, wv=min(pen.wv, max(0.42 * narrow + 10, 0.75 * pen.wv))):
            x = x0
            for i, (c, sh) in enumerate(zip(parts, shares)):
                w = avail * sh
                f = form
                if form in ("v", "m") and i < n - 1:
                    # the first letter of a double initial: ㄱ gets a short sweep, ㅅ and ㅈ keep theirs
                    f = "n" if c == "ㄱ" else ("v" if c in "ㅅㅈ" and form == "v" else "h")
                consonant(pen, c, (x, y0, x + w, y1), f)
                x += w + gap
        return
    if ch in LIGHT:
        x0, y0, x1, y1 = box
        H = y1 - y0
        ring_h = H * 0.30
        consonant(pen, LIGHT[ch], (x0, y0 + ring_h + max(pen.wh * 0.7, H * 0.06), x1, y1), form)
        cx = (x0 + x1) / 2
        rw = ring_h * 1.1
        with _Local(pen, wv=min(pen.wv, rw * 0.25), wh=min(pen.wh, ring_h * 0.23)):
            pen.ring(cx - rw / 2, y0, cx + rw / 2, y0 + ring_h)
        return
    o = OPTICAL.get(ch, 0.0)
    if o:
        box = _shrink(box, o, o * 0.8)
    SIMPLE[ch](pen, box, form)


# ------------------------------------------------------------------------------------------- layout

L_EDGE, R_EDGE = 70.0, 930.0


def layout(hs, vc, fin):
    """Geometry of one layout group: stroke widths, the initial box, the vowel strokes, the final box.
    Vertical positions are built bottom-up from the stroke widths, so heavy weights keep their counters."""
    kind = KIND[vc]
    wv, wh = hs.widths(kind, fin, vc)
    rv, rh = wv / 2, wh / 2
    gx = 0.62 * wv + 28          # gap between the initial and the vowel
    gy = 0.66 * wh + 22          # gap between stacked parts
    gs = 0.50 * wh + 18          # gap between a vowel stem tip and the initial above it
    if hs.plush:                 # rounder corners and puffier strokes take a little more room
        gy, gs = gy + 12, gs + 10
    T = 790.0 if fin else 772.0
    B = -70.0 if fin else -42.0
    F = T - B
    g = dict(kind=kind, wv=wv, wh=wh, T=T, B=B, fin=fin, gx=gx, gy=gy)
    tick = 112 + 0.35 * wv       # visible length of a vowel tick
    hv = hs.heavy
    ee_k = 0.86 - 0.16 * min(1.0, hv * 1.75)   # the left tick of ㅔ: shorter in heavy weights
    gap_ae = 0.60 * wv + 55      # between the stems of ㅐ (the tick joins them)
    gap_ee = 0.55 * wv + 45 - 15 * min(1.0, hv * 1.75)   # between the stems of ㅔ
    if fin:
        fh = {"V": 300.0, "H": 292.0, "M": 292.0}[kind] + 18 * min(1.0, hs.heavy * 1.75)
        g["fbox"] = (L_EDGE + 52, B, R_EDGE - 62, B + fh)
        low = B + fh + gy        # lowest ink of the upper parts
    else:
        low = B
    U = T - low
    g["low"] = low

    if kind == "V":
        ib0 = low + (0.15 * F if not fin else 0.04 * U)
        if vc == "A":
            xs = [R_EDGE - tick - rv]
            right_ink_left = xs[0] - rv
        elif vc == "I":
            xs = [R_EDGE - 92 - rv]
            right_ink_left = xs[0] - rv
        elif vc == "E":
            xs = [R_EDGE - 62 - rv]
            right_ink_left = xs[0] - rv - tick
        elif vc == "AE":
            xs2 = R_EDGE - 30 - rv
            xs1 = xs2 - wv - gap_ae
            xs = [xs1, xs2]
            right_ink_left = xs1 - rv
        else:  # EE
            xs2 = R_EDGE - 14 - rv
            xs1 = xs2 - wv - gap_ee
            xs = [xs1, xs2]
            right_ink_left = xs1 - rv - tick * ee_k
        g["xs"] = xs
        g["ee_tick"] = tick * ee_k
        li = L_EDGE + (34 if vc == "I" else 6 if vc in ("E", "A") else -8 if vc in ("AE", "EE") else 0)
        g["ibox"] = (li, ib0, right_ink_left - gx, T)
        g["form"] = "v"
        g["yc"] = (ib0 + T) / 2 - 0.02 * (T - ib0)
        g["tick"] = tick
        return g

    if kind == "H":
        if vc == "O":
            ybar = low + (0.07 * F if not fin else 0.0) + rh
            stem = (0.12 * F + 0.20 * wv) if not fin else max(0.095 * F + 0.12 * wv, 1.40 * wv)
            g["stemtop"] = ybar + rh + stem
            ib0 = g["stemtop"] + gs
        elif vc == "U":
            if fin:
                ih = min(0.60 * U, U - (max(0.10 * F, 1.1 * wv) + wh + gy))   # the stem under ㅜ stays clearly visible
            else:
                ih = (0.43 + 0.03 * hv) * F
            ib0 = T - ih
            ybar = ib0 - gy - rh
        else:  # EU
            ih = (0.52 + 0.03 * hv) * F if not fin else min(0.64 * U, U - (0.07 * F + wh + gy))
            ib0 = T - ih
            ybar = ib0 - gy - rh
        g["ybar"] = ybar
        g["bar"] = (L_EDGE, R_EDGE)
        g["cx"] = 500.0
        inset = 78 if fin else 96
        g["ibox"] = (L_EDGE + inset, ib0, R_EDGE - inset, T)
        g["form"] = "h"
        return g

    # mixed vowels
    if vc == "OA":
        xs = [R_EDGE - tick - rv]
    elif vc == "OAE":
        xs2 = R_EDGE - 30 - rv
        xs = [xs2 - wv - gap_ae, xs2]
    elif vc == "UE":
        xs = [R_EDGE - 52 - rv]
    elif vc == "UEE":
        xs2 = R_EDGE - 14 - rv
        xs = [xs2 - wv - gap_ee, xs2]
    else:  # EUI
        xs = [R_EDGE - 70 - rv]
    g["xs"] = xs
    bar_r = xs[0] - rv - 0.55 * gx
    if vc == "UEE":
        bar_r = xs[0] - rv - tick * 0.80 - 0.45 * gx
    g["ee_tick"] = tick * 0.80
    bar_l = L_EDGE + 4
    if vc in ("OA", "OAE"):
        ybar = low + (0.22 * F if not fin else 0.0) + rh
        stem = (0.10 * F + 0.20 * wv) if not fin else max(0.08 * F + 0.10 * wv, 1.30 * wv)
        g["stemtop"] = ybar + rh + stem
        ib0 = g["stemtop"] + gs
    elif vc in ("UE", "UEE"):
        if fin:
            ih = min(0.60 * U, U - (max(0.10 * F, 1.1 * wv) + wh + gy))   # the stem under ㅜ stays clearly visible
        else:
            ih = (0.45 + 0.03 * hv) * F
        ib0 = T - ih
        ybar = ib0 - gy - rh
    else:  # EUI
        ih = (0.52 + 0.03 * hv) * F if not fin else min(0.64 * U, U - (0.07 * F + wh + gy))
        ib0 = T - ih
        ybar = ib0 - gy - rh
    g["ybar"] = ybar
    g["bar"] = (bar_l, bar_r)
    g["cx"] = (bar_l + bar_r) / 2 + (0.03 * (bar_r - bar_l) if vc in ("UE", "UEE") else 0)
    ir = bar_r - 0.04 * (bar_r - bar_l)
    g["ibox"] = (L_EDGE + 10, ib0, ir, T)
    g["form"] = "m"
    g["yc"] = (ib0 + ybar) / 2 + 0.10 * (T - ib0)
    # The right stems of ㅝ ㅞ ㅟ reach down a little closer to the final, so the tick of ㅝ ㅞ, under the bar,
    # joins its stem well above the end (a T, never an L).
    g["rlow"] = low - (0.25 * gy if fin and vc == "UE" else 0.0)
    ylow = max((ybar - rh + g["rlow"]) / 2, g["rlow"] + 1.25 * wh + 12, g["rlow"] + 1.0 * wv + 0.02 * (T - low))
    # ㅝ: the tick passes under the bar, it stays clear of it; ㅞ: the bar stops before the tick, any height works
    g["ylow"] = min(ylow, ybar) if vc in ("UE", "UEE") else ylow
    g["bar_wo"] = xs[0] - rv - tick * 0.95 - 0.5 * gx   # the bar of ㅝ ends before its tick
    g["tick"] = tick
    return g


def medial(pen, m, g):
    """Draw medial vowel m with the geometry of its group."""
    vc = VCLASS[m]
    kind = g["kind"]
    T, low = g["T"], g["low"]
    rv, rh = pen.rv, pen.rh
    tick = g.get("tick", 120)

    def ticks(x_from, x_to, y, n):
        if n == 1:
            pen.hbar(min(x_from, x_to), max(x_from, x_to), y)
            return
        # two ticks (ㅑ ㅕ ㅒ ㅖ): a little lighter when the stem is short, so the stem goes on past both
        with _Local(pen, wh=min(pen.wh, 0.16 * (T - low))):
            th = pen.rh
            d = max(0.115 * (T - low), th + max(0.70 * pen.wh, 34))
            d = min(d, max(0.20 * (T - low), th + 0.6 * pen.wh))
            if kind == "M":
                d = max(0.085 * (T - low), th + max(0.62 * pen.wh, 30))
            for yy in (y + d, y - d):
                pen.hbar(min(x_from, x_to), max(x_from, x_to), yy)

    if kind == "V":
        xs = g["xs"]
        top, bot = T, low
        double_y = m in "ㅑㅒㅕㅖ"
        n = 2 if double_y else 1
        yc = g["yc"]
        if vc == "A":
            pen.vbar(xs[0], bot, top)
            ticks(xs[0], xs[0] + rv + tick, yc, n)
        elif vc == "I":
            pen.vbar(xs[0], bot, top)
        elif vc == "E":
            pen.vbar(xs[0], bot, top)
            ticks(xs[0] - rv - tick, xs[0], yc, n)
        elif vc == "AE":
            h = top - bot
            pen.vbar(xs[0], bot + 0.06 * h, top - 0.035 * h)
            pen.vbar(xs[1], bot, top)
            ticks(xs[0], xs[1], yc, n)
        else:  # EE
            h = top - bot
            pen.vbar(xs[0], bot + 0.06 * h, top - 0.035 * h)
            pen.vbar(xs[1], bot, top)
            ticks(xs[0] - rv - g["ee_tick"], xs[0], yc, n)
        return

    if kind == "H":
        bl, br = g["bar"]
        y = g["ybar"]
        cx = g["cx"]
        pen.hbar(bl, br, y)
        two = m in "ㅛㅠ"
        sx = [cx - 0.165 * (br - bl), cx + 0.165 * (br - bl)] if two else [cx + (0.02 * (br - bl) if m == "ㅜ" else 0)]
        if vc == "O":
            for x in sx:
                pen.vbar(x, y, g["stemtop"])
        elif vc == "U":
            for x in sx:
                pen.vbar(x, low, y)
        return

    # mixed
    xs = g["xs"]
    bl, br = g["bar"]
    y = g["ybar"]
    cx = g["cx"]
    pen.hbar(bl, g["bar_wo"] if m == "ㅝ" and "bar_wo" in g else br, y)
    top, bot = T, low
    h = top - bot
    if vc in ("OA", "OAE"):
        pen.vbar(cx, y, g["stemtop"])
    elif vc in ("UE", "UEE"):
        pen.vbar(cx, bot, y)
    rbot = g.get("rlow", bot)
    if len(xs) == 1:
        pen.vbar(xs[0], rbot, top)
    else:
        # the inner stem of ㅙ is a little shorter; the one of ㅞ carries the low tick, so it goes down further
        pen.vbar(xs[0], rbot + (0.01 if vc == "UEE" else 0.05) * h, top - 0.03 * h)
        pen.vbar(xs[1], rbot, top)
    if m == "ㅘ":
        ticks(xs[0], xs[0] + rv + tick, g["yc"], 1)
    elif m == "ㅙ":
        ticks(xs[0], xs[1], g["yc"], 1)
    elif m == "ㅝ":
        ticks(xs[0] - rv - tick * 0.95, xs[0], g["ylow"], 1)
    elif m == "ㅞ":
        ticks(xs[0] - rv - g["ee_tick"], xs[0], g["ylow"], 1)


# ------------------------------------------------------------------------------------------- parts


class Hangul:
    """All part glyphs of one style and the composition of every syllable."""

    def __init__(self, style):
        self.style = style
        self.hs = HStyle(style)
        self._layouts = {}
        self._parts = None

    def lay(self, vc, fin):
        key = (vc, fin)
        if key not in self._layouts:
            self._layouts[key] = layout(self.hs, vc, fin)
        return self._layouts[key]

    # part names
    @staticmethod
    def name_initial(c, vc, fin):
        return f"hgl.L.{CNAME[c]}.{vc.lower()}{'f' if fin else ''}"

    @staticmethod
    def name_medial(m, fin):
        return f"hgl.V.{VNAME[m]}{'.f' if fin else ''}"

    @staticmethod
    def name_final(c, kind):
        return f"hgl.T.{CNAME[c]}.{kind.lower()}"

    def initial_shapes(self, c, vc, fin):
        g = self.lay(vc, fin)
        pen = Pen(self.hs, g["wv"], g["wh"])
        box = g["ibox"]
        if c in CLUSTER and g["form"] in ("v", "m"):
            # double letters take a little of the gaps: two letters in one slot need the room; in the narrow
            # slots (two-stem vowels) and in heavy weights they are also drawn a little lighter
            box = (box[0] - 14, box[1], box[2] + 0.22 * g["gx"], box[3])
            k = 0.80 if vc in ("AE", "EE", "OAE", "UEE") else 1.0 - 0.10 * min(1.0, self.hs.heavy * 1.75)
            pen = Pen(self.hs, g["wv"] * k, g["wh"] * max(k, 0.9))
        consonant(pen, c, box, g["form"])
        return pen.shapes

    def medial_shapes(self, m, fin):
        g = self.lay(VCLASS[m], fin)
        pen = Pen(self.hs, g["wv"], g["wh"])
        medial(pen, m, g)
        return pen.shapes

    def final_shapes(self, c, kind):
        vc = {"V": "A", "H": "O", "M": "OA"}[kind]
        g = self.lay(vc, True)
        pen = Pen(self.hs, g["wv"], g["wh"])
        consonant(pen, c, g["fbox"], "f")
        return pen.shapes

    def parts(self):
        """{part name: shapes}, in a stable order."""
        if self._parts is None:
            out = {}
            for fin in (False, True):
                for vc in VCLASSES:
                    for c in INITIALS:
                        out[self.name_initial(c, vc, fin)] = self.initial_shapes(c, vc, fin)
            for fin in (False, True):
                for m in MEDIALS:
                    out[self.name_medial(m, fin)] = self.medial_shapes(m, fin)
            for kind in ("V", "H", "M"):
                for c in FINALS:
                    out[self.name_final(c, kind)] = self.final_shapes(c, kind)
            self._parts = out
        return self._parts

    def composite(self, ch):
        """Part names of a syllable (offsets are 0: parts are drawn in place)."""
        l, m, t = decompose(ch)
        vc = VCLASS[m]
        fin = bool(t)
        names = [self.name_initial(l, vc, fin), self.name_medial(m, fin)]
        if fin:
            names.append(self.name_final(t, KIND[vc]))
        return names

    def jamo_shapes(self, ch):
        """Standalone compatibility jamo: a letter in the middle of the em, a little smaller than a syllable."""
        hs = self.hs
        wv, wh = hs.widths("C", False)
        pen = Pen(hs, wv, wh)
        if ch == "ㅤ":  # Hangul filler: blank
            return []
        if ch in VCLASS:
            medial(pen, ch, _compat_vowel_geometry(ch, wv, wh))
        elif ch in OLD_VOWELS:
            below, right = OLD_VOWELS[ch]
            g = _compat_vowel_geometry("ㅚ" if below == "ㅛ" else "ㅟ", wv, wh)
            _old_vowel(pen, below, right, g)
        elif ch == "ㆍ":
            pen.dot(500, 330, max(wv * 0.92, 62))
        elif ch == "ㆎ":
            pen.dot(380, 330, max(wv * 0.92, 62))
            pen.vbar(620, 20, 700)
        else:
            box = (200.0, 30.0, 800.0, 660.0)
            if ch in CLUSTER and len(CLUSTER[ch][0]) == 3:
                box = (100.0, 90.0, 900.0, 610.0)
            elif ch in CLUSTER:
                box = (130.0, 60.0, 870.0, 640.0)
            elif ch in LIGHT:
                box = (230.0, 0.0, 770.0, 700.0)
            consonant(pen, ch, box, "c")
        return _centered(pen.shapes)


def _shift_shapes(shapes, dx):
    out = []
    for sh in shapes:
        if isinstance(sh, Dot):
            out.append(Dot(sh.x + dx, sh.y, sh.r))
        else:
            p = Path(0, 0)
            p.points = [(x + dx, y) for x, y in sh.path.points]
            p.closed = sh.path.closed
            out.append(Stroke(p, sh.width, sh.width_fn))
    return out


def _centered(shapes):
    if not shapes:
        return shapes
    x0, _, x1, _ = E.bounds(E.glyph_contours(shapes))
    return _shift_shapes(shapes, round(ADVANCE / 2 - (x0 + x1) / 2))


def _compat_vowel_geometry(ch, wv, wh):
    """Vowel geometry for a standalone vowel (centered afterwards)."""
    vc = VCLASS[ch]
    kind = KIND[vc]
    T, B = 700.0, 20.0
    tick = 112 + 0.35 * wv
    gx = 0.62 * wv + 28
    gap_ae, gap_ee = min(0.60 * wv + 55, 112.0), min(0.55 * wv + 45, 105.0)
    g = dict(kind=kind, wv=wv, wh=wh, T=T, B=B, fin=False, low=B, tick=tick, ee_tick=tick * 0.86)
    if kind == "V":
        g["xs"] = {"A": [440.0], "I": [440.0], "E": [560.0], "AE": [400.0, 400.0 + wv + gap_ae],
                   "EE": [470.0, 470.0 + wv + gap_ee]}[vc]
        g["yc"] = 360.0
        return g
    if kind == "H":
        g["bar"] = (190.0, 810.0)
        g["cx"] = 500.0
        g["ybar"] = {"O": 70.0 + wh / 2, "U": 470.0, "EU": 330.0}[vc]
        g["stemtop"] = 430.0
        if vc == "U":
            g["low"] = 40.0
        return g
    xs = {"OA": [770.0], "OAE": [700.0, 700.0 + wv + gap_ae], "UE": [790.0], "UEE": [720.0, 720.0 + wv + gap_ee],
          "EUI": [770.0]}[vc]
    g["xs"] = xs
    g["ee_tick"] = tick * 0.80
    br = xs[0] - wv / 2 - 0.55 * gx
    if vc == "UEE":
        br = xs[0] - wv / 2 - g["ee_tick"] - 0.45 * gx
    bl = 60.0 if vc in ("OAE", "UEE") else 100.0
    g["bar"] = (bl, br)
    g["cx"] = (bl + br) / 2
    g["ybar"] = {"OA": 150.0, "OAE": 150.0, "UE": 440.0, "UEE": 440.0, "EUI": 300.0}[vc]
    g["stemtop"] = 400.0
    g["yc"] = 380.0
    g["ylow"] = 230.0
    return g


def _old_vowel(pen, below, right, g):
    """ㆇ ㆈ ㆉ ㆊ ㆋ ㆌ: ㅛ or ㅠ joined to a right vowel."""
    rv = pen.rv
    T, low = g["T"], g["low"]
    bl, br = g["bar"]
    y = g["ybar"]
    pen.hbar(bl, br, y)
    for x in (bl + 0.32 * (br - bl), bl + 0.68 * (br - bl)):
        if below == "ㅛ":
            pen.vbar(x, y, g["stemtop"])
        else:
            pen.vbar(x, low, y)
    xs = g["xs"][0]
    tick = g["tick"]
    pen.vbar(xs, low, T)
    if right == "ㅣ":
        return
    d = max(56.0, pen.rh + 0.7 * pen.wh)
    if right in "ㅑㅒ":
        x2 = xs + pen.wv + tick if right == "ㅒ" else None
        for yy in (g["yc"] + d, g["yc"] - d):
            pen.hbar(xs, x2 if x2 else xs + rv + tick, yy)
        if x2:
            pen.vbar(x2, low, T)
        return
    x2 = xs + pen.wv + 92 if right == "ㅖ" else None   # ㅕ ㅖ
    clear = bl + 0.68 * (br - bl) + rv + max(0.7 * pen.wv, 40)  # right of the ㅠ stem
    for yy in (g["ylow"] + d, g["ylow"] - d):
        pen.hbar(max(xs - rv - tick * 0.9, clear), xs, yy)
    if x2:
        pen.vbar(x2, low, T)


# ------------------------------------------------------------------------------------------- API

_CACHE = {}


def _h(style):
    key = (style.key, style.weight_name, style.w, style.inflate)
    if key not in _CACHE:
        _CACHE[key] = Hangul(style)
    return _CACHE[key]


def parts(style):
    """{part glyph name: shapes} for the unencoded jamo parts, positioned in the em (x 0..1000)."""
    return _h(style).parts()


def part_order(style):
    return list(_h(style).parts().keys())


def composite(ch, style):
    """Part names whose union is the glyph of ch (syllables), or None (drawn directly: jamo)."""
    if SBASE <= ord(ch) < SBASE + 11172:
        return _h(style).composite(ch)
    return None


def advance(ch, style):
    return ADVANCE


# pw_font_build.SB["P"]: build_style's side bearing for class "P" (repeated here so this module never imports
# pw_font_build, which will import it).
_SB_P = 46.0


def _side_bearing(style):
    return _SB_P * (1.0 + (style.w - 88.0) * 0.0018) + style.tracking


def make(ch, style):
    """The Glyphs.make tuple (shapes, left class, right class, uppercase, (x0, x1)). Shapes are already in place
    in a 1000 wide em; (x0, x1) is chosen so that build_style's spacing (lsb + (x1 - x0) + rsb with class "P")
    gives exactly ADVANCE and moves nothing. Syllables are returned as the union of their parts (full outlines);
    composite() and parts() give the same glyph as references, which keeps the font small."""
    h = _h(style)
    if SBASE <= ord(ch) < SBASE + 11172:
        p = h.parts()
        shapes = [s for name in h.composite(ch) for s in p[name]]
    else:
        shapes = h.jamo_shapes(ch)
    sb = _side_bearing(style)
    return shapes, "P", "P", False, (sb, ADVANCE - sb)


def features(style=None):
    """No OpenType feature is needed: the syllables are precomposed (no conjoining jamo shaping)."""
    return ""


def jamo_shapes(ch, style):
    return _h(style).jamo_shapes(ch)
