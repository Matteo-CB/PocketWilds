# T49 art review: Decor_Burrows_GardenFence (E-DEC-EVT-burrows-12, « Clôture de jardin », Coffret)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_GardenFence.fbx`.
Proposed palette row `palette.event_burrows.garden_fence` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: Six cream pickets with round tops, two rails, two posts with round knobs, a leafy vine on a post, three daisies, two sprouts | yes (Blender) |
| Size 1.364 x 0.143 x 0.64 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 864 tris (under 2,000) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base pickets, R leaves and sprouts, G posts and rails, B hearts, A petals; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
