# T49 art review: Decor_Burrows_GardenShed (E-DEC-EVT-burrows-09, « Cabane de jardin », Coffret)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_GardenShed.fbx`.
Proposed palette row `palette.event_burrows.garden_shed` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A small wooden shed with soft rounded walls and corner posts, a rounded green roof and ridge, a rounded-top cream door with a round knob, a round window with a flower box and three daisies, a wooden step, sprouts | yes (Blender) |
| Size 1.168 x 1.164 x 1.267 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 1198 tris (under 2,000) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base boards, R roof, sprouts and leaves, G frame wood, B window, A door, window frame and petals; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
