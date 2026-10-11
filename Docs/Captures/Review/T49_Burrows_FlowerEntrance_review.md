# T49 art review: Decor_Burrows_FlowerEntrance (E-DEC-EVT-burrows-01, « Entrée de terrier fleurie », track T3, gold version at the same tier)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_FlowerEntrance.fbx`.
Proposed palette row `palette.event_burrows.flower_entrance (and gold)` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A lumpy earth mound with moss creeping over the top, a round dark burrow mouth framed by an arch of seven round daisies and six soft leaves, moss cushions, two sprouts, three round dew beads | yes (Blender) |
| Size 0.718 x 0.593 x 0.389 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 1020 tris (under 1,500) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base earth, R moss and leaves, G daisy hearts, B dew, A petals; the mouth shade is baked occlusion; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
