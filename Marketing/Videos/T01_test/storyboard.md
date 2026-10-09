# Pocket Wilds: T01 test video storyboard

The first test video for TikTok and Instagram. The owner judges its look before the daily series starts.

## Format

- **Size and length:** 1080 × 1920 portrait, 30 fps, 26.0 s (780 frames). H.264 high profile with yuv420p in bt709, about 14 Mbps, AAC at 192 kbps.
- **Loudness:** about −14 LUFS integrated, true peak under −1 dBTP.
- **Source:** everything comes from the real game.
  - The Prairie refuge and the real 3D animals with their own behaviour.
  - The real hatch and the real Hide and Seek game, played in Play Mode from SC_Bootstrap like a real launch.
  - Our own music (`MUS_RefugeTheme`) and the game's own sounds, rendered offline in sync with the frames.
- **Text:** English only, in our own fonts: Pocket Plush Black for the captions and the title, Pocket Round Bold for small text.
- **Project rules:**
  - no four-point star shape (only round glints, hearts and paws);
  - nothing religious;
  - only our own assets;
  - never a claim about how the game was made.

## Music and cuts

- **Music start:** `MUS_RefugeTheme` starts at 12.0 s into the track (bar 5). The theme's first phrase begins on the first frame, so the hook has a melody.
- **Tempo:** 80 BPM, so one bar lasts 3 s.
- **Cuts on the beat:**
  - 3.0 s: bar 6, downbeat;
  - 10.5 s: bar 8, beat 3;
  - 16.5 s: bar 10, beat 3;
  - 21.0 s: bar 12, downbeat.
- **Resolution:** the Csus4 → C → F/A cadence lands on the end card at **24.0 s**. The music then fades out from 24.3 s to 26.0 s.
- **Ducking:** the music dips by 7 dB under each key sound: the cracks and the pop of the hatch, the tap and the « coucou » of Hide and Seek, the treat.

## Beats

| # | Time | Frames | Shot | Camera | What happens in the game | Caption |
|---|---|---|---|---|---|---|
| 1 | 0.0–3.0 | 0–89 | **Hatch (the hook)** | Close on the waiting egg. Push in from 2.8 to 1.95 m, pitch from 17° to 12.5°, a small orbit from −11° to +5°. The egg sits at 60 % of the height. | The real hatch starts at 0.1 s: wobble, crack at 0.6 s, crack at 1.1 s, **pop at about 1.55 s**. A baby rabbit jumps out (the profile id is chosen so that the first hatch gives a rabbit), does its happy jump, and favorite hearts rise. | "Hatch tiny friends" (0.25–2.8 s) |
| 2 | 3.0–10.5 | 90–314 | **Meadow** | A slow orbit of 28° around the liveliest group, about 5.2 to 5.9 m away, pitch from 34° to 31°. The group and the angle are chosen by projecting the animals into the 9:16 frame and counting those clear of the captions and of TikTok's interface (a portrait frame is only about ±1.2 m wide at 8 m). | The animals live on their own: they hop, nibble, nap and meet. Butterflies, bees and birds fly around. Golden late-afternoon light (17:12), no rain. | "Watch them play" (3.35–6.6 s), then "in a cozy meadow" (6.85–10.25 s) |
| 3 | 10.5–16.5 | 315–494 | **Hide and Seek** | The real T39 game, played up to six times (first the free game, then practice games) until an animal hides in tall grass. The camera stays close to the zone camera's line of sight, swinging up to 36° when a prop blocks that line. It is raised a little and uses an 18° lens, so the subject fills the frame. It rests on the head, then pans with the hop and rises after the find. | The animal nibbles, hidden. The hint (a React, its head up) comes about 3.5 s into the shot. The tap follows 0.8 s later: « coucou », a heart, the hop out. | "Find who's hiding" (10.8–14.35 s, gone before the heart) |
| 4 | 16.5–21.0 | 495–629 | **Close-up** | The subject and the yaw are scored together: a clear sight line through the decor, a hedgehog first (then a squirrel, then a rabbit), eating or resting but never asleep, seen from the front. From 2.9 to 2.5 m, pitch from 41° to 37°, the animal low in the frame so its ears stay under the caption. | At 1.2 s it gets the treat from its sheet, the same action as in the game: hearts, the munch sound, a happy hop. | "Spoil them / with treats" on two lines (16.8–20.75 s) |
| 5 | 21.0–26.0 | 630–779 | **End card** | The game's own diorama view, pitch from 38° to 42°, pulling back from 7 to 9.5 m over the group that best fills the lower half (projection scoring as in shot 2). The background blurs softly (sigma 5, faded in over 0.6 s). | Life goes on in the meadow. | "Pocket Wilds" with a pink paw (our font's own picto) at 21.25 s; "Coming soon to" at 21.85 s; "Google Play & the App Store" at 22.0 s, held to the end |

### What the first capture filmed (T01 v1, 9 October 2026)

- **Hatch:** a spotted rabbit (Rare), as predicted. Pop at 1.57 s, hearts at 1.93 s.
- **Meadow:** a group of white and grey rabbits, a grey squirrel and a hedgehog around a mossy rock.
- **Hide and Seek:** the mushroom hedgehog in tall grass. Its cap peeks over the blades, its hint comes at 14.07 s, then the tap, « coucou » and the heart at 14.5 s.
- **Close-up:** the sakura rabbit eating among daisies; treat hearts at 17.7 s.
- **End card:** white, grey and black rabbits, a red squirrel and a hedgehog under the title.
- **Output:** `PocketWilds_T01_test.mp4`: 26.00 s, −14.1 LUFS, true peak −2.0 dBTP, about 12.5 Mbps H.264 High.

### Why this order

- **Hook:** the first frame already shows something about to happen (an egg pulsing in warm light), and the burst comes before 1.6 s.
- **World:** the glide shows the refuge is alive and full of different animals.
- **Play:** the hide and seek moment gives the video its "aww" beat, with a tiny payoff (the ears, the « coucou », the heart).
- **Bond:** the close-up ends on affection, right before the call to action.
- **Call to action:** the cadence of the music resolves under the title.

### Fallbacks (the capture picks them by itself and logs them)

| Situation | What the capture does instead | Caption |
|---|---|---|
| Hide and Seek is not in the build, or cannot be staged | The two closest animals stand side by side, each with a happy hop and hearts. | "Tiny best friends" |
| No treat is owned | A happy hop with hearts instead of the treat. | "Make them happy" |
| `endCard: "windmill"` (an option) | The end card rises toward the WindmillRise windmill in the golden light instead of the refuge. | unchanged |

## Captions

- **Length:** 3 to 6 words.
- **Look:** big and soft. Pocket Plush Black at 80 px, cream with a light gradient, a warm brown outline and a soft shadow.
- **Motion:** a gentle pop (0.34 s, slight overshoot, rising 18 px), then a soft fade out.
- **Safe area (TikTok's own interface):**
  - the top 10 %, the bottom 20 % and the right 12 % stay free;
  - captions are centered at 20.5 % of the height, at most 820 px wide (from x = 130 to x = 950 px);
  - the title card sits at 30 to 44 % of the height.
- **Never over the animals:** every shot frames its subject at 40 to 44 % from the bottom of the frame, below the caption band. The end card frames the herd at 30 % from the bottom.

## Capture settings (`Assets/_Game/Editor/Marketing/MarketingCapture.cs`)

**Picture**

- High tier, with adaptive quality and heat protection off.
- 2× supersampling (render at 2160 × 3840, box filtered down) plus the pipeline's MSAA.
- Light forced to 17:12, the sky kept clear.
- Every UI canvas and every wish bubble hidden.
- The store is unavailable for this boot, so no offer can pop up.

**Time and sound**

- Game time is fixed at 1/30 s per frame (`Time.captureFramerate`).
- The game's audio is rendered offline with `AudioRenderer`, in step with the frames.
- The music bed is muted in the game: our music is laid in the edit, so it can start on bar 5 and resolve on the title.

**Scratch save:** `%TEMP%/PocketWildsMarketing/T01_test/save`, never the owner's save. The capture checks this and stops if the bootstrap did not take it. The save holds:

- 13 animals:
  - 6 rabbits: white, brown, spotted, grey, sakura, black;
  - 4 squirrels: red, cream, grey, brown;
  - 3 hedgehogs: brown, mushroom, cream;
- the white rabbit and the mushroom hedgehog marked as favorites;
- two Prairie zones open and refuge level 5;
- treats for the treat moment;
- the welcome and today's rendez-vous already done, so no screen opens at boot.

## How to run

1. **Capture (Unity):**
   - Run the menu **Pocket Wilds/Marketing/Capture Test Video (T01)**, or through MCP: `PocketWilds.Editor.Marketing.MarketingCapture.Begin(null)`.
   - The call returns at once. Poll `Marketing/Videos/T01_test/capture_status.json` until its state is `done` or `failed`. A full capture takes a few minutes.
   - Quick preview (every 6th frame, no supersampling): `Begin("{\"stride\":6,\"supersample\":1}")`.
   - The capture writes `frames/%06d.png`, `game_audio.wav` and `capture_log.json`: the shots with their frame ranges and variants, the events, the hatched animal and the warnings.
2. **Edit:** `python Tools/Marketing/make_video.py`. It writes:
   - `PocketWilds_T01_test.mp4`;
   - `PocketWilds_T01_test_contact.png` (three frames: 1.9 s, 13.8 s, 23.2 s);
   - `build/report.json` (loudness, true peak, bitrate, caption sizes).
3. **Layout test without Unity:** `python Tools/Marketing/make_video.py --placeholder`. It uses stand-in frames from the project's own captures and a stand-in game track made of our own sounds.

## To judge in the first real capture

The camera numbers above are first values written from the code. They have not been seen in Unity yet. Check:

- the framing of each shot and the meadow path;
- the hide shot's distance and whether the ears really show;
- whether the hatch gives the predicted rabbit;
- the busyness of the end card's background;
- the frame orientation (`flipY`);
- the balance between the music and the game's sounds.
