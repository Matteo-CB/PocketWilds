# T34D Vertical Slice Gate

Date : 7 octobre 2026. Nouveau passage du gate de T30, après les tâches correctives T31 à T34C.

Références :

- « 10 — Vertical Slice Definition of Done » et « 12 — VALIDATION, REVIEW & BENCHMARK PROMPTS » ;
- audit automatique : `Docs/Gates/T34D_Audit.md`, produit par l'outil `Pocket Wilds/Gates/Vertical Slice Audit (T34D)` ;
- audit du build release : `Docs/Gates/T34D_Build_Audit.md`.

Critère de T34D : tous les éléments VerticalSlice sont intégrés ou reportés avec une raison écrite ; seule la mesure sur téléphone peut rester en attente.

## Verdict

**La tranche verticale est complète sur PC. Le critère de T34D est tenu.**

- Sur les 155 éléments de la tranche, 139 sont intégrés et 16 sont reportés avec une raison écrite. Aucun n'est en revue ni à produire.
- Ce qui reste ouvert ne se fait pas sans téléphone :
  - la mesure de performance sur appareil, sans laquelle aucun élément de performance ne passe à DONE (règle du doc 12 : ne jamais inventer de mesure) ;
  - la vidéo finale sur appareil.
- Ces deux points sont planifiés en T66.

## Définition du « terminé » : point par point

| Exigence (doc 10 VS DoD) | T30 | T34D | Preuve |
|---|---|---|---|
| Scène : portrait, chunk Prairie, caméra, lumière, UI, données locales | OK | OK | SC_VerticalSlice_Grassland, 8 chunks en streaming, PlayMode 73 réussis |
| Aucun asset TEMP visible | Non | **OK** | Polices maison Pocket Round et Pocket Plush (T31, T32) ; le BuildValidator refuse tout TEMP en release |
| Assets : 3 espèces, œuf, kit Prairie, matériaux, VFX, audio | Partiel | **OK** | AssetValidator 0 erreur, 0 avertissement ; 12 effets de feedback (T34A) ; 49 effets sonores, 4 ambiances et musiques, 9 profils de vibration (T34B, T34B2) |
| Gameplay : éclore, Lumière, œuf, variante, Codex, maîtrise, retour, hors ligne | OK | OK | Tests PlayMode de la boucle, du hors ligne, du Codex, de la maîtrise |
| Sauvegarde versionnée, atomique, reprise, pas de doublon | OK | OK | 8 sauvegardes de référence (v1 à v6) chargées |
| Performance : 60 FPS, 20 animaux, 0 octet par image, pas de gros pic | OK sur PC | OK sur PC | 10 scénarios à 60 FPS, image la plus longue 19,2 ms (T34C) ; **téléphone en attente** |
| Rendu : pas de clone de matériau, LOD, Low beau, pas d'overdraw herbe | OK | OK | 20 matériaux, 0 doublon ; LOD en couleurs ; overdraw 1,5 couche ; références par palier |
| Simulation : pas d'Update par animal, scheduler, agrégats | OK | OK | Seuls deux gestionnaires uniques de la caméra ont un Update |
| UI : HUD une main, fiche animal, Codex, éclosion lisible, listes virtualisées | Partiel | **OK** | Fiche animal et annonce de découverte (T34A) ; Codex 5 000 espèces fluide (T34C) |
| Feedback : éclosion (animation, VFX, vibration, son), tap, collecte, rareté perçue | Partiel | **OK** | T34A (effets, lueur rare, bannière), T34B (sons, vibrations) |
| Données : tout passe par ContentRegistry, palette sans code | OK | OK | Gate 16 : 0 problème ; assistant de contenu : 3 espèces et 2 œufs complets |
| Outils : SpeciesWizard minimal, AssetValidator, Benchmark Runner, pipeline reproductible | Partiel | **OK** | Assistant de contenu (espèces, œufs, identifiants), BuildValidator, captures de référence, benchmarks C1 et V1 |
| QA : sauvegarde et hors ligne, 30+ animaux, spam d'éclosions, pause, sans réseau | OK sauf appareil modeste | OK sauf appareil modeste | Tests, scénarios S30, S50, P5 ; aucun appareil réel |
| Business hooks : analytics, StoreService | OK | OK | T28, T29 |
| Validation finale : vidéo réelle sur appareil | Impossible | **Impossible pour l'instant** | Aucun téléphone de test (T66) |

## Ce que T34D a ajouté

**Assistant de contenu** (`Pocket Wilds/Content Wizard`, fichier `Editor/Production/ContentWizard.cs`). Il ferme ED-001, ED-003 et ED-011, et contrôle sur le disque et dans les données, jamais d'après un statut :

- **Espèces** : source Blender, modèle exporté, prefab, AssetValidator, catalogue visuel, 6 variantes de base, textes en et fr, vignettes et silhouette, au moins un œuf. Il indique l'étape suivante (côté Blender : `pw_species.py`) et lance les étapes Unity (reconstruire une espèce, mettre à jour les définitions, vignettes).
- **Œufs** : source, prefab et validateur, biome, pool, poids de rareté, prix, textes. Boutons pour reconstruire la famille d'œufs et mettre à jour les définitions.
- **Identifiants** : chaque identifiant de contenu avec son hash, une recherche, les identifiants retirés, et « Retirer pour toujours ». Un identifiant encore utilisé est refusé, et un retrait est définitif.
- **Premier manque trouvé** : les deux œufs n'avaient pas de nom traduit. C'est corrigé : « Meadow Egg » / « Œuf de prairie », « Precious Meadow Egg » / « Œuf de prairie précieux ».
- **Tests** : `ContentWizardTests` (4).

**Captures cadrées comme le jeu.** En jeu, la caméra se centre sur l'œuf au démarrage. Les captures partaient de la position enregistrée dans la scène : l'œuf était coupé au bord droit et la moitié des animaux hors champ. `WorldCapture.FrameLikeGame` applique maintenant le cadrage du jeu aux captures du refuge, de l'interface, du feedback, de la boutique, des références et des revues. Les références par palier sont refaites et le test les retrouve.

**Décisions du propriétaire appliquées** (7 octobre) :

- splash Unity retiré ;
- EmojiOne de TextMesh Pro supprimé ;
- un test empêche leur retour.

## Gates du doc 12

| Gate | Résultat |
|---|---|
| 00 Couverture (VerticalSlice) | 155 éléments : 139 intégrés, 16 reportés avec raison écrite, 0 en revue, 0 à produire |
| 01 Sources Blender | 7 fichiers .blend : 3 animaux, rig, famille d'œufs, Prairie de base et Prairie Magnifique |
| 02 à 05 Modèles, LOD, rig, animation | AssetValidator 0 erreur, 0 avertissement. LOD0, LOD1 et LOD2 : lapin 2 110, 792, 382 ; écureuil 2 398, 948, 418 ; hérisson 2 406, 1 094, 443 triangles (budget petit animal : 1,8k à 3k, 0,7k à 1,2k, 0,25k à 0,5k) |
| 06 et 31 Matériaux, doublons | 20 matériaux sur shaders approuvés, aucun doublon |
| 07 Textures | 82 textures, aucune au-dessus de 1 024 px hors atlas ; plafonds par famille contrôlés sur l'import Android (BuildValidator) |
| 08 Prefabs | 61 prefabs, aucun composant interdit ni matériau instancié. Update seulement sur le rig caméra et son entrée tactile : deux gestionnaires uniques, autorisés |
| 09 Chunks | 8 chunks en streaming, jointures continues ; W1 : la plus longue image de transition fait 16,8 ms |
| 10 VFX | 12 effets de feedback poolés ; 16 à la fois au maximum (V1), image la plus longue 17,4 ms |
| 11 et 29 Lumière, beauté du Low | Matin, jour, soir, nuit ; références par palier ; le Low garde silhouettes, palette et lisibilité |
| 12 et 13 UI | HUD, éclosion, fiche animal, chances de l'œuf, toasts, retour hors ligne, Codex et fiche espèce revus en fr et en ; Codex 5 000 espèces à 60 FPS |
| 14 et 15 Audio, haptique | 49 effets sonores, ambiance, thème, couches pluie et soir (T34B, T34B2) ; 9 profils de vibration et module Android. Écoute sur haut-parleur et ressenti des vibrations : T66 |
| 16 Données | 0 problème ; assistant de contenu complet sur les 3 espèces et les 2 œufs ; hashes uniques |
| 17 Addressables | Groupes Bootstrap, CommonAnimals, Biome_Grassland_Kit, Chunks, Audio_Grassland ; 0 doublon |
| 18 Sauvegardes de référence | 8 sauvegardes (v1 à v6) chargées |
| 19 Performance | PC : 10 scénarios à 60 FPS, aucune image au-dessus de 33 ms. **Aucune mesure téléphone** |
| 20 Collection de 50 000 | Test EditMode OK ; interface à 5 000 espèces (C1) fluide |
| 21 Hors ligne 30 jours | Test OK |
| 22 Spam d'éclosions | P5 : 10 éclosions de suite, aucune image au-dessus de 33 ms |
| 30 Traçabilité | 18 modèles sur 18 ont leur source Blender |
| 32 Assets non référencés | 33, tous expliqués (détail ci-dessous) |
| 33 Contenu d'un build release | BuildValidator : 0 erreur, 0 avertissement, 370 assets ; APK release de 45,8 Mo (46,0 Mo en T34C). Le contenu packé passe de 25,9 à 22,9 Mo avec le retrait du splash Unity et d'EmojiOne ; l'APK, déjà compressé, gagne peu. Les 2 avertissements listés par l'audit sont les journaux du pont MCP for Unity, déconnecté pendant le build ; le build lui-même n'en a aucun |

Gate 32, détail des 33 assets non atteints depuis les racines du build (rien n'est supprimé) :

- **Outils éditeur** : les 3 matériaux DBG_LOD.
- **Sources d'export** : les 10 FBX de la Prairie, dont les maillages sont cuits en copies autonomes pour les bundles.
- **Fichiers inclus par d'autres** : les 5 includes de shader ; les 3 atlas de sprites, embarqués par le mécanisme des atlas.
- **Polices** : les sources TTF, la feuille de styles d'édition DEF_TextStyles, et le préréglage « PocketRound-Bold SDF Shadow », qu'aucun texte n'utilise encore.
- **Prefabs devenus inutiles** : les 6 prefabs d'herbe et de fleurs du kit T15, remplacés par la dispersion instanciée de T33. Ce sont des candidats au retrait.

## Performance

Mesures PC (Ryzen 7 5800X, RTX 5060 Ti, Direct3D 12) : `Docs/Performance/T34C_Outils_et_Controle.md`, section 6, et `Baselines/T34C_player_windows_tools.json`.

Le code du jeu n'a pas changé depuis ces mesures. T34D ne touche que des outils éditeur, des textes et deux réglages du player.

| Scénario | Image 95e centile | Plus longue | Thread principal moyen |
|---|---:|---:|---:|
| P2 20 lapins | 16,71 ms | 17,0 ms | 1,72 ms |
| P5 10 éclosions | 16,74 ms | 19,2 ms | 1,98 ms |
| W1 streaming | 16,76 ms | 17,3 ms | 1,72 ms |
| F1 tous les feedbacks | 16,71 ms | 17,1 ms | 1,80 ms |
| C1 Codex 5 000 | 16,75 ms | 17,8 ms | 2,24 ms |
| V1 effets au maximum | 16,72 ms | 17,4 ms | 1,11 ms |

## Beauté

Captures dans `Docs/Captures/Review/` et `TestResults/Captures/T34D_*`.

- **Le refuge au cadrage du jeu.** L'œuf est au centre, les 6 animaux autour. On voit l'herbe, les fleurs, la vasque, la bûche et la clôture, avec un ciel doux.
- **Les trois paliers.** Ils gardent la même composition. Le Low allège l'herbe sans perdre le charme.
- **Les ambiances.** Matin et soir changent vraiment l'atmosphère, avec des ombres longues le soir.
- **La bande de 8 chunks.** Le moulin, le vieil arbre, le petit puits, la mare et le ruisseau sont reliés par un chemin, avec vergers, collines et lisière de forêt.

## Suite

- **T66** (sur téléphone) : mesures de performance, enregistrement des états graphiques Vulkan, réglage de la qualité adaptative, écoute, vibrations et vidéo finale. Sans téléphone de test, il faut soit un Android branché en USB (mode développeur), soit que le propriétaire installe l'APK de développement et lance le benchmark.
- **T35** : prochaine tâche de la file (simulateur d'économie).
