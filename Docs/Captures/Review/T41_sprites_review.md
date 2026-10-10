# T41 : revue « fait main » des sprites du Memory des empreintes

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour
les icônes et l'interface) appliquée aux 10 sprites ajoutés par T41 : le dos de carte du Prairie, la face, le bord
doré de la carte lueur, le badge ×2 de la paire double, la nappe, l'éclat rond de la paire, les empreintes des trois
espèces et le Biscuit aux graines (friandise 3 étoiles du Memory). Planche : `T41_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t41.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T40. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque. Le « ×2 » est dessiné en traits arrondis dans le style de Pocket Round : aucune
  police dans le sprite. Lancer `python SourceArt/UI/Tools/pw_ui_sprites_t41.py` réécrit seulement ces 10 sprites et
  leurs masters.
- Reproductible : deux lancements complets de `main()` (sorties dirigées vers un dossier de travail) donnent 264
  fichiers identiques octet pour octet, tous identiques à ceux du projet ; les 271 PNG qui existaient avant T41
  (sprites et masters) gardent la même empreinte SHA-256.
- Revue : chaque sprite regardé en master 4x, à sa taille, puis réduit (96, 64, 48 et 32 px) sur fond clair et sombre ;
  les cartes dans une partie fictive de 4 × 4 sur la nappe à l'échelle d'un téléphone de 1080 px (dos, faces avec les
  vignettes de capture, empreintes teintées, carte lueur, paire double, cœur et éclats d'une paire trouvée) ; les
  empreintes teintées de quatre pelages par espèce sur la face, à 128, 64 et 48 px ; le biscuit à côté des friandises
  de T36 à T40 et dans la cellule de la carte du résultat ; la nappe répétée 3 × 2.
- Écarts mesurés sur la trame 4x (rien n'entre dans rien) : entre deux feuilles du trèfle, contours compris, 2,6 px de
  crème (elles se rejoignent seulement au cœur, sous son point sombre) ; entre le trèfle (tige comprise) et l'anneau de
  points, 5,0 px ; entre deux graines du biscuit, contours compris, 3,5 px ; entre le × et le 2 du badge, contours
  compris, 2,5 px ; entre deux coussinets : lapin 4,1 px, écureuil 7,6 px, hérisson 4,1 px (donc 2 px au moins à 64 px).
  La nappe se répète sans raccord (période de 64 px, écart nul d'une période à l'autre).
- Itérations notables : les feuilles du trèfle, d'abord des disques coupés en quartiers qui se touchaient, faisaient un
  carré à quatre lobes ; ce sont maintenant des feuilles rondes, plus larges vers l'extérieur, séparées par un filet de
  crème, la tige descendant entre les deux feuilles du bas. Les doigts des empreintes, trop petits pour leur contour,
  se lisaient comme des anneaux : coussinets agrandis, contour aminci, chaque empreinte mise à l'échelle et recentrée
  par mesure. Le bord doré se perdait sur la bordure verte d'un dos : il a pris un bord ambre, une lueur plus forte, et
  se lit maintenant sur un dos comme sur une face. Les graines du biscuit se touchaient au centre : écartées.

## Choix faits

- Une seule toile pour les trois cartes : 256 x 340, le corps de la carte (232 x 312, environ 3:4, coins de 22 px)
  centré, une marge de 12 px sur les côtés et de 14 px en haut et en bas pour l'ombre et la lueur. Dos, face et bord
  doré posés sur le même rectangle tombent au pixel près ; un portrait ou une empreinte centrés sur la carte tombent
  dans le cadre de la face (208 x 288 à l'intérieur de son filet). Pas de 9-slice : les cartes gardent leur rapport
  dans les trois grilles.
- Le trèfle : quatre feuilles rondes, aucune pointe vers l'extérieur (jamais une étoile à quatre branches), un peu
  tourné, une tige courbe, une bande pâle arrondie et une nervure par feuille, dans un anneau de points ronds ; un ourlet
  de points ronds verts sur le panneau crème.
- Les empreintes sont blanches (ombre gris clair, contour gris moyen) pour être teintées par la couleur du pelage : le
  contour devient un ton plus profond du pelage, comme tous les contours de la famille, et une empreinte de pelage blanc
  reste lisible sur la face crème. Elles vivent avec les sprites de l'écran (`Art/UI/Activities/Memory`) et non dans
  `Art/UI/Icons` : l'atlas du HUD de la scène (Atlas_CommonHUD, plein) prend tout ce dossier, et la pipeline de l'écran
  les cherche avec les autres sprites du Memory.
- L'éclat de la paire est blanc (teinté par la scène) et rond partout : un centre rond dans une lueur douce.

## Grille de la charte « fait main »

| Point | Cartes et bord doré | Badge ×2 | Nappe | Éclat | Empreintes | Biscuit aux graines |
| --- | --- | --- | --- | --- | --- | --- |
| 33. Lisible à sa taille | vert : à 118 px de large dans la grille (moitié de l'écran) | vert : à 32 px | vert : discret derrière les cartes | vert | vert : à 48 px | vert : à 48 px |
| 34. Lumière du haut à gauche | vert | vert | sans objet | sans objet | vert | vert |
| 35. Contour d'un ton plus profond | vert : vert, tan, ambre | vert | sans objet | sans objet | vert : une fois teintées | vert |
| 36. Formes rondes, pas de pointe dure | vert : feuilles rondes | vert : traits arrondis | vert | vert | vert : coussinets ronds | vert |
| 37. Palette de la maison | vert : vert du brin de trèfle, crème | vert : or miel des médailles | vert | vert | vert | vert : biscuit du Trèfle croquant, graine de `ICO_Seed` |
| 38. Aucune étoile à quatre branches | vert : trèfle rond, aucun éclat pointu | vert : le × a des bouts ronds | vert | vert | vert | vert |
| 41. Source éditable, export reproductible | vert : script et master 4x ; deux rendus identiques | vert | vert | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert | vert | vert | vert | vert | vert |
| 43. Vocabulaire | vert | vert | vert | vert | vert | vert |
| 44. Fait par la même main (13.5) | vert : méthode et outils du kit | vert | vert | vert | vert : comme `ICO_Paw` | vert : comme les friandises de T36 à T40 |

## Assets

| Asset | Taille | Dossier | Usage | Teinte |
| --- | --- | --- | --- | --- |
| `UI_Memory_CardBack_Prairie` | 256 x 340 | `Art/UI/Activities/Memory` | dos de carte du Prairie : bordure verte douce, panneau crème à ourlet de points, trèfle à quatre feuilles rondes | non |
| `UI_Memory_CardFace` | 256 x 340 | idem | face crème, contour tan doux, filet intérieur doux | non |
| `UI_Memory_GlowEdge` | 256 x 340 | idem | bord doré de la carte lueur et lueur de sa paire (la scène fait pulser son alpha) | non |
| `UI_Memory_DoubleBadge` | 128 x 128 | idem | badge rond or miel « ×2 » dans un coin des cartes de la paire double | non |
| `UI_Memory_Tablecloth` | 256 x 256 | idem | fond de l'écran, vichy crème et sauge très doux, opaque, Image en mode Tiled | non |
| `UI_Memory_Glint` | 64 x 64 | idem | éclat rond de la paire (H-VFX-ACT-004-01), deux par paire avec le cœur | oui, blanc |
| `UI_Paw_Rabbit`, `UI_Paw_Squirrel`, `UI_Paw_Hedgehog` | 128 x 128 | idem | empreinte de l'espèce sur une face « empreinte et portrait », teintée de la couleur du pelage | oui, blanc |
| `ICO_Treat_SeedCookie` | 128 x 128 | `Art/UI/Icons` | cellule de la carte du résultat (`treat.seed_cookie`), dans `RewardItemIcons` et Atlas_Activities | non |

Masters : `SourceArt/UI/Masters/<nom>@4x.png` pour les 10. Aucun texte dans les sprites.
