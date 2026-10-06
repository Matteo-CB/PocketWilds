# T34C Outils et contrôle

But (gate T30, ligne T34C ; doc 11 « BUILD ASSET STRIPPING », « CAPTURE DATA ASSETS » ; doc 12 gates 17, 19, 33 ; famille R et ED-010) :

- un build release ne contient que ce qui doit partir, et le build s'arrête sinon ;
- des vues de contrôle pour les LOD, l'overdraw et les marqueurs des chunks ;
- des captures de référence qui détectent une régression visuelle ;
- des scènes de benchmark pour l'interface à 5 000 espèces et pour les effets ;
- les groupes Addressables communs (fait en partie 1/3, commit ea7b732).

Mesures PC (Ryzen 7 5800X, RTX 5060 Ti, Direct3D 12, fenêtre 480 x 1040) : ce ne sont pas des budgets mobiles, aucun téléphone de test.

## 1. BuildValidator (ED-010, porte 33)

`Editor/Build/BuildValidator.cs` parcourt ce qu'un build embarque réellement : les scènes du build, chaque entrée Addressables et tous les dossiers Resources du projet, en suivant les références d'assets, jamais les scripts.

| Règle | Ce qui est refusé |
|---|---|
| Scènes | En release, autre chose que SC_Bootstrap et SC_VerticalSlice_Grassland ; une scène obligatoire absente |
| Contenu interdit | TEMP, Sandbox, Debug, DBG_, Benchmarks, ArtCapture, Editor, Tests, SourceArt, fichiers de test de performance |
| Shaders | Hors des familles approuvées, ou qui ne compilent pas |
| Textures | Read/Write actif ; au-dessus du plafond de leur famille, mesuré sur l'import Android ; non compressée sur mobile, sauf si un atlas du build la contient |
| Contenu | Erreurs du ContentRegistry (identifiants stables, références) |
| Addressables | Un asset en double entre bundles, ou entre bundles et scènes |
| Assemblies | Bac à sable du magasin et benchmarks non exclus des builds release |
| Prefabs | Scripts manquants |

Il tourne pendant la préparation du build (`BuildPlayerProcessor`), avec la vraie liste de scènes de ce build. Les règles de doublons Addressables construisent des bundles, ce que Unity refuse une fois le build lancé ; le premier essai l'a montré par 3 exceptions. Un build release s'arrête à la moindre erreur. Un build de développement signale seulement, sans les règles de doublons, que les tests et le menu passent déjà.

Correction trouvée en route : les plafonds de texture sont des budgets téléphone. Ils étaient mesurés sur l'import de la cible active : 512 px pendant un build Windows, alors que la limite Android de `T_VFX_FeedbackAtlas` est de 256 px. Ils sont désormais mesurés sur l'import Android, quelle que soit la cible.

Fuite trouvée par l'audit (section 2) : le package de tests de performance, imposé par `com.unity.collections` et sans option pour le couper, écrit avant chaque build `PerformanceTestRunInfo.json` et `PerformanceTestRunSettings.json` dans `Assets/Resources`. Ces fichiers contiennent les détails de la machine de build et du run de tests. Il les efface après le build, mais ils partaient dans l'APK release. Trois corrections :

- le validateur parcourt maintenant les dossiers Resources ;
- ces fichiers sont classés comme contenu interdit ;
- `ReleaseResourcesGuard` passe juste après le package et retire les deux fichiers d'un build release avant que Unity ne collecte les Resources. Le build de développement les garde.

Résultats :

- build release Android : 0 erreur, 0 avertissement, 373 assets contrôlés, les deux fichiers retirés ;
- build Windows de développement : 0 erreur, 6 avertissements attendus (les 3 scènes de benchmark, refusées en release).

Tests : `BuildValidatorTests` (5).

## 2. Build release Android et audit du contenu (porte 33)

Menu `Pocket Wilds/Build/Android Release APK and Audit (gate 33)`. Un build release part d'un cache propre : il est reproductible, et son rapport liste chaque asset packé. `Editor/Build/BuildAudit.cs` écrit l'audit dans `Docs/Gates/T34C_Build_Audit.md`, à partir de deux sources : le rapport du player et le build layout Addressables.

- `PocketWilds.apk` : 46,0 Mo, contre 54,1 Mo pour l'APK de développement. Build propre en 299 s, 0 erreur, 0 avertissement.
- Les premiers builds avaient des avertissements de shader : `SH_BlobShadow` faisait un `pow` sur une valeur possiblement négative. Elle est maintenant bornée à 0, sans changement de rendu, et le build propre final n'a plus aucun avertissement.
- Contenu packé : 25,9 Mo, dont 18,1 Mo dans le player et 7,8 Mo dans les bundles.

| Catégorie | Taille |
|---|---:|
| Textures | 10,26 Mo |
| Audio | 5,50 Mo |
| Polices | 4,58 Mo |
| Shaders | 2,82 Mo |
| Prefabs, meshes, modèles | 2,07 Mo |
| Données | 0,47 Mo |
| Scripts | 0,18 Mo |

Bundles : Audio_Grassland 5,04 Mo (la musique fait 3,1 Mo), CommonAnimals 497 Ko, Bootstrap 212 Ko, Biome_Grassland_Kit 145 Ko, et de 93 à 172 Ko pour chacun des 8 chunks.

Pistes relevées par l'audit :

| Piste | Gain | Statut |
|---|---:|---|
| Splash Unity (logo « Made with Unity ») | 2,7 Mo et environ 2 s au lancement | **Décision du propriétaire.** Le jeu a son propre écran d'accueil depuis T34A ; Unity 6 permet de retirer le splash |
| EmojiOne de TextMesh Pro, dans un dossier Resources donc toujours embarqué | 0,35 Mo | **Décision du propriétaire.** Aucune balise sprite dans le jeu, et la couverture des polices est testée ; la suppression demande son accord |
| Variantes de post-traitement inutilisées (UberPost fait 1,4 Mo) | environ 1 Mo | Passe performance téléphone : le gain ne se vérifie que sur un build, avec un contrôle visuel sur appareil |
| Textures de post-traitement d'URP (SMAA, grain) | environ 0,8 Mo | Idem, avec un PostProcessData propre au jeu |
| Interface du Rendering Debugger (dossier Resources du package SRP Core) | environ 0,3 Mo | Non retirable sans modifier le package |

## 3. Vues de contrôle (R-DBG-LOD, R-DBG-OVERDRAW, R-DBG-SPAWN)

Toutes passent par des profils de capture (section 4). Les images de revue sont dans `Docs/Captures/Review/`.

**LOD.** `DBG_LOD_Red`, `DBG_LOD_Green` et `DBG_LOD_Blue` (URP Unlit, dans `Art/Debug`) colorent LOD0, LOD1 et LOD2 de chaque LODGroup, vus depuis le zoom le plus éloigné (`Capture_LOD_Refuge`). Les matériaux d'origine sont remis après la prise de vue. Résultat au zoom maximal : animaux, rochers et clôture en LOD0 ; arbres en LOD1 ; aucun LOD2 visible à cette distance.

**Overdraw.** Le mode overdraw du Rendering Debugger d'URP ne pouvait pas montrer le jeu, pour deux raisons :

- nos shaders n'ont pas le mot-clé `DEBUG_DISPLAY`, donc ils étaient rendus normalement, en simple additif ;
- la passe qui convertit en couleurs de chaleur ne tourne qu'à l'écran.

La capture compte donc elle-même les couches :

- chaque renderer, l'herbe et la petite faune instanciées, et chaque particule sont dessinés avec un matériau additif de comptage, dans une cible flottante ;
- opaque : test et écriture de profondeur, faces arrière masquées, comme nos shaders opaques ;
- transparent : pas d'écriture de profondeur, deux faces, comme nos effets ;
- chaque pixel est ensuite coloré selon son nombre de couches, avec une légende en bas : 1 bleu foncé, 4 vert, 6 orange, 8 rouge, 10 et plus rose pâle ;
- le ciel n'est pas compté : une couche, là où rien d'opaque ne le cache.

`ScatterRenderer` et `AmbientLife` ont reçu un `OverrideMaterial`, réservé aux captures et nul dans le jeu.

| Vue | Couches moyennes (pixels couverts) | 4 et plus | 6 et plus | 8 et plus | Max |
|---|---:|---:|---:|---:|---:|
| Refuge (High, 6 animaux) | 1,49 | 1,9 % | 0,1 % | 0,0 % | 10 |
| Tous les effets de feedback à la fois sur l'œuf (pire cas jamais joué) | 1,66 | 4,8 % | 2,1 % | 1,2 % | 15 |

Le sol fait une couche et l'herbe deux à trois ; la ligne « pas d'overdraw herbe » du gate T30 est tenue. Le test `Overdraw_StaysLow_InTheRefugeAndTheWorstHatch` garde ces niveaux :

- refuge : moins de 2 couches en moyenne, et moins de 1 % de pixels à 6 couches ou plus ;
- éclosion : moins de 3 % de pixels à 8 couches ou plus.

**Marqueurs des chunks.** `Editor/World/ChunkMarkerGizmos.cs` dessine dans la vue Scene, pour chaque `WorldChunk` :

- l'apparition de l'œuf et celles des animaux ;
- les points d'intérêt et les points de repas ;
- la zone de promenade et les volumes d'obstacles.

Ce sont des métadonnées d'édition, jamais un objet du jeu.

## 4. Profils et captures de référence (M-DEF-CAPTURE, R-GOLDEN-VISUAL)

`CaptureProfile` est un ScriptableObject : prise de vue, taille, suréchantillonnage, champ de vision, marge, angles de cadrage, fond, transparence, palier, animaux, vue de contrôle, MSAA, nom de sortie. Aucun script d'édition n'a plus de résolution ni de cadrage en dur. 12 profils, dans `Editor/Capture/Profiles` (jamais dans un build) :

- icônes : animal, œuf, décor (256 px, rendus trois fois plus grands puis réduits, détourés) ; `AnimalThumbnailPipeline` lit `Capture_AnimalIcon` ;
- boutique et partage : portrait 1242 x 2688, carré 1080, story 1080 x 1920, en palier High ;
- références : refuge en Low, Medium et High (540 x 1170, 6 animaux) ;
- revues : LOD, overdraw du refuge, overdraw de l'éclosion.

**Captures de référence.** `Docs/Captures/Golden/Capture_Golden_Refuge_{Low,Medium,High}.png`. L'horloge de l'éditeur ne s'arrête jamais : le vent est figé pendant la prise de vue, sinon deux rendus ne se ressemblent pas. `GoldenVisualTests` refait les trois rendus et les compare :

| | Écart moyen (0-255) | Pixels changés (plus de 24) |
|---|---:|---:|
| Même rendu, refait | 0,05 à 0,07 | 0,10 à 0,15 % |
| Seuils du test | < 1,0 | < 0,5 % |
| Medium comparé à High | 3,26 | 5,25 % |
| Low comparé à Medium | 8,72 | 8,16 % |

Un vrai changement fait donc échouer le test, sans fausse alerte. Les références sont propres à cette machine : après un changement de carte graphique ou de pilote, il faut les refaire avec `Pocket Wilds/Capture/Update Golden References`.

## 5. Scènes de benchmark (L-SC-007, L-SC-008, R-BENCH-5K)

**SC_Benchmark_UI, scénario C1.** `BenchmarkUiEntry` génère au démarrage 5 000 espèces sur 6 biomes et une collection de 5 001 animaux, sans texture par entrée. La scène utilise les vrais `PF_UI_Codex` et `PF_UI_HUD_Main`. Déroulé : ouverture, 30 s de défilement très rapide (toute la liste en 2 s, donc toutes les rangées changent à chaque image), puis 20 cycles de fermeture et réouverture, pendant que le compteur de Lumière du HUD change à chaque image.

Le premier passage a montré une interface à 3,1 ms par image sur PC, trop lourde pour un téléphone. Deux causes, deux corrections :

- **Recyclage des rangées.** Une rangée sortie était désactivée puis réactivée dans la même image, ce qui désinscrit et réinscrit chaque élément graphique et reconstruit chaque texte. Les rangées sorties restent maintenant actives et sont réutilisées dans la même passe ; seules celles que personne ne reprend sont masquées. Le compteur d'une carte ne bascule que s'il change.
- **Sous-canvas.** La liste a son propre canvas, avec son raycaster. Le défilement ne reconstruit plus l'en-tête, et une rangée qui change ne reconstruit que la liste. Le prefab est régénéré par `UiPipeline` : identique, plus ces deux composants, et le rendu est le même.

| C1 | Avant | Après |
|---|---:|---:|
| Interface, moyenne | 3,09 ms | **1,21 ms** |
| Interface, 95e centile | 5,96 ms | 4,59 ms |
| Thread principal, moyenne | 4,51 ms | **2,24 ms** |
| Image la plus longue | 17,5 ms | 17,8 ms |
| Première ouverture | 7,7 ms | 8,4 ms |
| Réouverture, maximum | 1,7 ms | 1,5 ms |
| Vues de rangée créées, pour 1 674 rangées | 9 | 9 |

Allocations :

- la première ouverture alloue 1,1 Mo une seule fois (les listes des 5 000 espèces) ;
- chaque réouverture alloue environ 2,6 Ko : 20 images avec allocation de plus qu'avant, une par réouverture, probablement à la réactivation du sous-canvas ;
- le défilement n'alloue rien (95e et 99e centiles à 0).

**SC_Benchmark_VFX, scénario V1.** Scène créée par `BenchmarkScenesPipeline` : cadrage du refuge, fond de prairie. `BenchmarkVfxEntry` charge la présentation des éclosions par Addressables et garde le pool d'effets à son maximum, le palier changeant toutes les 5 s. Résultat :

- 16 effets à la fois au pic (le plafond du catalogue), 367 lancés, 6 changements de palier ;
- image la plus longue 17,4 ms ; 12 petites allocations en 30 s, 2 Ko au total.

Les deux scènes ne partent qu'en build de développement : le BuildValidator les refuse en release.

## 6. Performance (player Windows de développement)

Rapports : `Baselines/T34C_player_windows_first.json` (avant les corrections du Codex) et `Baselines/T34C_player_windows_tools.json` (final). Cadence limitée à 60 images par seconde.

| Scénario | Palier | Image 95e centile | Plus longue | Thread principal moyen / max | GPU | Interface | Chevauchements |
|---|---|---:|---:|---:|---:|---:|---:|
| P0 Refuge vide | Medium | 16,69 ms | 17,0 ms | 1,36 / 2,6 ms | 0,33 ms | 0,08 ms | 0 |
| P2 20 lapins | Medium | 16,71 ms | 17,0 ms | 1,72 / 3,5 ms | 1,21 ms | 0,06 ms | 0 |
| P5 10 éclosions de suite | Medium | 16,74 ms | 19,2 ms | 1,98 / 10,1 ms | 1,09 ms | 0,08 ms | 0 |
| W1 Streaming des chunks | Medium | 16,76 ms | 17,3 ms | 1,72 / 3,0 ms | 0,54 ms | 0,06 ms | 0 |
| U1 Boutique | Medium | 16,76 ms | 17,2 ms | 1,47 / 8,8 ms | 0,38 ms | 0,12 ms | 0 |
| R1 Pluie | Medium | 16,70 ms | 17,1 ms | 1,74 / 2,7 ms | 0,63 ms | 0,06 ms | 0 |
| F1 Tous les feedbacks | High | 16,71 ms | 17,1 ms | 1,80 / 5,2 ms | 0,73 ms | 0,07 ms | 0 |
| A1 Tempête audio | High | 16,71 ms | 17,4 ms | 1,82 / 3,4 ms | 0,76 ms | 0,06 ms | 0 |
| C1 Codex 5 000 | Medium | 16,75 ms | 17,8 ms | 2,24 / 14,0 ms | 0,09 ms | 1,21 ms | 0 |
| V1 Effets au maximum | High | 16,72 ms | 17,4 ms | 1,11 / 2,4 ms | 0,14 ms | 0,04 ms | 0 |

Aucune image au-dessus de 33 ms et aucun chevauchement. La marge minimale des animaux reste à -0,2 cm, comme dans les mesures précédentes (T34, T34B, T34B2). Préchauffage des shaders : 82 états en 93 ms.

## 7. Tests

- EditMode 413/413, PlayMode 73 réussis et 1 ignoré (test explicite).
- Nouveaux tests :
  - `BuildValidatorTests` (5) ;
  - `GoldenVisualTests` (6) : profils, matériaux de debug jamais embarqués, overdraw, les trois références ;
  - `CommonContentTests` et `CommonContentPlayTests`, en partie 1/3.
- Les tests des règles du propriétaire (vocabulaire, aucune collision, aucune étincelle) passent.

## 8. Reportés avec raison

| Élément | Raison |
|---|---|
| L-SC-004, L-SC-005 (scènes de capture animaux et décors) | Les captures se font par code avec les profils : `AnimalThumbnailPipeline` cadre chaque animal dans une scène temporaire, `ReferenceCapture` et `WorldCapture` cadrent le refuge dans la vraie scène. À revoir avec la passe d'art boutique et marketing |
| L-SC-009 (bac à sable d'art) | Pas utile pour l'instant ; le BuildValidator refuse déjà tout contenu `/Sandbox/` en release |
| K-PF-SELECTION (proxy de sélection) | Le tap sélectionne par un test rayon-sphère par animal et par œuf, sans collider ni physique, comme le veut la règle « aucune collision ». Un proxy servira aux décorations du joueur |
| M-DEF-DENSITY (densité par biome) | La densité suit le palier (`DEF_WorldQuality`) ; un profil par biome servira avec le deuxième biome (Forêt) |
| N-GRP-UI_Common, N-GRP-Audio_Common | Voir partie 1/3 : aucun bundle ne dépend de ces assets, et ils servent dès la première image |

## 9. Décisions du propriétaire (7 octobre)

Les deux décisions ouvertes par l'audit sont prises et appliquées :

1. Le splash Unity est retiré (`PlayerSettings.SplashScreen.show` à faux) : 2,7 Mo et environ 2 s de lancement en moins. Le jeu s'ouvre sur son propre écran d'accueil.
2. EmojiOne de TextMesh Pro est supprimé (planche, données, attribution, dossiers vides), et la référence par défaut des réglages TMP est vidée : 0,35 Mo.

Le test `BuildValidatorTests.OwnerDecisions_NoUnitySplash_NoTmpEmoji` empêche leur retour. L'audit de la section 2 date d'avant ces deux retraits.
