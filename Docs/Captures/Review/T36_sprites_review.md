# T36 : revue « fait main » des sprites du rendez-vous du jour

8 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 20 sprites ajoutés par T36 : cadeaux de bienvenue, Cadeau du jour, calendrier des
28 jours, souhaits, Carnet de la semaine, cadeau de retour. Planche : `T36_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t36.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A, T35A et T35B. Aucun générateur,
  aucune image téléchargée, aucune bibliothèque. Lancer `python SourceArt/UI/Tools/pw_ui_sprites_t36.py` réécrit
  seulement ces 20 sprites et leurs masters.
- Revue : chaque sprite regardé à sa taille, en 2x et 3x, puis à 64 px (environ 24 dp sur un téléphone de 1080 px de
  large) et 32 px, sur fond clair et fond sombre, à côté de douze icônes existantes (Codex, sac, Améliorer, pancarte,
  cœur, Lumière, nid, coche, cadenas, étoile patte, gemme, loupe). Les sprites du kit ont été composés comme l'interface
  le fera : teinte par multiplication, remplissage radial de l'anneau autour du centre de l'image, couches superposées.
- Doc de référence : doc 01 « Contenu des rendez-vous, des activités et de l'accueil » (sections 2 à 6) et doc 05
  « Sensations et surprises » (fiche 18 : « une boîte ronde en bois clair marquée d'une patte », jamais un paquet avec
  un nœud ; fiche 19 : l'encre du tampon s'étale en anneau).

## Assets

| Asset | Taille | Dossier | Usage |
| --- | --- | --- | --- |
| `ICO_Today` | 128 x 128 | `Art/UI/Icons` | bouton Aujourd'hui du rail de droite : page du jour à en-tête corail et patte, soleil qui se lève derrière |
| `ICO_Gift` | 128 x 128 | idem | cadeau (accueil, Cadeau du jour, retour) : boîte ronde en bois clair, patte sur le couvercle, bande rose à trois perles, étiquette cœur, aucun nœud |
| `ICO_Wish` | 128 x 128 | idem | souhait : bulle de pensée douce et petit cœur |
| `ICO_Wish_Done` | 128 x 128 | idem | souhait accompli : coche verte et petit cœur, aussi dans la bulle du monde |
| `ICO_Calendar_Days` | 128 x 128 | idem | calendrier des 28 jours : page à en-tête vert, cinq cases tamponnées d'une patte, trois à venir, une étoile |
| `ICO_WeeklyJournal` | 128 x 128 | idem | Carnet de la semaine : carnet bleu ciel, élastique, signet rose, patte |
| `ICO_Ticket` | 128 x 128 | idem | billet d'activité : encoches rondes, pointillés ronds, étoile à cinq branches |
| `ICO_Seed` | 128 x 128 | idem | graine qui ouvre ses deux premières feuilles |
| `ICO_Treat` | 128 x 128 | idem | friandise en général : biscuit cœur au glaçage rose |
| `ICO_Treat_CloverCrunch` | 128 x 128 | idem | Trèfle croquant (Prairie) : biscuit rond piqué, brin de trèfle doux (fleur ronde, deux feuilles rondes) |
| `ICO_Swap` | 128 x 128 | idem | changer un souhait : deux flèches rondes bleu ciel |
| `ICO_Decoration` | 128 x 128 | idem | décoration : pot de terre cuite au petit cœur, fleur rose à cinq pétales |
| `UI_GiftBox_Base` | 256 x 256 | `Art/UI/Kit` | grande boîte de l'écran Aujourd'hui, couche 1 : ombre, corps, bords bruns de la bande, étiquette, intérieur vu quand le couvercle se lève |
| `UI_GiftBox_Band` | 256 x 256 | idem | couche 2, même cadre : bande blanche et grise, teintée crème, bleu ciel ou or selon la taille du lot |
| `UI_GiftBox_Lid` | 256 x 256 | idem | couche 3, même cadre : couvercle à patte, couvre exactement l'ouverture |
| `UI_Bubble_Wish` | 144 x 224 | idem | bulle au-dessus de l'animal, blanche pour la teinte, contour brun du HUD, deux points vers l'animal |
| `UI_Bubble_Ring` | 144 x 224 | idem | anneau de progression, même cadre que la bulle, anneau blanc complet centré sur l'image |
| `UI_Calendar_Box` | 112 x 120 | idem | une case de la bande des 28 jours, blanche pour la teinte, en-tête pour le numéro du jour |
| `UI_PawStamp` | 96 x 96 | idem | tampon patte posé sur une case récupérée, encre rose légèrement translucide |
| `UI_Journal_Chest` | 192 x 192 | idem | coffre du carnet complet : bois clair, dôme rond, sangles à clous dorés, fermoir doré au cœur |

Masters : `SourceArt/UI/Masters/<nom>@4x.png` pour les 20. Aucun texte dans les sprites : numéros de jour, quantités et
noms sont du texte du jeu (Pocket Round).

## Grille

| Point de la charte | Résultat |
| --- | --- |
| 12. Aucune texture peinte, aucun bruit | vert : aplats et dégradés construits. Les « manques d'encre » du tampon sont cinq petits cercles placés à la main, pas un bruit |
| 13. Matière mate | vert : un seul éclat doux par matière, en haut à gauche |
| 16. Lumière chaude, aucun gris sale | vert, comme la famille revue en T34E : ombre portée brun chaud, ombres internes teintées de la matière (bois, rose, vert). Les greys des sprites teintés deviennent la couleur de la teinte |
| 17. La matière se reconnaît | vert : bois clair cintré du coffret (fils du bois qui suivent la courbe), planches et sangles du coffre, terre cuite, biscuit piqué, papier, encre |
| 33. Géométrie construite, trait constant | vert : contour de 4,5 px sur les icônes de 128 (comme `ICO_ZoneSign` ou `ICO_Codex_Book`), 9,5 px sur la boîte de 256, 6 px sur le coffre de 192 ; coins arrondis partout (au moins 1/24 de la taille : 5,5 px et plus sur les silhouettes) |
| 34. Deux tons et un éclat, lumière du haut et de la gauche | vert par matière. Icônes à plusieurs matières, écart voulu comme le point H de T34E : boîte (bois, bande, étiquette), coffre (bois, sangles, or), Trèfle croquant (biscuit, feuilles, fleur), calendrier (page, en-tête, tampons, étoile). À confirmer par le propriétaire |
| 35. Aucun texte, aucune pseudo-lettre, aucun emoji | vert |
| 36. Lisible à 24 dp, cible de 48 dp | vert à 64 px sur fond clair et sombre. À 32 px, tout se lit encore ; les plus fragiles sont `ICO_Gift` (la patte du couvercle se perd, la bande et l'étiquette portent l'idée de cadeau) et `ICO_Calendar_Days` (les cases deviennent une grille de points). La cible de 48 dp est celle du bouton qui porte l'icône |
| 37. Rareté par étoiles, mot et motif | sans objet |
| 38. Aucune étoile à quatre branches | vert : étoiles rondes à cinq branches (billet, calendrier), points ronds, perles, cœurs, pattes. Aucun appel à `superellipse_points`, aucune étoile à quatre pointes |
| 41. Source éditable, export reproductible | vert : script et masters `@4x` ; un nouveau lancement redonne les mêmes images |
| 42. Aucun générateur | vert : rien d'autre que numpy et Pillow |
| 43. Vocabulaire | vert : noms, identifiants, commentaires et cette note vérifiés contre les listes de `VocabularyRuleTests` |
| 44. Fait par la même main | vert : planche de famille (bas de `T36_sprites_sheet.png`), même contour, même chaleur, même gloss, même ombre |

Points 1 à 11, 14, 15, 18 à 32, 39 et 40 : sans objet pour des sprites d'interface.

Règles du propriétaire, en plus de la grille :

- Aucun nœud : la boîte est ronde, en bois, marquée d'une patte ; la bande fait le tour du corps seulement, avec trois
  perles et une étiquette cœur. Aucune sangle verticale, donc aucune croix sur la face.
- Rien de religieux : aucun symbole. Le trèfle est dessiné par sa fleur ronde et deux feuilles rondes (le Trèfle doux du
  doc 01), pas par un emblème à trois lobes. Le soleil est celui de la Lumière (orbe chaud, rayons ronds).
- Le tampon est une patte dans un anneau, comme la fiche 19.

## Pour le côté Unity

- Import : automatique par `TextureImportRules` (préfixes `UI_` et `ICO_` sous `Art/UI`) : sprite, 100 PPU, sans
  mipmaps, clamp, pivot au centre. Aucun sprite T36 n'est découpé en neuf (bordures à zéro).
- Couches partagées : `UI_GiftBox_Base`, `UI_GiftBox_Band` et `UI_GiftBox_Lid` se posent sur le même rectangle de
  256 x 256, dans cet ordre. Pour faire basculer le couvercle autour de son propre centre, le placer sous un parent dont le
  pivot est à (0,5 ; 0,73) du cadre (centre visuel du couvercle à x 128, y 69 depuis le haut).
- Bulle : `UI_Bubble_Wish` puis `UI_Bubble_Ring`, même rectangle de 144 x 224. Le cercle est centré sur l'image : rayon
  de la bulle 58 px, anneau à 49,5 px de rayon, 7 px d'épaisseur. `Image.Type = Filled`, `Radial360`, origine en haut,
  sens horaire, ou le shader par angle autour de (0,5 ; 0,5). L'icône du souhait tient dans un cercle de 84 px au
  centre. Le haut du cadre est vide (prix du centrage). Le dernier point de la traîne est à x 41, y 217 depuis le coin
  haut gauche : c'est lui qui vise la tête de l'animal.
- Case du calendrier : tuile de 100 x 100 centrée en (56, 58) ; l'en-tête occupe les 23 px du haut de la tuile (numéro
  du jour), l'icône du lot se centre vers (56, 70). Teintes proposées : crème pour une case récupérée, or pour
  aujourd'hui, blanc pour les cases à venir.
- Tampon : à poser au centre de la case, encre déjà translucide (74 à 92 %) pour laisser voir le lot.
- Atlas : `Atlas_CommonHUD` emballe tout `Kit` et `Icons` sur une page de 1024 px au plus sur mobile. Avec T36, la surface
  des sprites (marge de 4 px comprise) fait environ 114 % d'une page de 1024 x 1024 (52 % pour les seuls sprites T36) :
  l'atlas passera sur deux pages. Proposition : un atlas propre à l'écran Aujourd'hui pour la boîte, le coffre, la case
  et le tampon (environ 25 % d'une page), chargé avec l'écran, à décider côté éditeur.
