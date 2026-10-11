# T49 art review: Decor_Burrows_Greenhouse (E-DEC-EVT-burrows-10, « Serre miniature », Coffret)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview, gold versions in the last row); to capture in Unity.
Sources: `SourceArt/Blender/Events/BL_Event_Burrows.blend` built by `pw_event_burrows_props.py`, exported by
`pw_export.py` to `Assets/_Game/Art/Events/Burrows/Models/Decor_Burrows_Greenhouse.fbx`.
Proposed palette row `palette.event_burrows.greenhouse` in `PALETTES` (docstring table).

| Check | Result |
|---|---|
| Soft, rounded, handmade: A small arched greenhouse on a wooden knee wall, cream rounded ribs, a rounded door frame, a round finial, a planter with three sprouts, a step and two terracotta pots | yes (Blender) |
| Size 1.248 x 1.044 x 1.35 m, pivot at the footprint center on the ground, front toward -Y | yes |
| Budget: 1365 tris (under 2,000) | yes |
| Flat bottom: lowest vertex at z = 0, nothing under the ground, one simple footprint | yes |
| Regions: base wood, R sprouts, G terracotta and finial, B glass (opaque mint), A frame; uv occlusion and height | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
