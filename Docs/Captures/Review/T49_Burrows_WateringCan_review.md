# T49 art review: Decor_Burrows_WateringCan (E-DEC-EVT-burrows-04, « Arrosoir », track T18, gold version)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_WateringCan.fbx`.
Proposed palette row `palette.event_burrows.watering_can (and gold)` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A round-bellied tin watering can with soft bands, a curved spout and a round brass rose, a smooth loop handle, a raised daisy on its side, two round dew beads | yes (Blender) |
| Size 0.247 x 0.47 x 0.341 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 788 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base tin, R handle, rims and bands, G brass rose and daisy heart, B dew, A petals; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
