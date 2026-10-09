# Polices : kana japonais, ponctuation CJK et pleine chasse (agent FJ)

9 octobre 2026. Décision du propriétaire : nos propres polices pour toutes les langues, sans autre police, en ajoutant
les caractères nécessaires. Cette note couvre le japonais (hiragana, katakana), la ponctuation CJK commune au japonais
et au chinois, et les formes pleine chasse, pour Pocket Round (Regular, SemiBold, Bold) et Pocket Plush (Black).

Tout est dessiné par notre code Python, avec le même stylo que le latin : squelettes de traits, bouts ronds, `swell`
et `inflate` du style. Aucune police, aucun contour ni squelette pris ailleurs, aucun téléchargement.

## Fichiers

| Fichier | Rôle |
|---|---|
| `SourceArt/Fonts/Tools/pw_font_kana.py` | le module : squelettes des kana, ponctuation, pleine chasse, variantes chinoises, règles de coupure de ligne |
| `SourceArt/Fonts/Tools/pw_font_kana_test.py` | polices de test, planches, contrôle de structure |
| `SourceArt/Fonts/Build/Test/PocketRound-<Graisse>-Kana-Test.ttf`, `PocketPlush-Black-Kana-Test.ttf` | polices de test (latin livré + kana) |
| `SourceArt/Fonts/Proofs/KANA_*.png` | planches de revue |

Les polices livrées de `SourceArt/Fonts/Build` ne sont pas touchées : empreintes SHA-256 identiques avant et après.
Le latin des polices de test est identique, glyphe par glyphe, à celui des polices livrées.

## Ce qui est couvert (324 caractères, 336 glyphes par style)

| Bloc | Caractères | Nombre |
|---|---|---|
| Hiragana | U+3041 à U+3096 (petits kana, ゔ, sonorisés), U+3099 et U+309A (marques combinantes, chasse nulle), ゛ ゜ ゝ ゞ | 92 |
| Katakana | U+30A0 à U+30FE (゠, petits kana, ヴ ヵ ヶ, ヷ ヸ ヹ ヺ, ・, ー, ヽ ヾ) | 95 |
| Petits katakana (Aïnou) | U+31F0 à U+31FF | 16 |
| Ponctuation CJK | espace idéographique, 、 。 〃 々 〆 〇, 〈 〉 《 》 「 」 『 』 【 】 〔 〕 〖 〗, 〒, 〜 | 23 |
| Pleine chasse | U+FF01 à U+FF5E (！ à ～, ０ à ９, Ａ à Ｚ, ａ à ｚ), plus ￠ ￡ ￥ ￦ | 98 |
| Variantes chinoises (sans code) | 、 。 ， ． centrés (traditionnel), ！ ： ； ？ à gauche (simplifié), “ ” ‘ ’ pleine chasse | 12 |

Laissés de côté, en le disant :

- ゟ (U+309F) et ヿ (U+30FF) : ligatures pour l'écriture verticale, que le jeu n'utilise pas.
- Le reste du bloc U+3000 à U+303F : marques verticales 〱 à 〵, chiffres de Hangzhou et de Suzhou, marques de ton,
  〄 〓 〠 〶 〼 〽 〾 〿, guillemets 〝 〞 〟 et crochets 〘 〙 〚 〛.
- Étoile à quatre branches : le bloc n'en contient aucune ; ※ (U+203B, hors bloc) et les étoiles des Dingbats ne sont
  pas dessinés. L'astérisque pleine chasse ＊ reprend notre astérisque latin à cinq branches.
- Katakana demi-chasse (U+FF61 à U+FF9F) : hors du périmètre demandé.

## Le dessin

- Chasse fixe d'un cadratin (1000), sans crénage, comme en interface japonaise.
- Cadre idéographique -120 à 880 (centre 380) et œil de 880, les mêmes valeurs que le module Han (`pw_font_han.py`),
  vérifiées dans son code.
- Les kana sont plus petits que les kanji : boîte de dessin de 860 unités, un kana courant tient environ 800 unités, soit
  90 % de l'œil. Les katakana sont un peu plus petits encore (96,5 %).
- Trait des kana : 90 % du trait latin (Pocket Plush : 80 %, avec 55 % de son gonflement, car les traits des kana sont
  longs). La couleur des lignes japonaises rejoint celle du latin.
- Boucles (あ お な は ほ ま よ す む ぬ ね る み ゐ) : elles grandissent avec la graisse, ancrées du côté de la ligne de base,
  pour que leur trou reste ouvert en Bold et en Black.
- Dakuten : deux points ronds. Handakuten : un anneau dont le trou reste net à toutes les graisses. S'ils frôlent la
  lettre, la marque monte un peu, puis le coin de la lettre se replie, puis la lettre rétrécit légèrement vers le bas à
  gauche (au plus 87 %). Aucune marque ne touche sa lettre, et aucune encre ne sort du cadratin.
- Petits kana : 70 % de la lettre, centrés, posés sur la ligne de base des kana.
- Ponctuation à la japonaise : 、 。 ， ． en bas à gauche ; crochets ouvrants dans la moitié droite, fermants dans la
  moitié gauche ; 「 」 et 『 』 à longues branches ; ！ ？ ： ； centrés.
- Pleine chasse : le latin de la même graisse, centré ; W, M et les autres lettres larges sont resserrées sans
  amincir le trait.
- Pocket Plush : le même squelette avec le stylo bulle du titre (gonflement au milieu des traits, bouts ronds).

## Chinois : variante régionale (locl)

Le chinois place certaines marques autrement que le japonais :

| Région | Différence | Glyphes |
|---|---|---|
| Chinois simplifié (ZHS) | ！ ： ； ？ dans la moitié gauche ; guillemets “ ” ‘ ’ pleine chasse | `uniFF01.zhs`, `uniFF1A.zhs`, `uniFF1B.zhs`, `uniFF1F.zhs`, `quote*.full` |
| Chinois traditionnel (ZHT, ZHH, ZHTM) | 、 。 ， ． au centre du cadratin ; mêmes guillemets | `uni3001.zht`, `uni3002.zht`, `uniFF0C.zht`, `uniFF0E.zht`, `quote*.full` |

Le module fournit la fonction `locl` (script `hani`) et, comme TextMeshPro ne lit pas `locl`, une table
`region_cmap(région)` pour fabriquer une police de repli par région, chargée seulement pour la langue active.

## Coupure de ligne pour TextMeshPro

Le module donne les deux listes à mettre dans les réglages de TMP :

- ne commencent jamais une ligne (« Leading Characters ») : `LINE_START_FORBIDDEN`, soit les fermants, 、 。 ， ． ・ ： ；
  ？ ！, les petits kana, ー, ゝ ゞ ヽ ヾ 々, 〜 et ～, et les signes latins `! % ) , . : ; ? ] }` ;
- ne finissent jamais une ligne (« Following Characters ») : `LINE_END_FORBIDDEN`, soit les ouvrants （ ［ ｛ 〔 〈 《 「 『
  【 et les ouvrants latins, plus les signes monétaires placés devant le nombre.

TMP coupe déjà entre deux caractères CJK quelconques (U+2E80 à U+9FFF, U+FF00 à U+FFEF) : les kana y sont.

## Contrôles

### Structure (le bon caractère)

Yu Gothic (police Windows) sert seulement à vérifier que chaque glyphe est le bon caractère : les deux rendus sont
recadrés, fortement flous, puis comparés ; on regarde si notre glyphe ressemble d'abord au bon caractère parmi tout son
groupe. Rien n'est copié, tracé ni gardé : les planches de comparaison restent hors du dépôt, seuls les chiffres sont
notés ici. Les formes ont été corrigées à l'œil quand la comparaison montrait une erreur de structure (ゐ, ゑ, ゆ, め,
や, ネ, シ, 々, 〆, 〃, longueur de 「 」 『 』).

Pocket Round Regular :

| Groupe | Identifié en premier | Note moyenne |
|---|---|---|
| Hiragana de base | 47 / 48 | 0,88 |
| Katakana de base | 48 / 48 | 0,90 |
| Petits hiragana, petits katakana | 12 / 12, 28 / 28 | 0,88, 0,91 |
| Symboles et ponctuation | 24 / 24 | 0,93 |
| Sonorisés : type de marque (deux points ou anneau) | 26 / 26 hiragana, 30 / 30 katakana | |
| Sonorisés : lettre sous la marque (coin masqué) | 24 / 26, 29 / 30 | |

Les écarts restants ont été regardés un par un et sont justes : め sort derrière ぬ (même famille de forme une fois
floue) ; ず, ぽ et グ perdent une partie de la lettre quand on masque le coin. Les graisses plus lourdes donnent des notes
plus basses face à une référence moyenne, avec les mêmes squelettes donc la même structure ; elles ont été revues sur
planche.

### Composition

- Chaque lettre sonorisée vient de la décomposition Unicode (が = か + U+3099), donc la bonne lettre de base.
- Contrôle automatique d'écart entre marque et lettre à chaque graisse : aucun contact.
- Encre dans le cadratin : aucune sortie, sauf ＿, prolongé exprès pour se raccorder.
- Hauteurs : encre entre -229 et 924 en Bold, dans l'interligne de la police (950 et -250).

### Constance

- Construction reproductible : deux constructions de suite donnent des fichiers identiques octet pour octet.
- Polices livrées inchangées (SHA-256 comparés avant et après).

## Tailles

| Police de test | Glyphes | Taille |
|---|---|---|
| PocketRound-Regular-Kana-Test.ttf | 705 (369 latins + 336) | 343 Ko |
| PocketRound-SemiBold-Kana-Test.ttf | 705 | 342 Ko |
| PocketRound-Bold-Kana-Test.ttf | 705 | 346 Ko |
| PocketPlush-Black-Kana-Test.ttf | 705 | 364 Ko |

Les kana ajoutent environ 140 Ko par police.

## Planches

- Tous les glyphes : [Regular](../../../SourceArt/Fonts/Proofs/KANA_grid_PocketRound-Regular.png),
  [SemiBold](../../../SourceArt/Fonts/Proofs/KANA_grid_PocketRound-SemiBold.png),
  [Bold](../../../SourceArt/Fonts/Proofs/KANA_grid_PocketRound-Bold.png),
  [Black](../../../SourceArt/Fonts/Proofs/KANA_grid_PocketPlush-Black.png).
- Phrases du jeu à 96, 48, 28 et 18 px : [fond clair](../../../SourceArt/Fonts/Proofs/KANA_sentences_light.png),
  [fond sombre](../../../SourceArt/Fonts/Proofs/KANA_sentences_dark.png).
- Les 4 styles avec leur latin : [KANA_styles.png](../../../SourceArt/Fonts/Proofs/KANA_styles.png).
- Écran de jeu en japonais : [clair](../../../SourceArt/Fonts/Proofs/KANA_ui_light.png),
  [sombre](../../../SourceArt/Fonts/Proofs/KANA_ui_dark.png).
- Ponctuation régionale : [KANA_regional.png](../../../SourceArt/Fonts/Proofs/KANA_regional.png).

Phrases d'essai : 「あたらしい なかま！」, 「ごほうびを うけとる」, 「タマゴが かえったよ」, 「ショップ・コイン ×500」,
「レベル 12 に アップ！ ★3」, 「「モチ」を ブラッシング しよう。」, 「きょうの おねがい 2/3 、 のこり 5ふん」,
「ハムスターが ねむそうに あくびを したよ。」, 「Pocket Wilds へ ようこそ！ Ｎｅｗ ＃１」.

## Ce qui reste

- Brancher le module dans `pw_font_build.py` (session principale) : appeler `make` et `make_extra`, ajouter
  `LANGUAGESYSTEMS` et `features()` au fichier de fonctionnalités, déclarer les sources dans
  `SourceArt/Handmade_Sources.json`.
- Unity : actifs de repli TMP par langue (japonais, chinois simplifié, chinois traditionnel avec `region_cmap`), et les
  deux listes de coupure dans les réglages TMP.
- Plus tard si besoin : katakana demi-chasse, écriture verticale (`vert`), kana proportionnels (`palt`).
