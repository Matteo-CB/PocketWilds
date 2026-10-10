# T42 : revue « fait main » du décor de carte postale des Prés fleuris

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11) appliquée au décor de la carte
postale des Expéditions (doc 01 Contenu, section 7.1 : « l'équipe pique-nique au pied d'une colline couverte de
marguerites, papillons blancs autour ») : la nappe vichy, le petit panier et son torchon, la colline aux marguerites, les
papillons blancs et le pré. Images rendues dans Blender (EEVEE, lumière unique venant du haut et de la gauche de l'image,
couleurs calculées depuis les régions et les palettes comme le fait `SH_EnvironmentOpaque`), à côté de ce fichier
(préfixe `T42_props_`). La revue finale dans Unity (la vraie carte postale, l'image de destination, les trois paliers)
reste à faire par la session principale (voir « Suivis »).

## Ce qui existait déjà (vérifié d'abord)

- `GR_SetBackdrop` (T38, groupe `Activity_Shared`) : le sol de pré, trois rangs de collines rondes, quelques arbres ronds
  et dix petites marguerites. Repris tel quel, deux fois plus grand, derrière la colline aux marguerites. Son sol a un
  trou au milieu (la place du monticule du toilettage) : le décor pose donc son propre pré par dessus.
- Le kit de la Prairie : la marguerite `GL_Flower_A`, les touffes d'herbe et le trèfle (entrées explicites de
  `Biome_Grassland_Kit`) : repris au premier plan.
- `GL_Life_Butterfly` n'a que des rangs jaune, bleu et doré, et la scène du refuge le contient : un bundle de décor ne
  peut pas le copier (`CommonContentTests`). D'où deux papillons blancs à nous.
- Manquaient : la nappe, le panier avec son torchon, une colline couverte de marguerites (les collines du fond sont nues
  et lointaines), les papillons blancs, un pré sans trou.

## Fiche d'intention

- **Caractère** : un après-midi doux et lent. Une nappe vichy posée sur l'herbe, dodue et un peu froissée sur ses bords,
  un petit panier d'osier avec son torchon rose replié sur le bord, une colline ronde semée de marguerites, des piérides
  blanches. Même famille que les accessoires de la Prairie, le décor du toilettage et le panier de la Pluie de noisettes
  (même tressage, même osier miel).
- **Échelle** : mesurée sur les corps des animaux (`DEF_AnimalVisualCatalog`, LOD0, échelle 1). Lapin 0,54 x 0,33 m,
  0,66 m de haut avec les oreilles ; écureuil 0,64 x 0,30 x 0,55 m (sa queue part loin derrière) ; hérisson 0,53 x 0,33 x
  0,33 m. La nappe (1,9 x 1,1 m) tient les trois côte à côte, celui du milieu un peu en retrait, et le panier au bout
  droit, avec de la place même pour la queue de l'écureuil et un gène de taille de +15 %. Le panier fait 0,32 m (celui de
  T40). La colline fait 4,9 m de large et 1,05 m de haut. Un papillon fait 0,1 m d'envergure, montré 2,2 à 2,4 fois
  comme ceux du refuge.
- **Parties comptables** : nappe 19 x 11 carreaux (une bande sur chaque bord), 1 ourlet roulé ; panier 5 rangs tressés de
  8 bosses, 1 bord roulé, 1 anse, 1 torchon bombé, 1 rabat sur le bord avant avec son liseré ; colline 84 marguerites (5
  lobes ronds et un coeur bombé chacune) ; papillon 1 corps, 4 ailes rondes ; pré 1 disque.
- **Détails porteurs de sens** : le vichy net (chaque carreau a ses propres sommets, la couleur ne déborde jamais) et la
  normale douce du tissu sur tout le dessus (aucune couture dans la lumière) ; l'ourlet roulé ; le torchon qui dépasse et
  retombe sur le bord avant, son liseré clair ; l'anse en osier torsadé ; les coeurs jaunes des marguerites ; le bout gris
  lilas des ailes avant (on reconnaît une piéride).
- **Imperfections voulues** : les bords de la nappe ondulent, ses carreaux s'incurvent doucement vers les coins arrondis
  et un coin avant gauche est un peu gonflé ; la crête de la colline penche à droite ; les marguerites varient en taille
  et en inclinaison (graine fixe) ; le rabat du torchon pend décentré.
- **Rien de pointu** : coins de nappe arrondis, lobes ronds des marguerites (jamais une étoile à quatre branches), ailes
  rondes, pas d'antennes.
- **Palette visée** : celle de la Prairie (vert du pré de `palette.grooming.set_backdrop`), osier du panier T40, nappe
  bleu ciel exprès : l'écureuil roux, le lapin brun et le hérisson s'y détachent tous.

## Sources

| Fichier | Rôle |
| --- | --- |
| `SourceArt/Blender/Tools/pw_postcards.py` | construction de chaque objet (chaque forme est une valeur écrite), palettes proposées, budgets contrôlés, sauvegarde, export, images de revue |
| `SourceArt/Blender/Tools/pw_grassland.py` | fonctions partagées de la Prairie (régions `PW_Regions`, occlusion `PW_Occlusion`, `object_from_bmesh`) |
| `SourceArt/Blender/Tools/pw_nutdrop.py` | formes de T40 réutilisées (tubes, lentilles) et studio de revue |
| `SourceArt/Blender/Tools/pw_export.py` | réglages d'export FBX du projet (`FBX_PRESET`) |
| `SourceArt/Blender/Activities/BL_Activity_Postcards.blend` | la scène `Postcards` (collection `PC_EXPORT`), écrite par `bpy.data.libraries.write` depuis la session ouverte, sans toucher au fichier de la Prairie |
| `Assets/_Game/Art/Activities/Expeditions/PC_FlowerMeadows.fbx` | `PC_Meadow`, `PC_Blanket`, `PC_Basket`, `PC_DaisyHill`, `PC_Butterfly_Open`, `PC_Butterfly_Up` |

L'en-tête du FBX nomme `SourceArt\Blender\Activities\BL_Activity_Postcards.blend` (le fichier qui contient ces objets,
écrit juste avant l'export), listé dans `SourceArt/Handmade_Sources.json` avec ses quatre scripts. L'image de destination
`EXP_flower_meadows.png` est rendue dans Unity par `PostcardSetPipeline` et listée sous « generated ». Aucun générateur,
aucune bibliothèque de modèles.

## Mesures

Tailles dans Blender (x, y, z). Triangles comptés après construction ; chaque budget est contrôlé par
`build_postcards()`, qui refuse de dépasser.

| Objet | Triangles (budget) | Taille | Pivot |
| --- | --- | --- | --- |
| `PC_Blanket` | 2 512 (2 600) | 1,92 x 1,12 x 0,016 m (dessus du tissu à 8 mm, bords jusqu'à 16 mm) | centre, au sol |
| `PC_Basket` | 866 (1 100) | 0,32 x 0,24 x 0,31 m (bord à 15 cm, anse à 30 cm) | centre du dessous, pied sur z = 0 |
| `PC_DaisyHill` | 2 400 (2 600) | 4,88 x 2,65 x 1,05 m, anneau extérieur 3,5 cm sous le pré | centre de l'emprise, au sol |
| `PC_Butterfly_Open` | 130 (130) | 0,106 x 0,048 x 0,016 m | centre du corps |
| `PC_Butterfly_Up` | 130 (130) | 0,051 x 0,048 x 0,053 m (ailes levées de 62°) | centre du corps |
| `PC_Meadow` | 520 (900) | disque de 13 m, plat sur z = 0 | centre |

## Aucune collision (mesuré)

- **Marguerites de la colline** : 84 posées, aucune dans une autre (centres écartés de la somme des rayons + 12 mm) ;
  chaque pointe de lobe est mesurée au-dessus de la colline et la fleur entière remonte jusqu'à 3 mm au-dessus (36 ont
  été remontées).
- **Torchon** : le rabat reste 6 mm devant la plus grosse bosse du tressage ; son départ et les pieds de l'anse sont
  cachés sous le torchon (parties d'un même objet).
- **Nappe et pré** : le dessous de la nappe est à 0,5 mm du pré, jamais dedans ; le panier se pose sur le point le plus
  haut du tissu sous son pied (+1 mm).
- **Équipe** (Unity, `PostcardTeamLayout`, même code dans le pipeline, le rendu et les tests) : places mesurées pour
  toutes les équipes des trois espèces (1 à 3 animaux, répétitions comprises) à l'échelle 1 et 1,15 : plus petit écart
  2,9 cm (deux hérissons à 1,15), le panier toujours libre. Un Géant est poussé le long de la ligne jusqu'à 2 cm de
  marge (test EditMode), et la caméra recule si besoin.
- **Fond** : le décor du toilettage est centré sous la colline (son trou et l'anneau de ses collines proches, à 4,5 m,
  sont hors de la colline), son sol 2 cm sous le pré ; ses petites marguerites sont contrôlées contre la colline, la
  nappe, le panier, les places et l'herbe (le pipeline essaie plusieurs orientations et le signale sinon).
- **Papillons** : contrôlés par le pipeline contre la colline sous eux, le panier, la tête la plus haute (lapin Géant,
  0,91 m) et entre eux.

## Palettes proposées

Même rangement que `DEF_Palettes_Grassland` : base, R, G, B, A, occlusion, accent (valeurs dans `PC_PALETTES`, reprises
par `PostcardSetPipeline` dans `DEF_Palettes_Postcards`).

| Palette | Base | R | G | B | A | Occlusion | Accent |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `palette.postcard.blanket` | blanc chaud `FFFBF2` | bandes `B7D3EE` | croisements `7EA8DB` | ourlet `6E99CF` | `FFFBF2` | `6D7096` | `FFFFFF` |
| `palette.postcard.basket` | osier `D3A05E` | bosses `EBC98C` | bord et anse `E4BB7C` | torchon `F2A5AE` | liseré `FFF6EC` | `6F5048` | `FFFFFF` |
| `palette.postcard.daisy_hill` | pré `8DC166` | crête au soleil `BCD97C` | pétales `FAF6EC` | coeurs `F2C24F` | herbe du bas `76AA57` | `4E6D5D` | `FFFFFF` |
| `palette.postcard.butterfly_white` | corps `5A4E5E` | ailes `FFFCF6` | bouts d'ailes `CFC7DE` | `5A4E5E` | `5A4E5E` | `7A7090` | `FFFFFF` |
| `palette.postcard.meadow` | pré `8DC166` | taches claires `A6D07A` | taches sombres `7DB55C` | `8DC166` | `8DC166` | `4E6D5D` | `FFFFFF` |

## Grille

Vert : tenu. Suivi : à vérifier dans Unity par la session principale. « s.o. » : sans objet.

| N° | Point | Résultat |
| --- | --- | --- |
| 1 | Silhouette à 64 px | suivi : planche de silhouettes non faite ; nappe, panier à anse et colline se lisent sur la carte (`T42_props_postcard.png`) |
| 2 | Volumes distincts, jonctions lisibles | vert : ourlet et tissu, bord roulé et paroi, torchon et rabat, anse, coeurs des marguerites, corps et ailes |
| 3 | Détails utiles, zones de repos | vert : le milieu de la nappe (où l'équipe s'assoit), le dessus de la colline et le pré restent calmes |
| 4 | Symétrie | vert : nappe et panier symétriques, colline, marguerites et rabat volontairement asymétriques (fiche) |
| 5 à 9 | Pattes, anatomie, yeux | s.o. |
| 10 | Parties comptables constantes | vert : un seul niveau de détail, comptes fixés dans la fiche |
| 11 | Silhouette de chaque LOD | s.o. (rendu une fois, à distance fixe) |
| 12 | Aucune texture peinte, aucun bruit | vert : régions de sommets et palettes ; le vichy est fait de carreaux, pas d'une texture |
| 13 | Matière mate | vert pour `SH_EnvironmentOpaque` |
| 14 | Bandes 60/30/10 | suivi : à mesurer sur la carte rendue dans Unity |
| 15 | L'animal se détache | vert dans Blender (roux, brun et hérisson sur le bleu ciel) ; suivi pour chaque variante dans Unity |
| 16 | Ombres froides, lumière chaude | vert : occlusions lilas bleuté, brun violacé, vert bleuté ; aucun gris |
| 17 | Matière reconnaissable | vert : vichy, osier tressé, torchon, herbe, marguerites, ailes de piéride |
| 18 | Une seule lumière, aucune lumière peinte | vert |
| 19 | Matin, jour, soir | s.o. (la carte a sa propre lumière d'après-midi) |
| 20 | Belle sans post-traitement | vert dans Blender ; suivi Unity |
| 21 | Contact au sol | vert : colline enracinée de 3,5 cm avec une ombre de pied, ourlet jusqu'au pré, panier posé sur le tissu, pied du panier assombri |
| 22 à 29 | Animation, famille d'animaux | s.o. (les animaux prennent leur Idle et un Happy) |
| 30 | Rien n'entre dans rien | vert : mesures ci-dessus ; suivi : écarts dans la vraie carte (test PlayMode `PostcardPlayTests`) |
| 31 | Taille juste | vert : nappe pour trois animaux et un panier, panier de T40, papillons comme ceux du refuge |
| 32 | Rien ne flotte | vert : chaque animal posé sur le point le plus haut du tissu sous ses pattes (+2 mm) ; papillons en vol par nature |
| 33 à 37 | Interface et icônes | s.o. |
| 38 | Aucune étoile à quatre branches | vert : aucune étoile ; marguerites à cinq lobes ronds |
| 39, 40 | Effets | s.o. |
| 41 | Source éditable, export reproductible | vert : `.blend` et scripts listés dans `SourceArt/Handmade_Sources.json` |
| 42 | Aucun générateur par IA | vert |
| 43 | Vocabulaire | vert |
| 44 | Fait par la même main | vert : la maquette de carte (`T42_props_postcard.png`) pose l'écureuil roux, le lapin brun et le hérisson avec le panier, devant la colline et les collines du toilettage |

## Images

- `T42_props_postcard.png` : maquette de la carte (512 x 768, caméra du décor Unity) : la nappe, l'équipe aux places
  mesurées, le panier, la colline aux marguerites, le fond du toilettage, les papillons (le premier plan d'herbe du kit
  est ajouté dans Unity).
- `T42_props_blanket_basket.png` : la nappe vue de trois quarts, le panier posé dessus, les deux papillons.
- `T42_props_basket.png` : le panier de près (tressage, torchon et rabat, anse).
- `T42_props_hill.png` : la colline aux marguerites devant les collines du toilettage, sur le pré.

## Reprise T42b (après la première image de destination dans Unity)

- **Marguerites du premier plan** : la marguerite du kit (`GL_Flower_A`) est faite pour la caméra haute du refuge ; vue
  par la caméra du décor, sa tête se montrait par la tranche sur une longue tige jaune. Remplacée par deux modèles à nous
  dans `PC_FlowerMeadows.fbx` : `PC_Daisy` (174 triangles, une fleur à 15 cm) et `PC_Daisy_Clump` (474, trois fleurs de
  11 à 15,5 cm), tige verte courbée vers la caméra, tête inclinée d'environ 50° vers elle, douze pétales ronds (une
  pointe ronde entre deux épaules, une encoche à leur pied : jamais une étoile) autour d'un coeur jaune bombé, feuilles
  au pied. Palette `palette.postcard.daisy` (tige `6DA24F`, pétales `FFFDF7`, coeur `F2C24F`, feuilles `7FB75C`,
  crème près du coeur `F4E6C6`). Le pipeline les tourne vers la caméra (180°, plus ou moins 18°).
- **Papillons** : les ailes ouvertes, presque horizontales à hauteur des yeux, se voyaient par la tranche (des points
  gris). Les ailes ont maintenant 14 points de contour, un blanc chaud (corps taupe `8A7466`, ombre crème `F1DDBE` à la
  racine des ailes, occlusion `A8917F`) ; trois papillons volent près de l'équipe, vers la hauteur des têtes, le dessus
  des ailes tourné vers la caméra, environ 0,19 m d'envergure (une vingtaine de pixels dans l'image de destination de
  512 px), trois autres plus loin au-dessus de la colline. Le rendu cache un papillon proche si un animal de l'équipe
  posée l'atteindrait (un Géant), il ne l'entre jamais.
- Les images Blender ci-dessus datent d'avant cette reprise ; la vérification se fait sur l'image Unity.

## Reprise T42c (cadrage de la carte)

- La caméra reculait à l'exécution : la largeur de l'équipe comptait la portée du corps (la queue de l'écureuil, qui
  part vers le fond) au lieu de sa largeur à l'écran. Corrigé : seule la largeur des corps compte.
- Caméra un peu plus proche et plus basse (0,05 ; 1,15 ; -4,3), même visée : le bord avant de la nappe est à 79 % de la
  hauteur (609 px sur 768), l'équipe plus grande, la colline, les marguerites, les papillons et la bande de ciel restent
  dans l'image ; les bouts de la nappe sortent un peu sur les côtés.
- Premier plan : chaque pièce est projetée par la caméra du décor et reste hors de l'image de chaque place (toutes les
  tailles d'équipe, le corps le plus large et le plus haut du catalogue à 1,15) et du panier ; une pièce qui ne trouve
  pas de place glisse le long du rang ou reste dehors. Le pipeline écrit « foreground clear of the team ».

## Suivis

- Revue dans Unity : la carte rendue (`PostcardPlayTests`), l'image de destination `EXP_flower_meadows.png`, points 1,
  14, 15 et 20.
- Les marguerites de la colline montrent leurs facettes en très gros plan ; à la taille de la carte elles se lisent
  comme des fleurs.
