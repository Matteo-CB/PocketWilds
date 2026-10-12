# T22 Performance baseline

Mesures produites par `PerformanceBaselineRunner` (vrai démarrage, vraie scène, sauvegarde de N lapins).
Aucun téléphone n'était branché : ces chiffres valent pour la régression relative, pas pour les budgets mobiles.

## T50 PC

- Build : Player (development), Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 540x1151, 2026-10-12 01:18:21Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.67 / 16.75 / 17.00 / 17.6 | 2.19 / 2.89 | 0.62 | 0.21 | 0.076 | 0.191 | 0.124 | 33 (3) | 0 | 110 | 52 | 137046 | 299 |
| W1 Chunk streaming: meadow in and out twice, 20 animals | Medium | 20 | 16.67 / 16.71 / 16.85 / 17.1 | 1.90 / 2.41 | 0.57 | 0.21 | 0.073 | 0.154 | 0.113 | 453 (10) | 0 | 114 | 57 | 142254 | 310 |
| W2 Strip travel: the camera crosses the eight Grassland chunks east and back, 20 animals | Medium | 20 | 16.67 / 16.70 / 16.79 / 17.3 | 1.85 / 2.42 | 0.58 | 0.17 | 0.077 | 0.089 | 0.116 | 2461 (29) | 0 | 98 | 56 | 143228 | 311 |
| P9 Prairie vers Forêt: the camera travels from the meadow into the Forest Clearing and back, Forest open, 20 animals | Medium | 20 | 16.67 / 16.72 / 16.84 / 17.4 | 1.84 / 2.36 | 0.57 | 0.16 | 0.148 | 0.054 | 0.115 | 2148 (61) | 0 | 82 | 53 | 122634 | 377 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P2 | OK (idéal) | OK (idéal) | OK | OK | 3 frames |
| W1 | OK (idéal) | OK (idéal) | OK | OK | 10 frames |
| W2 | OK (idéal) | OK (idéal) | OK | OK | 29 frames |
| P9 | OK (idéal) | OK (idéal) | OK | OK | 61 frames |

