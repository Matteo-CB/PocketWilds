# Polices : idéogrammes Han, kanji et hanzi simplifiés et traditionnels (agent FH)

9 octobre 2026. Décision du propriétaire : nos propres polices pour toutes les langues, sans autre police, en ajoutant
les caractères nécessaires. Cette note couvre les idéogrammes du japonais (kanji, formes shinjitai), du chinois
simplifié et du chinois traditionnel (Taïwan), pour Pocket Round (Regular, SemiBold, Bold) et Pocket Plush (Black).

Tout est dessiné par notre code Python, avec le même stylo que le latin : squelettes de traits, bouts ronds, `swell` et
`inflate` du style. Aucune police, aucun contour, squelette ou mesure pris ailleurs (pas de Make Me a Hanzi ni d'autre
base dérivée d'une police), aucun téléchargement. Les squelettes et les décompositions sont écrits à la main dans des
fichiers texte du dépôt.

Ce premier jalon livre le système complet (traits, composants, moteur de composition, données, vérification, extracteur
des caractères dont le jeu a besoin) et 625 caractères vérifiés.

## Fichiers

| Fichier | Rôle |
|---|---|
| `SourceArt/Fonts/Tools/pw_font_han.py` | le module : lecture des données, moteur de composition IDS, épaisseur selon la densité, contrôle « rien ne se touche », tracé au stylo rond, API pour le build |
| `SourceArt/Fonts/Tools/pw_font_han_components.txt` | composants, partie 1 : radicaux, formes de position (gauche, droite, haut, bas), enveloppes |
| `SourceArt/Fonts/Tools/pw_font_han_components_2.txt` | composants, partie 2 : caractères simples et éléments phonétiques |
| `SourceArt/Fonts/Tools/pw_font_han_components_3.txt` | composants, partie 3 : compositions intermédiaires, caractères complexes, formes régionales d'un même code |
| `SourceArt/Fonts/Tools/pw_font_han_ids.txt` | le jeu de caractères : chaque caractère, ses régions, son nombre de traits et sa décomposition |
| `SourceArt/Fonts/Tools/pw_font_han_verify.py` | vérification contre les polices système (comparaison seulement) |
| `SourceArt/Fonts/Tools/pw_font_han_test.py` | polices de test et planches |
| `SourceArt/Fonts/Tools/pw_font_han_extract.py` | « caractères dont le jeu a besoin » : lit les textes traduits, liste ce qui manque |
| `SourceArt/Fonts/Build/Test/PocketRound-<Graisse>-Han<JP\|SC\|TC>-Test.ttf`, `PocketPlush-Black-Han<JP\|SC\|TC>-Test.ttf` | 12 polices de test : latin du style + Han d'une région |
| `SourceArt/Fonts/Proofs/HAN_*.png` | planches de revue |

Les fichiers partagés (`pw_font_engine.py`, `pw_font_glyphs.py`, `pw_font_build.py`, `pw_font_proof.py`) et le dossier
`SourceArt/Fonts/Build` n'ont pas été modifiés par cet agent. À noter : `pw_font_glyphs.py` et les polices livrées ont
changé à 08:08 et 08:11 pendant ce travail, par un autre agent (correctif latin) ; mes polices de test reprennent ce
latin tel quel, par `build_style`.

## Ce qui est couvert

| Région | Caractères | Polices de test |
|---|---|---|
| JP, japonais (shinjitai) | 458 | `…-HanJP-Test.ttf` |
| SC, chinois simplifié | 477 | `…-HanSC-Test.ttf` |
| TC, chinois traditionnel | 467 | `…-HanTC-Test.ttf` |
| Total, points de code distincts | 625, dont 330 communs aux trois régions | |

Le jeu choisi : les caractères les plus fréquents communs au japonais et au chinois, les mots du jeu (animaux, nature,
couleurs, temps, interface, récompenses, boutique, activités, œuf et éclosion, cache-cache, toilettage, collection) et
leurs formes dans chaque région, par exemple 动 / 動, 奖励 / 獎勵 / 報酬, 继续 / 繼續 / 続, 选择 / 選擇 / 選択,
经验 / 經驗 / 経験, 图鉴 / 圖鑑 / 図鑑, 黑 / 黒, 说 / 說 / 説, 黄 / 黃, 绿 / 綠 / 緑. Rien de religieux, ni dans le jeu de
caractères choisi, ni dans les phrases d'exemple.

Formes régionales d'un même point de code (unification Han), chacune dans la police de sa région : 今, 令, 零, 次,
海 (毎 / 每), 直, 真, 置, 船 (八 / 几), 虎 (儿 / 几), 角, 解, 骨, 房 et 所 (户 avec barre, point ou trait penché), 都 (者
avec point en traditionnel), et tous les caractères à 艹, en quatre traits à Taïwan (花, 草, 茶, 苗, 葉, 菜, 夢, 藍,
藏, 萬). Planche : `HAN_regional_forms.png`.

## Le dessin

- Même stylo que le latin : traits à bouts ronds du moteur, enflure aux extrémités libres (0,6 fois celle du latin,
  aucune enflure aux jointures), trait de base 0,8 fois le trait latin (Regular 74, SemiBold 93, Bold 115, Plush 114
  unités). Pocket Plush ajoute la moitié de son `inflate` au milieu des traits et arrondit les angles.
- Traits : héng, shù, piě (courbe automatique : départ presque vertical, sortie couchée), nà, diǎn, tí, les angles zhé
  (jointure ronde du stylo, ou angle arrondi sur demande), les crochets gōu, les courbes lissées des 弯钩, 卧钩, 竖弯钩.
- Face idéographique 880, centre vertical 380, chasse 1000, sans crénage : mêmes constantes que les kana de FJ
  (`pw_font_kana.py`), l'accord est vérifié dans les phrases japonaises des planches.
- Couleur homogène : chaque caractère mesure l'écart entre ses traits parallèles et s'affine s'il est dense (jusqu'à
  0,62 fois son trait de base) ; le facteur est le même dans toutes les graisses, le gras reste gras.
- Rien ne se touche : une extrémité qui frôle un trait de son composant s'y attache (jointure voulue) ; un point ou un
  trait court trop près d'un voisin se raccourcit ou s'écarte ; ce qui reste trop serré s'affine localement pour garder
  un blanc net (0,3 fois le trait Regular). Cas restants, toutes régions : Regular 9, SemiBold 33, Bold 21,
  Plush 31 paires de traits trop proches sur 1 402 glyphes (surtout la tête de 雨 en Plush, 雪 雲 電 零, et 寶).

## Les données (écrites à la main)

- Composants : 469 dessins en traits sur une grille 0..100, 559 compositions (IDS). Formes de position automatiques :
  `木.L` (木 à gauche, le dernier trait devient un point), `女.B` (女 sous un autre élément, barre haute), `日.T`, `口.T`,
  `田.T` (plus plats en haut), `.O` pour l'enveloppe d'un 辶 ou d'un 走, `.N` pour forcer la forme de base.
- Opérateurs IDS : ⿰ ⿱ ⿲ ⿳ ⿴ ⿵ ⿶ ⿷ ⿸ ⿹ ⿺ ⿻, avec parts réglées (`⿰:0.46`), jonction sans blanc (`⿱:j`, pour
  古 et 早), et boîte explicite (`口@14,10,86,90`) pour 回, 国, 間, 圓, 寶.
- Régions : `NOM@J`, `NOM@S`, `NOM@T` dans les composants, et une ligne par forme dans `pw_font_han_ids.txt`.
- Chaque caractère porte son nombre de traits attendu, comparé au dessin par la vérification.

## Vérification (polices système pour comparer seulement)

Yu Gothic (JP), Microsoft YaHei (SC) et Microsoft JhengHei (TC) sont rendues en mémoire pour comparer, jamais copiées,
tracées ni squelettisées ; rien d'elles n'entre dans nos données ni dans nos polices. Les planches de revue avec ces
rendus restent en local (`TestResults/Fonts/Han`, non versionné).

Mesures par caractère, après normalisation identique des deux dessins (centre et étendue de l'encre) :

- structure : histogrammes d'orientation des contours sur des grilles 4x4, 6x6 et 8x8 (insensible à nos traits ronds et
  plus épais, sensible à la place des traits), plus une corrélation floutée ;
- rang du bon caractère parmi tous ceux de la région ;
- duel : si un autre caractère est plus proche, comparaison sur les seuls pixels qui distinguent les deux (己 / 已,
  土 / 士, 人 / 入, 王 / 玉) ; un caractère est FAUX s'il perd un duel ;
- trous (contreformes fermées), nombre de traits, longueur d'encre.

Calibrage du duel sur 58 paires voisines (己 / 已, 土 / 士, 王 / 玉, 日 / 目, 未 / 本…) : il choisit la bonne forme dans
les deux sens pour toutes les paires sauf celles où notre dessin s'écartait vraiment, que la revue a ensuite corrigées.

Résultat final : **0 caractère faux** dans les trois régions (JP 0 sur 458, SC 0 sur 477, TC 0 sur 467). Structure
moyenne 0,79 (JP), 0,79 (SC), 0,78 (TC). Le bon caractère arrive premier pour 443 JP, 465 SC et 450 TC ; les autres
gagnent tous leurs duels.

Ce que la revue a fait corriger (exemples) : 在 et 存 (le 丿 s'arrête à mi-hauteur, le 丨 descend jusqu'en bas, sinon on
lit 左), 右 (丿 court, 口 grand), 赤 (bas de 亦 : 丿 丨 亅 丶), 黒 japonais sans points dans la boîte et 黑 chinois avec
points, 兎 au sommet plat (sinon 免), 卵, 飞, 冬 (points longs), 文 (diagonales qui partent raides), 女 sous un autre élément
(barre haute : 安, 要), 糸 et 纟 à gauche, 寶, 賣, proportions de 早 草 原 男 間 国 向 回.

Nombres de traits : les conventions où le même dessin se compte autrement (阝 et 廴 dessinés en deux traits, 钅 en
quatre, 佥 en six) sont notées telles que dessinées. Écarts restants, à revoir au prochain lot : 鼠, 藏, 繼, 獎, 數, 實,
綠, 圖, 骨, 購, 藍, 驗, 那, 都, 以, 飲, 隠, 兔.

## Épreuves

- `HAN_JP_sentences_light.png`, `HAN_JP_sentences_dark.png` : phrases du jeu en japonais avec les kana de FJ
  (新しい仲間！, 報酬を受け取る, 卵がかえった！, 毎日ボーナス · 宝箱を開ける, ウサギは花の近くで眠っている。),
  à 96, 48, 28 et 18 px, dans les quatre styles, avec le latin du même style.
- `HAN_SC_sentences_*.png` : 新朋友！ 领取奖励 蛋孵化了！ 每日奖励 · 开始游戏 小兔子在花园里玩。 恭喜！你找到了星星.
- `HAN_TC_sentences_*.png` : 新朋友！ 領取獎勵 蛋孵化了！ 每日獎勵 · 開始遊戲 小兔子在花園裡玩。 恭喜！你找到了星星.
- `HAN_<région>_grid_PocketRound-Regular.png`, `…_grid_PocketPlush-Black.png` : tous les caractères.
- `HAN_<région>_styles.png` : les quatre styles côte à côte avec « Ag ».
- `HAN_regional_forms.png` : JP, SC, TC côte à côte pour les codes partagés dont la forme change.

À 18 px (environ 14 dp), le Regular se lit ; les caractères très denses du traditionnel (寶, 獎, 勵, 戲, 鑑) restent
pâteux en Bold et en Plush aux petites tailles, ce qui est attendu pour des titres.

## Polices de test

12 fichiers, latin du style (369 glyphes) plus les Han de la région : 610 à 680 Ko chacun. Construction déterministe :
deux constructions donnent des octets identiques (vérifié sur Regular JP et Plush TC).

## Plan pour la suite

**Temps mesuré.** Ce jalon (système compris) a pris environ 3 h 15 de travail d'agent pour 625 caractères, dont environ une heure pour
le système lui-même. Avec le
système et la bibliothèque de composants en place, compter 15 à 25 minutes d'agent par tranche de 100 caractères
(données, vérification, corrections), plus environ une minute de calcul (4 styles, 3 régions, vérification).

**Ensembles restants** (en plus des 625 faits) :

- japonais : environ 1 700 jōyō de plus (2 136 au total), puis les 863 jinmeiyō au fil du temps ;
- chinois simplifié : environ 3 000 de plus (3 500 courants : 2 500 常用 + 1 000 次常用) ;
- chinois traditionnel : environ 4 300 de plus (4 808 du 常用國字標準字體表).
  Une grande partie du traditionnel et du japonais partage le même point de code et le même dessin : au total, environ
  6 000 dessins de plus, soit une vingtaine d'heures d'agent.

**Travail en parallèle sans conflit.**

1. Un agent « bibliothèque » passe d'abord : il ajoute dans `pw_font_han_components_lib2.txt` les radicaux et les
   éléments phonétiques fréquents de la liste suivante, pour que les autres n'aient plus qu'à composer.
2. Les agents « caractères » reçoivent chacun une tranche disjointe (par région et par fréquence, environ 250
   caractères, une heure chacun) et écrivent seulement `pw_font_han_ids_<lot>.txt` et, si besoin,
   `pw_font_han_components_<lot>.txt` avec des noms privés préfixés (`{b07_...}`). Le chargeur lit tous les
   `pw_font_han_ids*.txt` et `pw_font_han_components*.txt` : deux définitions du même nom ou du même caractère pour la
   même région arrêtent le build avec le nom des deux fichiers, donc un conflit se voit tout de suite.
3. Chaque agent vérifie sa tranche (`python pw_font_han_verify.py SC 字字字...`) jusqu'à 0 faux, puis un dernier passage
   vérifie tout, refait les planches et mesure les polices.

Découpage proposé : lot A, les caractères communs aux trois régions sans variante ; lot B, le reste des jōyō ; lot C,
les simplifiés seuls ; lot D et E, les traditionnels seuls en deux moitiés par fréquence.

**Les caractères dont le jeu a besoin d'abord.** `pw_font_han_extract.py` lit les `LOC_*.asset` (code de langue ja,
zh-Hans, zh-Hant...) ou n'importe quel fichier texte, compte les caractères Han par région et liste ceux qui manquent,
les plus fréquents d'abord ; avec `--write`, il écrit `han_todo_<RÉGION>.txt` prêt à donner à un lot. Aujourd'hui il
n'y a que les textes anglais et français, donc rien à extraire.

## Tour bibliothèque (9 octobre, après-midi)

Demande de la session principale : une bibliothèque de composants pour que les lots n'aient plus qu'à composer, le
polissage des glyphes faibles, les traditionnels denses lisibles à 28 px en Bold et en Plush, et le plan des lots.

**Bibliothèque.** `pw_font_han_components_lib2.txt` (383 définitions) :

- formes gauches : 车 马 鱼 贝 巾 又 舌 身 角 (régional) 豆 革 缶 耒 片 黑 黒 羊 豸 谷 赤 釆 辛 齿 歯 齒 尢, et 魚 avec
  ses quatre points ;
- formes hautes : 耂 山 止 ⺷ 大 禾 米 卜 髟 吅, les têtes ⺍冖 (JP), 艹冖 (SC), 火火冖 (TC) ;
- formes basses : 廾 舛 氺 ⺗ 㔾 ;
- cadres : 凵 弋 戈 戊 𢦏 尢 几 毛 是 麻 耂, et 户 sans variante pour 启 / 啟 ;
- parties phonétiques, avec leurs formes par région : 亡 亥 衣 夹 / 夾 氏 争 / 爭 良 仓 / 倉 尧 / 堯 / 尭 戋 / 戔 (le
  japonais en trois traits horizontaux) 仑 / 侖 朱 夗 龙 / 龍 / 竜 象 专 / 專 / 専 甫 求 屯 奂 / 奐 卑 冊 / 册 乔 / 喬 寿
  平 兼 曾 / 曽 (un seul point de code partagé, dessiné par région) 坐 旁 吕 / 呂 单 兰 末 卢 / 盧 处 / 処 / 處 韦 / 韋
  义 / 義 會 业 / 業 戠 亚 非 尹 君 余 舍 / 舎 辛 辰 农 / 農 𠂤 帀 帚 危 两 / 兩 / 両 齐 / 斉 乌 / 烏 由 申 央 史 乎 血
  乃 久 具 曹 爰 奚 雚 䜌 蒦 蜀 参 啇 卓 桼 朿 㐬 离 睘 率 収 曷 (japonais, avec 匂) 恵 / 惠 着 (japonais en 12 traits),
  et des compositions courantes (章 竟 意 莫 复 责 / 責 贵 / 貴 宗 官 害 容 度 席 庶 康 唐 屈 居 建 扁 奇 炎 军 ...).

`pw_font_han_ids_lib2.txt` dessine 978 points de code qui vérifient ces parties (1 961 entrées par région), dont les
caractères des brouillons de traduction qui se composent déjà : le jeu compte maintenant 1 601 points de code
(1 023 caractères en japonais, 1 203 en simplifié, 1 137 en traditionnel, 712 communs aux trois). Il reste 89 caractères du jeu à dessiner, en tête des lots
a01, b01, c01 et d01.

**Moteur.**

- Jointures : un bout de trait qui dépasse un trait qu'il croise ou touche (les verticales de 艹 au-dessus de la
  barre, les débords de 毌) n'est plus ramené sur ce trait. Ce défaut aplatissait 艹 dans 幕, 墓, 暮, et 毌 dans 實.
- Amincissement local : dans les zones les plus denses, un trait horizontal ou vertical prend au plus la moitié de
  l'écart avec son voisin parallèle non relié (plancher 0,4 du trait du style).
- Espacement : un caractère dont les traits s'amincissent est remis en page avec des écarts mesurés sur ces traits
  amincis, donc plus de place à l'intérieur de 寶, 實, 鑑 en Bold et en Plush.
- Lots : la variable `PW_HAN_LOTS` charge les fichiers communs et un seul lot, pour que les agents travaillent en
  parallèle sans se gêner.

**Vérification.** Le duel compare maintenant les deux références dans le même repère (centre et étendue de la bonne
référence) : elles ne diffèrent plus que là où les caractères diffèrent. Étalonnage avec une autre police système
(SimSun, pour la vérification seulement) : un glyphe juste mais d'un tout autre dessin perd un duel dans environ 1 %
des cas. `pw_font_han_verify.py ... --tag <lot>` nomme les sorties par lot ; `pw_font_han_extract.py --drafts` lit les
brouillons (`Localization/Drafts/*.json` et `charset_*.txt`).

**Polissage.**

- 寶 : la boîte de la donnée s'appliquait au seul 缶, 王 prenait toute la hauteur ; réécrit (宀, puis 王 缶, puis 貝).
- 藏 : 臧 redessiné (丿, 丬, 臣 en six traits, 戈), forme de Taïwan avec 爿 en quatre traits.
- 實 et 貫 : 毌 en quatre traits avec ses débords ; 綠 : tête de 彔 en trois traits ; 鼠 en treize traits ; 骨 (JP, TC)
  avec le trait qui ferme la boîte ; 冓 (構, 講, 購, 溝) avec sa barre basse ; 兔 sans trait en trop ; 繼 avec la barre
  entre les quatre 幺 ; 獎 et 將 (TC) avec 爿 en quatre traits et ⺼ ; 那 ; 隠 (爫, ⺕, 心) ; 飲 et 飠 japonais en huit
  traits ; 鹿 en onze traits ; 鳥 / 烏, 言, 中, 史, 斥, 示 et 乌 / 鸟 repris pour que les paires proches se distinguent.
- Tête de 雨 (雪, 雲, 電, 零, 霜, 需, 震, 雷) : points plus courts, plus de hauteur ; plus aucun contact en Regular,
  un seul reste en Bold et Plush (需).
- Comptes de traits : les écarts restants sont des habitudes de comptage (阝, 廴, 钅, 佥, 臣, 以, 艹 de Taïwan), notés
  en tête de `pw_font_han_ids.txt`.

**Deuxième passage (pendant les premiers lots).** Les 124 duels perdus ont été revus sur les planches et annotés
dans `han_lists/han_open_duels.txt` : 19 corrigés (鳥, 浅, 奄, 棒, 暗, 豹 et 貌 en simplifié, 喊, 叉, 乌, 卷, 幫, 咸, 恢, 蜜...), 90
justes sur la planche (paires proches, ou radical juste mais placé autrement que la référence), 16 défauts réels
encore à reprendre (le cadre 戊 de 威 et 咸, 豸 à gauche en traditionnel et en japonais, le 戋 japonais de 残 et 践,
斑, 葵) et un point à vérifier (le haut de 韋 dans 緯). 幫 a maintenant un haut 封 et un bas 帛 dessinés pour lui, 奉
est dessiné d'un seul tenant, 灰 aussi. La tête de 雨 et 而 ne se touchent plus dans aucun style (需, 霜, 雷, 零...).
En Bold et en Plush, les caractères de 15 traits et plus s'allègent un peu (jusqu'à 0,78 du trait) : 寶 se lit à
28 px. 龍 : la structure suit la référence, mais nous comptons 14 traits contre 16 à Taïwan (habitude de comptage
du côté droit) ; 數 : un trait d'écart, structure conforme. Seules des modifications de forme et des ajouts ont été
faits dans les fichiers communs, aucun nom retiré ni renommé ; un échantillon de caractères qui utilisent chaque
partie modifiée a été revérifié (aucun nouveau duel perdu).

**Traditionnels denses à 28 px.** Planche `HAN_TC_dense28.png` (寶 獎 勵 戲 鑑 實 藏 數 et la tête de 雨, quatre
styles). 獎, 勵, 戲, 鑑, 實, 藏 et 數 restent lisibles en Bold et en Plush ; 寶 reste le plus serré (vingt traits) :
lisible en Regular et SemiBold, encore lourd en Bold et Plush.

**Plan des lots.** `SourceArt/Fonts/Tools/HAN_BATCHES.md` : règles, fichiers et noms privés (`{<lot>_nom}`), commandes
(`lot`, `parts`, `check`), critères de fin, et les listes figées de 21 lots (A 612, B 565, C 2 037, D 740, E 739,
soit 4 693 caractères). Listes de travail dans `han_lists/` : les 2 136 jōyō (liste officielle), environ 3 900
simplifiés courants, environ 4 700 traditionnels de Taïwan, la table simplifié vers traditionnel, les caractères
réservés au propriétaire (sens religieux, `han_list_held.txt`) et les points de code dessinés différemment selon la
région (`han_list_regional.txt`).

**Vérification finale.** Sur tout le jeu, 3 363 vérifications (1 023 JP, 1 203 SC, 1 137 TC), structure moyenne
0,765 / 0,759 / 0,750. 124 duels perdus (33 JP, 47 SC, 44 TC), soit 3,7 % ; la liste est dans
`han_lists/han_open_duels.txt`. Au premier jalon, avec l'ancien duel et 1 402 vérifications, il n'y en avait aucun :
le duel recalé dans un même repère et le jeu agrandi (plus de rivales proches : 吉 / 古, 史 / 央, 打 / 灯, 枚 / 牧,
据 / 锯) en font apparaître. Une partie sont de vrais défauts de proportion (j'en ai corrigé une trentaine pendant ce
tour) ; les autres sont des paires où nos proportions, plus rondes et plus lourdes, tombent entre deux dessins de
référence alors que la planche montre le bon caractère. Comptes de traits : conformes après corrections, sauf 數 (TC,
un trait de moins à vérifier) et 龍 (dessiné en 14 traits, 16 au dictionnaire de Taïwan : la partie droite est à
revoir).

## Troisième passage et pause (9 octobre, soir)

Les 15 défauts restants de la liste ouverte sont corrigés (rang 1 dans le jeu commun) : 威, 咸 et 喊 (le 女 sous le
cadre 戊, le 口 dégagé du cadre, un 口 haut et étroit à Taïwan), 豸 à gauche (貌, 豹, 貓), le 戋 japonais (残, 践),
斑, 癸 et 葵, 卷, 韋 (緯, 偉, 違, 圍). Tous les caractères qui utilisent une partie modifiée ont été revérifiés avec
tous les lots chargés. Une première consolidation a suivi : un fichier commun `pw_font_han_components_lib3.txt`
(dessins de caractère entier NAME.W pour 寸 欠 舟 皿 故 旦 景 邦 昭, parties promues depuis les lots, 口 à gauche par
région), des corrections de formes communes (乂, 尧, 反, 巷, 离, 歹, 阝, 矢, 犭, 钅, 扌, 山 à gauche...) et deux ajouts
au moteur (dessin de caractère entier, sous-boîte d'un opérateur IDS).

Le propriétaire a mis en pause le travail sur les langues et les polices jusqu'à la fin du jeu. L'état exact (lots
finis, en cours, non commencés, consolidation en attente, nouvelle règle : ne jamais lire de position sur un rendu
de référence) est noté dans `SourceArt/Fonts/Tools/HAN_BATCHES.md`, section « State at the pause ». Les fichiers
communs se chargent seuls et avec chaque lot.

## Limites connues

- Taille : environ 0,95 Ko par idéogramme en polylignes avec contours superposés. Pour 3 500 caractères, compter
  environ 3,5 Mo par style et par région. Pistes : courbes quadratiques pour les bouts ronds et les courbes (deux à
  trois fois plus léger), et ne charger que la police de la langue active.
- Duels perdus restants (`han_lists/han_open_duels.txt`) : surtout des paires proches où nos proportions, plus rondes
  et plus lourdes, tombent entre deux dessins de référence (打 / 灯, 据 / 锯, 枚 / 牧...).
- 寶 à 28 px reste lourd en Bold et en Plush ; 龍 et 數 (TC) à revoir pour le compte de traits.
- Pas d'écriture verticale (pas de `vert`), pas de `locl` : une police par région, comme le fait TextMeshPro.
- La ponctuation CJK et les kana sont dans le module de FJ, pas dans celui-ci.
