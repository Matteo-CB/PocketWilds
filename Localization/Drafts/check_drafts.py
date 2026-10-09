#!/usr/bin/env python3
"""Checks the draft translations of Localization/Drafts. Shared by the translation agents (TR1, TR2...): extend it,
never fork it.

Usage (from anywhere):
    python Localization/Drafts/check_drafts.py                 every <code>.json of Drafts
    python Localization/Drafts/check_drafts.py ja ko           only these languages
    options:
      --lengths      list every tight key (width > 130 % of the English, or a title wider than 18)
      --charset      also write Drafts/charset_<code>.txt: the draft's distinct characters, one line, sorted
      --quiet        errors and the summary only

For each draft <code>.json it verifies:
  - a UTF-8 JSON object {key: text}, no duplicate key, keys sorted (warning), no BOM (warning);
  - every LOC_en key is present, counted keys expanded per language, and no unknown key;
  - placeholders {x} and <tags> identical to the English ({species_the} and {biome_the} may be written as the bare
    {species} / {biome}, or the other way round: T39B turns them into per-language noun forms);
  - plural forms exactly PluralRules.Categories(language) (Assets/_Game/Core/Services/PluralRules.cs);
    en.json and fr.json are the sources, extracted unchanged: a plural mismatch there is only a warning;
  - NFC normalized;
  - no forbidden word (Localization/Glossary/<code>_forbidden.txt), no sparkle character, no em dash;
  - Thai: U+200B between words (long Thai runs without a break are reported), and never inside a syllable (before
    a following vowel or tone mark, after a leading vowel), doubled, or at either end of the text;
  - lengths: width of the translation against the English (wide CJK characters count 2, marks 0).

Forbidden list syntax (Localization/Glossary/<code>_forbidden.txt): one word or stem per line, "#" starts a comment
line (the meaning). A plain entry uses the language's default matching: anywhere in the text (substring) for ja,
zh-Hans, zh-Hant, th (no spaces between words); at the start of a word (stem) for every other language. Prefixes force a
mode: "~word" anywhere, "^word" at a word start, "=word" the whole word only. Matching ignores case; for Greek it also
ignores accents and diaeresis and reads final sigma as sigma (FOLD_ACCENTS).

The reference keys come from Assets/_Game/Content/Localization/LOC_en.asset when PyYAML can read it, else from
Drafts/en.json. Exit code 1 when an error is found.
"""

import json
import os
import re
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
LOC_ROOT = os.path.dirname(HERE)
REPO = os.path.dirname(LOC_ROOT)
GLOSSARY = os.path.join(LOC_ROOT, "Glossary")
LOC_EN_ASSET = os.path.join(REPO, "Assets", "_Game", "Content", "Localization", "LOC_en.asset")

# The languages of the Localization Plan (Docs/Design/Localization_Plan.md, 1.2).
KNOWN_CODES = ["en", "fr", "de", "es-419", "pt-BR", "it", "id", "tr", "pl", "ja", "ko", "zh-Hans", "zh-Hant", "th",
               "vi", "ru", "uk", "el", "nl"]
SOURCE_CODES = {"en", "fr"}
NO_SPACE_LANGUAGES = {"ja", "zh", "th", "lo", "km", "my", "yue"}

# Owner rule of 2026-10-06 (Docs/Design/No_Sparkle_Rule.md, NoSparkleRuleTests): written as escapes on purpose.
SPARKLE = ["\u2728", "\u2726", "\u2727", "\u2747", "\u2748", "\u2734", "\u2735", "\u2736", "\U0001F31F", "\U0001F4AB"]
# Writing Guide: no em dash. U+2015 is the horizontal bar some Japanese texts use as a dash.
EM_DASHES = ["\u2014", "\u2015", "\u2E3A", "\u2E3B"]

PLACEHOLDER = re.compile(r"\{[^{}]+\}|<[^<>]+>")
# Languages whose forbidden words match without accents (Greek: tonos and dialytika are often dropped, in capitals).
FOLD_ACCENTS = {"el"}

ARTICLE_FORMS = {"{species_the}": "{species}", "{biome_the}": "{biome}"}
TIGHT_RATIO = 1.30
TITLE_WIDTH = 18

# ---------------------------------------------------------------- CLDR plural rules (mirror of PluralRules.cs)

RULES = {}
for codes, rule in [
    ("en de nl el tr sv da nb nn no fi et hu bg ka az kk ur sw mr", "OneForOne"),
    ("ja ko zh yue th vi id in ms lo km my", "OtherOnly"),
    ("hi bn fa gu kn", "OneForZeroAndOne"),
    ("es it ca", "OneForOneManyMillions"),
    ("fr pt", "OneForZeroAndOneManyMillions"),
    ("ru uk be", "EastSlavic"),
    ("hr sr bs", "SouthSlavic"),
    ("pl", "Polish"),
    ("cs sk", "CzechSlovak"),
    ("ro mo", "Romanian"),
    ("ar", "Arabic"),
    ("he iw", "Hebrew"),
    ("fil tl", "Filipino"),
]:
    for c in codes.split():
        RULES[c] = rule

CATEGORIES = {
    "OtherOnly": ["other"],
    "OneForOne": ["one", "other"],
    "OneForZeroAndOne": ["one", "other"],
    "OneForOneManyMillions": ["one", "many", "other"],
    "OneForZeroAndOneManyMillions": ["one", "many", "other"],
    "EastSlavic": ["one", "few", "many", "other"],
    "Polish": ["one", "few", "many", "other"],
    "SouthSlavic": ["one", "few", "other"],
    "CzechSlovak": ["one", "few", "other"],
    "Romanian": ["one", "few", "other"],
    "Arabic": ["zero", "one", "two", "few", "many", "other"],
    "Hebrew": ["one", "two", "other"],
    "Filipino": ["one", "other"],
}
ALL_SUFFIXES = ["zero", "one", "two", "few", "many", "other"]
# Rules whose « one » form also serves other numbers than 1 (and 0): it must show {n}.
ONE_NEEDS_N = {"EastSlavic", "SouthSlavic", "Filipino"}


def base_language(code):
    return re.split(r"[-_]", code)[0].lower()


def rule_of(code):
    base = base_language(code)
    rule = RULES.get(base, "OneForOne")
    if rule == "OneForZeroAndOneManyMillions" and base == "pt":
        if any(part.upper() == "PT" for part in re.split(r"[-_]", code)[1:]):
            rule = "OneForOneManyMillions"
    return rule


# ---------------------------------------------------------------- reading


def load_json_object(path, problems):
    def hook(pairs):
        seen = {}
        for k, v in pairs:
            if k in seen:
                problems.append(("error", k, "duplicate key"))
            seen[k] = v
        return seen

    with open(path, "rb") as f:
        raw = f.read()
    if raw.startswith(b"\xef\xbb\xbf"):
        problems.append(("warning", "", "UTF-8 BOM at the start of the file"))
        raw = raw[3:]
    data = json.loads(raw.decode("utf-8"), object_pairs_hook=hook)
    if not isinstance(data, dict):
        raise ValueError("not a JSON object")
    keys = list(data.keys())
    if keys != sorted(keys):
        problems.append(("warning", "", "keys are not sorted"))
    for k, v in data.items():
        if not isinstance(v, str):
            problems.append(("error", k, "value is not a string"))
    return data


def load_reference():
    """The English table: LOC_en.asset when readable, else Drafts/en.json."""
    try:
        import yaml  # noqa: F401
        with open(LOC_EN_ASSET, encoding="utf-8") as f:
            text = f.read()
        text = re.sub(r"^%TAG.*$", "", text, flags=re.M)
        text = re.sub(r"^--- !u!\d+ &\d+", "---", text, flags=re.M)
        doc = yaml.safe_load(text)
        entries = doc["MonoBehaviour"]["entries"]
        return {e["key"]: ("" if e["value"] is None else str(e["value"])) for e in entries}, "LOC_en.asset"
    except Exception:
        with open(os.path.join(HERE, "en.json"), encoding="utf-8") as f:
            return json.load(f), "Drafts/en.json"


def load_forbidden(code):
    """[(entry, mode, meaning)] from Glossary/<code>_forbidden.txt; mode is 'any', 'start' or 'word'."""
    path = os.path.join(GLOSSARY, code + "_forbidden.txt")
    if not os.path.exists(path):
        return None
    default = "any" if base_language(code) in NO_SPACE_LANGUAGES else "start"
    entries = []
    meaning = ""
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            if line.startswith("#"):
                meaning = line.lstrip("#").strip()
                continue
            mode = default
            if line[0] in "~^=":
                mode = {"~": "any", "^": "start", "=": "word"}[line[0]]
                line = line[1:].strip()
            if line:
                entries.append((unicodedata.normalize("NFC", line), mode, meaning))
    return entries


def fold_accents(text):
    """Lower case without combining marks, final sigma read as sigma (Greek entries and texts)."""
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", text).lower().replace("\u03c2", "\u03c3")


def forbidden_regex(entry, mode):
    body = re.escape(entry)
    if mode == "any":
        return re.compile(body, re.IGNORECASE)
    if mode == "start":
        return re.compile(r"(?<!\w)" + body, re.IGNORECASE)
    return re.compile(r"(?<!\w)" + body + r"(?!\w)", re.IGNORECASE)


# ---------------------------------------------------------------- measures


def placeholders(text, normalize_articles=True):
    found = PLACEHOLDER.findall(text)
    if normalize_articles:
        found = [ARTICLE_FORMS.get(p, p) for p in found]
    return set(found)


def width(text):
    text = PLACEHOLDER.sub("000", text)
    w = 0
    for c in text:
        cat = unicodedata.category(c)
        if cat in ("Mn", "Me", "Cf"):
            continue
        w += 2 if unicodedata.east_asian_width(c) in ("W", "F") else 1
    return w


def is_title(key):
    last = key.split(".")[-1]
    return "title" in last or last in ("name", "name_the")


# ---------------------------------------------------------------- the check


def plural_groups(en):
    return sorted(k for k in en if k + ".one" in en and k + ".other" in en)


def expected_keys(en, groups, categories):
    forms = {g + "." + s for g in groups for s in ALL_SUFFIXES}
    keys = {k for k in en if k not in forms}
    for g in groups:
        keys.update(g + "." + c for c in categories)
    return keys


def check_language(code, en, groups, options):
    problems = []
    path = os.path.join(HERE, code + ".json")
    try:
        draft = load_json_object(path, problems)
    except Exception as e:  # unreadable file
        return [("error", "", "cannot read %s: %s" % (path, e))], {}, {}

    source = code in SOURCE_CODES
    rule = rule_of(code)
    categories = CATEGORIES[rule]
    group_forms = {g + "." + s: (g, s) for g in groups for s in ALL_SUFFIXES}
    expected = expected_keys(en, groups, categories)

    # Keys.
    for k in sorted(expected - set(draft)):
        if k in group_forms:
            level = "warning" if source else "error"
            problems.append((level, k, "missing plural form ." + group_forms[k][1] + " (" + rule + ")"))
        else:
            problems.append(("error", k, "missing key"))
    for k in sorted(set(draft) - expected):
        if k in group_forms:
            level = "warning" if source else "error"
            problems.append((level, k, "plural form ." + group_forms[k][1] + " not used by " + code + " (" + rule + ")"))
        else:
            problems.append(("error", k, "unknown key (not in LOC_en)"))

    forbidden = load_forbidden(code)
    if forbidden is None and code != "en":
        problems.append(("warning", "", "no forbidden list Glossary/%s_forbidden.txt" % code))
    folded = base_language(code) in FOLD_ACCENTS
    compiled = [(forbidden_regex(fold_accents(e) if folded else e, m), e, meaning) for e, m, meaning in (forbidden or [])]

    tight = []
    same_as_en = 0
    stats = {"keys": len(draft), "groups": len(groups), "rule": rule, "categories": categories}

    for k, text in sorted(draft.items()):
        if not isinstance(text, str):
            continue
        # Reference English text for this key.
        if k in group_forms:
            g, s = group_forms[k]
            ref = en.get(g + ".other", en.get(g, ""))
            ref_one = en.get(g + ".one", ref)
        elif k in en:
            g, s, ref, ref_one = None, None, en[k], None
        else:
            continue

        # Placeholders.
        want = placeholders(ref)
        got = placeholders(text)
        if s in ("one", "zero", "two") and "{n}" not in placeholders(ref_one) and rule not in ONE_NEEDS_N:
            allowed_missing = {"{n}"}
        else:
            allowed_missing = set()
        if s == "one" and rule in ONE_NEEDS_N and "{n}" not in got:
            problems.append(("error", k, "the « one » form of %s also counts 21, 31...: it must show {n}" % rule))
        missing = want - got - allowed_missing
        extra = got - want
        if missing or extra:
            problems.append(("error", k, "placeholders differ: missing %s, extra %s" % (sorted(missing), sorted(extra))))

        # Text rules.
        if text.strip() == "" and en.get(k, ref).strip() != "":
            problems.append(("error", k, "empty text"))
        if unicodedata.normalize("NFC", text) != text:
            problems.append(("error", k, "not NFC normalized"))
        if unicodedata.normalize("NFC", k) != k:
            problems.append(("error", k, "key not NFC normalized"))
        for c in SPARKLE:
            if c in text:
                problems.append(("error", k, "sparkle character U+%04X" % ord(c)))
        for c in EM_DASHES:
            if c in text:
                problems.append(("error", k, "em dash U+%04X" % ord(c)))
        if "\uFFFD" in text:
            problems.append(("error", k, "replacement character U+FFFD"))
        searched = fold_accents(text) if folded else text
        for regex, entry, meaning in compiled:
            if regex.search(searched):
                problems.append(("error", k, "forbidden word « %s » (%s)" % (entry, meaning)))

        # Thai: words separated by U+200B.
        if base_language(code) == "th":
            for run in re.findall(r"[\u0E00-\u0E7F]+", text):
                if len(run) > 24:
                    problems.append(("error", k, "Thai run of %d characters without U+200B: %s" % (len(run), run)))
                elif len(run) > 16:
                    problems.append(("warning", k, "long Thai run of %d characters: %s" % (len(run), run)))
            if "\u200B " in text or " \u200B" in text:
                problems.append(("warning", k, "U+200B next to a space"))
            # A break may not split a syllable: never before a vowel or tone mark that leans on the previous consonant
            # (U+0E30 to U+0E3A, U+0E45, U+0E47 to U+0E4E), never after a leading vowel (U+0E40 to U+0E44).
            for i, c in enumerate(text):
                if c != "\u200b":
                    continue
                before = text[i - 1] if i > 0 else ""
                after = text[i + 1] if i + 1 < len(text) else ""
                if not before or not after:
                    problems.append(("error", k, "U+200B at the start or the end of the text"))
                elif after == "\u200b":
                    problems.append(("error", k, "two U+200B in a row"))
                elif "\u0e30" <= after <= "\u0e3a" or after == "\u0e45" or "\u0e47" <= after <= "\u0e4e":
                    problems.append(("error", k, "U+200B before U+%04X: it splits a syllable" % ord(after)))
                elif "\u0e40" <= before <= "\u0e44":
                    problems.append(("error", k, "U+200B after the leading vowel U+%04X: it splits a syllable" % ord(before)))

        # Lengths.
        if code != "en":
            en_text = en.get(k, ref) if k in en else ref
            w_en, w = width(en_text), width(text)
            if text == en_text and re.search(r"[A-Za-z]{2,}", PLACEHOLDER.sub("", text)):
                same_as_en += 1
            ratio = w / w_en if w_en else 1.0
            reasons = []
            if ratio > TIGHT_RATIO and w - w_en >= 2:
                reasons.append("%d%%" % round(ratio * 100))
            if is_title(k) and w > TITLE_WIDTH:
                reasons.append("title %d" % w)
            if reasons:
                tight.append((k, w_en, w, ", ".join(reasons), en_text, text))

    stats["tight"] = tight
    stats["same_as_en"] = same_as_en
    return problems, stats, draft


def write_charset(code, draft):
    chars = set()
    blanks = set()
    for text in draft.values():
        for c in PLACEHOLDER.sub("", text):
            if c.isspace() or unicodedata.category(c) in ("Cc", "Cf"):
                blanks.add(c)
            else:
                chars.add(c)
    line = "".join(sorted(chars))
    with open(os.path.join(HERE, "charset_%s.txt" % code), "w", encoding="utf-8", newline="\n") as f:
        f.write(line + "\n")
    han = sum(1 for c in chars if 0x3400 <= ord(c) <= 0x9FFF or 0xF900 <= ord(c) <= 0xFAFF or ord(c) >= 0x20000)
    kana = sum(1 for c in chars if 0x3040 <= ord(c) <= 0x30FF or 0x31F0 <= ord(c) <= 0x31FF)
    hangul = sum(1 for c in chars if 0xAC00 <= ord(c) <= 0xD7A3 or 0x1100 <= ord(c) <= 0x11FF or 0x3130 <= ord(c) <= 0x318F)
    ascii_count = sum(1 for c in chars if ord(c) < 0x80)
    return len(chars), han, kana, hangul, ascii_count, sorted("U+%04X" % ord(c) for c in blanks)


def main(argv):
    options = {a for a in argv if a.startswith("--")}
    codes = [a for a in argv if not a.startswith("--")]
    if not codes:
        codes = sorted(os.path.splitext(n)[0] for n in os.listdir(HERE)
                       if n.endswith(".json") and os.path.splitext(n)[0] in KNOWN_CODES)
    en, reference = load_reference()
    groups = plural_groups(en)
    print("Reference: %s, %d keys, %d counted keys" % (reference, len(en), len(groups)))
    en_json = os.path.join(HERE, "en.json")
    if reference == "LOC_en.asset" and os.path.exists(en_json):
        with open(en_json, encoding="utf-8") as f:
            if json.load(f) != en:
                print("WARNING en.json differs from LOC_en.asset: extract it again")

    total_errors = 0
    for code in codes:
        problems, stats, draft = check_language(code, en, groups, options)
        errors = [p for p in problems if p[0] == "error"]
        warnings = [p for p in problems if p[0] == "warning"]
        total_errors += len(errors)
        print("\n== %s: %d keys, rule %s (%s), %d errors, %d warnings, %d tight, %d same as English" % (
            code, stats.get("keys", 0), stats.get("rule", "?"), "/".join(stats.get("categories", [])),
            len(errors), len(warnings), len(stats.get("tight", [])), stats.get("same_as_en", 0)))
        for level, key, message in problems:
            if level == "warning" and "--quiet" in options:
                continue
            print("  %-7s %-45s %s" % (level.upper(), key, message))
        if "--lengths" in options:
            for k, w_en, w, why, en_text, text in stats.get("tight", []):
                print("  TIGHT   %-45s en %3d -> %3d (%s)  %s  |  %s" % (k, w_en, w, why, en_text, text))
        if "--charset" in options and draft:
            n, han, kana, hangul, ascii_count, blanks = write_charset(code, draft)
            print("  charset_%s.txt: %d distinct characters (Han %d, kana %d, Hangul %d, ASCII %d); blanks left out: %s" % (
                code, n, han, kana, hangul, ascii_count, ", ".join(blanks) or "none"))
    print("\nTotal errors: %d" % total_errors)
    return 1 if total_errors else 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv[1:]))
