# T49 art review: ACC_GardenerScarf (O-OUT-103-04, « Foulard de jardinier »)

Sheet: `Docs/Captures/Review/T49_NapePieces_sheet.png` (Blender preview on the Field Hamster, three-quarter view from
above like the game camera, left); to capture in Unity on 3 species.
Sources: `SourceArt/Blender/Accessories/BL_Accessories.blend` built by `pw_accessories.py` (build_gardener_scarf),
exported by `pw_export.py`. Palette table (default and gold rows; the outfit uses the default row) in the
`pw_accessories.py` docstring.

| Check | Result |
|---|---|
| Soft, rounded, handmade: a scarf knotted at the nape, a soft round knot, two little folds tucked into it at the sides, two short tails lying back over the shoulders with a leaf print (R islands) and cream hems | yes (Blender) |
| Redesign: the chest version failed on every species (the chin and front legs cross the chest in the eat, sleep and inspect poses); the nape reads best from the camera above | yes |
| Budget: 280 tris (medium 150-500, asked under 400), 0.047 x 0.047 x 0.015 m on the hamster | yes |
| Socket space: origin at SOCK_Neck, +Z up, -Y forward, no rotation or scale; rest axis Up (removed from AccessoryFitPipeline.ForwardPieces) | yes |
| Fit on the Field Hamster at rest: lifted 6 mm to keep 3 mm over the skin and ears for every vertex and face center; underside on the nape surface | yes |
| Headless prediction at rest (margin = 12 mm x scale minus mean gap, 3 mm wanted): hamster 10.0, mole 8.4, guinea pig 6.0; field mouse 0.2 (rests on the back of its head on a 40 degree slope); hedgehog (spine saddle), fawn (nape under the lift range), rabbit and squirrel (head above the nape) none | 3 species |
| Prediction over every clip pose (the Unity search): hamster s1.00 margin 10.2 clearance 2.7 mm, mole s0.85 7.9 / 2.8, guinea pig s0.65 5.6 / 3.3, field mouse s0.65 3.3 / 3.8 | 4 species |
| Head-down poses (eat, sleep, inspect, dig) with those fits: the head stays 17 mm away on the hamster, never near on the mole, 4 mm on the guinea pig and the mouse | yes |
| Regions for one shared material: base fabric, R leaf print, G hems | yes |
| No four-point star, nothing religious | yes |
