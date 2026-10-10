# T43 : revue « fait main » du potager et des plantes

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11) appliquée au décor du Jardin (doc 01
Contenu, section 8 : « le potager du refuge ») et aux 13 plantes de lancement en 3 stades (table 8.2). Images rendues dans
Blender (EEVEE, lumière unique venant du haut et de la gauche de l'image, couleurs calculées depuis les régions et les
palettes comme le fait `SH_EnvironmentOpaque`), à côté de ce fichier (préfixe `T43_props_`). La revue finale dans Unity
(captures `TestResults/Captures/T43_garden_set.png` et `T43_garden_plants.png` du pipeline) reste à faire par la session
principale (voir « Suivis »).

## Ce qui existait déjà (vérifié d'abord)

- `GR_SetBackdrop` (T38, groupe `Activity_Shared`) : le pré, trois rangs de collines rondes, des arbres ronds et des
  petites marguerites. Repris tel quel, deux fois plus grand, derrière la clôture. Son sol a un trou au milieu : le décor
  pose son propre pré par dessus (`GD_Ground`).
- Le kit de la Prairie : touffes d'herbe, trèfle et fleurs (entrées explicites de `Biome_Grassland_Kit`) : posés par le
  pipeline sur les bords du potager, au pied de la clôture et hors des parcelles, seulement là où ils tiennent sans rien
  toucher.
- Les paniers de T40 et T42 : même tressage et même osier miel pour le panier de récolte, mais un modèle à nous (un panier
  ouvert, plus large et plus bas, où la récolte tombe ; le panier de T40 est dans un autre bundle).
- Manquaient : les parcelles surélevées, la housse d'une parcelle fermée et son panneau, les pas japonais, la clôture,
  l'arrosoir, le panier de récolte, un pré sans trou, et les 39 modèles de plantes (les fleurs du kit sont des fleurs des
  prés vues du refuge, pas des cultures en trois stades).

## Fiche d'intention

- **Caractère** : un petit potager derrière le refuge, ensoleillé, net mais doux. Planches dodues aux arêtes arrondies,
  terre sombre et grumeleuse, pas ronds couleur crème, clôture basse aux poteaux à tête ronde, arrosoir bleu-vert, panier
  d'osier miel. Les plantes sont mignonnes et généreuses : feuilles rondes, fleurs de pétales ronds autour d'un coeur
  bombé, baies et fruits en sphères dodues, épis en perles douces, un petit arbre rond.
- **Échelle** : une parcelle fait 1,04 x 0,88 m et 0,27 m de haut (« environ 1,0 x 0,8 m ») ; sa terre fait 0,92 x 0,76 m,
  donc une plante de rayon 0,36 m garde 2 cm jusqu'aux planches. Une plante mûre fait 0,2 à 0,6 m de haut, le tournesol
  0,98 m et le pommier nain 0,88 m. Pas de 0,24 à 0,28 m, clôture de 0,42 m, arrosoir de 0,24 m, panier de 0,44 x 0,32 m.
- **Parties comptables** : parcelle 8 planches (2 rangs), 4 poteaux d'angle à tête ronde, 1 dessus de terre, 4 mottes ;
  housse 1 toile bombée, 1 jupe à l'ourlet ondulé et 1 bande vert sauge, 1 panneau sur chevalet ; chemin 15 pas ; clôture
  11 poteaux, 2 lisses ; arrosoir 1 corps, 1 bec et sa pomme, 1 anse ; panier 4 rangs tressés de 10 bosses, 1 rang teint,
  1 bord roulé, 1 anse.
- **Détails porteurs de sens** : les têtes rondes des poteaux d'angle, la terre bombée au milieu (le pied de la plante),
  la bande sauge de la housse et son panneau ; la pomme laiton de l'arrosoir ; le rang rose du panier ; pour les plantes,
  les coeurs (jaunes des marguerites, sombre du coquelicot avec sa capsule claire, brun du tournesol), les calices verts
  des fraises et des boutons, les points de graines des fraises, les rayures du melon, le tuteur de bambou du pois de
  senteur.
- **Imperfections voulues** : le haut des planches se bombe un peu, une planche est un peu plus haute ; la toile se gonfle
  et son ourlet ondule ; les pas varient en taille et vont un peu à gauche et à droite ; feuilles et fleurs varient en
  longueur, inclinaison et rotation (graines fixes) ; les fleurs se tournent un peu autour de la direction de la caméra.
- **Rien de pointu** : feuilles en lentilles arrondies, pétales à pointe ronde entre deux épaules (jamais une étoile),
  épis en perles, blé sans barbes, fanes de carotte en folioles rondes, sépales et calices à lobes ronds.
- **Palette visée** : bois chaud, terre brun chaud (jamais noire), toile avoine et sauge, pierre crème, osier miel ; chaque
  plante a sa propre rangée `palette.garden.<plante>` (tige, feuille, puis ses couleurs).

## Sources

| Fichier | Rôle |
| --- | --- |
| `SourceArt/Blender/Tools/pw_garden.py` | le décor et le générateur paramétrique unique des plantes (tiges, feuilles rondes, folioles, rosettes, fleurs à pétales ronds, sphères, épis, couronne), palettes, budgets et emprises contrôlés, sauvegarde, export, images de revue |
| `SourceArt/Blender/Tools/pw_grassland.py` | fonctions partagées de la Prairie (régions `PW_Regions`, occlusion `PW_Occlusion`, `object_from_bmesh`) |
| `SourceArt/Blender/Tools/pw_nutdrop.py`, `pw_postcards.py` | formes de T40 et T42 réutilisées (tubes, lentilles, sphères, révolutions) et studio de revue |
| `SourceArt/Blender/Tools/pw_export.py` | réglages d'export FBX du projet (`FBX_PRESET`) |
| `SourceArt/Blender/Activities/BL_Activity_Garden.blend` | la scène `Garden` (collection `GD_EXPORT`), écrite par `bpy.data.libraries.write` depuis la session ouverte |
| `Assets/_Game/Art/Activities/Garden/GD_GardenSet.fbx` | `GD_Ground`, `GD_Bed`, `GD_BedCover`, `GD_Path`, `GD_Fence`, `GD_WateringCan`, `GD_Basket` |
| `Assets/_Game/Art/Activities/Garden/GD_GardenPlants.fbx` | `GD_Plant_<Plante>_<1 à 3>`, 39 objets |

L'en-tête des deux FBX nomme `SourceArt\Blender\Activities\BL_Activity_Garden.blend`, listé dans
`SourceArt/Handmade_Sources.json` avec ses cinq scripts. Aucun générateur, aucune bibliothèque de modèles.

## Mesures

Triangles comptés après construction ; `build_garden()` refuse un budget dépassé et liste tout écart.

| Décor | Triangles | Taille (x, y, z) |
| --- | --- | --- |
| `GD_Ground` | 600 | disque de 17,2 m, plat |
| `GD_Bed` | 814 | 1,04 x 0,88 x 0,27 m |
| `GD_BedCover` | 510 | 1,08 x 0,92 x 0,36 m (avec le panneau) |
| `GD_Path` | 675 | 15 pas sur 5,1 m |
| `GD_Fence` | 976 | 5,3 m, 0,46 m de haut |
| `GD_WateringCan` | 372 | 0,33 x 0,19 x 0,24 m |
| `GD_Basket` | 840 | 0,44 x 0,32 x 0,31 m (anse) |
| **Total des modèles** | **4 787** (budget 6 000) | |

| Plante | Stade 1 | Stade 2 | Stade 3 (mûre) : triangles, rayon, hauteur |
| --- | --- | --- | --- |
| Trèfle doux | 230 | 684 | 865, 0,31 m, 0,45 m |
| Fraise des bois | 168 | 748 | 836, 0,36 m, 0,24 m |
| Marguerite | 160 | 491 | 593, 0,25 m, 0,54 m |
| Carotte ronde | 264 | 746 | 692, 0,16 m, 0,53 m |
| Coquelicot | 309 | 414 | 489, 0,28 m, 0,59 m |
| Myrtillier | 243 | 836 | 876, 0,35 m, 0,50 m |
| Lavande | 168 | 872 | 872, 0,25 m, 0,60 m |
| Blé doré | 192 | 804 | 804, 0,24 m, 0,60 m |
| Indigo des prés | 157 | 579 | 885, 0,20 m, 0,59 m |
| Tournesol | 158 | 294 | 406, 0,21 m, 0,98 m |
| Pommier nain | 166 | 836 | 788, 0,36 m, 0,88 m |
| Melon rond | 234 | 687 | 735, 0,35 m, 0,24 m |
| Pois de senteur | 365 | 505 | 813, 0,23 m, 0,60 m |

Chaque stade tient entre 150 et 900 triangles (157 à 885). Pivot : la racine sur la terre (z = 0 au sommet de la terre,
au centre de la parcelle) ; les tiges partent 12 à 17 mm sous la terre (enracinées).

## Aucune collision (mesuré)

- **Housse et parcelle** (Blender, BVH) : aucune paire de triangles ne se croise, chaque sommet de la housse reste à 5,8 mm
  au moins de la parcelle ; la toile passe 6 mm au-dessus des têtes de poteaux, la jupe tombe 13 à 16 mm hors des planches.
- **Plantes dans la terre** (Blender, BVH, chaque stade posé à la racine d'une parcelle) : rien au-dessus de la terre ne
  croise une planche, un poteau ou une motte ; aucune partie non enracinée sous la terre ; rayon de 0,36 m au plus (la
  terre en tolère 0,38). Le pipeline et le test refont la mesure dans Unity : chaque point au-dessus de la terre reste à 5
  mm au moins du bord de la terre.
- **Décor** (Blender puis Unity) : emprises vues du dessus (enveloppes convexes) des 5 parcelles, des 2 housses, des 15
  pas (un par un), de la clôture par tronçons, de l'arrosoir, du panier et des pièces du kit : plus petit écart 5,5 cm
  (housse de la parcelle 4 et un pas) dans la maquette Blender ; le pipeline exige 2 cm entre toutes les paires.
- **Cadrage** : caméra portrait (0 ; 8,46 ; -7,65) visant (0 ; 0,3 ; 0,8), champ 30°. Chaque parcelle et la plus haute
  plante mûre (le tournesol, posé dans n'importe quelle parcelle) restent entre la barre du haut (300 px) et la feuille du
  bas (700 px) : environ 45 px de marge en bas, 24 px en haut, 24 px sur les côtés (calcul identique à
  `GardenSetPipeline.FramingMargins`).

## Palettes proposées

Même rangement que `DEF_Palettes_Grassland` : base, R, G, B, A, occlusion, accent (valeurs dans `GD_PALETTES`, reprises
par `GardenSetPipeline` dans `DEF_Palettes_Garden`).

| Palette | Base | R | G | B | A | Occlusion |
| --- | --- | --- | --- | --- | --- | --- |
| `palette.garden.bed` | planches `BF8758` | dessus `DDB07E` | terre `6A4B3B` | mottes `8C6A52` | poteaux `A8734C` | `5A4446` |
| `palette.garden.cover` | toile `E6D2AC` | bande sauge `9FBF88` | bois du panneau `C9955F` | face `F3DDB0` | bord `A9764B` | `857892` |
| `palette.garden.path` | pierre `CDBFA6` | dessus `DCD0B8` | mousse `9FBF74` | moucheture `CDBFA9` | | `6E6A7E` |
| `palette.garden.fence` | bois `D4A874` | têtes `E9C796` | lisses `C49460` | mousse `8DAA5C` | | `6A5250` |
| `palette.garden.watering_can` | bleu-vert `7FBFC4` | reflets `B4DEDD` | laiton `E3BE62` | anse `5E9FA8` | intérieur `35585E` | `4B5C7A` |
| `palette.garden.basket` | osier `D3A05E` | bosses `EBC98C` | bord, anse `E4BB7C` | intérieur `B9884D` | rang teint `DC8C7E` | `6F5048` |
| `palette.garden.ground` | pré `8DC166` | clair `A6D07A` | sombre `7DB55C` | terre battue `B9A27C` | | `4E6D5D` |

Plantes (`palette.garden.<plante>`, base = tiges, R = feuilles) : trèfle (fleurs `EE8FB4`, pointes `FFD3E3`), fraise
(pétales `FFFCF4`, fruit `E2403D`, coeurs et graines `F5C84C`), marguerite (`FFFDF7`, coeur `F2C24F`), carotte (racine
`EE8630`), coquelicot (pétales `E8463B`, coeur `3B2C35`, capsule `B9D58E`), myrtillier (clochettes `F4B5C6`, baies
`4C69C6`), lavande (épis verts `98BE6C`, violets `9474D2`, pointes `C8B3EE`), blé (or `E8BE55`), indigo (fleurs `3D52B8`),
tournesol (pétales `F8C531`, coeur `7A4A2B`), pommier (pommes `E04B42`, fleurs `FFF6F2` et `F4A6BB`), melon (fleurs
`F4CF46`, écorce `C8D98A` et rayures `7FAF5A`), pois de senteur (rose `F5A3C6`, mauve `B69BE0`, tuteur `D9BE84`).

## Grille

Vert : tenu. Suivi : à vérifier dans Unity par la session principale. « s.o. » : sans objet.

| N° | Point | Résultat |
| --- | --- | --- |
| 1 | Silhouette à 64 px | vert : planche `T43_props_silhouettes.png` (chaque plante mûre à environ 64 px) : trèfle, fraises, marguerites, carotte, coquelicots, buisson, épis, tournesol, arbre, melons et tuteur se distinguent |
| 2 | Volumes distincts, jonctions lisibles | vert : planches et poteaux, terre et bois, toile et jupe, corps et bec ; tiges, feuilles, fleurs et fruits séparés |
| 3 | Détails utiles, zones de repos | vert : la terre et le dessus de la toile restent calmes ; les détails sont sur les plantes |
| 4 | Symétrie | vert : décor symétrique, plantes volontairement asymétriques (graines fixes) |
| 5 à 9 | Pattes, anatomie, yeux | s.o. |
| 10 | Parties comptables constantes | vert : un seul niveau de détail par modèle, comptes fixés dans la fiche |
| 11 | Silhouette de chaque LOD | s.o. (un seul niveau, une seule distance) |
| 12 | Aucune texture peinte, aucun bruit | vert : régions de sommets et palettes |
| 13 | Matière mate | vert pour `SH_EnvironmentOpaque` |
| 14 | Bandes 60/30/10 | suivi : à mesurer sur la capture Unity |
| 15 | Le sujet se détache | vert : chaque plante mûre a une couleur forte sur la terre sombre (`T43_props_set.png`) ; suivi Unity |
| 16 | Ombres froides, lumière chaude | vert : occlusions brun violacé, bleu lilas, vert bleuté ; aucun gris |
| 17 | Matière reconnaissable | vert : bois, terre, toile, pierre, osier, métal peint, feuilles, pétales, fruits |
| 18 | Une seule lumière, aucune lumière peinte | vert |
| 19 | Matin, jour, soir | s.o. (le potager a sa propre lumière de jour) |
| 20 | Belle sans post-traitement | vert dans Blender ; suivi Unity |
| 21 | Contact au sol | vert : planches jusqu'au pré avec une ombre de pied, pas posés dans le pré, poteaux de clôture enracinés, ombre au pied de chaque plante |
| 22 à 29 | Animation, famille d'animaux | s.o. (le balancement des plantes mûres est fait par le jeu) |
| 30 | Rien n'entre dans rien | vert : mesures ci-dessus ; suivi : le test `GardenSetTests` dans Unity |
| 31 | Taille juste | vert : parcelles d'environ 1 x 0,8 m, plantes qui remplissent leur parcelle sans en sortir |
| 32 | Rien ne flotte | vert : feuilles basses au-dessus de la terre, melons posés à 4 mm au-dessus, panneau posé sur la toile |
| 33 à 37 | Interface et icônes | s.o. |
| 38 | Aucune étoile à quatre branches | vert : pétales ronds, cinq lobes au moins, aucune étoile |
| 39, 40 | Effets | s.o. |
| 41 | Source éditable, export reproductible | vert : `.blend` et scripts listés dans `SourceArt/Handmade_Sources.json` |
| 42 | Aucun générateur par IA | vert |
| 43 | Vocabulaire | vert |
| 44 | Fait par la même main | vert : maquette `T43_props_set.png` avec les collines du toilettage, même famille que les décors de T38, T40 et T42 |

## Images

- `T43_props_set.png` : maquette du potager par la caméra du décor Unity (1080 x 2340 à demi-taille) : cinq parcelles, un
  tournesol, des fraises et un pommier mûrs dans les trois premières, les housses sur les parcelles 3 et 4, les pas, la
  clôture, l'arrosoir, le panier, les collines du toilettage (le kit d'herbe est ajouté dans Unity).
- `T43_props_closeup.png` : une parcelle avec des coquelicots mûrs, une parcelle couverte et son panneau, l'arrosoir, le
  panier.
- `T43_props_plants_light.png`, `T43_props_plants_dark.png` : chaque plante en trois stades côte à côte, vue sous l'angle
  de la caméra du décor, sur fond clair et sur fond sombre.
- `T43_props_silhouettes.png` : les 13 plantes mûres en noir sur blanc, environ 64 px chacune.

## Suivis

- Revue dans Unity : `T43_garden_set.png` et `T43_garden_plants.png` (pipeline), points 14, 15 et 20 ; le buisson de
  myrtilles et la couronne du pommier gardent quelques facettes de près.
- Les stades 1 sont volontairement petits (des pousses) : à juger à la taille du téléphone.
