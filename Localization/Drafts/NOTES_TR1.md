# NOTES_TR1: draft translations ja, ko, zh-Hans, zh-Hant (and the en / fr sources)

Agent TR1, 9 October 2026. First pass for the T39B « Langues : socle » import and for the font team. Not reviewed by a
native speaker yet: see « Questions for native reviewers » below.

## Files

| File | Content |
|---|---|
| `Drafts/ja.json`, `ko.json`, `zh-Hans.json`, `zh-Hant.json` | 617 keys each: the 719 keys of LOC_en, minus the 102 `.one` forms these languages do not use |
| `Drafts/en.json`, `fr.json` | LOC_en and LOC_fr extracted unchanged (719 keys each), same file shape |
| `Drafts/charset_ja.txt`, `charset_ko.txt`, `charset_zh-Hans.txt`, `charset_zh-Hant.txt` | the distinct characters of each draft, one line, sorted by code point (placeholders and spaces left out) |
| `Glossary/ja.md`, `ko.md`, `zh-Hans.md`, `zh-Hant.md` | fixed terms, counters, tone |
| `Glossary/ja_forbidden.txt`, `ko_forbidden.txt`, `zh-Hans_forbidden.txt`, `zh-Hant_forbidden.txt` | the vocabulary rule in each language, plus the sparkle words |
| `Drafts/check_drafts.py` | the shared check (created by TR1, see the end of this file) |

`python Localization/Drafts/check_drafts.py ja ko zh-Hans zh-Hant` gives 0 errors and 0 warnings.

## Plurals

- CLDR (and `PluralRules.cs`): Japanese, Korean and Chinese have the « other » form only. Each of the 102 counted keys
  has its base key and `.other`, no `.one`.
- The « other » text must also read right for 1, so the drafts use wordings that work for any number: wish.049
  « Photograph {n} animals together » became « a photo with {n} animals in it »; journal.013 drops « different »;
  counters always follow the number (「タマゴを{n}個」「알 {n}개」「{n}颗蛋」).
- The base key carries the same text as `.other`, except `reward.gems`, which keeps the « + » of the level reward card
  like the English.
- `fr.json` is LOC_fr unchanged: it has no `.many` form, which CLDR French needs for exact millions. The check reports
  this as 102 warnings on the source only; T39B adds the forms (or the counts never reach a million).

## Article placeholders

ja, ko and zh have no articles: the drafts keep the English placeholders and translate around them.

| Key | Placeholder | Note |
|---|---|---|
| `wish.006` | {species_the} | 「図鑑で{species_the}を見てみよう」 |
| `wish.026`, `wish.026.curiosity`, `wish.026.nests` | {biome_the} | |
| `wish.028`, `wish.028.other` | {biome_the} | |
| `species.*.name_the`, `biome.*.name_the` | (the forms themselves) | equal to the bare name in ja, ko, zh |
| `zone.*.name` | (zone names carry « the » in English) | no article in ja, ko, zh |
| every {animal} key | the animal's name, else `species.*.name_the` | bare name in ja, ko, zh |

For T39B: Chinese and Japanese need no noun forms. Korean needs no case forms either, but the particles 은/는, 이/가,
을/를, 와/과, (으)로 change with the last syllable of the noun: the Korean draft never puts one of them right after a
placeholder (it uses 에, 에서, 에게, 의, or no particle, as Korean UI does). A future Korean sentence that needs one
would want a small 받침 helper in the formatter. Note also that LOC_fr writes {species_the} / {biome_the} in wish.005,
wish.011, wish.026.production and wish.073 where the English has the bare name; the drafts follow the English.

## Cultural adaptations

Japanese
- Refuge: 里 (「どうぶつの里へようこそ」), the warm home village of the animals. 保護区 reads as a nature reserve and
  シェルター as a rescue shelter; the usual Japanese word for an ideal haven is on the forbidden list.
- Light: ひかり in hiragana, so it reads as the currency's name; the « {biome} Glow » upgrade is 「{biome}のあかり」 so the
  two never mix.
- Pawprint Memory: 「あしあと絵合わせ」. The usual Japanese name of the memory card game contains a kanji on the
  forbidden list.
- Lily Pad Hop: スイレン (water lily), not the other water flower the rule replaces.
- Variants are カラー: 色ちがい means a « shiny » in a famous monster series.
- Rarities in katakana as in Japanese collection games; Fabulous is ファビュラス, a known loanword. 幻 is avoided: it is
  the Japanese name of the tier the vocabulary rule replaced.
- Round Garden Lantern: ランタン, never 灯籠 (the stone lantern of temples).
- Burrs: ひっつき虫, the children's word. Axolotl: ウーパールーパー. Expeditions: おでかけ.
- Tone: polite です／ます in system messages, friendly 「〜してね」「〜しよう」 elsewhere; jōyō kanji only.

Korean
- Refuge: 보금자리 (a cozy nest-home). Light: 빛; the Glow upgrade: 「{biome}의 햇살」 (sunshine).
- Goals in the 「~하기」 form of Korean game missions; sentences in 해요체; buttons short (받기, 닫기, 열기).
- Star Candy is 별사탕, a real Korean candy. Axolotl: 우파루파, the popular name.
- Fabulous: 환상 (from 환상적인). The usual top-tier word of Korean games is the one the rule replaced.
- No 반짝 anywhere: it is the everyday Korean word for « twinkle ».

Chinese (both)
- Refuge: 家园 / 家園. Activities: 小游戏 (活动 reads as « events » in Chinese games). Light: 光点 / 光點.
- Expeditions: 旅行, sending animals away and getting postcards, a format Chinese players know and love.
- Cuddle Match: 抱抱消消乐, 消消乐 being the usual Chinese name of match games.
- Photo Safari: 摄影漫游 (no 狩猎, « hunting »). Rendez-vous (glossary only): 每日相聚, never 约会 (a romantic date).
- Calendar stamp: 盖章 / 蓋章, not 签到 (« login check-in »), to keep the cute stamp and avoid a daily-duty feeling.
- Grand Grooming: 梳毛高手 (expert), not 大师 (it also has a religious sense).
- Unknown crops ({crop}) take 「×{n}」, since their measure word is not known in advance.

Traditional Chinese (Taiwan)
- Taiwan words, not a conversion: 躲貓貓 (hide and seek), 影片, 公尺, 機率, 票券, 收成, 咖啡色 (brown), 牠 for an
  animal, 「」 quotes, the particles 喔 and 囉. 鞦韆 for swing (the standard form, rarer glyphs for the font team).

## Questions for native reviewers

Japanese
1. 里 for the refuge: warm and clear? Alternatives: すみか, おうち, ひだまり.
2. Mission lists: volitional 「タマゴを{n}個かえそう」 (friendly) or plain 「タマゴを{n}個かえす」 (2 characters shorter)?
3. Rarities in katakana (コモン to ファビュラス) or Japanese words (ふつう, めずらしい...)? Is ファビュラス clear?
4. 繁殖 for « breeding »: too clinical for a cozy game? Alternatives: ペアリング, 赤ちゃんづくり.
5. Variant colors 白 / 黒 / 茶色 (kanji), しろ / くろ (kana) or ホワイト / ブラック (katakana)?
6. 強化 for « Upgrades » (gamer standard) or パワーアップ (softer)?
7. 「なかよし度」 for bond and 「熟練度」 for mastery: natural for this audience?
8. 「受け取る」 is the longest common button: is 「もらう」 acceptable where a box is small?
9. 「（夕暮れ）」 suffix for the Dusk decorations, or a prefix 「夕暮れの〜」?

Korean
1. 보금자리 for the refuge.
2. Mix of 해요체 sentences and 「~하기」 goals: natural?
3. Rarity names 일반 / 고급 / 희귀 / 영웅 / 전설 / 환상: is 환상 a good top tier?
4. Nut Drop as 열매 받기: the nuts are hazelnuts (구운 헤이즐넛 is a treat); 도토리 would be cuter if the art shows acorns.
5. 도꼬마리 for burrs: known to children? Alternative: 가시 열매.
6. 보석 or 젬 for Gems?
7. 「{first} 그리고 {second}」 for two unlock conditions: acceptable?

Chinese
1. 家园 / 家園 for the refuge (alternative: 小窝).
2. 光点 / 光點 for Light; 宝石 / 寶石 or 钻石 / 鑽石 for Gems.
3. 门票 (Simplified) and 票券 (Taiwan) for activity tickets.
4. 花色 for variants; 少见 / 少見 for Uncommon (alternative: 精良); 梦幻 / 夢幻 for Fabulous.
5. Taiwan: 日曆 or 月曆 for the stamp calendar?
6. zh-Hans audience (Singapore, Malaysia, abroad): mainland vocabulary (视频, 米) kept; any local preference?
7. 盖章 / 蓋章 for the calendar stamp, or the more common game word 签到 / 簽到?

## Forbidden lists

- One file per language in `Glossary/`, one word or stem per line, each with a comment line giving its meaning; these
  files and `Vocabulary_Rule.md` are the only places the words may appear.
- Japanese and Chinese entries match anywhere (no spaces): single kanji are used where every compound is religious (神,
  佛, 聖 / 圣, 祈, 寺, 霊, 魂, 祭...), compounds elsewhere so that everyday words stay allowed (睡蓮 / 睡莲 next to the
  flower the rule replaces, 星座, 星光, 灯笼, 水仙, 拜托, 灵活).
- Korean entries are stems matched at a word start (particles follow), « ~ » for compounds and « = » for the bare word
  신 (so 신나요 and 신기록 stay allowed, and 이기도 or 주기적 never match 기도 or 기적).
- Each list ends with a « no sparkle rule » section: the words for sparkle and twinkle (きらきら, 반짝, 闪闪 / 閃閃...).
- The drafts contain none of them (the check passes).

## check_drafts.py

Shared by every translation agent. It reads the reference keys from LOC_en.asset (or `Drafts/en.json`), the plural
rules mirrored from `PluralRules.cs`, and each language's forbidden list. Per draft: keys complete and nothing unknown,
placeholders and tags identical ({species_the} / {biome_the} may stand for the bare form), exact CLDR plural forms (a
« one » form may drop {n} only where « one » means 1 alone), NFC, forbidden words, sparkle characters (written as
escapes in the script), em dashes, Thai U+200B runs, and lengths. Options: `--lengths` (every tight key), `--charset`
(writes `charset_<code>.txt`), `--quiet`. Exit code 1 on any error.

## Tight lengths

Measure: display width, a Chinese character, kana or Hangul syllable counting 2 (they are about twice as wide as an
average Latin letter at the same size). Tight: width above 130 % of the English (and at least 2 more), or a title (a
`.title` or `.name` key) wider than 18. `.other` rows identical to their base key are not repeated. Japanese is the
tightest language; the T39B fit test in the real boxes will decide which ones need the shorter forms suggested above.

### ja: 142 keys

| Key | English | Draft | Width en → draft | Why |
|---|---|---|---|---|
| `activity.baking.name` | Baking | お菓子づくり | 6 → 12 | 200% |
| `activity.bubbles.name` | Bubbles | シャボン玉 | 7 → 10 | 143% |
| `activity.glide.name` | Glide | ふんわり滑空 | 5 → 12 | 240% |
| `activity.grand_contest.name` | Grand Contest | グランドコンテスト | 13 → 18 | 138% |
| `activity.grooming.sheet_groom` | Groom | ブラッシング | 5 → 12 | 240% |
| `activity.grooming.tip.bow` | Pick a bow | リボンを選んでね | 10 → 16 | 160% |
| `activity.grooming.tip.brush` | Brush the fur | ブラシで毛をとかしてね | 13 → 22 | 169% |
| `activity.hide_seek.tip.find` | Tap the hidden animals | かくれたどうぶつをタップしてね | 22 → 30 | 136% |
| `activity.hide_seek.tip.paused` | Gently… | ゆっくりね… | 7 → 11 | 157% |
| `activity.hub.play` | Play | あそぶ | 4 → 6 | 150% |
| `activity.lily_hop.name` | Lily Pad Hop | スイレンぴょんぴょん | 12 → 20 | 167%, title 20 |
| `activity.nut_drop.name` | Nut Drop | 木の実キャッチ | 8 → 14 | 175% |
| `activity.pause.quit` | Quit | やめる | 4 → 6 | 150% |
| `activity.pause.resume` | Resume | つづける | 6 → 8 | 133% |
| `activity.pause.title` | Paused | 一時停止 | 6 → 8 | 133% |
| `activity.result.back` | Back | もどる | 4 → 6 | 150% |
| `activity.result.title0` | Nice try | おしかったね | 8 → 12 | 150% |
| `activity.result.title3` | Perfect! | パーフェクト！ | 8 → 14 | 175% |
| `activity.test.name` | Test game | テストゲーム | 9 → 12 | 133% |
| `activity.unlock.axolotl` | Meet the Axolotl | ウーパールーパーと出会う | 16 → 24 | 150% |
| `biome.gate.title` | Open {biome} | {biome}をひらく | 8 → 11 | 138% |
| `challenge.grooming.01.name` | First Brush | はじめてのブラシ | 11 → 16 | 145% |
| `challenge.grooming.03.name` | Leaf Shower | 葉っぱのシャワー | 11 → 16 | 145% |
| `challenge.grooming.07.goal` | Groom a big animal | 大きなどうぶつの毛づくろい | 18 → 26 | 144% |
| `challenge.grooming.07.name` | Big Buddy | 大きなおともだち | 9 → 16 | 178% |
| `challenge.hide_seek.08.name` | All Alike | みんなそっくり | 9 → 14 | 156% |
| `codex.unknown` | ??? | ？？？ | 3 → 6 | 200% |
| `daily.bonus.title` | Bonus gift | おまけのプレゼント | 10 → 18 | 180% |
| `daily.calendar.stamp` | Stamp | スタンプ | 5 → 8 | 160% |
| `daily.gift.big` | Big gift | 大きなプレゼント | 8 → 16 | 200% |
| `daily.gift.info.title` | Daily gift | 今日のプレゼント | 10 → 16 | 160% |
| `daily.gift.nice` | Nice gift | すてきなプレゼント | 9 → 18 | 200% |
| `daily.gift.open` | Open | 開ける | 4 → 6 | 150% |
| `daily.gift.small` | Small gift | 小さなプレゼント | 10 → 16 | 160% |
| `daily.gift.title` | Daily gift | 今日のプレゼント | 10 → 16 | 160% |
| `daily.welcome.title` | Welcome gift | ようこそプレゼント | 12 → 18 | 150% |
| `decor.branch_swing_dusk.name` | Dusk Branch Swing | 枝のブランコ（夕暮れ） | 17 → 22 | title 22 |
| `decor.cloud_mobile_dusk.name` | Dusk Cloud Mobile | 雲のモビール（夕暮れ） | 17 → 22 | title 22 |
| `decor.daisy_carousel.name` | Daisy Carousel | デイジーのメリーゴーランド | 14 → 26 | 186%, title 26 |
| `decor.daisy_carousel_dusk.name` | Dusk Daisy Carousel | デイジーのメリーゴーランド（夕暮れ） | 19 → 36 | 189%, title 36 |
| `decor.giant_watering_can_dusk.name` | Dusk Giant Flower Watering Can | 大きな花のじょうろ（夕暮れ） | 30 → 28 | title 28 |
| `decor.moss_armchair.name` | Moss Armchair | コケのアームチェア | 13 → 18 | 138% |
| `decor.moss_armchair_dusk.name` | Dusk Moss Armchair | コケのアームチェア（夕暮れ） | 18 → 28 | 156%, title 28 |
| `decor.pebble_fountain_dusk.name` | Dusk Pebble Fountain | 小石の噴水（夕暮れ） | 20 → 20 | title 20 |
| `decor.round_garden_lantern_dusk.name` | Dusk Round Garden Lantern | まるい庭のランタン（夕暮れ） | 25 → 28 | title 28 |
| `decor.round_greenhouse_dusk.name` | Dusk Round Greenhouse | まるい温室（夕暮れ） | 21 → 20 | title 20 |
| `decor.stump_shelf_dusk.name` | Dusk Stump Shelf | 切り株の棚（夕暮れ） | 16 → 20 | title 20 |
| `decor.sunflower_clock_dusk.name` | Dusk Sunflower Clock | ひまわり時計（夕暮れ） | 20 → 22 | title 22 |
| `decor.woven_linen_hammock_dusk.name` | Dusk Woven Linen Hammock | リネンのハンモック（夕暮れ） | 24 → 28 | title 28 |
| `egg.grassland.focus.name` | Precious Meadow Egg | 草原のとっておきタマゴ | 19 → 22 | title 22 |
| `egg.odds.title` | Egg odds | タマゴの確率 | 8 → 12 | 150% |
| `feature.shop` | Shop | ショップ | 4 → 8 | 200% |
| `hud.play` | Play | あそぶ | 4 → 6 | 150% |
| `journal.001` | Hatch {n} eggs | タマゴを{n}個かえそう | 14 → 21 | 150% |
| `journal.002` | Discover {n} variants | 新しいカラーを{n}個見つけよう | 21 → 29 | 138% |
| `journal.005` | Hatch {n} precious eggs | とっておきタマゴを{n}個かえそう | 23 → 31 | 135% |
| `journal.008` | Say hi to {n} rare visitors | めずらしいお客さん{n}匹にあいさつしよう | 27 → 39 | 144% |
| `journal.015` | Clear {n} Cuddle Match levels | ぎゅっとマッチで{n}ステージクリアしよう | 29 → 39 | 134% |
| `journal.017` | Catch {n} nuts | 木の実を{n}個キャッチしよう | 14 → 27 | 193% |
| `journal.022` | Open {n} daily gifts | 毎日のプレゼントを{n}回開けよう | 20 → 31 | 155% |
| `journal.028` | Water {n} sprouts | {n}個の芽に水をあげよう | 17 → 23 | 135% |
| `journal.030` | Harvest {n} dyes | 染料を{n}個収穫しよう | 16 → 21 | 131% |
| `journal.031` | Earn {n} bond points | なかよしポイントを{n}ためよう | 20 → 29 | 145% |
| `journal.032` | Give {n} treats | おやつを{n}個あげよう | 15 → 21 | 140% |
| `journal.033` | Groom {n} animals | {n}匹の毛づくろいをしよう | 17 → 25 | 147% |
| `journal.034` | Tap {n} animals | どうぶつを{n}匹タップしよう | 15 → 27 | 180% |
| `journal.039` | Bake {n} cakes | ケーキを{n}個焼こう | 14 → 19 | 136% |
| `journal.040` | Give {n} cakes | ケーキを{n}個プレゼントしよう | 14 → 29 | 207% |
| `journal.044` | Enter the Grand Contest | グランドコンテストに参加しよう | 23 → 30 | 130% |
| `journal.046` | Earn {n} album stars | アルバムの星を{n}個集めよう | 20 → 27 | 135% |
| `journal.047` | Pop {n} bubbles | シャボン玉を{n}個割ろう | 15 → 23 | 153% |
| `journal.048` | Glide {n} meters | {n}メートル滑空しよう | 16 → 21 | 131% |
| `journal.050` | Finish {n} Tile Puzzles | スライドパズルを{n}回クリアしよう | 23 → 33 | 143% |
| `journal.051` | Land {n} perfect hops | パーフェクトジャンプを{n}回決めよう | 21 → 35 | 167% |
| `journal.claim` | Claim | 受け取る | 5 → 8 | 160% |
| `plant.sweet_clover.name` | Sweet Clover | スイートクローバー | 12 → 18 | 150% |
| `rarity.epic` | Epic | エピック | 4 → 8 | 200% |
| `rarity.fabulous` | Fabulous | ファビュラス | 8 → 12 | 150% |
| `return.subtitle_big` | A big gift for you | 大きなプレゼントをどうぞ | 18 → 24 | 133%, title 24 |
| `sheet.bond` | Bond | なかよし度 | 4 → 10 | 250% |
| `shop.badge.owned` | Owned | 購入済み | 5 → 8 | 160% |
| `shop.purchase.failed` | Purchase failed. Try again. | 購入できませんでした。もう一度お試しください。 | 27 → 46 | 170% |
| `shop.purchase.thanks` | Thank you. +{0} Gems | ありがとうございます。+{0}ジェム | 20 → 32 | 160% |
| `shop.purchase.thanks_item` | Thank you | ありがとうございます | 9 → 20 | 222% |
| `shop.restore.none` | Nothing to restore | 復元する購入はありません | 18 → 24 | 133% |
| `shop.section.featured` | Featured | ピックアップ | 8 → 12 | 150% |
| `shop.section.gems` | Gems | ジェム | 4 → 6 | 150% |
| `shop.starter_pack.title` | Starter Pack | スターターパック | 12 → 16 | 133% |
| `shop.title` | Shop | ショップ | 4 → 8 | 200% |
| `shop.unavailable` | Shop unavailable. Try again later. | ショップを開けません。あとでもう一度お試しください。 | 34 → 52 | 153% |
| `summary.title` | Welcome to the refuge | どうぶつの里へようこそ | 21 → 22 | title 22 |
| `toast.shop_open` | The shop is open | ショップがオープンしました | 16 → 26 | 162% |
| `toast.wish_done` | Wish come true | おねがいがかなったよ | 14 → 20 | 143% |
| `today.claim` | Claim | 受け取る | 5 → 8 | 160% |
| `today.claim_all` | Claim all | すべて受け取る | 9 → 14 | 156% |
| `treat.cherry_bite.name` | Cherry Bite | さくらんぼひとくち | 11 → 18 | 164% |
| `treat.clover_crunch.name` | Clover Crunch | サクサククローバー | 13 → 18 | 138% |
| `treat.mango_bite.name` | Mango Bite | マンゴーひとくち | 10 → 16 | 160% |
| `treat.moon_honey.name` | Moon Honey Drops | 月のはちみつドロップ | 16 → 20 | title 20 |
| `treat.star_candy.name` | Star Candy | 星のキャンディ | 10 → 14 | 140% |
| `treat.treat_basket.name` | Treat Basket | おやつバスケット | 12 → 16 | 133% |
| `ui.cancel.button` | Cancel | キャンセル | 6 → 10 | 167% |
| `upgrades.locked.mastery` | {mastery} {level} | {mastery} Lv.{level} | 7 → 10 | 143% |
| `upgrades.production.name` | {biome} Glow | {biome}のあかり | 8 → 11 | 138% |
| `variant.hedgehog.cream.name` | Cream | クリーム | 5 → 8 | 160% |
| `variant.squirrel.cream.name` | Cream | クリーム | 5 → 8 | 160% |
| `wish.001` | Hatch {n} eggs | タマゴを{n}個かえそう | 14 → 21 | 150% |
| `wish.002` | Hatch {n} {biome} eggs | {biome}のタマゴを{n}個かえそう | 18 → 26 | 144% |
| `wish.004` | Discover a variant | 新しいカラーを見つけよう | 18 → 24 | 133% |
| `wish.012` | Say hi to {visitor} | {visitor}にあいさつしよう | 13 → 19 | 146% |
| `wish.014` | Tap {n} animals | どうぶつを{n}匹タップしよう | 15 → 27 | 180% |
| `wish.016` | Give {animal} a treat | {animal}におやつをあげよう | 16 → 21 | 131% |
| `wish.017` | Give {n} treats | おやつを{n}個あげよう | 15 → 21 | 140% |
| `wish.018` | Earn {n} bond points | なかよしポイントを{n}ためよう | 20 → 29 | 145% |
| `wish.019` | Groom {animal} | {animal}の毛づくろいをしよう | 9 → 23 | 256% |
| `wish.023` | Dress up {animal} | {animal}をおしゃれさせよう | 12 → 21 | 175% |
| `wish.026.nests` | Upgrade Cozy Nests in {biome_the} | {biome_the}の「ふかふかの巣」を強化しよう | 25 → 33 | 132% |
| `wish.026.production` | Upgrade {biome} Glow | 「{biome}のあかり」を強化しよう | 16 → 27 | 169% |
| `wish.033` | Water {n} sprouts | {n}個の芽に水をあげよう | 17 → 23 | 135% |
| `wish.034` | Harvest {n} {crop} | {crop}を{n}個収穫しよう | 15 → 20 | 133% |
| `wish.035` | Bring {animal} {n} {crop} | {animal}に{crop}を{n}個とどけよう | 17 → 25 | 147% |
| `wish.040` | Bake {n} cakes | ケーキを{n}個焼こう | 14 → 19 | 136% |
| `wish.046` | Head to {destination} | {destination}へ出かけよう | 11 → 15 | 136% |
| `wish.050` | Photograph {animal} napping | おひるね中の{animal}の写真をとろう | 22 → 29 | 132% |
| `wish.052` | Find {n} hidden animals | かくれたどうぶつを{n}匹見つけよう | 23 → 33 | 143% |
| `wish.054` | Play Nut Drop | 木の実キャッチであそぼう | 13 → 24 | 185% |
| `wish.063` | Play Lily Pad Hop | スイレンぴょんぴょんであそぼう | 17 → 30 | 176% |
| `wish.065` | Land {n} perfect hops | パーフェクトジャンプを{n}回決めよう | 21 → 35 | 167% |
| `wish.066` | Clear {n} Cuddle Match levels | ぎゅっとマッチで{n}ステージクリアしよう | 29 → 39 | 134% |
| `wish.068` | Make {n} big hugs | 大きなぎゅっを{n}回つくろう | 17 → 27 | 159% |
| `wish.069` | Play Glide | ふんわり滑空であそぼう | 10 → 22 | 220% |
| `wish.076` | Pop {n} bubbles | シャボン玉を{n}個割ろう | 15 → 23 | 153% |
| `wish.078` | Finish a Tile Puzzle | スライドパズルをクリアしよう | 20 → 28 | 140% |
| `wish.079` | Finish a 16-tile puzzle | 16枚のスライドパズルをクリアしよう | 23 → 34 | 148% |
| `wish.claim` | Claim | 受け取る | 5 → 8 | 160% |
| `wish.done` | Wish come true | おねがいがかなったよ | 14 → 20 | 143% |
| `wish.t01` | Hatch {n} eggs | タマゴを{n}個かえそう | 14 → 21 | 150% |
| `wish.title` | Wishes | おねがい | 6 → 8 | 133% |
| `zone.grassland.start.name` | the Start | はじまりの地 | 9 → 12 | 133% |
| `zone.locked.mastery` | {mastery} {level} first | 先に{mastery}をLv.{level}に | 13 → 17 | 131% |
| `zone.open.button` | Open | ひらく | 4 → 6 | 150% |
| `zone.open.title` | Open {zone} | {zone}をひらく | 8 → 11 | 138% |

### ko: 46 keys

| Key | English | Draft | Width en → draft | Why |
|---|---|---|---|---|
| `activity.glide.name` | Glide | 두둥실 활공 | 5 → 11 | 220% |
| `activity.grooming.sheet_groom` | Groom | 빗질하기 | 5 → 8 | 160% |
| `activity.hub.play` | Play | 플레이 | 4 → 6 | 150% |
| `activity.pause.quit` | Quit | 그만하기 | 4 → 8 | 200% |
| `activity.pause.resume` | Resume | 계속하기 | 6 → 8 | 133% |
| `activity.pause.title` | Paused | 일시 정지 | 6 → 9 | 150% |
| `activity.result.back` | Back | 돌아가기 | 4 → 8 | 200% |
| `activity.result.best` | Best: {n} | 최고 기록: {n} | 9 → 14 | 156% |
| `activity.result.title2` | Great job | 정말 잘했어요 | 9 → 13 | 144% |
| `challenge.grooming.07.name` | Big Buddy | 덩치 큰 친구 | 9 → 12 | 133% |
| `daily.calendar.stamp` | Stamp | 도장 찍기 | 5 → 9 | 180% |
| `decor.daisy_carousel_dusk.name` | Dusk Daisy Carousel | 노을 데이지 회전목마 | 19 → 20 | title 20 |
| `decor.giant_watering_can_dusk.name` | Dusk Giant Flower Watering Can | 노을 커다란 꽃 물뿌리개 | 30 → 23 | title 23 |
| `decor.round_garden_lantern_dusk.name` | Dusk Round Garden Lantern | 노을 둥근 정원 랜턴 | 25 → 19 | title 19 |
| `hud.play` | Play | 플레이 | 4 → 6 | 150% |
| `journal.033` | Groom {n} animals | 동물 {n}마리 털 손질하기 | 17 → 24 | 141% |
| `journal.034` | Tap {n} animals | 동물 {n}마리 터치하기 | 15 → 21 | 140% |
| `journal.036` | Buy {n} upgrades | 업그레이드 {n}번 하기 | 16 → 21 | 131% |
| `journal.040` | Give {n} cakes | 케이크 {n}개 선물하기 | 14 → 21 | 150% |
| `journal.047` | Pop {n} bubbles | 비눗방울 {n}개 터뜨리기 | 15 → 23 | 153% |
| `offline.welcome_back` | Welcome back | 다시 만나 반가워요 | 12 → 18 | 150% |
| `return.title` | Welcome back | 다시 만나 반가워요 | 12 → 18 | 150% |
| `reward.unlock` | New: {feature} | 새 기능: {feature} | 8 → 12 | 150% |
| `sheet.bond` | Bond | 친밀도 | 4 → 6 | 150% |
| `shop.badge.owned` | Owned | 구매 완료 | 5 → 9 | 180% |
| `shop.purchase.failed` | Purchase failed. Try again. | 구매하지 못했어요. 다시 시도해 주세요. | 27 → 38 | 141% |
| `shop.unavailable` | Shop unavailable. Try again later. | 상점을 열 수 없어요. 나중에 다시 시도해 주세요. | 34 → 47 | 138% |
| `summary.title` | Welcome to the refuge | 보금자리에 온 걸 환영해요 | 21 → 25 | title 25 |
| `toast.wish_done` | Wish come true | 소원이 이루어졌어요 | 14 → 19 | 136% |
| `ui.ok.button` | OK | 확인 | 2 → 4 | 200% |
| `upgrades.locked.mastery` | {mastery} {level} | {mastery} Lv.{level} | 7 → 10 | 143% |
| `upgrades.max` | Maxed | 최대 레벨 | 5 → 9 | 180% |
| `variant.squirrel.red.name` | Red | 붉은색 | 3 → 6 | 200% |
| `wish.014` | Tap {n} animals | 동물 {n}마리 터치하기 | 15 → 21 | 140% |
| `wish.019` | Groom {animal} | {animal} 털 손질하기 | 9 → 15 | 167% |
| `wish.025` | Buy {n} upgrades | 업그레이드 {n}번 하기 | 16 → 21 | 131% |
| `wish.026` | Upgrade {upgrade} in {biome_the} | {biome_the}의 {upgrade} 업그레이드하기 | 18 → 24 | 133% |
| `wish.026.curiosity` | Upgrade Curious Eye in {biome_the} | {biome_the}의 ‘호기심의 눈’ 업그레이드하기 | 26 → 34 | 131% |
| `wish.026.nests` | Upgrade Cozy Nests in {biome_the} | {biome_the}의 ‘포근한 둥지’ 업그레이드하기 | 25 → 34 | 136% |
| `wish.026.production` | Upgrade {biome} Glow | ‘{biome}의 햇살’ 업그레이드하기 | 16 → 27 | 169% |
| `wish.035` | Bring {animal} {n} {crop} | {animal}에게 {crop} {n}개 가져다주기 | 17 → 28 | 165% |
| `wish.054` | Play Nut Drop | 열매 받기 한 판 하기 | 13 → 20 | 154% |
| `wish.069` | Play Glide | 두둥실 활공 한 판 하기 | 10 → 22 | 220% |
| `wish.076` | Pop {n} bubbles | 비눗방울 {n}개 터뜨리기 | 15 → 23 | 153% |
| `wish.done` | Wish come true | 소원이 이루어졌어요 | 14 → 19 | 136% |
| `wish.swap` | Swap | 바꾸기 | 4 → 6 | 150% |

### zh-Hans: 6 keys

| Key | English | Draft | Width en → draft | Why |
|---|---|---|---|---|
| `activity.grooming.tip.bow` | Pick a bow | 选一个蝴蝶结吧 | 10 → 14 | 140% |
| `codex.unknown` | ??? | ？？？ | 3 → 6 | 200% |
| `daily.welcome.day` | Day {n} of 3 | 第{n}天（共3天） | 12 → 16 | 133% |
| `sheet.bond` | Bond | 亲密度 | 4 → 6 | 150% |
| `ui.ok.button` | OK | 确定 | 2 → 4 | 200% |
| `zone.locked.mastery` | {mastery} {level} first | 先将{mastery}提升到{level}级 | 13 → 18 | 138% |

### zh-Hant: 6 keys

| Key | English | Draft | Width en → draft | Why |
|---|---|---|---|---|
| `activity.grooming.tip.bow` | Pick a bow | 選一個蝴蝶結吧 | 10 → 14 | 140% |
| `codex.unknown` | ??? | ？？？ | 3 → 6 | 200% |
| `daily.welcome.day` | Day {n} of 3 | 第{n}天（共3天） | 12 → 16 | 133% |
| `sheet.bond` | Bond | 親密度 | 4 → 6 | 150% |
| `ui.ok.button` | OK | 確定 | 2 → 4 | 200% |
| `zone.locked.mastery` | {mastery} {level} first | 先把{mastery}提升到{level}級 | 13 → 18 | 138% |

