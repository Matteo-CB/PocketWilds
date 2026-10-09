"""
Pocket Wilds sound effects (T34B), synthesized from scratch. Every cue of the hatch family shares the key of the
refuge theme (F major) and the same instruments (tine, wooden mallet, glass bell), so tiers sound related.

Each recipe returns a mono float array at 48 kHz; pw_audio_build normalizes it to its category level.
"""
import numpy as np

from pw_audio_dsp import (SR, attack_curve, band_noise, bandpass, chain, convolve, damped_sine, db, env_ad, env_asr, fade,
                          fft_filter, fm_bell, formant_voice, glide, highpass, hz, lowpass, partials, peak, phase_of,
                          reverb_ir, samples, t_axis, white)

ROOM = reverb_ir(0.9, 0.5, damping_hz=4200.0, predelay=0.006, seed=11, stereo=False)
HALL = reverb_ir(2.8, 1.6, damping_hz=3800.0, predelay=0.02, seed=12, stereo=False, early=0.3)


def wet(x, ir, amount):
    """Dry plus a reverb send. Output keeps the tail."""
    y = convolve(x, ir)[:, 0]
    dry = np.concatenate([x, np.zeros(len(y) - len(x))])
    return dry + amount * y


def place(buffer, signal, at, release=0.05):
    """Mixes a note into the buffer. Its end always fades (a note cut while ringing clicks)."""
    start = samples(at)
    end = min(len(buffer), start + len(signal))
    part = signal[:end - start].copy()
    nr = min(samples(release), len(part))
    if nr > 0:
        part[len(part) - nr:] *= attack_curve(nr)[::-1]
    buffer[start:end] += part
    return buffer


# ---------------------------------------------------------------- instruments shared with the theme

def tine(f, seconds=1.6, vel=1.0, rng=None):
    """Kalimba-like tine: warm fundamental, a quick metallic overtone near 6x, a tiny contact tick."""
    t = t_axis(seconds)
    tau = 1.1 * (440.0 / f) ** 0.35
    spec = [(1.0, 1.0, tau), (2.0, 0.05 * vel, tau * 0.3), (5.95, 0.15 * vel, tau * 0.1), (9.4, 0.04 * vel, tau * 0.05)]
    x = partials(t, f, spec, attack=0.0015)
    if rng is not None:
        x += band_noise(len(t), 2500.0, 6000.0, rng) * env_ad(t, 0.0004, 0.003) * 0.04 * vel
    return x * vel


def mallet(f, seconds=1.2, vel=1.0, damp=1.0):
    """Soft wooden bar (marimba-like 1:4:10 partials), rounded attack. damp < 1 mutes it (felt on the bar)."""
    t = t_axis(seconds)
    tau = 0.55 * damp * (440.0 / f) ** 0.4
    x = partials(t, f, [(1.0, 1.0, tau), (4.0, 0.10 * vel, tau * 0.14), (9.9, 0.025 * vel, tau * 0.05)], attack=0.003)
    return x * vel


def glass(f, seconds=1.8, vel=1.0, ratio=2.0):
    """Glass bell / music box: two-operator FM, bright strike that rounds off."""
    t = t_axis(seconds)
    return fm_bell(t, f, ratio=ratio, index=0.4 + 1.0 * vel, index_tau=0.16, amp_tau=0.85 * (880.0 / f) ** 0.3,
                   shimmer=0.03) * vel


def warm_bell(f, seconds=2.4, vel=1.0):
    """Handbell blend: a clean FM voice plus a softer inharmonic one."""
    t = t_axis(seconds)
    a = fm_bell(t, f, ratio=2.0, index=1.0 * vel, index_tau=0.25, amp_tau=1.3)
    b = fm_bell(t, f, ratio=3.5, index=0.8 * vel, index_tau=0.12, amp_tau=0.6)
    return (0.75 * a + 0.25 * b) * vel


def swell(freqs, seconds, attack, release, level=1.0):
    """Soft pad swell: sine plus a detuned second harmonic per note."""
    t = t_axis(seconds)
    n = len(t)
    out = np.zeros(n)
    for i, f in enumerate(freqs):
        out += np.sin(2 * np.pi * f * t + i) + 0.25 * np.sin(2 * np.pi * f * 2.003 * t + 2 * i)
    return out * env_asr(n, attack, release) * level / max(len(freqs), 1)


def air(seconds, lo, hi, attack, release, rng):
    n = samples(seconds)
    return band_noise(n, lo, hi, rng) * env_asr(n, attack, release)


# ---------------------------------------------------------------- egg

def egg_tap(v):
    """J-SFX-EGG-001: soft ceramic and wood 'tok', warm transient, no metallic click."""
    rng = np.random.default_rng(100 + v)
    t = t_axis(0.3)
    f1 = (520.0, 468.0)[v]
    f = f1 * (1.0 + 0.04 * np.exp(-t / 0.015))
    body = np.sin(phase_of(f)) * np.exp(-t / 0.055)
    body += 0.32 * np.sin(2 * np.pi * f1 * 2.31 * t) * np.exp(-t / 0.024)
    body += 0.10 * np.sin(2 * np.pi * f1 * 4.6 * t) * np.exp(-t / 0.011)
    body *= np.clip(t / 0.0008, 0, 1)
    thump = 0.35 * np.sin(2 * np.pi * (190.0, 176.0)[v] * t) * env_ad(t, 0.001, 0.03)
    click = fft_filter(white(len(t), rng) * env_ad(t, 0.0003, 0.0012), bandpass(900.0, 3600.0)) * 0.6
    x = fft_filter(body + thump + click, chain(lowpass(6000.0, 2), peak(3000.0, -3.0, 1.2)))
    return wet(x, ROOM, 0.12)


def crack(v):
    """J-SFX-EGG-002: dry fine shell crack (a run of tiny clicks) and a subtle airy tick."""
    rng = np.random.default_rng(200 + v)
    t = t_axis(0.42)
    n = len(t)
    out = np.zeros(n)
    count = (6, 8, 5)[v]
    at = 0.004
    for i in range(count):
        length = samples(0.012)
        tt = np.arange(length) / SR
        burst = white(length, rng) * np.exp(-tt / 0.0012)
        modes = sum(damped_sine(tt, rng.uniform(2200.0, 6200.0), rng.uniform(0.002, 0.005), rng.uniform(0, 6.28))
                    for _ in range(2))
        click = fft_filter(burst, highpass(1500.0, 2)) * 0.5 + modes * 0.6
        amp = (0.86 ** i) * rng.uniform(0.6, 1.0)
        place(out, click * amp, at)
        if i == 0:
            flex = band_noise(samples(0.08), 900.0, 2000.0, rng) * env_ad(np.arange(samples(0.08)) / SR, 0.001, 0.03)
            place(out, flex * 0.35, at)
        at += rng.uniform(0.01, 0.05) * (0.6 if i < 3 else 1.25)
    tick = air(0.06, 5000.0, 9000.0, 0.005, 0.045, rng) * 0.1
    place(out, tick, min(at, 0.33))
    x = fft_filter(out, chain(lowpass(9000.0, 2), peak(3200.0, -3.5, 1.2)))
    return wet(x, ROOM, 0.1)


def hatch_pop():
    """J-SFX-EGG-003: soft rounded pop, a puff of air, three tiny glass notes."""
    rng = np.random.default_rng(300)
    t = t_axis(0.8)
    f = glide(t, 820.0, 250.0, 0.035)
    ph = phase_of(f)
    pop = np.sin(ph) * env_ad(t, 0.003, 0.075) + 0.22 * np.sin(2 * ph) * env_ad(t, 0.003, 0.04)
    puff = band_noise(len(t), 300.0, 1400.0, rng) * env_ad(t, 0.004, 0.05) * 0.18
    x = pop + puff
    for k, (note, at) in enumerate((('F6', 0.07), ('A6', 0.12), ('C7', 0.17))):
        place(x, glass(hz(note), 0.5, 0.45) * 0.13, at)
    x = fft_filter(x, chain(lowpass(9000.0, 2), highpass(90.0, 2)))
    return wet(x, ROOM, 0.18)


def reveal_rare():
    """J-SFX-EGG-004: short pleasant rising tines over a soft F major swell."""
    rng = np.random.default_rng(400)
    out = np.zeros(samples(1.3))
    place(out, swell([hz('F4'), hz('A4'), hz('C5')], 1.1, 0.12, 0.6, 0.16), 0.0)
    for i, note in enumerate(('C5', 'F5', 'A5', 'C6')):
        place(out, tine(hz(note), 1.1, 0.75 + 0.08 * i, rng) * 0.5, 0.07 * i)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(120.0, 2)))
    return wet(x, HALL, 0.22)


def reveal_epic():
    """J-SFX-EGG-005: richer, not louder: an F major 7 tine run doubled by glass bells."""
    rng = np.random.default_rng(500)
    out = np.zeros(samples(1.6))
    place(out, swell([hz('F4'), hz('A4'), hz('C5'), hz('E5')], 1.35, 0.15, 0.7, 0.15), 0.0)
    for i, note in enumerate(('F5', 'A5', 'C6', 'E6')):
        place(out, tine(hz(note), 1.2, 0.8, rng) * 0.42, 0.06 * i)
    for i, note in enumerate(('C6', 'E6', 'A6')):
        place(out, glass(hz(note), 1.2, 0.5) * 0.16, 0.12 + 0.07 * i)
    x = fft_filter(out, chain(lowpass(9500.0, 2), highpass(120.0, 2)))
    return wet(x, HALL, 0.27)


def reveal_legendary():
    """J-SFX-EGG-006: warm bell cluster after a short rising breath. No fanfare."""
    rng = np.random.default_rng(600)
    out = np.zeros(samples(2.0))
    place(out, air(0.16, 1500.0, 6000.0, 0.14, 0.02, rng) * 0.06, 0.0)
    strike = 0.13
    place(out, tine(hz('F4'), 1.6, 0.8, rng) * 0.4, strike)
    for i, note in enumerate(('F5', 'A5', 'C6', 'F6')):
        place(out, warm_bell(hz(note), 1.7, 0.75) * 0.24, strike + 0.025 * i)
    place(out, swell([hz('F4'), hz('C5'), hz('A5')], 1.5, 0.08, 0.9, 0.12), strike)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(110.0, 2), peak(2800.0, -2.0, 1.0)))
    return wet(x, HALL, 0.3)


def reveal_fabulous():
    """J-SFX-EGG-007: airy starlit F major 9 chord with a glass arpeggio. Distinct and still cozy."""
    rng = np.random.default_rng(700)
    out = np.zeros(samples(2.3))
    place(out, swell([hz('F4'), hz('A4'), hz('C5'), hz('E5'), hz('G5')], 1.9, 0.3, 1.0, 0.17), 0.0)
    place(out, air(1.7, 6000.0, 11000.0, 0.5, 0.9, rng) * 0.035, 0.05)
    place(out, warm_bell(hz('F5'), 1.8, 0.6) * 0.2, 0.0)
    for i, (note, at) in enumerate((('G6', 0.05), ('A6', 0.13), ('C7', 0.21), ('E7', 0.29), ('C7', 0.46), ('A6', 0.62))):
        place(out, glass(hz(note), 1.1, 0.55 - 0.04 * max(0, i - 3)) * 0.13, at)
    x = fft_filter(out, chain(lowpass(11000.0, 2), highpass(120.0, 2), peak(3200.0, -2.0, 1.0)))
    return wet(x, HALL, 0.34)


# ---------------------------------------------------------------- currency and rewards

def collect_light(v):
    """J-SFX-CURRENCY-001: soft crystalline pling, rounded transient (same pitch: the service steps it)."""
    t = t_axis(0.35)
    f = hz('C6')
    x = fm_bell(t, f, ratio=(2.0, 2.0, 3.0)[v], index=(0.9, 0.7, 0.6)[v], index_tau=0.04,
                amp_tau=(0.11, 0.13, 0.10)[v], attack=0.003)
    x += (0.25, 0.15, 0.2)[v] * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t / 0.05) * np.clip(t / 0.003, 0, 1)
    x = fft_filter(x, lowpass(10000.0, 2))
    return wet(x, ROOM, 0.12)


def mastery_up():
    """J-SFX-REWARD-002: cheerful three-note tine motif with a glass echo."""
    rng = np.random.default_rng(800)
    out = np.zeros(samples(1.0))
    for i, (note, at, vel) in enumerate((('C5', 0.0, 0.7), ('F5', 0.09, 0.75), ('A5', 0.18, 0.85))):
        place(out, tine(hz(note), 0.9, vel, rng) * 0.5, at)
    place(out, glass(hz('A6'), 0.8, 0.4) * 0.1, 0.18)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(120.0, 2)))
    return wet(x, HALL, 0.2)


def purchase_success():
    """J-UI-010: distinct but elegant: two warm bells a fifth apart."""
    out = np.zeros(samples(1.2))
    place(out, warm_bell(hz('F5'), 1.0, 0.7) * 0.4, 0.0)
    place(out, warm_bell(hz('A5'), 1.0, 0.45) * 0.18, 0.09)
    place(out, warm_bell(hz('C6'), 1.0, 0.75) * 0.4, 0.09)
    place(out, glass(hz('F6'), 0.7, 0.4) * 0.1, 0.18)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(120.0, 2), peak(3000.0, -2.0, 1.0)))
    return wet(x, HALL, 0.22)


# ---------------------------------------------------------------- interface

def ui_tap(v):
    """J-UI-001: tiny soft click, mostly a short wooden body."""
    rng = np.random.default_rng(900 + v)
    t = t_axis(0.09)
    body = damped_sine(t, (520.0, 580.0, 480.0)[v], 0.014)
    tip = 0.55 * damped_sine(t, (1500.0, 1650.0, 1400.0)[v], 0.005)
    grit = fft_filter(white(len(t), rng) * env_ad(t, 0.0002, 0.0008), lowpass(5000.0, 2)) * 0.25
    x = (body + tip + grit) * np.clip(t / 0.0004, 0, 1)
    return fft_filter(x, chain(lowpass(7000.0, 2), highpass(150.0, 2)))


def ui_confirm():
    """J-UI-002: warm pop with a small rising tine pair."""
    rng = np.random.default_rng(910)
    t = t_axis(0.3)
    f = glide(t, 620.0, 440.0, 0.02)
    out = np.sin(phase_of(f)) * env_ad(t, 0.002, 0.06) * 0.8
    place(out, tine(hz('F5'), 0.3, 0.5, rng) * np.exp(-t_axis(0.3) / 0.12) * 0.35, 0.0)
    place(out, tine(hz('C6'), 0.25, 0.5, rng) * np.exp(-t_axis(0.25) / 0.1) * 0.3, 0.045)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(150.0, 2)))
    return wet(x, ROOM, 0.08)


def ui_back():
    """J-UI-003: softer, lower click."""
    t = t_axis(0.16)
    f = glide(t, 420.0, 330.0, 0.02)
    x = np.sin(phase_of(f)) * env_ad(t, 0.002, 0.045) + 0.3 * damped_sine(t, 1100.0, 0.006)
    return fft_filter(x, chain(lowpass(5000.0, 2), highpass(150.0, 2)))


def panel_open():
    """J-UI-004: short upward whoosh that lands on a soft pop."""
    rng = np.random.default_rng(920)
    t = t_axis(0.3)
    n = len(t)
    out = np.zeros(n)
    for i, center in enumerate((500.0, 800.0, 1250.0, 1900.0, 2800.0)):
        bandn = band_noise(n, center / 1.4, center * 1.4, rng)
        mid = 0.03 + 0.03 * i
        out += bandn * np.exp(-0.5 * ((t - mid) / 0.035) ** 2) * (1.0 - 0.1 * i)
    out *= 0.16
    pop_t = t_axis(0.12)
    pop = np.sin(phase_of(glide(pop_t, 560.0, 420.0, 0.02))) * env_ad(pop_t, 0.003, 0.04) * 0.5
    place(out, pop, 0.15)
    return fft_filter(out, chain(lowpass(7000.0, 2), highpass(200.0, 2)))


def error_soft():
    """J-UI-009: muted, gentle 'not yet': two felt notes stepping down. Never a buzzer."""
    out = np.zeros(samples(0.45))
    place(out, mallet(hz('A4'), 0.4, 0.7, damp=0.3), 0.0)
    place(out, mallet(hz('F4'), 0.4, 0.6, damp=0.35), 0.1)
    x = fft_filter(out, chain(lowpass(1500.0, 2), highpass(150.0, 2)))
    return wet(x, ROOM, 0.08)


# ---------------------------------------------------------------- animals (stylized, never a real recording)

RABBIT_FORMANTS = [(1100.0, 0.5, 1.0), (2400.0, 0.35, 0.8), (3400.0, 0.3, 0.3)]
SMALL_FORMANTS = [(1300.0, 0.5, 1.0), (2700.0, 0.35, 0.7), (3600.0, 0.3, 0.25)]


def _eep(seconds, f_start, f_end, rise_tau, formants, rng, breath=0.15, hold=0.03, decay=0.05, attack=0.012):
    t = t_axis(seconds)
    f0 = f_end + (f_start - f_end) * np.exp(-t / rise_tau)
    f0 = f0 * (1.0 + 0.008 * np.sin(2 * np.pi * 9.0 * t))
    v = formant_voice(t, f0, formants, tilt_db=-4.0, harmonics=6, breath=breath, rng=rng)
    return v * env_ad(t, attack, decay, hold=hold)


def _puff(seconds, lo, hi, attack, decay, rng):
    t = t_axis(seconds)
    return band_noise(len(t), lo, hi, rng) * env_ad(t, attack, decay)


def rabbit_happy(v):
    """J-AN-RabbitHappy: tiny breathy chirp or pop (single, double, or after a nose puff)."""
    rng = np.random.default_rng(1000 + v)
    out = np.zeros(samples(0.32))
    if v == 0:
        place(out, _eep(0.2, 820.0, 1200.0, 0.05, RABBIT_FORMANTS, rng), 0.0)
    elif v == 1:
        place(out, _eep(0.12, 840.0, 1150.0, 0.04, RABBIT_FORMANTS, rng, hold=0.015, decay=0.03), 0.0)
        place(out, _eep(0.14, 920.0, 1350.0, 0.04, RABBIT_FORMANTS, rng, hold=0.02, decay=0.035) * 0.85, 0.085)
    else:
        place(out, _puff(0.06, 1500.0, 4500.0, 0.008, 0.02, rng) * 0.08, 0.0)
        place(out, _eep(0.2, 760.0, 1150.0, 0.06, RABBIT_FORMANTS, rng), 0.05)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(300.0, 2)))
    return wet(x, ROOM, 0.06)


def rabbit_sleep(v):
    """J-AN-RabbitSleep: soft tiny nose breath (inhale with nostril flutter, or a sleepy exhale)."""
    rng = np.random.default_rng(1100 + v)
    if v == 0:
        t = t_axis(0.8)
        n = len(t)
        x = band_noise(n, 900.0, 3000.0, rng, order=4) + 0.3 * band_noise(n, 300.0, 900.0, rng, order=4)
        x *= env_asr(n, 0.28, 0.3) * (1.0 + 0.15 * np.sin(2 * np.pi * 9.0 * t))
    else:
        t = t_axis(0.65)
        n = len(t)
        x = band_noise(n, 400.0, 1700.0, rng, order=4) * env_ad(t, 0.05, 0.18)
        x += 0.05 * np.sin(2 * np.pi * 262.0 * t) * env_ad(t, 0.06, 0.15) * 4.0
    return fft_filter(x, lowpass(4500.0, 3))


def small_chirp(v):
    """J-AN-SmallQuadruped_CuteChirp: family bank (pip, little trill, questioning eep, chuff and pip)."""
    rng = np.random.default_rng(1200 + v)
    out = np.zeros(samples(0.3))
    if v == 0:
        place(out, _eep(0.12, 980.0, 1250.0, 0.03, SMALL_FORMANTS, rng, hold=0.02, decay=0.03), 0.0)
    elif v == 1:
        for i in range(3):
            place(out, _eep(0.06, 1100.0, 1300.0, 0.015, SMALL_FORMANTS, rng, hold=0.008, decay=0.014,
                            attack=0.006) * (1.0, 0.8, 0.65)[i], 0.06 * i)
    elif v == 2:
        t = t_axis(0.22)
        f0 = 900.0 - 80.0 * np.sin(np.pi * np.clip(t / 0.08, 0, 1)) + 300.0 * np.clip((t - 0.08) / 0.1, 0, 1)
        voice = formant_voice(t, f0, SMALL_FORMANTS, tilt_db=-4.0, harmonics=6, breath=0.12, rng=rng)
        place(out, voice * env_ad(t, 0.012, 0.04, hold=0.12), 0.0)
    else:
        place(out, _puff(0.05, 800.0, 2500.0, 0.004, 0.015, rng) * 0.1, 0.0)
        place(out, _eep(0.12, 1050.0, 1180.0, 0.03, SMALL_FORMANTS, rng, hold=0.02, decay=0.03), 0.04)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(300.0, 2)))
    return wet(x, ROOM, 0.06)


def small_sleep(v):
    """J-AN-SmallQuadruped_Sleep: a light inhale or a small sigh."""
    rng = np.random.default_rng(1300 + v)
    if v == 0:
        t = t_axis(0.6)
        n = len(t)
        x = band_noise(n, 1000.0, 3200.0, rng, order=4) * env_asr(n, 0.22, 0.25)
    else:
        t = t_axis(0.85)
        n = len(t)
        x = band_noise(n, 500.0, 2000.0, rng, order=4) * env_ad(t, 0.08, 0.22)
        x += 0.18 * np.sin(2 * np.pi * 300.0 * t) * env_ad(t, 0.1, 0.18)
    return fft_filter(x, lowpass(4500.0, 3))


# ---------------------------------------------------------------- T34B2: interface, currency, rewards

def panel_close():
    """J-UI-005: the panel leaves: a soft pop, then a short downward whoosh."""
    rng = np.random.default_rng(930)
    t = t_axis(0.3)
    n = len(t)
    out = np.zeros(n)
    pop_t = t_axis(0.1)
    place(out, np.sin(phase_of(glide(pop_t, 480.0, 360.0, 0.02))) * env_ad(pop_t, 0.003, 0.035) * 0.45, 0.0)
    for i, center in enumerate((2400.0, 1600.0, 1050.0, 700.0, 460.0)):
        bandn = band_noise(n, center / 1.4, center * 1.4, rng)
        mid = 0.05 + 0.028 * i
        out += bandn * np.exp(-0.5 * ((t - mid) / 0.032) ** 2) * (0.6 + 0.1 * i) * 0.14
    return fft_filter(out, chain(lowpass(6500.0, 2), highpass(200.0, 2)))


def ui_tab(v):
    """J-UI-006: subtle page turn of the Codex: a soft paper flick on a tiny wooden tick."""
    rng = np.random.default_rng(940 + v)
    t = t_axis(0.12)
    flick = band_noise(len(t), (1800.0, 2200.0)[v], 5200.0, rng) * env_ad(t, 0.004, 0.018) * 0.35
    tick = damped_sine(t, (880.0, 960.0)[v], 0.012) * np.clip(t / 0.0005, 0, 1) * 0.6
    return fft_filter(flick + tick, chain(lowpass(7000.0, 2), highpass(250.0, 2)))


def toggle_on():
    """J-UI-007: something switched on (favorite heart): two small rising glass notes."""
    out = np.zeros(samples(0.5))
    place(out, glass(hz('C6'), 0.45, 0.55) * 0.5, 0.0)
    place(out, glass(hz('F6'), 0.45, 0.6) * 0.5, 0.055)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(200.0, 2)))
    return wet(x, ROOM, 0.1)


def toggle_off():
    """J-UI-008: switched off: two muted wooden notes stepping down."""
    out = np.zeros(samples(0.35))
    place(out, mallet(hz('F5'), 0.3, 0.6, damp=0.35), 0.0)
    place(out, mallet(hz('C5'), 0.3, 0.55, damp=0.35), 0.06)
    return fft_filter(out, chain(lowpass(3000.0, 2), highpass(200.0, 2)))


def purchase_pending():
    """J-UI-011: the store is still answering: two calm wooden ticks, neutral."""
    out = np.zeros(samples(0.4))
    place(out, mallet(hz('A4'), 0.2, 0.6, damp=0.25), 0.0)
    place(out, mallet(hz('A4'), 0.2, 0.45, damp=0.25), 0.16)
    return fft_filter(out, chain(lowpass(2500.0, 2), highpass(180.0, 2)))


def spend_light():
    """J-SFX-CURRENCY-002: Light leaves the counter: a soft glass tick gliding down (about 150 ms)."""
    t = t_axis(0.25)
    f = glide(t, hz('F6'), hz('C6'), 0.03)
    x = np.sin(phase_of(f)) * env_ad(t, 0.002, 0.05)
    x += 0.18 * np.sin(phase_of(f * 2.76)) * env_ad(t, 0.002, 0.02)
    x = fft_filter(x, chain(lowpass(9000.0, 2), highpass(300.0, 2)))
    return wet(x, ROOM, 0.1)


def claim():
    """J-SFX-REWARD-001: a reward or an unlock is yours: a soft strummed F major chord (about 400 ms)."""
    rng = np.random.default_rng(950)
    out = np.zeros(samples(1.0))
    place(out, tine(hz('F4'), 0.9, 0.6, rng) * 0.35, 0.0)
    for i, note in enumerate(('F5', 'A5', 'C6')):
        place(out, tine(hz(note), 0.8, 0.7, rng) * 0.4, 0.03 * i)
    x = fft_filter(out, chain(lowpass(9000.0, 2), highpass(120.0, 2)))
    return wet(x, HALL, 0.16)


# ---------------------------------------------------------------- T34B2: eating, birds

def _nibble(rng, body_hz, bright):
    """One tiny bite: a short crunchy burst on a small body."""
    n = samples(0.03)
    t = np.arange(n) / SR
    crunch = band_noise(n, 1500.0 * bright, 5200.0 * bright, rng) * np.exp(-t / 0.006)
    clicks = np.zeros(n)
    for _ in range(3):
        at = int(rng.integers(0, samples(0.012)))
        clicks[at:at + 24] += rng.uniform(0.4, 1.0) * np.hanning(24)
    clicks = fft_filter(clicks, highpass(2000.0, 2))
    body = damped_sine(t, body_hz, 0.01) * 0.5
    return (crunch * 0.6 + clicks * 0.5 + body) * np.clip(t / 0.0008, 0, 1)


def rabbit_eat(v):
    """J-AN-RabbitEat: quick little nibbles of a rabbit eating grass."""
    rng = np.random.default_rng(1400 + v)
    count, gap = ((4, 0.085), (5, 0.075))[v]
    out = np.zeros(samples(count * gap + 0.12))
    for i in range(count):
        place(out, _nibble(rng, 520.0 + 40.0 * rng.standard_normal(), 1.0) * rng.uniform(0.7, 1.0), i * gap + rng.uniform(0.0, 0.012), release=0.004)
    x = fft_filter(out, chain(lowpass(7500.0, 2), highpass(250.0, 2), peak(3200.0, -3.0, 1.2)))
    return wet(x, ROOM, 0.05)


def small_eat(v):
    """J-DECOR-EatBiteSoft: family bank of soft bites (seeds, leaves)."""
    rng = np.random.default_rng(1500 + v)
    count, gap, bright = ((3, 0.13, 0.9), (4, 0.11, 1.0), (3, 0.15, 0.8))[v]
    out = np.zeros(samples(count * gap + 0.12))
    for i in range(count):
        place(out, _nibble(rng, 430.0 + 50.0 * rng.standard_normal(), bright) * rng.uniform(0.7, 1.0), i * gap + rng.uniform(0.0, 0.015), release=0.004)
    x = fft_filter(out, chain(lowpass(7000.0, 2), highpass(250.0, 2), peak(3200.0, -3.0, 1.2)))
    return wet(x, ROOM, 0.05)


def bird_takeoff(v):
    """J-WORLD-BirdTakeOff: a small bird flies off: soft wing beats slowing and fading away."""
    rng = np.random.default_rng(1600 + v)
    beats = (7, 6, 8)[v]
    out = np.zeros(samples(0.75))
    at = 0.0
    rate = (15.0, 13.0, 17.0)[v]
    for i in range(beats):
        n = samples(0.05)
        t = np.arange(n) / SR
        flap = band_noise(n, 350.0, 2600.0, rng) * np.sin(np.pi * np.clip(t / 0.045, 0, 1)) ** 2
        place(out, flap * (1.0 - 0.1 * i) * rng.uniform(0.8, 1.0), at, release=0.005)
        at += 1.0 / rate * (1.0 + 0.06 * i)
    x = fft_filter(out, chain(lowpass(5000.0, 2), highpass(250.0, 2)))
    return wet(x, ROOM, 0.12)


# ---------------------------------------------------------------- T35A: upgrades, zones, refuge level, biome mastery

def upgrade_buy(v):
    """J-SFX-UPGRADE-001: a small pluck that rises (a tine pulled up a third, a glass grace note above it). Three
    variants a step apart, so a run of purchases climbs instead of repeating one sound."""
    rng = np.random.default_rng(1100 + v)
    root = hz(('F5', 'G5', 'A5')[v])
    t = t_axis(0.5)
    f = glide(t, root * 0.84, root, 0.025)
    x = np.sin(phase_of(f)) * env_ad(t, 0.002, 0.11)
    x += 0.12 * np.sin(phase_of(f * 5.95)) * env_ad(t, 0.0015, 0.02)
    x += band_noise(len(t), 2500.0, 6000.0, rng) * env_ad(t, 0.0004, 0.003) * 0.03
    out = np.zeros(samples(0.6))
    place(out, x * 0.6, 0.0)
    place(out, glass(root * 2.0, 0.4, 0.35) * 0.12, 0.045)
    y = fft_filter(out, chain(lowpass(9500.0, 2), highpass(220.0, 2)))
    return wet(y, ROOM, 0.12)


def upgrade_milestone():
    """J-SFX-UPGRADE-002: a soft chord of two notes (F and A, a warm bell under a tine), the x2 milestone."""
    rng = np.random.default_rng(1110)
    out = np.zeros(samples(1.1))
    place(out, warm_bell(hz('F5'), 0.8, 0.6) * 0.32, 0.0)
    place(out, tine(hz('A5'), 0.75, 0.75, rng) * 0.42, 0.0)
    place(out, tine(hz('C6'), 0.65, 0.6, rng) * 0.3, 0.07)
    place(out, glass(hz('F6'), 0.6, 0.35) * 0.08, 0.12)
    y = fft_filter(out, chain(lowpass(9000.0, 2), highpass(130.0, 2)))
    return wet(y, ROOM, 0.2)


def zone_open():
    """J-SFX-REWARD-005: a new zone opens: leaves rustle up as an F major arpeggio rises (about 1.2 s)."""
    rng = np.random.default_rng(1120)
    out = np.zeros(samples(1.4))
    rustle = air(0.75, 900.0, 5200.0, 0.2, 0.45, rng) * 0.09
    place(out, fft_filter(rustle, chain(lowpass(6000.0, 2), highpass(700.0, 2))), 0.0, release=0.25)
    for i, note in enumerate(('F4', 'A4', 'C5', 'F5', 'A5')):
        place(out, tine(hz(note), 0.7, 0.55 + 0.07 * i, rng) * 0.36, 0.08 + 0.09 * i)
    place(out, warm_bell(hz('C6'), 0.7, 0.5) * 0.14, 0.5)
    place(out, swell([hz('F4'), hz('C5'), hz('A5')], 0.8, 0.25, 0.4, 0.12), 0.2, release=0.3)
    y = fft_filter(out, chain(lowpass(9000.0, 2), highpass(110.0, 2)))
    return wet(y, ROOM, 0.22)


def refuge_level_up():
    """J-SFX-REWARD-006: the refuge levels up: a warm four-note fanfare on bells and tines, F to F (about 1.2 s)."""
    rng = np.random.default_rng(1130)
    out = np.zeros(samples(1.3))
    for i, (note, at) in enumerate((('C5', 0.0), ('F5', 0.09), ('A5', 0.18), ('F6', 0.3))):
        place(out, tine(hz(note), 0.75, 0.6 + 0.08 * i, rng) * 0.38, at)
        place(out, warm_bell(hz(note), 0.7, 0.45) * 0.12, at + 0.01)
    place(out, swell([hz('F4'), hz('A4'), hz('C5')], 0.75, 0.15, 0.4, 0.1), 0.25, release=0.3)
    place(out, glass(hz('C7'), 0.5, 0.3) * 0.06, 0.35)
    y = fft_filter(out, chain(lowpass(9000.0, 2), highpass(120.0, 2), peak(3000.0, -2.0, 1.0)))
    return wet(y, ROOM, 0.22)


def biome_mastery_up():
    """J-SFX-REWARD-007: the biome's mastery grows: two soft wooden notes and a leafy rustle (about 0.8 s)."""
    rng = np.random.default_rng(1140)
    out = np.zeros(samples(1.2))
    place(out, mallet(hz('F5'), 0.7, 0.7) * 0.45, 0.0)
    place(out, mallet(hz('C6'), 0.7, 0.75) * 0.45, 0.11)
    place(out, tine(hz('A6'), 0.6, 0.45, rng) * 0.16, 0.16)
    rustle = air(0.5, 1200.0, 6000.0, 0.08, 0.3, rng) * 0.06
    place(out, fft_filter(rustle, chain(lowpass(7000.0, 2), highpass(900.0, 2))), 0.05, release=0.2)
    y = fft_filter(out, chain(lowpass(9000.0, 2), highpass(150.0, 2)))
    return wet(y, ROOM, 0.16)


# ---------------------------------------------------------------- T36: daily visit (wish, gift box, calendar, Gems)

def _soft_start(x, seconds=0.0015):
    """A 1.5 ms fade-in: the zero-phase filters leave a few samples before the attack, and the clip must start on 0."""
    return fade(x, seconds, 0.0)


def _glass_note(f, seconds, vel, tau):
    """The house glass note, damped sooner (tau): a short cue keeps its strike and loses the long music-box ring."""
    return glass(f, seconds, vel) * np.exp(-t_axis(seconds) / tau)


def _tine_under(f, seconds, vel, tau, rng):
    """A quiet tine under a glass note: the warm body that keeps the glass from sounding thin on a phone."""
    return tine(f, seconds, vel, rng) * np.exp(-t_axis(seconds) / tau)


def _bubble(f_start, f_end, rng, seconds=0.12):
    """A soft bubble pop: a round sine whose pitch rises as the bubble closes, a hint of its film. No click."""
    t = t_axis(seconds)
    f = glide(t, f_start, f_end, 0.014)
    ph = phase_of(f)
    body = np.sin(ph) * env_ad(t, 0.0025, 0.03) + 0.12 * np.sin(2.0 * ph) * env_ad(t, 0.0025, 0.014)
    film = band_noise(len(t), 900.0, 3000.0, rng) * env_ad(t, 0.0008, 0.004) * 0.05
    return body + film


def _wood_knock(f, rng, cavity=0.0, seconds=0.25):
    """A knock on a small round box of light wood: a hollow note that settles a hair lower, two short wood partials,
    the air of the box under them (cavity, Hz) and a soft contact. Rounded: no metallic click."""
    t = t_axis(seconds)
    fb = f * (1.0 + 0.05 * np.exp(-t / 0.012))
    body = np.sin(phase_of(fb)) * np.exp(-t / 0.045)
    body += 0.28 * np.sin(2.0 * np.pi * f * 2.32 * t) * np.exp(-t / 0.018)
    body += 0.07 * np.sin(2.0 * np.pi * f * 4.1 * t) * np.exp(-t / 0.008)
    if cavity > 0.0:
        body += 0.45 * np.sin(2.0 * np.pi * cavity * t) * env_ad(t, 0.002, 0.035)
    contact = fft_filter(white(len(t), rng) * env_ad(t, 0.0003, 0.0015), bandpass(700.0, 3000.0)) * 0.3
    return (body + contact) * np.clip(t / 0.0008, 0.0, 1.0)


def wish_done(v):
    """J-SFX-FEEL-009: a wish comes true: a soft bubble pops (its pitch rises to F), then two warm glass notes rise over
    quiet tines an octave below: A to C, or A to D for the second variant (a fourth, its bubble starting a little
    higher, its notes a touch sooner), so a run of wishes does not repeat one sound. About 0.7 s."""
    rng = np.random.default_rng(1700 + v)
    out = np.zeros(samples(0.9))
    place(out, _bubble((400.0, 430.0)[v], hz('F5'), rng) * 0.42, 0.0, release=0.02)
    second = ('C6', 'D6')[v]
    for note, at, vel, level in (('A5', (0.085, 0.08)[v], 0.4, 0.42), (second, (0.19, 0.18)[v], 0.48, 0.5)):
        place(out, _glass_note(hz(note), 0.6, vel, 0.2) * level, at)
        place(out, _tine_under(hz(note) / 2.0, 0.5, 0.45, 0.15, rng) * level * 0.3, at)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(160.0, 2)))
    return _soft_start(wet(x, ROOM, 0.14))


def gift_open():
    """J-SFX-FEEL-010: the daily gift box opens: a hollow knock of its wooden lid with a small puff of air, small glass
    notes rising on F major (F, A, C, F), and the lid set down beside the box. About 0.8 s."""
    rng = np.random.default_rng(1710)
    out = np.zeros(samples(1.0))
    place(out, _wood_knock(hz('A4'), rng, cavity=hz('C4')) * 0.5, 0.0, release=0.03)
    place(out, _puff(0.08, 300.0, 1400.0, 0.004, 0.025, rng) * 0.07, 0.004)
    place(out, _tine_under(hz('F4'), 0.6, 0.5, 0.2, rng) * 0.18, 0.07)
    for i, (note, at) in enumerate((('F5', 0.07), ('A5', 0.13), ('C6', 0.19), ('F6', 0.25))):
        place(out, _glass_note(hz(note), 0.6, 0.38 + 0.05 * i, 0.16 + 0.03 * i) * (0.3 + 0.03 * i), at)
    place(out, _wood_knock(hz('D5'), rng) * 0.1, 0.21, release=0.03)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(140.0, 2)))
    return _soft_start(wet(x, ROOM, 0.16))


def paw_stamp(v):
    """J-SFX-FEEL-011: the paw stamp of the calendar: a felt pad pressed on card (the puff of the felt, a short soft
    thump, a faint touch of the card), then the inked felt lets go. Two variants of pad and press. About 0.3 s."""
    rng = np.random.default_rng(1720 + v)
    out = np.zeros(samples(0.36))
    t = t_axis(0.14)
    thump = np.sin(phase_of(glide(t, (420.0, 390.0)[v], (330.0, 300.0)[v], 0.015))) * env_ad(t, 0.004, 0.024)
    felt = band_noise(len(t), (320.0, 280.0)[v], (1300.0, 1100.0)[v], rng) * env_ad(t, 0.006, 0.034) * 0.6
    card = damped_sine(t, (640.0, 580.0)[v], 0.012) * np.clip(t / 0.002, 0.0, 1.0) * 0.18
    place(out, thump * 0.45 + felt + card, 0.0, release=0.03)
    lt = t_axis(0.06)
    lift = band_noise(len(lt), 800.0, 2200.0, rng) * env_ad(lt, 0.005, 0.012) * 0.05
    lift += damped_sine(lt, 700.0, 0.004) * np.clip(lt / 0.0015, 0.0, 1.0) * 0.03
    place(out, lift, (0.15, 0.17)[v], release=0.01)
    x = fft_filter(out, chain(lowpass(3200.0, 2), highpass(180.0, 2)))
    return _soft_start(wet(x, ROOM, 0.05))


def gems_collect():
    """J-SFX-CURRENCY-004: Gems arrive at the counter: a clear rounded chime, glass C and F rolled together over a tine,
    a softer A just after (an F major chord). A pearl of glass, never coins. About 0.5 s."""
    rng = np.random.default_rng(1730)
    out = np.zeros(samples(0.8))
    place(out, _tine_under(hz('F5'), 0.6, 0.4, 0.2, rng) * 0.3, 0.0)
    place(out, _glass_note(hz('C6'), 0.6, 0.45, 0.2) * 0.32, 0.0)
    place(out, _glass_note(hz('F6'), 0.6, 0.5, 0.2) * 0.4, 0.012)
    place(out, _glass_note(hz('A6'), 0.55, 0.4, 0.17) * 0.26, 0.07)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(200.0, 2), peak(3000.0, -2.0, 1.0)))
    return _soft_start(wet(x, ROOM, 0.14))


# ---------------------------------------------------------------- T37: activities (doc 01 Contenu, section 12.0;
# doc 05 Sensations, sheet 20 and section 6.1). Same house family: F major, kalimba tines, wooden bars, glass of the
# music box. Soft attacks, short tails, nothing that hurries the player.

STREAK_STEPS = 8
CARD_STEPS = ('F5', 'G5', 'A5', 'C6', 'D6')


def _small_chime(f, seconds, vel, tau):
    """A small glass chime: the house glass note with the faint inharmonic partial of a little cup of glass (x2.76),
    damped sooner (tau) so it rings short."""
    t = t_axis(seconds)
    x = glass(f, seconds, vel)
    x += 0.12 * vel * np.sin(2.0 * np.pi * f * 2.76 * t) * np.exp(-t / 0.09) * np.clip(t / 0.003, 0.0, 1.0)
    return x * np.exp(-t / tau)


def activity_start():
    """J-SFX-ACT-000-01: a game begins: one small glass chime on F, a warm tine an octave below it, and the music comes
    in after it (the presenter). One strike, no countdown, nothing that hurries. About 0.9 s."""
    rng = np.random.default_rng(1800)
    out = np.zeros(samples(1.1))
    place(out, _tine_under(hz('F5'), 0.9, 0.45, 0.32, rng) * 0.24, 0.0)
    place(out, _small_chime(hz('F6'), 1.0, 0.55, 0.34) * 0.46, 0.006)
    x = fft_filter(out, chain(lowpass(8500.0, 2), highpass(180.0, 2), peak(3000.0, -2.0, 1.0)))
    return _soft_start(wet(x, ROOM, 0.16))


def activity_end():
    """J-SFX-ACT-000-02: the game is over: a soft kalimba chord rolled downward (C, A, F, then C and F below), the
    hands coming to rest; the presenter lowers the music by 5 dB at the same moment. Distinct from the Claim chord,
    which rolls upward. About 1.3 s."""
    rng = np.random.default_rng(1810)
    out = np.zeros(samples(1.7))
    for i, (note, vel) in enumerate((('C6', 0.48), ('A5', 0.52), ('F5', 0.58), ('C5', 0.6), ('F4', 0.62))):
        place(out, _tine_under(hz(note), 1.4, vel, 0.55, rng) * 0.34, 0.04 * i)
    place(out, swell([hz('C4'), hz('F4'), hz('A4')], 1.2, 0.18, 0.7, 0.06), 0.12, release=0.3)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(110.0, 2)))
    return _soft_start(wet(x, HALL, 0.14))


def result_star(v):
    """J-SFX-FEEL-013 and J-SFX-ACT-000-03: a result star lands: one kalimba tine with its glass note an octave above,
    F, A then C for the first, second and third star, so the three stars spell an F major chord. The third one rests
    on a quiet low F (the chord is complete). About 0.35 s, then a short ring."""
    rng = np.random.default_rng(1820 + v)
    note = hz(('F5', 'A5', 'C6')[v])
    out = np.zeros(samples(0.8))
    place(out, _tine_under(note, 0.75, 0.75, 0.24, rng) * 0.55, 0.0)
    place(out, _glass_note(note * 2.0, 0.6, 0.42, 0.15) * 0.2 * (hz('F5') / note) ** 1.5, 0.004)
    if v == 2:
        place(out, _tine_under(hz('F4'), 0.75, 0.5, 0.3, rng) * 0.18, 0.0)
    x = fft_filter(out, chain(lowpass(7000.0, 2), highpass(160.0, 2)))
    return _soft_start(wet(x, ROOM, 0.12))


def card_flip(v):
    """J-SFX-FEEL-012: a reward card turns over: the air of the paper as it turns (a soft swish whose band rises), the
    card laid flat (a light paper tap) and, with the tap, a small muted wooden note that climbs with each card: F, G,
    A, C, D (F major pentatonic). A bounded run of five (doc 05, section 6.4): past the fifth card, no sound per card.
    About 0.22 s."""
    rng = np.random.default_rng(1830 + v)
    out = np.zeros(samples(0.36))
    n = samples(0.13)
    t = np.arange(n) / SR
    rise = np.clip(t / 0.13, 0.0, 1.0)
    swish = band_noise(n, 600.0, 1900.0, rng) * (1.0 - rise) + band_noise(n, 1200.0, 3600.0, rng) * rise * 0.5
    place(out, swish * np.sin(np.pi * rise) ** 2 * 0.16, 0.0, release=0.02)
    tt = t_axis(0.08)
    tap = damped_sine(tt, (390.0, 405.0, 420.0, 440.0, 455.0)[v], 0.011) * np.clip(tt / 0.0015, 0.0, 1.0) * 0.32
    tap += fft_filter(white(len(tt), rng) * env_ad(tt, 0.0006, 0.004), bandpass(1000.0, 3600.0)) * 0.1
    place(out, tap, 0.11, release=0.02)
    place(out, mallet(hz(CARD_STEPS[v]), 0.22, 0.55, damp=0.35) * 0.32, 0.112)
    x = fft_filter(out, chain(lowpass(6000.0, 2), highpass(200.0, 2), peak(3200.0, -3.0, 1.2)))
    return _soft_start(wet(x, ROOM, 0.06))


def activity_record():
    """J-SFX-ACT-000-04: a new record: a four-note music box motif, F up to A, a step down to G, then the C left to ring
    over a soft tine fifth (F and C), its octave a whisper above. In the middle octave of a music box: higher, the
    glass sat in the 2 to 5 kHz band and pierced. Proud and light, never a fanfare. About 1 s."""
    rng = np.random.default_rng(1840)
    out = np.zeros(samples(1.5))
    for note, at, vel, seconds, tau, level in (('F5', 0.0, 0.5, 0.5, 0.16, 0.3), ('A5', 0.1, 0.55, 0.5, 0.16, 0.32),
                                              ('G5', 0.2, 0.48, 0.5, 0.15, 0.28), ('C6', 0.33, 0.6, 1.1, 0.45, 0.36)):
        place(out, _glass_note(hz(note), seconds, vel, tau) * level, at)
    place(out, _glass_note(hz('C7'), 0.8, 0.3, 0.25) * 0.05, 0.345)
    place(out, _tine_under(hz('F4'), 1.0, 0.5, 0.4, rng) * 0.2, 0.33)
    place(out, _tine_under(hz('C5'), 1.0, 0.45, 0.36, rng) * 0.15, 0.345)
    x = fft_filter(out, chain(lowpass(8000.0, 2), highpass(140.0, 2), peak(3000.0, -2.5, 1.0)))
    return _soft_start(wet(x, ROOM, 0.18))


def activity_pause(v):
    """J-SFX-ACT-000-05: pause (variant 1) and resume (variant 2): a soft wooden click, a fingertip on a small box of
    light wood. The pause sits a little lower (D), the resume a third higher (F). About 0.15 s."""
    rng = np.random.default_rng(1850 + v)
    out = np.zeros(samples(0.22))
    knock = _wood_knock(hz(('D5', 'F5')[v]), rng, seconds=0.18)
    place(out, fade(knock, 0.0015, 0.0) * 0.5, 0.0, release=0.03)
    x = fft_filter(out, chain(lowpass(3600.0, 2), highpass(200.0, 2)))
    return _soft_start(wet(x, ROOM, 0.04))


def activity_streak(v):
    """J-SFX-ACT-000-06: a streak climbs: the result star's tine, one semitone higher at each of the 8 steps (F up to C),
    lighter and shorter than the star so it can repeat during play. Past step 8 the presenter stays on the top. About
    0.3 s."""
    rng = np.random.default_rng(1860 + v)
    f = hz('F5') * 2.0 ** (v / 12.0)
    out = np.zeros(samples(0.55))
    place(out, _tine_under(f, 0.5, 0.65, 0.15, rng) * 0.55, 0.0)
    place(out, _glass_note(f * 2.0, 0.4, 0.34, 0.09) * 0.13 * (hz('F5') / f) ** 1.5, 0.003)
    x = fft_filter(out, chain(lowpass(7000.0, 2), highpass(180.0, 2)))
    return _soft_start(wet(x, ROOM, 0.08))


# ---------------------------------------------------------------- T38: grooming (doc 01 Contenu, S-ACT-001 "Sons et
# effets"; doc Audio, J-SFX-ACT-001-01 to 07). Soft, tactile, cosy: felt on fur, leaves, a small glass bead, the music
# box; F major. The giggle (-04) and the content sigh (-07) are the species voice banks (AnimalHappy at +10 %,
# AnimalSleep), played by the stage through IAudioService.PlayPitched.

CLEAN_STEPS = 8


def _rise_fall(n, peak_at, fall_power=1.4):
    """A stroke's envelope over n samples: a raised-cosine rise to peak_at (fraction of n), then a rounded fall to 0."""
    k = max(int(n * peak_at), 1)
    rise = 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
    fall = (0.5 + 0.5 * np.cos(np.pi * np.arange(n - k) / max(n - k, 1))) ** fall_power
    return np.concatenate([rise, fall])


def _moving_band(n, centre, rng, width=0.55, low=280.0, high=3200.0):
    """Noise whose band follows a centre curve (Hz per sample): half-octave bands from low to high, each weighted by its
    distance to the centre in octaves (width), so the colour of the swish glides with the stroke without a filter sweep."""
    bands = np.geomspace(low, high, 9)
    out = np.zeros(n)
    for c in bands:
        weight = np.exp(-0.5 * (np.log2(c / np.maximum(centre, 1.0)) / width) ** 2)
        out += band_noise(n, c / 1.25, c * 1.25, rng) * weight
    return out / 3.0


def _flutter(n, rate_hz, rng):
    """A slow random wobble around 1 (strands of fur passing under the bristles): white noise smoothed below rate_hz,
    unit deviation. Smooth on purpose: a texture, never a crackle."""
    w = fft_filter(rng.standard_normal(n), lowpass(rate_hz, 2))
    return w / (np.std(w) + 1e-12)


def groom_brush(v):
    """J-SFX-ACT-001-01: one pass of a soft brush on the coat, played per pass while the finger slides. A felted whoosh
    (noise whose colour glides with the stroke: up, down, or a short arc for the three variants), the strands of fur
    under the bristles (a slow smooth wobble of that noise, never a crackle) and the soft body of the felt pad pressing on
    the coat. Under 3.2 kHz: cosy, tactile, never scratchy. About 0.3 s."""
    rng = np.random.default_rng(1900 + v)
    seconds, peak_at = ((0.3, 0.42), (0.33, 0.36), (0.26, 0.5))[v]
    n = samples(seconds)
    t = np.arange(n) / SR
    u = t / seconds
    if v == 0:
        centre = 620.0 * 2.0 ** (1.1 * u)
    elif v == 1:
        centre = 1250.0 * 2.0 ** (-1.0 * u)
    else:
        centre = 640.0 * 2.0 ** (0.95 * np.sin(np.pi * u))
    env = _rise_fall(n, peak_at)
    swish = _moving_band(n, centre, rng) * (1.0 + 0.32 * _flutter(n, 22.0, rng)) * env
    body = band_noise(n, 200.0, 650.0, rng) * _rise_fall(n, peak_at * 0.8, 2.0) * 0.32
    air = band_noise(n, 2200.0, 4200.0, rng) * env ** 2 * 0.035
    x = swish + body + air
    x = fft_filter(x, chain(lowpass(3200.0, 2), highpass(170.0, 2), peak(2600.0, -3.0, 1.2)))
    return _soft_start(wet(x, ROOM, 0.04), 0.003)


def _pop(note, start=0.62, seconds=0.12, tau=0.028, bright=0.18):
    """A round plucky pop: a sine that bends up into its note within 12 ms (a soft 'pwip'), a touch of its octave. No
    click: 1.5 ms rounded attack."""
    t = t_axis(seconds)
    ph = phase_of(glide(t, note * start, note, 0.012))
    return np.sin(ph) * env_ad(t, 0.0015, tau) + bright * np.sin(2.0 * ph) * env_ad(t, 0.0015, tau * 0.45)


def _leaf_flight(seconds, spin_hz, rng, lo=450.0, hi=1700.0):
    """A small leaf flying off spinning: soft puffs of air at the spin rate, fading as it goes."""
    t = t_axis(seconds)
    spin = (0.5 + 0.5 * np.sin(2.0 * np.pi * spin_hz * t - np.pi / 2.0)) ** 2
    return band_noise(len(t), lo, hi, rng) * spin * env_ad(t, 0.03, seconds * 0.3)


def _tug(rng, grains=6, span=0.024, lo=900.0, hi=2800.0):
    """The hooks of a burr letting go of the fur: a few tiny soft grains (2 ms each), like a whisper of felt tape."""
    out = np.zeros(samples(span + 0.004))
    g = samples(0.002)
    window = np.hanning(g)
    for i in range(grains):
        at = int(samples(span) * (i + rng.uniform(0.1, 0.9)) / grains)
        out[at:at + g] += window * rng.uniform(0.5, 1.0) * rng.standard_normal(g)
    return fft_filter(out, bandpass(lo, hi))


def groom_debris(v):
    """J-SFX-ACT-001-02: a leaf picked off the coat with one tap: the soft snap of its stem, a round plucky pop that bends
    up into F (A for the second variant, so a run of leaves does not repeat one sound), and the leaf flying away
    spinning (H-VFX-ACT-001-02). Soft and cute, never a crunch. About 0.3 s."""
    rng = np.random.default_rng(1910 + v)
    out = np.zeros(samples(0.36))
    t = t_axis(0.012)
    place(out, band_noise(len(t), 1400.0, 3800.0, rng) * env_ad(t, 0.0008, 0.0025) * 0.1, 0.0, release=0.004)
    place(out, _pop(hz(('F5', 'A5')[v])) * 0.5, 0.004, release=0.02)
    place(out, _leaf_flight(0.2, (15.0, 18.0)[v], rng) * 0.045, 0.045, release=0.03)
    x = fft_filter(out, chain(lowpass(5500.0, 2), highpass(220.0, 2)))
    return _soft_start(wet(x, ROOM, 0.06))


def groom_burr(v):
    """J-SFX-ACT-001-03: a burr. Variant 1, the burr taken off (its second tap): the double pop, the hooks letting go
    of the fur in a whisper, then two round pops a fourth apart, C then F, 70 ms apart, and a little flight as it leaves.
    Variant 2, its first tap (it loosens): one softer, lower pop on A with the start of the tug, muffled. About 0.35 s."""
    rng = np.random.default_rng(1920 + v)
    out = np.zeros(samples(0.42))
    if v == 0:
        place(out, _tug(rng) * 0.09, 0.0, release=0.004)
        place(out, _pop(hz('C5'), start=0.66, tau=0.03, bright=0.14) * 0.42, 0.02, release=0.02)
        place(out, _pop(hz('F5'), start=0.6, tau=0.034) * 0.5, 0.09, release=0.02)
        place(out, _leaf_flight(0.18, 13.0, rng, 550.0, 2000.0) * 0.04, 0.12, release=0.03)
        x = fft_filter(out, chain(lowpass(5500.0, 2), highpass(200.0, 2)))
    else:
        place(out, _tug(rng, grains=3, span=0.012) * 0.06, 0.0, release=0.004)
        place(out, _pop(hz('A4'), start=0.7, tau=0.026, bright=0.1) * 0.5, 0.008, release=0.02)
        x = fft_filter(out, chain(lowpass(2600.0, 2), highpass(200.0, 2)))
    return _soft_start(wet(x, ROOM, 0.05))


def _glass_bead(f, seconds, vel, tau, attack=0.003, clear=0.12):
    """A small glass bead touched: an almost pure glass tone (a light FM, rounded over 3 ms) with the clear partial of a
    small glass (x2.76, clear), gone in tau. Shorter than the music box note: one clean zone, not a reward chime."""
    t = t_axis(seconds)
    x = fm_bell(t, f, ratio=2.0, index=0.25 + 0.35 * vel, index_tau=0.02, amp_tau=tau, attack=attack)
    x += clear * vel * np.sin(2.0 * np.pi * f * 2.76 * t) * np.exp(-t / (tau * 0.4)) * np.clip(t / attack, 0.0, 1.0)
    return x * vel


def groom_clean(v):
    """J-SFX-ACT-001-05: a zone is clean: a small glass bead, one semitone higher at each zone cleaned in a row (8 steps,
    F up to C: the climb ends on the fifth, stable), its octave whispered above. Glass where the activity streak is a
    kalimba tine (grooming plays this one instead). Very short and soft (-24, the star is -19) so a run of zones never
    tires and the result star stays the big sound. Past step 8 the stage stays on the top. About 0.15 s."""
    f = hz('F5') * 2.0 ** (v / 12.0)
    out = np.zeros(samples(0.36))
    place(out, _glass_bead(f, 0.3, 0.6, 0.06) * 0.42, 0.0)
    place(out, _glass_bead(f * 2.0, 0.2, 0.3, 0.03, clear=0.0) * 0.06, 0.001)
    x = fft_filter(out, chain(lowpass(6500.0, 2), highpass(300.0, 2)))
    return _soft_start(wet(x, ROOM, 0.07))


def groom_bow(v):
    """J-SFX-ACT-001-06: the bow is placed on the head: the ribbon settles (a tiny soft swish), then two music box notes,
    C then F above it (the fourth that comes home), the F left to ring over a soft tine on the F below. About 0.8 s,
    just before the end chord rolls down from the same C."""
    rng = np.random.default_rng(1930)
    out = np.zeros(samples(1.2))
    n = samples(0.07)
    place(out, band_noise(n, 600.0, 2000.0, rng) * _rise_fall(n, 0.5) * 0.025, 0.0, release=0.01)
    place(out, _glass_note(hz('C6'), 0.45, 0.48, 0.13) * 0.3, 0.05)
    place(out, _glass_note(hz('F6'), 1.0, 0.52, 0.36) * 0.36, 0.16)
    place(out, _tine_under(hz('F5'), 0.9, 0.45, 0.3, rng) * 0.2, 0.16)
    x = fft_filter(out, chain(lowpass(7500.0, 2), highpass(160.0, 2), peak(3000.0, -2.5, 1.0)))
    return _soft_start(wet(x, ROOM, 0.14))


def bubble_puff(v):
    """J-SFX-FEEL-020: a soft puff of bubbles (the grooming's soap bubble bursting into foam; doc 05 also gives it to the
    change of look of a bond level): a breath of air and a small cluster of round bubbles popping, rising around F
    (four for the first variant, three lighter ones for the second). About 0.4 s."""
    rng = np.random.default_rng(1940 + v)
    out = np.zeros(samples(0.5))
    place(out, _puff(0.14, 350.0, 1500.0, 0.012, 0.045, rng) * 0.1, 0.0, release=0.02)
    pops = ((0.012, 330.0, 'F5', 0.36), (0.05, 380.0, 'A5', 0.3), (0.082, 350.0, 'G5', 0.26), (0.125, 420.0, 'C6', 0.3))
    if v == 1:
        pops = ((0.01, 380.0, 'A5', 0.32), (0.055, 400.0, 'C6', 0.28), (0.1, 460.0, 'F6', 0.22))
    for at, start, note, level in pops:
        place(out, _bubble(start, hz(note), rng) * level, at, release=0.02)
    x = fft_filter(out, chain(lowpass(6500.0, 2), highpass(200.0, 2)))
    return _soft_start(wet(x, ROOM, 0.08))


# ---------------------------------------------------------------- T39: hide and seek (doc 01 Contenu, S-ACT-002 "Sons
# et effets"; doc Audio, J-SFX-ACT-002-01 to 06). The meadow plays along: leaves and grass, a round pop, a wooden
# whistle, the music box; F major. The voice of the coucou (-01, "ip ?") is the species' happy bank a little higher
# (AnimalHappy at 1.12 through IAudioService.PlayPitched, see HideSeekAudio): the clip here is the pop under it.

def _leaf_grains(n, count, density, rng, lo=900.0, hi=3600.0):
    """Leaves brushing together: count tiny grains of noise (3 to 8 ms, rounded windows: never a click, never a crackle)
    placed where density (an envelope over n samples) is high, each in the band of a leaf of its own size (lo to hi)."""
    bands = [band_noise(n, c / 1.4, c * 1.4, rng) for c in np.geomspace(lo, hi, 3)]
    cdf = np.cumsum(density)
    cdf /= cdf[-1]
    out = np.zeros(n)
    for _ in range(count):
        g = samples(rng.uniform(0.003, 0.008))
        at = min(int(np.searchsorted(cdf, rng.uniform(0.02, 0.98))), n - g)
        band = bands[int(rng.integers(0, len(bands)))]
        out[at:at + g] += band[at:at + g] * np.hanning(g) * rng.uniform(0.35, 1.0)
    return out


def _resonance(fc, octaves):
    """A narrow band in log frequency (a resonance of glass or wood), as a magnitude response."""
    return lambda f: np.exp(-0.5 * (np.log2(np.maximum(f, 1.0) / fc) / octaves) ** 2)


def _flute(freq, rng, breath=0.08, vibrato=0.006, vibrato_from=0.12):
    """A soft wooden flute on a frequency curve (Hz per sample), like a recorder played gently: an almost pure tone (a
    little second and third harmonic), a slow vibrato that grows in after vibrato_from seconds, and the player's breath
    around the note. No amplitude envelope: the caller shapes it."""
    n = len(freq)
    t = np.arange(n) / SR
    depth = vibrato * np.clip((t - vibrato_from) / 0.15, 0.0, 1.0)
    f = freq * (1.0 + depth * np.sin(2.0 * np.pi * 5.2 * t))
    ph = phase_of(f)
    tone = np.sin(ph) + 0.16 * np.sin(2.0 * ph + 0.4) + 0.05 * np.sin(3.0 * ph + 1.1) + 0.015 * np.sin(4.0 * ph)
    centre = float(np.median(freq))
    air = band_noise(n, centre * 0.8, centre * 2.6, rng) * breath + band_noise(n, 1200.0, 4000.0, rng) * breath * 0.2
    return tone + air


def hideseek_coucou(v):
    """J-SFX-ACT-002-01, the pop under the voice: the animal pops out of its hiding place. A whisper of leaves as it
    pushes through, then a round pop that bends up into its note and keeps lifting a whole tone, like a question
    ("ip ?"): F rising to G, or G rising to A for the second variant, so the coucous of a game never repeat one sound.
    About 0.2 s; the voice itself is the species' (HideSeekAudio)."""
    rng = np.random.default_rng(1950 + v)
    out = np.zeros(samples(0.3))
    n = samples(0.045)
    place(out, band_noise(n, 900.0, 3000.0, rng) * _rise_fall(n, 0.6) * 0.035, 0.0, release=0.01)
    note = hz(('F5', 'G5')[v])
    t = t_axis(0.2)
    lift = np.clip((t - 0.03) / 0.08, 0.0, 1.0)
    f = note * (0.62 + 0.38 * (1.0 - np.exp(-t / 0.01))) * 2.0 ** (2.0 / 12.0 * lift * lift * (3.0 - 2.0 * lift))
    ph = phase_of(f)
    body = np.sin(ph) * env_ad(t, 0.0015, 0.05, hold=0.025) + 0.16 * np.sin(2.0 * ph) * env_ad(t, 0.0015, 0.022)
    place(out, body * 0.5, 0.022, release=0.03)
    x = fft_filter(out, chain(lowpass(6000.0, 2), highpass(220.0, 2)))
    return _soft_start(wet(x, ROOM, 0.06))


def hideseek_empty(v):
    """J-SFX-ACT-002-02: a touch on an empty spot: the bush gives a little shake (a short soft swish and its warm body),
    its leaves brush together (tiny rounded grains, never a crackle), then a few leaves fly off (H-VFX-ACT-002-02).
    Kind and light: nothing says "wrong", no buzzer, no falling note. Three shakes, so quick taps never repeat one
    sound. About 0.35 s."""
    rng = np.random.default_rng(1960 + v)
    seconds, peak_at, grains, spin = ((0.3, 0.3, 38, 14.0), (0.34, 0.4, 44, 17.0), (0.27, 0.25, 32, 12.0))[v]
    n = samples(seconds)
    env = _rise_fall(n, peak_at, 1.6)
    swish = band_noise(n, 600.0, 2200.0, rng) * env * (1.0 + 0.25 * _flutter(n, 30.0, rng))
    body = band_noise(n, 220.0, 700.0, rng) * _rise_fall(n, peak_at * 0.8, 2.0)
    leaves = _leaf_grains(n, grains, env, rng, 600.0, 2200.0)
    out = np.zeros(samples(0.5))
    place(out, swish * 0.22 + body * 0.4 + leaves * 0.7, 0.0, release=0.03)
    place(out, _leaf_flight(0.2, spin, rng, 500.0, 1800.0) * 0.06, seconds * 0.55, release=0.03)
    x = fft_filter(out, chain(lowpass(3400.0, 3), highpass(200.0, 2), peak(3000.0, -3.0, 1.2)))
    return _soft_start(wet(x, ROOM, 0.05), 0.003)


def hideseek_hint(v):
    """J-SFX-ACT-002-03: a hidden animal gives its hint: the grass of its spot ripples (H-VFX-ACT-002-03). Two soft swells
    of grass running one after the other, smooth (blades of grass, not the crinkle of leaves of the empty touch), their
    colour lifting a little at the top of each swell so the ear turns to it; quieter than the empty touch. About 0.45 s."""
    rng = np.random.default_rng(1970 + v)
    seconds, second_at, second_level = ((0.46, 0.42, 0.8), (0.4, 0.36, 0.7))[v]
    n = samples(seconds)
    k = samples(seconds * second_at)
    first = np.zeros(n)
    first[:samples(seconds * 0.6)] = _rise_fall(samples(seconds * 0.6), 0.45, 1.4)
    second = np.zeros(n)
    second[k:] = _rise_fall(n - k, 0.45, 1.6) * second_level
    env = np.maximum(first, second)
    centre = 750.0 * 2.0 ** (1.0 * env)
    swish = _moving_band(n, centre, rng, width=0.5, low=400.0, high=3200.0) * env * (1.0 + 0.2 * _flutter(n, 18.0, rng))
    blades = _leaf_grains(n, 8, env, rng, 1200.0, 3000.0) * 0.1
    x = fft_filter(swish + blades, chain(lowpass(3800.0, 3), highpass(350.0, 2), peak(2800.0, -3.0, 1.2)))
    return _soft_start(wet(x, ROOM, 0.05), 0.003)


def hideseek_magnifier(v):
    """J-SFX-ACT-002-04: the magnifier: a soft breath across glass. A breath of air whose colour lifts as the lens comes up
    and settles back, the glass of the lens singing faintly inside it (the same breath through two narrow resonances, a
    C and the partial of a small glass above it, like a breath across the rim of a glass), then one small glass bead on
    C when the ring settles around the spot. About 0.6 s."""
    rng = np.random.default_rng(1980)
    out = np.zeros(samples(0.8))
    n = samples(0.42)
    env = _rise_fall(n, 0.45, 1.5)
    centre = 600.0 * 2.0 ** (1.5 * env)
    breath = _moving_band(n, centre, rng, width=0.6, low=350.0, high=3000.0) * env
    c6 = hz('C6')
    song = fft_filter(white(n, rng), lambda f: _resonance(c6, 0.012)(f) + 0.3 * _resonance(c6 * 2.76, 0.01)(f))
    song = song / (np.std(song) + 1e-12) * env ** 1.5
    place(out, breath * 0.5 + song * 0.2, 0.0, release=0.04)
    place(out, _glass_bead(c6, 0.4, 0.5, 0.11) * 0.18, 0.24)
    x = fft_filter(out, chain(lowpass(4500.0, 3), highpass(250.0, 2), peak(3000.0, -2.0, 1.0)))
    return _soft_start(wet(x, ROOM, 0.1), 0.003)


def hideseek_whistle(v):
    """J-SFX-ACT-002-05: the whistle calls the hidden animals: two soft notes of a wooden slide whistle, F then a slide up
    to the C above, held with a slow vibrato (a friendly "where are you?", rising like a question). Warm: an almost pure
    tone with a little breath, under 1.1 kHz, never shrill; a call, never a bird's alarm. About 0.6 s."""
    rng = np.random.default_rng(1990)
    seconds = 0.62
    n = samples(seconds)
    t = np.arange(n) / SR
    slide = np.clip((t - 0.15) / 0.05, 0.0, 1.0)
    freq = hz('F5') * 2.0 ** (7.0 / 12.0 * slide * slide * (3.0 - 2.0 * slide))
    voice = _flute(freq, rng, vibrato_from=0.26)
    amp = env_asr(n, 0.035, 0.14) * (1.0 - 0.55 * np.exp(-0.5 * ((t - 0.165) / 0.02) ** 2))
    chiff = band_noise(n, 1200.0, 3200.0, rng) * env_ad(t, 0.002, 0.012) * 0.1
    out = np.zeros(samples(0.8))
    place(out, voice * amp + chiff, 0.0, release=0.04)
    x = fft_filter(out, chain(lowpass(3600.0, 2), highpass(250.0, 2)))
    return _soft_start(wet(x, ROOM, 0.12))


def hideseek_all_found(v):
    """J-SFX-ACT-002-06: every animal is found: a happy three-note motif at the music box, C, D, then the F above (short,
    short, long, growing into the F), each over a warm tine an octave below, the F left to ring over a low F. It climbs
    from the C the end chord (J-SFX-ACT-000-02, played at the same moment) rolls down from, so the two meet in one F
    major, and its accent comes on the F, once the last coucou's pop and the chord's first notes have passed. Under the
    result stars (-20 against -19). About 0.9 s."""
    rng = np.random.default_rng(2000)
    out = np.zeros(samples(1.3))
    for note, at, vel, seconds, tau, level in (('C6', 0.0, 0.42, 0.45, 0.12, 0.2), ('D6', 0.1, 0.48, 0.45, 0.12, 0.26),
                                              ('F6', 0.2, 0.58, 1.0, 0.36, 0.38)):
        place(out, _glass_note(hz(note), seconds, vel, tau) * level, at)
        place(out, _tine_under(hz(note) / 2.0, seconds, 0.5, tau * 1.2, rng) * level * 0.45, at)
    place(out, _tine_under(hz('F4'), 0.9, 0.45, 0.32, rng) * 0.16, 0.2)
    x = fft_filter(out, chain(lowpass(7500.0, 2), highpass(150.0, 2), peak(3000.0, -2.5, 1.0)))
    return _soft_start(wet(x, ROOM, 0.14))


# ---------------------------------------------------------------- catalog

# name: (recipe, variants, loudness target (loudest 200 ms, K-weighted, LUFS scale), group)
SFX = {
    'SFX_Egg_Tap': (egg_tap, 2, -19.0, 'Rewards'),
    'SFX_Egg_Crack': (crack, 3, -21.0, 'Rewards'),
    'SFX_Hatch_Pop': (lambda v: hatch_pop(), 1, -16.0, 'Rewards'),
    'SFX_Reveal_Rare': (lambda v: reveal_rare(), 1, -15.0, 'Rewards'),
    'SFX_Reveal_Epic': (lambda v: reveal_epic(), 1, -15.0, 'Rewards'),
    'SFX_Reveal_Legendary': (lambda v: reveal_legendary(), 1, -15.0, 'Rewards'),
    'SFX_Reveal_Fabulous': (lambda v: reveal_fabulous(), 1, -15.0, 'Rewards'),
    'SFX_Collect_Light': (collect_light, 3, -21.0, 'Rewards'),
    'SFX_Reward_MasteryUp': (lambda v: mastery_up(), 1, -16.0, 'Rewards'),
    'SFX_UI_PurchaseSuccess': (lambda v: purchase_success(), 1, -16.0, 'UI'),
    'SFX_UI_Tap': (ui_tap, 3, -29.0, 'UI'),
    'SFX_UI_Confirm': (lambda v: ui_confirm(), 1, -22.0, 'UI'),
    'SFX_UI_Back': (lambda v: ui_back(), 1, -26.0, 'UI'),
    'SFX_UI_PanelOpen': (lambda v: panel_open(), 1, -25.0, 'UI'),
    'SFX_UI_ErrorSoft': (lambda v: error_soft(), 1, -23.0, 'UI'),
    'SFX_Animal_Rabbit_Happy': (rabbit_happy, 3, -21.0, 'Animals'),
    'SFX_Animal_Rabbit_Sleep': (rabbit_sleep, 2, -27.0, 'Animals'),
    'SFX_Animal_Small_Chirp': (small_chirp, 4, -21.0, 'Animals'),
    'SFX_Animal_Small_Sleep': (small_sleep, 2, -27.0, 'Animals'),
    # T34B2
    'SFX_UI_PanelClose': (lambda v: panel_close(), 1, -26.0, 'UI'),
    'SFX_UI_Tab': (ui_tab, 2, -29.0, 'UI'),
    'SFX_UI_ToggleOn': (lambda v: toggle_on(), 1, -23.0, 'UI'),
    'SFX_UI_ToggleOff': (lambda v: toggle_off(), 1, -25.0, 'UI'),
    'SFX_UI_PurchasePending': (lambda v: purchase_pending(), 1, -26.0, 'UI'),
    'SFX_Currency_SpendLight': (lambda v: spend_light(), 1, -26.0, 'Rewards'),
    'SFX_Reward_Claim': (lambda v: claim(), 1, -18.0, 'Rewards'),
    'SFX_Animal_Rabbit_Eat': (rabbit_eat, 2, -25.0, 'Animals'),
    'SFX_Animal_Small_Eat': (small_eat, 3, -25.0, 'Animals'),
    'SFX_World_BirdTakeOff': (bird_takeoff, 3, -26.0, 'World'),
    # T35A
    'SFX_Upgrade_Buy': (upgrade_buy, 3, -21.0, 'Rewards'),
    'SFX_Upgrade_Milestone': (lambda v: upgrade_milestone(), 1, -16.0, 'Rewards'),
    'SFX_Reward_ZoneOpen': (lambda v: zone_open(), 1, -16.0, 'Rewards'),
    'SFX_Reward_RefugeLevel': (lambda v: refuge_level_up(), 1, -16.0, 'Rewards'),
    'SFX_Reward_BiomeMastery': (lambda v: biome_mastery_up(), 1, -18.0, 'Rewards'),
    # T36 (doc 05 Sensations, section 6.1; doc 08 J-SFX-CURRENCY-004)
    'SFX_Reward_WishDone': (wish_done, 2, -18.0, 'Rewards'),
    'SFX_Reward_GiftOpen': (lambda v: gift_open(), 1, -17.0, 'Rewards'),
    'SFX_UI_PawStamp': (paw_stamp, 2, -21.0, 'UI'),
    'SFX_Currency_Gems': (lambda v: gems_collect(), 1, -19.0, 'Rewards'),
    # T37 activities (doc 01 Contenu, section 12.0; doc 05 Sensations, sheet 20 and section 6.1 for the star and card)
    'SFX_Activity_Start': (lambda v: activity_start(), 1, -20.0, 'UI'),
    'SFX_Activity_End': (lambda v: activity_end(), 1, -18.0, 'Rewards'),
    'SFX_Reward_Star': (result_star, 3, -19.0, 'Rewards'),
    'SFX_UI_CardFlip': (card_flip, len(CARD_STEPS), -23.0, 'UI'),
    'SFX_Activity_Record': (lambda v: activity_record(), 1, -17.0, 'Rewards'),
    'SFX_Activity_Pause': (activity_pause, 2, -25.0, 'UI'),
    'SFX_Activity_Streak': (activity_streak, STREAK_STEPS, -22.0, 'Rewards'),
    # T38 grooming (doc 01 Contenu, S-ACT-001 "Sons et effets"; doc 05 Sensations, section 6.1 for the bubble puff).
    # The burr's two variants are its two taps: the double pop when it comes off (1), the loosening tap (2), softer.
    'SFX_Groom_Brush': (groom_brush, 3, -25.0, 'UI'),
    'SFX_Groom_Debris': (groom_debris, 2, -20.0, 'Rewards'),
    'SFX_Groom_Burr': (groom_burr, 2, (-20.0, -22.5), 'Rewards'),
    'SFX_Groom_Clean': (groom_clean, CLEAN_STEPS, -24.0, 'Rewards'),
    'SFX_Groom_Bow': (groom_bow, 1, -19.0, 'Rewards'),
    'SFX_World_BubblePuff': (bubble_puff, 2, -20.0, 'World'),
    # T39 hide and seek (doc 01 Contenu, S-ACT-002 "Sons et effets"). The rustles sit low (the empty touch -25, the hint
    # under it at -27: it invites a look, never calls); the pop and the motif with the rewards, under the result star.
    'SFX_HideSeek_Coucou': (hideseek_coucou, 2, -21.0, 'Rewards'),
    'SFX_HideSeek_Empty': (hideseek_empty, 3, -25.0, 'World'),
    'SFX_HideSeek_Hint': (hideseek_hint, 2, -27.0, 'World'),
    'SFX_HideSeek_Magnifier': (hideseek_magnifier, 1, -23.0, 'UI'),
    'SFX_HideSeek_Whistle': (hideseek_whistle, 1, -21.0, 'UI'),
    'SFX_HideSeek_AllFound': (hideseek_all_found, 1, -20.0, 'Rewards'),
}
