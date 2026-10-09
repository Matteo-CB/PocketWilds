# Pocket Wilds glossary: Korean (ko)

Draft of 9 October 2026, agent TR1. Fixed terms for every Korean text of the game, so later texts stay consistent.
Source: `Localization/Drafts/ko.json`. Forbidden words: `ko_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR1.md`.

## Tone

- Polite and friendly 해요체 for every sentence: tips, toasts, messages (「보석이 아직 부족해요」「상점이 열렸어요」).
- Goals (wishes, weekly journal, challenges) use the nominal 「~하기」 form of Korean game missions:
  「알 {n}개 부화하기」「{animal}에게 간식 주기」.
- Buttons are short: 받기, 닫기, 열기, 보기, 바꾸기, 계속하기, 플레이, 취소, 확인.
- Never pushy: no 지금 바로, 서둘러, 놓치지 마세요, 한정.
- Western punctuation with Korean spacing (띄어쓰기). Quotes ‘ ’ around a name inside a sentence.
- Numbers and counters are written together: 「{n}개」「{n}마리」「{n}장」「{n}번」.
- Brand: Pocket Wilds stays in Latin letters.

## Particles after placeholders (important)

The particles 은/는, 이/가, 을/를, 와/과, (으)로 change with the last syllable of the word before them, which a
placeholder ({animal}, {species}, {biome}, {zone}, {visitor}, {destination}, {crop}, {cake}, {upgrade}, {mastery},
{feature}, {reward}, {goal}, {first}, {second}) does not know. The drafts therefore:

- use only invariant particles after a placeholder: 에, 에서, 에게, 의, 도, 만, 까지;
- or drop the particle, as Korean UI does: 「{animal} 털 손질하기」「{zone} 열기」「도감에서 {species_the} 살펴보기」;
- join two conditions with 그리고 / 또는: 「{first} 그리고 {second}」.

T39B may add a 받침 helper (or forms like 을(를)) if a future sentence really needs a variable particle.

## Counters

| Thing | Counter | Example |
|---|---|---|
| eggs, treats, seeds, stars, decorations, wishes, bubbles, plants, Gems | 개 | 알 {n}개 부화하기, 보석 {n}개 |
| animals (visitors, babies) | 마리 | 동물 {n}마리 |
| stickers, tickets, photos | 장 | 스티커 {n}장 |
| times | 번 | {n}번 |
| days | 일 / 일째 | {n}일 남음, {n}일째 |
| songs | 곡 | {n}곡 |
| pairs | 쌍 | {n}쌍 |
| places | 곳 | {n}곳 |
| bond and garden points | 점 | 친밀도 {n}점 |
| Light | none (the name comes first) | 빛 {n} |

## Species

| English | Korean | Notes |
|---|---|---|
| Rabbit | 토끼 | |
| Squirrel | 다람쥐 | |
| Hedgehog | 고슴도치 | |
| Axolotl | 우파루파 | the popular name in Korea |
| species (the word) | 종 | 「새로운 종!」「종 숙련도」 |
| animal | 동물 | |
| baby, newborn | 아기 | |
| visitor | 손님 | |
| variant (color of a species) | 색상 | 「새로운 색상!」 |
| Brown, White, Grey, Black, Cream | 갈색, 흰색, 회색, 검은색, 크림색 | |
| Sakura, Spotted, Mushroom, Autumn, Red | 벚꽃, 점박이, 버섯, 가을, 붉은색 | |
| rare traits | 희귀 특성 | |

## Currencies and items

| English | Korean | Notes |
|---|---|---|
| Gems | 보석 | 「보석 {n}개」「보석 +{n}」 |
| Light | 빛 | 「빛 {n}」「빛이 아직 부족해요」 |
| {biome} Glow (upgrade) | {biome}의 햇살 | 햇살 (sunshine) keeps it apart from the 빛 currency |
| tickets | 티켓 | 「티켓 {n}장」 |
| treats | 간식 | |
| Mushroom Biscuit | 버섯 비스킷 | |
| Golden Treat | 황금 간식 | |
| Star Candy | 별사탕 | the well-known Korean candy |
| stickers | 스티커 | |
| Flower Sugar | 꽃 설탕 | |
| seeds / sprouts / dyes | 씨앗 / 새싹 / 염료 | |
| decoration | 장식 | |
| egg | 알 | |
| Discovery Egg | 발견의 알 | |
| Meadow Egg / Precious Meadow Egg | 초원 알 / 소중한 초원 알 | precious = 소중한 |
| Flower Crown | 화관 | |
| bow (grooming) | 리본 | |
| burrs | 도꼬마리 | the cocklebur plant |
| postcard | 엽서 | |
| Dusk (decoration tint) | 노을 | prefix: 「노을 새집」 |

## Places

| English | Korean | Notes |
|---|---|---|
| refuge | 보금자리 | a cozy nest-home: 「보금자리에 온 걸 환영해요」「보금자리 레벨」 |
| biome | 지역 | |
| zone | 구역 | |
| Meadow (biome) | 초원 | |
| Forest, Lake, Mountain | 숲, 호수, 산 | |
| the Start | 출발점 | |
| the Flower Meadow | 꽃밭 | |
| the Stream Bend | 개울 굽이 | |
| the Orchard | 과수원 | |
| the Hill | 언덕 | |
| Windmill Rise | 풍차 둔덕 | |
| the Pond Edge | 연못가 | |
| the Forest Edge | 숲 가장자리 | |

## Rarities

| English | Korean |
|---|---|
| Common | 일반 |
| Uncommon | 고급 |
| Rare | 희귀 |
| Epic | 영웅 |
| Legendary | 전설 |
| Fabulous | 환상 (from 환상적인, "fabulous") |

The usual Korean name of the tier the vocabulary rule replaced is not used.

## Activities

| English | Korean |
|---|---|
| activities (hub) | 놀이 |
| Grooming | 털 손질 (sheet button: 빗질하기) |
| Hide and Seek | 숨바꼭질 |
| Nut Drop | 열매 받기 |
| Pawprint Memory | 발자국 짝 맞추기 (short: 짝 맞추기) |
| Expeditions | 나들이 |
| Garden | 정원 |
| Treasure Fishing | 보물 낚시 |
| Lily Pad Hop | 수련잎 폴짝 (수련, water lily, never the other water flower) |
| Baking | 베이킹 |
| Photo Safari | 사진 사파리 |
| Cuddle Match | 꼬옥 매치 (big hug: 큰 포옹) |
| Glide | 두둥실 활공 |
| Constellations | 별자리 |
| Firefly Rhythm | 반딧불 리듬 |
| Bubbles | 비눗방울 |
| Tile Puzzle | 슬라이드 퍼즐 |
| Burrow Maze | 굴속 미로 |
| Grand Contest | 그랜드 콘테스트 |
| challenge | 도전 |
| practice | 연습 |
| streak | 연속; in the rhythm game 콤보 |

## Features

| English | Korean |
|---|---|
| daily rendez-vous (the whole feature) | 매일의 만남 |
| Today | 오늘 |
| daily gift | 오늘의 선물 |
| wishes | 소원 (「소원 이루기」「소원이 이루어졌어요」) |
| calendar | 달력; stamp: 도장 (「도장 찍기」) |
| weekly journal | 주간 일지 (「이번 주 일지」) |
| album | 앨범 |
| Codex | 도감 |
| bond | 친밀도; bond points: 친밀도 점수 (「친밀도 {n}점」) |
| mastery | 숙련도 |
| upgrades | 업그레이드 |
| level | 레벨; short: Lv. |
| shop | 상점; Starter Pack: 스타터 팩 |
| hatch | 부화하기 |
| breeding | 번식 |

## UI verbs and buttons

| English | Korean |
|---|---|
| Collect, Claim | 받기 |
| Claim all | 모두 받기 |
| Play | 플레이 |
| Continue, Resume | 계속하기 |
| Close | 닫기 |
| Back | 돌아가기 |
| Quit | 그만하기 |
| Open | 열기 |
| See | 보기 |
| Swap | 바꾸기 |
| Stamp | 도장 찍기 |
| Cancel / OK | 취소 / 확인 |
| Free | 무료 |
| New (badge) | NEW |
| Details | 자세히 |
| Restore purchases | 구매 복원 |
