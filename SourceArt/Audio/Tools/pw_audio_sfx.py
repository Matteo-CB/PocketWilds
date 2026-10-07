"""
Pocket Wilds sound effects (T34B), synthesized from scratch. Every cue of the hatch family shares the key of the
refuge theme (F major) and the same instruments (tine, wooden mallet, glass bell), so tiers sound related.

Each recipe returns a mono float array at 48 kHz; pw_audio_build normalizes it to its category level.
"""
import numpy as np

from pw_audio_dsp import (SR, attack_curve, band_noise, bandpass, chain, convolve, damped_sine, db, env_ad, env_asr,
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
}
