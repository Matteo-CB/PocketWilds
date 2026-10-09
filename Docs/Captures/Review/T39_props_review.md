# T39 : revue « fait main » des hautes herbes et des buissons fleuris de la Prairie

9 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11) appliquée aux décors de cachette
du cache-cache (doc 01 Contenu, S-ACT-002 : « hautes herbes, derrière les petits arbres, derrière les buissons
fleuris »). Le jeu se joue dans le refuge : ces décors sont des props permanents des huit zones de la Prairie, posés
parmi le kit T33 (arbres, `GL_Bush_A/B/C`, rochers, touffes de la prairie) par le pipeline des zones. Ils suivent donc
la convention de la Prairie : objets `GL_*`, FBX `GL_Props_T39.fbx` dans `Assets/_Game/Art/Environment/Grassland/Models/`.

Images rendues dans Blender (EEVEE, sans fenêtre, une seule lumière venant du haut et de la gauche de l'image,
couleurs calculées depuis les régions et les palettes comme le fait `SH_EnvironmentOpaque`, caméra du refuge :
inclinaison 40°, champ vertical 30°), à côté de ce fichier (préfixe `T39_props_`). La revue finale dans Unity, dans les
vraies zones, avec les vrais shaders et les trois paliers, reste à faire (voir « Suivis »).

## Fiche d'intention

- **Caractère** : généreux, doux, un peu dodu. Une touffe de hautes herbes se lit comme une grosse touffe verte
  derrière laquelle un lapin peut s'asseoir, jamais comme un champ de tiges fines ; un buisson fleuri est un buisson de
  la prairie (même méthode que `GL_Bush_A/B`) qui porte des marguerites rondes.
- **Échelle** : mesurée sur les modèles LOD0 (lecture seule). Lapin : 0,30 m de large, 0,51 m de long, 0,65 m avec
  les oreilles. Écureuil : 0,54 m, 0,62 m de long avec la queue. Hérisson : 0,32 m. `GL_Bush_A` 0,96 x 0,78 x 0,57 m,
  `GL_Bush_C` 0,79 m, `GL_GrassWisp_L` 0,54 m, `GL_GrassTuft_L` 0,31 m. Les hautes herbes font 0,43 à 0,59 m (bien plus
  hautes et pleines que les touffes de la prairie), les buissons fleuris 0,56 et 0,59 m, comme les buissons.
- **Parties comptables** (identiques dans chaque export) : chaque touffe a 28 brins (4 droits au centre, 6 éventails
  de 3, 6 brins courts en jupe) ; A porte 3 trèfles roses, B 3 têtes de graines rondes, C 2 trèfles blancs et 1 rose ;
  chaque marguerite a 5 pétales ronds et un cœur bombé ; buisson A 8 marguerites, buisson B 7 ; 16 écailles de feuilles
  par buisson.
- **Détails porteurs de sens** : les pointes plus claires des brins (l'herbe au soleil), les têtes de trèfle rose et
  blanc au pied de la touffe, les têtes de graines couleur paille au-dessus de B, le cœur jaune des marguerites (on
  reconnaît la fleur à 64 px), les marguerites posées aussi sur le dessus du buisson (ce que la caméra du refuge voit
  le plus).
- **Imperfections voulues** : les brins penchent, se courbent, se croisent et tournent selon des graines fixes ; les
  éventails penchent un peu de côté pour ne pas dessiner une étoile ; les couronnes sont l'union de lobes inégaux ; C
  est une touffe basse et allongée (1,42 x 0,85).
- **Rien de piquant** : brins larges, resserrés à la racine, plus larges en bas, terminés par une ogive douce (épaules
  arrondies avant la pointe), jamais une aiguille ; trèfles et têtes de graines en boules ; pétales en lobes ronds.
- **Palette visée** : celle de la Prairie (`palette.grassland.grass` et `palette.grassland.bush_blossom`), pétales
  rose poudré, crème et jaune beurre, cœurs jaune d'or.

## Sources

| Fichier | Rôle |
| --- | --- |
| `SourceArt/Blender/Tools/pw_grassland_t39_props.py` | construction de chaque objet (chaque forme est une valeur écrite), palettes proposées, sauvegarde, export, images de revue |
| `SourceArt/Blender/Tools/pw_grassland.py` | fonctions partagées de la Prairie (`PW_Regions`, `PW_Occlusion`, `object_from_bmesh`, décimation, écaille de feuille) |
| `SourceArt/Blender/Tools/pw_grassland_t33.py` | méthode des couronnes des buissons (même union de lobes, même peinture du feuillage) |
| `SourceArt/Blender/Tools/pw_export.py` | réglages d'export FBX du projet (`FBX_PRESET`) |
| `SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_T39.blend` | la scène `HideSeek` (collection `GL39_EXPORT`), écrite par `bpy.data.libraries.write` depuis la session ouverte, sans toucher au fichier de la Prairie |
| `Assets/_Game/Art/Environment/Grassland/Models/GL_Props_T39.fbx` | `GL_TallGrass_A/B/C`, `GL_FlowerBush_A/B` ; l'en-tête du FBX nomme `BL_Biome_Grassland_T39.blend` |

Aucun générateur, aucune bibliothèque de modèles : tout est construit pas à pas par le script (brins à rangées,
ellipsoïdes, union de lobes, disques de pétales). Les seules références sont celles du projet : le kit de la Prairie
(touffes, buissons, écailles, rangement des données), les trois animaux pour l'échelle et leurs premières couleurs
(lapin brun, écureuil roux, hérisson brun) pour les images.

## Mesures

Tailles dans Blender (x, y, hauteur au-dessus du sol) ; dans Unity (-x, z, -y). Triangles comptés après construction ;
`build_t39_props()` refuse tout dépassement de budget.

| Objet | Triangles (budget) | Taille au sol | Hauteur | Enracinement | Rayon max | Pivot |
| --- | --- | --- | --- | --- | --- | --- |
| `GL_TallGrass_A` (trèfles roses) | 474 (500) | 0,55 x 0,59 m | 0,54 m | 2 cm | 0,32 m | centre du pied, au sol |
| `GL_TallGrass_B` (têtes de graines) | 474 (500) | 0,58 x 0,56 m | 0,59 m (brins 0,48 m) | 2 cm | 0,33 m | centre du pied, au sol |
| `GL_TallGrass_C` (basse et longue) | 474 (500) | 0,65 x 0,55 m | 0,43 m | 2 cm | 0,34 m | centre du pied, au sol |
| `GL_FlowerBush_A` | 642 (650) | 0,86 x 0,79 m | 0,59 m | 4 cm (comme `GL_Bush`) | 0,45 m | centre du pied, au sol |
| `GL_FlowerBush_B` | 596 (650) | 0,87 x 0,71 m | 0,56 m | 4 cm | 0,44 m | centre du pied, au sol |

## Données

- `PW_Regions` (couleur par sommet, octets) et une seule carte d'UV `PW_Occlusion` : x occlusion (racines et cœur de
  la touffe plus sombres, 0 à 0,80 ; buissons 0 à 0,55 comme `GL_Bush`), y hauteur (0 au sol, 1 à la pointe de chaque
  brin pour les herbes ; 0 au pied, 1 au sommet pour les buissons), lue par `SH_FoliageWind` pour le vent. Aucun
  matériau, comme les autres props de la Prairie.
- Hautes herbes : base vert profond, R pointes claires, G têtes de graines paille, B trèfles roses, A trèfles blancs.
- Buissons fleuris : comme `GL_Bush` (R feuillage, G côté soleil, B dessous ombré), A pétales ; le buisson n'a pas
  d'écorce, la couleur de base sert donc aux cœurs des marguerites.
- Normales : touffes éclairées comme un seul dôme (comme `GL_GrassTuft`) ; couronnes et écailles tournées vers le
  centre de la couronne (comme `GL_Bush`) ; chaque marguerite garde sa propre normale, un peu bombée.

## Aucune collision (mesuré)

- Trèfles et têtes de graines : poussés vers l'extérieur 5 mm par 5 mm jusqu'à garder 4 mm d'air autour de la tête et
  7 mm autour de la tige face à chaque brin (contrôle dans le script, qui refuse sinon) ; recoupement final : 0 tête
  dans un brin.
- Marguerites : aucun bord de pétale dans la couronne (3 mm de marge), aucune ne touche une écaille (test de
  recouvrement des maillages et 1 cm d'air), aucune ne touche une autre (écart de 2,6 rayons).
- Planche de cache-cache (`T39_props_hide.png`) : chaque animal recule derrière son décor jusqu'à n'y entrer nulle part ;
  écart mesuré au plus près : lapin 1,7 cm, écureuil 1,0 cm, hérisson 1,6 cm.
- Rien sous le pivot sauf les racines (brins 2 cm, couronnes 4 cm), comme les touffes et buissons de la Prairie.

## Palettes proposées

Même rangement que `DEF_Palettes_Grassland` : base, R, G, B, A, occlusion, accent (valeurs dans `T39_PALETTES`).

| Palette | Base | R | G | B | A | Occlusion | Accent |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `palette.grassland.tall_grass` | vert `66A253` | pointes `9FCE78` | graines `D0CB9A` | trèfle rose `F3C4D6` | trèfle blanc `F8F4EA` | `476F3E` | `B9D56C` |
| `palette.grassland.flower_bush_pink` | cœurs `F2C94C` | feuillage `73B25A` | soleil `A2D27E` | ombre `4F8A46` | pétales `F3BDD3` | `355E33` | `FFFFFF` |
| `palette.grassland.flower_bush_cream` | cœurs `F2C94C` | `73B25A` | `A2D27E` | `4F8A46` | pétales `F8F1DD` | `355E33` | `FFFFFF` |
| `palette.grassland.flower_bush_yellow` | cœurs `F29C4C` | `73B25A` | `A2D27E` | `4F8A46` | pétales `F7DF7A` | `355E33` | `FFFFFF` |

Les verts reprennent les rangées de la Prairie (`grass` pour les herbes, `bush_blossom` pour les buissons) : la famille
ne change pas. Valeurs (L*, C*) : brins 61/49 à 78/49 (le C* 52 de la Prairie, décision D de T34E), feuillage 52 à 79,
pétales roses 82, crème 95, jaunes 89, cœurs 82 ; lapin brun 56, écureuil roux 55, hérisson 61 : l'animal reste plus
sombre que les fleurs et se détache sur le vert.

## Grille

Vert : tenu. Suivi : à vérifier dans Unity. « s.o. » : sans objet.

| N° | Point | Résultat |
| --- | --- | --- |
| 1 | Silhouette à 64 px | vert : trois touffes (B avec ses têtes, C plus large) et deux buissons lisibles sur fond clair et sombre (`T39_props_silhouettes_light.png`, `_dark.png`) |
| 2 | Volumes distincts, jonctions lisibles | vert : centre droit, éventails, jupe ; couronne, écailles, fleurs |
| 3 | Détails utiles, zones de repos | vert : chaque détail est dans la fiche ; les flancs des couronnes restent simples |
| 4 | Symétrie | vert : formes naturelles, asymétrie écrite dans la fiche (graines fixes, lobes inégaux) |
| 5 à 9 | Pattes, anatomie, yeux | s.o. |
| 10 | Parties comptables constantes | vert : un seul niveau de détail, comptes fixés dans la fiche |
| 11 | Silhouette de chaque LOD | s.o. (un seul maillage, comme les buissons et touffes de la Prairie) |
| 12 | Aucune texture peinte, aucun bruit | vert : régions de sommets et palettes |
| 13 | Matière mate | vert pour `SH_EnvironmentOpaque` et `SH_FoliageWind` |
| 14 | Bandes 60/30/10 | suivi : à mesurer sur une capture d'une zone avec ses nouveaux décors |
| 15 | L'animal se détache | vert dans Blender (`T39_props_hide.png`) ; suivi Unity |
| 16 | Ombres froides, lumière chaude | vert : occlusions vert bleuté de la Prairie, aucun gris |
| 17 | Matière reconnaissable | vert : herbe, trèfle, graines, feuillage, marguerites |
| 18 | Une seule lumière, aucune lumière peinte | vert : seule l'occlusion est peinte ; le côté soleil des couronnes suit la règle de `GL_Bush` |
| 19 | Matin, jour, soir | suivi Unity |
| 20 | Belle sans post-traitement | vert dans Blender ; suivi Unity |
| 21 | Contact au sol | vert : racines enfoncées et assombries, couronnes enracinées de 4 cm |
| 22 à 29 | Animation, famille d'animaux | s.o. (le vent vient du shader) |
| 30 | Rien n'entre dans rien | vert : mesures ci-dessus ; dans le monde, volumes mesurés par le pipeline des zones (suivi HW) |
| 31 | Taille juste | vert : touffes 0,43 à 0,59 m, buissons 0,56 et 0,59 m, à côté du lapin (0,65 m avec les oreilles) |
| 32 | Rien ne flotte | vert : pivots au sol, racines dessous |
| 33 à 37 | Interface et icônes | s.o. |
| 38 | Aucune étoile à quatre branches | vert : marguerites à cinq pétales ronds, trèfles et graines en boules |
| 39, 40 | Effets | s.o. |
| 41 | Source éditable, export reproductible | vert : `.blend` et scripts listés dans `SourceArt/Handmade_Sources.json` |
| 42 | Aucun générateur par IA | vert |
| 43 | Vocabulaire | vert |
| 44 | Fait par la même main | vert : planche `T39_props_family.png` (les cinq décors à côté de `GL_Bush_A`, `GL_GrassTuft_L`, `GL_GrassWisp_L` et du lapin) |

## Images

- `T39_props_family.png` : les cinq décors parmi le buisson, la touffe et le brin de la Prairie, avec le lapin brun.
- `T39_props_tallgrass.png` : les trois touffes (trèfles roses, têtes de graines, basse et longue).
- `T39_props_flowerbush.png` : buisson A rose, buisson B crème, buisson A en jaune (troisième palette).
- `T39_props_hide.png` : la caméra du refuge à 7 m (écran portrait) : oreilles du lapin derrière A, queue et oreilles
  de l'écureuil derrière le buisson A, piquants du hérisson derrière C.
- `T39_props_hide_game.png` : la même scène à 9,5 m, distance la plus proche de la caméra du refuge.
- `T39_props_silhouettes_light.png`, `T39_props_silhouettes_dark.png` : silhouettes à 64 px.

## Suivis

- Revue dans Unity, dans les zones de la Prairie (trois paliers, matin, jour, soir), avec les palettes ajoutées par le
  pipeline : points 14, 15, 19 et 20.
- Shader conseillé : hautes herbes sur `SH_FoliageWind` (elles ondulent avec la prairie et portent l'indice « herbe qui
  ondule » ; pas d'ombre portée, comme les touffes), buissons fleuris sur `SH_EnvironmentOpaque` comme `GL_Bush` (ombre
  portée). Sur `SH_FoliageWind`, l'accent `B9D56C` teinte un peu le haut des touffes d'une instance à l'autre, comme
  les touffes de la prairie ; l'accent des buissons (`FFFFFF`, inutilisé par `SH_EnvironmentOpaque`) ne doit pas servir
  avec `SH_FoliageWind`.
- En très gros plan, le pied des touffes montre les bases plates des brins ; à la distance du refuge (9,5 m et plus)
  et entourées de l'herbe de la prairie, elles ne se voient pas.
