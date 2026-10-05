# T22 Performance baseline

Mesures produites par `PerformanceBaselineRunner` (vrai démarrage, vraie scène, sauvegarde de N lapins).
Aucun téléphone n'était branché : ces chiffres valent pour la régression relative, pas pour les budgets mobiles.

## Player Windows (après optimisations)

- Build : Player (development), Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 480x1040, 2026-10-05 02:25:51Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P0 Empty sanctuary | Medium | 0 | 16.67 / 16.73 / 16.91 / 17.0 | 1.14 / 1.46 | 0.46 | 0.07 | 0.002 | 0.000 | 0.046 | 0 (1) | 0 | 40 | 15 | 20312 | 222 |
| P1 1 rabbit | Medium | 1 | 16.67 / 16.76 / 16.89 / 17.3 | 1.32 / 1.76 | 0.49 | 0.09 | 0.008 | 0.061 | 0.048 | 32 (2) | 0 | 43 | 18 | 24558 | 223 |
| S10 10 rabbits | Medium | 10 | 16.67 / 16.71 / 16.79 / 17.1 | 1.32 / 1.69 | 0.47 | 0.14 | 0.010 | 0.153 | 0.046 | 32 (2) | 0 | 58 | 18 | 56222 | 224 |
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.67 / 16.72 / 16.93 / 17.5 | 1.52 / 1.87 | 0.50 | 0.29 | 0.012 | 0.222 | 0.048 | 32 (2) | 0 | 78 | 18 | 98392 | 225 |
| S30 30 rabbits | Medium | 30 | 16.67 / 16.73 / 16.84 / 17.0 | 1.52 / 1.88 | 0.50 | 0.38 | 0.013 | 0.240 | 0.046 | 32 (2) | 0 | 92 | 18 | 127932 | 226 |
| S50 50 rabbits (30 A + 20 B) | Medium | 50 | 16.67 / 16.80 / 17.03 / 17.5 | 1.76 / 2.29 | 0.55 | 0.56 | 0.016 | 0.315 | 0.047 | 32 (2) | 0 | 124 | 18 | 195405 | 227 |
| P2-Low 20 rabbits, Low tier | Low | 20 | 16.67 / 16.76 / 17.07 / 17.5 | 1.33 / 1.82 | 0.37 | 0.08 | 0.012 | 0.200 | 0.047 | 33 (2) | 0 | 58 | 15 | 26353 | 214 |
| S50-High 50 rabbits, High tier | High | 50 | 16.73 / 17.61 / 19.27 / 54.5 | 2.40 / 5.36 | 0.53 | 0.58 | 0.017 | 0.330 | 0.049 | 33 (2) | 1 | 126 | 18 | 200544 | 241 |
| P5 Hatch burst: 10 hatches in a row, 20 rabbits | Medium | 30 | 16.67 / 16.71 / 16.84 / 17.3 | 1.56 / 1.94 | 0.49 | 0.35 | 0.013 | 0.237 | 0.062 | 41 (9) | 0 | 90 | 20 | 124550 | 227 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P0 | OK (idéal) | OK (idéal) | OK | OK | 1 frames |
| P1 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S10 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P2 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S30 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P2-Low | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50-High | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P5 | OK (idéal) | OK (idéal) | OK | OK | 9 frames |

## Player Windows (avant)

- Build : Player (development), Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 480x1040, 2026-10-05 02:04:30Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P0 Empty sanctuary | Medium | 1 | 16.74 / 18.01 / 21.06 / 38.5 | 2.28 / 3.95 | 0.52 | 0.07 | 0.007 | 0.039 | 0.044 | 203 (13) | 3 | 70 | 17 | 14731 | 221 |
| P1 1 rabbit | Medium | 2 | 16.75 / 17.97 / 22.67 / 56.6 | 2.15 / 4.02 | 0.50 | 0.09 | 0.004 | 0.066 | 0.045 | 364 (12) | 1 | 69 | 18 | 17980 | 222 |
| S10 10 rabbits | Medium | 10 | 16.67 / 16.83 / 17.07 / 21.0 | 1.64 / 2.25 | 0.55 | 0.20 | 0.011 | 0.163 | 0.051 | 456 (4) | 0 | 89 | 18 | 48391 | 223 |
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.69 / 17.68 / 18.12 / 39.5 | 1.81 / 3.06 | 0.52 | 0.28 | 0.011 | 0.214 | 0.045 | 172 (3) | 1 | 102 | 18 | 87318 | 224 |
| S30 30 rabbits | Medium | 31 | 17.10 / 18.98 / 31.17 / 124.6 | 3.47 / 8.86 | 0.51 | 0.31 | 0.013 | 0.226 | 0.042 | 191 (9) | 4 | 122 | 18 | 100009 | 225 |
| S50 50 rabbits (30 A + 20 B) | Medium | 50 | 16.65 / 17.94 / 20.08 / 22.7 | 2.63 / 4.10 | 0.55 | 0.56 | 0.015 | 0.327 | 0.044 | 183 (7) | 0 | 144 | 19 | 180237 | 226 |
| P2-Low 20 rabbits, Low tier | Low | 20 | 16.67 / 16.90 / 17.70 / 18.1 | 1.53 / 2.42 | 0.40 | 0.09 | 0.012 | 0.228 | 0.047 | 172 (3) | 0 | 70 | 15 | 21247 | 213 |
| S50-High 50 rabbits, High tier | High | 50 | 16.93 / 20.67 / 28.95 / 65.2 | 3.03 / 10.77 | 0.35 | 0.27 | 0.017 | 0.192 | 0.055 | 655 (10) | 3 | 76 | 15 | 80927 | 240 |
| P5 Hatch burst: 10 hatches in a row, 20 rabbits | Medium | 30 | 16.67 / 16.84 / 17.72 / 19.1 | 1.78 / 2.58 | 0.52 | 0.36 | 0.014 | 0.248 | 0.062 | 308 (21) | 0 | 116 | 20 | 116555 | 226 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P0 | OK (idéal) | OK (idéal) | OK | OK | 13 frames |
| P1 | OK (idéal) | OK (idéal) | OK | OK | 12 frames |
| S10 | OK (idéal) | OK (idéal) | OK | OK | 4 frames |
| P2 | OK (idéal) | OK (idéal) | OK | OK | 3 frames |
| S30 | HORS BUDGET | OK (idéal) | OK | OK | 9 frames |
| S50 | OK (idéal) | OK (idéal) | OK | OK | 7 frames |
| P2-Low | OK (idéal) | OK (idéal) | OK | OK | 3 frames |
| S50-High | HORS BUDGET | OK (idéal) | OK | OK | 10 frames |
| P5 | OK (idéal) | OK (idéal) | OK | OK | 21 frames |

## Éditeur (avant)

- Build : Editor PlayMode, Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 1176x731, 2026-10-05 01:59:44Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P0 Empty sanctuary | Medium | 0 | 16.67 / 17.59 / 17.96 / 29.5 | 1.66 / 1.85 | 0.92 | 0.94 | 0.002 | 0.000 | 0.051 | 213172 (481) | 0 | 188 | 13 | 24318 | 1726 |
| P1 1 rabbit | Medium | 1 | 16.67 / 17.55 / 17.88 / 18.2 | 1.70 / 1.99 | 0.97 | 0.91 | 0.005 | 0.066 | 0.051 | 206838 (481) | 0 | 191 | 16 | 28564 | 1730 |
| S10 10 rabbits | Medium | 10 | 16.69 / 17.53 / 17.83 / 37.5 | 1.94 / 2.27 | 0.94 | 0.93 | 0.010 | 0.190 | 0.052 | 211008 (480) | 1 | 209 | 16 | 66550 | 1726 |
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.67 / 17.47 / 17.80 / 18.1 | 1.91 / 2.19 | 0.96 | 0.92 | 0.012 | 0.239 | 0.050 | 208491 (481) | 0 | 229 | 16 | 108750 | 1802 |
| S30 30 rabbits | Medium | 30 | 16.68 / 17.57 / 17.83 / 37.2 | 2.09 / 2.45 | 0.98 | 0.92 | 0.014 | 0.292 | 0.051 | 205661 (480) | 1 | 249 | 16 | 150946 | 1729 |
| S50 50 rabbits (30 A + 20 B) | Medium | 50 | 16.67 / 17.54 / 17.87 / 18.5 | 2.14 / 2.50 | 0.97 | 0.91 | 0.017 | 0.392 | 0.050 | 202327 (481) | 0 | 289 | 16 | 235341 | 1801 |
| P2-Low 20 rabbits, Low tier | Low | 20 | 16.68 / 17.47 / 17.91 / 35.3 | 1.86 / 2.02 | 0.84 | 0.90 | 0.012 | 0.243 | 0.049 | 214399 (480) | 1 | 148 | 13 | 29739 | 1714 |
| S50-High 50 rabbits, High tier | High | 50 | 16.67 / 17.57 / 18.02 / 18.3 | 2.26 / 2.72 | 1.04 | 0.88 | 0.019 | 0.413 | 0.053 | 188940 (481) | 0 | 289 | 16 | 236166 | 1813 |
| P5 Hatch burst: 10 hatches in a row, 20 rabbits | Medium | 30 | 16.67 / 17.63 / 18.08 / 26.4 | 2.12 / 2.58 | 1.02 | 0.89 | 0.013 | 0.289 | 0.071 | 201029 (840) | 0 | 230 | 18 | 137362 | 1759 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P0 | OK (idéal) | OK (idéal) | OK | OK | 481 frames |
| P1 | OK (idéal) | OK (idéal) | OK | OK | 481 frames |
| S10 | OK (idéal) | OK (idéal) | OK | OK | 480 frames |
| P2 | OK (idéal) | OK (idéal) | OK | OK | 481 frames |
| S30 | OK (idéal) | OK (idéal) | OK | OK | 480 frames |
| S50 | OK (idéal) | OK (idéal) | OK | OK | 481 frames |
| P2-Low | OK (idéal) | OK (idéal) | OK | OK | 480 frames |
| S50-High | OK (idéal) | OK (idéal) | OK | OK | 481 frames |
| P5 | OK (idéal) | OK (idéal) | OK | OK | 840 frames |

## Évolution : Player Windows (après optimisations) → Player Windows (avant)

| Scénario | Batches | SetPass | Main moy (ms) | Render moy (ms) | Frames avec GC | Visibles |
|---|---|---|---|---|---|---|
| P0 | 40 → 70 | 15 → 17 | 1.14 → 2.28 | 0.46 → 0.52 | 1 → 13 | 0 → 1 |
| P1 | 43 → 69 | 18 → 18 | 1.32 → 2.15 | 0.49 → 0.50 | 2 → 12 | 1 → 2 |
| S10 | 58 → 89 | 18 → 18 | 1.32 → 1.64 | 0.47 → 0.55 | 2 → 4 | 10 → 10 |
| P2 | 78 → 102 | 18 → 18 | 1.52 → 1.81 | 0.50 → 0.52 | 2 → 3 | 20 → 20 |
| S30 | 92 → 122 | 18 → 18 | 1.52 → 3.47 | 0.50 → 0.51 | 2 → 9 | 30 → 31 |
| S50 | 124 → 144 | 18 → 19 | 1.76 → 2.63 | 0.55 → 0.55 | 2 → 7 | 50 → 50 |
| P2-Low | 58 → 70 | 15 → 15 | 1.33 → 1.53 | 0.37 → 0.40 | 2 → 3 | 20 → 20 |
| S50-High | 126 → 76 | 18 → 15 | 2.40 → 3.03 | 0.53 → 0.35 | 2 → 10 | 50 → 50 |
| P5 | 90 → 116 | 20 → 20 | 1.56 → 1.78 | 0.49 → 0.52 | 9 → 21 | 30 → 30 |

## Évolution : Player Windows (après optimisations) → Éditeur (avant)

| Scénario | Batches | SetPass | Main moy (ms) | Render moy (ms) | Frames avec GC | Visibles |
|---|---|---|---|---|---|---|
| P0 | 40 → 188 | 15 → 13 | 1.14 → 1.66 | 0.46 → 0.92 | 1 → 481 | 0 → 0 |
| P1 | 43 → 191 | 18 → 16 | 1.32 → 1.70 | 0.49 → 0.97 | 2 → 481 | 1 → 1 |
| S10 | 58 → 209 | 18 → 16 | 1.32 → 1.94 | 0.47 → 0.94 | 2 → 480 | 10 → 10 |
| P2 | 78 → 229 | 18 → 16 | 1.52 → 1.91 | 0.50 → 0.96 | 2 → 481 | 20 → 20 |
| S30 | 92 → 249 | 18 → 16 | 1.52 → 2.09 | 0.50 → 0.98 | 2 → 480 | 30 → 30 |
| S50 | 124 → 289 | 18 → 16 | 1.76 → 2.14 | 0.55 → 0.97 | 2 → 481 | 50 → 50 |
| P2-Low | 58 → 148 | 15 → 13 | 1.33 → 1.86 | 0.37 → 0.84 | 2 → 480 | 20 → 20 |
| S50-High | 126 → 289 | 18 → 16 | 2.40 → 2.26 | 0.53 → 1.04 | 2 → 481 | 50 → 50 |
| P5 | 90 → 230 | 20 → 18 | 1.56 → 2.12 | 0.49 → 1.02 | 9 → 840 | 30 → 30 |

## Constats et actions (T22)

1. **Décor fusionné par chunk** : les touffes d'herbe et les fleurs (environ 190 renderers) sont fusionnées par palette et classe de densité (9 maillages). Le chunk passe de 218 à 43 renderers. Player : batches P0 70 → 40, P2 102 → 78, S50 144 → 124. Rendu identique (écart moyen 0,27/255 sur la capture de référence). La luminosité de chaque touffe reste dans uv.y (bit 14 de la Shader User Value).
2. **GC en jeu stable : 0 octet par frame.** Les seules allocations restantes viennent des écritures de sauvegarde (thread d'écriture dédié, environ 15 Ko par sauvegarde déclenchée par un événement) et du chemin d'éclosion (9 frames sur 10 éclosions). Corrigé pendant T22 : `Task.Run` par sauvegarde remplacé par un thread réutilisé, tampon `FileStream` de 64 Ko et relecture `ReadAllBytes` remplacés par un tampon réutilisé (0 o managé par écriture mesuré).
3. **Pool d'animaux préchauffé** : le sanctuaire remplit le reste du budget visible (2 vues par frame à l'entrée). Une éclosion n'instancie plus rien en jeu.
4. **Pic unique à 54 ms (S50-High)** : première utilisation des variantes de shaders du palier High dans le processus. À faire : préchauffage des variantes au démarrage (ShaderVariantCollection, GraphicsStateCollection) avant le gate T30.
5. **Mémoire** : 214 à 241 Mo utilisés sur le player PC (driver D3D12 inclus), sous la cible de 350 Mo. Ce n'est pas un chiffre mobile.
6. **Éditeur** : environ 200 Ko de GC par frame viennent de l'éditeur lui-même (sonde `AllocationProbe` : 1 frame d'allocation du jeu sur 885 au repos). Les chiffres éditeur ne servent qu'à la comparaison relative.
7. **Non validé sur mobile** : aucun appareil branché. À faire dès qu'un appareil Target est disponible : Android development build, même runner (`-pw-benchmark`), test thermique de 20 à 30 minutes.

Reproduire : `Pocket Wilds/Build/Windows Benchmark (development)` puis `PocketWilds-bench.exe -pw-benchmark <rapport.json> -screen-width 480 -screen-height 1040 -screen-fullscreen 0`, puis `python Tools/perf_report.py <sortie.md> "<libellé>=<rapport.json>" ...`. Dans l'éditeur : test PlayMode explicite `PerformanceBaselineTests.Baseline_AllScenarios_Editor`.
