# Pocket Wilds glossary: Italian (it)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/it.json`. Forbidden words: `it_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Informal « tu »: warm, gentle, playful, never pushy.
- Goals are short tu imperatives: « Fai schiudere 3 uova », « Spazzola un animale ».
- Buttons: Raccogli, Riscuoti, Gioca, Continua, Chiudi, Apri, Annulla.
- Gender-neutral greetings: « Che bello rivederti! », « Benvenuti al rifugio ».
- Cozy words: regalino, regalone, bocconcini, zampette, giostrina, coccole, Occhio di lince.
- Numbers: decimal comma (× 1,25); « 12% » without a space; units min, h, s with a no-break space.
- For light blue write azzurro, and for a ring of light write « cerchio di luce »: the other usual words are on the forbidden list. No em dash.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- `species.*.name_the` = il Coniglio, lo Scoiattolo, il Riccio; `biome.*.name_the` = il Prato, il Bosco; zone names carry their article (il Prato fiorito, l'Ansa del ruscello).
- The drafts avoid the contractions (a + il = al, di + il = del, in + il = nel): direct objects (« Spazzola {animal} », « Coccola {animal} », « Vizia {animal} con un bocconcino »), « per {animal} », « nel bioma {biome} » or « ({biome}) » with the bare name, and colons.
- « un {cake} » (wish.038) needs the cake's gender (un / una) from T39B.

## Plurals

CLDR categories: one / many / other (many = other). The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

| Noun | one | other |
|---|---|---|
| egg | un uovo / {n} uovo | {n} uova |
| Gem | {n} Gemma | {n} Gemme |
| ticket | {n} biglietto | {n} biglietti |
| day | Manca {n} giorno | Mancano {n} giorni |

## Species

| English | Italian | Key |
|---|---|---|
| Rabbit | Coniglio | `species.rabbit.name` |
| Squirrel | Scoiattolo | `species.squirrel.name` |
| Hedgehog | Riccio | `species.hedgehog.name` |
| the Rabbit (sentence form) | il Coniglio | `species.rabbit.name_the` |
| the Squirrel (sentence form) | lo Scoiattolo | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | il Riccio | `species.hedgehog.name_the` |
| species | Specie | `refuge.source.species` |
| variants | Varianti | `codex.variants` |
| newborn | cucciolo | wishes, journal |

## Variant colors

| English | Italian | Key |
|---|---|---|
| Brown | Marrone | `variant.rabbit.brown.name` |
| White | Bianco | `variant.rabbit.white.name` |
| Grey | Grigio | `variant.rabbit.grey.name` |
| Black | Nero | `variant.rabbit.black.name` |
| Spotted | Pezzato | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Rosso | `variant.squirrel.red.name` |
| Black (squirrel) | Nero | `variant.squirrel.black.name` |
| Cream | Crema | `variant.squirrel.cream.name` |
| Autumn | Autunnale | `variant.squirrel.autumn.name` |
| Mushroom | Fungo | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | Italian | Key |
|---|---|---|
| Gems | Gemme (sg. Gemma), capitalized as a currency | |
| Light | Luce (f), mass noun: « 120 di Luce », « +12% di Luce » | |
| ticket / tickets | {n} biglietti | `reward.tickets.other` |
| treat | Bocconcino | `sheet.treat` |
| treats | {n} bocconcini | `reward.treats.other` |
| the Mushroom Biscuit | Biscotto fungo | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} figurine | `reward.sticker.other` |
| seeds | {n} semi | `reward.seeds.other` |
| Flower Sugar | Zucchero di fiori | `ingredient.flower_sugar.name` |
| decoration | decorazione (pl. decorazioni) | |
| bond points | {n} punti legame | `reward.bond.other` |
| Discovery egg | Uovo Esploratore | `egg.discovery.name` |
| Meadow Egg | Uovo del prato | `egg.grassland.basic.name` |
| Precious Meadow Egg | Uovo prezioso del prato | `egg.grassland.focus.name` |
| Starter Pack | Pack iniziale | `shop.starter_pack.title` |
| Flower Crown | Coroncina di fiori | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | Italian | Key |
|---|---|---|
| refuge | rifugio (m): « Benvenuti al rifugio », « Livello del rifugio » (the more religious-sounding synonym is on the forbidden list) | |
| Meadow (biome; FR Prairie) | Prato | `biome.grassland.name` |
| Forest (biome) | Bosco | `biome.forest.name` |
| biome / biomes | Biomi | `refuge.source.biomes` |
| zone / zones | Zone | `refuge.source.zones` |
| Flower Meadow (zone) | il Prato fiorito | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | il Poggio del mulino | `zone.grassland.windmillrise.name` |
| Open the Lake | Apri il Lago | `activity.unlock.lake` |
| Open the Mountain | Apri la Montagna | `activity.unlock.mountain` |
| Shop | Negozio | `shop.title` |

## Rarities

| English | Italian | Key |
|---|---|---|
| Common | Comune | `rarity.common` |
| Uncommon | Non comune | `rarity.uncommon` |
| Rare | Raro | `rarity.rare` |
| Epic | Epico | `rarity.epic` |
| Legendary | Leggendario | `rarity.legendary` |
| Fabulous | Favoloso | `rarity.fabulous` |

## Activities

| English | Italian | Key |
|---|---|---|
| Grooming | Toelettatura | `activity.grooming.name` |
| Hide Seek | Nascondino | `activity.hide_seek.name` |
| Nut Drop | Pioggia di noci | `activity.nut_drop.name` |
| Memory | Memory delle zampette | `activity.memory.name` |
| Expeditions | Spedizioni | `activity.expeditions.name` |
| Garden | Giardino | `activity.garden.name` |
| Treasure Fishing | Pesca al tesoro | `activity.treasure_fishing.name` |
| Lily Hop | Salto sulle ninfee | `activity.lily_hop.name` |
| Baking | Pasticceria | `activity.baking.name` |
| Photo Safari | Safari fotografico | `activity.photo_safari.name` |
| Cuddle Match | Match di coccole | `activity.cuddle_match.name` |
| Glide | Planata | `activity.glide.name` |
| Constellations | Costellazioni | `activity.constellations.name` |
| Firefly Rhythm | Ritmo delle lucciole | `activity.firefly_rhythm.name` |
| Bubbles | Bolle | `activity.bubbles.name` |
| Tile Puzzle | Puzzle scorrevole | `activity.tile_puzzle.name` |
| Burrow Maze | Labirinto della tana | `activity.burrow_maze.name` |
| Grand Contest | Gran Concorso | `activity.grand_contest.name` |

## Features

| English | Italian | Key |
|---|---|---|
| rendez-vous (Daily Visit) | no name on screen: « Oggi » (the panel), « Regalo del giorno », « Desideri », « Calendario », « Diario settimanale » | |
| Today (panel) | Oggi | `hud.today` |
| Daily gift | Regalo del giorno | `daily.gift.title` |
| Wishes | Desideri | `wish.title` |
| Calendar | Calendario | `daily.calendar.title` |
| Weekly journal | Diario settimanale | `journal.title` |
| album | album (invariable); stickers are « figurine », the Italian sticker-album word | |
| bond | Legame | `sheet.bond` |
| mastery | maestria: « Maestria del Prato » | |
| Codex | Codex | `codex.title` |
| Upgrades | Migliorie | `upgrades.title` |
| Activities (hub) | Attività | `activity.hub.title` |
| challenge | sfida (pl. sfide) | |
| Practice | Allenamento | `activity.practice` |
| record | Nuovo record | `activity.result.record` |
| streak | Serie ×{n} | `activity.hud.streak` |
| Refuge level | Livello del rifugio | `refuge.level.title` |

## UI verbs and buttons

| English | Italian | Key |
|---|---|---|
| Collect | Raccogli | `ui.collect.button` |
| Claim | Riscuoti | `today.claim` |
| Claim all | Riscuoti tutto | `today.claim_all` |
| Play | Gioca | `hud.play` |
| Continue | Continua | `summary.continue` |
| Close | Chiudi | `today.close` |
| Open | Apri | `zone.open.button` |
| OK | OK | `ui.ok.button` |
| Cancel | Annulla | `ui.cancel.button` |
| Swap | Cambia | `wish.swap` |
| Stamp | Timbra | `daily.calendar.stamp` |
| Resume | Riprendi | `activity.pause.resume` |
| Quit | Esci | `activity.pause.quit` |
| Back | Indietro | `activity.result.back` |
| Play again | Rigioca | `activity.result.replay` |
| Details | Dettagli | `sheet.details.button` |
| Groom (button) | Spazzola | `activity.grooming.sheet_groom` |
| Free | Gratis | `egg.price.free` |
| New | Nuovo | `codex.new` |
