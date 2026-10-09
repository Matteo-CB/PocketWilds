# Pocket Wilds glossary: Japanese (ja)

Draft of 9 October 2026, agent TR1. Fixed terms for every Japanese text of the game, so later texts stay consistent.
Source: `Localization/Drafts/ja.json`. Forbidden words: `ja_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR1.md`.

## Tone

- Warm, gentle and playful, never pushy. The player is addressed without あなた: sentences speak to them directly.
- System messages (shop, purchase, restore, errors) are polite です／ます: 「ジェムが足りません。」
- Tips, goals and toasts are friendly plain style with soft endings: 「〜してね」「〜しよう」「〜だよ」「〜ね」.
- Goals (wishes, weekly journal, challenges) use the volitional 「〜しよう」: 「タマゴを{n}個かえそう」.
- Kanji: common jōyō kanji only; hiragana for soft words (どうぶつ, かざり, おやつ, たね, ひかり, ごほうび).
  Every kanji in the drafts is jōyō (see `Drafts/charset_ja.txt`).
- Punctuation: full width 「」、。！？：（）; 「」 around names inside a sentence. No space between words; a space only
  before a symbol or a number in short UI labels (「カラー +{pct}%」「連続 ×{n}」).
- Numbers: Arabic digits, counter right after the number, no space: 「{n}個」「{n}匹」「{n}枚」.
- Brand: Pocket Wilds stays in Latin letters.

## Counters

| Thing | Counter | Example |
|---|---|---|
| eggs, treats, seeds, stars, decorations, wishes, bubbles, plants | 個 | タマゴを{n}個かえそう |
| animals (and visitors, newborns) | 匹 | どうぶつ{n}匹 |
| stickers, tickets, photos, puzzle tiles | 枚 | シール{n}枚 |
| times | 回 | {n}回 |
| days | 日 / 日目 | あと{n}日、{n}日目 |
| songs | 曲 | {n}曲 |
| pairs (memory) | 組 | {n}組 |
| places | か所 | {n}か所 |
| moves (memory) | 手 | {n}手以内 |
| Gems, Light | none (the name follows the number) | {n}ジェム、{n}ひかり |

## Species

| English | Japanese | Notes |
|---|---|---|
| Rabbit | ウサギ | katakana, as in pet and nature books |
| Squirrel | リス | |
| Hedgehog | ハリネズミ | |
| Axolotl | ウーパールーパー | the popular Japanese name |
| species (the word) | 種類 | 「新しい種類！」; 「どうぶつの熟練度」 for species mastery |
| animal | どうぶつ | hiragana |
| newborn | 赤ちゃん | |
| visitor | お客さん | |
| variant (color of a species) | カラー | 「新しいカラー！」; not 色ちがい (a shiny in a famous series) |
| Brown, White, Grey, Black, Cream | 茶色、白、灰色、黒、クリーム | variant names |
| Sakura, Spotted, Mushroom, Autumn, Red | さくら、ぶち、きのこ、秋色、赤茶 | variant names |
| rare traits | めずらしい特徴 | |

## Currencies and items

| English | Japanese | Notes |
|---|---|---|
| Gems | ジェム | 「{n}ジェム」「+{n}ジェム」 |
| Light | ひかり | hiragana: reads as a currency name; 「{n}ひかり」 |
| {biome} Glow (upgrade) | {biome}のあかり | あかり, a soft light, keeps it apart from the ひかり currency |
| tickets | チケット | 「チケット{n}枚」 |
| treats | おやつ | |
| Mushroom Biscuit | きのこビスケット | |
| Golden Treat | 金色のおやつ | |
| stickers | シール | 「シール{n}枚」 |
| Flower Sugar | 花の砂糖 | |
| seeds | たね | |
| dyes | 染料 | |
| decoration | かざり | |
| Discovery Egg | はっけんのタマゴ | |
| Meadow Egg / Precious Meadow Egg | 草原のタマゴ / 草原のとっておきタマゴ | precious = とっておき |
| egg | タマゴ | katakana, as in monster-raising games |
| Flower Crown | 花かんむり | |
| bow (grooming) | リボン | |
| burrs | ひっつき虫 | the children's word |
| postcard | 絵はがき | |
| Dusk (decoration tint) | （夕暮れ） | suffix in brackets: 「鳥の巣箱（夕暮れ）」 |

## Places

| English | Japanese | Notes |
|---|---|---|
| refuge | 里 | 「どうぶつの里へようこそ」「里のレベル」「里レベル{n}」: a warm home village of the animals |
| biome | エリア | 「次のエリア」 |
| zone | ゾーン | 「次のゾーン」 |
| Meadow (biome, the Prairie) | 草原 | |
| Forest, Lake, Mountain | 森、湖、山 | |
| the Start | はじまりの地 | zone |
| the Flower Meadow | お花畑 | zone |
| the Stream Bend | 小川の曲がり角 | zone |
| the Orchard | 果樹園 | zone |
| the Hill | 丘 | zone |
| Windmill Rise | 風車の高台 | zone |
| the Pond Edge | 池のほとり | zone |
| the Forest Edge | 森のはずれ | zone |
| to open a zone or biome | ひらく | 「{zone}をひらく」; a gift is 開ける, an album or a postcard 開く |

## Rarities

Katakana, the convention of Japanese collection games.

| English | Japanese |
|---|---|
| Common | コモン |
| Uncommon | アンコモン |
| Rare | レア |
| Epic | エピック |
| Legendary | レジェンド |
| Fabulous | ファビュラス |

Not used for Fabulous: 幻 (the Japanese word for the rarity the vocabulary rule replaced) and 伝説 (kept free for
Legendary if a kanji set is ever wanted).

## Activities

| English | Japanese |
|---|---|
| activities (hub) | あそび |
| Grooming | 毛づくろい (button on the animal sheet: ブラッシング) |
| Hide and Seek | かくれんぼ |
| Nut Drop | 木の実キャッチ |
| Pawprint Memory | あしあと絵合わせ (short form: 絵合わせ) |
| Expeditions | おでかけ |
| Garden | お庭 |
| Treasure Fishing | おたから釣り |
| Lily Pad Hop | スイレンぴょんぴょん (スイレン, water lily, never the other water flower) |
| Baking | お菓子づくり |
| Photo Safari | フォトサファリ |
| Cuddle Match | ぎゅっとマッチ (big hug: 大きなぎゅっ) |
| Glide | ふんわり滑空 |
| Constellations | 星座 (the {species} constellation: 「{species}座」) |
| Firefly Rhythm | ホタルのリズム |
| Bubbles | シャボン玉 |
| Tile Puzzle | スライドパズル |
| Burrow Maze | 巣穴めいろ |
| Grand Contest | グランドコンテスト |
| challenge | チャレンジ |
| practice | 練習 |
| streak | 連続; in the rhythm game コンボ |
| star (score) | 星 |

## Features

| English | Japanese |
|---|---|
| daily rendez-vous (the whole feature) | まいにちのおたのしみ |
| Today (screen and HUD) | 今日 |
| daily gift | 今日のプレゼント (in counts: 毎日のプレゼント) |
| wishes | おねがい (「おねがいをかなえよう」「おねがいがかなったよ」) |
| calendar | カレンダー; stamp: スタンプ (「スタンプを押そう」) |
| weekly journal | 今週の手帳 |
| album | アルバム |
| Codex | 図鑑 |
| bond | なかよし度; bond points: なかよしポイント |
| mastery | 熟練度 (「草原の熟練度」「{species}の熟練度」) |
| upgrades | 強化 |
| level | レベル; short: Lv. |
| shop | ショップ; Starter Pack: スターターパック |
| hatch | かえす (「タマゴをかえそう」) |
| breeding | 繁殖 |

## UI verbs and buttons

| English | Japanese |
|---|---|
| Collect, Claim | 受け取る |
| Claim all | すべて受け取る |
| Play | あそぶ |
| Continue, Resume | つづける |
| Close | 閉じる |
| Back | もどる |
| Quit | やめる |
| Open (gift) | 開ける |
| Open (zone) | ひらく |
| See | 見る |
| Swap | 交換 |
| Stamp | スタンプ |
| Cancel / OK | キャンセル / OK |
| Free | 無料 |
| New (badge) | NEW |
| Max / Maxed | MAX / Lv.MAX |
| Details | くわしく |
| Restore purchases | 購入を復元 |
