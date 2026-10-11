# T49 art review: Mole (species.mole) and its Star Nose module

Sheet: to capture in Unity (Codex size, refuge, next to the Guinea Pig and the Hedgehog; Velvet, Golden Moss and Star Nose).
Sources: `SourceArt/Blender/Animals/Mole/BL_Animal_Mole.blend` built by `pw_species.py` (SPECS Mole, started from the Squirrel file; `build_star_nose` for the module) and `pw_masks.py` (mole_masks), exported by `pw_export.py`. No generator, library model or scan.

| Check | Result |
|---|---|
| Same family as the meadow animals (bead eyes, soft volumes, flat warm colors) | yes (Blender) |
| Reads as a mole: soft bean body, no visible neck, bead eyes almost hidden, pointed pink nose, big round mitten hands, small hind feet, tiny tail | yes (Blender), to confirm in Unity |
| Size: LOD0 0.47 m long (nose to tail), 0.207 m tall, 0.29 m wide across the hands (target about 0.46 x 0.20) | yes |
| Budgets: LOD0 2390, LOD1 888, LOD2 392 tris (1.9-2.6k / 0.75-1.0k / 0.28-0.42k), 3 influences, no unweighted vertex | yes |
| Star Nose module: 11 round lobes around the nose bead, half-circle outlines, no point; 143 tris LOD0, 55 LOD1, none at LOD2; rigid on Head, 0 bones | yes |
| Sockets in the FBX: HeadTop (0, -0.130, 0.186), Neck (0, -0.085, 0.128), Back (0, 0.030, 0.203); TailBase and Ear kept for the hierarchy, unused | yes (FBX listed) |
| Regions: velvet coat, pink hands, snout tip, hind feet and tail, paler underside and chin, pink nose bead | yes |
| Masks: A hands and nose (nose bead included), B paler muzzle, C round moss patches over the back (uv1.x) | yes, to check per color in Unity |
| Ground: hands and feet at z = 0, belly above; MoleWalk, MoleEat, MoleDig, PeekOut keep every vertex above the ground (LOD0, LOD2, module) | yes (checked every frame) |
| Nothing enters anything; no four-point star, nothing religious | yes |
