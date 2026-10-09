# Pocket Wilds: T02 storyboard

T02 answers the owner's notes on T01: more animation, better sound, show more of the game, a livelier edit, and more pull.
It is many short real moments cut on the beat of our upbeat activity music, with a hook in the first second.

## Format

- **Main cut:** `PocketWilds_T02.mp4`, 25.17 s (47 beats of 112 BPM).
- **Short cut:** `PocketWilds_T02_15s.mp4`, 15.00 s (7 bars).
- **Both cuts:** 1080 × 1920, 30 fps, H.264 High with yuv420p in bt709, AAC 192 kbps, about −14 LUFS, true peak about −2 dBTP.
- **Source:** only real game captures, from two Unity runs of `MarketingCapture` (plan "T02") plus the T01 capture. Only our music, our sounds and our fonts.
- **Text:** English only, never over a face, inside TikTok's safe area.
- **Loop:** the last frame is the new egg waiting on its pedestal, and the first frame is eggs cracking, so the video loops cleanly.

## Music

- **Track:** `MUS_Activity_HurriedPaws` (112 BPM, F major, bouncy marimba and kalimba). One beat lasts 0.536 s and one bar 2.14 s.
- **Main cut:** the music starts at bar 2. Two bars of lift (Bb, C) run under the hook, and the A section's melody enters at 4.29 s. The Gm7 → C → **F cadence lands at 21.43 s, when the title pops**. The music then fades out under the end card.
- **Short cut:** the music starts at bar 6, so the same cadence lands at 12.86 s with the title.
- **Cuts:** every cut falls on a downbeat (a clip lasts one bar) and every key sound sits on a beat: the burst pop at 0.53 s, the Rare pop at 2.67 s, the « coucou » at 5.37 s, the treat at 11.25 s.

## Beats (main cut)

| Beats | Time (s) | Clip (capture) | What happens | Edit | Caption |
|---|---|---|---|---|---|
| 0–4 | 0.00–2.14 | × 10 hatch (T02, run 1) | **Opens on the eggs cracking.** Ten eggs burst on beat 1 (0.53 s), then the newborns tumble out. | punch-in on the pop | "Hatch tiny friends" |
| 4–8 | 2.14–4.29 | Rare hatch (T02, run 1) | The predicted spotted rabbit (Rare) pops out in its glow and round glints, then hearts. | punch-in on the pop | "Every one / is different" |
| 8–12 | 4.29–6.43 | Hide and Seek (T01) | The mushroom hedgehog peeks over tall grass, then the « coucou » hop and its heart. | the music ducks deep for the « coucou » | "Can you find them?" (gone before the heart) |
| 12–16 | 6.43–8.57 | Grooming (T02, run 2) | Brush strokes on the mushroom hedgehog: foam, the dust fading. | 1.6× digital push on the set | "Brush them" |
| 16–20 | 8.57–10.71 | Grooming (T02, run 2) | The pink bow flies onto the head, then a heart. | the music ducks deep | "Brush them" (held) |
| 20–24 | 10.71–12.86 | Treat (T01) | The sakura bunny eats among daisies; the treat hearts rise. | | "Spoil them" |
| 24–28 | 12.86–15.00 | Greeting (T02, run 2) | The white bunny and the red squirrel side by side, faces to camera. | | "Make friends" |
| 28–32 | 15.00–17.14 | Zone opening (T02, run 2) | The hedge sinks in a puff of petals, then the camera cranes over the new zone and its stream. | | "Watch your / meadow grow" |
| 32–36 | 17.14–19.29 | Meadow (T01) | Rabbits, a squirrel and a hedgehog around a mossy rock. | | (held) |
| 36–47 | 19.29–25.17 | End loop (T02, run 1) | High over the herd, then down to the new egg on its pedestal. | the background blurs softly from 21.2 s | "Pocket Wilds" with a pink paw at **21.43 s, on the cadence**; then "Coming soon to / Google Play & the App Store" |

**Every clip moves.**

- Each clip carries a digital camera on top of its in-game camera move (push-ins, orbits, cranes, follows).
- A micro-zoom pop marks each cut, and the two hatches get a punch-in on their pop.
- Captions are 92 px Pocket Plush, cream with a warm outline, popping in with a bouncy overshoot (0.3 s).

## The 15 s cut

| Beats | Clip | Caption |
|---|---|---|
| 0–4 | × 10 burst | "Hatch tiny friends" |
| 4–8 | Rare reveal | "Hatch tiny friends" (held) |
| 8–12 | Hide and Seek | "Can you find them?" |
| 12–16 | Grooming bow | "Brush and / spoil them" |
| 16–20 | Treat | "Brush and / spoil them" (held) |
| 20–28 | End loop | Title on the cadence at 12.86 s, then the stores line |

## Sound

- **Layers:** the game's own sounds sit over the music, cut with each clip (8 ms fades at the cuts): egg cracks, the pop, happy voices, the « coucou », the brush, the bow, the treat and the zone opening.
- **Ducking:** the music dips by 6 dB under each key sound.
- **Quiet moments:** for the « coucou » and the bow it dips by 13 dB for half a second, so the sound breathes.
- **Loudness:** a two-pass loudnorm to −14 LUFS. The true peak is measured again after AAC and corrected (−1.9 to −2.0 dBTP).

## How to run

1. **Capture (Unity):**
   - `MarketingCapture.Begin("{\"video\":\"T02\",\"plan\":\"T02\",\"refugeLevel\":25,\"renewals\":1,\"light\":1000000000000.0,\"hatchMinRarity\":2}")`
   - What it captures:
     - the seeded save is at refuge level 25 with one Renewal, so × 10 hatching is unlocked;
     - the Light covers the eggs and the zone;
     - the first hatch is a Rare rabbit.
   - About 2 minutes once the game runs. Entering Play Mode takes about 4 minutes in this editor.
2. **Edit:** `python Tools/Marketing/make_cut.py --video T02` renders both cuts, their contact sheets and `build/report.json`.
   - `edit.json` lists the sources (two T02 runs and T01), the clips (shot, beats, the event put on a beat, the digital camera), the captions in beats and the end card.

## Notes for the next videos

- **Rain and the rainbow:** both were captured, but the drops and the arc do not read at this distance. Not used.
- **Evening nap:** the sleeping rabbit was only seen from behind. Not used.
- **× 10 in run 2:** the hatch quantity did not switch to × 10, so the burst comes from run 1.
