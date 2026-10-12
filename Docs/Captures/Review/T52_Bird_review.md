# T52 art review: Owl (species.owl), Bird

Sheet: `T52_bird_sheet.png` (Blender Workbench previews, palettes blended like SH_AnimalMaster). Row 1: Brown, Tawny, Grey, Cream, Dark, Constellation at LOD0, game pitch 40 degrees. Row 2: Cocoa, Periwinkle, Honey Wings (mask B bands), Honey Wings (mask A tips), Full Moon, Russet visitor (preview palettes). Row 3: Tawny in Idle_B (head turn), Happy (wings out), Eat_Loop (whole-body peck), Sleep_Loop, ReactTouch (fluff), Locomotion_Hop (in the air). Row 4: Constellation front, side, back three-quarter. Row 5: LOD1 Brown and Constellation, LOD2 Brown. To confirm in Unity next to the Raccoon and the Red Panda.
Sources: `SourceArt/Blender/Animals/Owl/BL_Animal_Owl.blend`, made by `pw_species.build_bird("Owl", BL_Animal_Fawn.blend)` (the Fawn file's helpers, its armature swapped for `pw_rig.build_armature("Bird")`), `pw_species.py` (SPECS Owl, apply_part_weights, carve_grooves, shape_beak, cut_mask_shapes), `pw_masks.py` (owl_masks), exported by `pw_export.py`. Rig `SourceArt/Blender/Rigs/BL_Rig_Bird.blend` (`pw_rig.py` BIRD), clips in `pw_anim_bird.py`. No generator, library model or scan.

| Check | Owl |
|---|---|
| Same family as the meadow and forest animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) |
| Reads as the species | nearly round body with the big head merged in, soft crown corners, two raised round eye discs with bead eyes, a small drop beak, folded wings with a soft crease, tiny feet under the belly, a short tail fan; no individual feather |
| Size (LOD0) | 0.33 m long, 0.44 m tall, 0.38 m wide at the wings (doc estimate 36 x 44) |
| Budgets | 2594 / 980 / 421 tris (2.0-2.8k / 0.8-1.1k / 0.3-0.5k) |
| Skinning | 3 influences, no unweighted vertex; face, discs, eyes and beak rigid on Head; wings on Wing_Upper and Wing_Lower by their volumes (only where they stand out of the body); feet on Foot; the lowest body on Root |
| Rig | SK_Bird, 14 bones (Root, Body, Neck, Head, Wing_L/R_Upper and _Lower, Leg_L/R, Foot_L/R, Tail_01, Tail_02); sockets HeadTop, Neck, Back (between the wings), TailBase; no ear socket |
| Masks | secondary eye discs (crisp, cut in the mesh), belly round front, detail beak and feet; A wing tips, tail tip and disc rims; B two bands per wing; C belly specks; D Constellation round five-point stars (eight, cut in the mesh) |
| Ground (every frame of the 11 clips, LOD0 and LOD2) | lowest point 0.0 mm (rest contact); Eat_Loop leans with Root and rises, lowest +0.3 mm |
| Nothing enters anything (head, each wing, tail and the body, every frame) | 0 overlaps in every clip, LOD0 and LOD2 |
| No four-point star, nothing religious | yes: the stars are round five-point stars only |
