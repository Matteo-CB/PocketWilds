# T44A : revue des icônes et des couvertures d'albums

10 octobre 2026. Dessinées par code (aucun générateur, aucune police, aucun modèle de bibliothèque), même méthode que les
icônes du HUD et de T43. Planche : `T44A_icons_sheet.png` (en haut les icônes à 128 px, dessous à 48 px sur fond sombre,
puis les 16 couvertures).

## Sources et rendu

- Script : `SourceArt/UI/Tools/pw_ui_sprites_t44.py` (branché sur `pw_ui_sprites.py`, `main`) ; planche :
  `SourceArt/UI/Tools/pw_ui_review_t44.py`. Masters `SourceArt/UI/Masters/<nom>@4x.png`.
- Icônes dans `Assets/_Game/Art/UI/Icons/` (128 x 128) : `ICO_Album`, `ICO_AlbumStar`, `ICO_Star_Rating`, `ICO_Event_Calendar`,
  `ICO_Visitor`, `ICO_World_Map`, `ICO_Season`, `ICO_Sticker`. Réutilisées sans redessin : `ICO_Act_Expeditions`
  (pour ICO_Expedition) et `ICO_Act_Garden` (pour ICO_Garden).
- Couvertures dans `Assets/_Game/Art/UI/Albums/Covers/` (256 x 320) : `UI_AlbumCover_<Nom>`, 13 biomes (Grassland, Forest, Lake,
  Mountain, Snow, Jungle, Desert, Sakura, Mushrooms, CloudIslands, Lunar, Prehistoric, StarrySky) + Events, Seasons, Activities.
  Pas de 9-slice : aucune règle à ajouter dans `TextureImportRules` (le dossier `Art/UI/` donne déjà Sprite, sans mipmaps).
- Chaque couverture : toile douce à la couleur du biome (fin tissage), dos plus foncé avec deux cordons, bordure cousue de
  points ronds, médaillon crème avec l'emblème rond, plaque crème au pied pour le nom de l'album (texte posé par Unity).

## Grille de revue (charte « fait main »)

| Critère | Résultat |
| --- | --- |
| Lisible à 48 px | oui pour les 8 icônes (rangée sombre de la planche) ; l'album, le calendrier, la valise et la carte se lisent d'un coup d'œil |
| Famille du HUD | contour brun chaud, lumière en haut à gauche, un dégradé, une ombre basse, un reflet, ombre portée douce |
| Aucune étincelle à quatre branches | oui : étoiles rondes à cinq branches, perles, pastilles, cœurs, pattes ; les feuilles de saison sont des vésicas ventrues en diagonale, le flocon a six bras à bouts ronds |
| Rien de religieux | oui |
| Rien n'entre dans rien | oui : pièces posées l'une sur l'autre avec leur propre contour, trous de la feuille de jungle ronds et espacés |
| Source modifiable | oui : script Python + master @4x de chaque image |
| Emblèmes demandés | marguerite, feuille de chêne, goutte, pic rond enneigé, flocon rond, grande feuille, arche de grès, pétale, champignon à pois, petit nuage, pleine lune, empreinte à trois doigts, étoile ronde ; rosette à rubans, quatre feuilles rondes, balle avec une patte |

## Points ouverts

- Le nuage et la lune sont clairs sur le médaillon crème (assez de contour, mais ce sont les emblèmes les plus doux) ;
  à juger dans Unity sur le téléphone.
- Les couvertures sont des textures complètes de 256 x 320 (16 fichiers) : si l'écran des albums les regroupe, les passer
  dans l'atlas de l'écran par son pipeline (comme T43 pour Garden).
