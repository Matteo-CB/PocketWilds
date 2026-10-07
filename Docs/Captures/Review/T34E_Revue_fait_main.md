# T34E : revue « fait main » du roster existant

7 octobre 2026. La grille de 44 points de la charte « fait main » (dossier 05 du Drive, section 11) est appliquée au
Lapin, à l'Écureuil, au Hérisson, au kit de la Prairie et aux icônes. Chaque point a été mesuré quand c'était possible,
puis jugé sur les captures Unity (vrais shaders, palettes, lumière du jeu).

## Comment c'est mesuré

| Mesure | Outil | Résultat |
|---|---|---|
| Yeux, tête, symétrie, parties par LOD, pieds, arêtes | Blender sans interface, lecture seule des `.blend` (`LOD0` à `LOD2`, maillages de construction) | tableau « Animaux » ci-dessous |
| Silhouettes noires à 64 px, face, profil, trois quarts, fond clair et sombre | `WorldCapture.CaptureHandmadeReview` | `T34E_silhouettes_64px.png` |
| Planche de famille sous le matin, le jour et le soir | idem | `T34E_family_Morning.png`, `_Day`, `_Evening` |
| Cadre de jeu par palier (Fluide, Équilibrée, Magnifique) | idem | `T34E_game_Low.png`, `_Medium`, `_High` |
| Contraste animal et herbe : L*, ΔE, vision deutéranope | idem, chaque variante dans le cadre de jeu, herbe rendue sans l'animal | `T34E_contrast.csv`, `T34E_deutan_game.png` |
| Animation : déplacement du corps, oreilles, pieds au repos, glissement des pattes | `WorldCapture.MeasureAnimation`, clips réels à 60 Hz | `T34E_animation.csv`, `T34E_frames_rabbit.png`, `T34E_frames_hedgehog.png` |
| Kit : triangles, taille, contact au sol de chaque décor posé | Blender (kit) et Unity (les 257 décors des 8 zones) | voir « Kit de la Prairie » |
| Icônes à 24 et 48 dp, nombre de tons | `icons.py` (revue) | `T34E_icons_24dp_48dp.png` |
| Bandes 60/30/10 | `Tools/Capture/beauty_review.py bands` (nouveau) | voir « Palette » |
| Traces de générateur, bibliothèques | `HandmadeRuleTests`, et une lecture des 7 `.blend` | aucune trace |

## Animaux

Les fourchettes de la charte sont mesurées sur le volume de tête de la construction (`pw_species.py`, blockout du
Lapin), comme les mesures de référence de la charte.

| Mesure | Lapin | Écureuil | Hérisson | Fourchette de la charte |
|---|---|---|---|---|
| Triangles LOD0, LOD1, LOD2 | 2 110, 792, 382 | 2 398, 948, 418 | 2 406, 1 094, 443 | Manifest |
| Diamètre de l'œil / largeur de la tête | 46 / 300 mm = 15,3 % | 48 / 264 mm = 18,2 % | 40 / 156 mm = 25,6 % | 17 à 26 % |
| Écart intérieur / diamètre | 74,6 mm = 1,62 | 57,6 mm = 1,20 | 29,8 mm = 0,74 | 0,9 à 1,4 |
| Hauteur du centre de l'œil dans la tête | 50,8 % | 51,7 % | 65,1 % | 50 à 62 % |
| Tête / volume perçu (oreilles exclues) | 43,6 % | 43,8 % sans la queue, 27,3 % avec | 9,7 % (carapace comprise) | 38 à 46 % |
| Hauteur de tête / hauteur hors oreilles | 0,62 | 0,49 (queue comprise) | 0,48 | 0,45 à 0,55 |
| Écart au miroir, LOD0 / LOD1 / LOD2 (max) | 0,1 / 8,9 / 12,5 mm | 2,1 / 7,1 / 11,8 mm | 1,1 / 7,6 / 15,5 mm | aucune asymétrie visible |
| Yeux et nez par LOD | 2+1 / 2+1 / 2+0 | 2+1 / 2+1 / 2+0 | 2+1 / 2+1 / 2+0, 30 piquants à chaque LOD | compte constant |
| Écart sous les pattes au repos (avant, arrière) | 9 mm, 2 mm | 3 mm, 0 | 2 mm, 0 | au sol (tolérance 2 cm) |
| Faces lissées | toutes | toutes | toutes | matière mate, pas de facettes |

Le LOD1 s'affiche sous 7 % de hauteur d'écran (au-delà d'environ 18 m, ou 13 m en Fluide), le LOD2 sous 3,5 % (au-delà
d'environ 36 m) : à 13,8 m en Fluide, 9 mm d'écart au miroir font 2 px. Le nez absent du LOD2 n'apparaît jamais aux distances du jeu.

### Contraste avec l'herbe (cadre de jeu, jour, Équilibrée)

| Variante | ΔL* | ΔE | ΔE deutéranope | Variante | ΔL* | ΔE | ΔE deutéranope |
|---|---|---|---|---|---|---|---|
| Lapin brun | -7,8 | 47,4 | 6,4 | Écureuil crème | 19,3 | 36,5 | 24,7 |
| Lapin blanc | 20,2 | 46,4 | 33,7 | Écureuil noir | -45,5 | 63,4 | 52,0 |
| Lapin gris | 1,8 | 44,1 | 26,6 | Écureuil d'automne | -1,5 | 54,9 | 20,0 |
| Lapin noir | -40,9 | 62,1 | 47,8 | Hérisson brun | -22,1 | 47,2 | 23,1 |
| Lapin tacheté | 18,2 | 43,2 | 29,0 | Hérisson crème | 14,0 | 35,4 | 18,6 |
| Lapin sakura | 16,6 | 51,4 | 33,8 | Hérisson gris | -7,6 | 41,3 | 25,5 |
| Écureuil roux | -14,5 | 65,1 | 14,0 | Hérisson noir | -37,1 | 56,4 | 44,0 |
| Écureuil brun | -26,1 | 51,6 | 26,0 | Hérisson blanc | 20,9 | 44,2 | 33,4 |
| Écureuil gris | 0,3 | 40,7 | 25,6 | Hérisson champignon | -6,9 | 66,4 | 3,3 |

Six variantes sont à moins de 12 L* de l'herbe (règle 4.1). En vision normale, toutes s'en détachent nettement par la
couleur (ΔE 35 à 66) et se lisent bien dans le cadre de jeu (`T34E_game_low_contrast_variants.png`). Le liseré renforcé
prévu par la charte a été essayé (0,16 à 0,32, `T34E_low_contrast_row_rim16.png` et `_rim32.png`) : les animaux
pâlissent sans mieux se lire, il n'est pas retenu. Le vrai risque est la vision rouge-vert (environ un homme sur douze) :
le Lapin brun (ΔE 6,4), le Hérisson champignon (3,3) et l'Écureuil roux (14,0) prennent la teinte olive de l'herbe. Ils
restent lisibles par le ventre blanc, les yeux et l'ombre, mais nettement moins (`T34E_deutan_game.png`).

### Animation (après correction)

| Mesure | Lapin | Écureuil | Hérisson | Règle |
|---|---|---|---|---|
| Déplacement du corps dans un clip | 0 | 0 | 0 | 3 cm au plus |
| Oreilles au repos (Idle A, Idle B) | 7,3° et 7,4° | 7,3° et 7,4° | 7,3° et 7,4° | 8° au plus |
| Glissement d'une patte d'appui en marche | 6,1 cm | 7,6 cm | 6,3 cm | 1 cm au plus |
| Squash et stretch | aucun (tassement par la hauteur du corps) | idem | idem | ±10 % |

## Kit de la Prairie

| Mesure | Résultat |
|---|---|
| Décors posés, 8 zones | 257 contrôlés : chacun touche le sol (seules les ailes du moulin sont en l'air, par construction) |
| Échelle | arbres 2,4 à 3,4 m, buissons 0,45 à 0,79 m, rochers héros 0,9 à 1,2 m, barrières 0,56 à 0,75 m, botte de foin 0,68 m, moulin 4,7 m, pour un lapin de 0,45 m |
| Ombres | teinte des ombres bleu violet sur tous les matériaux (`_ShadowTint` 0,55 à 0,62 / 0,58 à 0,62 / 0,72 à 0,74) |
| Arêtes | rochers, buissons, arbres lissés ; pièces de bois (barrières, panneau, puits, ailes et tour du moulin) en facettes à 90° sans chanfrein |
| Textures | aucune texture peinte : couleurs de sommets et palettes ; seules textures : le masque de l'œuf (construit par `pw_egg.py`) et la planche de particules (`VfxPipeline.cs`) |

## Palette

- Bandes 60/30/10 du cadre de jeu (dominante C* 35 au plus, secondaire 35 à 50, accents au-delà de 50) :
  Fluide 14 / 76 / 10 %, Équilibrée 15 / 65 / 20 %, Magnifique 15 / 58 / 27 %. Le sol et l'herbe de la Prairie sont
  dans la bande secondaire (C* 38 à 48) : la règle n'est pas tenue, et Équilibrée et Magnifique dépassent 18 % d'accents
  (fleurs plus denses).
- Blanc pur (L* au-delà de 95) dans les palettes : ventres des variantes blanches et crème (96 à 100, ventre du Hérisson
  blanc à 100), écume de l'eau (98 et 100), points blancs du champignon (97,5), fleur blanche (96,2), abeille (95,9). Les
  colonnes « accent » blanches des décors ne servent qu'à de légères variations de teinte.
- Noir pur hors des yeux : le nez du Hérisson noir (L* 7).

## La grille

Vert : tenu. Rouge : écart. Corrigé : écart trouvé et corrigé dans T34E. Décision : à trancher par le propriétaire.
Suivi : correction prévue dans une tâche à venir. « s.o. » : sans objet.

| N° | Point | Lapin | Écureuil | Hérisson | Kit Prairie | Icônes |
|---|---|---|---|---|---|---|
| 1 | Silhouette à 64 px | vert | vert | vert | vert | s.o. |
| 2 | Volumes distincts, jonctions lisibles | vert | vert | vert | vert | s.o. |
| 3 | Détails utiles, zones de repos | vert | vert | vert | vert | s.o. |
| 4 | Symétrie exacte | vert | vert | vert | s.o. | s.o. |
| 5 | Pattes simples | vert | vert | vert | s.o. | s.o. |
| 6 | Ordre anatomique | vert | vert | vert | s.o. | s.o. |
| 7 | Yeux en perles, sans iris | vert | vert | vert | s.o. | s.o. |
| 8 | Un seul reflet | vert | vert | vert | s.o. | s.o. |
| 9 | Taille, écart, hauteur des yeux | décision (A) | vert | décision (A) | s.o. | s.o. |
| 10 | Parties comptables constantes par LOD | suivi (G) | suivi (G) | suivi (G) | suivi (G) | s.o. |
| 11 | Silhouette de chaque LOD | vert | vert | vert | vert | s.o. |
| 12 | Aucune texture peinte | vert | vert | vert | vert | vert |
| 13 | Matière mate | vert | vert | vert | vert | vert |
| 14 | Bandes 60/30/10 | décision (D) | décision (D) | décision (D) | décision (D) | s.o. |
| 15 | Se détache du sol de 12 L* | décision (C) | décision (C) | décision (C) | s.o. | s.o. |
| 16 | Ombres froides, lumière chaude | vert | vert | vert | vert | vert |
| 17 | Matière reconnaissable | vert | vert | vert | vert | s.o. |
| 18 | Une seule lumière | vert | vert | vert | vert | s.o. |
| 19 | Belle sous matin, jour, soir | vert | vert | vert | vert | s.o. |
| 20 | Belle sans post-traitement | vert | vert | vert | vert | s.o. |
| 21 | Contact au sol | vert | vert | vert | corrigé (2) | s.o. |
| 22 | Poses clés lisibles | vert | vert | vert | s.o. | s.o. |
| 23 | Anticipation | vert | vert | vert | s.o. | s.o. |
| 24 | Impacts tassés, amortis | corrigé (1), suivi (E) | corrigé (1), suivi (E) | corrigé (1), suivi (E) | s.o. | s.o. |
| 25 | Pied qui glisse de 1 cm au plus | suivi (E) | suivi (E) | suivi (E) | s.o. | s.o. |
| 26 | 20 animaux au repos restent calmes | corrigé (3) | corrigé (3) | corrigé (3) | s.o. | s.o. |
| 27 | Mêmes yeux, joues, densité | décision (A) | vert | décision (A) | s.o. | s.o. |
| 28 | Proportions de famille | vert | vert | décision (B) | s.o. | s.o. |
| 29 | Deux espèces diffèrent par deux traits | vert | vert | vert | s.o. | s.o. |
| 30 | Rien n'entre dans rien | vert | vert | vert | corrigé (4) | s.o. |
| 31 | Taille juste | vert | vert | vert | vert | s.o. |
| 32 | Rien ne flotte | vert | vert | vert | corrigé (2) | s.o. |
| 33 | Géométrie construite, trait constant | s.o. | s.o. | s.o. | s.o. | vert |
| 34 | Deux tons et un éclat | s.o. | s.o. | s.o. | s.o. | vert, deux écarts voulus (H) |
| 35 | Aucun texte, symbole inconnu, emoji | s.o. | s.o. | s.o. | s.o. | vert |
| 36 | Lisible à 24 dp | s.o. | s.o. | s.o. | s.o. | vert |
| 37 | Rareté par étoiles, mot et motif | s.o. | s.o. | s.o. | s.o. | vert |
| 38 | Aucune étoile à quatre branches | vert | vert | vert | vert | vert |
| 39 | Quantités d'effets | vert | vert | vert | vert | s.o. |
| 40 | Beau sans effets | vert | vert | vert | vert | s.o. |
| 41 | Source éditable, export reproductible | corrigé (5) | corrigé (5) | corrigé (5) | vert | vert |
| 42 | Aucun générateur par IA | vert | vert | vert | vert | vert |
| 43 | Vocabulaire | vert | vert | vert | vert | vert |
| 44 | Fait par la même main | vert | vert | vert | vert | vert |

Hors grille, section 3.2 (angles adoucis) : les pièces de bois du kit n'ont pas de chanfrein (suivi F).

## Corrigé dans T34E

1. **Rebond des animaux.** L'os Root du squelette pointe vers le haut : l'axe Z local que la bibliothèque de clips
   utilisait pour le rebond est l'axe avant de l'animal. Depuis T10, chaque saut poussait le corps de 5 à 8 cm vers
   l'avant puis l'arrière au lieu de le soulever. Le rebond suit maintenant l'axe Y de l'os : le corps monte de 5,5 cm
   au saut et de 8,5 cm au bond de joie, sans avancer. Le déplacement dans les clips passe de 8,5 cm à 0.
2. **Décors qui flottaient.** Le Buisson C commençait 12,6 cm au-dessus de son pivot : il flottait de 1,9 à 10,7 cm à
   sept endroits. La tour du moulin flottait de 3,7 cm sur sa butte. Le script pose maintenant le bas de chaque buisson
   3 cm sous le sol (Buisson C reconstruit depuis le script, mêmes 236 faces). Les décors des zones de la bande se posent
   sur le sol le plus bas sous leur empreinte, et plus seulement sous leur centre. Contrôle : 257 décors, aucun ne flotte.
3. **Oreilles au repos.** Les mouvements d'oreille des Idle passent de 9,3° et 11,9° à 7,3° et 7,4°.
4. **Herbe dans un décor voisin.** La reconstruction a montré que l'herbe d'une zone ne tenait compte que de ses propres
   décors : trois touffes sont tombées dans un décor de la zone voisine, à la couture. Une dernière passe retire
   maintenant toute plante qui entre dans un décor, voisins compris, quel que soit l'ordre de construction.
5. **Bibliothèque de clips versionnée.** Elle ne vivait que dans le `.blend` du squelette ; elle est maintenant aussi dans
   `SourceArt/Blender/Tools/pw_anim_library.py` et listée comme source dans `SourceArt/Handmade_Sources.json`.

## Décisions du propriétaire

- **A. Yeux du Lapin et du Hérisson.** Les fourchettes de la charte ont été tirées de l'Écureuil et du Hérisson, sans le
  Lapin, pourtant étalon de la gamme. Le Lapin approuvé a des yeux un peu petits et écartés pour ces fourchettes (15,3 %
  de la tête, écart 1,6 fois le diamètre). Le Hérisson a des yeux rapprochés et hauts (écart 0,74, hauteur 65 %), sous
  sa carapace. Proposition : garder les deux visages approuvés et élargir les fourchettes à la gamme mesurée (diamètre 15
  à 26 %, écart 0,7 à 1,7, hauteur 50 à 66 %). L'autre choix est de retoucher les deux visages.
- **B. Tête du Hérisson.** Sa tête fait 9,7 % du volume, sous une carapace et des piquants qui en font 62 %, contre 38 à
  46 % de tête pour la famille. Proposition : écrire cette signature d'espèce dans la charte (la carapace compte à part).
- **C. Contraste et daltonisme.** La règle des 12 L* écarte six variantes qui se lisent bien en vision normale. Le
  risque réel est la vision rouge-vert pour le Lapin brun, le Hérisson champignon et l'Écureuil roux. Proposition :
  remplacer la règle par « ΔE 30 au moins en vision normale et 15 au moins en vision deutéranope », et assombrir de 8 à 10 L*
  le pelage de ces trois variantes. Le Lapin brun est l'animal de départ : c'est un changement visible.
- **D. Saturation de la Prairie.** Le sol et l'herbe sont plus saturés que la dominante de la charte (C* 38 à 48 pour 35
  au plus), et Équilibrée (20 %) et Magnifique (27 %) dépassent 18 % d'accents. Proposition : garder la Prairie approuvée et fixer la bande dominante à
  C* 50 au plus pour ce biome (et les accents à 25 % en Magnifique), ou adoucir le vert de 15 à 20 % et éclaircir les
  fleurs en Magnifique (ce qui ferait aussi mieux ressortir les animaux).
- **Palette, blanc et noir purs.** Les ventres des variantes blanches et crème montent à L* 96 à 100. Ce serait corrigé
  avec la décision C, en une seule passe sur les palettes (L* 95 au plus, nez du Hérisson noir à 10 au moins).

## Suivis

- **E. Contact des pattes en marche.** Le corps avance à vitesse constante pendant que les pattes d'appui restent
  immobiles dans le clip : elles glissent de 6 à 8 cm par appui. Il faut moduler la vitesse au rythme du saut (immobile à
  l'appui, plus rapide en l'air) et ajouter un vrai tassement à l'atterrissage. À faire dans une passe d'animation.
- **F. Arrondis des pièces de bois.** Chanfreins de 8 mm (petits décors) et 20 mm (moulin) sur les arêtes de silhouette :
  2 à 4 triangles par arête.
- **G. Comptes constants par LOD.** Le nez disparaît au LOD2 des trois espèces, comme les pommes du LOD2 du pommier.
  Aucun effet visible aux distances du jeu. À corriger à la prochaine reconstruction d'une espèce (`pw_species.py`).
- **H. Icônes.** La loupe (verre et bois) et la couronne de fleurs (trois fleurs) dépassent deux tons par sens : écart
  voulu, à confirmer.
