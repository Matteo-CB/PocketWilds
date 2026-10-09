# Polices : vietnamien et latin complété (module `viet`)

Décision du propriétaire du 9 octobre 2026 : nos propres polices pour toutes les langues. Pocket Round (Regular,
SemiBold, Bold) et Pocket Plush (Black) reçoivent le vietnamien et les dernières lettres latines manquantes, dessinés
par notre code Python avec la même plume que le latin.

## Ce qui est couvert : 105 caractères par style

- **Vietnamien, 94 caractères** : tout le bloc U+1EA0 à U+1EF9 (Ạ à ỹ) et Ơ ơ Ư ư (U+01A0, U+01A1, U+01AF, U+01B0).
  Đ đ, À Á Â Ã È É Ê Ì Í Ò Ó Ô Õ Ù Ú Ý, Ă Ĩ Ũ et le ₫ étaient déjà dans notre jeu.
- **Azerbaïdjanais** : Ə ə (U+018F, U+0259).
- **Croate** : les digrammes de compatibilité Ǆ ǅ ǆ Ǉ ǈ ǉ Ǌ ǋ ǌ (U+01C4 à U+01CC).
- Vérifié : le roumain (Ș ș Ț ț Ă Â Î), le turc, le polonais, le tchèque, le hongrois (Ő Ű), le croate (Č Ć Đ Š Ž) et
  l'indonésien n'ont besoin d'aucune autre lettre du latin étendu B.
- Police de test : 369 glyphes latins + 105 = 474 glyphes dans chaque style.

## Comment c'est construit

- Même idée que `decompose` du build : la lettre de base latine, puis ses signes. Les accents seuls (à á ã â ă) sont
  exactement ceux du latin existant, pour que le texte vietnamien ait le même grain que le français.
- **Corne** (Ơ ơ Ư ư) : dessinée avec la lettre, elle part de l'épaule du O ou du haut du fût du U, monte vers la droite
  et finit droite, bout rond. Les tons se centrent sur le corps de la lettre, jamais sur la corne.
- **Crochet** (dấu hỏi) : le haut d'un point d'interrogation, ouvert en bas à gauche, queue revenue au centre. Plume un
  peu plus légère que les autres signes pour qu'il reste ouvert jusqu'à 18 px.
- **Point souscrit** (dấu nặng) : rond, sous le centre de la lettre ; sous y, il se décale à droite de la queue.
- **Signes empilés, à la vietnamienne** : l'aigu et le crochet en haut à droite de l'accent circonflexe, côte à côte ;
  le grave en haut à gauche ; le tilde au-dessus du circonflexe ; tous les tons au-dessus de la brève. Chaque signe est
  posé en mesurant l'écart d'encre réel, aucune graisse ne fait se toucher deux signes ni un signe et sa lettre.
- **Capitales** : les grappes rétrécissent jusqu'à tenir sous un plafond de 1000 unités (Regular), 1010 (SemiBold),
  1021 (Bold) et 1045 (Plush, dont les barres gonflées dépassent la hauteur de capitale). La largeur de la brève et du
  circonflexe est gardée, seule la hauteur diminue, et les graisses fortes allègent un peu le trait des grappes.
- **Ə ə** : un e retourné, aux proportions des capitales pour Ə. **Digrammes** : les deux lettres avec leur approche
  normale.

## Crénage

Les paires latines du build, refaites avec une lettre vietnamienne à droite (TẠ, Vậ, Yế, Lỳ), mesurées sur leurs
propres glyphes comme dans le build. Paires ajoutées : 1677 (Regular), 1609 (SemiBold), 1567 (Bold), 1490 (Plush).

## Vérifications

- **Bon caractère** : chaque signe vient de la décomposition Unicode de la lettre (grave, aigu, tilde, crochet,
  circonflexe, brève, point souscrit, corne) : correct par construction, tous les 90 caractères du bloc sont couverts.
- **Disposition comparée à Segoe UI et Arial** (polices système de Windows), seulement sur des rendus floutés et une
  planche de revue hors dépôt, rien n'est copié : cette revue a corrigé le grave, d'abord à droite, maintenant à gauche
  du circonflexe comme dans l'usage. Ensuite :
  - nombre de morceaux de chaque lettre identique à la référence pour les 90 lettres du bloc (aucun signe collé) ;
  - zones des signes : 77 sur 90 reconnues en première position parmi les lettres de même base ; les 13 autres sont des
    égalités entre aigu et crochet à la même place, que le flou ne sépare pas.
- **Hauteur de ligne** : point le plus haut 999 (Regular), 1009 (SemiBold), 1021 (Bold), 1045 (Plush) ; point le plus
  bas -210 à -219. Planche `VIET_marks_metrics.png` avec les lignes 950 et 1000.
- **Déterminisme** : deux constructions de suite donnent des fichiers identiques octet pour octet (`--check`).
- **Polices livrées** : les TTF de `SourceArt/Fonts/Build/` ont la même empreinte SHA-256 avant et après.

## Planches

- Tous les glyphes : [Regular](../../../SourceArt/Fonts/Proofs/VIET_grid_PocketRound-Regular.png),
  [SemiBold](../../../SourceArt/Fonts/Proofs/VIET_grid_PocketRound-SemiBold.png),
  [Bold](../../../SourceArt/Fonts/Proofs/VIET_grid_PocketRound-Bold.png),
  [Plush](../../../SourceArt/Fonts/Proofs/VIET_grid_PocketPlush-Black.png).
- Phrases de jeu à 96, 48, 28 et 18 px : [fond clair](../../../SourceArt/Fonts/Proofs/VIET_sentences_light.png),
  [fond sombre](../../../SourceArt/Fonts/Proofs/VIET_sentences_dark.png),
  [titres en Plush](../../../SourceArt/Fonts/Proofs/VIET_titles_plush.png).
- Les quatre styles à côté du latin : [VIET_styles.png](../../../SourceArt/Fonts/Proofs/VIET_styles.png).
- Signes empilés dans la hauteur de ligne : [VIET_marks_metrics.png](../../../SourceArt/Fonts/Proofs/VIET_marks_metrics.png).

Exemples : « Bạn mới! », « Nhận phần thưởng », « Chạm vào quả trứng để nó nở. », « PHẦN THƯỞNG HẰNG NGÀY ».

## Fichiers

- `SourceArt/Fonts/Tools/pw_font_vietnamese.py` : le module (`SCRIPT`, `charset()`, `make()`, `features()`, `kerning()`).
- `SourceArt/Fonts/Tools/pw_font_vietnamese_test.py` : polices de test et planches (aides partagées avec le test
  cyrillique et grec).
- `SourceArt/Fonts/Build/Test/PocketRound-{Regular,SemiBold,Bold}-Viet-Test.ttf` et `PocketPlush-Black-Viet-Test.ttf`
  (environ 275 à 290 Ko, avec leur `.kern.tsv`).

## Limites connues

- Pas de signes combinants (U+0300 à U+036F) : les textes doivent arriver en forme composée (NFC). Certains claviers
  vietnamiens produisent la forme décomposée ; la chaîne de localisation doit normaliser en NFC.

## Correction du latin livré (9 octobre 2026)

Les signes latins qui touchaient leur lettre sont corrigés dans `pw_font_glyphs.py` et les polices livrées sont
reconstruites. Exemples : le tilde de Ã Õ Ĩ (« não », « põe » en portugais), le rond de Å, l'apostrophe de ť, la
barre de ħ et de đ.

- **Règle** : un signe séparé garde la place que lui donnait le dessin. S'il est à moins d'un écart d'encre minimal de
  sa lettre (14 unités en Regular, 15 % du trait au-delà), il monte par pas de 2 unités jusqu'à l'avoir. Ď ľ Ľ
  s'écartent vers la droite, la virgule souscrite descend, la cédille et l'ogonek restent attachés comme prévu.
- **Barres** : la barre de ħ et de đ monte jusqu'à dégager l'arche ou la panse. Dans la Plush seulement, faute de place
  sous l'ascendante, le fût monte un peu au-dessus de la barre pour qu'elle reste une barre qui traverse. La barre de Ł
  monte aussi dans les graisses fortes.
- **Glyphes changés, et seulement eux** (contours comparés glyphe par glyphe) :
  - Regular : Å å ť (3) ;
  - SemiBold : Å Õ å ď Ľ ľ ť (7) ;
  - Bold : Ã Å Õ å Ā ď đ Ē Ğ ħ Ĩ Ī Ľ ľ Ł Ō Ŏ ť ₫ (19) ;
  - Plush : Ã Å È É Ë Õ å ñ Ā Ă Č ď đ Ē ē Ĕ Ě Ğ ħ Ĩ Ī Ĭ Ľ ľ Ł Ō Ŏ Ś Ŝ Š Ť ť Ź Ž ₫ (35).
  - Le ₫ suit le đ, dont il est dessiné.
- **Approche** : seules ď et ľ s'élargissent de 2 à 12 unités. Quelques paires de crénage avec ces lettres bougent de
  2 à 4 unités, les fichiers `.kern.tsv` sont à jour.
- **Contrôles** : le build reste identique octet pour octet d'une construction à l'autre, et aucune lettre accentuée
  latine, cyrillique, grecque ou vietnamienne ne touche plus son signe.
- **Planche avant et après** : [LATIN_marks_fix.png](../../../SourceArt/Fonts/Proofs/LATIN_marks_fix.png).
- Les modules cyrillique, grec et vietnamien posent leurs signes avec exactement la même mesure.
