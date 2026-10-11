# T47 art review: AN_SmallQuadruped_FieldHamsterGreet and AN_SmallQuadruped_FieldHamsterStash

Sheet: to capture in Unity (frame strips on the Field Hamster, the Rabbit and the Squirrel).
Sources: `SourceArt/Blender/Rigs/BL_Rig_SmallQuadruped.blend` built by `pw_anim_library.py`, exported by `pw_export.py`
(`AN_SmallQuadruped.fbx`, 15 clips). Keyed by hand, 30 fps.

| Check | Result |
|---|---|
| Greet: 30 frames (1.0 s), one-shot, a lean back, one paw lifts and waggles, a bright nod, ears perk | yes |
| Stash: 72 frames (2.4 s), one-shot, sniffs, picks a seed with both paws, tucks it in two pushes, happy wiggle | yes |
| Cheek puff: Head bone scale only (7 % wider, 3 % lower at most), no shape key, no new bone, back to 1 at the end | yes |
| Existing bones only; every clip keys the Head scale at 1 | yes |
| Paws never below their rest height (measured on every frame: min 0.016 m, rest 0.016 m) | yes |
| Safe on every SmallQuadruped (rotations within the Eat, Inspect and Happy ranges) | yes (rig), to watch on the Rabbit ears in Unity |
| Nothing enters anything; no four-point star, nothing religious | yes |
