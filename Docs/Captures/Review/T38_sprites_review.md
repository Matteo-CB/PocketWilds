# T38 : revue « fait main » des sprites du Toilettage

8 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 5 sprites ajoutés par T38 : la patte du geste « glisser » du tutoriel A15, les
trois nœuds du choix de fin de partie et la brosse dorée de la puce du bonus. Planche : `T38_sprites_sheet.png` (ce
dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t38.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T37. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque, aucune police dans les sprites. Lancer `python SourceArt/UI/Tools/pw_ui_sprites_t38.py`
  réécrit seulement ces 5 sprites et leurs masters.
- Reproductible : trois lancements (le script seul, puis deux fois `pw_ui_sprites.py`) donnent des fichiers identiques
  octet pour octet ; les 243 PNG qui existaient avant T38 (sprites et masters) gardent la même empreinte SHA-256.
- Revue : chaque sprite regardé en master 4x, en 3x, à sa taille, à 64 px (environ 24 dp sur un téléphone de 1080 px de
  large) et à 32 px, sur fond clair et sur fond sombre, à côté de douze icônes existantes (brosse du hub, record, défi,
  Jouer, Trèfle croquant, cadeau, cœur, billet, étoile patte, entraînement, temps, Lumière). La patte a été posée à 120
  unités sur sept fonds (pelage lapin, crème, gris, brun foncé, renard, blanc, et le ciel), puis en situation : trois
  positions le long d'un dos rond, vers la droite et au retour. Les nœuds ont été posés dans les boutons ronds crème du
  choix (112 et 72 unités) sur fond clair et sombre ; la brosse dans une puce crème avec l'anneau qui se vide.
- Itérations notables : les traits derrière la patte étaient d'abord des parenthèses « ((( » ; elles se lisaient
  comme des ondes de son, elles sont devenues trois arcs qui suivent le chemin de la patte sur un dos rond, plus pleins
  près de la patte, plus fins et un peu plus clairs derrière (jamais pointus). La patte a gagné son épaisseur de
  peluche du côté qui traîne. Les boucles du nœud étaient d'abord des gouttes trop larges au bout (on aurait dit des
  ailes) : ce sont maintenant des ronds avec un col pincé vers le nœud, et l'intérieur de chaque boucle est un pli en
  goutte dont seul le bord du haut est tracé. Les éclats de la brosse dorée ressemblaient à des bulles (un anneau et un
  centre pâle, et les bulles de savon sont un objet du jeu) : ce sont des points de lumière, blancs au cœur, dorés au
  bord, dans une lueur douce.
- Doc de référence : doc 01 Contenu (S-ACT-001 : brosser, brosse dorée à 20 s, nœuds rose, bleu ciel et crème ; A15 :
  patte qui glisse `UI_Gesture_Swipe` sur le pelage sale), doc « Mini-jeux & activités » (section 3).

## Assets

| Asset | Taille | Dossier | Usage | Teinte |
| --- | --- | --- | --- | --- |
| `UI_Gesture_Swipe` | 192 x 192 | `Art/UI/Kit` | geste « glisser » du tutoriel (A15 « Brosse le pelage ») : la patte de `UI_Gesture_Tap` vue un peu de côté, penchée dans le glissé, coussinets sur le pelage, son épaisseur visible du côté qui traîne, trois arcs doux derrière elle. Dessinée glissant vers la droite | non, couleurs propres (mêmes crème, rose et brun que `UI_Gesture_Tap`) |
| `ICO_Bow_Pink` | 128 x 128 | `Art/UI/Icons` | nœud rose du choix de fin de partie (`GroomingBow.Pink`) : deux boucles rondes, un nœud rond, deux pans courts à encoche | non |
| `ICO_Bow_SkyBlue` | 128 x 128 | idem | nœud bleu ciel (`GroomingBow.SkyBlue`), même dessin | non |
| `ICO_Bow_Cream` | 128 x 128 | idem | nœud crème (`GroomingBow.Cream`), même dessin, contour brun clair chaud pour se lire sur les boutons crème | non |
| `ICO_GoldenBrush` | 128 x 128 | idem | puce du bonus de la brosse dorée : la brosse de `ICO_Act_Grooming` en or chaud, un éclat doux, deux éclats ronds | non |

Masters : `SourceArt/UI/Masters/<nom>@4x.png` pour les 5. Aucun texte dans les sprites : le « x 2 » et les secondes
de la puce sont du texte du jeu (Pocket Round).

Couleurs des nœuds (pour que les nœuds 3D posés sur la tête aient les mêmes) :

| Nœud | Haut des boucles | Bas des boucles | Contour | Pli intérieur |
| --- | --- | --- | --- | --- |
| Rose | FFC6D5 | EF85A3 | 9E3A5A | D9668A |
| Bleu ciel | C9E8FD | 71ABE0 | 2F5E9E | 4F8BCB |
| Crème | FFFCF4 | F0DDBC | B88B58 | DCC198 |

## Grille

| Point de la charte | Résultat |
| --- | --- |
| 12. Aucune texture peinte, aucun bruit | vert : aplats et dégradés construits ; les arcs, les plis du nœud et les fentes des touffes sont des courbes placées à la main |
| 13. Matière mate | vert : un éclat doux par matière, en haut à gauche ; la boucle de droite n'a qu'un reflet très léger (la lumière vient de la gauche) ; le satin du nœud garde un liseré clair très faible |
| 16. Lumière chaude, aucun gris sale | vert : ombres portées brun chaud ; ombres internes teintées de la matière (rose, bleu, crème, or) ; le bout des arcs s'éclaircit vers un brun clair chaud, jamais vers le gris |
| 17. La matière se reconnaît | vert : peluche de la patte, satin du nœud (plis, reflet le long des pans), bois doré de la brosse et ses touffes |
| 33. Géométrie construite, trait constant | vert : contour de 4,5 px sur les icônes de 128 (comme `ICO_Act_Grooming`, `ICO_Record` ou `ICO_Gift`) ; la patte garde le contour de 5,5 px de `UI_Gesture_Tap` sur 192, les arcs ont 4 px ; coins arrondis partout, bouts ronds |
| 34. Deux tons et un éclat au plus, lumière du haut et de la gauche | vert : dégradé du haut vers le bas, une ombre plus profonde en bas, un éclat en haut à gauche, pour chaque matière |
| 35. Aucun texte, aucune pseudo-lettre, aucun emoji | vert : aucun signe ; les plis du nœud suivent ses bords (des courbes, pas des barres) |
| 36. Lisible à 24 dp, cible de 48 dp | vert à 64 px sur fond clair et sombre. À 32 px les nœuds se lisent encore (deux boucles, un nœud) ; la brosse dorée garde sa forme, ses éclats deviennent deux petits points. La patte se lit à 120 unités sur les sept fonds essayés, et encore à 64 |
| 37. Rareté par étoiles, mot et motif | sans objet |
| 38. Aucune étoile à quatre branches | vert : les éclats de la brosse dorée sont des points ronds ; aucun appel à `superellipse_points`, aucun `star_points` à quatre branches |
| 41. Source éditable, export reproductible | vert : script et masters `@4x` ; trois lancements donnent les mêmes octets, aucun sprite existant ne change |
| 42. Aucun générateur | vert : rien d'autre que numpy et Pillow |
| 43. Vocabulaire | vert : noms, identifiants, commentaires et cette note vérifiés contre les listes de `VocabularyRuleTests` (la lueur des éclats s'appelle « glow ») |
| 44. Fait par la même main | vert : planche de famille (bas de `T38_sprites_sheet.png`), même contour, même chaleur, même éclat, même ombre que les icônes de T34A à T37 ; la patte est celle de `UI_Gesture_Tap` (mêmes formes, mêmes couleurs) |

Points 1 à 11, 14, 15, 18 à 32, 39 et 40 : sans objet pour des sprites d'interface.

Règles du propriétaire, en plus de la grille :

- Rien de religieux : le nœud n'a que deux boucles, un nœud et deux pans courts écartés en V, jamais un ruban en croix.
- Aucune étoile à quatre branches : les éclats de la brosse dorée sont des points ronds dans une lueur douce.
- Le geste est une patte en peluche, jamais une main.
- Rien n'entre dans rien : les arcs restent derrière la patte avec un écart, les éclats flottent au-dessus de la brosse
  sans la toucher, les pans partent de sous le nœud.
- Doux : les arcs du geste ont des bouts ronds et s'adoucissent derrière la patte, ce ne sont pas des traits de
  vitesse pointus.

## Pour le côté Unity

- Import : automatique par `TextureImportRules` (préfixes `UI_` et `ICO_` sous `Art/UI`) : sprite, 100 PPU, sans
  mipmaps, clamp, pivot au centre, aucune bordure de neuf (les 5 sont des images simples). Aucune teinte : couleur
  blanche de l'Image pour les 5.
- `UI_Gesture_Swipe` : à utiliser de 120 à 170 unités, comme `UI_Gesture_Tap`. Le coussinet (le point qui glisse sur
  le pelage) n'est pas au centre de l'image : il est à (+27,3 ; -10,2) px du centre sur 192, soit le pivot
  (0,642 ; 0,447). Pour que le coussinet suive le chemin `from` vers `to`, décaler l'image de
  (-27,3 ; +10,2) x taille / 192, ou donner ce pivot au RectTransform. La patte est dessinée glissant vers la droite :
  pour un glissé vers la gauche (ou le retour d'un aller-retour), retourner l'image (échelle x = -1) plutôt que la
  tourner ; pour un chemin en pente, la tourner de l'angle du chemin borné à ±30 degrés. Réduire les animations : la
  patte reste au point de départ, sans glisser.
- Nœuds : posés dans les boutons ronds du choix (`UI_Button_IconCircle` teinté crème) à environ 64 % du diamètre du
  bouton, comme sur la planche (72 unités dans un bouton de 112). Le contour brun clair du nœud crème le garde lisible
  sur le bouton crème.
- Brosse dorée : de 48 à 84 unités dans la puce sous le score. Les éclats sont dans l'image : aucun effet ajouté n'est
  nécessaire pour la lire.
- Atlas : `UI_Gesture_Swipe` va à côté de `UI_Gesture_Tap` dans `Atlas_CommonHUD`. Les quatre icônes vont dans
  `Atlas_Activities` (pipeline des écrans), avec les icônes de T37. Surface avec la marge de 4 px : environ 3,8 % d'une
  page de 1024 x 1024 pour `UI_Gesture_Swipe`, 7,1 % pour les quatre icônes.
