# NOTES_TR2: draft translations de, es-419, pt-BR, it, id, tr, pl

Agent TR2, 9 October 2026. First pass for the T39B « Langues : socle » import (wave A, Latin script). Not reviewed by a
native speaker yet: see « Questions for native reviewers » below.

## Files

| File | Content |
|---|---|
| `Drafts/de.json`, `tr.json` | 719 keys: the counted keys keep `.one` and `.other` |
| `Drafts/es-419.json`, `pt-BR.json`, `it.json` | 821 keys: `.one`, `.many`, `.other` for the 102 counted keys |
| `Drafts/pl.json` | 923 keys: `.one`, `.few`, `.many`, `.other` |
| `Drafts/id.json` | 617 keys: `.other` only |
| `Glossary/de.md` … `tr.md` (7 files) | fixed terms, tone, plural tables, how names sit in sentences |
| `Glossary/<code>_forbidden.txt` (7 files) | the vocabulary rule in each language (stems and whole words), plus the sparkle words |
| `Drafts/check_drafts.py` | the shared check, written by TR1; used as it is (it already covered every need of these languages) |

`python Localization/Drafts/check_drafts.py de es-419 pt-BR it id tr pl` gives 0 errors and 0 warnings. The drafts were
also checked against the English and French word lists of `VocabularyRuleTests` (whole tokens): none found. The glossary
files and this file contain no forbidden word of any list.

Every value is UTF-8, NFC, keys sorted, placeholders identical to the English, no em dash, no sparkle character. The
no-break spaces of the source (`{n} min`, `4 h`) are kept, and the translations put one between a number and its unit
or counted day word as well.

## Plurals

`PluralRules.Categories` per language, with the key without a suffix carrying the « other » text (the « one » text for
reward.decoration, reward.discovery_egg and reward.sticker, whose English bare key is singular; reward.gems keeps its
« + »).

| Language | Forms | Notes |
|---|---|---|
| de, tr | one, other | Turkish keeps the noun singular after a number, so both forms are often the same. |
| es-419, it | one, many, other | « many » is for exact millions; the counts are digits (« 1.000.000 Gemas », no « de »), so many = other. |
| pt-BR | one, many, other | « one » covers 0 and 1 in CLDR. many = other as above. |
| pl | one, few, many, other | one = 1 only (so it may say « jajko » without {n}); few = 2-4, 22-24…; many = 0, 5-21…; « other » is the fraction form in CLDR, never reached by whole counts: it copies « many » as the safe fallback. Verbs agree too (« Został 1 dzień », « Zostały 3 dni », « Zostało 5 dni »). |
| id | other | Every counted text keeps {n} and reads right for 1; wish.049 says « dalam satu jepretan » (in one shot) instead of « together ». |

## Placeholders with a noun (article and case keys)

The English article placeholders are a known problem. The drafts translate around them so that today's values read
right, and say below what T39B's noun forms must give. What each language puts in the `_the` keys:

| Language | `species.*.name_the` | `biome.*.name_the` | Zone names |
|---|---|---|---|
| de | accusative with article: das Kaninchen, das Eichhörnchen, den Igel | bare: Wiese, Wald | no article (Blumenwiese) |
| pl | bare nominative: Królik, Wiewiórka, Jeż | bare: Łąka, Las | bare |
| es-419 | el Conejo, la Ardilla, el Erizo | la Pradera, el Bosque | with article (la Pradera florida) |
| pt-BR | o Coelho, o Esquilo, o Ouriço | a Campina, a Floresta | with article |
| it | il Coniglio, lo Scoiattolo, il Riccio | il Prato, il Bosco | with article (l'Ansa del ruscello) |
| id | bare: Kelinci, Tupai, Landak | bare: Padang Rumput, Hutan | bare |
| tr | bare: Tavşan, Sincap, Kirpi | bare: Çayır, Orman | bare |

Important for T39B: `DailyVisitTextFormatter` fills {species}, {species_the}, {biome} and {biome_the} in every wish and
journal text, so the drafts swap them freely there (the check allows it). `ProgressionPresenter` replaces only the
exact token of each key ({biome} in biome.gate.title receives the `name_the` text, {biome} in biome.gate.species and
upgrades.production.name the bare name): those keys keep the English token.

Keys, and the form each language needs from T39B (« ok » = the draft already reads right with today's values):

| Key | English | de | pl | es-419 | pt-BR | it | id | tr |
|---|---|---|---|---|---|---|---|---|
| biome.gate.title | Open {biome} | ok (bare + « öffnen ») | ok (« biom: ») | ok | ok | ok | ok | ok (« biyomunu ») |
| zone.open.title | Open {zone} | ok (« {zone} öffnen ») | ok (colon) | ok | ok | ok | ok | ok (« bölgesini ») |
| biome.gate.species | {biome} species | ok (parentheses) | ok (parentheses) | « de {biome} »: « de la » reads better | as es | ok | ok | ok (compound) |
| upgrades.production.name, wish.026.production | {biome} Glow | compound form « Wiesen- » | ok (colon / parentheses) | ok | ok | ok | ok | ok |
| wish.002 | {biome} eggs | ok (parentheses) | ok (parentheses) | ok | ok | ok (parentheses) | ok | ok (compound) |
| wish.005, wish.073 | {species} mastery / constellation | ok (colon / compound) | ok (colon) | « de {species} »: « del Conejo » reads better | « do Coelho » reads better | ok (colon) | ok | ok (compound) |
| wish.006 | {species_the} in the Codex | accusative (given) | ok (colon) | ok | ok | ok | ok | ok (« türüne ») |
| wish.011 | the {biome} album | ok (colon) | ok (colon) | « de {biome} » | « de {biome} » | ok (colon) | ok | ok (compound) |
| wish.012 | {visitor} | accusative | ok (colon) | « a » + article → « al » | ok (« para ») | ok | ok | ok (« ile ») |
| wish.015, 016, 019, 023, 043, 047, 050 | {animal} | accusative (given) | ok (colon) | « a {animal} » → « al » (015, 016, 019, 023, 043, 050); « de {animal} » → « del » (047) | ok (direct object / « para ») | ok (direct object) | ok | ok (« için », « ile », colon) |
| wish.026, 026.nests, 026.curiosity, 028 | in {biome_the} | ok (parentheses) | ok (parentheses) | ok (« en la Pradera ») | ok (parentheses / « no bioma ») | ok (parentheses / « nel bioma ») | ok | ok (« biyomunda », colon); wish.026 « {upgrade} geliştir » would read better with the accusative « 'ı » |
| wish.034 | {n} {crop} | plural of the crop | ok (colon, ×{n}) | plural of the crop | plural | plural | ok | ok |
| wish.035 | Bring {animal} {n} {crop} | crop plural, « für » + accusative | « dla {animal} » genitive | crop plural, « a » → « al » | crop plural | crop plural | ok | ok |
| wish.038 | a {cake} for {animal} | « einen » needs the cake's gender | {cake} accusative, « dla » genitive | « un » needs the gender | « um » needs the gender | « un » needs the gender | ok | ok |
| wish.046 | Head to {destination} | « nach » for bare place names, else « in den / zum » | ok (colon) | « a » → « al » | ok (« para ») | ok (« per ») | ok | ok (« yönüne ») |
| activity.unlock.and / or | {first} and / or {second} | ok | ok | « y » becomes « e » before i-, « o » becomes « u » before o- | ok | ok | ok | ok |
| zone.locked.mastery, upgrades.locked.mastery, biome.mastery.up, upgrades.mastery.next, daily.calendar.tomorrow, reward.unlock, activity.hub.challenge_goal, upgrades.total | names after a colon or on their own | ok | ok | ok | ok | ok | ok | ok |

{visitor}, {crop}, {cake}, {destination} and {upgrade} are not filled by any code yet (future content): their names will
need the same noun forms.

## Lengths

Box sizes come from the UI pipelines (canvas 1080x2340; every label is one line unless noted, and only the hub's challenge goal
line ellipsizes: elsewhere a long text spills over its neighbours). For each label the maximum characters were estimated at the label's minimum
auto-size with the German glyph widths measured in Pocket Round and Pocket Plush; placeholders were filled with long
values (n = 10, the longest species and zone names, Giant Flower Watering Can for {reward}). The same estimate flags the
English and French overflows already known (« See you tomorrow » on the welcome card, the shop toast, « Nouveau » in
the summary pill), which supports it.

Already overflowing in English, so in every language: the summary cells (species name, about 8.7 characters; rarity,
about 9.4), the welcome card « See you tomorrow » (12.8), and the HUD egg name pill (20) with the Precious Meadow Egg.

German and Polish were shortened wherever a box was at risk (shop toasts, conditions, the carry line, the return lines,
the wish rows of the Today screen at about 39 characters, the upgrade card names, the level pill « St. {n} »). What is
still tight:

### de

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `reward.gems` | +10 Juwelen | 11 / 10.7 | level reward amount |
| `shop.restore.button` | Käufe wiederherstellen | 22 / 21.7 | Restore button |

Fits, but above 130 % of the English in a small box (under 30 characters):

| Key | Text | Chars (EN) / box |
|---|---|---|
| `activity.glide.name` | Gleitflug | 9 (5) / 26.6 |
| `activity.grand_contest.name` | Großer Wettbewerb | 17 (13) / 26.6 |
| `activity.grooming.sheet_groom` | Bürsten | 7 (5) / 12.8 |
| `activity.hub.free` | Gratis | 6 (4) / 10.7 |
| `activity.pause.quit` | Beenden | 7 (4) / 18.6 |
| `activity.result.back` | Zurück | 6 (4) / 13.6 |
| `activity.result.new_star` | Neuer Stern | 11 (8) / 20 |
| `activity.result.title0` | Schöner Versuch | 15 (8) / 22 |
| `activity.result.title2` | Super gemacht | 13 (9) / 22 |
| `biome.grassland.mastery` | Wiesen-Meisterschaft | 20 (14) / 28 |
| `daily.calendar.stamp` | Stempeln | 8 (5) / 13.7 |
| `daily.gift.big` | Großes Geschenk | 15 (8) / 22.7 |
| `daily.gift.nice` | Schönes Geschenk | 16 (9) / 22.7 |
| `daily.gift.small` | Kleines Geschenk | 16 (10) / 22.7 |
| `daily.welcome.title` | Willkommensgeschenk | 19 (12) / 28.7 |
| `egg.price.free` | Gratis | 6 (4) / 9 |
| `journal.claim` | Abholen | 7 (5) / 12.3 |
| `offline.welcome_back` | Willkommen zurück | 17 (12) / 26 |
| `rarity.common` | Gewöhnlich | 10 (6) / 14 |
| `rarity.epic` | Episch | 6 (4) / 14 |
| `rarity.rare` | Selten | 6 (4) / 14 |
| `rarity.uncommon` | Ungewöhnlich | 12 (8) / 14 |
| `refuge.source.biome_mastery` | Biom-Meisterschaft | 18 (13) / 29 |
| `refuge.source.traits` | Seltene Merkmale | 16 (11) / 29 |
| `return.subtitle` | Ein Geschenk für dich | 21 (14) / 26 |
| `return.subtitle_big` | Großes Geschenk für dich | 24 (18) / 26 |
| `return.title` | Willkommen zurück | 17 (12) / 26 |
| `reward.gems.one` | 10 Juwel | 8 (6) / 10.7 |
| `reward.gems.other` | 10 Juwelen | 10 (7) / 10.7 |
| `sheet.bond` | Bindung | 7 (4) / 9.5 |
| `sheet.treat` | Leckerli | 8 (5) / 12.8 |
| `shop.badge.owned` | Gekauft | 7 (5) / 8.8 |
| `species.rabbit.name` | Kaninchen | 9 (6) / 12.8 |
| `species.squirrel.name` | Eichhörnchen | 12 (8) / 12.8 |
| `today.claim` | Abholen | 7 (5) / 12.3 |
| `today.claim_all` | Alle abholen | 12 (9) / 18.9 |
| `today.close` | Schließen | 9 (5) / 15.5 |
| `ui.cancel.button` | Abbrechen | 9 (6) / 11.6 |
| `upgrades.locked.mastery` | Wiesen-Meisterschaft 3 | 22 (16) / 24 |
| `upgrades.max` | Maximum | 7 (5) / 24 |
| `upgrades.zone.next` | Nächste Zone | 12 (9) / 24 |
| `upgrades.zone.see` | Zeigen | 6 (3) / 7.7 |
| `variant.hedgehog.black.name` | Schwarz | 7 (5) / 12.8 |
| `variant.rabbit.black.name` | Schwarz | 7 (5) / 12.8 |
| `variant.squirrel.black.name` | Schwarz | 7 (5) / 12.8 |
| `wish.claim` | Abholen | 7 (5) / 12.3 |
| `wish.swap` | Tauschen | 8 (4) / 8.6 |
| `zone.open.button` | Öffnen | 6 (4) / 12.8 |

### pl

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `reward.gems` | Klejnoty +10 | 12 / 10.7 | level reward amount |
| `reward.gems.few` | 10 klejnoty | 11 / 10.7 | level reward amount |
| `reward.gems.many` | 10 klejnotów | 12 / 10.7 | level reward amount |
| `reward.gems.other` | 10 klejnotów | 12 / 10.7 | level reward amount |
| `upgrades.level` | Poz. 10 | 7 / 6 | level pill |

Fits, but above 130 % of the English in a small box (under 30 characters):

| Key | Text | Chars (EN) / box |
|---|---|---|
| `activity.baking.name` | Pieczenie | 9 (6) / 26.6 |
| `activity.glide.name` | Szybowanie | 10 (5) / 26.6 |
| `activity.grooming.name` | Pielęgnacja | 11 (8) / 26.6 |
| `activity.grooming.sheet_groom` | Szczotkuj | 9 (5) / 12.8 |
| `activity.hub.free` | Gratis | 6 (4) / 10.7 |
| `activity.lily_hop.name` | Skoki po liliach | 16 (12) / 26.6 |
| `activity.nut_drop.name` | Orzechowy deszcz | 16 (8) / 26.6 |
| `activity.result.new_star` | Nowa gwiazda | 12 (8) / 20 |
| `activity.result.title0` | Niezła próba | 12 (8) / 22 |
| `daily.calendar.stamp` | Stempluj | 8 (5) / 13.7 |
| `daily.gift.big` | Duży prezent | 12 (8) / 22.7 |
| `daily.gift.nice` | Fajny prezent | 13 (9) / 22.7 |
| `daily.welcome.title` | Prezent powitalny | 17 (12) / 28.7 |
| `egg.odds.title` | Szanse w jajku | 14 (8) / 22.5 |
| `egg.price.free` | Gratis | 6 (4) / 9 |
| `journal.claim` | Odbierz | 7 (5) / 12.3 |
| `rarity.common` | Pospolity | 9 (6) / 14 |
| `rarity.epic` | Epicki | 6 (4) / 14 |
| `rarity.rare` | Rzadki | 6 (4) / 14 |
| `rarity.uncommon` | Niepospolity | 12 (8) / 14 |
| `refuge.source.biome_mastery` | Mistrzostwo biomów | 18 (13) / 29 |
| `refuge.source.species_mastery` | Mistrzostwo gatunków | 20 (15) / 29 |
| `reward.gems.one` | 10 klejnot | 10 (6) / 10.7 |
| `sheet.treat` | Przysmak | 8 (5) / 12.8 |
| `shop.badge.owned` | Kupione | 7 (5) / 8.8 |
| `today.claim` | Odbierz | 7 (5) / 12.3 |
| `today.claim_all` | Odbierz wszystko | 16 (9) / 18.9 |
| `today.close` | Zamknij | 7 (5) / 15.5 |
| `upgrades.max` | Maksimum | 8 (5) / 24 |
| `upgrades.nests.name` | Ciepłe gniazdka | 15 (10) / 20 |
| `upgrades.qty.max` | Maks. | 5 (3) / 5.3 |
| `upgrades.zone.next` | Następna strefa | 15 (9) / 24 |
| `upgrades.zone.see` | Zobacz | 6 (3) / 7.7 |
| `variant.hedgehog.brown.name` | Brązowy | 7 (5) / 12.8 |
| `variant.hedgehog.cream.name` | Kremowy | 7 (5) / 12.8 |
| `variant.rabbit.brown.name` | Brązowy | 7 (5) / 12.8 |
| `variant.squirrel.autumn.name` | Jesienna | 8 (6) / 12.8 |
| `variant.squirrel.brown.name` | Brązowa | 7 (5) / 12.8 |
| `variant.squirrel.cream.name` | Kremowa | 7 (5) / 12.8 |
| `wish.claim` | Odbierz | 7 (5) / 12.3 |
| `wish.swap` | Zamień | 6 (4) / 8.6 |
| `wish.title` | Życzenia | 8 (6) / 21 |
| `zone.grassland.windmillrise.name` | Wiatrakowy pagórek | 18 (13) / 24 |
| `zone.open.button` | Otwórz | 6 (4) / 12.8 |

### es-419

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `egg.grassland.focus.name` | Huevo precioso de pradera | 25 / 20 | HUD egg pill |

49 more keys are above 130 % of the English but fit their box.

### pt-BR

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `egg.grassland.focus.name` | Ovo precioso da campina | 23 / 20 | HUD egg pill |

45 more keys are above 130 % of the English but fit their box.

### it

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `egg.grassland.focus.name` | Uovo prezioso del prato | 23 / 20 | HUD egg pill |
| `upgrades.level` | Liv. 10 | 7 / 6 | level pill |
| `upgrades.mastery.next` | Livello 10: Annaffiatoio fiorito gigante | 40 / 38.7 | mastery line |

66 more keys are above 130 % of the English but fit their box.

### id

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `biome.grassland.name` | Padang Rumput | 13 / 12.8 | theme pill (Codex header 20) |
| `egg.grassland.focus.name` | Telur Padang Rumput Istimewa | 28 / 20 | HUD egg pill |
| `reward.gems` | +10 Permata | 11 / 10.7 | level reward amount |

47 more keys are above 130 % of the English but fit their box.

### tr

Over the estimated box (characters at the minimum size, placeholders filled with long values):

| Key | Text | Chars / box | Where |
|---|---|---|---|
| `daily.gift.tomorrow` | Yarın görüşürüz | 15 / 12.8 | welcome card caption |
| `egg.grassland.focus.name` | Değerli Çayır Yumurtası | 23 / 20 | HUD egg pill |

45 more keys are above 130 % of the English but fit their box.

`check_drafts.py --lengths` also lists every key above 130 % of the English or any title above 18 characters, box or
not (de 168, es-419 222, pt-BR 178, it 263, id 124, tr 115, pl 158): most are short labels whose English is very short
(« Free », « Back ») in boxes that have room.

## Cultural adaptations

- All languages: the Discovery egg is an « Explorer egg » (Entdecker-Ei, Jajko Odkrywcy, Huevo explorador, Ovo
  Explorador, Uovo Esploratore, Telur Penjelajah, Kâşif Yumurtası): shorter and more natural than « discovery ».
- All languages: the « Glow » upgrade uses a plain light word (Leuchten, Blask, Brillo, Brilho, Chiarore, Pendar,
  Aydınlık), never a sparkle word.
- de: Juwelen for Gems (the usual German mobile word), Leckerli for treats, Kodex, Verstecken, Pfoten-Memo (« Memory »
  is a German trademark), Kuschelnester, Adleraugen, du-form.
- pl: Ostoja for the refuge (a haven for wild animals, warmer than « schronisko »), Bajeczny for Fabulous, maluszek for a
  newborn, Przytulanki for Cuddle Match, wiercipięta for the wiggler, « gwiazda » rather than « gwiazdka » (which also
  names a religious holiday), Łapkowe memory.
- es-419: neutral Latin American vocabulary (boletos, golosinas, moño, abrojos, canasta, lindo, video), ajolote for the
  axolotl (a much-loved Mexican animal), gender-neutral greetings (« ¡Hola de nuevo! », « Bienvenidos al refugio »),
  decimal point as in CLDR es-419 (× 1.25), Escondidas.
- pt-BR: figurinhas (the Brazilian sticker-album culture), petiscos, carrapichos, vitória-régia for Lily Pad Hop (the
  Amazon water lily), presentinho / presentão, « chocar » for hatch (the Pokémon GO Brazil word), ingressos for tickets,
  « Que bom te ver! » and « Boas-vindas » (no gender).
- it: figurine (the Italian sticker-album culture), bocconcini for treats, Nascondino, Occhio di lince, lappole for
  burrs, regalino / regalone, « Già tuo » for Owned, « Che bello rivederti! » and « Benvenuti » (no gender).
- id: Suaka for the refuge (as in suaka margasatwa, a protected haven), Petak Umpet, Lompat Lili Air (the usual word for
  water lily also names a flower on the forbidden list), HP for phone, casual « main », « mancing ».
- tr: Elmas for Gems (the Turkish mobile-game word for the premium currency), ikram for treats (hospitality), Tımar for
  Grooming, Saklambaç, Fındık Yağmuru (hazelnuts, as the French « noisettes »), Masalsı for Fabulous, pıtrak for burrs,
  the percent sign before the number (%12).

## Questions for native reviewers

- de: Kodex or Codex? Juwelen or Edelsteine? « Meisterschaft » for mastery (it can sound like a championship; « Meisterstufe »
  or « Können »)? « Dämmer- » for the Dusk decorations? Gewöhnlich / Ungewöhnlich are long for the summary cells:
  would « Häufig » be acceptable for Common? « Refugium » for the refuge, warm enough?
- pl: « Ostoja » for the refuge? « Stempluj » for the Stamp button (or « Przybij pieczątkę » if the box grows)?
  Pospolity / Niepospolity (Minecraft style) or Zwykły / Niezwykły? « Poz. » for the level pill? Are the colon wishes
  (« Wyszczotkuj: {animal} ») acceptable until T39B gives case forms? « Zmierzchowy » for Dusk?
- es-419: « Recibir » or « Reclamar » for Claim? boletos or fichas for tickets? stickers or estampas? « Eclosiona » used
  transitively (as Pokémon GO does)? Decimal point or comma for the shared Latin American Spanish?
- pt-BR: ingressos or fichas for tickets? « Escovação » for Grooming (or « Hora da escova »)? « Resgatar » for Claim?
- it: « Migliorie » or « Potenziamenti » for Upgrades (the box takes about 15 characters)? « Avvia una cucciolata » for
  starting a breeding? « Ideale » for Best pick?
- id: « Suaka » for the refuge, or something homelier? « Memukau » for Fabulous (top of the scale after Legendaris)?
  « Kurma Emas » (Golden Date): dates are tied to the fasting month in Indonesia; keep, or another golden fruit?
  « Padang Rumput » is 13 characters for the theme pill (12.8).
- tr: « Sığınak » for the refuge, or « Yuva » (warmer, but « Sıcak Yuvalar » is the nests upgrade)? « Elmas » or
  « Mücevher » for Gems? « Altın Hurma » (dates, same question as Indonesian)? « Akşam » for Dusk? « Görev » for an
  activity challenge?
- All: the Dusk decoration names are long in every language (« Sillón de musgo del ocaso »); is there a collection box
  for them yet?
