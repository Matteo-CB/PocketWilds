"""
T50 Forest audio (own DSP, no sample, no library): AMB_Forest_Day, AMB_Forest_Evening and MUS_ForestVariation.

The two ambience beds follow the Grassland ones (pw_audio_ambience.py): circular buffers, so every layer, the reverb
included, joins itself and the loop has no seam. No call on a fixed period, no repeated melody. The variation is the
refuge theme (pw_audio_music.py: same F major harmony, same phrases, same 64 bars = 192 s, same loop point) played by
a warmer, woodier band: a breathy wooden flute and a marimba for the lines, nylon plucks for the arpeggios, a rounder
pad and a darker, longer room.
"""
import numpy as np

from pw_audio_ambience import bird_call, cricket_track, periodic_curve
from pw_audio_dsp import (SR, attack_curve, bandpass, chain, convolve_circular, db, fft_filter, highpass, hz, lowpass,
                          pan, partials, peak, phase_of, pink, reverb_ir, samples)
import pw_audio_music as M

FOREST_DAY_SECONDS = 80.0
FOREST_EVENING_SECONDS = 72.0


# ---------------------------------------------------------------- ambience voices

def wood_pigeon(rng):
    """A soft, distant wood pigeon: coo, a longer COO, a short coo. Low, round, hollow."""
    base = rng.uniform(430.0, 520.0)
    out = np.zeros(samples(1.2))
    t = np.arange(len(out)) / SR
    for start, length, ratio in ((0.0, 0.22, 1.0), (0.3, 0.42, 1.12), (0.82, 0.2, 0.95)):
        local = np.clip(t - start, 0.0, length)
        f = base * ratio * (1.0 - 0.05 * local / length)
        env = np.sin(np.pi * local / length) ** 1.5 * ((t >= start) & (t < start + length))
        ph = phase_of(f)
        out += (np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph)) * env
    return out


def wren_trill(rng):
    """A small run of rapid, falling pips with a soft tail, like a wren far in the undergrowth."""
    seconds = rng.uniform(0.9, 1.3)
    t = np.arange(samples(seconds)) / SR
    rate = rng.uniform(18.0, 24.0)
    phase_in = (t * rate) % 1.0
    base = rng.uniform(3300.0, 3900.0)
    f = base * (1.0 - 0.18 * t / seconds) * (1.0 + 0.1 * (1.0 - phase_in))
    env = np.sin(np.pi * phase_in) ** 2 * np.sin(np.pi * np.clip(t / seconds, 0, 1)) ** 0.7
    return np.sin(phase_of(f)) * env


def thrush_phrase(rng):
    """A fluty phrase of three or four short glides, each on its own pitch."""
    out = np.zeros(samples(1.6))
    t = np.arange(len(out)) / SR
    at = 0.0
    for _ in range(int(rng.integers(3, 5))):
        length = rng.uniform(0.12, 0.26)
        base = rng.uniform(1800.0, 2700.0)
        direction = rng.choice([-1.0, 1.0])
        local = np.clip(t - at, 0.0, length)
        f = base * (1.0 + direction * 0.25 * local / length)
        env = np.sin(np.pi * local / length) ** 1.3 * ((t >= at) & (t < at + length))
        ph = phase_of(f)
        out += (np.sin(ph) + 0.2 * np.sin(2 * ph)) * env
        at += length + rng.uniform(0.05, 0.2)
    return out


def insect_tone(rng):
    """A soft airy insect pass: a thin tone with a fast shimmer, a rise and a fall."""
    seconds = rng.uniform(2.0, 3.4)
    t = np.arange(samples(seconds)) / SR
    f = rng.uniform(5200.0, 6400.0) * (1.0 + 0.02 * np.sin(2 * np.pi * 0.7 * t))
    am = 0.6 + 0.4 * np.sin(2 * np.pi * rng.uniform(55.0, 90.0) * t)
    return np.sin(phase_of(f)) * am * np.sin(np.pi * t / seconds) ** 2


def owl_hoot(rng, long_tail):
    """A far tawny-owl hoot: a short 'hu' then a long, wavering 'hooo'. Low, hollow, almost a breath."""
    base = rng.uniform(340.0, 400.0)
    n = samples(2.4 if long_tail else 0.8)
    t = np.arange(n) / SR
    out = np.zeros(n)
    parts = ((0.0, 0.3, 1.08), (0.5, 1.5, 1.0)) if long_tail else ((0.0, 0.5, 1.0),)
    for start, length, ratio in parts:
        local = np.clip(t - start, 0.0, length)
        vib = 1.0 + 0.012 * np.sin(2 * np.pi * 6.0 * local) * np.clip(local / 0.5, 0, 1)
        f = base * ratio * vib * (1.0 - 0.04 * local / length)
        env = (np.clip(local / 0.12, 0, 1) * np.clip((length - local) / 0.25, 0, 1)) ** 1.2 * ((t >= start) & (t < start + length))
        ph = phase_of(f)
        out += (np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.06 * np.sin(3 * ph)) * env
    return out


def _place(buf, sig, at_seconds, position):
    n = len(buf)
    np.add.at(buf, (samples(at_seconds) + np.arange(len(sig))) % n, pan(sig, position))


# ---------------------------------------------------------------- ambience beds

def forest_day(seed=50):
    """
    AMB_Forest_Day: deeper than the meadow. A low canopy breath, leaves moving overhead in slow swells, a few leaf
    drops, soft distant birds (pigeon, wren, thrush) in a lot of forest air, now and then a thin insect tone.
    """
    rng = np.random.default_rng(seed)
    n = samples(FOREST_DAY_SECONDS)
    sway = periodic_curve(n, rng, (2, 4, 7, 11), (1.0, 0.7, 0.4, 0.2))
    sway = 0.3 + 0.7 * sway ** 1.3

    breath = np.zeros((n, 2))
    common = pink(n, rng, circular=True)
    for c in range(2):
        own = pink(n, rng, circular=True)
        breath[:, c] = fft_filter(0.55 * common + 0.45 * own, chain(bandpass(90.0, 700.0, 2), peak(260.0, 2.0, 1.5)), pad=False)
    breath /= np.std(breath) + 1e-12
    breath *= sway[:, None]

    leaves = np.zeros((n, 2))
    for c in range(2):
        leaves[:, c] = fft_filter(rng.standard_normal(n), chain(bandpass(700.0, 4200.0, 2), peak(1800.0, -2.0, 1.2)), pad=False)
    leaves /= np.std(leaves) + 1e-12
    leaf_env = np.clip((sway - 0.35) / 0.65, 0.0, 1.0) ** 1.3
    flutter = periodic_curve(n, rng, (97, 163, 241), (1.0, 0.8, 0.6))
    leaves *= (leaf_env * (0.55 + 0.45 * flutter))[:, None]

    drops = np.zeros((n, 2))
    for _ in range(26):
        at = int(rng.integers(0, n))
        length = samples(rng.uniform(0.03, 0.07))
        tt = np.arange(length) / SR
        g = fft_filter(rng.standard_normal(length), bandpass(900.0, 3200.0, 2)) * np.exp(-tt / 0.012) * np.clip(tt / 0.002, 0, 1)
        g /= np.max(np.abs(g)) + 1e-12
        np.add.at(drops, (at + np.arange(length)) % n, pan(g * rng.uniform(0.3, 1.0), rng.uniform(-0.8, 0.8)))

    birds = np.zeros((n, 2))
    times = []
    while len(times) < 9:
        candidate = rng.uniform(0.0, FOREST_DAY_SECONDS)
        if all(min(abs(candidate - s), FOREST_DAY_SECONDS - abs(candidate - s)) > 6.0 for s in times):
            times.append(candidate)
    for at in times:
        kind = int(rng.integers(0, 4))
        if kind == 0:
            call = fft_filter(wood_pigeon(rng), chain(lowpass(1400.0, 2), highpass(250.0, 2))) * 1.4
        elif kind == 1:
            call = fft_filter(wren_trill(rng), chain(lowpass(6000.0, 2), highpass(2000.0, 2)))
        elif kind == 2:
            call = fft_filter(thrush_phrase(rng), chain(lowpass(4200.0, 2), highpass(1200.0, 2)))
        else:
            call = fft_filter(bird_call(int(rng.integers(0, 3)), rng), chain(lowpass(4800.0, 2), highpass(1500.0, 2)))
        _place(birds, call * rng.uniform(0.5, 1.0), at, rng.uniform(-0.8, 0.8))
    birds_wet = convolve_circular(birds, reverb_ir(3.2, 2.2, damping_hz=2800.0, predelay=0.04, seed=51))

    insects = np.zeros((n, 2))
    for at in (9.0, 31.0, 47.5, 66.0):
        _place(insects, insect_tone(rng), at + rng.uniform(-2.0, 2.0), rng.uniform(-0.7, 0.7))
    insects = fft_filter(insects, chain(highpass(3500.0, 2), lowpass(7500.0, 2)), pad=False)

    mix = (breath * db(0.0) + leaves * db(-12.0) + drops * db(-26.0)
           + birds * db(-27.0) + birds_wet * db(-23.0) + insects * db(-36.0))
    mix = fft_filter(mix, chain(highpass(80.0, 2), peak(3200.0, -2.0, 1.4), lowpass(8500.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-6.0)


def forest_evening(seed=52):
    """
    AMB_Forest_Evening: the Forest at dusk and night. A hushed canopy breath, a few softer and darker crickets than
    the meadow's, distant owl hoots in deep reverb.
    """
    rng = np.random.default_rng(seed)
    n = samples(FOREST_EVENING_SECONDS)
    sway = periodic_curve(n, rng, (2, 3, 5), (1.0, 0.6, 0.3))
    sway = 0.4 + 0.6 * sway ** 1.3
    breath = np.zeros((n, 2))
    common = pink(n, rng, circular=True)
    for c in range(2):
        own = pink(n, rng, circular=True)
        breath[:, c] = fft_filter(0.6 * common + 0.4 * own, chain(bandpass(80.0, 520.0, 2), peak(220.0, 2.0, 1.4)), pad=False)
    breath /= np.std(breath) + 1e-12
    breath *= sway[:, None]
    whisper = np.zeros((n, 2))
    for c in range(2):
        whisper[:, c] = fft_filter(rng.standard_normal(n), bandpass(900.0, 3000.0, 2), pad=False)
    whisper /= np.std(whisper) + 1e-12
    whisper *= (np.clip((sway - 0.6) / 0.4, 0.0, 1.0) ** 1.5)[:, None]

    crickets = np.zeros((n, 2))
    for _ in range(4):
        track = cricket_track(n, rng, rng.uniform(3500.0, 4400.0), rng.uniform(0.6, 1.1), int(rng.integers(3, 5)),
                              rng.uniform(24.0, 32.0), rng.uniform(0.3, 0.8))
        distance = rng.uniform(0.2, 1.0)
        track = fft_filter(track, lowpass(5500.0 - 2200.0 * distance, 2), pad=False) * (1.0 - 0.6 * distance)
        crickets += pan(track, rng.uniform(-0.8, 0.8))
    crickets_wet = convolve_circular(crickets, reverb_ir(2.6, 1.8, damping_hz=2800.0, predelay=0.03, seed=53))

    owls = np.zeros((n, 2))
    for at, tail in ((7.0, True), (29.0, False), (33.0, True), (58.0, True)):
        hoot = fft_filter(owl_hoot(rng, tail), chain(lowpass(1100.0, 2), highpass(180.0, 2)))
        _place(owls, hoot * rng.uniform(0.6, 1.0), at + rng.uniform(-1.5, 1.5), rng.uniform(-0.8, 0.8))
    owls_wet = convolve_circular(owls, reverb_ir(3.6, 2.8, damping_hz=1800.0, predelay=0.06, seed=54))

    mix = (breath * db(0.0) + whisper * db(-18.0) + crickets * db(-33.0) + crickets_wet * db(-28.0)
           + owls * db(-27.0) + owls_wet * db(-22.0))
    mix = fft_filter(mix, chain(highpass(70.0, 2), peak(4000.0, -2.0, 1.0), lowpass(7500.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-6.0)


# ---------------------------------------------------------------- variation: woody voices

def wood_flute(f, length, vel, rng):
    """A breathy wooden flute: a soft attack, a gentle late vibrato, weak upper partials, a little breath noise."""
    total = length + 0.5
    t = np.arange(samples(total)) / SR
    vib = 1.0 + 0.0035 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.25) / 0.4, 0.0, 1.0)
    ph = phase_of(f * vib)
    tone = np.sin(ph) + 0.22 * np.sin(2 * ph + 0.4) + 0.07 * np.sin(3 * ph + 0.9) + 0.02 * np.sin(4 * ph)
    env = np.clip(t / 0.075, 0.0, 1.0)
    env = 0.5 - 0.5 * np.cos(np.pi * env)
    env *= np.where(t < length, 1.0 - 0.12 * t / max(length, 1e-6), (1.0 - 0.12) * np.exp(-(t - length) / 0.11))
    breath = fft_filter(rng.standard_normal(len(t)), bandpass(1.5 * f, 4.0 * f, 2)) * 0.05 * np.clip(t / 0.02, 0, 1)
    breath *= np.exp(-t / 0.35) + 0.25
    x = (tone * 0.55 + breath) * env
    x[len(x) - samples(0.02):] *= attack_curve(samples(0.02))[::-1]
    return x * vel


def nylon_pluck(f, length, vel, rng):
    """A nylon string: a quick pluck, odd partials fading faster, a small wooden body knock."""
    tau = 1.3 * (330.0 / f) ** 0.4
    t = np.arange(samples(max(length + 0.5, 4.0 * tau))) / SR
    spec = [(k, k ** -1.1, tau / (1.0 + 0.9 * (k - 1))) for k in range(1, 7)]
    x = partials(t, f, spec, attack=0.0015, phase_rng=rng)
    nk = samples(0.03)
    tk = np.arange(nk) / SR
    knock = fft_filter(rng.standard_normal(nk) * np.exp(-tk / 0.004), bandpass(180.0, 900.0, 2)) * 0.08
    x[:nk] += knock * attack_curve(nk)[::-1]
    return x * 0.6 * vel


def forest_variation(seed=150):
    """
    MUS_ForestVariation: the refuge theme (same chords, same phrases, same 192 s loop) in the Forest's timbres. Flute
    on the theme, marimba for the tine part, plucks for the arpeggios, a warmer pad, no music box glass: marimba pings
    for the counter line, wood knocks for the shaker, a darker and longer room.
    """
    rng = np.random.default_rng(seed)
    jitter = lambda: rng.normal(0.0, 0.005)
    human = lambda: 1.0 + rng.uniform(-0.08, 0.08)
    lead = np.zeros((M.LOOP, 2))
    arps = np.zeros((M.LOOP, 2))
    pad = np.zeros((M.LOOP, 2))
    bass = np.zeros((M.LOOP, 2))
    bells = np.zeros((M.LOOP, 2))
    perc = np.zeros((M.LOOP, 2))
    add = M._add

    marimba_lead = M._marimba_voice(rng, 0.5)
    for instrument, first_bar, phrases in M.LEAD:
        for i, bar_notes in enumerate(phrases):
            bar = first_bar + i
            for note, start8, len8 in bar_notes:
                at = bar * M.BAR + start8 * M.EIGHTH + jitter()
                length = len8 * M.EIGHTH
                vel = 0.78 * human() * (0.75 if 32 <= bar < 48 else 1.0)
                f = hz(note)
                if instrument == 'tine':
                    add(lead, marimba_lead(f, length, vel * 0.95), at, 0.5, -0.1)
                elif 32 <= bar < 48:
                    # The breathing-room part: the flute alone, softer.
                    add(lead, wood_flute(f, length, vel * 0.9, rng), at, 0.5, 0.05)
                else:
                    add(lead, wood_flute(f, length, vel, rng), at, 0.5, 0.05)

    # Counter line: marimba pings in the second half of B, high and sparse ones in C.
    for i, note in enumerate(M.COUNTER):
        add(bells, M.marimba(hz(note) / 2.0, 0.5 * human(), rng, 0.8), (24 + i) * M.BAR + 2 * M.EIGHTH + jitter(), 0.3, 0.35)
    for bar, note in ((41, 'A5'), (43, 'C6'), (45, 'F5')):
        add(bells, M.marimba(hz(note), 0.4, rng, 0.7), bar * M.BAR + jitter(), 0.25, -0.3)

    # Plucked arpeggios over the chord tones, below the melody.
    for bar, name in enumerate(M.CHORDS):
        voicing = [hz(n) for n in M.CH[name][0]]
        tones = sorted(set([v * 2 for v in voicing if v * 2 < hz('F5')] + [v for v in voicing if v >= hz('G3')]))
        tones = [f for f in tones if hz('G3') <= f <= hz('F5')]
        cycle = tones + tones[-2:0:-1]
        for j, step in enumerate(M.arp_steps(bar)):
            f = cycle[(j + bar) % len(cycle)]
            vel = (0.5 if step % 2 == 0 else 0.4) * human()
            add(arps, nylon_pluck(f, M.EIGHTH, vel, rng), bar * M.BAR + step * M.EIGHTH + jitter(), 0.24, rng.uniform(-0.35, 0.35))

    # Pad: PADsynth chords, rounder (softened above 1.8 kHz), other table seeds than the theme's.
    for bar, name in enumerate(M.CHORDS):
        level = 0.75 if 32 <= bar < 40 else 1.0
        length = samples(M.BAR + 1.8)
        env = np.ones(length)
        env[:samples(0.7)] = attack_curve(samples(0.7))
        release = samples(1.8)
        env[length - release:] *= np.exp(-np.linspace(0, 4.0, release))
        env[length - samples(0.05):] *= attack_curve(samples(0.05))[::-1]
        for k, note in enumerate(M.CH[name][0]):
            table = M.pad_table(hz(note), 2000 + k)
            offset = int(rng.integers(0, len(table)))
            idx = (offset + np.arange(length)) % len(table)
            add(pad, table[idx] * env[:, None] * 0.25 * level, bar * M.BAR - 0.35, 0.22)
    pad = fft_filter(pad, lowpass(1800.0, 2), pad=False)

    # Bass, as in the theme.
    for bar, name in enumerate(M.CHORDS):
        root = hz(M.CH[name][1])
        if 32 <= bar < 36:
            continue
        if 16 <= bar < 32 or 36 <= bar < 48:
            hits = ((0, 4, root), (4, 4, root * (1.5 if name not in ('C/E', 'F/A') else 1.0)))
        else:
            hits = ((0, 7, root),) if bar % 2 == 0 else ((0, 4, root), (4, 3, root))
        for start8, len8, f in hits:
            f = f / 2 if f > hz('E3') else f
            add(bass, M.round_bass(f, len8 * M.EIGHTH, 0.8 * human()), bar * M.BAR + start8 * M.EIGHTH + jitter(), 0.15, 0.0)

    # Soft wood knocks where the theme has its shaker.
    for bar in range(M.BARS):
        if 16 <= bar < 32:
            steps = (1, 3, 5, 7)
        elif 56 <= bar < 63:
            steps = (3, 7)
        else:
            continue
        for step in steps:
            n = samples(0.06)
            tk = np.arange(n) / SR
            knock = fft_filter(rng.standard_normal(n), bandpass(700.0, 2200.0, 2)) * np.exp(-tk / 0.012) * np.clip(tk / 0.002, 0, 1)
            knock *= (0.9 if step in (3, 7) else 0.6) * human()
            add(perc, knock, bar * M.BAR + step * M.EIGHTH + rng.normal(0.0, 0.006), 0.07, rng.uniform(-0.5, 0.5))

    dry = lead + arps + pad + bass + bells + perc
    sends = lead * 0.3 + arps * 0.28 + pad * 0.3 + bells * 0.35 + perc * 0.12 + bass * 0.04
    wet = convolve_circular(sends, reverb_ir(3.6, 2.8, damping_hz=2600.0, predelay=0.03, seed=151, early=0.25))
    mix = dry + wet * 0.9
    mix = fft_filter(mix, chain(highpass(45.0, 2), peak(2500.0, -1.0, 1.2), lowpass(9500.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-3.0)
