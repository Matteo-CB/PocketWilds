"""Han batch plan (FH): which characters each drawing batch draws, and the check a batch runs before it hands over.

    python pw_font_han_batches.py plan [--force]   # compute the lots, write han_lists/han_lots.txt and the lot
                                                   # lists of HAN_BATCHES.md (refuses to move frozen lots without --force)
    python pw_font_han_batches.py lot c02          # the characters of lot c02 with their regions, in order
    python pw_font_han_batches.py parts            # every component name a lot can compose with
    python pw_font_han_batches.py check c02        # verify lot c02 alone (shared files + its own files):
                                                   # wrong characters, stroke counts, touches in every style,
                                                   # missing characters, proof sheet of the lot

Batches (see HAN_BATCHES.md):
  A  characters of the jōyō, Simplified and Traditional lists alike, one drawing for the three regions
  B  the rest of the jōyō (Japanese forms, and shared code points drawn differently per region)
  C  the Simplified list outside the jōyō (with Taiwan when the code point is shared)
  D  Traditional only, the more frequent half;  E  Traditional only, the rest
Inside a batch the characters the game's texts use come first, most used first (Localization/Drafts/*.json and
charset_*.txt, and the LOC assets),
then the planning lists' order. Characters already in the shared data files are left out, and so are the characters
held for the owner (han_lists/han_list_held.txt).
"""
import collections
import glob
import os
import re
import sys

if len(sys.argv) > 2 and sys.argv[1] == "check":
    os.environ["PW_HAN_LOTS"] = sys.argv[2].lower()        # the lot alone, before the data loads
elif len(sys.argv) > 1 and sys.argv[1] == "plan":
    os.environ["PW_HAN_LOTS"] = "none"                     # done = what the shared files already draw

import pw_font_han as H

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LISTS = os.path.join(HERE, "han_lists")
LOTS = os.path.join(LISTS, "han_lots.txt")
PLAN = os.path.join(HERE, "HAN_BATCHES.md")
OUT = os.path.join(ROOT, "TestResults", "Fonts", "Han", "lots")
LOT_SIZE = 250
RADICAL_FORMS = set("氵扌亻讠纟钅艹礻衤饣辶攵彳忄犭阝疒冫刂卩廴宀冖丷亠丶丿乛亅凵匚勹厶彐彡耂罒覀糹")


def is_han(c):
    o = ord(c)
    return (0x4E00 <= o <= 0x9FFF or 0x3400 <= o <= 0x4DBF or 0x20000 <= o <= 0x2FA1F) and c not in RADICAL_FORMS


def read_list(name):
    out, seen = [], set()
    with open(os.path.join(LISTS, name), encoding="utf-8") as f:
        for line in f:
            for c in line.split("#", 1)[0]:
                if is_han(c) and c not in seen:
                    seen.add(c)
                    out.append(c)
    return out


def game_counts():
    """Han characters the game's texts use, per region, with their counts (drafts and LOC assets)."""
    import pw_font_han_extract as X
    per = {r: collections.Counter() for r in H.REGIONS}
    for region, counter in X.draft_counts().items():
        per[region].update({c: n for c, n in counter.items() if is_han(c)})
    try:
        for path in sorted(glob.glob(os.path.join(X.LOC, "LOC_*.asset"))):
            lang, values = X.loc_values(path)
            region = X.REGION_OF.get(lang)
            if region:
                per[region].update(X.count(values))
    except Exception as exc:                                   # the plan still works from the drafts alone
        print("LOC assets not read:", exc)
    return per


def compute():
    joyo, sc, tc = read_list("han_list_jp_joyo.txt"), read_list("han_list_sc_common.txt"), read_list("han_list_tc_common.txt")
    held = set(read_list("han_list_held.txt"))
    regional = set(read_list("han_list_regional.txt"))
    game = game_counts()
    inset = {"JP": set(joyo) | set(game["JP"]), "SC": set(sc) | set(game["SC"]), "TC": set(tc) | set(game["TC"])}
    done = {r: set(H.charset(r)) for r in H.REGIONS}
    order = {}
    for lst in (sc, tc, joyo):
        for c in lst:
            order.setdefault(c, len(order))
    for r in H.REGIONS:
        for c in game[r]:
            order.setdefault(c, len(order))
    heat = collections.Counter()
    for r in H.REGIONS:
        heat.update(game[r])
    batches = {k: [] for k in "ABCT"}
    held_used = []
    for c in sorted(order, key=order.get):
        regions = "".join(H.REGION_LETTER[r] for r in H.REGIONS if c in inset[r] and c not in done[r])
        if not regions:
            continue
        if c in held:
            if heat[c]:
                held_used.append(c)
            continue
        everywhere = all(c in inset[r] for r in H.REGIONS)
        if everywhere and c not in regional:
            batches["A"].append((c, regions))
        elif c in inset["JP"]:
            batches["B"].append((c, regions))
        elif c in inset["SC"]:
            batches["C"].append((c, regions))
        else:
            batches["T"].append((c, regions))
    for k in batches:                                         # the game's characters first, most used first
        batches[k].sort(key=lambda e: (0 if heat[e[0]] else 1, -heat[e[0]], order[e[0]]))
    t = batches.pop("T")
    half = (len(t) + 1) // 2
    batches["D"], batches["E"] = t[:half], t[half:]
    lots = []
    for k in "ABCDE":
        lst = batches[k]
        for i in range(0, len(lst), LOT_SIZE):
            lots.append((f"{k.lower()}{i // LOT_SIZE + 1:02d}", lst[i:i + LOT_SIZE]))
    return lots, heat, held_used, {k: len(v) for k, v in batches.items()}


def read_lots():
    lots = collections.OrderedDict()
    with open(LOTS, encoding="utf-8") as f:
        for line in f:
            line = line.split("#", 1)[0].strip()
            if line:
                lot, ch, regions, game = line.split("\t")
                lots.setdefault(lot, []).append((ch, regions, game == "game"))
    return lots


def write_plan(lots, heat, held_used, sizes, force):
    if os.path.exists(LOTS) and not force:
        print(f"{LOTS} exists: the lots are frozen once the batches start (use --force to recompute).")
        return
    os.makedirs(LISTS, exist_ok=True)
    with open(LOTS, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Han lots: LOT<TAB>CHAR<TAB>REGIONS<TAB>game|list (written by pw_font_han_batches.py plan)\n")
        for lot, entries in lots:
            for c, regions in entries:
                f.write(f"{lot}\t{c}\t{regions}\t{'game' if heat[c] else 'list'}\n")
    lines = ["| Lot | Characters | Game | Regions |", "|---|---|---|---|"]
    body = []
    for lot, entries in lots:
        reg = collections.Counter(r for _, r in entries)
        ngame = sum(1 for c, _ in entries if heat[c])
        lines.append(f"| {lot} | {len(entries)} | {ngame} | " + ", ".join(f"{r} {n}" for r, n in reg.most_common()) + " |")
        body.append(f"### {lot}\n")
        groups = collections.OrderedDict()
        for c, r in entries:
            groups.setdefault(r, []).append(c)
        for r, cs in groups.items():
            text = "".join(cs)
            body.append(f"{r} ({len(cs)}):\n")
            body.append("```\n" + "\n".join(text[i:i + 50] for i in range(0, len(text), 50)) + "\n```\n")
    total = sum(sizes.values())
    head = [f"Batch sizes: A {sizes['A']}, B {sizes['B']}, C {sizes['C']}, D {sizes['D']}, E {sizes['E']} "
            f"(total {total} characters, {len(lots)} lots of up to {LOT_SIZE})."]
    if held_used:
        head.append("Held characters the game texts use (owner decision first): " + "".join(held_used))
    section = "\n".join(head) + "\n\n" + "\n".join(lines) + "\n\n" + "\n".join(body)
    text = open(PLAN, encoding="utf-8").read()
    a, b = "<!-- lots:start -->", "<!-- lots:end -->"
    i, j = text.index(a) + len(a), text.index(b)
    with open(PLAN, "w", encoding="utf-8", newline="\n") as f:
        f.write(text[:i] + "\n" + section + "\n" + text[j:])
    print(head[0])
    print("written", LOTS, "and the lot lists of", PLAN)


def show_lot(lot):
    entries = read_lots().get(lot.lower())
    if not entries:
        print("no lot", lot)
        return
    groups = collections.OrderedDict()
    for c, r, g in entries:
        groups.setdefault(r, []).append(c)
    for r, cs in groups.items():
        print(f"{r} ({len(cs)}): {''.join(cs)}")


def check_lot(lot):
    import pw_font_han_verify as V
    from pw_font_build import FAMILIES
    from PIL import Image, ImageDraw
    entries = read_lots().get(lot.lower())
    if not entries:
        print("no lot", lot)
        return
    styles = FAMILIES["PocketRound"] + FAMILIES["PocketPlush"]
    os.makedirs(OUT, exist_ok=True)
    report = []
    for region in H.REGIONS:
        L = H.REGION_LETTER[region]
        want = [c for c, r, _ in entries if L in r]
        if not want:
            continue
        have = set(H.charset(region))
        missing = [c for c in want if c not in have]
        drawn = [c for c in want if c in have]
        report.append(f"{region}: {len(drawn)}/{len(want)} drawn" + (f", missing {''.join(missing)}" if missing else ""))
        if not drawn:
            continue
        rows = V.verify(region, drawn, sheets=True, tag=lot.lower())
        s = V.summary(rows)
        wrong = [r["char"] + ":" + r["duel_lost"] for r in rows if r["duel_lost"]]
        strokes = [f"{r['char']}{r['strokes']}/{r['expected']}" for r in rows
                   if r["expected"] is not None and r["expected"] != r["strokes"]]
        report.append(f"  wrong {s['wrong']} {' '.join(wrong)}")
        report.append(f"  mean structure {s['mean_structure']}, rank>1 {s['rank>1']}")
        report.append(f"  stroke counts off {len(strokes)}: {' '.join(strokes)}")
        for st in styles:
            touch = [c for c in drawn if H.build_glyph(c, st, region).problems]
            report.append(f"  touches {st.key} {st.weight_name}: {len(touch)} {''.join(touch)}")
        # proof sheet: Regular and Plush side by side, at 56 px
        px, cols = 56, 20
        n = len(drawn)
        img = Image.new("RGB", (cols * (2 * px + 10) + 10, ((n + cols - 1) // cols) * (px + 8) + 10), (255, 251, 242))
        for i, c in enumerate(drawn):
            x = 10 + (i % cols) * (2 * px + 10)
            y = 10 + (i // cols) * (px + 8)
            for k, st in enumerate((styles[0], styles[-1])):
                o = V.render_ours(c, region, st).resize((px, px), Image.LANCZOS)
                ink = Image.new("RGB", (px, px), (61, 43, 31))
                img.paste(ink, (x + k * px, y), o)
        img.save(os.path.join(OUT, f"{lot.lower()}_{region}.png"))
    text = "\n".join(report)
    print(text)
    with open(os.path.join(OUT, f"{lot.lower()}_check.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(text + "\n")


def show_parts():
    """Component names grouped: position forms, frames, private parts, characters."""
    d = H.data()
    names = sorted(d.entries)
    groups = collections.OrderedDict((k, []) for k in ("left", "top", "bottom", "right", "frame", "other forms",
                                                        "named parts", "characters and parts"))
    for n in names:
        lst = d.entries[n]
        regs = "".join(sorted(set("".join("".join(sorted(e.regions)) if e.regions else "" for e in lst))))
        tag = n + ("@" + regs if regs else "")
        if n.endswith(".L"):
            groups["left"].append(tag)
        elif n.endswith(".T"):
            groups["top"].append(tag)
        elif n.endswith(".B"):
            groups["bottom"].append(tag)
        elif n.endswith(".R"):
            groups["right"].append(tag)
        elif n.endswith(".O") or any("in" in e.props for e in lst):
            groups["frame"].append(tag)
        elif "." in n.split("}")[-1]:
            groups["other forms"].append(tag)
        elif n.startswith("{"):
            groups["named parts"].append(tag)
        else:
            groups["characters and parts"].append(tag)
    for k, v in groups.items():
        print(f"{k} ({len(v)}):")
        print("  " + " ".join(v))


if __name__ == "__main__":
    args = sys.argv[1:]
    cmd = args[0] if args else "plan"
    if cmd == "plan":
        lots, heat, held_used, sizes = compute()
        write_plan(lots, heat, held_used, sizes, "--force" in args)
    elif cmd == "lot":
        show_lot(args[1])
    elif cmd == "check":
        check_lot(args[1])
    elif cmd == "parts":
        show_parts()
    else:
        print(__doc__)
