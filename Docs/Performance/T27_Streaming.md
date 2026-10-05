# T27 Streaming des chunks

Deux chunks prairie et leur transition : `PF_Chunk_Grassland_Start` (épinglé, il porte le sanctuaire) et
`PF_Chunk_Grassland_Meadow`, la prairie fleurie, 14 m à l'est. Ils sont chargés par Addressables et gérés par
`WorldStreamer`. Mesures : player Windows de développement, 480x1040, éditeur au repos. Rapport brut :
`Baselines/T27_player_windows_streaming.json`. Ce sont des chiffres desktop, pas des budgets mobiles.

## Transition sans couture

- Les sols `GL_Ground_Core_A` et `GL_Ground_Core_B` sont générés avec `pw_grassland.build_ground(joins=...)`.
  Le côté joint est carré et plat (pas de relief ni de lèvre près du bord). Les deux chunks partagent les mêmes
  60 sommets de couture (15 en surface, 45 de paroi), à la même hauteur. Le test
  `GroundSeam_StartAndMeadowShareTheSameEdgeVertices` le vérifie.
- Les taches d'herbe claires et sombres sont calculées en coordonnées monde avec une graine commune au biome :
  elles continuent d'un chunk à l'autre. Le chemin de la prairie s'efface avant la couture.
- Décor de couture : un arbre et un gros rocher aux deux bouts, des touffes posées à cheval sur la jonction.
- Captures Unity : `TestResults/Captures/T27_seam_overview.png`, `T27_seam_threequarter.png`,
  `T27_seam_gameplay.png`, `T27_meadow_gameplay.png`.

## WorldStreamer

Classe simple tickée par `VerticalSliceEntry`, sans `Update` propre. Elle mesure la distance entre l'emprise de
la caméra au sol (les 4 coins du frustum projetés sur y = 0) et le carré de chaque chunk.

| Seuil (`DEF_ChunkSet_Grassland`) | Distance | Effet |
|---|---|---|
| Préchargement | 4 m | Chargement async + `InstantiateAsync` sous une racine inactive, palettes liées, qualité appliquée |
| Activation | 1 m | Un seul `SetActive(true)`, avant que le chunk puisse entrer dans le cadre |
| Désactivation | 2 m | `SetActive(false)` |
| Libération | 6 m pendant 4 s | Destroy + `Addressables.Release` (anti va-et-vient) |

Par tick : au plus un démarrage de chargement, une activation et une libération. Marqueurs Profiler :
`WorldStreamer.Tick`, `.Load`, `.Activate`, `.Deactivate`, `.Release`. Le chunk épinglé est chargé derrière
l'écran de chargement (`ISceneEntryPreparation.PrepareAsync`, attendu par le bootstrap avant `Enter`). Un chunk
qui échoue est exclu du monde et des bornes caméra, sans exception ni blocage. Les bornes de déplacement vont de
x = -3,5 à 17,5 m.

## Addressables (N-GRP-BIOME-GRASSLAND)

| Groupe / bundle | Contenu | Taille Windows |
|---|---|---|
| `Biome_Grassland_Kit` | 6 meshes de kit (arbre LOD0-2, rochers), 2 matériaux, 2 shaders | 65 Ko |
| `chunk.grassland.start` | prefab, sol A, 9 meshes fusionnés | 206 Ko |
| `chunk.grassland.meadow` | prefab, sol B, 12 meshes fusionnés | 385 Ko |

Les FBX du kit restent hors bundles : leur prefab de modèle référence le matériau Lit d'URP. Les meshes partagés
sont donc des copies autonomes (`Baked/Kit`). Rapport de build : 0 asset dupliqué. Règles d'analyse
(doublons entre bundles, doublons scène/bundle) : aucun. Les meshes générés sont passés sans Read/Write, donc
sans copie CPU en mémoire. Le contenu est reconstruit à chaque build du player.

## Mesures (W1 : 20 animaux, caméra à 6 m/s, deux allers-retours vers la prairie)

| | P2-Mix3 (sans streaming) | W1 |
|---|---|---|
| Frames | 480 | 1680 |
| CPU frame moy / p99 / max (ms) | 16,67 / 16,84 / 17,08 | 16,67 / 16,89 / 17,32 |
| Thread principal moy / max (ms) | 1,43 / 3,27 | 1,42 / 3,28 |
| Frames au-dessus de 33 ms | 0 | 0 |
| Frames avec allocation / max (octets) | 3 / 9 888 | 13 / 9 184 |

- 10 frames de transition (chargement, prêt, activé, désactivé, libéré, deux fois) : max 16,77 ms, plafonné par
  la vsync. `WorldStreamer.Tick` : 0,61 ms au maximum sur une frame de transition.
- Activations tardives (chunk déjà dans le cadre) : 0. Échecs : 0.
- Mémoire mesh : 1,29 Mo prairie libérée, 2,13 Mo chargée, puis 1,29 Mo après chaque libération (deux cycles).
  Assets chargés : 2 177, puis 2 399, puis 2 177.
- Vérification par nom après la mesure : meshes de la prairie en mémoire 0 / 13 / 0, bundles chargés 3 / 4 / 3.
- Le tick du streamer n'alloue rien en régime établi (`SteadyTicks_DoNotAllocate`). Les quelques allocations
  restent du même ordre que sans streaming : chargements Addressables et sauvegardes sur événement.

## Tests

- EditMode `WorldStreamerTests` (10) : chunk épinglé, préchargement inactif puis activation, délai avant
  libération, retour pendant le délai, budget d'une transition par tick, chunk en échec, absence d'allocation,
  emprise caméra, registre Addressables et couture des sols.
- PlayMode `StreamingPlayTests` : boot réel, chunk de départ prêt avant `Enter`, prairie active avant d'entrer dans
  le cadre puis libérée sans instance restante, deux fois.
- Totaux : EditMode 278/278, PlayMode 32/32 (+1 explicite).

## Reste à faire

- Mesures sur appareil Android (comme T22 à T26).
- Le build Windows bascule l'éditeur sur Windows le temps du build : Addressables lit la plateforme active pour
  `[BuildTarget]`.
