"""
J-AMB-001 Grassland day ambience (T34B): soft breeze, grass rustle in the gusts, a few distant birds.
Rendered into a circular 75 s buffer: every layer (filtered noise, gust curves, events, reverb) is periodic, so the
loop has no seam. No bird call on a fixed period and no repeated melody (doc 08).
"""
import numpy as np

from pw_audio_dsp import (SR, attack_curve, band_noise, bandpass, chain, convolve_circular, db, fft_filter, highpass,
                          lowpass, pan, peak, phase_of, pink, reverb_ir, samples)

LOOP_SECONDS = 75.0


def periodic_curve(n, rng, cycles, weights):
    """Smooth periodic curve in [0, 1]: sum of sines with whole cycles per loop (joins itself)."""
    t = np.arange(n) / n
    x = np.zeros(n)
    for c, w in zip(cycles, weights):
        x += w * np.sin(2 * np.pi * c * t + rng.uniform(0, 2 * np.pi))
    x -= x.min()
    return x / (x.max() + 1e-12)


def bird_call(kind, rng):
    """A short, soft, distant call. kind 0: two-note whistle; 1: small trill; 2: two quick chirps."""
    if kind == 0:
        seconds = 0.55
        t = np.arange(samples(seconds)) / SR
        base = rng.uniform(2700.0, 3300.0)
        f = np.where(t < 0.2, base * (1.0 + 0.06 * t / 0.2), base * rng.uniform(0.8, 0.88))
        f = fft_filter(f - f.mean(), lowpass(40.0, 2)) + f.mean()
        env = np.where(t < 0.2, np.sin(np.pi * np.clip(t / 0.2, 0, 1)), np.sin(np.pi * np.clip((t - 0.24) / 0.3, 0, 1)))
        env *= (t < 0.2) | (t > 0.24)
        return np.sin(phase_of(f)) * env
    if kind == 1:
        pips = int(rng.integers(6, 11))
        rate = rng.uniform(15.0, 20.0)
        seconds = pips / rate + 0.1
        t = np.arange(samples(seconds)) / SR
        base = rng.uniform(3800.0, 4600.0)
        phase_in_pip = (t * rate) % 1.0
        f = base * (1.0 + 0.08 * (1.0 - phase_in_pip))
        env = np.sin(np.pi * phase_in_pip) ** 2 * (t * rate < pips)
        env *= np.linspace(1.0, 0.6, len(t))
        return np.sin(phase_of(f)) * env
    seconds = 0.4
    t = np.arange(samples(seconds)) / SR
    out = np.zeros(len(t))
    for start in (0.0, rng.uniform(0.14, 0.2)):
        seg = (t >= start) & (t < start + 0.09)
        tt = np.clip(t - start, 0, 0.09)
        f = rng.uniform(2400.0, 2700.0) + 1100.0 * (tt / 0.09)
        out += np.sin(phase_of(f)) * np.sin(np.pi * tt / 0.09) * seg
    return out


def grassland_day(seed=34):
    rng = np.random.default_rng(seed)
    n = samples(LOOP_SECONDS)
    t = np.arange(n) / SR

    # Gusts: slow periodic swell (whole cycles per loop) and a slower breathing underneath.
    gust = periodic_curve(n, rng, (3, 5, 8, 13), (1.0, 0.7, 0.45, 0.25))
    gust = 0.35 + 0.65 * gust ** 1.4

    # Breeze body: two decorrelated pink noises, warm band, circularly filtered (periodic).
    breeze = np.zeros((n, 2))
    common = pink(n, rng, circular=True)
    for c in range(2):
        own = pink(n, rng, circular=True)
        mixed = 0.55 * common + 0.45 * own
        breeze[:, c] = fft_filter(mixed, chain(bandpass(140.0, 1100.0, 2), peak(450.0, 2.0, 1.5)), pad=False)
    breeze /= np.std(breeze) + 1e-12
    breeze *= gust[:, None]

    # Grass rustle: brighter layer that only rises with the gusts.
    rustle = np.zeros((n, 2))
    for c in range(2):
        rustle[:, c] = fft_filter(rng.standard_normal(n), chain(bandpass(1200.0, 5200.0, 2), peak(2800.0, -3.0, 1.2)),
                                  pad=False)
    rustle /= np.std(rustle) + 1e-12
    rustle_env = np.clip((gust - 0.55) / 0.45, 0.0, 1.0) ** 1.5
    # Small fast flutter inside the rustle (leaves), periodic too.
    flutter = periodic_curve(n, rng, (311, 457, 613), (1.0, 0.8, 0.6))
    rustle *= (rustle_env * (0.6 + 0.4 * flutter))[:, None]

    # Leaf grains: sparse short rustles, more of them in the gusts.
    grains = np.zeros((n, 2))
    grain_count = 70
    for _ in range(grain_count):
        at = int(rng.integers(0, n))
        if rng.random() > gust[at]:
            continue
        length = samples(rng.uniform(0.03, 0.09))
        g = fft_filter(rng.standard_normal(length), bandpass(2000.0, 6500.0, 2))
        g *= np.sin(np.pi * np.arange(length) / length) ** 2
        g /= np.max(np.abs(g)) + 1e-12
        stereo = pan(g * rng.uniform(0.3, 1.0), rng.uniform(-0.8, 0.8))
        idx = (at + np.arange(length)) % n
        np.add.at(grains, idx, stereo)

    # Distant birds: 8 calls at irregular times (never closer than 5 s), random kind, pitch and side.
    birds = np.zeros((n, 2))
    times = []
    while len(times) < 8:
        candidate = rng.uniform(0.0, LOOP_SECONDS)
        if all(min(abs(candidate - s), LOOP_SECONDS - abs(candidate - s)) > 5.0 for s in times):
            times.append(candidate)
    for at in times:
        kind = int(rng.integers(0, 3))
        call = bird_call(kind, rng)
        call = fft_filter(call, chain(lowpass(5200.0, 2), highpass(1500.0, 2)))
        call *= rng.uniform(0.5, 1.0)
        # A call is sometimes answered softly a little later, from elsewhere.
        stereo = pan(call, rng.uniform(-0.7, 0.7))
        idx = (samples(at) + np.arange(len(call))) % n
        np.add.at(birds, idx, stereo)
        if rng.random() < 0.35:
            echo = pan(bird_call(kind, rng) * 0.45, rng.uniform(-0.8, 0.8))
            echo = fft_filter(echo, lowpass(4200.0, 2))
            idx = (samples(at + rng.uniform(0.9, 1.6)) + np.arange(len(echo))) % n
            np.add.at(birds, idx, echo)
    birds_wet = convolve_circular(birds, reverb_ir(2.2, 1.4, damping_hz=3500.0, predelay=0.03, seed=35))

    mix = (breeze * db(0.0)
           + rustle * db(-14.0)
           + grains * db(-24.0)
           + birds * db(-27.0) + birds_wet * db(-25.0))
    # Gentle master: no rumble (phones), soften the 2-5 kHz band, keep it airy.
    mix = fft_filter(mix, chain(highpass(90.0, 2), peak(3500.0, -2.0, 1.4), lowpass(9000.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-6.0)
