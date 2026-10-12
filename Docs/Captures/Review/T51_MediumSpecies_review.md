# T51 art review: Fox (species.fox) and Badger (species.badger), MediumQuadruped

Sheet: `T51_medium_sheet.png` (Blender Workbench previews, game pitch 40 degrees and side; Fox Red, Firefly and the curled sleep; Badger Classic, Honey, the dig and the sleep). To confirm in Unity next to the Fawn.
Sources: `SourceArt/Blender/Animals/Fox/BL_Animal_Fox.blend` and `.../Badger/BL_Animal_Badger.blend`, started from the Fawn file and built by `pw_species.py` (SPECS Fox, Badger), `pw_rig.py` (SPECIES_BONES, fit_species), `pw_masks.py` (fox_masks, badger_masks), exported by `pw_export.py`. Clips in `pw_anim_medium.py`. No generator, library model or scan.

| Check | Fox | Badger |
|---|---|---|
| Same family as the meadow animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) | yes (Blender) |
| Reads as the species | soft triangular face, white cheek ruffs, big upright ears, compact chest ruff, short legs with dark socks, huge plume with a pale tip | low heavy body, long tapered head, black and white face stripes, small round ears, short thick dark paws, short tail |
| Size (LOD0) | 0.745 m long, 0.453 m tall (target about 80 x 50) | 0.685 m long, 0.288 m tall (target about 70 x 36) |
| Budgets | 3568 / 1296 / 568 tris (2.8-3.8k / 1.1-1.5k / 0.4-0.65k) | 2910 / 1150 / 490 tris (2.5-3.4k / 1.0-1.3k / 0.35-0.6k) |
| Skinning | 3 influences, no unweighted vertex, plume on Tail_01..03, ears turn with Ear_01 (gain 0.7) | 3 influences, no unweighted vertex, ears rigid on Head |
| Rig reuse | SK_MediumQuadruped, every bone keeps the Fawn direction and roll (rest turn 0.0) | same (rest turn 0.0) |
| Masks | A tail tip and ear tips, B shoulder cross and face, C flank leaves and tail tip | detail stripes and paws, A paws and muzzle, B pale blaze, C back spots |
| Ground and contacts (every frame, LOD0 and LOD2) | family clips as on the Fawn (paws dip at most 2.7 cm in Happy, like the Fawn); FoxCurlSleep and the Low clips within 0.9 cm | family clips within 3.2 cm (Happy), BadgerSleep, BadgerDig and the Low clips within 0.9 cm |
| Nothing enters anything (head, tail and body faces, every frame) | 0 overlaps in FoxCurlSleep and the Low clips | 0 overlaps in BadgerSleep, BadgerDig and the Low clips |
| No four-point star, nothing religious | yes | yes |
