# T50 art review: Forêt Magnifique (kit, grounds, chunks, moods)

Sheets: `T50_vegetation_sheet.png` and `T50_ground_props_sheet.png` (Blender previews of the kit), `T50_forest_zones_1.png`
and `T50_forest_zones_2.png` (Unity, Magnifique, Soleil, noon: the Grassland seam, Entry, Undergrowth, Clearing, Hollow,
Brook, Mushroom Corner, Toward the Lake), `T50_forest_moods_tiers.png` (Clearing: morning, evening, Récolte, then noon on
Magnifique, Équilibrée, Fluide). Captures from `T50CapturePlayTests` (explicit).
Sources: `SourceArt/Blender/Tools/pw_forest.py` (BL_Biome_Forest_Magnifique.blend: trees, landmarks, bushes, ferns,
mushrooms), `pw_forest_ground.py` (BL_Biome_Forest_Ground.blend: grounds, brook, rocks, props, scatter; it also rebuilds the
Grassland ForestEdge ground in BL_Biome_Grassland_Magnifique.blend), Unity `Editor/Environment/ForestPipeline*.cs`
(prefabs, kits, layouts), palettes and moods in `BiomeWizard.ForestSpec` and `ForestPipeline.TunePalettes/TuneMoods`.

| Check | Result |
|---|---|
| Four clearly different trees (broad oak, tall soft fir in rounded tiers, leaning, twin birch), lobed crowns with leaf scales, no blob, no spike | yes |
| Landmarks with character: the Ancient Tree (owl branch) and the Giant Stump with its little round door, solid in game | yes, after the sun-shaft fix |
| Ferns with round lobes and blunt tips, mushrooms with smooth domed caps and round dots, round flowers on the bush | yes, after the polish pass |
| Cozy, never dark: first pass read olive-brown and gloomy; floor, palettes and moods brightened (noon floor about (142,160,90) sRGB against the Grassland (122,164,100)) | yes, after one retune |
| Framing like the Grassland: big trees only on the back and near rims and corners, the middle open for animals, a readable path | yes (guarded by ForestChunkTests) |
| Grassland to Forest seam at x 105: vertex-identical, the Forest starts as the Grassland meadow and turns to moss along a noisy line (top color difference 5.5/255); no straight band | yes, after one fix |
| Zones read: fern-lined Entry, Undergrowth, sunny Clearing with the hero, Hollow with the stump, Brook with the bridge and stepping stones, Mushroom Corner ring, Toward the Lake thinning out | yes |
| Morning mist, warm evening; no clock night (the night mood is for events) | yes |
| Équilibrée at least 85 % of Magnifique, Fluide keeps the identity | yes |
| Shared materials only, palettes by vertex regions, no new texture; budgets (trees 1.4-1.75k, hero 2.2k, stump 1.4k) | yes |
| No four-point star, nothing religious, handmade (own Blender Python, no library model) | yes |
