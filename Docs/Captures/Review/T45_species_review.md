# T45 art review (Field Mouse and Guinea Pig)

Sheet: `T45_species_sheet.png` (the 20 colors as Codex portraits: field mouse on top, guinea pig below; base colors,
then the mastery colors 10, 20, 30, then the secret color). Stickers: `TestResults/Captures/T45_stickers.png`
(STK-GL-04, 05, 08, 09, 11).
Sources: `SourceArt/Blender/Animals/FieldMouse/BL_Animal_FieldMouse.blend` and `GuineaPig/BL_Animal_GuineaPig.blend`,
built by `pw_species.py` (SPECS FieldMouse and GuineaPig: metaball volumes, QuadriFlow body, bead eyes and nose, the
shared SK_SmallQuadruped skeleton), `pw_masks.py` (regions and pattern masks), `pw_export.py`. Palettes in
`Editor/Animals/AnimalPipeline.cs`. No generator, library model or scan.

| Check | Result |
|---|---|
| Same family as Rabbit, Squirrel and Hedgehog (bead eyes, soft volumes, flat warm colors) | yes |
| Readable silhouettes at Codex size: big round ears and a thin tail curled up like a question mark (mouse), potato body and blunt face (guinea pig) | yes |
| Budgets: LOD0 2914 / 1960 tris, LOD1 898 / 856, LOD2 432 / 410, 3 influences (small animals: 1.8k to 3k) | yes (Hedgehog: 2406 / 1094 / 443) |
| Same skeleton, every family clip plays (head, ears on the head, tail on half the tail bones, legs) | yes (pose check in Blender) |
| Nothing enters anything: bodies measured from the meshes (mouse 0.53 m long with its curled tail, guinea pig 0.46 m) | yes (MeasureBodies) |
| Patterns: piebald and tricolor patches, lilac spots, golden wheat back stripe, Dutch belt and blaze | yes |
| Guinea pig ears in a darker tone of the coat (pink tiny ears read as a patch) | yes, changed after the first capture |
| No four-point star, nothing religious | yes |
| Glow on the tail tip and ears (doc « A émissif ») | not yet: the animal shader has no emissive mask; palettes only |
