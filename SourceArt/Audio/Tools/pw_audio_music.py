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

