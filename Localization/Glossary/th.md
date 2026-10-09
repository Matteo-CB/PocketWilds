# Pocket Wilds glossary: Thai (th)

Draft of 9 October 2026, agent TR3. Fixed terms for every Thai text of the game, so later texts stay consistent.
Source: `Localization/Drafts/th.json`. Forbidden words: `th_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR3.md`. Characters used: `Drafts/charset_th.txt`.

## Tone and writing

- Friendly and polite, never pushy. « คุณ » for « you » when a pronoun is needed; soft particles where natural
  (« นะ »: « ขอบคุณนะ », « ลองอีกครั้งนะ », « เจอกันพรุ่งนี้นะ »). No « ครับ / ค่ะ »: the game has no gender.
- Goals (wishes, weekly journal, challenges) are short verb phrases with a classifier after the number:
  « ฟักไข่ 3 ฟอง », « หาสัตว์ 5 ตัว ».
- Pets and species in sentences get the affectionate « น้อง »: {species_the} is « น้องกระต่าย », « น้องกระรอก »,
  « น้องเม่น ».
- Line breaks: U+200B (zero-width space) between words, a normal space between phrases and around numbers. A break is
  never placed inside a syllable (before a following vowel or tone mark, or after a leading vowel): the checker
  reports it. Compound words stay whole (ของขวัญ, ร้านค้า, ทุ่งหญ้า, กิจกรรม, ความปรารถนา).
- Repetition mark « ๆ » after a space, as in standard Thai: « เร็ว ๆ นี้ », « สั้น ๆ ».
- Numbers: Arabic digits; decimal point (× 1.25); « 50% ». Units นาที, ชม., วิ.
- No em dash. Brand: Pocket Wilds stays in Latin letters.
- Sun and moon are « ดวงอาทิตย์ », « ดวงจันทร์ » or « จันทรา », never the forms with the honorific prefix (see
  `th_forbidden.txt`).

## Classifiers

| Thing | Classifier | Example |
|---|---|---|
| eggs | ฟอง | ฟักไข่ {n} ฟอง |
| animals, visitors, babies | ตัว | สัตว์ {n} ตัว |
| tickets | ใบ | ตั๋ว {n} ใบ |
| treats, decorations, cakes, treasures | ชิ้น | ขนม {n} ชิ้น |
| seeds | เมล็ด | เมล็ด {n} เมล็ด |
| stickers | แผ่น | สติกเกอร์ {n} แผ่น |
| stars | ดวง | ดาว {n} ดวง |
| nuts | เม็ด | ถั่ว {n} เม็ด |
| bubbles | ลูก | {n} ลูก |
| plants, sprouts | ต้น | {n} ต้น |
| wishes (items) | ข้อ | {n} ข้อ |
| bond and garden points | แต้ม | {n} แต้ม |
| times | ครั้ง | {n} ครั้ง |
| songs | เพลง | {n} เพลง |
| Flower Sugar | ก้อน | น้ำตาลดอกไม้ {n} ก้อน |

## Plurals

Thai has one form (CLDR other). Counted keys keep « .other » only; the number and its classifier carry the count.

## Species and animals

| English | Thai | Notes |
|---|---|---|
| Rabbit | กระต่าย | in sentences: น้องกระต่าย |
| Squirrel | กระรอก | น้องกระรอก |
| Hedgehog | เม่น | น้องเม่น |
| Axolotl | แอกโซลอเติล | |
| species (the word) | ชนิด, สัตว์ชนิดใหม่ | « สัตว์ชนิดใหม่! » |
| animal | สัตว์ | |
| newborn | ลูกน้อยแรกเกิด | |
| visitor | แขก | « แขกยามค่ำคืน » |
| variant (coat color) | สีขน | « สีขนใหม่! »; Brown สีน้ำตาล, White สีขาว, Grey สีเทา, Black สีดำ, Cream สีครีม, Red สีส้ม, Spotted ลายจุด, Sakura ซากุระ, Autumn ใบไม้ร่วง, Mushroom เห็ด |
| rare traits | ลักษณะหายาก | |
| bond | ความผูกพัน | |

## Currencies and items

| English | Thai | Notes |
|---|---|---|
| Gems | เจม | the word Thai players use |
| Light | แสง | |
| ticket | ตั๋ว | |
| treat | ขนม | the sheet button: « ให้ขนม » |
| Mushroom Biscuit | บิสกิตเห็ด | |
| sticker | สติกเกอร์ | |
| Flower Sugar | น้ำตาลดอกไม้ | |
| seed | เมล็ด, เมล็ดพันธุ์ | |
| decoration | ของตกแต่ง | |
| Discovery Egg | ไข่แห่งการค้นพบ | |
| Meadow Egg | ไข่ทุ่งหญ้า, Precious: ไข่ทุ่งหญ้าล้ำค่า | |
| Starter Pack | แพ็กเริ่มต้น | |
| Dusk (decoration line) | ยามสนธยา | after the name |
| watering can | กระป๋องรดน้ำ | the usual word contains a forbidden stem |

## Places

| English | Thai | Notes |
|---|---|---|
| refuge | บ้านพักพิง | |
| Meadow | ทุ่งหญ้า | |
| Forest | ป่า | |
| Lake | ทะเลสาบ | |
| Mountain | ภูเขา | |
| Cloudlands (future biome) | หมู่เกาะเมฆ | |
| Starlight (future biome) | ท้องฟ้าเต็มดาว | |
| biome | ดินแดน | |
| zone | โซน | |
| zones of the Meadow | จุดเริ่มต้น, ทุ่งดอกไม้, โค้งลำธาร, สวนผลไม้, เนินเขา, เนินกังหันลม, ริมบึง, ชายป่า | |
| cairn (future landmark) | กองหิน | |

## Rarities

ทั่วไป (Common), พบไม่บ่อย (Uncommon), หายาก (Rare), ล้ำค่า (Epic), ในตำนาน (Legendary), แสนวิเศษ (Fabulous).

## Activities

| English | Thai |
|---|---|
| Activities (hub) | กิจกรรม |
| Grooming | ดูแลขน (verb: แปรงขน) |
| Hide and Seek | ซ่อนหา |
| Nut Drop | ฝนถั่ว |
| Pawprint Memory | จับคู่รอยเท้า |
| Expeditions | ออกสำรวจ |
| Garden | สวน |
| Treasure Fishing | ตกสมบัติ |
| Lily Pad Hop | กระโดดข้ามบึง (the usual lily pad word is on the forbidden list) |
| Baking | อบขนม |
| Photo Safari | ซาฟารีถ่ายภาพ |
| Cuddle Match | กอดจับคู่ |
| Glide | ร่อนลม |
| Constellations | กลุ่มดาว |
| Firefly Rhythm | จังหวะหิ่งห้อย |
| Bubbles | ฟองสบู่ |
| Tile Puzzle | ต่อภาพเลื่อน |
| Burrow Maze | เขาวงกตโพรง |
| Grand Contest | การประกวดใหญ่ |
| challenge | ด่านท้าทาย |
| practice | ฝึกซ้อม |
| streak | ต่อเนื่อง |
| record, best | สถิติ |

## Features

| English | Thai |
|---|---|
| rendez-vous (daily visit) | นัดประจำวัน |
| Today | วันนี้ |
| wish, wishes | ความปรารถนา; « ความปรารถนาเป็นจริงแล้ว » |
| daily gift | ของขวัญประจำวัน |
| welcome gift | ของขวัญต้อนรับ |
| calendar, stamp | ปฏิทิน, ประทับตรา (a box: ช่อง) |
| weekly journal | สมุดบันทึกประจำสัปดาห์ |
| album | อัลบั้ม |
| Codex | สมุดภาพ |
| mastery | ความชำนาญ |
| level | เลเวล (Lv. in short labels) |
| upgrade | อัปเกรด |
| glow | แสงเรือง |

## UI verbs

| English | Thai |
|---|---|
| Collect (Light) | เก็บ |
| Claim | รับ; Claim all: รับทั้งหมด |
| Play | เล่น |
| Play again | เล่นอีกครั้ง |
| Continue | ต่อไป; Resume: เล่นต่อ |
| Close | ปิด |
| Cancel | ยกเลิก |
| OK | ตกลง |
| Open | เปิด |
| Swap | เปลี่ยน |
| See | ดู |
| Back | กลับ |
| Quit | ออก |
| Details | รายละเอียด |
