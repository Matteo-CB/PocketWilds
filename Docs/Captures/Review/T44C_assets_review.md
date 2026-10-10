# T44C assets review (Journal du refuge et visiteurs rares)

Sheet: `Docs/Captures/Review/T44C_assets_sheet.png` (ICO_Journal, UI_Visitor_Badge, UI_Journal_PageCard, UI_Journal_MonthTab).
Sources: `SourceArt/UI/Tools/pw_ui_sprites_t44c.py` (masters `SourceArt/UI/Masters/<name>@4x.png`), `SourceArt/Audio/Tools/pw_audio_sfx.py`
(T44C section, masters in `SourceArt/Audio/Masters/SFX`), palettes in `Editor/Animals/AnimalPipeline.cs`.

| Asset | Source | Handmade | No four-point sparkle | Nothing religious | Readable at size | Notes |
|---|---|---|---|---|---|---|
| ICO_Journal 128 | script, SDF shapes | yes | yes (paw, corners) | yes | yes at 64 px | scrapbook, paw photo, pink photo corners |
| UI_Visitor_Badge 96 | script | yes | yes (heart) | yes | yes | round tag with a tail pointing at the head |
| UI_Journal_PageCard 256x320 | script | yes | yes | yes | n/a | Sliced 48 px; tape in the top left border |
| UI_Journal_MonthTab 192x64 | script | yes | yes | yes | yes | Sliced 28/24/28/24; text goes in the middle |
| SFX_World_VisitorArrive (J-SFX-FEEL-018) | recipe | yes | n/a | yes | -21 LUFS scale, 1.2 s | five paw steps, three wooden notes A C F |
| SFX_UI_JournalPage | recipe | yes | n/a | yes | -24, 0.5 s | paper rustle and felt tap, no note |
| VFX VisitorTrail, VisitorArrive | pipeline | yes | round dot, ring | yes | 8 + 1 particles | far under the 350 cap |
| Variants rabbit_meadow, rabbit_snow, hedgehog_flower visitors | palettes | yes | n/a | yes | to judge in Unity | secret flag: never in eggs |

Open: judge the three palettes on the models in Unity (capture), then tick the grid.
