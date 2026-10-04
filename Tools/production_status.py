"""Update Assets/_Game/Editor/Production/AssetProductionStatus.json from the command line.

Examples:
  python Tools/production_status.py asset A-SP-001 --status ART_REVIEW --note "T06 blockout approved" --gates G02
  python Tools/production_status.py task T06 --status DONE --note "Phone-scale captures pass"
  python Tools/production_status.py show A-SP-001
"""
import argparse
import datetime
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Assets", "_Game", "Editor", "Production", "AssetProductionStatus.json")
ASSET_ORDER = ["PLANNED", "BLOCKOUT", "ART_REVIEW", "INTEGRATED", "PERFORMANCE_REVIEW", "DONE"]


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load():
    with open(PATH, encoding="utf-8") as f:
        return json.load(f)


def save(doc):
    doc["updatedUtc"] = now()
    with open(PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
        f.write("\n")


def find(items, key, value):
    for item in items:
        if item[key] == value:
            return item
    sys.exit(f"Unknown {key} {value}")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("asset")
    a.add_argument("id")
    a.add_argument("--status")
    a.add_argument("--note")
    a.add_argument("--gates", nargs="*", default=[])
    a.add_argument("--version", default=None)
    a.add_argument("--force", action="store_true", help="Allow skipping or moving back a status.")
    t = sub.add_parser("task")
    t.add_argument("id")
    t.add_argument("--status", required=True)
    t.add_argument("--note")
    s = sub.add_parser("show")
    s.add_argument("id")
    args = parser.parse_args()

    doc = load()
    if args.cmd == "show":
        item = next((x for x in doc["assets"] if x["id"] == args.id), None) or find(doc["productionQueue"], "task", args.id)
        print(json.dumps(item, indent=2, ensure_ascii=False))
        return

    if args.cmd == "task":
        task = find(doc["productionQueue"], "task", args.id)
        task["status"] = args.status
        if args.note:
            task["notes"] = args.note
    else:
        asset = find(doc["assets"], "id", args.id)
        if args.status:
            old, new = asset["status"], args.status
            if old in ASSET_ORDER and new in ASSET_ORDER and not args.force:
                if ASSET_ORDER.index(new) > ASSET_ORDER.index(old) + 1:
                    sys.exit(f"{args.id}: {old} -> {new} skips a status. Use --force only with a reason.")
            asset["status"] = new
        if args.note:
            asset["notes"] = (asset["notes"] + " | " if asset["notes"] else "") + args.note
        if args.gates:
            asset["passedGates"] = sorted(set(asset.get("passedGates", []) + args.gates))
            asset["lastValidationUtc"] = now()
            asset["lastValidationVersion"] = args.version or asset.get("lastValidationVersion")
    save(doc)
    print("updated", args.id)


if __name__ == "__main__":
    main()
