# T37 : revue « fait main » des sprites du cadre des activités

8 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 27 sprites ajoutés par T37 : bouton Jouer, entraînement, défi, record, temps,
cadenas, pause, étoiles du résultat et une icône par activité pour les cartes du hub. Planche : `T37_sprites_sheet.png`
(ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t37.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A, T35A, T35B et T36. Aucun générateur,
  aucune image téléchargée, aucune bibliothèque, aucune police dans les sprites. Lancer
  `python SourceArt/UI/Tools/pw_ui_sprites_t37.py` réécrit seulement ces 27 sprites et leurs masters.
- Reproductible : trois lancements (le script seul, puis deux fois `pw_ui_sprites.py`) donnent des fichiers identiques
  octet pour octet ; les 189 PNG qui existaient avant T37 (sprites et masters) gardent la même empreinte SHA-256.
- Revue : chaque sprite regardé à sa taille, en 2x, 3x et 4x, puis à 64 px (environ 24 dp sur un téléphone de 1080 px de
  large) et 32 px, sur fond clair et sur fond sombre, à côté de douze icônes existantes (Codex, sac, Améliorer,
  pancarte, cœur, Lumière, nid, billet, cadenas ouvert, étoile patte, cadeau, Aujourd'hui). Les étoiles ont été
  superposées comme le fera la carte de résultat, et la planche montre aussi quatre cartes du hub (ouverte, record,
  à moitié, verrouillée), la barre du jeu et le bouton Jouer sur le bouton rond du rail à 80, 56 et 40 px.
- Itérations notables : la balle du bouton Jouer est devenue une balle à côtes (calotte, côtes corail, crème et bleu
  ciel) pour ne ressembler à aucune balle connue d'une autre licence ; la noisette a gagné sa base pâle au bon endroit
  (en bas) et perdu ses traits de chute, lus comme des tiges ; la brosse est vue de profil (lue comme une brosse, plus
  comme un miroir) ; la luge est passée dans une fenêtre ronde d'hiver (sans elle la pente se lisait mal) ; les lucioles
  ont reçu une petite tête brune et deux ailes ; l'arrosoir a été réduit et levé pour que les gouttes tombent entre lui
  et les feuilles.
- Doc de référence : doc « Mini-jeux & activités » (sections 1 à 3), doc 06 (écrans 029 et 030, inventaire d'icônes :
  ICO_Play, ICO_Practice, ICO_Record, étoiles vides et pleines), doc 01 Contenu (section 12, une activité par fiche).

## Assets

| Asset | Taille | Dossier | Usage | Teinte |
| --- | --- | --- | --- | --- |
| `ICO_Play` | 128 x 128 | `Art/UI/Icons` | bouton Jouer du rail du HUD : balle à côtes, patte corail sur la côte crème | non, couleurs propres |
| `ICO_Practice` | 128 x 128 | idem | entraînement sans récompense : cible douce crème et sauge, plume lilas posée sur le centre | non |
| `ICO_Challenge` | 128 x 128 | idem | défi fixe : médaille dorée ronde, étoile à cinq branches en relief, ruban bleu ciel en V | non |
| `ICO_Record` | 128 x 128 | idem | nouveau record : cocarde rose plissée, disque crème, étoile dorée arrondie, deux pans | non |
| `ICO_Timer` | 128 x 128 | idem | temps de la partie : horloge ronde en feuille, cadran crème, aiguilles à dix heures dix, tige et feuille | non |
| `ICO_Lock` | 128 x 128 | idem | carte d'activité verrouillée : cadenas fermé, jumeau de `ICO_Unlock` (il n'existait pas de cadenas fermé) | non |
| `ICO_Pause` | 96 x 96 | idem | bouton pause du jeu : deux barres rondes | oui, blanc comme `ICO_Close_X` et `ICO_Back_Chevron` |
| `UI_Star_Result_Empty` | 192 x 192 | `Art/UI/Kit` | étoile pas encore gagnée : creux crème à contour beige, sans ombre portée | non |
| `UI_Star_Result_Full` | 192 x 192 | idem | étoile gagnée : or chaud, un éclat ; même cadre, recouvre la vide jusqu'au dernier pixel du bord | non |
| `ICO_Act_Grooming` | 128 x 128 | `Art/UI/Icons` | Toilettage : brosse en bois de profil, touffes roses, petite feuille qui s'envole | non |
| `ICO_Act_HideAndSeek` | 128 x 128 | idem | Cache-cache : deux oreilles crème et rose qui dépassent d'un buisson rond à baies | non |
| `ICO_Act_NutRain` | 128 x 128 | idem | Pluie de noisettes : trois noisettes qui tombent vers un panier tressé, une déjà dedans | non |
| `ICO_Act_PawMemory` | 128 x 128 | idem | Memory des empreintes : deux cartes retournées (lilas et bleu ciel), une patte sur chaque dos | non |
| `ICO_Act_Expeditions` | 128 x 128 | idem | Expéditions : sac à dos orange devant une carte pliée (bois, lac, chemin pointillé, cœur) | non |
| `ICO_Act_Garden` | 128 x 128 | idem | Jardin : pousse à deux feuilles dans un pot de terre cuite, arrosoir menthe, trois gouttes | non |
| `ICO_Act_TreasureFishing` | 128 x 128 | idem | Pêche aux trésors : petite canne, coquillage rose remonté de l'eau, deux gouttes, ronds dans l'eau ; aucun hameçon, aucun poisson | non |
| `ICO_Act_LilyHop` | 128 x 128 | idem | Saut de nénuphars : feuille de nénuphar à encoche, patte crème, petite feuille derrière, arc pointillé du saut ; aucune grenouille, aucune fleur | non |
| `ICO_Act_Bakery` | 128 x 128 | idem | Pâtisserie : gâteau rond à étages, glaçage qui coule, couche de confiture rose, cerise | non |
| `ICO_Act_PhotoSafari` | 128 x 128 | idem | Safari photo : appareil jouet bleu ciel à bande crème, gros objectif, déclencheur corail | non |
| `ICO_Act_CuddleMatch` | 128 x 128 | idem | Match des câlins : trois jetons ronds roses à cœur, celui du milieu soulevé, trois points dorés | non |
| `ICO_Act_Slide` | 128 x 128 | idem | Glissade : fenêtre ronde d'hiver, pente de neige, luge en bois à lattes rouges et patin recourbé, bouffées de neige | non |
| `ICO_Act_Constellations` | 128 x 128 | idem | Constellations : ciel de nuit bleu (jamais noir) dans un rond, étoiles rondes à cinq branches et points reliés ; aucune lune | non |
| `ICO_Act_FireflyRhythm` | 128 x 128 | idem | Rythme des lucioles : ligne en vague lilas, trois lucioles rondes et lumineuses, tête brune, deux ailes | non |
| `ICO_Act_Bubbles` | 128 x 128 | idem | Bulles : quatre bulles douces de tailles et de couleurs pastel différentes | non |
| `ICO_Act_TilePuzzle` | 128 x 128 | idem | Taquin : plateau en bois de 3 x 3 tuiles portant un cœur, une case vide, la voisine qui glisse | non |
| `ICO_Act_BurrowMaze` | 128 x 128 | idem | Labyrinthe du terrier : butte d'herbe, entrée de terrier brun chaud, chemin sablé qui serpente, graines | non |
| `ICO_Act_GrandContest` | 128 x 128 | idem | Grand Concours : podium rond lilas à deux étages, patte sur le dessus, cocarde dorée à cœur et pans bleus | non |

Masters : `SourceArt/UI/Masters/<nom>@4x.png` pour les 27. Aucun texte dans les sprites : noms, scores, temps, records
et nombres de défis sont du texte du jeu (Pocket Round).

## Grille

| Point de la charte | Résultat |
| --- | --- |
| 12. Aucune texture peinte, aucun bruit | vert : aplats et dégradés construits. Les côtes de la cocarde, les plis de la cocarde dorée et le tressage du panier sont des lignes et des cosinus placés à la main, pas un bruit |
| 13. Matière mate | vert : un seul éclat doux par matière, en haut à gauche ; le verre de l'objectif a un éclat et un point, comme une perle d'œil |
| 16. Lumière chaude, aucun gris sale | vert : ombre portée brun chaud (bleu froid seulement sous la luge, sur la neige) ; ombres internes teintées de la matière (bois, rose, vert, or, lilas) ; le blanc de `ICO_Pause` devient la couleur de la teinte |
| 17. La matière se reconnaît | vert : bois de la brosse, de la canne, du plateau et de la luge, osier du panier, coque de noisette et sa base rugueuse, terre cuite, métal de l'arrosoir, papier de la carte, ruban, neige, eau, glaçage |
| 33. Géométrie construite, trait constant | vert : contour de 4,5 px sur les icônes de 128 (comme `ICO_ZoneSign`, `ICO_Ticket` ou `ICO_Gift`), 3 à 3,5 px sur les petites pièces posées devant (noisettes, petite bulle, lucioles, petite feuille de nénuphar, pans de la cocarde du podium, luge), 6,5 px sur l'étoile de 192 ; coins arrondis partout |
| 34. Deux tons et un éclat, lumière du haut et de la gauche | vert par matière. Icônes à plusieurs matières, écart voulu comme le point H de T34E et la revue T36 : jardin (terre cuite, feuilles, métal, eau), pêche (bois, coquillage, eau), pâtisserie (génoise, confiture, glaçage, cerise), concours (podium, or, ruban). À confirmer par le propriétaire |
| 35. Aucun texte, aucune pseudo-lettre, aucun emoji | vert : les cartes, le plateau et la carte pliée portent des pattes, des cœurs et des points, jamais de signe |
| 36. Lisible à 24 dp, cible de 48 dp | vert à 64 px sur fond clair et sombre. À 32 px tout se lit encore ; les plus fragiles sont `ICO_Act_Expeditions` (la carte devient un rectangle crème, le sac porte l'idée), `ICO_Act_FireflyRhythm` (trois points lumineux sur une vague) et `ICO_Act_Slide` (la luge devient un trait rouge dans le rond bleu). `ICO_Play` se lit à 40 px sur le bouton rond du rail |
| 37. Rareté par étoiles, mot et motif | sans objet (les étoiles du résultat disent le score, pas la rareté) |
| 38. Aucune étoile à quatre branches | vert : étoiles rondes à cinq branches (défi, record, constellations, résultat), points ronds, perles, bulles, cœurs, pattes. Aucun appel à `superellipse_points`, aucun `star_points` à quatre branches |
| 41. Source éditable, export reproductible | vert : script et masters `@4x` ; trois lancements donnent les mêmes octets, aucun sprite existant ne change |
| 42. Aucun générateur | vert : rien d'autre que numpy et Pillow |
| 43. Vocabulaire | vert : noms, identifiants, commentaires et cette note vérifiés contre les listes de `VocabularyRuleTests` |
| 44. Fait par la même main | vert : planche de famille (bas de `T37_sprites_sheet.png`), même contour, même chaleur, même éclat, même ombre que les icônes de T34A à T36 |

Points 1 à 11, 14, 15, 18 à 32, 39 et 40 : sans objet pour des sprites d'interface.

Règles du propriétaire, en plus de la grille :

- Rien de religieux : aucun symbole. Pas de lune en croissant à côté d'une étoile (les constellations sont dans un ciel
  rond sans lune), pas de fleur sur l'eau (le nénuphar n'a que ses feuilles), aucun nœud ni ruban en croix.
- Aucune étoile à quatre branches : toutes les étoiles ont cinq branches arrondies, les éclats sont des points ronds.
- Rien n'entre dans rien : la plume se pose sur la cible (aucune flèche plantée), les oreilles sont derrière le
  buisson, les gouttes tombent dans le vide entre l'arrosoir et les feuilles, le coquillage pend à sa ligne au-dessus
  de l'eau, les jetons du match et les bulles se touchent par leur contour sans se traverser.
- Aucun animal pêché, aucune grenouille dessinée : la pêche remonte un coquillage, le saut montre une patte du joueur.
- Calme et gentil : l'horloge n'a ni cloche ni sonnerie, l'entraînement n'a pas d'or (rien en jeu).

## Pour le côté Unity

- Import : automatique par `TextureImportRules` (préfixes `UI_` et `ICO_` sous `Art/UI`) : sprite, 100 PPU, sans
  mipmaps, clamp, pivot au centre, aucune bordure de neuf (les 27 sont des images simples).
- Teinte : seul `ICO_Pause` est blanc, à teinter (brun chaud du HUD sur le bouton rond crème `UI_Button_IconCircle`,
  comme la fermeture). Tous les autres gardent la couleur blanche de l'Image.
- Étoiles du résultat : `UI_Star_Result_Empty` puis `UI_Star_Result_Full` dans le même rectangle de 192 x 192 (ou à la
  même échelle). Le centre de l'étoile est à 99 px du haut du cadre : pour un pop centré sur l'étoile, pivot à
  (0,5 ; 0,484). La vide n'a pas d'ombre, la pleine en a une. À utiliser de 96 à 192 unités ; pour les petites marques
  (40 unités sur les cartes du hub, 32 dans les défis), garder `ICO_Rarity_Star` teinté : sans mipmaps, une image de
  192 réduite six fois scintille.
- Cartes du hub : les icônes `ICO_Act_*` sont dessinées à 128 px ; posées à 128 unités elles restent nettes, à 148
  unités elles sont agrandies de 16 %. Si la carte garde 148, je peux les rendre en 160 ou 192 px (un paramètre).
- Carte verrouillée : icône de l'activité éclaircie et désaturée (couleur de l'Image vers un gris chaud, ou alpha), et
  `ICO_Lock` à environ 44 unités sur le bord bas droit du disque, comme sur la planche.
- Atlas : `Atlas_Activities` (pipeline des écrans) prend les `ICO_Act_*` et les icônes propres aux écrans. Les noms à y
  mettre sont `ICO_Practice`, `ICO_Challenge`, `ICO_Record`, `ICO_Pause`, `ICO_Timer`, `ICO_Lock`,
  `UI_Star_Result_Empty` et `UI_Star_Result_Full` (les deux étoiles sont dans `Kit`, pas dans `Icons`). `ICO_Play` reste
  dans `Atlas_CommonHUD` (bouton du HUD). Surface avec la marge de 4 px : environ 47 % d'une page de 1024 x 1024 pour
  `Atlas_Activities`, 1,7 % pour `ICO_Play` dans `Atlas_CommonHUD`.
