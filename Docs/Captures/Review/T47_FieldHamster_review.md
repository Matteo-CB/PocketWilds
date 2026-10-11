# T47 art review: Field Hamster (species.fieldhamster, companion)

Sheet: to capture in Unity (Codex size, refuge, next to the Guinea Pig and the Field Mouse).
Sources: `SourceArt/Blender/Animals/FieldHamster/BL_Animal_FieldHamster.blend` built by `pw_species.py` (SPECS FieldHamster, started from the Squirrel file) and `pw_masks.py` (fieldhamster_masks), exported by `pw_export.py`. No generator, library model or scan.

| Check | Result |
|---|---|
| Same family as the meadow animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) |
| Reads apart from the Guinea Pig: round ball body, round ears, puffed cheek pouches, short legs, no tail | yes (Blender), to confirm in Unity |
| Size: LOD0 0.27 m wide, 0.42 m long, 0.29 m tall (target about 0.44 x 0.30) | yes |
| Budgets: LOD0 2262, LOD1 878, LOD2 358 tris (1.9-2.6k / 0.75-1.0k / 0.28-0.42k), 3 influences, no unweighted vertex | yes |
| Sockets: HeadTop (0, -0.107, 0.255), Neck, Back (0, 0.040, 0.260), Ear_L/R; TailBase kept on the rump, unused (hidden tail) | yes |
| Regions: golden back, white belly, chin, muzzle and lower cheeks; pale pink inner ears and paws | yes |
| Masks: A ear glow (uv0.x), B cheek band (uv0.y), C back spots (uv1.x, UV map PW_Masks2) | yes, to check per look in Unity |
| Looks Field Gold, Wheat Cream, Field Lavender on one mesh | to capture in Unity |
| Cheek squash (Stash clip) safe on LOD1 and LOD2: the whole face is rigid on Head | yes (weights) |
| Nothing enters anything; no four-point star, nothing religious | yes |
