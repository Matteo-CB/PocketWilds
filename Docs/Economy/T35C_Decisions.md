# T35C : l'économie simulée et les décisions à prendre

8 octobre 2026. Ce document accompagne le rapport du simulateur (`T35C_Simulation.md`, écrit à chaque passage du test) et ses
données jour par jour (`T35C_Simulation.csv`).

## En bref

- Le simulateur d'économie est prêt. Il joue les 6 profils pendant 120 jours en 4 secondes environ avec le vrai code du jeu
  (production, œufs, garanties, maîtrises, améliorations, zones, portes de biome, niveau du refuge, Renouveau, arbre d'Essence,
  hors ligne). Deux passages donnent exactement les mêmes jours.
- Avec les valeurs des docs, **42 garde-fous sur 96 sont respectés**. Les 54 autres ne peuvent pas l'être avec les formules des docs
  telles qu'elles sont écrites : sur 8 points, les cibles d'un doc contredisent les formules d'un autre. Aucun réglage dans les bornes
  de Remote Config ne fait tout passer : plusieurs centaines d'essais automatiques (sur les premiers jours, puis sur les 120 jours)
  n'en ont trouvé aucun, et le meilleur réglage mesuré avec la version finale du simulateur en respecte 51.
- Le problème le plus grave pour le joueur : **du jour 3 au jour 21, le joueur régulier reste bloqué aux niveaux 13 et 14**.
  Le niveau 15, qui ouvre la Montagne et le Renouveau, demande presque tout le contenu des trois premiers biomes. Il fait 2 à 15
  éclosions par jour et ne découvre presque plus rien (voir le journal dans le rapport).
- Le test `EconomySimulatorTests` passe. Il laisse en attente ces 54 garde-fous (liste nommée dans `SimGuardrails`, chacun avec sa
  décision) et échoue sur tout autre écart. Quand une décision est prise, je change les valeurs et ses garde-fous sortent de la liste.

## Comment lire les chiffres

- **Le joueur simulé est « guidé »**. Il éclot surtout. Il prend l'œuf qui cache encore une nouveauté (le moins cher), sinon celui
  qui produit le plus pour son prix. Il achète une amélioration quand le HUD la conseille (au moins 5 % de production, ou un palier
  × 2 qu'il peut atteindre) et ouvre une zone dès que son panneau s'allume. Quand l'œuf voulu coûte plus de 10 minutes de production,
  il prend la carte « Conseillé » de l'écran Améliorer. Un second joueur, l'« optimiseur », achète toujours ce qui se rembourse le
  plus vite. Il sert de contrôle : la plupart des écarts sont les mêmes avec lui, ils viennent donc des formules et non du comportement.
- **Les jours sont ceux des docs** : le jour de l'installation est le jour 1. L'installation a lieu à midi, donc « jour 2,4 » veut
  dire le jour 2 vers 10 h.
- **Le contenu** reprend les définitions livrées (Prairie) et le roster prévu : 6 espèces par biome, 6 variantes de base, 13 biomes.
- **Les systèmes pas encore construits** (traits, visiteurs, albums, décorations posées, variantes de maîtrise, objets d'événement)
  donnent leurs points et leur XP comme leurs docs le disent. La reproduction (T56) n'est pas simulée.

## Les décisions

### Décision 1 : le rythme des éclosions

**Les docs.** Doc Améliorations, section 17 : 40 à 80 éclosions le jour 1, 30 à 60 par jour jusqu'au jour 7, 20 à 50 ensuite.
Doc Formules : un œuf ne coûte jamais plus de 15 minutes de production la première semaine, 45 ensuite. Doc Formules et doc Œufs :
prix 10 × 1,18^n, un compteur par type d'œuf (Basic et Focus de chaque biome), 1 Lumière par seconde pour un animal de la Prairie.
Doc Formules : hors ligne payé à 100 % pendant 24 heures.

**Le simulateur.** Le régulier fait 309 éclosions le jour 1 (l'occasionnel 154). Les premiers œufs coûtent 3 à 5 secondes de
production : 13 animaux et le niveau 4 en moins d'une minute, une soixantaine d'animaux en 3 minutes. Chaque retour après une
absence paie des heures de production, ce qui permet de grimper chaque compteur de quelques œufs. La première semaine, l'œuf le plus
cher coûte jusqu'à 438 minutes de production : le joueur garde sa Lumière pendant le blocage de la décision 4.

| Essai | Éclosions jour 1 | Jours 2 à 7 | Montagne | Garde-fous respectés |
| --- | --- | --- | --- | --- |
| Valeurs des docs | 309 | 4 à 175 | jour 22,4 | 42 |
| Réglages les plus lents permis (croissance des œufs 1,30, Production + 2 % par niveau et chère) | 140 | 10 à 92 | jour 26,4 | 42 |
| Croissance des œufs 1,5 (hors bornes) | 121 | 8 à 64 | jour 27,4 | 47 |
| Croissance des œufs 1,8 (hors bornes) | 80 | 5 à 45 | jour 39,5 | 43 |

**Les choix possibles.**
1. Garder les formules, qui donnent le départ rapide voulu par le doc Formules (« les 10 premières minutes rapides »), et réécrire les
   bandes d'éclosions d'après ce qu'elles donnent.
2. Garder les bandes, ce qui suit la décision « pas trop facile » du 5 octobre : prix des œufs plus raides (croissance vers 1,5,
   au-delà de la borne actuelle de 1,30) et un seul compteur pour le Basic et le Focus d'un biome (le Focus coûte alors 4 fois le
   Basic du moment, comme le dit le doc Formules : « environ 3 à 5 × le coût d'un Basic »).

**Ma recommandation.** Le choix 2, avec la décision 4 dans le même passage : les prix plus raides seuls ralentissent aussi les biomes
(Montagne au jour 27 à 40).

### Décision 2 : le Renouveau et les compteurs d'œufs

**Les docs.** Doc Renouveau, section 1.4 : le Renouveau remet les compteurs d'œufs à zéro (« le prix des œufs repart de la base »).
Doc Améliorations, section 17 : 20 à 50 éclosions par jour après le jour 7.

**Le simulateur.** Après un Renouveau, les animaux restent et produisent, et les œufs coûtent de nouveau 10, 300 ou 2 500 Lumière.
Le jour d'un Renouveau, le régulier fait des centaines d'éclosions (787 au jour 27, jusqu'à 1 255). Si le Renouveau gardait les
compteurs, il n'y aurait plus ces pics, mais les œufs deviendraient trop chers : 2 éclosions par jour en médiane après le jour 7.
La remise à zéro fait donc tourner tout le cycle.

**Les choix possibles.**
1. Garder la remise à zéro : le pic est le moment « Ça repart » que le doc décrit (« il retrouve vite ses anciens niveaux ») ;
   avec l'éclosion par 10, 787 éclosions font 79 appuis. La bande se mesure alors hors des jours de Renouveau.
2. Faire repartir les compteurs à la moitié de leur valeur.

**Ma recommandation.** Le choix 1.

### Décision 3 : la part des agrandissements et des décorations dans les dépenses

**Les docs.** Doc Améliorations, section 1 : sur les 30 premiers jours, œufs 55 à 65 %, améliorations 20 à 30 %, agrandissements
5 à 10 %, décorations communes 3 à 5 %. Les prix des zones (25 × B × 2,15^k) et des décorations (30 × B) sont des multiples fixes du
prix de base de l'œuf du biome.

**Le simulateur.** Œufs et améliorations suivent la courbe de production. Les zones et les décorations gardent leur prix de départ.
Dès le jour 2, elles pèsent moins de 0,1 % des dépenses. Les zones sont de toute façon retenues par la maîtrise du biome, pas par
leur prix. La part des œufs (62,8 %) est dans la cible ; celle des améliorations est à 36,9 % (6,7 % et 93 % avec l'optimiseur).

**Les choix possibles.**
1. Retirer les parts des zones et des décorations des garde-fous (leur rythme est réglé par la maîtrise et le niveau du refuge).
2. Indexer leur prix sur la production du moment, comme les récompenses (par exemple au moins 20 minutes de production pour une zone).

**Ma recommandation.** Le choix 1. Le choix 2 ferait des zones une attente de plus, alors qu'elles sont déjà le cadeau de la maîtrise.

### Décision 4 : les portes de biome et le niveau du refuge

**Les docs.** Doc Améliorations, section 5.1 : Montagne au niveau 15 (et maîtrise 6 du Lac), Neige au 22, Jungle au 30, puis 8
niveaux de plus par biome jusqu'au 86. Courbe S(L) = 20 × (L − 1)^1,45 : 918 points pour le niveau 15. Maîtrise de biome :
40 × L^1,3 XP par niveau, la zone k demande la maîtrise 3k − 2. Cibles : Montagne aux jours 4 et 5, niveau 15 aux jours 4 et 5,
premier Renouveau aux jours 5 à 7, sixième zone de la Prairie entre les jours 5 et 10.

**Le simulateur.** Les trois premiers biomes rapportent au plus 900 points environ (espèces, variantes, deux ouvertures, les zones que
la maîtrise permet, les niveaux de maîtrise). Le régulier atteint 776 points au jour 2, puis gagne 0 à 35 points par jour. Il passe
le niveau 15 au jour 22. Ensuite, chaque porte demande plus de points et plus de maîtrise de biome qu'un biome n'en apporte (un biome
donne environ 350 points). Résultat : Montagne au jour 22,4, Neige au jour 48,9, Jungle au jour 109,9, Désert jamais ; premier
Renouveau au jour 22,4.

| Essai (dans les bornes de Remote Config) | Montagne | Neige | Jungle | Désert | 1er Renouveau | 6e zone | Garde-fous |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Valeurs des docs | 22,4 | 48,9 | 109,9 | jamais | 22,4 | jamais | 42 |
| Courbe du refuge 15, une zone tous les 2 niveaux de maîtrise, maîtrise de biome 20 | 3,4 | 6,4 | 14,4 | 24,5 | 6,4 | 7,4 | 51 |

**Les choix possibles.**
1. Appliquer tout de suite par Remote Config les trois réglages de la deuxième ligne (`econ.refuge.curve_mult` 15,
   `econ.zone.mastery_step` 2, `econ.biome_mastery.curve_mult` 20). Les biomes 1 à 6 arrivent alors près de leurs cibles, mais le
   niveau 15 arrive au jour 2,4 et les biomes 7 à 13 restent en retard.
2. Revoir la forme de la courbe : un exposant plus bas (1,2) pour que chaque porte demande environ ce qu'un biome rapporte, ou des
   niveaux de porte plus bas pour les biomes 7 à 13.

**Ma recommandation.** Le choix 1 maintenant, puis le choix 2 une fois les décisions 1 et 2 prises, dans un seul passage de calibrage.

### Décision 5 : l'Essence du premier Renouveau

**Les docs.** Doc Renouveau, section 1.3 : « premier Renouveau d'un joueur régulier entre les jours 5 et 7 avec 20 à 40 Essence ».
Le même doc donne 20 Essence offertes au premier Renouveau, plus un bonus de complétion (+1 par espèce, +5 par biome).

**Le simulateur.** Aux jours 5 à 7 visés, le régulier aurait 24 espèces et 4 biomes : le cadeau (20), le bonus des espèces (24) et
celui des biomes (20) font déjà 64 Essence, avant toute Lumière. Le simulateur lit donc « 20 à 40 » comme l'Essence liée à la Lumière,
la seule part que règle `econ.renewal.scale`. Avec les valeurs des docs, le premier Renouveau arrive au jour 22,4 avec 113 Essence,
dont 55 liées à la Lumière.

**Ma recommandation.** Confirmer cette lecture.

### Décision 6 : la maîtrise 30 d'une espèce

**Les docs.** Doc Gemmes, section 5 : maîtrise 30 sur une espèce entre les jours 35 et 45 (régulier). Doc Formules : XP pour passer
le niveau L = 20 × L^1,35, 2 XP par doublon, 15 par nouvelle variante, 25 par découverte.

**Le simulateur.** Il faut environ 24 000 XP pour le niveau 30, soit 12 000 doublons d'une même espèce. Le régulier atteint la
maîtrise 5 au jour 40 et 7 au jour 120 (13 148 éclosions en 120 jours, réparties sur des dizaines d'espèces).

**Les choix possibles.** Repousser la cible (maîtrise 30 comme objectif de plusieurs mois), ou adoucir nettement la courbe (de l'ordre
de 10 fois moins d'XP par niveau) ou donner beaucoup plus d'XP par doublon.

**Ma recommandation.** Décider après la décision 1, puisque le nombre d'éclosions fait l'XP.

### Décision 7 : les Codex arrivent trop tôt

**Les docs.** Doc Gemmes, section 5 : Codex Prairie aux jours 12 à 16 (régulier) et 18 à 25 (occasionnel) ; les variantes de base
des 24 espèces du lancement aux jours 100 à 120 (régulier), 150 et plus (occasionnel), 80 à 95 (Club et Pass), avec un payeur au plus
25 % plus rapide.

**Le simulateur.** Codex Prairie au jour 7 (régulier comme occasionnel) ; les 24 espèces au jour 27 (régulier), au jour 55
(occasionnel), au jour 18 (Club et Pass), soit un payeur 33 % plus rapide. La cause : 3 à 10 fois plus d'éclosions que prévu (décision 1)
et des garanties de nouveauté efficaces (bonus jusqu'à 35 % pour une nouvelle espèce, 30 % plus 10 points de Regard curieux pour une
nouvelle variante).

**Ma recommandation.** Décider après la décision 1. Si le volume d'éclosions reste haut, adoucir les garanties.

### Décision 8 : les jours sans nouveauté

**Les docs.** Doc Gemmes, section 5 : jamais plus d'un jour sans découverte pendant les 30 premiers jours, jamais plus de 3 ensuite,
jamais plus de 2 jours sans objectif atteint.

**Le simulateur.** Tant qu'une porte est bloquée, plus rien de nouveau ne s'ouvre : jusqu'à 11 jours sans découverte et 9 jours sans
objectif selon les profils.

**Ma recommandation.** Ces garde-fous suivent la décision 4 : je les remesure après.

## Ce qui a changé dans le jeu

- `RefugeLevelService` garde les points des variantes entre deux découvertes. Le calcul du score se refait à chaque éclosion : le
  résultat ne change pas, le calcul est plus court.
- `ProgressionConfig.Zones` peut être remplacé (le simulateur y met les zones prévues des 12 autres biomes).
- Aucune règle ni aucune valeur du jeu n'a changé.

## Ce qui reste à faire de mon côté

- La clé `econ.egg.growth` du doc Améliorations (section 15) n'est pas encore lue par le jeu (chaque œuf garde son costGrowth de
  1,18) ; le simulateur l'applique à tous les œufs pour les essais. Je la branche avec la décision 1.
- Le temps de calcul monte avec le nombre d'éclosions (7,8 s avec les réglages de la décision 4, choix 1) : je l'optimise au moment du
  calibrage pour rester sous 10 secondes.
- Une fois les décisions prises : un passage de calibrage (le simulateur fait un essai complet en 4 secondes et sait chercher seul),
  la mise à jour des docs du Drive et des tests de progression qui citent ces valeurs.
