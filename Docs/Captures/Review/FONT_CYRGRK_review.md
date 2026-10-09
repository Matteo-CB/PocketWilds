# Polices : cyrillique et grec (module `cyrgrk`)

Décision du propriétaire du 9 octobre 2026 : nos propres polices pour toutes les langues, sans autre police. Pocket
Round (Regular, SemiBold, Bold) et Pocket Plush (Black) reçoivent le cyrillique et le grec, dessinés par notre code
Python avec la même plume que le latin.

## Ce qui est couvert : 172 caractères par style

- **Cyrillique, 99 caractères** : tout le bloc U+0400 à U+045F (russe, ukrainien, biélorusse, serbe, macédonien,
  bulgare), plus Ґ ґ (U+0490, U+0491) et № (U+2116).
- **Grec moderne monotonique, 73 caractères** : Α à Ω, α à ω avec ς, les lettres à tonos (Ά Έ Ή Ί Ό Ύ Ώ ά έ ή ί ό ύ ώ),
  les lettres à dialytika (Ϊ Ϋ ϊ ϋ ΐ ΰ), le point d'interrogation grec ; (U+037E), l'ano teleia · (U+0387), le tonos et
  le dialytika tonos seuls (U+0384, U+0385).
- Police de test : 369 glyphes latins + 172 = 541 glyphes dans chaque style.

## Comment c'est dessiné

- Même moteur et mêmes aides que le latin (`Glyphs` de `pw_font_glyphs.py`, lu sans modification) : fûts à bouts ronds,
  panses superelliptiques, courbes lissées, mêmes épaisseurs par graisse, même hauteur d'x (530, Plush 590), mêmes
  classes d'approche. La Plush garde son gonflement et ses bouts dodus.
- Formes latines reprises seulement là où la lettre est vraiment la même : А В Е К М Н О Р С Т Х Ѕ І Ј, а е о р с у х ѕ і
  ј ; Α Β Ε Ζ Η Ι Κ Μ Ν Ο Ρ Τ Υ Χ, ο, κ, μ.
- Vraies formes cyrilliques : Б б (б avec son drapeau), В minuscule en petite capitale, Г Ґ, Д д avec descendantes
  (fût droit prolongé, pied gauche), Ж ж, З з, И и Й й, к avec un bras qui part à plat et se relève, Л л avec leur pied
  arrondi, м н п т en petites capitales, У à queue douce, Ф ф, Ц Щ Џ à descendante, Ч ч, Ш, Ъ Ы Ь, Э Є, Ю, Я, Ђ Ћ ђ ћ,
  Љ Њ.
- Vraies formes grecques : α à queue, β à deux panses, γ, δ avec son cou penché, ε, ζ, η, θ, ι à petite queue, λ, ν à
  bras courbe, ξ, π, ρ, σ et ς, τ, υ, φ, χ, ψ, ω ; Δ Θ Λ Ξ Σ Ψ Ω. Tonos plus droit et plus court que l'aigu ; sur les
  capitales, il se place avant la lettre, rapproché ligne par ligne de son profil.
- Ajustements de la Plush (hauteur d'x haute) : les ascendantes grecques et celles de ђ ћ montent à 790 au lieu de 740
  pour que β, δ, ζ, θ, ξ et la barre de ђ ћ gardent leurs contreformes ouvertes. Le crochet de Ђ descend sous la ligne
  de base pour ne jamais se confondre avec Ћ.

## Crénage

Même mesure que le build (profils d'encre, 55 % de l'écart à H-H, jamais plus près que 30 % de la référence, à partir
de 14 unités). Les paires restent dans une même écriture, avec la ponctuation et les guillemets « ». Lettres accentuées
mesurées sur leurs propres glyphes (Тё, Γέ). Paires ajoutées : 1548 (Regular), 1508 (SemiBold), 1435 (Bold), 1227
(Plush). Avec le latin et le vietnamien, 5579 paires en Regular : sous la limite de 10000 de la table kern.

## Vérifications

- **Bon caractère** : chaque glyphe est rendu à côté du même caractère en Segoe UI (police système de Windows), puis
  les deux images sont floutées fortement et comparées. Rien n'est copié ni stocké : seule une note de ressemblance
  sort, et les planches de comparaison restent hors du dépôt.
  - Cyrillique : 94 sur 99 en première position, 97 sur 99 dans les trois premières. Les cinq écarts sont des lettres
    voisines à égalité ou presque (Е et Є, К et Н, Ъ et Ь), notre а à une seule panse, et le J latin du build.
  - Grec : 68 sur 73 en première position, 72 sur 73 dans les trois premières. Écarts à égalité (Ά et Α, Β et θ, Κ et
    Η, φ et ψ) et l'ano teleia, un simple point.
  - Topologie (morceaux et trous) : identique partout, sauf β et φ, dont nos formes fermées sont un choix de dessin
    courant.
- **Table de composition** : chaque lettre accentuée (Ё Й Ї Ў Ѓ Ќ Ѐ Ѝ, tonos, dialytika) vérifiée contre la décomposition
  Unicode : aucun écart.
- **Hauteur de ligne** : point le plus haut 922 (Regular) à 974 (Bold), point le plus bas -211 à -218, comme le latin
  existant. Planche `CYRGRK_metrics.png`.
- **Signes jamais collés** : les accents de Ё Й Ї Ў Ѓ Ќ Ѐ Ѝ et des lettres grecques sont posés avec la même mesure d'encre
  que les accents latins corrigés le 9 octobre. Écart minimal : 14 unités en Regular, 15 % du trait au-delà.
- **Déterminisme** : deux constructions de suite donnent des fichiers identiques octet pour octet (`--check`).
- **Polices livrées** : les TTF de `SourceArt/Fonts/Build/` ont la même empreinte SHA-256 avant et après.
- **Règles du projet** : phrases d'exemple sans rien de religieux, aucune étoile à quatre branches, vocabulaire du code
  contrôlé.

## Planches

- Tous les glyphes : [Regular](../../../SourceArt/Fonts/Proofs/CYRGRK_grid_PocketRound-Regular.png),
  [SemiBold](../../../SourceArt/Fonts/Proofs/CYRGRK_grid_PocketRound-SemiBold.png),
  [Bold](../../../SourceArt/Fonts/Proofs/CYRGRK_grid_PocketRound-Bold.png),
  [Plush](../../../SourceArt/Fonts/Proofs/CYRGRK_grid_PocketPlush-Black.png).
- Phrases de jeu à 96, 48, 28 et 18 px : [fond clair](../../../SourceArt/Fonts/Proofs/CYRGRK_sentences_light.png),
  [fond sombre](../../../SourceArt/Fonts/Proofs/CYRGRK_sentences_dark.png),
  [titres en Plush](../../../SourceArt/Fonts/Proofs/CYRGRK_titles_plush.png).
- Les quatre styles à côté du latin : [CYRGRK_styles.png](../../../SourceArt/Fonts/Proofs/CYRGRK_styles.png).
- Hauteur de ligne : [CYRGRK_metrics.png](../../../SourceArt/Fonts/Proofs/CYRGRK_metrics.png).

Exemples : « Новый друг! », « Собрать награду », « Новий друг! », « Зібрати нагороду », « Νέος φίλος! »,
« Πάρε την ανταμοιβή ».

## Fichiers

- `SourceArt/Fonts/Tools/pw_font_cyrillic_greek.py` : le module (`SCRIPT`, `charset()`, `make()`, `features()`,
  `kerning()`).
- `SourceArt/Fonts/Tools/pw_font_cyrillic_greek_test.py` : polices de test et planches.
- `SourceArt/Fonts/Build/Test/PocketRound-{Regular,SemiBold,Bold}-CyrGrk-Test.ttf` et
  `PocketPlush-Black-CyrGrk-Test.ttf` (environ 280 à 300 Ko, avec leur `.kern.tsv`).

## Limites connues

- Pas de formes bulgares (д proche d'un g, л en lambda, и et т proches de u et m) ni de б serbe : option `locl` à
  ajouter plus tard si le bulgare ou le serbe sont visés en priorité. Les formes actuelles sont correctes et lisibles
  dans ces deux langues.
- Pas d'autres lettres cyrilliques (kazakh, ouzbek, tatar, mongol : Ә Ғ Қ Ң Ө Ү Ұ Һ...).
- Pas d'italique (non demandé).
