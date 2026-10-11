# T49 art review: Decor_Burrows_SeedlingPots (E-DEC-EVT-burrows-06, « Pots de semis », shop 500)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_SeedlingPots.fbx`.
Proposed palette row `palette.event_burrows.seedling_pots` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: Three terracotta pots of three sizes on a soft wooden tray, sprouts with two and four round leaves, a little cream label, two dew beads | yes (Blender) |
| Size 0.5 x 0.21 x 0.256 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 956 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base terracotta, R sprouts, G soil, B dew, A tray and label; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
