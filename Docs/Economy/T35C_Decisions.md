# T35C : calibrage de l'économie

8 octobre 2026. Ce document accompagne le rapport du simulateur (`T35C_Simulation.md`, écrit à chaque passage du test) et ses
données jour par jour (`T35C_Simulation.csv`). Sa version du matin (le constat avant décision, 42 garde-fous sur 96) est dans
l'historique git (commit 6c9fc72).

## En bref

- Le propriétaire a demandé d'appliquer toutes les recommandations du constat du matin, en privilégiant le plaisir du joueur
  (pas de lassitude, envie de revenir, envie d'acheter sans pression). C'est fait : **79 garde-fous sur 94 sont respectés**
  (42 sur 96 avant), aucun n'est hors bornes sans explication, les 15 autres sont des points ouverts nommés ci-dessous.
- Le blocage du jour 3 au jour 21 a disparu. Les 13 biomes s'ouvrent près de leurs cibles pour le joueur régulier (Forêt après
  35 minutes de jeu, Lac jour 2,4, Montagne 5,4, Neige 7,4, Jungle 12,8, Désert 18,8, Sakura 22,9, Champignons 27,5, Îles des
  nuages 33,4, Lunaire 37,4, Préhistorique 45,4, Ciel étoilé 54,4) comme pour l'occasionnel (Forêt jour 2,9, Montagne 5,8,
  Neige 9,8, Jungle 17,8).
- Chaque jour ordinaire donne quelque chose à faire : 20 à 60 éclosions, aucun œuf ne coûte plus de 15 minutes de production la
  première semaine ni plus de 45 ensuite, premier Renouveau au jour 6,4, couleurs de maîtrise qui arrivent vraiment.
- Les 5 docs du Drive concernés et une page « 00 — Décisions du propriétaire — 8 octobre 2026 » disent la même chose que le
  code (anciennes versions archivées « [Archive 2026-10-08 avant T35C] »).

## Ce qui a changé dans le jeu

| Sujet | Avant | Maintenant | Pourquoi |
| --- | --- | --- | --- |
| Prix des œufs | 10 × 1,18^n, un compteur par type d'œuf | 10 × 1,45^n, un compteur par biome (Basic et Focus ensemble : le Focus vaut toujours 4 fois le Basic du moment) | 309 éclosions le jour 1 pour 40 à 80 visées ; maintenant 72 |
| Prix maximal d'un œuf | garde-fou du simulateur | règle du jeu : 15 minutes de production les 7 premiers jours, 45 ensuite (`econ.egg.cap_minutes_week1`, `econ.egg.cap_minutes`) | sans lui, après un Renouveau les œufs devenaient trop chers en 2 à 3 jours, puis presque plus rien à éclore pendant des semaines |
| Croissance des œufs | dans chaque définition d'œuf | clé `econ.egg.growth` lue par le jeu (1,45 ; bornes 1,05 à 2,0) | réglable à distance |
| Niveau du refuge | 20 × (L − 1)^1,45 | 50 × (L − 1)^1,10 | un biome rapporte toujours à peu près autant de points (environ 400) : l'ancienne courbe demandait de plus en plus par porte |
| Portes des 3 derniers biomes | niveaux 70, 78, 86 | 66, 72, 78 | mêmes raisons ; les clés `econ.gate.<biome>.*` sont lues par le jeu |
| Maîtrise de biome | 40 × L^1,3 | 18 × L^1,3 | les portes demandent la maîtrise 6 à 10 du biome précédent |
| Zones | maîtrise 3 × k − 2 | 2 × k − 2, au moins 1 (zones aux maîtrises 1, 2, 4, 6, 8, 10) | la sixième zone n'arrivait jamais ; maintenant vers le jour 5 |
| Maîtrise d'espèce | 20 × L^1,35, doublon 2 XP | 2,7 × L^1,15, doublon 10 XP | 24 000 XP pour la maîtrise 30 : les couleurs de maîtrise (10, 20, 30) n'auraient jamais été vues |
| Gemmes d'un palier de maîtrise d'espèce | 5 | 2 | les paliers sont environ 8 fois plus fréquents ; le Club garde environ deux fois les Gemmes d'un joueur gratuit (× 1,62) |
| RenewalScale | 100 millions | 70 millions | 20 à 40 Essence liée à la Lumière au premier Renouveau |

Le calcul de la production ne normalise plus un grand nombre par espèce (somme en double, un seul BigValue à la fin) : il est
plus court en jeu comme dans le simulateur.

## Ce qui a changé dans les garde-fous

- Les bandes d'éclosions se mesurent hors des jours de Renouveau, d'ouverture d'un biome et de leur lendemain : les œufs y
  repartent de leur prix de base, c'est la pluie d'œufs voulue (« Ça repart »).
- La part des agrandissements et des décorations communes est affichée, plus vérifiée : leurs prix restent fixes, leur rythme
  vient de la maîtrise et du niveau du refuge. Les améliorations reprennent leur part : 20 à 45 %.
- Les « 20 à 40 Essence » du premier Renouveau sont l'Essence liée à la Lumière (le cadeau de 20 et le bonus de complétion
  s'y ajoutent).

## Le joueur simulé

Le « joueur guidé » (celui des garde-fous) éclot surtout. Il prend l'œuf qui cache une nouveauté (le moins cher) ; sinon
celui du biome dont une espèce est la plus proche de sa prochaine couleur de maîtrise ; sinon l'œuf Basic du biome le plus
récent. Il achète une amélioration quand le HUD la conseille, ouvre une zone dès que son panneau s'allume, et va sur l'écran
Améliorer quand le prochain œuf est à plus de 10 minutes. Il renouvelle quand le Renouveau ajoute au moins la moitié de
l'Essence déjà gagnée.

Le « joueur optimiseur » (contrôle) achète toujours ce qui se rembourse le plus vite : il met 95 % de sa Lumière dans les
améliorations, découvre moins et ouvre ses biomes beaucoup plus tard (Montagne jour 17, Jungle jour 39). L'économie
récompense donc la collection plutôt que l'optimisation, ce que veut le jeu.

## Points ouverts (les 15 garde-fous hors cibles)

1. **Part des œufs** (54,9 % pour 55 à 65 %) : à la limite, et très dépendante du comportement du joueur. À mesurer avec les
   vrais joueurs du soft launch.
2. **Jours de pluie d'œufs** : certains jours ordinaires montent à quelques centaines d'éclosions, quand le joueur vise une
   couleur de maîtrise dans un ancien biome dont les œufs viennent de repartir de leur prix de base. Un plaisir, ou un prix
   plancher en minutes de production si le propriétaire préfère un rythme régulier.
3. **Niveaux 4 et 9 plus tôt** (niveau 4 en quelques minutes, niveau 9 au soir du jour 1) : voulu pour l'accueil ; la Forêt
   garde son plancher de 30 minutes de jeu et le Lac celui du jour 2.
4. **Premier Renouveau de l'occasionnel au jour 6,5** (8 à 10 visé) : le hors-ligne payé 24 heures à 100 % le fait avancer
   presque comme le régulier en Lumière. Ses biomes, eux, tiennent leurs cibles.
5. **Rythme des Renouveaux** : de 1,5 à 18 jours selon la période (environ 5 en moyenne), parce que la Lumière produite croît
   par paliers (biomes, Renouveaux).
6. **Codex trop tôt** : Prairie au jour 9 (12 à 16 visé), les 24 espèces du lancement au jour 33 (100 à 120 visé) ; écart du
   payeur 36 %. Avec les garanties de nouveauté et les œufs Découverte, les couleurs de base se trouvent vite. Tenir 100 jours
   demanderait des couleurs bien plus rares : une décision de contenu.
7. **Maîtrise 30 vers le jour 28** (35 à 45 visé) : le joueur vise la couleur de maîtrise la plus proche.
8. **Deux écarts d'un jour** (un jour sans objectif de plus que la limite pour le payeur Club + Pass, un jour sans découverte
   de plus pour le grand collectionneur) : ils bougent d'un réglage à l'autre.

## Depuis T36 (8 octobre 2026)

Le simulateur donne maintenant les récompenses des rendez-vous avec les tables du jeu (`DailyRewardTables`) : cadeaux de
bienvenue, cadeau du jour (premier œuf Découverte, un gros cadeau au moins tous les 15), case du calendrier, avec les
remplacements tant que les activités, le Jardin et les albums ne sont pas ouverts (un ticket vaut 15 minutes de Lumière).
Chaque souhait donne aussi 5 XP à l'espèce de l'animal (doc 08, `econ.mastery_xp.wish`) ; les souhaits vont d'abord aux
animaux visibles, donc au biome où le joueur joue.

Résultat : **81 garde-fous sur 94 respectés**, 13 en attente d'une décision, aucun hors bornes. La part des œufs et l'écart
du payeur reviennent dans leurs bandes. Ce qui bouge :

- Point 7 : le régulier atteint la maîtrise 30 au jour 58 environ (il répartit son XP entre les espèces du biome où il
  joue), l'occasionnel au jour 54 (60 visé). Réglable par Remote Config sans mise à jour.
- Point 8 : le payeur Club finit les 120 jours sur 4 jours sans découverte (3 au plus visé), un écart de bord de course.

Note sur le simulateur : donner l'XP des souhaits à un animal tiré dans toute la collection faisait viser au joueur simulé
les espèces des anciens biomes (les plus nombreuses) et retardait ses biomes de 8 à 12 jours. Le modèle suit maintenant la
règle du jeu (animaux visibles d'abord).

## Depuis T37 (8 octobre 2026)

Le simulateur suit maintenant la table de récompenses des activités du jeu (`ActivityConfig`, doc Mini-jeux section 2).

- **Ce qui ne change pas.**
  - La Lumière d'une partie reste la moyenne du profil (10, 13 et 16 minutes, soit 2 étoiles et un peu de 3 étoiles).
  - Les 2 Gemmes des premières 3 étoiles du jour, 6 par jour au plus.
- **Ce qui s'ajoute.**
  - La chance d'autocollant selon les étoiles : 10, 20 et 35 % au lieu de 15 % fixes.
  - Les défis réussis à 3 étoiles : 0,5 par semaine pour l'occasionnel, 2 pour le régulier, 4 pour le passionné. Chacun
    rapporte 5 Gemmes une fois, et une décoration de défi arrive tous les 5 défis.

Résultat : **81 garde-fous sur 94 respectés**, 13 en attente d'une décision, aucun hors bornes.

- Point 8 : le passionné finit sur 4 jours sans découverte vers le jour 92 (3 au plus visés). C'est le même écart d'un
  jour que le payeur Club, qui bouge d'un réglage à l'autre.

Note sur le simulateur : un autocollant d'activité vient de la page de l'activité (doc Mini-jeux, section 2), pas d'un
album de biome. Le ranger dans l'album du biome le moins avancé donnait son XP de maîtrise aux anciens biomes. Le joueur
simulé visait alors ces biomes, et les Champignons et les Îles des nuages arrivaient 7 à 13 jours plus tard. Le modèle
suit maintenant la règle du jeu : page de l'activité, XP au biome où l'on joue.

## Pour rejouer

Menu Pocket Wilds/Economy/Run Simulator, ou le test `EconomySimulatorTests` (6 profils sur 120 jours en 7 secondes environ ;
le rapport et le CSV sont réécrits à chaque passage). Le test échoue si un garde-fou sort des bornes sans être dans la liste
`SimGuardrails.PendingOwnerDecision`.
