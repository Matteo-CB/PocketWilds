"""Letterforms of the Pocket Wilds fonts, written as stroke skeletons (T31).

Every glyph method returns (shapes, left_class, right_class). Coordinates: font units, ink starts near x=0,
baseline y=0. Proportions follow the Style, so one skeleton set gives every candidate and every weight.
Spacing classes: 'S' straight, 'R' round, 'D' diagonal, 'O' open, 'P' punctuation.
"""
import math
import unicodedata
from dataclasses import dataclass

from pw_font_engine import Dot, Path, Poly, Stroke, bounds, glyph_contours


@dataclass
class Style:
    key: str
    family: str
    weight_name: str
    w: float = 88.0           # stroke width
    xh: float = 530.0         # x-height
    cap: float = 700.0
    asc: float = 740.0
    desc: float = -200.0
    ov: float = 10.0          # overshoot of round shapes
    n: float = 2.0            # superellipse exponent of bowls (2 = ellipse, higher = squarer)
    wf: float = 1.0           # width factor
    a_story: int = 2          # 2 = double-story a, 1 = single-story
    l_tail: bool = True       # l with a small foot
    swell: float = 0.0        # extra width at stroke ends (plush terminals)
    inflate: float = 0.0      # extra width in the middle of open strokes (puffy display)
    bounce: float = 0.0       # baseline offset per glyph (display)
    tilt: float = 0.0         # rotation per glyph in degrees (display)
    aperture: float = 42.0    # c/e/s terminal angle
    tracking: float = 0.0     # extra side bearing
    tabular: bool = True
    grow: float = 1.0         # how much letters widen with weight (display fonts need more room for counters)
    bar: float = 1.0          # width of horizontal bars relative to the stroke (e, A, H...)


ACCENTS = {
    "À": ("A", "grave"), "Á": ("A", "acute"), "Â": ("A", "circumflex"), "Ã": ("A", "tilde"), "Ä": ("A", "dieresis"),
    "Å": ("A", "ring"), "Ç": ("C", "cedilla"), "È": ("E", "grave"), "É": ("E", "acute"), "Ê": ("E", "circumflex"),
    "Ë": ("E", "dieresis"), "Ì": ("I", "grave"), "Í": ("I", "acute"), "Î": ("I", "circumflex"), "Ï": ("I", "dieresis"),
    "Ñ": ("N", "tilde"), "Ò": ("O", "grave"), "Ó": ("O", "acute"), "Ô": ("O", "circumflex"), "Õ": ("O", "tilde"),
    "Ö": ("O", "dieresis"), "Ù": ("U", "grave"), "Ú": ("U", "acute"), "Û": ("U", "circumflex"), "Ü": ("U", "dieresis"),
    "Ý": ("Y", "acute"), "Ÿ": ("Y", "dieresis"),
    "à": ("a", "grave"), "á": ("a", "acute"), "â": ("a", "circumflex"), "ã": ("a", "tilde"), "ä": ("a", "dieresis"),
    "å": ("a", "ring"), "ç": ("c", "cedilla"), "è": ("e", "grave"), "é": ("e", "acute"), "ê": ("e", "circumflex"),
    "ë": ("e", "dieresis"), "ì": ("ı", "grave"), "í": ("ı", "acute"), "î": ("ı", "circumflex"), "ï": ("ı", "dieresis"),
    "ñ": ("n", "tilde"), "ò": ("o", "grave"), "ó": ("o", "acute"), "ô": ("o", "circumflex"), "õ": ("o", "tilde"),
    "ö": ("o", "dieresis"), "ù": ("u", "grave"), "ú": ("u", "acute"), "û": ("u", "circumflex"), "ü": ("u", "dieresis"),
    "ý": ("y", "acute"), "ÿ": ("y", "dieresis"),
}


class Glyphs:
    def __init__(self, s: Style):
        self.s = s
        w = s.w
        self.h = w / 2
        k = s.wf
        grow = (w - 88.0) * s.grow
        self.ow = (470 + 1.15 * grow) * k        # o
        self.nw = (430 + 1.05 * grow) * k        # n
        self.Ow = (640 + 1.3 * grow) * k         # O
        self.Hw = (560 + 1.2 * grow) * k         # H
        self.dw = (520 + 1.1 * grow) * k         # digits ink width
        self.dr = max(w * 0.62, w * 0.5 + 8)     # dot radius
        self.dot_gap = max(55.0, w * 0.75)

    # ---------------- helpers ----------------

    def st(self, path, mult=1.0):
        s = self.s
        fn = None
        if not path.closed and (s.swell > 0 or s.inflate > 0):
            sw, inf = s.swell, s.inflate

            def fn(t, sw=sw, inf=inf):
                end = max(0.0, 1.0 - min(t, 1.0 - t) * 8.0)  # 1 at the ends, 0 after 12 % of the length
                return 1.0 + sw * end + inf * math.sin(math.pi * t)
        return Stroke(path, self.s.w * mult, fn)

    def line(self, x0, y0, x1, y1, mult=1.0):
        return self.st(Path(x0, y0).line(x1, y1), mult)

    def stem(self, x, y0, y1):
        """Vertical stroke whose round caps reach y0 and y1 exactly."""
        return self.line(x, y0 + self.h, x, y1 - self.h)

    def bowl(self, x0, x1, y0, y1, n=None, mult=1.0):
        """Closed bowl whose outer edge fits the box."""
        h = self.h * mult
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        rx, ry = (x1 - x0) / 2 - h, (y1 - y0) / 2 - h
        p = Path(*_se(cx, cy, rx, ry, 0, n or self.s.n)).arc(cx, cy, rx, ry, 0, 360, n or self.s.n).close()
        return self.st(p, mult)

    def open_arc(self, x0, x1, y0, y1, a0, a1, n=None, mult=1.0):
        h = self.h * mult
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        rx, ry = (x1 - x0) / 2 - h, (y1 - y0) / 2 - h
        nn = n or self.s.n
        return self.st(Path(*_se(cx, cy, rx, ry, a0, nn)).arc(cx, cy, rx, ry, a0, a1, nn), mult)

    def spline(self, knots, tension=0.39):
        x, y, _ = knots[0]
        return self.st(Path(x, y).spline(knots, tension))

    def dot(self, x, y, scale=1.0):
        return Dot(x, y, self.dr * scale)

    def fill(self, pts, r):
        """Filled polygon whose outline grows by r with round corners (polygon + a capsule on every edge)."""
        shapes = [Poly(pts)]
        for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
            shapes.append(Stroke(Path(x0, y0).line(x1, y1), 2 * r))
        return shapes

    def small(self, shapes, sc, dx=0.0, dy=0.0, wm=0.78):
        """Shapes scaled by sc (superscripts, fractions, ordinals) with strokes at wm of the full weight."""
        return [_map_points(sh, lambda x, y: (x * sc + dx, y * sc + dy), wm) for sh in shapes]

    @staticmethod
    def box(shapes):
        return bounds(glyph_contours(shapes))

    # ---------------- lowercase ----------------

    def g_o(self):
        s = self.s
        return [self.bowl(0, self.ow, -s.ov, s.xh + s.ov)], "R", "R"

    def g_c(self):
        s = self.s
        a = s.aperture + max(0.0, s.w - 88) * 0.2  # heavier strokes would close the opening
        return [self.open_arc(0, self.ow * 0.92, -s.ov, s.xh + s.ov, a, 360 - a)], "R", "O"

    def g_e(self):
        s, h = self.s, self.h
        ew = self.ow * 0.97
        cx, cy = ew / 2, s.xh / 2
        rx = ew / 2 - h
        bar = self.line(h, cy, ew - h, cy, s.bar)
        arc = self.open_arc(0, ew, -s.ov, s.xh + s.ov, 0, 360 - (s.aperture - 6 + max(0.0, s.w - 88) * 0.35))
        return [bar, arc], "R", "O"

    def g_a(self):
        s, h = self.s, self.h
        if s.a_story == 1:
            aw = self.ow * 0.98
            return [self.bowl(0, aw, -s.ov, s.xh + s.ov), self.stem(aw - h, 0, s.xh)], "R", "S"
        aw = self.ow * 0.93
        R = aw - h
        cyh = s.xh * 0.56
        top = s.xh + s.ov - h
        rx = (R - h) * 0.52
        cxh = R - rx
        hook = Path(*_se(cxh, cyh, rx, top - cyh, 158, 2.0)).arc(cxh, cyh, rx, top - cyh, 158, 0, 2.0).line(R, h)
        bowl_top = s.xh * 0.60
        return [self.st(hook), self.bowl(0, R + h, -s.ov, bowl_top)], "O", "S"

    def g_b(self):
        s, h = self.s, self.h
        return [self.stem(h, 0, s.asc), self.bowl(0, self.ow * 0.99, -s.ov, s.xh + s.ov)], "S", "R"

    def g_d(self):
        s, h = self.s, self.h
        bw = self.ow * 0.99
        return [self.bowl(0, bw, -s.ov, s.xh + s.ov), self.stem(bw - h, 0, s.asc)], "R", "S"

    def g_p(self):
        s, h = self.s, self.h
        return [self.stem(h, s.desc, s.xh), self.bowl(0, self.ow * 0.99, -s.ov, s.xh + s.ov)], "S", "R"

    def g_q(self):
        s, h = self.s, self.h
        bw = self.ow * 0.99
        return [self.bowl(0, bw, -s.ov, s.xh + s.ov), self.stem(bw - h, s.desc, s.xh)], "R", "S"

    def g_g(self):
        s, h = self.s, self.h
        bw = self.ow * 0.99
        R = bw - h
        yb = s.desc - s.ov + h
        cyg = s.desc * 0.30
        cxg = bw * 0.47
        tail = Path(R, s.xh - h).line(R, cyg).arc(cxg, cyg, R - cxg, cyg - yb, 0, -152, 2.0)
        return [self.bowl(0, bw, -s.ov, s.xh + s.ov), self.st(tail)], "R", "S"

    def _arch(self, x0, x1, top, y_join):
        """Arch from the left stem (x0 centerline) to the right leg (x1 centerline), down to the baseline."""
        h = self.h
        cx = (x0 + x1) / 2
        rx = (x1 - x0) / 2
        p = Path(*_se(cx, y_join, rx, top - y_join, 180, self.s.n)).arc(cx, y_join, rx, top - y_join, 180, 0, self.s.n).line(x1, h)
        return self.st(p)

    def g_n(self):
        s, h = self.s, self.h
        top = s.xh + s.ov - h
        return [self.stem(h, 0, s.xh), self._arch(h, self.nw - h, top, s.xh * 0.46)], "S", "S"

    def g_h(self):
        s, h = self.s, self.h
        top = s.xh + s.ov - h
        return [self.stem(h, 0, s.asc), self._arch(h, self.nw - h, top, s.xh * 0.46)], "S", "S"

    def g_m(self):
        s, h = self.s, self.h
        mw = self.nw * 1.58
        top = s.xh + s.ov - h
        mid = mw / 2
        return [self.stem(h, 0, s.xh), self._arch(h, mid, top, s.xh * 0.5), self._arch(mid, mw - h, top, s.xh * 0.5)], "S", "S"

    def g_u(self):
        s, h = self.s, self.h
        R = self.nw - h
        cy = s.xh * 0.54
        bottom = h - s.ov
        cx = (h + R) / 2
        p = Path(h, s.xh - h).line(h, cy).arc(cx, cy, (R - h) / 2, cy - bottom, 180, 360, s.n)
        return [self.st(p), self.stem(R, 0, s.xh)], "S", "S"

    def g_r(self):
        s, h = self.s, self.h
        rw = self.nw * 0.70
        top = s.xh + s.ov - h
        cyj = s.xh * 0.50
        rx = rw - h - 10
        p = Path(*_se(h + rx, cyj, rx, top - cyj, 180, s.n)).arc(h + rx, cyj, rx, top - cyj, 180, 62, s.n)
        return [self.stem(h, 0, s.xh), self.st(p)], "S", "O"

    def g_i(self):
        s, h = self.s, self.h
        return [self.stem(h, 0, s.xh), self.dot(h, s.xh + self.dot_gap + self.dr)], "S", "S"

    def g_dotless_i(self):
        s, h = self.s, self.h
        return [self.stem(h, 0, s.xh)], "S", "S"

    def g_j(self):
        s, h = self.s, self.h
        x = h + 70
        yb = s.desc - s.ov + h
        cy = s.desc * 0.25
        rx = 95
        p = Path(x, s.xh - h).line(x, cy).arc(x - rx, cy, rx, cy - yb, 0, -150, 2.0)
        return [self.st(p), self.dot(x, s.xh + self.dot_gap + self.dr)], "O", "S"

    def g_k(self):
        s, h = self.s, self.h
        kw = self.nw * 0.92
        arm_end = (h + 6, s.xh * 0.36)
        arm = self.line(kw - h - 10, s.xh - h, *arm_end)
        t = 0.52
        jx = (kw - h - 10) * (1 - t) + arm_end[0] * t
        jy = (s.xh - h) * (1 - t) + arm_end[1] * t
        leg = self.line(jx, jy, kw - h, h)
        return [self.stem(h, 0, s.asc), arm, leg], "S", "D"

    def g_l(self):
        s, h = self.s, self.h
        if not s.l_tail:
            return [self.stem(h, 0, s.asc)], "S", "S"
        r = h + 34
        p = Path(h, s.asc - h).line(h, h + r).arc(h + r, h + r, r, r, 180, 270, 2.0).line(h + r + 26, h + 10)
        return [self.st(p)], "S", "O"

    def g_t(self):
        s, h = self.s, self.h
        x = h + 64
        r = h + 30
        top = s.xh + s.xh * 0.24
        p = Path(x, top - h).line(x, h + r).arc(x + r, h + r, r, r, 180, 270, 2.0).line(x + r + 30, h + 12)
        bar = self.line(h, s.xh - h, x + 118 + (self.s.w - 88) * 0.5, s.xh - h)
        return [self.st(p), bar], "O", "O"

    def g_f(self):
        s, h = self.s, self.h
        x = h + 66
        r = 92 + (self.s.w - 88) * 0.4
        top = s.asc - h
        p = Path(x, h).line(x, top - r).arc(x + r, top - r, r, r, 180, 90, 2.0).line(x + r + 46, top - 16)
        bar = self.line(h, s.xh - h, x + 118 + (self.s.w - 88) * 0.5, s.xh - h)
        return [self.st(p), bar], "O", "O"

    def g_s(self):
        s, h = self.s, self.h
        sw = self.ow * (0.80 + min(0.08, (s.w - 88) * 0.0008))
        X = s.xh
        k = [
            (sw - h - 4, X * 0.80, 112),
            (sw * 0.50, X + s.ov - h, 180),
            (h + 2, X * 0.71, 270),
            (sw * 0.50, X * 0.49, 334),
            (sw - h - 1, X * 0.24, 270),
            (sw * 0.48, h - s.ov, 180),
            (h + 2, X * 0.19, 118),
        ]
        return [self.spline(k)], "R", "R"

    def g_v(self):
        s, h = self.s, self.h
        vw = self.nw * 0.98
        return [self.line(h, s.xh - h, vw / 2, h), self.line(vw / 2, h, vw - h, s.xh - h)], "D", "D"

    def g_w(self):
        s, h = self.s, self.h
        ww = self.nw * 1.48
        pts = [(h, s.xh - h), (ww * 0.27, h), (ww * 0.5, s.xh * 0.70), (ww * 0.73, h), (ww - h, s.xh - h)]
        return [self.line(*pts[i], *pts[i + 1]) for i in range(4)], "D", "D"

    def g_x(self):
        s, h = self.s, self.h
        xw = self.nw * 0.92
        return [self.line(h, s.xh - h, xw - h, h), self.line(xw - h, s.xh - h, h, h)], "D", "D"

    def g_y(self):
        s, h = self.s, self.h
        yw = self.nw * 0.98
        top = (yw - h, s.xh - h)
        bottom = (yw * 0.30, s.desc + h)
        t = (top[1] - (h + 8)) / (top[1] - bottom[1])
        meet = (top[0] + (bottom[0] - top[0]) * t, h + 8)
        return [self.line(h, s.xh - h, *meet), self.line(*top, *bottom)], "D", "D"

    def g_z(self):
        s, h = self.s, self.h
        zw = self.nw * 0.86
        return [self.line(h, s.xh - h, zw - h, s.xh - h), self.line(zw - h, s.xh - h, h, h), self.line(h, h, zw - h, h)], "D", "D"

    def g_germandbls(self):
        s, h = self.s, self.h
        bw = self.ow * 0.95
        top = s.asc + s.ov - h
        k = [
            (h, h, 90),
            (h, s.asc - 150, 90),
            (bw * 0.47, top, 0),
            (bw - h - 30, s.asc - 150, 270),
            (bw * 0.42, s.xh * 0.88, 300),
            (bw - h, s.xh * 0.34, 270),
            (bw * 0.50, h - s.ov, 180),
            (bw * 0.26, h + 6, 160),
        ]
        return [self.spline(k)], "S", "R"

    # ---------------- uppercase ----------------

    def g_A(self):
        s, h = self.s, self.h
        aw = self.Hw * 1.10
        apex = (aw / 2, s.cap - h)
        yb = s.cap * 0.30
        t = (apex[1] - yb) / (apex[1] - h)
        xl = apex[0] + (h - apex[0]) * t
        xr = apex[0] + (aw - h - apex[0]) * t
        return [self.line(h, h, *apex), self.line(*apex, aw - h, h), self.line(xl, yb, xr, yb, s.bar)], "D", "D"

    def g_B(self):
        s, h = self.s, self.h
        bw = self.Hw * 0.92
        ym = s.cap * 0.53
        r1 = (s.cap - h - ym) / 2
        x1 = bw * 0.90 - h - r1
        r2 = (ym - h) / 2
        x2 = bw - h - r2
        top = Path(h, s.cap - h).line(x1, s.cap - h).arc(x1, ym + r1, r1, r1, 90, -90, s.n).line(h, ym)
        bot = Path(h, ym).line(x2, ym).arc(x2, h + r2, r2, r2, 90, -90, s.n).line(h, h)
        return [self.stem(h, 0, s.cap), self.st(top), self.st(bot)], "S", "R"

    def g_C(self):
        s = self.s
        return [self.open_arc(0, self.Ow * 0.92, -s.ov, s.cap + s.ov, 45, 315)], "R", "O"

    def g_D(self):
        s, h = self.s, self.h
        dw = self.Ow * 0.94
        ry = s.cap / 2 - h
        rx = min(ry, dw * 0.55)
        xd = dw - h - rx
        p = Path(h, s.cap - h).line(xd, s.cap - h).arc(xd, s.cap / 2, rx, ry, 90, -90, s.n).line(h, h)
        return [self.stem(h, 0, s.cap), self.st(p)], "S", "R"

    def g_E(self):
        s, h = self.s, self.h
        ew = self.Hw * 0.82
        return [self.stem(h, 0, s.cap), self.line(h, s.cap - h, ew - h, s.cap - h), self.line(h, s.cap * 0.5, ew * 0.92 - h, s.cap * 0.5),
                self.line(h, h, ew - h, h)], "S", "O"

    def g_F(self):
        s, h = self.s, self.h
        fw = self.Hw * 0.80
        return [self.stem(h, 0, s.cap), self.line(h, s.cap - h, fw - h, s.cap - h), self.line(h, s.cap * 0.5, fw * 0.90 - h, s.cap * 0.5)], "S", "O"

    def g_G(self):
        s, h = self.s, self.h
        gw = self.Ow * 0.96
        cx, cy = gw / 2, s.cap / 2
        rx = gw / 2 - h
        arc = self.open_arc(0, gw, -s.ov, s.cap + s.ov, 45, 360)
        bar = self.line(cx + rx, cy, cx + rx * 0.12, cy)
        return [arc, bar], "R", "R"

    def g_H(self):
        s, h = self.s, self.h
        hw = self.Hw
        return [self.stem(h, 0, s.cap), self.stem(hw - h, 0, s.cap), self.line(h, s.cap * 0.5, hw - h, s.cap * 0.5, s.bar)], "S", "S"

    def g_I(self):
        s, h = self.s, self.h
        return [self.stem(h, 0, s.cap)], "S", "S"

    def g_J(self):
        s, h = self.s, self.h
        jw = self.Hw * 0.72
        x = jw - h
        cy = s.cap * 0.30
        yb = h - s.ov
        cx = jw * 0.47
        p = Path(x, s.cap - h).line(x, cy).arc(cx, cy, x - cx, cy - yb, 0, -160, 2.0)
        return [self.st(p)], "O", "S"

    def g_K(self):
        s, h = self.s, self.h
        kw = self.Hw * 0.95
        a0 = (kw - h - 6, s.cap - h)
        a1 = (h + 6, s.cap * 0.36)
        t = 0.50
        j = (a0[0] * (1 - t) + a1[0] * t, a0[1] * (1 - t) + a1[1] * t)
        return [self.stem(h, 0, s.cap), self.line(*a0, *a1), self.line(*j, kw - h, h)], "S", "D"

    def g_L(self):
        s, h = self.s, self.h
        lw = self.Hw * 0.78
        return [self.stem(h, 0, s.cap), self.line(h, h, lw - h, h)], "S", "O"

    def g_M(self):
        s, h = self.s, self.h
        mw = self.Hw * 1.25
        v = (mw / 2, s.cap * 0.26)
        return [self.stem(h, 0, s.cap), self.stem(mw - h, 0, s.cap), self.line(h, s.cap - h, *v), self.line(*v, mw - h, s.cap - h)], "S", "S"

    def g_N(self):
        s, h = self.s, self.h
        nw = self.Hw * 1.02
        return [self.stem(h, 0, s.cap), self.stem(nw - h, 0, s.cap), self.line(h, s.cap - h, nw - h, h)], "S", "S"

    def g_O(self):
        s = self.s
        return [self.bowl(0, self.Ow, -s.ov, s.cap + s.ov)], "R", "R"

    def g_P(self):
        s, h = self.s, self.h
        pw = self.Hw * 0.90
        ym = s.cap * 0.42
        r = (s.cap - h - ym) / 2
        x1 = pw - h - r
        p = Path(h, s.cap - h).line(x1, s.cap - h).arc(x1, ym + r, r, r, 90, -90, s.n).line(h, ym)
        return [self.stem(h, 0, s.cap), self.st(p)], "S", "R"

    def g_Q(self):
        s, h = self.s, self.h
        ow = self.Ow
        return [self.bowl(0, ow, -s.ov, s.cap + s.ov), self.line(ow * 0.58, s.cap * 0.22, ow + 18, -42)], "R", "R"

    def g_R(self):
        s, h = self.s, self.h
        rw = self.Hw * 0.94
        ym = s.cap * 0.44
        r = (s.cap - h - ym) / 2
        x1 = rw * 0.95 - h - r
        p = Path(h, s.cap - h).line(x1, s.cap - h).arc(x1, ym + r, r, r, 90, -90, s.n).line(h, ym)
        leg = self.line(x1 - 10, ym, rw - h, h)
        return [self.stem(h, 0, s.cap), self.st(p), leg], "S", "D"

    def g_S(self):
        s, h = self.s, self.h
        sw = self.Hw * 0.90
        C = s.cap
        k = [
            (sw - h - 6, C * 0.82, 112),
            (sw * 0.50, C + s.ov - h, 180),
            (h + 2, C * 0.73, 270),
            (sw * 0.50, C * 0.495, 335),
            (sw - h, C * 0.24, 270),
            (sw * 0.48, h - s.ov, 180),
            (h + 2, C * 0.19, 116),
        ]
        return [self.spline(k)], "R", "R"

    def g_T(self):
        s, h = self.s, self.h
        tw = self.Hw * 0.90
        return [self.line(h, s.cap - h, tw - h, s.cap - h), self.stem(tw / 2, 0, s.cap)], "O", "O"

    def g_U(self):
        s, h = self.s, self.h
        uw = self.Hw
        cy = s.cap * 0.40
        yb = h - s.ov
        p = Path(h, s.cap - h).line(h, cy).arc(uw / 2, cy, uw / 2 - h, cy - yb, 180, 360, s.n).line(uw - h, s.cap - h)
        return [self.st(p)], "S", "S"

    def g_V(self):
        s, h = self.s, self.h
        vw = self.Hw * 1.05
        return [self.line(h, s.cap - h, vw / 2, h), self.line(vw / 2, h, vw - h, s.cap - h)], "D", "D"

    def g_W(self):
        s, h = self.s, self.h
        ww = self.Hw * 1.55
        pts = [(h, s.cap - h), (ww * 0.26, h), (ww * 0.5, s.cap * 0.68), (ww * 0.74, h), (ww - h, s.cap - h)]
        return [self.line(*pts[i], *pts[i + 1]) for i in range(4)], "D", "D"

    def g_X(self):
        s, h = self.s, self.h
        xw = self.Hw
        return [self.line(h, s.cap - h, xw - h, h), self.line(xw - h, s.cap - h, h, h)], "D", "D"

    def g_Y(self):
        s, h = self.s, self.h
        yw = self.Hw * 1.0
        c = (yw / 2, s.cap * 0.44)
        return [self.line(h, s.cap - h, *c), self.line(yw - h, s.cap - h, *c), self.stem(yw / 2, 0, c[1] + h)], "D", "D"

    def g_Z(self):
        s, h = self.s, self.h
        zw = self.Hw * 0.92
        return [self.line(h, s.cap - h, zw - h, s.cap - h), self.line(zw - h, s.cap - h, h, h), self.line(h, h, zw - h, h)], "D", "D"

    def g_AE(self):
        s, h = self.s, self.h
        sx = self.Hw * 0.62
        ew = sx + self.Hw * 0.62
        yb = s.cap * 0.30
        t = (s.cap - h - yb) / (s.cap - 2 * h)
        xl = sx + (h - sx) * t
        return [self.line(h, h, sx, s.cap - h), self.stem(sx, 0, s.cap), self.line(sx, s.cap - h, ew - h, s.cap - h),
                self.line(sx, s.cap * 0.5, ew * 0.95 - h, s.cap * 0.5), self.line(sx, h, ew - h, h), self.line(xl, yb, sx, yb)], "D", "O"

    def g_OE(self):
        s, h = self.s, self.h
        ow = self.Ow * 0.88
        sx = ow - h - ow * 0.18
        ew = sx + self.Hw * 0.58
        return [self.open_arc(0, ow, -s.ov, s.cap + s.ov, 60, 300), self.stem(sx, 0, s.cap), self.line(sx - ow * 0.25, s.cap - h, ew - h, s.cap - h),
                self.line(sx, s.cap * 0.5, ew * 0.95 - h, s.cap * 0.5), self.line(sx - ow * 0.25, h, ew - h, h)], "R", "O"

    def g_Oslash(self):
        s, h = self.s, self.h
        shapes, _, _ = self.g_O()
        ow = self.Ow
        shapes.append(self.line(ow * 0.10, -30, ow * 0.90, s.cap + 30))
        return shapes, "R", "R"

    def g_ae(self):
        s, h = self.s, self.h
        a_shapes, _, _ = self.g_a()
        aw = self.ow * 0.93
        e_shapes, _, _ = self.g_e()
        dx = aw - self.s.w * 0.9
        return a_shapes + [_shift(sh, dx) for sh in e_shapes], "O", "O"

    def g_oe(self):
        o_shapes, _, _ = self.g_o()
        e_shapes, _, _ = self.g_e()
        dx = self.ow - self.s.w * 1.1
        return o_shapes + [_shift(sh, dx) for sh in e_shapes], "R", "O"

    def g_oslash(self):
        s = self.s
        shapes, _, _ = self.g_o()
        shapes.append(self.line(self.ow * 0.10, -30, self.ow * 0.90, s.xh + 30))
        return shapes, "R", "R"

    # ---------------- figures (lining, tabular) ----------------

    def g_zero(self):
        s = self.s
        return [self.bowl(0, self.dw, -s.ov, s.cap + s.ov)], "R", "R"

    def g_one(self):
        s, h = self.s, self.h
        x = self.dw * 0.58
        return [self.stem(x, 0, s.cap), self.line(x, s.cap - h, x - self.dw * 0.36, s.cap * 0.76)], "D", "S"

    def g_two(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        k = [(h + 6, C * 0.72, 80), (dw * 0.50, C + s.ov - h, 0), (dw - h, C * 0.68, 270), (h, h, 226)]
        return [self.spline(k), self.line(h, h, dw - h, h)], "D", "O"

    def g_three(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        up = [(h + 4, C * 0.80, 62), (dw * 0.47, C + s.ov - h, 0), (dw - h - 18, C * 0.76, 270), (dw * 0.40, C * 0.53, 180)]
        lo = [(dw * 0.40, C * 0.53, 0), (dw - h, C * 0.26, 270), (dw * 0.48, h - s.ov, 180), (h + 2, C * 0.19, 118)]
        return [self.spline(up), self.spline(lo)], "O", "R"

    def g_four(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        x = dw * 0.70
        yb = C * 0.28
        return [self.stem(x, 0, C), self.line(h, yb, dw - h, yb), self.line(x, C - h, h, yb)], "D", "O"

    def g_five(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        xs = h + 36
        bar = self.line(dw - h - 14, C - h, xs, C - h)
        side = self.line(xs, C - h, xs - 8, C * 0.56)
        k = [(xs - 8, C * 0.56, 24), (dw * 0.52, C * 0.64 - h + 6, 0), (dw - h, C * 0.32, 270), (dw * 0.48, h - s.ov, 180), (h + 2, C * 0.18, 124)]
        return [bar, side, self.spline(k)], "O", "R"

    def g_six(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        top = C * 0.62
        bowl = self.bowl(0, dw, -s.ov, top)
        k = [(dw - h - 22, C - h - 30, 156), (dw * 0.46, C + s.ov - h, 186), (h, C * 0.50, 268), (h, top * 0.5, 270)]
        return [bowl, self.spline(k)], "R", "R"

    def g_seven(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        return [self.line(h, C - h, dw - h, C - h), self.line(dw - h, C - h, dw * 0.36, h)], "O", "D"

    def g_eight(self):
        s, h = self.s, self.h
        dw, C = self.dw, s.cap
        return [self.bowl(dw * 0.07, dw * 0.93, C * 0.50, C + s.ov), self.bowl(0, dw, -s.ov, C * 0.56)], "R", "R"

    def g_nine(self):
        shapes, _, _ = self.g_six()
        s = self.s
        return [_rotate180(sh, self.dw / 2, s.cap / 2) for sh in shapes], "R", "R"

    # ---------------- punctuation and symbols ----------------

    def g_period(self):
        return [self.dot(self.dr, self.dr)], "P", "P"

    def g_comma(self):
        r = self.dr
        return [self.dot(r, r), self.line(r + 4, r - 4, r - 26, -self.s.cap * 0.17, 0.85)], "P", "P"

    def g_colon(self):
        r = self.dr
        return [self.dot(r, r), self.dot(r, self.s.xh - r)], "P", "P"

    def g_semicolon(self):
        shapes, _, _ = self.g_comma()
        r = self.dr
        return shapes + [self.dot(r, self.s.xh - r)], "P", "P"

    def g_exclam(self):
        s, h = self.s, self.h
        x = self.dr
        return [self.line(x, s.cap - h, x, s.cap * 0.32), self.dot(x, self.dr)], "P", "P"

    def g_question(self):
        s, h = self.s, self.h
        qw = self.dw * 0.86
        C = s.cap
        k = [(h + 4, C * 0.74, 76), (qw * 0.48, C + s.ov - h, 0), (qw - h, C * 0.70, 270), (qw * 0.50, C * 0.44, 240), (qw * 0.46, C * 0.30, 270)]
        return [self.spline(k), self.dot(qw * 0.46, self.dr)], "O", "R"

    def g_exclamdown(self):
        shapes, a, b = self.g_exclam()
        return [_rotate180(sh, self.dr, self.s.xh / 2) for sh in shapes], a, b

    def g_questiondown(self):
        shapes, a, b = self.g_question()
        return [_rotate180(sh, self.dw * 0.43, self.s.xh / 2) for sh in shapes], b, a

    def g_quotesingle(self):
        s, h = self.s, self.h
        return [self.line(h, s.cap - h, h, s.cap * 0.68)], "P", "P"

    def g_quotedbl(self):
        s, h = self.s, self.h
        g = self.s.w + 50
        return [self.line(h, s.cap - h, h, s.cap * 0.68), self.line(h + g, s.cap - h, h + g, s.cap * 0.68)], "P", "P"

    def _raised_comma(self, x, flip=False):
        r = self.dr
        y = self.s.cap - r
        if flip:
            return [self.dot(x, self.s.cap * 0.70 + r), self.line(x - 4, self.s.cap * 0.70 + r + 4, x + 26, self.s.cap + 30, 0.85)]
        return [self.dot(x, y), self.line(x + 4, y - 4, x - 26, self.s.cap * 0.60, 0.85)]

    def g_quoteright(self):
        return self._raised_comma(self.dr + 20), "P", "P"

    def g_quoteleft(self):
        return self._raised_comma(self.dr, flip=True), "P", "P"

    def g_quotedblright(self):
        g = self.dr * 2 + 40
        return self._raised_comma(self.dr + 20) + self._raised_comma(self.dr + 20 + g), "P", "P"

    def g_quotedblleft(self):
        g = self.dr * 2 + 40
        return self._raised_comma(self.dr, True) + self._raised_comma(self.dr + g, True), "P", "P"

    def g_quotesinglbase(self):
        shapes, a, b = self.g_comma()
        return shapes, a, b

    def g_hyphen(self):
        s, h = self.s, self.h
        y = s.xh * 0.48
        return [self.line(h, y, 230 + (s.w - 88) * 0.4 - h, y)], "P", "P"

    def g_endash(self):
        s, h = self.s, self.h
        y = s.xh * 0.48
        return [self.line(h, y, 500 - h, y)], "P", "P"

    def g_emdash(self):
        s, h = self.s, self.h
        y = s.xh * 0.48
        return [self.line(h, y, 1000 - h, y)], "P", "P"

    def g_underscore(self):
        h = self.h
        return [self.line(h, -110, 520 - h, -110)], "P", "P"

    def g_parenleft(self):
        s, h = self.s, self.h
        pw = 190 + (s.w - 88) * 0.5
        k = [(pw - h, s.cap + 80, 238), (h + 4, s.cap * 0.40, 270), (pw - h, -150, 302)]
        return [self.spline(k)], "O", "P"

    def g_parenright(self):
        shapes, a, b = self.g_parenleft()
        pw = 190 + (self.s.w - 88) * 0.5
        return [_mirror(sh, pw / 2) for sh in shapes], b, a

    def g_bracketleft(self):
        s, h = self.s, self.h
        bw = 170
        return [self.line(bw - h, s.cap + 80, h, s.cap + 80), self.line(h, s.cap + 80, h, -150), self.line(h, -150, bw - h, -150)], "S", "P"

    def g_bracketright(self):
        shapes, a, b = self.g_bracketleft()
        return [_mirror(sh, 85) for sh in shapes], b, a

    def g_slash(self):
        s, h = self.s, self.h
        return [self.line(h, -110, 330 - h, s.cap + 60)], "D", "D"

    def g_backslash(self):
        s, h = self.s, self.h
        return [self.line(h, s.cap + 60, 330 - h, -110)], "D", "D"

    def g_ampersand(self):
        s, h = self.s, self.h
        C = s.cap
        aw = self.Hw * 1.02
        k = [(aw - h, h + 4, 136), (aw * 0.36, C * 0.55, 116), (aw * 0.30, C * 0.80, 80), (aw * 0.48, C + s.ov - h, 0),
             (aw * 0.66, C * 0.80, 270), (aw * 0.28, C * 0.45, 230), (h, C * 0.22, 272), (aw * 0.42, h - s.ov, 0),
             (aw * 0.78, C * 0.40, 60)]
        return [self.spline(k)], "R", "D"

    def g_numbersign(self):
        s, h = self.s, self.h
        w = self.dw
        C = s.cap
        return [self.line(h + 30, C * 0.66, w - h, C * 0.66), self.line(h, C * 0.32, w - h - 30, C * 0.32),
                self.line(w * 0.40, C - h, w * 0.28, h), self.line(w * 0.74, C - h, w * 0.62, h)], "D", "D"

    def _percent_parts(self):
        """Ring size follows the stroke, so the rings stay open in Bold and Black."""
        s, h = self.s, self.h
        m = 0.85
        size = 2 * s.w * m + 140
        tall = size * 1.08
        w = max(self.dw * 1.25, 2 * size + 40)
        return m, size, tall, w

    def g_percent(self):
        s, h = self.s, self.h
        C = s.cap
        m, size, tall, w = self._percent_parts()
        return [self.bowl(0, size, C + s.ov - tall, C + s.ov, mult=m), self.bowl(w - size, w, -s.ov, tall - s.ov, mult=m),
                self.line(w * 0.80, C - h, w * 0.20, h)], "R", "R"

    def _math_y(self):
        return self.s.xh * 0.50 + 10

    def g_plus(self):
        h = self.h
        y = self._math_y()
        w = self.dw * 0.86
        return [self.line(h, y, w - h, y), self.line(w / 2, y - w / 2 + h, w / 2, y + w / 2 - h)], "P", "P"

    def g_minus(self):
        h = self.h
        y = self._math_y()
        w = self.dw * 0.86
        return [self.line(h, y, w - h, y)], "P", "P"

    def g_equal(self):
        h = self.h
        y = self._math_y()
        w = self.dw * 0.86
        g = 60 + self.s.w * (1 + self.s.inflate)  # the gap stays open in Bold and in the puffy Black
        return [self.line(h, y + g / 2, w - h, y + g / 2), self.line(h, y - g / 2, w - h, y - g / 2)], "P", "P"

    def g_multiply(self):
        h = self.h
        y = self._math_y()
        r = self.dw * 0.30
        c = r + h
        return [self.line(c - r + h, y - r + h, c + r - h, y + r - h), self.line(c - r + h, y + r - h, c + r - h, y - r + h)], "P", "P"

    def g_less(self):
        h = self.h
        y = self._math_y()
        w = self.dw * 0.78
        return [self.line(w - h, y + w * 0.42, h, y), self.line(h, y, w - h, y - w * 0.42)], "P", "P"

    def g_greater(self):
        shapes, a, b = self.g_less()
        return [_mirror(sh, self.dw * 0.39) for sh in shapes], b, a

    def g_asterisk(self):
        s, h = self.s, self.h
        c = (180, s.cap - 150)
        r = 120
        shapes = []
        for k in range(5):
            a = math.radians(90 + 72 * k)
            shapes.append(self.line(c[0], c[1], c[0] + r * math.cos(a), c[1] + r * math.sin(a), 0.8))
        return shapes, "P", "P"

    def g_periodcentered(self):
        return [self.dot(self.dr, self.s.xh * 0.5)], "P", "P"

    def g_bullet(self):
        return [self.dot(self.dr * 1.5, self.s.xh * 0.5, 1.5)], "P", "P"

    def g_ellipsis(self):
        r = self.dr
        g = r * 2 + 70
        return [self.dot(r, r), self.dot(r + g, r), self.dot(r + 2 * g, r)], "P", "P"

    def _chevron(self, x0, flip):
        s, h = self.s, self.h
        y = s.xh * 0.48
        a = 110
        b = 115
        if flip:
            return [self.line(x0 + h, y + b, x0 + a + h, y, 0.9), self.line(x0 + a + h, y, x0 + h, y - b, 0.9)]
        return [self.line(x0 + a + h, y + b, x0 + h, y, 0.9), self.line(x0 + h, y, x0 + a + h, y - b, 0.9)]

    def g_guilsinglleft(self):
        return self._chevron(0, False), "P", "P"

    def g_guilsinglright(self):
        return self._chevron(0, True), "P", "P"

    def g_guillemotleft(self):
        return self._chevron(0, False) + self._chevron(150, False), "P", "P"

    def g_guillemotright(self):
        return self._chevron(0, True) + self._chevron(150, True), "P", "P"

    def g_degree(self):
        s = self.s
        d = 190 + s.w * 0.9  # the ring stays open at every weight
        return [self.bowl(0, d, s.cap - d, s.cap + s.ov, 2.0, mult=0.82)], "P", "P"

    def g_Euro(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.dw * 1.05
        arc = self.open_arc(w * 0.10, w, -s.ov, C + s.ov, 48, 312, 2.0)
        return [arc, self.line(h, C * 0.58, w * 0.62, C * 0.58, 0.85), self.line(h, C * 0.40, w * 0.58, C * 0.40, 0.85)], "O", "O"

    def g_dollar(self):
        shapes, a, b = self.g_S()
        s = self.s
        sw = self.Hw * 0.90
        scale = self.dw / sw
        shapes = [_scale_x(sh, scale) for sh in shapes]
        x = self.dw / 2
        return shapes + [self.line(x, s.cap + 90, x, -90, 0.85)], a, b

    def g_cent(self):
        s = self.s
        shapes, a, b = self.g_c()
        x = self.ow * 0.50
        return shapes + [self.line(x, s.xh + 100, x, -100, 0.85)], a, b

    def g_sterling(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.dw
        k = [(w - h - 10, C * 0.80, 112), (w * 0.56, C + s.ov - h, 180), (w * 0.30, C * 0.70, 270), (w * 0.30, C * 0.22, 270),
             (h + 10, h, 220)]
        return [self.spline(k), self.line(h, h, w - h, h), self.line(h + 10, C * 0.46, w * 0.68, C * 0.46, 0.9)], "O", "O"

    def g_yen(self):
        shapes, a, b = self.g_Y()
        s, h = self.s, self.h
        yw = self.Hw
        sc = self.dw / yw
        shapes = [_scale_x(sh, sc) for sh in shapes]
        w = self.dw
        return shapes + [self.line(w * 0.18, s.cap * 0.34, w * 0.82, s.cap * 0.34, 0.85),
                         self.line(w * 0.18, s.cap * 0.17, w * 0.82, s.cap * 0.17, 0.85)], a, b

    def g_won(self):
        shapes, a, b = self.g_W()
        s = self.s
        ww = self.Hw * 1.55
        return shapes + [self.line(0, s.cap * 0.55, ww, s.cap * 0.55, 0.8), self.line(0, s.cap * 0.38, ww, s.cap * 0.38, 0.8)], a, b

    def g_ruble(self):
        shapes, a, b = self.g_P()
        s = self.s
        return shapes + [self.line(-30, s.cap * 0.24, self.Hw * 0.55, s.cap * 0.24, 0.85)], a, b

    def g_rupee(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.dw
        r = (C * 0.27)
        bowl = Path(h, C * 0.80 - h + r).line(w * 0.45, C * 0.80 - h + r)
        return [self.line(h, C - h, w - h, C - h), self.line(h, C * 0.70, w - h, C * 0.70),
                self.st(Path(h + 20, C - h).line(w * 0.40, C - h).arc(w * 0.40, C * 0.70 + 2, w * 0.30, C * 0.30 - h - 2, 90, -90, 2.0).line(h + 20, C * 0.40)),
                self.line(h + 40, C * 0.40, w - h, h)], "S", "D"

    def g_lira(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.dw
        x = w * 0.34
        k = [(x, C - h, 270), (x, h + 40, 270), (w * 0.62, h, 0), (w - h, C * 0.42, 90)]
        return [self.spline(k), self.line(h, C * 0.56, w * 0.74, C * 0.70, 0.8), self.line(h, C * 0.38, w * 0.74, C * 0.52, 0.8)], "O", "R"

    # ---------------- ASCII completion ----------------

    def g_at(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.Ow * 1.10
        cx, cy = w / 2, C * 0.40
        rx, ry = w / 2 - h, C * 0.56 - h
        xs = cx + rx * 0.30
        ytop, ylow = cy + ry * 0.40, cy - ry * 0.42
        inner = self.bowl(cx - rx * 0.50, xs + h, ylow - h, ytop + h)
        k = [(xs, ytop, 270), (xs + rx * 0.22, ylow, 0), (cx + rx, cy + ry * 0.05, 90), (cx, cy + ry, 180),
             (cx - rx, cy, 270), (cx, cy - ry, 0), (cx + rx * 0.72, cy - ry * 0.78, 38)]
        return [inner, self.spline(k)], "R", "R"

    def g_asciicircum(self):
        s, h = self.s, self.h
        w = self.dw * 0.86
        return [self.line(h, s.cap * 0.42, w / 2, s.cap - h), self.line(w / 2, s.cap - h, w - h, s.cap * 0.42)], "P", "P"

    def g_grave(self):
        return self.accent("grave", 0, self.s.xh, False), "P", "P"

    def g_asciitilde(self):
        h = self.h
        y = self._math_y()
        w = self.dw * 0.86
        k = [(h, y - 18, 64), (w * 0.31, y + 42, 0), (w * 0.69, y - 42, 0), (w - h, y + 18, 64)]
        return [self.spline(k, 0.42)], "P", "P"

    def _brace_w(self):
        return 230 + (self.s.w - 88) * 0.5

    def g_braceleft(self):
        s, h = self.s, self.h
        C = s.cap
        bw = self._brace_w()
        xm = bw * 0.48
        up = [(bw - h, C + 80, 180), (xm, C - 10, 270), (xm, C * 0.46, 270), (h, C * 0.32, 220)]
        lo = [(h, C * 0.32, 320), (xm, C * 0.18, 270), (xm, -80, 270), (bw - h, -150, 0)]
        return [self.spline(up), self.spline(lo)], "O", "P"

    def g_braceright(self):
        shapes, a, b = self.g_braceleft()
        return [_mirror(sh, self._brace_w() / 2) for sh in shapes], b, a

    def g_bar(self):
        s, h = self.s, self.h
        return [self.stem(h, -150 - h, s.cap + 80 + h)], "S", "S"

    # ---------------- Latin-1 symbols ----------------

    def g_currency(self):
        h = self.h
        y = self._math_y()
        R = self.dw * 0.30
        cx = R + 70
        shapes = [self.bowl(cx - R, cx + R, y - R, y + R, 2.0)]
        for a in (45, 135, 225, 315):
            t = math.radians(a)
            c, sn = math.cos(t), math.sin(t)
            shapes.append(self.line(cx + c * (R - h), y + sn * (R - h), cx + c * (R + 64), y + sn * (R + 64), 0.85))
        return shapes, "P", "P"

    def g_brokenbar(self):
        s, h = self.s, self.h
        return [self.stem(h, -150 - h, s.cap * 0.30), self.stem(h, s.cap * 0.50, s.cap + 80 + h)], "S", "S"

    def _s_curve(self, sw, y0, H, m):
        h = self.h * m
        k = [(sw - h - 4, y0 + H * 0.80, 112), (sw * 0.50, y0 + H - h, 180), (h + 2, y0 + H * 0.71, 270),
             (sw * 0.50, y0 + H * 0.49, 334), (sw - h - 1, y0 + H * 0.24, 270), (sw * 0.48, y0 + h, 180),
             (h + 2, y0 + H * 0.19, 118)]
        return self.st(Path(k[0][0], k[0][1]).spline(k), m)

    def g_section(self):
        s = self.s
        C = s.cap
        m = 0.80
        sw = self.ow * 0.80 + s.w * 0.4
        H = C * 0.60 + s.w * 0.3
        return [self._s_curve(sw, C + s.ov - H, H, m), self._s_curve(sw, -s.ov - C * 0.14, H, m)], "R", "R"

    def g_dieresis(self):
        return self.accent("dieresis", 0, self.s.xh, False), "P", "P"

    def g_acute(self):
        return self.accent("acute", 0, self.s.xh, False), "P", "P"

    def g_macron(self):
        return self.accent("macron", 0, self.s.xh, False), "P", "P"

    def g_cedilla(self):
        return self.accent("cedilla", 0, 0, False), "P", "P"

    def _circled(self, inner_fn):
        s = self.s
        C = s.cap
        D = C + 2 * s.ov + 20
        cy = C / 2
        ring = self.bowl(0, D, cy - D / 2, cy + D / 2, 2.0, mult=0.78)
        return [ring] + _center(inner_fn(D), D / 2, cy)

    def g_copyright(self):
        return self._circled(lambda D: [self.open_arc(0, D * 0.46, 0, D * 0.50, 48, 312, 2.0, mult=0.78)]), "R", "R"

    def g_registered(self):
        return self._circled(lambda D: self.small(self.g_R()[0], 0.44, wm=0.78)), "R", "R"

    def _ordinal(self, method):
        s = self.s
        sc = 0.62
        shapes = self.small(getattr(self, method)()[0], sc, 0, s.cap - s.xh * sc, 0.80)
        x0, y0, x1, _ = self.box(shapes)
        y = y0 - 70
        return shapes + [self.line(x0 + self.h * 0.6, y, x1 - self.h * 0.6, y, 0.78)], "P", "P"

    def g_ordfeminine(self):
        return self._ordinal("g_a")

    def g_ordmasculine(self):
        return self._ordinal("g_o")

    def g_logicalnot(self):
        h = self.h
        y = self._math_y() + 50
        w = self.dw * 0.86
        return [self.line(h, y, w - h, y), self.line(w - h, y, w - h, y - 150)], "P", "P"

    def g_plusminus(self):
        h = self.h
        w = self.dw * 0.86
        a = w / 2 - h - 20
        y = self._math_y() + 80
        return [self.line(h, y, w - h, y), self.line(w / 2, y - a, w / 2, y + a), self.line(h, y - a - 80, w - h, y - a - 80)], "P", "P"

    def _sup(self, method, sc=0.56):
        s = self.s
        return self.small(getattr(self, method)()[0], sc, 0, s.cap * (1 - sc) + 10, 0.80), "P", "P"

    def g_onesuperior(self):
        return self._sup("g_one")

    def g_twosuperior(self):
        return self._sup("g_two")

    def g_threesuperior(self):
        return self._sup("g_three")

    def g_mu(self):
        s, h = self.s, self.h
        shapes, _, _ = self.g_u()
        return shapes + [self.stem(h, s.desc, s.xh * 0.6)], "S", "S"

    def g_paragraph(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.Hw * 0.80
        x1, x2 = w * 0.55, w - h
        cy = C * 0.70
        pts = [_se(x1, cy, x1 - h, C - cy - h, a, 2.0) for a in range(90, 271, 10)]
        return self.fill(pts, h) + [self.stem(x1, -150, C), self.stem(x2, -150, C), self.line(x1, C - h, x2, C - h)], "R", "S"

    def _fraction(self, num, den):
        s, h = self.s, self.h
        sc = 0.50
        n = self.small(getattr(self, num)()[0], sc, 0, s.cap * (1 - sc), 0.80)
        d = self.small(getattr(self, den)()[0], sc, 0, 0, 0.80)
        nx0, _, nx1, _ = self.box(n)
        n = [_shift(sh, -nx0) for sh in n]
        wn = nx1 - nx0
        xs = wn * 0.62
        slash = self.line(xs, h, xs + 250, s.cap - h, 0.8)
        dx0 = self.box(d)[0]
        d = [_shift(sh, xs + 180 - dx0) for sh in d]
        return n + [slash] + d, "P", "P"

    def g_onequarter(self):
        return self._fraction("g_one", "g_four")

    def g_onehalf(self):
        return self._fraction("g_one", "g_two")

    def g_threequarters(self):
        return self._fraction("g_three", "g_four")

    def g_divide(self):
        shapes, a, b = self.g_minus()
        y = self._math_y()
        w = self.dw * 0.86
        g = 150 + (self.s.w - 88) * 0.5
        return shapes + [self.dot(w / 2, y + g), self.dot(w / 2, y - g)], a, b

    # ---------------- letters with strokes, ligatures, Icelandic ----------------

    def g_Dcroat(self):
        shapes, a, b = self.g_D()
        s, h = self.s, self.h
        return shapes + [self.line(h - 70, s.cap * 0.5, h + 170, s.cap * 0.5, s.bar)], "O", b

    def g_dcroat(self):
        shapes, a, b = self.g_d()
        s, h = self.s, self.h
        x = self.ow * 0.99 - h
        y = s.asc - 120
        return shapes + [self.line(x - 160, y, x + 80, y, 0.9)], a, "O"

    def g_Hbar(self):
        shapes, a, b = self.g_H()
        s, h = self.s, self.h
        y = s.cap * 0.77
        return shapes + [self.line(h - 60, y, self.Hw - h + 60, y, 0.9)], "O", "O"

    def g_hbar(self):
        shapes, a, b = self.g_h()
        s, h = self.s, self.h
        y = s.asc - 125
        return shapes + [self.line(h - 60, y, h + 160, y, 0.9)], "O", b

    def g_Lslash(self):
        shapes, a, b = self.g_L()
        s, h = self.s, self.h
        return shapes + [self.line(h - 80, s.cap * 0.30, h + 130, s.cap * 0.56, 0.9)], "O", b

    def g_lslash(self):
        shapes, a, b = self.g_l()
        s, h = self.s, self.h
        return shapes + [self.line(h - 80, s.asc * 0.40, h + 120, s.asc * 0.64, 0.9)], "O", "O"

    def g_Tbar(self):
        shapes, a, b = self.g_T()
        s = self.s
        tw = self.Hw * 0.90
        return shapes + [self.line(tw * 0.24, s.cap * 0.46, tw * 0.76, s.cap * 0.46, 0.9)], a, b

    def g_tbar(self):
        shapes, a, b = self.g_t()
        s, h = self.s, self.h
        x = h + 64
        y = s.xh * 0.40
        return shapes + [self.line(x - 60, y, x + 70, y, 0.9)], a, b

    def g_Ldot(self):
        shapes, a, b = self.g_L()
        s, h = self.s, self.h
        return shapes + [self.dot(2 * h + self.dr + 60, s.cap * 0.46)], a, b

    def g_ldot(self):
        shapes, a, b = self.g_l()
        s, h = self.s, self.h
        return shapes + [self.dot(2 * h + self.dr + 44, s.xh * 0.55)], a, "P"

    def g_IJ(self):
        I, J = self.g_I()[0], self.g_J()[0]
        dx = self.box(I)[2] + 80 - self.box(J)[0]
        return I + [_shift(sh, dx) for sh in J], "S", "S"

    def g_ij(self):
        i, j = self.g_i()[0], self.g_j()[0]
        dx = self.box(i)[2] + 60 - self.box(j)[0]
        return i + [_shift(sh, dx) for sh in j], "S", "S"

    def g_kgreenlandic(self):
        s, h = self.s, self.h
        kw = self.nw * 0.92
        top = (kw - h - 10, s.xh - h)
        end = (h + 6, s.xh * 0.40)
        t = 0.50
        j = (top[0] * (1 - t) + end[0] * t, top[1] * (1 - t) + end[1] * t)
        return [self.stem(h, 0, s.xh), self.line(*top, *end), self.line(*j, kw - h, h)], "S", "D"

    def _eng(self, height, width, join, hook_cy, hook_bottom):
        s, h = self.s, self.h
        x1 = width - h
        cx, rx = (h + x1) / 2, (x1 - h) / 2
        top = height + s.ov - h
        rr = min(95.0, rx * 0.8)
        p = Path(*_se(cx, join, rx, top - join, 180, s.n)).arc(cx, join, rx, top - join, 180, 0, s.n).line(x1, hook_cy)
        p.arc(x1 - rr, hook_cy, rr, hook_cy - hook_bottom, 0, -150, 2.0)
        return [self.stem(h, 0, height), self.st(p)]

    def g_Eng(self):
        s, h = self.s, self.h
        return self._eng(s.cap, self.Hw, s.cap * 0.52, s.desc * 0.05, s.desc * 0.80 + h), "S", "S"

    def g_eng(self):
        s, h = self.s, self.h
        return self._eng(s.xh, self.nw, s.xh * 0.46, s.desc * 0.25, s.desc - s.ov + h), "S", "S"

    def g_longs(self):
        return [self.g_f()[0][0]], "S", "O"

    def g_dotlessj(self):
        shapes, a, b = self.g_j()
        return [shapes[0]], a, b

    def g_napostrophe(self):
        q = self._raised_comma(self.dr + 20)
        n, _, rc = self.g_n()
        dx = self.box(q)[2] + 40
        return q + [_shift(sh, dx) for sh in n], "P", rc

    def g_Thorn(self):
        s, h = self.s, self.h
        pw = self.Hw * 0.88
        y0, y1 = s.cap * 0.17, s.cap * 0.80
        r = (y1 - y0) / 2 - h
        x1 = pw - h - r
        p = Path(h, y1 - h).line(x1, y1 - h).arc(x1, (y0 + y1) / 2, r, r, 90, -90, s.n).line(h, y0 + h)
        return [self.stem(h, 0, s.cap), self.st(p)], "S", "R"

    def g_thorn(self):
        s, h = self.s, self.h
        return [self.stem(h, s.desc, s.asc), self.bowl(0, self.ow * 0.99, -s.ov, s.xh + s.ov)], "S", "R"

    def g_eth(self):
        s, h = self.s, self.h
        bw = self.ow * 0.98
        top = s.xh * 0.94 + s.ov
        k = [(bw - h, top * 0.48, 90), (bw * 0.66, s.asc * 0.86, 122), (bw * 0.30, s.asc - h, 160)]
        cross = self.line(bw * 0.34, s.asc * 0.80, bw * 0.86, s.asc * 0.97, 0.8)
        return [self.bowl(0, bw, -s.ov, top), self.spline(k), cross], "R", "R"

    # ---------------- more punctuation ----------------

    def g_quotedblbase(self):
        c, _, _ = self.g_comma()
        g = self.dr * 2 + 40
        return c + [_shift(sh, g) for sh in c], "P", "P"

    def g_perthousand(self):
        shapes, _, _ = self.g_percent()
        s = self.s
        m, size, tall, w = self._percent_parts()
        return shapes + [self.bowl(w + 40, w + 40 + size, -s.ov, tall - s.ov, mult=m)], "R", "R"

    def g_trademark(self):
        s = self.s
        sc = 0.44
        T = self.small(self.g_T()[0], sc, wm=0.74)
        M = self.small(self.g_M()[0], sc, wm=0.74)
        M = [_shift(sh, self.box(T)[2] + 30 - self.box(M)[0]) for sh in M]
        return [_shift(sh, 0, s.cap * (1 - sc)) for sh in T + M], "P", "P"

    # ---------------- currencies ----------------

    def g_dong(self):
        d = self.small(self.g_dcroat()[0], 0.80, 0, 130, 0.92)
        x0, _, x1, _ = self.box(d)
        hh = self.h * 0.85
        return d + [self.line(x0 + hh, hh, x1 - hh, hh, 0.85)], "O", "O"

    def g_peso(self):
        shapes, a, b = self.g_P()
        s, h = self.s, self.h
        C = s.cap
        pw = self.Hw * 0.90
        return shapes + [self.line(h - 70, C * 0.80, pw + 20, C * 0.80, 0.8), self.line(h - 70, C * 0.62, pw + 20, C * 0.62, 0.8)], "O", b

    def g_sheqel(self):
        s, h = self.s, self.h
        H = s.cap * 0.78
        W = self.dw * 1.30
        xa = W * 0.64
        cxa, rxa, ya = (h + xa) / 2, (xa - h) / 2, H * 0.52
        A = Path(h, h).line(h, ya).arc(cxa, ya, rxa, H - h - ya, 180, 0, s.n).line(xa, H * 0.36)
        xb = W * 0.36
        cxb, rxb, yb = (xb + W - h) / 2, (W - h - xb) / 2, H * 0.48
        B = Path(W - h, H - h).line(W - h, yb).arc(cxb, yb, rxb, yb - h, 0, -180, s.n).line(xb, H * 0.64)
        return [self.st(A), self.st(B)], "S", "S"

    def g_baht(self):
        shapes, a, b = self.g_B()
        x = self.Hw * 0.92 * 0.42
        return shapes + [self.line(x, -90, x, self.s.cap + 90, 0.85)], a, b

    def g_naira(self):
        shapes, a, b = self.g_N()
        s, h = self.s, self.h
        nw = self.Hw * 1.02
        return shapes + [self.line(h - 50, s.cap * y, nw - h + 50, s.cap * y, 0.8) for y in (0.60, 0.40)], "O", "O"

    def g_hryvnia(self):
        s, h = self.s, self.h
        C = s.cap
        w = self.dw
        k = [(h + 6, C * 0.76, 74), (w * 0.50, C + s.ov - h, 0), (w - h, C * 0.72, 270), (w * 0.62, C * 0.48, 222),
             (h + 4, C * 0.22, 270), (w * 0.50, h - s.ov, 0), (w - h - 4, C * 0.22, 74)]
        bars = [self.line(h - 20, C * y, w - h + 20, C * y, 0.72) for y in (0.60, 0.38)]
        return [self.spline(k)] + bars, "O", "O"

    def g_tenge(self):
        s, h = self.s, self.h
        C = s.cap
        tw = self.Hw * 0.86
        y2 = C * 0.70
        return [self.line(h, C - h, tw - h, C - h), self.line(h, y2, tw - h, y2), self.stem(tw / 2, 0, y2 + h)], "O", "O"

    def g_colonsign(self):
        shapes, a, b = self.g_C()
        w = self.Ow * 0.92
        C = self.s.cap
        return shapes + [self.line(w * x, -80, w * x + w * 0.16, C + 80, 0.75) for x in (0.34, 0.52)], a, b

    def g_guarani(self):
        shapes, a, b = self.g_G()
        x = self.Ow * 0.96 / 2
        return shapes + [self.line(x, -90, x, self.s.cap + 90, 0.85)], a, b

    def g_cedi(self):
        shapes, a, b = self.g_C()
        x = self.Ow * 0.92 * 0.52
        return shapes + [self.line(x, -90, x, self.s.cap + 90, 0.85)], a, b

    # ---------------- arrows and symbols ----------------

    def _arrow(self):
        h = self.h
        y = self._math_y()
        L = self.dw * 1.25
        a = 150 + (self.s.w - 88) * 0.4
        x1 = L - h
        return [self.line(h, y, x1, y), self.line(x1, y, x1 - a, y + a), self.line(x1, y, x1 - a, y - a)], L, y

    def g_arrowright(self):
        shapes, _, _ = self._arrow()
        return shapes, "P", "P"

    def g_arrowleft(self):
        shapes, L, _ = self._arrow()
        return [_mirror(sh, L / 2) for sh in shapes], "P", "P"

    def g_arrowup(self):
        shapes, L, y = self._arrow()
        return [_shift(_rotate(sh, 90, L / 2, y), 0, self.s.cap / 2 - y) for sh in shapes], "P", "P"

    def g_arrowdown(self):
        shapes, L, y = self._arrow()
        return [_shift(_rotate(sh, -90, L / 2, y), 0, self.s.cap / 2 - y) for sh in shapes], "P", "P"

    def _star_points(self, R, ri, cx, cy):
        pts = []
        for i in range(10):
            a = math.radians(90 + 36 * i)
            r = R if i % 2 == 0 else ri
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        return pts

    def g_blackstar(self):
        C = self.s.cap
        r = self.h * 0.8
        R = C * 0.55 - r
        return self.fill(self._star_points(R, R * 0.47, R + r, C * 0.42), r), "R", "R"

    def g_whitestar(self):
        C = self.s.cap
        m = 0.85
        hh = self.h * m
        R = C * 0.55 - hh
        pts = self._star_points(R, R * 0.47, R + hh, C * 0.42)
        return [self.line(*pts[i], *pts[(i + 1) % 10], m) for i in range(10)], "R", "R"

    def g_heart(self):
        C = self.s.cap
        r = self.h * 0.6
        raw = []
        for i in range(96):
            t = 2 * math.pi * i / 96
            raw.append((16 * math.sin(t) ** 3, 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)))
        x0 = min(p[0] for p in raw)
        y0 = min(p[1] for p in raw)
        y1 = max(p[1] for p in raw)
        k = (C * 0.88 - 2 * r) / (y1 - y0)
        pts = [((x - x0) * k + r, (y - y0) * k + r - 20) for x, y in raw]
        return self.fill(pts, r), "R", "R"

    def g_check(self):
        C = self.s.cap
        m = 1.15
        hh = self.h * m
        w = self.dw * 1.1
        p0, p1, p2 = (hh, C * 0.42), (w * 0.38, hh), (w - hh, C * 0.86)
        return [self.line(*p0, *p1, m), self.line(*p1, *p2, m)], "P", "P"

    def g_crossmark(self):
        y = self._math_y()
        a = self.dw * 0.36
        hh = self.h * 1.1
        c = a + hh
        return [self.line(c - a, y - a, c + a, y + a, 1.1), self.line(c - a, y + a, c + a, y - a, 1.1)], "P", "P"

    # ---------------- pictograms (private use area, doc 05) ----------------

    def g_paw(self):
        """U+E000: one pad and four toes."""
        C = self.s.cap
        r = min(self.h * 0.7, C * 0.06)
        w = C * 0.98
        cx = w / 2
        pad = [_se(cx, C * 0.24, w * 0.29 - r, C * 0.19 - r, a, 2.4) for a in range(0, 360, 10)]
        toe = C * 0.10
        toes = [Dot(cx - w * 0.34, C * 0.57, toe), Dot(cx - w * 0.12, C * 0.80, toe),
                Dot(cx + w * 0.12, C * 0.80, toe), Dot(cx + w * 0.34, C * 0.57, toe)]
        return self.fill(pad, r) + toes, "R", "R"

    def g_leaf(self):
        """U+E003: a plump leaf tipped to the right, with its stem."""
        C = self.s.cap
        r = self.h * 0.6
        L, b = C * 0.95, C * 0.30
        pts = []
        for i in range(0, 37):
            t = math.pi * i / 36
            pts.append((L * (1 - math.cos(t)) / 2, b * math.sin(t) ** 0.9))
        for i in range(1, 36):
            t = math.pi * (36 - i) / 36
            pts.append((L * (1 - math.cos(t)) / 2, -b * 0.75 * math.sin(t) ** 0.9))
        leaf = [_rotate(sh, 40, 0, 0) for sh in self.fill(pts, r)]
        stem = self.line(-C * 0.12, -C * 0.10, C * 0.05, C * 0.04, 0.8)
        return [_shift(sh, C * 0.14, C * 0.12) for sh in leaf + [stem]], "R", "R"

    # ---------------- composition ----------------

    def make(self, ch):
        """Shapes, spacing classes, uppercase flag and the x range used for spacing (accents may overhang)."""
        if ch in CHARS:
            shapes, lc, rc = getattr(self, CHARS[ch])()
            x0, _, x1, _ = self.box(shapes)
            if ch in "jȷ":
                x0, lc = self._j_left(), "S"
            return shapes, lc, rc, ch.isupper(), (x0, x1)
        base, marks = decompose(ch)
        shapes, lc, rc = getattr(self, CHARS[base])()
        s, h = self.s, self.h
        bx0, _, bx1, _ = self.box(shapes)
        if base in "jȷ":
            bx0, lc = self._j_left(), "S"
        upper = base.isupper()
        cx = (bx0 + bx1) / 2
        stems = {"ı": h, "l": h, "h": h, "k": h, "b": h, "ȷ": h + 70, "J": self.Hw * 0.72 - h, "t": h + 64}
        x0, x1 = bx0, bx1
        for m in marks:
            if m == "caronvertical":
                stem = self.ow * 0.99 - h if base == "d" else stems.get(base, h)
                top = s.asc if base in "dl" else s.cap if base == "L" else s.xh * 1.24 + 80
                mark = self.accent(m, stem + h + 26, top, True)
                x1 = max(x1, self.box(mark)[2])
            else:
                if m == "ogonek":
                    ax = self._ogonek_x(base, bx0, bx1)
                elif m in ("cedilla", "commaaccent"):
                    ax = stems["t"] if base == "t" else h if base == "l" else cx
                else:
                    ax = stems[base] if base in "ılhkbȷJ" else cx
                if upper:
                    by, up = s.cap, True
                elif base in "bdhklſ" and m not in ("cedilla", "commaaccent", "ogonek"):
                    by, up = s.asc, True
                else:
                    by, up = s.xh, False
                mark = self.accent(m, ax, by, up)
                mx0, _, mx1, _ = self.box(mark)
                x0, x1 = min(x0, mx0 + 25), max(x1, mx1 - 25)
            shapes = shapes + mark
        return shapes, lc, rc, upper, (x0, x1)

    def _j_left(self):
        """Spacing edge of j: its stem, so the descender hook tucks under the letter before (as in 'mjuka', 'Pijama')."""
        return 70.0

    def _ogonek_x(self, base, bx0, bx1):
        h = self.h
        if base in "Aau":
            return bx1 - h * 1.1
        if base == "E":
            return bx1 - h * 1.6
        if base in "eU":
            return (bx0 + bx1) / 2 + (bx1 - bx0) * 0.14
        return (bx0 + bx1) / 2 + h * 0.3

    # ---------------- accents ----------------

    def accent(self, kind, cx, base_y, upper):
        s, h = self.s, self.h
        lift = 30 if upper else self.dot_gap * 0.62
        y = base_y + lift
        aw = 0.82
        L = 100 + s.w * 0.35
        if kind == "acute":
            return [self.line(cx - L * 0.30, y + h, cx + L * 0.42, y + L + h * 0.4, aw)]
        if kind == "grave":
            return [self.line(cx + L * 0.30, y + h, cx - L * 0.42, y + L + h * 0.4, aw)]
        if kind == "circumflex":
            return [self.line(cx - L * 0.72, y + h, cx, y + L * 0.86 + h * 0.4, aw), self.line(cx, y + L * 0.86 + h * 0.4, cx + L * 0.72, y + h, aw)]
        if kind == "caron":
            return [self.line(cx - L * 0.72, y + L * 0.86 + h * 0.4, cx, y + h, aw), self.line(cx, y + h, cx + L * 0.72, y + L * 0.86 + h * 0.4, aw)]
        if kind == "dieresis":
            r = self.dr
            return [self.dot(cx - 90 - (s.w - 88) * 0.3, y + r), self.dot(cx + 90 + (s.w - 88) * 0.3, y + r)]
        if kind == "tilde":
            k = [(cx - 100, y + 30, 70), (cx - 50, y + 90, 0), (cx + 50, y + 40, 0), (cx + 100, y + 100, 70)]
            return [self.st(Path(cx - 100, y + 30).spline(k, 0.42), aw)]
        if kind == "ring":
            r = 70
            return [self.st(Path(cx + r, y + r + 10).arc(cx, y + r + 10, r, r, 0, 360, 2.0).close(), 0.75)]
        if kind == "cedilla":
            k = [(cx + 6, 6, 270), (cx + 4, -70, 300), (cx + 54, -126, 300), (cx - 50, -180, 200)]
            return [self.st(Path(cx + 6, 6).spline(k, 0.5), aw)]
        if kind == "macron":
            return [self.line(cx - 90, y + 50, cx + 90, y + 50, aw)]
        if kind == "breve":
            rx, ry = L * 0.60, L * 0.46
            cy = y + h * aw + ry
            return [self.st(Path(*_se(cx, cy, rx, ry, 180, 2.0)).arc(cx, cy, rx, ry, 180, 360, 2.0), aw)]
        if kind == "dotaccent":
            r = self.dr
            return [self.dot(cx, base_y + (44 if upper else self.dot_gap) + r)]
        if kind == "hungarumlaut":
            d = L * 0.30
            return [self.line(c - L * 0.20, y + h, c + L * 0.34, y + L * 0.92 + h * 0.4, aw) for c in (cx - d, cx + d)]
        if kind == "ogonek":
            k = [(cx, 24, 236), (cx - 42, -124, 296), (cx + 38, -184, 8)]
            return [self.st(Path(cx, 24).spline(k, 0.5), aw)]
        if kind == "commaaccent":
            r = self.dr * 0.85
            yd = -54 - r
            return [self.dot(cx, yd, 0.85), self.line(cx + 4, yd - 4, cx - 24, yd - 104, 0.78)]
        if kind == "commaturned":
            r = self.dr * 0.85
            yd = y + r
            return [self.dot(cx, yd, 0.85), self.line(cx - 4, yd + 4, cx + 24, yd + 104, 0.78)]
        if kind == "caronvertical":
            # Slovak/Czech ď ľ ť Ľ: an apostrophe right of the stem; cx is its left edge, base_y its top.
            r = self.dr * 0.85
            yd = base_y - r
            return [self.dot(cx + r, yd, 0.85), self.line(cx + r + 4, yd - 4, cx + r - 20, yd - 104, 0.78)]
        raise KeyError(kind)


def _se(cx, cy, rx, ry, a, n):
    t = math.radians(a)
    c, s = math.cos(t), math.sin(t)
    e = 2.0 / n
    return (cx + rx * math.copysign(abs(c) ** e, c), cy + ry * math.copysign(abs(s) ** e, s))


def _map_points(shape, fn, wm=1.0):
    if isinstance(shape, Dot):
        x, y = fn(shape.x, shape.y)
        return Dot(x, y, shape.r * wm)
    if isinstance(shape, Poly):
        return Poly([fn(x, y) for x, y in shape.points])
    p = Path(0, 0)
    p.points = [fn(x, y) for x, y in shape.path.points]
    p.closed = shape.path.closed
    return Stroke(p, shape.width * wm, shape.width_fn)


def _shift(shape, dx, dy=0.0):
    return _map_points(shape, lambda x, y: (x + dx, y + dy))


def _mirror(shape, cx):
    return _map_points(shape, lambda x, y: (2 * cx - x, y))


def _rotate180(shape, cx, cy):
    return _map_points(shape, lambda x, y: (2 * cx - x, 2 * cy - y))


def _scale_x(shape, sx):
    return _map_points(shape, lambda x, y: (x * sx, y))


def _center(shapes, cx, cy):
    x0, y0, x1, y1 = bounds(glyph_contours(shapes))
    return [_shift(sh, cx - (x0 + x1) / 2, cy - (y0 + y1) / 2) for sh in shapes]


def _rotate(shape, angle_deg, cx, cy):
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    return _map_points(shape, lambda x, y: (cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca))


# Character -> method name.
CHARS = {}
for c in "abcdefghijklmnopqrstuvwxyz":
    CHARS[c] = "g_" + c
for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
    CHARS[c] = "g_" + c
for c, n in zip("0123456789", ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]):
    CHARS[c] = "g_" + n
CHARS.update({
    ".": "g_period", ",": "g_comma", ":": "g_colon", ";": "g_semicolon", "!": "g_exclam", "?": "g_question",
    "¡": "g_exclamdown", "¿": "g_questiondown", "'": "g_quotesingle", '"': "g_quotedbl", "’": "g_quoteright",
    "‘": "g_quoteleft", "“": "g_quotedblleft", "”": "g_quotedblright", "‚": "g_quotesinglbase", "-": "g_hyphen",
    "–": "g_endash", "—": "g_emdash", "_": "g_underscore", "(": "g_parenleft", ")": "g_parenright", "[": "g_bracketleft",
    "]": "g_bracketright", "/": "g_slash", "\\": "g_backslash", "&": "g_ampersand", "#": "g_numbersign", "%": "g_percent",
    "+": "g_plus", "−": "g_minus", "=": "g_equal", "×": "g_multiply", "<": "g_less", ">": "g_greater", "*": "g_asterisk",
    "·": "g_periodcentered", "•": "g_bullet", "…": "g_ellipsis", "‹": "g_guilsinglleft", "›": "g_guilsinglright",
    "«": "g_guillemotleft", "»": "g_guillemotright", "°": "g_degree", "€": "g_Euro", "$": "g_dollar", "¢": "g_cent",
    "£": "g_sterling", "¥": "g_yen", "₩": "g_won", "₽": "g_ruble", "₹": "g_rupee", "₺": "g_lira", "ß": "g_germandbls",
    "ı": "g_dotless_i", "Æ": "g_AE", "æ": "g_ae", "Œ": "g_OE", "œ": "g_oe", "Ø": "g_Oslash", "ø": "g_oslash",
})

CHARS.update({
    "@": "g_at", "^": "g_asciicircum", "`": "g_grave", "~": "g_asciitilde", "{": "g_braceleft", "}": "g_braceright",
    "|": "g_bar", "¤": "g_currency", "¦": "g_brokenbar", "§": "g_section", "¨": "g_dieresis", "©": "g_copyright",
    "ª": "g_ordfeminine", "¬": "g_logicalnot", "®": "g_registered", "¯": "g_macron", "±": "g_plusminus",
    "²": "g_twosuperior", "³": "g_threesuperior", "´": "g_acute", "µ": "g_mu", "¶": "g_paragraph", "¸": "g_cedilla",
    "¹": "g_onesuperior", "º": "g_ordmasculine", "¼": "g_onequarter", "½": "g_onehalf", "¾": "g_threequarters",
    "Ð": "g_Dcroat", "÷": "g_divide", "Þ": "g_Thorn", "ð": "g_eth", "þ": "g_thorn",
    "Đ": "g_Dcroat", "đ": "g_dcroat", "Ħ": "g_Hbar", "ħ": "g_hbar", "Ĳ": "g_IJ", "ĳ": "g_ij", "ĸ": "g_kgreenlandic",
    "Ŀ": "g_Ldot", "ŀ": "g_ldot", "Ł": "g_Lslash", "ł": "g_lslash", "ŉ": "g_napostrophe", "Ŋ": "g_Eng", "ŋ": "g_eng",
    "Ŧ": "g_Tbar", "ŧ": "g_tbar", "ſ": "g_longs", "ȷ": "g_dotlessj",
    "„": "g_quotedblbase", "‰": "g_perthousand", "™": "g_trademark",
    "₫": "g_dong", "₱": "g_peso", "₪": "g_sheqel", "฿": "g_baht", "₦": "g_naira", "₴": "g_hryvnia", "₸": "g_tenge",
    "₡": "g_colonsign", "₲": "g_guarani", "₵": "g_cedi",
    "←": "g_arrowleft", "↑": "g_arrowup", "→": "g_arrowright", "↓": "g_arrowdown",
    "★": "g_blackstar", "☆": "g_whitestar", "♥": "g_heart", "✓": "g_check", "✕": "g_crossmark",
    # Private use area: pictograms for rich text in titles and rewards (<U+E000> paw, star, heart, leaf).
    "\ue000": "g_paw", "\ue001": "g_blackstar", "\ue002": "g_heart", "\ue003": "g_leaf",
})

# Combining marks (NFD) -> accent drawn by Glyphs.accent.
MARKS = {
    "̀": "grave", "́": "acute", "̂": "circumflex", "̃": "tilde", "̄": "macron",
    "̆": "breve", "̇": "dotaccent", "̈": "dieresis", "̊": "ring", "̋": "hungarumlaut",
    "̌": "caron", "̦": "commaaccent", "̧": "cedilla", "̨": "ogonek",
}
ABOVE = {"grave", "acute", "circumflex", "tilde", "macron", "breve", "dotaccent", "dieresis", "ring", "hungarumlaut",
         "caron", "commaturned"}
COMMA_BELOW = set("ĢĶĻŅŖķļņŗ")   # Latvian: the decomposition says cedilla, the letter shows a comma
VERTICAL_CARON = set("ďľťĽ")      # Czech and Slovak: the caron becomes an apostrophe


def decompose(ch):
    """Base letter and marks of an accented letter; i and j lose their dot under a mark above."""
    d = unicodedata.normalize("NFD", ch)
    if len(d) < 2 or d[0] not in CHARS or any(m not in MARKS for m in d[1:]):
        raise KeyError(f"no glyph for {ch!r} (U+{ord(ch):04X})")
    base = d[0]
    marks = [MARKS[m] for m in d[1:]]
    if ch in COMMA_BELOW:
        marks = ["commaaccent" if m == "cedilla" else m for m in marks]
    if ch == "ģ":
        marks = ["commaturned"]
    if ch in VERTICAL_CARON:
        marks = ["caronvertical"]
    if any(m in ABOVE for m in marks):
        base = {"i": "ı", "j": "ȷ"}.get(base, base)
    return base, marks


def charset():
    """Doc 05 character set: Basic Latin, Latin-1, Latin Extended-A, Romanian comma letters, typography,
    every store currency, arrows and the few symbols the UI uses. Spaces are in SPACES."""
    chars = [chr(c) for c in range(0x21, 0x7F)] + [chr(c) for c in range(0xA1, 0x100) if c != 0xAD]
    chars += [chr(c) for c in range(0x100, 0x180)] + list("ȘșȚțȷ")
    chars += list("–—‘’‚“”„•…‹›‰−€₫₱₪฿₦₴₸₡₲₵₩₽₹₺←↑→↓★☆♥✓✕™") + ["\ue000", "\ue001", "\ue002", "\ue003"]
    out = []
    for c in chars:
        if c not in out:
            out.append(c)
    return out


SPACES = {" ": 0.26, " ": 0.26, " ": 0.13, " ": 0.15}
