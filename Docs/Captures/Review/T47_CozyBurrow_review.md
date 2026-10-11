# T47 art review: Decor_CozyBurrow (decor.cozy_burrow, « Terrier douillet »)

Sheet: to capture in Unity (refuge, next to the Field Hamster).
Sources: `SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_T47.blend` built by `pw_grassland_t47_props.py`
(helpers of `pw_grassland.py` and `pw_grassland_t39_props.py`), exported by `pw_export.py` to `GL_Props_T47.fbx`.
Proposed palette row `palette.grassland.cozy_burrow` in `T47_PALETTES`.

| Check | Result |
|---|---|
| Soft mossy earth mound, round dark entrance, tiny wooden frame, moss cushions, five round daisies | yes (Blender) |
| Footprint 1.13 x 0.98 m (about 1.2 x 1.0), 0.44 m tall, pivot at the base center, entrance toward -Y | yes |
| Budget: 996 tris (under 1500) | yes |
| Regions: base earth, R moss, G honey wood and daisy hearts, B entrance shade, A petals; uv occlusion | yes |
| Nothing enters anything (decor footprint, animals walk around it) | AssetValidator and CollisionRuleTests in Unity |
| No four-point star, nothing religious | yes |
