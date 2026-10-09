# Google Play listing (draft)

Status: draft, English, written on 9 October 2026 for the working title "Pocket Wilds". Every limit below was checked against the official Play Console Help pages listed at the end. Character counts were measured with a script (spaces and punctuation included).

Before publishing, check two things:

1. **Final name.** "Pocket Wilds" is still a working title. The Android package name becomes permanent with the first upload to Play Console, even for an internal test.
2. **Content of the build.** Every sentence must describe what the uploaded build really contains. Lines marked [CHECK] depend on what ships at launch (species, activities, purchases).

---

## 1. Text fields

### 1.1 App name (limit 30 characters)

| Option | Text | Count |
|---|---|---|
| A (recommended) | Pocket Wilds: Cozy Animal Game | 30 |
| B | Pocket Wilds: Cute Pet Refuge | 29 |
| C | Pocket Wilds: Hatch Cute Pets | 29 |

Why A: "cozy" and "animal game" are the two phrases people actually type, and the name is the strongest search field on Play. B is the fallback if the owner prefers "refuge" (the game's own word).

Metadata rules that apply to the name (Play Metadata policy):
- no emojis, emoticons or repeated special characters;
- no ALL CAPS unless it is part of the brand;
- no words about ranking or price ("#1", "Best of Play", "Popular", "Free for limited time");
- 30 characters or less.

### 1.2 Short description (limit 80 characters)

| Option | Text | Count |
|---|---|---|
| A (recommended) | Hatch cute baby animals, brush them, play hide and seek and grow a cozy refuge. | 79 |
| B | Hatch bunnies, hedgehogs and squirrels, then care for them in a cozy meadow. | 76 |

A carries five search phrases (hatch, cute baby animals, brush, hide and seek, cozy refuge) in one natural sentence.

### 1.3 Full description (limit 4,000 characters)

Count: 2,467 characters. Plain text, short lines, simple dashes for lists (no emojis, no ALL CAPS headings, no keyword lists).

```
Welcome to Pocket Wilds, a cozy animal game about little lives in a big meadow. Hatch an egg, meet a new baby animal, and watch your refuge grow, one gentle day at a time.

Tap the egg. It wobbles, cracks and pops, and a new friend blinks up at you. Bunnies, squirrels and hedgehogs come in many colors. Each one wanders, nibbles the grass, naps in the sun and hops over to say hello.

Hatch and collect
- Hatch eggs one at a time, or several at once, and welcome every newborn to the refuge.
- Discover new colors and rarer animals, from Common to Legendary and beyond.
- The odds of every egg are shown in the game, and eggs are hatched with Light you earn by playing.
- Fill your Codex with every species and color you find.

Grow a cozy refuge
- Your animals make Light. Spend it on upgrades and open the meadow zone by zone.
- When a hedge sinks, the color flows back into the new zone and your animals hop in to explore.
- The light follows your day, from soft morning to golden evening, with gentle rain and the odd rainbow.

Play with your animals
- Grooming: brush the dust out of a fluffy coat, flick off leaves, pop the soap bubbles and finish with a bow. Your little friend closes its eyes and sighs, happy and clean.
- Hide and Seek: your animals hide behind the real bushes and tall grass of your refuge. Spot an ear, a tail or a nose, tap, and they jump out with a peekaboo.
- Each activity has ten challenges, stars to earn and a free practice mode you can play anytime.

A gentle daily visit
- Open a little gift, stamp your calendar and help your animals with their wishes.
- A full visit takes about two minutes. Miss a day and nothing resets.
- Work through a weekly journal of small goals.
- Give treats and gentle pets to grow your bond with each animal.

Made for the game
- Every animal is modeled for the game, with original music, sounds and fonts.
- Soft round shapes, warm colors and a calm soundtrack, for a quick relaxing break or a long cozy session.
- The game adapts its detail to your phone.

If you love cozy games, cute animals, virtual pets and calm idle games, Pocket Wilds is a little meadow to come back to whenever you like.

Fair and friendly
- Free to play, with optional in-app purchases.
- Nothing random is for sale: eggs are hatched with what you earn by playing.
- No fake timers and no streaks to lose.
- You can turn off purchases in your phone settings.

Privacy policy: [PRIVACY POLICY URL]
Support: [SUPPORT EMAIL]
```

[CHECK] before upload:
- Species line: list only the species of the launch build. If the Forest, Lake or Mountain ship at launch, add one line ("Open new biomes: a forest, a lake and the mountains").
- Activities: name only the activities that ship. Never write "18 activities" or "13 biomes" before they exist.
- If rewarded ads ship, add: "Ads are optional: you choose to watch one for a reward."
- If a subscription ships (the Wild Club of the design docs), add its renewal line and the terms link.

What this text never says (project rules and store rules):
- no "made without AI" (code and models were produced with AI help; only the honest line "modeled for the game" is used);
- no ranking, award, review or download claim;
- no four-point star symbol and no religious vocabulary (project rules in `Docs/Design/`, including `Vocabulary_Rule.md`);
- no promise of future content with a date.

### 1.4 Category and tags

| Field | Choice | Note |
|---|---|---|
| App or game | Game | |
| Category | Simulation (recommended), Casual as the alternative | Cozy pet and idle collection games usually sit in Simulation. Only one category is allowed |
| Tags (up to 5) | Pick from the list Play Console offers. Candidates to look for: Idle, Virtual pet (or Pet), Animals, Casual, Cute or Stylized | Google says: pick the tags a stranger would find obviously relevant, and change them only after big content changes. Google also adds some tags automatically |
| Accessibility tags | None for now | Only if the game really supports an assistive feature |
| Contact details | Email (required), website (the privacy policy site works), phone (optional unless trader rules apply) | In the EU the developer's contact details are shown publicly |

---

## 2. ASO keyword plan (Play has no keyword field)

Google Play reads the name, the short description and the full description. There is no hidden keyword box. Google's Metadata policy forbids repeated or unrelated keywords, so each word must sit inside a real sentence.

| Priority | Search phrase | Where it goes | How often in the full description |
|---|---|---|---|
| 1 | cozy game, cozy animal game | Name, short description, full description | 3 to 4 times ("cozy" counts) |
| 1 | cute animals, baby animals | Short description, full description | 2 to 3 |
| 1 | hatch, hatch eggs | Short description, full description | 4 to 6 |
| 2 | virtual pet, pets | Full description | 1 to 2 |
| 2 | idle game | Full description | 1 |
| 2 | bunny, hedgehog, squirrel | Short description B, full description | 1 each |
| 2 | hide and seek, grooming, brush | Short description, full description | 1 to 2 each |
| 3 | refuge, meadow, relaxing, collect, Codex | Full description | 1 to 4 |

Rules of thumb:
- Write for people first. Read the text aloud: if a sentence sounds like a list of keywords, rewrite it.
- Do not use competitor names or other games' titles.
- Translate the listing (French first) with the same plan, choosing the words French players type ("jeu cosy", "animaux mignons", "éclosion").
- After launch, look once a month at the Play Console acquisition reports (they show which search terms bring visitors to the listing) and adjust the full description, not the name.

---

## 3. Graphics list with exact specs

All art must come from the real game (captures in Unity or recordings of the build). No image generator, no stock art, no third-party font (project rule `Docs/Design/Handmade_Rule.md`).

| Asset | Exact spec (Play Console Help) | Required? | What to show |
|---|---|---|---|
| App icon | 512 × 512 px, 32-bit PNG with alpha, sRGB, 1,024 KB max. Full square: Google applies the rounded mask (radius 30 % of the size) and the shadow, so do not round corners or add a shadow. No text or badge about ranking, price or Play programs | Yes | Mochi's face (the first brown bunny), large and centered, on a warm meadow-green or cream background. Rendered from the real model. The icon does not exist yet in the project |
| Feature graphic | 1,024 × 500 px, JPEG or 24-bit PNG, no alpha. Keep key elements away from the edges. Avoid pure white, black or dark gray backgrounds. Alt text up to 140 characters | Yes | Wide meadow shot with the three species, the egg in the middle, logo on the left third |
| Phone screenshots | 2 to 8 per device type, JPEG or 24-bit PNG, no alpha. Each side 320 to 3,840 px. The long side can be at most twice the short side | Yes (at least 2) | See the 8 captions below |
| For promotion (games) | At least three 9:16 portrait screenshots of at least 1,080 × 1,920 px (or three 16:9 landscape, 1,920 × 1,080), showing gameplay. UI first in the first three. Caption text no more than 20 % of the image. Alt text up to 140 characters | Highly recommended | Export every screenshot at 1,080 × 1,920 |
| Tablet screenshots (7" and 10") | At least 4, 1,080 to 7,680 px, 16:9 or 9:16 | Only if the game is offered on tablets | Same scenes, re-framed |
| Preview video | One YouTube URL (not a playlist or channel), public or unlisted, ads turned off (no monetization), not age-restricted, embeddable. No time code in the URL. Only the first 30 seconds autoplay; show the game in the first 10 seconds | Not required, but games need it to appear in some parts of Play | 30-second cut, script below |

Important: the game's own captures are 1,080 × 2,340 px (19.5:9). That is more than twice as tall as wide, so Play will refuse them. Compose each screenshot on a 1,080 × 1,920 canvas: the game image scaled down, plus the caption band.

### 3.1 Eight screenshot captions tied to real screens

Each caption is 2 to 6 words, in the Pocket Plush title font, on a cream band at the top that takes less than 20 % of the image. Use the English build (all the screens below have English captures in `Docs/Captures/`).

| # | Caption | Real screen to capture | Notes |
|---|---|---|---|
| 1 | Hatch your first friend | Single hatch: the egg cracking, then the "New species!" banner over Mochi (brown rabbit, 1 round star, "Common") | The first screenshot carries the promise: egg + baby animal |
| 2 | A cozy refuge in your pocket | The refuge at "golden evening" light with rabbits, squirrels and the hedgehog, HUD visible (Light counter, Hatch button) | UI visible in the first three, as Google asks |
| 3 | Welcome every newborn | The multi-hatch summary card "Welcome to the refuge" with the "New" badges (×10 grid, `T35B_summary10_en`) | Shows collection and rarity frames |
| 4 | Brush away the dust | Grooming: brush on a dusty bunny, then the three bow buttons (pink, sky blue, cream) | Use the squirrel or the bunny with a bow on its head |
| 5 | Can you spot them? | Hide and Seek: ears above tall grass, a tail behind the flower bush, magnifier and whistle buttons | The moment just before a "peekaboo" |
| 6 | A little gift every day | Today screen: the round wooden gift box opening, the calendar with the paw stamp, the wishes list | No streak or countdown anywhere on the screen, which is true to the game |
| 7 | Open the meadow, zone by zone | Zone opening: the hedge sinking, petals and leaves flying, color coming back | A frame in the middle of the color wave |
| 8 | Fill your Codex | The Codex page or the egg odds window ("Odds" with round stars and percentages) | The odds window is a quiet trust signal: odds are always shown |

Alt text example (140 characters max): "A brown bunny hatches from a spotted egg in a green meadow, with a banner that says New species."

### 3.2 Preview video script (30 seconds, portrait, game footage only)

| Time | Shot | On-screen text |
|---|---|---|
| 0 to 3 s | Egg wobbles, cracks in three beats, Mochi pops out and hops | Hatch a new friend |
| 3 to 8 s | Grooming: dust fades under the brush, soap bubble pops, bow lands | (none) |
| 8 to 13 s | Hide and Seek: tap a bush, a squirrel jumps out with a peekaboo and a heart | Find them all |
| 13 to 18 s | Refuge wide shot, light rain, then a rainbow | (none) |
| 18 to 23 s | Zone opening: the hedge sinks, color returns, animals hop in | Grow your refuge |
| 23 to 27 s | Today screen: the gift box lid lifts, the calendar gets its paw stamp | A little gift every day |
| 27 to 30 s | Logo over the meadow | Pocket Wilds |

Music: the game's own soundtrack, no licensed track. Upload to the game's YouTube channel as "Unlisted", with monetization off.

---

## 4. Checklist before the listing goes live

- [ ] Final name chosen and the package name set before the first upload.
- [ ] Icon and feature graphic rendered from the real models, reviewed with the "fait main" grid (`Docs/Captures/Review/`).
- [ ] 8 screenshots at 1,080 × 1,920, captions in Pocket Plush, alt text written.
- [ ] Preview video on YouTube (unlisted, no ads, embeddable).
- [ ] Every [CHECK] line resolved against the build.
- [ ] Privacy policy URL live (see `Privacy_Policy_Draft_en.md` and the French guide).
- [ ] App content declarations done (see `Declarations_Draft.md`).
- [ ] French translation of the listing.

## Sources

- Play Console Help, store listing fields and character limits: https://support.google.com/googleplay/android-developer/answer/9859152
- Play Console Help, preview assets (icon, feature graphic, screenshots, video): https://support.google.com/googleplay/android-developer/answer/9866151
- Google Play icon design specifications: https://developer.android.com/distribute/google-play/resources/icon-design-specifications
- Play Developer Policy, Metadata: https://support.google.com/googleplay/android-developer/answer/9898842
- Play Console Help, choose a category and tags (up to five tags): https://support.google.com/googleplay/android-developer/answer/113475
