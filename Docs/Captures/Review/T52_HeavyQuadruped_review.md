# T52 art review: Boar Piglet (species.boarpiglet), HeavyQuadruped

Sheet: `T52_heavy_sheet.png` (Blender Workbench previews, palettes blended like SH_AnimalMaster). Row 1: Brown at LOD0, game pitch 40 degrees, front three-quarter, side, front. Row 2: Chestnut Stripe, Cream, Black, Acorn (preview palettes). Rows 3 and 4: Chestnut Stripe in Locomotion_Walk, Happy (in the air), Sleep_Loop, Eat_Loop, Inspect (snout push), Idle_B (looking round), Social_Nose, PeekOut. Row 5: LOD1 and LOD2. To confirm in Unity next to the Badger and the Fawn.
Sources: `SourceArt/Blender/Animals/BoarPiglet/BL_Animal_BoarPiglet.blend`, started from the Badger file with its armature swapped for `pw_rig.build_armature("HeavyQuadruped")`, built by `pw_species.py` (SPECS BoarPiglet), `pw_masks.py` (boarpiglet_masks), exported by `pw_export.py`. Rig `SourceArt/Blender/Rigs/BL_Rig_HeavyQuadruped.blend` (`pw_rig.py` HEAVY_QUADRUPED), clips in `pw_anim_heavy.py`. No generator, library model or scan.

| Check | Boar Piglet |
|---|---|
| Same family as the meadow and forest animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) |
| Reads as the species | round barrel body, big round head with chubby cheeks, short snout ending in a round pink disc with two nostrils, soft triangle ears leaning forward, short stubby legs, little curly tail, no tusks |
| Size (LOD0) | 0.60 m long, 0.43 m tall with the ears, 0.27 m wide (doc estimate 66 x 42) |
| Budgets | 3272 / 1256 / 536 tris (2.5-3.5k / 1.0-1.4k / 0.4-0.65k) |
| Skinning | 3 influences, no unweighted vertex, ears turn with Ear_01 (gain 0.8), curly tube tail on Tail_01 and Tail_02, face and nostrils rigid on Head |
| Rig | SK_HeavyQuadruped, 24 bones with the quadruped names, parents and axes; sockets HeadTop, Neck, Back, TailBase, Ear_L, Ear_R on the usual bones |
| Masks | secondary snout disc and inner ears, belly underside, detail nostrils; A snout disc and tail, B three light stripes per flank, C small back spots |
| Ground (every frame of the 12 clips, LOD0 and LOD2) | lowest point 0.9 mm below the ground (Walk, LOD2); Sleep belly at +0.1 mm |
| Nothing enters anything (head, tail, each leg and the body, every frame) | 0 overlaps in every clip, LOD0 and LOD2 |
| No four-point star, nothing religious | yes |
