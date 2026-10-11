# T49 art review: ACC_OverallsBib (O-OUT-103-07, « Salopette de jardinier », strap junction)

Sheet: `Docs/Captures/Review/T49_NapePieces_sheet.png` (Blender preview on the Field Hamster with ACC_OverallsStraps,
three-quarter view from above like the game camera, right); to capture in Unity on 3 species.
Sources: `SourceArt/Blender/Accessories/BL_Accessories.blend` built by `pw_accessories.py` (build_overalls_bib),
exported by `pw_export.py`. Palette table (default and gold rows; the outfit uses the default row) in the
`pw_accessories.py` docstring. The object and FBX keep the name ACC_OverallsBib.

| Check | Result |
|---|---|
| Soft, rounded, handmade: the overalls' strap junction at the nape, a soft denim patch with a darker inset panel, two round buttons where the straps meet, two strap stubs leaving toward the shoulders where the crossed straps of the back begin (same strap thickness and denim as ACC_OverallsStraps) | yes (Blender) |
| Redesign: the chest bib failed on every species (the chin and front legs cross the chest in the eat, sleep and inspect poses); the nape and back read best from the camera above | yes |
| Budget: 280 tris (medium 150-500, asked under 400), 0.074 x 0.059 x 0.015 m on the hamster | yes |
| Socket space: origin at SOCK_Neck, +Z up, -Y forward, no rotation or scale; rest axis Up (removed from AccessoryFitPipeline.ForwardPieces) | yes |
| Fit on the Field Hamster at rest: lifted 6 mm to keep 3 mm over the skin and ears for every vertex and face center; underside on the nape surface | yes |
| Headless prediction at rest (margin = 12 mm x scale minus mean gap, 3 mm wanted): hamster 10.2, mole 8.3, guinea pig 5.8; field mouse, hedgehog, fawn, rabbit, squirrel none (same causes as the scarf) | 3 species |
| Prediction over every clip pose (the Unity search): hamster s1.00 margin 9.7 clearance 3.7 mm, mole s0.85 8.1 / 2.6, guinea pig s0.70 5.0 / 3.3, field mouse s0.65 1.8 / 3.3 (tight) | 4 species |
| Shared with ACC_OverallsStraps (straps: field mouse, guinea pig, fawn, hamster, mole): hamster, mole, guinea pig, and the field mouse when its tight fit holds | 3 to 4 species |
| Head-down poses (eat, sleep, inspect, dig) with those fits: the head stays 22 mm away on the hamster, never near on the mole, 4 mm on the guinea pig, 3 mm on the mouse | yes |
| Regions for one shared material: base denim, R buttons, G inset panel | yes |
| No four-point star, nothing religious | yes |
