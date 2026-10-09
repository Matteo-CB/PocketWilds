"""Vietnamese and the last Latin letters (Azerbaijani schwa, Croatian digraphs) for the Pocket Wilds fonts.

Vietnamese is built the way pw_font_glyphs.decompose builds accented Latin, extended to the Vietnamese marks:
    - the horn (Ơ ơ Ư ư) is drawn as part of the letter, leaving the shoulder of O or the top of the U stem;
    - hook above and dot below are new marks in the same pen as the Latin accents;
    - circumflex with acute or hook above: the tone sits at the upper right of the circumflex, side by side; with the
      grave, at the upper left;
      circumflex with tilde, and breve with any tone: the tone is stacked above, compactly;
    - every mark in a cluster is placed by measuring the ink gap (clearance), so no weight lets two marks touch;
    - capitals shrink their clusters until they fit under a ceiling, so lines keep a sane height.
Single marks (à á ã â ă) are exactly the Latin build's, so Vietnamese text matches the existing accented letters.

Script hook API, like pw_font_cyrillic_greek:
    SCRIPT = "viet"
    charset(), make(ch, style), features(style), kerning(cmap, data, style, existing=None)
"""
import unicodedata

from pw_font_engine import Dot, Path
from pw_font_glyphs import CHARS, Glyphs, _rotate180, _se
from pw_font_cyrillic_greek import clearance, measure_pairs, move, scale_about, style_key

SCRIPT = "viet"
LANGUAGESYSTEMS = []

MARK_OF = {"̀": "grave", "́": "acute", "̃": "tilde", "̉": "hook", "̂": "circumflex",
           "̆": "breve", "̣": "dotbelow", "̛": "horn"}
TONES = ("grave", "acute", "tilde", "hook")
HORN = {"O": "g_Ohorn", "o": "g_ohorn", "U": "g_Uhorn", "u": "g_uhorn"}
DIGRAPHS = {"Ǆ": "DŽ", "ǅ": "Dž", "ǆ": "dž", "Ǉ": "LJ", "ǈ": "Lj", "ǉ": "lj",
            "Ǌ": "NJ", "ǋ": "Nj", "ǌ": "nj"}
OWN = {"Ơ": "g_Ohorn", "ơ": "g_ohorn", "Ư": "g_Uhorn", "ư": "g_uhorn",
       "Ə": "g_Schwa", "ə": "g_schwa"}


class Viet(Glyphs):
    def __init__(self, s):
        super().__init__(s)
        self.L = 100 + s.w * 0.35          # accent length, as in Glyphs.accent
        self.cluster_gap = max(22.0, s.w * 0.22)   # between the marks of a cluster
        # Highest ink allowed for a stacked cluster: 1000 in Regular, a little more for heavy pens and the puffy Plush,
        # whose bars bulge above the cap height.
        self.ceiling = s.cap + 300 + max(0.0, s.w - 92) * 0.4 + s.inflate * 90

    # ---------------- letters ----------------

    def _horn(self, x0, y0, tip_x, tip_y):
        """The horn: leaves the letter heading up and right, ends upright in a round terminal."""
        return self.st(Path(x0, y0).spline([(x0, y0, 22), (tip_x, tip_y, 84)], 0.46), 0.88)

    def _o_horn(self, H, W):
        s, h = self.s, self.h
        cx, cy = W / 2, H / 2
        rx, ry = W / 2 - h, (H + 2 * s.ov) / 2 - h
        x0, y0 = _se(cx, cy, rx, ry, 40, s.n)
        rise = 34 if H == s.cap else 70
        return self._horn(x0, y0, W + 26 + (s.w - 88) * 0.3, H + rise - h * 0.4)

    def g_Ohorn(self):
        shapes, lc, _ = self.g_O()
        return shapes + [self._o_horn(self.s.cap, self.Ow)], lc, "O"

    def g_ohorn(self):
        shapes, lc, _ = self.g_o()
        return shapes + [self._o_horn(self.s.xh, self.ow)], lc, "O"

    def _u_horn(self, H, xr):
        s, h = self.s, self.h
        rise = 34 if H == s.cap else 70
        return self._horn(xr, H - h - 54, xr + 104 + (s.w - 88) * 0.5, H + rise - h * 0.4)

    def g_Uhorn(self):
        shapes, lc, _ = self.g_U()
        return shapes + [self._u_horn(self.s.cap, self.Hw - self.h)], lc, "O"

    def g_uhorn(self):
        shapes, lc, _ = self.g_u()
        return shapes + [self._u_horn(self.s.xh, self.nw - self.h)], lc, "O"

    def g_Schwa(self):
        """Ə: a capital e turned round (opening at the top left, bar across the middle)."""
        s, h = self.s, self.h
        ew = self.Ow * 0.90
        C = s.cap
        a = s.aperture - 6 + max(0.0, s.w - 88) * 0.35
        arc = self.open_arc(0, ew, -s.ov, C + s.ov, 0, 360 - a)
        bar = self.line(h, C / 2, ew - h, C / 2, s.bar)
        return [_rotate180(sh, ew / 2, C / 2) for sh in (arc, bar)], "O", "R"

    def g_schwa(self):
        shapes, _, _ = self.g_e()
        ew = self.ow * 0.97
        return [_rotate180(sh, ew / 2, self.s.xh / 2) for sh in shapes], "O", "R"

    # ---------------- marks ----------------

    def hook_above(self, cx, base_y, upper):
        """Vietnamese hook above (dấu hỏi): the top of a question mark, open at the lower left, its tail back at the
        center. A lighter pen than the other marks keeps the curl open at small sizes."""
        s = self.s
        lift = 30 if upper else self.dot_gap * 0.62
        heavy = max(0.0, s.w - 92)
        aw = 0.64 * (1.0 - heavy * 0.002)
        hw = s.w * aw / 2
        r = max(self.L * 0.34, hw * (2.1 - heavy * 0.006))
        yb = base_y + lift + hw
        cy = yb + r * 1.40
        p = Path(*_se(cx, cy, r, r * 0.90, 150, 2.0)).arc(cx, cy, r, r * 0.90, 150, -20, 2.0)
        ex, ey = p.last
        p.spline([(ex, ey, -110), (cx + r * 0.04, yb, 262)], 0.45)
        return [self.st(p, aw)]

    def tone(self, kind, cx, base_y, upper):
        if kind == "hook":
            return self.hook_above(cx, base_y, upper)
        return self.accent(kind, cx, base_y, upper)

    def dot_below(self, cx):
        s = self.s
        r = self.dr * 0.92
        return [Dot(cx, -(max(30.0, s.w * 0.38) + r), r)]

    # ---------------- clusters ----------------

    def cluster(self, first, tone, cx, by, upper, k=1.0):
        """Circumflex or breve with a tone mark. k < 1 shrinks the marks (capitals under the ceiling)."""
        s = self.s
        # Heavy weights draw cluster marks a little lighter; capitals sit their cluster closer to the letter.
        wm = 1.0 - max(0.0, s.w - 92) * (0.0052 if upper else 0.003)
        f = self.accent(first, cx, by, upper)
        if upper:
            f = move(f, 0, -14)
        fx0, fy0, fx1, fy1 = self.box(f)
        fs = (0.86 if upper else 1.0) * k
        # Only the height shrinks much: a narrow breve or circumflex in a heavy weight would close up.
        f = scale_about(f, cx, fy0, max(fs, 0.92) if first == "circumflex" else 1.0, fs, wm)
        fx0, fy0, fx1, fy1 = self.box(f)
        ts = (0.80 if upper else 0.88) * k
        t = self.tone(tone, 0, 0, False)
        tx0, ty0, tx1, ty1 = self.box(t)
        t = scale_about(t, (tx0 + tx1) / 2, ty0, ts, ts, wm)
        tx0, ty0, tx1, ty1 = self.box(t)
        if first == "circumflex" and tone != "tilde":
            # Side by side, the Vietnamese way: acute and hook at the upper right of the circumflex, grave at the upper
            # left. The tone starts over the apex and slides outwards and up until it clears the circumflex.
            sy = fy0 + (fy1 - fy0) * 0.45 - ty0
            if tone == "grave":
                sx = cx - (cx - fx0) * 0.10 - tx1
                dx, dy = -0.74, 0.67
            else:
                sx = cx + (fx1 - cx) * 0.10 - tx0
                dx, dy = 0.74, 0.67
            gap = self.cluster_gap * 0.75
        else:
            sx = cx - (tx0 + tx1) / 2
            sy = fy1 - (fy1 - fy0) * 0.60 - ty0
            dx, dy = 0.0, 1.0
            gap = self.cluster_gap * 1.05
        step = 0
        placed = move(t, sx, sy)
        while clearance(f, placed) < gap and step < 150:
            step += 1
            placed = move(t, sx + dx * 2 * step, sy + dy * 2 * step)
        return f + placed

    def seated(self, base, first, tone, cx, by, upper, k):
        """A cluster lifted, if needed, until it clears the letter (a capital cluster sits low, over the A apex)."""
        return self._seat(base, self.cluster(first, tone, cx, by, upper, k), 0.0, 2.0)

    # ---------------- composition ----------------

    def compose(self, ch):
        if ch in OWN:
            shapes, lc, rc = getattr(self, OWN[ch])()
            x0, _, x1, _ = self.box(shapes)
            return shapes, lc, rc, ch.isupper(), (x0, x1)
        if ch in DIGRAPHS:
            return self.digraph(DIGRAPHS[ch])
        d = unicodedata.normalize("NFD", ch)
        base, marks = d[0], [MARK_OF[m] for m in d[1:]]
        horn = "horn" in marks
        below = "dotbelow" in marks
        first = next((m for m in marks if m in ("circumflex", "breve")), None)
        tone = next((m for m in marks if m in TONES), None)
        if (first or tone) and base in "ij":
            base = {"i": "ı", "j": "ȷ"}[base]
        method = HORN[base] if horn else CHARS[base]
        shapes, lc, rc = getattr(self, method)()
        s, h = self.s, self.h
        upper = base.isupper()
        if horn:
            body, _, _ = (self.g_O() if base == "O" else self.g_o() if base == "o" else self.g_U() if base == "U" else self.g_u())
            bx0, _, bx1, _ = self.box(body)
        else:
            bx0, _, bx1, _ = self.box(shapes)
        x0, _, x1, _ = self.box(shapes)
        cx = h if base == "ı" else (bx0 + bx1) / 2
        by = s.cap if upper else s.xh
        mark = []
        if first and tone:
            k = 1.0
            mark = self.seated(shapes, first, tone, cx, by, upper, k)
            while self.box(mark)[3] > self.ceiling and k > 0.44:
                k -= 0.03
                mark = self.seated(shapes, first, tone, cx, by, upper, k)
        elif first or tone:
            mark = self._seat(shapes, self.tone(first or tone, cx, by, upper), 0.0, 2.0)
        if mark:
            mx0, _, mx1, _ = self.box(mark)
            x0, x1 = min(x0, mx0 + 25), max(x1, mx1 - 25)
        if below:
            dot = self.dot_below(cx)
            if base == "y":
                step = 0
                while clearance(shapes, dot) < self.cluster_gap and step < 80:
                    step += 1
                    dot = self.dot_below(cx + 4 * step)
            mark = mark + dot
        return shapes + mark, lc, rc, upper, (x0, x1)

    def digraph(self, pair):
        """Croatian compatibility digraphs: the two letters with their own spacing between them."""
        from pw_font_build import side_bearing
        a, b = pair
        sa, la, ra, ua, (ax0, ax1) = Glyphs.make(self, a)
        sb, lb, rb, ub, (bx0, bx1) = Glyphs.make(self, b)
        dx = ax1 + side_bearing(self.s, ra, ua) + side_bearing(self.s, lb, ub) - bx0
        return sa + move(sb, dx), la, rb, ua, (ax0, bx1 + dx)


def charset():
    out = ["Ơ", "ơ", "Ư", "ư", "Ə", "ə"] + [chr(c) for c in range(0x01C4, 0x01CD)]
    return out + [chr(c) for c in range(0x1EA0, 0x1EFA)]


_CACHE = {}


def glyphs_for(style):
    key = style_key(style)
    g = _CACHE.get(key)
    if g is None:
        g = _CACHE[key] = Viet(style)
    return g


def make(ch, style):
    """(shapes, left_class, right_class, upper, (x0, x1)) for one character, like Glyphs.make."""
    return glyphs_for(style).compose(ch)


def features(style):
    return ""


# ------------------------------------------------------------------------------------------- kerning

def kern_variants(bases):
    """Vietnamese letters grouped by their Latin base (Ơ and Ư count as O and U)."""
    out = {}
    for ch in charset():
        if ch in DIGRAPHS or ch in "Əə":
            continue
        b = unicodedata.normalize("NFD", ch)[0]
        if b in bases:
            out.setdefault(b, []).append(ch)
    return out


def kerning(cmap, data, style, existing=None):
    """Latin pairs again, with a Vietnamese right letter (TẠ, Vậ, Yế), measured on their own glyphs."""
    from pw_font_build import KERN_LEFT, KERN_RIGHT
    variants = kern_variants(KERN_RIGHT)
    rights = "".join(variants.keys())
    pairs = measure_pairs(KERN_LEFT, rights, variants, cmap, data, existing)
    # Only the new letters: the base pairs are already in the Latin build.
    new = {c for v in variants.values() for c in v}
    return {k: v for k, v in pairs.items() if data[k[1]][2] in new}
