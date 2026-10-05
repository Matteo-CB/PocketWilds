"""Pocket Wilds performance baseline report (T22).

Reads the JSON reports written by PerformanceBaselineRunner and writes a Markdown summary checked
against the Performance Test Matrix budgets (Target 60 FPS).

Usage: python Tools/perf_report.py <out.md> <label>=<report.json> [<label>=<report.json> ...]
The first report is the reference; the others are compared with it.
"""
import json
import sys

BUDGETS = {
    "main": (8.0, 6.0),      # ms, (budget, ideal)
    "gpu": (11.0, 10.0),
    "sim": (1.5, None),
    "ui": (0.7, None),
}


def stat(s, key, field="mean"):
    v = s.get(key, {})
    return v.get(field, 0.0) if v.get("valid") else None


def fmt(v, digits=2):
    return "n/a" if v is None else f"{v:.{digits}f}"


def verdict(value, budget):
    if value is None:
        return "n/a"
    limit, ideal = budget
    if ideal is not None and value <= ideal:
        return "OK (idéal)"
    return "OK" if value <= limit else "HORS BUDGET"


def section(label, report):
    lines = [f"## {label}", ""]
    lines.append(f"- Build : {report['buildType']}, Unity {report['unityVersion']}, {report['graphicsApi']}")
    lines.append(f"- Machine : {report['device']} / {report['cpu']} / {report['gpu']} / {report['systemMemoryMb']} Mo RAM")
    lines.append(f"- Résolution : {report['screenWidth']}x{report['screenHeight']}, {report['timestampUtc']}")
    lines.append("")
    lines.append("| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for s in report["scenarios"]:
        cpu = s["cpuFrameMs"]
        lines.append(
            f"| {s['id']} {s['label']} | {s['tier']} | {s['visibleAnimals']} | "
            f"{cpu['mean']:.2f} / {cpu['p95']:.2f} / {cpu['p99']:.2f} / {cpu['max']:.1f} | "
            f"{fmt(stat(s, 'mainThreadMs'))} / {fmt(stat(s, 'mainThreadMs', 'p95'))} | {fmt(stat(s, 'renderThreadMs'))} | "
            f"{fmt(stat(s, 'gpuMs'))} | {fmt(stat(s, 'simulationMs'), 3)} | {fmt(stat(s, 'animationMs'), 3)} | {fmt(stat(s, 'uiMs'), 3)} | "
            f"{(s['gcBytes'].get('mean', 0.0)):.0f} ({s['framesWithGc']}) | {s['spikesOver33Ms']} | {s['batches']:.0f} | "
            f"{s['setPassCalls']:.0f} | {s['triangles']:.0f} | {s['totalUsedMemoryMb']:.0f} |")
    lines.append("")
    lines.append("Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :")
    lines.append("")
    lines.append("| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |")
    lines.append("|---|---|---|---|---|---|")
    for s in report["scenarios"]:
        gc_frames = s["framesWithGc"]
        lines.append(
            f"| {s['id']} | {verdict(stat(s, 'mainThreadMs', 'p95'), BUDGETS['main'])} | {verdict(stat(s, 'gpuMs', 'p95'), BUDGETS['gpu'])} | "
            f"{verdict(stat(s, 'simulationMs', 'p95'), BUDGETS['sim'])} | {verdict(stat(s, 'uiMs', 'p95'), BUDGETS['ui'])} | "
            f"{'OK' if gc_frames == 0 else f'{gc_frames} frames'} |")
    lines.append("")
    return lines


def comparison(ref_label, ref, label, other):
    lines = [f"## Évolution : {ref_label} → {label}", ""]
    lines.append("| Scénario | Batches | SetPass | Main moy (ms) | Render moy (ms) | Frames avec GC | Visibles |")
    lines.append("|---|---|---|---|---|---|---|")
    by_id = {s["id"]: s for s in ref["scenarios"]}
    for s in other["scenarios"]:
        r = by_id.get(s["id"])
        if r is None:
            continue
        lines.append(
            f"| {s['id']} | {r['batches']:.0f} → {s['batches']:.0f} | {r['setPassCalls']:.0f} → {s['setPassCalls']:.0f} | "
            f"{fmt(stat(r, 'mainThreadMs'))} → {fmt(stat(s, 'mainThreadMs'))} | {fmt(stat(r, 'renderThreadMs'))} → {fmt(stat(s, 'renderThreadMs'))} | "
            f"{r['framesWithGc']} → {s['framesWithGc']} | {r['visibleAnimals']} → {s['visibleAnimals']} |")
    lines.append("")
    return lines


def main():
    out = sys.argv[1]
    pairs = [a.split("=", 1) for a in sys.argv[2:]]
    reports = [(label, json.load(open(path, encoding="utf-8"))) for label, path in pairs]
    lines = ["# T22 Performance baseline", "",
             "Mesures produites par `PerformanceBaselineRunner` (vrai démarrage, vraie scène, sauvegarde de N lapins).",
             "Aucun téléphone n'était branché : ces chiffres valent pour la régression relative, pas pour les budgets mobiles.",
             ""]
    for label, report in reports:
        lines += section(label, report)
    ref_label, ref = reports[0]
    for label, report in reports[1:]:
        lines += comparison(ref_label, ref, label, report)
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("wrote", out)


if __name__ == "__main__":
    main()
