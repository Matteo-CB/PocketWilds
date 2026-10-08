# T38 : revue « fait main » du décor et des accessoires du toilettage

8 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11) appliquée au décor du toilettage
(Prairie) et à ses accessoires. Images rendues dans Blender (EEVEE, lumière unique venant du haut et de la gauche de
l'image, couleurs calculées depuis les régions et les palettes comme le fait `SH_EnvironmentOpaque`), à côté de ce
fichier (préfixe `T38_props_`). La revue finale dans Unity, avec les vrais shaders, la lumière du décor et les trois
paliers, reste à faire par la session principale (voir « Suivis »).

## Fiche d'intention

- **Caractère** : doux, douillet, tactile. Un petit diorama (une butte d'herbe ronde, un coussin tricoté, des collines
  rondes derrière) et des accessoires trapus, ronds, un peu plus gros que nature, lisibles sur un téléphone dans le gros
  plan du toilettage.
- **Échelle** : mesurée sur les vrais animaux (LOD0 du Lapin, de l'Écureuil et du Hérisson, lecture seule). Lapin :
  0,30 m de large, 0,51 m de long, 0,65 m avec les oreilles, pattes dans un rayon de 0,22 m, `SOCK_HeadTop` à 0,438 m sur
  le pelage, 3 cm libres devant les oreilles. Écureuil : 0,54 m, `SOCK_HeadTop` à 0,432 m. Hérisson : 0,32 m, son
  `SOCK_HeadTop` est 2 cm sous les piquants (haut du pelage à 0,279 m).
- **Parties comptables** (identiques dans chaque export) : brosse 7 touffes ; marguerites 5 pétales ronds ; trèfle
  3 folioles ; bardane 12 picots ronds ; nœud 2 boucles, 2 pans, 1 cœur de nœud.
- **Détails porteurs de sens** : l'ovale incrusté sur le dos de la brosse (on reconnaît une brosse en bois, la version
  dorée le reprend), le pli sur le devant de chaque boucle du nœud et les pans coupés en V (un ruban), la nervure claire
  de la feuille, les bouts clairs de la brindille (bois sous l'écorce), la base jaune du pétale (là où il tenait à la
  fleur), les rangs crème du coussin (tricot).
- **Imperfections voulues** : le contour de la butte ondule de 4 % ; touffes d'herbe, fleurs, trèfles et arbres posés
  par des graines fixes ; la brindille, le brin d'herbe et la feuille légèrement courbés, la pointe relevée.
- **Palette visée** : celle de la Prairie (vert de prairie `8DC166`, occlusion vert bleuté froid `4E6D5D`), coussin
  rose poudré et crème, bois miel, nœuds rose, bleu ciel, crème.

## Sources

| Fichier | Rôle |
| --- | --- |
| `SourceArt/Blender/Tools/pw_grooming.py` | construction de chaque objet (chaque forme est une valeur écrite), palettes proposées, sauvegarde, export, images de revue |
| `SourceArt/Blender/Tools/pw_grassland.py` | fonctions partagées de la Prairie (régions `PW_Regions`, occlusion `PW_Occlusion`, `object_from_bmesh`, feuille de tige) |
| `SourceArt/Blender/Tools/pw_export.py` | réglages d'export FBX du projet (`export_static`) |
| `SourceArt/Blender/Activities/BL_Activity_Grooming.blend` | la scène `Grooming` (collection `EXPORT`), écrite par `bpy.data.libraries.write` depuis la session ouverte, sans toucher au fichier de la Prairie |
| `Assets/_Game/Art/Activities/Grooming/*.fbx` | `GR_Set_Prairie`, `GR_Brush`, `GR_Debris`, `GR_Burr`, `GR_Bow`, `GR_SoapBubble` ; l'en-tête de chaque FBX nomme `BL_Activity_Grooming.blend` |

Aucun générateur, aucune bibliothèque de modèles : tout est construit pas à pas par le script (anneaux, tubes,
ellipsoïdes, profils de révolution). Les seules références sont celles du projet : le kit de la Prairie
(`pw_grassland.py` : touffes d'herbe, marguerite à cinq pétales, trèfle, arbres ronds, rangement des données), les trois
animaux pour l'échelle et la couleur du Lapin brun (`variant.rabbit.brown`) pour les images.

## Mesures

Tailles dans Blender (x, y, z) ; dans Unity (-x, z, -y). Triangles comptés après construction ; chaque budget est
contrôlé par `build_grooming()`, qui refuse de dépasser.

| Objet | Triangles (budget) | Taille | Pivot |
| --- | --- | --- | --- |
| `GR_SetMound` (butte, coussin, fleurs) | 2 440 (2 500) | butte 1,29 m de large, coussin 0,71 m, 0,30 m de haut | centre du dessus du coussin, plat et de niveau (z = 0) |
| `GR_SetBackdrop` (sol, collines, arbres, marguerites) | 2 906 (3 000) | 16,9 x 10,9 x 3,1 m | même origine ; le sol est à -0,30 m |
| `GR_Brush` | 568 (600) | 11,7 x 5,2 x 3,8 cm | centre des pointes des poils |
| `GR_Debris_GrassBlade` | 96 (120) | 4,3 x 1,2 cm | contact, à plat |
| `GR_Debris_DaisyPetal` | 84 (120) | 2,9 x 1,3 cm | contact, à plat |
| `GR_Debris_CloverLeaf` | 92 (120) | 2,6 x 2,6 cm | contact, à plat |
| `GR_Debris_Leaf` | 116 (120) | 4,1 x 2,0 cm | contact, à plat |
| `GR_Debris_Twig` | 90 (120) | 4,3 x 1,9 cm | contact, à plat |
| `GR_Debris_Seed` | 114 (120) | 3,8 x 1,5 cm | contact, à plat |
| `GR_Burr` | 296 (300) | 3,7 x 3,2 x 3,1 cm | sous le milieu des trois picots d'appui |
| `GR_Bow` | 384 (400) | 6,3 x 1,4 x 3,1 cm | point d'attache sous le cœur du nœud |
| `GR_SoapBubble` | 288 (300) | rayon 1 (mis à l'échelle dans Unity) | centre |

## Aucune collision (mesuré)

- Les pattes du Lapin sont à 2 mm au-dessus du coussin, celles de l'Écureuil et du Hérisson le touchent (z = 0) : aucun
  animal n'entre dans le coussin ; le dessus est plat jusqu'à 0,235 m du centre (les pattes du Lapin tiennent dans
  0,22 m).
- Le dessous du coussin est 0,1 mm au-dessus du plateau de la butte (contact).
- Fleurs, trèfles et touffes autour de la butte : au moins 4,3 cm de la surface de la butte, à au moins 3 cm les uns
  des autres, posés sur le sol (z = -0,30 m), jamais sur le coussin.
- Collines : les couronnes des arbres sont relevées tant qu'un point de la couronne serait sous une colline ; seuls les
  pieds des troncs sont enracinés, 3 cm au centre, comme les arbres de la Prairie (plus du côté amont d'une pente).
- Nœud posé sur `SOCK_HeadTop` (pose de repos) : ne touche ni le Lapin ni l'Écureuil ; le Hérisson demande 2 mm de plus
  (son socket est sous les piquants).
- Débris, bardane et brosse : pivot au contact (z = 0), rien sous le pivot ; la brosse se pose 1,2 cm au-dessus du
  pelage dans la planche (`T38_props_family.png`).

## Palettes proposées

Même rangement que `DEF_Palettes_Grassland` : base, R, G, B, A, occlusion, accent (valeurs dans `GROOMING_PALETTES`).

| Palette | Base | R | G | B | A | Occlusion |
| --- | --- | --- | --- | --- | --- | --- |
| `palette.grooming.set_mound` | herbe `8DC166` | coussin `E3A3A9` | rangs crème `F3DCCB` | pétales `F5EFE2` | cœurs `F2C94C` | `4E6D5D` |
| `palette.grooming.set_backdrop` | prairie `8DC166` | crêtes `D5E08B` | lointain `A9C9C2` | pétales `F5EFE2` | écorce `8F6B50` | `4E6D5D` |
| `palette.grooming.brush` | bois `C69467` | poils `EBDCBF` | incrustation `E3C08F` | `A87850` | bois | `6F5A57` |
| `palette.grooming.brush_gold` | or `E2B24A` | poils `EBDCBF` | incrustation `F7DC8E` | `C2902F` | or | `8A6A4A` |
| `palette.grooming.debris` | vert `86BD5E` | vert clair `C3DF8A` | crème `F6EFE0` | écorce `9C7352` | jaune `F0CB5A` | `4E6650` |
| `palette.grooming.burr` | olive `8A7B48` | picots `B8A764` | | | | `4E4733` |
| `palette.grooming.bow_pink` / `_sky` / `_cream` | `F2AAC1` / `9EC8EE` / `F5E7CA` | idem | nœud `DE8CA9` / `7FAEDD` / `E5CFA6` | | | `9A6A88` / `5F7AA0` / `9C8A78` |

Valeurs : coussin L* 73 (Lapin brun 56, blanc 94, sakura 85, noir 21, gris 68 mais sans chroma : ΔE nettement plus
grand). Le coussin bleu ciel essayé d'abord se lisait comme une flaque, l'abricot comme un biscuit : le rose poudré à
rangs crème se lit comme un tissu.

## Grille

Vert : tenu. Suivi : à vérifier dans Unity par la session principale. « s.o. » : sans objet.

| N° | Point | Résultat |
| --- | --- | --- |
| 1 | Silhouette à 64 px | vert : brosse, nœud, bardane, feuille, trèfle, pétale, brin, brindille, graine se lisent sur fond clair et sombre (`T38_props_silhouettes_light.png`, `_dark.png`) |
| 2 | Volumes distincts, jonctions lisibles | vert : dos, manche et touffes de la brosse ; boucles, pans et nœud ; butte et coussin séparés par un pli d'occlusion |
| 3 | Détails utiles, zones de repos | vert : chaque détail est dans la fiche ; le dessus du coussin et les collines restent simples |
| 4 | Symétrie | vert : brosse et nœud symétriques ; les débris sont des formes naturelles, leur courbure est écrite dans la fiche |
| 5 à 9 | Pattes, anatomie, yeux | s.o. |
| 10 | Parties comptables constantes | vert : un seul niveau de détail, comptes fixés dans la fiche |
| 11 | Silhouette de chaque LOD | s.o. (accessoires de gros plan, décor à distance fixe) |
| 12 | Aucune texture peinte, aucun bruit | vert : régions de sommets et palettes ; seules les grandes taches claires du sol, comme le sol de la Prairie |
| 13 | Matière mate | vert pour `SH_EnvironmentOpaque` ; suivi : le matériau doré de la brosse dorée reste mat (pas de reflet de plastique) |
| 14 | Bandes 60/30/10 | suivi : à mesurer sur la capture Unity du gros plan (l'herbe garde le C* 52 de la Prairie, décision D de T34E) |
| 15 | L'animal se détache | suivi : coussin L* 73, collines 72 à 87 ; à mesurer pour chaque variante dans Unity |
| 16 | Ombres froides, lumière chaude | vert : occlusions vert bleuté, brun violacé, rose violacé ; aucun gris |
| 17 | Matière reconnaissable | vert : bois, poils, tissu tricoté, ruban, herbe, feuilles, écorce |
| 18 | Une seule lumière, aucune lumière peinte | vert : seule l'occlusion est peinte ; les crêtes claires suivent la pente, pas le soleil |
| 19 | Matin, jour, soir | suivi Unity |
| 20 | Belle sans post-traitement | vert dans Blender ; suivi Unity |
| 21 | Contact au sol | vert : occlusion au pied de la butte et anneau d'ombre sur le sol, pli sous le coussin, dessous des débris assombri |
| 22 à 29 | Animation, famille d'animaux | s.o. |
| 30 | Rien n'entre dans rien | vert : mesures ci-dessus ; nœud sur le Hérisson : +2 mm (suivi stage) |
| 31 | Taille juste | vert : brosse 11,7 cm pour un Lapin de 0,45 m, débris 2,6 à 4,3 cm, nœud 6,3 cm, bardane 3,7 cm |
| 32 | Rien ne flotte | vert : pivots au contact, butte posée sur le sol du décor |
| 33 à 37 | Interface et icônes | s.o. |
| 38 | Aucune étoile à quatre branches | vert : marguerites à cinq pétales ronds, picots ronds, aigrette de pissenlit en boule (jamais en rayons) |
| 39, 40 | Effets | s.o. (la bulle de savon est la forme de l'effet, son matériau est fait dans Unity) |
| 41 | Source éditable, export reproductible | vert : `.blend` et scripts listés dans `SourceArt/Handmade_Sources.json` |
| 42 | Aucun générateur par IA | vert |
| 43 | Vocabulaire | vert |
| 44 | Fait par la même main | vert : planche `T38_props_family.png` (Lapin brun, nœud, débris, bardane, brosse) |

## Images

- `T38_props_set_right.png`, `T38_props_set_left.png` : le gros plan de trois quarts (caméra à 2,1 m de l'animal,
  champ vertical de 32°, écran portrait) : derrière l'animal, seulement des collines et le ciel.
- `T38_props_set_wide.png` : le diorama entier (les collines sont faites pour le gros plan, pas pour une vue d'en haut).
- `T38_props_brush.png` : la brosse en bois, la brosse dorée, le dessous.
- `T38_props_debris.png` : les six débris, la bardane, la bulle.
- `T38_props_bows.png` : les trois nœuds.
- `T38_props_family.png` : le Lapin brun sur son coussin, nœud sur la tête, débris et bardane sur le pelage, brosse.
- `T38_props_silhouettes_light.png`, `T38_props_silhouettes_dark.png` : silhouettes à 64 px.

## Suivis

- Revue dans Unity (gros plan, trois paliers, matin, jour, soir) avec les palettes ajoutées par le pipeline : points 13
  à 15, 19 et 20.
- Le Hérisson : nœud relevé de 2 mm au moins au-dessus de `SOCK_HeadTop`.
- En très gros plan, les picots de la bardane et les boucles du nœud montrent quelques facettes ; à la taille du jeu
  (3,7 et 6,3 cm sur un animal de 0,45 m) elles ne se voient pas.
