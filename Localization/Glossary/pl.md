# Pocket Wilds glossary: Polish (pl)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/pl.json`. Forbidden words: `pl_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Informal « ty » everywhere: warm, gentle, playful, never pushy.
- Goals are short imperatives (perfective): « Wykluj 3 jajka », « Wyszczotkuj zwierzaka ».
- Buttons are short imperatives, as Polish apps do: Zbierz, Odbierz, Graj, Dalej, Zamknij, Otwórz, Anuluj.
- Cozy diminutives: zwierzak, maluszek, nasionko, futerko, kokardka, chmurki, wiercipięta.
- Numbers: decimal comma (× 1,25); « 12% » without a space; units min, h, s with a no-break space.
- Avoid « gwiazdka » for stars (it also names a religious holiday): write gwiazda / gwiazdy / gwiazd. Avoid the adjective built on the noun for a supernatural wonder (see pl_forbidden.txt): write wspaniały, przepiękny.
- No em dash: use a colon or a comma.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- Polish declines every name; a placeholder gives the nominative only. The drafts keep each placeholder where the nominative is right: after a generic noun in apposition (« w biomie {biome_the} », « gatunek {species_the} »), after a colon (« Wyszczotkuj: {animal} », « Otwórz: {zone} », « Blask: {biome} »), or in parentheses (« Wykluj 3 jajka (Łąka) »).
- Two sentences still need a case form from T39B: wish.035 and wish.038 « dla {animal} » (genitive), and wish.038 « Upiecz {cake} » (accusative).
- `species.*.name_the` and `biome.*.name_the` hold the bare nominative (Królik, Wiewiórka, Jeż, Łąka, Las).
- Variant colors agree with the species: wiewiórka is feminine (Ruda, Brązowa, Czarna), królik and jeż masculine (Brązowy, Czarny).

## Plurals

CLDR categories: one / few / many (+ other). The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

« other » is the fraction form in CLDR; the game counts whole numbers only, so the drafts copy « many » there as
the safe fallback.

| Noun | one | few | many |
|---|---|---|---|
| egg | jajko | {n} jajka | {n} jajek |
| Gem | {n} klejnot | {n} klejnoty | {n} klejnotów |
| ticket | {n} bilet | {n} bilety | {n} biletów |
| treat | {n} przysmak | {n} przysmaki | {n} przysmaków |
| seed | {n} nasionko | {n} nasionka | {n} nasionek |
| sticker | {n} naklejka | {n} naklejki | {n} naklejek |
| animal | zwierzak | {n} zwierzaki | {n} zwierzaków |
| star | gwiazda | {n} gwiazdy | {n} gwiazd |
| day | {n} dzień | {n} dni | {n} dni |
| wish | jedno życzenie | {n} życzenia | {n} życzeń |

## Species

| English | Polish | Key |
|---|---|---|
| Rabbit | Królik | `species.rabbit.name` |
| Squirrel | Wiewiórka | `species.squirrel.name` |
| Hedgehog | Jeż | `species.hedgehog.name` |
| the Rabbit (sentence form) | Królik | `species.rabbit.name_the` |
| the Squirrel (sentence form) | Wiewiórka | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | Jeż | `species.hedgehog.name_the` |
| species | Gatunki | `refuge.source.species` |
| variants | Warianty | `codex.variants` |
| newborn | maluszek | wishes, journal |

## Variant colors

| English | Polish | Key |
|---|---|---|
| Brown | Brązowy | `variant.rabbit.brown.name` |
| White | Biały | `variant.rabbit.white.name` |
| Grey | Szary | `variant.rabbit.grey.name` |
| Black | Czarny | `variant.rabbit.black.name` |
| Spotted | Łaciaty | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Ruda | `variant.squirrel.red.name` |
| Black (squirrel) | Czarna | `variant.squirrel.black.name` |
| Cream | Kremowa | `variant.squirrel.cream.name` |
| Autumn | Jesienna | `variant.squirrel.autumn.name` |
| Mushroom | Grzybkowy | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | Polish | Key |
|---|---|---|
| Gems | Klejnoty (sg. klejnot): 1 klejnot, 2 klejnoty, 5 klejnotów; without a counted form: « Klejnoty: {0} » | |
| Light | Światło (n), mass noun in the genitive after a number: « 120 Światła », « +12% Światła », « Światło × 1,25 » | |
| ticket / tickets | {n} biletów | `reward.tickets.other` |
| treat | Przysmak | `sheet.treat` |
| treats | {n} przysmaków | `reward.treats.other` |
| the Mushroom Biscuit | Ciastko-grzybek | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} naklejek | `reward.sticker.other` |
| seeds | {n} nasionek | `reward.seeds.other` |
| Flower Sugar | Cukier kwiatowy | `ingredient.flower_sugar.name` |
| decoration | dekoracja: 1 dekoracja, 2 dekoracje, 5 dekoracji | |
| bond points | {n} punktów więzi | `reward.bond.other` |
| Discovery egg | Jajko Odkrywcy | `egg.discovery.name` |
| Meadow Egg | Jajko łąkowe | `egg.grassland.basic.name` |
| Precious Meadow Egg | Cenne jajko łąkowe | `egg.grassland.focus.name` |
| Starter Pack | Pakiet startowy | `shop.starter_pack.title` |
| Flower Crown | Wianek | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | Polish | Key |
|---|---|---|
| refuge | Ostoja (f): « Witaj w ostoi », « Poziom ostoi » (a haven for wild animals; not « schronisko », which means a shelter for strays) | |
| Meadow (biome; FR Prairie) | Łąka | `biome.grassland.name` |
| Forest (biome) | Las | `biome.forest.name` |
| biome / biomes | Biomy | `refuge.source.biomes` |
| zone / zones | Strefy | `refuge.source.zones` |
| Flower Meadow (zone) | Kwietna łąka | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | Wiatrakowy pagórek | `zone.grassland.windmillrise.name` |
| Open the Lake | Otwórz Jezioro | `activity.unlock.lake` |
| Open the Mountain | Otwórz Góry | `activity.unlock.mountain` |
| Shop | Sklep | `shop.title` |

## Rarities

| English | Polish | Key |
|---|---|---|
| Common | Pospolity | `rarity.common` |
| Uncommon | Niepospolity | `rarity.uncommon` |
| Rare | Rzadki | `rarity.rare` |
| Epic | Epicki | `rarity.epic` |
| Legendary | Legendarny | `rarity.legendary` |
| Fabulous | Bajeczny | `rarity.fabulous` |

## Activities

| English | Polish | Key |
|---|---|---|
| Grooming | Pielęgnacja | `activity.grooming.name` |
| Hide Seek | Chowany | `activity.hide_seek.name` |
| Nut Drop | Orzechowy deszcz | `activity.nut_drop.name` |
| Memory | Łapkowe memory | `activity.memory.name` |
| Expeditions | Wyprawy | `activity.expeditions.name` |
| Garden | Ogród | `activity.garden.name` |
| Treasure Fishing | Łowienie skarbów | `activity.treasure_fishing.name` |
| Lily Hop | Skoki po liliach | `activity.lily_hop.name` |
| Baking | Pieczenie | `activity.baking.name` |
| Photo Safari | Fotosafari | `activity.photo_safari.name` |
| Cuddle Match | Przytulanki | `activity.cuddle_match.name` |
| Glide | Szybowanie | `activity.glide.name` |
| Constellations | Gwiazdozbiory | `activity.constellations.name` |
| Firefly Rhythm | Rytm świetlików | `activity.firefly_rhythm.name` |
| Bubbles | Bańki | `activity.bubbles.name` |
| Tile Puzzle | Przesuwanka | `activity.tile_puzzle.name` |
| Burrow Maze | Labirynt nor | `activity.burrow_maze.name` |
| Grand Contest | Wielki Konkurs | `activity.grand_contest.name` |

## Features

| English | Polish | Key |
|---|---|---|
| rendez-vous (Daily Visit) | the daily visit has no name on screen: « Dziś » (the panel), « Prezent dnia », « Życzenia », « Kalendarz », « Dziennik tygodnia » | |
| Today (panel) | Dziś | `hud.today` |
| Daily gift | Prezent dnia | `daily.gift.title` |
| Wishes | Życzenia | `wish.title` |
| Calendar | Kalendarz | `daily.calendar.title` |
| Weekly journal | Dziennik tygodnia | `journal.title` |
| album | album (pl. albumy) | |
| bond | Więź | `sheet.bond` |
| mastery | Mistrzostwo: « Mistrzostwo Łąki », « Mistrzostwo gatunków » | |
| Codex | Kodeks | `codex.title` |
| Upgrades | Ulepszenia | `upgrades.title` |
| Activities (hub) | Aktywności | `activity.hub.title` |
| challenge | wyzwanie (pl. wyzwania) | |
| Practice | Trening | `activity.practice` |
| record | Nowy rekord | `activity.result.record` |
| streak | Seria ×{n} | `activity.hud.streak` |
| Refuge level | Poziom ostoi | `refuge.level.title` |

## UI verbs and buttons

| English | Polish | Key |
|---|---|---|
| Collect | Zbierz | `ui.collect.button` |
| Claim | Odbierz | `today.claim` |
| Claim all | Odbierz wszystko | `today.claim_all` |
| Play | Graj | `hud.play` |
| Continue | Dalej | `summary.continue` |
| Close | Zamknij | `today.close` |
| Open | Otwórz | `zone.open.button` |
| OK | OK | `ui.ok.button` |
| Cancel | Anuluj | `ui.cancel.button` |
| Swap | Zamień | `wish.swap` |
| Stamp | Stempluj | `daily.calendar.stamp` |
| Resume | Wznów | `activity.pause.resume` |
| Quit | Wyjdź | `activity.pause.quit` |
| Back | Wróć | `activity.result.back` |
| Play again | Jeszcze raz | `activity.result.replay` |
| Details | Szczegóły | `sheet.details.button` |
| Groom (button) | Szczotkuj | `activity.grooming.sheet_groom` |
| Free | Gratis | `egg.price.free` |
| New | Nowe | `codex.new` |
