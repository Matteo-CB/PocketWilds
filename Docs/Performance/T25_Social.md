# T25 Social behavior, mesures

Rencontres (Social) et points d'intérêt (Inspect, Eat) dans le scheduler central, sans NavMeshAgent.
Player Windows de développement, 480x1040, éditeur au repos. Rapport brut : `Baselines/T25_player_windows_social.json`.

## Coût

| Scénario | Simulation moy (ms) T24 → T25 | Simulation p95 (ms) | Main moy (ms) T24 → T25 |
|---|---|---|---|
| P2, 20 lapins | 0,017 → 0,025 | 0,019 | 1,57 → 1,43 |
| P2-Mix3, 20 animaux, 3 espèces | 0,013 → 0,013 | 0,017 | 1,56 → 1,42 |
| S50, 50 lapins | 0,016 → 0,017 | 0,023 | 1,71 → 1,58 |
| S50-Mix3, 50 animaux, 3 espèces | 0,017 → 0,018 | 0,024 | 1,65 → 1,63 |

Budget simulation de la Performance Guide : 1,5 ms. Aucune allocation par frame (test
`Tick_WithGreetingsAndPoints_DoesNotAllocate`), aucun pic de décisions synchronisées (tests T12 inchangés).

## Comportement

- Rencontre : un animal de tier A choisit Social (poids du profil x tempérament). Il invite le voisin libre le
  plus proche du même groupe (rayon 2,2 m, voisin au repos), qui accepte selon son tempérament (Shy 35 %,
  Playful 95 %). Le scheduler réserve les deux animaux et un marqueur à deux ancres sur la ligne qui les relie,
  `socialGap` apart (0,42 m, hérisson 0,46 m). Chacun marche vers son ancre ; le premier arrivé attend en se
  tournant vers l'autre (3 s maximum), puis les deux jouent `Social` dans la même frame, face à face.
  Ensuite, parfois `Happy`. Une caresse, un départ ou un délai dépassé libère proprement l'autre animal.
- Inspect et Eat : marche jusqu'à un point d'intérêt réservé (un animal par point), arrêt à `standOff` du point,
  rotation sur place pour lui faire face. Si quelqu'un occupe déjà l'emplacement, l'activité se fait sur place.
- Le tier B (arrière-plan) ne fait ni rencontre ni inspection.
- Points d'intérêt du chunk de départ : 7, générés depuis la disposition déterministe des props
  (`Pocket Wilds/Environment/Update Chunk Points of Interest`).
