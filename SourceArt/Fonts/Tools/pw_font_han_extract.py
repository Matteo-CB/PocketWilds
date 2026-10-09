"""Characters the game needs (FH): which Han characters the translated texts use, and which we still have to draw.

    python pw_font_han_extract.py                       # every LOC_*.asset of the project
    python pw_font_han_extract.py path/to/texts.txt ... # any text, csv, json or LOC asset files (region from the
                                                        # LOC languageCode, or --region JP|SC|TC for plain files)
    python pw_font_han_extract.py --drafts              # the translation drafts: Localization/Drafts/ja.json,
                                                        # zh-Hans.json, zh-Hant.json and charset_*.txt

For each region: the distinct Han characters used, how many times, and the ones missing from our data, most frequent
first. With --write, the missing lists go to SourceArt/Fonts/Tools/han_todo_<REGION>.txt, one character per line
with its count, ready to hand to a drawing batch. The language code of a LOC asset decides the region:
ja -> JP, zh-Hans / zh-CN / zh-SG / zh -> SC, zh-Hant / zh-TW / zh-HK / zh-MO -> TC.
"""
import collections
import glob
import os
import re
import sys

import pw_font_han as H

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LOC = os.path.join(ROOT, "Assets", "_Game", "Content", "Localization")

DRAFTS = os.path.join(ROOT, "Localization", "Drafts")
REGION_OF = {"ja": "JP", "ja-jp": "JP", "zh-hans": "SC", "zh-cn": "SC", "zh-sg": "SC", "zh": "SC",
             "zh-hant": "TC", "zh-tw": "TC", "zh-hk": "TC", "zh-mo": "TC"}


def is_han(c):
    o = ord(c)
    return 0x4E00 <= o <= 0x9FFF or 0x3400 <= o <= 0x4DBF or 0xF900 <= o <= 0xFAFF or 0x20000 <= o <= 0x2FA1F


def _unescape(s):
    def rep(m):
        t = m.group(0)
        if t[1] == "x":
            return chr(int(t[2:4], 16))
        if t[1] == "u":
            return chr(int(t[2:6], 16))
        if t[1] == "U":
            return chr(int(t[2:10], 16))
        return {"n": "\n", "t": "\t", '"': '"', "\\": "\\", "/": "/", " ": " "}.get(t[1], t[1])
    return re.sub(r'\\(x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|.)', rep, s)


def loc_values(path):
    """(language code, [values]) of a Unity LocalizationTableAsset (YAML scalars, quoted or plain)."""
    text = open(path, encoding="utf-8").read()
    m = re.search(r"^\s*languageCode:\s*(\S+)", text, re.M)
    lang = m.group(1).strip("'\"").lower() if m else ""
    values = []
    for block in re.split(r"\n\s*- key:", text)[1:]:
        m = re.search(r"\n\s*value:\s?(.*)", block, re.S)
        if not m:
            continue
        raw = m.group(1).rstrip()
        if raw.startswith('"'):
            body = raw[1:]
            end = re.search(r'(?<!\\)"', body)
            body = body[:end.start()] if end else body
            body = re.sub(r"\s*\n\s*", " ", body)
            values.append(_unescape(body))
        elif raw.startswith("'"):
            body = raw[1:]
            end = body.rfind("'")
            values.append(re.sub(r"\s*\n\s*", " ", body[:end]).replace("''", "'"))
        else:
            values.append(re.sub(r"\s*\n\s*", " ", raw))
    return lang, values


def file_region(path):
    """Region from a file name such as ja.json, charset_zh-Hant.txt or LOC_zh-Hans.asset."""
    stem = os.path.splitext(os.path.basename(path))[0].lower()
    for sep in ("charset_", "loc_"):
        if stem.startswith(sep):
            stem = stem[len(sep):]
    return REGION_OF.get(stem)


def file_values(path):
    """The strings of a text file: JSON values (any depth) or the whole text."""
    text = open(path, encoding="utf-8").read()
    if path.endswith(".json"):
        import json
        out = []

        def walk(v):
            if isinstance(v, str):
                out.append(v)
            elif isinstance(v, dict):
                for x in v.values():
                    walk(x)
            elif isinstance(v, list):
                for x in v:
                    walk(x)
        walk(json.loads(text))
        return out
    return [text]


def draft_counts():
    """Per region, the Han characters of the translation drafts with their counts. A character listed only in a
    charset file counts once."""
    per = collections.defaultdict(collections.Counter)
    for path in sorted(glob.glob(os.path.join(DRAFTS, "*.json"))):
        region = file_region(path)
        if region:
            per[region].update(count(file_values(path)))
    for path in sorted(glob.glob(os.path.join(DRAFTS, "charset_*.txt"))):
        region = file_region(path)
        if region:
            for ch in count(file_values(path)):
                if ch not in per[region]:
                    per[region][ch] = 1
    return per


def count(texts):
    c = collections.Counter()
    for t in texts:
        for ch in t:
            if is_han(ch):
                c[ch] += 1
    return c


def report(region, counter, write=False):
    have = set(H.charset(region))
    missing = [(ch, n) for ch, n in counter.most_common() if ch not in have]
    print(f"{region}: {len(counter)} distinct Han characters, {sum(counter.values())} uses, "
          f"{len(counter) - len(missing)} drawn, {len(missing)} missing")
    if missing:
        print("  missing, most used first: " + "".join(ch for ch, _ in missing[:200]))
    if write:
        out = os.path.join(HERE, f"han_todo_{region}.txt")
        with open(out, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# Han characters used by the {region} texts and not drawn yet (char, uses)\n")
            for ch, n in missing:
                f.write(f"{ch}\t{n}\tU+{ord(ch):04X}\n")
        print("  written", out)
    return missing


def main(args):
    write = "--write" in args
    args = [a for a in args if a != "--write"]
    forced = None
    if "--region" in args:
        i = args.index("--region")
        forced = args[i + 1]
        del args[i:i + 2]
    drafts = "--drafts" in args
    args = [a for a in args if a != "--drafts"]
    files = args or ([] if drafts else sorted(glob.glob(os.path.join(LOC, "LOC_*.asset"))))
    per = draft_counts() if drafts else collections.defaultdict(collections.Counter)
    for path in files:
        if path.endswith(".asset"):
            lang, values = loc_values(path)
            region = forced or REGION_OF.get(lang)
        else:
            values = file_values(path)
            region = forced or file_region(path)
        c = count(values)
        if region is None:
            if c:
                print(f"{os.path.basename(path)}: {len(c)} Han characters but no region (use --region)")
            continue
        per[region] += c
    if not per:
        print("No Japanese or Chinese text found yet (LOC assets: " + ", ".join(os.path.basename(f) for f in files) + ").")
        print("Our data covers " + ", ".join(f"{r} {len(H.charset(r))}" for r in H.REGIONS) + " characters.")
        return
    for region in H.REGIONS:
        if region in per:
            report(region, per[region], write)


if __name__ == "__main__":
    main(sys.argv[1:])
