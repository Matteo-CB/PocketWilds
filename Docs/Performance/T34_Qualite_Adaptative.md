# T34 Qualité adaptative et préparation des shaders

But (Performance Guide, « Qualité adaptative » ; doc 07 « Beauté maximale », sections 2 et 5) :

- le jeu choisit son palier selon l'appareil, puis l'ajuste prudemment en cours de partie ;
- la chaleur fait baisser la qualité avant que la cadence ne chute ;
- le choix du joueur est respecté ;
- le premier affichage d'un objet ne saccade jamais (point ouvert depuis T22 : 54 ms en Magnifique).

Critère de la tâche : aucune image au-dessus de 50 ms au premier affichage d'un objet, et changements de palier testés.

Rapports bruts du benchmark, dans `Baselines/` :

- `T34_player_windows_trace_nowarm.json` : premier run, sans préchauffage, avec enregistrement des états ;
- `T34_player_windows_adaptive.json` : exe final, avec préchauffage ;
- `T34_player_windows_nowarm.json` : exe final, préchauffage coupé (`-pw-no-warmup`).

Ce sont des mesures PC (Ryzen 7 5800X, RTX 5060 Ti, Direct3D 12), pas des budgets mobiles : aucun téléphone de test.

## Qualité adaptative

| Partie | Fichiers | Ce qu'elle fait |
|---|---|---|
| Contrôleur | `Core/Platform/AdaptiveQuality.cs` | Mesure le 95e centile du temps de travail de l'image sur des fenêtres de 10 s (sélection en place, sans allocation). Deux fenêtres de suite au-dessus de 90 % du budget baissent d'un cran. Après 60 s stables (sous 65 % du budget), il remonte d'un cran, jamais au-dessus du palier de l'appareil. Une remontée qui ne tient pas double l'attente suivante, jusqu'à 4 min : pas de va-et-vient. |
| Ordre des crans | `QualityState.StepDown / StepUp` | Vie ambiante, puis détails au sol, puis météo, puis ombres, puis résolution, palier par palier (10 crans de Magnifique à Fluide). La remontée suit l'ordre inverse. Les animaux proches et la lisibilité ne baissent jamais en premier. |
| Chaleur | `Presentation/Platform/ThermalMonitor.cs` | Android : `PowerManager.getCurrentThermalStatus` (API 29+), lu toutes les 5 s. « Sérieux » : un cran de moins tout de suite. « Critique » : Fluide et 30 FPS. « Tiède » : aucune remontée. Après 60 s au frais, le choix du joueur revient. PC : pas d'API, aucune action. |
| Service | `Presentation/Platform/QualityService.cs` | Lit le temps de travail de chaque image : le plus long du thread principal, du thread de rendu et du GPU, via `FrameTimingManager`. Le temps du thread principal ne compte pas l'attente de la cadence. Une valeur aberrante (plus de trois fois l'image réelle) est ignorée. Applique la partie rendu : niveau de qualité, et ombres d'un palier plus bas posées sur le profil URP actif. Les valeurs d'origine sont toujours restaurées, y compris en sortie du mode Play dans l'éditeur. |
| Monde | `VerticalSliceEntry.ApplyQuality`, `LightingProfile.ApplyQuality` | Chaque cran atteint le bon système : vie ambiante (densité), sol (densité, fondu au loin, petits décors secondaires), météo (gouttes, rayons, nuages, brouillard de hauteur, eau), ombres du soleil et ombres rondes des animaux et de l'œuf déjà affichés. Les couleurs et les ambiances ne bougent pas. |
| Message | `RefugeHud.SetNotice`, clé `quality.thermal_notice` | Quand la chaleur allège l'image : « Téléphone chaud : image allégée pour rester fluide ». Pastille sous le compteur de Lumière, avec fondu. |
| Choix du joueur | `QualityService.SetMode`, `LocalDeviceSettingsStore` | Automatique par défaut, ou Fluide, Équilibrée, Magnifique, enregistré sur le téléphone. L'écran Réglages viendra en T56. En mode manuel, seule la chaleur peut baisser l'image, et le message le dit. |
| Cadence | `FrameRateService.SetThermalCap` | 30 FPS tant que l'appareil est critique, puis la cadence choisie revient. |
| Paliers | `ProjectSetup` | Résolution de rendu alignée sur le tableau du doc 07 : 0,8, 0,9 et 1,0. Les ombres d'Équilibrée restent à 24 m (le doc dit 20 m) : décidé en T16, car la caméra est à 13,8 m et 20 m ne couvriraient pas toute la zone de jeu. |

## Préparation des shaders

| Partie | Fichiers | Ce qu'elle fait |
|---|---|---|
| Enregistrement | `ShaderWarmup.BeginBootTrace` (option `-pw-trace-graphics <fichier>`) | Le joueur de benchmark enregistre chaque état graphique dessiné : tous les scénarios, les trois paliers et les 20 crans du scénario Q1. Il y a un fichier par démarrage, car un enregistrement gardé pendant un déchargement de scène fait planter le joueur. |
| Import | `Editor/Build/ShaderWarmupPipeline.cs` (menu Build > Import Graphics State Trace) | Fusionne les fichiers en une collection par plateforme et API graphique (`GSC_WindowsPlayer_Direct3D12`). En tire la liste des variantes de shaders (`SVC_Game`), qui sert d'appoint là où aucun enregistrement n'existe encore (Android, en attendant un téléphone). Relie `DEF_ShaderWarmup` au démarrage. |
| Préchauffage | `ShaderWarmup.RunAsync`, étape « ShaderWarmup » du démarrage | Pendant l'écran de chargement, construit les états graphiques de la collection qui correspond à l'appareil. Ils partent par lots de 16 vers des tâches de fond, que le thread principal se contente de surveiller : l'écran reste animé, même là où un état prend plusieurs millisecondes. Sans collection pour l'appareil, il compile les variantes d'appoint. |

Après tout changement de shader, il faut refaire l'enregistrement. Un enregistrement périmé ne manque que les
variantes modifiées.

## Mesures

Exe Windows de développement, 480 × 1040, synchronisé à 60 images par seconde. Chaque scénario dure 10 s, et 26 s pour Q1.

| Mesure | Premier run, sans préchauffage | Exe final, avec préchauffage | Exe final, sans préchauffage |
|---|---|---|---|
| Préchauffage | aucun | 75 états en 74 ms, image la plus longue 25,6 ms | coupé |
| Image la plus longue, tous scénarios | 47,0 ms (W2), 42,1 ms (Q1) | 17,5 ms | 17,6 ms |
| Images au-dessus de 33 ms | 2 | 0 | 0 |
| Entrée dans un scénario, image la plus longue | non mesurée | 25,7 ms (P0, inclut le préchauffage), 17,5 ms ensuite | 20,9 ms |
| Q1 : 20 crans, image la plus longue après un cran | 16,8 ms | 16,8 ms | 17,3 ms |
| Chevauchements (règle « aucune collision ») | 0 | 0 | 0 |

Le critère de la tâche est tenu : aucune image au-dessus de 50 ms au premier affichage, et les 20 crans de qualité
(10 vers le bas, 10 vers le haut) changent l'image sans à-coup.

Lecture honnête :

- **Gain du préchauffage.** Il n'est mesurable sur ce PC qu'à froid. Le premier run, sans préchauffage, a eu deux
  images longues : 47 ms à la traversée de la bande (W2) et 42 ms pendant les changements de cran (Q1), au moment où
  ces états graphiques apparaissaient pour la première fois. Le pilote NVIDIA garde ensuite sur disque les états qu'il
  a compilés. C'est pourquoi le dernier run sans préchauffage n'a plus de pic : il profite du cache du pilote.
- **Téléphone.** Sur un téléphone fraîchement installé, ce cache est vide, et c'est là que le préchauffage compte.
  Il n'existe encore d'enregistrement que pour Windows et Direct3D 12. Sur Android, en attendant un téléphone, le jeu
  compile les 29 variantes d'appoint de `SVC_Game`, ce qui couvre les variantes mais pas les états complets.
  L'enregistrement Vulkan sera à faire en T66, sur les vrais appareils.
- **Temps de travail lu par la qualité adaptative.** Il suit le thread principal du benchmark, à 0,03 ms près au plus (p95
  entre 1,7 et 2,6 ms selon les scénarios). Le premier run affichait 0,5 à 1,2 ms : la formule retirait une deuxième
  fois l'attente d'affichage. C'est corrigé.
- **Coût de la qualité adaptative.** Il n'est pas visible : les p95 du thread principal restent entre 1,7 et 2,6 ms,
  comme en T33C. Le contrôleur n'alloue rien (test `Tick_DoesNotAllocate`).

## Tests

- EditMode `AdaptiveQualityTests` (15) : fenêtre p95, ordre des crans, ombres jamais au-dessus de la résolution,
  deux fenêtres pour baisser, 60 s pour remonter, attente doublée après une remontée ratée, mode manuel, chaleur
  (Sérieux, Critique, inconnue), aucune allocation, correspondance des états Android, ombres posées puis restaurées
  sur le profil URP, résolution des paliers, préchauffage relié au démarrage.
- PlayMode `AdaptiveQualityPlayTests` (2) : chaque cran atteint le monde dans la vraie scène, en descendant puis en
  remontant ; la chaleur allège l'image et le HUD affiche la phrase traduite.
- Benchmark : scénario Q1 (les 20 crans, image la plus longue après chaque cran) ; temps de travail p95 ; préchauffage
  dans le rapport.
