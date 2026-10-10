# T43 : revue « fait main » des sprites du Jardin

10 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive, section 11, points 33 à 38 et 41 à 44 pour les
icônes et l'interface) appliquée aux 58 sprites ajoutés par T43 : les 13 sachets de graines, les 9 produits, les 4
ingrédients manquants, les 25 teintures, l'arrosoir et les 6 sprites de l'écran du Jardin. Planche :
`T43_sprites_sheet.png` (ce dossier).

## Comment c'est fait et vu

- Dessin : `SourceArt/UI/Tools/pw_ui_sprites_t43.py`, même méthode que tout le kit (distances signées rendues en 4x,
  réduites par moyenne), branché dans `main()` de `pw_ui_sprites.py` comme T34A à T42. Aucun générateur, aucune image
  téléchargée, aucune bibliothèque, aucune police dans les sprites. `python SourceArt/UI/Tools/pw_ui_sprites_t43.py`
  réécrit seulement ces 58 sprites et leurs masters (`SourceArt/UI/Masters/<nom>@4x.png`).
- Chaque plante est dessinée une seule fois par sa fonction de motif (`m_clover`, `m_strawberry`, ... `m_sweet_pea`), à
  toute échelle : entière sur l'icône du produit, petite dans la fenêtre ronde de son sachet. Les pièces d'un motif ont
  chacune leur contour d'un ton plus profond, dessinées de l'arrière vers l'avant.
- Reproductible : un second rendu en mémoire donne les 58 sprites identiques à l'octet aux fichiers écrits. Aucun sprite
  existant n'est réécrit.
- Revue : chaque icône à 128 px, puis à 48 px sur fond crème et sur bleu nuit ; les 9-slices étirés comme à l'écran (la
  pilule de niveau à 420 px, l'étiquette de parcelle à 300 px avec la goutte et le cœur, la carte du sachet à 200 x 260
  avec un sachet posé dessus) ; la pancarte à sa taille.
- Rien n'entre dans rien : les baies, les fleurons, les grains du blé et les points des ourlets restent séparés ou se
  recouvrent comme de vrais pétales, chacun avec son contour ; le motif de chaque sachet tient dans sa fenêtre ; la
  tranche de melon est posée devant le melon avec son propre contour.
- Itérations : le sac de farine se lisait comme un bocal à couvercle (redessiné : col replié, dôme de farine, ficelle,
  farine renversée au pied) ; le melon se lisait comme un chou (une tranche à chair pêche ajoutée devant, la couleur de
  sa teinture) ; les pépins de la tranche tombaient hors de la chair (demi-disque retourné) ; la pointe des gouttes
  arrondie davantage.

## Choix faits

- Noms : `ICO_Seed_<Plante>`, `ICO_Crop_<Produit>` pour `crop.<nom>`, `ICO_Ingredient_<Nom>` pour `ingredient.<nom>`
  (comme `ICO_Ingredient_Strawberry` et `ICO_Ingredient_Hazelnut` de T40, réutilisés), `ICO_Dye_<Nom>` pour `dye.<nom>`,
  le nom en PascalCase de l'identifiant (`dye.lingonberry_garnet` donne `ICO_Dye_LingonberryGarnet`).
- Le sachet porte en bas une bande de la couleur de la teinture de sa plante (table 9.2) : les 13 sachets se
  distinguent d'un coup d'œil, et la couleur annonce la teinture.
- Les gouttes de teinture ont leur bande du milieu exactement de la couleur de la table, plus claire vers la pointe et
  un rien plus profonde en bas, un contour d'un ton plus profond de la même couleur (gris chaud pour Blanc nuage) et une
  petite flaque de la même couleur.
- Le tournesol du sachet est la tête seule ; la lavande du produit est un bouquet de trois brins noué d'un ruban rose.
- Les icônes vont dans `Art/UI/Icons` (partagées : inventaire, souhaits, récompenses) ; la pilule, l'étiquette, la
  pancarte, la carte, la goutte et le cœur dans `Art/UI/Activities/Garden` (`GardenLayout.SpriteFolder`).
- 9-slices (bords dans `TextureImportRules.UiSpriteBorder`, gauche, bas, droite, haut) : `UI_Garden_LevelPill` 128, 40,
  56, 52 (le badge et la pousse dans le bord gauche) ; `UI_Garden_BedLabel` 48 partout ; `UI_Garden_SeedPacketCard` 64,
  72, 64, 64 (la bande verte du pied dans le bord bas).
- Réutilisés sans redessiner : `ICO_Act_Garden`, `ICO_Ingredient_Strawberry`, `ICO_Ingredient_Hazelnut`, `ICO_Lock`,
  `ICO_Timer`, `ICO_Challenge`, `ICO_Currency_Gems`, `ICO_Seed` (graine générique).

## Grille de la charte « fait main »

| Point | Sachets | Produits et ingrédients | Teintures | Arrosoir | Écran (pilule, étiquette, pancarte, carte, goutte, cœur) |
| --- | --- | --- | --- | --- | --- |
| 33. Lisible à sa taille | vert : plante lisible à 48 px | vert : à 48 px | vert : à 48 px | vert : à 48 px | vert : 9-slice à toute taille |
| 34. Lumière du haut à gauche | vert | vert | vert | vert | vert |
| 35. Contour d'un ton plus profond | vert : brun chaud, bande et fenêtre | vert : chaque pièce | vert : ton profond de la teinte | vert : sarcelle | vert : tan, vert, brun du bois |
| 36. Formes rondes, pas de pointe dure | vert | vert : pétales et grains ronds | vert : pointe arrondie | vert | vert |
| 37. Palette de la maison | vert : crème des cartes, vert du trèfle | vert | vert : couleurs de la table 9.2 | vert : menthe de `ICO_Act_Garden` | vert : crème, vert feuille, bois |
| 38. Aucune étoile à quatre branches | vert : coquelicot à cinq pétales | vert : tournesol à quatorze pétales séparés | vert | vert | vert |
| 41. Source éditable, export reproductible | vert : script et master 4x ; deux rendus identiques | vert | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert | vert | vert | vert | vert |
| 43. Vocabulaire | vert | vert | vert | vert | vert |
| 44. Fait par la même main (13.5) | vert : méthode et outils du kit | vert : trèfle de T36, pâquerette de T42, fraise de T40 | vert | vert : arrosoir de T37 | vert : comme les cartes de T41 et T42 |

## Assets

Icônes dans `Assets/_Game/Art/UI/Icons/`, écran dans `Assets/_Game/Art/UI/Activities/Garden/`, masters dans
`SourceArt/UI/Masters/<nom>@4x.png`. Toutes en image Simple sauf les trois 9-slices.

| Asset | Taille | Usage |
| --- | --- | --- |
| `ICO_Seed_SweetClover`, `_WildStrawberry`, `_Daisy`, `_RoundCarrot`, `_Poppy`, `_Blueberry`, `_Lavender`, `_GoldenWheat`, `_MeadowIndigo`, `_Sunflower`, `_DwarfApple`, `_RoundMelon`, `_SweetPea` | 128 x 128 | sachet de graines de chaque plante de lancement |
| `ICO_Crop_CloverFlower`, `_Daisy`, `_RoundCarrot`, `_Poppy`, `_Lavender`, `_IndigoFlower`, `_Sunflower`, `_Melon`, `_SweetPeaFlower` | 128 x 128 | produits `crop.*` |
| `ICO_Ingredient_WheatFlour`, `_Blueberry`, `_Apple`, `_FlowerSugar` | 128 x 128 | ingrédients de Pâtisserie manquants |
| `ICO_Dye_<Nom>` (25, table 9.2) | 128 x 128 | goutte de chaque teinture |
| `ICO_WateringCan` | 128 x 128 | bouton de l'arrosoir |
| `UI_Garden_LevelPill` | 288 x 112 | pilule du niveau du Jardin, badge vert du numéro | 
| `UI_Garden_BedLabel` | 160 x 104 | étiquette au-dessus d'une parcelle (Sliced, bords 48) |
| `UI_Garden_CoverSign` | 192 x 192 | pancarte en bois d'une parcelle fermée |
| `UI_Garden_SeedPacketCard` | 192 x 232 | carte d'un sachet dans la feuille des graines (Sliced, bords 64, 72, 64, 64) |
| `UI_Garden_Drop` | 64 x 64 | goutte : parcelle arrosée |
| `UI_Garden_Heart` | 64 x 64 | petit cœur vert : les voisines s'entraident |
