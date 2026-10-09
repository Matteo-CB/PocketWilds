# Pocket Wilds glossary: German (de)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/de.json`. Forbidden words: `de_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Informal « du » everywhere (the French « tu »): warm, gentle, playful, never pushy. No « Jetzt kaufen! », no countdown pressure.
- Goals (wishes, weekly journal, challenges) are short imperatives in the du-form: « Lass 3 Eier schlüpfen », « Bürste ein Tier ».
- Buttons are infinitives or short verbs, as German apps do: Sammeln, Abholen, Spielen, Weiter, Schließen, Öffnen.
- Cozy diminutives with care: Leckerli, Häuschen, Blümchen, Nickerchen, Zappler.
- Numbers: decimal comma (× 1,25); « 12 % » with a no-break space before %; SI units min, h, s with a no-break space.
- German quotes „ “ if a name ever needs quoting. No em dash: use a colon or a comma.
- Avoid the German noun for a supernatural wonder and every word built on it, the usual adjectives for wonderful included (see de_forbidden.txt): write schön, toll, herrlich.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- {species_the} and {animal} (an animal's own name, else « species_the »): German sentences always place them as an accusative object (« Bürste {animal} », « Ein Leckerli für {animal} », « Schick {animal} auf Expedition »), so `species.*.name_the` holds the accusative with its article: das Kaninchen, das Eichhörnchen, den Igel.
- {biome_the} is the bare name (Wiese, Wald) and sits after « Biom » or in parentheses (« Verbessere Kuschelnester (Wiese) »), because the gate title « {biome} öffnen » also reads it.
- Zone names carry no article (Blumenwiese, Obstgarten): « {zone} öffnen », and the zone footer shows the bare name.
- Compounds with a name: « {species}-Sternbild » works for the three species; « {biome}-Leuchten » needs the compound form « Wiesen- » (T39B noun forms).

## Plurals

CLDR categories: one / other. The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

| Noun | one | other |
|---|---|---|
| egg | {n} Ei | {n} Eier |
| Gem | {n} Juwel | {n} Juwelen |
| ticket | {n} Ticket | {n} Tickets |
| treat | {n} Leckerli | {n} Leckerlis |
| seed | {n} Samen | {n} Samen |
| sticker | {n} Sticker | {n} Sticker |
| decoration | {n} Deko | {n} Dekos |
| bond point | {n} Bindungspunkt | {n} Bindungspunkte |
| day | Noch {n} Tag | Noch {n} Tage |

## Species

| English | German | Key |
|---|---|---|
| Rabbit | Kaninchen | `species.rabbit.name` |
| Squirrel | Eichhörnchen | `species.squirrel.name` |
| Hedgehog | Igel | `species.hedgehog.name` |
| the Rabbit (sentence form) | das Kaninchen | `species.rabbit.name_the` |
| the Squirrel (sentence form) | das Eichhörnchen | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | den Igel | `species.hedgehog.name_the` |
| species | Arten | `refuge.source.species` |
| variants | Varianten | `codex.variants` |
| newborn | Neugeborenes | wishes, journal |

## Variant colors

| English | German | Key |
|---|---|---|
| Brown | Braun | `variant.rabbit.brown.name` |
| White | Weiß | `variant.rabbit.white.name` |
| Grey | Grau | `variant.rabbit.grey.name` |
| Black | Schwarz | `variant.rabbit.black.name` |
| Spotted | Gefleckt | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Rot | `variant.squirrel.red.name` |
| Black (squirrel) | Schwarz | `variant.squirrel.black.name` |
| Cream | Creme | `variant.squirrel.cream.name` |
| Autumn | Herbst | `variant.squirrel.autumn.name` |
| Mushroom | Pilz | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | German | Key |
|---|---|---|
| Gems | Juwelen (sg. Juwel), capitalized as a currency | |
| Light | Licht (das), mass noun: « 120 Licht », « +12 % Licht » | |
| ticket / tickets | {n} Tickets | `reward.tickets.other` |
| treat | Leckerli | `sheet.treat` |
| treats | {n} Leckerlis | `reward.treats.other` |
| the Mushroom Biscuit | Pilzkeks | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} Sticker | `reward.sticker.other` |
| seeds | {n} Samen | `reward.seeds.other` |
| Flower Sugar | Blütenzucker | `ingredient.flower_sugar.name` |
| decoration | Deko (pl. Dekos), the casual German word, short for the boxes | |
| bond points | {n} Bindungspunkte | `reward.bond.other` |
| Discovery egg | Entdecker-Ei | `egg.discovery.name` |
| Meadow Egg | Wiesen-Ei | `egg.grassland.basic.name` |
| Precious Meadow Egg | Kostbares Wiesen-Ei | `egg.grassland.focus.name` |
| Starter Pack | Starterpaket | `shop.starter_pack.title` |
| Flower Crown | Blumenkranz | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | German | Key |
|---|---|---|
| refuge | Refugium (das): « Willkommen im Refugium », « Refugium-Stufe » | |
| Meadow (biome; FR Prairie) | Wiese | `biome.grassland.name` |
| Forest (biome) | Wald | `biome.forest.name` |
| biome / biomes | Biome | `refuge.source.biomes` |
| zone / zones | Zonen | `refuge.source.zones` |
| Flower Meadow (zone) | Blumenwiese | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | Mühlenhügel | `zone.grassland.windmillrise.name` |
| Open the Lake | Öffne den See | `activity.unlock.lake` |
| Open the Mountain | Öffne den Berg | `activity.unlock.mountain` |
| Shop | Shop | `shop.title` |

## Rarities

| English | German | Key |
|---|---|---|
| Common | Gewöhnlich | `rarity.common` |
| Uncommon | Ungewöhnlich | `rarity.uncommon` |
| Rare | Selten | `rarity.rare` |
| Epic | Episch | `rarity.epic` |
| Legendary | Legendär | `rarity.legendary` |
| Fabulous | Fabelhaft | `rarity.fabulous` |

## Activities

| English | German | Key |
|---|---|---|
| Grooming | Fellpflege | `activity.grooming.name` |
| Hide Seek | Verstecken | `activity.hide_seek.name` |
| Nut Drop | Nussregen | `activity.nut_drop.name` |
| Memory | Pfoten-Memo | `activity.memory.name` |
| Expeditions | Expeditionen | `activity.expeditions.name` |
| Garden | Garten | `activity.garden.name` |
| Treasure Fishing | Schatzangeln | `activity.treasure_fishing.name` |
| Lily Hop | Seerosen-Hüpfen | `activity.lily_hop.name` |
| Baking | Backen | `activity.baking.name` |
| Photo Safari | Fotosafari | `activity.photo_safari.name` |
| Cuddle Match | Kuschel-Match | `activity.cuddle_match.name` |
| Glide | Gleitflug | `activity.glide.name` |
| Constellations | Sternbilder | `activity.constellations.name` |
| Firefly Rhythm | Glühwürmchen-Takt | `activity.firefly_rhythm.name` |
| Bubbles | Blasen | `activity.bubbles.name` |
| Tile Puzzle | Schiebepuzzle | `activity.tile_puzzle.name` |
| Burrow Maze | Bau-Labyrinth | `activity.burrow_maze.name` |
| Grand Contest | Großer Wettbewerb | `activity.grand_contest.name` |

## Features

| English | German | Key |
|---|---|---|
| rendez-vous (Daily Visit) | the daily visit has no name on screen: « Heute » (the panel), « Tagesgeschenk », « Wünsche », « Kalender », « Wochenheft » | |
| Today (panel) | Heute | `hud.today` |
| Daily gift | Tagesgeschenk | `daily.gift.title` |
| Wishes | Wünsche | `wish.title` |
| Calendar | Kalender | `daily.calendar.title` |
| Weekly journal | Wochenheft | `journal.title` |
| album | Album (pl. Alben) | |
| bond | Bindung | `sheet.bond` |
| mastery | Meisterschaft: « Wiesen-Meisterschaft », « Arten-Meisterschaft » | |
| Codex | Kodex | `codex.title` |
| Upgrades | Upgrades | `upgrades.title` |
| Activities (hub) | Aktivitäten | `activity.hub.title` |
| challenge | Aufgabe (pl. Aufgaben); « Herausforderung » is too long for the boxes | |
| Practice | Üben | `activity.practice` |
| record | Neuer Rekord | `activity.result.record` |
| streak | Serie ×{n} | `activity.hud.streak` |
| Refuge level | Refugium-Stufe | `refuge.level.title` |

## UI verbs and buttons

| English | German | Key |
|---|---|---|
| Collect | Sammeln | `ui.collect.button` |
| Claim | Abholen | `today.claim` |
| Claim all | Alle abholen | `today.claim_all` |
| Play | Spielen | `hud.play` |
| Continue | Weiter | `summary.continue` |
| Close | Schließen | `today.close` |
| Open | Öffnen | `zone.open.button` |
| OK | OK | `ui.ok.button` |
| Cancel | Abbrechen | `ui.cancel.button` |
| Swap | Tauschen | `wish.swap` |
| Stamp | Stempeln | `daily.calendar.stamp` |
| Resume | Weiter | `activity.pause.resume` |
| Quit | Beenden | `activity.pause.quit` |
| Back | Zurück | `activity.result.back` |
| Play again | Noch mal | `activity.result.replay` |
| Details | Details | `sheet.details.button` |
| Groom (button) | Bürsten | `activity.grooming.sheet_groom` |
| Free | Gratis | `egg.price.free` |
| New | Neu | `codex.new` |
