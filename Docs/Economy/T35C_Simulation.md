# T35C : simulation de l'économie

Rapport écrit par le simulateur (menu Pocket Wilds/Economy/Run Simulator, test EconomySimulatorTests), avec les valeurs des docs telles que le jeu les a. 6 profils sur 120 jours en 5,48 s. Garde-fous : 79 respectés sur 94, 15 en attente d'une décision du propriétaire, 0 hors bornes.

Analyse, contradictions entre les docs et décisions à prendre : `T35C_Decisions.md`. Données jour par jour : `T35C_Simulation.csv` (même dossier).

## Garde-fous

| Règle | Mesuré | Résultat |
| --- | --- | --- |
| Régulier, jours 1 à 30 : les œufs entre 55 et 65 % des dépenses de Lumière | 54,9 % | en attente de décision |
| Régulier, jours 1 à 30 : les améliorations entre 20 et 45 % des dépenses de Lumière | 44,7 % | respecté |
| Régulier, jours 1 à 30 : l'ouverture des biomes entre 0 et 2 % des dépenses de Lumière | 0,0 % | respecté |
| Régulier, jours 1 à 30 : l'épicerie entre 0 et 1 % des dépenses de Lumière | 0,4 % | respecté |
| Régulier : Forêt après 30 à 60 min de jeu cumulé | 35 min (jour 1,8) | respecté |
| Régulier : Lac au jour 2 (1,0 à 3,0) | jour 2,4 | respecté |
| Régulier : Montagne au jour 4 à 5 (3,0 à 6,0) | jour 5,4 | respecté |
| Régulier : Neige au jour 8 (7,0 à 9,0) | jour 7,4 | respecté |
| Régulier : Jungle au jour 12 (9,6 à 14,4) | jour 12,8 | respecté |
| Régulier : Désert au jour 16 (12,8 à 19,2) | jour 18,8 | respecté |
| Régulier : Sakura au jour 21 (16,8 à 25,2) | jour 22,9 | respecté |
| Régulier : Champignons au jour 27 (21,6 à 32,4) | jour 27,5 | respecté |
| Régulier : Îles des nuages au jour 34 (27,2 à 40,8) | jour 33,4 | respecté |
| Régulier : Lunaire au jour 42 (33,6 à 50,4) | jour 37,4 | respecté |
| Régulier : Préhistorique au jour 51 (40,8 à 61,2) | jour 45,4 | respecté |
| Régulier : Ciel étoilé au jour 60 (48,0 à 72,0) | jour 54,4 | respecté |
| Occasionnel : Forêt au jour 1 à 2 (0,0 à 3,0) | jour 2,9 | respecté |
| Occasionnel : Lac au jour 3 à 4 (2,0 à 5,0) | jour 2,9 | respecté |
| Occasionnel : Montagne au jour 6 à 8 (5,0 à 9,0) | jour 5,8 | respecté |
| Occasionnel : Neige au jour 12 (9,6 à 14,4) | jour 9,8 | respecté |
| Occasionnel : Jungle au jour 18 (14,4 à 21,6) | jour 17,8 | respecté |
| Occasionnel : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Occasionnel : Lac jamais avant le jour 2 | jour 2,9 | respecté |
| Occasionnel : Montagne jamais avant le jour 3 | jour 5,8 | respecté |
| Occasionnel : Neige jamais avant le jour 6 | jour 9,8 | respecté |
| Occasionnel : Jungle jamais avant le jour 9 | jour 17,8 | respecté |
| Régulier : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Régulier : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Régulier : Montagne jamais avant le jour 3 | jour 5,4 | respecté |
| Régulier : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Régulier : Jungle jamais avant le jour 9 | jour 12,8 | respecté |
| Passionné : jamais la Forêt avant 30 min de jeu | 31 min | respecté |
| Passionné : Lac jamais avant le jour 2 | jour 2,3 | respecté |
| Passionné : Montagne jamais avant le jour 3 | jour 4,3 | respecté |
| Passionné : Neige jamais avant le jour 6 | jour 6,6 | respecté |
| Passionné : Jungle jamais avant le jour 9 | jour 11,3 | respecté |
| Payeur Club : jamais la Forêt avant 30 min de jeu | 34 min | respecté |
| Payeur Club : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club : Montagne jamais avant le jour 3 | jour 4,5 | respecté |
| Payeur Club : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club : Jungle jamais avant le jour 9 | jour 10,5 | respecté |
| Payeur Club + Pass + packs : jamais la Forêt avant 30 min de jeu | 33 min | respecté |
| Payeur Club + Pass + packs : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club + Pass + packs : Montagne jamais avant le jour 3 | jour 4,5 | respecté |
| Payeur Club + Pass + packs : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club + Pass + packs : Jungle jamais avant le jour 9 | jour 13,4 | respecté |
| Grand collectionneur : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Grand collectionneur : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Grand collectionneur : Montagne jamais avant le jour 3 | jour 4,8 | respecté |
| Grand collectionneur : Neige jamais avant le jour 6 | jour 7,5 | respecté |
| Grand collectionneur : Jungle jamais avant le jour 9 | jour 12,8 | respecté |
| Régulier, jours 1 à 7 : un œuf ne coûte jamais plus de 15 min de production | 15,0 min au plus | respecté |
| Régulier, ensuite : jamais plus de 45 min | 45,0 min au plus | respecté |
| Régulier : 40 à 80 éclosions le jour 1 | 72 | respecté |
| Régulier : 30 à 60 éclosions par jour jusqu'au jour 7, hors Renouveau, nouveau biome et lendemain | 39 à 39 | respecté |
| Régulier : 20 à 50 éclosions par jour ensuite, hors Renouveau, nouveau biome et lendemain | 21 à 535 | en attente de décision |
| Régulier : deux zones de la Prairie dans la première heure | 2 min | respecté |
| Régulier : la sixième entre les jours 5 et 10 | jour 5,4 | respecté |
| Régulier : niveau 4 entre 30 et 60 min | 3 min | en attente de décision |
| Régulier : niveau 9 au jour 2 | jour 1,9 | en attente de décision |
| Régulier : niveau 15 entre les jours 4 et 5 | jour 5,4 | respecté |
| Régulier : premier Renouveau entre les jours 5 et 7 | jour 6,4 | respecté |
| Régulier : 20 à 40 Essence liée à la Lumière au premier Renouveau | 21 Essence (en tout 90) | respecté |
| Occasionnel : premier Renouveau entre les jours 8 et 10 | jour 6,5 | en attente de décision |
| Régulier : ensuite un Renouveau environ tous les 4 à 7 jours | 25 Renouveaux, écarts de 1,5 à 18,1 jours | en attente de décision |
| Régulier : Codex Prairie complet (variantes de base) entre les jours 12 et 16 | jour 9 | en attente de décision |
| Occasionnel : Codex Prairie complet entre les jours 18 et 25 | jour 34 | en attente de décision |
| Régulier : variantes de base des 24 espèces du lancement entre les jours 100 et 120 | jour 33 | en attente de décision |
| Occasionnel : variantes de base des 24 espèces au jour 150 ou plus | jour 40 | en attente de décision |
| Club + Pass : variantes de base des 24 espèces entre les jours 80 et 95 | jour 21 | en attente de décision |
| Régulier : maîtrise 30 sur une espèce entre les jours 35 et 45 | jour 28 | en attente de décision |
| Occasionnel : maîtrise 30 au jour 60 ou plus | jour 77 | respecté |
| Occasionnel, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 13) | respecté |
| Occasionnel, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 85) | respecté |
| Occasionnel, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 1 jour(s) de suite (jusqu'au jour 16) | respecté |
| Régulier, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Régulier, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 62) | respecté |
| Régulier, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 1 jour(s) de suite (jusqu'au jour 69) | respecté |
| Passionné, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Passionné, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 93) | respecté |
| Passionné, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 94) | respecté |
| Payeur Club, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Payeur Club, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 67) | respecté |
| Payeur Club, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 84) | respecté |
| Payeur Club + Pass + packs, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 24) | respecté |
| Payeur Club + Pass + packs, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 93) | respecté |
| Payeur Club + Pass + packs, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 3 jour(s) de suite (jusqu'au jour 89) | en attente de décision |
| Grand collectionneur, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 25) | en attente de décision |
| Grand collectionneur, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 32) | respecté |
| Grand collectionneur, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 88) | respecté |
| Régulier gratuit : Gemmes non dépensées sous 1 500 au jour 30 | 293 | respecté |
| Club : environ deux fois les Gemmes d'un régulier gratuit (1,6 à 2,5) | × 1,62 | respecté |
| Club + Pass : collection 20 à 25 % plus rapide, jamais plus | 36 % | en attente de décision |
| 6 profils sur 120 jours en moins de 10 s | 5,48 s | respecté |

## Décisions en attente

- Point 1 : à la limite basse de la bande ; la part des œufs dépend surtout du comportement du joueur (joueur guidé autour de 55 %, optimiseur sous 10 %) : à mesurer avec les vrais joueurs du soft launch. (1 garde-fou hors bornes)
- Point 2 : les jours qui suivent un Renouveau, les œufs des anciens biomes repartent de leur prix de base ; un joueur qui vise une couleur de maîtrise en fait éclore des centaines. Une « pluie d'œufs » voulue, ou un prix plancher en minutes de production si le propriétaire préfère un rythme régulier. (1 garde-fou hors bornes)
- Point 3 : voulu, les premiers niveaux du refuge arrivent vite (accueil, fonctions débloquées tôt) ; la Forêt garde son plancher de 30 min de jeu et le Lac celui du jour 2. (2 garde-fous hors bornes)
- Point 4 : le hors-ligne payé 24 h à 100 % fait avancer l'occasionnel presque comme le régulier en Lumière ; ses biomes tiennent leurs cibles, son premier Renouveau arrive au jour 6 à 7. (1 garde-fou hors bornes)
- Point 5 : la Lumière produite croît par paliers (biomes, Renouveaux), l'Essence la suit : des Renouveaux rentables tous les 1,5 à 18 jours selon la période, environ tous les 5 jours en moyenne. (1 garde-fou hors bornes)
- Point 6 : avec les garanties de nouveauté et les œufs Découverte, les couleurs de base se trouvent bien avant les cibles des Codex (100 à 120 jours pour le lancement) : il faudrait des couleurs plus rares, une décision de contenu. (6 garde-fous hors bornes)
- Point 7 : le joueur vise la couleur de maîtrise la plus proche, son espèce préférée atteint la maîtrise 30 vers le jour 30 (35 à 45 visé). (1 garde-fou hors bornes)
- Point 8 : un jour de plus que la limite, sur un seul profil ; ces écarts bougent d'un réglage à l'autre. (2 garde-fous hors bornes)

## Profils

| Profil | Jeu par jour (j. 2 à 120) | Éclosions | Découvertes | Biomes au jour 30 / 120 | Niveau du refuge au jour 30 / 120 | Renouveaux | Gemmes gagnées / dépensées |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 11 min | 6101 | 761 | 7 / 13 | 43 / 85 | 21 | 5960 / 6300 |
| Régulier | 32 min | 11020 | 917 | 9 / 13 | 61 / 102 | 25 | 8239 / 8400 |
| Passionné | 80 min | 10587 | 904 | 9 / 13 | 60 / 103 | 25 | 8506 / 8750 |
| Payeur Club | 32 min | 11081 | 908 | 9 / 13 | 61 / 102 | 24 | 13369 / 13650 |
| Payeur Club + Pass + packs | 32 min | 8359 | 869 | 8 / 13 | 52 / 97 | 24 | 17190 / 16450 |
| Grand collectionneur | 32 min | 10382 | 911 | 8 / 13 | 52 / 101 | 24 | 17834 / 18000 |

## Ouverture des biomes (jour, minutes de jeu cumulées)

| Biome | Occasionnel | Régulier | Passionné | Payeur Club | Payeur Club + Pass + packs | Grand collectionneur |
| --- | --- | --- | --- | --- | --- | --- |
| Prairie | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ |
| Forêt | j. 2,9 (35 min) | j. 1,8 (35 min) | j. 1,5 (31 min) | j. 1,6 (34 min) | j. 1,6 (33 min) | j. 1,8 (35 min) |
| Lac | j. 2,9 (36 min) | j. 2,4 (55 min) | j. 2,3 (110 min) | j. 2,4 (55 min) | j. 2,4 (55 min) | j. 2,4 (55 min) |
| Montagne | j. 5,8 (63 min) | j. 5,4 (151 min) | j. 4,3 (270 min) | j. 4,5 (127 min) | j. 4,5 (127 min) | j. 4,8 (135 min) |
| Neige | j. 9,8 (106 min) | j. 7,4 (215 min) | j. 6,6 (461 min) | j. 7,4 (215 min) | j. 7,4 (215 min) | j. 7,5 (223 min) |
| Jungle | j. 17,8 (192 min) | j. 12,8 (391 min) | j. 11,3 (830 min) | j. 10,5 (319 min) | j. 13,4 (408 min) | j. 12,8 (392 min) |
| Désert | j. 28,5 (301 min) | j. 18,8 (583 min) | j. 17,6 (1340 min) | j. 15,8 (490 min) | j. 19,8 (615 min) | j. 19,9 (623 min) |
| Sakura | j. 36,5 (388 min) | j. 22,9 (719 min) | j. 22,3 (1710 min) | j. 20,5 (641 min) | j. 26,5 (833 min) | j. 26,4 (826 min) |
| Champignons | j. 54,5 (576 min) | j. 27,5 (863 min) | j. 27,3 (2114 min) | j. 25,5 (799 min) | j. 36,4 (1143 min) | j. 33,9 (1071 min) |
| Îles des nuages | j. 86,5 (910 min) | j. 33,4 (1047 min) | j. 33,3 (2596 min) | j. 32,4 (1020 min) | j. 47,5 (1503 min) | j. 46,9 (1487 min) |
| Lunaire | j. 87,8 (924 min) | j. 37,4 (1175 min) | j. 37,3 (2910 min) | j. 37,4 (1175 min) | j. 48,8 (1543 min) | j. 47,8 (1512 min) |
| Préhistorique | j. 95,8 (1008 min) | j. 45,4 (1431 min) | j. 45,3 (3550 min) | j. 45,4 (1431 min) | j. 52,8 (1671 min) | j. 49,8 (1581 min) |
| Ciel étoilé | j. 105,8 (1113 min) | j. 54,4 (1719 min) | j. 54,3 (4270 min) | j. 54,4 (1719 min) | j. 57,8 (1832 min) | j. 54,4 (1719 min) |

## Journal du joueur régulier, jours 1 à 30

Ce qui retient la porte du biome suivant à la fin de chaque jour (niveau du refuge, espèces du biome précédent, maîtrise, jour plancher, Lumière en pourcentage du prix).

| Jour | Éclosions | Découvertes | Niveau (score) | Biomes | Renouveau | Œuf le plus cher (min de production) | Porte suivante |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 72 | 48 | 9 (500) | 2 |  | 15 | Lac : plancher dans 1 j. |
| 2 | 82 | 31 | 13 (813) | 3 |  | 15 | Montagne : niveau 13/15, plancher dans 1 j. |
| 3 | 108 | 14 | 14 (884) | 3 |  | 15 | Montagne : niveau 14/15 |
| 4 | 39 | 2 | 14 (896) | 3 |  | 15 | Montagne : niveau 14/15 |
| 5 | 112 | 27 | 19 (1243) | 4 |  | 15 | Neige : niveau 19/22, maîtrise 7/8, plancher dans 1 j. |
| 6 | 168 | 22 | 21 (1374) | 4 | oui | 2 | Neige : niveau 21/22 |
| 7 | 238 | 53 | 26 (1796) | 5 | oui | 15 | Jungle : niveau 26/30, maîtrise 7/9, plancher dans 2 j. |
| 8 | 94 | 13 | 27 (1868) | 5 |  | 9 | Jungle : niveau 27/30, maîtrise 8/9, plancher dans 1 j. |
| 9 | 179 | 13 | 28 (1940) | 5 | oui | 45 | Jungle : niveau 28/30, maîtrise 8/9 |
| 10 | 85 | 10 | 29 (2006) | 5 |  | 45 | Jungle : niveau 29/30 |
| 11 | 41 | 4 | 29 (2022) | 5 |  | 45 | Jungle : niveau 29/30 |
| 12 | 255 | 40 | 34 (2394) | 6 | oui | 45 | Désert : niveau 34/38, maîtrise 7/10, plancher dans 1 j. |
| 13 | 85 | 4 | 35 (2437) | 6 |  | 45 | Désert : niveau 35/38, maîtrise 8/10 |
| 14 | 230 | 13 | 36 (2539) | 6 | oui | 45 | Désert : niveau 36/38, maîtrise 9/10 |
| 15 | 88 | 4 | 36 (2563) | 6 |  | 45 | Désert : niveau 36/38, maîtrise 9/10 |
| 16 | 32 | 2 | 36 (2575) | 6 |  | 45 | Désert : niveau 36/38, maîtrise 9/10 |
| 17 | 263 | 7 | 37 (2635) | 6 | oui | 29 | Désert : niveau 37/38 |
| 18 | 84 | 30 | 41 (2962) | 7 |  | 45 | Sakura : niveau 41/46, maîtrise 8/10 |
| 19 | 282 | 14 | 42 (3048) | 7 | oui | 45 | Sakura : niveau 42/46, maîtrise 9/10 |
| 20 | 103 | 11 | 43 (3108) | 7 |  | 45 | Sakura : niveau 43/46, maîtrise 9/10 |
| 21 | 306 | 18 | 45 (3254) | 7 | oui | 45 | Sakura : niveau 45/46 |
| 22 | 116 | 31 | 49 (3595) | 8 |  | 45 | Champignons : niveau 49/54, maîtrise 9/10, plancher dans 2 j. |
| 23 | 354 | 16 | 51 (3717) | 8 | oui | 45 | Champignons : niveau 51/54, plancher dans 1 j. |
| 24 | 133 | 11 | 52 (3788) | 8 |  | 45 | Champignons : niveau 52/54 |
| 25 | 347 | 12 | 53 (3895) | 8 | oui | 45 | Champignons : niveau 53/54 |
| 26 | 121 | 4 | 53 (3929) | 8 |  | 45 | Champignons : niveau 53/54 |
| 27 | 85 | 26 | 57 (4245) | 9 |  | 45 | Îles des nuages : niveau 57/62, maîtrise 9/10, plancher dans 3 j. |
| 28 | 450 | 23 | 59 (4396) | 9 | oui | 1 | Îles des nuages : niveau 59/62, plancher dans 2 j. |
| 29 | 100 | 7 | 60 (4447) | 9 |  | 45 | Îles des nuages : niveau 60/62, plancher dans 1 j. |
| 30 | 443 | 14 | 61 (4555) | 9 | oui | 45 | Îles des nuages : niveau 61/62 |

## Dépenses de Lumière, jours 1 à 30

| Profil | Œufs | Améliorations | Zones | Biomes | Décorations | Épicerie |
| --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 36,6 % | 63,1 % | 0,0 % | 0,0 % | 0,0 % | 0,2 % |
| Régulier | 54,9 % | 44,7 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Passionné | 64,6 % | 35,1 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club | 62,5 % | 37,2 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club + Pass + packs | 76,4 % | 23,3 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Grand collectionneur | 69,0 % | 30,6 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Régulier, joueur optimiseur | 5,2 % | 94,5 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |

## Sensibilité : le joueur optimiseur

Même profil régulier, mais chaque achat est celui qui se rembourse le plus vite (sans suivre les conseils du jeu). L'écart avec le joueur guidé montre ce qui dépend du comportement du joueur et ce qui dépend des formules.

| Mesure | Joueur guidé | Joueur optimiseur |
| --- | --- | --- |
| Éclosions le jour 1 | 72 | 51 |
| Éclosions sur 120 jours | 11020 | 12864 |
| Œufs, part des dépenses (j. 1 à 30) | 54,9 % | 5,2 % |
| Améliorations, part des dépenses | 44,7 % | 94,5 % |
| Ouverture : Montagne | jour 5,4 | jour 17,4 |
| Ouverture : Neige | jour 7,4 | jour 24,4 |
| Ouverture : Jungle | jour 12,8 | jour 38,8 |
| Ouverture : Désert | jour 18,8 | jour 51,4 |
| Premier Renouveau | jour 6,4 | jour 17,5 |
| Niveau du refuge au jour 120 | 102 | 105 |

## Renouveaux

| Profil | Jour et Essence de chaque Renouveau (dont liée à la Lumière) |
| --- | --- |
| Occasionnel | j. 6,5 (87, dont 18) ; j. 9,8 (47, dont 47) ; j. 12,5 (73, dont 62) ; j. 17,8 (120, dont 120) ; j. 21,8 (194, dont 183) ; j. 25,8 (314, dont 314) ; j. 29,8 (431, dont 420) ; j. 32,5 (800, dont 800) ; j. 36,5 (1176, dont 1176) ; j. 40,5 (1676, dont 1665) ; j. 45,8 (2867, dont 2867) ; j. 53,8 (3910, dont 3910) ; j. 60,5 (6631, dont 6620) ; j. 69,8 (9722, dont 9722) ; j. 80,5 (15102, dont 15100) ; j. 90,9 (22868, dont 22836) ; j. 96,9 (36695, dont 36684) ; j. 100,5 (53633, dont 53633) ; j. 106,5 (90534, dont 90523) ; j. 110,5 (143448, dont 143448) ; j. 116,5 (221774, dont 221774) |
| Régulier | j. 6,4 (90, dont 21) ; j. 7,9 (54, dont 43) ; j. 9,9 (78, dont 78) ; j. 12,8 (122, dont 111) ; j. 14,8 (187, dont 187) ; j. 17,4 (276, dont 276) ; j. 19,9 (440, dont 429) ; j. 21,8 (664, dont 664) ; j. 23,9 (1068, dont 1057) ; j. 25,9 (1685, dont 1685) ; j. 28,4 (2750, dont 2739) ; j. 30,5 (3760, dont 3758) ; j. 33,5 (5755, dont 5742) ; j. 35,9 (8945, dont 8945) ; j. 38,4 (13804, dont 13793) ; j. 40,5 (20903, dont 20903) ; j. 43,9 (31801, dont 31799) ; j. 47,5 (46410, dont 46395) ; j. 51,5 (71634, dont 71634) ; j. 55,8 (112768, dont 112757) ; j. 59,5 (170837, dont 170837) ; j. 65,4 (259258, dont 259258) ; j. 73,9 (381862, dont 381840) ; j. 85,8 (573579, dont 573559) ; j. 103,9 (858205, dont 858173) |
| Passionné | j. 6,3 (100, dont 31) ; j. 7,6 (55, dont 44) ; j. 9,7 (84, dont 84) ; j. 11,8 (128, dont 117) ; j. 13,6 (190, dont 190) ; j. 15,8 (287, dont 287) ; j. 18,5 (436, dont 425) ; j. 20,3 (728, dont 728) ; j. 22,6 (1059, dont 1048) ; j. 24,5 (1631, dont 1631) ; j. 27,3 (2382, dont 2382) ; j. 29,8 (3659, dont 3648) ; j. 33,3 (5789, dont 5789) ; j. 36,4 (8467, dont 8452) ; j. 38,3 (12641, dont 12630) ; j. 40,3 (19534, dont 19534) ; j. 43,3 (30480, dont 30480) ; j. 47,3 (48255, dont 48244) ; j. 51,4 (69616, dont 69616) ; j. 55,5 (106805, dont 106794) ; j. 59,3 (174282, dont 174282) ; j. 64,5 (247612, dont 247608) ; j. 73,5 (369688, dont 369668) ; j. 90,6 (554193, dont 554163) ; j. 115,7 (829783, dont 829751) |
| Payeur Club | j. 6,4 (98, dont 24) ; j. 7,8 (49, dont 38) ; j. 9,5 (83, dont 83) ; j. 11,4 (130, dont 119) ; j. 13,4 (208, dont 208) ; j. 15,8 (303, dont 303) ; j. 17,9 (478, dont 467) ; j. 19,9 (763, dont 763) ; j. 21,9 (1184, dont 1173) ; j. 23,9 (1669, dont 1669) ; j. 26,4 (2837, dont 2826) ; j. 28,8 (4276, dont 4276) ; j. 32,4 (6577, dont 6577) ; j. 35,8 (9365, dont 9354) ; j. 38,5 (14957, dont 14946) ; j. 41,4 (23344, dont 23334) ; j. 45,4 (34908, dont 34906) ; j. 49,4 (53492, dont 53481) ; j. 54,5 (86577, dont 86566) ; j. 57,4 (132426, dont 132426) ; j. 61,5 (195893, dont 195893) ; j. 68,4 (292997, dont 292997) ; j. 79,4 (440855, dont 440851) ; j. 100,5 (656055, dont 656009) |
| Payeur Club + Pass + packs | j. 6,4 (99, dont 30) ; j. 7,9 (57, dont 46) ; j. 9,9 (84, dont 84) ; j. 13,4 (127, dont 127) ; j. 15,8 (201, dont 190) ; j. 18,9 (294, dont 294) ; j. 21,4 (474, dont 463) ; j. 23,4 (731, dont 731) ; j. 26,4 (1149, dont 1149) ; j. 29,4 (1708, dont 1697) ; j. 32,8 (2484, dont 2484) ; j. 37,4 (3798, dont 3787) ; j. 40,8 (5917, dont 5917) ; j. 46,4 (9116, dont 9116) ; j. 49,8 (13707, dont 13685) ; j. 52,4 (23432, dont 23432) ; j. 54,5 (36065, dont 36054) ; j. 57,4 (57778, dont 57778) ; j. 59,5 (84282, dont 84271) ; j. 62,4 (123688, dont 123688) ; j. 66,4 (194747, dont 194747) ; j. 72,5 (279999, dont 279995) ; j. 82,4 (435757, dont 435755) ; j. 102,9 (639968, dont 639950) |
| Grand collectionneur | j. 6,4 (92, dont 23) ; j. 7,9 (51, dont 40) ; j. 9,9 (78, dont 78) ; j. 12,8 (113, dont 113) ; j. 15,4 (189, dont 178) ; j. 18,4 (270, dont 270) ; j. 21,4 (475, dont 464) ; j. 23,4 (706, dont 706) ; j. 26,4 (1108, dont 1108) ; j. 29,5 (1652, dont 1639) ; j. 33,5 (2467, dont 2465) ; j. 37,4 (3816, dont 3799) ; j. 41,5 (5687, dont 5685) ; j. 47,5 (8484, dont 8473) ; j. 49,8 (13527, dont 13516) ; j. 51,4 (26348, dont 26337) ; j. 52,9 (37348, dont 37348) ; j. 54,8 (56950, dont 56937) ; j. 56,5 (89112, dont 89104) ; j. 59,4 (143869, dont 143867) ; j. 63,8 (202533, dont 202533) ; j. 70,5 (301658, dont 301656) ; j. 82,4 (450353, dont 450339) ; j. 110,8 (675663, dont 675631) |

## Ce que le simulateur suppose

- Contenu : les définitions livrées (Prairie : 3 espèces, 18 variantes, œufs Basic, Focus et Découverte), complétées par le roster prévu (6 espèces par biome, 6 variantes de base, 13 biomes, zones de la section 4.2). Les espèces prévues ne sont jamais livrées.
- Jours : le jour calendaire des docs (le jour de l'installation est le jour 1, installation à midi) ; 1,5 veut dire midi du jour 1.
- Joueur guidé, le même pour tous les profils : il éclot surtout ; il prend l'œuf qui cache encore une nouveauté (le moins cher), sinon celui du biome dont une espèce est la plus proche de sa prochaine couleur de maîtrise, sinon l'œuf Basic du biome le plus récent ; une amélioration quand le HUD la conseille (au moins 5 % de production totale, ou le palier × 2 que la Lumière atteint) ; une zone dès que son panneau s'allume ; Nids douillets et Regard curieux quand ils coûtent moins de 10 et 30 minutes de production ; quand le prochain œuf est à plus de 10 minutes, il ouvre l'écran Améliorer et prend la carte « Conseillé » (le plus court temps de retour). Il garde sa Lumière pour une porte de biome dont les autres conditions sont remplies, achète les décorations communes et les friandises bon marché, éclot les œufs Découverte tout de suite, par 1, 3 ou 10 selon le niveau du refuge.
- Renouveau : le premier dès que le bouton s'active, les suivants quand ils ajoutent au moins la moitié de l'Essence déjà gagnée (« rentable ») ; Essence au nœud ouvert le moins cher, branche Économie d'abord.
- Rendez-vous : cadeau du jour (table et garantie), calendrier de 28 cases, souhaits, carnet de la semaine, activités (minutes de production), expéditions (minutes de production, œufs Découverte et garantie), un événement par mois (7 objets), Pass de 30 jours.
- Systèmes prévus mais pas encore construits, comptés comme leurs docs le disent : traits aux taux du doc Formules, visiteurs rares, autocollants et pages d'album, décorations posées, variantes de maîtrise (niveaux 10, 20, 30), objets d'événement et couleurs du Pass (3 points chacun), souhaits (maîtrise du biome).
- Pas encore simulés : la reproduction (T56) et les 5 % de Lumière du compagnon de pack.
- Les Gemmes d'un palier de maîtrise d'espèce (2) sont comptées ici ; le jeu les donnera avec l'écran des maîtrises.
