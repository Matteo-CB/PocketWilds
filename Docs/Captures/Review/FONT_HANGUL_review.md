# Coréen (hangul) dans Pocket Round et Pocket Plush : revue

Agent police FK, 9 octobre 2026. Décision du propriétaire : nos polices pour toutes les langues, sans aucune autre
police, en ajoutant les caractères nécessaires. Tout le hangul est dessiné par notre code Python, au même stylo que
notre latin.

## Ce qui est couvert

- Les 11 172 syllabes précomposées U+AC00 à U+D7A3 : tout texte coréen moderne s'affiche, y compris les noms que le
  joueur tape.
- Les 94 jamo de compatibilité U+3131 à U+318E : 30 consonnes, 21 voyelles, le remplissage U+3164 (vide) et les
  42 lettres anciennes.
- Ponctuation et chiffres : ceux de notre latin, inchangés (le coréen utilise la ponctuation latine). Leur centrage
  vertical avec le hangul est vérifié sur `HANGUL_styles.png` (parenthèses, guillemets, virgule, points de
  suspension, deux-points, chiffres).
- Les quatre styles livrés : Pocket Round Regular, SemiBold, Bold et Pocket Plush Black.

Glyphes par style : 617 pièces de jamo (non encodées), 94 jamo, 11 172 syllabes, soit 11 883 glyphes hangul, plus
le latin de la famille (12 252 glyphes dans les polices de test).

## Méthode

- Les jamo sont des squelettes de traits du moteur `pw_font_engine` : bouts ronds, léger renflement aux extrémités
  comme Câline ; dans Plush, coins intérieurs arrondis et traits gonflés au milieu comme Guimauve, et plus de graisse.
- 19 initiales, 21 voyelles, 27 finales. Chaque jamo est dessiné pour chaque groupe de mise en page : 13 classes de
  voyelles (à droite, dessous, mixte, selon la largeur et le côté du trait court) avec ou sans finale. Les 6 types
  classiques sont les trois familles de voyelles, avec et sans finale.
- Les syllabes sont composées par l'algorithme Unicode : index = (initiale × 21 + voyelle) × 28 + finale. Chaque
  syllabe est un glyphe composite TrueType qui reprend 2 ou 3 pièces déjà en place (décalage nul) : 617 dessins par
  graisse au lieu de 11 172, une police légère et une composition identique dans tout un groupe.
- Épaisseurs : le fût des syllabes simples est celui du latin ; les groupes chargés (finale, voyelle mixte, voyelles
  à deux fûts) amincissent leurs traits, les horizontaux plus que les verticaux, comme les bonnes polices coréennes.
  Un jamo serré s'amincit encore un peu de lui-même, sans jamais fermer un contre-poinçon. Aux angles, un trait épais
  qui rejoint un trait plus fin s'arrête à fleur pour ne pas déborder dans le blanc.
- Même taille optique : les syllabes sans finale (이, 가) sont dessinées un peu plus petites que les syllabes chargées
  (뷁, 햏, 꿿), qui occupent tout le cadre.
- Plush : plus rond (coins intérieurs arrondis), plus gras dans les syllabes simples, traits gonflés, toujours lisible.
- Chasse fixe d'un cadratin (1000), sans crénage pour le hangul.

## Contrôles

Tous les contrôles sont dans `SourceArt/Fonts/Tools/pw_font_hangul_test.py` (`python pw_font_hangul_test.py checks`).

- Tables Unicode : l'ordre des jamo est vérifié contre les noms Unicode à chaque import du module.
- Composition : chaque syllabe reprend exactement les pièces de ses propres jamo, retrouvés indépendamment par la
  décomposition canonique Unicode (NFD) ; toutes les pièces servent.
- Identité des jamo par la structure, comparée à Malgun Gothic (voir plus bas) : pour les consonnes, le nombre de
  contre-poinçons, de morceaux et d'extrémités de traits du squelette ; pour les voyelles, les fûts, les barres, les
  traits qui montent ou descendent d'une barre et les traits courts à gauche, entre deux fûts ou à droite. Les
  21 voyelles ont 21 signatures différentes. Contrôlé sur les 51 jamo modernes, les 42 lettres anciennes et les
  614 pièces de chaque style (sauf ㄵ, que Malgun dessine avec ses deux lettres soudées alors que les nôtres restent
  séparées).
- Syllabes : 300 syllabes tirées au hasard et les 200 syllabes les plus fréquentes, tirées de mots coréens courants
  (verbes, nombres, jours, nourriture, animaux et mots du jeu). Pour chacune, la structure de la syllabe doit être
  exactement la somme de celles de ses pièces : la composition ne soude, ne ferme ni ne casse aucun jamo. Une
  comparaison automatique syllabe entière contre Malgun n'a pas de sens (ses jamo se touchent souvent, les nôtres
  jamais) : les 500 syllabes ont été relues à l'œil, les nôtres au-dessus de celles de Malgun, sur des planches de
  revue côte à côte.
- Rien ne se touche : écart minimal entre l'initiale, la voyelle et la finale de toutes les syllabes (8 034 paires de
  pièces), limite 28 unités.
- Déterminisme : deux constructions de suite donnent des fichiers identiques octet par octet.
- Polices livrées : les TTF de `SourceArt/Fonts/Build/` sont hachés avant et après chaque lancement de nos outils ;
  nos outils n'y écrivent jamais.

### Résultats (9 octobre 2026, dernier lancement)

| Contrôle | Regular | SemiBold | Bold | Plush Black |
|---|---|---|---|---|
| Syllabes mal composées (11 172) | 0 | 0 | 0 | 0 |
| Pièces inutilisées | 0 | 0 | 0 | 0 |
| Jamo modernes : structure différente de Malgun | 0 | 0 | 0 | 0 (+1 extrémités) |
| Lettres anciennes : contre-poinçons différents | 0 | 0 | 0 | 0 |
| Pièces (614) : contre-poinçons, morceaux ou voyelle différents | 0 | 0 | 0 | 0 |
| Pièces : extrémités de traits seulement | 1 | 3 | 11 | 29 |
| Syllabes différentes de la somme de leurs pièces (300 + 200) | 0 | 0 | 0 | 0 |
| Paires de pièces à moins de 28 unités (8 034) | 0 | 0 | 0 | 0 |
| Écart minimal mesuré | 40 | 40 | 40 | 32 |

- Pour Regular, la somme des pièces a aussi été vérifiée sur les 11 172 syllabes : aucune différence de
  contre-poinçons ni de morceaux (2 syllabes, 쩨 et 쪠, avec une extrémité de trait en plus).
- Les différences « extrémités seulement » viennent des doubles consonnes serrées (ㅉ ㅆ ㅃ ㄸ) et de quelques coins
  de ㅁ ㅂ ㅋ dans les cases très chargées de Bold et Plush : un bout de trait se fond dans un autre au squelette ;
  aucune lettre ne change.
- Déterminisme : deux constructions de suite identiques octet par octet. Polices livrées : inchangées par nos outils.
- Relecture à l'œil, nos glyphes au-dessus de ceux de Malgun : les 94 jamo, les 300 syllabes au hasard et les
  200 syllabes fréquentes en Regular ; tous les glyphes sont le bon caractère.
- Taille optique, mesurée à 200 px (largeur et hauteur d'encre en unités, couverture de l'em) en Regular : 이
  700 × 820 (18,8 %), 가 865 × 825 (19,7 %), 각 860 × 870 (22,4 %), 뷁 850 × 870 (30,4 %), 꿿 860 × 875 (28,5 %).

## Usage de Malgun Gothic

Malgun Gothic (police Windows) n'est utilisée qu'en mémoire, dans les contrôles, pour vérifier que chaque glyphe est
le bon caractère : on compare des structures (contre-poinçons, morceaux, extrémités du squelette, disposition des
traits des voyelles), jamais des formes. Aucun contour, squelette ni tracé n'en est copié, stocké ou livré ; les
planches de revue côte à côte sont écrites dans un dossier temporaire hors du projet.

## Taille

- Polices de test, latin compris : Regular 978 Ko, SemiBold 975 Ko, Bold 976 Ko, Plush Black 968 Ko (le latin seul
  fait environ 200 Ko).
- Le hangul ajoute environ 780 Ko par style : 617 pièces dessinées (251 Ko de contours), 94 jamo (46 Ko), 11 172
  composites de 28 octets (310 Ko), plus les index (loca, hmtx) et les noms de glyphes (table post).
- Avec la table post en format 3 (sans noms de glyphes), chaque fichier perd environ 120 Ko (878 Ko pour Regular).
- Sans composites (chaque syllabe en contours complets), un style ferait environ 12 Mo : la construction finale doit
  écrire les syllabes en composites, comme `write_test_font`.

## Planches

- `SourceArt/Fonts/Proofs/HANGUL_styles.png` : les quatre styles avec leur latin, des boutons, le centrage de la
  ponctuation et des chiffres.
- `SourceArt/Fonts/Proofs/HANGUL_layouts.png` : les six types de mise en page et les syllabes chargées, quatre styles.
- `SourceArt/Fonts/Proofs/HANGUL_text_<style>_light.png` et `_dark.png` : phrases du jeu à 96, 48, 28 et 18 px, fond
  clair et fond sombre, avec la traduction dans le latin du même style.
- `SourceArt/Fonts/Proofs/HANGUL_grid_<style>_jamo.png` et `_1.png` à `_5.png` : tous les glyphes (lignes :
  voyelles, colonnes : finales, quatre initiales par page).

Phrases de test écrites pour le jeu : « 새 친구! », « 보상 받기 », « 알이 부화했어요! », « 아기 토끼가 태어났어요 »,
« 새로운 종 발견! 붉은 다람쥐 », « 화면을 눌러 먹이를 주세요. », « 고슴도치가 꽃 옆에서 낮잠을 자요. »,
« 다시 오신 걸 환영해요! », « 알을 3개 부화하세요 · 2/3 », « 초원 꾸러미 · ₩1,990 », « 오늘의 만남 », et la phrase
pangramme « 다람쥐 헌 쳇바퀴에 타고파 » (un écureuil, toutes les consonnes de base).

## Limites connues

- Non couverts, inutiles au texte du jeu : les jamo conjoints U+1100 à U+11FF (et le hangul ancien composé), les
  formes demi-chasse U+FFA0 à U+FFDC, les symboles et la ponctuation pleine chasse CJC (「」, ※, ～).
- Dans Bold et Plush, les syllabes très chargées (뷁, 쀍, 꿿, doubles consonnes devant ㅔ comme 뻬) ont des
  horizontales nettement plus fines que les fûts : c'est voulu, pour garder les blancs ouverts.
  Conséquence visible dans une ligne en Bold et Plush : les syllabes à voyelle dessous et finale (운, 종, 곡) sont
  un peu plus claires que les syllabes simples (가, 다, 자).
- Doubles consonnes très serrées (ㅉ ㅆ ㅃ ㄸ devant ㅐ ㅔ en Bold et Plush) : quelques extrémités de traits se
  rapprochent jusqu'à se confondre dans le squelette (différences « extrémités seulement » des contrôles), sans jamais
  fermer un contre-poinçon ni souder deux lettres.
- ㅎ et ㅊ portent un petit trait vertical en tête, forme courante des polices arrondies ; Malgun dessine celui de ㅎ à
  l'horizontale. Le contrôle de structure ne compte pas cette différence (même nombre de traits).
- À vérifier dans Unity par la session principale : les composites TrueType dans les atlas dynamiques TextMeshPro
  (FreeType les lit comme des contours ordinaires ; une capture de contrôle reste à faire).
- Une relecture par un lecteur coréen natif reste conseillée pour l'espacement et les proportions fines.
