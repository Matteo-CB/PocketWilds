# T39 : revue « fait main » des icônes du Cache-cache

9 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 2 sprites ajoutés par T39 : la loupe et le sifflet des boutons d'objets du
Cache-cache. Planche : `T39_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t39.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T38. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque, aucune police dans les sprites. Lancer `python SourceArt/UI/Tools/pw_ui_sprites_t39.py`
  réécrit seulement ces 2 sprites et leurs masters.
- Reproductible : le script seul, puis deux lancements complets de `main()` (sorties dirigées vers un dossier de
  travail) donnent des fichiers identiques octet pour octet ; les 230 PNG que dessine `main()` sont identiques à ceux du
  projet, et les 253 PNG qui existaient avant T39 (sprites et masters) gardent la même empreinte SHA-256. Trois
  lancements complets dans le projet ont été interrompus par un fichier tenu un instant par un autre programme (erreur
  d'écriture de Windows) : les fichiers déjà réécrits avaient les mêmes octets, rien n'a changé.
- Revue : chaque icône regardée en master 4x, en 2x, à sa taille, à 64 px (environ 24 dp sur un téléphone de 1080 px
  de large), à 48, 32 et 24 px, sur fond clair et sur fond sombre, à côté de dix icônes existantes (cache-cache du
  hub, brosse, brosse dorée, appareil photo, temps, entraînement, défi, billet, Trèfle croquant, cœur). Posées dans le
  bouton rond crème à 150, 96 et 75 unités, libres et utilisées (grisées, avec une coche douce), puis sur deux captures
  réelles du jeu (T38) dans le coin bas du terrain, au tiers et à la taille du téléphone.
- Écarts mesurés (rien n'entre dans rien) : entre la première note et le sifflet, 4,5 px de vide sur 128 ; entre les
  deux notes, 3,9 px ; l'anneau doré passe derrière le bord de la chambre, la ficelle passe dans l'anneau ; la patte est
  derrière le verre, coupée par le bord de la lentille.
- Itérations notables : le premier sifflet sortait du cadre en bas et à gauche, et sa fenêtre était une boîte vide
  (une encoche trop large qui laissait voir le fond) : la fenêtre est maintenant une fente sombre avec la lèvre en
  biais, comme un vrai sifflet. La ficelle a été essayée en crochet (on aurait dit un ver), en vague (une langue), en
  boucle pincée (un cœur) et en anneau rond (une bouée) : elle est devenue une boucle qui pend, étroite en haut, ronde
  en bas, un peu penchée, fermée par une perle sous l'anneau. Le bois du sifflet, d'abord trop pâle (on aurait dit du
  plastique beige), a pris un ton miel plus chaud, partagé avec le manche de la loupe. Les notes touchaient la chambre :
  elles ont été placées par mesure, au-dessus de la fente, en montant doucement vers la droite.
- Doc de référence : doc 01 Contenu (S-ACT-002, tableau des objets : loupe 2 s qui entoure la cachette occupée la plus
  proche, sifflet qui fait remuer les animaux encore cachés ; J-SFX-ACT-002-05 « deux notes de flûte douce », d'où les
  deux notes), doc « Mini-jeux & activités » (section 3). Le sifflet sert aussi au Safari photo (S-ACT-010).

## Assets

| Asset | Taille | Dossier | Usage | Teinte |
| --- | --- | --- | --- | --- |
| `ICO_Magnifier` | 128 x 128 | `Art/UI/Icons` | bouton de la Loupe (`IActivityHudView.ShowItems`) : une loupe ronde, cerclage or miel, verre bleu pâle avec un reflet courbe et un petit point de lumière, une patte bleu plus profond vue à travers, une bague corail, un manche en bois chaud terminé par un pommeau rond percé | non, couleurs propres |
| `ICO_Whistle` | 128 x 128 | idem | bouton du Sifflet (Cache-cache, puis Safari photo) : un petit sifflet rond en bois vu de côté, la face de la chambre montre le bois de bout (un cerne, le cœur), l'embouchure monte vers la gauche, la fente ouverte sur le dessus, deux notes rondes (corail, or) qui s'en échappent, une ficelle corail en boucle passée dans un anneau doré | non |

Masters : `SourceArt/UI/Masters/ICO_Magnifier@4x.png` et `ICO_Whistle@4x.png`. Aucun texte dans les sprites.

Couleurs (si un effet ou un modèle doit s'accorder) :

| Matière | Haut | Bas | Contour |
| --- | --- | --- | --- |
| Bois (manche, sifflet) | F3C07E | B46F35 | 5E3F24 |
| Face de la chambre | F8D7A6 | CB8A4A | cerne B8763A |
| Or miel (cerclage, anneau, note) | FFE48A | F0A73A | A8662A |
| Corail (bague, ficelle, note) | FFA594 | E2654F | 9C3E3A |
| Verre | F2FAFF | B4DCF4 | patte 7FA8D6 à 5D88C0 |

## Grille

| Point de la charte | Résultat |
| --- | --- |
| 12. Aucune texture peinte, aucun bruit | vert : aplats et dégradés construits ; le cerne du bois de bout, la fente et le reflet du verre sont des formes placées à la main |
| 13. Matière mate | vert : un reflet par matière, en haut à gauche (le verre a un reflet courbe et un petit point, comme la lentille de l'appareil photo) |
| 16. Lumière chaude, aucun gris sale | vert : ombres portées brun chaud ; ombres internes teintées de la matière (bois, or, corail, bleu du verre) ; la patte vue à travers le verre est un bleu plus profond du verre, jamais un brun grisé |
| 17. La matière se reconnaît | vert : verre (reflet courbe, bord plus profond), or du cerclage, bois (bois de bout et cerne sur la face, fil de lumière sur l'embouchure et le manche), ficelle corail |
| 33. Géométrie construite, trait constant | vert : contour de 4,5 px sur 128, comme `ICO_Act_Grooming` ou `ICO_Timer` ; 2,6 px pour la ficelle, 2 px pour l'anneau et les notes ; coins arrondis partout, bouts ronds |
| 34. Deux tons et un éclat au plus, lumière du haut et de la gauche | vert : dégradé du haut vers le bas, une ombre plus profonde en bas, un éclat en haut à gauche, pour chaque matière |
| 35. Aucun texte, aucune pseudo-lettre, aucun emoji | vert : aucun signe ; les notes sont des points ronds, sans hampe |
| 36. Lisible à 24 dp, cible de 48 dp | vert : à 48 px dans le bouton de 75 unités, la loupe (cercle, manche, patte) et le sifflet (embouchure, chambre, boucle) se lisent sur fond clair et sombre ; à 32 px la loupe se lit encore, le sifflet garde sa silhouette, ses notes deviennent deux petits points ; utilisées et grisées, les deux gardent leur forme |
| 37. Rareté par étoiles, mot et motif | sans objet |
| 38. Aucune étoile à quatre branches | vert : notes et points de lumière ronds ; aucun appel à `superellipse_points`, aucun `star_points` |
| 41. Source éditable, export reproductible | vert : script et masters `@4x` ; lancements identiques octet pour octet, aucun sprite existant ne change |
| 42. Aucun générateur | vert : rien d'autre que numpy et Pillow |
| 43. Vocabulaire | vert : noms, identifiants, commentaires et cette note vérifiés contre les listes de `VocabularyRuleTests` (le reflet du verre s'appelle « gloss », le point de lumière « glint ») |
| 44. Fait par la même main | vert : planche de famille (bas de `T39_sprites_sheet.png`) à 128, 64 et 32 px, même contour, même chaleur, même éclat, même ombre que les icônes de T34A à T38 ; l'or est celui de `ICO_Challenge`, le corail celui de `ICO_Act_PhotoSafari` |

Points 1 à 11, 14, 15, 18 à 32, 39 et 40 : sans objet pour des sprites d'interface.

Règles du propriétaire, en plus de la grille :

- Rien de religieux : une loupe, un sifflet, une patte, deux notes rondes.
- Aucune étoile à quatre branches : les deux notes sont des points ronds, jamais des traits pointus ni des étoiles.
- Rien n'entre dans rien : les notes flottent au-dessus de la fente avec un vide mesuré, la ficelle passe dans
  l'anneau, l'anneau passe derrière la chambre, la patte reste derrière le verre.
- Doux : bouts ronds, coins arrondis, une boucle qui pend au lieu d'un cordon raide.

## Pour le côté Unity

- Import : automatique par `TextureImportRules` (préfixe `ICO_` sous `Art/UI`) : sprite, 100 PPU, sans mipmaps,
  clamp, pivot au centre, aucune bordure de neuf. Aucune teinte : couleur blanche de l'Image pour les 2.
- Boutons : posées dans le bouton rond des objets (`UI_Button_IconCircle` teinté crème) à environ 64 % de son diamètre,
  comme sur la planche (96 unités dans un bouton de 150). Lues aussi à 75 unités (icône de 48).
- Objet utilisé : la planche montre la forme grisée (luminance vers un gris chaud, 75 % d'opacité) avec `ICO_Check` en
  bas à droite ; les deux silhouettes restent lisibles ainsi.
- Atlas : les deux icônes vont dans `Atlas_Activities` avec celles de T37 et T38 (liste tenue par le pipeline des
  écrans). Surface avec la marge de 4 px : environ 3,5 % d'une page de 1024 x 1024.
- Emprunt dans la scène de l'activité : `host.Hud.Icon("ICO_Magnifier")` et `host.Hud.Icon("ICO_Whistle")`, jamais une
  copie dans le groupe `Activity_HideAndSeek`.
