# T40 : revue « fait main » du panier, des objets qui tombent et du noisetier

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11) appliquée aux accessoires de la Pluie
de noisettes (doc 01 Contenu, S-ACT-003) : le panier d'osier, les six objets qui tombent et le grand noisetier du décor.
Images rendues dans Blender (EEVEE, lumière unique venant du haut et de la gauche de l'image, couleurs calculées depuis
les régions et les palettes comme le fait `SH_EnvironmentOpaque`), à côté de ce fichier (préfixe `T40_props_`). La
revue finale dans Unity, avec les vrais shaders, la lumière du décor et les trois paliers, reste à faire par la session
principale (voir « Suivis »).

## Fiche d'intention

- **Caractère** : généreux, rond, chaud comme une récolte. Un panier d'osier dodu, des noisettes, glands et baies
  trapus qui se lisent d'un coup d'oeil pendant leur chute sur un téléphone, et un grand noisetier rond penché sur la
  scène avec ses grappes de noisettes, de la même famille que les accessoires de la Prairie (`pw_grassland`,
  `pw_grassland_t33`) et ceux du toilettage (`pw_grooming`).
- **Échelle** : mesurée sur les vrais animaux (LOD0, pose de repos, lecture seule). Lapin : `SOCK_HeadTop` à 0,438 m,
  tête de 0,19 m de large, oreilles qui montent de 3,5 cm derrière le socket jusqu'à 0,648 m. Écureuil : `SOCK_HeadTop`
  à 0,432 m, oreilles 4,6 cm au-dessus. Hérisson : `SOCK_HeadTop` à 0,260 m, piquants 6 cm au-dessus. Le panier fait
  0,32 m de large et 0,165 m de haut. Les objets qui tombent sont volontairement grands (0,13 à 0,17 m, la moitié du
  panier) pour se lire pendant la chute.
- **Parties comptables** (identiques dans chaque export) : panier 7 rangs tressés de 7 bosses, 2 rangs teints, 1 bord
  roulé ; noisette 7 stries ; gland 1 cupule à 2 bourrelets, 1 queue ; baies 3 et 1 feuille ; pomme de pin 18 écailles
  rondes en 4 rangs (5, 5, 5, 3) ; plume 1 vexille, 1 rachis ; noisetier 14 lobes, 1 tronc, 1 seconde tige, 1 branche,
  8 grappes (20 noisettes).
- **Détails porteurs de sens** : le tressage en damier (les bosses claires alternent d'un rang à l'autre, on reconnaît
  l'osier) et les deux rangs teints rose (un panier fait main), la calotte pâle de la noisette (la cicatrice d'attache),
  la cupule à bourrelets et à lèvre roulée du gland, le point sombre du calice de chaque baie (une baie d'aubépine), les
  écailles en tuiles de la pomme de pin, le duvet crème et l'encoche douce de la plume, les involucres à cinq lobes
  ronds des noisettes du noisetier (on reconnaît un noisetier, pas un pommier).
- **Imperfections voulues** : les bosses du tressage alternent rang par rang ; la plume est cambrée et sa pointe tourne
  à droite ; la couronne est une union de lobes inégaux ; feuilles et grappes sont posées par des graines fixes.
- **Rien de pointu** : écailles de pomme de pin en lentilles rondes qui épousent le coeur, pointes de la noisette et du
  gland en petits bouts arrondis, involucres à lobes ronds, plume sans barbes fendues.
- **Palette visée** : celle de la Prairie (verts de `palette.grassland.tree`, occlusions vert bleuté) ; osier miel,
  rangs teints rose poudré (le rose du coussin du toilettage), noisette châtaigne, gland fauve, baies rouge chaud,
  noisette dorée or profond, pomme de pin brun, plume blanc chaud et crème.

## Sources

| Fichier | Rôle |
| --- | --- |
| `SourceArt/Blender/Tools/pw_nutdrop.py` | construction de chaque objet (chaque forme est une valeur écrite), palettes proposées, sauvegarde, export, mesures (`measure_canopy`, `fit_basket`), images de revue |
| `SourceArt/Blender/Tools/pw_grassland.py` | fonctions partagées de la Prairie (régions `PW_Regions`, occlusion `PW_Occlusion`, `object_from_bmesh`, décimation) |
| `SourceArt/Blender/Tools/pw_export.py` | réglages d'export FBX du projet (`export_static`) |
| `SourceArt/Blender/Activities/BL_Activity_NutDrop.blend` | la scène `NutDrop` (collection `EXPORT`), écrite par `bpy.data.libraries.write` depuis la session ouverte, sans toucher au fichier de la Prairie ni à ses autres scènes |
| `Assets/_Game/Art/Activities/NutDrop/ND_Props.fbx` | `ND_Basket`, `ND_Hazelnut`, `ND_Acorn`, `ND_Berry`, `ND_GoldenNut`, `ND_Pinecone`, `ND_Feather` |
| `Assets/_Game/Art/Activities/NutDrop/ND_HazelTree.fbx` | `ND_HazelCanopy`, `ND_HazelTrunk` |

L'en-tête de chaque FBX nomme `SourceArt\Blender\Activities\BL_Activity_NutDrop.blend`, listé dans
`SourceArt/Handmade_Sources.json` avec ses trois scripts. Aucun générateur, aucune bibliothèque de modèles : tout est
construit pas à pas par le script (profils de révolution, tubes, ellipsoïdes, lentilles, union de lobes, remaillage et
décimation contrôlée de la couronne comme les arbres de la Prairie). Les seules références sont celles du projet : les
animaux (échelle et couleurs de leur première variante), le décor du toilettage, l'arbre fruitier et le buisson de la
Prairie pour les images.

## Mesures

Tailles dans Blender (x, y, z) ; dans Unity (-x, z, -y). Triangles comptés après construction ; chaque budget est
contrôlé par `build_nutdrop()`, qui refuse de dépasser.

| Objet | Triangles (budget) | Taille | Pivot |
| --- | --- | --- | --- |
| `ND_Basket` | 896 (900) | 0,316 x 0,316 x 0,164 m | centre du dessous : le pied annulaire pose sur z = 0, le dessous est creusé de 1 cm |
| `ND_Hazelnut` | 280 (300) | 0,132 x 0,129 x 0,129 m | centre de la boîte |
| `ND_Acorn` | 289 (300) | 0,114 x 0,114 x 0,161 m | centre de la boîte |
| `ND_Berry` | 283 (300) | 0,142 x 0,087 x 0,164 m | centre de la boîte |
| `ND_GoldenNut` | 280 (300) | 0,132 x 0,129 x 0,129 m (forme de la noisette) | centre de la boîte |
| `ND_Pinecone` | 296 (300) | 0,110 x 0,111 x 0,146 m | centre de la boîte |
| `ND_Feather` | 260 (300) | 0,073 x 0,020 x 0,170 m (face à la caméra) | centre de la boîte |
| `ND_HazelCanopy` | 3 576 | 3,68 x 2,13 x 1,71 m, de 2,61 à 4,32 m de haut | pied du tronc, au sol |
| `ND_HazelTrunk` | 254 | 1,62 x 0,83 x 3,22 m (pied évasé 3 cm sous le sol) | pied du tronc, au sol (même origine) |
| Noisetier entier | 3 830 (4 000) | | |

Intérieur du panier : fond à 2,8 cm (rayon 0,074 m), paroi de rayon 0,098 m à 3,6 cm, 0,121 m à 9,2 cm, bord intérieur
de rayon 0,134 m à 14,6 cm, haut du bord à 16,4 cm.

## Aucune collision (mesuré)

- **Panier sur la tête** (`fit_basket`, pose de repos, 4 mm d'écart au moins, mesuré sur le maillage) : posé sur
  `SOCK_HeadTop` sans décalage, le panier touche les oreilles de chaque espèce. Il faut l'avancer vers la caméra.
  Décalages trouvés (vers l'avant, puis hauteur au-dessus du socket) :

  | Espèce | Centré | Avancé de 6 cm | Avancé de 8 cm | Avancé de 10 cm | Avancé de 12 cm |
  | --- | --- | --- | --- | --- | --- |
  | Écureuil | +4,6 cm (sur le bout des oreilles) | +2,0 cm | **+0,2 cm** (posé sur la tête) | +0,4 cm | +0,2 cm |
  | Lapin | +18 cm (sur le bout des oreilles) | +7,0 cm | +1,4 cm | **+0,2 cm** (posé sur la tête) | 0 |
  | Hérisson | +5,4 cm (sur les piquants) | +5,2 cm | +3,8 cm | **+1,2 cm** | -0,4 cm |

  Le panier élargi (x 1,5 en largeur d'écran) donne les mêmes valeurs à 8 et 10 cm.
- **Objets attrapés** (`nd_drop_in`, mesuré) : le fond ne reçoit qu'un objet (la noisette repose 3 mm au-dessus) ; les
  suivants reposent sur lui sans entrer dans le tressage ni dans un autre objet (gland 10,8 cm au-dessus du fond, baies
  12,7 cm), le tas dépasse du bord (`T40_props_basket.png`).
- **Couloir de chute sous le noisetier** (`measure_canopy`) : le plan de jeu est le plan y = 0 du noisetier, entre
  x = 0,55 et 2,05 m (son milieu à 1,3 m à droite du tronc). Au-dessus de ce couloir, le dessous de la couronne est à
  2,739 m au plus bas ; la frange avant descend à 2,609 m mais se tient devant, vers la caméra (y = -0,9 m). Aucune
  grappe ne s'approche à moins de 0,222 m du plan, ni le tronc ou la branche à moins de 0,177 m : un objet qui tombe
  (rayon 8 cm au plus) ne rencontre rien.
- **Noisetier** : les bouts du tronc, de la seconde tige et de la branche sont dans la couronne (contrôlé par lancer de
  rayon, sinon la construction s'arrête) ; chaque grappe descend jusqu'à ce que chaque noisette soit hors de la couronne
  d'au moins un quart de son rayon ; aucune feuille n'est posée à moins de 0,22 m d'une grappe.
- **Objets** : chaque pièce d'un objet est un volume fermé ou cache son bout ouvert dans une autre (queues, rachis).

## Palettes proposées

Même rangement que `DEF_Palettes_Grassland` : base, R, G, B, A, occlusion, accent (valeurs dans `ND_PALETTES`).

| Palette | Base | R | G | B | A | Occlusion | Accent |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `palette.nut_drop.basket` | osier `B97A3C` | bosses `F0C987` | bord `E2AE68` | intérieur `A06C3A` | rangs teints `D9786A` | `5A3B33` | `FFFFFF` |
| `palette.nut_drop.hazelnut` | coque `B0703F` | calotte `E8CFA2` | stries `93582F` | `B0703F` | `B0703F` | `5E3B2E` | `FFFFFF` |
| `palette.nut_drop.acorn` | gland `C48D50` | cupule `8E6B4A` | bourrelets `B39370` | queue `6E4E38` | pointe `9C7A3E` | `5A4234` | `FFFFFF` |
| `palette.nut_drop.berry` | baie `D8453F` | calice `5A2E2C` | tiges `8A8A4E` | feuille `6FAE57` | joue `E9705A` | `7A2A3A` | `FFFFFF` |
| `palette.nut_drop.golden_nut` | or `D99A2B` | calotte `F0CB66` | reflet doux `FFE9A6` | stries et dessous `B06A1C` | `D99A2B` | `86552A` | `FFFFFF` |
| `palette.nut_drop.pinecone` | écailles `A8714A` | bords `D2A372` | coeur `5E4030` | queue `7A5A3E` | `A8714A` | `4F3427` | `FFFFFF` |
| `palette.nut_drop.feather` | blanc chaud `FFF6E6` | duvet `EDCF9E` | rachis `E2C08C` | bout du rachis `C9A06C` | pointe `F0D8B4` | `9E93AE` | `FFFFFF` |
| `palette.nut_drop.hazel_canopy` | coque des noisettes `A86A3C` | feuillage `7DB960` | soleil `A9D57C` | ombre `578F4B` | involucres `C9DC86` | `3B6338` | `FFFFFF` |
| `palette.nut_drop.hazel_trunk` | écorce `8F6A52` | lenticelles `B3967C` | mousse `8DAA5C` | `8F6A52` | sillons `6E4C3A` | `4E3B38` | `FFFFFF` |

Les verts du feuillage et l'occlusion sont ceux de `palette.grassland.tree`. Le « panier large » et l'« averse dorée »
réutilisent le panier et la noisette dorée : la scène les met à l'échelle ou les teinte, aucune nouvelle palette n'est
demandée. Le reflet de la noisette dorée est la seule lumière peinte, demandée par la fiche de l'objet ; il est doux et
placé en haut à gauche, du côté du soleil.

## Grille

Vert : tenu. Suivi : à vérifier dans Unity par la session principale. « s.o. » : sans objet.

| N° | Point | Résultat |
| --- | --- | --- |
| 1 | Silhouette à 64 px | vert : panier, noisette, gland, baies, noisette dorée, pomme de pin, plume et noisetier se lisent sur fond clair et sombre (`T40_props_silhouettes_light.png`, `_dark.png`) ; la noisette dorée a la silhouette de la noisette par choix, sa couleur et sa traînée la distinguent |
| 2 | Volumes distincts, jonctions lisibles | vert : bord roulé et paroi du panier, cupule et gland, trois baies, écailles en tuiles, rachis et vexille, tronc et couronne, grappes sous la couronne |
| 3 | Détails utiles, zones de repos | vert : chaque détail est dans la fiche ; l'intérieur du panier, la coque de la noisette et le dessous de la couronne restent simples |
| 4 | Symétrie | vert : panier, noisette et gland de révolution ; plume, baies et couronne volontairement asymétriques (fiche) |
| 5 à 9 | Pattes, anatomie, yeux | s.o. |
| 10 | Parties comptables constantes | vert : un seul niveau de détail, comptes fixés dans la fiche |
| 11 | Silhouette de chaque LOD | s.o. (objets de jeu à distance fixe, un seul niveau) |
| 12 | Aucune texture peinte, aucun bruit | vert : régions de sommets et palettes ; seul le feuillage garde la légère variation de lumière des arbres de la Prairie |
| 13 | Matière mate | vert pour `SH_EnvironmentOpaque` ; suivi : la noisette dorée reste mate (pas de reflet de métal) |
| 14 | Bandes 60/30/10 | suivi : à mesurer sur la capture Unity de la partie |
| 15 | L'animal se détache | suivi : panier miel sur l'écureuil roux, à mesurer pour chaque variante dans Unity |
| 16 | Ombres froides, lumière chaude | vert : occlusions brun violacé, vert bleuté, lilas pour la plume ; aucun gris |
| 17 | Matière reconnaissable | vert : osier tressé, coques, cupule, baies, écailles, plume, feuillage, écorce |
| 18 | Une seule lumière, aucune lumière peinte | vert : seule l'occlusion est peinte, sauf le reflet doux de la noisette dorée demandé par la fiche (voir les palettes) |
| 19 | Matin, jour, soir | suivi Unity |
| 20 | Belle sans post-traitement | vert dans Blender ; suivi Unity |
| 21 | Contact au sol | vert : pied du tronc évasé et enraciné de 3 cm, occlusion au pied, mousse au pied ; dessous du panier assombri |
| 22 à 29 | Animation, famille d'animaux | s.o. (aucun clip nouveau ; le balancement du panier est fait par la scène) |
| 30 | Rien n'entre dans rien | vert : mesures ci-dessus ; suivi : les oreilles bougent dans Idle, Move, Happy et React, l'écart est à contrôler sur les clips dans Unity |
| 31 | Taille juste | vert : panier de 0,32 m pour une tête de 0,19 m ; objets de 0,13 à 0,17 m, agrandis exprès pour la lecture en chute (moitié du panier) ; noisetier de 4,3 m, tronc de 0,33 m au pied, proportions des arbres de la Prairie |
| 32 | Rien ne flotte | vert : panier posé sur la tête (0,2 cm d'écart sur l'écureuil et le lapin) ; tronc enraciné ; grappes tenues par leur pédoncule |
| 33 à 37 | Interface et icônes | s.o. |
| 38 | Aucune étoile à quatre branches | vert : aucune étoile ; écailles rondes, involucres à cinq lobes ronds, plume sans pointe |
| 39, 40 | Effets | s.o. (traînée et éclats faits dans Unity) |
| 41 | Source éditable, export reproductible | vert : `.blend` et scripts listés dans `SourceArt/Handmade_Sources.json` |
| 42 | Aucun générateur par IA | vert |
| 43 | Vocabulaire | vert |
| 44 | Fait par la même main | vert : planche `T40_props_family.png` (écureuil roux et lapin brun avec le panier, objets, brosse, noeud et bardane du toilettage, buisson de la Prairie) et `T40_props_tree.png` (noisetier à côté de l'arbre fruitier et du buisson de la Prairie) |

## Images

- `T40_props_items.png` : les six objets qui tombent à leur taille réelle, de face et un peu au-dessus.
- `T40_props_basket.png` : le panier vu par la caméra haute de trois quarts, trois objets attrapés posés au fond et l'un
  sur l'autre (positions mesurées), la noisette dorée et la pomme de pin à côté.
- `T40_props_tree.png` : le noisetier à côté de l'arbre fruitier et du buisson de la Prairie, le lapin brun pour
  l'échelle.
- `T40_props_game.png` : maquette du cadre de jeu (portrait, caméra à 3,6 m, champ vertical de 58°) : prairie et
  collines du décor du toilettage, noisetier à gauche, sa couronne en haut de l'écran, l'écureuil avec le panier sur la
  tête, les objets en chute dans le plan de jeu.
- `T40_props_family.png` : l'écureuil et le lapin avec le panier (décalages mesurés), les objets au sol, la brosse, le
  noeud et la bardane du toilettage, un buisson de la Prairie.
- `T40_props_silhouettes_light.png`, `T40_props_silhouettes_dark.png` : silhouettes à 64 px.

## Suivis

- Revue dans Unity (cadre de jeu, trois paliers, matin, jour, soir) avec les palettes ajoutées par le pipeline : points
  13 à 15, 19 et 20.
- Panier sur la tête : avancer le panier de 8 cm (écureuil) ou 10 cm (lapin, hérisson) vers la caméra depuis
  `SOCK_HeadTop`, hauteurs du tableau ci-dessus ; contrôler l'écart sur les clips (les oreilles bougent) et pendant le
  balancement.
- Objets attrapés : un seul tient au fond ; montrer les suivants plus petits ou n'en garder que un ou deux.
- En très gros plan, les noisettes du noisetier (6 côtés) et les baies montrent quelques facettes au contour ; leurs
  normales sont arrondies, et à la taille du jeu elles ne se voient pas.
