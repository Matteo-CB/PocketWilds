# T48B art review: the refuge's seasons (DEF_SeasonLooks, drift, fireflies, season visitors)

Sheets (Unity captures, Magnifique, French, from `SeasonPlayTests.Capture_Seasons_Fr`):
`T48B_seasons_sheet.png` (Éveil, Soleil, Récolte, Givre at midday), `T48B_mornings_sheet.png` (Éveil, Récolte and Givre
mornings, Soleil evening with fireflies), `T48B_ui_sheet.png` (season card, weekend chip, weekend sheet).
Sources: palette transforms and light tints in `Assets/_Game/Editor/Environment/SeasonLookPipeline.cs` (hand-tuned
numbers, rebuilt from the base Grassland palettes by « Pocket Wilds/World/Build Season Looks (T48B) »); drift and
firefly shaders `SH_SeasonDrift.shader`, `SH_Firefly.shader`; the shared quad `MSH_SeasonQuad` (Baked/Weather, made by
the pipeline); visitor palettes in `AnimalPipeline.cs` (variant.fawn.white_visitor, variant.squirrel.sun_visitor).

| Check | Result |
|---|---|
| Soleil is the validated look, untouched (no palette row, white light tints) | yes |
| Éveil: fresher, greener lawn and crowns, flowers a little more vivid, round pink and cream petals drifting, showers more often | yes |
| Récolte: golden and amber crowns, warmer golden grass with ochre tips, golden mist in the morning, round amber leaves falling. First pass read olive and muddy: retuned (warmer hue, more saturation and light) | yes, after one retune |
| Givre: pale frosted greens with blue-white tips, cooler light, pink morning sky, fine round snow, no rain nor rainbow. First pass read mint teal: retuned (less hue shift, more frost white) | yes, after one retune |
| Only vegetation and ground greens change; rocks, water, wood, props, animals and UI keep their colors (SeasonLooks_ChangeOnlyVegetationGreens) | yes |
| Fireflies: round soft glows, Soleil evenings and nights only, steered around every volume (Fireflies_SoleilEvening_NeverInsideAVolume) | yes |
| Drift: one instanced draw, round shapes only (petals, leaves, snow), fades out above the ground, 160 / 112 / 64 instances by tier | yes |
| Visitors: White Fawn (white coat, soft pink ears) and Sun Squirrel (sunny orange, cream belly) read clearly beside the Meadow Rabbit; palette only, no new mesh | yes |
| Season card and weekend sheet: titles and lines keep their margins in French and English (SeasonCardTitle_FitsTheModal_InEveryLanguage) | yes |
| No four-point star, nothing religious, no real-holiday imagery | yes |
| Hand-tuned, not generated: every color is a number in the pipeline, every shape a few lines of shader | yes |
