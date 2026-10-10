# T44B art review (badges, category icons, profile frames)

Sheet: `T44B_art_sheet.png` (badges, the 13 category icons over badges, the achievement button, the 42 frames around a sample portrait).
Source: `SourceArt/UI/Tools/pw_ui_sprites_t44b.py` (masters `SourceArt/UI/Masters/<name>@4x.png`), sheet script `pw_ui_review_t44b.py`.
Every shape is drawn by script (signed distances, 4x, box filtered). No generator, library model or third-party font.

| Check | Result |
|---|---|
| Same family as the HUD icons (rounded, warm outline, light top left, soft shadow) | yes |
| Star tier is a plump round five-point star, never inside a circle, never four points | yes |
| No four-point star, no religious or national symbol, no sparkle character | yes (stars are 5-point; snowflakes have six round-tipped arms) |
| Category icons readable over bronze, silver, gold and star badges (dark ink, cream edge) | yes at 64 px on a 112 px badge |
| Frames: one white ring tinted at run time, rim copy 1.06 behind, motifs on 6 fixed slots | yes, table in `Assets/_Game/Editor/Profile/FrameArt.cs` (generated) |
| Collision rule | UI sprites only; motifs sit on the ring, not on the portrait |

Open points: ring styles Double, Pearls, Bands, Gradient, Dotted, Ticks and Lines are described in FrameArt (style, count, colours) and must be drawn at run time from UI_Frame_Ring and plain shapes (the sheet shows the intended look). The Hatching glyph is the weakest at 48 px; to re-check in Unity.
