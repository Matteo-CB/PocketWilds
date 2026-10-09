# Polices : thaï (module `thai`)

Décision du propriétaire du 9 octobre 2026 : nos propres polices pour toutes les langues, sans autre police. Pocket
Round (Regular, SemiBold, Bold) et Pocket Plush (Black) reçoivent le thaï, dessiné par notre code Python avec la même
plume que le latin Câline.

## Ce qui est couvert : 114 points de code par style

- **Bloc thaï, 86 caractères** : U+0E01 à U+0E3A et U+0E40 à U+0E5B, soit les 44 consonnes, toutes les voyelles, les
  quatre tons, ์ ํ ๎, les chiffres thaïs ๐ à ๙ et les signes ฯ ๆ ๏ ๚ ๛. Le baht ฿ (U+0E3F) reste le glyphe du jeu
  latin, déjà présent.
- **27 variantes positionnées** dans la zone privée U+F700 à U+F71A, selon la disposition historique des polices thaïes
  de Windows et du Mac : ญ et ฐ sans queue, voyelles hautes et tons décalés à gauche, tons abaissés, voyelles basses
  abaissées. Elles servent au pré-traitement du jeu (ญ ฐ) et de secours si TextMeshPro ne peut pas positionner les
  marques.
- **U+200B**, espace de largeur nulle : glyphe vide, avance 0. Les fichiers de traduction placent ce caractère entre
  les mots ; TextMeshPro coupe la ligne dessus.
- Polices de test : 369 glyphes latins + 114 = 483 glyphes dans chaque style.
- Aucun caractère religieux ajouté au jeu ni aux exemples ; aucune étoile à quatre branches dans le bloc thaï.

## Le choix : thaï bouclé, à têtes rondes

Les deux versions sont sur la même épreuve (`THAI_loops_vs_loopless.png`), mêmes proportions et même graisse.

- Le style bouclé est celui que tous les lecteurs apprennent à l'école : il reste le plus lisible pour les enfants
  comme pour les aînés.
- Les boucles portent l'identité de nombreuses lettres. Sans elles, notre squelette confond ข et บ, ล et ก, ถ et ค,
  ไข่ devient ไป่ : un style sans boucles devrait inventer d'autres détails pour séparer ces paires.
- La tête ronde rejoint les points ronds du i et du j de Câline et les bouts arrondis de toute la famille : c'est le
  même geste doux, très « Pocket Wilds ».
- Les polices sans boucles des jeux visent un ton net et moderne ; notre ton est tendre et rond.

## Comment c'est dessiné

- Même moteur et mêmes aides que le latin (`Glyphs` et le moteur de traits, lus sans modification) : traits à bouts
  ronds, arcs superelliptiques, mêmes épaisseurs par graisse (92, 116, 144, Plush 142), mêmes classes d'approche.
- Hauteur du corps thaï : 1,035 fois la hauteur d'x (549 en Round, 611 en Plush), un peu au-dessus des minuscules
  latines comme dans les bonnes paires thaï-latin. Ascendantes de ป ฝ ฟ ฬ à 1,40 fois le corps, voyelles initiales
  โ ใ ไ à 1,55, descendantes de ฎ ฏ ฤ ฦ ๅ ๆ à 0,38 sous la ligne.
- Une tête fait partie de son trait : la plume fait un tour autour d'un petit cercle et le quitte en tangente, comme à
  la main. La boucle est un peu plus légère que le fût (64 %), avec un passage progressif. Contreforme ouverte en
  Regular et SemiBold ; en Bold et en Plush la contreforme deviendrait un trou d'épingle, elle est donc remplie et la
  tête devient une petite bille, comme le point du i.
- Boucles croisées à la manière thaïe : pied de ม et de ฌ, pied droit de น et ณ, spirale centrale de ด ต ฒ, boucle
  logée dans l'angle de ล, boucle extérieure de ภ ฎ ฏ.
- Marques un peu plus légères que les lettres (76 % du trait, 72 % en Plush), têtes en billes. En Plush les lettres
  gardent leur gonflement et leurs bouts dodus ; les marques gardent les bouts dodus mais pas le gonflement, et leur
  pile est plus compacte.
- Paires travaillées pour rester distinctes à 18 px : ด (boucle haute au centre), ล (petite boucle dans l'angle et petit
  bec en haut à gauche), ถ (ergot intérieur net), ภ (côté en S et boucle extérieure) ; บ et ย (chevron sur le fût) ;
  ข, ช (crochet court vers la droite) et ป (fût droit) ; ผ (pointe à mi-hauteur) et พ (pointe en haut) ; ั (bille à
  gauche et queue), ์ (bille et crochet qui revient), ็ (deux billes et une vague, un petit 8 couché).

## Positionnement des marques

Fonctions OpenType dans `features(style)` :

- **GPOS mark** (ancres par lettre) : voyelles hautes ั ิ ี ึ ื ็ ํ ๎, tons ่ ้ ๊ ๋ ์ et voyelles basses ุ ู ฺ.
  Les ancres sont calées sur le fût droit, comme le veut la typographie thaïe.
- **GPOS mkmk** : un ton posé sur une voyelle haute monte au-dessus d'elle (sur ี et ื il se range à gauche du petit
  trait vertical, pour ne pas empiler trop haut).
- Les quatre ajustements classiques :
  - tons abaissés sans voyelle haute : l'ancre de ton de chaque lettre est basse, mkmk ne remonte le ton que s'il y a
    une voyelle haute ;
  - marques décalées à gauche après ป ฝ ฟ ฬ : ancres propres à ces lettres, à gauche de l'ascendante ;
  - ญ et ฐ sans queue devant ุ ู ฺ : GSUB ccmp contextuel vers les glyphes U+F70F et U+F700 ;
  - ฎ et ฏ avec voyelles basses descendues : ancres basses propres à ces lettres (ญ ฐ gardant leur queue reçoivent
    aussi une ancre basse, filet de sécurité).
- ำ : le moteur HarfBuzz le décompose lui-même ; pour TextMeshPro, c'est le pré-traitement (plus bas). Le glyphe ำ seul
  porte son nikhahit à la place habituelle au-dessus de la consonne précédente.

## Ce que fait TextMeshPro dans Unity 6 (com.unity.ugui 2.0.0, lu dans le PackageCache)

- Import des fonctions de la police (`TMP_FontAsset.ImportFontFeatures`) : paires d'ajustement (kern), MarkToBase,
  MarkToMark et substitutions de ligatures. Rien d'autre : pas de substitution simple, multiple ni contextuelle.
- Application (`TextMeshProUGUI`) : MarkToBase et MarkToMark seulement si `mark` et `mkmk` sont dans les `fontFeatures`
  du composant. Les réglages TMP du projet n'activent que `kern` : il faut ajouter `mark` et `mkmk` pour le thaï.
- `TMP_TextParsingUtilities.IsBaseGlyph` traite U+0E31, U+0E34 à U+0E3A et U+0E47 à U+0E4E comme des marques : nos
  marques sont donc reconnues. Pour une marque, TMP essaie d'abord MarkToMark sur les marques précédentes jusqu'à la
  base, puis MarkToBase sur la base : même résultat que HarfBuzz avec nos ancres.
- U+200B : TMP le reconnaît comme point de coupure ; la police a aussi son glyphe vide d'avance 0.
- Conséquence : le positionnement passe par GPOS ; seuls ำ et ญ ฐ demandent un petit pré-traitement.

## Pré-traitement que le jeu applique aux textes thaïs (référence : `preshape()` du module)

Fonction pure sur la chaîne, avant TextMeshPro. Grappe = une base et les marques qui la suivent.

1. ำ (U+0E33) devient ํ (U+0E4D) + า (U+0E32) ; le ํ rejoint les marques de la grappe précédente :
   C [voyelle basse] [ton] ำ → C [voyelle basse] ํ [ton] า.
2. Dans chaque grappe, ordre des marques : voyelle basse, puis voyelle haute (et ํ, ๎), puis ton ou ์ (ordre stable
   dans un même groupe).
3. ญ (U+0E0D) → U+F70F et ฐ (U+0E10) → U+F700 quand la grappe contient ุ ู ou ฺ.

Mode secours (seulement si `mark` et `mkmk` ne peuvent pas être activés) :

4. après ป ฝ ฟ ฬ : voyelles hautes et ํ → variantes à gauche (U+F701 à U+F704, U+F710 à U+F712) ; tons → bas et à gauche
   (U+F705 à U+F709) sans voyelle haute avant eux, sinon à gauche (U+F713 à U+F717) ;
5. après toute autre base : tons sans voyelle haute avant eux → bas (U+F70A à U+F70E) ;
6. après ฎ ฏ : voyelles basses → abaissées (U+F718 à U+F71A).

Les deux modes donnent le même dessin sur l'épreuve `THAI_marks_*.png` ; la troisième ligne montre le texte brut, à
éviter.

## Contrôles

- **Structure, contre Leelawadee UI** (police système, rendue seulement pour vérifier, jamais copiée ni tracée ni
  enregistrée dans un fichier livré) : chaque glyphe est rendu dans une bande fixe autour de la ligne de base, flouté
  fortement, puis comparé à tous les glyphes de référence. 45 sur 69 sont plus proches de leur propre lettre, 58 sur 69
  dans les trois plus proches. Les écarts relus un par un : paires qui ne diffèrent que par un détail effacé par le flou
  (ข ฃ, ค ฅ, ช ซ, ฎ ฏ, ด ต, ก ถ, ท ห, ล ถ), et écarts de style (nos têtes rondes, nos chiffres). Aucune lettre fausse.
- **Contreformes** : nombre de trous identique à la référence pour 61 glyphes sur 69. Les 8 écarts sont voulus : petites
  boucles devenues billes (ฎ ฏ ฐ, ะ, ๕), boucle fermée de ฯ et ๚, spirale de ๖.
- **Revue côte à côte** : `THAI_check_leelawadee.png` (notre glyphe, puis la référence floutée). Les structures ont été
  vérifiées lettre par lettre sur des rendus flous : côté de chaque boucle, ergots, encoches, ascendantes.
- **Épreuves regardées une à une**, agrandies, à 96, 48, 28 et 18 px, sur fond clair et sombre ; corrections faites
  pour ด ล ถ ภ, บ ย, ข ช ป, ั ็ ์, ก, ธ, ฮ, ผ ฝ, โ ใ ไ.
- **Déterminisme** : chaque police de test construite deux fois, octets identiques (horodatage fixé par
  SOURCE_DATE_EPOCH).
- **Tables** : GDEF classe les 16 marques, GSUB ccmp, GPOS kern + mark + mkmk pour DFLT, latn et thai ; les 42 glyphes de
  largeur nulle (marques, variantes de marques, U+200B) ont bien une avance 0.

## Hauteurs (unités sur 1000)

| Style | Corps thaï | Point le plus haut | Point le plus bas |
|---|---|---|---|
| Round Regular | 549 | 953 | -404 |
| Round SemiBold | 549 | 1004 | -423 |
| Round Bold | 549 | 1063 | -445 |
| Plush Black | 611 | 1083 | -474 |

Le point haut est un ton posé sur une voyelle haute ; le point bas, une voyelle basse sous ฎ ou ฏ. L'ascendante de la
police est 950 : prévoir un interligne d'au moins 1,2 pour le thaï en Bold et en Plush.

## Fichiers

- `SourceArt/Fonts/Tools/pw_font_thai.py` : le module (dessin, `features`, `preshape`).
- `SourceArt/Fonts/Tools/pw_font_thai_test.py` : polices de test, épreuves, contrôles.
- `SourceArt/Fonts/Build/Test/PocketRound-Regular-Thai-Test.ttf` (255 784 octets), `-SemiBold-` (254 660),
  `-Bold-` (257 064), `PocketPlush-Black-Thai-Test.ttf` (270 368).
- Épreuves dans `SourceArt/Fonts/Proofs/` :
  - `THAI_glyphs_Regular.png`, `_SemiBold`, `_Bold`, `_Black` : tous les glyphes ;
  - `THAI_text_<style>_light.png` et `_dark.png` : phrases du jeu à 96, 48, 28 et 18 px avec l'anglais du même style ;
  - `THAI_styles.png` : les quatre styles côte à côte avec le latin ;
  - `THAI_marks_<style>.png` : positionnement des marques (TMP, secours, texte brut) ;
  - `THAI_loops_vs_loopless.png` : le choix du style ;
  - `THAI_line_breaks.png` : coupures sur U+200B ;
  - `THAI_check_leelawadee.png` : contrôle de structure.

## Limites connues

- Les polices livrées dans `SourceArt/Fonts/Build/` ne sont pas touchées par ce module ; l'intégration (crochet dans
  `pw_font_build.py`, atlas dynamiques Unity, activation de `mark` et `mkmk`, pré-traitement en C#) revient à la
  session principale.
- Les chiffres thaïs, ๛ et ๎ sont corrects mais moins travaillés que les lettres : rares dans une interface.
- Les piles hautes de la Plush dépassent l'ascendante (titres seulement).
- Relecture conseillée par une personne qui lit le thaï au quotidien, surtout à 18 px.
