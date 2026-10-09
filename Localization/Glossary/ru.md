# Pocket Wilds glossary: Russian (ru)

Draft of 9 October 2026, agent TR3. Fixed terms for every Russian text of the game, so later texts stay consistent.
Source: `Localization/Drafts/ru.json`. Forbidden words: `ru_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR3.md`.

## Tone

- Informal « ты » everywhere (the French uses « tu »): warm, gentle, playful, never pushy. No « срочно », « успей »
  with a countdown, « только сегодня ».
- Goals (wishes, weekly journal, challenges) are short imperatives: « Вылупи 3 яйца », « Погладь 5 зверьков ».
- Buttons are infinitives or short nouns, as Russian apps do: Собрать, Забрать, Играть, Продолжить, Закрыть, Отмена.
- Diminutives give the cozy tone, used with care: зверёк, Ёжик, вкусняшка, гнёздышки, шёрстка, бантик, тортик.
- « ё » is always written (Ёжик, ещё, всё, звёзды): the font needs Ё and ё.
- Russian quotes « » around names inside a sentence (activity names, upgrade names, biome and species names after a
  generic noun): « Поиграй в «Прятки» », « в биоме «Луг» ».
- Numbers: decimal comma (× 1,25); « 50% » without a space; units мин, ч, с, дн., ур.
- No em dash: use a colon or a comma.
- Brand: Pocket Wilds stays in Latin letters.

## Names in sentences (cases)

Russian declines nouns, and a placeholder gives the name in the nominative only. The drafts keep every placeholder in
a slot where the nominative is right:

- after a generic noun and inside quotes: « в биоме «{biome_the}» », « мастерство вида «{species}» »;
- after a colon: « Открыть: {zone} », « Сияние: {biome} », « Собери урожай: {crop} ×{n} »;
- as the subject: « {animal} хочет вкусняшку », « {species_the} ждёт тебя в Кодексе », « {visitor} хочет
  поздороваться ».

T39B can add case forms (genitive, accusative, prepositional) per name and turn some of these into fully natural
sentences. {species_the} and {biome_the} are the bare name in Russian (no article).

## Plurals

CLDR one / few / many / other. one: 1, 21, 31, 101; few: 2 to 4, 22 to 24; many: 0, 5 to 20, 25 to 30; other: fractions
(it takes the few form, the genitive singular: « 1,5 яйца »). The « one » form always shows {n} (it also counts 21).
The key without a suffix (read without GetPlural) carries the many form, or the one form for things that come one at a
time (a Discovery egg, a decoration, a sticker).

| Noun | one | few | many |
|---|---|---|---|
| egg | {n} яйцо | {n} яйца | {n} яиц |
| animal (little) | {n} зверёк | {n} зверька | {n} зверьков |
| Gem | {n} Самоцвет | {n} Самоцвета | {n} Самоцветов |
| ticket | {n} билет | {n} билета | {n} билетов |
| treat | {n} вкусняшка | {n} вкусняшки | {n} вкусняшек |
| seed | {n} семечко | {n} семечка | {n} семечек |
| decoration | {n} украшение | {n} украшения | {n} украшений |
| sticker | {n} наклейка | {n} наклейки | {n} наклеек |
| bond point | {n} очко дружбы | {n} очка дружбы | {n} очков дружбы |
| day | {n} день | {n} дня | {n} дней |
| wish | {n} желание | {n} желания | {n} желаний |
| star | {n} звезда | {n} звезды | {n} звёзд |
| time (once, twice) | {n} раз | {n} раза | {n} раз |

## Species and animals

| English | Russian | Notes |
|---|---|---|
| Rabbit | Кролик | masculine: variants Коричневый, Белый, Серый, Чёрный, Пятнистый, Сакура |
| Squirrel | Белка | feminine: variants Рыжая, Коричневая, Серая, Кремовая, Чёрная, Осенняя |
| Hedgehog | Ёжик | masculine, the cute form: variants Коричневый, Кремовый, Серый, Чёрный, Белый, Грибной |
| Axolotl | аксолотль | |
| species (the word) | вид | « Новый вид! », « Мастерство видов » |
| animal | зверёк (cute), зверь in « большой зверь » | |
| newborn, baby | малыш | « Поприветствуй малыша » |
| visitor | гость | « Ночной гость » |
| variant (coat color) | окрас | « Новый окрас! », codex tab « Окрасы » |
| rare traits | редкие черты | |
| bond | дружба | « очки дружбы » |

## Currencies and items

| English | Russian | Notes |
|---|---|---|
| Gems | Самоцветы | capitalized as a game currency, like the English |
| Light | Свет | capitalized; mass noun: « {n} Света », « +10% Света » |
| ticket | билет | |
| treat | вкусняшка | the sheet button « Treat » is the verb « Угостить » |
| Mushroom Biscuit | Грибное печенье | |
| sticker | наклейка | |
| Flower Sugar | Цветочный сахар | counted as « Цветочный сахар ×{n} » |
| seed | семечко | |
| decoration | украшение | |
| Discovery Egg | Яйцо открытий | |
| Meadow Egg, Precious Meadow Egg | Луговое яйцо, Драгоценное луговое яйцо | |
| Starter Pack | Набор новичка | |
| Dusk (decoration line) | «Сумерки» | after the name: « Моховое кресло «Сумерки» » |

## Places

| English | Russian | Notes |
|---|---|---|
| refuge | приют | « Уровень приюта », « Добро пожаловать в приют » |
| Meadow (Prairie biome) | Луг | « Мастерство луга » |
| Forest | Лес | |
| Lake | Озеро | |
| Mountain | Горы | |
| Cloudlands (future biome) | Облачные острова | the cloud islands: a plain sky word, see `ru_forbidden.txt` |
| Starlight (future biome) | Звёздное небо | |
| biome | биом | |
| zone | зона | |
| zones of the Meadow | Начало, Цветочный луг, Излучина ручья, Фруктовый сад, Холм, Мельничная горка, Берег пруда, Опушка | |
| cairn (future landmark) | каменная пирамидка | |

## Rarities

Обычный, Необычный, Редкий, Эпический, Легендарный, Сказочный (Fabulous). Masculine labels.

## Activities

| English | Russian |
|---|---|
| Activities (hub) | Игры |
| Grooming | Груминг (verb: причесать) |
| Hide and Seek | Прятки |
| Nut Drop | Ореховый дождь |
| Pawprint Memory | Мемори с лапками (short: «Мемори») |
| Expeditions | Экспедиции |
| Garden | Сад (the Orchard zone is « Фруктовый сад ») |
| Treasure Fishing | Ловля сокровищ |
| Lily Pad Hop | Прыжки по кувшинкам |
| Baking | Выпечка (a cake: тортик) |
| Photo Safari | Фотосафари |
| Cuddle Match | Обнимашки |
| Glide | Полёт |
| Constellations | Созвездия |
| Firefly Rhythm | Ритм светлячков |
| Bubbles | Пузырьки |
| Tile Puzzle | Пятнашки |
| Burrow Maze | Лабиринт норок |
| Grand Contest | Большой конкурс |
| challenge | испытание |
| practice | тренировка |
| streak | серия |
| record, best | рекорд |

## Features

| English | Russian |
|---|---|
| rendez-vous (daily visit) | встреча дня |
| Today | Сегодня |
| wish, wishes | желание, Желания; « Желание исполнено » |
| daily gift | подарок дня |
| welcome gift | приветственный подарок |
| calendar, stamp | календарь, отметить (a stamp: отметка; a box: клетка) |
| weekly journal | Дневник недели |
| album | альбом |
| Codex | Кодекс |
| mastery | мастерство |
| refuge level | уровень приюта |
| upgrade | улучшение |
| glow | сияние |

## UI verbs

| English | Russian |
|---|---|
| Collect (Light) | Собрать |
| Claim, Collect (a reward) | Забрать; Claim all: Забрать всё |
| Play | Играть |
| Play again | Ещё раз |
| Continue, Resume | Продолжить |
| Close | Закрыть |
| Cancel | Отмена |
| OK | ОК |
| Open | Открыть |
| Swap | Сменить |
| See | Смотреть |
| Back | Назад |
| Quit | Выйти |
| Details | Подробнее |
