# T30 Vertical Slice Gate

Date : 5 octobre 2026. Référence : « 10 — Vertical Slice Definition of Done » et « 12 — VALIDATION, REVIEW & BENCHMARK
PROMPTS ». Audit automatique détaillé : `Docs/Gates/T30_Audit.md` (outil `Pocket Wilds/Gates/Vertical Slice Audit (T30)`).

## Verdict

**La tranche verticale n'est pas finale.** Le cœur est solide et validé sur PC : architecture, boucle complète, sauvegarde,
simulation, données, performance desktop, 3 espèces, streaming, boutique, analytics. Il reste des manques de contenu
(audio, VFX de feedback, quelques écrans), le niveau de beauté demandé le 5 octobre, les polices maison et surtout la
validation sur un vrai téléphone, impossible sans appareil. La règle du doc 12 est claire : tant qu'un élément requis est
en revue, la tranche n'est pas finale. Les manques sont planifiés (section « Suite »).

## Définition du « terminé » : point par point

| Exigence (doc 10 VS DoD) | État | Preuve |
|---|---|---|
| Scène : démarre en portrait, chunk Prairie, caméra, lumière, UI, données locales | OK | SC_VerticalSlice_Grassland, PlayMode 35/35 |
| Aucun asset TEMP visible | **Non** | Police LiberationSans TEMP visible (T31, T32) |
| Assets : lapin complet, écureuil, hérisson, œuf, kit prairie, matériaux, VFX hatch, hooks audio | OK partiel | 3 espèces validées (AssetValidator : aucun avertissement), hooks audio présents mais aucun son produit |
| Gameplay : lancer → éclore → Lumière → œuf → variante → Codex/maîtrise → fermer → revenir → hors ligne | OK | Tests PlayMode SanctuaryLoop, Offline, Codex, Mastery |
| Sauvegarde versionnée, atomique, recovery, pas de doublon d'éclosion | OK | SaveTests (golden saves v1 à v6), schéma 6 |
| Performance : 60 FPS, 20 animaux, 0 B/frame, pas de gros pic | OK sur PC, **non mesuré sur téléphone** | Benchmark Windows ci-dessous ; tests 0 allocation |
| Rendu : pas de clone de matériau, LOD, Low beau, pas d'overdraw herbe | OK | Gate 06 : 7 matériaux, 0 doublon ; Gate 29 : Low garde l'identité |
| Simulation : pas d'Update par animal, scheduler A/B, agrégats, tests | OK | Gate 08 : seul le rig caméra a un Update (gestionnaire unique, autorisé) |
| UI : HUD une main, fiche animal, Codex, hatch lisibles, listes virtualisées | **Partiel** | HUD et Codex OK ; **fiche animal absente** ; pas d'annonce « Nouvelle espèce » |
| Feedback : hatch (animation, VFX, haptique, audio), tap animal, collecte, rareté perçue | **Partiel** | Animation, VFX commun/rare, réaction au tap OK ; aucun son, aucun profil haptique, VFX de collecte et de nouveauté absents |
| Données : tout passe par ContentRegistry, palette sans code | OK | Gate 16 : 0 problème |
| Outils : SpeciesWizard minimal, AssetValidator, Benchmark Runner, pipeline reproductible | OK partiel | AssetValidator et Benchmark Runner OK ; pipeline espèce par menus et scripts (pas de fenêtre d'assistant) ; Gate 30 : 12/12 modèles ont leur source |
| QA : save/offline, 30+ animaux, spam hatch, pause/reprise, appareil modeste, sans réseau | OK sauf appareil modeste | Tests + scénarios S30, S50, P5 ; aucun appareil réel |
| Business hooks : analytics, StoreService | OK | T28, T29 |
| Validation finale : vidéo réelle sur appareil qui donne envie de jouer | **Impossible pour l'instant** | Aucun téléphone de test |

## Gates du doc 12

| Gate | Résultat |
|---|---|
| 00 Couverture (VerticalSlice) | 151 éléments : 0 DONE, 64 intégrés, 13 en revue, 65 à produire, 9 reportés avec raison écrite. Statuts remis en accord avec le disque, chaque correction avec sa preuve dans les notes ; 3 éléments partagés entre biomes déplacés en SoftLaunch |
| 01 Sources Blender | 6 fichiers .blend présents, export reproductible (outils pw_*.py) |
| 02 à 05 Modèles, LOD, rig, animation | Validés en T06 à T10, T23, T24 ; AssetValidator : budgets respectés (lapin LOD0 2 110 triangles, hérisson 2 406) |
| 06 et 31 Matériaux, doublons | 7 matériaux, tous sur shaders approuvés, aucun doublon |
| 07 Textures | 45 textures, aucune au-dessus de 1 024 px hors atlas |
| 08 Prefabs | 26 prefabs, aucun composant interdit, aucun matériau instancié ; Update seulement sur le rig caméra (gestionnaire unique) |
| 09 Chunks | Validé en T27 (jointures, streaming, mémoire rendue) |
| 10 VFX | 3 VFX produits, poolés ; 8 VFX de feedback manquants |
| 11 et 29 Lumière, beauté du Low | Low garde silhouettes, palette, lisibilité, composition |
| 12 et 13 UI | HUD et Codex propres ; police TEMP ; fiche animal manquante ; Codex 50 000 entrées testé en logique, scénario UI 5 000 à faire |
| 14 et 15 Audio, haptique | **Aucun son, aucun profil haptique** |
| 16 Données | 0 problème |
| 17 Addressables | Aucun doublon entre bundles (T27) |
| 18 Sauvegardes de référence | v1 à v6 chargées |
| 19 Performance | Desktop seulement (ci-dessous) ; **aucune mesure téléphone** |
| 20 Collection de 50 000 | Test EditMode OK |
| 21 Hors ligne 30 jours | Test OK |
| 22 Spam d'éclosions | P5 : 30 animaux + éclosions en série, aucune frame au-dessus de 33 ms |
| 30 Traçabilité | 12 modèles sur 12 ont leur source |
| 32 Assets non référencés | 15 : sources d'export FBX, includes de shader, atlas, prefabs du kit (gardés pour la dispersion de T33) |

## Performance (player Windows de développement, 480x1040, Ryzen 7 5800X + RTX 5060 Ti, PC et non téléphone)

| Scénario | Animaux | Frame CPU p95 | Thread principal moyen / max | GPU moyen | Frames > 33 ms | Mémoire utilisée |
|---|---|---|---|---|---|---|
| P0 vide | 0 | 16,74 ms | 1,26 / 2,52 ms | 0,08 ms | 0 | 231 Mo |
| P2 | 20 | 16,70 ms | 1,51 / 2,88 ms | 0,36 ms | 0 | 232 Mo |
| S30 | 30 | 16,76 ms | 1,79 / 9,39 ms | 0,42 ms | 0 | 232 Mo |
| S50 | 50 | 17,83 ms | 2,42 / 50,44 ms | 0,57 ms | 1 | 233 Mo |
| P2-Low | 20 | 16,87 ms | 1,43 / 4,80 ms | 0,10 ms | 0 | 220 Mo |
| S50-High | 50 | 16,99 ms | 1,96 / 10,43 ms | 0,58 ms | 0 | 248 Mo |
| P5 éclosions | 30 | 16,72 ms | 1,69 / 9,99 ms | 0,36 ms | 0 | 235 Mo |
| S50-Mix3 | 50 | 16,71 ms | 1,71 / 2,78 ms | 0,52 ms | 0 | 234 Mo |
| W1 streaming | 20 | 16,75 ms | 1,50 / 3,41 ms | 0,17 ms | 0 | 234 Mo |
| U1 boutique | 20 | 16,71 ms | 1,20 / 6,47 ms | 0,18 ms | 0 | 234 Mo |

La frame CPU de 16,7 ms est la synchronisation verticale à 60 Hz : le jeu travaille environ 1,5 à 2,5 ms par frame. Le pic
unique de S50 (63 ms) ne s'est pas reproduit : deux relances isolées de S30 et S50 donnent un maximum de 17,1 ms et un
thread principal à 3 ms. Rapport complet : `Docs/Performance/Baselines/T30_player_windows_all.json`.

## Beauté (captures TestResults/Captures/T30_*)

Les animaux sont charmants, lisibles et bien éclairés ; l'œuf attire l'œil ; le Low garde le charme. Mais le décor est loin
du niveau « magnifique et très détaillé » demandé le 5 octobre : grande moitié inférieure de sol uni, peu d'herbes et de
fleurs, pas de chemin, pas d'eau, pas de vie ambiante, ciel uni, une seule ambiance de lumière. C'est l'objet de T33.

## Suite : tâches correctives ajoutées à la file

| Tâche | Contenu | Ferme les manques |
|---|---|---|
| T31, T32 | Polices maison, styles TMP | Aucun TEMP visible |
| T33 | Prairie Magnifique (dispersion, vie ambiante, ciel, ambiances, kit complet) | Beauté, D-BIO-001, F-* Prairie |
| T34 | Qualité adaptative, préparation des shaders | Pics de premier affichage |
| T34A | Feedback et écrans de la tranche : VFX EggTap, CollectLight, NewSpecies, NewVariant, FavoriteHeart, MasteryLevelUp, HatchEpic/Legendary/Mythic, SH_RareAura ; fiche animal ; annonce de découverte ; splash ; toasts ; modales ; geste de tutoriel ; cadres de rareté ; badges de traits ; icône favori | Gates 10, 12 ; UI et Feedback du DoD |
| T34B | Audio et haptique de base : SFX créés pour le projet (synthèse maison, sans dépendance tierce), ambiance Prairie, thème du sanctuaire, profils haptiques et implémentation Android | Gates 14, 15 ; famille J |
| T34C | Outils et contrôle : BuildValidator (exclut TEMP et bac à sable), matériaux de debug LOD et overdraw, marqueurs de spawn, captures de référence (golden visual), scènes de capture et benchmark VFX, scénario Codex 5 000, groupes Addressables Bootstrap, CommonAnimals et UI_Common | Gates 17, 19, 33 ; famille R et ED-010 |
| T34D | Nouveau passage du gate de la tranche verticale | Verdict final, hors mesure téléphone |

**Validation sur téléphone** : aucun gate de performance ne peut passer à DONE sans mesure sur un vrai appareil (règle du doc
12 : ne jamais inventer de mesure). Il faut un téléphone Android branché en USB (mode développeur), ou que le propriétaire
installe l'APK de développement et lance le benchmark. En attendant, les statuts de performance restent en revue.
