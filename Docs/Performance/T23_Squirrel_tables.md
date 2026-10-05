# T23 Squirrel, performance

Mesures produites par `PerformanceBaselineRunner` (vrai démarrage, vraie scène, sauvegarde de N lapins).
Aucun téléphone n'était branché : ces chiffres valent pour la régression relative, pas pour les budgets mobiles.

## T22 (lapins seuls)

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

## T23 (lapins + écureuils)

- Build : Player (development), Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 480x1040, 2026-10-05 03:37:51Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P0 Empty sanctuary | Medium | 0 | 16.67 / 16.70 / 16.82 / 17.1 | 1.14 / 1.43 | 0.47 | 0.07 | 0.002 | 0.000 | 0.045 | 0 (1) | 0 | 40 | 15 | 20309 | 225 |
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.67 / 16.78 / 17.56 / 18.8 | 1.59 / 2.21 | 0.49 | 0.29 | 0.018 | 0.229 | 0.049 | 32 (2) | 0 | 78 | 18 | 98388 | 226 |
| S50 50 rabbits (30 A + 20 B) | Medium | 50 | 16.67 / 16.70 / 16.81 / 17.0 | 1.64 / 2.01 | 0.47 | 0.55 | 0.017 | 0.314 | 0.046 | 32 (2) | 0 | 124 | 18 | 195410 | 226 |
| P5 Hatch burst: 10 hatches in a row, 20 rabbits | Medium | 30 | 16.67 / 16.70 / 16.79 / 17.0 | 1.55 / 1.86 | 0.49 | 0.34 | 0.014 | 0.240 | 0.060 | 116 (20) | 0 | 90 | 20 | 126254 | 227 |
| P2-Mix 20 animals, 10 rabbits + 10 squirrels | Medium | 20 | 16.67 / 16.70 / 16.85 / 17.1 | 1.49 / 1.99 | 0.50 | 0.29 | 0.012 | 0.219 | 0.046 | 33 (2) | 0 | 78 | 18 | 103774 | 226 |
| S50-Mix 50 animals, 25 rabbits + 25 squirrels | Medium | 50 | 16.67 / 16.74 / 16.82 / 16.9 | 1.68 / 1.99 | 0.47 | 0.60 | 0.017 | 0.330 | 0.045 | 33 (2) | 0 | 126 | 18 | 212614 | 227 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P0 | OK (idéal) | OK (idéal) | OK | OK | 1 frames |
| P2 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P5 | OK (idéal) | OK (idéal) | OK | OK | 20 frames |
| P2-Mix | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50-Mix | OK (idéal) | OK (idéal) | OK | OK | 2 frames |

## Évolution : T22 (lapins seuls) → T23 (lapins + écureuils)

| Scénario | Batches | SetPass | Main moy (ms) | Render moy (ms) | Frames avec GC | Visibles |
|---|---|---|---|---|---|---|
| P0 | 40 → 40 | 15 → 15 | 1.14 → 1.14 | 0.46 → 0.47 | 1 → 1 | 0 → 0 |
| P2 | 78 → 78 | 18 → 18 | 1.52 → 1.59 | 0.50 → 0.49 | 2 → 2 | 20 → 20 |
| S50 | 124 → 124 | 18 → 18 | 1.76 → 1.64 | 0.55 → 0.47 | 2 → 2 | 50 → 50 |
| P5 | 90 → 90 | 20 → 20 | 1.56 → 1.55 | 0.49 → 0.49 | 9 → 20 | 30 → 30 |

