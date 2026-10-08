"""
J-MUS-001 RefugeTheme (T34B): the refuge's gentle loop, composed and rendered here (no sample, no library).

F major, 80 BPM, 64 bars = 192 s exactly (36 000 samples per beat at 48 kHz, 33 075 at 44.1 kHz: the loop point is
a whole sample at both rates). Four parts: A (intro, theme), B (tine lead, music box counter line), C (breathing
room, sparse), A' (theme returns, ends on space so the intro follows naturally). Low density, no drums, no vocals.
Every voice is written into a circular buffer, reverb included, so bar 64 flows into bar 1 without a seam.
"""
import numpy as np

from pw_audio_dsp import (SR, attack_curve, band_noise, bandpass, chain, convolve_circular, db, fft_filter, fm_bell,
                          highpass, hz, lowpass, partials, peak, reverb_ir, samples)

BPM = 80
BEAT = 60.0 / BPM
BAR = 4 * BEAT
EIGHTH = BEAT / 2
BARS = 64
LOOP = samples(BARS * BAR)

# Chord per bar: (pad voicing, bass note).
CH = {
    'F': (['F3', 'C4', 'F4', 'A4'], 'F2'),
    'C/E': (['E3', 'C4', 'E4', 'G4'], 'E2'),
    'Dm7': (['D3', 'A3', 'C4', 'F4'], 'D2'),
    'Bbadd9': (['F3', 'Bb3', 'C4', 'D4'], 'Bb2'),
    'Bb': (['F3', 'Bb3', 'D4', 'F4'], 'Bb2'),
    'F/A': (['A3', 'C4', 'F4', 'A4'], 'A2'),
    'Gm7': (['G3', 'Bb3', 'D4', 'F4'], 'G2'),
    'Csus4': (['G3', 'C4', 'F4', 'G4'], 'C3'),
    'C': (['G3', 'C4', 'E4', 'G4'], 'C3'),
    'Am7': (['A3', 'C4', 'E4', 'G4'], 'A2'),
    'Bbmaj7': (['F3', 'A3', 'Bb3', 'D4'], 'Bb2'),
}

CHORDS = (
    # A: intro (1-4), theme (5-12), answer (13-16)
    'F', 'C/E', 'Dm7', 'Bbadd9', 'F', 'C/E', 'Dm7', 'Bb', 'F/A', 'Gm7', 'Csus4', 'C', 'F/A', 'Gm7', 'Bb', 'C',
    # B
    'Bb', 'F/A', 'Gm7', 'C', 'Dm7', 'Am7', 'Bb', 'Csus4', 'Bb', 'F/A', 'Gm7', 'C', 'Dm7', 'Am7', 'Bb', 'C',
    # C
    'Dm7', 'Bbmaj7', 'F', 'C', 'Dm7', 'Bbmaj7', 'Gm7', 'Csus4', 'Dm7', 'Bbmaj7', 'F', 'C/E', 'Dm7', 'Bbmaj7', 'Gm7', 'C',
    # A'
    'F', 'C/E', 'Dm7', 'Bb', 'F/A', 'Gm7', 'Csus4', 'C', 'F', 'C/E', 'Dm7', 'Bb', 'F/A', 'Gm7', 'Bb', 'C',
)
assert len(CHORDS) == BARS

# Melody phrases: (note, start in eighths, length in eighths).
P1 = [[('A4', 0, 2), ('C5', 2, 2), ('F5', 4, 4)],
      [('E5', 0, 3), ('D5', 3, 1), ('C5', 4, 4)],
      [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('G5', 6, 2)],
      [('F5', 0, 6), ('D5', 6, 2)]]
P1B = [[('A4', 0, 1), ('C5', 1, 1), ('F5', 2, 2), ('A5', 4, 4)],
       [('G5', 0, 3), ('E5', 3, 1), ('C5', 4, 4)],
       [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('G5', 6, 2)],
       [('F5', 0, 4), ('D5', 4, 2), ('C5', 6, 2)]]
P2 = [[('C5', 0, 2), ('F5', 2, 1), ('G5', 3, 1), ('A5', 4, 4)],
      [('Bb5', 0, 2), ('A5', 2, 2), ('G5', 4, 2), ('F5', 6, 2)],
      [('G5', 0, 4), ('F5', 4, 4)],
      [('E5', 0, 6)]]
P3 = [[('A5', 0, 2), ('G5', 2, 2), ('F5', 4, 4)],
      [('D5', 0, 2), ('F5', 2, 2), ('G5', 4, 4)],
      [('F5', 0, 3), ('D5', 3, 1), ('C5', 4, 4)],
      [('E5', 0, 6)]]
P3_END = P3[:3] + [[]]
PB = [[('D5', 0, 1), ('F5', 1, 1), ('Bb5', 2, 2), ('A5', 4, 4)],
      [('C6', 0, 2), ('A5', 2, 2), ('F5', 4, 4)],
      [('G5', 0, 2), ('Bb5', 2, 2), ('D6', 4, 2), ('C6', 6, 2)],
      [('C6', 0, 4), ('Bb5', 4, 2), ('G5', 6, 2)],
      [('A5', 0, 3), ('G5', 3, 1), ('F5', 4, 2), ('A5', 6, 2)],
      [('G5', 0, 2), ('E5', 2, 2), ('C5', 4, 4)],
      [('D5', 0, 2), ('F5', 2, 2), ('Bb5', 4, 2), ('A5', 6, 2)],
      [('G5', 0, 6)]]
PB_END = PB[:6] + [[('F5', 0, 2), ('D5', 2, 2), ('C5', 4, 4)], []]
PC = [[('A5', 0, 8)], [('F5', 0, 4), ('D5', 4, 4)], [('C6', 0, 4), ('A5', 4, 4)], [('G5', 0, 8)],
      [('F5', 0, 4), ('A5', 4, 4)], [('D6', 0, 8)], [('Bb5', 0, 4), ('A5', 4, 2), ('G5', 6, 2)], [('G5', 0, 8)]]
PC_END = [[], [], [], [], [], [], [('F5', 0, 4), ('G5', 4, 4)], [('E5', 4, 4)]]
COUNTER = ['D6', 'C6', 'D6', 'E6', 'F6', 'E6', 'D6', 'E6']

# (instrument, bar index 0-based, phrase list)
LEAD = [('piano', 4, P1), ('piano', 8, P2), ('piano', 12, P3),
        ('tine', 16, PB), ('tine', 24, PB_END),
        ('piano', 32, PC), ('piano', 40, PC_END),
        ('piano', 48, P1), ('piano', 52, P2), ('piano', 56, P1B), ('piano', 60, P3_END)]

# Arpeggio rhythm (eighths) per bar: sparse, syncopated, thinner in C.
def arp_steps(bar):
    if 16 <= bar < 32:
        return (0, 2, 3, 4, 6, 7)
    if 32 <= bar < 40:
        return (0, 3, 6)
    return (0, 2, 3, 5, 6)


def section_gain(bar_position):
    """Dynamics over the loop (A 0.85, B 1.0, C 0.75, A' 0.95), smoothed over two bars, circular."""
    levels = np.array([0.85] * 16 + [1.0] * 16 + [0.75] * 16 + [0.95] * 16)
    x = bar_position % BARS
    i = int(np.floor(x))
    frac = x - i
    a, b = levels[i], levels[(i + 1) % BARS]
    return a + (b - a) * frac


# ---------------------------------------------------------------- instruments

def felt_piano(f, length, vel, rng, release=0.25):
    """Soft felt piano: slightly stretched partials, two strings a hair apart, hammer thump, damper release."""
    total = length + release * 6
    t = np.arange(samples(total)) / SR
    tau1 = 2.4 * (262.0 / f) ** 0.5
    rolloff = 1.7 - 0.6 * vel
    out = np.zeros(len(t))
    for detune in (-0.3, 0.3):
        fs = f * 2 ** (detune / 1200.0)
        for k in range(1, 9):
            fk = fs * k * np.sqrt(1.0 + 0.0002 * k * k)
            if fk > SR * 0.45:
                break
            out += (k ** -rolloff) * np.sin(2 * np.pi * fk * t + rng.uniform(0, 6.28)) * np.exp(-t / (tau1 / (1 + 0.55 * (k - 1))))
    out *= 0.5
    hammer = fft_filter(rng.standard_normal(len(t)) * np.exp(-t / 0.003), lowpass(1800.0, 2)) * 0.05 * vel
    out = (out + hammer) * np.clip(t / 0.004, 0, 1)
    damper = np.where(t < length, 1.0, np.exp(-(t - length) / release))
    out *= damper
    n = len(out)
    out[n - samples(0.02):] *= attack_curve(samples(0.02))[::-1]
    return out * vel


def tine(f, length, vel, rng):
    tau = 1.1 * (440.0 / f) ** 0.35
    seconds = max(length + 0.4, 4.0 * tau)
    t = np.arange(samples(seconds)) / SR
    spec = [(1.0, 1.0, tau), (2.0, 0.05 * vel, tau * 0.3), (5.95, 0.14 * vel, tau * 0.1), (9.4, 0.035 * vel, tau * 0.05)]
    x = partials(t, f, spec, attack=0.0015, phase_rng=rng)
    return x * vel


def music_box(f, vel):
    t = np.arange(samples(2.4)) / SR
    return fm_bell(t, f, ratio=2.0, index=0.3 + 0.8 * vel, index_tau=0.15, amp_tau=0.9 * (880.0 / f) ** 0.3,
                   shimmer=0.03) * vel


def round_bass(f, length, vel):
    """Soft bass: the harmonics carry it on small speakers, no sub."""
    seconds = length + 0.6
    t = np.arange(samples(seconds)) / SR
    x = (np.sin(2 * np.pi * f * t) + 0.55 * np.sin(4 * np.pi * f * t) + 0.25 * np.sin(6 * np.pi * f * t)
         + 0.1 * np.sin(8 * np.pi * f * t))
    env = np.clip(t / 0.012, 0, 1) * np.exp(-t / 1.6) * np.where(t < length, 1.0, np.exp(-(t - length) / 0.12))
    x = x * env
    x[len(x) - samples(0.02):] *= attack_curve(samples(0.02))[::-1]
    return x * vel


_PAD_TABLES = {}


def pad_table(f, seed):
    """PADsynth wavetable (stereo): each harmonic spread into a soft band with random phases. Periodic by design."""
    key = (round(f, 3), seed)
    if key in _PAD_TABLES:
        return _PAD_TABLES[key]
    rng = np.random.default_rng(seed)
    size = 2 ** 18
    freqs = np.fft.rfftfreq(size, 1.0 / SR)
    chans = []
    for c in range(2):
        mag = np.zeros(len(freqs))
        for k in range(1, 14):
            fk = f * k
            if fk > 6000.0:
                break
            amp = k ** -1.15 / np.sqrt(1.0 + (fk / 1600.0) ** 4)
            bw = fk * (2 ** (16.0 * np.sqrt(k) / 1200.0) - 1.0)
            mag += amp * np.exp(-0.5 * ((freqs - fk) / max(bw, 1.0)) ** 2) / np.sqrt(max(bw, 1.0))
        spectrum = mag * np.exp(1j * rng.uniform(0, 2 * np.pi, len(freqs)))
        table = np.fft.irfft(spectrum, size)
        chans.append(table / (np.max(np.abs(table)) + 1e-12))
    table = np.stack(chans, axis=1)
    _PAD_TABLES[key] = table
    return table


def shaker(vel, rng):
    n = samples(0.05)
    t = np.arange(n) / SR
    x = fft_filter(rng.standard_normal(n), bandpass(4500.0, 9500.0, 2)) * (np.clip(t / 0.006, 0, 1) * np.exp(-t / 0.014))
    return x * vel


# ---------------------------------------------------------------- render

def _add(bus, signal, at_seconds, gain=1.0, pan=0.0):
    start = samples(at_seconds)
    angle = (pan + 1.0) * np.pi / 4.0
    stereo = np.stack([signal * np.cos(angle), signal * np.sin(angle)], axis=1) if signal.ndim == 1 else signal
    stereo = stereo * (gain * section_gain(at_seconds / BAR))
    start %= LOOP
    end = start + len(stereo)
    if end <= LOOP:
        bus[start:end] += stereo
    else:
        split = LOOP - start
        bus[start:] += stereo[:split]
        bus[:end - LOOP] += stereo[split:]


def refuge_theme(seed=80, parts=None):
    rng = np.random.default_rng(seed)
    jitter = lambda: rng.normal(0.0, 0.005)
    human = lambda: 1.0 + rng.uniform(-0.08, 0.08)
    lead = np.zeros((LOOP, 2))
    arps = np.zeros((LOOP, 2))
    pad = np.zeros((LOOP, 2))
    bass = np.zeros((LOOP, 2))
    bells = np.zeros((LOOP, 2))
    perc = np.zeros((LOOP, 2))

    # Lead melody.
    for instrument, first_bar, phrases in LEAD:
        for i, bar_notes in enumerate(phrases):
            bar = first_bar + i
            for note, start8, len8 in bar_notes:
                at = bar * BAR + start8 * EIGHTH + jitter()
                length = len8 * EIGHTH
                vel = 0.78 * human() * (0.75 if 32 <= bar < 48 else 1.0)
                if instrument == 'piano':
                    sig = felt_piano(hz(note), length, vel, rng, release=0.6 if 32 <= bar < 48 else 0.25)
                    _add(lead, sig, at, 0.5, 0.05)
                else:
                    sig = tine(hz(note), length, vel, rng)
                    _add(lead, sig, at, 0.48, -0.05)

    # Counter line: music box, one soft ping per bar in the second half of B.
    for i, note in enumerate(COUNTER):
        bar = 24 + i
        _add(bells, music_box(hz(note), 0.4 * human()), bar * BAR + 2 * EIGHTH + jitter(), 0.3, 0.35)
    # A few music box pings in C, high and sparse.
    for bar, note in ((41, 'A6'), (43, 'C7'), (45, 'F6')):
        _add(bells, music_box(hz(note), 0.3), bar * BAR + jitter(), 0.22, -0.3)

    # Tine arpeggios over the chord tones, below the melody.
    for bar, name in enumerate(CHORDS):
        voicing = [hz(n) for n in CH[name][0]]
        tones = sorted(set([v * 2 for v in voicing if v * 2 < hz('F5')] + [v for v in voicing if v >= hz('G3')]))
        tones = [f for f in tones if hz('G3') <= f <= hz('F5')]
        cycle = tones + tones[-2:0:-1]
        for j, step in enumerate(arp_steps(bar)):
            f = cycle[(j + bar) % len(cycle)]
            vel = (0.42 if step % 2 == 0 else 0.34) * human()
            _add(arps, tine(f, EIGHTH, vel, rng), bar * BAR + step * EIGHTH + jitter(), 0.22, rng.uniform(-0.35, 0.35))

    # Pad: one PADsynth chord per bar, long attack and release (bars overlap smoothly).
    for bar, name in enumerate(CHORDS):
        level = 0.75 if 32 <= bar < 40 else 1.0
        length = samples(BAR + 1.8)
        env = np.ones(length)
        env[:samples(0.7)] = attack_curve(samples(0.7))
        release = samples(1.8)
        env[length - release:] *= np.exp(-np.linspace(0, 4.0, release))
        env[length - samples(0.05):] *= attack_curve(samples(0.05))[::-1]
        for k, note in enumerate(CH[name][0]):
            table = pad_table(hz(note), 1000 + k)
            offset = int(rng.integers(0, len(table)))
            idx = (offset + np.arange(length)) % len(table)
            seg = table[idx] * env[:, None] * 0.25 * level
            _add(pad, seg, bar * BAR - 0.35, 0.22)

    # Bass.
    for bar, name in enumerate(CHORDS):
        root = hz(CH[name][1])
        if 32 <= bar < 36:
            continue
        if 16 <= bar < 32 or 36 <= bar < 48:
            hits = ((0, 4, root), (4, 4, root * (1.5 if name not in ('C/E', 'F/A') else 1.0)))
        else:
            hits = ((0, 7, root),) if bar % 2 == 0 else ((0, 4, root), (4, 3, root))
        for start8, len8, f in hits:
            f = f / 2 if f > hz('E3') else f
            _add(bass, round_bass(f, len8 * EIGHTH, 0.8 * human()), bar * BAR + start8 * EIGHTH + jitter(), 0.15, 0.0)

    # Shaker: off-beat whispers in B and the end of A'.
    for bar in range(BARS):
        if 16 <= bar < 32:
            steps = (1, 3, 5, 7)
        elif 56 <= bar < 63:
            steps = (3, 7)
        else:
            continue
        for step in steps:
            vel = (0.9 if step in (3, 7) else 0.6) * human()
            _add(perc, shaker(vel, rng), bar * BAR + step * EIGHTH + rng.normal(0.0, 0.006), 0.08,
                 rng.uniform(-0.5, 0.5))

    if parts is not None:
        for name, bus in (('lead', lead), ('arps', arps), ('pad', pad), ('bass', bass), ('bells', bells), ('perc', perc)):
            parts[name] = bus
    dry = lead + arps + pad + bass + bells + perc
    sends = lead * 0.28 + arps * 0.25 + pad * 0.3 + bells * 0.4 + perc * 0.15 + bass * 0.04
    ir = reverb_ir(3.0, 2.2, damping_hz=3500.0, predelay=0.025, seed=81, early=0.25)
    wet = convolve_circular(sends, ir)
    mix = dry + wet * 0.9
    mix = fft_filter(mix, chain(highpass(45.0, 2), peak(3500.0, -1.5, 1.2), lowpass(12000.0, 2)), pad=False)
    return mix / (np.max(np.abs(mix)) + 1e-12) * db(-3.0)


# ---------------------------------------------------------------- T37: activity musics (doc 01 Contenu, section 12.0)
#
# J-MUS-ACT-01 « Petite partie » (Little Game, calm, 84 BPM), J-MUS-ACT-02 « Pattes pressées » (Hurried Paws, lively,
# 112 BPM) and J-MUS-ACT-03 « Câlins en série » (Hug Streak, puzzle, 96 BPM, soft D minor moving to F major). The
# theme's instruments (kalimba tines, felt piano, glass of the music box, PADsynth pad, round bass, the soft shaker)
# and a marimba, in the theme's key family. Under an activity they play with the theme biome's ambience at -6 dB (the
# presenter mixes; doc 04 Biomes tardifs). Games last 30 to 90 s, are replayed a lot, and the music keeps running from
# one game to the next: loops of 80 to 120 s written in sections with a gentle arc (the lead passes between kalimba,
# marimba and felt piano, the texture thins and fills), no hook repeated every few bars, no drums. Each loop is a whole
# number of samples at 48 and 44.1 kHz (a multiple of 160 samples at 48 kHz), so its loop point stays exact after
# Unity's resampling; every voice is written into circular buses, reverb included, so the last bar flows into the first.

ACT_CH = {
    'F': (['F3', 'C4', 'F4', 'A4'], 'F2'),
    'Fmaj7': (['F3', 'A3', 'C4', 'E4'], 'F2'),
    'Bb/F': (['F3', 'Bb3', 'D4', 'F4'], 'F2'),
    'F/A': (['A3', 'C4', 'F4', 'A4'], 'A2'),
    'C': (['G3', 'C4', 'E4', 'G4'], 'C3'),
    'C/E': (['E3', 'C4', 'E4', 'G4'], 'E2'),
    'Csus4': (['G3', 'C4', 'F4', 'G4'], 'C3'),
    'Dm': (['D3', 'A3', 'D4', 'F4'], 'D2'),
    'Dm7': (['D3', 'A3', 'C4', 'F4'], 'D2'),
    'Dmadd9': (['D3', 'A3', 'E4', 'F4'], 'D2'),
    'Am7': (['A3', 'C4', 'E4', 'G4'], 'A2'),
    'Bb': (['F3', 'Bb3', 'D4', 'F4'], 'Bb2'),
    'Bbmaj7': (['F3', 'A3', 'Bb3', 'D4'], 'Bb2'),
    'Gm7': (['G3', 'Bb3', 'D4', 'F4'], 'G2'),
    'Asus4': (['A3', 'D4', 'E4', 'A4'], 'A2'),
}


def marimba(f, vel, rng, mallet=1.0):
    """Marimba bar (T37): the tuned 1:4:10 partials of a wooden bar under a yarn mallet, the tube under the bar holding
    the fundamental (low bars ring longer), the soft knock of the mallet. mallet < 1: a lighter stroke (rolls)."""
    tau = 0.8 * (262.0 / f) ** 0.7
    seconds = min(4.0 * tau + 0.05, 3.0)
    t = np.arange(samples(seconds)) / SR
    bright = (0.08 + 0.1 * vel) * mallet
    x = partials(t, f, [(1.0, 1.0, tau), (3.98, bright, tau * 0.16), (9.85, bright * 0.22, tau * 0.06)], attack=0.003,
                 phase_rng=rng)
    nk = samples(0.03)
    tk = np.arange(nk) / SR
    knock = fft_filter(rng.standard_normal(nk) * np.exp(-tk / 0.0012), bandpass(250.0, 2000.0, 2)) * 0.02 * mallet
    x[:nk] += knock * np.clip(tk / 0.0008, 0.0, 1.0) * attack_curve(nk)[::-1]
    return x * vel


def _held(x, hold, release):
    """The note is let go after hold seconds (a fingertip resting on the tine), fading over release, then ends."""
    end = min(len(x), samples(hold + 5.0 * release))
    y = x[:end].copy()
    t = np.arange(end) / SR
    y *= np.where(t < hold, 1.0, np.exp(-(t - hold) / release))
    n = min(samples(0.02), end // 4)
    y[end - n:] *= attack_curve(n)[::-1]
    return y


def _kalimba(rng, release):
    """A kalimba voice for the lines: the theme's tine, let go release seconds after its written length."""
    return lambda f, length, vel: _held(tine(f, length, vel, rng), length + 0.08, release)


def _marimba_voice(rng, roll_from):
    """A marimba voice for the lines: a long note (roll_from seconds or more) is held by a soft roll, light strokes
    about every 85 ms from alternating hands that fade a little toward the end, as a player holds a note on wood."""
    def play(f, length, vel):
        x = marimba(f, vel, rng)
        if length < roll_from:
            return x
        out = np.zeros(samples(length) + len(x))
        out[:len(x)] += x
        t, k = 0.085, 1
        while t < length - 0.05:
            stroke = marimba(f, vel * (0.42 if k % 2 else 0.36) * (1.0 - 0.3 * t / length), rng, mallet=0.6)
            start = samples(t + rng.normal(0.0, 0.003))
            out[start:start + len(stroke)] += stroke
            t += 0.085 + rng.uniform(-0.005, 0.005)
            k += 1
        return out
    return play


def _tones(name, lo, hi):
    """Chord tones of a voicing folded by octaves into [lo, hi] Hz, low to high."""
    out = set()
    for note in ACT_CH[name][0]:
        for k in (-2, -1, 0, 1, 2):
            f = hz(note) * 2.0 ** k
            if lo * 0.999 <= f <= hi * 1.001:
                out.add(round(f, 4))
    return sorted(out)


class _Score:
    """T37: a circular 4/4 score. Every voice is mixed into stereo buses that wrap at the loop point."""

    BUSES = ('lead', 'keys', 'pad', 'bass', 'bells', 'perc')

    def __init__(self, bpm, bars, levels, seed):
        self.beat = 60.0 / bpm
        self.bar = 4.0 * self.beat
        self.eighth = self.beat / 2.0
        self.bars = bars
        self.length = samples(bars * self.bar)
        assert self.length % 160 == 0, 'the loop must be a whole number of samples at 48 and 44.1 kHz'
        self.levels = np.asarray(levels, dtype=np.float64)
        assert len(self.levels) == bars
        self.rng = np.random.default_rng(seed)
        self.bus = {name: np.zeros((self.length, 2)) for name in self.BUSES}

    def at(self, bar, eighth=0.0):
        """Seconds of a position, played by a hand (a few ms early or late)."""
        return bar * self.bar + eighth * self.eighth + self.rng.normal(0.0, 0.005)

    def human(self, spread=0.08):
        return 1.0 + self.rng.uniform(-spread, spread)

    def level(self, bar_position):
        """Section dynamics, smoothed across the bar before each change, circular."""
        x = bar_position % self.bars
        i = int(np.floor(x))
        a, b = self.levels[i], self.levels[(i + 1) % self.bars]
        return a + (b - a) * (x - i)

    def add(self, bus, signal, at_seconds, gain=1.0, pan=0.0):
        angle = (pan + 1.0) * np.pi / 4.0
        stereo = np.stack([signal * np.cos(angle), signal * np.sin(angle)], axis=1) if signal.ndim == 1 else signal
        stereo = stereo * (gain * self.level(at_seconds / self.bar))
        buffer = self.bus[bus]
        start = samples(at_seconds) % self.length
        end = start + len(stereo)
        if end <= self.length:
            buffer[start:end] += stereo
        else:
            split = self.length - start
            buffer[start:] += stereo[:split]
            buffer[:end - self.length] += stereo[split:]

    def line(self, bus, first_bar, phrases, play, gain, pan=0.0, vel=0.78):
        """A melody: per bar, (note, start in eighths, length in eighths), played by play(f, seconds, velocity)."""
        for i, bar_notes in enumerate(phrases):
            for note, start8, len8 in bar_notes:
                signal = play(hz(note), len8 * self.eighth, vel * self.human())
                self.add(bus, signal, self.at(first_bar + i, start8), gain, pan)

    def broken(self, bar, name, steps, low, high, play, bus, gain, vels, spread=0.3):
        """A broken chord over the bar's chord tones in [low, high] (up then down, starting one tone later each bar)."""
        tones = _tones(name, hz(low), hz(high))
        cycle = tones + tones[-2:0:-1]
        for j, step in enumerate(steps):
            f = cycle[(j + bar) % len(cycle)]
            signal = play(f, self.eighth, vels[j % len(vels)] * self.human())
            self.add(bus, signal, self.at(bar, step), gain, self.rng.uniform(-spread, spread))

    def bass(self, bar, name, hits, gain=0.15):
        """hits: (start in eighths, length in eighths, semitones above the bass note), folded under G3."""
        root = hz(ACT_CH[name][1])
        for start8, len8, semis in hits:
            f = root * 2.0 ** (semis / 12.0)
            f = f / 2.0 if f > hz('G3') * 1.001 else f
            self.add('bass', round_bass(f, len8 * self.eighth, 0.8 * self.human()), self.at(bar, start8), gain)

    def pad(self, chords, levels, seed, gain=0.22):
        """One PADsynth chord per bar, long attack and release (bars overlap smoothly), as in the theme."""
        length = samples(self.bar + 1.8)
        env = np.ones(length)
        env[:samples(0.7)] = attack_curve(samples(0.7))
        release = samples(1.8)
        env[length - release:] *= np.exp(-np.linspace(0.0, 4.0, release))
        env[length - samples(0.05):] *= attack_curve(samples(0.05))[::-1]
        for bar, name in enumerate(chords):
            if levels[bar] <= 0.0:
                continue
            for k, note in enumerate(ACT_CH[name][0]):
                table = pad_table(hz(note), seed + k)
                offset = int(self.rng.integers(0, len(table)))
                idx = (offset + np.arange(length)) % len(table)
                self.add('pad', table[idx] * env[:, None] * 0.25 * levels[bar], bar * self.bar - 0.35, gain)

    def chime(self, bar, eighth, note, vel, gain=0.26, pan=0.3):
        """A glass note of the music box."""
        self.add('bells', music_box(hz(note), vel * self.human(0.05)), self.at(bar, eighth), gain, pan)

    def shake(self, bar, steps, accents, gain=0.06):
        for step in steps:
            vel = (0.85 if step in accents else 0.45) * self.human(0.12)
            self.add('perc', shaker(vel, self.rng), bar * self.bar + step * self.eighth + self.rng.normal(0.0, 0.004), gain,
                     self.rng.uniform(-0.45, 0.45))

    def master(self, sends, rt60, seed, wet=0.9):
        """
        Dry buses, a circular reverb send, then the tone curve: a little presence near 2.2 kHz (the kalimba and the
        marimba are rounder than the theme's piano and would sound muffled on a phone), a softer 4 kHz. Peak at -3 dBFS;
        the build sets the level.
        """
        dry = sum(self.bus[k] for k in self.BUSES)
        send = sum(self.bus[k] * sends.get(k, 0.0) for k in self.BUSES)
        ir = reverb_ir(3.0, rt60, damping_hz=3500.0, predelay=0.025, seed=seed, early=0.25)
        mix = dry + convolve_circular(send, ir) * wet
        mix = fft_filter(mix, chain(highpass(45.0, 2), peak(2200.0, 2.0, 1.5), peak(4200.0, -1.0, 1.0), lowpass(12000.0, 2)),
                         pad=False)
        return mix / (np.max(np.abs(mix)) + 1e-12) * db(-3.0)


# ---------------------------------------------------------------- J-MUS-ACT-01 « Petite partie »

LITTLE_CHORDS = (
    'Fmaj7', 'Bb/F', 'Fmaj7', 'Bb/F',                                       # intro
    'F', 'Am7', 'Bbmaj7', 'C', 'F', 'Am7', 'Bbmaj7', 'Csus4',               # A
    'Dm7', 'Am7', 'Bbmaj7', 'F/A', 'Gm7', 'F/A', 'Bbmaj7', 'Csus4',         # B
    'F', 'Am7', 'Bbmaj7', 'C', 'Dm7', 'Gm7', 'Bbmaj7', 'C',                 # A'
)
LITTLE_INTRO = [[('C6', 4, 2), ('A5', 6, 2)],
                [('Bb5', 0, 3), ('F5', 3, 1), ('D5', 4, 4)],
                [('C6', 4, 2), ('E6', 6, 2)],
                [('D6', 0, 3), ('C6', 3, 1), ('Bb5', 4, 4)]]
LITTLE_A = [[('C5', 0, 2), ('F5', 2, 2), ('A5', 4, 3), ('G5', 7, 1)],
            [('A5', 0, 2), ('G5', 2, 2), ('E5', 4, 4)],
            [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('C6', 6, 2)],
            [('Bb5', 0, 3), ('A5', 3, 1), ('G5', 4, 4)],
            [('C5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('C6', 6, 2)],
            [('C6', 0, 3), ('A5', 3, 1), ('G5', 4, 2), ('E5', 6, 2)],
            [('F5', 0, 2), ('D5', 2, 2), ('F5', 4, 2), ('A5', 6, 2)],
            [('G5', 0, 6)]]
LITTLE_B = [[('A4', 0, 2), ('C5', 2, 2), ('D5', 4, 4)],
            [('E5', 0, 2), ('C5', 2, 2), ('A4', 4, 4)],
            [('Bb4', 0, 2), ('D5', 2, 2), ('F5', 4, 3), ('E5', 7, 1)],
            [('F5', 0, 2), ('C5', 2, 2), ('A4', 4, 4)],
            [('Bb4', 0, 2), ('D5', 2, 2), ('G5', 4, 2), ('F5', 6, 2)],
            [('F5', 0, 3), ('E5', 3, 1), ('C5', 4, 4)],
            [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('G5', 6, 2)],
            [('F5', 0, 4), ('G5', 4, 4)]]
LITTLE_A2 = [[('A4', 0, 1), ('C5', 1, 1), ('F5', 2, 2), ('A5', 4, 4)],
             [('G5', 0, 3), ('E5', 3, 1), ('C5', 4, 4)],
             [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('Bb5', 6, 2)],
             [('C6', 0, 4), ('G5', 4, 4)],
             [('F5', 0, 2), ('A5', 2, 2), ('C6', 4, 2), ('A5', 6, 2)],
             [('Bb5', 0, 3), ('A5', 3, 1), ('G5', 4, 2), ('F5', 6, 2)],
             [('D5', 0, 4), ('F5', 4, 2), ('E5', 6, 2)],
             [('G5', 0, 6)]]


def little_game(seed=84):
    """
    J-MUS-ACT-01 « Petite partie » (Little Game): calm, 84 BPM, F major, 28 bars = 80 s exactly. Intro (the marimba
    rocks on F under the pad), A (kalimba melody over the marimba), B (the marimba sings, rolled on its long notes, light
    kalimba and glass answer above it), A' (the kalimba again, a new ending that leans back into the intro). Grooming,
    Pawprint Memory, Baking, Constellations, Tile Puzzle, Photo Safari.
    """
    s = _Score(84, 28, [0.88] * 4 + [0.92] * 8 + [1.0] * 8 + [0.95] * 8, seed)
    rng = s.rng
    kalimba = _kalimba(rng, 0.6)
    wood = lambda f, length, vel: marimba(f, vel, rng)
    s.line('lead', 0, LITTLE_INTRO, kalimba, 0.42, -0.12, vel=0.62)
    s.line('lead', 4, LITTLE_A, kalimba, 0.5, -0.12)
    s.line('lead', 12, LITTLE_B, _marimba_voice(rng, 0.95), 0.46, 0.15, vel=0.8)
    s.line('lead', 20, LITTLE_A2, kalimba, 0.5, -0.12)
    for bar, name in enumerate(LITTLE_CHORDS):
        if bar < 4:
            s.broken(bar, name, (0, 2, 3, 4, 6), 'F3', 'F4', wood, 'keys', 0.3, (0.55, 0.36, 0.3, 0.46, 0.36), spread=0.4)
            s.bass(bar, name, ((0, 8, 0),))
        elif 12 <= bar < 20:
            s.broken(bar, name, (2, 3, 6), 'A5', 'E6', kalimba, 'keys', 0.15, (0.34, 0.28, 0.32))
            s.bass(bar, name, ((0, 8, 0),))
        else:
            s.broken(bar, name, (0, 2, 3, 4, 6), 'A3', 'C5', wood, 'keys', 0.28, (0.5, 0.36, 0.3, 0.44, 0.34), spread=0.4)
            fifth = 7 if '/' not in name and bar % 2 == 1 else 0
            s.bass(bar, name, ((0, 4, 0), (4, 4, fifth)))
    s.pad(LITTLE_CHORDS, [0.9] * 4 + [0.75] * 8 + [1.0] * 8 + [0.8] * 8, 2100)
    for bar, eighth, note, vel in ((0, 0, 'A5', 0.4), (11, 6, 'C6', 0.35), (13, 4, 'E6', 0.35),
                                   (15, 4, 'C6', 0.35), (17, 4, 'A5', 0.35), (19, 4, 'G5', 0.35), (27, 6, 'E6', 0.3)):
        s.chime(bar, eighth, note, vel)
    return s.master({'lead': 0.3, 'keys': 0.25, 'pad': 0.3, 'bells': 0.4, 'bass': 0.04}, 2.2, 2101)


# ---------------------------------------------------------------- J-MUS-ACT-02 « Pattes pressées »

HURRIED_CHORDS = (
    'F', 'F', 'Bb', 'C',                                                    # intro
    'F', 'C/E', 'Dm7', 'Bb', 'F', 'C/E', 'Gm7', 'C',                        # A
    'F', 'C/E', 'Dm7', 'Bb', 'F/A', 'Bb', 'Gm7', 'C',                       # A2
    'Bb', 'C', 'Am7', 'Dm7', 'Bb', 'C', 'Dm7', 'C',                         # B
    'Dm7', 'Bbmaj7', 'F', 'C', 'Dm7', 'Bbmaj7', 'Gm7', 'C',                 # C (breathing room)
    'Bb', 'C', 'Am7', 'Dm7', 'Bb', 'C', 'F/A', 'Bb',                        # B2
    'F', 'C/E', 'Dm7', 'Bb', 'F', 'C/E', 'Gm7', 'C',                        # A3
    'Bb', 'C', 'Gm7', 'C',                                                  # turnaround
)
HURRIED_INTRO = [[('F5', 4, 1), ('A5', 5, 1), ('C6', 6, 2)],
                 [('A5', 4, 1), ('C6', 5, 1), ('D6', 6, 1), ('C6', 7, 1)],
                 [('F5', 4, 1), ('Bb5', 5, 1), ('D6', 6, 2)],
                 [('C6', 4, 1), ('Bb5', 5, 1), ('G5', 6, 1), ('E5', 7, 1)]]
HURRIED_A = [[('A5', 0, 1), ('C6', 1, 1), ('A5', 2, 1), ('F5', 3, 1), ('G5', 4, 2), ('A5', 6, 2)],
             [('G5', 0, 1), ('E5', 1, 1), ('C5', 2, 2), ('E5', 4, 1), ('G5', 5, 1), ('C6', 6, 2)],
             [('A5', 0, 1), ('F5', 1, 1), ('D5', 2, 2), ('F5', 5, 1), ('A5', 6, 2)],
             [('C6', 0, 2), ('Bb5', 2, 1), ('A5', 3, 1), ('F5', 4, 4)],
             [('A5', 0, 1), ('C6', 1, 1), ('A5', 2, 1), ('F5', 3, 1), ('G5', 4, 2), ('A5', 6, 2)],
             [('G5', 0, 1), ('E5', 1, 1), ('C5', 2, 2), ('D5', 4, 1), ('E5', 5, 1), ('G5', 6, 2)],
             [('F5', 0, 1), ('G5', 1, 1), ('Bb5', 2, 2), ('A5', 5, 1), ('G5', 6, 2)],
             [('E5', 0, 2), ('G5', 2, 2), ('C6', 4, 2)]]
HURRIED_A_END = [[('C6', 0, 1), ('A5', 1, 1), ('F5', 2, 1), ('A5', 3, 1), ('C6', 4, 2), ('D6', 6, 2)],
                 [('D6', 0, 2), ('C6', 2, 1), ('Bb5', 3, 1), ('A5', 4, 2), ('F5', 6, 2)],
                 [('G5', 0, 1), ('A5', 1, 1), ('Bb5', 2, 1), ('D6', 3, 1), ('C6', 4, 2), ('Bb5', 6, 2)],
                 [('A5', 0, 2), ('G5', 2, 2), ('E5', 4, 2)]]
HURRIED_B = [[('D5', 0, 2), ('F5', 2, 1), ('Bb5', 3, 3), ('A5', 6, 2)],
             [('G5', 0, 2), ('E5', 2, 1), ('C5', 3, 3), ('E5', 6, 2)],
             [('A5', 0, 2), ('G5', 2, 1), ('E5', 3, 3), ('C5', 6, 2)],
             [('D5', 0, 2), ('F5', 2, 1), ('A5', 3, 3), ('C6', 6, 2)],
             [('D6', 0, 2), ('C6', 2, 1), ('Bb5', 3, 3), ('F5', 6, 2)],
             [('E5', 0, 2), ('G5', 2, 1), ('C6', 3, 3), ('Bb5', 6, 2)],
             [('A5', 0, 2), ('F5', 2, 1), ('D5', 3, 3), ('E5', 6, 2)],
             [('G5', 0, 4), ('E5', 4, 2), ('C5', 6, 2)]]
HURRIED_B2_END = [('F5', 0, 2), ('G5', 2, 2), ('A5', 4, 2), ('C6', 6, 2)]
HURRIED_C_GLASS = [[('A5', 0, 4), ('D6', 4, 4)],
                   [('F6', 0, 4), ('D6', 4, 4)],
                   [('C6', 0, 4), ('A5', 4, 4)],
                   [('G5', 0, 4), ('C6', 4, 4)]]
HURRIED_C_KALIMBA = [[('A5', 0, 2), ('F5', 2, 2), ('D5', 4, 2), ('F5', 6, 2)],
                     [('A5', 0, 2), ('C6', 2, 2), ('D6', 4, 4)],
                     [('Bb5', 0, 2), ('A5', 2, 2), ('G5', 4, 2), ('F5', 6, 2)],
                     [('G5', 0, 4), ('C6', 4, 4)]]
HURRIED_TURN = [[('F5', 0, 2), ('D5', 2, 2), ('Bb4', 4, 2)],
                [('E5', 0, 2), ('G5', 2, 2), ('C6', 4, 2)],
                [('Bb5', 0, 2), ('A5', 2, 2), ('G5', 4, 2), ('F5', 6, 2)],
                [('E5', 0, 3), ('G5', 3, 1), ('C6', 4, 2)]]


def hurried_paws(seed=112):
    """
    J-MUS-ACT-02 « Pattes pressées » (Hurried Paws): lively, 112 BPM, F major, 56 bars = 120 s exactly. Bouncy marimba,
    short kalimba lines, a round bass that skips, a soft shaker (never drums). Intro, A, A2 (a new answer), B (the
    marimba leads, kalimba plucks on the off-beats), C (breathing room: glass and pad, the shaker steps out), B2 (the
    kalimba takes B), A3 (A with its second answer, glass doubling), a four-bar turnaround into the intro. Hide and
    Seek, Nut Drop, Treasure Fishing, Lily Pad Hop, Glide, Bubbles, Burrow Maze.
    """
    levels = [0.9] * 4 + [0.92] * 8 + [0.95] * 8 + [1.0] * 8 + [0.86] * 8 + [1.0] * 8 + [0.97] * 8 + [0.92] * 4
    s = _Score(112, 56, levels, seed)
    rng = s.rng
    kalimba = _kalimba(rng, 0.28)
    wood = lambda f, length, vel: marimba(f, vel, rng)
    glass = lambda f, length, vel: music_box(f, vel)
    s.line('lead', 0, HURRIED_INTRO, kalimba, 0.42, -0.12, vel=0.62)
    s.line('lead', 4, HURRIED_A, kalimba, 0.48, -0.12)
    s.line('lead', 12, HURRIED_A[:4] + HURRIED_A_END, kalimba, 0.48, -0.12)
    s.line('lead', 20, HURRIED_B, _marimba_voice(rng, 1.0), 0.46, 0.15, vel=0.82)
    s.line('bells', 28, HURRIED_C_GLASS, glass, 0.38, 0.25, vel=0.48)
    s.line('lead', 32, HURRIED_C_KALIMBA, kalimba, 0.44, -0.12, vel=0.72)
    s.line('lead', 36, HURRIED_B[:7] + [HURRIED_B2_END], kalimba, 0.48, -0.12)
    a3 = HURRIED_A[:4] + HURRIED_A_END
    s.line('lead', 44, a3, kalimba, 0.48, -0.12)
    for i, bar_notes in enumerate(a3):
        note, start8, _ = bar_notes[0]
        s.add('bells', music_box(hz(note) * 2.0, 0.3 * s.human(0.05)), s.at(44 + i, start8), 0.14, 0.3)
    s.line('lead', 52, HURRIED_TURN, kalimba, 0.44, -0.12, vel=0.72)

    bounce = ((0, 1.5, 0), (3, 1, 0), (4, 1.5, 7), (7, 1, 12))
    for bar, name in enumerate(HURRIED_CHORDS):
        if 20 <= bar < 28:
            s.broken(bar, name, (1, 3, 5, 7), 'F4', 'C5', kalimba, 'keys', 0.17, (0.32, 0.28))
        elif 28 <= bar < 36:
            s.broken(bar, name, (0, 3, 6), 'D3', 'A4', wood, 'keys', 0.28, (0.48, 0.36, 0.42), spread=0.4)
        else:
            s.broken(bar, name, (0, 2, 3, 5, 6, 7), 'F3', 'C5', wood, 'keys', 0.26, (0.55, 0.35, 0.5, 0.35, 0.5, 0.3),
                     spread=0.4)
        if 28 <= bar < 36:
            s.bass(bar, name, ((0, 6, 0),))
        else:
            hits = bounce if '/' not in name else ((0, 1.5, 0), (3, 1, 0), (4, 1.5, 0), (7, 1, 12))
            s.bass(bar, name, hits)
        if bar < 2 or 28 <= bar < 30:
            continue
        if bar < 4 or 30 <= bar < 36 or bar >= 52:
            s.shake(bar, (1, 3, 5, 7), (3, 7))
        else:
            s.shake(bar, (0, 1, 2, 3, 4, 5, 6, 7) + ((6.5,) if bar % 2 else ()), (1, 3, 5, 7))
    s.pad(HURRIED_CHORDS, [0.7] * 4 + [0.6] * 16 + [0.7] * 8 + [1.0] * 8 + [0.7] * 8 + [0.6] * 8 + [0.7] * 4, 2200)
    for bar, eighth, note, vel in ((2, 4, 'F6', 0.35), (11, 7, 'C6', 0.35), (19, 6, 'G6', 0.32), (27, 6, 'E6', 0.35),
                                   (35, 6, 'G6', 0.32), (43, 6, 'D6', 0.35), (55, 6, 'E6', 0.32)):
        s.chime(bar, eighth, note, vel)
    return s.master({'lead': 0.24, 'keys': 0.2, 'pad': 0.28, 'bells': 0.38, 'perc': 0.1, 'bass': 0.03}, 1.8, 2201)


# ---------------------------------------------------------------- J-MUS-ACT-03 « Câlins en série »

HUG_CHORDS = (
    'Dmadd9', 'Bbmaj7', 'Gm7', 'Asus4',                                     # intro (D minor)
    'Dm', 'Bbmaj7', 'F', 'C', 'Dm', 'Bbmaj7', 'Gm7', 'Asus4',               # A (D minor)
    'Dm', 'Bbmaj7', 'F', 'C', 'Gm7', 'Am7', 'Bbmaj7', 'Csus4',              # A2 (turning toward F)
    'F', 'C/E', 'Dm7', 'Bbmaj7', 'F/A', 'Gm7', 'Bbmaj7', 'C',               # B (F major)
    'F', 'Am7', 'Bbmaj7', 'C', 'F', 'C/E', 'Dm7', 'Bbmaj7',                 # B2 (F major, the brightest)
    'Gm7', 'C', 'Bbmaj7', 'Asus4',                                          # back toward D minor
)
HUG_INTRO_GLASS = [[('A5', 0, 4), ('D6', 4, 4)],
                   [('F6', 0, 4), ('D6', 4, 4)],
                   [('Bb5', 0, 4), ('D6', 4, 4)],
                   [('E6', 0, 4), ('D6', 4, 4)]]
HUG_A = [[('A4', 0, 2), ('D5', 2, 2), ('F5', 4, 3), ('E5', 7, 1)],
         [('D5', 0, 4), ('A4', 4, 4)],
         [('C5', 0, 2), ('F5', 2, 2), ('A5', 4, 3), ('G5', 7, 1)],
         [('E5', 0, 6)],
         [('A4', 0, 2), ('D5', 2, 2), ('F5', 4, 2), ('A5', 6, 2)],
         [('G5', 0, 3), ('F5', 3, 1), ('D5', 4, 4)],
         [('Bb4', 0, 2), ('D5', 2, 2), ('F5', 4, 2), ('E5', 6, 2)],
         [('D5', 0, 6)]]
HUG_A2 = [[('F5', 0, 2), ('E5', 2, 2), ('D5', 4, 2), ('A4', 6, 2)],
          [('Bb4', 0, 2), ('D5', 2, 2), ('F5', 4, 4)],
          [('A5', 0, 2), ('G5', 2, 2), ('F5', 4, 2), ('C5', 6, 2)],
          [('E5', 0, 2), ('G5', 2, 2), ('C6', 4, 4)],
          [('Bb5', 0, 3), ('A5', 3, 1), ('G5', 4, 2), ('D5', 6, 2)],
          [('C5', 0, 2), ('E5', 2, 2), ('A5', 4, 4)],
          [('A5', 0, 2), ('F5', 2, 2), ('D5', 4, 2), ('F5', 6, 2)],
          [('G5', 0, 6)]]
HUG_B = [[('C5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('C6', 6, 2)],
         [('G5', 0, 3), ('E5', 3, 1), ('C5', 4, 4)],
         [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('C6', 6, 2)],
         [('D6', 0, 4), ('A5', 4, 4)],
         [('C6', 0, 2), ('A5', 2, 2), ('F5', 4, 2), ('A5', 6, 2)],
         [('Bb5', 0, 2), ('A5', 2, 2), ('G5', 4, 2), ('F5', 6, 2)],
         [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('Bb5', 6, 2)],
         [('G5', 0, 6)]]
HUG_B2 = [[('A4', 0, 2), ('C5', 2, 2), ('F5', 4, 4)],
          [('E5', 0, 2), ('G5', 2, 2), ('C6', 4, 4)],
          [('D5', 0, 2), ('F5', 2, 2), ('A5', 4, 2), ('G5', 6, 2)],
          [('E5', 0, 4), ('G5', 4, 4)],
          [('A5', 0, 2), ('G5', 2, 2), ('F5', 4, 2), ('C5', 6, 2)],
          [('E5', 0, 2), ('G5', 2, 2), ('C6', 4, 4)],
          [('A5', 0, 2), ('F5', 2, 2), ('D5', 4, 2), ('F5', 6, 2)],
          [('F5', 0, 4), ('D5', 4, 4)]]
HUG_TURN = [[('D5', 0, 2), ('F5', 2, 2), ('Bb5', 4, 4)],
            [('G5', 0, 2), ('E5', 2, 2), ('C5', 4, 4)],
            [('D5', 0, 4), ('F5', 4, 4)],
            [('E5', 0, 4), ('D5', 4, 4)]]


def hug_streak(seed=96):
    """
    J-MUS-ACT-03 « Câlins en série » (Hug Streak): the puzzle music, 96 BPM, 40 bars = 100 s exactly. A soft D minor
    (felt piano over a low marimba, a kalimba pulse) moving to F major (the kalimba, then the marimba, sing in the
    light), and back through Gm7, C and Bbmaj7 to an A sus4 that opens onto D minor again (no C sharp: the minor stays
    soft). Thoughtful, never sad or tense. Cuddle Match, Grand Contest.
    """
    s = _Score(96, 40, [0.88] * 4 + [0.88] * 8 + [0.93] * 8 + [1.0] * 16 + [0.88] * 4, seed)
    rng = s.rng
    kalimba = _kalimba(rng, 0.45)
    wood = lambda f, length, vel: marimba(f, vel, rng)
    piano = lambda f, length, vel: felt_piano(f, length, vel, rng, release=0.45)
    s.line('bells', 0, HUG_INTRO_GLASS, lambda f, length, vel: music_box(f, vel), 0.34, 0.25, vel=0.42)
    s.line('lead', 4, HUG_A, piano, 0.5, 0.08)
    s.line('lead', 12, HUG_A2, kalimba, 0.48, -0.12)
    s.line('lead', 20, HUG_B, kalimba, 0.5, -0.12)
    s.line('lead', 28, HUG_B2, _marimba_voice(rng, 1.0), 0.46, 0.15, vel=0.8)
    s.line('lead', 36, HUG_TURN, piano, 0.46, 0.08, vel=0.7)
    for bar, name in enumerate(HUG_CHORDS):
        if bar < 4 or bar >= 36:
            s.broken(bar, name, (0, 3, 6), 'D5', 'A5', kalimba, 'keys', 0.22, (0.44, 0.36, 0.4), spread=0.4)
            s.bass(bar, name, ((0, 8, 0),))
            continue
        if bar < 20:
            s.broken(bar, name, (0, 2, 3, 5, 6), 'D3', 'A4', wood, 'keys', 0.27, (0.5, 0.34, 0.42, 0.34, 0.4), spread=0.4)
        elif bar < 28:
            s.broken(bar, name, (0, 2, 3, 4, 6), 'F3', 'C5', wood, 'keys', 0.26, (0.5, 0.36, 0.3, 0.44, 0.34), spread=0.4)
        else:
            s.broken(bar, name, (2, 4, 6), 'F4', 'C5', kalimba, 'keys', 0.15, (0.34, 0.3, 0.32))
        fifth = 7 if '/' not in name and 'sus' not in name and bar % 2 == 1 else 0
        s.bass(bar, name, ((0, 4, 0), (4, 4, fifth)))
    s.pad(HUG_CHORDS, [1.0] * 4 + [0.85] * 16 + [0.8] * 8 + [0.85] * 8 + [1.0] * 4, 2300)
    for bar, eighth, note, vel in ((11, 6, 'E6', 0.32), (19, 6, 'G6', 0.3), (21, 4, 'E6', 0.34),
                                   (23, 4, 'F6', 0.34), (25, 4, 'D6', 0.34), (27, 4, 'E6', 0.32), (29, 6, 'G6', 0.3),
                                   (33, 6, 'E6', 0.32), (35, 6, 'F6', 0.32), (39, 4, 'A5', 0.3)):
        s.chime(bar, eighth, note, vel)
    return s.master({'lead': 0.3, 'keys': 0.25, 'pad': 0.3, 'bells': 0.4, 'bass': 0.04}, 2.2, 2301)


# name: recipe (pw_audio_build.render_music renders them all at the theme's level)
MUSIC = {
    'MUS_RefugeTheme': refuge_theme,
    'MUS_Activity_LittleGame': little_game,
    'MUS_Activity_HurriedPaws': hurried_paws,
    'MUS_Activity_HugStreak': hug_streak,
}
