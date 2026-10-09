"""
Pocket Wilds marketing edit (the T01 test video, then the daily series). Assembles what the capture recorded in the real
game (Assets/_Game/Editor/Marketing/MarketingCapture.cs: frames, the game's own audio, the shot and event log) with our
music and the English captions drawn in our own fonts, into a vertical H.264 video for TikTok and Instagram.

usage:
  python Tools/Marketing/make_video.py [--video T01_test] [--placeholder] [--no-contact]

Inputs, in Marketing/Videos/<video>/:
  edit.json         timing, captions, mix and encode settings (the storyboard in numbers)
  frames/%06d.png   the captured frames (1080 x 1920, 30 fps, or every Nth frame for a preview: see capture_log.json)
  game_audio.wav    the game's audio rendered offline in step with the frames (ambience, sounds, voices; no music)
  capture_log.json  shots (frame ranges, variants) and events (hatch pop, hide and seek tap...) for captions and ducking
--placeholder builds stand-in frames from the project's own Unity captures (Docs/Captures) and a stand-in game track from
our own sounds, so layout, fonts, timing and the mix are proven before a capture exists.

Output: the video (H.264 high, yuv420p bt709, 1080 x 1920, 30 fps, about 14 Mbps, AAC 192 kbps, about -14 LUFS
integrated, true peak under -1 dBTP) and a three-frame contact sheet, plus build/report.json with the measures.
Only our assets: fonts from SourceArt/Fonts/Build, music and sounds from Assets/_Game/Art/Audio.
"""
import argparse
import json
import math
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FFMPEG_DIR = Path(r'C:\Users\matte\AppData\Local\Microsoft\WinGet\Packages'
                  r'\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.1-full_build\bin')
FONTS = {
    'plush': ROOT / 'SourceArt/Fonts/Build/PocketPlush-Black.ttf',
    'round': ROOT / 'SourceArt/Fonts/Build/PocketRound-Bold.ttf',
}
PAW = '\ue000'
HEART = '\ue002'

# Stand-in shots for --placeholder: (name, first frame, end frame, image, (cx, cy, height) at start, at end).
PLACEHOLDER_SHOTS = [
    ('hatch', 0, 90, 'Docs/Captures/Golden/Capture_Golden_Refuge_High.png', (270, 560, 640), (270, 560, 420)),
    ('meadow', 90, 315, 'Docs/Captures/Review/T34E_game_High.png', (270, 430, 760), (270, 640, 700)),
    ('hide', 315, 495, 'Docs/Captures/Review/T39_props_hide_game.png', (170, 440, 560), (175, 450, 430)),
    ('cozy', 495, 630, 'Docs/Captures/Review/T34E_family_Evening.png', (985, 420, 700), (985, 430, 560)),
    ('end', 630, 780, 'Docs/Captures/Review/T34E_game_High.png', (270, 600, 900), (270, 585, 1170)),
]
PLACEHOLDER_EVENTS = [
    ('hatch_request', 3), ('hatch_crack1', 18), ('hatch_crack2', 33), ('hatch_pop', 47), ('hatch_heart', 58),
    ('hide_hint', 375), ('hide_tap', 402), ('hide_found', 403), ('cozy_treat', 531),
]
PLACEHOLDER_SOUNDS = [
    ('SFX/SFX_Egg_Tap_01.wav', 3, 0.0), ('SFX/SFX_Egg_Crack_01.wav', 18, 0.0), ('SFX/SFX_Egg_Crack_02.wav', 33, 0.0),
    ('SFX/SFX_Hatch_Pop_01.wav', 47, 0.0), ('SFX/SFX_Animal_Rabbit_Happy_01.wav', 51, -2.0),
    ('SFX/SFX_HideSeek_Coucou_01.wav', 403, 0.0), ('SFX/SFX_Animal_Rabbit_Happy_02.wav', 407, -3.0),
    ('SFX/SFX_Animal_Small_Eat_01.wav', 531, 0.0),
]


# ---------------------------------------------------------------- tools

def tool(name):
    custom = os.environ.get('PW_FFMPEG_DIR')
    for folder in ([Path(custom)] if custom else []) + [FFMPEG_DIR]:
        path = folder / (name + '.exe')
        if path.exists():
            return str(path)
    found = shutil.which(name)
    if found:
        return found
    sys.exit(f'{name} not found (install FFmpeg or set PW_FFMPEG_DIR)')


def run(cmd, what):
    result = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace')
    if result.returncode != 0:
        print(result.stderr[-6000:])
        sys.exit(f'ffmpeg failed: {what}')
    return result


def ffmpeg(*args, what='ffmpeg'):
    return run([tool('ffmpeg'), '-hide_banner', '-nostdin', '-y', *map(str, args)], what)


def loudness(path, af=None):
    """Integrated loudness (LUFS) and true peak (dBTP) of a file, from ebur128."""
    chain = (af + ',' if af else '') + 'ebur128=peak=true'
    out = run([tool('ffmpeg'), '-hide_banner', '-nostdin', '-i', str(path), '-af', chain, '-f', 'null', '-'], 'ebur128').stderr
    summary = out[out.rfind('Summary:'):]
    i = re.search(r'I:\s+(-?[\d.]+|-inf) LUFS', summary)
    peak = re.search(r'Peak:\s+(-?[\d.]+|-inf) dBFS', summary)
    as_float = lambda m: float('-inf') if not m or m.group(1) == '-inf' else float(m.group(1))
    return as_float(i), as_float(peak)


def nest(fn, terms):
    expr = terms[0]
    for term in terms[1:]:
        expr = f'{fn}({expr},{term})'
    return expr


# ---------------------------------------------------------------- captions

def color(value, alpha=255):
    value = value.lstrip('#')
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def draw_text(text, font, style, gradient=True):
    """Soft cream text (a light vertical gradient), a warm brown outline and a soft shadow, on a transparent sprite."""
    size = font.size
    stroke = max(2, round(size * style['outline']))
    blur = max(1.0, size * style['shadowBlur'])
    drop = round(size * style['shadowOffset'])
    x0, y0, x1, y1 = font.getbbox(text, stroke_width=stroke)
    pad = stroke + int(blur * 3) + drop + 4
    width, height = x1 - x0 + 2 * pad, y1 - y0 + 2 * pad
    origin = (pad - x0, pad - y0)

    outline = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(outline).text(origin, text, font=font, fill=color(style['outlineColor']), stroke_width=stroke,
                                 stroke_fill=color(style['outlineColor']))
    mask = Image.new('L', (width, height), 0)
    ImageDraw.Draw(mask).text(origin, text, font=font, fill=255)
    top, bottom = color(style['fillTop']), color(style['fillBottom'])
    fill = Image.new('RGBA', (width, height))
    if gradient:
        span = max(1, y1 - y0)
        column = Image.new('RGBA', (1, height))
        for y in range(height):
            k = min(1.0, max(0.0, (y - origin[1] - y0) / span))
            column.putpixel((0, y), tuple(round(top[c] + (bottom[c] - top[c]) * k) for c in range(4)))
        fill = column.resize((width, height))
    else:
        fill = Image.new('RGBA', (width, height), top)

    shadow = Image.new('RGBA', (width, height), color(style['shadowColor'], 0))
    shadow.putalpha(outline.getchannel('A').point(lambda v: int(v * style['shadowAlpha'])))
    shadow = shadow.filter(ImageFilter.GaussianBlur(blur))
    filled = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    filled.paste(fill, (0, 0), mask)
    composed = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    composed.alpha_composite(shadow, (0, drop))
    composed.alpha_composite(outline)
    composed.alpha_composite(filled)
    return composed


def trim(image, margin=2):
    """Crops a sprite to what it draws (shadow included), so accents and lines sit by what the eye sees."""
    box = image.getchannel('A').getbbox()
    if not box:
        return image
    return image.crop((max(0, box[0] - margin), max(0, box[1] - margin), min(image.width, box[2] + margin),
                       min(image.height, box[3] + margin)))


def caption_sprite(caption, styles, max_width):
    """The caption as one sprite: lines ("\\n") centered and stacked, shrunk only if a line is wider than the safe area."""
    style = dict(styles['default'])
    style.update(styles.get(caption.get('style', 'default'), {}))
    style.update({k: v for k, v in caption.items() if k in style})
    font_path = FONTS[caption.get('font', style.get('font', 'plush'))]
    size = int(caption.get('size', style.get('size', 100)))
    lines = caption['text'].split('\n')
    while True:
        font = ImageFont.truetype(str(font_path), size)
        stroke = max(2, round(size * style['outline']))
        widest = max(font.getbbox(line, stroke_width=stroke)[2] - font.getbbox(line, stroke_width=stroke)[0] for line in lines)
        if widest <= max_width or size <= 28:
            break
        size = int(size * 0.96)
    rows = [trim(draw_text(line, font, style)) for line in lines]
    gap = -int(size * 0.10)
    sprite = Image.new('RGBA', (max(r.width for r in rows), sum(r.height for r in rows) + gap * (len(rows) - 1)), (0, 0, 0, 0))
    y = 0
    for row in rows:
        sprite.alpha_composite(row, ((sprite.width - row.width) // 2, y))
        y += row.height + gap
    accent = caption.get('accent')
    if accent in ('paw', 'heart'):
        # A small pink paw (our font's own picto, U+E000) tucked on the title's top right, like a stamp.
        glyph = PAW if accent == 'paw' else HEART
        accent_style = dict(style)
        accent_style['fillTop'] = accent_style['fillBottom'] = style.get('accentColor', '#FFB7C9')
        mark = draw_text(glyph, ImageFont.truetype(str(FONTS['plush']), int(size * 0.72)), accent_style, gradient=False)
        mark = trim(mark.rotate(-16, resample=Image.BICUBIC, expand=True))
        side = int(mark.width * 0.5)
        lift = int(mark.height * 0.42)
        canvas = Image.new('RGBA', (sprite.width + 2 * side, sprite.height + lift), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, (side, lift))
        canvas.alpha_composite(mark, (canvas.width - mark.width - int(side * 0.15), 0))
        sprite = canvas
    return sprite, size


def ease_out_back(k, overshoot=1.2):
    k -= 1.0
    return 1.0 + (overshoot + 1.0) * k ** 3 + overshoot * k ** 2


def caption_state(t, duration, fade_out, pop, overshoot=1.2, start_scale=0.8):
    """Scale, alpha and vertical offset of a caption at its local time t: a pop in (overshoot), a soft fade out."""
    if t < pop:
        k = t / pop
        return (start_scale + (1.0 - start_scale) * ease_out_back(k, overshoot), min(1.0, t / (pop * 0.45)),
                (1.0 - (1.0 - (1.0 - k) ** 3)) * 18.0)
    if fade_out and t > duration - 0.24:
        k = max(0.0, (duration - t) / 0.24)
        return 0.94 + 0.06 * k, k, (1.0 - k) * -6.0
    return 1.0, 1.0, 0.0


def write_caption_frames(sprite, duration, fps, fade_out, pop, folder, overshoot=1.2, start_scale=0.8):
    folder.mkdir(parents=True, exist_ok=True)
    for old in folder.glob('*.png'):
        old.unlink()
    width, height = sprite.size
    canvas_w, canvas_h = math.ceil(width * 1.3) + 4, math.ceil(height * 1.3) + 44
    count = max(1, round(duration * fps))
    previous = None
    for i in range(count):
        scale, alpha, dy = caption_state(i / fps, duration, fade_out, pop, overshoot, start_scale)
        state = (round(scale, 4), round(alpha, 3), round(dy))
        path = folder / f'{i:04d}.png'
        if state == previous:
            shutil.copyfile(folder / f'{i - 1:04d}.png', path)
            continue
        previous = state
        frame = Image.new('RGBA', (canvas_w, canvas_h), (0, 0, 0, 0))
        if alpha > 0.0:
            w, h = max(1, round(width * scale)), max(1, round(height * scale))
            image = sprite if (w, h) == (width, height) else sprite.resize((w, h), Image.LANCZOS)
            if alpha < 1.0:
                image = image.copy()
                image.putalpha(image.getchannel('A').point(lambda v: int(v * alpha)))
            frame.alpha_composite(image, ((canvas_w - w) // 2, (canvas_h - h) // 2 + round(dy)))
        frame.save(path, compress_level=1)
    return canvas_w, canvas_h, count


# ---------------------------------------------------------------- placeholder (before a capture exists)

def build_placeholder(edit, build, rebuild):
    fps = edit['fps']
    width, height = edit['width'], edit['height']
    frames = build / 'placeholder_frames'
    frames.mkdir(parents=True, exist_ok=True)
    total = PLACEHOLDER_SHOTS[-1][2]
    cached = not rebuild and len(list(frames.glob('*.png'))) == total
    if not cached:
        for old in frames.glob('*.png'):
            old.unlink()
    smooth = lambda u: u * u * (3 - 2 * u)
    for name, first, end, image_path, a, b in ([] if cached else PLACEHOLDER_SHOTS):
        image = Image.open(ROOT / image_path).convert('RGB')
        for f in range(first, end):
            u = smooth((f - first) / max(1, end - first - 1))
            cx, cy, h = (a[i] + (b[i] - a[i]) * u for i in range(3))
            h = min(h, image.height)
            w = h * width / height
            if w > image.width:
                w = image.width
                h = w * height / width
            left = min(max(cx - w / 2, 0), image.width - w)
            top = min(max(cy - h / 2, 0), image.height - h)
            crop = image.resize((width, height), Image.BICUBIC, box=(left, top, left + w, top + h))
            crop.save(frames / f'{f:06d}.png', compress_level=1)
        print(f'  placeholder {name}: frames {first}-{end - 1} from {image_path}')
    log = {
        'video': edit.get('title', ''), 'fps': fps, 'stride': 1, 'width': width, 'height': height, 'frames': total,
        'written': total, 'audioFile': 'placeholder_game.wav',
        'shots': [{'name': s[0], 'variant': {'hide': 'hide', 'cozy': 'treat'}.get(s[0], ''), 'start': s[1], 'end': s[2] - 1}
                  for s in PLACEHOLDER_SHOTS],
        'events': [{'name': n, 'frame': f, 'time': f / fps} for n, f in PLACEHOLDER_EVENTS],
        'warnings': ['placeholder frames and audio, not a capture'],
    }
    # A stand-in game track: the Prairie day ambience and the real sounds of the beats, at the event frames.
    duration = total / fps
    audio = ROOT / 'Assets/_Game/Art/Audio'
    inputs = ['-i', audio / 'Ambience/AMB_Grassland_Day.wav']
    chains = [f'[0:a]atrim=0:{duration},asetpts=PTS-STARTPTS,volume=-9dB[a0]']
    for i, (sound, frame, gain) in enumerate(PLACEHOLDER_SOUNDS, start=1):
        inputs += ['-i', audio / sound]
        delay = round(frame / fps * 1000)
        chains.append(f'[{i}:a]volume={gain}dB,adelay={delay}|{delay}[a{i}]')
    mix = ''.join(f'[a{i}]' for i in range(len(PLACEHOLDER_SOUNDS) + 1))
    chains.append(f'{mix}amix=inputs={len(PLACEHOLDER_SOUNDS) + 1}:normalize=0:duration=first[out]')
    wav = build / 'placeholder_game.wav'
    ffmpeg(*inputs, '-filter_complex', ';'.join(chains), '-map', '[out]', '-ar', 48000, '-ac', 2, '-c:a', 'pcm_f32le', '-t', duration,
           wav, what='placeholder audio')
    return frames, log, wav


# ---------------------------------------------------------------- audio

def build_audio(edit, log, game_wav, duration, build):
    fps = edit['fps']
    a = edit['audio']
    music = a['music']
    music_path = ROOT / music['file']
    sfx_events = [e['time'] for e in log.get('events', []) if e['name'] in a['duck']['events']]
    cuts = [s['start'] / fps for s in log.get('shots', []) if s['start'] > 0]
    balance = a.get('balance', {})
    report = {}

    # Relative balance: the music bed and the game track are each brought to a level before the master pass.
    music_trim = f"atrim=start={music['offset']}:duration={duration + 1.0},asetpts=PTS-STARTPTS"
    music_lufs, _ = loudness(music_path, music_trim)
    music_gain = balance.get('musicLufs', -20.0) - music_lufs if math.isfinite(music_lufs) else 0.0
    music_gain += music.get('gainDb', 0.0)
    has_game = game_wav is not None and Path(game_wav).exists()
    game_gain = 0.0
    if has_game:
        game_lufs, game_peak = loudness(game_wav)
        report['gameInputLufs'] = game_lufs
        report['gameInputPeak'] = game_peak
        if math.isfinite(game_lufs):
            game_gain = balance.get('gameLufs', -22.0) - game_lufs
            # The sounds' peaks stay under -1.5 dBFS before the master: the pop keeps its snap instead of being limited flat.
            if math.isfinite(game_peak):
                game_gain = min(game_gain, balance.get('gamePeak', -1.5) - game_peak)
        game_gain += a['game'].get('gainDb', 0.0)
    report['musicInputLufs'] = music_lufs
    report['musicGainDb'] = round(music_gain, 2)
    report['gameGainDb'] = round(game_gain, 2)

    # Ducking: the music dips under each key sound (hatch, coucou, treat), smooth attack and release.
    duck = a['duck']
    depth = 1.0 - 10 ** (duck['depthDb'] / 20.0)
    bumps = []
    for te in sfx_events:
        rise = f"clip((t-{te - duck['attack']:.3f})/{duck['attack']},0,1)"
        fall = f"clip(({te + duck['hold'] + duck['release']:.3f}-t)/{duck['release']},0,1)"
        bumps.append(f'{rise}*{fall}')
    duck_expr = f"1-{depth:.4f}*{nest('max', bumps)}" if bumps else '1'
    # The game track jumps at each cut: a 12 ms dip on both sides keeps it click free.
    half = a['game'].get('cutFadeMs', 12) / 1000.0
    dips = [f'clip(abs(t-{tc:.4f})/{half},0,1)' for tc in cuts]
    cut_expr = nest('min', dips) if dips else '1'

    inputs = ['-i', music_path]
    chains = [
        f"[0:a]{music_trim},aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,volume={music_gain:.2f}dB,"
        f"afade=t=in:st=0:d={music['fadeIn']},afade=t=out:st={music['fadeOutStart']}:d={music['fadeOutDuration']},"
        f"asetnsamples=n=256:p=0,volume='{duck_expr}':eval=frame[m]"
    ]
    if has_game:
        inputs += ['-i', game_wav]
        chains.append(
            f"[1:a]aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,volume={game_gain:.2f}dB,"
            f"asetnsamples=n=64:p=0,volume='{cut_expr}':eval=frame,apad,atrim=0:{duration}[g]")
        chains.append(f"[m][g]amix=inputs=2:normalize=0:duration=first,atrim=0:{duration}[mix]")
    else:
        chains.append(f'[m]atrim=0:{duration}[mix]')
    fade = a.get('masterFadeOut', 1.2)
    chains.append(f"[mix]afade=t=out:st={duration - fade:.3f}:d={fade}[out]")
    mix = build / 'mix.wav'
    ffmpeg(*inputs, '-filter_complex', ';'.join(chains), '-map', '[out]', '-c:a', 'pcm_f32le', '-ar', 48000, mix, what='audio mix')

    master = normalize(mix, a['loudness'], a['loudness']['truePeak'], build / 'master.wav')
    report['duckEvents'] = sfx_events
    report['cuts'] = cuts
    return master, mix, report


def normalize(mix, target, true_peak, master):
    """Two-pass loudnorm for social: about -14 LUFS integrated, true peak under <true_peak> dBTP."""
    first = run([tool('ffmpeg'), '-hide_banner', '-nostdin', '-i', str(mix), '-af',
                 f"loudnorm=I={target['integrated']}:TP={true_peak}:LRA={target['lra']}:print_format=json",
                 '-f', 'null', '-'], 'loudnorm measure').stderr
    measured = json.loads(first[first.rfind('{'):first.rfind('}') + 1])
    ffmpeg('-i', mix, '-af',
           f"loudnorm=I={target['integrated']}:TP={true_peak}:LRA={target['lra']}:"
           f"measured_I={measured['input_i']}:measured_TP={measured['input_tp']}:measured_LRA={measured['input_lra']}:"
           f"measured_thresh={measured['input_thresh']}:offset={measured['target_offset']}:linear=true",
           '-ar', 48000, '-c:a', 'pcm_f32le', master, what='loudnorm apply')
    return master


def replace_audio(edit, video, master, duration):
    """Re-encodes only the audio of a finished video (the picture is copied)."""
    temp = video.with_name(video.stem + '_audio.mp4')
    ffmpeg('-i', video, '-i', master, '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac',
           '-b:a', edit['audio'].get('aacBitrate', '192k'), '-ar', 48000, '-ac', 2, '-movflags', '+faststart',
           '-t', f'{duration:.3f}', temp, what='audio re-encode')
    temp.replace(video)


# ---------------------------------------------------------------- video

def resolve_text(caption, log):
    if 'text' in caption:
        return caption['text']
    shot = next((s for s in log.get('shots', []) if s['name'] == caption.get('shot')), None)
    variants = caption['textByVariant']
    if shot and shot.get('variant') in variants:
        return variants[shot['variant']]
    return variants.get('default', next(iter(variants.values())))


def build_video(edit, log, frames_dir, master_wav, duration, out_path, build):
    fps = edit['fps']
    width, height = edit['width'], edit['height']
    stride = max(1, int(log.get('stride', 1)))
    safe = edit['safeArea']
    # Captions stay out of TikTok's own UI: top 10 %, bottom 20 %, right 12 %; centered on the frame's middle.
    max_width = int(2 * (width * (1.0 - safe['right']) - width / 2))
    styles = edit['styles']
    inputs = ['-framerate', f'{fps}/{stride}', '-start_number', 0, '-i', frames_dir / '%06d.png']
    placed = []
    for index, caption in enumerate(edit['captions']):
        caption = dict(caption)
        caption['text'] = resolve_text(caption, log)
        start, end = caption['start'], min(caption['end'], duration)
        if end <= start:
            continue
        sprite, size = caption_sprite(caption, styles, max_width)
        folder = build / 'captions' / f'c{index:02d}'
        canvas_w, canvas_h, count = write_caption_frames(sprite, end - start, fps, caption.get('fadeOut', True),
                                                         caption.get('pop', 0.34), folder)
        cy = caption['y'] * height
        top, bottom = cy - sprite.height / 2, cy + sprite.height / 2
        if top < safe['top'] * height or bottom > (1.0 - safe['bottom']) * height:
            print(f"  warning: caption '{caption['text']}' leaves the safe area ({top:.0f}-{bottom:.0f} px)")
        inputs += ['-framerate', fps, '-start_number', 0, '-i', folder / '%04d.png']
        placed.append({'text': caption['text'], 'start': start, 'end': end, 'cy': cy, 'size': size,
                       'width': sprite.width, 'height': sprite.height, 'input': len(placed) + 1})

    chains = []
    base = '[0:v]'
    if stride > 1:
        chains.append(f'{base}fps={fps}[v0]')
        base = '[v0]'
    chains.append(f'{base}format=gbrp[vbase]')
    current = '[vbase]'
    end_card = edit.get('endCard')
    if end_card:
        e, f = end_card['start'], end_card['blurFade']
        dim = 1.0 - end_card.get('dim', 0.06)
        chains.append(f'{current}split=2[va][vb]')
        chains.append(f"[vb]gblur=sigma={end_card['blurSigma']},colorchannelmixer=rr={dim}:gg={dim}:bb={dim},format=gbrap,"
                      f"fade=t=in:st={e}:d={f}:alpha=1[vblur]")
        chains.append(f"[va][vblur]overlay=0:0:format=rgb:enable='gte(t,{e})'[vcard]")
        current = '[vcard]'
    for i, p in enumerate(placed):
        label = f'[vc{i}]'
        chains.append(f"[{p['input']}:v]format=rgba,setpts=PTS-STARTPTS+{p['start']}/TB[cap{i}]")
        chains.append(f"{current}[cap{i}]overlay=x='{width / 2:.1f}-w/2':y='{p['cy']:.1f}-h/2':format=rgb:eof_action=pass:"
                      f"repeatlast=0:enable='between(t,{p['start']},{p['end']})'{label}")
        current = label
    chains.append(f'{current}scale=out_color_matrix=bt709:out_range=tv,format=yuv420p[vout]')
    audio_index = len(placed) + 1
    inputs += ['-i', master_wav]
    v = edit['video']
    ffmpeg(*inputs, '-filter_complex', ';'.join(chains), '-map', '[vout]', '-map', f'{audio_index}:a',
           '-c:v', 'libx264', '-profile:v', v['profile'], '-level:v', v.get('level', '4.1'), '-preset', v['preset'],
           '-b:v', v['bitrate'], '-maxrate', v['maxrate'], '-bufsize', v['bufsize'], '-pix_fmt', 'yuv420p', '-r', fps,
           '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-color_range', 'tv',
           '-c:a', 'aac', '-b:a', edit['audio'].get('aacBitrate', '192k'), '-ar', 48000, '-ac', 2,
           '-movflags', '+faststart', '-t', f'{duration:.3f}', out_path, what='encode')
    return placed


def contact_sheet(edit, video, out_path, build):
    times = edit.get('contactFrames', [1.9, 13.6, 23.0])
    labels = edit.get('contactLabels', [''] * len(times))
    thumbs = []
    for i, t in enumerate(times):
        still = build / f'contact_{i}.png'
        ffmpeg('-ss', f'{t:.3f}', '-i', video, '-frames:v', 1, still, what='contact frame')
        thumbs.append(Image.open(still).convert('RGB').resize((360, 640), Image.LANCZOS))
    gap, label_h = 18, 46
    sheet = Image.new('RGB', (len(thumbs) * 360 + (len(thumbs) + 1) * gap, 640 + 2 * gap + label_h), (246, 239, 226))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(str(FONTS['round']), 24)
    for i, thumb in enumerate(thumbs):
        x = gap + i * (360 + gap)
        sheet.paste(thumb, (x, gap))
        text = f'{times[i]:.1f} s  {labels[i] if i < len(labels) else ""}'.strip()
        draw.text((x + 180, gap + 640 + label_h // 2 + 4), text, font=font, fill=(94, 58, 33), anchor='mm')
    sheet.save(out_path)


# ---------------------------------------------------------------- main

def main():
    parser = argparse.ArgumentParser(description='Pocket Wilds marketing edit')
    parser.add_argument('--video', default='T01_test')
    parser.add_argument('--placeholder', action='store_true', help='stand-in frames and audio from the project captures')
    parser.add_argument('--rebuild-placeholder', action='store_true', help='redraw the stand-in frames even if cached')
    parser.add_argument('--no-contact', action='store_true')
    args = parser.parse_args()

    video_dir = ROOT / 'Marketing' / 'Videos' / args.video
    edit = json.loads((video_dir / 'edit.json').read_text(encoding='utf-8'))
    build = video_dir / 'build'
    build.mkdir(parents=True, exist_ok=True)
    fps = edit['fps']

    if args.placeholder:
        print('Building placeholder frames and audio...')
        frames_dir, log, game_wav = build_placeholder(edit, build, args.rebuild_placeholder)
        out_name = Path(edit['output']).stem + '_placeholder.mp4'
        contact_name = Path(edit['contactSheet']).stem + '_placeholder.png'
    else:
        frames_dir = video_dir / 'frames'
        log_path = video_dir / 'capture_log.json'
        if not log_path.exists() or not any(frames_dir.glob('*.png')):
            sys.exit(f'No capture in {video_dir} (run Pocket Wilds/Marketing/Capture Test Video in Unity, or use --placeholder)')
        log = json.loads(log_path.read_text(encoding='utf-8'))
        game_wav = video_dir / log['audioFile'] if log.get('audioFile') else None
        out_name, contact_name = edit['output'], edit['contactSheet']
        for warning in log.get('warnings', []):
            print('  capture warning:', warning)

    stride = max(1, int(log.get('stride', 1)))
    written = len(list(frames_dir.glob('*.png')))
    duration = min(edit['duration'], written * stride / fps)
    print(f'Edit {args.video}: {written} frames (stride {stride}), {duration:.2f} s')

    print('Mixing audio...')
    master, mix, report = build_audio(edit, log, game_wav, duration, build)
    print('Rendering captions and encoding...')
    out_path = video_dir / out_name
    placed = build_video(edit, log, frames_dir, master, duration, out_path, build)

    # AAC adds a little overshoot on sharp sounds (the hatch pop): measured on the file, corrected on the master.
    lufs, peak = loudness(out_path)
    true_peak = edit['audio']['loudness']['truePeak']
    for attempt in range(3):
        if peak <= -1.1:
            break
        true_peak -= peak + 1.35
        print(f'  true peak {peak:.1f} dBTP after AAC: loudnorm again at {true_peak:.1f} dBTP')
        normalize(mix, edit['audio']['loudness'], round(true_peak, 2), master)
        replace_audio(edit, out_path, master, duration)
        lufs, peak = loudness(out_path)
    report['masterTruePeakTarget'] = round(true_peak, 2)
    probe = run([tool('ffprobe'), '-v', 'error', '-show_entries', 'format=duration,bit_rate:stream=codec_name,profile,width,height,'
                 'pix_fmt,r_frame_rate,bit_rate,sample_rate', '-of', 'json', str(out_path)], 'ffprobe').stdout
    report.update({
        'output': str(out_path.relative_to(ROOT)), 'durationSeconds': round(duration, 3),
        'sizeMB': round(out_path.stat().st_size / 1e6, 2), 'integratedLufs': lufs, 'truePeakDbtp': peak,
        'captions': placed, 'probe': json.loads(probe),
    })
    if not args.no_contact:
        contact = video_dir / contact_name
        contact_sheet(edit, out_path, contact, build)
        report['contactSheet'] = str(contact.relative_to(ROOT))
    (build / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f"Done: {report['output']} ({report['sizeMB']} MB, {duration:.2f} s), {lufs:.1f} LUFS, true peak {peak:.1f} dBTP")
    if 'contactSheet' in report:
        print('Contact sheet:', report['contactSheet'])
    if peak > -1.0:
        print('  warning: true peak above -1 dBTP')


if __name__ == '__main__':
    main()
