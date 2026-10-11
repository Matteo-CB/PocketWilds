# T49 art review: the Burrows event's effects and UI art

Sources: `SourceArt/UI/Tools/pw_ui_sprites_t49.py` (masters `SourceArt/UI/Masters/<name>@4x.png`), `SourceArt/UI/Tools/pw_ui_sprites_t44b.py`
(FRAMES row `frame.event_burrows_gold`, writes `Assets/_Game/Editor/Profile/FrameArt.cs`), `Assets/_Game/Editor/Vfx/VfxPipeline.cs`
(`ConfigureSproutBurst`, atlas tiles Petal, Bubble and Ring), the PeekOut clips of `pw_anim_library.py` and `pw_anim_medium.py`.
Game captures: `TestResults/Captures/T49_<Low|Medium|High>_*.png` (T49CapturePlayTests, run on request).

| Asset | Check | Result |
|---|---|---|
| ICO_Event_Pousses (128 px) | Round cream coin, tender green rim, earth mound, plump two-leaf sprout, round dew pearls; reads at 24 and 48 dp | yes (sheet) |
| ICO_Effect_SproutBurst (128 px) | Cream egg on a green patch in a ring of 6 sprouts and 6 round dew beads | yes (sheet) |
| ICO_Frame_EventBurrowsGold (128 px) | Gold ring, rounded warm roots at its foot, tender sprouts at its top | yes (sheet) |
| UI_EventBanner_Burrows (688 x 254, shown 1032 x 380) | Soft hills, 3 warm earth mounds, sprouts, round dew beads, cream flowers, warm glow; top left clear for the name and date; edges fade inside the panel corners | yes (sheet); judge in the hub capture |
| frame.event_burrows « Racines » | Warm earth ring A0663A, rounded roots bottom left and right, tender sprouts top left and right | yes (FrameArt) |
| frame.event_burrows_gold « Racines dorées » | Gold double ring F5C451 / FFE9A0, the same roots and sprouts | yes (FrameArt); judge in the profile capture |
| VFX_SproutBurst | Sprouts grow from the ground on a 0.66 m circle (outside the 0.55 m egg spot), beads roll on the grass and stop inside the 1.3 m apron; never in the egg, the newborn or a decor | to judge in the hatch captures |
| VFX_SproutBurst budget | 25 particles Medium, 13 Low, 31 High; pooled (1 soft, 2 hard); 1.6 s | yes (pipeline) |
| pose.peek_out | PeekOut state in AC_SmallQuadruped and AC_MediumQuadruped, back to Idle; no floater family | after EnsureFamilyController |
| Owner rules | Round beads and round dots only, no four-point star; nothing religious; no generator, no library art; no em dash | yes |
