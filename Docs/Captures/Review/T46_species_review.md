# T46 art review (MediumQuadruped rig, Fawn, mastery colors)

Sheet: `T46_species_sheet.png` (top: the Fawn's ten colors, base colors then mastery 10, 20, 30 then the secret color;
bottom: the mastery colors of the Rabbit, Squirrel and Hedgehog). Stickers: `TestResults/Captures/T46_stickers.png`
(STK-GL-06, 10, 12).
Sources: `SourceArt/Blender/Rigs/BL_Rig_MediumQuadruped.blend` built by `pw_rig.py` (bone table) and
`pw_anim_medium.py` (12 clips), `SourceArt/Blender/Animals/Fawn/BL_Animal_Fawn.blend` built by `pw_species.py`
(SPECS Fawn on SK_MediumQuadruped) and `pw_masks.py`, exported by `pw_export.py`. Palettes in
`Editor/Animals/AnimalPipeline.cs`. No generator, library model or scan.

| Check | Result |
|---|---|
| Same family as the meadow animals (bead eyes, soft volumes, flat warm colors) | yes |
| Fawn silhouette at Codex size: big head, leaf ears facing forward, slender legs, hooves, white tail tuft | yes |
| Budgets (medium: 2.5k to 4k): LOD0 2928, LOD1 1198, LOD2 568 tris, 3 influences | yes |
| Skeleton: same bone names and axes as SmallQuadruped, 25 bones, sockets on the same parent bones | yes |
| Clips: walk (four beats), trot (diagonals), happy hop, reaction, lying down legs folded, grazing in the tall grass, social, inspect, ears listening (Idle_B) | yes (poses measured: lying belly 0 to 2 cm above the ground, hooves above it) |
| Nothing enters anything: body measured 0.55 m long, 0.62 m tall; hide spots rebuilt for six species, 0 overlaps | yes |
| Patterns: fawn spots, blossom spots, dew drops, golden fern stripes, daisy spots; Moon rabbit ear tips (rabbit mask B redrawn) | yes |
| No four-point star, nothing religious | yes |
| Glows (« A émissif ») on the fern, daisy and other mastery colors | not yet: the animal shader has no emissive mask |
