# Pocket Wilds glossary: Turkish (tr)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/tr.json`. Forbidden words: `tr_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Informal « sen »: warm, gentle, playful, never pushy.
- Goals are short imperatives: « 3 yumurta çatlat », « Bir hayvanı tımarla ».
- Buttons: Topla, Al, Oyna, Devam, Kapat, Aç, İptal.
- Hospitality words: ikram for a treat (offered with care), Kâşif for the Discovery egg, Kıpır Kıpır, pıtrak.
- Numbers: decimal comma (× 1,25); the percent sign before the number (%12, +%{pct}); units dk, sa, sn with a no-break space.
- Suffixes on names take an apostrophe (Tımar'da, Büyük Yarışma'ya). Never put a suffix right after a placeholder: vowel harmony depends on the word.
- No em dash.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- Turkish has no articles: `species.*.name_the` and `biome.*.name_the` are the bare names.
- No suffix is ever attached to a placeholder. The name sits before a noun that takes the suffix (« {biome_the} biyomunda », « {species} ustalığını », « {zone} bölgesini aç », « Kodeks'te {species_the} türüne bak »), before a postposition (« {animal} için », « {animal} ile ») or after a colon (« Fotoğraf çek: {animal} »).
- wish.026 « {upgrade} geliştir » uses a bare object; with the accusative T39B could write « Meraklı Göz'ü geliştir ».

## Plurals

CLDR categories: one / other (the noun stays singular after a number). The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

| Noun | one | other |
|---|---|---|
| egg | Bir yumurta | {n} yumurta |
| Gem | {n} Elmas | {n} Elmas |
| ticket | {n} bilet | {n} bilet |

## Species

| English | Turkish | Key |
|---|---|---|
| Rabbit | Tavşan | `species.rabbit.name` |
| Squirrel | Sincap | `species.squirrel.name` |
| Hedgehog | Kirpi | `species.hedgehog.name` |
| the Rabbit (sentence form) | Tavşan | `species.rabbit.name_the` |
| the Squirrel (sentence form) | Sincap | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | Kirpi | `species.hedgehog.name_the` |
| species | Türler | `refuge.source.species` |
| variants | Varyantlar | `codex.variants` |
| newborn | yavru | wishes, journal |

## Variant colors

| English | Turkish | Key |
|---|---|---|
| Brown | Kahverengi | `variant.rabbit.brown.name` |
| White | Beyaz | `variant.rabbit.white.name` |
| Grey | Gri | `variant.rabbit.grey.name` |
| Black | Siyah | `variant.rabbit.black.name` |
| Spotted | Benekli | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Kızıl | `variant.squirrel.red.name` |
| Black (squirrel) | Siyah | `variant.squirrel.black.name` |
| Cream | Krem | `variant.squirrel.cream.name` |
| Autumn | Sonbahar | `variant.squirrel.autumn.name` |
| Mushroom | Mantar | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | Turkish | Key |
|---|---|---|
| Gems | Elmas, as Turkish mobile games name the premium currency; capitalized; singular after a number: « 10 Elmas » | |
| Light | Işık: « 120 Işık », « +%12 Işık » | |
| ticket / tickets | {n} bilet | `reward.tickets.other` |
| treat | İkram | `sheet.treat` |
| treats | {n} ikram | `reward.treats.other` |
| the Mushroom Biscuit | Mantar Bisküvisi | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} çıkartma | `reward.sticker.other` |
| seeds | {n} tohum | `reward.seeds.other` |
| Flower Sugar | Çiçek Şekeri | `ingredient.flower_sugar.name` |
| decoration | dekor | |
| bond points | {n} bağ puanı | `reward.bond.other` |
| Discovery egg | Kâşif Yumurtası | `egg.discovery.name` |
| Meadow Egg | Çayır Yumurtası | `egg.grassland.basic.name` |
| Precious Meadow Egg | Değerli Çayır Yumurtası | `egg.grassland.focus.name` |
| Starter Pack | Başlangıç Seti | `shop.starter_pack.title` |
| Flower Crown | Çiçek Tacı | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | Turkish | Key |
|---|---|---|
| refuge | Sığınak: « Sığınağa hoş geldin », « Sığınak seviyesi » | |
| Meadow (biome; FR Prairie) | Çayır | `biome.grassland.name` |
| Forest (biome) | Orman | `biome.forest.name` |
| biome / biomes | Biyomlar | `refuge.source.biomes` |
| zone / zones | Bölgeler | `refuge.source.zones` |
| Flower Meadow (zone) | Çiçekli Çayır | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | Değirmen Yamacı | `zone.grassland.windmillrise.name` |
| Open the Lake | Göl'ü aç | `activity.unlock.lake` |
| Open the Mountain | Dağ'ı aç | `activity.unlock.mountain` |
| Shop | Mağaza | `shop.title` |

## Rarities

| English | Turkish | Key |
|---|---|---|
| Common | Yaygın | `rarity.common` |
| Uncommon | Az Yaygın | `rarity.uncommon` |
| Rare | Nadir | `rarity.rare` |
| Epic | Destansı | `rarity.epic` |
| Legendary | Efsanevi | `rarity.legendary` |
| Fabulous | Masalsı | `rarity.fabulous` |

## Activities

| English | Turkish | Key |
|---|---|---|
| Grooming | Tımar | `activity.grooming.name` |
| Hide Seek | Saklambaç | `activity.hide_seek.name` |
| Nut Drop | Fındık Yağmuru | `activity.nut_drop.name` |
| Memory | Pati Hafızası | `activity.memory.name` |
| Expeditions | Geziler | `activity.expeditions.name` |
| Garden | Bahçe | `activity.garden.name` |
| Treasure Fishing | Hazine Avı | `activity.treasure_fishing.name` |
| Lily Hop | Nilüfer Zıplama | `activity.lily_hop.name` |
| Baking | Pastacılık | `activity.baking.name` |
| Photo Safari | Foto Safari | `activity.photo_safari.name` |
| Cuddle Match | Sarılmaca | `activity.cuddle_match.name` |
| Glide | Süzülme | `activity.glide.name` |
| Constellations | Takımyıldızlar | `activity.constellations.name` |
| Firefly Rhythm | Ateşböceği Ritmi | `activity.firefly_rhythm.name` |
| Bubbles | Baloncuklar | `activity.bubbles.name` |
| Tile Puzzle | Kayan Yapboz | `activity.tile_puzzle.name` |
| Burrow Maze | Tünel Labirenti | `activity.burrow_maze.name` |
| Grand Contest | Büyük Yarışma | `activity.grand_contest.name` |

## Features

| English | Turkish | Key |
|---|---|---|
| rendez-vous (Daily Visit) | no name on screen: « Bugün » (the panel), « Günün hediyesi », « Dilekler », « Takvim », « Haftalık Defter » | |
| Today (panel) | Bugün | `hud.today` |
| Daily gift | Günün hediyesi | `daily.gift.title` |
| Wishes | Dilekler | `wish.title` |
| Calendar | Takvim | `daily.calendar.title` |
| Weekly journal | Haftalık Defter | `journal.title` |
| album | albüm | |
| bond | Bağ | `sheet.bond` |
| mastery | ustalık: « Çayır Ustalığı », « Tür ustalığı » | |
| Codex | Kodeks | `codex.title` |
| Upgrades | Geliştirmeler | `upgrades.title` |
| Activities (hub) | Etkinlikler | `activity.hub.title` |
| challenge | görev (pl. görevler) | |
| Practice | Antrenman | `activity.practice` |
| record | Yeni rekor | `activity.result.record` |
| streak | Seri ×{n} | `activity.hud.streak` |
| Refuge level | Sığınak seviyesi | `refuge.level.title` |

## UI verbs and buttons

| English | Turkish | Key |
|---|---|---|
| Collect | Topla | `ui.collect.button` |
| Claim | Al | `today.claim` |
| Claim all | Hepsini al | `today.claim_all` |
| Play | Oyna | `hud.play` |
| Continue | Devam | `summary.continue` |
| Close | Kapat | `today.close` |
| Open | Aç | `zone.open.button` |
| OK | Tamam | `ui.ok.button` |
| Cancel | İptal | `ui.cancel.button` |
| Swap | Değiştir | `wish.swap` |
| Stamp | Damgala | `daily.calendar.stamp` |
| Resume | Devam et | `activity.pause.resume` |
| Quit | Çık | `activity.pause.quit` |
| Back | Geri | `activity.result.back` |
| Play again | Tekrar oyna | `activity.result.replay` |
| Details | Ayrıntılar | `sheet.details.button` |
| Groom (button) | Tımarla | `activity.grooming.sheet_groom` |
| Free | Ücretsiz | `egg.price.free` |
| New | Yeni | `codex.new` |
