# T35C : simulation de l'économie

Rapport écrit par le simulateur (menu Pocket Wilds/Economy/Run Simulator, test EconomySimulatorTests), avec les valeurs des docs telles que le jeu les a. 6 profils sur 120 jours en 5,31 s. Garde-fous : 81 respectés sur 94, 13 en attente d'une décision du propriétaire, 0 hors bornes.

Analyse, contradictions entre les docs et décisions à prendre : `T35C_Decisions.md`. Données jour par jour : `T35C_Simulation.csv` (même dossier).

## Garde-fous

| Règle | Mesuré | Résultat |
| --- | --- | --- |
| Régulier, jours 1 à 30 : les œufs entre 55 et 65 % des dépenses de Lumière | 71,1 % | en attente de décision |
| Régulier, jours 1 à 30 : les améliorations entre 20 et 45 % des dépenses de Lumière | 28,5 % | respecté |
| Régulier, jours 1 à 30 : l'ouverture des biomes entre 0 et 2 % des dépenses de Lumière | 0,0 % | respecté |
| Régulier, jours 1 à 30 : l'épicerie entre 0 et 1 % des dépenses de Lumière | 0,4 % | respecté |
| Régulier : Forêt après 30 à 60 min de jeu cumulé | 35 min (jour 1,8) | respecté |
| Régulier : Lac au jour 2 (1,0 à 3,0) | jour 2,4 | respecté |
| Régulier : Montagne au jour 4 à 5 (3,0 à 6,0) | jour 4,9 | respecté |
| Régulier : Neige au jour 8 (7,0 à 9,0) | jour 7,4 | respecté |
| Régulier : Jungle au jour 12 (9,6 à 14,4) | jour 12,4 | respecté |
| Régulier : Désert au jour 16 (12,8 à 19,2) | jour 18,9 | respecté |
| Régulier : Sakura au jour 21 (16,8 à 25,2) | jour 24,5 | respecté |
| Régulier : Champignons au jour 27 (21,6 à 32,4) | jour 31,9 | respecté |
| Régulier : Îles des nuages au jour 34 (27,2 à 40,8) | jour 41,5 | respecté |
| Régulier : Lunaire au jour 42 (33,6 à 50,4) | jour 41,9 | respecté |
| Régulier : Préhistorique au jour 51 (40,8 à 61,2) | jour 48,5 | respecté |
| Régulier : Ciel étoilé au jour 60 (48,0 à 72,0) | jour 54,4 | respecté |
| Occasionnel : Forêt au jour 1 à 2 (0,0 à 3,0) | jour 2,9 | respecté |
| Occasionnel : Lac au jour 3 à 4 (2,0 à 5,0) | jour 2,9 | respecté |
| Occasionnel : Montagne au jour 6 à 8 (5,0 à 9,0) | jour 5,8 | respecté |
| Occasionnel : Neige au jour 12 (9,6 à 14,4) | jour 10,5 | respecté |
| Occasionnel : Jungle au jour 18 (14,4 à 21,6) | jour 18,5 | respecté |
| Occasionnel : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Occasionnel : Lac jamais avant le jour 2 | jour 2,9 | respecté |
| Occasionnel : Montagne jamais avant le jour 3 | jour 5,8 | respecté |
| Occasionnel : Neige jamais avant le jour 6 | jour 10,5 | respecté |
| Occasionnel : Jungle jamais avant le jour 9 | jour 18,5 | respecté |
| Régulier : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Régulier : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Régulier : Montagne jamais avant le jour 3 | jour 4,9 | respecté |
| Régulier : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Régulier : Jungle jamais avant le jour 9 | jour 12,4 | respecté |
| Passionné : jamais la Forêt avant 30 min de jeu | 31 min | respecté |
| Passionné : Lac jamais avant le jour 2 | jour 2,3 | respecté |
| Passionné : Montagne jamais avant le jour 3 | jour 4,3 | respecté |
| Passionné : Neige jamais avant le jour 6 | jour 6,6 | respecté |
| Passionné : Jungle jamais avant le jour 9 | jour 11,3 | respecté |
| Payeur Club : jamais la Forêt avant 30 min de jeu | 34 min | respecté |
| Payeur Club : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club : Montagne jamais avant le jour 3 | jour 4,9 | respecté |
| Payeur Club : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club : Jungle jamais avant le jour 9 | jour 11,4 | respecté |
| Payeur Club + Pass + packs : jamais la Forêt avant 30 min de jeu | 33 min | respecté |
| Payeur Club + Pass + packs : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Payeur Club + Pass + packs : Montagne jamais avant le jour 3 | jour 4,5 | respecté |
| Payeur Club + Pass + packs : Neige jamais avant le jour 6 | jour 7,4 | respecté |
| Payeur Club + Pass + packs : Jungle jamais avant le jour 9 | jour 11,4 | respecté |
| Grand collectionneur : jamais la Forêt avant 30 min de jeu | 35 min | respecté |
| Grand collectionneur : Lac jamais avant le jour 2 | jour 2,4 | respecté |
| Grand collectionneur : Montagne jamais avant le jour 3 | jour 4,8 | respecté |
| Grand collectionneur : Neige jamais avant le jour 6 | jour 8,4 | respecté |
| Grand collectionneur : Jungle jamais avant le jour 9 | jour 11,4 | respecté |
| Régulier, jours 1 à 7 : un œuf ne coûte jamais plus de 15 min de production | 15,0 min au plus | respecté |
| Régulier, ensuite : jamais plus de 45 min | 45,0 min au plus | respecté |
| Régulier : 40 à 80 éclosions le jour 1 | 72 | respecté |
| Régulier : 30 à 60 éclosions par jour jusqu'au jour 7, hors Renouveau, nouveau biome et lendemain | aucun jour ordinaire | respecté |
| Régulier : 20 à 50 éclosions par jour ensuite, hors Renouveau, nouveau biome et lendemain | 16 à 232 | en attente de décision |
| Régulier : deux zones de la Prairie dans la première heure | 2 min | respecté |
| Régulier : la sixième entre les jours 5 et 10 | jour 5,4 | respecté |
| Régulier : niveau 4 entre 30 et 60 min | 3 min | en attente de décision |
| Régulier : niveau 9 au jour 2 | jour 1,9 | en attente de décision |
| Régulier : niveau 15 entre les jours 4 et 5 | jour 4,9 | respecté |
| Régulier : premier Renouveau entre les jours 5 et 7 | jour 6,4 | respecté |
| Régulier : 20 à 40 Essence liée à la Lumière au premier Renouveau | 22 Essence (en tout 91) | respecté |
| Occasionnel : premier Renouveau entre les jours 8 et 10 | jour 6,5 | en attente de décision |
| Régulier : ensuite un Renouveau environ tous les 4 à 7 jours | 25 Renouveaux, écarts de 1,5 à 19,4 jours | en attente de décision |
| Régulier : Codex Prairie complet (variantes de base) entre les jours 12 et 16 | jour 9 | en attente de décision |
| Occasionnel : Codex Prairie complet entre les jours 18 et 25 | jour 13 | en attente de décision |
| Régulier : variantes de base des 24 espèces du lancement entre les jours 100 et 120 | jour 27 | en attente de décision |
| Occasionnel : variantes de base des 24 espèces au jour 150 ou plus | jour 40 | en attente de décision |
| Club + Pass : variantes de base des 24 espèces entre les jours 80 et 95 | jour 31 | en attente de décision |
| Régulier : maîtrise 30 sur une espèce entre les jours 35 et 45 | jour 63 | en attente de décision |
| Occasionnel : maîtrise 30 au jour 60 ou plus | pas dans les 120 jours | respecté |
| Occasionnel, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 3) | respecté |
| Occasionnel, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 61) | respecté |
| Occasionnel, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 80) | respecté |
| Régulier, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | 1 jour(s) de suite (jusqu'au jour 29) | respecté |
| Régulier, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 95) | respecté |
| Régulier, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 73) | respecté |
| Passionné, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Passionné, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 4 jour(s) de suite (jusqu'au jour 92) | en attente de décision |
| Passionné, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 1 jour(s) de suite (jusqu'au jour 80) | respecté |
| Payeur Club, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Payeur Club, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 100) | respecté |
| Payeur Club, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 100) | respecté |
| Payeur Club + Pass + packs, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Payeur Club + Pass + packs, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 46) | respecté |
| Payeur Club + Pass + packs, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 89) | respecté |
| Grand collectionneur, jours 1 à 30 : jamais plus de 1 jour(s) sans nouvelle découverte | aucun jour sans | respecté |
| Grand collectionneur, jours 31 à 120 : jamais plus de 3 jour(s) sans nouvelle découverte | 2 jour(s) de suite (jusqu'au jour 112) | respecté |
| Grand collectionneur, jours 1 à 120 : jamais plus de 2 jour(s) sans objectif atteint | 2 jour(s) de suite (jusqu'au jour 89) | respecté |
| Régulier gratuit : Gemmes non dépensées sous 1 500 au jour 30 | 389 | respecté |
| Club : environ deux fois les Gemmes d'un régulier gratuit (1,6 à 2,5) | × 1,63 | respecté |
| Club + Pass : collection 20 à 25 % plus rapide, jamais plus | -15 % | respecté |
| 6 profils sur 120 jours en moins de 10 s | 5,31 s | respecté |

En attente de décision mais respectés aujourd'hui (à retirer de la liste) : G5.target.casual.mastery30, G5.discovery.late.club, G5.goals.club_pass_packs, G5.discovery.early.collector, G4.payer.speed.

## Décisions en attente

- Point 1 : à la limite basse de la bande ; la part des œufs dépend surtout du comportement du joueur (joueur guidé autour de 55 %, optimiseur sous 10 %) : à mesurer avec les vrais joueurs du soft launch. (1 garde-fou hors bornes)
- Point 2 : les jours qui suivent un Renouveau, les œufs des anciens biomes repartent de leur prix de base ; un joueur qui vise une couleur de maîtrise en fait éclore des centaines. Une « pluie d'œufs » voulue, ou un prix plancher en minutes de production si le propriétaire préfère un rythme régulier. (1 garde-fou hors bornes)
- Point 3 : voulu, les premiers niveaux du refuge arrivent vite (accueil, fonctions débloquées tôt) ; la Forêt garde son plancher de 30 min de jeu et le Lac celui du jour 2. (2 garde-fous hors bornes)
- Point 4 : le hors-ligne payé 24 h à 100 % fait avancer l'occasionnel presque comme le régulier en Lumière ; ses biomes tiennent leurs cibles, son premier Renouveau arrive au jour 6 à 7. (1 garde-fou hors bornes)
- Point 5 : la Lumière produite croît par paliers (biomes, Renouveaux), l'Essence la suit : des Renouveaux rentables tous les 1,5 à 18 jours selon la période, environ tous les 5 jours en moyenne. (1 garde-fou hors bornes)
- Point 6 : avec les garanties de nouveauté et les œufs Découverte, les couleurs de base se trouvent bien avant les cibles des Codex (100 à 120 jours pour le lancement) : il faudrait des couleurs plus rares, une décision de contenu. (5 garde-fous hors bornes)
- Point 7 : la maîtrise 30 dépend de la couleur que le joueur vise. Depuis T36, chaque souhait donne aussi 5 XP à l'espèce de l'animal (doc 08 econ.mastery_xp.wish), surtout dans le biome où il joue : le régulier répartit son XP et arrive plus tard que visé (jours 35 à 45), l'occasionnel un peu plus tôt (jour 60 visé). Réglable par Remote Config sans mise à jour. (1 garde-fou hors bornes)
- Point 8 : un jour de plus que la limite, sur un seul profil ; ces écarts bougent d'un réglage à l'autre. (1 garde-fou hors bornes)

## Profils

| Profil | Jeu par jour (j. 2 à 120) | Éclosions | Découvertes | Biomes au jour 30 / 120 | Niveau du refuge au jour 30 / 120 | Renouveaux | Gemmes gagnées / dépensées |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 11 min | 5491 | 747 | 6 / 13 | 37 / 84 | 20 | 6039 / 6300 |
| Régulier | 32 min | 8680 | 870 | 8 / 13 | 53 / 99 | 25 | 8276 / 8400 |
| Passionné | 80 min | 9894 | 884 | 9 / 13 | 58 / 103 | 26 | 8724 / 9100 |
| Payeur Club | 32 min | 10113 | 875 | 9 / 13 | 60 / 101 | 25 | 13455 / 13650 |
| Payeur Club + Pass + packs | 32 min | 8533 | 868 | 8 / 13 | 52 / 99 | 24 | 17383 / 16800 |
| Grand collectionneur | 32 min | 9821 | 909 | 10 / 13 | 65 / 101 | 25 | 17917 / 18000 |

## Ouverture des biomes (jour, minutes de jeu cumulées)

| Biome | Occasionnel | Régulier | Passionné | Payeur Club | Payeur Club + Pass + packs | Grand collectionneur |
| --- | --- | --- | --- | --- | --- | --- |
| Prairie | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ | ouvert au départ |
| Forêt | j. 2,9 (35 min) | j. 1,8 (35 min) | j. 1,5 (31 min) | j. 1,6 (34 min) | j. 1,6 (33 min) | j. 1,8 (35 min) |
| Lac | j. 2,9 (36 min) | j. 2,4 (55 min) | j. 2,3 (110 min) | j. 2,4 (55 min) | j. 2,4 (55 min) | j. 2,4 (55 min) |
| Montagne | j. 5,8 (64 min) | j. 4,9 (143 min) | j. 4,3 (270 min) | j. 4,9 (143 min) | j. 4,5 (127 min) | j. 4,8 (135 min) |
| Neige | j. 10,5 (113 min) | j. 7,4 (216 min) | j. 6,6 (460 min) | j. 7,4 (215 min) | j. 7,4 (215 min) | j. 8,4 (247 min) |
| Jungle | j. 18,5 (202 min) | j. 12,4 (375 min) | j. 11,3 (830 min) | j. 11,4 (343 min) | j. 11,4 (343 min) | j. 11,4 (343 min) |
| Désert | j. 32,5 (345 min) | j. 18,9 (591 min) | j. 15,7 (1194 min) | j. 16,4 (506 min) | j. 17,8 (551 min) | j. 15,8 (487 min) |
| Sakura | j. 48,5 (516 min) | j. 24,5 (770 min) | j. 21,8 (1683 min) | j. 21,5 (674 min) | j. 25,4 (791 min) | j. 19,8 (618 min) |
| Champignons | j. 70,5 (742 min) | j. 31,9 (1007 min) | j. 29,5 (2290 min) | j. 27,4 (855 min) | j. 35,4 (1111 min) | j. 24,4 (759 min) |
| Îles des nuages | j. 95,8 (1008 min) | j. 41,5 (1311 min) | j. 38,3 (2990 min) | j. 35,5 (1119 min) | j. 49,4 (1559 min) | j. 30,9 (975 min) |
| Lunaire | j. 96,9 (1022 min) | j. 41,9 (1332 min) | j. 39,5 (3095 min) | j. 37,5 (1183 min) | j. 51,5 (1631 min) | j. 37,4 (1175 min) |
| Préhistorique | j. 105,8 (1113 min) | j. 48,5 (1535 min) | j. 45,3 (3550 min) | j. 45,4 (1431 min) | j. 55,9 (1780 min) | j. 45,4 (1431 min) |
| Ciel étoilé | j. 112,5 (1185 min) | j. 54,4 (1719 min) | j. 54,3 (4270 min) | j. 54,4 (1719 min) | j. 60,5 (1926 min) | j. 54,4 (1719 min) |

## Journal du joueur régulier, jours 1 à 30

Ce qui retient la porte du biome suivant à la fin de chaque jour (niveau du refuge, espèces du biome précédent, maîtrise, jour plancher, Lumière en pourcentage du prix).

| Jour | Éclosions | Découvertes | Niveau (score) | Biomes | Renouveau | Œuf le plus cher (min de production) | Porte suivante |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 72 | 48 | 9 (500) | 2 |  | 15 | Lac : plancher dans 1 j. |
| 2 | 82 | 33 | 13 (823) | 3 |  | 15 | Montagne : niveau 13/15, plancher dans 1 j. |
| 3 | 106 | 13 | 14 (891) | 3 |  | 15 | Montagne : niveau 14/15 |
| 4 | 62 | 29 | 18 (1190) | 4 |  | 15 | Neige : niveau 18/22, maîtrise 7/8, plancher dans 2 j. |
| 5 | 101 | 5 | 19 (1260) | 4 |  | 15 | Neige : niveau 19/22, maîtrise 7/8, plancher dans 1 j. |
| 6 | 168 | 20 | 21 (1399) | 4 | oui | 1 | Neige : niveau 21/22 |
| 7 | 241 | 55 | 27 (1840) | 5 | oui | 15 | Jungle : niveau 27/30, maîtrise 8/9, plancher dans 2 j., Lumière 33 % |
| 8 | 96 | 10 | 28 (1882) | 5 |  | 15 | Jungle : niveau 28/30, maîtrise 8/9, plancher dans 1 j. |
| 9 | 166 | 17 | 29 (1965) | 5 | oui | 45 | Jungle : niveau 29/30 |
| 10 | 86 | 6 | 29 (2009) | 5 |  | 45 | Jungle : niveau 29/30 |
| 11 | 41 | 3 | 29 (2028) | 5 |  | 45 | Jungle : niveau 29/30 |
| 12 | 259 | 41 | 35 (2434) | 6 | oui | 45 | Désert : niveau 35/38, maîtrise 8/10, plancher dans 1 j. |
| 13 | 75 | 5 | 35 (2472) | 6 |  | 45 | Désert : niveau 35/38, maîtrise 8/10 |
| 14 | 249 | 14 | 36 (2564) | 6 | oui | 20 | Désert : niveau 36/38, maîtrise 9/10 |
| 15 | 44 | 1 | 37 (2579) | 6 |  | 45 | Désert : niveau 37/38 |
| 16 | 178 | 5 | 37 (2635) | 6 | oui | 45 | Désert : niveau 37/38 |
| 17 | 84 | 3 | 37 (2652) | 6 |  | 45 | Désert : niveau 37/38 |
| 18 | 63 | 27 | 41 (2956) | 7 |  | 45 | Sakura : niveau 41/46, maîtrise 8/10 |
| 19 | 250 | 12 | 42 (3035) | 7 | oui | 45 | Sakura : niveau 42/46, maîtrise 9/10 |
| 20 | 64 | 10 | 43 (3104) | 7 |  | 45 | Sakura : niveau 43/46 |
| 21 | 243 | 13 | 44 (3196) | 7 | oui | 45 | Sakura : niveau 44/46 |
| 22 | 68 | 2 | 45 (3218) | 7 |  | 45 | Sakura : niveau 45/46 |
| 23 | 43 | 4 | 45 (3238) | 7 |  | 45 | Sakura : niveau 45/46 |
| 24 | 247 | 36 | 50 (3648) | 8 | oui | 45 | Champignons : niveau 50/54, maîtrise 9/10 |
| 25 | 79 | 7 | 51 (3704) | 8 |  | 45 | Champignons : niveau 51/54, maîtrise 9/10 |
| 26 | 31 | 5 | 51 (3732) | 8 |  | 45 | Champignons : niveau 51/54 |
| 27 | 199 | 19 | 52 (3838) | 8 | oui | 16 | Champignons : niveau 52/54 |
| 28 | 26 | 1 | 52 (3848) | 8 |  | 45 | Champignons : niveau 52/54 |
| 29 | 26 | 0 | 52 (3854) | 8 |  | 45 | Champignons : niveau 52/54 |
| 30 | 108 | 11 | 53 (3928) | 8 | oui | 45 | Champignons : niveau 53/54 |

## Dépenses de Lumière, jours 1 à 30

| Profil | Œufs | Améliorations | Zones | Biomes | Décorations | Épicerie |
| --- | --- | --- | --- | --- | --- | --- |
| Occasionnel | 78,4 % | 21,2 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Régulier | 71,1 % | 28,5 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |
| Passionné | 72,7 % | 27,0 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club | 62,6 % | 37,1 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Payeur Club + Pass + packs | 71,2 % | 28,5 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Grand collectionneur | 57,6 % | 42,1 % | 0,0 % | 0,0 % | 0,0 % | 0,3 % |
| Régulier, joueur optimiseur | 3,8 % | 95,9 % | 0,0 % | 0,0 % | 0,0 % | 0,4 % |

## Sensibilité : le joueur optimiseur

Même profil régulier, mais chaque achat est celui qui se rembourse le plus vite (sans suivre les conseils du jeu). L'écart avec le joueur guidé montre ce qui dépend du comportement du joueur et ce qui dépend des formules.

| Mesure | Joueur guidé | Joueur optimiseur |
| --- | --- | --- |
| Éclosions le jour 1 | 72 | 51 |
| Éclosions sur 120 jours | 8680 | 13070 |
| Œufs, part des dépenses (j. 1 à 30) | 71,1 % | 3,8 % |
| Améliorations, part des dépenses | 28,5 % | 95,9 % |
| Ouverture : Montagne | jour 4,9 | jour 17,8 |
| Ouverture : Neige | jour 7,4 | jour 23,9 |
| Ouverture : Jungle | jour 12,4 | jour 35,4 |
| Ouverture : Désert | jour 18,9 | jour 46,9 |
| Premier Renouveau | jour 6,4 | jour 17,8 |
| Niveau du refuge au jour 120 | 99 | 104 |

## Renouveaux

| Profil | Jour et Essence de chaque Renouveau (dont liée à la Lumière) |
| --- | --- |
| Occasionnel | j. 6,5 (87, dont 18) ; j. 10,5 (51, dont 51) ; j. 13,8 (86, dont 75) ; j. 18,5 (130, dont 130) ; j. 21,8 (186, dont 175) ; j. 25,8 (320, dont 320) ; j. 31,8 (444, dont 444) ; j. 35,8 (838, dont 827) ; j. 40,9 (1155, dont 1155) ; j. 48,5 (1944, dont 1944) ; j. 54,5 (2966, dont 2955) ; j. 62,9 (4347, dont 4347) ; j. 72,5 (6341, dont 6330) ; j. 82,5 (9826, dont 9826) ; j. 90,5 (15580, dont 15580) ; j. 99,8 (23635, dont 23613) ; j. 106,5 (44701, dont 44690) ; j. 110,9 (62446, dont 62446) ; j. 114,9 (89220, dont 89209) ; j. 119,8 (155550, dont 155550) |
| Régulier | j. 6,4 (91, dont 22) ; j. 7,9 (55, dont 44) ; j. 9,9 (81, dont 81) ; j. 12,5 (126, dont 115) ; j. 14,4 (186, dont 186) ; j. 16,9 (286, dont 286) ; j. 19,8 (446, dont 435) ; j. 21,8 (722, dont 722) ; j. 24,5 (1011, dont 1011) ; j. 27,4 (1524, dont 1513) ; j. 30,5 (2401, dont 2401) ; j. 33,9 (3482, dont 3471) ; j. 37,4 (5310, dont 5310) ; j. 41,9 (8011, dont 8000) ; j. 43,9 (12932, dont 12921) ; j. 46,4 (21717, dont 21717) ; j. 49,4 (35330, dont 35319) ; j. 51,5 (47844, dont 47844) ; j. 54,8 (72720, dont 72709) ; j. 57,4 (120666, dont 120666) ; j. 60,8 (169178, dont 169178) ; j. 66,4 (256762, dont 256760) ; j. 77,4 (388944, dont 388938) ; j. 96,8 (578583, dont 578557) ; j. 112,4 (871107, dont 871095) |
| Passionné | j. 6,3 (105, dont 31) ; j. 7,6 (55, dont 44) ; j. 9,6 (80, dont 80) ; j. 11,7 (128, dont 117) ; j. 13,4 (191, dont 191) ; j. 15,7 (295, dont 295) ; j. 17,6 (440, dont 429) ; j. 19,3 (658, dont 658) ; j. 21,8 (1000, dont 1000) ; j. 24,3 (1528, dont 1517) ; j. 27,3 (2336, dont 2334) ; j. 31,3 (3639, dont 3624) ; j. 34,7 (5256, dont 5252) ; j. 39,5 (8147, dont 8134) ; j. 41,3 (14327, dont 14316) ; j. 43,3 (20366, dont 20364) ; j. 45,7 (29679, dont 29666) ; j. 47,7 (46200, dont 46200) ; j. 50,8 (68186, dont 68186) ; j. 55,3 (106547, dont 106536) ; j. 58,4 (157575, dont 157575) ; j. 62,9 (233805, dont 233805) ; j. 69,3 (364555, dont 364549) ; j. 82,3 (543401, dont 543397) ; j. 95,3 (819054, dont 819036) ; j. 114,4 (1214531, dont 1214507) |
| Payeur Club | j. 6,4 (91, dont 22) ; j. 7,8 (52, dont 41) ; j. 9,5 (79, dont 79) ; j. 11,8 (130, dont 119) ; j. 13,8 (190, dont 190) ; j. 16,4 (287, dont 287) ; j. 18,4 (430, dont 419) ; j. 20,4 (758, dont 758) ; j. 22,5 (1145, dont 1134) ; j. 24,8 (1753, dont 1753) ; j. 27,5 (2589, dont 2578) ; j. 29,9 (4194, dont 4194) ; j. 33,4 (5975, dont 5975) ; j. 37,5 (9152, dont 9139) ; j. 39,8 (15041, dont 15030) ; j. 42,4 (21416, dont 21416) ; j. 45,5 (34339, dont 34328) ; j. 47,9 (51559, dont 51559) ; j. 51,4 (75853, dont 75853) ; j. 55,8 (118387, dont 118376) ; j. 59,8 (179165, dont 179165) ; j. 66,4 (269656, dont 269654) ; j. 76,4 (414022, dont 414014) ; j. 92,9 (603929, dont 603905) ; j. 112,8 (912672, dont 912648) |
| Payeur Club + Pass + packs | j. 6,4 (105, dont 31) ; j. 7,9 (56, dont 45) ; j. 9,9 (81, dont 81) ; j. 12,4 (146, dont 135) ; j. 14,4 (209, dont 209) ; j. 16,8 (321, dont 321) ; j. 19,4 (511, dont 500) ; j. 21,4 (804, dont 804) ; j. 24,8 (1166, dont 1166) ; j. 27,9 (1719, dont 1708) ; j. 31,9 (2612, dont 2612) ; j. 37,4 (4252, dont 4241) ; j. 41,9 (6241, dont 6241) ; j. 49,4 (9519, dont 9519) ; j. 52,9 (15177, dont 15155) ; j. 55,8 (22405, dont 22405) ; j. 57,9 (33858, dont 33847) ; j. 60,5 (51706, dont 51706) ; j. 62,9 (81828, dont 81817) ; j. 65,8 (124020, dont 124020) ; j. 69,4 (178826, dont 178826) ; j. 75,4 (270722, dont 270710) ; j. 84,8 (408599, dont 408599) ; j. 104,4 (616268, dont 616242) |
| Grand collectionneur | j. 6,4 (92, dont 23) ; j. 8,5 (56, dont 45) ; j. 10,5 (77, dont 77) ; j. 12,4 (138, dont 127) ; j. 14,4 (206, dont 206) ; j. 16,4 (319, dont 308) ; j. 17,9 (494, dont 494) ; j. 19,8 (764, dont 764) ; j. 21,4 (1108, dont 1097) ; j. 23,4 (1785, dont 1785) ; j. 25,5 (2806, dont 2795) ; j. 27,9 (4026, dont 4026) ; j. 31,4 (6900, dont 6889) ; j. 33,9 (9781, dont 9777) ; j. 37,5 (14539, dont 14526) ; j. 40,4 (24300, dont 24300) ; j. 44,4 (35577, dont 35577) ; j. 48,4 (52168, dont 52157) ; j. 53,5 (77593, dont 77593) ; j. 57,9 (120064, dont 120053) ; j. 62,4 (176776, dont 176776) ; j. 68,4 (283078, dont 283078) ; j. 77,4 (416842, dont 416826) ; j. 98,4 (617853, dont 617827) ; j. 116,5 (934702, dont 934684) |

## Ce que le simulateur suppose

- Contenu : les définitions livrées (Prairie : 3 espèces, 18 variantes, œufs Basic, Focus et Découverte), complétées par le roster prévu (6 espèces par biome, 6 variantes de base, 13 biomes, zones de la section 4.2). Les espèces prévues ne sont jamais livrées.
- Jours : le jour calendaire des docs (le jour de l'installation est le jour 1, installation à midi) ; 1,5 veut dire midi du jour 1.
- Joueur guidé, le même pour tous les profils : il éclot surtout ; il prend l'œuf qui cache encore une nouveauté (le moins cher), sinon celui du biome dont une espèce est la plus proche de sa prochaine couleur de maîtrise, sinon l'œuf Basic du biome le plus récent ; une amélioration quand le HUD la conseille (au moins 5 % de production totale, ou le palier × 2 que la Lumière atteint) ; une zone dès que son panneau s'allume ; Nids douillets et Regard curieux quand ils coûtent moins de 10 et 30 minutes de production ; quand le prochain œuf est à plus de 10 minutes, il ouvre l'écran Améliorer et prend la carte « Conseillé » (le plus court temps de retour). Il garde sa Lumière pour une porte de biome dont les autres conditions sont remplies, achète les décorations communes et les friandises bon marché, éclot les œufs Découverte tout de suite, par 1, 3 ou 10 selon le niveau du refuge.
- Renouveau : le premier dès que le bouton s'active, les suivants quand ils ajoutent au moins la moitié de l'Essence déjà gagnée (« rentable ») ; Essence au nœud ouvert le moins cher, branche Économie d'abord.
- Rendez-vous : cadeau du jour (table et garantie), calendrier de 28 cases, souhaits, carnet de la semaine, activités (minutes de production), expéditions (minutes de production, œufs Découverte et garantie), un événement par mois (7 objets), Pass de 30 jours.
- Systèmes prévus mais pas encore construits, comptés comme leurs docs le disent : traits aux taux du doc Formules, visiteurs rares, autocollants et pages d'album, décorations posées, variantes de maîtrise (niveaux 10, 20, 30), objets d'événement et couleurs du Pass (3 points chacun), souhaits (maîtrise du biome).
- Pas encore simulés : la reproduction (T56) et les 5 % de Lumière du compagnon de pack.
- Les Gemmes d'un palier de maîtrise d'espèce (2) sont comptées ici ; le jeu les donnera avec l'écran des maîtrises.
