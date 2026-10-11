# T49 art review: Landmark_Burrows_RootBurrow (E-LMK-EVT-burrows, « Grand terrier aux racines », gold T30)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Landmark_Burrows_RootBurrow.fbx`.
Proposed palette row `palette.event_burrows.root_burrow` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A big lumpy burrow mound with moss creeping over the top and six moss cushions, a round dark entrance under a thick rounded root arch tied to the top of the mound, two roots down the front flanks and two creeping down the back, six big sprouts, nine round dew beads | yes (Blender) |
| Size 2.711 x 2.406 x 1.813 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 2058 tris (under 2,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base earth, R moss and sprouts, G roots, B dew; the mouth shade is baked occlusion; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
