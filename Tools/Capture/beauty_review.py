"""T33 beauty review support: compares tier captures of the same frame (Fluide / Équilibrée vs Magnifique).

Doc 07 asks for a side-by-side review where Équilibrée keeps at least 85 % of the perceived beauty of Magnifique.
The verdict stays a human review of the images; this script adds measurable evidence for the same frames:
  - structural similarity (SSIM, luminance) of the whole frame and of its upper and lower halves;
  - colour fidelity: mean colour distance in CIE Lab (dE76);
  - detail energy: high-frequency content (Laplacian) relative to Magnifique, a proxy for "richness".
Usage: python beauty_review.py <prefix> [folder]   e.g. python beauty_review.py T33h TestResults/Captures
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def load(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64) / 255.0


def luminance(rgb):
    return rgb @ np.array([0.2126, 0.7152, 0.0722])


def box(img, k=7):
    """Mean filter with a k x k window (valid region), via integral images."""
    s = np.pad(img, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
    return (s[k:, k:] - s[:-k, k:] - s[k:, :-k] + s[:-k, :-k]) / (k * k)


def ssim(a, b):
    c1, c2 = 0.01 ** 2, 0.03 ** 2
    ma, mb = box(a), box(b)
    va = box(a * a) - ma * ma
    vb = box(b * b) - mb * mb
    cov = box(a * b) - ma * mb
    m = ((2 * ma * mb + c1) * (2 * cov + c2)) / ((ma * ma + mb * mb + c1) * (va + vb + c2))
    return float(m.mean())


def lab(rgb):
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    xyz = lin @ np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]).T
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def detail(gray):
    lap = 4 * gray[1:-1, 1:-1] - gray[:-2, 1:-1] - gray[2:, 1:-1] - gray[1:-1, :-2] - gray[1:-1, 2:]
    return float(np.abs(lap).mean())


def compare(reference, other):
    ga, gb = luminance(reference), luminance(other)
    h = ga.shape[0] // 2
    return {
        "ssim": round(ssim(ga, gb), 4),
        "ssimTop": round(ssim(ga[:h], gb[:h]), 4),
        "ssimBottom": round(ssim(ga[h:], gb[h:]), 4),
        "deltaE": round(float(np.linalg.norm(lab(reference) - lab(other), axis=-1).mean()), 3),
        "detailRatio": round(detail(gb) / max(detail(ga), 1e-9), 4),
    }


def main():
    prefix = sys.argv[1]
    folder = Path(sys.argv[2] if len(sys.argv) > 2 else "TestResults/Captures")
    high = load(folder / f"{prefix}_world_High.png")
    result = {"prefix": prefix}
    for tier in ("Medium", "Low"):
        result[tier] = compare(high, load(folder / f"{prefix}_world_{tier}.png"))
    out = folder / f"{prefix}_beauty_metrics.json"
    out.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
