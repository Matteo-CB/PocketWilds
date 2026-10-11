# T49 art review: ACC_MinerHelmet (O-OUT-103-05, « Casque de mineur doré »)

Sheet: `Docs/Captures/Review/T49_props_sheet.png` (Blender preview on the Field Hamster); to capture in Unity on 3 species.
Sources: `SourceArt/Blender/Accessories/BL_Accessories.blend` built by `pw_accessories.py`, exported by `pw_export.py`.
Palette table (default and gold rows; the outfit uses the gold row) in the `pw_accessories.py` docstring.

| Check | Result |
|---|---|
| Soft, rounded, handmade: A rounded helmet with a soft brim wider in front, a round crest and a round decorative lamp (painted lens, no light) | yes (Blender) |
| Budget: 346 tris (medium 150-500), 0.071 x 0.067 x 0.051 m | yes |
| Socket space: origin at SOCK_HeadTop, +Z up, -Y forward; on the forehead in front of the ears, tilted 24 degrees | yes |
| Fit on the Field Hamster at rest: 3 mm clear of skin and ears for every vertex and face center (lift -12.5 mm) | yes |
| Regions for one shared material: base shell, R brim and crest, G lamp body, B lamp lens | yes |
| Fit on the other SmallQuadruped species and MediumQuadruped | fitting profile in Unity |
| No four-point star, nothing religious | yes |
