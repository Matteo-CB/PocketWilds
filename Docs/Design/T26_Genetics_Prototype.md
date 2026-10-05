# T26 Génétique, prototype

`GeneticsService.TryBreed(parentA, parentB, contextSeed)` (Game.Simulation) donne le génome d'un enfant.
Pur et déterministe : aucun objet Unity, aucune horloge, aucun hasard global. L'ordre des parents ne change
rien. `Preview` calcule exactement les chances que `TryBreed` tire (vérifié sur 200 000 reproductions).

## Génome

Stocké dans `AnimalInstance.TraitBits`, déjà sauvegardé : aucun changement de format de sauvegarde.
0 = animal ordinaire, donc tous les animaux existants gardent leur apparence.

| Bits | Contenu |
|---|---|
| 0-7 | Traits : Tiny, Giant, Shiny, Golden, Albino, Melanistic |
| 8-11 | Gène de taille, -6 à +6, 2,5 % par pas (Tiny x0,8, Giant x1,2) |
| 12-14 | Motif porté (canal de masque) |
| 15-18 | Intensité du motif porté |

## Table d'héritage (`GeneticsConfig`, valeurs de départ à équilibrer)

| Règle | Valeur |
|---|---|
| Deux parents de même variante : l'enfant la garde | 90 % |
| Variantes différentes : chaque parent transmet la sienne | 42 % chacun |
| Mutation (ni l'une ni l'autre, jamais une variante secrète) | le reste, rareté voisine du parent le plus rare : -2 5 %, -1 25 %, 0 40 %, +1 25 %, +2 4 %, +3 1 % |
| Motif porté : deux parents porteurs / un / aucun | 75 % / 40 % / 3 % |
| Trait : deux parents / un / aucun | 60 % / 25 % / 1 % à 0,2 % selon le trait |
| Exclusions | Tiny ou Giant, Albino ou Melanistic |
| Taille | moyenne des parents, puis -1, 0 ou +1 (25 / 50 / 25 %) |
| Délai de l'œuf (pour l'intégration) | 300 s |

Les motifs autorisés d'une espèce sont ceux de ses propres variantes (doc 03 : associations autorisées).
Une palette sans motif montre un motif porté ton sur ton (couleur de motif dérivée de la couleur principale).

## Rendu

`AnimalVisualResolver` applique la taille et le motif porté sans allocation. Albino, Melanistic, Golden et
Shiny sont hérités en données ; leur rendu (palettes dédiées, reflet) viendra avec la fonction de reproduction
dans le refuge.

Capture : `TestResults/Captures/T26_genetics_families.png`.
