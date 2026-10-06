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


# ---------------------------------------------------------------- T34B2: evening and rain layers

EVENING_SECONDS = 60.0
RAIN_SECONDS = 30.0


def cricket_track(n, rng, carrier, chirp_every, pulses, pulse_rate, level):
    """One distant cricket: chirps of a few rapid pulses on a high carrier, irregular gaps, circular."""
    out = np.zeros(n)
    t_chirp = np.arange(samples(pulses / pulse_rate + 0.01)) / SR
    phase_in = (t_chirp * pulse_rate) % 1.0
    am = np.sin(np.pi * phase_in) ** 2 * (t_chirp * pulse_rate < pulses)
    at = rng.uniform(0.0, chirp_every)
    total = n / SR
    while at < total:
        f = carrier * rng.uniform(0.995, 1.005)
        chirp = np.sin(2 * np.pi * f * t_chirp) * am * rng.uniform(0.75, 1.0)
        idx = (samples(at) + np.arange(len(chirp))) % n
        np.add.at(out, idx, chirp)
        at += chirp_every * rng.uniform(0.75, 1.35)
        # Now and then a cricket pauses for a few seconds.
        if rng.random() < 0.06:
            at += rng.uniform(2.0, 6.0)
    return out * level


def grassland_evening(seed=35):
    """
    Evening and night layer of the meadow: a light breeze and a few distant crickets at different distances, no bird
    song. Crossfaded with the day loop by the local hour (and by the night mood of events).
    """
    rng = np.random.default_rng(seed)
    n = samples(EVENING_SECONDS)
    gust = periodic_curve(n, rng, (2, 3, 5), (1.0, 0.6, 0.3))
    gust = 0.4 + 0.6 * gust ** 1.3
    breeze = np.zeros((n, 2))
    common = pink(n, rng, circular=True)
    for c in range(2):
        own = pink(n, rng, circular=True)
        breeze[:, c] = fft_filter(0.6 * common + 0.4 * own, chain(bandpass(120.0, 800.0, 2), peak(380.0, 1.5, 1.4)), pad=False)
    breeze /= np.std(breeze) + 1e-12
    breeze *= gust[:, None]

    crickets = np.zeros((n, 2))
    for k in range(6):
        carrier = rng.uniform(3900.0, 4900.0)
        track = cricket_track(n, rng, carrier, rng.uniform(0.45, 0.9), int(rng.integers(3, 5)), rng.uniform(26.0, 34.0),
                              rng.uniform(0.35, 1.0))
        # Farther crickets are darker and quieter.
        distance = rng.uniform(0.0, 1.0)
        track = fft_filter(track, lowpass(6500.0 - 2500.0 * distance, 2), pad=False) * (1.0 - 0.6 * distance)
        crickets += pan(track, rng.uniform(-0.8, 0.8))
    crickets_wet = convolve_circular(crickets, reverb_ir(1.8, 1.2, damping_hz=3000.0, predelay=0.02, seed=36))
    mix = breeze * db(0.0) + crickets * db(-30.0) + crickets_wet * db(-27.0)
    mix = fft_filter(mix, chain(highpass(80.0, 2), peak(4300.0, -2.0, 1.0), lowpass(8000.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-6.0)


def rain_light(seed=37):
    """
    J-WEATHER-RainLight: soft rain on grass and leaves, layered over the meadow by the shower's strength. A hushed
    bed, thousands of tiny drops, a few round drips from the leaves. Circular, so it loops without a seam.
    """
    rng = np.random.default_rng(seed)
    n = samples(RAIN_SECONDS)
    bed = np.zeros((n, 2))
    for c in range(2):
        bed[:, c] = fft_filter(pink(n, rng, circular=True), chain(bandpass(500.0, 7000.0, 2), peak(2500.0, -3.0, 1.5)), pad=False)
    bed /= np.std(bed) + 1e-12

    drops = np.zeros((n, 2))
    count = int(140 * RAIN_SECONDS)
    for _ in range(count):
        length = samples(rng.uniform(0.002, 0.006))
        t = np.arange(length) / SR
        f = rng.uniform(2200.0, 7000.0)
        drop = np.sin(2 * np.pi * f * t) * np.exp(-t / rng.uniform(0.0008, 0.0025)) * rng.uniform(0.2, 1.0)
        stereo = pan(drop, rng.uniform(-0.9, 0.9))
        idx = (int(rng.integers(0, n)) + np.arange(length)) % n
        np.add.at(drops, idx, stereo)
    drops = fft_filter(drops, chain(lowpass(8000.0, 2), peak(4000.0, -3.0, 1.0)), pad=False)

    drips = np.zeros((n, 2))
    for _ in range(int(2.2 * RAIN_SECONDS)):
        length = samples(0.06)
        t = np.arange(length) / SR
        f0 = rng.uniform(700.0, 1400.0)
        f = f0 * (1.0 + 0.5 * np.exp(-t / 0.01))
        drip = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.018) * np.clip(t / 0.0015, 0, 1) * rng.uniform(0.4, 1.0)
        stereo = pan(drip, rng.uniform(-0.8, 0.8))
        idx = (int(rng.integers(0, n)) + np.arange(length)) % n
        np.add.at(drips, idx, stereo)

    mix = bed * db(0.0) + drops * (np.std(bed) / (np.std(drops) + 1e-12)) * db(-6.0) + drips * db(-24.0)
    mix = fft_filter(mix, chain(highpass(150.0, 2), lowpass(9500.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-6.0)
