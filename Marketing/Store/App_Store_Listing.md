# App Store listing (draft)

Status: draft, English, written on 9 October 2026 for the working title "Pocket Wilds". Limits checked against App Store Connect Help and the App Review Guidelines (sources at the end). Counts measured with a script.

The App Store reads three text fields for search: the **name**, the **subtitle** and the hidden **keyword field**. A word used in one of them should not be repeated in another. The promotional text and the description do not help search.

---

## 1. Text fields

### 1.1 Name (2 to 30 characters)

| Text | Count |
|---|---|
| Pocket Wilds: Cozy Animals | 26 |

App Review guideline 2.3.7: a unique name, 30 characters at most, no trademarked terms, popular app names or prices packed in. The name can be edited until the first submission, then only with a new version.

### 1.2 Subtitle (30 characters max)

| Option | Text | Count |
|---|---|---|
| A (recommended) | Hatch cute pets, grow a refuge | 30 |
| B | Hatch, brush and find pets | 26 |

### 1.3 Keyword field (100 bytes max)

```
idle,egg,bunny,rabbit,hedgehog,squirrel,kawaii,relaxing,collect,virtual,grooming,hide,seek,meadow
```

Count: 97 characters, 97 bytes, 14 keywords, no duplicates.

How it was built (Apple's own guidance):
- commas between keywords, no spaces after the commas;
- no word already in the name or subtitle: pocket, wilds, cozy, animal(s), hatch, cute, pet(s), grow, refuge are left out (Apple treats a plural as a duplicate);
- no "game" or "app" (too broad, and "Games" is already the category);
- no competitor or other app names, no trademarks;
- each keyword longer than two characters;
- "hide" and "seek" are separate words to save bytes. ASO tools observe that the App Store combines words across the name, subtitle and keyword field, so "hide and seek" searches should still match (Apple does not document this; check after launch by searching the store yourself and in App Store Connect analytics).

If the subtitle B is chosen, "cute" and "grow" become free: swap "kawaii" for "cute".

### 1.4 Promotional text (170 characters max)

```
Meet the baby animals of the meadow. Brush them, find them in Hide and Seek and open your refuge zone by zone. A gentle visit a day, never a streak to lose.
```

Count: 156 characters. It appears above the description and can be changed at any time without a new version, so use it for news ("A new activity is here"). It does not affect search.

### 1.5 Description (4,000 characters max, plain text)

Count: 2,057 characters.

```
Welcome to Pocket Wilds, a cozy game about little lives in a big meadow. Hatch an egg, meet a new baby animal, and watch your refuge grow, one gentle day at a time.

Tap the egg. It wobbles, cracks and pops, and a new friend blinks up at you. Bunnies, squirrels and hedgehogs come in many colors. Each one wanders, nibbles the grass, naps in the sun and hops over to say hello.

HATCH AND COLLECT
- Hatch eggs one at a time, or several at once, and welcome every newborn to the refuge.
- Discover new colors and rarer animals, from Common to Legendary and beyond.
- The odds of every egg are shown in the game, and eggs are hatched with Light you earn by playing.
- Fill your Codex with every species and color you find.

GROW YOUR REFUGE
- Your animals make Light. Spend it on upgrades and open the meadow zone by zone.
- When a hedge sinks, the color flows back into the new zone and your animals hop in to explore.
- The light follows your day, from soft morning to golden evening, with gentle rain and the odd rainbow.

PLAY WITH YOUR ANIMALS
- Grooming: brush the dust out of a fluffy coat, flick off leaves, pop the soap bubbles and finish with a bow.
- Hide and Seek: your animals hide behind the real bushes and tall grass of your refuge. Spot an ear, a tail or a nose, and they jump out with a peekaboo.
- Ten challenges per activity, stars to earn and a free practice mode.

A GENTLE DAILY VISIT
- Open a little gift, stamp your calendar and help your animals with their wishes.
- A full visit takes about two minutes. Miss a day and nothing resets.
- Give treats and gentle pets to grow your bond with each animal.

MADE FOR THE GAME
- Every animal is modeled for the game, with original music, sounds and fonts.
- Soft round shapes, warm colors and a calm soundtrack for a relaxing break.

FAIR AND FRIENDLY
- Free to play, with optional in-app purchases.
- Nothing random is for sale: eggs are hatched with what you earn by playing.
- No fake timers and no streaks to lose.

Privacy policy: [PRIVACY POLICY URL]
Terms of use: [TERMS OF USE URL]
```

[CHECK] before submission, as for Google Play: species, activities and purchases must match the build. Never name other platforms in Apple metadata (guideline 2.3.10). If a subscription ships, the description (or the license field) must link to the terms of use; Apple's standard license can be used.

### 1.6 Other fields

| Field | Draft | Rule |
|---|---|---|
| Primary category | Games | |
| Game subcategories (one or two) | Simulation, Casual | Allowed when Games is the category |
| Secondary category | Entertainment, or none | |
| Support URL | [SUPPORT URL] | Must lead to real contact information |
| Marketing URL | The game's small website, if any | Optional |
| Privacy policy URL | [PRIVACY POLICY URL] | Required. The policy must also be linked inside the game (guideline 5.1.1(i)) |
| Copyright | "2026 [Rights holder]" | Year then the owner of the rights. Apple adds the copyright sign itself |
| Age rating | See `Declarations_Draft.md` | |
| What's New | Not shown for the first version | Required for every later version, 4,000 characters max |

---

## 2. Screenshots

Apple's current table names device groups by display type. The two sizes asked for here:

| Class | Devices (examples) | Accepted sizes, portrait (px) | Role |
|---|---|---|---|
| 6.9" (iPhone with Dynamic Island, large display) | iPhone 16 Pro Max, 17 Pro Max, 18 Pro Max, Air | 1320 × 2868 (recommended), 1290 × 2796, 1260 × 2736 | Main set. If missing, Apple scales the 6.5" set |
| 6.5" (iPhone with Face ID, large display) | iPhone 11 Pro Max, 13 Pro Max, 14 Plus | 1284 × 2778 (recommended), 1242 × 2688 | Required if the 6.9" set is not provided |

- 1 to 10 screenshots per size, `.jpeg`, `.jpg` or `.png`, no alpha channel, no transparency.
- Apple's page also lists, under "Required device sizes", at least one screenshot for the 6.1" class (iPhone with Dynamic Island, medium display: 1206 × 2622 or 1179 × 2556). Apple scales between classes when a size is missing, but check what App Store Connect asks for on the day of upload. Producing the 6.9" set and a 1206 × 2622 set covers every case.
- iPad: if the build runs on iPad (Unity's default is iPhone and iPad), 13" iPad screenshots are required: 2064 × 2752 or 2048 × 2732. Owner decision: ship iPhone only at first, or also capture iPad screens.

Use the same 8 scenes and captions as the Google Play list (`Google_Play_Listing.md`, section 3.1). Apple screenshots can keep the tall 19.5:9 shape, so the game captures fit with little cropping.

## 3. App previews (videos)

| Spec | Value |
|---|---|
| Number | Up to 3 per device size and language |
| Length | 15 to 30 seconds |
| File | .mov, .m4v or .mp4 (H.264), or .mov (ProRes 422 HQ). 500 MB max |
| Resolution, 6.9" and 6.5" iPhone, portrait | 886 × 1920 |
| Frame rate | 30 fps max |
| Video bit rate (H.264) | 10 to 12 Mbps, progressive, up to High Profile Level 4.0 |
| Audio | Stereo, AAC 256 kbps, 44.1 or 48 kHz |
| Poster frame | Default at 5 seconds, choose a strong frame (Mochi hatching) |

Content rules from Apple's app preview guidelines:
- show only footage captured in the app: no hands, no person holding a phone;
- touch hints can be drawn as graphics;
- the video plays muted by default, so add short on-screen text;
- no prices, no seasonal or dated references;
- if features shown need an in-app purchase, say so in the footage or the end frame;
- only music and content you have the rights to: the game's own soundtrack is ideal.

Use the 30-second script from the Google Play file. If a shot shows a paid item, add an end-frame line: "In-app purchases available."

---

## 4. Checklist

- [ ] Name and subtitle chosen, no word repeated in the keyword field.
- [ ] Keyword field pasted exactly (97 bytes), revised after the final name.
- [ ] 6.9" set (1320 × 2868) and 6.1" set (1206 × 2622) exported, plus 6.5" if App Store Connect asks.
- [ ] iPad decision made (screenshots 2064 × 2752 if yes).
- [ ] One app preview, 886 × 1920, 15 to 30 s, game audio only.
- [ ] Privacy policy URL live and linked inside the game.
- [ ] Age rating questionnaire and App Privacy answers entered (`Declarations_Draft.md`).
- [ ] EU trader status declared (Digital Services Act section of App Store Connect).

## Sources

- App Store Connect Help, App information (name 2 to 30 characters, subtitle 30, subcategories): https://developer.apple.com/help/app-store-connect/reference/app-information/app-information
- App Store Connect Help, Platform version information (promotional text 170, description 4,000, keywords 100 bytes, support URL, copyright): https://developer.apple.com/help/app-store-connect/reference/app-information/platform-version-information
- Apple, App Store search guidance (commas, no spaces, no repeated words, plurals): https://developer.apple.com/app-store/search/
- App Store Connect Help, Screenshot specifications: https://developer.apple.com/help/app-store-connect/reference/screenshot-specifications
- App Store Connect Help, App preview specifications: https://developer.apple.com/help/app-store-connect/reference/app-preview-specifications
- Apple, App previews guidelines: https://developer.apple.com/app-store/app-previews/
- App Review Guidelines (2.3.7, 2.3.10, 5.1.1): https://developer.apple.com/app-store/review/guidelines/
- App Store Connect Help, EU Digital Services Act trader requirements: https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/
