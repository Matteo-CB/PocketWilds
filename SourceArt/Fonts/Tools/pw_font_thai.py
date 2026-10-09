"""Thai letterforms for Pocket Round and Pocket Plush (FT), drawn as stroke skeletons in the Caline style.

Looped Thai with soft round heads. A head is part of its stroke: the pen turns once around a small circle and leaves
it tangentially, so the loop flows into the stem like handwriting. Loops are drawn a little lighter than stems.
Regular and SemiBold keep an open counter; at Bold and in Plush the tiny counter is filled and the heads read as round
balls, like the dots of i and j. Marks use a slightly lighter pen and ball heads.

Module API for the merge hook in pw_font_build.py:
    SCRIPT                   "thai"
    LANGUAGESYSTEMS          ["thai dflt"], to add to the languagesystem lines at the top of the feature file
    charset()                code points covered, in order (Thai block without U+0E3F which Latin has, the legacy
                             Thai PUA U+F700-U+F71A, U+200B)
    make(ch, style)          (shapes, left_class, right_class, upper, (x0, x1)) like Glyphs.make. Class "Z" on both
                             sides means a zero-width glyph: keep the outline where it is (no side bearing shift) and
                             write advance 0
    place(ch, style)         (contours, advance) exactly as build_style would write the glyph ("Z" handled)
    features(style)          feaLib code without languagesystem lines: GSUB ccmp (ญ ฐ lose their tail before a below
                             vowel), GPOS mark (anchors per base) and mkmk (tone marks over upper vowels)
    preshape(text, legacy)   the pre-shaping step the game runs on Thai strings for TextMeshPro (reference)
Coordinates: font units, 1000 per em, baseline y=0, ink starts near x=0.
"""
import math

import numpy as np

import pw_font_engine as E
from pw_font_engine import Dot, Path, Stroke
from pw_font_glyphs import Glyphs, Style, _shift

SCRIPT = "thai"
LANGUAGESYSTEMS = ["thai dflt"]

# ---------------------------------------------------------------------------------------------------------------
# Character sets and classes
# ---------------------------------------------------------------------------------------------------------------

THAI = [chr(c) for c in range(0x0E01, 0x0E3B)] + [chr(c) for c in range(0x0E40, 0x0E5C)]  # U+0E3F is in the Latin set
PUA = [chr(c) for c in range(0xF700, 0xF71B)]
ZW = ["​"]

ABOVE_VOWELS = "ัิีึื็ํ"     # ั ิ ี ึ ื ็ ํ
TONES = "่้๊๋์"                          # ่ ้ ๊ ๋ ์
BELOW = "ฺุู"                                      # ุ ู ฺ
OTHER_ABOVE = "๎"                                            # ๎ yamakkan
MARKS = ABOVE_VOWELS + TONES + BELOW + OTHER_ABOVE
TALL = "ปฝฟฬ"                                 # ป ฝ ฟ ฬ
TAILED = "ญฐ"                                           # ญ ฐ
DEEP = "ฎฏ"                                             # ฎ ฏ
SARA_AM = "ำ"
NIKHAHIT = "ํ"
SARA_AA = "า"

# Legacy Thai PUA layout (the one Windows and Mac Thai fonts share): positioned variants for engines without GPOS.
# kind: rd = consonant without its tail, l = shifted left, d = shifted down (tones: low), dl = low and left.
PUA_OF = {
    "": ("ฐ", "rd"), "": ("ญ", "rd"),
    "": ("ิ", "l"), "": ("ี", "l"), "": ("ึ", "l"), "": ("ื", "l"),
    "": ("่", "dl"), "": ("้", "dl"), "": ("๊", "dl"), "": ("๋", "dl"),
    "": ("์", "dl"),
    "": ("่", "d"), "": ("้", "d"), "": ("๊", "d"), "": ("๋", "d"),
    "": ("์", "d"),
    "": ("ั", "l"), "": ("ํ", "l"), "": ("็", "l"),
    "": ("่", "l"), "": ("้", "l"), "": ("๊", "l"), "": ("๋", "l"),
    "": ("์", "l"),
    "": ("ุ", "d"), "": ("ู", "d"), "": ("ฺ", "d"),
}
PUA_FOR = {(base, kind): pua for pua, (base, kind) in PUA_OF.items()}


def charset():
    return THAI + PUA + ZW


def is_mark(ch):
    return ch in MARKS


# ---------------------------------------------------------------------------------------------------------------
# Paths with loops
# ---------------------------------------------------------------------------------------------------------------

class TPath(Path):
    """Path that remembers its loops (point ranges and centers), so the stroke can be lighter there."""

    def __init__(self, x, y):
        super().__init__(x, y)
        self.loops = []

    def heading(self):
        (x0, y0), (x1, y1) = self.points[-2], self.points[-1]
        return math.degrees(math.atan2(y1 - y0, x1 - x0))

    def loop(self, rc, side, heading=None):
        """Full loop at the current point, tangent to the heading; side +1 turns left (counter-clockwise)."""
        th = self.heading() if heading is None else heading
        x, y = self.last
        nx, ny = math.cos(math.radians(th + 90 * side)), math.sin(math.radians(th + 90 * side))
        cx, cy = x + nx * rc, y + ny * rc
        a0 = th - 90 * side
        i0 = len(self.points) - 1
        self.arc(cx, cy, rc, rc, a0, a0 + 360 * side, 2.0)
        self.points[-1] = (x, y)
        self.loops.append((i0, len(self.points) - 1, cx, cy, rc))
        return self

    def around(self, cx, cy, rc, a0, a1):
        """Arc on a circle that the stroke later crosses: a loop closed by the crossing."""
        i0 = len(self.points) - 1
        self.arc(cx, cy, rc, rc, a0, a1, 2.0)
        self.loops.append((i0, len(self.points) - 1, cx, cy, rc))
        return self

    def to(self, x, y, a0, a1, tension=0.45):
        """Smooth segment from the current point (leaving at angle a0) to (x, y) (arriving at angle a1)."""
        xa, ya = self.last
        return self.spline([(xa, ya, a0), (x, y, a1)], tension)


# ---------------------------------------------------------------------------------------------------------------
# Glyph drawing
# ---------------------------------------------------------------------------------------------------------------

class ThaiGlyphs:
    def __init__(self, s: Style, loopless=False):
        from pw_font_build import side_bearing
        self.s = s
        self.g = Glyphs(s)
        self.loopless = loopless
        w = s.w
        self.w, self.h = w, w / 2
        k = s.wf
        grow = (w - 88.0) * s.grow
        self.T = round(s.xh * 1.035)
        T = self.T
        self.U = (T * 0.86 + 1.2 * grow) * k        # nominal letter width
        self.B = (T * 0.60 + 0.9 * grow) * k        # stem to stem (centerlines) of a plain letter like บ
        self.asc = T * 1.40
        self.asc_v = T * 1.55                      # top of the leading vowels โ ใ ไ
        self.desc = -T * 0.38
        self.ov = s.ov
        self.top = T + s.ov - self.h                # centerline of round tops
        self.ft = T - self.h                        # centerline of flat tops
        # heads
        self.lr = 0.64
        self.lw = w * self.lr
        if w < 125:
            self.rc = self.lw / 2 + 38.0 - (w - 92.0) * 0.15
            self.filled = False
        else:
            self.rc = (0.44 if s.inflate else 0.38) * w
            self.filled = True
        self.R = self.rc + self.lw / 2
        # marks
        self.mw = w * (0.72 if s.inflate else 0.76)
        self.mh = self.mw / 2
        self.rb = self.mw * 0.58 + 8.0                # ball head of marks
        cpt = 0.75 if s.inflate else 1.0              # display style: a more compact stack above the letters
        self.yu0 = T + 0.07 * T * cpt + 0.25 * self.mw   # bottom of upper vowels and of low tone marks
        self.rise = (0.11 * T + 0.35 * self.mw) * (0.86 if s.inflate else 1.0)  # centerline rise of the ิ arc
        self.yu1 = self.yu0 + self.rise + self.mw     # top of ิ
        self.tick = 0.07 * T
        self.gap2 = 0.045 * T
        self.tone_h = (0.19 * T + 0.75 * self.mw) * (0.88 if s.inflate else 1.0)  # ink height of ่
        self.yt_hi = self.yu1 + self.gap2             # bottom of a tone mark over ิ (default position)
        self.yl0 = -(0.05 * T + 0.2 * self.mw)        # top of below vowels
        self.low_h = 0.22 * T + 0.80 * self.mw        # ink height of ุ
        self.rsb = side_bearing(s, "S", False)

    # ---------- strokes ----------

    def S(self, path, mult=1.0, lr=None, puff=True):
        """Stroke along a TPath: loops at lr of the width, Plush swell and inflate on the open part."""
        s = self.s
        lr = self.lr if lr is None else lr
        pts = np.array(path.points, dtype=float)
        if len(pts) < 2:
            return []
        seg = np.hypot(*np.diff(pts, axis=0).T)
        cum = np.concatenate([[0.0], np.cumsum(seg)])
        L = max(cum[-1], 1.0)
        loops = [(cum[i0] / L, cum[i1] / L) for i0, i1, _, _, _ in getattr(path, "loops", [])]
        ua, ub = 0.0, 1.0
        for a, b in loops:
            if a <= 1e-6:
                ua = max(ua, b)
            if b >= 1 - 1e-6:
                ub = min(ub, a)
        if ub - ua < 0.05:
            ua, ub = 0.0, 1.0
        tr = min(1.1 * self.w * mult / L, 0.22)
        sw, inf = s.swell, (s.inflate if puff else 0.0)
        closed = path.closed

        def fn(u):
            m = 1.0
            for a, b in loops:
                if a - tr < u < b + tr:
                    d = min(u - (a - tr), (b + tr) - u) / tr
                    m = min(m, 1.0 - (1.0 - lr) * min(1.0, d))
            if (sw or inf) and not closed:
                t = min(1.0, max(0.0, (u - ua) / (ub - ua)))
                end = max(0.0, 1.0 - min(t, 1.0 - t) * 8.0)
                m *= 1.0 + sw * end + inf * math.sin(math.pi * t)
            return m

        out = [Stroke(path, self.w * mult, fn)]
        for _, _, cx, cy, rc in getattr(path, "loops", []):
            inner = rc - self.w * mult * lr / 2
            if self.filled or inner < 18:
                out.append(Dot(cx, cy, max(inner + 8, 4)))
        return out

    def P(self, x, y):
        return TPath(x, y)

    def line(self, x0, y0, x1, y1, mult=1.0):
        return self.S(self.P(x0, y0).line(x1, y1), mult)

    def ring(self, cx, cy, scale=1.0):
        """Standalone loop (closed), for heads that strokes only touch."""
        rc = self.rc * scale
        if self.loopless:
            return [Dot(cx, cy, self.h)]
        p = self.P(cx + rc, cy).arc(cx, cy, rc, rc, 0, 360, 2.0).close()
        out = [Stroke(p, self.lw)]
        inner = rc - self.lw / 2
        if self.filled or inner < 18:
            out.append(Dot(cx, cy, max(inner + 8, 4)))
        return out

    def head_at(self, cx, cy, heading, side, scale=1.0):
        """New path that starts with a loop centered at (cx, cy) and leaves it along heading (side -1: clockwise)."""
        rc = self.rc * scale
        a = math.radians(heading - 90 * side)
        x, y = cx + rc * math.cos(a), cy + rc * math.sin(a)
        p = self.P(x, y)
        if not self.loopless:
            p.loop(rc, side, heading=heading)
        else:
            p.points.append((x + 0.01 * math.cos(math.radians(heading)), y + 0.01 * math.sin(math.radians(heading))))
        return p

    def curl(self, p, side, scale=1.0):
        """Ends a path with a loop: side +1 curls to the left (counter-clockwise)."""
        if not self.loopless:
            p.loop(self.rc * scale, side)
        return p

    def lolly(self, xs, y_end, ytop=None, scale=1.0):
        """Head loop on the top-left of a stem at xs that goes down to y_end: the stem leaves the loop
        tangentially from its right side."""
        ytop = self.T + self.ov if ytop is None else ytop
        rc = self.rc * scale
        R = rc + self.lw / 2
        if self.loopless:
            return self.P(xs, ytop - self.h).line(xs, y_end)
        cy = ytop - R
        p = self.P(xs, cy)
        p.loop(rc, -1, heading=270.0)
        return p.line(xs, y_end)

    def lolly_x(self, scale=1.0):
        """Stem x of a lollipop head whose loop starts at x=0."""
        if self.loopless:
            return self.h
        rc = self.rc * scale
        return 2 * rc + self.lw / 2

    def foot_loop_left(self, p, x, y1):
        """From a stem going down at x: a loop to the left at the foot, closed when the stroke leaves to the right."""
        p.line(x, y1)
        if self.loopless:
            return p.to(x + 0.06 * self.U, self.h + 4, 270, 0, 0.5)
        return p.around(x - self.rc, y1, self.rc, 0, -270)

    def loop_right_up(self, p, xr, y1, top):
        """From a stroke coming down from the left: a loop at the foot of the stem xr, outside right, then up."""
        rc = self.rc
        cx, cy = xr + rc, y1
        if self.loopless:
            p.to(xr, self.h + 6, p.heading(), 270, 0.5)
            return self.P(xr, self.h).line(xr, top)
        a_in = 75
        ex, ey = cx + rc * math.cos(math.radians(a_in)), cy + rc * math.sin(math.radians(a_in))
        p.to(ex, ey, p.heading(), a_in - 90, 0.5)
        p.around(cx, cy, rc, a_in, -180)
        p.line(xr, top)
        return None

    def u_body(self, p, xa, xr, ytop_r, flat=0.30):
        """Continue a path from the left side at xa: down, a U bottom, up the right side at xr to ytop_r."""
        h, ov = self.h, self.ov
        bottom = h - ov * 0.5
        cy = flat * self.T + h
        rx = (xr - xa) / 2
        p.line(xa, cy)
        p.arc((xa + xr) / 2, cy, rx, cy - bottom, 180, 360, 2.6)
        p.line(xr, ytop_r)
        return p

    def arch(self, p, xl, xr, y_end, top=None):
        """From the current point going up at xl: over the top to xr and down to y_end (arch of ก ค ด ...)."""
        top = self.top if top is None else top
        y0 = p.last[1]
        cx = (xl + xr) / 2
        ry = min(top - y0, (xr - xl) * 0.52)
        yj = top - ry
        if yj > y0 + 1:
            p.line(xl, yj)
        p.arc(cx, yj, (xr - xl) / 2, ry, 180, 0, self.s.n)
        if y_end < yj - 1:
            p.line(xr, y_end)
        return p

    def arch_back(self, p, xr, xl, y_end, top=None):
        """From the current point going up at xr: over the top to the left side xl and down to y_end."""
        top = self.top if top is None else top
        y0 = p.last[1]
        cx = (xl + xr) / 2
        ry = min(top - y0, (xr - xl) * 0.52)
        yj = top - ry
        if yj > y0 + 1:
            p.line(xr, yj)
        p.arc(cx, yj, (xr - xl) / 2, ry, 0, 180, self.s.n)
        if y_end < yj - 1:
            p.line(xl, y_end)
        return p

    def nub(self, x, y):
        return self.line(x, y, x + 0.12 * self.U, y, 0.86)

    def flick(self, x, y0, long=False):
        """Rising flick at the top right: short for ช ซ, a longer diagonal for ศ ส."""
        T, U = self.T, self.U
        if long:
            p = self.P(x, y0).line(x, T * 0.80).to(x + 0.15 * U, T * 1.20, 90, 55, 0.5)
        else:
            p = self.P(x, y0).line(x, T - self.h * 0.8).to(x + 0.16 * U, T * 1.07, 90, 15, 0.5)
        return self.S(p)

    def beak(self, xl, xr, top=None):
        """Small spur at the top-left shoulder of an arch (ก ล ส)."""
        T, U = self.T, self.U
        top = self.top if top is None else top
        cx = (xl + xr) / 2
        ry = min(top - self.h, (xr - xl) * 0.52)
        ax, ay = E.superellipse(cx, top - ry, (xr - xl) / 2, ry, 150, self.s.n)
        return self.S(self.P(ax, ay).line(ax - 0.11 * U, ay - 0.07 * T), 0.94)

    def zig(self, pts):
        out = []
        for a, b in zip(pts, pts[1:]):
            out += self.line(*a, *b)
        return out

    def notch_head(self, cx, cy, xa):
        """Head of ฃ ซ ฆ ฑ: the loop, then a v-shaped notch whose second peak is at xa. Returns shapes, peak."""
        T, U, rc = self.T, self.U, self.rc
        p = self.head_at(cx, cy, 82, 1)
        p1 = (p.points[0][0] + 0.03 * U, self.top)
        v = ((p1[0] + xa) / 2, T * 0.72)
        p2 = (xa, self.top - 0.01 * T)
        p.to(*p1, 82, 88, 0.5)
        return self.S(p) + self.zig([p1, v, p2]), p2

    # ---------------------------------------------------------------------------------------------------------
    # consonants
    # ---------------------------------------------------------------------------------------------------------

    def k_kai(self):  # ก
        T, h, U, B = self.T, self.h, self.U, self.B
        xl = h + 0.10 * U
        xr = xl + B * 0.98
        p = self.P(xl, h)
        self.arch(p, xl, xr, h)
        top, cx = self.top, (xl + xr) / 2
        ry = min(top - h, (xr - xl) * 0.52)
        ax, ay = E.superellipse(cx, top - ry, (xr - xl) / 2, ry, 150, self.s.n)
        spur = self.S(self.P(ax, ay).line(ax - 0.11 * U, ay - 0.07 * T), 0.94)
        return self.S(p) + spur, "S", "S", dict(xm=xr + h)

    def khai(self, notch=False, flag=False):  # ข (ฃ notch, ช ซ flag)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        cx = R if not self.loopless else h
        shapes = []
        if notch:
            xa = cx + rc + 0.24 * U
            sh, pk = self.notch_head(cx, T * 0.60, xa)
            shapes += sh
            q = self.P(*pk)
        else:
            if self.loopless:
                xa = h
                q = self.P(h, T * 0.62)
            else:
                q = self.head_at(cx, T * 0.56, 90, 1)
                jx = q.points[0][0]
                xa = jx + 0.30 * U
                q.line(jx, T * 0.62)
                self.arch(q, jx, xa, T * 0.56)
        xr = xa + B * 0.92
        self.u_body(q, xa, xr, self.ft)
        shapes += self.S(q)
        if flag:
            shapes += self.flick(xr, T * 0.70)
        return shapes, "R", "O" if flag else "S", dict(xm=xr + h)

    def kho_khwai(self, notch=False, flick=False):  # ค (ฅ notch, ศ flick)
        T, h, U, B, R = self.T, self.h, self.U, self.B, self.R
        xl, xr = h, h + B * 1.05
        shapes = []
        if notch:
            mx = xl + 0.36 * B
            p = self.P(xl, h).line(xl, T * 0.66).to(xl + 0.18 * B, self.top, 90, 0, 0.5).to(mx, T * 0.78, 0, 270, 0.55)
            q = self.P(mx, T * 0.78).to((mx + xr) / 2, self.top, 90, 0, 0.5).to(xr, T * 0.55, 0, 270, 0.55).line(xr, h)
            shapes += self.S(p) + self.S(q)
        else:
            p = self.P(xl, h)
            self.arch(p, xl, xr, h)
            shapes += self.S(p)
        if self.loopless:
            shapes += self.S(self.P(xl, T * 0.30).to(xl + 0.16 * U, T * 0.46, 30, 90, 0.5))
        else:
            shapes += self.ring(xl + 0.7 * h + R, T * 0.40)
        if flick:
            shapes += self.flick(xr, T * 0.70, long=True)
        return shapes, "S", "O" if flick else "S", dict(xm=xr + h)

    def kho_rakhang(self):  # ฆ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        cx = R if not self.loopless else h
        xa = cx + rc + 0.24 * U
        shapes, pk = self.notch_head(cx, T * 0.62, xa)
        xs = xa - 0.03 * U
        q = self.P(*pk).to(xs, T * 0.44, 270, 268, 0.5)
        self.foot_loop_left(q, xs, R - self.ov if not self.loopless else h)
        xr = xs + B * 0.80
        q.to(xs + (xr - xs) * 0.48, T * 0.40, q.heading(), 0, 0.5).to(xr, h + 4, 0, 290, 0.5)
        shapes += self.S(q) + self.line(xr, h, xr, self.ft)
        return shapes, "R", "S", dict(xm=xr + h)

    def ngo(self):  # ง
        T, h, U, B = self.T, self.h, self.U, self.B
        xr = max(self.lolly_x() + 0.30 * B, h + 0.62 * B)
        p = self.lolly(xr, h)
        d = self.line(xr, h + 4, h, T * 0.56)
        return self.S(p) + d, "D", "S", dict(xm=xr + h)

    def cho_chan(self):  # จ
        T, h, U, B = self.T, self.h, self.U, self.B
        xl, xr = h, h + B * 1.0
        p = self.P(xl, T * 0.66).to(xl + 0.30 * B, self.top, 78, 0, 0.5).to(xr, T * 0.55, 0, 270, 0.55).line(xr, h)
        q = self.P(xr - 0.03 * U, h + 0.02 * T).to(xl + 0.48 * B, T * 0.24, 165, 160, 0.5)
        self.curl(q, -1)
        return self.S(p) + self.S(q), "O", "S", dict(xm=xr + h)

    def cho_ching(self):  # ฉ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xs = self.lolly_x(0.9)
        xm = xs + B * 0.80
        p = self.P(h, T * 0.76).to(h + 0.30 * B, self.top, 75, 0, 0.5).to(xm, T * 0.55, 0, 270, 0.55)
        p.line(xm, R - self.ov + 2)
        self.curl(p, 1)
        q = self.lolly(xs, h, ytop=T * 0.64, scale=0.9)
        hump = self.P(xs, h + 2).to(xs + (xm - xs) * 0.48, T * 0.34, 62, 0, 0.5).to(xm + rc * 0.15, h + rc * 0.75, 0, 300, 0.5)
        return self.S(p) + self.S(q) + self.S(hump), "R", "R", dict(xm=xm + h)

    def e_part(self, xm):
        """Left part shared by ฌ ญ ณ: loop at the foot, the side up with a nub, an arch over to the middle stem."""
        T, h, U, R, rc = self.T, self.h, self.U, self.R, self.rc
        xl = h
        if self.loopless:
            p = self.P(xl + 0.10 * U, h).to(xl, T * 0.20, 180, 90, 0.5)
        else:
            p = self.head_at(xl + rc, R - self.ov, 90, -1)
        p.line(xl, T * 0.50)
        self.arch(p, xl, xm, T * 0.55)
        return p, self.nub(xl, T * 0.50)

    def cho_choe(self):  # ฌ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xm = h + 2 * rc + 2 * R + 0.06 * U if not self.loopless else h + 0.62 * B
        p, nub = self.e_part(xm)
        self.foot_loop_left(p, xm, R - self.ov if not self.loopless else h)
        xr = xm + B * 0.78
        p.to(xm + (xr - xm) * 0.48, T * 0.40, p.heading(), 0, 0.5).to(xr, h + 4, 0, 290, 0.5)
        return self.S(p) + nub + self.line(xr, h, xr, self.ft), "S", "S", dict(xm=xr + h)

    def yo_ying(self, tail=True):  # ญ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xm = h + 2 * R + h + 0.07 * U if not self.loopless else h + 0.55 * B
        p, nub = self.e_part(xm)
        xr = xm + B * 0.78
        self.u_body(p, xm, xr, self.ft)
        shapes = self.S(p) + nub
        if tail:
            shapes += self.tail_ying(xm, xr)
        return shapes, "S", "S", dict(xm=xr + h, tailed=tail)

    def tail_ying(self, xm, xr):
        T, U = self.T, self.U
        q = self.head_at(xm + 0.10 * U, -0.21 * T, 300, 1, 0.70)
        q.to(xm + 0.36 * U, -0.30 * T, 300, 0, 0.5).to(xr + 0.03 * U, -0.10 * T, 0, 70, 0.5)
        return self.S(q, 0.86)

    def g_0e13(self):  # ณ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xm = h + 2 * R + h + 0.07 * U if not self.loopless else h + 0.55 * B
        p, nub = self.e_part(xm)
        xr = xm + B * 0.70
        p.line(xm, h)
        hump = self.P(xm, h + 2).to(xm + (xr - xm) * 0.45, T * 0.34, 62, 0, 0.5)
        q = self.loop_right_up(hump, xr, R - self.ov, self.ft)
        shapes = self.S(p) + nub + self.S(hump) + (self.S(q) if q else [])
        return shapes, "S", "R", dict(xm=xr + h)

    def no(self):  # น
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xs = self.lolly_x()
        xr = xs + B * 0.78
        p = self.lolly(xs, h)
        hump = self.P(xs, h + 2).to(xs + (xr - xs) * 0.45, T * 0.34, 62, 0, 0.5)
        q = self.loop_right_up(hump, xr, R - self.ov, self.ft)
        return self.S(p) + self.S(hump) + (self.S(q) if q else []), "R", "R", dict(xm=xr + h)

    def tho_thung(self, nub=True, desc=False, flick=False):  # ถ (ฤ with the descender)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xl, xr = h, h + B * 1.02
        p = self.P(xr, (self.desc + h) if desc else h)
        sc = 0.84
        self.arch_back(p, xr, xl, rc * sc + self.lw / 2 - self.ov)
        self.curl(p, 1, sc)
        shapes = self.S(p)
        if nub:
            shapes += self.line(xl, T * 0.52, xl + 0.17 * U, T * 0.52, 0.9)
        if flick:
            shapes += self.flick(xr, T * 0.70, long=True)
        return shapes, "S", "O" if flick else "S", dict(xm=xr + h, deep=desc)

    def pho_samphao(self, desc=False, under=None):  # ภ (ฦ descender, ฎ ฏ with the stroke under)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        jx = 0.94 * rc + R if not self.loopless else h
        xl = jx + 0.07 * U
        xr = xl + B * 0.90
        ybot = (self.desc * (0.82 if under else 1.0) + h) if desc else h
        p = self.P(xr, ybot)
        self.arch_back(p, xr, xl, T * 0.46)
        p.to(jx, R - self.ov - 0.34 * rc, 270, 250, 0.5)
        self.curl(p, -1)
        shapes = self.S(p) + self.nub(xl, T * 0.50)
        if under:
            yb = ybot - h
            q = self.head_at(xl + 0.10 * U, yb * 0.80, 40, 1, 0.62)
            if under == "zig":
                q.to(xl + 0.24 * U, -0.07 * T, 40, 0, 0.5)
                shapes += self.zig([(xl + 0.24 * U, -0.07 * T), (xl + 0.36 * U, yb * 0.62), (xl + 0.48 * U, -0.09 * T), (xr, ybot + 6)])
            else:
                q.to(xl + 0.36 * U, -0.07 * T, 40, 0, 0.5).to(xr, ybot + 6, 0, 300, 0.5)
            shapes += self.S(q, 0.88)
        return shapes, "R", "S", dict(xm=xr + h, deep=bool(under) or desc)

    def bo(self, tall=False, nub=False):  # บ (ป tall, ย nub)
        T, h, U, B = self.T, self.h, self.U, self.B
        xs = self.lolly_x()
        xr = xs + B * 0.88
        p = self.lolly(xs, T * 0.45)
        self.u_body(p, xs, xr, (self.asc if tall else T) - h)
        shapes = self.S(p)
        if nub:
            shapes += self.zig([(xs, T * 0.64), (xs + 0.21 * U, T * 0.48), (xs, T * 0.32)])
        info = dict(xm=xr + h, xb=xr + h)
        if tall:
            info["xm"] = xr - h - 0.12 * self.mw
        return shapes, "R", "S", info

    def g_0e29(self):  # ษ
        T, h, U, B = self.T, self.h, self.U, self.B
        shapes, lc, rc_, info = self.bo()
        xs = self.lolly_x()
        xr = xs + B * 0.88
        q = self.head_at((xs + xr) / 2 - 0.02 * U, T * 0.36, 35, 1, 0.80)
        q.to(xr + 0.14 * U, T * 0.80, 35, 70, 0.5)
        return shapes + self.S(q), lc, "O", info

    def pho(self, peak, tall=False):  # ผ พ (ฝ ฟ tall)
        T, h, U, B = self.T, self.h, self.U, self.B
        xs = self.lolly_x()
        xr = xs + B * 0.92
        p = self.lolly(xs, h)
        mid = ((xs + xr) / 2, peak)
        shapes = self.S(p) + self.line(xs, h + 2, *mid) + self.line(*mid, xr, h + 2)
        shapes += self.line(xr, h, xr, (self.asc if tall else T) - h)
        info = dict(xm=xr + h, xb=xr + h)
        if tall:
            info["xm"] = xr - h - 0.12 * self.mw
        return shapes, "R", "S", info

    def lo_chula(self):  # ฬ
        T, h, U, B = self.T, self.h, self.U, self.B
        xs = self.lolly_x()
        xr = xs + B * 0.92
        p = self.lolly(xs, h)
        mid = ((xs + xr) / 2, self.ft)
        sc = 0.80
        rc = self.rc * sc
        shapes = self.S(p) + self.line(xs, h + 2, *mid) + self.line(*mid, xr, h + 2)
        shapes += self.S(self.lolly(xr, h, ytop=self.asc + self.ov, scale=sc))
        if not self.loopless:
            cx, cy = xr - 0.55 * rc, self.asc + self.ov - rc - self.lw / 2
            a = math.radians(70)
            shapes += self.S(self.P(cx + rc * math.cos(a), cy + rc * math.sin(a)).to(xr + 0.16 * U, self.asc + 0.03 * T, -20, 55, 0.5), 0.9)
            xm = cx - rc - self.lw / 2 - 0.10 * self.mw
        else:
            xm = xr - h - 0.12 * self.mw
        return shapes, "R", "O", dict(xm=xm, xb=xr + h)

    def mo(self):  # ม
        T, h, U, B, R = self.T, self.h, self.U, self.B, self.R
        xs = self.lolly_x()
        xr = xs + B * 0.86
        p = self.lolly(xs, T * 0.30)
        self.foot_loop_left(p, xs, R - self.ov if not self.loopless else h)
        p.to(xs + (xr - xs) * 0.48, T * 0.40, p.heading(), 0, 0.5).to(xr, h + 4, 0, 290, 0.5)
        return self.S(p) + self.line(xr, h, xr, self.ft), "R", "S", dict(xm=xr + h)

    def tho_thahan(self, notch=False):  # ท (ฑ notch)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        shapes = []
        if notch:
            cx = R if not self.loopless else h
            pk_x = cx + rc + 0.20 * U
            sh, pk = self.notch_head(cx, T * 0.58, pk_x)
            xf = pk_x + 0.05 * U
            shapes += sh + self.line(*pk, xf, h)
        else:
            xf = self.lolly_x()
            shapes += self.S(self.lolly(xf, h))
        xr = xf + B * 0.84
        d = self.P(xf, h + 2).line(xr - 0.18 * B, T * 0.82).to(xr, T * 0.60, 60, 270, 0.55).line(xr, h)
        return shapes + self.S(d), "R", "S", dict(xm=xr + h)

    def do(self, notch=False):  # ด (ต notch)
        T, h, U, B, R = self.T, self.h, self.U, self.B, self.R
        xl, xr = h, h + B * 1.04
        p = self.P(xr, h)
        shapes = []
        if notch:
            mx = xl + 0.40 * B
            p.line(xr, T * 0.55).to((mx + xr) / 2, self.top, 90, 180, 0.5).to(mx, T * 0.76, 180, 270, 0.55)
            shapes += self.S(p)
            p = self.P(mx, T * 0.76).to((xl + mx) / 2, self.top, 90, 180, 0.5).to(xl, T * 0.55, 180, 270, 0.55)
        else:
            self.arch_back(p, xr, xl, T * 0.50)
        p.line(xl, T * 0.36)
        self.spiral(p, xl, xl + 0.50 * B, T * 0.45)
        return shapes + self.S(p), "S", "S", dict(xm=xr + h)

    def pho_thao(self):  # ฒ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xl = h
        xm = xl + B * 1.26
        mx = xl + 0.46 * B
        p = self.P(xm, T * 0.55).to((mx + xm) / 2, self.top, 90, 180, 0.5).to(mx, T * 0.76, 180, 270, 0.55)
        q = self.P(mx, T * 0.76).to((xl + mx) / 2, self.top, 90, 180, 0.5).to(xl, T * 0.55, 180, 270, 0.55)
        q.line(xl, T * 0.36)
        self.spiral(q, xl, xl + 0.46 * B, T * 0.44, 0.92)
        r = self.P(xm, T * 0.56)
        self.foot_loop_left(r, xm, R - self.ov if not self.loopless else h)
        xr = xm + B * 0.78
        r.to(xm + (xr - xm) * 0.48, T * 0.40, r.heading(), 0, 0.5).to(xr, h + 4, 0, 290, 0.5)
        shapes = self.S(p) + self.S(q) + self.S(r) + self.line(xr, h, xr, self.ft)
        return shapes, "S", "S", dict(xm=xr + h)

    def tho_than(self, tail=True):  # ฐ
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xl, xr = h, h + B * 0.96
        sc = 0.82
        top = self.P(xr + 0.03 * U, T * 0.86).to(xr - 0.08 * B, self.ft, 105, 180, 0.5).line(xl + 0.16 * B, self.ft)
        top.to(xl, T * 0.80, 180, 270, 0.5).line(xl, T * 0.74).to(xl + 0.15 * U, T * 0.62, 270, 0, 0.5)
        if self.loopless:
            bowl = self.P(xl + 0.08 * U, T * 0.42).to(xl, T * 0.30, 200, 270, 0.5)
        else:
            bowl = self.head_at(xl + rc * sc, T * 0.32, 270, 1, sc)
        self.u_body(bowl, xl, xr, T * 0.56, flat=0.24)
        shapes = self.S(top) + self.S(bowl)
        if tail:
            shapes += self.tail_than(xl, xr)
        return shapes, "S", "S", dict(xm=xr + h, tailed=tail)

    def tail_than(self, xl, xr):
        T, U = self.T, self.U
        W = xr - xl
        q = self.head_at(xl + 0.16 * W, -0.21 * T, 300, 1, 0.66)
        q.to(xl + 0.38 * W, -0.10 * T, 330, 0, 0.5).to(xl + 0.58 * W, -0.27 * T, 300, 0, 0.5).to(xl + 0.78 * W, -0.10 * T, 30, 60, 0.5)
        return self.S(q, 0.86) + self.line(xr, -0.08 * T, xr, -0.27 * T, 0.86)

    def ro(self):  # ร
        T, h, U, B = self.T, self.h, self.U, self.B
        xl, xr = h, h + B * 0.86
        p = self.P(xr + 0.02 * U, self.ft).line(xl + 0.12 * B, self.ft).to(xl, T * 0.80, 180, 270, 0.5).line(xl, T * 0.64)
        p.spline([(xl, T * 0.64, 270), (xl + 0.48 * B, T * 0.50, 0), (xr, T * 0.30, 270)], 0.5)
        p.to((xl + xr) / 2 + 0.06 * B, self.R - self.ov - self.rc, 270, 180, 0.5)
        self.curl(p, -1)
        return self.S(p), "S", "S", dict(xm=xr + h)

    def wo(self):  # ว
        T, h, U, B = self.T, self.h, self.U, self.B
        xl, xr = h, h + B * 0.80
        p = self.P(xl, T * 0.68).to((xl + xr) / 2, self.top, 80, 0, 0.5).to(xr, T * 0.50, 0, 270, 0.55).line(xr, T * 0.34)
        p.to((xl + xr) / 2 + 0.06 * B, self.R - self.ov - self.rc, 270, 180, 0.5)
        self.curl(p, -1)
        return self.S(p), "O", "S", dict(xm=xr + h)

    def ho_hip(self):  # ห
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xs = self.lolly_x()
        xr = xs + B * 0.90
        p = self.lolly(xs, h)
        if self.loopless:
            return self.S(p) + self.line(xs, h + 2, xr, self.ft) + self.line(xr, self.ft, xr, h), "R", "S", dict(xm=xr + h)
        cx, cy = xr - rc, T + self.ov - R
        a = math.radians(145 - 180)
        jx, jy = cx + rc * math.cos(a), cy + rc * math.sin(a)
        d = self.P(xs, h + 2).to(jx, jy, 58, 55, 0.5)
        d.loop(rc, 1)
        stem = self.P(xr, cy).line(xr, h)
        return self.S(p) + self.S(d) + self.S(stem), "R", "S", dict(xm=xr + h)

    def o_ang(self, lid=False):  # อ (ฮ lid)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xl, xr = h, h + B * 1.0
        W = xr + h
        shapes = []
        if lid:
            m = 0.80
            hh = h * m
            ry = 0.14 * T
            cyl = T + self.ov - hh - ry
            lid_p = self.P(xr, cyl).arc(W / 2, cyl, xr - W / 2, ry, 0, 360, 2.0).close()
            shapes += [Stroke(lid_p, self.w * m)]
            shapes += self.S(self.P(xr - 0.01 * U, cyl + ry * 0.4).to(xr + 0.11 * U, T * 1.10, 50, 70, 0.5), m)
            p = self.P(xr, cyl).line(xr, T * 0.42)
        else:
            p = self.P(xl + 0.02 * U, T * 0.76).to(W / 2, self.top, 75, 0, 0.5).to(xr, T * 0.45, 0, 270, 0.55)
        p.spline([(p.last[0], p.last[1], 270), (W / 2, h - self.ov, 180), (xl, T * 0.30, 90)], 0.52)
        p.line(xl, T * 0.40)
        self.curl(p, -1, 0.92)
        return shapes + self.S(p), "R", "R", dict(xm=xr + h)

    def tho_thong(self):  # ธ
        """Top bar with a small drop at its right end, the left side down to mid height, a middle bar across, the
        right side down, the bottom bar back, and the lower left side up again, stopping short of the middle bar."""
        T, h, U, B = self.T, self.h, self.U, self.B
        xl, xr = h, h + B * 0.96
        r = min(0.16 * B + h, T * 0.20)
        ym = T * 0.56
        p = self.P(xr + 0.01 * U, T * 0.84).to(xr - 0.12 * B, self.ft, 95, 180, 0.5).line(xl + r, self.ft)
        p.to(xl, self.ft - r, 180, 270, 0.55).line(xl, ym + r * 0.9).to(xl + r, ym, 270, 0, 0.55)
        p.line(xr - r, ym).to(xr, ym - r, 0, 270, 0.55).line(xr, h + r * 0.8).to(xr - r * 0.8, h - self.ov * 0.5, 270, 180, 0.55)
        p.line(xl + r * 0.8, h - self.ov * 0.5).to(xl, h + r * 0.8, 180, 90, 0.55).line(xl, T * 0.30)
        return self.S(p), "S", "S", dict(xm=xr + h)

    def spiral(self, p, xl, cx, cy, scale=1.0):
        """From the left side going down at xl: round the bottom corner and rise into a loop centered at (cx, cy)
        (the loop of ด ต ฒ, in the middle of the letter)."""
        rc = self.rc * scale
        h, ov = self.h, self.ov
        jx, jy = cx + 0.866 * rc, cy - 0.5 * rc
        y0 = p.last[1]
        p.spline([(xl, y0, 270), (xl + (jx - xl) * 0.40, h - ov * 0.5, 0), (jx, jy, 60)], 0.5)
        if not self.loopless:
            p.loop(rc, 1)
        return p

    def lo_ling(self, flick=False):  # ล (ส with the flick)
        """Arch, a straight left side, a small loop tucked into the bottom-left corner and a small beak at the
        top-left shoulder (ด has its loop high in the middle, ถ a nub, ภ an S into an outside loop)."""
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        xl, xr = h + 0.06 * U, h + 0.06 * U + B * 0.98
        sc = 0.74
        p = self.P(xr, h)
        self.arch_back(p, xr, xl, rc * sc + self.lw / 2 - self.ov)
        self.curl(p, 1, sc)
        shapes = self.S(p)
        top, cx = self.top, (xl + xr) / 2
        ry = min(top - h, (xr - xl) * 0.52)
        ax, ay = E.superellipse(cx, top - ry, (xr - xl) / 2, ry, 155, self.s.n)
        shapes += self.S(self.P(ax, ay).line(ax - 0.08 * U, ay - 0.06 * T), 0.92)
        if flick:
            shapes += self.flick(xr, T * 0.70, long=True)
        return shapes, "S", "O" if flick else "S", dict(xm=xr + h)

    def paiyan(self, desc=False, notch=False):  # ฯ (ๆ: descender and notch)
        T, h, U, B, R, rc = self.T, self.h, self.U, self.B, self.R, self.rc
        cx = R if not self.loopless else h
        xr = cx + rc + 0.40 * B
        shapes = []
        if notch:
            sh, pk = self.notch_head(cx, T * 0.42, xr - 0.10 * B)
            shapes += sh + self.S(self.P(*pk).to(xr, T * 0.64, 0, 270, 0.5).line(xr, self.desc + h))
        else:
            p = self.head_at(cx, T * 0.42, 70, -1)
            p.to(xr - 0.16 * B, self.top, 70, 0, 0.5).to(xr, T * 0.55, 0, 270, 0.55).line(xr, (self.desc + h) if desc else h)
            shapes += self.S(p)
        return shapes, "R", "S", {}

    def sara_a(self):  # ะ
        T, h, U = self.T, self.h, self.U
        shapes = []
        r = self.rb * 1.05
        for yb in (0.0, T * 0.52):
            bx, by = r, yb + r + 0.02 * T
            p = self.P(bx + r * 0.6, by - r * 0.7).to(bx + 0.20 * U, yb + h * 0.86, 330, 0, 0.5).to(bx + 0.36 * U, yb + 0.30 * T, 0, 85, 0.5)
            shapes += [Dot(bx, by, r)] + self.S(p, 0.86)
        return shapes, "R", "O", {}

    def sara_aa(self, desc=False):  # า (ๅ descender)
        T, h, U, B = self.T, self.h, self.U, self.B
        xl, xr = h, h + B * 0.66
        p = self.P(xl, T * 0.66).to((xl + xr) / 2, self.top, 78, 0, 0.5).to(xr, T * 0.52, 0, 270, 0.55)
        p.line(xr, (self.desc + h) if desc else h)
        return self.S(p), "O", "S", {}

    def sara_e(self, n=1):  # เ แ
        T, h, U = self.T, self.h, self.U
        rc = self.rc * 0.92
        R = rc + self.lw / 2
        shapes = []
        step = 2 * R + 0.10 * U
        for i in range(n):
            xs = h + i * step
            p = self.P(xs, self.ft).line(xs, R - self.ov)
            self.curl(p, 1, 0.92)
            shapes += self.S(p)
        return shapes, "S", "R", {}

    def sara_o(self, kind):  # โ ใ ไ
        T, h, U, B = self.T, self.h, self.U, self.B
        sc = 0.92
        rc = self.rc * sc
        R = rc + self.lw / 2
        top = self.asc_v - h
        shapes = []
        if kind == "o":
            xs = h + 0.13 * U
            shapes += self.S(self.P(h, top - 0.20 * T).to(h + 0.09 * U, top, 80, 0, 0.5).line(xs + 0.30 * U, top))
            p = self.P(xs, top)
        elif kind == "maimuan":
            cx = self.R * 0.9 if not self.loopless else h
            xs = cx + rc + 0.24 * U
            q = self.head_at(cx, top + h - self.R * 0.95, 60, -1, 0.9)
            q.to(xs - 0.12 * U, self.asc_v + self.ov - h, 60, 0, 0.5).to(xs, top - 0.20 * T, 0, 270, 0.55)
            shapes += self.S(q)
            p = self.P(xs, top - 0.20 * T)
        else:
            xs = h + 0.30 * U
            shapes += self.zig([(h, top), (h + 0.13 * U, top - 0.26 * T), (xs, top)])
            p = self.P(xs, top)
        p.line(xs, R - self.ov)
        self.curl(p, 1, sc)
        shapes += self.S(p)
        return shapes, "O", "R", {}

    def fongman(self):  # ๏
        T, h = self.T, self.h
        D = T * 0.90
        ring = self.P(D - h * 0.86, D / 2).arc(D / 2, D / 2, D / 2 - h * 0.86, D / 2 - h * 0.86, 0, 360, 2.0).close()
        return [Stroke(ring, self.w * 0.86), Dot(D / 2, D / 2, self.w * 0.60)], "R", "R", {}

    def angkhankhu(self):  # ๚
        shapes, lc, rc, info = self.paiyan()
        T, h, U, B, R = self.T, self.h, self.U, self.B, self.R
        x = (R if not self.loopless else h) + self.rc + 0.40 * B + 0.24 * U
        return shapes + self.line(x, h, x, self.ft + 0.04 * T), lc, "S", {}

    def khomut(self):  # ๛
        T, h, U, ov = self.T, self.h, self.U, self.ov
        W = U * 0.62
        p = self.P(W - h, T * 0.80)
        p.spline([(W - h, T * 0.80, 130), (W * 0.50, self.top, 180), (h, T * 0.48, 270), (W * 0.52, h - ov, 0),
                  (W * 0.86, T * 0.22, 60)], 0.5)
        p.to(W * 1.06, T * 0.44, 60, 0, 0.5).to(W * 1.24, T * 0.22, 0, 270, 0.5).to(W * 1.30, T * 0.10, 270, 270, 0.5)
        q = self.P(W * 1.24, T * 0.26).to(W * 1.44, T * 0.44, 60, 0, 0.5).to(W * 1.62, T * 0.22, 0, 290, 0.5)
        return self.S(p) + self.S(q), "R", "O", {}

    def digit(self, d):
        T, h, U, B, ov = self.T, self.h, self.U, self.B, self.ov
        DH = T * 0.88                      # digit height
        dtop = DH + ov - h
        W = B * 1.02 + 2 * h
        xl, xr = h, W - h
        hi = T * 1.18 - h
        sc = 0.80
        if d == 0:
            return [self.g.bowl(0, W * 1.02, -ov, DH + ov)], "R", "R", {}
        if d == 1:
            p = self.head_at(W * 0.46, DH * 0.40, 200, 1, sc) if not self.loopless else self.P(W * 0.40, DH * 0.40)
            p.to(xl, DH * 0.42, 200, 90, 0.5)
            p.spline([(xl, DH * 0.42, 90), (W / 2, dtop, 0), (xr, DH * 0.42, 270), (xr - 0.20 * B, h - ov + 2, 220)], 0.5)
            return self.S(p), "R", "R", {}
        if d == 2:
            p = self.P(xl, hi).line(xl, DH * 0.32)
            p.arc(W / 2, DH * 0.32, (xr - xl) / 2, DH * 0.32 - h + ov, 180, 360, 2.4)
            p.line(xr, DH * 0.52).to(W * 0.58, DH * 0.70, 90, 180, 0.55).to(W * 0.40, DH * 0.44, 180, 270, 0.55)
            self.curl(p, 1, sc)
            return self.S(p), "S", "R", {}
        if d in (3, 7):
            p = self.head_at(W * 0.34, DH * 0.28, 210, 1, sc) if not self.loopless else self.P(W * 0.30, DH * 0.30)
            p.to(xl, DH * 0.42, 210, 90, 0.5).to(W * 0.28, dtop, 90, 0, 0.5).to(W * 0.50, DH * 0.62, 0, 270, 0.5)
            if d == 3:
                q = self.P(W * 0.50, DH * 0.62).to(W * 0.75, dtop, 90, 0, 0.5).to(xr + 0.04 * B, DH * 0.40, 0, 270, 0.5)
                q.to(xr - 0.14 * B, h - ov + 2, 270, 215, 0.5)
                return self.S(p) + self.S(q), "R", "R", {}
            q = self.P(W * 0.50, DH * 0.62).to(W * 0.68, DH * 0.82, 90, 0, 0.5).to(W * 0.84, DH * 0.44, 0, 270, 0.5)
            q.line(W * 0.84, DH * 0.24).to(W * 1.00, h - ov, 270, 0, 0.5).to(W * 1.14, DH * 0.40, 0, 90, 0.5).line(W * 1.14, hi)
            return self.S(p) + self.S(q), "R", "S", {}
        if d in (4, 5):
            p = self.P(xr + 0.04 * B, hi).to(xl, DH * 0.44, 255, 270, 0.65)
            p.to(W * 0.42, h - ov, 270, 0, 0.5).to(xr + 0.12 * B, h + 4, 0, 10, 0.5)
            q = self.P(W * 0.42, h + 8).to(W * 0.62, DH * 0.36, 70, 90, 0.5)
            self.curl(q, 1, sc)
            shapes = self.S(p) + self.S(q)
            if d == 5:
                shapes += self.ring(W * 0.30, DH * 0.88, 0.62)
            return shapes, "R", "R", {}
        if d == 6:
            p = self.P(xl, hi).to(W * 0.44, DH * 0.62, 295, 300, 0.5)
            p.spline([(W * 0.44, DH * 0.62, 300), (xr, DH * 0.34, 270), (W / 2, h - ov, 180), (xl + 0.04 * B, DH * 0.30, 90)], 0.5)
            p.to(W * 0.42, DH * 0.42, 90, 330, 0.6)
            self.curl(p, -1, sc)
            return self.S(p), "R", "R", {}
        if d == 8:
            p = self.P(xr + 0.04 * B, hi).to(xl, DH * 0.44, 255, 270, 0.65)
            p.to(W * 0.30, h - ov, 270, 0, 0.5).to(W * 0.52, DH * 0.30, 60, 0, 0.5).to(W * 0.80, h + 4, 300, 0, 0.5)
            p.to(xr + 0.06 * B, DH * 0.36, 30, 120, 0.5)
            self.curl(p, 1, sc)
            return self.S(p), "R", "R", {}
        if d == 9:
            p = self.head_at(W * 0.36, DH * 0.28, 210, 1, sc) if not self.loopless else self.P(W * 0.32, DH * 0.30)
            p.to(xl, DH * 0.42, 210, 90, 0.5).to(W * 0.36, dtop, 90, 0, 0.5).to(W * 0.60, DH * 0.60, 0, 290, 0.5)
            p.line(W * 0.86, h)
            q = self.P(W * 0.64, DH * 0.66).to(W * 0.80, DH * 0.96, 60, 0, 0.5).to(W * 0.96, DH * 0.80, 300, 0, 0.5)
            q.to(xr + 0.12 * B, hi - 0.06 * T, 60, 80, 0.5)
            return self.S(p) + self.S(q), "R", "R", {}
        raise KeyError(d)

    # ---------------------------------------------------------------------------------------------------------
    # marks: right edge at x=0, bottom (top for below vowels) at y=0; mark_shapes places them
    # ---------------------------------------------------------------------------------------------------------

    def MS(self, p, m=1.0):
        return self.S(p, self.mw / self.w * m, lr=1.0, puff=False)

    def mark_parts(self, ch):
        T, U, mw, mh, rb = self.T, self.U, self.mw, self.mh, self.rb
        if ch in "ิีึื":  # ิ ี ึ ื
            Wv = U * 0.70
            x0, x1 = -Wv + mh, -mh
            arc = self.P(x0, mh).spline([(x0, mh, 60), (x0 + (x1 - x0) * 0.55, mh + self.rise, 0),
                                         (x1, mh + self.rise * 0.30, 300)], 0.5)
            shapes = self.MS(arc)
            top = mw + self.rise
            tone = (0.0, top + self.gap2)
            if ch in "ีื":
                shapes += self.MS(self.P(x1, mh + self.rise * 0.30).line(x1, top + self.tick - mh))
                tone = (-mw - 0.04 * U, top + self.gap2)
            if ch == "ื":
                xm = x1 - mw - 0.07 * U
                shapes += self.MS(self.P(xm, mh + self.rise * 0.95).line(xm, top + self.tick - mh))
                tone = (-2 * mw - 0.11 * U, top + self.gap2)
            if ch == "ึ":
                shapes += [Dot(x1 - rb * 0.15, top + self.tick * 0.35, rb * 0.92)]
                tone = (-2 * rb - 0.03 * U, top + self.gap2)
            return shapes, dict(kind="above", top=top, tone=tone)
        if ch == "ั":  # ั
            Wv = U * 0.60
            bx = -Wv + rb
            p = self.P(bx + rb * 0.4, rb * 0.55).to(-0.16 * U, mh, 340, 0, 0.5).to(-mh, mh + self.rise * 1.1, 10, 80, 0.5)
            top = mw + self.rise * 1.1
            return [Dot(bx, rb, rb)] + self.MS(p), dict(kind="above", top=top, tone=(0.0, top + self.gap2))
        if ch == "็":  # ็ (a small 8 on its side: a ball, a wave, a ball; ั has one ball and a rising tail)
            Wv = U * 0.58
            H = mw + self.rise * 1.15
            b1 = (-Wv + rb, H * 0.42)
            b2 = (-rb * 0.95, H * 0.58)
            p = self.P(b1[0] + rb * 0.4, b1[1] - rb * 0.4).to(-Wv * 0.52, mh, 330, 0, 0.5).to(-Wv * 0.34, H * 0.46, 20, 60, 0.5)
            p.to(b2[0] - rb * 0.5, b2[1] + rb * 0.2, 60, 10, 0.5)
            return self.MS(p) + [Dot(*b1, rb), Dot(*b2, rb * 0.92)], dict(kind="above", top=H, tone=(0.0, H + self.gap2))
        if ch == "ํ":  # ํ
            r = rb * 1.25
            rcn = r - mh * 0.62
            ring = self.P(-r + rcn, r).arc(-r, r, rcn, rcn, 0, 360, 2.0).close()
            return [Stroke(ring, mw * 0.62)], dict(kind="above", top=2 * r, tone=(0.0, 2 * r + self.gap2))
        if ch == "๎":  # ๎
            Wv = U * 0.28
            p = self.P(-mh, mh + self.rise * 1.25).to(-Wv, mh + self.rise * 0.85, 180, 270, 0.5)
            p.to(-Wv * 0.45, mh + self.rise * 0.55, 0, 0, 0.4).to(-Wv, mh + self.rise * 0.20, 180, 270, 0.4).to(-mh, mh, 300, 0, 0.5)
            top = mw + self.rise * 1.25
            return self.MS(p), dict(kind="above", top=top, tone=(0.0, top + self.gap2))
        th = self.tone_h
        if ch == "่":  # ่
            return self.MS(self.P(-mh, mh).line(-mh, th - mh)), dict(kind="tone")
        if ch == "้":  # ้
            Wv = U * 0.46
            bx = -Wv + rb
            p = self.P(bx + rb * 0.4, th * 0.72 - rb * 0.6).to(bx + 0.08 * U, mh, 290, 0, 0.5).to(-mh, th - mh, 10, 85, 0.5)
            return [Dot(bx, th * 0.72, rb)] + self.MS(p), dict(kind="tone")
        if ch == "๊":  # ๊
            Wv = U * 0.60
            bx = -Wv + rb
            p = self.P(bx + rb * 0.4, th * 0.70 - rb * 0.6).to(bx + 0.07 * U, mh, 290, 0, 0.5).to(-Wv * 0.42, th * 0.68, 50, 50, 0.5)
            p.to(-Wv * 0.26, mh, 310, 0, 0.5).to(-mh, th - mh, 20, 85, 0.5)
            return [Dot(bx, th * 0.70, rb)] + self.MS(p), dict(kind="tone")
        if ch == "๋":  # ๋
            a = min(U * 0.15, th * 0.33)
            cx, cy = -a - mh, th * 0.5
            return self.MS(self.P(cx - a, cy).line(cx + a, cy)) + self.MS(self.P(cx, cy - a).line(cx, cy + a)), dict(kind="tone")
        if ch == "์":  # ์ (ball, then a hook that rises and curls back, taller and narrower than ั)
            Wv = U * 0.46
            bx = -Wv + rb
            p = self.P(bx + rb * 0.5, rb * 0.6).to(-0.15 * U, mh + 0.01 * T, 345, 10, 0.5).to(-mh, th * 0.60, 10, 90, 0.5)
            p.to(-mh - 0.12 * U, th - mh, 90, 175, 0.5)
            return [Dot(bx, rb, rb)] + self.MS(p), dict(kind="tone")
        if ch == "ุ":  # ุ
            hl = self.low_h
            bx = -0.27 * U
            p = self.P(bx + rb * 0.5, -rb * 0.5).to(-mh, -rb * 1.3, 10, 270, 0.5).line(-mh, -hl + mh)
            return [Dot(bx, -rb, rb)] + self.MS(p), dict(kind="below", bottom=-hl)
        if ch == "ู":  # ู
            hl = self.low_h
            Wv = U * 0.48
            bx = -Wv + rb
            xs = bx + rb * 0.55
            p = self.P(xs, -rb * 1.2).line(xs, -hl * 0.55)
            p.arc((xs - mh) / 2, -hl * 0.55, (-mh - xs) / 2, hl * 0.45 - mh, 180, 360, 2.4).line(-mh, -mh)
            return [Dot(bx, -rb, rb)] + self.MS(p), dict(kind="below", bottom=-hl)
        if ch == "ฺ":  # ฺ
            r = rb
            return [Dot(-r - 0.04 * U, -r - 0.02 * T, r)], dict(kind="below", bottom=-2 * r - 0.02 * T)
        raise KeyError(ch)


# ---------------------------------------------------------------------------------------------------------------
# Character table
# ---------------------------------------------------------------------------------------------------------------

def _table(tg):
    T, h = tg.T, tg.h
    t = {
        "ก": tg.k_kai,
        "ข": lambda: tg.khai(),
        "ฃ": lambda: tg.khai(notch=True),
        "ค": lambda: tg.kho_khwai(),
        "ฅ": lambda: tg.kho_khwai(notch=True),
        "ฆ": tg.kho_rakhang,
        "ง": tg.ngo,
        "จ": tg.cho_chan,
        "ฉ": tg.cho_ching,
        "ช": lambda: tg.khai(flag=True),
        "ซ": lambda: tg.khai(notch=True, flag=True),
        "ฌ": tg.cho_choe,
        "ญ": lambda: tg.yo_ying(tail=True),
        "ฎ": lambda: tg.pho_samphao(desc=True, under="peak"),
        "ฏ": lambda: tg.pho_samphao(desc=True, under="zig"),
        "ฐ": lambda: tg.tho_than(tail=True),
        "ฑ": lambda: tg.tho_thahan(notch=True),
        "ฒ": tg.pho_thao,
        "ณ": tg.g_0e13,
        "ด": lambda: tg.do(),
        "ต": lambda: tg.do(notch=True),
        "ถ": lambda: tg.tho_thung(nub=True),
        "ท": lambda: tg.tho_thahan(),
        "ธ": tg.tho_thong,
        "น": tg.no,
        "บ": lambda: tg.bo(),
        "ป": lambda: tg.bo(tall=True),
        "ผ": lambda: tg.pho(T * 0.46),
        "ฝ": lambda: tg.pho(T * 0.46, tall=True),
        "พ": lambda: tg.pho(tg.ft),
        "ฟ": lambda: tg.pho(tg.ft, tall=True),
        "ภ": lambda: tg.pho_samphao(),
        "ม": tg.mo,
        "ย": lambda: tg.bo(nub=True),
        "ร": tg.ro,
        "ฤ": lambda: tg.tho_thung(nub=True, desc=True),
        "ล": lambda: tg.lo_ling(),
        "ฦ": lambda: tg.pho_samphao(desc=True),
        "ว": tg.wo,
        "ศ": lambda: tg.kho_khwai(flick=True),
        "ษ": tg.g_0e29,
        "ส": lambda: tg.lo_ling(flick=True),
        "ห": tg.ho_hip,
        "ฬ": tg.lo_chula,
        "อ": lambda: tg.o_ang(),
        "ฮ": lambda: tg.o_ang(lid=True),
        "ฯ": lambda: tg.paiyan(),
        "ะ": tg.sara_a,
        "า": lambda: tg.sara_aa(),
        "เ": lambda: tg.sara_e(1),
        "แ": lambda: tg.sara_e(2),
        "โ": lambda: tg.sara_o("o"),
        "ใ": lambda: tg.sara_o("maimuan"),
        "ไ": lambda: tg.sara_o("maimalai"),
        "ๅ": lambda: tg.sara_aa(desc=True),
        "ๆ": lambda: tg.paiyan(desc=True, notch=True),
        "๏": tg.fongman,
        "๚": tg.angkhankhu,
        "๛": tg.khomut,
    }
    for i in range(10):
        t[chr(0x0E50 + i)] = (lambda d: (lambda: tg.digit(d)))(i)
    return t


# ---------------------------------------------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------------------------------------------

_CACHE = {}


def _tg(style, loopless=False):
    key = (style.key, style.weight_name, style.w, style.xh, style.wf, style.inflate, style.swell, loopless)
    if key not in _CACHE:
        _CACHE[key] = ThaiGlyphs(style, loopless)
    return _CACHE[key]


def tall_shift(tg):
    """How far marks move left over ป ฝ ฟ (legacy PUA variants; the GPOS anchors give the same position)."""
    return tg.w + 0.12 * tg.mw


def deep_drop(tg):
    """How far below vowels go down under ฎ ฏ (and under ญ ฐ when they keep their tail)."""
    return tg.desc * 0.82 - 0.02 * tg.T


def mark_shapes(ch, style, loopless=False, kind=""):
    """Mark in its default position after a consonant whose right edge is at x = -rsb, or a PUA variant
    position (kind l, d, dl). Tone marks default to the high position (over an upper vowel)."""
    tg = _tg(style, loopless)
    parts, info = tg.mark_parts(ch)
    info = dict(info)
    xr = -tg.rsb
    if info["kind"] == "above":
        dx, dy = xr, tg.yu0
    elif info["kind"] == "tone":
        dx, dy = xr - 0.08 * tg.U, tg.yt_hi
    else:
        dx, dy = xr - 0.03 * tg.U, tg.yl0
    if "l" in kind:
        dx -= tall_shift(tg)
    if "d" in kind:
        dy = tg.yu0 if info["kind"] == "tone" else dy + deep_drop(tg)
    return [_shift(sh, dx, dy) for sh in parts], info


def glyph(ch, style, loopless=False):
    """(shapes, left class, right class, info) before spacing; info["xm"] is where marks align (right edge)."""
    tg = _tg(style, loopless)
    if ch in ZW:
        return [], "Z", "Z", {}
    if ch in PUA_OF:
        base, kind = PUA_OF[ch]
        if kind == "rd":
            return tg.yo_ying(tail=False) if base == "ญ" else tg.tho_than(tail=False)
        shapes, info = mark_shapes(base, style, loopless, kind)
        return shapes, "Z", "Z", info
    if ch in MARKS:
        shapes, info = mark_shapes(ch, style, loopless)
        return shapes, "Z", "Z", info
    if ch == SARA_AM:
        return _sara_am(tg, style, loopless)
    return _table(tg)[ch]()


def _sara_am(tg, style, loopless):
    """ำ: sara aa plus the nikhahit over the consonant before (where it sits after a plain consonant)."""
    from pw_font_build import side_bearing
    shapes, lc, rc, info = tg.sara_aa()
    x0, _, _, _ = Glyphs.box(shapes)
    nik, _ = mark_shapes(NIKHAHIT, style, loopless)
    dx = x0 - side_bearing(style, lc, False)  # the origin of ำ, in make() coordinates
    return [_shift(sh, dx, 0) for sh in nik] + shapes, "AM", rc, dict(info, x0=x0)


def make(ch, style, loopless=False):
    shapes, lc, rc, info = glyph(ch, style, loopless)
    if not shapes:
        return shapes, lc, rc, False, (0.0, 0.0)
    if lc == "AM":  # spaced by its sara aa; the nikhahit overhangs the consonant before
        _, _, x1, _ = Glyphs.box(shapes)
        return shapes, "O", rc, False, (info["x0"], x1)
    x0, _, x1, _ = Glyphs.box(shapes)
    return shapes, lc, rc, False, (x0, x1)


def _bounce_xform(ch, style, contours):
    """The per-glyph bounce and tilt of build_style, as a point function (identity for the shipped styles)."""
    if not contours or not (style.bounce or style.tilt):
        return lambda x, y: (x, y)
    bx0, by0, bx1, by1 = E.bounds(contours)
    seed = sum(ord(c) for c in ch) * 7919 % 1000 / 1000.0
    dy = (seed - 0.5) * 2 * style.bounce
    ang = ((seed * 3.7) % 1.0 - 0.5) * 2 * style.tilt
    if not ch.isalpha():
        dy, ang = dy * 0.3, 0.0
    cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    return lambda x, y: ((x - cx) * ca - (y - cy) * sa + cx, (x - cx) * sa + (y - cy) * ca + cy + dy)


def _placement(ch, style, loopless=False):
    """(contours, advance, point function) following build_style exactly; "Z" glyphs keep x and get advance 0."""
    from pw_font_build import side_bearing
    shapes, lc, rc, upper, (x0, x1) = make(ch, style, loopless)
    contours = E.glyph_contours(shapes)
    bx = _bounce_xform(ch, style, contours)
    if contours and (style.bounce or style.tilt):
        contours = [np.array([bx(x, y) for x, y in c]) for c in contours]
    if lc == "Z":
        dx, adv = 0.0, 0
    else:
        lsb = side_bearing(style, lc, upper)
        rsb = side_bearing(style, rc, upper)
        adv = int(round(lsb + (x1 - x0) + rsb))
        dx = lsb - x0
    contours = E.transform(contours, dx, 0) if contours else []

    def pt(x, y):
        x2, y2 = bx(x, y)
        return x2 + dx, y2
    return contours, adv, pt


def place(ch, style, loopless=False):
    """Final glyph as build_style would write it: (simplified integer contours, advance)."""
    contours, adv, _ = _placement(ch, style, loopless)
    return [E.simplify(c) for c in contours], adv


# ---------------------------------------------------------------------------------------------------------------
# OpenType features
# ---------------------------------------------------------------------------------------------------------------

BASES = [chr(c) for c in range(0x0E01, 0x0E2F)] + ["", ""]


def _gname(ch):
    from pw_font_build import glyph_name
    return glyph_name(ch)


def anchors(style, loopless=False):
    """Anchors in final glyph coordinates.
    bases {ch: {"above", "tone", "below": (x, y)}}, marks {ch: (class, (x, y))}, mark2 {ch: (x, y)} (tone over vowel)."""
    tg = _tg(style, loopless)
    bases, marks, mark2 = {}, {}, {}
    for ch in BASES:
        shapes, lc, rc, info = glyph(ch, style, loopless)
        _, _, pt = _placement(ch, style, loopless)
        _, _, x1, _ = Glyphs.box(shapes)
        xm = info.get("xm", x1)
        xb = info.get("xb", xm)
        ybot = tg.yl0 + (deep_drop(tg) if (info.get("deep") or info.get("tailed")) else 0.0)
        a = pt(xm, tg.yu0)
        bases[ch] = {"above": a, "tone": a, "below": pt(xb, ybot)}
    ref = -tg.rsb
    for ch in MARKS:
        _, info = mark_shapes(ch, style, loopless)
        _, _, pt = _placement(ch, style, loopless)
        if info["kind"] == "above":
            marks[ch] = ("above", pt(ref, tg.yu0))
            tx, ty = info["tone"]
            mark2[ch] = pt(ref + tx, tg.yu0 + ty)
        elif info["kind"] == "tone":
            marks[ch] = ("tone", pt(ref, tg.yt_hi))
        else:
            marks[ch] = ("below", pt(ref, tg.yl0))
    return bases, marks, mark2


def features(style, loopless=False):
    """feaLib code for Thai (no languagesystem lines: the hook writes LANGUAGESYSTEMS with the others at the top)."""
    bases, marks, mark2 = anchors(style, loopless)
    n = _gname
    r = lambda v: int(round(v))
    out = ["# Thai (pw_font_thai)\n"]
    # GSUB: ญ and ฐ lose their tail before a below vowel; the tailless forms are the legacy PUA glyphs.
    out.append("lookup TH_TAILLESS {\n  sub %s by %s;\n  sub %s by %s;\n} TH_TAILLESS;\n"
               % (n("ญ"), n(""), n("ฐ"), n("")))
    out.append("feature ccmp {\n  sub [%s %s]' lookup TH_TAILLESS [%s];\n} ccmp;\n"
               % (n("ญ"), n("ฐ"), " ".join(n(c) for c in BELOW)))
    # GPOS: every mark carries its own anchor; bases place upper vowels and tone marks (low position) on top,
    # below vowels underneath (deeper under ฎ ฏ and under a tailed ญ ฐ); upper vowels lift tone marks (mkmk).
    cls = {"above": "@TH_ABOVE", "tone": "@TH_TONE", "below": "@TH_BELOW"}
    for ch, (kind, (x, y)) in marks.items():
        out.append(f"markClass {n(ch)} <anchor {r(x)} {r(y)}> {cls[kind]};\n")
    out.append("lookup TH_MARK_BASE {\n")
    for ch, a in bases.items():
        out.append("  pos base %s <anchor %d %d> mark @TH_ABOVE <anchor %d %d> mark @TH_TONE <anchor %d %d> mark @TH_BELOW;\n"
                   % (n(ch), r(a["above"][0]), r(a["above"][1]), r(a["tone"][0]), r(a["tone"][1]),
                      r(a["below"][0]), r(a["below"][1])))
    out.append("} TH_MARK_BASE;\n")
    out.append("lookup TH_MARK_MARK {\n")
    for ch, (x, y) in mark2.items():
        out.append("  pos mark %s <anchor %d %d> mark @TH_TONE;\n" % (n(ch), r(x), r(y)))
    out.append("} TH_MARK_MARK;\n")
    out.append("feature mark {\n  lookup TH_MARK_BASE;\n} mark;\n")
    out.append("feature mkmk {\n  lookup TH_MARK_MARK;\n} mkmk;\n")
    return "".join(out)


# ---------------------------------------------------------------------------------------------------------------
# Pre-shaping for TextMeshPro (reference implementation of the rules the game ports to C#)
# ---------------------------------------------------------------------------------------------------------------

def preshape(text, legacy=False):
    """Pure function on a Thai string, run once per string before it reaches TextMeshPro.

    Always (TMP applies GPOS mark and mkmk from the font, but no contextual GSUB):
      1. sara am (U+0E33) becomes nikhahit (U+0E4D) + sara aa (U+0E32); the nikhahit joins the marks of the
         cluster before it (C [below] [tone] U+0E33  ->  C [below] U+0E4D [tone] U+0E32).
      2. Marks of a cluster are ordered below vowel, upper vowel, tone mark (stable within a group), so mkmk finds
         the vowel before the tone.
      3. U+0E0D -> U+F70F and U+0E10 -> U+F700 (tail removed) when the cluster has a below vowel U+0E38-U+0E3A.
    legacy=True (only if the text component cannot enable the mark and mkmk features) also:
      4. after U+0E1B U+0E1D U+0E1F U+0E2C: upper vowels and nikhahit -> shifted left (U+F701-F704, F710-F712);
         tone marks -> low-left U+F705-F709 when no upper vowel comes before them, else left U+F713-F717.
      5. after any other base: tone marks with no upper vowel before them in the cluster -> low U+F70A-F70E.
      6. after U+0E0E U+0E0F: below vowels -> lowered U+F718-F71A.
    """
    chars = list(text)
    out = []
    i = 0
    order = {**{m: 0 for m in BELOW}, **{m: 1 for m in ABOVE_VOWELS + OTHER_ABOVE}, **{m: 2 for m in TONES}}
    while i < len(chars):
        c = chars[i]
        if c in MARKS or c == SARA_AM:
            out.append(c)
            i += 1
            continue
        j = i + 1
        while j < len(chars) and chars[j] in MARKS:
            j += 1
        marks = chars[i + 1:j]
        am = j < len(chars) and chars[j] == SARA_AM
        if am:
            marks = marks + [NIKHAHIT]
        marks = sorted(marks, key=lambda m: order.get(m, 1))
        base = c
        if base in TAILED and any(m in BELOW for m in marks):
            base = PUA_FOR[(base, "rd")]
        if legacy:
            seen_above = False
            new = []
            for m in marks:
                if m in ABOVE_VOWELS:
                    seen_above = True
                    new.append(PUA_FOR.get((m, "l"), m) if c in TALL else m)
                elif m in TONES:
                    if c in TALL:
                        new.append(PUA_FOR[(m, "l" if seen_above else "dl")])
                    else:
                        new.append(m if seen_above else PUA_FOR[(m, "d")])
                elif m in BELOW and c in DEEP:
                    new.append(PUA_FOR[(m, "d")])
                else:
                    new.append(m)
            marks = new
        out.append(base)
        out += marks
        if am:
            out.append(SARA_AA)
            j += 1
        i = j
    return "".join(out)
