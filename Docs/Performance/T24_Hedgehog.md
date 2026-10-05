# T24 Hedgehog, mesures

Troisième espèce, sur le même squelette SmallQuadruped, le même contrôleur de famille et le même matériau.
Même runner que T22 et T23, player Windows de développement, 480x1040, éditeur Unity au repos.
Rapport brut : `Baselines/T24_player_windows_three_species.json`. Tableaux : `T24_Hedgehog_tables.md`.
Aucun téléphone branché : comparaison relative uniquement.

## Trois espèces dans la prairie

| Scénario | Main moy (ms) | GPU moy (ms) | Batches | SetPass | Triangles | Mémoire (Mo) |
|---|---|---|---|---|---|---|
| P2, 20 lapins | 1,57 | 0,34 | 78 | 18 | 98 400 | 227,9 |
| P2-Mix3, 20 animaux (3 espèces) | 1,56 | 0,27 | 78 | 18 | 94 600 | 228,4 |
| S50, 50 lapins | 1,71 | 0,55 | 124 | 18 | 195 400 | 228,3 |
| S50-Mix3, 50 animaux (3 espèces) | 1,65 | 0,51 | 124 | 18 | 186 000 | 228,4 |

- Un seul matériau, aucun clone, aucun MaterialPropertyBlock (test `SixVariants_OnOnePrefab_DistinctData_NoMaterialClone`).
- La carapace est dans le même mesh : batches et SetPass identiques au lapin seul.
- Mémoire : +2,4 Mo depuis T22 pour les pools préchauffés de l'écureuil et du hérisson.
- Régime stable : 2 frames avec allocation sur 8 s, comme T22 et T23.
- Rafale d'éclosions (P5) : 24 frames avec allocation (20 en T23). Il y a maintenant deux espèces à découvrir
  dans la rafale. Coût événementiel à attribuer avec `AllocationProbe` avant le gate T30.

## Carapace

QuadriFlow lissait les 30 lobes en une carapace froissée. Le corps est remaillé sans eux, puis chaque lobe
est une géométrie basse résolution propre (34 / 18 / 4 triangles selon le LOD), rigide sur le poids du
corps sous sa base : aucun cisaillement en pose, aucun matériau ni sous-mesh en plus.

## Animation

Deux clips de famille ajoutés au rig partagé (doc RIG-001 : marche optionnelle des espèces basses) :
`Locomotion_Walk` (0,8 s, boucle) et `Happy_Bounce` (1 s, même durée que `Happy`). Le hérisson les reçoit par
un `AnimatorOverrideController` du contrôleur de famille (données, aucun code runtime). Vitesse de
déplacement du profil : 0,28 m/s au lieu de 0,38.
