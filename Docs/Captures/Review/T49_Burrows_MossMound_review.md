# T49 art review: Decor_Burrows_MossMound (E-DEC-EVT-burrows-02, « Monticule de mousse », track T8, gold version)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_MossMound.fbx`.
Proposed palette row `palette.event_burrows.moss_mound (and gold)` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A soft low mound under five uneven moss cushions (sunlit tops lighter), three sprouts, four round dew beads, two round pebbles | yes (Blender) |
| Size 0.663 x 0.543 x 0.341 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 775 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base earth, R moss, G sunlit moss and sprouts, B dew, A pebbles; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
