# Pocket Wilds glossary: Indonesian (id)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/id.json`. Forbidden words: `id_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Friendly everyday Indonesian with « kamu » and the possessive « -mu » (Permata pertamamu): warm, gentle, never pushy, never stiff or bureaucratic.
- Goals are short imperatives: « Tetaskan 3 telur », « Sikat {animal} ».
- Buttons: Kumpulkan, Ambil, Main, Lanjut, Tutup, Buka, Batal.
- Casual words players use: HP (phone), main (play), mancing.
- Numbers: decimal comma (× 1,25); « 12% » without a space; units mnt (in tight cells) or menit, jam, detik.
- No em dash.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- Indonesian has no articles, cases or noun plurals: `species.*.name_the` and `biome.*.name_the` are the bare names, every placeholder reads right as it is.
- One form only (CLDR « other »): every counted text keeps {n} and reads right for 1 (« Tetaskan 1 telur »); wish.049 says « dalam satu jepretan » (in one shot) so that it works for 1 too.

## Plurals

CLDR categories: other only. The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

| Noun | other |
|---|---|
| egg | {n} telur |
| Gem | {n} Permata |
| ticket | {n} tiket |

## Species

| English | Indonesian | Key |
|---|---|---|
| Rabbit | Kelinci | `species.rabbit.name` |
| Squirrel | Tupai | `species.squirrel.name` |
| Hedgehog | Landak | `species.hedgehog.name` |
| the Rabbit (sentence form) | Kelinci | `species.rabbit.name_the` |
| the Squirrel (sentence form) | Tupai | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | Landak | `species.hedgehog.name_the` |
| species | Spesies | `refuge.source.species` |
| variants | Varian | `codex.variants` |
| newborn | bayi hewan | wishes, journal |

## Variant colors

| English | Indonesian | Key |
|---|---|---|
| Brown | Cokelat | `variant.rabbit.brown.name` |
| White | Putih | `variant.rabbit.white.name` |
| Grey | Abu-abu | `variant.rabbit.grey.name` |
| Black | Hitam | `variant.rabbit.black.name` |
| Spotted | Belang | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Merah | `variant.squirrel.red.name` |
| Black (squirrel) | Hitam | `variant.squirrel.black.name` |
| Cream | Krem | `variant.squirrel.cream.name` |
| Autumn | Musim Gugur | `variant.squirrel.autumn.name` |
| Mushroom | Jamur | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | Indonesian | Key |
|---|---|---|
| Gems | Permata, capitalized as a currency; no plural mark: « 10 Permata » | |
| Light | Cahaya: « 120 Cahaya », « +12% Cahaya » | |
| ticket / tickets | {n} tiket | `reward.tickets.other` |
| treat | Camilan | `sheet.treat` |
| treats | {n} camilan | `reward.treats.other` |
| the Mushroom Biscuit | Biskuit Jamur | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} stiker | `reward.sticker.other` |
| seeds | {n} benih | `reward.seeds.other` |
| Flower Sugar | Gula Bunga | `ingredient.flower_sugar.name` |
| decoration | dekorasi | |
| bond points | {n} poin ikatan | `reward.bond.other` |
| Discovery egg | Telur Penjelajah | `egg.discovery.name` |
| Meadow Egg | Telur Padang Rumput | `egg.grassland.basic.name` |
| Precious Meadow Egg | Telur Padang Rumput Istimewa | `egg.grassland.focus.name` |
| Starter Pack | Paket Pemula | `shop.starter_pack.title` |
| Flower Crown | Mahkota Bunga | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | Indonesian | Key |
|---|---|---|
| refuge | Suaka: « Selamat datang di Suaka », « Level Suaka » (a protected haven, as in suaka margasatwa) | |
| Meadow (biome; FR Prairie) | Padang Rumput | `biome.grassland.name` |
| Forest (biome) | Hutan | `biome.forest.name` |
| biome / biomes | Bioma | `refuge.source.biomes` |
| zone / zones | Zona | `refuge.source.zones` |
| Flower Meadow (zone) | Padang Bunga | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | Tanjakan Kincir | `zone.grassland.windmillrise.name` |
| Open the Lake | Buka Danau | `activity.unlock.lake` |
| Open the Mountain | Buka Gunung | `activity.unlock.mountain` |
| Shop | Toko | `shop.title` |

## Rarities

| English | Indonesian | Key |
|---|---|---|
| Common | Biasa | `rarity.common` |
| Uncommon | Tak Biasa | `rarity.uncommon` |
| Rare | Langka | `rarity.rare` |
| Epic | Epik | `rarity.epic` |
| Legendary | Legendaris | `rarity.legendary` |
| Fabulous | Memukau | `rarity.fabulous` |

## Activities

| English | Indonesian | Key |
|---|---|---|
| Grooming | Perawatan Bulu | `activity.grooming.name` |
| Hide Seek | Petak Umpet | `activity.hide_seek.name` |
| Nut Drop | Hujan Kacang | `activity.nut_drop.name` |
| Memory | Memori Jejak Kaki | `activity.memory.name` |
| Expeditions | Ekspedisi | `activity.expeditions.name` |
| Garden | Kebun | `activity.garden.name` |
| Treasure Fishing | Mancing Harta Karun | `activity.treasure_fishing.name` |
| Lily Hop | Lompat Lili Air | `activity.lily_hop.name` |
| Baking | Membuat Kue | `activity.baking.name` |
| Photo Safari | Safari Foto | `activity.photo_safari.name` |
| Cuddle Match | Match Pelukan | `activity.cuddle_match.name` |
| Glide | Meluncur | `activity.glide.name` |
| Constellations | Rasi Bintang | `activity.constellations.name` |
| Firefly Rhythm | Irama Kunang-Kunang | `activity.firefly_rhythm.name` |
| Bubbles | Gelembung | `activity.bubbles.name` |
| Tile Puzzle | Puzzle Geser | `activity.tile_puzzle.name` |
| Burrow Maze | Labirin Liang | `activity.burrow_maze.name` |
| Grand Contest | Kontes Akbar | `activity.grand_contest.name` |

## Features

| English | Indonesian | Key |
|---|---|---|
| rendez-vous (Daily Visit) | no name on screen: « Hari Ini » (the panel), « Hadiah harian », « Keinginan », « Kalender », « Jurnal Mingguan » | |
| Today (panel) | Hari Ini | `hud.today` |
| Daily gift | Hadiah harian | `daily.gift.title` |
| Wishes | Keinginan | `wish.title` |
| Calendar | Kalender | `daily.calendar.title` |
| Weekly journal | Jurnal Mingguan | `journal.title` |
| album | album | |
| bond | Ikatan | `sheet.bond` |
| mastery | keahlian: « Keahlian Padang Rumput » | |
| Codex | Kodeks | `codex.title` |
| Upgrades | Peningkatan | `upgrades.title` |
| Activities (hub) | Aktivitas | `activity.hub.title` |
| challenge | tantangan | |
| Practice | Latihan | `activity.practice` |
| record | Rekor baru | `activity.result.record` |
| streak | Rentetan ×{n} | `activity.hud.streak` |
| Refuge level | Level Suaka | `refuge.level.title` |

## UI verbs and buttons

| English | Indonesian | Key |
|---|---|---|
| Collect | Kumpulkan | `ui.collect.button` |
| Claim | Ambil | `today.claim` |
| Claim all | Ambil semua | `today.claim_all` |
| Play | Main | `hud.play` |
| Continue | Lanjut | `summary.continue` |
| Close | Tutup | `today.close` |
| Open | Buka | `zone.open.button` |
| OK | OK | `ui.ok.button` |
| Cancel | Batal | `ui.cancel.button` |
| Swap | Ganti | `wish.swap` |
| Stamp | Stempel | `daily.calendar.stamp` |
| Resume | Lanjut | `activity.pause.resume` |
| Quit | Keluar | `activity.pause.quit` |
| Back | Kembali | `activity.result.back` |
| Play again | Main lagi | `activity.result.replay` |
| Details | Detail | `sheet.details.button` |
| Groom (button) | Sikat | `activity.grooming.sheet_groom` |
| Free | Gratis | `egg.price.free` |
| New | Baru | `codex.new` |
