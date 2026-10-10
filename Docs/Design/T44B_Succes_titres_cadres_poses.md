# T44B Succès, titres, cadres, poses

Deuxième partie de T44 (spec : « 01 Succès, titres, cadres, poses et journal », sections 1 à 5). Critère : garanties
testées, 0 GC en jeu, les 200 succès sont des données validées.

## Ce que le joueur vit

- **Un bouton Succès** dans le HUD (au-dessus des Albums), avec un point quand une récompense attend.
- **Mon profil** : la carte du profil (le portrait de l'animal favori dans le cadre choisi, le titre choisi dessous) et
  quatre onglets :
  - **Succès** : 200 médailles en 4 formes partagées (Bronze, Argent, Or, Étoile ronde à cinq branches) avec l'icône de
    la catégorie, la barre et « Plus que 3 », « Réclamer » avec ses Gemmes, filtres par catégorie ;
  - **Titres** : la galerie, « Aucun titre » possible, les titres verrouillés disent comment les gagner ;
  - **Cadres** : les 42 cadres autour du portrait (anneau teinté et motifs dessinés) ;
  - **Poses** : les 47 poses avec « Mode photo bientôt ».

## Règles

- Les 200 succès, 322 titres, 42 cadres et 47 poses viennent des tableaux du doc, lus par un script
  (`Tools/Data/pw_profile_data.py`) qui refuse les seuils qui ne montent pas et les doublons.
- Compteurs cumulés depuis la création du compte, jamais remis à zéro (Renouveau compris) ; les compteurs d'état sont
  recalculés depuis la sauvegarde. Paliers : 5, 10, 20 et 50 Gemmes, décoration de biome à l'Or, titre, cadre ou pose à
  l'Étoile pour les succès indiqués. Les récompenses attendent sans limite.
- Titres et cadres gagnés depuis les succès, les pages d'album, la maîtrise, le niveau du refuge (les autres sources
  ont leur crochet prêt). Titre « Petite patte » et cadre « Rond doux » donnés à la création.
- Sauvegarde section 21 (schéma 15), analytique 170 à 172.

## Décisions prises

1. Les poses ont leurs données et leurs déblocages ; leurs animations faites main viennent avec le mode photo.
2. Les compteurs des systèmes pas encore faits (visiteurs, reproduction, événements, photos…) restent à 0 avec leur
   crochet prêt.
3. Les styles d'anneau élaborés (perles, bandes, dégradé) sont dessinés comme un anneau simple pour l'instant.
