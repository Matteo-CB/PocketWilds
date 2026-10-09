"""Verification of the Han glyphs (FH): is each of our glyphs the right character?

    python pw_font_han_verify.py                 # every region, metrics + review sheets
    python pw_font_han_verify.py JP 新友卵       # some characters of one region
    python pw_font_han_verify.py JP 新友卵 --tag c02   # outputs named verify_JP_c02.csv, review_JP_c02_00.png

The Windows system fonts (Yu Gothic for JP, Microsoft YaHei for SC, Microsoft JhengHei for TC) are rendered ONLY
to compare with ours: nothing is traced, skeletonized or stored from them, and no metric of theirs enters our data.
Both glyphs are normalised the same way (ink centroid and spread, so the proportions of the two designs matter less).
Per character:
  - blur: blurred-structure similarity (Gaussian blur, correlation);
  - structure: similarity of gradient-orientation histograms on 4x4, 6x6 and 8x8 cells (robust to our rounder,
    heavier strokes, sensitive to which strokes are where);
  - rank: rank of the right character among every reference of the region by structure (1 = ours is closer to the
    right character than to any other character of the set);
  - duel: when the rank is above 1, a two-way test on the pixels where the right reference and each closer one
    differ (near twins such as 己 / 已 or 土 / 士): ours must side with the right character every time; the two
    references are normalised with the same transform (the right one's centroid and spread), so they stay in
    register and differ only where the characters do;
  - holes: closed counters of both at 64 px;
  - strokes: strokes we draw against the count written in pw_font_han_ids.txt;
  - ink: our skeleton length against half the perimeter of the reference ink (missing or extra strokes).
A character is WRONG when it loses a duel. Outputs (local only, TestResults is not versioned):
TestResults/Fonts/Han/verify_<REGION>.csv and review sheets review_<REGION>_<n>.png, wrong first, then the lowest
structure (ours left, the reference right in grey).
"""
import csv
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import pw_font_engine as E
import pw_font_han as H
from pw_font_build import FAMILIES

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "TestResults", "Fonts", "Han")
REF = {"JP": r"C:\Windows\Fonts\YuGothM.ttc", "SC": r"C:\Windows\Fonts\msyh.ttc", "TC": r"C:\Windows\Fonts\msjh.ttc"}
N = 48          # comparison size
HR = 256        # render size before reduction


def _cmap(path):
    from fontTools.ttLib import TTCollection
    return set(TTCollection(path).fonts[0].getBestCmap().keys())


def _square(img, size):
    """Crop to the ink box, centre on a square canvas (aspect kept), resize."""
    a = np.asarray(img)
    ys, xs = np.where(a > 40)
    if len(xs) == 0:
        return np.zeros((size, size))
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    w, h = x1 - x0, y1 - y0
    s = max(w, h)
    canvas = Image.new("L", (s + 8, s + 8), 0)
    canvas.paste(img.crop((x0, y0, x1, y1)), (4 + (s - w) // 2, 4 + (s - h) // 2))
    return np.asarray(canvas.resize((size, size), Image.LANCZOS), dtype=float)


def render_ours(ch, region, style):
    shapes, *_ = H.make(ch, style, region)
    contours = E.glyph_contours(shapes)
    s = HR / 1000.0
    img = Image.new("L", (HR, HR), 0)
    d = ImageDraw.Draw(img)
    for c in contours:
        pts = [(x * s, (H.EM_TOP - y) * s) for x, y in c]
        if len(pts) > 2:
            d.polygon(pts, fill=255)
    return img


def render_ref(ch, font):
    img = Image.new("L", (HR, HR), 0)
    d = ImageDraw.Draw(img)
    d.text((HR * 0.06, -HR * 0.04), ch, font=font, fill=255)
    return img


def _blur(a, sigma):
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    b = np.asarray(img.filter(ImageFilter.GaussianBlur(sigma)), dtype=float)
    b = b - b.mean()
    n = np.linalg.norm(b)
    return b / n if n else b


def moments(img):
    """Ink centroid and spread per axis."""
    a = np.asarray(img, dtype=float)
    ys, xs = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    m = a.sum() or 1.0
    cx, cy = (a * xs).sum() / m, (a * ys).sum() / m
    sx = math.sqrt((a * (xs - cx) ** 2).sum() / m) or 1.0
    sy = math.sqrt((a * (ys - cy) ** 2).sum() / m) or 1.0
    return cx, cy, sx, sy


def moment_norm(img, size=48, k=2.2, params=None):
    """Resample so the ink centroid is centred and its spread (per axis) fills a fixed part of the square
    (params: use another image's centroid and spread, so two renders of one font stay in register)."""
    a = np.asarray(img, dtype=float)
    cx, cy, sx, sy = params if params is not None else moments(a)
    u = (np.arange(size) + 0.5 - size / 2) / (size / 2)
    X = cx + u[None, :] * k * sx
    Y = cy + u[:, None] * k * sy
    X0 = np.clip(np.floor(X).astype(int), 0, a.shape[1] - 2)
    Y0 = np.clip(np.floor(Y).astype(int), 0, a.shape[0] - 2)
    fx, fy = np.clip(X - X0, 0, 1), np.clip(Y - Y0, 0, 1)
    out = (a[Y0, X0] * (1 - fx) * (1 - fy) + a[Y0, X0 + 1] * fx * (1 - fy) + a[Y0 + 1, X0] * (1 - fx) * fy
           + a[Y0 + 1, X0 + 1] * fx * fy)
    inside = (X >= 0) & (X < a.shape[1] - 1) & (Y >= 0) & (Y < a.shape[0] - 1)
    return out * inside


def hog(a, cells, bins=8, s=1.2):
    b = np.asarray(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s)), dtype=float)
    gy, gx = np.gradient(b)
    mag = np.hypot(gx, gy)
    ang = (np.arctan2(gy, gx) % np.pi) / np.pi * bins
    h, w = a.shape
    ch, cw = h // cells, w // cells
    f = np.zeros((cells, cells, bins))
    bi = np.floor(ang).astype(int) % bins
    fr = ang - np.floor(ang)
    for y in range(cells):
        for x in range(cells):
            sl = (slice(y * ch, (y + 1) * ch), slice(x * cw, (x + 1) * cw))
            m, b0, f0 = mag[sl].ravel(), bi[sl].ravel(), fr[sl].ravel()
            np.add.at(f[y, x], b0, m * (1 - f0))
            np.add.at(f[y, x], (b0 + 1) % bins, m * f0)
    v = np.sqrt(f).ravel()
    v = v - v.mean()
    n = np.linalg.norm(v)
    return v / n if n else v


def features(img):
    a = moment_norm(img, 48)
    return {"h": [hog(a, c) for c in (4, 6, 8)], "b": _blur(a, 2.0).ravel(), "d": _blur(moment_norm(img, 64), 1.4)}


def duel(ours, right, other):
    """Positive when ours sides with the right reference on the pixels where the two references differ."""
    D = right - other
    nd = np.linalg.norm(D)
    if nd < 1e-9:
        return 0.0
    mid = (right + other) / 2
    return float(((ours - mid) * D).sum() / nd)


def holes(a, thr=110):
    """Closed counters: background regions (4-connected) that do not touch the border."""
    ink = a > thr
    h, w = ink.shape
    seen = np.zeros_like(ink, dtype=bool)
    count = 0
    for y0 in range(h):
        for x0 in range(w):
            if ink[y0, x0] or seen[y0, x0]:
                continue
            stack = [(y0, x0)]
            seen[y0, x0] = True
            border = False
            size = 0
            while stack:
                y, x = stack.pop()
                size += 1
                if y == 0 or x == 0 or y == h - 1 or x == w - 1:
                    border = True
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and not ink[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if not border and size >= 4:
                count += 1
    return count


def perimeter(a, thr=110):
    ink = a > thr
    p = np.sum(ink[:, 1:] != ink[:, :-1]) + np.sum(ink[1:, :] != ink[:-1, :])
    return float(p)


def skeleton_length(g):
    total = 0.0
    for runs in g.runs:
        for pts in runs:
            total += float(np.hypot(*np.diff(pts, axis=0).T).sum())
    return total


def verify(region, chars=None, style=None, sheets=True, tag=None):
    style = style or FAMILIES["PocketRound"][0]
    font = ImageFont.truetype(REF[region], int(HR * 0.86), index=0)
    has = _cmap(REF[region])
    allc = H.charset(region)
    chars = [c for c in chars if c in allc] if chars else allc
    keys = [ch for ch in allc if ord(ch) in has]
    rf = {ch: features(render_ref(ch, font)) for ch in keys}
    RH = [np.stack([rf[k]["h"][i] for k in keys]) for i in range(3)]
    RB = np.stack([rf[k]["b"] for k in keys])
    rows = []
    for ch in chars:
        g = H.build_glyph(ch, style, region)
        ours_img = render_ours(ch, region, style)
        of = features(ours_img)
        row = {"char": ch, "cp": "U+%04X" % ord(ch), "region": region}
        row["strokes"] = len(g.strokes)
        row["expected"] = H.data().expected.get((ch, region))
        row["touch"] = len(g.problems)
        if ch in rf:
            sh = sum(RH[i] @ of["h"][i] for i in range(3)) / 3.0
            sb = RB @ of["b"]
            i = keys.index(ch)
            row["structure"] = round(float(sh[i]), 3)
            row["blur"] = round(float(sb[i]), 3)
            order = list(np.argsort(-sh))
            row["rank"] = order.index(i) + 1
            row["best"] = keys[order[0]]
            lost = []
            closer = order[:order.index(i)]
            if closer:
                right_img = render_ref(ch, font)
                pr = moments(right_img)
                rd = _blur(moment_norm(right_img, 64, params=pr), 1.4)
            for j in closer:
                # the other reference in the right one's register: the two differ only where the characters do
                od = _blur(moment_norm(render_ref(keys[j], font), 64, params=pr), 1.4)
                if duel(of["d"], rd, od) <= 0:
                    lost.append(keys[j])
            row["duel_lost"] = "".join(lost)
            a64o = _square(ours_img, 64)
            a64r = _square(render_ref(ch, font), 64)
            row["holes"] = holes(a64o)
            row["holes_ref"] = holes(a64r)
            ref_len = perimeter(a64r) / 2.0
            ours_len = skeleton_length(g) * 64.0 / max(1.0, max(_inkbox(g)))
            row["ink_ratio"] = round(ours_len / max(1.0, ref_len), 2)
        else:
            row.update(structure=None, blur=None, rank=None, best=None, duel_lost="", holes=None, holes_ref=None,
                       ink_ratio=None)
        row["flags"] = _flags(row)
        rows.append(row)
    os.makedirs(OUT, exist_ok=True)
    tag = region + "_" + tag if tag else (region if chars is allc else region + "_part")
    with open(os.path.join(OUT, f"verify_{tag}.csv"), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    if sheets:
        review_sheets(rows, region, font, style, tag)
    return rows


def _inkbox(g):
    x0, y0, x1, y1 = g.ink_bounds()
    return (x1 - x0, y1 - y0)


def _flags(r):
    f = []
    if r["duel_lost"]:
        f.append("WRONG:" + r["duel_lost"])
    elif r["rank"] is not None and r["rank"] > 1:
        f.append("rank%d" % r["rank"])
    if r["structure"] is not None and r["structure"] < 0.55:
        f.append("low")
    if r["holes"] is not None and r["holes"] != r["holes_ref"]:
        f.append("holes%d/%d" % (r["holes"], r["holes_ref"]))
    if r["expected"] is not None and r["expected"] != r["strokes"]:
        f.append("strokes%d/%d" % (r["strokes"], r["expected"]))
    if r["ink_ratio"] is not None and not (0.72 <= r["ink_ratio"] <= 1.38):
        f.append("ink%.2f" % r["ink_ratio"])
    if r["touch"]:
        f.append("touch%d" % r["touch"])
    return " ".join(f)


def review_sheets(rows, region, font, style, tag, per=60):
    rows = sorted(rows, key=lambda r: (0 if r["duel_lost"] else 1,
                                       r["structure"] if r["structure"] is not None else -1))
    lab = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 12, index=0)
    px = 96
    cols = 6
    for page in range(0, len(rows), per):
        chunk = rows[page:page + per]
        nrows = (len(chunk) + cols - 1) // cols
        cw = 2 * px + 24
        img = Image.new("RGB", (cols * cw + 10, nrows * (px + 30) + 10), (255, 255, 255))
        d = ImageDraw.Draw(img)
        for i, r in enumerate(chunk):
            x = 10 + (i % cols) * cw
            y = 10 + (i // cols) * (px + 30)
            o = render_ours(r["char"], region, style).resize((px, px), Image.LANCZOS)
            img.paste(Image.merge("RGB", [Image.eval(o, lambda v: 255 - v)] * 3), (x, y))
            ref = render_ref(r["char"], font).resize((px, px), Image.LANCZOS)
            img.paste(Image.merge("RGB", [Image.eval(ref, lambda v: 255 - int(v * 0.55))] * 3), (x + px + 4, y))
            col = (200, 30, 30) if r["flags"] else (0, 0, 0)
            d.text((x, y + px + 1), "%s s%s r%s" % (r["char"], r["structure"], r["rank"]), font=lab, fill=col)
            d.text((x, y + px + 14), r["flags"][:34], font=lab, fill=col)
        img.save(os.path.join(OUT, f"review_{tag}_{page // per:02d}.png"))


def summary(rows):
    scores = [r["structure"] for r in rows if r["structure"] is not None]
    return {"n": len(rows), "wrong": sum(1 for r in rows if r["duel_lost"]),
            "rank>1": sum(1 for r in rows if r["rank"] and r["rank"] > 1),
            "flagged": sum(1 for r in rows if r["flags"]),
            "mean_structure": round(sum(scores) / max(1, len(scores)), 3),
            "min_structure": min(scores) if scores else None}


if __name__ == "__main__":
    args = sys.argv[1:]
    tag = None
    if "--tag" in args:
        i = args.index("--tag")
        tag = args[i + 1]
        del args[i:i + 2]
    regions = [a for a in args if a in H.REGIONS] or list(H.REGIONS)
    chars = None
    rest = [a for a in args if a not in H.REGIONS]
    if rest:
        chars = list("".join(rest))
    for rg in regions:
        rows = verify(rg, chars, tag=tag)
        print(rg, summary(rows))
        for r in rows:
            if r["flags"]:
                print(" ", r["char"], r["cp"], r["structure"], r["flags"])
