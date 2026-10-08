# T35C : simulation de l'économie

Rapport écrit par le simulateur (menu Pocket Wilds/Economy/Run Simulator, test EconomySimulatorTests), avec les valeurs des docs telles que le jeu les a. 6 profils sur 120 jours en 5,86 s. Garde-fous : 81 respectés sur 94, 13 en attente d'une décision du propriétaire, 0 hors bornes.

Analyse, contradictions entre les docs et décisions à prendre : `T35C_Decisions.md`. Données jour par jour : `T35C_Simulation.csv` (même dossier).

## Garde-fous

| Règle | Mesuré | Résultat |
| --- | --- | --- |
| Régulier, jours 1 à 30 : les œufs entre 55 et 65 % des dépenses de Lumière | 62,0 % | respecté |
| Régulier, jours 1 à 30 : les améliorations entre 20 et 45 % des dépenses de Lumière | 37,7 % | respecté |
| Régulier, jours 1 à 30 : l'ouverture des biomes entre 0 et 2 % des dépenses de Lumière | 0,0 % | respecté |
| Régulier, jours 1 à 30 : l'épicerie entre 0 et 1 % des dépenses de Lumière | 0,4 % | respecté |
| Régulier : Forêt après 30 à 60 min de jeu cumulé | 35 min (jour 1,8) | respecté |
| Régulier : Lac au jour 2 (1,0 à 3,0) | jour 2,4 | respecté |
| Régulier : Montagne au jour 4 à 5 (3,0 à 6,0) | jour 4,9 | respecté |
| Régulier : Neige au jour 8 (7,0 à 9,0) | jour 8,4 | respecté |
| Régulier : Jungle au jour 12 (9,6 à 14,4) | jour 10,8 | respecté |
| Régulier : Désert au jour 16 (12,8 à 19,2) | jour 17,5 | respecté |
| Régulier : Sakura au jour 21 (16,8 à 25,2) | jour 21,5 | respecté |
| Régulier : Champignons au jour 27 (21,6 à 32,4) | jour 27,8 | respecté |
| Régulier : Îles des nuages au jour 34 (27,2 à 40,8) | jour 36,8 | respecté |
| Régulier : Lunaire au jour 42 (33,6 à 50,4) | jour 38,9 | respecté |
| Régulier : Préhistorique au jour 51 (40,8 à 61,2) | jour 45,4 | respecté |
| Régulier : Ciel étoilé au jour 60 (48,0 à 72,0) | jour 54,4 | respecté |
| Occasionnel : Forêt au jour 1 à 2 (0,0 à 3,0) | jour 2,9 | respecté |
| Occasionnel : Lac au jour 3 à 4 (2,0 à 5,0) | jour 2,9 | respecté |
| Occasionnel : Montagne au jour 6 à 8 (5,0 à 9,0) | jour 5,8 | respecté |
| Occasionnel : Neige au jour 12 (9,6 à 14,4) | jour 10,5 | respecté |
| Occasionnel : Jungle au jour 18 (14,4 à 21,6) | jour 19,8 | respecté |
| Occasionnel : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Occasionnel : Lac jamais avant le jour 2 | jour 2,9 | respecté |
| Occasionnel : Montagne jamais avant le jour 3 | jour 5,8 | respecté |
| Occasionnel : Neige jamais avant le jour 6 | jour 10,5 | respecté |
| Occasionnel : Jungle jamais avant le jour 9 | jour 19,8 | respecté |
| Régulier : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Régulier : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Régulier : Montagne jamais avant le jour 3 | jour 4,9 | respecté |
| Régulier : Neige jamais avant le jour 6 | jour 8,4 | respecté |
| Régulier : Jungle jamais avant le jour 9 | jour 10,8 | respecté |
| Passionné : jamais la Forêt avant 30 min de jeu | 31 min | respecté |
| Passionné : Lac jamais avant le jour 2 | jour 2,3 | respecté |
| Passionné : Montagne jamais avant le jour 3 | jour 4,3 | respecté |
| Passionné : Neige jamais avant le jour 6 | jour 6,6 | respecté |
| Passionné : Jungle jamais avant le jour 9 | jour 12,5 | respecté |
| Payeur Club : jamais la Forêt avant 30 min de jeu | 34 min | respecté |
| Payeur Club : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club : Montagne jamais avant le jour 3 | jour 4,9 | respecté |
| Payeur Club : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club : Jungle jamais avant le jour 9 | jour 10,4 | respecté |
| Payeur Club + Pass + packs : jamais la Forêt avant 30 min de jeu | 33 min | respecté |
| Payeur Club + Pass + packs : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club + Pass + packs : Montagne jamais avant le jour 3 | jour 4,5 | respecté |
| Payeur Club + Pass + packs : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club + Pass + packs : Jungle jamais avant le jour 9 | jour 11,4 | respecté |
| Grand collectionneur : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Grand collectionneur : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Grand collectionneur : Montagne jamais avant le jour 3 | jour 4,8 | respecté |
| Grand collectionneur : Neige jamais avant le jour 6 | jour 8,4 | respecté |
| Grand collectionneur : Jungle jamais avant le jour 9 | jour 10,9 | respecté |
| Régulier, jours 1 à 7 : un œuf ne coûte jamais plus de 15 min de production | 15,0 min au plus | respecté |
| Régulier, ensuite : jamais plus de 45 min | 45,0 min au plus | respecté |
| Régulier : 40 à 80 éclosions le jour 1 | 72 | respecté |
| Régulier : 30 à 60 éclosions par jour jusqu'au jour 7, hors Renouveau, nouveau biome et lendemain | aucun jour ordinaire | respecté |
| Régulier : 20 à 50 éclosions par jour ensuite, hors Renouveau, nouveau biome et lendemain | 15 à 147 | en attente de décision |
| Régulier : deux zones de la Prairie dans la première heure | 2 min | respecté |
| Régulier : la sixième entre les jours 5 et 10 | jour 5,4 | respecté |
| Régulier : niveau 4 entre 30 et 60 min | 3 min | en attente de décision |
| Régulier : niveau 9 au jour 2 | jour 1,9 | en attente de décision |
| Régulier : niveau 15 entre les jours 4 et 5 | jour 4,9 | respecté |
| Régulier : premier Renouveau entre les jours 5 et 7 | jour 6,4 | respecté |
| Régulier : 20 à 40 Essence liée à la Lumière au premier Renouveau | 22 Essence (en tout 91) | respecté |
| Occasionnel : premier Renouveau entre les jours 8 et 10 | jour 6,5 | en attente de décision |
| Régulier : ensuite un Renouveau environ tous les 4 à 7 jours | 25 Renouveaux, écarts de 1,8 à 20,0 jours | en attente de décision |
| Régulier : Codex Prairie complet (variantes de base) entre les jours 12 et 16 | jour 8 | en attente de décision |
| Occasionnel : Codex Prairie complet entre les jours 18 et 25 | jour 13 | en attente de décision |
| Régulier : variantes de base des 24 espèces du lancement entre les jours 100 et 120 | jour 34 | en attente de décision |
| Occasionnel : variantes de base des 24 espèces au jour 150 ou plus | jour 55 | en attente de décision |
| Club + Pass : variantes de base des 24 espèces entre les jours 80 et 95 | jour 28 | en attente de décision |
| Régulier : maîtrise 30 sur une espèce entre les jours 35 et 45 | jour 58 | en attente de décision |
| Occasionnel : maîtrise 30 au jour 60 ou plus | jour 54 | en attente de décision |
| Occasionnel, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 3) | respecté |
| Occasionnel, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 107) | respecté |
| Occasionnel, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 1 jour(s) de suite (jusqu'au jour 29) | respecté |
| Régulier, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 9) | respecté |
| Régulier, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 120) | respecté |
| Régulier, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 97) | respecté |
| Passionné, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 10) | respecté |
| Passionné, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 120) | respecté |
| Passionné, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 85) | respecté |
| Payeur Club, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 25) | respecté |
| Payeur Club, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 4 jour(s) de suite (jusqu'au jour 120) | en attente de décision |
| Payeur Club, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 119) | respecté |
| Payeur Club + Pass + packs, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Payeur Club + Pass + packs, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 40) | respecté |
| Payeur Club + Pass + packs, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 1 jour(s) de suite (jusqu'au jour 32) | respecté |
| Grand collectionneur, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 30) | respecté |
| Grand collectionneur, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 3 jour(s) de suite (jusqu'au jour 119) | respecté |
| Grand collectionneur, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 80) | respecté |
| Régulier gratuit : Gemmes non dépensées sous 1 500 au jour 30 | 260 | respecté |
| Club : environ deux fois les Gemmes d'un régulier gratuit (1,6 à 2,5) | × 1,64 | respecté |
| Club + Pass : collection 20 à 25 % plus rapide, jamais plus | 18 % | respecté |
| 6 profils sur 120 jours en moins de 10 s | 5,86 s | respecté |

En attente de décision mais respectés aujourd'hui (à retirer de la liste) : A1.share.eggs, G5.goals.club_pass_packs, G5.discovery.early.collector, G4.payer.speed.

## Décisions en attente

- Point 1 : à la limite basse de la bande ; la part des œufs dépend surtout du comportement du joueur (joueur guidé autour de 55 %, optimiseur sous 10 %) : à mesurer avec les vrais joueurs du soft launch. (0 garde-fou hors bornes)
- Point 2 : les jours qui suivent un Renouveau, les œufs des anciens biomes repartent de leur prix de base ; un joueur qui vise une couleur de maîtrise en fait éclore des centaines. Une « pluie d'œufs » voulue, ou un prix plancher en minutes de production si le propriétaire préfère un rythme régulier. (1 garde-fou hors bornes)
- Point 3 : voulu, les premiers niveaux du refuge arrivent vite (accueil, fonctions débloquées tôt) ; la Forêt garde son plancher de 30 min de jeu et le Lac celui du jour 2. (2 garde-fous hors bornes)
- Point 4 : le hors-ligne payé 24 h à 100 % fait avancer l'occasionnel presque comme le régulier en Lumière ; ses biomes tiennent leurs cibles, son premier Renouveau arrive au jour 6 à 7. (1 garde-fou hors bornes)
- Point 5 : la Lumière produite croît par paliers (biomes, Renouveaux), l'Essence la suit : des Renouveaux rentables tous les 1,5 à 18 jours selon la période, environ tous les 5 jours en moyenne. (1 garde-fou hors bornes)
- Point 6 : avec les garanties de nouveauté et les œufs Découverte, les couleurs de base se trouvent bien avant les cibles des Codex (100 à 120 jours pour le lancement) : il faudrait des couleurs plus rares, une décision de contenu. (5 garde-fous hors bornes)
- Point 7 : la maîtrise 30 dépend de la couleur que le joueur vise. Depuis T36, chaque souhait donne aussi 5 XP à l'espèce de l'animal (doc 08 econ.mastery_xp.wish), surtout dans le biome où il joue : le régulier répartit son XP et arrive plus tard que visé (jours 35 à 45), l'occasionnel un peu plus tôt (jour 60 visé). Réglable par Remote Config sans mise à jour. (2 garde-fous hors bornes)
- Point 8 : un jour de plus que la limite, sur un seul profil ; ces écarts bougent d'un réglage à l'autre. (1 garde-fou hors bornes)

## Profils

| Profil | Jeu par jour (j. 2 à 120) | Éclosions | Découvertes | Biomes au jour 30 / 120 | Niveau du refuge au jour 30 / 120 | Renouveaux | Gemmes gagnées / dépensées |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 11 min | 6159 | 784 | 6 / 13 | 37 / 86 | 22 | 6333 / 6650 |
| Régulier | 32 min | 9982 | 891 | 9 / 13 | 60 / 100 | 25 | 8157 / 8400 |
| Passionné | 80 min | 9395 | 870 | 8 / 13 | 53 / 99 | 26 | 8348 / 8750 |
| Payeur Club | 32 min | 11808 | 912 | 9 / 13 | 58 / 104 | 24 | 13374 / 13650 |
| Payeur Club + Pass + packs | 32 min | 9210 | 888 | 9 / 13 | 58 / 99 | 25 | 17225 / 16450 |
| Grand collectionneur | 32 min | 10791 | 908 | 9 / 13 | 61 / 102 | 25 | 17802 / 18000 |

## Ouverture des biomes (jour, minutes de jeu cumulées)

| Biome | Occasionnel | Régulier | Passionné | Payeur Club | Payeur Club + Pass + packs | Grand collectionneur |
| --- | --- | --- | --- | --- | --- | --- |
| Prairie | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ |
| Forêt | j. 2,9 (35 min) | j. 1,8 (35 min) | j. 1,5 (31 min) | j. 1,6 (34 min) | j. 1,6 (33 min) | j. 1,8 (35 min) |
| Lac | j. 2,9 (36 min) | j. 2,4 (55 min) | j. 2,3 (110 min) | j. 2,4 (55 min) | j. 2,4 (55 min) | j. 2,4 (55 min) |
| Montagne | j. 5,8 (64 min) | j. 4,9 (143 min) | j. 4,3 (270 min) | j. 4,9 (143 min) | j. 4,5 (127 min) | j. 4,8 (135 min) |
| Neige | j. 10,5 (113 min) | j. 8,4 (247 min) | j. 6,6 (460 min) | j. 7,4 (215 min) | j. 7,4 (215 min) | j. 8,4 (247 min) |
| Jungle | j. 19,8 (211 min) | j. 10,8 (329 min) | j. 12,5 (931 min) | j. 10,4 (311 min) | j. 11,4 (343 min) | j. 10,9 (337 min) |
| Désert | j. 31,8 (337 min) | j. 17,5 (543 min) | j. 19,6 (1500 min) | j. 16,5 (511 min) | j. 17,5 (550 min) | j. 16,8 (519 min) |
| Sakura | j. 42,5 (448 min) | j. 21,5 (672 min) | j. 25,8 (2010 min) | j. 23,4 (730 min) | j. 22,8 (711 min) | j. 20,4 (634 min) |
| Champignons | j. 60,5 (641 min) | j. 27,8 (871 min) | j. 35,7 (2790 min) | j. 29,8 (939 min) | j. 29,8 (939 min) | j. 24,4 (761 min) |
| Îles des nuages | j. 76,9 (816 min) | j. 36,8 (1159 min) | j. 46,4 (3640 min) | j. 37,4 (1179 min) | j. 45,8 (1447 min) | j. 31,9 (1012 min) |
| Lunaire | j. 78,9 (833 min) | j. 38,9 (1236 min) | j. 48,4 (3800 min) | j. 38,9 (1231 min) | j. 47,5 (1503 min) | j. 37,4 (1175 min) |
| Préhistorique | j. 86,5 (910 min) | j. 45,4 (1431 min) | j. 50,6 (3986 min) | j. 45,4 (1431 min) | j. 50,5 (1599 min) | j. 45,4 (1431 min) |
| Ciel étoilé | j. 97,8 (1029 min) | j. 54,4 (1719 min) | j. 54,3 (4277 min) | j. 54,4 (1719 min) | j. 54,4 (1725 min) | j. 54,4 (1719 min) |

## Journal du joueur régulier, jours 1 à 30

Ce qui retient la porte du biome suivant à la fin de chaque jour (niveau du refuge, espèces du biome précédent, maîtrise, jour plancher, Lumière en pourcentage du prix).

| Jour | Éclosions | Découvertes | Niveau (score) | Biomes | Renouveau | Œuf le plus cher (min de production) | Porte suivante |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 72 | 48 | 9 (500) | 2 |  | 15 | Lac : plancher dans 1 j. |
| 2 | 82 | 33 | 13 (823) | 3 |  | 15 | Montagne : niveau 13/15, plancher dans 1 j. |
| 3 | 106 | 13 | 14 (891) | 3 |  | 15 | Montagne : niveau 14/15 |
| 4 | 62 | 29 | 18 (1190) | 4 |  | 15 | Neige : niveau 18/22, maîtrise 7/8, plancher dans 2 j. |
| 5 | 101 | 5 | 19 (1259) | 4 |  | 15 | Neige : niveau 19/22, maîtrise 7/8, plancher dans 1 j. |
| 6 | 168 | 18 | 21 (1382) | 4 | oui | 1 | Neige : niveau 21/22 |
| 7 | 104 | 8 | 21 (1419) | 4 |  | 15 | Neige : niveau 21/22 |
| 8 | 283 | 65 | 28 (1906) | 5 | oui | 45 | Jungle : niveau 28/30, maîtrise 8/9, plancher dans 1 j. |
| 9 | 27 | 0 | 28 (1907) | 5 |  | 45 | Jungle : niveau 28/30, maîtrise 8/9 |
| 10 | 199 | 43 | 33 (2297) | 6 | oui | 45 | Désert : niveau 33/38, maîtrise 7/10, plancher dans 3 j. |
| 11 | 81 | 9 | 34 (2343) | 6 |  | 45 | Désert : niveau 34/38, maîtrise 7/10, plancher dans 2 j. |
| 12 | 226 | 11 | 35 (2451) | 6 | oui | 5 | Désert : niveau 35/38, maîtrise 8/10, plancher dans 1 j. |
| 13 | 69 | 4 | 35 (2481) | 6 |  | 45 | Désert : niveau 35/38, maîtrise 9/10 |
| 14 | 256 | 12 | 37 (2583) | 6 | oui | 16 | Désert : niveau 37/38, maîtrise 9/10 |
| 15 | 50 | 2 | 37 (2594) | 6 |  | 45 | Désert : niveau 37/38 |
| 16 | 230 | 5 | 37 (2631) | 6 | oui | 45 | Désert : niveau 37/38 |
| 17 | 130 | 30 | 42 (2976) | 7 |  | 30 | Sakura : niveau 42/46, maîtrise 8/10, plancher dans 1 j. |
| 18 | 49 | 8 | 42 (3006) | 7 |  | 45 | Sakura : niveau 42/46, maîtrise 8/10 |
| 19 | 335 | 23 | 44 (3166) | 7 | oui | 15 | Sakura : niveau 44/46 |
| 20 | 56 | 5 | 44 (3190) | 7 |  | 45 | Sakura : niveau 44/46 |
| 21 | 323 | 43 | 50 (3621) | 8 | oui | 8 | Champignons : niveau 50/54, maîtrise 9/10, plancher dans 3 j. |
| 22 | 65 | 3 | 50 (3635) | 8 |  | 45 | Champignons : niveau 50/54, maîtrise 9/10, plancher dans 2 j. |
| 23 | 267 | 21 | 51 (3775) | 8 | oui | 2 | Champignons : niveau 51/54, plancher dans 1 j. |
| 24 | 60 | 5 | 52 (3820) | 8 |  | 45 | Champignons : niveau 52/54 |
| 25 | 295 | 11 | 53 (3909) | 8 | oui | 39 | Champignons : niveau 53/54 |
| 26 | 28 | 1 | 53 (3928) | 8 |  | 45 | Champignons : niveau 53/54 |
| 27 | 78 | 29 | 57 (4246) | 9 |  | 45 | Îles des nuages : niveau 57/62, maîtrise 9/10, plancher dans 3 j. |
| 28 | 274 | 20 | 59 (4379) | 9 | oui | 1 | Îles des nuages : niveau 59/62, plancher dans 2 j. |
| 29 | 70 | 6 | 59 (4412) | 9 |  | 45 | Îles des nuages : niveau 59/62, plancher dans 1 j. |
| 30 | 232 | 8 | 60 (4487) | 9 | oui | 45 | Îles des nuages : niveau 60/62 |

## Dépenses de Lumière, jours 1 à 30

| Profil | Œufs | Améliorations | Zones | Biomes | Décorations | Épicerie |
| --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 72,9 % | 26,7 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Régulier | 62,0 % | 37,7 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Passionné | 67,7 % | 32,0 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club | 68,8 % | 31,0 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club + Pass + packs | 70,4 % | 29,4 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Grand collectionneur | 61,8 % | 37,9 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Régulier, joueur optimiseur | 3,9 % | 95,7 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |

## Sensibilité : le joueur optimiseur

Même profil régulier, mais chaque achat est celui qui se rembourse le plus vite (sans suivre les conseils du jeu). L'écart avec le joueur guidé montre ce qui dépend du comportement du joueur et ce qui dépend des formules.

| Mesure | Joueur guidé | Joueur optimiseur |
| --- | --- | --- |
| Éclosions le jour 1 | 72 | 51 |
| Éclosions sur 120 jours | 9982 | 13034 |
| Œufs, part des dépenses (j. 1 à 30) | 62,0 % | 3,9 % |
| Améliorations, part des dépenses | 37,7 % | 95,7 % |
| Ouverture : Montagne | jour 4,9 | jour 17,8 |
| Ouverture : Neige | jour 8,4 | jour 23,9 |
| Ouverture : Jungle | jour 10,8 | jour 35,5 |
| Ouverture : Désert | jour 17,5 | jour 46,5 |
| Premier Renouveau | jour 6,4 | jour 17,8 |
| Niveau du refuge au jour 120 | 100 | 105 |

## Renouveaux

| Profil | Jour et Essence de chaque Renouveau (dont liée à la Lumière) |
| --- | --- |
| Occasionnel | j. 6,5 (87, dont 18) ; j. 10,5 (51, dont 51) ; j. 13,8 (83, dont 72) ; j. 18,9 (120, dont 120) ; j. 22,9 (182, dont 171) ; j. 26,5 (274, dont 274) ; j. 31,8 (439, dont 439) ; j. 35,8 (868, dont 857) ; j. 40,9 (1149, dont 1149) ; j. 45,8 (1982, dont 1971) ; j. 51,8 (2817, dont 2817) ; j. 60,5 (4628, dont 4626) ; j. 66,5 (6615, dont 6598) ; j. 74,9 (9747, dont 9743) ; j. 80,5 (17426, dont 17404) ; j. 84,5 (24063, dont 24063) ; j. 88,5 (38498, dont 38487) ; j. 92,5 (55809, dont 55809) ; j. 98,5 (102585, dont 102574) ; j. 102,5 (140691, dont 140691) ; j. 108,5 (229031, dont 229031) ; j. 118,9 (326739, dont 326739) |
| Régulier | j. 6,4 (91, dont 22) ; j. 8,5 (54, dont 43) ; j. 10,5 (79, dont 79) ; j. 12,4 (129, dont 118) ; j. 14,4 (193, dont 193) ; j. 16,9 (280, dont 280) ; j. 19,4 (487, dont 476) ; j. 21,4 (708, dont 708) ; j. 23,4 (1292, dont 1281) ; j. 25,4 (1668, dont 1668) ; j. 28,4 (2802, dont 2791) ; j. 30,8 (4080, dont 4080) ; j. 34,4 (6493, dont 6493) ; j. 38,9 (9362, dont 9351) ; j. 41,4 (14681, dont 14670) ; j. 43,9 (22504, dont 22504) ; j. 46,5 (33037, dont 33026) ; j. 49,4 (53920, dont 53920) ; j. 53,4 (83182, dont 83182) ; j. 57,4 (121818, dont 121807) ; j. 61,9 (182831, dont 182829) ; j. 69,4 (284769, dont 284761) ; j. 79,5 (416586, dont 416572) ; j. 99,5 (626980, dont 626956) ; j. 117,8 (936920, dont 936900) |
| Passionné | j. 6,3 (105, dont 31) ; j. 7,6 (55, dont 44) ; j. 9,6 (80, dont 80) ; j. 12,5 (122, dont 122) ; j. 14,9 (185, dont 174) ; j. 18,3 (305, dont 305) ; j. 20,9 (434, dont 423) ; j. 22,7 (648, dont 648) ; j. 25,5 (990, dont 990) ; j. 27,8 (1500, dont 1489) ; j. 30,8 (2240, dont 2240) ; j. 35,9 (3345, dont 3328) ; j. 39,4 (5121, dont 5115) ; j. 44,5 (7728, dont 7728) ; j. 48,8 (11760, dont 11738) ; j. 50,6 (17794, dont 17794) ; j. 52,3 (35978, dont 35967) ; j. 54,3 (48489, dont 48489) ; j. 55,8 (68776, dont 68765) ; j. 57,8 (104894, dont 104894) ; j. 60,7 (161199, dont 161199) ; j. 65,3 (238188, dont 238188) ; j. 72,5 (357137, dont 357137) ; j. 87,3 (542754, dont 542742) ; j. 100,6 (807315, dont 807299) ; j. 119,8 (1211476, dont 1211456) |
| Payeur Club | j. 6,4 (91, dont 22) ; j. 7,8 (51, dont 40) ; j. 9,5 (80, dont 80) ; j. 11,4 (138, dont 127) ; j. 13,4 (193, dont 193) ; j. 15,9 (301, dont 301) ; j. 18,4 (482, dont 471) ; j. 20,4 (711, dont 711) ; j. 23,4 (1142, dont 1140) ; j. 26,4 (1618, dont 1601) ; j. 29,8 (2477, dont 2473) ; j. 33,4 (4026, dont 4015) ; j. 37,4 (6162, dont 6154) ; j. 39,8 (10588, dont 10562) ; j. 41,4 (14941, dont 14941) ; j. 43,8 (22557, dont 22557) ; j. 46,4 (38066, dont 38055) ; j. 49,4 (57469, dont 57469) ; j. 53,4 (87891, dont 87891) ; j. 57,9 (127719, dont 127702) ; j. 63,4 (193608, dont 193600) ; j. 71,4 (286038, dont 286036) ; j. 85,5 (432423, dont 432393) ; j. 107,4 (671035, dont 671019) |
| Payeur Club + Pass + packs | j. 6,4 (105, dont 31) ; j. 7,9 (56, dont 45) ; j. 10,4 (95, dont 95) ; j. 12,5 (147, dont 136) ; j. 14,8 (209, dont 209) ; j. 17,5 (327, dont 327) ; j. 19,9 (510, dont 499) ; j. 21,9 (758, dont 758) ; j. 23,9 (1177, dont 1166) ; j. 26,4 (2025, dont 2025) ; j. 29,8 (2752, dont 2752) ; j. 33,4 (4312, dont 4301) ; j. 37,4 (6396, dont 6396) ; j. 43,4 (9486, dont 9486) ; j. 48,4 (15737, dont 15713) ; j. 50,8 (22360, dont 22349) ; j. 52,4 (37284, dont 37284) ; j. 54,4 (52456, dont 52456) ; j. 56,5 (83350, dont 83339) ; j. 58,9 (122485, dont 122485) ; j. 62,5 (192379, dont 192379) ; j. 68,5 (286360, dont 286360) ; j. 78,9 (422196, dont 422194) ; j. 96,9 (636754, dont 636724) ; j. 111,4 (972717, dont 972705) |
| Grand collectionneur | j. 6,4 (92, dont 23) ; j. 8,5 (56, dont 45) ; j. 10,8 (80, dont 80) ; j. 12,5 (121, dont 110) ; j. 14,4 (184, dont 184) ; j. 16,9 (314, dont 303) ; j. 18,5 (454, dont 454) ; j. 20,4 (756, dont 756) ; j. 21,9 (1178, dont 1167) ; j. 23,9 (1637, dont 1637) ; j. 25,9 (2691, dont 2680) ; j. 28,4 (4319, dont 4319) ; j. 31,9 (6259, dont 6259) ; j. 35,9 (9455, dont 9444) ; j. 38,8 (14383, dont 14370) ; j. 41,8 (22039, dont 22033) ; j. 45,8 (34901, dont 34888) ; j. 48,5 (50329, dont 50329) ; j. 52,8 (76062, dont 76062) ; j. 56,8 (114233, dont 114222) ; j. 60,9 (176928, dont 176928) ; j. 67,4 (269248, dont 269248) ; j. 76,4 (404590, dont 404566) ; j. 95,5 (597313, dont 597285) ; j. 114,8 (895300, dont 895270) |

## Ce que le simulateur suppose

- Contenu : les définitions livrées (Prairie : 3 espèces, 18 variantes, œufs Basic, Focus et Découverte), complétées par le roster prévu (6 espèces par biome, 6 variantes de base, 13 biomes, zones de la section 4.2). Les espèces prévues ne sont jamais livrées.
- Jours : le jour calendaire des docs (le jour de l'installation est le jour 1, installation à midi) ; 1,5 veut dire midi du jour 1.
- Joueur guidé, le même pour tous les profils : il éclot surtout ; il prend l'œuf qui cache encore une nouveauté (le moins cher), sinon celui du biome dont une espèce est la plus proche de sa prochaine couleur de maîtrise, sinon l'œuf Basic du biome le plus récent ; une amélioration quand le HUD la conseille (au moins 5 % de production totale, ou le palier × 2 que la Lumière atteint) ; une zone dès que son panneau s'allume ; Nids douillets et Regard curieux quand ils coûtent moins de 10 et 30 minutes de production ; quand le prochain œuf est à plus de 10 minutes, il ouvre l'écran Améliorer et prend la carte « Conseillé » (le plus court temps de retour). Il garde sa Lumière pour une porte de biome dont les autres conditions sont remplies, achète les décorations communes et les friandises bon marché, éclot les œufs Découverte tout de suite, par 1, 3 ou 10 selon le niveau du refuge.
- Renouveau : le premier dès que le bouton s'active, les suivants quand ils ajoutent au moins la moitié de l'Essence déjà gagnée (« rentable ») ; Essence au nœud ouvert le moins cher, branche Économie d'abord.
- Rendez-vous : cadeau du jour (table et garantie), calendrier de 28 cases, souhaits, carnet de la semaine, activités (minutes de production), expéditions (minutes de production, œufs Découverte et garantie), un événement par mois (7 objets), Pass de 30 jours.
- Systèmes prévus mais pas encore construits, comptés comme leurs docs le disent : traits aux taux du doc Formules, visiteurs rares, autocollants et pages d'album, décorations posées, variantes de maîtrise (niveaux 10, 20, 30), objets d'événement et couleurs du Pass (3 points chacun), souhaits (maîtrise du biome).
- Pas encore simulés : la reproduction (T56) et les 5 % de Lumière du compagnon de pack.
- Les Gemmes d'un palier de maîtrise d'espèce (2) sont comptées ici ; le jeu les donnera avec l'écran des maîtrises.
