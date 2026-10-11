# T49 animation review: MoleDig, MoleEat, MoleWalk, PeekOut (SmallQuadruped) and PeekOut (MediumQuadruped)

Sources: `SourceArt/Blender/Rigs/BL_Rig_SmallQuadruped.blend` (`pw_anim_library.py`) and `SourceArt/Blender/Rigs/BL_Rig_MediumQuadruped.blend` (`pw_anim_medium.py`), exported by `pw_export.py`. Hand-keyed rotations, no capture, no generator.

| Check | Result |
|---|---|
| MoleDig, 2 s: both front paws scratch in turn (two strokes each), head low over the spot, then sniffs | yes (Blender) |
| PeekOut, 1.8 s: muzzle down towards the ground, sniffs, pops up, little shake, settles | yes (Blender) |
| MoleWalk 0.8 s loop and MoleEat 2 s loop: the Mole's low shuffle and nibble | yes (Blender) |
| Mole mesh lowest point over every frame (LOD0, LOD2, Star Nose): MoleDig +0.3 mm, PeekOut +0.6 mm, MoleWalk +0.6 mm, MoleEat +0.6 mm | yes, nothing below the ground |
| Paw bones and the Rabbit preview over every frame: PeekOut, MoleWalk, MoleEat never below rest; MoleDig 0.3 mm under rest, still 2 mm above the ground | yes |
| Fawn PeekOut: muzzle down to 25 cm (Eat stops at 22 cm), hooves never move | yes |
| Upper legs only swing forward, Chest only lifts, Root only rises (safe for every species of each family) | yes |
| To confirm in Unity: the clips on Rabbit, Hedgehog, Guinea Pig, Field Mouse, Fawn | to capture |

## T49 fix: the Mole's sleep, startle and happy clips (SmallQuadruped)

The family Sleep (-6.7 cm, star nose), ReactTouch (-1.9 cm, mittens) and Happy_Bounce (-5.6 mm) took the Mole below the ground. The Mole now plays its own clips (AnimalPipeline `MoleClips`); the other species keep the family ones.

| Check | Result |
|---|---|
| MoleSleep_Enter 0.8 s, _Loop 4 s, _Exit 0.6 s: the mitten hands come forward and in beside the chin, the head lowers and turns onto them, the hind legs splay so the belly rests on the ground; slow breathing, one ear twitch, a little hand twitch; a stretch to wake | yes (Blender) |
| MoleReact 0.6 s: a small startled lean back, the head rises with a tilt, both hands come up; Root only rises | yes (Blender) |
| MoleHappy 1 s: a happy wiggle, the hands wave in turn, the chest sways, the head bobs; hind feet planted (bone heads fixed every frame) | yes (Blender) |
| Same lengths as Sleep_Enter, Sleep_Loop, Sleep_Exit, ReactTouch and Happy (behavior timings unchanged) | yes |
| Lowest vertex over every frame, LOD0 and Star Nose LOD0: MoleSleep +2.1 mm, MoleReact +2.6, MoleHappy +2.6, MoleWalk +2.6, MoleEat +2.6, MoleDig +2.4, PeekOut +0.9, Idle_A +2.6, Idle_B +2.6, Social_Nose +2.6 | yes, nothing below the ground |
| Same on LOD1, LOD2 and Star Nose LOD1: MoleSleep +0.1 mm, MoleDig +0.3, Social_Nose +0.5, every other clip +0.6 (LOD2 rests 0.6 mm above the ground) | yes |
| MoleWalk and MoleEat loop in Unity (AssetImportRules looping markers) | yes |
| To confirm in Unity: the Mole sleeping, startled and happy in the refuge | to capture |
