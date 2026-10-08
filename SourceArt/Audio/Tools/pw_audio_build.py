"""
Pocket Wilds audio build (T34B).

    python pw_audio_build.py sfx|ambience|music|all     render masters (48 kHz 24-bit) and runtime clips
    python pw_audio_build.py sfx|music <names...>       render only these catalog entries (T37)
    python pw_audio_build.py review <out.png> [names]   waveform and spectrogram sheet of the runtime clips
    python pw_audio_build.py report                     levels, peaks, lengths, loop seams (JSON on stdout)

Masters: SourceArt/Audio/Masters/. Runtime: Assets/_Game/Art/Audio/ (48 kHz 16-bit, silent tails trimmed; Unity
compresses them on import). Everything is deterministic: same script, same files.
"""
import json
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from pw_audio_dsp import (SR, integrated_lufs, momentary_lufs_max, normalize_integrated, normalize_momentary, peak_db, read_wav, seam_jump, trim_tail,  # noqa: E402
                          true_peak_db, write_wav)

ROOT = HERE.parents[2]
MASTERS = ROOT / 'SourceArt' / 'Audio' / 'Masters'
RUNTIME = ROOT / 'Assets' / '_Game' / 'Art' / 'Audio'

# Beds sit clearly under the feedback (doc 08): effects peak around -15 (loudest 200 ms), the theme at -20 LUFS
# integrated and the ambience at -27, before the mixer groups.
MUSIC_LUFS = -20.0
AMBIENCE_LUFS = -27.0


def render_sfx(names=None):
    from pw_audio_sfx import SFX
    out = {}
    for name, (recipe, variants, target, group) in SFX.items():
        if names and name not in names:
            continue
        for v in range(variants):
            x = recipe(v)
            x = trim_tail(x, threshold_db=-54.0)
            # T38: a tuple gives each variant its own level (the burr's loosening tap sits under its double pop).
            level = target[v] if isinstance(target, (tuple, list)) else target
            x = normalize_momentary(x, level, ceiling_db=-1.0)
            file = f'{name}_{v + 1:02d}.wav'
            write_wav(MASTERS / 'SFX' / file, x, 24)
            write_wav(RUNTIME / 'SFX' / file, x, 16)
            out[file] = round(momentary_lufs_max(x), 1)
    return out


def render_ambience():
    from pw_audio_ambience import grassland_day, grassland_evening, rain_light
    out = {}
    # Day under the feedback; evening a little quieter still; rain as loud as the day bed it replaces.
    for name, recipe, target in (('AMB_Grassland_Day', grassland_day, AMBIENCE_LUFS),
                                 ('AMB_Grassland_Evening', grassland_evening, AMBIENCE_LUFS - 2.0),
                                 ('AMB_Rain_Light', rain_light, AMBIENCE_LUFS)):
        x = normalize_integrated(recipe(), target)
        write_wav(MASTERS / 'Ambience' / (name + '.wav'), x, 24)
        write_wav(RUNTIME / 'Ambience' / (name + '.wav'), x, 16)
        out[name + '.wav'] = {'seconds': len(x) / SR, 'lufs': round(integrated_lufs(x), 1), 'peakDb': round(peak_db(x), 1),
                              'seam': round(seam_jump(x), 3)}
    return out


def render_music(names=None):
    """The refuge theme and, from T37, the three activity musics (pw_audio_music.MUSIC), all at the same level so the
    cross-fade between the refuge and an activity keeps its loudness."""
    from pw_audio_music import MUSIC
    out = {}
    for name, recipe in MUSIC.items():
        if names and name not in names:
            continue
        x = normalize_integrated(recipe(), MUSIC_LUFS)
        write_wav(MASTERS / 'Music' / (name + '.wav'), x, 24)
        write_wav(RUNTIME / 'Music' / (name + '.wav'), x, 16)
        out[name + '.wav'] = {'seconds': len(x) / SR, 'lufs': round(integrated_lufs(x), 1), 'peakDb': round(peak_db(x), 1),
                              'seam': round(seam_jump(x), 3)}
    return out


def report():
    rows = {}
    for path in sorted(RUNTIME.rglob('*.wav')):
        x, rate = read_wav(path)
        row = {
            'seconds': round(len(x) / rate, 3),
            'channels': 1 if x.ndim == 1 else x.shape[1],
            'peakDb': round(peak_db(x), 2),
            'truePeakDb': round(true_peak_db(x), 2) if len(x) < SR * 20 else None,
        }
        if len(x) < SR * 5:
            row['momentaryLufs'] = round(momentary_lufs_max(x), 1)
        else:
            row['seamJump'] = round(seam_jump(x), 3)
        rows[str(path.relative_to(RUNTIME)).replace('\\', '/')] = row
    return rows


def review(out_png, names=None):
    from PIL import Image, ImageDraw
    files = sorted(RUNTIME.rglob('*.wav'))
    if names:
        files = [f for f in files if any(n in f.name for n in names)]
    width, wave_h, spec_h, label_h = 900, 70, 140, 18
    rows = []
    for path in files:
        x, rate = read_wav(path)
        mono = x if x.ndim == 1 else x.mean(axis=1)
        seconds = len(mono) / rate
        img = Image.new('RGB', (width, label_h + wave_h + spec_h), (24, 24, 30))
        d = ImageDraw.Draw(img)
        d.text((4, 2), f'{path.name}  {seconds:.2f} s  peak {peak_db(mono):.1f} dB', fill=(230, 230, 230))
        # Waveform (min/max per column).
        cols = np.array_split(mono, width)
        for c, seg in enumerate(cols):
            if len(seg) == 0:
                continue
            lo, hi = float(seg.min()), float(seg.max())
            y0 = label_h + wave_h / 2 - hi * wave_h / 2
            y1 = label_h + wave_h / 2 - lo * wave_h / 2
            d.line([(c, y0), (c, y1)], fill=(120, 200, 255))
        # Spectrogram: log frequency 60 Hz to 16 kHz, 72 dB range.
        nfft = 2048
        hop = max(1, (len(mono) - nfft) // width) if len(mono) > nfft else 1
        window = np.hanning(nfft)
        freqs = np.fft.rfftfreq(nfft, 1.0 / rate)
        bins = np.geomspace(60.0, 16000.0, spec_h + 1)
        spec = np.zeros((spec_h, width))
        for c in range(width):
            start = c * hop
            frame = mono[start:start + nfft]
            if len(frame) < nfft:
                frame = np.concatenate([frame, np.zeros(nfft - len(frame))])
            mag = np.abs(np.fft.rfft(frame * window))
            for r in range(spec_h):
                sel = (freqs >= bins[r]) & (freqs < bins[r + 1])
                spec[spec_h - 1 - r, c] = mag[sel].max() if sel.any() else mag[np.argmin(np.abs(freqs - bins[r]))]
        spec_db = 20 * np.log10(spec + 1e-9)
        spec_db = np.clip((spec_db - (spec_db.max() - 72.0)) / 72.0, 0, 1)
        rgb = np.stack([spec_db ** 0.8 * 255, spec_db ** 1.6 * 200, (1 - spec_db) * 60 + spec_db * 120], axis=2)
        img.paste(Image.fromarray(rgb.astype(np.uint8)), (0, label_h + wave_h))
        for f_mark in (100, 1000, 2000, 5000, 10000):
            y = label_h + wave_h + spec_h - 1 - int(np.searchsorted(bins, f_mark))
            d.text((width - 40, y - 6), f'{f_mark // 1000}k' if f_mark >= 1000 else '100', fill=(200, 200, 200))
        rows.append(img)
    sheet = Image.new('RGB', (width, sum(r.height + 4 for r in rows)), (10, 10, 12))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height + 4
    sheet.save(out_png)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'all'
    only = sys.argv[2:] if cmd in ('sfx', 'music') else None
    for folder in ('SFX', 'Ambience', 'Music'):
        (MASTERS / folder).mkdir(parents=True, exist_ok=True)
        (RUNTIME / folder).mkdir(parents=True, exist_ok=True)
    if cmd in ('sfx', 'all'):
        print(json.dumps(render_sfx(only), indent=1))
    if cmd in ('ambience', 'all'):
        print(json.dumps(render_ambience(), indent=1))
    if cmd in ('music', 'all'):
        print(json.dumps(render_music(only), indent=1))
    if cmd == 'report':
        print(json.dumps(report(), indent=1))
    if cmd == 'review':
        review(sys.argv[2], sys.argv[3:] or None)
