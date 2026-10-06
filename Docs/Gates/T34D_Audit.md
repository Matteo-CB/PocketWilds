# T34D Vertical Slice Gate — audit automatique

Généré par `Pocket Wilds/Gates/Vertical Slice Audit` le 2026-10-06 23:16:21Z.
Chaque ligne est vérifiée sur le disque ; le fichier de statut n'est jamais cru sur parole.

## Gate 00 — couverture VerticalSlice

| Total | DONE | INTEGRATED ou PERFORMANCE_REVIEW | En revue (BLOCKOUT, ART_REVIEW) | PLANNED | Bloquants |
|---|---|---|---|---|---|
| 155 | 0 | 139 | 0 | 0 | 0 |

Écarts entre statut et réalité :

| ID | Nom | Statut | Source | Runtime | Constat |
|---|---|---|---|---|---|

Éléments bloquants (à produire ou en revue) :

| ID | Nom | Famille | Statut | Constat |
|---|---|---|---|---|

## Gates 06 et 31 — matériaux

20 matériaux dans Assets/_Game (hors Art/Debug, matériaux de contrôle jamais dans un build, et hors matériaux internes des polices sources TTF).

| Shader | Matériaux |
|---|---|
| TextMeshPro/Mobile/Distance Field | 7 |
| PocketWilds/SkyGradient | 1 |
| PocketWilds/AmbientLife | 1 |
| PocketWilds/AnimalMaster | 1 |
| PocketWilds/BlobShadow | 1 |
| PocketWilds/EggMaster | 1 |
| PocketWilds/EnvironmentOpaque | 1 |
| PocketWilds/FoliageWind | 1 |
| PocketWilds/GroundMaster | 1 |
| PocketWilds/RainLite | 1 |
| PocketWilds/Rainbow | 1 |
| PocketWilds/SunRays | 1 |
| PocketWilds/AdditiveVFXLite | 1 |
| PocketWilds/WaterLite | 1 |

Shaders non approuvés : aucun.

Doublons exacts (mêmes shader et propriétés) : aucun.

## Gate 07 — textures (Assets/_Game/Art)

82 textures, 20,6 Mo en mémoire éditeur (indicatif, la compression Android réduit).

| Texture | Taille | Format Android | Mipmaps | Mémoire (Ko) |
|---|---|---|---|---|
| UI/Fonts/PocketPlush/PocketPlush-Black SDF.asset | 1024x1024 | ? | non | 2048 |
| UI/Fonts/PocketRound/PocketRound-Bold SDF.asset | 1024x1024 | ? | non | 2048 |
| UI/Fonts/PocketRound/PocketRound-Regular SDF.asset | 1024x1024 | ? | non | 2048 |
| UI/Fonts/PocketRound/PocketRound-SemiBold SDF.asset | 1024x1024 | ? | non | 2048 |
| UI/Codex/UI_Frame_Common.png | 256x256 | Uncompressed | non | 512 |
| UI/Kit/UI_Button_IconCircle.png | 256x256 | Uncompressed | non | 512 |
| Eggs/Textures/T_EggMask_Common.png | 256x256 | ASTC_4x4 | oui | 470 |
| UI/Kit/UI_Gesture_Tap.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_01.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_02.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_03.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_04.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_05.png | 192x192 | Uncompressed | non | 336 |
| UI/Shop/ICO_GemPack_06.png | 192x192 | Uncompressed | non | 336 |
| UI/Thumbnails/Animals/TH_species_hedgehog_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_species_rabbit_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_species_squirrel_Silhouette.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_brown.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_spotted.png | 256x256 | ASTC_4x4 | non | 320 |
| UI/Thumbnails/Animals/TH_variant_rabbit_white.png | 256x256 | ASTC_4x4 | non | 320 |

Textures de plus de 1024 px hors atlas : aucune.

## Gate 08 — prefabs

61 prefabs dans Assets/_Game/Prefabs. Interdits cherchés : MeshCollider, Rigidbody, NavMeshAgent, AudioSource, CharacterController, Update/LateUpdate/FixedUpdate dans les prefabs du monde, matériaux instanciés, scripts manquants.

- Assets/_Game/Prefabs/World/PF_CameraRig_Refuge.prefab : RefugeCameraRig a un Update/LateUpdate/FixedUpdate
- Assets/_Game/Prefabs/World/PF_CameraRig_Refuge.prefab : RefugeCameraInput a un Update/LateUpdate/FixedUpdate

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
| Environment/Grassland/Models/GL_Grounds_T33B.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Life.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Props_T33.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Props_T33B.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Rocks.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Scatter.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Tree_A.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |
| Environment/Grassland/Models/GL_Water.fbx | SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Core.blend |

Textures et sprites générés : SourceArt/UI/Tools/pw_ui_sprites.py (UI), SourceArt/Blender/Tools/pw_masks.py (masques), maillages de kit cuits depuis les FBX par GrasslandPipeline.BakeKitMeshes.

## Gate 32 — assets non référencés

Racines : scènes du build, base de contenu, entrées Addressables. 350 assets dans Art et Prefabs, 33 non atteints depuis ces racines (rapport seulement, rien n'est supprimé ; les sources d'export FBX et les assets d'outils éditeur apparaissent ici normalement).

- Art/Debug/DBG_LOD_Blue.mat
- Art/Debug/DBG_LOD_Green.mat
- Art/Debug/DBG_LOD_Red.mat
- Art/Environment/Grassland/Models/GL_Flowers.fbx
- Art/Environment/Grassland/Models/GL_GrassTufts.fbx
- Art/Environment/Grassland/Models/GL_Grounds_T33B.fbx
- Art/Environment/Grassland/Models/GL_Life.fbx
- Art/Environment/Grassland/Models/GL_Props_T33.fbx
- Art/Environment/Grassland/Models/GL_Props_T33B.fbx
- Art/Environment/Grassland/Models/GL_Rocks.fbx
- Art/Environment/Grassland/Models/GL_Scatter.fbx
- Art/Environment/Grassland/Models/GL_Tree_A.fbx
- Art/Environment/Grassland/Models/GL_Water.fbx
- Art/Shaders/Include/PW_DepthOnlyPass.hlsl
- Art/Shaders/Include/PW_ShadowCasterPass.hlsl
- Art/Shaders/Include/PW_Stylized.hlsl
- Art/Shaders/Include/PW_World.hlsl
- Art/Shaders/Include/SH_RareGlow.hlsl
- Art/UI/Atlases/Atlas_Codex.spriteatlasv2
- Art/UI/Atlases/Atlas_CommonHUD.spriteatlasv2
- Art/UI/Atlases/Atlas_Shop.spriteatlasv2
- Art/UI/Fonts/DEF_TextStyles.asset
- Art/UI/Fonts/PocketPlush/PocketPlush-Black.ttf
- Art/UI/Fonts/PocketRound/PocketRound-Bold SDF Shadow.mat
- Art/UI/Fonts/PocketRound/PocketRound-Bold.ttf
- Art/UI/Fonts/PocketRound/PocketRound-Regular.ttf
- Art/UI/Fonts/PocketRound/PocketRound-SemiBold.ttf
- Prefabs/Environment/Grassland/PF_GL_Flower_A.prefab
- Prefabs/Environment/Grassland/PF_GL_Flower_B.prefab
- Prefabs/Environment/Grassland/PF_GL_Flower_C.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_L.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_M.prefab
- Prefabs/Environment/Grassland/PF_GL_GrassTuft_S.prefab

## Gate 33 — contenu d'un build release

BuildValidator en mode release sur les scènes du build, les entrées Addressables et les dossiers Resources : 0 erreurs, 0 avertissements, 370 assets contrôlés.


