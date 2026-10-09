"""
Pocket Wilds marketing edit, timeline mode (T02 and the daily series): many short real moments cut on the beat.

usage:
  python Tools/Marketing/make_cut.py [--video T02] [--cut main|short|all] [--no-contact]

Marketing/Videos/<video>/edit.json describes:
  sources   captures to cut from (frames, the game's own audio and the shot log of each Unity capture run)
  music     our music file and its tempo (the cuts land on its beats)
  cuts      one entry per output video: the music's start bar, the clips (shot, length in beats, an event placed on a
            beat, a digital camera move: slow push, drift, punch-in, micro-zoom pop on the cut), captions (in beats), the
            end card (blur under the title) and the output file
Each clip shows real frames of a shot, in their own time; the game's audio is cut with them (short fades at each cut).
The music starts on a bar, the key sounds duck it (deeper for the quiet "close" moments), the master is brought to about
-14 LUFS with its true peak under -1 dBTP after AAC. Captions use make_video.py's renderer (our fonts, bouncy pop).
"""
import argparse
import json
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_video as mv  # noqa: E402

ROOT = mv.ROOT
SAMPLE_RATE = 48000


# ---------------------------------------------------------------- sources

def read_wav(path):
    """A float32 or int16 WAV as a (frames, channels) float32 array."""
    raw = Path(path).read_bytes()
    pos, fmt, channels, bits, data = 12, 1, 2, 16, None
    while pos + 8 <= len(raw):
        tag, size = raw[pos:pos + 4], int.from_bytes(raw[pos + 4:pos + 8], 'little')
        body = raw[pos + 8:pos + 8 + size]
        if tag == b'fmt ':
            fmt = int.from_bytes(body[0:2], 'little')
            channels = int.from_bytes(body[2:4], 'little')
            bits = int.from_bytes(body[14:16], 'little')
        elif tag == b'data':
            data = body
            break
        pos += 8 + size + (size & 1)
    if fmt == 3:
        samples = np.frombuffer(data, dtype='<f4')
    else:
        samples = np.frombuffer(data, dtype='<i2').astype(np.float32) / 32768.0
    return samples[: len(samples) // channels * channels].reshape(-1, channels)


def write_wav(path, samples):
    data = np.ascontiguousarray(samples, dtype='<f4')
    channels = data.shape[1]
    header = b'RIFF' + (36 + data.nbytes).to_bytes(4, 'little') + b'WAVEfmt ' + (16).to_bytes(4, 'little')
    header += (3).to_bytes(2, 'little') + channels.to_bytes(2, 'little') + SAMPLE_RATE.to_bytes(4, 'little')
    header += (SAMPLE_RATE * channels * 4).to_bytes(4, 'little') + (channels * 4).to_bytes(2, 'little') + (32).to_bytes(2, 'little')
    header += b'data' + data.nbytes.to_bytes(4, 'little')
    Path(path).write_bytes(header + data.tobytes())


class Source:
    def __init__(self, video_dir, spec):
        base = ROOT / spec.get('dir', str(video_dir.relative_to(ROOT)))
        self.frames = base / spec.get('frames', 'frames')
        self.log = json.loads((base / spec.get('log', 'capture_log.json')).read_text(encoding='utf-8'))
        self.audio = read_wav(base / spec.get('audio', 'game_audio.wav'))
        self.shots = {}
        for shot in self.log['shots']:
            self.shots.setdefault(shot['name'], shot)
        self.events = self.log.get('events', [])

    def frame(self, index):
        return Image.open(self.frames / f'{index:06d}.png').convert('RGB')


# ---------------------------------------------------------------- the plan

def plan(edit, cut, sources):
    fps = edit['fps']
    beat = 60.0 / edit['music']['bpm']
    clips, at = [], 0.0
    for spec in cut['clips']:
        source = sources[spec.get('source', 'main')]
        shot = source.shots[spec['shot']]
        out_start, out_end = round(at * beat * fps), round((at + spec['beats']) * beat * fps)
        count = out_end - out_start
        if 'event' in spec:
            event = next(e for e in source.events if e['name'] == spec['event'] and shot['start'] <= e['frame'] <= shot['end'])
            src = event['frame'] - round(spec.get('eventBeat', 1.0) * beat * fps)
        else:
            src = shot['start'] + round(spec.get('in', 0.0) * fps)
        src = max(shot['start'], min(src, shot['end'] - count + 1))
        if src + count - 1 > shot['end']:
            print(f"  warning: {spec['shot']} is {shot['end'] - shot['start'] + 1} frames, the clip wants {count}: its last frame holds")
        clips.append(dict(spec, source=source, src=src, last=shot['end'], start=out_start, count=count, beat0=at))
        at += spec['beats']
    return clips, round(at * beat * fps)


def zoom_at(clip, t, fps):
    """The clip's digital camera at local time t: a slow push (or pull), a micro-zoom pop on the cut, a punch-in."""
    z0, z1 = clip.get('zoom', [1.0, 1.06])
    length = clip['count'] / fps
    k = min(1.0, t / max(length, 1e-3))
    ease = k * k * (3 - 2 * k)
    z = z0 + (z1 - z0) * (0.35 * k + 0.65 * ease)
    pop = clip.get('pop', 0.045)
    if pop:
        z *= 1.0 + pop * max(0.0, 1.0 - t / 0.3) ** 2
    punch = clip.get('punch')
    if punch:
        tp = punch['beat'] * 60.0 / clip['bpm']
        q = min(1.0, max(0.0, (t - tp) / punch.get('seconds', 0.14)))
        z *= 1.0 + (punch['to'] - 1.0) * (1 - (1 - q) ** 3)
    c0 = clip.get('center', [0.5, 0.5])
    c1 = clip.get('centerTo', c0)
    return z, (c0[0] + (c1[0] - c0[0]) * ease, c0[1] + (c1[1] - c0[1]) * ease)


def conform(image, zoom, center, width, height):
    if abs(zoom - 1.0) < 1e-4 and abs(center[0] - 0.5) < 1e-4 and abs(center[1] - 0.5) < 1e-4:
        return image
    inv = 1.0 / zoom
    # Keep the window inside the frame.
    cx = min(max(center[0] * width, width * inv / 2), width - width * inv / 2)
    cy = min(max(center[1] * height, height * inv / 2), height - height * inv / 2)
    return image.transform((width, height), Image.AFFINE, (inv, 0, cx - width * inv / 2, 0, inv, cy - height * inv / 2),
                           resample=Image.BICUBIC)


# ---------------------------------------------------------------- audio

def cut_audio(edit, clips, total, build, name):
    fps = edit['fps']
    per_frame = SAMPLE_RATE // fps
    out = np.zeros((total * per_frame, 2), dtype=np.float32)
    fade = int(SAMPLE_RATE * 0.008)
    ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)[:, None]
    events, asmr = [], []
    duck_names = set(edit['audio']['duck']['events'])
    for clip in clips:
        audio = clip['source'].audio
        a = clip['src'] * per_frame
        n = clip['count'] * per_frame
        piece = np.zeros((n, 2), dtype=np.float32)
        chunk = audio[a:a + n, :2]
        piece[:len(chunk), :chunk.shape[1]] = chunk
        piece *= clip.get('gain', 1.0)
        if n > 2 * fade:
            piece[:fade] *= ramp
            piece[-fade:] *= ramp[::-1]
        b = clip['start'] * per_frame
        out[b:b + n] += piece
        for e in clip['source'].events:
            if clip['src'] <= e['frame'] < clip['src'] + clip['count'] and e['name'] in duck_names:
                t = (clip['start'] + e['frame'] - clip['src']) / fps
                (asmr if clip.get('asmr') else events).append(round(t, 3))
    path = build / f'{name}_game.wav'
    write_wav(path, out)
    return path, events, asmr


def mix(edit, cut, game_wav, events, asmr, duration, build, name):
    a = edit['audio']
    music = edit['music']
    bar = 4 * 60.0 / music['bpm']
    offset = cut.get('musicBar', 0) * bar
    music_path = ROOT / music['file']
    balance = a.get('balance', {})
    trim = f"atrim=start={offset:.4f}:duration={duration + 1.0},asetpts=PTS-STARTPTS"
    music_lufs, _ = mv.loudness(music_path, trim)
    music_gain = balance.get('musicLufs', -19.0) - music_lufs + music.get('gainDb', 0.0)
    game_lufs, game_peak = mv.loudness(game_wav)
    game_gain = 0.0
    if math.isfinite(game_lufs):
        game_gain = min(balance.get('gameLufs', -21.0) - game_lufs, balance.get('gamePeak', -1.5) - game_peak)

    def bumps(times, d):
        terms = []
        for te in times:
            rise = f"clip((t-{te - d['attack']:.3f})/{d['attack']},0,1)"
            fall = f"clip(({te + d['hold'] + d['release']:.3f}-t)/{d['release']},0,1)"
            terms.append(f'{rise}*{fall}')
        return terms

    duck, quiet = a['duck'], a.get('asmr', a['duck'])
    depth = 1.0 - 10 ** (duck['depthDb'] / 20.0)
    deep = 1.0 - 10 ** (quiet['depthDb'] / 20.0)
    parts = []
    if events:
        parts.append(f"{depth:.4f}*{mv.nest('max', bumps(events, duck))}")
    if asmr:
        parts.append(f"{deep:.4f}*{mv.nest('max', bumps(asmr, quiet))}")
    duck_expr = f"1-{mv.nest('max', parts)}" if parts else '1'
    fade_out = cut.get('fadeOut', 1.6)
    chains = [
        f"[0:a]{trim},aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,volume={music_gain:.2f}dB,"
        f"afade=t=in:st=0:d=0.05,afade=t=out:st={duration - fade_out:.3f}:d={fade_out},"
        f"asetnsamples=n=256:p=0,volume='{duck_expr}':eval=frame[m]",
        f"[1:a]aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo,volume={game_gain:.2f}dB,apad,atrim=0:{duration}[g]",
        f"[m][g]amix=inputs=2:normalize=0:duration=first,atrim=0:{duration},afade=t=out:st={duration - 0.6:.3f}:d=0.6[out]",
    ]
    mixed = build / f'{name}_mix.wav'
    mv.ffmpeg('-i', music_path, '-i', game_wav, '-filter_complex', ';'.join(chains), '-map', '[out]', '-c:a', 'pcm_f32le',
              '-ar', 48000, mixed, what='audio mix')
    master = mv.normalize(mixed, a['loudness'], a['loudness']['truePeak'], build / f'{name}_master.wav')
    return master, mixed, {'musicGainDb': round(music_gain, 2), 'gameGainDb': round(game_gain, 2), 'duck': events, 'asmr': asmr,
                           'musicOffset': offset}


# ---------------------------------------------------------------- video

def render(edit, cut, clips, total, master, out_path, build, name):
    fps, width, height = edit['fps'], edit['width'], edit['height']
    beat = 60.0 / edit['music']['bpm']
    duration = total / fps
    safe = edit['safeArea']
    max_width = int(2 * (width * (1.0 - safe['right']) - width / 2))
    inputs = ['-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{width}x{height}', '-framerate', fps, '-i', '-']
    placed = []
    for index, caption in enumerate(cut.get('captions', [])):
        start = caption['startBeat'] * beat
        end = min(caption['endBeat'] * beat, duration)
        sprite, size = mv.caption_sprite(caption, edit['styles'], max_width)
        folder = build / f'{name}_captions' / f'c{index:02d}'
        mv.write_caption_frames(sprite, end - start, fps, caption.get('fadeOut', True), caption.get('pop', 0.3), folder,
                                overshoot=caption.get('overshoot', 2.4), start_scale=caption.get('startScale', 0.55))
        cy = caption['y'] * height
        inputs += ['-framerate', fps, '-start_number', 0, '-i', folder / '%04d.png']
        placed.append({'text': caption['text'], 'start': round(start, 3), 'end': round(end, 3), 'cy': cy, 'size': size,
                       'input': len(placed) + 1})
    chains = ['[0:v]format=gbrp[vbase]']
    current = '[vbase]'
    card = cut.get('endCard')
    if card:
        e = card['startBeat'] * beat
        dim = 1.0 - card.get('dim', 0.06)
        chains.append(f'{current}split=2[va][vb]')
        chains.append(f"[vb]gblur=sigma={card['blurSigma']},colorchannelmixer=rr={dim}:gg={dim}:bb={dim},format=gbrap,"
                      f"fade=t=in:st={e:.3f}:d={card['blurFade']}:alpha=1[vblur]")
        chains.append(f"[va][vblur]overlay=0:0:format=rgb:enable='gte(t,{e:.3f})'[vcard]")
        current = '[vcard]'
    for i, p in enumerate(placed):
        chains.append(f"[{p['input']}:v]format=rgba,setpts=PTS-STARTPTS+{p['start']}/TB[cap{i}]")
        chains.append(f"{current}[cap{i}]overlay=x='{width / 2:.1f}-w/2':y='{p['cy']:.1f}-h/2':format=rgb:eof_action=pass:"
                      f"repeatlast=0:enable='between(t,{p['start']},{p['end']})'[vc{i}]")
        current = f'[vc{i}]'
    chains.append(f'{current}scale=out_color_matrix=bt709:out_range=tv,format=yuv420p[vout]')
    inputs += ['-i', master]
    v = edit['video']
    cmd = [mv.tool('ffmpeg'), '-hide_banner', '-loglevel', 'error', '-nostats', '-y', *map(str, inputs), '-filter_complex', ';'.join(chains),
           '-map', '[vout]', '-map', f'{len(placed) + 1}:a', '-c:v', 'libx264', '-profile:v', v['profile'], '-level:v', v['level'],
           '-preset', v['preset'], '-b:v', v['bitrate'], '-maxrate', v['maxrate'], '-bufsize', v['bufsize'], '-pix_fmt', 'yuv420p',
           '-r', str(fps), '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-color_range', 'tv',
           '-c:a', 'aac', '-b:a', edit['audio'].get('aacBitrate', '192k'), '-ar', '48000', '-ac', '2', '-movflags', '+faststart',
           '-t', f'{duration:.3f}', str(out_path)]
    # ffmpeg's messages go to a file: a full stderr pipe would stop it reading the frames (and both would wait forever).
    log_path = build / f'{name}_ffmpeg.log'
    log_file = open(log_path, 'w', encoding='utf-8')
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stderr=log_file, stdout=subprocess.DEVNULL)
    for clip in clips:
        clip['bpm'] = edit['music']['bpm']
        for i in range(clip['count']):
            image = clip['source'].frame(min(clip['src'] + i, clip['last']))
            zoom, center = zoom_at(clip, i / fps, fps)
            proc.stdin.write(conform(image, zoom, center, width, height).tobytes())
    proc.stdin.close()
    code = proc.wait()
    log_file.close()
    if code != 0:
        print(log_path.read_text(encoding='utf-8', errors='replace')[-5000:])
        sys.exit('encode failed')
    return placed


def contact(edit, cut, video, out_path, build, name):
    times = cut.get('contactFrames', [1.0, 5.0, 10.0, 15.0, 20.0, 24.0])
    labels = cut.get('contactLabels', [''] * len(times))
    thumbs = []
    for i, t in enumerate(times):
        still = build / f'{name}_contact_{i}.png'
        mv.ffmpeg('-ss', f'{t:.3f}', '-i', video, '-frames:v', 1, still, what='contact frame')
        thumbs.append(Image.open(still).convert('RGB').resize((270, 480), Image.LANCZOS))
    gap, label_h = 14, 40
    sheet = Image.new('RGB', (len(thumbs) * 270 + (len(thumbs) + 1) * gap, 480 + 2 * gap + label_h), (246, 239, 226))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(str(mv.FONTS['round']), 20)
    for i, thumb in enumerate(thumbs):
        x = gap + i * (270 + gap)
        sheet.paste(thumb, (x, gap))
        draw.text((x + 135, gap + 480 + label_h // 2 + 3), f'{times[i]:.1f} s  {labels[i] if i < len(labels) else ""}'.strip(),
                  font=font, fill=(94, 58, 33), anchor='mm')
    sheet.save(out_path)


def main():
    parser = argparse.ArgumentParser(description='Pocket Wilds marketing edit, timeline mode')
    parser.add_argument('--video', default='T02')
    parser.add_argument('--cut', default='all')
    parser.add_argument('--no-contact', action='store_true')
    args = parser.parse_args()
    video_dir = ROOT / 'Marketing' / 'Videos' / args.video
    edit = json.loads((video_dir / 'edit.json').read_text(encoding='utf-8'))
    build = video_dir / 'build'
    build.mkdir(parents=True, exist_ok=True)
    sources = {key: Source(video_dir, spec) for key, spec in edit['sources'].items()}
    names = list(edit['cuts']) if args.cut == 'all' else [args.cut]
    report = {}
    for name in names:
        cut = edit['cuts'][name]
        clips, total = plan(edit, cut, sources)
        duration = total / edit['fps']
        print(f'{name}: {len(clips)} clips, {total} frames, {duration:.2f} s', flush=True)
        game_wav, events, asmr = cut_audio(edit, clips, total, build, name)
        master, mixed, audio = mix(edit, cut, game_wav, events, asmr, duration, build, name)
        out_path = video_dir / cut['output']
        placed = render(edit, cut, clips, total, master, out_path, build, name)
        lufs, peak = mv.loudness(out_path)
        true_peak = edit['audio']['loudness']['truePeak']
        for _ in range(3):
            if peak <= -1.1:
                break
            true_peak -= peak + 1.35
            mv.normalize(mixed, edit['audio']['loudness'], round(true_peak, 2), master)
            mv.replace_audio(edit, out_path, master, duration)
            lufs, peak = mv.loudness(out_path)
        entry = {'output': str(out_path.relative_to(ROOT)), 'seconds': round(duration, 3), 'lufs': lufs, 'truePeak': peak,
                 'sizeMB': round(out_path.stat().st_size / 1e6, 2), 'audio': audio, 'captions': placed,
                 'clips': [{k: c[k] for k in ('shot', 'src', 'start', 'count')} for c in clips]}
        if not args.no_contact and cut.get('contact'):
            contact(edit, cut, out_path, video_dir / cut['contact'], build, name)
            entry['contact'] = cut['contact']
        report[name] = entry
        print(f"  {entry['output']}: {duration:.2f} s, {lufs:.1f} LUFS, true peak {peak:.1f} dBTP, {entry['sizeMB']} MB")
    (build / 'report.json').write_text(json.dumps(report, indent=2, default=str), encoding='utf-8')


if __name__ == '__main__':
    main()
