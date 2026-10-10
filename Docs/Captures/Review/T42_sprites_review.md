# T42 : revue « fait main » des sprites des Expéditions

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 24 sprites ajoutés par T42 : le papier de la carte, la destination fermée, les
quatre cartes d'emplacement, l'anneau du minuteur (piste et remplissage), le « + », les puces de durée et leurs quatre
horloges, le cadre de la carte postale, le timbre et le cachet des Prés fleuris, le pétale, les trois raisons de la note
d'équipe, `ICO_Postcard` et `ICO_Expedition_Back`. Planche : `T42_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t42.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T41. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque, aucune police dans les sprites. `python SourceArt/UI/Tools/pw_ui_sprites_t42.py`
  réécrit seulement ces 24 sprites et leurs masters (`SourceArt/UI/Masters/<nom>@4x.png`).
- Reproductible : un second rendu dans un dossier de travail donne les 24 sprites et les 24 masters identiques à
  l'octet. Aucun sprite existant n'est réécrit.
- Revue : chaque sprite à sa taille sur fond clair et sombre ; les icônes à 96, 64, 48 et 32 px ; une maquette de
  l'écran à l'échelle d'un téléphone de 1080 px (moitié) : la carte et ses destinations, les quatre emplacements en
  9-slice à 490 x 410, l'anneau teinté en vert avec deux vignettes, les puces de durée, les raisons ; la carte postale
  en mosaïque à 592 x 848 autour d'une image de remplacement, avec le timbre et le cachet, et un coin à taille réelle
  pour vérifier les raccords ; l'accueil (anneau doux et six pétales sur un monde assombri).
- Rien n'entre dans rien : les collines de la carte sont à 4 px au moins l'une de l'autre et aucun point du chemin
  n'approche une colline à moins de 5 px ; les onze pétales de la pâquerette restent séparés (plus larges vers leur bout
  rond, étroits au cœur) ; les coussinets de la patte gardent 2 px clairs contours compris ; les trois points 7 px.
- Itérations : le but du chemin touchait une colline (déplacé), deux paires de collines se chevauchaient (écartées),
  les trois points de « Trois espèces » se touchaient à 48 px (plus petits, plus écartés).

## Choix faits

- Préfixes `UI_Exp_*` et `ICO_*` au lieu de `EXP_UI_*` : `TextureImportRules` n'importe en sprite que les noms en
  `UI_` et `ICO_` sous `Art/UI`, et `EXP_*` est le nom des cartes de destination rendues par l'éditeur
  (`PostcardLayout.CardArtName`, sous-dossier `Destinations`).
- Les emplacements et les puces sont des 9-slices (bords dans `TextureImportRules.UiSpriteBorder`) : la mise en page de
  l'écran choisit leur taille. Les quatre états partagent le même corps, à échanger sans bouger.
- Le cadre de la carte postale est un 9-slice en mode Tiled : un cadre à sa taille (592 x 848) dépasserait 512 px.
  Son bord déchiré se répète tous les 64 px et sa tuile du milieu fait 64 px, donc 7 x 11 tuiles tombent juste, sans
  raccord. Les côtés ont chacun leur propre bord (quatre jeux de phases).
- L'anneau du minuteur : piste crème et remplissage blanc à teinter (Image Filled, Radial 360 depuis le haut).
- Réutilisés sans redessiner : `ICO_Act_Expeditions`, `ICO_Lock` (emplacement fermé), `ICO_Timer`, `ICO_Challenge`,
  `ICO_Currency_Gems`, `UI_Ring_Soft` (l'anneau doux de la carte), `UI_Star_Result_Full` et `_Empty`.

## Grille de la charte « fait main »

| Point | Carte et destination fermée | Emplacements, puces, anneau | Horloges | Carte postale, timbre, cachet, pétale | Raisons | `ICO_Postcard`, `ICO_Expedition_Back` |
| --- | --- | --- | --- | --- | --- | --- |
| 33. Lisible à sa taille | vert : à 500 px dans la maquette | vert : 9-slice à toute taille | vert : à 32 px | vert : timbre à 64 px | vert : à 32 px | vert : à 48 px |
| 34. Lumière du haut à gauche | vert | vert | vert | vert | vert | vert |
| 35. Contour d'un ton plus profond | vert : tan, vert | vert : tan, ambre | vert | vert | vert : vert, brun, trois tons | vert |
| 36. Formes rondes, pas de pointe dure | vert : collines rondes, points ronds | vert : « + » aux bouts ronds | vert | vert : pétales ronds | vert : feuille au bout arrondi | vert |
| 37. Palette de la maison | vert : crème, vert du brin de trèfle | vert : crème des cartes de T41, or miel | vert | vert | vert | vert : or miel des médailles |
| 38. Aucune étoile à quatre branches | vert | vert | vert | vert : onze pétales, aucune pointe | vert | vert |
| 41. Source éditable, export reproductible | vert : script et master 4x ; deux rendus identiques | vert | vert | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert | vert | vert | vert | vert | vert |
| 43. Vocabulaire | vert | vert | vert | vert | vert | vert : clochette de boutique |
| 44. Fait par la même main (13.5) | vert : méthode et outils du kit | vert : comme les cartes du Memory | vert | vert | vert : patte comme `ICO_Paw` | vert |

## Assets

Tous dans `Assets/_Game/Art/UI/Activities/Expeditions/`, masters dans `SourceArt/UI/Masters/<nom>@4x.png`.

| Asset | Taille | Usage | Image |
| --- | --- | --- | --- |
| `UI_Exp_MapPaper` | 512 x 320 | papier de la carte : parchemin, chemin pointillé, petites collines | Simple |
| `UI_Exp_DestLocked` | 256 x 192 | destination fermée : silhouette douce et petit cadenas | Simple |
| `UI_Exp_Slot_Empty`, `_Away`, `_Back`, `_Locked` | 192 x 192 | les quatre états d'un emplacement | Sliced, bords 64 |
| `UI_Exp_Ring_Track`, `UI_Exp_Ring_Fill` | 128 x 128 | anneau du minuteur ; remplissage blanc à teinter | Simple ; Filled Radial 360 |
| `UI_Exp_Plus` | 96 x 96 | « + » d'un emplacement libre | Simple |
| `UI_Exp_Chip`, `UI_Exp_Chip_Selected` | 160 x 120 | puce de durée, choisie ou non | Sliced, bords 64, 60, 64, 56 |
| `ICO_Exp_Clock_1h`, `_4h`, `_8h`, `_12h` | 96 x 96 | horloge de chaque durée | Simple |
| `UI_Exp_PostcardFrame` | 208 x 208 | bord blanc déchiré de la carte postale, fenêtre 512 x 768 à 40 px du bord | Tiled à 592 x 848, bords 72 |
| `UI_Exp_Stamp_FlowerMeadows` | 128 x 128 | timbre rond des Prés fleuris (une pâquerette) | Simple |
| `UI_Exp_Postmark` | 128 x 128 | cachet rond, encre brune douce | Simple |
| `UI_Exp_Petal` | 64 x 64 | un des six pétales de l'accueil (H-VFX-ACT-005-02) | Simple |
| `ICO_Exp_Reason_Biome`, `_Family`, `_Species` | 96 x 96 | raisons de la note : même biome, famille préférée, trois espèces | Simple |
| `ICO_Postcard` | 128 x 128 | dos d'une carte postale | Simple |
| `ICO_Expedition_Back` | 128 x 128 | clochette de porte : une équipe est rentrée | Simple |
