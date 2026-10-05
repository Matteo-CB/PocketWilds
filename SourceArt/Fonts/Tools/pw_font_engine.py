"""Stroke-skeleton font engine for the Pocket Wilds fonts (T31).

A glyph is a list of strokes. A stroke is a centerline path (lines, superelliptic arcs, cubic curves, smooth
splines through points with tangent angles) expanded by a width with round caps. Closed paths give an outer and an
inner contour (a counter). Overlapping strokes are kept as separate contours: TrueType fills with the non-zero rule,
so overlaps merge visually (FreeType, Unity TextCore and TextMeshPro all render them as one shape).

Only numpy and fontTools are needed. Units: font units, 1000 per em, y up.
"""
import math

import numpy as np

SAMPLE_STEP = 5.0  # font units between samples along a path


class Path:
    """Centerline built segment by segment. Every segment continues from the current point."""

    def __init__(self, x, y):
        self.points = [(float(x), float(y))]
        self.closed = False

    @property
    def last(self):
        return self.points[-1]

    def _add(self, pts):
        for p in pts:
            q = self.points[-1]
            if (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 0.01:
                self.points.append((float(p[0]), float(p[1])))
        return self

    def line(self, x, y):
        x0, y0 = self.last
        length = math.hypot(x - x0, y - y0)
        n = max(1, int(length / SAMPLE_STEP))
        return self._add([(x0 + (x - x0) * i / n, y0 + (y - y0) * i / n) for i in range(1, n + 1)])

    def arc(self, cx, cy, rx, ry, a0, a1, n=2.0):
        """Superelliptic arc from angle a0 to a1 (degrees, increasing = counter-clockwise). n=2 is an ellipse."""
        span = abs(a1 - a0)
        steps = max(8, int(span / 360.0 * 2 * math.pi * max(rx, ry) / SAMPLE_STEP * 1.5))
        pts = [superellipse(cx, cy, rx, ry, a0 + (a1 - a0) * i / steps, n) for i in range(0, steps + 1)]
        return self._add(pts)

    def curve(self, x1, y1, x2, y2, x3, y3):
        x0, y0 = self.last
        approx = math.hypot(x1 - x0, y1 - y0) + math.hypot(x2 - x1, y2 - y1) + math.hypot(x3 - x2, y3 - y2)
        steps = max(8, int(approx / SAMPLE_STEP))
        pts = []
        for i in range(1, steps + 1):
            t = i / steps
            u = 1 - t
            pts.append((u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
                        u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3))
        return self._add(pts)

    def spline(self, knots, tension=0.39):
        """Smooth curve through knots [(x, y, angle_deg), ...]. The first knot must equal the current point;
        each angle is the direction of travel at that knot. Handles = chord x tension (0.39 ~ circular arcs)."""
        for (xa, ya, aa), (xb, yb, ab) in zip(knots, knots[1:]):
            d = math.hypot(xb - xa, yb - ya) * tension
            ta = math.radians(aa)
            tb = math.radians(ab)
            self.curve(xa + math.cos(ta) * d, ya + math.sin(ta) * d, xb - math.cos(tb) * d, yb - math.sin(tb) * d, xb, yb)
        return self

    def close(self):
        if len(self.points) > 2:
            p, q = self.points[0], self.points[-1]
            if (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 < 4.0:
                self.points.pop()
        self.closed = True
        return self


def superellipse(cx, cy, rx, ry, angle_deg, n=2.0):
    t = math.radians(angle_deg)
    c, s = math.cos(t), math.sin(t)
    e = 2.0 / n
    return (cx + rx * math.copysign(abs(c) ** e, c), cy + ry * math.copysign(abs(s) ** e, s))


class Stroke:
    def __init__(self, path, width, width_fn=None):
        self.path = path
        self.width = width
        self.width_fn = width_fn  # f(t in 0..1) -> multiplier


class Dot:
    def __init__(self, x, y, r):
        self.x, self.y, self.r = x, y, r


def _tangents(p, closed):
    if closed:
        d = np.roll(p, -1, axis=0) - np.roll(p, 1, axis=0)
    else:
        d = np.empty_like(p)
        d[1:-1] = p[2:] - p[:-2]
        d[0] = p[1] - p[0]
        d[-1] = p[-1] - p[-2]
    norm = np.linalg.norm(d, axis=1, keepdims=True)
    norm[norm == 0] = 1
    return d / norm


def _cap(center, tangent, normal, h, start):
    pts = []
    for i in range(1, 16):
        a = math.pi * i / 16
        if start:
            v = -normal * math.cos(a) - tangent * math.sin(a)
        else:
            v = normal * math.cos(a) + tangent * math.sin(a)
        pts.append(center + v * h)
    return pts


def signed_area(c):
    x, y = c[:, 0], c[:, 1]
    return 0.5 * float(np.sum(x * np.roll(y, -1) - np.roll(x, -1) * y))


def stroke_contours(stroke):
    p = np.array(stroke.path.points, dtype=float)
    closed = stroke.path.closed
    if len(p) < 2:
        return []
    t = _tangents(p, closed)
    n = np.stack([-t[:, 1], t[:, 0]], axis=1)
    seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    u = s / s[-1] if s[-1] > 0 else s
    h = np.full(len(p), stroke.width * 0.5)
    if stroke.width_fn is not None:
        h = h * np.array([stroke.width_fn(v) for v in u])
    left = p + n * h[:, None]
    right = p - n * h[:, None]
    if closed:
        a, b = left, right
        if abs(signed_area(a)) < abs(signed_area(b)):
            a, b = b, a
        if signed_area(a) > 0:
            a = a[::-1]
        if signed_area(b) < 0:
            b = b[::-1]
        return [a, b]
    contour = list(left)
    contour += _cap(p[-1], t[-1], n[-1], h[-1], start=False)
    contour += list(right[::-1])
    contour += _cap(p[0], t[0], n[0], h[0], start=True)
    c = np.array(contour)
    if signed_area(c) > 0:
        c = c[::-1]
    return [c]


def dot_contour(dot):
    steps = max(24, int(2 * math.pi * dot.r / SAMPLE_STEP))
    pts = [(dot.x + dot.r * math.cos(-2 * math.pi * i / steps), dot.y + dot.r * math.sin(-2 * math.pi * i / steps)) for i in range(steps)]
    return np.array(pts)  # clockwise


def _rdp(points, eps):
    if len(points) < 3:
        return points
    start, end = points[0], points[-1]
    line = end - start
    norm = np.hypot(*line)
    if norm == 0:
        dists = np.hypot(*(points - start).T)
    else:
        dists = np.abs(line[0] * (points[:, 1] - start[1]) - line[1] * (points[:, 0] - start[0])) / norm
    i = int(np.argmax(dists))
    if dists[i] > eps:
        left = _rdp(points[: i + 1], eps)
        right = _rdp(points[i:], eps)
        return np.vstack([left[:-1], right])
    return np.vstack([start, end])


def simplify(contour, eps=0.45):
    """Douglas-Peucker on a closed contour, then integer rounding without consecutive duplicates."""
    c = np.asarray(contour, dtype=float)
    if len(c) < 8:
        r = np.round(c)
    else:
        k = len(c) // 2
        a = _rdp(c[: k + 1], eps)
        b = _rdp(np.vstack([c[k:], c[:1]]), eps)
        r = np.round(np.vstack([a[:-1], b[:-1]]))
    out = [r[0]]
    for q in r[1:]:
        if q[0] != out[-1][0] or q[1] != out[-1][1]:
            out.append(q)
    if len(out) > 1 and out[0][0] == out[-1][0] and out[0][1] == out[-1][1]:
        out.pop()
    return np.array(out, dtype=int)


def glyph_contours(shapes):
    contours = []
    for shape in shapes:
        if isinstance(shape, Dot):
            contours.append(dot_contour(shape))
        else:
            contours.extend(stroke_contours(shape))
    return contours


def transform(contours, dx=0.0, dy=0.0, angle_deg=0.0, cx=0.0, cy=0.0, sx=1.0):
    out = []
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    for c in contours:
        x = (c[:, 0] - cx) * sx
        y = c[:, 1] - cy
        xr = x * ca - y * sa + cx + dx
        yr = x * sa + y * ca + cy + dy
        out.append(np.stack([xr, yr], axis=1))
    return out


def bounds(contours):
    allp = np.vstack(contours)
    return float(allp[:, 0].min()), float(allp[:, 1].min()), float(allp[:, 0].max()), float(allp[:, 1].max())
