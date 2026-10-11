# T49 art review: Decor_Burrows_WoodenBench (E-DEC-EVT-burrows-11, « Banc en bois », Coffret)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_WoodenBench.fbx`.
Proposed palette row `palette.event_burrows.wooden_bench` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A rounded plank bench (three seat planks, two back planks a degree off), stubby legs, curled armrests with round knobs, a sprout tuft and a daisy underneath | yes (Blender) |
| Size 0.972 x 0.499 x 0.677 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 1182 tris (under 2,000) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base planks, R sprouts, G legs and arms, B daisy heart, A petals; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
