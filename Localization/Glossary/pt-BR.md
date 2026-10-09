# Pocket Wilds glossary: Brazilian Portuguese (pt-BR)

Draft of 9 October 2026, agent TR2. Fixed terms for every text of the game in this language, so later texts stay
consistent. Source: `Localization/Drafts/pt-BR.json`. Forbidden words: `pt-BR_forbidden.txt`. Questions for the
native reviewer: `Drafts/NOTES_TR2.md`.

## Tone

- Brazilian Portuguese, « você » with the friendly third-person imperative: « Choque 3 ovos », « Escove um animal ».
- Warm and playful, with Brazilian diminutives and augmentatives: presentinho, presentão, bocadinho, toquinhos, devagarinho, amigão.
- Buttons: Coletar, Resgatar, Jogar, Continuar, Fechar, Abrir, Cancelar.
- Gender-neutral greetings: « Que bom te ver! », « Boas-vindas ».
- Numbers: decimal comma (× 1,25); « 12% » without a space; units min, h, s with a no-break space.
- Brazilian words: ingressos, petiscos, figurinhas, carrapichos, vitória-régia, bala, celular.
- No em dash.
- Brand: Pocket Wilds stays in Latin letters, never translated.

## Names in sentences

- `species.*.name_the` = o Coelho, o Esquilo, o Ouriço; `biome.*.name_the` = a Campina, a Floresta; zone names carry their article.
- The drafts avoid the contractions (em + o = no, de + o = do, a + o = ao): « para {animal} », direct objects (« Escove {animal} », « Fotografe {animal} »), and « (Campina) » or « no bioma {biome} » with the bare name.
- Still for T39B: « de {species} » in wish.005 and wish.073 would read better as « do Coelho ».

## Plurals

CLDR categories: one (0 and 1) / many / other (many = other). The key without a suffix carries the « other » form (the « one » form
where the English bare key is singular: reward.decoration, reward.discovery_egg, reward.sticker), and reward.gems keeps
the « + » of the level reward card.

| Noun | one | other |
|---|---|---|
| egg | um ovo / {n} ovo | {n} ovos |
| Gem | {n} Gema | {n} Gemas |
| ticket | {n} ingresso | {n} ingressos |
| day | Falta {n} dia | Faltam {n} dias |

## Species

| English | Brazilian Portuguese | Key |
|---|---|---|
| Rabbit | Coelho | `species.rabbit.name` |
| Squirrel | Esquilo | `species.squirrel.name` |
| Hedgehog | Ouriço | `species.hedgehog.name` |
| the Rabbit (sentence form) | o Coelho | `species.rabbit.name_the` |
| the Squirrel (sentence form) | o Esquilo | `species.squirrel.name_the` |
| the Hedgehog (sentence form) | o Ouriço | `species.hedgehog.name_the` |
| species | Espécies | `refuge.source.species` |
| variants | Variantes | `codex.variants` |
| newborn | filhote | wishes, journal |

## Variant colors

| English | Brazilian Portuguese | Key |
|---|---|---|
| Brown | Marrom | `variant.rabbit.brown.name` |
| White | Branco | `variant.rabbit.white.name` |
| Grey | Cinza | `variant.rabbit.grey.name` |
| Black | Preto | `variant.rabbit.black.name` |
| Spotted | Malhado | `variant.rabbit.spotted.name` |
| Sakura | Sakura | `variant.rabbit.sakura.name` |
| Red (squirrel) | Ruivo | `variant.squirrel.red.name` |
| Black (squirrel) | Preto | `variant.squirrel.black.name` |
| Cream | Creme | `variant.squirrel.cream.name` |
| Autumn | Outonal | `variant.squirrel.autumn.name` |
| Mushroom | Cogumelo | `variant.hedgehog.mushroom.name` |

## Currencies and items

| English | Brazilian Portuguese | Key |
|---|---|---|
| Gems | Gemas (sg. Gema), capitalized as a currency | |
| Light | Luz (f), mass noun: « 120 de Luz », « +12% de Luz » | |
| ticket / tickets | {n} ingressos | `reward.tickets.other` |
| treat | Petisco | `sheet.treat` |
| treats | {n} petiscos | `reward.treats.other` |
| the Mushroom Biscuit | Biscoito de cogumelo | `treat.mushroom_biscuit.name` |
| sticker / stickers | {n} figurinhas | `reward.sticker.other` |
| seeds | {n} sementes | `reward.seeds.other` |
| Flower Sugar | Açúcar de flor | `ingredient.flower_sugar.name` |
| decoration | decoração (pl. decorações) | |
| bond points | {n} pontos de vínculo | `reward.bond.other` |
| Discovery egg | Ovo Explorador | `egg.discovery.name` |
| Meadow Egg | Ovo da campina | `egg.grassland.basic.name` |
| Precious Meadow Egg | Ovo precioso da campina | `egg.grassland.focus.name` |
| Starter Pack | Pacote inicial | `shop.starter_pack.title` |
| Flower Crown | Coroa de flores | `entitlement.cosmetic.flower_crown.name` |

## Places

| English | Brazilian Portuguese | Key |
|---|---|---|
| refuge | refúgio (m): « Boas-vindas ao refúgio », « Nível do refúgio » (the more religious-sounding synonym is on the forbidden list) | |
| Meadow (biome; FR Prairie) | Campina | `biome.grassland.name` |
| Forest (biome) | Floresta | `biome.forest.name` |
| biome / biomes | Biomas | `refuge.source.biomes` |
| zone / zones | Zonas | `refuge.source.zones` |
| Flower Meadow (zone) | a Campina florida | `zone.grassland.meadow.name` |
| Windmill Rise (zone) | o Morro do moinho | `zone.grassland.windmillrise.name` |
| Open the Lake | Abra o Lago | `activity.unlock.lake` |
| Open the Mountain | Abra a Montanha | `activity.unlock.mountain` |
| Shop | Loja | `shop.title` |

## Rarities

| English | Brazilian Portuguese | Key |
|---|---|---|
| Common | Comum | `rarity.common` |
| Uncommon | Incomum | `rarity.uncommon` |
| Rare | Raro | `rarity.rare` |
| Epic | Épico | `rarity.epic` |
| Legendary | Lendário | `rarity.legendary` |
| Fabulous | Fabuloso | `rarity.fabulous` |

## Activities

| English | Brazilian Portuguese | Key |
|---|---|---|
| Grooming | Escovação | `activity.grooming.name` |
| Hide Seek | Esconde-esconde | `activity.hide_seek.name` |
| Nut Drop | Chuva de nozes | `activity.nut_drop.name` |
| Memory | Memória de patinhas | `activity.memory.name` |
| Expeditions | Expedições | `activity.expeditions.name` |
| Garden | Jardim | `activity.garden.name` |
| Treasure Fishing | Pesca de tesouros | `activity.treasure_fishing.name` |
| Lily Hop | Pulo na vitória-régia | `activity.lily_hop.name` |
| Baking | Confeitaria | `activity.baking.name` |
| Photo Safari | Safári de fotos | `activity.photo_safari.name` |
| Cuddle Match | Match de abraços | `activity.cuddle_match.name` |
| Glide | Voo planado | `activity.glide.name` |
| Constellations | Constelações | `activity.constellations.name` |
| Firefly Rhythm | Ritmo dos vaga-lumes | `activity.firefly_rhythm.name` |
| Bubbles | Bolhas | `activity.bubbles.name` |
| Tile Puzzle | Quebra-cabeça | `activity.tile_puzzle.name` |
| Burrow Maze | Labirinto da toca | `activity.burrow_maze.name` |
| Grand Contest | Grande Concurso | `activity.grand_contest.name` |

## Features

| English | Brazilian Portuguese | Key |
|---|---|---|
| rendez-vous (Daily Visit) | no name on screen: « Hoje » (the panel), « Presente do dia », « Desejos », « Calendário », « Diário da semana » | |
| Today (panel) | Hoje | `hud.today` |
| Daily gift | Presente do dia | `daily.gift.title` |
| Wishes | Desejos | `wish.title` |
| Calendar | Calendário | `daily.calendar.title` |
| Weekly journal | Diário da semana | `journal.title` |
| album | álbum (pl. álbuns); stickers are « figurinhas », the Brazilian sticker-album word | |
| bond | Vínculo | `sheet.bond` |
| mastery | maestria: « Maestria da Campina » | |
| Codex | Códice | `codex.title` |
| Upgrades | Melhorias | `upgrades.title` |
| Activities (hub) | Atividades | `activity.hub.title` |
| challenge | desafio (pl. desafios) | |
| Practice | Treino | `activity.practice` |
| record | Novo recorde | `activity.result.record` |
| streak | Sequência ×{n} | `activity.hud.streak` |
| Refuge level | Nível do refúgio | `refuge.level.title` |

## UI verbs and buttons

| English | Brazilian Portuguese | Key |
|---|---|---|
| Collect | Coletar | `ui.collect.button` |
| Claim | Resgatar | `today.claim` |
| Claim all | Resgatar tudo | `today.claim_all` |
| Play | Jogar | `hud.play` |
| Continue | Continuar | `summary.continue` |
| Close | Fechar | `today.close` |
| Open | Abrir | `zone.open.button` |
| OK | OK | `ui.ok.button` |
| Cancel | Cancelar | `ui.cancel.button` |
| Swap | Trocar | `wish.swap` |
| Stamp | Carimbar | `daily.calendar.stamp` |
| Resume | Continuar | `activity.pause.resume` |
| Quit | Sair | `activity.pause.quit` |
| Back | Voltar | `activity.result.back` |
| Play again | Jogar de novo | `activity.result.replay` |
| Details | Detalhes | `sheet.details.button` |
| Groom (button) | Escovar | `activity.grooming.sheet_groom` |
| Free | Grátis | `egg.price.free` |
| New | Novo | `codex.new` |
