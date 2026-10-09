# Translation notes: agent TR3 (th, vi, ru, uk, el)

Draft of 9 October 2026. First-pass drafts of every current text (LOC_en, 719 keys) for Thai, Vietnamese, Russian,
Ukrainian and Greek, for the font team and for T39B « Langues : socle ». Written from the English with the French for
nuance and the code for context (Editor/UI/*Texts.cs, the UI pipelines, DailyVisitTextFormatter, ProgressionPresenter).

## Files

| Language | Draft | Keys | Plural forms | Glossary | Forbidden list | Characters |
|---|---|---|---|---|---|---|
| Thai | `Drafts/th.json` | 617 | other | `Glossary/th.md` | `Glossary/th_forbidden.txt` | `Drafts/charset_th.txt` (80) |
| Vietnamese | `Drafts/vi.json` | 617 | other | `Glossary/vi.md` | `Glossary/vi_forbidden.txt` | `Drafts/charset_vi.txt` (128) |
| Russian | `Drafts/ru.json` | 923 | one, few, many, other | `Glossary/ru.md` | `Glossary/ru_forbidden.txt` | `Drafts/charset_ru.txt` (83) |
| Ukrainian | `Drafts/uk.json` | 923 | one, few, many, other | `Glossary/uk.md` | `Glossary/uk_forbidden.txt` | `Drafts/charset_uk.txt` (82) |
| Greek | `Drafts/el.json` | 719 | one, other | `Glossary/el.md` | `Glossary/el_forbidden.txt` | `Drafts/charset_el.txt` (84) |

`python Localization/Drafts/check_drafts.py th vi ru uk el`: 0 errors, 0 warnings.

## Plurals

- 102 counted keys (a key with « .one » and « .other » in LOC_en; `upgrades.qty.one` is not one of them and stays).
- Russian and Ukrainian: one, few, many, other (PluralRules.cs, EastSlavic). The « one » form always shows {n}, because
  it also counts 21, 31, 101: « Вылупи {n} яйцо », not « Вылупи яйцо ». « other » (fractions in CLDR) takes the few
  form, the genitive singular. The animate accusative after 2 to 4 is written with the usual digit form (« Погладь 3
  зверька »).
- Greek: one, other; « one » says « ένα / μια / μία » instead of {n} where English drops the number.
- Thai and Vietnamese: other only; the « .one » keys are left out (the checker requires exactly the language's forms).
- The key without a suffix (read by a plain Get) carries the usual plural (many in Russian and Ukrainian), or the one
  form for what comes one at a time (Discovery egg, decoration, sticker, Flower Sugar), as in English.
- `reward.gems` (the key itself is « +{n} Gems »): « Самоцветы: +{n} » and « Самоцвіти: +{n} » so that it reads right for
  any number without GetPlural.
- Number-agnostic phrasing where an English text has a number but no plural pair: « Через {n} дн. » / « За {n} дн. »,
  « {n} мин », « Цветочный сахар ×{n} », « Собери урожай: {crop} ×{n} ».
- Russian and Ukrainian texts with a number but no plural pair, correct only for the numbers the game uses today:
  `shop.contents.gems` and `shop.purchase.thanks` (« {0} Самоцветов »: every Gem pack ends in 0), `zone.open.price`,
  `reward.light` and `reward.light_minutes.words` (Light is a mass noun in the genitive: « {price} Света »; wrong only
  for amounts ending in 1, which the game's large formatted amounts never are). T39B could make them counted keys.

## Keys with article or name placeholders

The names arrive in the nominative (Russian, Ukrainian), with their article (Greek), or bare (Thai, Vietnamese). The
drafts put each name where that form is right; T39B can then give per-language noun forms and smooth these sentences.

| Key | Placeholder | ru / uk | el | th / vi |
|---|---|---|---|---|
| `wish.006` | {species_the} | subject: « {species_the} ждёт тебя в Кодексе » | subject after the verb: « Στον Κατάλογο σε περιμένει {species_the} » | th « ดู{species_the}ในสมุดภาพ » with « น้องกระต่าย »; vi « chú Thỏ » |
| `wish.026`, `wish.026.nests`, `wish.026.curiosity`, `wish.028` | {biome_the} | in quotes after « в биоме » / « у біомі » | « για {biome_the} » and « Στόλισε {biome_the} με... » (accusative = nominative for the neuter biome names) | bare name |
| `species.*.name_the`, `biome.*.name_the` | (the forms themselves) | bare name | « το Κουνελάκι », « το Λιβάδι » (neuter) | th « น้อง » + species, bare biome; vi « chú » + species, bare biome |
| `zone.grassland.*.name` | (zone names, with « the » in English) | bare names | bare names, « Άνοιγμα: {zone} » | bare names |
| `zone.open.title` | {zone} | « Открыть: {zone} » / « Відкрити: {zone} » | « Άνοιγμα: {zone} » | plain |
| `biome.gate.title` | {biome} (filled with the name_the form) | « Открыть: {biome} » | « Άνοιξε {biome} » (« το Δάσος ») | plain |
| `biome.gate.species`, `upgrades.production.name` | {biome} | « {biome}: виды », « Сияние: {biome} » | same pattern | th compound « แสงเรือง{biome} »; vi « {biome} rạng rỡ » |
| `wish.002`, `wish.011`, `wish.026.production` | {biome} | « биома «{biome}» » | « ({biome}) », « «{biome}» » | plain |
| `wish.005`, `wish.073` | {species} | « вида «{species}» », « созвездие «{species}» » | colon, quotes | plain |
| `wish.015`, `wish.016`, `wish.019`, `wish.023`, `wish.035`, `wish.038`, `wish.043`, `wish.047`, `wish.050` | {animal} | subject: « {animal} хочет вкусняшку », « {animal} ждёт груминга », « Сфотографируй, как {animal} дремлет » (these wishes now read as the animal's own wish) | after a colon: « Μια λιχουδιά για: {animal} » (a pet's own name has no known gender for the article) | plain |
| `wish.012` | {visitor} | subject: « {visitor} хочет поздороваться » | colon | plain |
| `wish.046` | {destination} | colon: « Отправляйся в путь: {destination} » | colon | plain |
| `wish.034`, `wish.035` | {crop} | « {crop} ×{n} » | « {crop} ×{n} » | th « {crop} ×{n} »; vi « {n} {crop} » |
| `wish.038` | {cake} | quotes: « тортик «{cake}» » | quotes | plain |
| `wish.026` | {upgrade} | quotes: « Улучши «{upgrade}» » | quotes | plain |
| `zone.locked.mastery`, `upgrades.locked.mastery`, `biome.mastery.up` | {mastery} | « Сначала: {mastery}, ур. {level} » | « Πρώτα: {mastery}, επίπεδο {level} » | plain |

Russian and Ukrainian {mastery} is filled with « Мастерство луга » / « Майстерність лугу » (the genitive is inside the
fixed key, not in a placeholder).

## Cultural adaptations

- Thai « Lily Pad Hop » is « กระโดดข้ามบึง » (Pond Hop): the Thai word for lily pad and lotus is on the forbidden list
  (water-bloom rule), and so is the usual word for a watering can, so the Giant Flower Watering Can is a « กระป๋องรดน้ำ ».
- Thai sun and moon words avoid the honorific prefix of the forbidden list: Moon Honey Drops is « หยดน้ำผึ้งจันทรา ».
- Thai pets get « น้อง » in sentences (« ให้ขนมน้องกระต่าย »), a warm and very common Thai usage for animals.
- Thai Gems are « เจม », the word Thai players use, rather than a formal gem word.
- Vietnamese animals get « chú » (« chú Thỏ »); the refuge is « Mái ấm » (warm home), the usual word for an animal
  shelter in Vietnamese; Fabulous is « Tuyệt diệu » (the usual fantasy word for this tier uses a forbidden syllable).
- Russian and Ukrainian wishes with {animal} are phrased as the animal's wish (« {animal} хочет вкусняшку »), which
  fits the « Wishes » feature and keeps the name in the nominative.
- Russian Tile Puzzle is « Пятнашки » and Ukrainian « П'ятнашки », the familiar name of the sliding puzzle.
- Russian and Ukrainian variants are « окрас » / « забарвлення » (coat color), the natural word for an animal's colors;
  their adjectives agree with each species' gender (Белка is feminine: « Рыжая », « Кремовая »).
- Ukrainian uses its own words, not Russian ones: крамниця, бракує, смаколик, наліпка, звірятко, садок, конюшина,
  гойдалка, шпаківня, лійка, латаття, реп'яхи, « за {n} дн. » for « in {n} days ».
- Greek species are neuter diminutives (Κουνελάκι, Σκιουράκι, Σκαντζοχοιράκι): cuter, and their article is the same in
  the nominative and the accusative, which makes the placeholders safe. Expeditions are « Εκδρομές » (outings), cozier
  than « αποστολές ».
- Greek « wish » is « επιθυμία », not « ευχή », which also has a religious meaning.
- Greek « Weekly journal » is « Τετράδιο της εβδομάδας », because « ημερολόγιο » is already the calendar.
- Decoration lines « Dusk » are a name after the item: « «Сумерки» », « «Сутінки» », « «Σούρουπο» », « ยามสนธยา »,
  « Hoàng hôn ».
- Units and decimals follow each language: « × 1,25 » (ru, uk, el, vi), « × 1.25 » (th); « 50% » without a space.

## Questions for native reviewers

Russian
1. « Вылупи {n} яиц » (Hatch): colloquial transitive « вылупить » is common in games; is « Высиди » or another verb better?
2. Gems « Самоцветы » and Light « Свет », capitalized as in English: keep the capitals, or lowercase like most Russian
   games (« самоцветы », « свет »)?
3. « Груминг » for Grooming: natural for players, or « Причёсывание » / « Уход за шёрсткой »?
4. « Кодекс » for the Codex: keep, or « Справочник »?
5. Digit forms with animate nouns after 2 to 4 (« Погладь 3 зверька »): acceptable in UI, or « 3 зверьков »?

Ukrainian
1. « Вилупи {n} яєць »: natural, or « Виведи » / another verb?
2. « Садок » for the Garden activity and « Фруктовий сад » for the Orchard zone: clear enough?
3. « Грумінг » vs « Догляд за шерсткою »; « Мемо з лапками » for Pawprint Memory.
4. Apostrophe: straight ' (as now) or U+02BC?
5. « ОК » (Cyrillic) or « Гаразд » for the OK button?

Greek
1. Neuter diminutives for the species: charming, or too childish for a Greek adult player?
2. « Κατάλογος » for the Codex, « Βιότοπος » for biome, « Εκδρομές » for Expeditions: the right words?
3. The colon pattern with pet names (« Μια λιχουδιά για: {animal} »): acceptable until T39B gives articles?
4. « Παραμυθένιο » for Fabulous: does it read as a rarity tier?

Thai
1. Word breaks (U+200B): compound words are kept whole; please check the segmentation of long texts (`egg.odds.discovery`,
   `shop.purchase.pending`, `quality.thermal_notice`).
2. « ความปรารถนา » for Wishes: right register for a cute game, or « คำขอ »?
3. Rarities: ทั่วไป, พบไม่บ่อย, หายาก, ล้ำค่า, ในตำนาน, แสนวิเศษ: a clear ladder?
4. « หญ้าเจ้าชู้ » for burrs: does it read naturally in the grooming tips?

Vietnamese
1. « Ngọc » for Gems (and « ngọc trai » for a pearl in Treasure Fishing): any confusion?
2. « Mái ấm » for the refuge and « Sổ tay » for the Codex.
3. « Ghép ôm ấp » for Cuddle Match and « Lật hình dấu chân » for Pawprint Memory.
4. « Cho ăn » on the animal sheet's Treat button.

## Checker extensions (check_drafts.py, shared)

- Thai: a U+200B that splits a syllable is an error (before a following vowel or tone mark U+0E30 to U+0E3A, U+0E45,
  U+0E47 to U+0E4E; after a leading vowel U+0E40 to U+0E44), as are a doubled U+200B and one at either end.
- Greek: forbidden words match without accents or diaeresis, with final sigma read as sigma (`FOLD_ACCENTS`), so
  capitals and unaccented spellings are caught.
- The sparkle and em dash characters of the checker itself are written as escapes, as its comment says.

## Lengths

Width is the checker's measure (Latin, Cyrillic, Greek and Thai base letters count 1, marks 0). Over all texts: Russian
113 %, Ukrainian 114 %, Greek 122 %, Vietnamese 104 %, Thai 79 % of the English width. Below: every title wider than 18,
then every text over 160 % of the English, then the keys between 130 % and 160 % (titles and short buttons need a box
check in T39B with the real font).

### Thai (th): 18 tight keys

Titles wider than 18:

| Key | en | th | English | Translation |
|---|---|---|---|---|
| `decor.cloud_mobile_dusk.name` | 17 | 19 | Dusk Cloud Mobile | โมบายก้อนเมฆยามสนธยา |
| `decor.daisy_carousel_dusk.name` | 19 | 19 | Dusk Daisy Carousel | ม้าหมุนดอกเดซี่ยามสนธยา |
| `decor.giant_watering_can.name` | 25 | 21 | Giant Flower Watering Can | กระป๋องรดน้ำลายดอกไม้ยักษ์ |
| `decor.giant_watering_can_dusk.name` | 30 | 29 | Dusk Giant Flower Watering Can | กระป๋องรดน้ำลายดอกไม้ยักษ์ยามสนธยา |
| `decor.round_garden_lantern_dusk.name` | 25 | 22 | Dusk Round Garden Lantern | โคมไฟสวนทรงกลมยามสนธยา |
| `decor.round_greenhouse_dusk.name` | 21 | 23 | Dusk Round Greenhouse | เรือนกระจกทรงกลมยามสนธยา |
| `decor.sunflower_clock_dusk.name` | 20 | 20 | Dusk Sunflower Clock | นาฬิกาทานตะวันยามสนธยา |
| `decor.woven_linen_hammock_dusk.name` | 24 | 21 | Dusk Woven Linen Hammock | เปลญวนผ้าลินินถักยามสนธยา |

Over 160 %:

| Key | en | th | English | Translation |
|---|---|---|---|---|
| `sheet.bond` | 4 | 8 | Bond | ความผูกพัน |
| `ui.ok.button` | 2 | 4 | OK | ตกลง |
| `wish.title` | 6 | 11 | Wishes | ความปรารถนา |

Between 130 % and 160 %: `daily.calendar.stamp` (160%), `daily.gift.big` (138%), `reward.seeds` (144%), `reward.seeds.other` (144%), `reward.sticker` (136%), `toast.wish_done` (143%), `wish.done` (143%).

### Vietnamese (vi): 112 tight keys

Titles wider than 18:

| Key | en | vi | English | Translation |
|---|---|---|---|---|
| `decor.branch_swing_dusk.name` | 17 | 26 | Dusk Branch Swing | Xích đu cành cây Hoàng hôn |
| `decor.cloud_mobile_dusk.name` | 17 | 26 | Dusk Cloud Mobile | Đồ treo hình mây Hoàng hôn |
| `decor.daisy_carousel_dusk.name` | 19 | 27 | Dusk Daisy Carousel | Vòng quay hoa cúc Hoàng hôn |
| `decor.giant_watering_can.name` | 25 | 22 | Giant Flower Watering Can | Bình tưới hoa khổng lồ |
| `decor.giant_watering_can_dusk.name` | 30 | 32 | Dusk Giant Flower Watering Can | Bình tưới hoa khổng lồ Hoàng hôn |
| `decor.log_bridge_dusk.name` | 15 | 21 | Dusk Log Bridge | Cầu thân gỗ Hoàng hôn |
| `decor.moss_armchair_dusk.name` | 18 | 22 | Dusk Moss Armchair | Ghế bành rêu Hoàng hôn |
| `decor.pebble_fountain_dusk.name` | 20 | 27 | Dusk Pebble Fountain | Đài phun nước sỏi Hoàng hôn |
| `decor.round_garden_lantern_dusk.name` | 25 | 23 | Dusk Round Garden Lantern | Đèn vườn tròn Hoàng hôn |
| `decor.round_greenhouse_dusk.name` | 21 | 23 | Dusk Round Greenhouse | Nhà kính tròn Hoàng hôn |
| `decor.stump_shelf_dusk.name` | 16 | 20 | Dusk Stump Shelf | Kệ gốc cây Hoàng hôn |
| `decor.sunflower_clock.name` | 15 | 19 | Sunflower Clock | Đồng hồ hướng dương |
| `decor.sunflower_clock_dusk.name` | 20 | 29 | Dusk Sunflower Clock | Đồng hồ hướng dương Hoàng hôn |
| `decor.woven_linen_hammock_dusk.name` | 24 | 27 | Dusk Woven Linen Hammock | Võng vải lanh đan Hoàng hôn |
| `return.subtitle_big` | 18 | 23 | A big gift for you | Một món quà lớn cho bạn |
| `summary.title` | 21 | 20 | Welcome to the refuge | Chào mừng đến Mái ấm |

Over 160 %:

| Key | en | vi | English | Translation |
|---|---|---|---|---|
| `activity.grooming.sheet_groom` | 5 | 9 | Groom | Chải lông |
| `activity.grooming.tip.bow` | 10 | 17 | Pick a bow | Chọn một chiếc nơ |
| `activity.hide_seek.tip.hint` | 13 | 24 | A wiggly ear! | Một chiếc tai ngọ nguậy! |
| `activity.hide_seek.tip.paused` | 7 | 15 | Gently… | Nhẹ nhàng thôi… |
| `activity.hub.free` | 4 | 8 | Free | Miễn phí |
| `activity.result.back` | 4 | 8 | Back | Quay lại |
| `egg.odds.title` | 8 | 15 | Egg odds | Tỉ lệ của trứng |
| `egg.price.free` | 4 | 8 | Free | Miễn phí |
| `feature.shop` | 4 | 8 | Shop | Cửa hàng |
| `journal.030` | 16 | 30 | Harvest {n} dyes | Thu hoạch {n} loại thuốc nhuộm |
| `journal.030.other` | 16 | 30 | Harvest {n} dyes | Thu hoạch {n} loại thuốc nhuộm |
| `sheet.bond` | 4 | 10 | Bond | Thân thiết |
| `shop.title` | 4 | 8 | Shop | Cửa hàng |
| `treat.frost_berry.name` | 11 | 18 | Frost Berry | Quả mọng sương giá |
| `upgrades.max` | 5 | 9 | Maxed | Đã tối đa |
| `upgrades.qty.max` | 3 | 6 | Max | Tối đa |
| `variant.squirrel.red.name` | 3 | 6 | Red | Nâu đỏ |
| `wish.019` | 9 | 17 | Groom {animal} | Chải lông cho {animal} |
| `wish.036` | 13 | 21 | Harvest a dye | Thu hoạch thuốc nhuộm |
| `wish.037` | 11 | 20 | Bake a cake | Nướng một chiếc bánh |
| `wish.062` | 15 | 27 | Reel in a pearl | Câu được một viên ngọc trai |
| `wish.swap_used` | 34 | 55 | Free swap used. Next one tomorrow. | Đã dùng lượt đổi miễn phí. Lượt tiếp theo vào ngày mai. |
| `zone.locked.mastery` | 13 | 22 | {mastery} {level} first | Cần trước: {mastery} cấp {level} |

Between 130 % and 160 %: `activity.baking.name` (133%), `activity.glide.name` (160%), `activity.hide_seek.tip.find` (136%), `activity.pause.resume` (133%), `activity.pause.title` (133%), `activity.practice.line` (156%), `activity.result.title0` (150%), `challenge.grooming.01.name` (155%), `challenge.grooming.07.goal` (139%), `challenge.hide_seek.01.name` (155%), `challenge.hide_seek.02.goal` (136%), `challenge.hide_seek.02.name` (138%), `challenge.hide_seek.08.name` (156%), `challenge.hide_seek.10.goal` (133%), `daily.bonus.title` (150%), `daily.calendar.stamp` (160%), `decor.branch_swing.name` (133%), `decor.cloud_mobile.name` (133%), `entitlement.cosmetic.flower_crown.name` (133%), `hud.today` (140%), `journal.001` (136%), `journal.001.other` (136%), `journal.033` (147%), `journal.033.other` (147%), `journal.034` (133%), `journal.034.other` (133%), `journal.039` (143%), `journal.039.other` (143%), `journal.040` (136%), `journal.040.other` (136%), `journal.047` (140%), `journal.047.other` (140%), `journal.complete` (131%), `journal.each_goal` (133%), `offline.welcome_back` (142%), `rarity.epic` (150%), `refuge.level.rewards` (157%), `return.title` (142%), `reward.light` (133%), `reward.seeds` (144%), `reward.seeds.other` (144%), `shop.restore.none` (133%), `toast.gems_first` (138%), `toast.wish_done` (157%), `today.title` (140%), `upgrades.biome.next` (140%), `upgrades.locked.mastery` (157%), `upgrades.production.name` (138%), `upgrades.rate_after` (143%), `upgrades.tutorial` (152%), `upgrades.zone.next` (144%), `wish.001` (136%), `wish.001.other` (136%), `wish.009` (135%), `wish.014` (133%), `wish.014.other` (133%), `wish.029` (153%), `wish.040` (143%), `wish.040.other` (143%), `wish.041` (147%), `wish.045` (133%), `wish.046` (155%), `wish.059` (131%), `wish.059.other` (131%), `wish.068` (141%), `wish.068.other` (141%), `wish.076` (140%), `wish.076.other` (140%), `wish.done` (157%), `wish.t01` (136%), `wish.t01.other` (136%), `wish.title` (133%), `zone.grassland.start.name` (144%).

### Russian (ru): 230 tight keys

Titles wider than 18:

| Key | en | ru | English | Translation |
|---|---|---|---|---|
| `activity.lily_hop.name` | 12 | 19 | Lily Pad Hop | Прыжки по кувшинкам |
| `challenge.hide_seek.04.name` | 14 | 23 | Race the Clock | Наперегонки со временем |
| `daily.welcome.title` | 12 | 22 | Welcome gift | Приветственный подарок |
| `decor.birdhouse_dusk.name` | 14 | 20 | Dusk Birdhouse | Скворечник «Сумерки» |
| `decor.branch_swing_dusk.name` | 17 | 25 | Dusk Branch Swing | Качели из веток «Сумерки» |
| `decor.cloud_mobile_dusk.name` | 17 | 28 | Dusk Cloud Mobile | Мобиль с облачками «Сумерки» |
| `decor.daisy_carousel.name` | 14 | 19 | Daisy Carousel | Ромашковая карусель |
| `decor.daisy_carousel_dusk.name` | 19 | 29 | Dusk Daisy Carousel | Ромашковая карусель «Сумерки» |
| `decor.giant_watering_can.name` | 25 | 24 | Giant Flower Watering Can | Огромная цветочная лейка |
| `decor.giant_watering_can_dusk.name` | 30 | 34 | Dusk Giant Flower Watering Can | Огромная цветочная лейка «Сумерки» |
| `decor.log_bridge_dusk.name` | 15 | 28 | Dusk Log Bridge | Бревенчатый мостик «Сумерки» |
| `decor.moss_armchair_dusk.name` | 18 | 24 | Dusk Moss Armchair | Моховое кресло «Сумерки» |
| `decor.pebble_fountain_dusk.name` | 20 | 26 | Dusk Pebble Fountain | Фонтан из гальки «Сумерки» |
| `decor.round_garden_lantern.name` | 20 | 22 | Round Garden Lantern | Круглый садовый фонарь |
| `decor.round_garden_lantern_dusk.name` | 25 | 32 | Dusk Round Garden Lantern | Круглый садовый фонарь «Сумерки» |
| `decor.round_greenhouse_dusk.name` | 21 | 25 | Dusk Round Greenhouse | Круглая теплица «Сумерки» |
| `decor.stump_shelf_dusk.name` | 16 | 21 | Dusk Stump Shelf | Полка-пенёк «Сумерки» |
| `decor.sunflower_clock_dusk.name` | 20 | 24 | Dusk Sunflower Clock | Часы-подсолнух «Сумерки» |
| `decor.woven_linen_hammock.name` | 19 | 22 | Woven Linen Hammock | Плетёный льняной гамак |
| `decor.woven_linen_hammock_dusk.name` | 24 | 32 | Dusk Woven Linen Hammock | Плетёный льняной гамак «Сумерки» |
| `egg.grassland.focus.name` | 19 | 24 | Precious Meadow Egg | Драгоценное луговое яйцо |
| `return.subtitle_big` | 18 | 24 | A big gift for you | Большой подарок для тебя |
| `summary.title` | 21 | 24 | Welcome to the refuge | Добро пожаловать в приют |
| `treat.seed_cookie.name` | 11 | 19 | Seed Cookie | Печенье с семечками |
| `treat.watercress_cookie.name` | 17 | 23 | Watercress Cookie | Печенье с кресс-салатом |

Over 160 %:

| Key | en | ru | English | Translation |
|---|---|---|---|---|
| `activity.grooming.sheet_groom` | 5 | 9 | Groom | Причесать |
| `activity.hub.free` | 4 | 9 | Free | Бесплатно |
| `activity.hub.new` | 3 | 7 | New | Новинка |
| `activity.nut_drop.name` | 8 | 14 | Nut Drop | Ореховый дождь |
| `activity.pause.resume` | 6 | 10 | Resume | Продолжить |
| `activity.result.title0` | 8 | 16 | Nice try | Неплохая попытка |
| `challenge.hide_seek.02.name` | 8 | 13 | No Hints | Без подсказок |
| `codex.new` | 3 | 7 | New | Новинка |
| `daily.calendar.big_today` | 13 | 22 | Big box today | Большая клетка сегодня |
| `daily.gift.big` | 8 | 15 | Big gift | Большой подарок |
| `daily.gift.nice` | 9 | 15 | Nice gift | Хороший подарок |
| `daily.gift.open` | 4 | 7 | Open | Открыть |
| `daily.gift.small` | 10 | 17 | Small gift | Маленький подарок |
| `decor.log_bridge.name` | 10 | 18 | Log Bridge | Бревенчатый мостик |
| `egg.price.free` | 4 | 9 | Free | Бесплатно |
| `feature.shop` | 4 | 7 | Shop | Магазин |
| `journal.032.one` | 12 | 20 | Give a treat | Раздай {n} вкусняшку |
| `journal.last_day` | 8 | 14 | Last day | Последний день |
| `rarity.epic` | 4 | 9 | Epic | Эпический |
| `reward.gems` | 9 | 15 | +{n} Gems | Самоцветы: +{n} |
| `reward.gems.few` | 8 | 13 | {n} Gems | {n} Самоцвета |
| `reward.gems.many` | 8 | 14 | {n} Gems | {n} Самоцветов |
| `reward.gems.one` | 7 | 12 | {n} Gem | {n} Самоцвет |
| `reward.gems.other` | 8 | 13 | {n} Gems | {n} Самоцвета |
| `shop.contents.gems` | 8 | 14 | {0} Gems | {0} Самоцветов |
| `shop.section.gems` | 4 | 9 | Gems | Самоцветы |
| `shop.title` | 4 | 7 | Shop | Магазин |
| `summary.new` | 3 | 7 | New | Новинка |
| `treat.star_candy.name` | 10 | 17 | Star Candy | Звёздная конфетка |
| `upgrades.locked.mastery` | 7 | 12 | {mastery} {level} | {mastery}, ур. {level} |
| `upgrades.qty.max` | 3 | 5 | Max | Макс. |
| `upgrades.zone.see` | 3 | 8 | See | Смотреть |
| `variant.hedgehog.brown.name` | 5 | 10 | Brown | Коричневый |
| `variant.rabbit.brown.name` | 5 | 10 | Brown | Коричневый |
| `variant.squirrel.brown.name` | 5 | 10 | Brown | Коричневая |
| `variant.squirrel.red.name` | 3 | 5 | Red | Рыжая |
| `wish.002.one` | 15 | 27 | Hatch a {biome} egg | Вылупи {n} яйцо биома «{biome}» |
| `wish.012` | 13 | 23 | Say hi to {visitor} | {visitor} хочет поздороваться |
| `wish.017.one` | 12 | 20 | Give a treat | Раздай {n} вкусняшку |
| `wish.019` | 9 | 17 | Groom {animal} | {animal} ждёт груминга |
| `wish.023` | 12 | 21 | Dress up {animal} | {animal} хочет новый наряд |
| `wish.046` | 11 | 23 | Head to {destination} | Отправляйся в путь: {destination} |
| `wish.054` | 13 | 26 | Play Nut Drop | Поиграй в «Ореховый дождь» |
| `wish.063` | 17 | 31 | Play Lily Pad Hop | Поиграй в «Прыжки по кувшинкам» |
| `wish.068.one` | 14 | 26 | Make a big hug | Сделай {n} крепкое объятие |
| `wish.069` | 10 | 17 | Play Glide | Поиграй в «Полёт» |
| `wish.swap` | 4 | 7 | Swap | Сменить |
| `zone.locked.mastery` | 13 | 21 | {mastery} {level} first | Сначала: {mastery}, ур. {level} |
| `zone.open.button` | 4 | 7 | Open | Открыть |

Between 130 % and 160 %: `activity.hide_seek.tip.find` (145%), `activity.hide_seek.tip.paused` (157%), `activity.hub.best` (138%), `activity.hub.challenge_pick` (142%), `activity.hub.play` (150%), `activity.practice.line` (131%), `activity.result.new_star` (150%), `activity.result.practice` (138%), `activity.test.name` (144%), `activity.tickets.empty.need_gems` (137%), `activity.tickets.next_h` (135%), `activity.tickets.next_m` (132%), `activity.unlock.axolotl` (150%), `biome.gate.title` (150%), `challenge.grooming.01.name` (136%), `challenge.grooming.06.name` (140%), `challenge.grooming.07.name` (133%), `challenge.hide_seek.02.goal` (132%), `challenge.hide_seek.03.name` (155%), `daily.bonus.body` (133%), `daily.bonus.title` (160%), `daily.calendar.big_tomorrow` (131%), `daily.calendar.stamp` (160%), `decor.cloud_mobile.name` (150%), `hud.play` (150%), `hud.today` (140%), `journal.003.one` (145%), `journal.005.one` (135%), `journal.006.one` (141%), `journal.007.one` (136%), `journal.008.one` (133%), `journal.010.one` (140%), `journal.014.one` (144%), `journal.017.one` (155%), `journal.020.one` (142%), `journal.026.one` (138%), `journal.030` (131%), `journal.030.many` (131%), `journal.030.one` (154%), `journal.031.one` (147%), `journal.032` (133%), `journal.032.few` (133%), `journal.032.many` (133%), `journal.032.other` (133%), `journal.034` (133%), `journal.034.many` (133%), `journal.034.one` (146%), `journal.038.one` (150%), `journal.039` (136%), `journal.039.many` (136%), `journal.039.one` (155%), `journal.040` (136%), `journal.040.many` (136%), `journal.040.one` (155%), `journal.044` (152%), `journal.045.one` (133%), `journal.046` (135%), `journal.046.few` (140%), `journal.046.many` (135%), `journal.046.one` (156%), `journal.046.other` (140%), `journal.047.one` (142%), `journal.051` (133%), `journal.051.many` (133%), `journal.051.one` (150%), `journal.claim` (140%), `journal.days_left` (131%), `journal.days_left.many` (131%), `journal.days_left.one` (133%), `journal.each_goal` (156%), `rarity.rare` (150%), `refuge.source.biome_mastery` (131%), `reward.seeds.one` (138%), `reward.treats.one` (144%), `sheet.bond` (150%), `sheet.treat` (160%), `shop.badge.best_value` (140%), `shop.badge.owned` (140%), `shop.not_enough_gems` (138%), `shop.one_time` (136%), `shop.purchase.failed` (133%), `shop.section.featured` (138%), `toast.gems_first` (138%), `today.claim` (140%), `today.close` (140%), `today.title` (140%), `treat.cherry_bite.name` (145%), `treat.frost_berry.name` (136%), `treat.golden_treat.name` (142%), `treat.treat_basket.name` (150%), `upgrades.best` (133%), `upgrades.biome.next` (140%), `upgrades.curiosity.name` (155%), `upgrades.max` (160%), `upgrades.nests.name` (160%), `upgrades.production.name` (138%), `upgrades.zone.next` (156%), `variant.hedgehog.cream.name` (160%), `variant.squirrel.cream.name` (160%), `wish.002` (144%), `wish.002.few` (150%), `wish.002.many` (144%), `wish.002.other` (150%), `wish.005` (140%), `wish.011` (139%), `wish.014` (133%), `wish.014.many` (133%), `wish.014.one` (146%), `wish.017` (133%), `wish.017.few` (133%), `wish.017.many` (133%), `wish.017.other` (133%), `wish.018.one` (147%), `wish.020.one` (143%), `wish.026` (144%), `wish.026.curiosity` (154%), `wish.026.nests` (156%), `wish.026.production` (156%), `wish.028.one` (140%), `wish.031.one` (150%), `wish.034` (153%), `wish.034.few` (153%), `wish.034.many` (153%), `wish.034.one` (153%), `wish.034.other` (153%), `wish.040` (136%), `wish.040.many` (136%), `wish.040.one` (155%), `wish.050` (136%), `wish.052` (135%), `wish.052.few` (130%), `wish.052.many` (135%), `wish.052.one` (155%), `wish.052.other` (130%), `wish.055.one` (130%), `wish.056` (136%), `wish.056.few` (136%), `wish.056.many` (136%), `wish.056.one` (136%), `wish.056.other` (136%), `wish.057` (140%), `wish.059.one` (148%), `wish.065` (133%), `wish.065.many` (133%), `wish.065.one` (150%), `wish.068` (153%), `wish.068.few` (153%), `wish.068.many` (153%), `wish.068.other` (153%), `wish.076.one` (142%), `wish.077.one` (156%), `wish.079` (130%), `wish.083.one` (133%), `wish.claim` (140%), `wish.swap_used` (141%), `zone.open.title` (150%).

### Ukrainian (uk): 233 tight keys

Titles wider than 18:

| Key | en | uk | English | Translation |
|---|---|---|---|---|
| `activity.tickets.empty.title` | 18 | 19 | Tickets on the way | Квитки вже в дорозі |
| `challenge.hide_seek.04.name` | 14 | 20 | Race the Clock | Наввипередки з часом |
| `daily.welcome.title` | 12 | 19 | Welcome gift | Вітальний подарунок |
| `decor.branch_swing_dusk.name` | 17 | 26 | Dusk Branch Swing | Гойдалка з гілок «Сутінки» |
| `decor.cloud_mobile.name` | 12 | 20 | Cloud Mobile | Мобіль із хмаринками |
| `decor.cloud_mobile_dusk.name` | 17 | 30 | Dusk Cloud Mobile | Мобіль із хмаринками «Сутінки» |
| `decor.daisy_carousel_dusk.name` | 19 | 28 | Dusk Daisy Carousel | Ромашкова карусель «Сутінки» |
| `decor.giant_watering_can.name` | 25 | 24 | Giant Flower Watering Can | Величезна квіткова лійка |
| `decor.giant_watering_can_dusk.name` | 30 | 34 | Dusk Giant Flower Watering Can | Величезна квіткова лійка «Сутінки» |
| `decor.log_bridge_dusk.name` | 15 | 25 | Dusk Log Bridge | Місток із колод «Сутінки» |
| `decor.moss_armchair_dusk.name` | 18 | 23 | Dusk Moss Armchair | Мохове крісло «Сутінки» |
| `decor.pebble_fountain_dusk.name` | 20 | 26 | Dusk Pebble Fountain | Фонтан із гальки «Сутінки» |
| `decor.round_garden_lantern.name` | 20 | 22 | Round Garden Lantern | Круглий садовий ліхтар |
| `decor.round_garden_lantern_dusk.name` | 25 | 32 | Dusk Round Garden Lantern | Круглий садовий ліхтар «Сутінки» |
| `decor.round_greenhouse_dusk.name` | 21 | 24 | Dusk Round Greenhouse | Кругла теплиця «Сутінки» |
| `decor.stump_shelf_dusk.name` | 16 | 23 | Dusk Stump Shelf | Полиця-пеньок «Сутінки» |
| `decor.sunflower_clock_dusk.name` | 20 | 27 | Dusk Sunflower Clock | Годинник-соняшник «Сутінки» |
| `decor.woven_linen_hammock.name` | 19 | 21 | Woven Linen Hammock | Плетений лляний гамак |
| `decor.woven_linen_hammock_dusk.name` | 24 | 31 | Dusk Woven Linen Hammock | Плетений лляний гамак «Сутінки» |
| `egg.grassland.focus.name` | 19 | 23 | Precious Meadow Egg | Дорогоцінне лугове яйце |
| `return.subtitle_big` | 18 | 26 | A big gift for you | Великий подарунок для тебе |
| `summary.title` | 21 | 27 | Welcome to the refuge | Ласкаво просимо до притулку |
| `treat.moon_honey.name` | 16 | 21 | Moon Honey Drops | Краплі місячного меду |
| `treat.watercress_cookie.name` | 17 | 21 | Watercress Cookie | Печиво з крес-салатом |

Over 160 %:

| Key | en | uk | English | Translation |
|---|---|---|---|---|
| `activity.grooming.sheet_groom` | 5 | 9 | Groom | Причесати |
| `activity.hub.free` | 4 | 11 | Free | Безкоштовно |
| `activity.hub.new` | 3 | 7 | New | Новинка |
| `activity.nut_drop.name` | 8 | 13 | Nut Drop | Горіховий дощ |
| `activity.pause.resume` | 6 | 10 | Resume | Продовжити |
| `biome.gate.title` | 8 | 13 | Open {biome} | Відкрити: {biome} |
| `challenge.grooming.01.name` | 11 | 18 | First Brush | Перше розчісування |
| `codex.new` | 3 | 7 | New | Новинка |
| `daily.bonus.title` | 10 | 18 | Bonus gift | Бонусний подарунок |
| `daily.calendar.big_today` | 13 | 24 | Big box today | Велика клітинка сьогодні |
| `daily.calendar.stamp` | 5 | 9 | Stamp | Позначити |
| `daily.gift.big` | 8 | 17 | Big gift | Великий подарунок |
| `daily.gift.nice` | 9 | 16 | Nice gift | Гарний подарунок |
| `daily.gift.open` | 4 | 8 | Open | Відкрити |
| `daily.gift.small` | 10 | 19 | Small gift | Маленький подарунок |
| `egg.price.free` | 4 | 11 | Free | Безкоштовно |
| `feature.shop` | 4 | 8 | Shop | Крамниця |
| `journal.040.one` | 11 | 18 | Give a cake | Подаруй {n} тортик |
| `journal.last_day` | 8 | 13 | Last day | Останній день |
| `rarity.epic` | 4 | 7 | Epic | Епічний |
| `rarity.rare` | 4 | 9 | Rare | Рідкісний |
| `reward.gems` | 9 | 15 | +{n} Gems | Самоцвіти: +{n} |
| `reward.gems.few` | 8 | 13 | {n} Gems | {n} Самоцвіти |
| `reward.gems.many` | 8 | 14 | {n} Gems | {n} Самоцвітів |
| `reward.gems.one` | 7 | 12 | {n} Gem | {n} Самоцвіт |
| `reward.gems.other` | 8 | 13 | {n} Gems | {n} Самоцвіти |
| `sheet.treat` | 5 | 10 | Treat | Пригостити |
| `shop.contents.gems` | 8 | 14 | {0} Gems | {0} Самоцвітів |
| `shop.section.gems` | 4 | 9 | Gems | Самоцвіти |
| `shop.title` | 4 | 8 | Shop | Крамниця |
| `summary.new` | 3 | 7 | New | Новинка |
| `upgrades.best` | 9 | 15 | Best pick | Найкращий вибір |
| `upgrades.locked.mastery` | 7 | 13 | {mastery} {level} | {mastery}, рів. {level} |
| `upgrades.nests.name` | 10 | 17 | Cozy Nests | Затишні гніздечка |
| `upgrades.qty.max` | 3 | 5 | Max | Макс. |
| `upgrades.zone.see` | 3 | 7 | See | Глянути |
| `variant.hedgehog.brown.name` | 5 | 10 | Brown | Коричневий |
| `variant.rabbit.brown.name` | 5 | 10 | Brown | Коричневий |
| `variant.squirrel.brown.name` | 5 | 9 | Brown | Коричнева |
| `wish.002.one` | 15 | 27 | Hatch a {biome} egg | Вилупи {n} яйце біому «{biome}» |
| `wish.019` | 9 | 20 | Groom {animal} | {animal} чекає на грумінг |
| `wish.023` | 12 | 21 | Dress up {animal} | {animal} хоче нове вбрання |
| `wish.046` | 11 | 21 | Head to {destination} | Вирушай у мандри: {destination} |
| `wish.054` | 13 | 24 | Play Nut Drop | Пограй у «Горіховий дощ» |
| `wish.063` | 17 | 29 | Play Lily Pad Hop | Пограй у «Стрибки по лататті» |
| `wish.068.one` | 14 | 26 | Make a big hug | Зроби {n} велику обіймашку |
| `wish.swap` | 4 | 7 | Swap | Змінити |
| `zone.locked.mastery` | 13 | 21 | {mastery} {level} first | Спершу: {mastery}, рів. {level} |
| `zone.open.button` | 4 | 8 | Open | Відкрити |
| `zone.open.title` | 8 | 13 | Open {zone} | Відкрити: {zone} |

Between 130 % and 160 %: `activity.hide_seek.tip.hint` (131%), `activity.hub.best` (138%), `activity.hub.challenge_pick` (150%), `activity.lily_hop.name` (150%), `activity.practice.line` (150%), `activity.result.title0` (150%), `activity.tickets.empty.need_gems` (132%), `activity.tickets.next_h` (135%), `activity.unlock.axolotl` (144%), `challenge.grooming.07.name` (133%), `challenge.hide_seek.02.name` (150%), `challenge.hide_seek.03.name` (136%), `challenge.hide_seek.08.name` (133%), `codex.variants` (138%), `daily.calendar.big_tomorrow` (138%), `daily.gift.info` (134%), `decor.branch_swing.name` (133%), `decor.log_bridge.name` (150%), `egg.odds.new_color` (144%), `entitlement.cosmetic.flower_crown.name` (142%), `hud.today` (160%), `journal.003` (135%), `journal.003.few` (130%), `journal.003.many` (135%), `journal.003.one` (155%), `journal.003.other` (130%), `journal.005.one` (135%), `journal.007.one` (136%), `journal.008.one` (138%), `journal.010.one` (145%), `journal.014.one` (144%), `journal.017.one` (155%), `journal.020` (131%), `journal.020.many` (131%), `journal.020.one` (142%), `journal.022.one` (147%), `journal.026.one` (133%), `journal.030.one` (131%), `journal.032` (140%), `journal.032.few` (133%), `journal.032.many` (140%), `journal.032.one` (158%), `journal.032.other` (133%), `journal.033.one` (133%), `journal.034` (133%), `journal.034.few` (133%), `journal.034.many` (133%), `journal.034.one` (154%), `journal.034.other` (133%), `journal.036.one` (136%), `journal.038.one` (143%), `journal.039.one` (145%), `journal.040` (143%), `journal.040.few` (136%), `journal.040.many` (143%), `journal.040.other` (136%), `journal.044` (157%), `journal.045.one` (137%), `journal.046.one` (139%), `journal.047.one` (158%), `journal.051` (133%), `journal.051.many` (133%), `journal.051.one` (150%), `journal.claim` (140%), `journal.days_left` (131%), `journal.days_left.many` (131%), `journal.days_left.one` (133%), `journal.each_goal` (144%), `plant.sweet_clover.name` (133%), `rarity.common` (150%), `rarity.uncommon` (138%), `refuge.source.biome_mastery` (146%), `refuge.source.variants` (138%), `return.line.calendar` (133%), `reveal.new_variant` (142%), `reward.seeds` (133%), `reward.seeds.few` (133%), `reward.seeds.many` (133%), `reward.seeds.one` (150%), `reward.seeds.other` (133%), `reward.treats` (140%), `reward.treats.many` (140%), `reward.treats.one` (133%), `sheet.bond` (150%), `sheet.details.button` (143%), `shop.badge.owned` (160%), `toast.gems_first` (131%), `toast.wish_done` (136%), `today.claim` (140%), `today.close` (140%), `today.title` (160%), `treat.cherry_bite.name` (155%), `treat.golden_treat.name` (133%), `treat.mango_bite.name` (140%), `treat.seed_cookie.name` (155%), `treat.star_candy.name` (150%), `treat.treat_basket.name` (133%), `ui.cancel.button` (150%), `upgrades.biome.next` (140%), `upgrades.curiosity.name` (155%), `upgrades.max` (160%), `upgrades.zone.next` (144%), `variant.hedgehog.cream.name` (160%), `variant.squirrel.cream.name` (140%), `wish.002` (150%), `wish.002.few` (150%), `wish.002.many` (150%), `wish.002.other` (150%), `wish.004` (133%), `wish.005` (140%), `wish.011` (144%), `wish.012` (154%), `wish.014` (133%), `wish.014.few` (133%), `wish.014.many` (133%), `wish.014.one` (154%), `wish.014.other` (133%), `wish.017` (140%), `wish.017.few` (133%), `wish.017.many` (140%), `wish.017.one` (158%), `wish.017.other` (133%), `wish.020.one` (130%), `wish.025.one` (136%), `wish.026` (144%), `wish.026.curiosity` (154%), `wish.026.nests` (160%), `wish.026.production` (150%), `wish.028.one` (132%), `wish.031.one` (158%), `wish.034` (147%), `wish.034.few` (147%), `wish.034.many` (147%), `wish.034.one` (147%), `wish.034.other` (147%), `wish.038` (139%), `wish.040.one` (145%), `wish.052.one` (135%), `wish.056` (132%), `wish.056.few` (132%), `wish.056.many` (132%), `wish.056.one` (132%), `wish.056.other` (132%), `wish.059.one` (136%), `wish.065` (133%), `wish.065.many` (133%), `wish.065.one` (150%), `wish.068` (159%), `wish.068.few` (153%), `wish.068.many` (159%), `wish.068.other` (153%), `wish.069` (160%), `wish.076.one` (158%), `wish.077.one` (150%), `wish.079` (130%), `wish.083.one` (137%), `wish.claim` (140%), `wish.done` (136%), `wish.swap_used` (141%).

### Greek (el): 268 tight keys

Titles wider than 18:

| Key | en | el | English | Translation |
|---|---|---|---|---|
| `activity.burrow_maze.name` | 11 | 21 | Burrow Maze | Λαβύρινθος της φωλιάς |
| `activity.firefly_rhythm.name` | 14 | 23 | Firefly Rhythm | Ρυθμός των πυγολαμπίδων |
| `activity.grand_contest.name` | 13 | 19 | Grand Contest | Μεγάλος διαγωνισμός |
| `activity.memory.name` | 15 | 19 | Pawprint Memory | Μνήμη με πατουσάκια |
| `activity.tickets.empty.title` | 18 | 21 | Tickets on the way | Τα εισιτήρια έρχονται |
| `challenge.grooming.09.name` | 14 | 20 | Stubborn Burrs | Επίμονες κολλιτσίδες |
| `challenge.hide_seek.09.name` | 14 | 21 | Shifting Spots | Κρυψώνες που αλλάζουν |
| `daily.welcome.title` | 12 | 19 | Welcome gift | Δώρο καλωσορίσματος |
| `decor.birdhouse_dusk.name` | 14 | 29 | Dusk Birdhouse | Σπιτάκι για πουλιά «Σούρουπο» |
| `decor.branch_swing_dusk.name` | 17 | 28 | Dusk Branch Swing | Κούνια από κλαδιά «Σούρουπο» |
| `decor.cloud_mobile.name` | 12 | 22 | Cloud Mobile | Κρεμαστό με συννεφάκια |
| `decor.cloud_mobile_dusk.name` | 17 | 33 | Dusk Cloud Mobile | Κρεμαστό με συννεφάκια «Σούρουπο» |
| `decor.daisy_carousel.name` | 14 | 22 | Daisy Carousel | Καρουζέλ με μαργαρίτες |
| `decor.daisy_carousel_dusk.name` | 19 | 33 | Dusk Daisy Carousel | Καρουζέλ με μαργαρίτες «Σούρουπο» |
| `decor.giant_watering_can.name` | 25 | 29 | Giant Flower Watering Can | Γιγάντιο λουλουδάτο ποτιστήρι |
| `decor.giant_watering_can_dusk.name` | 30 | 40 | Dusk Giant Flower Watering Can | Γιγάντιο λουλουδάτο ποτιστήρι «Σούρουπο» |
| `decor.log_bridge.name` | 10 | 20 | Log Bridge | Γεφυράκι από κορμούς |
| `decor.log_bridge_dusk.name` | 15 | 31 | Dusk Log Bridge | Γεφυράκι από κορμούς «Σούρουπο» |
| `decor.moss_armchair_dusk.name` | 18 | 29 | Dusk Moss Armchair | Πολυθρόνα από βρύα «Σούρουπο» |
| `decor.pebble_fountain.name` | 15 | 21 | Pebble Fountain | Σιντριβάνι με βότσαλα |
| `decor.pebble_fountain_dusk.name` | 20 | 32 | Dusk Pebble Fountain | Σιντριβάνι με βότσαλα «Σούρουπο» |
| `decor.round_garden_lantern.name` | 20 | 22 | Round Garden Lantern | Στρογγυλό φανάρι κήπου |
| `decor.round_garden_lantern_dusk.name` | 25 | 33 | Dusk Round Garden Lantern | Στρογγυλό φανάρι κήπου «Σούρουπο» |
| `decor.round_greenhouse.name` | 16 | 20 | Round Greenhouse | Στρογγυλό θερμοκήπιο |
| `decor.round_greenhouse_dusk.name` | 21 | 31 | Dusk Round Greenhouse | Στρογγυλό θερμοκήπιο «Σούρουπο» |
| `decor.stump_shelf_dusk.name` | 16 | 29 | Dusk Stump Shelf | Ράφι από κούτσουρο «Σούρουπο» |
| `decor.sunflower_clock_dusk.name` | 20 | 27 | Dusk Sunflower Clock | Ρολόι ηλιοτρόπιο «Σούρουπο» |
| `decor.woven_linen_hammock_dusk.name` | 24 | 28 | Dusk Woven Linen Hammock | Πλεκτή λινή αιώρα «Σούρουπο» |
| `egg.grassland.focus.name` | 19 | 26 | Precious Meadow Egg | Πολύτιμο αυγό του Λιβαδιού |
| `entitlement.cosmetic.flower_crown.name` | 12 | 19 | Flower Crown | Λουλουδένιο στεφάνι |
| `journal.title` | 14 | 22 | Weekly journal | Τετράδιο της εβδομάδας |
| `return.subtitle_big` | 18 | 24 | A big gift for you | Ένα μεγάλο δώρο για σένα |
| `summary.title` | 21 | 25 | Welcome to the refuge | Καλώς ήρθες στο καταφύγιο |
| `treat.moon_honey.name` | 16 | 21 | Moon Honey Drops | Σταγόνες φεγγαρόμελου |
| `treat.seed_cookie.name` | 11 | 19 | Seed Cookie | Μπισκότο με σπόρους |
| `treat.toasted_hazelnut.name` | 16 | 23 | Toasted Hazelnut | Καβουρδισμένο φουντούκι |
| `treat.treat_basket.name` | 12 | 21 | Treat Basket | Καλαθάκι με λιχουδιές |
| `treat.watercress_cookie.name` | 17 | 23 | Watercress Cookie | Μπισκότο με νεροκάρδαμο |

Over 160 %:

| Key | en | el | English | Translation |
|---|---|---|---|---|
| `activity.baking.name` | 6 | 14 | Baking | Ζαχαροπλαστική |
| `activity.grooming.sheet_groom` | 5 | 10 | Groom | Περιποίηση |
| `activity.hide_seek.tip.hint` | 13 | 21 | A wiggly ear! | Ένα αυτάκι κουνιέται! |
| `activity.nut_drop.name` | 8 | 17 | Nut Drop | Βροχή φουντουκιών |
| `activity.result.title0` | 8 | 15 | Nice try | Καλή προσπάθεια |
| `activity.test.name` | 9 | 16 | Test game | Παιχνίδι δοκιμής |
| `activity.tickets.empty.need_gems` | 19 | 31 | Not enough Gems yet | Δεν έχεις ακόμα αρκετά Πετράδια |
| `challenge.grooming.05.goal` | 21 | 43 | Groom a wiggly animal | Περιποιήσου ένα ζωάκι που δεν κάθεται ήσυχο |
| `challenge.hide_seek.02.name` | 8 | 14 | No Hints | Χωρίς βοήθειες |
| `challenge.hide_seek.05.name` | 10 | 17 | Big Family | Μεγάλη οικογένεια |
| `codex.title` | 5 | 9 | Codex | Κατάλογος |
| `daily.calendar.big_today` | 13 | 21 | Big box today | Μεγάλο κουτάκι σήμερα |
| `decor.birdhouse.name` | 9 | 18 | Birdhouse | Σπιτάκι για πουλιά |
| `decor.stump_shelf.name` | 11 | 18 | Stump Shelf | Ράφι από κούτσουρο |
| `egg.grassland.basic.name` | 10 | 17 | Meadow Egg | Αυγό του Λιβαδιού |
| `egg.odds.title` | 8 | 17 | Egg odds | Πιθανότητες αυγού |
| `feature.shop` | 4 | 9 | Shop | Κατάστημα |
| `journal.007.one` | 14 | 23 | Earn a sticker | Κέρδισε ένα αυτοκόλλητο |
| `journal.017.one` | 11 | 19 | Catch a nut | Πιάσε ένα φουντούκι |
| `journal.020` | 29 | 47 | Play {n} Firefly Rhythm songs | Παίξε {n} τραγούδια στον Ρυθμό των πυγολαμπίδων |
| `journal.020.one` | 26 | 46 | Play a Firefly Rhythm song | Παίξε ένα τραγούδι στον Ρυθμό των πυγολαμπίδων |
| `journal.020.other` | 29 | 47 | Play {n} Firefly Rhythm songs | Παίξε {n} τραγούδια στον Ρυθμό των πυγολαμπίδων |
| `journal.042.one` | 12 | 22 | Take a photo | Τράβηξε μια φωτογραφία |
| `journal.044` | 23 | 39 | Enter the Grand Contest | Δήλωσε συμμετοχή στον Μεγάλο διαγωνισμό |
| `journal.carry` | 28 | 47 | Saved for you from last week | Το φυλάξαμε για σένα από την περασμένη εβδομάδα |
| `journal.last_day` | 8 | 14 | Last day | Τελευταία μέρα |
| `sheet.details.button` | 7 | 12 | Details | Λεπτομέρειες |
| `shop.badge.best_value` | 10 | 17 | Best value | Καλύτερη προσφορά |
| `shop.restore.none` | 18 | 30 | Nothing to restore | Δεν υπάρχει κάτι για επαναφορά |
| `shop.section.gems` | 4 | 8 | Gems | Πετράδια |
| `shop.title` | 4 | 9 | Shop | Κατάστημα |
| `species.hedgehog.name` | 8 | 14 | Hedgehog | Σκαντζοχοιράκι |
| `treat.star_candy.name` | 10 | 17 | Star Candy | Καραμέλα αστεράκι |
| `upgrades.best` | 9 | 16 | Best pick | Καλύτερη επιλογή |
| `upgrades.biome.next` | 10 | 17 | Next biome | Επόμενος βιότοπος |
| `upgrades.locked.mastery` | 7 | 16 | {mastery} {level} | {mastery}, επίπεδο {level} |
| `variant.squirrel.autumn.name` | 6 | 11 | Autumn | Φθινοπωρινό |
| `variant.squirrel.red.name` | 3 | 7 | Red | Κόκκινο |
| `wish.010` | 14 | 23 | Earn a sticker | Κέρδισε ένα αυτοκόλλητο |
| `wish.019` | 9 | 19 | Groom {animal} | Περιποίηση για: {animal} |
| `wish.023` | 12 | 20 | Dress up {animal} | Νέο ντύσιμο για: {animal} |
| `wish.054` | 13 | 23 | Play Nut Drop | Παίξε Βροχή φουντουκιών |
| `wish.068.one` | 14 | 23 | Make a big hug | Κάνε μια μεγάλη αγκαλιά |
| `wish.074` | 26 | 46 | Play a Firefly Rhythm song | Παίξε ένα τραγούδι στον Ρυθμό των πυγολαμπίδων |
| `zone.locked.mastery` | 13 | 23 | {mastery} {level} first | Πρώτα: {mastery}, επίπεδο {level} |

Between 130 % and 160 %: `activity.glide.name` (140%), `activity.grooming.tip.bow` (160%), `activity.grooming.tip.brush` (154%), `activity.grooming.tip.burr` (152%), `activity.hub.challenge_pick` (142%), `activity.hub.free` (150%), `activity.hub.title` (140%), `activity.lily_hop.name` (150%), `activity.pause.quit` (150%), `activity.pause.resume` (133%), `activity.practice.line` (150%), `activity.result.practice` (138%), `activity.tickets.next_h` (140%), `activity.tickets.next_hm` (132%), `activity.tickets.next_m` (132%), `activity.unlock.night_visitor` (144%), `activity.unlock.refuge_level` (138%), `biome.gate.level` (138%), `biome.gate.play` (133%), `challenge.grooming.01.name` (145%), `challenge.grooming.02.goal` (135%), `challenge.grooming.03.name` (136%), `challenge.grooming.04.goal` (135%), `challenge.grooming.07.goal` (156%), `challenge.grooming.07.name` (144%), `challenge.grooming.08.goal` (132%), `challenge.hide_seek.02.goal` (136%), `challenge.hide_seek.10.goal` (133%), `daily.bonus.body` (143%), `daily.calendar.stamp` (160%), `daily.gift.big` (138%), `daily.gift.info.title` (150%), `daily.gift.open` (150%), `daily.gift.title` (150%), `decor.branch_swing.name` (142%), `decor.moss_armchair.name` (138%), `egg.price.free` (150%), `ingredient.flower_sugar.name` (142%), `journal.001.one` (142%), `journal.003.one` (145%), `journal.007` (135%), `journal.007.other` (135%), `journal.008.one` (138%), `journal.009` (139%), `journal.009.one` (152%), `journal.009.other` (139%), `journal.010` (139%), `journal.010.one` (155%), `journal.010.other` (139%), `journal.013.one` (144%), `journal.014.one` (144%), `journal.015.one` (131%), `journal.017` (143%), `journal.017.other` (143%), `journal.021` (132%), `journal.021.one` (152%), `journal.021.other` (132%), `journal.022.one` (153%), `journal.028.one` (136%), `journal.031.one` (147%), `journal.032.one` (142%), `journal.033.one` (140%), `journal.034.one` (131%), `journal.036` (150%), `journal.036.one` (157%), `journal.036.other` (150%), `journal.037.one` (150%), `journal.038` (132%), `journal.038.one` (150%), `journal.038.other` (132%), `journal.040.one` (136%), `journal.042` (153%), `journal.042.other` (153%), `journal.043` (141%), `journal.043.one` (154%), `journal.043.other` (141%), `journal.046` (135%), `journal.046.one` (144%), `journal.046.other` (135%), `journal.047.one` (133%), `journal.049.one` (130%), `journal.claim` (160%), `journal.complete` (150%), `journal.each_goal` (156%), `journal.first` (153%), `journal.row` (136%), `offline.welcome_back` (133%), `quality.thermal_notice` (131%), `rarity.fabulous` (138%), `rarity.rare` (150%), `refuge.level.rewards` (143%), `refuge.level.title` (150%), `refuge.source.biome_mastery` (131%), `refuge.source.biomes` (133%), `refuge.source.traits` (155%), `return.line.calendar` (148%), `return.line.gift` (135%), `return.title` (133%), `reward.flower_sugar` (138%), `reward.flower_sugar.one` (138%), `reward.flower_sugar.other` (138%), `reward.gems` (144%), `reward.gems.one` (157%), `reward.gems.other` (150%), `reward.sticker` (136%), `reward.sticker.one` (136%), `reward.treats.one` (133%), `sheet.treat` (160%), `shop.badge.owned` (160%), `shop.contents.gems` (150%), `shop.not_enough_gems` (150%), `shop.purchase.failed` (148%), `shop.purchase.thanks` (135%), `shop.purchase.thanks_item` (144%), `shop.section.featured` (150%), `shop.starter_pack.title` (133%), `shop.unavailable` (153%), `species.hedgehog.name_the` (142%), `species.rabbit.name` (150%), `toast.gems_first` (138%), `toast.journal_new` (132%), `toast.wish_done` (157%), `today.claim` (160%), `today.claim_all` (144%), `today.close` (160%), `treat.cherry_bite.name` (145%), `treat.mango_bite.name` (160%), `upgrades.curiosity.name` (136%), `upgrades.max` (140%), `upgrades.nests.name` (150%), `upgrades.title` (150%), `upgrades.tutorial` (133%), `upgrades.zone.next` (133%), `wish.001.one` (142%), `wish.002.one` (153%), `wish.005` (137%), `wish.008` (138%), `wish.012` (131%), `wish.014.one` (131%), `wish.016` (131%), `wish.017.one` (142%), `wish.018.one` (147%), `wish.020` (135%), `wish.020.one` (148%), `wish.020.other` (135%), `wish.025` (150%), `wish.025.one` (157%), `wish.025.other` (150%), `wish.026` (133%), `wish.026.curiosity` (142%), `wish.026.nests` (152%), `wish.026.production` (144%), `wish.027` (150%), `wish.029` (159%), `wish.030` (150%), `wish.031.one` (142%), `wish.033.one` (136%), `wish.045` (147%), `wish.046` (136%), `wish.048` (141%), `wish.048.one` (154%), `wish.048.other` (141%), `wish.055` (138%), `wish.055.one` (152%), `wish.055.other` (138%), `wish.062` (147%), `wish.063` (141%), `wish.066.one` (131%), `wish.067.one` (135%), `wish.068` (147%), `wish.068.other` (147%), `wish.076.one` (133%), `wish.077.one` (150%), `wish.079` (152%), `wish.081.one` (130%), `wish.claim` (160%), `wish.done` (157%), `wish.swap` (150%), `wish.swap_used` (144%), `wish.t01.one` (142%), `wish.title` (150%), `zone.grassland.pondedge.name` (138%), `zone.open.button` (150%), `zone.open.gain.decor` (139%), `zone.open.title` (150%).
