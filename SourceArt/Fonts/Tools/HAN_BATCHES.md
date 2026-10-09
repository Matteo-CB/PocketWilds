# Han batches: the plan for drawing the rest of the ideographs

Pocket Round and Pocket Plush draw every Han character with our own engine (`pw_font_han.py`) from our own data:
stroke skeletons of components and IDS compositions, written by hand. This file splits the remaining characters
into lots so several agents can draw them in parallel without touching each other's files. The lot lists at the
end are written by `pw_font_han_batches.py plan` and are frozen once the batches start.

## State at the pause (9 October 2026)

The owner paused all language and font work until the game is finished. Where the work stands:

- Lots done: a01, a02, a03, b01, b02 (b02 is redrawing 8 glyphs on its own), b03, c01, c02, c03, c04, d01, e01, e02.
- Lots paused part-way: c05, c06, c07, c08, d02, e03.
- Lots not started: c09, d03.
- New brief rule: never read positions from a reference render. The system fonts only decide pass or fail
  through `pw_font_han_verify.py`; no grid, no coordinates, no "where is this stroke" taken from them.
  Drawings to review under this rule (made in the last round while comparing with references on a grid):
  豸 and 豸.L, 威, 咸 and their Taiwan and right-slot forms, 戋@J and 戋@S, 戔, 斑, 癸, 卷, 韋, 感@T, 灰@ST and
  灰.R@ST, 斥 and 斥@T, 奄, 奉, 史, 倉, 带, 龍 and 龍@T, 昼, 去.T, 柬, {ying_s}, {lib_xun6}.
- Shared files at the pause: they parse, the shared set loads alone, every lot loads with it, and everything
  loads together (checked after the last edit).

Shared changes of the last round (additions or in-place shape fixes, users re-verified with every lot loaded):
the 16 open faults fixed (see `han_lists/han_open_duels.txt`); 乂, 尧 (turning piě), 反, 巷, 离, 歹.L, 阝 and 阝.L
(roomier ear), 矢 (piě on the first bar), 犭, 钅, 扌 (higher tí), 山.L, 斥, 奄, 奉, 灰@ST, 倉, 气 (larger inner box),
戔, 龍, 昼, 带, 史, 柬, {ying_s}; new: `pw_font_han_components_lib3.txt` (NAME.W drawings for 寸 欠 舟 皿 故 旦
景 邦 昭, the promoted `{lib_*}` parts, 口.L@S / 口.L@T / 口.TL / 口.BL, 去.T, 并.L, 贝.B, 办, 冊.L / 册.L, 鹿.B,
骨.L by region); engine: whole-character drawings (NAME.W) and operator sub-boxes (`⿰@x0,y0,x1,y1`).

Queued for the consolidation pass (sent by the coordinator, not started as one pass):

1. Promote the best private parts into lib3 (first set done, see "Shared names added"); the final merge switches
   the lots to them.
2. Shapes still to fix: 雨 dots in 漏 (Bold, Plush) and 露 (Plush); 蓋@T (done through 去.T, check again); 旬 as
   a part (only `{lib_xun6}` added); 婁 in 11 strokes (Taiwan form designed, not applied); crowding or touching
   when squeezed: 鹿, 頁, 殳, 馬.L, 食.L@J, 有 narrow, 丬 (reads as 亻), the dots of 夹 and 交, 右 in 諾, 立 in 音,
   彎 in Plush, 闌@ST (柬 eased), 谷, 曼, 切, 夆, 艮, 夗, 凡, 辛, 豕; missing forms 尚 at full height, 巛 tall; the
   Taiwan ⺼ with two ticks (done inside 龍@T only); weak shared characters 花 and 菊; lot character lines that
   shadow a shared part (尚 矢 豊 貫 袁 昼 双 办: write them with `*` and promote a NAME.W).
3. Engine: `_close_joints` snaps short inner strokes onto nearby sides in Bold. A weight-independent threshold
   was tried and rejected (touches rose from 35 to 76 on a 1,200-glyph sample in SemiBold to Plush); a short-stroke
   rule is still to design.
4. Tested and not applied: the c01 雨.T (weaker 雲 零 雷 霜), switching 穴 to its top form (空 控 窝 read better
   with the full 穴), a smaller 勹 inner box (fewer touches, more duel losses), a new 力.B and a lower 春 head
   (both weaker). Designed, not tested: one-piece {xue_t}, 弗.T, 必.T, 帛 ratio, 黙 / 町 / 古 / 昔 proportions.
5. Re-verify every character, shared and lots together, in the three regions, and report the totals. Partial
   measure at the pause (Japan only, before the last fixes): 2,194 characters, 57 lost duels (37 shared, 20 in
   lots), touches 35 / 34 / 15 / 19 (Regular / SemiBold / Bold / Plush), mean structure 0.758. With every lot
   loaded the rival set is much larger, so many shared near pairs marked "ok on the sheet" lose again (78 of the
   124 entries of the open list); among them real proportion faults to fix first: 費, 密, 黙, 町, 営, 錦, 昔, 労 /
   劳, 使, 榛, 秦, 它, 尬, 建, 咖, 哨, 噴.

## Rules (same as the font brief)

- Our font only: no other font, no system or open font, no font-derived data (no outline, median, skeleton or
  metric taken from any font, no Make Me a Hanzi or similar). No download of any kind. Python with fontTools,
  numpy and Pillow only, no new package. No Unity.
- The Windows system fonts (Yu Gothic, Microsoft YaHei, Microsoft JhengHei) are rendered only to check that a
  glyph is the right character, through `pw_font_han_verify.py`. Never copy, trace, measure or store their
  shapes. Looking at the review sheet to see which stroke is missing is fine; copying its coordinates is not.
- Never edit `pw_font_engine.py`, `pw_font_glyphs.py`, `pw_font_build.py`, `pw_font_proof.py`, never build into
  `SourceArt/Fonts/Build/` itself. The shared Han files are not yours either (see below).
- Nothing religious in data, names, comments or samples (`Docs/Design/Vocabulary_Rule.md`); no four-point
  sparkle anywhere. Characters held for the owner (`han_lists/han_list_held.txt`) are not in any lot.
- Deterministic: the same data gives the same glyphs.

## Files

Shared (read only for the batches):

| File | What |
|---|---|
| `pw_font_han.py` | engine and data syntax (docstring at the top) |
| `pw_font_han_components.txt`, `_2`, `_3` | first milestone: radicals, position forms, simple characters |
| `pw_font_han_components_lib2.txt` | library round: more position forms, frames, phonetic parts, regional forms |
| `pw_font_han_components_lib3.txt` | consolidation round: whole-character drawings (`NAME.W`), parts promoted from the lots (`{lib_*}`), regional left 口, new position forms |
| `pw_font_han_ids.txt`, `pw_font_han_ids_lib2.txt` | characters already drawn (1,601 code points) |
| `pw_font_han_verify.py`, `pw_font_han_batches.py` | verification and the lot tools |
| `han_lists/` | planning lists: jōyō, Simplified, Traditional, held, regional; `han_lots.txt` (the frozen lots); `han_open_duels.txt` (shared characters the library owner is still polishing) |

Per lot (yours, nothing else):

- `pw_font_han_ids_<lot>.txt`: one line per character of your lot, `CHAR REGIONS STROKES IDS`.
- `pw_font_han_components_<lot>.txt` (only if needed): your private parts.

`<lot>` is the lot id in lower case, for example `c03`. The loader picks these files up by name.

## Private names

Every component you create is named `{<lot>_<ascii>}`, for example `{c03_qiao_r}` or `{d02_luan_t}`. Never
define a bare character name, a position form (`木.X`) or a regional form (`@J`) of a shared name in a lot file:
another lot may do the same and the merge would fail on the duplicate. Character lines (CHAR REGIONS ...) are
allowed only for the characters of your own lot and regions. When a shared part is wrong or missing, make a
private one, use it, and say so in your handover; the library owner promotes it to the shared files.

## Shared names added by the consolidation round

Additions only: nothing a lot composes with was renamed or removed.

- `NAME.W`: the drawing of NAME when it is the character itself. The bare NAME stays the narrow part the other
  characters compose with. When a character of your lot is also a shared part name (寸 欠 舟 旦 尚 矢 豊 昼 ...),
  write its line with `*`: an IDS on that line replaces the part in every character that uses it. Propose the
  full-face drawing in your handover; the library owner adds it as `NAME.W`.
- Operator sub-box: `⿰:0.4@0,6,100,100 A B` places the whole subtree in a part of its slot (the component form
  `A@x0,y0,x1,y1` still works).
- Promoted parts, for the later lots: `{lib_ju}` 巨 (Taiwan form by region), `{lib_jia}` 叚, `{lib_mao}` 卯,
  `{lib_xun}` 卂, `{lib_gao}` 告 whole, `{lib_ni}` 屰 and `{lib_ni_l}` its left form, `{lib_pai_r}` 𠂢,
  `{lib_jiang_r}` 夅, `{lib_liao}` 尞, `{lib_ji_l}` the left of 即 / 既, `{lib_jii}` 既 whole, `{lib_jin}` 堇 as a
  right part, `{lib_fu}` 巿, `{lib_shu_l}` 尗, `{lib_xun6}` 旬 whole, `{lib_xue_t}` 穴 as a flat top.
- Position forms: `口.L@S` and `口.L@T` (taller left 口, Taiwan tall and narrow), `口.TL` / `口.BL` (the corners of a
  2 x 2 stack), `去.T` (flat 去 over or between parts).

## Working on a lot

PowerShell, from `SourceArt/Fonts/Tools`:

```
$env:PW_HAN_LOTS = 'c03'                 # load the shared files and lot c03 only
python pw_font_han_batches.py lot c03    # the characters, grouped by regions, game characters first
python pw_font_han_batches.py parts      # every component name you can compose with
python pw_font_han_verify.py SC 字字字 --tag c03   # quick check of a few characters while drawing
python pw_font_han_batches.py check c03  # the full check of the lot (sets PW_HAN_LOTS itself)
```

The check writes `TestResults/Fonts/Han/lots/<lot>_check.txt`, a proof sheet per region
(`<lot>_<REGION>.png`, Regular and Plush side by side) and review sheets
(`TestResults/Fonts/Han/review_<REGION>_<lot>_NN.png`, ours left, the reference right in grey, the doubtful first).

Write the IDS over shared parts first; draw a primitive (stroke skeleton) only when no composition looks right.
Use explicit ratios (`⿱:0.42`) and sub-boxes (`口@14,0,86,100`) when the automatic split is off; the left,
top, bottom and outer forms are picked by position automatically when they exist (`木.L`, `日.T`, `户` frames).

Stroke counts are counted as we draw them: 阝 and 廴 in two strokes, 钅 in four, 佥 in six, 臣 in six, 以 in four,
the Taiwan 艹 in four. Write the drawn count; when it differs from a dictionary count only by such a habit, keep
the drawn count.

## Done criteria for a lot

1. Every character of the lot is drawn for every region of its line, or listed in the handover as deferred
   with the reason.
2. `check` reports 0 wrong in every region. A character is wrong when its glyph sides with another
   character against the right one on the pixels where the two references differ (registered duel). Fix the
   proportions first (explicit ratios, sub-boxes, a private part). If a duel loss remains and the review sheet
   shows the right character beyond doubt (the rival only shares a proportion our rounder design does not), list
   it in the handover with its rival and the duel value; the main session decides.
3. Stroke counts match the STROKES column (drawn count), or the difference is explained.
4. Touches: none left in Regular; in SemiBold, Bold and Plush only the pairs the density makes unavoidable,
   listed in the handover.
5. You looked at the proof sheet and the review sheets: nothing looks wrong, nothing is blobby at the proof
   size.
6. Nothing outside your lot files changed. The handover gives: characters drawn per region, deferred ones,
   private parts worth promoting, shared parts you found wrong.

The main session then merges: it loads every lot together (no `PW_HAN_LOTS`), runs the full verification,
rebuilds the test fonts and the proofs.

## Batches

- **A**: characters of the jōyō, the Simplified list and the Traditional list alike (one drawing for the three
  regions; regional components such as 艹, 户, 今, 骨 handle the small differences).
- **B**: the rest of the jōyō: Japanese forms, and shared code points drawn differently per region
  (`han_lists/han_list_regional.txt`, for example 器 突 練 欄 with 大 / 犬 and 東 / 柬).
- **C**: the Simplified list outside the jōyō (with Taiwan on the same line when the code point is shared).
- **D** and **E**: Traditional only, the more frequent half and the rest.

Inside a batch the characters the game's texts use come first (`Localization/Drafts/*.json` and
`charset_*.txt`, counted by `pw_font_han_extract.py --drafts`), most used first, then the planning lists'
order. Lots hold up to 250 characters; a lot is one agent's session.

The planning lists (`han_lists/`) are written from our own knowledge: the jōyō list is the official 2,136; the
Simplified list holds the usual 3,500 common characters and the rest of the national level-one set as we
recalled it; the Traditional list follows the Simplified one through our correspondence table and adds the
Taiwan forms and less frequent characters of the Taiwan common list. They are planning lists, not standards:
a character the game needs is added to its batch whatever the lists say.

## Lots

<!-- lots:start -->
Batch sizes: A 612, B 565, C 2037, D 740, E 739 (total 4693 characters, 21 lots of up to 250).

| Lot | Characters | Game | Regions |
|---|---|---|---|
| a01 | 250 | 17 | JST 246, JT 2, ST 1, J 1 |
| a02 | 250 | 0 | JST 250 |
| a03 | 112 | 0 | JST 110, ST 1, S 1 |
| b01 | 250 | 22 | JT 184, JS 52, J 8, JST 3, S 2, T 1 |
| b02 | 250 | 0 | JT 126, J 124 |
| b03 | 65 | 0 | J 65 |
| c01 | 250 | 21 | ST 126, S 121, T 3 |
| c02 | 250 | 0 | S 127, ST 123 |
| c03 | 250 | 0 | S 130, ST 120 |
| c04 | 250 | 0 | S 131, ST 119 |
| c05 | 250 | 0 | ST 127, S 123 |
| c06 | 250 | 0 | ST 165, S 85 |
| c07 | 250 | 0 | ST 154, S 96 |
| c08 | 250 | 0 | ST 181, S 69 |
| c09 | 37 | 0 | ST 22, S 15 |
| d01 | 250 | 29 | T 250 |
| d02 | 250 | 0 | T 250 |
| d03 | 240 | 0 | T 240 |
| e01 | 250 | 0 | T 250 |
| e02 | 250 | 0 | T 250 |
| e03 | 239 | 0 | T 239 |

### a01

JT (2):

```
后准
```

JST (246):

```
尺乾巨旋段留候戴往派降幻概即者民但便因重政才比性命表叫死反系司京制各清冷叔般且希束注科技治律宿拉擦搬
背握醒逃越蛇酒根梅荷柳藤菌露彩沙江峰街牙肩指掌膝腰皮肝肺眉登培索察布造采散士警演板沿微差究底甚波潮深
澄浮港源漂漏洪汁汗液激浴淑滋泣沸泌沃沼浸涯批抄折抵拘括拾挨挫授掘搭撤撮操材杯析枕柄柔柱柿栓案桑梗棋吐
吟否含吸吹告咽哀哲哺唯唾喉嗅嘲仁介仰企伏伐伯伴似佐例侍供侮侵促俗修倒偶催傲僚儒芋芝芳苛若苦荒萎葬蒸蓄
蔑薪肌肖股肢肥肪胎胞脂脊腐腺膜奴妄妙妥妨妻姓姻姿婆嫁嫉坊均垂埋基堂堆堤堪塑塔塞墨壁炊炭烈煎煮
```

ST (1):

```
丸
```

J (1):

```
拐
```

### a02

JST (250):

```
燥爆研硝硬磁盾省督睦瞬瞭距踏蚊租秩稚稼稽稿粒粗粘筋筒答籍宅宇宙宜宴宵寂富寒寡寸阻陪陵陶隆隔邪邦郊郎郭
彼徒御循忌忍怒怖怠急怪恨恩悔悟悠患悼惑惰愁愉愚慈慌慎慨慰憎憾犯狂猛猿祖祝祥禁裂裕裸褒襟班琴璃旦旨旬旺
昧昭景暇暑暴志忠念憩刃刊刑列判刹削剖副劣努勃勇勉勘募勤序庸廊疫疾症痕痛痘迅迫述逆逐逝逮逸遣避赴趣欠欺
款改故敏敢皿盆盛盟窟衰衷袋羞尿屈履弓引弦弧孔孤巾帝崇崎崖崩舟艇酌酪酵酷革丙串丹乏乞乳予互井兵典冗冶凸
凹匠匿卒占卸厘巧式夫契奔奥疑翼翻臣致衡誓豪阜雄雅雇雌魅囚困扇扉戚嫡幽廷弊旗棺椎殉狙畔畜眺窒肘膳舷藻虐
```

### a03

JST (110):

```
辱遂隙骸麓甲畏略矛矢簿素累署翁丁丈瓦勾斗未句永幼朽朴延兆充州防巡拒汽沈尉茨淫媛虞劾釜伎岐既臼凝隅憧拳
玄枯侯控拷唆札伺祉施恣嗣爵狩塾尚承拭芯津娠杉牲婿拙善爽汰替拓濯朕邸溺迭殿毒妊捻俳舶阪藩妃披瓶敷覆陛蔽
璧癖俸岬厄融抑翌吏痢
```

ST (1):

```
豚
```

S (1):

```
曜
```

### b01

J (8):

```
隻装蓋灯机軽徴踪
```

JS (52):

```
区将湾乱当与却旧属秘随湿滞潜担携条楼嘱茎胆堕炉蚕蛮称寝径恋独献双痴欧盗窃尽弥岳状凄誉麦挟虚壮殴横枢断
党踊
```

JT (184):

```
親節餅機誤尋進熱隊薦裏軍問幾並別員華難係製幹許統況為須視網絡術飯貝霧陰島穀膚腸幣養顧潔採導極較彙潤溝
湯漸涼塗湧漁潰濫撲揚擁掛揮損傑構棟嚇詠響僅僕優傘傷偽傾償訂討訓講訟詐診詞誠誕諸諾課調諧謎謹譜糾約綱納
紛紋紡細絞絹緒維綿綻縛縫縮針鈍鋼鈴鉛銘鍋鍛鎮範繭腫脅脈騰壇墳墜墾滅煙煩礎躍負貢財賢販貪貧貯貫貿賀賊賄
資賦賭賠軌軒軟軸輩輝輸轄駒駐罵鶴糧筆築簡審憲賓閑閥閣陣際陸陳郵徹憂懇
```

S (2):

```
届昼
```

JST (3):

```
葛突臭
```

T (1):

```
併
```

### b02

JT (126):

```
懸驚懲慣憤獄禍襲飽飼餌餓誌態剛剝劇勢慶庫療遷跡遜頃項順頒預頰頻規監盤競窮窯羨孫恥艦醜喪臨麗畝興岡沖衝
凍協衛異棄誇奪奮緻韻斬曇曖柵毀渦矯締虜賃鬱緊羅罰罷嘆漢託執殺猶災嵐唄顎韓擬詰詣錮紺摯璽銃詔錠腎詮箋繕
漬諦謄篤訃諭瘍賂搾粧喫箇鎌潟憬瑠汎氾裾彫昇箸塚弔鉢碁
```

J (124):

```
帥亜悪圧扱囲壱鋭駅閲塩縁艶汚応桜穏仮壊懐拡殻刈缶巻陥勧寛関歓観飢亀畿戯犠拠挙郷駆繰勲薫啓渓継鶏芸撃桁
県倹剣圏検権顕厳戸呉娯効鉱込査砕斎歳埼酢咲拶雑桟産賛糸諮児𠮟衆蹴従渋獣縦粛渉証奨乗浄剰畳縄壌嬢譲醸尻
粋酔髄瀬摂絶繊疎遡荘捜挿窓痩総騒蔵臓駄帯沢単団弾
```

### b03

J (65):

```
酎庁聴勅捗逓鉄転塡伝妬稲闘胴働峠徳栃丼弐匂悩脳覇廃抜坂晩姫氷俵浜払雰塀辺変弁歩舗倣砲豊堀満麺訳薬闇喩
癒揺様謡頼覧慄猟寮涙塁隷錬脇枠
```

### c01

T (3):

```
游叶云
```

ST (126):

```
戳步剩馨坡蕨扮于其此些几什假爸哥阿姨穿抬扔抓站爬猴狼蜘蛛螃蟹杏李橘檬葡萄瓜柏桂橡蕾瓣蒲冰睛嘴脖胳膊腿
肚卡邀聊喂澡耍啊呀哦哈嘿嗯唉哎啦咯哇噢咦咱哪怎按尤溜沉洲渠淋溢汛潭浩涂涉涵淘淮湘瀑泛沮浙涕淆淌渣渴湃
滔漾澳扒扛扣扯抖拔拴挎挖挪挺挽捅捆捌捍捎捏捐捧掀掂掏
```

S (121):

```
赛亲为寻组枣队缘质频畅么儿问听产别场员华应记许觉统亿坏妈爷况虽须变脑网络术饭厂举丢捡赶鹅龟蚂蚁蝉虾贝
壳盐柠杨枫雾阴铁岛滩岭庄脸肤肠钻删签码养顾抚吗众导护极毕汇浊润滨泽沟泄汤泪浆渐凉涨泼涌渊渔溃溅滚滥潇
沧沪泻浑淀漓扑扬扰抛拣拥拨挂挠挡挣捂捞损捣
```

### c02

ST (123):

```
掐掠掰揉插揍揣揪揭搏搓搜摔摧撇撒撕撩播擅擒攀杆杖杜杭枉柑框桌桐桶梁梢梧梭梯棍棘棠棵椒楚榆榨榴槐樟橄檀
叮叭吁吆吋吞吠吩吭吮吱吵吻吼呆呵呻咀咋咐咕咧咨咬咳哄哩哮唧唬啃啄喇喘喧嗓嗜嗡嗦嗽嘀嘉噪噩嚷嚼囊仆仇仔
仗仿伍伙估伶佩俄俏俐俘俯俱倔倚倡偎偷傀傻僵僻儡
```

S (127):

```
搁搂搅摇摊撑攒杠杰构柜栋桦桨档检椭檐吓呐呕咏咙响哗哟啰啸嚣仅优伞伤伪侄侠侥侨俭倾偿储订讥讨训讯讲讳讶
讹讼讽诀证识诈诊词译诚诛诞诡询诵诸诺课调谅谆谊谍谎谚谜谣谨谬谱谴纠纤约纫纬纱纲纳纵纷纹纺纽细绊绑绒绕
绘绝绞绢绣绪绰绳维绵绷绸绽缀缅缆缎缚缝缠缤缩缭缰缴针钉
```

### c03

S (130):

```
钙钝钞钢钥钦钧钩钮钳铃铅铝铭铲链销锄锅锈锋锐锡锣锤锦锹锻镀镇镊镰镶艺芦苇苏茧荚荞荡荤荧药萝萧萨葱蒋蔼
蕴肴肿胀胁胧胶脉脏脐脓腊腌腻腾妆娇婴婶坛坝坟坠垒垦垫墙灭灶灿烁烛烟烦烫焕矿砚础硕碍碱眯睁瞒跃跷躏负贡
贤贩贪贫贬贮贯贱贸贺贼贿资赋赌赖赚赞赢轧轨轩软轰轴轿辐辑输辙
```

ST (120):

```
艾芙芥芬芭芹茁范茄茅茉茫茬荔莉菠萌萍董葫蒂蒙蒜蓉蓬蔓蔗蔚蔬薯藐藕蘸胖胡脯腋腔腥腮膏膛臀臂奸妓姆姊姑姚
姥娃娜娩娶婉媳嫂圾址坍坎坤坦垃堡堰塌填墅壕壤炒炕炫炬炮炸烙焊焚熏熔熬燕砌砍砰砸硅碌碎碟碧碰磅磕盯盼眨
眶睬瞄瞎瞪瞻趴趾跌跛跨踩踮蹄蹈蹦蹬蹲躁赫
```

### c04

S (131):

```
驮驯驱驳驴驹驻驼驾骆骇骏骗骚骡骤鸥鸦鸯鸳鸽鸿鹃鹊鹏鹤鹦鹰鹉鲁鲤鲨鳄鳞鲫鲍虱蚀蜕蜗蜡蝇蝎秃秆稳笋笔筑筛
筝简箩箫篱审宪宽宾闪闯闰闲闷闸闹闺阀阁阅阐阔阎阵际陆陈陕隐邓邮邻郑彻忆忏忧怀怜恳恼悬惊惩惫惭惯愤懒狈
狞狰狱猎祸禀补衬袄袭裤饥饱饲饵饶饺饿馅馈馋馒玛环琐瑶旷晋晒晕态
```

ST (119):

```
蚌蚓蚣蚤蚪蛀蛾蜀蜓蝌蝗蝙蝠螺蟀蟋蠢禾秉秤秧稠稻穗籽粥粱粹糊糙糠竿笆笨筐筏筷箍箭篷簇簸宋宏宦寅寇寓寨陋
陌陡陷隧鄙徊徘徙德徽怔怕恍恤恰悄悉悍您惋惕惦惶惹愈愧慧憋憔憨懂懈懊懦狠狡猖猜猾衫袍裳裹褂褥褪玖玫玲玻
珊琢琳琵琶瑞瑰旭旱昂晃晌晰智曙曝忽刁划
```

### c05

S (123):

```
刘刚剑剥办劲势叠广庆库庞废疗疮疯痒痪瘫瘾迁迈迹适逊递逻遥赵趋顷项顺颁颂预颇颈颊颓颖颠颤规觅览盏监盘雳
竖竞穷窍窑窜窥羡尸层屉屡弯孙孪帘帜屿岂岗峦崭耻职联聪舰舱酝酱酿丝严丧临丽乡亩亵兴兽冈冯冲冻凑凛凭凯凿
协卤卧卫厅厉压厌厕厢厦厨县夺奋肃艰衔躯韵飘匀团
```

ST (127):

```
刨刮剃剪剿劈劫叛廓疙疚疤疼痰瘦瘤瘩瘸癌迪逗逛逢遏邃趁趟欣歇效敞敦敲盈盒雹霉霍霎霓霞霸竣竭窄窗窘袁袈裔
襄屏屑屠弘弛孕孟帕帖幢岔峭嵌巍耽聘聚舵艘酉酗酣酥醇醉靶鞍鞠鞭丐丫丰乍乒乓乖乘亦兮冕冤凋凌凳函匆匈匕匪
卜巴夷套奠耀耘耙舔躬雀雁雕靠靡髓黎鼎傣冀咆哼唁啼喻嚎圃
```

### c06

S (85):

```
囱户诬庐怂悯揽搀敛斩昙暧栅栖榄槛橱檩毁毙氢泞洼涡涣澜濒牍獭畴瘪癣皱矫砾秽粤缉缔缕翘耸聂艳莺虏衅裆谄谒
赁赎赡踌辫钠铬锌锰镁闽陨靓颐飕馁馏驭龋紧罗罚罢叹汉辽巩权杀杂污歼犹灾岚
```

ST (165):

```
扎戈佣侈倘孽寥屁屎幌庇彗彪彬忿愕懵捶摹撰擎攘斟斧斯晦曼朦柬桅殃氓氧氮氯汞沐淤渺湛溉溯滓漩潦瀚灸灼焰煽
熨燎爹牡犀犁猩玷琉瑕瓢瓷甄甥甸疆疹痊瘟瘴盎睿瞥矩硼碘碳碾磐禽笙笤箔篡簧糜絮罕罩罹羹翡翩肆肋肛胚胰腆臊
苞茵菱蒿蓑蔫薛虔蚯蛹蜒蜻蠕衍袒訾詹豁豺跋跺躇蹂辜辟迂迄逞逾遁邑郁鄂醛阱隘霄靖鞘髦魁黔黝鼾畸疏皂皓睹瞅
矮紊纂翅翠翰勿允叼叨甩托尖汪牢
```

### c07

ST (154):

```
壹叱敕冢妒姬拂氛靛倩滂跤皎沽骷坷柯珀笠拎聆峙痔晟扳笞伽珈迦洛歪箕璋琅拱曷岑矜衿斌膘侏豫埔馥叟嗔剔裘佯
勺蕃潘敝囤盹腱毽稗婢匾婷淳惇篙毫晾碉粽倌桔弗蹭吾晤佻沾娥峨琰拌洽踞杈叩歧樽攸揩樵琥敖逼辨蔡餐碴柴歹殆
狄翟滇佃侗恫兜堵墩掇哆垛剁扼洱樊菲酚汾烽氟涪袱腑噶嘎皋蛤庚埂耿苟垢圭癸氦邯撼夯郝菏貉涸亨瑚桓豢磺蛔卉
汲悸枷窖
```

S (96):

```
呗驿颚韩拟牺诘勋诣锢绀斋栈挚玺涩铳诏锭肾濑诠笺缮泷渍谛誊笃钵讣辩谕疡炼赂谤镑诅贻峥抡沦镖谩馍垄珑彦缜
沥奂镐琼颉犊锉莱闾咛讷惮殚钗尔麸韧诫鳖摈掺尝郸涤珐矾钒诽赣龚蛊剐刽辊阂烩诲蓟贾钾缄硷饯涧铰秸
```

### c08

ST (181):

```
捷睫藉疥靳兢粳炯韭舅咎疽娟眷撅攫抉浚喀慷垮胯匡盔奎婪榔佬磊擂楞傈撂廖霖吝窿潞戮莽卯酶寐媚醚抿螟牟拇穆
氖淖妮倪拈涅糯啪耪呸裴沛抨烹澎坯砒霹毗痞譬粕莆柒沏畦祁扦仟羌橇撬怯沁卿氰邱泅蛆炔榷冉瓤壬戎茸茹孺汝阮
蕊搔瑟莎煞苫汕墒芍韶邵奢砷噬恃抒孰黍戍漱舜朔嘶粟僳隋蹋酞搪嚏恬迢汀酮彤湍陀烷皖惟魏渭斡毋熙硒矽嘻烯汐
檄匣涎楔忻惺邢墟戌眩蚜衙焉舀噎耶掖曳揖沂彝矣屹肄毅殷臃雍恿釉盂
```

S (69):

```
烬荆痉厩抠侩岿阑谰涝镭棱涟镣陇娄篓掳禄挛滦纶锚铆幂撵啮镍疟沤讫牵钎堑呛蔷颧鳃叁赊慑擞绥挞谭绦锑烃鸵潍
瓮挝钨芜坞铣锨汹嘘绚阉铱绎荫缨莹痈铀舆
```

### c09

ST (22):

```
俞渝禹峪曰匝哉喳漳肇砧臻拯炙盅拄拽篆淄孜鬃柞
```

S (15):

```
辕郧赃铡辗蛰锗帧掷诌瞩锥赘兹邹
```

### d01

T (250):

```
對區賽沒攝從擊讓樣邊韆參帶樁囉鞦叢亞雛壺總棗灣滿緣徵頁亂齊齣於傢當噹與將麵迴聲兒嚮聽卻產體彆纔錶應閤
繫臺颱檯覺鍾舊壞媽爺雖鬚腦濟醫廠舉丟撿趕龜螞蟻蟬蝦殼鹽蘋檸楊楓鐵灘嶺莊臉脣鑽屬刪簽籤賬號碼餵撫鑒佈嗎
眾闆祕據隨畢濤匯濕溼濱澤灑洩滲淚漿漲潑淵濺滾滯瀟滄滬瀉渾澱灕潛擾摺拋搶擔攏揀攔撥撓擋掙擠摀撈搗擱摟攪
攜搖攤撐攢桿槓條櫃樺槳檔樑檢橢樓簷籲噸吳吶嘔嗚嚨鹹嚥啞嘩喲嘯囑囂夥餘姪俠僥僑倆儉儲譏訊諱訝訛諷訣證譯
誅詭詢誦讀諒諄誼諜謊諺謠謬譴纖縴紉紗縱紐絆綁絨繞絕繡綽繩繃綢綜綴緬纜緞纏繽繚韁繳釘鈣鈔鑰欽鈞鉤鈕鉗鋁
```

### d02

T (250):

```
鏟鑄鋪鏈銷鋤鏽鋒銳錫鑼錘鋸鍬鍍鑷鐮鑲藝蘆葦蘇甦莖莢蕎蕩葷熒藥蘿蕭薩蔥蔣矇濛懞衊藹蘊餚脹膽鬍朧膠髒臟臍
膿臘醃膩妝薑嬌娛嬰嬸罈壩壘墊墮牆竈燦爍爛菸燙煥燻礦磚硯碩礙鹼瞇睜瞞蹺蹤躪貶貳賤賴賺贊讚贏軋轟轎輔輻輯
轍馱馴馳驅駁驢駛駝駕駱駭駿騙騷騾驟鷗鴉鴦鴛鴿鴻鵑鵲鵬鸚鷹鵡魯鯉鯊鱷鱗鯽鮑蝨蝕蠶蛻蝸蠟蠅蠍禿稈稱穩黏糞
筍篩箏籮簫籬寬寢壽閃闖閏閒悶閘鬧閨閱闡闊閻陝隱鄧鄭徑禦懺懷憐恆惡噁惱懼慘憊慚懶狽獰猙獵獻稟襯襖襪褲饑
饒餃餡饋饞饅瑪瑣瑤曠顯晉曬暈劃劉颳劑劍辦勁雙敘疊廣龐廢瘡瘋癥癢瘓癡癱癮邁遲遞邏遙趙趨歐頌頗頸頹穎顛顫
```

### d03

T (240):

```
覓覽盞盜靂黴豎竊竅竄窺屍盡儘屜屆屢彌瀰彈孿簾幟嶼歲豈崗嶽巒嶄狀聯聰艙醞醬釀絲嚴豐鄉虧褻獸寫鼕馮淒湊凜
鳳憑凱兇鑿單蔔佔滷鹵臥捲廳厲壓厭廁廂廈廚縣肅艱銜譽軀隸飄麥勸勻睏團糰囪紮戶傭誣廬慫憫挾攬攙斂棲欖檻櫥
檁斃氫濘窪渙瀾瀕牘獺疇癟癬皺礫穢籌粵緝縷翹聳聶艷鶯虛釁襠諂贖贍躊闢辮鈉鉻鋅錳鎂閩隕靚頤颼餒餾馭齲纍鬥
遼鞏樸麴硃雜壯汙剋殲瀋驛毆橫犧勳齋棧辭澀樞瀨鏇瀧斷晝黨缽辯踴煉謗鎊詛貽崢掄淪槍鏢謾饃壟瓏彥縝瀝奐鎬瓊
頡犢氈銼萊閭嚀擰訥憚殫釵爾麩韌誡顱鱉擯摻嘗鄲滌琺礬釩誹贛龔蠱剮劊輥閡燴誨薊賈鉀緘
```

### e01

T (250):

```
餞澗鉸稭燼荊痙廄摳儈巋闌讕澇鐳稜漣鐐隴婁簍擄祿攣灤綸錨鉚冪攆嚙鎳瘧漚訖牽釬塹嗆薔顴鰓賒懾擻綏撻譚絛銻
烴鴕濰甕撾鎢蕪塢銑鍁洶噓絢閹銥繹蔭纓瑩癰鈾輿轅鄖贓鍘輾蟄鍺幀擲謅矚錐贅茲鄒妳唷呃欸甭峯綑綵蒐豔擷枱柺
歎粿蚵嚐恁乂亟亳伉佚佾侑俎俟倨偃僭兕冑冽剌劭勖匐卮吒呎咫哂唏啐喙嗟嘖囹圄坳埠堊墉夙奕妤姒娓娑婀嫣嬉孑
孚宥宸屐岌崆巉巽帑幔庠庾弈彀彿徇徉恙悌悻惆愎慇慝懍戕扈抔拗挹捫揄揶摭擘擢旌昊昕暹曩杳枋栩桎梏梓棣椿楫
槁櫺歆歙殂毓氤氳汨沆沔泗洙洵淙淞渥湄湟溥漪澹濂烜焜熹燧犍狎猗獒玎玟玠珥琦琨瑀瑜璀璨瓠畀疋疵痼瘠睥睨瞑
```

### e02

T (250):

```
矍砥碣磬祚秣稔穹窈窕竺笏筵箴籀粲紈紓紜紲綰緋繾罔羲翊翎翦耄耋聿肱胤脩腴膺臬舛舸芊芮芷苒苓茱荼莞莠菁萃
萋葆蓁蔻蕙蕤薇薰藜蘅蚩蛟蜿蝣蟠蠡衾衽袂褫襦訕詡諤豕赧跚踽蹇迥逍遐遨邈郢酆醴鉅銖錚鍥鏗鐸閎陟隍雋霏靄鞅
顥飫餉饌駑騁髻鳶麒麟黜黠鼐丕丞乩亙佇佞侃俑倀倭偌傖僂儆儋兀凇刎剎剡劬匏匍卅卞厝叵呱咂咄咿哽唔啁啖喟喑
嗤嘍噤噱嚶囁垓垠埒埤堙墀壑夭夼妁妯姍姝娌娣婕媲媾嫦嫻嬋嬪孀孱宓寰尨尪屣岷峇峋崑崙嵊嵩嶙嶸帛帷幃幗廝弋
弒彊徂徨忖忡怏怫恂恚悖悚悱惘惴愀愆愍愴慊慟憮懣懨戟戡拮挈捋捭捺掬掾揆搐摑摒攖斐斛斫旃旎昀昃昶晏暉暝杓
```

### e03

T (239):

```
杞枇柩栲桀桴棹椽楣楹榭槃槭檳櫓欷歃歟殛殞氅汜汩沅泯洌洎涓淬渲溟溱滸漯潺澍澧瀛炷煦熠燔燮爻牖牝犄狒猊猝
猥猷獐玳珂珣琚瑄瑛璜璣畛痍痲瘀瘁癇皈盍眈眥睇睪瞋瞿砭碇磋磯祜祺秫秭稷窠筌箝簌簣籐籟粼糗紘紕絃綬綾緇縉
縑罄羯翱聒肓肼胥脛腠膂舁舂芎苜苡茆茴荀荃莒莘莩菀菅菖菡萁萸葩蒞蒯蓊蓍蔌薜蛞蜃蜷螫袤裊褓褸襁覬覷觚訐詆
詒諉諍諫謔謨譎讒貂賅賑赭趄跆跎踉踝踱蹉蹊蹙蹣躉躑躡軻輒輓輜迤逅逋逵遴邂郜酊酩醍醐醺釗釧鉉鋏鍊鍔鎔鎧鏃
鏤鐫閂閔閫闈闔陂陲隗雉雎霆靼鞣韃韞頷颶飴餮饗饜駟騏騫驀驍髖鬣鬩鰭鷓鷺麾黛黧黯鼴
```

<!-- lots:end -->
