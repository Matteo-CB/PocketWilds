# T49 art review: Decor_Burrows_WormCushion (E-DEC-EVT-burrows-08, « Coussin ver de terre », shop 800)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_WormCushion.fbx`.
Proposed palette row `palette.event_burrows.worm_cushion` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A plush ring cushion shaped like a round little earthworm: nine soft stuffed rings, a lifted round head poking out with bead eyes, blush cheeks and a smile, three cream tufting buttons; pastel, never gross | yes (Blender) |
| Size 0.461 x 0.514 x 0.222 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 728 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base plush, R soft rings, G buttons, B eyes and smile, A cheeks; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
