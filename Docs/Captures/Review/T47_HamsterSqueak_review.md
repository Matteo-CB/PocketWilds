# T47 sound review: SFX_Animal_HamsterSqueakHappy (bank HamsterSqueakHappy)

Sources: `SourceArt/Audio/Tools/pw_audio_sfx.py` (hamster_squeak_happy, formant voice synthesis), rendered by
`pw_audio_build.py sfx SFX_Animal_HamsterSqueakHappy`. Masters `SourceArt/Audio/Masters/SFX/` (48 kHz 24-bit),
runtime `Assets/_Game/Art/Audio/SFX/` (48 kHz 16-bit). No recording, no generator.

| Check | Result |
|---|---|
| Two variants: a rising squeak after a nose puff; a quick double squeak | yes |
| Length 150 to 300 ms: 257 ms and 260 ms; mono | yes |
| Level: -21 LUFS momentary like the small family chirp, Animals group | yes |
| Reads apart from SFX_Animal_Small_Chirp (higher formants) | to hear in Unity |
| Cooldown 6 s | Unity audio definition |
