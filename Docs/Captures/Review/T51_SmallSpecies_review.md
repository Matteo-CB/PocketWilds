# T51 art review: Raccoon (species.raccoon) and Red Panda (species.redpanda), SmallQuadruped

Sheet: `Docs/Captures/Review/T51_small_sheet.png` (Blender Workbench, vertex colors through `pw_preview` with variant palettes; to confirm in Unity at Codex size and in the refuge). Row 1 Raccoon: three-quarter, side, face, LOD1, Cream, Moon Mask. Row 2 Red Panda: three-quarter, side, face, LOD1, Snow, Bamboo. Row 3: RaccoonWash (frames 16 and 38), RedPandaStand (frames 20 and 56), both LOD1 at phone size.
Sources: `SourceArt/Blender/Animals/Raccoon/BL_Animal_Raccoon.blend` and `.../RedPanda/BL_Animal_RedPanda.blend`, both started from `BL_Animal_Rabbit.blend` and built by `pw_species.py` (SPECS Raccoon, RedPanda; `add_tail_tube` for the ringed tails) and `pw_masks.py` (raccoon_masks, redpanda_masks), exported by `pw_export.py`. Clips: `pw_anim_library.py` in `BL_Rig_SmallQuadruped.blend`. No generator, library model or scan.

| Check | Raccoon | Red Panda |
|---|---|---|
| Same family as the meadow animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) | yes (Blender) |
| Reads as the species | round body, big round head with cheek ruffs, short muzzle, small round ears, joined bandit mask, white brows and muzzle, ringed tail with a dark tip | round face, white muzzle, cheek patches and brow dots with red tear tracks, white-fronted triangle ears, dark legs and belly, thick ringed tail curling up |
| Rig | SK_SmallQuadruped, Rabbit bind pose unchanged | same |
| Model size (LOD0, small rig scale) | 0.585 m long (nose to tail), 0.32 m tall, 0.24 m wide | 0.567 m long, 0.31 m tall, 0.23 m wide |
| Prefab uniform scale (doc length) | 1.06 (about 62 x 34 cm) | 1.165 (about 66 x 36 cm) |
| Budgets (LOD0 / LOD1 / LOD2) | 2486 / 1070 / 436 (2.2-3.0k / 0.9-1.2k / 0.35-0.5k) | 2988 / 1178 / 476 (2.6-3.4k / 1.0-1.4k / 0.4-0.6k) |
| Skinning | 3 influences, no unweighted vertex | same |
| Rings readable on LOD1, no extra submesh | yes: the tube tail has loop pairs on every ring edge (one mesh, one material) | yes |
| Regions | secondary inner ears and paws, belly brows, muzzle, lower cheeks, throat, underside; detail = face mask and nose | secondary white face and ear fronts, belly dark legs, underside and chest; detail = nose |
| Masks | A and B = tail rings and dark tip | A = rings and ear tips, B = rings, C (uv1.x) = two light dots per cheek |
| Sockets | HeadTop, Neck, Back, TailBase, Ear_L/R on the mesh | same |
| Ground | feet at z = 0 on every LOD; RaccoonWash and RedPandaStand keep every vertex on or above the ground (1 mm at most below, LOD0 and LOD2, checked every frame) | same |
| Nothing enters anything; no four-point star, nothing religious | yes | yes |

Clips (AN_SmallQuadruped, 26 clips; the 24 existing clips are identical in the FBX, compared key by key):
- `AN_SmallQuadruped_RaccoonWash` (72 frames, one-shot): sits back, lifts both hands in front of the chest, four rubs, settles. Hind paws stay within 1.4 mm of rest; the hands never pass through each other (closest 6 mm on the Raccoon) or the body.
- `AN_SmallQuadruped_RedPandaStand` (84 frames, one-shot): rises on the hind legs (pelvis 50, torso about 76 degrees), paws lifted and spread, looks to each side, settles. Hind paws stay within 4.3 mm of rest.
- Both are safe on the Rabbit, Guinea Pig and Field Hamster too. Known for the whole family (not new): Sleep, Hop and ReactTouch already dip 1.5 to 3.9 cm below the ground on every small species, Rabbit included.
