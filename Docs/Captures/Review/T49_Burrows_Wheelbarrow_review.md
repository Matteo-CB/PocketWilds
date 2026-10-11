# T49 art review: Decor_Burrows_Wheelbarrow (E-DEC-EVT-burrows-05, « Brouette miniature », shop 500)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_Wheelbarrow.fbx`.
Proposed palette row `palette.event_burrows.wheelbarrow` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A rounded cream tub with a green rim, a soil heap and a sprout inside, one chunky front wheel, wooden handles with round grips, two stubby legs | yes (Blender) |
| Size 0.358 x 0.829 x 0.328 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 773 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base tub, R rim and sprout, G wood, B tire, A soil; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
