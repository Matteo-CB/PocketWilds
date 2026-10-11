# T49 art review: Decor_Burrows_DewMushrooms (E-DEC-EVT-burrows-03, « Champignons de rosée », track T13, gold version)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_DewMushrooms.fbx`.
Proposed palette row `palette.event_burrows.dew_mushrooms (and gold)` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: Three leaning round mushrooms on a moss tuft, domed coral caps with round cream spots, a round dew bead on each cap, a sprout and two beads in the moss | yes (Blender) |
| Size 0.356 x 0.294 x 0.394 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 1047 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base caps, R moss and sprout, G stems and gills, B dew, A spots; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
