# T24 Hedgehog, performance (tableaux)

Mesures produites par `PerformanceBaselineRunner` (vrai démarrage, vraie scène, sauvegarde de N lapins).
Aucun téléphone n'était branché : ces chiffres valent pour la régression relative, pas pour les budgets mobiles.

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

## T24 (trois espèces)

- Build : Player (development), Unity 6000.3.8f1, Direct3D12
- Machine : B450 AORUS ELITE (Gigabyte Technology Co., Ltd.) / AMD Ryzen 7 5800X 8-Core Processor  x16 / NVIDIA GeForce RTX 5060 Ti / 32691 Mo RAM
- Résolution : 480x1040, 2026-10-05 04:08:12Z

| Scénario | Tier | Visibles | CPU frame moy/p95/p99/max (ms) | Main moy/p95 | Render | GPU | Simulation | Animation | UI | GC o/frame (frames) | Pics >33 ms | Batches | SetPass | Triangles | Mémoire utilisée (Mo) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| P0 Empty sanctuary | Medium | 0 | 16.67 / 16.70 / 16.76 / 17.2 | 1.09 / 1.38 | 0.46 | 0.07 | 0.002 | 0.000 | 0.044 | 0 (1) | 0 | 40 | 15 | 20312 | 227 |
| P2 20 rabbits (vertical slice baseline) | Medium | 20 | 16.67 / 16.77 / 16.88 / 17.1 | 1.57 / 2.14 | 0.52 | 0.34 | 0.017 | 0.223 | 0.047 | 32 (2) | 0 | 78 | 18 | 98388 | 228 |
| S50 50 rabbits (30 A + 20 B) | Medium | 50 | 16.67 / 16.70 / 16.85 / 17.1 | 1.71 / 2.11 | 0.49 | 0.55 | 0.016 | 0.336 | 0.048 | 32 (2) | 0 | 124 | 18 | 195407 | 228 |
| P5 Hatch burst: 10 hatches in a row, 20 rabbits | Medium | 30 | 16.67 / 16.71 / 16.82 / 17.0 | 1.62 / 2.03 | 0.52 | 0.35 | 0.014 | 0.251 | 0.063 | 153 (24) | 0 | 90 | 20 | 127382 | 229 |
| P2-Mix 20 animals, 10 rabbits + 10 squirrels | Medium | 20 | 16.67 / 16.71 / 16.88 / 17.3 | 1.48 / 1.81 | 0.49 | 0.29 | 0.012 | 0.221 | 0.046 | 33 (2) | 0 | 78 | 18 | 103774 | 228 |
| S50-Mix 50 animals, 25 rabbits + 25 squirrels | Medium | 50 | 16.67 / 16.70 / 16.77 / 17.0 | 1.64 / 1.95 | 0.48 | 0.60 | 0.016 | 0.315 | 0.045 | 33 (2) | 0 | 126 | 18 | 212619 | 229 |
| P2-Mix3 20 animals, rabbits, squirrels and hedgehogs | Medium | 20 | 16.67 / 16.79 / 16.91 / 17.3 | 1.56 / 1.96 | 0.50 | 0.27 | 0.013 | 0.229 | 0.049 | 33 (2) | 0 | 78 | 18 | 94602 | 228 |
| S50-Mix3 50 animals, rabbits, squirrels and hedgehogs | Medium | 50 | 16.67 / 16.70 / 16.79 / 16.9 | 1.65 / 2.07 | 0.49 | 0.51 | 0.017 | 0.319 | 0.046 | 33 (2) | 0 | 124 | 18 | 185955 | 228 |

Budgets Target 60 FPS (Performance Test Matrix) sur le palier cible (Medium, 20 à 30 animaux A) :

| Scénario | Main (< 8, idéal 6) | GPU (< 11) | Simulation (< 1,5) | UI (< 0,7) | GC 0 o/frame |
|---|---|---|---|---|---|
| P0 | OK (idéal) | OK (idéal) | OK | OK | 1 frames |
| P2 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P5 | OK (idéal) | OK (idéal) | OK | OK | 24 frames |
| P2-Mix | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50-Mix | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| P2-Mix3 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |
| S50-Mix3 | OK (idéal) | OK (idéal) | OK | OK | 2 frames |

## Évolution : T23 (lapins + écureuils) → T24 (trois espèces)

| Scénario | Batches | SetPass | Main moy (ms) | Render moy (ms) | Frames avec GC | Visibles |
|---|---|---|---|---|---|---|
| P0 | 40 → 40 | 15 → 15 | 1.14 → 1.09 | 0.47 → 0.46 | 1 → 1 | 0 → 0 |
| P2 | 78 → 78 | 18 → 18 | 1.59 → 1.57 | 0.49 → 0.52 | 2 → 2 | 20 → 20 |
| S50 | 124 → 124 | 18 → 18 | 1.64 → 1.71 | 0.47 → 0.49 | 2 → 2 | 50 → 50 |
| P5 | 90 → 90 | 20 → 20 | 1.55 → 1.62 | 0.49 → 0.52 | 20 → 24 | 30 → 30 |
| P2-Mix | 78 → 78 | 18 → 18 | 1.49 → 1.48 | 0.50 → 0.49 | 2 → 2 | 20 → 20 |
| S50-Mix | 126 → 126 | 18 → 18 | 1.68 → 1.64 | 0.47 → 0.48 | 2 → 2 | 50 → 50 |

