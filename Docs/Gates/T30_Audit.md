# T30 Vertical Slice Gate — audit automatique

Généré par `Pocket Wilds/Gates/Vertical Slice Audit (T30)` le 2026-10-05 18:04:17Z.
Chaque ligne est vérifiée sur le disque ; le fichier de statut n'est jamais cru sur parole.

## Gate 00 — couverture VerticalSlice

| Total | DONE | INTEGRATED ou PERFORMANCE_REVIEW | En revue (BLOCKOUT, ART_REVIEW) | PLANNED | Bloquants |
|---|---|---|---|---|---|
| 151 | 0 | 64 | 13 | 65 | 78 |

Écarts entre statut et réalité :

| ID | Nom | Statut | Source | Runtime | Constat |
|---|---|---|---|---|---|

Éléments bloquants (à produire ou en revue) :

| ID | Nom | Famille | Statut | Constat |
|---|---|---|---|---|
| N-GRP-Audio_Common | Addressables group Audio_Common | Addressables | PLANNED | à produire |
| N-GRP-Audio_Grassland | Addressables group Audio_Grassland | Addressables | PLANNED | à produire |
| N-GRP-Bootstrap | Addressables group Bootstrap | Addressables | PLANNED | à produire |
| N-GRP-CommonAnimals | Addressables group CommonAnimals | Addressables | PLANNED | à produire |
| N-GRP-UI_Common | Addressables group UI_Common | Addressables | PLANNED | à produire |
| J-AMB-001 | Ambience Grassland | Audio | PLANNED | à produire |
| J-AN-RabbitHappy | RabbitHappy x3 | Audio | PLANNED | à produire |
| J-AN-RabbitSleep | RabbitSleep x2 | Audio | PLANNED | à produire |
| J-AN-SmallQuadruped_CuteChirp | SmallQuadruped_CuteChirp x4 | Audio | PLANNED | à produire |
| J-AN-SmallQuadruped_Sleep | SmallQuadruped_Sleep x2 | Audio | PLANNED | à produire |
| J-MUS-001 | SanctuaryTheme | Audio | PLANNED | à produire |
| J-SFX-CURRENCY-001 | CollectLight x3 | Audio | PLANNED | à produire |
| J-SFX-EGG-001 | EggTap x2 | Audio | PLANNED | à produire |
| J-SFX-EGG-002 | Crack x3 | Audio | PLANNED | à produire |
| J-SFX-EGG-003 | HatchPop common | Audio | PLANNED | à produire |
| J-SFX-EGG-004 | RareReveal | Audio | PLANNED | à produire |
| J-UI-001 | Tap x3 | Audio | PLANNED | à produire |
| J-UI-002 | Confirm | Audio | PLANNED | à produire |
| J-UI-003 | Back | Audio | PLANNED | à produire |
| R-BENCH-5K | Synthetic collection 5k | Benchmark | PLANNED | à produire |
| D-BIO-001 | Grassland | Biome | BLOCKOUT | en cours (revue) |
| M-DEF-AUDIOEVENT | Audio event registry | ContentData | PLANNED | à produire |
| M-DEF-CAPTURE | Capture profiles | ContentData | PLANNED | à produire |
| M-DEF-DENSITY | Biome density profiles | ContentData | PLANNED | à produire |
| M-DEF-HAPTIC | Haptic profiles | ContentData | PLANNED | à produire |
| M-DEF-QUALITY | Quality profiles | ContentData | BLOCKOUT | en cours (revue) |
| M-DEF-VFXEVENT | VFX registry | ContentData | PLANNED | à produire |
| R-DBG-LOD | DBG_LOD_Red/Green/Blue materials | Debug | PLANNED | à produire |
| R-DBG-OVERDRAW | Overdraw/quality capture configs | Debug | PLANNED | à produire |
| R-DBG-SPAWN | Spawn markers/icons (editor) | Debug | PLANNED | à produire |
| ED-001 | SpeciesWizard | EditorTool | BLOCKOUT | en cours (revue) |
| ED-003 | EggWizard | EditorTool | BLOCKOUT | en cours (revue) |
| ED-010 | BuildValidator | EditorTool | PLANNED | à produire |
| ED-011 | ContentIdRegistryTool | EditorTool | BLOCKOUT | en cours (revue) |
| D-BIO-001/GL_Edge_A | GL_Edge_A | EnvironmentProp | PLANNED | à produire |
| R-GOLDEN-VISUAL | Golden reference screenshots | Fixture | PLANNED | à produire |
| J-HAPTIC-AnimalTouch | Haptic_AnimalTouch | Haptic | PLANNED | à produire |
| J-HAPTIC-Confirm | Haptic_Confirm | Haptic | PLANNED | à produire |
| J-HAPTIC-ErrorSoft | Haptic_ErrorSoft | Haptic | PLANNED | à produire |
| J-HAPTIC-HatchCommon | Haptic_HatchCommon | Haptic | PLANNED | à produire |
| J-HAPTIC-Legendary | Haptic_Legendary | Haptic | PLANNED | à produire |
| J-HAPTIC-Mythic | Haptic_Mythic | Haptic | PLANNED | à produire |
| J-HAPTIC-None | Haptic_None | Haptic | PLANNED | à produire |
| J-HAPTIC-PurchaseSuccess | Haptic_PurchaseSuccess | Haptic | PLANNED | à produire |
| J-HAPTIC-Rare | Haptic_Rare | Haptic | PLANNED | à produire |
| J-HAPTIC-TapLight | Haptic_TapLight | Haptic | PLANNED | à produire |
| K-PF-POPUP | Pooled feedback popups | Prefab | PLANNED | à produire |
| K-PF-SELECTION | PF_WorldSelectionProxy | Prefab | PLANNED | à produire |
| L-SC-004 | SC_ArtCapture_Animals | Scene | PLANNED | à produire |
| L-SC-005 | SC_ArtCapture_Props | Scene | PLANNED | à produire |
| L-SC-008 | SC_Benchmark_VFX | Scene | PLANNED | à produire |
| L-SC-009 | SC_Sandbox_Art | Scene | PLANNED | à produire |
| G-SH-006 | SH_RareAura module | Shader | PLANNED | à produire |
| I-CODEX | Codex silhouettes/badges/trait badges | UIIcon | BLOCKOUT | en cours (revue) |
| I-HUD-010 | Favorite heart icon | UIIcon | PLANNED | à produire |
| I-RARITY | Rarity frames + badges (Common..Secret) | UIIcon | BLOCKOUT | en cours (revue) |
| I-TUTORIAL | Gesture sprites | UIIcon | PLANNED | à produire |
| I-KIT | Core shape kit (panels, buttons, pills, progress, tooltip) | UIKit | BLOCKOUT | en cours (revue) |
| K-PF-UI-Toast | PF_UI_Toast | UIPrefab | PLANNED | à produire |
| I-SCR-001 | Boot/Splash | UIScreen | PLANNED | à produire |
| I-SCR-002 | Main HUD | UIScreen | ART_REVIEW | en cours (revue) |
| I-SCR-003 | Hatch overlay | UIScreen | PLANNED | à produire |
| I-SCR-004 | Offline return | UIScreen | ART_REVIEW | en cours (revue) |
| I-SCR-005 | Animal quick sheet | UIScreen | PLANNED | à produire |
| I-SCR-007 | Codex list | UIScreen | ART_REVIEW | en cours (revue) |
| I-SCR-008 | Codex species detail | UIScreen | ART_REVIEW | en cours (revue) |
| I-SCR-025 | Error/Confirm modals | UIScreen | PLANNED | à produire |
| I-SCR-026 | Tutorial gestures | UIScreen | PLANNED | à produire |
| I-SCR-027 | Toasts | UIScreen | PLANNED | à produire |
| H-VFX-001 | VFX_EggTap | VFX | PLANNED | à produire |
| H-VFX-005 | VFX_HatchEpic | VFX | PLANNED | à produire |
| H-VFX-006 | VFX_HatchLegendary | VFX | PLANNED | à produire |
| H-VFX-007 | VFX_HatchMythic | VFX | PLANNED | à produire |
| H-VFX-008 | VFX_CollectLight | VFX | PLANNED | à produire |
| H-VFX-009 | VFX_MasteryLevelUp | VFX | BLOCKOUT | en cours, runtime absent |
| H-VFX-010 | VFX_NewSpecies | VFX | PLANNED | à produire |
| H-VFX-011 | VFX_NewVariant | VFX | PLANNED | à produire |
| H-VFX-012 | VFX_FavoriteHeart | VFX | PLANNED | à produire |

## Gates 06 et 31 — matériaux

7 matériaux dans Assets/_Game.

| Shader | Matériaux |
|---|---|
| PocketWilds/SkyGradient | 1 |
| PocketWilds/AnimalMaster | 1 |
| PocketWilds/BlobShadow | 1 |
| PocketWilds/EggMaster | 1 |
| PocketWilds/EnvironmentOpaque | 1 |
| PocketWilds/GroundMaster | 1 |
| PocketWilds/AdditiveVFXLite | 1 |

Shaders non approuvés : aucun.

Doublons exacts (mêmes shader et propriétés) : aucun.

## Gate 07 — textures (Assets/_Game/Art)

45 textures, 11,7 Mo en mémoire éditeur (indicatif, la compression Android réduit).

| Texture | Taille | Format Android | Mipmaps | Mémoire (Ko) |
|---|---|---|---|---|
| UI/Kit/UI_Button_IconCircle.png | 256x256 | Uncompressed | non | 512 |
| Eggs/Textures/T_EggMask_Common.png | 256x256 | ASTC_4x4 | oui | 470 |
| UI/Shop/ICO_GemPack_01.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_02.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_03.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_04.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_05.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_06.png | 192x192 | Uncompressed | non | 336 |
| UI/Thumbnails/Animals/TH_species_hedgehog_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_species_rabbit_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_species_squirrel_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_black.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_brown.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_cream.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_grey.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_mushroom.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_hedgehog_white.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_black.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_brown.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_grey.png | 256x256 | ASTC_4x4 | non | 320 |

Textures de plus de 1024 px hors atlas : aucune.

## Gate 08 — prefabs

26 prefabs dans Assets/_Game/Prefabs. Interdits cherchés : MeshCollider, Rigidbody, NavMeshAgent, AudioSource, CharacterController, Update/LateUpdate/FixedUpdate dans les prefabs du monde, matériaux instanciés, scripts manquants.

- Assets/_Game/Prefabs/World/PF_CameraRig_Sanctuary.prefab : SanctuaryCameraRig a un Update/LateUpdate/FixedUpdate
- Assets/_Game/Prefabs/World/PF_CameraRig_Sanctuary.prefab : SanctuaryCameraInput a un Update/LateUpdate/FixedUpdate

## Gate 16 — données de contenu

Aucune erreur. 3 espèces, 18 variantes, 2 œufs, 1 biome(s), 7 produits, 8 offres.

## Gate 30 — traçabilité des sources

| Modèle runtime | Source Blender |
|---|---|
| Animals/Hedgehog/Models/SKM_Hedgehog.fbx | SourceArt/Blender/Animals/Hedgehog/BL_Animal_Hedgehog.blend |
| Animals/Rabbit/Models/SKM_Rabbit.fbx | SourceArt/Blender/Animals/Rabbit/BL_Animal_Rabbit.blend |
| Animals/Squirrel/Models/SKM_Squirrel.fbx | SourceArt/Blender/Animals/Squirrel/BL_Animal_Squirrel.blend |
| Animations/SmallQuadruped/AN_SmallQuadruped.fbx | SourceArt/Blender/Rigs/BL_Rig_SmallQuadruped.blend |
| Eggs/Models/SM_Egg_Base.fbx | SourceArt/Blender/Eggs/BL_Egg_Family.blend |
| Eggs/Models/SM_Egg_Fragments.fbx | SourceArt/Blender/Eggs/BL_Egg_Family.blend |
| Environment/Grassland/Models/GL_Flowers.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_GrassTufts.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Ground_Core_A.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Ground_Core_B.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Rocks.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Tree_A.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |

Textures et sprites générés : SourceArt/UI/Tools/pw_ui_sprites.py (UI), SourceArt/Blender/Tools/pw_masks.py (masques), maillages de kit cuits depuis les FBX par GrasslandPipeline.BakeKitMeshes.

## Gate 32 — assets non référencés

Racines : scènes du build, base de contenu, entrées Addressables. 138 assets dans Art et Prefabs, 15 non atteints depuis ces racines (rapport seulement, rien n'est supprimé ; les sources d'export FBX et les assets d'outils éditeur apparaissent ici normalement).

- Art/Environment/Grassland/Models/GL_Flowers.fbx
- Art/Environment/Grassland/Models/GL_GrassTufts.fbx
- Art/Environment/Grassland/Models/GL_Rocks.fbx
- Art/Environment/Grassland/Models/GL_Tree_A.fbx
- Art/Shaders/Include/PW_DepthOnlyPass.hlsl
- Art/Shaders/Include/PW_ShadowCasterPass.hlsl
- Art/Shaders/Include/PW_Stylized.hlsl
- Art/UI/Atlases/Atlas_CommonHUD.spriteatlasv2
- Art/UI/Atlases/Atlas_Shop.spriteatlasv2
- Prefabs/Environment/Grassland/PF_GL_Flower_A.prefab
- Prefabs/Environment/Grassland/PF_GL_Flower_B.prefab
- Prefabs/Environment/Grassland/PF_GL_Flower_C.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_L.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_M.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_S.prefab

