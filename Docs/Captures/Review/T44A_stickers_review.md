# T44A : revue « fait main » des autocollants capturés

10 octobre 2026. Doc « 01 Albums et autocollants », section 1.11 (art par capture des vrais modèles, L-SC-011), pages
Prairie (section 2.1) et pages d'activité des 6 activités sorties (sections 5.1 à 5.6). Planche contact écrite par le
pipeline à côté de ce fichier : `T44A_stickers_contact.png` (images de jeu 256 x 256 dans l'ordre du tableau ci-dessous,
8 par rang, sur un gris doux pour voir le bord blanc). La planche n'existe qu'après le passage du pipeline dans Unity par la
session principale ; les cases « à voir » se remplissent alors.

## Comment les images sont faites

- Outil : `Assets/_Game/Editor/Albums/StickerCapturePipeline.cs`, données `StickerCaptureDefinition.cs` et
  `StickerCaptureDefinitions.cs`, traitement d'image `StickerImage.cs`. Menu « Pocket Wilds/Albums/Capture page » (un
  sous-menu par page) et `StickerCapturePipeline.CaptureAvailable()` pour execute_code (rapport : ce qui est fait et chaque
  WARNING).
- Sujets : les vrais prefabs du jeu. Animaux depuis leur prefab et la palette de leur variante, dans un état de
  `AC_SmallQuadruped` à un temps normalisé choisi (Idle, IdleLook, Move, Eat, Sleep, Happy, React), LOD0. Décors de la
  Prairie (`PF_GL_*`, kit `GL_Flower_A`, `GL_Clover_A`), accessoires des activités (`GR_Brush`, `GR_Bow`, `GR_Debris_*`,
  `ND_*`, `GD_WateringCan`, `PC_Butterfly_Open`), plantes du Jardin (`PF_GardenPlant_*`), avec leurs palettes comme le jeu
  les lie. Aucune image générée, aucune peinture, aucune retouche.
- Cadrages et focales du tableau : Portrait 50 mm (yeux vers le tiers haut), Plein pied 35 mm, Scène 35 mm, Paysage 28 mm
  (horizon au tiers haut), Détail 85 mm, pour un cadre de 24 mm de haut.
- Lumière : profils `LP_Grassland_Morning`, `Day`, `Evening`, `Night`, soleil toujours du haut à gauche de l'image à 35
  degrés, même contour pour tout le roster (celui des shaders).
- Formes : Découpé (Portrait, Plein pied, Détail : deux rendus sur noir et sur blanc dans un studio vide, alpha calculé),
  Carte (Scène : coins ronds de 24 px) et Médaillon (Paysage : rond), rendus dans la scène du monde sur la zone ou sur le
  décor du Jardin, avec le ciel, le sol, les plantes et, pour les paysages sans sujet, la vie ambiante.
- Sortie : rendu 1 024 x 1 024 MSAA 8x au palier haut, maître 512 x 512 en PNG dans `SourceArt/UI/Stickers/<page>/`, image
  de jeu 256 x 256 dans `Assets/_Game/Art/UI/Albums/Stickers/<page>/` avec le bord blanc de découpe de 10 px calculé
  depuis l'alpha, sans ombre cuite. Un atlas par page (`Art/UI/Albums/Atlas_Page_<page>`, ASTC 4x4), entrée Addressables
  du groupe `Albums` (`albums.page.<clé de page>`), et `DEF_StickerArt` qui liste chaque autocollant capturé.
- Aucune collision : chaque sujet a une forme mesurée (corps du catalogue pour les animaux, capsule mesurée sur les
  sommets des décors et accessoires, obstacles de la zone, parcelles mesurées du Jardin). Un chevauchement refuse
  l'autocollant ; dans une zone, l'outil prend la place libre la plus proche de l'ancre ; les plantes du sol ne poussent
  jamais dans un sujet posé (zone d'exclusion de la dispersion). Seuls les éléments d'une averse ou les fruits posés dans
  un panier qui toucheraient quelque chose sont retirés, jamais enfoncés.

## Ordre de la planche (42 autocollants capturables aujourd'hui)

| # | ID | Page | Cadrage | Sujet capturé | À voir |
|---|---|---|---|---|---|
| 1 | STK-GL-01 | grassland.1 | Plein pied, matin | Lapin trois quarts (IdleLook), pâquerettes et touffe à ses pattes | |
| 2 | STK-GL-02 | grassland.1 | Portrait, jour | Écureuil qui mange, noisette sur la souche GL_Stump | |
| 3 | STK-GL-03 | grassland.1 | Détail, jour | Hérisson en boule (Sleep), trèfles GL_Clover_A | |
| 4 | STK-GL-13 | grassland.2 | Paysage, matin | Le Départ | |
| 5 | STK-GL-14 | grassland.2 | Paysage, jour | La Prairie fleurie | |
| 6 | STK-GL-15 | grassland.2 | Paysage, jour | Le Coude du ruisseau | |
| 7 | STK-GL-16 | grassland.2 | Paysage, jour | Le Verger | |
| 8 | STK-GL-17 | grassland.2 | Paysage, soir | La Colline et le Vieil arbre, un Lapin et un Hérisson | |
| 9 | STK-GL-18 | grassland.2 | Paysage, soir | Le Moulin de la Butte | |
| 10 | STK-GL-19 | grassland.2 | Paysage, matin | Le Bord de la mare | |
| 11 | STK-GL-20 | grassland.2 | Paysage, soir | La Lisière | |
| 12 | STK-GL-21 | grassland.2 | Détail, jour | Trois papillons blancs posés sur pâquerettes et coquelicot | |
| 13 | STK-ACT-001-02 | activity.001 | Détail | Brindille qui s'envole du dos d'un Lapin | |
| 14 | STK-ACT-001-03 | activity.001 | Détail | Feuille sur le dos d'un Écureuil | |
| 15 | STK-ACT-001-04 | activity.001 | Portrait | Hérisson aux yeux fermés (Sleep) | |
| 16 | STK-ACT-001-08 | activity.001 | Scène | Lapin qui glousse sous la brosse | |
| 17 | STK-ACT-001-09 | activity.001 | Détail | Brosse de pro, manche doré | |
| 18 | STK-ACT-001-10 | activity.001 | Scène | Lapin, Écureuil, Hérisson alignés, un nœud chacun | |
| 19 | STK-ACT-002-01 | activity.002 | Détail | Oreille de Lapin derrière une touffe | |
| 20 | STK-ACT-002-02 | activity.002 | Détail | Herbe haute, queue d'Écureuil qui dépasse | |
| 21 | STK-ACT-002-06 | activity.002 | Portrait | Écureuil devant un buisson | |
| 22 | STK-ACT-002-07 | activity.002 | Scène | Cinq animaux sortis de leurs cachettes | |
| 23 | STK-ACT-002-12 | activity.002 | Scène | Tous sortent en même temps : buisson, herbe haute, souche | |
| 24 | STK-ACT-003-01 | activity.003 | Détail | Noisette qui tombe | |
| 25 | STK-ACT-003-02 | activity.003 | Détail | Gland qui tombe | |
| 26 | STK-ACT-003-03 | activity.003 | Détail | Baie rouge | |
| 27 | STK-ACT-003-04 | activity.003 | Détail | Panier plein de noisettes et de baies | |
| 28 | STK-ACT-003-05 | activity.003 | Scène | Écureuil qui évite une pomme de pin | |
| 29 | STK-ACT-003-06 | activity.003 | Scène | Cinq noisettes en file vers le panier | |
| 30 | STK-ACT-003-07 | activity.003 | Scène | Écureuil, panier plein sur la tête | |
| 31 | STK-ACT-003-08 | activity.003 | Détail | Noisette dorée | |
| 32 | STK-ACT-003-09 | activity.003 | Détail | Panier tressé neuf | |
| 33 | STK-ACT-003-12 | activity.003 | Paysage | Averse de noisettes sur le Verger, Écureuil ravi | |
| 34 | STK-ACT-005-03 | activity.005 | Scène, matin | Trois animaux en file au départ | |
| 35 | STK-ACT-005-04 | activity.005 | Paysage, matin | Sentier vers la Colline | |
| 36 | STK-ACT-005-11 | activity.005 | Paysage, soir | Petite équipe qui traverse le pré | |
| 37 | STK-ACT-006-02 | activity.006 | Détail | Pousse au premier stade (tournesol) | |
| 38 | STK-ACT-006-03 | activity.006 | Scène | Arrosoir penché sur une parcelle | |
| 39 | STK-ACT-006-04 | activity.006 | Détail | Fleur ouverte (marguerite, stade 3) | |
| 40 | STK-ACT-006-05 | activity.006 | Détail | Melon rond prêt à cueillir | |
| 41 | STK-ACT-006-06 | activity.006 | Paysage | Une parcelle pleine (fraises des bois) | |
| 42 | STK-ACT-006-12 | activity.006 | Paysage | Toutes les parcelles en fleurs, tournesol au fond, Lapin et Écureuil | |

## Grille de la charte (à remplir sur la planche)

| Critère | Attendu | Résultat |
|---|---|---|
| Source | Rendu de nos modèles seulement, maître et outil listés (`SourceArt/Handmade_Sources.json`) | fait par construction |
| Lisibilité à 256 | Silhouette claire, sujet qui remplit la forme, rien de coupé hors du bord blanc | à voir |
| Bord blanc | 10 px régulier, sans trou ni marche, coins de carte ronds, médaillon rond | à voir |
| Lumière | Du haut à gauche, même contour pour tous, soirées dorées sans noir bouché | à voir |
| Aucune collision | Rien n'entre dans rien (refus automatique, retraits listés dans le rapport) | par construction, à voir |
| Rien de pointu, pas d'étincelle | Aucune étoile à quatre branches ; la noisette dorée garde sa lueur douce | à voir |
| Cohérence de page | Même échelle d'animal d'un autocollant à l'autre, fonds de même famille | à voir |

## Écarts assumés par rapport au doc

- STK-GL-02 : la noisette est posée sur la souche, pas tenue (pas encore de point d'attache aux pattes).
- STK-ACT-001-04 : les yeux fermés viennent de l'état Sleep (pas d'expression « plaisir » à part).
- STK-ACT-003-01 : pas de traînée de mouvement (aucune dans le jeu).
- STK-ACT-006-03 : pas de gouttes (aucun modèle de goutte d'arrosage).
- STK-GL-21 : papillons blancs du décor de carte postale ; coquelicot du kit de la Prairie.

## Pas encore capturables (54), et pourquoi

Liste complète dans `StickerCaptureDefinitions.Unavailable` (repris dans le rapport du pipeline). En bref :

- Espèces absentes : Mulot, Cochon d'Inde, Faon, Hamster (STK-GL-04 à 12), Renard, Panda roux (STK-ACT-001-05, 06, 07,
  11, 12), Renard, Raton laveur, Blaireau, Mulot, Chouette (STK-ACT-002-04, 05, 08, 10, 11), Marcassin (STK-ACT-003-11),
  Mulot (STK-ACT-005-05).
- Décors ou accessoires absents : grande feuille (GL-22), coussin (001-01), gros champignon (002-03), loupe (002-09),
  sac à dos et sacs (005-01, 06), boussole (005-09), carte du monde (005-12), graine et trou de terre (006-01), récolte
  tenue (006-07), pots de teinture (006-08, 11), fruits en vrac (006-09), herbier (006-10).
- Effets ou biomes absents : papillon doré (GL-24), arc-en-ciel et pluie fine non figés par l'outil (GL-22, GL-23), Forêt
  (003-10), Lac (005-02), six biomes (005-10).
- Memory des empreintes (les 12 de STK-ACT-004) : les cartes sont des sprites d'interface, pas de modèle 3D de carte à
  capturer ; carte postale cornée (005-07) : même cas.
- Œuf Découverte (005-08) : l'œuf existe, l'outil ne fait pas encore apparaître d'œuf (prochaine passe).

## Suivis

- Passer le pipeline dans Unity, remplir la colonne « À voir » et la grille, régler cadrages et poses donnée par donnée
  dans `StickerCaptureDefinitions.cs` (azimut, hauteur, zoom, état et temps de pose).
- Si le rapport signale un bord de zone visible sur un paysage, rapprocher la caméra (distance) dans la définition.
- L'AssetValidator ne signale pas encore un autocollant périmé quand son prefab source change.
