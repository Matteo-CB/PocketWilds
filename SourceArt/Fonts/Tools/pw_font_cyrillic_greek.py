"""Cyrillic and Greek for the Pocket Wilds fonts (Pocket Round, Pocket Plush), drawn as stroke skeletons.

Same pen, proportions and spacing classes as the Latin of pw_font_glyphs: every letter is built from the Glyphs
helpers (stems with round caps, superelliptic bowls, splines), so each weight and the puffy Plush follow the Style.
Latin shapes are reused only where the letters really are the same (А В Е К М Н О Р С Т Х, Α Β Ε Ζ Η Ι Κ Μ Ν Ο Ρ Τ Υ Χ,
а е о р с у х, ο); the others are true Cyrillic and Greek forms.

Script hook API (pw_font_build merges every module the same way):
    SCRIPT = "cyrgrk"
    charset()                    -> characters covered, in order
    make(ch, style)              -> (shapes, left_class, right_class, upper, (x0, x1)), exactly like Glyphs.make
    features(style)              -> "" (kerning only; see LANGUAGESYSTEMS)
    kerning(cmap, data, style)   -> {(left_glyph, right_glyph): value} for the new letters, measured like
                                    pw_font_build.kerning (ink profiles, same formula and limits)
"""
import dataclasses
import math

import numpy as np
from PIL import Image, ImageDraw

from pw_font_engine import Dot, Path, Poly, Stroke, glyph_contours
from pw_font_glyphs import Glyphs, _map_points, _mirror, _se, _shift, ink_gap

SCRIPT = "cyrgrk"
LANGUAGESYSTEMS = [("cyrl", "dflt"), ("grek", "dflt")]


# ------------------------------------------------------------------------------------------- shared helpers

def clearance(a, b):
    """Smallest ink gap between two shape lists (negative when they overlap): pw_font_glyphs.ink_gap, the measure the
    Latin marks are seated with, so every script keeps the same distances."""
    return ink_gap(a, b)


def move(shapes, dx, dy=0.0):
    return [_shift(sh, dx, dy) for sh in shapes]


def scale_about(shapes, ax, ay, sx, sy, wm=1.0):
    """Geometry scaled about (ax, ay); stroke widths kept, or multiplied by wm (used to fit stacked marks)."""
    return [_map_points(sh, lambda x, y: (ax + (x - ax) * sx, ay + (y - ay) * sy), wm) for sh in shapes]


def row_extents(shapes, scale=0.5, band=8):
    """Leftmost and rightmost ink per horizontal band: {y: (x_left, x_right)} in font units."""
    contours = glyph_contours(shapes)
    allp = np.vstack(contours)
    x0, y0 = allp[:, 0].min() - 4, allp[:, 1].min() - 4
    x1, y1 = allp[:, 0].max() + 4, allp[:, 1].max() + 4
    w = int(math.ceil((x1 - x0) * scale)) + 2
    h = int(math.ceil((y1 - y0) * scale)) + 2
    acc = np.zeros((h, w), dtype=np.int32)
    for c in contours:
        img = Image.new("L", (w, h), 0)
        pts = [((x - x0) * scale, (y1 - y) * scale) for x, y in c]
        if len(pts) >= 3:
            ImageDraw.Draw(img).polygon(pts, fill=1)
        area = 0.5 * float(np.sum(c[:, 0] * np.roll(c[:, 1], -1) - np.roll(c[:, 0], -1) * c[:, 1]))
        acc += np.array(img, dtype=np.int32) * (1 if area < 0 else -1)
    mask = acc != 0
    out = {}
    step = max(1, int(band * scale))
    for r0 in range(0, h, step):
        cols = np.where(mask[r0:r0 + step].any(axis=0))[0]
        if len(cols):
            y = y1 - (r0 + step / 2) / scale
            out[int(round(y))] = (cols[0] / scale + x0, (cols[-1] + 1) / scale + x0)
    return out


def tuck_offset(left_shapes, right_shapes, gap, band=8):
    """dx to move right_shapes so that, row by row, they keep at least `gap` from left_shapes."""
    L = row_extents(left_shapes, band=band)
    R = row_extents(right_shapes, band=band)
    need = None
    for y, (_, lr) in L.items():
        for yy in (y - band, y, y + band):
            if yy in R:
                v = lr + gap - R[yy][0]
                need = v if need is None else max(need, v)
    return need if need is not None else 0.0


def style_key(style):
    return tuple(dataclasses.astuple(style))


def measure_pairs(left, right, variants, cmap, data, existing=None):
    """Kerning measured exactly like pw_font_build.kerning: average gap over shared ink bands against the H-H gap,
    55 % of the difference, never closer than 30 % of the reference, kept from -14 units. `variants` maps a right
    base letter to its accented forms, measured on their own glyphs."""
    from pw_font_build import profiles
    prof = {}
    need = set(left) | set(right) | {"H"} | {c for v in variants.values() for c in v}
    for ch in need:
        name = cmap.get(ord(ch))
        if name:
            contours, adv, _ = data[name]
            prof[ch] = (profiles(contours, adv), adv)

    def avg_gap(a, b):
        pa, adv_a = prof[a]
        pb, _ = prof[b]
        gaps = [adv_a - r + pb[y][0] for y, (l, r) in pa.items() if y in pb]
        if not gaps:
            return None, None
        return sum(gaps) / len(gaps), min(gaps)

    ref, _ = avg_gap("H", "H")

    def value(a, b):
        if a not in prof or b not in prof:
            return None
        avg, mn = avg_gap(a, b)
        if avg is None:
            return None
        k = 0.55 * (ref - avg)
        limit = -(mn - 0.30 * ref)
        k = min(max(k, limit), 0.0)
        return int(round(k / 2) * 2) if k <= -14 else None

    pairs = {}
    for a in left:
        for b in right:
            if a not in prof:
                continue
            for v in [b] + variants.get(b, []):
                key = (cmap[ord(a)], cmap.get(ord(v)))
                if key[1] is None or (existing and key in existing):
                    continue
                k = value(a, v)
                if k is not None:
                    pairs[key] = k
    return pairs


# ------------------------------------------------------------------------------------------- letterforms

class CyrGrk(Glyphs):
    def __init__(self, s):
        super().__init__(s)
        # Horizontal-heavy letters at x-height (в з ε) thin their curves a little in heavy weights so counters stay open.
        self.tight = 1.0 - max(0.0, s.w - 92.0) * 0.0025
        self.ddesc = -140.0  # short descenders of Д Ц Щ Џ д ц щ џ (ink bottom)
        # Greek ascenders: the Plush x-height is tall, so β δ ζ θ λ ξ ψ get room for their inner shapes.
        self.gasc = max(s.asc, s.xh + 200.0)

    # ---------------- helpers ----------------

    def hbar(self, x0, x1, y, mult=1.0):
        """Horizontal stroke whose round caps reach x0 and x1."""
        hh = self.h * mult
        return self.line(x0 + hh, y, x1 - hh, y, mult)

    def lobe(self, xs, xr, yt, yb=None, mult=1.0):
        """Bowl hanging right of a stem at xs: centerline from (xs, yt) to the right edge xr (outer) and back to yb."""
        s = self.s
        hh = self.h * mult
        if yb is None:
            yb = hh
        r = (yt - yb) / 2
        rx = min(r * 1.08, (xr - hh - xs) * 0.92)
        xc = xr - hh - rx
        p = Path(xs, yt).line(xc, yt).arc(xc, (yt + yb) / 2, rx, r, 90, -90, s.n).line(xs, yb)
        return self.st(p, mult)

    def H(self, lower):
        return self.s.xh if lower else self.s.cap

    def W(self, lower, k_lower, k_upper):
        return self.nw * k_lower if lower else self.Hw * k_upper

    # ---------------- Cyrillic, both cases ----------------

    def cy_Be(self):
        s, h = self.s, self.h
        W = self.Hw * 0.88
        return [self.stem(h, 0, s.cap), self.hbar(0, W * 0.96, s.cap - h), self.lobe(h, W, s.cap * 0.56)], "S", "R"

    def cy_be(self):
        s, h = self.s, self.h
        bw = self.ow * 0.98
        X = s.xh
        k = [(h, X * 0.50, 90), (h + bw * 0.08, s.asc * 0.80, 72), (bw * 0.52, s.asc - h, 4), (bw - h - 4, s.asc - h - 18, 340)]
        return [self.bowl(0, bw, -s.ov, X + s.ov), self.spline(k)], "R", "R"

    def cy_ve(self):
        s, h = self.s, self.h
        W = self.nw * 0.90
        X = s.xh
        m = self.tight
        hh = h * m
        ym = X * 0.54
        r1 = (X - hh - ym) / 2
        r2 = (ym - hh) / 2
        x1 = W * 0.93 - hh - r1
        x2 = W - hh - r2
        top = Path(h, X - hh).line(x1, X - hh).arc(x1, ym + r1, r1, r1, 90, -90, s.n).line(h, ym)
        bot = Path(h, ym).line(x2, ym).arc(x2, hh + r2, r2, r2, 90, -90, s.n).line(h, hh)
        return [self.stem(h, 0, X), self.st(top, m), self.st(bot, m)], "S", "R"

    def _ge(self, lower):
        h = self.h
        H = self.H(lower)
        W = self.W(lower, 0.72, 0.76)
        return [self.stem(h, 0, H), self.hbar(0, W, H - h)], W

    def cy_Ge(self):
        return self._ge(False)[0], "S", "O"

    def cy_ge(self):
        return self._ge(True)[0], "S", "O"

    def cy_Gheup(self):
        shapes, W = self._ge(False)
        h = self.h
        return shapes + [self.line(W - h, self.s.cap - h, W - h, self.s.cap + 120)], "S", "O"

    def cy_gheup(self):
        shapes, W = self._ge(True)
        h = self.h
        return shapes + [self.line(W - h, self.s.xh - h, W - h, self.s.xh + 105)], "S", "O"

    def _de(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        W = self.W(lower, 1.10, 1.04)
        xr = W - h
        xl = W * 0.31
        xb = W * 0.15
        yd = self.ddesc - (10 if lower else 0)
        leg = self.spline([(xl, H - h, 262), (xb, h + 2, 250)], 0.30)
        return [self.stem(xr, yd, H), self.hbar(xl - h, xr + h, H - h), leg, self.hbar(0, xr + h, h),
                self.line(h, h, h, yd + h)], "O", "S"

    def cy_De(self):
        return self._de(False)

    def cy_de(self):
        return self._de(True)

    def _zhe(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        half = self.W(lower, 0.66, 0.66)
        cx = half
        a0 = (cx + half - h - 6, H - h)
        a1 = (cx + 4, H * 0.42)
        t = 0.48
        j = (a0[0] * (1 - t) + a1[0] * t, a0[1] * (1 - t) + a1[1] * t)
        right = [self.line(*a0, *a1), self.line(*j, cx + half - h, h)]
        left = [_mirror(sh, cx) for sh in right]
        return [self.stem(cx, 0, H)] + right + left, "D", "D"

    def cy_Zhe(self):
        return self._zhe(False)

    def cy_zhe(self):
        return self._zhe(True)

    def _ze(self, lower):
        s = self.s
        m = self.tight if lower else 1.0
        h = self.h * m
        H = self.H(lower)
        zw = self.ow * 0.84 if lower else self.Hw * 0.84
        up = [(h + 4, H * 0.79, 64), (zw * 0.48, H + s.ov - h, 0), (zw - h - 16, H * 0.765, 270), (zw * 0.40, H * 0.535, 180)]
        lo = [(zw * 0.40, H * 0.535, 0), (zw - h, H * 0.26, 270), (zw * 0.48, h - s.ov, 180), (h + 2, H * 0.19, 116)]
        return [self.st(Path(*up[0][:2]).spline(up), m), self.st(Path(*lo[0][:2]).spline(lo), m)], "O", "R"

    def cy_Ze(self):
        return self._ze(False)

    def cy_ze(self):
        return self._ze(True)

    def _i(self, lower):
        h = self.h
        H = self.H(lower)
        W = self.W(lower, 1.0, 1.02)
        return [self.stem(h, 0, H), self.stem(W - h, 0, H), self.line(h, h, W - h, H - h)], "S", "S"

    def cy_I(self):
        return self._i(False)

    def cy_i(self):
        return self._i(True)

    def cy_ka(self):
        """Cyrillic к: x-height stem, an arm that leaves the stem flat and curls up, a straight leg."""
        s, h = self.s, self.h
        X = s.xh
        kw = self.nw * 0.90
        yj = X * 0.47
        arm = self.spline([(h + 4, yj, 0), (kw * 0.55, X * 0.64, 42), (kw - h - 6, X - h, 70)], 0.36)
        leg = self.line(kw * 0.40, yj + 6, kw - h, h)
        return [self.stem(h, 0, X), arm, leg], "S", "D"

    def _el_leg(self, xl, H):
        h = self.h
        y1 = H * 0.40
        r = xl - h - 2
        return self.st(Path(xl, H - h).line(xl, y1).arc(xl - r, y1, r, y1 - h, 0, -90, 2.0))

    def _el(self, lower, W=None):
        h = self.h
        H = self.H(lower)
        W = W or self.W(lower, 0.98, 0.96)
        xr = W - h
        xl = W * 0.30 + h * 0.4
        return [self.stem(xr, 0, H), self.hbar(xl - h, xr + h, H - h), self._el_leg(xl, H)], xr

    def cy_El(self):
        return self._el(False)[0], "O", "S"

    def cy_el(self):
        return self._el(True)[0], "O", "S"

    def cy_em(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 1.30
        v = (W / 2, X * 0.22)
        return [self.stem(h, 0, X), self.stem(W - h, 0, X), self.line(h, X - h, *v), self.line(*v, W - h, X - h)], "S", "S"

    def cy_en(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.96
        return [self.stem(h, 0, X), self.stem(W - h, 0, X), self.hbar(h, W - h, X * 0.5, s.bar)], "S", "S"

    def _pe(self, lower):
        h = self.h
        H = self.H(lower)
        W = self.W(lower, 0.96, 0.98)
        return [self.stem(h, 0, H), self.stem(W - h, 0, H), self.hbar(0, W, H - h)], "S", "S"

    def cy_Pe(self):
        return self._pe(False)

    def cy_pe(self):
        return self._pe(True)

    def cy_te(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.94
        return [self.hbar(0, W, X - h), self.stem(W / 2, 0, X)], "O", "O"

    def cy_U(self):
        """Cyrillic У: a straight right stroke to the baseline with a soft turn, the left arm meets it high."""
        s, h = self.s, self.h
        C = s.cap
        W = self.Hw * 1.0
        tr = (W - h, C - h)
        bl = (W * 0.22, h)
        k = [(tr[0], tr[1], 247), (W * 0.36, C * 0.16, 240), (bl[0], bl[1], 200)]
        diag = self.spline(k, 0.30)
        ym = C * 0.36
        t = (tr[1] - ym) / (tr[1] - C * 0.16)
        meet = (tr[0] + (W * 0.36 - tr[0]) * t, ym)
        return [self.line(h, C - h, *meet), diag], "D", "D"

    def _ef(self, lower):
        s, h = self.s, self.h
        if lower:
            W = self.ow * 1.38
            return [self.bowl(0, W, -s.ov, s.xh + s.ov), self.stem(W / 2, s.desc, s.asc)], "R", "R"
        W = self.Ow * 1.04
        return [self.bowl(0, W, s.cap * 0.10, s.cap * 0.90), self.stem(W / 2, 0, s.cap)], "R", "R"

    def cy_Ef(self):
        return self._ef(False)

    def cy_ef(self):
        return self._ef(True)

    def _tse(self, lower, W=None):
        s, h = self.s, self.h
        H = self.H(lower)
        W = W or self.W(lower, 0.96, 0.98)
        xr = W - h
        xe = xr + s.w * 0.80 + 30
        yd = self.ddesc - (10 if lower else 0)
        return [self.stem(h, 0, H), self.stem(xr, 0, H), self.hbar(0, xe + h, h), self.line(xe, h, xe, yd + h)], xr, xe

    def cy_Tse(self):
        return self._tse(False)[0], "S", "O"

    def cy_tse(self):
        return self._tse(True)[0], "S", "O"

    def _che(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        W = self.W(lower, 0.90, 0.90)
        xr = W - h
        yb = H * 0.40 + (8 if lower else 0)
        r = (xr - h) * 0.56
        cy = yb + min(r * 0.95, (H - h - yb) * 0.55)
        p = Path(h, H - h).line(h, cy).arc(h + r, cy, r, cy - yb, 180, 270, s.n).line(xr, yb)
        return [self.st(p), self.stem(xr, 0, H)], "S", "S"

    def cy_Che(self):
        return self._che(False)

    def cy_che(self):
        return self._che(True)

    def _sha(self, lower, extra=False):
        s, h = self.s, self.h
        H = self.H(lower)
        W = self.W(lower, 1.44, 1.38)
        shapes = [self.stem(h, 0, H), self.stem(W / 2, 0, H), self.stem(W - h, 0, H)]
        if not extra:
            return shapes + [self.hbar(0, W, h)], "S", "S"
        xe = W - h + s.w * 0.80 + 30
        yd = self.ddesc - (10 if lower else 0)
        return shapes + [self.hbar(0, xe + h, h), self.line(xe, h, xe, yd + h)], "S", "O"

    def cy_Sha(self):
        return self._sha(False)

    def cy_sha(self):
        return self._sha(True)

    def cy_Shcha(self):
        return self._sha(False, True)

    def cy_shcha(self):
        return self._sha(True, True)

    def _soft(self, lower, W=None, xs=None):
        """Ь: a stem and a bowl a little over half the height."""
        h = self.h
        H = self.H(lower)
        W = W or self.W(lower, 0.84, 0.84)
        xs = h if xs is None else xs
        m = self.tight if lower else 1.0
        return [self.stem(xs, 0, H), self.lobe(xs, W, self.lobe_top(lower), mult=m)]

    def lobe_top(self, lower):
        """Centerline of the top of the Ь bowl (outer edge at 62 % of x-height, 60 % of cap height)."""
        m = self.tight if lower else 1.0
        return self.s.xh * 0.62 - self.h * m if lower else self.s.cap * 0.60 - self.h

    def cy_Soft(self):
        return self._soft(False), "S", "R"

    def cy_soft(self):
        return self._soft(True), "S", "R"

    def _hard(self, lower):
        h = self.h
        H = self.H(lower)
        xs = self.W(lower, 0.30, 0.26)
        W = xs + self.W(lower, 0.80, 0.80)
        return [self.hbar(0, xs + h, H - h)] + self._soft(lower, W, xs), "O", "R"

    def cy_Hard(self):
        return self._hard(False)

    def cy_hard(self):
        return self._hard(True)

    def _yeru(self, lower):
        h = self.h
        H = self.H(lower)
        Wb = self.W(lower, 0.78, 0.78)
        xi = Wb + self.W(lower, 0.22, 0.20) + h
        return self._soft(lower, Wb) + [self.stem(xi, 0, H)], "S", "S"

    def cy_Yeru(self):
        return self._yeru(False)

    def cy_yeru(self):
        return self._yeru(True)

    def _e_rev(self, lower):
        """Э: a C turned round with a bar from the middle to the right side."""
        s, h = self.s, self.h
        H = self.H(lower)
        if lower:
            W = self.ow * 0.92
            a = s.aperture + max(0.0, s.w - 88) * 0.2
            arc = self.open_arc(0, W, -s.ov, H + s.ov, a, 360 - a)
        else:
            W = self.Ow * 0.92
            arc = self.open_arc(0, W, -s.ov, H + s.ov, 45, 315)
        arc = _mirror(arc, W / 2)
        return [arc, self.hbar(W * 0.34, W - h, H * 0.5, s.bar)], "O", "R"

    def cy_Ereversed(self):
        return self._e_rev(False)

    def cy_ereversed(self):
        return self._e_rev(True)

    def _ie_ukr(self, lower):
        """Є: C with a bar from the left side to the middle."""
        s, h = self.s, self.h
        H = self.H(lower)
        if lower:
            W = self.ow * 0.92
            a = s.aperture + max(0.0, s.w - 88) * 0.2
            arc = self.open_arc(0, W, -s.ov, H + s.ov, a, 360 - a)
        else:
            W = self.Ow * 0.92
            arc = self.open_arc(0, W, -s.ov, H + s.ov, 45, 315)
        return [arc, self.hbar(h, W * 0.66, H * 0.5, s.bar)], "R", "O"

    def cy_Ieukr(self):
        return self._ie_ukr(False)

    def cy_ieukr(self):
        return self._ie_ukr(True)

    def _yu(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        x0 = self.W(lower, 0.30, 0.26) + h
        ow = self.ow * 0.90 if lower else self.Ow * 0.84
        return [self.stem(h, 0, H), self.hbar(h, x0 + h, H * 0.5, s.bar), self.bowl(x0, x0 + ow, -s.ov, H + s.ov)], "S", "R"

    def cy_Yu(self):
        return self._yu(False)

    def cy_yu(self):
        return self._yu(True)

    def cy_Ya(self):
        shapes, _, _ = self.g_R()
        rw = self.Hw * 0.94
        return [_mirror(sh, rw / 2) for sh in shapes], "D", "S"

    def cy_ya(self):
        s, h = self.s, self.h
        X = s.xh
        rw = self.nw * 0.90
        ym = X * 0.42
        r = (X - h - ym) / 2
        x1 = rw * 0.96 - h - r
        bowl = Path(h, X - h).line(x1, X - h).arc(x1, ym + r, r, r, 90, -90, s.n).line(h, ym)
        leg = self.line(x1 - 6, ym, rw - h, h)
        shapes = [self.stem(h, 0, X), self.st(bowl), leg]
        return [_mirror(sh, rw / 2) for sh in shapes], "D", "S"

    def _dje_arch(self, xs, xr, join, top, end_y, hook):
        s, h = self.s, self.h
        cx, rx = (xs + xr) / 2, (xr - xs) / 2
        p = Path(*_se(cx, join, rx, top - join, 180, s.n)).arc(cx, join, rx, top - join, 180, 0, s.n)
        if not hook:
            return self.st(p.line(xr, h))
        rr = min(90.0 + (s.w - 88) * 0.6, rx * 0.85)
        p.line(xr, end_y)
        p.arc(xr - rr, end_y, rr, end_y - hook, 0, -155, 2.0)
        return self.st(p)

    def cy_Dje(self):
        s, h = self.s, self.h
        C = s.cap
        tw = self.Hw * 0.82
        xs = tw * 0.40
        xr = xs + self.Hw * 0.56
        # The hook dips below the baseline so Ђ never reads as Ћ, even in the heavy Plush.
        arch = self._dje_arch(xs, xr, C * 0.34, C * 0.60, C * 0.16, h - s.ov - C * 0.07)
        return [self.hbar(0, tw, C - h), self.stem(xs, 0, C), arch], "O", "O"

    def cy_Tshe(self):
        s, h = self.s, self.h
        C = s.cap
        tw = self.Hw * 0.82
        xs = tw * 0.40
        xr = xs + self.Hw * 0.56
        arch = self._dje_arch(xs, xr, C * 0.34, C * 0.60, 0, None)
        return [self.hbar(0, tw, C - h), self.stem(xs, 0, C), arch], "O", "S"

    def _hbar_asc(self):
        """Crossbar of ђ ћ, between the arch and the top of the stem (the stem rises to the Greek ascender, so the
        bar has room in the tall Plush x-height too)."""
        s, h = self.s, self.h
        y = self.gasc - (self.gasc - s.xh) * 0.40
        return self.line(h - 64, y, h + 176 + (s.w - 88) * 0.4, y, 0.9)

    def cy_dje(self):
        s, h = self.s, self.h
        X = s.xh
        x1 = self.nw - h
        arch = self._dje_arch(h, x1, X * 0.46, X + s.ov - h, s.desc * 0.25, s.desc - s.ov + h)
        return [self.stem(h, 0, self.gasc), arch, self._hbar_asc()], "O", "S"

    def cy_tshe(self):
        s, h = self.s, self.h
        X = s.xh
        arch = self._dje_arch(h, self.nw - h, X * 0.46, X + s.ov - h, 0, None)
        return [self.stem(h, 0, self.gasc), arch, self._hbar_asc()], "O", "S"

    def _lje(self, lower):
        H = self.H(lower)
        h = self.h
        shapes, xr = self._el(lower, self.W(lower, 0.88, 0.88))
        m = self.tight if lower else 1.0
        bw = self.W(lower, 0.62, 0.60)
        yt = self.lobe_top(lower)
        return shapes + [self.lobe(xr, xr + h + bw, yt, mult=m)], "O", "R"

    def cy_Lje(self):
        return self._lje(False)

    def cy_lje(self):
        return self._lje(True)

    def _nje(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        xr = self.W(lower, 0.80, 0.80) - h
        m = self.tight if lower else 1.0
        bw = self.W(lower, 0.62, 0.60)
        yt = self.lobe_top(lower)
        return [self.stem(h, 0, H), self.stem(xr, 0, H), self.line(h, yt, xr, yt, m),
                self.lobe(xr, xr + h + bw, yt, mult=m)], "S", "R"

    def cy_Nje(self):
        return self._nje(False)

    def cy_nje(self):
        return self._nje(True)

    def _dzhe(self, lower):
        s, h = self.s, self.h
        H = self.H(lower)
        W = self.W(lower, 0.96, 0.98)
        yd = self.ddesc - (10 if lower else 0)
        return [self.stem(h, 0, H), self.stem(W - h, 0, H), self.hbar(0, W, h), self.line(W / 2, h, W / 2, yd + h)], "S", "S"

    def cy_Dzhe(self):
        return self._dzhe(False)

    def cy_dzhe(self):
        return self._dzhe(True)

    def cy_numero(self):
        s, h = self.s, self.h
        N, _, _ = self.g_N()
        nw = self.Hw * 1.02
        sc = 0.56
        o = self.small(self.g_o()[0], sc, 0, 0, 0.80)
        ox0, oy0, ox1, oy1 = self.box(o)
        o = move(o, nw + 40 - ox0, s.cap - oy1)
        ox0, oy0, ox1, oy1 = self.box(o)
        hh = h * 0.78
        bar = self.line(ox0 + hh, oy0 - 50 - hh, ox1 - hh, oy0 - 50 - hh, 0.78)
        return N + o + [bar], "S", "P"

    # ---------------- Greek ----------------

    def gr_Delta(self):
        s, h = self.s, self.h
        W = self.Hw * 1.08
        apex = (W / 2, s.cap - h)
        return [self.line(h, h, *apex), self.line(*apex, W - h, h), self.hbar(0, W, h)], "D", "D"

    def gr_Theta(self):
        s, h = self.s, self.h
        W = self.Ow
        return [self.bowl(0, W, -s.ov, s.cap + s.ov), self.hbar(W * 0.29, W * 0.71, s.cap * 0.5, s.bar)], "R", "R"

    def gr_Lambda(self):
        s, h = self.s, self.h
        W = self.Hw * 1.06
        apex = (W / 2, s.cap - h)
        return [self.line(h, h, *apex), self.line(*apex, W - h, h)], "D", "D"

    def gr_Xi(self):
        s, h = self.s, self.h
        W = self.Hw * 0.86
        return [self.hbar(0, W, s.cap - h), self.hbar(W * 0.13, W * 0.87, s.cap * 0.5, s.bar), self.hbar(0, W, h)], "O", "O"

    def gr_Sigma(self):
        s, h = self.s, self.h
        C = s.cap
        W = self.Hw * 0.88
        mid = (W * 0.50, C * 0.5)
        return [self.hbar(0, W, C - h), self.line(h + 4, C - h, *mid), self.line(*mid, h + 4, h), self.hbar(0, W, h)], "O", "O"

    def gr_Psi(self):
        s, h = self.s, self.h
        C = s.cap
        W = self.Hw * 1.10
        cy = C * 0.64
        yb = C * 0.30
        cup = Path(h, C - h).line(h, cy).arc(W / 2, cy, W / 2 - h, cy - yb, 180, 360, s.n).line(W - h, C - h)
        return [self.st(cup), self.stem(W / 2, 0, C)], "S", "S"

    def gr_Omega(self):
        s, h = self.s, self.h
        C = s.cap
        W = self.Ow * 0.98
        fx = W * 0.30
        k = [(fx + 4, h + 2, 112), (h, C * 0.50, 90), (W / 2, C + s.ov - h, 0), (W - h, C * 0.50, 270), (W - fx - 4, h + 2, 248)]
        body = self.spline(k, 0.40)
        return [body, self.hbar(0, fx + h + 8, h), self.hbar(W - fx - h - 8, W, h)], "O", "O"

    def gr_alpha(self):
        s, h = self.s, self.h
        X = s.xh
        bw = self.ow * 0.94
        R = bw - h
        tail = self.spline([(R + 6, X - h + 4, 272), (R, X * 0.30, 270), (R + 64 + (s.w - 88) * 0.3, h - 2, 330)], 0.40)
        return [self.bowl(0, bw, -s.ov, X + s.ov), tail], "R", "O"

    def gr_beta(self):
        """β: a stem from the descender with two stacked bowls, the upper one narrower."""
        s, h = self.s, self.h
        X = s.xh
        bw = self.ow * 0.96
        A = self.gasc
        ym = X * (0.86 - max(0.0, s.w - 92) * 0.0016)
        return [self.stem(h, s.desc, A - 120), self.bowl(0, bw * 0.86, ym - h, A + s.ov),
                self.bowl(0, bw, -s.ov, ym + h)], "S", "R"

    def gr_gamma(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.98
        v = (W * 0.50, s.desc * 0.62)
        left = self.spline([(h, X - h, 300), (v[0], v[1], 290)], 0.40)
        return [left, self.line(W - h, X - h, *v)], "D", "D"

    def gr_delta(self):
        """δ: a bowl and a neck that leaves its top, leans up to the left and curls right at the top."""
        s, h = self.s, self.h
        X = s.xh
        bw = self.ow * 0.98
        top = self.gasc - h
        k = [(bw * 0.58, X + s.ov - h * 0.5, 128), (bw * 0.22, X + (top - X) * 0.66, 104), (bw * 0.46, top, 8),
             (bw - h - 2, top - 30, 335)]
        return [self.bowl(0, bw, -s.ov, X + s.ov), self.st(Path(*k[0][:2]).spline(k), 0.92)], "R", "R"
    def gr_epsilon(self):
        shapes, _, _ = self._ze(True)
        zw = self.ow * 0.84
        return [_mirror(sh, zw / 2) for sh in shapes], "R", "O"

    def gr_zeta(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.ow * 0.86
        top = self.gasc - h
        bar = self.hbar(W * 0.10, W, top)
        k = [(W - h - 10, top, 222), (h, X * 0.36, 270), (W * 0.46, h, 0), (W * 0.74, -60, 290), (W * 0.50, s.desc + h, 200)]
        return [bar, self.spline(k, 0.42)], "R", "O"

    def gr_eta(self):
        s, h = self.s, self.h
        X = s.xh
        top = X + s.ov - h
        x1 = self.nw - h
        cx, rx = (h + x1) / 2, (x1 - h) / 2
        j = X * 0.46
        p = Path(*_se(cx, j, rx, top - j, 180, s.n)).arc(cx, j, rx, top - j, 180, 0, s.n).line(x1, s.desc + h)
        return [self.stem(h, 0, X), self.st(p)], "S", "S"

    def gr_theta(self):
        s, h = self.s, self.h
        W = self.ow * 0.92
        A = self.gasc
        return [self.bowl(0, W, -s.ov, A + s.ov), self.hbar(h, W - h, A * 0.5, s.bar)], "R", "R"

    def gr_iota(self):
        s, h = self.s, self.h
        r = h + 30
        p = Path(h, s.xh - h).line(h, h + r).arc(h + r, h + r, r, r, 180, 270, 2.0).line(h + r + 26, h + 8)
        return [self.st(p)], "S", "O"

    def gr_lambda(self):
        s, h = self.s, self.h
        W = self.nw * 0.98
        top = (W * 0.10 + h, self.gasc - h)
        bot = (W - h, h)
        jy = s.xh * 0.62
        tt = (top[1] - jy) / (top[1] - bot[1])
        j = (top[0] + (bot[0] - top[0]) * tt, jy)
        return [self.line(*top, *bot), self.line(*j, h, h)], "D", "D"

    def gr_nu(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.98
        v = (W * 0.44, h)
        right = self.spline([(v[0], v[1], 30), (W - h, X - h, 88)], 0.42)
        return [self.line(h, X - h, *v), right], "D", "O"

    def gr_xi(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.ow * 0.86
        top = self.gasc - h
        mid = X * 0.72
        up = [(W * 0.66, top, 200), (h + 20, (top + mid) / 2, 270), (W * 0.60, mid, 0)]
        lo = [(W * 0.60, mid, 180), (h, mid * 0.48, 270), (W * 0.48, h, 0), (W * 0.74, -60, 290), (W * 0.50, s.desc + h, 200)]
        return [self.hbar(W * 0.12, W, top), self.spline(up, 0.42), self.spline(lo, 0.42)], "R", "O"

    def gr_pi(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 1.12
        xl, xr = W * 0.24, W * 0.74
        r = h + 26
        right = Path(xr, X - h).line(xr, h + r).arc(xr + r, h + r, r, r, 180, 270, 2.0).line(xr + r + 18, h + 6)
        return [self.hbar(0, W, X - h), self.stem(xl, 0, X), self.st(right)], "O", "O"

    def gr_rho(self):
        s, h = self.s, self.h
        bw = self.ow * 0.99
        return [self.bowl(0, bw, -s.ov, s.xh + s.ov), self.stem(h, s.desc, s.xh * 0.5)], "S", "R"

    def gr_sigma(self):
        s, h = self.s, self.h
        X = s.xh
        bw = self.ow * 0.96
        return [self.bowl(0, bw, -s.ov, X + s.ov), self.line(bw * 0.50, X - h + s.ov * 0.5, bw + 96 + (s.w - 88) * 0.3, X - h)], "R", "O"

    def gr_sigmafinal(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.ow * 0.86
        k = [(W - h, X - h - 12, 160), (W * 0.52, X + s.ov - h, 180), (h, X * 0.48, 270), (W * 0.50, h + 6, 0),
             (W * 0.78, -54, 290), (W * 0.56, s.desc + h, 210)]
        return [self.spline(k, 0.40)], "R", "O"

    def gr_tau(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.90
        x = W * 0.46
        r = h + 28
        p = Path(x, X - h).line(x, h + r).arc(x + r, h + r, r, r, 180, 270, 2.0).line(x + r + 24, h + 8)
        return [self.hbar(0, W, X - h), self.st(p)], "O", "O"

    def gr_upsilon(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 0.98
        cy = X * 0.50
        p = Path(h, X - h).line(h, cy).arc(W / 2, cy, W / 2 - h, cy - h + s.ov, 180, 360, s.n).line(W - h, X - h - 20)
        return [self.st(p)], "S", "S"

    def gr_phi(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.ow * 1.30
        return [self.bowl(0, W, -s.ov, X + s.ov), self.stem(W / 2, s.desc, X + 150 + (s.w - 88) * 0.5)], "R", "R"

    def gr_chi(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 1.0
        return [self.line(h, X - h, W - h, s.desc + h), self.line(W - h, X - h, h, s.desc + h)], "D", "D"

    def gr_psi(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 1.24
        cy = X * 0.52
        cup = Path(h, X - h).line(h, cy).arc(W / 2, cy, W / 2 - h, cy - h + s.ov, 180, 360, s.n).line(W - h, X - h)
        return [self.st(cup), self.stem(W / 2, s.desc, self.gasc)], "S", "S"

    def gr_omega(self):
        s, h = self.s, self.h
        X = s.xh
        W = self.nw * 1.50
        m = W / 2
        jy = X * 0.52
        left = self.spline([(W * 0.24, X - h, 228), (h, X * 0.42, 270), (W * 0.27, h - s.ov, 0), (m, jy, 90)], 0.40)
        right = self.spline([(m, jy, 270), (W * 0.73, h - s.ov, 0), (W - h, X * 0.42, 90), (W * 0.76, X - h, 132)], 0.40)
        return [left, right], "R", "R"

    def gr_anoteleia(self):
        return [self.dot(self.dr, self.s.xh - self.dr)], "P", "P"

    # ---------------- marks ----------------

    def tonos(self, cx, base_y):
        """Greek tonos over a lowercase letter: steeper and shorter than the acute."""
        s, h = self.s, self.h
        y = base_y + self.dot_gap * 0.62
        L = 100 + s.w * 0.35
        return [self.line(cx - L * 0.14, y + h, cx + L * 0.22, y + L * 0.98 + h * 0.4, 0.82)]

    def tonos_cap(self):
        """Capital tonos, set before the letter at its top left (x from 0)."""
        s, h = self.s, self.h
        L = 100 + s.w * 0.35
        hh = h * 0.82
        top = s.cap + 24
        return [self.line(hh, top - L * 1.02, hh + L * 0.36, top - hh * 0.6, 0.82)]

    def dialytika_tonos(self, cx, base_y):
        """Dialytika with the tonos between the dots: the dots move apart until the tonos has room."""
        s = self.s
        dots = self.accent("dieresis", cx, base_y, False)
        tonos = self.tonos(cx, base_y + 12)
        gap = max(18.0, s.w * 0.16)
        sp = 0.0
        while sp < 200 and min(clearance([move([dots[0]], -sp)[0]], tonos), clearance([move([dots[1]], sp)[0]], tonos)) < gap:
            sp += 4.0
        return [move([dots[0]], -sp)[0], move([dots[1]], sp)[0]] + tonos

    # ---------------- composition ----------------

    def base(self, ch):
        shapes, lc, rc = getattr(self, BASE[ch])()
        x0, _, x1, _ = self.box(shapes)
        if ch in "јj":
            x0, lc = self._j_left(), "S"
        return shapes, lc, rc, (x0, x1)

    def compose(self, ch):
        if ch in BASE:
            shapes, lc, rc, rng = self.base(ch)
            return shapes, lc, rc, ch.isupper(), rng
        if ch in SPACING_MARKS:
            return self.spacing_mark(ch)
        b, marks = COMPOSE[ch]
        shapes, lc, rc, (x0, x1) = self.base(b)
        s, h = self.s, self.h
        upper = b.isupper()
        bx0, _, bx1, _ = self.box(shapes)
        cx = STEM_X.get(b, lambda g: (bx0 + bx1) / 2)(self)
        for m in marks:
            if m == "tonoscap":
                mark = self.tonos_cap()
                gap = 36 + s.w * 0.25
                dx = tuck_offset(mark, shapes, gap)
                tx0 = self.box(mark)[0]
                dx = max(dx, self.box(mark)[2] + gap * 0.25 - bx0)
                shapes = move(shapes, dx)
                x0, x1 = tx0, x1 + dx
                shapes = mark + shapes
                lc = "O"
                continue
            by = s.cap if upper else s.xh
            if m == "tonos":
                mark = self.tonos(cx, by)
            elif m == "dialytikatonos":
                mark = self.dialytika_tonos(cx, by)
            else:
                mark = self.accent(m, cx, by, upper)
            mark = self._seat(shapes, mark, 0.0, 2.0)  # same rule as the Latin marks: never touching the letter
            mx0, _, mx1, _ = self.box(mark)
            x0, x1 = min(x0, mx0 + 25), max(x1, mx1 - 25)
            shapes = shapes + mark
        return shapes, lc, rc, upper, (x0, x1)

    def spacing_mark(self, ch):
        s = self.s
        if ch == "΄":
            mark = self.tonos(0, s.xh)
        else:
            mark = self.dialytika_tonos(0, s.xh)
        x0, _, x1, _ = self.box(mark)
        return mark, "P", "P", False, (x0, x1)


# Character -> glyph method (Latin methods where the letters are the same).
BASE = {
    # Cyrillic capitals
    "А": "g_A", "Б": "cy_Be", "В": "g_B", "Г": "cy_Ge", "Д": "cy_De", "Е": "g_E", "Ж": "cy_Zhe", "З": "cy_Ze",
    "И": "cy_I", "К": "g_K", "Л": "cy_El", "М": "g_M", "Н": "g_H", "О": "g_O", "П": "cy_Pe", "Р": "g_P", "С": "g_C",
    "Т": "g_T", "У": "cy_U", "Ф": "cy_Ef", "Х": "g_X", "Ц": "cy_Tse", "Ч": "cy_Che", "Ш": "cy_Sha", "Щ": "cy_Shcha",
    "Ъ": "cy_Hard", "Ы": "cy_Yeru", "Ь": "cy_Soft", "Э": "cy_Ereversed", "Ю": "cy_Yu", "Я": "cy_Ya",
    "Ђ": "cy_Dje", "Є": "cy_Ieukr", "Ѕ": "g_S", "І": "g_I", "Ј": "g_J", "Љ": "cy_Lje", "Њ": "cy_Nje", "Ћ": "cy_Tshe",
    "Џ": "cy_Dzhe", "Ґ": "cy_Gheup",
    # Cyrillic small letters
    "а": "g_a", "б": "cy_be", "в": "cy_ve", "г": "cy_ge", "д": "cy_de", "е": "g_e", "ж": "cy_zhe", "з": "cy_ze",
    "и": "cy_i", "к": "cy_ka", "л": "cy_el", "м": "cy_em", "н": "cy_en", "о": "g_o", "п": "cy_pe", "р": "g_p",
    "с": "g_c", "т": "cy_te", "у": "g_y", "ф": "cy_ef", "х": "g_x", "ц": "cy_tse", "ч": "cy_che", "ш": "cy_sha",
    "щ": "cy_shcha", "ъ": "cy_hard", "ы": "cy_yeru", "ь": "cy_soft", "э": "cy_ereversed", "ю": "cy_yu", "я": "cy_ya",
    "ђ": "cy_dje", "є": "cy_ieukr", "ѕ": "g_s", "і": "g_i", "ј": "g_j", "љ": "cy_lje", "њ": "cy_nje", "ћ": "cy_tshe",
    "џ": "cy_dzhe", "ґ": "cy_gheup", "№": "cy_numero",
    # Greek capitals
    "Α": "g_A", "Β": "g_B", "Γ": "cy_Ge", "Δ": "gr_Delta", "Ε": "g_E", "Ζ": "g_Z", "Η": "g_H", "Θ": "gr_Theta",
    "Ι": "g_I", "Κ": "g_K", "Λ": "gr_Lambda", "Μ": "g_M", "Ν": "g_N", "Ξ": "gr_Xi", "Ο": "g_O", "Π": "cy_Pe",
    "Ρ": "g_P", "Σ": "gr_Sigma", "Τ": "g_T", "Υ": "g_Y", "Φ": "cy_Ef", "Χ": "g_X", "Ψ": "gr_Psi", "Ω": "gr_Omega",
    # Greek small letters
    "α": "gr_alpha", "β": "gr_beta", "γ": "gr_gamma", "δ": "gr_delta", "ε": "gr_epsilon", "ζ": "gr_zeta",
    "η": "gr_eta", "θ": "gr_theta", "ι": "gr_iota", "κ": "g_kgreenlandic", "λ": "gr_lambda", "μ": "g_mu",
    "ν": "gr_nu", "ξ": "gr_xi", "ο": "g_o", "π": "gr_pi", "ρ": "gr_rho", "ς": "gr_sigmafinal", "σ": "gr_sigma",
    "τ": "gr_tau", "υ": "gr_upsilon", "φ": "gr_phi", "χ": "gr_chi", "ψ": "gr_psi", "ω": "gr_omega",
    ";": "g_semicolon", "·": "gr_anoteleia",
    # bases used by compositions only
    "ı": "g_dotless_i",
}

COMPOSE = {
    "Ѐ": ("Е", ["grave"]), "Ё": ("Е", ["dieresis"]), "Ѓ": ("Г", ["acute"]), "Ї": ("І", ["dieresis"]),
    "Ќ": ("К", ["acute"]), "Ѝ": ("И", ["grave"]), "Ў": ("У", ["breve"]), "Й": ("И", ["breve"]),
    "й": ("и", ["breve"]), "ѐ": ("е", ["grave"]), "ё": ("е", ["dieresis"]), "ѓ": ("г", ["acute"]),
    "ї": ("ı", ["dieresis"]), "ќ": ("к", ["acute"]), "ѝ": ("и", ["grave"]), "ў": ("у", ["breve"]),
    "Ά": ("Α", ["tonoscap"]), "Έ": ("Ε", ["tonoscap"]), "Ή": ("Η", ["tonoscap"]), "Ί": ("Ι", ["tonoscap"]),
    "Ό": ("Ο", ["tonoscap"]), "Ύ": ("Υ", ["tonoscap"]), "Ώ": ("Ω", ["tonoscap"]),
    "Ϊ": ("Ι", ["dieresis"]), "Ϋ": ("Υ", ["dieresis"]),
    "ά": ("α", ["tonos"]), "έ": ("ε", ["tonos"]), "ή": ("η", ["tonos"]), "ί": ("ι", ["tonos"]),
    "ό": ("ο", ["tonos"]), "ύ": ("υ", ["tonos"]), "ώ": ("ω", ["tonos"]),
    "ϊ": ("ι", ["dieresis"]), "ϋ": ("υ", ["dieresis"]), "ΐ": ("ι", ["dialytikatonos"]), "ΰ": ("υ", ["dialytikatonos"]),
}

SPACING_MARKS = {"΄", "΅"}

# Mark axis for letters whose ink center is not where a mark sits.
STEM_X = {
    "ı": lambda g: g.h,
    "ι": lambda g: g.h,
    "η": lambda g: g.nw / 2,
    "а": lambda g: g.ow * 0.98 * 0.47,
}


def charset():
    cyr = [chr(c) for c in range(0x0400, 0x0460)] + ["Ґ", "ґ", "№"]
    greek = [";", "΄", "΅", "Ά", "·", "Έ", "Ή", "Ί", "Ό", "Ύ", "Ώ"]
    greek += [chr(c) for c in range(0x0390, 0x03CF) if c != 0x03A2]
    return cyr + greek


_CACHE = {}


def glyphs_for(style):
    key = style_key(style)
    g = _CACHE.get(key)
    if g is None:
        g = _CACHE[key] = CyrGrk(style)
    return g


def make(ch, style):
    """(shapes, left_class, right_class, upper, (x0, x1)) for one character, like Glyphs.make."""
    return glyphs_for(style).compose(ch)


def features(style):
    return ""


# ------------------------------------------------------------------------------------------- kerning

# Same idea as the Latin sets of pw_font_build: open or diagonal right sides on the left, open or round left sides on
# the right, plus punctuation and the guillemets of Russian and Ukrainian text. Pairs stay inside one script.
PUNCT_LEFT = ".,’'\"”«"
PUNCT_RIGHT = ".,’'\"“»"
KERN_SETS = [
    ("АГҐЃКЌРТЋЂУЎХЖОФЭЮ" "гґѓкќтуўхж", "АДЛЉТУЎХЖЯОСЄЭФ" "адлљуўхжяосеёєфч"),
    ("ΑΓΚΡΤΥΎΫΧΛΔΟΘΦΩ" "γκντχλ", "ΑΆΔΛΤΥΎΧΟΌΘΦΩΏ" "αάδλοόσςεέωώθφχγνυύ"),
]
KERN_LEFT = "".join(l for l, _ in KERN_SETS) + PUNCT_LEFT
KERN_RIGHT = "".join(r for _, r in KERN_SETS) + PUNCT_RIGHT


def kern_variants():
    """Accented right letters measured with their own glyphs, like the Latin build does."""
    out = {}
    for ch, (b, _) in COMPOSE.items():
        if b in KERN_RIGHT and ch not in KERN_RIGHT:
            out.setdefault(b, []).append(ch)
    return out


def kerning(cmap, data, style, existing=None):
    """Extra pairs for Cyrillic and Greek: letters of one script with each other and with punctuation, plus the
    accented right letters (Тё, Γέ). `existing` holds pairs already made (they are never overwritten)."""
    variants = kern_variants()
    pairs = {}
    for left, right in KERN_SETS:
        pairs.update(measure_pairs(left, right + PUNCT_RIGHT, variants, cmap, data, existing))
        pairs.update(measure_pairs(PUNCT_LEFT, right, variants, cmap, data, existing))
    return pairs
