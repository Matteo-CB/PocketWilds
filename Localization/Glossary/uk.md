# Pocket Wilds glossary: Ukrainian (uk)

Draft of 9 October 2026, agent TR3. Fixed terms for every Ukrainian text of the game, so later texts stay consistent.
Source: `Localization/Drafts/uk.json`. Forbidden words: `uk_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR3.md`.

The Ukrainian file is written in Ukrainian from the English and French, never adapted from the Russian: its own
vocabulary (крамниця, бракує, смаколик, наліпка, звірятко, садок, конюшина, гойдалка, шпаківня, лійка), its own
grammar (за {n} дн., у/в and з/із/зі alternation) and its own plurals.

## Tone

- Informal « ти » everywhere (the French uses « tu »): warm, gentle, playful, never pushy.
- Goals (wishes, weekly journal, challenges) are short imperatives: « Вилупи 3 яйця », « Погладь 5 звіряток ».
- Buttons are infinitives or short words: Зібрати, Забрати, Грати, Далі, Продовжити, Закрити, Скасувати, ОК.
- Diminutives give the cozy tone, with care: звірятко, Їжачок, смаколик, гніздечка, шерстка, бантик, тортик, малятко.
- Euphony: у / в and з / із / зі follow the sounds around them (« зірок у «Грумінгу» », « звірятко з кущів »,
  « звіряток із кущів »).
- Ukrainian quotes « » around names inside a sentence: « Пограй у «Хованки» », « у біомі «Луг» ».
- The apostrophe is the straight ' (U+0027), as in the French texts of the game: сузір'я, п'ятнашки, з'явиться,
  реп'яхи. A typographic apostrophe (U+02BC) can replace it if the font team prefers.
- Numbers: decimal comma (× 1,25); « 50% » without a space; units хв, год, с, дн., рів.
- No em dash: use a colon or a comma.
- Brand: Pocket Wilds stays in Latin letters.

## Names in sentences (cases)

Ukrainian declines nouns (seven cases, with the vocative), and a placeholder gives the name in the nominative only. The
drafts keep every placeholder in a slot where the nominative is right:

- after a generic noun and inside quotes: « у біомі «{biome_the}» », « майстерність виду «{species}» »;
- after a colon: « Відкрити: {zone} », « Сяйво: {biome} », « Збери врожай: {crop} ×{n} »;
- as the subject: « {animal} хоче смаколик », « {species_the} чекає на тебе в Кодексі ».

{species_the} and {biome_the} are the bare name in Ukrainian (no article). T39B can add case forms per name.

## Plurals

CLDR one / few / many / other, as in PluralRules.cs: one: 1, 21, 31; few: 2 to 4, 22 to 24; many: 0, 5 to 20, 25 to 30;
other: fractions (it takes the few form). The « one » form always shows {n}. The key without a suffix carries the many
form, or the one form for things that come one at a time.

| Noun | one | few | many |
|---|---|---|---|
| egg | {n} яйце | {n} яйця | {n} яєць |
| animal (little) | {n} звірятко | {n} звірятка | {n} звіряток |
| Gem | {n} Самоцвіт | {n} Самоцвіти | {n} Самоцвітів |
| ticket | {n} квиток | {n} квитки | {n} квитків |
| treat | {n} смаколик | {n} смаколики | {n} смаколиків |
| seed | {n} зернятко | {n} зернятка | {n} зерняток |
| decoration | {n} прикраса | {n} прикраси | {n} прикрас |
| sticker | {n} наліпка | {n} наліпки | {n} наліпок |
| bond point | {n} бал дружби | {n} бали дружби | {n} балів дружби |
| day | {n} день | {n} дні | {n} днів |
| wish | {n} бажання | {n} бажання | {n} бажань |
| star | {n} зірка | {n} зірки | {n} зірок |
| time (once, twice) | {n} раз | {n} рази | {n} разів |

## Species and animals

| English | Ukrainian | Notes |
|---|---|---|
| Rabbit | Кролик | masculine: Коричневий, Білий, Сірий, Чорний, Плямистий, Сакура |
| Squirrel | Білка | feminine: Руда, Коричнева, Сіра, Кремова, Чорна, Осіння |
| Hedgehog | Їжачок | masculine: Коричневий, Кремовий, Сірий, Чорний, Білий, Грибний |
| Axolotl | аксолотль | |
| species (the word) | вид | « Новий вид! » |
| animal | звірятко | |
| newborn, baby | малятко | |
| visitor | гість | « Нічний гість » |
| variant (coat color) | забарвлення | « Нове забарвлення! » |
| rare traits | рідкісні риси | |
| bond | дружба | « бали дружби » |

## Currencies and items

| English | Ukrainian | Notes |
|---|---|---|
| Gems | Самоцвіти | capitalized as a game currency |
| Light | Світло | capitalized; « {n} Світла », « +10% Світла » |
| ticket | квиток | |
| treat | смаколик | the sheet button « Treat » is the verb « Пригостити » |
| Mushroom Biscuit | Грибне печиво | |
| sticker | наліпка | |
| Flower Sugar | Квітковий цукор | counted as « Квітковий цукор ×{n} » |
| seed | зернятко | |
| decoration | прикраса | |
| Discovery Egg | Яйце відкриттів | |
| Meadow Egg, Precious Meadow Egg | Лугове яйце, Дорогоцінне лугове яйце | |
| Starter Pack | Набір новачка | |
| clover | конюшина | not клевер |
| Dusk (decoration line) | «Сутінки» | after the name |

## Places

| English | Ukrainian | Notes |
|---|---|---|
| refuge | притулок | « Рівень притулку » |
| Meadow | Луг | « Майстерність лугу » |
| Forest | Ліс | |
| Lake | Озеро | |
| Mountain | Гори | |
| Cloudlands (future biome) | Хмарні острови | a plain sky word, see `uk_forbidden.txt` |
| Starlight (future biome) | Зоряне небо | |
| biome, zone | біом, зона | |
| zones of the Meadow | Початок, Квітковий луг, Вигин струмка, Фруктовий сад, Пагорб, Вітрякова гірка, Берег ставка, Узлісся | |
| cairn (future landmark) | кам'яна пірамідка | |

## Rarities

Звичайний, Незвичайний, Рідкісний, Епічний, Легендарний, Казковий (Fabulous).

## Activities

| English | Ukrainian |
|---|---|
| Activities (hub) | Ігри |
| Grooming | Грумінг (verb: причесати) |
| Hide and Seek | Хованки |
| Nut Drop | Горіховий дощ |
| Pawprint Memory | Мемо з лапками (short: «Мемо») |
| Expeditions | Експедиції |
| Garden | Садок (the Orchard zone is « Фруктовий сад ») |
| Treasure Fishing | Ловля скарбів |
| Lily Pad Hop | Стрибки по лататті |
| Baking | Випічка (a cake: тортик) |
| Photo Safari | Фотосафарі |
| Cuddle Match | Обіймашки |
| Glide | Політ |
| Constellations | Сузір'я |
| Firefly Rhythm | Ритм світлячків |
| Bubbles | Бульбашки |
| Tile Puzzle | П'ятнашки |
| Burrow Maze | Лабіринт нірок |
| Grand Contest | Великий конкурс |
| challenge | випробування |
| practice | тренування |
| streak | серія |
| record, best | рекорд |

## Features

| English | Ukrainian |
|---|---|
| rendez-vous (daily visit) | зустріч дня |
| Today | Сьогодні |
| wish, wishes | бажання; « Бажання здійснилося » |
| daily gift | подарунок дня |
| welcome gift | вітальний подарунок |
| calendar, stamp | календар, позначити (a stamp: позначка; a box: клітинка) |
| weekly journal | Щоденник тижня |
| album | альбом |
| Codex | Кодекс |
| mastery | майстерність |
| upgrade | покращення |
| glow | сяйво |
| shop | крамниця |

## UI verbs

| English | Ukrainian |
|---|---|
| Collect (Light) | Зібрати |
| Claim, Collect (a reward) | Забрати; Claim all: Забрати все |
| Play | Грати |
| Play again | Ще раз |
| Continue | Далі (summary), Продовжити (pause) |
| Close | Закрити |
| Cancel | Скасувати |
| OK | ОК |
| Open | Відкрити |
| Swap | Змінити |
| See | Глянути |
| Back | Назад |
| Quit | Вийти |
| Details | Докладніше |
