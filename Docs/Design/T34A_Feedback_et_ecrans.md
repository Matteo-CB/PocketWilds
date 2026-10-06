# T34A Feedback et écrans de la tranche

But (file de production, T34A) : les VFX de feedback, la lueur des animaux rares et les écrans qui manquaient au
gate T30 :

- fiche animal ;
- annonce de découverte ;
- écran de démarrage ;
- toasts ;
- fenêtres modales ;
- geste de tutoriel ;
- cadres de rareté, badges de traits, icône favori.

Critères : gates 10 (VFX) et 12 (UI visuelle et UX) du doc 12, et les points UI et Feedback de la Definition of Done.

En cours de tâche, le propriétaire a fixé une nouvelle règle : aucune étincelle (étoile scintillante à quatre branches) dans tout le projet
(`Docs/Design/No_Sparkle_Rule.md`). Il a aussi refusé la main du tutoriel et le cadenas : la main est devenue une
patte, le cadenas a été redessiné.

## VFX de feedback

Tous les effets sont construits par `Editor/Vfx/VfxPipeline.cs`, qui en est la source :

- une seule planche de particules, `T_VFX_FeedbackAtlas` : 8 cases en niveaux de gris (point doux, éclat rond,
  cœur, patte, anneau, étoile ronde à cinq branches, pétale, bulle douce), sans étoile à quatre branches ;
- un seul matériau, `MAT_VFX_AdditiveLite` ;
- la liste et le profil de qualité dans `DEF_VfxCatalog`.

À l'exécution, `FeedbackVfx` met tous les effets en pool à l'entrée de la scène. Aucun ne s'instancie ensuite. Les
quantités suivent le palier de l'appareil : Fluide ×0,5, Magnifique ×1,25, jamais le double. 16 effets au plus en
même temps ; au-delà, l'effet est sauté, sans à-coup.

| Effet | Particules (Équilibrée) | Durée | Quand |
|---|---|---|---|
| EggTap | 8 (anneau + 7 étoiles) | 0,40 s | toucher de l'œuf |
| Crack | 11 | 0,6 s | chaque fêlure |
| HatchCommon | 20 | 1,15 s | chaque éclosion |
| HatchRare | 25, bleu ciel | 1,0 s | rareté Rare |
| HatchEpic | 28, lilas, un anneau | 1,05 s | Épique |
| HatchLegendary | 40, or et ciel | 1,2 s | Légendaire |
| HatchFabulous | 44, rose vers ciel, pétales | 1,6 s | Fabuleux |
| NewSpecies | 18, deux anneaux au sol | 1,5 s | nouvelle espèce |
| NewVariant | 7, un anneau au sol | 1,1 s | nouvelle variante |
| MasteryLevelUp | 14, pattes dorées | 1,0 s | niveau de maîtrise |
| FavoriteHeart | 3 cœurs | 0,69 s | animal ajouté aux favoris |
| RareGlint | 3 bulles de lumière | 1,0 s | animal rare, de temps en temps |

Les couleurs des raretés sont celles de l'interface (jetons de couleur) : l'éclosion dit la même chose que le cadre.
Les petites particules sont 1,5 fois plus grandes que la taille de base du doc 07, pour se lire sur un téléphone à la
distance d'éclosion. Les lueurs et les anneaux sont surélevés pour ne jamais être coupés par le sol. Les captures
sont faites comme sur le téléphone de référence (caméra du refuge, 9,5 m, découpe 1:1 de l'écran 1080 × 2340) :
`TestResults/Captures/T34A_vfx_hatch.png` et `T34A_vfx_feedback.png`.

Collecte de la Lumière, avec écart justifié : des motes d'interface (12, ou 8 en Fluide) volent de la carte de retour
jusqu'au compteur de Lumière, qui fait un petit bond. Un système de particules ne peut pas dessiner dans un canvas
d'interface en superposition.

## Lueur des animaux rares (SH_RareGlow)

- **Bord du pelage** : option 3 du doc 07, presque gratuite. Le niveau passe dans 4 bits de la valeur par rendu que
  `SH_AnimalMaster` lit déjà (`Include/SH_RareGlow.hlsl`) :
  - 1 à 7 : or chaud ;
  - 8 à 15 : dégradé ciel vers rose ;
  - le bord prend la couleur de la lueur puis s'éclaire un peu, ce qui reste visible sur un pelage blanc ;
  - niveaux : Légendaire 3, trait Doré 5, Fabuleux 12. Pas de mot-clé, pas de rendu en plus, pas de lumière.
- **Éclats** : option 1. Un seul animal rare visible à la fois reçoit trois bulles de lumière, toutes les 2,2 s
  (4,4 s en Fluide). Ils sont blancs pour Épique et Brillant, dorés ou couleur ciel selon la lueur.
- Capture : `T34A_rare_glow.png`, avec des pelages blancs et bruns.

## Écrans

| Pièce | Prefab, classe | Ce qu'elle fait |
|---|---|---|
| Fiche animal | `PF_UI_AnimalQuickSheet`, `AnimalSheetScreen` | Deuxième toucher sur le même animal (le premier fait réagir l'animal). Carte en bas d'écran, le refuge reste visible. Portrait dans son cadre de rareté, nom, espèce et variante, rareté (badge, étoiles, mot), traits, lien, cœur favori (sauvegardé, avec des cœurs), Détails ouvre la page de l'espèce dans le Codex. Croix, voile ou retour pour fermer. |
| Annonce de découverte | `RevealBanner` dans le HUD | Après l'éclosion : « Nouvelle espèce ! », « Nouvelle variante ! » ou le nom de la rareté à partir de Rare. Badge, nom, étoiles. Passif, avec son propre canvas. |
| Toasts | `ToastQueue` dans le HUD | Un seul panneau réutilisé, file de 4. Sert à l'ouverture de la boutique et au calme « Pas encore assez de Lumière » (au plus toutes les 4 s). Attend si la note de chaleur occupe la place. |
| Fenêtre modale | `PF_UI_Modal`, `ModalScreen` | Info, Confirmation, Attention. La carte prend la hauteur du texte. Sert aux chances de l'œuf : les 6 raretés et la garantie d'une nouvelle espèce. |
| Bouton des chances | HUD, à côté du bouton d'éclosion | Doc 05 : le bouton info de l'œuf affiche les chances. |
| Geste de tutoriel | `TapHint` dans le HUD, sprite `UI_Gesture_Tap` | Une patte en peluche flotte au-dessus du premier œuf et tape vers lui, avec un anneau qui s'étend. Elle part avec la première éclosion et ne revient plus. |
| Écran de démarrage | `PF_UI_Splash`, `SplashScreen` | Dégradé ciel vers prairie, nom en Pocket Plush (texte vivant, en attendant le nom définitif), trois pattes qui sautillent. Pas de fausse barre de progression. Il part en fondu quand le refuge est prêt. |
| Cadres et badges | `UI_Frame_*`, `ICO_Rarity_*`, `ICO_Trait_*` (`Atlas_Codex`, `DEF_UiRarity`) | Un motif par rareté : point, pousse, losange, trois pointes, couronne, nuage, étoile. Badges de traits : petite patte, grande patte, perle, étoile dorée, œil rose, lune. |
| Réduire les animations | `IMotionSettings` | Le réglage du joueur, sauvegardé mais jusqu'ici transmis à aucun écran, atteint maintenant tous les écrans de la scène. |

Tous les écrans sont préchauffés à l'entrée de la scène (leçon de T28) : la première ouverture ne saccade pas.

## Gate 10 (VFX)

`FeedbackVfxCatalogTests` (EditMode) et `FeedbackVfxPlayTests` (PlayMode) vérifient :

- **Quantités** : chaque effet dans la fourchette du doc 07 en Équilibrée ; Fluide garde environ la moitié, et au
  moins une particule par salve ; Magnifique au plus ×1,35.
- **Matériau** : un seul pour tous les effets.
- **Réglages de base** : salves seulement, pas de boucle, pas de lecture au réveil, pas d'ombres, taille des
  particules plafonnée à un tiers de l'écran.
- **Durées** : tape sur l'œuf sous 0,4 s, cœurs sous 0,7 s, éclosion commune sous 1,2 s.
- **Pool** : chaque effet joué deux fois après le préchauffage, sans instanciation ni allocation, avec un retour
  propre.
- **Changement de palier** : il réécrit les quantités des instances en pool, sans en créer de nouvelles.
- **Pire cas** : les 12 effets en même temps jouent tous ; au-delà, l'effet est sauté.

Le test des 10 éclosions (`HatchSequenceTests`) couvre maintenant toutes les raretés et les deux découvertes, sans
instanciation ni allocation. Une petite allocation au premier accès au `transform` de chaque instance a été trouvée et
déplacée au préchauffage.

## Gate 12 (UI)

- **Trois formes d'écran** : petit téléphone 16:9, téléphone haut 19,5:9, tablette 4:3
  (`T34A_shape_*_hud.png`, `T34A_shape_*_sheet.png`).
- **Textes 30 % plus longs** : chaque nouveau libellé tient à la taille minimale de son style
  (`FeedbackScreenTests.Labels_SurviveThirtyPercentLongerText`).
- **Cibles tactiles** d'au moins 48 dp sur le HUD, la fiche et la fenêtre modale.
- **Rareté jamais par la seule couleur** : motif, étoiles et mot.
- **Réduire les animations** transmis à tous les écrans (test PlayMode).
- **Aucun texte dans les sprites.**
- Le fond de nuit derrière le HUD n'a pas été capturé : la nuit ne sert qu'aux événements, qui n'existent pas encore.

## Mesures

Exe Windows de développement, 480 × 1040 à 60 images par seconde, sur PC (Ryzen 7 5800X, RTX 5060 Ti, Direct3D 12),
pas de téléphone. Rapport brut : `Docs/Performance/Baselines/T34A_player_windows_feedback.json`.

Un nouveau scénario, **F1**, mesure le pire cas du gate 10 : les 12 effets en même temps près de l'œuf, puis chacun
seul, puis l'ouverture de la fiche, des chances et d'un toast, avec les motes de collecte.

| Mesure | T34 (référence précédente) | T34A |
|---|---|---|
| Image la plus longue, tous scénarios | 17,5 ms | 17,8 ms (21 scénarios, F1 compris) |
| Images au-dessus de 33 ms | 0 | 0 |
| F1, pire cas des effets et des écrans | — | 17,2 ms au plus, thread principal p95 2,32 ms |
| P5, 10 éclosions de suite | 17,1 ms | 17,4 ms |
| Thread principal p95 (selon scénario) | 1,7 à 2,6 ms | 1,7 à 2,5 ms |
| Préchauffage des shaders | 75 états en 74 ms | 82 états en 108 ms, image la plus longue 27 ms |
| Allocations, régime établi | 33 à 34 o par image (enregistreur) | identique |
| Batches, P2 / P5 / U1 | 112 / 117 / 68 | 119 / 128 / 72 |
| Chevauchements | 0 | 0 |

Ce qu'il faut savoir :

- **Pic corrigé.** Un premier run (sans préchauffage des écrans) avait une image de 53 ms à la première ouverture de
  la fiche. Les écrans sont désormais construits à l'entrée de la scène : F1 est à 17,2 ms.
- **Pics non reproduits.** Ce même premier run avait aussi un pic isolé de 73 ms dans P2-Mix et de 77 ms dans W2, sur
  le thread principal, sans message ni ramasse-miettes. Ils ne sont apparus dans aucun des deux runs suivants.
- **Entrée dans la scène.** Elle est un peu plus chargée : pools d'effets et préchauffage des écrans, 23 ms au plus
  sur la première entrée. Cela reste derrière l'écran de démarrage.
- **Batches.** Les quelques batches en plus viennent du bouton des chances, de l'anneau de réception de la Lumière et
  des effets pendant les éclosions. Le temps CPU ne bouge pas. L'anneau de réception, transparent mais actif, pourra
  être désactivé hors usage.
- **États graphiques.** Les shaders ont changé (animal, ombre ronde). Les états ont donc été réenregistrés avec tous
  les scénarios, F1 compris : 82 états au lieu de 75.

## Tests

- EditMode : `FeedbackScreenTests` (5), `FeedbackVfxCatalogTests` (1), `NoSparkleRuleTests` (3), plus les tests
  adaptés : `HudTests` (4 cibles tactiles), `UiTypographyTests` (3 nouveaux prefabs).
- PlayMode : `FeedbackPlayTests` (7 : patte, bandeau et anneau, fiche, favori, Codex, chances, toast, motes, réduction
  des animations, écran de démarrage), `FeedbackVfxPlayTests` (16), `HatchSequenceTests` (toutes les raretés et
  découvertes sans allocation).

## Écarts et suites

- **Fiche animal.** Toilettage, photo, tenue et renommer viendront avec ces fonctions. La fiche complète (SCREEN 006)
  n'est pas faite.
- **Surcouche d'éclosion (SCREEN 003).** Le bandeau et les chances sont faits. Le nom du type d'œuf et l'éclosion
  multiple viendront plus tard.
- **Toast.** Il fait partie du HUD au lieu d'être un prefab à part : un seul panneau réutilisé.
- **Cadres Event et Companion.** Ils viendront avec les événements et les compagnons.
- **Gestes glisser et maintenir.** Ils viendront avec les activités, dans la même famille de pattes.
- **Sons.** Aucun son n'est encore produit : c'est T34B.
