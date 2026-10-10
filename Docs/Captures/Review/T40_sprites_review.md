# T40 : revue « fait main » des icônes de la Pluie de noisettes

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 7 sprites ajoutés par T40 : le panier large, la plume et l'averse dorée, les
objets spéciaux montrés dans la puce de boost du HUD ; puis les icônes des récompenses de la carte du résultat : le
biscuit champignon (friandise 3 étoiles de T39), la noisette grillée (friandise 3 étoiles de T40), la noisette et la
fraise (ingrédients de la Pluie de noisettes). Planche : `T40_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t40.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T39. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque, aucune police dans les sprites. Lancer `python SourceArt/UI/Tools/pw_ui_sprites_t40.py`
  réécrit seulement ces 7 sprites et leurs masters.
- Reproductible : deux lancements complets de `main()` (sorties dirigées vers un dossier de travail) donnent des
  fichiers identiques octet pour octet ; les 230 PNG d'avant T40 que dessine `main()` sont identiques à ceux du projet,
  et les 257 PNG qui existaient avant T40 (sprites et masters) gardent la même empreinte SHA-256. Les 7 sprites et
  leurs masters écrits dans le projet sont ceux des lancements complets ; ajouter les icônes des récompenses n'a pas
  changé un octet des trois icônes de boost.
- Revue : chaque icône regardée en master 4x, en 2x, à sa taille, à 64, 48, 32 et 24 px, sur fond clair et sur fond
  sombre ; dans la puce de boost telle que `ActivityUiPipeline.BuildBoost` la construit (lueur dorée, disque crème,
  piste et anneau doré qui se vide, icône à 60 %) à 104 unités (icône de 62) et à 80 unités (icône de 48), à côté de la
  brosse dorée de T38 ; puis sur une capture réelle de T38 à la place de la puce de la brosse dorée, au tiers et à la
  taille du téléphone ; enfin à côté de dix icônes existantes (pluie de noisettes du hub, brosse dorée, entraînement,
  loupe, sifflet, temps, défi, nœud bleu, friandise, Lumière).
- Écarts mesurés sur la trame 4x (rien n'entre dans rien, contours compris) : entre le panier et chaque flèche, 4,5 px
  sur 128 ; entre deux noisettes dorées, 4,9 px au moins ; entre une noisette et le premier point de sa traînée, 4,5 à
  4,9 px ; entre un point et une autre noisette, 3,2 px au moins ; entre deux points, 3,0 px. Tout tient à 3 px ou plus
  du bord de l'icône.
- Itérations notables : les flèches du panier touchaient d'abord ses flancs et se lisaient comme des anses ; le panier
  a été resserré et les flèches descendues à côté du bas du bol, avec un vide mesuré. Les traînées de l'averse étaient
  d'abord droites au-dessus de chaque noisette, pointe en haut : on aurait dit des pendentifs sur un fil de perles. La
  chute penche maintenant un peu vers la gauche, les noisettes tournent sur elles-mêmes, les points s'éloignent en
  pâlissant et la base de chaque noisette a pris un ambre plus profond (en crème, on aurait dit de l'ail). Le duvet de
  la plume faisait deux bulles collées ; il se fond maintenant dans la base de la plume. Les trois icônes ont été
  recentrées par mesure de leur étendue.
- Doc de référence : doc 01 Contenu (S-ACT-003, tableau des objets : panier large +50 % pendant 8 s, plume chute × 0,6
  pendant 5 s, averse dorée 5 s de noisettes dorées ; H-VFX-ACT-003-02 « traînée de la noisette dorée : 6 points doux »,
  d'où les points ronds), doc « Mini-jeux & activités » (section 3).

### Icônes des récompenses

- Modèle : `ICO_Treat_CloverCrunch` (T36) : biscuit au contour brun chaud de la famille, ombre portée douce, garniture
  à son propre contour. Les deux friandises gardent ce biscuit et ce contour ; les deux ingrédients sont des produits
  frais, dans les couleurs de leur matière (le vert du brin de trèfle pour les feuilles).
- Revue : chaque icône regardée en master 4x, à sa taille, à 64, 48, 32 et 24 px, sur fond clair et sombre, à côté du
  Trèfle croquant ; puis dans les cellules de la carte du résultat telles que `ActivityUiPipeline` les construit
  (cellule de 124 x 152, disque de 108 teinté par le genre, icône de 78, « ×n » dessous), à leur taille et à l'échelle
  0,62 (icône de 48).
- Noisette fraîche et noisette grillée se distinguent au premier coup d'œil : la fraîche est dans sa coque brillante,
  avec sa base claire, une large feuille verte et une brindille ; la grillée est deux amandes rondes sans coque, brun
  doré, avec des plaques de leur peau fine, un glaçage de miel et deux volutes de chaleur. Le biscuit champignon a un
  chapeau glacé rose doux à pois crème, une lèvre de biscuit et un pied de biscuit nature.
- Écarts mesurés (contours compris) : volute et noisette grillée la plus proche 3,8 px au moins, entre les deux
  volutes 7 px. Étendue : tout tient à 4 px ou plus du bord, chaque icône recentrée par mesure.
- Itérations notables : la noisette fraîche avait d'abord une cupule de cinq lobes verts (on aurait dit un chou) : elle
  a pris une feuille de noisetier derrière elle. Les amandes grillées étaient d'abord en goutte (on aurait dit des
  amandes) : elles sont devenues rondes, et leurs volutes une seule courbe douce, plus épaisse en bas. Le glaçage du
  champignon, d'abord rouge vif, est passé au rose doux d'une friandise. La fraise des bois est un peu plus ronde.

## Assets

| Asset | Taille | Dossier | Usage | Teinte |
| --- | --- | --- | --- | --- |
| `ICO_WideBasket` | 128 x 128 | `Art/UI/Icons` | puce de boost du Panier large : un panier d'osier large et rond vu un peu de dessus (sa paroi intérieure, un bord torsadé, des montants et deux rangs tressés qui suivent la courbe du bol, un pied plat), deux flèches douces or miel qui pointent vers l'extérieur | non, couleurs propres |
| `ICO_Feather` | 128 x 128 | idem | puce de boost de la Plume : une plume ronde et douce bleu ciel, sa pointe ronde en haut à droite, une encoche douce de chaque côté, le rachis clair, des barbes légères, un calamus crème, un peu de duvet à sa base | non |
| `ICO_GoldenShower` | 128 x 128 | idem | puce de boost de l'Averse dorée : trois noisettes dorées (la noisette de `ICO_Act_NutRain` en or chaud) qui tombent à des hauteurs différentes, chacune sous une traînée de points ronds dorés, plus petits et plus pâles vers le haut | non |

| `ICO_Treat_MushroomBiscuit` | 128 x 128 | idem | cellule de la carte du résultat (`treat.mushroom_biscuit`, friandise 3 étoiles du Cache-cache) : un biscuit rond en forme de champignon, chapeau glacé rose doux à pois crème, lèvre et pied de biscuit | non |
| `ICO_Treat_ToastedHazelnut` | 128 x 128 | idem | cellule de la carte du résultat (`treat.toasted_hazelnut`, friandise 3 étoiles de la Pluie de noisettes) : deux noisettes grillées sans coque, brun doré, peau fine par plaques, glaçage de miel, deux volutes de chaleur | non |
| `ICO_Ingredient_Hazelnut` | 128 x 128 | idem | cellule de la carte du résultat (`ingredient.hazelnut`) : une noisette fraîche dans sa coque, base claire, une feuille de noisetier et une brindille | non |
| `ICO_Ingredient_Strawberry` | 128 x 128 | idem | cellule de la carte du résultat (`ingredient.strawberry`) : une petite fraise des bois ronde, graines rondes claires dans de petits creux, couronne de cinq feuilles rondes et queue courte | non |

Masters : `SourceArt/UI/Masters/<nom>@4x.png` pour les 7. Aucun texte dans les sprites.

Couleurs (si un effet ou un modèle doit s'accorder) :

| Matière | Haut | Bas | Contour |
| --- | --- | --- | --- |
| Osier du bol (comme `ICO_Act_NutRain`) | E2AE6E | 9A6234 | 5E3F24 |
| Bord torsadé | F6D7A2 | C48B52 | idem |
| Paroi intérieure | B17A42 | 6E4626 | idem |
| Or miel (flèches, points) | FFE48A | F0A73A | A8662A |
| Coque de la noisette dorée | FFE892 | E9A23A | A8662A, base EDB357 à C98536 |
| Plume bleu ciel | E2F2FF | 86BDEA | 35649F, calamus FFFAF0 à E6D6BC |
| Biscuit (comme `ICO_Treat_CloverCrunch`) | F6D08E | D8994F | 8A5A2E |
| Glaçage du champignon | FFC4B8 | EC7569 | B8443F, pois FFFBF2 à F3E2C8 |
| Noisette grillée | F2C27E | B8732F | 8A5A2E, peau 8E5226 |
| Feuilles (comme le brin de trèfle) | B8E68A | 5E9E4A | 4F7A2E |
| Fraise | FF8C82 | D9403F | 9C2F35, graines FBE7A2 |

## Grille

| Point de la charte | Résultat |
| --- | --- |
| 12. Aucune texture peinte, aucun bruit | vert : aplats et dégradés construits ; montants, rangs, torsades, barbes et stries sont des formes placées à la main |
| 13. Matière mate | vert : un reflet par matière, en haut à gauche |
| 16. Lumière chaude, aucun gris sale | vert : ombres portées brun chaud ; ombres internes teintées de la matière (osier, or, bleu de la plume) |
| 17. La matière se reconnaît | vert : osier tressé, coque dorée avec sa base rugueuse, plume avec son rachis et ses barbes |
| 33. Géométrie construite, trait constant | vert : contour de 4,5 px sur 128 comme `ICO_Act_NutRain` ou `ICO_Whistle` ; 2,4 px pour les flèches, 1,5 px pour les points ; coins arrondis partout |
| 34. Deux tons et un éclat au plus, lumière du haut et de la gauche | vert : dégradé du haut vers le bas, une ombre plus profonde en bas ou à droite, un éclat en haut à gauche |
| 35. Aucun texte, aucune pseudo-lettre, aucun emoji | vert : aucun signe ; les flèches sont des triangles arrondis |
| 36. Lisible à 24 dp, cible de 48 dp | vert : à 48 px dans la puce, le panier et ses flèches, la plume, les trois noisettes et leurs points se lisent sur fond clair et sombre ; à 32 px les silhouettes restent, les points de l'averse deviennent de petits grains |
| 37. Rareté par étoiles, mot et motif | sans objet |
| 38. Aucune étoile à quatre branches | vert : points ronds seulement ; aucun appel à `superellipse_points`, aucun `star_points` |
| 41. Source éditable, export reproductible | vert : script et masters `@4x` ; lancements identiques octet pour octet, aucun sprite existant ne change |
| 42. Aucun générateur | vert : rien d'autre que numpy et Pillow |
| 43. Vocabulaire | vert : noms, identifiants, commentaires et cette note vérifiés contre les listes de `VocabularyRuleTests` |
| 44. Fait par la même main | vert : planche de famille (bas de `T40_sprites_sheet.png`) à 128, 64 et 32 px, même contour, même chaleur, même éclat, même ombre que les icônes de T34A à T39 ; l'osier et la noisette sont ceux de `ICO_Act_NutRain`, l'or celui de `ICO_Challenge` |

Points 1 à 11, 14, 15, 18 à 32, 39 et 40 : sans objet pour des sprites d'interface.

Règles du propriétaire, en plus de la grille :

- Rien de religieux : un panier, une plume, des noisettes, des points ronds.
- Aucune étoile à quatre branches : les traînées sont des points ronds, jamais des étoiles ni des éclats pointus.
- Rien n'entre dans rien : les flèches restent à l'écart du panier, chaque point flotte à l'écart de sa noisette, des
  autres points et des autres noisettes, avec des vides mesurés.
- Doux : bouts ronds, coins arrondis, flèches aux pointes arrondies.

## Pour le côté Unity

- Import : automatique par `TextureImportRules` (préfixe `ICO_` sous `Art/UI`) : sprite, 100 PPU, sans mipmaps, clamp,
  pivot au centre. Aucune teinte : couleur blanche de l'Image.
- Atlas et prêt : les trois noms sont ajoutés à `ScreenOnlyIcons` (Atlas_Activities) et à `LentIcons` (prêtés par
  `PF_UI_ActivityHud`) dans `ActivityUiPipeline.cs`. Surface avec la marge de 4 px : environ 5,3 % d'une page de
  1024 x 1024.
- Carte du résultat : `ActivityUiPipeline.RewardItemIcons` relie `treat.mushroom_biscuit`, `treat.toasted_hazelnut`,
  `ingredient.hazelnut` et `ingredient.strawberry` à leur icône dans `RewardIconSet.items`, après le Trèfle croquant ;
  le genre `RewardKind.Ingredient` a son disque abricot doux (FBE8D4). Les quatre icônes vont dans Atlas_Activities
  (Atlas_CommonHUD est plein) : environ 7 % d'une page de 1024 x 1024 avec la marge de 4 px.
- Emprunt dans la scène : `host.Hud.ShowBoost(host.Hud.Icon("ICO_WideBasket"), 8f)`, `Icon("ICO_Feather")` 5 s,
  `Icon("ICO_GoldenShower")` 5 s ; jamais une copie dans le groupe `Activity_NutDrop`.
- Menu à lancer : « Pocket Wilds/UI/Build Activities (T37) », pour l'atlas, la liste des icônes prêtées du HUD, les
  icônes et les disques de la carte du résultat et les textes (« {n} ingrédient », « {n} ingrédients »).
