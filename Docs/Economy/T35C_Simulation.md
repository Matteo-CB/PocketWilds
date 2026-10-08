# T35C : simulation de l'économie

Rapport écrit par le simulateur (menu Pocket Wilds/Economy/Run Simulator, test EconomySimulatorTests), avec les valeurs des docs telles que le jeu les a. 6 profils sur 120 jours en 3,45 s. Garde-fous : 42 respectés sur 96, 54 en attente d'une décision du propriétaire, 0 hors bornes.

Analyse, contradictions entre les docs et décisions à prendre : `T35C_Decisions.md`. Données jour par jour : `T35C_Simulation.csv` (même dossier).

## Garde-fous

| Règle | Mesuré | Résultat |
| --- | --- | --- |
| Régulier, jours 1 à 30 : les œufs entre 55 et 65 % des dépenses de Lumière | 62,8 % | respecté |
| Régulier, jours 1 à 30 : les améliorations entre 20 et 30 % des dépenses de Lumière | 36,9 % | en attente de décision |
| Régulier, jours 1 à 30 : les agrandissements entre 5 et 10 % des dépenses de Lumière | 0,0 % | en attente de décision |
| Régulier, jours 1 à 30 : l'ouverture des biomes entre 0 et 2 % des dépenses de Lumière | 0,0 % | respecté |
| Régulier, jours 1 à 30 : les décorations communes entre 3 et 5 % des dépenses de Lumière | 0,0 % | en attente de décision |
| Régulier, jours 1 à 30 : l'épicerie entre 0 et 1 % des dépenses de Lumière | 0,3 % | respecté |
| Régulier : Forêt après 30 à 60 min de jeu cumulé | 30 min (jour 1,6) | respecté |
| Régulier : Lac au jour 2 (1,0 à 3,0) | jour 2,4 | respecté |
| Régulier : Montagne au jour 4 à 5 (3,0 à 6,0) | jour 22,4 | en attente de décision |
| Régulier : Neige au jour 8 (7,0 à 9,0) | jour 48,9 | en attente de décision |
| Régulier : Jungle au jour 12 (9,6 à 14,4) | jour 109,9 | en attente de décision |
| Régulier : Désert au jour 16 (12,8 à 19,2) | jamais | en attente de décision |
| Régulier : Sakura au jour 21 (16,8 à 25,2) | jamais | en attente de décision |
| Régulier : Champignons au jour 27 (21,6 à 32,4) | jamais | en attente de décision |
| Régulier : Îles des nuages au jour 34 (27,2 à 40,8) | jamais | en attente de décision |
| Régulier : Lunaire au jour 42 (33,6 à 50,4) | jamais | en attente de décision |
| Régulier : Préhistorique au jour 51 (40,8 à 61,2) | jamais | en attente de décision |
| Régulier : Ciel étoilé au jour 60 (48,0 à 72,0) | jamais | en attente de décision |
| Occasionnel : Forêt au jour 1 à 2 (0,0 à 3,0) | jour 2,5 | respecté |
| Occasionnel : Lac au jour 3 à 4 (2,0 à 5,0) | jour 2,5 | respecté |
| Occasionnel : Montagne au jour 6 à 8 (5,0 à 9,0) | jour 46,9 | en attente de décision |
| Occasionnel : Neige au jour 12 (9,6 à 14,4) | jour 89,8 | en attente de décision |
| Occasionnel : Jungle au jour 18 (14,4 à 21,6) | jamais | en attente de décision |
| Occasionnel : jamais la Forêt avant 30 min de jeu | 32 min | respecté |
| Occasionnel : Lac jamais avant le jour 2 | jour 2,5 | respecté |
| Occasionnel : Montagne jamais avant le jour 3 | jour 46,9 | respecté |
| Occasionnel : Neige jamais avant le jour 6 | jour 89,8 | respecté |
| Occasionnel : Jungle jamais avant le jour 9 | jamais | respecté |
| Régulier : jamais la Forêt avant 30 min de jeu | 30 min | respecté |
| Régulier : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Régulier : Montagne jamais avant le jour 3 | jour 22,4 | respecté |
| Régulier : Neige jamais avant le jour 6 | jour 48,9 | respecté |
| Régulier : Jungle jamais avant le jour 9 | jour 109,9 | respecté |
| Passionné : jamais la Forêt avant 30 min de jeu | 30 min | respecté |
| Passionné : Lac jamais avant le jour 2 | jour 2,3 | respecté |
| Passionné : Montagne jamais avant le jour 3 | jour 19,3 | respecté |
| Passionné : Neige jamais avant le jour 6 | jour 41,7 | respecté |
| Passionné : Jungle jamais avant le jour 9 | jour 100,3 | respecté |
| Payeur Club : jamais la Forêt avant 30 min de jeu | 31 min | respecté |
| Payeur Club : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club : Montagne jamais avant le jour 3 | jour 15,9 | respecté |
| Payeur Club : Neige jamais avant le jour 6 | jour 41,5 | respecté |
| Payeur Club : Jungle jamais avant le jour 9 | jour 93,5 | respecté |
| Payeur Club + Pass + packs : jamais la Forêt avant 30 min de jeu | 30 min | respecté |
| Payeur Club + Pass + packs : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club + Pass + packs : Montagne jamais avant le jour 3 | jour 17,9 | respecté |
| Payeur Club + Pass + packs : Neige jamais avant le jour 6 | jour 36,9 | respecté |
| Payeur Club + Pass + packs : Jungle jamais avant le jour 9 | jour 77,4 | respecté |
| Grand collectionneur : jamais la Forêt avant 30 min de jeu | 30 min | respecté |
| Grand collectionneur : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Grand collectionneur : Montagne jamais avant le jour 3 | jour 21,4 | respecté |
| Grand collectionneur : Neige jamais avant le jour 6 | jour 45,4 | respecté |
| Grand collectionneur : Jungle jamais avant le jour 9 | jour 107,5 | respecté |
| Régulier, jours 1 à 7 : un œuf ne coûte jamais plus de 15 min de production | 438,4 min au plus | en attente de décision |
| Régulier, ensuite : jamais plus de 45 min | 1106,2 min au plus | en attente de décision |
| Régulier : 40 à 80 éclosions le jour 1 | 309 | en attente de décision |
| Régulier : 30 à 60 éclosions par jour jusqu'au jour 7 | 4 à 175 | en attente de décision |
| Régulier : 20 à 50 éclosions par jour ensuite | 0 à 1255 | en attente de décision |
| Régulier : deux zones de la Prairie dans la première heure | 2 min | respecté |
| Régulier : la sixième entre les jours 5 et 10 | pas encore | en attente de décision |
| Régulier : niveau 4 entre 30 et 60 min | 1 min | en attente de décision |
| Régulier : niveau 9 au jour 2 | jour 1,6 | en attente de décision |
| Régulier : niveau 15 entre les jours 4 et 5 | jour 22,4 | en attente de décision |
| Régulier : premier Renouveau entre les jours 5 et 7 | jour 22,4 | en attente de décision |
| Régulier : 20 à 40 Essence liée à la Lumière au premier Renouveau | 55 Essence (en tout 113) | en attente de décision |
| Occasionnel : premier Renouveau entre les jours 8 et 10 | jour 46,9 | en attente de décision |
| Régulier : ensuite un Renouveau environ tous les 4 à 7 jours | 10 Renouveaux, écarts de 5,0 à 24,4 jours | en attente de décision |
| Régulier : Codex Prairie complet (variantes de base) entre les jours 12 et 16 | jour 7 | en attente de décision |
| Occasionnel : Codex Prairie complet entre les jours 18 et 25 | jour 7 | en attente de décision |
| Régulier : variantes de base des 24 espèces du lancement entre les jours 100 et 120 | jour 27 | en attente de décision |
| Occasionnel : variantes de base des 24 espèces au jour 150 ou plus | jour 55 | en attente de décision |
| Club + Pass : variantes de base des 24 espèces entre les jours 80 et 95 | jour 18 | en attente de décision |
| Régulier : maîtrise 30 sur une espèce entre les jours 35 et 45 | pas dans les 120 jours | en attente de décision |
| Occasionnel : maîtrise 30 au jour 60 ou plus | pas dans les 120 jours | respecté |
| Occasionnel, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 11 jour(s) de suite (jusqu'au jour 30) | en attente de décision |
| Occasionnel, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 10 jour(s) de suite (jusqu'au jour 87) | en attente de décision |
| Occasionnel, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 9 jour(s) de suite (jusqu'au jour 24) | en attente de décision |
| Régulier, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 30) | en attente de décision |
| Régulier, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 4 jour(s) de suite (jusqu'au jour 37) | en attente de décision |
| Régulier, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 8 jour(s) de suite (jusqu'au jour 87) | en attente de décision |
| Passionné, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 10) | en attente de décision |
| Passionné, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 11 jour(s) de suite (jusqu'au jour 97) | en attente de décision |
| Passionné, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 7 jour(s) de suite (jusqu'au jour 89) | en attente de décision |
| Payeur Club, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 28) | en attente de décision |
| Payeur Club, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 6 jour(s) de suite (jusqu'au jour 36) | en attente de décision |
| Payeur Club, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 7 jour(s) de suite (jusqu'au jour 83) | en attente de décision |
| Payeur Club + Pass + packs, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 10) | en attente de décision |
| Payeur Club + Pass + packs, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 7 jour(s) de suite (jusqu'au jour 67) | en attente de décision |
| Payeur Club + Pass + packs, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 9 jour(s) de suite (jusqu'au jour 119) | en attente de décision |
| Grand collectionneur, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 29) | en attente de décision |
| Grand collectionneur, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 5 jour(s) de suite (jusqu'au jour 95) | en attente de décision |
| Grand collectionneur, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 9 jour(s) de suite (jusqu'au jour 105) | en attente de décision |
| Régulier gratuit : Gemmes non dépensées sous 1 500 au jour 30 | 200 | respecté |
| Club : environ deux fois les Gemmes d'un régulier gratuit (1,6 à 2,5) | × 1,80 | respecté |
| Club + Pass : collection 20 à 25 % plus rapide, jamais plus | 33 % | en attente de décision |
| 6 profils sur 120 jours en moins de 10 s | 3,45 s | respecté |

En attente de décision mais respectés aujourd'hui (à retirer de la liste) : A1.share.eggs.

## Décisions en attente

- Décision 1 : les formules (œuf 10 × 1,18^n par type d'œuf, 1 Lumière par seconde et par animal, hors ligne à 100 % pendant 24 h) donnent 3 à 10 fois plus d'éclosions que les bandes ; même les réglages les plus lents permis en donnent 112 le jour 1, et les premiers niveaux du refuge tombent dans la première minute. (6 garde-fous hors bornes)
- Décision 2 : le Renouveau remet les compteurs d'œufs à zéro (doc Renouveau, section 1.4) : des centaines d'éclosions le jour d'un Renouveau ; sans cette remise, les œufs deviennent trop chers et les autres jours n'en ont presque plus. (1 garde-fou hors bornes)
- Décision 3 : agrandissements et décorations coûtent des multiples fixes de B, quand œufs et améliorations suivent la courbe : moins de 0,1 % des dépenses après le jour 1 ; la part œufs et améliorations dépend du joueur (guidé ou optimiseur). (3 garde-fous hors bornes)
- Décision 4 : le niveau 15 (918 points) demande presque tout le contenu des trois premiers biomes, puis chaque porte demande plus de points et de maîtrise de biome qu'un biome n'en apporte : les biomes, le Renouveau et la sixième zone arrivent trop tard ou jamais. (18 garde-fous hors bornes)
- Décision 5 : 20 à 40 Essence au premier Renouveau ; le cadeau de 20 et le bonus de complétion dépassent déjà 40, le simulateur lit la part liée à la Lumière, qui dépend aussi du jour du premier Renouveau (décision 4). (1 garde-fou hors bornes)
- Décision 6 : la maîtrise 30 demande environ 24 000 XP sur une espèce (20 × L^1,35, 2 XP par doublon), soit 12 000 doublons ; le régulier atteint la maîtrise 8 au jour 120. (1 garde-fou hors bornes)
- Décision 7 : au rythme d'éclosion des formules et avec les garanties de nouveauté, les Codex se complètent bien avant les cibles, et l'écart du payeur se mesure sur des dates hors cibles (suit la décision 1). (6 garde-fous hors bornes)
- Décision 8 : quand les portes se bloquent, plus rien de nouveau ne s'ouvre : des jours sans découverte ni objectif (suit la décision 4). (18 garde-fous hors bornes)

## Profils

| Profil | Jeu par jour (j. 2 à 120) | Éclosions | Découvertes | Biomes au jour 30 / 120 | Niveau du refuge au jour 30 / 120 | Renouveaux | Gemmes gagnées / dépensées |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 11 min | 8870 | 285 | 3 / 5 | 14 / 26 | 7 | 4057 / 3850 |
| Régulier | 32 min | 13148 | 387 | 4 / 6 | 19 / 33 | 10 | 6418 / 6300 |
| Passionné | 80 min | 12940 | 371 | 4 / 6 | 19 / 33 | 10 | 6680 / 6650 |
| Payeur Club | 32 min | 14454 | 378 | 4 / 6 | 20 / 33 | 11 | 11547 / 11550 |
| Payeur Club + Pass + packs | 32 min | 15817 | 402 | 4 / 6 | 20 / 35 | 11 | 15450 / 14350 |
| Grand collectionneur | 32 min | 13716 | 395 | 4 / 6 | 19 / 33 | 10 | 15882 / 15750 |

## Ouverture des biomes (jour, minutes de jeu cumulées)

| Biome | Occasionnel | Régulier | Passionné | Payeur Club | Payeur Club + Pass + packs | Grand collectionneur |
| --- | --- | --- | --- | --- | --- | --- |
| Prairie | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Forêt | j. 2,5 (32 min) | j. 1,6 (30 min) | j. 1,5 (30 min) | j. 1,6 (31 min) | j. 1,6 (30 min) | j. 1,6 (30 min) |
| Lac | j. 2,5 (35 min) | j. 2,4 (55 min) | j. 2,3 (110 min) | j. 2,4 (55 min) | j. 2,4 (55 min) | j. 2,4 (55 min) |
| Montagne | j. 46,9 (497 min) | j. 22,4 (695 min) | j. 19,3 (1470 min) | j. 15,9 (495 min) | j. 17,9 (559 min) | j. 21,4 (663 min) |
| Neige | j. 89,8 (946 min) | j. 48,9 (1558 min) | j. 41,7 (3278 min) | j. 41,5 (1311 min) | j. 36,9 (1167 min) | j. 45,4 (1433 min) |
| Jungle | pas dans les 120 jours | j. 109,9 (3503 min) | j. 100,3 (7954 min) | j. 93,5 (2975 min) | j. 77,4 (2455 min) | j. 107,5 (3423 min) |
| Désert | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Sakura | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Champignons | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Îles des nuages | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Lunaire | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Préhistorique | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |
| Ciel étoilé | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours | pas dans les 120 jours |

## Journal du joueur régulier, jours 1 à 30

Ce qui retient la porte du biome suivant à la fin de chaque jour (niveau du refuge, espèces du biome précédent, maîtrise, jour plancher, Lumière en pourcentage du prix).

| Jour | Éclosions | Découvertes | Niveau (score) | Biomes | Renouveau | Œuf le plus cher (min de production) | Porte suivante |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 309 | 84 | 10 (502) | 2 |  | 10 | Lac : plancher dans 1 j. |
| 2 | 175 | 43 | 13 (776) | 3 |  | 14 | Montagne : niveau 13/15, maîtrise 5/6, plancher dans 1 j. |
| 3 | 38 | 2 | 13 (787) | 3 |  | 61 | Montagne : niveau 13/15, maîtrise 5/6 |
| 4 | 14 | 0 | 13 (787) | 3 |  | 192 | Montagne : niveau 13/15, maîtrise 5/6 |
| 5 | 6 | 0 | 13 (789) | 3 |  | 315 | Montagne : niveau 13/15, maîtrise 5/6 |
| 6 | 4 | 1 | 13 (793) | 3 |  | 438 | Montagne : niveau 13/15, maîtrise 5/6 |
| 7 | 83 | 2 | 13 (813) | 3 |  | 44 | Montagne : niveau 13/15 |
| 8 | 31 | 1 | 13 (817) | 3 |  | 117 | Montagne : niveau 13/15 |
| 9 | 12 | 0 | 13 (819) | 3 |  | 191 | Montagne : niveau 13/15 |
| 10 | 11 | 1 | 14 (830) | 3 |  | 260 | Montagne : niveau 14/15 |
| 11 | 6 | 1 | 14 (835) | 3 |  | 341 | Montagne : niveau 14/15 |
| 12 | 6 | 0 | 14 (835) | 3 |  | 397 | Montagne : niveau 14/15 |
| 13 | 4 | 0 | 14 (838) | 3 |  | 448 | Montagne : niveau 14/15 |
| 14 | 4 | 1 | 14 (858) | 3 |  | 601 | Montagne : niveau 14/15 |
| 15 | 7 | 1 | 14 (869) | 3 |  | 562 | Montagne : niveau 14/15 |
| 16 | 2 | 0 | 14 (870) | 3 |  | 669 | Montagne : niveau 14/15 |
| 17 | 2 | 1 | 14 (875) | 3 |  | 783 | Montagne : niveau 14/15 |
| 18 | 6 | 0 | 14 (876) | 3 |  | 916 | Montagne : niveau 14/15 |
| 19 | 2 | 0 | 14 (911) | 3 |  | 854 | Montagne : niveau 14/15 |
| 20 | 4 | 1 | 14 (915) | 3 |  | 799 | Montagne : niveau 14/15 |
| 21 | 2 | 0 | 14 (916) | 3 |  | 0 | Montagne : niveau 14/15 |
| 22 | 142 | 40 | 17 (1184) | 4 | oui | 2 | Neige : niveau 17/22, maîtrise 5/8 |
| 23 | 50 | 3 | 17 (1194) | 4 |  | 107 | Neige : niveau 17/22, maîtrise 5/8 |
| 24 | 11 | 0 | 17 (1201) | 4 |  | 235 | Neige : niveau 17/22, maîtrise 6/8 |
| 25 | 5 | 1 | 17 (1210) | 4 |  | 321 | Neige : niveau 17/22, maîtrise 6/8 |
| 26 | 5 | 1 | 17 (1216) | 4 |  | 487 | Neige : niveau 17/22, maîtrise 6/8 |
| 27 | 787 | 9 | 18 (1295) | 4 | oui | 12 | Neige : niveau 18/22, maîtrise 7/8 |
| 28 | 27 | 0 | 19 (1327) | 4 |  | 17 | Neige : niveau 19/22, maîtrise 7/8 |
| 29 | 87 | 0 | 19 (1339) | 4 |  | 52 | Neige : niveau 19/22, maîtrise 7/8 |
| 30 | 39 | 0 | 19 (1339) | 4 |  | 131 | Neige : niveau 19/22, maîtrise 7/8 |

## Dépenses de Lumière, jours 1 à 30

| Profil | Œufs | Améliorations | Zones | Biomes | Décorations | Épicerie |
| --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 76,4 % | 23,2 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Régulier | 62,8 % | 36,9 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Passionné | 44,6 % | 55,1 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club | 39,1 % | 60,5 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club + Pass + packs | 37,1 % | 62,6 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Grand collectionneur | 40,9 % | 58,7 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Régulier, joueur optimiseur | 6,7 % | 93,0 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |

## Sensibilité : le joueur optimiseur

Même profil régulier, mais chaque achat est celui qui se rembourse le plus vite (sans suivre les conseils du jeu). L'écart avec le joueur guidé montre ce qui dépend du comportement du joueur et ce qui dépend des formules.

| Mesure | Joueur guidé | Joueur optimiseur |
| --- | --- | --- |
| Éclosions le jour 1 | 309 | 270 |
| Éclosions sur 120 jours | 13148 | 12838 |
| Œufs, part des dépenses (j. 1 à 30) | 62,8 % | 6,7 % |
| Améliorations, part des dépenses | 36,9 % | 93,0 % |
| Ouverture : Montagne | jour 22,4 | jour 23,5 |
| Ouverture : Neige | jour 48,9 | jour 48,4 |
| Ouverture : Jungle | jour 109,9 | jour 110,4 |
| Ouverture : Désert | pas dans les 120 jours | pas dans les 120 jours |
| Premier Renouveau | jour 22,4 | jour 23,5 |
| Niveau du refuge au jour 120 | 33 | 33 |

## Renouveaux

| Profil | Jour et Essence de chaque Renouveau (dont liée à la Lumière) |
| --- | --- |
| Occasionnel | j. 46,9 (142, dont 84) ; j. 56,5 (72, dont 61) ; j. 63,8 (110, dont 110) ; j. 74,5 (171, dont 171) ; j. 88,5 (261, dont 261) ; j. 103,8 (381, dont 370) ; j. 114,5 (581, dont 581) |
| Régulier | j. 22,4 (113, dont 55) ; j. 27,4 (57, dont 46) ; j. 33,4 (91, dont 91) ; j. 39,5 (132, dont 132) ; j. 48,9 (200, dont 200) ; j. 57,9 (304, dont 293) ; j. 67,5 (459, dont 459) ; j. 76,4 (697, dont 697) ; j. 91,4 (1036, dont 1036) ; j. 115,8 (1551, dont 1540) |
| Passionné | j. 19,3 (111, dont 53) ; j. 23,5 (56, dont 45) ; j. 28,5 (84, dont 84) ; j. 34,3 (129, dont 129) ; j. 41,6 (193, dont 193) ; j. 48,8 (289, dont 278) ; j. 57,3 (439, dont 439) ; j. 64,7 (658, dont 658) ; j. 77,3 (1002, dont 1002) ; j. 100,3 (1494, dont 1494) |
| Payeur Club | j. 15,9 (103, dont 45) ; j. 19,8 (52, dont 41) ; j. 24,5 (78, dont 78) ; j. 29,5 (118, dont 118) ; j. 37,5 (180, dont 180) ; j. 45,5 (268, dont 257) ; j. 53,5 (407, dont 407) ; j. 60,4 (627, dont 627) ; j. 71,5 (921, dont 921) ; j. 92,5 (1387, dont 1387) ; j. 118,5 (2078, dont 2067) |
| Payeur Club + Pass + packs | j. 17,9 (108, dont 50) ; j. 21,5 (54, dont 43) ; j. 25,9 (81, dont 81) ; j. 31,4 (131, dont 131) ; j. 37,8 (194, dont 183) ; j. 43,4 (296, dont 296) ; j. 50,5 (446, dont 446) ; j. 56,9 (658, dont 658) ; j. 68,5 (991, dont 991) ; j. 85,9 (1495, dont 1484) ; j. 106,4 (2264, dont 2264) |
| Grand collectionneur | j. 21,4 (112, dont 54) ; j. 26,4 (58, dont 47) ; j. 31,4 (86, dont 86) ; j. 37,4 (135, dont 135) ; j. 45,4 (202, dont 202) ; j. 56,8 (300, dont 289) ; j. 66,4 (461, dont 461) ; j. 75,4 (701, dont 701) ; j. 90,4 (1040, dont 1040) ; j. 114,4 (1577, dont 1566) |

## Ce que le simulateur suppose

- Contenu : les définitions livrées (Prairie : 3 espèces, 18 variantes, œufs Basic, Focus et Découverte), complétées par le roster prévu (6 espèces par biome, 6 variantes de base, 13 biomes, zones de la section 4.2). Les espèces prévues ne sont jamais livrées.
- Jours : le jour calendaire des docs (le jour de l'installation est le jour 1, installation à midi) ; 1,5 veut dire midi du jour 1.
- Joueur guidé, le même pour tous les profils : il éclot surtout ; il prend l'œuf qui cache encore une nouveauté (le moins cher), sinon celui qui produit le plus pour son prix ; une amélioration quand le HUD la conseille (au moins 5 % de production totale, ou le palier × 2 que la Lumière atteint) ; une zone dès que son panneau s'allume ; Nids douillets et Regard curieux quand ils coûtent moins de 10 et 30 minutes de production ; quand l'œuf voulu coûte plus de 10 minutes de production, il ouvre l'écran Améliorer et prend la carte « Conseillé » (le plus court temps de retour). Il garde sa Lumière pour une porte de biome dont les autres conditions sont remplies, achète les décorations communes et les friandises bon marché, éclot les œufs Découverte tout de suite, par 1, 3 ou 10 selon le niveau du refuge.
- Renouveau : le premier dès que le bouton s'active, les suivants quand ils ajoutent au moins la moitié de l'Essence déjà gagnée (« rentable ») ; Essence au nœud ouvert le moins cher, branche Économie d'abord.
- Rendez-vous : cadeau du jour (table et garantie), calendrier de 28 cases, souhaits, carnet de la semaine, activités (minutes de production), expéditions (minutes de production, œufs Découverte et garantie), un événement par mois (7 objets), Pass de 30 jours.
- Systèmes prévus mais pas encore construits, comptés comme leurs docs le disent : traits aux taux du doc Formules, visiteurs rares, autocollants et pages d'album, décorations posées, variantes de maîtrise (niveaux 10, 20, 30), objets d'événement et couleurs du Pass (3 points chacun), souhaits (maîtrise du biome).
- Pas encore simulés : la reproduction (T56) et les 5 % de Lumière du compagnon de pack.
- La clé econ.egg.growth du doc Améliorations (section 15) n'est pas encore lue par le jeu : chaque œuf garde son costGrowth (1,18). Le simulateur l'applique à tous les œufs pour les essais.
