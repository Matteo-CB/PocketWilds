# T49 art review: Decor_Burrows_Souvenir (E-DEC-EVT-burrows-13, « Souvenir », track T5, dated: the date is data drawn in Unity over the blank cream face)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_Souvenir.fbx`.
Proposed palette row `palette.event_burrows.souvenir` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A round wooden plaque with a soft rim on a stake planted in an earth mound, a flat blank cream face, a sprout growing on the top edge, three sprouts and two dew beads | yes (Blender) |
| Size 0.28 x 0.236 x 0.6 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 662 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base wood, R sprouts, G earth, B dew, A plaque face; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
