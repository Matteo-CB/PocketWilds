# T23 Squirrel, mesures

Deuxième espèce, construite sur le squelette, les clips, le matériau et le pipeline du lapin.
Mesures : même runner que T22 (`PerformanceBaselineRunner`, vrai démarrage, vraie scène, vraie sauvegarde),
player Windows de développement, 480x1040, éditeur au repos pendant la mesure.
Rapport brut : `Baselines/T23_player_windows_mixed.json`. Tableaux complets : `T23_Squirrel_tables.md`.
Aucun téléphone branché : ces chiffres servent à la comparaison relative, pas aux budgets mobiles.

## Nouveaux scénarios

- `P2-Mix` : 20 animaux, 10 lapins + 10 écureuils (6 palettes chacun), Medium.
- `S50-Mix` : 50 animaux, 25 + 25, Medium.
- Filtre `-pw-scenarios P2,P2-Mix` pour ne lancer qu'une partie.

## Coût incrémental de l'écureuil

| Scénario | Main moy (ms) | GPU moy (ms) | Batches | SetPass | Triangles | Mémoire (Mo) |
|---|---|---|---|---|---|---|
| P2, 20 lapins | 1,59 | 0,29 | 78 | 18 | 98 400 | 225,5 |
| P2-Mix, 10 + 10 | 1,49 | 0,29 | 78 | 18 | 103 800 | 226,1 |
| S50, 50 lapins | 1,64 | 0,55 | 124 | 18 | 195 400 | 226,4 |
| S50-Mix, 25 + 25 | 1,68 | 0,60 | 126 | 18 | 212 600 | 226,9 |

- Matériau unique partagé, aucun clone, aucun MaterialPropertyBlock (test `SixVariants_OnOnePrefab_DistinctData_NoMaterialClone`).
- +2 batches à 50 animaux (deux maillages skinnés au lieu d'un), SetPass inchangé.
- Triangles +5 % (20) et +9 % (50) : LOD0 écureuil 2 398 contre 2 110, grande queue.
- Mémoire : +1,5 Mo par rapport à T22 pour le pool préchauffé de la seconde espèce.

## GC

- Régime stable identique à T22 : 2 frames avec allocation sur 8 s (~32 o/frame en moyenne).
- Rafale de 10 éclosions (P5) : 9 → 20 frames avec allocation (~5 Ko chacune). La rafale fait maintenant
  éclore des écureuils inédits : chaque découverte déclenche une sauvegarde prioritaire, coût événementiel
  déjà noté en T22. À attribuer précisément avec `AllocationProbe` avant le gate T30.

## Fiabilité des mesures (corrigée pendant T23)

- `FrameTimingManager` renvoie parfois une valeur absurde (6,9 s dans un run de 841 frames en 14,0 s) :
  l'échantillonneur rejette désormais un timing contredit par l'horloge murale (`rejectedTimings`).
- Les déchets des scénarios précédents (un processus, plusieurs démarrages) sont ramassés avant la chauffe.
- Le player quitte même si le rapport ne peut pas être écrit.
- Mesurer avec l'éditeur Unity au repos : juste après un build Windows, l'éditeur repasse sur Android et
  recompile, ce qui a produit des trous de plusieurs secondes dans deux runs.

## Pools

Le refuge enregistre un pool et un profil de comportement par espèce ayant un visuel, et préchauffe
le reste du budget visible pour chaque espèce que l'œuf peut donner (en parallèle, environ deux par frame
au total). Un hatch d'écureuil ne crée aucune instance (test `SquirrelPlayTests`). Au-delà de trois ou quatre
espèces par œuf, passer à une réserve fixe par espèce plus réapprovisionnement en frames calmes.
