"""
Pocket Wilds audio DSP (T34B). Pure numpy: every sound of the game is synthesized here, no sample, no third-party
library. Masters are 48 kHz; loops are rendered into circular buffers so they join without a seam.
"""
import struct
import wave

import numpy as np

SR = 48000
A4 = 440.0
_NOTE_INDEX = {'C': -9, 'D': -7, 'E': -5, 'F': -4, 'G': -2, 'A': 0, 'B': 2}


def hz(name):
    """'F4', 'Bb5', 'C#6' to Hz (equal temperament, A4 = 440)."""
    letter = name[0].upper()
    rest = name[1:]
    shift = 0
    while rest and rest[0] in '#b':
        shift += 1 if rest[0] == '#' else -1
        rest = rest[1:]
    octave = int(rest)
    semis = _NOTE_INDEX[letter] + shift + (octave - 4) * 12
    return A4 * 2.0 ** (semis / 12.0)


def samples(seconds):
    return int(round(seconds * SR))


def t_axis(seconds):
    return np.arange(samples(seconds)) / SR


def db(gain_db):
    return 10.0 ** (gain_db / 20.0)


# ---------------------------------------------------------------- envelopes

def attack_curve(n):
    """Raised cosine from 0 to 1 over n samples (no click)."""
    if n <= 1:
        return np.ones(max(n, 0))
    return 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, n))


def env_ad(t, attack, tau, hold=0.0):
    """Soft attack, optional hold, then exponential decay with time constant tau."""
    e = np.exp(-np.maximum(t - attack - hold, 0.0) / tau)
    a = np.clip(t / max(attack, 1e-6), 0.0, 1.0)
    return e * (0.5 - 0.5 * np.cos(np.pi * a))


def env_asr(n, attack, release, total=None):
    """Attack, sustain, release (seconds) over n samples: pads and swells."""
    e = np.ones(n)
    na = min(samples(attack), n)
    nr = min(samples(release), n - na)
    if na > 0:
        e[:na] = attack_curve(na)
    if nr > 0:
        e[n - nr:] *= attack_curve(nr)[::-1]
    return e


def fade(x, fade_in=0.002, fade_out=0.01):
    x = x.copy()
    ni = min(samples(fade_in), len(x))
    no = min(samples(fade_out), len(x))
    if ni > 0:
        x[:ni] *= attack_curve(ni)[:, None] if x.ndim == 2 else attack_curve(ni)
    if no > 0:
        tail = attack_curve(no)[::-1]
        x[len(x) - no:] *= tail[:, None] if x.ndim == 2 else tail
    return x


# ---------------------------------------------------------------- oscillators

def phase_of(freq):
    """Phase (radians) of a frequency curve (Hz per sample)."""
    return 2.0 * np.pi * np.cumsum(freq) / SR


def glide(t, f_start, f_end, tau):
    """Exponential glide from f_start to f_end (time constant tau)."""
    return f_end + (f_start - f_end) * np.exp(-t / tau)


def damped_sine(t, f, tau, phase=0.0):
    return np.sin(2.0 * np.pi * f * t + phase) * np.exp(-t / tau)


def partials(t, f, spec, attack=0.002, phase_rng=None):
    """Sum of (ratio, amplitude, tau) partials, each with its own decay: mallets, tines, bars."""
    out = np.zeros_like(t)
    for i, (ratio, amp, tau) in enumerate(spec):
        fk = f * ratio
        if fk >= SR * 0.45:
            continue
        ph = phase_rng.uniform(0, 2 * np.pi) if phase_rng is not None else 0.0
        out += amp * np.sin(2.0 * np.pi * fk * t + ph) * np.exp(-t / tau)
    a = np.clip(t / max(attack, 1e-6), 0.0, 1.0)
    return tail_fade(out * (0.5 - 0.5 * np.cos(np.pi * a)))


def tail_fade(x, seconds=0.03):
    """Every rendered note ends on a short fade: a note cut while ringing clicks."""
    n = min(samples(seconds), len(x) // 4)
    if n > 0:
        x[len(x) - n:] *= attack_curve(n)[::-1]
    return x


def fm_bell(t, f, ratio=2.0, index=1.6, index_tau=0.25, amp_tau=1.4, attack=0.002, shimmer=0.0):
    """Two-operator FM. ratio 2 gives a clean celesta, 3.5 an inharmonic bell; the index decays (bright to round)."""
    mod = np.sin(2.0 * np.pi * f * ratio * t) * index * np.exp(-t / index_tau)
    out = np.sin(2.0 * np.pi * f * t + mod)
    if shimmer > 0.0:
        out += shimmer * np.sin(2.0 * np.pi * f * 4.07 * t) * np.exp(-t / (amp_tau * 0.25))
    a = np.clip(t / max(attack, 1e-6), 0.0, 1.0)
    return tail_fade(out * np.exp(-t / amp_tau) * (0.5 - 0.5 * np.cos(np.pi * a)))


def formant_voice(t, f0, formants, tilt_db=-6.0, harmonics=8, breath=0.0, rng=None):
    """
    Tiny stylized voice: harmonics of f0(t) weighted by a formant envelope (Gaussians in log frequency) evaluated
    at each harmonic's current frequency. Cute 'eep' sounds, never a real animal recording.
    f0 is an array (Hz per sample). formants: list of (center Hz, width in octaves, gain).
    """
    ph = phase_of(f0)
    out = np.zeros_like(t)
    for k in range(1, harmonics + 1):
        fk = f0 * k
        weight = np.zeros_like(t)
        for center, width, gain in formants:
            weight += gain * np.exp(-0.5 * (np.log2(np.maximum(fk, 1.0) / center) / width) ** 2)
        weight *= db(tilt_db * np.log2(k))
        weight *= fk < SR * 0.45
        out += weight * np.sin(k * ph)
    if breath > 0.0 and rng is not None:
        noise = band_noise(len(t), 1800.0, 5200.0, rng)
        out += breath * noise * np.max(np.abs(out) + 1e-9)
    return out


# ---------------------------------------------------------------- noise and filters

def _next_fast(n):
    m = 1
    while m < n:
        m *= 2
    return m


def fft_filter(x, response, pad=True):
    """Zero-phase filter by a magnitude response(freqs Hz). pad=False filters circularly (seamless loops)."""
    x = np.asarray(x, dtype=np.float64)
    if x.ndim == 2:
        return np.stack([fft_filter(x[:, c], response, pad) for c in range(x.shape[1])], axis=1)
    n = len(x)
    size = _next_fast(n + SR // 4) if pad else n
    spectrum = np.fft.rfft(x, size)
    freqs = np.fft.rfftfreq(size, 1.0 / SR)
    spectrum *= response(freqs)
    return np.fft.irfft(spectrum, size)[:n]


def lowpass(fc, order=2):
    return lambda f: 1.0 / np.sqrt(1.0 + (f / fc) ** (2 * order))


def highpass(fc, order=2):
    return lambda f: (f / fc) ** order / np.sqrt(1.0 + (f / fc) ** (2 * order))


def bandpass(lo, hi, order=2):
    return lambda f: highpass(lo, order)(f) * lowpass(hi, order)(f)


def peak(fc, gain_db, octaves=1.0):
    """Bell EQ in log frequency."""
    return lambda f: db(gain_db * np.exp(-0.5 * (np.log2(np.maximum(f, 1.0) / fc) / (octaves * 0.5)) ** 2))


def chain(*responses):
    def r(f):
        out = np.ones_like(f)
        for resp in responses:
            out = out * resp(f)
        return out
    return r


def white(n, rng):
    return rng.standard_normal(n)


def pink(n, rng, circular=False):
    """1/f noise shaped in the frequency domain (circular=True: periodic, for loops)."""
    x = rng.standard_normal(n)
    shaped = fft_filter(x, lambda f: 1.0 / np.sqrt(np.maximum(f, 20.0)), pad=not circular)
    return shaped / (np.std(shaped) + 1e-12)


def band_noise(n, lo, hi, rng, order=2, circular=False):
    y = fft_filter(rng.standard_normal(n), bandpass(lo, hi, order), pad=not circular)
    return y / (np.std(y) + 1e-12)


def biquad(x, b, a):
    """Direct form I biquad (loop): short signals and loudness weighting only."""
    b0, b1, b2 = b
    _, a1, a2 = a
    y = np.empty_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        xi = x[i]
        yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, xi
        y2, y1 = y1, yi
        y[i] = yi
    return y


# ---------------------------------------------------------------- space

def reverb_ir(seconds, rt60, damping_hz=5000.0, predelay=0.012, seed=7, stereo=True, early=0.0):
    """
    Synthetic room: decaying noise whose highs die faster (rt60 at low, shorter above damping_hz). Stereo channels are
    decorrelated. early > 0 adds a few soft early reflections.
    """
    rng = np.random.default_rng(seed)
    n = samples(seconds)
    t = np.arange(n) / SR
    chans = []
    for c in range(2 if stereo else 1):
        noise = rng.standard_normal(n)
        low = fft_filter(noise, lowpass(damping_hz, 1))
        high = noise - low
        decay_low = np.exp(-6.91 * t / rt60)
        decay_high = np.exp(-6.91 * t / (rt60 * 0.35))
        ir = low * decay_low + 0.5 * high * decay_high
        onset = attack_curve(samples(0.02))
        ir[:len(onset)] *= onset
        if early > 0.0:
            for k in range(6):
                pos = samples(rng.uniform(0.004, 0.035))
                ir[pos] += early * rng.uniform(0.4, 1.0) * (1 if rng.random() > 0.5 else -1)
        pd = samples(predelay)
        ir = np.concatenate([np.zeros(pd), ir])
        ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
        chans.append(ir)
    return np.stack(chans, axis=1) if stereo else chans[0]


def convolve(x, ir):
    """Linear convolution by FFT (mono or stereo x with mono or stereo ir). Output is len(x) + len(ir) - 1."""
    x = np.atleast_2d(x.T).T if x.ndim == 1 else x
    ir = np.atleast_2d(ir.T).T if ir.ndim == 1 else ir
    n = len(x) + len(ir) - 1
    size = _next_fast(n)
    chans = max(x.shape[1], ir.shape[1])
    out = np.zeros((n, chans))
    for c in range(chans):
        xs = x[:, min(c, x.shape[1] - 1)]
        hs = ir[:, min(c, ir.shape[1] - 1)]
        out[:, c] = np.fft.irfft(np.fft.rfft(xs, size) * np.fft.rfft(hs, size), size)[:n]
    return out


def convolve_circular(x, ir):
    """Circular convolution over len(x): the tail wraps to the start, so a loop stays seamless."""
    x = x[:, None] if x.ndim == 1 else x
    ir = ir[:, None] if ir.ndim == 1 else ir
    n = len(x)
    chans = max(x.shape[1], ir.shape[1])
    out = np.zeros((n, chans))
    for c in range(chans):
        xs = x[:, min(c, x.shape[1] - 1)]
        hs = np.zeros(n)
        h = ir[:, min(c, ir.shape[1] - 1)]
        if len(h) > n:
            for start in range(0, len(h), n):
                seg = h[start:start + n]
                hs[:len(seg)] += seg
        else:
            hs[:len(h)] = h
        out[:, c] = np.fft.irfft(np.fft.rfft(xs) * np.fft.rfft(hs), n)
    return out


def add_at(buffer, signal, start, circular=False, gain=1.0):
    """Mixes signal into buffer at sample start (mono or stereo; circular wraps past the end)."""
    n = len(buffer)
    sig = signal * gain
    if buffer.ndim == 2 and sig.ndim == 1:
        sig = np.stack([sig, sig], axis=1)
    if circular:
        idx = (start + np.arange(len(sig))) % n
        np.add.at(buffer, idx, sig)
        return
    if start >= n:
        return
    end = min(n, start + len(sig))
    buffer[start:end] += sig[:end - start]


def pan(mono, position):
    """Equal-power pan: -1 left, +1 right."""
    angle = (position + 1.0) * np.pi / 4.0
    return np.stack([mono * np.cos(angle), mono * np.sin(angle)], axis=1)


# ---------------------------------------------------------------- levels and files

def peak_db(x):
    return 20.0 * np.log10(np.max(np.abs(x)) + 1e-12)


def true_peak_db(x, oversample=4):
    """Inter-sample peak estimate by FFT oversampling."""
    x = x[:, None] if x.ndim == 1 else x
    best = 0.0
    for c in range(x.shape[1]):
        s = x[:, c]
        n = len(s)
        spec = np.fft.rfft(s)
        up = np.fft.irfft(spec, n * oversample) * oversample
        best = max(best, np.max(np.abs(up)))
    return 20.0 * np.log10(best + 1e-12)


def normalize_peak(x, target_db):
    return x * db(target_db) / (np.max(np.abs(x)) + 1e-12)


_K1 = ([1.53512485958697, -2.69169618940638, 1.19839281085285], [1.0, -1.69065929318241, 0.73248077421585])
_K2 = ([1.0, -2.0, 1.0], [1.0, -1.99004745483398, 0.99007225036621])


def k_weighted(x):
    """ITU-R BS.1770 K-weighting at 48 kHz."""
    x = x[:, None] if x.ndim == 1 else x
    return np.stack([biquad(biquad(x[:, c], *_K1), *_K2) for c in range(x.shape[1])], axis=1)


def momentary_lufs_max(x, window=0.2):
    """
    Loudest window of K-weighted power (BS.1770 weighting). 200 ms by default: the ear integrates short sounds over
    roughly that span, so a 90 ms click and a 2 s chime compare fairly (the 400 ms momentary window flatters clicks).
    """
    k = k_weighted(x)
    power = np.sum(k ** 2, axis=1)
    win = samples(window)
    if len(power) < win:
        power = np.concatenate([power, np.zeros(win - len(power))])
    csum = np.concatenate([[0.0], np.cumsum(power)])
    hop = samples(0.01)
    best = 1e-12
    for start in range(0, len(power) - win + 1, hop):
        best = max(best, (csum[start + win] - csum[start]) / win)
    return -0.691 + 10.0 * np.log10(best)


def normalize_momentary(x, target_lufs, ceiling_db=-1.0):
    """Sets the loudest 400 ms to target_lufs, never above the true-peak ceiling."""
    gain = db(target_lufs - momentary_lufs_max(x))
    y = x * gain
    tp = true_peak_db(y)
    if tp > ceiling_db:
        y *= db(ceiling_db - tp)
    return y


def trim_tail(x, threshold_db=-66.0, fade_ms=8.0):
    """Drops the silent tail (doc 08: no stored silence) with a short fade."""
    mono = np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x)
    above = np.nonzero(mono > np.max(mono) * db(threshold_db))[0]
    end = int(above[-1]) + 1 if len(above) else len(x)
    end = min(len(x), end + samples(fade_ms / 1000.0))
    return fade(x[:end], 0.0, fade_ms / 1000.0)


def write_wav(path, x, bits=24):
    """Writes mono (n,) or stereo (n, 2) float audio in [-1, 1] as PCM WAV."""
    x = np.asarray(x, dtype=np.float64)
    chans = 1 if x.ndim == 1 else x.shape[1]
    clipped = np.clip(x, -1.0, 1.0)
    if bits == 24:
        ints = np.round(clipped * 8388607.0).astype(np.int32).reshape(-1)
        raw = ints.astype('<i4').tobytes()
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 4)[:, :3]
        data = b.tobytes()
    else:
        data = np.round(clipped * 32767.0).astype('<i2').reshape(-1).tobytes()
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(chans)
        w.setsampwidth(bits // 8)
        w.setframerate(SR)
        w.writeframes(data)


def read_wav(path):
    with wave.open(str(path), 'rb') as w:
        chans, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(frames)
    if width == 3:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3)
        ints = (b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int32) << 16))
        ints = np.where(ints & 0x800000, ints - 0x1000000, ints)
        x = ints / 8388607.0
    else:
        x = np.frombuffer(raw, dtype='<i2') / 32767.0
    x = x.reshape(-1, chans)
    return (x[:, 0] if chans == 1 else x), rate


def seam_jump(x):
    """Loop seam check: step between the last and first sample relative to the typical sample step."""
    x = x[:, None] if x.ndim == 1 else x
    steps = np.abs(np.diff(x, axis=0))
    typical = np.percentile(steps, 99.0, axis=0) + 1e-12
    seam = np.abs(x[0] - x[-1])
    return float(np.max(seam / typical))


def _biquad_mag(b, a, f):
    z = np.exp(-1j * 2.0 * np.pi * f / SR)
    return np.abs((b[0] + b[1] * z + b[2] * z * z) / (a[0] + a[1] * z + a[2] * z * z))


def integrated_lufs(x):
    """BS.1770 integrated loudness (K-weighting applied by its magnitude response, gated 400 ms blocks)."""
    x = x[:, None] if x.ndim == 1 else x
    k = fft_filter(x, lambda f: _biquad_mag(*_K1, f) * _biquad_mag(*_K2, f))
    power = np.sum(k ** 2, axis=1)
    win, hop = samples(0.4), samples(0.1)
    csum = np.concatenate([[0.0], np.cumsum(power)])
    blocks = np.array([(csum[s + win] - csum[s]) / win for s in range(0, len(power) - win + 1, hop)])
    loud = -0.691 + 10.0 * np.log10(blocks + 1e-20)
    gated = blocks[loud > -70.0]
    if len(gated) == 0:
        return -70.0
    relative = -0.691 + 10.0 * np.log10(np.mean(gated)) - 10.0
    final = blocks[(loud > -70.0) & (loud > relative)]
    return -0.691 + 10.0 * np.log10(np.mean(final))


def normalize_integrated(x, target_lufs, ceiling_db=-1.0):
    y = x * db(target_lufs - integrated_lufs(x))
    tp = peak_db(y)
    if tp > ceiling_db:
        y *= db(ceiling_db - tp)
    return y
